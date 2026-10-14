using Godot;
using System;

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

    public const string BattleScene = "res://scenes/Battle.tscn";
    public const string ProgressionScene = "res://scenes/ProgressionMenu.tscn";
    public const string BossRushScene = "res://scenes/BossRushMenu.tscn";
    public void GoToProgression()=>ChangeScene(ProgressionScene);
    public void GoToBossRush()=>ChangeScene(BossRushScene);

    private const float FadeDuration = 0.18f;

    public static SceneRouter Instance { get; private set; }
    public string SettingsReturnLabel => ResolveSceneLabel(_settingsReturnScenePath);
    public int InitialShopTab { get; private set; }
    public bool IsTransitioning => _transitioning;

    private string _settingsReturnScenePath = MainMenuScene;
    private CanvasLayer _fadeLayer;
    private ColorRect _fadeRect;
    private LoadingScreen _loadingScreen;
    private bool _transitioning;

    private static bool IsHomeActivity(string path) => path is ShopScene or MultiplayerScene or LanRaceScene
        or EndlessScene or LoadoutScene or SettingsScene or CashShopScene or ForgeScene or ExpeditionScene
        or EventScene or CodexScene or ArenaScene or GuildScene or ProfileScene or RaidScene
        or BountyScene or TowerScene or FriendsScene or LoginCalendarScene or LeaderboardScene or SeasonPassScene or ProgressionScene or BossRushScene;

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
        ProcessMode = ProcessModeEnum.Always;
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

        _loadingScreen = new LoadingScreen { Visible = false, Modulate = new Color(1, 1, 1, 0), ProcessMode = ProcessModeEnum.Always };
        _fadeLayer.AddChild(_loadingScreen);

    }

    public void GoToMainMenu()
    {
        ChangeScene(MainMenuScene);
    }

    public void GoToMap()
    {
        ChangeScene(MapScene);
    }

    public void GoToShop(int tab = 0)
    {
        InitialShopTab = Mathf.Clamp(tab, 0, 3);
        ChangeScene(ShopScene);
    }

    public void SetInitialShopTab(int tab) => InitialShopTab = Mathf.Clamp(tab, 0, 3);

    public int ConsumeInitialShopTab()
    {
        var tab = InitialShopTab;
        InitialShopTab = 0;
        return tab;
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
        if (GetTree().CurrentScene is BattleController battle) { battle.CloseBattleSettings(); return; }
        if (GetTree().CurrentScene is MapMenu home && home.HasHomeModal) { home.BackHomeModal(); return; }
        ChangeScene(_settingsReturnScenePath);
    }

    public void RetryBattle()
    {
        ChangeScene(BattleScene);
    }

    public void ReloadHome() => ChangeScene(MainMenuScene, false);

    private async void ChangeScene(string path, bool allowOverlay = true)
    {
        if (_transitioning) return;
        if (allowOverlay && GetTree().CurrentScene is MapMenu home && home.OpenHomeDestination(path)) return;
        _transitioning = true;
        try
        {
            AudioDirector.Instance?.PlaySceneChange();
            var illustrated = path == BattleScene || GetTree().CurrentScene is BattleController;
            var started = Time.GetTicksMsec();
            if (illustrated)
            {
                var stageNumber = Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameData.MaxStage);
                if (path == BattleScene && GameState.Instance.CurrentBattleMode == BattleRunMode.BossRush) stageNumber = 10;
                if (path == BattleScene && GameState.Instance.CurrentBattleMode == BattleRunMode.Tower)
                    stageNumber = ChallengeTowerCatalog.GetFloor(GameState.Instance.SelectedTowerFloor)?.BaseStageNumber ?? 1;
                _loadingScreen.Prepare(GameData.GetStage(stageNumber), path == BattleScene);
                _loadingScreen.Show();
            }
            var fadeOut = CreateTween().SetParallel(true);
            fadeOut.TweenProperty(_fadeRect, "color:a", 1f, FadeDuration);
            if (illustrated) fadeOut.TweenProperty(_loadingScreen, "modulate:a", 1f, FadeDuration);
            await ToSignal(fadeOut, Tween.SignalName.Finished);

            var homeActivity = allowOverlay && IsHomeActivity(path);
            var scenePath = homeActivity ? MainMenuScene : path;
            var error = GetTree().ChangeSceneToFile(scenePath);
            if (error != Error.Ok) throw new InvalidOperationException($"Could not open {scenePath}: {error}");
            await ToSignal(GetTree(), SceneTree.SignalName.SceneChanged);
            if (homeActivity && GetTree().CurrentScene is MapMenu destinationHome) destinationHome.OpenHomeDestination(path);
            MusicPlayer.Instance?.PlayForScene(scenePath);
            // Battle starts only once this overlay clears; quick menu navigation has no holding time.
            var remaining = 0.65 - (Time.GetTicksMsec() - started) / 1000.0;
            if (path == BattleScene && remaining > 0)
                await ToSignal(GetTree().CreateTimer(remaining, true, false, true), SceneTreeTimer.SignalName.Timeout);
            var fadeIn = CreateTween().SetParallel(true);
            fadeIn.TweenProperty(_fadeRect, "color:a", 0f, FadeDuration);
            if (illustrated) fadeIn.TweenProperty(_loadingScreen, "modulate:a", 0f, FadeDuration);
            await ToSignal(fadeIn, Tween.SignalName.Finished);
        }
        catch (Exception exception)
        {
            GD.PushError(exception.Message);
        }
        finally
        {
            if (IsInstanceValid(_fadeRect)) _fadeRect.Color = new Color(0, 0, 0, 0);
            if (IsInstanceValid(_loadingScreen)) { _loadingScreen.Modulate = new Color(1, 1, 1, 0); _loadingScreen.Hide(); }
            _transitioning = false;
        }
    }

    private static string ResolveSceneLabel(string path)
    {
        return path switch
        {
            MainMenuScene => "Home",
            MapScene => "Campaign Map",
            ShopScene => "Caravan Armory",
            CashShopScene => "Royal Storehouse",
            ForgeScene => "Relic Forge",
            ExpeditionScene => "Expeditions",
            EventScene => "Seasonal Event",
            CodexScene => "Codex",
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
            ProgressionScene => "Caravan progression",
            BossRushScene => "Boss rush",

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
