using System;
using System.Collections.Generic;
using Godot;

// A shot in flight. The simulation homes it in a straight line on the target's body; only the drawing
// lifts it along its style's arc, turns it with the flight path, spins or sways it, and trails it.
public partial class Projectile : Node2D
{
    /// <summary>The zone's light on the battlefield, so lit sprites match the units around them.</summary>
    public static Color FieldTint { get; set; } = Colors.White;

    private Node2D _target = null!;
    private float _damage;
    private float _speed;
    private Color _color = Colors.White;
    private float _radius = 5f;
    private bool _active, _launched;
    private Vector2 _travelDirection = Vector2.Right;
    private Action<Vector2, float, Color> _onHit = null!;
    private Func<float, float> _applyImpact = null!;
    private Func<bool> _shouldCancel = null!;
    private CpuParticles2D _trail;
    private ProjectileGlow _glow;
    private ProjectileStyle _style = ProjectileStyles.Default;
    private uint _targetLifetime;
    private float _flightLength, _distanceTravelled, _remaining = 1f, _age, _seed;
    private float _launchGroundY, _targetGroundY;
    private Vector2 _drawOffset, _drawDirection = Vector2.Right;
    private readonly List<Vector2> _streak = new();
    public Vector2 TargetOffset { get; set; }
    public Func<bool> ShouldPause { get; set; }
    /// <summary>Ground line under the shooter; lobbed shots cast their shadow between it and the target's feet.</summary>
    public float LaunchGroundY { get; set; } = float.NaN;
    public ProjectileStyle Style => _style;
    internal Vector2 DrawOffset => _drawOffset;
    internal Vector2 DrawDirection => _drawDirection;
    internal float Age => _age;
    internal float Seed => _seed;

    public void SetStyle(ProjectileStyle style) => _style = style ?? ProjectileStyles.Default;
    public void SetWeaponVisual(BaseWeaponKind kind, string shot = null) => SetStyle(ProjectileStyles.ForBaseWeapon(kind, shot));

    public override void _ExitTree()
    {
        CleanupTrail(null);
    }

    public void ResetForPool()
    {
        _active = false;
        _launched = false;
        _style = ProjectileStyles.Default;
        TargetOffset = Vector2.Zero;
        LaunchGroundY = float.NaN;
        _distanceTravelled = 0f;
        _age = 0f;
        ShouldPause = null;
        ProcessMode = ProcessModeEnum.Inherit;
        _target = null;
        _damage = 0f;
        _speed = 0f;
        _onHit = null;
        _applyImpact = null;
        _shouldCancel = null;
        _travelDirection = Vector2.Right;
        _streak.Clear();
        CleanupTrail(null);
        if (_glow != null) _glow.Visible = false;
        Visible = false;
    }

    private void CleanupTrail(Node world)
    {
        if (_trail != null && IsInstanceValid(_trail))
        {
            if (world != null) ProjectileEffects.ReleaseTrail(_trail, world);
            else { _trail.Emitting = false; _trail.QueueFree(); }
        }
        _trail = null;
    }

    public void Setup(Unit target, float damage, float speed, Color color, Action<Vector2, float, Color> onHit = null)
    {
        Setup(
            target,
            damage,
            speed,
            color,
            d => target.TakeDamage(d),
            () => !IsInstanceValid(target) || target.IsDead,
            onHit);
    }

