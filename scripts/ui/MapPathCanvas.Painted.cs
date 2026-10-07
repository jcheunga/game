using System.Linq;
using Godot;

/// <summary>
/// The painted campaign map (art/royal/mapsections.py): one painting per zone, made over the zone's own
/// geography and installed as a few texture tiles, drawn in world space beneath the sites, with painted
/// landmark sprites at each site. A zone without a painting shows plain sea around its sites.
/// </summary>
public partial class MapPathCanvas
{
    // The painting's tiles with their world rects; _painted is the first (or only) one.
    private readonly System.Collections.Generic.List<(Texture2D Texture, Rect2 Rect)> _paintedTiles = new();
    private Texture2D _painted;
    private Color _paintedSea = new("1d3b57");
    private static readonly System.Collections.Generic.Dictionary<string, Texture2D> Landmarks = new();

    private bool Painted => _painted != null;
    // The view is framed by the painting's rectangle, which the geography defines (the painting is made to it).
    private Rect2 _mapBounds;
    private Rect2 MapBounds => _mapBounds;
    private GradientTexture2D _atlasVignette;

    private void LoadPaintedMap()
    {
        _painted = null;
        _mapBounds = AdventureAtlasLandscape.PaintingRect(ActiveMapId);
        _paintedTiles.Clear();
        var path = $"res://assets/world/royal/maps/{ActiveMapId}";
        if (!FileAccess.FileExists(path + ".json")) return;
        var meta = Json.ParseString(FileAccess.GetFileAsString(path + ".json")).AsGodotDictionary();
        static Rect2 Rect(Godot.Collections.Array r) => new((float)r[0].AsDouble(), (float)r[1].AsDouble(), (float)r[2].AsDouble(), (float)r[3].AsDouble());
        _paintedSea = new Color(meta["sea"].AsString());
        if (meta.TryGetValue("tiles", out var tiles))
        {
            var rects = tiles.AsGodotArray();
            for (var k = 0; k < rects.Count; k++)
                if (ResourceLoader.Exists($"{path}-{k}.jpg"))
                    _paintedTiles.Add((ResourceLoader.Load<Texture2D>($"{path}-{k}.jpg"), Rect(rects[k].AsGodotArray())));
        }
        else if (ResourceLoader.Exists(path + ".jpg"))
            _paintedTiles.Add((ResourceLoader.Load<Texture2D>(path + ".jpg"), Rect(meta["rect"].AsGodotArray())));
        _painted = _paintedTiles.Count > 0 ? _paintedTiles[0].Texture : null;
    }

    private void DrawPaintedBackground()
    {
        var view = new Rect2(-MapOffset / Zoom, Size / Zoom).Grow(48);
        DrawRect(view, _paintedSea);
        foreach (var (texture, rect) in _paintedTiles) DrawTextureRect(texture, rect, false);
    }

    private MapFogLayer _fogLayer;
    private MapOverlayLayer _overlayLayer;
    private int _fogRevision = -1;
    private string _fogMap = "";

    /// <summary>Adds the cloud bank and the layer above it; they stay beneath the site tokens.</summary>
    private void CreateMapLayers()
    {
        _fogLayer = new MapFogLayer();
        _overlayLayer = new MapOverlayLayer { Paint = PaintOverlay };
        foreach (var layer in new Control[] { _fogLayer, _overlayLayer })
        {
            layer.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(layer);
        }
        MoveChild(_fogLayer, 0); MoveChild(_overlayLayer, 1);
    }

    private void UpdateMapLayers()
    {
        BuildFogMask();
        _fogLayer.Visible = _fogLayer.Mask != null;
        _fogLayer.Offset = MapOffset; _fogLayer.Zoom = Zoom;
        if (!GameState.Instance.ReducedMotion) _fogLayer.Clock = _time;
        _fogLayer.QueueRedraw(); _overlayLayer.QueueRedraw();
    }

    private const float FogCell = 8;
    private static readonly System.Collections.Generic.Dictionary<string, (int[] Tiles, int Width, int Height, Rect2 Bounds)> FogRasters = new();

