using Godot;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>The caravan's troop door: opening frames cropped to <see cref="Rect"/> (normalised over the plate),
/// laid side by side in <see cref="Strip"/>. Troops appear at <see cref="Exit"/> and leave the ramp at <see cref="Foot"/>.</summary>
public sealed record BattleDoorArt(Texture2D Strip, Rect2 Rect, int Frames, Vector2 Exit, Vector2 Foot);

public sealed record BattleStructureArt(Texture2D Texture, Vector2 Anchor, float Width, Vector2[] Mounts, Vector2[] Contacts, Vector2[] Lights, Vector2 Smoke)
{
    public BattleDoorArt Door { get; init; }
    // Alpha centroid of the plate (normalised), used to centre a structure's visual mass on the band.
    public Vector2 Centre { get; init; } = new(.5f, .5f);
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
            root.GetProperty("width").GetSingle() * GameData.Combat.StructureScale, Points(root.GetProperty("mounts")), Points(root.GetProperty("contacts")), Points(root.GetProperty("lights")), Point(root.GetProperty("smoke")))
        {
            Centre = root.TryGetProperty("centre", out var centre) ? Point(centre) : new Vector2(.5f, .5f),
            Door = LoadDoor(path, root)
        };
    }
    private static BattleDoorArt LoadDoor(string path, JsonElement root)
    {
        if (!root.TryGetProperty("door", out var door) || !ResourceLoader.Exists(path + "_door.png")) return null;
        var rect = door.GetProperty("rect");
        return new BattleDoorArt(ResourceLoader.Load<Texture2D>(path + "_door.png"),
            new Rect2(rect[0].GetSingle(), rect[1].GetSingle(), rect[2].GetSingle() - rect[0].GetSingle(), rect[3].GetSingle() - rect[1].GetSingle()),
            door.GetProperty("frames").GetInt32(), new Vector2(door.GetProperty("exit")[0].GetSingle(), door.GetProperty("exit")[1].GetSingle()),
            new Vector2(door.GetProperty("foot")[0].GetSingle(), door.GetProperty("foot")[1].GetSingle()));
    }

    // Draws door frame `frame` (0-based) over the plate drawn in `plate`.
    public void DrawDoor(CanvasItem canvas, Rect2 plate, int frame, Color modulate)
    {
        if (Door == null || frame < 0) return;
        var size = new Vector2(Door.Strip.GetWidth() / (float)Door.Frames, Door.Strip.GetHeight());
        canvas.DrawTextureRectRegion(Door.Strip, new Rect2(plate.Position + Door.Rect.Position * plate.Size, Door.Rect.Size * plate.Size),
            new Rect2(new Vector2(size.X * Mathf.Min(frame, Door.Frames - 1), 0), size), modulate);
    }

    public static void ClearCache() => Cache.Clear();
}
