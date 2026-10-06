using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>One persistent point of interest per atlas tile. Legacy terrain coordinates remain save-compatible.</summary>
public sealed record AdventureTile(string MapId, int Column, int Row, AdventureMapNode Site = null, AdventureDiscovery Discovery = null,
    string RetiredSiteId = "")
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
    // Compact the atlas without changing persistent tiles or the 2:1 ground perspective.
    public const float LayoutScale = .8f;
    public const float HalfWidth = 176 * LayoutScale, HalfHeight = 88 * LayoutScale;
    public static readonly Vector2 Origin = new Vector2(1744, 352) * LayoutScale;
    public static Vector2 WorldSize => AdventureTerrain.WorldSize * LayoutScale;
    private static readonly Dictionary<string, IReadOnlyList<AdventureTile>> Cache = new();
    // Ten stages per zone: the road climbs the west bank, crosses the top bridge, runs down the east side
    // and turns back north to the boss in the far corner.
    private static readonly Vector2I[] Leaders = { new(1,5), new(1,3), new(2,1), new(4,0), new(6,1), new(5,3), new(4,5), new(6,5), new(7,3), new(8,1) };
    private static readonly Vector2I[] Supplies = { new(0,4), new(0,2), new(3,2), new(3,0), new(6,0), new(6,3), new(3,6), new(6,6), new(8,3), new(7,1) };
    public static IReadOnlyList<AdventureTile> ForMap(string mapId)
    {
        mapId = RouteCatalog.Normalize(mapId);
        if (Cache.TryGetValue(mapId, out var cached)) return cached;
        var stages = GameData.GetStagesForMap(mapId).OrderBy(stage => stage.StageNumber).ToArray();
        var tiles = new List<AdventureTile>();
        void Add(AdventureMapNode site, Vector2I at) => tiles.Add(new(mapId, at.X, at.Y, site));
        // Retire Lantern Camp as plain terrain without shifting existing discovery tiles.
        tiles.Add(new(mapId, 2, 5, RetiredSiteId: $"camp-{mapId}"));
        for (var i = 0; i < stages.Length; i++)
        {
            var stage = stages[i].StageNumber;
            Add(AdventureMapCatalog.Find($"leader-{stage}"), Leaders[i]);
            Add(AdventureMapCatalog.Find($"supply-{stage}"), Supplies[i]);
        }
        Add(AdventureMapCatalog.Find($"hidden-{mapId}"), new(0,0));
        // Keep discovery IDs and amounts; only their atlas presentation changes.
        var rewards = new Queue<AdventureDiscovery>(AdventureDiscoveryCatalog.ForMap(mapId));
        var vacancies = Enumerable.Range(0, Columns * Rows).Select(cell => new Vector2I(cell % Columns, cell / Columns))
            .Where(at => tiles.All(tile => tile.Column != at.X || tile.Row != at.Y))
            .OrderBy(at => Math.Max(Math.Abs(at.X - 1), Math.Abs(at.Y - 5))).ThenBy(at => at.Y).ThenBy(at => at.X);
        foreach (var at in vacancies) tiles.Add(new(mapId, at.X, at.Y, Discovery: rewards.TryDequeue(out var reward) ? reward : null));
        return Cache[mapId] = tiles.OrderBy(tile => tile.Column + tile.Row).ThenBy(tile => tile.Column).ToArray();
    }
    public static AdventureTile Find(string mapId, string id) => ForMap(mapId).FirstOrDefault(tile => tile.Id == id);
    public static AdventureTile Starting(string mapId) => ForMap(mapId).Where(tile => tile.Site?.Kind == AdventureSiteKind.Leader)
        .OrderBy(tile => tile.Site.Stage).First();
    public static AdventureTile At(string mapId, Vector2 point)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || AdventureAtlasLandscape.IsWater(mapId, point)) return null;
        return ForMap(mapId).FirstOrDefault(tile => AdventureAtlasLandscape.Contains(tile, point));
    }
    public static IEnumerable<AdventureTile> Surrounding(AdventureTile tile, int radius = 1) => ForMap(tile.MapId)
        .Where(other => Math.Max(Math.Abs(tile.Column - other.Column), Math.Abs(tile.Row - other.Row)) <= radius);
}
