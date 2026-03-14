#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/http-room-session-smoke.XXXXXX")"
SERVER_LOG="$TMP_DIR/server.log"
REQUEST_LOG="$TMP_DIR/request.json"
SMOKE_LOG="$TMP_DIR/smoke.log"
RUNNER_CSPROJ="$TMP_DIR/RoomSessionSmoke.csproj"
RUNNER_PROGRAM="$TMP_DIR/Program.cs"
SERVER_PID=""
SMOKE_STATUS=0
PORT="${HTTP_ROOM_SESSION_SMOKE_PORT:-$((19800 + RANDOM % 1000))}"

cleanup() {
	if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
		kill "$SERVER_PID" 2>/dev/null || true
	fi

	if [[ "${KEEP_HTTP_ROOM_SESSION_SMOKE_LOGS:-0}" != "1" && $SMOKE_STATUS -eq 0 ]]; then
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
            "message": "stub room session fetched",
            "roomId": "ROOM-LOCK-02",
            "roomTitle": "Locked Squad Scrim",
            "boardCode": "CH-03-RAT-6222",
            "boardTitle": "Harbor Front S3 Breakwater",
            "transportLabel": "Internet Relay",
            "roleLabel": "Online contender",
            "deckModeSummary": "Deck mode: locked shared squad negotiated by backend.",
            "relayEndpoint": "wss://relay.example.invalid/room-lock-02",
            "usesLockedDeck": True,
            "roundLocked": False,
            "roundComplete": False,
            "raceCountdownActive": False,
            "raceCountdownRemainingSeconds": 0,
            "selectedBoardDeckMode": "locked shared squad",
            "peers": [
                {
                    "peerId": 1,
                    "label": "IronBell",
                    "isLocalPlayer": False,
                    "phase": "submitted",
                    "isReady": True,
                    "isLoaded": True,
                    "isLaunchEligible": True,
                    "hasFullDeck": True,
                    "monitorRank": 1,
                    "raceElapsedSeconds": -1,
                    "hullPercent": 88,
                    "enemyDefeats": 9,
                    "postedScore": 18450,
                    "postedRank": 1,
                    "presenceText": "result submitted, provisional #1",
                    "monitorText": "IronBell  |  submitted  |  #1  |  18450 pts",
                    "deckText": "IronBell  |  locked squad"
                },
                {
                    "peerId": 2,
                    "label": "SmokeConvoy",
                    "isLocalPlayer": True,
                    "phase": "racing",
                    "isReady": True,
                    "isLoaded": True,
                    "isLaunchEligible": True,
                    "hasFullDeck": True,
                    "monitorRank": 2,
                    "raceElapsedSeconds": 12.4,
                    "hullPercent": 91,
                    "enemyDefeats": 6,
                    "presenceText": "racing live: 12.4s, hull 91%",
                    "monitorText": "SmokeConvoy  |  racing  |  12.4s  |  Hull 91%  |  Defeats 6",
                    "deckText": "SmokeConvoy  |  locked squad"
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
using System.Linq;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.Error.WriteLine("missing endpoint");
            return 1;
        }

        var provider = new HttpApiOnlineRoomSessionProvider(args[0]);
        var ticket = new OnlineRoomJoinTicket
        {
            RoomId = "ROOM-LOCK-02",
            RoomTitle = "Locked Squad Scrim",
            BoardCode = "CH-03-RAT-6222",
            TicketId = "JOIN-HTTP-01",
            JoinToken = "token-http-join-01",
            RelayEndpoint = "wss://relay.example.invalid/room-lock-02",
            UsesLockedDeck = true,
            LockedDeckUnitIds = new[] { "brawler", "shooter", "defender" }
        };

        var snapshot = provider.FetchRoomSession(ticket);
        if (snapshot.RoomSnapshot == null || !snapshot.RoomSnapshot.HasRoom)
        {
            Console.Error.WriteLine("expected room snapshot");
            return 1;
        }

        if (!string.Equals(snapshot.RoomSnapshot.RoomId, "ROOM-LOCK-02", StringComparison.Ordinal) ||
            !string.Equals(snapshot.RoomSnapshot.RoomTitle, "Locked Squad Scrim", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("expected room identity fields in room snapshot");
            return 1;
        }

        if (snapshot.RoomSnapshot.Peers.Count < 2)
        {
            Console.Error.WriteLine($"expected at least 2 peers, got {snapshot.RoomSnapshot.Peers.Count}");
            return 1;
        }

        if (!snapshot.RoomSnapshot.UsesLockedDeck)
        {
            Console.Error.WriteLine("expected locked-deck room snapshot");
            return 1;
        }

        var localPeer = snapshot.RoomSnapshot.Peers.FirstOrDefault(peer => peer.IsLocalPlayer);
        if (localPeer == null || localPeer.RaceElapsedSeconds < 12f || localPeer.HullPercent != 91 || localPeer.EnemyDefeats != 6)
        {
            Console.Error.WriteLine("expected structured local peer telemetry in room snapshot");
            return 1;
        }

        var leaderPeer = snapshot.RoomSnapshot.Peers.FirstOrDefault(peer => peer.Label == "IronBell");
        if (leaderPeer == null || leaderPeer.PostedScore != 18450 || leaderPeer.PostedRank != 1)
        {
            Console.Error.WriteLine("expected structured submitted peer standings in room snapshot");
            return 1;
        }

        Console.WriteLine(
            $"ROOM_SESSION_SMOKE PASS  |  provider {snapshot.ProviderDisplayName}  |  room {snapshot.RoomSnapshot.SharedChallengeCode}  |  peers {snapshot.RoomSnapshot.Peers.Count}");
        return 0;
    }
}
CS

echo "Running HTTP online room session smoke..."
dotnet run --project "$RUNNER_CSPROJ" -- "http://127.0.0.1:${PORT}/challenge-room-session" >"$SMOKE_LOG" 2>&1 || SMOKE_STATUS=$?

if [[ $SMOKE_STATUS -ne 0 && -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
	kill "$SERVER_PID" 2>/dev/null || true
fi

wait "$SERVER_PID" || true

if [[ $SMOKE_STATUS -ne 0 ]]; then
	echo "HTTP online room session smoke failed."
	echo "--- smoke log ---"
	cat "$SMOKE_LOG"
	echo "--- server log ---"
	cat "$SERVER_LOG"
	exit 1
fi

if ! grep -q "ROOM_SESSION_SMOKE PASS" "$SMOKE_LOG"; then
	echo "Room session smoke exited cleanly but did not report pass."
	cat "$SMOKE_LOG"
	exit 1
fi

if [[ ! -s "$REQUEST_LOG" ]]; then
	echo "Room session stub did not capture a request."
	cat "$SMOKE_LOG"
	exit 1
fi

if ! grep -q '"joinToken"' "$REQUEST_LOG"; then
	echo "Room session request payload did not include the expected joinToken field."
	cat "$REQUEST_LOG"
	exit 1
fi

echo "HTTP online room session smoke passed."
echo "Smoke summary: $(grep 'ROOM_SESSION_SMOKE PASS' "$SMOKE_LOG" | tail -n 1)"
