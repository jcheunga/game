using System;

public sealed class LocalOnlineRoomActionProvider : IOnlineRoomActionProvider
{
	public string Id => ChallengeSyncProviderCatalog.LocalJournalId;
	public string DisplayName => "Local Action Stub";

	public string BuildLocationSummary()
	{
		return "Source: generated local room actions";
	}

	public OnlineRoomActionResult SendAction(OnlineRoomJoinTicket ticket, OnlineRoomActionRequest request)
	{
		var processedAt = request.RequestedAtUnixSeconds > 0
			? request.RequestedAtUnixSeconds
			: DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		if (string.Equals(request.ActionId, "launch_round", StringComparison.OrdinalIgnoreCase))
		{
			LocalOnlineRoomStubState.MarkRoundLaunched(ticket.RoomId);
		}
		else if (string.Equals(request.ActionId, "reset_round", StringComparison.OrdinalIgnoreCase))
		{
			LocalOnlineRoomStubState.MarkRoundReset(ticket.RoomId);
			LocalOnlineRoomResultProvider.ClearRoom(ticket.RoomId);
		}
		else if (string.Equals(request.ActionId, "leave_room", StringComparison.OrdinalIgnoreCase))
		{
			LocalOnlineRoomStubState.MarkRoundReset(ticket.RoomId);
			LocalOnlineRoomResultProvider.ClearRoom(ticket.RoomId);
		}

		var actionLabel = string.Equals(request.ActionId, "launch_round", StringComparison.OrdinalIgnoreCase)
			? "launched the round countdown"
			: string.Equals(request.ActionId, "reset_round", StringComparison.OrdinalIgnoreCase)
				? "reset the room for rematch"
			: string.Equals(request.ActionId, "leave_room", StringComparison.OrdinalIgnoreCase)
				? "left the room"
				: request.ReadyState
					? "ready"
					: "standing by";
		return new OnlineRoomActionResult
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			RoomId = ticket.RoomId,
			BoardCode = ticket.BoardCode,
			TicketId = ticket.TicketId,
			ActionId = string.IsNullOrWhiteSpace(request.ActionId) ? "set_ready" : request.ActionId,
			Status = "accepted",
			Summary = string.Equals(request.ActionId, "launch_round", StringComparison.OrdinalIgnoreCase)
				? $"Stub room action launched {ticket.RoomTitle} into countdown."
				: string.Equals(request.ActionId, "reset_round", StringComparison.OrdinalIgnoreCase)
					? $"Stub room action reset {ticket.RoomTitle} for rematch."
				: string.Equals(request.ActionId, "leave_room", StringComparison.OrdinalIgnoreCase)
					? $"Stub room action removed {request.PlayerCallsign} from {ticket.RoomTitle}."
					: $"Stub room action marked {request.PlayerCallsign} as {actionLabel} in {ticket.RoomTitle}.",
			ReadyState = request.ReadyState,
			ProcessedAtUnixSeconds = processedAt
		};
	}
}
