using Godot;

// A short-lived visual only: the defeated unit is removed from combat immediately.
public partial class UnitDeathVisual : Node2D
{
    private UnitSpriteSheet _sheet;
    private SpriteAnimRange _clip;
    private Vector2 _size;
    private float _facing;
    private float _age;
    private Color _tint = Colors.White;
    private bool _groundShadowsManaged;

    public void Setup(UnitSpriteSheet sheet, SpriteAnimRange clip, Vector2 size, float facing, Color tint, bool groundShadowsManaged)
    {
        _sheet = sheet;
        _clip = clip;
        _size = size;
        _facing = facing;
        _tint = tint;
        _groundShadowsManaged = groundShadowsManaged;
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= _clip.FrameCount * _clip.FrameDuration + 0.18f)
            QueueFree();
        else
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

    private int Frame => Mathf.Min((int)(_age / Mathf.Max(.01f, _clip.FrameDuration)), _clip.FrameCount - 1);
    private float Alpha => 1 - Mathf.Clamp((_age - (_clip.FrameCount * _clip.FrameDuration - .12f)) / .3f, 0, 1);
    public void DrawGroundShadow(CanvasItem canvas, BattleLighting lighting)
    {
        var feet = canvas == this ? Vector2.Zero : Position;
        var rect = new Rect2(new Vector2(-_size.X * _sheet.AnchorX, -_size.Y * _sheet.AnchorY), _size);
        lighting.DrawShadow(canvas, _sheet.Texture, UnitSpriteLoader.GetFrameRect(_sheet, _clip.StartFrame + Frame), rect, feet, _facing, .85f, Alpha);
        lighting.DrawContact(canvas, feet, new Vector2(_size.X * .10f, _size.Y * .025f), Alpha * .8f);
    }
}
