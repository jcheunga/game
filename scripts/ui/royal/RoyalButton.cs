using System;
using Godot;

/// <summary>
/// A control over a button painted on a plate (or drawn from a kit slice). The plate already shows
/// the resting button, so states are light overlays: a warm sheen on hover, a press shade, a dim veil
/// when disabled and an optional selected surface for tabs and cards.
/// </summary>
public partial class RoyalButton : Button
{
    public RoyalLabel Caption { get; private set; }
    public TextureRect Glyph { get; private set; }
    private Color _captionInk;
    private bool _wired;

    public RoyalButton() { }

    /// <summary>Touch reach: on phones small painted buttons still take a full 56 px fingertip.</summary>
    public const float TouchSize = 56;
    public Rect2 TouchRect
    {
        get
        {
            var rect = new Rect2(Vector2.Zero, Size);
            if (!MobilePresentation.Enabled) return rect;
            var grow = new Vector2(Mathf.Max(0, TouchSize - Size.X), Mathf.Max(0, TouchSize - Size.Y)) / 2;
            return new Rect2(rect.Position - grow, rect.Size + grow * 2);
        }
    }

    public override bool _HasPoint(Vector2 point) => TouchRect.HasPoint(point);

    public static RoyalButton Over(Rect2 rect, string name, Action action, float corner = 5)
    {
        var button = new RoyalButton { Position = rect.Position, Size = rect.Size, AccessibilityName = name, TooltipText = name,
            FocusMode = FocusModeEnum.All, MouseDefaultCursorShape = CursorShape.PointingHand };
        button.SetStates(null, corner);
        if (action != null) button.Pressed += action;
        return button;
    }

    /// <summary>Resting surface (null when the plate paints it) plus the overlay states.</summary>
    public void SetStates(StyleBox resting, float corner = 5, StyleBox selected = null, StyleBox disabled = null)
    {
        StyleBox Overlay(Color fill, Color border, int width = 0)
        {
            var box = new StyleBoxFlat { BgColor = fill, BorderColor = border, AntiAliasing = true };
            box.SetCornerRadiusAll((int)corner); box.SetBorderWidthAll(width);
            return box;
        }
        StyleBox Stack(StyleBox under, StyleBox over) => under == null ? over : new StackedStyle(under, over);
        var rest = resting ?? new StyleBoxEmpty();
        AddThemeStyleboxOverride("normal", rest);
        AddThemeStyleboxOverride("hover", Stack(resting, Overlay(new Color(1f, .9f, .66f, .10f), new Color(1f, .86f, .55f, .55f), 1)));
        AddThemeStyleboxOverride("pressed", Stack(selected ?? resting, selected != null ? new StyleBoxEmpty() : Overlay(new Color(0, 0, 0, .22f), Colors.Transparent)));
        AddThemeStyleboxOverride("hover_pressed", Stack(selected ?? resting, Overlay(new Color(1f, .9f, .66f, .08f), Colors.Transparent)));
        AddThemeStyleboxOverride("disabled", Stack(disabled ?? resting, disabled != null ? new StyleBoxEmpty() : Overlay(new Color(.03f, .04f, .05f, .5f), Colors.Transparent)));
        var focus = Overlay(Colors.Transparent, new Color(1f, .87f, .56f, .9f), 2);
        AddThemeStyleboxOverride("focus", focus);
        foreach (var key in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color", "font_disabled_color" })
            AddThemeColorOverride(key, Colors.Transparent);
    }

    /// <summary>Marks a tab as the selected page; selected tabs keep their resting look when pressed.</summary>
    public void MarkTab(bool selected)
    {
        ToggleMode = true;
        SetPressedNoSignal(selected);
        if (selected)
        {
            AddThemeStyleboxOverride("pressed", GetThemeStylebox("normal"));
            AddThemeStyleboxOverride("hover_pressed", GetThemeStylebox("hover"));
        }
    }

    /// <summary>A live caption centred on (or placed in) the button.</summary>
    public RoyalLabel SetCaption(RoyalLabel label, Rect2? local = null)
    {
        Caption?.QueueFree();
        Caption = label;
        AddChild(label);
        var rect = local ?? new Rect2(Vector2.Zero, Size);
        label.Position = rect.Position; label.Size = rect.Size;
        _captionInk = label.Ink;
        Text = label.Text;
        WireStates();
        return label;
    }

    public TextureRect SetGlyph(Texture2D texture, Rect2 local, Color? tint = null)
    {
        Glyph?.QueueFree();
        Glyph = new TextureRect { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore, Position = local.Position, Size = local.Size, SelfModulate = tint ?? Colors.White };
        AddChild(Glyph);
        WireStates();
        return Glyph;
    }

    private void WireStates()
    {
        if (_wired) return;
        _wired = true;
        ButtonDown += () => Shift(1);
        ButtonUp += () => Shift(0);
        Draw += RefreshInk;
    }

    private float _shift;
    private void Shift(float amount)
    {
        var delta = amount - _shift; _shift = amount;
        if (Caption != null) Caption.Position += new Vector2(0, delta);
        if (Glyph != null) Glyph.Position += new Vector2(0, delta);
    }

    private void RefreshInk()
    {
        var dim = Disabled ? new Color(1, 1, 1, .55f) : Colors.White;
        if (Caption != null) { Caption.Modulate = dim; }
        if (Glyph != null) Glyph.Modulate = dim;
    }
}

/// <summary>Draws one style box over another (a painted surface plus a state overlay).</summary>
public partial class StackedStyle : StyleBox
{
    private readonly StyleBox _under, _over;
    public StackedStyle() { }
    public StackedStyle(StyleBox under, StyleBox over)
    {
        _under = under; _over = over;
        ContentMarginLeft = under.GetMargin(Side.Left); ContentMarginRight = under.GetMargin(Side.Right);
        ContentMarginTop = under.GetMargin(Side.Top); ContentMarginBottom = under.GetMargin(Side.Bottom);
    }
    public override void _Draw(Rid canvasItem, Rect2 rect)
    {
        _under?.Draw(canvasItem, rect);
        _over?.Draw(canvasItem, rect);
    }
}
