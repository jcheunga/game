using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

public static class GameData
{
    public static readonly string[] PlayerRosterIds =
    {
        PlayerBrawlerId,
        PlayerShooterId,
        PlayerDefenderId,
        PlayerRangerId,
        PlayerRaiderId,
        PlayerMechanicId,
        PlayerMarksmanId,
        PlayerBreacherId,
        PlayerGrenadierId,
        PlayerCoordinatorId
    };

    public static readonly string[] EnemyRosterIds =
    {
        EnemyWalkerId,
        EnemyRunnerId,
        EnemyBloaterId,
        EnemyBruteId,
        EnemySpitterId,
        EnemySplitterId,
        EnemySaboteurId,
        EnemyHowlerId,
        EnemyJammerId,
        EnemyCrusherId,
        EnemyBossId
    };

    public const string PlayerBrawlerId = "player_brawler";
    public const string PlayerShooterId = "player_shooter";
    public const string PlayerDefenderId = "player_defender";
    public const string PlayerRangerId = "player_ranger";
    public const string PlayerRaiderId = "player_raider";
    public const string PlayerMechanicId = "player_mechanic";
    public const string PlayerMarksmanId = "player_marksman";
    public const string PlayerBreacherId = "player_breacher";
    public const string PlayerGrenadierId = "player_grenadier";
    public const string PlayerCoordinatorId = "player_coordinator";
    public const string EnemyWalkerId = "enemy_walker";
    public const string EnemyRunnerId = "enemy_runner";
    public const string EnemyBloaterId = "enemy_bloater";
    public const string EnemyBruteId = "enemy_brute";
    public const string EnemySpitterId = "enemy_spitter";
    public const string EnemySplitterId = "enemy_splitter";
    public const string EnemySaboteurId = "enemy_saboteur";
    public const string EnemyHowlerId = "enemy_howler";
    public const string EnemyJammerId = "enemy_jammer";
    public const string EnemyCrusherId = "enemy_crusher";
    public const string EnemyBossId = "enemy_boss";

    private const string UnitsPath = "res://data/units.json";
    private const string StagesPath = "res://data/stages.json";
    private const string CombatPath = "res://data/combat_config.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static bool _loaded;
    private static StageDefinition[] _stages = Array.Empty<StageDefinition>();
    private static Dictionary<string, UnitDefinition> _units = new();
    private static CombatTuning _combat = new();

    private sealed class UnitCollection
    {
        public List<UnitDefinition> Units { get; set; } = new();
    }

    private sealed class StageCollection
    {
        public List<StageDefinition> Stages { get; set; } = new();
    }

    private sealed class CombatCollection
    {
        public CombatTuning Combat { get; set; } = new();
    }

    public static int MaxStage
    {
        get
        {
            EnsureLoaded();
            return _stages.Length;
        }
    }

    public static StageDefinition[] Stages
    {
        get
        {
            EnsureLoaded();
            return _stages;
        }
    }

    public static CombatTuning Combat
    {
        get
        {
            EnsureLoaded();
            return _combat;
        }
    }

    public static IReadOnlyList<UnitDefinition> GetPlayerUnits()
    {
        EnsureLoaded();
        return PlayerRosterIds
            .Select(GetUnit)
            .ToArray();
    }

    public static IReadOnlyList<UnitDefinition> GetEnemyUnits()
    {
        EnsureLoaded();
        return EnemyRosterIds
            .Select(GetUnit)
            .ToArray();
    }

    public static IReadOnlyList<UnitDefinition> GetUnitsByIds(IEnumerable<string> unitIds)
    {
        EnsureLoaded();
        return unitIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(GetUnit)
            .ToArray();
    }

    public static StageDefinition GetStage(int stageNumber)
    {
        EnsureLoaded();

        if (_stages.Length == 0)
        {
            throw new InvalidOperationException("No stage data loaded.");
        }

        var index = Mathf.Clamp(stageNumber, 1, _stages.Length) - 1;
        return _stages[index];
    }

