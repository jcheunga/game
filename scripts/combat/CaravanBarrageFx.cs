using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// The look of the caravan spell's arcane missiles. Each missile leaves the war wagon's lantern on a wide fanned
/// arc, weaves a little as it homes on its target and bursts there, trailing a tapered ribbon of violet light and
/// shedding sparks; the lantern blazes while it fires and the field falls into a violet dusk so the light reads
/// on sunlit roads.
/// Colour is painted in normal blend (halos, ribbons, rings), because added light alone washes out to white over
/// the bright backdrops; only the hot cores, stars and flashes add light, from a child layer on top.
/// The controller launches missiles and steps their flight on the battle clock, so hits keep the simulation's
/// timing. Sparks, flashes and the lantern glow run on the frame clock. All of it freezes while the battle is
/// paused (the nodes are Pausable) and runs at the battle's speed.
/// </summary>
public partial class CaravanBarrageFx : Node2D
{
    public sealed class Missile
    {
        public Unit Target;
        public Vector2 Aim, Position;
        public float Hue, Size;
        internal Vector2 Start, Bend, Previous, Path;
        internal float Age, Duration, Phase;
        internal readonly Vector2[] Trail = new Vector2[TrailPoints];
        internal int TrailCount;
        internal float SparkDebt;
    }

    private struct Spark
    {
        public Vector2 Position, Velocity;
        public float Age, Life, Size, Angle, Spin;
        public Color Color;
        public bool Star;
    }

    private struct Burst
    {
        public Vector2 Position;
        public float Age, Life, Radius;
        public Color Color;
        public bool Ring;
    }

    private const int TrailPoints = 16;
    private static readonly Vector2[] NoUvs = Array.Empty<Vector2>();
    // Hot core, violet body, ice-blue edge and the deep violet the light falls off to (the painted icon's palette).
    public static readonly Color Core = new(1f, .95f, 1f), Body = new(.7f, .42f, 1f), Edge = new(.42f, .66f, 1f), Deep = new(.38f, .18f, .9f);

    private readonly List<Missile> _missiles = new();
    private readonly List<Spark> _sparks = new();
    private readonly List<Burst> _bursts = new();
    private readonly RandomNumberGenerator _rng = new();
    private Texture2D _star;
    private Node2D _light, _veil;
    private float _clock, _firing, _veilAmount, _lanternFlare;

    /// <summary>The lantern on the wagon roof that the missiles leave from.</summary>
    public Vector2 Emitter;
    /// <summary>Where a missile homing on a unit aims (its chest).</summary>
    public Func<Unit, Vector2> AimOf = unit => unit.Position;

    public IReadOnlyList<Missile> Missiles => _missiles;
    public bool Firing => _firing > 0f;
    public float VeilAmount => _veilAmount;

    public CaravanBarrageFx()
    {
        Name = "CaravanBarrage";
        ProcessMode = ProcessModeEnum.Pausable;
        ZIndex = SpellVfx.AirLayer;
        TextureFilter = TextureFilterEnum.Linear;
        _rng.Randomize();
    }

    public override void _Ready()
    {
        _star = SpellVfx.Tex("p_star");
        // The added light draws over the painted colour.
        _light = new Node2D { Name = "BarrageLight", Material = SpellVfx.Additive, ProcessMode = ProcessModeEnum.Pausable, TextureFilter = TextureFilterEnum.Linear };
        _light.Draw += DrawLight;
        AddChild(_light);
        // The veil dims the field under the missiles (troops included), but not the other spell light above them.
        _veil = new Node2D { Name = "BarrageVeil", ZAsRelative = false, ZIndex = SpellVfx.AirLayer - 2, ProcessMode = ProcessModeEnum.Pausable };
        _veil.Draw += DrawVeil;
        AddChild(_veil);
    }

    /// <summary>Keeps the lantern blazing (and the field dimmed) for this many more seconds.</summary>
    public void Ignite(float seconds)
    {
        _firing = Mathf.Max(_firing, seconds);
        _lanternFlare = 1f;
    }

