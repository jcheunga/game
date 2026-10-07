using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>Warband and spell pages: equipped strip, collection grid, preview and profile.</summary>
public partial class ShopMenu
{
    private string _selectedId = "";
    private int _filter;
    private float _gridScroll;
    private static readonly string[] Filters = { "All", "Owned", "Equipped" };

    private readonly record struct Entry(string Id, string Name, bool Owned, bool Available, bool Equipped, int Level);

    private List<Entry> Entries(bool spells)
    {
        var state = _state;
        IEnumerable<Entry> all = spells
            ? GameData.GetPlayerSpells().Select(s => new Entry(s.Id, s.DisplayName, state.IsSpellOwned(s.Id), state.IsSpellAvailableForPurchase(s.Id), state.IsSpellInActiveDeck(s.Id), state.GetSpellLevel(s.Id)))
            : GameData.GetPlayerUnits().Select(u => new Entry(u.Id, u.DisplayName, state.IsUnitOwned(u.Id), state.IsUnitAvailableForPurchase(u.Id), state.IsUnitInActiveDeck(u.Id), state.GetUnitLevel(u.Id)));
        return all.ToList();
    }

    private IEnumerable<Entry> Filtered(List<Entry> entries) => _filter switch
    {
        1 => entries.Where(e => e.Owned),
        2 => entries.Where(e => e.Equipped),
        _ => entries
    };

    private RoyalSpec RosterSpec(bool spells) => RoyalSpec.For(spells ? "spells" : "warband");

    private void BuildRoster(bool spells)
    {
        var spec = RosterSpec(spells);
        var entries = Entries(spells);
        if (entries.All(e => e.Id != _selectedId))
            _selectedId = (entries.FirstOrDefault(e => e.Equipped).Id ?? entries.FirstOrDefault().Id) ?? "";
        BuildEquippedStrip(spells, spec, entries);
        BuildCollection(spells, spec, entries);
        var selected = entries.FirstOrDefault(e => e.Id == _selectedId);
        if (selected.Id == null) return;
        BuildPreview(spells, spec, entries, selected);
        if (spells) BuildSpellProfile(spec, GameData.GetSpell(selected.Id), selected);
        else BuildUnitProfile(spec, GameData.GetUnit(selected.Id), selected);
    }

    private static string ClassIcon(UnitDefinition unit) => unit.Id switch
    {
        "player_shooter" or "player_ranger" or "player_marksman" or "player_ballista" => "class-ranged",
        "player_defender" or "player_lantern_guard" or "player_banner" => "class-shield",
        _ => unit.SquadTag == SquadSynergyCatalog.SupportTag ? "class-shield" : "class-melee"
    };

    private static string SpellIcon(SpellDefinition spell) => "spellicon-" + spell.Id.Replace("spell_", "");

    private static Texture2D SpellArt(SpellDefinition spell) => RoyalArt.Item(spell.Id) is { } art ? art : UiArtLoader.TryLoadSpellIcon(spell);

