using Godot;

/// <summary>Buttons share readable, width-bounded tooltips instead of unwrapped native text.</summary>
public partial class RealmButton : Button
{
    public override Control _MakeCustomTooltip(string forText)
    {
        const int size = 18;
        var font = ThemeDB.FallbackFont;
        var width = Mathf.Clamp(font.GetStringSize(forText, HorizontalAlignment.Left, -1, size).X, 220, 420);
        while (width < 760 && font.GetMultilineStringSize(forText, HorizontalAlignment.Left, width, size).Y > 530)
            width += 80;
        var label = new Label { Text = forText, CustomMinimumSize = new Vector2(width, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.SetMeta("realm_tooltip", true);
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }
}
