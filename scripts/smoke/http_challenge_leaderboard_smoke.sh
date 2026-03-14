#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/http-leaderboard-smoke.XXXXXX")"
SERVER_LOG="$TMP_DIR/server.log"
REQUEST_LOG="$TMP_DIR/request.txt"
SMOKE_LOG="$TMP_DIR/smoke.log"
SERVER_PID=""
SMOKE_STATUS=0
PORT="${HTTP_LEADERBOARD_SMOKE_PORT:-$((19000 + RANDOM % 1000))}"
SAVE_SUFFIX="leaderboard_smoke_${PORT}_$$"
CODE="CH-01-PRS-5151"

cleanup() {
	if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
		kill "$SERVER_PID" 2>/dev/null || true
	fi

	if [[ "${KEEP_HTTP_LEADERBOARD_SMOKE_LOGS:-0}" != "1" && $SMOKE_STATUS -eq 0 ]]; then
		rm -rf "$TMP_DIR"
	fi
}
trap cleanup EXIT

cd "$ROOT_DIR"

/usr/bin/python3 - "$PORT" "$REQUEST_LOG" "$CODE" >"$SERVER_LOG" 2>&1 <<'PY' &
import json
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer
from urllib.parse import urlparse, parse_qs

port = int(sys.argv[1])
request_log = sys.argv[2]
code = sys.argv[3]

class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        query = parse_qs(urlparse(self.path).query)
        with open(request_log, "w", encoding="utf-8") as handle:
            handle.write(self.path)

        response = {
            "code": query.get("code", [code])[0],
            "status": "ok",
            "message": "stub leaderboard fetched",
            "entries": [
                {
                    "rank": 1,
                    "code": code,
                    "playerCallsign": "RemoteAce",
                    "playerProfileId": "CVY-REMOTEACE",
                    "score": 1880,
                    "starsEarned": 3,
                    "hullPercent": 92,
                    "elapsedSeconds": 24.2,
                    "usedLockedDeck": False,
                    "playedAtUnixSeconds": 1710000000
                },
                {
                    "rank": 2,
                    "code": code,
                    "playerCallsign": "RunnerTwo",
                    "playerProfileId": "CVY-RUNNERTWO",
                    "score": 1710,
                    "starsEarned": 2,
                    "hullPercent": 88,
                    "elapsedSeconds": 27.4,
                    "usedLockedDeck": True,
                    "playedAtUnixSeconds": 1710000100
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

echo "Running HTTP challenge leaderboard smoke..."
godot --headless --path . -- \
	--leaderboard-smoke-provider=http_api \
	--leaderboard-smoke-endpoint="http://127.0.0.1:${PORT}/challenge-sync" \
	--leaderboard-smoke-code="${CODE}" \
	--save-suffix="${SAVE_SUFFIX}" >"$SMOKE_LOG" 2>&1 || SMOKE_STATUS=$?

wait "$SERVER_PID" || true

if [[ $SMOKE_STATUS -ne 0 ]]; then
	echo "HTTP challenge leaderboard smoke failed."
	echo "--- smoke log ---"
	cat "$SMOKE_LOG"
	echo "--- server log ---"
	cat "$SERVER_LOG"
	exit 1
fi

if ! grep -q "LEADERBOARD_SMOKE PASS" "$SMOKE_LOG"; then
	echo "Leaderboard smoke exited cleanly but did not report pass."
	cat "$SMOKE_LOG"
	exit 1
fi

if [[ ! -s "$REQUEST_LOG" ]]; then
	echo "Leaderboard stub did not capture a request."
	cat "$SMOKE_LOG"
	exit 1
fi

echo "HTTP challenge leaderboard smoke passed."
echo "Smoke summary: $(grep 'LEADERBOARD_SMOKE PASS' "$SMOKE_LOG" | tail -n 1)"
