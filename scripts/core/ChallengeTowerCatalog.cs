using System;
using System.Collections.Generic;

public sealed class TowerFloorDefinition
{
	public int Floor { get; }
	public int BaseStageNumber { get; }
	public float EnemyHealthScale { get; }
	public float EnemyDamageScale { get; }
	public string[] ForcedModifierIds { get; }
	public int RewardGold { get; }
	public int RewardFood { get; }
	public int RewardTomes { get; }
	public int RewardEssence { get; }
	public string MilestoneRelicId { get; }

	public TowerFloorDefinition(int floor, int baseStageNumber,
		float enemyHealthScale, float enemyDamageScale, string[] forcedModifierIds,
		int rewardGold, int rewardFood, int rewardTomes, int rewardEssence,
		string milestoneRelicId = "")
	{
		Floor = floor;
		BaseStageNumber = baseStageNumber;
		EnemyHealthScale = enemyHealthScale;
		EnemyDamageScale = enemyDamageScale;
		ForcedModifierIds = forcedModifierIds ?? Array.Empty<string>();
		RewardGold = rewardGold;
		RewardFood = rewardFood;
		RewardTomes = rewardTomes;
		RewardEssence = rewardEssence;
		MilestoneRelicId = milestoneRelicId;
	}
}

public static class ChallengeTowerCatalog
{
	public const int MaxFloor = 100;
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
			var baseStage = ((i % 50) + 1); // cycles through 1-50
			var healthScale = 1.0f + t * 4.0f; // 1.0x to 5.0x
			var damageScale = 1.0f + t * 2.5f; // 1.0x to 3.5x
			var gold = 50 + (int)(t * 250);
			var food = 1 + (int)(t * 5);
			var tomes = (floor % 10 == 0) ? 2 : 0;
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
				gold, food, tomes, essence, milestoneRelic);
			ByFloor[floor] = Floors[i];
		}
	}

	public static TowerFloorDefinition GetFloor(int floor)
	{
		return ByFloor.TryGetValue(floor, out var f) ? f : null;
	}

	public static IReadOnlyList<TowerFloorDefinition> GetAll() => Floors;
}
