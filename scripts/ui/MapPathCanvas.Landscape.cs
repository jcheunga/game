using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class MapPathCanvas
{
    private sealed record LandscapeProp(AdventureTile Tile, Vector2 Point, int Kind, float Size, uint Seed);
    private readonly List<LandscapeProp> _landscapeProps = new();
    private readonly List<(AdventureTile Tile, Vector2 Point)> _landscapeBridges = new();
    private Vector2[][] _landscapeFrontier = Array.Empty<Vector2[]>();
    private int _landscapeFrontierRevision = -1;
    private void UpdateLandscapeFrontier()
    {
        if (_landscapeFrontierRevision == GameState.Instance.AdventureKnowledgeRevision) return;
        var open = _tiles.Where(GameState.Instance.IsAdventureTileOpen).ToArray();
        var segments = new List<Vector2[]>();
        foreach (var tile in open)
        {
            var outline = AdventureAtlasLandscape.Outline(tile);
            for (var i = 0; i < outline.Length; i++)
            {
                var a = outline[i]; var b = outline[(i + 1) % outline.Length];
                var middle = (a + b) * .5f; var direction = (b - a).Normalized();
                var normal = new Vector2(-direction.Y, direction.X) * 6;
                var left = open.Any(region => AdventureAtlasLandscape.Contains(region, middle + normal));
                var right = open.Any(region => AdventureAtlasLandscape.Contains(region, middle - normal));
                if (left != right) segments.Add(new[] { a, b });
            }
        }
        _landscapeFrontier = segments.ToArray();
        _landscapeFrontierRevision = GameState.Instance.AdventureKnowledgeRevision;
    }
    private Color ForestColor() => new(ActiveMapId switch {
        "harbor" => "486958", "foundry" => "524c3b", "quarantine" => "506344", "thornwall" => "3e665b",
        "basilica" => "626d45", "mire" => "355343", "steppe" => "7d8043", "gloamwood" => "2f5147", "citadel" => "485447", _ => "44683a" });
    private Color RiverColor() => new(ActiveMapId switch {
        "harbor" => "477b86", "foundry" => "9c5836", "quarantine" => "4b7160", "thornwall" => "6898a3",
        "mire" => "406a5f", "steppe" => "6d8b80", "gloamwood" => "466b74", "citadel" => "526d82", _ => "548c91" });
    private Color RiverBankColor() => new(ActiveMapId switch {
        "foundry" => "645344", "thornwall" => "c8c6b3", "mire" or "gloamwood" => "6a7960", "steppe" => "baac83", _ => "bab18a" });
    private void BuildLandscapeScenery()
    {
        _landscapeProps.Clear(); _landscapeBridges.Clear(); _landscapeFrontierRevision = -1;
        var seed = AdventureTerrain.Seed(ActiveMapId);
        var river = AdventureAtlasLandscape.River(ActiveMapId);
        var road = AdventureAtlasLandscape.RoadPath(ActiveMapId);
        foreach (var tile in _tiles)
        {
            var outline = AdventureAtlasLandscape.Outline(tile);
            var bounds = new Rect2(outline[0], Vector2.Zero);
            foreach (var point in outline) bounds = bounds.Expand(point);
            var tileSeed = AdventureTerrain.Hash(seed, tile.Column + tile.Row * 9);
            for (var i = 0; i < 62; i++)
            {
                var hash = AdventureTerrain.Hash(tileSeed, i + 1100);
                var p = bounds.Position + new Vector2(hash % 1009 / 1009f * bounds.Size.X, (hash >> 12) % 1009 / 1009f * bounds.Size.Y);
                if (!AdventureAtlasLandscape.Contains(tile, p) || AdventureAtlasLandscape.DistanceToLine(p, river) < AdventureAtlasLandscape.WaterWidth(ActiveMapId) * .5f + 13) continue;
                if (_tiles.Any(site => Math.Abs(site.Point.X - p.X) < 56 && p.Y > site.Point.Y - 24 && p.Y < site.Point.Y + 90)) continue;
                var roadDistance = AdventureAtlasLandscape.DistanceToLine(p, road);
                var forest = AdventureAtlasLandscape.ForestDensity(ActiveMapId, p);
                var roll = (hash >> 20) % 100 / 100f;
                var ridge = tile.Row == 0 || tile.Column == 8 || tile.Row == 6 && tile.Column % 3 == 0;
                int kind;
                float size;
                if (i < 3 && (ridge || (ActiveMapId is "thornwall" or "citadel") && tileSeed % 4 == 0) && roadDistance > 36)
                { kind = 2; size = 70 + hash % (ActiveMapId == "thornwall" ? 90u : 55u); }
                else if (roll < forest && roadDistance > 20)
                { kind = 0; size = 38 + hash % 35; }
                else if (i % 11 == 0 && roadDistance > 25)
                { kind = 1; size = 10 + hash % 12; }
                else if (i % 17 == 0 && roadDistance < 80)
                { kind = 3; size = 1; }
                else { kind = 4; size = 5 + hash % 7; }
                _landscapeProps.Add(new(tile, p, kind, size, hash));
            }
            // Villages, outworks and farms give stage sites a medieval setting.
            if (tile.Site?.Kind == AdventureSiteKind.Leader || tile.Site?.Kind == AdventureSiteKind.Camp)
            {
                foreach (var offset in new[] { new Vector2(-89, 22), new Vector2(91, 26), new Vector2(-70, -33) })
                {
                    var p = tile.Point + offset;
                    if (AdventureAtlasLandscape.Contains(tile, p) && AdventureAtlasLandscape.DistanceToLine(p, river) > 40)
                        _landscapeProps.Add(new(tile, p, 5, 1, tileSeed));
                }
            }
        }
        foreach (var point in AdventureAtlasLandscape.Bridges(ActiveMapId))
        {
            var owner = _tiles.FirstOrDefault(tile => AdventureAtlasLandscape.Contains(tile, point));
            if (owner != null) _landscapeBridges.Add((owner, point));
        }
        _landscapeProps.Sort((a, b) => a.Point.Y.CompareTo(b.Point.Y));
    }
    private void DrawLandscapeBackground()
    {
        if (_zoneArtwork == null) return;
        var world = new Rect2(Vector2.Zero, AdventureTileCatalog.WorldSize);
        // The painted landscape provides quiet context around the playable coastline.
        DrawTextureRect(_zoneArtwork, world, false, new Color(.72f, .76f, .71f, 1));
        DrawRect(world, TileMistColor() with { A = .43f });
    }
    private void DrawAtlasTiles()
    {
        var state = GameState.Instance;
        var view = new Rect2((-MapOffset - new Vector2(200, 220)) / Zoom, (Size + new Vector2(400, 440)) / Zoom);
        var ground = TileGroundColor(); var mist = TileMistColor();
        var coast = AdventureAtlasLandscape.Coast(ActiveMapId);
        var shoreline = coast.Append(coast[0]).ToArray();
        DrawPolyline(shoreline.Select(p => p + new Vector2(0, 15)).ToArray(), new Color(0, 0, 0, .16f), 30, true);
        DrawPolyline(shoreline, mist.Lightened(.1f), 18, true);
        if (_zoneArtwork == null) DrawOcean(view, coast);
        foreach (var tile in _tiles)
        {
            var outline = AdventureAtlasLandscape.Outline(tile);
            if (!view.Intersects(PolygonBounds(outline))) continue;
            if (!state.IsAdventureTileOpen(tile)) continue;
            var hash = AdventureTerrain.Hash(AdventureTerrain.Seed(ActiveMapId), tile.Row * 9 + tile.Column);
            DrawColoredPolygon(outline, ground.Lightened((hash % 7 - 3f) * .01f));
            if (_zoneArtwork != null)
                DrawPolygon(outline, new[] { new Color(1, 1, 1, .29f) }, outline.Select(point => point / AdventureTileCatalog.WorldSize).ToArray(), _zoneArtwork);
            DrawRegionGeography(tile);
        }
        foreach (var bridge in _landscapeBridges)
            if (state.IsAdventureTileOpen(bridge.Tile) && view.HasPoint(bridge.Point)) DrawLandscapeBridge(bridge.Point);
        // Sort terrain props and buildings together so wooded hills have natural depth.
        var landmarks = _tiles.Where(tile => state.IsAdventureTileOpen(tile) && view.HasPoint(tile.Point)).OrderBy(tile => tile.Point.Y).ToArray();
        var siteIndex = 0;
        foreach (var prop in _landscapeProps)
        {
            while (siteIndex < landmarks.Length && landmarks[siteIndex].Point.Y + 14 < prop.Point.Y)
            {
                var tile = landmarks[siteIndex++]; DrawTileLandmark(tile, state.IsAdventureTileComplete(tile));
            }
            if (!view.HasPoint(prop.Point) || !state.IsAdventureTileOpen(prop.Tile)) continue;
            DrawLandscapeProp(prop);
        }
        while (siteIndex < landmarks.Length)
        { var tile = landmarks[siteIndex++]; DrawTileLandmark(tile, state.IsAdventureTileComplete(tile)); }
        // A fine ink line makes curved tile edges readable without overpowering the landscape.
        foreach (var tile in _tiles)
        {
            if (!state.IsAdventureTileOpen(tile)) continue;
            var outline = AdventureAtlasLandscape.Outline(tile);
            if (!view.Intersects(PolygonBounds(outline))) continue;
            DrawPolyline(outline.Append(outline[0]).ToArray(), ground.Darkened(.6f) with { A = .22f }, 1.1f / Zoom, true);
        }
        // Draw fog last so the backdrop and neighboring trees cannot show through hidden regions.
        foreach (var tile in _tiles)
        {
            if (state.IsAdventureTileOpen(tile)) continue;
            var outline = AdventureAtlasLandscape.Outline(tile);
            if (!view.Intersects(PolygonBounds(outline))) continue;
            DrawColoredPolygon(outline, mist.Lightened(.055f) with { A = 1 });
            var hash = AdventureTerrain.Hash(AdventureTerrain.Seed(ActiveMapId), tile.Row * 9 + tile.Column);
            for (var cloud = 0; cloud < 3; cloud++)
            {
                var drift = state.ReducedMotion ? 0 : Mathf.Sin(_time * .2f + hash % 17 + cloud) * 8;
                DrawLine(tile.Point + new Vector2(-45 + drift, cloud * 13 - 15), tile.Point + new Vector2(53 + drift, cloud * 13 - 15), new Color(.65f, .75f, .79f, .025f), 9, true);
            }
        }
        foreach (var frontier in _landscapeFrontier)
        {
            var edge = frontier;
            DrawPolyline(edge, mist with { A = .07f }, 36, true);
            DrawPolyline(edge, mist with { A = .12f }, 20, true);
            DrawPolyline(edge, mist with { A = .17f }, 9, true);
        }
        if (AdventureTileCatalog.Find(ActiveMapId, _selectedId) is { } selected && state.IsAdventureTileOpen(selected))
        {
            foreach (var outline in AdventureAtlasLandscape.Land(selected).Where(outline => Geometry2D.IsPointInPolygon(selected.Point, outline)))
                DrawPolyline(outline.Append(outline[0]).ToArray(), new Color("dac795aa"), 1.8f, true);
        }
    }
    private static Rect2 PolygonBounds(Vector2[] outline)
    {
        var bounds = new Rect2(outline[0], Vector2.Zero);
        foreach (var point in outline) bounds = bounds.Expand(point);
        return bounds;
    }
    private void DrawRegionGeography(AdventureTile tile)
    {
        var water = RiverColor(); var bank = RiverBankColor();
        foreach (var polygon in AdventureAtlasLandscape.Roads(tile))
        { DrawColoredPolygon(polygon, TileGroundColor().Darkened(.12f)); DrawPolyline(polygon.Append(polygon[0]).ToArray(), TileGroundColor().Lightened(.14f), 1.5f, true); }
        foreach (var polygon in AdventureAtlasLandscape.Banks(tile)) DrawColoredPolygon(polygon, bank);
        foreach (var polygon in AdventureAtlasLandscape.Water(tile))
        {
            DrawColoredPolygon(polygon, water.Darkened(.12f));
            DrawPolyline(polygon.Append(polygon[0]).ToArray(), water.Lightened(.23f), 1.4f, true);
        }
        var river = AdventureAtlasLandscape.River(ActiveMapId);
        for (var i = 3; i < river.Length - 3; i += 5)
        {
            var p = river[i];
            if (!AdventureAtlasLandscape.Contains(tile, p)) continue;
            var drift = GameState.Instance.ReducedMotion ? 0 : Mathf.Sin(_time * .8f + i) * 3;
            var direction = (river[i + 1] - river[i - 1]).Normalized();
            DrawLine(p - direction * 9 + new Vector2(drift, 0), p + direction * 10 + new Vector2(drift, 0), water.Lightened(.36f) with { A = .6f }, 1.3f, true);
            if (ActiveMapId == "foundry") DrawCircle(p + new Vector2(5, 2), 2, new Color("e5a75b99"));
        }
    }
    private void DrawOcean(Rect2 view, Vector2[] coast)
    {
        var seed = AdventureTerrain.Seed(ActiveMapId);
        for (var i = 0; i < 160; i++)
        {
            var hash = AdventureTerrain.Hash(seed, i + 8200);
            var p = new Vector2(hash % 3800 + 80, (hash >> 12) % 2020 + 80);
            if (!view.HasPoint(p) || Geometry2D.IsPointInPolygon(p, coast)) continue;
            var drift = GameState.Instance.ReducedMotion ? 0 : Mathf.Sin(_time * .3f + i) * 4;
            DrawLine(p + new Vector2(-12 + drift, 0), p + new Vector2(14 + drift, 0), new Color(.6f, .75f, .79f, .07f), 1.5f, true);
            DrawLine(p + new Vector2(-4 + drift, 5), p + new Vector2(20 + drift, 5), new Color(.6f, .75f, .79f, .035f), 1, true);
        }
    }
    private void DrawLandscapeProp(LandscapeProp prop)
    {
        var p = prop.Point; var hash = prop.Seed; var forest = ForestColor();
        switch (prop.Kind)
        {
            case 0:
                if (ActiveMapId is "foundry" or "quarantine" or "citadel" && hash % 3 == 0) DrawDeadTree(p, prop.Size);
                else if (ActiveMapId is "thornwall" or "gloamwood" || hash % 4 == 0) DrawPine(p, forest, prop.Size, ActiveMapId == "thornwall");
                else DrawBroadleafTree(p, forest, prop.Size, hash);
                break;
            case 1: DrawBoulder(p, prop.Size, hash); break;
            case 2: DrawMountain(p, prop.Size, hash); break;
            case 3: DrawZoneDetail(p, hash); break;
            case 5: DrawHamlet(p, hash); break;
            default:
                for (var i = 0; i < 3; i++) DrawLine(p + new Vector2(i * 3, 0), p + new Vector2(i * 3 - 2, -prop.Size + i * 2), forest.Lightened(.12f), 1, true);
                if (hash % 5 == 0) DrawCircle(p - new Vector2(0, 5), 1.5f, new Color(ActiveMapId == "gloamwood" ? "aa9cba" : "c4b67d"));
                break;
        }
    }
    private void DrawBroadleafTree(Vector2 p, Color color, float height, uint hash)
    {
        DrawEllipse(p + new Vector2(8, 5), new Vector2(height * .32f, height * .12f), new Color(0, 0, 0, .18f));
        DrawLine(p, p - new Vector2(0, height * .7f), new Color("655741"), 4, true);
        DrawLine(p - new Vector2(0, height * .42f), p + new Vector2(-height * .14f, -height * .72f), new Color("716047"), 2, true);
        var crown = p - new Vector2(0, height * .66f);
        for (var layer = 0; layer < 4; layer++)
        {
            var center = crown + new Vector2(layer % 2 == 0 ? -height * .12f : height * .14f, (layer / 2 - 1) * height * .16f);
            var points = Enumerable.Range(0, 12).Select(i => {
                var angle = i / 12f * Mathf.Tau;
                var radius = height * (.22f + (AdventureTerrain.Hash(hash, i + layer * 23) % 10) * .006f);
                return center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .78f);
            }).ToArray();
            DrawColoredPolygon(points, color.Lightened(layer * .045f));
            DrawPolyline(points.Take(6).ToArray(), color.Lightened(.24f), 1, true);
        }
    }
    private void DrawPine(Vector2 p, Color color, float height, bool snowy)
    {
        DrawEllipse(p + new Vector2(6, 4), new Vector2(height * .25f, height * .08f), new Color(0, 0, 0, .18f));
        DrawTree(p, color, height, 0);
        if (snowy)
            for (var tier = 0; tier < 3; tier++)
            {
                var y = -height + tier * height * .22f; var w = height * (.18f + tier * .065f);
                DrawColoredPolygon(new[] { p + new Vector2(0, y), p + new Vector2(w, y + height * .24f), p + new Vector2(-w, y + height * .24f) }, new Color("d3d6c1"));
            }
    }
    private void DrawDeadTree(Vector2 p, float height)
    {
        var wood = new Color("6c6250");
        DrawLine(p, p - new Vector2(3, height), wood, 4, true);
        for (var i = 0; i < 3; i++)
        {
            var from = p - new Vector2(1, height * (.3f + i * .2f));
            var to = from + new Vector2((i % 2 == 0 ? -1 : 1) * height * .28f, -height * .22f);
            DrawLine(from, to, wood.Lightened(.1f), 2.5f, true);
            DrawLine(to, to - new Vector2(0, height * .14f), wood, 1.5f, true);
        }
    }
    private void DrawEllipse(Vector2 p, Vector2 radius, Color color) => DrawColoredPolygon(Enumerable.Range(0, 18)
        .Select(i => p + new Vector2(Mathf.Cos(i / 18f * Mathf.Tau) * radius.X, Mathf.Sin(i / 18f * Mathf.Tau) * radius.Y)).ToArray(), color);
    private void DrawBoulder(Vector2 p, float size, uint hash)
    {
        var stone = new Color(ActiveMapId == "foundry" ? "776b59" : ActiveMapId == "thornwall" ? "9fa799" : "8a8e76");
        var shape = new[] { p + new Vector2(-size, 0), p + new Vector2(-size * .6f, -size), p + new Vector2(size * .25f, -size * 1.3f), p + new Vector2(size, -size * .4f), p + new Vector2(size * .9f, size * .2f), p + new Vector2(0, size * .4f) };
        DrawColoredPolygon(shape, stone);
        DrawColoredPolygon(new[] { shape[2], shape[3], shape[4], shape[5] }, stone.Darkened(.23f));
        DrawLine(shape[1], shape[2], stone.Lightened(.32f), 1.3f, true);
    }
    private void DrawMountain(Vector2 p, float height, uint hash)
    {
        var stone = new Color(ActiveMapId switch { "foundry" => "756454", "thornwall" => "81918d", "gloamwood" => "67746c", "citadel" => "727884", _ => "838772" });
        var w = height * .72f; var peak = p + new Vector2(-height * .08f, -height);
        DrawEllipse(p + new Vector2(12, 8), new Vector2(w, height * .18f), new Color(0, 0, 0, .16f));
        var left = p + new Vector2(-w, 1); var shoulder = p + new Vector2(-w * .42f, -height * .56f);
        var right = p + new Vector2(w, 5); var split = p + new Vector2(w * .05f, height * .14f);
        DrawColoredPolygon(new[] { left, shoulder, peak, right, split }, stone);
        DrawColoredPolygon(new[] { peak, p + new Vector2(w * .38f, -height * .48f), right, split }, stone.Darkened(.25f));
        DrawColoredPolygon(new[] { peak, shoulder, p + new Vector2(-w * .13f, -height * .38f), split }, stone.Lightened(.14f));
        DrawLine(shoulder, peak, stone.Lightened(.34f), 1.5f, true);
        DrawLine(peak, split, stone.Darkened(.33f), 1.4f, true);
        if (ActiveMapId == "thornwall" || ActiveMapId == "city" && height > 110)
            DrawColoredPolygon(new[] { peak, peak + new Vector2(-height * .18f, height * .34f), peak + new Vector2(-height * .03f, height * .25f), peak + new Vector2(height * .13f, height * .37f), peak + new Vector2(height * .22f, height * .31f) }, new Color("d3d5be"));
        if (ActiveMapId == "foundry")
        {
            DrawLine(peak + new Vector2(0, 17), split - new Vector2(0, 12), new Color("bd784b"), 2, true);
            DrawCircle(peak - new Vector2(0, 7), 8, new Color("93806c22"));
        }
    }
    private void DrawLandscapeBridge(Vector2 p)
    {
        var stone = new Color("a8a388");
        var direction = new Vector2(.86f, .5f); var side = new Vector2(-.5f, .86f) * 9;
        var length = AdventureAtlasLandscape.WaterWidth(ActiveMapId) * .6f + 17;
        var a = p - direction * length; var b = p + direction * length;
        DrawLine(a + new Vector2(0, 10), b + new Vector2(0, 10), new Color("4b574d"), 25, true);
        DrawColoredPolygon(new[] { a - side, a + side, b + side, b - side }, stone);
        DrawLine(a - side - new Vector2(0, 7), b - side - new Vector2(0, 7), stone.Lightened(.2f), 5, true);
        DrawLine(a + side - new Vector2(0, 6), b + side - new Vector2(0, 6), stone.Darkened(.25f), 5, true);
        for (var i = 0; i <= 7; i++)
        {
            var at = a.Lerp(b, i / 7f);
            DrawLine(at - side, at + side, stone.Darkened(.22f), 1, true);
        }
        DrawIsoBlock(a + side, 6, 4, 13, stone); DrawIsoBlock(b + side, 6, 4, 13, stone);
    }
    private void DrawHamlet(Vector2 p, uint hash)
    {
        if (ActiveMapId == "harbor") { DrawHarborDock(p); return; }
        if (ActiveMapId == "foundry") { DrawForgeOutwork(p); return; }
        var wood = new Color("aa9b76"); var roof = TileRoofColor();
        DrawIsoBlock(p, 22, 12, 25, wood);
        var top = p - new Vector2(0, 25);
        DrawColoredPolygon(new[] { top + new Vector2(-27, 0), top + new Vector2(0, -21), top + new Vector2(27, 0), top + new Vector2(0, 16) }, roof);
        DrawLine(top - new Vector2(27, 0), top - new Vector2(0, 21), roof.Lightened(.25f), 1.4f, true);
        DrawLine(p + new Vector2(4, 6), p + new Vector2(4, -12), new Color("4c4938"), 6, true);
        DrawIsoBlock(p + new Vector2(-8, -27), 4, 3, 13, new Color("8f8770"));
        DrawFence(p + new Vector2(-35, 14), p + new Vector2(31, 45));
    }
    private void DrawFence(Vector2 a, Vector2 b)
    {
        DrawLine(a - new Vector2(0, 8), b - new Vector2(0, 8), new Color("8a7854"), 2, true);
        DrawLine(a - new Vector2(0, 14), b - new Vector2(0, 14), new Color("b19a68"), 2, true);
        for (var i = 0; i <= 5; i++) { var p = a.Lerp(b, i / 5f); DrawLine(p, p - new Vector2(0, 20), new Color("655c43"), 2.3f, true); }
    }
    private void DrawZoneDetail(Vector2 p, uint hash)
    {
        switch (ActiveMapId)
        {
            case "city": if (hash % 2 == 0) DrawWindmill(p); else DrawFarm(p); break;
            case "harbor": DrawHarborDock(p); break;
            case "foundry": DrawForgeOutwork(p); break;
            case "quarantine": DrawGraveyard(p); break;
            case "basilica": DrawRuinedArch(p); break;
            case "mire": DrawReeds(p); break;
            case "steppe": DrawFarm(p); break;
            case "gloamwood": DrawRuinedArch(p); break;
            case "citadel": DrawIsoBlock(p, 32, 11, 29, new Color("8a8790")); DrawBanner(p - new Vector2(0, 30), new Color("755675")); break;
            default: DrawBoulder(p, 18, hash); break;
        }
    }
    private void DrawWindmill(Vector2 p)
    {
        DrawIsoBlock(p, 13, 9, 37, new Color("b0aa89"));
        var hub = p - new Vector2(0, 43);
        DrawColoredPolygon(new[] { hub + new Vector2(-18, 6), hub - new Vector2(0, 19), hub + new Vector2(18, 6) }, TileRoofColor());
        var rotation = GameState.Instance.ReducedMotion ? .25f : _time * .17f;
        for (var blade = 0; blade < 4; blade++)
        {
            var angle = rotation + blade * Mathf.Pi / 2; var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var side = new Vector2(-direction.Y, direction.X);
            DrawLine(hub, hub + direction * 29, new Color("706044"), 2, true);
            DrawColoredPolygon(new[] { hub + direction * 10, hub + direction * 29, hub + direction * 28 + side * 7, hub + direction * 10 + side * 4 }, new Color("d0bd91"));
        }
        DrawCircle(hub, 3, new Color("6c6045"));
    }
    private void DrawFarm(Vector2 p)
    {
        DrawColoredPolygon(new[] { p + new Vector2(-34, 0), p + new Vector2(0, -17), p + new Vector2(36, 0), p + new Vector2(0, 18) }, new Color("8e8051"));
        for (var i = -3; i <= 3; i++) DrawLine(p + new Vector2(i * 8 - 13, i * 4 - 7), p + new Vector2(i * 8 + 13, i * 4 + 7), new Color("c1ae70"), 2.5f, true);
        DrawFence(p + new Vector2(-37, 1), p + new Vector2(0, 20));
    }
    private void DrawHarborDock(Vector2 p)
    {
        DrawColoredPolygon(new[] { p + new Vector2(-31, 0), p + new Vector2(-4, -13), p + new Vector2(33, 6), p + new Vector2(7, 19) }, new Color("867559"));
        for (var i = 0; i < 5; i++) DrawLine(p + new Vector2(-25 + i * 10, i * 5), p + new Vector2(-1 + i * 10, -12 + i * 5), new Color("b39c73"), 2, true);
        DrawIsoBlock(p + new Vector2(-23, 0), 7, 4, 17, new Color("857354"));
        DrawIsoBlock(p + new Vector2(26, 10), 7, 4, 17, new Color("857354"));
        DrawLine(p, p - new Vector2(0, 40), new Color("8b7852"), 2, true);
        DrawColoredPolygon(new[] { p - new Vector2(0, 38), p + new Vector2(23, -18), p + new Vector2(0, -12) }, new Color("c8bea0"));
    }
    private void DrawForgeOutwork(Vector2 p)
    {
        DrawIsoBlock(p, 22, 13, 26, new Color("807260"));
        DrawIsoBlock(p + new Vector2(-10, -25), 5, 4, 32, new Color("5c5b51"));
        DrawLine(p + new Vector2(5, 1), p + new Vector2(5, -14), new Color("c78a49"), 8, true);
        DrawCircle(p + new Vector2(-10, -66), 9, new Color("b0a28a44"));
        DrawCircle(p + new Vector2(-5, -82), 13, new Color("b0a28a22"));
    }
    private void DrawRuinedArch(Vector2 p)
    {
        var stone = new Color("a6a68a");
        DrawIsoBlock(p + new Vector2(-16, 0), 7, 5, 34, stone);
        DrawIsoBlock(p + new Vector2(16, 0), 7, 5, 26, stone.Darkened(.1f));
        DrawIsoBlock(p + new Vector2(-8, -33), 16, 5, 8, stone.Lightened(.1f));
        DrawBoulder(p + new Vector2(20, 9), 10, 0);
        DrawLine(p + new Vector2(-19, -32), p + new Vector2(-19, -5), ForestColor(), 3, true);
    }
    private void DrawGraveyard(Vector2 p)
    {
        for (var i = 0; i < 4; i++)
        {
            var at = p + new Vector2((i % 2) * 20 - 10, (i / 2) * 13);
            DrawIsoBlock(at, 6, 4, 15, new Color("939480"));
            DrawLine(at - new Vector2(0, 16), at - new Vector2(0, 28), new Color("b3b399"), 3, true);
            DrawLine(at + new Vector2(-5, -23), at + new Vector2(5, -23), new Color("b3b399"), 2, true);
        }
    }
    private void DrawReeds(Vector2 p)
    {
        for (var i = 0; i < 7; i++)
        {
            var at = p + new Vector2(i * 4 - 12, i % 3 * 2);
            DrawLine(at, at + new Vector2((i % 2 == 0 ? -3 : 3), -18 - i % 3 * 5), new Color("849469"), 1.5f, true);
            DrawCircle(at + new Vector2(0, -21), 2, new Color("a19669"));
        }
    }
}
