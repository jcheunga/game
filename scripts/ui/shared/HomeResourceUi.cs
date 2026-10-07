using Godot;

/// <summary>Painted resource icons with readable amounts and named accessibility text.</summary>
public static class HomeResourceUi
{
    public static HBoxContainer Amount(string icon, string amount, string hint, int iconSize = 32)
    {
        var row = new HBoxContainer { TooltipText = hint, AccessibilityName = hint, MouseFilter = Control.MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 7);
        row.AddChild(new TextureRect {
            Texture = icon is "sigils" or "shards" or "essence" ? UiArtLoader.TryLoadRewardIcon(icon) : HomeMapArt.Icon(icon),
            CustomMinimumSize = new Vector2(iconSize, iconSize),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        var number = new Label { Text = amount, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        number.AddThemeFontSizeOverride("font_size", 22);
        number.AddThemeColorOverride("font_color", RealmUi.Gold);
        row.AddChild(number);
        return row;
    }

    public static float AmountWidth(string icon, string amount, int fontSize, int iconSize) =>
        RealmUi.TitleFont.GetStringSize(amount, fontSize: fontSize).X + (string.IsNullOrEmpty(icon) ? 0 : iconSize + 5);

    public static void DrawAmount(CanvasItem canvas, Vector2 center, string icon, string amount, int fontSize, int iconSize, Color tint, bool outline = false)
    {
        var font = RealmUi.TitleFont;
        var left = center.X - AmountWidth(icon, amount, fontSize, iconSize) / 2;
        if (!string.IsNullOrEmpty(icon))
        {
            canvas.DrawTextureRect(HomeMapArt.Icon(icon), new Rect2(left, center.Y - iconSize / 2f, iconSize, iconSize), false, new Color(1, 1, 1, tint.A));
            left += iconSize + 5;
        }
        var position = new Vector2(left, center.Y + (font.GetAscent(fontSize) - font.GetDescent(fontSize)) / 2);
        if (outline) canvas.DrawStringOutline(font, position, amount, fontSize: fontSize, size: 3, modulate: new Color("172521") { A = tint.A });
        canvas.DrawString(font, position, amount, fontSize: fontSize, modulate: tint);
    }
}
