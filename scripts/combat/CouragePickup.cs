using Godot;

// Battle-owned lifetime: pausing, checkpoints and start barriers stop both expiry and collection flight.
public partial class CouragePickup : Node2D
{
    public float Amount { get; private set; }
    public float RemainingSeconds { get; private set; }
    public bool Collected { get; private set; }
    public bool Available => !Collected && RemainingSeconds > 0f && !IsQueuedForDeletion();
    public bool Finished => Collected ? _flightTime >= FlightDuration : RemainingSeconds <= 0f;
    public float FadeAlpha => Collected ? 1f - _flightTime / FlightDuration :
        _fadeDuration <= 0f ? 1f : Mathf.Clamp(RemainingSeconds / _fadeDuration, 0f, 1f);

    private const float FlightDuration = 0.32f;
    private float _fadeDuration, _age, _flightTime;
    private Vector2 _flightStart;

    public void Setup(Vector2 position, float amount, float lifetime, float fadeDuration)
    {
        Position = position;
        Amount = amount;
        RemainingSeconds = lifetime;
        _fadeDuration = fadeDuration;
        ZIndex = 450;
        // No input Control over the lane: the controller resolves pickup taps before aimed magic.
        SetProcess(false);
        QueueRedraw();
    }

    public void Collect()
    {
        Collected = true;
        _flightStart = Position;
        QueueRedraw();
    }

    public void Tick(float delta, Vector2 meterPosition, bool reducedMotion)
    {
        _age += delta;
        if (Collected)
        {
            _flightTime = Mathf.Min(FlightDuration, _flightTime + delta);
            if (!reducedMotion)
            {
                var t = _flightTime / FlightDuration;
                Position = _flightStart.Lerp(meterPosition, t * t);
            }
        }
        else RemainingSeconds = Mathf.Max(0f, RemainingSeconds - delta);
        Modulate = new Color(1f, 1f, 1f, FadeAlpha);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var reduced = GameState.Instance?.ReducedMotion ?? false;
        var bob = reduced || Collected ? 0f : Mathf.Sin(_age * 3f) * 1.2f;
        var center = new Vector2(0, -7 + bob);
        if (!Collected)
        {
            DrawSetTransform(new Vector2(0, 4), 0, new Vector2(1, .35f));
            DrawCircle(Vector2.Zero, 11, new Color(0, 0, 0, .32f));
            DrawSetTransform(Vector2.Zero);
        }
        // Brass caravan seal, with the ember-flame emblem used by the courage meter.
        DrawCircle(center, 12, new Color("24160f"));
        DrawCircle(center, 10.5f, new Color("b88137"));
        DrawCircle(center, 8.4f, new Color("5d321c"));
        DrawArc(center, 10.5f, Mathf.Pi * 1.1f, Mathf.Pi * 1.9f, 20, new Color("ffe2a1"), 1.7f, true);
        var flame = new[] { center + new Vector2(-4, 4), center + new Vector2(-5, 0),
            center + new Vector2(-2, -4), center + new Vector2(0, -6), center + new Vector2(1, -1),
            center + new Vector2(4, -3), center + new Vector2(5, 2), center + new Vector2(2, 5) };
        DrawColoredPolygon(flame, new Color("ffd18b"));
        DrawCircle(center + new Vector2(0, 2), 2, new Color("f49345"));
        if (GameState.Instance?.HighContrast ?? false)
            DrawArc(center, 12, 0, Mathf.Tau, 36, new Color("fff0cc"), 1.6f, true);
    }
}
