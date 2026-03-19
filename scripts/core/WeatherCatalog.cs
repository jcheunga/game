using System.Collections.Generic;
using System.Linq;

public sealed class WeatherDefinition
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public float SpeedScale { get; }
    public float AggroRangeScale { get; }
    public float CourageGainScale { get; }
    public float DamageScale { get; }

    public WeatherDefinition(
        string id,
        string title,
        string description,
        float speedScale,
        float aggroRangeScale,
        float courageGainScale,
        float damageScale)
    {
        Id = id;
        Title = title;
        Description = description;
        SpeedScale = speedScale;
        AggroRangeScale = aggroRangeScale;
        CourageGainScale = courageGainScale;
        DamageScale = damageScale;
    }

    public bool IsClear => Id == "clear" || string.IsNullOrEmpty(Id);

    public string BuildEffectSummary()
    {
        if (IsClear)
        {
            return "";
        }

        var parts = new List<string>();
        if (SpeedScale < 0.999f || SpeedScale > 1.001f)
        {
            parts.Add($"Speed {FormatPercent(SpeedScale)}");
        }
        if (AggroRangeScale < 0.999f || AggroRangeScale > 1.001f)
        {
            parts.Add($"Aggro {FormatPercent(AggroRangeScale)}");
        }
        if (CourageGainScale < 0.999f || CourageGainScale > 1.001f)
        {
            parts.Add($"Courage {FormatPercent(CourageGainScale)}");
        }
        if (DamageScale < 0.999f || DamageScale > 1.001f)
        {
            parts.Add($"Damage {FormatPercent(DamageScale)}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "";
    }

    private static string FormatPercent(float scale)
    {
        var delta = Godot.Mathf.RoundToInt((scale - 1f) * 100f);
        return delta >= 0 ? $"+{delta}%" : $"{delta}%";
    }
}

public static class WeatherCatalog
{
    private static readonly WeatherDefinition[] Entries =
    {
        new("clear", "Clear Skies", "Standard conditions.", 1f, 1f, 1f, 1f),
        new("rain", "Heavy Rain", "Rain slows movement but the convoy pushes harder.", 0.88f, 0.92f, 1.08f, 1f),
        new("fog", "Dense Fog", "Reduced visibility forces close-range engagements.", 1f, 0.72f, 1f, 0.95f),
        new("ashstorm", "Ash Storm", "Choking ash clouds obscure the battlefield and fuel aggression.", 0.92f, 0.85f, 0.92f, 1.05f),
        new("blizzard", "Blizzard", "Bitter cold slows all forces and dulls weapon edges.", 0.82f, 0.88f, 1f, 0.92f),
    };

    private static readonly Dictionary<string, WeatherDefinition> ById =
        Entries.ToDictionary(e => e.Id);

    public static WeatherDefinition GetById(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Entries[0];
        }

        return ById.TryGetValue(id.ToLowerInvariant(), out var definition)
            ? definition
            : Entries[0];
    }

    public static IReadOnlyList<WeatherDefinition> GetAll() => Entries;

    public static string BuildStageSummary(StageDefinition stage)
    {
        var weather = GetById(stage?.WeatherId);
        if (weather.IsClear)
        {
            return "Weather: Clear Skies";
        }

        var effects = weather.BuildEffectSummary();
        return string.IsNullOrEmpty(effects)
            ? $"Weather: {weather.Title}"
            : $"Weather: {weather.Title} — {effects}";
    }

    public static string BuildInlineSummary(StageDefinition stage)
    {
        var weather = GetById(stage?.WeatherId);
        if (weather.IsClear)
        {
            return "";
        }

        var effects = weather.BuildEffectSummary();
        return string.IsNullOrEmpty(effects)
            ? weather.Title
            : $"{weather.Title} ({effects})";
    }
}
