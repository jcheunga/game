using System;

public sealed class LocalOnlineRoomScoreboardProvider : IOnlineRoomScoreboardProvider
{
	public string Id => ChallengeSyncProviderCatalog.LocalJournalId;
	public string DisplayName => "Local Room Scoreboard Stub";

	public string BuildLocationSummary()
	{
		return "Source: generated local room-scoreboard cache";
	}

	public OnlineRoomScoreboardSnapshot FetchScoreboard(OnlineRoomJoinTicket ticket, int limit)
	{
		var rankedEntries = LocalOnlineRoomResultProvider.GetRankedEntries(ticket.RoomId, limit);
		return new OnlineRoomScoreboardSnapshot
		{
			RoomId = ticket.RoomId,
			BoardCode = ticket.BoardCode,
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			Status = rankedEntries.Count == 0 ? "empty" : "ok",
			Summary = rankedEntries.Count == 0
				? $"No local room results cached for {ticket.RoomTitle} yet."
				: $"Loaded {rankedEntries.Count} cached room result entr{(rankedEntries.Count == 1 ? "y" : "ies")} for {ticket.RoomTitle}.",
			FetchedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			Entries = [.. rankedEntries]
		};
	}
}
