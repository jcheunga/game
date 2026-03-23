using System;
using System.Collections.Generic;

public sealed class BattleMutatorDefinition
{
	public string Id { get; }
	public string Title { get; }
	public string Description { get; }
	public float PlayerHealthScale { get; }
	public float PlayerDamageScale { get; }
	public float EnemyHealthScale { get; }
	public float EnemySpawnRateScale { get; }
	public float GameSpeedScale { get; }
	public float GoldRewardMultiplier { get; }
	public bool DisableSpells { get; }
	public int MaxDeployedUnits { get; }

	public BattleMutatorDefinition(string id, string title, string description,
		float playerHealthScale, float playerDamageScale, float enemyHealthScale,
		float enemySpawnRateScale, float gameSpeedScale, float goldRewardMultiplier,
		bool disableSpells = false, int maxDeployedUnits = 0)
	{
		Id = id;
		Title = title;
		Description = description;
		PlayerHealthScale = playerHealthScale;
		PlayerDamageScale = playerDamageScale;
		EnemyHealthScale = enemyHealthScale;
		EnemySpawnRateScale = enemySpawnRateScale;
		GameSpeedScale = gameSpeedScale;
		GoldRewardMultiplier = goldRewardMultiplier;
		DisableSpells = disableSpells;
		MaxDeployedUnits = maxDeployedUnits;
	}
}

public static class BattleMutatorCatalog
{
	private static readonly BattleMutatorDefinition[] Definitions =
	{
		new("mut_double_speed", "Double Speed", "Game runs at 2x speed.", 1f, 1f, 1f, 1f, 2f, 1.2f),
		new("mut_glass_cannon", "Glass Cannon", "+50% damage, -50% health.", 0.5f, 1.5f, 1f, 1f, 1f, 1.3f),
		new("mut_tank_mode", "Tank Mode", "+100% health, -30% damage.", 2f, 0.7f, 1f, 1f, 1f, 1.1f),
		new("mut_no_spells", "No Spells", "Spells are disabled.", 1f, 1f, 1f, 1f, 1f, 1.25f, disableSpells: true),
		new("mut_swarm", "Swarm Mode", "2x enemy count, -40% enemy HP each.", 1f, 1f, 0.6f, 2f, 1f, 1.3f),
		new("mut_gold_fever", "Gold Fever", "+100% gold, +50% enemy HP.", 1f, 1f, 1.5f, 1f, 1f, 2f),
		new("mut_fog", "Fog of War", "Reduced visibility. Enemies appear late.", 1f, 1f, 1f, 1f, 1f, 1.15f),
		new("mut_minimalist", "Minimalist", "Max 2 units deployed at once.", 1f, 1f, 1f, 1f, 1f, 1.4f, maxDeployedUnits: 2),
		new("mut_berserker", "Berserker", "All units in permanent frenzy (+30% speed, +20% damage, -15% HP).", 0.85f, 1.2f, 1f, 1f, 1f, 1.2f),
		new("mut_ironman", "Ironman", "No healing. Units cannot be healed.", 1f, 1f, 1f, 1f, 1f, 1.35f),
		new("mut_elite_only", "Elite Gauntlet", "All enemies are elite tier (+30% stats).", 1f, 1f, 1.3f, 1f, 1f, 1.5f),
		new("mut_blitz", "Blitz", "3x speed, +25% enemy damage. Pure chaos.", 1f, 1f, 1f, 1f, 3f, 1.6f),
	};

	private static readonly Dictionary<string, BattleMutatorDefinition> ById;

	static BattleMutatorCatalog()
	{
		ById = new Dictionary<string, BattleMutatorDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (var d in Definitions) ById[d.Id] = d;
	}

	public static IReadOnlyList<BattleMutatorDefinition> GetAll() => Definitions;

	public static BattleMutatorDefinition GetById(string id)
	{
		return ById.TryGetValue(id, out var d) ? d : null;
	}
}
