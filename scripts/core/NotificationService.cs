using System;

public static class NotificationService
{
	public static int GetPendingCount()
	{
		var gs = GameState.Instance;
		if (gs == null) return 0;

		var count = 0;

		// Unclaimed achievement rewards
		count += gs.GetUnclaimedAchievementRewardCount();

		// Completed expeditions
		var expeditions = gs.GetActiveExpeditions();
		for (var i = 0; i < expeditions.Count; i++)
		{
			if (gs.IsExpeditionComplete(i)) count++;
		}

		// Claimable bounties
		var bounties = BountyBoardCatalog.GetDailyBounties(DateTime.UtcNow);
		foreach (var b in bounties)
		{
			if (!gs.IsBountyCompleted(b.Id) && gs.GetBountyProgress(b.Id) >= b.TargetCount) count++;
		}

		// Login calendar
		if (gs.CanClaimLoginReward()) count++;

		// Active seasonal event
		if (gs.GetActiveEvent() != null) count++;

		return count;
	}

	public static int GetBountyClaimableCount()
	{
		var gs = GameState.Instance;
		if (gs == null) return 0;
		var count = 0;
		var bounties = BountyBoardCatalog.GetDailyBounties(DateTime.UtcNow);
		foreach (var b in bounties)
		{
			if (!gs.IsBountyCompleted(b.Id) && gs.GetBountyProgress(b.Id) >= b.TargetCount) count++;
		}
		return count;
	}

	public static int GetExpeditionCompleteCount()
	{
		var gs = GameState.Instance;
		if (gs == null) return 0;
		var count = 0;
		var expeditions = gs.GetActiveExpeditions();
		for (var i = 0; i < expeditions.Count; i++)
		{
			if (gs.IsExpeditionComplete(i)) count++;
		}
		return count;
	}

	public static string BuildSummary()
	{
		var total = GetPendingCount();
		if (total == 0) return "";
		return $"You have {total} unclaimed reward{(total > 1 ? "s" : "")}!";
	}
}
