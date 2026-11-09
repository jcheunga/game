using System;
using System.Linq;
using Godot;

public partial class BattleController
{
    private int _rushCleared;
    private bool _rushWaveActive;
    private float _rushNextWave=3;
    private float _rushNextReinforcement;
    private int _rushReinforcementsLeft;
    private bool IsBossRushMode=>_battleMode==BattleRunMode.BossRush;

    private static StageDefinition RushStage(int index)
    {
        var wave=BossRushCatalog.Waves[index];var original=GameData.GetStage(wave.SourceStage);
        return new StageDefinition {StageNumber=wave.SourceStage,StageName=$"Boss rush {index+1}/14 · {wave.BossName}",MapId="city",MapName="Boss rush",
            TerrainId="urban",EnemyLevel=original.EnemyLevel,EnemyBaseHealth=100000,PlayerBaseHealth=400,EntryFoodCost=0,RewardFood=0,RewardGold=0};
    }

    private void TickBossRush(float delta)
    {
        if(_battleEnded)return;
        if(_playerBaseHealth<=0){EndBattle(false);return;}
        if(_rushWaveActive)
        {
            if(_units.Any(u=>u.Team==Team.Enemy && !u.IsDead && !u.IsSummoned)){TickRushReinforcements();return;}
            _rushCleared++;_rushWaveActive=false;
            if(_rushCleared==BossRushCatalog.TotalWaves){EndBattle(true);return;}
            foreach(var unit in _units.ToArray())unit.Despawn();CleanupDeadUnits();
            _pendingBossPhases.Clear();_targetLocks.Clear();
            foreach(var field in _contentFields)field.QueueFree();_contentFields.Clear();
            _courage=Math.Min(_maxCourage,40);_playerBaseHealth=_playerBaseMaxHealth;
            _mana=Mathf.Min(_maxMana,Mathf.Max(_mana,_combat.ManaStart));
            _rushNextWave=_elapsed+5;
            SetStatus($"Commander {_rushCleared}/14 defeated. Regrouping for {BossRushCatalog.Waves[_rushCleared].BossName}.");
            return;
        }
        if(_elapsed<_rushNextWave)return;
        _stageData=RushStage(_rushCleared);_stage=_stageData.StageNumber;
        _spawnDirector.Initialize(_stage,_stageData,_combat,GameData.GetEnemyUnits());
        var wave=BossRushCatalog.Waves[_rushCleared];
        foreach(var id in new[]{wave.BossUnitId}.Concat(wave.EscortUnitIds))
        {
            var def=GameData.GetUnit(id);var level=_stageData.EnemyLevel;
            SpawnEnemyUnit(new UnitStats(def,UnitBalance.HealthScale(level),UnitBalance.DamageScale(level),level:level),new Vector2(EnemySpawnX,BaseCenterY+_rng.RandfRange(-22,22)));
        }
        _rushWaveActive=true;_rushNextReinforcement=_elapsed+BossRushCatalog.ReinforcementInterval;
        _rushReinforcementsLeft=BossRushCatalog.ReinforcementBudget(_rushCleared);
        SetStatus($"Boss rush {_rushCleared+1}/14 · {wave.BossName} · Lv {_stageData.EnemyLevel}");
    }

    /// <summary>While the commander lives, a pack of walkers, runners and brutes joins it every few seconds until the
    /// round's reinforcements run out. They count as summoned: they drop no courage, don't hold the round open, and leave
    /// with the commander.</summary>
    private void TickRushReinforcements()
    {
        var size=Math.Min(_rushReinforcementsLeft,BossRushCatalog.ReinforcementPack(_rushCleared));
        if(size<=0 || _elapsed<_rushNextReinforcement)return;
        _rushNextReinforcement=_elapsed+BossRushCatalog.ReinforcementInterval;
        if(!_units.Any(u=>u.Team==Team.Enemy && !u.IsDead && !u.IsSummoned && u.Definition.IsBoss))return;
        var cycle=new[]{GameData.EnemyWalkerId,GameData.EnemyRunnerId,GameData.EnemyBruteId};var level=_stageData.EnemyLevel;
        for(var i=0;i<size && _units.Count(u=>u.Team==Team.Enemy && !u.IsDead)<24;i++)
        {
            var def=GameData.GetUnit(cycle[_rushReinforcementsLeft--%cycle.Length]);
            SpawnEnemyReinforcement(new UnitStats(def,UnitBalance.HealthScale(level),UnitBalance.DamageScale(level),level:level),
                new Vector2(EnemySpawnX+i/3*14,BaseCenterY+_rng.RandfRange(-22,22)));
        }
    }
}
