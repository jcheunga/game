using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>Relics: the collection, a large preview on the pedestal and the parchment detail card.</summary>
public partial class ShopMenu
{
    private string _selectedRelic = "";
    private float _relicScroll;

    public static string RelicIcon(EquipmentDefinition relic)
    {
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
        var view = new Rect2(first.Position - new Vector2(4, 4), new Vector2(track.Position.X - first.Position.X + 2, spec.Rect("button.back").Position.Y - first.Position.Y - 4));
        var scroll = new ScrollContainer { Position = view.Position, Size = view.Size, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever };
        _body.AddChild(scroll);
        var grid = new Control { CustomMinimumSize = new Vector2(view.Size.X - 2, Mathf.Max(view.Size.Y, Mathf.Ceil(visible.Count / 3f) * pitch.Y + 6)), MouseFilter = MouseFilterEnum.Pass };
        scroll.AddChild(grid);
        var labelSpec = spec.Rect("card.0.0");
        for (var i = 0; i < visible.Count; i++)
        {
            var relic = visible[i];
            var rect = new Rect2(new Vector2(4 + i % 3 * pitch.X, 4 + i / 3 * pitch.Y), cell);
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
        var selectedRow = visible.FindIndex(r => r.Id == _selectedRelic) / 3;
        var rowTop = 4 + selectedRow * pitch.Y;
        if (rowTop < _relicScroll || rowTop + cell.Y > _relicScroll + view.Size.Y) _relicScroll = Mathf.Max(0, rowTop - 4);
        Callable.From(() => { if (GodotObject.IsInstanceValid(scroll)) { scroll.ScrollVertical = (int)_relicScroll; } }).CallDeferred();

        var back = RoyalButton.Over(spec.Rect("button.back"), "Back", Close, 6);
        back.SetGlyph(RoyalKit.Texture("icon-back-chevron"), new Rect2(spec.Rect("button.back.icon").Position - spec.Rect("button.back").Position, spec.Rect("button.back.icon").Size));
        var backLabel = spec.Label("button.back.label", "BACK", 90);
        backLabel.Position -= spec.Rect("button.back").Position;
        back.SetCaption(backLabel, new Rect2(backLabel.Position, backLabel.Size));
        _body.AddChild(back);

        // Preview: the relic itself, large, over the pedestal.
        var preview = spec.Rect("preview");
        _body.AddChild(new SpellAura { Position = preview.Position, Size = preview.Size });
        _body.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Texture = RelicArt(selected), Position = preview.Position + new Vector2(30, 60), Size = new Vector2(preview.Size.X - 60, preview.Size.Y - 190),
            MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps, SelfModulate = owned.Contains(selected.Id) ? Colors.White : new Color(.55f, .57f, .6f) });
        var index = visible.FindIndex(r => r.Id == selected.Id);
        void Step(int delta) { _selectedRelic = visible[(index + delta + visible.Count) % visible.Count].Id; Refresh(); }
        _body.AddChild(RoyalButton.Over(spec.Rect("preview.prev").Grow(10), "Previous relic", () => Step(-1), 8));
        _body.AddChild(RoyalButton.Over(spec.Rect("preview.next").Grow(10), "Next relic", () => Step(1), 8));
        var details=RoyalButton.Over(new Rect2(preview.Position.X+44,preview.End.Y-70,preview.Size.X-88,48),
            "View all bonuses",()=>RelicEquipmentInspector.Show(this,selected,Refresh),6);
        details.SetStates(new ModalSurface(ModalMaterial.Steel,8));
        var detailsCaption=RoyalText.Caps("View all bonuses",18);
        detailsCaption.Align=HorizontalAlignment.Center;detailsCaption.Baseline=30;
        details.SetCaption(detailsCaption,new Rect2(8,0,preview.Size.X-104,48));
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
        equip.SetGlyph(RoyalKit.Texture("icon-helm"), new Rect2(spec.Rect("button.equip.icon").Position - equipRect.Position, spec.Rect("button.equip.icon").Size));
        var equipLabel = spec.Label("button.equip.label", holder != null ? "Unequip" : "Equip", 200);
        equipLabel.Position -= equipRect.Position;
        equip.SetCaption(equipLabel, new Rect2(equipLabel.Position, equipLabel.Size));
        _body.AddChild(equip);
        var forgeRect = spec.Rect("button.forge");
        var forge = RoyalButton.Over(forgeRect, "Forge", () => SceneRouter.Instance.GoToForge(), 6);
        forge.SetGlyph(RoyalKit.Texture("icon-hammers"), new Rect2(spec.Rect("button.forge.icon").Position - forgeRect.Position, spec.Rect("button.forge.icon").Size));
        var forgeLabel = spec.Label("button.forge.label", "Forge", 200);
        forgeLabel.Ink = new Color("1a0a04"); forgeLabel.ShadowInk = new Color(1, .95f, .8f, .3f);
        forgeLabel.Position -= forgeRect.Position;
        forge.SetCaption(forgeLabel, new Rect2(forgeLabel.Position, forgeLabel.Size));
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
