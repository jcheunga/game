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

    /// <summary>Seconds between reinforcement packs while a commander lives.</summary>
    public const float ReinforcementInterval = 8f;

    /// <summary>How many walkers, runners and brutes may join each commander while it lives, a pack every
    /// <see cref="ReinforcementInterval"/> seconds until they run out. A squad that kills the commander quickly meets few
    /// of them; one that can't meets them all, so the rush climbs from round 6 instead of staying a walkover until the
    /// last two. Set per commander because the commanders' own strength is uneven: the ranged Harrow Tidemaster (3), the
    /// armoured Plague Monarch (6), the Bone Pontiff (8) and the Steppe Warlord (11) get fewer than their neighbours. The
    /// last two summon their own reinforcements, which are the fight, and get none. Fitted in November 2026 with
    /// --bossrush-gameplay --squad/--deck (docs/PROGRESSION_AND_BOSS_RUSH.md). Tried and rejected: one formula for every
    /// commander (rounds 3 and 6 became walls), uncapped reinforcements (slow commanders snowballed), and bigger opening
    /// crowds of escorts (troops cut down a single crowd however large).</summary>
    private static readonly int[] ReinforcementBudgets = {0, 1, 2, 3, 4, 3, 6, 5, 13, 10, 7, 15, 0, 0};

    public static int ReinforcementBudget(int index) => index >= 0 && index < ReinforcementBudgets.Length ? ReinforcementBudgets[index] : 0;

    /// <summary>Troops per reinforcement pack: one, then a larger pack every fourth round.</summary>
    public static int ReinforcementPack(int index) => 1 + index / 4;

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
