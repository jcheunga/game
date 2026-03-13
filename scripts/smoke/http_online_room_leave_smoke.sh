#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/http-room-leave-smoke.XXXXXX")"
SERVER_LOG="$TMP_DIR/server.log"
REQUEST_LOG="$TMP_DIR/request.json"
SMOKE_LOG="$TMP_DIR/smoke.log"
RUNNER_CSPROJ="$TMP_DIR/RoomLeaveSmoke.csproj"
RUNNER_PROGRAM="$TMP_DIR/Program.cs"
SERVER_PID=""
SMOKE_STATUS=0
PORT="${HTTP_ROOM_LEAVE_SMOKE_PORT:-$((20700 + RANDOM % 1000))}"

cleanup() {
	if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
		kill "$SERVER_PID" 2>/dev/null || true
	fi

	if [[ "${KEEP_HTTP_ROOM_LEAVE_SMOKE_LOGS:-0}" != "1" && $SMOKE_STATUS -eq 0 ]]; then
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
            "message": "stub room leave applied",
            "roomId": "ROOM-HOST-11",
            "boardCode": "CH-05-BLK-5110",
            "ticketId": "HOST-HTTP-01",
            "actionId": "leave_room",
            "readyState": False,
            "processedAtUnixSeconds": 1710003600
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

        var provider = new HttpApiOnlineRoomActionProvider(args[0]);
        var ticket = new OnlineRoomJoinTicket
        {
            RoomId = "ROOM-HOST-11",
            RoomTitle = "SmokeConvoy Relay",
            BoardCode = "CH-05-BLK-5110",
            TicketId = "HOST-HTTP-01",
            JoinToken = "token-http-host-01",
            Status = "hosted",
            SeatLabel = "host seat"
        };
        var request = new OnlineRoomActionRequest
        {
            RoomId = ticket.RoomId,
            BoardCode = ticket.BoardCode,
            TicketId = ticket.TicketId,
            JoinToken = ticket.JoinToken,
            PlayerProfileId = "CVY-SMOKE",
            PlayerCallsign = "SmokeConvoy",
            ActionId = "leave_room",
            ReadyState = false,
            RequestedAtUnixSeconds = 1710003500
        };

        var result = provider.SendAction(ticket, request);
        if (!string.Equals(result.Status, "accepted", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"expected accepted room leave, got {result.Status}");
            return 1;
        }

        if (!string.Equals(result.ActionId, "leave_room", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"expected leave_room action echo, got {result.ActionId}");
            return 1;
        }

        Console.WriteLine(
            $"ROOM_LEAVE_SMOKE PASS  |  provider {result.ProviderDisplayName}  |  room {result.RoomId}  |  action {result.ActionId}");
        return 0;
    }
}
CS

echo "Running HTTP online room leave smoke..."
dotnet run --project "$RUNNER_CSPROJ" -- "http://127.0.0.1:${PORT}/challenge-room-action" >"$SMOKE_LOG" 2>&1 || SMOKE_STATUS=$?

if [[ $SMOKE_STATUS -ne 0 && -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
	kill "$SERVER_PID" 2>/dev/null || true
fi

wait "$SERVER_PID" || true

if [[ $SMOKE_STATUS -ne 0 ]]; then
	echo "HTTP online room leave smoke failed."
	echo "--- smoke log ---"
	cat "$SMOKE_LOG"
	echo "--- server log ---"
	cat "$SERVER_LOG"
	exit 1
fi

if ! grep -q "ROOM_LEAVE_SMOKE PASS" "$SMOKE_LOG"; then
	echo "Room leave smoke exited cleanly but did not report pass."
	cat "$SMOKE_LOG"
	exit 1
fi

if [[ ! -s "$REQUEST_LOG" ]]; then
	echo "Room leave stub did not capture a request."
	cat "$SMOKE_LOG"
	exit 1
fi

if ! grep -q '"actionId":"leave_room"' "$REQUEST_LOG"; then
	echo "Room leave request payload did not include the expected leave_room action."
	cat "$REQUEST_LOG"
	exit 1
fi

echo "HTTP online room leave smoke passed."
echo "Smoke summary: $(grep 'ROOM_LEAVE_SMOKE PASS' "$SMOKE_LOG" | tail -n 1)"