    public void Setup(
        Node2D target,
        float damage,
        float speed,
        Color color,
        Func<float, float> applyImpact,
        Func<bool> shouldCancel = null,
        Action<Vector2, float, Color> onHit = null)
    {
        _target = target;
        _targetLifetime = target is Unit unit ? unit.CombatLifetime : 0;
        _damage = damage;
        _speed = _style.MaxSpeed > 0 ? Mathf.Min(speed, _style.MaxSpeed) : speed;
        _color = color;
        _radius = _style.Shape == ProjectileShape.Sprite ? Mathf.Max(3f, _style.Length * .2f) : damage >= 18f ? 6f : 5f;
        _applyImpact = applyImpact;
        _shouldCancel = shouldCancel;
        var aim = AimPoint();
        _flightLength = Mathf.Max(1f, GlobalPosition.DistanceTo(aim));
        _remaining = _flightLength;
        _distanceTravelled = 0f;
        _age = 0f;
        _seed = GD.Randf() * 100f;
        _travelDirection = (aim - GlobalPosition).Normalized();
        _drawDirection = _travelDirection;
        _drawOffset = Vector2.Zero;
        _targetGroundY = target is Unit feet ? feet.GlobalPosition.Y : aim.Y + 14f;
        _launchGroundY = float.IsNaN(LaunchGroundY) ? GlobalPosition.Y + 14f : LaunchGroundY;
        _onHit = onHit;
        _active = true;
        _launched = false;
        _streak.Clear();
        // Flight is above grounded actors; impact effects remain above projectiles.
        ZIndex = 10;
        Visible = true;
        CleanupTrail(null);
        _trail = ProjectileEffects.SpawnTrail(this, _style);
        if (_style.GlowColor.A > 0 || _style.Shape != ProjectileShape.Sprite)
        {
            if (_glow == null || !IsInstanceValid(_glow))
            {
                _glow = new ProjectileGlow { Shot = this };
                AddChild(_glow);
            }
            _glow.Visible = true;
        }
        else if (_glow != null) _glow.Visible = false;
    }

    private Vector2 AimPoint() => _target is Unit aimedUnit ? aimedUnit.BodyContactPosition : _target.GlobalPosition + TargetOffset;

    public override void _PhysicsProcess(double delta)
    {
        if (!_active || (ShouldPause?.Invoke() ?? false))
        {
            return;
        }

        if (!IsInstanceValid(_target) || (_target is Unit pooled && pooled.CombatLifetime != _targetLifetime) || (_shouldCancel != null && _shouldCancel()))
        {
            // Target died mid-flight: the shot fizzles where it is instead of vanishing silently.
            if (GetParent() != null)
            {
                BattleParticles.SpawnImpactSparks(GetParent(), GlobalPosition + _drawOffset, _style.GlowColor.A > 0 ? _style.GlowColor : _color, _damage * 0.5f);
            }
            CleanupTrail(GetParent());
            ProjectilePool.Release(this);
            return;
        }

        var deltaF = (float)delta;
        var targetPoint = AimPoint();
        var toTarget = targetPoint - GlobalPosition;
        var distance = toTarget.Length();
        var step = _speed * deltaF;

        if (distance <= step + _radius)
        {
            GlobalPosition = targetPoint;
            var appliedDamage = _applyImpact?.Invoke(_damage) ?? 0f;
            _onHit?.Invoke(GlobalPosition, appliedDamage, _color);
            ProjectileEffects.SpawnImpact(GetParent(), _style, GlobalPosition, _drawDirection, _damage, _target, ShouldPause);
            CleanupTrail(GetParent());
            ProjectilePool.Release(this);
            return;
        }

        if (distance > 0.001f)
        {
            _travelDirection = toTarget / distance;
            GlobalPosition += _travelDirection * step;
            _distanceTravelled += step;
            _remaining = Mathf.Max(0, distance - step);
        }
    }

    public override void _Process(double delta)
    {
        if (!_active) return;
        if (!_launched && GetParent() != null)
        {
            _launched = true;
            ProjectileEffects.SpawnLaunch(GetParent(), _style, GlobalPosition, _travelDirection);
        }
        if (!(ShouldPause?.Invoke() ?? false)) _age += (float)delta;
        UpdateDrawPath();
        if (_trail != null && IsInstanceValid(_trail)) _trail.Position = _drawOffset;
        // The head only moves on physics ticks, so a sample per tick keeps the streak's length the same at
        // any frame rate (and while paused).
        var head = GlobalPosition + _drawOffset;
        if (_style.Trail == ProjectileTrail.Streak && (_streak.Count == 0 || _streak[^1].DistanceSquaredTo(head) > .01f))
        {
            _streak.Add(head);
            if (_streak.Count > 5) _streak.RemoveAt(0);
        }
        QueueRedraw();
        _glow?.QueueRedraw();
    }

    // Progress runs 0 at launch to 1 at impact even when the target moves.
    private float Progress => Mathf.Clamp(_distanceTravelled / Mathf.Max(1f, _distanceTravelled + _remaining), 0f, 1f);
    private float ArcHeight => Mathf.Min(_style.ArcMax, _style.Arc * _flightLength);

