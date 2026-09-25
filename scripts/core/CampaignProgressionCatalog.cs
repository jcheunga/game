using System.Collections.Generic;
using System.Linq;

public sealed record CampaignMilestone(int Stage, string RelicId = "", string UnitId = "", int UnitLevel = 1, int Tomes = 0);

public static class CampaignProgressionCatalog
{
    private static readonly CampaignMilestone[] Milestones =
    {
        new(4, "relic_iron_pendant"), new(8, "relic_sharpened_edge"), new(12, "relic_battle_drum"),
        new(16, "relic_war_brand"), new(18, UnitId: "player_marksman", UnitLevel: 3, Tomes: 1),
        new(23, UnitId: "player_coordinator", UnitLevel: 3, Tomes: 2), new(26, "relic_guardian_shield"),
        new(28, UnitId: "player_grenadier", UnitLevel: 3, Tomes: 2), new(31, "relic_sages_ring"),
        new(33, UnitId: "player_breacher", UnitLevel: 3, Tomes: 2),
        new(38, UnitId: "player_banner", UnitLevel: 4, Tomes: 3), new(41, "relic_crown_of_valor"),
        new(43, UnitId: "player_lantern_guard", UnitLevel: 4, Tomes: 3), new(46, "relic_blade_of_ruin"),
        new(47, UnitId: "player_ballista", UnitLevel: 4, Tomes: 3),
        new(53, UnitId: "player_stormcaller", UnitLevel: 4, Tomes: 3),
        new(56, "relic_frostbound_crown"), new(60, "relic_immortal_wreath")
    };

    public static IReadOnlyList<CampaignMilestone> GetAll() => Milestones;
    public static CampaignMilestone Get(int stage) => Milestones.FirstOrDefault(x => x.Stage == stage);
    public static int SuggestedLevel(int stage) => stage < 4 ? 1 : stage < 9 ? 2 : stage < 16 ? 3 : stage < 37 ? 4 : 5;
    public static int MasteryShards(int stage) => GameData.GetStage(stage).Waves
        .Any(w => w.Entries.Any(e => GameData.GetUnit(e.UnitId).VisualClass == "boss")) ? 3 : 1;

    public static string Preparation(int stage)
    {
        var tactic = stage switch
        {
            12 => "Bring protected ranged damage and a Mounted Ballista. Keep reinforcements clear of the furnace warnings.",
            52 => "Lantern Guard, Ballista Crew and Mage counter the vault. Use the clear lanes and save courage for siege reinforcements.",
            58 => "Lantern Guard, Ballista Crew and Stormcaller answer the artillery. Develop the wagon's weapon mounts.",
            60 => "Lantern Guard, Stormcaller and Battle Monk balance area damage and recovery. Save your command for the boss phase.",
            _ when stage < 9 => "Protect ranged troops with a frontline and save courage between waves.",
            _ when stage < 27 => "Invest in your core squad first; a useful relic or counter unit can replace another level.",
            _ => "Match your squad to the enemy roles. Keep reserves for dives and avoid marked hazards."
        };
        return $"Suggested core: level {SuggestedLevel(stage)}. Lower levels remain a challenge option.\n{tactic}";
    }

    // Raid and tower identity is preserved; ordinary bosses only roll campaign relics.
    public static bool IsCampaignRelic(string id) => !id.StartsWith("relic_raid_") && !id.StartsWith("relic_tower_");
}
