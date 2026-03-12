using System;
using System.Collections.Generic;
using System.Linq;

public readonly struct SquadSynergyBonus
{
    public SquadSynergyBonus(float healthScale, float damageScale, float cooldownReduction, int baseDamageBonus)
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

    public static SquadSynergyBonus None => new(1f, 1f, 0f, 0);
}

public sealed class SquadSynergyDefinition
{
    public SquadSynergyDefinition(
        string id,
        string title,
        string requiredTag,
        int requiredCount,
        string summary,
        float healthScale,
        float damageScale,
        float cooldownReduction,
        int baseDamageBonus)
    {
        Id = id;
        Title = title;
        RequiredTag = requiredTag;
        RequiredCount = Math.Max(1, requiredCount);
        Summary = summary;
        Bonus = new SquadSynergyBonus(healthScale, damageScale, cooldownReduction, baseDamageBonus);
    }

    public string Id { get; }
    public string Title { get; }
    public string RequiredTag { get; }
    public int RequiredCount { get; }
    public string Summary { get; }
    public SquadSynergyBonus Bonus { get; }
}

public static class SquadSynergyCatalog
{
    public const string FrontlineTag = "frontline";
    public const string ReconTag = "recon";
    public const string SupportTag = "support";
    public const string BreachTag = "breach";

    private static readonly SquadSynergyDefinition[] Definitions =
    {
        new(
            "frontline_drill",
            "Frontline Drill",
            FrontlineTag,
            2,
            "2 Frontline cards: deployed squad gains +10% health and +2 base damage.",
            1.10f,
            1f,
            0f,
            2),
        new(
            "recon_link",
            "Recon Link",
            ReconTag,
            2,
            "2 Recon cards: deployed squad gains +8% attack damage and -0.08s attack cooldown.",
            1f,
            1.08f,
            0.08f,
            0),
        new(
            "support_mesh",
            "Support Mesh",
            SupportTag,
            2,
            "2 Support cards: deployed squad gains +8% health and +6% attack damage.",
            1.08f,
            1.06f,
            0f,
            0),
        new(
            "breach_line",
            "Breach Line",
            BreachTag,
            2,
            "2 Breach cards: deployed squad gains +5% attack damage and +4 base damage.",
            1f,
            1.05f,
            0f,
            4)
    };

    public static IReadOnlyList<SquadSynergyDefinition> ResolveActive(IEnumerable<UnitDefinition> deckUnits)
    {
        var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var unit in deckUnits)
        {
            if (unit == null || string.IsNullOrWhiteSpace(unit.SquadTag))
            {
                continue;
            }

            var normalizedTag = NormalizeTag(unit.SquadTag);
            tagCounts[normalizedTag] = tagCounts.TryGetValue(normalizedTag, out var current)
                ? current + 1
                : 1;
        }

        return Definitions
            .Where(definition =>
                tagCounts.TryGetValue(definition.RequiredTag, out var count) &&
                count >= definition.RequiredCount)
            .ToArray();
    }

    public static SquadSynergyBonus Aggregate(IEnumerable<SquadSynergyDefinition> synergies)
    {
        var healthScale = 1f;
        var damageScale = 1f;
        var cooldownReduction = 0f;
        var baseDamageBonus = 0;

        foreach (var synergy in synergies)
        {
            if (synergy == null)
            {
                continue;
            }

            healthScale *= synergy.Bonus.HealthScale;
            damageScale *= synergy.Bonus.DamageScale;
            cooldownReduction += synergy.Bonus.CooldownReduction;
            baseDamageBonus += synergy.Bonus.BaseDamageBonus;
        }

        return new SquadSynergyBonus(healthScale, damageScale, cooldownReduction, baseDamageBonus);
    }

    public static string NormalizeTag(string tag)
    {
        return string.IsNullOrWhiteSpace(tag)
            ? ""
            : tag.Trim().ToLowerInvariant();
    }

    public static string GetTagDisplayName(string tag)
    {
        return NormalizeTag(tag) switch
        {
            FrontlineTag => "Frontline",
            ReconTag => "Recon",
            SupportTag => "Support",
            BreachTag => "Breach",
            _ => "Unassigned"
        };
    }
}
