public sealed class OnlineRoomActionRequest
{
	public string RoomId { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string TicketId { get; set; } = "";
	public string JoinToken { get; set; } = "";
	public string PlayerProfileId { get; set; } = "";
	public string PlayerCallsign { get; set; } = "";
	public string ActionId { get; set; } = "";
	public bool ReadyState { get; set; }
	public long RequestedAtUnixSeconds { get; set; }
}

public sealed class OnlineRoomActionResult
{
	public string ProviderId { get; set; } = "";
	public string ProviderDisplayName { get; set; } = "";
	public string RoomId { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string TicketId { get; set; } = "";
	public string ActionId { get; set; } = "";
	public string Status { get; set; } = "accepted";
	public string Summary { get; set; } = "";
	public bool ReadyState { get; set; }
	public long ProcessedAtUnixSeconds { get; set; }
}

public interface IOnlineRoomActionProvider
{
	string Id { get; }
	string DisplayName { get; }
	string BuildLocationSummary();
	OnlineRoomActionResult SendAction(OnlineRoomJoinTicket ticket, OnlineRoomActionRequest request);
}
