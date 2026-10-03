using Godot;
using System;
using System.Linq;

public partial class BattleController
{
    private BattleHudBar _healthBar;
    private HBoxContainer _goldReadout;
    private Label _goldAmount, _restartMessage, _endRetryMessage;
    private Button _hudSettingsButton, _restartButton, _resumeButton;
    private RealmModal _battleSettingsModal;
    private Control _battleHudRoot;
    private PanelContainer _deploymentRail;
    private Action _hudLayout;
    private bool _restartPending;

    private void BuildCompactHud(Control root)
    {
        _battleHudRoot = root;
        _topHudPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        _topHudPanel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        root.AddChild(_topHudPanel);
        var meters = new VBoxContainer(); meters.AddThemeConstantOverride("separation", 8);
        _topHudPanel.AddChild(meters);
        BattleHudBar Meter(string icon, string name, Color color)
        {
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 8); meters.AddChild(row);
            row.AddChild(new TextureRect { Texture = HomeMapArt.Icon(icon), CustomMinimumSize = new Vector2(32, 32),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore });
            var bar = new BattleHudBar { CustomMinimumSize = new Vector2(184, 30), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                TooltipText = name, AccessibilityName = name };
            bar.Setup(color, new Color("ffffff33"), ""); row.AddChild(bar); return bar;
        }
        _healthBar = Meter("heart", "War wagon health", new Color("bd605c"));
        _courageBar = Meter("flame", "Courage", new Color("71b6d1"));
        _goldReadout = HomeResourceUi.Amount("gold", "0", "Gold", 36);
        _goldAmount = _goldReadout.GetChildren().OfType<Label>().Single();
        _goldAmount.AddThemeFontSizeOverride("font_size", 28);
        _goldAmount.AddThemeConstantOverride("outline_size", 4);
        _goldAmount.AddThemeColorOverride("font_outline_color", new Color("111a20"));
        root.AddChild(_goldReadout);
        _hudSettingsButton = RealmUi.IconButton("gear", "Battle menu [Escape]", TogglePause);
        _hudSettingsButton.CustomMinimumSize = new Vector2(56, 56); root.AddChild(_hudSettingsButton);

        // Keep diagnostic data available to review tools, outside the live battle UI.
        _intelPanel = new PanelContainer { Visible = false }; root.AddChild(_intelPanel);
        var diagnostics = new VBoxContainer(); _intelPanel.AddChild(diagnostics);
        Label Diagnostic() { var label = new Label(); diagnostics.AddChild(label); return label; }
        _battleBannerLabel = Diagnostic(); _baseHealthLabel = Diagnostic(); _timerLabel = Diagnostic();
        _statusLabel = Diagnostic(); _battleSubtitleLabel = Diagnostic(); _baseWeaponsIntel = Diagnostic();
        _battleMissionLabel = Diagnostic(); _resourceLabel = Diagnostic(); _waveIntelLabel = Diagnostic();
        _objectiveStatusLabel = Diagnostic();
        _waveProgressBar = new BattleHudBar { Visible = false }; diagnostics.AddChild(_waveProgressBar);
        _showDevUiToggle = new CheckBox { Visible = false }; diagnostics.AddChild(_showDevUiToggle);
        _showFpsToggle = new CheckBox { Visible = false }; diagnostics.AddChild(_showFpsToggle);
        _showDevUiToggle.Toggled += OnShowDevUiToggled; _showFpsToggle.Toggled += OnShowFpsToggled;
        _fpsLabel = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        _fpsLabel.AddThemeFontSizeOverride("font_size", 14); root.AddChild(_fpsLabel);
    }

    private void ConfigureCompactHudLayout(PanelContainer cards, HBoxContainer row)
    {
        _deploymentRail = cards;
        cards.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        _hudLayout = () =>
        {
            if (!IsInstanceValid(_battleHudRoot)) return;
            var scale = _battleHudRoot.Scale.X;
            var size = GetViewportRect().Size / scale;
            var left = (SafeAreaService.Instance?.MarginLeft ?? 0) / scale + 18;
            var right = (SafeAreaService.Instance?.MarginRight ?? 0) / scale + 18;
            var top = (SafeAreaService.Instance?.MarginTop ?? 0) / scale + 16;
            var bottom = (SafeAreaService.Instance?.MarginBottom ?? 0) / scale + 16;
            _topHudPanel.Position = new Vector2(left, top); _topHudPanel.Size = new Vector2(224, 72);
            _goldReadout.Size = _goldReadout.GetCombinedMinimumSize();
            _goldReadout.Position = new Vector2(size.X - right - _goldReadout.Size.X, top);
            _fpsLabel.Position = new Vector2(size.X - right - 100, top + 48); _fpsLabel.Size = new Vector2(100, 20);
            _hudSettingsButton.Position = new Vector2(left, size.Y - bottom - 56); _hudSettingsButton.Size = new Vector2(56, 56);
            var veil = _pauseOverlay.GetChildren().OfType<ColorRect>().FirstOrDefault();
            if (veil != null) veil.CustomMinimumSize = size;
            var width = Mathf.Min(row.GetCombinedMinimumSize().X + 40, size.X - left - right - 160);
            var height = MobilePresentation.Enabled ? 124 : 148;
            cards.Position = new Vector2(Mathf.Max(left + 80, (size.X - width) / 2), size.Y - bottom - height);
            cards.Size = new Vector2(width, height);
            if (_convoyOrderButton != null)
                ((Control)_convoyOrderButton.GetParent()).Position = new Vector2(left, top + 94);
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
            var label = _battleEnded ? _endRetryMessage : _restartMessage;
            label.Text = state.Food < state.GetStageEntryFoodCost(_stage)
                ? $"Need {state.GetStageEntryFoodCost(_stage)} rations · Have {state.Food}" : message;
            label.Visible = true; return;
        }
        _restartPending = true; _restartButton.Disabled = true; _endPrimaryButton.Disabled = true;
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
