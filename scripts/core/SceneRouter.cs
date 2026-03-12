using Godot;

public partial class SceneRouter : Node
{
    public const string MainMenuScene = "res://scenes/MainMenu.tscn";
    public const string MapScene = "res://scenes/MapMenu.tscn";
    public const string ShopScene = "res://scenes/ShopMenu.tscn";
    public const string MultiplayerScene = "res://scenes/MultiplayerMenu.tscn";
    public const string LanRaceScene = "res://scenes/LanRaceMenu.tscn";
    public const string EndlessScene = "res://scenes/EndlessMenu.tscn";
    public const string LoadoutScene = "res://scenes/LoadoutMenu.tscn";
    public const string SettingsScene = "res://scenes/SettingsMenu.tscn";
    public const string BattleScene = "res://scenes/Battle.tscn";

    public static SceneRouter Instance { get; private set; }
    public string SettingsReturnLabel => ResolveSceneLabel(_settingsReturnScenePath);

    private string _settingsReturnScenePath = MainMenuScene;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void GoToMainMenu()
    {
        ChangeScene(MainMenuScene);
    }

    public void GoToMap()
    {
        ChangeScene(MapScene);
    }

    public void GoToShop()
    {
        ChangeScene(ShopScene);
    }

    public void GoToMultiplayer()
    {
        ChangeScene(MultiplayerScene);
    }

    public void GoToLanRace()
    {
        ChangeScene(LanRaceScene);
    }

    public void GoToEndless()
    {
        ChangeScene(EndlessScene);
    }

    public void GoToBattle()
    {
        ChangeScene(BattleScene);
    }

    public void GoToLoadout()
    {
        ChangeScene(LoadoutScene);
    }

    public void GoToSettings()
    {
        var currentScenePath = GetTree().CurrentScene?.SceneFilePath;
        if (!string.IsNullOrWhiteSpace(currentScenePath) &&
            !currentScenePath.Equals(SettingsScene))
        {
            _settingsReturnScenePath = currentScenePath;
        }

        ChangeScene(SettingsScene);
    }

    public void ReturnFromSettings()
    {
        ChangeScene(_settingsReturnScenePath);
    }

    public void RetryBattle()
    {
        ChangeScene(BattleScene);
    }

    private void ChangeScene(string path)
    {
        AudioDirector.Instance?.PlaySceneChange();
        GetTree().ChangeSceneToFile(path);
    }

    private static string ResolveSceneLabel(string path)
    {
        return path switch
        {
            MapScene => "Campaign Map",
            ShopScene => "Convoy Shop",
            MultiplayerScene => "Multiplayer Challenge",
            LanRaceScene => "LAN Race",
            EndlessScene => "Endless Prep",
            LoadoutScene => "Stage Briefing",
            BattleScene => "Battle",
            SettingsScene => "Settings",
            _ => "Title"
        };
    }
}
