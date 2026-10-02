using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>Shared tile travel, discoveries and a world-aligned, animated smoke veil.</summary>
public partial class MapPathCanvas : Control
{
    public event Action<AdventureMapNode> SiteSelected;
    public event Action TravelStateChanged;
    public event Action<string> TravelFeedback;
    public string ActiveMapId { get; private set; } = "city";
    public bool IsTravelling { get; private set; }
    public float Zoom { get; private set; } = 1f;
    public Vector2 MapOffset { get; private set; }
    private readonly List<AdventureMapToken> _tokens = new();
    private readonly List<AdventureDiscoveryToken> _discoveries = new();
    private readonly List<(AdventureDiscovery Reward,float Time)> _rewardBursts = new();
    private string _selectedId = "";
    private Vector2 _heroPoint;
    private bool _dragging;
    private float _dragDistance, _time;
    private Tween _travelTween;
    private int _travelSerial, _fogRevision = -1;
    private Texture2D _zoneArtwork;
    private ColorRect _fog;
    private ShaderMaterial _fogMaterial;
    private Image _fogImage;
    private ImageTexture _fogTexture;
    public override void _Ready()
    {
        ClipContents = true; MouseDefaultCursorShape = CursorShape.Drag;
        _fogImage = Image.CreateEmpty(AdventureFogMask.Width,AdventureFogMask.Height,false,Image.Format.L8);
        _fogTexture = ImageTexture.CreateFromImage(_fogImage);
        _fogMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/map/adventure/fog.gdshader") };
        _fogMaterial.SetShaderParameter("charted_mask",_fogTexture);
        _fogMaterial.SetShaderParameter("world_size",AdventureTerrain.WorldSize);
        _fogMaterial.SetShaderParameter("ground_perspective",AdventureFogMask.Perspective);
        _fog = new ColorRect { Color=Colors.White,MouseFilter=MouseFilterEnum.Ignore,Material=_fogMaterial };
        AddChild(_fog); _fog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Resized += UpdateView;
        GameState.Instance.AdventureDiscoveryFound += OnDiscovery;
    }
    public void ShowMap(string mapId, string selectedId)
    {
        _travelSerial++; _travelTween?.Kill(); IsTravelling = false;
        ActiveMapId = RouteCatalog.Normalize(mapId); _selectedId = selectedId; _fogRevision = -1;
        _zoneArtwork = WorldEnvironmentArt.LoadZone(ActiveMapId);
        BuildWaterContours();
        foreach (var token in _tokens) { RemoveChild(token); token.QueueFree(); } _tokens.Clear();
        foreach (var token in _discoveries) { RemoveChild(token); token.QueueFree(); } _discoveries.Clear();
        _rewardBursts.Clear();
        foreach (var node in AdventureMapCatalog.ForMap(ActiveMapId))
        {
            var site = node;
            var token = new AdventureMapToken { Site=site,Size=site.Kind == AdventureSiteKind.Leader ? new Vector2(72,100) : new Vector2(56,64) };
            token.Pressed += () => { if (!IsTravelling) SiteSelected?.Invoke(site); };
            AddChild(token); _tokens.Add(token);
        }
        foreach (var reward in AdventureDiscoveryCatalog.ForMap(ActiveMapId))
        {
            var discovery = reward;
            var token = new AdventureDiscoveryToken { Discovery=discovery,Size=new Vector2(44,44) };
            token.Pressed += () => TravelToPoint(discovery.Point);
            AddChild(token); _discoveries.Add(token);
        }
        MoveChild(_fog,GetChildCount()-1);
        _heroPoint = GameState.Instance.GetAdventureHeroPosition(ActiveMapId);
        Zoom = Mathf.Max(1f, Mathf.Max(Size.X / AdventureTerrain.WorldSize.X, Size.Y / AdventureTerrain.WorldSize.Y));
        RefreshKnowledge(); FocusCaravan(); Callable.From(FocusCaravan).CallDeferred();
    }
    public void SelectSite(string id) { _selectedId=id; UpdateView(); }
    public void RefreshKnowledge()
    {
        foreach (var token in _tokens) token.RefreshRating();
        if (_fogRevision != GameState.Instance.AdventureKnowledgeRevision)
        {
            var charted = Enumerable.Range(0, AdventureTerrain.CellCount)
                .Select(cell => GameState.Instance.IsAdventureCellRevealed(ActiveMapId, cell)).ToArray();
            _fogImage.SetData(AdventureFogMask.Width, AdventureFogMask.Height, false, Image.Format.L8, AdventureFogMask.Build(charted));
            _fogTexture.Update(_fogImage); _fogRevision=GameState.Instance.AdventureKnowledgeRevision;
        }
        UpdateView();
    }
    public void FocusCaravan() => FocusPoint(_heroPoint);
    public void FocusOverview() => FocusPoint(AdventureTerrain.WorldSize * .5f);
    public void FocusPoint(Vector2 point) { MapOffset=Size/2-point*Zoom; UpdateView(); }
    public void ChangeZoom(float factor, Vector2? around=null)
    {
        var anchor=around??Size/2; var world=(anchor-MapOffset)/Zoom;
        // Even the widest exploration view spans multiple screens of terrain.
        var fit=Mathf.Max(Size.X/AdventureTerrain.WorldSize.X,Size.Y/AdventureTerrain.WorldSize.Y);
        Zoom=Mathf.Clamp(Zoom*factor,Mathf.Max(.62f,fit),1.4f);
        MapOffset=anchor-world*Zoom; UpdateView();
    }
    private void UpdateView()
    {
        if (Size.X<1 || _fogMaterial==null) return;
        MapOffset=new Vector2(Mathf.Clamp(MapOffset.X,Math.Min(0,Size.X-AdventureTerrain.WorldSize.X*Zoom),0),Mathf.Clamp(MapOffset.Y,Math.Min(0,Size.Y-AdventureTerrain.WorldSize.Y*Zoom),0));
        var view=new Rect2(Vector2.Zero,Size); var state=GameState.Instance;
        foreach (var token in _tokens)
        {
            token.Scale=Vector2.One*Mathf.Clamp(Zoom/.78f,.75f,1.25f);
            token.Position=token.Site.Point*Zoom+MapOffset-token.MarkerCenter*token.Scale;
            token.Selected=token.Site.Id==_selectedId;
            token.Visible=state.IsAdventureSiteDiscovered(token.Site.Id) && view.Intersects(new Rect2(token.Position,token.Size*token.Scale));
            token.Disabled=IsTravelling; token.QueueRedraw();
        }
        foreach (var token in _discoveries)
        {
            token.Scale=Vector2.One*Mathf.Clamp(Zoom/.78f,.65f,1.15f);
            token.Position=token.Discovery.Point*Zoom+MapOffset-token.Size*token.Scale*.5f;
            token.Visible=state.IsAdventureCellRevealed(ActiveMapId,token.Discovery.Cell) && !state.HasClaimedAdventureDiscovery(token.Discovery.Id) && view.Intersects(new Rect2(token.Position,token.Size*token.Scale));
            token.Disabled=IsTravelling;
        }
        _fogMaterial.SetShaderParameter("canvas_size",Size);
        _fogMaterial.SetShaderParameter("map_offset",MapOffset);
        _fogMaterial.SetShaderParameter("map_zoom",Zoom);
        _fogMaterial.SetShaderParameter("hero_point",_heroPoint);
        QueueRedraw();
    }
    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton mouse)
        {
            if (mouse.ButtonIndex==MouseButton.WheelUp && mouse.Pressed) { ChangeZoom(1.12f,mouse.Position); AcceptEvent(); }
            if (mouse.ButtonIndex==MouseButton.WheelDown && mouse.Pressed) { ChangeZoom(1/1.12f,mouse.Position); AcceptEvent(); }
            if (mouse.ButtonIndex is MouseButton.Left or MouseButton.Middle)
            {
                if (mouse.Pressed) { _dragging=true; _dragDistance=0; }
                else { var walk=_dragging && _dragDistance<8 && mouse.ButtonIndex==MouseButton.Left; _dragging=false;
                    if (walk) TravelToPoint((mouse.Position-MapOffset)/Zoom); }
                AcceptEvent();
            }
        }
        if (input is InputEventMouseMotion motion && _dragging) { _dragDistance+=motion.Relative.Length(); MapOffset+=motion.Relative; UpdateView(); AcceptEvent(); }
        if (input is InputEventScreenDrag drag) { _dragDistance+=drag.Relative.Length(); MapOffset+=drag.Relative; UpdateView(); AcceptEvent(); }
        if (input is InputEventMagnifyGesture pinch) { ChangeZoom(pinch.Factor,pinch.Position); AcceptEvent(); }
    }
    public void TravelTo(AdventureMapNode destination,Action arrived)
    {
        if (IsTravelling || destination.MapId!=ActiveMapId || !GameState.Instance.CanVisitAdventureSite(destination.Id)) return;
        TravelToPoint(destination.Point,arrived);
    }
    public void TravelToPoint(Vector2 destination,Action arrived=null)
    {
        if (IsTravelling || !float.IsFinite(destination.X) || !float.IsFinite(destination.Y)) return;
        var path=AdventureTerrain.Path(ActiveMapId,AdventureTerrain.Cell(_heroPoint),AdventureTerrain.Cell(destination));
        if (path.Length==0) { TravelFeedback?.Invoke("Choose a ground tile."); return; }
        if (!GameState.Instance.TryBeginAdventureTravel(ActiveMapId,path,out var message)) { TravelFeedback?.Invoke(message); return; }
        IsTravelling=true; _travelSerial++; TravelFeedback?.Invoke(message); UpdateView(); TravelStateChanged?.Invoke();
        ContinueTravel(path,1,_travelSerial,arrived);
    }
    private void ContinueTravel(int[] path,int step,int serial,Action arrived)
    {
        if (serial!=_travelSerial || !IsInsideTree()) return;
        if (step>=path.Length)
        {
            GameState.Instance.CompleteAdventureStep(ActiveMapId,path[^1]);
            IsTravelling=false; arrived?.Invoke(); RefreshKnowledge(); TravelStateChanged?.Invoke(); return;
        }
        if (!GameState.Instance.TryPayAdventureStep(ActiveMapId,path[step],out var message))
        {
            IsTravelling=false; GameState.Instance.MoveAdventureHero(ActiveMapId,_heroPoint);
            TravelFeedback?.Invoke(message); RefreshKnowledge(); TravelStateChanged?.Invoke(); return;
        }
        var to=AdventureTerrain.Point(path[step]); _travelTween=CreateTween();
        _travelTween.TweenMethod(Callable.From<Vector2>(point => {
            _heroPoint=point;
            if (!_dragging)
            {
                var screen=point*Zoom+MapOffset;
                if (!new Rect2(new Vector2(100,80),Size-new Vector2(200,160)).HasPoint(screen))
                    MapOffset=MapOffset.Lerp(Size/2-point*Zoom,.15f);
            }
            UpdateView();
        }),_heroPoint,to,.28);
        _travelTween.TweenCallback(Callable.From(() => {
            if (serial!=_travelSerial) return;
            _heroPoint=to; GameState.Instance.CompleteAdventureStep(ActiveMapId,path[step]); RefreshKnowledge();
            ContinueTravel(path,step+1,serial,arrived);
        }));
    }
    private void OnDiscovery(AdventureDiscovery reward)
    {
        if (reward.MapId!=ActiveMapId) return;
        _rewardBursts.Add((reward,_time)); TravelFeedback?.Invoke(reward.RewardText);
        AudioDirector.Instance?.PlayRelicPickup();
    }
    public override void _ExitTree()
    {
        if (IsTravelling) GameState.Instance.MoveAdventureHero(ActiveMapId,_heroPoint);
        _travelSerial++; _travelTween?.Kill(); GameState.Instance.AdventureDiscoveryFound-=OnDiscovery;
        _zoneArtwork=null; _fog.Material=null; _fogMaterial?.Dispose(); _fogTexture?.Dispose(); _fogImage?.Dispose();
    }
    public override void _Process(double delta)
    {
        _time+=(float)delta; _rewardBursts.RemoveAll(r => _time-r.Time>2.4f);
        _fogMaterial?.SetShaderParameter("fog_time",GameState.Instance.ReducedMotion ? 0f : _time);
        QueueRedraw();
    }
    public override void _Draw()
    {
        DrawSetTransform(MapOffset,0,Vector2.One*Zoom);
        if (_zoneArtwork!=null) DrawTextureRect(_zoneArtwork,new Rect2(Vector2.Zero,AdventureTerrain.WorldSize),false);
        DrawTerrainScenery();
        DrawCircle(_heroPoint+new Vector2(0,8),22/Zoom,new Color(0,0,0,.5f));
        DrawTextureRect(AdventureMapArt.Piece(5),new Rect2(_heroPoint-new Vector2(24,36)/Zoom,new Vector2(48,54)/Zoom),false);
        foreach (var burst in _rewardBursts)
        {
            var age=_time-burst.Time; var lift=GameState.Instance.ReducedMotion ? 0 : age*14;
            var p=burst.Reward.Point+new Vector2(-26,-48-lift)/Zoom;
            var color=new Color("fff1be") {A=Mathf.Clamp((2.4f-age)*1.5f,0,1)};
            DrawString(ThemeDB.FallbackFont,p,burst.Reward.RewardText,fontSize:Mathf.RoundToInt(18/Zoom),modulate:color);
        }
        DrawSetTransform(Vector2.Zero,0,Vector2.One);
    }
}
