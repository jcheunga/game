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

        // New systems (4)
        new("first_forge", "Relic Smith", "Forge or fuse your first relic.", "collection"),
        new("first_promotion", "Elite Vanguard", "Promote a unit to elite rank.", "mastery"),
        new("expedition_10", "Expedition Veteran", "Complete 10 expeditions.", "mastery"),
        new("event_complete", "Festival Champion", "Clear all stages in a seasonal event.", "campaign"),

        // Social, Competitive, and Knowledge (8)
        new("codex_10", "Lore Seeker", "Discover 10 codex entries.", "collection"),
        new("codex_complete", "Archivist", "Discover all codex entries.", "collection"),
        new("first_talent", "Scholar", "Unlock your first skill tree node.", "mastery"),
        new("talent_master", "Talent Master", "Max out a unit's skill tree.", "mastery"),
        new("arena_first_win", "Challenger", "Win your first arena battle.", "combat"),
        new("arena_10_wins", "Arena Champion", "Win 10 arena battles.", "combat"),
        new("arena_gold_tier", "Gold Rank", "Reach Gold tier in the arena.", "mastery"),
        new("guild_join", "Warband Recruit", "Join a guild.", "mastery"),

        // Endgame content (4)
        new("hard_mode_10", "Hardened Veteran", "Clear 10 hard mode stages.", "campaign"),
        new("hard_mode_complete", "Iron Legend", "Clear all 50 hard mode stages.", "campaign"),
        new("first_enchantment", "Enchanter", "Apply your first enchantment.", "collection"),
        new("raid_contributor", "Raid Striker", "Contribute to 3 weekly raids.", "mastery"),

        // Daily engagement and progression depth (7)
        new("bounty_streak_7", "Bounty Hunter", "Complete bounties on 7 different days.", "mastery"),
        new("tower_25", "Tower Climber", "Reach floor 25 in the Challenge Tower.", "campaign"),
        new("tower_50", "Tower Conqueror", "Reach floor 50 in the Challenge Tower.", "campaign"),
        new("tower_100", "Tower Pinnacle", "Conquer all 100 floors of the Challenge Tower.", "campaign"),
        new("first_mastery", "Combat Veteran", "Reach Adept mastery on any unit.", "mastery"),
        new("grand_master", "Grand Master", "Reach Grand Master mastery on any unit.", "mastery"),
        new("gift_sent", "Generous Soul", "Send a gift to a friend.", "mastery"),

        // Retention polish (1)
        new("first_skin", "Wagon Artisan", "Equip a non-default war wagon skin.", "collection"),

        // Monetization and collection depth (2)
        new("first_awakening", "Star Forger", "Awaken a unit to 2 stars.", "mastery"),
        new("collector_complete", "Grand Collector", "Claim all 100% collection milestones.", "collection"),

        // Gameplay variety (1)
        new("mutator_5", "Rule Breaker", "Complete 5 battles with mutators active.", "combat"),
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
