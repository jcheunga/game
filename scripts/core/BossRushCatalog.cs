using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BossRushWave
{
	public string BossUnitId { get; set; } = "";
	public string BossName { get; set; } = "";
    public int SourceStage { get; set; }
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
    public static BossRushWave[] Waves => new[]{10,15,20,30,35,40,50,55,60,70,80,90,95,100}
        .Select((stage,index)=>
        {
            var source=GameData.GetStage(stage);
            var boss=source.Waves.SelectMany(w=>w.Entries).Select(e=>GameData.GetUnit(e.UnitId)).First(u=>u.IsBoss);
            return new BossRushWave {BossUnitId=boss.Id,BossName=boss.DisplayName,DistrictName=source.MapName,TerrainId=source.TerrainId,
                SourceStage=stage,EscortUnitIds=new[]{GameData.EnemyWalkerId, index%2==0?GameData.EnemyRunnerId:GameData.EnemyBruteId},
                HealthScale=1,DamageScale=1,RewardGold=75+index*10};
        }).ToArray();

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
