using System.Collections.Generic;
using Godot;

// Light blood and the stuff a blow knocks loose: a spray from the wound in the direction the blow travelled,
// droplets that arc down under gravity and leave small stains where they land, bone chips off skeletons and
// splinters off wood and shields, and a pool that spreads under a fallen body. The sprites are painted in the
// roster sheet's style (art/remaster/fx_cut.py -> assets/fx/); the colour comes from what the creature is made of.
// Stains are flat on the ground layer, fade out after a while, and are capped so a long fight never fills the
// field. Reduced motion keeps the stains but drops the flying droplets.
public static class BattleGore
{
    public enum Body { Flesh, Undead, Bone, Plague, Water, Ember, Spirit, Wood, Stone }

    private const int MaxStains = 90;
    private static readonly Queue<GoreStain> Stains = new();
    private static readonly Dictionary<string, Texture2D> Textures = new();

    public static Texture2D Tex(string id)
    {
        if (Textures.TryGetValue(id, out var t)) return t;
        var path = $"res://assets/fx/{id}.png";
        return Textures[id] = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }

    /// <summary>What the creature bleeds, from its id and look: the living bleed red, the risen dark and thick,
    /// skeletons shed bone, plague things ooze green, drowned things water, furnace things embers, spirits light.</summary>
    public static Body BodyOf(Unit unit)
    {
        var id = unit.DefinitionId ?? "";
        var cls = unit.VisualClass ?? "";
        if (id.Contains("wisp") || id.Contains("shade") || id.Contains("eye") || id.Contains("wraith") || cls == "mirror")
            return Body.Spirit;
        if (id.Contains("siegetower") || id.Contains("ballista") || id.Contains("wicker") || cls == "siegetower")
            return Body.Wood;
        if (id.Contains("plague") || id.Contains("bloat") || id.Contains("spore") || id.Contains("miasma") || id.Contains("leech")
            || id.Contains("maw") || id.Contains("blight") || cls == "bloater" || cls == "spitter")
            return Body.Plague;
        if (id.Contains("drowned") || id.Contains("eel") || id.Contains("barnacle") || id.Contains("tide") || id.Contains("docks")
            || id.Contains("salt"))
            return Body.Water;
        if (id.Contains("furnace") || id.Contains("cinder") || id.Contains("ash") || id.Contains("forge"))
            return Body.Ember;
        if (id.Contains("bone") || id.Contains("lich") || id.Contains("skeleton") || id.Contains("ossuary") || id.Contains("cantor")
            || id.Contains("crusher") || id.Contains("splitter") || id.Contains("rustguard") || id.Contains("shieldwall"))
            return Body.Bone;
        if (unit.Team == Team.Enemy) return Body.Undead;
        return Body.Flesh;
    }

    public static Color BloodColor(Body body) => body switch
    {
        Body.Flesh => new Color(.62f, .05f, .06f),
        Body.Undead => new Color(.36f, .06f, .07f),
        Body.Bone => new Color(.82f, .76f, .62f),
        Body.Plague => new Color(.48f, .7f, .12f),
        Body.Water => new Color(.42f, .62f, .72f),
        Body.Ember => new Color(1f, .5f, .14f),
        Body.Spirit => new Color(.6f, .95f, .9f),
        Body.Wood => new Color(.5f, .34f, .2f),
        _ => new Color(.5f, .48f, .45f)
    };

    private static bool Bleeds(Body body) => body is Body.Flesh or Body.Undead or Body.Plague or Body.Water;
    private static bool Reduced => GameState.Instance?.ReducedMotion ?? false;

