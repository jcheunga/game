using System;

public sealed class EndlessBossCheckpointDefinition
{
    public EndlessBossCheckpointDefinition(
        string routeId,
        string title,
        string summary,
        string rewardSummary,
        string clearStatus,
        int rewardGold,
        int rewardFood,
        StageWaveEntryDefinition[] escortEntries)
    {
        RouteId = routeId;
        Title = title;
        Summary = summary;
        RewardSummary = rewardSummary;
        ClearStatus = clearStatus;
        RewardGold = rewardGold;
        RewardFood = rewardFood;
        EscortEntries = escortEntries ?? Array.Empty<StageWaveEntryDefinition>();
    }

    public string RouteId { get; }
    public string Title { get; }
    public string Summary { get; }
    public string RewardSummary { get; }
    public string ClearStatus { get; }
    public int RewardGold { get; }
    public int RewardFood { get; }
    public StageWaveEntryDefinition[] EscortEntries { get; }
}

public static class EndlessBossCheckpointCatalog
{
    public const int BossCheckpointInterval = 15;

    private static readonly EndlessBossCheckpointDefinition[] Definitions =
    {
        new(
            RouteCatalog.CityId,
            "Signal Breaker",
            "An Overlord charge backed by runners and spitters tries to crack the expressway hold.",
            "Reward: bank +32 gold, +1 food, and surge +18 courage.",
            "The convoy broke the expressway warlord and regained tempo for the next stretch.",
            32,
            1,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemyRunnerId, Count = 3 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySpitterId, Count = 1 }
            }),
        new(
            RouteCatalog.HarborId,
            "Drowned Crown",
            "A harbor Overlord leans behind bloater and crusher pressure to collapse the choke point.",
            "Reward: bank +36 gold, +1 food, and repair 6% bus hull.",
            "The harbor crown collapsed and the convoy patched the bus before the next push.",
            36,
            1,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemyBloaterId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyCrusherId, Count = 1 }
            }),
        new(
            RouteCatalog.FoundryId,
            "Furnace Tyrant",
            "A furnace Overlord arrives with splitter nests and saboteur escorts through the smelter lanes.",
            "Reward: bank +38 gold, +1 food, and trim deck cooldowns by 1.0s.",
            "The furnace tyrant fell and the convoy reset its deploy rhythm before the next surge.",
            38,
            1,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemySplitterId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySaboteurId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyHowlerId, Count = 1 }
            }),
        new(
            RouteCatalog.QuarantineId,
            "Containment Breach",
            "A blacksite Overlord breaches containment under howler, spitter, and saboteur support.",
            "Reward: bank +34 gold, +1 food, and call a defender reinforcement.",
            "The containment breach was sealed and militia support stabilized the next block.",
            34,
            1,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemySpitterId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyHowlerId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySaboteurId, Count = 1 }
            })
    };

    public static EndlessBossCheckpointDefinition GetForRoute(string routeId)
    {
        var normalizedRouteId = RouteCatalog.Normalize(routeId);
        for (var i = 0; i < Definitions.Length; i++)
        {
            if (Definitions[i].RouteId.Equals(normalizedRouteId, StringComparison.OrdinalIgnoreCase))
            {
                return Definitions[i];
            }
        }

        return Definitions[0];
    }

    public static bool IsBossCheckpointWave(int waveNumber)
    {
        return waveNumber > 0 && waveNumber % BossCheckpointInterval == 0;
    }

    public static int GetNextBossCheckpointWave(int waveNumber)
    {
        if (waveNumber < 0)
        {
            return BossCheckpointInterval;
        }

        return ((waveNumber / BossCheckpointInterval) + 1) * BossCheckpointInterval;
    }
}
