using System;

public sealed class CombatTuning
{
	public float PlayerBaseX { get; set; } = 48f;
	public float EnemyBaseX { get; set; } = 900f;
	public float PlayerSpawnX { get; set; } = 70f;
	public float EnemySpawnX { get; set; } = 878f;

	public float BattlefieldLeft { get; set; } = 42f;
	public float BattlefieldRight { get; set; } = 906f;
	// About two screens from wagon to stronghold at Dead Ahead-like proportions.
	// A shallow band: a unit marching out of the wagon's centre line can reach an enemy on either
	// edge, but a unit pulled to one edge cannot see the other. Every unit's AggroRangeY sits in
	// [LaneHalfHeight, 2 * LaneHalfHeight); the data validator enforces it.
	public float BattlefieldTop { get; set; } = 313f;
	public float BattlefieldBottom { get; set; } = 367f;
	public float SpawnVerticalPadding { get; set; } = 12f;
	public float LaneHalfHeight => (BattlefieldBottom - BattlefieldTop) * 0.5f - SpawnVerticalPadding;

	public float BaseCoreRadius { get; set; } = 22f;
	public float BaseApproachDistance { get; set; } = 40f;
	// The wagon and stronghold plates are drawn at this fraction of their authored width, about two
	// soldiers tall.
	public float StructureScale { get; set; } = 0.51f;
	// World units visible across the screen: sets the soldier scale (about 14% of screen height).
	public float ViewWidth { get; set; } = 474f;

	public float CourageStart { get; set; } = 0f;
	public float CourageMax { get; set; } = 100f;
	public float CourageGainPerSecond { get; set; } = 3f;

	public float InitialEnemySpawnDelay { get; set; } = 2.8f;
	public float EnemySpawnPressureTimeScale { get; set; } = 180f;
	public float EnemySpawnPressureMin { get; set; } = 1f;
	public float EnemySpawnPressureMax { get; set; } = 1.25f;
	public float EnemySpawnIntervalFloor { get; set; } = 1.4f;

	public int[] MaxActiveEnemiesByStage { get; set; } = { 5, 6, 6, 7, 7, 7, 8, 8, 8, 8, 9, 9, 9, 10, 10, 10, 10, 10, 10, 10, 11, 11, 11, 11, 11, 11, 11, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 13 };
	public int VictoryFoodReward { get; set; } = 2;
	public int VictoryFuelReward { get => VictoryFoodReward; set => VictoryFoodReward = value; }

	public int GetMaxActiveEnemies(int stage)
	{
		if (MaxActiveEnemiesByStage == null || MaxActiveEnemiesByStage.Length == 0)
		{
			return 10;
		}

		var index = Math.Clamp(stage - 1, 0, MaxActiveEnemiesByStage.Length - 1);
		return Math.Max(1, MaxActiveEnemiesByStage[index]);
	}

	public void Normalize()
	{
		if (BattlefieldRight <= BattlefieldLeft)
		{
			BattlefieldRight = BattlefieldLeft + 100f;
		}

		if (BattlefieldBottom <= BattlefieldTop)
		{
			BattlefieldBottom = BattlefieldTop + 100f;
		}

		if (ViewWidth <= 0f)
		{
			ViewWidth = BattlefieldRight + BattlefieldLeft;
		}

		if (StructureScale <= 0f)
		{
			StructureScale = 1f;
		}

		if (SpawnVerticalPadding < 0f)
		{
			SpawnVerticalPadding = 0f;
		}

		if (EnemySpawnPressureTimeScale <= 0f)
		{
			EnemySpawnPressureTimeScale = 1f;
		}

		if (EnemySpawnPressureMax < EnemySpawnPressureMin)
		{
			EnemySpawnPressureMax = EnemySpawnPressureMin;
		}

		if (EnemySpawnIntervalFloor <= 0f)
		{
			EnemySpawnIntervalFloor = 0.1f;
		}
	}
}
