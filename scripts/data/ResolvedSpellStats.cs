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
		Power = definition.Power * (1f + (bonusLevel * 0.10f));
		SecondaryPower = definition.SecondaryPower * (1f + (bonusLevel * 0.10f));
		Radius = definition.Radius * (1f + (bonusLevel * 0.05f));
		Cooldown = Mathf.Max(4f, definition.Cooldown - (bonusLevel * 0.6f));
		CourageCost = Math.Max(8, definition.CourageCost - (bonusLevel * 1));
		Duration = definition.Duration > 0f
			? definition.Duration + (bonusLevel * 0.3f)
			: 0f;
	}

	public string Id { get; }
	public string DisplayName { get; }
	public string EffectType { get; }
	public string ColorHex { get; }
	public int Level { get; }
	public float Power { get; }
	public float SecondaryPower { get; }
	public float Radius { get; }
	public float Cooldown { get; }
	public int CourageCost { get; }
	public float Duration { get; }

	public Color GetTint()
	{
		return string.IsNullOrWhiteSpace(ColorHex)
			? Colors.White
			: new Color(ColorHex);
	}
}
