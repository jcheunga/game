using System.Collections.Generic;
using Godot;

/// <summary>
/// Where a zone ends. The view stays over the island: the point it centres on never leaves an outline just inside the
/// coast, so a drag or a flick stops with land in sight instead of carrying the view out over empty sea. Beyond the
/// coast the painted sea fades progressively into its own flat colour, a plain silhouette with no waves, rocks or ships.
/// </summary>
public partial class MapPathCanvas
{
    // How far inside the coast's outline (world units) the view's centre stops.
    private const float PanInset = 260;
    // The painted sea fades into the flat sea over this stretch of open water (world units out from the land).
    private const float FadeStart = 100, FadeEnd = 560;
    private static readonly Dictionary<string, (Vector2[] PanArea, ImageTexture Fade, Rect2 Rect)> Edges = new();

    // The point of the screen the view centres on, above the dock.
    private Vector2 ViewAnchor => new(Size.X * .5f, Size.Y * .44f);

    private (Vector2[] PanArea, ImageTexture Fade, Rect2 Rect) Edge()
    {
        if (Edges.TryGetValue(ActiveMapId, out var edge)) return edge;
        // The coast's convex outline, so the view slides smoothly along it rather than catching on every cove.
        var hull = Geometry2D.ConvexHull(AdventureAtlasLandscape.Coast(ActiveMapId));
        var inset = Geometry2D.OffsetPolygon(hull, -PanInset, Geometry2D.PolyJoinType.Round);
        var area = inset.Count > 0 ? inset[0] : hull;
        var raster = FogRaster();
        var alpha = new float[raster.Distance.Length];
        for (var i = 0; i < alpha.Length; i++) alpha[i] = Mathf.SmoothStep(FadeStart, FadeEnd, raster.Distance[i] * FogCell);
        // The distance sweep runs in octagons; a light blur rounds them off.
        for (var pass = 0; pass < 3; pass++) { BoxBlur(alpha, raster.Width, raster.Height, 3, true); BoxBlur(alpha, raster.Width, raster.Height, 3, false); }
        var bytes = new byte[alpha.Length * 2];
        for (var i = 0; i < alpha.Length; i++) { bytes[i * 2] = 255; bytes[i * 2 + 1] = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha[i] * 255), 0, 255); }
        var image = Image.CreateFromData(raster.Width, raster.Height, false, Image.Format.La8, bytes);
        image.GenerateMipmaps();
        return Edges[ActiveMapId] = (area, ImageTexture.CreateFromImage(image), new Rect2(raster.Bounds.Position, new Vector2(raster.Width, raster.Height) * FogCell));
    }

    /// <summary>The far sea in the zone's own open-sea colour (measured from the painting by art/royal/mapsections.py).</summary>
    private void DrawSeaFade()
    {
        if (_tiles.Count == 0) return;
        var (_, fade, rect) = Edge();
        DrawTextureRect(fade, rect, false, _paintedSea);
    }

    /// <summary>
    /// For the edge review: how far (world units) the view's centre lies outside the island's outline, whether the
    /// screen stays on the painting, and the share of the screen that is painted water not yet faded into the flat sea.
    /// </summary>
    public (float OffIsland, bool OnPainting, float Water) MeasureView()
    {
        var area = Edge().PanArea; var centre = (ViewAnchor - MapOffset) / Zoom;
        var off = 0f;
        if (!Geometry2D.IsPointInPolygon(centre, area))
        {
            off = float.MaxValue;
            for (var i = 0; i < area.Length; i++)
                off = Mathf.Min(off, Geometry2D.GetClosestPointToSegment(centre, area[i], area[(i + 1) % area.Length]).DistanceTo(centre));
        }
        var view = new Rect2(-MapOffset / Zoom, Size / Zoom);
        var raster = FogRaster(); int water = 0, samples = 0;
        for (var y = 16f; y < Size.Y; y += 32)
            for (var x = 16f; x < Size.X; x += 32)
            {
                samples++;
                var cell = (((new Vector2(x, y) - MapOffset) / Zoom - raster.Bounds.Position) / FogCell).Floor();
                if (cell.X < 0 || cell.Y < 0 || cell.X >= raster.Width || cell.Y >= raster.Height) continue;
                var i = (int)cell.Y * raster.Width + (int)cell.X;
                if (raster.Tiles[i] < 0 && Mathf.SmoothStep(FadeStart, FadeEnd, raster.Distance[i] * FogCell) < .5f) water++;
            }
        return (off, MapBounds.Grow(.5f).Encloses(view), water / (float)samples);
    }

    /// <summary>
    /// Keeps the view's centre over the island, then the screen inside the painting (which wins when zoomed out, where
    /// the view already shows most of the zone).
    /// </summary>
    private void ClampView()
    {
        // Before a zone is shown there is no island yet (and its edge must not be worked out from no tiles).
        var area = _tiles.Count > 0 ? Edge().PanArea : System.Array.Empty<Vector2>();
        var centre = (ViewAnchor - MapOffset) / Zoom;
        if (area.Length > 2 && !Geometry2D.IsPointInPolygon(centre, area))
        {
            var nearest = centre; var best = float.MaxValue;
            for (var i = 0; i < area.Length; i++)
            {
                var point = Geometry2D.GetClosestPointToSegment(centre, area[i], area[(i + 1) % area.Length]);
                if (point.DistanceSquaredTo(centre) < best) { best = point.DistanceSquaredTo(centre); nearest = point; }
            }
            MapOffset = ViewAnchor - nearest * Zoom;
        }
        var bounds = MapBounds;
        var low = Size - bounds.End * Zoom; var high = -bounds.Position * Zoom;
        MapOffset = new Vector2(low.X <= high.X ? Mathf.Clamp(MapOffset.X, low.X, high.X) : (low.X + high.X) / 2,
            low.Y <= high.Y ? Mathf.Clamp(MapOffset.Y, low.Y, high.Y) : (low.Y + high.Y) / 2);
    }
}
