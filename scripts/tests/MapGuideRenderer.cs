using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Renders each zone's deterministic geography (coast, river, bridges, roads and site points) as a flat
/// colour layout guide for the painted atlas (art/royal/gen/map-guides). The painting is generated over this
/// guide in four overlapping sections (AdventureAtlasLandscape.PaintingSections) so its roads, river and
/// clearings line up with the playable tiles.
///   UiReviewSmoke --map-guides
/// </summary>
public partial class MapGuideRenderer : Node2D
{
    public string Map = "city";
    private static readonly Color Sea = new("1d3b57"), Land = new("86a95e"), Forest = new("2f5a2c"), River = new("3f86c2"),
        Road = new("d3ad73"), Bridge = new("7a4f2a"), Leader = new("e0262b"), Site = new("ffffff");

    public override void _Draw()
    {
        var rect = AdventureAtlasLandscape.PaintingRect(Map);
        DrawSetTransform(-rect.Position);
        DrawRect(rect, Sea);
        var coast = AdventureAtlasLandscape.Coast(Map);
        foreach (var triangle in Chunk(Geometry2D.TriangulatePolygon(coast)))
            DrawColoredPolygon(new[] { coast[triangle[0]], coast[triangle[1]], coast[triangle[2]] }, Land);
        var river = AdventureAtlasLandscape.River(Map);
        var roads = AdventureAtlasLandscape.Roads(Map);
        var sites = AdventureTileCatalog.ForMap(Map).Where(tile => tile.HasInterest).Select(tile => tile.Point).ToArray();
        // Forest hints: dense ground away from the roads, river and sites.
        for (var y = rect.Position.Y + 40; y < rect.End.Y; y += 30)
            for (var x = rect.Position.X + 40; x < rect.End.X; x += 30)
            {
                var point = new Vector2(x + (Mathf.RoundToInt(y / 30) % 2 == 0 ? 15 : 0), y);
                if (!Geometry2D.IsPointInPolygon(point, coast)) continue;
                if (AdventureAtlasLandscape.ForestDensity(Map, point) <= .58f) continue;
                if (sites.Any(site => site.DistanceTo(point) < 70)) continue;
                if (AdventureAtlasLandscape.DistanceToLine(point, river) < AdventureAtlasLandscape.WaterWidth(Map) + 40) continue;
                if (roads.Any(road => AdventureAtlasLandscape.DistanceToLine(point, road) < 46)) continue;
                DrawCircle(point, 17, Forest);
            }
        DrawPolyline(river, River, AdventureAtlasLandscape.WaterWidth(Map), true);
        foreach (var road in roads) DrawPolyline(road, Road, 14, true);
        foreach (var bridge in AdventureAtlasLandscape.Bridges(Map))
            DrawRect(new Rect2(bridge - new Vector2(30, 13), new Vector2(60, 26)), Bridge);
        foreach (var tile in AdventureTileCatalog.ForMap(Map).Where(tile => tile.HasInterest))
        {
            var leader = tile.Site?.Kind == AdventureSiteKind.Leader;
            DrawCircle(tile.Point, leader ? 24 : 12, leader ? Leader : Site);
        }
    }

    private static IEnumerable<int[]> Chunk(int[] indices)
    {
        for (var i = 0; i + 2 < indices.Length; i += 3) yield return new[] { indices[i], indices[i + 1], indices[i + 2] };
    }

    /// <summary>Writes each zone's full guide, its four 1920x1080 section guides and crops.json (world rects).</summary>
    public static async Task RenderAll(Node host, string folder)
    {
        System.IO.Directory.CreateDirectory(folder);
        var crops = new Godot.Collections.Dictionary();
        foreach (var map in RouteCatalog.GetAll().Select(route => route.Id))
        {
            var rect = AdventureAtlasLandscape.PaintingRect(map);
            var size = (Vector2I)rect.Size.Ceil();
            var viewport = new SubViewport { Size = size, TransparentBg = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            host.AddChild(viewport);
            viewport.AddChild(new MapGuideRenderer { Map = map });
            await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var image = viewport.GetTexture().GetImage();
            image.SavePng($"{folder}/{map}.png");
            var sections = new Godot.Collections.Array();
            var sectionRects = AdventureAtlasLandscape.PaintingSections(map);
            for (var i = 0; i < sectionRects.Length; i++)
            {
                var local = new Rect2I((Vector2I)(sectionRects[i].Position - rect.Position).Round(), (Vector2I)sectionRects[i].Size.Round());
                var crop = image.GetRegion(local.Intersection(new Rect2I(Vector2I.Zero, image.GetSize())));
                crop.Resize(1920, 1080, Image.Interpolation.Lanczos);
                crop.SavePng($"{folder}/{map}-section{i}.png");
                sections.Add(Rect(sectionRects[i]));
            }
            crops[map] = new Godot.Collections.Dictionary { ["rect"] = Rect(rect), ["sections"] = sections };
            GD.Print($"MAP_GUIDE {map} {size} tiles={AdventureTileCatalog.ForMap(map).Count} bridges={AdventureAtlasLandscape.Bridges(map).Length}");
            viewport.QueueFree();
        }
        using var file = FileAccess.Open($"{folder}/crops.json", FileAccess.ModeFlags.Write);
        file.StoreString(Json.Stringify(crops, "  "));
    }

    private static Godot.Collections.Array Rect(Rect2 rect) => new() { Mathf.Round(rect.Position.X), Mathf.Round(rect.Position.Y), Mathf.Round(rect.Size.X), Mathf.Round(rect.Size.Y) };
}
