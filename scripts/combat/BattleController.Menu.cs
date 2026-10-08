using Godot;
using System;
using System.Linq;

public partial class BattleController
{
    private HBoxContainer _goldReadout;
    private PanelContainer _goldFrame;
    private Label _goldAmount, _restartMessage, _endRetryMessage;
    private Button _hudSettingsButton, _restartButton, _resumeButton;
    private RealmModal _battleSettingsModal;
    private Control _battleHudRoot;
    private Action _hudLayout;
    private bool _restartPending;

    private TextureRect _hudBanner;

    // The clean-steel concept HUD: banner and the courage and mana plates at the top left, the gold plaque and pause
    // button at the top right, and the card dock at the bottom centre.
    private void BuildCompactHud(Control root)
    {
        _battleHudRoot = root;
        var spec = RoyalSpec.For("battle");
        _topHudPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _topHudPanel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        root.AddChild(_topHudPanel);
        var meters = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(480, 160) };
        _topHudPanel.AddChild(meters);
        RoyalMeter Meter(string key, string plate, string fill, string name, string icon)
        {
            var rect = spec.Rect(key);
            var iconRect = spec.Rect(key + ".icon");
            var track = spec.Rect(key + ".track");
            var meter = new RoyalMeter { Plate = plate, FillKit = fill, Position = rect.Position, Size = rect.Size, TooltipText = name, AccessibilityName = name,
                Track = new Rect2(track.Position - rect.Position + new Vector2(4, 4), track.Size - new Vector2(8, 8)),
                ValueRight = spec.Number(key + ".value", "x", rect.End.X - 30) - rect.Position.X, ValueBaseline = spec.Number(key + ".value", "baseline", 30) - rect.Position.Y,
                ValueSize = (int)spec.Number(key + ".value", "size", 20), MouseFilter = Control.MouseFilterEnum.Pass,
                IconKit = icon, IconRect = new Rect2(iconRect.Position - rect.Position - new Vector2(2, 2), iconRect.Size + new Vector2(4, 4)) };
            meter.Setup(Colors.White, Colors.White, "");
            meters.AddChild(meter);
            return meter;
        }
        // Courage (troops) takes the concept's long top plate and mana (magic) the one below it; the war
        // wagon's health is drawn on the wagon itself. Mana is blue, so courage fills amber like its flame.
        _courageBar = Meter("courage", "hud-hull", "hud-fill-courage-ember", "Courage", "hud-flame");
        _manaBar = Meter("mana", "hud-courage", "hud-fill-mana", "Mana", "hud-mana");
        _hudBanner = RoyalKit.Image("hud-banner", spec.Rect("banner"));
        meters.AddChild(_hudBanner);

