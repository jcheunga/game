using System;
using Godot;

/// <summary>The home panels use materials and action hierarchy separate from map controls.</summary>
public static class ModalUi
{
    public static readonly Font HeadingFont = RealmUi.TitleFont;
    public static readonly Color Cream = new("fff0cf"), Muted = new("cbbba5");
    public static void StyleButton(Button button, bool selected = false, ModalMaterial? material = null)
    {
        bool tab = (button.GetParent()?.HasMeta("realm_tabs") ?? false) || button.HasMeta("realm_toggle");
        if (tab && material == null) { StyleTab(button); return; }
        var type = material ?? (button.HasMeta("realm_primary") ? ModalMaterial.Gold : ModalMaterial.Steel);
        if (selected && type == ModalMaterial.Gold && !button.HasMeta("realm_primary")) type = ModalMaterial.Portrait;
        button.AddThemeStyleboxOverride("normal", new ModalSurface(type, 8, selected));
        button.AddThemeStyleboxOverride("hover", new ModalSurface(type, 8, true));
        button.AddThemeStyleboxOverride("pressed", new ModalSurface(type, 8, true, true));
        button.AddThemeStyleboxOverride("hover_pressed", new ModalSurface(type, 8, true));
        button.AddThemeStyleboxOverride("disabled", new ModalSurface(ModalMaterial.Inset, 8));
        var focus = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = new Color("dcc693") }; focus.SetBorderWidthAll(2); focus.SetCornerRadiusAll(4); button.AddThemeStyleboxOverride("focus", focus);
        var ink = type == ModalMaterial.Gold ? new Color("382316") : Cream;
        button.AddThemeFontOverride("font", HeadingFont); button.AddThemeFontSizeOverride("font_size", RealmUi.ButtonFontSize);
        if (button is RealmButton realm) realm.SetPresentation(HeadingFont, ink, new Color("aaa091"));
        else foreach (var key in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" }) button.AddThemeColorOverride(key, ink);
        button.AddThemeColorOverride("icon_normal_color", ink);
        button.AddThemeColorOverride("icon_hover_color", ink);
        button.AddThemeColorOverride("icon_pressed_color", ink);
        if (button is RealmButton { CenterIconAndText: true }) foreach (var key in new[] { "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_hover_pressed_color", "icon_focus_color", "icon_disabled_color" }) button.AddThemeColorOverride(key, Colors.Transparent);
    }

    // Every tab row shares one material; only the selected tab is lit, so the
    // current page reads at a glance instead of competing with coloured siblings.
    private static readonly Color TabInk = new("ece3d2"), TabSelectedInk = new("fff0cf");

