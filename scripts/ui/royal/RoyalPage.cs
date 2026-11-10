using System;
using Godot;

/// <summary>
/// Composes painted content into a canvas-sized layer from kit pieces: gold-rimmed sections, pictures with a
/// name plaque, engraved and book text, action buttons, cards and pagers. <see cref="RoyalBoard"/> builds its
/// boards with one; screens on a concept plate of their own (Multiplayer Challenges) compose their pages the same
/// way, so every board reads alike.
/// </summary>
public sealed class RoyalPage
{
    public RoyalPage(Control root) => Root = root;

    /// <summary>The layer everything is added to, in canvas units.</summary>
    public Control Root { get; }

    // ---- Surfaces -------------------------------------------------------------------------------------------

    public Panel Frame(string slice, Rect2 rect, float margin, bool center = true)
    {
        var panel = RoyalKit.Frame(slice, rect, margin, center);
        Root.AddChild(panel);
        return panel;
    }

    /// <summary>A dark, gold-rimmed section with an engraved heading; returns the room below the heading.</summary>
    public Rect2 Section(Rect2 rect, string heading = null, string glyph = null, string aside = null, bool warm = false)
    {
        Frame(warm ? "ach-card-ready" : "ach-card", rect, 14);
        if (heading == null) return rect.Grow(-14);
        var x = rect.Position.X + 20;
        if (glyph != null)
        {
            Image(glyph, new Rect2(x, rect.Position.Y + 14, 28, 28));
            x += 38;
        }
        var title = Place(RoyalText.Caps(heading, 18, RoyalBoard.Engraved, 700), new Rect2(x, rect.Position.Y + 12, rect.End.X - x - 20, 32));
        if (aside != null)
        {
            // The aside takes the room the heading leaves, so a long one shrinks instead of running over it.
            var left = x + Mathf.Ceil(title.TextWidth(title.FontSize)) + 24;
            Place(RoyalText.Serif(aside, 18, RoyalBoard.Soft, 600), new Rect2(left, rect.Position.Y + 12, rect.End.X - 20 - left, 32), HorizontalAlignment.Right);
        }
        Rule(new Rect2(rect.Position.X + 18, rect.Position.Y + 48, rect.Size.X - 36, 1));
        return new Rect2(rect.Position.X + 16, rect.Position.Y + 58, rect.Size.X - 32, rect.Size.Y - 72);
    }

    /// <summary>A hairline gold rule with a diamond at its centre.</summary>
    public void Rule(Rect2 line)
    {
        Root.AddChild(new ColorRect { Color = new Color(.93f, .77f, .45f, .32f), Position = line.Position, Size = new Vector2(line.Size.X, 1), MouseFilter = Control.MouseFilterEnum.Ignore });
        Image("result-diamond", new Rect2(line.GetCenter().X - 7, line.Position.Y - 7, 14, 15));
    }

    /// <summary>A painted picture in a gold rim, cropped to fill its window.</summary>
    public TextureRect Picture(Rect2 rect, Texture2D art, bool dim = false, bool rim = true)
    {
        var picture = new TextureRect { Texture = art, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            ClipContents = true, Position = rect.Position + new Vector2(3, 3), Size = rect.Size - new Vector2(6, 6), MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps, SelfModulate = dim ? new Color(.46f, .46f, .5f) : Colors.White };
        Root.AddChild(picture);
        if (rim) Frame("hub-card", rect, 10, false);
        return picture;
    }

    /// <summary>A shaded band across the foot of a picture, for its caption.</summary>
    public void Shade(Rect2 rect)
    {
        Root.AddChild(new TextureRect { Texture = ShadeTexture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            Position = rect.Position, Size = rect.Size, MouseFilter = Control.MouseFilterEnum.Ignore });
    }

    private static Texture2D _shade;
    private static Texture2D ShadeTexture => _shade ??= new GradientTexture2D
    {
        Gradient = new Gradient { Colors = new[] { new Color(0, 0, 0, 0), new Color(.02f, .02f, .03f, .86f) }, Offsets = new[] { 0f, 1f } },
        FillFrom = new Vector2(0, 0), FillTo = new Vector2(0, 1), Width = 4, Height = 64
    };

