#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/room-ticket-swap-smoke.XXXXXX")"
SMOKE_LOG="$TMP_DIR/smoke.log"
RUNNER_CSPROJ="$TMP_DIR/RoomTicketSwapSmoke.csproj"
RUNNER_PROGRAM="$TMP_DIR/Program.cs"
SMOKE_STATUS=0

cleanup() {
	if [[ "${KEEP_ROOM_TICKET_SWAP_SMOKE_LOGS:-0}" != "1" && $SMOKE_STATUS -eq 0 ]]; then
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
        var ticketA = new OnlineRoomJoinTicket
        {
            ProviderId = "local_journal",
            ProviderDisplayName = "Local Join Stub",
            RoomId = "ROOM-A",
            RoomTitle = "Alpha Relay",
            BoardCode = "CH-03-RAT-6222",
            Status = "accepted",
            Summary = "Alpha seat ready.",
            TicketId = "JOIN-A",
            JoinToken = "token-a",
            TransportHint = "internet_room_pending",
            RelayEndpoint = "wss://relay.example.invalid/alpha",
            SeatLabel = "player-convoy seat",
            RequestedAtUnixSeconds = 1710004000,
            ExpiresAtUnixSeconds = 1710004300
        };
        var ticketB = new OnlineRoomJoinTicket
        {
            ProviderId = "local_journal",
            ProviderDisplayName = "Local Join Stub",
            RoomId = "ROOM-B",
            RoomTitle = "Beta Relay",
            BoardCode = "CH-04-PRS-4821",
            Status = "accepted",
            Summary = "Beta seat ready.",
            TicketId = "JOIN-B",
            JoinToken = "token-b",
            TransportHint = "internet_room_pending",
            RelayEndpoint = "wss://relay.example.invalid/beta",
            SeatLabel = "player-convoy seat",
            RequestedAtUnixSeconds = 1710005000,
            ExpiresAtUnixSeconds = 1710005300
        };

        if (!OnlineRoomJoinService.AdoptNegotiatedTicket(ticketA, out _))
        {
            Console.Error.WriteLine("failed to arm initial room ticket");
            return 1;
        }

        SetPrivateStaticField(
            typeof(OnlineRoomSessionService),
            "_cachedSnapshot",
            new OnlineRoomSessionSnapshot
            {
                ProviderId = "local_journal",
                ProviderDisplayName = "Local Session Stub",
                Status = "ok",
                Summary = "alpha session",
                FetchedAtUnixSeconds = 1710004010,
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
                    LocalCallsign = "AlphaRunner",
                    DeckModeSummary = "Deck mode: locked shared squad.",
                    JoinAddressSummary = "wss://relay.example.invalid/alpha",
                    UsesLockedDeck = true,
                    SelectedBoardCode = "CH-03-RAT-6222",
                    SelectedBoardDeckMode = "locked shared squad",
                    Peers = new[]
                    {
                        new MultiplayerRoomPeerSnapshot
                        {
                            PeerId = 1,
                            Label = "AlphaRunner",
                            IsLocalPlayer = true,
                            Phase = "prep",
                            IsReady = true,
                            IsLaunchEligible = true,
                            HasFullDeck = true,
                            MonitorRank = 1,
                            PresenceText = "joined and ready",
                            MonitorText = "AlphaRunner  |  prep  |  ready"
                        }
                    }
                }
            });

        SetPrivateStaticField(
            typeof(OnlineRoomScoreboardService),
            "_cachedSnapshot",
            new OnlineRoomScoreboardSnapshot
            {
                RoomId = "ROOM-A",
                BoardCode = "CH-03-RAT-6222",
                ProviderId = "local_journal",
                ProviderDisplayName = "Local Room Scoreboard Stub",
                Status = "ok",
                Summary = "alpha scoreboard",
                FetchedAtUnixSeconds = 1710004011,
                Entries =
                {
                    new OnlineRoomScoreboardEntry
                    {
                        Rank = 1,
                        RoomId = "ROOM-A",
                        BoardCode = "CH-03-RAT-6222",
                        PlayerCallsign = "AlphaRunner",
                        Score = 9999,
                        HullPercent = 85,
                        ElapsedSeconds = 15.2f,
                        EnemyDefeats = 12,
                        Won = true
                    }
                }
            });

        SetPrivateStaticField(
            typeof(OnlineRoomSeatLeaseService),
            "_lastResult",
            new OnlineRoomSeatLeaseResult
            {
                ProviderId = "local_journal",
                ProviderDisplayName = "Local Seat Lease",
                RoomId = "ROOM-A",
                BoardCode = "CH-03-RAT-6222",
                TicketId = "JOIN-A",
                JoinToken = "token-a",
                Status = "accepted",
                Summary = "alpha lease",
                ExpiresAtUnixSeconds = 1710004300,
                RenewedAtUnixSeconds = 1710004012
            });

        SetPrivateStaticField(
            typeof(OnlineRoomReportService),
            "_lastResult",
            new OnlineRoomReportResult
            {
                ProviderId = "local_journal",
                ProviderDisplayName = "Local Report Stub",
                RoomId = "ROOM-A",
                BoardCode = "CH-03-RAT-6222",
                ReportId = "RPT-A",
                SubjectType = "player",
                SubjectLabel = "AlphaRunner",
                ReasonId = "suspicious_score",
                Status = "accepted",
                Summary = "alpha report",
                SubmittedAtUnixSeconds = 1710004013
            });

        SetPrivateStaticField(
            typeof(OnlineRoomResultService),
            "_lastSubmission",
            new OnlineRoomResultSubmission
            {
                ProviderId = "local_journal",
                ProviderDisplayName = "Local Result Stub",
                RoomId = "ROOM-A",
                BoardCode = "CH-03-RAT-6222",
                TicketId = "JOIN-A",
                Status = "accepted",
                Summary = "alpha result",
                Score = 9999,
                ProvisionalRank = 1,
                SubmittedAtUnixSeconds = 1710004014
            });

        SetPrivateStaticField(
            typeof(OnlineRoomTelemetryService),
            "_lastSubmission",
            new OnlineRoomTelemetrySubmission
            {
                ProviderId = "local_journal",
                ProviderDisplayName = "Local Telemetry Stub",
                RoomId = "ROOM-A",
                BoardCode = "CH-03-RAT-6222",
                TicketId = "JOIN-A",
                Status = "accepted",
                Summary = "alpha telemetry",
                ProcessedAtUnixSeconds = 1710004015
            });

        SetPrivateStaticField(
            typeof(OnlineRoomActionService),
            "_lastResult",
            new OnlineRoomActionResult
            {
                ProviderId = "local_journal",
                ProviderDisplayName = "Local Action Stub",
                RoomId = "ROOM-A",
                BoardCode = "CH-03-RAT-6222",
                TicketId = "JOIN-A",
                ActionId = "set_ready",
                Status = "accepted",
                Summary = "alpha action",
                ReadyState = true,
                ProcessedAtUnixSeconds = 1710004016
            });

        SetPrivateStaticField(
            typeof(OnlineRoomCreateService),
            "_hostedRoom",
            new OnlineRoomCreateResult
            {
                ProviderId = "local_journal",
                ProviderDisplayName = "Local Host Stub",
                RoomId = "ROOM-A",
                Title = "Alpha Relay",
                Summary = "alpha hosted room",
                HostCallsign = "AlphaRunner",
                BoardCode = "CH-03-RAT-6222",
                BoardTitle = "Harbor Front S3 Breakwater",
                CurrentPlayers = 1,
                MaxPlayers = 4,
                Status = "lobby",
                Region = "global",
                TransportHint = "internet_room_hosted",
                RelayEndpoint = "wss://relay.example.invalid/alpha",
                HostTicket = ticketA
            });

        if (!OnlineRoomJoinService.AdoptNegotiatedTicket(ticketB, out var adoptMessage))
        {
            Console.Error.WriteLine("failed to swap to the second room ticket");
            return 1;
        }

        if (OnlineRoomSessionService.GetCachedSnapshot() != null)
        {
            Console.Error.WriteLine("old room session cache survived the ticket swap");
            return 1;
        }

        if (OnlineRoomScoreboardService.GetCachedSnapshot() != null)
        {
            Console.Error.WriteLine("old room scoreboard cache survived the ticket swap");
            return 1;
        }

        if (OnlineRoomCreateService.GetHostedRoom() != null)
        {
            Console.Error.WriteLine("hosted room cache was not cleared for the previous room");
            return 1;
        }

        var leaseSummary = OnlineRoomSeatLeaseService.BuildStatusSummary();
        var reportSummary = OnlineRoomReportService.BuildStatusSummary();
        var resultSummary = OnlineRoomResultService.BuildStatusSummary();
        var telemetrySummary = OnlineRoomTelemetryService.BuildStatusSummary();
        var actionSummary = OnlineRoomActionService.BuildStatusSummary();
        var sessionSummary = OnlineRoomSessionService.BuildStatusSummary();
        var scoreboardSummary = OnlineRoomScoreboardService.BuildStatusSummary();

        if (leaseSummary.Contains("alpha lease", StringComparison.OrdinalIgnoreCase) ||
            reportSummary.Contains("alpha report", StringComparison.OrdinalIgnoreCase) ||
            resultSummary.Contains("alpha result", StringComparison.OrdinalIgnoreCase) ||
            telemetrySummary.Contains("alpha telemetry", StringComparison.OrdinalIgnoreCase) ||
            actionSummary.Contains("alpha action", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("stale room-A service summaries leaked into room-B state");
            return 1;
        }

        if (!sessionSummary.Contains("Beta Relay", StringComparison.Ordinal) ||
            !scoreboardSummary.Contains("Beta Relay", StringComparison.Ordinal) ||
            !leaseSummary.Contains("Beta Relay", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("ticket-swap summaries did not pivot to the new room");
            return 1;
        }

        if (!actionSummary.Contains("idle for the current join ticket", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("room action state did not reset for the new ticket");
            return 1;
        }

        Console.WriteLine(
            $"ROOM_TICKET_SWAP_SMOKE PASS  |  adopted {ticketB.RoomId}  |  message {adoptMessage.Split('\n')[0]}");
        return 0;
    }

    private static void SetPrivateStaticField(Type type, string fieldName, object value)
    {
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException($"missing field {type.FullName}.{fieldName}");
        }

        field.SetValue(null, value);
    }
}
CS

echo "Running online room ticket swap smoke..."
dotnet run --project "$RUNNER_CSPROJ" >"$SMOKE_LOG" 2>&1 || SMOKE_STATUS=$?

if [[ $SMOKE_STATUS -ne 0 ]]; then
	echo "Online room ticket swap smoke failed."
	echo "--- smoke log ---"
	cat "$SMOKE_LOG"
	exit 1
fi

if ! grep -q "ROOM_TICKET_SWAP_SMOKE PASS" "$SMOKE_LOG"; then
	echo "Room ticket swap smoke exited cleanly but did not report pass."
	cat "$SMOKE_LOG"
	exit 1
fi

echo "Online room ticket swap smoke passed."
echo "Smoke summary: $(grep 'ROOM_TICKET_SWAP_SMOKE PASS' "$SMOKE_LOG" | tail -n 1)"
