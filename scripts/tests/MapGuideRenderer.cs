using System.Linq;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Renders each zone's deterministic geography (coast, river, bridges, stage road and site points) as a
/// flat colour layout guide for the painted atlas (art/royal/gen/map-guides). The painting is generated
/// over this guide so its roads, river and clearings line up with the playable tiles.
///   UiReviewSmoke --map-guides
/// </summary>
public partial class MapGuideRenderer : Node2D
{
    public string Map = "city";
    private static readonly Color Sea = new("1d3b57"), Land = new("86a95e"), Forest = new("2f5a2c"), River = new("3f86c2"),
        Road = new("d3ad73"), Bridge = new("7a4f2a"), Leader = new("e0262b"), Site = new("ffffff");

    public override void _Draw()
    {
        var world = AdventureTileCatalog.WorldSize;
        DrawRect(new Rect2(Vector2.Zero, world), Sea);
        var coast = AdventureAtlasLandscape.Coast(Map);
        foreach (var triangle in Chunk(Geometry2D.TriangulatePolygon(coast)))
            DrawColoredPolygon(new[] { coast[triangle[0]], coast[triangle[1]], coast[triangle[2]] }, Land);
        var river = AdventureAtlasLandscape.River(Map);
        var roads = AdventureAtlasLandscape.RoadPath(Map);
        var tiles = AdventureTileCatalog.ForMap(Map);
        // Forest hints: dense ground away from the road, river and sites.
        for (var y = 40f; y < world.Y; y += 30)
            for (var x = 40f; x < world.X; x += 30)
            {
                var point = new Vector2(x + (y % 60 == 0 ? 15 : 0), y);
                if (!Geometry2D.IsPointInPolygon(point, coast)) continue;
                if (AdventureAtlasLandscape.DistanceToLine(point, river) < AdventureAtlasLandscape.WaterWidth(Map) + 40) continue;
                if (AdventureAtlasLandscape.DistanceToLine(point, roads) < 46) continue;
                if (tiles.Any(tile => tile.HasInterest && tile.Point.DistanceTo(point) < 70)) continue;
                if (AdventureAtlasLandscape.ForestDensity(Map, point) > .58f) DrawCircle(point, 17, Forest);
            }
        DrawPolyline(river, River, AdventureAtlasLandscape.WaterWidth(Map), true);
        DrawPolyline(roads, Road, 14, true);
        foreach (var bridge in AdventureAtlasLandscape.Bridges(Map))
            DrawRect(new Rect2(bridge - new Vector2(30, 13), new Vector2(60, 26)), Bridge);
        foreach (var tile in tiles.Where(tile => tile.HasInterest))
        {
            var leader = tile.Site?.Kind == AdventureSiteKind.Leader;
            DrawCircle(tile.Point, leader ? 24 : 12, leader ? Leader : Site);
        }
    }

    private static System.Collections.Generic.IEnumerable<int[]> Chunk(int[] indices)
    {
        for (var i = 0; i + 2 < indices.Length; i += 3) yield return new[] { indices[i], indices[i + 1], indices[i + 2] };
    }

    public static async Task RenderAll(Node host, string folder)
    {
        System.IO.Directory.CreateDirectory(folder);
        foreach (var map in RouteCatalog.GetAll().Select(route => route.Id))
        {
            var size = (Vector2I)AdventureTileCatalog.WorldSize.Ceil();
            var viewport = new SubViewport { Size = size, TransparentBg = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            host.AddChild(viewport);
            viewport.AddChild(new MapGuideRenderer { Map = map });
            await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            viewport.GetTexture().GetImage().SavePng($"{folder}/{map}.png");
            GD.Print($"MAP_GUIDE {map} {size}");
            viewport.QueueFree();
        }
    }
}
