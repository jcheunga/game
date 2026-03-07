using Godot;

public partial class SceneRouter : Node
{
    public const string MainMenuScene = "res://scenes/MainMenu.tscn";
    public const string MapScene = "res://scenes/MapMenu.tscn";
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

    public void GoToBattle()
    {
        ChangeScene(BattleScene);
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
