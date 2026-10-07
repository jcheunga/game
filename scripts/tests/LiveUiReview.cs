using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

/// <summary>Presentation reviews enter screens through the same router as the player.</summary>
public static class LiveUiReview
{
    public static string[] ActivityScenes => typeof(SceneRouter).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string) && field.Name.EndsWith("Scene")
            && field.Name is not "MainMenuScene" and not "MapScene" and not "BattleScene")
        .Select(field => (string)field.GetRawConstantValue()).ToArray();

    public static Node ActiveRoot(SceneTree tree) => tree.CurrentScene is MapMenu { HasHomeModal: true } home
        ? home.GetNode<RealmModal>("HomeModal") : tree.CurrentScene;

    public static void PreserveDriver(Node driver)
    {
        if (driver.GetTree().CurrentScene == driver) driver.GetTree().CurrentScene = null;
    }

    public static async Task<Node> Open(Node driver, string scene)
    {
        PreserveDriver(driver);
        var tree = driver.GetTree();
        var path = scene.StartsWith("res://") ? scene : $"res://scenes/{scene}.tscn";
        await Settle(driver);
        if (tree.CurrentScene is MapMenu home) home.CloseHomeModal();
        var router = SceneRouter.Instance;
        switch (path)
        {
            case SceneRouter.MainMenuScene: case SceneRouter.MapScene: router.ReloadHome(); break;
            case SceneRouter.BattleScene: router.GoToBattle(); break;
            case SceneRouter.ShopScene: router.GoToShop(); break;
            case SceneRouter.MultiplayerScene: router.GoToMultiplayer(); break;
            case SceneRouter.LanRaceScene: router.GoToLanRace(); break;
            case SceneRouter.EndlessScene: router.GoToEndless(); break;
            case SceneRouter.LoadoutScene: router.GoToLoadout(); break;
            case SceneRouter.SettingsScene: router.GoToSettings(); break;
            case SceneRouter.CashShopScene: router.GoToCashShop(); break;
            case SceneRouter.ForgeScene: router.GoToForge(); break;
            case SceneRouter.ExpeditionScene: router.GoToExpeditions(); break;
            case SceneRouter.EventScene: router.GoToEvent(); break;
            case SceneRouter.CodexScene: router.GoToCodex(); break;
            case SceneRouter.ArenaScene: router.GoToArena(); break;
            case SceneRouter.GuildScene: router.GoToGuild(); break;
            case SceneRouter.ProfileScene: router.GoToProfile(); break;
            case SceneRouter.RaidScene: router.GoToRaid(); break;
            case SceneRouter.BountyScene: router.GoToBounty(); break;
            case SceneRouter.TowerScene: router.GoToTower(); break;
            case SceneRouter.FriendsScene: router.GoToFriends(); break;
            case SceneRouter.LoginCalendarScene: router.GoToLoginCalendar(); break;
            case SceneRouter.LeaderboardScene: router.GoToLeaderboard(); break;
            case SceneRouter.SeasonPassScene: router.GoToSeasonPass(); break;
            default: throw new InvalidOperationException($"No live review route for {path}");
        }
        await Settle(driver);
        if (path is SceneRouter.MainMenuScene or SceneRouter.MapScene)
        {
            if (tree.CurrentScene is not MainMenu || ((MapMenu)tree.CurrentScene).HasHomeModal)
                throw new InvalidOperationException("Review did not reach the live home map.");
            return tree.CurrentScene;
        }
        if (path == SceneRouter.BattleScene)
        {
            if (tree.CurrentScene is not BattleController) throw new InvalidOperationException("Review did not reach battle.");
            return tree.CurrentScene;
        }
        return AssertDestination(tree, path);
    }

    public static Control AssertDestination(SceneTree tree, string path)
    {
        if (tree.CurrentScene is not MapMenu { HasHomeModal: true } home || home.HomeModalDestination != path)
            throw new InvalidOperationException($"Review bypassed the live map overlay for {path}.");
        var content = home.GetNode<RealmModal>("HomeModal").ActivePage;
        if (!content.HasMeta("home_modal") || content.SceneFilePath != path)
            throw new InvalidOperationException($"Review is showing the wrong activity for {path}.");
        GD.Print($"LIVE_UI_ROUTE: {path} above {home.SceneFilePath}");
        return content;
    }

    public static async Task Settle(Node driver)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (SceneRouter.Instance.IsTransitioning)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Live scene transition did not finish.");
            await driver.ToSignal(driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        for (var frame = 0; frame < 4; frame++)
            await driver.ToSignal(driver.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    public static async Task StopAudio(Node driver)
    {
        // Rapid headless shutdown can leave audio-thread playback references alive.
        // Finish component reviews after their active streams have stopped.
        GameState.Instance.SetAudioMuted(true);
        MusicPlayer.Instance?.StopAll();
        foreach (var player in driver.GetTree().Root.FindChildren("*", "AudioStreamPlayer", true, false).OfType<AudioStreamPlayer>())
        {
            player.Stop(); player.Stream = null;
        }
        foreach (var player in driver.GetTree().Root.FindChildren("*", "AudioStreamPlayer2D", true, false).OfType<AudioStreamPlayer2D>())
        {
            player.Stop(); player.Stream = null;
        }
        await Task.Delay(100);
    }
}
