using System;
using Godot;

/// <summary>The home panels use materials and action hierarchy separate from map controls.</summary>
public static class ModalUi
{
    public static readonly Font HeadingFont = new SystemFont { FontNames = new[] { "Trebuchet MS", "Verdana", "sans-serif" }, FontWeight = 800 };
    public static readonly Color Cream = new("fff0cf"), Muted = new("cbbba5");
    // Pigments found on worn banners: slate, plum, ochre, forest and oxblood.
    private static readonly Color[] TabColours = { new("657789"), new("817080"), new("9b7e53"), new("627867"), new("8e6763"), new("727885") };

    public static Color Accent(string title)
    {
        var text = title.ToLowerInvariant();
        return text.Contains("spell") || text.Contains("rite") || text.Contains("relic") || text.Contains("tower") || text.Contains("raid") ? TabColours[1]
            : text.Contains("upgrade") || text.Contains("wagon") || text.Contains("forge") ? TabColours[2]
            : text.Contains("achievement") || text.Contains("reward") ? new Color("b09a69")
            : text.Contains("codex") || text.Contains("caravan") || text.Contains("expedition") ? TabColours[3]
            : text.Contains("setting") || text.Contains("arena") || text.Contains("guild") || text.Contains("endless") ? TabColours[4] : TabColours[0];
    }

    public static void StyleButton(Button button, bool selected = false, Color? accent = null, ModalMaterial? material = null)
    {
        bool tab = button.GetParent()?.HasMeta("realm_tabs") ?? false;
        var colour = accent ?? (tab ? TabColours[button.GetIndex() % TabColours.Length] : Accent(button.Text));
        var type = material ?? (tab ? ModalMaterial.Tab : button.HasMeta("realm_primary") ? ModalMaterial.Gold : ModalMaterial.Steel);
        button.AddThemeStyleboxOverride("normal", new ModalSurface(type, 8, colour, selected));
        button.AddThemeStyleboxOverride("hover", new ModalSurface(type, 8, colour.Lightened(.05f), true));
        button.AddThemeStyleboxOverride("pressed", new ModalSurface(type == ModalMaterial.Tab ? ModalMaterial.Tab : type, 8, colour, true, true));
        button.AddThemeStyleboxOverride("hover_pressed", new ModalSurface(type, 8, colour.Lightened(.04f), true));
        button.AddThemeStyleboxOverride("disabled", new ModalSurface(ModalMaterial.Inset, 8, colour));
        var focus = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = new Color("dcc693") }; focus.SetBorderWidthAll(2); focus.SetCornerRadiusAll(4); button.AddThemeStyleboxOverride("focus", focus);
        var ink = type == ModalMaterial.Gold ? new Color("382316") : Cream;
        button.AddThemeFontOverride("font", HeadingFont); button.AddThemeFontSizeOverride("font_size", 20);
        if (button is RealmButton realm) realm.SetPresentation(HeadingFont, ink, new Color("aaa091"));
        else foreach (var key in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" }) button.AddThemeColorOverride(key, ink);
        button.AddThemeColorOverride("icon_normal_color", ink);
        button.AddThemeColorOverride("icon_hover_color", ink);
        button.AddThemeColorOverride("icon_pressed_color", ink);
        if (button is RealmButton { CenterIconAndText: true }) foreach (var key in new[] { "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_hover_pressed_color", "icon_focus_color", "icon_disabled_color" }) button.AddThemeColorOverride(key, Colors.Transparent);
    }

