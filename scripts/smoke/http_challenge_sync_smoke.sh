#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/http-sync-smoke.XXXXXX")"
SERVER_LOG="$TMP_DIR/server.log"
REQUEST_LOG="$TMP_DIR/request.json"
SMOKE_LOG="$TMP_DIR/smoke.log"
SERVER_PID=""
SMOKE_STATUS=0

cleanup() {
	if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
		kill "$SERVER_PID" 2>/dev/null || true
	fi

	if [[ "${KEEP_HTTP_SYNC_SMOKE_LOGS:-0}" != "1" && $SMOKE_STATUS -eq 0 ]]; then
		rm -rf "$TMP_DIR"
	fi
}
trap cleanup EXIT

PORT="${HTTP_SYNC_SMOKE_PORT:-$((18000 + RANDOM % 1000))}"
SAVE_SUFFIX="sync_smoke_http_${PORT}_$$"
cd "$ROOT_DIR"

/usr/bin/python3 - "$PORT" "$REQUEST_LOG" >"$SERVER_LOG" 2>&1 <<'PY' &
import json
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer

port = int(sys.argv[1])
request_log = sys.argv[2]

class Handler(BaseHTTPRequestHandler):
    def do_POST(self):
        length = int(self.headers.get("Content-Length", "0"))
        body = self.rfile.read(length).decode("utf-8")
        with open(request_log, "w", encoding="utf-8") as handle:
            handle.write(body)

        payload = json.loads(body)
        batch = payload.get("batch", {})
        accepted = [entry.get("submissionId", "") for entry in batch.get("submissions", []) if entry.get("submissionId")]
        response = {
            "batchId": batch.get("batchId", ""),
            "status": "accepted",
            "message": "stub accepted batch",
            "acceptedSubmissionIds": accepted,
            "rejectedSubmissionIds": []
        }
        encoded = json.dumps(response).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(encoded)))
        self.end_headers()
        self.wfile.write(encoded)

    def log_message(self, format, *args):
        return

HTTPServer.allow_reuse_address = True
server = HTTPServer(("127.0.0.1", port), Handler)
server.handle_request()
PY
SERVER_PID=$!
sleep 1

echo "Running HTTP challenge sync smoke..."
godot --headless --path . -- \
	--sync-smoke-provider=http_api \
	--sync-smoke-endpoint="http://127.0.0.1:${PORT}/challenge-sync" \
	--save-suffix="${SAVE_SUFFIX}" >"$SMOKE_LOG" 2>&1 || SMOKE_STATUS=$?

wait "$SERVER_PID" || true

if [[ $SMOKE_STATUS -ne 0 ]]; then
	echo "HTTP challenge sync smoke failed."
	echo "--- smoke log ---"
	cat "$SMOKE_LOG"
	echo "--- server log ---"
	cat "$SERVER_LOG"
	exit 1
fi

if ! grep -q "SYNC_SMOKE PASS" "$SMOKE_LOG"; then
	echo "Smoke process exited cleanly but did not report pass."
	cat "$SMOKE_LOG"
	exit 1
fi

if [[ ! -s "$REQUEST_LOG" ]]; then
	echo "HTTP stub did not capture a request."
	cat "$SMOKE_LOG"
	exit 1
fi

echo "HTTP challenge sync smoke passed."
echo "Smoke summary: $(grep 'SYNC_SMOKE PASS' "$SMOKE_LOG" | tail -n 1)"
