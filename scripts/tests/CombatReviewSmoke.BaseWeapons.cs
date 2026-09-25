using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

public partial class CombatReviewSmoke
{
    private async Task CheckBaseWeapons()
    {
        var state = GameState.Instance;
        var original = (GameSaveData)Invoke(state, "BuildSaveData");
        var levels = Read<Dictionary<string, int>>(state, "_baseUpgradeLevels");
        foreach (var upgrade in BaseUpgradeCatalog.GetAll()) levels[upgrade.Id] = 0;
        var battle = await OpenBattle(1);
        Vector2 Core(bool player) => (Vector2)typeof(BattleController).GetProperty(
            player ? "PlayerBaseCorePosition" : "EnemyBaseCorePosition", Hidden)!.GetValue(battle)!;
        Unit Spawn(Team team, Vector2 position, string visual = "fighter", float speed = 40, float armor = 1) =>
            (Unit)Invoke(battle, "SpawnUnit", team, new UnitStats(new UnitDefinition {
                Id = "base_weapon_test_" + visual, DisplayName = "Test " + visual,
                MaxHealth = 500, Speed = speed, DamageTakenScale = armor, VisualClass = visual
            }), position);
        Projectile[] Shots() => battle.GetChildren().OfType<Projectile>().ToArray();
        void ResolveShots()
        {
            foreach (var shot in Shots()) shot._PhysicsProcess(5);
        }
        var core = Core(true);
        var arrows = BaseWeaponCatalog.Wagon(BaseUpgradeCatalog.ArcherCrewId, 0);
        Check(arrows != null && BaseWeaponCatalog.Wagon(BaseUpgradeCatalog.BallistaId, 0) == null,
            "New saves start with archers; extra mounts require an upgrade");
        var outside = Spawn(Team.Enemy, core + new Vector2(550, 0));
        Invoke(battle, "TickBaseWeapons", 1f);
        Check(Shots().Length == 0, "Bases do not shoot out-of-range enemies");
        var infantry = Spawn(Team.Enemy, core + new Vector2(110, 0));
        var runner = Spawn(Team.Enemy, core + new Vector2(240, 0), "saboteur", 100);
        var ally = Spawn(Team.Player, core + new Vector2(120, 10));
        Check(ReferenceEquals(Invoke(battle, "FindBaseWeaponTarget", Team.Player, core, arrows), runner),
            "Wagon archers prioritize fast raiders over closer infantry");
        Invoke(battle, "TickBaseWeapons", 0.1f);
        Check(Shots().Length == 1, "Starting wagon fires one ranged shot");
        var shotPosition = Shots()[0].Position;
        Write(battle, "_endlessCheckpointActive", true);
        ResolveShots();
        Check(Shots()[0].Position == shotPosition && runner.Health == runner.MaxHealth, "Wagon projectiles freeze during checkpoint choices");
        Write(battle, "_endlessCheckpointActive", false);
        ResolveShots();
        Check(runner.Health < runner.MaxHealth && ally.Health == ally.MaxHealth && outside.Health == outside.MaxHealth,
            "Arrows damage their target without friendly fire or distant damage");
        Invoke(battle, "TickBaseWeapons", 0.1f);
        Check(Shots().Length == 0, "Wagon weapons respect their cooldown");
        Invoke(battle, "TickBaseWeapons", 3f);
        Check(Shots().Length == 1 && Shots()[0].Visible, "A recycled projectile remains visible on the next shot");
        ResolveShots();
        var armored = Spawn(Team.Enemy, core + new Vector2(280, -60), "brute", 30, 0.5f);
        runner.SetUntargetable(5);
        Check(!ReferenceEquals(Invoke(battle, "FindBaseWeaponTarget", Team.Player, core, arrows), runner),
            "Base weapons skip untargetable enemies");
        runner.TickActiveAbilityTimer(6);
        var ballista = BaseWeaponCatalog.Wagon(BaseUpgradeCatalog.BallistaId, 1);
        Check(ReferenceEquals(Invoke(battle, "FindBaseWeaponTarget", Team.Player, core, ballista), armored),
            "Ballista prioritizes armored targets");
        Invoke(battle, "FireBaseWeapon", Team.Player, ballista, armored);
        ResolveShots();
        Check(Mathf.IsEqualApprox(armored.MaxHealth - armored.Health, ballista.Damage * 1.5f * 0.5f),
            "Ballista armor bonus still respects the target's damage reduction");
        var shield = (Unit)Invoke(battle, "SpawnUnit", Team.Enemy, new UnitStats(GameData.GetUnit("enemy_shieldwall")), core + new Vector2(230, -60));
        var armoredHealth = armored.Health;
        Invoke(battle, "FireBaseWeapon", Team.Player, arrows, armored);
        ResolveShots();
        Check(armored.Health == armoredHealth && shield.Health < shield.MaxHealth, "Shield walls intercept wagon arrows aimed behind them");
        shield.TakeDamage(10000);
        var firepot = BaseWeaponCatalog.Wagon(BaseUpgradeCatalog.FirepotId, 1);
        var clusterA = Spawn(Team.Enemy, core + new Vector2(210, 110));
        var clusterB = Spawn(Team.Enemy, core + new Vector2(220, 110));
        Check(new[] { clusterA, clusterB }.Contains((Unit)Invoke(battle, "FindBaseWeaponTarget", Team.Player, core, firepot)),
            "Firepots prefer clustered enemies");
        ally.Position = clusterA.Position;
        Invoke(battle, "FireBaseWeapon", Team.Player, firepot, clusterA);
        ResolveShots();
        Check(clusterA.Health < clusterA.MaxHealth && clusterB.Health < clusterB.MaxHealth && ally.Health == ally.MaxHealth,
            "Firepot impact damages a group and spares allies");
        Invoke(battle, "FireBaseWeapon", Team.Player, arrows, clusterA);
        clusterA.TakeDamage(10000);
        var before = clusterB.Health;
        ResolveShots();
        Check(clusterB.Health == before && Shots().Length == 0, "Dead projectile targets cancel cleanly");

        levels[BaseUpgradeCatalog.BallistaId] = 1;
        levels[BaseUpgradeCatalog.FirepotId] = 1;
        levels[BaseUpgradeCatalog.ArrowVolleyId] = 2;
        levels[BaseUpgradeCatalog.EmergencyRepairId] = 3;
        levels[BaseUpgradeCatalog.ReinforcedArmorId] = 5;
        Invoke(battle, "InitializeBaseWeapons");
        Invoke(battle, "TickBaseWeapons", 0.1f);
        Check(Shots().Length == 3, "All three purchased weapon mounts fire together");
        if (OS.GetCmdlineUserArgs().Contains("--screenshots"))
        {
            foreach (var shot in Shots()) { shot._PhysicsProcess(0.18); shot.SetPhysicsProcess(false); }
            Invoke(battle, "UpdateHud");
            battle.QueueRedraw();
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://artifacts/combat-review/wagon-armaments.png"));
            foreach (var shot in Shots()) shot.SetPhysicsProcess(true);
        }
        ResolveShots();
        Write(battle, "_wagonVolleyRecovery", 0f);
        Invoke(battle, "TickWagonSkills", 0.1f);
        Check(Shots().Length == 3 && Read<float>(battle, "_wagonVolleyRecovery") > 0, "Volley fires at three targets and enters recovery");
        ResolveShots();
        var maxHull = Read<float>(battle, "_playerBaseMaxHealth");
        Write(battle, "_playerBaseHealth", maxHull * 0.3f);
        Invoke(battle, "TickWagonSkills", 0.1f);
        Check(Mathf.IsEqualApprox(Read<float>(battle, "_playerBaseHealth"), maxHull * 0.49f), "Emergency repairs restore the purchased hull percentage");
        Write(battle, "_playerBaseHealth", maxHull * 0.3f);
        Invoke(battle, "TickWagonSkills", 0.1f);
        Check(Mathf.IsEqualApprox(Read<float>(battle, "_playerBaseHealth"), maxHull * 0.3f), "Emergency repairs only trigger once per battle");
        infantry.Position = core;
        var hullBefore = Read<float>(battle, "_playerBaseHealth");
        Invoke(battle, "TryAttackBase", infantry);
        Check(Mathf.IsEqualApprox(hullBefore - Read<float>(battle, "_playerBaseHealth"), infantry.BaseDamage * 0.7f),
            "Maximum axle armor reduces enemy base attacks by 30%");
        foreach (var unit in Read<List<Unit>>(battle, "_units").Where(x => x.Team == Team.Enemy)) unit.Position = core + new Vector2(600, 0);
        ally.Position = Core(false) - new Vector2(150, 0);
        Invoke(battle, "TickBaseWeapons", 10f);
        Check(Shots().Length == 0 && Read<Unit>(battle, "_strongholdAim") == ally, "Castle archers warn before firing");
        Invoke(battle, "TickBaseWeapons", 0.8f);
        Check(Shots().Length == 1, "Castle archers fire after their aim window");
        ResolveShots();
        Check(ally.Health < ally.MaxHealth, "Stronghold projectiles damage attacking troops");
        Write(battle, "_enemyBaseHealth", 0f);
        Invoke(battle, "TickBaseWeapons", 10f);
        Invoke(battle, "TickBaseWeapons", 1f);
        Check(Shots().Length == 0, "Breached strongholds stop firing");
        Write(battle, "_wagonVolleyRecovery", 0f);
        Invoke(battle, "TickWagonSkills", 1f);
        Check(Read<float>(battle, "_wagonVolleyRecovery") == 0, "A ready volley is saved when no targets are in range");
        runner.Position = core + new Vector2(200, 0);
        Invoke(battle, "TickBaseWeapons", 10f);
        Check(Shots().Length > 0, "Wagon keeps fighting surviving enemies after the gate falls");
        ResolveShots();
        Write(battle, "_battlePaused", true);
        Invoke(battle, "TickBaseWeapons", 20f);
        Check(Shots().Length == 0, "Paused battles do not fire weapons or recharge skills");
        Write(battle, "_battlePaused", false);
        Write(battle, "_playerBaseHealth", 0f);
        Invoke(battle, "TickBaseWeapons", 20f);
        Check(Shots().Length == 0, "Destroyed wagons stop firing");
        await CloseBattle(battle);

        Check(BaseWeaponCatalog.Stronghold(RouteCatalog.CityId).Kind == BaseWeaponKind.Arrows &&
            BaseWeaponCatalog.Stronghold(RouteCatalog.FoundryId).SplashRadius > 0 &&
            BaseWeaponCatalog.Stronghold(RouteCatalog.ThornwallId).Kind == BaseWeaponKind.Frost &&
            BaseWeaponCatalog.Stronghold(RouteCatalog.CitadelId).Kind == BaseWeaponKind.Ballista,
            "Enemy factions have distinct stronghold weapons");
        var saved = (GameSaveData)Invoke(state, "BuildSaveData");
        saved.Gold = 10000;
        Invoke(state, "ApplySavedData", JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(saved))!);
        var gold = state.Gold;
        var cost = state.GetBaseUpgradeCost(BaseUpgradeCatalog.ArcherCrewId);
        Check(state.TryUpgradeBase(BaseUpgradeCatalog.ArcherCrewId, out _) && state.Gold == gold - cost,
            "Shop purchase charges the displayed price and upgrades the weapon");
        saved = (GameSaveData)Invoke(state, "BuildSaveData");
        Invoke(state, "ApplySavedData", JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(saved))!);
        Check(state.GetBaseUpgradeLevel(BaseUpgradeCatalog.ArcherCrewId) == 1 && state.GetBaseUpgradeLevel(BaseUpgradeCatalog.BallistaId) == 1 &&
            state.GetBaseUpgradeLevel(BaseUpgradeCatalog.EmergencyRepairId) == 3,
            "Weapon and skill purchases survive a save round trip");
        gold = state.Gold;
        Check(!state.TryUpgradeBase(BaseUpgradeCatalog.ReinforcedArmorId, out _) && state.Gold == gold,
            "Maximum-level purchases do not spend gold");
        saved.Gold = 0;
        saved.BaseUpgradeLevels.Clear();
        Invoke(state, "ApplySavedData", saved);
        Check(!state.TryUpgradeBase(BaseUpgradeCatalog.BallistaId, out _) && state.GetBaseUpgradeLevel(BaseUpgradeCatalog.BallistaId) == 0,
            "Insufficient gold cannot install a weapon");
        Check(state.GetBaseUpgradeLevel(BaseUpgradeCatalog.ArcherCrewId) == 0,
            "Older saves with no armaments retain the free starting archers");
        state.PrepareEndlessBattle(RouteCatalog.CityId);
        battle = GD.Load<PackedScene>("res://scenes/Battle.tscn").Instantiate<BattleController>();
        AddChild(battle);
        battle.SetPhysicsProcess(false);
        Check(Read<Label>(battle, "_statusLabel") != null && Read<Label>(battle, "_baseWeaponsIntel") != null,
            "Endless battle initializes its HUD before issuing directives");
        Check(Read<object>(battle, "_strongholdMount") == null, "Endless mode has no phantom stronghold weapon");
        core = Core(true);
        Spawn(Team.Enemy, core + new Vector2(200, 0));
        Invoke(battle, "TickBaseWeapons", 1f);
        Check(Shots().Length == 1, "Starting wagon archers also defend endless runs");
        ResolveShots();
        await CloseBattle(battle);
        Invoke(state, "ApplySavedData", original);
        state.PrepareCampaignBattle();
    }
}