    /// <summary>Launches one missile from the lantern towards a unit (or, with no target, a point on the ground).</summary>
    public Missile Launch(Unit target, Vector2 aim)
    {
        var missile = new Missile { Target = target, Aim = aim, Hue = _rng.Randf(), Size = _rng.RandfRange(.85f, 1.2f), Phase = _rng.RandfRange(0f, Mathf.Tau) };
        var start = Emitter + new Vector2(_rng.RandfRange(-5f, 5f), _rng.RandfRange(-4f, 3f));
        // A wide fireworks fan out of the lantern: high up and forward, some thrown back over the wagon before they turn.
        var angle = Mathf.DegToRad(_rng.RandfRange(-158f, -28f));
        Seed(missile, start, Vector2.FromAngle(angle) * _rng.RandfRange(130f, 270f));
        missile.Position = missile.Previous = start;
        _missiles.Add(missile);
        _lanternFlare = Mathf.Max(_lanternFlare, .6f);
        // A little muzzle spit at the lantern.
        for (var i = 0; i < (SpellVfx.Reduced ? 1 : 4); i++)
            AddSpark(start, Vector2.FromAngle(angle + _rng.RandfRange(-.5f, .5f)) * _rng.RandfRange(50f, 140f), _rng.RandfRange(.2f, .45f),
                _rng.RandfRange(4f, 8f), Tint(missile.Hue).Lerp(Core, .4f), true);
        return missile;
    }

    /// <summary>Points a missile at a new target from where it is now, bending out of its current heading.</summary>
    public void Redirect(Missile missile, Unit target, Vector2 aim)
    {
        missile.Target = target;
        missile.Aim = aim;
        var heading = missile.Position - missile.Previous;
        Seed(missile, missile.Position, (heading.LengthSquared() > .01f ? heading.Normalized() : Vector2.Up) * 70f);
        missile.Duration *= .7f;
    }

    private void Seed(Missile missile, Vector2 start, Vector2 bend)
    {
        missile.Start = start;
        missile.Bend = start + bend;
        missile.Path = start;
        missile.Age = 0f;
        // Far targets take longer, but even the stronghold is reached in about a second and a half.
        missile.Duration = .6f + Mathf.Min(1f, start.DistanceTo(missile.Aim) / 1000f) * .85f + _rng.RandfRange(0f, .18f);
    }

    /// <summary>Advances every missile on the battle clock. <paramref name="lost"/> is asked to retarget a missile
    /// whose target fell; <paramref name="arrive"/> runs as each one lands.</summary>
    public void Step(float delta, Action<Missile> lost, Action<Missile> arrive)
    {
        for (var i = _missiles.Count - 1; i >= 0; i--)
        {
            var missile = _missiles[i];
            if (missile.Target != null && (!IsInstanceValid(missile.Target) || missile.Target.IsDead))
            {
                missile.Target = null;
                lost?.Invoke(missile);
            }
            if (missile.Target != null) missile.Aim = AimOf(missile.Target);
            missile.Age += delta;
            var t = Mathf.Clamp(missile.Age / missile.Duration, 0f, 1f);
            // It eases off the lantern and accelerates into the strike.
            var u = t * t * (1.6f - .6f * t);
            var a = 1f - u;
            var path = a * a * missile.Start + 2f * a * u * missile.Bend + u * u * missile.Aim;
            // Arcane missiles weave as they fly, settling onto the line for the strike.
            var heading = path - missile.Path;
            var weave = heading.LengthSquared() > .0001f ? heading.Normalized().Orthogonal() * Mathf.Sin(missile.Age * 15f + missile.Phase) * 7f * (1f - t) : Vector2.Zero;
            missile.Path = path;
            missile.Previous = missile.Position;
            missile.Position = path + weave;
            PushTrail(missile);
            if (t < 1f) continue;
            _missiles.RemoveAt(i);
            arrive?.Invoke(missile);
            Impact(missile.Position, Tint(missile.Hue), missile.Target != null);
        }
    }

    private static void PushTrail(Missile missile)
    {
        var trail = missile.Trail;
        Array.Copy(trail, 0, trail, 1, TrailPoints - 1);
        trail[0] = missile.Position;
        missile.TrailCount = Math.Min(TrailPoints, missile.TrailCount + 1);
    }

    /// <summary>A missile's burst: a hot flash, a ring on the ground and a spray of sparks.</summary>
    public void Impact(Vector2 at, Color tint, bool onBody)
    {
        _bursts.Add(new Burst { Position = at, Life = .36f, Radius = onBody ? 34f : 28f, Color = tint });
        _bursts.Add(new Burst { Position = at + new Vector2(0, onBody ? 16f : 0f), Life = .45f, Radius = 30f, Color = tint, Ring = true });
        var count = SpellVfx.Reduced ? 2 : 10;
        for (var i = 0; i < count; i++)
        {
            var direction = Vector2.FromAngle(_rng.RandfRange(-Mathf.Pi, 0f) + _rng.RandfRange(-.3f, .3f));
            AddSpark(at, direction * _rng.RandfRange(60f, 220f), _rng.RandfRange(.25f, .6f), _rng.RandfRange(3f, 7f),
                i % 3 == 0 ? Core : tint, i % 2 == 0);
        }
    }

