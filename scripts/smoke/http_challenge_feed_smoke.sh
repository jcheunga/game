#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/http-feed-smoke.XXXXXX")"
SERVER_LOG="$TMP_DIR/server.log"
REQUEST_LOG="$TMP_DIR/request.txt"
SMOKE_LOG="$TMP_DIR/smoke.log"
SERVER_PID=""
SMOKE_STATUS=0
PORT="${HTTP_FEED_SMOKE_PORT:-$((19500 + RANDOM % 1000))}"
SAVE_SUFFIX="feed_smoke_${PORT}_$$"

cleanup() {
	if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
		kill "$SERVER_PID" 2>/dev/null || true
	fi

	if [[ "${KEEP_HTTP_FEED_SMOKE_LOGS:-0}" != "1" && $SMOKE_STATUS -eq 0 ]]; then
		rm -rf "$TMP_DIR"
	fi
}
trap cleanup EXIT

cd "$ROOT_DIR"

/usr/bin/python3 - "$PORT" "$REQUEST_LOG" >"$SERVER_LOG" 2>&1 <<'PY' &
import json
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer

port = int(sys.argv[1])
request_log = sys.argv[2]

class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        with open(request_log, "w", encoding="utf-8") as handle:
            handle.write(self.path)

        response = {
            "status": "ok",
            "message": "stub feed fetched",
            "items": [
                {
                    "id": "remote_route_trial",
                    "title": "Remote Route Trial",
                    "summary": "Backend-authored opener for remote score races.",
                    "code": "CH-01-PRS-7001",
                    "lockedDeckUnitIds": ["brawler", "shooter", "defender"]
                },
                {
                    "id": "remote_pressure",
                    "title": "Remote Pressure Test",
                    "summary": "Backend-authored higher-pressure board.",
                    "code": "CH-01-RAT-7002",
                    "lockedDeckUnitIds": ["brawler", "ranger", "defender"]
                }
            ]
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

echo "Running HTTP challenge feed smoke..."
godot --headless --path . -- \
	--feed-smoke-provider=http_api \
	--feed-smoke-endpoint="http://127.0.0.1:${PORT}/challenge-sync" \
	--save-suffix="${SAVE_SUFFIX}" >"$SMOKE_LOG" 2>&1 || SMOKE_STATUS=$?

wait "$SERVER_PID" || true

if [[ $SMOKE_STATUS -ne 0 ]]; then
	echo "HTTP challenge feed smoke failed."
	echo "--- smoke log ---"
	cat "$SMOKE_LOG"
	echo "--- server log ---"
	cat "$SERVER_LOG"
	exit 1
fi

if ! grep -q "FEED_SMOKE PASS" "$SMOKE_LOG"; then
	echo "Feed smoke exited cleanly but did not report pass."
	cat "$SMOKE_LOG"
	exit 1
fi

if [[ ! -s "$REQUEST_LOG" ]]; then
	echo "Feed stub did not capture a request."
	cat "$SMOKE_LOG"
	exit 1
fi

echo "HTTP challenge feed smoke passed."
echo "Smoke summary: $(grep 'FEED_SMOKE PASS' "$SMOKE_LOG" | tail -n 1)"
