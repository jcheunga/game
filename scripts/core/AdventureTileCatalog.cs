using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>One persistent point of interest per atlas tile. Legacy terrain coordinates remain save-compatible.</summary>
public sealed record AdventureTile(string MapId, int Column, int Row, AdventureMapNode Site = null, AdventureDiscovery Discovery = null)
{
    public string Id => Site?.Id ?? Discovery?.Id ?? $"ground-{MapId}-{Column}-{Row}";
    public Vector2 Point => AdventureAtlasLandscape.SitePoint(MapId, Column, Row);
    public bool HasInterest => Site != null || Discovery != null;
    public string Icon => Site?.Icon ?? Discovery?.Icon ?? "map";
    public string Title => Site?.Title ?? Discovery?.Title ?? "Charted ground";
}

public static class AdventureTileCatalog
{
    public const int Columns = 9, Rows = 7;
    public const float HalfWidth = 176, HalfHeight = 88;
    public static readonly Vector2 Origin = new(1744, 352);
    public static Vector2 WorldSize => AdventureTerrain.WorldSize;
    private static readonly Dictionary<string, IReadOnlyList<AdventureTile>> Cache = new();
    private static readonly Vector2I[] Leaders = { new(2,5), new(3,4), new(4,3), new(5,2), new(6,2), new(7,1) };
    private static readonly Vector2I[] Supplies = { new(1,4), new(2,4), new(3,3), new(4,2), new(6,3), new(8,1) };
    private static readonly Vector2I[] Landmarks = { new(2,6), new(3,5), new(4,4), new(5,3), new(6,1), new(7,0) };
    public static IReadOnlyList<AdventureTile> ForMap(string mapId)
    {
        mapId = RouteCatalog.Normalize(mapId);
        if (Cache.TryGetValue(mapId, out var cached)) return cached;
        var sites = AdventureMapCatalog.ForMap(mapId);
        var stages = GameData.GetStagesForMap(mapId).OrderBy(stage => stage.StageNumber).ToArray();
        var tiles = new List<AdventureTile>();
        void Add(AdventureMapNode site, Vector2I at) => tiles.Add(new(mapId, at.X, at.Y, site));
        Add(sites.First(), new(1,5));
        for (var i = 0; i < stages.Length; i++)
        {
            var stage = stages[i].StageNumber;
            Add(AdventureMapCatalog.Find($"leader-{stage}"), Leaders[i]);
            Add(AdventureMapCatalog.Find($"supply-{stage}"), Supplies[i]);
            Add(AdventureMapCatalog.Find($"landmark-{stage}"), Landmarks[i]);
        }
        Add(AdventureMapCatalog.Find($"hidden-{mapId}"), new(0,3));
        // Keep discovery IDs and amounts; only their atlas presentation changes.
        var rewards = new Queue<AdventureDiscovery>(AdventureDiscoveryCatalog.ForMap(mapId));
        var vacancies = Enumerable.Range(0, Columns * Rows).Select(cell => new Vector2I(cell % Columns, cell / Columns))
            .Where(at => tiles.All(tile => tile.Column != at.X || tile.Row != at.Y))
            .OrderBy(at => Math.Max(Math.Abs(at.X - 1), Math.Abs(at.Y - 5))).ThenBy(at => at.Y).ThenBy(at => at.X);
        foreach (var at in vacancies) tiles.Add(new(mapId, at.X, at.Y, Discovery: rewards.TryDequeue(out var reward) ? reward : null));
        return Cache[mapId] = tiles.OrderBy(tile => tile.Column + tile.Row).ThenBy(tile => tile.Column).ToArray();
    }
    public static AdventureTile Find(string mapId, string id) => ForMap(mapId).FirstOrDefault(tile => tile.Id == id);
    public static AdventureTile At(string mapId, Vector2 point)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || AdventureAtlasLandscape.IsWater(mapId, point)) return null;
        return ForMap(mapId).FirstOrDefault(tile => AdventureAtlasLandscape.Contains(tile, point));
    }
    public static IEnumerable<AdventureTile> Surrounding(AdventureTile tile, int radius = 1) => ForMap(tile.MapId)
        .Where(other => Math.Max(Math.Abs(tile.Column - other.Column), Math.Abs(tile.Row - other.Row)) <= radius);
    public static Vector2[] Boundary(AdventureTile tile, float inset = 0) => AdventureAtlasLandscape.Outline(tile)
        .Select(point => inset == 0 ? point : point.MoveToward(tile.Point, inset)).ToArray();
}
