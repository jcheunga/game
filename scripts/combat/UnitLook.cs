using System.Collections.Generic;
using Godot;

// How a painted unit sits in its battlefield's light, as one canvas shader on every unit and body:
//  - the zone's key light and ambient (measured from its painted backdrop: sky, horizon and road colours) grade the
//    painting, so troops in the night forest go cool and dark and troops in the foundry glow orange;
//  - a rim of sky light along the edges on the side away from the sun and a warm bounce off the road on the feet,
//    so the figure is lit by the same scene as the backdrop rather than pasted over it;
//  - a little haze toward the horizon colour for units further up the road;
//  - a true white hit flash (a multiply can only tint, never whiten).
public static class UnitLook
{
    public readonly record struct Zone(Color Key, Color Ambient, Color Rim, Color Bounce, Color Haze, float Exposure);

    // From art: assets/world/royal/<zone>_{far,ground}.png averages and the zone's sky colour (see the measuring note
    // in art/remaster/README.md). Key = light on the troops; Ambient = the shadow side; Rim = sky; Bounce = road.
    private static readonly Dictionary<string, Zone> Zones = new()
    {
        ["city"] = new(new Color(1.06f, 1.0f, .9f), new Color(.62f, .64f, .72f), new Color(.72f, .82f, .98f), new Color(.82f, .66f, .36f), new Color(.68f, .68f, .7f), 1f),
        ["harbor"] = new(new Color(.98f, 1.0f, 1.0f), new Color(.56f, .62f, .66f), new Color(.66f, .8f, .9f), new Color(.66f, .56f, .38f), new Color(.57f, .62f, .63f), .96f),
        ["foundry"] = new(new Color(1.12f, .82f, .62f), new Color(.6f, .34f, .26f), new Color(1f, .5f, .26f), new Color(.86f, .44f, .18f), new Color(.62f, .21f, .09f), .92f),
        ["quarantine"] = new(new Color(.9f, .95f, .82f), new Color(.5f, .52f, .45f), new Color(.66f, .7f, .55f), new Color(.6f, .5f, .34f), new Color(.43f, .43f, .35f), .9f),
        ["thornwall"] = new(new Color(1.0f, 1.02f, .96f), new Color(.6f, .66f, .74f), new Color(.66f, .82f, 1f), new Color(.8f, .72f, .62f), new Color(.53f, .66f, .84f), 1f),
        ["basilica"] = new(new Color(.92f, .86f, 1.0f), new Color(.46f, .4f, .58f), new Color(.66f, .5f, .9f), new Color(.66f, .54f, .5f), new Color(.42f, .25f, .45f), .9f),
        ["mire"] = new(new Color(.86f, .9f, .74f), new Color(.42f, .44f, .34f), new Color(.6f, .66f, .52f), new Color(.5f, .44f, .2f), new Color(.46f, .41f, .26f), .86f),
        ["steppe"] = new(new Color(1.12f, .92f, .78f), new Color(.66f, .48f, .46f), new Color(1f, .62f, .52f), new Color(.92f, .6f, .3f), new Color(.78f, .4f, .27f), 1f),
        ["gloamwood"] = new(new Color(.66f, .78f, .92f), new Color(.3f, .4f, .5f), new Color(.4f, .66f, .9f), new Color(.36f, .42f, .3f), new Color(.14f, .27f, .37f), .82f),
        ["citadel"] = new(new Color(1.0f, .78f, .74f), new Color(.5f, .32f, .3f), new Color(.9f, .38f, .32f), new Color(.6f, .42f, .28f), new Color(.43f, .14f, .11f), .88f),
    };

    public static Zone For(string zone) => Zones.TryGetValue(zone ?? "", out var z) ? z : Zones["city"];

    private static Shader _shader;

