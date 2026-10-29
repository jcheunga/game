using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class CombatReviewSmoke
{
    private static IEnumerable<Node> Walk(Node node) => node.GetChildren().SelectMany(child => Walk(child).Prepend(child));

    // The bases alive: the painted caravan's layers, crew, team, damage, weather and fall; each zone's stronghold with
    // its barricade, sentries, scars, fires, gate light and banner; and the collapse that ends a won battle.
    // --screenshots writes artifacts/base-life/*.png (run with a window, e.g. --resolution 1920x1080).
    private async Task CheckBaseLife()
    {
        var combat = GameData.Combat;
        var shots = OS.GetCmdlineUserArgs().Contains("--screenshots") && DisplayServer.GetName() != "headless";
        var folder = ProjectSettings.GlobalizePath("res://artifacts/base-life");
        System.IO.Directory.CreateDirectory(folder);
        async Task Frames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        async Task Seconds(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        async Task Shot(string name)
        {
            if (!shots) return;
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng($"{folder}/{name}.png");
        }
        void Simulate(BattleController battle, float seconds) { for (var t = 0f; t < seconds; t += 1f / 60) battle._PhysicsProcess(1.0 / 60); }
        void Camera(BattleController battle, float x) { Invoke(battle, "SetBattleCameraX", x); Read<Camera2D>(battle, "_battleCamera").ForceUpdateScroll(); }
        Unit Spawn(BattleController battle, string id, float x, float y, Team team) =>
            (Unit)Invoke(battle, "SpawnUnit", team, new UnitStats(GameData.GetUnit(id), healthScale: 20), new Vector2(x, y));

        // The caravan, fully crewed and armoured, in the king's road.
        var state = GameState.Instance;
        var levels = Read<Dictionary<string, int>>(state, "_baseUpgradeLevels");
        var saved = levels.ToDictionary(p => p.Key, p => p.Value);
        levels[BaseUpgradeCatalog.BallistaId] = 1; levels[BaseUpgradeCatalog.FirepotId] = 1; levels[BaseUpgradeCatalog.ReinforcedArmorId] = 3;
        var battle = await OpenBattle(1);
        var names = Walk(battle).OfType<BattleBaseCanvas>().Select(c => c.Name.ToString()).ToHashSet();
        Check(new[] { "Caravan", "CaravanDetail", "CaravanGlow", "CaravanHud" }.All(names.Contains),
            "The painted caravan draws as one object: plate, crew and door, lamp glow and health");
        Check(Read<object>(battle, "_wagonArcher") != null && Read<Texture2D>(battle, "_wagonArmour") != null,
            "An archer crews the arrow mount and the armour plating is loaded");
        var art = (BattleStructureArt)Invoke(battle, "get_WagonArt");
        Check(art.Damaged != null && art.Lantern != null && art.Hitch != null && art.Flag.Size.X > 0,
            "The caravan's plate records its lantern, hitch and flag, and has a battered twin");
        Write(battle, "_mana", 100f);
        Spawn(battle, "enemy_walker", combat.PlayerBaseX + 120, 336, Team.Enemy);
        Camera(battle, 0);
        Invoke(battle, "TickBaseWeapons", 5f);
        await Frames(4); await Seconds(.25);
        await Shot("wagon-crewed");

        // Battered: below half health it shows its damage, rocks when struck and keeps the bolts that hit it.
        Write(battle, "_playerBaseHealth", Read<float>(battle, "_playerBaseMaxHealth") * .4f);
        var ballista = Spawn(battle, "enemy_boneballista", combat.PlayerBaseX + 150, 340, Team.Enemy);
        for (var i = 0; i < 3; i++) Invoke(battle, "OnWagonStruck", 12f, ballista, ballista.CombatLifetime);
        Check(Read<System.Collections.IList>(battle, "_wagonBolts").Count == 3 && Read<float>(battle, "_wagonSwaySpeed") < 0f,
            "Siege bolts stay stuck in the hull and the wagon rocks back from the blow");
        // (An unfocused review window pauses the battle, which holds its presentation too.)
        Write(battle, "_battlePaused", false);
        await Seconds(.7);
        Check(Read<float>(battle, "_wagonDamagedMix") > .99f, "Below half health the caravan wears its battle damage");
        await Shot("wagon-damaged");

        // Defeat: it tips forward on a broken wheel and lies there.
        Write(battle, "_playerBaseHealth", 0f);
        Invoke(battle, "EndBattle", false);
        await Seconds(.8);
        Check(Read<float>(battle, "_wagonFallenAt") >= 0f, "A destroyed caravan falls over");
        await Shot("wagon-fallen");
        await CloseBattle(battle);
        foreach (var (key, value) in saved) levels[key] = value;
        foreach (var key in levels.Keys.Except(saved.Keys).ToArray()) levels.Remove(key);

        // Weather: snow on the roof in the mountain pass, mud on the wheels in the marsh.
        foreach (var (stage, zone) in new[] { (41, "thornwall"), (61, "mire") })
        {
            battle = await OpenBattle(stage);
            Check(Read<Texture2D>(battle, "_wagonWeather") != null, $"In {zone} the caravan carries the zone's weather");
            Camera(battle, 0);
            await Frames(4);
            await Shot($"wagon-{zone}");
            await CloseBattle(battle);
        }

        // Each zone's stronghold, a third down, a wave being called and troops at its walls.
        foreach (var stage in new[] { 1, 15, 30, 36, 41, 55, 61, 75, 90, 100 })
        {
            battle = await OpenBattle(stage);
            var zone = GameData.GetStage(stage).MapId;
            var painted = (EnemyBaseArt)Invoke(battle, "get_PaintedBase");
            var pieces = Walk(battle).OfType<BattleBaseCanvas>().Count(c => c.Name.ToString().StartsWith("Barricade"));
            Check(pieces == 4, $"Stage {stage} ({zone}): the zone's barricade closes the road in front of the stronghold");
            Check(Read<System.Collections.IList>(battle, "_sentries").Count == Mathf.Min(2, painted.Posts.Length),
                $"Stage {stage}: a sentry keeps watch on each wall it can stand behind ({painted.Posts.Length} found)");
            var max = Read<float>(battle, "_enemyBaseMaxHealth");
            for (var hit = 0; hit < 7; hit++) Invoke(battle, "ApplyUnitBaseHit", Team.Player, max * .1f, Colors.White, null!, 0u);
            await Frames(2);
            var scars = Read<System.Collections.IList>(battle, "_baseScars").Count;
            Check(Read<int>(battle, "_crackStage") == 2 && scars >= Mathf.Min(painted.Wall.Length, 6),
                $"Stage {stage}: at 30% its walls are cracked and chipped ({scars} marks) and its roofs burn");
            foreach (var (id, x, y) in new[] { ("player_brawler", -40f, 330f), ("player_defender", -36f, 352f), ("player_spear", -60f, 342f),
                ("enemy_walker", -12f, 340f) })
                Spawn(battle, id, combat.EnemyBaseX + x, y, id.StartsWith("player") ? Team.Player : Team.Enemy);
            Write(battle, "_waveWarnAt", Read<float>(battle, "_elapsed") - 1.6f);
            Write(battle, "_enemyBaseFlashTimer", 0f); // the test's hits would leave it flushed red
            Camera(battle, combat.BattlefieldRight + combat.BattlefieldLeft);
            // The banner's fresh-damage trail eases down over about a second.
            await Frames(4); await Seconds(1.3);
            await Shot($"stronghold-{stage:000}-{zone}");
            await CloseBattle(battle);
        }

        // Victory: the stronghold shakes and sinks into its dust, the caravan's standard is raised, then the board.
        battle = await OpenBattle(1);
        Camera(battle, combat.BattlefieldRight + combat.BattlefieldLeft);
        Spawn(battle, "player_brawler", combat.EnemyBaseX - 40, 336, Team.Player);
        Write(battle, "_enemyBaseHealth", 0f);
        Invoke(battle, "EndBattle", true);
        Check((bool)Invoke(battle, "get_StrongholdFallen"), "Destroying the stronghold brings it down");
        foreach (var (at, name) in new[] { (.25, "collapse-1"), (.6, "collapse-2"), (.55, "collapse-3"), (.6, "collapse-4") })
        {
            await Seconds(at);
            await Shot(name);
        }
        Check(Walk(battle).OfType<BattleBaseCanvas>().Any(c => c.Name == "VictoryFlag"), "The caravan's standard rises where the gate stood");
        Check(!Walk(battle).OfType<RoyalResult>().Any(r => r.IsVisibleInTree()), "The result board waits while the stronghold falls");
        await Seconds(.6);
        Check(Walk(battle).OfType<RoyalResult>().Any(r => r.IsVisibleInTree()), "Then the result board comes up");
        await CloseBattle(battle);
    }
}
