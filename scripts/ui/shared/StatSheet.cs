using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// A compact stat ledger: one framed panel of rows, each cell an icon, the stat's name and its value on the right.
/// Cells sit in up to the asked number of columns; columns drop until every name and value fits its cell, so text
/// never shrinks below the phone floor or runs past the frame. Each cell is named "Health: 53" for hints and tests.
/// Without names a sheet is a strip of icons and values, each pair centred in its cell, for cards with little room.
/// </summary>
public partial class StatSheet : Container
{
    public const float RowHeight = 34, Inset = 5;
    private const float Pad = 9, IconSize = 20, IconGap = 7, ValueGap = 10;
    // A squeezed single column: tighter spacing and the most a name's letter spacing closes up.
    private const float TightPad = 7, TightIconGap = 5, TightValueGap = 8, MostTracking = .7f;
    private const int NameSize = 18, ValueSize = 20;
    private static readonly Color NameInk = new("c2b59d"), ValueInk = new("fbf0cf"), Rule = new(.86f, .71f, .45f, .2f);

    private readonly List<(Control Cell, TextureRect Icon, RoyalLabel Name, RoyalLabel Value)> _cells = new();
    private readonly int _columns;
    private readonly bool _names;
    private readonly StyleBox _frame = RoyalKit.Slice("tile", 9);

    /// <summary>Columns in use at the current width.</summary>
    public int Columns { get; private set; }

    public StatSheet(IEnumerable<ArmoryDetailUi.Stat> stats, int columns = 2, bool names = true)
    {
        Name = "StatSheet";
        _columns = Columns = Mathf.Max(1, columns);
        _names = names;
        MouseFilter = MouseFilterEnum.Pass;
        foreach (var stat in stats)
        {
            var text = $"{stat.Label}: {stat.Value}";
            var cell = new Control { Name = stat.Label, TooltipText = text, AccessibilityName = text, MouseFilter = MouseFilterEnum.Pass };
            var icon = new TextureRect { Texture = Glyph(stat.Icon), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore,
                TextureFilter = TextureFilterEnum.LinearWithMipmaps };
            var name = RoyalText.Serif(stat.Label, NameSize, NameInk, 600);
            var value = RoyalText.Serif(stat.Value, ValueSize, ValueInk, 700);
            value.Align = HorizontalAlignment.Right;
            foreach (var label in new[] { name, value }) { label.ShrinkToFit = false; label.MinimumFontSize = ReadingText.PhoneBody; }
            name.Visible = names;
            cell.AddChild(icon); cell.AddChild(name); cell.AddChild(value);
            AddChild(cell);
            _cells.Add((cell, icon, name, value));
        }
    }

    /// <summary>Stat icons: the concept's painted glyphs, and drawn ones in the same gold for the rest.</summary>
    public static Texture2D Glyph(string icon) => icon switch
    {
        "heart" => RoyalKit.Texture("stat-health"), "sword" => RoyalKit.Texture("stat-damage"), "bolt" => RoyalKit.Texture("stat-courage"),
        "eye" => RoyalKit.Texture("stat-range"), "shield" => RoyalKit.Texture("stat-recovery"), "clock" => RoyalKit.Texture("spellstat-cooldown"),
        "target" => RoyalKit.Texture("spellstat-radius"), "people" => RoyalKit.Texture("icon-people-gold"),
        "crystal" or "mana" => RoyalArt.Load("res://assets/ui/icons/stats/mana.svg"), "duration" => RoyalArt.Load("res://assets/ui/icons/stats/duration.svg"),
        "arrow" or "speed" => RoyalArt.Load("res://assets/ui/icons/stats/speed.svg"), "hammer" or "siege" => RoyalArt.Load("res://assets/ui/icons/stats/siege.svg"),
        _ => RoyalKit.Texture(icon)
    };

    /// <summary>The panel height for a number of stats at a width.</summary>
    public float HeightFor(float width) => Mathf.Ceil(_cells.Count / (float)FitColumns(width)) * RowHeight + Inset * 2;

    /// <summary>Sizes the sheet to its rows at a canvas rect's position and width (screens placed by spec).</summary>
    public StatSheet Place(Vector2 at, float width)
    {
        Position = at;
        Size = new Vector2(width, HeightFor(width));
        return this;
    }

    // At the sheet's own sizes and spacing, whatever a squeeze last set.
    private static float Width(RoyalLabel label, int size) => label.Font.GetStringSize(label.Text, HorizontalAlignment.Left, -1, size).X;

