using Godot;

/// <summary>Authored scene plates; playable ground is mapped to the simulation's exact bounds.</summary>
public static class WorldEnvironmentArt
{
    public const string BattleDirectory = "res://assets/world/battles/";

    public static readonly Rect2 BattleFloorSource = new(.08f, .27f, .84f, .43f);
    public static string BattlePath(int stage) => $"{BattleDirectory}stage-{stage:00}.png";

    public static Texture2D LoadBattle(int stage) => Load(BattlePath(stage));

    private static Texture2D Load(string path) => ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;

    public static void DrawBattle(CanvasItem canvas, Texture2D texture, Rect2 ground)
    {
        // Keep buildings, trees and ground in one perspective. Crop the outer
        // scenery naturally instead of crushing it into thin strips.
        canvas.DrawTextureRect(texture, BattleSceneRect(texture.GetSize(), ground), false);
    }

    public static Rect2 BattleSceneRect(Vector2 sourceSize, Rect2 ground)
    {
        var scale = ground.Size.X / (sourceSize.X * BattleFloorSource.Size.X);
        return new Rect2(ground.Position - sourceSize * BattleFloorSource.Position * scale, sourceSize * scale);
    }

    public static Color WaterColor(string zone) => new(zone switch
    {
        "foundry" => "59463d", "quarantine" => "405953", "thornwall" => "506775",
        "basilica" => "4d565b", "mire" => "304b43", "steppe" => "655b43",
        "gloamwood" => "334e4c", "citadel" => "415c6d", _ => "355d6c"
    });
}
