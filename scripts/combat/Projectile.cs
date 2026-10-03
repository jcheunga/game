using System;
using Godot;

public partial class Projectile : Node2D
{
    private Node2D _target = null!;
    private float _damage;
    private float _speed;
    private Color _color = Colors.White;
    private float _radius = 5f;
    private bool _active;
    private Vector2 _travelDirection = Vector2.Right;
    private Action<Vector2, float, Color> _onHit = null!;
    private Func<float, float> _applyImpact = null!;
    private Func<bool> _shouldCancel = null!;
    private CpuParticles2D _trail;
    private BaseWeaponKind? _weaponVisual;
    private uint _targetLifetime;
    private float _flightLength, _distanceTravelled;
    public Vector2 TargetOffset { get; set; }
    public Func<bool> ShouldPause { get; set; }

    public void SetWeaponVisual(BaseWeaponKind kind) => _weaponVisual = kind;

    public override void _ExitTree()
    {
        CleanupTrail();
    }

    public void ResetForPool()
    {
        _active = false;
        _weaponVisual = null;
        TargetOffset = Vector2.Zero;
        _distanceTravelled = 0f;
        ShouldPause = null;
        ProcessMode = ProcessModeEnum.Inherit;
        _target = null;
        _damage = 0f;
        _speed = 0f;
        _onHit = null;
        _applyImpact = null;
        _shouldCancel = null;
        _travelDirection = Vector2.Right;
        CleanupTrail();
        Visible = false;
    }

    private void CleanupTrail()
    {
        if (_trail != null && IsInstanceValid(_trail))
        {
            _trail.Emitting = false;
            _trail.QueueFree();
            _trail = null;
        }
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
        _speed = _weaponVisual == BaseWeaponKind.Ballista ? Mathf.Min(speed, 620f) : speed;
        _color = _weaponVisual == BaseWeaponKind.Ballista ? new Color("aaa393") : color;
        _radius = _weaponVisual == BaseWeaponKind.Ballista ? 10f : damage >= 18f ? 6f : 5f;
        _applyImpact = applyImpact;
        _shouldCancel = shouldCancel;
        var aim = target is Unit aimedUnit ? aimedUnit.BodyContactPosition : target.GlobalPosition + TargetOffset;
        _flightLength = Mathf.Max(1f, GlobalPosition.DistanceTo(aim));
        _distanceTravelled = 0f;
        _travelDirection = (aim-GlobalPosition).Normalized();
        _onHit = onHit;
        _active = true;
        // Flight is above grounded actors; impact effects remain above projectiles.
        ZIndex = 10;
        Visible = true;
        if (_weaponVisual is not (BaseWeaponKind.Arrows or BaseWeaponKind.Ballista))
            _trail = BattleParticles.SpawnProjectileTrail(this, _color);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_active || (ShouldPause?.Invoke() ?? false))
        {
            return;
        }

        if (!IsInstanceValid(_target) || (_target is Unit pooled && pooled.CombatLifetime != _targetLifetime) || (_shouldCancel != null && _shouldCancel()))
        {
            // Target died mid-flight — spawn impact effect at current position instead of vanishing silently
            if (GetParent() != null)
            {
                BattleParticles.SpawnImpactSparks(GetParent(), GlobalPosition, _color, _damage * 0.5f);
            }
            ProjectilePool.Release(this);
            return;
        }

        var deltaF = (float)delta;
        var targetPoint = _target is Unit victim ? victim.BodyContactPosition : _target.GlobalPosition + TargetOffset;
        var toTarget = targetPoint - GlobalPosition;
        var distance = toTarget.Length();
        var step = _speed * deltaF;

        if (distance <= step + _radius)
        {
            GlobalPosition = targetPoint;
            var appliedDamage = _applyImpact?.Invoke(_damage) ?? 0f;
            _onHit?.Invoke(GlobalPosition, appliedDamage, _color);
            SpawnImpactEffect();
            if (GetParent() != null)
            {
                BattleParticles.SpawnImpactSparks(GetParent(), GlobalPosition, _color, _damage);
            }
            ProjectilePool.Release(this);
            return;
        }

        if (distance > 0.001f)
        {
            _travelDirection = toTarget / distance;
            GlobalPosition += _travelDirection * step;
            _distanceTravelled += step;
        }
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_weaponVisual == BaseWeaponKind.Ballista)
        {
            var progress = Mathf.Clamp(_distanceTravelled / _flightLength, 0f, 1f);
            var lift = Mathf.Sin(progress * Mathf.Pi) * Mathf.Min(64f, _flightLength * .14f);
            DrawSetTransform(new Vector2(0, 16), 0, new Vector2(1, .35f));
            DrawCircle(Vector2.Zero, _radius * .85f, new Color("11182045"));
            DrawSetTransform(new Vector2(0, -lift), progress * 3f);
            DrawColoredPolygon(new[] { new Vector2(-10,-3), new Vector2(-5,-10), new Vector2(4,-9),
                new Vector2(11,-2), new Vector2(8,7), new Vector2(-1,10), new Vector2(-9,5) }, _color.Darkened(.25f));
            DrawColoredPolygon(new[] { new Vector2(-10,-3), new Vector2(-5,-10), new Vector2(4,-9),
                new Vector2(2,-1), new Vector2(-4,3) }, _color.Lightened(.16f));
            DrawColoredPolygon(new[] { new Vector2(2,-1), new Vector2(4,-9), new Vector2(11,-2), new Vector2(8,7) }, _color);
            DrawLine(new Vector2(-4,3), new Vector2(2,-1), _color.Darkened(.45f), 1.5f, true);
            return;
        }
        if (_weaponVisual == BaseWeaponKind.Arrows)
        {
            var length = 17f;
            var normal = _travelDirection.Orthogonal();
            DrawLine(-_travelDirection * length, Vector2.Zero, _color, 2f, true);
            DrawColoredPolygon(new[] { _travelDirection * 4, -_travelDirection * 6 + normal * 4, -_travelDirection * 6 - normal * 4 }, _color.Lightened(0.2f));
            return;
        }
        var tailEnd = -_travelDirection * (_radius * 2.2f);
        DrawLine(Vector2.Zero, tailEnd, new Color(_color, 0.55f), _radius * 1.2f, true);
        DrawCircle(Vector2.Zero, _radius, _color);
        DrawCircle(Vector2.Zero, _radius * 0.42f, _color.Lightened(0.4f));
    }

    private void SpawnImpactEffect()
    {
        if (GetParent() == null || IsReducedMotionEnabled())
        {
            return;
        }

        var effect = new BattleEffect();
        effect.GlobalPosition = GlobalPosition;
        effect.Setup(_color.Lightened(0.15f), _radius * 0.7f, _radius * 3f, 0.16f, false);
        GetParent().AddChild(effect);
    }

    private static bool IsReducedMotionEnabled()
    {
        return GameState.Instance != null && GameState.Instance.ReducedMotion;
    }
}
