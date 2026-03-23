using System;
using System.Collections.Generic;

public sealed class CollectionMilestone
{
	public string Id { get; }
	public string Category { get; }
	public string CategoryLabel { get; }
	public int ThresholdPercent { get; }
	public string RewardType { get; }
	public int RewardAmount { get; }
	public string RewardLabel { get; }

	public CollectionMilestone(string id, string category, string categoryLabel,
		int thresholdPercent, string rewardType, int rewardAmount, string rewardLabel)
	{
		Id = id;
		Category = category;
		CategoryLabel = categoryLabel;
		ThresholdPercent = thresholdPercent;
		RewardType = rewardType;
		RewardAmount = rewardAmount;
		RewardLabel = rewardLabel;
	}
}

public static class CollectionMilestoneCatalog
{
	private static readonly CollectionMilestone[] Milestones =
	{
		// Codex (72 entries)
		new("codex_25", "codex", "Codex", 25, "gold", 300, "+300 Gold"),
		new("codex_50", "codex", "Codex", 50, "tomes", 3, "+3 Tomes"),
		new("codex_75", "codex", "Codex", 75, "essence", 5, "+5 Essence"),
		new("codex_100", "codex", "Codex", 100, "sigils", 5, "+5 Sigils"),

		// Relics (33)
		new("relics_25", "relics", "Relics", 25, "gold", 400, "+400 Gold"),
		new("relics_50", "relics", "Relics", 50, "essence", 4, "+4 Essence"),
		new("relics_75", "relics", "Relics", 75, "sigils", 4, "+4 Sigils"),
		new("relics_100", "relics", "Relics", 100, "essence", 10, "+10 Essence"),

		// Units (16)
		new("units_25", "units", "Units", 25, "gold", 200, "+200 Gold"),
		new("units_50", "units", "Units", 50, "tomes", 2, "+2 Tomes"),
		new("units_75", "units", "Units", 75, "food", 10, "+10 Food"),
		new("units_100", "units", "Units", 100, "sigils", 3, "+3 Sigils"),

		// Spells (10)
		new("spells_25", "spells", "Spells", 25, "gold", 150, "+150 Gold"),
		new("spells_50", "spells", "Spells", 50, "tomes", 2, "+2 Tomes"),
		new("spells_75", "spells", "Spells", 75, "essence", 3, "+3 Essence"),
		new("spells_100", "spells", "Spells", 100, "sigils", 2, "+2 Sigils"),

		// Achievements (45)
		new("achieve_25", "achievements", "Achievements", 25, "gold", 500, "+500 Gold"),
		new("achieve_50", "achievements", "Achievements", 50, "tomes", 4, "+4 Tomes"),
		new("achieve_75", "achievements", "Achievements", 75, "essence", 6, "+6 Essence"),
		new("achieve_100", "achievements", "Achievements", 100, "sigils", 8, "+8 Sigils"),

		// Tower (100 floors)
		new("tower_25p", "tower", "Tower Floors", 25, "gold", 400, "+400 Gold"),
		new("tower_50p", "tower", "Tower Floors", 50, "essence", 5, "+5 Essence"),
		new("tower_75p", "tower", "Tower Floors", 75, "sigils", 5, "+5 Sigils"),
		new("tower_100p", "tower", "Tower Floors", 100, "essence", 12, "+12 Essence"),
	};

	private static readonly Dictionary<string, CollectionMilestone> ById;

	static CollectionMilestoneCatalog()
	{
		ById = new Dictionary<string, CollectionMilestone>(StringComparer.OrdinalIgnoreCase);
		foreach (var m in Milestones)
		{
			ById[m.Id] = m;
		}
	}

	public static IReadOnlyList<CollectionMilestone> GetAll() => Milestones;

	public static CollectionMilestone GetById(string id)
	{
		return ById.TryGetValue(id, out var m) ? m : null;
	}

	public static int GetCollectionPercent(string category, GameState gs)
	{
		if (gs == null) return 0;

		return category switch
		{
			"codex" => CodexCatalog.TotalEntries > 0 ? gs.DiscoveredCodexCount * 100 / CodexCatalog.TotalEntries : 0,
			"relics" => gs.OwnedRelicCount * 100 / Math.Max(1, GameData.GetAllEquipment().Count),
			"units" => gs.OwnedUnitCount * 100 / Math.Max(1, 16),
			"spells" => gs.OwnedSpellCount * 100 / Math.Max(1, 10),
			"achievements" => gs.AchievementUnlockedCount * 100 / Math.Max(1, AchievementCatalog.GetAll().Count),
			"tower" => gs.TowerHighestFloor * 100 / ChallengeTowerCatalog.MaxFloor,
			_ => 0
		};
	}
}
