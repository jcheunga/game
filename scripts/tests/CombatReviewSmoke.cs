using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

// Runs real combat with an isolated save. Reflection keeps test controls out of gameplay APIs.
public partial class CombatReviewSmoke : Node
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private int _failures;
    private static T Read<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden)!.GetValue(owner)!;
    private static void Write(object owner, string name, object value) => owner.GetType().GetField(name, Hidden)!.SetValue(owner, value);
    private static object Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Hidden)!.Invoke(owner, args);
    public override void _Ready() => Callable.From(Run).CallDeferred();

    private void Check(bool ok, string label)
    {
        GD.Print($"COMBAT_CHECK: {(ok ? "PASS" : "FAIL")} {label}");
        if (!ok) _failures++;
    }

    private async Task<BattleController> OpenBattle(int stage)
    {
        var state = GameState.Instance;
        state.SetSelectedStage(stage);
        state.PrepareCampaignBattle();
        var battle = GD.Load<PackedScene>("res://scenes/Battle.tscn").Instantiate<BattleController>();
        AddChild(battle);
        battle.SetPhysicsProcess(false);
        var seedArg = OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--seed-offset="));
        Read<RandomNumberGenerator>(battle, "_rng").Seed = 4100UL + (ulong)stage + (seedArg == null ? 0UL : ulong.Parse(seedArg.Split('=')[1]));
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        return battle;
    }

    private async Task CloseBattle(BattleController battle)
    {
        battle.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async void Run()
    {
        try
        {
            var args = OS.GetCmdlineUserArgs();
            if (!args.Any(x => x.StartsWith("--save-suffix=combat-review-")))
                throw new InvalidOperationException("Requires an isolated --save-suffix=combat-review-<unique-id>.");
            if (args.Contains("--armaments") && !args.Contains("--tactical"))
                throw new InvalidOperationException("The --armaments profile requires --tactical.");
            GameState.Instance.SetAnalyticsConsent(false);
            GameState.Instance.SetShowHints(false);
            GameState.Instance.UnlockNextStage(59);
            if (args.Contains("--economy-export")) ExportProgressionEconomy();
            else if (args.Contains("--regressions")) await Regressions();
            else
            {
                var selected = args.FirstOrDefault(x => x.StartsWith("--stages="))?.Split('=')[1];
                var stages = selected == null ? Enumerable.Range(1, 60) : selected.Split(',').Select(int.Parse);
                foreach (var stage in stages) await ReviewBattle(stage);
                if (args.Contains("--manual") || args.Contains("--handoff-boss") || args.Any(x => x.StartsWith("--handoff-at="))) return; // Leave the result visible for direct review.
            }
        }
        catch (Exception ex) { GD.PrintErr(ex.ToString()); _failures++; }
        GD.Print($"COMBAT_REVIEW_RESULT: {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private async Task Regressions()
    {
        CheckSpawnScheduling();
        CheckProgressionRewards();
        await CheckBaseWeapons();
        await CheckFinishPacing();
        var battle = await OpenBattle(12);
        var director = Read<BattleSpawnDirector>(battle, "_spawnDirector");
        foreach (var id in new[] { "enemy_boss", "enemy_boss_ward", "enemy_lich" })
        {
            director.TryBuildEnemyStats(id, out var stats);
            var unit = (Unit)Invoke(battle, "SpawnUnit", Team.Enemy, stats, new Vector2(900, 340));
            unit.TickSpecialTimer(100);
            try { Invoke(battle, "SimulateUnits", 1f / 60f); Check(true, $"{id} can summon during simulation"); }
            catch (TargetInvocationException ex) { Check(false, $"{id}: {ex.InnerException?.Message}"); }
        }
        var boss = Read<List<Unit>>(battle, "_units").First(x => x.DefinitionId == "enemy_boss_ward");
        boss.TakeDamage(boss.MaxHealth * 0.6f / boss.DamageTakenScale);
        Write(battle, "_enemyBaseHealth", 0f);
        try
        {
            Invoke(battle, "TryTriggerCampaignBossPhase");
            Check(Read<Dictionary<Unit, float>>(battle, "_pendingBossPhases").ContainsKey(boss), "Boss phase gives a reaction window even after the gate is breached");
            if (OS.GetCmdlineUserArgs().Contains("--screenshots"))
            {
                battle.QueueRedraw();
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://artifacts/combat-review/phase-warning.png"));
            }
            Write(battle, "_elapsed", 2f);
            Invoke(battle, "TryTriggerCampaignBossPhase");
            Check(Read<HashSet<Unit>>(battle, "_campaignBossPhaseTriggeredUnits").Contains(boss), "Boss phase resolves and summons safely");
            var count = Read<List<Unit>>(battle, "_units").Count;
            Invoke(battle, "TryTriggerCampaignBossPhase");
            Check(Read<List<Unit>>(battle, "_units").Count == count, "Boss phase only fires once");
        }
        catch (TargetInvocationException ex) { Check(false, $"Boss phase: {ex.InnerException?.Message}"); }
        await CloseBattle(battle);

        battle = await OpenBattle(4);
        Write(battle, "_enemyBaseHealth", 0f);
        Invoke(battle, "CheckBattleEnd");
        Check(!Read<bool>(battle, "_battleEnded"), "Breaching the gate cannot skip the waves or boss");
        director = Read<BattleSpawnDirector>(battle, "_spawnDirector");
        for (var i = 0; i < 100; i++) director.Tick(1, 200 + i, () => 0, (_, _) => { }, _ => { });
        director.TryBuildEnemyStats("enemy_boss", out var bossStats);
        boss = (Unit)Invoke(battle, "SpawnUnit", Team.Enemy, bossStats, new Vector2(900, 340));
        Invoke(battle, "CheckBattleEnd");
        Check(!Read<bool>(battle, "_battleEnded"), "A living commander blocks victory after the gate falls");
        boss.TakeDamage(boss.MaxHealth * 10);
        Invoke(battle, "CheckBattleEnd");
        Check(Read<bool>(battle, "_battleEnded"), "Routed defenders and breached gate allow victory");
        await CloseBattle(battle);

        battle = await OpenBattle(52);
        Write(battle, "_enemyBaseHealth", 100f);
        Invoke(battle, "RepairEnemyBaseByRatio", 0.05f, Colors.White, "");
        Check(Read<float>(battle, "_enemyBaseHealth") > 100f, "An intact gate can still be repaired");
        Write(battle, "_enemyBaseHealth", 0f);
        Invoke(battle, "RepairEnemyBaseByRatio", 0.05f, Colors.White, "");
        Check(Read<float>(battle, "_enemyBaseHealth") == 0f, "Boss repairs cannot resurrect a breached gate");
        director = Read<BattleSpawnDirector>(battle, "_spawnDirector");
        director.TryBuildEnemyStats(GameData.EnemyBossReliquaryId, out var tyrantStats);
        var tyrant = (Unit)Invoke(battle, "SpawnUnit", Team.Enemy, tyrantStats, new Vector2(900, 240));
        tyrant.TakeDamage(tyrant.MaxHealth * 0.6f / tyrant.DamageTakenScale);
        var woundedHealth = tyrant.Health;
        var beforePressureCount = Read<List<Unit>>(battle, "_units").Count;
        Write(battle, "_enemyBaseHealth", 100f);
        Invoke(battle, "ApplyCampaignBossPressure", tyrant);
        Check(tyrant.Health == woundedHealth, "Repeated Ossuary Fire preserves damage dealt to the Tyrant");
        Check(Read<List<Unit>>(battle, "_units").Count == beforePressureCount && Read<float>(battle, "_enemyBaseHealth") == 100f,
            "Ossuary Fire leaves a recovery window instead of adding another summon and gate-repair loop");
        Invoke(battle, "ApplyCampaignBossPhase", tyrant);
        tyrant.TickSpecialTimer(100);
        Invoke(battle, "TriggerEnemyRaiseFallen", tyrant);
        var artillery = Read<List<Unit>>(battle, "_units").Where(x => !x.IsDead && x.DefinitionId == tyrant.SpecialSpawnUnitId).ToList();
        Check(artillery.Count == 2, "Tyrant special and phase commands share a two-artillery limit");
        artillery[0].TakeDamage(artillery[0].MaxHealth * 10);
        tyrant.TickSpecialTimer(100);
        Invoke(battle, "TriggerEnemyRaiseFallen", tyrant);
        Check(Read<List<Unit>>(battle, "_units").Count(x => !x.IsDead && x.DefinitionId == tyrant.SpecialSpawnUnitId) == 2,
            "Tyrant can replace defeated artillery without accumulating it");
        for (var attempt = 0; attempt < 64; attempt++) Invoke(battle, "TryLichGraveyardReanimate", artillery[0]);
        Check(Read<List<Unit>>(battle, "_units").Count(x => !x.IsDead && x.DefinitionId == tyrant.SpecialSpawnUnitId) == 2,
            "Graveyard resurrection cannot bypass the Tyrant's artillery limit after a replacement");
        await CloseBattle(battle);

        battle = await OpenBattle(55);
        Invoke(battle, "TickTunnelInvasion", 1f);
        Check(Read<List<Unit>>(battle, "_units").Count == 0, "Tunnel invasion leaves the opening deployment window clear");
        Invoke(battle, "TickTunnelInvasion", 15f);
        Check(Read<Vector2?>(battle, "_pendingTunnelInvasion").HasValue && Read<List<Unit>>(battle, "_units").Count == 0,
            "Tunnel invasion marks its position before an enemy appears");
        Invoke(battle, "TickTunnelInvasion", 2f);
        Check(Read<List<Unit>>(battle, "_units").Count == 1 && !Read<Vector2?>(battle, "_pendingTunnelInvasion").HasValue,
            "The warned tunneler emerges after its reaction window");
        Check(Read<float>(battle, "_tunnelInvasionTimer") >= 22f, "Tunnel invasion grants recovery between ambushes");
        await CloseBattle(battle);

        battle = await OpenBattle(1);
        var archer = (Unit)Invoke(battle, "SpawnUnit", Team.Player, new UnitStats(GameData.GetUnit("player_shooter")), new Vector2(400, 340));
        var caster = (Unit)Invoke(battle, "SpawnUnit", Team.Enemy, new UnitStats(GameData.GetUnit("enemy_spitter")), new Vector2(600, 340));
        var shield = (Unit)Invoke(battle, "SpawnUnit", Team.Enemy, new UnitStats(GameData.GetUnit("enemy_shieldwall")), new Vector2(560, 340));
        Check(ReferenceEquals(Invoke(battle, "FindProjectileShieldInterceptor", archer, caster), shield), "Shield Wall intercepts shots in front of its caster");
        shield.Position = new Vector2(690, 340);
        Check(Invoke(battle, "FindProjectileShieldInterceptor", archer, caster) == null, "Shield Wall behind the caster cannot intercept");
        shield.Position = new Vector2(560, 340);
        var swordsman = (Unit)Invoke(battle, "SpawnUnit", Team.Player, new UnitStats(GameData.GetUnit("player_brawler")), new Vector2(540, 340));
        Check(ReferenceEquals(Invoke(battle, "FindClosestEnemy", swordsman), shield), "Melee troops engage nearby blockers before chasing support enemies");
        Check(ReferenceEquals(Invoke(battle, "FindClosestEnemy", archer), caster), "Ranged troops retain support targeting");
        await CloseBattle(battle);

        battle = await OpenBattle(49);
        archer = (Unit)Invoke(battle, "SpawnUnit", Team.Player, new UnitStats(GameData.GetUnit("player_shooter")), new Vector2(500, 180));
        var health = archer.Health;
        Invoke(battle, "ApplyCursedGroundAttrition", 1f);
        Check(archer.Health == health, "Cursed ground has safe deployment lanes");
        archer.Position = new Vector2(500, 340);
        Invoke(battle, "ApplyCursedGroundAttrition", 1f);
        Check(archer.Health < health, "The marked cursed strip still applies attrition");
        if (OS.GetCmdlineUserArgs().Contains("--screenshots"))
        {
            battle.QueueRedraw();
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://artifacts/combat-review/cursed-ground.png"));
        }
        await CloseBattle(battle);

        battle = await OpenBattle(16);
        var jammer = (Unit)Invoke(battle, "SpawnUnit", Team.Enemy, new UnitStats(GameData.GetUnit("enemy_jammer")), new Vector2(900, 340));
        jammer.TickSpecialTimer(100);
        Check((bool)Invoke(battle, "TriggerEnemySignalJam", jammer), "A ready hexer can disrupt courage");
        jammer.TickSpecialTimer(100);
        Check(!(bool)Invoke(battle, "TriggerEnemySignalJam", jammer), "Active jams cannot stack card penalties");
        Write(battle, "_enemySignalJamTimer", 0f);
        Write(battle, "_enemySignalJamRecoveryUntil", 3f);
        Check(!(bool)Invoke(battle, "TriggerEnemySignalJam", jammer), "Hexers respect the recovery window");
        Write(battle, "_elapsed", 3.1f);
        Check((bool)Invoke(battle, "TriggerEnemySignalJam", jammer), "Hexer pressure resumes after recovery");
        await CloseBattle(battle);
    }

    private void CheckSpawnScheduling()
    {
        Check(StageMissionEvents.GetCampaignMissionEvents(GameData.GetStage(1)).Length == 0,
            "The opening teaches combat before adding side missions");
        var stage = new StageDefinition { Waves = new[] { new StageWaveDefinition { TriggerTime = 100 } } };
        var director = new BattleSpawnDirector(new RandomNumberGenerator { Seed = 42 });
        director.Initialize(1, stage, new CombatTuning(), GameData.GetEnemyUnits());
        director.QueueScriptedWaveSupplement(20, 1, new[] { new StageWaveEntryDefinition { UnitId = "enemy_brute" } });
        director.QueueScriptedWaveSupplement(2, 1, new[] { new StageWaveEntryDefinition { UnitId = "enemy_runner", Count = 3 } });
        var spawned = new List<string>();
        director.Tick(2, 2, () => 0, (stats, _) => spawned.Add(stats.DefinitionId), _ => { });
        Check(spawned.SequenceEqual(new[] { "enemy_runner" }), "Earlier reinforcements are not blocked by a later queued wave");
        director.Tick(8, 10, () => 5, (stats, _) => spawned.Add(stats.DefinitionId), _ => { });
        Check(spawned.Count == 1, "Full enemy cap delays pending spawns");
        director.Tick(1, 11, () => 0, (stats, _) => spawned.Add(stats.DefinitionId), _ => { });
        Check(spawned.Count == 2, "Delayed enemies do not all burst out in one frame");
        director.Initialize(60, GameData.GetStage(60), new CombatTuning(), GameData.GetEnemyUnits());
        director.TryBuildEnemyStats("enemy_crusher", out var stats60);
        Check(Math.Abs(stats60.AttackCooldown - GameData.GetUnit("enemy_crusher").AttackCooldown) < 0.001,
            "Late heavy enemies retain their authored attack rhythm");
        var quiet = new StageDefinition { Waves = new[] {
            new StageWaveDefinition { TriggerTime = 1 }, new StageWaveDefinition { TriggerTime = 90 }
        }};
        director.Initialize(1, quiet, new CombatTuning(), GameData.GetEnemyUnits());
        director.Tick(1, 1, () => 0, (_, _) => { }, _ => { });
        director.Tick(1, 2, () => 0, (_, _) => { }, _ => { });
        Check(Math.Abs(director.NextScriptedWaveTime - 6) < 0.001 && director.NextScriptedWaveIndex == 1,
            "A cleared wave grants four seconds of visible recovery before the next wave");
        director.Tick(90, 100, () => 5, (_, _) => { }, _ => { });
        Check(director.NextScriptedWaveIndex == 1, "A crowded frontline holds the next scripted wave");
        director.Tick(1, 101, () => 1, (_, _) => { }, _ => { });
        Check(director.NextScriptedWaveIndex == 2, "Scripted waves resume once pressure is under control");
    }

    private async Task ReviewBattle(int stage)
    {
        GameState.Instance.ResetProgress();
        GameState.Instance.SetAnalyticsConsent(false);
        GameState.Instance.SetShowHints(false);
        GameState.Instance.UnlockNextStage(59);
        // A modest reference squad, with normal unit upgrades but no equipment, doctrines or purchases.
        var tactical = OS.GetCmdlineUserArgs().Contains("--tactical");
        var level = tactical ? (stage < 4 ? 1 : stage < 8 ? 2 : stage < 16 ? 3 : stage < 28 ? 4 : 5) : Math.Min(5, 1 + (stage - 1) / 8);
        var levelDelta = OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--unit-level-delta="));
        if (levelDelta != null) level = Math.Clamp(level + int.Parse(levelDelta.Split('=')[1]), 1, 5);
        var squad = OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--squad="))?.Split('=')[1];
        if (squad != null)
        {
            var selected = squad.Split(',');
            if (selected.Length != 3 || selected.Any(id => GameData.GetUnit(id).UnlockStage > stage))
                throw new InvalidOperationException("Benchmark squad must have three stage-available units.");
            var owned = Read<HashSet<string>>(GameState.Instance, "_ownedPlayerUnitIds");
            var active = Read<List<string>>(GameState.Instance, "_activeDeckUnitIds");
            active.Clear();
            foreach (var id in selected) { owned.Add(id); active.Add(id); }
        }
        var levels = Read<Dictionary<string, int>>(GameState.Instance, "_unitUpgradeLevels");
        foreach (var id in GameState.Instance.ActiveDeckUnitIds) levels[id] = level;
        if (tactical)
        {
            var upgrades = Read<Dictionary<string, int>>(GameState.Instance, "_baseUpgradeLevels");
            var upgradeLevel = Math.Min(3, (stage - 1) / 8);
            foreach (var id in new[] { BaseUpgradeCatalog.HullPlatingId, BaseUpgradeCatalog.PantryId,
                BaseUpgradeCatalog.DispatchConsoleId, BaseUpgradeCatalog.SignalRelayId, BaseUpgradeCatalog.ProjectileWardId })
                upgrades[id] = upgradeLevel;
            // Explicit alternate profile: preserve the original reference while exercising the new purchases.
            if (OS.GetCmdlineUserArgs().Contains("--armaments"))
                foreach (var id in new[] { BaseUpgradeCatalog.ArcherCrewId, BaseUpgradeCatalog.BallistaId,
                    BaseUpgradeCatalog.FirepotId, BaseUpgradeCatalog.ArrowVolleyId,
                    BaseUpgradeCatalog.EmergencyRepairId, BaseUpgradeCatalog.ReinforcedArmorId })
                    upgrades[id] = upgradeLevel;
            var spellLevels = Read<Dictionary<string, int>>(GameState.Instance, "_spellUpgradeLevels");
            foreach (var id in GameState.Instance.ActiveDeckSpellIds) spellLevels[id] = Math.Min(3, 1 + (stage - 1) / 15);
        }
        ApplyProgressionRelics(stage);
        var investment = GetProgressionInvestment();
        var battle = await OpenBattle(stage);
        var deck = Read<BattleDeckState>(battle, "_deck");
        var units = Read<List<Unit>>(battle, "_units");
        if (OS.GetCmdlineUserArgs().Contains("--manual"))
        {
            await ManualPlay(battle, stage, level, deck);
            return;
        }
        var handoffArg = OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--handoff-at="));
        var handoffAt = handoffArg == null ? float.PositiveInfinity : float.Parse(handoffArg.Split('=')[1], System.Globalization.CultureInfo.InvariantCulture);
        var bosses = new HashSet<string>();
        var snapshotTaken = false;
        var peak = 0;
        var tick = 0;
        var limitArg = OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--time-limit="));
        var timeLimit = limitArg == null ? 210 : Math.Clamp(int.Parse(limitArg.Split('=')[1]), 60, 600);
        while (!Read<bool>(battle, "_battleEnded") && Read<float>(battle, "_elapsed") < timeLimit)
        {
            if (Read<float>(battle, "_elapsed") >= handoffAt ||
                (OS.GetCmdlineUserArgs().Contains("--handoff-boss") && units.Any(x => !x.IsDead && x.Team == Team.Enemy && x.VisualClass == "boss")))
            {
                await ManualPlay(battle, stage, level, deck);
                return;
            }
            if (tick++ % 30 == 0)
            {
                var enemy = units.Where(x => x.Team == Team.Enemy && !x.IsDead).OrderBy(x => x.Position.X).FirstOrDefault();
                var targetY = enemy?.Position.Y ?? 340;
                var courage = Read<float>(battle, "_courage");
                var savingForSpell = false;
                if (tactical)
                {
                    var hurt = units.Where(x => x.Team == Team.Player && !x.IsDead && x.HealthRatio < 0.5f)
                        .OrderBy(x => x.HealthRatio).FirstOrDefault();
                    var cluster = units.Where(x => x.Team == Team.Enemy && !x.IsDead)
                        .OrderByDescending(x => units.Count(y => y.Team == Team.Enemy && !y.IsDead && y.Position.DistanceTo(x.Position) < 76))
                        .FirstOrDefault();
                    var spellDeck = Read<BattleSpellState>(battle, "_spellDeck");
                    if (hurt != null && spellDeck.GetCooldownRemaining("spell_heal") <= 0)
                    {
                        savingForSpell = courage < 20;
                        if (!savingForSpell) Invoke(battle, "TryCastSpellAt", GameData.GetSpell("spell_heal"), hurt.Position);
                    }
                    else if (cluster != null && units.Count(x => x.Team == Team.Enemy && !x.IsDead && x.Position.DistanceTo(cluster.Position) < 76) >= 3)
                    {
                        savingForSpell = courage < 22 && spellDeck.GetCooldownRemaining("spell_fireball") <= 0;
                        if (!savingForSpell) Invoke(battle, "TryCastSpellAt", GameData.GetSpell("spell_fireball"), cluster.Position);
                    }
                    courage = Read<float>(battle, "_courage");
                }
                var frontline = units.Count(x => x.Team == Team.Player && !x.IsDead && !x.UsesProjectile && Math.Abs(x.Position.Y - targetY) < 110);
                var preferred = frontline == 0 ? deck.Roster.Where(x => !x.UsesProjectile).OrderByDescending(x => x.MaxHealth).FirstOrDefault() :
                    deck.Roster.Where(x => x.UsesProjectile).OrderBy(x => units.Count(u => !u.IsDead && u.Team == Team.Player && u.DefinitionId == x.Id)).FirstOrDefault();
                var order = deck.Roster.OrderBy(x => x == preferred ? 0 : 1);
                var card = order.FirstOrDefault(x => deck.CanDeploy(x, courage, false, out _));
                if (card != null && !savingForSpell) { Invoke(battle, "ArmPlayerUnit", card); Invoke(battle, "TryDeployAtY", targetY); }
                if (Read<bool>(battle, "_campaignConvoyCommandReady")) Invoke(battle, "TryActivateCampaignConvoyCommand");
            }
            battle._PhysicsProcess(1.0 / 60);
            if (!snapshotTaken && OS.GetCmdlineUserArgs().Contains("--screenshots") &&
                (Read<Dictionary<Unit, float>>(battle, "_pendingBossPhases").Count > 0 || Read<float>(battle, "_elapsed") > 55))
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://artifacts/combat-review/stage-{stage}.png"));
                snapshotTaken = true;
            }
            foreach (var unit in units.Where(x => x.Team == Team.Enemy && x.VisualClass == "boss")) bosses.Add(unit.DefinitionId);
            peak = Math.Max(peak, units.Count(x => x.Team == Team.Enemy && !x.IsDead));
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        GD.Print("COMBAT_SAMPLE: " + System.Text.Json.JsonSerializer.Serialize(new {
            milestoneRelics = OS.GetCmdlineUserArgs().Contains("--milestone-relics"), timeLimit,
            stage, level, tactical, seedOffset = OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--seed-offset="))?.Split('=')[1] ?? "0", armaments = OS.GetCmdlineUserArgs().Contains("--armaments"), commonRelics = OS.GetCmdlineUserArgs().Contains("--common-relics"), investment, squad = string.Join(",", deck.Roster.Select(x => x.Id)), seconds = Math.Round(Read<float>(battle, "_elapsed"), 1),
            won = Read<bool>(battle, "_battleEnded") && Read<float>(battle, "_enemyBaseHealth") <= 0 && Read<float>(battle, "_playerBaseHealth") > 0,
            hull = Math.Round(Read<float>(battle, "_playerBaseHealth") / Read<float>(battle, "_playerBaseMaxHealth"), 3),
            gate = Math.Round(Read<float>(battle, "_enemyBaseHealth") / Read<float>(battle, "_enemyBaseMaxHealth"), 3),
            defeats = Read<int>(battle, "_enemyDefeats"), spells = Read<int>(battle, "_spellsCast"), peak, bosses
        }));
        if (!Read<bool>(battle, "_battleEnded"))
            GD.Print("COMBAT_REMAINS: " + System.Text.Json.JsonSerializer.Serialize(units.Where(x => !x.IsDead)
                .Select(x => new { id = x.DefinitionId, hp = Math.Round(x.Health), maxHp = Math.Round(x.MaxHealth),
                    x = Math.Round(x.Position.X), y = Math.Round(x.Position.Y) })));
        await CloseBattle(battle);
    }
    private async Task ManualPlay(BattleController battle, int stage, int level, BattleDeckState deck)
    {
        Engine.MaxFps = 60;
        Engine.TimeScale = 1;
        var timeScaleArg = OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--manual-time-scale="));
        if (timeScaleArg != null)
            Engine.TimeScale = Math.Clamp(double.Parse(timeScaleArg.Split('=')[1], System.Globalization.CultureInfo.InvariantCulture), 0.1, 1);
        battle.SetPhysicsProcess(true);
        Invoke(battle, "TogglePause");
        GD.Print($"MANUAL_PLAY_READY: stage={stage}, level={level}, squad={string.Join(",", deck.Roster.Select(x => x.Id))}; Escape resumes. All combat actions use normal input.");
        while (!Read<bool>(battle, "_battleEnded"))
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print("MANUAL_PLAY_RESULT: " + System.Text.Json.JsonSerializer.Serialize(new {
            stage, seconds = Math.Round(Read<float>(battle, "_elapsed"), 1),
            hull = Math.Round(Read<float>(battle, "_playerBaseHealth") / Read<float>(battle, "_playerBaseMaxHealth"), 3),
            gate = Math.Round(Read<float>(battle, "_enemyBaseHealth") / Read<float>(battle, "_enemyBaseMaxHealth"), 3),
            defeats = Read<int>(battle, "_enemyDefeats"), spells = Read<int>(battle, "_spellsCast")
        }));
    }

}
