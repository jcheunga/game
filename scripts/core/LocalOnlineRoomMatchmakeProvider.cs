using System;
using System.Linq;

public sealed class LocalOnlineRoomMatchmakeProvider : IOnlineRoomMatchmakeProvider
{
	private static readonly LocalOnlineRoomJoinProvider JoinProvider = new();

	public string Id => ChallengeSyncProviderCatalog.LocalJournalId;
	public string DisplayName => "Local Matchmaker Stub";

	public string BuildLocationSummary()
	{
		return "Source: generated local matchmaking seats";
	}

	public OnlineRoomMatchmakeResult Matchmake(AsyncChallengeDefinition challenge, OnlineRoomMatchmakeRequest request)
	{
		var stage = GameData.GetStage(challenge.Stage);
		var lockedDeckUnitIds = request.WantsLockedDeckSeat
			? GameState.Instance?.GetSelectedAsyncChallengeDeckUnits().Select(unit => unit.Id).Take(3).ToArray() ?? []
			: [];
		var room = new OnlineRoomDirectoryEntry
		{
			RoomId = $"match_{challenge.Stage}_{Math.Abs(challenge.Seed % 1000)}",
			Title = request.WantsLockedDeckSeat ? "Quick Match Lockstep" : "Quick Match Relay",
			Summary = "Local matchmaker stub negotiated the best open seat for the selected async board.",
			HostCallsign = request.WantsLockedDeckSeat ? "MatchRelay" : "OpenRelay",
			BoardCode = challenge.Code,
			BoardTitle = $"{stage.MapName} S{stage.StageNumber} {stage.StageName}",
			CurrentPlayers = request.WantsLockedDeckSeat ? 3 : 2,
			MaxPlayers = 4,
			SpectatorCount = 0,
			Status = "lobby",
			Region = string.IsNullOrWhiteSpace(request.Region) ? "global" : request.Region,
			UsesLockedDeck = request.WantsLockedDeckSeat,
			LockedDeckUnitIds = request.WantsLockedDeckSeat ? lockedDeckUnitIds : []
		};
		var joinTicket = JoinProvider.RequestJoin(room, new OnlineRoomJoinRequest
		{
			RoomId = room.RoomId,
			BoardCode = room.BoardCode,
			PlayerProfileId = request.PlayerProfileId,
			PlayerCallsign = request.PlayerCallsign,
			WantsLockedDeckSeat = request.WantsLockedDeckSeat,
			RequestedAtUnixSeconds = request.RequestedAtUnixSeconds
		});

		return new OnlineRoomMatchmakeResult
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			Status = "accepted",
			Summary = $"Matched {request.PlayerCallsign} into {room.Title}.",
			CreatedNewRoom = false,
			Room = room,
			JoinTicket = joinTicket
		};
	}
}
