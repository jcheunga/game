using Godot;

public partial class EndlessContactActor : Node2D
{
    private string _contactId = "";
    private Color _accentColor = Colors.White;
    private float _radius = 72f;
    private float _maxHealth = 100f;
    private float _health = 100f;
    private float _progressRatio;
    private bool _playerInside;
    private bool _enemyInside;
    private bool _completed;
    private bool _failed;
    private float _flashTimer;
    private float _pulseTimer;

    public float MaxHealth => _maxHealth;
    public float Health => _health;
    public float HealthRatio => _maxHealth <= 0.01f ? 0f : Mathf.Clamp(_health / _maxHealth, 0f, 1f);

    public void Setup(string contactId, Color accentColor, float radius, float maxHealth)
    {
        _contactId = contactId ?? "";
        _accentColor = accentColor;
        _radius = Mathf.Max(24f, radius);
        _maxHealth = Mathf.Max(1f, maxHealth);
        _health = _maxHealth;
    }

    public void UpdateState(float progressRatio, bool playerInside, bool enemyInside, bool completed, bool failed)
    {
        _progressRatio = Mathf.Clamp(progressRatio, 0f, 1f);
        _playerInside = playerInside;
        _enemyInside = enemyInside;
        _completed = completed;
        _failed = failed;
        QueueRedraw();
    }

    public float ApplyPressureDamage(float amount)
    {
        if (_completed || _failed || amount <= 0f || _health <= 0f)
        {
            return 0f;
        }

        var previous = _health;
        _health = Mathf.Max(0f, _health - amount);
        if (_health < previous)
        {
            _flashTimer = 0.18f;
        }

        QueueRedraw();
        return previous - _health;
    }

    public float Repair(float amount)
    {
        if (amount <= 0f || _health >= _maxHealth)
        {
            return 0f;
        }

        var previous = _health;
        _health = Mathf.Min(_maxHealth, _health + amount);
        QueueRedraw();
        return _health - previous;
    }

    public override void _Process(double delta)
    {
        var deltaF = (float)delta;
        _pulseTimer += deltaF;
        _flashTimer = Mathf.Max(0f, _flashTimer - deltaF);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var drawColor = ResolveDrawColor();
        var ringAlpha = _completed
            ? 0.7f
            : _failed
                ? 0.18f
                : 0.34f + (Mathf.Sin(_pulseTimer * 4.2f) * 0.08f);
        var ringColor = new Color(drawColor, Mathf.Clamp(ringAlpha, 0.12f, 0.78f));

        DrawCircle(Vector2.Zero, _radius, new Color(drawColor, _failed ? 0.04f : 0.1f));
        DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, 32, ringColor, 3f);
        DrawCircle(Vector2.Zero, 8f, ringColor.Lightened(0.15f));

        if (!_failed)
        {
            var progressRadius = Mathf.Lerp(18f, _radius - 8f, _progressRatio);
            DrawArc(Vector2.Zero, progressRadius, -Mathf.Pi * 0.5f, -Mathf.Pi * 0.5f + (Mathf.Tau * _progressRatio), 32, ringColor.Lightened(0.18f), 5f);
        }

