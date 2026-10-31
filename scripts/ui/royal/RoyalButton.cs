using System;
using Godot;

/// <summary>
/// A control over a button painted on a plate (or drawn from a kit slice). The plate already shows
/// the resting button, so states are light overlays: no hover highlight, a dim veil when disabled and an
/// optional selected surface for tabs and cards. A held press comes from <see cref="ButtonFeel"/>: a kit surface
/// darkens and the whole button sinks, while painted plate art is pressed in place.
/// </summary>
public partial class RoyalButton : Button
{
    public RoyalLabel Caption { get; private set; }
    public TextureRect Glyph { get; private set; }
    private Color _captionInk;
    private bool _wired;

    public RoyalButton()
    {
        // The button's own text is never drawn (the caption is a RoyalLabel); it stays as a name for tests and
        // accessibility. It must not give the button a minimum size, or Godot stretches the button past its rect
        // while the caption stays centred in the rect it was given.
        ClipText = true;
        AddThemeFontSizeOverride("font_size", 1);
    }

    /// <summary>Small painted buttons still take a fingertip of the screen (<see cref="TouchReach"/>).</summary>
    public override bool _HasPoint(Vector2 point) => TouchReach.Has(this, point);

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
        // Captions and glyphs are placed by hand, so a surface's content margins must not size the button either.
        var rest = new MarginlessStyle(resting ?? new StyleBoxEmpty());
        // A kit surface darkens while held (ButtonFeel sinks it); painted plate art is pressed in by ButtonFeel itself.
        var held = new MarginlessStyle(selected ?? (resting == null ? new StyleBoxEmpty() : Stack(resting, Overlay(new Color(0, 0, 0, .2f), Colors.Transparent))));
        ButtonFeel.PaintBelow(this, resting == null && selected == null ? corner : null);
        AddThemeStyleboxOverride("normal", rest);
        AddThemeStyleboxOverride("hover", rest);
        AddThemeStyleboxOverride("pressed", held);
        AddThemeStyleboxOverride("hover_pressed", held);
        AddThemeStyleboxOverride("disabled", new MarginlessStyle(Stack(disabled ?? resting, disabled != null ? new StyleBoxEmpty() : Overlay(new Color(.03f, .04f, .05f, .5f), Colors.Transparent))));
        var focus = Overlay(Colors.Transparent, new Color(1f, .87f, .56f, .9f), 2);
        AddThemeStyleboxOverride("focus", focus);
        foreach (var key in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color", "font_disabled_color" })
            AddThemeColorOverride(key, Colors.Transparent);
    }

    /// <summary>Marks a tab as the selected page; selected tabs keep their resting look when pressed.</summary>
    public void MarkTab(bool selected)
    {
        // Reviews find a screen's pages by this mark, as they find RealmUi tab rows by "realm_tabs".
        SetMeta("royal_tab", true);
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
        // Engraved captions read in full capitals on every button, whatever case the caller's name is in.
        if (RoyalFonts.IsDisplay(label.Font)) label.AllCaps = true;
        // Dark lettering on a light plate (gold buttons) reads as doubled with a dark drop shadow.
        if (!label.Gold && label.Ink.Luminance < .45f) label.ShadowOffset = Vector2.Zero;
        AddChild(label);
        var rect = local ?? new Rect2(Vector2.Zero, Size);
        label.Position = rect.Position; label.Size = rect.Size;
        _captionInk = label.Ink;
        Text = label.Text;
        WireStates();
        return label;
    }

