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
        // Undo the map's 2:1 ground projection so surface detail lies on the same plane as the buildings.
        Vector2 GroundUv(Vector2 point) => new Vector2(point.X + point.Y * 2, -point.X + point.Y * 2) / (scale * Mathf.Sqrt(5));
        DrawPolygon(polygon, new[] { color }, polygon.Select(point => GroundUv(point) + drift).ToArray(), texture);
    }
    private bool DrawPaintedSprite(int index, Vector2 point, float height, Color? tint = null, float groundShear = 0)
    {
        if (AdventureAtlasArt.Sprite(index) is not { } texture) return false;
        var size = texture.GetSize(); size *= height / size.Y;
        var anchor = AdventureAtlasArt.GroundAnchor(index) * size;
        if (Math.Abs(groundShear) > .001f)
        {
            // Follow the local crossing without tilting upright stonework or bridge posts.
            DrawSetTransformMatrix(new Transform2D(new Vector2(Zoom, groundShear * Zoom), new Vector2(0, Zoom), MapOffset + point * Zoom));
            DrawTextureRect(texture, new Rect2(-anchor, size), false, tint ?? AdventureAtlasArt.SceneryTint(ActiveMapId));
            DrawSetTransform(MapOffset, 0, Vector2.One * Zoom);
        }
        else DrawTextureRect(texture, new Rect2(point - anchor, size), false, tint ?? AdventureAtlasArt.SceneryTint(ActiveMapId));
        return true;
    }

    private void DrawLandmarkFootprint(AdventureTile tile)
    {
        if (!tile.HasInterest || tile.Site != null && !string.IsNullOrEmpty(tile.Site.RequiredVisit) && !GameState.Instance.HasVisitedAdventureSite(tile.Site.RequiredVisit)) return;
        var complete = GameState.Instance.IsAdventureTileComplete(tile);
        var resource = tile.Discovery != null || tile.Site?.Kind is AdventureSiteKind.Gold or AdventureSiteKind.Food;
        if (resource && complete) return;
        var hot = tile.Id == _selectedId
            || _tokens.Any(token => token.Site.Id == tile.Id && (token.IsHovered() || token.HasFocus()))
            || _discoveries.Any(token => token.Discovery.Id == tile.Id && (token.IsHovered() || token.HasFocus()));
        var radius = resource ? 31 : tile.Site?.Kind == AdventureSiteKind.Leader && GameState.Instance.IsAdventureBoss(tile.Site.Stage) ? 68 : 48;
        var ellipse = Enumerable.Range(0, 48).Select(i => tile.Point + new Vector2(Mathf.Cos(i * Mathf.Tau / 48) * radius, Mathf.Sin(i * Mathf.Tau / 48) * radius * .5f)).ToArray();
        DrawColoredPolygon(ellipse, hot ? new Color("d5b36d1a") : new Color("26342212"));
        if (hot) DrawPolyline(ellipse.Append(ellipse[0]).ToArray(), new Color("ead19ca6"), 1.5f / Zoom, true);
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
