using Godot;

public static class BattleGroundPlane
{
    // Ground effects share the shallow angle of the authored battlefield.
    public const float DepthScale = .42f;

    public static void Ring(CanvasItem canvas, Vector2 center, float radius, Color color, float width = 2f)
    {
        canvas.DrawSetTransform(center, 0, new Vector2(1f, DepthScale));
        canvas.DrawArc(Vector2.Zero, radius, 0, Mathf.Tau, 64, color, width, true);
        canvas.DrawSetTransform(Vector2.Zero);
    }
}
