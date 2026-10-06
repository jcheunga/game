using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// The relic forge on the approved concept. Tap a relic in the inventory to select it: Dismantle
/// melts the selection into shards, tapping an empty fuse socket places it there, and three relics
/// of one rarity fuse into one of the next. Craft forges a chosen recipe for gold and shards.
/// </summary>
public partial class ForgeMenu : RoyalScreen
{
    private readonly List<string> _selectedFuseRelics = new();
    private string _dismantleId = "", _craftId = "";
    private int _recipeTop;
    private float _inventoryScroll;
    private Control _layer;

    public ForgeMenu() { PlateName = "forge"; }

    private static RoyalSpec Spec => RoyalSpec.For("forge");

    protected override void Build()
    {
        _layer = Layer("Live");
        RefreshUi();
    }

    private static string Frame(EquipmentDefinition relic) => relic.Rarity.ToLowerInvariant() switch
    {
        "epic" or "hardened" or "legendary" => "forge-slot-purple", "rare" => "forge-slot-blue", _ => "forge-slot-grey"
    };

    private void RefreshUi()
    {
        if (_layer == null) return;
        RoyalUiTools.Clear(_layer);
        var spec = Spec;
        var state = GameState.Instance;
        _layer.AddChild(spec.Label("title", "Relic Forge", 500));
        _layer.AddChild(RoyalButton.Over(spec.Rect("close"), "Close panel", Close, 6));
        _layer.AddChild(RoyalKit.Image("forge-gold", spec.Rect("resource.gold.icon")));
        _layer.AddChild(spec.Label("resource.gold.value", state.Gold.ToString("N0"), 100));
        _layer.AddChild(RoyalKit.Image("forge-shard", spec.Rect("resource.shard.icon")));
        _layer.AddChild(spec.Label("resource.shard.value", state.RelicShards.ToString("N0"), 100));
        _layer.AddChild(spec.Label("dismantle.header.title", "Dismantle", 260));
        _layer.AddChild(spec.Label("dismantle.header.subtitle", "SELECT RELICS", 260));
        _layer.AddChild(spec.Label("fuse.header.title", "Fuse", 260));
        _layer.AddChild(spec.Label("fuse.header.subtitle", "THREE OF ONE RARITY", 260));
        _layer.AddChild(spec.Label("craft.header.title", "Craft", 220));
        BuildInventory(spec);
        BuildFuse(spec);
        BuildCraft(spec);
        Footer(spec, "armory", "icon-armory", "ARMORY", () => SceneRouter.Instance.GoToShop(3));
        Footer(spec, "map", "icon-map-gold", "BACK TO MAP", Close);
    }

    private RoyalButton Action(RoyalSpec spec, string key, string icon, string text, Action run, bool enabled, string hint)
    {
        var rect = spec.Rect($"button.{key}");
        var button = RoyalButton.Over(rect, text, run, 6);
        button.Disabled = !enabled;
        button.TooltipText = hint;
        button.SetGlyph(RoyalKit.Texture(icon), new Rect2(spec.Rect($"button.{key}.icon").Position - rect.Position, spec.Rect($"button.{key}.icon").Size));
        var label = spec.Label($"button.{key}.label", text, 200);
        label.Ink = new Color("140803"); label.ShadowInk = new Color(1, .95f, .8f, .3f);
        label.Position -= rect.Position;
        button.SetCaption(label, new Rect2(label.Position, label.Size));
        _layer.AddChild(button);
        return button;
    }

    private void Footer(RoyalSpec spec, string key, string icon, string text, Action run)
    {
        var rect = spec.Rect($"button.{key}");
        var button = RoyalButton.Over(rect, text, run, 6);
        button.SetGlyph(RoyalKit.Texture(icon), new Rect2(spec.Rect($"button.{key}.icon").Position - rect.Position, spec.Rect($"button.{key}.icon").Size));
        var label = spec.Label($"button.{key}.label", text, 200);
        label.Position -= rect.Position;
        button.SetCaption(label, new Rect2(label.Position, label.Size));
        _layer.AddChild(button);
    }