    private void UpdateDrawPath()
    {
        var p = Progress;
        var h = ArcHeight;
        var lift = h * Mathf.Sin(p * Mathf.Pi);
        var normal = _travelDirection.Orthogonal();
        var reduced = GameState.Instance?.ReducedMotion ?? false;
        var sway = reduced ? 0 : Mathf.Sin(_age * 13f + _seed) * _style.Wobble;
        _drawOffset = new Vector2(0, -lift) + normal * sway;
        // Tangent of the drawn path: straight travel plus the arc's rise and fall.
        var rise = h * Mathf.Pi * Mathf.Cos(p * Mathf.Pi) / Mathf.Max(1f, _distanceTravelled + _remaining);
        var tangent = _travelDirection + new Vector2(0, -rise);
        _drawDirection = tangent.LengthSquared() > 1e-6f ? tangent.Normalized() : _travelDirection;
    }

    public override void _Draw()
    {
        if (!_active) return;
        var tint = FieldTint;
        if (_style.Lobbed)
        {
            // A soft shadow on the ground beneath a lobbed shot sells its height.
            var ground = Mathf.Lerp(_launchGroundY, _targetGroundY, Progress) - GlobalPosition.Y;
            var lift = -_drawOffset.Y;
            var fade = Mathf.Clamp(1f - lift / 90f, .35f, 1f);
            DrawSetTransform(new Vector2(_drawOffset.X, ground), 0, new Vector2(1, .38f));
            DrawCircle(Vector2.Zero, _style.Length * .45f, new Color(0.04f, 0.05f, 0.06f, .24f * fade));
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
        if (_streak.Count > 1)
        {
            var color = new Color(_style.TrailTint, 0);
            for (var i = 1; i < _streak.Count; i++)
            {
                var a = ToLocal(_streak[i - 1]);
                var b = ToLocal(_streak[i]);
                var t = (float)i / _streak.Count;
                DrawLine(a, b, new Color(color, .32f * t), Mathf.Max(.8f, _style.Length * .08f) * t, true);
            }
        }
        if (_style.Shape != ProjectileShape.Sprite) return;
        var texture = ProjectileStyles.Texture(_style.Sprite);
        if (texture == null)
        {
            DrawCircle(_drawOffset, _radius * .6f, _color);
            return;
        }
        var meta = ProjectileStyles.Meta(_style.Sprite);
        var w = _style.Length;
        var h = w * texture.GetHeight() / texture.GetWidth();
        var angle = _drawDirection.Angle();
        Vector2 scale;
        Rect2 rect;
        switch (_style.Orient)
        {
            case ProjectileOrient.Spin:
                angle = (_age * _style.Spin + _seed) * (_travelDirection.X < 0 ? -1 : 1);
                scale = Vector2.One;
                rect = new Rect2(-meta.Centre.X * w, -meta.Centre.Y * h, w, h);
                break;
            case ProjectileOrient.Upright:
                var leftward = _drawDirection.X < 0;
                var tilt = Mathf.Clamp(Mathf.Atan2(_drawDirection.Y, Mathf.Abs(_drawDirection.X)), -.5f, .5f);
                angle = leftward ? -tilt : tilt;
                scale = new Vector2(leftward ? -1 : 1, 1);
                rect = new Rect2(-meta.Centre.X * w, -meta.Centre.Y * h, w, h);
                break;
            default:
                // Turned along the path; mirrored when flying left so the top vane stays on top.
                scale = new Vector2(1, Mathf.Cos(angle) < 0 ? -1 : 1);
                rect = new Rect2(-meta.Tip.X * w, -meta.Tip.Y * h, w, h);
                break;
        }
        DrawSetTransform(_drawOffset, angle, scale);
        DrawTextureRect(texture, rect, false, tint * _style.SpriteTint);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}

/// <summary>The additive light of a shot: halos round glowing sprites, and the whole of magic orbs and
/// lightning. It draws in its projectile's space.</summary>
public partial class ProjectileGlow : Node2D
{
    public Projectile Shot { get; set; }

    public override void _Ready()
    {
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        ZIndex = 1;
    }

    private void Blob(Vector2 at, float radius, Color color)
        => DrawTextureRect(ParticleTextureLoader.SoftTexture, new Rect2(at - Vector2.One * radius, Vector2.One * radius * 2), false, color);

    private void Textured(string id, Vector2 at, float radius, float angle, Color color)
    {
        var texture = ParticleTextureLoader.TryLoad(id);
        if (texture == null) return;
        DrawSetTransform(at, angle, Vector2.One);
        DrawTextureRect(texture, new Rect2(-Vector2.One * radius, Vector2.One * radius * 2), false, color);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    public override void _Draw()
    {
        if (Shot == null || !IsInstanceValid(Shot)) return;
        var style = Shot.Style;
        var at = Shot.DrawOffset;
        var age = Shot.Age;
        var glow = style.GlowColor;
        var pulse = 1f + .12f * Mathf.Sin(age * 22f + Shot.Seed);
        var r = style.GlowRadius * pulse;
        switch (style.Shape)
        {
            case ProjectileShape.Sprite:
                if (glow.A > 0) Blob(at, r, new Color(glow, .5f));
                return;
            case ProjectileShape.Lightning:
                DrawLightning(at, Shot.DrawDirection, r, glow, style.CoreColor, age, Shot.Seed);
                return;
        }
        // Magic orb: a wide halo, the style's sigil turning behind it, a hot core.
        Blob(at, r * 1.25f, new Color(glow, .38f));
        switch (style.Id)
        {
            case "arcane":
                Textured("particle_arcane", at, r * .95f, age * 4f, new Color(glow.Lightened(.2f), .7f));
                break;
            case "hex":
                Textured("particle_arcane", at, r * .9f, -age * 5f, new Color(glow, .65f));
                for (var i = 0; i < 3; i++)
                {
                    var a = age * 9f + i * Mathf.Tau / 3;
                    Blob(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * .6f) * r * .55f, r * .22f, new Color(style.CoreColor, .8f));
                }
                break;
            case "holy":
                for (var i = 0; i < 4; i++)
                    Textured("particle_spark", at, r * 1.1f, age * 2f + i * Mathf.Pi / 4, new Color(glow.Lightened(.3f), .45f));
                break;
            case "relic":
                Textured("particle_fire", at + new Vector2(0, -1), r * .9f, Mathf.Sin(age * 18f) * .2f,
                    new Color(glow, .85f));
                break;
            case "water":
                for (var i = 0; i < 3; i++)
                {
                    var a = -age * 10f + i * Mathf.Tau / 3;
                    Blob(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * .45f, r * .25f, new Color(style.CoreColor, .55f));
                }
                break;
        }
        Blob(at, r * .55f, new Color(glow.Lightened(.25f), .9f));
        Blob(at, style.Length * .7f, new Color(style.CoreColor, 1f));
    }

    // A crackling ball with short arcs leaping off it, and a jagged tail back along its path.
    private void DrawLightning(Vector2 at, Vector2 direction, float r, Color glow, Color core, float age, float seed)
    {
        Blob(at, r * 1.3f, new Color(glow, .45f));
        var rng = new RandomNumberGenerator { Seed = (ulong)(Mathf.FloorToInt(age * 30f) * 7919 + (int)(seed * 13)) };
        for (var k = 0; k < 3; k++)
        {
            var a = rng.RandfRange(0, Mathf.Tau);
            var points = new Vector2[4];
            for (var i = 0; i < 4; i++)
            {
                var d = r * .25f + r * .85f * i / 3f;
                var jitter = rng.RandfRange(-.5f, .5f);
                points[i] = at + new Vector2(Mathf.Cos(a + jitter), Mathf.Sin(a + jitter)) * d;
            }
            DrawPolyline(points, new Color(glow.Lightened(.4f), .9f), 1.2f, true);
        }
        var tail = new Vector2[6];
        var normal = direction.Orthogonal();
        for (var i = 0; i < 6; i++)
            tail[i] = at - direction * (i * r * .45f) + normal * (i == 0 ? 0 : rng.RandfRange(-2.2f, 2.2f));
        DrawPolyline(tail, new Color(glow, .75f), 2.4f, true);
        DrawPolyline(tail, new Color(core, .95f), 1f, true);
        Blob(at, r * .5f, new Color(glow.Lightened(.3f), .95f));
        Blob(at, 3f, new Color(core, 1f));
    }
}
