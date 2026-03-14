using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class OnlineRoomSessionService
{
	public static bool IsAvailable => true;

	private static readonly IOnlineRoomSessionProvider LocalProvider = new LocalOnlineRoomSessionProvider();
	private static OnlineRoomSessionSnapshot _cachedSnapshot;
	private static string _lastStatus = "Online room session not fetched yet.";

	public static bool RefreshJoinedRoom(out string message)
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null)
		{
			message = "No cached join ticket. Request Join on a room listing first.";
			_lastStatus = message;
			return false;
		}

		if (OnlineRoomJoinService.IsTicketExpired(ticket))
		{
			message = $"Join ticket for {ticket.RoomTitle} has expired. Renew the room seat before refreshing the room session.";
			_lastStatus = message;
			return false;
		}

		var provider = ResolveProvider();
		try
		{
			_cachedSnapshot = provider.FetchRoomSession(ticket);
			ApplyCachedScoreboardSnapshot();
			_lastStatus = $"{provider.DisplayName}: {_cachedSnapshot.Summary}";
			message = $"Refreshed online room session for {ticket.RoomTitle} via {provider.DisplayName}.";
			return true;
		}
		catch (Exception ex)
		{
			_lastStatus = $"{provider.DisplayName} session fetch failed: {ex.Message}";
			message = _lastStatus;
			return false;
		}
	}

	public static OnlineRoomSessionSnapshot GetCachedSnapshot()
	{
		return BelongsToTicket(_cachedSnapshot, OnlineRoomJoinService.GetCachedTicket()) ? _cachedSnapshot : null;
	}

	public static void ClearCachedSnapshot(string reason = "")
	{
		_cachedSnapshot = null;
		if (!string.IsNullOrWhiteSpace(reason))
		{
			_lastStatus = reason;
		}
	}

	public static void ApplyCachedScoreboardSnapshot()
	{
		var currentSnapshot = GetCachedSnapshot();
		if (currentSnapshot == null || currentSnapshot.RoomSnapshot == null || !currentSnapshot.RoomSnapshot.HasRoom)
		{
			return;
		}

		_cachedSnapshot = MergeScoreboardIntoSnapshot(currentSnapshot, OnlineRoomScoreboardService.GetCachedSnapshot());
	}

	public static string BuildStatusSummary()
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null)
		{
			return
				"Online room session:\n" +
				"No join ticket cached yet. Request Join first, then `Refresh Online` will pull the room lobby snapshot.\n" +
				$"Provider status: {_lastStatus}";
		}

		var currentSnapshot = GetCachedSnapshot();
		if (currentSnapshot == null || currentSnapshot.RoomSnapshot == null || !currentSnapshot.RoomSnapshot.HasRoom)
		{
			return
				"Online room session:\n" +
				$"Join ticket ready for {ticket.RoomTitle}, but no room snapshot is cached yet.\n" +
				"Use `Refresh Online` to pull the current runner/ready state.\n" +
				$"Provider status: {_lastStatus}";
		}

		var builder = new StringBuilder();
		builder.AppendLine($"Online room session ({currentSnapshot.ProviderDisplayName}):");
		builder.AppendLine(currentSnapshot.Summary);
		builder.AppendLine(MultiplayerRoomFormatter.BuildRoomSummary(currentSnapshot.RoomSnapshot));
		builder.AppendLine();
		builder.Append(MultiplayerRoomFormatter.BuildRaceMonitorSummary(currentSnapshot.RoomSnapshot));
		return builder.ToString().TrimEnd();
	}

	private static IOnlineRoomSessionProvider ResolveProvider()
	{
		var providerId = ChallengeSyncProviderCatalog.NormalizeId(GameState.Instance?.ChallengeSyncProviderId ?? "");
		return providerId == ChallengeSyncProviderCatalog.HttpApiId
			? new HttpApiOnlineRoomSessionProvider(BuildHttpEndpoint(GameState.Instance?.ChallengeSyncEndpoint ?? ""))
			: LocalProvider;
	}

	private static string BuildHttpEndpoint(string syncEndpoint)
	{
		var normalized = string.IsNullOrWhiteSpace(syncEndpoint) ? "" : syncEndpoint.Trim();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return "";
		}

		if (normalized.EndsWith("/challenge-sync", StringComparison.OrdinalIgnoreCase))
		{
			return normalized[..^"/challenge-sync".Length] + "/challenge-room-session";
		}

		return normalized.TrimEnd('/') + "/challenge-room-session";
	}

	private static OnlineRoomSessionSnapshot MergeScoreboardIntoSnapshot(OnlineRoomSessionSnapshot snapshot, OnlineRoomScoreboardSnapshot scoreboardSnapshot)
	{
		if (snapshot == null ||
			snapshot.RoomSnapshot == null ||
			!snapshot.RoomSnapshot.HasRoom ||
			scoreboardSnapshot == null ||
			scoreboardSnapshot.Entries == null ||
			scoreboardSnapshot.Entries.Count == 0)
		{
			return snapshot;
		}

		if (!BelongsToSameRoom(snapshot.RoomSnapshot, scoreboardSnapshot))
		{
			return snapshot;
		}

		var entriesByCallsign = new Dictionary<string, OnlineRoomScoreboardEntry>(StringComparer.OrdinalIgnoreCase);
		foreach (var entry in scoreboardSnapshot.Entries)
		{
			if (string.IsNullOrWhiteSpace(entry.PlayerCallsign))
			{
				continue;
			}

			entriesByCallsign[entry.PlayerCallsign.Trim()] = entry;
		}

		if (entriesByCallsign.Count == 0)
		{
			return snapshot;
		}

		var mergedPeers = snapshot.RoomSnapshot.Peers
			.Select(peer => MergePeerWithScoreboard(peer, entriesByCallsign))
			.ToArray();
		return new OnlineRoomSessionSnapshot
		{
			ProviderId = snapshot.ProviderId,
			ProviderDisplayName = snapshot.ProviderDisplayName,
			Status = snapshot.Status,
			Summary = snapshot.Summary,
			FetchedAtUnixSeconds = snapshot.FetchedAtUnixSeconds,
			RoomSnapshot = new MultiplayerRoomSnapshot
			{
				HasRoom = snapshot.RoomSnapshot.HasRoom,
				RoomId = snapshot.RoomSnapshot.RoomId,
				RoomTitle = snapshot.RoomSnapshot.RoomTitle,
				TransportLabel = snapshot.RoomSnapshot.TransportLabel,
				RoleLabel = snapshot.RoomSnapshot.RoleLabel,
				PeerCount = snapshot.RoomSnapshot.PeerCount,
				SharedChallengeCode = snapshot.RoomSnapshot.SharedChallengeCode,
				SharedChallengeTitle = snapshot.RoomSnapshot.SharedChallengeTitle,
				LocalCallsign = snapshot.RoomSnapshot.LocalCallsign,
				DeckModeSummary = snapshot.RoomSnapshot.DeckModeSummary,
				JoinAddressSummary = snapshot.RoomSnapshot.JoinAddressSummary,
				UsesLockedDeck = snapshot.RoomSnapshot.UsesLockedDeck,
				RoundLocked = snapshot.RoomSnapshot.RoundLocked,
				RoundComplete = snapshot.RoomSnapshot.RoundComplete,
				RaceCountdownActive = snapshot.RoomSnapshot.RaceCountdownActive,
				RaceCountdownRemainingSeconds = snapshot.RoomSnapshot.RaceCountdownRemainingSeconds,
				SelectedBoardCode = snapshot.RoomSnapshot.SelectedBoardCode,
				SelectedBoardDeckMode = snapshot.RoomSnapshot.SelectedBoardDeckMode,
				Peers = mergedPeers
			}
		};
	}

	private static bool BelongsToSameRoom(MultiplayerRoomSnapshot roomSnapshot, OnlineRoomScoreboardSnapshot scoreboardSnapshot)
	{
		if (roomSnapshot == null || scoreboardSnapshot == null)
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(roomSnapshot.RoomId) &&
			!string.IsNullOrWhiteSpace(scoreboardSnapshot.RoomId) &&
			!roomSnapshot.RoomId.Equals(scoreboardSnapshot.RoomId, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(roomSnapshot.SelectedBoardCode) &&
			!string.IsNullOrWhiteSpace(scoreboardSnapshot.BoardCode) &&
			!AsyncChallengeCatalog.NormalizeCode(scoreboardSnapshot.BoardCode)
				.Equals(AsyncChallengeCatalog.NormalizeCode(roomSnapshot.SelectedBoardCode), StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return true;
	}

	private static bool BelongsToTicket(OnlineRoomSessionSnapshot snapshot, OnlineRoomJoinTicket ticket)
	{
		if (snapshot == null || snapshot.RoomSnapshot == null || !snapshot.RoomSnapshot.HasRoom || ticket == null)
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(snapshot.RoomSnapshot.RoomId) &&
			!string.IsNullOrWhiteSpace(ticket.RoomId) &&
			!snapshot.RoomSnapshot.RoomId.Equals(ticket.RoomId, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return string.IsNullOrWhiteSpace(snapshot.RoomSnapshot.SelectedBoardCode) ||
			string.IsNullOrWhiteSpace(ticket.BoardCode) ||
			AsyncChallengeCatalog.NormalizeCode(snapshot.RoomSnapshot.SelectedBoardCode)
				.Equals(AsyncChallengeCatalog.NormalizeCode(ticket.BoardCode), StringComparison.OrdinalIgnoreCase);
	}

	private static MultiplayerRoomPeerSnapshot MergePeerWithScoreboard(
		MultiplayerRoomPeerSnapshot peer,
		IReadOnlyDictionary<string, OnlineRoomScoreboardEntry> entriesByCallsign)
	{
		if (peer == null || string.IsNullOrWhiteSpace(peer.Label) || !entriesByCallsign.TryGetValue(peer.Label, out var entry))
		{
			return peer;
		}

		return new MultiplayerRoomPeerSnapshot
		{
			PeerId = peer.PeerId,
			Label = peer.Label,
			IsLocalPlayer = peer.IsLocalPlayer,
			Phase = "submitted",
			IsReady = peer.IsReady,
			IsLoaded = peer.IsLoaded,
			IsLaunchEligible = peer.IsLaunchEligible,
			HasFullDeck = peer.HasFullDeck,
			MonitorRank = peer.MonitorRank,
			RaceElapsedSeconds = peer.RaceElapsedSeconds >= 0f ? peer.RaceElapsedSeconds : entry.ElapsedSeconds,
			HullPercent = peer.HullPercent >= 0 ? peer.HullPercent : entry.HullPercent,
			EnemyDefeats = peer.EnemyDefeats >= 0 ? peer.EnemyDefeats : entry.EnemyDefeats,
			PostedScore = entry.Score,
			PostedRank = entry.Rank,
			PresenceText = entry.Rank > 0
				? $"result submitted, provisional #{entry.Rank}"
				: "result submitted, awaiting standings",
			MonitorText = entry.Rank > 0
				? $"{peer.Label}  |  submitted  |  #{entry.Rank}  |  {entry.Score} pts"
				: $"{peer.Label}  |  submitted  |  {entry.Score} pts",
			DeckText = peer.DeckText
		};
	}
}
