using System;
using System.Collections.Generic;
using Godot;

public sealed class BattleSpawnDirector
{
    private sealed class PendingEnemySpawn
    {
        public PendingEnemySpawn(UnitDefinition definition, float executeAt)
        {
            Definition = definition;
            ExecuteAt = executeAt;
        }

        public UnitDefinition Definition { get; }
        public float ExecuteAt { get; }
    }

    private readonly Queue<PendingEnemySpawn> _pendingEnemySpawns = new();
    private readonly List<UnitDefinition> _enemyRoster = new();
    private readonly RandomNumberGenerator _rng;

    private CombatTuning _combat = new();
    private StageDefinition _stageData = null!;
    private int _stage;
    private float _enemySpawnTimer;
    private bool _isEndlessMode;
    private float _additionalEnemyHealthScale = 1f;
    private float _additionalEnemyDamageScale = 1f;
    private string _endlessRouteId = "city";
    private string _endlessRouteForkId = EndlessRouteForkCatalog.MainlinePushId;
    private int _endlessSegmentStartWave = 1;
    private float _nextEndlessWaveTime;
    private int _endlessTradeoffEnemyCapModifier;

    public BattleSpawnDirector(RandomNumberGenerator rng)
    {
        _rng = rng;
    }

    public bool IsEndlessMode => _isEndlessMode;
    public bool UsesScriptedWaves => _stageData != null && _stageData.HasScriptedWaves;
    public int NextScriptedWaveIndex { get; private set; }
    public int TotalScriptedWaves => _stageData?.Waves.Length ?? 0;
    public int PendingSpawnCount => _pendingEnemySpawns.Count;
    public int EndlessWaveNumber { get; private set; }
    public float NextEndlessWaveTime => _nextEndlessWaveTime;
    public bool EndlessCheckpointPending { get; private set; }
    public bool EndlessBossCheckpointPending => _isEndlessMode && EndlessCheckpointPending && EndlessBossCheckpointCatalog.IsBossCheckpointWave(EndlessWaveNumber);
    public string EndlessRouteForkId => _endlessRouteForkId;
    public string EndlessSegmentEventLabel { get; private set; } = "";

    public void Initialize(int stage, StageDefinition stageData, CombatTuning combat, IEnumerable<UnitDefinition> enemyRoster)
    {
        _isEndlessMode = false;
        _stage = stage;
        _stageData = stageData;
        _combat = combat;
        _enemySpawnTimer = _combat.InitialEnemySpawnDelay;
        NextScriptedWaveIndex = 0;
        EndlessWaveNumber = 0;
        _nextEndlessWaveTime = 0f;
        EndlessCheckpointPending = false;
        _endlessRouteId = NormalizeRouteId(stageData?.MapId);
        _endlessRouteForkId = EndlessRouteForkCatalog.MainlinePushId;
        _endlessSegmentStartWave = 1;
        EndlessSegmentEventLabel = ResolveEndlessSegmentEventLabel(_endlessRouteForkId);
        _endlessTradeoffEnemyCapModifier = 0;
        _additionalEnemyHealthScale = 1f;
        _additionalEnemyDamageScale = 1f;

        _enemyRoster.Clear();
        _enemyRoster.AddRange(enemyRoster);

        _pendingEnemySpawns.Clear();
    }

    public void InitializeEndless(string routeId, StageDefinition stageData, CombatTuning combat, IEnumerable<UnitDefinition> enemyRoster)
    {
        _isEndlessMode = true;
        _endlessRouteId = NormalizeRouteId(routeId);
        _stage = Math.Max(1, stageData.StageNumber);
        _stageData = stageData;
        _combat = combat;
        _enemySpawnTimer = 0f;
        NextScriptedWaveIndex = 0;
        EndlessWaveNumber = 0;
        _nextEndlessWaveTime = _combat.InitialEnemySpawnDelay;
        EndlessCheckpointPending = false;
        _endlessRouteForkId = EndlessRouteForkCatalog.MainlinePushId;
        _endlessSegmentStartWave = 1;
        EndlessSegmentEventLabel = ResolveEndlessSegmentEventLabel(_endlessRouteForkId);
        _endlessTradeoffEnemyCapModifier = 0;
        _additionalEnemyHealthScale = 1f;
        _additionalEnemyDamageScale = 1f;

        _enemyRoster.Clear();
        _enemyRoster.AddRange(enemyRoster);

        _pendingEnemySpawns.Clear();
    }

