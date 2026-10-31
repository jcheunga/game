using Godot;

/// <summary>
/// Touch reach for buttons drawn smaller than a fingertip: on phones they still take a 72 px fingertip (about 39 pt)
/// of the screen, whatever scale their layer is drawn at. <see cref="RoyalButton"/> always reaches; a
/// <see cref="RealmButton"/> reaches when it sets <see cref="RealmButton.FingertipReach"/>.
/// </summary>
public static class TouchReach
{
    public const float Size = 72;

    public static bool Reaches(Control button) => button is RoyalButton or RealmButton { FingertipReach: true };

    /// <summary>The area a tap counts in, in the button's own coordinates: its rect, grown about its centre to the reach.</summary>
    public static Rect2 Rect(Control button)
    {
        var rect = new Rect2(Vector2.Zero, button.Size);
        if (!Reaches(button)) return rect;
        var scale = button.IsInsideTree() ? button.GetGlobalTransformWithCanvas().Scale.Abs() : Vector2.One;
        var reach = new Vector2(Size / Mathf.Max(.01f, scale.X), Size / Mathf.Max(.01f, scale.Y));
        var grow = new Vector2(Mathf.Max(0, reach.X - button.Size.X), Mathf.Max(0, reach.Y - button.Size.Y)) / 2;
        return new Rect2(rect.Position - grow, rect.Size + grow * 2);
    }

    public static bool Has(Control button, Vector2 point)
    {
        if (new Rect2(Vector2.Zero, button.Size).HasPoint(point)) return true;
        if (!Rect(button).HasPoint(point)) return false;
        // The extra reach never takes a tap that lands on a neighbouring button itself (stacked list rows).
        var global = button.GetGlobalTransform() * point;
        foreach (var sibling in button.GetParent()?.GetChildren() ?? new Godot.Collections.Array<Node>())
            if (sibling != button && sibling is BaseButton { Visible: true } other && other.GetGlobalRect().HasPoint(global)) return false;
        return true;
    }
}
