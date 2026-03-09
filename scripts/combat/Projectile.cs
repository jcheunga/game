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

    public void Setup(Unit target, float damage, float speed, Color color, Action<Vector2, float, Color> onHit = null)
    {
        Setup(
            target,
            damage,
            speed,
            color,
            target.TakeDamage,
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
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_active)
        {
            return;
        }

        if (!IsInstanceValid(_target) || (_shouldCancel != null && _shouldCancel()))
        {
            QueueFree();
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
            QueueFree();
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
