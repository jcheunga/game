#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/http-room-lease-smoke.XXXXXX")"
SERVER_LOG="$TMP_DIR/server.log"
REQUEST_LOG="$TMP_DIR/request.json"
SMOKE_LOG="$TMP_DIR/smoke.log"
RUNNER_CSPROJ="$TMP_DIR/RoomLeaseSmoke.csproj"
RUNNER_PROGRAM="$TMP_DIR/Program.cs"
SERVER_PID=""
SMOKE_STATUS=0
PORT="${HTTP_ROOM_LEASE_SMOKE_PORT:-$((21300 + RANDOM % 1000))}"

cleanup() {
	if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
		kill "$SERVER_PID" 2>/dev/null || true
	fi

	if [[ "${KEEP_HTTP_ROOM_LEASE_SMOKE_LOGS:-0}" != "1" && $SMOKE_STATUS -eq 0 ]]; then
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
            "status": "accepted",
            "message": "stub room seat lease refreshed",
            "roomId": "ROOM-LOCK-02",
            "boardCode": "CH-03-RAT-6222",
            "ticketId": "JOIN-HTTP-01",
            "joinToken": "token-http-join-02",
            "expiresAtUnixSeconds": 1710001800,
            "renewedAtUnixSeconds": 1710001500
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

        var provider = new HttpApiOnlineRoomSeatLeaseProvider(args[0]);
        var ticket = new OnlineRoomJoinTicket
        {
            RoomId = "ROOM-LOCK-02",
            RoomTitle = "Locked Squad Scrim",
            BoardCode = "CH-03-RAT-6222",
            TicketId = "JOIN-HTTP-01",
            JoinToken = "token-http-join-01",
            ExpiresAtUnixSeconds = 1710001200
        };
        var request = new OnlineRoomSeatLeaseRequest
        {
            RoomId = ticket.RoomId,
            BoardCode = ticket.BoardCode,
            TicketId = ticket.TicketId,
            JoinToken = ticket.JoinToken,
            PlayerProfileId = "CVY-SMOKE",
            PlayerCallsign = "SmokeConvoy",
            ForceRefresh = false,
            RequestedAtUnixSeconds = 1710001500
        };

        var result = provider.RenewSeat(ticket, request);
        if (!string.Equals(result.Status, "accepted", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"expected accepted seat lease, got {result.Status}");
            return 1;
        }

        if (result.ExpiresAtUnixSeconds <= ticket.ExpiresAtUnixSeconds)
        {
            Console.Error.WriteLine("seat lease expiry was not extended");
            return 1;
        }

        if (!string.Equals(result.JoinToken, "token-http-join-02", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("seat lease did not return the refreshed join token");
            return 1;
        }

        Console.WriteLine(
            $"ROOM_LEASE_SMOKE PASS  |  provider {result.ProviderDisplayName}  |  room {result.RoomId}  |  expiry {result.ExpiresAtUnixSeconds}");
        return 0;
    }
}
CS

echo "Running HTTP online room lease smoke..."
dotnet run --project "$RUNNER_CSPROJ" -- "http://127.0.0.1:${PORT}/challenge-room-lease" >"$SMOKE_LOG" 2>&1 || SMOKE_STATUS=$?

if [[ $SMOKE_STATUS -ne 0 && -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
	kill "$SERVER_PID" 2>/dev/null || true
fi

wait "$SERVER_PID" || true

if [[ $SMOKE_STATUS -ne 0 ]]; then
	echo "HTTP online room lease smoke failed."
	echo "--- smoke log ---"
	cat "$SMOKE_LOG"
	echo "--- server log ---"
	cat "$SERVER_LOG"
	exit 1
fi

if ! grep -q "ROOM_LEASE_SMOKE PASS" "$SMOKE_LOG"; then
	echo "Room lease smoke exited cleanly but did not report pass."
	cat "$SMOKE_LOG"
	exit 1
fi

if [[ ! -s "$REQUEST_LOG" ]]; then
	echo "Room lease stub did not capture a request."
	cat "$SMOKE_LOG"
	exit 1
fi

if ! grep -q '"forceRefresh":false' "$REQUEST_LOG"; then
	echo "Room lease request payload did not include the expected forceRefresh field."
	cat "$REQUEST_LOG"
	exit 1
fi

echo "HTTP online room lease smoke passed."
echo "Smoke summary: $(grep 'ROOM_LEASE_SMOKE PASS' "$SMOKE_LOG" | tail -n 1)"
