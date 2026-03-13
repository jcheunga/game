using System.Collections.Generic;

public sealed class ChallengeBoardFeedItem
{
	public string Id { get; set; } = "";
	public string Title { get; set; } = "";
	public string Summary { get; set; } = "";
	public string Code { get; set; } = "";
	public string[] LockedDeckUnitIds { get; set; } = [];
}

public sealed class ChallengeBoardFeedSnapshot
{
	public string ProviderId { get; set; } = "";
	public string ProviderDisplayName { get; set; } = "";
	public string Status { get; set; } = "";
	public string Summary { get; set; } = "";
	public long FetchedAtUnixSeconds { get; set; }
	public List<ChallengeBoardFeedItem> Items { get; set; } = [];
}

public interface IChallengeBoardFeedProvider
{
	string Id { get; }
	string DisplayName { get; }
	string BuildLocationSummary();
	ChallengeBoardFeedSnapshot FetchFeed(int highestUnlockedStage, int maxStage, int limit);
}
