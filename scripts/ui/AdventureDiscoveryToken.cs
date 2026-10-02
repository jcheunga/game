using Godot;

public partial class AdventureDiscoveryToken : RealmButton
{
    public AdventureDiscovery Discovery { get; set; }
    public override void _Ready()
    {
        foreach (var state in new[] { "normal","hover","pressed","disabled","focus" }) AddThemeStyleboxOverride(state,new StyleBoxEmpty());
        AccessibilityName = Discovery.Title + ", " + Discovery.RewardText;
        MouseDefaultCursorShape = CursorShape.PointingHand;
    }
    public override void _Draw()
    {
        var center = Size*.5f;
        DrawCircle(center+new Vector2(0,3),19,new Color(0,0,0,.5f));
        DrawCircle(center,17,new Color("17292cd9"));
        DrawArc(center,18,0,Mathf.Tau,32,Discovery.Kind == AdventureDiscoveryKind.Food ? new Color("b2d892") : RealmUi.Gold,1.5f,true);
        DrawTextureRect(HomeMapArt.Icon(Discovery.Icon),new Rect2(center-new Vector2(20,20),new Vector2(40,40)),false);
        if (IsHovered() || HasFocus()) DrawArc(center,21,0,Mathf.Tau,32,new Color("ffe3ae"),2,true);
    }
}