    private void AddSpark(Vector2 at, Vector2 velocity, float life, float size, Color color, bool star)
    {
        if (_sparks.Count > 900) return;
        _sparks.Add(new Spark { Position = at, Velocity = velocity, Life = life, Size = size, Color = color, Star = star,
            Angle = _rng.RandfRange(0f, Mathf.Tau), Spin = _rng.RandfRange(-6f, 6f) });
    }

    private static Color Tint(float hue) => Body.Lerp(Edge, hue);

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _clock += dt;
        _firing = Mathf.Max(0f, _firing - dt);
        _lanternFlare = Mathf.MoveToward(_lanternFlare, 0f, dt * 2.5f);
        _veilAmount = Mathf.MoveToward(_veilAmount, _firing > 0f || _missiles.Count > 0 ? 1f : 0f, dt * (_firing > 0f ? 3f : 1.2f));

        // Each missile sheds sparks along the stretch it flew since the last frame.
        var rate = SpellVfx.Reduced ? 4f : 24f;
        foreach (var missile in _missiles)
        {
            missile.SparkDebt += rate * dt;
            while (missile.SparkDebt >= 1f)
            {
                missile.SparkDebt -= 1f;
                var at = missile.Previous.Lerp(missile.Position, _rng.Randf());
                var drift = (missile.Position - missile.Previous).Normalized() * -30f + new Vector2(_rng.RandfRange(-25f, 25f), _rng.RandfRange(-30f, 15f));
                AddSpark(at, drift, _rng.RandfRange(.3f, .7f), _rng.RandfRange(2.5f, 5.5f) * missile.Size,
                    _rng.Randf() < .3f ? Core : Tint(missile.Hue), _rng.Randf() < .45f);
            }
        }
        if (_firing > 0f && !SpellVfx.Reduced && _rng.Randf() < dt * 30f)
            AddSpark(Emitter + new Vector2(_rng.RandfRange(-8f, 8f), _rng.RandfRange(-6f, 4f)),
                new Vector2(_rng.RandfRange(-30f, 30f), _rng.RandfRange(-90f, -40f)), _rng.RandfRange(.4f, .8f), _rng.RandfRange(3f, 6f), Body, true);

