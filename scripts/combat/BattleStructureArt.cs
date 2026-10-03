using Godot;
using System.Collections.Generic;
using System.Text.Json;

public sealed record BattleStructureArt(Texture2D Texture, Vector2 Anchor, float Width, Vector2[] Mounts, Vector2[] Contacts, Vector2[] Lights, Vector2 Smoke)
{
    private static readonly Dictionary<string, BattleStructureArt> Cache = new();
    public Vector2 Size => Texture.GetSize() * (Width / Texture.GetWidth());
    public Rect2 At(Vector2 ground) => new(ground - Anchor * Size, Size);
    public Vector2 Point(Vector2 ground, Vector2 uv) => ground + (uv - Anchor) * Size;

    public static BattleStructureArt Load(string id)
    {
        if (Cache.TryGetValue(id, out var art)) return art;
        var path = "res://assets/structures/battle-v2/" + id;
        if (!ResourceLoader.Exists(path + ".png") || !Godot.FileAccess.FileExists(path + ".json")) return null;
        using var data = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(path + ".json"));
        var root = data.RootElement;
        static Vector2 Point(JsonElement point) => new(point[0].GetSingle(), point[1].GetSingle());
        static Vector2[] Points(JsonElement array)
        {
            var points = new List<Vector2>();
            foreach (var point in array.EnumerateArray()) points.Add(Point(point));
            return points.ToArray();
        }
        return Cache[id] = new(ResourceLoader.Load<Texture2D>(path + ".png"), Point(root.GetProperty("anchor")),
            root.GetProperty("width").GetSingle(), Points(root.GetProperty("mounts")), Points(root.GetProperty("contacts")), Points(root.GetProperty("lights")), Point(root.GetProperty("smoke")));
    }
    public static void ClearCache() => Cache.Clear();
}