    public void Tick(
        float delta,
        float elapsed,
        Func<int> getActiveEnemyCount,
        Action<UnitStats, Vector2> spawnEnemy,
        Action<string> setStatus)
    {
        if (_isEndlessMode)
        {
            TickEndless(elapsed, getActiveEnemyCount, spawnEnemy, setStatus);
            return;
        }

        if (UsesScriptedWaves)
        {
            TriggerScriptedWaves(elapsed, setStatus);
            FlushPendingEnemySpawns(elapsed, getActiveEnemyCount, spawnEnemy);
            return;
        }

        _enemySpawnTimer -= delta;
        if (_enemySpawnTimer > 0f)
        {
            return;
        }

        SpawnWeightedWave(getActiveEnemyCount, spawnEnemy, elapsed);

        var pressureTimeScale = Mathf.Max(1f, _combat.EnemySpawnPressureTimeScale);
        var pressure = Mathf.Clamp(
            1f + (elapsed / pressureTimeScale),
            _combat.EnemySpawnPressureMin,
            _combat.EnemySpawnPressureMax);
        var spawnIntervalScale = StageModifiers.ResolveEnemySpawnIntervalScale(_stageData);
        _enemySpawnTimer = Mathf.Max(
            _combat.EnemySpawnIntervalFloor,
            (_rng.RandfRange(_stageData.EnemySpawnMin, _stageData.EnemySpawnMax) * spawnIntervalScale) / pressure);
    }

    public int GetMaxActiveEnemies()
    {
        if (_isEndlessMode)
        {
            return _combat.GetMaxActiveEnemies(_stage) +
                StageModifiers.ResolveEnemyCapBonus(_stageData) +
                Math.Min(10, Math.Max(0, EndlessWaveNumber / 2)) +
                ResolveEndlessEnemyCapModifier() +
                _endlessTradeoffEnemyCapModifier;
        }

        return _combat.GetMaxActiveEnemies(_stage) + StageModifiers.ResolveEnemyCapBonus(_stageData);
    }

    public bool TryGetNextScriptedWave(out StageWaveDefinition wave)
    {
        if (UsesScriptedWaves && NextScriptedWaveIndex < _stageData.Waves.Length)
        {
            wave = _stageData.Waves[NextScriptedWaveIndex];
            return true;
        }

        wave = null!;
        return false;
    }

    public bool TryBuildEnemyStats(string unitId, out UnitStats stats)
    {
        if (TryGetEnemyById(unitId, out var definition))
        {
            stats = BuildEnemyStats(definition);
            return true;
        }

        stats = null!;
        return false;
    }

    public void SetEnemyScaleModifiers(float healthScale, float damageScale)
    {
        _additionalEnemyHealthScale = Mathf.Max(0.5f, healthScale);
        _additionalEnemyDamageScale = Mathf.Max(0.5f, damageScale);
    }

    private void SpawnWeightedWave(Func<int> getActiveEnemyCount, Action<UnitStats, Vector2> spawnEnemy, float elapsed)
    {
        if (getActiveEnemyCount() >= GetMaxActiveEnemies())
        {
            return;
        }

        SpawnDefinition(PickEnemyDefinition(elapsed), spawnEnemy);

        if (_stageData.BonusWaveChance <= 0f || _rng.Randf() >= _stageData.BonusWaveChance)
        {
            return;
        }

        if (getActiveEnemyCount() >= GetMaxActiveEnemies())
        {
            return;
        }

        SpawnDefinition(PickEnemyDefinition(elapsed), spawnEnemy);
    }

    private void TriggerScriptedWaves(float elapsed, Action<string> setStatus)
    {
        while (NextScriptedWaveIndex < _stageData.Waves.Length)
        {
            var wave = _stageData.Waves[NextScriptedWaveIndex];
            if (elapsed + 0.001f < wave.TriggerTime)
            {
                return;
            }

            QueueScriptedWave(elapsed, wave);
            var label = string.IsNullOrWhiteSpace(wave.Label)
                ? $"Wave {NextScriptedWaveIndex + 1}"
                : wave.Label;
            setStatus($"Enemy wave {NextScriptedWaveIndex + 1} incoming: {label}.");
            NextScriptedWaveIndex++;
        }
    }

