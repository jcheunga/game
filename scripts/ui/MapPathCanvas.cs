using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>Selectable atlas tiles. Caravan motion is cosmetic; completion controls revelation.</summary>
public partial class MapPathCanvas : Control
{
    public event Action<AdventureMapNode> SiteSelected;
    public event Action<AdventureDiscovery> DiscoverySelected;
    public event Action TravelStateChanged;
    public event Action<string> TravelFeedback;
    public string ActiveMapId { get; private set; } = "city";
    public bool IsTravelling { get; private set; }
    public float Zoom { get; private set; } = 1f;
    public Vector2 MapOffset { get; private set; }
    private readonly List<AdventureMapToken> _tokens = new();
    private readonly List<AdventureDiscoveryToken> _discoveries = new();
    private readonly List<(AdventureDiscovery Reward, float Time)> _rewardBursts = new();
    private IReadOnlyList<AdventureTile> _tiles = Array.Empty<AdventureTile>();
    private string _selectedId = "";
    private Vector2 _heroPoint;
    private bool _dragging;
    private float _dragDistance, _time;
    private Tween _travelTween;
    private Texture2D _zoneArtwork;
    private int _travelSerial;
    public override void _Ready()
    {
        ClipContents = true;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        TextureRepeat = TextureRepeatEnum.Enabled;
        MouseDefaultCursorShape = CursorShape.Drag;
        Resized += UpdateView;
        GameState.Instance.AdventureDiscoveryFound += OnDiscovery;
    }
    public void ShowMap(string mapId, string selectedId)
    {
        _travelSerial++; _travelTween?.Kill(); IsTravelling = false;
        ActiveMapId = RouteCatalog.Normalize(mapId); _selectedId = selectedId;
        _tiles = AdventureTileCatalog.ForMap(ActiveMapId);
        BuildAtlasMaterials();
        BuildLandscapeScenery();
        _zoneArtwork = WorldEnvironmentArt.LoadZone(ActiveMapId);
        foreach (var token in _tokens) { RemoveChild(token); token.QueueFree(); } _tokens.Clear();
        foreach (var token in _discoveries) { RemoveChild(token); token.QueueFree(); } _discoveries.Clear();
        _rewardBursts.Clear();
        foreach (var tile in _tiles)
        {
            if (tile.Site != null)
            {
                var token = new AdventureMapToken { Site = tile.Site, Size = new Vector2(104, 130) };
                token.Pressed += () => { if (!IsTravelling) SiteSelected?.Invoke(tile.Site); };
                AddChild(token); _tokens.Add(token);
            }
            else if (tile.Discovery != null)
            {
                var token = new AdventureDiscoveryToken { Discovery = tile.Discovery, Size = new Vector2(90, 100) };
                token.Pressed += () => { if (!IsTravelling) DiscoverySelected?.Invoke(tile.Discovery); };
                AddChild(token); _discoveries.Add(token);
            }
        }
        _heroPoint = GameState.Instance.GetAdventureCaravanTile(ActiveMapId).Point;
        Zoom = Mathf.Max(.86f, Mathf.Max(Size.X / AdventureTileCatalog.WorldSize.X, Size.Y / AdventureTileCatalog.WorldSize.Y));
        RefreshKnowledge(); FocusCaravan(); Callable.From(FocusCaravan).CallDeferred();
    }
    public void SelectSite(string id) { _selectedId = id; UpdateView(); }
    public void RefreshKnowledge()
    {
        foreach (var token in _tokens) token.RefreshRating();
        foreach (var token in _discoveries) token.RefreshRating();
        UpdateLandscapeFrontier();
        UpdateView();
    }
    public void FocusCaravan() => FocusPoint(_heroPoint);
    public void FocusOverview() => FocusPoint(AdventureTileCatalog.WorldSize * .5f);
    public void FocusPoint(Vector2 point) { MapOffset = new Vector2(Size.X * .5f, Size.Y * .44f) - point * Zoom; UpdateView(); }
    public void FocusSite(string id) { if (AdventureTileCatalog.Find(ActiveMapId, id) is { } tile) FocusPoint(tile.Point); }
    public void ChangeZoom(float factor, Vector2? around = null)
    {
        var anchor = around ?? Size / 2; var world = (anchor - MapOffset) / Zoom;
        var fit = Mathf.Max(Size.X / AdventureTileCatalog.WorldSize.X, Size.Y / AdventureTileCatalog.WorldSize.Y);
        Zoom = Mathf.Clamp(Zoom * factor, Mathf.Max(.45f, fit), 1.25f);
        MapOffset = anchor - world * Zoom; UpdateView();
    }
    private void UpdateView()
    {
        if (Size.X < 1) return;
        var world = AdventureTileCatalog.WorldSize;
        MapOffset = new Vector2(Mathf.Clamp(MapOffset.X, Math.Min(0, Size.X - world.X * Zoom), 0), Mathf.Clamp(MapOffset.Y, Math.Min(0, Size.Y - world.Y * Zoom), 0));
        var view = new Rect2(Vector2.Zero, Size); var state = GameState.Instance;
        foreach (var token in _tokens)
        {
            var tile = AdventureTileCatalog.Find(ActiveMapId, token.Site.Id);
            token.Scale = Vector2.One * Mathf.Clamp(Zoom, .65f, 1.15f);
            token.Position = tile.Point * Zoom + MapOffset - token.MarkerCenter * token.Scale;
            token.Selected = tile.Id == _selectedId;
            var collected = (tile.Site.Kind is AdventureSiteKind.Gold or AdventureSiteKind.Food) && state.IsAdventureTileComplete(tile);
            token.Visible = !collected && state.IsAdventureSiteDiscovered(tile.Id) && view.Intersects(new Rect2(token.Position, token.Size * token.Scale));
            token.Disabled = IsTravelling || collected; token.QueueRedraw();
        }
        foreach (var token in _discoveries)
        {
            var tile = AdventureTileCatalog.Find(ActiveMapId, token.Discovery.Id);
            token.Scale = Vector2.One * Mathf.Clamp(Zoom, .65f, 1.15f);
            token.Position = tile.Point * Zoom + MapOffset - new Vector2(token.Size.X / 2, 66) * token.Scale;
            token.Visible = state.IsAdventureTileOpen(tile) && !state.IsAdventureTileComplete(tile) && view.Intersects(new Rect2(token.Position, token.Size * token.Scale));
            token.Disabled = IsTravelling; token.QueueRedraw();
        }
        QueueRedraw();
    }
    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton mouse)
        {
            if (mouse.ButtonIndex == MouseButton.WheelUp && mouse.Pressed) { ChangeZoom(1.12f, mouse.Position); AcceptEvent(); }
            if (mouse.ButtonIndex == MouseButton.WheelDown && mouse.Pressed) { ChangeZoom(1 / 1.12f, mouse.Position); AcceptEvent(); }
            if (mouse.ButtonIndex is MouseButton.Left or MouseButton.Middle)
            {
                if (mouse.Pressed) { _dragging = true; _dragDistance = 0; }
                else
                {
                    var select = _dragging && _dragDistance < 8 && mouse.ButtonIndex == MouseButton.Left;
                    _dragging = false;
                    if (select && AdventureTileCatalog.At(ActiveMapId, (mouse.Position - MapOffset) / Zoom) is { } tile && GameState.Instance.IsAdventureTileOpen(tile))
                    {
                        if (tile.Site != null && GameState.Instance.IsAdventureSiteDiscovered(tile.Id)) SiteSelected?.Invoke(tile.Site);
                        else if (tile.Discovery != null) DiscoverySelected?.Invoke(tile.Discovery);
                    }
                }
                AcceptEvent();
            }
        }
        if (input is InputEventMouseMotion motion && _dragging) { _dragDistance += motion.Relative.Length(); MapOffset += motion.Relative; UpdateView(); AcceptEvent(); }
        if (input is InputEventScreenDrag drag) { _dragDistance += drag.Relative.Length(); MapOffset += drag.Relative; UpdateView(); AcceptEvent(); }
        if (input is InputEventMagnifyGesture pinch) { ChangeZoom(pinch.Factor, pinch.Position); AcceptEvent(); }
    }
    public void TravelTo(AdventureMapNode destination, Action arrived)
    {
        if (destination == null || destination.MapId != ActiveMapId) return;
        TravelToTile(AdventureTileCatalog.Find(ActiveMapId, destination.Id), arrived);
    }
    // Compatibility for callers that hold a legacy site's coordinates; empty terrain never initiates travel.
    public void TravelToPoint(Vector2 destination, Action arrived = null)
    {
        var tile = _tiles.FirstOrDefault(tile => tile.Site?.Point == destination || tile.Discovery?.Point == destination);
        if (tile == null) return;
        TravelToTile(tile, arrived ?? (tile.Discovery != null ? () => GameState.Instance.TryCollectAdventureTile(tile, out _) : null));
    }
    public void TravelToTile(AdventureTile tile, Action arrived)
    {
        if (IsTravelling || tile == null || tile.MapId != ActiveMapId) return;
        if (!GameState.Instance.CanTravelToAdventureTile(tile, out var message)) { TravelFeedback?.Invoke(message); return; }
        IsTravelling = true; var serial = ++_travelSerial;
        UpdateView(); TravelStateChanged?.Invoke();
        var from = _heroPoint;
        void Complete()
        {
            if (serial != _travelSerial || !IsInsideTree()) return;
            if (GameState.Instance.TryReachAdventureTile(tile, out var result)) { _heroPoint = tile.Point; arrived?.Invoke(); }
            else { _heroPoint = from; TravelFeedback?.Invoke(result); }
            IsTravelling = false;
            if (!IsInsideTree()) return;
            RefreshKnowledge(); TravelStateChanged?.Invoke();
        }
        if (GameState.Instance.ReducedMotion || from.DistanceTo(tile.Point) < 1) { Callable.From(Complete).CallDeferred(); return; }
        _travelTween = CreateTween();
        _travelTween.TweenMethod(Callable.From<Vector2>(point => { _heroPoint = point; UpdateView(); }), from, tile.Point, .55).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _travelTween.TweenCallback(Callable.From(Complete));
    }
    private void OnDiscovery(AdventureDiscovery reward)
    {
        if (reward.MapId != ActiveMapId) return;
        _rewardBursts.Add((reward, _time));
        AudioDirector.Instance?.PlayRelicPickup();
    }
    public override void _ExitTree()
    {
        _travelSerial++; _travelTween?.Kill();
        GameState.Instance.AdventureDiscoveryFound -= OnDiscovery;
        _atlasMist?.Dispose(); _atlasMist = null;
        _atlasVignette?.Dispose(); _atlasVignette = null;
        _zoneArtwork = null;
    }
    public override void _Process(double delta)
    {
        _time += (float)delta; _rewardBursts.RemoveAll(reward => _time - reward.Time > 2.4f);
        QueueRedraw();
    }
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), TileMistColor());
        DrawSetTransform(MapOffset, 0, Vector2.One * Zoom);
        DrawLandscapeBackground();
        DrawAtlasTiles();
        var caravanBob = IsTravelling && !GameState.Instance.ReducedMotion ? Mathf.Sin(_time * 9) * 1.3f : 0;
        var caravanPoint = _heroPoint + new Vector2(50, 18 + caravanBob);
        if (!DrawPaintedSprite(24, caravanPoint, 45)) DrawCaravan(caravanPoint, IsTravelling);
        foreach (var burst in _rewardBursts)
        {
            var tile = AdventureTileCatalog.Find(ActiveMapId, burst.Reward.Id);
            var age = _time - burst.Time;
            var lift = GameState.Instance.ReducedMotion ? 0 : age * 16;
            var p = tile.Point + new Vector2(-38, -70 - lift);
            DrawString(ThemeDB.FallbackFont, p, burst.Reward.RewardText, fontSize: 20, modulate: new Color("fff1be") { A = Mathf.Clamp((2.4f - age) * 1.5f, 0, 1) });
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        if (_atlasVignette != null) DrawTextureRect(_atlasVignette, new Rect2(Vector2.Zero, Size), false);
    }
}
