using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>Relics: the collection beside the parchment detail card.</summary>
public partial class ShopMenu
{
    private string _selectedRelic = "";
    private float _relicScroll;

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

    /// <summary>The relic's modifiers as stat rows (value, caption, icon), most important first.</summary>
    private static List<(string Value, string Caption, string Icon)> RelicRows(EquipmentDefinition relic)
    {
        return RelicStatText.Stats(relic).Select(row=>(row.Value,row.Label,row.Icon switch
        {"heart"=>"stat-health","shield"=>"class-shield","crystal"=>"crystal",
         "clock"=>"relicstat-cooldown","eye"=>"stat-range","arrow"=>"stat-courage",_=>"relicstat-damage"})).ToList();
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
        if (visible.All(r => r.Id != _selectedRelic)) _selectedRelic = (visible.FirstOrDefault(r => owned.Contains(r.Id)) ?? visible[0]).Id;
        var selected = all.First(r => r.Id == _selectedRelic);

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
                Texture = RelicArt(relic), Position = new Vector2(10, 8), Size = new Vector2(cell.X - 20, 92), MouseFilter = MouseFilterEnum.Ignore,
                TextureFilter = TextureFilterEnum.LinearWithMipmaps, SelfModulate = have ? Colors.White : new Color(.42f, .44f, .48f) });
            var plate = new Panel { Position = new Vector2(1, 1), Size = new Vector2(32, 35), MouseFilter = MouseFilterEnum.Ignore };
            var plateStyle = new StyleBoxFlat { BgColor = new Color(isSelected ? "2b2016" : "1b1a19"), BorderColor = new Color(isSelected ? "e9b45c" : "8e7550"), AntiAliasing = true };
            plateStyle.BorderWidthRight = plateStyle.BorderWidthBottom = 1; plateStyle.CornerRadiusBottomRight = 8; plateStyle.CornerRadiusTopLeft = 3;
            plate.AddThemeStyleboxOverride("panel", plateStyle);
            card.AddChild(plate);
            card.AddChild(RoyalKit.Image(RelicIcon(relic), new Rect2(7, 6, 19, 24)));
            var label = spec.Label("card.0.0.label", relic.DisplayName, cell.X - 10, have ? null : new Color("9c968c"));
            label.Position -= labelSpec.Position;
            card.AddChild(label);
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

        var back = RoyalButton.Over(spec.Rect("button.back"), "Back", Close, 6);
        back.Arrange(spec.Label("button.back.label", "Back", 90), RoyalKit.Texture("icon-back-chevron"), spec.Rect("button.back.icon").Size, gap: 10);
        _body.AddChild(back);

        var detailsRect = spec.Rect("button.details");
        var details = RoyalButton.Over(detailsRect, "View all bonuses", () => RelicEquipmentInspector.Show(this, selected, Refresh), 6);
        var detailsLabel = spec.Label("button.details.label", "VIEW ALL BONUSES", detailsRect.Size.X - 24);
        detailsLabel.Position -= detailsRect.Position;
        details.SetCaption(detailsLabel, new Rect2(detailsLabel.Position, detailsLabel.Size));
        _body.AddChild(details);

        // Detail card.
        _body.AddChild(RoyalKit.Image(RelicIcon(selected) == "relicicon-lantern" ? "relic-title-icon" : RelicIcon(selected), spec.Rect("detail.icon")));
        _body.AddChild(spec.Label("detail.name", selected.DisplayName, spec.Rect("detail.titlebar").End.X - spec.Number("detail.name", "pen_x", 955) - 10));
        var description = RoyalText.Paragraph(selected.Description, (int)spec.Number("detail.desc.1", "size", 17), new Color("1c120a"), 700);
        description.RemoveThemeColorOverride("font_shadow_color");
        description.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        description.Position = new Vector2(spec.Number("detail.desc.1", "pen_x", 897), spec.Number("detail.desc.1", "baseline", 256) - 18);
        description.Size = new Vector2(spec.Rect("detail.stats").End.X - description.Position.X, 46);
        description.AddThemeConstantOverride("line_spacing", -1);
        RoyalText.FitLines(description, 2);
        _body.AddChild(description);
        var rows = RelicRows(selected).Take(2).ToList();
        rows.Add((selected.Rarity.ToUpperInvariant(), "Rarity", "relicstat-rarity"));
        for (var i = 0; i < 3; i++)
        {
            if (i >= rows.Count) break;
            _body.AddChild(RoyalKit.Image(rows[i].Icon, spec.Rect($"stat.{i}.icon")));
            _body.AddChild(spec.Label($"stat.{i}.value", rows[i].Value, 200));
            _body.AddChild(spec.Label($"stat.{i}.caption", rows[i].Caption, 200));
        }

        var holder = EquippedBy(selected.Id);
        var have2 = owned.Contains(selected.Id);
        var equipRect = spec.Rect("button.equip");
        var equip = RoyalButton.Over(equipRect, holder != null ? "Unequip" : "Equip", () =>
        {
            if (holder != null) RelicEquipmentInspector.Show(this,selected,Refresh,holder.Unit,holder.Slot);
            else ChooseRelicBearer(selected);
        }, 6);
        equip.Disabled = !have2;
        equip.TooltipText = !have2 ? "Not owned yet · craft it in the forge" : holder != null ? $"Equipped on {holder.Unit.DisplayName} · slot {holder.Slot}" : "Choose an ally to compare";
        // The two big plaques share one capitals size with the other primary actions (result board, forge).
        var equipLabel = spec.Label("button.equip.label", holder != null ? "Unequip" : "Equip", 200);
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

    private void ChooseRelicBearer(EquipmentDefinition relic)
    {
        RelicEquipmentInspector.Show(this,relic,Refresh);
    }
}
