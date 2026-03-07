using Godot;

public partial class Projectile : Node2D
{
    private Unit _target = null!;
    private float _damage;
    private float _speed;
    private Color _color = Colors.White;
    private float _radius = 5f;
    private bool _active;

    public void Setup(Unit target, float damage, float speed, Color color)
    {
        _target = target;
        _damage = damage;
        _speed = speed;
        _color = color;
        _active = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_active)
        {
            return;
        }

        if (!IsInstanceValid(_target) || _target.IsDead)
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
            _target.TakeDamage(_damage);
            QueueFree();
            return;
        }

        if (distance > 0.001f)
        {
            GlobalPosition += toTarget / distance * step;
        }
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, _radius, _color);
    }
}
