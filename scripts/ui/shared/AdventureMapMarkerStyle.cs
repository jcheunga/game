using Godot;

/// <summary>Map hit targets follow the painted landmark and its foundation.</summary>
public static class AdventureMapMarkerStyle
{
    public static bool Contains(Vector2 point, Vector2 ground, AdventureTile tile)
    {
        var delta = point - ground;
        // The foundation remains an easy touch target even where the painting has soft shadow pixels.
        if (new Vector2(delta.X / 25, delta.Y / 14).LengthSquared() <= 1) return true;
        return MapPathCanvas.RoyalLandmarkContains(tile, delta);
    }
}
