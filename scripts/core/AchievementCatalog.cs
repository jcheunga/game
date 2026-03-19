using System;
using System.Collections.Generic;

public sealed class AchievementDefinition
{
    public string Id;
    public string Title;
    public string Description;
    public string Category; // "campaign", "combat", "endless", "collection", "mastery"

    public AchievementDefinition(string id, string title, string description, string category)
    {
        Id = id;
        Title = title;
        Description = description;
        Category = category;
    }
}

public static class AchievementCatalog
{
    private static readonly AchievementDefinition[] Definitions =
    {
        // Campaign (5)
        new("first_blood", "First Blood", "Complete your first campaign stage.", "campaign"),
        new("district_clear", "District Marshal", "Clear an entire campaign district.", "campaign"),
        new("campaign_complete", "Caravan Commander", "Clear all 10 campaign districts.", "campaign"),
        new("all_stars", "Star Collector", "Earn 3 stars on 25 stages.", "campaign"),
        new("heroic_clear", "Heroic Legend", "Complete 10 heroic directives.", "campaign"),

        // Combat (5)
        new("boss_slayer", "Boss Slayer", "Defeat any district boss.", "combat"),
        new("boss_hunter", "Boss Hunter", "Defeat all 10 district bosses.", "combat"),
        new("no_damage", "Untouchable", "Complete a stage with the war wagon at full health.", "combat"),
        new("speed_clear", "Blitz", "Complete a stage in under 60 seconds.", "combat"),
        new("combo_master", "Combo Master", "Trigger all 6 combo pairs in a single battle.", "combat"),

        // Endless (4)
        new("endless_30", "Survivor", "Reach wave 30 in endless mode.", "endless"),
        new("endless_60", "Endurance", "Reach wave 60 in endless mode.", "endless"),
        new("endless_90", "Unstoppable", "Reach wave 90 in endless mode.", "endless"),
        new("endless_boss", "Gauntlet Runner", "Defeat 5 endless boss checkpoints in a single run.", "endless"),

        // Collection (3)
        new("relic_collector", "Relic Collector", "Own at least 6 relics.", "collection"),
        new("full_armory", "Full Armory", "Own all 12 relics.", "collection"),
        new("full_roster", "Full Roster", "Own all 16 player units.", "collection"),

        // Mastery (3)
        new("max_unit", "Master at Arms", "Upgrade any unit to level 5.", "mastery"),
        new("all_spells", "Arcane Scholar", "Own all 10 spells.", "mastery"),
        new("daily_streak", "Dedicated", "Complete 7 daily challenges.", "mastery"),
    };

    private static readonly Dictionary<string, AchievementDefinition> ById;

    static AchievementCatalog()
    {
        ById = new Dictionary<string, AchievementDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in Definitions)
        {
            ById[definition.Id] = definition;
        }
    }

    public static IReadOnlyList<AchievementDefinition> GetAll() => Definitions;

    public static AchievementDefinition GetById(string id)
    {
        return ById.TryGetValue(id, out var definition) ? definition : null;
    }
}
