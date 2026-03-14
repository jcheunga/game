using Godot;

public partial class SettingsMenu : Control
{
    private Label _audioLabel = null!;
    private Label _interfaceLabel = null!;
    private Label _callsignLabel = null!;
    private Label _syncLabel = null!;
    private Label _lifecycleLabel = null!;
    private Label _returnLabel = null!;
    private Button _muteButton = null!;
    private Button _showDevUiButton = null!;
    private Button _showFpsButton = null!;
    private Button _syncProviderButton = null!;
    private Button _syncAutoFlushButton = null!;
    private Button _backButton = null!;
    private LineEdit _callsignEdit = null!;
    private LineEdit _syncEndpointEdit = null!;

    public override void _Ready()
    {
        if (AppLifecycleService.Instance != null)
        {
            AppLifecycleService.Instance.StateChanged += OnAppLifecycleStateChanged;
        }
        BuildUi();
        RefreshUi();
        AnimateEntrance();
    }

    private Control _mainPanel;

    private void AnimateEntrance()
    {
        if (_mainPanel == null) return;
        _mainPanel.Modulate = new Color(1f, 1f, 1f, 0f);
        _mainPanel.Scale = new Vector2(0.97f, 0.97f);
        _mainPanel.PivotOffset = _mainPanel.Size * 0.5f;
        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_mainPanel, "modulate:a", 1f, 0.25f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_mainPanel, "scale", Vector2.One, 0.3f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
    }

    public override void _ExitTree()
    {
        if (AppLifecycleService.Instance != null)
        {
            AppLifecycleService.Instance.StateChanged -= OnAppLifecycleStateChanged;
        }
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
            CustomMinimumSize = new Vector2(760f, 720f)
        };
        center.AddChild(panel);
        _mainPanel = panel;

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

        _callsignLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        interfaceStack.AddChild(_callsignLabel);

        var callsignRow = new HBoxContainer();
        callsignRow.AddThemeConstantOverride("separation", 8);
        interfaceStack.AddChild(callsignRow);

