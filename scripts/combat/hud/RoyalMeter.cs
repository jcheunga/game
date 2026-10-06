using Godot;

/// <summary>
/// A clean-steel concept meter: the painted bar plate behind, a sliced enamel fill that eases to its
/// value inside the plate's track, and the value right-aligned in the plate's tail.
/// </summary>
public partial class RoyalMeter : BattleHudBar
{
    public string Plate = "hud-hull";
    public string FillKit = "hud-fill-hull";
    public string IconKit = "";
    public Rect2 IconRect;
    /// <summary>Track interior and value baseline, local to the meter (its rect is the whole plate).</summary>
    public Rect2 Track;
    public float ValueRight, ValueBaseline;
    public int ValueSize = 20;
    private SliceStyle _plate, _fill;
    private float _shown = -1;

    public override void _Process(double delta)
    {
        var target = Mathf.Clamp(GetTargetRatio(), 0, 1);
        if (_shown < 0) _shown = target;
        _shown = Mathf.MoveToward(_shown, target, (float)delta * 1.6f);
        QueueRedraw();
    }

    private float GetTargetRatio() => TargetRatio;

    public override void _Draw()
    {
        _plate ??= RoyalKit.Slice(Plate, 30, 8, 30, 8);
        _fill ??= RoyalKit.Slice(FillKit, 6, 4, 6, 4);
        DrawStyleBox(_plate, new Rect2(Vector2.Zero, Size));
        if (IconKit.Length > 0 && RoyalKit.Texture(IconKit) is { } icon) DrawTextureRect(icon, IconRect, false);
        var width = Track.Size.X * _shown;
        if (width > 2) DrawStyleBox(_fill, new Rect2(Track.Position, new Vector2(Mathf.Max(12, width), Track.Size.Y)));
        var _text = (ValueText ?? "").Replace("/", " / ");
        if (_text.Length == 0) return;
        var font = RoyalFonts.Body(500);
        var textWidth = font.GetStringSize(_text, HorizontalAlignment.Left, -1, ValueSize).X;
        var at = new Vector2(ValueRight - textWidth, ValueBaseline);
        DrawString(font, at + new Vector2(0, 1.5f), _text, HorizontalAlignment.Left, -1, ValueSize, new Color(0, 0, 0, .6f));
        DrawString(font, at, _text, HorizontalAlignment.Left, -1, ValueSize, new Color("f4f3f1"));
    }
}
