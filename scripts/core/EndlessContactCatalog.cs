using System;

public sealed class EndlessContactDefinition
{
    public EndlessContactDefinition(
        string id,
        string routeForkId,
        string title,
        string summary,
        string rewardSummary,
        string tradeoffSummary,
        string penaltySummary,
        string type,
        float radius,
        float targetSeconds)
    {
        Id = id;
        RouteForkId = routeForkId;
        Title = title;
        Summary = summary;
        RewardSummary = rewardSummary;
        TradeoffSummary = tradeoffSummary;
        PenaltySummary = penaltySummary;
        Type = type;
        Radius = radius;
        TargetSeconds = targetSeconds;
    }

    public string Id { get; }
    public string RouteForkId { get; }
    public string Title { get; }
    public string Summary { get; }
    public string RewardSummary { get; }
    public string TradeoffSummary { get; }
    public string PenaltySummary { get; }
    public string Type { get; }
    public float Radius { get; }
    public float TargetSeconds { get; }
}

public static class EndlessContactCatalog
{
    public const string RelaySignalId = "relay_signal";
    public const string SalvageCacheId = "salvage_cache";
    public const string SafehouseRescueId = "safehouse_rescue";

    private static readonly EndlessContactDefinition[] Contacts =
    {
        new(
            RelaySignalId,
            EndlessRouteForkCatalog.MainlinePushId,
            "Relay Signal",
            "Push units into the forward relay zone and hold it long enough to keep the route intel online.",
            "Reward: +14 courage and -1.2s cooldowns across the deck.",
            "Tradeoff: route intel forces a sprint and pulls the next surge closer.",
            "Risk: lose 14 courage and add +1.8s card recovery across the deck.",
            "forward_presence",
            74f,
            6.5f),
        new(
            SalvageCacheId,
            EndlessRouteForkCatalog.ScavengeDetourId,
            "Salvage Cache",
            "Secure the wreck cache with allied presence while keeping infected out of the pickup radius.",
            "Reward: bank +22 gold, +1 food, and a light convoy repair.",
            "Tradeoff: the hauling delay opens one extra enemy slot until checkpoint.",
            "Risk: lose 16 projected gold, -1 food, and take convoy damage.",
            "secure_cache",
            68f,
            7.5f),
        new(
            SafehouseRescueId,
            EndlessRouteForkCatalog.FortifiedBlockId,
            "Safehouse Rescue",
            "Keep the rescue block clear long enough for survivors to reach the convoy.",
            "Reward: defender reinforcement, cooldown trim, and a repair burst.",
            "Tradeoff: evac load cuts courage generation until checkpoint.",
            "Risk: take heavy bus damage and add +1.0s card recovery.",
            "rescue_hold",
            84f,
            8f)
    };

    public static EndlessContactDefinition GetForRouteFork(string routeForkId)
    {
        var normalizedForkId = EndlessRouteForkCatalog.Normalize(routeForkId);
        for (var i = 0; i < Contacts.Length; i++)
        {
            if (Contacts[i].RouteForkId.Equals(normalizedForkId, StringComparison.OrdinalIgnoreCase))
            {
                return Contacts[i];
            }
        }

        return Contacts[0];
    }
}
