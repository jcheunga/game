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
        return ResolveScaleModifier(stage, "armored_convoy", 1f, 1f, 2f);
    }

    public static float ResolveEnemyBaseHealthScale(StageDefinition stage)
    {
        return ResolveScaleModifier(stage, "reinforced_barricade", 1f, 1f, 2f);
    }

    public static float ResolveCourageGainScale(StageDefinition stage)
    {
        return ResolveScaleModifier(stage, "surging_courage", 1f, 0.5f, 2f);
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
        if (density <= 0)
        {
            return 1f;
        }

        return Mathf.Clamp(1f - (density * 0.08f), 0.72f, 1f);
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
            "armored_convoy" => $"Armored convoy ({ToPercent(modifier.Value, 1f)} bus hull)",
            "reinforced_barricade" => $"Reinforced barricade ({ToPercent(modifier.Value, 1f)} enemy hull)",
            "surging_courage" => $"Surging courage ({ToPercent(modifier.Value, 1f)} courage gain)",
            "swarm_density" => $"Swarm density (+{Mathf.Max(1, Mathf.RoundToInt(modifier.Value <= 0f ? 1f : modifier.Value))} enemy cap, faster pressure)",
            _ => modifier.Type
        };
    }

    private static string BuildShortLabel(StageModifierDefinition modifier)
    {
        return modifier.NormalizedType switch
        {
            "armored_convoy" => "Armored convoy",
            "reinforced_barricade" => "Reinforced barricade",
            "surging_courage" => "Surging courage",
            "swarm_density" => "Swarm density",
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
