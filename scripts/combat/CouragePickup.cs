using System.Collections.Generic;
using Godot;

/// <summary>
/// A courage pack a fallen enemy sometimes drops: a painted ember gem (art/royal/pickups.py) that tumbles out of
/// the body, hovers over the road in its own warm light with embers rising off it, and, once tapped, streaks into
/// the courage meter on a comet trail. Courage granted directly (supply crates, quartermasters, relics, objectives)
/// borrows only the flight, as a loose flame that can never be tapped.
/// Battle-owned lifetime: pausing, checkpoints and start barriers stop expiry, the pop and the collection flight.
/// </summary>
public partial class CouragePickup : Node2D
{
    /// <summary>Where the gem hovers above its spot on the road, and the smallest tap radius around it (battle px).</summary>
    public static readonly Vector2 TapCenter = new(0, -15);
    public const float TapRadius = 14f;
    public const float FlightDuration = .58f;
    public float Amount { get; private set; }
    public float RemainingSeconds { get; private set; }
    public bool Collected { get; private set; }
    /// <summary>A direct grant's flight: already counted, never tappable.</summary>
    public bool IsEmber { get; private set; }
    public bool Available => !IsEmber && !Collected && RemainingSeconds > 0f && !IsQueuedForDeletion();
    public bool Finished => Collected ? _flightTime >= FlightDuration : RemainingSeconds <= 0f;
    public float FadeAlpha => Collected ? 1f - Mathf.SmoothStep(.72f, 1f, _flightTime / FlightDuration) :
        _fadeDuration <= 0f ? 1f : Mathf.Clamp(RemainingSeconds / _fadeDuration, 0f, 1f);

    private const string TexturePath = "res://assets/fx/pickups/courage_ember.png";
    private const float GemHeight = 25f, PopDuration = .55f, WindUp = .12f;
    private static readonly Color Amber = new(1f, .66f, .26f), Gold = new(1f, .84f, .5f);
    private static Texture2D _gem;
    private float _fadeDuration, _age, _flightTime, _popTime = PopDuration, _glintOffset;
    private Vector2 _flightStart, _flightLift, _popFrom, _visual;
    private float _spin;
    private bool _landed = true;
    private Sprite2D _shadow, _pool, _halo, _bloom, _token, _glint, _outline;
    private CpuParticles2D _motes;
    private Line2D _trail, _trailCore;
    private readonly List<Vector2> _trailPoints = new();

    private static bool ReducedMotion => GameState.Instance?.ReducedMotion ?? false;

    public void Setup(Vector2 position, float amount, float lifetime, float fadeDuration)
    {
        Position = position;
        Amount = amount;
        RemainingSeconds = lifetime;
        _fadeDuration = fadeDuration;
        _glintOffset = GD.Randf() * 2f;
        Build(false);
    }

    /// <summary>A grant's flight from <paramref name="from"/> to the meter; the controller has already added the courage.</summary>
    public void SetupEmber(Vector2 from, float amount)
    {
        Position = from;
        Amount = amount;
        IsEmber = true;
        Build(true);
        Collect();
    }

    /// <summary>Tumbles the gem out of the fallen body, <paramref name="offset"/> above its spot, before it settles.</summary>
    public void PopFrom(Vector2 offset)
    {
        if (ReducedMotion || IsEmber) return;
        _popFrom = offset;
        _popTime = 0f;
        _landed = false;
        _spin = GD.Randf() < .5f ? -1f : 1f;
        Place();
    }

    public void Collect()
    {
        Collected = true;
        _flightStart = Position;
        // The gem rises off the road first, then swoops: the lift bends the path up and away from the troops.
        _flightLift = new Vector2(0, -(IsEmber ? 34f : 48f));
        _trailPoints.Clear();
        if (_motes != null) _motes.Emitting = !ReducedMotion;
        Place();
    }

