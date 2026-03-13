public sealed class OnlineRoomTelemetryRequest
{
	public string RoomId { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string TicketId { get; set; } = "";
	public string JoinToken { get; set; } = "";
	public string PlayerProfileId { get; set; } = "";
	public string PlayerCallsign { get; set; } = "";
	public float ElapsedSeconds { get; set; }
	public int EnemyDefeats { get; set; }
	public int HullPercent { get; set; }
	public long RequestedAtUnixSeconds { get; set; }
}

public sealed class OnlineRoomTelemetrySubmission
{
	public string ProviderId { get; set; } = "";
	public string ProviderDisplayName { get; set; } = "";
	public string RoomId { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string TicketId { get; set; } = "";
	public string Status { get; set; } = "accepted";
	public string Summary { get; set; } = "";
	public long ProcessedAtUnixSeconds { get; set; }
}

public interface IOnlineRoomTelemetryProvider
{
	string Id { get; }
	string DisplayName { get; }
	string BuildLocationSummary();
	OnlineRoomTelemetrySubmission SubmitTelemetry(OnlineRoomJoinTicket ticket, OnlineRoomTelemetryRequest request);
}
