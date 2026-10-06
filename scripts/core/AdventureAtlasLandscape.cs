using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>Deterministic geography, independent of saved point identities and completion rules.</summary>
public static class AdventureAtlasLandscape
{
    private sealed record Region(Vector2[] Outline, Vector2[][] Land);
    private sealed record Geography(Dictionary<string, Region> Regions, Vector2[] Coast, Vector2[] River, Vector2[] Bridges, Vector2[] RoadPath);
    private static readonly Dictionary<string, Geography> Cache = new();
    private static readonly Dictionary<string, Vector2[]> RiverLines = new();
    private static readonly Dictionary<string, Vector2[]> CoastLines = new();
    private static readonly Dictionary<(string Map, int Column, int Row), Vector2> SitePoints = new();
    public static float Phase(string map) => AdventureTerrain.Seed(map) % 1000 / 137f;
    public static Vector2 Project(string map, float column, float row)
    {
        var phase = Phase(map);
        // Broad bends vary the entire region rather than merely jittering individual corners.
        var c = column + .62f * Mathf.Sin(row * .8f + phase) + .24f * Mathf.Sin(column * .65f + row * .45f);
        var r = row + .50f * Mathf.Sin(column * .7f + phase * .7f) + .18f * Mathf.Cos(row * 1.1f);
        return AdventureTileCatalog.Origin + new Vector2((c - r) * AdventureTileCatalog.HalfWidth, (c + r) * AdventureTileCatalog.HalfHeight);
    }
    public static float RiverColumn(string map, float row) => 5.1f - row * .58f + .46f * Mathf.Sin(row * 1.1f + Phase(map));
    public static Vector2 SitePoint(string map, int column, int row)
    {
        if (SitePoints.TryGetValue((map, column, row), out var cached)) return cached;
        var hash = AdventureTerrain.Hash(AdventureTerrain.Seed(map), row * AdventureTileCatalog.Columns + column + 370);
        var c = column + ((int)(hash % 101) - 50) / 220f;
        var r = row + ((int)((hash >> 8) % 101) - 50) / 310f;
        var river = RiverColumn(map, r);
        // Landmarks sit on the bank, never in the water itself.
        if (Math.Abs(c - river) < .58f) c = river + (c < river ? -.58f : .58f);
        var point = Project(map, c, r);
        var side = c < river ? -1 : 1;
        for (var attempt = 0; attempt < 12 && DistanceToLine(point, RiverSkeleton(map)) < WaterWidth(map) * .5f + 22; attempt++)
        {
            c += side * .08f;
            point = Project(map, c, r);
        }
        var coast = CoastSkeleton(map);
        for (var attempt = 0; attempt < 16 && !Geometry2D.IsPointInPolygon(point, coast); attempt++)
        {
            c = Mathf.Lerp(c, 4, .06f); r = Mathf.Lerp(r, 3, .06f);
            point = Project(map, c, r);
            if (DistanceToLine(point, RiverSkeleton(map)) < WaterWidth(map) * .5f + 22)
            {
                c += side * .12f; point = Project(map, c, r);
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
        return RiverLines[map] = Enumerable.Range(0, 90).Select(i => {
            var row = -.9f + i / 89f * 8.6f;
            return Project(map, RiverColumn(map, row), row);
        }).ToArray();
    }
    public static Vector2[] Outline(AdventureTile tile) => ForMap(tile.MapId).Regions[tile.Id].Outline;
    public static Vector2[][] Land(AdventureTile tile) => ForMap(tile.MapId).Regions[tile.Id].Land;
    public static Vector2[] Coast(string map) => ForMap(map).Coast;
    public static Vector2[] River(string map) => ForMap(map).River;
    public static Vector2[] RoadPath(string map) => ForMap(map).RoadPath;
    public static Vector2[] Bridges(string map) => ForMap(map).Bridges;
    public static bool Contains(AdventureTile tile, Vector2 point) => Geometry2D.IsPointInPolygon(point, Outline(tile));
    public static bool IsWater(string map, Vector2 point) => DistanceToLine(point, River(map)) < WaterWidth(map) * .5f;
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
    private static Geography ForMap(string map)
    {
        if (Cache.TryGetValue(map, out var geography)) return geography;
        var tiles = AdventureTileCatalog.ForMap(map);
        var coast = CoastSkeleton(map);
        var river = RiverSkeleton(map);
        var water = Ribbon(river, WaterWidth(map));
        var route = tiles.Where(tile => tile.Site?.Kind == AdventureSiteKind.Leader)
            .OrderBy(tile => tile.Site.Stage).Select(tile => tile.Point + new Vector2(0, 31)).ToArray();
        var roadPath = Smooth(route, 15);
        var regions = new Dictionary<string, Region>();
        var sitePoints = tiles.Select(tile => tile.Point).ToArray();
        var minimum = Vector2.One * (100 * AdventureTileCatalog.LayoutScale);
        var maximum = AdventureTileCatalog.WorldSize - minimum;
        foreach (var tile in tiles)
        {
            var bounds = new[] { minimum, new Vector2(maximum.X, minimum.Y), maximum, new Vector2(minimum.X, maximum.Y) };
            foreach (var other in tiles)
            {
                if (other.Id == tile.Id) continue;
                var normal = other.Point - tile.Point;
                bounds = ClipHalfPlane(bounds, (tile.Point + other.Point) * .5f, normal);
                if (bounds.Length < 3) break;
            }
            var clipped = Geometry2D.IntersectPolygons(bounds, coast);
            var main = clipped.FirstOrDefault(polygon => Geometry2D.IsPointInPolygon(tile.Point, polygon)) ?? bounds;
            var outline = CurveEdges(map, main, sitePoints);
            var land = Geometry2D.ClipPolygons(outline, water).Where(polygon => polygon.Length >= 3 && Geometry2D.TriangulatePolygon(polygon).Length > 0).ToArray();
            regions[tile.Id] = new(outline, land);
        }
        var bridges = new[] { 1.3f, 3.4f, 5.55f }.Select(row => Project(map, RiverColumn(map, row), row)).ToArray();
        return Cache[map] = new(regions, coast, river, bridges, roadPath);
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
        // A tapered shoreline encloses every site while breaking the rectangular atlas footprint.
        var points = new List<Vector2>(); var phase = Phase(map);
        for (var i = 0; i <= 30; i++)
        {
            var c = -.68f + i / 30f * 9.35f;
            var r = -.76f + .16f * Mathf.Sin(c * 1.6f + phase);
            points.Add(Project(map, c, r));
        }
        for (var i = 1; i <= 24; i++)
        {
            var r = -.76f + i / 24f * 7.5f;
            var c = 8.68f + .15f * Mathf.Sin(r * 1.9f + phase);
            // A pronounced inlet occupies the unused southeastern margin.
            if (r > 4) c -= Mathf.Sin((r - 4) / 2.74f * Mathf.Pi) * .42f;
            points.Add(Project(map, c, r));
        }
        for (var i = 1; i <= 30; i++)
        {
            var c = 8.68f - i / 30f * 9.36f;
            var r = 6.74f + .20f * Mathf.Sin(c * 1.4f + phase * .8f);
            points.Add(Project(map, c, r));
        }
        for (var i = 1; i < 24; i++)
        {
            var r = 6.74f - i / 24f * 7.5f;
            var c = -.68f + .17f * Mathf.Sin(r * 1.8f + phase);
            points.Add(Project(map, c, r));
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
