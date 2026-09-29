using System;
using Godot;

// A short, directional stroke at the struck surface, rather than a ground explosion.
public partial class WeaponContactEffect : Node2D
{
    public Func<bool> ShouldPause { get; set; }
    private float _age;
    private float _direction;
    private Color _color;
    private bool _thrust;
    private bool _heavy;
    private bool _reduced;
    private bool _isBeam;
    private Vector2 _beamEnd;
    public void SetupBeam(Vector2 end, Color color, bool reduced)
    {
        _isBeam=true; _beamEnd=end; _color=color; _reduced=reduced;
    }
    public void Setup(string profile, float direction, Color color, bool reduced)
    {
        _direction=direction; _color=color.Lightened(.55f); _reduced=reduced;
        _thrust=profile.Contains("thrust") || profile.Contains("lance") || profile.Contains("stab");
        _heavy=profile.Contains("cleave") || profile.Contains("smash");
    }
    public override void _Process(double delta)
    {
        if (ShouldPause?.Invoke() ?? false) return;
        _age+=(float)delta;
        if (_age >= .16f) QueueFree();
        QueueRedraw();
    }
    public override void _Draw()
    {
        var fade=1-Mathf.Clamp(_age/.16f,0,1);
        var color=new Color(_color,fade*.85f);
        if (_isBeam)
        {
            DrawLine(Vector2.Zero,_beamEnd,new Color(_color,fade*.2f),_reduced?2:7,true);
            DrawLine(Vector2.Zero,_beamEnd,color,_reduced?1:2,true);
            DrawCircle(_beamEnd,3*fade,color);
            return;
        }
        if (_reduced) { DrawCircle(Vector2.Zero,2.5f,color); return; }
        DrawSetTransform(Vector2.Zero,0,new Vector2(_direction,1));
        if (_thrust) DrawLine(new Vector2(-19,1),new Vector2(5,-1),color,2*fade,true);
        else DrawArc(new Vector2(-9,0),_heavy?18:13,-1.05f,1.1f,12,color,(_heavy?3:2)*fade,true);
        DrawLine(new Vector2(-4,-7),new Vector2(4,7),new Color(1,.92f,.73f,fade),1.5f,true);
        DrawLine(new Vector2(-6,3),new Vector2(6,-3),color,1.5f,true);
    }
}
