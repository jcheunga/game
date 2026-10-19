using Godot;

public partial class BattleController
{
    private void ClearContentCast(Unit unit)
    {
        if(_contentCasts.Remove(unit,out var cast))cast.Warning?.QueueFree();
        unit.MarkedTime=0;
    }
}

// The telegraph of a channelled ability: a sigil on the ground under whoever it lands on (or the caster), its
// runes turning, the inner ring filling as the cast charges, and a short label so a player learns what is coming.
// It follows its unit and flares as it completes. Drawn flat on the ground plane (BattleGroundPlane.DepthScale).
public partial class BattleContentWarning : Node2D
{
    public string Label="CAST";
    public float Progress;
    public Color Tint=new("e8b876");
    public float Radius=22f;
    public Unit Follow;
    public string School="war";
    private float _age;

    public override void _Ready()
    {
        ZAsRelative=false; ZIndex=-3;            // on the ground, under the troops
    }

    public override void _Process(double delta)
    {
        _age+=(float)delta;
        if (Follow!=null && IsInstanceValid(Follow) && !Follow.IsDead) Position=Follow.Position;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var squash=new Vector2(1,BattleGroundPlane.DepthScale);
        var r=Radius*(.92f+.08f*Mathf.Sin(_age*9));
        var glow=new Color(Tint,.10f+.12f*Progress);
        DrawSetTransform(Vector2.Zero,0,squash);
        DrawCircle(Vector2.Zero,r*1.08f,glow);
        // the painted rune circle of the ability's school, turning
        var sigil=SpellVfx.Tex("sigil_"+School);
        DrawSetTransform(Vector2.Zero,0,squash);
        DrawSetTransformMatrix(Transform2D.Identity.Scaled(squash)*Transform2D.Identity.Rotated(_age*.8f));
        DrawTextureRect(sigil,new Rect2(-r*1.15f,-r*1.15f,r*2.3f,r*2.3f),false,new Color(Tint,.45f+.4f*Progress));
        DrawSetTransform(Vector2.Zero,0,squash);
        DrawArc(Vector2.Zero,r,0,Mathf.Tau,48,new Color(Tint,.55f),1.4f,true);
        // the charge: an arc sweeping round the inner ring
        if (Progress>0.001f) DrawArc(Vector2.Zero,r*.86f,-Mathf.Pi/2,-Mathf.Pi/2+Mathf.Tau*Progress,48,new Color(Tint.Lightened(.25f),.95f),3f,true);
        // runes: short ticks turning round the outer ring
        for (var k=0;k<8;k++)
        {
            var a=_age*1.4f+k*Mathf.Tau/8;
            var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
            DrawLine(d*r*1.0f,d*r*1.18f,new Color(Tint,.7f),1.4f,true);
        }
        DrawSetTransform(Vector2.Zero,0,Vector2.One);
        var font=RoyalFonts.Body(700);
        var size=font.GetStringSize(Label,HorizontalAlignment.Left,-1,11);
        var at=new Vector2(-size.X/2,-r*BattleGroundPlane.DepthScale-6);
        DrawStringOutline(font,at,Label,HorizontalAlignment.Left,-1,11,3,new Color(0,0,0,.65f));
        DrawString(font,at,Label,HorizontalAlignment.Left,-1,11,new Color(Tint.Lightened(.2f),.95f));
    }
}
