using System;
using System.Collections.Generic;
using System.Linq;

public readonly struct UnitDoctrineBonus
{
    public UnitDoctrineBonus(float healthScale, float damageScale, float cooldownReduction, int baseDamageBonus)
    {
        HealthScale = healthScale;
        DamageScale = damageScale;
        CooldownReduction = cooldownReduction;
        BaseDamageBonus = baseDamageBonus;
    }

    public float HealthScale { get; }
    public float DamageScale { get; }
    public float CooldownReduction { get; }
    public int BaseDamageBonus { get; }

    public static UnitDoctrineBonus None => new(1f, 1f, 0f, 0);
}

public sealed class UnitDoctrineDefinition
{
    public UnitDoctrineDefinition(
        string id,
        string title,
        string squadTag,
        string summary,
        float healthScale,
        float damageScale,
        float cooldownReduction,
        int baseDamageBonus)
    {
        Id = id;
        Title = title;
        SquadTag = SquadSynergyCatalog.NormalizeTag(squadTag);
        Summary = summary;
        Bonus = new UnitDoctrineBonus(healthScale, damageScale, cooldownReduction, baseDamageBonus);
    }

    public string Id { get; }
    public string Title { get; }
    public string SquadTag { get; }
    public string Summary { get; }
    public UnitDoctrineBonus Bonus { get; }
}

public static class UnitDoctrineCatalog
{
    private static readonly UnitDoctrineDefinition[] Definitions =
    {
        new(
            "frontline_bastion",
            "Bastion Oath",
            SquadSynergyCatalog.FrontlineTag,
            "+18% health and +3 base damage.",
            1.18f,
            1f,
            0f,
            3),
        new(
            "frontline_duelist",
            "Duelist Rite",
            SquadSynergyCatalog.FrontlineTag,
            "+14% attack damage and -0.08s attack cooldown.",
            1f,
            1.14f,
            0.08f,
            0),
        new(
            "recon_trailblazer",
            "Trailblazer Mark",
            SquadSynergyCatalog.ReconTag,
            "+10% health and -0.10s attack cooldown.",
            1.10f,
            1f,
            0.10f,
            0),
        new(
            "recon_deadeye",
            "Deadeye Sigil",
            SquadSynergyCatalog.ReconTag,
            "+16% attack damage and +3 base damage.",
            1f,
            1.16f,
            0f,
            3),
        new(
            "support_ward_circle",
            "Ward Circle",
            SquadSynergyCatalog.SupportTag,
            "+12% health and +10% attack damage.",
            1.12f,
            1.10f,
            0f,
            0),
        new(
            "support_quick_chant",
            "Quick Chant",
            SquadSynergyCatalog.SupportTag,
            "+8% attack damage and -0.10s attack cooldown.",
            1f,
            1.08f,
            0.10f,
            0),
        new(
            "breach_siegebreaker",
            "Siegebreaker Seal",
            SquadSynergyCatalog.BreachTag,
            "+10% attack damage and +6 base damage.",
            1f,
            1.10f,
            0f,
            6),
        new(
            "breach_iron_vanguard",
            "Iron Vanguard",
            SquadSynergyCatalog.BreachTag,
            "+16% health and +4 base damage.",
            1.16f,
            1f,
            0f,
            4)
    };

    public static IReadOnlyList<UnitDoctrineDefinition> GetForTag(string squadTag)
    {
        var normalizedTag = SquadSynergyCatalog.NormalizeTag(squadTag);
        return Definitions
            .Where(definition => definition.SquadTag.Equals(normalizedTag, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public static UnitDoctrineDefinition GetOrNull(string doctrineId)
    {
        if (string.IsNullOrWhiteSpace(doctrineId))
        {
            return null;
        }

        for (var i = 0; i < Definitions.Length; i++)
        {
            if (Definitions[i].Id.Equals(doctrineId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Definitions[i];
            }
        }

        return null;
    }

    public static string NormalizeId(string doctrineId)
    {
        return GetOrNull(doctrineId)?.Id ?? "";
    }
}
