using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class MapPathCanvas
{
    private readonly List<(Vector2[] Shore, int[] Cells)> _waterContours = new();
    private Texture2D _sceneryAtlas;
    private AtlasTexture _rockScenery, _bridgeScenery, _reedScenery;

    private void BuildWaterContours()
    {
        _waterContours.Clear();
        var artPath = "res://assets/world/overworld/painted-scenery-v2.png";
        if (ResourceLoader.Exists(artPath))
        {
            _sceneryAtlas = ResourceLoader.Load<Texture2D>(artPath);
            var size = _sceneryAtlas.GetSize() / 2;
            AtlasTexture Piece(int column, int row) => new() { Atlas = _sceneryAtlas, Region = new Rect2(new Vector2(column, row) * size, size) };
            _rockScenery = Piece(1, 0); _bridgeScenery = Piece(0, 1); _reedScenery = Piece(1, 1);
        }
        var edges = new HashSet<(Vector2 From, Vector2 To)>();
        foreach (var cell in Enumerable.Range(0, AdventureTerrain.CellCount))
        {
            if (AdventureTerrain.Ground(ActiveMapId, cell) is not (AdventureGroundKind.Water or AdventureGroundKind.Bridge)) continue;
            var diamond = AdventureTerrain.Diamond(cell);
            for (var i = 0; i < 4; i++)
            {
                var edge = (diamond[i], diamond[(i + 1) % 4]);
                if (!edges.Remove((edge.Item2, edge.Item1))) edges.Add(edge);
            }
        }
        var outgoing = edges.GroupBy(edge => edge.From).ToDictionary(group => group.Key, group => group.Select(edge => edge.To).ToArray());
        while (edges.Count > 0)
        {
            var first = edges.First();
            var from = first.From; var to = first.To;
            var loop = new List<Vector2> { from };
            var remaining = edges.Count + 1;
            while (remaining-- > 0 && edges.Remove((from, to)))
            {
                loop.Add(to);
                if (to == first.From) break;
                var candidates = outgoing[to].Where(next => edges.Contains((to, next))).ToArray();
                if (candidates.Length == 0) break;
                var incoming = to - from;
                var next = candidates.MinBy(candidate => Mathf.PosMod((candidate - to).Angle() - incoming.Angle(), Mathf.Tau));
                from = to; to = next;
            }
            if (loop.Count < 4 || loop[^1] != loop[0]) continue;
            loop.RemoveAt(loop.Count - 1);
            // Preserve collision-aligned shorelines, rounding only their corners.
            var corners = loop.Where((point, i) => Math.Abs((point - loop[(i + loop.Count - 1) % loop.Count]).Cross(loop[(i + 1) % loop.Count] - point)) > .1f).ToArray();
            var smooth = new List<Vector2>();
            for (var i = 0; i < corners.Length; i++)
            {
                var point = corners[i];
                var previous = corners[(i + corners.Length - 1) % corners.Length];
                var next = corners[(i + 1) % corners.Length];
                var rounding = Mathf.Min(point.DistanceTo(previous), point.DistanceTo(next)) * .42f;
                var before = point.MoveToward(previous, rounding);
                var after = point.MoveToward(next, rounding);
                for (var step = 0; step <= 4; step++)
                {
                    var t = step / 4f;
                    smooth.Add(before * (1 - t) * (1 - t) + point * 2 * (1 - t) * t + after * t * t);
                }
            }
            if (smooth.Count >= 3)
            {
                var shore = smooth.ToArray();
                var cells = Enumerable.Range(0, AdventureTerrain.CellCount)
                    .Where(cell => AdventureTerrain.Ground(ActiveMapId, cell) is AdventureGroundKind.Water or AdventureGroundKind.Bridge)
                    .Where(cell => Geometry2D.IsPointInPolygon(AdventureTerrain.Point(cell), shore)).ToArray();
                _waterContours.Add((shore, cells));
            }
        }
    }

    private void DrawTerrainScenery()
    {
        var view = new Rect2((-MapOffset - new Vector2(100, 100)) / Zoom, (Size + new Vector2(200, 200)) / Zoom);
        var state = GameState.Instance;
        foreach (var patch in _waterContours)
        {
            if (!patch.Cells.Any(cell => state.IsAdventureCellRevealed(ActiveMapId, cell))) continue;
            var shore = patch.Shore;
            var closed = shore.Append(shore[0]).ToArray();
            DrawPolyline(closed, new Color("49613c66"), 25, true);
            DrawColoredPolygon(shore, new Color("358f9e"));
            if (_sceneryAtlas != null)
            {
                var bounds = new Rect2(shore[0], Vector2.Zero);
                foreach (var point in shore) bounds = bounds.Expand(point);
                var uv = shore.Select(point => new Vector2(.065f, .065f) + (point - bounds.Position) / bounds.Size * .36f).ToArray();
                DrawPolygon(shore, new[] { Colors.White }, uv, _sceneryAtlas);
            }
            DrawPolyline(closed, new Color("c6b67caa"), 10, true);
            DrawPolyline(closed, new Color("f1e0b266"), 3, true);
            if (_reedScenery != null)
                for (var point = 0; point < shore.Length; point += 35)
                    DrawTextureRect(_reedScenery, new Rect2(shore[point] - new Vector2(40, 50), new Vector2(80, 80)), false);
        }
        foreach (var cell in AdventureTerrain.DrawOrder)
        {
            var point = AdventureTerrain.Point(cell);
            if (!view.HasPoint(point) || !state.IsAdventureCellRevealed(ActiveMapId, cell)) continue;
            switch (AdventureTerrain.Ground(ActiveMapId, cell))
            {
                case AdventureGroundKind.Water:
                    var drift = GameState.Instance.ReducedMotion ? 0 : Mathf.Sin(_time + cell) * 3;
                    DrawArc(point + new Vector2(4, 7 + drift), 21, .1f, 2.8f, 12, new Color("bae3da66"), 1.5f, true);
                    DrawLine(point + new Vector2(-16, -7 - drift), point + new Vector2(13, -7 - drift), new Color("d0f0e955"), 1.3f, true);
                    break;
                case AdventureGroundKind.Bridge:
                    if (_bridgeScenery != null)
                    {
                        var alongRow = AdventureTerrain.Neighbors(cell).Any(next => Math.Abs(next - cell) == AdventureTerrain.Columns && AdventureTerrain.Ground(ActiveMapId, next) == AdventureGroundKind.Bridge);
                        var bridge = new Rect2(point - new Vector2(85, 60), new Vector2(170, 120));
                        if (alongRow) { bridge.Position += new Vector2(bridge.Size.X, 0); bridge.Size = new Vector2(-bridge.Size.X, bridge.Size.Y); }
                        DrawTextureRect(_bridgeScenery, bridge, false);
                        break;
                    }
                    DrawLine(point + new Vector2(-35, -8), point + new Vector2(35, 8), new Color("493d2d"), 17, true);
                    for (var plank = -4; plank <= 4; plank++)
                        DrawLine(point + new Vector2(plank * 8, -8 + plank * 2), point + new Vector2(plank * 8, 8 + plank * 2), new Color(plank % 2 == 0 ? "c9af78" : "aa8b58"), 6, true);
                    DrawLine(point + new Vector2(-38, -15), point + new Vector2(38, 5), new Color("edd4a1"), 3, true);
                    DrawLine(point + new Vector2(-38, 5), point + new Vector2(38, 25), new Color("745234"), 3, true);
                    break;
                case AdventureGroundKind.Rock:
                    if (_rockScenery != null)
                    {
                        DrawTextureRect(_rockScenery, new Rect2(point - new Vector2(75, 66), new Vector2(150, 120)), false);
                        break;
                    }
                    DrawCircle(point + new Vector2(0, 10), 36, new Color("263b2d55"));
                    for (var rock = 0; rock < 3; rock++)
                    {
                        var center = point + new Vector2((rock - 1) * 23, rock % 2 * 10 - 7);
                        var shape = new[] { center + new Vector2(-22, 7), center + new Vector2(-15, -14), center + new Vector2(6, -25), center + new Vector2(24, -7), center + new Vector2(21, 10), center + new Vector2(-3, 18) };
                        DrawPolygon(shape, new[] { new Color("536663"), new Color("a1b1a0"), new Color("c7ceb3"), new Color("8b9c90"), new Color("586f6b"), new Color("687e71") });
                        DrawLine(center + new Vector2(-15, -14), center + new Vector2(6, -25), new Color("e2ddba"), 2, true);
                    }
                    break;
            }
        }
    }
}
