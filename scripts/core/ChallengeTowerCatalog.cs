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
		Modifiers = StageModifiers.Authored(ForcedModifierIds);
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
	/// stage (s &lt; 0). Rerun the fit when campaign stages, modifiers or troops change. Floors 1-70 were refitted on
	/// 9 November 2026 when their campaign stages got harder; floors 71-100 sit on stages that kept their strength.
	/// </summary>
	private static readonly float[] Strength =
	{
		0.000f, 0.010f, 0.020f, 0.030f, 0.040f, -0.016f, 0.061f, 0.071f, 0.081f, -0.097f,
		0.046f, 0.079f, 0.010f, -0.018f, 0.015f, 0.044f, 0.043f, 0.066f, 0.006f, -0.114f,
		-0.034f, 0.007f, -0.101f, -0.043f, -0.001f, -0.070f, -0.158f, -0.092f, 0.055f, -0.070f,
		-0.020f, 0.019f, -0.020f, -0.068f, -0.020f, 0.068f, -0.051f, -0.078f, -0.040f, -0.064f,
		-0.001f, -0.022f, -0.042f, -0.036f, -0.020f, -0.020f, -0.081f, -0.045f, 0.003f, -0.020f,
		-0.020f, -0.039f, -0.037f, -0.039f, -0.108f, -0.079f, -0.065f, -0.139f, -0.051f, -0.126f,
		-0.001f, 0.047f, -0.087f, -0.075f, -0.025f, -0.058f, -0.114f, -0.120f, -0.050f, -0.009f,
		0.035f, 0.014f, -0.089f, 0.003f, 0.073f, 0.027f, -0.045f, -0.089f, -0.089f, 0.030f,
		0.056f, -0.026f, -0.020f, -0.095f, -0.047f, -0.020f, -0.114f, -0.076f, -0.043f, -0.101f,
		-0.006f, -0.006f, -0.020f, -0.047f, -0.120f, -0.083f, -0.087f, -0.076f, -0.045f, -0.089f,
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
