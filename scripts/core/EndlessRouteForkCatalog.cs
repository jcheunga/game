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
    public const string AmbushRavineId = "ambush_ravine";
    public const string RitualGroundsId = "ritual_grounds";
    public const string SiegeCampId = "siege_camp";
    public const string PlagueWindsId = "plague_winds";
    public const string NecromancersTombId = "necromancers_tomb";

    private static readonly EndlessRouteForkDefinition[] Forks =
    {
        new(
            MainlinePushId,
            "Mainline Push",
            "Faster surge cadence with more ghouls and blight casters. Gold payout +10%."),
        new(
            ScavengeDetourId,
            "Supply Detour",
            "Slightly slower surges with heavier dead and richer supply lanes. Gold payout +20%."),
        new(
            FortifiedBlockId,
            "Fortified Block",
            "More controlled pressure and a stronger ward line. Gold payout -10%."),
        new(
            AmbushRavineId,
            "Ambush Ravine",
            "Isolated enemy groups with tunnelers flanking from the rear. High risk, high reward. Gold payout +15%."),
        new(
            RitualGroundsId,
            "Ritual Grounds",
            "Lich and mirror knight pressure drains your momentum. Courage generation reduced. Gold payout +10%."),
        new(
            SiegeCampId,
            "Siege Camp",
            "Siege towers roll in alongside heavy brute formations. Brace the wagon. Gold payout +20%."),
        new(
            PlagueWindsId,
            "Plague Winds",
            "Rot hulks and bloater swarms. Explosive death bursts everywhere. Gold payout +12%."),
        new(
            NecromancersTombId,
            "Necromancer's Tomb",
            "Constant undead spam with lich reanimation. Overwhelming numbers. Gold payout +25%.")
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
