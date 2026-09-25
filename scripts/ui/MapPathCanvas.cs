using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>Adventure terrain, persistent fog, pan/zoom and caravan travel. Rewards live in GameState.</summary>
public partial class MapPathCanvas : Control
{
    public event Action<AdventureMapNode> SiteSelected;
    public event Action TravelStateChanged;
    public string ActiveMapId { get; private set; } = "city";
    public bool IsTravelling { get; private set; }
    public float Zoom { get; private set; } = .78f;
    public Vector2 MapOffset { get; private set; }
    private readonly List<AdventureMapToken> _tokens = new();
    private ShaderMaterial _fog;
    private ColorRect _fogLayer;
    private string _selectedId = "";
    private Vector2 _heroPoint;
    private bool _dragging;
    private float _dragDistance;
    private Tween _travelTween;
    private float _time;
    public override void _Ready()
    {
        ClipContents = true;
        MouseDefaultCursorShape = CursorShape.Drag;
        _fog = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>("res://assets/map/adventure/fog.gdshader") };
        _fogLayer = new ColorRect { Material = _fog, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_fogLayer); _fogLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Resized += UpdateView;
    }
    public void ShowMap(string mapId, string selectedId)
    {
        _travelTween?.Kill(); IsTravelling = false;
        ActiveMapId = RouteCatalog.Normalize(mapId); _selectedId = selectedId;
        foreach (var token in _tokens) { RemoveChild(token); token.QueueFree(); } _tokens.Clear();
        foreach (var node in AdventureMapCatalog.ForMap(ActiveMapId))
        {
            var site = node;
            var token = new AdventureMapToken { Site = site, Size = new Vector2(78,78) };
            token.Pressed += () => { if (!IsTravelling) SiteSelected?.Invoke(site); };
            AddChild(token); _tokens.Add(token);
        }
        _heroPoint = GameState.Instance.GetAdventureHeroPosition(ActiveMapId);
        RefreshKnowledge();
        FocusCaravan();
        Callable.From(FocusCaravan).CallDeferred();
    }
    public void SelectSite(string id) { _selectedId = id; UpdateView(); }
    public void RefreshKnowledge()
    {
        var areas = GameState.Instance.GetAdventureRevealAreas(ActiveMapId).Take(128).ToArray();
        var packed = new Vector3[128]; Array.Copy(areas, packed, areas.Length);
        _fog.SetShaderParameter("revealed", packed); _fog.SetShaderParameter("reveal_count", areas.Length);
        UpdateView();
    }
    public void FocusCaravan() => FocusPoint(_heroPoint);
    public void FocusPoint(Vector2 point) { MapOffset = Size / 2 - point * Zoom; UpdateView(); }
    public void ChangeZoom(float factor, Vector2? around = null)
    {
        var anchor = around ?? Size / 2; var world = (anchor - MapOffset) / Zoom;
        Zoom = Mathf.Clamp(Zoom * factor, Mathf.Max(Size.X / AdventureMapCatalog.WorldSize.X, Size.Y / AdventureMapCatalog.WorldSize.Y), 1.4f);
        MapOffset = anchor - world * Zoom; UpdateView();
    }
    private void UpdateView()
    {
        if (_fog == null || Size.X < 1) return;
        MapOffset = new Vector2(Mathf.Clamp(MapOffset.X, Math.Min(0, Size.X - AdventureMapCatalog.WorldSize.X * Zoom), 0), Mathf.Clamp(MapOffset.Y, Math.Min(0, Size.Y - AdventureMapCatalog.WorldSize.Y * Zoom), 0));
        _fog.SetShaderParameter("canvas_size", Size); _fog.SetShaderParameter("map_offset", MapOffset); _fog.SetShaderParameter("map_zoom", Zoom);
        foreach (var token in _tokens)
        {
            token.Position = token.Site.Point * Zoom + MapOffset - token.Size / 2;
            token.Selected = token.Site.Id == _selectedId;
            token.Visible = GameState.Instance.IsAdventureSiteDiscovered(token.Site.Id) && new Rect2(Vector2.Zero, Size).Encloses(new Rect2(token.Position, token.Size));
            token.Disabled = IsTravelling;
            token.QueueRedraw();
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
                    var walk = _dragging && _dragDistance < 8 && mouse.ButtonIndex == MouseButton.Left;
                    _dragging = false;
                    if (walk) TravelToPoint((mouse.Position - MapOffset) / Zoom);
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
        if (IsTravelling || destination.MapId != ActiveMapId || !GameState.Instance.CanVisitAdventureSite(destination.Id)) return;
        TravelToPoint(destination.Point, arrived);
    }
    public void TravelToPoint(Vector2 destination, Action arrived = null)
    {
        if (IsTravelling || !float.IsFinite(destination.X) || !float.IsFinite(destination.Y)) return;
        destination = destination.Clamp(Vector2.Zero, AdventureMapCatalog.WorldSize - Vector2.One);
        IsTravelling = true; _travelTween = CreateTween();
        // The atlas is freely traversable: movement never visits or clears intervening encounters.
        _travelTween.TweenMethod(Callable.From<Vector2>(point => {
            _heroPoint = point;
            GameState.Instance.MoveAdventureHero(ActiveMapId, point, false);
            FocusPoint(point); RefreshKnowledge();
        }), _heroPoint, destination, Mathf.Clamp(_heroPoint.DistanceTo(destination) / 600, .18, 2.6));
        _travelTween.TweenCallback(Callable.From(() => {
            IsTravelling = false;
            GameState.Instance.MoveAdventureHero(ActiveMapId, _heroPoint);
            arrived?.Invoke(); RefreshKnowledge(); TravelStateChanged?.Invoke();
        }));
        UpdateView(); TravelStateChanged?.Invoke();
    }
    public override void _ExitTree()
    {
        if (IsTravelling) GameState.Instance.MoveAdventureHero(ActiveMapId, _heroPoint);
        _travelTween?.Kill();
    }
    public override void _Process(double delta) { _time += (float)delta; QueueRedraw(); }
    public override void _Draw()
    {
        DrawSetTransform(MapOffset, 0, Vector2.One * Zoom);
        DrawTextureRect(AdventureMapArt.TerrainForMap(ActiveMapId), new Rect2(Vector2.Zero, AdventureMapCatalog.WorldSize), false);
        var stages = AdventureMapCatalog.ForMap(ActiveMapId).Where(x => x.Kind == AdventureSiteKind.Leader).ToArray();
        var last = AdventureMapCatalog.ForMap(ActiveMapId)[0].Point;
        foreach (var stage in stages)
        {
            if (!GameState.Instance.IsAdventureSiteDiscovered(stage.Id)) continue;
            Trail(last, stage.Point); last = stage.Point;
            foreach (var site in AdventureMapCatalog.ForMap(ActiveMapId).Where(x => x.Stage == stage.Stage && x.Kind is not AdventureSiteKind.Leader and not AdventureSiteKind.Camp && GameState.Instance.CanVisitAdventureSite(x.Id))) Trail(stage.Point, site.Point, true);
        }
        var hero = _heroPoint + new Vector2(-45, 35);
        DrawCircle(hero + new Vector2(0,8), 22 / Zoom, new Color(0,0,0,.5f));
        DrawTextureRect(AdventureMapArt.Piece(5), new Rect2(hero - new Vector2(31,47) / Zoom, new Vector2(62,70) / Zoom), false);
        if (IsTravelling) DrawArc(hero, (23 + Mathf.Sin(_time * 8) * 2) / Zoom, 0, Mathf.Tau, 32, new Color("b6e9d4"), 2 / Zoom, true);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
    private void Trail(Vector2 from, Vector2 to, bool branch = false)
    {
        DrawLine(from, to, new Color(.12f,.09f,.04f,.55f), (branch ? 4 : 8) / Zoom, true);
        for (var d = 0f; d < from.DistanceTo(to); d += 19 / Zoom)
            DrawCircle(from.MoveToward(to, d), (branch ? 1.5f : 2.4f) / Zoom, new Color("e1c383aa"));
    }
}
