using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>An atlas tile: a stage, a resource, or plain ground.</summary>
public sealed record AdventureTile(string MapId, int Column, int Row, AdventureMapNode Site = null, AdventureDiscovery Discovery = null)
{
    public string Id => Site?.Id ?? Discovery?.Id ?? $"ground-{MapId}-{Column}-{Row}";
    public Vector2 Point => AdventureAtlasLandscape.SitePoint(MapId, Column, Row);
    public bool HasInterest => Site != null || Discovery != null;
    public bool IsResource => Discovery != null || Site?.Kind is AdventureSiteKind.Gold or AdventureSiteKind.Food;
    public string Icon => Site?.Icon ?? Discovery?.Icon ?? "map";
    public string Title => Site?.Title ?? Discovery?.Title ?? "Uncharted land";
}

/// <summary>
/// Each zone is a wide isometric grid of tiles. The ten stages lie far apart along roads that leave the
/// first stage in the west, split into three lanes and converge on the boss in the east; supply caches,
/// a forgotten treasury and scattered finds sit off the roads, and every other tile is plain ground.
/// </summary>
public static class AdventureTileCatalog
{
    public const int Columns = 18, Rows = 14;
    public const float HalfWidth = 140.8f, HalfHeight = 70.4f;
    private const float MarginX = 260, MarginY = 230;
    public static readonly Vector2 Origin = new(MarginX + Rows * HalfWidth, MarginY);
    public static readonly Vector2 WorldSize = new((Columns + Rows) * HalfWidth + 2 * MarginX, (Columns + Rows) * HalfHeight + 2 * MarginY);
    /// <summary>Stage positions as fractions of the map (u west to east, v north to south), in stage order.</summary>
    private static readonly Vector2[] StageLayout = {
        new(.08f, .43f), new(.24f, .46f),
        new(.37f, .25f), new(.40f, .50f), new(.37f, .71f),
        new(.58f, .27f), new(.61f, .53f), new(.58f, .78f),
        new(.76f, .50f), new(.93f, .54f) };
    /// <summary>The roads between stages (indices in stage order): three lanes leave the second stage and meet at the ninth.</summary>
    public static readonly (int From, int To)[] Roads = { (0, 1), (1, 2), (1, 3), (1, 4), (2, 5), (3, 6), (4, 7), (5, 8), (6, 8), (7, 8), (8, 9) };
    // Six food, four gold, two essence and two survey charts per zone; the first, near the start, is food.
    private static readonly AdventureDiscoveryKind[] FindKinds = {
        AdventureDiscoveryKind.Food, AdventureDiscoveryKind.Gold, AdventureDiscoveryKind.Food, AdventureDiscoveryKind.Essence,
        AdventureDiscoveryKind.Gold, AdventureDiscoveryKind.Food, AdventureDiscoveryKind.Survey, AdventureDiscoveryKind.Gold,
        AdventureDiscoveryKind.Food, AdventureDiscoveryKind.Essence, AdventureDiscoveryKind.Gold, AdventureDiscoveryKind.Food,
        AdventureDiscoveryKind.Survey, AdventureDiscoveryKind.Food };
    private static readonly Dictionary<string, IReadOnlyList<AdventureTile>> Cache = new();
    private static readonly Dictionary<string, Dictionary<string, AdventureTile>> ById = new();
    private static readonly Dictionary<string, AdventureTile[]> ByCell = new();
    private static readonly Dictionary<string, AdventureTile[]> StageTiles = new();
    private static readonly Dictionary<string, int> FindIndex = new();

    /// <summary>Grid coordinates (column, row) of a point given as map fractions.</summary>
    public static Vector2 GridOf(float u, float v)
    {
        var across = u * (Columns + Rows) - Rows; var down = v * (Columns + Rows);
        return new((across + down) / 2, (down - across) / 2);
    }
    /// <summary>Map fractions (u, v) of a grid position.</summary>
    public static Vector2 LayoutOf(float column, float row) => new((column - row + Rows) / (Columns + Rows), (column + row) / (Columns + Rows));

