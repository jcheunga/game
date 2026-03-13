using System;
using System.Linq;

public sealed class LocalChallengeBoardFeedProvider : IChallengeBoardFeedProvider
{
	public string Id => ChallengeSyncProviderCatalog.LocalJournalId;
	public string DisplayName => "Local Daily Feed";

	public string BuildLocationSummary()
	{
		return $"Source: local daily rotation ({FeaturedChallengeCatalog.GetDailyRotationStamp()})";
	}

	public ChallengeBoardFeedSnapshot FetchFeed(int highestUnlockedStage, int maxStage, int limit)
	{
		var items = FeaturedChallengeCatalog.GetDailyRotation(highestUnlockedStage, maxStage)
			.Take(Math.Max(1, limit))
			.Select(featured => new ChallengeBoardFeedItem
			{
				Id = featured.Id,
				Title = featured.Title,
				Summary = featured.Summary,
				Code = featured.Challenge.Code,
				LockedDeckUnitIds = featured.LockedDeckUnitIds.ToArray()
			})
			.ToList();

		return new ChallengeBoardFeedSnapshot
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			Status = items.Count == 0 ? "empty" : "ok",
			Summary = items.Count == 0
				? "No local featured boards resolved."
				: $"Loaded {items.Count} featured board entr{(items.Count == 1 ? "y" : "ies")} from the local rotation.",
			FetchedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			Items = items
		};
	}
}
