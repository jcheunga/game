using Godot;

/// <summary>Buttons share readable, width-bounded tooltips instead of unwrapped native text.</summary>
public partial class RealmButton : Button
{
    // Godot aligns the icon and text independently. For navigation buttons we
    // center their combined width by adjusting only the stylebox content inset.
    public bool CenterIconAndText { get; set; }
    private string _measuredText;
    private float _measuredWidth = -1f;

    public override void _Ready()
    {
        if (!CenterIconAndText) return;
        Alignment = HorizontalAlignment.Left;
        IconAlignment = HorizontalAlignment.Left;
        Resized += UpdateContentInset;
        CallDeferred(nameof(UpdateContentInset));
    }

    public override void _Process(double delta)
    {
        if (CenterIconAndText && (_measuredText != Text || !Mathf.IsEqualApprox(_measuredWidth, Size.X)))
            UpdateContentInset();
    }

    private void UpdateContentInset()
    {
        if (!IsInsideTree() || Icon == null || string.IsNullOrEmpty(Text) || Size.X <= 0f) return;
        var font = GetThemeFont("font");
        var fontSize = GetThemeFontSize("font_size");
        var textWidth = font.GetStringSize(Text, HorizontalAlignment.Left, -1, fontSize).X;
        var iconLimit = GetThemeConstant("icon_max_width");
        var iconWidth = iconLimit > 0 ? Mathf.Min(Icon.GetWidth(), iconLimit) : Icon.GetWidth();
        var groupWidth = iconWidth + GetThemeConstant("h_separation") + textWidth;
        var inset = Mathf.Max(18f, Mathf.Floor((Size.X - groupWidth) * 0.5f));

        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
        {
            if (GetThemeStylebox(state) is not StyleBoxTexture style) continue;
            if (Mathf.IsEqualApprox(style.ContentMarginLeft, inset)) continue;
            var centered = (StyleBoxTexture)style.Duplicate();
            centered.ContentMarginLeft = inset;
            AddThemeStyleboxOverride(state, centered);
        }
        _measuredText = Text;
        _measuredWidth = Size.X;
    }

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
