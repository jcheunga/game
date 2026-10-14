using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ContentRevampSmoke
{
    private void CheckProgressionExpansion(GameState state)
    {
        state.ResetProgress();state.SetShowHints(false);state.SetAnalyticsConsent(false);
        Check(state.GetCollectionProgress("units")==3,"New core collection counts one Peasant out of 32 cards");
        var fixture=state.BuildSaveData();fixture.Gold=10000;fixture.Essence=100;fixture.HighestUnlockedStage=100;
        fixture.OwnedPlayerUnitIds=GameData.PlayerRosterIds;fixture.UnitLevels=GameData.PlayerRosterIds.ToDictionary(id=>id,_=>20);
        fixture.ActiveDeckUnitIds=new[]{"player_peasant"};fixture.UnitStarLevels.Clear();fixture.UnitTokens.Clear();
        fixture.OwnedPlayerSpellIds=GameData.PlayerSpellIds.Take(10).ToArray();Call(state,"ApplySavedData",fixture);
        Check(state.GetCollectionProgress("units")==100 && state.GetCollectionProgress("spells")==23,"Collection thresholds use the full core and all 42 utilities");
        Check(!WagonSkinCatalog.IsSkinUnlocked("skin_royal",state),"Reaching the final stage alone does not unlock the campaign victor look");
        var gold=state.Gold;var essence=state.Essence;
        Check(state.TryTrainAwakeningToken("player_peasant",out _) && state.GetUnitTokens("player_peasant")==1 && state.Gold==gold-100 && state.Essence==essence-2,"Awakening token training spends the exact published resources");
        var preview=state.BuildAwakeningPreview("player_peasant");
        Check(state.TryAwakenUnit("player_peasant",out _) && state.GetUnitStarLevel("player_peasant")==1 && state.BuildPlayerUnitStats(GameData.GetUnit("player_peasant")).MaxHealth==preview.After.MaxHealth,"Awakening applies its previewed capped battle stats");
        state.ReloadFromDisk();Check(state.GetUnitStarLevel("player_peasant")==1 && state.GetUnitTokens("player_peasant")==0,"Awakening and spent tokens persist after reload");
        gold=state.Gold;Check(state.TryClaimCollectionMilestone("units_25",out _) && state.Gold==gold+200 && !state.TryClaimCollectionMilestone("units_25",out _),"Collection reward pays once and rejects duplicate claims");
        state.ReloadFromDisk();Check(state.HasClaimedCollectionMilestone("units_25"),"Collection reward claims survive reload");
        fixture=state.BuildSaveData();fixture.StageStars=new int[100];fixture.StageStars[99]=1;Call(state,"ApplySavedData",fixture);
        state.SetWagonSkin("skin_royal");state.ReloadFromDisk();
        Check(state.SelectedWagonSkinId=="skin_royal","Unlocked wagon look selection persists");
        var stats=state.BuildPlayerUnitStats(GameData.GetUnit("player_peasant"));state.SetWagonSkin("skin_default");
        Check(state.BuildPlayerUnitStats(GameData.GetUnit("player_peasant")).MaxHealth==stats.MaxHealth,"Wagon cosmetics do not alter unit power");
    }
    private async Task CheckBossRushAndFeedback()
    {
        var state=GameState.Instance;state.UnlockNextStage(99);state.PrepareBossRush(out _);
        var battle=GD.Load<PackedScene>("res://scenes/Battle.tscn").Instantiate<BattleController>();AddChild(battle);battle.SetPhysicsProcess(false);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        try
        {
            Check(BossRushCatalog.TotalWaves==14 && BossRushCatalog.Waves.Select(w=>w.BossUnitId).Distinct().Count()==14 && BossRushCatalog.Waves.All(w=>w.HealthScale==1 && w.DamageScale==1),"Boss rush includes all 14 unique commanders without the old multiplier ladder");
            Write(battle,"_enemyBaseHealth",0f);Call(battle,"CheckBattleEnd");
            Check(!Read<bool>(battle,"_battleEnded"),"Boss rush cannot end by damaging the scenery gate");
            var units=Read<System.Collections.Generic.List<Unit>>(battle,"_units");
            state.SetShowHints(true);
            Call(battle,"TeachEnemy",GameData.GetUnit("enemy_rustguard"),false);Call(battle,"TickEnemyLessons",.1f);
            Check(Read<PanelContainer>(battle,"_enemyLesson")!=null,"First-sighting counter lesson appears as a non-blocking HUD card");
            var queued=Read<System.Collections.Generic.Queue<UnitDefinition>>(battle,"_enemyLessons").Count;
            Call(battle,"TeachEnemy",GameData.GetUnit("enemy_rustguard"),true);
            Check(Read<System.Collections.Generic.Queue<UnitDefinition>>(battle,"_enemyLessons").Count==queued,"Known enemies do not repeat their first-sighting lesson");
            state.SetShowHints(false);Call(battle,"TickEnemyLessons",9f);
            var probe=(Unit)Call(battle,"SpawnUnit",Team.Player,new UnitStats(GameData.GetUnit("player_brawler")),new Vector2(550,340));
            probe.GiveShield(5,3);probe.ApplyArmorBreak(3,2);probe.ApplyControl(.4f);probe.Silence(.5f);
            Check(probe.ContentStatusLabels.Contains("SHIELD") && probe.ContentStatusLabels.Contains("ARM−") && probe.ContentStatusLabels.Contains("STUN") && probe.ContentStatusLabels.Contains("SIL"),"Combat feedback exposes active protection and disabling statuses");
            Read<System.Collections.Generic.Dictionary<Unit,float>>(battle,"_pendingBossPhases")[probe]=1000;
            probe.Despawn();Call(battle,"CleanupDeadUnits");
            Check(!Read<System.Collections.Generic.Dictionary<Unit,float>>(battle,"_pendingBossPhases").ContainsKey(probe),"Death cleanup removes pending phases before a pooled body is reused");
            var gold=state.Gold;var stars=state.GetStageStars(10);
            for(var i=0;i<14;i++)
            {
                Write(battle,"_elapsed",100f+i*10);Call(battle,"TickBossRush",.1f);
                Check(units.Count(u=>u.Team==Team.Enemy && !u.IsDead && u.Definition.IsBoss)==1,$"Rush round {i+1} spawns exactly one commander");
                foreach(var enemy in units.Where(u=>u.Team==Team.Enemy && !u.IsDead).ToArray())enemy.TakeDamage(enemy.MaxHealth*10,damageType:CombatDamageType.True);
                Call(battle,"CleanupDeadUnits");Call(battle,"TickBossRush",.1f);
            }
            Check(Read<bool>(battle,"_battleEnded") && state.BestBossRushWave==14 && state.Gold==gold+BossRushCatalog.TotalRewardGold(),"Clearing all rush rounds records the run and pays its earned rewards once");
            state.ApplyBossRushResult(14,1,100000,true);
            Check(state.Gold==gold+BossRushCatalog.TotalRewardGold() && state.GetStageStars(10)==stars,"Repeated rush result cannot pay twice or alter campaign stars");
        }
        finally {battle.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    }
}
