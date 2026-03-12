using Godot;

public partial class SettingsMenu : Control
{
    private Label _audioLabel = null!;
    private Label _interfaceLabel = null!;
    private Label _returnLabel = null!;
    private Button _muteButton = null!;
    private Button _showDevUiButton = null!;
    private Button _showFpsButton = null!;
    private Button _backButton = null!;

    public override void _Ready()
    {
        BuildUi();
        RefreshUi();
    }

    private void BuildUi()
    {
        var background = new ColorRect
        {
            Color = new Color("14213d")
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(720f, 560f)
        };
        center.AddChild(panel);

        var content = new MarginContainer();
        content.AddThemeConstantOverride("margin_left", 24);
        content.AddThemeConstantOverride("margin_top", 24);
        content.AddThemeConstantOverride("margin_right", 24);
        content.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(content);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 16);
        content.AddChild(stack);

        stack.AddChild(new Label
        {
            Text = "Settings",
            HorizontalAlignment = HorizontalAlignment.Center
        });

        _returnLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        stack.AddChild(_returnLabel);

        var audioPanel = new PanelContainer();
        stack.AddChild(audioPanel);

        var audioPadding = new MarginContainer();
        audioPadding.AddThemeConstantOverride("margin_left", 14);
        audioPadding.AddThemeConstantOverride("margin_top", 14);
        audioPadding.AddThemeConstantOverride("margin_right", 14);
        audioPadding.AddThemeConstantOverride("margin_bottom", 14);
        audioPanel.AddChild(audioPadding);

        var audioStack = new VBoxContainer();
        audioStack.AddThemeConstantOverride("separation", 10);
        audioPadding.AddChild(audioStack);

        audioStack.AddChild(new Label
        {
            Text = "Audio Mix"
        });

        _audioLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        audioStack.AddChild(_audioLabel);

        var audioRow = new HBoxContainer();
        audioRow.AddThemeConstantOverride("separation", 8);
        audioStack.AddChild(audioRow);

        audioRow.AddChild(BuildCompactButton("SFX -", () =>
        {
            GameState.Instance.SetEffectsVolumePercent(GameState.Instance.EffectsVolumePercent - 10);
            RefreshUi();
        }));
        audioRow.AddChild(BuildCompactButton("SFX +", () =>
        {
            GameState.Instance.SetEffectsVolumePercent(GameState.Instance.EffectsVolumePercent + 10);
            RefreshUi();
        }));
        audioRow.AddChild(BuildCompactButton("Amb -", () =>
        {
            GameState.Instance.SetAmbienceVolumePercent(GameState.Instance.AmbienceVolumePercent - 10);
            RefreshUi();
        }));
        audioRow.AddChild(BuildCompactButton("Amb +", () =>
        {
            GameState.Instance.SetAmbienceVolumePercent(GameState.Instance.AmbienceVolumePercent + 10);
            RefreshUi();
        }));

        _muteButton = BuildCompactButton("Mute", () =>
        {
            GameState.Instance.SetAudioMuted(!GameState.Instance.AudioMuted);
            RefreshUi();
            if (!GameState.Instance.AudioMuted)
            {
                AudioDirector.Instance?.PlayUiConfirm();
            }
        });
        audioStack.AddChild(_muteButton);

        var interfacePanel = new PanelContainer();
        stack.AddChild(interfacePanel);

        var interfacePadding = new MarginContainer();
        interfacePadding.AddThemeConstantOverride("margin_left", 14);
        interfacePadding.AddThemeConstantOverride("margin_top", 14);
        interfacePadding.AddThemeConstantOverride("margin_right", 14);
        interfacePadding.AddThemeConstantOverride("margin_bottom", 14);
        interfacePanel.AddChild(interfacePadding);

        var interfaceStack = new VBoxContainer();
        interfaceStack.AddThemeConstantOverride("separation", 10);
        interfacePadding.AddChild(interfaceStack);

        interfaceStack.AddChild(new Label
        {
            Text = "Interface"
        });

        _interfaceLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        interfaceStack.AddChild(_interfaceLabel);

        var interfaceRow = new HBoxContainer();
        interfaceRow.AddThemeConstantOverride("separation", 8);
        interfaceStack.AddChild(interfaceRow);

        _showDevUiButton = BuildCompactButton("Toggle Combat Intel", () =>
        {
            GameState.Instance.SetShowDevUi(!GameState.Instance.ShowDevUi);
            RefreshUi();
        });
        interfaceRow.AddChild(_showDevUiButton);

        _showFpsButton = BuildCompactButton("Toggle FPS Counter", () =>
        {
            GameState.Instance.SetShowFpsCounter(!GameState.Instance.ShowFpsCounter);
            RefreshUi();
        });
        interfaceRow.AddChild(_showFpsButton);

        var defaultsButton = new Button
        {
            Text = "Restore Defaults",
            CustomMinimumSize = new Vector2(0f, 46f)
        };
        defaultsButton.Pressed += () =>
        {
            GameState.Instance.SetAudioMuted(false);
            GameState.Instance.SetEffectsVolumePercent(85);
            GameState.Instance.SetAmbienceVolumePercent(65);
            GameState.Instance.SetShowDevUi(true);
            GameState.Instance.SetShowFpsCounter(true);
            RefreshUi();
        };
        stack.AddChild(defaultsButton);

        stack.AddChild(new Control
        {
            CustomMinimumSize = new Vector2(0f, 8f),
            SizeFlagsVertical = SizeFlags.ExpandFill
        });

        var bottomRow = new HBoxContainer();
        bottomRow.AddThemeConstantOverride("separation", 12);
        stack.AddChild(bottomRow);

        _backButton = new Button
        {
            CustomMinimumSize = new Vector2(220f, 48f)
        };
        _backButton.Pressed += () => SceneRouter.Instance.ReturnFromSettings();
        bottomRow.AddChild(_backButton);

        var titleButton = new Button
        {
            Text = "Back To Title",
            CustomMinimumSize = new Vector2(180f, 48f)
        };
        titleButton.Pressed += () => SceneRouter.Instance.GoToMainMenu();
        bottomRow.AddChild(titleButton);
    }

    private static Button BuildCompactButton(string text, System.Action onPressed)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0f, 40f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        button.Pressed += onPressed;
        return button;
    }

    private void RefreshUi()
    {
        _returnLabel.Text = $"Return target: {SceneRouter.Instance.SettingsReturnLabel}";
        _audioLabel.Text =
            $"Effects: {GameState.Instance.EffectsVolumePercent}%  |  Ambience: {GameState.Instance.AmbienceVolumePercent}%\n" +
            $"Muted: {(GameState.Instance.AudioMuted ? "Yes" : "No")}";
        _interfaceLabel.Text =
            $"Combat intel panels: {(GameState.Instance.ShowDevUi ? "Shown" : "Hidden")}\n" +
            $"FPS counter: {(GameState.Instance.ShowFpsCounter ? "Shown" : "Hidden")}";
        _muteButton.Text = GameState.Instance.AudioMuted ? "Unmute" : "Mute";
        _showDevUiButton.Text = GameState.Instance.ShowDevUi ? "Hide Combat Intel" : "Show Combat Intel";
        _showFpsButton.Text = GameState.Instance.ShowFpsCounter ? "Hide FPS Counter" : "Show FPS Counter";
        _backButton.Text = $"Back To {SceneRouter.Instance.SettingsReturnLabel}";
    }
}
