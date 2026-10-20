using System;
using System.Collections.Generic;
using Godot;

/// <summary>What a piece of combat text reports; each kind keeps one colour everywhere on the field.</summary>
public enum CombatTextKind
{
    /// <summary>Damage to anyone, troops, enemies or bases: red, or yellow for a critical hit.</summary>
    Damage,
    /// <summary>Healing and repairs: sage.</summary>
    Heal,
    /// <summary>Courage gained: amber.</summary>
    Courage,
    /// <summary>A word such as Dodge or Parry: pale stone.</summary>
    Callout,
}

/// <summary>
/// One number or word over the battlefield, set small in the engraved Roman capitals. It fades in, drifts up and
/// eases to a stop, then fades; a critical hit is yellow and a little larger, settling like a seal pressed into wax. Fills are soft vertical gradients (the gold-title shader) inside a thin ink outline. Hits
/// landing on one target within a moment are added into one tally.
/// </summary>
public partial class BattleFloatText : Node2D
{
    private const int NumberSize = 17;
    private const int CalloutSize = 14;
    private const float SettleSeconds = .18f;
    private const float RiseSeconds = .7f;
    private const float CriticalScale = 1.2f;
    private static readonly Color Ink = new(.11f, .07f, .045f, .92f);
    private static readonly Dictionary<(CombatTextKind, bool), ShaderMaterial> Materials = new();
    private static Shader _shader;

    /// <summary>Screen-size correction: the camera zooms in on the field but text keeps one size on screen.</summary>
    public Func<float> PresentationScale { get; set; }
    public CombatTextKind Kind { get; private set; }
    public bool Critical { get; private set; }
    public float Amount { get; private set; }
    public float Age { get; private set; }
    public string Text { get; private set; } = "";

    private Vector2 _origin;
    private Vector2 _drift;
    private float _lifetime;
    private float _emphasis = 1f;
    private float _bump;
    private bool _still;
    private string _prefix = "";

    /// <summary>A word such as Dodge or Parry.</summary>
    public void Setup(string text, CombatTextKind kind = CombatTextKind.Callout, Vector2? drift = null, bool reducedMotion = false) =>
        Begin(text, kind, false, 0f, drift, reducedMotion);

    /// <summary>A number: damage shows bare, healing and courage carry a plus sign.</summary>
    public void SetupAmount(float amount, CombatTextKind kind, bool critical = false, float emphasis = 0f, Vector2? drift = null, bool reducedMotion = false)
    {
        _prefix = kind is CombatTextKind.Heal or CombatTextKind.Courage ? "+" : "";
        Amount = amount;
        Begin(Format(), kind, critical, emphasis, drift, reducedMotion);
    }

    private void Begin(string text, CombatTextKind kind, bool critical, float emphasis, Vector2? drift, bool reducedMotion)
    {
        _origin = Position;
        Text = text;
        Kind = kind;
        Critical = critical;
        _still = reducedMotion;
        _emphasis = 1f + .1f * Mathf.Clamp(emphasis, 0f, 1f);
        _drift = drift ?? Vector2.Zero;
        _lifetime = BaseLifetime;
        ProcessMode = ProcessModeEnum.Pausable;
        ZIndex = 400;
        Material = MaterialFor(kind, critical);
        Apply();
    }

    // Anything that registers shows at least 1.
    private string Format() => _prefix + Mathf.Max(1, Mathf.RoundToInt(Amount));

    /// <summary>Adds another hit into this tally and bumps it, keeping its place in the rise.</summary>
    public void AddAmount(float amount)
    {
        Amount += amount;
        Text = Format();
        _bump = 1f;
        _lifetime = Mathf.Max(_lifetime, Age + BaseLifetime * .8f);
        Apply();
    }

    /// <summary>Turns a fresh number into a critical hit: yellow and a little larger.</summary>
    public void MakeCritical()
    {
        if (Critical) return;
        Critical = true;
        _lifetime = Mathf.Max(_lifetime, Age + BaseLifetime);
        Material = MaterialFor(Kind, true);
        Apply();
    }

    /// <summary>Hurries an old label off when the field is crowded with text.</summary>
    public void Retire() => _lifetime = Mathf.Min(_lifetime, Age + .14f);

    public bool CanMerge(CombatTextKind kind) => !Critical && Kind == kind && Kind != CombatTextKind.Callout && Age < .3f && _lifetime - Age > .2f;