    private static void StyleTab(Button button)
    {
        // List rows read from the left edge, so they need a wider inset than centred tabs.
        var inset = button.Alignment == HorizontalAlignment.Left ? 16 : ModalSurface.MinimumSideInset;
        StyleBox Surface(ModalMaterial material, bool active = false)
        {
            var surface = new ModalSurface(material, 8, active);
            surface.ContentMarginLeft = surface.ContentMarginRight = inset;
            return surface;
        }
        button.AddThemeStyleboxOverride("normal", Surface(ModalMaterial.Tab));
        button.AddThemeStyleboxOverride("hover", Surface(ModalMaterial.Tab));
        button.AddThemeStyleboxOverride("pressed", Surface(ModalMaterial.Tab, true));
        button.AddThemeStyleboxOverride("hover_pressed", Surface(ModalMaterial.Tab, true));
        button.AddThemeStyleboxOverride("disabled", Surface(ModalMaterial.Inset));
        var focus = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = new Color("dcc693") }; focus.SetBorderWidthAll(2); focus.SetCornerRadiusAll(4); button.AddThemeStyleboxOverride("focus", focus);
        button.AddThemeFontOverride("font", HeadingFont); button.AddThemeFontSizeOverride("font_size", RealmUi.ButtonFontSize);
        if (button is RealmButton realm) realm.SetPresentation(HeadingFont, TabInk, new Color("aaa091"));
        foreach (var key in new[] { "font_color", "font_hover_color", "font_focus_color" }) button.AddThemeColorOverride(key, TabInk);
        foreach (var key in new[] { "font_pressed_color", "font_hover_pressed_color" }) button.AddThemeColorOverride(key, TabSelectedInk);
        if (button is RealmButton { CenterIconAndText: true, HasContentGroup: true })
        {
            foreach (var key in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color", "font_disabled_color",
                "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_hover_pressed_color", "icon_focus_color", "icon_disabled_color" })
                button.AddThemeColorOverride(key, Colors.Transparent);
        }
    }

    public static void Dress(Node node)
    {
        if (node is PanelContainer badge && badge.HasMeta("realm_badge"))
            badge.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Portrait, 1));
        if (node is PanelContainer panel && panel.MouseFilter != Control.MouseFilterEnum.Ignore && !panel.HasMeta("modal_unframed"))
        {
            panel.SelfModulate = Colors.White;
            var material = panel.HasMeta("modal_material") ? (ModalMaterial)(int)panel.GetMeta("modal_material") : ModalMaterial.Inset;
            panel.AddThemeStyleboxOverride("panel", new ModalSurface(material, 12));
        }
        if (node is Button button) StyleButton(button);
        if (node is Label label)
        {
            var minimum = 18;
            for (var parent = label.GetParent(); parent != null; parent = parent.GetParent())
                if (parent is Button) { minimum = RealmUi.ButtonFontSize; break; }
            label.AddThemeFontSizeOverride("font_size", Math.Max(minimum, label.GetThemeFontSize("font_size")));
            if (label.GetThemeFont("font") == RealmUi.TitleFont) label.AddThemeFontOverride("font", HeadingFont);
            if (label.GetThemeColor("font_color") == RealmUi.Muted) label.AddThemeColorOverride("font_color", Muted);
            label.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
            label.AddThemeConstantOverride("shadow_offset_x", 0); label.AddThemeConstantOverride("shadow_offset_y", 0);
        }
        if (node is ProgressBar progress && !progress.HasMeta("royal_progress")) StyleProgress(progress, new Color("319fa9"));
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
        progress.SetMeta("royal_progress", true);
        var track = new StyleBoxFlat { BgColor = new Color("10191e"), BorderColor = new Color("ac8755") }; track.SetBorderWidthAll(2); track.SetCornerRadiusAll(7);
        var fill = new StyleBoxFlat { BgColor = colour, BorderColor = colour.Lightened(.3f) }; fill.SetBorderWidthAll(1); fill.SetCornerRadiusAll(6);
        progress.AddThemeStyleboxOverride("background", track); progress.AddThemeStyleboxOverride("fill", fill);
    }
    private static void StyleSlider(HSlider slider)
    {
        var rail = new StyleBoxFlat { BgColor = new Color("111a1e"), BorderColor = new Color("ac8755"), ContentMarginTop = 7, ContentMarginBottom = 7 }; rail.SetBorderWidthAll(2); rail.SetCornerRadiusAll(8);
        var fill = new StyleBoxFlat { BgColor = new Color("277c8a"), BorderColor = new Color("4aa5ac"), ContentMarginTop = 7, ContentMarginBottom = 7 }; fill.SetBorderWidthAll(1); fill.SetCornerRadiusAll(8);
        slider.AddThemeStyleboxOverride("slider", rail); slider.AddThemeStyleboxOverride("grabber_area", fill); slider.AddThemeStyleboxOverride("grabber_area_highlight", fill);
        var thumb = ResourceLoader.Load<Texture2D>("res://assets/ui/royal/slider-thumb.svg");
        slider.AddThemeIconOverride("grabber", thumb); slider.AddThemeIconOverride("grabber_highlight", thumb); slider.AddThemeIconOverride("grabber_disabled", thumb);
    }
}
