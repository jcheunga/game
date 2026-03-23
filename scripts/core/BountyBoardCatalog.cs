using System;
using System.Collections.Generic;

public sealed class BountyDefinition
{
	public string Id { get; }
	public string Title { get; }
	public string Description { get; }
	public string TrackingType { get; } // "enemy_defeats", "unit_deploys", "spell_casts", "stages_cleared", "gold_earned", "arena_wins"
	public int TargetCount { get; }
	public string RewardType { get; } // "gold", "food", "tomes", "essence"
	public int RewardAmount { get; }

	public BountyDefinition(string id, string title, string description,
		string trackingType, int targetCount, string rewardType, int rewardAmount)
	{
		Id = id;
		Title = title;
		Description = description;
		TrackingType = trackingType;
		TargetCount = targetCount;
		RewardType = rewardType;
		RewardAmount = rewardAmount;
	}
}

public static class BountyBoardCatalog
{
	public const int DailyBountyCount = 3;

	private static readonly BountyDefinition[] Templates =
	{
		new("bounty_kill_20", "Rotbound Purge", "Defeat 20 enemies in battle.", "enemy_defeats", 20, "gold", 150),
		new("bounty_kill_50", "Mass Extermination", "Defeat 50 enemies in battle.", "enemy_defeats", 50, "gold", 350),
		new("bounty_deploy_10", "Muster the Troops", "Deploy 10 units in battle.", "unit_deploys", 10, "food", 4),
		new("bounty_deploy_25", "Full Mobilization", "Deploy 25 units in battle.", "unit_deploys", 25, "food", 8),
		new("bounty_spells_5", "Arcane Practice", "Cast 5 spells in battle.", "spell_casts", 5, "tomes", 1),
		new("bounty_spells_12", "Spell Barrage", "Cast 12 spells in battle.", "spell_casts", 12, "tomes", 2),
		new("bounty_stages_2", "Campaign Push", "Clear 2 campaign stages.", "stages_cleared", 2, "gold", 200),
		new("bounty_stages_5", "March Forward", "Clear 5 campaign stages.", "stages_cleared", 5, "gold", 500),
		new("bounty_gold_500", "Fortune Seeker", "Earn 500 gold from any source.", "gold_earned", 500, "essence", 2),
		new("bounty_gold_1000", "Treasure Hunter", "Earn 1000 gold from any source.", "gold_earned", 1000, "essence", 4),
		new("bounty_arena_1", "Arena Challenger", "Win 1 arena battle.", "arena_wins", 1, "essence", 3),
		new("bounty_arena_3", "Arena Gladiator", "Win 3 arena battles.", "arena_wins", 3, "essence", 6),
		new("bounty_endless_15", "Endurance Test", "Reach wave 15 in endless mode.", "endless_wave", 15, "tomes", 2),
		new("bounty_boss_1", "Boss Hunter", "Defeat 1 boss enemy.", "boss_kills", 1, "sigils", 1),
		new("bounty_expedition_1", "Expedition Dispatch", "Complete 1 expedition.", "expeditions", 1, "gold", 200),
	};

	private static readonly Dictionary<string, BountyDefinition> ById;

	static BountyBoardCatalog()
	{
		ById = new Dictionary<string, BountyDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (var t in Templates)
		{
			ById[t.Id] = t;
		}
	}

	public static BountyDefinition GetById(string id)
	{
		return ById.TryGetValue(id, out var d) ? d : null;
	}

	public static BountyDefinition[] GetDailyBounties(DateTime date)
	{
		var seed = date.Year * 10000 + date.Month * 100 + date.Day;
		var rng = new Random(seed);
		var selected = new List<BountyDefinition>();
		var used = new HashSet<int>();

		while (selected.Count < DailyBountyCount && used.Count < Templates.Length)
		{
			var idx = rng.Next(Templates.Length);
			if (used.Add(idx))
			{
				selected.Add(Templates[idx]);
			}
		}

		return selected.ToArray();
	}

	public static string GetDateKey() => DateTime.UtcNow.ToString("yyyy-MM-dd");
}
