using System.Collections.Generic;
using Godot;

/// <summary>Painted home-screen assets, with scalable icons as an import-time fallback.</summary>
public static class HomeMapArt
{
    public const string IconAtlasPath = "res://assets/ui/home/painted-icons-v2.png";
    private static Texture2D _atlas;
    private static readonly Dictionary<string, Texture2D> Icons = new();
    private static readonly Dictionary<string, int> Cells = new()
    {
        ["map"] = 0, ["sword"] = 1, ["flame"] = 2, ["hammer"] = 3, ["book"] = 4,
        ["people"] = 5, ["gold"] = 6, ["food"] = 7, ["star"] = 8
    };

    public static Texture2D Icon(string id)
    {
        if (!Cells.TryGetValue(id, out var cell) || !ResourceLoader.Exists(IconAtlasPath)) return RealmUi.Icon(id);
        if (Icons.TryGetValue(id, out var icon)) return icon;
        _atlas ??= ResourceLoader.Load<Texture2D>(IconAtlasPath);
        var size = _atlas.GetSize() / 3;
        return Icons[id] = new AtlasTexture { Atlas = _atlas, Region = new Rect2(new Vector2(cell % 3, cell / 3) * size, size) };
    }
}
