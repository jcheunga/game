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
        _mapCanvas.GroundSelected += SelectGround;
        _mapCanvas.TravelStateChanged += RefreshUi;
        _mapCanvas.TravelFeedback += _ => RefreshUi();
        GameState.Instance.FoodChanged += RefreshUi;
        GameState.Instance.DeveloperStateChanged += RefreshUi;

        // The concept HUD on the fixed canvas: painted pieces from the atlas plate, live values on top.
        _hud = new Control { Name = "HomeHud", MouseFilter = MouseFilterEnum.Ignore, Position = Vector2.Zero, Size = RoyalArt.Canvas };
        AddChild(_hud);
        var spec = RoyalSpec.For("home");
        // The plate paints the settings ring at the top right; it moves to the top left corner so the resource
        // strip has the whole stretch beside the zone plaque for long balances.
        var settingsShift = new Vector2(SettingsRingLeft - 1199, 0);
        foreach (var (piece, shift) in new[] { (new Rect2(434, 0, 414, 108), Vector2.Zero), (new Rect2(1199, 12, 64, 64), settingsShift), (new Rect2(27, 504, 1253, 216), Vector2.Zero) })
            _hud.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                Texture = RoyalArt.Cut("hud-home", piece), Position = piece.Position + shift, Size = piece.Size, MouseFilter = MouseFilterEnum.Ignore });
        BuildResources(spec);
        BuildPlayerCard();
        BuildDeveloperControls();
        BuildZoneHeading(spec);
        var ring = new Rect2(spec.Rect("settings.ring").Position + settingsShift, spec.Rect("settings.ring").Size);
        var settings = RoyalButton.Over(ring, "Settings", () => SceneRouter.Instance.GoToSettings(), 28);
        settings.Name = "SettingsButton";
        settings.SetGlyph(RoyalKit.Texture("settings-face"), new Rect2(7, 7, ring.Size.X - 14, ring.Size.Y - 14));
        _hud.AddChild(settings);
        BuildSitePanel();
        BuildDock(spec);
    }

    private RoyalResourceBar _resources;
    /// <summary>Left edge of the settings ring's painted piece: the plate's right margin, mirrored.</summary>
    private const float SettingsRingLeft = 17;

    private void BuildResources(RoyalSpec spec)
    {
        // The resources take the top right corner; the settings ring and the player's card take the top left.
        // Compact spacing keeps it clear of the zone plaque's right banner (which ends at x 848).
        _resources = new RoyalResourceBar { Name = "Resources", BarRect = new Rect2(19, 13, 220, 62), MaxWidth = 409, RightEdge = 1261,
            IconScale = .76f, BeforeDivider = 3, AfterDivider = 4, EndPadding = 22, StartInset = 20 };
        _hud.AddChild(_resources);
        _resources.Add(spec, "res", "gold", HomeMapArt.Icon("gold"), "Royal storehouse", () => SceneRouter.Instance.GoToCashShop(), "0,000");
        _foodHint = _resources.Add(spec, "res", "food", HomeMapArt.Icon("food"), "Refill food", () => SceneRouter.Instance.GoToCashShop(), "00 / 00");
        _resources.Add(spec, "res", "stars", HomeMapArt.Icon("star"), "Player profile", () => SceneRouter.Instance.GoToProfile(), "000");
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
        // The banners are painted art: an unavailable direction keeps its look rather than darkening.
        foreach (var banner in new[] { _previousZone, _nextZone }.OfType<RoyalButton>()) banner.SetStates(null, 4, null, new StyleBoxEmpty());
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

    // The selected-site card is a sheet of the atlas's own parchment: dark ink, engraved capitals, kit buttons.
    private static readonly Color SiteInk = new("2a1608"), SiteFaded = new("5a4330"), SiteCrimson = new("7a1f14");
    private const float SitePanelWidth = 372, SitePanelPadding = 28, SitePanelHead = 52, SiteCloseSize = 38;

    private void BuildSitePanel()
    {
        _sitePanel = new PanelContainer { Name = "SelectedSite", Visible = false };
        // The sheet's top flourish is part of its heading: the content starts beneath it.
        var paper = RoyalKit.Slice("paper-clean", SitePanelPadding);
        paper.ContentMarginTop = SitePanelHead;
        _sitePanel.AddThemeStyleboxOverride("panel", paper);
        _hud.AddChild(_sitePanel);
        _sitePanel.AnchorLeft = _sitePanel.AnchorRight = 1;
        _sitePanel.OffsetLeft = -22 - SitePanelWidth;
        _sitePanel.OffsetRight = -22;
        _sitePanel.OffsetTop = SitePanelTop;
        _sitePanel.OffsetBottom = SitePanelTop + 240;
        _sitePanel.VisibilityChanged += QueueSitePanelFit;
        var side = new VBoxContainer();
        side.AddThemeConstantOverride("separation", 12);
        _sitePanel.AddChild(side);

        var encounter = new HBoxContainer();
        encounter.AddThemeConstantOverride("separation", 14);
        side.AddChild(encounter);
        // The portrait sits in a dark gilt cell, so leaders, landmark miniatures and line icons all read on paper.
        var cell = new PanelContainer { CustomMinimumSize = new Vector2(72, 80), SizeFlagsVertical = SizeFlags.ShrinkBegin, MouseFilter = MouseFilterEnum.Ignore };
        cell.AddThemeStyleboxOverride("panel", new SliceStyle(RoyalKit.Texture("relic-card"), new Rect2(Vector2.Zero, RoyalKit.Texture("relic-card").GetSize() / RoyalKit.Density), RoyalKit.Density, 10, 10, 10, 10, 6, 6));
        cell.SetMeta("frame_bleed", true);
        encounter.AddChild(cell);
        _portrait = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps };
        _portrait.SetMeta("frame_bleed", true);
        cell.AddChild(_portrait);
        var names = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        names.AddThemeConstantOverride("separation", 2);
        encounter.AddChild(names);
        _siteEyebrow = RoyalText.Caps("", ReadingText.PhoneCaps, SiteCrimson, 700);
        _siteEyebrow.ShadowOffset = Vector2.Zero;
        _siteEyebrow.CustomMinimumSize = new Vector2(0, 26);
        names.AddChild(_siteEyebrow);
        _siteName = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        _siteName.AddThemeFontOverride("font", RoyalFonts.Display(700));
        _siteName.AddThemeFontSizeOverride("font_size", 24);
        _siteName.AddThemeColorOverride("font_color", SiteInk);
        _siteName.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        _siteName.AddThemeConstantOverride("line_spacing", -4);
        names.AddChild(_siteName);

        side.AddChild(new TextureRect { Texture = RoyalKit.Texture("codex-rule"), CustomMinimumSize = new Vector2(0, 14), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps });

        _overview = RealmUi.Scroll(side);
        _overview.AddThemeConstantOverride("separation", 10);
        _siteStatus = SiteText("", SiteFaded);
        _overview.AddChild(_siteStatus);
        _rewards = new VBoxContainer { Name = "SiteRewards" };
        _rewards.AddThemeConstantOverride("separation", 8);
        _overview.AddChild(_rewards);
        _description = SiteText("", SiteCrimson, 600);
        _overview.AddChild(_description);

        var width = SitePanelWidth - 2 * SitePanelPadding;
        var action = RoyalButton.Over(new Rect2(0, 0, width, 50), "Prepare battle", VisitSelected, 6);
        action.CustomMinimumSize = action.Size;
        side.AddChild(action);
        _action = action;

        // The kit's red close sits in the card's top right corner, over the layout rather than in it.
        var close = RoyalButton.Over(new Rect2(SitePanelWidth - SiteCloseSize - 14, 14, SiteCloseSize, SiteCloseSize), "Close site details", CloseSiteDetails, 6);
        close.SetStates(RoyalKit.Slice("close-button", 14), 6);
        var corner = new Control { MouseFilter = MouseFilterEnum.Ignore };
        corner.SetMeta("frame_bleed", true);
        _sitePanel.AddChild(corner);
        corner.AddChild(close);
        // The panel container lays the holder out inside its padding; the button is placed from the card's corner.
        close.Position -= new Vector2(SitePanelPadding, SitePanelHead);
    }

    /// <summary>Wrapping book-serif text in ink for the site card.</summary>
    private static Label SiteText(string text, Color ink, int weight = 500)
    {
        var label = RoyalText.Paragraph(text, ReadingText.PhoneBody, ink, weight);
        label.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        return label;
    }

    private (string Caption, string Glyph, bool Enabled) _siteActionLook;

    /// <summary>The card's action: a gold plaque when it can be taken, a locked slate one when it can't.</summary>
    private void StyleSiteAction(string glyph)
    {
        var button = (RoyalButton)_action;
        var enabled = !button.Disabled;
        var look = (button.Text, glyph, enabled);
        if (look == _siteActionLook && button.Caption != null) return;
        _siteActionLook = look;
        button.SetStates(RoyalKit.Slice(enabled ? "button-gold" : "button-slate", 14), 6);
        var caption = RoyalText.Caps(button.Text, 18, enabled ? RoyalBoard.Ink : new Color("fbf3cf"), 700);
        var icon = enabled ? (glyph == null ? null : RoyalKit.Texture(glyph)) : RoyalKit.Texture("icon-lock-small");
        button.Arrange(caption, icon, new Vector2(24, 24), gap: 10);
        button.AccessibilityName = button.Text;
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
        // Wrapped text reports its height for the width it has, so the card is measured after layout.
        var content = side.GetCombinedMinimumSize().Y + _overview.GetCombinedMinimumSize().Y
            + _sitePanel.GetThemeStylebox("panel").GetMinimumSize().Y;
        var available = _hud.Size.Y - SitePanelTop - SitePanelBottomGap;
        _sitePanel.OffsetBottom = SitePanelTop + Mathf.Min(content, available);
    }

    private const float SitePanelTop = 118, SitePanelBottomGap = 156;

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
