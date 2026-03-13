using System;
using System.Text;

public static class OnlineRoomMatchmakeService
{
	public static bool IsAvailable => true;

	private static readonly IOnlineRoomMatchmakeProvider LocalProvider = new LocalOnlineRoomMatchmakeProvider();
	private static OnlineRoomMatchmakeResult _lastResult;
	private static string _lastStatus = "Online room matchmaking not requested yet.";

	public static bool QuickMatchSelectedChallenge(out string message)
	{
		var gameState = GameState.Instance;
		if (gameState == null)
		{
			message = "Game state is unavailable.";
			_lastStatus = message;
			return false;
		}

		var challenge = gameState.GetSelectedAsyncChallenge();
		var request = new OnlineRoomMatchmakeRequest
		{
			BoardCode = challenge.Code,
			PlayerProfileId = gameState.PlayerProfileId,
			PlayerCallsign = gameState.PlayerCallsign,
			WantsLockedDeckSeat = gameState.HasSelectedAsyncChallengeLockedDeck,
			Region = "global",
			RequestedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		};

		var provider = ResolveProvider();
		try
		{
			_lastResult = provider.Matchmake(challenge, request);
			_lastStatus = $"{provider.DisplayName}: {_lastResult.Summary}";

			if (_lastResult.Room != null)
			{
				OnlineRoomDirectoryService.InjectRoom(_lastResult.Room);
			}

			var statusParts = new System.Collections.Generic.List<string>
			{
				$"Matched into {_lastResult.Room?.Title ?? challenge.Code} via {provider.DisplayName}."
			};
			if (_lastResult.JoinTicket != null &&
				OnlineRoomJoinService.AdoptNegotiatedTicket(_lastResult.JoinTicket, out var adoptMessage))
			{
				statusParts.Add(adoptMessage);
				OnlineRoomSessionService.RefreshJoinedRoom(out var sessionMessage);
				statusParts.Add(sessionMessage);
				OnlineRoomScoreboardService.RefreshJoinedRoomScoreboard(5, out var scoreboardMessage);
				statusParts.Add(scoreboardMessage);
			}

			message = string.Join("\n", statusParts);
			return true;
		}
		catch (Exception ex)
		{
			_lastStatus = $"{provider.DisplayName} matchmaking failed: {ex.Message}";
			message = _lastStatus;
			return false;
		}
	}

	public static string BuildStatusSummary()
	{
		if (_lastResult == null || _lastResult.Room == null)
		{
			return
				"Online room matchmaker:\n" +
				"No backend quick-match attempt cached yet.\n" +
				$"Provider status: {_lastStatus}";
		}

		var builder = new StringBuilder();
		builder.AppendLine($"Online room matchmaker ({_lastResult.ProviderDisplayName}):");
		builder.AppendLine(_lastResult.Summary);
		builder.AppendLine($"Room: {_lastResult.Room.Title}  |  Board: {_lastResult.Room.BoardCode}  |  Status: {_lastResult.Status}");
		builder.Append($"Created new room: {(_lastResult.CreatedNewRoom ? "yes" : "no")}");
		return builder.ToString();
	}

	private static IOnlineRoomMatchmakeProvider ResolveProvider()
	{
		var providerId = ChallengeSyncProviderCatalog.NormalizeId(GameState.Instance?.ChallengeSyncProviderId ?? "");
		return providerId == ChallengeSyncProviderCatalog.HttpApiId
			? new HttpApiOnlineRoomMatchmakeProvider(BuildHttpEndpoint(GameState.Instance?.ChallengeSyncEndpoint ?? ""))
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
			return normalized[..^"/challenge-sync".Length] + "/challenge-room-matchmake";
		}

		return normalized.TrimEnd('/') + "/challenge-room-matchmake";
	}
}
