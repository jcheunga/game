using System;
using Godot;

/// <summary>
/// Shared visual language and responsive canvas used by every menu. Keeping it here
/// prevents each screen from slowly growing its own set of colours, spacing and sizes.
/// </summary>
public static class MedievalUi
{
    private static Theme _theme;

    public static void Apply(Control root)
    {
        root.Theme = _theme ??= BuildTheme();
    }

    public static void MarkBackdrop(CanvasItem item)
    {
        item.SetMeta("medieval_backdrop", true);
        if (item is Control control)
        {
            control.MouseFilter = Control.MouseFilterEnum.Ignore;
        }
    }

    public static void ShowConfirmation(Control host, string title, string body, string confirmText, Action onConfirm)
    {
        var stack = CreateModal(host, new Vector2(460f, 0f), 14, out var veil, out var center, out var panel);
        stack.AddChild(new Label { Text = title.ToUpperInvariant(), HorizontalAlignment = HorizontalAlignment.Center });
        stack.AddChild(new Label { Text = body, AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center });

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 10);
        stack.AddChild(row);
        var cancel = new Button { Text = "Keep playing", CustomMinimumSize = new Vector2(160f, 44f) };
        cancel.Pressed += () => { veil.QueueFree(); center.QueueFree(); };
        row.AddChild(cancel);
        var confirm = new Button { Text = confirmText, CustomMinimumSize = new Vector2(160f, 44f) };
        confirm.AddThemeColorOverride("font_color", new Color("ffd8c4"));
        confirm.Pressed += () => { veil.QueueFree(); center.QueueFree(); onConfirm?.Invoke(); };
        row.AddChild(confirm);

