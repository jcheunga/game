using System;
using System.Linq;
using Godot;

/// <summary>
/// Endless survival on the approved concept: route picture with pager, three opening boons, the
/// warband and spells marching out, and Begin endless march with its food cost.
/// </summary>
public partial class EndlessMenu : RoyalScreen
{
    private string _selectedRouteId = "city";
    private string _selectedBoonId = EndlessBoonCatalog.SurplusCourageId;
    private int _boonPage;
    private Control _layer;

    public EndlessMenu() { PlateName = "endless"; }

    private static RoyalSpec Spec => RoyalSpec.For("endless");
    private static string[] Routes => GameData.Stages.Select(stage => RouteCatalog.Normalize(stage.MapId)).Distinct().ToArray();

    protected override void Build()
    {
        _selectedRouteId = RouteCatalog.Normalize(GameState.Instance.SelectedEndlessRouteId);
        _selectedBoonId = EndlessBoonCatalog.Normalize(GameState.Instance.SelectedEndlessBoonId);
        _boonPage = Math.Max(0, Array.FindIndex(OrderedBoons(), b => b.Id == _selectedBoonId)) / 3;
        _layer = Layer("Live");
        Refresh();
    }

    /// <summary>The concept's opening trio first: courage, supplies, then the wagon.</summary>
    private static EndlessBoonDefinition[] OrderedBoons()
    {
        var first = new[] { EndlessBoonCatalog.SurplusCourageId, EndlessBoonCatalog.SalvageCacheId, EndlessBoonCatalog.ReinforcedBusId };
        return EndlessBoonCatalog.GetAll().OrderBy(b => Array.IndexOf(first, b.Id) is var i && i >= 0 ? i : 9).ToArray();
    }

    private static string BoonArt(string id) => id switch
    {
        EndlessBoonCatalog.SurplusCourageId => "boon-courage",
        EndlessBoonCatalog.SalvageCacheId or EndlessBoonCatalog.SplitterBaneId => "boon-supplies",
        EndlessBoonCatalog.ReinforcedBusId or EndlessBoonCatalog.ShieldFormationId => "boon-wagon",
        EndlessBoonCatalog.RelicForgeId => "activity-bounties",
        _ => "activity-endless"
    };

