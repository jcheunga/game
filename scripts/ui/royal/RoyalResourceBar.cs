using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// The concept's resource strip: icon and amount pairs with brass dividers between them. The pieces
/// are laid out from the real text widths, so large balances push the dividers along (and stretch the
/// parchment) instead of running into them. Without a bar of its own (the hub, whose plate paints the
/// strip) it only spaces the pairs and shrinks the amounts if they would overflow.
/// </summary>
public partial class RoyalResourceBar : Control
{
    private sealed class Pair { public TextureRect Icon; public RoyalLabel Value; public RoyalButton Hotspot; public Rect2 IconRect; }
    private readonly List<Pair> _pairs = new();
    private readonly List<TextureRect> _dividers = new();
    private Panel _bar;
    private RoyalSpec _spec;
    private string _prefix;
    /// <summary>Bar rect on the canvas: left, top and height are fixed; the width grows with the text.</summary>
    public Rect2 BarRect;
    /// <summary>When set, draws the parchment bar and dividers; otherwise the plate supplies the strip.</summary>
    public bool DrawsBar = true;
    public float MaxWidth = 420;
    public Color Ink = new("1b1610");
    private int? _baseSize;

    public RoyalResourceBar() { MouseFilter = MouseFilterEnum.Ignore; Position = Vector2.Zero; Size = RoyalArt.Canvas; }

    /// <summary>Adds one pair; icon and value positions come from the spec ids <c>{prefix}.{key}.icon</c> / <c>.value</c>.</summary>
    public RoyalButton Add(RoyalSpec spec, string prefix, string key, Texture2D icon, string hint, Action open)
    {
        _spec = spec; _prefix = prefix;
        if (DrawsBar && _bar == null)
        {
            _bar = new Panel { MouseFilter = MouseFilterEnum.Ignore };
            var slice = RoyalKit.Slice("res-bar", 30, 0, 30, 0);
            _bar.AddThemeStyleboxOverride("panel", slice);
            AddChild(_bar);
        }
        var iconRect = spec.Rect($"{prefix}.{key}.icon");
        var pair = new Pair
        {
            IconRect = iconRect,
            Icon = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Texture = icon, Size = iconRect.Size, MouseFilter = MouseFilterEnum.Ignore },
            Value = spec.Label($"{prefix}.{key}.value", "", 200, Ink),
        };
        pair.Value.ShrinkToFit = false;
        if (DrawsBar) pair.Value.ShadowOffset = Vector2.Zero;
        pair.Hotspot = RoyalButton.Over(new Rect2(Vector2.Zero, Vector2.One), hint, open, 6);
        AddChild(pair.Icon); AddChild(pair.Value); AddChild(pair.Hotspot);
        _pairs.Add(pair);
        if (DrawsBar && _pairs.Count > 1)
        {
            var divider = RoyalKit.Image("res-divider", new Rect2(0, 22, 10, 48));
            _dividers.Add(divider); AddChild(divider);
        }
        return pair.Hotspot;
    }

    public void SetValues(params string[] values)
    {
        for (var i = 0; i < _pairs.Count && i < values.Length; i++) _pairs[i].Value.Text = values[i];
        Layout();
    }

    private void Layout()
    {
        if (_pairs.Count == 0) return;
        // Spacing measured on the concept: icon to amount 12, amount to divider 10, divider to icon 12.
        const float IconGap = 12, BeforeDivider = 10, DividerWidth = 10, AfterDivider = 12, EndPadding = 22;
        var size = _pairs[0].Value.FontSize;
        float Measure()
        {
            var width = 0f;
            for (var i = 0; i < _pairs.Count; i++)
            {
                width += _pairs[i].IconRect.Size.X + IconGap + _pairs[i].Value.TextWidth(size);
                if (i < _pairs.Count - 1) width += DrawsBar ? BeforeDivider + DividerWidth + AfterDivider : 26;
            }
            return width;
        }
        var start = _pairs[0].IconRect.Position.X;
        var limit = (DrawsBar ? MaxWidth : BarRect.Size.X) - (start - BarRect.Position.X) - EndPadding;
        size = _baseSize ??= size;
        foreach (var pair in _pairs) pair.Value.FontSize = size;
        while (size > 12 && Measure() > limit) { size--; foreach (var pair in _pairs) pair.Value.FontSize = size; }
        var x = start;
        for (var i = 0; i < _pairs.Count; i++)
        {
            var pair = _pairs[i];
            pair.Icon.Position = new Vector2(x, pair.IconRect.Position.Y);
            var left = x;
            x += pair.IconRect.Size.X + IconGap;
            var width = pair.Value.TextWidth(size);
            pair.Value.Position = new Vector2(x, pair.Value.Position.Y);
            pair.Value.Size = new Vector2(width + 4, pair.Value.Size.Y);
            x += width;
            pair.Hotspot.Position = new Vector2(left - 6, pair.IconRect.Position.Y - 4);
            pair.Hotspot.Size = new Vector2(x - left + 12, pair.IconRect.Size.Y + 8);
            if (i < _pairs.Count - 1)
            {
                if (DrawsBar)
                {
                    x += BeforeDivider;
                    _dividers[i].Position = new Vector2(x, 22);
                    x += DividerWidth + AfterDivider;
                }
                else x += 26;
            }
        }
        if (_bar != null)
        {
            var width = Mathf.Max(BarRect.Size.X, x + EndPadding - BarRect.Position.X);
            _bar.Position = BarRect.Position;
            _bar.Size = new Vector2(width, BarRect.Size.Y);
        }
    }
}
