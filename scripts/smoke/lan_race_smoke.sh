#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/lan-race-smoke.XXXXXX")"
HOST_LOG="$TMP_DIR/host.log"
CLIENT_LOG="$TMP_DIR/client.log"
HOST_STATUS=0
CLIENT_STATUS=0
HOST_PID=""
CLIENT_PID=""
RUN_TAG="${LAN_SMOKE_RUN_TAG:-$$_$RANDOM}"
HOST_SAVE_SUFFIX="lan_smoke_host_${RUN_TAG}"
CLIENT_SAVE_SUFFIX="lan_smoke_client_${RUN_TAG}"

cleanup() {
	if [[ -n "$HOST_PID" ]] && kill -0 "$HOST_PID" 2>/dev/null; then
		kill "$HOST_PID" 2>/dev/null || true
	fi

	if [[ -n "$CLIENT_PID" ]] && kill -0 "$CLIENT_PID" 2>/dev/null; then
		kill "$CLIENT_PID" 2>/dev/null || true
	fi

	if [[ "${KEEP_LAN_SMOKE_LOGS:-0}" != "1" && $HOST_STATUS -eq 0 && $CLIENT_STATUS -eq 0 ]]; then
		rm -rf "$TMP_DIR"
	fi
}
trap cleanup EXIT

cd "$ROOT_DIR"

echo "Running LAN smoke host/client pair..."
godot --headless --path . -- --lan-smoke-role=host --save-suffix="${HOST_SAVE_SUFFIX}" >"$HOST_LOG" 2>&1 &
HOST_PID=$!
sleep 1
godot --headless --path . -- --lan-smoke-role=client --lan-smoke-address=127.0.0.1 --save-suffix="${CLIENT_SAVE_SUFFIX}" >"$CLIENT_LOG" 2>&1 &
CLIENT_PID=$!

wait "$HOST_PID" || HOST_STATUS=$?
wait "$CLIENT_PID" || CLIENT_STATUS=$?

if [[ $HOST_STATUS -ne 0 || $CLIENT_STATUS -ne 0 ]]; then
	echo "LAN smoke test failed."
	echo "--- host log ---"
	cat "$HOST_LOG"
	echo "--- client log ---"
	cat "$CLIENT_LOG"
	exit 1
fi

if ! grep -q "LAN_SMOKE PASS" "$HOST_LOG"; then
	echo "Host process exited cleanly but did not report pass."
	cat "$HOST_LOG"
	exit 1
fi

if ! grep -q "LAN_SMOKE PASS" "$CLIENT_LOG"; then
	echo "Client process exited cleanly but did not report pass."
	cat "$CLIENT_LOG"
	exit 1
fi

echo "LAN smoke test passed."
echo "Host summary: $(grep 'LAN_SMOKE PASS' "$HOST_LOG" | tail -n 1)"
echo "Client summary: $(grep 'LAN_SMOKE PASS' "$CLIENT_LOG" | tail -n 1)"
