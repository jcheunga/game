using System.Collections.Generic;
using Godot;

/// <summary>Painted materials and scene vignettes, kept in two production atlases.</summary>
public static class ModalArt
{
    private static readonly Dictionary<string, Texture2D> Cache = new();
    public static Texture2D Material(int index) => Region("materials-v1", index, 2, 2);
    public static Texture2D Illustration(int index) => Region("illustrations-v1", index, 3, 2);
    private static Texture2D Region(string asset, int index, int columns, int rows)
    {
        var key = asset + index;
        if (Cache.TryGetValue(key, out var cached)) return cached;
        var path = $"res://assets/ui/modal/{asset}.png";
        if (!ResourceLoader.Exists(path)) return null;
        var atlas = ResourceLoader.Load<Texture2D>(path);
        var cell = atlas.GetSize() / new Vector2(columns, rows);
        return Cache[key] = new AtlasTexture { Atlas = atlas, Region = new Rect2(new Vector2(index % columns, index / columns) * cell, cell), FilterClip = true };
    }
}
