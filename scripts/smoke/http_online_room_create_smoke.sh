#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/http-room-create-smoke.XXXXXX")"
SERVER_LOG="$TMP_DIR/server.log"
REQUEST_LOG="$TMP_DIR/request.json"
SMOKE_LOG="$TMP_DIR/smoke.log"
RUNNER_CSPROJ="$TMP_DIR/RoomCreateSmoke.csproj"
RUNNER_PROGRAM="$TMP_DIR/Program.cs"
SERVER_PID=""
SMOKE_STATUS=0
PORT="${HTTP_ROOM_CREATE_SMOKE_PORT:-$((20000 + RANDOM % 1000))}"

cleanup() {
	if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
		kill "$SERVER_PID" 2>/dev/null || true
	fi

	if [[ "${KEEP_HTTP_ROOM_CREATE_SMOKE_LOGS:-0}" != "1" && $SMOKE_STATUS -eq 0 ]]; then
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
            "status": "lobby",
            "message": "stub room host published",
            "roomId": "ROOM-HOST-11",
            "title": "SmokeConvoy Relay",
            "summary": "Hosted internet room for the selected board.",
            "hostCallsign": "SmokeConvoy",
            "boardCode": "CH-05-BLK-5110",
            "boardTitle": "City Route S5 Clocktower",
            "currentPlayers": 1,
            "maxPlayers": 4,
            "spectatorCount": 0,
            "region": "global",
            "transportHint": "relay_room",
            "relayEndpoint": "wss://relay.example.invalid/room-host-11",
            "usesLockedDeck": True,
            "lockedDeckUnitIds": ["brawler", "shooter", "defender"],
            "hostTicket": {
                "status": "hosted",
                "message": "host seat reserved",
                "ticketId": "HOST-HTTP-01",
                "joinToken": "token-http-host-01",
                "seatLabel": "host seat",
                "transportHint": "relay_room",
                "relayEndpoint": "wss://relay.example.invalid/room-host-11",
                "expiresAtUnixSeconds": 1710002400,
                "usesLockedDeck": True,
                "lockedDeckUnitIds": ["brawler", "shooter", "defender"]
            }
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

        var provider = new HttpApiOnlineRoomCreateProvider(args[0]);
        var request = new OnlineRoomCreateRequest
        {
            BoardCode = "CH-05-BLK-5110",
            BoardTitle = "City Route S5 Clocktower",
            PlayerProfileId = "CVY-SMOKE",
            PlayerCallsign = "SmokeConvoy",
            Region = "global",
            UsesLockedDeck = true,
            LockedDeckUnitIds = new[] { "brawler", "shooter", "defender" },
            RequestedAtUnixSeconds = 1710001800
        };

        var result = provider.CreateRoom(request);
        if (!string.Equals(result.RoomId, "ROOM-HOST-11", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"unexpected room id {result.RoomId}");
            return 1;
        }

        if (result.HostTicket == null || !string.Equals(result.HostTicket.Status, "hosted", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("expected hosted host-ticket payload");
            return 1;
        }

        if (!result.UsesLockedDeck || result.LockedDeckUnitIds.Length < 3)
        {
            Console.Error.WriteLine("expected locked-deck hosted room");
            return 1;
        }

        Console.WriteLine(
            $"ROOM_CREATE_SMOKE PASS  |  provider {result.ProviderDisplayName}  |  room {result.RoomId}  |  ticket {result.HostTicket.TicketId}");
        return 0;
    }
}
CS

echo "Running HTTP online room create smoke..."
dotnet run --project "$RUNNER_CSPROJ" -- "http://127.0.0.1:${PORT}/challenge-room-create" >"$SMOKE_LOG" 2>&1 || SMOKE_STATUS=$?

if [[ $SMOKE_STATUS -ne 0 && -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
	kill "$SERVER_PID" 2>/dev/null || true
fi

wait "$SERVER_PID" || true

if [[ $SMOKE_STATUS -ne 0 ]]; then
	echo "HTTP online room create smoke failed."
	echo "--- smoke log ---"
	cat "$SMOKE_LOG"
	echo "--- server log ---"
	cat "$SERVER_LOG"
	exit 1
fi

if ! grep -q "ROOM_CREATE_SMOKE PASS" "$SMOKE_LOG"; then
	echo "Room create smoke exited cleanly but did not report pass."
	cat "$SMOKE_LOG"
	exit 1
fi

if [[ ! -s "$REQUEST_LOG" ]]; then
	echo "Room create stub did not capture a request."
	cat "$SMOKE_LOG"
	exit 1
fi

if ! grep -q '"boardCode"' "$REQUEST_LOG"; then
	echo "Room create request payload did not include the expected boardCode field."
	cat "$REQUEST_LOG"
	exit 1
fi

echo "HTTP online room create smoke passed."
echo "Smoke summary: $(grep 'ROOM_CREATE_SMOKE PASS' "$SMOKE_LOG" | tail -n 1)"
