using System;

public sealed class BaseUpgradeDefinition
{
    public BaseUpgradeDefinition(string id, string title, string summary, int maxLevel)
    {
        Id = id;
        Title = title;
        Summary = summary;
        MaxLevel = maxLevel;
    }

    public string Id { get; }
    public string Title { get; }
    public string Summary { get; }
    public int MaxLevel { get; }
}

public static class BaseUpgradeCatalog
{
    public const string HullPlatingId = "hull_plating";
    public const string PantryId = "convoy_pantry";
    public const string DispatchConsoleId = "dispatch_console";
    public const string SignalRelayId = "signal_relay";
    public const string RelicVaultId = "relic_vault";
    public const string ProjectileWardId = "projectile_ward";
    public const string GateBreakerId = "gate_breaker";
    public const string ArcherCrewId = "wagon_archers";
    public const string BallistaId = "wagon_ballista";
    public const string FirepotId = "wagon_firepot";
    public const string ArrowVolleyId = "wagon_volley";
    public const string EmergencyRepairId = "wagon_emergency_repair";
    public const string ReinforcedArmorId = "wagon_armor";

    private static readonly BaseUpgradeDefinition[] Upgrades =
    {
        new(ArcherCrewId, "Wagon Archer Crew", "Your starting ranged weapon. Prioritizes fast raiders and sappers; each level adds damage and range.", 5),
        new(BallistaId, "Mounted Ballista", "Install a second weapon that prioritizes armored enemies and bosses, dealing 50% bonus damage to them. Fires alongside the archers.", 5),
        new(FirepotId, "Firepot Launcher", "Install a third weapon that targets clustered enemies and burns the group on impact. Fires alongside other mounts.", 5),
        new(ArrowVolleyId, "Arrow Volley", "Automatic skill: fires at up to three enemies in archer range when ready. Upgrades shorten recovery.", 5),
        new(EmergencyRepairId, "Emergency Repairs", "Automatic skill: repair the wagon once per battle when hull falls to 40% or less. Upgrades restore more hull.", 5),
        new(ReinforcedArmorId, "Reinforced Axles", "Reduce damage from enemy attacks against the wagon by 6% per level. Stacks with hull plating.", 5),
        new(
            HullPlatingId,
            "War Wagon Plating",
            "Increase war wagon hull by 12% per level in every battle.",
            5),
        new(
            PantryId,
            "Caravan Stores",
            "Increase max courage by 6 and courage gain by 6% per level.",
            5),
        new(
            DispatchConsoleId,
            "March Drum",
            "Reduce troop and spell card cooldowns by 6% per level.",
            5),
        new(
            SignalRelayId,
            "Rune Beacon",
            "Harden caravan wards against hexers. Shorten signal jams and blunt their courage and cooldown penalties.",
            5),
        new(
            RelicVaultId,
            "Relic Repository",
            "Improve boss relic rarity: each level adds 4.8 percentage points to epic odds and 2.4 to rare odds.",
            5),
        new(
            ProjectileWardId,
            "Arrow Ward",
            "Reduce incoming projectile damage by 8% per level.",
            5),
        new(
            GateBreakerId,
            "Siege Hammer",
            "Increase base damage against the gatehouse by 8% per level.",
            5)
    };

    public static BaseUpgradeDefinition[] GetAll()
    {
        return Upgrades;
    }

    public static BaseUpgradeDefinition Get(string upgradeId)
    {
        for (var i = 0; i < Upgrades.Length; i++)
        {
            if (Upgrades[i].Id.Equals(upgradeId, StringComparison.OrdinalIgnoreCase))
            {
                return Upgrades[i];
            }
        }

        throw new InvalidOperationException($"Unknown base upgrade '{upgradeId}'.");
    }
}
