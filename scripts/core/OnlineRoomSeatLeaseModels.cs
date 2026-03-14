public sealed class OnlineRoomSeatLeaseRequest
{
	public string RoomId { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string TicketId { get; set; } = "";
	public string JoinToken { get; set; } = "";
	public string PlayerProfileId { get; set; } = "";
	public string PlayerCallsign { get; set; } = "";
	public bool ForceRefresh { get; set; }
	public long RequestedAtUnixSeconds { get; set; }
}

public sealed class OnlineRoomSeatLeaseResult
{
	public string ProviderId { get; set; } = "";
	public string ProviderDisplayName { get; set; } = "";
	public string RoomId { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string TicketId { get; set; } = "";
	public string JoinToken { get; set; } = "";
	public string Status { get; set; } = "accepted";
	public string Summary { get; set; } = "";
	public long ExpiresAtUnixSeconds { get; set; }
	public long RenewedAtUnixSeconds { get; set; }
}

public interface IOnlineRoomSeatLeaseProvider
{
	string Id { get; }
	string DisplayName { get; }
	string BuildLocationSummary();
	OnlineRoomSeatLeaseResult RenewSeat(OnlineRoomJoinTicket ticket, OnlineRoomSeatLeaseRequest request);
}

public sealed class OnlineRoomSeatLeaseApiRequest
{
	public OnlineRoomSeatLeaseRequest Lease { get; set; } = new();
}

public sealed class OnlineRoomSeatLeaseApiResponse
{
	public string Status { get; set; } = "accepted";
	public string Message { get; set; } = "";
	public string RoomId { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string TicketId { get; set; } = "";
	public string JoinToken { get; set; } = "";
	public long ExpiresAtUnixSeconds { get; set; }
	public long RenewedAtUnixSeconds { get; set; }
}