    /// <summary>A zone's stage cell: the shared layout, mirrored north-south in alternate zones and nudged per zone.</summary>
    private static Vector2I StageCell(string map, int index)
    {
        var zone = Math.Max(0, Array.IndexOf(AssetCoverageCatalog.RouteIds, map));
        var layout = StageLayout[index];
        // Mirror the lanes about the line through the map's west and east corners so neighbouring zones differ.
        if ((zone & 1) == 1) layout.Y = 2 * (Rows + layout.X * (Columns - Rows)) / (Columns + Rows) - layout.Y;
        var hash = AdventureTerrain.Hash(AdventureTerrain.Seed(map), 700 + index);
        layout += new Vector2(((int)(hash % 9) - 4) * .005f, ((int)((hash >> 8) % 9) - 4) * .007f);
        var grid = GridOf(layout.X, layout.Y);
        return new(Mathf.Clamp(Mathf.RoundToInt(grid.X), 1, Columns - 2), Mathf.Clamp(Mathf.RoundToInt(grid.Y), 1, Rows - 2));
    }

    public static IReadOnlyList<AdventureTile> ForMap(string mapId)
    {
        mapId = RouteCatalog.Normalize(mapId);
        if (Cache.TryGetValue(mapId, out var cached)) return cached;
        var stages = GameData.GetStagesForMap(mapId).OrderBy(stage => stage.StageNumber).ToArray();
        var count = Math.Min(stages.Length, StageLayout.Length);
        var interest = new Dictionary<Vector2I, (AdventureMapNode Site, AdventureDiscovery Find)>();
        Vector2 Point(Vector2I at) => AdventureAtlasLandscape.SitePoint(mapId, at.X, at.Y);
        var stageCells = Enumerable.Range(0, count).Select(i => StageCell(mapId, i)).ToArray();
        for (var i = 0; i < count; i++) interest[stageCells[i]] = (AdventureMapCatalog.Find($"leader-{stages[i].StageNumber}"), null);
        var roads = AdventureAtlasLandscape.BuildRoads(mapId, stageCells.Select(Point).ToArray());
        var cells = Enumerable.Range(0, Columns * Rows).Select(cell => new Vector2I(cell % Columns, cell / Columns)).ToArray();
        float RoadDistance(Vector2 point) => roads.Min(road => AdventureAtlasLandscape.DistanceToLine(point, road));
        float InterestDistance(Vector2 point) => interest.Keys.Min(at => Point(at).DistanceTo(point));
        uint Hash(Vector2I at, int salt) => AdventureTerrain.Hash(AdventureTerrain.Seed(mapId), at.Y * Columns + at.X + salt);
        // Resources never share a border with one another.
        var adjacency = AdventureAtlasLandscape.Cells(mapId).Neighbors;
        bool Apart(Vector2I at) => adjacency[at.Y * Columns + at.X].All(cell =>
            !interest.TryGetValue(new Vector2I(cell % Columns, cell / Columns), out var near) || near.Site?.Kind == AdventureSiteKind.Leader);
        bool Free(Vector2I at, float road, float spacing) => !interest.ContainsKey(at) && Apart(at) && !AdventureAtlasLandscape.IsWater(mapId, Point(at))
            && RoadDistance(Point(at)) > road && InterestDistance(Point(at)) > spacing;
        // One supply cache a short walk off the road beside each stage.
        for (var i = 0; i < count; i++)
        {
            var stagePoint = Point(stageCells[i]);
            var cache = cells.Where(at => Free(at, 70, 230) && stagePoint.DistanceTo(Point(at)) is > 250 and < 470)
                .OrderBy(at => Hash(at, 1300 + i)).Cast<Vector2I?>().FirstOrDefault()
                ?? cells.Where(at => Free(at, 40, 180)).OrderBy(at => stagePoint.DistanceTo(Point(at))).First();
            interest[cache] = (AdventureMapCatalog.Find($"supply-{stages[i].StageNumber}"), null);
        }
        // The forgotten treasury hides as far from every road and site as the land allows.
        var hidden = cells.Where(at => Free(at, 0, 0)).OrderByDescending(at => Math.Min(RoadDistance(Point(at)), InterestDistance(Point(at)))).First();
        interest[hidden] = (AdventureMapCatalog.Find($"hidden-{mapId}"), null);
        // Finds: the first close to the first stage, the rest spread evenly over the open country.
        var placed = new List<AdventureDiscovery>();
        var start = Point(stageCells[0]);
        var early = cells.Where(at => Free(at, 70, 220) && start.DistanceTo(Point(at)) is > 250 and < 480).OrderBy(at => Hash(at, 1900)).First();
        foreach (var kind in FindKinds)
        {
            var at = placed.Count == 0 ? early : cells.Where(at => Free(at, 60, 200))
                .OrderByDescending(at => Mathf.Round(InterestDistance(Point(at)) / 40)).ThenBy(at => Hash(at, 2100 + placed.Count)).First();
            var find = AdventureDiscoveryCatalog.Create(mapId, at.X, at.Y, kind);
            interest[at] = (null, find); placed.Add(find);
        }
        for (var i = 0; i < placed.Count; i++) FindIndex[placed[i].Id] = i;
        var tiles = cells.Select(at => interest.TryGetValue(at, out var content)
            ? new AdventureTile(mapId, at.X, at.Y, content.Site, content.Find) : new AdventureTile(mapId, at.X, at.Y)).ToArray();
        ById[mapId] = tiles.ToDictionary(tile => tile.Id);
        ByCell[mapId] = tiles;
        StageTiles[mapId] = stageCells.Select(at => tiles[at.Y * Columns + at.X]).ToArray();
        return Cache[mapId] = tiles;
    }
    public static int FindOrder(AdventureDiscovery find) => FindIndex.GetValueOrDefault(find.Id, int.MaxValue);
    public static AdventureTile Find(string mapId, string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        mapId = RouteCatalog.Normalize(mapId); ForMap(mapId);
        return ById[mapId].GetValueOrDefault(id);
    }
    public static AdventureTile Cell(string mapId, int column, int row)
    {
        mapId = RouteCatalog.Normalize(mapId); ForMap(mapId);
        return column >= 0 && column < Columns && row >= 0 && row < Rows ? ByCell[mapId][row * Columns + column] : null;
    }
    public static AdventureTile Starting(string mapId) => Stage(mapId, 0);
    /// <summary>The tile of the zone's stage at an index in stage order (0 is the first stage).</summary>
    public static AdventureTile Stage(string mapId, int index)
    {
        mapId = RouteCatalog.Normalize(mapId); ForMap(mapId);
        var stages = StageTiles[mapId];
        return stages[Math.Clamp(index, 0, stages.Length - 1)];
    }
    /// <summary>The tile under a world point: the nearest tile centres are tested against their curved outlines.</summary>
    public static AdventureTile At(string mapId, Vector2 point)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || AdventureAtlasLandscape.IsWater(mapId, point)) return null;
        var delta = point - Origin;
        var column = Mathf.RoundToInt((delta.X / HalfWidth + delta.Y / HalfHeight) / 2);
        var row = Mathf.RoundToInt((delta.Y / HalfHeight - delta.X / HalfWidth) / 2);
        for (var r = row - 2; r <= row + 2; r++)
            for (var c = column - 2; c <= column + 2; c++)
                if (Cell(mapId, c, r) is { } tile && AdventureAtlasLandscape.Contains(tile, point)) return tile;
        return null;
    }
    /// <summary>The tiles that share a border with this one.</summary>
    public static IReadOnlyList<AdventureTile> Neighbors(AdventureTile tile) => AdventureAtlasLandscape.Neighbors(tile);
}
