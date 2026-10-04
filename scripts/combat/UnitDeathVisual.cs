using System;
using Godot;

// A short-lived visual only: the defeated unit is removed from combat immediately.
// Plays the authored death clip, rests on the ground for a moment, then dissolves in its
// faction's style (DeathStyle). Impacted fires once, on the frame the body hits the ground.
public partial class UnitDeathVisual : Node2D
{
    // In a crowded fight bodies rest only briefly so the field never fills with corpses.
    private const int CrowdLimit = 16;
    private static int _active;

    private UnitSpriteSheet _sheet;
    private SpriteAnimRange _clip;
    private Vector2 _size;
    private float _facing;
    private float _age;
    private Color _tint = Colors.White;
    private bool _groundShadowsManaged;
    private DeathStyle _style = new();
    private ShaderMaterial _material;
    private bool _impacted, _dissolving;
    private float _rest, _dissolve;

    public Action<UnitDeathVisual> Impacted { get; set; }
    public Action<UnitDeathVisual> DissolveStarted { get; set; }
    public DeathStyle Style => _style;
    internal static int ActiveCount => _active;

    public void Setup(UnitSpriteSheet sheet, SpriteAnimRange clip, Vector2 size, float facing, Color tint, bool groundShadowsManaged,
        DeathStyle style = null)
    {
        _sheet = sheet;
        _clip = clip;
        _size = size;
        _facing = facing;
        _tint = tint;
        _groundShadowsManaged = groundShadowsManaged;
        _style = style ?? DeathStyle.For(sheet.DeathFx, true, false, size.X * .2f);
    }

    private static bool Reduced => GameState.Instance?.ReducedMotion ?? false;
    private float ClipSeconds => _clip.FrameCount * _clip.FrameDuration;
    private int ImpactFrame => _clip.ImpactFrame >= 0 ? _clip.ImpactFrame : Mathf.FloorToInt(_clip.FrameCount * .6f);
    private int Frame => Mathf.Min((int)(_age / Mathf.Max(.01f, _clip.FrameDuration)), _clip.FrameCount - 1);
    private float DissolveProgress => Mathf.Clamp((_age - ClipSeconds - _rest) / Mathf.Max(.01f, _dissolve), 0, 1);
    private float Alpha => _material == null ? 1 - DissolveProgress : 1;
    internal float Lifetime => ClipSeconds + _rest + _dissolve;

    /// <summary>Ground point under the torso where the body lands.</summary>
    public Vector2 ImpactPosition => GlobalPosition + new Vector2(_clip.ImpactPoint.X * _size.X * (_facing < 0 ? -1 : 1),
        _clip.ImpactPoint.Y * _size.Y);
    /// <summary>Width of the fallen body on screen, for dust and dissolve motes.</summary>
    public float BodyLength => _size.X * .42f;

    public override void _EnterTree() => _active++;
    public override void _ExitTree() => _active--;

    public override void _Ready()
    {
        _rest = Reduced ? .45f : _active > CrowdLimit ? .3f : _style.Linger;
        _dissolve = Reduced ? .3f : _style.Dissolve;
        if (Reduced) return;
        _material = DeathDissolve.CreateMaterial(_style);
        Material = _material;
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (!_impacted && (Frame >= ImpactFrame || _age >= ClipSeconds))
        {
            _impacted = true;
            // Once down, the body lies on the ground layer beneath the living.
            ZIndex -= 1;
            Impacted?.Invoke(this);
        }
        if (!_dissolving && _age >= ClipSeconds + _rest)
        {
            _dissolving = true;
            DissolveStarted?.Invoke(this);
        }
        if (_age >= Lifetime)
        {
            QueueFree();
            return;
        }
        if (_material != null)
        {
            var src = UnitSpriteLoader.GetFrameRect(_sheet, _clip.StartFrame + Frame);
            var tex = _sheet.Texture.GetSize();
            _material.SetShaderParameter("region", new Vector4(src.Position.X / tex.X, src.Position.Y / tex.Y, src.Size.X / tex.X, src.Size.Y / tex.Y));
            _material.SetShaderParameter("progress", DissolveProgress);
            _material.SetShaderParameter("rest", Mathf.Clamp((_age - ClipSeconds * .7f) / .6f, 0, 1));
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_groundShadowsManaged) DrawGroundShadow(this, BattleLighting.ForZone("city"));
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(_facing < 0 ? -1 : 1, 1));
        DrawTextureRectRegion(_sheet.Texture,
            new Rect2(new Vector2(-_size.X * _sheet.AnchorX, -_size.Y * _sheet.AnchorY), _size),
            UnitSpriteLoader.GetFrameRect(_sheet, _clip.StartFrame + Frame), new Color(_tint, Alpha));
    }

    public void DrawGroundShadow(CanvasItem canvas, BattleLighting lighting)
    {
        var feet = canvas == this ? Vector2.Zero : Position;
        var fade = 1 - DissolveProgress;
        var rect = new Rect2(new Vector2(-_size.X * _sheet.AnchorX, -_size.Y * _sheet.AnchorY), _size);
        lighting.DrawShadow(canvas, _sheet.Texture, UnitSpriteLoader.GetFrameRect(_sheet, _clip.StartFrame + Frame), rect, feet, _facing, .85f, fade);
        var r = _style.Radius;
        lighting.DrawContact(canvas, feet, new Vector2(r * .48f, r * .16f), fade * .8f);
    }
}
