using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>Relics: the collection beside the parchment detail card.</summary>
public partial class ShopMenu
{
    private string _selectedRelic = "";
    private float _relicScroll;
    private const float NameLeading = 18;

    public static string RelicIcon(EquipmentDefinition relic)
    {
        // Relics with a glyph of their own (art/royal/glyphs.py); the rest share one by the kind of object.
        var own = "relicicon-" + relic.Id.Replace("relic_", "");
        if (ResourceLoader.Exists($"res://assets/ui/royal/kit/{own}.png")) return own;
        var name = relic.DisplayName.ToLowerInvariant();
        if (name.Contains("lantern") || name.Contains("censer")) return "relicicon-lantern";
        if (name.Contains("boot")) return "relicicon-boots";
        if (name.Contains("fang") || name.Contains("tooth") || name.Contains("charm")) return "relicicon-fang";
        if (name.Contains("cloak") || name.Contains("mantle")) return "relicicon-cloak";
        if (name.Contains("crown") || name.Contains("circlet") || name.Contains("wreath")) return "relicicon-crown";
        if (name.Contains("signet")) return "relicicon-signet";
        if (name.Contains("ring")) return "relicicon-ring";
        if (name.Contains("talisman") || name.Contains("sigil") || name.Contains("heart") || name.Contains("soul")) return "relicicon-talisman";
        if (name.Contains("shield") || name.Contains("bulwark") || name.Contains("ward")) return "class-shield";
        if (name.Contains("blade") || name.Contains("edge") || name.Contains("brand") || name.Contains("hammer")) return "class-melee";
        return "relicicon-pendant";
    }

    public static Texture2D RelicArt(EquipmentDefinition relic) => UiArtLoader.TryLoadRelicIcon(relic);

    /// <summary>The relic's modifiers as stat rows (value, caption, stat-sheet icon id), most important first.</summary>
    private static List<(string Value, string Caption, string Icon)> RelicRows(EquipmentDefinition relic)
    {
        return RelicStatText.Stats(relic).Select(row => (row.Value, row.Label, row.Icon)).ToList();
    }

    private void BuildRelics()
    {
        var spec = RoyalSpec.For("relics");
        var state = _state;
        var owned = state.GetOwnedEquipment();
        // Owned relics lead the collection, as in the concept; the rest follow in catalogue order.
        var all = GameData.GetAllEquipment().OrderBy(r => owned.Contains(r.Id) ? 0 : 1).ToList();
        var visible = _filter switch { 1 => all.Where(r => owned.Contains(r.Id)).ToList(), 2 => all.Where(r => EquippedBy(r.Id) != null).ToList(), _ => all };
        if (visible.Count == 0) visible = all;
        if (!_equipping && visible.All(r => r.Id != _selectedRelic)) _selectedRelic = (visible.FirstOrDefault(r => owned.Contains(r.Id)) ?? visible[0]).Id;
        var selected = all.First(r => r.Id == _selectedRelic);

        if (_equipping) BuildRelicEquip(spec, selected);
        else BuildRelicCollection(spec, visible, owned);

        var back = RoyalButton.Over(spec.Rect("button.back"), "Back", _equipping ? EndRelicEquip : Close, 6);
        back.Arrange(spec.Label("button.back.label", "Back", 90), RoyalKit.Texture("icon-back-chevron"), spec.Rect("button.back.icon").Size, gap: 10);
        _body.AddChild(back);

        // The detail card lists every bonus, so draw uninterrupted wood over the plate's obsolete details button
        // (the wood beside it, mirrored so the grain runs on across the join).
        _body.AddChild(new TextureRect { Texture = RoyalArt.Cut("relics", new Rect2(352, 634, 242, 51)), FlipH = true,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            Position = new Vector2(594, 634), Size = new Vector2(242, 51), MouseFilter = MouseFilterEnum.Ignore });

        // Detail card.
        _body.AddChild(RoyalKit.Image(RelicIcon(selected) == "relicicon-lantern" ? "relic-title-icon" : RelicIcon(selected), spec.Rect("detail.icon")));
        _body.AddChild(spec.Label("detail.name", selected.DisplayName, spec.Rect("detail.titlebar").End.X - spec.Number("detail.name", "pen_x", 955) - 10));
        var description = RoyalText.Paragraph(selected.Description, (int)spec.Number("detail.desc.1", "size", 17), new Color("1c120a"), 700);
        description.RemoveThemeColorOverride("font_shadow_color");
        description.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        description.Position = new Vector2(spec.Number("detail.desc.1", "pen_x", 897), spec.Number("detail.desc.1", "baseline", 256) - 18);
        description.Size = new Vector2(spec.Rect("detail.stats").End.X - description.Position.X, 46);
        description.AddThemeConstantOverride("line_spacing", -1);
        // Most descriptions take two lines; a longer one takes a third and the bonus panel's top edge drops for it.
        var font = description.GetThemeFont("font");
        var size = description.GetThemeFontSize("font_size");
        var descriptionLines = font.GetMultilineStringSize(selected.Description, HorizontalAlignment.Left, description.Size.X, size).Y > font.GetHeight(size) * 2 + 1 ? 3 : 2;
        RoyalText.FitLines(description, descriptionLines);
        BuildRelicBonuses(spec, selected, (descriptionLines - 2) * Mathf.Ceil(font.GetHeight(size) - 1));
        _body.AddChild(description);

        // The plaque starts equipping (or removing a worn relic); while equipping it confirms the previewed change.
        var holder = EquippedBy(selected.Id);
        var have2 = owned.Contains(selected.Id);
        var verb = _equipping ? _removing ? "Remove" : "Equip" : holder != null ? "Unequip" : "Equip";
        var action = _equipping && RelicPreview != null ? $"{verb} {(_removing ? "from" : "to")} slot {_equipSlot}" : verb;
        var equip = RoyalButton.Over(spec.Rect("button.equip"), action, () =>
        {
            if (_equipping) ApplyRelicEquip(); else BeginRelicEquip(selected.Id);
        }, 6);
        equip.Disabled = _equipping ? RelicPreview?.CanApply != true : !have2;
        equip.TooltipText = _equipping ? RelicPreview == null ? "Choose an ally" : RelicPreview.CanApply ? action : RelicPreview.Reason
            : !have2 ? "Not owned yet · craft it in the forge" : holder != null ? $"Equipped on {holder.Unit.DisplayName} · slot {holder.Slot}" : "Choose an ally to compare";
        // The two big plaques share one capitals size with the other primary actions (result board, forge).
        var equipLabel = spec.Label("button.equip.label", verb, 200);
        equipLabel.FontSize = 22;
        equip.Arrange(equipLabel, RoyalKit.Texture("icon-helm"), spec.Rect("button.equip.icon").Size * .85f, gap: 14);
        _body.AddChild(equip);
        var forgeRect = spec.Rect("button.forge");
        var forge = RoyalButton.Over(forgeRect, "Forge", () => SceneRouter.Instance.GoToForge(), 6);
        var forgeLabel = spec.Label("button.forge.label", "Forge", 200);
        forgeLabel.FontSize = 22; forgeLabel.Ink = new Color("1a0a04");
        forge.Arrange(forgeLabel, RoyalKit.Texture("icon-hammers"), spec.Rect("button.forge.icon").Size * .85f, gap: 14);
        _body.AddChild(forge);
    }

