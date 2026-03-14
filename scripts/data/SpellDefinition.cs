using System;
using Godot;

public sealed class SpellDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int UnlockStage { get; set; } = 1;
    public int GoldCost { get; set; }
    public int CourageCost { get; set; } = 20;
    public float Cooldown { get; set; } = 12f;
    public string EffectType { get; set; } = "";
    public float Power { get; set; }
    public float SecondaryPower { get; set; }
    public float Radius { get; set; } = 80f;
    public float Duration { get; set; }
    public string Description { get; set; } = "";
    public string ColorHex { get; set; } = "ffffff";

    public Color GetTint()
    {
        if (string.IsNullOrWhiteSpace(ColorHex))
        {
            return Colors.White;
        }

        return new Color(ColorHex);
    }

    public bool MatchesEffect(string effectType)
    {
        return EffectType.Equals(effectType ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
