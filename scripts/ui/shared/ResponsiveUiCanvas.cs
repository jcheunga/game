using Godot;

// A logical, touch-sized canvas. Containers inside it handle the actual layout.
public partial class ResponsiveUiCanvas : Control
{
    public MarginContainer Content { get; private set; }
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        MouseFilter=MouseFilterEnum.Ignore;
        Content=new MarginContainer(); AddChild(Content);
        Content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        GetViewport().SizeChanged+=ResizeCanvas;
        ResizeCanvas();
    }

    private void ResizeCanvas()
    {
        var scale=MobilePresentation.Enabled?MobilePresentation.HudScale:1f;
        Scale=Vector2.One*scale;
        Size=GetViewportRect().Size/scale;
        Content.AddThemeConstantOverride("margin_left",16+(int)((SafeAreaService.Instance?.MarginLeft??0)/scale));
        Content.AddThemeConstantOverride("margin_right",16+(int)((SafeAreaService.Instance?.MarginRight??0)/scale));
        Content.AddThemeConstantOverride("margin_top",8+(int)((SafeAreaService.Instance?.MarginTop??0)/scale));
        Content.AddThemeConstantOverride("margin_bottom",8+(int)((SafeAreaService.Instance?.MarginBottom??0)/scale));
    }

    public override void _ExitTree()=>GetViewport().SizeChanged-=ResizeCanvas;
}
