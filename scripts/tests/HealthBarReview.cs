using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class HealthBarReview : Node2D
{
    private int _failures;
    private bool _gallery;
    private const BindingFlags PrivateMembers = BindingFlags.Instance | BindingFlags.NonPublic;
    public override void _Ready() => Callable.From(Run).CallDeferred();

    private void Check(bool condition, string label)
    {
        GD.Print($"HEALTH_BAR_CHECK: {(condition ? "PASS" : "FAIL")} {label}");
        if (!condition) _failures++;
    }

    private async void Run()
    {
        try
        {
            if (!OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--save-suffix=healthbar-review-")))
                throw new InvalidOperationException("Use a unique --save-suffix=healthbar-review-... to protect player saves.");
            GameState.Instance.SetAnalyticsConsent(false);
            GameState.Instance.SetShowHints(false);
            var motion = new HealthBarMotion();
            motion.Update(0.6f, 0.016f);
            Check(Mathf.IsEqualApprox(motion.TrailRatio, 1), "Damage chip briefly holds the previous health");
            motion.Update(0.6f, 0.6f);
            Check(motion.TrailRatio > 0.6f && motion.TrailRatio < 1f, "Damage chip drains smoothly");
            motion.Update(0.6f, 1f);
            Check(Mathf.IsEqualApprox(motion.TrailRatio, 0.6f), "Damage chip settles at actual health");
            motion.Update(0.9f, 0.016f);
            Check(motion.TrailRatio >= 0.9f, "Healing cannot leave the chip behind the fill");
            motion.Update(0.3f, 0.016f); motion.Update(0.1f, 0.016f);
            Check(motion.TrailRatio >= 0.9f, "Successive hits preserve the recent damage amount");
            motion.Update(0.1f, 0.016f, true);
            Check(Mathf.IsEqualApprox(motion.TrailRatio, 0.1f), "Reduced-motion mode removes chip animation");
            motion.Update(4f, 2f); Check(motion.TrailRatio == 1f, "Overheal is clamped");
            motion.Update(-2f, 2f); Check(motion.TrailRatio == 0f, "Empty health has no lingering fill");
            motion.Reset(); Check(motion.TrailRatio == 1f, "Pooled health presentation resets");
            foreach (var width in new[] { 10f, 34f, 62f, 132f })
            {
                var bounds = new Rect2(0, 0, width, 12);
                var well = HealthBarPainter.Well(bounds);
                Check(well.Size.X > 0 && bounds.Encloses(well), $"Fill stays inside the {width}px frame");
            }
            foreach (var name in new[] { "health_frame", "health_fill" })
                Check(GD.Load<Texture2D>($"res://assets/ui/bars/{name}.svg") != null, name + " imports");
            if (OS.GetCmdlineUserArgs().Contains("--screenshots"))
            {
                await CaptureBattle(false);
                await CaptureBattle(true);
                _gallery = true; QueueRedraw();
                await Capture("health-bars-detail");
            }
        }
        catch (Exception ex) { GD.PrintErr(ex.ToString()); _failures++; }
        await LiveUiReview.StopAudio(this);
        GD.Print($"HEALTH_BAR_RESULT: {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private async Task CaptureBattle(bool contrast)
    {
        GameState.Instance.SetHighContrast(contrast);
        GameState.Instance.SetSelectedStage(1);
        GameState.Instance.PrepareCampaignBattle();
        var battle = GD.Load<PackedScene>("res://scenes/Battle.tscn").Instantiate<BattleController>();
        AddChild(battle); battle.SetPhysicsProcess(false);
        typeof(BattleController).GetField("_playerBaseHealth", PrivateMembers)!.SetValue(battle, 174f);
        typeof(BattleController).GetField("_enemyBaseHealth", PrivateMembers)!.SetValue(battle, 106f);
        typeof(BattleController).GetMethod("UpdateHud", PrivateMembers)!.Invoke(battle, null);
        var ids = new[] { "player_brawler", "player_shooter", "player_defender", "player_raider", "enemy_walker", "enemy_brute", "enemy_lich", "enemy_boss" };
        var ratios = new[] { 1f, 0.73f, 0.39f, 0.12f, 0.85f, 0.48f, 0.17f, 0.63f };
        for (var i = 0; i < ids.Length; i++)
        {
            var unit = new Unit();
            unit.Setup(i < 4 ? Team.Player : Team.Enemy, new UnitStats(GameData.GetUnit(ids[i])),
                new Vector2(270 + (i % 4) * 95 + (i < 4 ? 0 : 330), i < 4 ? 365 + i % 2 * 58 : 455 - i % 2 * 72));
            battle.AddChild(unit); unit.ZIndex = 5;
            if (ratios[i] < 1) unit.TakeDamage(unit.MaxHealth * (1 - ratios[i]) / unit.DamageTakenScale);
        }
        await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
        await Capture(contrast ? "health-bars-high-contrast" : "health-bars-in-battle");
        battle.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Capture(string name)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var folder = ProjectSettings.GlobalizePath("res://artifacts/blender");
        Directory.CreateDirectory(folder);
        Check(GetViewport().GetTexture().GetImage().SavePng($"{folder}/{name}.png") == Error.Ok, "Captured " + name);
    }

    public override void _Draw()
    {
        if (!_gallery) return;
        DrawRect(new Rect2(0, 0, 1280, 720), new Color("142321"));
        var font = ThemeDB.FallbackFont;
        DrawString(font, new Vector2(64, 73), "CROWNROAD / VITALITY", fontSize: 28, modulate: new Color("dfc88d"));
        DrawString(font, new Vector2(64, 103), "Forged frames  ·  brushed enamel  ·  delayed damage chips", fontSize: 16, modulate: new Color("9aada6"));
        var labels = new[] { "ALLIED", "HOSTILE", "COMMANDER", "CARAVAN / GATE", "ACCESSIBILITY" };
        var health = new[] { 1f, 0.72f, 0.36f, 0.1f };
        for (var column = 0; column < 4; column++)
            DrawString(font, new Vector2(320 + column * 218, 160), new[] { "FULL", "WOUNDED", "RECENT HIT", "CRITICAL" }[column], fontSize: 14, modulate: new Color("b4c4bb"));
        for (var row = 0; row < 5; row++)
        {
            var y = 215 + row * 94;
            DrawLine(new Vector2(64, y + 58), new Vector2(1215, y + 58), new Color("30413a"));
            DrawString(font, new Vector2(64, y + 24), labels[row], fontSize: 16, modulate: new Color("dfc88d"));
            for (var column = 0; column < 4; column++)
            {
                var kind = row == 2 ? HealthBarKind.Boss : row == 3 ? HealthBarKind.Base : HealthBarKind.Unit;
                HealthBarPainter.Draw(this, new Rect2(300 + column * 218, y, 175, row == 2 ? 30 : 26),
                    health[column], column == 2 ? 0.68f : health[column], row is 0 or 3 or 4, kind, row == 4);
            }
        }
    }
}
