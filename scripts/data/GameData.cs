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
        PlayerBallistaId,
        PlayerStormcallerId
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
        EnemyRevenantCaptainId,
        EnemyPlagueEngineId,
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
        EnemyBossAshenRegentId,
        EnemyBossTidemasterId,
        EnemyBossPlagueMonarchId
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
    public const string PlayerStormcallerId = "player_stormcaller";
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
    public const string EnemyRevenantCaptainId = "enemy_revenant_captain";
    public const string EnemyPlagueEngineId = "enemy_plague_engine";
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
    public const string EnemyBossTidemasterId = "enemy_boss_tidemaster";
    public const string EnemyBossPlagueMonarchId = "enemy_boss_plague_monarch";

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
    private static string _loadError = "";
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

    /// <summary>True when data/*.json could not be read. The game shows an error screen and never saves.</summary>
    public static bool LoadFailed
    {
        get
        {
            EnsureLoaded();
            return _loadError.Length > 0;
        }
    }

    public static string LoadError
    {
        get
        {
            EnsureLoaded();
            return _loadError;
        }
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

    public static UnitDefinition TryGetUnit(string unitId)
    {
        EnsureLoaded();
        return unitId != null && _units.TryGetValue(unitId, out var unit) ? unit : null;
    }

    public static SpellDefinition TryGetSpell(string spellId)
    {
        EnsureLoaded();
        return spellId != null && _spells.TryGetValue(spellId, out var spell) ? spell : null;
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
            MarkLoadFailed(ex.Message);
        }
    }

    // There is deliberately no built-in copy of the data: a stale duplicate would quietly
    // replace the campaign, and saving over it would truncate the player's progress.
    private static void MarkLoadFailed(string reason)
    {
        _loadError = string.IsNullOrWhiteSpace(reason) ? "Unknown error." : reason.Trim();
        _stages = Array.Empty<StageDefinition>();
        _units = new Dictionary<string, UnitDefinition>(StringComparer.OrdinalIgnoreCase);
        _spells = new Dictionary<string, SpellDefinition>(StringComparer.OrdinalIgnoreCase);
        _equipment = new Dictionary<string, EquipmentDefinition>(StringComparer.OrdinalIgnoreCase);
        _combat = new CombatTuning();
        _combat.Normalize();
        GD.PushError($"Failed to load game data from JSON. Saving is disabled for this session. {_loadError}");
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

    private static string NormalizeMapId(string mapId)
    {
        return string.IsNullOrWhiteSpace(mapId)
            ? "city"
            : mapId.Trim().ToLowerInvariant();
    }
}
