using System;

public sealed class LocalOnlineRoomSeatLeaseProvider : IOnlineRoomSeatLeaseProvider
{
	public string Id => ChallengeSyncProviderCatalog.LocalJournalId;
	public string DisplayName => "Local Seat Lease";

	public string BuildLocationSummary()
	{
		return "Source: generated local room-seat lease";
	}

	public OnlineRoomSeatLeaseResult RenewSeat(OnlineRoomJoinTicket ticket, OnlineRoomSeatLeaseRequest request)
	{
		var renewedAt = Math.Max(request.RequestedAtUnixSeconds, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
		var nextExpiry = Math.Max(ticket?.ExpiresAtUnixSeconds ?? 0, renewedAt + 180);
		if (request.ForceRefresh)
		{
			nextExpiry = renewedAt + 180;
		}

		return new OnlineRoomSeatLeaseResult
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			RoomId = ticket?.RoomId ?? request.RoomId,
			BoardCode = ticket?.BoardCode ?? request.BoardCode,
			TicketId = ticket?.TicketId ?? request.TicketId,
			JoinToken = ticket?.JoinToken ?? request.JoinToken,
			Status = "accepted",
			Summary = $"Stub seat lease refreshed for {ticket?.RoomTitle ?? request.RoomId}.",
			ExpiresAtUnixSeconds = nextExpiry,
			RenewedAtUnixSeconds = renewedAt
		};
	}
}
