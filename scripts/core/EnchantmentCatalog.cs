using System;
using System.Collections.Generic;

public sealed class EnchantmentDefinition
{
	public string Id { get; }
	public string Title { get; }
	public string Description { get; }
	public float HealthScale { get; }
	public float DamageScale { get; }
	public float SpeedScale { get; }
	public float LifestealRatio { get; }
	public float ThornsDamageRatio { get; }
	public float CritChance { get; }
	public float CritMultiplier { get; }
	public int GoldCost { get; }
	public int EssenceCost { get; }

	public EnchantmentDefinition(string id, string title, string description,
		float healthScale, float damageScale, float speedScale,
		float lifestealRatio, float thornsDamageRatio,
		float critChance, float critMultiplier,
		int goldCost, int essenceCost)
	{
		Id = id;
		Title = title;
		Description = description;
		HealthScale = healthScale;
		DamageScale = damageScale;
		SpeedScale = speedScale;
		LifestealRatio = lifestealRatio;
		ThornsDamageRatio = thornsDamageRatio;
		CritChance = critChance;
		CritMultiplier = critMultiplier;
		GoldCost = goldCost;
		EssenceCost = essenceCost;
	}
}

public static class EnchantmentCatalog
{
	private static readonly EnchantmentDefinition[] Definitions =
	{
		new("ench_flame_touch", "Flame Touch", "+12% damage", 1f, 1.12f, 1f, 0f, 0f, 0f, 1f, 500, 3),
		new("ench_lifesteal", "Lifesteal", "Heal 8% of damage dealt", 1f, 1f, 1f, 0.08f, 0f, 0f, 1f, 600, 4),
		new("ench_thorns", "Thorns", "Reflect 15% of damage taken", 1f, 1f, 1f, 0f, 0.15f, 0f, 1f, 600, 4),
		new("ench_haste", "Haste", "+10% movement speed", 1f, 1f, 1.10f, 0f, 0f, 0f, 1f, 400, 2),
		new("ench_shielding", "Shielding", "+15% max health", 1.15f, 1f, 1f, 0f, 0f, 0f, 1f, 400, 2),
		new("ench_vampiric", "Vampiric", "+5% damage, heal 5% of damage dealt", 1f, 1.05f, 1f, 0.05f, 0f, 0f, 1f, 700, 5),
		new("ench_crit_strike", "Crit Strike", "15% chance for 1.5x damage", 1f, 1f, 1f, 0f, 0f, 0.15f, 1.5f, 600, 4),
		new("ench_poison", "Poison", "+8% damage", 1f, 1.08f, 1f, 0f, 0f, 0f, 1f, 450, 3),
		new("ench_fortify", "Fortify", "+20% health, -5% speed", 1.20f, 1f, 0.95f, 0f, 0f, 0f, 1f, 500, 3),
		new("ench_arcane_echo", "Arcane Echo", "+10% damage, +5% health", 1.05f, 1.10f, 1f, 0f, 0f, 0f, 1f, 650, 5),
	};

	private static readonly Dictionary<string, EnchantmentDefinition> ById;

	static EnchantmentCatalog()
	{
		ById = new Dictionary<string, EnchantmentDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (var d in Definitions)
		{
			ById[d.Id] = d;
		}
	}

	public static IReadOnlyList<EnchantmentDefinition> GetAll() => Definitions;

	public static EnchantmentDefinition GetById(string id)
	{
		return ById.TryGetValue(id, out var d) ? d : null;
	}
}
