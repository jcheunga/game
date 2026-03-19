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
    public const string CashShopScene = "res://scenes/CashShopMenu.tscn";
    public const string BattleScene = "res://scenes/Battle.tscn";

    private const float FadeDuration = 0.18f;

    public static SceneRouter Instance { get; private set; }
    public string SettingsReturnLabel => ResolveSceneLabel(_settingsReturnScenePath);

    private string _settingsReturnScenePath = MainMenuScene;
    private CanvasLayer _fadeLayer;
    private ColorRect _fadeRect;
    private Label _tipLabel;
    private bool _transitioning;

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

    public override void _Ready()
    {
        _fadeLayer = new CanvasLayer { Layer = 128 };
        AddChild(_fadeLayer);
        _fadeRect = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0f),
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _fadeLayer.AddChild(_fadeRect);

        _tipLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AnchorLeft = 0.1f,
            AnchorRight = 0.9f,
            AnchorTop = 0.7f,
            AnchorBottom = 0.85f,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1f, 1f, 1f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _tipLabel.AddThemeColorOverride("font_color", new Color("c8c8c8"));
        _fadeLayer.AddChild(_tipLabel);
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

    public void GoToCashShop()
    {
        ChangeScene(CashShopScene);
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

    private async void ChangeScene(string path)
    {
        if (_transitioning)
        {
            return;
        }

        _transitioning = true;
        AudioDirector.Instance?.PlaySceneChange();

        if (_tipLabel != null)
        {
            _tipLabel.Text = LoadingTipCatalog.GetRandom();
        }

        if (_fadeRect != null)
        {
            var fadeOut = CreateTween();
            fadeOut.SetParallel(true);
            fadeOut.TweenProperty(_fadeRect, "color:a", 1f, FadeDuration);
            if (_tipLabel != null)
            {
                fadeOut.TweenProperty(_tipLabel, "modulate:a", 1f, FadeDuration);
            }
            await ToSignal(fadeOut, Tween.SignalName.Finished);
        }

        GetTree().ChangeSceneToFile(path);
        MusicPlayer.Instance?.PlayForScene(path);

        if (_fadeRect != null)
        {
            var fadeIn = CreateTween();
            fadeIn.SetParallel(true);
            fadeIn.TweenProperty(_fadeRect, "color:a", 0f, FadeDuration);
            if (_tipLabel != null)
            {
                fadeIn.TweenProperty(_tipLabel, "modulate:a", 0f, FadeDuration);
            }
            await ToSignal(fadeIn, Tween.SignalName.Finished);
        }

        _transitioning = false;
    }

    private static string ResolveSceneLabel(string path)
    {
        return path switch
        {
            MapScene => "Campaign Map",
            ShopScene => "Caravan Armory",
            CashShopScene => "Royal Storehouse",
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
