using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// The concept's resource strip: icon and amount pairs with brass dividers between them. Each amount
/// keeps room for its widest expected value (five-figure gold, three-figure stars), so the dividers stay
/// put as balances change; anything wider still pushes them along (and stretches the parchment). Without
/// a bar of its own (the hub, whose plate paints the strip) it only spaces the pairs and shrinks the
/// amounts if they would overflow.
/// </summary>
public partial class RoyalResourceBar : Control
{
    private sealed class Pair { public TextureRect Icon; public RoyalLabel Value; public RoyalButton Hotspot; public Rect2 IconRect; public string Room = ""; }
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
    /// <summary>When set, the strip keeps its right end here as amounts widen it (the home bar beside the settings ring).</summary>
    public float RightEdge = -1;
    /// <summary>Spacing and icon size. Defaults follow the concept; the home strip uses a compact set to fit beside the zone plaque.</summary>
    public float IconScale = 1, BeforeDivider = 10, AfterDivider = 12, EndPadding = 22, StartInset = -1;
    private float _centerY = float.NaN;
    public Color Ink = new("1b1610");
    private int? _baseSize;

    public RoyalResourceBar() { MouseFilter = MouseFilterEnum.Ignore; Position = Vector2.Zero; Size = RoyalArt.Canvas; }

    /// <summary>Adds one pair; icon and value positions come from the spec ids <c>{prefix}.{key}.icon</c> / <c>.value</c>.
    /// <paramref name="room"/> is the widest value the slot keeps space for, such as "00,000".</summary>
    public RoyalButton Add(RoyalSpec spec, string prefix, string key, Texture2D icon, string hint, Action open, string room = "")
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
        // Icons and amounts centre on the parchment between the strip's rims.
        if (float.IsNaN(_centerY)) _centerY = spec.Has($"{prefix}.parchment") ? spec.Rect($"{prefix}.parchment").GetCenter().Y : BarRect.GetCenter().Y;
        var pair = new Pair
        {
            IconRect = iconRect, Room = room,
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
        // Amounts sit close to their icons; dividers keep the concept's spacing.
        const float IconGap = 6, DividerWidth = 10;
        var size = _pairs[0].Value.FontSize;
        float Slot(Pair pair, int at) => Mathf.Max(pair.Value.TextWidth(at), pair.Value.TextWidth(pair.Room, at));
        float Measure()
        {
            var width = 0f;
            for (var i = 0; i < _pairs.Count; i++)
            {
                width += IconSize(_pairs[i]).X + IconGap + Slot(_pairs[i], size);
                if (i < _pairs.Count - 1) width += DrawsBar ? BeforeDivider + DividerWidth + AfterDivider : 26;
            }
            return width;
        }
        var start = StartInset >= 0 ? BarRect.Position.X + StartInset : _pairs[0].IconRect.Position.X;
        var limit = (DrawsBar ? MaxWidth : BarRect.Size.X) - (start - BarRect.Position.X) - EndPadding;
        size = _baseSize ??= size;
        foreach (var pair in _pairs) pair.Value.FontSize = size;
        while (size > 12 && Measure() > limit) { size--; foreach (var pair in _pairs) pair.Value.FontSize = size; }
        var x = start;
        for (var i = 0; i < _pairs.Count; i++)
        {
            var pair = _pairs[i];
            var iconSize = IconSize(pair);
            pair.Icon.Size = iconSize;
            pair.Icon.Position = new Vector2(x, IconTop(pair));
            pair.Value.Baseline = _centerY - pair.Value.Position.Y + pair.Value.FontSize * pair.Value.CapRatio / 2;
            var left = x;
            x += iconSize.X + IconGap;
            var width = Slot(pair, size);
            pair.Value.Position = new Vector2(x, pair.Value.Position.Y);
            pair.Value.Size = new Vector2(width + 4, pair.Value.Size.Y);
            x += width;
            pair.Hotspot.Position = new Vector2(left - 6, _centerY - iconSize.Y / 2 - 4);
            pair.Hotspot.Size = new Vector2(x - left + 12, iconSize.Y + 8);
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
        var barWidth = Mathf.Max(BarRect.Size.X, x + EndPadding - BarRect.Position.X);
        if (_bar != null)
        {
            _bar.Position = BarRect.Position;
            _bar.Size = new Vector2(barWidth, BarRect.Size.Y);
        }
        Position = new Vector2(RightEdge >= 0 ? RightEdge - (BarRect.Position.X + barWidth) : 0, 0);
    }

    // Painted icons sit at different heights in their cells (the star and rations ride high), so each is
    // centred on its drawn pixels rather than its cell.
    private static readonly Dictionary<Texture2D, float> DrawnCenters = new();
    private Vector2 IconSize(Pair pair) => pair.IconRect.Size * IconScale;

    private float IconTop(Pair pair)
    {
        var box = IconSize(pair);
        var texture = pair.Icon.Texture;
        if (texture == null) return _centerY - box.Y / 2;
        if (!DrawnCenters.TryGetValue(texture, out var drawn))
        {
            var image = texture.GetImage();
            var used = image == null || image.IsEmpty() ? new Rect2I(Vector2I.Zero, (Vector2I)texture.GetSize()) : image.GetUsedRect();
            DrawnCenters[texture] = drawn = (used.Position.Y + used.Size.Y / 2f) / Mathf.Max(1, image?.GetHeight() ?? texture.GetHeight());
        }
        var size = texture.GetSize();
        var scale = Mathf.Min(box.X / size.X, box.Y / size.Y);
        var inset = (box.Y - size.Y * scale) / 2;
        return _centerY - inset - drawn * size.Y * scale;
    }
}
