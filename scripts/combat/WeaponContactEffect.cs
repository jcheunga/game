using System;
using Godot;

// A short, directional stroke at the struck surface, rather than a ground explosion: a tapered smear
// along the swing's path, a hot flash where it lands, and for heavy blows a scuff of dust.
public partial class WeaponContactEffect : Node2D
{
    private const float Life = .2f;
    public Func<bool> ShouldPause { get; set; }
    private float _age;
    private float _direction;
    private Color _color;
    private bool _thrust;
    private bool _heavy;
    private bool _reduced;
    private bool _isBeam;
    private Vector2 _beamEnd;
    private float _tilt;

    public override void _Ready()
    {
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
    }

    public void SetupBeam(Vector2 end, Color color, bool reduced)
    {
        _isBeam=true; _beamEnd=end; _color=color; _reduced=reduced;
    }
    public void Setup(string profile, float direction, Color color, bool reduced)
    {
        _direction=direction; _color=color.Lightened(.55f); _reduced=reduced;
        _thrust=profile.Contains("thrust") || profile.Contains("lance") || profile.Contains("stab");
        _heavy=profile.Contains("cleave") || profile.Contains("smash");
        // Vary each swing a little so a flurry does not stamp the same mark.
        _tilt = (float)GD.RandRange(-.25, .25);
    }
    public override void _Process(double delta)
    {
        if (ShouldPause?.Invoke() ?? false) return;
        _age+=(float)delta;
        if (_age >= Life) QueueFree();
        QueueRedraw();
    }

    public override void _Draw()
    {
        var t=Mathf.Clamp(_age/Life,0,1);
        var fade=1-t;
        if (_isBeam)
        {
            DrawLine(Vector2.Zero,_beamEnd,new Color(_color,fade*.2f),_reduced?2:7,true);
            DrawLine(Vector2.Zero,_beamEnd,new Color(_color,fade*.85f),_reduced?1:2,true);
            DrawCircle(_beamEnd,3*fade,new Color(_color,fade*.85f));
            return;
        }
        if (_reduced) { DrawCircle(Vector2.Zero,2.5f,new Color(_color,fade*.85f)); return; }
        DrawSetTransform(Vector2.Zero,_tilt*_direction,new Vector2(_direction,1));
        if (_thrust) DrawThrust(t,fade);
        else DrawSwing(t,fade);
        DrawSetTransform(Vector2.Zero,0,Vector2.One);
        // The hot point of contact flares and shrinks quickly.
        var flash=Mathf.Clamp(1-t*2.2f,0,1);
        if (flash>0)
        {
            DrawCircle(Vector2.Zero,(_heavy?6.5f:4.5f)*(.6f+.4f*flash),new Color(_color,.35f*flash));
            DrawCircle(Vector2.Zero,(_heavy?3.2f:2.2f)*flash,new Color(1,.97f,.88f,.95f*flash));
            var spike=(_heavy?11:8)*flash;
            DrawLine(new Vector2(-spike,0),new Vector2(spike,0),new Color(1,.95f,.8f,.8f*flash),1.2f,true);
            DrawLine(new Vector2(0,-spike*.7f),new Vector2(0,spike*.7f),new Color(1,.95f,.8f,.6f*flash),1f,true);
        }
    }

    // A crescent swept through the target: thick in the middle of the arc, tapering at both ends, and
    // sweeping on past the contact point as it fades.
    private void DrawSwing(float t, float fade)
    {
        var radius=_heavy?19f:14f;
        var width=(_heavy?5.5f:4f)*(.5f+.5f*fade);
        var sweep=Mathf.Lerp(-.2f,.35f,t);
        var from=-1.25f+sweep;
        var to=1.05f+sweep;
        const int steps=14;
        var outer=new Vector2[steps+1];
        var inner=new Vector2[steps+1];
        var centre=new Vector2(-radius*.62f,0);
        for (var i=0;i<=steps;i++)
        {
            var u=(float)i/steps;
            var a=Mathf.Lerp(from,to,u);
            var w=width*Mathf.Sin(u*Mathf.Pi)*(.35f+.65f*u);
            var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
            outer[i]=centre+d*(radius+w*.5f);
            inner[i]=centre+d*(radius-w*.5f);
        }
        var points=new Vector2[(steps+1)*2];
        var colors=new Color[points.Length];
        for (var i=0;i<=steps;i++)
        {
            var u=(float)i/steps;
            var c=new Color(_color.Lerp(Colors.White,u*.5f),fade*(.15f+.75f*u));
            points[i]=outer[i]; colors[i]=c;
            points[points.Length-1-i]=inner[i]; colors[points.Length-1-i]=c;
        }
        DrawPolygon(points,colors);
        DrawPolyline(outer,new Color(1,.96f,.86f,.55f*fade),1f,true);
    }

    // A tapered streak driven through the target, with a short echo behind it.
    private void DrawThrust(float t, float fade)
    {
        var reach=Mathf.Lerp(18f,24f,t);
        var head=new Vector2(6f+4f*t,0);
        var tail=head-new Vector2(reach,0);
        var w=3.2f*fade;
        DrawPolygon(new[]{tail,head-new Vector2(reach*.35f,-w),head,head-new Vector2(reach*.35f,w)},
            new[]{new Color(_color,0),new Color(_color,.7f*fade),new Color(1,.97f,.9f,.95f*fade),new Color(_color,.7f*fade)});
        DrawLine(tail+new Vector2(-6,-3),head+new Vector2(-10,-3),new Color(_color,.25f*fade),1f,true);
        DrawLine(tail+new Vector2(-6,3),head+new Vector2(-10,3),new Color(_color,.25f*fade),1f,true);
    }
}
