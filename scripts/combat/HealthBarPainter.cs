using Godot;

public enum HealthBarKind { Unit, Boss, Base }

// The real fill changes immediately. Only the amber damage chip is delayed.
public sealed class HealthBarMotion
{
    public float TrailRatio { get; private set; } = 1f;
    private float _previous = 1f;
    private float _hold;

    public void Reset(float ratio = 1f)
    {
        TrailRatio = _previous = Mathf.Clamp(ratio, 0f, 1f);
        _hold = 0f;
    }

    public void Update(float ratio, float delta, bool reducedMotion = false)
    {
        ratio = Mathf.Clamp(ratio, 0f, 1f);
        delta = Mathf.Max(0f, delta);
        if (reducedMotion) { Reset(ratio); return; }
        if (ratio < _previous - 0.0001f)
        {
            TrailRatio = Mathf.Max(TrailRatio, _previous);
            _hold = 0.18f;
        }
        if (ratio > _previous) _hold = 0f;
        var remaining = Mathf.Max(0f, delta - _hold);
        _hold = Mathf.Max(0f, _hold - delta);
        TrailRatio = Mathf.Max(ratio, Mathf.MoveToward(TrailRatio, ratio, remaining * 0.75f));
        _previous = ratio;
    }
}

// Shared textured UI art, sliced into three pieces so the end caps never stretch.
public static class HealthBarPainter
{
    private static Texture2D _frame;
    private static Texture2D _fill;
    public static Color Friendly(bool highContrast) => new(highContrast ? "78e3ff" : "77d6aa");
    public static Color Hostile(bool highContrast) => new(highContrast ? "ffbe6a" : "e46c78");

    public static Rect2 Well(Rect2 bounds)
    {
        var cap = Mathf.Min(bounds.Size.Y * 0.6f, bounds.Size.X * 0.2f);
        return new Rect2(bounds.Position + new Vector2(cap, bounds.Size.Y * 0.3f),
            new Vector2(Mathf.Max(0, bounds.Size.X - cap * 2), bounds.Size.Y * 0.4f));
    }

    public static void Draw(CanvasItem canvas, Rect2 bounds, float ratio, float trailRatio,
        bool friendly, HealthBarKind kind = HealthBarKind.Unit, bool highContrast = false)
    {
        if (bounds.Size.X <= 0f || bounds.Size.Y <= 0f) return;
        ratio = Mathf.Clamp(ratio, 0f, 1f);
        trailRatio = Mathf.Clamp(trailRatio, ratio, 1f);
        _frame ??= GD.Load<Texture2D>("res://assets/ui/bars/health_frame.svg");
        _fill ??= GD.Load<Texture2D>("res://assets/ui/bars/health_fill.svg");
        var frameTint = kind == HealthBarKind.Unit ? new Color("b3c4c3") : new Color("efd093");
        if (highContrast) frameTint = new Color("fff1c8");
        var cap = Mathf.Min(bounds.Size.Y * 0.6f, bounds.Size.X * 0.2f);
        if (_frame != null)
        {
            var size = _frame.GetSize();
            var sourceCap = size.X * 24f / 256f;
            canvas.DrawTextureRectRegion(_frame, new Rect2(bounds.Position, new Vector2(cap, bounds.Size.Y)),
                new Rect2(0, 0, sourceCap, size.Y), frameTint);
            canvas.DrawTextureRectRegion(_frame, new Rect2(bounds.Position + new Vector2(cap, 0), new Vector2(bounds.Size.X - 2 * cap, bounds.Size.Y)),
                new Rect2(sourceCap, 0, size.X - sourceCap * 2, size.Y), frameTint);
            canvas.DrawTextureRectRegion(_frame, new Rect2(bounds.End.X - cap, bounds.Position.Y, cap, bounds.Size.Y),
                new Rect2(size.X - sourceCap, 0, sourceCap, size.Y), frameTint);
        }
        else
        {
            canvas.DrawRect(bounds, new Color("111c1d"));
            canvas.DrawRect(bounds, frameTint.Darkened(0.3f), false, 1f);
        }

        var well = Well(bounds);
        var color = friendly ? Friendly(highContrast) : Hostile(highContrast);
        if (trailRatio > ratio + 0.0001f)
            Fill(canvas, well, trailRatio, new Color(highContrast ? "fff2bf" : "c3a56e"));
        Fill(canvas, well, ratio, color);
        if (ratio > 0f && ratio < 1f)
        {
            var edgeX = well.Position.X + well.Size.X * ratio;
            canvas.DrawLine(new Vector2(edgeX, well.Position.Y), new Vector2(edgeX, well.End.Y), color.Lightened(0.38f), 0.6f);
        }
        if (kind != HealthBarKind.Unit)
        {
            for (var i = 1; i < 4; i++)
            {
                var x = well.Position.X + well.Size.X * i / 4f;
                canvas.DrawLine(new Vector2(x, well.Position.Y), new Vector2(x, well.End.Y), new Color(0.03f, 0.08f, 0.08f, 0.48f), 0.75f);
            }
        }
        if (kind == HealthBarKind.Boss)
        {
            var center = new Vector2(bounds.GetCenter().X, bounds.Position.Y + 0.5f);
            canvas.DrawColoredPolygon(new[] { center + new Vector2(-4, 0), center + new Vector2(-5, -4),
                center + new Vector2(-1.5f, -2), center + new Vector2(0, -5), center + new Vector2(1.5f, -2),
                center + new Vector2(5, -4), center + new Vector2(4, 0) }, new Color("d5ba7a"));
        }
    }

    private static void Fill(CanvasItem canvas, Rect2 well, float ratio, Color color)
    {
        if (ratio <= 0f) return;
        var fillRect = new Rect2(well.Position, new Vector2(well.Size.X * ratio, well.Size.Y));
        if (_fill != null)
            canvas.DrawTextureRectRegion(_fill, fillRect, new Rect2(0, 0, _fill.GetWidth() * ratio, _fill.GetHeight()), color);
        else
            canvas.DrawRect(fillRect, color);
    }
}
