public sealed class OnlineRoomReportRequest
{
	public string RoomId { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string TicketId { get; set; } = "";
	public string JoinToken { get; set; } = "";
	public string PlayerProfileId { get; set; } = "";
	public string PlayerCallsign { get; set; } = "";
	public string SubjectType { get; set; } = "room";
	public string SubjectLabel { get; set; } = "";
	public string ReasonId { get; set; } = OnlineRoomReportReasonCatalog.SuspiciousScoreId;
	public string Notes { get; set; } = "";
	public long RequestedAtUnixSeconds { get; set; }
}

public sealed class OnlineRoomReportResult
{
	public string ProviderId { get; set; } = "";
	public string ProviderDisplayName { get; set; } = "";
	public string RoomId { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string ReportId { get; set; } = "";
	public string SubjectType { get; set; } = "room";
	public string SubjectLabel { get; set; } = "";
	public string ReasonId { get; set; } = OnlineRoomReportReasonCatalog.SuspiciousScoreId;
	public string Status { get; set; } = "accepted";
	public string Summary { get; set; } = "";
	public long SubmittedAtUnixSeconds { get; set; }
}

public interface IOnlineRoomReportProvider
{
	string Id { get; }
	string DisplayName { get; }
	string BuildLocationSummary();
	OnlineRoomReportResult SubmitReport(OnlineRoomJoinTicket ticket, OnlineRoomReportRequest request);
}

public sealed class OnlineRoomReportApiRequest
{
	public OnlineRoomReportRequest Report { get; set; } = new();
}

public sealed class OnlineRoomReportApiResponse
{
	public string RoomId { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string ReportId { get; set; } = "";
	public string SubjectType { get; set; } = "room";
	public string SubjectLabel { get; set; } = "";
	public string ReasonId { get; set; } = OnlineRoomReportReasonCatalog.SuspiciousScoreId;
	public string Status { get; set; } = "accepted";
	public string Message { get; set; } = "";
	public long SubmittedAtUnixSeconds { get; set; }
}
