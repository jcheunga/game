public sealed class OnlineRoomMatchmakeRequest
{
	public string BoardCode { get; set; } = "";
	public string PlayerProfileId { get; set; } = "";
	public string PlayerCallsign { get; set; } = "";
	public bool WantsLockedDeckSeat { get; set; }
	public string Region { get; set; } = "";
	public long RequestedAtUnixSeconds { get; set; }
}

public sealed class OnlineRoomMatchmakeResult
{
	public string ProviderId { get; set; } = "";
	public string ProviderDisplayName { get; set; } = "";
	public string Status { get; set; } = "accepted";
	public string Summary { get; set; } = "";
	public bool CreatedNewRoom { get; set; }
	public OnlineRoomDirectoryEntry Room { get; set; } = new();
	public OnlineRoomJoinTicket JoinTicket { get; set; } = new();
}

public interface IOnlineRoomMatchmakeProvider
{
	string Id { get; }
	string DisplayName { get; }
	string BuildLocationSummary();
	OnlineRoomMatchmakeResult Matchmake(AsyncChallengeDefinition challenge, OnlineRoomMatchmakeRequest request);
}
