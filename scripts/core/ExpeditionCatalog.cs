using System;
using System.Collections.Generic;

public sealed class ExpeditionDefinition
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public int DurationMinutes { get; }
    public int MinUnits { get; }
    public int MaxUnits { get; }
    public int BaseGoldReward { get; }
    public int BaseFoodReward { get; }
    public float RelicDropChance { get; }
    public string RelicDropRarity { get; }

    public ExpeditionDefinition(string id, string title, string description,
        int durationMinutes, int minUnits, int maxUnits,
        int baseGoldReward, int baseFoodReward,
        float relicDropChance, string relicDropRarity)
    {
        Id = id;
        Title = title;
        Description = description;
        DurationMinutes = durationMinutes;
        MinUnits = minUnits;
        MaxUnits = maxUnits;
        BaseGoldReward = baseGoldReward;
        BaseFoodReward = baseFoodReward;
        RelicDropChance = relicDropChance;
        RelicDropRarity = relicDropRarity;
    }
}

public sealed class ExpeditionSlotState
{
    public string ExpeditionId { get; set; } = "";
    public string[] AssignedUnitIds { get; set; } = Array.Empty<string>();
    public long StartedAtUnixSeconds { get; set; }
}

public static class ExpeditionCatalog
{
    public const int MaxSlots = 3;

    private static readonly ExpeditionDefinition[] Definitions =
    {
        new("patrol", "Border Patrol", "Send units to patrol the frontier. Steady gold income.",
            30, 1, 2, 150, 2, 0.05f, "common"),
        new("scavenge", "Ruin Scavenge", "Explore ruined keeps for food and supplies.",
            60, 1, 3, 80, 6, 0.15f, "common"),
        new("dungeon", "Dungeon Delve", "Venture into the deep vaults. High risk, high reward.",
            120, 2, 3, 300, 4, 0.30f, "rare"),
    };

    private static readonly Dictionary<string, ExpeditionDefinition> ById;

    static ExpeditionCatalog()
    {
        ById = new Dictionary<string, ExpeditionDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in Definitions)
        {
            ById[def.Id] = def;
        }
    }

    public static IReadOnlyList<ExpeditionDefinition> GetAll() => Definitions;

    public static ExpeditionDefinition Get(string id)
    {
        return ById.TryGetValue(id, out var def) ? def : null;
    }
}
