using Godot;

/// <summary>
/// One line of concept typography drawn on a measured baseline. Labels in the concepts are placed
/// by their lettering, not by font boxes, so this draws the cap band centred in its rect (or on an
/// explicit baseline) and shrinks long values to fit instead of overflowing a frame.
/// </summary>
public partial class RoyalLabel : Control
{
    private static Shader _goldShader;
    private string _text = "";
    private Font _font;
    private int _fontSize = 20;
    private bool _gold;

    public string Text { get => _text; set { if (_text == value) return; _text = value ?? ""; QueueRedraw(); UpdateMinimumSize(); } }
    public Font Font { get => _font ??= RoyalFonts.Body(); set { _font = value; QueueRedraw(); UpdateMinimumSize(); } }
    public int FontSize { get => _fontSize; set { _fontSize = value; QueueRedraw(); UpdateMinimumSize(); } }
    public Color Ink = new("efe4cc");
    public HorizontalAlignment Align = HorizontalAlignment.Left;
    /// <summary>Local y of the baseline. NaN centres the cap band in the rect.</summary>
    public float Baseline = float.NaN;
    /// <summary>Cap height as a fraction of the font size (Cinzel .70, Crimson Pro .58).</summary>
    public float CapRatio = .58f;
    public int OutlineSize;
    public Color OutlineInk = new("1a0f07");
    public Vector2 ShadowOffset;
    public Color ShadowInk = new(0, 0, 0, .55f);
    public bool ShrinkToFit = true;
    public int MinimumFontSize = 10;
    public float Tracking;

    public bool Gold
    {
        get => _gold;
        set
        {
            _gold = value;
            if (value)
            {
                _goldShader ??= ResourceLoader.Load<Shader>("res://assets/shaders/royal_gold_text.gdshader");
                Material = new ShaderMaterial { Shader = _goldShader };
            }
            else Material = null;
            QueueRedraw();
        }
    }

    public RoyalLabel() { MouseFilter = MouseFilterEnum.Ignore; }

    public override void _Ready() => Resized += QueueRedraw;

    public int FittedSize()
    {
        var size = _fontSize;
        if (!ShrinkToFit || Size.X <= 0) return size;
        while (size > MinimumFontSize && TextWidth(size) > Size.X) size--;
        return size;
    }

    public float TextWidth(int size) => Font.GetStringSize(_text, HorizontalAlignment.Left, -1, size).X + Tracking * Mathf.Max(0, _text.Length - 1);

    public override Vector2 _GetMinimumSize() => new(ShrinkToFit ? 0 : TextWidth(_fontSize), _fontSize * CapRatio);

    public override void _Draw()
    {
        if (_text.Length == 0) return;
        var size = FittedSize();
        var width = TextWidth(size);
        var x = Align switch { HorizontalAlignment.Center => (Size.X - width) / 2, HorizontalAlignment.Right => Size.X - width, _ => 0f };
        var cap = size * CapRatio;
        var baseline = float.IsNaN(Baseline) ? Size.Y / 2 + cap / 2 : Baseline;
        if (Material is ShaderMaterial gold)
        {
            gold.SetShaderParameter("top_y", baseline - cap);
            gold.SetShaderParameter("bottom_y", baseline);
        }
        var origin = new Vector2(Mathf.Round(x), Mathf.Round(baseline));
        if (ShadowOffset != Vector2.Zero) DrawRun(origin + ShadowOffset, size, ShadowInk, OutlineSize);
        if (OutlineSize > 0) DrawRun(origin, size, OutlineInk, OutlineSize);
        DrawRun(origin, size, _gold ? Colors.White : Ink, 0);
    }

    private void DrawRun(Vector2 origin, int size, Color ink, int outline)
    {
        if (Tracking == 0)
        {
            if (outline > 0) DrawStringOutline(Font, origin, _text, HorizontalAlignment.Left, -1, size, outline, ink);
            else DrawString(Font, origin, _text, HorizontalAlignment.Left, -1, size, ink);
            return;
        }
        var position = origin;
        foreach (var letter in _text)
        {
            var glyph = letter.ToString();
            if (outline > 0) DrawStringOutline(Font, position, glyph, HorizontalAlignment.Left, -1, size, outline, ink);
            else DrawString(Font, position, glyph, HorizontalAlignment.Left, -1, size, ink);
            position.X += Font.GetStringSize(glyph, HorizontalAlignment.Left, -1, size).X + Tracking;
        }
    }
}
