using System;
using Godot;

public partial class SettingsMenu : Control
{
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
    private Button _showDevUiButton = null!;
    private Button _showFpsButton = null!;
    private Button _showHintsButton = null!;
    private Button _syncProviderButton = null!;
    private Button _syncAutoFlushButton = null!;
    private Button _backButton = null!;
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
        RefreshUi();
        TryShowMenuHint();
        AnimateEntrance();
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

        var musicRow = new HBoxContainer();
        musicRow.AddThemeConstantOverride("separation", 8);
        audioStack.AddChild(musicRow);

        musicRow.AddChild(BuildCompactButton("Music -", () =>
        {
            GameState.Instance.SetMusicVolumePercent(GameState.Instance.MusicVolumePercent - 10);
            RefreshUi();
        }));
        musicRow.AddChild(BuildCompactButton("Music +", () =>
        {
            GameState.Instance.SetMusicVolumePercent(GameState.Instance.MusicVolumePercent + 10);
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

        _showHintsButton = BuildCompactButton("Toggle Hints", () =>
        {
            GameState.Instance.SetShowHints(!GameState.Instance.ShowHints);
            RefreshUi();
        });
        interfaceRow.AddChild(_showHintsButton);

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

        var accessRow = new HBoxContainer();
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
        stack.AddChild(difficultyPanel);

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
            GameState.Instance.SetMusicVolumePercent(50);
            GameState.Instance.SetLanguage("en");
            GameState.Instance.SetFontSizeOffset(0);
            GameState.Instance.SetHighContrast(false);
            GameState.Instance.SetShowDevUi(true);
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

        var purchasePanel = new PanelContainer();
        stack.AddChild(purchasePanel);

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

        var purchaseEndpointButton = new Button
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
            CloudSaveService.Download(out var msg);
            _cloudSaveLabel.Text = msg;
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
            Text = $"Analytics: {(GameState.Instance.AnalyticsConsent ? "Enabled" : "Disabled")}\nAnonymous gameplay data helps improve balance and difficulty.",
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
        purchaseStack.AddChild(analyticsButton);

        var achievementsPanel = new PanelContainer();
        stack.AddChild(achievementsPanel);

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
            $"Effects: {GameState.Instance.EffectsVolumePercent}%  |  Ambience: {GameState.Instance.AmbienceVolumePercent}%  |  Music: {GameState.Instance.MusicVolumePercent}%\n" +
            $"Muted: {(GameState.Instance.AudioMuted ? "Yes" : "No")}";
        _interfaceLabel.Text =
            $"Combat intel panels: {(GameState.Instance.ShowDevUi ? "Shown" : "Hidden")}\n" +
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
        _showHintsButton.Text = GameState.Instance.ShowHints ? "Hide Hints" : "Show Hints";
        _syncProviderButton.Text = GameState.Instance.ChallengeSyncProviderId == ChallengeSyncProviderCatalog.HttpApiId
            ? "Use Local Stub"
            : "Use HTTP API";
        _syncAutoFlushButton.Text = GameState.Instance.ChallengeSyncAutoFlush
            ? "Disable Auto Flush"
            : "Enable Auto Flush";
        _purchaseLabel.Text =
            $"Purchase endpoint: {(string.IsNullOrWhiteSpace(GameState.Instance.PurchaseValidationEndpoint) ? "not set (local mode)" : GameState.Instance.PurchaseValidationEndpoint)}\n" +
            $"Total purchases: {GameState.Instance.TotalPurchaseCount}\n" +
            $"Platform: {DetectPurchasePlatform()}";
        if (!_purchaseEndpointEdit.HasFocus())
        {
            _purchaseEndpointEdit.Text = GameState.Instance.PurchaseValidationEndpoint;
        }
        _backButton.Text = $"Back To {SceneRouter.Instance.SettingsReturnLabel}";
        _achievementsLabel.Text = BuildAchievementsText();
    }

    private static string BuildAchievementsText()
    {
        var all = AchievementCatalog.GetAll();
        var unlocked = GameState.Instance.GetUnlockedAchievementCount();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{unlocked}/{all.Count} unlocked");
        foreach (var achievement in all)
        {
            var done = GameState.Instance.IsAchievementUnlocked(achievement.Id);
            var marker = done ? "[x]" : "[ ]";
            sb.AppendLine($"{marker} {achievement.Title} - {achievement.Description}");
        }
        return sb.ToString().TrimEnd();
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
