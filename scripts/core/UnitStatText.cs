using System.Collections.Generic;
using Godot;

public static class UnitStatText
{
    public static string BuildInlineTraits(UnitStats stats)
    {
        var parts = new List<string>();

        if (stats.AttackSplashRadius > 0.05f)
        {
            parts.Add($"Splash {stats.AttackSplashRadius:0.#}");
        }

        if (stats.BusRepairAmount > 0.05f)
        {
            parts.Add($"Repair {stats.BusRepairAmount:0.#}");
        }

        if (HasAura(stats))
        {
            parts.Add(BuildAuraSummary(stats));
        }

        return parts.Count == 0
            ? ""
            : "  |  " + string.Join("  |  ", parts);
    }

    public static bool HasAura(UnitStats stats)
    {
        return stats.AuraRadius > 0.05f &&
            (stats.AuraAttackDamageScale > 1.01f || stats.AuraSpeedScale > 1.01f);
    }

    public static string BuildAuraSummary(UnitStats stats)
    {
        var attackBonus = Mathf.RoundToInt(Mathf.Max(0f, stats.AuraAttackDamageScale - 1f) * 100f);
        var speedBonus = Mathf.RoundToInt(Mathf.Max(0f, stats.AuraSpeedScale - 1f) * 100f);
        return $"Aura {stats.AuraRadius:0.#}r / +{attackBonus}% ATK / +{speedBonus}% SPD";
    }
}
