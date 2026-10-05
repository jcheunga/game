using Godot;

/// <summary>Authored scene plates; playable ground is mapped to the simulation's exact bounds.</summary>
public static class WorldEnvironmentArt
{
    public const string BattleDirectory = "res://assets/world/battles/";

    public static readonly Rect2 BattleFloorSource = new(.08f, .27f, .84f, .43f);
    public static string BattlePath(int stage) => $"{BattleDirectory}stage-{stage:00}.png";

    public static Texture2D LoadBattle(int stage) => Load(BattlePath(stage));

    public const string BackdropDirectory = "res://assets/world/backdrops/";

    /// <summary>A zone's layered Blender battle backdrop (art/remaster/build_backdrops.py), back to front, or null
    /// when the zone has none. Each layer covers its battle-world rect while the camera is centred on the field
    /// and scrolls at Parallax times the camera's speed.</summary>
    public static ZoneBackdrop LoadZoneBackdrop(string zoneId)
    {
        var path = BackdropDirectory + zoneId;
        if (!Godot.FileAccess.FileExists(path + ".json")) return null;
        using var meta = System.Text.Json.JsonDocument.Parse(Godot.FileAccess.GetFileAsString(path + ".json"));
        var root = meta.RootElement;
        static Rect2 Rect(System.Text.Json.JsonElement r) => new(r[0].GetSingle(), r[1].GetSingle(), r[2].GetSingle(), r[3].GetSingle());
        var layers = new System.Collections.Generic.List<BackdropLayer>();
        if (root.TryGetProperty("layers", out var list))
            foreach (var layer in list.EnumerateArray())
            {
                var texture = Load(BackdropDirectory + layer.GetProperty("file").GetString());
                if (texture != null) layers.Add(new(texture, Rect(layer.GetProperty("rect")), layer.GetProperty("parallax").GetSingle()));
            }
        else if (Load(path + ".png") is { } single)
            layers.Add(new(single, Rect(root.GetProperty("rect")), 1f));
        if (layers.Count == 0) return null;
        var sky = root.TryGetProperty("sky", out var hex) ? new Color(hex.GetString()) : new Color("8aa4c0");
        return new ZoneBackdrop(layers, sky);
    }

    private static Texture2D Load(string path) => ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;

    // Scene plates cover the whole battle view at their own proportions, the way a backdrop frames a
    // stage: the bases stand on the plate's floor and its scenery fills the screen above and below.
    public static void DrawBattle(CanvasItem canvas, Texture2D texture, Rect2 cover) =>
        canvas.DrawTextureRect(texture, CoverRect(texture.GetSize(), cover), false);

    public static Rect2 CoverRect(Vector2 sourceSize, Rect2 cover)
    {
        var size = sourceSize * Mathf.Max(cover.Size.X / sourceSize.X, cover.Size.Y / sourceSize.Y);
        return new Rect2(cover.GetCenter() - size * .5f, size);
    }

    public static Color WaterColor(string zone) => new(zone switch
    {
        "foundry" => "59463d", "quarantine" => "405953", "thornwall" => "506775",
        "basilica" => "4d565b", "mire" => "304b43", "steppe" => "655b43",
        "gloamwood" => "334e4c", "citadel" => "415c6d", _ => "355d6c"
    });
}

/// <summary>One painted depth of a zone backdrop.</summary>
public sealed record BackdropLayer(Texture2D Texture, Rect2 Rect, float Parallax);

/// <summary>A zone's backdrop layers, far to near, and the sky colour above the farthest one.</summary>
public sealed record ZoneBackdrop(System.Collections.Generic.IReadOnlyList<BackdropLayer> Layers, Color Sky)
{
    public Texture2D Near => Layers[^1].Texture;
}
