using System;
using System.Collections.Generic;

public sealed class MasteryRankDefinition
{
	public int Rank { get; }
	public string Title { get; }
	public int XPRequired { get; }
	public float HealthScale { get; }
	public float DamageScale { get; }

	public MasteryRankDefinition(int rank, string title, int xpRequired, float healthScale, float damageScale)
	{
		Rank = rank;
		Title = title;
		XPRequired = xpRequired;
		HealthScale = healthScale;
		DamageScale = damageScale;
	}
}

public readonly struct MasteryBonus
{
	public float HealthScale { get; }
	public float DamageScale { get; }

	public MasteryBonus(float healthScale, float damageScale)
	{
		HealthScale = healthScale;
		DamageScale = damageScale;
	}

	public static readonly MasteryBonus None = new(1f, 1f);
}

public static class MasteryCatalog
{
	public const int XPPerDeploy = 10;
	public const int XPPerKill = 25;
	public const int XPPerBattleWin = 50;

	private static readonly MasteryRankDefinition[] Ranks =
	{
		new(0, "Unranked", 0, 1f, 1f),
		new(1, "Initiate", 100, 1.01f, 1.01f),
		new(2, "Adept", 350, 1.02f, 1.02f),
		new(3, "Expert", 800, 1.03f, 1.03f),
		new(4, "Master", 1500, 1.04f, 1.04f),
		new(5, "Grand Master", 3000, 1.05f, 1.05f),
	};

	public static IReadOnlyList<MasteryRankDefinition> GetAllRanks() => Ranks;

	public static MasteryRankDefinition GetRank(int xp)
	{
		MasteryRankDefinition best = Ranks[0];
		foreach (var r in Ranks)
		{
			if (xp >= r.XPRequired)
			{
				best = r;
			}
		}
		return best;
	}

	public static MasteryRankDefinition GetNextRank(int xp)
	{
		foreach (var r in Ranks)
		{
			if (xp < r.XPRequired)
			{
				return r;
			}
		}
		return null; // max rank
	}

	public static MasteryBonus ResolveBonus(int xp)
	{
		var rank = GetRank(xp);
		return new MasteryBonus(rank.HealthScale, rank.DamageScale);
	}
}
