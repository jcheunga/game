using System;
using System.Collections.Generic;
using Godot;

public partial class SettingsMenu : Control
{
    private readonly List<(HSlider Slider, Label Amount, string Channel)> _volumes = new();
    private Label _audioLabel = null!;
    private Label _interfaceLabel = null!;
    private Label _callsignLabel = null!;
    private Label _syncLabel = null!;
    private Label _lifecycleLabel = null!;
    private Label _returnLabel = null!;
    private Label _achievementsLabel = null!;
    private Label _purchaseLabel = null!;
    private Label _cloudSaveLabel = null!;
    private LineEdit _purchaseEndpointEdit = null!;
    private Button _muteButton = null!;
    private Button _showFpsButton = null!;
    private Button _showHintsButton = null!;
    private Button _syncProviderButton = null!;
    private Button _syncAutoFlushButton = null!;
    private Button _backButton = null!;
    private Button _titleButton = null!;
    private Button _difficultyButton = null!;
    private Label _difficultyLabel = null!;
    private LineEdit _callsignEdit = null!;
    private LineEdit _syncEndpointEdit = null!;

    public override void _Ready()
    {
        if (AppLifecycleService.Instance != null)
        {
            AppLifecycleService.Instance.StateChanged += OnAppLifecycleStateChanged;
        }
        BuildUi();
        GameState.Instance.DeveloperStateChanged += RefreshUi;
        RefreshUi();
        TryShowMenuHint();
        AnimateEntrance();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        if (keyEvent.Keycode == Key.Escape)
        {
            if (RealmModal.Embedded(this) && GetTree().CurrentScene is MapMenu home) home.CloseHomeModal();
            else SceneRouter.Instance.ReturnFromSettings();
            GetViewport().SetInputAsHandled();
        }
    }

    private void TryShowMenuHint()
    {
        if (!GameState.Instance.ShowHints)
        {
            return;
        }

        var hints = TutorialHintCatalog.GetByContext("first_settings");
        foreach (var hint in hints)
        {
            if (GameState.Instance.HasSeenHint(hint.Id))
            {
                continue;
            }

            _returnLabel.Text = $"[{hint.Title}] {hint.Body}";
            GameState.Instance.MarkHintSeen(hint.Id);
        }
    }

    private Control _mainPanel;

    private void AnimateEntrance()
    {
        if (_mainPanel == null || GameState.Instance.ReducedMotion) return;
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
        if (GameState.Instance != null) GameState.Instance.DeveloperStateChanged -= RefreshUi;
        if (AppLifecycleService.Instance != null)
        {
            AppLifecycleService.Instance.StateChanged -= OnAppLifecycleStateChanged;
        }
    }

