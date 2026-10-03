using Godot;

/// <summary>Map hit targets follow the painted object and its foundation.</summary>
public static class AdventureMapMarkerStyle
{
    public static bool Contains(Vector2 point, Vector2 ground, AdventureTile tile)
    {
        var delta = point - ground;
        // The foundation remains an easy touch target even where an atlas has soft shadow pixels.
        if (new Vector2(delta.X / 25, delta.Y / 14).LengthSquared() <= 1) return true;
        var index = AdventureAtlasArt.LandmarkSprite(tile);
        var height = AdventureAtlasArt.LandmarkHeight(tile);
        var texture = AdventureAtlasArt.Sprite(index);
        var size = texture == null ? new Vector2(height, height) : texture.GetSize() * (height / texture.GetSize().Y);
        return AdventureAtlasArt.SpriteContains(index, delta / size + AdventureAtlasArt.GroundAnchor(index));
    }

}
