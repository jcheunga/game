using System;
using System.Linq;
using System.Text;

public static class OnlineRoomScoreboardService
{
	public static bool IsAvailable => true;

	private static readonly IOnlineRoomScoreboardProvider LocalProvider = new LocalOnlineRoomScoreboardProvider();
	private static OnlineRoomScoreboardSnapshot _cachedSnapshot;
	private static string _lastStatus = "Online room scoreboard not fetched yet.";

	public static bool RefreshJoinedRoomScoreboard(int limit, out string message)
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null)
		{
			message = "No cached join ticket. Join or host an online room first.";
			_lastStatus = message;
			return false;
		}

		if (OnlineRoomJoinService.IsTicketExpired(ticket))
		{
			message = $"Join ticket for {ticket.RoomTitle} has expired. Renew the room seat before refreshing the room scoreboard.";
			_lastStatus = message;
			return false;
		}

		// The production room-session endpoint includes the ordered peer scores.
		// Reuse a just-fetched session instead of immediately issuing a second
		// scoreboard request; this cuts steady room polling in half.
		var embeddedSnapshot = BuildEmbeddedSnapshot(ticket);
		if (embeddedSnapshot != null)
		{
			_cachedSnapshot = embeddedSnapshot;
			OnlineRoomSessionService.ApplyCachedScoreboardSnapshot();
			_lastStatus = "Reused the combined room session snapshot.";
			message = $"Updated room scoreboard for {ticket.RoomTitle} from the combined session snapshot.";
			return true;
		}

		var provider = ResolveProvider();
		try
		{
			_cachedSnapshot = provider.FetchScoreboard(ticket, limit);
			OnlineRoomSessionService.ApplyCachedScoreboardSnapshot();
			_lastStatus = $"{provider.DisplayName}: {_cachedSnapshot.Summary}";
			message = $"Refreshed online room scoreboard for {ticket.RoomTitle} via {provider.DisplayName}.";
			return true;
		}
		catch (Exception ex)
		{
			_lastStatus = $"{provider.DisplayName} room scoreboard fetch failed: {ex.Message}";
			message = _lastStatus;
			return false;
		}
	}

	public static OnlineRoomScoreboardSnapshot GetCachedSnapshot()
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

	public static string BuildStatusSummary(int maxEntries = 5)
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null)
		{
			return
				"Online room scoreboard:\n" +
				"No active online room ticket. Join or host a room first.\n" +
				$"Provider status: {_lastStatus}";
		}

		var currentSnapshot = GetCachedSnapshot();
		if (currentSnapshot == null)
		{
			return
				"Online room scoreboard:\n" +
				$"Room {ticket.RoomTitle} is armed, but no scoreboard snapshot is cached yet.\n" +
				"Refresh to load the latest results.\n" +
				$"Provider status: {_lastStatus}";
		}

		var builder = new StringBuilder();
		builder.AppendLine($"Online room scoreboard ({currentSnapshot.ProviderDisplayName}):");
		builder.AppendLine(currentSnapshot.Summary);
		if (currentSnapshot.Entries.Count == 0)
		{
			builder.Append($"No room results cached for {currentSnapshot.RoomId} yet.");
			return builder.ToString();
		}

		foreach (var entry in currentSnapshot.Entries.Take(Math.Max(1, maxEntries)))
		{
			builder.AppendLine(
				$"#{entry.Rank} {entry.PlayerCallsign} · {entry.Score} pts · Hull {entry.HullPercent}% · {entry.ElapsedSeconds:0.0}s · {(entry.Retreated ? "retreated" : entry.Won ? "cleared" : "failed")}");
		}

		return builder.ToString().TrimEnd();
	}

	private static IOnlineRoomScoreboardProvider ResolveProvider()
	{
		var providerId = ChallengeSyncProviderCatalog.NormalizeId(GameState.Instance?.ChallengeSyncProviderId ?? "");
		return providerId == ChallengeSyncProviderCatalog.HttpApiId
			? new HttpApiOnlineRoomScoreboardProvider(BuildHttpEndpoint(GameState.Instance?.ChallengeSyncEndpoint ?? ""))
			: LocalProvider;
	}

	private static OnlineRoomScoreboardSnapshot BuildEmbeddedSnapshot(OnlineRoomJoinTicket ticket)
	{
		var session = OnlineRoomSessionService.GetCachedSnapshot();
		var room = session?.RoomSnapshot;
		if (session == null || !session.IncludesScoreboard || room == null || !room.HasRoom ||
			!string.Equals(room.RoomId, ticket.RoomId, StringComparison.OrdinalIgnoreCase) ||
			DateTimeOffset.UtcNow.ToUnixTimeSeconds() - session.FetchedAtUnixSeconds > 15)
		{
			return null;
		}

		var rank = 0;
		var entries = room.Peers
			.Where(peer => peer.IsLaunchEligible)
			.OrderByDescending(peer => peer.PostedScore)
			.ThenBy(peer => peer.RaceElapsedSeconds < 0f ? float.MaxValue : peer.RaceElapsedSeconds)
			.Select(peer => new OnlineRoomScoreboardEntry
			{
				Rank = ++rank,
				RoomId = room.RoomId,
				BoardCode = room.SharedChallengeCode,
				PlayerCallsign = peer.Label,
				Score = Math.Max(0, peer.PostedScore),
				HullPercent = Math.Max(0, peer.HullPercent),
				ElapsedSeconds = Math.Max(0f, peer.RaceElapsedSeconds),
				EnemyDefeats = Math.Max(0, peer.EnemyDefeats),
				Won = peer.Phase.Equals("submitted", StringComparison.OrdinalIgnoreCase) ||
					peer.Phase.Equals("complete", StringComparison.OrdinalIgnoreCase),
				Retreated = peer.Phase.Equals("retreated", StringComparison.OrdinalIgnoreCase),
				SubmittedAtUnixSeconds = session.FetchedAtUnixSeconds
			})
			.ToList();

		return new OnlineRoomScoreboardSnapshot
		{
			RoomId = room.RoomId,
			BoardCode = room.SharedChallengeCode,
			ProviderId = session.ProviderId,
			ProviderDisplayName = session.ProviderDisplayName,
			Status = "ok",
			Summary = "Included in the current room session snapshot.",
			FetchedAtUnixSeconds = session.FetchedAtUnixSeconds,
			Entries = entries
		};
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
			return normalized[..^"/challenge-sync".Length] + "/challenge-room-scoreboard";
		}

		return normalized.TrimEnd('/') + "/challenge-room-scoreboard";
	}

	private static bool BelongsToTicket(OnlineRoomScoreboardSnapshot snapshot, OnlineRoomJoinTicket ticket)
	{
		if (snapshot == null || ticket == null)
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(snapshot.RoomId) &&
			!string.IsNullOrWhiteSpace(ticket.RoomId) &&
			!snapshot.RoomId.Equals(ticket.RoomId, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return string.IsNullOrWhiteSpace(snapshot.BoardCode) ||
			string.IsNullOrWhiteSpace(ticket.BoardCode) ||
			AsyncChallengeCatalog.NormalizeCode(snapshot.BoardCode)
				.Equals(AsyncChallengeCatalog.NormalizeCode(ticket.BoardCode), StringComparison.OrdinalIgnoreCase);
	}
}