        panel.Modulate = new Color(1f, 1f, 1f, 0f);
        panel.Scale = new Vector2(0.96f, 0.96f);
        panel.PivotOffset = panel.CustomMinimumSize * 0.5f;
        var tween = host.CreateTween().SetParallel();
        tween.TweenProperty(panel, "modulate:a", 1f, 0.16f);
        tween.TweenProperty(panel, "scale", Vector2.One, 0.18f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    public static void ShowQuickSettings(Control host)
    {
        var stack = CreateModal(host, new Vector2(500f, 0f), 12, out var veil, out var center, out _);
        stack.AddChild(new Label { Text = "CAMPFIRE SETTINGS", HorizontalAlignment = HorizontalAlignment.Center });
        var summary = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        stack.AddChild(summary);

        void Refresh() => summary.Text = $"Music {GameState.Instance.MusicVolumePercent}%  ·  Effects {GameState.Instance.EffectsVolumePercent}%  ·  " +
            (GameState.Instance.AudioMuted ? "Muted" : "Sound on") + "\n" +
            (GameState.Instance.ShowHints ? "Field hints enabled" : "Field hints hidden");
        Button SettingButton(string text, Action action)
        {
            var button = new Button { Text = text, CustomMinimumSize = new Vector2(0f, 40f) };
            button.Pressed += () => { action(); Refresh(); };
            stack.AddChild(button);
            return button;
        }
        SettingButton("Music −", () => GameState.Instance.SetMusicVolumePercent(GameState.Instance.MusicVolumePercent - 10));
        SettingButton("Music +", () => GameState.Instance.SetMusicVolumePercent(GameState.Instance.MusicVolumePercent + 10));
        SettingButton("Toggle sound", () => GameState.Instance.SetAudioMuted(!GameState.Instance.AudioMuted));
        SettingButton("Toggle field hints", () => GameState.Instance.SetShowHints(!GameState.Instance.ShowHints));
        var advanced = new Button { Text = "Open full settings", CustomMinimumSize = new Vector2(0f, 42f) };
        advanced.Pressed += () => SceneRouter.Instance.GoToSettings();
        stack.AddChild(advanced);
        var close = new Button { Text = "Return to camp", CustomMinimumSize = new Vector2(0f, 40f) };
        close.Pressed += () => { veil.QueueFree(); center.QueueFree(); };
        stack.AddChild(close);
        Refresh();
    }

    private static Theme BuildTheme()
    {
        var theme = new Theme();
        var ink = new Color("f4e7c3");
        var gold = new Color("d9ad55");
        var goldLight = new Color("f3d78c");
        var oak = new Color("2a1c19");
        var oakLight = new Color("4b3027");

        theme.SetColor("font_color", "Label", ink);
        theme.SetColor("font_shadow_color", "Label", new Color(0f, 0f, 0f, 0.7f));
        theme.SetConstant("shadow_offset_x", "Label", 1);
        theme.SetConstant("shadow_offset_y", "Label", 2);
        theme.SetFontSize("font_size", "Label", 16);
        theme.SetFontSize("font_size", "Button", 16);
        theme.SetFontSize("font_size", "LineEdit", 16);
        theme.SetColor("font_color", "Button", ink);
        theme.SetColor("font_hover_color", "Button", goldLight);
        theme.SetColor("font_pressed_color", "Button", Colors.White);
        theme.SetColor("font_disabled_color", "Button", new Color("8c806a"));
        theme.SetColor("caret_color", "LineEdit", goldLight);
        theme.SetColor("font_color", "LineEdit", ink);

        theme.SetStylebox("panel", "Panel", Box(oak, gold.Darkened(0.42f), 2, 10, 10));
        theme.SetStylebox("panel", "PanelContainer", Box(new Color("201717e8"), new Color("8c6a3e"), 2, 12, 14));
        theme.SetStylebox("normal", "Button", Box(oakLight, new Color("b78b48"), 1, 7, 10));
        theme.SetStylebox("hover", "Button", Box(new Color("62412d"), goldLight, 2, 7, 10));
        theme.SetStylebox("pressed", "Button", Box(new Color("1b1212"), gold, 2, 7, 10));
        theme.SetStylebox("disabled", "Button", Box(new Color("241d1a"), new Color("544535"), 1, 7, 10));
        theme.SetStylebox("focus", "Button", Box(new Color(0f, 0f, 0f, 0f), goldLight, 2, 7, 9));
        theme.SetStylebox("normal", "LineEdit", Box(new Color("171213"), new Color("8c6a3e"), 1, 6, 10));
        theme.SetStylebox("focus", "LineEdit", Box(new Color("201817"), goldLight, 2, 6, 9));
        theme.SetStylebox("read_only", "LineEdit", Box(new Color("171213"), new Color("5b4935"), 1, 6, 10));
        theme.SetStylebox("panel", "ScrollContainer", new StyleBoxEmpty());
        theme.SetStylebox("scroll", "VScrollBar", Box(new Color("150f0f"), new Color("6d5334"), 1, 4, 2));
        theme.SetStylebox("grabber", "VScrollBar", Box(new Color("8a693d"), gold, 1, 4, 2));
        theme.SetStylebox("grabber_highlight", "VScrollBar", Box(gold, goldLight, 1, 4, 2));
        theme.SetColor("font_color", "TooltipLabel", ink);
        theme.SetStylebox("panel", "TooltipPanel", Box(new Color("1d1515"), gold.Darkened(0.2f), 1, 6, 10));
        theme.SetConstant("separation", "VBoxContainer", 10);
        theme.SetConstant("separation", "HBoxContainer", 10);
        theme.SetConstant("separation", "GridContainer", 10);
        return theme;
    }

    private static StyleBoxFlat Box(Color background, Color border, int borderWidth, int radius, int padding)
    {
        var box = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = padding,
            ContentMarginTop = Mathf.Max(6, padding / 2),
            ContentMarginRight = padding,
            ContentMarginBottom = Mathf.Max(6, padding / 2),
            ShadowColor = new Color(0f, 0f, 0f, 0.32f),
            ShadowSize = 5,
            ShadowOffset = new Vector2(0f, 2f)
        };
        return box;
    }

    private static VBoxContainer CreateModal(
        Control host,
        Vector2 minimumSize,
        int separation,
        out ColorRect veil,
        out CenterContainer center,
        out PanelContainer panel)
    {
        veil = new ColorRect { Color = new Color("08090dcc"), MouseFilter = Control.MouseFilterEnum.Stop };
        veil.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        host.AddChild(veil);

        center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        host.AddChild(center);

        panel = new PanelContainer { CustomMinimumSize = minimumSize };
        center.AddChild(panel);
        var padding = new MarginContainer();
        padding.AddThemeConstantOverride("margin_left", 26);
        padding.AddThemeConstantOverride("margin_right", 26);
        padding.AddThemeConstantOverride("margin_top", 24);
        padding.AddThemeConstantOverride("margin_bottom", 22);
        panel.AddChild(padding);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", separation);
        padding.AddChild(stack);
        return stack;
    }
}
