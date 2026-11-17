using System.Collections.Generic;
using Godot;

/// <summary>
/// A zone's painted battlefield as a still scene behind a full-screen page (the Account page): the five backdrop
/// layers laid out as the battle camera shows them, the player's caravan standing on the road, and a slow drift of
/// the camera so the layers part as they do in battle. Drawn on the 1280x720 canvas.
/// </summary>
public partial class RoadVista : Control
{
    // The desktop battle camera the layers were laid out for (art/royal/backdrops.py): world x 600, y 340 at
    // screen (640, 464.6), 600 world units across the canvas.
    private const float Zoom = 1280f / 600f, CentreX = 600, CentreY = 340;
    private static readonly Vector2 Anchor = new(640, 464.6f);

    /// <summary>Where the caravan stands, in world units, how much larger than in battle it is drawn, and how far the
    /// camera drifts each way.</summary>
    public Vector2 Caravan = new(452, 352);
    public float CaravanScale = 1;
    public float Drift = 26, DriftSeconds = 64;
    /// <summary>Warband figures standing beside the caravan: unit id, world x and height in world units.</summary>
    public (string Unit, float X, float Height)[] Escort = System.Array.Empty<(string, float, float)>();

    private ZoneBackdrop _backdrop;
    private BattleStructureArt _wagon;
    private readonly List<(Texture2D Figure, float X, float Height)> _escort = new();
    private float _time;
    private static GradientTexture2D _shadow;
    private static GradientTexture2D Shadow => _shadow ??= new GradientTexture2D
    {
        Gradient = new Gradient { Colors = new[] { new Color(0, 0, 0, .5f), new Color(0, 0, 0, 0) }, Offsets = new[] { 0f, 1f } },
        Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(1f, .5f), Width = 128, Height = 64
    };

    public RoadVista(string zoneId = "city")
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _backdrop = WorldEnvironmentArt.LoadZoneBackdrop(zoneId);
        var skin = GameState.Instance?.SelectedWagonSkinId ?? WagonSkinCatalog.DefaultSkinId;
        _wagon = BattleStructureArt.Load(skin == WagonSkinCatalog.DefaultSkinId ? "war_wagon" : "war_wagon_" + skin) ?? BattleStructureArt.Load("war_wagon");
    }

    public override void _Ready()
    {
        Position = Vector2.Zero; Size = RoyalArt.Canvas;
        ClipContents = true;
        foreach (var (unit, x, height) in Escort)
            if (UnitFigure.For(GameData.GetUnit(unit)) is { } figure) _escort.Add((figure, x, height));
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    private float CameraX => CentreX + Drift * Mathf.Sin(_time * Mathf.Tau / DriftSeconds);

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), _backdrop?.Sky ?? new Color("1d2a33"));
        if (_backdrop == null) return;
        var camera = CameraX;
        DrawSetTransform(Anchor - new Vector2(camera, CentreY) * Zoom, 0, Vector2.One * Zoom);
        var halfWidth = Size.X / Zoom / 2;
        foreach (var layer in _backdrop.Layers)
        {
            if (layer.Front) continue;
            foreach (var rect in Tiles(layer, camera, halfWidth)) DrawTextureRect(layer.Texture, rect, false);
        }
        if (_wagon != null)
        {
            var size = _wagon.Size * CaravanScale;
            var rect = new Rect2(Caravan - _wagon.Anchor * size, size);
            DrawTextureRect(Shadow, new Rect2(Caravan.X - size.X * .56f, Caravan.Y - 9, size.X * 1.12f, 18), false);
            DrawTextureRect(_wagon.Texture, rect, false);
        }
        foreach (var (figure, x, height) in _escort)
        {
            var size = figure.GetSize() * (height / figure.GetHeight());
            var foot = new Vector2(x, Caravan.Y + 4);
            DrawTextureRect(Shadow, new Rect2(foot.X - size.X * .42f, foot.Y - 4, size.X * .84f, 8), false);
            DrawTextureRect(figure, new Rect2(foot.X - size.X / 2, foot.Y - size.Y, size.X, size.Y), false);
        }
        foreach (var layer in _backdrop.Layers)
        {
            if (!layer.Front) continue;
            foreach (var rect in Tiles(layer, camera, halfWidth)) DrawTextureRect(layer.Texture, rect, false);
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    /// <summary>Where a layer falls for this camera, as <see cref="BattleTerrainCanvas"/> places it in battle.</summary>
    private static IEnumerable<Rect2> Tiles(BackdropLayer layer, float camera, float halfWidth)
    {
        var rect = new Rect2(layer.Rect.Position + new Vector2((1 - layer.Parallax) * (camera - CentreX), 0), layer.Rect.Size);
        if (!layer.Tile)
        {
            rect.Position += new Vector2(CentreX - layer.Rect.GetCenter().X, 0);
            yield return rect;
            yield break;
        }
        var step = rect.Size.X;
        var first = Mathf.Floor((camera - halfWidth - rect.Position.X) / step);
        var last = Mathf.Ceil((camera + halfWidth - rect.Position.X) / step);
        for (var k = first; k <= last; k++)
            yield return new Rect2(rect.Position + new Vector2(k * step, 0), new Vector2(step + .5f, rect.Size.Y));
    }
}
