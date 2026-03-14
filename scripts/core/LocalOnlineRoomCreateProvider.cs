using System;

public sealed class LocalOnlineRoomCreateProvider : IOnlineRoomCreateProvider
{
	public string Id => ChallengeSyncProviderCatalog.LocalJournalId;
	public string DisplayName => "Local Host Stub";

	public string BuildLocationSummary()
	{
		return "Source: generated local hosted-room result";
	}

	public OnlineRoomCreateResult CreateRoom(OnlineRoomCreateRequest request)
	{
		var requestedAt = request.RequestedAtUnixSeconds > 0
			? request.RequestedAtUnixSeconds
			: DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var roomId = $"ROOM-HOST-{requestedAt % 100000:00000}";
		var boardCode = AsyncChallengeCatalog.NormalizeCode(request.BoardCode);
		return new OnlineRoomCreateResult
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			RoomId = roomId,
			Title = $"{request.PlayerCallsign} Relay",
			Summary = request.UsesLockedDeck
				? "Hosted local internet-room stub using the selected locked async squad."
				: "Hosted local internet-room stub using the selected async board and player squad seats.",
			HostCallsign = string.IsNullOrWhiteSpace(request.PlayerCallsign) ? "Lantern Host" : request.PlayerCallsign.Trim(),
			BoardCode = boardCode,
			BoardTitle = string.IsNullOrWhiteSpace(request.BoardTitle) ? boardCode : request.BoardTitle.Trim(),
			CurrentPlayers = 1,
			MaxPlayers = 4,
			SpectatorCount = 0,
			Status = "lobby",
			Region = string.IsNullOrWhiteSpace(request.Region) ? "global" : request.Region.Trim(),
			TransportHint = "internet_room_hosted",
			RelayEndpoint = $"wss://local-relay.invalid/{roomId.ToLowerInvariant()}",
			UsesLockedDeck = request.UsesLockedDeck,
			LockedDeckUnitIds = request.UsesLockedDeck ? request.LockedDeckUnitIds ?? [] : [],
			HostTicket = new OnlineRoomJoinTicket
			{
				ProviderId = Id,
				ProviderDisplayName = DisplayName,
				RoomId = roomId,
				RoomTitle = $"{request.PlayerCallsign} Relay",
				BoardCode = boardCode,
				Status = "hosted",
				Summary = $"Hosted room {roomId} is ready to accept join requests.",
				TicketId = $"HOST-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}",
				JoinToken = $"host-{Guid.NewGuid():N}",
				TransportHint = "internet_room_hosted",
				RelayEndpoint = $"wss://local-relay.invalid/{roomId.ToLowerInvariant()}",
				SeatLabel = "host seat",
				RequestedAtUnixSeconds = requestedAt,
				ExpiresAtUnixSeconds = requestedAt + 3600,
				UsesLockedDeck = request.UsesLockedDeck,
				LockedDeckUnitIds = request.UsesLockedDeck ? request.LockedDeckUnitIds ?? [] : []
			}
		};
	}
}
