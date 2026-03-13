using System.Collections.Generic;

public sealed class OnlineRoomScoreboardEntry
{
	public int Rank { get; set; }
	public string RoomId { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string PlayerCallsign { get; set; } = "";
	public string PlayerProfileId { get; set; } = "";
	public int Score { get; set; }
	public int StarsEarned { get; set; }
	public int HullPercent { get; set; }
	public float ElapsedSeconds { get; set; }
	public int EnemyDefeats { get; set; }
	public bool Won { get; set; }
	public bool Retreated { get; set; }
	public bool UsedLockedDeck { get; set; }
	public long SubmittedAtUnixSeconds { get; set; }
}

public sealed class OnlineRoomScoreboardSnapshot
{
	public string RoomId { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string ProviderId { get; set; } = "";
	public string ProviderDisplayName { get; set; } = "";
	public string Status { get; set; } = "";
	public string Summary { get; set; } = "";
	public long FetchedAtUnixSeconds { get; set; }
	public List<OnlineRoomScoreboardEntry> Entries { get; set; } = [];
}

public interface IOnlineRoomScoreboardProvider
{
	string Id { get; }
	string DisplayName { get; }
	string BuildLocationSummary();
	OnlineRoomScoreboardSnapshot FetchScoreboard(OnlineRoomJoinTicket ticket, int limit);
}
