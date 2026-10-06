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

    private static string Percent(float value) => $"{(value >= 0 ? "+" : "")}{Mathf.RoundToInt(value * 100)}%";

    /// <summary>The relic's modifiers as stat rows (value, caption, icon), most important first.</summary>
    private static List<(string Value, string Caption, string Icon)> RelicRows(EquipmentDefinition relic)
    {
        var rows = new List<(string, string, string)>();
        if (Mathf.Abs(relic.DamageScale - 1) > .001f) rows.Add((Percent(relic.DamageScale - 1), "Damage", "relicstat-damage"));
        if (Mathf.Abs(relic.HealthScale - 1) > .001f) rows.Add((Percent(relic.HealthScale - 1), "Health", "stat-health"));
        if (Mathf.Abs(relic.CooldownReduction) > .001f) rows.Add((Percent(-relic.CooldownReduction), "Cooldown", "relicstat-cooldown"));
        if (Mathf.Abs(relic.SpeedScale - 1) > .001f) rows.Add((Percent(relic.SpeedScale - 1), "Speed", "stat-courage"));
        if (relic.BaseDamageBonus != 0) rows.Add(($"+{relic.BaseDamageBonus}", "Gate damage", "class-melee"));
        return rows;
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
        var thumb = new Panel { MouseFilter = MouseFilterEnum.Ignore, Position = track.Position, Size = new Vector2(track.Size.X, 60) };
        thumb.AddThemeStyleboxOverride("panel", ThumbStyle());
        _body.AddChild(thumb);
        void PlaceThumb()
        {
            var range = Mathf.Max(1, grid.CustomMinimumSize.Y - view.Size.Y);
            var length = Mathf.Clamp(track.Size.Y * view.Size.Y / grid.CustomMinimumSize.Y, 30, track.Size.Y);
            thumb.Size = new Vector2(track.Size.X, length);
            thumb.Position = new Vector2(track.Position.X, track.Position.Y + (track.Size.Y - length) * Mathf.Clamp(scroll.ScrollVertical / range, 0, 1));
            _relicScroll = scroll.ScrollVertical;
        }
        scroll.GetVScrollBar().ValueChanged += _ => PlaceThumb();
        // Keep the selected relic in view when the page opens or the selection moves.
        var selectedRow = visible.FindIndex(r => r.Id == _selectedRelic) / 3;
        var rowTop = 4 + selectedRow * pitch.Y;
        if (rowTop < _relicScroll || rowTop + cell.Y > _relicScroll + view.Size.Y) _relicScroll = Mathf.Max(0, rowTop - 4);
        Callable.From(() => { if (GodotObject.IsInstanceValid(scroll)) { scroll.ScrollVertical = (int)_relicScroll; PlaceThumb(); } }).CallDeferred();

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
            if (holder != null) { state.UnequipItem(holder.Id); Toast($"Unequipped {selected.DisplayName} from {holder.DisplayName}."); Refresh(); }
            else ChooseRelicBearer(selected);
        }, 6);
        equip.Disabled = !have2;
        equip.TooltipText = !have2 ? "Not owned yet · craft it in the forge" : holder != null ? $"Equipped on {holder.DisplayName}" : "Choose an ally to carry it";
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

    private UnitDefinition EquippedBy(string relicId) =>
        _state.GetOwnedPlayerUnits().FirstOrDefault(unit => _state.GetUnitEquipment(unit.Id)?.Id == relicId);

    private void ChooseRelicBearer(EquipmentDefinition relic)
    {
        var stack = OpenTraits($"Equip {relic.DisplayName}");
        stack.AddChild(RealmUi.Label("Choose the ally who carries this relic.", 18, true));
        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 10);
        stack.AddChild(grid);
        foreach (var unit in _state.GetOwnedPlayerUnits())
        {
            var current = _state.GetUnitEquipment(unit.Id);
            var button = RealmUi.Button("shield", unit.DisplayName, () =>
            {
                _state.TryEquipItem(unit.Id, relic.Id);
                Toast($"Equipped {relic.DisplayName} on {unit.DisplayName}.");
                GetNode<CanvasLayer>("TraitsLayer").QueueFree(); Refresh();
            });
            button.Icon = UnitFigure.For(unit);
            button.TooltipText = current != null ? $"Replaces {current.DisplayName}" : unit.DisplayName;
            button.CustomMinimumSize = new Vector2(160, 56);
            grid.AddChild(button);
        }
        RealmModal.Polish(stack);
    }
}
