using System;
using Godot;

/// <summary>Crafted floating controls for the atlas home screen.</summary>
public static class HomeMapUi
{
    public static StyleBox Surface(bool highlighted = false, int padding = 12) => new HomeMapFrame(highlighted, padding);

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
        button.AddThemeStyleboxOverride("normal", Surface(active, 10));
        button.AddThemeStyleboxOverride("hover", Surface(true, 10));
        button.AddThemeStyleboxOverride("pressed", Surface(true, 10));
        button.AddThemeStyleboxOverride("disabled", Surface(false, 10));
        var focus = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = new Color("ffe2a6") };
        focus.SetCornerRadiusAll(12);
        focus.SetBorderWidthAll(2);
        button.AddThemeStyleboxOverride("focus", focus);
    }

}
