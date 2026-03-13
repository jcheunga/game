#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/http-player-profile-smoke.XXXXXX")"
SERVER_LOG="$TMP_DIR/server.log"
REQUEST_LOG="$TMP_DIR/request.json"
SMOKE_LOG="$TMP_DIR/smoke.log"
RUNNER_CSPROJ="$TMP_DIR/PlayerProfileSmoke.csproj"
RUNNER_PROGRAM="$TMP_DIR/Program.cs"
SERVER_PID=""
SMOKE_STATUS=0
PORT="${HTTP_PLAYER_PROFILE_SMOKE_PORT:-$((20800 + RANDOM % 1000))}"

cleanup() {
	if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
		kill "$SERVER_PID" 2>/dev/null || true
	fi

	if [[ "${KEEP_HTTP_PLAYER_PROFILE_SMOKE_LOGS:-0}" != "1" && $SMOKE_STATUS -eq 0 ]]; then
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
    def do_POST(self):
        body = self.rfile.read(int(self.headers.get("Content-Length", "0") or "0")).decode("utf-8")
        with open(request_log, "w", encoding="utf-8") as handle:
            handle.write(body)

        response = {
            "status": "ok",
            "message": "stub player profile synced",
            "playerProfileId": "CVY-SMOKE",
            "playerCallsign": "SmokeConvoy",
            "authState": "verified",
            "sessionToken": "token-smoke-verified",
            "canSubmitChallenges": True,
            "canJoinRooms": True,
            "relayEnabled": True,
            "syncedAtUnixSeconds": 1710003900
        }
        encoded = json.dumps(response).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(encoded)))
        self.end_headers()
        self.wfile.write(encoded)

    def log_message(self, format, *args):
        return

server = HTTPServer(("127.0.0.1", port), Handler)
server.handle_request()
PY
SERVER_PID=$!
sleep 1

cat >"$RUNNER_CSPROJ" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>disable</Nullable>
    <RollForward>Major</RollForward>
    <GodotProjectDir>$ROOT_DIR</GodotProjectDir>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$ROOT_DIR/Game.csproj" />
  </ItemGroup>
</Project>
EOF

cat >"$RUNNER_PROGRAM" <<'CS'
using System;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.Error.WriteLine("missing endpoint");
            return 1;
        }

        var provider = new HttpApiPlayerProfileSyncProvider(args[0]);
        var request = new PlayerProfileSyncRequest
        {
            PlayerProfileId = "CVY-SMOKE",
            PlayerCallsign = "SmokeConvoy",
            SyncProviderId = ChallengeSyncProviderCatalog.HttpApiId,
            RequestedAtUnixSeconds = 1710003800
        };

        var snapshot = provider.SyncProfile(request);
        if (!string.Equals(snapshot.Status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"expected ok player profile status, got {snapshot.Status}");
            return 1;
        }

        if (!string.Equals(snapshot.AuthState, "verified", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"expected verified auth state, got {snapshot.AuthState}");
            return 1;
        }

        if (!string.Equals(snapshot.PlayerProfileId, "CVY-SMOKE", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"expected CVY-SMOKE profile id, got {snapshot.PlayerProfileId}");
            return 1;
        }

        Console.WriteLine(
            $"PLAYER_PROFILE_SMOKE PASS  |  provider {snapshot.ProviderDisplayName}  |  profile {snapshot.PlayerProfileId}  |  auth {snapshot.AuthState}");
        return 0;
    }
}
CS

echo "Running HTTP player profile smoke..."
dotnet run --project "$RUNNER_CSPROJ" -- "http://127.0.0.1:${PORT}/player-profile" >"$SMOKE_LOG" 2>&1 || SMOKE_STATUS=$?

if [[ $SMOKE_STATUS -ne 0 && -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
	kill "$SERVER_PID" 2>/dev/null || true
fi

wait "$SERVER_PID" || true

if [[ $SMOKE_STATUS -ne 0 ]]; then
	echo "HTTP player profile smoke failed."
	echo "--- smoke log ---"
	cat "$SMOKE_LOG"
	echo "--- server log ---"
	cat "$SERVER_LOG"
	exit 1
fi

if ! grep -q "PLAYER_PROFILE_SMOKE PASS" "$SMOKE_LOG"; then
	echo "Player profile smoke exited cleanly but did not report pass."
	cat "$SMOKE_LOG"
	exit 1
fi

if [[ ! -s "$REQUEST_LOG" ]]; then
	echo "Player profile stub did not capture a request."
	cat "$SMOKE_LOG"
	exit 1
fi

if ! grep -q '"playerProfileId":"CVY-SMOKE"' "$REQUEST_LOG"; then
	echo "Player profile request payload did not include the expected playerProfileId field."
	cat "$REQUEST_LOG"
	exit 1
fi

if ! grep -q '"playerCallsign":"SmokeConvoy"' "$REQUEST_LOG"; then
	echo "Player profile request payload did not include the expected playerCallsign field."
	cat "$REQUEST_LOG"
	exit 1
fi

echo "HTTP player profile smoke passed."
echo "Smoke summary: $(grep 'PLAYER_PROFILE_SMOKE PASS' "$SMOKE_LOG" | tail -n 1)"
