using System;
using System.Collections.Generic;

public sealed class TowerFloorDefinition
{
	public int Floor { get; }
	public int BaseStageNumber { get; }
	public float EnemyHealthScale { get; }
	public float EnemyDamageScale { get; }
	public string[] ForcedModifierIds { get; }
	/// <summary>The floor's modifiers with their strengths, added to its campaign stage's in battle.</summary>
	public StageModifierDefinition[] Modifiers { get; }
	public int RewardGold { get; }
	public int RewardFood { get; }
	public int RewardEssence { get; }
	public string MilestoneRelicId { get; }

	public TowerFloorDefinition(int floor, int baseStageNumber,
		float enemyHealthScale, float enemyDamageScale, string[] forcedModifierIds,
		int rewardGold, int rewardFood, int rewardEssence,
		string milestoneRelicId = "")
	{
		Floor = floor;
		BaseStageNumber = baseStageNumber;
		EnemyHealthScale = enemyHealthScale;
		EnemyDamageScale = enemyDamageScale;
		ForcedModifierIds = forcedModifierIds ?? Array.Empty<string>();
		Modifiers = Array.ConvertAll(ForcedModifierIds, id => new StageModifierDefinition { Type = id, Value = ChallengeTowerCatalog.ModifierValue(id) });
		RewardGold = rewardGold;
		RewardFood = rewardFood;
		RewardEssence = rewardEssence;
		MilestoneRelicId = milestoneRelicId;
	}
}

public static class ChallengeTowerCatalog
{
	public const int MaxFloor = 100;

	/// <summary>
	/// Each floor's enemy strength s: health ×(1 + 4s) and damage ×(1 + 2.5s) over its campaign stage, with the floor's
	/// modifiers on. Fitted by CombatReviewSmoke --tower-fit and scripts/analysis/tower_fit.py (2026-11-03): the most
	/// the floor's reference player beats on two seeds, less 0.02. That player has the campaign stage's units at its
	/// level cap, rising to level 20 by floor 100, and no relics, so a floor asks for troops trained past the campaign.
	/// A tower battle lacks the campaign's battlefield help, so floors with heavy modifiers sit below their campaign
	/// stage (s &lt; 0). Rerun the fit when campaign stages, modifiers or troops change.
	/// </summary>
	private static readonly float[] Strength =
	{
		0.000f, 0.010f, 0.020f, 0.030f, 0.040f, 0.051f, 0.061f, 0.071f, 0.081f, 0.068f,
		0.101f, 0.111f, 0.121f, 0.131f, 0.141f, 0.152f, 0.162f, 0.172f, 0.182f, 0.049f,
		0.202f, 0.212f, 0.222f, 0.232f, 0.242f, 0.253f, 0.263f, 0.273f, 0.283f, 0.149f,
		0.303f, 0.313f, 0.323f, 0.333f, -0.004f, 0.354f, 0.364f, 0.374f, 0.384f, -0.100f,
		0.226f, 0.414f, 0.424f, 0.380f, 0.313f, 0.214f, 0.278f, 0.381f, 0.275f, 0.011f,
		0.375f, 0.141f, 0.202f, 0.390f, -0.083f, 0.275f, 0.254f, 0.178f, 0.218f, 0.054f,
		0.141f, 0.298f, 0.244f, 0.427f, 0.212f, 0.226f, 0.261f, 0.213f, 0.238f, -0.078f,
		0.102f, 0.204f, 0.253f, 0.187f, 0.155f, 0.134f, 0.136f, 0.065f, 0.226f, -0.070f,
		0.081f, 0.044f, 0.122f, 0.137f, 0.152f, 0.275f, 0.184f, 0.145f, 0.077f, 0.008f,
		0.179f, 0.138f, 0.212f, 0.508f, -0.070f, 0.115f, 0.147f, 0.087f, 0.104f, -0.100f,
	};

	/// <summary>
	/// Each modifier's strength on a tower floor: the values the campaign authored them with
	/// (scripts/analysis/expand_campaign.py). Elite vanguard and rapid assault do nothing without one.
	/// </summary>
	public static float ModifierValue(string id) => id switch
	{
		"elite_vanguard" => 1.18f, // enemies' health and damage
		"rapid_assault" => .82f, // time between enemy spawns
		"cursed_ground" => 2.5f, // damage a second inside the strip
		"fortified_deploy" => .6f, // damage a freshly deployed troop takes, for 4 s
		_ => 0f
	};

	private static readonly TowerFloorDefinition[] Floors;
	private static readonly Dictionary<int, TowerFloorDefinition> ByFloor;

	static ChallengeTowerCatalog()
	{
		Floors = new TowerFloorDefinition[MaxFloor];
		ByFloor = new Dictionary<int, TowerFloorDefinition>();

		for (var i = 0; i < MaxFloor; i++)
		{
			var floor = i + 1;
			var t = i / 99f; // 0.0 to 1.0
			var baseStage = ((i % Math.Max(1, GameData.MaxStage)) + 1); // cycles through all authored campaign stages
			var healthScale = 1f + 4f * Strength[i];
			var damageScale = 1f + 2.5f * Strength[i];
			// Every tenth floor adds a 200 gold bonus.
			var gold = 50 + (int)(t * 250) + (floor % 10 == 0 ? 200 : 0);
			var food = 1 + (int)(t * 5);
			var essence = (floor % 25 == 0) ? 3 : 0;

			var modifiers = floor switch
			{
				<= 20 => Array.Empty<string>(),
				<= 40 => new[] { "elite_vanguard" },
				<= 60 => new[] { "elite_vanguard", "rapid_assault" },
				<= 80 => new[] { "rapid_assault", "cursed_ground" },
				_ => new[] { "elite_vanguard", "cursed_ground", "fortified_deploy" }
			};

			var milestoneRelic = floor switch
			{
				25 => "relic_tower_sentinel",
				50 => "relic_tower_ascendant",
				75 => "relic_tower_apex",
				100 => "relic_tower_pinnacle",
				_ => ""
			};

			Floors[i] = new TowerFloorDefinition(floor, baseStage, healthScale, damageScale, modifiers,
				gold, food, essence, milestoneRelic);
			ByFloor[floor] = Floors[i];
		}
	}

	public static TowerFloorDefinition GetFloor(int floor)
	{
		return ByFloor.TryGetValue(floor, out var f) ? f : null;
	}

	public static IReadOnlyList<TowerFloorDefinition> GetAll() => Floors;
}
