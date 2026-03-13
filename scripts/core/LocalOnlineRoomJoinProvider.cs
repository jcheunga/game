using System;

public sealed class LocalOnlineRoomJoinProvider : IOnlineRoomJoinProvider
{
	public string Id => ChallengeSyncProviderCatalog.LocalJournalId;
	public string DisplayName => "Local Room Stub";

	public string BuildLocationSummary()
	{
		return "Source: generated local join tickets";
	}

	public OnlineRoomJoinTicket RequestJoin(OnlineRoomDirectoryEntry room, OnlineRoomJoinRequest request)
	{
		var requestedAt = request.RequestedAtUnixSeconds > 0
			? request.RequestedAtUnixSeconds
			: DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var normalizedStatus = string.Equals(room.Status, "racing", StringComparison.OrdinalIgnoreCase)
			? "spectate"
			: room.CurrentPlayers >= room.MaxPlayers
				? "waitlist"
				: "accepted";
		var summary = normalizedStatus switch
		{
			"spectate" => $"Room {room.Title} is already racing. Stub join ticket marked spectator-only until the next round.",
			"waitlist" => $"Room {room.Title} is full. Stub join ticket placed this convoy on the waitlist.",
			_ => $"Stub join ticket reserved a seat in {room.Title}."
		};

		return new OnlineRoomJoinTicket
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			RoomId = room.RoomId,
			RoomTitle = room.Title,
			BoardCode = room.BoardCode,
			Status = normalizedStatus,
			Summary = summary,
			TicketId = $"JOIN-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}",
			JoinToken = $"stub-{Guid.NewGuid():N}",
			TransportHint = "internet_room_stub",
			RelayEndpoint = $"stub://room/{room.RoomId}",
			SeatLabel = room.UsesLockedDeck ? "locked-squad seat" : "player-convoy seat",
			RequestedAtUnixSeconds = requestedAt,
			ExpiresAtUnixSeconds = requestedAt + 180,
			UsesLockedDeck = room.UsesLockedDeck,
			LockedDeckUnitIds = room.UsesLockedDeck ? room.LockedDeckUnitIds ?? [] : []
		};
	}
}
