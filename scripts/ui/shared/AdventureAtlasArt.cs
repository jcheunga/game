using System;
using Godot;

/// <summary>Painted map materials and fitted scenery regions, shared across all zone themes.</summary>
public static class AdventureAtlasArt
{
    public const string Directory = "res://assets/world/overworld/polished-v3/";
    private static readonly Texture2D[] Materials = new Texture2D[9];
    private static readonly AtlasTexture[] Sprites = new AtlasTexture[26];
    private static Texture2D _scenery, _utility;
    // Fitted source rectangles preserve the natural proportions and complete sprite silhouettes.
    private static readonly Rect2[] Regions = {
        new(15, 27, 290, 300), new(330, 15, 213, 312), new(595, 10, 231, 321), new(872, 20, 225, 313), new(1160, 24, 213, 305),
        new(12, 394, 255, 163), new(265, 341, 315, 233), new(580, 341, 262, 229), new(842, 351, 286, 219), new(1128, 335, 272, 235),
        new(32, 561, 241, 269), new(278, 585, 278, 245), new(559, 567, 286, 244), new(843, 583, 282, 251), new(1125, 594, 272, 240),
        new(8, 813, 288, 265), new(278, 816, 317, 279), new(597, 806, 236, 290), new(834, 831, 290, 275), new(1106, 872, 287, 208)
    };
    private static readonly Rect2[] UtilityRegions = {
        new(70, 97, 370, 371), new(526, 48, 468, 425), new(994, 46, 519, 448),
        new(36, 528, 466, 425), new(530, 523, 462, 426), new(981, 566, 536, 396)
    };
    public static Texture2D Material(int index)
    {
        if (Materials[index] != null) return Materials[index];
        var path = Directory + "terrain-materials.png";
        if (!ResourceLoader.Exists(path)) return null;
        using var source = ResourceLoader.Load<Texture2D>(path).GetImage();
        if (source.IsCompressed()) source.Decompress();
        var cell = source.GetWidth() / 3;
        using var image = source.GetRegion(new Rect2I(index % 3 * cell, index / 3 * cell, cell, cell));
        image.GenerateMipmaps();
        return Materials[index] = ImageTexture.CreateFromImage(image);
    }
    public static Texture2D Sprite(int index)
    {
        if (Sprites[index] != null) return Sprites[index];
        if (index >= 20)
        {
            var utilityPath = Directory + "utility-scenery.png";
            if (!ResourceLoader.Exists(utilityPath)) return null;
            _utility ??= ResourceLoader.Load<Texture2D>(utilityPath);
            return Sprites[index] = new AtlasTexture { Atlas = _utility, Region = UtilityRegions[index - 20], FilterClip = true };
        }
        var path = Directory + "medieval-scenery.png";
        if (!ResourceLoader.Exists(path)) return null;
        _scenery ??= ResourceLoader.Load<Texture2D>(path);
        return Sprites[index] = new AtlasTexture { Atlas = _scenery, Region = Regions[index], FilterClip = true };
    }
    public static int GroundMaterial(string map) => map switch {
        "harbor" => 3, "foundry" => 5, "quarantine" or "gloamwood" => 1, "thornwall" => 6,
        "basilica" => 7, "mire" => 4, "steppe" => 2, "citadel" => 7, _ => 0
    };
    public static Color SceneryTint(string map) => new(map switch {
        "harbor" => "bdc9c5", "foundry" => "bb9c80", "quarantine" => "a2b0a2", "thornwall" => "d3e0e5",
        "basilica" => "dfd6b5", "mire" => "a8bcad", "steppe" => "d9cda6", "gloamwood" => "9cafa9", "citadel" => "b2b7ca", _ => "ffffff"
    });
}
