using System;
using System.Linq;
using Godot;

/// <summary>
/// The war wagon workshop: the concept's two-plus-three upgrade cards around the painted wagon, paged so
/// every upgrade is reachable, and the selected upgrade's detail panel underneath. Like the other tabs it
/// sits inside the armory frame: the workshop scene is fitted into the body below the header.
/// </summary>
public partial class ShopMenu
{
    // Page one is the concept: plating and stores on the left; ballista, drum and beacon on the right.
    private static readonly string[][] WagonPages =
    {
        new[] { BaseUpgradeCatalog.HullPlatingId, BaseUpgradeCatalog.PantryId, BaseUpgradeCatalog.BallistaId, BaseUpgradeCatalog.DispatchConsoleId, BaseUpgradeCatalog.SignalRelayId },
        new[] { BaseUpgradeCatalog.ReinforcedArmorId, BaseUpgradeCatalog.ProjectileWardId, BaseUpgradeCatalog.ArcherCrewId, BaseUpgradeCatalog.FirepotId, BaseUpgradeCatalog.ArrowVolleyId },
        new[] { BaseUpgradeCatalog.EmergencyRepairId, BaseUpgradeCatalog.GateBreakerId, BaseUpgradeCatalog.RelicVaultId },
    };
    private string _selectedUpgrade = BaseUpgradeCatalog.BallistaId;
    private int _wagonPage;
    private Control _wagonStage;
    // The armory body inside its frame, and the part of the workshop concept shown there: everything from
    // the top of the ballista card to the foot of the detail panel (the concept's title and gold plaque
    // give way to the armory header).
    private static readonly Rect2 WagonBody = new(42, 127, 1197, 558);
    private static readonly Rect2 WagonWindow = new(0, 103, 1280, 597);

    private static string UpgradeName(BaseUpgradeDefinition upgrade) => upgrade.Id switch
    {
        BaseUpgradeCatalog.HullPlatingId => "Plating", BaseUpgradeCatalog.PantryId => "Stores", _ => upgrade.Title
    };

    private static Texture2D UpgradePicture(string id, bool wide = false)
    {
        foreach (var path in new[] { wide ? $"res://assets/ui/royal/kit/upgrade-wide-{id}.png" : "", $"res://assets/ui/royal/kit/upgrade-{id}.png", $"res://assets/ui/royal/items/upgrade_{id}.png" })
            if (path.Length > 0 && ResourceLoader.Exists(path)) return RoyalArt.Load(path);
        return RoyalKit.Texture("hammer");
    }

