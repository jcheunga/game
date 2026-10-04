using System;
using System.Linq;
using Godot;

public partial class MapMenu
{
    private Control _hud;
    private GridContainer _destinations;

    private void BuildUi()
    {
        var background = new ColorRect { Color = new Color("253331"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(background);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _mapCanvas = new MapPathCanvas { Name = "ZoneMap" };
        AddChild(_mapCanvas);
        _mapCanvas.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _mapCanvas.SiteSelected += SelectSite;
        _mapCanvas.DiscoverySelected += SelectDiscovery;
        _mapCanvas.TravelStateChanged += RefreshUi;
        _mapCanvas.TravelFeedback += _ => RefreshUi();
        GameState.Instance.FoodChanged += RefreshUi;
        GameState.Instance.DeveloperStateChanged += RefreshUi;

        _hud = new Control { Name = "HomeHud", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_hud);
        _hud.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SafeAreaService.Instance?.ApplyToControl(_hud);
        BuildResources();
        BuildDeveloperControls();
        BuildZoneHeading();
        var settings = HomeMapUi.IconButton("gear", "Settings", () => SceneRouter.Instance.GoToSettings());
        _hud.AddChild(settings);
        HomeMapUi.Place(settings, 1, 0, new Rect2(-74, 20, 52, 52));
        BuildSitePanel();
        BuildDock();
    }

    private void BuildResources()
    {
        var panel = new PanelContainer { Name = "Resources" };
        panel.AddThemeStyleboxOverride("panel", HomeMapUi.Surface(false, 10));
        _hud.AddChild(panel);
        HomeMapUi.Place(panel, 0, 0, new Rect2(22, 20, 318, 62));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        panel.AddChild(row);
        Label Metric(string icon, string hint, Action action)
        {
            var button = new Button
            {
                AccessibilityName = hint, TooltipText = hint, Flat = true,
                SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseDefaultCursorShape = CursorShape.PointingHand
            };
            foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
                button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            row.AddChild(button);
            var content = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            button.AddChild(content);
            content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            content.AddThemeConstantOverride("separation", 7);
            content.AddChild(new TextureRect
            {
                Texture = HomeMapArt.Icon(icon), CustomMinimumSize = new Vector2(38, 38),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore
            });
            var value = new Label { VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            value.AddThemeFontSizeOverride("font_size", RealmUi.ButtonFontSize);
            value.AddThemeColorOverride("font_color", new Color("f1e7cb"));
            content.AddChild(value);
            button.Pressed += () => action();
            // Allow the amount to grow without clipping large existing balances.
            content.MinimumSizeChanged += () => button.CustomMinimumSize = content.GetCombinedMinimumSize();
            return value;
        }
        _gold = Metric("gold", "Royal storehouse", () => SceneRouter.Instance.GoToCashShop());
        _food = Metric("food", "Refill food", () => SceneRouter.Instance.GoToCashShop());
        _stars = Metric("star", "Player profile", () => SceneRouter.Instance.GoToProfile());
    }

    private void BuildZoneHeading()
    {
        var panel = new PanelContainer { Name = "ZoneHeading" };
        panel.AddThemeStyleboxOverride("panel", HomeMapUi.Surface(false, 8));
        _hud.AddChild(panel);
        HomeMapUi.Place(panel, .5f, 0, new Rect2(-180, 20, 360, 86));
        var title = new VBoxContainer();
        title.AddThemeConstantOverride("separation", 0);
        panel.AddChild(title);
        var pager = new HBoxContainer();
        pager.AddThemeConstantOverride("separation", 5);
        title.AddChild(pager);
        _previousZone = HomeMapUi.IconButton("back", "Previous zone", () => ChangeZone(-1));
        pager.AddChild(_previousZone);
        _mapTitle = RealmUi.Heading("", 22);
        _mapTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _mapTitle.VerticalAlignment = VerticalAlignment.Center;
        pager.AddChild(_mapTitle);
        _nextZone = HomeMapUi.IconButton("arrow", "Next zone", () => ChangeZone(1));
        pager.AddChild(_nextZone);
        _zoneProgress = RealmUi.Label("", 18, true);
        _zoneProgress.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddChild(_zoneProgress);
    }

    private void BuildDock()
    {
        var dock = new PanelContainer { Name = "HomeTabs" };
        dock.AddThemeStyleboxOverride("panel", HomeMapUi.Surface(false, 8, 16));
        _hud.AddChild(dock);
        HomeMapUi.Place(dock, .5f, 1, new Rect2(-354, -142, 708, 126));
        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        dock.AddChild(tabs);
        tabs.AddChild(HomeMapUi.Tab("sword", "Warband", () => SceneRouter.Instance.GoToShop(0)));
        tabs.AddChild(HomeMapUi.Tab("flame", "Spells", () => SceneRouter.Instance.GoToShop(1)));
        tabs.AddChild(HomeMapUi.Tab("hammer", "Upgrades", () => SceneRouter.Instance.GoToShop(2)));
        tabs.AddChild(HomeMapUi.Tab("star", "Achievements", () => OpenHomeDestination("achievements")));
        tabs.AddChild(HomeMapUi.Tab("book", "Codex", () => SceneRouter.Instance.GoToCodex()));
        tabs.AddChild(HomeMapUi.Tab("people", "More", ShowMore));
    }

    private void BuildSitePanel()
    {
        _sitePanel = new PanelContainer { Name = "SelectedSite", Visible = false };
        _sitePanel.AddThemeStyleboxOverride("panel", HomeMapUi.Surface(false, 16));
        _hud.AddChild(_sitePanel);
        _sitePanel.AnchorLeft = _sitePanel.AnchorRight = 1;
        _sitePanel.AnchorBottom = 1;
        _sitePanel.OffsetLeft = -382;
        _sitePanel.OffsetRight = -22;
        _sitePanel.OffsetTop = 136;
        _sitePanel.OffsetBottom = -156;
        var side = new VBoxContainer();
        side.AddThemeConstantOverride("separation", 8);
        _sitePanel.AddChild(side);
        var header = new HBoxContainer();
        side.AddChild(header);
        _siteEyebrow = RealmUi.Label("", 18, true);
        _siteEyebrow.VerticalAlignment = VerticalAlignment.Center;
        header.AddChild(_siteEyebrow);
        var close = HomeMapUi.IconButton("close", "Close site details", CloseSiteDetails);
        close.CustomMinimumSize = new Vector2(40, 40);
        header.AddChild(close);
        var encounter = new HBoxContainer();
        encounter.AddThemeConstantOverride("separation", 12);
        side.AddChild(encounter);
        _portrait = new TextureRect
        {
            CustomMinimumSize = new Vector2(56, 64), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        encounter.AddChild(_portrait);
        var names = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        names.AddThemeConstantOverride("separation", 4);
        encounter.AddChild(names);
        _siteName = RealmUi.Heading("", 22);
        _siteName.VerticalAlignment = VerticalAlignment.Center;
        names.AddChild(_siteName);
        _siteStatus = RealmUi.Label("", 18, true);
        names.AddChild(_siteStatus);
        _overview = RealmUi.Scroll(side);
        _overview.AddThemeConstantOverride("separation", 10);
        _rewards = new VBoxContainer { Name = "SiteRewards" };
        _rewards.AddThemeConstantOverride("separation", 10);
        _overview.AddChild(_rewards);
        _description = RealmUi.Label("");
        _overview.AddChild(_description);
        _directive = RealmUi.Button("shield", "Heroic directive", ToggleDirective);
        HomeMapUi.StyleButton(_directive);
        side.AddChild(_directive);
        _action = RealmUi.Button("flag", "Prepare battle", VisitSelected, true);
        HomeMapUi.StyleButton(_action, true);
        side.AddChild(_action);
    }

    private void RefreshZoneNavigation()
    {
        var maps = GameData.Stages.Select(stage => stage.MapId).Distinct().ToArray();
        var index = Array.IndexOf(maps, _activeMapId);
        var stages = GameData.GetStagesForMap(_activeMapId);
        _zoneProgress.Text = $"ZONE {index + 1:00}  ·  {stages.Count(stage => GameState.Instance.GetStageStars(stage.StageNumber) > 0)}/{stages.Count} cleared";
        _previousZone.Disabled = _mapCanvas.IsTravelling || index <= 0;
        var hasNext = index + 1 < maps.Length;
        var nextUnlocked = hasNext && GameState.Instance.IsAdventureZoneUnlocked(maps[index + 1]);
        _nextZone.Disabled = _mapCanvas.IsTravelling || !nextUnlocked;
        _nextZone.Icon = RealmUi.Icon(nextUnlocked ? "arrow" : "lock");
        _nextZone.TooltipText = nextUnlocked ? "Next zone" : hasNext ? $"Defeat the boss of {RouteCatalog.Get(_activeMapId).Title} to reveal the next zone" : "You have reached the final zone";
    }

    private void ChangeZone(int direction)
    {
        var maps = GameData.Stages.Select(stage => stage.MapId).Distinct().ToArray();
        var index = Array.IndexOf(maps, _activeMapId) + direction;
        if (index >= 0 && index < maps.Length) SwitchRegion(maps[index]);
    }

    private void ShowMore() => OpenHomeDestination("more");

    private void ShowDestinations(int tab)
    {
        RealmUi.Clear(_destinations);
        void Link(string icon, string title, Action action, string locked = null)
        {
            var button = new Button { CustomMinimumSize = new Vector2(0, 216), SizeFlagsHorizontal = SizeFlags.ExpandFill, AccessibilityName = title, TooltipText = locked ?? title, Disabled = locked != null };
            ModalUi.StyleButton(button, accent: tab == 0 ? new Color("6387b9") : tab == 1 ? new Color("55a28a") : new Color("bd6073"), material: ModalMaterial.Inset);
            var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; content.AddThemeConstantOverride("separation", 5);
            button.AddChild(content); content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); content.OffsetLeft = content.OffsetTop = 14; content.OffsetRight = content.OffsetBottom = -14;
            content.MinimumSizeChanged += () => button.CustomMinimumSize = new Vector2(0, Mathf.Max(216, content.GetCombinedMinimumSize().Y + 28));
            int illustration = title switch {
                "Endless" or "Codex" => 1, "Tower" or "Warband guild" => 0, "Forge" => 2,
                "Bounties" or "Expeditions" or "Challenges" => 3,
                "Boss rush" or "Weekly raid" or "Season" or "Arena" or "Rankings" => 5, _ => 4 };
            var art = new TextureRect { Texture = ModalArt.Illustration(illustration), CustomMinimumSize = new Vector2(0, 64), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore, Modulate = locked == null ? Colors.White : new Color(.45f,.45f,.45f) }; content.AddChild(art);
            var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; head.AddThemeConstantOverride("separation", 12); content.AddChild(head);
            head.AddChild(new TextureRect { Texture = HomeMapArt.Icon(icon), CustomMinimumSize = new Vector2(44,44), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore });
            var name = RealmUi.Heading(title, 20); name.AddThemeFontSizeOverride("font_size", RealmUi.ButtonFontSize); name.VerticalAlignment = VerticalAlignment.Center; name.MouseFilter = MouseFilterEnum.Ignore; head.AddChild(name);
            var description = RealmUi.Label(locked ?? title switch {
                "Endless" => "Hold the line against an endless horde.", "Tower" => "Climb 100 floors of escalating battles.", "Bounties" => "Daily objectives and useful rewards.", "Weekly raid" => "Face a powerful boss with your guild.", "Event" => "Limited adventures and seasonal rewards.",
                "Expeditions" => "Send reserve allies to gather supplies.", "Forge" => "Craft, fuse and enchant your relics.", "Daily gifts" => "Collect today's caravan supplies.", "Season" => "Earn rewards as your journey continues.", "Codex" => "Read your field notes and discoveries.", "Store" => "Refill supplies and browse offers.", "Warband guild" => "Join allies and contribute to your guild.", "Friends" => "Find friends and exchange gifts.", "Challenges" => "Daily races, shared runs and LAN play.", "Arena" => "Challenge rival warbands.", "Rankings" => "See the kingdom's leading caravans.", _ => "Continue your Crownroad journey." }, 18, true);
            description.MouseFilter = MouseFilterEnum.Ignore; content.AddChild(description);
            RealmModal.Polish(name); RealmModal.Polish(description);
            button.Pressed += () => action?.Invoke();
            _destinations.AddChild(button);
        }
        if (tab == 0)
        {
            Link("flame", "Endless", () => SceneRouter.Instance.GoToEndless());
            Link("mountain", "Tower", () => SceneRouter.Instance.GoToTower());
            Link("flag", "Bounties", () => SceneRouter.Instance.GoToBounty());
            Link("shield", "Boss rush", null, "Boss rush is in development");
            Link("sword", "Weekly raid", () => SceneRouter.Instance.GoToRaid(), GameState.Instance.HighestUnlockedStage < 5 ? "Win stage 4 or higher to unlock raids" : null);
            Link("star", "Event", () => SceneRouter.Instance.GoToEvent(), GameState.Instance.GetActiveEvent() == null ? "No event is active" : null);
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
                Link("star", "Prestige", () => MedievalUi.ShowConfirmation(this, "Begin a new age?", "Restart campaign progression for prestige rewards.", "Prestige", () => { GameState.Instance.TryPrestige(out _); SceneRouter.Instance.ReloadHome(); }));
        }
        else
        {
            Link("people", "Warband guild", () => SceneRouter.Instance.GoToGuild());
            Link("people", "Friends", () => SceneRouter.Instance.GoToFriends());
            Link("sword", "Challenges", () => SceneRouter.Instance.GoToMultiplayer());
            Link("shield", "Arena", () => SceneRouter.Instance.GoToArena(), GameState.Instance.HighestUnlockedStage < ArenaCatalog.MinRequiredStage ? $"Win stage {ArenaCatalog.MinRequiredStage - 1} or higher to unlock the arena" : null);
            Link("crown", "Rankings", () => SceneRouter.Instance.GoToLeaderboard());
        }
    }

    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Keycode: Key.Escape }) return;
        if (HasHomeModal) CloseHomeModal();
        else if (_sitePanel.Visible) CloseSiteDetails();
        else return;
        GetViewport().SetInputAsHandled();
    }
}
