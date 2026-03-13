public sealed class OnlineRoomResultRequest
{
	public string RoomId { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string TicketId { get; set; } = "";
	public string JoinToken { get; set; } = "";
	public string PlayerProfileId { get; set; } = "";
	public string PlayerCallsign { get; set; } = "";
	public int Score { get; set; }
	public int StarsEarned { get; set; }
	public int HullPercent { get; set; }
	public float ElapsedSeconds { get; set; }
	public int EnemyDefeats { get; set; }
	public bool Won { get; set; }
	public bool Retreated { get; set; }
	public bool UsedLockedDeck { get; set; }
	public long RequestedAtUnixSeconds { get; set; }
}

public sealed class OnlineRoomResultSubmission
{
	public string ProviderId { get; set; } = "";
	public string ProviderDisplayName { get; set; } = "";
	public string RoomId { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string TicketId { get; set; } = "";
	public string Status { get; set; } = "accepted";
	public string Summary { get; set; } = "";
	public int Score { get; set; }
	public int ProvisionalRank { get; set; }
	public long SubmittedAtUnixSeconds { get; set; }
}

public interface IOnlineRoomResultProvider
{
	string Id { get; }
	string DisplayName { get; }
	string BuildLocationSummary();
	OnlineRoomResultSubmission SubmitResult(OnlineRoomJoinTicket ticket, OnlineRoomResultRequest request);
}
