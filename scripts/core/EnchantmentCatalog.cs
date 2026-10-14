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
	public float ArmorBonus { get; init; }
	public float MagicalResistanceBonus { get; init; }
	public float AttackIntervalReductionPct { get; init; }

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
		new("ench_flame_touch", "Flame Touch", "+2% damage, +2 critical damage points; periodic burn", 1f, 1.02f, 1f, 0f, 0f, 0f, 1.02f, 180, 2),
		new("ench_lifesteal", "Lifesteal", "+1% damage; heal 3% of direct troop damage", 1f, 1.01f, 1f, 0.03f, 0f, 0f, 1f, 220, 2),
		new("ench_thorns", "Thorns", "+3 armor; reflect 5% of direct melee damage", 1f, 1f, 1f, 0f, 0.05f, 0f, 1f, 220, 2) { ArmorBonus = 3f },
		new("ench_haste", "Haste", "+3% movement, 1% faster attack recovery", 1f, 1f, 1.03f, 0f, 0f, 0f, 1f, 160, 1) { AttackIntervalReductionPct = 1f },
		new("ench_shielding", "Shielding", "+3% health, +2 magical resistance points", 1.03f, 1f, 1f, 0f, 0f, 0f, 1f, 160, 1) { MagicalResistanceBonus = 2f },
		new("ench_vampiric", "Vampiric", "+1% damage, +2 critical damage points; 2% lifesteal", 1f, 1.01f, 1f, 0.02f, 0f, 0f, 1.02f, 240, 3),
		new("ench_crit_strike", "Crit Strike", "+2 critical chance points, +4 critical damage points", 1f, 1f, 1f, 0f, 0f, 0.02f, 1.04f, 220, 2),
		new("ench_poison", "Poison", "+1% damage, +1 magical resistance point; periodic blight", 1f, 1.01f, 1f, 0f, 0f, 0f, 1f, 180, 2) { MagicalResistanceBonus = 1f },
		new("ench_fortify", "Fortify", "+3% health, +4 armor, -3% movement", 1.03f, 1f, 0.97f, 0f, 0f, 0f, 1f, 180, 2) { ArmorBonus = 4f },
		new("ench_arcane_echo", "Arcane Echo", "+2% damage, +2 magical resistance points; a limited echo", 1f, 1.02f, 1f, 0f, 0f, 0f, 1f, 240, 3) { MagicalResistanceBonus = 2f },
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
		if(string.IsNullOrWhiteSpace(id))return null;
		return ById.TryGetValue(id, out var d) ? d : null;
	}
}
