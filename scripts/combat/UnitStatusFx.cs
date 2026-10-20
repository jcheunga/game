using System;
using Godot;

// The light and motion a status throws off a unit, on top of the painted marks Unit.StatusVisuals draws on the
// sprite itself (flames, brambles or chains, the shield ring, stars, the silence rune): a burning soldier glows
// and sheds rising flame, a sleeper breathes out drifting z's, and the battle-mad smoulder with embers at their
// feet. A separate canvas item, so the light is additive and not graded by the unit's UnitLook shader. Made the
// first time a unit needs it and kept for the unit's life (pooled with it).
public partial class UnitStatusFx : Node2D
{
    public Unit Owner;
    private Layer _glow;
    private CpuParticles2D _flames, _embers;
    private float _age, _sleepAge;
    private static Texture2D _flame, _ember;

    private static readonly Color Sleep = new("c9d6ff");

    /// <summary>A second canvas item under the same node, drawn additively (light), while the node itself draws
    /// solid things (the z's) with normal blending.</summary>
    private sealed partial class Layer : Node2D
    {
        public Action<Layer> Painter;
        public override void _Draw() => Painter?.Invoke(this);
    }

    public static bool Wants(Unit unit) => unit.IsBurning || unit.IsAsleep || unit.IsFrenzied;

    public override void _Ready()
    {
        _flame ??= SpellVfx.Tex("p_flame");
        _ember ??= SpellVfx.Tex("p_ember");
        _glow = new Layer { Painter = DrawGlow, Material = SpellVfx.Additive };
        AddChild(_glow);
        _flames = Particles(_flame, 12, .55f, new Color("ffb347"), new Color("e0401c"));
        _embers = Particles(_ember, 5, .9f, new Color("ffcf7a"), new Color("d2452a"));
        _glow.AddChild(_flames);
        _glow.AddChild(_embers);
    }

    private static CpuParticles2D Particles(Texture2D texture, int amount, float lifetime, Color hot, Color cool) => new()
    {
        Amount = amount, Lifetime = lifetime, Emitting = false, LocalCoords = false, Texture = texture,
        EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle, Direction = Vector2.Up, Spread = 12f,
        Gravity = new Vector2(0, -70f), InitialVelocityMin = 8f, InitialVelocityMax = 22f,
        AngleMin = -20f, AngleMax = 20f,
        Material = SpellVfx.Additive,
        ColorRamp = new Gradient { Offsets = new[] { 0f, .2f, .6f, 1f },
            Colors = new[] { new Color(hot, 0f), new Color(hot, .9f), new Color(cool, .55f), new Color(cool, 0f) } }
    };

    public override void _Process(double delta)
    {
        var u = Owner;
        if (u == null || !IsInstanceValid(u)) { QueueFree(); return; }
        var on = !u.IsDead && Wants(u);
        Visible = on;
        _flames.Emitting = on && u.IsBurning && !SpellVfx.Reduced;
        _embers.Emitting = on && u.IsFrenzied && !u.IsBurning && !SpellVfx.Reduced;
        if (!on) { _sleepAge = 0f; return; }
        var dt = (float)delta;
        _age += dt;
        _sleepAge = u.IsAsleep ? _sleepAge + dt : 0f;
        Modulate = new Color(1, 1, 1, u.IsConcealed ? .42f : 1f);

        var size = u.FigureSize;
        var scale = Mathf.Clamp(size.X / 40f, .6f, 2.2f);
        PlaceEmitter(_flames, u.GlobalPosition + new Vector2(0, -size.Y * .45f), new Vector2(size.X * .3f, size.Y * .3f), .1f * scale, .22f * scale);
        PlaceEmitter(_embers, u.GlobalPosition + new Vector2(0, -size.Y * .12f), new Vector2(size.X * .35f, size.Y * .1f),
            .05f * scale, .1f * scale);
        QueueRedraw();
        _glow.QueueRedraw();
    }

    private static void PlaceEmitter(CpuParticles2D p, Vector2 at, Vector2 extents, float min, float max)
    {
        p.GlobalPosition = at;
        p.EmissionRectExtents = extents;
        p.ScaleAmountMin = min;
        p.ScaleAmountMax = max;
    }

    // Solid layer: z's drifting off a sleeper.
    public override void _Draw()
    {
        var u = Owner;
        if (u == null || u.IsDead || !u.IsAsleep) return;
        var size = u.FigureSize;
        var head = new Vector2(size.X * .2f, -size.Y * .95f);
        for (var i = 0; i < 3; i++)
        {
            var t = (_sleepAge * .55f + i / 3f) % 1f;
            var at = head + new Vector2(t * 14f + Mathf.Sin(t * 6f + i) * 2f, -t * 22f);
            var s = 2f + 2.6f * t;
            var c = new Color(Sleep, Mathf.Sin(Mathf.Pi * t) * .95f);
            var z = new[] { at + new Vector2(-s, -s), at + new Vector2(s, -s), at + new Vector2(-s, s), at + new Vector2(s, s) };
            DrawPolyline(z, new Color(.08f, .1f, .18f, c.A * .6f), 3.2f, true);
            DrawPolyline(z, c, 1.6f, true);
        }
    }

    // Light layer: the glow of a burning body, and the warm smoulder at a frenzied fighter's feet.
    private void DrawGlow(Layer layer)
    {
        var u = Owner;
        if (u == null || u.IsDead) return;
        var size = u.FigureSize;
        var glow = SpellVfx.Glow;
        if (u.IsBurning)
        {
            var r = size.X * (.75f + .08f * Mathf.Sin(_age * 17f) + .05f * Mathf.Sin(_age * 29f));
            var c = new Vector2(0, -size.Y * .45f);
            layer.DrawTextureRect(glow, new Rect2(c - new Vector2(r, r * 1.2f), new Vector2(r * 2, r * 2.4f)), false, new Color(1f, .45f, .15f, .45f));
        }
        else if (u.IsFrenzied)
        {
            var r = size.X * .7f;
            var feet = new Vector2(0, -2);
            layer.DrawTextureRect(glow, new Rect2(feet - new Vector2(r, r * .35f), new Vector2(r * 2, r * .7f)), false,
                new Color(1f, .5f, .2f, .22f + .08f * Mathf.Sin(_age * 5f)));
        }
    }
}
