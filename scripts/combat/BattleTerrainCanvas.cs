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
        foreach (var layer in Layers.Layers)
        {
            if (layer.Front != FrontLayers) continue;
            var offset = new Vector2((1 - layer.Parallax) * (_drawnCamera.X - WorldCentreX), 0);
            var rect = new Rect2(layer.Rect.Position + offset, layer.Rect.Size);
            if (!layer.Tile) { DrawTextureRect(layer.Texture, rect, false); continue; }
            var step = rect.Size.X;
            var first = Mathf.Floor((_drawnCamera.X - halfWidth - rect.Position.X) / step);
            var last = Mathf.Ceil((_drawnCamera.X + halfWidth - rect.Position.X) / step);
            for (var k = first; k <= last; k++)
                DrawTextureRect(layer.Texture, new Rect2(rect.Position + new Vector2(k * step, 0), new Vector2(step + .5f, rect.Size.Y)), false);
        }
    }
}
