using System;
using System.Linq;
using System.Text;
using Godot;

public static class StageModifiers
{
    public static bool HasModifiers(StageDefinition stage)
    {
        return stage?.Modifiers != null && stage.Modifiers.Length > 0;
    }

    public static float ResolvePlayerBaseHealthScale(StageDefinition stage)
    {
        return ResolveScaleModifier(stage, "armored_convoy", 1f, 1f, 2f) *
            ResolveScaleModifier(stage, "strained_caravan", 1f, 0.5f, 1f);
    }

    public static float ResolveEnemyBaseHealthScale(StageDefinition stage)
    {
        return ResolveScaleModifier(stage, "reinforced_barricade", 1f, 1f, 2f);
    }

    public static float ResolveCourageGainScale(StageDefinition stage)
    {
        return ResolveScaleModifier(stage, "surging_courage", 1f, 0.5f, 2f) *
            ResolveScaleModifier(stage, "drained_courage", 1f, 0.5f, 1f);
    }

    public static int ResolveEnemyCapBonus(StageDefinition stage)
    {
        if (!TryGetModifier(stage, "swarm_density", out var modifier))
        {
            return 0;
        }

        return Mathf.Max(0, Mathf.RoundToInt(modifier.Value <= 0f ? 1f : modifier.Value));
    }

    public static float ResolveEnemySpawnIntervalScale(StageDefinition stage)
    {
        var density = ResolveEnemyCapBonus(stage);
        var rapidScale = ResolveScaleModifier(stage, "rapid_assault", 1f, 0.5f, 1f);
        if (density <= 0 && rapidScale >= 1f)
        {
            return 1f;
        }

        var densityScale = density > 0
            ? Mathf.Clamp(1f - (density * 0.08f), 0.72f, 1f)
            : 1f;
        return Mathf.Clamp(densityScale * rapidScale, 0.55f, 1f);
    }

    public static float ResolveEnemyHealthScale(StageDefinition stage)
    {
        return ResolveScaleModifier(stage, "elite_vanguard", 1f, 1f, 1.5f);
    }

    public static float ResolveEnemyDamageScale(StageDefinition stage)
    {
        return ResolveScaleModifier(stage, "elite_vanguard", 1f, 1f, 1.5f);
    }

    public static float ResolveCursedGroundDps(StageDefinition stage)
    {
        if (!TryGetModifier(stage, "cursed_ground", out var modifier))
        {
            return 0f;
        }

        return Mathf.Clamp(modifier.Value <= 0f ? 2.5f : modifier.Value, 0.5f, 8f);
    }

    public static bool HasCursedGround(StageDefinition stage)
    {
        return ResolveCursedGroundDps(stage) > 0.01f;
    }

    public static float ResolveFortifiedDeployDefenseScale(StageDefinition stage)
    {
        if (!TryGetModifier(stage, "fortified_deploy", out var modifier))
        {
            return 1f;
        }

        return Mathf.Clamp(modifier.Value <= 0f ? 0.6f : modifier.Value, 0.3f, 0.9f);
    }

    public static float ResolveFortifiedDeployDuration(StageDefinition stage)
    {
        return HasModifierType(stage, "fortified_deploy") ? 4f : 0f;
    }

    public static bool HasFortifiedDeploy(StageDefinition stage)
    {
        return HasModifierType(stage, "fortified_deploy");
    }

    public static bool HasRapidAssault(StageDefinition stage)
    {
        return HasModifierType(stage, "rapid_assault");
    }

    public static bool HasEliteVanguard(StageDefinition stage)
    {
        return HasModifierType(stage, "elite_vanguard");
    }

    private static bool HasModifierType(StageDefinition stage, string type)
    {
        return TryGetModifier(stage, type, out _);
    }

    public static string BuildSummaryText(StageDefinition stage)
    {
        if (!HasModifiers(stage))
        {
            return "Stage modifiers: none";
        }

        var builder = new StringBuilder();
        builder.AppendLine("Stage modifiers:");

        foreach (var modifier in stage.Modifiers)
        {
            if (modifier == null || string.IsNullOrWhiteSpace(modifier.Type))
            {
                continue;
            }

            builder.AppendLine($"- {BuildModifierLabel(modifier)}");
        }

        return builder.ToString().TrimEnd();
    }

