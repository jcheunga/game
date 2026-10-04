using System;
using System.Collections.Generic;
using Godot;

public partial class SettingsMenu : Control
{
    private readonly List<(HSlider Slider, Label Amount, string Channel)> _volumes = new();
    private Label _syncLabel = null!;
    private Label _lifecycleLabel = null!;
    private Label _returnLabel = null!;
    private Label _purchaseLabel = null!;
    private Label _cloudSaveLabel = null!;
    private LineEdit _purchaseEndpointEdit = null!;
    private Button _syncProviderButton = null!;
    private Button _syncAutoFlushButton = null!;
    private Button _backButton = null!;
    private Button _titleButton = null!;
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

            _returnLabel.Text = $"{hint.Title}: {hint.Body}";
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
        // Sound
        var audioStack = Section(pages[0], "Volume");
        void Volume(string label, int initial, Action<int> apply)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 16); audioStack.AddChild(row);
            var name = RealmUi.Label(label, 18); name.CustomMinimumSize = new Vector2(120, 0); name.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; name.VerticalAlignment = VerticalAlignment.Center; row.AddChild(name);
            var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 1, Value = initial, CustomMinimumSize = new Vector2(0, 44), SizeFlagsHorizontal = SizeFlags.ExpandFill, AccessibilityName = label + " volume" }; row.AddChild(slider);
            var amount = RealmUi.Label(initial + "%", 18); amount.CustomMinimumSize = new Vector2(56, 0); amount.SizeFlagsHorizontal = SizeFlags.ShrinkEnd; amount.HorizontalAlignment = HorizontalAlignment.Right; amount.VerticalAlignment = VerticalAlignment.Center; row.AddChild(amount);
            slider.ValueChanged += value => { apply((int)value); amount.Text = $"{value:0}%"; RefreshUi(); };
            slider.SetMeta("volume_channel", label); _volumes.Add((slider, amount, label));
        }
        Volume("Music", GameState.Instance.MusicVolumePercent, GameState.Instance.SetMusicVolumePercent);
        Volume("Effects", GameState.Instance.EffectsVolumePercent, GameState.Instance.SetEffectsVolumePercent);
        Volume("Ambience", GameState.Instance.AmbienceVolumePercent, GameState.Instance.SetAmbienceVolumePercent);
        Toggle(audioStack, "Mute all sound", () => GameState.Instance.AudioMuted, muted =>
        {
            GameState.Instance.SetAudioMuted(muted);
            if (!muted) AudioDirector.Instance?.PlayUiConfirm();
        });

        var defaultsButton = new RealmButton
        {
            Text = "Restore defaults",
            CustomMinimumSize = new Vector2(220f, 48f),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd
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
        pages[0].AddChild(defaultsButton);

        // Gameplay
        var interfaceStack = Section(pages[1], "Interface");
        var callsignRow = new HBoxContainer();
        callsignRow.AddThemeConstantOverride("separation", 12);
        interfaceStack.AddChild(callsignRow);
        var callsignName = RealmUi.Label("Caravan name", 18); callsignName.CustomMinimumSize = new Vector2(150, 0);
        callsignName.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; callsignName.VerticalAlignment = VerticalAlignment.Center;
        callsignRow.AddChild(callsignName);
        _callsignEdit = new LineEdit
        {
            PlaceholderText = "Lantern",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Shown in rooms and shared rankings"
        };
        callsignRow.AddChild(_callsignEdit);
        var callsignButton = new RealmButton
        {
            Text = "Save",
            CustomMinimumSize = new Vector2(120f, 44f)
        };
        callsignButton.Pressed += () =>
        {
            GameState.Instance.SetPlayerCallsign(_callsignEdit.Text);
            RefreshUi();
        };
        callsignRow.AddChild(callsignButton);

        Toggle(interfaceStack, "Tutorial hints", () => GameState.Instance.ShowHints, GameState.Instance.SetShowHints);
        Toggle(interfaceStack, "Reduced motion", () => GameState.Instance.ReducedMotion, GameState.Instance.SetReducedMotion);
        Toggle(interfaceStack, "High contrast", () => GameState.Instance.HighContrast, GameState.Instance.SetHighContrast);
        Toggle(interfaceStack, "FPS counter", () => GameState.Instance.ShowFpsCounter, GameState.Instance.SetShowFpsCounter);
        Choice(interfaceStack, "Language", () => GameState.Instance.Language.ToUpperInvariant(), () =>
        {
            var supported = Locale.GetSupportedLanguages();
            var currentIndex = Math.Max(0, Array.IndexOf(supported, GameState.Instance.Language));
            GameState.Instance.SetLanguage(supported[(currentIndex + 1) % supported.Length]);
        });
        Stepper(interfaceStack, "Text size", () => $"{16 + GameState.Instance.FontSizeOffset} px",
            () => GameState.Instance.SetFontSizeOffset(GameState.Instance.FontSizeOffset - 2),
            () => GameState.Instance.SetFontSizeOffset(GameState.Instance.FontSizeOffset + 2));

        var difficultyStack = Section(pages[1], "Difficulty");
        Choice(difficultyStack, "Challenge level", () => GameState.Instance.GetDifficulty().Title, () =>
        {
            var all = DifficultyCatalog.GetAll();
            var currentIndex = 0;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Id == GameState.Instance.DifficultyId) { currentIndex = i; break; }
            GameState.Instance.SetDifficulty(all[(currentIndex + 1) % all.Count].Id);
        });
        _difficultyLabel = RealmUi.Label("", 18, true);
        difficultyStack.AddChild(_difficultyLabel);

        // Online
        var syncStack = Section(pages[2], "Online play");
        _syncLabel = RealmUi.Label("", 18, true);
        syncStack.AddChild(_syncLabel);
        _lifecycleLabel = RealmUi.Label("", 18, true);
        syncStack.AddChild(_lifecycleLabel); _lifecycleLabel.Visible = !embedded;

        var providerRow = new HBoxContainer();
        providerRow.AddThemeConstantOverride("separation", 8);
        syncStack.AddChild(providerRow);
        _syncProviderButton = BuildCompactButton("Switch provider", () =>
        {
            var nextProviderId = GameState.Instance.ChallengeSyncProviderId == ChallengeSyncProviderCatalog.HttpApiId
                ? ChallengeSyncProviderCatalog.LocalJournalId
                : ChallengeSyncProviderCatalog.HttpApiId;
            GameState.Instance.SetChallengeSyncProvider(nextProviderId);
            RefreshUi();
        });
        providerRow.AddChild(_syncProviderButton);
		_syncProviderButton.Disabled = GameState.Instance.IsReleaseBackendConfigured;
        _syncAutoFlushButton = BuildCompactButton("Auto flush", () =>
        {
            GameState.Instance.SetChallengeSyncAutoFlush(!GameState.Instance.ChallengeSyncAutoFlush);
            RefreshUi();
        });
        providerRow.AddChild(_syncAutoFlushButton);
		_syncAutoFlushButton.Disabled = GameState.Instance.IsReleaseBackendConfigured;

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
            Text = "Apply",
            CustomMinimumSize = new Vector2(120f, 44f)
        };
        endpointButton.Pressed += () =>
        {
            GameState.Instance.SetChallengeSyncEndpoint(_syncEndpointEdit.Text);
            RefreshUi();
        };
        endpointRow.AddChild(endpointButton);
		endpointButton.Disabled = GameState.Instance.IsReleaseBackendConfigured;
        endpointRow.Visible = providerRow.Visible = !embedded;
        var onlineActions = ActionRow(syncStack);
        onlineActions.AddChild(Grow(RealmUi.Button("people", "Refresh profile", () => { PlayerProfileSyncService.RefreshProfile(out _); RefreshUi(); })));
        onlineActions.AddChild(Grow(RealmUi.Button("gear", "Connection details", () => { endpointRow.Visible = !endpointRow.Visible; providerRow.Visible = endpointRow.Visible; })));

        // Account
        var accountActions = ActionRow(pages[3]);
        accountActions.AddChild(Grow(RealmUi.Button("people", "Manage account", () => AccountDialog.Show(this))));
        accountActions.AddChild(Grow(RealmUi.Button("close", "Reset campaign", () => MedievalUi.ShowConfirmation(this,
            "Abandon this campaign?", "Erase this local campaign and return to the first march. This cannot be undone.", "Reset campaign",
            () => { GameState.Instance.ResetProgress(); SceneRouter.Instance.ReloadHome(); }))));

        var cloudStack = Section(pages[3], "Cloud save");
        _cloudSaveLabel = RealmUi.Label("", 18, true);
        cloudStack.AddChild(_cloudSaveLabel);
        var cloudSaveRow = ActionRow(cloudStack);
        cloudSaveRow.AddChild(BuildCompactButton("Upload", () =>
        {
            CloudSaveService.Upload(out var msg);
            _cloudSaveLabel.Text = msg;
            RefreshUi();
        }));
        cloudSaveRow.AddChild(BuildCompactButton("Restore", () =>
        {
            var restored = CloudSaveService.Download(out var msg);
            _cloudSaveLabel.Text = msg;
            if (restored && RealmModal.Embedded(this)) { SceneRouter.Instance.ReloadHome(); return; }
            RefreshUi();
        }));
        cloudSaveRow.AddChild(BuildCompactButton("Check status", () =>
        {
            var info = CloudSaveService.GetInfo();
            if (info.Status == "ok")
            {
                var when = DateTimeOffset.FromUnixTimeSeconds(info.UploadedAtUnixSeconds).ToLocalTime().ToString("MM-dd HH:mm");
                _cloudSaveLabel.Text = $"Saved {when} · version {info.SaveVersion} · {info.SizeBytes / 1024} KB";
            }
            else
            {
                _cloudSaveLabel.Text = info.Message;
            }
            RefreshUi();
        }));

        // Each optional upload keeps its own switch and a plain list of what it sends.
        var privacyStack = Section(pages[3], "Privacy");
        Toggle(privacyStack, "Share analytics", () => GameState.Instance.AnalyticsConsent, GameState.Instance.SetAnalyticsConsent);
        privacyStack.AddChild(RealmUi.Label("Gameplay events, player ID, game version and platform. Used to tune balance and difficulty.", 18, true));
        Toggle(privacyStack, "Send crash reports", () => GameState.Instance.CrashReportingConsent, GameState.Instance.SetCrashReportingConsent);
        privacyStack.AddChild(RealmUi.Label("Error messages, technical traces, player ID, game version, platform and current screen. Used to fix bugs.", 18, true));

        var purchaseStack = Section(pages[3], "Payments");
        _purchaseLabel = RealmUi.Label("", 18, true);
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
            Text = "Apply",
            CustomMinimumSize = new Vector2(120f, 44f)
        };
        purchaseEndpointButton.Pressed += () =>
        {
            GameState.Instance.SetPurchaseValidationEndpoint(_purchaseEndpointEdit.Text);
            RefreshUi();
        };
        purchaseEndpointRow.AddChild(purchaseEndpointButton);
		purchaseEndpointButton.Disabled = GameState.Instance.IsReleaseBackendConfigured;
        if (embedded)
        {
            purchaseEndpointRow.Hide();
            var details = RealmUi.Button("gear", "Connection details", () => purchaseEndpointRow.Visible = !purchaseEndpointRow.Visible);
            details.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            purchaseStack.AddChild(details);
        }

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
            Text = "Back to title",
            CustomMinimumSize = new Vector2(180f, 48f)
        };
        _titleButton.Pressed += () => SceneRouter.Instance.GoToMainMenu();
        bottomRow.AddChild(_titleButton);
        if (embedded) RealmModal.Polish(rootStack);
    }

    private readonly List<Action> _rowSyncs = new();

    private static VBoxContainer Section(VBoxContainer page, string title)
    {
        var panel = new PanelContainer();
        page.AddChild(panel);
        var padding = new MarginContainer();
        foreach (var side in new[] { "left", "top", "right", "bottom" }) padding.AddThemeConstantOverride("margin_" + side, 14);
        panel.AddChild(padding);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 10);
        padding.AddChild(stack);
        stack.AddChild(RealmUi.SectionTitle(title));
        return stack;
    }

    private static HBoxContainer ActionRow(Control host)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        host.AddChild(row);
        return row;
    }

    private static Button Grow(Button button)
    {
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return button;
    }

    // A setting name on the left and its control, sized consistently, on the right.
    private static T SettingRow<T>(VBoxContainer host, string name, T control) where T : Control
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        host.AddChild(row);
        var label = RealmUi.Label(name, 18);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(label);
        control.CustomMinimumSize = new Vector2(Math.Max(control.CustomMinimumSize.X, 168), 44);
        control.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        row.AddChild(control);
        return control;
    }

    private Button Toggle(VBoxContainer host, string name, Func<bool> value, Action<bool> apply)
    {
        var button = new RealmButton { ToggleMode = true, AccessibilityName = name, TooltipText = name, MouseDefaultCursorShape = CursorShape.PointingHand };
        button.SetMeta("realm_toggle", true);
        void Sync() { button.SetPressedNoSignal(value()); button.Text = value() ? "On" : "Off"; }
        button.Pressed += () => { apply(!value()); RefreshUi(); };
        _rowSyncs.Add(Sync); Sync();
        return SettingRow(host, name, button);
    }

    private Button Choice(VBoxContainer host, string name, Func<string> value, Action next)
    {
        var button = new RealmButton { AccessibilityName = name, TooltipText = $"Change {name.ToLowerInvariant()}", MouseDefaultCursorShape = CursorShape.PointingHand };
        void Sync() => button.Text = value();
        button.Pressed += () => { next(); RefreshUi(); };
        _rowSyncs.Add(Sync); Sync();
        return SettingRow(host, name, button);
    }

    private void Stepper(VBoxContainer host, string name, Func<string> value, Action decrease, Action increase)
    {
        var group = new HBoxContainer();
        group.AddThemeConstantOverride("separation", 8);
        var less = RealmUi.IconButton("minus", $"Smaller {name.ToLowerInvariant()}", () => { decrease(); RefreshUi(); });
        var amount = RealmUi.Label("", 18); amount.CustomMinimumSize = new Vector2(64, 0); amount.AutowrapMode = TextServer.AutowrapMode.Off;
        amount.HorizontalAlignment = HorizontalAlignment.Center; amount.VerticalAlignment = VerticalAlignment.Center;
        var more = RealmUi.IconButton("plus", $"Larger {name.ToLowerInvariant()}", () => { increase(); RefreshUi(); });
        group.AddChild(less); group.AddChild(amount); group.AddChild(more);
        _rowSyncs.Add(() => amount.Text = value());
        SettingRow(host, name, group);
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
        foreach (var sync in _rowSyncs) sync();
        _returnLabel.Text = $"Return target: {SceneRouter.Instance.SettingsReturnLabel}";
        _difficultyLabel.Text = GameState.Instance.GetDifficulty().Description;
        _syncLabel.Text = string.IsNullOrEmpty(GameState.Instance.AccountProvider)
            ? "Playing locally. Sign in from the Account tab to connect your caravan."
            : $"Connected with {GameState.Instance.AccountProvider}.";
        _lifecycleLabel.Text = AppLifecycleService.Instance?.BuildStatusSummary() ?? "";
        if (!_callsignEdit.HasFocus())
        {
            _callsignEdit.Text = GameState.Instance.PlayerCallsign;
        }
        if (!_syncEndpointEdit.HasFocus())
        {
            _syncEndpointEdit.Text = GameState.Instance.ChallengeSyncEndpoint;
        }
        _syncProviderButton.Text = GameState.Instance.ChallengeSyncProviderId == ChallengeSyncProviderCatalog.HttpApiId
            ? "Use local journal"
            : "Use HTTP API";
        _syncAutoFlushButton.Text = GameState.Instance.ChallengeSyncAutoFlush ? "Auto flush: On" : "Auto flush: Off";
        _purchaseLabel.Text = $"{GameState.Instance.TotalPurchaseCount} purchases · {DetectPurchasePlatform()}";
        _cloudSaveLabel.Visible = _cloudSaveLabel.Text.Length > 0;
        if (!_purchaseEndpointEdit.HasFocus())
        {
            _purchaseEndpointEdit.Text = GameState.Instance.PurchaseValidationEndpoint;
        }
        var returnLabel = SceneRouter.Instance.SettingsReturnLabel;
        _backButton.Text = $"Back to {returnLabel.ToLowerInvariant()}";
        _titleButton.Visible = !returnLabel.Equals("Title", StringComparison.OrdinalIgnoreCase);
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
