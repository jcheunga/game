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
        var heading = RealmUi.Heading(title, 24);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(heading);
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

    private static Theme BuildTheme()
    {
        var theme = new Theme();
        theme.DefaultFont = RealmUi.TitleFont;
        var ink = new Color("eae5d9");
        var goldLight = new Color("f3d78c");

        theme.SetColor("font_color", "Label", ink);
        // A soft one-pixel drop keeps light text legible on painted art without doubling the letters.
        theme.SetColor("font_shadow_color", "Label", new Color(0f, 0f, 0f, 0.55f));
        theme.SetConstant("shadow_offset_x", "Label", 0);
        theme.SetConstant("shadow_offset_y", "Label", 1);
        theme.SetFontSize("font_size", "Label", 20);
        theme.SetFontSize("font_size", "Button", RealmUi.ButtonFontSize);
        theme.SetFont("font", "Button", RealmUi.TitleFont);
        theme.SetFontSize("font_size", "LineEdit", 20);
        foreach (var type in new[] { "OptionButton", "MenuButton", "PopupMenu", "CheckBox", "CheckButton" })
            theme.SetFontSize("font_size", type, RealmUi.ButtonFontSize);
        theme.SetFontSize("font_size", "TooltipLabel", 18);
        theme.SetColor("font_color", "Button", ink);
        theme.SetColor("font_hover_color", "Button", goldLight);
        theme.SetColor("font_pressed_color", "Button", Colors.White);
        theme.SetColor("font_disabled_color", "Button", new Color("9aaba5"));
        theme.SetColor("caret_color", "LineEdit", goldLight);
        theme.SetColor("font_color", "LineEdit", ink);

        theme.SetStylebox("panel", "Panel", new ModalSurface(ModalMaterial.Inset, 10));
        theme.SetStylebox("panel", "PanelContainer", new ModalSurface(ModalMaterial.Wood, 14));
        theme.SetStylebox("normal", "Button", Engraved("button", 18, 10));
        theme.SetStylebox("hover", "Button", Engraved("button_hover", 18, 10));
        theme.SetStylebox("pressed", "Button", Engraved("button_pressed", 18, 10));
        theme.SetStylebox("disabled", "Button", Engraved("button_disabled", 18, 10));
        theme.SetStylebox("hover_pressed", "Button", Engraved("button_hover_pressed", 18, 10));
        theme.SetStylebox("focus", "Button", Engraved("focus", 9, 6));
        theme.SetStylebox("normal", "LineEdit", Engraved("inset", 10, 6));
        theme.SetStylebox("focus", "LineEdit", Engraved("input_focus", 9, 6));
        theme.SetStylebox("read_only", "LineEdit", Engraved("input_disabled", 10, 6));
        theme.SetColor("font_uneditable_color", "LineEdit", new Color("a2b2ad"));
        theme.SetColor("selection_color", "LineEdit", new Color("516653"));
        theme.SetStylebox("panel", "ScrollContainer", new StyleBoxEmpty { ContentMarginRight = 8 });
        // Panel padding sits outside the rail; reserve a separate gutter beside content.
        theme.SetConstant("scrollbar_h_separation", "ScrollContainer", 18);
        theme.SetConstant("scrollbar_v_separation", "ScrollContainer", 12);
        foreach(var type in new[]{"VScrollBar","HScrollBar"})
        {
            var thumb=type=="HScrollBar" ? "scroll_thumb_horizontal" : "scroll_thumb";
            theme.SetStylebox("scroll", type, Engraved("inset", 2, 6));
            theme.SetStylebox("scroll_focus", type, Engraved("input_focus", 2, 6));
            theme.SetStylebox("grabber", type, Engraved(thumb, 2, 6));
            theme.SetStylebox("grabber_highlight", type, Engraved(thumb+"_hover", 2, 6));
            theme.SetStylebox("grabber_pressed", type, Engraved(thumb+"_hover", 2, 6));
        }
        foreach(var type in new[]{"OptionButton","MenuButton"})
        {
            foreach(var state in new[]{"normal","hover","pressed","hover_pressed","disabled"})
                theme.SetStylebox(state, type, Engraved(state=="normal"?"button":"button_"+state, 12, 6));
            theme.SetStylebox("focus", type, Engraved("focus", 9, 6));
            theme.SetColor("font_color", type, ink);
            theme.SetColor("font_hover_color", type, goldLight);
            theme.SetColor("font_pressed_color", type, Colors.White);
            theme.SetColor("font_disabled_color", type, new Color("9aaba5"));
        }
        theme.SetIcon("arrow", "OptionButton", FrameTexture("dropdown"));
        theme.SetStylebox("panel", "PopupMenu", Engraved("engraved_panel", 12, 10));
        theme.SetStylebox("hover", "PopupMenu", Engraved("button_hover", 6, 4));
        theme.SetColor("font_color", "PopupMenu", ink);
        theme.SetColor("font_hover_color", "PopupMenu", goldLight);
        theme.SetColor("font_disabled_color", "PopupMenu", new Color("9aaba5"));
        theme.SetStylebox("panel", "AcceptDialog", Engraved("engraved_panel", 18, 14));
        foreach(var type in new[]{"CheckBox","CheckButton"})
        {
            foreach(var icon in new[]{"checked","unchecked","radio_checked","radio_unchecked",
                "checked_disabled","unchecked_disabled","radio_checked_disabled","radio_unchecked_disabled"})
                theme.SetIcon(icon, type, FrameTexture(icon));
            theme.SetStylebox("focus", type, Engraved("focus", 4, 4));
            theme.SetColor("font_color", type, ink);
        }
        theme.SetStylebox("background", "ProgressBar", Engraved("meter_track", 0, 0));
        var progressFill=Engraved("meter_fill", 0, 0); progressFill.ModulateColor=new Color("948759");
        theme.SetStylebox("fill", "ProgressBar", progressFill);
        theme.SetColor("font_color", "ProgressBar", ink);
        theme.SetColor("font_outline_color", "ProgressBar", new Color("080e12"));
        theme.SetConstant("outline_size", "ProgressBar", 2);
        theme.SetStylebox("separator", "HSeparator", Engraved("separator", 0, 1));
        theme.SetStylebox("separator", "PopupMenu", Engraved("separator", 0, 1));
        theme.SetColor("font_color", "TooltipLabel", ink);
        theme.SetStylebox("panel", "TooltipPanel", Engraved("inset", 10, 6));
        theme.SetConstant("separation", "VBoxContainer", 10);
        theme.SetConstant("separation", "HBoxContainer", 10);
        theme.SetConstant("separation", "GridContainer", 10);
        return theme;
    }

    public static StyleBoxTexture Engraved(string asset, float horizontal, float vertical)
    {
        var slice = asset switch { "engraved_panel"=>32,
            "scroll_thumb" or "scroll_thumb_hover" or "scroll_thumb_horizontal" or "scroll_thumb_horizontal_hover"=>6,
            "meter_track" or "meter_fill"=>4, "separator"=>1, _=>16 };
        var tileVertical=asset is "engraved_panel" or "inset" or "input_focus" or "input_disabled" or "surface_body" or "surface_rim";
        return new StyleBoxTexture
        {
            Texture = FrameTexture(asset),
            AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile,
            AxisStretchVertical = tileVertical ? StyleBoxTexture.AxisStretchMode.Tile : StyleBoxTexture.AxisStretchMode.Stretch,
            TextureMarginLeft = slice, TextureMarginRight = slice, TextureMarginTop = slice, TextureMarginBottom = slice,
            ContentMarginLeft = horizontal, ContentMarginRight = horizontal, ContentMarginTop = vertical, ContentMarginBottom = vertical
        };
    }

    public static Texture2D FrameTexture(string asset) => ResourceLoader.Load<Texture2D>($"res://assets/ui/frames/{asset}.svg");

    public static void StyleButton(Button button,float horizontal,float vertical)
    {
        var primary=button.HasMeta("realm_primary");
        foreach(var state in new[]{"normal","hover","pressed","hover_pressed","disabled"})
        {
            var asset=state=="normal"?"button":"button_"+state;
            if(primary && state!="disabled") asset=state switch
            {
                "normal"=>"button_primary", "hover"=>"button_primary_hover", _=>"button_primary_pressed"
            };
            button.AddThemeStyleboxOverride(state,Engraved(asset,horizontal,vertical));
        }
        button.AddThemeStyleboxOverride("focus",Engraved("focus",horizontal,vertical));
    }

    private static void DressHeader(Control root)
    {
        if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree() || root.GetParent() is CanvasLayer) return;
        foreach (var node in root.GetChildren())
        {
            if (node is not PanelContainer panel || panel.Position.Y > 30 || panel.Size.X < 1000) continue;
            var label = FindFirstLabel(panel);
            if (label == null) continue;
            RealmUi.Display(label, 32);
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
        if (node is BaseButton) return null;
        if (node is Label label) return label;
        foreach (var child in node.GetChildren())
        {
            var found = FindFirstLabel(child);
            if (found != null) return found;
        }
        return null;
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
