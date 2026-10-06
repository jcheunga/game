using System;
using Godot;

public partial class BattleFloatText : Label
{
    public Func<float> PresentationScale { get; set; }
    private Vector2 _velocity = new(0f, -42f);
    private float _lifetime = 0.5f;
    private float _elapsed;
    private Color _baseColor = Colors.White;

    public void Setup(string text, Color color, float lifetime = 0.5f, Vector2? velocity = null)
    {
        Text = text;
        _baseColor = color;
        _lifetime = Mathf.Max(0.1f, lifetime);
        _velocity = velocity ?? new Vector2(0f, -42f);

        HorizontalAlignment = HorizontalAlignment.Center;
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 400;

        AddThemeColorOverride("font_color", color);
        AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        // The concept's bold book-serif numerals with a dark outline.
        AddThemeFontOverride("font", RoyalFonts.Body(700));
        AddThemeFontSizeOverride("font_size", 21);
        AddThemeConstantOverride("outline_size", 5);
        Scale = Vector2.One * (PresentationScale?.Invoke() ?? 1f);
    }

    public override void _Process(double delta)
    {
        var deltaF = (float)delta;
        _elapsed += deltaF;
        if (_elapsed >= _lifetime)
        {
            QueueFree();
            return;
        }

        var scale = PresentationScale?.Invoke() ?? 1f;
        Position += _velocity * deltaF * scale;
        _velocity *= 0.96f;

        var alpha = 1f - (_elapsed / _lifetime);
        Modulate = new Color(_baseColor, alpha);
        Scale = Vector2.One * scale * (1f + (0.08f * alpha));
    }
}