    private void BuildUi()
    {
        var embedded = RealmModal.Embedded(this);
        MedievalUi.Apply(this);

        Container center = embedded ? new MarginContainer() : new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var viewportSize = GetViewportRect().Size;
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(
                Mathf.Clamp(viewportSize.X - 48f, 560f, 760f),
                Mathf.Clamp(viewportSize.Y - 48f, 560f, 860f))
        };
        if (embedded) { panel.CustomMinimumSize = Vector2.Zero; panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty()); }
        center.AddChild(panel);
        _mainPanel = panel;

        var content = new MarginContainer();
        content.AddThemeConstantOverride("margin_left", embedded ? 0 : 24);
        content.AddThemeConstantOverride("margin_top", embedded ? 0 : 24);
        content.AddThemeConstantOverride("margin_right", embedded ? 0 : 24);
        content.AddThemeConstantOverride("margin_bottom", embedded ? 0 : 24);
        panel.AddChild(content);

        var rootStack = new VBoxContainer();
        rootStack.AddThemeConstantOverride("separation", 16);
        rootStack.SizeFlagsVertical = SizeFlags.ExpandFill;
        content.AddChild(rootStack);

        var title = RealmUi.Heading("Settings", 30);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        rootStack.AddChild(title); title.Visible = !embedded;
        var account = RealmUi.Button("people", "Account", () => AccountDialog.Show(this));
        rootStack.AddChild(account); account.Visible = !embedded;

        _returnLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _returnLabel.AddThemeColorOverride("font_color", RealmUi.Muted);
        rootStack.AddChild(_returnLabel); _returnLabel.Visible = !embedded;

        var pages = new VBoxContainer[GameState.DeveloperModeAvailable ? 5 : 4];
        var tabTitles = GameState.DeveloperModeAvailable
            ? new[] { "Sound", "Gameplay", "Online", "Account", "Developer" }
            : new[] { "Sound", "Gameplay", "Online", "Account" };
        if (HasMeta("battle_modal")) tabTitles = new[] { "Sound", "Gameplay" };
        RealmUi.Tabs(rootStack, index => { for (int i = 0; i < pages.Length; i++) pages[i].GetParent<ScrollContainer>().Visible = index == i; }, tabTitles);
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i] = RealmUi.Scroll(rootStack);
            pages[i].GetParent<ScrollContainer>().Visible = i == 0;
        }
        if (GameState.DeveloperModeAvailable) BuildDeveloperPage(pages[4]);
        var stack = pages[0];

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
        audioStack.AddChild(_audioLabel); _audioLabel.Visible = false;

        void Volume(string label, string icon, int initial, Action<int> apply)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 16); audioStack.AddChild(row);
            row.AddChild(new TextureRect { Texture = RealmUi.Icon(icon), CustomMinimumSize = new Vector2(32, 32), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
            var name = RealmUi.Label(label, 20); name.CustomMinimumSize = new Vector2(112, 0); name.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; name.VerticalAlignment = VerticalAlignment.Center; row.AddChild(name);
            var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 1, Value = initial, CustomMinimumSize = new Vector2(0, 44), SizeFlagsHorizontal = SizeFlags.ExpandFill, AccessibilityName = label + " volume" }; row.AddChild(slider);
            var amount = RealmUi.Label(initial + "%", 18); amount.CustomMinimumSize = new Vector2(54, 0); amount.SizeFlagsHorizontal = SizeFlags.ShrinkEnd; amount.VerticalAlignment = VerticalAlignment.Center; row.AddChild(amount);
            slider.ValueChanged += value => { apply((int)value); amount.Text = $"{value:0}%"; RefreshUi(); };
            slider.SetMeta("volume_channel", label); _volumes.Add((slider, amount, label));
        }
        Volume("Music", "star", GameState.Instance.MusicVolumePercent, GameState.Instance.SetMusicVolumePercent);
        Volume("Effects", "flame", GameState.Instance.EffectsVolumePercent, GameState.Instance.SetEffectsVolumePercent);
        Volume("Ambience", "mountain", GameState.Instance.AmbienceVolumePercent, GameState.Instance.SetAmbienceVolumePercent);

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
        pages[1].AddChild(interfacePanel);

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

        var callsignButton = new RealmButton
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

        var interfaceRow = new GridContainer { Columns = 2 };
        interfaceRow.AddThemeConstantOverride("separation", 8);
        interfaceStack.AddChild(interfaceRow);

        _showFpsButton = BuildCompactButton("Toggle FPS Counter", () =>
        {
            GameState.Instance.SetShowFpsCounter(!GameState.Instance.ShowFpsCounter);
            RefreshUi();
        });
        interfaceRow.AddChild(_showFpsButton);

        _showHintsButton = BuildCompactButton("Toggle Hints", () =>
        {
            GameState.Instance.SetShowHints(!GameState.Instance.ShowHints);
            RefreshUi();
        });
        interfaceRow.AddChild(_showHintsButton);

        Button motionButton = null!;
        motionButton = BuildCompactButton(GameState.Instance.ReducedMotion ? "Motion reduced" : "Full motion", () => {
            GameState.Instance.SetReducedMotion(!GameState.Instance.ReducedMotion);
            motionButton.Text = GameState.Instance.ReducedMotion ? "Motion reduced" : "Full motion";
        });
        interfaceRow.AddChild(motionButton);
        var langButton = BuildCompactButton("Language", () =>
        {
            var supported = Locale.GetSupportedLanguages();
            var currentIndex = 0;
            for (var li = 0; li < supported.Length; li++)
            {
                if (supported[li] == GameState.Instance.Language)
                {
                    currentIndex = li;
                    break;
                }
            }
            var nextIndex = (currentIndex + 1) % supported.Length;
            GameState.Instance.SetLanguage(supported[nextIndex]);
            RefreshUi();
        });
        interfaceRow.AddChild(langButton);

        var accessRow = new GridContainer { Columns = 2 };
        accessRow.AddThemeConstantOverride("separation", 8);
        interfaceStack.AddChild(accessRow);

        accessRow.AddChild(BuildCompactButton("Font -", () =>
        {
            GameState.Instance.SetFontSizeOffset(GameState.Instance.FontSizeOffset - 2);
            RefreshUi();
        }));
        accessRow.AddChild(BuildCompactButton("Font +", () =>
        {
            GameState.Instance.SetFontSizeOffset(GameState.Instance.FontSizeOffset + 2);
            RefreshUi();
        }));
        accessRow.AddChild(BuildCompactButton("High Contrast", () =>
        {
            GameState.Instance.SetHighContrast(!GameState.Instance.HighContrast);
            RefreshUi();
        }));

        var difficultyPanel = new PanelContainer();
        pages[1].AddChild(difficultyPanel);

        var difficultyPadding = new MarginContainer();
        difficultyPadding.AddThemeConstantOverride("margin_left", 14);
        difficultyPadding.AddThemeConstantOverride("margin_top", 14);
        difficultyPadding.AddThemeConstantOverride("margin_right", 14);
        difficultyPadding.AddThemeConstantOverride("margin_bottom", 14);
        difficultyPanel.AddChild(difficultyPadding);

        var difficultyStack = new VBoxContainer();
        difficultyStack.AddThemeConstantOverride("separation", 10);
        difficultyPadding.AddChild(difficultyStack);

        difficultyStack.AddChild(new Label
        {
            Text = "Difficulty"
        });

        _difficultyLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        difficultyStack.AddChild(_difficultyLabel);

        _difficultyButton = BuildCompactButton("Next Difficulty", () =>
        {
            var all = DifficultyCatalog.GetAll();
            var currentIndex = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Id == GameState.Instance.DifficultyId)
                {
                    currentIndex = i;
                    break;
                }
            }
            var nextIndex = (currentIndex + 1) % all.Count;
            GameState.Instance.SetDifficulty(all[nextIndex].Id);
            RefreshUi();
        });
        difficultyStack.AddChild(_difficultyButton);

        var syncPanel = new PanelContainer();
        pages[2].AddChild(syncPanel);

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
        syncStack.AddChild(_lifecycleLabel); _lifecycleLabel.Visible = !embedded;

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
		_syncProviderButton.Disabled = GameState.Instance.IsReleaseBackendConfigured;

        _syncAutoFlushButton = BuildCompactButton("Toggle Auto Flush", () =>
        {
            GameState.Instance.SetChallengeSyncAutoFlush(!GameState.Instance.ChallengeSyncAutoFlush);
            RefreshUi();
        });
        providerRow.AddChild(_syncAutoFlushButton);
		_syncAutoFlushButton.Disabled = GameState.Instance.IsReleaseBackendConfigured;

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
		_syncEndpointEdit.Editable = !GameState.Instance.IsReleaseBackendConfigured;

        var endpointButton = new RealmButton
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
        if (embedded) {
            endpointRow.Hide(); providerRow.Hide();
            syncStack.AddChild(RealmUi.Button("gear", "Connection details", () => { endpointRow.Visible = !endpointRow.Visible; providerRow.Visible = endpointRow.Visible; }));
            syncStack.AddChild(RealmUi.Button("people", "Refresh profile", () => { PlayerProfileSyncService.RefreshProfile(out _); RefreshUi(); }));
        }
		endpointButton.Disabled = GameState.Instance.IsReleaseBackendConfigured;

        var defaultsButton = new RealmButton
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
            GameState.Instance.SetMusicVolumePercent(50);
            GameState.Instance.SetLanguage("en");
            GameState.Instance.SetFontSizeOffset(0);
            GameState.Instance.SetHighContrast(false);
            GameState.Instance.SetShowDevUi(true);
            GameState.Instance.SetDeveloperMode(false);
            GameState.Instance.SetShowFpsCounter(true);
            GameState.Instance.SetChallengeSyncProvider(ChallengeSyncProviderCatalog.LocalJournalId);
            GameState.Instance.SetChallengeSyncEndpoint("");
            GameState.Instance.SetChallengeSyncAutoFlush(false);
            GameState.Instance.SetDifficulty(DifficultyCatalog.NormalId);
            GameState.Instance.SetShowHints(true);
            GameState.Instance.SetPurchaseValidationEndpoint("");
            RefreshUi();
        };
        stack.AddChild(defaultsButton);

        if (embedded) pages[3].AddChild(RealmUi.Button("people", "Manage account", () => AccountDialog.Show(this)));
        pages[3].AddChild(RealmUi.Button("close", "Reset campaign", () => MedievalUi.ShowConfirmation(this,
            "Abandon this campaign?", "Erase this local campaign and return to the first march. This cannot be undone.", "Reset campaign",
            () => { GameState.Instance.ResetProgress(); SceneRouter.Instance.ReloadHome(); })));
        var purchasePanel = new PanelContainer();
        pages[3].AddChild(purchasePanel);

        var purchasePadding = new MarginContainer();
        purchasePadding.AddThemeConstantOverride("margin_left", 14);
        purchasePadding.AddThemeConstantOverride("margin_top", 14);
        purchasePadding.AddThemeConstantOverride("margin_right", 14);
        purchasePadding.AddThemeConstantOverride("margin_bottom", 14);
        purchasePanel.AddChild(purchasePadding);

        var purchaseStack = new VBoxContainer();
        purchaseStack.AddThemeConstantOverride("separation", 10);
        purchasePadding.AddChild(purchaseStack);

        purchaseStack.AddChild(new Label
        {
            Text = "Payments"
        });

        _purchaseLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        purchaseStack.AddChild(_purchaseLabel);

        var purchaseEndpointRow = new HBoxContainer();
        purchaseEndpointRow.AddThemeConstantOverride("separation", 8);
        purchaseStack.AddChild(purchaseEndpointRow);

        _purchaseEndpointEdit = new LineEdit
        {
            PlaceholderText = "https://api.example.com",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        purchaseEndpointRow.AddChild(_purchaseEndpointEdit);
		_purchaseEndpointEdit.Editable = !GameState.Instance.IsReleaseBackendConfigured;

        var purchaseEndpointButton = new RealmButton
        {
            Text = "Apply Endpoint",
            CustomMinimumSize = new Vector2(190f, 40f)
        };
        purchaseEndpointButton.Pressed += () =>
        {
            GameState.Instance.SetPurchaseValidationEndpoint(_purchaseEndpointEdit.Text);
            RefreshUi();
        };
        purchaseEndpointRow.AddChild(purchaseEndpointButton);
        if (embedded) { purchaseEndpointRow.Hide(); purchaseStack.AddChild(RealmUi.Button("gear", "Payment connection details", () => purchaseEndpointRow.Visible = !purchaseEndpointRow.Visible)); }
		purchaseEndpointButton.Disabled = GameState.Instance.IsReleaseBackendConfigured;

        _cloudSaveLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        purchaseStack.AddChild(_cloudSaveLabel);

        var cloudSaveRow = new HBoxContainer();
        cloudSaveRow.AddThemeConstantOverride("separation", 8);
        purchaseStack.AddChild(cloudSaveRow);

        var uploadButton = BuildCompactButton("Upload Save", () =>
        {
            CloudSaveService.Upload(out var msg);
            _cloudSaveLabel.Text = msg;
            RefreshUi();
        });
        cloudSaveRow.AddChild(uploadButton);

        var downloadButton = BuildCompactButton("Restore Save", () =>
        {
            var restored = CloudSaveService.Download(out var msg);
            _cloudSaveLabel.Text = msg;
            if (restored && RealmModal.Embedded(this)) { SceneRouter.Instance.ReloadHome(); return; }
            RefreshUi();
        });
        cloudSaveRow.AddChild(downloadButton);

        var cloudInfoButton = BuildCompactButton("Check Cloud", () =>
        {
            var info = CloudSaveService.GetInfo();
            if (info.Status == "ok")
            {
                var when = DateTimeOffset.FromUnixTimeSeconds(info.UploadedAtUnixSeconds).ToLocalTime().ToString("MM-dd HH:mm");
                _cloudSaveLabel.Text = $"Cloud save: v{info.SaveVersion}, {info.SizeBytes / 1024}KB, hash {info.SaveHash}\nUploaded: {when}";
            }
            else
            {
                _cloudSaveLabel.Text = $"Cloud: {info.Message}";
            }
        });
        cloudSaveRow.AddChild(cloudInfoButton);

        purchaseStack.AddChild(new Label
        {
            Text = "Privacy"
        });

        var privacyLabel = new Label
        {
            Text = "Optional analytics sends gameplay events, player ID, game version, and platform to improve balance and difficulty. You can turn it off at any time.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        purchaseStack.AddChild(privacyLabel);

        var analyticsButton = BuildCompactButton(
            GameState.Instance.AnalyticsConsent ? "Disable Analytics" : "Enable Analytics",
            () =>
            {
                GameState.Instance.SetAnalyticsConsent(!GameState.Instance.AnalyticsConsent);
                RefreshUi();
            });
        analyticsButton.Pressed += () => analyticsButton.Text = GameState.Instance.AnalyticsConsent ? "Disable Analytics" : "Enable Analytics";
        purchaseStack.AddChild(analyticsButton);

        purchaseStack.AddChild(new Label
        {
            Text = "Optional crash reports send error messages, technical traces, player ID, game version, platform, and the current screen to help fix bugs. You can turn them off at any time.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        var crashButton = BuildCompactButton(
            GameState.Instance.CrashReportingConsent ? "Disable Crash Reports" : "Enable Crash Reports",
            () => GameState.Instance.SetCrashReportingConsent(!GameState.Instance.CrashReportingConsent));
        crashButton.Pressed += () => crashButton.Text = GameState.Instance.CrashReportingConsent ? "Disable Crash Reports" : "Enable Crash Reports";
        purchaseStack.AddChild(crashButton);

        var achievementsPanel = new PanelContainer();
        pages[3].AddChild(achievementsPanel);

        var achievementsPadding = new MarginContainer();
        achievementsPadding.AddThemeConstantOverride("margin_left", 14);
        achievementsPadding.AddThemeConstantOverride("margin_top", 14);
        achievementsPadding.AddThemeConstantOverride("margin_right", 14);
        achievementsPadding.AddThemeConstantOverride("margin_bottom", 14);
        achievementsPanel.AddChild(achievementsPadding);

        var achievementsStack = new VBoxContainer();
        achievementsStack.AddThemeConstantOverride("separation", 6);
        achievementsPadding.AddChild(achievementsStack);

        achievementsStack.AddChild(new Label
        {
            Text = "Achievements"
        });

        _achievementsLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        achievementsStack.AddChild(_achievementsLabel);

        var bottomRow = new HBoxContainer();
        bottomRow.AddThemeConstantOverride("separation", 12);
        rootStack.AddChild(bottomRow); bottomRow.Visible = !embedded;

        _backButton = new RealmButton
        {
            CustomMinimumSize = new Vector2(220f, 48f)
        };
        _backButton.Pressed += () => SceneRouter.Instance.ReturnFromSettings();
        bottomRow.AddChild(_backButton);

        _titleButton = new RealmButton
        {
            Text = "Back To Title",
            CustomMinimumSize = new Vector2(180f, 48f)
        };
        _titleButton.Pressed += () => SceneRouter.Instance.GoToMainMenu();
        bottomRow.AddChild(_titleButton);
        if (embedded) RealmModal.Polish(rootStack);
    }

    private static Button BuildCompactButton(string text, System.Action onPressed)
    {
        var button = new RealmButton
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
        RefreshDeveloperPage();
        foreach (var (slider, amount, channel) in _volumes) {
            int value = channel == "Music" ? GameState.Instance.MusicVolumePercent : channel == "Effects" ? GameState.Instance.EffectsVolumePercent : GameState.Instance.AmbienceVolumePercent;
            slider.SetValueNoSignal(value); amount.Text = value + "%";
        }
        _returnLabel.Text = $"Return target: {SceneRouter.Instance.SettingsReturnLabel}";
        _audioLabel.Text =
            $"Effects: {GameState.Instance.EffectsVolumePercent}%  |  Ambience: {GameState.Instance.AmbienceVolumePercent}%  |  Music: {GameState.Instance.MusicVolumePercent}%\n" +
            $"Muted: {(GameState.Instance.AudioMuted ? "Yes" : "No")}";
        _interfaceLabel.Text =
            $"FPS counter: {(GameState.Instance.ShowFpsCounter ? "Shown" : "Hidden")}\n" +
            $"Tutorial hints: {(GameState.Instance.ShowHints ? "Shown" : "Hidden")}\n" +
            $"Language: {GameState.Instance.Language}\n" +
            $"Font size: {16 + GameState.Instance.FontSizeOffset}px  |  High contrast: {(GameState.Instance.HighContrast ? "On" : "Off")}";
        _callsignLabel.Text = $"Caravan callsign: {GameState.Instance.PlayerCallsign}\nUsed for LAN room labels and shared scoreboards.";
        var currentDiff = GameState.Instance.GetDifficulty();
        _difficultyLabel.Text =
            $"Current: {currentDiff.Title} ({currentDiff.Id})\n{currentDiff.Description}";
        _difficultyButton.Text = $"Difficulty: {currentDiff.Title}";
        _syncLabel.Text =
            $"Profile: {GameState.Instance.PlayerProfileId}\n" +
            $"Auth token: {(string.IsNullOrWhiteSpace(GameState.Instance.PlayerAuthToken) ? "none" : "active")}\n" +
            $"Last profile sync: {(GameState.Instance.LastPlayerProfileSyncAtUnixSeconds <= 0 ? "never" : System.DateTimeOffset.FromUnixTimeSeconds(GameState.Instance.LastPlayerProfileSyncAtUnixSeconds).ToLocalTime().ToString("MM-dd HH:mm:ss"))}\n" +
            $"Provider: {ChallengeSyncProviderCatalog.GetDisplayName(GameState.Instance.ChallengeSyncProviderId)}\n" +
            $"Auto flush: {(GameState.Instance.ChallengeSyncAutoFlush ? "On" : "Off")}\n" +
            $"Endpoint: {(string.IsNullOrWhiteSpace(GameState.Instance.ChallengeSyncEndpoint) ? "not set" : GameState.Instance.ChallengeSyncEndpoint)}{(GameState.Instance.IsReleaseBackendConfigured ? " (managed by release)" : "")}\n\n" +
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
        _showFpsButton.Text = GameState.Instance.ShowFpsCounter ? "Hide FPS Counter" : "Show FPS Counter";
        _showHintsButton.Text = GameState.Instance.ShowHints ? "Hide Hints" : "Show Hints";
        _syncProviderButton.Text = GameState.Instance.ChallengeSyncProviderId == ChallengeSyncProviderCatalog.HttpApiId
            ? "Use Local Stub"
            : "Use HTTP API";
        _syncAutoFlushButton.Text = GameState.Instance.ChallengeSyncAutoFlush
            ? "Disable Auto Flush"
            : "Enable Auto Flush";
        _purchaseLabel.Text =
            $"Purchase endpoint: {(string.IsNullOrWhiteSpace(GameState.Instance.PurchaseValidationEndpoint) ? "not set (local mode)" : GameState.Instance.PurchaseValidationEndpoint)}{(GameState.Instance.IsReleaseBackendConfigured ? " (managed by release)" : "")}\n" +
            $"Total purchases: {GameState.Instance.TotalPurchaseCount}\n" +
            $"Platform: {DetectPurchasePlatform()}";
        if (!_purchaseEndpointEdit.HasFocus())
        {
            _purchaseEndpointEdit.Text = GameState.Instance.PurchaseValidationEndpoint;
        }
        var returnLabel = SceneRouter.Instance.SettingsReturnLabel;
        _backButton.Text = $"Back To {returnLabel}";
        _titleButton.Visible = !returnLabel.Equals("Title", StringComparison.OrdinalIgnoreCase);
        if (RealmModal.Embedded(this)) {
            _interfaceLabel.Text = "Adjust readability and hints.";
            _callsignLabel.Text = "Caravan name · used in rooms and shared rankings";
            _syncLabel.Text = string.IsNullOrEmpty(GameState.Instance.AccountProvider) ? "Playing locally. Sign in from Account to connect your caravan." : $"Connected with {GameState.Instance.AccountProvider}. Refresh your profile to check the latest progress.";
            _purchaseLabel.Text = $"Purchases completed: {GameState.Instance.TotalPurchaseCount}\nPayments: {DetectPurchasePlatform()}";
        }
        _achievementsLabel.Text = $"{GameState.Instance.GetUnlockedAchievementCount()}/{AchievementCatalog.GetAll().Count} completed. Open Achievements from the home dock to view objectives and claim rewards.";
    }

    private static string DetectPurchasePlatform()
    {
        if (OS.HasFeature("ios")) return "Apple (StoreKit 2)";
        if (OS.HasFeature("android")) return "Google Play Billing";
        return "Stripe Checkout (web/PC)";
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
