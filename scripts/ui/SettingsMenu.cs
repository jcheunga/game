using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Settings on the approved concept: four tabs and rows of icon box, name and control (slider,
/// switch, choice or action), with Restore defaults and Back to map underneath.
/// </summary>
public partial class SettingsMenu : RoyalScreen
{
    private int _tab;
    private Control _layer;
    private string _status = "";
    private float _rowsScroll;

    public SettingsMenu() { PlateName = "settings"; }

    private static RoyalSpec Spec => RoyalSpec.For("settings");
    private string[] Tabs => HasMeta("battle_modal") ? new[] { "Sound", "Gameplay" } : new[] { "Sound", "Gameplay", "Online", "Account" };

    protected override void Build()
    {
        _layer = Layer("Live");
        if (AppLifecycleService.Instance != null) AppLifecycleService.Instance.StateChanged += OnAppLifecycleStateChanged;
        GameState.Instance.DeveloperStateChanged += Rebuild;
        Rebuild();
        TryShowMenuHint();
    }

    public override void _ExitTree()
    {
        if (GameState.Instance != null) GameState.Instance.DeveloperStateChanged -= Rebuild;
        if (AppLifecycleService.Instance != null) AppLifecycleService.Instance.StateChanged -= OnAppLifecycleStateChanged;
    }

    private void OnAppLifecycleStateChanged() { if (IsInsideTree()) Rebuild(); }

    private void TryShowMenuHint()
    {
        if (!GameState.Instance.ShowHints) return;
        foreach (var hint in TutorialHintCatalog.GetByContext("first_settings"))
        {
            if (GameState.Instance.HasSeenHint(hint.Id)) continue;
            RoyalToast.Show(this, $"{hint.Title}: {hint.Body}");
            GameState.Instance.MarkHintSeen(hint.Id);
        }
    }

    private void Rebuild()
    {
        if (_layer == null) return;
        RoyalUiTools.Clear(_layer);
        var spec = Spec;
        _layer.AddChild(spec.Label("title", "Settings", 600));
        _layer.AddChild(RoyalButton.Over(spec.Rect("close"), "Close panel", Leave, 6));
        var keys = new[] { "sound", "gameplay", "online", "account" };
        var tabs = Tabs;
        for (var i = 0; i < keys.Length; i++)
        {
            var rect = spec.Rect("tab." + keys[i]);
            if (i >= tabs.Length) { _layer.AddChild(EmptyTabVeil(rect)); continue; }
            var index = i;
            var tab = RoyalButton.Over(rect, tabs[i], () => { _tab = index; _rowsScroll = 0; Rebuild(); }, 5);
            if (i == _tab) tab.SetStates(RoyalKit.Slice("settings-tab-selected", 14), 5);
            tab.MarkTab(i == _tab);
            var icon = spec.Rect($"tab.{keys[i]}.icon");
            tab.SetGlyph(RoyalKit.Texture("settingstab-" + keys[i]), new Rect2(icon.Position - rect.Position, icon.Size));
            var label = spec.Label($"tab.{keys[i]}.label", tabs[i], 160, i == _tab ? new Color("fff6c8") : new Color("e1ddd9"));
            label.Position -= rect.Position;
            tab.SetCaption(label, new Rect2(label.Position, label.Size));
            _layer.AddChild(tab);
        }
        var heading = spec.Label("section.volume", _tab switch { 0 => "Volume", 1 => "Interface", 2 => "Online play", _ => "Account" }, 600);
        _layer.AddChild(heading);
        BuildRows(spec);
        BuildButtons(spec);
    }

    private static Control EmptyTabVeil(Rect2 rect)
    {
        var veil = new Panel { Position = rect.Position + new Vector2(3, 3), Size = rect.Size - new Vector2(6, 6), MouseFilter = MouseFilterEnum.Ignore };
        veil.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(.03f, .04f, .05f, .6f) });
        return veil;
    }

    // ---- Rows ---------------------------------------------------------------------------------

    private abstract record Row(string Icon, string Name);
    private sealed record SliderRow(string Icon, string Name, Func<int> Value, Action<int> Apply) : Row(Icon, Name);
    private sealed record SwitchRow(string Icon, string Name, Func<bool> Value, Action<bool> Apply) : Row(Icon, Name);
    private sealed record ChoiceRow(string Icon, string Name, Func<string> Value, Action Next, string Hint = null) : Row(Icon, Name);
    private sealed record ActionRow(string Icon, string Name, string Button, Action Run, bool Enabled = true) : Row(Icon, Name);
    private sealed record InfoRow(string Icon, string Name, Func<string> Value) : Row(Icon, Name);
    private sealed record EditRow(string Icon, string Name, Func<string> Value, Action<string> Apply, string Placeholder, bool Enabled = true) : Row(Icon, Name);

    private IEnumerable<Row> RowsFor(int tab)
    {
        var state = GameState.Instance;
        switch (tab)
        {
            case 0:
                yield return new SliderRow("settingsicon-music", "Music", () => state.MusicVolumePercent, state.SetMusicVolumePercent);
                yield return new SliderRow("settingsicon-effects", "Effects", () => state.EffectsVolumePercent, state.SetEffectsVolumePercent);
                yield return new SliderRow("settingsicon-ambience", "Ambience", () => state.AmbienceVolumePercent, state.SetAmbienceVolumePercent);
                yield return new SwitchRow("settingsicon-mute", "Mute all sound", () => state.AudioMuted, muted =>
                {
                    // The switch's own latch sound plays (once) after unmuting.
                    state.SetAudioMuted(muted);
                });
                break;
            case 1:
                yield return new EditRow("people", "Caravan name", () => state.PlayerCallsign, state.SetPlayerCallsign, "Lantern");
                yield return new ChoiceRow("sword", "Challenge level", () => state.GetDifficulty().Title, () =>
                {
                    var all = DifficultyCatalog.GetAll();
                    var index = Math.Max(0, all.ToList().FindIndex(d => d.Id == state.DifficultyId));
                    state.SetDifficulty(all[(index + 1) % all.Count].Id);
                }, state.GetDifficulty().Description);
                yield return new SwitchRow("book", "Tutorial hints", () => state.ShowHints, state.SetShowHints);
                yield return new SwitchRow("eye", "Reduced motion", () => state.ReducedMotion, state.SetReducedMotion);
                yield return new SwitchRow("flame", "High contrast", () => state.HighContrast, state.SetHighContrast);
                yield return new SwitchRow("clock", "FPS counter", () => state.ShowFpsCounter, state.SetShowFpsCounter);
                yield return new ChoiceRow("map", "Language", () => state.Language.ToUpperInvariant(), () =>
                {
                    var supported = Locale.GetSupportedLanguages();
                    var index = Math.Max(0, Array.IndexOf(supported, state.Language));
                    state.SetLanguage(supported[(index + 1) % supported.Length]);
                });
                yield return new ChoiceRow("plus", "Text size", () => $"{16 + state.FontSizeOffset} px", () =>
                    state.SetFontSizeOffset(state.FontSizeOffset >= 6 ? -2 : state.FontSizeOffset + 2), "Tap to step through sizes");
                break;
            case 2:
                yield return new InfoRow("people", "Account", () => string.IsNullOrEmpty(state.AccountProvider) ? "Playing locally" : $"Connected · {state.AccountProvider}");
                yield return new ActionRow("people", "Player profile", "Refresh", () => { PlayerProfileSyncService.RefreshProfile(out var message); _status = message; });
                if (!RealmModal.Embedded(this)) yield return new InfoRow("clock", "App state", () => AppLifecycleService.Instance?.BuildStatusSummary() ?? "");
                var release = state.IsReleaseBackendConfigured;
                yield return new ChoiceRow("gear", "Challenge sync", () => state.ChallengeSyncProviderId == ChallengeSyncProviderCatalog.HttpApiId ? "HTTP API" : "Local journal", () =>
                {
                    if (release) return;
                    state.SetChallengeSyncProvider(state.ChallengeSyncProviderId == ChallengeSyncProviderCatalog.HttpApiId ? ChallengeSyncProviderCatalog.LocalJournalId : ChallengeSyncProviderCatalog.HttpApiId);
                }, release ? "Set by this release" : null);
                yield return new SwitchRow("arrow", "Auto flush", () => state.ChallengeSyncAutoFlush, value => { if (!release) state.SetChallengeSyncAutoFlush(value); });
                yield return new EditRow("map", "Sync endpoint", () => state.ChallengeSyncEndpoint, state.SetChallengeSyncEndpoint, "https://api.example.com/challenge-sync", !release);
                break;
            default:
                yield return new ActionRow("people", "Account", "Manage", () => AccountDialog.Show(this));
                yield return new ActionRow("book", "Cloud save", "Upload", () => { CloudSaveService.Upload(out var message); _status = message; });
                yield return new ActionRow("arrow", "Restore cloud save", "Restore", () =>
                {
                    var restored = CloudSaveService.Download(out var message);
                    _status = message;
                    if (restored && RealmModal.Embedded(this)) SceneRouter.Instance.ReloadHome();
                });
                yield return new ActionRow("clock", "Cloud status", "Check", () =>
                {
                    var info = CloudSaveService.GetInfo();
                    _status = info.Status == "ok"
                        ? $"Saved {DateTimeOffset.FromUnixTimeSeconds(info.UploadedAtUnixSeconds).ToLocalTime():MM-dd HH:mm} · version {info.SaveVersion} · {info.SizeBytes / 1024} KB"
                        : info.Message;
                });
                yield return new SwitchRow("eye", "Share analytics", () => state.AnalyticsConsent, state.SetAnalyticsConsent);
                yield return new SwitchRow("flag", "Send crash reports", () => state.CrashReportingConsent, state.SetCrashReportingConsent);
                yield return new InfoRow("gold", "Payments", () => $"{state.TotalPurchaseCount} purchases · {DetectPurchasePlatform()}");
                yield return new EditRow("gold", "Payment endpoint", () => state.PurchaseValidationEndpoint, state.SetPurchaseValidationEndpoint, "https://api.example.com", !state.IsReleaseBackendConfigured);
                if (GameState.DeveloperModeAvailable) yield return new ActionRow("gear", "Developer tools", "Open", OpenDeveloperTools);
                yield return new ActionRow("close", "Reset campaign", "Reset", () => MedievalUi.ShowConfirmation(this, "Abandon this campaign?",
                    "Erase this local campaign and return to the first march. This cannot be undone.", "Reset campaign",
                    () => { GameState.Instance.ResetProgress(); SceneRouter.Instance.ReloadHome(); }));
                break;
        }
    }

    private void BuildRows(RoyalSpec spec)
    {
        var first = spec.Rect("row.music");
        var pitch = spec.Rect("row.effects").Position.Y - first.Position.Y;
        var area = new Rect2(first.Position.X - 2, first.Position.Y - 2, first.Size.X + 6, spec.Rect("button.restore").Position.Y - first.Position.Y - 6);
        var rows = RowsFor(_tab).ToList();
        var scroll = new ScrollContainer { Position = area.Position, Size = area.Size, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever };
        _layer.AddChild(scroll);
        var needed = (rows.Count - 1) * pitch + first.Size.Y + 4;
        var list = new Control { CustomMinimumSize = new Vector2(area.Size.X - 2, Mathf.Max(area.Size.Y, needed)), MouseFilter = MouseFilterEnum.Pass };
        scroll.AddChild(list);
        for (var i = 0; i < rows.Count; i++)
            list.AddChild(BuildRow(spec, rows[i], new Rect2(2, 2 + i * pitch, first.Size.X, first.Size.Y)));
        if (needed > area.Size.Y + 2)
        {
            var hint = new RoyalScrollHint { Position = new Vector2(area.End.X - 10, area.Position.Y), Size = new Vector2(6, area.Size.Y), Scroll = scroll };
            _layer.AddChild(hint);
        }
        scroll.GetVScrollBar().ValueChanged += value => _rowsScroll = (float)value;
        Callable.From(() => { if (GodotObject.IsInstanceValid(scroll)) scroll.ScrollVertical = (int)_rowsScroll; }).CallDeferred();
        if (_status.Length > 0) { RoyalToast.Show(this, _status, 690); _status = ""; }
    }

    private Control BuildRow(RoyalSpec spec, Row row, Rect2 rect)
    {
        var origin = spec.Rect("row.music").Position;
        Rect2 Local(string id) => new(spec.Rect(id).Position - origin, spec.Rect(id).Size);
        var holder = new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Pass };
        holder.AddThemeStyleboxOverride("panel", RoyalKit.Slice("settings-row", 92, 10, 12, 10));
        var iconBox = Local("row.music.iconbox");
        var icon = row.Icon.StartsWith("settingsicon-") ? RoyalKit.Image(row.Icon, Local("row.music.icon"))
            : RoyalKit.Image(row.Icon, new Rect2(iconBox.GetCenter() - new Vector2(17, 17), new Vector2(34, 34)), new Color("d8b46e"));
        holder.AddChild(icon);
        var name = spec.Label("row.music.label", row.Name, 300);
        name.Position -= origin;
        holder.AddChild(name);
        switch (row)
        {
            case SliderRow slider: AddSlider(spec, holder, slider, Local); break;
            case SwitchRow toggle: AddSwitch(spec, holder, toggle, Local); break;
            case ChoiceRow choice:
            {
                var box = new Rect2(Local("row.music.slider.track").Position.X + 60, Local("row.music.value").Position.Y, Local("row.music.value").End.X - Local("row.music.slider.track").Position.X - 60, Local("row.music.value").Size.Y);
                var button = RoyalButton.Over(box, row.Name, () => { choice.Next(); Rebuild(); }, 4);
                button.SetStates(RoyalKit.Slice("value-box", 8), 4);
                button.TooltipText = choice.Hint ?? $"Change {row.Name.ToLowerInvariant()}";
                var text = RoyalText.Serif(choice.Value(), 20, new Color("fff9ec"));
                text.Align = HorizontalAlignment.Center;
                button.SetCaption(text, new Rect2(8, 0, box.Size.X - 16, box.Size.Y));
                holder.AddChild(button);
                break;
            }
            case ActionRow action:
            {
                var box = Local("row.music.value"); box = new Rect2(box.End.X - 150, box.Position.Y, 150, box.Size.Y);
                var button = RoyalButton.Over(box, row.Name, () => { action.Run(); Rebuild(); }, 4);
                button.SetStates(RoyalKit.Slice("value-box", 8), 4);
                button.Disabled = !action.Enabled;
                var text = RoyalText.Caps(action.Button, 16, new Color("fff3d6"), 700);
                text.Align = HorizontalAlignment.Center;
                button.SetCaption(text, new Rect2(6, 0, box.Size.X - 12, box.Size.Y));
                holder.AddChild(button);
                break;
            }
            case InfoRow info:
            {
                var value = RoyalText.Serif(info.Value(), 18, new Color("d8cfc0"), 500);
                value.Align = HorizontalAlignment.Right;
                value.Position = new Vector2(Local("row.music.slider.track").Position.X - 60, name.Position.Y);
                value.Size = new Vector2(Local("row.music.value").End.X - value.Position.X, name.Size.Y);
                value.Baseline = name.Baseline;
                holder.AddChild(value);
                break;
            }
            case EditRow edit:
            {
                var box = new Rect2(Local("row.music.slider.track").Position.X - 30, Local("row.music.value").Position.Y, Local("row.music.value").End.X - Local("row.music.slider.track").Position.X + 30, Local("row.music.value").Size.Y);
                var field = new LineEdit { Text = edit.Value(), PlaceholderText = edit.Placeholder, Position = box.Position, Size = box.Size, Editable = edit.Enabled,
                    TooltipText = row.Name, AccessibilityName = row.Name };
                field.AddThemeStyleboxOverride("normal", RoyalKit.Slice("value-box", 8));
                field.AddThemeStyleboxOverride("focus", RoyalKit.Slice("value-box", 8));
                field.AddThemeStyleboxOverride("read_only", RoyalKit.Slice("value-box", 8));
                field.AddThemeFontOverride("font", RoyalFonts.Body(500));
                field.AddThemeFontSizeOverride("font_size", 18);
                field.AddThemeColorOverride("font_color", new Color("fff9ec"));
                field.AddThemeConstantOverride("minimum_character_width", 4);
                field.TextSubmitted += value => { edit.Apply(value); Rebuild(); };
                field.FocusExited += () => { if (field.Text != edit.Value()) edit.Apply(field.Text); };
                holder.AddChild(field);
                break;
            }
        }
        return holder;
    }

    private void AddSlider(RoyalSpec spec, Control holder, SliderRow row, Func<string, Rect2> local)
    {
        var track = local("row.music.slider.track");
        holder.AddChild(new Panel { Position = track.Position, Size = track.Size, MouseFilter = MouseFilterEnum.Ignore }.With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("slider-track", 8, 6, 8, 6))));
        var fill = new Panel { Position = local("row.music.slider.fill").Position, MouseFilter = MouseFilterEnum.Ignore };
        fill.AddThemeStyleboxOverride("panel", RoyalKit.Slice("slider-fill", 5, 3, 5, 3));
        holder.AddChild(fill);
        var knobSize = local("row.music.slider.knob").Size;
        var knob = RoyalKit.Image("slider-knob", new Rect2(Vector2.Zero, knobSize));
        holder.AddChild(knob);
        var value = spec.Label("row.music.value.text", "", 90);
        value.Position -= spec.Rect("row.music").Position;
        var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 1, Value = row.Value(), Position = track.Position - new Vector2(10, 10), Size = track.Size + new Vector2(20, 20),
            AccessibilityName = row.Name + " volume", MouseDefaultCursorShape = CursorShape.PointingHand };
        foreach (var style in new[] { "slider", "grabber_area", "grabber_area_highlight" }) slider.AddThemeStyleboxOverride(style, new StyleBoxEmpty());
        var empty = new ImageTexture();
        foreach (var icon in new[] { "grabber", "grabber_highlight", "grabber_disabled" }) slider.AddThemeIconOverride(icon, empty);
        slider.SetMeta("volume_channel", row.Name);
        holder.AddChild(slider);
        holder.AddChild(new Panel { Position = local("row.music.value").Position, Size = local("row.music.value").Size, MouseFilter = MouseFilterEnum.Ignore }
            .With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("value-box", 8))));
        holder.AddChild(value);
        void Show(double amount)
        {
            var width = (track.Size.X - 6) * (float)(amount / 100.0);
            fill.Size = new Vector2(Mathf.Max(0, width), local("row.music.slider.fill").Size.Y);
            fill.Visible = width > 2;
            knob.Position = new Vector2(track.Position.X + 3 + width - knobSize.X / 2, local("row.music.slider.knob").Position.Y);
            value.Text = $"{amount:0}%";
        }
        Show(row.Value());
        slider.ValueChanged += amount => { row.Apply((int)amount); Show(amount); };
    }

    private void AddSwitch(RoyalSpec spec, Control holder, SwitchRow row, Func<string, Rect2> local)
    {
        var rect = local("row.mute.toggle");
        rect.Position = new Vector2(rect.Position.X, (holder.Size.Y - rect.Size.Y) / 2);
        var on = row.Value();
        var button = RoyalButton.Over(rect, row.Name, () => { row.Apply(!row.Value()); Rebuild(); }, (int)(rect.Size.Y / 2));
        button.ToggleMode = true; button.SetPressedNoSignal(on);
        button.SetMeta("realm_toggle", true);
        var track = RoyalKit.Slice("toggle-track", 18, 12, 18, 12);
        button.SetStates(on ? track.Tinted(new Color(.55f, 1.25f, 1.3f)) : track, rect.Size.Y / 2, on ? track.Tinted(new Color(.55f, 1.25f, 1.3f)) : null);
        var knob = local("row.mute.toggle.knob").Size;
        button.SetGlyph(RoyalKit.Texture("toggle-knob"), new Rect2(on ? rect.Size.X - knob.X - 3 : 3, (rect.Size.Y - knob.Y) / 2, knob.X, knob.Y));
        holder.AddChild(button);
        var state = spec.Label("row.mute.state", on ? "ON" : "OFF", 60, on ? new Color("f3dfa6") : null);
        state.Position = new Vector2(rect.End.X + spec.Number("row.mute.state", "pen_x", 1070) - spec.Rect("row.mute.toggle").End.X, state.Position.Y - spec.Rect("row.mute").Position.Y + (holder.Size.Y - spec.Rect("row.mute").Size.Y) / 2);
        holder.AddChild(state);
        button.Text = on ? "On" : "Off";
    }

    private void BuildButtons(RoyalSpec spec)
    {
        var restore = RoyalButton.Over(spec.Rect("button.restore"), "Restore defaults", RestoreDefaults, 6);
        restore.SetGlyph(RoyalKit.Texture("icon-restore"), new Rect2(spec.Rect("button.restore.icon").Position - spec.Rect("button.restore").Position, spec.Rect("button.restore.icon").Size));
        var restoreLabel = spec.Label("button.restore.label", "Restore Defaults", 240);
        restoreLabel.Position -= spec.Rect("button.restore").Position;
        restore.SetCaption(restoreLabel, new Rect2(restoreLabel.Position, restoreLabel.Size));
        _layer.AddChild(restore);
        var returnLabel = SceneRouter.Instance.SettingsReturnLabel;
        var backText = RealmModal.Embedded(this) || HasMeta("battle_modal") ? "Back to Map" : $"Back to {returnLabel}";
        if (HasMeta("battle_modal")) backText = "Back to Battle";
        var back = RoyalButton.Over(spec.Rect("button.back"), backText, Leave, 6);
        back.SetGlyph(RoyalKit.Texture("icon-map-pin"), new Rect2(spec.Rect("button.back.icon").Position - spec.Rect("button.back").Position, spec.Rect("button.back.icon").Size));
        var backLabel = spec.Label("button.back.label", backText, 220);
        backLabel.Position -= spec.Rect("button.back").Position;
        back.SetCaption(backLabel, new Rect2(backLabel.Position, backLabel.Size));
        _layer.AddChild(back);
    }

    private void Leave()
    {
        if (HasMeta("battle_modal") || RealmModal.Embedded(this)) { Close(); return; }
        SceneRouter.Instance.ReturnFromSettings();
    }

    private void RestoreDefaults()
    {
        var state = GameState.Instance;
        state.SetPlayerCallsign("Lantern");
        state.ClearPlayerProfileSession();
        state.SetAudioMuted(false);
        state.SetEffectsVolumePercent(85);
        state.SetAmbienceVolumePercent(65);
        state.SetMusicVolumePercent(50);
        state.SetLanguage("en");
        state.SetFontSizeOffset(0);
        state.SetHighContrast(false);
        state.SetShowDevUi(true);
        state.SetDeveloperMode(false);
        state.SetShowFpsCounter(true);
        state.SetChallengeSyncProvider(ChallengeSyncProviderCatalog.LocalJournalId);
        state.SetChallengeSyncEndpoint("");
        state.SetChallengeSyncAutoFlush(false);
        state.SetDifficulty(DifficultyCatalog.NormalId);
        state.SetShowHints(true);
        state.SetPurchaseValidationEndpoint("");
        _status = "Settings restored to defaults.";
        Rebuild();
    }

    private void OpenDeveloperTools()
    {
        var layer = new CanvasLayer { Layer = 40 };
        AddChild(layer);
        var modal = RealmModal.OpenInspector(layer, "Developer tools", "settings", 900, 560);
        var page = RealmUi.Scroll(modal.Content);
        ((ScrollContainer)page.GetParent()).SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildDeveloperPage(page);
        RefreshDeveloperPage();
        RealmModal.Polish(page);
    }

    private static string DetectPurchasePlatform()
    {
        if (OS.HasFeature("ios")) return "Apple (StoreKit 2)";
        if (OS.HasFeature("android")) return "Google Play Billing";
        return "Stripe Checkout (web/PC)";
    }
}

/// <summary>A thin brass thumb showing where a scrolling list is.</summary>
public partial class RoyalScrollHint : Control
{
    public ScrollContainer Scroll;
    public RoyalScrollHint() { MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (!GodotObject.IsInstanceValid(Scroll)) return;
        var bar = Scroll.GetVScrollBar();
        var range = Mathf.Max(1, bar.MaxValue - bar.Page);
        var length = Mathf.Clamp(Size.Y * (float)(bar.Page / Mathf.Max(1, bar.MaxValue)), 24, Size.Y);
        var top = (Size.Y - length) * (float)(bar.Value / range);
        DrawRect(new Rect2(0, 0, Size.X, Size.Y), new Color(.1f, .08f, .06f, .55f));
        DrawRect(new Rect2(0, top, Size.X, length), new Color("b89458"));
    }
}

public static class ControlExtensions
{
    public static T With<T>(this T control, Action<T> setup) where T : Control { setup(control); return control; }
}
