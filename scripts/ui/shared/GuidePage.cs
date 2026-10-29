using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

/// <summary>
/// The reading page every guide and briefing opens: the inspector modal stacked over whatever is showing, its content
/// set as sections, picture cards and ledgers rather than paragraphs. Pages with several topics put them on tabs so
/// none scrolls far. Plain text (<see cref="ShowText"/>) is laid out the same way: "Name: value" lines become ledger
/// rows and a line ending in a colon becomes a section heading.
/// </summary>
public sealed class GuidePage
{
    public CanvasLayer Layer { get; private init; }
    public RealmModal Modal { get; private init; }
    /// <summary>The current page's scrolled column.</summary>
    public VBoxContainer Body { get; private set; }
    private VBoxContainer _root;
    private (string Label, Action<VBoxContainer> Build)[] _tabs = Array.Empty<(string, Action<VBoxContainer>)>();

    public const int Gap = 12;
    private static readonly Color RowRule = new(.86f, .71f, .45f, .2f);
    private static readonly Regex Pair = new(@"^(?<name>[A-Z][^:·#]{1,30}):\s+(?<value>\S.*)$");

    /// <summary>Opens an empty page on its own layer, named so callers and tests can find it.</summary>
    public static GuidePage Open(Node host, string name, string title, string identity = "codex", int layer = 66)
    {
        var canvas = new CanvasLayer { Layer = layer, Name = name };
        host.AddChild(canvas);
        var modal = RealmModal.OpenInspector(canvas, title, identity, 900, 600);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 10);
        modal.Content.AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var page = new GuidePage { Layer = canvas, Modal = modal, _root = root };
        page.Body = page.NewPage();
        return page;
    }

    /// <summary>A page of text from older screens, laid out as sections, ledgers and paragraphs.</summary>
    public static GuidePage ShowText(Node host, string title, string text)
    {
        var page = Open(host, "GuideText", title, title);
        var blocks = (text ?? "").Replace("\r", "").Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // A lone short line is an empty list ("No endless runs yet."), shown as one calm message.
        if (blocks.Length == 1 && !blocks[0].Contains('\n') && blocks[0].Length <= 60)
            page.Body.AddChild(RealmUi.EmptyState("book", blocks[0].TrimEnd('.')));
        else foreach (var block in blocks) page.AddTextBlock(block);
        page.Finish();
        return page;
    }

    private void AddTextBlock(string block)
    {
        var rows = new List<(string, string)>();
        void Flush() { if (rows.Count > 0) Body.AddChild(Ledger(rows.ToArray())); rows.Clear(); }
        foreach (var line in block.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            // "Threat: Moderate · Contacts: 3" holds two rows; a sentence that merely contains a colon stays text.
            var parts = line.Split(" · ").Select(part => Pair.Match(part)).ToArray();
            if (line.EndsWith(':') && line.Length <= 60) { Flush(); Body.AddChild(RealmUi.SectionTitle(line.TrimEnd(':'))); }
            else if (parts.All(m => m.Success)) rows.AddRange(parts.Select(m => (m.Groups["name"].Value, m.Groups["value"].Value)));
            else { Flush(); Body.AddChild(RealmUi.Label(line, 20)); }
        }
        Flush();
        Body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 2) });
    }

    /// <summary>
    /// Puts each topic on a tab above one scrolled page; selecting a tab rebuilds the page. The first tab shows
    /// unless <paramref name="first"/> names another.
    /// </summary>
    public void Tabs(int first, params (string Label, Action<VBoxContainer> Build)[] tabs)
    {
        _tabs = tabs;
        _root.RemoveChild(Body.GetParent()); Body.GetParent().QueueFree();
        var row = RealmUi.Tabs(_root, Show, tabs.Select(t => t.Label).ToArray());
        foreach (var button in row.GetChildren().OfType<Button>()) button.ButtonPressed = button.GetIndex() == first;
        Body = NewPage();
        Show(first);
    }

    private void Show(int index)
    {
        RealmUi.Clear(Body);
        ((ScrollContainer)Body.GetParent()).ScrollVertical = 0;
        _tabs[index].Build(Body);
        Finish();
    }

    private VBoxContainer NewPage()
    {
        var body = RealmUi.Scroll(_root);
        ((ScrollContainer)body.GetParent()).SetMeta("modal_min_height", 0);
        body.AddThemeConstantOverride("separation", Gap);
        return body;
    }

    /// <summary>Dresses what was added in the modal's materials; call after building a page.</summary>
    public void Finish() => RealmModal.Polish(_root);

    /// <summary>The page's lead: an icon and a line or two on a framed slab.</summary>
    public static PanelContainer Callout(string kitIcon, string text)
    {
        var slab = Framed("tile", 9, 14, 10);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 12); slab.AddChild(row);
        row.AddChild(Picture(RoyalKit.Texture(kitIcon), new Vector2(34, 34)));
        var label = RealmUi.Label(text, 20); label.VerticalAlignment = VerticalAlignment.Center;
        label.AddThemeColorOverride("font_color", ModalUi.Cream);
        row.AddChild(label);
        return slab;
    }

    /// <summary>A card on the modal's inset material holding a column of content.</summary>
    public static VBoxContainer Card(Control parent)
    {
        var card = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Pass };
        parent.AddChild(card);
        var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 8); card.AddChild(stack);
        return stack;
    }

    /// <summary>Cards side by side, as many columns as the page asks.</summary>
    public static GridContainer Grid(int columns)
    {
        var grid = new GridContainer { Columns = columns, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", Gap); grid.AddThemeConstantOverride("v_separation", Gap);
        return grid;
    }

    /// <summary>A painting in the kit's relic-card frame, or a unit standing in it as its battle figure.</summary>
    public static PanelContainer Portrait(Texture2D texture, float size, UnitDefinition unit = null)
    {
        var frame = Framed("relic-card", 12, 6, 6);
        frame.CustomMinimumSize = new Vector2(size, size);
        frame.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin; frame.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        if (unit != null) { var figure = new UnitFigure { Fill = .92f }; figure.SetUnit(unit); frame.AddChild(figure); }
        else frame.AddChild(Picture(texture, Vector2.Zero).With(picture => picture.SizeFlagsVertical = Control.SizeFlags.Fill));
        return frame;
    }

    /// <summary>Rows of a muted name and its value, ruled like a <see cref="StatSheet"/> but with room to wrap.</summary>
    public static PanelContainer Ledger(params (string Name, string Value)[] rows)
    {
        var frame = Framed("tile", 9, 14, 4);
        var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 0); frame.AddChild(stack);
        for (var i = 0; i < rows.Length; i++)
        {
            var line = new HBoxContainer { CustomMinimumSize = new Vector2(0, StatSheet.RowHeight), Name = rows[i].Name };
            line.AddThemeConstantOverride("separation", 16);
            var name = RealmUi.Label(rows[i].Name, 18, true); name.SizeFlagsStretchRatio = 1; name.VerticalAlignment = VerticalAlignment.Center;
            var value = RealmUi.Label(rows[i].Value, 18); value.SizeFlagsStretchRatio = 1.6f; value.VerticalAlignment = VerticalAlignment.Center;
            value.HorizontalAlignment = HorizontalAlignment.Right; value.AddThemeColorOverride("font_color", ModalUi.Cream);
            line.AddChild(name); line.AddChild(value);
            if (i > 0) stack.AddChild(new ColorRect { Color = RowRule, CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore });
            stack.AddChild(line);
        }
        return frame;
    }

    /// <summary>A small label slab, as drawn over units in battle.</summary>
    public static PanelContainer Chip(string text, Color ink, int minimumWidth = 0)
    {
        var slab = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(minimumWidth, 0),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        slab.SetMeta("modal_unframed", true);
        var fill = new StyleBoxFlat { BgColor = new Color("17222aed"), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 1, ContentMarginBottom = 2 };
        fill.SetCornerRadiusAll(2);
        slab.AddThemeStyleboxOverride("panel", fill);
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontOverride("font", RoyalFonts.Body(600));
        label.AddThemeFontSizeOverride("font_size", ReadingText.PhoneBody);
        label.AddThemeColorOverride("font_color", ink);
        slab.AddChild(label);
        return slab;
    }

    /// <summary>A row of an icon or chip and the line that explains it.</summary>
    public static HBoxContainer Explained(Control mark, string text, float markWidth)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 12);
        var holder = new CenterContainer { CustomMinimumSize = new Vector2(markWidth, 0) };
        holder.AddChild(mark); row.AddChild(holder);
        var label = RealmUi.Label(text, 18); label.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(label);
        return row;
    }

    public static TextureRect Picture(Texture2D texture, Vector2 size, Color? tint = null) => new()
    {
        Texture = texture, CustomMinimumSize = size, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore,
        TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps, Modulate = tint ?? Colors.White,
        SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
    };

    private static PanelContainer Framed(string kit, int slice, int side, int end)
    {
        var frame = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        frame.SetMeta("modal_unframed", true);
        var box = RoyalKit.Slice(kit, slice);
        box.ContentMarginLeft = box.ContentMarginRight = side; box.ContentMarginTop = box.ContentMarginBottom = end;
        frame.AddThemeStyleboxOverride("panel", box);
        return frame;
    }
}
