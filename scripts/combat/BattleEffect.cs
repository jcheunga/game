using Godot;

public enum BattleEffectStyle
{
    Pulse,
    Fireburst,
    HealBloom,
    FrostBurst,
    LightningStrike,
    WardSigil
}

public partial class BattleEffect : Node2D
{
    private Color _color = Colors.White;
    private float _startRadius = 8f;
    private float _endRadius = 30f;
    private float _lifetime = 0.3f;
    private float _elapsed;
    private bool _filled = true;
    private BattleEffectStyle _style = BattleEffectStyle.Pulse;

    public void Setup(
        Color color,
        float startRadius,
        float endRadius,
        float lifetime,
        bool filled = true,
        BattleEffectStyle style = BattleEffectStyle.Pulse)
    {
        _color = color;
        _startRadius = startRadius;
        _endRadius = endRadius;
        _lifetime = Mathf.Max(0.05f, lifetime);
        _filled = filled;
        _style = style;
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
        var alpha = 1f - t;
        var color = new Color(_color, alpha * 0.75f);

        switch (_style)
        {
            case BattleEffectStyle.Fireburst:
                DrawFireburst(radius, color, t);
                break;
            case BattleEffectStyle.HealBloom:
                DrawHealBloom(radius, color, t);
                break;
            case BattleEffectStyle.FrostBurst:
                DrawFrostBurst(radius, color, t);
                break;
            case BattleEffectStyle.LightningStrike:
                DrawLightningStrike(radius, color, t);
                break;
            case BattleEffectStyle.WardSigil:
                DrawWardSigil(radius, color, t);
                break;
            default:
                DrawPulse(radius, color);
                break;
        }
    }

