using Godot;

// How a fallen unit leaves the field: how long the body rests, and the colour and shape of
// its dissolve. The family (Fx) is authored with each unit's death clip in
// art/remaster/roster/deaths.py. Lantern Caravan dead go out as warm lantern light; the
// Rotbound Host burns away to ash, scatters as soul-light, rots into gas, and so on.
public sealed class DeathStyle
{
    public string Fx = "dust";
    public bool PlayerSide;
    public bool Boss;
    public float Radius = 14f;
    public Color Edge = new(1f, .78f, .38f, .9f);
    public Color Mote = new(1f, .82f, .45f);
    public float EdgeWidth = .09f;
    public float Rise = .45f;        // 0 = even dissolve, 1 = top first (ash carried upward)
    public float Desaturate = .25f;  // drained of colour while the body rests
    public float Darken;
    public float Linger = 1.1f;
    public float Dissolve = .75f;

    public bool Heavy => Boss || Radius >= 20f;

    public static DeathStyle For(string fx, bool playerSide, bool boss, float radius)
    {
        var s = new DeathStyle { Fx = string.IsNullOrWhiteSpace(fx) ? (playerSide ? "dust" : "ash") : fx, PlayerSide = playerSide,
            Boss = boss, Radius = radius };
        if (playerSide)
        {
            // Every Caravan soldier returns to the lantern; the family only tints the light.
            s.Edge = new Color(1f, .78f, .38f, .9f);
            s.Mote = new Color(1f, .82f, .45f);
            s.Rise = .4f;
            switch (s.Fx)
            {
                case "arcane": s.Edge = new Color(.78f, .58f, 1f, .9f); s.Mote = new Color(.85f, .7f, 1f); break;
                case "storm": s.Edge = new Color(.62f, .86f, 1f, .95f); s.Mote = new Color(.75f, .92f, 1f); break;
                case "holy": s.Edge = new Color(1f, .9f, .58f, .95f); s.Mote = new Color(1f, .94f, .7f); break;
                case "alchemy": s.Edge = new Color(.55f, 1f, .62f, .9f); s.Mote = new Color(.7f, 1f, .72f); break;
                case "bones": s.Edge = new Color(.6f, 1f, .92f, .9f); s.Mote = new Color(.75f, 1f, .95f); s.Rise = .6f; break;
            }
        }
        else
        {
            switch (s.Fx)
            {
                case "bones": s.Edge = new Color(.55f, 1f, .9f, .95f); s.Mote = new Color(.72f, 1f, .94f); s.Rise = .6f; s.Desaturate = .35f; break;
                case "gas": s.Edge = new Color(.72f, 1f, .3f, .9f); s.Mote = new Color(.62f, .82f, .3f); s.Rise = .3f; s.Desaturate = .2f; break;
                case "embers": s.Edge = new Color(1f, .55f, .18f, 1f); s.Mote = new Color(1f, .62f, .25f); s.Rise = .8f; s.Desaturate = .3f; s.Darken = .2f; break;
                case "water": s.Edge = new Color(.62f, .86f, 1f, .85f); s.Mote = new Color(.7f, .9f, 1f); s.Rise = .15f; s.Desaturate = .2f; break;
                case "arcane": s.Edge = new Color(.72f, .45f, 1f, .95f); s.Mote = new Color(.78f, .6f, 1f); s.Rise = .55f; break;
                case "holy": s.Edge = new Color(1f, .86f, .5f, .95f); s.Mote = new Color(1f, .92f, .65f); s.Rise = .65f; break;
                case "glass": s.Edge = new Color(.9f, .96f, 1f, 1f); s.Mote = new Color(.92f, .97f, 1f); s.Rise = .3f; s.EdgeWidth = .05f; break;
                case "debris": s.Edge = new Color(0, 0, 0, 0); s.Mote = new Color(.45f, .42f, .38f); s.Rise = .5f; s.Darken = .3f; s.Desaturate = .3f; break;
                default: s.Edge = new Color(1f, .45f, .15f, .85f); s.Mote = new Color(.28f, .26f, .25f); s.Rise = .75f; s.Desaturate = .45f; break; // ash, dust
            }
        }
        if (boss)
        {
            s.Linger = 2.2f;
            s.Dissolve = 1.5f;
            s.EdgeWidth *= 1.25f;
        }
        else if (s.Heavy)
        {
            s.Linger = 1.35f;
            s.Dissolve = .9f;
        }
        return s;
    }
}

public static class DeathDissolve
{
    private static Shader _shader;
    private static Texture2D _noise;

    private const string Code = @"
shader_type canvas_item;
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform float rest : hint_range(0.0, 1.0) = 0.0;
uniform vec4 region = vec4(0.0, 0.0, 1.0, 1.0);
uniform sampler2D noise : repeat_enable, filter_linear;
uniform vec4 edge_color : source_color = vec4(1.0, 0.7, 0.3, 1.0);
uniform float edge_width = 0.09;
uniform float rise = 0.45;
uniform float desaturate = 0.25;
uniform float darken = 0.0;

void fragment() {
    vec4 c = COLOR; // already texture * modulate in Godot 4
    vec2 local = (UV - region.xy) / max(region.zw, vec2(1e-5));
    float n = texture(noise, local * 2.2).r * 0.7 + texture(noise, local * 6.1 + vec2(0.37, 0.71)).r * 0.3;
    float h = mix(n, local.y * 0.62 + n * 0.38, rise);
    float t = progress * (1.0 + edge_width * 2.0) - edge_width;
    float keep = smoothstep(t, t + 0.025, h);
    float edge = (1.0 - smoothstep(t + 0.01, t + edge_width, h)) * keep * step(0.001, progress);
    float grey = dot(c.rgb, vec3(0.3, 0.59, 0.11));
    c.rgb = mix(c.rgb, vec3(grey), desaturate * rest) * (1.0 - darken * rest);
    c.rgb = mix(c.rgb, edge_color.rgb * 1.45, edge * edge_color.a * 0.9);
    c.a *= keep;
    COLOR = c;
}";

    public static ShaderMaterial CreateMaterial(DeathStyle style)
    {
        _shader ??= new Shader { Code = Code };
        var m = new ShaderMaterial { Shader = _shader };
        m.SetShaderParameter("noise", Noise);
        m.SetShaderParameter("edge_color", style.Edge);
        m.SetShaderParameter("edge_width", style.EdgeWidth);
        m.SetShaderParameter("rise", style.Rise);
        m.SetShaderParameter("desaturate", style.Desaturate);
        m.SetShaderParameter("darken", style.Darken);
        return m;
    }

    // Deterministic, generated once on the main thread (NoiseTexture2D builds asynchronously).
    private static Texture2D Noise
    {
        get
        {
            if (_noise != null) return _noise;
            const int size = 128;
            var noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.045f, Seed = 7,
                FractalOctaves = 3 };
            var image = Image.CreateEmpty(size, size, false, Image.Format.L8);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    // Tile seamlessly by blending the four wrapped samples.
                    float u = x / (float)size, v = y / (float)size;
                    var value = noise.GetNoise2D(x, y) * (1 - u) * (1 - v) + noise.GetNoise2D(x - size, y) * u * (1 - v)
                        + noise.GetNoise2D(x, y - size) * (1 - u) * v + noise.GetNoise2D(x - size, y - size) * u * v;
                    var g = Mathf.Clamp(value * .5f + .5f, 0f, 1f);
                    image.SetPixel(x, y, new Color(g, g, g));
                }
            return _noise = ImageTexture.CreateFromImage(image);
        }
    }
}
