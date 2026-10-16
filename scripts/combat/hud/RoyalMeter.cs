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
    private float _shown = -1, _flare;
    private TextureRect _flash;

    /// <summary>Flares the icon and fill as something lands in the meter (a collected courage pack).</summary>
    public void Flare()
    {
        _flare = 1f;
        if (_flash != null || IconRect.Size == Vector2.Zero) return;
        // Added light over the icon reads as a hot flash; drawn colour alone dulls against the dark plate.
        _flash = new TextureRect { Texture = SpellVfx.Glow, Material = SpellVfx.Additive, MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
        var glow = IconRect.Grow(IconRect.Size.X * .45f);
        _flash.Position = glow.Position; _flash.Size = glow.Size;
        AddChild(_flash);
    }

    public override void _Process(double delta)
    {
        var target = Mathf.Clamp(GetTargetRatio(), 0, 1);
        if (_shown < 0) _shown = target;
        _shown = Mathf.MoveToward(_shown, target, (float)delta * 1.6f);
        _flare = Mathf.Max(0f, _flare - (float)delta * 2.2f);
        if (_flash != null) _flash.Modulate = new Color(1f, .72f, .34f, .85f * _flare * _flare);
        QueueRedraw();
    }

    private float GetTargetRatio() => TargetRatio;

    public override void _Draw()
    {
        _plate ??= RoyalKit.Slice(Plate, 30, 8, 30, 8);
        _fill ??= RoyalKit.Slice(FillKit, 6, 4, 6, 4);
        DrawStyleBox(_plate, new Rect2(Vector2.Zero, Size));
        var width = Track.Size.X * _shown;
        if (width > 2) DrawStyleBox(_fill, new Rect2(Track.Position, new Vector2(Mathf.Max(12, width), Track.Size.Y)));
        if (_flare > 0f && width > 2) DrawRect(new Rect2(Track.Position, new Vector2(Mathf.Max(12, width), Track.Size.Y)), new Color(1f, .86f, .55f, .28f * _flare));
        if (IconKit.Length > 0 && RoyalKit.Texture(IconKit) is { } icon)
        {
            var rect = IconRect;
            if (_flare > 0f)
            {
                DrawTextureRect(SpellVfx.Glow, rect.Grow(rect.Size.X * .6f), false, new Color(1f, .68f, .28f, .9f * _flare));
                if (!SpellVfx.Reduced) rect = rect.Grow(rect.Size.X * .16f * Mathf.Sin(_flare * Mathf.Pi));
            }
            DrawTextureRect(icon, rect, false);
        }
        var _text = (ValueText ?? "").Replace("/", " / ");
        if (_text.Length == 0) return;
        var font = RoyalFonts.Body(500);
        var textWidth = font.GetStringSize(_text, HorizontalAlignment.Left, -1, ValueSize).X;
        var at = new Vector2(ValueRight - textWidth, ValueBaseline);
        DrawString(font, at + new Vector2(0, 1.5f), _text, HorizontalAlignment.Left, -1, ValueSize, new Color(0, 0, 0, .6f));
        DrawString(font, at, _text, HorizontalAlignment.Left, -1, ValueSize, new Color("f4f3f1"));
    }
}
