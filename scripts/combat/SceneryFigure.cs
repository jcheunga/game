using Godot;

/// <summary>A unit's sprite played as scenery (the archer on the caravan roof, sentries on a stronghold's walls): its
/// own sheet and clips, sized as the unit stands on the field, but no body, no combat and no place in the battle.</summary>
public sealed class SceneryFigure
{
    private readonly UnitSpriteSheet _sheet;
    private UnitAnimState _state = UnitAnimState.Idle;
    private SpriteAnimRange _clip;
    private float _time;

    /// <summary>The figure's drawn frame size in world units.</summary>
    public Vector2 Size { get; }
    public UnitAnimState State => _state;
    /// <summary>How high the head stands above the feet, in world units.</summary>
    public float HeadHeight => Size.Y * _sheet.HealthBarY;
    /// <summary>True once a clip that does not loop has played its last frame.</summary>
    public bool Finished => _clip != null && !_clip.Loop && _time >= _clip.FrameCount * _clip.FrameDuration;

    private SceneryFigure(UnitSpriteSheet sheet, Vector2 size)
    {
        _sheet = sheet;
        Size = size;
        Play(UnitAnimState.Idle);
    }

    public static SceneryFigure Load(string unitId, float scale = 1f)
    {
        var definition = GameData.GetUnit(unitId);
        if (definition == null) return null;
        var stats = new UnitStats(definition);
        var sheet = UnitSpriteLoader.TryLoad(stats.VisualClass, unitId);
        if (sheet == null || sheet.FrameWidth <= 0) return null;
        // As Unit draws itself: two radii across, by the unit's visual scale and the sheet's own draw scale.
        var drawScale = 14f * stats.VisualScale * 2f / sheet.FrameWidth * stats.VisualScale * sheet.DrawScale * scale;
        return new SceneryFigure(sheet, new Vector2(sheet.FrameWidth, sheet.FrameHeight) * drawScale);
    }

    /// <summary>Starts a clip (Idle when the sheet lacks it), from <paramref name="fromFrame"/>.</summary>
    public void Play(UnitAnimState state, int fromFrame = 0)
    {
        if (!_sheet.Animations.TryGetValue(state, out var clip) && !_sheet.Animations.TryGetValue(UnitAnimState.Idle, out clip)) return;
        _state = state;
        _clip = clip;
        _time = Mathf.Clamp(fromFrame, 0, clip.FrameCount - 1) * clip.FrameDuration;
    }

    public int ContactFrame => _sheet.Animations.TryGetValue(UnitAnimState.Attack, out var attack) ? attack.ContactFrame : 0;

    public void Tick(float delta)
    {
        _time += delta;
        // A one-shot clip settles back into the idle loop.
        if (Finished) Play(UnitAnimState.Idle);
    }

    /// <summary>Draws the figure standing at <paramref name="feet"/> (in the canvas's current transform).</summary>
    public void Draw(CanvasItem canvas, Vector2 feet, float facing, Color modulate)
    {
        if (_clip == null) return;
        var frame = (int)(_time / Mathf.Max(.01f, _clip.FrameDuration));
        frame = _clip.Loop ? frame % _clip.FrameCount : Mathf.Min(frame, _clip.FrameCount - 1);
        var piece = UnitSpriteLoader.Frame(_sheet, _clip.StartFrame + frame);
        var rect = new Rect2(new Vector2(-Size.X * _sheet.AnchorX, -Size.Y * _sheet.AnchorY), Size);
        var placed = UnitSpriteLoader.Place(_sheet, piece, rect);
        if (facing < 0) placed = new Rect2(-placed.Position.X - placed.Size.X, placed.Position.Y, placed.Size.X, placed.Size.Y);
        // A negative width mirrors the frame about its own rectangle.
        var destination = facing < 0 ? new Rect2(placed.Position.X + placed.Size.X, placed.Position.Y, -placed.Size.X, placed.Size.Y) : placed;
        canvas.DrawTextureRectRegion(piece.Texture, new Rect2(destination.Position + feet, destination.Size), piece.Source, modulate);
    }
}
