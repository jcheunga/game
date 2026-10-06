using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewBattleCleanup()
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        T Read<T>(object owner, string name) => (T)owner.GetType().GetField(name, hidden)!.GetValue(owner)!;
        void Write(object owner, string name, object value) => owner.GetType().GetField(name, hidden)!.SetValue(owner, value);
        object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, hidden,
            null, args.Select(arg => arg.GetType()).ToArray(), null)!.Invoke(owner, args);
        _output = ProjectSettings.GlobalizePath("res://artifacts/battle-cleanup/" + (OS.GetCmdlineUserArgs().Contains("--small-window") ? "small" : "desktop"));
        System.IO.Directory.CreateDirectory(_output);
        var state = GameState.Instance;
        state.ResetProgress(); state.SetShowHints(false); state.SetAnalyticsConsent(false);
        var fixture = state.BuildSaveData(); fixture.HighestUnlockedStage = GameData.MaxStage; fixture.Food = 24;
        fixture.OwnedPlayerUnitIds = new[] { GameData.PlayerBrawlerId, GameData.PlayerShooterId, GameData.PlayerBallistaId };
        fixture.ActiveDeckUnitIds = fixture.OwnedPlayerUnitIds;
        fixture.OwnedPlayerSpellIds = GameData.PlayerSpellIds.ToArray(); fixture.ActiveDeckSpellIds = GameData.PlayerSpellIds.Take(2).ToArray();
        fixture.UnitLevels = fixture.OwnedPlayerUnitIds.ToDictionary(id => id, _ => 4);
        state.RestoreCloudSave(fixture);

        async Task<BattleController> Battle(BattleRunMode mode = BattleRunMode.Campaign)
        {
            if (GetTree().CurrentScene != null) { GetTree().CurrentScene.QueueFree(); GetTree().CurrentScene = null; await Wait(.05); }
            state.SetSelectedStage(48);
            typeof(GameState).GetProperty(nameof(GameState.CurrentBattleMode))!.SetValue(state, mode);
            var battle = GD.Load<PackedScene>(SceneRouter.BattleScene).Instantiate<BattleController>();
            GetTree().Root.AddChild(battle); GetTree().CurrentScene = battle; battle.SetPhysicsProcess(false);
            Check(Read<float>(battle, "_courage") == 0, mode + " starts with zero courage");
            await Wait(.1);
            Check(typeof(BattleController).GetField("_stageHazards", hidden) == null,
                mode + " has no retired hazard system");
            return battle;
        }
        foreach (var mode in Enum.GetValues<BattleRunMode>()) await Battle(mode);
        var fight = await Battle();
        var deck = Read<BattleDeckState>(fight, "_deck"); var first = deck.Roster[0];
        Write(fight, "_courage", 100f);
        Call(fight, "TryUseSelectionAt", new Vector2(350, 340));
        Check(Read<int>(fight, "_playerDeployments") == 0, "A ground click never deploys a unit");
        Call(fight, "DeployPlayerUnit", first);
        Check(Read<int>(fight, "_playerDeployments") == 1 && !deck.HasArmedUnit
            && Mathf.IsEqualApprox(Read<float>(fight, "_courage"), 100 - first.Cost), "Selecting a unit card deploys it once and spends once");
        Call(fight, "TryUseSelectionAt", new Vector2(350, 340));
        Check(Read<int>(fight, "_playerDeployments") == 1, "A later ground click cannot deploy another unit");
        Call(fight, "DeployPlayerUnit", first);
        Check(Read<int>(fight, "_playerDeployments") == 1, "A recovering card cannot deploy again");
        Call(fight, "DeployPlayerUnit", GameData.GetUnit(GameData.PlayerMarksmanId));
        Check(Read<int>(fight, "_playerDeployments") == 1, "Unowned units outside the warband cannot be deployed");
        Check(UnitActiveAbilityCatalog.GetForUnit(GameData.PlayerNecromancerId) == null
            && UnitActiveAbilityCatalog.GetForUnit(GameData.PlayerMechanicId) == null, "Abilities cannot summon additional allied units");
        Check(typeof(BattleController).GetMethods(hidden).All(method => method.Name != "SpawnSupportUnit"),
            "Automatic allied support spawning is removed");
        Call(fight, "SpawnEnemyUnit", new UnitStats(GameData.GetUnit(GameData.EnemyRunnerId)), new Vector2(650, 200));
        var spawned = Read<List<Unit>>(fight, "_units").Last();
        Check(spawned.Position.X == GameData.Combat.EnemySpawnX && Math.Abs(spawned.Position.Y - 340) <= GameData.Combat.LaneHalfHeight,
            "An enemy requested in the middle of the field enters at the stronghold, inside the band");
        var director = Read<BattleSpawnDirector>(fight, "_spawnDirector");
        Check(director.NextEncounterSpawnX == GameData.Combat.EnemySpawnX, "Advance-triggered waves also enter at the stronghold");
        foreach (var terrain in new[] { "marsh", "pass", "grove", "foundry", "night", "cathedral" })
        {
            var ambient = new BattleAmbientParticles(); fight.AddChild(ambient); ambient.Setup(terrain, 84, 2476, 96, 584);
            foreach (var weather in new[] { "rain", "fog", "ashstorm", "blizzard" }) ambient.ApplyWeather(weather, 84, 2476, 96, 584);
            Check(Walk(ambient).OfType<CpuParticles2D>().All(emitter => emitter.Texture != null && emitter.ScaleAmountMax < 1),
                terrain + " weather uses textured particles at the intended size");
            ambient.QueueFree();
        }
        await Wait(.05);
        var ballista = (Unit)Call(fight, "SpawnUnit", Team.Player, state.BuildPlayerUnitStats(GameData.GetUnit(GameData.PlayerBallistaId)), new Vector2(GameData.Combat.EnemyBaseX - 120, 340));
        var hull = Read<float>(fight, "_enemyBaseHealth");
        Call(fight, "ResolveAttackBase", ballista);
        var projectile = Walk(fight).OfType<Projectile>().Single(); projectile.SetPhysicsProcess(false);
        Check(projectile.Style == ProjectileStyles.BallistaBolt
            && Read<float>(fight, "_enemyBaseHealth") == hull, "Ballista looses a bolt; the base is undamaged during flight");
        projectile._PhysicsProcess(.12);
        await Capture("01-bolt-flight");
        projectile._PhysicsProcess(2);
        var impacted = Read<float>(fight, "_enemyBaseHealth");
        projectile._PhysicsProcess(2);
        Check(impacted < hull && Read<float>(fight, "_enemyBaseHealth") == impacted, "A ballista bolt damages the base exactly once on impact");
        spawned.TakeDamage(10000); Call(fight, "CleanupDeadUnits");
        var victim = (Unit)Call(fight, "SpawnUnit", Team.Enemy, new UnitStats(GameData.GetUnit(GameData.EnemyBruteId)), new Vector2(GameData.Combat.EnemyBaseX - 36, 340));
        var health = victim.Health;
        Call(fight, "ActiveAbilitySnipe", ballista);
        projectile = Walk(fight).OfType<Projectile>().Single(); projectile.SetPhysicsProcess(false);
        Check(projectile.Style == ProjectileStyles.BallistaBolt && victim.Health == health,
            "Ballista Anchor Shot also looses a bolt");
        projectile._PhysicsProcess(2);
        Check(victim.Health < health, "Anchor Shot applies its damage on impact");
        Call(fight, "SetBattleCameraX", 700f);
        Write(fight, "_cardPointerDown", true); Write(fight, "_cardDragging", true); Write(fight, "_dragSpell", GameData.GetSpell("spell_fireball"));
        Write(fight, "_cardPointerPosition", fight.GetGlobalTransformWithCanvas() * new Vector2(680, 340));
        fight.SetProcess(false); await Capture("02-ground-spell-preview");
        Write(fight, "_cardPointerDown", false); Write(fight, "_cardDragging", false);
        Call(fight, "TogglePause");
        Check(!Walk(fight).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text.Contains("1–6")), "Pause menu contains no control-instruction paragraph");
        await Capture("03-pause-menu"); Call(fight, "TogglePause");

        fight = await Battle(BattleRunMode.Endless);
        var runs = state.EndlessRuns; var gold = state.Gold;
        Write(fight, "_elapsed", 90f); Write(fight, "_enemyDefeats", 12);
        var payout = (int)Call(fight, "CalculateEndlessGoldReward");
        Call(fight, "RetreatToMap"); await Wait(.7);
        Check(GetTree().CurrentScene is MapMenu quitHome && !quitHome.HasHomeModal && state.EndlessRuns == runs + 1 && state.Gold == gold + payout,
            "Quitting Endless banks rewards and returns directly to the map without another modal");
        fight = await Battle(BattleRunMode.Endless); Call(fight, "EndBattle", false); await Wait(.5);
        Check(Walk(fight).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text == "Run over"), "An endless run ends on the shared result card");
        AuditText("Endless result"); await Capture("03-endless-result");
        Call(fight, "HandleEndPanelSecondaryAction"); await Wait(.7);
        Check(GetTree().CurrentScene is MapMenu endlessHome && endlessHome.HomeModalDestination == SceneRouter.EndlessScene,
            "Endless result returns to the current preparation modal");
        await Capture("04-endless-return");
        foreach (var destination in typeof(SceneRouter).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string)).Select(field => (string)field.GetRawConstantValue()!)
            .Where(path => path is not (SceneRouter.MainMenuScene or SceneRouter.MapScene or SceneRouter.BattleScene)))
        {
            fight = await Battle();
            Call(SceneRouter.Instance, "ChangeScene", destination, true); await Wait(.65);
            Check(GetTree().CurrentScene is MapMenu home && home.HomeModalDestination == destination,
                System.IO.Path.GetFileNameWithoutExtension(destination) + " opens with the current map modal from battle");
        }
        MobilePresentation.TestOverride = true; GetWindow().Size = new Vector2I(844, 390);
        fight = await Battle(); SceneRouter.Instance.GoToLoadout(); await Wait(.7);
        var prepare = (MapMenu)GetTree().CurrentScene;
        var deploy = Walk(prepare).OfType<Button>().Single(button => button.IsVisibleInTree() && button.Text.StartsWith("Deploy"));
        Check(deploy.GetGlobalRect().End.Y <= prepare.GetViewportRect().End.Y && deploy.GetGlobalRect().Position.Y >= 0,
            "Phone preparation keeps the entry button inside the current modal");
        Check(prepare.GetNode<RealmModal>("HomeModal").Content.GetGlobalRect().Encloses(deploy.GetGlobalRect()),
            "Phone entry action is fully visible within the modal content");
        Check(state.SelectedStage == 48, "Opening a preparation modal preserves the selected stage");
        Check(Walk(prepare).OfType<LoadoutMenu>().Single().IsVisibleInTree(), "Phone preparation is visible after returning from battle");
        await Capture("05-phone-preparation-return");
        fight = await Battle(BattleRunMode.Endless); Call(fight, "EndBattle", false);
        Call(fight, "HandleEndPanelSecondaryAction"); await Wait(.7);
        var phoneEndless = (MapMenu)GetTree().CurrentScene;
        var start = Walk(phoneEndless).OfType<Button>().Single(button => button.IsVisibleInTree() && button.Text == "Begin endless march");
        Check(start.GetGlobalRect().End.Y <= phoneEndless.GetViewportRect().End.Y,
            "Phone Endless preparation keeps the launch action inside the current modal");
        Check(phoneEndless.GetNode<RealmModal>("HomeModal").Content.GetGlobalRect().Encloses(start.GetGlobalRect()),
            "Phone Endless launch action is fully visible within the modal content");
        await Capture("06-phone-endless-return");
        MobilePresentation.TestOverride = null;
        GD.Print($"BATTLE_CLEANUP_REVIEW_RESULT: {_failures} failures"); QuitAfterAudio(_failures == 0 ? 0 : 1);
    }
}
