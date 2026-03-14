#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/http-room-directory-smoke.XXXXXX")"
SERVER_LOG="$TMP_DIR/server.log"
REQUEST_LOG="$TMP_DIR/request.txt"
SMOKE_LOG="$TMP_DIR/smoke.log"
RUNNER_CSPROJ="$TMP_DIR/RoomDirectorySmoke.csproj"
RUNNER_PROGRAM="$TMP_DIR/Program.cs"
SERVER_PID=""
SMOKE_STATUS=0
PORT="${HTTP_ROOM_DIRECTORY_SMOKE_PORT:-$((19600 + RANDOM % 1000))}"

cleanup() {
	if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
		kill "$SERVER_PID" 2>/dev/null || true
	fi

	if [[ "${KEEP_HTTP_ROOM_DIRECTORY_SMOKE_LOGS:-0}" != "1" && $SMOKE_STATUS -eq 0 ]]; then
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
            "message": "stub rooms fetched",
            "rooms": [
                {
                    "roomId": "ROOM-NORTH-01",
                    "title": "Northern Relay Cup",
                    "summary": "Open async race room using player convoys.",
                    "hostCallsign": "AlderKeep",
                    "boardCode": "CH-02-PRS-6111",
                    "boardTitle": "City Route S2 Street Break",
                    "currentPlayers": 2,
                    "maxPlayers": 4,
                    "spectatorCount": 1,
                    "status": "lobby",
                    "region": "us-west",
                    "usesLockedDeck": False,
                    "lockedDeckUnitIds": []
                },
                {
                    "roomId": "ROOM-LOCK-02",
                    "title": "Locked Squad Scrim",
                    "summary": "Featured locked-deck room for fair daily score races.",
                    "hostCallsign": "IronBell",
                    "boardCode": "CH-03-RAT-6222",
                    "boardTitle": "Harbor Front S3 Breakwater",
                    "currentPlayers": 4,
                    "maxPlayers": 4,
                    "spectatorCount": 0,
                    "status": "countdown",
                    "region": "eu-north",
                    "usesLockedDeck": True,
                    "lockedDeckUnitIds": ["brawler", "shooter", "defender"]
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

echo "Running HTTP online room directory smoke..."
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

        var provider = new HttpApiOnlineRoomDirectoryProvider(args[0]);
        var snapshot = provider.FetchRooms(4, 16, 3);
        if (snapshot == null)
        {
            Console.Error.WriteLine("no snapshot returned");
            return 1;
        }

        if (snapshot.Entries.Count < 2)
        {
            Console.Error.WriteLine($"expected at least 2 rooms, got {snapshot.Entries.Count}");
            return 1;
        }

        if (!AsyncChallengeCatalog.TryParse(snapshot.Entries[0].BoardCode, out _, out _))
        {
            Console.Error.WriteLine("first room entry did not contain a valid challenge code");
            return 1;
        }

        if (!snapshot.Entries.Exists(entry => entry.UsesLockedDeck))
        {
            Console.Error.WriteLine("expected at least one locked-deck room");
            return 1;
        }

        Console.WriteLine(
            $"ROOM_DIRECTORY_SMOKE PASS  |  provider {snapshot.ProviderDisplayName}  |  rooms {snapshot.Entries.Count}  |  first {snapshot.Entries[0].RoomId}");
        return 0;
    }
}
CS

dotnet run --project "$RUNNER_CSPROJ" -- "http://127.0.0.1:${PORT}/challenge-rooms" >"$SMOKE_LOG" 2>&1 || SMOKE_STATUS=$?

if [[ $SMOKE_STATUS -ne 0 && -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
	kill "$SERVER_PID" 2>/dev/null || true
fi

wait "$SERVER_PID" || true

if [[ $SMOKE_STATUS -ne 0 ]]; then
	echo "HTTP online room directory smoke failed."
	echo "--- smoke log ---"
	cat "$SMOKE_LOG"
	echo "--- server log ---"
	cat "$SERVER_LOG"
	exit 1
fi

if ! grep -q "ROOM_DIRECTORY_SMOKE PASS" "$SMOKE_LOG"; then
	echo "Room directory smoke exited cleanly but did not report pass."
	cat "$SMOKE_LOG"
	exit 1
fi

if [[ ! -s "$REQUEST_LOG" ]]; then
	echo "Room directory stub did not capture a request."
	cat "$SMOKE_LOG"
	exit 1
fi

if ! grep -q "challenge-rooms" "$REQUEST_LOG"; then
	echo "Room directory request did not hit the expected challenge-rooms path."
	cat "$REQUEST_LOG"
	exit 1
fi

echo "HTTP online room directory smoke passed."
echo "Smoke summary: $(grep 'ROOM_DIRECTORY_SMOKE PASS' "$SMOKE_LOG" | tail -n 1)"
