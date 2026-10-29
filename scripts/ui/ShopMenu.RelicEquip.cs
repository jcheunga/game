using System.Linq;
using Godot;

/// <summary>
/// Equipping a relic on the relics page itself: the collection gives way to the allies and a before/after
/// comparison in battle stats, and the detail card's plaque confirms the change. No modal.
/// </summary>
public partial class ShopMenu
{
    private bool _equipping, _removing;
    private string _equipUnit;
    private int _equipSlot = 1;
    private float _allyScroll;

    /// <summary>The change the plaque would make while equipping (null until an ally is chosen).</summary>
    public RelicEquipmentPreview RelicPreview { get; private set; }

    /// <summary>Starts equipping a relic, or removing it from its bearer when it is worn; a unit id picks the ally.</summary>
    public void BeginRelicEquip(string relicId, string unitId = null)
    {
        var holder = EquippedBy(relicId);
        _selectedRelic = relicId; _equipping = true; _removing = holder != null;
        _equipUnit = holder?.Unit.Id ?? unitId; _equipSlot = holder?.Slot ?? 1; _allyScroll = 0;
        Refresh();
    }

    private void EndRelicEquip()
    {
        _equipping = false; RelicPreview = null;
        Refresh();
    }

    private void ApplyRelicEquip()
    {
        if (RelicPreview == null) return;
        var success = _removing ? _state.TryUnequipRelic(_equipUnit, _equipSlot, out _)
            : _equipSlot == 1 ? _state.TryEquipItem(_equipUnit, _selectedRelic) : _state.TryEquipItem2(_equipUnit, _selectedRelic);
        if (success) EndRelicEquip(); else Refresh();
    }

