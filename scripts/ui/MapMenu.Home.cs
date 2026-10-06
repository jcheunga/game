using System;
using System.Linq;
using Godot;

public partial class MapMenu
{
    private Control _hud;

    private void BuildUi()
    {
        var background = new ColorRect { Color = new Color("1d3b57"), MouseFilter = MouseFilterEnum.Ignore };
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

        // The concept HUD on the fixed canvas: painted pieces from the atlas plate, live values on top.
        _hud = new Control { Name = "HomeHud", MouseFilter = MouseFilterEnum.Ignore, Position = Vector2.Zero, Size = RoyalArt.Canvas };
        AddChild(_hud);
        var spec = RoyalSpec.For("home");
        foreach (var piece in new[] { new Rect2(434, 0, 414, 108), new Rect2(1199, 12, 64, 64), new Rect2(27, 504, 1253, 216) })
            _hud.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                Texture = RoyalArt.Cut("hud-home", piece), Position = piece.Position, Size = piece.Size, MouseFilter = MouseFilterEnum.Ignore });
        BuildResources(spec);
        BuildDeveloperControls();
        BuildZoneHeading(spec);
        var settings = RoyalButton.Over(spec.Rect("settings.ring"), "Settings", () => SceneRouter.Instance.GoToSettings(), 28);
        var ring = spec.Rect("settings.ring");
        settings.SetGlyph(RoyalKit.Texture("settings-face"), new Rect2(7, 7, ring.Size.X - 14, ring.Size.Y - 14));
        _hud.AddChild(settings);
        BuildSitePanel();
        BuildDock(spec);
    }

    private RoyalResourceBar _resources;

    private void BuildResources(RoyalSpec spec)
    {
        _resources = new RoyalResourceBar { Name = "Resources", BarRect = new Rect2(19, 13, 391, 62), MaxWidth = 412 };
        _hud.AddChild(_resources);
        _resources.Add(spec, "res", "gold", HomeMapArt.Icon("gold"), "Royal storehouse", () => SceneRouter.Instance.GoToCashShop());
        _foodHint = _resources.Add(spec, "res", "food", HomeMapArt.Icon("food"), "Refill food", () => SceneRouter.Instance.GoToCashShop());
        _resources.Add(spec, "res", "stars", HomeMapArt.Icon("star"), "Player profile", () => SceneRouter.Instance.GoToProfile());
    }

    private Button _foodHint;

    private void BuildZoneHeading(RoyalSpec spec)
    {
        _mapTitle = spec.Label("zone.title", "", 250, new Color("15120e"));
        _mapTitle.ShadowOffset = Vector2.Zero;
        _hud.AddChild(_mapTitle);
        _zoneProgress = spec.Label("zone.subtitle", "", 220);
        _zoneProgress.ShadowOffset = Vector2.Zero;
        _hud.AddChild(_zoneProgress);
        // The lion banners either side of the plaque page between zones.
        _previousZone = RoyalButton.Over(spec.Rect("zone.banner.left.cloth"), "Previous zone", () => ChangeZone(-1), 4);
        _nextZone = RoyalButton.Over(spec.Rect("zone.banner.right.cloth"), "Next zone", () => ChangeZone(1), 4);
        _hud.AddChild(_previousZone); _hud.AddChild(_nextZone);
        var zoneInfo = RoyalButton.Over(spec.Rect("zone.plaque"), "Zone progress", null, 8);
        zoneInfo.FocusMode = FocusModeEnum.None;
        _hud.AddChild(zoneInfo);
        _zoneInfo = zoneInfo;
    }

    private Button _zoneInfo;

    private void BuildDock(RoyalSpec spec)
    {
        void Medallion(string key, string unused, string title, System.Action action)
        {
            var rect = spec.Rect($"dock.{key}");
            var label = spec.Label($"dock.{key}.label", title, 140, new Color("1b1712"));
            label.ShadowOffset = Vector2.Zero;
            var area = new Rect2(rect.Position - new Vector2(16, 2), new Vector2(rect.Size.X + 32, label.Position.Y + label.Size.Y - rect.Position.Y));
            var button = RoyalButton.Over(area, title, action, 12);
            button.Name = title + "Tab";
            var face = new Rect2(rect.Position + new Vector2(6.5f, 6.5f), rect.Size - new Vector2(13, 13));
            button.SetGlyph(RoyalKit.Texture("dock-" + key), new Rect2(face.Position - area.Position, face.Size));
            label.Position -= area.Position;
            button.SetCaption(label, new Rect2(label.Position, label.Size));
            _hud.AddChild(button);
        }
        Medallion("warband", "sword", "Warband", () => SceneRouter.Instance.GoToShop(0));
        Medallion("spells", "flame", "Spells", () => SceneRouter.Instance.GoToShop(1));
        Medallion("upgrades", "hammer", "Upgrades", () => SceneRouter.Instance.GoToShop(2));
        Medallion("achievements", "star", "Achievements", () => OpenHomeDestination("achievements"));
        Medallion("codex", "book", "Codex", () => SceneRouter.Instance.GoToCodex());
        Medallion("more", "people", "More", ShowMore);
    }

    private void BuildSitePanel()
    {
        _sitePanel = new PanelContainer { Name = "SelectedSite", Visible = false };
        _sitePanel.AddThemeStyleboxOverride("panel", HomeMapUi.Surface(false, 16));
        _hud.AddChild(_sitePanel);
        _sitePanel.AnchorLeft = _sitePanel.AnchorRight = 1;
        _sitePanel.OffsetLeft = -382;
        _sitePanel.OffsetRight = -22;
        _sitePanel.OffsetTop = SitePanelTop;
        _sitePanel.OffsetBottom = SitePanelTop + 240;
        _sitePanel.VisibilityChanged += QueueSitePanelFit;
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
        close.AddThemeConstantOverride("icon_max_width", 22); // Clears the rim of the compact button.
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
        _action = RealmUi.Button("flag", "Prepare battle", VisitSelected, true);
        HomeMapUi.StyleButton(_action, true);
        side.AddChild(_action);
    }

    // New reward rows only report their wrapped height once laid out, so measure on the next frame.
    private void QueueSitePanelFit()
    {
        if (IsInsideTree()) GetTree().CreateTimer(0).Timeout += FitSitePanel;
    }

    // The details card hugs its content, up to the space between the header and the dock.
    private void FitSitePanel()
    {
        if (!IsInstanceValid(_sitePanel) || !_sitePanel.Visible) return;
        var side = _sitePanel.GetChild<VBoxContainer>(0);
        var content = side.GetCombinedMinimumSize().Y + _overview.GetCombinedMinimumSize().Y
            + _sitePanel.GetThemeStylebox("panel").GetMinimumSize().Y;
        var available = _hud.Size.Y - SitePanelTop - SitePanelBottomGap;
        _sitePanel.OffsetBottom = SitePanelTop + Mathf.Min(content, available);
    }

    private const float SitePanelTop = 136, SitePanelBottomGap = 156;

    private void RefreshZoneNavigation()
    {
        var maps = GameData.Stages.Select(stage => stage.MapId).Distinct().ToArray();
        var index = Array.IndexOf(maps, _activeMapId);
        var stages = GameData.GetStagesForMap(_activeMapId);
        _zoneProgress.Text = $"ZONE {index + 1:00}";
        _zoneInfo.TooltipText = $"Zone {index + 1} · {stages.Count(stage => GameState.Instance.GetStageStars(stage.StageNumber) > 0)}/{stages.Count} cleared";
        _previousZone.Disabled = _mapCanvas.IsTravelling || index <= 0;
        var hasNext = index + 1 < maps.Length;
        var nextUnlocked = hasNext && GameState.Instance.IsAdventureZoneUnlocked(maps[index + 1]);
        _nextZone.Disabled = _mapCanvas.IsTravelling || !nextUnlocked;
        _nextZone.TooltipText = nextUnlocked ? "Next zone" : hasNext ? $"Defeat the boss of {RouteCatalog.Get(_activeMapId).Title} to reveal the next zone" : "You have reached the final zone";
    }

    private void ChangeZone(int direction)
    {
        var maps = GameData.Stages.Select(stage => stage.MapId).Distinct().ToArray();
        var index = Array.IndexOf(maps, _activeMapId) + direction;
        if (index >= 0 && index < maps.Length) SwitchRegion(maps[index]);
    }

    private void ShowMore() => OpenHomeDestination("more");

    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Keycode: Key.Escape }) return;
        if (HasHomeModal) CloseHomeModal();
        else if (_sitePanel.Visible) CloseSiteDetails();
        else return;
        GetViewport().SetInputAsHandled();
    }
}
