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
    public const string RelicForgeId = "relic_forge";
    public const string CorpseHoardId = "corpse_hoard";
    public const string BerserkerBloodId = "berserker_blood";
    public const string ShieldFormationId = "shield_formation";
    public const string SplitterBaneId = "splitter_bane";

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
            "Endless run gold rewards are increased by 25% when the run ends."),
        new(
            RelicForgeId,
            "Relic Forge",
            "Grant a random relic at this checkpoint."),
        new(
            CorpseHoardId,
            "Corpse Hoard",
            "Necromancer skeletons gain +30% health for the rest of the run."),
        new(
            BerserkerBloodId,
            "Berserker Blood",
            "All units gain +0.5% damage for every 1% missing health."),
        new(
            ShieldFormationId,
            "Shield Formation",
            "War wagon gains +20% hull armor for the rest of the run."),
        new(
            SplitterBaneId,
            "Splitter Bane",
            "Gold rewards from this run increase by 12%.")
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