    private Control Picture(bool spells, string id, Rect2 rect, bool dim)
    {
        if (spells)
        {
            var art = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Texture = SpellArt(GameData.GetSpell(id)), Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Ignore,
                TextureFilter = TextureFilterEnum.LinearWithMipmaps, SelfModulate = dim ? new Color(.45f, .47f, .5f) : Colors.White };
            return art;
        }
        var figure = new UnitFigure { Position = rect.Position, Size = rect.Size, Tint = dim ? new Color(.45f, .47f, .5f) : Colors.White };
        figure.SetUnit(GameData.GetUnit(id));
        return figure;
    }

    /// <summary>Where the concept places a part inside its slot, applied to any slot.</summary>
    private static Rect2 Relative(RoyalSpec spec, string part, string sampleSlot, Rect2 slot, Rect2 fallback)
    {
        if (!spec.Has(sampleSlot + part)) return new Rect2(slot.Position + fallback.Position, fallback.Size);
        var sample = spec.Rect(sampleSlot); var piece = spec.Rect(sampleSlot + part);
        return new Rect2(slot.Position + (piece.Position - sample.Position), piece.Size);
    }

    private void BuildEquippedStrip(bool spells, RoyalSpec spec, List<Entry> entries)
    {
        var ids = (spells ? _state.ActiveDeckSpellIds : _state.ActiveDeckUnitIds).ToList();
        var limit = spells ? _state.SpellDeckSizeLimit : _state.DeckSizeLimit;
        var emptySample = Enumerable.Range(1, 6).Select(i => $"slot.{i}").FirstOrDefault(key => spec.Has(key + ".plus")) ?? "slot.6";
        for (var slot = 1; spec.Has($"slot.{slot}"); slot++)
        {
            var key = $"slot.{slot}";
            var rect = spec.Rect(key);
            if (slot > limit)
            {
                _body.AddChild(RoyalKit.Image("lock", new Rect2(rect.GetCenter() - new Vector2(13, 15), new Vector2(26, 30)), new Color(1, 1, 1, .55f)));
                continue;
            }
            if (slot > ids.Count)
            {
                // Plus and label are centred in each slot; only their heights come from the concept's empty slot.
                var plus = Relative(spec, ".plus", emptySample, rect, new Rect2(rect.Size.X / 2 - 17, 22, 34, 34));
                plus.Position = new Vector2(rect.GetCenter().X - plus.Size.X / 2, plus.Position.Y);
                _body.AddChild(RoyalKit.Image("empty-plus", plus));
                var empty = spec.Label(emptySample + ".label", "Empty slot", rect.Size.X);
                empty.Align = HorizontalAlignment.Center;
                empty.Position = new Vector2(rect.Position.X, empty.Position.Y + rect.Position.Y - spec.Rect(emptySample).Position.Y);
                empty.Size = new Vector2(rect.Size.X, empty.Size.Y);
                _body.AddChild(empty);
                continue;
            }
            var entry = entries.FirstOrDefault(e => e.Id == ids[slot - 1]);
            if (entry.Id == null) continue;
            if (entry.Id == _selectedId)
            {
                var warmth = new Panel { Position = rect.Position + new Vector2(5, 5), Size = rect.Size - new Vector2(10, 10), MouseFilter = MouseFilterEnum.Ignore };
                warmth.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(.62f, .42f, .2f, .34f) });
                _body.AddChild(warmth);
                _body.AddChild(RoyalKit.Frame("card-selected", rect.Grow(1), 12, false));
            }
            var art = spec.Rect(key + ".art", new Rect2(rect.Position + new Vector2(rect.Size.X * .3f, 4), new Vector2(rect.Size.X * .5f, rect.Size.Y - 10)));
            if (!spells) art = new Rect2(new Vector2(rect.Position.X + rect.Size.X * .25f, rect.Position.Y + 3), new Vector2(rect.Size.X * .56f, rect.Size.Y - 8));
            _body.AddChild(Picture(spells, entry.Id, art, !entry.Owned));
            var icon = spells ? SpellIcon(GameData.GetSpell(entry.Id)) : ClassIcon(GameData.GetUnit(entry.Id));
            _body.AddChild(RoyalKit.Image(icon, Relative(spec, ".class", "slot.1", rect, new Rect2(9, 63, 24, 27))));
            var level = spec.Label("slot.1.level", $"Lv {entry.Level}", 70);
            level.Position += rect.Position - spec.Rect("slot.1").Position;
            _body.AddChild(level);
            // Spells show their level pips; units only their level number, since unit levels will keep growing.
            if (spells)
            {
                var pipRect = Relative(spec, ".pips", "slot.1", rect, new Rect2(rect.Size.X - 80, 75, 70, 15));
                _body.AddChild(RoyalKit.Pips(pipRect.Position, Mathf.Min(entry.Level, _state.MaxSpellLevel), _state.MaxSpellLevel, pipRect.Size.X / 4f, pipRect.Size.Y));
            }
            var id = entry.Id;
            var hotspot = RoyalButton.Over(rect, entry.Name, () => { _selectedId = id; Refresh(); }, 6);
            hotspot.TooltipText = $"{entry.Name} · equipped";
            _body.AddChild(hotspot);
        }
    }

    private void BuildCollection(bool spells, RoyalSpec spec, List<Entry> entries)
    {
        _body.AddChild(spec.Label("collection.label", spells ? "Spell collection" : "Unit Collection", 190));
        var filterRect = spec.Rect("collection.filter");
        var filter = RoyalButton.Over(filterRect, "Filter collection", () => { _filter = (_filter + 1) % Filters.Length; _gridScroll = 0; Refresh(); }, 4);
        _body.AddChild(filter);
        var filterLabel = spec.Label("collection.filter.label", Filters[_filter], 60);
        filterLabel.Position -= filterRect.Position;
        filter.SetCaption(filterLabel, new Rect2(filterLabel.Position, filterLabel.Size));
        filter.TooltipText = $"Showing {Filters[_filter].ToLowerInvariant()} · tap to change";

        var first = spec.Rect("grid.0.0");
        var second = spec.Rect("grid.0.1");
        var below = spec.Rect("grid.1.0");
        var track = spec.Rect("grid.scroll");
        var cell = new Vector2(Mathf.Round((first.Size.X + second.Size.X) / 2), first.Size.Y);
        var pitch = new Vector2(second.Position.X - first.Position.X, below.Position.Y - first.Position.Y);
        var view = new Rect2(first.Position - new Vector2(4, 4), new Vector2(track.Position.X - first.Position.X + 2, track.End.Y - first.Position.Y + 6));
        var scroll = new ScrollContainer { Position = view.Position, Size = view.Size, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever, Name = "Collection" };
        _body.AddChild(scroll);
        var visible = Filtered(entries).ToList();
        var rows = Mathf.CeilToInt(visible.Count / 3f);
        var grid = new Control { CustomMinimumSize = new Vector2(view.Size.X - 2, Mathf.Max(view.Size.Y, rows * pitch.Y + 8)), MouseFilter = MouseFilterEnum.Pass };
        scroll.AddChild(grid);
        for (var i = 0; i < visible.Count; i++)
        {
            var at = new Vector2(4 + i % 3 * pitch.X, 4 + i / 3 * pitch.Y);
            grid.AddChild(Cell(spells, spec, visible[i], new Rect2(at, cell)));
        }
        // The plate paints the scroll track; a brass thumb follows the list.
        var thumb = new Panel { MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(track.Size.X, 40), Position = track.Position };
        thumb.AddThemeStyleboxOverride("panel", ThumbStyle());
        _body.AddChild(thumb);
        void PlaceThumb()
        {
            var range = Mathf.Max(1, grid.CustomMinimumSize.Y - view.Size.Y);
            var length = Mathf.Clamp(track.Size.Y * view.Size.Y / grid.CustomMinimumSize.Y, 30, track.Size.Y);
            thumb.Size = new Vector2(track.Size.X, length);
            thumb.Position = new Vector2(track.Position.X, track.Position.Y + (track.Size.Y - length) * Mathf.Clamp(scroll.ScrollVertical / range, 0, 1));
            thumb.Visible = grid.CustomMinimumSize.Y > view.Size.Y + 1;
            _gridScroll = scroll.ScrollVertical;
        }
        scroll.GetVScrollBar().ValueChanged += _ => PlaceThumb();
        var selectedRow = visible.FindIndex(e => e.Id == _selectedId) / 3;
        var rowTop = 4 + selectedRow * pitch.Y;
        if (selectedRow >= 0 && (rowTop < _gridScroll || rowTop + cell.Y > _gridScroll + view.Size.Y)) _gridScroll = Mathf.Max(0, rowTop - 4);
        Callable.From(() => { if (GodotObject.IsInstanceValid(scroll)) { scroll.ScrollVertical = (int)_gridScroll; PlaceThumb(); } }).CallDeferred();
    }

    private static StyleBox ThumbStyle()
    {
        var box = new StyleBoxFlat { BgColor = new Color("8d7046"), BorderColor = new Color("d8b878"), AntiAliasing = true };
        box.SetBorderWidthAll(1); box.SetCornerRadiusAll(3);
        return box;
    }

    private Control Cell(bool spells, RoyalSpec spec, Entry entry, Rect2 rect)
    {
        var selected = entry.Id == _selectedId;
        var button = new RoyalButton { Position = rect.Position, Size = rect.Size, AccessibilityName = entry.Name, MouseDefaultCursorShape = CursorShape.PointingHand,
            TooltipText = entry.Name + (entry.Owned ? entry.Equipped ? " · Equipped" : " · Reserve" : " · Not owned"), MouseFilter = MouseFilterEnum.Pass };
        button.SetStates(RoyalKit.Slice(selected ? "cell-selected-empty" : "cell-empty", 8), 4);
        var id = entry.Id;
        button.Pressed += () => { _selectedId = id; Refresh(); };
        button.AddChild(Picture(spells, entry.Id, spells ? new Rect2(10, 10, rect.Size.X - 20, rect.Size.Y - 20) : new Rect2(6, 6, rect.Size.X - 12, rect.Size.Y - 9), !entry.Owned));
        var tab = new Panel { Position = new Vector2(1.5f, 2), Size = new Vector2(26, 25), MouseFilter = MouseFilterEnum.Ignore };
        var tabStyle = new StyleBoxFlat { BgColor = new Color(selected ? "2b2016" : "1c1b1a"), BorderColor = new Color(selected ? "e9b45c" : "8e7550"), AntiAliasing = true };
        tabStyle.BorderWidthRight = tabStyle.BorderWidthBottom = 1; tabStyle.CornerRadiusBottomRight = 7; tabStyle.CornerRadiusTopLeft = 2;
        tab.AddThemeStyleboxOverride("panel", tabStyle);
        button.AddChild(tab);
        var icon = spells ? SpellIcon(GameData.GetSpell(entry.Id)) : ClassIcon(GameData.GetUnit(entry.Id));
        button.AddChild(RoyalKit.Image(icon, new Rect2(5.5f, 6, 17, 17)));
        if (spells && spec.Has("grid.0.0.level"))
        {
            var origin = spec.Rect("grid.0.0").Position;
            var level = spec.Label("grid.0.0.level", $"Lv {entry.Level}", 40);
            level.Position -= origin;
            button.AddChild(level);
            var pips = spec.Rect("grid.0.0.pips");
            button.AddChild(RoyalKit.Pips(pips.Position - origin, Mathf.Min(entry.Level, _state.MaxSpellLevel), _state.MaxSpellLevel, pips.Size.X / 3f, pips.Size.Y));
        }
        if (!entry.Owned && !entry.Available)
            button.AddChild(RoyalKit.Image("lock", new Rect2(rect.Size.X - 24, rect.Size.Y - 24, 16, 16), new Color(1, 1, 1, .8f)));
        return button;
    }

    private void BuildPreview(bool spells, RoyalSpec spec, List<Entry> entries, Entry selected)
    {
        var rect = spec.Rect("preview");
        if (spells)
        {
            var glow = new SpellAura { Position = rect.Position, Size = rect.Size };
            _body.AddChild(glow);
            var art = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Texture = SpellArt(GameData.GetSpell(selected.Id)), MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps,
                Position = rect.Position + new Vector2(rect.Size.X * .14f, rect.Size.Y * .12f), Size = new Vector2(rect.Size.X * .72f, rect.Size.Y * .62f),
                SelfModulate = selected.Owned ? Colors.White : new Color(.55f, .57f, .6f) };
            _body.AddChild(art);
        }
        else
        {
            // The animated battle model stands on the painted pedestal.
            var model = new UnitModelPreview { Position = new Vector2(rect.GetCenter().X - 165, rect.Position.Y + 12), Size = new Vector2(330, rect.Size.Y - 66), AlignBottom = true,
                MouseFilter = MouseFilterEnum.Ignore };
            model.SizeFlagsHorizontal = model.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            _body.AddChild(model);
            model.SetUnit(GameData.GetUnit(selected.Id));
            model.Modulate = selected.Owned ? Colors.White : new Color(.6f, .62f, .66f);
        }
        var visible = Filtered(entries).ToList();
        var index = Mathf.Max(0, visible.FindIndex(e => e.Id == selected.Id));
        void Step(int delta)
        {
            if (visible.Count == 0) return;
            _selectedId = visible[(index + delta + visible.Count) % visible.Count].Id;
            Refresh();
        }
        _body.AddChild(RoyalButton.Over(spec.Rect("preview.prev").Grow(10), spells ? "Previous spell" : "Previous unit", () => Step(-1), 8));
        _body.AddChild(RoyalButton.Over(spec.Rect("preview.next").Grow(10), spells ? "Next spell" : "Next unit", () => Step(1), 8));
    }
}

/// <summary>Warm light behind a spell picture on the preview pedestal.</summary>
public partial class SpellAura : Control
{
    public SpellAura() { MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Draw()
    {
        var centre = Size * new Vector2(.5f, .45f);
        for (var i = 14; i > 0; i--) DrawCircle(centre, Mathf.Min(Size.X, Size.Y) * (.12f + i * .022f), new Color(1f, .72f, .35f, .02f));
    }
}
