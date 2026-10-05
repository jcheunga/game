using System.Collections.Generic;
using System.Linq;

public sealed record CampaignMilestone(int Stage, string RelicId = "", string UnitId = "", int UnitLevel = 1, int Tomes = 0);

public static class CampaignProgressionCatalog
{
    private static readonly CampaignMilestone[] Milestones =
    {
        // Every zone boss grants a relic; unit contracts arrive a zone or so after each unit unlocks.
        new(10, "relic_iron_pendant"), new(20, "relic_sharpened_edge"), new(23, UnitId: "player_marksman", UnitLevel: 3, Tomes: 1),
        new(30, "relic_battle_drum"), new(33, UnitId: "player_grenadier", UnitLevel: 3, Tomes: 2),
        new(38, UnitId: "player_breacher", UnitLevel: 3, Tomes: 2), new(40, "relic_war_brand"),
        new(45, UnitId: "player_coordinator", UnitLevel: 3, Tomes: 2), new(50, "relic_guardian_shield"),
        new(53, UnitId: "player_banner", UnitLevel: 4, Tomes: 3), new(58, UnitId: "player_lantern_guard", UnitLevel: 4, Tomes: 3),
        new(60, "relic_sages_ring"), new(68, UnitId: "player_ballista", UnitLevel: 4, Tomes: 3), new(70, "relic_crown_of_valor"),
        new(78, UnitId: "player_stormcaller", UnitLevel: 4, Tomes: 3), new(80, "relic_blade_of_ruin"),
        new(90, "relic_frostbound_crown"), new(100, "relic_immortal_wreath")
    };

    public static IReadOnlyList<CampaignMilestone> GetAll() => Milestones;
    public static CampaignMilestone Get(int stage) => Milestones.FirstOrDefault(x => x.Stage == stage);
    public static int SuggestedLevel(int stage) => stage < 10 ? 1 : stage < 21 ? 2 : stage < 35 ? 3 : stage < 81 ? 4 : 5;
    public static int MasteryShards(int stage) => GameData.GetStage(stage).Waves
        .Any(w => w.Entries.Any(e => GameData.GetUnit(e.UnitId).VisualClass == "boss")) ? 3 : 1;

    public static string Preparation(int stage)
    {
        var tactic = stage switch
        {
            30 => "Bring protected ranged damage and a Mounted Ballista. Keep reinforcements clear of the furnace warnings.",
            60 => "Lantern Guard, Ballista Crew and Mage counter the vault. Save courage for siege reinforcements.",
            20 => "Keep ranged troops behind a strong front line and develop the wagon's weapon mounts before the Tidemaster arrives.",
            40 => "Battle Monk recovery and area damage hold the plague court. Save your command for the boss phase.",
            _ when stage < 21 => "Protect ranged troops with a frontline and save courage between waves.",
            _ when stage < 61 => "Invest in your core squad first; a useful relic or counter unit can replace another level.",
            _ => "Match your squad to the enemy roles. Keep reserves for dives and avoid marked hazards."
        };
        return $"Suggested core: level {SuggestedLevel(stage)}. Lower levels remain a challenge option.\n{tactic}";
    }

    // Raid and tower identity is preserved; ordinary bosses only roll campaign relics.
    public static bool IsCampaignRelic(string id) => !id.StartsWith("relic_raid_") && !id.StartsWith("relic_tower_");
}
