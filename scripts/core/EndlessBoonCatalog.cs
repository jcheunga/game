using System;

public sealed class EndlessBoonDefinition
{
    public EndlessBoonDefinition(string id, string title, string summary)
    {
        Id = id;
        Title = title;
        Summary = summary;
    }

    public string Id { get; }
    public string Title { get; }
    public string Summary { get; }
}

public static class EndlessBoonCatalog
{
    public const string SurplusCourageId = "surplus_courage";
    public const string ReinforcedBusId = "reinforced_bus";
    public const string SalvageCacheId = "salvage_cache";

    private static readonly EndlessBoonDefinition[] Boons =
    {
        new(
            SurplusCourageId,
            "Surplus Courage",
            "Start the run with +25 courage so the opening lane stabilizes faster."),
        new(
            ReinforcedBusId,
            "Reinforced Wagon",
            "The caravan starts with +20% war wagon hull for longer survival against spikes."),
        new(
            SalvageCacheId,
            "Supply Cache",
            "Endless run gold rewards are increased by 25% when the run ends.")
    };

    public static EndlessBoonDefinition[] GetAll()
    {
        return Boons;
    }

    public static EndlessBoonDefinition Get(string id)
    {
        var normalizedId = Normalize(id);
        for (var i = 0; i < Boons.Length; i++)
        {
            if (Boons[i].Id.Equals(normalizedId, StringComparison.OrdinalIgnoreCase))
            {
                return Boons[i];
            }
        }

        return Boons[0];
    }

    public static string Normalize(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return SurplusCourageId;
        }

        var normalizedId = id.Trim().ToLowerInvariant();
        for (var i = 0; i < Boons.Length; i++)
        {
            if (Boons[i].Id.Equals(normalizedId, StringComparison.OrdinalIgnoreCase))
            {
                return Boons[i].Id;
            }
        }

        return SurplusCourageId;
    }
}