    public static void Dress(Node node)
    {
        if (node is PanelContainer badge && badge.HasMeta("realm_badge"))
            badge.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Portrait, 1, badge.HasMeta("badge_tint") ? (Color)badge.GetMeta("badge_tint") : new Color("6b82a4")));
        if (node is PanelContainer panel && panel.MouseFilter != Control.MouseFilterEnum.Ignore && !panel.HasMeta("modal_unframed"))
        {
            panel.SelfModulate = Colors.White;
            var material = panel.HasMeta("modal_material") ? (ModalMaterial)(int)panel.GetMeta("modal_material") : ModalMaterial.Inset;
            panel.AddThemeStyleboxOverride("panel", new ModalSurface(material, 12));
        }
        if (node is Button button) StyleButton(button);
        if (node is Label label)
        {
            label.AddThemeFontSizeOverride("font_size", Math.Max(18, label.GetThemeFontSize("font_size")));
            if (label.GetThemeFont("font") == RealmUi.TitleFont) label.AddThemeFontOverride("font", HeadingFont);
            if (label.GetThemeColor("font_color") == RealmUi.Muted) label.AddThemeColorOverride("font_color", Muted);
            label.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
            label.AddThemeConstantOverride("shadow_offset_x", 0); label.AddThemeConstantOverride("shadow_offset_y", 0);
        }
        if (node is ProgressBar progress) StyleProgress(progress, new Color("64bde7"));
        if (node is HSlider slider) StyleSlider(slider);
        if (node is ScrollBar scroll)
        {
            var rail = new StyleBoxFlat { BgColor = new Color("201921") }; rail.SetCornerRadiusAll(3);
            var handle = new StyleBoxFlat { BgColor = new Color("b8a47f"), BorderColor = new Color("efdcaa") }; handle.SetBorderWidthAll(1); handle.SetCornerRadiusAll(3);
            scroll.AddThemeStyleboxOverride("scroll", rail);
            foreach (var state in new[] { "grabber", "grabber_highlight", "grabber_pressed" }) scroll.AddThemeStyleboxOverride(state, handle);
        }
    }
    public static void StyleProgress(ProgressBar progress, Color colour)
    {
        var track = new StyleBoxFlat { BgColor = new Color("1b151c"), BorderColor = new Color("7c695d") }; track.SetBorderWidthAll(1); track.SetCornerRadiusAll(3);
        var fill = new StyleBoxFlat { BgColor = colour, BorderColor = colour.Lightened(.3f) }; fill.SetBorderWidthAll(1); fill.SetCornerRadiusAll(3);
        progress.AddThemeStyleboxOverride("background", track); progress.AddThemeStyleboxOverride("fill", fill);
    }
    public static Control Banner(int illustration, string title, string description, int height = 112)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, height) }; panel.SetMeta("modal_unframed", true);
        panel.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Inset, 12));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 18); panel.AddChild(row);
        row.AddChild(new TextureRect { Texture = ModalArt.Illustration(illustration), CustomMinimumSize = new Vector2(148, 82), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore });
        var words = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }; words.AddThemeConstantOverride("separation", 6); row.AddChild(words);
        var heading = RealmUi.Heading(title, 22); heading.AddThemeFontOverride("font", HeadingFont); heading.AddThemeColorOverride("font_color", Cream); words.AddChild(heading);
        var text = RealmUi.Label(description, 18); text.AddThemeFontSizeOverride("font_size", 18); text.AddThemeColorOverride("font_color", Muted); words.AddChild(text);
        return panel;
    }
    private static void StyleSlider(HSlider slider)
    {
        var rail = new StyleBoxFlat { BgColor = new Color("1e161c"), BorderColor = new Color("8b6946"), ContentMarginTop = 5, ContentMarginBottom = 5 }; rail.SetBorderWidthAll(1); rail.SetCornerRadiusAll(5);
        var fill = new StyleBoxFlat { BgColor = new Color("ba9450"), BorderColor = new Color("d8bf83"), ContentMarginTop = 5, ContentMarginBottom = 5 }; fill.SetBorderWidthAll(1); fill.SetCornerRadiusAll(5);
        slider.AddThemeStyleboxOverride("slider", rail); slider.AddThemeStyleboxOverride("grabber_area", fill); slider.AddThemeStyleboxOverride("grabber_area_highlight", fill);
        var thumb = ResourceLoader.Load<Texture2D>("res://assets/ui/modal/slider-thumb-v1.svg");
        slider.AddThemeIconOverride("grabber", thumb); slider.AddThemeIconOverride("grabber_highlight", thumb); slider.AddThemeIconOverride("grabber_disabled", thumb);
    }
}
