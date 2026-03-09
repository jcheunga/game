using Godot;

public partial class SceneRouter : Node
{
    public const string MainMenuScene = "res://scenes/MainMenu.tscn";
    public const string MapScene = "res://scenes/MapMenu.tscn";
    public const string ShopScene = "res://scenes/ShopMenu.tscn";
    public const string MultiplayerScene = "res://scenes/MultiplayerMenu.tscn";
    public const string EndlessScene = "res://scenes/EndlessMenu.tscn";
    public const string LoadoutScene = "res://scenes/LoadoutMenu.tscn";
    public const string BattleScene = "res://scenes/Battle.tscn";

    public static SceneRouter Instance { get; private set; }

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

    public void RetryBattle()
    {
        ChangeScene(BattleScene);
    }

    private void ChangeScene(string path)
    {
        GetTree().ChangeSceneToFile(path);
    }
}
