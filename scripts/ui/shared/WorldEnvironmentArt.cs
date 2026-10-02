using Godot;

/// <summary>Authored scene plates; playable ground is mapped to the simulation's exact bounds.</summary>
public static class WorldEnvironmentArt
{
    public const string BattleDirectory = "res://assets/world/battles/";
    public const string ZoneDirectory = "res://assets/world/zones/";
    public const string OverworldDirectory = "res://assets/world/overworld/";
    public static readonly Rect2 BattleFloorSource = new(.08f, .36f, .84f, .28f);
    // Keep the walkable field inside clear illustrated ground, away from perimeter cliffs and buildings.
    public static readonly Vector2[] ZoneFloorSource = {
        new(.46f,.29f), new(.81f,.47f), new(.64f,.64f), new(.28f,.44f)
    };

    public static string BattlePath(int stage) => $"{BattleDirectory}stage-{stage:00}.png";
    public static string ZonePath(string zone) => $"{ZoneDirectory}{RouteCatalog.Normalize(zone)}.png";
    public static Texture2D LoadBattle(int stage) => Load(BattlePath(stage));
    public static string OverworldPath(string zone) => $"{OverworldDirectory}{RouteCatalog.Normalize(zone)}-painted-v2.png";
    public static Texture2D LoadZone(string zone) => Load(OverworldPath(zone)) ?? Load(ZonePath(zone));
    private static Texture2D Load(string path) => ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;

    public static Vector2 ZoneGroundUv(Vector2 point)
    {
        var grid = AdventureTerrain.GridPoint(point);
        var column = (grid.X + .5f) / AdventureTerrain.Columns;
        var row = (grid.Y + .5f) / AdventureTerrain.Rows;
        return ZoneFloorSource[0] * (1-column) * (1-row)
            + ZoneFloorSource[1] * column * (1-row)
            + ZoneFloorSource[2] * column * row
            + ZoneFloorSource[3] * (1-column) * row;
    }

    public static void DrawBattle(CanvasItem canvas, Texture2D texture, Vector2 worldSize, Rect2 ground)
    {
        foreach (var slice in BattleSlices(texture.GetSize(), worldSize, ground))
            canvas.DrawTextureRectRegion(texture, slice.Target, slice.Source);
    }

    public static (Rect2 Target, Rect2 Source)[] BattleSlices(Vector2 size, Vector2 worldSize, Rect2 ground)
    {
        var sourceX = new[] { 0f, size.X * BattleFloorSource.Position.X, size.X * BattleFloorSource.End.X, size.X };
        var sourceY = new[] { 0f, size.Y * BattleFloorSource.Position.Y, size.Y * BattleFloorSource.End.Y, size.Y };
        var targetX = new[] { 0f, ground.Position.X, ground.End.X, worldSize.X };
        var targetY = new[] { 0f, ground.Position.Y, ground.End.Y, worldSize.Y };
        var slices = new (Rect2 Target, Rect2 Source)[9];
        // Stretch the clear central floor to the actual combat rectangle. Scenery stays in the eight margins.
        for (var row = 0; row < 3; row++)
            for (var column = 0; column < 3; column++)
                slices[row * 3 + column] = (
                    new Rect2(targetX[column], targetY[row], targetX[column + 1] - targetX[column], targetY[row + 1] - targetY[row]),
                    new Rect2(sourceX[column], sourceY[row], sourceX[column + 1] - sourceX[column], sourceY[row + 1] - sourceY[row]));
        return slices;
    }

    public static Color WaterColor(string zone) => new(zone switch
    {
        "foundry" => "59463d", "quarantine" => "405953", "thornwall" => "506775",
        "basilica" => "4d565b", "mire" => "304b43", "steppe" => "655b43",
        "gloamwood" => "334e4c", "citadel" => "415c6d", _ => "355d6c"
    });
}