        _callsignEdit = new LineEdit
        {
            PlaceholderText = "Lantern",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        callsignRow.AddChild(_callsignEdit);

        var callsignButton = new Button
        {
            Text = "Apply Callsign",
            CustomMinimumSize = new Vector2(180f, 40f)
        };
        callsignButton.Pressed += () =>
        {
            GameState.Instance.SetPlayerCallsign(_callsignEdit.Text);
            RefreshUi();
        };
        callsignRow.AddChild(callsignButton);

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

        var syncPanel = new PanelContainer();
        stack.AddChild(syncPanel);

        var syncPadding = new MarginContainer();
        syncPadding.AddThemeConstantOverride("margin_left", 14);
        syncPadding.AddThemeConstantOverride("margin_top", 14);
        syncPadding.AddThemeConstantOverride("margin_right", 14);
        syncPadding.AddThemeConstantOverride("margin_bottom", 14);
        syncPanel.AddChild(syncPadding);

        var syncStack = new VBoxContainer();
        syncStack.AddThemeConstantOverride("separation", 10);
        syncPadding.AddChild(syncStack);

        syncStack.AddChild(new Label
        {
            Text = "Multiplayer Sync"
        });

        _syncLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        syncStack.AddChild(_syncLabel);

        _lifecycleLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        syncStack.AddChild(_lifecycleLabel);

        var providerRow = new HBoxContainer();
        providerRow.AddThemeConstantOverride("separation", 8);
        syncStack.AddChild(providerRow);

        _syncProviderButton = BuildCompactButton("Switch Provider", () =>
        {
            var nextProviderId = GameState.Instance.ChallengeSyncProviderId == ChallengeSyncProviderCatalog.HttpApiId
                ? ChallengeSyncProviderCatalog.LocalJournalId
                : ChallengeSyncProviderCatalog.HttpApiId;
            GameState.Instance.SetChallengeSyncProvider(nextProviderId);
            RefreshUi();
        });
        providerRow.AddChild(_syncProviderButton);

        _syncAutoFlushButton = BuildCompactButton("Toggle Auto Flush", () =>
        {
            GameState.Instance.SetChallengeSyncAutoFlush(!GameState.Instance.ChallengeSyncAutoFlush);
            RefreshUi();
        });
        providerRow.AddChild(_syncAutoFlushButton);

        var profileButton = BuildCompactButton("Refresh Profile", () =>
        {
            PlayerProfileSyncService.RefreshProfile(out _);
            RefreshUi();
        });
        providerRow.AddChild(profileButton);

        var endpointRow = new HBoxContainer();
        endpointRow.AddThemeConstantOverride("separation", 8);
        syncStack.AddChild(endpointRow);

        _syncEndpointEdit = new LineEdit
        {
            PlaceholderText = "https://api.example.com/challenge-sync",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        endpointRow.AddChild(_syncEndpointEdit);

        var endpointButton = new Button
        {
            Text = "Apply Endpoint",
            CustomMinimumSize = new Vector2(190f, 40f)
        };
        endpointButton.Pressed += () =>
        {
            GameState.Instance.SetChallengeSyncEndpoint(_syncEndpointEdit.Text);
            RefreshUi();
        };
        endpointRow.AddChild(endpointButton);

        var defaultsButton = new Button
        {
            Text = "Restore Defaults",
            CustomMinimumSize = new Vector2(0f, 46f)
        };
        defaultsButton.Pressed += () =>
        {
            GameState.Instance.SetPlayerCallsign("Lantern");
            GameState.Instance.ClearPlayerProfileSession();
            GameState.Instance.SetAudioMuted(false);
            GameState.Instance.SetEffectsVolumePercent(85);
            GameState.Instance.SetAmbienceVolumePercent(65);
            GameState.Instance.SetShowDevUi(true);
            GameState.Instance.SetShowFpsCounter(true);
            GameState.Instance.SetChallengeSyncProvider(ChallengeSyncProviderCatalog.LocalJournalId);
            GameState.Instance.SetChallengeSyncEndpoint("");
            GameState.Instance.SetChallengeSyncAutoFlush(false);
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
        _callsignLabel.Text = $"Caravan callsign: {GameState.Instance.PlayerCallsign}\nUsed for LAN room labels and shared scoreboards.";
        _syncLabel.Text =
            $"Profile: {GameState.Instance.PlayerProfileId}\n" +
            $"Auth token: {(string.IsNullOrWhiteSpace(GameState.Instance.PlayerAuthToken) ? "none" : "active")}\n" +
            $"Last profile sync: {(GameState.Instance.LastPlayerProfileSyncAtUnixSeconds <= 0 ? "never" : System.DateTimeOffset.FromUnixTimeSeconds(GameState.Instance.LastPlayerProfileSyncAtUnixSeconds).ToLocalTime().ToString("MM-dd HH:mm:ss"))}\n" +
            $"Provider: {ChallengeSyncProviderCatalog.GetDisplayName(GameState.Instance.ChallengeSyncProviderId)}\n" +
            $"Auto flush: {(GameState.Instance.ChallengeSyncAutoFlush ? "On" : "Off")}\n" +
            $"Endpoint: {(string.IsNullOrWhiteSpace(GameState.Instance.ChallengeSyncEndpoint) ? "not set" : GameState.Instance.ChallengeSyncEndpoint)}\n\n" +
            $"{PlayerProfileSyncService.BuildStatusSummary()}\n\n" +
            $"{(ChallengeSyncService.Instance?.BuildStatusSummary() ?? "Sync service unavailable.")}";
        _lifecycleLabel.Text = AppLifecycleService.Instance?.BuildStatusSummary() ?? "App lifecycle service unavailable.";
        if (!_callsignEdit.HasFocus())
        {
            _callsignEdit.Text = GameState.Instance.PlayerCallsign;
        }
        if (!_syncEndpointEdit.HasFocus())
        {
            _syncEndpointEdit.Text = GameState.Instance.ChallengeSyncEndpoint;
        }
        _muteButton.Text = GameState.Instance.AudioMuted ? "Unmute" : "Mute";
        _showDevUiButton.Text = GameState.Instance.ShowDevUi ? "Hide Combat Intel" : "Show Combat Intel";
        _showFpsButton.Text = GameState.Instance.ShowFpsCounter ? "Hide FPS Counter" : "Show FPS Counter";
        _syncProviderButton.Text = GameState.Instance.ChallengeSyncProviderId == ChallengeSyncProviderCatalog.HttpApiId
            ? "Use Local Stub"
            : "Use HTTP API";
        _syncAutoFlushButton.Text = GameState.Instance.ChallengeSyncAutoFlush
            ? "Disable Auto Flush"
            : "Enable Auto Flush";
        _backButton.Text = $"Back To {SceneRouter.Instance.SettingsReturnLabel}";
    }

    private void OnAppLifecycleStateChanged()
    {
        if (!IsInsideTree())
        {
            return;
        }

        RefreshUi();
    }
}