    /// <summary>The collection panel: owned relics first, scrolling under the painted track.</summary>
    private void BuildRelicCollection(RoyalSpec spec, List<EquipmentDefinition> visible, HashSet<string> owned)
    {
        _body.AddChild(spec.Label("collection.label", "Collection", 200));
        var filterRect = spec.Rect("collection.filter");
        var filter = RoyalButton.Over(filterRect, "Filter collection", () => { _filter = (_filter + 1) % Filters.Length; _relicScroll = 0; Refresh(); }, 4);
        var filterLabel = spec.Label("collection.filter.label", Filters[_filter], 70);
        filterLabel.Position -= filterRect.Position;
        filter.SetCaption(filterLabel, new Rect2(filterLabel.Position, filterLabel.Size));
        _body.AddChild(filter);

        // Collection: cards on the concept's pitch, scrolling under the painted track.
        var first = spec.Rect("card.0.0"); var right = spec.Rect("card.0.1"); var below = spec.Rect("card.1.0");
        var track = spec.Rect("grid.scroll");
        var pitch = new Vector2(right.Position.X - first.Position.X, below.Position.Y - first.Position.Y + 2);
        var cell = new Vector2(119, 132);
        var (columns, pitchX) = GridColumns(first.Position.X, track.Position.X, cell.X, pitch.X);
        pitch.X = pitchX;
        var view = new Rect2(first.Position - new Vector2(4, 4), new Vector2(track.Position.X - first.Position.X + 2, spec.Rect("button.back").Position.Y - first.Position.Y - 4));
        var scroll = new ScrollContainer { Position = view.Position, Size = view.Size, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever };
        _body.AddChild(scroll);
        var grid = new Control { CustomMinimumSize = new Vector2(view.Size.X - 2, Mathf.Max(view.Size.Y, Mathf.Ceil(visible.Count / (float)columns) * pitch.Y + 6)), MouseFilter = MouseFilterEnum.Pass };
        scroll.AddChild(grid);
        var labelSpec = spec.Rect("card.0.0");
        for (var i = 0; i < visible.Count; i++)
        {
            var relic = visible[i];
            var rect = new Rect2(new Vector2(4 + i % columns * pitch.X, 4 + i / columns * pitch.Y), cell);
            var isSelected = relic.Id == _selectedRelic;
            var have = owned.Contains(relic.Id);
            var card = new RoyalButton { Position = rect.Position, Size = rect.Size, AccessibilityName = relic.DisplayName, MouseFilter = MouseFilterEnum.Pass,
                TooltipText = relic.DisplayName + (have ? "" : " · Not owned"), MouseDefaultCursorShape = CursorShape.PointingHand };
            card.SetStates(RoyalKit.Slice(isSelected ? "relic-card-selected-empty" : "relic-card", 12), 6);
            var id = relic.Id;
            card.Pressed += () => { _selectedRelic = id; Refresh(); };
            card.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Texture = RelicArt(relic), Position = new Vector2(10, 6), Size = new Vector2(cell.X - 20, 81), MouseFilter = MouseFilterEnum.Ignore,
                TextureFilter = TextureFilterEnum.LinearWithMipmaps, SelfModulate = have ? Colors.White : new Color(.42f, .44f, .48f) });
            var plate = new Panel { Position = new Vector2(1, 1), Size = new Vector2(32, 35), MouseFilter = MouseFilterEnum.Ignore };
            var plateStyle = new StyleBoxFlat { BgColor = new Color(isSelected ? "2b2016" : "1b1a19"), BorderColor = new Color(isSelected ? "e9b45c" : "8e7550"), AntiAliasing = true };
            plateStyle.BorderWidthRight = plateStyle.BorderWidthBottom = 1; plateStyle.CornerRadiusBottomRight = 8; plateStyle.CornerRadiusTopLeft = 3;
            plate.AddThemeStyleboxOverride("panel", plateStyle);
            card.AddChild(plate);
            card.AddChild(RoyalKit.Image(RelicIcon(relic), new Rect2(7, 6, 19, 24)));
            // Names sit under the art on the concept's baseline, two lines when long, one line centred between them.
            var lines = NameLines(spec, relic.DisplayName, cell.X - 10, have ? null : new Color("9c968c"));
            for (var line = 0; line < lines.Count; line++)
            {
                lines[line].Position -= labelSpec.Position + new Vector2(0, lines.Count == 1 ? NameLeading / 2 : (1 - line) * NameLeading);
                card.AddChild(lines[line]);
            }
            grid.AddChild(card);
        }
        RoyalUiTools.ClearPaintedScrollTrack(_body, "relics", spec);
        _body.AddChild(new RoyalScrollHint { Name = "CollectionScrollbar", Position = track.Position, Size = track.Size, Scroll = scroll });
        scroll.GetVScrollBar().ValueChanged += value => _relicScroll = (float)value;
        // Keep the selected relic in view when the page opens or the selection moves.
        var selectedRow = visible.FindIndex(r => r.Id == _selectedRelic) / columns;
        var rowTop = 4 + selectedRow * pitch.Y;
        if (rowTop < _relicScroll || rowTop + cell.Y > _relicScroll + view.Size.Y) _relicScroll = Mathf.Max(0, rowTop - 4);
        Callable.From(() => { if (GodotObject.IsInstanceValid(scroll)) { scroll.ScrollVertical = (int)_relicScroll; } }).CallDeferred();
    }

    /// <summary>
    /// Every bonus the relic gives, one compact line each, sharing the painted stats panel evenly (three lines at
    /// the concept's pitch); a relic with more than fit scrolls them. <paramref name="drop"/> lowers the panel's top
    /// edge to make room for a longer description.
    /// </summary>
    private void BuildRelicBonuses(RoyalSpec spec, EquipmentDefinition relic, float drop)
    {
        var rows = RelicRows(relic);
        var enchantment = _state.GetRelicEnchantment(relic.Id);
        if (enchantment != null) rows.Add((enchantment.Title, "Enchantment", "crystal"));
        rows.Add((relic.Rarity.Length > 0 ? char.ToUpperInvariant(relic.Rarity[0]) + relic.Rarity[1..] : "", "Rarity", "relicstat-rarity"));

        // The plate paints dividers for exactly three rows: cover each with the panel just above it, then reuse it.
        var panel = spec.Rect("detail.stats");
        TextureRect Cut(Rect2 source, Vector2 at) => new() { Texture = RoyalArt.Cut("relics", source), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, Position = at, Size = source.Size, MouseFilter = MouseFilterEnum.Ignore };
        var painted = new[] { spec.Rect("stat.1").Position.Y + 1, spec.Rect("stat.2").Position.Y + 1.5f };
        foreach (var y in painted)
            _body.AddChild(Cut(new Rect2(panel.Position.X - 1, y - 17, panel.Size.X + 2, 11), new Vector2(panel.Position.X - 1, y - 5)));
        var divider = new Rect2(panel.Position.X + 2, painted[0] - 4, panel.Size.X - 4, 8);
        if (drop > 0)
        {
            // Parchment from just above covers the old edge; the edge itself is drawn again lower down.
            var edge = new Rect2(panel.Position.X - 4, panel.Position.Y - 3, panel.Size.X + 8, 15);
            _body.AddChild(Cut(new Rect2(edge.Position - new Vector2(0, drop), new Vector2(edge.Size.X, drop)), edge.Position));
            _body.AddChild(Cut(edge, edge.Position + new Vector2(0, drop)));
            panel = new Rect2(panel.Position + new Vector2(0, drop), panel.Size - new Vector2(0, drop));
        }

        const float LeastPitch = 28;
        var inner = new Rect2(panel.Position + new Vector2(0, 4), panel.Size - new Vector2(0, 8));
        var pitch = inner.Size.Y / Mathf.Clamp(rows.Count, 3, Mathf.FloorToInt(inner.Size.Y / LeastPitch));
        var scroll = new ScrollContainer { Name = "RelicBonuses", Position = inner.Position, Size = inner.Size,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever };
        _body.AddChild(scroll);
        var list = new Control { CustomMinimumSize = new Vector2(inner.Size.X, rows.Count * pitch), MouseFilter = MouseFilterEnum.Pass };
        scroll.AddChild(list);
        var icon = Mathf.Min(28, pitch - 6);
        for (var i = 0; i < rows.Count; i++)
        {
            var top = i * pitch;
            if (i > 0) list.AddChild(Cut(divider, new Vector2(divider.Position.X - inner.Position.X, top - divider.Size.Y / 2)));
            // The same glyphs as every other stat sheet; ids it doesn't know (rarity) are kit pieces.
            list.AddChild(new TextureRect { Texture = StatSheet.Glyph(rows[i].Icon), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.LinearWithMipmaps,
                Position = new Vector2(30 - icon / 2, top + (pitch - icon) / 2), Size = new Vector2(icon, icon), MouseFilter = MouseFilterEnum.Ignore });
            var value = RoyalText.Serif(rows[i].Value, 18, new Color("f6ebbd"), 700);
            value.Align = HorizontalAlignment.Right;
            var valueWidth = value.TextWidth(value.FontSize) + 4;
            value.Position = new Vector2(inner.Size.X - 16 - valueWidth, top); value.Size = new Vector2(valueWidth, pitch);
            var caption = RoyalText.Serif(rows[i].Caption, 18, new Color("c7bcaf"), 600);
            caption.Position = new Vector2(54, top); caption.Size = new Vector2(value.Position.X - 8 - 54, pitch);
            list.AddChild(caption);
            list.AddChild(value);
        }
        _body.AddChild(new RoyalScrollHint { Position = new Vector2(inner.End.X - 7, inner.Position.Y + 6), Size = new Vector2(3, inner.Size.Y - 12), Scroll = scroll });
    }

    /// <summary>
    /// A collection name at the reading size in one or two centred lines, broken where the longer line is shortest.
    /// A line still too wide closes its letter spacing a little rather than shrinking below the phone floor.
    /// </summary>
    private static List<RoyalLabel> NameLines(RoyalSpec spec, string name, float room, Color? ink)
    {
        RoyalLabel Line(string text)
        {
            var label = spec.Label("card.0.0.label", text, room, ink);
            label.MinimumFontSize = label.FontSize;
            var width = label.TextWidth(label.FontSize);
            if (width > room && text.Length > 1) label.Tracking = Mathf.Max(-1, (room - width) / (text.Length - 1));
            return label;
        }
        var whole = Line(name);
        var words = name.Split(' ');
        if (whole.Tracking == 0 || words.Length < 2) return new List<RoyalLabel> { whole };
        float Width(string text) => whole.Font.GetStringSize(text, HorizontalAlignment.Left, -1, whole.FontSize).X;
        var split = Enumerable.Range(1, words.Length - 1)
            .MinBy(i => Mathf.Max(Width(string.Join(' ', words[..i])), Width(string.Join(' ', words[i..]))));
        return new List<RoyalLabel> { Line(string.Join(' ', words[..split])), Line(string.Join(' ', words[split..])) };
    }

    private sealed record RelicBearer(UnitDefinition Unit,int Slot);
    private RelicBearer EquippedBy(string relicId)
    {
        foreach(var unit in _state.GetOwnedPlayerUnits())
        {
            if(_state.GetUnitEquipment(unit.Id)?.Id==relicId)return new RelicBearer(unit,1);
            if(_state.GetUnitEquipment2(unit.Id)?.Id==relicId)return new RelicBearer(unit,2);
        }
        return null;
    }
}
