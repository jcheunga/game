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
        Callable.From(() => DressHeader(root)).CallDeferred();
        if (root.IsInsideTree()) root.GetWindow().Title = "Crownroad — Siege of Ash";
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
        var cancel = new RealmButton { Text = "Keep playing", CustomMinimumSize = new Vector2(160f, 44f) };
        cancel.Pressed += () => { veil.QueueFree(); center.QueueFree(); };
        row.AddChild(cancel);
        var confirm = new RealmButton { Text = confirmText, CustomMinimumSize = new Vector2(160f, 44f) };
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
            var button = new RealmButton { Text = text, CustomMinimumSize = new Vector2(0f, 40f) };
            button.Pressed += () => { action(); Refresh(); };
            stack.AddChild(button);
            return button;
        }
        SettingButton("Music −", () => GameState.Instance.SetMusicVolumePercent(GameState.Instance.MusicVolumePercent - 10));
        SettingButton("Music +", () => GameState.Instance.SetMusicVolumePercent(GameState.Instance.MusicVolumePercent + 10));
        SettingButton("Toggle sound", () => GameState.Instance.SetAudioMuted(!GameState.Instance.AudioMuted));
        SettingButton("Toggle field hints", () => GameState.Instance.SetShowHints(!GameState.Instance.ShowHints));
        var advanced = new RealmButton { Text = "Open full settings", CustomMinimumSize = new Vector2(0f, 42f) };
        advanced.Pressed += () => SceneRouter.Instance.GoToSettings();
        stack.AddChild(advanced);
        var close = new RealmButton { Text = "Return to camp", CustomMinimumSize = new Vector2(0f, 40f) };
        close.Pressed += () => { veil.QueueFree(); center.QueueFree(); };
        stack.AddChild(close);
        Refresh();
    }

    private static Theme BuildTheme()
    {
        var theme = new Theme();
        var ink = new Color("eae5d9");
        var gold = new Color("d9ad55");
        var goldLight = new Color("f3d78c");
        var oak = new Color("111d22");
        var oakLight = new Color("203139");

        theme.SetColor("font_color", "Label", ink);
        theme.SetColor("font_shadow_color", "Label", new Color(0f, 0f, 0f, 0.7f));
        theme.SetConstant("shadow_offset_x", "Label", 1);
        theme.SetConstant("shadow_offset_y", "Label", 2);
        theme.SetFontSize("font_size", "Label", 20);
        theme.SetFontSize("font_size", "Button", 20);
        theme.SetFont("font", "Button", RealmUi.TitleFont);
        theme.SetFontSize("font_size", "LineEdit", 20);
        theme.SetFontSize("font_size", "OptionButton", 20);
        theme.SetFontSize("font_size", "PopupMenu", 20);
        theme.SetFontSize("font_size", "TooltipLabel", 18);
        theme.SetColor("font_color", "Button", ink);
        theme.SetColor("font_hover_color", "Button", goldLight);
        theme.SetColor("font_pressed_color", "Button", Colors.White);
        theme.SetColor("font_disabled_color", "Button", new Color("8c806a"));
        theme.SetColor("caret_color", "LineEdit", goldLight);
        theme.SetColor("font_color", "LineEdit", ink);

        theme.SetStylebox("panel", "Panel", Box(oak, gold.Darkened(0.42f), 1, 8, 10));
        theme.SetStylebox("panel", "PanelContainer", Engraved("engraved_panel", 18, 14));
        theme.SetStylebox("normal", "Button", Engraved("button", 18, 10));
        theme.SetStylebox("hover", "Button", Engraved("button_hover", 18, 10));
        theme.SetStylebox("pressed", "Button", Engraved("button_pressed", 18, 10));
        theme.SetStylebox("disabled", "Button", Engraved("button_disabled", 18, 10));
        theme.SetStylebox("focus", "Button", Box(new Color(0f, 0f, 0f, 0f), goldLight, 2, 7, 9));
        theme.SetStylebox("normal", "LineEdit", Box(new Color("171213"), new Color("46514b"), 1, 6, 10));
        theme.SetStylebox("focus", "LineEdit", Box(new Color("201817"), goldLight, 2, 6, 9));
        theme.SetStylebox("read_only", "LineEdit", Box(new Color("171213"), new Color("5b4935"), 1, 6, 10));
        theme.SetStylebox("panel", "ScrollContainer", new StyleBoxEmpty { ContentMarginRight = 8 });
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

    public static StyleBoxTexture Engraved(string asset, float horizontal, float vertical)
    {
        var slice = asset == "engraved_panel" ? 30 : 16;
        return new StyleBoxTexture
        {
            Texture = ResourceLoader.Load<Texture2D>($"res://assets/ui/frames/{asset}.svg"),
            TextureMarginLeft = slice, TextureMarginRight = slice, TextureMarginTop = slice, TextureMarginBottom = slice,
            ContentMarginLeft = horizontal, ContentMarginRight = horizontal, ContentMarginTop = vertical, ContentMarginBottom = vertical
        };
    }

    private static void DressHeader(Control root)
    {
        if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree() || root.GetParent() is CanvasLayer) return;
        foreach (var node in root.GetChildren())
        {
            if (node is not PanelContainer panel || panel.Position.Y > 30 || panel.Size.X < 1000) continue;
            var label = FindFirstLabel(panel);
            if (label == null) continue;
            label.AddThemeFontOverride("font", RealmUi.TitleFont);
            label.AddThemeFontSizeOverride("font_size", 32);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.AddThemeColorOverride("font_color", new Color("f0d9a1"));
            if (!label.HasMeta("heraldic_title"))
            {
                label.SetMeta("heraldic_title", true);
                var parent = label.GetParent(); var index = label.GetIndex();
                var group = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                parent.RemoveChild(label); parent.AddChild(group); parent.MoveChild(group, index);
                group.AddChild(new HeraldicEmblem { Symbol = root.Name.ToString() switch
                {
                    "ShopMenu" => "sword", "ForgeMenu" => "hammer", "BountyMenu" => "flag",
                    "ProfileMenu" => "shield", "CodexMenu" => "book", "SettingsMenu" => "gear",
                    "CashShopMenu" => "gold", "GuildMenu" or "FriendsMenu" => "people",
                    "EndlessMenu" => "flame", "TowerMenu" => "mountain", _ => "crown"
                } });
                group.AddChild(label);
            }
        }
    }

    private static Label FindFirstLabel(Node node)
    {
        if (node is Label label) return label;
        foreach (var child in node.GetChildren())
        {
            var found = FindFirstLabel(child);
            if (found != null) return found;
        }
        return null;
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
