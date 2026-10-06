using System;
using System.Collections.Generic;
using Godot;

// Displays the authored Blender frames, not the small inventory icon. No combat
// Unit is spawned, so previews cannot affect gameplay, audio, or save progress.
public partial class UnitModelPreview : Control
{
    private static readonly Dictionary<string,Rect2> Bounds=new();
    private UnitSpriteSheet _sheet;
    private bool _ownsTexture;
    private Rect2 _crop;
    private float _time;
    private int _frame;
    private bool _pressed, _dragged;
    private Vector2 _pressPosition;
    public UnitAnimState Animation { get; private set; }=UnitAnimState.Idle;
    public Action InspectRequested { get; set; }
    /// <summary>Stands the model on the bottom edge (a painted pedestal) instead of centring it.</summary>
    public bool AlignBottom { get; set; }
    internal Rect2 ModelRect { get; private set; }
    internal int GlobalFrame => _frame;

    public UnitModelPreview()
    {
        ClipContents=true;
        TextureFilter=TextureFilterEnum.Linear;
        SizeFlagsHorizontal=SizeFlags.ExpandFill;
        SizeFlagsVertical=SizeFlags.ExpandFill;
        // Let the parent card scroller observe presses and drags; only a
        // completed, stationary tap is consumed by the inspector.
        MouseFilter=MouseFilterEnum.Pass;
        MouseDefaultCursorShape=CursorShape.PointingHand;
    }

    public void SetUnit(UnitDefinition unit)
    {
        ReleaseTexture();
        _sheet=UnitSpriteLoader.LoadOwnedPreview(unit.Id);
        _ownsTexture=_sheet!=null;
        _sheet??=UnitSpriteLoader.TryLoad(unit.VisualClass,unit.Id);
        TooltipText=$"Inspect {unit.DisplayName}";
        AccessibilityName=$"Animated model of {unit.DisplayName}";
        if(_sheet!=null)
        {
            var cacheKey=$"{unit.Id}:{_sheet.FrameWidth}x{_sheet.FrameHeight}";
            if(!Bounds.TryGetValue(cacheKey,out _crop))
            {
                // One stable crop across the three preview clips keeps long
                // weapons in frame and prevents scale jumping between poses.
                using var atlas=_sheet.Texture.GetImage();
                if(atlas.IsCompressed()) atlas.Decompress();
                var used=new Rect2I();
                foreach(var state in new[]{UnitAnimState.Idle,UnitAnimState.Walk,UnitAnimState.Attack})
                {
                    if(!_sheet.Animations.TryGetValue(state,out var clip)) continue;
                    for(var i=0;i<clip.FrameCount;i++)
                    {
                        using var frame=atlas.GetRegion((Rect2I)UnitSpriteLoader.GetFrameRect(_sheet,clip.StartFrame+i));
                        var rect=frame.GetUsedRect();
                        if(rect.Size.X>0 && rect.Size.Y>0) used=used.Size==Vector2I.Zero?rect:used.Merge(rect);
                    }
                }
                _crop=used.Size==Vector2I.Zero?new Rect2(0,0,_sheet.FrameWidth,_sheet.FrameHeight):(Rect2)used;
                Bounds[cacheKey]=_crop;
            }
        }
        Play(UnitAnimState.Idle);
    }

    private void ReleaseTexture()
    {
        if(_ownsTexture) _sheet?.Texture?.Dispose();
        _sheet=null; _ownsTexture=false;
    }

    public override void _ExitTree()=>ReleaseTexture();

    public void Play(UnitAnimState state)
    {
        Animation=state; _time=0;
        UpdateFrame(); QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if(!IsVisibleInTree() || _sheet==null || (GameState.Instance?.ReducedMotion??false)) return;
        _time+=(float)delta;
        var old=_frame; UpdateFrame();
        if(old!=_frame) QueueRedraw();
    }

    private void UpdateFrame()
    {
        if(_sheet==null || !_sheet.Animations.TryGetValue(Animation,out var clip)) return;
        if(GameState.Instance?.ReducedMotion??false)
            _frame=clip.StartFrame+(Animation==UnitAnimState.Attack?clip.ContactFrame:0);
        else
        {
            var duration=clip.FrameCount*Mathf.Max(.01f,clip.FrameDuration);
            var phase=_time%(duration+(Animation==UnitAnimState.Attack?.65f:0));
            _frame=phase>=duration?_sheet.Animations[UnitAnimState.Idle].StartFrame:
                clip.StartFrame+Mathf.Min(clip.FrameCount-1,(int)(phase/clip.FrameDuration));
        }
    }

    public override void _Draw()
    {
        if(_sheet==null || _crop.Size.X<=0 || _crop.Size.Y<=0) return;
        var available=(Size-new Vector2(16,12)).Max(Vector2.One);
        var scale=Mathf.Min(available.X/_crop.Size.X,available.Y/_crop.Size.Y);
        var size=_crop.Size*scale;
        ModelRect=AlignBottom?new Rect2(new Vector2((Size.X-size.X)*.5f,Size.Y-8-size.Y),size):new Rect2((Size-size)*.5f,size);
        DrawSetTransform(new Vector2(Size.X*.5f,Size.Y-8),0,new Vector2(Size.X*.34f,6));
        DrawCircle(Vector2.Zero,1,new Color("00000038"));
        DrawSetTransform(Vector2.Zero);
        var source=UnitSpriteLoader.GetFrameRect(_sheet,_frame);
        source.Position+=_crop.Position; source.Size=_crop.Size;
        DrawTextureRectRegion(_sheet.Texture,ModelRect,source);
    }

    public override void _GuiInput(InputEvent input)
    {
        if(input is InputEventMouseButton {ButtonIndex:MouseButton.Left} mouse)
        {
            if(mouse.Pressed) {_pressed=true; _dragged=false; _pressPosition=mouse.Position;}
            else
            {
                var inspect=_pressed && !_dragged && !mouse.Canceled && new Rect2(Vector2.Zero,Size).HasPoint(mouse.Position);
                _pressed=false;
                if(inspect) {InspectRequested?.Invoke(); AcceptEvent();}
            }
        }
        else if(input is InputEventMouseMotion motion && _pressed)
            _dragged|=motion.Position.DistanceTo(_pressPosition)>10;
    }
}