    private void QueueScriptedWave(float elapsed, StageWaveDefinition wave)
    {
        var executeAt = Mathf.Max(elapsed, wave.TriggerTime);
        var spawnInterval = Mathf.Max(0.1f, wave.SpawnInterval);

        foreach (var entry in wave.Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.UnitId))
            {
                continue;
            }

            var enemyDefinition = GetEnemyById(entry.UnitId);
            var count = Math.Max(1, entry.Count);
            for (var i = 0; i < count; i++)
            {
                _pendingEnemySpawns.Enqueue(new PendingEnemySpawn(enemyDefinition, executeAt));
                executeAt += spawnInterval;
            }
        }
    }

    private void FlushPendingEnemySpawns(
        float elapsed,
        Func<int> getActiveEnemyCount,
        Action<UnitStats, Vector2> spawnEnemy)
    {
        while (_pendingEnemySpawns.Count > 0)
        {
            if (_pendingEnemySpawns.Peek().ExecuteAt > elapsed)
            {
                return;
            }

            if (getActiveEnemyCount() >= GetMaxActiveEnemies())
            {
                return;
            }

            var pendingSpawn = _pendingEnemySpawns.Dequeue();
            SpawnDefinition(pendingSpawn.Definition, spawnEnemy);
        }
    }

    private void TickEndless(
        float elapsed,
        Func<int> getActiveEnemyCount,
        Action<UnitStats, Vector2> spawnEnemy,
        Action<string> setStatus)
    {
        FlushPendingEnemySpawns(elapsed, getActiveEnemyCount, spawnEnemy);
        if (EndlessCheckpointPending)
        {
            return;
        }

        while (elapsed + 0.001f >= _nextEndlessWaveTime)
        {
            EndlessWaveNumber++;
            QueueEndlessWave(_nextEndlessWaveTime, EndlessWaveNumber);
            if (EndlessBossCheckpointCatalog.IsBossCheckpointWave(EndlessWaveNumber))
            {
                var bossCheckpoint = EndlessBossCheckpointCatalog.GetForRoute(_endlessRouteId);
                setStatus($"Boss checkpoint wave {EndlessWaveNumber}: {bossCheckpoint.Title} incoming. {bossCheckpoint.Summary}");
            }
            else
            {
                setStatus($"Endless wave {EndlessWaveNumber} incoming. Hold the lane.");
            }

            if (EndlessWaveNumber % 5 == 0)
            {
                EndlessCheckpointPending = true;
                return;
            }

            var cadence = ResolveEndlessCadence();
            _nextEndlessWaveTime += cadence;
        }
    }

    public void ResumeEndlessAfterCheckpoint(float elapsed)
    {
        if (!_isEndlessMode)
        {
            return;
        }

        EndlessCheckpointPending = false;
        var cadence = ResolveEndlessCadence();
        _nextEndlessWaveTime = elapsed + cadence;
    }

    public void SetEndlessRouteFork(string forkId)
    {
        _endlessRouteForkId = EndlessRouteForkCatalog.Normalize(forkId);
        _endlessSegmentStartWave = Math.Max(1, EndlessWaveNumber + 1);
        EndlessSegmentEventLabel = ResolveEndlessSegmentEventLabel(_endlessRouteForkId);
    }

    public void ResetEndlessSegmentTradeoffs()
    {
        _endlessTradeoffEnemyCapModifier = 0;
    }

    public void SetEndlessTradeoffEnemyCapModifier(int value)
    {
        _endlessTradeoffEnemyCapModifier = value;
    }

    public void AdvanceNextEndlessWave(float elapsed, float seconds)
    {
        if (!_isEndlessMode || seconds <= 0f)
        {
            return;
        }

        _nextEndlessWaveTime = Math.Max(elapsed + 0.75f, _nextEndlessWaveTime - seconds);
    }

    private void QueueEndlessWave(float executeAt, int waveNumber)
    {
        var spawnInterval = Mathf.Clamp(0.54f - (waveNumber * 0.012f), 0.18f, 0.54f);
        var remainingBudget = 4 + waveNumber + (waveNumber / 3);

        while (remainingBudget > 0)
        {
            var definition = PickEndlessEnemyDefinition(waveNumber);
            QueuePendingSpawn(definition, ref executeAt, spawnInterval);
            remainingBudget -= GetEndlessEnemyCost(definition.Id);
        }

        if (_endlessRouteId == RouteCatalog.CityId && waveNumber >= 3 && waveNumber % 3 == 0)
        {
            QueuePendingSpawn(GetEnemyById(GameData.EnemySpitterId), ref executeAt, spawnInterval);
        }

        if (_endlessRouteId == RouteCatalog.HarborId && waveNumber >= 4 && waveNumber % 3 == 0)
        {
            QueuePendingSpawn(GetEnemyById(GameData.EnemyBloaterId), ref executeAt, spawnInterval);
        }

        if (_endlessRouteId == RouteCatalog.HarborId && waveNumber >= 5 && waveNumber % 4 == 1)
        {
            QueuePendingSpawn(GetEnemyById(GameData.EnemyHowlerId), ref executeAt, spawnInterval * 0.9f);
        }

        if (_endlessRouteId == RouteCatalog.FoundryId && waveNumber >= 4 && waveNumber % 3 == 0)
        {
            QueuePendingSpawn(GetEnemyById(GameData.EnemySplitterId), ref executeAt, spawnInterval * 0.96f);
        }

        if (_endlessRouteId == RouteCatalog.FoundryId && waveNumber >= 5 && waveNumber % 3 == 2)
        {
            QueuePendingSpawn(GetEnemyById(GameData.EnemyHowlerId), ref executeAt, spawnInterval * 0.88f);
        }

        if (_endlessRouteId == RouteCatalog.FoundryId && waveNumber >= 6 && waveNumber % 4 == 1)
        {
            QueuePendingSpawn(GetEnemyById(GameData.EnemySaboteurId), ref executeAt, spawnInterval * 0.82f);
        }

        if (_endlessRouteId == RouteCatalog.QuarantineId && waveNumber >= 4 && waveNumber % 3 == 1)
        {
            QueuePendingSpawn(GetEnemyById(GameData.EnemySpitterId), ref executeAt, spawnInterval * 0.9f);
        }

        if (_endlessRouteId == RouteCatalog.QuarantineId && waveNumber >= 5 && waveNumber % 4 == 0)
        {
            QueuePendingSpawn(GetEnemyById(GameData.EnemyHowlerId), ref executeAt, spawnInterval * 0.94f);
        }

        if (_endlessRouteId == RouteCatalog.QuarantineId && waveNumber >= 7 && waveNumber % 5 == 1)
        {
            QueuePendingSpawn(GetEnemyById(GameData.EnemyJammerId), ref executeAt, spawnInterval * 0.9f);
        }

        if (_endlessRouteId == RouteCatalog.QuarantineId && waveNumber >= 6 && waveNumber % 4 == 2)
        {
            QueuePendingSpawn(GetEnemyById(GameData.EnemySaboteurId), ref executeAt, spawnInterval * 0.82f);
        }

        if (waveNumber >= 5 && waveNumber % 4 == 0)
        {
            var supportId = _endlessRouteId switch
            {
                RouteCatalog.HarborId => GameData.EnemyCrusherId,
                RouteCatalog.FoundryId => GameData.EnemyCrusherId,
                RouteCatalog.QuarantineId => GameData.EnemyHowlerId,
                _ => GameData.EnemySplitterId
            };
            QueuePendingSpawn(GetEnemyById(supportId), ref executeAt, spawnInterval);

            if (_endlessRouteId == RouteCatalog.FoundryId && waveNumber >= 8)
            {
                QueuePendingSpawn(GetEnemyById(GameData.EnemyBruteId), ref executeAt, spawnInterval * 1.08f);
            }

            if (_endlessRouteId == RouteCatalog.QuarantineId && waveNumber >= 8)
            {
                QueuePendingSpawn(GetEnemyById(GameData.EnemyJammerId), ref executeAt, spawnInterval * 0.96f);
            }
        }

        if (EndlessBossCheckpointCatalog.IsBossCheckpointWave(waveNumber))
        {
            QueueEndlessBossCheckpointWave(waveNumber, ref executeAt, spawnInterval);
        }
        else if (waveNumber >= 8 && waveNumber % 8 == 0)
        {
            QueuePendingSpawn(GetEnemyById(GameData.EnemyBossId), ref executeAt, spawnInterval * 1.4f);
        }

        QueueRouteForkEventWave(waveNumber, ref executeAt, spawnInterval);
    }

    private void QueuePendingSpawn(UnitDefinition definition, ref float executeAt, float spawnInterval)
    {
        _pendingEnemySpawns.Enqueue(new PendingEnemySpawn(definition, executeAt));
        executeAt += spawnInterval * _rng.RandfRange(0.88f, 1.14f);
    }

    private void QueueRouteForkEventWave(int waveNumber, ref float executeAt, float spawnInterval)
    {
        var segmentWave = Math.Max(1, waveNumber - _endlessSegmentStartWave + 1);

        switch (_endlessRouteForkId)
        {
            case EndlessRouteForkCatalog.MainlinePushId:
                QueueMainlinePushWave(segmentWave, waveNumber, ref executeAt, spawnInterval);
                break;
            case EndlessRouteForkCatalog.ScavengeDetourId:
                QueueScavengeDetourWave(segmentWave, waveNumber, ref executeAt, spawnInterval);
                break;
            case EndlessRouteForkCatalog.FortifiedBlockId:
                QueueFortifiedBlockWave(segmentWave, waveNumber, ref executeAt, spawnInterval);
                break;
        }
    }

    private void QueueMainlinePushWave(int segmentWave, int waveNumber, ref float executeAt, float spawnInterval)
    {
        if (segmentWave <= 2)
        {
            var runnerCount = 1 + (waveNumber >= 12 ? 1 : 0);
            QueueEnemyCopies(GameData.EnemyRunnerId, runnerCount, ref executeAt, spawnInterval * 0.82f);
        }

        if (segmentWave == 3 || segmentWave == 5)
        {
            QueueEnemyCopies(GameData.EnemySpitterId, 1, ref executeAt, spawnInterval * 0.95f);
        }

        if (segmentWave == 4 && waveNumber >= 8)
        {
            QueueEnemyCopies(GameData.EnemyHowlerId, 1, ref executeAt, spawnInterval);
        }

        if (segmentWave == 5 && waveNumber >= 10)
        {
            QueueEnemyCopies(GameData.EnemySplitterId, 1, ref executeAt, spawnInterval * 1.05f);
        }
    }

    private void QueueScavengeDetourWave(int segmentWave, int waveNumber, ref float executeAt, float spawnInterval)
    {
        if (segmentWave == 1)
        {
            QueueEnemyCopies(GameData.EnemyBloaterId, 1, ref executeAt, spawnInterval);
            QueueEnemyCopies(GameData.EnemyWalkerId, 2, ref executeAt, spawnInterval * 0.9f);
        }

        if (segmentWave == 3)
        {
            QueueEnemyCopies(GameData.EnemyBruteId, 1, ref executeAt, spawnInterval);
        }

        if (segmentWave == 4 && waveNumber >= 9)
        {
            QueueEnemyCopies(GameData.EnemyHowlerId, 1, ref executeAt, spawnInterval);
        }

        if (segmentWave == 5 && waveNumber >= 10)
        {
            var heavyId = waveNumber >= 15 ? GameData.EnemyCrusherId : GameData.EnemyBruteId;
            QueueEnemyCopies(heavyId, 1, ref executeAt, spawnInterval * 1.08f);
        }
    }

    private void QueueFortifiedBlockWave(int segmentWave, int waveNumber, ref float executeAt, float spawnInterval)
    {
        if (segmentWave == 1)
        {
            QueueEnemyCopies(GameData.EnemyCrusherId, 1, ref executeAt, spawnInterval * 1.12f);
            QueueEnemyCopies(GameData.EnemyWalkerId, 2, ref executeAt, spawnInterval * 0.94f);
        }

        if (segmentWave == 3)
        {
            QueueEnemyCopies(GameData.EnemyWalkerId, 3, ref executeAt, spawnInterval * 0.88f);
        }

        if (segmentWave == 2 && waveNumber >= 8)
        {
            QueueEnemyCopies(GameData.EnemyHowlerId, 1, ref executeAt, spawnInterval * 1.02f);
        }

        if (segmentWave == 5 && waveNumber >= 10)
        {
            QueueEnemyCopies(GameData.EnemyBruteId, 1, ref executeAt, spawnInterval);
        }

        if (segmentWave == 4 && waveNumber >= 11 && _endlessRouteId == RouteCatalog.QuarantineId)
        {
            QueueEnemyCopies(GameData.EnemyJammerId, 1, ref executeAt, spawnInterval * 0.98f);
        }
    }

    private void QueueEnemyCopies(string unitId, int count, ref float executeAt, float spawnInterval)
    {
        var definition = GetEnemyById(unitId);
        for (var i = 0; i < count; i++)
        {
            QueuePendingSpawn(definition, ref executeAt, spawnInterval);
        }
    }

    private void QueueEndlessBossCheckpointWave(int waveNumber, ref float executeAt, float spawnInterval)
    {
        var definition = EndlessBossCheckpointCatalog.GetForRoute(_endlessRouteId);
        if (definition.EscortEntries.Length > 0)
        {
            var leadEscort = definition.EscortEntries[0];
            QueueEnemyCopies(
                leadEscort.UnitId,
                Math.Max(1, leadEscort.Count + (waveNumber >= 30 ? 1 : 0)),
                ref executeAt,
                spawnInterval * 0.9f);
        }

        QueueEnemyCopies(GameData.EnemyBossId, 1, ref executeAt, spawnInterval * 1.18f);

        for (var i = 1; i < definition.EscortEntries.Length; i++)
        {
            var escort = definition.EscortEntries[i];
            var count = Math.Max(1, escort.Count);
            if (waveNumber >= 30 && i == definition.EscortEntries.Length - 1)
            {
                count++;
            }

            QueueEnemyCopies(escort.UnitId, count, ref executeAt, spawnInterval * 0.98f);
        }
    }

    private void SpawnDefinition(UnitDefinition definition, Action<UnitStats, Vector2> spawnEnemy)
    {
        var spawnY = _rng.RandfRange(
            _combat.BattlefieldTop + _combat.SpawnVerticalPadding,
            _combat.BattlefieldBottom - _combat.SpawnVerticalPadding);
        spawnEnemy(
            BuildEnemyStats(definition),
            new Vector2(_combat.EnemySpawnX, spawnY));
    }

    private UnitStats BuildEnemyStats(UnitDefinition source)
    {
        var healthScale = _stageData.EnemyHealthScale * _additionalEnemyHealthScale;
        var damageScale = _stageData.EnemyDamageScale * _additionalEnemyDamageScale;
        var cooldownReduction = (_stage - 1) * 0.05f;
        var baseDamageBonus = (_stage - 1) * 2;

        if (_isEndlessMode)
        {
            var waveFactor = Math.Max(0, EndlessWaveNumber - 1);
            healthScale *= 1f + (waveFactor * 0.07f);
            damageScale *= 1f + (waveFactor * 0.05f);
            cooldownReduction += waveFactor * 0.025f;
            baseDamageBonus += waveFactor * 2;
        }

        return new UnitStats(
            source,
            healthScale,
            damageScale,
            cooldownReduction,
            baseDamageBonus);
    }

    private UnitDefinition PickEndlessEnemyDefinition(int waveNumber)
    {
        var walkerWeight = 7f;
        var runnerWeight = waveNumber >= 2
            ? (_endlessRouteId switch
            {
                RouteCatalog.CityId => 4.6f,
                RouteCatalog.HarborId => 3.1f,
                RouteCatalog.FoundryId => 2.4f,
                RouteCatalog.QuarantineId => 3.2f,
                _ => 3.4f
            }) + (waveNumber * 0.12f)
            : 0f;
        var bloaterWeight = waveNumber >= 4
            ? (_endlessRouteId switch
            {
                RouteCatalog.HarborId => 3.6f,
                RouteCatalog.FoundryId => 1.5f,
                RouteCatalog.QuarantineId => 1.2f,
                _ => 1.8f
            })
            : 0f;
        var bruteWeight = waveNumber >= 3 ? 2.5f + (waveNumber * 0.08f) : 0f;
        var saboteurWeight = waveNumber >= 5
            ? (_endlessRouteId switch
            {
                RouteCatalog.FoundryId => 2.6f,
                RouteCatalog.QuarantineId => 2.4f,
                _ => 0.9f
            }) + (waveNumber * 0.03f)
            : 0f;
        var howlerWeight = waveNumber >= 4
            ? (_endlessRouteId switch
            {
                RouteCatalog.HarborId => 2.2f,
                RouteCatalog.FoundryId => 2.8f,
                RouteCatalog.QuarantineId => 3.4f,
                _ => 0.8f
            }) + (waveNumber * 0.03f)
            : 0f;
        var jammerWeight = waveNumber >= 6
            ? (_endlessRouteId switch
            {
                RouteCatalog.QuarantineId => 2.7f,
                RouteCatalog.FoundryId => 0.8f,
                _ => 0.2f
            }) + (waveNumber * 0.02f)
            : 0f;
        var spitterWeight = waveNumber >= 3
            ? (_endlessRouteId switch
            {
                RouteCatalog.CityId => 3.6f,
                RouteCatalog.FoundryId => 1.8f,
                RouteCatalog.QuarantineId => 4.1f,
                _ => 2.2f
            }) + (waveNumber * 0.05f)
            : 0f;
        var splitterWeight = waveNumber >= 6
            ? (_endlessRouteId switch
            {
                RouteCatalog.HarborId => 2.9f,
                RouteCatalog.FoundryId => 3.9f,
                RouteCatalog.QuarantineId => 1.4f,
                _ => 2.1f
            }) + (waveNumber * 0.04f)
            : 0f;
        var crusherWeight = waveNumber >= 5 ? 2.1f + (waveNumber * 0.05f) : 0f;

        if (_endlessRouteId == RouteCatalog.FoundryId)
        {
            bruteWeight *= 1.18f;
            crusherWeight *= 1.28f;
            runnerWeight *= 0.92f;
            saboteurWeight *= 1.25f;
            howlerWeight *= 1.2f;
        }

        if (_endlessRouteId == RouteCatalog.QuarantineId)
        {
            bloaterWeight *= 0.82f;
            bruteWeight *= 0.94f;
            saboteurWeight *= 1.22f;
            howlerWeight *= 1.3f;
            jammerWeight *= 1.34f;
            spitterWeight *= 1.35f;
            splitterWeight *= 0.72f;
            crusherWeight *= 0.92f;
        }

        switch (_endlessRouteForkId)
        {
            case EndlessRouteForkCatalog.MainlinePushId:
                runnerWeight *= 1.35f;
                spitterWeight *= 1.25f;
                bloaterWeight *= 0.85f;
                bruteWeight *= 0.9f;
                saboteurWeight *= 1.18f;
                howlerWeight *= 0.92f;
                jammerWeight *= 0.94f;
                break;
            case EndlessRouteForkCatalog.ScavengeDetourId:
                bloaterWeight *= 1.35f;
                bruteWeight *= 1.3f;
                runnerWeight *= 0.92f;
                spitterWeight *= 0.9f;
                saboteurWeight *= 0.88f;
                howlerWeight *= 0.94f;
                jammerWeight *= 0.82f;
                break;
            case EndlessRouteForkCatalog.FortifiedBlockId:
                runnerWeight *= 0.8f;
                spitterWeight *= 0.8f;
                crusherWeight *= 1.15f;
                saboteurWeight *= 1.08f;
                howlerWeight *= 1.28f;
                jammerWeight *= 1.22f;
                break;
        }

        var total = walkerWeight + runnerWeight + bloaterWeight + bruteWeight + saboteurWeight + howlerWeight + jammerWeight + spitterWeight + splitterWeight + crusherWeight;
        if (total <= 0f)
        {
            return GetEnemyById(GameData.EnemyWalkerId);
        }

        var roll = _rng.RandfRange(0f, total);
        if (roll < walkerWeight)
        {
            return GetEnemyById(GameData.EnemyWalkerId);
        }

        roll -= walkerWeight;
        if (roll < runnerWeight)
        {
            return GetEnemyById(GameData.EnemyRunnerId);
        }

        roll -= runnerWeight;
        if (roll < bloaterWeight)
        {
            return GetEnemyById(GameData.EnemyBloaterId);
        }

        roll -= bloaterWeight;
        if (roll < bruteWeight)
        {
            return GetEnemyById(GameData.EnemyBruteId);
        }

        roll -= bruteWeight;
        if (roll < saboteurWeight)
        {
            return GetEnemyById(GameData.EnemySaboteurId);
        }

        roll -= saboteurWeight;
        if (roll < howlerWeight)
        {
            return GetEnemyById(GameData.EnemyHowlerId);
        }

        roll -= howlerWeight;
        if (roll < jammerWeight)
        {
            return GetEnemyById(GameData.EnemyJammerId);
        }

        roll -= jammerWeight;
        if (roll < spitterWeight)
        {
            return GetEnemyById(GameData.EnemySpitterId);
        }

        roll -= spitterWeight;
        if (roll < splitterWeight)
        {
            return GetEnemyById(GameData.EnemySplitterId);
        }

        return GetEnemyById(GameData.EnemyCrusherId);
    }

    private UnitDefinition PickEnemyDefinition(float elapsed)
    {
        var walkerWeight = Mathf.Max(0f, _stageData.WalkerWeight);
        var runnerWeight = Mathf.Max(0f, _stageData.RunnerWeight);
        var bruteWeight = Mathf.Max(0f, _stageData.BruteWeight);
        var spitterWeight = Mathf.Max(0f, _stageData.SpitterWeight);
        var crusherWeight = Mathf.Max(0f, _stageData.CrusherWeight);
        var bossWeight = Mathf.Max(0f, _stageData.BossWeight);
        if (elapsed < Mathf.Max(0f, _stageData.BossSpawnStartTime))
        {
            bossWeight = 0f;
        }

        var total = walkerWeight + runnerWeight + bruteWeight + spitterWeight + crusherWeight + bossWeight;
        if (total <= 0f)
        {
            return _enemyRoster[0];
        }

        var roll = _rng.RandfRange(0f, total);
        if (roll < walkerWeight)
        {
            return GetEnemyById(GameData.EnemyWalkerId);
        }

        roll -= walkerWeight;
        if (roll < runnerWeight)
        {
            return GetEnemyById(GameData.EnemyRunnerId);
        }

        roll -= runnerWeight;
        if (roll < bruteWeight)
        {
            return GetEnemyById(GameData.EnemyBruteId);
        }

        roll -= bruteWeight;
        if (roll < spitterWeight)
        {
            return GetEnemyById(GameData.EnemySpitterId);
        }

        roll -= spitterWeight;
        if (roll < crusherWeight)
        {
            return GetEnemyById(GameData.EnemyCrusherId);
        }

        if (bossWeight <= 0f)
        {
            return GetEnemyById(GameData.EnemyCrusherId);
        }

        return GetEnemyById(GameData.EnemyBossId);
    }

    private UnitDefinition GetEnemyById(string id)
    {
        if (TryGetEnemyById(id, out var definition))
        {
            return definition;
        }

        return _enemyRoster[0];
    }

    private bool TryGetEnemyById(string id, out UnitDefinition definition)
    {
        for (var i = 0; i < _enemyRoster.Count; i++)
        {
            if (_enemyRoster[i].Id == id)
            {
                definition = _enemyRoster[i];
                return true;
            }
        }

        definition = null!;
        return false;
    }

    private static int GetEndlessEnemyCost(string unitId)
    {
        return unitId switch
        {
            GameData.EnemyWalkerId => 1,
            GameData.EnemyRunnerId => 1,
            GameData.EnemyBloaterId => 2,
            GameData.EnemyBruteId => 2,
            GameData.EnemySpitterId => 2,
            GameData.EnemySaboteurId => 2,
            GameData.EnemyHowlerId => 2,
            GameData.EnemyJammerId => 2,
            GameData.EnemySplitterId => 3,
            GameData.EnemyCrusherId => 3,
            GameData.EnemyBossId => 6,
            _ => 1
        };
    }

    private static string NormalizeRouteId(string routeId)
    {
        return RouteCatalog.Normalize(routeId);
    }

    private float ResolveEndlessCadence()
    {
        var cadence = Mathf.Clamp(9.5f - (EndlessWaveNumber * 0.22f), 4.2f, 9.5f);
        return _endlessRouteForkId switch
        {
            EndlessRouteForkCatalog.MainlinePushId => cadence * 0.88f,
            EndlessRouteForkCatalog.ScavengeDetourId => cadence * 1.08f,
            EndlessRouteForkCatalog.FortifiedBlockId => cadence * 1.12f,
            _ => cadence
        };
    }

    private int ResolveEndlessEnemyCapModifier()
    {
        return _endlessRouteForkId switch
        {
            EndlessRouteForkCatalog.MainlinePushId => 1,
            EndlessRouteForkCatalog.FortifiedBlockId => -1,
            _ => 0
        };
    }

    private static string ResolveEndlessSegmentEventLabel(string forkId)
    {
        return EndlessRouteForkCatalog.Normalize(forkId) switch
        {
            EndlessRouteForkCatalog.MainlinePushId => "Redline Interchange: runner and spitter vanguards hit the opening waves.",
            EndlessRouteForkCatalog.ScavengeDetourId => "Wreckyard Loop: bloater and brute salvage pockets clog the lane.",
            EndlessRouteForkCatalog.FortifiedBlockId => "Safehouse Ring: slower crusher-led blockades anchor each segment.",
            _ => "Convoy route event pending."
        };
    }
}