    /// <summary>The collection panel while equipping: allies down the left, the chosen ally's comparison beside them.</summary>
    private void BuildRelicEquip(RoyalSpec spec, EquipmentDefinition relic)
    {
        // The title bar's own wood, from just left of the painted filter, covers it: there is nothing to filter here.
        var filter = new Rect2(716, 151, 130, 42);
        _body.AddChild(new TextureRect { Texture = RoyalArt.Cut("relics", new Rect2(filter.Position - new Vector2(filter.Size.X + 2, 0), filter.Size)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            Position = filter.Position, Size = filter.Size, MouseFilter = MouseFilterEnum.Ignore });
        _body.AddChild(spec.Label("collection.label", $"{(_removing ? "Unequip" : "Equip")} · {relic.DisplayName}", 600));
        RoyalUiTools.ClearPaintedScrollTrack(_body, "relics", spec);
        var first = spec.Rect("card.0.0");
        var area = new Rect2(first.Position - new Vector2(4, 4),
            new Vector2(spec.Rect("grid.scroll").End.X - first.Position.X + 4, spec.Rect("button.back").Position.Y - first.Position.Y - 4));

        // Allies, in the warband's cells. Removal previews the bearer alone.
        const float Cell = 86, Gap = 8;
        const int Columns = 3;
        var allies = _removing ? new[] { GameData.GetUnit(_equipUnit) } : _state.GetOwnedPlayerUnits().ToArray();
        var allyView = new Rect2(area.Position, new Vector2(Columns * Cell + (Columns - 1) * Gap + 12, area.Size.Y));
        var allyScroll = new ScrollContainer { Name = "RelicAllies", Position = allyView.Position, Size = allyView.Size,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever };
        _body.AddChild(allyScroll);
        var rows = Mathf.CeilToInt(allies.Length / (float)Columns);
        var grid = new Control { CustomMinimumSize = new Vector2(allyView.Size.X, Mathf.Max(allyView.Size.Y, rows * (Cell + Gap) + 4)), MouseFilter = MouseFilterEnum.Pass };
        allyScroll.AddChild(grid);
        for (var i = 0; i < allies.Length; i++)
        {
            var unit = allies[i];
            var chosen = unit.Id == _equipUnit;
            var cell = new RoyalButton { Position = new Vector2(4 + i % Columns * (Cell + Gap), 4 + i / Columns * (Cell + Gap)), Size = new Vector2(Cell, Cell),
                AccessibilityName = $"Compare {relic.DisplayName} on {unit.DisplayName}", TooltipText = unit.DisplayName,
                MouseFilter = MouseFilterEnum.Pass, MouseDefaultCursorShape = CursorShape.PointingHand };
            cell.SetStates(RoyalKit.Slice(chosen ? "cell-selected-empty" : "cell-empty", 8), 4);
            var id = unit.Id;
            if (!_removing) cell.Pressed += () => { _equipUnit = id; _equipSlot = 1; Refresh(); };
            cell.AddChild(Picture(false, unit.Id, new Rect2(6, 6, Cell - 12, Cell - 9), false));
            AddCornerTab(cell, chosen, ClassIcon(unit));
            grid.AddChild(cell);
        }
        _body.AddChild(new RoyalScrollHint { Position = new Vector2(allyView.End.X - 4, allyView.Position.Y + 6), Size = new Vector2(3, allyView.Size.Y - 12), Scroll = allyScroll });
        allyScroll.GetVScrollBar().ValueChanged += value => _allyScroll = (float)value;
        Callable.From(() => { if (GodotObject.IsInstanceValid(allyScroll)) { allyScroll.ScrollVertical = (int)_allyScroll; } }).CallDeferred();
        _body.AddChild(new ColorRect { Color = new Color(.91f, .71f, .36f, .22f), Position = new Vector2(allyView.End.X + 4, area.Position.Y + 8),
            Size = new Vector2(1, area.Size.Y - 16), MouseFilter = MouseFilterEnum.Ignore });

        var compare = new Rect2(allyView.End.X + 20, area.Position.Y + 6, area.End.X - allyView.End.X - 30, area.Size.Y - 12);
        RelicPreview = _equipUnit == null ? null : _state.BuildRelicEquipmentPreview(_equipUnit, _removing ? null : relic, _equipSlot);
        if (RelicPreview == null)
        {
            var hint = RoyalText.Serif("Choose an ally to compare", 20, RoyalText.Muted);
            hint.Align = HorizontalAlignment.Center;
            hint.Position = new Vector2(compare.Position.X, compare.GetCenter().Y - 20); hint.Size = new Vector2(compare.Size.X, 40);
            _body.AddChild(hint);
            return;
        }

        var ally = RelicPreview.Unit;
        var heading = RoyalText.Serif($"{ally.DisplayName} · Lv {_state.GetUnitLevel(ally.Id)}", 23);
        heading.Position = compare.Position; heading.Size = new Vector2(compare.Size.X, 32);
        _body.AddChild(heading);

        // Both relic slots; the chosen one is lit. Removal can only pick the slot that holds this relic.
        var slotWidth = (compare.Size.X - 10) / 2;
        foreach (var slot in new[] { 1, 2 })
        {
            var worn = slot == 1 ? _state.GetUnitEquipment(ally.Id) : _state.GetUnitEquipment2(ally.Id);
            var unlocked = slot == 1 || _state.IsUnitPromoted(ally.Id);
            var lit = slot == _equipSlot;
            var button = new RoyalButton { Position = compare.Position + new Vector2((slot - 1) * (slotWidth + 10), 40), Size = new Vector2(slotWidth, 58),
                AccessibilityName = $"Preview relic slot {slot}", TooltipText = unlocked ? $"Slot {slot}" : "Promote this ally to unlock its second relic slot",
                MouseDefaultCursorShape = CursorShape.PointingHand, ToggleMode = true, Disabled = !unlocked || (_removing && worn?.Id != relic.Id) };
            button.SetStates(RoyalKit.Slice(lit ? "cell-selected-empty" : "cell-empty", 8), 4);
            button.SetPressedNoSignal(lit);
            var chosenSlot = slot;
            button.Pressed += () => { _equipSlot = chosenSlot; Refresh(); };
            var caps = RoyalText.Caps($"Slot {slot}", 16, lit ? new Color("f3cf86") : RoyalText.Muted);
            caps.Position = new Vector2(14, 6); caps.Size = new Vector2(slotWidth - 28, 20);
            button.AddChild(caps);
            var name = RoyalText.Serif(unlocked ? worn?.DisplayName ?? "Empty" : "Promote to unlock", 18, unlocked && worn != null ? RoyalText.Cream : RoyalText.Muted);
            button.SetCaption(name, new Rect2(14, 28, slotWidth - 28, 24));
            if (button.Disabled) caps.Modulate = new Color(1, 1, 1, .55f);
            _body.AddChild(button);
        }

        // Notes, then every stat now and after (changes first), scrolling when long.
        var body = new Rect2(compare.Position + new Vector2(0, 110), compare.Size - new Vector2(0, 110));
        var scroll = new ScrollContainer { Name = "RelicComparison", Position = body.Position, Size = body.Size,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever };
        _body.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(column);
        foreach (var limit in RelicPreview.Limits) column.AddChild(RealmUi.Label(RelicStatText.LimitNotice(limit), 18));
        if (!RelicPreview.After.DamagesStructures && !_removing && (relic.StructureDamageScale != 1 || relic.BaseDamageBonus != 0))
            column.AddChild(RealmUi.Label("This ally can't attack the gate, so gate bonuses do nothing.", 18, true));
        if (!RelicPreview.CanApply) column.AddChild(RealmUi.Label(RelicPreview.Reason, 18, true));
        column.AddChild(ArmoryDetailUi.CompareStats(RelicPreview.Current, RelicPreview.After, true));
        _body.AddChild(new RoyalScrollHint { Position = new Vector2(body.End.X + 4, body.Position.Y + 6), Size = new Vector2(3, body.Size.Y - 12), Scroll = scroll });
    }
}