    private void Refresh()
    {
        RoyalUiTools.Clear(_layer);
        var spec = Spec;
        var state = GameState.Instance;
        _layer.AddChild(spec.Label("title", "Endless Survival", 460));
        var close = RoyalButton.Over(spec.Rect("close"), "Close panel", Close, 6);
        close.SetGlyph(RoyalKit.Texture("icon-close-x"), new Rect2(spec.Rect("close.icon").Position - spec.Rect("close").Position, spec.Rect("close.icon").Size));
        _layer.AddChild(close);
        _layer.AddChild(spec.Label("route.label", "ROUTE", 120));
        _layer.AddChild(spec.Label("boon.label", "OPENING BOON", 220));

        // Route card.
        var routes = Routes;
        var index = Math.Max(0, Array.IndexOf(routes, _selectedRouteId));
        var art = spec.Rect("route.art");
        var picture = ResourceLoader.Exists($"res://assets/ui/royal/missions/{_selectedRouteId}.png") ? RoyalArt.Load($"res://assets/ui/royal/missions/{_selectedRouteId}.png") : null;
        _layer.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, ClipContents = true,
            Texture = picture, Position = art.Position, Size = art.Size, MouseFilter = MouseFilterEnum.Ignore });
        var plaque = spec.Rect("route.plaque");
        var name = spec.Label("route.name", RouteCatalog.Get(_selectedRouteId).Title.Replace("'", "’"), 360, new Color("060504"));
        name.ShadowOffset = Vector2.Zero;
        var plaqueWidth = Mathf.Max(plaque.Size.X, name.Position.X - plaque.Position.X + name.TextWidth(name.FontSize) + 40);
        _layer.AddChild(new Panel { Position = plaque.Position, Size = new Vector2(plaqueWidth, plaque.Size.Y), MouseFilter = MouseFilterEnum.Ignore }
            .With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("endless-plaque", 14, 8, 40, 8))));
        _layer.AddChild(name);
        _layer.AddChild(RoyalKit.Image("endless-banner", spec.Rect("route.banner")));
        void Route(int step) { _selectedRouteId = routes[(index + step + routes.Length) % routes.Length]; GameState.Instance.SetSelectedEndlessRoute(_selectedRouteId); Refresh(); }
        var previous = RoyalButton.Over(spec.Rect("route.prev").Grow(12), "Previous route", () => Route(-1), 6);
        previous.SetGlyph(RoyalKit.Texture("icon-chevron-left"), new Rect2(10, 10, spec.Rect("route.prev").Size.X + 4, spec.Rect("route.prev").Size.Y + 4));
        var next = RoyalButton.Over(spec.Rect("route.next").Grow(12), "Next route", () => Route(1), 6);
        next.SetGlyph(RoyalKit.Texture("icon-chevron-right"), new Rect2(10, 10, spec.Rect("route.next").Size.X + 4, spec.Rect("route.next").Size.Y + 4));
        _layer.AddChild(previous); _layer.AddChild(next);
        var dot = spec.Rect("route.dot.1");
        var dotsLeft = art.GetCenter().X - routes.Length * 9;
        for (var i = 0; i < routes.Length; i++)
            _layer.AddChild(RoyalKit.Image(i == index ? "route-dot-on" : "pip-off", new Rect2(dotsLeft + i * 18, dot.Position.Y, dot.Size.X, dot.Size.Y)));
        _layer.AddChild(new Control { Position = art.Position, Size = art.Size, TooltipText = RouteCatalog.Get(_selectedRouteId).EndlessSummary, MouseFilter = MouseFilterEnum.Pass });

        // Boons.
        var boons = OrderedBoons();
        var pages = (boons.Length + 2) / 3;
        _boonPage = Math.Clamp(_boonPage, 0, pages - 1);
        for (var i = 0; i < 3; i++)
        {
            var at = _boonPage * 3 + i;
            var key = $"boon.{i + 1}";
            var rect = spec.Rect(key);
            if (at >= boons.Length) continue;
            var boon = boons[at];
            var selected = boon.Id == _selectedBoonId;
            var card = RoyalButton.Over(rect, boon.Title, () => { _selectedBoonId = boon.Id; GameState.Instance.SetSelectedEndlessBoon(boon.Id); Refresh(); }, 6);
            card.SetStates(RoyalKit.Slice(selected ? "boon-card-selected" : "boon-card", 12), 6);
            card.TooltipText = boon.Summary;
            var origin = spec.Rect("boon.1").Position;
            var artRect = spec.Rect("boon.1.art");
            card.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, ClipContents = true,
                Texture = RoyalKit.Texture(BoonArt(boon.Id)), Position = artRect.Position - origin, Size = new Vector2(rect.Size.X - 10, artRect.Size.Y), MouseFilter = MouseFilterEnum.Ignore });
            var title = spec.Label("boon.1.name", boon.Title, rect.Size.X - 10);
            title.Position = new Vector2(5, title.Position.Y - origin.Y);
            title.Size = new Vector2(rect.Size.X - 10, title.Size.Y);
            card.AddChild(title);
            card.AddChild(RoyalKit.Image("boon-divider", new Rect2(rect.Size.X / 2 - 60, spec.Rect("boon.1.divider").Position.Y - origin.Y, 120, spec.Rect("boon.1.divider").Size.Y)));
            var description = RoyalText.Paragraph(boon.Summary, 15, new Color("e3ddcd"), 500, HorizontalAlignment.Center);
            description.Position = new Vector2(8, spec.Number("boon.1.desc.1", "baseline", 540) - origin.Y - 14);
            description.Size = new Vector2(rect.Size.X - 16, rect.Size.Y - description.Position.Y - 8);
            description.AddThemeConstantOverride("line_spacing", -3);
            RoyalText.FitLines(description, 3, 11);
            card.AddChild(description);
            _layer.AddChild(card);
        }
        if (pages > 1)
            for (var side = -1; side <= 1; side += 2)
            {
                var direction = side;
                var anchor = side < 0 ? spec.Rect("boon.1") : spec.Rect("boon.3");
                var rect = new Rect2(side < 0 ? anchor.Position.X - 13 : anchor.End.X - 13, anchor.GetCenter().Y - 18, 26, 36);
                var pager = RoyalButton.Over(rect, side < 0 ? "Previous boons" : "More boons", () => { _boonPage = (_boonPage + direction + pages) % pages; Refresh(); }, 6);
                pager.SetStates(RoyalKit.Slice("chevron-button", 8), 6);
                pager.SetGlyph(RoyalKit.Texture(side < 0 ? "icon-chevron-left" : "icon-chevron-right"), new Rect2(7, 8, 12, 20));
                _layer.AddChild(pager);
            }

        Button("fieldguide", "icon-open-book", "Field Guide", () => RealmUi.Details(this, "Endless field guide",
            $"{RouteCatalog.Get(_selectedRouteId).EndlessSummary}\n\nOpening boon: {EndlessBoonCatalog.Get(_selectedBoonId).Title}\n{EndlessBoonCatalog.Get(_selectedBoonId).Summary}\n\nBest wave {state.BestEndlessWave} · {state.EndlessRuns} runs"));
        Button("runhistory", "icon-scroll", "Run History", () => RealmUi.Details(this, "Run history", RunHistoryText()));
        Button("back", "icon-back-chevron", "Back", Close);

        // Warband and spells.
        _layer.AddChild(spec.Label("warband.title", "Your Warband", 300));
        Button("editwarband", "icon-people-gold", "Edit Warband", () => SceneRouter.Instance.GoToShop(0));
        var units = state.GetActiveDeckUnits().ToArray();
        var firstCard = spec.Rect("unit.1");
        for (var i = 0; i < 6; i++)
        {
            var rect = spec.Has($"unit.{i + 1}") ? spec.Rect($"unit.{i + 1}") : new Rect2(firstCard.Position + new Vector2(i * 103, 0), firstCard.Size);
            _layer.AddChild(new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Ignore }.With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("endless-unit-card", 12))));
            var tab = spec.Rect("unit.1.tab"); tab.Position += rect.Position - firstCard.Position;
            _layer.AddChild(RoyalKit.Image("endless-tab", tab));
            var number = spec.Label("unit.1.number", (i + 1).ToString(), 30);
            number.Position += rect.Position - firstCard.Position; number.ShadowOffset = Vector2.Zero;
            _layer.AddChild(number);
            if (i >= units.Length) { _layer.AddChild(RoyalKit.Image("empty-plus", new Rect2(rect.GetCenter() - new Vector2(16, 30), new Vector2(32, 32)), new Color(1, 1, 1, .6f))); continue; }
            var unit = units[i];
            var origin = firstCard.Position;
            Rect2 Part(string part) => new(spec.Rect("unit.1" + part).Position - origin + rect.Position, spec.Rect("unit.1" + part).Size);
            var figure = new UnitFigure { Position = Part(".figure").Position, Size = Part(".figure").Size };
            figure.SetUnit(unit);
            _layer.AddChild(figure);
            _layer.AddChild(RoyalKit.Image(UnitClassIcon(unit), Part(".class")));
            var level = spec.Label("unit.1.level", $"Lv {state.GetUnitLevel(unit.Id)}", 60);
            level.Position += rect.Position - origin;
            _layer.AddChild(level);
            var hotspot = RoyalButton.Over(rect, unit.DisplayName, () => ModelShowcase.Show(this, GameState.Instance.GetActiveDeckUnits().ToArray(), unit.Id), 6);
            hotspot.TooltipText = $"{unit.DisplayName} · Level {state.GetUnitLevel(unit.Id)} · {unit.Cost} courage";
            _layer.AddChild(hotspot);
        }
        _layer.AddChild(RoyalKit.Image("icon-flame-gold", spec.Rect("spells.icon")));
        _layer.AddChild(spec.Label("spells.label", "Spells", 160));
        var spells = state.GetActiveDeckSpells().ToArray();
        var firstSpell = spec.Rect("spell.1");
        for (var i = 0; i < 5; i++)
        {
            var rect = spec.Has($"spell.{i + 1}") ? spec.Rect($"spell.{i + 1}") : new Rect2(firstSpell.Position + new Vector2(i * 82, 0), firstSpell.Size);
            _layer.AddChild(new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Ignore }.With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("endless-spell-slot", 10, 10, 10, 18))));
            var gem = spec.Rect("spell.1.gem"); gem.Position += rect.Position - firstSpell.Position;
            _layer.AddChild(RoyalKit.Image("endless-gem", gem));
            if (i >= spells.Length) continue;
            var spell = spells[i];
            var slot = RoyalButton.Over(rect, spell.DisplayName, () => SpellShowcase.Show(this, spell), 6);
            slot.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Texture = ResourceLoader.Exists($"res://assets/ui/royal/items/{spell.Id}.png") ? RoyalArt.Load($"res://assets/ui/royal/items/{spell.Id}.png") : UiArtLoader.TryLoadSpellIcon(spell),
                Position = new Vector2(7, 6), Size = rect.Size - new Vector2(14, 18), MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps });
            _layer.AddChild(slot);
        }

        // Begin.
        var begin = spec.Rect("button.begin");
        var canStart = state.CanStartBattle(out var reason);
        var march = RoyalButton.Over(begin, "Begin endless march", StartRun, 8);
        march.Disabled = !canStart;
        march.TooltipText = canStart ? "Begin endless march" : reason;
        march.SetGlyph(RoyalKit.Texture("icon-swords-dark"), new Rect2(spec.Rect("button.begin.icon").Position - begin.Position, spec.Rect("button.begin.icon").Size));
        var label = spec.Label("button.begin.label", "Begin Endless March", 330);
        label.Ink = new Color("080603"); label.ShadowInk = new Color(1, .95f, .8f, .3f);
        label.Position -= begin.Position;
        march.SetCaption(label, new Rect2(label.Position, label.Size));
        // Endless runs cost no food; the concept's cost slot shows the best wave reached instead.
        var food = spec.Rect("button.begin.food");
        var record = RoyalText.Caps(state.BestEndlessWave > 0 ? $"BEST {state.BestEndlessWave}" : "FREE", 19, new Color("060402"), 700);
        record.ShadowInk = new Color(1, .95f, .8f, .3f);
        record.Align = HorizontalAlignment.Center;
        record.Position = new Vector2(food.Position.X - begin.Position.X - 4, 0); record.Size = new Vector2(begin.End.X - food.Position.X - 8, begin.Size.Y);
        march.AddChild(record);
        _layer.AddChild(march);
    }

    private void Button(string key, string icon, string text, Action action)
    {
        var spec = Spec;
        var rect = spec.Rect($"button.{key}");
        var button = RoyalButton.Over(rect, text, action, 6);
        button.SetGlyph(RoyalKit.Texture(icon), new Rect2(spec.Rect($"button.{key}.icon").Position - rect.Position, spec.Rect($"button.{key}.icon").Size));
        var label = spec.Label($"button.{key}.label", text, rect.Size.X);
        label.Position -= rect.Position;
        button.SetCaption(label, new Rect2(label.Position, label.Size));
        _layer.AddChild(button);
    }

    private static string UnitClassIcon(UnitDefinition unit) => unit.Id switch
    {
        "player_shooter" or "player_ranger" or "player_marksman" or "player_ballista" => "class-ranged",
        "player_defender" or "player_lantern_guard" or "player_banner" => "class-shield",
        _ => "class-melee"
    };

    private static string RunHistoryText()
    {
        var history = GameState.Instance.GetEndlessRunHistory();
        if (history.Count == 0) return "No endless runs yet.";
        var best = GameState.Instance.BestEndlessWave;
        return string.Join("\n", history.Take(10).Select((run, i) =>
        {
            var routeName = GameData.GetLatestStageForMap(run.RouteId).MapName;
            var marker = run.Wave >= best && best > 0 ? " · best" : "";
            return $"#{i + 1}  Wave {run.Wave} · {(int)(run.TimeSeconds / 60f)}:{(int)(run.TimeSeconds % 60f):D2} · {routeName} · +{run.GoldEarned} gold · {DifficultyCatalog.GetById(run.DifficultyId).Title}{marker}";
        }));
    }

    private void StartRun()
    {
        if (!GameState.Instance.CanStartBattle(out var reason)) { RoyalToast.Show(this, reason); return; }
        GameState.Instance.PrepareEndlessBattle(_selectedRouteId);
        SceneRouter.Instance.GoToBattle();
    }
}
