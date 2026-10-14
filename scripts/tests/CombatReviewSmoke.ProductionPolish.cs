using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class CombatReviewSmoke
{
    private async Task CheckProductionPolish()
    {
        var state = GameState.Instance;
        state.ResetProgress(); state.SetAnalyticsConsent(false);
        var fixture = state.BuildSaveData();
        fixture.PlayerAuthToken = "polish-test-session";
        fixture.AccountProvider = "email"; fixture.AccountLabel = "Test account";
        fixture.PlayerCallsign = "Test caravan";
        fixture.ChallengeSyncEndpoint = "https://example.invalid/challenge-sync";
        fixture.PurchaseValidationEndpoint = "https://example.invalid";
        fixture.CrashReportingConsent = false; fixture.AnalyticsConsent = false;
        fixture.Gold = 432; fixture.Food = 24;
        Invoke(state, "ApplySavedData", fixture);
        state.SetEffectsVolumePercent(12); state.SetReducedMotion(true); state.SetFontSizeOffset(6);
        state.RestorePlayerPreferences();
        Check(state.PlayerAuthToken == fixture.PlayerAuthToken && state.AccountProvider == "email" && state.PlayerCallsign == "Test caravan", "Restoring preferences preserves account identity and caravan name");
        Check(state.ChallengeSyncEndpoint == fixture.ChallengeSyncEndpoint && state.PurchaseValidationEndpoint == fixture.PurchaseValidationEndpoint, "Restoring preferences preserves service connections");
        Check(state.Gold == 432 && state.Food == 24 && !state.AnalyticsConsent && !state.CrashReportingConsent, "Restoring preferences preserves progress and privacy choices");
        Check(state.EffectsVolumePercent == 85 && state.FontSizeOffset == 0 && !state.ReducedMotion && !state.ShowFpsCounter && !state.ShowDevUi, "Restoring preferences restores player defaults without enabling debug displays");
        state.SaveNow(); Check(SaveSystem.Instance.LastSaveSucceeded, "Preference restore saves successfully");
        var first = state.BuildSaveData(); first.Gold = 111; SaveSystem.Instance.Save(first);
        var second = state.BuildSaveData(); second.Gold = 222; SaveSystem.Instance.Save(second);
        using (var file = Godot.FileAccess.Open(SaveSystem.Instance.ActiveSaveFilePath, Godot.FileAccess.ModeFlags.Write)) file.StoreString(" ");
        Check(SaveSystem.Instance.TryLoad(out var recovered) && recovered.Gold == 111 && SaveSystem.Instance.LoadedFromBackup, "An interrupted empty save recovers the previous valid backup");
        SaveSystem.Instance.Save(recovered);
        Check(SaveSystem.Instance.TryLoad(out recovered) && recovered.Gold == 111, "Saving recovered progress restores a valid primary file");
        state.ResetProgress(); state.SetShowHints(false); state.SetAnalyticsConsent(false);
        state.SetChallengeSyncEndpoint(""); state.SetPurchaseValidationEndpoint(""); state.ClearPlayerProfileSession();
        var battle = await OpenBattle(1);
        var spells = Read<BattleSpellState>(battle, "_spellDeck");
        var allSpells = GameData.GetPlayerSpells().ToArray(); spells.Initialize(allSpells);
        Write(battle, "_mana", 100f);
        var point = new Vector2(550, 340);
        foreach (var effect in new[] { "aegis_bolt", "gale_step", "verdant_renewal", "radiant_rebuke", "anchor_hex", "molten_brand", "chain_lightning", "banish", "heal", "purifying_light", "resurrect", "soul_harvest", "polymorph", "tactic_repair_cache" })
        {
            var spell = allSpells.Single(s => s.EffectType == effect);
            var mana = Read<float>(battle, "_mana");
            Invoke(battle, "TryCastSpellAt", spell, point);
            Check(Read<float>(battle, "_mana") == mana && spells.GetCooldownRemaining(spell.Id) == 0, effect + ": invalid target spends no mana or cooldown");
        }
        var ally = (Unit)Invoke(battle, "SpawnUnit", Team.Player, new UnitStats(GameData.GetUnit("player_brawler")), point);
        ally.TakeDamage(10, damageType: CombatDamageType.True);
        var heal = allSpells.Single(s => s.EffectType == "heal");
        Invoke(battle, "TryCastSpellAt", heal, point);
        Check(ally.Health == ally.MaxHealth && Read<float>(battle, "_mana") < 100 && spells.GetCooldownRemaining(heal.Id) > 0, "A valid heal spends mana and starts cooldown exactly once");
        var gold = state.Gold; var food = state.Food;
        Invoke(battle, "EndBattle", true);
        var rewardedGold = state.Gold; var rewardedFood = state.Food;
        Invoke(battle, "EndBattle", true);
        Check(rewardedGold > gold && state.Gold == rewardedGold && state.Food == rewardedFood, "Repeated victory callbacks cannot duplicate rewards");
        await CloseBattle(battle);
        battle = await OpenBattle(1);
        Write(battle, "_cardPointerDown", true); Write(battle, "_cardPointerId", -2);
        AppLifecycleService.Instance._Notification((int)Node.NotificationApplicationPaused);
        Check(Read<bool>(battle, "_battlePaused") && GetTree().Paused && !Read<bool>(battle, "_cardPointerDown"), "Backgrounding pauses an offline battle and cancels pending gestures");
        var elapsed = Read<float>(battle, "_elapsed"); battle._PhysicsProcess(1);
        Check(Read<float>(battle, "_elapsed") == elapsed, "Background pause freezes the combat clock");
        AppLifecycleService.Instance._Notification((int)Node.NotificationApplicationResumed);
        Check(Read<bool>(battle, "_battlePaused"), "Returning to the app waits for deliberate Resume");
        battle.QueueFree(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(!GetTree().Paused, "Leaving a paused battle cannot strand the next menu paused");
        var discarded = new Unit(); var reusable = new Unit();
        UnitPool.Release(discarded); UnitPool.Release(reusable); discarded.Free();
        Check(UnitPool.Acquire() == reusable, "An invalid pool entry does not discard a later reusable unit");
        reusable.Free(); UnitPool.Clear();
        Check(TutorialHintCatalog.GetByContext("first_map").Single().Body.Contains("Travel and collecting supplies are free"), "Map teaching matches free travel and entry-only food costs");
        foreach (var zone in GameData.Stages.GroupBy(s => s.MapId))
        {
            var ordered = zone.OrderBy(s => s.StageNumber).ToArray();
            Check(ordered.Count(s => !string.IsNullOrEmpty(s.NewEnemyId)) == 1 && ordered.All(s => s.EnemyLevel is >= 1 and <= 20), zone.Key + ": one regional introduction and bounded enemy levels");
        }
        GD.Print("PRODUCTION_POLISH_COMBAT_RESULT: " + _failures + " failures");
    }

    private async Task ReviewCombatPerformance()
    {
        GameState.Instance.ResetProgress(); GameState.Instance.SetShowHints(false);
        var battle = await OpenBattle(100);
        var director = Read<BattleSpawnDirector>(battle, "_spawnDirector");
        var army = new List<Unit>();
        for (var i = 0; i < 32; i++)
        {
            var player = (Unit)Invoke(battle, "SpawnUnit", Team.Player,
                new UnitStats(GameData.GetUnit("player_brawler")), new Vector2(530 + i % 8 * 7, 330 + i % 3 * 8));
            director.TryBuildEnemyStats(i % 2 == 0 ? GameData.EnemyWalkerId : "enemy_cinder_spitter", out var stats);
            var enemy = (Unit)Invoke(battle, "SpawnUnit", Team.Enemy, stats, new Vector2(630 + i % 8 * 7, 330 + i % 3 * 8));
            player.GiveShield(10, 10); enemy.ApplyBurn(.1f, 6, "Stress test"); army.Add(player); army.Add(enemy);
        }
        var samples = new List<double>();
        for (var i = 0; i < 360; i++)
        {
            var watch = Stopwatch.StartNew(); battle._PhysicsProcess(1.0 / 60); watch.Stop();
            samples.Add(watch.Elapsed.TotalMilliseconds);
            if (i % 12 == 0) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var ordered = samples.OrderBy(x => x).ToArray();
        GD.Print("PERFORMANCE_SAMPLE: " + System.Text.Json.JsonSerializer.Serialize(new {
            initialCombatants = 64, ticks = samples.Count, medianMs = ordered[ordered.Length / 2],
            p95Ms = ordered[(int)(ordered.Length * .95)], maxMs = ordered[^1],
            liveNodes = (int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount),
            note = "Local simulation timings under concurrent review load; not a physical-device frame-rate claim." }));
        Check(ordered.All(double.IsFinite), "Crowded battle simulation completes with finite tick timings");
        Check(Read<List<Unit>>(battle, "_units").Count < 128, "Crowded battle keeps combatant count bounded");
        await CloseBattle(battle);
        Check(!GetTree().Paused && Engine.TimeScale == 1, "Crowded battle cleanup restores pause and speed state");
    }
}