    private const string Code = @"
shader_type canvas_item;
uniform vec4 region = vec4(0.0, 0.0, 1.0, 1.0);
uniform vec2 texel = vec2(0.001, 0.001);
uniform vec3 key_light = vec3(1.0);
uniform vec3 ambient = vec3(0.6);
uniform vec3 rim_color = vec3(0.8, 0.9, 1.0);
uniform vec3 bounce = vec3(0.8, 0.6, 0.4);
uniform vec3 haze_color = vec3(0.7);
uniform float exposure = 1.0;
uniform float haze = 0.0;
uniform float flash = 0.0;
uniform float facing = 1.0;      // the sun is on the left of the screen: the rim falls on the right edges
uniform float fade = 1.0;
uniform float raised = 0.0;      // raised from the dead: drained to grave-green, rimmed with soul-light


void fragment() {
    vec4 c = texture(TEXTURE, UV);
    vec2 local = (UV - region.xy) / max(region.zw, vec2(1e-5));
    // sun side to shadow side: a soft gradient across the figure (the paintings are lit from the upper left)
    float lum = dot(c.rgb, vec3(0.3, 0.59, 0.11));
    vec3 lit = c.rgb * mix(ambient, key_light, smoothstep(0.05, 0.75, lum + 0.25));
    // rim: alpha falls off toward the edge facing away from the sun
    float s = 3.0;
    vec2 away = vec2(facing * texel.x * s, -texel.y * s * 0.5);
    // only solid paint takes the rim (a motion-blur smear or a soft edge is see-through, not lit from behind)
    float edge = smoothstep(0.85, 1.0, c.a) * (1.0 - smoothstep(0.0, 0.75, texture(TEXTURE, clamp(UV + away, region.xy, region.xy + region.zw)).a));
    lit += rim_color * edge * 0.55 * (1.0 - local.y * 0.6);
    // bounce: warm road light on the lowest part of the figure
    lit += bounce * c.rgb * smoothstep(0.62, 1.0, local.y) * 0.22;
    lit *= exposure;
    if (raised > 0.0) {
        float g = dot(lit, vec3(0.3, 0.59, 0.11));
        lit = mix(lit, vec3(g * 0.72, g * 1.02, g * 0.78) * 0.9, 0.55 * raised);
        lit += vec3(0.35, 1.0, 0.5) * edge * 0.9 * raised;
    }
    lit = mix(lit, haze_color, haze);
    lit = mix(lit, vec3(1.0), flash);
    COLOR = vec4(lit, c.a * fade) * vec4(COLOR.rgb, COLOR.a);
}";

    public static ShaderMaterial CreateMaterial(string zone)
    {
        _shader ??= new Shader { Code = Code };
        var m = new ShaderMaterial { Shader = _shader };
        Apply(m, For(zone));
        return m;
    }

    public static void Apply(ShaderMaterial m, Zone z)
    {
        m.SetShaderParameter("key_light", new Vector3(z.Key.R, z.Key.G, z.Key.B));
        m.SetShaderParameter("ambient", new Vector3(z.Ambient.R, z.Ambient.G, z.Ambient.B));
        m.SetShaderParameter("rim_color", new Vector3(z.Rim.R, z.Rim.G, z.Rim.B));
        m.SetShaderParameter("bounce", new Vector3(z.Bounce.R, z.Bounce.G, z.Bounce.B));
        m.SetShaderParameter("haze_color", new Vector3(z.Haze.R, z.Haze.G, z.Haze.B));
        m.SetShaderParameter("exposure", z.Exposure);
    }

    /// <summary>Per-draw values: the frame's region in the atlas (for the rim's edge lookups), the flash, facing,
    /// and how far up the road the unit stands (0 = front, 1 = back) for haze.</summary>
    public static void SetFrame(ShaderMaterial m, Texture2D tex, Rect2 src, float flash, float facing, float depth, float fade = 1f)
    {
        var size = tex.GetSize();
        m.SetShaderParameter("region", new Vector4(src.Position.X / size.X, src.Position.Y / size.Y, src.Size.X / size.X, src.Size.Y / size.Y));
        m.SetShaderParameter("texel", new Vector2(1f / size.X, 1f / size.Y));
        m.SetShaderParameter("flash", flash);
        m.SetShaderParameter("facing", facing < 0 ? -1f : 1f);
        m.SetShaderParameter("haze", Mathf.Clamp(depth, 0, 1) * .14f);
        m.SetShaderParameter("fade", fade);
    }
}
