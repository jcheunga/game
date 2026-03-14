#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/http-room-scoreboard-smoke.XXXXXX")"
SERVER_LOG="$TMP_DIR/server.log"
SMOKE_LOG="$TMP_DIR/smoke.log"
RUNNER_CSPROJ="$TMP_DIR/RoomScoreboardSmoke.csproj"
RUNNER_PROGRAM="$TMP_DIR/Program.cs"
SERVER_PID=""
SMOKE_STATUS=0
PORT="${HTTP_ROOM_SCOREBOARD_SMOKE_PORT:-$((20300 + RANDOM % 1000))}"

cleanup() {
	if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
		kill "$SERVER_PID" 2>/dev/null || true
	fi

	if [[ "${KEEP_HTTP_ROOM_SCOREBOARD_SMOKE_LOGS:-0}" != "1" && $SMOKE_STATUS -eq 0 ]]; then
		rm -rf "$TMP_DIR"
	fi
}
trap cleanup EXIT

cd "$ROOT_DIR"

/usr/bin/python3 - "$PORT" >"$SERVER_LOG" 2>&1 <<'PY' &
import json
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer

port = int(sys.argv[1])

class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        response = {
            "status": "ok",
            "message": "stub room scoreboard fetched",
            "entries": [
                {
                    "rank": 1,
                    "playerCallsign": "IronBell",
                    "playerProfileId": "STUB-IRONBELL",
                    "score": 1520,
                    "starsEarned": 3,
                    "hullPercent": 84,
                    "elapsedSeconds": 69.2,
                    "enemyDefeats": 30,
                    "won": True,
                    "retreated": False,
                    "usedLockedDeck": True,
                    "submittedAtUnixSeconds": 1710002900
                },
                {
                    "rank": 2,
                    "playerCallsign": "SmokeConvoy",
                    "playerProfileId": "CVY-SMOKE",
                    "score": 1475,
                    "starsEarned": 3,
                    "hullPercent": 78,
                    "elapsedSeconds": 70.4,
                    "enemyDefeats": 27,
                    "won": True,
                    "retreated": False,
                    "usedLockedDeck": True,
                    "submittedAtUnixSeconds": 1710002800
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

        var provider = new HttpApiOnlineRoomScoreboardProvider(args[0]);
        var ticket = new OnlineRoomJoinTicket
        {
            RoomId = "ROOM-HOST-11",
            RoomTitle = "SmokeConvoy Relay",
            BoardCode = "CH-05-BLK-5110",
            TicketId = "HOST-HTTP-01"
        };

        var snapshot = provider.FetchScoreboard(ticket, 5);
        if (snapshot.Entries.Count != 2)
        {
            Console.Error.WriteLine($"expected 2 room-scoreboard entries, got {snapshot.Entries.Count}");
            return 1;
        }

        if (!string.Equals(snapshot.Entries[0].PlayerCallsign, "IronBell", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("unexpected top room-scoreboard entry");
            return 1;
        }

        Console.WriteLine(
            $"ROOM_SCOREBOARD_SMOKE PASS  |  provider {snapshot.ProviderDisplayName}  |  room {snapshot.RoomId}  |  entries {snapshot.Entries.Count}");
        return 0;
    }
}
CS

echo "Running HTTP online room scoreboard smoke..."
dotnet run --project "$RUNNER_CSPROJ" -- "http://127.0.0.1:${PORT}/challenge-room-scoreboard" >"$SMOKE_LOG" 2>&1 || SMOKE_STATUS=$?

if [[ $SMOKE_STATUS -ne 0 && -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
	kill "$SERVER_PID" 2>/dev/null || true
fi

wait "$SERVER_PID" || true

if [[ $SMOKE_STATUS -ne 0 ]]; then
	echo "HTTP online room scoreboard smoke failed."
	echo "--- smoke log ---"
	cat "$SMOKE_LOG"
	echo "--- server log ---"
	cat "$SERVER_LOG"
	exit 1
fi

if ! grep -q "ROOM_SCOREBOARD_SMOKE PASS" "$SMOKE_LOG"; then
	echo "Room scoreboard smoke exited cleanly but did not report pass."
	cat "$SMOKE_LOG"
	exit 1
fi

echo "HTTP online room scoreboard smoke passed."
echo "Smoke summary: $(grep 'ROOM_SCOREBOARD_SMOKE PASS' "$SMOKE_LOG" | tail -n 1)"
