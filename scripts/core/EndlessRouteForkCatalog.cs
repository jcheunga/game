using System;

public sealed class EndlessRouteForkDefinition
{
    public EndlessRouteForkDefinition(string id, string title, string summary)
    {
        Id = id;
        Title = title;
        Summary = summary;
    }

    public string Id { get; }
    public string Title { get; }
    public string Summary { get; }
}

public static class EndlessRouteForkCatalog
{
    public const string MainlinePushId = "mainline_push";
    public const string ScavengeDetourId = "scavenge_detour";
    public const string FortifiedBlockId = "fortified_block";

    private static readonly EndlessRouteForkDefinition[] Forks =
    {
        new(
            MainlinePushId,
            "Mainline Push",
            "Faster surge cadence with more runners and spitters. Scrap payout +10%."),
        new(
            ScavengeDetourId,
            "Scavenge Detour",
            "Slightly slower surges with heavier infected and richer salvage lanes. Scrap payout +20%."),
        new(
            FortifiedBlockId,
            "Fortified Block",
            "More controlled pressure and a stronger hold point. Scrap payout -10%.")
    };

    public static EndlessRouteForkDefinition[] GetAll()
    {
        return Forks;
    }

    public static EndlessRouteForkDefinition Get(string id)
    {
        var normalizedId = Normalize(id);
        for (var i = 0; i < Forks.Length; i++)
        {
            if (Forks[i].Id.Equals(normalizedId, StringComparison.OrdinalIgnoreCase))
            {
                return Forks[i];
            }
        }

        return Forks[0];
    }

    public static string Normalize(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return MainlinePushId;
        }

        var normalizedId = id.Trim().ToLowerInvariant();
        for (var i = 0; i < Forks.Length; i++)
        {
            if (Forks[i].Id.Equals(normalizedId, StringComparison.OrdinalIgnoreCase))
            {
                return Forks[i].Id;
            }
        }

        return MainlinePushId;
    }
}
