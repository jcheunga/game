using System.Linq;
using Godot;

/// <summary>The painted battle backdrops (art/royal/backdrops.py): layered parallax scenery for each zone.</summary>
public static class WorldEnvironmentArt
{
    public const string RoyalBackdropDirectory = "res://assets/world/royal/";

    /// <summary>A zone's painted battle backdrop, back to front, or null when the zone has none. Each layer
    /// covers its battle-world rect while the camera is centred on the field and scrolls at Parallax times the
    /// camera's speed.</summary>
    public static ZoneBackdrop LoadZoneBackdrop(string zoneId)
    {
        var path = RoyalBackdropDirectory + zoneId;
        if (!Godot.FileAccess.FileExists(path + ".json")) return null;
        using var meta = System.Text.Json.JsonDocument.Parse(Godot.FileAccess.GetFileAsString(path + ".json"));
        var root = meta.RootElement;
        static Rect2 Rect(System.Text.Json.JsonElement r) => new(r[0].GetSingle(), r[1].GetSingle(), r[2].GetSingle(), r[3].GetSingle());
        var layers = new System.Collections.Generic.List<BackdropLayer>();
        foreach (var layer in root.GetProperty("layers").EnumerateArray())
        {
            var file = RoyalBackdropDirectory + layer.GetProperty("file").GetString();
            if (!ResourceLoader.Exists(file)) continue;
            var tile = layer.TryGetProperty("tile", out var tiled) && tiled.GetBoolean();
            var front = layer.TryGetProperty("front", out var fore) && fore.GetBoolean();
            layers.Add(new(ResourceLoader.Load<Texture2D>(file), Rect(layer.GetProperty("rect")), layer.GetProperty("parallax").GetSingle(), tile, front));
        }
        if (layers.Count == 0) return null;
        var sky = root.TryGetProperty("sky", out var hex) ? new Color(hex.GetString()) : new Color("8aa4c0");
        return new ZoneBackdrop(layers, sky);
    }
}

/// <summary>One painted depth of a zone backdrop. Tiled layers repeat along the field; front layers
/// are drawn in front of the troops.</summary>
public sealed record BackdropLayer(Texture2D Texture, Rect2 Rect, float Parallax, bool Tile = false, bool Front = false);

/// <summary>A zone's backdrop layers, far to near, and the sky colour above the farthest one.</summary>
public sealed record ZoneBackdrop(System.Collections.Generic.IReadOnlyList<BackdropLayer> Layers, Color Sky)
{
    public Texture2D Near => Layers.LastOrDefault(layer => !layer.Front)?.Texture ?? Layers[^1].Texture;
}