    /// <summary>Which tile each fog cell of the zone lies in (-1 for open sea), worked out once per zone.</summary>
    private (int[] Tiles, int Width, int Height, Rect2 Bounds) FogRaster()
    {
        if (FogRasters.TryGetValue(ActiveMapId, out var raster)) return raster;
        var bounds = AdventureAtlasLandscape.PaintingRect(ActiveMapId).Grow(64);
        var width = Mathf.CeilToInt(bounds.Size.X / FogCell); var height = Mathf.CeilToInt(bounds.Size.Y / FogCell);
        var cells = new int[width * height];
        System.Array.Fill(cells, -1);
        for (var i = 0; i < _tiles.Count; i++)
        {
            var outline = AdventureAtlasLandscape.Outline(_tiles[i]);
            var box = PolygonBounds(outline);
            var x0 = Mathf.Max(0, Mathf.FloorToInt((box.Position.X - bounds.Position.X) / FogCell));
            var y0 = Mathf.Max(0, Mathf.FloorToInt((box.Position.Y - bounds.Position.Y) / FogCell));
            var x1 = Mathf.Min(width, Mathf.CeilToInt((box.End.X - bounds.Position.X) / FogCell));
            var y1 = Mathf.Min(height, Mathf.CeilToInt((box.End.Y - bounds.Position.Y) / FogCell));
            for (var y = y0; y < y1; y++)
                for (var x = x0; x < x1; x++)
                    if (Geometry2D.IsPointInPolygon(bounds.Position + new Vector2(x + .5f, y + .5f) * FogCell, outline)) cells[y * width + x] = i;
        }
        return FogRasters[ActiveMapId] = (cells, width, height, bounds);
    }

    /// <summary>
    /// Builds the fog mask once per change in what the player has explored: the red channel covers hidden
    /// tiles (storm cloud), the green channel frontier tiles (thin mist). Both are softened so the shader can
    /// fray their outlines into billows.
    /// </summary>
    private void BuildFogMask()
    {
        var state = GameState.Instance;
        if (_fogRevision == state.AdventureKnowledgeRevision && _fogMap == ActiveMapId) return;
        _fogRevision = state.AdventureKnowledgeRevision; _fogMap = ActiveMapId;
        var raster = FogRaster();
        var kinds = _tiles.Select(tile => state.IsAdventureTileOpened(tile) ? 0 : state.IsAdventureTileRevealed(tile) ? 1 : 2).ToArray();
        if (kinds.All(kind => kind == 0)) { _fogLayer.Mask = null; return; }
        var storm = new float[raster.Tiles.Length]; var mist = new float[raster.Tiles.Length];
        for (var i = 0; i < raster.Tiles.Length; i++)
        {
            var tile = raster.Tiles[i];
            if (tile < 0) continue;
            if (kinds[tile] == 2) storm[i] = 1; else if (kinds[tile] == 1) mist[i] = 1;
        }
        // Three box passes approximate a gaussian about 50 world units wide.
        foreach (var values in new[] { storm, mist })
            for (var pass = 0; pass < 3; pass++) { BoxBlur(values, raster.Width, raster.Height, 5, true); BoxBlur(values, raster.Width, raster.Height, 5, false); }
        var bytes = new byte[storm.Length * 2];
        for (var i = 0; i < storm.Length; i++)
        {
            bytes[i * 2] = (byte)Mathf.Clamp(Mathf.RoundToInt(storm[i] * 255), 0, 255);
            bytes[i * 2 + 1] = (byte)Mathf.Clamp(Mathf.RoundToInt(mist[i] * 255), 0, 255);
        }
        _fogLayer.Mask = ImageTexture.CreateFromImage(Image.CreateFromData(raster.Width, raster.Height, false, Image.Format.Rg8, bytes));
        _fogLayer.MaskRect = new Rect2(raster.Bounds.Position, new Vector2(raster.Width, raster.Height) * FogCell);
    }

