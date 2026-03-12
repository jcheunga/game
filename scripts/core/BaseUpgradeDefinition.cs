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

    private static readonly BaseUpgradeDefinition[] Upgrades =
    {
        new(
            HullPlatingId,
            "Hull Plating",
            "Increase bus hull by 12% per level in every battle.",
            5),
        new(
            PantryId,
            "Convoy Pantry",
            "Increase max courage by 6 and courage gain by 6% per level.",
            5),
        new(
            DispatchConsoleId,
            "Dispatch Console",
            "Reduce squad deploy cooldowns by 6% per level.",
            5),
        new(
            SignalRelayId,
            "Signal Relay",
            "Harden convoy comms against jammers. Shorten signal jams and blunt their courage/cooldown penalties.",
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
