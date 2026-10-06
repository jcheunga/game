using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>Selectable atlas tiles. Travel is immediate; completion controls revelation.</summary>
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
    private bool _dragging;
    private float _dragDistance, _time;
    public override void _Ready()
    {
        ClipContents = true;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        TextureRepeat = TextureRepeatEnum.Enabled;
        MouseDefaultCursorShape = CursorShape.Drag;
        Resized += UpdateView;
        GameState.Instance.AdventureDiscoveryFound += OnDiscovery;
        CreateMapLayers();
    }
    public void ShowMap(string mapId, string selectedId)
    {
        IsTravelling = false;
        ActiveMapId = RouteCatalog.Normalize(mapId); _selectedId = selectedId;
        _tiles = AdventureTileCatalog.ForMap(ActiveMapId);
        LoadPaintedMap();
        foreach (var token in _tokens) { RemoveChild(token); token.QueueFree(); } _tokens.Clear();
        foreach (var token in _discoveries) { RemoveChild(token); token.QueueFree(); } _discoveries.Clear();
        _rewardBursts.Clear();
        foreach (var tile in _tiles)
        {
            if (tile.Site != null)
            {
                var resource = tile.Site.Kind is AdventureSiteKind.Gold or AdventureSiteKind.Food;
                var boss = tile.Site.Kind == AdventureSiteKind.Leader && GameState.Instance.IsAdventureBoss(tile.Site.Stage);
                var token = new AdventureMapToken { Site = tile.Site, Size = resource ? new Vector2(116, 124) : boss ? new Vector2(144, 200) : new Vector2(136, 172) };
                token.Pressed += () => { if (!IsTravelling) SiteSelected?.Invoke(tile.Site); };
                AddChild(token); _tokens.Add(token);
            }
            else if (tile.Discovery != null)
            {
                var token = new AdventureDiscoveryToken { Discovery = tile.Discovery, Size = new Vector2(116, 124) };
                token.Pressed += () => { if (!IsTravelling) DiscoverySelected?.Invoke(tile.Discovery); };
                AddChild(token); _discoveries.Add(token);
            }
        }
        // The atlas concept shows most of a zone at once.
        Zoom = Mathf.Max(.6f, Mathf.Max(Size.X / MapBounds.Size.X, Size.Y / MapBounds.Size.Y));
        RefreshKnowledge(); FocusCurrentTile(); Callable.From(FocusCurrentTile).CallDeferred();
    }
    public void SelectSite(string id) { _selectedId = id; UpdateView(); }
    public void RefreshKnowledge()
    {
        foreach (var token in _tokens) token.RefreshRating();
        foreach (var token in _discoveries) token.RefreshRating();
        UpdateView();
    }
    public void FocusCurrentTile() => FocusPoint(GameState.Instance.GetAdventureCaravanTile(ActiveMapId).Point);
    public void FocusOverview() => FocusPoint(AdventureTileCatalog.WorldSize * .5f);
    public void FocusPoint(Vector2 point) { MapOffset = new Vector2(Size.X * .5f, Size.Y * .44f) - point * Zoom; UpdateView(); }
    public void FocusSite(string id) { if (AdventureTileCatalog.Find(ActiveMapId, id) is { } tile) FocusPoint(tile.Point); }
    public void ChangeZoom(float factor, Vector2? around = null)
    {
        var anchor = around ?? Size / 2; var world = (anchor - MapOffset) / Zoom;
        var fit = Mathf.Max(Size.X / MapBounds.Size.X, Size.Y / MapBounds.Size.Y);
        Zoom = Mathf.Clamp(Zoom * factor, Mathf.Max(.45f, fit), 1.25f);
        MapOffset = anchor - world * Zoom; UpdateView();
    }
    private void UpdateView()
    {
        if (Size.X < 1) return;
        // A zone is framed by its painting, so the view never runs past the painted sea.
        var bounds = MapBounds;
        var low = Size - bounds.End * Zoom; var high = -bounds.Position * Zoom;
        MapOffset = new Vector2(low.X <= high.X ? Mathf.Clamp(MapOffset.X, low.X, high.X) : (low.X + high.X) / 2,
            low.Y <= high.Y ? Mathf.Clamp(MapOffset.Y, low.Y, high.Y) : (low.Y + high.Y) / 2);
        var view = new Rect2(Vector2.Zero, Size); var state = GameState.Instance;
        foreach (var token in _tokens)
        {
            var tile = AdventureTileCatalog.Find(ActiveMapId, token.Site.Id);
            token.Scale = Vector2.One * Zoom;
            token.Position = tile.Point * Zoom + MapOffset - token.MarkerCenter * token.Scale;
            var collected = (tile.Site.Kind is AdventureSiteKind.Gold or AdventureSiteKind.Food) && state.IsAdventureTileComplete(tile);
            token.Visible = !collected && state.IsAdventureSiteDiscovered(tile.Id) && view.Intersects(new Rect2(token.Position, token.Size * token.Scale));
            token.Disabled = IsTravelling || collected; token.QueueRedraw();
        }
        foreach (var token in _discoveries)
        {
            var tile = AdventureTileCatalog.Find(ActiveMapId, token.Discovery.Id);
            token.Scale = Vector2.One * Zoom;
            token.Position = tile.Point * Zoom + MapOffset - token.MarkerCenter * token.Scale;
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
        IsTravelling = true;
        try
        {
            if (GameState.Instance.TryReachAdventureTile(tile, out var result)) arrived?.Invoke();
            else TravelFeedback?.Invoke(result);
        }
        finally
        {
            IsTravelling = false;
            if (IsInsideTree()) { RefreshKnowledge(); TravelStateChanged?.Invoke(); }
        }
    }
    private void OnDiscovery(AdventureDiscovery reward)
    {
        if (reward.MapId != ActiveMapId) return;
        _rewardBursts.Add((reward, _time));
        AudioDirector.Instance?.PlayRelicPickup();
    }
    public override void _ExitTree()
    {
        GameState.Instance.AdventureDiscoveryFound -= OnDiscovery;
        _atlasVignette?.Dispose(); _atlasVignette = null;
    }
    public override void _Process(double delta)
    {
        _time += (float)delta; _rewardBursts.RemoveAll(reward => _time - reward.Time > 2.4f);
        QueueRedraw();
    }
    public override void _Draw()
    {
        DrawSetTransform(MapOffset, 0, Vector2.One * Zoom);
        DrawPaintedBackground();
        DrawSiteHighlights();
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        UpdateMapLayers();
    }
}
