using Godot;

// A short-lived visual only: the defeated unit is removed from combat immediately.
public partial class UnitDeathVisual : Node2D
{
    private UnitSpriteSheet _sheet;
    private SpriteAnimRange _clip;
    private Vector2 _size;
    private float _facing;
    private float _age;

    public void Setup(UnitSpriteSheet sheet, SpriteAnimRange clip, Vector2 size, float facing)
    {
        _sheet = sheet;
        _clip = clip;
        _size = size;
        _facing = facing;
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
        var frame = Mathf.Min((int)(_age / Mathf.Max(0.01f, _clip.FrameDuration)), _clip.FrameCount - 1);
        var fadeAt = _clip.FrameCount * _clip.FrameDuration - 0.12f;
        var alpha = 1f - Mathf.Clamp((_age - fadeAt) / 0.3f, 0f, 1f);
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(_facing < 0 ? -1 : 1, 1));
        DrawTextureRectRegion(_sheet.Texture,
            new Rect2(new Vector2(-_size.X * _sheet.AnchorX, -_size.Y * _sheet.AnchorY), _size),
            UnitSpriteLoader.GetFrameRect(_sheet, _clip.StartFrame + frame), new Color(1, 1, 1, alpha));
    }
}
