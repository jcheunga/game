using System;
using Godot;

/// <summary>
/// A painted board for the activities that had no concept screen of their own (tower, expeditions, guild,
/// arena and the rest). The plate is a plain board repainted from a concept plate (art/royal/boards.py): frame,
/// navy header with its castle skyline, wooden board and the props spilling over its corners. Each screen adds
/// a gold emblem and title, then composes its sections from kit pieces at canvas positions, as the concept
/// screens do. Everything fits on the canvas: long lists page instead of scrolling.
/// </summary>
public abstract partial class RoyalBoard : RoyalScreen
{
    public enum Look { Banners, Camp }

    /// <summary>
    /// Where a board's painted parts are, in canvas units. <see cref="Footer"/> is the row for Back and the main
    /// action, clear of the props in the bottom corners; <see cref="Props"/> is the corner they cover (keep content
    /// out of it) and <see cref="Notch"/> the top-left corner a hanging banner covers.
    /// </summary>
    protected readonly record struct Geometry(Rect2 Body, Rect2 Footer, Rect2 Props, Rect2 Notch, Vector2 TitlePen, int TitleSize,
        Rect2 Emblem, Rect2 Close, float HeaderRight);

    // Measured on the installed plates (board-banners from the achievements plate, board-camp from season's).
    private static Geometry Measure(Look look) => look switch
    {
        Look.Camp => new(new Rect2(56, 132, 1172, 552), new Rect2(404, 628, 820, 52), new Rect2(0, 552, 394, 168), new Rect2(),
            new Vector2(160, 93), 50, new Rect2(60, 30, 88, 86), new Rect2(1185, 26, 54, 54), 1170),
        _ => new(new Rect2(110, 132, 1072, 526), new Rect2(150, 606, 978, 50), new Rect2(0, 606, 140, 114), new Rect2(56, 120, 104, 70),
            new Vector2(282, 97), 54, new Rect2(186, 38, 86, 80), new Rect2(1160, 40, 53, 54), 1146)
    };

    public static readonly Color Ink = new("140a04");
    public static readonly Color Engraved = new("fbe7bb");
    public static readonly Color Soft = new("d9ccb2");
    public static readonly Color Green = new("9fd49a");

    protected Control Live { get; private set; }
    /// <summary>The canvas corners the board's painted props and banner cover, which live content keeps clear of.</summary>
    public Rect2[] PaintedCorners => new[] { Board.Props, Board.Notch };
    protected Geometry Board { get; }
    protected string Title { get; set; }
    private readonly string _emblem;
    private string _notice = "";
    private Rect2 _noticeRect;
    private float _chipRight;

    protected RoyalBoard(string title, string emblem, Look look = Look.Banners)
    {
        Title = title;
        _emblem = emblem;
        Board = Measure(look);
        PlateName = look == Look.Camp ? "board-camp" : "board-banners";
    }

    protected override void Build()
    {
        Live = Layer("Live");
        Refresh();
    }

    /// <summary>Rebuilds the live layer from the current state; the plate stays.</summary>
    public void Refresh()
    {
        if (Live == null) return;
        RoyalUiTools.Clear(Live);
        _chipRight = Board.HeaderRight;
        _noticeRect = new Rect2();
        var title = RoyalText.Title(Title, Board.TitleSize);
        title.Position = new Vector2(Board.TitlePen.X, Board.TitlePen.Y - Board.TitleSize * 1.1f);
        title.Size = new Vector2(Board.HeaderRight - Board.TitlePen.X - 360, Board.TitleSize * 1.6f);
        title.Baseline = Board.TitleSize * 1.1f;
        Live.AddChild(title);
        Live.AddChild(RoyalKit.Image("emblem-" + _emblem, Board.Emblem));
        var close = RoyalButton.Over(Board.Close, "Close panel", Close, 6);
        close.SetGlyph(RoyalKit.Texture("icon-close-x"), new Rect2(16, 16, Board.Close.Size.X - 32, Board.Close.Size.Y - 32));
        Live.AddChild(close);
        Compose();
        if (_noticeRect.Size.X > 0 && _notice.Length > 0) ShowNotice();
    }

    protected abstract void Compose();

    // ---- Header ---------------------------------------------------------------------------------------------

