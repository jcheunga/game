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
    public const string RelicRecoveryId = "relic_recovery";
    public const string RitualDisruptionId = "ritual_disruption";
    public const string ConvoyEscortId = "convoy_escort";

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
            "Supply Cache",
            "Secure the quartermaster cache with allied presence while keeping undead out of the pickup radius.",
            "Reward: bank +22 gold, +1 food, and a light war wagon repair.",
            "Tradeoff: the hauling delay opens one extra enemy slot until checkpoint.",
            "Risk: lose 16 projected gold, -1 food, and take war wagon damage.",
            "secure_cache",
            68f,
            7.5f),
        new(
            SafehouseRescueId,
            EndlessRouteForkCatalog.FortifiedBlockId,
            "Safehouse Rescue",
            "Keep the rescue block clear long enough for survivors to reach the caravan.",
            "Reward: defender reinforcement, cooldown trim, and a repair burst.",
            "Tradeoff: evac load cuts courage generation until checkpoint.",
            "Risk: take heavy war wagon damage and add +1.0s card recovery.",
            "rescue_hold",
            84f,
            8f),
        new(
            RelicRecoveryId,
            EndlessRouteForkCatalog.AmbushRavineId,
            "Relic Recovery",
            "Secure a buried relic before enemies destroy the dig site.",
            "Reward: grant a random common relic.",
            "Tradeoff: dig site defense splits your attention from the main push.",
            "Risk: lose projected gold and the relic is destroyed.",
            "site_defense",
            72f,
            7f),
        new(
            RitualDisruptionId,
            EndlessRouteForkCatalog.RitualGroundsId,
            "Ritual Disruption",
            "Interrupt an enemy ritual channel before it completes.",
            "Reward: temporary +15% damage buff for 30s.",
            "Tradeoff: rushing the ritual ground pulls aggro from nearby packs.",
            "Risk: enemy health +10% for the segment.",
            "channel_interrupt",
            78f,
            6f),
        new(
            ConvoyEscortId,
            EndlessRouteForkCatalog.SiegeCampId,
            "Convoy Escort",
            "Escort a supply wagon through the danger zone.",
            "Reward: restore 20% war wagon hull and +1 food bonus.",
            "Tradeoff: escort pace slows your advance and extends exposure.",
            "Risk: lose 10% war wagon hull.",
            "escort_guard",
            80f,
            9f),
        new(
            RelicRecoveryId + "_plague",
            EndlessRouteForkCatalog.PlagueWindsId,
            "Relic Recovery",
            "Secure a buried relic before enemies destroy the dig site.",
            "Reward: grant a random common relic.",
            "Tradeoff: dig site defense splits your attention from the main push.",
            "Risk: lose projected gold and the relic is destroyed.",
            "site_defense",
            72f,
            7f),
        new(
            RitualDisruptionId + "_necro",
            EndlessRouteForkCatalog.NecromancersTombId,
            "Ritual Disruption",
            "Interrupt an enemy ritual channel before it completes.",
            "Reward: temporary +15% damage buff for 30s.",
            "Tradeoff: rushing the ritual ground pulls aggro from nearby packs.",
            "Risk: enemy health +10% for the segment.",
            "channel_interrupt",
            78f,
            6f)
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
