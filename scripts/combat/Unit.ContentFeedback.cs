using System.Collections.Generic;
using Godot;

public partial class Unit
{
    private readonly List<string> _contentStatusLabels = new(10);
    private static readonly Dictionary<string, float> StatusWidths = new();
    private static readonly Vector2[] MarkChevron = { new(-5, 0), new(0, 5), new(5, 0) };
    public IReadOnlyList<string> ContentStatusLabels
    {
        get
        {
            var labels=_contentStatusLabels;
            labels.Clear();
            if(IsControlled)labels.Add(_wakeOnDamage?"SLEEP":"STUN");else if(MovementControlled)labels.Add("ROOT");
            if(IsSilenced)labels.Add("SIL");if(ArmorBreak>0)labels.Add("ARM−");
            if(HealingReceivedScale<1)labels.Add("WOUND");if(IsBurning)labels.Add("BURN");
            if(ShieldHealth>0)labels.Add("SHIELD");if(FocusMarkTime>0)labels.Add("MARK");
            if(MarkedTime>0)labels.Add("CAST");if(IsConcealed)labels.Add("HIDE");
            return labels;
        }
    }
    private void DrawContentFeedback()
    {
        if(IsDead)return;
        var height=_spriteSheet==null?Radius+16:Radius*2*VisualScale*_spriteSheet.DrawScale*_spriteSheet.FrameHeight/_spriteSheet.FrameWidth*_spriteSheet.HealthBarY+6;
        var labels=ContentStatusLabels;var x=-24f;var y=-height-16;
        var font=RoyalFonts.Body(600);
        // stun, sleep, root, silence, burning and shields show on the body (UnitStatusFx), so only high contrast
        // repeats them as chips
        var all=GameState.Instance?.HighContrast ?? false;
        for(int i=0,drawn=0;i<labels.Count && drawn<3;i++)
        {
            var label=labels[i];
            if(!all && label is "STUN" or "SLEEP" or "ROOT" or "SIL" or "BURN" or "SHIELD") continue;
            drawn++;
            if (!StatusWidths.TryGetValue(label, out var width))
                StatusWidths[label] = width = font.GetStringSize(label,fontSize:11).X+6;
            var good=label is "SHIELD" or "HIDE";var tint=new Color(good?"8bc7e2":label is "MARK" or "CAST"?"efd082":"eab18d");
            DrawRect(new Rect2(x,y-11,width,14),new Color("17222aed"));
            DrawString(font,new Vector2(x+3,y),label,fontSize:11,modulate:tint);x+=width+3;
        }
        if(ShieldHealth>0)DrawLine(new Vector2(-15,-height+12),new Vector2(-15+30*Mathf.Clamp(ShieldHealth/(MaxHealth*.35f),0,1),-height+12),new Color("8bc7e2"),2);
        if(FocusMarkTime>0)
        {
            DrawSetTransform(new Vector2(0, -height-34));
            DrawPolyline(MarkChevron,new Color("efd082"),2,true);
            DrawSetTransform(Vector2.Zero);
        }
    }
}
