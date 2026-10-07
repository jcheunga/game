using System.Linq;
using Godot;

/// <summary>
/// The painted campaign map (art/royal/maps.py): one painting per zone, made over the zone's own
/// geography, drawn in world space beneath the sites, with painted landmark sprites at each site.
/// A zone without a painting shows plain sea around its sites.
/// </summary>
public partial class MapPathCanvas
{
    private Texture2D _painted;
    private Rect2 _paintedRect;
    private Color _paintedSea = new("1d3b57");
    private static readonly System.Collections.Generic.Dictionary<string, Texture2D> Landmarks = new();

    private bool Painted => _painted != null;
    private Rect2 MapBounds => Painted ? _paintedRect : new Rect2(Vector2.Zero, AdventureTileCatalog.WorldSize);
    private GradientTexture2D _atlasVignette;

    private void LoadPaintedMap()
    {
        _painted = null;
        var path = $"res://assets/world/royal/maps/{ActiveMapId}";
        if (!ResourceLoader.Exists(path + ".png") || !FileAccess.FileExists(path + ".json")) return;
        var meta = Json.ParseString(FileAccess.GetFileAsString(path + ".json")).AsGodotDictionary();
        var r = meta["rect"].AsGodotArray();
        _paintedRect = new Rect2((float)r[0].AsDouble(), (float)r[1].AsDouble(), (float)r[2].AsDouble(), (float)r[3].AsDouble());
        _paintedSea = new Color(meta["sea"].AsString());
        _painted = ResourceLoader.Load<Texture2D>(path + ".png");
    }

    private void DrawPaintedBackground()
    {
        var view = new Rect2(-MapOffset / Zoom, Size / Zoom).Grow(48);
        DrawRect(view, _paintedSea);
        if (Painted) DrawTextureRect(_painted, _paintedRect, false);
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

    /// <summary>
    /// Rasterises every unexplored tile into a coarse world-space mask and softens it, once per change in
    /// what the player has explored. The fog shader frays this outline into billows.
    /// </summary>
    private void BuildFogMask()
    {
        var state = GameState.Instance;
        if (_fogRevision == state.AdventureKnowledgeRevision && _fogMap == ActiveMapId) return;
        _fogRevision = state.AdventureKnowledgeRevision; _fogMap = ActiveMapId;
        var hidden = _tiles.Where(tile => !state.IsAdventureTileOpen(tile)).Select(AdventureAtlasLandscape.Outline).ToArray();
        if (hidden.Length == 0) { _fogLayer.Mask = null; return; }
        const float Cell = 6;
        var bounds = hidden.Select(PolygonBounds).Aggregate((a, b) => a.Merge(b)).Grow(150);
        var width = Mathf.CeilToInt(bounds.Size.X / Cell); var height = Mathf.CeilToInt(bounds.Size.Y / Cell);
        var values = new float[width * height];
        foreach (var outline in hidden)
        {
            var box = PolygonBounds(outline);
            var x0 = Mathf.Max(0, Mathf.FloorToInt((box.Position.X - bounds.Position.X) / Cell));
            var y0 = Mathf.Max(0, Mathf.FloorToInt((box.Position.Y - bounds.Position.Y) / Cell));
            var x1 = Mathf.Min(width, Mathf.CeilToInt((box.End.X - bounds.Position.X) / Cell));
            var y1 = Mathf.Min(height, Mathf.CeilToInt((box.End.Y - bounds.Position.Y) / Cell));
            for (var y = y0; y < y1; y++)
                for (var x = x0; x < x1; x++)
                    if (Geometry2D.IsPointInPolygon(bounds.Position + new Vector2(x + .5f, y + .5f) * Cell, outline)) values[y * width + x] = 1;
        }
        // Three box passes approximate a gaussian about 50 world units wide.
        for (var pass = 0; pass < 3; pass++) { BoxBlur(values, width, height, 6, true); BoxBlur(values, width, height, 6, false); }
        var bytes = new byte[values.Length];
        for (var i = 0; i < values.Length; i++) bytes[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(values[i] * 255), 0, 255);
        _fogLayer.Mask = ImageTexture.CreateFromImage(Image.CreateFromData(width, height, false, Image.Format.L8, bytes));
        _fogLayer.MaskRect = new Rect2(bounds.Position, new Vector2(width, height) * Cell);
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

    /// <summary>Above the fog: the painted landmarks of explored sites, reward bursts and the vignette.</summary>
    private void PaintOverlay(Control layer)
    {
        layer.DrawSetTransform(MapOffset, 0, Vector2.One * Zoom);
        var state = GameState.Instance;
        var view = new Rect2((-MapOffset - new Vector2(200, 220)) / Zoom, (Size + new Vector2(400, 440)) / Zoom);
        foreach (var tile in _tiles.Where(tile => state.IsAdventureTileOpen(tile) && view.HasPoint(tile.Point)).OrderBy(tile => tile.Point.Y))
        {
            if (tile.Site != null && !string.IsNullOrEmpty(tile.Site.RequiredVisit) && !state.HasVisitedAdventureSite(tile.Site.RequiredVisit)) continue;
            var resource = tile.Discovery != null || tile.Site?.Kind is AdventureSiteKind.Gold or AdventureSiteKind.Food;
            if (tile.HasInterest && (!resource || !state.IsAdventureTileComplete(tile))) DrawRoyalLandmark(layer, tile);
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
        _atlasVignette ??= new GradientTexture2D {
            Width = 512, Height = 512, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(.5f, .43f), FillTo = new Vector2(1, 1),
            Gradient = new Gradient { Offsets = new[] { 0f, .45f, 1f },
                Colors = new[] { new Color("101a2000"), new Color("101a2008"), new Color("101a2078") } }
        };
        layer.DrawTextureRect(_atlasVignette, new Rect2(Vector2.Zero, Size), false);
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
