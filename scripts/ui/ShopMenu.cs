using System;
using System.Linq;
using Godot;

/// <summary>
/// The armory: warband, spells, war wagon and relics pages, drawn on the approved concept
/// plates. Game rules stay in GameState; this screen only presents them and forwards actions.
/// </summary>
public partial class ShopMenu : RoyalScreen
{
    private static readonly string[] TabNames = { "Warband", "Spells", "War wagon", "Relics" };
    private static readonly string[] TabIcons = { "tab-warband", "tab-spells", "tab-wagon", "tab-relics" };
    private int _tab;
    public int Tab => _tab;
    private Control _header, _body;
    private GameState _state;

    public ShopMenu() { PlateName = "warband"; }

    protected override void Build()
    {
        _header = Layer("Header");
        _body = Layer("Body");
        _state = GameState.Instance;
        _state.FoodChanged += Refresh;
        SelectTab(SceneRouter.Instance.ConsumeInitialShopTab());
    }

    public override void _ExitTree()
    {
        if (_state != null) _state.FoodChanged -= Refresh;
    }

    /// <summary>The armory chrome as measured on the current page's concept (the wagon shares the relics frame,
    /// whose right edge is plain).</summary>
    private RoyalSpec Chrome => RoyalSpec.For(_tab switch { 1 => "spells", 2 or 3 => "relics", _ => "warband" });

    private void SelectTab(int index)
    {
        _tab = Mathf.Clamp(index, 0, TabNames.Length - 1);
        _selectedId = "";
        SetPlate(_tab switch { 1 => "spells", 2 or 3 => "relics", _ => "warband" });
        Refresh();
    }

    private void Refresh()
    {
        if (!IsInsideTree()) return;
        RoyalUiTools.Clear(_header);
        RoyalUiTools.Clear(_body);
        BuildHeader();
        switch (_tab)
        {
            case 0: case 1: BuildRoster(_tab == 1); break;
            case 2: BuildWagon(); break;
            default: BuildRelics(); break;
        }
    }

    private void BuildHeader()
    {
        var spec = Chrome;
        var title = spec.Label("title", TabNames[_tab], 420);
        _header.AddChild(title);
        _header.AddChild(RoyalKit.Image("icon-squad", spec.Rect("subtitle.icon")));
        // Units and spells share one loadout, so both pages count it whole.
        var (caption, count) = _tab switch
        {
            2 => ("UPGRADES", $"{BaseUpgradeCatalog.GetAll().Sum(u => _state.GetBaseUpgradeLevel(u.Id))} / {BaseUpgradeCatalog.GetAll().Sum(u => u.MaxLevel)}"),
            3 => ("OWNED", $"{_state.GetOwnedEquipment().Count} / {GameData.GetAllEquipment().Count()}"),
            _ => ("LOADOUT", $"{_state.LoadoutCount} / {_state.LoadoutSizeLimit}")
        };
        if (spec.Has("subtitle.count"))
        {
            var word = spec.Label("subtitle", caption, 200);
            _header.AddChild(word);
            var number = spec.Label("subtitle.count", count, 120);
            number.Position = new Vector2(word.Position.X + word.TextWidth(word.FontSize) + 10, number.Position.Y);
            _header.AddChild(number);
        }
        else _header.AddChild(spec.Label("subtitle", $"{caption} {count}", 260));
        // Four tabs share the concept's tab strip (measured from its first tab to its last).
        var first = spec.Rect("tab.warband");
        var strip = new Rect2(first.Position, new Vector2(spec.Rect("tab.adviser").End.X - first.Position.X, first.Size.Y));
        var width = strip.Size.X / TabNames.Length;
        var iconOffset = spec.Rect("tab.warband.icon").Position.Y - first.Position.Y;
        for (var i = 0; i < TabNames.Length; i++)
        {
            var index = i;
            // A resting tab is the plate's own tab with its whole rim (art/royal/chrome.py cuts it from y 56, 72.6 tall),
            // so drawn over that footprint its rails lie on the plate's; the lit tab keeps the concept's rect.
            var rect = i == _tab ? new Rect2(strip.Position.X + i * width, strip.Position.Y, width, strip.Size.Y)
                : new Rect2(strip.Position.X + i * width, 56, width, 72.6f);
            var tab = RoyalButton.Over(rect, TabNames[i], () => SelectTab(index), 4);
            tab.SetStates(RoyalKit.Slice(i == _tab ? "tab-selected" : "tab", 10), 4);
            tab.MarkTab(i == _tab);
            _header.AddChild(tab);
            tab.SetGlyph(RoyalKit.Texture(TabIcons[i]), new Rect2(width / 2 - 16, iconOffset - 2 + strip.Position.Y - rect.Position.Y, 32, 27), i == _tab ? new Color("ffe7b0") : Colors.White);
            var label = spec.Label("tab.warband.label", TabNames[i], width - 10, i == _tab ? new Color("fff1d2") : new Color("e2ddd6"));
            label.Align = HorizontalAlignment.Center;
            label.Position = new Vector2(5, label.Position.Y - rect.Position.Y);
            label.Size = new Vector2(width - 10, label.Size.Y);
            tab.SetCaption(label, new Rect2(label.Position, label.Size));
        }
        _header.AddChild(RoyalButton.Over(spec.Rect("close"), "Close panel", Close, 6));
    }
}

/// <summary>Small helpers shared by the concept screens.</summary>
public static class RoyalUiTools
{
    public static void Clear(Node node)
    {
        foreach (var child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); }
    }

    /// <summary>Replace the concept's fixed track and thumb before drawing a live scrollbar.</summary>
    public static void ClearPaintedScrollTrack(Control layer, string plate, RoyalSpec spec)
    {
        var track = spec.Rect("grid.scroll");
        var thumb = spec.Rect("grid.scroll.thumb");
        var area = track.Merge(thumb).Grow(3);
        // Sample the uninterrupted collection background immediately to the left.
        var background = new Rect2(area.Position - new Vector2(area.Size.X + 2, 0), area.Size);
        layer.AddChild(new TextureRect { Texture = RoyalArt.Cut(plate, background),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            Position = area.Position, Size = area.Size, MouseFilter = Control.MouseFilterEnum.Ignore });
    }

    public static Control Box(Rect2 rect, bool clip = false) => new() { Position = rect.Position, Size = rect.Size, ClipContents = clip, MouseFilter = Control.MouseFilterEnum.Ignore };

}