    public static IReadOnlyList<StageDefinition> GetStagesForMap(string mapId)
    {
        EnsureLoaded();
        var normalizedMapId = NormalizeMapId(mapId);
        return _stages
            .Where(stage => NormalizeMapId(stage.MapId).Equals(normalizedMapId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public static StageDefinition GetLatestStageForMap(string mapId)
    {
        EnsureLoaded();
        var normalizedMapId = NormalizeMapId(mapId);
        var match = _stages
            .LastOrDefault(stage => NormalizeMapId(stage.MapId).Equals(normalizedMapId, StringComparison.OrdinalIgnoreCase));
        return match ?? GetStage(1);
    }

    public static UnitDefinition GetUnit(string unitId)
    {
        EnsureLoaded();

        if (_units.TryGetValue(unitId, out var unit))
        {
            return unit;
        }

        throw new InvalidOperationException($"Unit id '{unitId}' was not found in data.");
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        try
        {
            var unitsText = ReadTextFile(UnitsPath);
            var stagesText = ReadTextFile(StagesPath);
            var combatText = ReadTextFile(CombatPath);

            var unitsDoc = JsonSerializer.Deserialize<UnitCollection>(unitsText, JsonOptions);
            var stagesDoc = JsonSerializer.Deserialize<StageCollection>(stagesText, JsonOptions);
            var combatDoc = JsonSerializer.Deserialize<CombatCollection>(combatText, JsonOptions);

            if (unitsDoc == null || unitsDoc.Units.Count == 0)
            {
                throw new InvalidOperationException("units.json does not contain units.");
            }

            if (stagesDoc == null || stagesDoc.Stages.Count == 0)
            {
                throw new InvalidOperationException("stages.json does not contain stages.");
            }

            if (combatDoc == null || combatDoc.Combat == null)
            {
                throw new InvalidOperationException("combat_config.json does not contain combat tuning.");
            }

            _units = new Dictionary<string, UnitDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var unit in unitsDoc.Units)
            {
                if (string.IsNullOrWhiteSpace(unit.Id))
                {
                    continue;
                }

                _units[unit.Id] = unit;
            }

            _stages = stagesDoc.Stages
                .OrderBy(stage => stage.StageNumber)
                .ToArray();

            _combat = combatDoc.Combat;
            _combat.Normalize();

            ValidateStageOrder(_stages);
        }
        catch (Exception ex)
        {
            GD.PushError($"Failed to load game data from JSON. Using fallback data. {ex.Message}");
            LoadFallbackData();
        }
    }

    private static void ValidateStageOrder(StageDefinition[] stages)
    {
        for (var i = 0; i < stages.Length; i++)
        {
            if (stages[i].StageNumber != i + 1)
            {
                throw new InvalidOperationException("Stage numbers must be contiguous and start at 1.");
            }
        }
    }

    private static string ReadTextFile(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            throw new InvalidOperationException($"Could not open '{path}'.");
        }

        return file.GetAsText();
    }

    private static void LoadFallbackData()
    {
        _combat = new CombatTuning();
        _combat.Normalize();

        _units = new Dictionary<string, UnitDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            {
                PlayerBrawlerId,
                new UnitDefinition
                {
                    Id = PlayerBrawlerId,
                    DisplayName = "Brawler",
                    Side = "Player",
                    SquadTag = SquadSynergyCatalog.FrontlineTag,
                    UnlockStage = 1,
                    GoldCost = 0,
                    Cost = 20,
                    MaxHealth = 74f,
                    Speed = 95f,
                    AttackDamage = 15f,
                    AttackRange = 32f,
                    AttackCooldown = 0.9f,
                    BaseDamage = 22,
                    DeployCooldown = 6f,
                    ColorHex = "f4a261"
                }
            },
            {
                PlayerShooterId,
                new UnitDefinition
                {
                    Id = PlayerShooterId,
                    DisplayName = "Shooter",
                    Side = "Player",
                    SquadTag = SquadSynergyCatalog.SupportTag,
                    UnlockStage = 1,
                    GoldCost = 0,
                    Cost = 30,
                    MaxHealth = 48f,
                    Speed = 58f,
                    AttackDamage = 10f,
                    AttackRange = 146f,
                    AttackCooldown = 1.0f,
                    UsesProjectile = true,
                    ProjectileSpeed = 480f,
                    BaseDamage = 16,
                    DeployCooldown = 8f,
                    ColorHex = "8ecae6"
                }
            },
            {
                PlayerDefenderId,
                new UnitDefinition
                {
                    Id = PlayerDefenderId,
                    DisplayName = "Defender",
                    Side = "Player",
                    SquadTag = SquadSynergyCatalog.FrontlineTag,
                    UnlockStage = 1,
                    GoldCost = 0,
                    Cost = 28,
                    MaxHealth = 108f,
                    Speed = 46f,
                    AttackDamage = 12f,
                    AttackRange = 28f,
                    AttackCooldown = 0.85f,
                    BaseDamage = 20,
                    DeployCooldown = 9f,
                    ColorHex = "f6bd60"
                }
            },
            {
                PlayerRangerId,
                new UnitDefinition
                {
                    Id = PlayerRangerId,
                    DisplayName = "Ranger",
                    Side = "Player",
                    SquadTag = SquadSynergyCatalog.ReconTag,
                    UnlockStage = 2,
                    GoldCost = 170,
                    Cost = 36,
                    MaxHealth = 44f,
                    Speed = 54f,
                    AttackDamage = 13f,
                    AttackRange = 188f,
                    AttackCooldown = 1.2f,
                    UsesProjectile = true,
                    ProjectileSpeed = 560f,
                    BaseDamage = 18,
                    DeployCooldown = 11f,
                    ColorHex = "a8dadc"
                }
            },
            {
                PlayerRaiderId,
                new UnitDefinition
                {
                    Id = PlayerRaiderId,
                    DisplayName = "Raider",
                    Side = "Player",
                    SquadTag = SquadSynergyCatalog.ReconTag,
                    UnlockStage = 4,
                    GoldCost = 220,
                    Cost = 18,
                    MaxHealth = 54f,
                    Speed = 126f,
                    AttackDamage = 11f,
                    AttackRange = 24f,
                    AttackCooldown = 0.72f,
                    AggroRangeX = 220f,
                    AggroRangeY = 86f,
                    BaseDamage = 20,
                    DeployCooldown = 5f,
                    ColorHex = "ffb703"
                }
            },
            {
                PlayerMarksmanId,
                new UnitDefinition
                {
                    Id = PlayerMarksmanId,
                    DisplayName = "Marksman",
                    Side = "Player",
                    SquadTag = SquadSynergyCatalog.ReconTag,
                    UnlockStage = 6,
                    GoldCost = 320,
                    Cost = 42,
                    MaxHealth = 38f,
                    Speed = 52f,
                    AttackDamage = 26f,
                    AttackRange = 250f,
                    AttackCooldown = 1.8f,
                    UsesProjectile = true,
                    ProjectileSpeed = 720f,
                    AggroRangeX = 440f,
                    AggroRangeY = 156f,
                    BaseDamage = 22,
                    DeployCooldown = 14f,
                    ColorHex = "e9c46a"
                }
            },
            {
                PlayerBreacherId,
                new UnitDefinition
                {
                    Id = PlayerBreacherId,
                    DisplayName = "Breacher",
                    Side = "Player",
                    SquadTag = SquadSynergyCatalog.BreachTag,
                    UnlockStage = 9,
                    GoldCost = 420,
                    Cost = 38,
                    MaxHealth = 92f,
                    Speed = 72f,
                    AttackDamage = 19f,
                    AttackRange = 30f,
                    AttackCooldown = 1.02f,
                    AggroRangeX = 225f,
                    AggroRangeY = 88f,
                    BaseDamage = 38,
                    DeployCooldown = 12f,
                    VisualClass = "fighter",
                    VisualScale = 1.06f,
                    ColorHex = "f3722c"
                }
            },
            {
                PlayerGrenadierId,
                new UnitDefinition
                {
                    Id = PlayerGrenadierId,
                    DisplayName = "Grenadier",
                    Side = "Player",
                    SquadTag = SquadSynergyCatalog.BreachTag,
                    UnlockStage = 10,
                    GoldCost = 460,
                    Cost = 40,
                    MaxHealth = 58f,
                    Speed = 50f,
                    AttackDamage = 16f,
                    AttackRange = 160f,
                    AttackCooldown = 1.45f,
                    AttackSplashRadius = 56f,
                    UsesProjectile = true,
                    ProjectileSpeed = 430f,
                    AggroRangeX = 320f,
                    AggroRangeY = 136f,
                    BaseDamage = 18,
                    DeployCooldown = 12.5f,
                    VisualClass = "gunner",
                    VisualScale = 1.02f,
                    ColorHex = "f28482"
                }
            },
            {
                PlayerCoordinatorId,
                new UnitDefinition
                {
                    Id = PlayerCoordinatorId,
                    DisplayName = "Coordinator",
                    Side = "Player",
                    SquadTag = SquadSynergyCatalog.SupportTag,
                    UnlockStage = 13,
                    GoldCost = 560,
                    Cost = 34,
                    MaxHealth = 62f,
                    Speed = 58f,
                    AttackDamage = 11f,
                    AttackRange = 150f,
                    AttackCooldown = 1.06f,
                    UsesProjectile = true,
                    ProjectileSpeed = 500f,
                    AggroRangeX = 300f,
                    AggroRangeY = 128f,
                    BaseDamage = 16,
                    AuraRadius = 148f,
                    AuraAttackDamageScale = 1.12f,
                    AuraSpeedScale = 1.08f,
                    DeployCooldown = 11.5f,
                    VisualClass = "support",
                    VisualScale = 1f,
                    ColorHex = "b7efc5"
                }
            },
            {
                PlayerMechanicId,
                new UnitDefinition
                {
                    Id = PlayerMechanicId,
                    DisplayName = "Mechanic",
                    Side = "Player",
                    SquadTag = SquadSynergyCatalog.SupportTag,
                    UnlockStage = 5,
                    GoldCost = 260,
                    Cost = 26,
                    MaxHealth = 56f,
                    Speed = 60f,
                    AttackDamage = 8f,
                    AttackRange = 132f,
                    AttackCooldown = 1.08f,
                    UsesProjectile = true,
                    ProjectileSpeed = 470f,
                    AggroRangeX = 280f,
                    AggroRangeY = 120f,
                    BaseDamage = 14,
                    BusRepairAmount = 10f,
                    DeployCooldown = 10f,
                    VisualClass = "support",
                    ColorHex = "90be6d"
                }
            },
            {
                EnemyWalkerId,
                new UnitDefinition
                {
                    Id = EnemyWalkerId,
                    DisplayName = "Walker",
                    Side = "Enemy",
                    Cost = 0,
                    MaxHealth = 62f,
                    Speed = 55f,
                    AttackDamage = 9f,
                    AttackRange = 30f,
                    AttackCooldown = 1.0f,
                    BaseDamage = 14,
                    ColorHex = "84a98c"
                }
            },
            {
                EnemyRunnerId,
                new UnitDefinition
                {
                    Id = EnemyRunnerId,
                    DisplayName = "Runner",
                    Side = "Enemy",
                    Cost = 0,
                    MaxHealth = 40f,
                    Speed = 106f,
                    AttackDamage = 8f,
                    AttackRange = 24f,
                    AttackCooldown = 0.7f,
                    BaseDamage = 12,
                    ColorHex = "6d597a"
                }
            },
            {
                EnemyBloaterId,
                new UnitDefinition
                {
                    Id = EnemyBloaterId,
                    DisplayName = "Bloater",
                    Side = "Enemy",
                    Cost = 0,
                    MaxHealth = 88f,
                    Speed = 42f,
                    AttackDamage = 13f,
                    AttackRange = 30f,
                    AttackCooldown = 1.15f,
                    AggroRangeX = 190f,
                    AggroRangeY = 84f,
                    BaseDamage = 18,
                    DeathBurstDamage = 20f,
                    DeathBurstRadius = 54f,
                    ColorHex = "c77dff"
                }
            },
            {
                EnemyBruteId,
                new UnitDefinition
                {
                    Id = EnemyBruteId,
                    DisplayName = "Brute",
                    Side = "Enemy",
                    Cost = 0,
                    MaxHealth = 112f,
                    Speed = 38f,
                    AttackDamage = 17f,
                    AttackRange = 34f,
                    AttackCooldown = 1.1f,
                    BaseDamage = 24,
                    ColorHex = "9d0208"
                }
            },
            {
                EnemySpitterId,
                new UnitDefinition
                {
                    Id = EnemySpitterId,
                    DisplayName = "Spitter",
                    Side = "Enemy",
                    Cost = 0,
                    MaxHealth = 46f,
                    Speed = 48f,
                    AttackDamage = 11f,
                    AttackRange = 170f,
                    AttackCooldown = 1.3f,
                    UsesProjectile = true,
                    ProjectileSpeed = 430f,
                    AggroRangeX = 320f,
                    AggroRangeY = 148f,
                    BaseDamage = 15,
                    ColorHex = "bdb2ff"
                }
            },
            {
                EnemySplitterId,
                new UnitDefinition
                {
                    Id = EnemySplitterId,
                    DisplayName = "Splitter",
                    Side = "Enemy",
                    Cost = 0,
                    MaxHealth = 78f,
                    Speed = 52f,
                    AttackDamage = 10f,
                    AttackRange = 28f,
                    AttackCooldown = 0.95f,
                    AggroRangeX = 205f,
                    AggroRangeY = 90f,
                    BaseDamage = 16,
                    SpawnOnDeathUnitId = EnemyWalkerId,
                    SpawnOnDeathCount = 2,
                    ColorHex = "43aa8b"
                }
            },
            {
                EnemySaboteurId,
                new UnitDefinition
                {
                    Id = EnemySaboteurId,
                    DisplayName = "Saboteur",
                    Side = "Enemy",
                    Cost = 0,
                    MaxHealth = 52f,
                    Speed = 116f,
                    AttackDamage = 12f,
                    AttackRange = 24f,
                    AttackCooldown = 0.74f,
                    AggroRangeX = 210f,
                    AggroRangeY = 90f,
                    BaseDamage = 32,
                    VisualClass = "saboteur",
                    VisualScale = 0.92f,
                    ColorHex = "f94144"
                }
            },
            {
                EnemyHowlerId,
                new UnitDefinition
                {
                    Id = EnemyHowlerId,
                    DisplayName = "Howler",
                    Side = "Enemy",
                    Cost = 0,
                    MaxHealth = 84f,
                    Speed = 72f,
                    AttackDamage = 13f,
                    AttackRange = 30f,
                    AttackCooldown = 1.02f,
                    AggroRangeX = 220f,
                    AggroRangeY = 96f,
                    BaseDamage = 18,
                    AuraRadius = 158f,
                    AuraAttackDamageScale = 1.22f,
                    AuraSpeedScale = 1.18f,
                    VisualClass = "howler",
                    VisualScale = 1.04f,
                    ColorHex = "f8961e"
                }
            },
            {
                EnemyJammerId,
                new UnitDefinition
                {
                    Id = EnemyJammerId,
                    DisplayName = "Jammer",
                    Side = "Enemy",
                    Cost = 0,
                    MaxHealth = 70f,
                    Speed = 64f,
                    AttackDamage = 9f,
                    AttackRange = 30f,
                    AttackCooldown = 0.96f,
                    AggroRangeX = 214f,
                    AggroRangeY = 94f,
                    BaseDamage = 16,
                    SpecialAbilityId = "jam_signal",
                    SpecialCooldown = 11f,
                    SpecialCourageGainScale = 0.68f,
                    SpecialDeployCooldownPenalty = 1.2f,
                    SpecialBuffDuration = 5.2f,
                    VisualClass = "jammer",
                    VisualScale = 0.98f,
                    ColorHex = "577590"
                }
            },
            {
                EnemyCrusherId,
                new UnitDefinition
                {
                    Id = EnemyCrusherId,
                    DisplayName = "Crusher",
                    Side = "Enemy",
                    Cost = 0,
                    MaxHealth = 152f,
                    Speed = 30f,
                    AttackDamage = 23f,
                    AttackRange = 36f,
                    AttackCooldown = 1.2f,
                    BaseDamage = 30,
                    DamageTakenScale = 0.76f,
                    ColorHex = "7f5539"
                }
            },
            {
                EnemyBossId,
                new UnitDefinition
                {
                    Id = EnemyBossId,
                    DisplayName = "Overlord",
                    Side = "Enemy",
                    Cost = 0,
                    MaxHealth = 320f,
                    Speed = 24f,
                    AttackDamage = 31f,
                    AttackRange = 44f,
                    AttackCooldown = 1.4f,
                    AggroRangeX = 230f,
                    AggroRangeY = 110f,
                    BaseDamage = 55,
                    DamageTakenScale = 0.82f,
                    SpecialAbilityId = "rally_call",
                    SpecialCooldown = 12f,
                    SpecialSpawnUnitId = EnemyWalkerId,
                    SpecialSpawnCount = 2,
                    SpecialBuffRadius = 182f,
                    SpecialBuffDuration = 5.4f,
                    SpecialBuffAttackDamageScale = 1.32f,
                    SpecialBuffSpeedScale = 1.2f,
                    ColorHex = "5a189a"
                }
            }
        };

        _stages = new[]
        {
            new StageDefinition
            {
                StageNumber = 1,
                StageName = "Outskirts",
                MapId = "city",
                MapName = "City Route",
                TerrainId = "urban",
                Description = "Low density swarm near abandoned gas stations.\nRecommended squad: Brawler + Shooter.",
                RewardGold = 50,
                RewardFood = 3,
                EntryFoodCost = 1,
                ExploreFoodCost = 1,
                MapX = 132f,
                MapY = 356f,
                PlayerBaseHealth = 300f,
                EnemyBaseHealth = 280f,
                EnemySpawnMin = 3.6f,
                EnemySpawnMax = 5.0f,
                EnemyHealthScale = 0.9f,
                EnemyDamageScale = 0.95f,
                WalkerWeight = 0.78f,
                RunnerWeight = 0.22f,
                BruteWeight = 0f,
                SpitterWeight = 0f,
                CrusherWeight = 0f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0f
            },
            new StageDefinition
            {
                StageNumber = 2,
                StageName = "Highway",
                MapId = "city",
                MapName = "City Route",
                TerrainId = "highway",
                Description = "Faster infected and tighter lane pressure.\nRecommended squad: 2x Brawler, 1x Shooter.",
                RewardGold = 75,
                RewardFood = 3,
                EntryFoodCost = 1,
                ExploreFoodCost = 1,
                MapX = 324f,
                MapY = 248f,
                PlayerBaseHealth = 315f,
                EnemyBaseHealth = 330f,
                EnemySpawnMin = 3.2f,
                EnemySpawnMax = 4.6f,
                EnemyHealthScale = 1.02f,
                EnemyDamageScale = 1.10f,
                WalkerWeight = 0.56f,
                RunnerWeight = 0.34f,
                BruteWeight = 0f,
                SpitterWeight = 0.18f,
                CrusherWeight = 0.08f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.05f
            },
            new StageDefinition
            {
                StageNumber = 3,
                StageName = "Mall District",
                MapId = "city",
                MapName = "City Route",
                TerrainId = "night",
                Description = "Dense mixed wave before the city boss siege. Keep your line stable.",
                RewardGold = 105,
                RewardFood = 3,
                EntryFoodCost = 1,
                ExploreFoodCost = 2,
                MapX = 540f,
                MapY = 314f,
                PlayerBaseHealth = 335f,
                EnemyBaseHealth = 380f,
                EnemySpawnMin = 2.8f,
                EnemySpawnMax = 4.0f,
                EnemyHealthScale = 1.18f,
                EnemyDamageScale = 1.22f,
                WalkerWeight = 0.42f,
                RunnerWeight = 0.24f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.26f,
                CrusherWeight = 0.16f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.08f
            },
            new StageDefinition
            {
                StageNumber = 4,
                StageName = "Metro Citadel",
                MapId = "city",
                MapName = "City Route",
                TerrainId = "night",
                Description = "City boss stage. Overlord spawns begin toward the end of the battle.",
                RewardGold = 145,
                RewardFood = 4,
                EntryFoodCost = 1,
                ExploreFoodCost = 2,
                MapX = 742f,
                MapY = 226f,
                PlayerBaseHealth = 355f,
                EnemyBaseHealth = 440f,
                EnemySpawnMin = 2.6f,
                EnemySpawnMax = 3.8f,
                EnemyHealthScale = 1.30f,
                EnemyDamageScale = 1.32f,
                WalkerWeight = 0.28f,
                RunnerWeight = 0.16f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.20f,
                CrusherWeight = 0.24f,
                BossWeight = 0.22f,
                BossSpawnStartTime = 70f,
                BonusWaveChance = 0.10f
            },
            new StageDefinition
            {
                StageNumber = 5,
                StageName = "Docks Perimeter",
                MapId = "harbor",
                MapName = "Harbor Front",
                TerrainId = "industrial",
                Description = "Second map unlocked. Rusted docks and container chokepoints.",
                RewardGold = 170,
                RewardFood = 4,
                EntryFoodCost = 2,
                ExploreFoodCost = 3,
                MapX = 168f,
                MapY = 496f,
                PlayerBaseHealth = 365f,
                EnemyBaseHealth = 430f,
                EnemySpawnMin = 2.9f,
                EnemySpawnMax = 4.1f,
                EnemyHealthScale = 1.22f,
                EnemyDamageScale = 1.25f,
                WalkerWeight = 0.38f,
                RunnerWeight = 0.20f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.24f,
                CrusherWeight = 0.20f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.08f
            },
            new StageDefinition
            {
                StageNumber = 6,
                StageName = "Flooded Terminal",
                MapId = "harbor",
                MapName = "Harbor Front",
                TerrainId = "swamp",
                Description = "Waterlogged streets slow pushes. Spitters and heavy units dominate.",
                RewardGold = 200,
                RewardFood = 4,
                EntryFoodCost = 2,
                ExploreFoodCost = 3,
                MapX = 346f,
                MapY = 546f,
                PlayerBaseHealth = 385f,
                EnemyBaseHealth = 470f,
                EnemySpawnMin = 2.6f,
                EnemySpawnMax = 3.8f,
                EnemyHealthScale = 1.30f,
                EnemyDamageScale = 1.33f,
                WalkerWeight = 0.24f,
                RunnerWeight = 0.14f,
                BruteWeight = 0.28f,
                SpitterWeight = 0.34f,
                CrusherWeight = 0.24f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.10f
            },
            new StageDefinition
            {
                StageNumber = 7,
                StageName = "Crane Gauntlet",
                MapId = "harbor",
                MapName = "Harbor Front",
                TerrainId = "shipyard",
                Description = "Heavy armor waves push through wrecked cranes before the final harbor boss.",
                RewardGold = 235,
                RewardFood = 5,
                EntryFoodCost = 2,
                ExploreFoodCost = 4,
                MapX = 556f,
                MapY = 502f,
                PlayerBaseHealth = 400f,
                EnemyBaseHealth = 530f,
                EnemySpawnMin = 2.5f,
                EnemySpawnMax = 3.6f,
                EnemyHealthScale = 1.38f,
                EnemyDamageScale = 1.38f,
                WalkerWeight = 0.18f,
                RunnerWeight = 0.10f,
                BruteWeight = 0.30f,
                SpitterWeight = 0.28f,
                CrusherWeight = 0.34f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.12f
            },
            new StageDefinition
            {
                StageNumber = 8,
                StageName = "Wreck Flagship",
                MapId = "harbor",
                MapName = "Harbor Front",
                TerrainId = "shipyard",
                Description = "Harbor boss stage. Overlord spawns begin toward the end of the battle.",
                RewardGold = 280,
                RewardFood = 5,
                EntryFoodCost = 3,
                ExploreFoodCost = 4,
                MapX = 742f,
                MapY = 432f,
                PlayerBaseHealth = 420f,
                EnemyBaseHealth = 620f,
                EnemySpawnMin = 2.4f,
                EnemySpawnMax = 3.4f,
                EnemyHealthScale = 1.48f,
                EnemyDamageScale = 1.44f,
                WalkerWeight = 0.12f,
                RunnerWeight = 0.08f,
                BruteWeight = 0.26f,
                SpitterWeight = 0.22f,
                CrusherWeight = 0.32f,
                BossWeight = 0.26f,
                BossSpawnStartTime = 72f,
                BonusWaveChance = 0.14f
            },
            new StageDefinition
            {
                StageNumber = 9,
                StageName = "Freight Siding",
                MapId = "foundry",
                MapName = "Foundry Line",
                TerrainId = "railyard",
                Description = "Third route unlocked. Freight cuts favor splitters and brute pressure.",
                RewardGold = 320,
                RewardFood = 6,
                EntryFoodCost = 3,
                ExploreFoodCost = 5,
                MapX = 148f,
                MapY = 206f,
                PlayerBaseHealth = 440f,
                EnemyBaseHealth = 650f,
                EnemySpawnMin = 2.35f,
                EnemySpawnMax = 3.3f,
                EnemyHealthScale = 1.54f,
                EnemyDamageScale = 1.50f,
                WalkerWeight = 0.12f,
                RunnerWeight = 0.12f,
                BruteWeight = 0.28f,
                SpitterWeight = 0.14f,
                CrusherWeight = 0.22f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.14f
            },
            new StageDefinition
            {
                StageNumber = 10,
                StageName = "Smelter Row",
                MapId = "foundry",
                MapName = "Foundry Line",
                TerrainId = "smelter",
                Description = "Molten lanes and reinforced barricades reward higher bus damage and disciplined deployments.",
                RewardGold = 360,
                RewardFood = 6,
                EntryFoodCost = 3,
                ExploreFoodCost = 5,
                MapX = 334f,
                MapY = 154f,
                PlayerBaseHealth = 455f,
                EnemyBaseHealth = 700f,
                EnemySpawnMin = 2.25f,
                EnemySpawnMax = 3.15f,
                EnemyHealthScale = 1.62f,
                EnemyDamageScale = 1.58f,
                WalkerWeight = 0.1f,
                RunnerWeight = 0.08f,
                BruteWeight = 0.30f,
                SpitterWeight = 0.16f,
                CrusherWeight = 0.26f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.16f
            },
            new StageDefinition
            {
                StageNumber = 11,
                StageName = "Cinder Causeway",
                MapId = "foundry",
                MapName = "Foundry Line",
                TerrainId = "foundry",
                Description = "Steady heavy pushes and splitter screens test how well the convoy recovers between surges.",
                RewardGold = 410,
                RewardFood = 7,
                EntryFoodCost = 4,
                ExploreFoodCost = 5,
                MapX = 548f,
                MapY = 244f,
                PlayerBaseHealth = 470f,
                EnemyBaseHealth = 760f,
                EnemySpawnMin = 2.15f,
                EnemySpawnMax = 3.0f,
                EnemyHealthScale = 1.68f,
                EnemyDamageScale = 1.64f,
                WalkerWeight = 0.08f,
                RunnerWeight = 0.06f,
                BruteWeight = 0.32f,
                SpitterWeight = 0.14f,
                CrusherWeight = 0.3f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.18f
            },
            new StageDefinition
            {
                StageNumber = 12,
                StageName = "Furnace Crown",
                MapId = "foundry",
                MapName = "Foundry Line",
                TerrainId = "foundry",
                Description = "Foundry boss stage. Splitters and crushers hold the line until the Overlord joins the furnace push.",
                RewardGold = 470,
                RewardFood = 8,
                EntryFoodCost = 4,
                ExploreFoodCost = 6,
                MapX = 728f,
                MapY = 170f,
                PlayerBaseHealth = 490f,
                EnemyBaseHealth = 840f,
                EnemySpawnMin = 2.1f,
                EnemySpawnMax = 2.9f,
                EnemyHealthScale = 1.78f,
                EnemyDamageScale = 1.72f,
                WalkerWeight = 0.06f,
                RunnerWeight = 0.05f,
                BruteWeight = 0.28f,
                SpitterWeight = 0.16f,
                CrusherWeight = 0.34f,
                BossWeight = 0.3f,
                BossSpawnStartTime = 76f,
                BonusWaveChance = 0.2f
            },
            new StageDefinition
            {
                StageNumber = 13,
                StageName = "Outer Gate",
                MapId = "quarantine",
                MapName = "Quarantine Wall",
                TerrainId = "checkpoint",
                Description = "Fourth route unlocked. Screening barriers and decon sirens set up spitter fire behind sealed gates.",
                RewardGold = 520,
                RewardFood = 8,
                EntryFoodCost = 4,
                ExploreFoodCost = 6,
                MapX = 156f,
                MapY = 472f,
                PlayerBaseHealth = 510f,
                EnemyBaseHealth = 900f,
                EnemySpawnMin = 2.0f,
                EnemySpawnMax = 2.82f,
                EnemyHealthScale = 1.86f,
                EnemyDamageScale = 1.80f,
                WalkerWeight = 0.06f,
                RunnerWeight = 0.06f,
                BruteWeight = 0.18f,
                SpitterWeight = 0.30f,
                CrusherWeight = 0.22f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.20f
            },
            new StageDefinition
            {
                StageNumber = 14,
                StageName = "Decon Corridor",
                MapId = "quarantine",
                MapName = "Quarantine Wall",
                TerrainId = "decon",
                Description = "Toxic wash tunnels punish overcommits while support infected stack behind the spray lanes.",
                RewardGold = 580,
                RewardFood = 9,
                EntryFoodCost = 5,
                ExploreFoodCost = 7,
                MapX = 332f,
                MapY = 548f,
                PlayerBaseHealth = 530f,
                EnemyBaseHealth = 950f,
                EnemySpawnMin = 1.96f,
                EnemySpawnMax = 2.76f,
                EnemyHealthScale = 1.94f,
                EnemyDamageScale = 1.88f,
                WalkerWeight = 0.05f,
                RunnerWeight = 0.05f,
                BruteWeight = 0.20f,
                SpitterWeight = 0.32f,
                CrusherWeight = 0.22f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.22f
            },
            new StageDefinition
            {
                StageNumber = 15,
                StageName = "Triage Break",
                MapId = "quarantine",
                MapName = "Quarantine Wall",
                TerrainId = "lab",
                Description = "Lab-side barricades harden up while saboteurs and howlers try to crack the convoy from behind.",
                RewardGold = 650,
                RewardFood = 9,
                EntryFoodCost = 5,
                ExploreFoodCost = 7,
                MapX = 548f,
                MapY = 506f,
                PlayerBaseHealth = 545f,
                EnemyBaseHealth = 1030f,
                EnemySpawnMin = 1.9f,
                EnemySpawnMax = 2.68f,
                EnemyHealthScale = 2.02f,
                EnemyDamageScale = 1.95f,
                WalkerWeight = 0.05f,
                RunnerWeight = 0.04f,
                BruteWeight = 0.22f,
                SpitterWeight = 0.30f,
                CrusherWeight = 0.26f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.24f
            },
            new StageDefinition
            {
                StageNumber = 16,
                StageName = "Blacksite Seal",
                MapId = "quarantine",
                MapName = "Quarantine Wall",
                TerrainId = "blacksite",
                Description = "Quarantine boss stage. Toxic purge cycles and sealed kill-box walls hold until the Overlord breaches the blacksite.",
                RewardGold = 730,
                RewardFood = 10,
                EntryFoodCost = 6,
                ExploreFoodCost = 8,
                MapX = 734f,
                MapY = 556f,
                PlayerBaseHealth = 565f,
                EnemyBaseHealth = 1140f,
                EnemySpawnMin = 1.82f,
                EnemySpawnMax = 2.56f,
                EnemyHealthScale = 2.12f,
                EnemyDamageScale = 2.05f,
                WalkerWeight = 0.04f,
                RunnerWeight = 0.04f,
                BruteWeight = 0.22f,
                SpitterWeight = 0.32f,
                CrusherWeight = 0.28f,
                BossWeight = 0.32f,
                BossSpawnStartTime = 78f,
                BonusWaveChance = 0.24f
            }
        };
    }

    private static string NormalizeMapId(string mapId)
    {
        return string.IsNullOrWhiteSpace(mapId)
            ? "city"
            : mapId.Trim().ToLowerInvariant();
    }
}
