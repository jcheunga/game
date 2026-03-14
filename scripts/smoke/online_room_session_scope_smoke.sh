#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/room-session-scope-smoke.XXXXXX")"
SMOKE_LOG="$TMP_DIR/smoke.log"
RUNNER_CSPROJ="$TMP_DIR/RoomSessionScopeSmoke.csproj"
RUNNER_PROGRAM="$TMP_DIR/Program.cs"
SMOKE_STATUS=0

cleanup() {
	if [[ "${KEEP_ROOM_SESSION_SCOPE_SMOKE_LOGS:-0}" != "1" && $SMOKE_STATUS -eq 0 ]]; then
		rm -rf "$TMP_DIR"
	fi
}
trap cleanup EXIT

cd "$ROOT_DIR"

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
using System.Reflection;

internal static class Program
{
    private static int Main()
    {
        var mergeMethod = typeof(OnlineRoomSessionService).GetMethod(
            "MergeScoreboardIntoSnapshot",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (mergeMethod == null)
        {
            Console.Error.WriteLine("missing MergeScoreboardIntoSnapshot helper");
            return 1;
        }

        var baseSnapshot = new OnlineRoomSessionSnapshot
        {
            ProviderId = "local_journal",
            ProviderDisplayName = "Local Session Stub",
            Status = "ok",
            Summary = "base snapshot",
            FetchedAtUnixSeconds = 1710003000,
            RoomSnapshot = new MultiplayerRoomSnapshot
            {
                HasRoom = true,
                RoomId = "ROOM-A",
                RoomTitle = "Alpha Relay",
                TransportLabel = "Internet Relay",
                RoleLabel = "Online contender",
                PeerCount = 2,
                SharedChallengeCode = "CH-03-RAT-6222",
                SharedChallengeTitle = "Harbor Front S3 Breakwater",
                LocalCallsign = "SmokeConvoy",
                DeckModeSummary = "Deck mode: locked shared squad.",
                JoinAddressSummary = "wss://relay.example.invalid/alpha",
                UsesLockedDeck = true,
                RoundLocked = false,
                RoundComplete = false,
                RaceCountdownActive = false,
                RaceCountdownRemainingSeconds = 0f,
                SelectedBoardCode = "CH-03-RAT-6222",
                SelectedBoardDeckMode = "locked shared squad",
                Peers = new[]
                {
                    new MultiplayerRoomPeerSnapshot
                    {
                        PeerId = 1,
                        Label = "IronBell",
                        IsLocalPlayer = false,
                        Phase = "prep",
                        IsReady = true,
                        IsLoaded = false,
                        IsLaunchEligible = true,
                        HasFullDeck = true,
                        MonitorRank = 1,
                        PostedScore = -1,
                        PostedRank = -1,
                        PresenceText = "joined and ready",
                        MonitorText = "IronBell  |  prep  |  ready"
                    },
                    new MultiplayerRoomPeerSnapshot
                    {
                        PeerId = 2,
                        Label = "SmokeConvoy",
                        IsLocalPlayer = true,
                        Phase = "prep",
                        IsReady = true,
                        IsLoaded = false,
                        IsLaunchEligible = true,
                        HasFullDeck = true,
                        MonitorRank = 2,
                        PostedScore = -1,
                        PostedRank = -1,
                        PresenceText = "joined and ready",
                        MonitorText = "SmokeConvoy  |  prep  |  ready"
                    }
                }
            }
        };

        var otherRoomScoreboard = new OnlineRoomScoreboardSnapshot
        {
            RoomId = "ROOM-B",
            BoardCode = "CH-03-RAT-6222",
            ProviderId = "http_api",
            ProviderDisplayName = "HTTP Room Scoreboard",
            Status = "ok",
            Summary = "other room",
            FetchedAtUnixSeconds = 1710003010,
            Entries =
            {
                new OnlineRoomScoreboardEntry
                {
                    Rank = 1,
                    RoomId = "ROOM-B",
                    BoardCode = "CH-03-RAT-6222",
                    PlayerCallsign = "IronBell",
                    Score = 18450,
                    HullPercent = 88,
                    ElapsedSeconds = 12.4f,
                    EnemyDefeats = 9,
                    Won = true
                }
            }
        };

        var ignoredMerge = (OnlineRoomSessionSnapshot)mergeMethod.Invoke(null, new object[] { baseSnapshot, otherRoomScoreboard });
        var ignoredPeer = ignoredMerge.RoomSnapshot.Peers[0];
        if (ignoredPeer.PostedScore != -1 || ignoredPeer.PostedRank != -1 || !string.Equals(ignoredPeer.Phase, "prep", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("scoreboard from a different room leaked into the session snapshot");
            return 1;
        }

        var sameRoomScoreboard = new OnlineRoomScoreboardSnapshot
        {
            RoomId = "ROOM-A",
            BoardCode = "CH-03-RAT-6222",
            ProviderId = "http_api",
            ProviderDisplayName = "HTTP Room Scoreboard",
            Status = "ok",
            Summary = "same room",
            FetchedAtUnixSeconds = 1710003020,
            Entries =
            {
                new OnlineRoomScoreboardEntry
                {
                    Rank = 1,
                    RoomId = "ROOM-A",
                    BoardCode = "CH-03-RAT-6222",
                    PlayerCallsign = "IronBell",
                    Score = 18450,
                    HullPercent = 88,
                    ElapsedSeconds = 12.4f,
                    EnemyDefeats = 9,
                    Won = true
                }
            }
        };

        var acceptedMerge = (OnlineRoomSessionSnapshot)mergeMethod.Invoke(null, new object[] { baseSnapshot, sameRoomScoreboard });
        var mergedPeer = acceptedMerge.RoomSnapshot.Peers[0];
        if (mergedPeer.PostedScore != 18450 || mergedPeer.PostedRank != 1 || !string.Equals(mergedPeer.Phase, "submitted", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("scoreboard from the active room did not merge into the session snapshot");
            return 1;
        }

        if (!string.Equals(acceptedMerge.RoomSnapshot.RoomId, "ROOM-A", StringComparison.Ordinal) ||
            !string.Equals(acceptedMerge.RoomSnapshot.RoomTitle, "Alpha Relay", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("room identity was not preserved after scoreboard merge");
            return 1;
        }

        Console.WriteLine("ROOM_SESSION_SCOPE_SMOKE PASS  |  room ROOM-A stays isolated from ROOM-B");
        return 0;
    }
}
CS

echo "Running online room session scope smoke..."
dotnet run --project "$RUNNER_CSPROJ" >"$SMOKE_LOG" 2>&1 || SMOKE_STATUS=$?

if [[ $SMOKE_STATUS -ne 0 ]]; then
	echo "Online room session scope smoke failed."
	echo "--- smoke log ---"
	cat "$SMOKE_LOG"
	exit 1
fi

if ! grep -q "ROOM_SESSION_SCOPE_SMOKE PASS" "$SMOKE_LOG"; then
	echo "Room session scope smoke exited cleanly but did not report pass."
	cat "$SMOKE_LOG"
	exit 1
fi

echo "Online room session scope smoke passed."
echo "Smoke summary: $(grep 'ROOM_SESSION_SCOPE_SMOKE PASS' "$SMOKE_LOG" | tail -n 1)"
