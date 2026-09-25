using System;
using System.Linq;
using Godot;

public partial class MainMenu : Control
{
    private VBoxContainer _destinations;
    private Label _notice;

    public override void _Ready()
    {
        BuildUi();
        TryShowConsentPrompt();
        TryHandleDeepLink();
    }

    private void TryHandleDeepLink()
    {
        if (DeepLinkHandler.Instance == null || !DeepLinkHandler.Instance.HasPendingChallenge())
            return;

        var code = DeepLinkHandler.Instance.ConsumePendingChallenge();
        if (string.IsNullOrWhiteSpace(code)) return;

        GameState.Instance?.TrySetSelectedAsyncChallengeCode(code, out _);
        SceneRouter.Instance?.GoToMultiplayer();
    }

    private void TryShowConsentPrompt()
    {
        if (GameState.Instance == null || GameState.Instance.HasShownConsentPrompt)
            return;

        var overlay = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.7f)
        };
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(520f, 280f)
        };
        center.AddChild(panel);

        var padding = new MarginContainer();
        padding.AddThemeConstantOverride("margin_left", 24);
        padding.AddThemeConstantOverride("margin_right", 24);
        padding.AddThemeConstantOverride("margin_top", 24);
        padding.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(padding);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 14);
        padding.AddChild(stack);

        stack.AddChild(new Label
        {
            Text = "Privacy & Analytics",
            HorizontalAlignment = HorizontalAlignment.Center
        });

        stack.AddChild(new Label
        {
            Text = "Crownroad can collect anonymous gameplay data to help improve balance, difficulty, and game quality.\n\nNo personal information is collected. You can change this at any time in Settings.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var buttonRow = new HBoxContainer();
        buttonRow.AddThemeConstantOverride("separation", 16);
        stack.AddChild(buttonRow);

        var acceptButton = new RealmButton
        {
            Text = "Allow Analytics",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 48f)
        };
        acceptButton.Pressed += () =>
        {
            GameState.Instance.SetAnalyticsConsent(true);
            overlay.QueueFree();
            center.QueueFree();
        };
        buttonRow.AddChild(acceptButton);

        var declineButton = new RealmButton
        {
            Text = "No Thanks",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 48f)
        };
        declineButton.Pressed += () =>
        {
            GameState.Instance.SetAnalyticsConsent(false);
            overlay.QueueFree();
            center.QueueFree();
        };
        buttonRow.AddChild(declineButton);
    }

    private void BuildUi()
    {
        MenuBackdropComposer.AddSolidBackdrop(this, "main_menu", new Color("101d26"));
        var top = new HBoxContainer { Position = new Vector2(40, 26), Size = new Vector2(1200, 44) };
        AddChild(top);
        top.AddChild(RealmUi.IconButton("crown", "Player profile", () => SceneRouter.Instance.GoToProfile()));
        top.AddChild(RealmUi.Label("THE LANTERN CARAVAN", 13, true));
        top.AddChild(UiBadgeFactory.CreateRewardMetric("gold", "", GameState.Instance.Gold.ToString("N0"), new Vector2(24,24)));
        top.AddChild(UiBadgeFactory.CreateRewardMetric("food", "", GameState.Instance.Food.ToString("N0"), new Vector2(24,24)));
        top.AddChild(RealmUi.IconButton("gear", "Settings", () => SceneRouter.Instance.GoToSettings()));
        top.AddChild(RealmUi.IconButton("close", "Quit game", () => MedievalUi.ShowConfirmation(this, "Leave Crownroad?", "Your progress is saved.", "Quit", () => GetTree().Quit())));

        var hero = new VBoxContainer { Position = new Vector2(64, 112), Size = new Vector2(530, 440) };
        hero.AddThemeConstantOverride("separation", 13);
        AddChild(hero);
        var overline = RealmUi.Label("A KINGDOM WAITING TO BE RECLAIMED", 12, true);
        hero.AddChild(overline);
        hero.AddChild(RealmUi.Heading("CROWNROAD", 56));
        var subtitle = RealmUi.Label("S I E G E   O F   A S H", 17);
        subtitle.AddThemeColorOverride("font_color", RealmUi.Gold);
        hero.AddChild(subtitle);
        hero.AddChild(RealmUi.Label("Gather your warband. Light the road home.", 17, true));
        hero.AddChild(new Control { CustomMinimumSize = new Vector2(0, 20) });
        var stage = GameData.GetStage(Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage));
        hero.AddChild(RealmUi.Label($"STAGE {stage.StageNumber:00}  /  {RouteCatalog.Get(stage.MapId).Title.ToUpperInvariant()}", 12, true));
        hero.AddChild(RealmUi.Heading(stage.StageName, 27));
        var journey = new HBoxContainer();
        hero.AddChild(journey);
        var campaign = RealmUi.Button("map", GameState.Instance.HighestUnlockedStage > 1 ? "Continue journey" : "Begin journey", () => SceneRouter.Instance.GoToMap(), true);
        campaign.CustomMinimumSize = new Vector2(270, 56);
        journey.AddChild(campaign);
        journey.AddChild(RealmUi.Button("sword", "Armory", () => SceneRouter.Instance.GoToShop()));
        var stars = GameData.Stages.Sum(s => GameState.Instance.GetStageStars(s.StageNumber));
        hero.AddChild(RealmUi.Label($"{GameData.Stages.Count(s => GameState.Instance.GetStageStars(s.StageNumber) > 0)} / {GameState.Instance.MaxStage} leaders defeated   ·   {stars} stars earned", 13, true));
        _notice = RealmUi.Label("", 13, true);
        hero.AddChild(_notice);

        var squad = RealmUi.Panel(this, new Rect2(864, 388, 350, 157), out _);
        squad.AddChild(RealmUi.Label("YOUR WARBAND", 12, true));
        var portraits = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        squad.AddChild(portraits);
        foreach (var unit in GameState.Instance.GetActiveDeckUnits())
        {
            var card = new VBoxContainer();
            var portrait = UiBadgeFactory.CreateUnitBadge(unit, new Vector2(80, 80));
            portrait.TooltipText = $"{unit.DisplayName} · Level {GameState.Instance.GetUnitLevel(unit.Id)}";
            portrait.MouseFilter = MouseFilterEnum.Stop;
            card.AddChild(portrait);
            portraits.AddChild(card);
        }

        var dock = RealmUi.Panel(this, new Rect2(40, 574, 1200, 120), out _);
        RealmUi.Tabs(dock, ShowDestinations, "Adventure", "Caravan", "Community");
        _destinations = new VBoxContainer();
        dock.AddChild(_destinations);
        ShowDestinations(0);
        RealmUi.FadeIn(hero);
    }

    private void ShowDestinations(int tab)
    {
        RealmUi.Clear(_destinations);
        var row = new HBoxContainer();
        _destinations.AddChild(row);
        void Link(string icon, string title, Action action, string locked = null)
        {
            var button = RealmUi.Button(icon, title, action);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.Disabled = locked != null;
            button.TooltipText = locked ?? title;
            row.AddChild(button);
        }
        if (tab == 0)
        {
            Link("flame", "Endless", () => SceneRouter.Instance.GoToEndless());
            Link("mountain", "Tower", () => SceneRouter.Instance.GoToTower());
            Link("flag", "Bounties", () => SceneRouter.Instance.GoToBounty());
            Link("shield", "Boss rush", null, "Boss rush is in development");
            Link("sword", "Weekly raid", () => SceneRouter.Instance.GoToRaid(), GameState.Instance.HighestUnlockedStage < 5 ? "Win stage 4 or higher to unlock raids" : null);
            var activeEvent = GameState.Instance.GetActiveEvent();
            Link("star", "Event", () => SceneRouter.Instance.GoToEvent(), activeEvent == null ? "No event is active" : null);
        }
        else if (tab == 1)
        {
            Link("flag", "Expeditions", () => SceneRouter.Instance.GoToExpeditions());
            Link("hammer", "Forge", () => SceneRouter.Instance.GoToForge());
            Link("gift", "Daily gifts", () => SceneRouter.Instance.GoToLoginCalendar());
            Link("crown", "Season", () => SceneRouter.Instance.GoToSeasonPass());
            Link("book", "Codex", () => SceneRouter.Instance.GoToCodex());
            Link("gold", "Store", () => SceneRouter.Instance.GoToCashShop());
            if (GameState.Instance.CanPrestige)
                Link("star", "Prestige", () => MedievalUi.ShowConfirmation(this, "Begin a new age?", "Restart campaign progression for prestige rewards.", "Prestige", () => { GameState.Instance.TryPrestige(out _); SceneRouter.Instance.GoToMainMenu(); }));
        }
        else
        {
            Link("people", "Warband", () => SceneRouter.Instance.GoToGuild());
            Link("people", "Friends", () => SceneRouter.Instance.GoToFriends());
            Link("sword", "Challenges", () => SceneRouter.Instance.GoToMultiplayer());
            Link("shield", "Arena", () => SceneRouter.Instance.GoToArena(), GameState.Instance.HighestUnlockedStage < ArenaCatalog.MinRequiredStage ? $"Win stage {ArenaCatalog.MinRequiredStage - 1} or higher to unlock the arena" : null);
            Link("crown", "Rankings", () => SceneRouter.Instance.GoToLeaderboard());
        }
    }
}
