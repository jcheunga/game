using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Deterministic geography for each zone's atlas: the coast, the river and its bridges, the roads between
/// stages and every tile's curved region, from which tile adjacency follows. The painted map is made over
/// this geography (MapGuideRenderer), so it never depends on saved progress.
/// </summary>
public static class AdventureAtlasLandscape
{
    private sealed record Region(Vector2[] Outline, Vector2[][] Land);
    private sealed record Geography(Dictionary<string, Region> Regions, Dictionary<string, AdventureTile[]> Neighbors,
        Vector2[] Coast, Vector2[] River, Vector2[] Bridges, Vector2[][] Roads);
    private static readonly Dictionary<string, Geography> Cache = new();
    private static readonly Dictionary<string, Vector2[]> RiverLines = new();
    private static readonly Dictionary<string, Vector2[]> CoastLines = new();
    private static readonly Dictionary<(string Map, int Column, int Row), Vector2> SitePoints = new();
    private static readonly Dictionary<string, (Vector2[][] Regions, int[][] Neighbors)> CellCache = new();
    private const int Columns = AdventureTileCatalog.Columns, Rows = AdventureTileCatalog.Rows;
    public static float Phase(string map) => AdventureTerrain.Seed(map) % 1000 / 137f;
    public static Vector2 Project(string map, float column, float row)
    {
        var phase = Phase(map);
        // Broad bends vary the entire region rather than merely jittering individual corners.
        var c = column + .62f * Mathf.Sin(row * .8f + phase) + .24f * Mathf.Sin(column * .65f + row * .45f);
        var r = row + .50f * Mathf.Sin(column * .7f + phase * .7f) + .18f * Mathf.Cos(row * 1.1f);
        return AdventureTileCatalog.Origin + new Vector2((c - r) * AdventureTileCatalog.HalfWidth, (c + r) * AdventureTileCatalog.HalfHeight);
    }
    /// <summary>Where the river runs across the map (as a fraction of its width) at a point down its length.</summary>
    public static float RiverAcross(string map, float down)
    {
        var phase = Phase(map);
        return .495f + .034f * Mathf.Sin(down * 6f + phase) + .012f * Mathf.Sin(down * 13f + phase * 1.7f) - .03f * (down - .5f);
    }
    private static Vector2 ProjectLayout(string map, float across, float down)
    {
        var grid = AdventureTileCatalog.GridOf(across, down);
        return Project(map, grid.X, grid.Y);
    }
    public static Vector2 SitePoint(string map, int column, int row)
    {
        if (SitePoints.TryGetValue((map, column, row), out var cached)) return cached;
        var hash = AdventureTerrain.Hash(AdventureTerrain.Seed(map), row * Columns + column + 370);
        var c = column + ((int)(hash % 101) - 50) / 220f;
        var r = row + ((int)((hash >> 8) % 101) - 50) / 310f;
        // Landmarks sit on the bank, never in the water itself: step across the map away from the river.
        var layout = AdventureTileCatalog.LayoutOf(c, r);
        var river = RiverAcross(map, layout.Y);
        const float Bank = .6f / (Columns + Rows);
        if (Math.Abs(layout.X - river) < Bank) layout.X = river + (layout.X < river ? -Bank : Bank);
        var side = layout.X < river ? -1 : 1;
        var point = ProjectLayout(map, layout.X, layout.Y);
        for (var attempt = 0; attempt < 12 && DistanceToLine(point, RiverSkeleton(map)) < WaterWidth(map) * .5f + 22; attempt++)
        {
            layout.X += side * .08f / (Columns + Rows);
            point = ProjectLayout(map, layout.X, layout.Y);
        }
        var coast = CoastSkeleton(map);
        for (var attempt = 0; attempt < 24 && !Geometry2D.IsPointInPolygon(point, coast); attempt++)
        {
            var grid = AdventureTileCatalog.GridOf(layout.X, layout.Y);
            grid = grid.Lerp(new Vector2(Columns / 2f, Rows / 2f), .06f);
            layout = AdventureTileCatalog.LayoutOf(grid.X, grid.Y);
            point = ProjectLayout(map, layout.X, layout.Y);
            if (DistanceToLine(point, RiverSkeleton(map)) < WaterWidth(map) * .5f + 22)
            {
                layout.X += side * .12f / (Columns + Rows); point = ProjectLayout(map, layout.X, layout.Y);
            }
        }
        return SitePoints[(map, column, row)] = point;
    }
    private static Vector2[] CoastSkeleton(string map)
    {
        if (CoastLines.TryGetValue(map, out var coast)) return coast;
        return CoastLines[map] = BuildCoast(map);
    }
    private static Vector2[] RiverSkeleton(string map)
    {
        if (RiverLines.TryGetValue(map, out var line)) return line;
        return RiverLines[map] = Enumerable.Range(0, 140).Select(i => {
            var down = -.08f + i / 139f * 1.16f;
            return ProjectLayout(map, RiverAcross(map, down), down);
        }).ToArray();
    }
    public static Vector2[] Outline(AdventureTile tile) => ForMap(tile.MapId).Regions[tile.Id].Outline;
    public static Vector2[][] Land(AdventureTile tile) => ForMap(tile.MapId).Regions[tile.Id].Land;
    public static IReadOnlyList<AdventureTile> Neighbors(AdventureTile tile) => ForMap(tile.MapId).Neighbors[tile.Id];
    public static Vector2[] Coast(string map) => ForMap(map).Coast;
    public static Vector2[] River(string map) => ForMap(map).River;
    public static Vector2[][] Roads(string map) => ForMap(map).Roads;
    public static Vector2[] Bridges(string map) => ForMap(map).Bridges;
    public static bool Contains(AdventureTile tile, Vector2 point) => Geometry2D.IsPointInPolygon(point, Outline(tile));
    public static bool IsWater(string map, Vector2 point) => DistanceToLine(point, RiverSkeleton(map)) < WaterWidth(map) * .5f;
    public static float WaterWidth(string map) => map switch { "harbor" => 48, "mire" => 42, "foundry" => 27, "steppe" => 23, _ => 34 };
    public static float DistanceToLine(Vector2 point, Vector2[] line)
    {
        var distance = float.MaxValue;
        for (var i = 1; i < line.Length; i++)
        {
            var a = line[i - 1]; var delta = line[i] - a;
            var t = delta.LengthSquared() < .0001f ? 0 : Mathf.Clamp((point - a).Dot(delta) / delta.LengthSquared(), 0, 1);
            distance = Math.Min(distance, point.DistanceTo(a + delta * t));
        }
        return distance;
    }
    public static float ForestDensity(string map, Vector2 point)
    {
        var phase = Phase(map);
        var clusters = .5f + .25f * Mathf.Sin(point.X * .006f + phase) + .25f * Mathf.Cos(point.Y * .011f - phase);
        var baseline = map switch { "gloamwood" => .78f, "mire" => .63f, "city" => .55f, "thornwall" => .5f,
            "quarantine" => .38f, "harbor" => .35f, "steppe" => .28f, "basilica" => .3f, _ => .22f };
        return Mathf.Clamp(baseline + (clusters - .5f) * .55f, .12f, .94f);
    }
    /// <summary>
    /// Each grid cell's straight-edged region (the cells nearest its site point) and the cells sharing an edge with
    /// it. These depend only on the site points, so tile placement can respect adjacency before any tile exists.
    /// </summary>
    public static (Vector2[][] Regions, int[][] Neighbors) Cells(string map)
    {
        map = RouteCatalog.Normalize(map);
        if (CellCache.TryGetValue(map, out var cached)) return cached;
        var points = Enumerable.Range(0, Columns * Rows).Select(cell => SitePoint(map, cell % Columns, cell / Columns)).ToArray();
        var minimum = new Vector2(-200, -200);
        var maximum = AdventureTileCatalog.WorldSize + new Vector2(200, 200);
        var regions = new Vector2[points.Length][];
        var neighbors = Enumerable.Range(0, points.Length).Select(_ => new List<int>()).ToArray();
        for (var cell = 0; cell < points.Length; cell++)
        {
            var bounds = new[] { minimum, new Vector2(maximum.X, minimum.Y), maximum, new Vector2(minimum.X, maximum.Y) };
            // A region is bounded only by nearby sites, so look at the surrounding grid cells.
            var nearby = Enumerable.Range(0, points.Length).Where(other => other != cell
                && Math.Max(Math.Abs(other % Columns - cell % Columns), Math.Abs(other / Columns - cell / Columns)) <= 3).ToArray();
            foreach (var other in nearby)
            {
                bounds = ClipHalfPlane(bounds, (points[cell] + points[other]) * .5f, points[other] - points[cell]);
                if (bounds.Length < 3) break;
            }
            regions[cell] = bounds;
            // Regions share an edge when two of their corners lie on the bisector between them.
            foreach (var other in nearby)
            {
                var middle = (points[cell] + points[other]) * .5f; var normal = (points[other] - points[cell]).Normalized();
                if (bounds.Count(corner => Math.Abs((corner - middle).Dot(normal)) < .75f) >= 2) neighbors[cell].Add(other);
            }
        }
        // Keep adjacency symmetric even where rounding saw an edge from one side only.
        for (var cell = 0; cell < points.Length; cell++)
            foreach (var other in neighbors[cell].ToArray())
                if (!neighbors[other].Contains(cell)) neighbors[other].Add(cell);
        return CellCache[map] = (regions, neighbors.Select(list => list.ToArray()).ToArray());
    }
    /// <summary>The painted map's world rectangle: the coast with a margin of sea, at the painting's 16:9.</summary>
    public static Rect2 PaintingRect(string map)
    {
        var coast = Coast(map);
        var bounds = new Rect2(coast[0], Vector2.Zero);
        foreach (var point in coast) bounds = bounds.Expand(point);
        bounds = bounds.Grow(110);
        var height = Math.Max(bounds.Size.Y, bounds.Size.X * 9 / 16); var width = height * 16 / 9;
        return new Rect2(bounds.GetCenter() - new Vector2(width, height) / 2, new Vector2(width, height));
    }
    /// <summary>The painting is made in four overlapping 16:9 sections (north-west, north-east, south-west, south-east).</summary>
    public static Rect2[] PaintingSections(string map)
    {
        var rect = PaintingRect(map); var size = rect.Size * .57f;
        return new[] { new Rect2(rect.Position, size), new Rect2(new Vector2(rect.End.X - size.X, rect.Position.Y), size),
            new Rect2(new Vector2(rect.Position.X, rect.End.Y - size.Y), size), new Rect2(rect.End - size, size) };
    }
    /// <summary>The roads between stages, from the foot of one castle to the next, bending gently on the way.</summary>
    public static Vector2[][] BuildRoads(string map, Vector2[] stages) => AdventureTileCatalog.Roads
        .Where(edge => edge.To < stages.Length)
        .Select(edge =>
        {
            var a = stages[edge.From] + new Vector2(0, 31); var b = stages[edge.To] + new Vector2(0, 31);
            var delta = b - a; var normal = new Vector2(-delta.Y, delta.X).Normalized();
            var hash = AdventureTerrain.Hash(AdventureTerrain.Seed(map), 4000 + edge.From * 16 + edge.To);
            var reach = Math.Min(80, delta.Length() * .08f);
            var first = ((int)(hash % 101) - 50) / 50f * reach; var second = ((int)((hash >> 8) % 101) - 50) / 50f * reach;
            return Smooth(new[] { a, a + delta * .34f + normal * first, a + delta * .67f + normal * second, b }, 18);
        }).ToArray();
    private static Geography ForMap(string map)
    {
        map = RouteCatalog.Normalize(map);
        if (Cache.TryGetValue(map, out var geography)) return geography;
        var tiles = AdventureTileCatalog.ForMap(map);
        var coast = CoastSkeleton(map);
        var river = RiverSkeleton(map);
        var water = Ribbon(river, WaterWidth(map));
        var stagePoints = Enumerable.Range(0, GameData.GetStagesForMap(map).Count).Select(i => AdventureTileCatalog.Stage(map, i).Point).ToArray();
        var roads = BuildRoads(map, stagePoints);
        var regions = new Dictionary<string, Region>();
        var sitePoints = tiles.Select(tile => tile.Point).ToArray();
        var cells = Cells(map);
        foreach (var tile in tiles)
        {
            var bounds = cells.Regions[tile.Row * Columns + tile.Column];
            var clipped = Geometry2D.IntersectPolygons(bounds, coast);
            var main = clipped.FirstOrDefault(polygon => Geometry2D.IsPointInPolygon(tile.Point, polygon)) ?? bounds;
            var outline = CurveEdges(map, main, sitePoints);
            var land = Geometry2D.ClipPolygons(outline, water)
                .Where(polygon => polygon.Length >= 3 && Math.Abs(Area(polygon)) > 1 && Geometry2D.TriangulatePolygon(polygon).Length > 0).ToArray();
            regions[tile.Id] = new(outline, land);
        }
        var neighbors = tiles.ToDictionary(tile => tile.Id,
            tile => cells.Neighbors[tile.Row * Columns + tile.Column].Select(cell => tiles[cell]).ToArray());
        var bridges = new List<Vector2>();
        foreach (var road in roads)
            for (var i = 1; i < road.Length; i++)
                for (var j = 1; j < river.Length; j++)
                    if (Geometry2D.SegmentIntersectsSegment(road[i - 1], road[i], river[j - 1], river[j]) is { VariantType: Variant.Type.Vector2 } crossing)
                        bridges.Add(crossing.AsVector2());
        return Cache[map] = new(regions, neighbors, coast, river, bridges.ToArray(), roads);
    }
    private static float Area(Vector2[] polygon)
    {
        var area = 0f;
        for (var i = 0; i < polygon.Length; i++) area += polygon[i].Cross(polygon[(i + 1) % polygon.Length]);
        return area / 2;
    }
    private static Vector2[] ClipHalfPlane(Vector2[] polygon, Vector2 middle, Vector2 normal)
    {
        var result = new List<Vector2>();
        for (var i = 0; i < polygon.Length; i++)
        {
            var a = polygon[i]; var b = polygon[(i + 1) % polygon.Length];
            var da = (a - middle).Dot(normal); var db = (b - middle).Dot(normal);
            if (da <= .001f) result.Add(a);
            if ((da < 0) != (db < 0)) result.Add(a.Lerp(b, da / (da - db)));
        }
        return result.ToArray();
    }
    private static Vector2[] BuildCoast(string map)
    {
        // A wavering shoreline around the whole grid, with a few bays cut into its long sides.
        var points = new List<Vector2>(); var phase = Phase(map);
        var shift = AdventureTerrain.Hash(AdventureTerrain.Seed(map), 5100) % 100 / 100f - .5f;
        float Bay(float t, float from, float to, float depth) => t > from && t < to ? Mathf.Sin((t - from) / (to - from) * Mathf.Pi) * depth : 0;
        float Waver(float t, float speed, float offset) => .16f * Mathf.Sin(t * speed + offset) + .07f * Mathf.Sin(t * speed * 2.9f + offset * 1.7f);
        const float West = -.68f, North = -.76f, East = Columns - .32f, South = Rows - .26f;
        for (var i = 0; i <= 60; i++)
        {
            var c = West + i / 60f * (East - West);
            points.Add(Project(map, c, North + Waver(c, .8f, phase) + Bay(c, Columns * (.3f + shift * .1f), Columns * (.5f + shift * .1f), .8f)));
        }
        for (var i = 1; i <= 48; i++)
        {
            var r = North + i / 48f * (South - North);
            points.Add(Project(map, East + Waver(r, .95f, phase) - Bay(r, Rows * .5f, Rows * .8f, .9f), r));
        }
        for (var i = 1; i <= 60; i++)
        {
            var c = East - i / 60f * (East - West);
            points.Add(Project(map, c, South + Waver(c, .7f, phase * .8f) - Bay(c, Columns * (.17f - shift * .06f), Columns * (.33f - shift * .06f), .8f)));
        }
        for (var i = 1; i < 48; i++)
        {
            var r = South - i / 48f * (South - North);
            points.Add(Project(map, West + Waver(r, .9f, phase) + Bay(r, Rows * .2f, Rows * .5f, .7f), r));
        }
        return points.ToArray();
    }
    private static Vector2[] CurveEdges(string map, Vector2[] polygon, Vector2[] sitePoints)
    {
        var result = new List<Vector2>();
        Vector2 Snap(Vector2 p) => new(Mathf.Round(p.X * 10) / 10, Mathf.Round(p.Y * 10) / 10);
        foreach (var i in Enumerable.Range(0, polygon.Length))
        {
            var from = Snap(polygon[i]); var to = Snap(polygon[(i + 1) % polygon.Length]);
            var canonical = from.X < to.X || from.X == to.X && from.Y < to.Y;
            var a = canonical ? from : to; var b = canonical ? to : from;
            var seed = AdventureTerrain.Seed($"{map}:{Mathf.RoundToInt(a.X * 10)}:{Mathf.RoundToInt(a.Y * 10)}:{Mathf.RoundToInt(b.X * 10)}:{Mathf.RoundToInt(b.Y * 10)}");
            var delta = b - a; var normal = new Vector2(-delta.Y, delta.X).Normalized();
            var clearance = sitePoints.Min(point => DistanceToLine(point, new[] { a, b }));
            var bendLimit = Math.Min(Math.Min(28, delta.Length() * .16f), clearance * .8f);
            var bend = ((int)(seed % 101) - 50) / 50f * bendLimit;
            var control = (a + b) * .5f + normal * bend;
            var steps = Math.Clamp(Mathf.CeilToInt(delta.Length() / 20), 2, 16);
            for (var step = 0; step < steps; step++)
            {
                var t = step / (float)steps;
                if (!canonical) t = 1 - t;
                result.Add(a * (1 - t) * (1 - t) + control * 2 * t * (1 - t) + b * t * t);
            }
        }
        return result.ToArray();
    }
    public static Vector2[] Smooth(Vector2[] points, int samples)
    {
        var result = new List<Vector2>();
        for (var i = 0; i < points.Length - 1; i++)
        {
            var a = points[Math.Max(0, i - 1)]; var b = points[i]; var c = points[i + 1]; var d = points[Math.Min(points.Length - 1, i + 2)];
            for (var step = 0; step < samples; step++)
            {
                var t = step / (float)samples; var t2 = t * t; var t3 = t2 * t;
                result.Add((b * 2 + (-a + c) * t + (a * 2 - b * 5 + c * 4 - d) * t2 + (-a + b * 3 - c * 3 + d) * t3) * .5f);
            }
        }
        result.Add(points[^1]); return result.ToArray();
    }
    private static Vector2[] Ribbon(Vector2[] line, float width)
    {
        var left = new List<Vector2>(); var right = new List<Vector2>();
        for (var i = 0; i < line.Length; i++)
        {
            var delta = line[Math.Min(line.Length - 1, i + 1)] - line[Math.Max(0, i - 1)];
            var normal = new Vector2(-delta.Y, delta.X).Normalized();
            left.Add(line[i] + normal * width * .5f); right.Add(line[i] - normal * width * .5f);
        }
        right.Reverse(); return left.Concat(right).ToArray();
    }
}
