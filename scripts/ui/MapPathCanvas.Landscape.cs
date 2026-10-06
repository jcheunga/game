using System.Linq;
using Godot;

public partial class MapPathCanvas
{
    /// <summary>Outlines the selected tile and highlights the hovered one on the painted land.</summary>
    private void DrawSiteHighlights()
    {
        var state = GameState.Instance;
        if (AdventureTileCatalog.Find(ActiveMapId, _selectedId) is { } selected && state.IsAdventureTileOpen(selected))
        {
            foreach (var outline in AdventureAtlasLandscape.Land(selected).Where(outline => Geometry2D.IsPointInPolygon(selected.Point, outline)))
                DrawPolyline(outline.Append(outline[0]).ToArray(), new Color("dac795aa"), 1.8f, true);
        }
        var hoveredId = _dragging ? null :
            _tokens.FirstOrDefault(token => token.Visible && !token.Disabled && token.IsHovered())?.Site.Id ??
            _discoveries.FirstOrDefault(token => token.Visible && !token.Disabled && token.IsHovered())?.Discovery.Id;
        if (AdventureTileCatalog.Find(ActiveMapId, hoveredId) is { } hovered && state.IsAdventureTileOpen(hovered))
        {
            foreach (var outline in AdventureAtlasLandscape.Land(hovered).Where(outline => Geometry2D.IsPointInPolygon(hovered.Point, outline)))
            {
                DrawColoredPolygon(outline, new Color(1, .86f, .55f, .035f));
                // Keep the highlight inside the tile so neighboring fog cannot cover its edge.
                foreach (var inset in Geometry2D.OffsetPolygon(outline, -4f / Zoom))
                {
                    var edge = inset.Append(inset[0]).ToArray();
                    DrawPolyline(edge, new Color("e7bf7338"), 5.5f / Zoom, true);
                    DrawPolyline(edge, new Color("f4d89bed"), 2f / Zoom, true);
                }
            }
        }
    }

    private static Rect2 PolygonBounds(Vector2[] outline)
    {
        var bounds = new Rect2(outline[0], Vector2.Zero);
        foreach (var point in outline) bounds = bounds.Expand(point);
        return bounds;
    }
}