    private static void BoxBlur(float[] values, int width, int height, int radius, bool horizontal)
    {
        var lines = horizontal ? height : width; var length = horizontal ? width : height;
        var line = new float[length];
        for (var l = 0; l < lines; l++)
        {
            int Index(int i) => horizontal ? l * width + i : i * width + l;
            for (var i = 0; i < length; i++) line[i] = values[Index(i)];
            var sum = 0f;
            for (var i = 0; i <= radius && i < length; i++) sum += line[i];
            for (var i = 0; i < length; i++)
            {
                values[Index(i)] = sum / (radius * 2 + 1);
                if (i + radius + 1 < length) sum += line[i + radius + 1];
                if (i - radius >= 0) sum -= line[i - radius];
            }
        }
    }

    /// <summary>Above the fog: the painted landmarks of explored sites, the food price of each unopened resource,
    /// reward bursts and the vignette.</summary>
    private void PaintOverlay(Control layer)
    {
        layer.DrawSetTransform(MapOffset, 0, Vector2.One * Zoom);
        var state = GameState.Instance;
        var view = new Rect2((-MapOffset - new Vector2(200, 220)) / Zoom, (Size + new Vector2(400, 440)) / Zoom);
        var priced = new System.Collections.Generic.List<AdventureTile>();
        foreach (var tile in _tiles.Where(tile => view.HasPoint(tile.Point) && state.IsAdventureTileRevealed(tile)).OrderBy(tile => tile.Point.Y))
        {
            if (tile.Site != null && !string.IsNullOrEmpty(tile.Site.RequiredVisit) && !state.HasVisitedAdventureSite(tile.Site.RequiredVisit)) continue;
            if (tile.HasInterest && (!tile.IsResource || !state.IsAdventureTileComplete(tile))) DrawRoyalLandmark(layer, tile);
            // Every frontier tile but a stage (which is won, not bought) shows what it costs to open.
            if (tile.Site?.Kind != AdventureSiteKind.Leader && state.IsAdventureTileFrontier(tile)) priced.Add(tile);
        }
        foreach (var burst in _rewardBursts)
        {
            var tile = AdventureTileCatalog.Find(ActiveMapId, burst.Reward.Id);
            var age = _time - burst.Time;
            var lift = GameState.Instance.ReducedMotion ? 0 : age * 16;
            var p = tile.Point + new Vector2(0, -70 - lift);
            var amount = burst.Reward.Kind == AdventureDiscoveryKind.Survey ? "Nearby terrain revealed" : $"+{burst.Reward.Amount:N0}";
            HomeResourceUi.DrawAmount(layer, p, burst.Reward.Icon, amount, 20, 28,
                new Color("fff1be") { A = Mathf.Clamp((2.4f - age) * 1.5f, 0, 1) }, true);
        }
        layer.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        // Price tags keep a readable screen size at every zoom.
        var affordable = state.Food >= GameState.AdventureTileFoodCost;
        foreach (var tile in priced) DrawOpeningPrice(layer, tile.Point * Zoom + MapOffset + new Vector2(0, tile.HasInterest ? 20 : 0), affordable);
        _atlasVignette ??= new GradientTexture2D {
            Width = 512, Height = 512, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(.5f, .43f), FillTo = new Vector2(1, 1),
            Gradient = new Gradient { Offsets = new[] { 0f, .45f, 1f },
                Colors = new[] { new Color("101a2000"), new Color("101a2008"), new Color("101a2078") } }
        };
        layer.DrawTextureRect(_atlasVignette, new Rect2(Vector2.Zero, Size), false);
    }

    private static readonly StyleBoxFlat PricePill = new()
    {
        BgColor = new Color("172521d9"), BorderColor = new Color("d8b46a8c"),
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 11, CornerRadiusTopRight = 11, CornerRadiusBottomLeft = 11, CornerRadiusBottomRight = 11
    };

    /// <summary>The food it costs to open an unopened resource tile, red when the caravan can't afford it.</summary>
    private static void DrawOpeningPrice(CanvasItem layer, Vector2 center, bool affordable)
    {
        const int font = 15, icon = 17;
        var text = GameState.AdventureTileFoodCost.ToString();
        var width = HomeResourceUi.AmountWidth("food", text, font, icon) + 16;
        layer.DrawStyleBox(PricePill, new Rect2(center - new Vector2(width / 2, 11), new Vector2(width, 22)));
        HomeResourceUi.DrawAmount(layer, center, "food", text, font, icon, new Color(affordable ? "fff1be" : "ff9f86"));
    }

