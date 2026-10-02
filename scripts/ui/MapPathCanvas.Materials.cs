using System;
using System.Linq;
using Godot;

public partial class MapPathCanvas
{
    private NoiseTexture2D _atlasMist;
    private GradientTexture2D _atlasVignette;

    private void BuildAtlasMaterials()
    {
        _atlasMist?.Dispose();
        var mist = TileMistColor().Lightened(.075f);
        _atlasMist = new NoiseTexture2D {
            Width = 512, Height = 512, Seamless = true, GenerateMipmaps = true,
            Noise = new FastNoiseLite { Seed = (int)(AdventureTerrain.Seed(ActiveMapId) % int.MaxValue), Frequency = .009f,
                NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, FractalOctaves = 4 },
            ColorRamp = new Gradient {
                Offsets = new[] { 0f, .45f, .75f, 1f },
                Colors = new[] { mist.Darkened(.18f), mist.Darkened(.03f), mist.Lightened(.065f), mist.Lightened(.11f) }
            }
        };
        _atlasVignette ??= new GradientTexture2D {
            Width = 512, Height = 512, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(.5f, .43f), FillTo = new Vector2(1, 1),
            Gradient = new Gradient { Offsets = new[] { 0f, .45f, 1f },
                Colors = new[] { new Color("101a2000"), new Color("101a2008"), new Color("101a2078") } }
        };
        // Warm the texture cache once per map; rendering reuses the same GPU resources.
        foreach (var index in new[] { AdventureAtlasArt.GroundMaterial(ActiveMapId), 2, 3, 7, 8 }) AdventureAtlasArt.Material(index);
    }
    private void DrawAtlasMaterial(Vector2[] polygon, int material, float scale, Color color, Vector2 drift = default)
    {
        if (AdventureAtlasArt.Material(material) is not { } texture) return;
        DrawPolygon(polygon, new[] { color }, polygon.Select(point => point / scale + drift).ToArray(), texture);
    }
    private bool DrawPaintedSprite(int index, Vector2 point, float height, Color? tint = null, float anchor = .89f)
    {
        if (AdventureAtlasArt.Sprite(index) is not { } texture) return false;
        var size = texture.GetSize(); size *= height / size.Y;
        DrawTextureRect(texture, new Rect2(point - new Vector2(size.X * .5f, size.Y * anchor), size), false,
            tint ?? AdventureAtlasArt.SceneryTint(ActiveMapId));
        return true;
    }
    private bool DrawPaintedProp(LandscapeProp prop)
    {
        var height = prop.Size; var sprite = -1;
        switch (prop.Kind)
        {
            case 0:
                sprite = ActiveMapId == "thornwall" ? 3 : ActiveMapId is "foundry" or "quarantine" or "citadel" && prop.Seed % 3 == 0 ? 4
                    : ActiveMapId == "gloamwood" || prop.Seed % 4 == 0 ? 2 : (int)(prop.Seed % 2);
                height *= 1.32f; break;
            case 1: sprite = 5; height *= 1.8f; break;
            case 2: sprite = ActiveMapId == "thornwall" ? 7 : ActiveMapId == "foundry" ? 8 : 6; height *= 1.1f; break;
            case 3:
                sprite = ActiveMapId switch { "city" => 10, "harbor" => 9, "foundry" => 12, "basilica" or "gloamwood" => 13,
                    "mire" => 14, "citadel" => 15, "steppe" => 25, _ => -1 };
                height = sprite == 14 ? 30 : 52; break;
            case 5: sprite = ActiveMapId == "foundry" ? 12 : 9; height = 54; break;
            case 6: sprite = 11; height = 54; break;
        }
        return sprite >= 0 && DrawPaintedSprite(sprite, prop.Point, height);
    }
}
