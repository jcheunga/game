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
        PlayerMarksmanId
    };

    public static readonly string[] EnemyRosterIds =
    {
        EnemyWalkerId,
        EnemyRunnerId,
        EnemyBloaterId,
        EnemyBruteId,
        EnemySpitterId,
        EnemySplitterId,
        EnemyCrusherId,
        EnemyBossId
    };

    public const string PlayerBrawlerId = "player_brawler";
    public const string PlayerShooterId = "player_shooter";
    public const string PlayerDefenderId = "player_defender";
    public const string PlayerRangerId = "player_ranger";
    public const string PlayerRaiderId = "player_raider";
    public const string PlayerMarksmanId = "player_marksman";
    public const string EnemyWalkerId = "enemy_walker";
    public const string EnemyRunnerId = "enemy_runner";
    public const string EnemyBloaterId = "enemy_bloater";
    public const string EnemyBruteId = "enemy_brute";
    public const string EnemySpitterId = "enemy_spitter";
    public const string EnemySplitterId = "enemy_splitter";
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
                RewardScrap = 40,
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
                RewardScrap = 68,
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
                RewardScrap = 95,
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
                RewardScrap = 130,
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
                RewardScrap = 150,
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
                RewardScrap = 178,
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
                RewardScrap = 208,
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
                RewardScrap = 245,
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
