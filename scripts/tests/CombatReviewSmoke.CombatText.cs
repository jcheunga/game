using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class CombatReviewSmoke
{
    // --combat-text [--screenshots] [--window=WxH]: a scripted exchange of hits, crits, heals and callouts on a bright
    // stage, captured at fixed ages into artifacts/combat-text.
    private async Task CheckCombatText()
    {
        var window = OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--window="))?.Split('=')[1].Split('x');
        if (window != null)
        {
            GetWindow().Mode = Window.ModeEnum.Windowed;
            GetWindow().Size = new Vector2I(int.Parse(window[0]), int.Parse(window[1]));
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        foreach (var stage in new[] { 8, 47 })
        {
            var battle = await OpenBattle(stage);
            var camera = Read<Camera2D>(battle, "_battleCamera");
            // A window losing focus pauses the battle, which also freezes the camera.
            if (Read<bool>(battle, "_battlePaused")) Invoke(battle, "TogglePause");
            Invoke(battle, "SetBattleCameraX", 340f);
            camera.ForceUpdateScroll();
            var y = (GameData.Combat.BattlefieldTop + GameData.Combat.BattlefieldBottom) * .5f;
            Unit Place(string id, Team team, float x, float dy = 0)
            {
                var unit = (Unit)Invoke(battle, "SpawnUnit", team, new UnitStats(GameData.GetUnit(id)), new Vector2(x, y + dy));
                unit.SetProcess(false); unit._Process(.5); unit._Process(.01);
                return unit;
            }
            var knight = Place("player_brawler", Team.Player, 250, 6);
            var archer = Place("player_shooter", Team.Player, 205, -10);
            var walker = Place("enemy_walker", Team.Enemy, 330, 4);
            var brute = Place("enemy_brute", Team.Enemy, 395, -8);
            var runner = Place("enemy_walker", Team.Enemy, 455, 10);
            var rng = Read<RandomNumberGenerator>(battle, "_rng");
            var rngState = rng.State;
            void Damage(Unit target, float amount, bool critical = false) => Invoke(battle, "SpawnDamageFeedback", target, amount, critical);
            Damage(walker, 7);
            Damage(walker, 5);
            Damage(brute, 38, true);
            Damage(knight, 9);
            Invoke(battle, "SpawnHealFeedback", archer, 14f);
            Invoke(battle, "SpawnCombatCallout", runner, "Dodge");
            Invoke(battle, "SpawnCombatCallout", knight, "Parry");
            Invoke(battle, "SpawnCourageFeedback", walker.Position + new Vector2(30, -40), 5f);
            Check(rng.State == rngState, "Combat text draws nothing from the seeded battle generator");
            BattleFloatText On(Unit unit) => battle.GetChildren().OfType<BattleFloatText>()
                .Where(x => x.Kind != CombatTextKind.Callout).OrderBy(x => x.Position.DistanceTo(unit.CombatTextPoint)).First();
            var tally = On(walker);
            Check(tally.Text == "12" && battle.GetChildren().OfType<BattleFloatText>().Count(x => x.Position.DistanceTo(walker.CombatTextPoint) < 1) == 1,
                "Quick hits on one enemy add into one tally, shown without a minus sign");
            Check(On(brute).Critical && On(brute).Kind == CombatTextKind.Damage, "A critical hit is its own yellow number");
            Check(On(knight).Kind == CombatTextKind.Damage && !On(knight).Critical && On(archer).Kind == CombatTextKind.Heal && On(archer).Text == "+14",
                "Damage reads red on either side and healing green, whoever struck");
            Check(Mathf.Abs(tally.Position.X - walker.Position.X) < 1 && tally.Position.Y < walker.Position.Y - 10,
                "Numbers start centred over the body, above the feet");
            var texts = battle.GetChildren().OfType<BattleFloatText>().ToList();
            foreach (var text in texts) text.SetProcess(false);
            Check(texts.Count == 7, "Combat text spawns once for each scripted event");
            var age = 0f;
            foreach (var target in new[] { .04f, .1f, .2f, .32f, .44f })
            {
                foreach (var text in texts.Where(IsInstanceValid)) text._Process(target - age);
                age = target;
                await CaptureCombatText($"stage{stage}-{Mathf.RoundToInt(target * 100):000}");
            }
            if (stage == 8) await CheckCombatTextRules(battle, walker, knight);
            await CloseBattle(battle);
        }
    }

    private async Task CheckCombatTextRules(BattleController battle, Unit enemy, Unit ally)
    {
        foreach (var text in battle.GetChildren().OfType<BattleFloatText>()) text.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Invoke(battle, "SpawnDamageFeedback", enemy, 6f, false);
        Invoke(battle, "MarkCriticalHit", enemy);
        var marked = battle.GetChildren().OfType<BattleFloatText>().Single(x => !x.IsQueuedForDeletion());
        Check(marked.Critical, "A critical rolled outside the damage call upgrades the number it just raised");
        Invoke(battle, "SpawnDamageFeedback", enemy, 6f, false);
        Check(battle.GetChildren().OfType<BattleFloatText>().Count(x => !x.IsQueuedForDeletion()) == 2 && marked.Text == "6",
            "Ordinary hits never fold into a critical number");
        GetTree().Paused = true;
        var age = marked.Age;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(marked.Age == age, "Pause holds combat text in place");
        GetTree().Paused = false;
        for (var i = 0; i < 60; i++) Invoke(battle, "SpawnDamageFeedback", i % 2 == 0 ? enemy : ally, 3f, true);
        var live = battle.GetChildren().OfType<BattleFloatText>().Where(x => !x.IsQueuedForDeletion()).ToList();
        foreach (var text in live) text._Process(.2);
        Check(live.Count(x => !x.IsQueuedForDeletion()) <= 36, "A crowded field retires its oldest text instead of stacking more");
        GameState.Instance.SetReducedMotion(true);
        Invoke(battle, "SpawnDamageFeedback", enemy, 40f, true);
        var still = battle.GetChildren().OfType<BattleFloatText>().Last();
        Check(Mathf.IsEqualApprox(still.Scale.X, ((Func<float>)still.PresentationScale)() * 1.2f * StillEmphasis(enemy, 40f)), "Reduced motion drops the settle");
        GameState.Instance.SetReducedMotion(false);
    }

    private static float StillEmphasis(Unit target, float damage) => 1f + .1f * Mathf.Clamp(damage / Mathf.Max(1f, target.MaxHealth) * 3f, 0f, 1f);

    private async Task CaptureCombatText(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--screenshots") || DisplayServer.GetName() == "headless") return;
        var folder = ProjectSettings.GlobalizePath("res://artifacts/combat-text");
        System.IO.Directory.CreateDirectory(folder);
        var battle = GetChildren().OfType<BattleController>().Last();
        Image image;
        do
        {
            // A focus loss can pause the battle between frames; retake the frame without the pause menu.
            if (Read<bool>(battle, "_battlePaused")) Invoke(battle, "TogglePause");
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            image = GetViewport().GetTexture().GetImage();
        } while (Read<bool>(battle, "_battlePaused"));
        var suffix = OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--window="))?.Split('=')[1] ?? "default";
        Check(image.SavePng($"{folder}/{name}-{suffix}.png") == Error.Ok, "Capture combat text: " + name);
    }
}
