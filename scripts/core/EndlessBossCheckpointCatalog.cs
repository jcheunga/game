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
            "Bell-Tower Breaker",
            "A Grave Lord charge backed by ghouls and blight casters tries to crack the road hold.",
            "Reward: bank +32 gold, +1 food, and surge +18 courage.",
            "The road warlord fell and the caravan regained tempo for the next stretch.",
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
            "A Saltwake Grave Lord leans behind rot hulk and bone juggernaut pressure to collapse the choke point.",
            "Reward: bank +36 gold, +1 food, and repair 6% war wagon hull.",
            "The drowned crown collapsed and the caravan patched the war wagon before the next push.",
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
            "A furnace Grave Lord arrives with bone nests and sapper escorts through the smelter lanes.",
            "Reward: bank +38 gold, +1 food, and trim deck cooldowns by 1.0s.",
            "The furnace tyrant fell and the caravan reset its deploy rhythm before the next surge.",
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
            "Vault Breach",
            "An Ashen Ward grave lord breaches the vault under dread herald, blight caster, and sapper support.",
            "Reward: bank +34 gold, +1 food, and call a defender reinforcement.",
            "The vault breach was sealed and militia support stabilized the next block.",
            34,
            1,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemySpitterId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyHowlerId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySaboteurId, Count = 1 }
            }),
        new(
            RouteCatalog.ThornwallId,
            "Frosthorn Breaker",
            "A Thornwall Grave Lord leads howler horns, saboteur raiders, and crusher escorts down through the watch gate.",
            "Reward: bank +40 gold, +1 food, and call a crossbow reinforcement.",
            "The frosthorn breaker fell and the high watch fired for the caravan's next climb.",
            40,
            1,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemyRunnerId, Count = 3 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyHowlerId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySaboteurId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyCrusherId, Count = 1 }
            }),
        new(
            RouteCatalog.BasilicaId,
            "Sepulcher Warden",
            "A basilica Grave Lord advances behind hexers, blight casters, and relic-guard crushers through the altar lane.",
            "Reward: bank +42 gold, +1 food, and call a battle monk reinforcement.",
            "The sepulcher warden fell and the caravan seized the choir line for the next push.",
            42,
            1,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemySplitterId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyJammerId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySpitterId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyCrusherId, Count = 1 }
            }),
        new(
            RouteCatalog.MireId,
            "Plague Ferryman",
            "A mire Grave Lord pushes through plague mist behind rot hulks, blight casters, and split-brood escorts.",
            "Reward: bank +44 gold, +1 food, and call a siege engineer reinforcement.",
            "The plague ferryman sank and the caravan steadied its axle through the next bog pull.",
            44,
            1,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemyBloaterId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySplitterId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySpitterId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyCrusherId, Count = 1 }
            }),
        new(
            RouteCatalog.SteppeId,
            "Ash-Rider Khan",
            "A steppe Grave Lord gallops in behind howler horns, sapper riders, and crusher escorts across the open field.",
            "Reward: bank +46 gold, +1 food, and call a cavalry reinforcement.",
            "The ash-rider khan broke under the caravan line and the march seized the open steppe again.",
            46,
            1,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemyRunnerId, Count = 3 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyHowlerId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySaboteurId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyCrusherId, Count = 1 }
            }),
        new(
            RouteCatalog.GloamwoodId,
            "Thorn Hexlord",
            "A gloamwood Grave Lord advances behind hexers, howler packs, and sapper ambushers through the thorn road.",
            "Reward: bank +48 gold, +1 food, and call a mage reinforcement.",
            "The thorn hexlord fell and the caravan burned a safe line through the next grove.",
            48,
            1,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemyRunnerId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyJammerId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyHowlerId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySaboteurId, Count = 1 }
            }),
        new(
            RouteCatalog.CitadelId,
            "Crownbreaker Marshal",
            "A citadel Grave Lord leads mixed herald, hexer, and breach pressure through the final bridge fort.",
            "Reward: bank +50 gold, +1 food, and call a halberdier reinforcement.",
            "The crownbreaker marshal fell and the caravan forced a breach into the inner keep.",
            50,
            1,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemySpitterId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyHowlerId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyJammerId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySaboteurId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyCrusherId, Count = 1 }
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
