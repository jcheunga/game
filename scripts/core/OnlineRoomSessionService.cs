using System;
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

		var provider = ResolveProvider();
		try
		{
			_cachedSnapshot = provider.FetchRoomSession(ticket);
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

		if (_cachedSnapshot == null || _cachedSnapshot.RoomSnapshot == null || !_cachedSnapshot.RoomSnapshot.HasRoom)
		{
			return
				"Online room session:\n" +
				$"Join ticket ready for {ticket.RoomTitle}, but no room snapshot is cached yet.\n" +
				"Use `Refresh Online` to pull the current runner/ready state.\n" +
				$"Provider status: {_lastStatus}";
		}

		var builder = new StringBuilder();
		builder.AppendLine($"Online room session ({_cachedSnapshot.ProviderDisplayName}):");
		builder.AppendLine(_cachedSnapshot.Summary);
		builder.AppendLine(MultiplayerRoomFormatter.BuildRoomSummary(_cachedSnapshot.RoomSnapshot));
		builder.AppendLine();
		builder.Append(MultiplayerRoomFormatter.BuildRaceMonitorSummary(_cachedSnapshot.RoomSnapshot));
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
}
