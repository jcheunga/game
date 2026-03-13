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

		var provider = ResolveProvider();
		try
		{
			_cachedSnapshot = provider.FetchScoreboard(ticket, limit);
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
		return _cachedSnapshot;
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

		if (_cachedSnapshot == null)
		{
			return
				"Online room scoreboard:\n" +
				$"Room {ticket.RoomTitle} is armed, but no scoreboard snapshot is cached yet.\n" +
				"Use `Refresh Online` or `Refresh Room Scoreboard` to pull results.\n" +
				$"Provider status: {_lastStatus}";
		}

		var builder = new StringBuilder();
		builder.AppendLine($"Online room scoreboard ({_cachedSnapshot.ProviderDisplayName}):");
		builder.AppendLine(_cachedSnapshot.Summary);
		if (_cachedSnapshot.Entries.Count == 0)
		{
			builder.Append($"No room results cached for {_cachedSnapshot.RoomId} yet.");
			return builder.ToString();
		}

		foreach (var entry in _cachedSnapshot.Entries.Take(Math.Max(1, maxEntries)))
		{
			builder.AppendLine(
				$"#{entry.Rank} {entry.PlayerCallsign}  |  {entry.Score} pts  |  Hull {entry.HullPercent}%  |  {entry.ElapsedSeconds:0.0}s  |  {(entry.Retreated ? "retreated" : entry.Won ? "cleared" : "failed")}");
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
}