        DrawLine(new Vector2(-10f, 0f), new Vector2(10f, 0f), ringColor, 2f, true);
        DrawLine(new Vector2(0f, -10f), new Vector2(0f, 10f), ringColor, 2f, true);
        DrawHealthMeter(drawColor);
        DrawProp(drawColor);
    }

    private Color ResolveDrawColor()
    {
        var color = _accentColor
            .Lightened(_playerInside ? 0.08f : 0f)
            .Darkened(_enemyInside ? 0.12f : 0f);

        if (_flashTimer > 0f)
        {
            color = color.Lerp(Colors.White, Mathf.Clamp(_flashTimer / 0.18f, 0f, 1f) * 0.42f);
        }

        if (_failed)
        {
            color = color.Lerp(new Color("6c757d"), 0.45f);
        }

        return color;
    }

    private void DrawHealthMeter(Color drawColor)
    {
        var ratio = HealthRatio;
        var origin = new Vector2(-24f, -_radius - 22f);
        DrawRect(new Rect2(origin, new Vector2(48f, 7f)), new Color(0f, 0f, 0f, 0.55f), true);
        DrawRect(new Rect2(origin + new Vector2(1f, 1f), new Vector2(46f * ratio, 5f)), drawColor.Lightened(0.08f), true);
        DrawRect(new Rect2(origin, new Vector2(48f, 7f)), drawColor.Darkened(0.18f), false, 2f);
    }

    private void DrawProp(Color drawColor)
    {
        switch (_contactId)
        {
            case EndlessContactCatalog.RelaySignalId:
                DrawRelaySignal(drawColor);
                break;
            case EndlessContactCatalog.SalvageCacheId:
                DrawSalvageCache(drawColor);
                break;
            case EndlessContactCatalog.SafehouseRescueId:
                DrawSafehouseRescue(drawColor);
                break;
        }
    }

    private void DrawRelaySignal(Color drawColor)
    {
        var mastBase = new Vector2(0f, 18f);
        var mastTop = new Vector2(0f, -40f);
        var boxColor = _failed ? new Color("495057") : new Color("1f2a44");

        DrawRect(new Rect2(new Vector2(-18f, 10f), new Vector2(36f, 18f)), boxColor, true);
        DrawRect(new Rect2(new Vector2(-12f, 4f), new Vector2(24f, 8f)), drawColor.Darkened(0.25f), true);
        DrawLine(mastBase, mastTop, drawColor, 4f, true);
        DrawLine(mastTop, mastTop + new Vector2(-12f, 10f), drawColor, 3f, true);
        DrawLine(mastTop, mastTop + new Vector2(12f, 10f), drawColor, 3f, true);
        DrawCircle(mastTop + new Vector2(0f, -6f), 6f, drawColor.Lightened(0.1f));

        var signalAlpha = _failed ? 0.08f : 0.18f + (_progressRatio * 0.24f);
        var pulseHeight = 62f + (_progressRatio * 18f);
        var signalOrigin = mastTop + new Vector2(0f, -pulseHeight);
        DrawLine(mastTop + new Vector2(0f, -6f), signalOrigin, new Color(drawColor, signalAlpha), 3f, true);
        DrawArc(signalOrigin, 16f + (_progressRatio * 8f), Mathf.Pi * 0.18f, Mathf.Pi * 0.82f, 16, new Color(drawColor, signalAlpha), 2.5f);
        DrawArc(signalOrigin + new Vector2(0f, 4f), 28f + (_progressRatio * 10f), Mathf.Pi * 0.2f, Mathf.Pi * 0.8f, 18, new Color(drawColor, signalAlpha * 0.8f), 2f);

        var operatorColor = _enemyInside ? new Color("adb5bd") : new Color("e9ecef");
        DrawCivilianSilhouette(new Vector2(-26f, 22f), operatorColor, 0.95f, false);
    }

    private void DrawSalvageCache(Color drawColor)
    {
        var crateColor = _failed ? new Color("6c757d") : new Color("7f5539");
        var lidColor = _completed ? drawColor.Lightened(0.18f) : new Color("b08968");

        DrawRect(new Rect2(new Vector2(-30f, -6f), new Vector2(28f, 22f)), crateColor, true);
        DrawRect(new Rect2(new Vector2(2f, -14f), new Vector2(30f, 30f)), crateColor.Lightened(0.05f), true);
        DrawRect(new Rect2(new Vector2(-30f, -10f), new Vector2(28f, 5f)), lidColor, true);
        DrawRect(new Rect2(new Vector2(2f, -18f), new Vector2(30f, 5f)), lidColor, true);
        DrawCircle(new Vector2(-14f, 24f), 8f, new Color("495057"));
        DrawCircle(new Vector2(18f, 24f), 8f, new Color("495057"));
        DrawLine(new Vector2(-14f, 24f), new Vector2(18f, 24f), new Color("343a40"), 4f, true);

        if (_completed)
        {
            var offset = new Vector2(42f + (6f * Mathf.Sin(_pulseTimer * 2.4f)), -18f);
            DrawRect(new Rect2(offset + new Vector2(-10f, -8f), new Vector2(20f, 16f)), drawColor.Lightened(0.12f), true);
            DrawLine(new Vector2(18f, 6f), offset, new Color(drawColor, 0.28f), 2f, true);
        }
        else if (!_failed)
        {
            var lampAlpha = 0.26f + (0.12f * Mathf.Sin(_pulseTimer * 5.8f));
            DrawCircle(new Vector2(34f, -20f), 6f, new Color(drawColor, lampAlpha));
        }
    }

    private void DrawSafehouseRescue(Color drawColor)
    {
        var wallColor = _failed ? new Color("4f5d75") : new Color("5a677d");
        var doorColor = _completed ? drawColor.Lightened(0.08f) : new Color("ced4da");

        DrawRect(new Rect2(new Vector2(-34f, -26f), new Vector2(68f, 52f)), wallColor, true);
        DrawRect(new Rect2(new Vector2(-12f, -6f), new Vector2(24f, 32f)), doorColor, true);
        DrawRect(new Rect2(new Vector2(-24f, -16f), new Vector2(12f, 10f)), drawColor.Lightened(0.05f), true);
        DrawRect(new Rect2(new Vector2(12f, -16f), new Vector2(12f, 10f)), drawColor.Lightened(0.05f), true);

        if (_completed)
        {
            for (var i = 0; i < 3; i++)
            {
                var start = new Vector2(-18f + (i * 14f), 30f);
                var target = new Vector2(88f + (i * 14f), 42f + ((i % 2) * 10f));
                var travel = Mathf.Clamp(0.3f + (0.12f * i) + (0.08f * Mathf.Sin((_pulseTimer * 2.2f) + i)), 0f, 0.88f);
                DrawCivilianSilhouette(start.Lerp(target, travel), drawColor.Lightened(0.15f), 0.88f, i == 1);
            }
        }
        else
        {
            DrawCivilianSilhouette(new Vector2(-16f, 30f), new Color("f8f9fa"), 0.86f, false);
            DrawCivilianSilhouette(new Vector2(0f, 30f), new Color("dee2e6"), 0.92f, true);
            DrawCivilianSilhouette(new Vector2(16f, 30f), new Color("f8f9fa"), 0.82f, false);
        }

        if (_failed)
        {
            DrawSmoke(new Vector2(0f, -24f), wallColor);
        }
        else
        {
            var signalAlpha = 0.18f + (0.08f * Mathf.Sin(_pulseTimer * 4.9f));
            DrawCircle(new Vector2(0f, -34f), 8f + (_progressRatio * 3f), new Color(drawColor, signalAlpha));
        }
    }

    private void DrawCivilianSilhouette(Vector2 position, Color color, float scale, bool carrying)
    {
        DrawCircle(position + new Vector2(0f, -12f * scale), 4.5f * scale, color);
        DrawRect(new Rect2(position + new Vector2(-4f * scale, -8f * scale), new Vector2(8f * scale, 14f * scale)), color.Darkened(0.08f), true);
        DrawLine(position + new Vector2(-2f * scale, 6f * scale), position + new Vector2(-6f * scale, 14f * scale), color, 2f, true);
        DrawLine(position + new Vector2(2f * scale, 6f * scale), position + new Vector2(6f * scale, 14f * scale), color, 2f, true);
        DrawLine(position + new Vector2(-4f * scale, -2f * scale), position + new Vector2(-8f * scale, 6f * scale), color, 2f, true);
        DrawLine(position + new Vector2(4f * scale, -2f * scale), position + new Vector2(8f * scale, 6f * scale), color, 2f, true);

        if (carrying)
        {
            DrawRect(new Rect2(position + new Vector2(8f * scale, -6f * scale), new Vector2(6f * scale, 6f * scale)), color.Lightened(0.08f), true);
        }
    }

    private void DrawSmoke(Vector2 origin, Color sourceColor)
    {
        var plumeOffset = Mathf.Sin(_pulseTimer * 1.8f) * 6f;
        DrawCircle(origin + new Vector2(plumeOffset, -12f), 14f, new Color(sourceColor.Darkened(0.7f), 0.22f));
        DrawCircle(origin + new Vector2(-4f + (plumeOffset * 0.4f), -24f), 20f, new Color(0f, 0f, 0f, 0.18f));
    }
}
