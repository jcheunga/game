using System;
using System.Collections.Generic;

public sealed class AwakeningLevel
{
	public int Stars { get; }
	public int TokenCost { get; }
	public int GoldCost { get; }
	public float HealthScale { get; }
	public float DamageScale { get; }

	public AwakeningLevel(int stars, int tokenCost, int goldCost, float healthScale, float damageScale)
	{
		Stars = stars;
		TokenCost = tokenCost;
		GoldCost = goldCost;
		HealthScale = healthScale;
		DamageScale = damageScale;
	}
}

public readonly struct AwakeningBonus
{
	public float HealthScale { get; }
	public float DamageScale { get; }

	public AwakeningBonus(float healthScale, float damageScale)
	{
		HealthScale = healthScale;
		DamageScale = damageScale;
	}

	public static readonly AwakeningBonus None = new(1f, 1f);
}

public static class AwakeningCatalog
{
	public const int MaxStars = 5;

	private static readonly AwakeningLevel[] Levels =
	{
		new(1, 1, 200, 1.02f, 1.02f),
		new(2, 2, 500, 1.04f, 1.04f),
		new(3, 3, 1000, 1.06f, 1.06f),
		new(4, 5, 2000, 1.08f, 1.08f),
		new(5, 8, 4000, 1.10f, 1.10f),
	};

	public static AwakeningLevel GetLevel(int stars)
	{
		var index = stars - 1;
		return index >= 0 && index < Levels.Length ? Levels[index] : null;
	}

	public static AwakeningLevel GetNextLevel(int currentStars)
	{
		return GetLevel(currentStars + 1);
	}

	public static IReadOnlyList<AwakeningLevel> GetAll() => Levels;

	public static AwakeningBonus ResolveBonus(int stars)
	{
		if (stars <= 0) return AwakeningBonus.None;
		var clamped = Math.Min(stars, MaxStars);
		var level = GetLevel(clamped);
		return level != null ? new AwakeningBonus(level.HealthScale, level.DamageScale) : AwakeningBonus.None;
	}
}
