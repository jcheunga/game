#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/http-room-matchmake-smoke.XXXXXX")"
SERVER_LOG="$TMP_DIR/server.log"
REQUEST_LOG="$TMP_DIR/request.json"
SMOKE_LOG="$TMP_DIR/smoke.log"
RUNNER_CSPROJ="$TMP_DIR/RoomMatchmakeSmoke.csproj"
RUNNER_PROGRAM="$TMP_DIR/Program.cs"
SERVER_PID=""
SMOKE_STATUS=0
PORT="${HTTP_ROOM_MATCHMAKE_SMOKE_PORT:-$((20900 + RANDOM % 1000))}"

cleanup() {
	if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
		kill "$SERVER_PID" 2>/dev/null || true
	fi

	if [[ "${KEEP_HTTP_ROOM_MATCHMAKE_SMOKE_LOGS:-0}" != "1" && $SMOKE_STATUS -eq 0 ]]; then
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
            "message": "stub matchmaker assigned a room seat",
            "createdNewRoom": False,
            "room": {
                "roomId": "ROOM-MATCH-01",
                "title": "Matchmaker Relay",
                "summary": "Backend quick match room.",
                "hostCallsign": "MatchRelay",
                "boardCode": "CH-05-BLK-5110",
                "boardTitle": "Quarantine Wall S5 Blacksite",
                "currentPlayers": 2,
                "maxPlayers": 4,
                "spectatorCount": 0,
                "status": "lobby",
                "region": "global",
                "usesLockedDeck": True,
                "lockedDeckUnitIds": ["brawler", "shooter", "defender"]
            },
            "joinTicket": {
                "roomId": "ROOM-MATCH-01",
                "roomTitle": "Matchmaker Relay",
                "boardCode": "CH-05-BLK-5110",
                "status": "accepted",
                "message": "Seat assigned by backend matchmaker.",
                "ticketId": "JOIN-MATCH-01",
                "joinToken": "token-match-01",
                "transportHint": "internet_room_pending",
                "relayEndpoint": "wss://relay.example.invalid/room-match-01",
                "seatLabel": "locked-squad seat",
                "requestedAtUnixSeconds": 1710004100,
                "expiresAtUnixSeconds": 1710004280,
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

        var provider = new HttpApiOnlineRoomMatchmakeProvider(args[0]);
        var challenge = AsyncChallengeCatalog.Create(5, AsyncChallengeCatalog.BlackoutRelayId, 5110);
        var request = new OnlineRoomMatchmakeRequest
        {
            BoardCode = challenge.Code,
            PlayerProfileId = "CVY-SMOKE",
            PlayerCallsign = "SmokeConvoy",
            WantsLockedDeckSeat = true,
            Region = "global",
            RequestedAtUnixSeconds = 1710004100
        };

        var result = provider.Matchmake(challenge, request);
        if (!string.Equals(result.Status, "accepted", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"expected accepted matchmake status, got {result.Status}");
            return 1;
        }

        if (result.Room == null || !string.Equals(result.Room.RoomId, "ROOM-MATCH-01", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("expected ROOM-MATCH-01 room payload");
            return 1;
        }

        if (result.JoinTicket == null || !string.Equals(result.JoinTicket.TicketId, "JOIN-MATCH-01", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("expected JOIN-MATCH-01 join ticket");
            return 1;
        }

        Console.WriteLine(
            $"ROOM_MATCHMAKE_SMOKE PASS  |  provider {result.ProviderDisplayName}  |  room {result.Room.RoomId}  |  seat {result.JoinTicket.SeatLabel}");
        return 0;
    }
}
CS

echo "Running HTTP online room matchmake smoke..."
dotnet run --project "$RUNNER_CSPROJ" -- "http://127.0.0.1:${PORT}/challenge-room-matchmake" >"$SMOKE_LOG" 2>&1 || SMOKE_STATUS=$?

if [[ $SMOKE_STATUS -ne 0 && -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
	kill "$SERVER_PID" 2>/dev/null || true
fi

wait "$SERVER_PID" || true

if [[ $SMOKE_STATUS -ne 0 ]]; then
	echo "HTTP online room matchmake smoke failed."
	echo "--- smoke log ---"
	cat "$SMOKE_LOG"
	echo "--- server log ---"
	cat "$SERVER_LOG"
	exit 1
fi

if ! grep -q "ROOM_MATCHMAKE_SMOKE PASS" "$SMOKE_LOG"; then
	echo "Room matchmake smoke exited cleanly but did not report pass."
	cat "$SMOKE_LOG"
	exit 1
fi

if [[ ! -s "$REQUEST_LOG" ]]; then
	echo "Room matchmake stub did not capture a request."
	cat "$SMOKE_LOG"
	exit 1
fi

if ! grep -q '"boardCode":"CH-05-BLK-5110"' "$REQUEST_LOG"; then
	echo "Room matchmake request payload did not include the expected boardCode field."
	cat "$REQUEST_LOG"
	exit 1
fi

echo "HTTP online room matchmake smoke passed."
echo "Smoke summary: $(grep 'ROOM_MATCHMAKE_SMOKE PASS' "$SMOKE_LOG" | tail -n 1)"