    /// <summary>
    /// Lays an action out as one centred row: icon, caption and, after an engraved divider, an optional price
    /// (its icon, then its amount). The price dims with the button when it is disabled. Arranging again (a caption
    /// that changes, such as Buy to Confirm) replaces the previous row.
    /// </summary>
    public void Arrange(RoyalLabel caption, Texture2D glyph = null, Vector2 glyphSize = default,
        RoyalLabel price = null, Texture2D priceIcon = null, Vector2 priceIconSize = default, float gap = 12, float rule = 20)
    {
        if (RoyalFonts.IsDisplay(caption.Font)) caption.AllCaps = true;
        if (GetNodeOrNull<Control>(PriceRow) is { } previous) { RemoveChild(previous); previous.QueueFree(); }
        if (glyph == null && Glyph != null) { Glyph.QueueFree(); Glyph = null; }
        var height = Size.Y;
        float words = 0, lead = 0, amount = 0, priced = 0;
        void Measure()
        {
            words = Mathf.Ceil(caption.TextWidth(caption.FontSize));
            lead = glyph == null ? 0 : glyphSize.X + gap;
            amount = price == null ? 0 : Mathf.Ceil(price.TextWidth(price.FontSize));
            priced = price == null ? 0 : rule * 2 + 2 + (priceIcon == null ? 0 : priceIconSize.X + 6) + amount;
        }
        Measure();
        // The row keeps inside the plate's rims: a long caption steps down (with its price) as far as the phone
        // floor, then gives up its icon.
        var room = Size.X - 2 * Mathf.Clamp(height * .22f, 8, 16);
        while (Size.X > 0 && lead + words + priced > room && caption.FontSize > caption.MinimumFontSize)
        {
            caption.FontSize--;
            if (price != null && price.FontSize > price.MinimumFontSize) price.FontSize--;
            Measure();
        }
        if (Size.X > 0 && lead + words + priced > room && glyph != null) { glyph = null; Measure(); }
        if (glyph == null && Glyph != null) { Glyph.QueueFree(); Glyph = null; }
        var x = Mathf.Round((Size.X - lead - words - priced) / 2);
        if (glyph != null) SetGlyph(glyph, new Rect2(x, Mathf.Round((height - glyphSize.Y) / 2), glyphSize));
        x += lead;
        // Spec labels carry the concept's measured baseline; in a row the cap band centres on the button instead.
        caption.Align = HorizontalAlignment.Left; caption.ShrinkToFit = false; caption.Baseline = float.NaN;
        SetCaption(caption, new Rect2(x, 0, words + 2, height));
        if (price == null) return;
        var row = new Control { Name = PriceRow, Position = new Vector2(x + words + rule, 0), Size = new Vector2(priced - rule, height), MouseFilter = MouseFilterEnum.Ignore };
        row.AddChild(new ColorRect { Color = new Color(0, 0, 0, .45f), Position = new Vector2(0, height / 2 - 16), Size = new Vector2(1, 32), MouseFilter = MouseFilterEnum.Ignore });
        row.AddChild(new ColorRect { Color = new Color(.95f, .82f, .55f, .4f), Position = new Vector2(1, height / 2 - 16), Size = new Vector2(1, 32), MouseFilter = MouseFilterEnum.Ignore });
        var left = 2 + rule;
        if (priceIcon != null)
        {
            row.AddChild(new TextureRect { Texture = priceIcon, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Position = new Vector2(left, Mathf.Round((height - priceIconSize.Y) / 2)), Size = priceIconSize, MouseFilter = MouseFilterEnum.Ignore,
                TextureFilter = TextureFilterEnum.LinearWithMipmaps });
            left += priceIconSize.X + 6;
        }
        price.Align = HorizontalAlignment.Left; price.ShrinkToFit = false; price.Baseline = float.NaN;
        if (RoyalFonts.IsDisplay(price.Font)) price.AllCaps = true;
        row.AddChild(price); price.Position = new Vector2(left, 0); price.Size = new Vector2(amount + 2, height);
        AddChild(row);
    }

    private const string PriceRow = "Price";

    public TextureRect SetGlyph(Texture2D texture, Rect2 local, Color? tint = null)
    {
        Glyph?.QueueFree();
        Glyph = new TextureRect { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore, Position = local.Position, Size = local.Size, SelfModulate = tint ?? Colors.White,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps };
        AddChild(Glyph);
        WireStates();
        return Glyph;
    }

    private void WireStates()
    {
        if (_wired) return;
        _wired = true;
        Draw += RefreshInk;
    }

    private void RefreshInk()
    {
        var dim = Disabled ? new Color(1, 1, 1, .55f) : Colors.White;
        if (Caption != null) { Caption.Modulate = dim; }
        if (Glyph != null) Glyph.Modulate = dim;
        if (GetNodeOrNull<Control>(PriceRow) is { } price) price.Modulate = dim;
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

/// <summary>Draws another style box but reports no content margins, so it never sets a button's minimum size.</summary>
public partial class MarginlessStyle : StyleBox
{
    private readonly StyleBox _surface;
    public StyleBox Surface => _surface;
    public MarginlessStyle() { }
    public MarginlessStyle(StyleBox surface) => _surface = surface;
    public override void _Draw(Rid canvasItem, Rect2 rect) => _surface?.Draw(canvasItem, rect);
}