    private static Texture2D Landmark(string name)
    {
        if (Landmarks.TryGetValue(name, out var cached)) return cached;
        var path = $"res://assets/world/royal/maps/landmarks/{name}.png";
        return Landmarks[name] = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }

    private static string LandmarkName(AdventureTile tile) => tile.Site?.Kind switch
    {
        AdventureSiteKind.Leader => GameState.Instance.IsAdventureBoss(tile.Site.Stage) ? "boss" : "leader",
        AdventureSiteKind.Camp => "camp", AdventureSiteKind.Watchtower => "watchtower", AdventureSiteKind.Shrine => "shrine",
        AdventureSiteKind.Food => "food", AdventureSiteKind.Gold => "gold",
        _ => tile.Discovery?.Kind switch
        {
            AdventureDiscoveryKind.Food => "food", AdventureDiscoveryKind.Essence => "essence",
            AdventureDiscoveryKind.Survey => "survey", _ => "gold"
        }
    };

    // A landmark stands on its tile point: the point sits this far down the painting.
    private const float LandmarkFoot = .86f;
    private static readonly System.Collections.Generic.Dictionary<string, Bitmap> LandmarkMasks = new();

    /// <summary>How far a painted landmark rises above its tile point (world units).</summary>
    public static float RoyalLandmarkRise(AdventureTile tile) => RoyalLandmarkHeight(tile) * LandmarkFoot;

    /// <summary>Castles stand tall over the painted land; camps, towers and shrines are smaller, supplies and finds smallest.</summary>
    private static float RoyalLandmarkHeight(AdventureTile tile) => tile.Site?.Kind switch
    {
        AdventureSiteKind.Leader => GameState.Instance.IsAdventureBoss(tile.Site.Stage) ? 214 : 160,
        AdventureSiteKind.Camp => 106, AdventureSiteKind.Watchtower => 123, AdventureSiteKind.Shrine => 112,
        AdventureSiteKind.Food => 57, AdventureSiteKind.Gold => 48,
        _ => tile.Discovery?.Kind switch { AdventureDiscoveryKind.Food => 57, AdventureDiscoveryKind.Essence => 59,
            AdventureDiscoveryKind.Survey => 44, _ => 48 }
    };

    private static Rect2 LandmarkRect(AdventureTile tile, Texture2D texture)
    {
        var size = texture.GetSize() * (RoyalLandmarkHeight(tile) / texture.GetHeight());
        return new Rect2(-new Vector2(size.X * .5f, size.Y * LandmarkFoot), size);
    }

    /// <summary>Draws the painted landmark standing on the tile's point.</summary>
    private static void DrawRoyalLandmark(CanvasItem target, AdventureTile tile)
    {
        if (Landmark(LandmarkName(tile)) is not { } texture) return;
        var rect = LandmarkRect(tile, texture);
        target.DrawTextureRect(texture, new Rect2(tile.Point + rect.Position, rect.Size), false);
    }

    /// <summary>Whether a point (world units from the tile's point) lands on the painted landmark's pixels.</summary>
    public static bool RoyalLandmarkContains(AdventureTile tile, Vector2 delta)
    {
        var name = LandmarkName(tile);
        if (Landmark(name) is not { } texture) return false;
        var rect = LandmarkRect(tile, texture);
        var uv = (delta - rect.Position) / rect.Size;
        if (uv.X < 0 || uv.Y < 0 || uv.X >= 1 || uv.Y >= 1) return false;
        if (!LandmarkMasks.TryGetValue(name, out var mask))
        {
            using var image = texture.GetImage();
            if (image.IsCompressed()) image.Decompress();
            mask = new Bitmap();
            mask.CreateFromImageAlpha(image, .15f);
            LandmarkMasks[name] = mask;
        }
        var bits = mask.GetSize();
        return mask.GetBitv(new Vector2I((int)(uv.X * bits.X), (int)(uv.Y * bits.Y)));
    }
}
