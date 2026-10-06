using System;
using Godot;

/// <summary>
/// Draws the painted map's fog of war with assets/shaders/royal_fog.gdshader: the owning canvas supplies a
/// blurred mask of the hidden tiles in world space and the shader turns it into drifting storm cloud.
/// </summary>
public partial class MapFogLayer : Control
{
    private static Shader _shader;
    private static Texture2D _noise;
    private readonly ShaderMaterial _material = new();
    public Texture2D Mask;
    public Rect2 MaskRect;
    public Vector2 Offset;
    public float Zoom = 1;
    public float Clock;

    public MapFogLayer()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Linear;
        _shader ??= ResourceLoader.Load<Shader>("res://assets/shaders/royal_fog.gdshader");
        _noise ??= ResourceLoader.Load<Texture2D>("res://assets/world/royal/maps/fog-noise.png");
        _material.Shader = _shader;
        _material.SetShaderParameter("noise_tex", _noise);
        Material = _material;
    }

    public override void _Draw()
    {
        if (Mask == null) return;
        _material.SetShaderParameter("world_origin", MaskRect.Position);
        _material.SetShaderParameter("world_size", MaskRect.Size);
        _material.SetShaderParameter("clock", Clock);
        DrawSetTransform(Offset, 0, Vector2.One * Zoom);
        DrawTextureRect(Mask, MaskRect, false);
    }
}

/// <summary>A layer whose drawing is supplied by its owner, for art that must sit above a sibling layer.</summary>
public partial class MapOverlayLayer : Control
{
    public Action<Control> Paint;

    public MapOverlayLayer() { MouseFilter = MouseFilterEnum.Ignore; }

    public override void _Draw() => Paint?.Invoke(this);
}
