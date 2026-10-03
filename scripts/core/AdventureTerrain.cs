using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public enum AdventureGroundKind { Ground, Water, Rock, Bridge }

public static class AdventureTerrain
{
    public const int Columns = 32, Rows = 24, CellCount = Columns * Rows;
    public const float HalfWidth = 64, HalfHeight = 32;
    public static readonly Vector2 Origin = new(Rows * HalfWidth + 192, 192);
    public static readonly Vector2 WorldSize = new((Columns + Rows) * HalfWidth + 384, (Columns + Rows) * HalfHeight + 384);
    private static readonly Dictionary<string, AdventureGroundKind[]> Terrain = new();
    public static int Index(int column, int row) => column >= 0 && column < Columns && row >= 0 && row < Rows ? row * Columns + column : -1;
    public static Vector2 Point(int cell) => Origin + new Vector2((cell % Columns - cell / Columns) * HalfWidth, (cell % Columns + cell / Columns) * HalfHeight);
    public static Vector2[] Diamond(int cell)
    {
        var p = Point(cell);
        return new[] { p + new Vector2(0,-HalfHeight), p + new Vector2(HalfWidth,0), p + new Vector2(0,HalfHeight), p + new Vector2(-HalfWidth,0) };
    }
    public static Vector2 GridPoint(Vector2 point)
    {
        var delta = point - Origin;
        return new((delta.X / HalfWidth + delta.Y / HalfHeight) / 2, (delta.Y / HalfHeight - delta.X / HalfWidth) / 2);
    }
    public static int Cell(Vector2 point)
    {
        var grid = GridPoint(point);
        return Index(Mathf.RoundToInt(grid.X), Mathf.RoundToInt(grid.Y));
    }
    public static int Distance(int a, int b) => Math.Abs(a % Columns - b % Columns) + Math.Abs(a / Columns - b / Columns);
    public static IEnumerable<int> Neighbors(int cell)
    {
        if (cell < 0 || cell >= CellCount) yield break;
        if (cell % Columns > 0) yield return cell - 1;
        if (cell % Columns < Columns - 1) yield return cell + 1;
        if (cell >= Columns) yield return cell - Columns;
        if (cell < Columns * (Rows - 1)) yield return cell + Columns;
    }
    public static IEnumerable<int> Area(int center, int radius) => Enumerable.Range(0, CellCount).Where(c => Distance(c,center) <= radius);
    public static uint Seed(string text)
    {
        uint value = 2166136261;
        foreach (var ch in text) value = (value ^ ch) * 16777619;
        return value;
    }
    public static uint Hash(uint seed, int cell)
    {
        uint value = seed ^ ((uint)cell * 747796405u + 2891336453u);
        value = ((value >> (int)((value >> 28) + 4)) ^ value) * 277803737u;
        return (value >> 22) ^ value;
    }
    public static AdventureGroundKind Ground(string map, int cell)
    {
        if (cell < 0 || cell >= CellCount) return AdventureGroundKind.Rock;
        map = RouteCatalog.Normalize(map);
        if (!Terrain.TryGetValue(map, out var cells)) Terrain[map] = cells = Build(map);
        return cells[cell];
    }
    private static AdventureGroundKind[] Build(string map)
    {
        var cells = new AdventureGroundKind[CellCount]; var seed = Seed(map);
        // Irregular pools and ridges break the plain into several places to explore.
        for (var pocket = 0; pocket < 11; pocket++)
        {
            var h = Hash(seed,pocket + 2000);
            var column = 3 + (int)(h % 26); var row = 3 + (int)((h >> 8) % 18);
            var width = 1.3f + (h >> 16) % 3 * .55f; var height = 1.1f + (h >> 20) % 3 * .45f;
            for (var cell = 0; cell < CellCount; cell++)
            {
                var x = (cell % Columns - column) / width; var y = (cell / Columns - row) / height;
                if (x*x + y*y < 1.0f + Hash(seed,cell) % 10 * .025f)
                    cells[cell] = pocket % 3 == 0 ? AdventureGroundKind.Rock : AdventureGroundKind.Water;
            }
        }
        var sites = AdventureMapCatalog.ForMap(map); var camp = Cell(sites[0].Point);
        void Open(int cell) => cells[cell] = cells[cell] is AdventureGroundKind.Water or AdventureGroundKind.Bridge ? AdventureGroundKind.Bridge : AdventureGroundKind.Ground;
        foreach (var site in sites)
        {
            var goal = Cell(site.Point); var at = camp;
            // Every landmark has a route, including crossings through an obstructed pocket.
            while (at % Columns != goal % Columns) { Open(at); at += Math.Sign(goal % Columns - at % Columns); }
            while (at / Columns != goal / Columns) { Open(at); at += Columns * Math.Sign(goal / Columns - at / Columns); }
            foreach (var nearby in Area(goal,1)) Open(nearby);
        }
        var reachable = new HashSet<int> { camp }; var queue = new Queue<int>(); queue.Enqueue(camp);
        while (queue.TryDequeue(out var at)) foreach (var next in Neighbors(at))
            if ((cells[next] is AdventureGroundKind.Ground or AdventureGroundKind.Bridge) && reachable.Add(next)) queue.Enqueue(next);
        for (var cell = 0; cell < CellCount; cell++)
            if (!reachable.Contains(cell) && (cells[cell] is AdventureGroundKind.Ground or AdventureGroundKind.Bridge)) cells[cell] = AdventureGroundKind.Rock;
        return cells;
    }
    public static bool Walkable(string map, int cell) => cell >= 0 && cell < CellCount && Ground(map,cell) is AdventureGroundKind.Ground or AdventureGroundKind.Bridge;
    public static int[] Path(string map, int from, int to)
    {
        if (!Walkable(map, from) || !Walkable(map, to)) return Array.Empty<int>();
        var previous = new Dictionary<int, int> { [from] = -1 }; var queue = new Queue<int>(); queue.Enqueue(from);
        while (queue.TryDequeue(out var current))
        {
            if (current == to) break;
            foreach (var next in Neighbors(current))
                if (Walkable(map, next) && previous.TryAdd(next, current)) queue.Enqueue(next);
        }
        if (!previous.ContainsKey(to)) return Array.Empty<int>();
        var path = new List<int>();
        for (var cell = to; cell != -1; cell = previous[cell]) path.Add(cell);
        path.Reverse(); return path.ToArray();
    }
}
