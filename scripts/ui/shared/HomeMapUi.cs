using System;
using Godot;

/// <summary>Crafted floating controls for the atlas home screen.</summary>
public static class HomeMapUi
{
    public static StyleBox Surface(bool highlighted = false, int padding = 12, int radius = 12) => new HomeMapFrame(highlighted, padding, radius);

    public static void Place(Control control, float anchorX, float anchorY, Rect2 offsets)
    {
        control.AnchorLeft = control.AnchorRight = anchorX;
        control.AnchorTop = control.AnchorBottom = anchorY;
        control.OffsetLeft = offsets.Position.X;
        control.OffsetTop = offsets.Position.Y;
        control.OffsetRight = offsets.End.X;
        control.OffsetBottom = offsets.End.Y;
    }

    public static Button IconButton(string icon, string hint, Action action)
    {
        var button = RealmUi.IconButton(icon, hint, action);
        button.CustomMinimumSize = new Vector2(48, 48);
        button.AddThemeConstantOverride("icon_max_width", 28);
        StyleButton(button);
        return button;
    }

    public static void StyleButton(Button button, bool active = false)
    {
        button.AddThemeStyleboxOverride("normal", Surface(active, 8));
        button.AddThemeStyleboxOverride("hover", Surface(true, 8));
        button.AddThemeStyleboxOverride("pressed", Surface(true, 8));
        button.AddThemeStyleboxOverride("disabled", Surface(false, 8));
        var focus = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = new Color("ffe2a6") };
        focus.SetCornerRadiusAll(12);
        focus.SetBorderWidthAll(2);
        button.AddThemeStyleboxOverride("focus", focus);
    }

    public static Button Tab(string icon, string title, Action action, bool active = false)
    {
        var button = new Button
        {
            Name = title + "Tab", AccessibilityName = title, TooltipText = title,
            CustomMinimumSize = new Vector2(108, 108), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        foreach (var state in new[] { "normal", "disabled" }) button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("hover", Surface(false, 0, 14));
        button.AddThemeStyleboxOverride("pressed", Surface(true, 0, 14));
        var focus = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = new Color("ffe1a0") };
        focus.SetCornerRadiusAll(14); focus.SetBorderWidthAll(2);
        button.AddThemeStyleboxOverride("focus", focus);
        var content = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 3);
        button.AddChild(content);
        content.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        content.OffsetTop = 4;
        content.OffsetBottom = -4;
        var circle = new PanelContainer
        {
            CustomMinimumSize = new Vector2(70, 70), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        circle.AddThemeStyleboxOverride("panel", Surface(active, 2, 35));
        content.AddChild(circle);
        circle.AddChild(new TextureRect
        {
            Texture = HomeMapArt.Icon(icon), CustomMinimumSize = new Vector2(64, 64),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        var label = new Label
        {
            Text = title, HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", RealmUi.CompactButtonFontSize);
        label.AddThemeFontOverride("font", RealmUi.TitleFont);
        label.AddThemeColorOverride("font_color", active ? new Color("ffe5a8") : new Color("f1e7d2"));
        label.AddThemeColorOverride("font_shadow_color", new Color("0b171c"));
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        content.AddChild(label);
        button.Pressed += () => action?.Invoke();
        return button;
    }
}