    private float BaseLifetime => Kind switch
    {
        CombatTextKind.Callout or CombatTextKind.Heal or CombatTextKind.Courage => .85f,
        _ => Critical ? .95f : .8f,
    };

    public override void _Process(double delta)
    {
        Age += (float)delta;
        if (Age >= _lifetime)
        {
            QueueFree();
            return;
        }
        _bump = Mathf.Max(0f, _bump - (float)delta / .14f);
        Apply();
    }

    private void Apply()
    {
        var presentation = PresentationScale?.Invoke() ?? 1f;
        var fadeOut = Mathf.Clamp((Age - _lifetime * .6f) / (_lifetime * .4f), 0f, 1f);
        fadeOut = fadeOut * fadeOut * (3f - 2f * fadeOut);
        var fadeIn = _still ? 1f : Mathf.Clamp(Age / .08f, 0f, 1f);
        var rise = EaseOutCubic(Mathf.Min(1f, Age / RiseSeconds));
        var risePixels = (Kind == CombatTextKind.Callout ? 14f : Critical ? 22f : 20f) * (_still ? .6f : 1f);
        var offset = new Vector2(_drift.X * rise, -risePixels * rise);
        var settle = 1f;
        if (!_still)
        {
            // Ordinary text grows the last little way into place; a critical hit presses down into it.
            var p = EaseOutCubic(Mathf.Min(1f, Age / (Critical ? SettleSeconds * 1.3f : SettleSeconds)));
            settle = Mathf.Lerp(Critical ? 1.3f : .88f, 1f, p) + .1f * _bump * _bump;
        }
        var size = (Critical ? CriticalScale : 1f) * _emphasis * settle * Mathf.Lerp(1f, .94f, fadeOut);
        Position = _origin + offset * presentation;
        Scale = Vector2.One * presentation * size;
        Modulate = new Color(1f, 1f, 1f, fadeIn * (1f - fadeOut));
        QueueRedraw();
    }

    public override void _Draw()
    {
        var font = RoyalFonts.Display(Kind == CombatTextKind.Callout ? 700 : 900);
        var fontSize = Kind == CombatTextKind.Callout ? CalloutSize : NumberSize;
        var width = font.GetStringSize(Text, HorizontalAlignment.Left, -1, fontSize).X;
        // The baseline sits so the capitals and figures centre on the origin.
        var at = new Vector2(-width * .5f, fontSize * .35f);
        DrawStringOutline(font, at + new Vector2(0f, 1.5f), Text, HorizontalAlignment.Left, -1, fontSize, 6, new Color(0f, 0f, 0f, .32f));
        DrawStringOutline(font, at, Text, HorizontalAlignment.Left, -1, fontSize, 4, Ink);
        // Pure white marks the fill for the gradient shader.
        DrawString(font, at, Text, HorizontalAlignment.Left, -1, fontSize, Colors.White);
    }

    private static ShaderMaterial MaterialFor(CombatTextKind kind, bool critical)
    {
        if (Materials.TryGetValue((kind, critical), out var cached)) return cached;
        _shader ??= ResourceLoader.Load<Shader>("res://assets/shaders/royal_gold_text.gdshader");
        var (top, mid, bottom) = (kind, critical) switch
        {
            // Bright and close in tone from top to bottom, so the figures read at a glance at this size.
            (CombatTextKind.Damage, true) => ("fff8b8", "ffe04a", "f7b81c"),
            (CombatTextKind.Damage, false) => ("ff8f80", "ff5442", "e8301f"),
            (CombatTextKind.Heal, _) => ("e8f3d2", "bcd79e", "8db368"),
            (CombatTextKind.Courage, _) => ("f8e3b0", "e6bd78", "cf9440"),
            _ => ("f5f0e6", "e2d9c9", "c8bda9"),
        };
        var fontSize = kind == CombatTextKind.Callout ? CalloutSize : NumberSize;
        var material = new ShaderMaterial { Shader = _shader };
        material.SetShaderParameter("top_color", new Color(top));
        material.SetShaderParameter("mid_color", new Color(mid));
        material.SetShaderParameter("bottom_color", new Color(bottom));
        material.SetShaderParameter("top_y", -fontSize * .36f);
        material.SetShaderParameter("bottom_y", fontSize * .36f);
        return Materials[(kind, critical)] = material;
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

}
