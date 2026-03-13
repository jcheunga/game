using System.Collections.Generic;

public sealed class ChallengeLeaderboardEntry
{
	public int Rank { get; set; }
	public string Code { get; set; } = "";
	public string PlayerCallsign { get; set; } = "";
	public string PlayerProfileId { get; set; } = "";
	public int Score { get; set; }
	public int StarsEarned { get; set; }
	public int HullPercent { get; set; }
	public float ElapsedSeconds { get; set; }
	public bool UsedLockedDeck { get; set; }
	public long PlayedAtUnixSeconds { get; set; }
}

public sealed class ChallengeLeaderboardSnapshot
{
	public string Code { get; set; } = "";
	public string ProviderId { get; set; } = "";
	public string ProviderDisplayName { get; set; } = "";
	public string Status { get; set; } = "";
	public string Summary { get; set; } = "";
	public long FetchedAtUnixSeconds { get; set; }
	public List<ChallengeLeaderboardEntry> Entries { get; set; } = [];
}

public sealed class ChallengeLeaderboardApiResponse
{
	public string Code { get; set; } = "";
	public string Status { get; set; } = "ok";
	public string Message { get; set; } = "";
	public ChallengeLeaderboardEntry[] Entries { get; set; } = [];
}

public interface IChallengeLeaderboardProvider
{
	string Id { get; }
	string DisplayName { get; }
	string BuildLocationSummary();
	ChallengeLeaderboardSnapshot FetchLeaderboard(string code, int limit);
}