        _goldReadout = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _goldReadout.AddThemeConstantOverride("separation", 14);
        var coin = spec.Rect("gold.icon");
        _goldReadout.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Texture = RoyalKit.Texture("hud-coin"), CustomMinimumSize = coin.Size + new Vector2(6, 6), MouseFilter = Control.MouseFilterEnum.Ignore });
        _goldAmount = new Label { VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        _goldAmount.AddThemeFontOverride("font", RoyalFonts.Body(500));
        _goldAmount.AddThemeFontSizeOverride("font_size", (int)spec.Number("gold.value", "size", 27));
        _goldAmount.AddThemeColorOverride("font_color", new Color("f6e6c3"));
        _goldAmount.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, .6f));
        _goldAmount.AddThemeConstantOverride("shadow_offset_y", 1);
        _goldReadout.AddChild(_goldAmount);
        _goldFrame = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var plaque = RoyalKit.Slice("hud-gold", 20, 8, 30, 8);
        plaque.ContentMarginLeft = coin.Position.X - spec.Rect("gold").Position.X - 3; plaque.ContentMarginRight = 26;
        plaque.ContentMarginTop = plaque.ContentMarginBottom = 0;
        _goldFrame.AddThemeStyleboxOverride("panel", plaque);
        root.AddChild(_goldFrame); _goldFrame.AddChild(_goldReadout);
        var pause = new RoyalButton { AccessibilityName = "Battle menu [Escape]", TooltipText = "Battle menu [Escape]", MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        pause.SetStates(RoyalKit.Slice("hud-pause", 14), 8);
        pause.Pressed += TogglePause;
        var icon = spec.Rect("button.pause.icon");
        pause.SetGlyph(RoyalKit.Texture("hud-pause-icon"), new Rect2(icon.Position - spec.Rect("button.pause").Position - new Vector2(2, 2), icon.Size + new Vector2(4, 4)));
        _hudSettingsButton = pause; root.AddChild(pause);

        _statusLabel = new Label { Visible = false };
        root.AddChild(_statusLabel);
        _fpsLabel = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        _fpsLabel.AddThemeFontSizeOverride("font_size", 14); root.AddChild(_fpsLabel);
    }

    private PanelContainer _cardDock;
    // First-time hints (TutorialHintCatalog) appear in a banner above the card dock.
    private BattleHintBanner _hintBanner;

    private void ConfigureCompactHudLayout(PanelContainer cards, HBoxContainer row)
    {
        _cardDock = cards;
        _hintBanner = new BattleHintBanner();
        _battleHudRoot.AddChild(_hintBanner);
        var spec = RoyalSpec.For("battle");
        var dock = RoyalKit.Slice("hud-dock", 30, 22, 30, 22);
        dock.ContentMarginLeft = dock.ContentMarginRight = 9; dock.ContentMarginTop = 9; dock.ContentMarginBottom = 11;
        cards.AddThemeStyleboxOverride("panel", dock);
        row.AddThemeConstantOverride("separation", 13);
        _hudLayout = () =>
        {
            if (!IsInstanceValid(_battleHudRoot)) return;
            var scale = _battleHudRoot.Scale.X;
            var size = GetViewportRect().Size / scale;
            var left = (SafeAreaService.Instance?.MarginLeft ?? 0) / scale;
            var right = (SafeAreaService.Instance?.MarginRight ?? 0) / scale;
            var top = (SafeAreaService.Instance?.MarginTop ?? 0) / scale;
            var bottom = (SafeAreaService.Instance?.MarginBottom ?? 0) / scale;
            _topHudPanel.Position = new Vector2(left, top); _topHudPanel.Size = new Vector2(480, 160);
            var gold = spec.Rect("gold");
            _goldFrame.Size = new Vector2(Mathf.Max(gold.Size.X, _goldReadout.GetCombinedMinimumSize().X + 60), gold.Size.Y);
            _goldFrame.Position = new Vector2(size.X - right - (1280 - gold.End.X) - _goldFrame.Size.X, top + gold.Position.Y);
            var pause = spec.Rect("button.pause");
            _hudSettingsButton.Position = new Vector2(size.X - right - (1280 - pause.Position.X), top + pause.Position.Y); _hudSettingsButton.Size = pause.Size;
            _fpsLabel.Position = new Vector2(size.X - right - 100, top + 64); _fpsLabel.Size = new Vector2(100, 20);
            if (_battleFollowButton != null)
            { _battleFollowButton.Position = new Vector2(left + 18, size.Y - bottom - 120); _battleFollowButton.Size = new Vector2(56, 56); }
            var veil = _pauseOverlay.GetChildren().OfType<ColorRect>().FirstOrDefault();
            if (veil != null) veil.CustomMinimumSize = size;
            // Cards keep the concept's 119 x 131 size and 132 px pitch while they fit, then share the width.
            var count = row.GetChildCount();
            var available = size.X - left - right - 40;
            var pitch = Mathf.Min(132, (available - 18) / Mathf.Max(1, count));
            var cardWidth = pitch - 13;
            var cardHeight = Mathf.Round(cardWidth * 131 / 119f);
            foreach (var child in row.GetChildren().OfType<Control>()) child.CustomMinimumSize = new Vector2(cardWidth, cardHeight);
            // The cards already shrink to fit, so the row never scrolls; the dock wraps the row's real size so
            // no card edge is clipped.
            if (row.GetParent() is ScrollContainer scroller)
            {
                // The theme's scrollers keep a right gutter for a scrollbar; this one never scrolls, so drop it.
                scroller.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
                scroller.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
                scroller.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
                scroller.CustomMinimumSize = Vector2.Zero;
            }
            var rowSize = row.GetCombinedMinimumSize();
            var dockRect = spec.Rect("dock");
            var width = rowSize.X + 18;
            var height = rowSize.Y + 20;
            cards.Position = new Vector2((size.X - width) / 2, size.Y - bottom - (720 - dockRect.End.Y) - height);
            cards.Size = new Vector2(width, height);
            _hintBanner.Place(size, cards.Position.Y);
            _battleSettingsModal?.FitToArea(size);
        };
        GetViewport().SizeChanged += _hudLayout;
        _hudLayout();
        _mobileResize?.Invoke();
    }

    private void BuildBattleMenu(Control root)
    {
        _pauseOverlay = new CenterContainer { Visible = false };
        root.AddChild(_pauseOverlay); _pauseOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var veil = new ColorRect { Color = new Color("081018ac"), MouseFilter = Control.MouseFilterEnum.Stop };
        _pauseOverlay.AddChild(veil); veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var card = new PanelContainer { CustomMinimumSize = new Vector2(420, 0) };
        card.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Wood, MobilePresentation.Enabled ? 16 : 24)); _pauseOverlay.AddChild(card);
        var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", MobilePresentation.Enabled ? 8 : 12); card.AddChild(stack);
        var title = RealmUi.Heading("Battle paused", 28); title.HorizontalAlignment = HorizontalAlignment.Center; stack.AddChild(title);
        Button ActionButton(string icon, string text, Action action, bool primary = false)
        {
            var button = RealmUi.Button(icon, text, action, primary);
            button.CustomMinimumSize = new Vector2(0, 52); ModalUi.StyleButton(button); stack.AddChild(button); return button;
        }
        _resumeButton = ActionButton("arrow", "Resume", TogglePause, true);
        _restartButton = RealmUi.Button("arrow", "Restart", TryRestartBattle);
        _restartButton.CustomMinimumSize = new Vector2(0, 52); ModalUi.StyleButton(_restartButton);
        StyleRestartButton(_restartButton); stack.AddChild(_restartButton);
        ActionButton("gear", "Game settings", OpenBattleSettings);
        ActionButton("back", "Quit battle", RetreatToMap);
        _restartMessage = RealmUi.Label("", 18, true); _restartMessage.Visible = false; stack.AddChild(_restartMessage);
    }

    private void StyleRestartButton(Button button)
    {
        var cost = GameState.Instance.GetStageEntryFoodCost(_stage);
        button.Text = $"Restart  ·  {cost}";
        button.SetMeta("painted_resource_icon", true); button.Icon = HomeMapArt.Icon("food");
        button.IconAlignment = HorizontalAlignment.Right; button.AddThemeConstantOverride("icon_max_width", 28);
        button.TooltipText = $"Restart this battle · {cost} rations";
        button.AccessibilityName = $"Restart battle, {cost} rations";
        button.Disabled = IsLanRaceMode || IsOnlineRoomMode;
        if (button.Disabled) { button.Text = "Restart unavailable"; button.TooltipText = "Shared matches cannot be restarted individually."; }
    }

    private void TryRestartBattle()
    {
        if (_restartPending || SceneRouter.Instance.IsTransitioning || IsLanRaceMode || IsOnlineRoomMode || _endlessCheckpointActive) return;
        var state = GameState.Instance;
        string message;
        var paid = IsCampaignMode ? state.TrySpendStageEntryFood(_stage, out message) : state.TrySpendBattleRestartFood(_stage, out message);
        if (!paid)
        {
            var reason = state.Food < state.GetStageEntryFoodCost(_stage)
                ? $"Need {state.GetStageEntryFoodCost(_stage)} rations · Have {state.Food}" : message;
            // After the battle the reason shows on the result board; the old card is only used by shared rooms.
            if (_battleEnded && IsInstanceValid(_royalResult)) { RoyalToast.Show(_royalResult, reason, 690); return; }
            var label = _battleEnded ? _endRetryMessage : _restartMessage;
            label.Text = reason; label.Visible = true; return;
        }
        _restartPending = true; _restartButton.Disabled = true; _endPrimaryButton.Disabled = true; if (IsInstanceValid(_royalResult)) _royalResult.RetryButton.Disabled = true;
        CancelCardDrag(); _battlePaused = true; GetTree().Paused = false; ResetBattleSpeed();
        // Freeze the departing simulation while the scene transition runs.
        SceneRouter.Instance.RetryBattle();
    }

    private void OpenBattleSettings()
    {
        if (_battleSettingsModal != null || !_battlePaused) return;
        _pauseOverlay.Hide();
        _battleSettingsModal = new RealmModal { ProcessMode = ProcessModeEnum.Always };
        _battleHudRoot.AddChild(_battleSettingsModal);
        _battleSettingsModal.Closed = CloseBattleSettings; _battleSettingsModal.Back = CloseBattleSettings;
        var settings = GD.Load<PackedScene>(SceneRouter.SettingsScene).Instantiate<SettingsMenu>();
        settings.SetMeta("home_modal", true); settings.SetMeta("battle_modal", true); _battleSettingsModal.Content.AddChild(settings);
        settings.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _battleSettingsModal.Present(SceneRouter.SettingsScene, "Game settings", "Battle paused", true, 840);
        _battleSettingsModal.FitToArea(_battleHudRoot.Size);
    }

    public void CloseBattleSettings()
    {
        if (_battleSettingsModal == null) return;
        _battleSettingsModal.QueueFree(); _battleSettingsModal = null;
        _pauseOverlay.Show(); ApplyDevUiSettings();
        _resumeButton.GrabFocus();
    }

    private bool HandleBattleMenuInput(InputEvent input)
    {
        if (!_battlePaused || _battleSettingsModal != null || input is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Tab } key) return false;
        var buttons = _pauseOverlay.FindChildren("*", "Button", true, false).OfType<Button>()
            .Where(button => button.IsVisibleInTree() && !button.Disabled).ToArray();
        if (buttons.Length == 0) return false;
        var index = Array.IndexOf(buttons, GetViewport().GuiGetFocusOwner());
        buttons[(index + (key.ShiftPressed ? -1 : 1) + buttons.Length) % buttons.Length].GrabFocus();
        GetViewport().SetInputAsHandled(); return true;
    }
}
