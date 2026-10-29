using System.Collections.Generic;
using System.Linq;

public sealed record CampaignMilestone(int Stage, string RelicId = "", string UnitId = "", int UnitLevel = 1, int Gold = 0);

public static class CampaignProgressionCatalog
{
    private static readonly CampaignMilestone[] Milestones =
    {
        new(1, UnitId: "player_militia", Gold: 25),
        new(2, UnitId: "player_spear", Gold: 25),
        new(4, UnitId: "player_shooter", Gold: 30),
        new(6, UnitId: "player_brawler", Gold: 35),
        new(10, "relic_iron_pendant"),
        new(20, "relic_sharpened_edge"),
        new(30, "relic_battle_drum"),
        new(40, "relic_war_brand"),
        new(50, "relic_guardian_shield"),
        new(60, "relic_sages_ring"),
        new(70, "relic_crown_of_valor"),
        new(80, "relic_blade_of_ruin"),
        new(90, "relic_frostbound_crown"),
        new(100, "relic_immortal_wreath"),
        new(9, UnitId: "player_field_medic", Gold: 40),
        new(22, UnitId: "player_billman", Gold: 40),
        new(42, UnitId: "player_pikeman", Gold: 40),
        new(58, UnitId: "player_quartermaster", Gold: 40),
        new(78, UnitId: "player_warhammer_knight", Gold: 40)
    };

    public static IReadOnlyList<CampaignMilestone> GetAll() => Milestones;
    public static CampaignMilestone Get(int stage) => Milestones.FirstOrDefault(x => x.Stage == stage);
    public static int SuggestedLevel(int stage) => System.Math.Clamp(1 + (stage - 1) / 5, 1, 20);
    public static int MasteryShards(int stage) => GameData.GetStage(stage).Waves
        .Any(w => w.Entries.Any(e => GameData.GetUnit(e.UnitId).VisualClass == "boss")) ? 3 : 1;

    public static string Preparation(int stage)
    {
        var tactic = stage switch
        {
            30 => "Hold a melee line, expose armor with Maceman or Spearman, and protect ranged support from the furnace warnings.",
            60 => "Use a steady melee line, an interrupt or corpse-denial spell, and reserves for the vault reinforcements.",
            20 => "Keep ranged troops behind a strong front line and develop the wagon's weapon mounts before the Tidemaster arrives.",
            40 => "Keep the melee line healthy with cleansing and recovery; avoid standing in marked plague fields.",
            _ when stage < 21 => "Protect ranged troops with a frontline and save courage between waves.",
            _ when stage < 61 => "Invest in your core squad first; a useful relic or counter unit can replace another level.",
            _ => "Match your squad to the enemy roles. Keep reserves for dives and avoid marked hazards."
        };
        return $"Suggested level {SuggestedLevel(stage)}. {tactic}";
    }

    // Raid and tower identity is preserved; ordinary bosses only roll campaign relics.
    public static bool IsCampaignRelic(string id) => !id.StartsWith("relic_raid_") && !id.StartsWith("relic_tower_");
}
