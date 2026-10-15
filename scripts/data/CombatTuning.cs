using System;

public sealed class CombatTuning
{
	public float PlayerBaseX { get; set; } = 48f;
	public float EnemyBaseX { get; set; } = 1152f;
	public float PlayerSpawnX { get; set; } = 70f;
	public float EnemySpawnX { get; set; } = 1130f;

	public float BattlefieldLeft { get; set; } = 42f;
	public float BattlefieldRight { get; set; } = 1158f;
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
	public float StructureScale { get; set; } = 0.58f;
	// World units visible across the screen: sets the soldier scale (about 14% of screen height).
	public float ViewWidth { get; set; } = 600f;

	// Ranged units strike in melee instead of shooting when an enemy is this close (centre to centre,
	// plus how far either body extends past a regular soldier's), at this fraction of their damage.
	public float RangedMeleeReach { get; set; } = 34f;
	public float RangedMeleeDamageScale { get; set; } = 0.8f;

	public float CourageStart { get; set; } = 0f;
	public float CourageMax { get; set; } = 100f;
	public float CourageGainPerSecond { get; set; } = 3f;
	public float CourageDropChance { get; set; } = 0.10f;
	public float CourageDropAmount { get; set; } = 20f;
	public float CourageDropLifetime { get; set; } = 8f;
	public float CourageDropFadeDuration { get; set; } = 2f;
	public int CourageDropGuaranteeAfterMisses { get; set; } = 15;

	// Mana pays for magic, separately from the courage that pays for troops. It is earned from kills, not time.
	public float ManaStart { get; set; } = 0f;
	public float ManaMax { get; set; } = 100f;
	public float ManaPerEnemyKill { get; set; } = 7f;
	// The war wagon's own spell, which opens beside the card dock once the mana bar is full.
	public CaravanSpellTuning CaravanSpell { get; set; } = new();

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
		CaravanSpell ??= new CaravanSpellTuning();
		CaravanSpell.Normalize();
		CourageDropChance = Math.Clamp(CourageDropChance, 0f, 1f);
		CourageDropAmount = Math.Max(0f, CourageDropAmount);
		CourageDropLifetime = Math.Max(0.1f, CourageDropLifetime);
		CourageDropFadeDuration = Math.Clamp(CourageDropFadeDuration, 0f, CourageDropLifetime);
		CourageDropGuaranteeAfterMisses = Math.Max(0, CourageDropGuaranteeAfterMisses);

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

/// <summary>
/// The caravan spell: the war wagon's lantern looses arcane missiles at every enemy on the field. It is not a
/// loadout card; it opens beside the card dock when the player holds <see cref="ManaCost"/> mana.
/// </summary>
public sealed class CaravanSpellTuning
{
	public string Id { get; set; } = "caravan_arcane_barrage";
	public string DisplayName { get; set; } = "Arcane Barrage";
	public string Description { get; set; } = "";
	public float ManaCost { get; set; } = 100f;
	public float Cooldown { get; set; } = 60f;
	// Seconds the lantern keeps firing.
	public float Duration { get; set; } = 3f;
	public float MissilesPerSecond { get; set; } = 30f;
	public float MissileDamage { get; set; } = 14f;
	// Hits one enemy can take from a single barrage; missiles past every enemy's cap burst on open ground.
	public int MaxHitsPerEnemy { get; set; } = 10;

	public void Normalize()
	{
		if (string.IsNullOrWhiteSpace(Id)) Id = "caravan_arcane_barrage";
		if (string.IsNullOrWhiteSpace(DisplayName)) DisplayName = "Arcane Barrage";
		ManaCost = Math.Max(1f, ManaCost);
		Cooldown = Math.Max(0f, Cooldown);
		Duration = Math.Max(0.1f, Duration);
		MissilesPerSecond = Math.Max(1f, MissilesPerSecond);
		MissileDamage = Math.Max(0f, MissileDamage);
		MaxHitsPerEnemy = Math.Max(1, MaxHitsPerEnemy);
	}
}
