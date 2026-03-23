using System;
using System.Collections.Generic;

public sealed class HardModeStageOverride
{
	public int Stage { get; }
	public string[] ForcedModifierIds { get; }
	public float EnemyHealthScale { get; }
	public float EnemyDamageScale { get; }
	public int BonusGold { get; }
	public int BonusFood { get; }
	public string MilestoneRelicId { get; }

	public HardModeStageOverride(int stage, string[] forcedModifierIds,
		float enemyHealthScale, float enemyDamageScale,
		int bonusGold, int bonusFood, string milestoneRelicId = "")
	{
		Stage = stage;
		ForcedModifierIds = forcedModifierIds ?? Array.Empty<string>();
		EnemyHealthScale = enemyHealthScale;
		EnemyDamageScale = enemyDamageScale;
		BonusGold = bonusGold;
		BonusFood = bonusFood;
		MilestoneRelicId = milestoneRelicId;
	}
}

public static class HardModeCatalog
{
	private static readonly HardModeStageOverride[] Overrides;
	private static readonly Dictionary<int, HardModeStageOverride> ByStage;

	static HardModeCatalog()
	{
		Overrides = new HardModeStageOverride[50];
		ByStage = new Dictionary<int, HardModeStageOverride>();

		for (var i = 0; i < 50; i++)
		{
			var stage = i + 1;
			var t = i / 49f; // 0.0 to 1.0 across all stages
			var healthScale = 1.3f + t * 1.2f; // 1.3x to 2.5x
			var damageScale = 1.2f + t * 0.8f; // 1.2x to 2.0x
			var bonusGold = (int)(50 + t * 200);
			var bonusFood = (int)(2 + t * 6);

			var modifiers = stage switch
			{
				<= 10 => new[] { "elite_vanguard" },
				<= 20 => new[] { "elite_vanguard", "rapid_assault" },
				<= 30 => new[] { "rapid_assault", "cursed_ground" },
				<= 40 => new[] { "cursed_ground", "fortified_deploy" },
				_ => new[] { "elite_vanguard", "cursed_ground", "fortified_deploy" }
			};

			var milestoneRelic = stage switch
			{
				10 => "relic_hardened_bulwark",
				20 => "relic_hardened_fang",
				30 => "relic_hardened_sigil",
				40 => "relic_hardened_crown",
				50 => "relic_hardened_soul",
				_ => ""
			};

			Overrides[i] = new HardModeStageOverride(stage, modifiers, healthScale, damageScale, bonusGold, bonusFood, milestoneRelic);
			ByStage[stage] = Overrides[i];
		}
	}

	public static HardModeStageOverride GetForStage(int stage)
	{
		return ByStage.TryGetValue(stage, out var o) ? o : null;
	}

	public static IReadOnlyList<HardModeStageOverride> GetAll() => Overrides;
}
