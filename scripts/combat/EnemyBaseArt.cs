using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>A stage's painted enemy stronghold (art/royal/bases.py): a cut-out painted into its zone's battle scene,
/// with its soft contact shadow baked in. <see cref="Rect"/> is where it stands in the world; it sits behind the
/// band like the scenery it was painted into, so every troop on the road passes in front of it.</summary>
public sealed record EnemyBaseArt(Texture2D Texture, Rect2 Rect, string Zone, (Vector2 Point, Color Colour)[] Lights, Vector2[] Smoke)
{
    /// <summary>World row where the building meets the ground (its baked shadow reaches a little lower).</summary>
    public float Foot { get; init; }
    /// <summary>World x of the gate's centre, where troops strike it and its health meter hangs.</summary>
    public float GateX { get; init; }
    private const string Folder = "res://assets/structures/bases/";
    private static readonly Dictionary<int, EnemyBaseArt> Cache = new();

    /// <summary>The stronghold for a stage, or, when that stage has none painted for the zone on screen (endless runs,
    /// boss rushes and events borrow a stage number), the zone's final stronghold, else any painted for the zone.</summary>
    public static EnemyBaseArt Resolve(int stageNumber, string zone)
    {
        if (Load(stageNumber) is { } own && own.Zone == zone) return own;
        foreach (var stage in GameData.Stages.Where(s => s.MapId == zone).OrderByDescending(s => s.StageNumber))
            if (Load(stage.StageNumber) is { } art && art.Zone == zone) return art;
        return null;
    }

    public static EnemyBaseArt Load(int stageNumber)
    {
        if (Cache.TryGetValue(stageNumber, out var cached)) return cached;
        var path = Folder + $"stage_{stageNumber:000}";
        if (!ResourceLoader.Exists(path + ".png") || !Godot.FileAccess.FileExists(path + ".json")) return Cache[stageNumber] = null;
        using var data = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(path + ".json"));
        var root = data.RootElement;
        var rect = root.GetProperty("rect");
        var lights = root.GetProperty("lights").EnumerateArray()
            .Select(l => (new Vector2(l[0].GetSingle(), l[1].GetSingle()), Color.FromHtml(l[2].GetString()))).ToArray();
        var smoke = root.GetProperty("smoke").EnumerateArray().Select(s => new Vector2(s[0].GetSingle(), s[1].GetSingle())).ToArray();
        return Cache[stageNumber] = new EnemyBaseArt(ResourceLoader.Load<Texture2D>(path + ".png"),
            new Rect2(rect[0].GetSingle(), rect[1].GetSingle(), rect[2].GetSingle(), rect[3].GetSingle()),
            root.GetProperty("zone").GetString(), lights, smoke) { Foot = root.GetProperty("foot").GetSingle(), GateX = root.GetProperty("gate").GetSingle() };
    }

    public static void ClearCache() => Cache.Clear();
}
