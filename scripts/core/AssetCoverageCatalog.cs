using System;

public static class AssetCoverageCatalog
{
    public static readonly string[] RouteIds =
    {
        "city",
        "harbor",
        "foundry",
        "quarantine",
        "thornwall",
        "basilica",
        "mire",
        "steppe",
        "gloamwood",
        "citadel"
    };

    public static readonly string[] ScreenBackgroundIds =
    {
        "main_menu",
        "map",
        "loadout",
        "shop",
        "cash_shop",
        "endless",
        "multiplayer",
        "lan_race",
        "arena",
        "battle_summary",
        "bounty",
        "codex",
        "event",
        "expedition",
        "forge",
        "friends",
        "guild",
        "leaderboard",
        "login_calendar",
        "profile",
        "raid",
        "season_pass",
        "skill_tree",
        "settings",
        "tower"
    };

    public static readonly string[] StructureIds =
    {
        "war_wagon",
        "gatehouse"
    };

    public static readonly string[] ParticleTextureIds =
    {
        "particle_soft",
        "particle_deploy",
        "particle_smoke",
        "particle_spark",
        "particle_fire",
        "particle_heal",
        "particle_frost",
        "particle_lightning",
        "particle_arcane",
        "particle_stone",
        "particle_trail"
    };

    public static readonly string[] MusicTrackIds =
    {
        "title",
        "campaign",
        "shop",
        "loadout",
        "endless_prep",
        "multiplayer",
        "battle",
        "battle_road",
        "battle_harbor",
        "battle_foundry",
        "battle_quarantine",
        "battle_pass",
        "battle_basilica",
        "battle_mire",
        "battle_steppe",
        "battle_gloamwood",
        "battle_citadel"
    };

    public static readonly string[] SfxCueIds =
    {
        "ui_hover",
        "ui_confirm",
        "scene_change",
        "deploy",
        "impact_light",
        "impact_heavy",
        "bus_hit",
        "barricade_hit",
        "repair",
        "hazard_warning",
        "hazard_strike",
        "victory",
        "defeat",
        "spell_cast",
        "boss_spawn",
        "upgrade_confirm",
        "achievement_unlock",
        "relic_pickup",
        "boss_death",
        "ambience_menu",
        "ambience_battle",
        "ambience_endless",
        "ambience_multiplayer",
        "ambience_shop",
        "ambience_route_road",
        "ambience_route_harbor",
        "ambience_route_foundry",
        "ambience_route_quarantine",
        "ambience_route_thornwall",
        "ambience_route_basilica",
        "ambience_route_mire",
        "ambience_route_steppe",
        "ambience_route_gloamwood",
        "ambience_route_citadel"
    };

    public static readonly string[] RewardIconIds =
    {
        "gold",
        "food",
        "tomes",
        "essence",
        "sigils",
        "shards",
        "relic",
        "season_xp",
        "unit",
        "spell"
    };

    public static readonly string[] MetaIconIds =
    {
        "arena_rating",
        "tower_floor",
        "endless_wave",
        "daily_streak",
        "guild",
        "friends",
        "challenge",
        "members"
    };

    public static string BuildScreenVariantId(string screenId, string variantId)
    {
        if (string.IsNullOrWhiteSpace(screenId))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(variantId))
        {
            return NormalizeId(screenId);
        }

        return $"{NormalizeId(screenId)}_{NormalizeId(variantId)}";
    }

    public static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}