    private void DrawPulse(float radius, Color color)
    {
        if (_filled)
        {
            DrawCircle(Vector2.Zero, radius, color);
        }
        else
        {
            DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 24, color, 3f);
        }
    }

    private void DrawFireburst(float radius, Color color, float t)
    {
        var coreColor = color.Lightened(0.2f);
        coreColor.A = color.A * 0.58f;
        DrawCircle(Vector2.Zero, radius * 0.34f, coreColor);
        DrawArc(Vector2.Zero, radius * 0.74f, 0f, Mathf.Tau, 28, color, Mathf.Lerp(5f, 2f, t));

        for (var i = 0; i < 8; i++)
        {
            var angle = ((Mathf.Tau / 8f) * i) + (t * 0.2f);
            var direction = Direction(angle);
            DrawLine(
                direction * (radius * 0.22f),
                direction * radius,
                color,
                Mathf.Lerp(5f, 1.4f, t),
                true);
        }
    }

    private void DrawHealBloom(float radius, Color color, float t)
    {
        var ringColor = color.Lightened(0.08f);
        ringColor.A = color.A * 0.84f;
        var coreColor = color.Lightened(0.18f);
        coreColor.A = color.A * 0.34f;
        DrawArc(Vector2.Zero, radius * 0.74f, 0f, Mathf.Tau, 28, ringColor, Mathf.Lerp(4.5f, 1.8f, t));
        DrawCircle(Vector2.Zero, radius * 0.26f, coreColor);

        var crossWidth = Mathf.Lerp(5f, 1.6f, t);
        DrawLine(new Vector2(0f, -radius * 0.56f), new Vector2(0f, radius * 0.56f), ringColor, crossWidth, true);
        DrawLine(new Vector2(-radius * 0.56f, 0f), new Vector2(radius * 0.56f, 0f), ringColor, crossWidth, true);

        for (var i = 0; i < 4; i++)
        {
            var angle = (Mathf.Pi * 0.25f) + ((Mathf.Tau / 4f) * i);
            var petalColor = color.Lightened(0.16f);
            petalColor.A = color.A * 0.7f;
            DrawCircle(Direction(angle) * (radius * 0.5f), Mathf.Lerp(6f, 2.3f, t), petalColor);
        }
    }

    private void DrawFrostBurst(float radius, Color color, float t)
    {
        var spikeColor = color.Lightened(0.08f);
        spikeColor.A = color.A * 0.82f;
        DrawArc(Vector2.Zero, radius * 0.7f, 0f, Mathf.Tau, 24, spikeColor, Mathf.Lerp(4.2f, 1.4f, t));

        var coreColor = color.Lightened(0.16f);
        coreColor.A = color.A * 0.3f;
        DrawCircle(Vector2.Zero, radius * 0.12f, coreColor);

        for (var i = 0; i < 6; i++)
        {
            var angle = ((Mathf.Tau / 6f) * i) - (Mathf.Pi * 0.5f);
            var direction = Direction(angle);
            var perpendicular = new Vector2(-direction.Y, direction.X);
            var outer = direction * radius;
            var inner = direction * (radius * 0.18f);
            var branchBase = direction * (radius * 0.72f);
            DrawLine(inner, outer, spikeColor, Mathf.Lerp(4.2f, 1.3f, t), true);
            DrawLine(branchBase, branchBase + (perpendicular * radius * 0.12f), spikeColor, Mathf.Lerp(2.8f, 1.1f, t), true);
            DrawLine(branchBase, branchBase - (perpendicular * radius * 0.12f), spikeColor, Mathf.Lerp(2.8f, 1.1f, t), true);
        }
    }

    private void DrawLightningStrike(float radius, Color color, float t)
    {
        var boltColor = color.Lightened(0.18f);
        boltColor.A = color.A * 0.9f;
        var width = Mathf.Lerp(6f, 2f, t);
        var top = new Vector2(0f, -radius);
        var bendA = new Vector2(-radius * 0.18f, -radius * 0.34f);
        var bendB = new Vector2(radius * 0.14f, -radius * 0.04f);
        var bendC = new Vector2(-radius * 0.1f, radius * 0.34f);
        var bottom = new Vector2(radius * 0.04f, radius);

        DrawLine(top, bendA, boltColor, width, true);
        DrawLine(bendA, bendB, boltColor, width, true);
        DrawLine(bendB, bendC, boltColor, width, true);
        DrawLine(bendC, bottom, boltColor, width, true);

        DrawLine(
            new Vector2(-radius * 0.42f, -radius * 0.14f),
            new Vector2(-radius * 0.16f, radius * 0.08f),
            boltColor,
            width * 0.46f,
            true);
        DrawLine(
            new Vector2(radius * 0.38f, -radius * 0.08f),
            new Vector2(radius * 0.12f, radius * 0.18f),
            boltColor,
            width * 0.42f,
            true);
        DrawArc(Vector2.Zero, radius * 0.22f, 0f, Mathf.Tau, 18, color, Mathf.Lerp(4f, 1.4f, t));
    }

    private void DrawWardSigil(float radius, Color color, float t)
    {
        var outerWidth = Mathf.Lerp(4.4f, 1.6f, t);
        var outerRadius = radius * 0.82f;
        var innerRadius = radius * 0.52f;
        var sigilColor = color.Lightened(0.1f);
        sigilColor.A = color.A * 0.82f;
        DrawArc(Vector2.Zero, outerRadius, 0f, Mathf.Tau, 32, sigilColor, outerWidth);
        DrawArc(Vector2.Zero, innerRadius, 0f, Mathf.Tau, 32, sigilColor, outerWidth * 0.72f);

        var points = new Vector2[6];
        for (var i = 0; i < points.Length; i++)
        {
            var angle = ((Mathf.Tau / 6f) * i) - (Mathf.Pi * 0.5f);
            var direction = Direction(angle);
            points[i] = direction * outerRadius;
            DrawLine(direction * innerRadius, points[i], sigilColor, outerWidth * 0.52f, true);
            DrawCircle(points[i], Mathf.Lerp(4.5f, 1.8f, t), sigilColor);
        }

        for (var i = 0; i < points.Length; i++)
        {
            DrawLine(points[i], points[(i + 1) % points.Length], sigilColor, outerWidth * 0.44f, true);
        }

        var coreColor = color.Lightened(0.16f);
        coreColor.A = color.A * 0.28f;
        DrawCircle(Vector2.Zero, radius * 0.22f, coreColor);
    }

    private static Vector2 Direction(float angle)
    {
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }
}
