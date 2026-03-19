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

    public override void _ExitTree()
    {
        CleanupTrail();
    }

    public void ResetForPool()
    {
        _active = false;
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
        _damage = damage;
        _speed = speed;
        _color = color;
        _radius = damage >= 18f ? 6f : 5f;
        _applyImpact = applyImpact;
        _shouldCancel = shouldCancel;
        _onHit = onHit;
        _active = true;
        _trail = BattleParticles.SpawnProjectileTrail(this, _color);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_active)
        {
            return;
        }

        if (!IsInstanceValid(_target) || (_shouldCancel != null && _shouldCancel()))
        {
            // Target died mid-flight — spawn impact effect at current position instead of vanishing silently
            _onHit?.Invoke(GlobalPosition, 0f, _color);
            if (GetParent() != null)
            {
                BattleParticles.SpawnImpactSparks(GetParent(), GlobalPosition, _color, _damage * 0.5f);
            }
            ProjectilePool.Release(this);
            return;
        }

        var deltaF = (float)delta;
        var toTarget = _target.GlobalPosition - GlobalPosition;
        var distance = toTarget.Length();
        var step = _speed * deltaF;

        if (distance <= step + _radius)
        {
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
        }
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        var tailEnd = -_travelDirection * (_radius * 2.2f);
        DrawLine(Vector2.Zero, tailEnd, new Color(_color, 0.55f), _radius * 1.2f, true);
        DrawCircle(Vector2.Zero, _radius, _color);
        DrawCircle(Vector2.Zero, _radius * 0.42f, _color.Lightened(0.4f));
    }

    private void SpawnImpactEffect()
    {
        if (GetParent() == null)
        {
            return;
        }

        var effect = new BattleEffect();
        effect.GlobalPosition = GlobalPosition;
        effect.Setup(_color.Lightened(0.15f), _radius * 0.7f, _radius * 3f, 0.16f, false);
        GetParent().AddChild(effect);
    }
}