        for (var i = _sparks.Count - 1; i >= 0; i--)
        {
            var spark = _sparks[i];
            spark.Age += dt;
            if (spark.Age >= spark.Life) { _sparks.RemoveAt(i); continue; }
            spark.Velocity *= 1f - Mathf.Min(1f, 2.2f * dt);
            spark.Velocity.Y += 40f * dt;
            spark.Position += spark.Velocity * dt;
            spark.Angle += spark.Spin * dt;
            _sparks[i] = spark;
        }
        for (var i = _bursts.Count - 1; i >= 0; i--)
        {
            var burst = _bursts[i];
            burst.Age += dt;
            if (burst.Age >= burst.Life) _bursts.RemoveAt(i);
            else _bursts[i] = burst;
        }
        QueueRedraw();
        _light?.QueueRedraw();
        _veil?.QueueRedraw();
    }

    // ── Painted colour (normal blend) ──
    public override void _Draw()
    {
        var glow = SpellVfx.Glow;
        var lantern = LanternStrength();
        if (lantern > .01f) Sprite(this, glow, Emitter, 90f + 40f * lantern, new Color(Deep, .45f * lantern));

        foreach (var missile in _missiles) DrawTrail(missile);

        foreach (var burst in _bursts)
        {
            var t = burst.Age / burst.Life;
            if (burst.Ring)
            {
                // The ring lies on the ground plane.
                var size = burst.Radius * (.4f + 1.5f * (1f - Mathf.Pow(1f - t, 3f)));
                DrawTextureRect(SpellVfx.Ring, new Rect2(burst.Position - new Vector2(size, size * BattleGroundPlane.DepthScale),
                    new Vector2(size * 2f, size * 2f * BattleGroundPlane.DepthScale)), false, new Color(burst.Color, .9f * (1f - t)));
            }
            else Sprite(this, glow, burst.Position, burst.Radius * (1.6f + 1.2f * t), new Color(burst.Color, .75f * (1f - t) * (1f - t)));
        }

        foreach (var missile in _missiles)
            Sprite(this, glow, missile.Position, 44f * missile.Size, new Color(Tint(missile.Hue), .7f));
    }

    private void DrawTrail(Missile missile)
    {
        var count = missile.TrailCount;
        if (count < 2) return;
        var tint = Tint(missile.Hue);
        var deep = tint.Lerp(Deep, .5f);
        var points = missile.Trail;
        for (var i = 0; i < count - 1; i++)
        {
            var a = points[i]; var b = points[i + 1];
            var span = b - a;
            if (span.LengthSquared() < .01f) continue;
            var normal = span.Orthogonal().Normalized();
            var near = 1f - i / (float)(count - 1);
            var far = 1f - (i + 1) / (float)(count - 1);
            // A broad deep-violet ribbon with a brighter band inside it.
            Quad(this, a, b, normal, 8f * missile.Size * near, 8f * missile.Size * far, new Color(deep, .45f * near), new Color(deep, .45f * far));
            Quad(this, a, b, normal, 4f * missile.Size * near, 4f * missile.Size * far, new Color(tint, .8f * near), new Color(tint, .8f * far));
        }
    }

    // ── Added light ──
    private void DrawLight()
    {
        var glow = SpellVfx.Glow;
        var lantern = LanternStrength();
        if (lantern > .01f)
        {
            Sprite(_light, glow, Emitter, 52f + 18f * lantern, new Color(Body, .55f * lantern));
            Sprite(_light, glow, Emitter, 16f + 6f * lantern, new Color(Core, .8f * lantern));
        }

        // Hot threads down the middle of the ribbons.
        foreach (var missile in _missiles)
        {
            var count = Math.Min(missile.TrailCount, TrailPoints / 2);
            var core = Core.Lerp(Tint(missile.Hue), .35f);
            for (var i = 0; i < count - 1; i++)
            {
                var a = missile.Trail[i]; var b = missile.Trail[i + 1];
                var span = b - a;
                if (span.LengthSquared() < .01f) continue;
                var near = 1f - i / (float)(count - 1);
                var far = 1f - (i + 1) / (float)(count - 1);
                Quad(_light, a, b, span.Orthogonal().Normalized(), 1.6f * missile.Size * near, 1.6f * missile.Size * far,
                    new Color(core, .7f * near), new Color(core, .7f * far));
            }
        }

        foreach (var burst in _bursts)
        {
            if (burst.Ring) continue;
            var t = burst.Age / burst.Life;
            Sprite(_light, glow, burst.Position, burst.Radius * (.7f + .6f * t), new Color(Core.Lerp(burst.Color, .3f), .85f * (1f - t)));
        }

        foreach (var spark in _sparks)
        {
            var t = spark.Age / spark.Life;
            var alpha = Mathf.Min(1f, t * 8f) * (1f - t);
            var size = spark.Size * (1f - .5f * t);
            if (spark.Star && _star != null)
            {
                _light.DrawSetTransform(spark.Position, spark.Angle);
                _light.DrawTextureRect(_star, new Rect2(-size, -size, size * 2f, size * 2f), false, new Color(spark.Color, alpha));
                _light.DrawSetTransform(Vector2.Zero);
            }
            else Sprite(_light, glow, spark.Position, size * 2.2f, new Color(spark.Color, alpha * .9f));
        }

        foreach (var missile in _missiles)
        {
            var tint = Tint(missile.Hue);
            var flicker = .85f + .15f * Mathf.Sin(_clock * 41f + missile.Hue * 17f);
            Sprite(_light, glow, missile.Position, 22f * missile.Size * flicker, new Color(tint, .8f));
            Sprite(_light, glow, missile.Position, 9f * missile.Size, new Color(Core, .9f));
            if (_star == null) continue;
            var size = 11f * missile.Size * flicker;
            _light.DrawSetTransform(missile.Position, _clock * 5f + missile.Hue * 6f);
            _light.DrawTextureRect(_star, new Rect2(-size, -size, size * 2f, size * 2f), false, new Color(Core.Lerp(tint, .45f), .8f));
            _light.DrawSetTransform(Vector2.Zero);
        }
    }

    private float LanternStrength() =>
        Mathf.Clamp(_firing * 3f, 0f, 1f) * (.8f + .2f * Mathf.Sin(_clock * 23f)) + _lanternFlare * .5f;

    private static void Quad(CanvasItem canvas, Vector2 a, Vector2 b, Vector2 normal, float widthA, float widthB, Color colorA, Color colorB) =>
        canvas.DrawPrimitive(new[] { a + normal * widthA, b + normal * widthB, b - normal * widthB, a - normal * widthA },
            new[] { colorA, colorB, colorB, colorA }, NoUvs);

    private static void Sprite(CanvasItem canvas, Texture2D texture, Vector2 at, float size, Color color)
    {
        if (color.A <= .003f) return;
        canvas.DrawTextureRect(texture, new Rect2(at - Vector2.One * size * .5f, Vector2.One * size), false, color);
    }

    private void DrawVeil()
    {
        if (_veilAmount <= .01f) return;
        var view = _veil.GetGlobalTransformWithCanvas().AffineInverse() * GetViewportRect();
        _veil.DrawRect(view.Grow(80f), new Color(.07f, .03f, .17f, .36f * _veilAmount));
    }
}
