using Godot;

/// <summary>A zone's painted parallax layers, behind the simulation (or, for the front layers, before it).</summary>
public partial class BattleTerrainCanvas : Node2D
{
    public ZoneBackdrop Layers;
    /// <summary>Draws only the front layers (a second canvas above the troops), or only the others.</summary>
    public bool FrontLayers;
    // Layers scroll relative to the camera's position when centred on the field.
    public float WorldCentreX;
    private Vector2 _drawnCamera = new(float.NaN, float.NaN);

    private Vector2 CameraCentre => GetViewport()?.GetCamera2D() is { } camera ? camera.GetScreenCenterPosition() : new Vector2(WorldCentreX, 0);

    public override void _Process(double delta)
    {
        // Far layers move with the camera, so they redraw whenever it does.
        if (Layers != null && CameraCentre != _drawnCamera) QueueRedraw();
    }

    public override void _Draw()
    {
        if (Layers == null) return;
        if (!FrontLayers)
        {
            DrawSetTransformMatrix(GetGlobalTransformWithCanvas().AffineInverse());
            DrawRect(GetViewportRect(), Layers.Sky);
            DrawSetTransform(Vector2.Zero);
        }
        _drawnCamera = CameraCentre;
        // The world span the camera can see, so tiled layers repeat exactly as far as needed.
        var view = GetViewportRect();
        var zoom = GetViewport()?.GetCamera2D()?.Zoom.X ?? 1f;
        var halfWidth = view.Size.X / zoom * .5f + 4;
        var viewBottom = _drawnCamera.Y + view.Size.Y / zoom * .5f + 2;
        var groundBottom = float.MinValue;
        BackdropLayer lowest = null;
        foreach (var layer in Layers.Layers)
        {
            if (layer.Front) continue;
            if (layer.Rect.End.Y > groundBottom) { groundBottom = layer.Rect.End.Y; lowest = layer; }
        }
        // The painting ends a little below the road. A wide view (the battle overview) sees past it: there the
        // ground carries on in its own colour, darkening toward the bottom of the screen, behind the front layer.
        if (!FrontLayers && lowest != null && viewBottom > groundBottom)
        {
            var top = groundBottom - 2;
            var colour = BottomColour(lowest.Texture);
            var left = _drawnCamera.X - halfWidth;
            var right = _drawnCamera.X + halfWidth;
            DrawPolygon(new[] { new Vector2(left, top), new Vector2(right, top), new Vector2(right, viewBottom), new Vector2(left, viewBottom) },
                new[] { colour, colour, colour.Darkened(.45f), colour.Darkened(.45f) });
        }
        BackdropLayer front = null;
        foreach (var layer in Layers.Layers)
        {
            if (layer.Front != FrontLayers) continue;
            if (front == null || layer.Rect.End.Y > front.Rect.End.Y) front = layer;
            foreach (var rect in Tiles(layer, halfWidth)) DrawTextureRect(layer.Texture, rect, false);
        }
        // ...and the front layer's last rows, mirrored, fade into that ground so the painting has no hard edge.
        if (!FrontLayers || front == null || viewBottom <= front.Rect.End.Y) return;
        const float fraction = .45f;
        var strip = front.Rect.Size.Y * fraction;
        var solid = Colors.White; var clear = new Color(1, 1, 1, 0);
        foreach (var rect in Tiles(front, halfWidth))
            DrawPolygon(new[] { new Vector2(rect.Position.X, rect.End.Y), new Vector2(rect.End.X, rect.End.Y),
                    new Vector2(rect.End.X, rect.End.Y + strip), new Vector2(rect.Position.X, rect.End.Y + strip) },
                new[] { solid, solid, clear, clear },
                new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 1 - fraction), new Vector2(0, 1 - fraction) }, front.Texture);
    }

    private static readonly System.Collections.Generic.Dictionary<Texture2D, Color> BottomColours = new();

    /// <summary>The average colour of the painted bottom rows of a layer.</summary>
    private static Color BottomColour(Texture2D texture)
    {
        if (BottomColours.TryGetValue(texture, out var cached)) return cached;
        using var image = texture.GetImage();
        if (image.IsCompressed()) image.Decompress();
        var w = image.GetWidth(); var h = image.GetHeight();
        float r = 0, g = 0, b = 0, n = 0;
        for (var y = (int)(h * .88f); y < h; y += 2)
            for (var x = 0; x < w; x += 4)
            {
                var c = image.GetPixel(x, y);
                if (c.A < .9f) continue;
                r += c.R; g += c.G; b += c.B; n++;
            }
        return BottomColours[texture] = n > 0 ? new Color(r / n, g / n, b / n) : new Color(.25f, .3f, .18f);
    }

    /// <summary>Where a layer is drawn for the current camera: once, or tiled as far as the view reaches.</summary>
    private System.Collections.Generic.IEnumerable<Rect2> Tiles(BackdropLayer layer, float halfWidth)
    {
        var offset = new Vector2((1 - layer.Parallax) * (_drawnCamera.X - WorldCentreX), 0);
        var rect = new Rect2(layer.Rect.Position + offset, layer.Rect.Size);
        if (!layer.Tile)
        {
            // A single painting (the far panorama) grows from its foot to cover a view wider than itself.
            var grow = Mathf.Max(1f, halfWidth * 2 / rect.Size.X);
            var size = rect.Size * grow;
            yield return new Rect2(new Vector2(_drawnCamera.X - size.X * .5f + (rect.GetCenter().X - _drawnCamera.X) * (grow > 1 ? 0 : 1),
                rect.End.Y - size.Y), size);
            yield break;
        }
        var step = rect.Size.X;
        var first = Mathf.Floor((_drawnCamera.X - halfWidth - rect.Position.X) / step);
        var last = Mathf.Ceil((_drawnCamera.X + halfWidth - rect.Position.X) / step);
        for (var k = first; k <= last; k++)
            yield return new Rect2(rect.Position + new Vector2(k * step, 0), new Vector2(step + .5f, rect.Size.Y));
    }
}
