#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/room-recovery-smoke.XXXXXX")"
SMOKE_LOG="$TMP_DIR/smoke.log"
RUNNER_CSPROJ="$TMP_DIR/RoomRecoverySmoke.csproj"
RUNNER_PROGRAM="$TMP_DIR/Program.cs"
SMOKE_STATUS=0

cleanup() {
	if [[ "${KEEP_ROOM_RECOVERY_SMOKE_LOGS:-0}" != "1" && $SMOKE_STATUS -eq 0 ]]; then
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
        if (!RunDirectRoomRecovery())
        {
            return 1;
        }

        if (!RunQuickMatchFallback())
        {
            return 1;
        }

        Console.WriteLine("ROOM_RECOVERY_SMOKE PASS  |  direct rejoin and quick-match fallback both recovered stale seats");
        return 0;
    }

    private static bool RunDirectRoomRecovery()
    {
        ResetRoomState("direct recovery reset");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        OnlineRoomDirectoryService.InjectRoom(new OnlineRoomDirectoryEntry
        {
            RoomId = "ROOM-A",
            Title = "Alpha Relay",
            Summary = "Recoverable room listing.",
            HostCallsign = "IronBell",
            BoardCode = "CH-03-RAT-6222",
            BoardTitle = "Harbor Front S3 Breakwater",
            CurrentPlayers = 2,
            MaxPlayers = 4,
            SpectatorCount = 0,
            Status = "lobby",
            Region = "global",
            UsesLockedDeck = true,
            LockedDeckUnitIds = new[] { "brawler", "shooter", "defender" }
        });

        var expiredTicket = BuildExpiredTicket(
            "ROOM-A",
            "Alpha Relay",
            "CH-03-RAT-6222",
            "JOIN-OLD-A",
            now - 180,
            now - 30,
            true);

        if (!OnlineRoomJoinService.AdoptNegotiatedTicket(expiredTicket, out var adoptMessage))
        {
            Console.Error.WriteLine($"could not arm expired recovery ticket: {adoptMessage}");
            return false;
        }

        if (!OnlineRoomRecoveryService.TryRecoverExpiredSeat(out var recoveryMessage))
        {
            Console.Error.WriteLine($"direct room recovery failed: {recoveryMessage}");
            return false;
        }

        var recoveredTicket = OnlineRoomJoinService.GetCachedTicket();
        if (recoveredTicket == null ||
            !string.Equals(recoveredTicket.RoomId, "ROOM-A", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(recoveredTicket.TicketId, "JOIN-OLD-A", StringComparison.OrdinalIgnoreCase) ||
            OnlineRoomJoinService.IsTicketExpired(recoveredTicket))
        {
            Console.Error.WriteLine("direct room recovery did not replace the expired ticket with an active room-A seat");
            return false;
        }

        if (!recoveryMessage.Contains("Skipped live room refresh because the full game state is unavailable.", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("direct room recovery did not report the reduced-runtime refresh skip");
            return false;
        }

        if (!OnlineRoomRecoveryService.BuildStatusSummary().Contains("direct room rejoin", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("recovery status summary did not record the direct room rejoin path");
            return false;
        }

        return true;
    }

    private static bool RunQuickMatchFallback()
    {
        ResetRoomState("quick-match recovery reset");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        SetPrivateStaticField(typeof(OnlineRoomDirectoryService), "_cachedSnapshot", null);

        var expiredTicket = BuildExpiredTicket(
            "ROOM-Z",
            "Missing Relay",
            "CH-04-PRS-4821",
            "JOIN-OLD-Z",
            now - 180,
            now - 30,
            false);

        if (!OnlineRoomJoinService.AdoptNegotiatedTicket(expiredTicket, out var adoptMessage))
        {
            Console.Error.WriteLine($"could not arm fallback recovery ticket: {adoptMessage}");
            return false;
        }

        if (!OnlineRoomRecoveryService.TryRecoverExpiredSeat(out var recoveryMessage))
        {
            Console.Error.WriteLine($"quick-match fallback recovery failed: {recoveryMessage}");
            return false;
        }

        var recoveredTicket = OnlineRoomJoinService.GetCachedTicket();
        if (recoveredTicket == null ||
            string.Equals(recoveredTicket.RoomId, "ROOM-Z", StringComparison.OrdinalIgnoreCase) ||
            !recoveredTicket.RoomId.StartsWith("match_", StringComparison.OrdinalIgnoreCase) ||
            !AsyncChallengeCatalog.NormalizeCode(recoveredTicket.BoardCode)
                .Equals(AsyncChallengeCatalog.NormalizeCode("CH-04-PRS-4821"), StringComparison.OrdinalIgnoreCase) ||
            OnlineRoomJoinService.IsTicketExpired(recoveredTicket))
        {
            Console.Error.WriteLine("quick-match fallback did not recover the expired ticket onto an active matched room");
            return false;
        }

        if (!OnlineRoomRecoveryService.BuildStatusSummary().Contains("quick match fallback", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("recovery status summary did not record the quick-match fallback path");
            return false;
        }

        return true;
    }

    private static OnlineRoomJoinTicket BuildExpiredTicket(
        string roomId,
        string roomTitle,
        string boardCode,
        string ticketId,
        long requestedAt,
        long expiresAt,
        bool usesLockedDeck)
    {
        return new OnlineRoomJoinTicket
        {
            ProviderId = ChallengeSyncProviderCatalog.LocalJournalId,
            ProviderDisplayName = "Local Room Stub",
            RoomId = roomId,
            RoomTitle = roomTitle,
            BoardCode = boardCode,
            Status = "accepted",
            Summary = $"Expired seat for {roomTitle}.",
            TicketId = ticketId,
            JoinToken = $"token-{ticketId.ToLowerInvariant()}",
            TransportHint = "internet_room_stub",
            RelayEndpoint = $"stub://room/{roomId}",
            SeatLabel = usesLockedDeck ? "locked-squad seat" : "player-convoy seat",
            RequestedAtUnixSeconds = requestedAt,
            ExpiresAtUnixSeconds = expiresAt,
            UsesLockedDeck = usesLockedDeck,
            LockedDeckUnitIds = usesLockedDeck ? new[] { "brawler", "shooter", "defender" } : Array.Empty<string>()
        };
    }

    private static void ResetRoomState(string reason)
    {
        OnlineRoomCreateService.ClearHostedRoom(reason: reason);
        OnlineRoomJoinService.ClearCachedTicket(reason);
        OnlineRoomSessionService.ClearCachedSnapshot(reason);
        OnlineRoomResultService.ClearLastSubmission(reason);
        OnlineRoomScoreboardService.ClearCachedSnapshot(reason);
        OnlineRoomTelemetryService.ClearLastSubmission(reason);
        OnlineRoomSeatLeaseService.ClearLastLease(reason);
        OnlineRoomReportService.ClearLastReport(reason);
        OnlineRoomActionService.ClearLastAction(reason);
        OnlineRoomRecoveryService.ClearLastRecovery(reason);
        SetPrivateStaticField(typeof(OnlineRoomDirectoryService), "_cachedSnapshot", null);
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

echo "Running online room recovery smoke..."
dotnet run --project "$RUNNER_CSPROJ" >"$SMOKE_LOG" 2>&1 || SMOKE_STATUS=$?

if [[ $SMOKE_STATUS -ne 0 ]]; then
	echo "Online room recovery smoke failed."
	echo "--- smoke log ---"
	cat "$SMOKE_LOG"
	exit 1
fi

if ! grep -q "ROOM_RECOVERY_SMOKE PASS" "$SMOKE_LOG"; then
	echo "Room recovery smoke exited cleanly but did not report pass."
	cat "$SMOKE_LOG"
	exit 1
fi

echo "Online room recovery smoke passed."
echo "Smoke summary: $(grep 'ROOM_RECOVERY_SMOKE PASS' "$SMOKE_LOG" | tail -n 1)"