    /// <summary>A value plaque on the header, laid right to left before the close button.</summary>
    protected RoyalLabel Chip(string icon, string value, string hint, Texture2D picture = null)
    {
        var text = RoyalText.Caps(value, 20, new Color("ebeaeb"), 700);
        var width = Mathf.Ceil(text.TextWidth(text.FontSize)) + 72;
        var rect = new Rect2(_chipRight - width, Board.Close.Position.Y + (Board.Close.Size.Y - 46) / 2, width, 46);
        _chipRight = rect.Position.X - 12;
        var plaque = Frame("value-box", rect, 10);
        plaque.TooltipText = hint; plaque.MouseFilter = MouseFilterEnum.Pass;
        var art = picture ?? RoyalKit.Texture(icon);
        Live.AddChild(new TextureRect { Texture = art, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Position = rect.Position + new Vector2(12, 7), Size = new Vector2(34, 32), MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps });
        return Place(text, new Rect2(rect.Position.X + 54, rect.Position.Y, width - 62, rect.Size.Y));
    }

    // ---- Surfaces -------------------------------------------------------------------------------------------

    protected Panel Frame(string slice, Rect2 rect, float margin, bool center = true)
    {
        var panel = RoyalKit.Frame(slice, rect, margin, center);
        Live.AddChild(panel);
        return panel;
    }

    /// <summary>A dark, gold-rimmed section with an engraved heading; returns the room below the heading.</summary>
    protected Rect2 Section(Rect2 rect, string heading = null, string glyph = null, string aside = null, bool warm = false)
    {
        Frame(warm ? "ach-card-ready" : "ach-card", rect, 14);
        if (heading == null) return rect.Grow(-14);
        var x = rect.Position.X + 20;
        if (glyph != null)
        {
            Image(glyph, new Rect2(x, rect.Position.Y + 14, 28, 28));
            x += 38;
        }
        Place(RoyalText.Caps(heading, 18, Engraved, 700), new Rect2(x, rect.Position.Y + 12, rect.End.X - x - 20, 32));
        if (aside != null) Place(RoyalText.Serif(aside, 18, Soft, 600), new Rect2(rect.Position.X + 20, rect.Position.Y + 12, rect.Size.X - 40, 32), HorizontalAlignment.Right);
        Rule(new Rect2(rect.Position.X + 18, rect.Position.Y + 48, rect.Size.X - 36, 1));
        return new Rect2(rect.Position.X + 16, rect.Position.Y + 58, rect.Size.X - 32, rect.Size.Y - 72);
    }

    /// <summary>A hairline gold rule with a diamond at its centre.</summary>
    protected void Rule(Rect2 line)
    {
        Live.AddChild(new ColorRect { Color = new Color(.93f, .77f, .45f, .32f), Position = line.Position, Size = new Vector2(line.Size.X, 1), MouseFilter = MouseFilterEnum.Ignore });
        Image("result-diamond", new Rect2(line.GetCenter().X - 7, line.Position.Y - 7, 14, 15));
    }

    /// <summary>A painted picture in a gold rim, cropped to fill its window.</summary>
    protected TextureRect Picture(Rect2 rect, Texture2D art, bool dim = false, bool rim = true)
    {
        var picture = new TextureRect { Texture = art, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            ClipContents = true, Position = rect.Position + new Vector2(3, 3), Size = rect.Size - new Vector2(6, 6), MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps, SelfModulate = dim ? new Color(.46f, .46f, .5f) : Colors.White };
        Live.AddChild(picture);
        if (rim) Frame("hub-card", rect, 10, false);
        return picture;
    }

    /// <summary>A shaded band across the foot of a picture, for its caption.</summary>
    protected void Shade(Rect2 rect)
    {
        Live.AddChild(new TextureRect { Texture = ShadeTexture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Ignore });
    }

    private static Texture2D _shade;
    private static Texture2D ShadeTexture => _shade ??= new GradientTexture2D
    {
        Gradient = new Gradient { Colors = new[] { new Color(0, 0, 0, 0), new Color(.02f, .02f, .03f, .86f) }, Offsets = new[] { 0f, 1f } },
        FillFrom = new Vector2(0, 0), FillTo = new Vector2(0, 1), Width = 4, Height = 64
    };

