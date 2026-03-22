using System;
using System.Collections.Generic;

public sealed class BossRushWave
{
	public string BossUnitId { get; set; } = "";
	public string BossName { get; set; } = "";
	public string DistrictName { get; set; } = "";
	public string TerrainId { get; set; } = "urban";
	public string[] EscortUnitIds { get; set; } = Array.Empty<string>();
	public float HealthScale { get; set; } = 1f;
	public float DamageScale { get; set; } = 1f;
	public int RewardGold { get; set; }
}

public sealed class BossRushBuff
{
	public string Id { get; set; } = "";
	public string Title { get; set; } = "";
	public string Description { get; set; } = "";
	public float UnitHealthScale { get; set; } = 1f;
	public float UnitDamageScale { get; set; } = 1f;
	public float CourageGainScale { get; set; } = 1f;
	public int BonusGold { get; set; }
}

public static class BossRushCatalog
{
	public static readonly BossRushWave[] Waves =
	{
		new()
		{
			BossUnitId = GameData.EnemyBossId, BossName = "Grave Lord", DistrictName = "King's Road",
			TerrainId = "urban", EscortUnitIds = new[] { GameData.EnemyWalkerId, GameData.EnemyWalkerId, GameData.EnemyBruteId },
			HealthScale = 1.2f, DamageScale = 1.1f, RewardGold = 300
		},
		new()
		{
			BossUnitId = GameData.EnemyBossDocksId, BossName = "Tidecaller", DistrictName = "Saltwake Docks",
			TerrainId = "industrial", EscortUnitIds = new[] { GameData.EnemySpitterId, GameData.EnemyRunnerId, GameData.EnemyHowlerId },
			HealthScale = 1.5f, DamageScale = 1.3f, RewardGold = 400
		},
		new()
		{
			BossUnitId = GameData.EnemyBossForgeId, BossName = "Iron Warden", DistrictName = "Emberforge March",
			TerrainId = "foundry", EscortUnitIds = new[] { GameData.EnemySaboteurId, GameData.EnemyBruteId, GameData.EnemySplitterId },
			HealthScale = 1.8f, DamageScale = 1.5f, RewardGold = 500
		},
		new()
		{
			BossUnitId = GameData.EnemyBossWardId, BossName = "Plague Archon", DistrictName = "Ashen Ward",
			TerrainId = "checkpoint", EscortUnitIds = new[] { GameData.EnemyJammerId, GameData.EnemySpitterId, GameData.EnemyHowlerId },
			HealthScale = 2.1f, DamageScale = 1.7f, RewardGold = 600
		},
		new()
		{
			BossUnitId = GameData.EnemyBossPassId, BossName = "Thornwall Chieftain", DistrictName = "Thornwall Pass",
			TerrainId = "pass", EscortUnitIds = new[] { GameData.EnemyRunnerId, GameData.EnemySaboteurId, GameData.EnemyRunnerId },
			HealthScale = 2.4f, DamageScale = 1.9f, RewardGold = 700
		},
		new()
		{
			BossUnitId = GameData.EnemyBossBasilicaId, BossName = "Bone Pontiff", DistrictName = "Hollow Basilica",
			TerrainId = "cathedral", EscortUnitIds = new[] { GameData.EnemyLichId, GameData.EnemySplitterId, GameData.EnemyCrusherId },
			HealthScale = 2.8f, DamageScale = 2.1f, RewardGold = 850
		},
		new()
		{
			BossUnitId = GameData.EnemyBossMireId, BossName = "Mire Behemoth", DistrictName = "Mire of Saints",
			TerrainId = "swamp", EscortUnitIds = new[] { GameData.EnemyBloaterId, GameData.EnemyBloaterId, GameData.EnemySpitterId, GameData.EnemySplitterId },
			HealthScale = 3.2f, DamageScale = 2.3f, RewardGold = 1000
		},
		new()
		{
			BossUnitId = GameData.EnemyBossSteppeId, BossName = "Steppe Warlord", DistrictName = "Sunfall Steppe",
			TerrainId = "grassland", EscortUnitIds = new[] { GameData.EnemyRunnerId, GameData.EnemyShieldWallId, GameData.EnemySaboteurId, GameData.EnemyHowlerId },
			HealthScale = 3.6f, DamageScale = 2.6f, RewardGold = 1200
		},
		new()
		{
			BossUnitId = GameData.EnemyBossVergeId, BossName = "Gloamwood Witch", DistrictName = "Gloamwood Verge",
			TerrainId = "grove", EscortUnitIds = new[] { GameData.EnemyTunnelerId, GameData.EnemyJammerId, GameData.EnemyMirrorId, GameData.EnemyRunnerId },
			HealthScale = 4.0f, DamageScale = 2.9f, RewardGold = 1400
		},
		new()
		{
			BossUnitId = GameData.EnemyBossCitadelId, BossName = "Dread Sovereign", DistrictName = "Crownfall Citadel",
			TerrainId = "innerkeep", EscortUnitIds = new[] { GameData.EnemyCrusherId, GameData.EnemyLichId, GameData.EnemyShieldWallId, GameData.EnemySiegeTowerId, GameData.EnemyHowlerId },
			HealthScale = 5.0f, DamageScale = 3.5f, RewardGold = 2000
		},
	};

	public static readonly BossRushBuff[] Buffs =
	{
		new() { Id = "rush_fortify", Title = "Fortify", Description = "+15% unit health for the rest of the rush.", UnitHealthScale = 1.15f },
		new() { Id = "rush_sharpen", Title = "Sharpen", Description = "+12% unit damage for the rest of the rush.", UnitDamageScale = 1.12f },
		new() { Id = "rush_fervor", Title = "Fervor", Description = "+20% courage generation for the rest of the rush.", CourageGainScale = 1.2f },
		new() { Id = "rush_plunder", Title = "Plunder", Description = "+500 bonus gold at the end of the rush.", BonusGold = 500 },
		new() { Id = "rush_ironwall", Title = "Iron Wall", Description = "+25% unit health, -5% damage.", UnitHealthScale = 1.25f, UnitDamageScale = 0.95f },
		new() { Id = "rush_frenzy", Title = "Frenzy", Description = "+20% damage, -10% health.", UnitDamageScale = 1.2f, UnitHealthScale = 0.9f },
	};

	public static int TotalWaves => Waves.Length;

	public static int TotalRewardGold()
	{
		var total = 0;
		foreach (var wave in Waves) total += wave.RewardGold;
		return total;
	}

	public static BossRushBuff[] GetRandomBuffChoices(int count, Random rng, HashSet<string> exclude)
	{
		var available = new List<BossRushBuff>();
		foreach (var buff in Buffs)
		{
			if (!exclude.Contains(buff.Id)) available.Add(buff);
		}

		var result = new List<BossRushBuff>();
		for (var i = 0; i < count && available.Count > 0; i++)
		{
			var idx = rng.Next(available.Count);
			result.Add(available[idx]);
			available.RemoveAt(idx);
		}

		return result.ToArray();
	}
}