    /// <summary>A hit: spray from the wound along the blow, droplets that fall and stain, chips off bone and
    /// wood. `strength` ~ damage relative to the target's toughness (0..1+).</summary>
    public static void SpawnHit(Node parent, Unit target, Vector2 wound, float direction, float strength, float groundY,
        Color fieldTint)
    {
        var body = BodyOf(target);
        var color = BloodColor(body) * fieldTint;
        var scale = Mathf.Clamp(target.Radius / 14f, .7f, 2.2f);
        var amount = Mathf.Clamp(strength, .25f, 1.4f);
        if (Bleeds(body))
        {
            if (!Reduced)
            {
                var spray = Tex($"spray_{1 + (int)(GD.Randi() % 4)}");
                if (spray != null) parent.AddChild(new GoreSpray(spray, wound, direction, color, 14f * scale * (.7f + .5f * amount)));
                var drops = Mathf.RoundToInt(3 + 6 * amount);
                for (var i = 0; i < drops; i++)
                    parent.AddChild(new GoreDroplet(wound, direction, color, scale, groundY, fieldTint, body));
            }
            else
            {
                AddStain(parent, new Vector2(wound.X + direction * 6f, groundY), color, scale * .6f);
            }
        }
        else if (body is Body.Bone or Body.Wood)
        {
            var count = Mathf.RoundToInt(1 + 1.6f * amount);
            for (var i = 0; i < count && !Reduced; i++)
                parent.AddChild(new GoreChip(Tex(body == Body.Bone ? $"bone_{1 + i % 2}" : $"splinter_{1 + i % 2}"), wound,
                    direction, scale, groundY, fieldTint));
        }
        else if (body is Body.Ember && !Reduced)
        {
            BattleParticles.SpawnImpactSparks(parent, wound, new Color(1f, .55f, .2f), 14f * amount);
        }
        else if (body is Body.Spirit && !Reduced)
        {
            BattleParticles.SpawnImpactSparks(parent, wound, color, 10f * amount);
        }
    }

    /// <summary>A pool that spreads out from under a fallen body over a second or so.</summary>
    public static void SpawnPool(Node parent, Vector2 at, Unit unit, float length, Color fieldTint)
    {
        var body = BodyOf(unit);
        if (!Bleeds(body)) return;
        var stain = AddStain(parent, at, BloodColor(body) * fieldTint, Mathf.Clamp(length / 40f, .7f, 3f));
        stain?.Spread(1.1f);
    }

    internal static GoreStain AddStain(Node parent, Vector2 at, Color color, float scale)
    {
        var tex = Tex($"stain_{1 + (int)(GD.Randi() % 4)}");
        if (tex == null) return null;
        while (Stains.Count >= MaxStains)
        {
            var old = Stains.Dequeue();
            if (GodotObject.IsInstanceValid(old)) old.FadeSoon();
        }
        var stain = new GoreStain(tex, color, scale);
        parent.AddChild(stain);
        stain.GlobalPosition = at;
        Stains.Enqueue(stain);
        return stain;
    }

    public static void Clear() => Stains.Clear();
}

/// <summary>A flat stain on the ground under the fighting, fading out after a while.</summary>
public partial class GoreStain : Node2D
{
    private readonly Texture2D _tex;
    private readonly Color _color;
    private readonly float _scale, _flip;
    private float _age, _life = 14f, _grow = 0f, _growFor;

    public GoreStain(Texture2D tex, Color color, float scale)
    {
        _tex = tex; _color = color; _scale = scale; _flip = GD.Randf() < .5f ? -1 : 1;
        ZIndex = -4;                 // under the troops, over the road and the shadows
        ZAsRelative = false;
    }

    public void Spread(float seconds) { _growFor = seconds; _grow = 0f; }
    public void FadeSoon() => _life = Mathf.Min(_life, _age + .6f);

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_growFor > 0) _grow = Mathf.Min(1f, _grow + (float)delta / _growFor);
        if (_age >= _life) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_tex == null) return;
        var g = _growFor > 0 ? 1 - Mathf.Pow(1 - _grow, 3) : 1f;
        var size = _tex.GetSize() * (.075f * _scale) * new Vector2(1f, .55f) * Mathf.Lerp(.3f, 1f, g);
        var fade = Mathf.Clamp((_life - _age) / 2.5f, 0, 1) * Mathf.Clamp(_age / .08f, 0, 1);
        DrawSetTransform(Vector2.Zero, 0, new Vector2(_flip, 1));
        DrawTextureRect(_tex, new Rect2(-size / 2, size), false, new Color(_color, .82f * fade));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}

/// <summary>The spray off a wound: a painted streak of droplets thrown along the blow, opening out and fading.</summary>
public partial class GoreSpray : Node2D
{
    private readonly Texture2D _tex;
    private readonly float _dir, _len;
    private readonly Color _color;
    private float _age;
    private const float Life = .22f;