    public void Tick(float delta, Vector2 meterPosition, bool reducedMotion)
    {
        _age += delta;
        if (Collected)
        {
            _flightTime = Mathf.Min(FlightDuration, _flightTime + delta);
            if (!reducedMotion)
            {
                var start = _flightStart + _visual;
                var t = Mathf.Clamp((_flightTime - WindUp) / (FlightDuration - WindUp), 0f, 1f);
                t = t * t * (1.6f - .6f * t);
                var control = start + _flightLift + new Vector2((meterPosition.X - start.X) * .2f, 0);
                var a = start.Lerp(control, t);
                Position = a.Lerp(control.Lerp(meterPosition, t), t) - _visual;
                RecordTrail();
            }
        }
        else
        {
            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - delta);
            if (_popTime < PopDuration) _popTime = Mathf.Min(PopDuration, _popTime + delta);
            // The embers die down as the pack fades, so it gutters out rather than vanishing mid-blaze.
            if (_motes != null && _fadeDuration > 0f) _motes.Emitting = !reducedMotion && RemainingSeconds > _fadeDuration * .45f;
        }
        Modulate = new Color(1f, 1f, 1f, FadeAlpha);
        Place();
    }

    private void Build(bool ember)
    {
        ZIndex = 450;
        // The controller ticks the pack; its own particles must still freeze with the battle.
        ProcessMode = ProcessModeEnum.Pausable;
        SetProcess(false);
        var reduced = ReducedMotion;
        // Light and shade lie on the road under the troops; the gem itself floats over everything so it stays tappable.
        _shadow = Layer(SpellVfx.Disc, new Color(0, 0, 0, .34f), false, true);
        _pool = Layer(SpellVfx.Glow, new Color(Amber, .3f), true, true);
        _halo = Layer(SpellVfx.Glow, new Color(Amber, .5f), false);
        _bloom = Layer(SpellVfx.Glow, new Color(Gold, .5f), true);
        if (GameState.Instance?.HighContrast ?? false) _outline = Layer(SpellVfx.Ring, new Color("fff0cc"), false);
        _gem ??= ResourceLoader.Exists(TexturePath) ? ResourceLoader.Load<Texture2D>(TexturePath) : null;
        _token = Layer(ember || _gem == null ? SpellVfx.Tex("p_flame") : _gem, Colors.White, false);
        _token.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        _glint = Layer(SpellVfx.Tex("p_star"), new Color(1f, .95f, .82f, 0f), true);
        // The comet trail: a painted amber ribbon (added light alone washes out against the sky) with a hot core.
        _trail = Trail(ember ? 8f : 12f, new Color(1f, .55f, .16f, .7f), false);
        _trailCore = Trail(ember ? 3f : 4.5f, new Color(1f, .9f, .62f), true);
        if (!reduced)
        {
            var ramp = new Gradient();
            ramp.SetColor(0, new Color(1f, .9f, .6f, 0f)); ramp.SetColor(1, new Color(.95f, .35f, .08f, 0f));
            ramp.AddPoint(.15f, new Color(1f, .82f, .45f, .95f)); ramp.AddPoint(.6f, new Color(1f, .5f, .15f, .6f));
            var size = new Curve(); size.AddPoint(new Vector2(0, .5f)); size.AddPoint(new Vector2(.2f, 1f)); size.AddPoint(new Vector2(1, .2f));
            var tex = SpellVfx.Tex("p_ember");
            _motes = new CpuParticles2D
            {
                Amount = ember ? 14 : 8, Lifetime = ember ? .45f : 1.15f, Texture = tex, LocalCoords = false, Emitting = !ember,
                EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere, EmissionSphereRadius = ember ? 3f : 6f,
                Direction = Vector2.Up, Spread = 28f, InitialVelocityMin = 7f, InitialVelocityMax = 17f, Gravity = new Vector2(0, -14f),
                ScaleAmountMin = 3.5f / tex.GetWidth(), ScaleAmountMax = 6.5f / tex.GetWidth(), ScaleAmountCurve = size,
                AngleMin = 0, AngleMax = 360, ColorRamp = ramp, Material = SpellVfx.Additive,
            };
            AddChild(_motes);
        }
        Place();
    }

    private Line2D Trail(float width, Color color, bool additive)
    {
        var line = new Line2D
        {
            Texture = SpellVfx.Streak, TextureMode = Line2D.LineTextureMode.Stretch, Width = width, DefaultColor = color,
            JointMode = Line2D.LineJointMode.Round, WidthCurve = new Curve(), Visible = false,
        };
        if (additive) line.Material = SpellVfx.Additive;
        line.WidthCurve.AddPoint(new Vector2(0, .1f)); line.WidthCurve.AddPoint(new Vector2(1, 1));
        AddChild(line); MoveChild(line, _halo.GetIndex());
        return line;
    }

    private Sprite2D Layer(Texture2D texture, Color color, bool additive, bool ground = false)
    {
        var sprite = new Sprite2D { Texture = texture, Modulate = color };
        if (additive) sprite.Material = SpellVfx.Additive;
        if (ground) { sprite.ZAsRelative = false; sprite.ZIndex = SpellVfx.GroundLayer; }
        AddChild(sprite);
        return sprite;
    }

    private static void Size(Sprite2D sprite, Vector2 size)
    {
        if (sprite?.Texture == null) return;
        sprite.Scale = size / sprite.Texture.GetSize();
    }

    /// <summary>Lays out every layer for the current pop, hover, glint and flight state.</summary>
    private void Place()
    {
        if (_token == null) return;
        var reduced = ReducedMotion;
        var hover = TapCenter + new Vector2(0, reduced || Collected ? 0f : Mathf.Sin(_age * 2.4f) * 1.6f);
        var rotation = reduced ? 0f : Mathf.Sin(_age * 1.6f) * .05f;
        var scale = reduced ? 1f : 1f + .03f * Mathf.Sin(_age * 3.1f);
        if (_popTime < PopDuration)
        {
            // A tumbling arc out of the body, a dip below the hover height, then a springy settle.
            var t = _popTime / PopDuration;
            const float split = .68f;
            if (t < split)
            {
                var u = t / split;
                var low = TapCenter + new Vector2(0, 5);
                hover = _popFrom.Lerp(low, u) + new Vector2(0, -26f * 4f * u * (1 - u));
                scale = Mathf.Lerp(.35f, 1f, Mathf.Min(1f, u * 1.8f));
                rotation = (1 - u) * _spin * Mathf.Tau * .55f;
            }
            else
            {
                if (!_landed) Land();
                var u = (t - split) / (1 - split);
                hover = (TapCenter + new Vector2(0, 5)).Lerp(hover, EaseOutBack(u));
                scale *= 1f + .14f * Mathf.Sin(u * Mathf.Pi) * (1 - u);
            }
        }
        if (Collected)
        {
            hover = _visual = _flightTime < .001f ? hover : _visual;
            var wind = Mathf.Clamp(_flightTime / WindUp, 0f, 1f);
            var travel = Mathf.Clamp((_flightTime - WindUp) / (FlightDuration - WindUp), 0f, 1f);
            // A quick swell as it is claimed, then it shrinks into the meter.
            scale = IsEmber ? Mathf.Lerp(1.2f, .7f, travel) : (1f + .4f * Mathf.Sin(wind * Mathf.Pi * .5f)) * Mathf.Lerp(1f, .5f, travel * travel);
            rotation = 0f;
        }
        else _visual = hover;

        var height = Mathf.Max(0f, TapCenter.Y - hover.Y);
        var gem = IsEmber ? new Vector2(15, 15) : new Vector2(GemHeight * .7f, GemHeight);
        _token.Position = hover; _token.Rotation = rotation; Size(_token, gem * scale);
        Size(_halo, Vector2.One * 50f * scale * (reduced ? 1f : 1f + .07f * Mathf.Sin(_age * 3.1f + 1f)));
        _halo.Position = hover;
        var flicker = reduced ? 0f : .1f * Mathf.Sin(_age * 11f) + .06f * Mathf.Sin(_age * 17.3f);
        _bloom.Position = hover + new Vector2(0, 2); Size(_bloom, Vector2.One * 30f * scale);
        _bloom.Modulate = new Color(Gold, .5f + flicker);
        if (_outline != null) { _outline.Position = hover; Size(_outline, Vector2.One * 38f * scale); }
        var grounded = !Collected;
        _shadow.Visible = _pool.Visible = grounded;
        _shadow.Position = new Vector2(0, 1); Size(_shadow, new Vector2(20f, 6.5f) / (1f + height * .03f));
        _shadow.Modulate = new Color(0, 0, 0, .34f / (1f + height * .05f));
        _pool.Position = new Vector2(0, 0); Size(_pool, new Vector2(44f, 14f) * (1f + flicker * .4f));
        if (_motes != null) _motes.Position = hover + new Vector2(0, -2);

        // A glint travels across the facets every couple of seconds.
        var phase = Mathf.PosMod(_age + _glintOffset, 2.3f);
        var glint = reduced || Collected || IsEmber || phase > .42f ? 0f : Mathf.Sin(Mathf.Pi * phase / .42f);
        _glint.Visible = glint > 0f;
        _glint.Position = hover + new Vector2(-4.5f + phase * 9f, -6f + phase * 6f);
        _glint.Rotation = _age * 1.8f;
        _glint.Modulate = new Color(1f, .95f, .82f, glint);
        Size(_glint, Vector2.One * Mathf.Max(.1f, 15f * glint));

        _trail.Visible = _trailCore.Visible = Collected && _trailPoints.Count > 1;
        if (_trail.Visible)
        {
            var points = new Vector2[_trailPoints.Count];
            for (var i = 0; i < points.Length; i++) points[i] = ToLocal(_trailPoints[i]);
            _trail.Points = _trailCore.Points = points;
        }
    }

    private void RecordTrail()
    {
        var point = GlobalPosition + _visual;
        if (_trailPoints.Count > 0 && _trailPoints[^1].DistanceSquaredTo(point) < .25f) return;
        _trailPoints.Add(point);
        if (_trailPoints.Count > 12) _trailPoints.RemoveAt(0);
    }

    private void Land()
    {
        _landed = true;
        var parent = GetParent();
        if (parent == null) return;
        SpellVfx.Burst(parent, Position + new Vector2(0, -1), "p_dust", 5, .5f, SpellVfx.Fade(new Color("cbb48e"), new Color("8f7a5c")),
            5, 9, 14, 30, 70, Vector2.Up, 20, false, 6, layer: SpellVfx.TroopLayer, damping: 30);
        SpellVfx.Burst(parent, Position + TapCenter, "p_ember", 7, .55f, SpellVfx.Fade(Gold, Amber), 3, 5, 30, 70, 70, Vector2.Up,
            60, damping: 40, layer: SpellVfx.FlashLayer);
    }

    private static float EaseOutBack(float t)
    {
        const float c = 1.9f;
        var p = t - 1f;
        return 1f + (c + 1f) * p * p * p + c * p * p;
    }
}
