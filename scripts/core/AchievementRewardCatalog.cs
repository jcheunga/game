using System;
using System.Collections.Generic;

public sealed class AchievementReward
{
	public string AchievementId { get; }
	public string RewardType { get; } // "gold", "food", "tomes", "essence", "sigils", "relic"
	public string RewardItemId { get; }
	public int RewardAmount { get; }
	public string RewardLabel { get; }

	public AchievementReward(string achievementId, string rewardType, int rewardAmount, string rewardLabel, string rewardItemId = "")
	{
		AchievementId = achievementId;
		RewardType = rewardType;
		RewardAmount = rewardAmount;
		RewardLabel = rewardLabel;
		RewardItemId = rewardItemId;
	}
}

public static class AchievementRewardCatalog
{
	private static readonly AchievementReward[] Rewards =
	{
		// Campaign
		new("first_blood", "gold", 100, "+100 gold"),
		new("district_clear", "gold", 300, "+300 gold"),
		new("campaign_complete", "sigils", 5, "+5 sigils"),
		new("all_stars", "tomes", 5, "+5 tomes"),
		new("heroic_clear", "essence", 5, "+5 essence"),

		// Combat
		new("boss_slayer", "gold", 200, "+200 gold"),
		new("boss_hunter", "essence", 8, "+8 essence"),
		new("no_damage", "food", 10, "+10 food"),
		new("speed_clear", "tomes", 3, "+3 tomes"),
		new("combo_master", "sigils", 3, "+3 sigils"),

		// Endless
		new("endless_30", "gold", 400, "+400 gold"),
		new("endless_60", "tomes", 4, "+4 tomes"),
		new("endless_90", "essence", 10, "+10 essence"),
		new("endless_boss", "sigils", 4, "+4 sigils"),

		// Collection
		new("relic_collector", "gold", 250, "+250 gold"),
		new("full_armory", "essence", 6, "+6 essence"),
		new("full_roster", "tomes", 5, "+5 tomes"),
		new("first_forge", "gold", 150, "+150 gold"),
		new("codex_10", "food", 8, "+8 food"),
		new("codex_complete", "essence", 12, "+12 essence"),
		new("first_enchantment", "gold", 200, "+200 gold"),

		// Mastery
		new("max_unit", "tomes", 3, "+3 tomes"),
		new("all_spells", "gold", 300, "+300 gold"),
		new("daily_streak", "food", 12, "+12 food"),
		new("first_promotion", "sigils", 2, "+2 sigils"),
		new("expedition_10", "gold", 500, "+500 gold"),
		new("event_complete", "essence", 8, "+8 essence"),

		// Social/Competitive
		new("first_talent", "gold", 150, "+150 gold"),
		new("talent_master", "tomes", 6, "+6 tomes"),
		new("arena_first_win", "gold", 200, "+200 gold"),
		new("arena_10_wins", "essence", 6, "+6 essence"),
		new("arena_gold_tier", "sigils", 5, "+5 sigils"),
		new("guild_join", "gold", 100, "+100 gold"),
		new("guild_contributor", "tomes", 4, "+4 tomes"),

		// Endgame
		new("hard_mode_10", "essence", 5, "+5 essence"),
		new("hard_mode_complete", "sigils", 8, "+8 sigils"),
		new("raid_contributor", "gold", 300, "+300 gold"),

		// Engagement
		new("bounty_streak_7", "tomes", 3, "+3 tomes"),
		new("tower_25", "gold", 400, "+400 gold"),
		new("tower_50", "essence", 6, "+6 essence"),
		new("tower_100", "sigils", 10, "+10 sigils"),
		new("first_mastery", "gold", 200, "+200 gold"),
		new("grand_master", "essence", 10, "+10 essence"),
		new("gift_sent", "food", 5, "+5 food"),
		new("first_skin", "gold", 200, "+200 gold"),
		new("first_awakening", "tomes", 3, "+3 tomes"),
		new("collector_complete", "sigils", 10, "+10 sigils"),
		new("mutator_5", "essence", 5, "+5 essence"),
	};

	private static readonly Dictionary<string, AchievementReward> ById;

	static AchievementRewardCatalog()
	{
		ById = new Dictionary<string, AchievementReward>(StringComparer.OrdinalIgnoreCase);
		foreach (var r in Rewards)
		{
			ById[r.AchievementId] = r;
		}
	}

	public static AchievementReward GetForAchievement(string achievementId)
	{
		return ById.TryGetValue(achievementId, out var r) ? r : null;
	}

	public static IReadOnlyList<AchievementReward> GetAll() => Rewards;
}
