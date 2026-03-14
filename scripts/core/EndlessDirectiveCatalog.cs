using System;

public sealed class EndlessDirectiveDefinition
{
    public EndlessDirectiveDefinition(
        string id,
        string routeForkId,
        string title,
        string summary,
        string rewardSummary,
        string type,
        int targetCount,
        float targetRatio)
    {
        Id = id;
        RouteForkId = routeForkId;
        Title = title;
        Summary = summary;
        RewardSummary = rewardSummary;
        Type = type;
        TargetCount = targetCount;
        TargetRatio = targetRatio;
    }

    public string Id { get; }
    public string RouteForkId { get; }
    public string Title { get; }
    public string Summary { get; }
    public string RewardSummary { get; }
    public string Type { get; }
    public int TargetCount { get; }
    public float TargetRatio { get; }
}

public static class EndlessDirectiveCatalog
{
    public const string BreakthroughDirectiveId = "breakthrough";
    public const string SalvageSweepDirectiveId = "salvage_sweep";
    public const string HoldLineDirectiveId = "hold_line";

    private static readonly EndlessDirectiveDefinition[] Directives =
    {
        new(
            BreakthroughDirectiveId,
            EndlessRouteForkCatalog.MainlinePushId,
            "Breakthrough Directive",
            "Defeat a surge quota before the next checkpoint to keep the caravan on the redline route.",
            "Reward: +16 courage and -1.5s cooldowns across the deck.",
            "enemy_defeats",
            10,
            0f),
        new(
            SalvageSweepDirectiveId,
            EndlessRouteForkCatalog.ScavengeDetourId,
            "Supply Sweep",
            "Reach the next checkpoint with a tight deployment count to secure the richest quartermaster caches.",
            "Reward: bank +28 gold, +1 food, and a quick war wagon repair.",
            "deploy_limit",
            4,
            0f),
        new(
            HoldLineDirectiveId,
            EndlessRouteForkCatalog.FortifiedBlockId,
            "Hold The Line",
            "Keep the war wagon hull above the defense threshold until the next checkpoint reaches the safehouse ward.",
            "Reward: militia reinforcement, cooldown trim, and a repair burst.",
            "bus_hull_ratio",
            0,
            0.7f)
    };

    public static EndlessDirectiveDefinition GetForRouteFork(string routeForkId)
    {
        var normalizedForkId = EndlessRouteForkCatalog.Normalize(routeForkId);
        for (var i = 0; i < Directives.Length; i++)
        {
            if (Directives[i].RouteForkId.Equals(normalizedForkId, StringComparison.OrdinalIgnoreCase))
            {
                return Directives[i];
            }
        }

        return Directives[0];
    }
}
