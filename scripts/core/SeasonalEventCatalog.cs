using System;
using System.Collections.Generic;
using System.Linq;

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
    /// <summary>The stage's modifiers at their authored strengths, added to its campaign stage's in battle.</summary>
    public StageModifierDefinition[] Modifiers { get; }
    public SeasonalEventReward CompletionReward { get; }

    public SeasonalEventStage(int baseStageNumber, float enemyHealthScale, float enemyDamageScale,
        string[] forcedModifierIds, SeasonalEventReward completionReward)
    {
        BaseStageNumber = baseStageNumber;
        EnemyHealthScale = enemyHealthScale;
        EnemyDamageScale = enemyDamageScale;
        ForcedModifierIds = forcedModifierIds ?? Array.Empty<string>();
        Modifiers = StageModifiers.Authored(ForcedModifierIds);
        CompletionReward = completionReward;
    }
}

public sealed class SeasonalEventDefinition
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string BannerColorHex { get; }
    /// <summary>"MM-DD": the event opens on this day every year and runs to the end day, which may fall in the next year.</summary>
    public string StartMonthDay { get; }
    public string EndMonthDay { get; }
    public SeasonalEventStage[] Stages { get; }
    public SeasonalEventMilestone[] Milestones { get; }

    public SeasonalEventDefinition(string id, string title, string description,
        string bannerColorHex, string startMonthDay, string endMonthDay,
        SeasonalEventStage[] stages, SeasonalEventMilestone[] milestones)
    {
        Id = id;
        Title = title;
        Description = description;
        BannerColorHex = bannerColorHex;
        StartMonthDay = startMonthDay;
        EndMonthDay = endMonthDay;
        Stages = stages;
        Milestones = milestones;
    }

    /// <summary>The year's run that opened most recently on or before this day: its first and last days.</summary>
    public (DateTime Start, DateTime End) Edition(DateTime utcNow)
    {
        var today = utcNow.Date;
        var start = Day(today.Year, StartMonthDay);
        if (start > today) start = Day(today.Year - 1, StartMonthDay);
        var end = Day(start.Year, EndMonthDay);
        if (end < start) end = Day(start.Year + 1, EndMonthDay);
        return (start, end);
    }

    public bool IsActive(DateTime utcNow) => utcNow.Date <= Edition(utcNow).End;

    /// <summary>The first day of the next run to open after this day.</summary>
    public DateTime NextStart(DateTime utcNow)
    {
        var start = Day(utcNow.Year, StartMonthDay);
        return start > utcNow.Date ? start : Day(utcNow.Year + 1, StartMonthDay);
    }

    /// <summary>The key a year's run keeps its clears and claims under, such as midwinter_siege@2026.</summary>
    public string EditionKey(DateTime utcNow) => $"{Id}@{Edition(utcNow).Start.Year}";

    private static DateTime Day(int year, string monthDay)
    {
        var parts = monthDay.Split('-');
        var month = int.Parse(parts[0]);
        return new DateTime(year, month, Math.Min(int.Parse(parts[1]), DateTime.DaysInMonth(year, month)));
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
            "4488cc", "12-15", "01-05",
            new SeasonalEventStage[]
            {
                new(34, 1.15f, 1.10f, new[] { "elite_vanguard" }, new SeasonalEventReward("gold", amount: 300)),
                new(51, 1.20f, 1.15f, new[] { "rapid_assault" }, new SeasonalEventReward("gold", amount: 400)),
                new(66, 1.25f, 1.20f, new[] { "cursed_ground" }, new SeasonalEventReward("food", amount: 8)),
                new(82, 1.35f, 1.25f, new[] { "elite_vanguard", "fortified_deploy" }, new SeasonalEventReward("sigils", amount: 2)),
                new(97, 1.50f, 1.35f, new[] { "cursed_ground", "rapid_assault" }, new SeasonalEventReward("relic", "relic_frostbound_crown")),
            },
            new SeasonalEventMilestone[]
            {
                new(2, new SeasonalEventReward("gold", amount: 500), "Hold the outer wall"),
                new(4, new SeasonalEventReward("sigils", amount: 3), "Breach the inner keep"),
                new(5, new SeasonalEventReward("relic", "relic_frostbound_crown"), "Survive the siege"),
            }),

        new("harvest_moon", "Harvest Moon Hunt", "Under the blood-red harvest moon, ancient beasts stir in the wilds. Track and defeat them before dawn.",
            "cc6622", "09-20", "10-10",
            new SeasonalEventStage[]
            {
                new(22, 1.10f, 1.10f, new[] { "rapid_assault" }, new SeasonalEventReward("gold", amount: 250)),
                new(42, 1.18f, 1.15f, new[] { "mirror_pressure" }, new SeasonalEventReward("food", amount: 6)),
                new(57, 1.25f, 1.20f, new[] { "lich_graveyard" }, new SeasonalEventReward("gold", amount: 500)),
                new(76, 1.30f, 1.25f, new[] { "tunnel_invasion" }, new SeasonalEventReward("sigils", amount: 2)),
                new(91, 1.40f, 1.30f, new[] { "mirror_pressure", "lich_graveyard" }, new SeasonalEventReward("relic", "relic_moonfire_talisman")),
            },
            new SeasonalEventMilestone[]
            {
                new(2, new SeasonalEventReward("gold", amount: 400), "Track the first beast"),
                new(4, new SeasonalEventReward("sigils", amount: 2), "Corner the pack alpha"),
                new(5, new SeasonalEventReward("relic", "relic_moonfire_talisman"), "Finish the moon hunt"),
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

    /// <summary>The event that opens next after this day, and the day it opens.</summary>
    public static (SeasonalEventDefinition Event, DateTime Start) GetNextEvent(DateTime utcNow) =>
        Definitions.Select(def => (def, def.NextStart(utcNow))).OrderBy(next => next.Item2).First();

    public static SeasonalEventDefinition GetActiveEvent(DateTime utcNow)
    {
        foreach (var def in Definitions)
        {
            if (def.IsActive(utcNow))
            {
                return def;
            }
        }

        return null;
    }
}
