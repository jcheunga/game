using System;
using System.Collections.Generic;

public sealed class SeasonalEventReward
{
    public string Type { get; }
    public string ItemId { get; }
    public int Amount { get; }

    public SeasonalEventReward(string type, string itemId = "", int amount = 0)
    {
        Type = type;
        ItemId = itemId;
        Amount = amount;
    }
}

public sealed class SeasonalEventStage
{
    public int BaseStageNumber { get; }
    public float EnemyHealthScale { get; }
    public float EnemyDamageScale { get; }
    public string[] ForcedModifierIds { get; }
    public SeasonalEventReward CompletionReward { get; }

    public SeasonalEventStage(int baseStageNumber, float enemyHealthScale, float enemyDamageScale,
        string[] forcedModifierIds, SeasonalEventReward completionReward)
    {
        BaseStageNumber = baseStageNumber;
        EnemyHealthScale = enemyHealthScale;
        EnemyDamageScale = enemyDamageScale;
        ForcedModifierIds = forcedModifierIds ?? Array.Empty<string>();
        CompletionReward = completionReward;
    }
}

public sealed class SeasonalEventDefinition
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string BannerColorHex { get; }
    public string StartDate { get; }
    public string EndDate { get; }
    public SeasonalEventStage[] Stages { get; }
    public SeasonalEventMilestone[] Milestones { get; }

    public SeasonalEventDefinition(string id, string title, string description,
        string bannerColorHex, string startDate, string endDate,
        SeasonalEventStage[] stages, SeasonalEventMilestone[] milestones)
    {
        Id = id;
        Title = title;
        Description = description;
        BannerColorHex = bannerColorHex;
        StartDate = startDate;
        EndDate = endDate;
        Stages = stages;
        Milestones = milestones;
    }
}

public sealed class SeasonalEventMilestone
{
    public int StagesRequired { get; }
    public SeasonalEventReward Reward { get; }
    public string Label { get; }

    public SeasonalEventMilestone(int stagesRequired, SeasonalEventReward reward, string label)
    {
        StagesRequired = stagesRequired;
        Reward = reward;
        Label = label;
    }
}

public static class SeasonalEventCatalog
{
    private static readonly SeasonalEventDefinition[] Definitions =
    {
        new("midwinter_siege", "Midwinter Siege", "The Rotbound Host launches a desperate winter assault. Hold the line through 5 brutal encounters.",
            "4488cc", "2026-03-20", "2026-04-03",
            new SeasonalEventStage[]
            {
                new(15, 1.15f, 1.10f, new[] { "elite_vanguard" }, new SeasonalEventReward("gold", amount: 300)),
                new(22, 1.20f, 1.15f, new[] { "rapid_assault" }, new SeasonalEventReward("gold", amount: 400)),
                new(30, 1.25f, 1.20f, new[] { "cursed_ground" }, new SeasonalEventReward("food", amount: 8)),
                new(38, 1.35f, 1.25f, new[] { "elite_vanguard", "fortified_deploy" }, new SeasonalEventReward("sigils", amount: 2)),
                new(45, 1.50f, 1.35f, new[] { "cursed_ground", "rapid_assault" }, new SeasonalEventReward("relic", "relic_frostbound_crown")),
            },
            new SeasonalEventMilestone[]
            {
                new(2, new SeasonalEventReward("gold", amount: 500), "Hold the outer wall"),
                new(4, new SeasonalEventReward("sigils", amount: 3), "Breach the inner keep"),
                new(5, new SeasonalEventReward("relic", "relic_frostbound_crown"), "Survive the Midwinter Siege"),
            }),

        new("harvest_moon", "Harvest Moon Hunt", "Under the blood-red harvest moon, ancient beasts stir in the wilds. Track and defeat them before dawn.",
            "cc6622", "2026-05-01", "2026-05-15",
            new SeasonalEventStage[]
            {
                new(10, 1.10f, 1.10f, new[] { "rapid_assault" }, new SeasonalEventReward("gold", amount: 250)),
                new(18, 1.18f, 1.15f, new[] { "mirror_pressure" }, new SeasonalEventReward("food", amount: 6)),
                new(25, 1.25f, 1.20f, new[] { "lich_graveyard" }, new SeasonalEventReward("gold", amount: 500)),
                new(35, 1.30f, 1.25f, new[] { "tunnel_invasion" }, new SeasonalEventReward("sigils", amount: 2)),
                new(42, 1.40f, 1.30f, new[] { "mirror_pressure", "lich_graveyard" }, new SeasonalEventReward("relic", "relic_moonfire_talisman")),
            },
            new SeasonalEventMilestone[]
            {
                new(2, new SeasonalEventReward("gold", amount: 400), "Track the first beast"),
                new(4, new SeasonalEventReward("sigils", amount: 2), "Corner the pack alpha"),
                new(5, new SeasonalEventReward("relic", "relic_moonfire_talisman"), "Complete the Harvest Moon Hunt"),
            }),
    };

    private static readonly Dictionary<string, SeasonalEventDefinition> ById;

    static SeasonalEventCatalog()
    {
        ById = new Dictionary<string, SeasonalEventDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in Definitions)
        {
            ById[def.Id] = def;
        }
    }

    public static IReadOnlyList<SeasonalEventDefinition> GetAll() => Definitions;

    public static SeasonalEventDefinition GetById(string id)
    {
        return ById.TryGetValue(id, out var def) ? def : null;
    }

    public static SeasonalEventDefinition GetActiveEvent(DateTime utcNow)
    {
        foreach (var def in Definitions)
        {
            if (DateTime.TryParse(def.StartDate, out var start) &&
                DateTime.TryParse(def.EndDate, out var end))
            {
                if (utcNow.Date >= start.Date && utcNow.Date <= end.Date)
                {
                    return def;
                }
            }
        }

        return null;
    }
}
