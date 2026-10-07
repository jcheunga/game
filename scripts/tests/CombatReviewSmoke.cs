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
            GameState.Instance.UnlockNextStage(GameData.MaxStage - 1);
            if (args.Contains("--reference-map"))
            {
                // Test-only comparison with the original one-screen layout; never write game data.
                GameData.Combat.EnemyBaseX = 1184f;
                GameData.Combat.EnemySpawnX = 1140f;
                GameData.Combat.BattlefieldRight = 1196f;
            }
            ApplyTuningOverrides(args);
            if (args.Contains("--stage-layout")) ExportStageLayoutReview();
            else if (args.Contains("--courage-pacing")) await CheckCouragePacing();
            else if (args.Contains("--mana")) await CheckManaFromKills();
            else if (args.Contains("--stage-stars")) await CheckStageStars();
            else if (args.Contains("--field-objectives")) await CheckCampaignFieldObjectives();
            else if (args.Contains("--camera")) await CheckBattleCamera();
            else if (args.Contains("--lanes")) await CheckBattleLanes();
            else if (args.Contains("--structures")) await CheckBattleStructures();
            else if (args.Contains("--base-weapons")) await CheckBaseWeapons();
            else if (args.Contains("--economy-export")) ExportProgressionEconomy();
            else if (args.Contains("--regressions")) await Regressions();
            else if (args.Contains("--projectile-gallery") || args.Contains("--melee-gallery")) await ProjectileGallery();
            else
            {
                var selected = args.FirstOrDefault(x => x.StartsWith("--stages="))?.Split('=')[1];
                var stages = selected == null ? Enumerable.Range(1, GameData.MaxStage) : selected.Split(',').Select(int.Parse);
                foreach (var stage in stages) await ReviewBattle(stage);
                if (args.Contains("--manual") || args.Contains("--handoff-boss") || args.Any(x => x.StartsWith("--handoff-at="))) return; // Leave the result visible for direct review.
            }
        }
        catch (Exception ex) { GD.PrintErr(ex.ToString()); _failures++; }
        await LiveUiReview.StopAudio(this);
        GD.Print($"COMBAT_REVIEW_RESULT: {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private async Task Regressions()
    {
        CheckDataLoadFailureKeepsSave();
        CheckSpawnScheduling();
        CheckProgressionRewards();
        await CheckStageStars();
        await CheckBaseWeapons();
        await CheckFinishPacing();
        var battle = await OpenBattle(30);
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

        // Destroying a base ends the battle at once, regardless of remaining waves or defenders.
        battle = await OpenBattle(10);
        director = Read<BattleSpawnDirector>(battle, "_spawnDirector");
        director.TryBuildEnemyStats("enemy_boss", out var bossStats);
        boss = (Unit)Invoke(battle, "SpawnUnit", Team.Enemy, bossStats, new Vector2(900, 340));
        Invoke(battle, "CheckBattleEnd");
        Check(!Read<bool>(battle, "_battleEnded"), "The battle continues while the gate stands");
        Write(battle, "_enemyBaseHealth", 0f);
        Invoke(battle, "CheckBattleEnd");
        Check(Read<bool>(battle, "_battleEnded") && director.NextScriptedWaveIndex < director.TotalScriptedWaves,
            "Breaching the gate wins immediately, regardless of remaining waves");
        Check(!boss.IsDead, "A living commander does not block victory after the gate falls");
        await CloseBattle(battle);

        battle = await OpenBattle(60);
        Write(battle, "_enemyBaseHealth", 100f);
        Invoke(battle, "RepairEnemyBaseByRatio", 0.05f, Colors.White);
        Check(Read<float>(battle, "_enemyBaseHealth") > 100f, "An intact gate can still be repaired");
        Write(battle, "_enemyBaseHealth", 0f);
        Invoke(battle, "RepairEnemyBaseByRatio", 0.05f, Colors.White);
        Check(Read<float>(battle, "_enemyBaseHealth") == 0f, "Boss repairs cannot resurrect a breached gate");
        director = Read<BattleSpawnDirector>(battle, "_spawnDirector");
        director.TryBuildEnemyStats(GameData.EnemyBossReliquaryId, out var tyrantStats);
        var tyrant = (Unit)Invoke(battle, "SpawnUnit", Team.Enemy, tyrantStats, new Vector2(900, 300));
        tyrant.TakeDamage(tyrant.MaxHealth * 0.6f / tyrant.DamageTakenScale);
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
        // Aggro is local: keep the caster inside the archer's range, with the shield still nearer.
        archer.Position = new Vector2(caster.Position.X - archer.AggroRangeX + 10f, 340);
        Check(ReferenceEquals(Invoke(battle, "FindClosestEnemy", archer), caster), "Ranged troops retain support targeting");
        await CloseBattle(battle);
        await CheckRangedMelee();

        battle = await OpenBattle(64);
        archer = (Unit)Invoke(battle, "SpawnUnit", Team.Player, new UnitStats(GameData.GetUnit("player_shooter")),
            new Vector2(500, GameData.Combat.BattlefieldTop + GameData.Combat.SpawnVerticalPadding));
        var health = archer.Health;
        Invoke(battle, "ApplyCursedGroundAttrition", 1f);
        Check(archer.Health == health, "Cursed ground leaves the band's edges clear");
        archer.Position = ((IEnumerable<Rect2>)Invoke(battle, "CursedGroundAreas")).First().GetCenter();
        Invoke(battle, "ApplyCursedGroundAttrition", 1f);
        Check(archer.Health < health, "The marked cursed strip still applies attrition");
        if (OS.GetCmdlineUserArgs().Contains("--screenshots"))
        {
            battle.QueueRedraw();
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://artifacts/combat-review/cursed-ground.png"));
        }
        await CloseBattle(battle);

        battle = await OpenBattle(35);
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
        await CheckBattleLanes();
        await CheckCouragePacing();
        await CheckManaFromKills();
    }

    // Review captures of every shot in flight: each ranged unit fires at a pinned target across the band.
    private async Task ProjectileGallery()
    {
        if (DisplayServer.GetName() == "headless")
        {
            Check(false, "The projectile gallery captures frames, so it needs a window (drop --headless)");
            return;
        }
        var shooters = GameData.GetPlayerUnits().Concat(GameData.GetEnemyUnits())
            .Where(def => def.UsesProjectile).Select(def => def.Id).ToArray();
        var dir = ProjectSettings.GlobalizePath(OS.GetCmdlineUserArgs().Contains("--melee-gallery")
            ? "res://artifacts/melee-review" : "res://artifacts/projectile-review");
        System.IO.Directory.CreateDirectory(dir);
        var rows = new[] { 318f, 331f, 344f, 357f };
        for (var group = 0; group * 4 < shooters.Length; group++)
        {
            var battle = await OpenBattle(1);
            var units = new List<Unit>();
            var pins = new List<(Unit unit, Vector2 at)>();
            var ids = shooters.Skip(group * 4).Take(4).ToArray();
            for (var i = 0; i < ids.Length; i++)
            {
                var def = GameData.GetUnit(ids[i]);
                var player = ids[i].StartsWith("player_");
                var x = 470f + (i % 2) * 18f;
                // --melee-gallery puts every target at arm's length, for the ranged units' close-quarters strikes.
                var range = OS.GetCmdlineUserArgs().Contains("--melee-gallery") ? 24f : Mathf.Min(def.AttackRange - 4f, 150f);
                var shooter = (Unit)Invoke(battle, "SpawnUnit", player ? Team.Player : Team.Enemy, new UnitStats(def),
                    new Vector2(player ? x : x + range, rows[i]));
                var target = (Unit)Invoke(battle, "SpawnUnit", player ? Team.Enemy : Team.Player,
                    new UnitStats(GameData.GetUnit(player ? "enemy_walker" : "player_brawler")), new Vector2(player ? x + range : x, rows[i]));
                target.ApplyTemporaryDefenseModifier(0.01f, 600f);
                shooter.ApplyTemporaryDefenseModifier(0.01f, 600f);
                units.Add(shooter);
                pins.Add((target, target.Position));
                pins.Add((shooter, shooter.Position));
            }
            Invoke(battle, "SetBattleCameraX", 540f);
            for (var frame = 0; frame < 96; frame++)
            {
                Invoke(battle, "SimulateUnits", 1f / 60);
                foreach (var (unit, at) in pins) unit.Position = at;
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                if (frame % 8 == 0)
                    GD.Print($"PROJECTILE_GALLERY frame {frame}: {battle.GetChildren().OfType<Projectile>().Count(p => p.Visible)} in flight, " +
                        string.Join(" ", units.Select(u => $"{u.DefinitionId}:{(u.IsAttackCommitted ? (u.IsMeleeStrike ? "melee" : "shoot") : "-")}")));
                if (frame % 3 == 2)
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng($"{dir}/group{group}-f{frame:000}.png");
                }
            }
            GD.Print($"PROJECTILE_GALLERY group {group}: {string.Join(", ", ids)}");
            await CloseBattle(battle);
        }
    }

    // Ranged units on either side fight hand to hand when an enemy is inside melee reach, and shoot otherwise.
    private async Task CheckRangedMelee()
    {
        foreach (var (rangedId, rangedTeam, foeId) in new[] { ("player_shooter", Team.Player, "enemy_walker"),
                                                              ("enemy_spitter", Team.Enemy, "player_brawler") })
        {
            var battle = await OpenBattle(1);
            var foeTeam = rangedTeam == Team.Player ? Team.Enemy : Team.Player;
            var ranged = (Unit)Invoke(battle, "SpawnUnit", rangedTeam, new UnitStats(GameData.GetUnit(rangedId)), new Vector2(600, 340));
            var side = rangedTeam == Team.Player ? 1f : -1f;
            var foe = (Unit)Invoke(battle, "SpawnUnit", foeTeam, new UnitStats(GameData.GetUnit(foeId)), new Vector2(600 + side * 22f, 340));
            Check(ranged.InMeleeReach(foe), $"{rangedId}: an enemy at arm's length is inside melee reach");
            var projectiles = battle.GetChildren().OfType<Projectile>().Count(p => p.Visible);
            Invoke(battle, "SimulateUnits", 1f / 60);
            Check(ranged.IsAttackCommitted && ranged.IsMeleeStrike, $"{rangedId}: strikes in melee instead of shooting point-blank");
            var health = foe.Health;
            for (var i = 0; i < 90 && foe.Health >= health && !foe.IsDead; i++) Invoke(battle, "SimulateUnits", 1f / 60);
            Check(foe.Health < health, $"{rangedId}: the melee strike lands");
            Check(battle.GetChildren().OfType<Projectile>().Count(p => p.Visible) == projectiles, $"{rangedId}: no projectile is fired in melee");
            await CloseBattle(battle);

            battle = await OpenBattle(1);
            ranged = (Unit)Invoke(battle, "SpawnUnit", rangedTeam, new UnitStats(GameData.GetUnit(rangedId)), new Vector2(600, 340));
            foe = (Unit)Invoke(battle, "SpawnUnit", foeTeam, new UnitStats(GameData.GetUnit(foeId)),
                new Vector2(600 + side * (ranged.AttackRange - 6f), 340));
            Check(!ranged.InMeleeReach(foe), $"{rangedId}: an enemy at shooting range is outside melee reach");
            Invoke(battle, "SimulateUnits", 1f / 60);
            Check(ranged.IsAttackCommitted && !ranged.IsMeleeStrike, $"{rangedId}: shoots at range");
            await CloseBattle(battle);
        }
    }

    private async Task CheckCouragePacing()
    {
        var state = GameState.Instance;
        var saved = state.BuildSaveData();
        state.ResetProgress(); state.SetAnalyticsConsent(false); state.SetShowHints(false);
        state.SetDifficulty(DifficultyCatalog.NormalId);
        var battle = await OpenBattle(1);
        Check(Read<float>(battle, "_courage") == 0f && state.HasCampaignScoutBonus(1) &&
            Mathf.IsEqualApprox(Read<float>(battle, "_campaignScoutCourageGainScale"), state.GetCampaignScoutCourageGainScale(1)),
            "Battles open at zero courage; the first-clear scout bonus speeds regeneration instead");
        var swordsman = Read<BattleDeckState>(battle, "_deck").Roster.Single(unit => unit.Id == "player_brawler");
        Write(battle, "_courage", 0f);
        for (var tick = 0; tick < 300; tick++) battle._PhysicsProcess(1.0 / 60);
        Invoke(battle, "DeployPlayerUnit", swordsman);
        Check(Read<int>(battle, "_playerDeployments") == 0 && Read<float>(battle, "_courage") < swordsman.Cost,
            "An empty courage bar cannot fund another Swordsman within five seconds, even with the first-clear boost");
        var pausedCourage = Read<float>(battle, "_courage");
        Write(battle, "_battlePaused", true);
        for (var tick = 0; tick < 300; tick++) battle._PhysicsProcess(1.0 / 60);
        Check(Mathf.IsEqualApprox(Read<float>(battle, "_courage"), pausedCourage), "Paused battles do not regenerate courage");
        Write(battle, "_battlePaused", false);
        for (var tick = 0; tick < 120; tick++) battle._PhysicsProcess(1.0 / 60);
        var available = Read<float>(battle, "_courage");
        Invoke(battle, "DeployPlayerUnit", swordsman);
        Check(Read<int>(battle, "_playerDeployments") == 1 && Mathf.IsEqualApprox(Read<float>(battle, "_courage"), available - swordsman.Cost),
            "Seven seconds of regeneration enables one Swordsman and charges its normal cost");
        Write(battle, "_courage", Read<float>(battle, "_maxCourage") - 1f);
        for (var tick = 0; tick < 60; tick++) battle._PhysicsProcess(1.0 / 60);
        Check(Mathf.IsEqualApprox(Read<float>(battle, "_courage"), Read<float>(battle, "_maxCourage")), "Slower regeneration still respects maximum courage");
        await CloseBattle(battle);
        Invoke(state, "ApplySavedData", saved);
    }

    // Magic runs on mana: enemy kills grant it, the bar caps it, and casting spends it without touching courage.
    private async Task CheckManaFromKills()
    {
        var battle = await OpenBattle(1);
        var tuning = GameData.Combat;
        Check(Mathf.IsEqualApprox(Read<float>(battle, "_mana"), tuning.ManaStart) && Mathf.IsEqualApprox(Read<float>(battle, "_maxMana"), tuning.ManaMax),
            "Battles open with the starting mana and the configured cap");
        Unit Spawn(Team team, string id) => (Unit)Invoke(battle, "SpawnUnit", team, new UnitStats(GameData.GetUnit(id)), new Vector2(team == Team.Enemy ? 700 : 300, 340));
        Spawn(Team.Enemy, "enemy_walker").TakeDamage(1e6f);
        Invoke(battle, "CleanupDeadUnits");
        var afterKill = tuning.ManaStart + tuning.ManaPerEnemyKill;
        Check(Mathf.IsEqualApprox(Read<float>(battle, "_mana"), afterKill), $"Killing an enemy grants {tuning.ManaPerEnemyKill} mana");
        Spawn(Team.Player, "player_brawler").TakeDamage(1e6f);
        Invoke(battle, "CleanupDeadUnits");
        Check(Mathf.IsEqualApprox(Read<float>(battle, "_mana"), afterKill), "Losing a troop grants no mana");
        for (var i = 0; i < 40; i++) Spawn(Team.Enemy, "enemy_walker").TakeDamage(1e6f);
        Invoke(battle, "CleanupDeadUnits");
        Check(Mathf.IsEqualApprox(Read<float>(battle, "_mana"), tuning.ManaMax), "Mana stops at the bar's maximum");

        var fireball = GameData.GetSpell("spell_fireball");
        var spells = Read<BattleSpellState>(battle, "_spellDeck");
        spells.Initialize(new[] { fireball });
        var cost = GameState.Instance.BuildSpellStats(fireball).ManaCost;
        Write(battle, "_courage", 50f);
        Invoke(battle, "TryCastSpellAt", fireball, new Vector2(700, 340));
        Check(Read<int>(battle, "_spellsCast") == 1 && Mathf.IsEqualApprox(Read<float>(battle, "_mana"), tuning.ManaMax - cost)
            && Read<float>(battle, "_courage") == 50f, "Casting spends the spell's mana cost and leaves courage alone");
        spells.ReduceCooldowns(1000);
        Write(battle, "_mana", cost - 1f);
        Write(battle, "_courage", Read<float>(battle, "_maxCourage"));
        Invoke(battle, "TryCastSpellAt", fireball, new Vector2(700, 340));
        Check(Read<int>(battle, "_spellsCast") == 1, "Magic cannot be cast without enough mana, however much courage is banked");
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
        director.Initialize(GameData.MaxStage, GameData.GetStage(GameData.MaxStage), new CombatTuning(), GameData.GetEnemyUnits());
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
        GameState.Instance.UnlockNextStage(GameData.MaxStage - 1);
        // A modest reference squad, with normal unit upgrades but no equipment or purchases.
        var tactical = OS.GetCmdlineUserArgs().Contains("--tactical");
        var level = tactical ? (stage < 10 ? 1 : stage < 15 ? 2 : stage < 35 ? 3 : stage < 62 ? 4 : 5) : Math.Min(5, 1 + (stage - 1) / 13);
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
        else if (tactical && !OS.GetCmdlineUserArgs().Contains("--starter-squad")) FieldStageDeck(stage);
        var levels = Read<Dictionary<string, int>>(GameState.Instance, "_unitUpgradeLevels");
        foreach (var id in GameState.Instance.ActiveDeckUnitIds) levels[id] = level;
        if (tactical)
        {
            var upgrades = Read<Dictionary<string, int>>(GameState.Instance, "_baseUpgradeLevels");
            var upgradeLevel = Math.Min(3, (stage - 1) / 13);
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
            foreach (var id in GameState.Instance.ActiveDeckSpellIds) spellLevels[id] = Math.Min(3, 1 + (stage - 1) / 25);
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
        var shotArg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--shot-every="));
        var shotEvery = shotArg == null || DisplayServer.GetName() == "headless" ? 0f
            : float.Parse(shotArg["--shot-every=".Length..], System.Globalization.CultureInfo.InvariantCulture);
        if (shotEvery > 0) System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath("res://artifacts/combat-review"));
        var nextShot = shotEvery;
        var peak = 0;
        var firstContact = -1f;
        var firstGateDamage = -1f;
        var waveTimes = new List<float>();
        var director = Read<BattleSpawnDirector>(battle, "_spawnDirector");
        var tick = 0;
        var pushBurst = 0;
        var gateSeconds = 0f;
        var gateCrowd = 0;
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
                var mana = Read<float>(battle, "_mana");
                var bankCourage = false;
                if (tactical)
                {
                    var hurt = units.Where(x => x.Team == Team.Player && !x.IsDead && x.HealthRatio < 0.5f)
                        .OrderBy(x => x.HealthRatio).FirstOrDefault();
                    var cluster = units.Where(x => x.Team == Team.Enemy && !x.IsDead)
                        .OrderByDescending(x => units.Count(y => y.Team == Team.Enemy && !y.IsDead && y.Position.DistanceTo(x.Position) < 76))
                        .FirstOrDefault();
                    var spellDeck = Read<BattleSpellState>(battle, "_spellDeck");
                    // Magic is paid in mana from kills, so it never competes with troops for courage.
                    bool Affordable(string id) => spellDeck.GetCooldownRemaining(id) <= 0 && mana >= GameState.Instance.BuildSpellStats(GameData.GetSpell(id)).ManaCost;
                    if (hurt != null && Affordable("spell_heal"))
                        Invoke(battle, "TryCastSpellAt", GameData.GetSpell("spell_heal"), hurt.Position);
                    else if (cluster != null && units.Count(x => x.Team == Team.Enemy && !x.IsDead && x.Position.DistanceTo(cluster.Position) < 76) >= 3
                             && Affordable("spell_fireball"))
                        Invoke(battle, "TryCastSpellAt", GameData.GetSpell("spell_fireball"), cluster.Position);
                }
                // With no enemy advancing, bank courage and push in groups, so troops reach the gate together
                // instead of marching into its weapons one at a time.
                if (tactical)
                {
                    var threat = units.Any(x => x.Team == Team.Enemy && !x.IsDead && x.Position.X < GameData.Combat.EnemyBaseX - 220);
                    if (threat) pushBurst = 0;
                    else if (pushBurst == 0 && courage < 70) bankCourage = true;
                    else if (pushBurst == 0) pushBurst = 3;
                }
                var frontline = units.Count(x => x.Team == Team.Player && !x.IsDead && !x.UsesProjectile && Math.Abs(x.Position.Y - targetY) < 110);
                // An empty front line gets the strongest melee type that is least represented on the field.
                var preferred = frontline == 0 ? deck.Roster.Where(x => !x.UsesProjectile)
                        .OrderBy(x => units.Count(u => !u.IsDead && u.Team == Team.Player && u.DefinitionId == x.Id))
                        .ThenByDescending(x => Math.Sqrt(x.MaxHealth * x.AttackDamage / Math.Max(.3, x.AttackCooldown))).FirstOrDefault() :
                    deck.Roster.Where(x => x.UsesProjectile).OrderBy(x => units.Count(u => !u.IsDead && u.Team == Team.Player && u.DefinitionId == x.Id)).FirstOrDefault();
                var order = deck.Roster.OrderBy(x => x == preferred ? 0 : 1);
                var card = order.FirstOrDefault(x => deck.CanDeploy(x, courage, false, out _));
                if (card != null && !bankCourage)
                {
                    Invoke(battle, "DeployPlayerUnit", card);
                    if (pushBurst > 0) pushBurst--;
                }
                if (Read<bool>(battle, "_campaignConvoyCommandReady")) Invoke(battle, "TryActivateCampaignConvoyCommand");
            }
            battle._PhysicsProcess(1.0 / 60);
            var elapsed = Read<float>(battle, "_elapsed");
            if (director.NextScriptedWaveIndex > waveTimes.Count) waveTimes.Add(elapsed);
            if (firstGateDamage < 0 && Read<float>(battle, "_enemyBaseHealth") < Read<float>(battle, "_enemyBaseMaxHealth")) firstGateDamage = elapsed;
            if (OS.GetCmdlineUserArgs().Contains("--trace") && tick % 300 == 0)
                GD.Print($"TRACE t={elapsed:0} hull={Read<float>(battle, "_playerBaseHealth"):0} courage={Read<float>(battle, "_courage"):0} mana={Read<float>(battle, "_mana"):0} " +
                    $"players=[{string.Join(" ", units.Where(x => x.Team == Team.Player && !x.IsDead).Select(x => $"{x.DefinitionId.Replace("player_", "")}@{x.Position.X:0},{x.Position.Y:0}"))}] " +
                    $"enemies=[{string.Join(" ", units.Where(x => x.Team == Team.Enemy && !x.IsDead).Select(x => $"{x.DefinitionId.Replace("enemy_", "")}@{x.Position.X:0},{x.Position.Y:0}{(x.VisualClass == "boss" ? $"[{x.Health:0}/{x.MaxHealth:0}]" : "")}"))}]");
            var atGate = units.Count(x => x.Team == Team.Player && !x.IsDead && x.Position.X > GameData.Combat.EnemyBaseX - 80);
            if (atGate > 0) gateSeconds += 1f / 60;
            gateCrowd = Math.Max(gateCrowd, atGate);
            if (firstContact < 0 && tick % 30 == 0 && units.Any(p => !p.IsDead && p.Team == Team.Player &&
                units.Any(e => !e.IsDead && e.Team == Team.Enemy && p.Position.DistanceTo(e.Position) < 180))) firstContact = elapsed;
            // --shot-every=S: a frame every S seconds, for reviewing unit art and deaths in play.
            if (shotEvery > 0 && elapsed >= nextShot)
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://artifacts/combat-review/stage-{stage}-t{elapsed:000}.png"));
                nextShot += shotEvery;
            }
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
        var battleResult = (StageBattleResult)Invoke(battle, "BuildStageBattleResult");
        var victory = Read<bool>(battle, "_battleEnded") && Read<float>(battle, "_enemyBaseHealth") <= 0 && Read<float>(battle, "_playerBaseHealth") > 0;
        var evaluation = StageObjectives.EvaluateBattle(Read<StageDefinition>(battle, "_stageData"), battleResult, victory);
        GD.Print("COMBAT_SAMPLE: " + System.Text.Json.JsonSerializer.Serialize(new {
            fieldTactics = OS.GetCmdlineUserArgs().Contains("--field-tactics"),
            baseCouragePerSecond = GameData.Combat.CourageGainPerSecond,
            mapWidth = GameData.Combat.BattlefieldLeft + GameData.Combat.BattlefieldRight,
            firstContact, firstGateDamage, waveTimes, stars = evaluation.StarsEarned,
            objectives = evaluation.Outcomes, battleResult.CompletedMissionEvents, battleResult.FailedMissionEvents,
            battleResult.TotalMissionEvents, battleResult.PlayerHazardHits, battleResult.CampaignBossPressureTriggers,
            battleResult.PlayerDeployments,
            milestoneRelics = OS.GetCmdlineUserArgs().Contains("--milestone-relics"), timeLimit,
            stage, level, tactical, seedOffset = OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--seed-offset="))?.Split('=')[1] ?? "0", armaments = OS.GetCmdlineUserArgs().Contains("--armaments"), commonRelics = OS.GetCmdlineUserArgs().Contains("--common-relics"), investment, squad = string.Join(",", deck.Roster.Select(x => x.Id)), seconds = Math.Round(Read<float>(battle, "_elapsed"), 1),
            won = Read<bool>(battle, "_battleEnded") && Read<float>(battle, "_enemyBaseHealth") <= 0 && Read<float>(battle, "_playerBaseHealth") > 0,
            hull = Math.Round(Read<float>(battle, "_playerBaseHealth") / Read<float>(battle, "_playerBaseMaxHealth"), 3),
            gate = Math.Round(Read<float>(battle, "_enemyBaseHealth") / Read<float>(battle, "_enemyBaseMaxHealth"), 3),
            defeats = Read<int>(battle, "_enemyDefeats"), spells = Read<int>(battle, "_spellsCast"), peak, bosses,
            gateSeconds = Math.Round(gateSeconds, 1), gateCrowd
        }));
        if (!Read<bool>(battle, "_battleEnded"))
            GD.Print("COMBAT_REMAINS: " + System.Text.Json.JsonSerializer.Serialize(units.Where(x => !x.IsDead)
                .Select(x => new { id = x.DefinitionId, hp = Math.Round(x.Health), maxHp = Math.Round(x.MaxHealth),
                    x = Math.Round(x.Position.X), y = Math.Round(x.Position.Y) })));
        await CloseBattle(battle);
    }
    // Test-only balance experiments, in memory: --combat=CourageGainPerSecond=3.6,... sets tuning values;
    // --stage-mult=EnemyDamageScale=0.9,... multiplies a stage field on every stage; --stage-shift adds to one.
    private static void ApplyTuningOverrides(string[] args)
    {
        static IEnumerable<(string Key, float Value)> Pairs(string[] args, string flag) =>
            args.Where(a => a.StartsWith(flag)).SelectMany(a => a[flag.Length..].Split(','))
                .Select(p => p.Split('=')).Where(p => p.Length == 2)
                .Select(p => (p[0], float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture)));
        foreach (var (key, value) in Pairs(args, "--combat="))
        {
            var prop = typeof(CombatTuning).GetProperty(key) ?? throw new InvalidOperationException("Unknown tuning " + key);
            prop.SetValue(GameData.Combat, Convert.ChangeType(value, prop.PropertyType));
            GD.Print($"TUNING_OVERRIDE {key}={value}");
        }
        if (args.Contains("--no-hazards"))
            foreach (var stage in GameData.Stages) stage.Hazards = Array.Empty<StageHazardDefinition>();
        if (args.Contains("--no-modifiers"))
            foreach (var stage in GameData.Stages) stage.Modifiers = Array.Empty<StageModifierDefinition>();
        foreach (var (flag, multiply) in new[] { ("--stage-mult=", true), ("--stage-shift=", false) })
            foreach (var (key, value) in Pairs(args, flag))
            {
                var prop = typeof(StageDefinition).GetProperty(key) ?? throw new InvalidOperationException("Unknown stage field " + key);
                foreach (var stage in GameData.Stages)
                {
                    var current = Convert.ToSingle(prop.GetValue(stage));
                    prop.SetValue(stage, Convert.ChangeType(multiply ? current * value : current + value, prop.PropertyType));
                }
                GD.Print($"STAGE_OVERRIDE {key}{(multiply ? "*" : "+")}{value}");
            }
    }

    // A player at this stage owns what it has unlocked and fields a balanced deck: its three strongest frontline
    // troops and three strongest ranged troops (by health x damage rate), with every unlocked spell.
    private void FieldStageDeck(int stage)
    {
        var state = GameState.Instance;
        var available = GameData.GetPlayerUnits().Where(u => u.UnlockStage <= stage).ToArray();
        static double Power(UnitDefinition u) => Math.Sqrt(u.MaxHealth * u.AttackDamage / Math.Max(.3, u.AttackCooldown));
        var melee = available.Where(u => !u.UsesProjectile).OrderByDescending(Power).Take(3);
        var ranged = available.Where(u => u.UsesProjectile).OrderByDescending(Power).Take(3);
        var owned = Read<HashSet<string>>(state, "_ownedPlayerUnitIds");
        var active = Read<List<string>>(state, "_activeDeckUnitIds");
        active.Clear();
        foreach (var unit in melee.Concat(ranged)) { owned.Add(unit.Id); active.Add(unit.Id); }
        var spells = GameData.PlayerSpellIds.Select(GameData.GetSpell).Where(x => x.UnlockStage <= stage)
            .OrderBy(x => x.Id is "spell_heal" or "spell_fireball" ? 0 : 1).ThenByDescending(x => x.UnlockStage).Take(5).ToArray();
        var ownedSpells = Read<HashSet<string>>(state, "_ownedPlayerSpellIds");
        var activeSpells = Read<List<string>>(state, "_activeDeckSpellIds");
        activeSpells.Clear();
        foreach (var spell in spells) { ownedSpells.Add(spell.Id); activeSpells.Add(spell.Id); }
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