    public GoreSpray(Texture2D tex, Vector2 at, float dir, Color color, float length)
    {
        _tex = tex; _dir = dir; _color = color; _len = length;
        Position = at; ZIndex = 30; ZAsRelative = false;
        Rotation = (GD.Randf() - .5f) * .7f - .15f * dir;
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= Life) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var t = _age / Life;
        var open = 1 - Mathf.Pow(1 - t, 3);
        var w = _len * Mathf.Lerp(.45f, 1.25f, open);
        var size = new Vector2(w, w * _tex.GetHeight() / _tex.GetWidth());
        DrawSetTransform(Vector2.Zero, 0, new Vector2(_dir, 1));
        DrawTextureRect(_tex, new Rect2(new Vector2(-size.X * .12f, -size.Y * .5f), size), false,
            new Color(_color, Mathf.Clamp((1 - t) * 1.6f, 0, 1)));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}

/// <summary>A single droplet: thrown from the wound, falls under gravity, and leaves a small stain where it lands.</summary>
public partial class GoreDroplet : Node2D
{
    private Vector2 _vel;
    private readonly float _ground, _size;
    private readonly Color _color, _tint;
    private readonly Texture2D _tex;
    private readonly bool _stains;
    private float _age;

    public GoreDroplet(Vector2 at, float dir, Color color, float scale, float groundY, Color tint, BattleGore.Body body)
    {
        Position = at; ZIndex = 30; ZAsRelative = false;
        _color = color; _tint = tint;
        _ground = groundY + (GD.Randf() - .5f) * 10f;
        _size = (.7f + GD.Randf() * 1.1f) * Mathf.Min(scale, 1.5f);
        _vel = new Vector2(dir * (30 + GD.Randf() * 90), -(30 + GD.Randf() * 70)) * Mathf.Sqrt(scale);
        _tex = BattleGore.Tex($"drop_{1 + (int)(GD.Randi() % 3)}");
        _stains = GD.Randf() < .45f;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _age += dt;
        _vel.Y += 420f * dt;
        Position += _vel * dt;
        if (Position.Y >= _ground && _vel.Y > 0)
        {
            if (_stains) BattleGore.AddStain(GetParent(), new Vector2(Position.X, _ground), _color, _size * .14f);
            QueueFree();
            return;
        }
        if (_age > 1.5f) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_tex == null) { DrawCircle(Vector2.Zero, _size * .5f, _color); return; }
        var stretch = Mathf.Clamp(_vel.Length() / 220f, 1f, 2f);
        DrawSetTransform(Vector2.Zero, _vel.Angle(), new Vector2(stretch, 1f / Mathf.Sqrt(stretch)));
        DrawTextureRect(_tex, new Rect2(-Vector2.One * _size * .5f, Vector2.One * _size), false, _color);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}

/// <summary>A bone chip or wood splinter knocked loose: tumbles, bounces once and lies a moment before fading.</summary>
public partial class GoreChip : Node2D
{
    private Vector2 _vel;
    private readonly float _ground, _size;
    private readonly Texture2D _tex;
    private readonly Color _tint;
    private float _age, _spin, _rest = -1;

    public GoreChip(Texture2D tex, Vector2 at, float dir, float scale, float groundY, Color tint)
    {
        _tex = tex; _tint = tint;
        Position = at; ZIndex = 30; ZAsRelative = false;
        _ground = groundY + (GD.Randf() - .5f) * 8f;
        _size = (2.6f + GD.Randf() * 2f) * Mathf.Min(scale, 1.3f);
        _vel = new Vector2(dir * (40 + GD.Randf() * 80), -(60 + GD.Randf() * 80));
        _spin = (GD.Randf() - .5f) * 24f;
        Rotation = GD.Randf() * Mathf.Tau;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _age += dt;
        if (_rest < 0)
        {
            _vel.Y += 520f * dt;
            Position += _vel * dt;
            Rotation += _spin * dt;
            if (Position.Y >= _ground && _vel.Y > 0)
            {
                Position = new Vector2(Position.X, _ground);
                if (Mathf.Abs(_vel.Y) > 90) { _vel = new Vector2(_vel.X * .4f, -_vel.Y * .3f); _spin *= .5f; }
                else { _rest = 0; ZIndex = -3; }
            }
        }
        else
        {
            _rest += dt;
            if (_rest > 1.6f) { QueueFree(); return; }
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_tex == null) return;
        var s = _tex.GetSize();
        var size = s * (_size / Mathf.Max(s.X, s.Y));
        var fade = _rest < 0 ? 1f : Mathf.Clamp((1.6f - _rest) / .5f, 0, 1);
        DrawTextureRect(_tex, new Rect2(-size / 2, size), false, new Color(_tint * .82f, fade));
    }
}
