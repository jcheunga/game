using System;

public sealed class LocalOnlineRoomReportProvider : IOnlineRoomReportProvider
{
	public string Id => ChallengeSyncProviderCatalog.LocalJournalId;
	public string DisplayName => "Local Report Stub";

	public string BuildLocationSummary()
	{
		return "Source: generated local room report";
	}

	public OnlineRoomReportResult SubmitReport(OnlineRoomJoinTicket ticket, OnlineRoomReportRequest request)
	{
		return new OnlineRoomReportResult
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			RoomId = ticket?.RoomId ?? request.RoomId,
			BoardCode = ticket?.BoardCode ?? request.BoardCode,
			ReportId = $"RPT-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}",
			SubjectType = request.SubjectType,
			SubjectLabel = request.SubjectLabel,
			ReasonId = OnlineRoomReportReasonCatalog.NormalizeId(request.ReasonId),
			Status = "accepted",
			Summary = $"Stub report filed for {request.SubjectLabel} in {ticket?.RoomTitle ?? request.RoomId}.",
			SubmittedAtUnixSeconds = Math.Max(request.RequestedAtUnixSeconds, DateTimeOffset.UtcNow.ToUnixTimeSeconds())
		};
	}
}
