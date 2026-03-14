using System;
using System.Collections.Generic;
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
		return QuickMatchChallenge(challenge, gameState.HasSelectedAsyncChallengeLockedDeck, out message);
	}

	public static bool QuickMatchBoard(string boardCode, bool wantsLockedDeckSeat, out string message)
	{
		if (!AsyncChallengeCatalog.TryParse(boardCode, out var challenge, out message))
		{
			_lastStatus = message;
			return false;
		}

		return QuickMatchChallenge(challenge, wantsLockedDeckSeat, out message);
	}

	private static bool QuickMatchChallenge(AsyncChallengeDefinition challenge, bool wantsLockedDeckSeat, out string message)
	{
		var gameState = GameState.Instance;
		if (gameState == null && ResolveProvider().Id == ChallengeSyncProviderCatalog.LocalJournalId)
		{
			_lastResult = BuildFallbackLocalResult(challenge, wantsLockedDeckSeat);
			_lastStatus = $"{LocalProvider.DisplayName}: {_lastResult.Summary}";
			return FinalizeMatchResult(_lastResult, LocalProvider.DisplayName, out message);
		}

		var request = new OnlineRoomMatchmakeRequest
		{
			BoardCode = challenge.Code,
			PlayerProfileId = ResolvePlayerProfileId(gameState),
			PlayerCallsign = ResolvePlayerCallsign(gameState),
			WantsLockedDeckSeat = wantsLockedDeckSeat,
			Region = "global",
			RequestedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		};

		var provider = ResolveProvider();
		try
		{
			_lastResult = provider.Matchmake(challenge, request);
			_lastStatus = $"{provider.DisplayName}: {_lastResult.Summary}";
			return FinalizeMatchResult(_lastResult, provider.DisplayName, out message);
		}
		catch (Exception ex)
		{
			_lastStatus = $"{provider.DisplayName} matchmaking failed: {ex.Message}";
			message = _lastStatus;
			return false;
		}
	}

	private static string ResolvePlayerProfileId(GameState gameState)
	{
		return string.IsNullOrWhiteSpace(gameState?.PlayerProfileId)
			? "CVY-LOCAL"
			: gameState.PlayerProfileId;
	}

	private static string ResolvePlayerCallsign(GameState gameState)
	{
		return string.IsNullOrWhiteSpace(gameState?.PlayerCallsign)
			? "Convoy"
			: gameState.PlayerCallsign;
	}

	private static bool FinalizeMatchResult(OnlineRoomMatchmakeResult result, string providerDisplayName, out string message)
	{
		if (result?.Room != null)
		{
			OnlineRoomDirectoryService.InjectRoom(result.Room);
		}

		var statusParts = new List<string>
		{
			$"Matched into {result?.Room?.Title ?? "remote room"} via {providerDisplayName}."
		};
		if (result?.JoinTicket != null &&
			OnlineRoomJoinService.AdoptNegotiatedTicket(result.JoinTicket, out var adoptMessage))
		{
			statusParts.Add(adoptMessage);
			if (GameState.Instance != null)
			{
				OnlineRoomSessionService.RefreshJoinedRoom(out var sessionMessage);
				statusParts.Add(sessionMessage);
				OnlineRoomScoreboardService.RefreshJoinedRoomScoreboard(5, out var scoreboardMessage);
				statusParts.Add(scoreboardMessage);
			}
		}

		message = string.Join("\n", statusParts);
		return true;
	}

	private static OnlineRoomMatchmakeResult BuildFallbackLocalResult(AsyncChallengeDefinition challenge, bool wantsLockedDeckSeat)
	{
		var roomId = $"match_{challenge.Stage}_{Math.Abs(challenge.Seed % 1000)}";
		var room = new OnlineRoomDirectoryEntry
		{
			RoomId = roomId,
			Title = wantsLockedDeckSeat ? "Quick Match Lockstep" : "Quick Match Relay",
			Summary = "Fallback local matchmaker seat for reduced-runtime recovery.",
			HostCallsign = wantsLockedDeckSeat ? "MatchRelay" : "OpenRelay",
			BoardCode = challenge.Code,
			BoardTitle = $"Board {challenge.Code}",
			CurrentPlayers = wantsLockedDeckSeat ? 3 : 2,
			MaxPlayers = 4,
			SpectatorCount = 0,
			Status = "lobby",
			Region = "global",
			UsesLockedDeck = wantsLockedDeckSeat,
			LockedDeckUnitIds = wantsLockedDeckSeat
				? new[] { GameData.PlayerBrawlerId, GameData.PlayerShooterId, GameData.PlayerDefenderId }
				: []
		};
		var requestedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		return new OnlineRoomMatchmakeResult
		{
			ProviderId = LocalProvider.Id,
			ProviderDisplayName = LocalProvider.DisplayName,
			Status = "accepted",
			Summary = $"Matched Convoy into {room.Title}.",
			CreatedNewRoom = false,
			Room = room,
			JoinTicket = new OnlineRoomJoinTicket
			{
				ProviderId = LocalProvider.Id,
				ProviderDisplayName = LocalProvider.DisplayName,
				RoomId = room.RoomId,
				RoomTitle = room.Title,
				BoardCode = room.BoardCode,
				Status = "accepted",
				Summary = $"Matched into {room.Title}.",
				TicketId = $"JOIN-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}",
				JoinToken = $"matched-{Guid.NewGuid():N}",
				TransportHint = "internet_room_stub",
				RelayEndpoint = $"stub://room/{room.RoomId}",
				SeatLabel = wantsLockedDeckSeat ? "locked-squad seat" : "player-convoy seat",
				RequestedAtUnixSeconds = requestedAt,
				ExpiresAtUnixSeconds = requestedAt + 180,
				UsesLockedDeck = room.UsesLockedDeck,
				LockedDeckUnitIds = room.UsesLockedDeck ? room.LockedDeckUnitIds : []
			}
		};
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