    /// <summary>The parchment name plaque that sits on a picture's top-left corner (Endless route names).</summary>
    protected void Plaque(Vector2 at, string text, int size = 26, float height = 44)
    {
        var label = RoyalText.Serif(text.Replace("'", "’"), size, Ink, 700);
        label.ShadowOffset = Vector2.Zero;
        var width = Mathf.Ceil(label.TextWidth(label.FontSize)) + 48;
        var rect = new Rect2(at, new Vector2(width, height));
        Live.AddChild(new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Ignore }
            .With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("endless-plaque", 14, 8, 40, 8))));
        Place(label, new Rect2(rect.Position.X + 22, rect.Position.Y, width - 30, height));
    }

    /// <summary>A gold track filled to <paramref name="fraction"/>.</summary>
    protected void Meter(Rect2 rect, float fraction)
    {
        Frame("ach-track", rect, Mathf.Min(8, rect.Size.Y / 2));
        fraction = Mathf.Clamp(fraction, 0, 1);
        if (fraction <= 0) return;
        var fill = new Rect2(rect.Position + new Vector2(3, 3), new Vector2(Mathf.Max(rect.Size.Y - 6, (rect.Size.X - 6) * fraction), rect.Size.Y - 6));
        Frame("ach-fill-gold", fill, Mathf.Min(6, fill.Size.Y / 2));
    }

    // ---- Text -----------------------------------------------------------------------------------------------

    protected T Place<T>(T label, Rect2 rect, HorizontalAlignment align = HorizontalAlignment.Left) where T : RoyalLabel
    {
        label.Align = align;
        label.Position = rect.Position; label.Size = rect.Size;
        Live.AddChild(label);
        return label;
    }

    protected RoyalLabel Caps(string text, Rect2 rect, int size = 18, Color? ink = null, HorizontalAlignment align = HorizontalAlignment.Left, int weight = 700)
        => Place(RoyalText.Caps(text, size, ink ?? Engraved, weight), rect, align);

    protected RoyalLabel Serif(string text, Rect2 rect, int size = 20, Color? ink = null, HorizontalAlignment align = HorizontalAlignment.Left, int weight = 600)
        => Place(RoyalText.Serif(text, size, ink ?? RoyalText.Cream, weight), rect, align);

    /// <summary>A large engraved gold heading ("Your Warband").</summary>
    protected RoyalLabel Heading(string text, Rect2 rect, int size = 28, HorizontalAlignment align = HorizontalAlignment.Left)
        => Place(RoyalText.Title(text, size), rect, align);

    /// <summary>Wrapping book text fitted to a number of lines.</summary>
    protected Label Paragraph(string text, Rect2 rect, int lines, int size = 18, Color? ink = null, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = RoyalText.Paragraph(text, size, ink ?? Soft, 500, align);
        RoyalText.Place(Live, label, rect);
        RoyalText.FitLines(label, lines);
        return label;
    }

    protected TextureRect Image(string name, Rect2 rect, Color? tint = null)
    {
        var image = RoyalKit.Image(name, rect, tint);
        Live.AddChild(image);
        return image;
    }

    protected TextureRect Image(Texture2D texture, Rect2 rect, Color? tint = null)
    {
        var image = new TextureRect { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Ignore, SelfModulate = tint ?? Colors.White,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps };
        Live.AddChild(image);
        return image;
    }

    // ---- Controls -------------------------------------------------------------------------------------------

    public enum Kind { Gold, Navy, Slate, Claim, Claimed }

    /// <summary>An action button on a kit surface: icon and engraved caption centred, an optional price after a rule.</summary>
    protected RoyalButton Action(Rect2 rect, string caption, string glyph, Action run, Kind kind = Kind.Navy, bool enabled = true,
        string hint = null, string price = null, Texture2D priceIcon = null, int size = 18, string name = null)
    {
        var button = RoyalButton.Over(rect, name ?? caption, run, 6);
        var (slice, margin) = kind switch
        {
            Kind.Gold => ("button-gold", 14f), Kind.Claim => ("ach-claim", 14f), Kind.Claimed => ("ach-claimed", 14f),
            Kind.Slate => ("button-slate", 14f), _ => ("button-navy", 12f)
        };
        button.SetStates(RoyalKit.Slice(slice, margin), 6);
        var light = kind is Kind.Gold or Kind.Claim;
        var ink = light ? Ink : new Color("fbf3cf");
        var label = RoyalText.Caps(caption, size, ink, 700);
        var icon = glyph == null ? null : RoyalKit.Texture(glyph);
        var iconSize = new Vector2(Mathf.Round(rect.Size.Y * .52f), Mathf.Round(rect.Size.Y * .52f));
        var amount = price == null ? null : RoyalText.Caps(price, size, ink, 700);
        button.Arrange(label, icon, iconSize, amount, priceIcon, new Vector2(26, 26), gap: 10);
        button.Disabled = !enabled;
        button.TooltipText = hint ?? caption;
        Live.AddChild(button);
        return button;
    }

    /// <summary>The navy Back plaque that closes the board (or returns to the screen that opened it).</summary>
    protected RoyalButton Back(Rect2 rect) => Action(rect, "Back", "icon-back-chevron", Close, Kind.Navy, size: 17);

    /// <summary>A tappable card on a kit surface, lit while selected.</summary>
    protected RoyalButton Card(Rect2 rect, string name, Action press, bool selected = false, string slice = "ach-card", string selectedSlice = "ach-card-ready", bool enabled = true)
    {
        var card = RoyalButton.Over(rect, name, press, 8);
        card.SetStates(RoyalKit.Slice(selected ? selectedSlice : slice, 14), 8);
        card.Disabled = !enabled;
        card.MouseFilter = MouseFilterEnum.Pass;
        Live.AddChild(card);
        return card;
    }

    /// <summary>A page tab: navy at rest, lit gold when selected, with an optional emblem before its caption.</summary>
    protected RoyalButton Tab(Rect2 rect, string caption, bool selected, Action select, string glyph = null)
    {
        var tab = Card(rect, caption, select, selected, "tab", "ach-tab-selected");
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
    protected void Pager(Rect2 row, int page, int pages, Action<int> turn, string noun = "page", bool dots = true)
    {
        if (pages <= 1) return;
        for (var side = -1; side <= 1; side += 2)
        {
            var step = side;
            var rect = new Rect2(side < 0 ? row.Position.X : row.End.X - 30, row.GetCenter().Y - 20, 30, 40);
            var pager = RoyalButton.Over(rect, side < 0 ? $"Previous {noun}" : $"Next {noun}", () => turn((page + step + pages) % pages), 6);
            pager.SetStates(RoyalKit.Slice("chevron-button", 8), 6);
            pager.SetGlyph(RoyalKit.Texture(side < 0 ? "icon-chevron-left" : "icon-chevron-right"), new Rect2(8, 9, 14, 22));
            Live.AddChild(pager);
        }
        if (!dots) return;
        var left = row.GetCenter().X - pages * 9 + 1;
        for (var i = 0; i < pages; i++)
            Image(i == page ? "route-dot-on" : "pip-off", new Rect2(left + i * 18, row.GetCenter().Y - 8, 16, 16));
    }

    /// <summary>The painted picture for a reward type ("gold", "food", "essence", a relic id...).</summary>
    public static Texture2D RewardArt(string type, string itemId = "")
    {
        switch (type?.ToLowerInvariant())
        {
            case "gold": return RoyalKit.Texture("reward-gold");
            case "food": return RoyalKit.Texture("reward-rations");
            case "shards": return RoyalKit.Texture("reward-shards");
            case "season": return RoyalKit.Texture("reward-season");
            case "relic" when !string.IsNullOrEmpty(itemId): return UiArtLoader.TryLoadRelicIcon(GameData.GetEquipment(itemId)) ?? UiArtLoader.TryLoadRewardIcon(type, itemId);
            case "spell" when !string.IsNullOrEmpty(itemId): return UiArtLoader.TryLoadSpellIcon(GameData.GetSpell(itemId));
            case "unit" when !string.IsNullOrEmpty(itemId): return UnitFigure.For(GameData.GetUnit(itemId));
            default: return UiArtLoader.TryLoadRewardIcon(type, itemId);
        }
    }

    // ---- Notices --------------------------------------------------------------------------------------------

    /// <summary>Reserves the line where results ("Expedition dispatched.") appear; they survive a refresh.</summary>
    protected void NoticeAt(Rect2 rect) => _noticeRect = rect;

    protected void Notify(string text)
    {
        _notice = text ?? "";
        ShowNotice();
    }

    private void ShowNotice()
    {
        if (Live == null || _noticeRect.Size.X <= 0) return;
        foreach (var child in Live.GetChildren())
            if (child is Node node && node.Name == "Notice") { Live.RemoveChild(node); node.QueueFree(); }
        if (_notice.Length == 0) return;
        // Two lines at most, centred on the line it was given, so a long result never runs into the buttons beside it.
        var label = RoyalText.Paragraph(_notice, 18, new Color("f2e6cc"), 600, HorizontalAlignment.Center);
        label.Name = "Notice";
        label.VerticalAlignment = VerticalAlignment.Center;
        RoyalText.Place(Live, label, _noticeRect);
        RoyalText.FitLines(label, 2);
    }

    public static string Pretty(string id) => string.IsNullOrEmpty(id) ? "" : char.ToUpperInvariant(id[0]) + id[1..].Replace('_', ' ');
}
