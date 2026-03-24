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
        PlayerSpearId,
        PlayerRangerId,
        PlayerRaiderId,
        PlayerMechanicId,
        PlayerMarksmanId,
        PlayerBreacherId,
        PlayerGrenadierId,
        PlayerCoordinatorId,
        PlayerHoundId,
        PlayerBannerId,
        PlayerNecromancerId,
        PlayerRogueId,
        PlayerBerserkerId,
        PlayerLanternGuardId,
        PlayerBallistaId
    };

    public static readonly string[] PlayerSpellIds =
    {
        SpellFireballId,
        SpellHealId,
        SpellFrostBurstId,
        SpellLightningStrikeId,
        SpellBarrierWardId,
        SpellStoneBarricadeId,
        SpellWarCryId,
        SpellEarthquakeId,
        SpellPolymorphId,
        SpellResurrectId
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
        EnemyBossId,
        EnemyShieldWallId,
        EnemyLichId,
        EnemySiegeTowerId,
        EnemyMirrorId,
        EnemyTunnelerId,
        EnemyBoneBallistaId,
        EnemyCatacombGiantId,
        EnemyBossDocksId,
        EnemyBossForgeId,
        EnemyBossWardId,
        EnemyBossPassId,
        EnemyBossBasilicaId,
        EnemyBossMireId,
        EnemyBossSteppeId,
        EnemyBossVergeId,
        EnemyBossCitadelId,
        EnemyBossReliquaryId,
        EnemyBossAshenRegentId
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
    public const string PlayerSpearId = "player_spear";
    public const string PlayerCoordinatorId = "player_coordinator";
    public const string PlayerHoundId = "player_hound";
    public const string PlayerBannerId = "player_banner";
    public const string PlayerNecromancerId = "player_necromancer";
    public const string PlayerRogueId = "player_rogue";
    public const string PlayerBerserkerId = "player_berserker";
    public const string PlayerLanternGuardId = "player_lantern_guard";
    public const string PlayerBallistaId = "player_ballista";
    public const string PlayerSkeletonId = "player_skeleton";
    public const string SpellFireballId = "spell_fireball";
    public const string SpellHealId = "spell_heal";
    public const string SpellFrostBurstId = "spell_frost_burst";
    public const string SpellLightningStrikeId = "spell_lightning_strike";
    public const string SpellBarrierWardId = "spell_barrier_ward";
    public const string SpellStoneBarricadeId = "spell_stone_barricade";
    public const string SpellWarCryId = "spell_war_cry";
    public const string SpellEarthquakeId = "spell_earthquake";
    public const string SpellPolymorphId = "spell_polymorph";
    public const string SpellResurrectId = "spell_resurrect";
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
    public const string EnemyShieldWallId = "enemy_shieldwall";
    public const string EnemyLichId = "enemy_lich";
    public const string EnemySiegeTowerId = "enemy_siegetower";
    public const string EnemyMirrorId = "enemy_mirror";
    public const string EnemyTunnelerId = "enemy_tunneler";
    public const string EnemyBoneBallistaId = "enemy_boneballista";
    public const string EnemyCatacombGiantId = "enemy_catacomb_giant";
    public const string EnemyBossDocksId = "enemy_boss_docks";
    public const string EnemyBossForgeId = "enemy_boss_forge";
    public const string EnemyBossWardId = "enemy_boss_ward";
    public const string EnemyBossPassId = "enemy_boss_pass";
    public const string EnemyBossBasilicaId = "enemy_boss_basilica";
    public const string EnemyBossMireId = "enemy_boss_mire";
    public const string EnemyBossSteppeId = "enemy_boss_steppe";
    public const string EnemyBossVergeId = "enemy_boss_verge";
    public const string EnemyBossCitadelId = "enemy_boss_citadel";
    public const string EnemyBossReliquaryId = "enemy_boss_reliquary";
    public const string EnemyBossAshenRegentId = "enemy_boss_ashen_regent";

    private const string UnitsPath = "res://data/units.json";
    private const string SpellsPath = "res://data/spells.json";
    private const string StagesPath = "res://data/stages.json";
    private const string CombatPath = "res://data/combat_config.json";
    private const string EquipmentPath = "res://data/equipment.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static bool _loaded;
    private static StageDefinition[] _stages = Array.Empty<StageDefinition>();
    private static Dictionary<string, UnitDefinition> _units = new();
    private static Dictionary<string, SpellDefinition> _spells = new();
    private static Dictionary<string, EquipmentDefinition> _equipment = new();
    private static CombatTuning _combat = new();

    private sealed class UnitCollection
    {
        public List<UnitDefinition> Units { get; set; } = new();
    }

    private sealed class StageCollection
    {
        public List<StageDefinition> Stages { get; set; } = new();
    }

    private sealed class SpellCollection
    {
        public List<SpellDefinition> Spells { get; set; } = new();
    }

    private sealed class EquipmentCollection
    {
        public List<EquipmentDefinition> Equipment { get; set; } = new();
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

    public static IReadOnlyList<SpellDefinition> GetPlayerSpells()
    {
        EnsureLoaded();
        return PlayerSpellIds
            .Select(GetSpell)
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

    public static IReadOnlyList<SpellDefinition> GetSpellsByIds(IEnumerable<string> spellIds)
    {
        EnsureLoaded();
        return spellIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(GetSpell)
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

    public static SpellDefinition GetSpell(string spellId)
    {
        EnsureLoaded();

        if (_spells.TryGetValue(spellId, out var spell))
        {
            return spell;
        }

        throw new InvalidOperationException($"Spell id '{spellId}' was not found in data.");
    }

    public static EquipmentDefinition GetEquipment(string id)
    {
        EnsureLoaded();

        if (_equipment.TryGetValue(id, out var equip))
        {
            return equip;
        }

        throw new InvalidOperationException($"Equipment id '{id}' was not found in data.");
    }

    public static IReadOnlyList<EquipmentDefinition> GetAllEquipment()
    {
        EnsureLoaded();
        return _equipment.Values.ToArray();
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
            var spellsText = ReadTextFile(SpellsPath);
            var stagesText = ReadTextFile(StagesPath);
            var combatText = ReadTextFile(CombatPath);
            var equipmentText = ReadTextFile(EquipmentPath);

            var unitsDoc = JsonSerializer.Deserialize<UnitCollection>(unitsText, JsonOptions);
            var spellsDoc = JsonSerializer.Deserialize<SpellCollection>(spellsText, JsonOptions);
            var stagesDoc = JsonSerializer.Deserialize<StageCollection>(stagesText, JsonOptions);
            var combatDoc = JsonSerializer.Deserialize<CombatCollection>(combatText, JsonOptions);
            var equipmentDoc = JsonSerializer.Deserialize<EquipmentCollection>(equipmentText, JsonOptions);

            if (unitsDoc == null || unitsDoc.Units.Count == 0)
            {
                throw new InvalidOperationException("units.json does not contain units.");
            }

            if (spellsDoc == null || spellsDoc.Spells.Count == 0)
            {
                throw new InvalidOperationException("spells.json does not contain spells.");
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

            _spells = new Dictionary<string, SpellDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var spell in spellsDoc.Spells)
            {
                if (string.IsNullOrWhiteSpace(spell.Id))
                {
                    continue;
                }

                _spells[spell.Id] = spell;
            }

            _equipment = new Dictionary<string, EquipmentDefinition>(StringComparer.OrdinalIgnoreCase);
            if (equipmentDoc != null)
            {
                foreach (var equip in equipmentDoc.Equipment)
                {
                    if (string.IsNullOrWhiteSpace(equip.Id))
                    {
                        continue;
                    }

                    _equipment[equip.Id] = equip;
                }
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
                    DisplayName = "Swordsman",
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
                    DisplayName = "Archer",
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
                    DisplayName = "Shield Knight",
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
                PlayerSpearId,
                new UnitDefinition
                {
                    Id = PlayerSpearId,
                    DisplayName = "Spearman",
                    Side = "Player",
                    SquadTag = SquadSynergyCatalog.FrontlineTag,
                    UnlockStage = 3,
                    GoldCost = 140,
                    Cost = 24,
                    MaxHealth = 84f,
                    Speed = 72f,
                    AttackDamage = 14f,
                    AttackRange = 46f,
                    AttackCooldown = 1.08f,
                    AggroRangeX = 220f,
                    AggroRangeY = 88f,
                    BaseDamage = 20,
                    DeployCooldown = 7.5f,
                    VisualClass = "fighter",
                    VisualScale = 1.04f,
                    ColorHex = "d4a373"
                }
            },
            {
                PlayerRangerId,
                new UnitDefinition
                {
                    Id = PlayerRangerId,
                    DisplayName = "Crossbowman",
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
                    DisplayName = "Cavalry Rider",
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
                    DisplayName = "Mage",
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
                    DisplayName = "Halberdier",
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
                    DisplayName = "Alchemist",
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
                    DisplayName = "Battle Monk",
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
                    DisplayName = "Siege Engineer",
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
                    DisplayName = "Risen",
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
                    DisplayName = "Ghoul",
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
                    DisplayName = "Rot Hulk",
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
                    DisplayName = "Grave Brute",
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
                    DisplayName = "Blight Caster",
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
                    DisplayName = "Bone Nest",
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
                    DisplayName = "Sapper",
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
                    DisplayName = "Dread Herald",
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
                    DisplayName = "Hexer",
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
                    DisplayName = "Bone Juggernaut",
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
                    DisplayName = "Grave Lord",
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
                StageName = "Far Gate",
                MapId = "city",
                MapName = "King's Road",
                TerrainId = "urban",
                Description = "Thin undead probes along the outer farms and pilgrim road.\nRecommended squad: Swordsman + Archer.",
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
                StageName = "Stone Causeway",
                MapId = "city",
                MapName = "King's Road",
                TerrainId = "highway",
                Description = "Faster ghouls and tighter lane pressure on the raised road.\nRecommended squad: 2x Swordsman, 1x Archer.",
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
                StageName = "Market Ward",
                MapId = "city",
                MapName = "King's Road",
                TerrainId = "night",
                Description = "Dense mixed waves before the inner gate assault. Keep your line steady.",
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
                StageName = "Bell Tower Gate",
                MapId = "city",
                MapName = "King's Road",
                TerrainId = "night",
                Description = "King's Road boss stage. Grave Lord entries begin toward the end of the battle.",
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
                StageName = "Mooring Ring",
                MapId = "harbor",
                MapName = "Saltwake Docks",
                TerrainId = "industrial",
                Description = "Second district unlocked. Salt-soaked quays and chainlift chokepoints.",
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
                StageName = "Drowned Quay",
                MapId = "harbor",
                MapName = "Saltwake Docks",
                TerrainId = "swamp",
                Description = "Flooded approaches slow pushes. Blight casters and heavy dead control the lane.",
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
                StageName = "Chainlift Yard",
                MapId = "harbor",
                MapName = "Saltwake Docks",
                TerrainId = "shipyard",
                Description = "Armored waves force through broken cranes before the Grave Lord arrives.",
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
                StageName = "Wreck Admiral",
                MapId = "harbor",
                MapName = "Saltwake Docks",
                TerrainId = "shipyard",
                Description = "Saltwake boss stage. Grave Lord entries begin toward the end of the battle.",
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
                StageName = "Forge Siding",
                MapId = "foundry",
                MapName = "Emberforge March",
                TerrainId = "railyard",
                Description = "Third district unlocked. Coal spurs favor split-brood screens, sapper dives, and brute-led pushes.\nRecommended squad: Shield Knight + Archer + Siege Engineer or Halberdier.",
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
                MapName = "Emberforge March",
                TerrainId = "smelter",
                Description = "Molten choke points, sapper flanks, and reinforced gates reward higher gate damage and disciplined deployment.\nRecommended squad: Halberdier + Shield Knight + Mage.",
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
                MapName = "Emberforge March",
                TerrainId = "foundry",
                Description = "Steady heavy pushes, split-brood screens, and sapper feints test how well the caravan recovers between surges.",
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
                MapName = "Emberforge March",
                TerrainId = "foundry",
                Description = "Emberforge boss stage. Split broods, sappers, and juggernauts hold the line until the Grave Lord joins the furnace push.",
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
                StageName = "Outer Ward",
                MapId = "quarantine",
                MapName = "Ashen Ward",
                TerrainId = "checkpoint",
                Description = "Fourth district unlocked. Warded barriers and purge bells set up blight fire behind sealed gates.",
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
                StageName = "Purge Cloister",
                MapId = "quarantine",
                MapName = "Ashen Ward",
                TerrainId = "decon",
                Description = "Caustic wash halls punish overcommits while curse support stacks behind the ritual lanes.",
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
                StageName = "Leechcourt",
                MapId = "quarantine",
                MapName = "Ashen Ward",
                TerrainId = "lab",
                Description = "Plaguehouse barricades harden up while sappers and dread heralds try to crack the caravan from behind.",
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
                StageName = "Black Vault Seal",
                MapId = "quarantine",
                MapName = "Ashen Ward",
                TerrainId = "blacksite",
                Description = "Ashen Ward boss stage. Purge cycles and sealed kill-box walls hold until the Grave Lord breaches the vault.",
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
            },
            new StageDefinition
            {
                StageNumber = 17,
                StageName = "Narrow Ascent",
                MapId = "thornwall",
                MapName = "Thornwall Pass",
                TerrainId = "pass",
                Description = "Frostbound scouts test the cliff road while raiders look for the war wagon axle.",
                RewardGold = 820,
                RewardFood = 10,
                EntryFoodCost = 6,
                ExploreFoodCost = 8,
                MapX = 138f,
                MapY = 520f,
                PlayerBaseHealth = 580f,
                EnemyBaseHealth = 1180f,
                EnemySpawnMin = 1.82f,
                EnemySpawnMax = 2.52f,
                EnemyHealthScale = 2.18f,
                EnemyDamageScale = 2.1f,
                WalkerWeight = 0.06f,
                RunnerWeight = 0.1f,
                BruteWeight = 0.22f,
                SpitterWeight = 0.16f,
                CrusherWeight = 0.24f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.24f
            },
            new StageDefinition
            {
                StageNumber = 18,
                StageName = "Rime Switchback",
                MapId = "thornwall",
                MapName = "Thornwall Pass",
                TerrainId = "pass",
                Description = "Narrow turns compress the climb under sleet while howlers and sappers chain quick dives.",
                RewardGold = 900,
                RewardFood = 11,
                EntryFoodCost = 6,
                ExploreFoodCost = 8,
                MapX = 304f,
                MapY = 430f,
                PlayerBaseHealth = 600f,
                EnemyBaseHealth = 1240f,
                EnemySpawnMin = 1.76f,
                EnemySpawnMax = 2.46f,
                EnemyHealthScale = 2.24f,
                EnemyDamageScale = 2.16f,
                WalkerWeight = 0.05f,
                RunnerWeight = 0.12f,
                BruteWeight = 0.22f,
                SpitterWeight = 0.18f,
                CrusherWeight = 0.24f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.25f
            },
            new StageDefinition
            {
                StageNumber = 19,
                StageName = "Avalanche Shrine",
                MapId = "thornwall",
                MapName = "Thornwall Pass",
                TerrainId = "shrine",
                Description = "Avalanche bells ring over the pass, and the crew must steady the shrine dais before the drifts crack open again.",
                RewardGold = 980,
                RewardFood = 11,
                EntryFoodCost = 6,
                ExploreFoodCost = 9,
                MapX = 470f,
                MapY = 252f,
                PlayerBaseHealth = 620f,
                EnemyBaseHealth = 1310f,
                EnemySpawnMin = 1.72f,
                EnemySpawnMax = 2.38f,
                EnemyHealthScale = 2.32f,
                EnemyDamageScale = 2.24f,
                WalkerWeight = 0.05f,
                RunnerWeight = 0.1f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.18f,
                CrusherWeight = 0.26f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.26f,
                Objectives = new[]
                {
                    new StageObjectiveDefinition { Type = "clear_route" },
                    new StageObjectiveDefinition { Type = "mission_event_success" },
                    new StageObjectiveDefinition { Type = "hazard_hits_limit", Value = 4f }
                },
                MissionEvents = new[]
                {
                    new StageMissionEventDefinition
                    {
                        Type = "ritual_site",
                        Title = "Avalanche Shrine",
                        Summary = "Hold the shrine dais long enough to silence the dead bells before the pass collapses again.",
                        RewardSummary = "Reward: +12 courage and cleaner deck recovery once the bells go quiet.",
                        PenaltySummary = "Risk: lose courage and card tempo if the shrine falls.",
                        XRatio = 0.58f,
                        YRatio = 0.48f,
                        Radius = 80f,
                        TargetSeconds = 7.5f,
                        StartTime = 24f,
                        ColorHex = "fefae0"
                    }
                }
            },
            new StageDefinition
            {
                StageNumber = 20,
                StageName = "High Watch",
                MapId = "thornwall",
                MapName = "Thornwall Pass",
                TerrainId = "watchfort",
                Description = "Watchfire bastions harden the lane while horns and raid ladders drive the climb.",
                RewardGold = 1060,
                RewardFood = 12,
                EntryFoodCost = 7,
                ExploreFoodCost = 9,
                MapX = 642f,
                MapY = 364f,
                PlayerBaseHealth = 640f,
                EnemyBaseHealth = 1400f,
                EnemySpawnMin = 1.66f,
                EnemySpawnMax = 2.32f,
                EnemyHealthScale = 2.4f,
                EnemyDamageScale = 2.32f,
                WalkerWeight = 0.04f,
                RunnerWeight = 0.1f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.2f,
                CrusherWeight = 0.3f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.27f
            },
            new StageDefinition
            {
                StageNumber = 21,
                StageName = "Thornwall Gate",
                MapId = "thornwall",
                MapName = "Thornwall Pass",
                TerrainId = "watchfort",
                Description = "Thornwall Pass boss stage. Avalanche bells and watchfire volleys hold until the Grave Lord storms the gate.",
                RewardGold = 1160,
                RewardFood = 13,
                EntryFoodCost = 7,
                ExploreFoodCost = 10,
                MapX = 770f,
                MapY = 220f,
                PlayerBaseHealth = 665f,
                EnemyBaseHealth = 1520f,
                EnemySpawnMin = 1.58f,
                EnemySpawnMax = 2.22f,
                EnemyHealthScale = 2.52f,
                EnemyDamageScale = 2.42f,
                WalkerWeight = 0.03f,
                RunnerWeight = 0.1f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.2f,
                CrusherWeight = 0.32f,
                BossWeight = 0.34f,
                BossSpawnStartTime = 80f,
                BonusWaveChance = 0.28f
            },
            new StageDefinition
            {
                StageNumber = 22,
                StageName = "Outer Nave",
                MapId = "basilica",
                MapName = "Hollow Basilica",
                TerrainId = "cathedral",
                Description = "Collapsed transepts and pew barricades turn the first nave into a slow grind under curse fire.",
                RewardGold = 1260,
                RewardFood = 13,
                EntryFoodCost = 7,
                ExploreFoodCost = 10,
                MapX = 150f,
                MapY = 338f,
                PlayerBaseHealth = 690f,
                EnemyBaseHealth = 1600f,
                EnemySpawnMin = 1.56f,
                EnemySpawnMax = 2.18f,
                EnemyHealthScale = 2.6f,
                EnemyDamageScale = 2.48f,
                WalkerWeight = 0.05f,
                RunnerWeight = 0.06f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.22f,
                CrusherWeight = 0.28f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.28f
            },
            new StageDefinition
            {
                StageNumber = 23,
                StageName = "Ossuary Court",
                MapId = "basilica",
                MapName = "Hollow Basilica",
                TerrainId = "ossuary",
                Description = "Bone courts spill smaller dead while hexers and heralds hold the center under crumbling saints.",
                RewardGold = 1360,
                RewardFood = 13,
                EntryFoodCost = 7,
                ExploreFoodCost = 10,
                MapX = 322f,
                MapY = 206f,
                PlayerBaseHealth = 710f,
                EnemyBaseHealth = 1680f,
                EnemySpawnMin = 1.5f,
                EnemySpawnMax = 2.12f,
                EnemyHealthScale = 2.68f,
                EnemyDamageScale = 2.56f,
                WalkerWeight = 0.05f,
                RunnerWeight = 0.06f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.24f,
                CrusherWeight = 0.28f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.29f
            },
            new StageDefinition
            {
                StageNumber = 24,
                StageName = "Choir Ruin",
                MapId = "basilica",
                MapName = "Hollow Basilica",
                TerrainId = "cathedral",
                Description = "The choir loft rains curses over the aisle while reliquary bearers try to cross the nave under caravan cover.",
                RewardGold = 1470,
                RewardFood = 14,
                EntryFoodCost = 8,
                ExploreFoodCost = 10,
                MapX = 510f,
                MapY = 410f,
                PlayerBaseHealth = 730f,
                EnemyBaseHealth = 1770f,
                EnemySpawnMin = 1.46f,
                EnemySpawnMax = 2.06f,
                EnemyHealthScale = 2.76f,
                EnemyDamageScale = 2.64f,
                WalkerWeight = 0.04f,
                RunnerWeight = 0.05f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.26f,
                CrusherWeight = 0.3f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.3f,
                Objectives = new[]
                {
                    new StageObjectiveDefinition { Type = "clear_route" },
                    new StageObjectiveDefinition { Type = "mission_event_success" },
                    new StageObjectiveDefinition { Type = "clear_within", Value = 122f }
                },
                MissionEvents = new[]
                {
                    new StageMissionEventDefinition
                    {
                        Type = "relic_escort",
                        Title = "Reliquary Bearers",
                        Summary = "Hold the aisle long enough for the reliquary bearers to reach the war wagon.",
                        RewardSummary = "Reward: a repair burst and escort reinforcement once the relics clear the nave.",
                        PenaltySummary = "Risk: the war wagon takes a direct hit if the escort lane collapses.",
                        XRatio = 0.56f,
                        YRatio = 0.5f,
                        Radius = 82f,
                        TargetSeconds = 8.4f,
                        StartTime = 28f,
                        ColorHex = "ffe5b4"
                    }
                }
            },
            new StageDefinition
            {
                StageNumber = 25,
                StageName = "Reliquary Steps",
                MapId = "basilica",
                MapName = "Hollow Basilica",
                TerrainId = "reliquary",
                Description = "Relic vault stairs narrow the line into a ceremonial kill zone guarded by elite undead and curse engines.",
                RewardGold = 1590,
                RewardFood = 14,
                EntryFoodCost = 8,
                ExploreFoodCost = 11,
                MapX = 676f,
                MapY = 262f,
                PlayerBaseHealth = 750f,
                EnemyBaseHealth = 1880f,
                EnemySpawnMin = 1.4f,
                EnemySpawnMax = 1.98f,
                EnemyHealthScale = 2.86f,
                EnemyDamageScale = 2.74f,
                WalkerWeight = 0.04f,
                RunnerWeight = 0.05f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.26f,
                CrusherWeight = 0.32f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.31f
            },
            new StageDefinition
            {
                StageNumber = 26,
                StageName = "Sepulcher Crown",
                MapId = "basilica",
                MapName = "Hollow Basilica",
                TerrainId = "reliquary",
                Description = "Hollow Basilica boss stage. Reliquary flares and censer clouds hold until the Grave Lord claims the high altar.",
                RewardGold = 1740,
                RewardFood = 15,
                EntryFoodCost = 8,
                ExploreFoodCost = 11,
                MapX = 780f,
                MapY = 500f,
                PlayerBaseHealth = 780f,
                EnemyBaseHealth = 2020f,
                EnemySpawnMin = 1.34f,
                EnemySpawnMax = 1.92f,
                EnemyHealthScale = 2.98f,
                EnemyDamageScale = 2.86f,
                WalkerWeight = 0.03f,
                RunnerWeight = 0.05f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.28f,
                CrusherWeight = 0.34f,
                BossWeight = 0.36f,
                BossSpawnStartTime = 82f,
                BonusWaveChance = 0.32f
            },
            new StageDefinition
            {
                StageNumber = 27,
                StageName = "Bog Causeway",
                MapId = "mire",
                MapName = "Mire of Saints",
                TerrainId = "marsh",
                Description = "The first plank roads sink under shambling dead and rot mist while the caravan tests the bog line.",
                RewardGold = 1860,
                RewardFood = 15,
                EntryFoodCost = 8,
                ExploreFoodCost = 11,
                MapX = 166f,
                MapY = 472f,
                PlayerBaseHealth = 805f,
                EnemyBaseHealth = 2120f,
                EnemySpawnMin = 1.32f,
                EnemySpawnMax = 1.88f,
                EnemyHealthScale = 3.06f,
                EnemyDamageScale = 2.94f,
                WalkerWeight = 0.04f,
                RunnerWeight = 0.04f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.28f,
                CrusherWeight = 0.34f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.32f
            },
            new StageDefinition
            {
                StageNumber = 28,
                StageName = "Drowned Chapel",
                MapId = "mire",
                MapName = "Mire of Saints",
                TerrainId = "chapel",
                Description = "Flooded pews and bell wakes narrow the lane while curse support stacks behind the mire dead.",
                RewardGold = 1990,
                RewardFood = 16,
                EntryFoodCost = 9,
                ExploreFoodCost = 11,
                MapX = 332f,
                MapY = 324f,
                PlayerBaseHealth = 830f,
                EnemyBaseHealth = 2240f,
                EnemySpawnMin = 1.28f,
                EnemySpawnMax = 1.82f,
                EnemyHealthScale = 3.16f,
                EnemyDamageScale = 3.02f,
                WalkerWeight = 0.04f,
                RunnerWeight = 0.04f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.3f,
                CrusherWeight = 0.34f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.33f
            },
            new StageDefinition
            {
                StageNumber = 29,
                StageName = "Plague Ferry",
                MapId = "mire",
                MapName = "Mire of Saints",
                TerrainId = "ferry",
                Description = "A chained plague ferry drags the fight into a soaked choke point full of split-brood and blight fire.",
                RewardGold = 2130,
                RewardFood = 16,
                EntryFoodCost = 9,
                ExploreFoodCost = 12,
                MapX = 492f,
                MapY = 548f,
                PlayerBaseHealth = 850f,
                EnemyBaseHealth = 2360f,
                EnemySpawnMin = 1.24f,
                EnemySpawnMax = 1.76f,
                EnemyHealthScale = 3.26f,
                EnemyDamageScale = 3.12f,
                WalkerWeight = 0.03f,
                RunnerWeight = 0.03f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.3f,
                CrusherWeight = 0.36f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.34f
            },
            new StageDefinition
            {
                StageNumber = 30,
                StageName = "Saints' Mire",
                MapId = "mire",
                MapName = "Mire of Saints",
                TerrainId = "marsh",
                Description = "Deep mire drag, plague bells, and endless drift pools force a patient advance against attrition-heavy pressure.",
                RewardGold = 2280,
                RewardFood = 17,
                EntryFoodCost = 9,
                ExploreFoodCost = 12,
                MapX = 648f,
                MapY = 420f,
                PlayerBaseHealth = 875f,
                EnemyBaseHealth = 2500f,
                EnemySpawnMin = 1.2f,
                EnemySpawnMax = 1.7f,
                EnemyHealthScale = 3.38f,
                EnemyDamageScale = 3.22f,
                WalkerWeight = 0.03f,
                RunnerWeight = 0.03f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.32f,
                CrusherWeight = 0.36f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.35f
            },
            new StageDefinition
            {
                StageNumber = 31,
                StageName = "Mire Bell",
                MapId = "mire",
                MapName = "Mire of Saints",
                TerrainId = "chapel",
                Description = "Mire of Saints boss stage. Plague fog and chapel bells roll until the Grave Lord rises at the drowned altar.",
                RewardGold = 2440,
                RewardFood = 18,
                EntryFoodCost = 10,
                ExploreFoodCost = 12,
                MapX = 782f,
                MapY = 248f,
                PlayerBaseHealth = 900f,
                EnemyBaseHealth = 2660f,
                EnemySpawnMin = 1.16f,
                EnemySpawnMax = 1.64f,
                EnemyHealthScale = 3.5f,
                EnemyDamageScale = 3.34f,
                WalkerWeight = 0.02f,
                RunnerWeight = 0.03f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.32f,
                CrusherWeight = 0.38f,
                BossWeight = 0.38f,
                BossSpawnStartTime = 84f,
                BonusWaveChance = 0.36f
            },
            new StageDefinition
            {
                StageNumber = 32,
                StageName = "Burned Waystation",
                MapId = "steppe",
                MapName = "Sunfall Steppe",
                TerrainId = "waystation",
                Description = "Charred shelters and open lanes force quick answers to raider probes and horn-led rushes.",
                RewardGold = 2620,
                RewardFood = 18,
                EntryFoodCost = 10,
                ExploreFoodCost = 13,
                MapX = 172f,
                MapY = 286f,
                PlayerBaseHealth = 930f,
                EnemyBaseHealth = 2780f,
                EnemySpawnMin = 1.12f,
                EnemySpawnMax = 1.6f,
                EnemyHealthScale = 3.6f,
                EnemyDamageScale = 3.42f,
                WalkerWeight = 0.02f,
                RunnerWeight = 0.05f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.22f,
                CrusherWeight = 0.38f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.36f
            },
            new StageDefinition
            {
                StageNumber = 33,
                StageName = "Ash Grass",
                MapId = "steppe",
                MapName = "Sunfall Steppe",
                TerrainId = "grassland",
                Description = "Open ground and cinder wind favor fast raiders, howler signals, and long flanking dives.",
                RewardGold = 2810,
                RewardFood = 19,
                EntryFoodCost = 10,
                ExploreFoodCost = 13,
                MapX = 336f,
                MapY = 482f,
                PlayerBaseHealth = 955f,
                EnemyBaseHealth = 2920f,
                EnemySpawnMin = 1.08f,
                EnemySpawnMax = 1.54f,
                EnemyHealthScale = 3.72f,
                EnemyDamageScale = 3.52f,
                WalkerWeight = 0.02f,
                RunnerWeight = 0.06f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.2f,
                CrusherWeight = 0.4f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.37f
            },
            new StageDefinition
            {
                StageNumber = 34,
                StageName = "Siege Ring",
                MapId = "steppe",
                MapName = "Sunfall Steppe",
                TerrainId = "siegecamp",
                Description = "Roving siege tents and breach crews turn the lane into a tempo fight over repeated dives.",
                RewardGold = 3010,
                RewardFood = 19,
                EntryFoodCost = 11,
                ExploreFoodCost = 13,
                MapX = 506f,
                MapY = 210f,
                PlayerBaseHealth = 980f,
                EnemyBaseHealth = 3080f,
                EnemySpawnMin = 1.04f,
                EnemySpawnMax = 1.48f,
                EnemyHealthScale = 3.84f,
                EnemyDamageScale = 3.64f,
                WalkerWeight = 0.02f,
                RunnerWeight = 0.06f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.2f,
                CrusherWeight = 0.4f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.38f
            },
            new StageDefinition
            {
                StageNumber = 35,
                StageName = "Sunfall Redoubt",
                MapId = "steppe",
                MapName = "Sunfall Steppe",
                TerrainId = "waystation",
                Description = "The redoubt burns hot with horn relays and rider pressure that punish any stalled push.",
                RewardGold = 3220,
                RewardFood = 20,
                EntryFoodCost = 11,
                ExploreFoodCost = 14,
                MapX = 654f,
                MapY = 372f,
                PlayerBaseHealth = 1005f,
                EnemyBaseHealth = 3240f,
                EnemySpawnMin = 1.0f,
                EnemySpawnMax = 1.42f,
                EnemyHealthScale = 3.98f,
                EnemyDamageScale = 3.76f,
                WalkerWeight = 0.01f,
                RunnerWeight = 0.06f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.18f,
                CrusherWeight = 0.42f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.39f
            },
            new StageDefinition
            {
                StageNumber = 36,
                StageName = "Sunfall Warcamp",
                MapId = "steppe",
                MapName = "Sunfall Steppe",
                TerrainId = "siegecamp",
                Description = "Sunfall Steppe boss stage. Fire lines and horn relays pound the lane until the Grave Lord charges out of the warcamp.",
                RewardGold = 3450,
                RewardFood = 21,
                EntryFoodCost = 11,
                ExploreFoodCost = 14,
                MapX = 784f,
                MapY = 546f,
                PlayerBaseHealth = 1035f,
                EnemyBaseHealth = 3420f,
                EnemySpawnMin = 0.96f,
                EnemySpawnMax = 1.36f,
                EnemyHealthScale = 4.12f,
                EnemyDamageScale = 3.9f,
                WalkerWeight = 0.01f,
                RunnerWeight = 0.06f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.18f,
                CrusherWeight = 0.42f,
                BossWeight = 0.4f,
                BossSpawnStartTime = 86f,
                BonusWaveChance = 0.4f
            },
            new StageDefinition
            {
                StageNumber = 37,
                StageName = "Thorn Verge",
                MapId = "gloamwood",
                MapName = "Gloamwood Verge",
                TerrainId = "grove",
                Description = "The timber edge closes in with ambush packs, snare hazards, and curse-lit brush.",
                RewardGold = 3700,
                RewardFood = 21,
                EntryFoodCost = 11,
                ExploreFoodCost = 14,
                MapX = 164f,
                MapY = 512f,
                PlayerBaseHealth = 1060f,
                EnemyBaseHealth = 3600f,
                EnemySpawnMin = 0.94f,
                EnemySpawnMax = 1.34f,
                EnemyHealthScale = 4.24f,
                EnemyDamageScale = 4.0f,
                WalkerWeight = 0.01f,
                RunnerWeight = 0.05f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.2f,
                CrusherWeight = 0.42f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.4f
            },
            new StageDefinition
            {
                StageNumber = 38,
                StageName = "Witch Circle",
                MapId = "gloamwood",
                MapName = "Gloamwood Verge",
                TerrainId = "witchcircle",
                Description = "The circle grounds the fight under hex support and repeated flank dives from the treeline.",
                RewardGold = 3960,
                RewardFood = 22,
                EntryFoodCost = 12,
                ExploreFoodCost = 14,
                MapX = 334f,
                MapY = 282f,
                PlayerBaseHealth = 1090f,
                EnemyBaseHealth = 3780f,
                EnemySpawnMin = 0.9f,
                EnemySpawnMax = 1.3f,
                EnemyHealthScale = 4.38f,
                EnemyDamageScale = 4.12f,
                WalkerWeight = 0.01f,
                RunnerWeight = 0.05f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.2f,
                CrusherWeight = 0.44f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.41f
            },
            new StageDefinition
            {
                StageNumber = 39,
                StageName = "Blackbark Road",
                MapId = "gloamwood",
                MapName = "Gloamwood Verge",
                TerrainId = "timberroad",
                Description = "A cursed timber road turns every stall into an ambush window for raiders and hexers.",
                RewardGold = 4230,
                RewardFood = 22,
                EntryFoodCost = 12,
                ExploreFoodCost = 15,
                MapX = 500f,
                MapY = 430f,
                PlayerBaseHealth = 1120f,
                EnemyBaseHealth = 3980f,
                EnemySpawnMin = 0.86f,
                EnemySpawnMax = 1.26f,
                EnemyHealthScale = 4.54f,
                EnemyDamageScale = 4.24f,
                WalkerWeight = 0.01f,
                RunnerWeight = 0.05f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.2f,
                CrusherWeight = 0.44f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.42f
            },
            new StageDefinition
            {
                StageNumber = 40,
                StageName = "Snare Grove",
                MapId = "gloamwood",
                MapName = "Gloamwood Verge",
                TerrainId = "grove",
                Description = "Every advance through the grove is punished by snare roots, curse fire, and hidden breach teams.",
                RewardGold = 4510,
                RewardFood = 23,
                EntryFoodCost = 12,
                ExploreFoodCost = 15,
                MapX = 662f,
                MapY = 208f,
                PlayerBaseHealth = 1150f,
                EnemyBaseHealth = 4200f,
                EnemySpawnMin = 0.82f,
                EnemySpawnMax = 1.22f,
                EnemyHealthScale = 4.7f,
                EnemyDamageScale = 4.38f,
                WalkerWeight = 0.01f,
                RunnerWeight = 0.05f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.2f,
                CrusherWeight = 0.46f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.43f
            },
            new StageDefinition
            {
                StageNumber = 41,
                StageName = "Gloamwood Heart",
                MapId = "gloamwood",
                MapName = "Gloamwood Verge",
                TerrainId = "witchcircle",
                Description = "Gloamwood Verge boss stage. Thorn snares and witchfire close the lane until the Grave Lord rises in the heart circle.",
                RewardGold = 4800,
                RewardFood = 24,
                EntryFoodCost = 12,
                ExploreFoodCost = 15,
                MapX = 784f,
                MapY = 560f,
                PlayerBaseHealth = 1185f,
                EnemyBaseHealth = 4440f,
                EnemySpawnMin = 0.78f,
                EnemySpawnMax = 1.18f,
                EnemyHealthScale = 4.88f,
                EnemyDamageScale = 4.52f,
                WalkerWeight = 0.01f,
                RunnerWeight = 0.05f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.2f,
                CrusherWeight = 0.46f,
                BossWeight = 0.42f,
                BossSpawnStartTime = 88f,
                BonusWaveChance = 0.44f
            },
            new StageDefinition
            {
                StageNumber = 42,
                StageName = "Bridge Bastion",
                MapId = "citadel",
                MapName = "Crownfall Citadel",
                TerrainId = "bridgefort",
                Description = "The outer bridge forts lock the lane under mixed support fire and heavy breach pressure.",
                RewardGold = 5100,
                RewardFood = 24,
                EntryFoodCost = 12,
                ExploreFoodCost = 15,
                MapX = 170f,
                MapY = 302f,
                PlayerBaseHealth = 1215f,
                EnemyBaseHealth = 4680f,
                EnemySpawnMin = 0.76f,
                EnemySpawnMax = 1.16f,
                EnemyHealthScale = 5.04f,
                EnemyDamageScale = 4.68f,
                WalkerWeight = 0.01f,
                RunnerWeight = 0.04f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.22f,
                CrusherWeight = 0.46f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.44f
            },
            new StageDefinition
            {
                StageNumber = 43,
                StageName = "Breach Yard",
                MapId = "citadel",
                MapName = "Crownfall Citadel",
                TerrainId = "breachyard",
                Description = "Collapsed walls and siege rubble force a grinding advance against mixed command waves.",
                RewardGold = 5420,
                RewardFood = 25,
                EntryFoodCost = 13,
                ExploreFoodCost = 15,
                MapX = 338f,
                MapY = 514f,
                PlayerBaseHealth = 1245f,
                EnemyBaseHealth = 4940f,
                EnemySpawnMin = 0.74f,
                EnemySpawnMax = 1.12f,
                EnemyHealthScale = 5.22f,
                EnemyDamageScale = 4.82f,
                WalkerWeight = 0.01f,
                RunnerWeight = 0.04f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.22f,
                CrusherWeight = 0.48f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.45f
            },
            new StageDefinition
            {
                StageNumber = 44,
                StageName = "Crownward Gate",
                MapId = "citadel",
                MapName = "Crownfall Citadel",
                TerrainId = "bridgefort",
                Description = "Gatehouse towers and command relays stack pressure while the breach crew tries to crack the crownward wall.",
                RewardGold = 5760,
                RewardFood = 25,
                EntryFoodCost = 13,
                ExploreFoodCost = 16,
                MapX = 496f,
                MapY = 230f,
                PlayerBaseHealth = 1280f,
                EnemyBaseHealth = 5220f,
                EnemySpawnMin = 0.72f,
                EnemySpawnMax = 1.08f,
                EnemyHealthScale = 5.42f,
                EnemyDamageScale = 4.98f,
                WalkerWeight = 0.01f,
                RunnerWeight = 0.04f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.22f,
                CrusherWeight = 0.48f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.46f,
                Objectives = new[]
                {
                    new StageObjectiveDefinition { Type = "clear_route" },
                    new StageObjectiveDefinition { Type = "mission_event_success" },
                    new StageObjectiveDefinition { Type = "enemy_defeats", Value = 60f }
                },
                MissionEvents = new[]
                {
                    new StageMissionEventDefinition
                    {
                        Type = "gate_breach",
                        Title = "Breach Crew",
                        Summary = "Keep the charge line clear long enough for the crew to break the crownward gate braces.",
                        RewardSummary = "Reward: direct gatehouse damage when the breach charge lands.",
                        PenaltySummary = "Risk: the gatehouse regains footing and hull if the charge fails.",
                        XRatio = 0.78f,
                        YRatio = 0.48f,
                        Radius = 76f,
                        TargetSeconds = 7.8f,
                        StartTime = 32f,
                        ColorHex = "ffd166"
                    }
                }
            },
            new StageDefinition
            {
                StageNumber = 45,
                StageName = "Inner Ring",
                MapId = "citadel",
                MapName = "Crownfall Citadel",
                TerrainId = "innerkeep",
                Description = "The inner ring tightens into a final attrition line where heralds, hexers, and raiders converge.",
                RewardGold = 6120,
                RewardFood = 26,
                EntryFoodCost = 13,
                ExploreFoodCost = 16,
                MapX = 648f,
                MapY = 390f,
                PlayerBaseHealth = 1310f,
                EnemyBaseHealth = 5520f,
                EnemySpawnMin = 0.7f,
                EnemySpawnMax = 1.04f,
                EnemyHealthScale = 5.64f,
                EnemyDamageScale = 5.14f,
                WalkerWeight = 0.01f,
                RunnerWeight = 0.04f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.22f,
                CrusherWeight = 0.5f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.47f
            },
            new StageDefinition
            {
                StageNumber = 46,
                StageName = "Crownfall Keep",
                MapId = "citadel",
                MapName = "Crownfall Citadel",
                TerrainId = "innerkeep",
                Description = "Crownfall Citadel boss stage. Mixed command waves and breach lines collapse into the final Grave Lord siege.",
                RewardGold = 6500,
                RewardFood = 27,
                EntryFoodCost = 13,
                ExploreFoodCost = 16,
                MapX = 784f,
                MapY = 548f,
                PlayerBaseHealth = 1350f,
                EnemyBaseHealth = 5860f,
                EnemySpawnMin = 0.68f,
                EnemySpawnMax = 1.0f,
                EnemyHealthScale = 5.88f,
                EnemyDamageScale = 5.32f,
                WalkerWeight = 0.01f,
                RunnerWeight = 0.04f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.22f,
                CrusherWeight = 0.5f,
                BossWeight = 0.44f,
                BossSpawnStartTime = 90f,
                BonusWaveChance = 0.48f
            },
            new StageDefinition
            {
                StageNumber = 47,
                StageName = "Lantern Requiem",
                MapId = "city",
                MapName = "King's Road",
                TerrainId = "night",
                Description = "A late King's Road capstone where faster dead and curse fire return as an endgame remix of the first march.",
                RewardGold = 6900,
                RewardFood = 27,
                EntryFoodCost = 13,
                ExploreFoodCost = 16,
                MapX = 780f,
                MapY = 404f,
                PlayerBaseHealth = 1390f,
                EnemyBaseHealth = 6120f,
                EnemySpawnMin = 0.66f,
                EnemySpawnMax = 0.98f,
                EnemyHealthScale = 6.04f,
                EnemyDamageScale = 5.48f,
                WalkerWeight = 0.01f,
                RunnerWeight = 0.05f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.24f,
                CrusherWeight = 0.5f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.48f
            },
            new StageDefinition
            {
                StageNumber = 48,
                StageName = "Deadwake Armada",
                MapId = "harbor",
                MapName = "Saltwake Docks",
                TerrainId = "shipyard",
                Description = "A late Saltwake capstone where drowned hulks and deck-side pressure come back as a full attrition gauntlet.",
                RewardGold = 7300,
                RewardFood = 28,
                EntryFoodCost = 13,
                ExploreFoodCost = 16,
                MapX = 784f,
                MapY = 184f,
                PlayerBaseHealth = 1430f,
                EnemyBaseHealth = 6420f,
                EnemySpawnMin = 0.64f,
                EnemySpawnMax = 0.96f,
                EnemyHealthScale = 6.24f,
                EnemyDamageScale = 5.66f,
                WalkerWeight = 0.01f,
                RunnerWeight = 0.04f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.24f,
                CrusherWeight = 0.52f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.49f
            },
            new StageDefinition
            {
                StageNumber = 49,
                StageName = "Forge Crown",
                MapId = "foundry",
                MapName = "Emberforge March",
                TerrainId = "foundry",
                Description = "A late Emberforge capstone where furnace hazards, split broods, and sapper dives all peak together.",
                RewardGold = 7720,
                RewardFood = 29,
                EntryFoodCost = 14,
                ExploreFoodCost = 16,
                MapX = 770f,
                MapY = 136f,
                PlayerBaseHealth = 1470f,
                EnemyBaseHealth = 6740f,
                EnemySpawnMin = 0.62f,
                EnemySpawnMax = 0.92f,
                EnemyHealthScale = 6.46f,
                EnemyDamageScale = 5.84f,
                WalkerWeight = 0.01f,
                RunnerWeight = 0.04f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.24f,
                CrusherWeight = 0.52f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.5f
            },
            new StageDefinition
            {
                StageNumber = 50,
                StageName = "Ashen Remnant",
                MapId = "quarantine",
                MapName = "Ashen Ward",
                TerrainId = "blacksite",
                Description = "A late Ashen Ward capstone where purge hazards, hex pressure, and a failing seal circle define the final caravan push.",
                RewardGold = 8160,
                RewardFood = 30,
                EntryFoodCost = 14,
                ExploreFoodCost = 16,
                MapX = 786f,
                MapY = 148f,
                PlayerBaseHealth = 1510f,
                EnemyBaseHealth = 7080f,
                EnemySpawnMin = 0.6f,
                EnemySpawnMax = 0.9f,
                EnemyHealthScale = 6.7f,
                EnemyDamageScale = 6.02f,
                WalkerWeight = 0.01f,
                RunnerWeight = 0.04f,
                BruteWeight = 0.24f,
                SpitterWeight = 0.26f,
                CrusherWeight = 0.54f,
                BossWeight = 0f,
                BossSpawnStartTime = 0f,
                BonusWaveChance = 0.52f,
                Objectives = new[]
                {
                    new StageObjectiveDefinition { Type = "clear_route" },
                    new StageObjectiveDefinition { Type = "hazard_hits_limit", Value = 3f },
                    new StageObjectiveDefinition { Type = "mission_event_success" }
                },
                MissionEvents = new[]
                {
                    new StageMissionEventDefinition
                    {
                        Type = "ritual_site",
                        Title = "Purge Seal",
                        Summary = "Hold the failing seal circle long enough to keep the containment grid from turning on the caravan.",
                        RewardSummary = "Reward: courage surge and cleaner recovery when the seal stabilizes.",
                        PenaltySummary = "Risk: lose card tempo if the seal fails under pressure.",
                        XRatio = 0.46f,
                        YRatio = 0.52f,
                        Radius = 82f,
                        TargetSeconds = 8.8f,
                        StartTime = 36f,
                        ColorHex = "bbf7d0"
                    }
                }
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
