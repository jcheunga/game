using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewLiveParity()
    {
        _output = ProjectSettings.GlobalizePath("res://artifacts/live-ui-parity/" +
            (MobilePresentation.Enabled ? "phone" : OS.GetCmdlineUserArgs().Contains("--small-window") ? "small" : "desktop"));
        System.IO.Directory.CreateDirectory(_output);
        var state = GameState.Instance;
        state.ResetProgress(); state.SetShowHints(false); state.SetAnalyticsConsent(false);
        await Open("MainMenu");
        var home = (MapMenu)GetTree().CurrentScene;
        var canvas = Walk(home).OfType<MapPathCanvas>().Single();
        var camera = canvas.MapOffset;
        var food = state.Food; var gold = state.Gold; var knowledge = state.AdventureKnowledgeRevision;
        async Task TapHint(string hint)
        {
            var button = Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Button>()
                .Single(b => b.IsVisibleInTree() && !b.Disabled && (b.TooltipText == hint || b.AccessibilityName == hint));
            await TapModal(button); await LiveUiReview.Settle(this);
        }
        foreach (var (hint, path, tab) in new[] {
            ("Warband", SceneRouter.ShopScene, "Warband"), ("Spells", SceneRouter.ShopScene, "Spells"),
            ("Upgrades", SceneRouter.ShopScene, "War wagon"), ("Codex", SceneRouter.CodexScene, "All"),
            ("Settings", SceneRouter.SettingsScene, "Sound") })
        {
            await TapHint(hint);
            var activity = LiveUiReview.AssertDestination(GetTree(), path);
            Check(Walk(activity).OfType<Button>().Any(b => b.Text == tab && b.ButtonPressed), hint + ": native home click selects its live page");
            AuditText("Live click / " + hint); await Capture("click-" + hint);
            await TapHint("Close panel");
            Check(!home.HasHomeModal && GetTree().CurrentScene == home, hint + ": close restores the same home");
        }
        foreach (var hint in new[] { "Achievements", "More" })
        {
            await TapHint(hint);
            Check(home.HomeModalDestination == (hint == "More" ? "more" : "achievements"), hint + ": native home click opens its live overlay");
            AuditText("Live click / " + hint); await Capture("click-" + hint);
            await TapHint("Close panel");
        }
        await TapHint("More");
        await TapModal(Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Button>().Single(b => b.Text == "Community"));
        await TapHint("Challenges");
        LiveUiReview.AssertDestination(GetTree(), SceneRouter.MultiplayerScene);
        Check(Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Button>().Any(b => b.Text == "Rooms" && b.ButtonPressed),
            "Challenges opens the live multiplayer Rooms page through More / Community");
        AuditText("Live click / Multiplayer"); await Capture("click-Multiplayer");
        await TapModal(Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Button>().Single(b => b.Text == "LAN"));
        LiveUiReview.AssertDestination(GetTree(), SceneRouter.LanRaceScene);
        AuditText("Live click / LAN"); await Capture("click-LAN");
        await TapHint("Back to previous panel");
        Check(home.HomeModalDestination == SceneRouter.MultiplayerScene, "LAN back returns to the multiplayer overlay");
        home.CloseHomeModal();

        foreach (var path in LiveUiReview.ActivityScenes)
        {
            var activity = (Control)await LiveUiReview.Open(this, path);
            await Wait(0.4); // Captures show the settled overlay, not its fade-in.
            var modal = home.GetNode<RealmModal>("HomeModal");
            Check(modal.Content.GetGlobalRect().Grow(2).Encloses(activity.GetGlobalRect()), path + ": live activity fits its modal body");
            AuditText("Live route / " + path); await Capture("route-" + System.IO.Path.GetFileNameWithoutExtension(path));
            await TapHint("Close panel");
            Check(GetTree().CurrentScene == home && !home.HasHomeModal, path + ": closing preserves the map");
        }
        Check(canvas.MapOffset == camera && state.Food == food && state.Gold == gold && state.AdventureKnowledgeRevision == knowledge,
            "Browsing every destination preserves the camera, resources and exploration");
        System.IO.File.WriteAllText(_output + "/text-audit.json", System.Text.Json.JsonSerializer.Serialize(_textAudit,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"LIVE_UI_PARITY_RESULT: {_failures} failures; {LiveUiReview.ActivityScenes.Length} routed activities");
        QuitAfterAudio(_failures == 0 ? 0 : 1);
    }
}
