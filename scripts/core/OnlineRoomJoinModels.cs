public sealed class OnlineRoomJoinRequest
{
	public string RoomId { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string PlayerProfileId { get; set; } = "";
	public string PlayerCallsign { get; set; } = "";
	public bool WantsLockedDeckSeat { get; set; }
	public long RequestedAtUnixSeconds { get; set; }
}

public sealed class OnlineRoomJoinTicket
{
	public string ProviderId { get; set; } = "";
	public string ProviderDisplayName { get; set; } = "";
	public string RoomId { get; set; } = "";
	public string RoomTitle { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string Status { get; set; } = "pending";
	public string Summary { get; set; } = "";
	public string TicketId { get; set; } = "";
	public string JoinToken { get; set; } = "";
	public string TransportHint { get; set; } = "";
	public string RelayEndpoint { get; set; } = "";
	public string SeatLabel { get; set; } = "";
	public long RequestedAtUnixSeconds { get; set; }
	public long ExpiresAtUnixSeconds { get; set; }
	public bool UsesLockedDeck { get; set; }
	public string[] LockedDeckUnitIds { get; set; } = [];
}

public interface IOnlineRoomJoinProvider
{
	string Id { get; }
	string DisplayName { get; }
	string BuildLocationSummary();
	OnlineRoomJoinTicket RequestJoin(OnlineRoomDirectoryEntry room, OnlineRoomJoinRequest request);
}

public sealed class OnlineRoomJoinApiRequest
{
	public OnlineRoomJoinRequest Join { get; set; } = new();
}

public sealed class OnlineRoomJoinApiResponse
{
	public string Status { get; set; } = "accepted";
	public string Message { get; set; } = "";
	public string RoomId { get; set; } = "";
	public string RoomTitle { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string TicketId { get; set; } = "";
	public string JoinToken { get; set; } = "";
	public string TransportHint { get; set; } = "";
	public string RelayEndpoint { get; set; } = "";
	public string SeatLabel { get; set; } = "";
	public long ExpiresAtUnixSeconds { get; set; }
	public bool UsesLockedDeck { get; set; }
	public string[] LockedDeckUnitIds { get; set; } = [];
}