    private void BuildWagon()
    {
        var spec = RoyalSpec.For("wagon");
        var state = _state;
        var page = WagonPages[Mathf.Clamp(_wagonPage, 0, WagonPages.Length - 1)];
        if (!page.Contains(_selectedUpgrade)) _selectedUpgrade = page[Math.Min(2, page.Length - 1)];
        var clip = RoyalUiTools.Box(WagonBody, true);
        _body.AddChild(clip);
        var scale = WagonBody.Size / WagonWindow.Size;
        _wagonStage = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = RoyalArt.Canvas, Scale = scale, Position = -WagonWindow.Position * scale };
        clip.AddChild(_wagonStage);
        _wagonStage.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = RoyalArt.Plate("wagon"), Size = RoyalArt.Canvas, MouseFilter = MouseFilterEnum.Ignore });
        // The header frame casts a soft shadow over the scenery (beneath the cards).
        var shade = new Gradient { Colors = new[] { new Color(0, 0, 0, .62f), new Color(0, 0, 0, 0) }, Offsets = new[] { 0f, 1f } };
        _wagonStage.AddChild(new TextureRect { Texture = new GradientTexture2D { Gradient = shade, Width = 4, Height = 64, FillFrom = Vector2.Zero, FillTo = new Vector2(0, 1) },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore,
            Position = WagonWindow.Position, Size = new Vector2(WagonWindow.Size.X, 38) });
        BuildWagonGold(spec);

        for (var i = 0; i < 5; i++)
        {
            var key = $"card.{i + 1}";
            if (i >= page.Length) { _wagonStage.AddChild(EmptyCardVeil(spec.Rect(key))); continue; }
            var upgrade = BaseUpgradeCatalog.Get(page[i]);
            var level = state.GetBaseUpgradeLevel(upgrade.Id);
            var rect = spec.Rect(key);
            var selected = upgrade.Id == _selectedUpgrade;
            if (selected) _wagonStage.AddChild(RoyalKit.Frame("wagon-card-selected", rect.Grow(3), 14, false));
            var picture = spec.Rect(key + ".picture");
            _wagonStage.AddChild(Cover(UpgradePicture(upgrade.Id), picture.Grow(selected ? 2 : 0)));
            var name = spec.Label(key + ".name", UpgradeName(upgrade), spec.Rect(key + ".button").Position.X - spec.Number(key + ".name", "pen_x", rect.Position.X + 120) - 8);
            _wagonStage.AddChild(name);
            _wagonStage.AddChild(spec.Label(key + ".level", $"Level {level} / {upgrade.MaxLevel}", 150));
            for (var p = 1; p <= 5; p++)
                _wagonStage.AddChild(RoyalKit.Image(p <= level ? "level-square-on" : "level-square-off", spec.Rect($"{key}.pip.{p}")));
            var id = upgrade.Id;
            var hotspot = RoyalButton.Over(rect, upgrade.Title, () => { _selectedUpgrade = id; Refresh(); }, 6);
            _wagonStage.AddChild(hotspot);
        }
        if (_wagonPage == 0) DrawCallouts(spec);
        BuildWagonPager(spec);
        BuildWagonDetail(spec, BaseUpgradeCatalog.Get(_selectedUpgrade));
    }

    /// <summary>The balance sits in the plaque at the scene's lower left, beside the upgrade's price.</summary>
    private void BuildWagonGold(RoyalSpec spec)
    {
        var plaque = spec.Rect("button.back");
        var shift = plaque.Position - spec.Rect("gold").Position + new Vector2(-6, 0);
        var icon = spec.Rect("gold.icon");
        _wagonStage.AddChild(RoyalKit.Image("coin-pile", new Rect2(icon.Position + shift, icon.Size)));
        var x = spec.Number("gold.value", "pen_x", spec.Number("gold.value", "x", 1142)) + shift.X;
        var value = spec.Label("gold.value", _state.Gold.ToString("N0"), plaque.End.X - 10 - x);
        value.Position += shift;
        _wagonStage.AddChild(value);
    }

    private static Control EmptyCardVeil(Rect2 rect)
    {
        var veil = new Panel { Position = rect.Position + new Vector2(4, 4), Size = rect.Size - new Vector2(8, 8), MouseFilter = MouseFilterEnum.Ignore };
        veil.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(.02f, .03f, .04f, .72f), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
        return veil;
    }

    /// <summary>A picture filling its well (cropped to the well's shape).</summary>
    private static Control Cover(Texture2D texture, Rect2 rect)
    {
        return new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, ClipContents = true,
            Texture = texture, Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps };
    }

    private void DrawCallouts(RoyalSpec spec)
    {
        var lines = new CalloutLines();
        for (var i = 1; i <= 4; i++)
        {
            if (!spec.Has($"callout.{i}")) continue;
            var points = spec.Points($"callout.{i}");
            var marker = spec.Rect($"callout.{i}.marker");
            // The armory body starts just above the ballista, so its marker drops onto the weapon's stock.
            var drop = Mathf.Max(0, WagonWindow.Position.Y + 18 - marker.Position.Y);
            if (drop > 0)
            {
                points = points.Select(p => p.Y < marker.End.Y ? p + new Vector2(0, drop) : p).ToArray();
                marker.Position += new Vector2(0, drop);
            }
            lines.Lines.Add(points);
            lines.Markers.Add(marker);
        }
        lines.Position = Vector2.Zero; lines.Size = RoyalArt.Canvas;
        _wagonStage.AddChild(lines);
    }

    private void BuildWagonPager(RoyalSpec spec)
    {
        var wagon = spec.Rect("wagon");
        var y = wagon.End.Y - 30;
        for (var side = -1; side <= 1; side += 2)
        {
            var direction = side;
            var rect = new Rect2(wagon.GetCenter().X + side * 70 - 19, y - 19, 38, 38);
            var button = RoyalButton.Over(rect, side < 0 ? "Previous upgrades" : "More upgrades", () =>
            {
                _wagonPage = (_wagonPage + direction + WagonPages.Length) % WagonPages.Length;
                _selectedUpgrade = "";
                Refresh();
            }, 6);
            button.SetStates(RoyalKit.Slice("chevron-button", 8), 6);
            button.SetGlyph(RoyalKit.Texture("chevron"), new Rect2(12, 9, 14, 20));
            if (side < 0) { button.Glyph.FlipH = true; }
            _wagonStage.AddChild(button);
        }
        var dots = new Control { Position = new Vector2(wagon.GetCenter().X - 24, y - 4), Size = new Vector2(48, 8), MouseFilter = MouseFilterEnum.Ignore };
        for (var i = 0; i < WagonPages.Length; i++)
            dots.AddChild(RoyalKit.Image(i == _wagonPage ? "pip-on" : "pip-off", new Rect2(4 + i * 15, -2, 11, 11)));
        _wagonStage.AddChild(dots);
    }

    private void BuildWagonDetail(RoyalSpec spec, BaseUpgradeDefinition upgrade)
    {
        var state = _state;
        var level = state.GetBaseUpgradeLevel(upgrade.Id);
        var max = level >= upgrade.MaxLevel;
        var picture = spec.Rect("detail.picture");
        _wagonStage.AddChild(Cover(UpgradePicture(upgrade.Id, true), picture));
        _wagonStage.AddChild(spec.Label("detail.title", upgrade.Title, 460));
        var description = new[] { upgrade.Summary, level == 0 ? "Not installed yet." : "Now: " + BuildBaseUpgradeEffectText(upgrade, level) };
        var body = RoyalText.Paragraph(description[0], (int)spec.Number("detail.desc.1", "size", 17), new Color("e6dccb"), 400);
        var top = spec.Number("detail.desc.1", "baseline", 568) - 16;
        body.Position = new Vector2(spec.Number("detail.desc.1", "x", 604), top);
        body.Size = new Vector2(spec.Rect("button.upgrade").End.X - body.Position.X, 44);
        body.AddThemeConstantOverride("line_spacing", 0);
        RoyalText.FitLines(body, 2);
        _wagonStage.AddChild(body);
        _wagonStage.AddChild(spec.Label("detail.level", $"LEVEL {level} / {upgrade.MaxLevel}", 200));
        for (var p = 1; p <= 5; p++)
            _wagonStage.AddChild(RoyalKit.Image(p <= level ? "level-bar-on-full" : "level-bar-off", spec.Rect($"detail.bar.{p}")));
        var cost = state.GetBaseUpgradeCost(upgrade.Id);
        var rect = spec.Rect("button.upgrade");
        var button = RoyalButton.Over(rect, max ? "Fully trained" : "Upgrade", () =>
        {
            if (state.TryUpgradeBase(upgrade.Id, out var message)) AudioDirector.Instance?.PlayUpgradeConfirm();
            Toast(message); Refresh();
        }, 6);
        button.Disabled = max || state.Gold < cost;
        _wagonStage.AddChild(button);
        var verb = spec.Label("button.upgrade.label", max ? "TRAINED" : "UPGRADE", 120);
        verb.Position -= rect.Position;
        button.SetCaption(verb, new Rect2(verb.Position, verb.Size));
        if (!max)
        {
            button.AddChild(RoyalKit.Image("coin-pile", new Rect2(spec.Rect("button.upgrade.icon").Position - rect.Position, spec.Rect("button.upgrade.icon").Size)));
            var price = spec.Label("button.upgrade.cost", cost.ToString("N0"), 60);
            price.Position -= rect.Position;
            button.AddChild(price);
        }
        button.TooltipText = $"{upgrade.Title} · {(max ? "fully trained" : $"next: {BuildBaseUpgradeEffectText(upgrade, level + 1)}")}";
    }
}

/// <summary>Thin gold leader lines from the upgrade cards to the parts of the wagon they improve.</summary>
public partial class CalloutLines : Control
{
    public readonly System.Collections.Generic.List<Vector2[]> Lines = new();
    public readonly System.Collections.Generic.List<Rect2> Markers = new();
    public CalloutLines() { MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Draw()
    {
        var gold = new Color("d9b56b");
        foreach (var line in Lines) DrawPolyline(line, gold, 1.6f, true);
        foreach (var marker in Markers)
        {
            var centre = marker.GetCenter(); var radius = marker.Size.X / 2;
            DrawCircle(centre, radius, new Color(.08f, .06f, .04f, .55f));
            DrawArc(centre, radius - 1, 0, Mathf.Tau, 32, gold, 2, true);
            DrawCircle(centre, radius * .35f, new Color("f3d58e"));
        }
    }
}