    private static TextureRect Art(EquipmentDefinition relic, Rect2 rect, bool dim = false) => new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        Texture = ShopMenu.RelicArt(relic), Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Ignore,
        TextureFilter = TextureFilterEnum.LinearWithMipmaps, SelfModulate = dim ? new Color(.5f, .5f, .52f) : Colors.White
    };

    private void BuildInventory(RoyalSpec spec)
    {
        var state = GameState.Instance;
        var owned = state.GetOwnedEquipment().ToArray();
        if (!owned.Contains(_dismantleId)) _dismantleId = owned.FirstOrDefault() ?? "";
        var first = spec.Rect("slot.0"); var right = spec.Rect("slot.1"); var below = spec.Rect("slot.4");
        var cell = new Vector2(80, 78);
        var pitch = new Vector2(right.Position.X - first.Position.X + 1, below.Position.Y - first.Position.Y);
        var view = spec.Rect("dismantle.grid");
        var scroll = new ScrollContainer { Position = view.Position, Size = view.Size, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever };
        _layer.AddChild(scroll);
        var count = Math.Max(16, (owned.Length + 3) / 4 * 4);
        var grid = new Control { CustomMinimumSize = new Vector2(view.Size.X, Mathf.Max(view.Size.Y, (count / 4) * pitch.Y + 6)), MouseFilter = MouseFilterEnum.Pass };
        scroll.AddChild(grid);
        var origin = new Vector2(first.Position.X - view.Position.X + 2, first.Position.Y - view.Position.Y + 1);
        for (var i = 0; i < count; i++)
        {
            var rect = new Rect2(origin + new Vector2(i % 4 * pitch.X, i / 4 * pitch.Y), cell);
            if (i >= owned.Length)
            {
                grid.AddChild(new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Ignore }.With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("forge-slot-grey", 10))));
                continue;
            }
            var relic = GameData.GetEquipment(owned[i]);
            var selected = relic.Id == _dismantleId;
            var inFuse = _selectedFuseRelics.Contains(relic.Id);
            var slot = new RoyalButton { Position = rect.Position, Size = rect.Size, AccessibilityName = relic.DisplayName, MouseFilter = MouseFilterEnum.Pass,
                TooltipText = $"{relic.DisplayName} · {relic.Rarity} · {RelicForgeCatalog.GetDismantleShards(relic.Rarity)} shards", MouseDefaultCursorShape = CursorShape.PointingHand };
            slot.SetStates(RoyalKit.Slice(selected ? "forge-slot-selected" : Frame(relic), 10), 6);
            slot.AddChild(Art(relic, new Rect2(8, 7, rect.Size.X - 16, rect.Size.Y - 14), inFuse));
            if (selected) slot.AddChild(RoyalKit.Image("forge-check", new Rect2(4, 4, 26, 27)));
            var id = relic.Id;
            slot.Pressed += () => { _dismantleId = id; RefreshUi(); };
            grid.AddChild(slot);
        }
        scroll.GetVScrollBar().ValueChanged += value => _inventoryScroll = (float)value;
        Callable.From(() => { if (GodotObject.IsInstanceValid(scroll)) scroll.ScrollVertical = (int)_inventoryScroll; }).CallDeferred();
        var chosen = _dismantleId.Length > 0 ? GameData.GetEquipment(_dismantleId) : null;
        Action(spec, "dismantle", "icon-dismantle", "Dismantle", () =>
        {
            if (GameState.Instance.TryDismantleRelic(_dismantleId, out var gained))
            {
                RoyalToast.Show(this, $"Dismantled for +{gained} shards.");
                _selectedFuseRelics.Remove(_dismantleId);
                RefreshUi();
            }
        }, chosen != null && !_selectedFuseRelics.Contains(_dismantleId),
            chosen == null ? "No relics to dismantle" : $"Melt {chosen.DisplayName} into {RelicForgeCatalog.GetDismantleShards(chosen.Rarity)} shards");
    }

    private void BuildFuse(RoyalSpec spec)
    {
        var state = GameState.Instance;
        var owned = state.GetOwnedEquipment();
        _selectedFuseRelics.RemoveAll(id => !owned.Contains(id));
        var selected = _dismantleId.Length > 0 ? GameData.GetEquipment(_dismantleId) : null;
        for (var i = 0; i < 3; i++)
        {
            var key = $"fuse.socket.{i + 1}";
            var rect = spec.Rect(key);
            var socket = RoyalButton.Over(rect, "Fuse socket", null, 6);
            if (i < _selectedFuseRelics.Count)
            {
                var relic = GameData.GetEquipment(_selectedFuseRelics[i]);
                socket.AddThemeStyleboxOverride("normal", RoyalKit.Slice(Frame(relic), 10));
                socket.AddChild(Art(relic, new Rect2(9, 8, rect.Size.X - 18, rect.Size.Y - 24)));
                socket.AccessibilityName = relic.DisplayName;
                socket.TooltipText = $"{relic.DisplayName} · tap to remove";
                var id = relic.Id;
                socket.Pressed += () => { _selectedFuseRelics.Remove(id); RefreshUi(); };
            }
            else
            {
                var canPlace = selected != null && !_selectedFuseRelics.Contains(selected.Id) && RelicForgeCatalog.GetFusionTargetRarity(selected.Rarity) != null
                    && _selectedFuseRelics.All(id => GameData.GetEquipment(id).Rarity == selected.Rarity);
                socket.TooltipText = canPlace ? $"Place {selected.DisplayName}" : "Select a relic, then tap a socket";
                socket.AccessibilityName = canPlace ? $"Place {selected.DisplayName}" : "Empty fuse socket";
                socket.Disabled = !canPlace;
                socket.Pressed += () => { _selectedFuseRelics.Add(selected.Id); RefreshUi(); };
                if (canPlace) socket.AddChild(RoyalKit.Image("empty-plus", new Rect2(rect.Size.X / 2 - 14, rect.Size.Y / 2 - 22, 28, 28), new Color(1, 1, 1, .45f)));
            }
            _layer.AddChild(socket);
        }
        var result = spec.Rect("fuse.result");
        var ready = _selectedFuseRelics.Count == 3;
        if (_selectedFuseRelics.Count > 0)
        {
            var rarity = GameData.GetEquipment(_selectedFuseRelics[0]).Rarity;
            var target = RelicForgeCatalog.GetFusionTargetRarity(rarity);
            var preview = target == null ? null : RelicForgeCatalog.GetRelicsByRarity(target).FirstOrDefault();
            if (preview != null)
            {
                var mystery = Art(preview, new Rect2(result.Position + new Vector2(14, 14), result.Size - new Vector2(28, 28)));
                mystery.SelfModulate = ready ? new Color(1, .92f, .75f) : new Color(.25f, .22f, .2f, .8f);
                _layer.AddChild(mystery);
                var hint = RoyalText.Caps(ready ? target.ToUpperInvariant() : $"{_selectedFuseRelics.Count} / 3", 14, new Color("f3dfa6"), 700);
                hint.Align = HorizontalAlignment.Center;
                RoyalText.Place(_layer, hint, result.Position.X, result.End.Y - 30, result.Size.X, 22);
            }
        }
        var tier = _selectedFuseRelics.Count == 0 ? 0 : GameData.GetEquipment(_selectedFuseRelics[0]).Rarity.ToLowerInvariant() switch { "common" => 2, "rare" => 3, "epic" => 4, _ => 5 };
        var pips = spec.Rect("fuse.result.pips");
        _layer.AddChild(RoyalKit.Pips(new Vector2(pips.Position.X + 22, pips.Position.Y + 5), tier, 5, 16, 15));
        Action(spec, "fuse", "icon-fuse", "Fuse", () =>
        {
            if (GameState.Instance.TryFuseRelics(_selectedFuseRelics.ToArray(), out var made))
            {
                RoyalToast.Show(this, "Fused into " + GameData.GetEquipment(made).DisplayName + ".");
                _selectedFuseRelics.Clear();
                RefreshUi();
            }
        }, ready, ready ? "Fuse the three relics" : "Place three relics of one rarity");
    }

    private void BuildCraft(RoyalSpec spec)
    {
        var state = GameState.Instance;
        var owned = state.GetOwnedEquipment();
        var candidates = GameData.GetAllEquipment().Where(e => !owned.Contains(e.Id) && RelicForgeCatalog.GetCraftRecipe(e.Id) != null).ToArray();
        if (candidates.Length == 0)
        {
            var done = RoyalText.Paragraph("Every recipe is already in your collection.", 18, RoyalText.Cream, 500, HorizontalAlignment.Center);
            RoyalText.Place(_layer, done, spec.Rect("craft.preview").Position.X + 20, 300, spec.Rect("craft.preview").Size.X - 40, 80);
            return;
        }
        if (!candidates.Any(e => e.Id == _craftId)) _craftId = candidates[0].Id;
        var index = Array.FindIndex(candidates, e => e.Id == _craftId);
        if (index < _recipeTop) _recipeTop = index;
        if (index >= _recipeTop + 5) _recipeTop = index - 4;
        _recipeTop = Math.Clamp(_recipeTop, 0, Math.Max(0, candidates.Length - 5));
        for (var i = 0; i < 5 && _recipeTop + i < candidates.Length; i++)
        {
            var relic = candidates[_recipeTop + i];
            var rect = spec.Rect($"recipe.{i + 1}");
            var selected = relic.Id == _craftId;
            var slot = RoyalButton.Over(rect, relic.DisplayName, null, 6);
            slot.SetStates(RoyalKit.Slice(selected ? "forge-recipe-selected" : "forge-recipe", 10), 6);
            slot.AddChild(Art(relic, new Rect2(14, 6, rect.Size.X - 28, rect.Size.Y - (selected ? 24 : 12))));
            if (selected)
            {
                var caption = spec.Label("recipe.1.caption", relic.DisplayName, rect.Size.X - 8);
                caption.Align = HorizontalAlignment.Center;
                caption.Position = new Vector2(4, caption.Position.Y - spec.Rect("recipe.1").Position.Y);
                caption.Size = new Vector2(rect.Size.X - 8, caption.Size.Y);
                slot.AddChild(caption);
            }
            var id = relic.Id;
            slot.Pressed += () => { _craftId = id; RefreshUi(); };
            _layer.AddChild(slot);
        }
        if (candidates.Length > 5)
        {
            var list = spec.Rect("craft.list");
            for (var side = -1; side <= 1; side += 2)
            {
                var direction = side;
                var rect = new Rect2(list.GetCenter().X - 14, side < 0 ? list.Position.Y - 6 : list.End.Y - 22, 28, 20);
                var arrow = RoyalButton.Over(rect, side < 0 ? "Earlier recipes" : "More recipes", () => { _recipeTop += direction * 5; RefreshUi(); }, 4);
                arrow.SetGlyph(RoyalKit.Texture(side < 0 ? "icon-chevron-left" : "icon-chevron-right"), new Rect2(9, 2, 10, 16));
                arrow.Glyph.PivotOffset = new Vector2(5, 8); arrow.Glyph.Rotation = Mathf.Pi / 2;
                arrow.Disabled = side < 0 ? _recipeTop == 0 : _recipeTop + 5 >= candidates.Length;
                _layer.AddChild(arrow);
            }
        }
        var chosen = GameData.GetEquipment(_craftId);
        var recipe = RelicForgeCatalog.GetCraftRecipe(_craftId);
        var previewRect = spec.Rect("craft.preview");
        var title = spec.Label("craft.preview.title", chosen.DisplayName, previewRect.Size.X - 20);
        title.Align = HorizontalAlignment.Center;
        title.Position = new Vector2(previewRect.Position.X + 10, title.Position.Y); title.Size = new Vector2(previewRect.Size.X - 20, title.Size.Y);
        _layer.AddChild(new SpellAura { Position = previewRect.Position + new Vector2(0, 30), Size = previewRect.Size - new Vector2(0, 60) });
        _layer.AddChild(Art(chosen, new Rect2(previewRect.Position + new Vector2(50, 48), new Vector2(previewRect.Size.X - 100, 190))));
        _layer.AddChild(title);
        _layer.AddChild(RoyalKit.Image("forge-cost-gold", spec.Rect("craft.cost.gold.icon")));
        _layer.AddChild(spec.Label("craft.cost.gold.value", recipe.GoldCost.ToString("N0"), 60, state.Gold >= recipe.GoldCost ? null : new Color("e88f7a")));
        _layer.AddChild(RoyalKit.Image("forge-cost-shard", spec.Rect("craft.cost.shard.icon")));
        _layer.AddChild(spec.Label("craft.cost.shard.value", recipe.ShardCost.ToString(), 50, state.RelicShards >= recipe.ShardCost ? null : new Color("e88f7a")));
        var affordable = state.Gold >= recipe.GoldCost && state.RelicShards >= recipe.ShardCost;
        Action(spec, "craft", "icon-craft", "Craft", () =>
        {
            GameState.Instance.TryForgeRelic(_craftId, out var message);
            RoyalToast.Show(this, message);
            RefreshUi();
        }, affordable, affordable ? $"Forge {chosen.DisplayName}" : "Not enough gold or shards");
    }
}