    /// <summary>The parchment name plaque that sits on a picture's top-left corner (Endless route names).</summary>
    public void Plaque(Vector2 at, string text, int size = 26, float height = 44, float maxWidth = float.MaxValue)
    {
        var label = RoyalText.Serif(text.Replace("'", "’"), size, RoyalBoard.Ink, 700);
        label.ShadowOffset = Vector2.Zero;
        var width = Mathf.Min(maxWidth, Mathf.Ceil(label.TextWidth(label.FontSize)) + 48);
        var rect = new Rect2(at, new Vector2(width, height));
        Root.AddChild(new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = Control.MouseFilterEnum.Ignore }
            .With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("endless-plaque", 14, 8, 40, 8))));
        Place(label, new Rect2(rect.Position.X + 22, rect.Position.Y, width - 30, height));
    }

    /// <summary>A gold track filled to <paramref name="fraction"/>.</summary>
    public void Meter(Rect2 rect, float fraction)
    {
        Frame("ach-track", rect, Mathf.Min(8, rect.Size.Y / 2));
        fraction = Mathf.Clamp(fraction, 0, 1);
        if (fraction <= 0) return;
        var fill = new Rect2(rect.Position + new Vector2(3, 3), new Vector2(Mathf.Max(rect.Size.Y - 6, (rect.Size.X - 6) * fraction), rect.Size.Y - 6));
        Frame("ach-fill-gold", fill, Mathf.Min(6, fill.Size.Y / 2));
    }

    // ---- Text -----------------------------------------------------------------------------------------------

    public T Place<T>(T label, Rect2 rect, HorizontalAlignment align = HorizontalAlignment.Left) where T : RoyalLabel
    {
        label.Align = align;
        label.Position = rect.Position; label.Size = rect.Size;
        Root.AddChild(label);
        return label;
    }

    public RoyalLabel Caps(string text, Rect2 rect, int size = 18, Color? ink = null, HorizontalAlignment align = HorizontalAlignment.Left, int weight = 700)
        => Place(RoyalText.Caps(text, size, ink ?? RoyalBoard.Engraved, weight), rect, align);

    public RoyalLabel Serif(string text, Rect2 rect, int size = 20, Color? ink = null, HorizontalAlignment align = HorizontalAlignment.Left, int weight = 600)
        => Place(RoyalText.Serif(text, size, ink ?? RoyalText.Cream, weight), rect, align);

    /// <summary>A large engraved gold heading ("Your Warband").</summary>
    public RoyalLabel Heading(string text, Rect2 rect, int size = 28, HorizontalAlignment align = HorizontalAlignment.Left)
        => Place(RoyalText.Title(text, size), rect, align);

    /// <summary>Wrapping book text fitted to a number of lines.</summary>
    public Label Paragraph(string text, Rect2 rect, int lines, int size = 18, Color? ink = null, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = RoyalText.Paragraph(text, size, ink ?? RoyalBoard.Soft, 500, align);
        RoyalText.Place(Root, label, rect);
        RoyalText.FitLines(label, lines);
        return label;
    }

    public TextureRect Image(string name, Rect2 rect, Color? tint = null)
    {
        var image = RoyalKit.Image(name, rect, tint);
        Root.AddChild(image);
        return image;
    }

    public TextureRect Image(Texture2D texture, Rect2 rect, Color? tint = null)
    {
        var image = new TextureRect { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Position = rect.Position, Size = rect.Size, MouseFilter = Control.MouseFilterEnum.Ignore, SelfModulate = tint ?? Colors.White,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
        Root.AddChild(image);
        return image;
    }

    /// <summary>A unit's painted figure fitted into a rect.</summary>
    public UnitFigure Figure(UnitDefinition unit, Rect2 rect)
    {
        var figure = new UnitFigure { Position = rect.Position, Size = rect.Size };
        figure.SetUnit(unit);
        Root.AddChild(figure);
        return figure;
    }

    // ---- Controls -------------------------------------------------------------------------------------------

    /// <summary>An action button on a kit surface: icon and engraved caption centred, an optional price after a rule.</summary>
    public RoyalButton Action(Rect2 rect, string caption, string glyph, Action run, RoyalBoard.Kind kind = RoyalBoard.Kind.Navy, bool enabled = true,
        string hint = null, string price = null, Texture2D priceIcon = null, int size = 18, string name = null)
    {
        var button = RoyalBoard.ActionButton(rect, caption, glyph, run, kind, enabled, hint, price, priceIcon, size, name);
        Root.AddChild(button);
        return button;
    }

    /// <summary>A square button showing only an icon, named for touch readers and reviews.</summary>
    public RoyalButton IconButton(Rect2 rect, string glyph, string name, Action run, RoyalBoard.Kind kind = RoyalBoard.Kind.Navy, bool enabled = true)
    {
        var button = RoyalBoard.ActionButton(rect, "", null, run, kind, enabled, name, name: name);
        var inset = Mathf.Round(rect.Size.Y * .24f);
        button.SetGlyph(RoyalKit.Texture(glyph), new Rect2(inset, inset, rect.Size.X - 2 * inset, rect.Size.Y - 2 * inset));
        Root.AddChild(button);
        return button;
    }

    /// <summary>A tappable card on a kit surface, lit while selected.</summary>
    public RoyalButton Card(Rect2 rect, string name, Action press, bool selected = false, string slice = "ach-card", string selectedSlice = "ach-card-ready", bool enabled = true)
    {
        var card = RoyalButton.Over(rect, name, press, 8);
        card.SetStates(RoyalKit.Slice(selected ? selectedSlice : slice, 14), 8);
        card.Disabled = !enabled;
        card.MouseFilter = Control.MouseFilterEnum.Pass;
        Root.AddChild(card);
        return card;
    }

    /// <summary>A page tab: navy at rest, lit gold when selected, with an optional emblem before its caption.</summary>
    public RoyalButton Tab(Rect2 rect, string caption, bool selected, Action select, string glyph = null)
    {
        var tab = Card(rect, caption, select, selected, "ach-tab", "ach-tab-selected");
        // Reviews find and name a screen's pages by their tabs' text.
        tab.Text = caption;
        tab.MarkTab(selected);
        var label = RoyalText.Caps(caption, 17, selected ? new Color("fff2cf") : new Color("cfc8c0"), 700);
        var width = label.TextWidth(label.FontSize) + 4;
        var lead = glyph == null ? 0 : 36;
        var x = rect.GetCenter().X - (lead + width) / 2;
        if (glyph != null) Image(glyph, new Rect2(x, rect.Position.Y + (rect.Size.Y - 32) / 2, 30, 32));
        Place(label, new Rect2(x + lead, rect.Position.Y, width, rect.Size.Y));
        return tab;
    }

    /// <summary>Previous and next chevrons at the ends of a row, with page diamonds centred between them.</summary>
    public void Pager(Rect2 row, int page, int pages, Action<int> turn, string noun = "page", bool dots = true)
    {
        if (pages <= 1) return;
        for (var side = -1; side <= 1; side += 2)
        {
            var step = side;
            var rect = new Rect2(side < 0 ? row.Position.X : row.End.X - 30, row.GetCenter().Y - 20, 30, 40);
            var pager = RoyalButton.Over(rect, side < 0 ? $"Previous {noun}" : $"Next {noun}", () => turn((page + step + pages) % pages), 6);
            pager.SetStates(RoyalKit.Slice("chevron-button", 8), 6);
            pager.SetGlyph(RoyalKit.Texture(side < 0 ? "icon-chevron-left" : "icon-chevron-right"), new Rect2(8, 9, 14, 22));
            Root.AddChild(pager);
        }
        if (!dots) return;
        var left = row.GetCenter().X - pages * 9 + 1;
        for (var i = 0; i < pages; i++)
            Image(i == page ? "route-dot-on" : "pip-off", new Rect2(left + i * 18, row.GetCenter().Y - 8, 16, 16));
    }
}
