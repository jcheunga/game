using System;

public sealed class EndlessBossCheckpointDefinition
{
    public EndlessBossCheckpointDefinition(
        string routeId,
        string bossId,
        string title,
        string summary,
        string rewardSummary,
        string clearStatus,
        int rewardGold,
        int rewardFood,
        StageWaveEntryDefinition[] escortEntries)
    {
        RouteId = routeId;
        BossId = bossId;
        Title = title;
        Summary = summary;
        RewardSummary = rewardSummary;
        ClearStatus = clearStatus;
        RewardGold = rewardGold;
        RewardFood = rewardFood;
        EscortEntries = escortEntries ?? Array.Empty<StageWaveEntryDefinition>();
    }

    public string RouteId { get; }
    public string BossId { get; }
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

    /// <summary>
    /// Route-specific definitions used for the first boss checkpoint (wave 15) and for UI previews.
    /// Each route keeps its original Grave Lord encounter as the opening boss.
    /// </summary>
    private static readonly EndlessBossCheckpointDefinition[] RouteDefinitions =
    {
        new(
            RouteCatalog.CityId,
            GameData.EnemyBossId,
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
            GameData.EnemyBossId,
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
            GameData.EnemyBossId,
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
            GameData.EnemyBossId,
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
            GameData.EnemyBossId,
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
            GameData.EnemyBossId,
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
            GameData.EnemyBossId,
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
            GameData.EnemyBossId,
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
            GameData.EnemyBossId,
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
            GameData.EnemyBossId,
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

    /// <summary>
    /// Wave-tiered boss rotation for endless mode. Bosses escalate in difficulty as the
    /// player survives deeper. Two bosses share each mid-tier slot and alternate based on
    /// the checkpoint index (even/odd) to provide variety across runs on the same route.
    ///
    /// Tier 1 (wave 15):  Grave Lord          - easy, introductory
    /// Tier 2 (wave 30):  Tidecaller / Iron Warden - mid
    /// Tier 3 (wave 45):  Plague Archon / Thornwall Chieftain - mid-hard
    /// Tier 4 (wave 60):  Bone Pontiff / Steppe Warlord - hard
    /// Tier 5 (wave 75):  Mire Behemoth / Gloamwood Witch - very hard
    /// Tier 6 (wave 90+): Dread Sovereign      - final boss, repeats
    /// </summary>
    private static readonly EndlessBossCheckpointDefinition[] TieredBossRotation =
    {
        // Tier 1 - Wave 15: Grave Lord (easy)
        new(
            "",
            GameData.EnemyBossId,
            "Grave Lord Vanguard",
            "The Grave Lord marches with a shambling escort of ghouls and blight casters.",
            "Reward: bank +32 gold, +1 food, and surge +18 courage.",
            "The Grave Lord fell and the caravan regained tempo for the next stretch.",
            32,
            1,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemyRunnerId, Count = 3 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySpitterId, Count = 1 }
            }),
        // Tier 2A - Wave 30 (even index): Tidecaller
        new(
            "",
            GameData.EnemyBossDocksId,
            "Tidecaller Surge",
            "The Tidecaller rises from the depths behind bloater hulks and spitter salvos.",
            "Reward: bank +38 gold, +1 food, and repair 6% war wagon hull.",
            "The Tidecaller sank and the caravan steadied for the next push.",
            38,
            1,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemyBloaterId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySpitterId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyCrusherId, Count = 1 }
            }),
        // Tier 2B - Wave 30 (odd index): Iron Warden
        new(
            "",
            GameData.EnemyBossForgeId,
            "Iron Warden Assault",
            "The Iron Warden advances through furnace smoke behind brute escorts and saboteur sappers.",
            "Reward: bank +38 gold, +1 food, and trim deck cooldowns by 1.0s.",
            "The Iron Warden crumbled and the caravan reset its deploy rhythm.",
            38,
            1,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemyBruteId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySaboteurId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySplitterId, Count = 1 }
            }),
        // Tier 3A - Wave 45 (even index): Plague Archon
        new(
            "",
            GameData.EnemyBossWardId,
            "Plague Archon Incursion",
            "The Plague Archon breaches the line under a toxic veil of spitters, howlers, and jammers.",
            "Reward: bank +42 gold, +1 food, and call a defender reinforcement.",
            "The Plague Archon was driven back and the caravan stabilized the next block.",
            42,
            1,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemySpitterId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyHowlerId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyJammerId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySaboteurId, Count = 1 }
            }),
        // Tier 3B - Wave 45 (odd index): Thornwall Chieftain
        new(
            "",
            GameData.EnemyBossPassId,
            "Thornwall Chieftain Charge",
            "The Thornwall Chieftain leads a howler-horn stampede with crusher and saboteur flankers through the pass.",
            "Reward: bank +42 gold, +1 food, and call a crossbow reinforcement.",
            "The Thornwall Chieftain fell and the high watch fired for the caravan's next climb.",
            42,
            1,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemyRunnerId, Count = 3 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyHowlerId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyCrusherId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySaboteurId, Count = 1 }
            }),
        // Tier 4A - Wave 60 (even index): Bone Pontiff
        new(
            "",
            GameData.EnemyBossBasilicaId,
            "Bone Pontiff Procession",
            "The Bone Pontiff advances behind hexer acolytes, relic-guard crushers, and split-brood escorts.",
            "Reward: bank +46 gold, +2 food, and call a battle monk reinforcement.",
            "The Bone Pontiff was shattered and the caravan seized the choir line.",
            46,
            2,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemySplitterId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyJammerId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyCrusherId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySpitterId, Count = 1 }
            }),
        // Tier 4B - Wave 60 (odd index): Steppe Warlord
        new(
            "",
            GameData.EnemyBossSteppeId,
            "Steppe Warlord Raid",
            "The Steppe Warlord gallops in with howler riders, brute vanguards, and saboteur outriders.",
            "Reward: bank +46 gold, +2 food, and call a cavalry reinforcement.",
            "The Steppe Warlord broke under the caravan line and the march resumed.",
            46,
            2,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemyRunnerId, Count = 3 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyHowlerId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyBruteId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySaboteurId, Count = 1 }
            }),
        // Tier 5A - Wave 75 (even index): Mire Behemoth
        new(
            "",
            GameData.EnemyBossMireId,
            "Mire Behemoth Eruption",
            "The Mire Behemoth pushes through plague mist behind rot hulks, split-brood swarms, and crusher escorts.",
            "Reward: bank +50 gold, +2 food, and call a siege engineer reinforcement.",
            "The Mire Behemoth sank and the caravan steadied its axle through the next stretch.",
            50,
            2,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemyBloaterId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySplitterId, Count = 3 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyCrusherId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySpitterId, Count = 1 }
            }),
        // Tier 5B - Wave 75 (odd index): Gloamwood Witch
        new(
            "",
            GameData.EnemyBossVergeId,
            "Gloamwood Witch Ambush",
            "The Gloamwood Witch strikes from the thorn canopy behind hexer packs, howler ambushers, and saboteur traps.",
            "Reward: bank +50 gold, +2 food, and call a mage reinforcement.",
            "The Gloamwood Witch fell and the caravan burned a safe line through the next grove.",
            50,
            2,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemyJammerId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyHowlerId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySaboteurId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyRunnerId, Count = 2 }
            }),
        // Tier 6 - Wave 90+: Dread Sovereign (repeats)
        new(
            "",
            GameData.EnemyBossCitadelId,
            "Dread Sovereign Onslaught",
            "The Dread Sovereign leads the final host: heralds, hexers, crushers, and breach sappers in a full siege column.",
            "Reward: bank +55 gold, +3 food, and call a halberdier reinforcement.",
            "The Dread Sovereign was broken and the caravan forced a breach into the inner keep.",
            55,
            3,
            new[]
            {
                new StageWaveEntryDefinition { UnitId = GameData.EnemySpitterId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyHowlerId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyJammerId, Count = 2 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemySaboteurId, Count = 1 },
                new StageWaveEntryDefinition { UnitId = GameData.EnemyCrusherId, Count = 2 }
            })
    };

    /// <summary>
    /// Returns the route-specific boss checkpoint definition for the first checkpoint (wave 15).
    /// Used by the UI preview and as the default fallback.
    /// </summary>
    public static EndlessBossCheckpointDefinition GetForRoute(string routeId)
    {
        var normalizedRouteId = RouteCatalog.Normalize(routeId);
        for (var i = 0; i < RouteDefinitions.Length; i++)
        {
            if (RouteDefinitions[i].RouteId.Equals(normalizedRouteId, StringComparison.OrdinalIgnoreCase))
            {
                return RouteDefinitions[i];
            }
        }

        return RouteDefinitions[0];
    }

    /// <summary>
    /// Returns the boss checkpoint definition for a given wave number, selecting from the
    /// tiered boss rotation. For wave 15, falls back to the route-specific definition so
    /// each district keeps its unique flavor text and escort pack. For wave 30+, the
    /// tiered rotation picks the district boss matching the current difficulty tier.
    /// At tiers with two boss options, the route ID is used to vary the pick so that
    /// different districts face different bosses at the same checkpoint.
    /// </summary>
    public static EndlessBossCheckpointDefinition GetForWave(int waveNumber, string routeId)
    {
        if (!IsBossCheckpointWave(waveNumber))
        {
            return GetForRoute(routeId);
        }

        var checkpointIndex = waveNumber / BossCheckpointInterval; // 1-based: 1=wave15, 2=wave30, ...

        // Use route length as a simple seed so different districts get different boss variants.
        var normalizedRouteId = RouteCatalog.Normalize(routeId);
        var routeSeed = normalizedRouteId.Length % 2; // 0 or 1

        switch (checkpointIndex)
        {
            case 1:
                // Wave 15: route-specific Grave Lord
                return GetForRoute(routeId);
            case 2:
                // Wave 30: Tidecaller or Iron Warden
                return TieredBossRotation[1 + routeSeed]; // index 1 or 2
            case 3:
                // Wave 45: Plague Archon or Thornwall Chieftain
                return TieredBossRotation[3 + routeSeed]; // index 3 or 4
            case 4:
                // Wave 60: Bone Pontiff or Steppe Warlord
                return TieredBossRotation[5 + routeSeed]; // index 5 or 6
            case 5:
                // Wave 75: Mire Behemoth or Gloamwood Witch
                return TieredBossRotation[7 + routeSeed]; // index 7 or 8
            default:
                // Wave 90+: Dread Sovereign repeats
                return TieredBossRotation[9];
        }
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
