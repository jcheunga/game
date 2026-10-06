using Godot;

/// <summary>Painted map materials and fitted scenery regions, shared across all zone themes.</summary>
public static class AdventureAtlasArt
{
    public const string Directory = "res://assets/world/overworld/polished-v3/";
    private static readonly Texture2D[] Materials = new Texture2D[9];
    private static readonly AtlasTexture[] Sprites = new AtlasTexture[29];
    private static readonly Bitmap[] HitMasks = new Bitmap[29];
    private static Texture2D _scenery, _utility, _resources;
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
    private static readonly Rect2[] ResourceRegions = {
        new(58, 116, 719, 518), new(818, 68, 588, 570), new(1443, 167, 716, 470)
    };
    // Ground-contact centres exclude each sprite's cast shadow and forward-facing details.
    public static Vector2 GroundAnchor(int index) => index switch {
        0 => new(.46f, .89f), 1 => new(.43f, .89f), 2 or 3 => new(.46f, .9f), 4 => new(.43f, .9f),
        5 => new(.5f, .78f), 6 or 7 or 8 => new(.5f, .84f), 9 => new(.5f, .82f),
        10 => new(.46f, .9f), 11 => new(.5f, .8f), 12 => new(.49f, .83f), 13 => new(.5f, .84f),
        14 => new(.5f, .8f), 15 => new(.51f, .84f), 16 => new(.49f, .84f), 17 => new(.47f, .89f),
        18 => new(.5f, .85f), 19 => new(.46f, .82f), 20 => new(.48f, .83f), 21 => new(.49f, .78f),
        22 => new(.52f, .53f), 23 => new(.52f, .54f), 24 => new(.49f, .79f), 25 => new(.5f, .67f),
        26 => new(.51f, .8f), 27 => new(.51f, .85f), 28 => new(.5f, .67f), _ => new(.5f, .82f)
    };
    public static int LandmarkSprite(AdventureTile tile) => tile.Site?.Kind switch {
        AdventureSiteKind.Leader => GameState.Instance.IsAdventureBoss(tile.Site.Stage) ? 16 : 15,
        AdventureSiteKind.Camp => 19, AdventureSiteKind.Watchtower => 17, AdventureSiteKind.Shrine => 18,
        AdventureSiteKind.Food => 21, AdventureSiteKind.Gold => 20,
        _ => tile.Discovery?.Kind switch { AdventureDiscoveryKind.Food => 21, AdventureDiscoveryKind.Tomes => 26,
            AdventureDiscoveryKind.Essence => 27, AdventureDiscoveryKind.Survey => 28, _ => 20 }
    };
    public static float LandmarkHeight(AdventureTile tile) => tile.Site?.Kind switch {
        AdventureSiteKind.Leader => GameState.Instance.IsAdventureBoss(tile.Site.Stage) ? 126 : 94,
        AdventureSiteKind.Camp => 76, AdventureSiteKind.Watchtower => 88, AdventureSiteKind.Shrine => 80,
        AdventureSiteKind.Food => 52, AdventureSiteKind.Gold => 44,
        _ => tile.Discovery?.Kind switch { AdventureDiscoveryKind.Food => 52, AdventureDiscoveryKind.Essence => 54,
            AdventureDiscoveryKind.Survey => 40, _ => 44 }
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
        if (index >= 26)
        {
            var resourcePath = Directory + "resource-scenery.png";
            if (!ResourceLoader.Exists(resourcePath)) return null;
            _resources ??= ResourceLoader.Load<Texture2D>(resourcePath);
            return Sprites[index] = new AtlasTexture { Atlas = _resources, Region = ResourceRegions[index - 26], FilterClip = true };
        }
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
    public static bool SpriteContains(int index, Vector2 uv)
    {
        if (uv.X < 0 || uv.Y < 0 || uv.X >= 1 || uv.Y >= 1) return false;
        if (HitMasks[index] == null)
        {
            var path = Directory + (index >= 26 ? "resource-scenery.png" : index >= 20 ? "utility-scenery.png" : "medieval-scenery.png");
            if (!ResourceLoader.Exists(path)) return true;
            var region = index >= 26 ? ResourceRegions[index - 26] : index >= 20 ? UtilityRegions[index - 20] : Regions[index];
            using var source = ResourceLoader.Load<Texture2D>(path).GetImage();
            if (source.IsCompressed()) source.Decompress();
            using var image = source.GetRegion(new Rect2I((int)region.Position.X, (int)region.Position.Y, (int)region.Size.X, (int)region.Size.Y));
            HitMasks[index] = new Bitmap();
            HitMasks[index].CreateFromImageAlpha(image, .15f);
        }
        var size = HitMasks[index].GetSize();
        return HitMasks[index].GetBitv(new Vector2I((int)(uv.X * size.X), (int)(uv.Y * size.Y)));
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
