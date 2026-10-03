using Godot;

/// <summary>Centers an icon and label as one group without changing style margins.</summary>
public partial class RealmButton : Button
{
    public bool CenterIconAndText { get; set; }
    private TextureRect _groupIcon;
    private Label _groupLabel;
    private Color _normalInk, _disabledInk;
    private Color _iconInk = Colors.White;
    private bool? _lastDisabled;
    public void SetPresentation(Font font, Color ink, Color disabled)
    {
        _normalInk = ink; _disabledInk = disabled; _lastDisabled = null;
        _iconInk = !HasMeta("painted_resource_icon") && ink.R < .5f ? ink : Colors.White;
        if (_groupLabel != null)
        {
            _groupLabel.AddThemeFontOverride("font", font);
            _groupLabel.AddThemeFontSizeOverride("font_size", 20);
            return;
        }
        foreach (var key in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" }) AddThemeColorOverride(key, ink);
        AddThemeColorOverride("font_disabled_color", disabled);
    }
    public override void _Ready()
    {
        if (!CenterIconAndText) return;
        _normalInk = GetThemeColor("font_color"); _disabledInk = GetThemeColor("font_disabled_color");
        foreach (var key in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color", "font_disabled_color",
            "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_hover_pressed_color", "icon_focus_color", "icon_disabled_color" })
            AddThemeColorOverride(key, Colors.Transparent);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(center); center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10); center.AddChild(row);
        var iconSize = GetThemeConstant("icon_max_width");
        _groupIcon = new TextureRect { CustomMinimumSize = new Vector2(iconSize,iconSize), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        _groupLabel = new Label { MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        _groupLabel.AddThemeFontOverride("font", GetThemeFont("font"));
        _groupLabel.AddThemeFontSizeOverride("font_size", GetThemeFontSize("font_size"));
        if (IconAlignment == HorizontalAlignment.Right) { row.AddChild(_groupLabel); row.AddChild(_groupIcon); }
        else { row.AddChild(_groupIcon); row.AddChild(_groupLabel); }
        RefreshGroup();
    }
    public override void _Process(double delta) { if (_groupLabel != null) RefreshGroup(); }
    private void RefreshGroup()
    {
        if (_groupLabel.Text != Text) _groupLabel.Text = Text;
        _groupLabel.Visible = Text.Length > 0;
        if (_groupIcon.Texture != Icon) _groupIcon.Texture = Icon;
        _groupIcon.Visible = Icon != null;
        if (_lastDisabled != Disabled)
        {
            _lastDisabled = Disabled;
            _groupLabel.AddThemeColorOverride("font_color", Disabled ? _disabledInk : _normalInk);
            _groupIcon.Modulate = Disabled ? new Color(1,1,1,.4f) : _iconInk;
        }
    }
}
