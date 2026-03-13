using System;

public sealed class LocalOnlineRoomTelemetryProvider : IOnlineRoomTelemetryProvider
{
	public string Id => ChallengeSyncProviderCatalog.LocalJournalId;
	public string DisplayName => "Local Room Telemetry Stub";

	public string BuildLocationSummary()
	{
		return "Source: generated local room telemetry";
	}

	public OnlineRoomTelemetrySubmission SubmitTelemetry(OnlineRoomJoinTicket ticket, OnlineRoomTelemetryRequest request)
	{
		var processedAt = request.RequestedAtUnixSeconds > 0
			? request.RequestedAtUnixSeconds
			: DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		LocalOnlineRoomStubState.UpdateTelemetry(
			ticket.RoomId,
			request.PlayerCallsign,
			request.ElapsedSeconds,
			request.EnemyDefeats,
			request.HullPercent);
		return new OnlineRoomTelemetrySubmission
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			RoomId = ticket.RoomId,
			BoardCode = ticket.BoardCode,
			TicketId = ticket.TicketId,
			Status = "accepted",
			Summary = $"Stub telemetry updated {request.PlayerCallsign} for {ticket.RoomTitle}.",
			ProcessedAtUnixSeconds = processedAt
		};
	}
}
