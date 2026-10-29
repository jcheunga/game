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
    /// <summary>Tops of flat wall a sentry stands behind (art/royal/bases_life.py): x, y and half the stretch's width.</summary>
    public Vector3[] Posts { get; init; } = System.Array.Empty<Vector3>();
    /// <summary>Warm lights in the painting (lit windows, candles, braziers) and their radius.</summary>
    public (Vector2 Point, float Radius)[] Warm { get; init; } = System.Array.Empty<(Vector2, float)>();
    /// <summary>Spots on solid facade near the gate where a crack or chip decal lies whole, nearest the gate first.</summary>
    public Vector2[] Wall { get; init; } = System.Array.Empty<Vector2>();
    private const string Folder = "res://assets/structures/bases/";
    // The two-screen field's EnemyBaseX, which plates cut before they recorded their own were laid out against.
    private const float FirstEnemyBaseX = 1152f;
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
        // Plates are laid out against the EnemyBaseX they were cut for; they move with the field's end.
        var shift = new Vector2(GameData.Combat.EnemyBaseX -
            (root.TryGetProperty("enemyBaseX", out var authored) ? authored.GetSingle() : FirstEnemyBaseX), 0);
        var rect = root.GetProperty("rect");
        var lights = root.GetProperty("lights").EnumerateArray()
            .Select(l => (new Vector2(l[0].GetSingle(), l[1].GetSingle()) + shift, Color.FromHtml(l[2].GetString()))).ToArray();
        var smoke = root.GetProperty("smoke").EnumerateArray().Select(s => new Vector2(s[0].GetSingle(), s[1].GetSingle()) + shift).ToArray();
        return Cache[stageNumber] = new EnemyBaseArt(ResourceLoader.Load<Texture2D>(path + ".png"),
            new Rect2(new Vector2(rect[0].GetSingle(), rect[1].GetSingle()) + shift, rect[2].GetSingle(), rect[3].GetSingle()),
            root.GetProperty("zone").GetString(), lights, smoke)
        {
            Foot = root.GetProperty("foot").GetSingle(), GateX = root.GetProperty("gate").GetSingle() + shift.X,
            Posts = root.TryGetProperty("posts", out var posts)
                ? posts.EnumerateArray().Select(p => new Vector3(p[0].GetSingle() + shift.X, p[1].GetSingle(), p[2].GetSingle())).ToArray()
                : System.Array.Empty<Vector3>(),
            Warm = root.TryGetProperty("warm", out var warm)
                ? warm.EnumerateArray().Select(w => (new Vector2(w[0].GetSingle(), w[1].GetSingle()) + shift, w[2].GetSingle())).ToArray()
                : System.Array.Empty<(Vector2, float)>(),
            Wall = root.TryGetProperty("wall", out var wall)
                ? wall.EnumerateArray().Select(w => new Vector2(w[0].GetSingle(), w[1].GetSingle()) + shift).ToArray()
                : System.Array.Empty<Vector2>()
        };
    }

    public static void ClearCache() => Cache.Clear();
}
