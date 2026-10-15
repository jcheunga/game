using Godot;

/// <summary>
/// A plain expanding ring or disc for small battle feedback. Spells have their own effects (SpellVfx).
/// </summary>
public partial class BattleEffect : Node2D
{
    private Color _color = Colors.White;
    private float _startRadius = 8f;
    private float _endRadius = 30f;
    private float _lifetime = 0.3f;
    private float _elapsed;
    private bool _filled = true;
    public bool GroundProjected { get; set; }

    public void Setup(Color color, float startRadius, float endRadius, float lifetime, bool filled = true)
    {
        _color = color;
        _startRadius = startRadius;
        _endRadius = endRadius;
        _lifetime = Mathf.Max(0.05f, lifetime);
        _filled = filled;
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        if (_elapsed >= _lifetime)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var t = Mathf.Clamp(_elapsed / _lifetime, 0f, 1f);
        var radius = Mathf.Lerp(_startRadius, _endRadius, t);
        var color = new Color(_color, (1f - t) * 0.75f);
        if (GroundProjected) DrawSetTransform(Vector2.Zero, 0, new Vector2(1f, BattleGroundPlane.DepthScale));
        if (_filled) DrawCircle(Vector2.Zero, radius, color);
        else DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 24, color, 3f);
    }
}
