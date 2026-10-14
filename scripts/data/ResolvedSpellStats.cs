using System;
using Godot;

public readonly struct ResolvedSpellStats
{
	public ResolvedSpellStats(SpellDefinition definition, int level)
	{
		Id = definition.Id;
		DisplayName = definition.DisplayName;
		EffectType = definition.EffectType;
		ColorHex = definition.ColorHex;
		Level = Math.Max(1, level);

		var bonusLevel = Math.Max(0, Level - 1);
		// Upgrade flat damage, healing and durability. Ratios, distance, resource income,
		// charge counts and control duration keep their authored bounds.
		var flatPower = definition.EffectType is "fireball" or "heal" or "frost_burst" or
			"lightning_strike" or "stone_barricade" or "earthquake" or "aegis_bolt" or
			"radiant_rebuke" or "tidal_push" or "molten_brand" or "meteor" or "purifying_light" or
			"consecrated_ground" or "verdant_renewal" or "bramble_snare" or "chain_lightning" or
			"revealing_flare" or "banish" or "tactic_firepot_mine" or "tactic_healing_shrine" or
			"tactic_bolt_turret" or "tactic_frost_trap" or "tactic_repair_cache";
		Power = definition.Power * (flatPower ? 1f + bonusLevel * 0.05f : 1f);
		SecondaryPower = definition.SecondaryPower * (definition.EffectType == "heal" ? 1f + bonusLevel * 0.05f : 1f);
		Radius = definition.Radius;
		Cooldown = definition.Cooldown;
		ManaCost = definition.ManaCost;
		Duration = definition.Duration;
		Definition = definition;
	}

	public string Id { get; }
	public SpellDefinition Definition { get; }
	public string DisplayName { get; }
	public string EffectType { get; }
	public string ColorHex { get; }
	public int Level { get; }
	public float Power { get; }
	public float SecondaryPower { get; }
	public float Radius { get; }
	public float Cooldown { get; }
	public int ManaCost { get; }
	public float Duration { get; }

	public Color GetTint()
	{
		return string.IsNullOrWhiteSpace(ColorHex)
			? Colors.White
			: new Color(ColorHex);
	}
}