    public static string BuildInlineSummary(StageDefinition stage)
    {
        if (!HasModifiers(stage))
        {
            return "none";
        }

        var labels = stage.Modifiers
            .Where(modifier => modifier != null && !string.IsNullOrWhiteSpace(modifier.Type))
            .Select(BuildShortLabel)
            .ToArray();

        return labels.Length > 0
            ? string.Join(", ", labels)
            : "none";
    }

    private static float ResolveScaleModifier(
        StageDefinition stage,
        string type,
        float defaultValue,
        float minValue,
        float maxValue)
    {
        if (!TryGetModifier(stage, type, out var modifier))
        {
            return defaultValue;
        }

        var value = modifier.Value <= 0f ? defaultValue : modifier.Value;
        return Mathf.Clamp(value, minValue, maxValue);
    }

    private static bool TryGetModifier(StageDefinition stage, string type, out StageModifierDefinition modifier)
    {
        if (stage?.Modifiers != null)
        {
            for (var i = 0; i < stage.Modifiers.Length; i++)
            {
                var candidate = stage.Modifiers[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.NormalizedType == type)
                {
                    modifier = candidate;
                    return true;
                }
            }
        }

        modifier = null!;
        return false;
    }

    private static string BuildModifierLabel(StageModifierDefinition modifier)
    {
        if (!string.IsNullOrWhiteSpace(modifier.Label))
        {
            return modifier.Label;
        }

        return modifier.NormalizedType switch
        {
            "armored_convoy" => $"Armored caravan ({ToPercent(modifier.Value, 1f)} war wagon hull)",
            "strained_caravan" => $"Strained caravan ({ToPercent(modifier.Value, 1f)} war wagon hull)",
            "reinforced_barricade" => $"Reinforced gatehouse ({ToPercent(modifier.Value, 1f)} enemy hull)",
            "surging_courage" => $"Surging courage ({ToPercent(modifier.Value, 1f)} courage gain)",
            "drained_courage" => $"Drained courage ({ToPercent(modifier.Value, 1f)} courage gain)",
            "swarm_density" => $"Swarm density (+{Mathf.Max(1, Mathf.RoundToInt(modifier.Value <= 0f ? 1f : modifier.Value))} enemy cap, faster pressure)",
            "elite_vanguard" => $"Elite vanguard ({ToPercent(modifier.Value, 1f)} enemy health and damage)",
            "rapid_assault" => $"Rapid assault ({ToPercent(modifier.Value, 1f)} wave interval)",
            "cursed_ground" => $"Cursed ground ({(modifier.Value <= 0f ? 2.5f : modifier.Value):0.#} damage/s to deployed allies)",
            "fortified_deploy" => $"Fortified deploy (allies gain {Mathf.RoundToInt((1f - Mathf.Clamp(modifier.Value <= 0f ? 0.6f : modifier.Value, 0.3f, 0.9f)) * 100f)}% defense for 4s on deploy)",
            _ => modifier.Type
        };
    }

    private static string BuildShortLabel(StageModifierDefinition modifier)
    {
        return modifier.NormalizedType switch
        {
            "armored_convoy" => "Armored caravan",
            "strained_caravan" => "Strained caravan",
            "reinforced_barricade" => "Reinforced gatehouse",
            "surging_courage" => "Surging courage",
            "drained_courage" => "Drained courage",
            "swarm_density" => "Swarm density",
            "elite_vanguard" => "Elite vanguard",
            "rapid_assault" => "Rapid assault",
            "cursed_ground" => "Cursed ground",
            "fortified_deploy" => "Fortified deploy",
            _ => modifier.Type
        };
    }

    private static string ToPercent(float value, float baseline)
    {
        var effectiveValue = value <= 0f ? baseline : value;
        var delta = Mathf.RoundToInt((effectiveValue - baseline) * 100f);
        if (delta >= 0)
        {
            return $"+{delta}%";
        }

        return $"{delta}%";
    }
}
