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
    public const string ForgeScene = "res://scenes/ForgeMenu.tscn";
    public const string ExpeditionScene = "res://scenes/ExpeditionMenu.tscn";
    public const string EventScene = "res://scenes/EventMenu.tscn";
    public const string CodexScene = "res://scenes/CodexMenu.tscn";
    public const string SkillTreeScene = "res://scenes/SkillTreeMenu.tscn";
    public const string ArenaScene = "res://scenes/ArenaMenu.tscn";
    public const string GuildScene = "res://scenes/GuildMenu.tscn";
    public const string ProfileScene = "res://scenes/ProfileMenu.tscn";
    public const string RaidScene = "res://scenes/RaidMenu.tscn";
    public const string BountyScene = "res://scenes/BountyMenu.tscn";
    public const string TowerScene = "res://scenes/TowerMenu.tscn";
    public const string FriendsScene = "res://scenes/FriendsMenu.tscn";
    public const string LoginCalendarScene = "res://scenes/LoginCalendarMenu.tscn";
    public const string LeaderboardScene = "res://scenes/LeaderboardMenu.tscn";
    public const string SeasonPassScene = "res://scenes/SeasonPassMenu.tscn";
    public const string BattleSummaryScene = "res://scenes/BattleSummaryMenu.tscn";
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

    public void GoToForge()
    {
        ChangeScene(ForgeScene);
    }

    public void GoToExpeditions()
    {
        ChangeScene(ExpeditionScene);
    }

    public void GoToEvent()
    {
        ChangeScene(EventScene);
    }

    public void GoToCodex()
    {
        ChangeScene(CodexScene);
    }

    public void GoToSkillTree()
    {
        ChangeScene(SkillTreeScene);
    }

    public void GoToArena()
    {
        ChangeScene(ArenaScene);
    }

    public void GoToGuild()
    {
        ChangeScene(GuildScene);
    }

    public void GoToProfile()
    {
        ChangeScene(ProfileScene);
    }

    public void GoToRaid()
    {
        ChangeScene(RaidScene);
    }

    public void GoToBounty()
    {
        ChangeScene(BountyScene);
    }

    public void GoToTower()
    {
        ChangeScene(TowerScene);
    }

    public void GoToFriends()
    {
        ChangeScene(FriendsScene);
    }

    public void GoToLoginCalendar()
    {
        ChangeScene(LoginCalendarScene);
    }

    public void GoToLeaderboard()
    {
        ChangeScene(LeaderboardScene);
    }

    public void GoToSeasonPass()
    {
        ChangeScene(SeasonPassScene);
    }

    public void GoToBattleSummary()
    {
        ChangeScene(BattleSummaryScene);
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
            ForgeScene => "Relic Forge",
            ExpeditionScene => "Expeditions",
            EventScene => "Seasonal Event",
            CodexScene => "Codex",
            SkillTreeScene => "Skill Trees",
            ArenaScene => "PvP Arena",
            GuildScene => "Warband",
            ProfileScene => "Player Profile",
            RaidScene => "Weekly Raid",
            BountyScene => "Bounty Board",
            TowerScene => "Challenge Tower",
            FriendsScene => "Friends",
            LoginCalendarScene => "Login Calendar",
            LeaderboardScene => "Leaderboards",
            SeasonPassScene => "Season Pass",
            BattleSummaryScene => "Battle Summary",
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
