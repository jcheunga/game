#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/http-room-join-smoke.XXXXXX")"
SERVER_LOG="$TMP_DIR/server.log"
REQUEST_LOG="$TMP_DIR/request.json"
SMOKE_LOG="$TMP_DIR/smoke.log"
RUNNER_CSPROJ="$TMP_DIR/RoomJoinSmoke.csproj"
RUNNER_PROGRAM="$TMP_DIR/Program.cs"
SERVER_PID=""
SMOKE_STATUS=0
PORT="${HTTP_ROOM_JOIN_SMOKE_PORT:-$((19700 + RANDOM % 1000))}"

cleanup() {
	if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
		kill "$SERVER_PID" 2>/dev/null || true
	fi

	if [[ "${KEEP_HTTP_ROOM_JOIN_SMOKE_LOGS:-0}" != "1" && $SMOKE_STATUS -eq 0 ]]; then
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
            "message": "stub join ticket granted",
            "roomId": "ROOM-LOCK-02",
            "roomTitle": "Locked Squad Scrim",
            "boardCode": "CH-03-RAT-6222",
            "ticketId": "JOIN-HTTP-01",
            "joinToken": "token-http-join-01",
            "transportHint": "relay_room",
            "relayEndpoint": "wss://relay.example.invalid/room-lock-02",
            "seatLabel": "locked-squad seat",
            "expiresAtUnixSeconds": 1710001200,
            "usesLockedDeck": True,
            "lockedDeckUnitIds": ["brawler", "shooter", "defender"]
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

        var provider = new HttpApiOnlineRoomJoinProvider(args[0]);
        var room = new OnlineRoomDirectoryEntry
        {
            RoomId = "ROOM-LOCK-02",
            Title = "Locked Squad Scrim",
            BoardCode = "CH-03-RAT-6222",
            UsesLockedDeck = true,
            LockedDeckUnitIds = new[] { "brawler", "shooter", "defender" }
        };
        var request = new OnlineRoomJoinRequest
        {
            RoomId = room.RoomId,
            BoardCode = room.BoardCode,
            PlayerProfileId = "CVY-SMOKE",
            PlayerCallsign = "SmokeConvoy",
            WantsLockedDeckSeat = true,
            RequestedAtUnixSeconds = 1710000000
        };

        var ticket = provider.RequestJoin(room, request);
        if (!string.Equals(ticket.Status, "accepted", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"expected accepted join ticket, got {ticket.Status}");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(ticket.JoinToken) || string.IsNullOrWhiteSpace(ticket.RelayEndpoint))
        {
            Console.Error.WriteLine("join ticket is missing token or relay endpoint");
            return 1;
        }

        if (!ticket.UsesLockedDeck || ticket.LockedDeckUnitIds.Length < 3)
        {
            Console.Error.WriteLine("expected locked-deck join ticket payload");
            return 1;
        }

        Console.WriteLine(
            $"ROOM_JOIN_SMOKE PASS  |  provider {ticket.ProviderDisplayName}  |  room {ticket.RoomId}  |  seat {ticket.SeatLabel}");
        return 0;
    }
}
CS

echo "Running HTTP online room join smoke..."
dotnet run --project "$RUNNER_CSPROJ" -- "http://127.0.0.1:${PORT}/challenge-room-join" >"$SMOKE_LOG" 2>&1 || SMOKE_STATUS=$?

if [[ $SMOKE_STATUS -ne 0 && -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
	kill "$SERVER_PID" 2>/dev/null || true
fi

wait "$SERVER_PID" || true

if [[ $SMOKE_STATUS -ne 0 ]]; then
	echo "HTTP online room join smoke failed."
	echo "--- smoke log ---"
	cat "$SMOKE_LOG"
	echo "--- server log ---"
	cat "$SERVER_LOG"
	exit 1
fi

if ! grep -q "ROOM_JOIN_SMOKE PASS" "$SMOKE_LOG"; then
	echo "Room join smoke exited cleanly but did not report pass."
	cat "$SMOKE_LOG"
	exit 1
fi

if [[ ! -s "$REQUEST_LOG" ]]; then
	echo "Room join stub did not capture a request."
	cat "$SMOKE_LOG"
	exit 1
fi

if ! grep -q '"roomId"' "$REQUEST_LOG"; then
	echo "Room join request payload did not include the expected roomId field."
	cat "$REQUEST_LOG"
	exit 1
fi

echo "HTTP online room join smoke passed."
echo "Smoke summary: $(grep 'ROOM_JOIN_SMOKE PASS' "$SMOKE_LOG" | tail -n 1)"
