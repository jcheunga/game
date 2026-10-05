using Godot;

/// <summary>Lossless scene art and world-sized fine ground detail, behind the simulation.</summary>
public partial class BattleTerrainCanvas : Node2D
{
    public Texture2D Artwork;
    public Rect2 Ground;
    public System.Func<Rect2> Cover;
    // Zone backdrops are layered and rendered for exact battle-world rects; painted plates cover the view instead.
    public ZoneBackdrop Layers;
    // Layers scroll relative to the camera's position when centred on the field.
    public float WorldCentreX;
    private Vector2 _drawnCamera = new(float.NaN, float.NaN);
    public Color Backdrop;
    private Polygon2D _detail;
    private ShaderMaterial _detailMaterial;
    private Shader _detailShader;
    private static readonly Texture2D[] Materials = new Texture2D[9];
    public const string MaterialPath = "res://assets/world/battles/polished-v2/ground-materials.png";

    public static Texture2D GroundMaterial(string zone)
    {
        var index = zone switch { "gloamwood" or "quarantine" => 1, "steppe" => 2, "harbor" => 3,
            "mire" => 4, "foundry" => 5, "thornwall" => 6, "basilica" => 7, "citadel" => 8, _ => 0 };
        if (Materials[index] != null) return Materials[index];
        if (!ResourceLoader.Exists(MaterialPath)) return null;
        using var source = ResourceLoader.Load<Texture2D>(MaterialPath).GetImage();
        if (source.IsCompressed()) source.Decompress();
        var cell = source.GetWidth() / 3;
        using var pixels = source.GetRegion(new Rect2I(index % 3 * cell, index / 3 * cell, cell, cell));
        pixels.GenerateMipmaps();
        return Materials[index] = ImageTexture.CreateFromImage(pixels);
    }

    public void AddGroundDetail(string zone, int stage)
    {
        var texture = GroundMaterial(zone);
        if (texture == null) return;
        _detailShader = new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D detail : source_color, filter_linear_mipmap, repeat_enable;
uniform vec2 ground_size;
uniform vec2 variation;
// Detail grain follows the plate, which is fitted to the band height (488 world rows at 1.0).
uniform float grain = 1.0;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
vec3 patch(vec2 p, vec2 cell) {
    float angle = hash(cell) * 6.283185;
    mat2 rotation = mat2(vec2(cos(angle), sin(angle)), vec2(-sin(angle), cos(angle)));
    vec2 offset = vec2(hash(cell + 7.0), hash(cell + 19.0));
    return texture(detail, rotation * p + offset).rgb;
}
void fragment() {
    vec2 ground_uv = UV * ground_size;
    vec2 p = (ground_uv + variation) / (vec2(420.0, 280.0) * grain);
    vec2 cell = floor(p * 0.65);
    vec2 blend = smoothstep(vec2(0.0), vec2(1.0), fract(p * 0.65));
    vec3 material = mix(mix(patch(p, cell), patch(p, cell + vec2(1.0, 0.0)), blend.x),
        mix(patch(p, cell + vec2(0.0, 1.0)), patch(p, cell + vec2(1.0, 1.0)), blend.x), blend.y);
    vec2 edges = min(ground_uv, ground_size - ground_uv);
    float feather = smoothstep(0.0, 92.0 * grain, min(edges.x, edges.y));
    COLOR = vec4(material, 0.68 * feather);
}" };
        _detailMaterial = new ShaderMaterial { Shader = _detailShader };
        _detailMaterial.SetShaderParameter("detail", texture); _detailMaterial.SetShaderParameter("ground_size", Ground.Size);
        _detailMaterial.SetShaderParameter("variation", new Vector2(stage * 37 % 420, stage * 61 % 280));
        _detailMaterial.SetShaderParameter("grain", Ground.Size.Y / 488f);
        var texSize = texture.GetSize();
        _detail = new Polygon2D { Texture = texture, Polygon = new[] { Ground.Position, Ground.Position + new Vector2(Ground.Size.X, 0), Ground.End,
            Ground.Position + new Vector2(0, Ground.Size.Y) }, UV = new[] { Vector2.Zero, new Vector2(texSize.X, 0), texSize,
            new Vector2(0, texSize.Y) }, Material = _detailMaterial };
        AddChild(_detail);
    }

    public override void _ExitTree()
    {
        Artwork = null;
        if (_detail != null) { _detail.Material = null; _detail.Texture = null; }
        if (_detailMaterial != null) { _detailMaterial.Shader = null; _detailMaterial.Dispose(); _detailMaterial = null; }
        _detailShader?.Dispose(); _detailShader = null;
    }

    private Vector2 CameraCentre => GetViewport()?.GetCamera2D() is { } camera ? camera.GetScreenCenterPosition() : new Vector2(WorldCentreX, 0);

    public override void _Process(double delta)
    {
        // Far layers move with the camera, so they redraw whenever it does.
        if (Layers != null && CameraCentre != _drawnCamera) QueueRedraw();
    }

    public override void _Draw()
    {
        DrawSetTransformMatrix(GetGlobalTransformWithCanvas().AffineInverse());
        DrawRect(GetViewportRect(), Layers?.Sky ?? Backdrop);
        DrawSetTransform(Vector2.Zero);
        if (Layers != null)
        {
            _drawnCamera = CameraCentre;
            foreach (var layer in Layers.Layers)
            {
                var offset = new Vector2((1 - layer.Parallax) * (_drawnCamera.X - WorldCentreX), 0);
                DrawTextureRect(layer.Texture, new Rect2(layer.Rect.Position + offset, layer.Rect.Size), false);
            }
        }
        else WorldEnvironmentArt.DrawBattle(this, Artwork, Cover?.Invoke() ?? Ground);
    }
}