    // A strip's pair is centred, so its cells only need the tight insets.
    private float Needed((Control Cell, TextureRect Icon, RoyalLabel Name, RoyalLabel Value) cell) => _names
        ? Pad + IconSize + IconGap + Width(cell.Name, NameSize) + ValueGap + Width(cell.Value, ValueSize) + Pad
        : TightPad + IconSize + TightIconGap + Width(cell.Value, ValueSize) + TightPad;

    private int FitColumns(float width)
    {
        var columns = Mathf.Min(_columns, Mathf.Max(1, _cells.Count));
        var widest = _cells.Count == 0 ? 0 : _cells.Max(Needed);
        while (columns > 1 && (width - Inset * 2) / columns < widest) columns--;
        return columns;
    }

    // Height follows width (more columns, fewer rows), so a new width asks the parent for a new height.
    public override Vector2 _GetMinimumSize()
    {
        // As narrow as one column closed up as far as it goes (see the squeeze below).
        var widest = _cells.Count == 0 ? 0 : _cells.Max(cell => 2 * TightPad + IconSize + TightIconGap + Width(cell.Value, ReadingText.PhoneBody)
            + (_names ? TightValueGap + Width(cell.Name, NameSize) - MostTracking * Mathf.Max(0, cell.Name.Text.Length - 1) : 0));
        return new Vector2(Mathf.Ceil(widest) + Inset * 2, Size.X > 0 ? HeightFor(Size.X) : 0);
    }

    private float _measuredWidth = -1;

    public override void _Notification(int what)
    {
        if (what == NotificationResized && !Mathf.IsEqualApprox(Size.X, _measuredWidth)) { _measuredWidth = Size.X; UpdateMinimumSize(); }
        if (what != NotificationSortChildren) return;
        Columns = FitColumns(Size.X);
        var width = (Size.X - Inset * 2) / Columns;
        // A single column too narrow for a long pair ("Faster attacks +0.88%") closes up instead of overflowing:
        // tighter gaps and values at the reading floor for every row (icons and names stay in line), then a little
        // less letter spacing in just the names still too wide.
        var squeeze = _cells.Any(cell => Needed(cell) > width);
        var (pad, gap, valueGap) = squeeze ? (TightPad, TightIconGap, TightValueGap) : (Pad, IconGap, ValueGap);
        for (var i = 0; i < _cells.Count; i++)
        {
            var (cell, icon, name, value) = _cells[i];
            FitChildInRect(cell, new Rect2(Inset + i % Columns * width, Inset + i / Columns * RowHeight, width, RowHeight));
            value.FontSize = squeeze ? ReadingText.PhoneBody : ValueSize; name.Tracking = 0;
            var over = 2 * pad + IconSize + gap + valueGap + Width(name, NameSize) + Width(value, value.FontSize) - width;
            if (_names && over > 0) name.Tracking = -Mathf.Min(MostTracking, over / Mathf.Max(1, name.Text.Length - 1));
            // A strip's icon and value sit together in the middle of the cell; a ledger's icon starts the row.
            var start = _names ? pad : Mathf.Round((width - IconSize - gap - Width(value, value.FontSize)) / 2);
            icon.Position = new Vector2(start, (RowHeight - IconSize) / 2); icon.Size = new Vector2(IconSize, IconSize);
            // Name and value share one baseline, the value's cap band centred in the row.
            var baseline = Mathf.Round(RowHeight / 2 + ValueSize * value.CapRatio / 2);
            var left = start + IconSize + gap;
            value.Align = _names ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            name.Position = new Vector2(left, 0); name.Size = new Vector2(width - left - pad, RowHeight); name.Baseline = baseline;
            value.Position = new Vector2(left, 0); value.Size = new Vector2(width - left - pad, RowHeight); value.Baseline = baseline;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawStyleBox(_frame, new Rect2(Vector2.Zero, Size));
        if (_cells.Count == 0) return;
        var rows = Mathf.CeilToInt(_cells.Count / (float)Columns);
        var width = (Size.X - Inset * 2) / Columns;
        // Hairlines between rows, and between columns where both cells are filled.
        for (var row = 1; row < rows; row++)
        {
            var y = Mathf.Round(Inset + row * RowHeight) + .5f;
            DrawLine(new Vector2(Inset + Pad, y), new Vector2(Size.X - Inset - Pad, y), Rule, 1);
        }
        for (var column = 1; column < Columns; column++)
        {
            var x = Mathf.Round(Inset + column * width) + .5f;
            var filled = Mathf.CeilToInt((_cells.Count - column) / (float)Columns);
            DrawLine(new Vector2(x, Inset + 7), new Vector2(x, Inset + filled * RowHeight - 7), Rule, 1);
        }
    }
}
