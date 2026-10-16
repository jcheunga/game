using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// How every button answers a touch. Hovering changes nothing. Holding a button presses it in: it shrinks a few
/// pixels, drops and darkens, contents and all, for as long as the finger stays on it. Letting go over it springs
/// it back up a little past rest as its action runs (on release, as Godot buttons act); sliding off or starting a
/// scroll lets it rise without acting. <see cref="Install"/> gives every <see cref="BaseButton"/> in the tree this
/// feel, so no screen has to opt in.
///
/// A button that draws its own surface and contents moves as a whole. Only its drawn transform changes, never its
/// layout or hit area, so a finger near its edge keeps the press. A button over art it does not draw (a
/// <see cref="RoyalButton"/> over a painted plate, or a hotspot over the content beside it, marked with
/// <see cref="PaintBelow"/>) presses that art in place: <see cref="PressedArt"/> redraws what lies under it sunk
/// into its socket. Controls that read touches themselves (the battle card dock, map landmarks) drive the same
/// press with <see cref="Hold"/>. Keyboard focus keeps its outline, which Godot shows only for keyboard navigation.
/// </summary>
public static class ButtonFeel
{
    /// <summary>How far a fully held button drops, in canvas pixels.</summary>
    public const float Depth = 2;
    /// <summary>Meta for a button that shows its press some other way: it still reports <see cref="Amount"/>.</summary>
    public const string Off = "button_feel_off";
    /// <summary>Meta (the corner radius) on a button whose art is painted beneath it.</summary>
    public const string Painted = "button_feel_painted";

    private const string Bound = "button_feel_bound";
    private const float PressSeconds = .06f, RiseSeconds = .1f, PopSeconds = .07f, SettleSeconds = .16f;
    // How far past rest a released button springs (as a fraction of a full press), and how much it brightens there.
    private const float Overshoot = .45f, Darken = .1f, Brighten = .08f;

    private static readonly Dictionary<ulong, Press> Presses = new();
    private static readonly List<ulong> Finished = new();
    private static ulong _lastTick;
    private static bool _installed;

    /// <summary>Gives every button that enters the tree the press feel (called once at startup).</summary>
    public static void Install(SceneTree tree)
    {
        if (_installed) return;
        _installed = true;
        tree.NodeAdded += node => { if (node is BaseButton button) Bind(button); };
        // Real time, and on while the tree is paused, so pause menus press too.
        tree.ProcessFrame += Step;
        foreach (var button in Walk(tree.Root).OfType<BaseButton>()) Bind(button);
    }

    /// <summary>Gives a button the shared look: hover reuses its resting style and colours; a held press shows <paramref name="held"/>.</summary>
    public static void Apply(Button button, StyleBox normal, StyleBox held)
    {
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", normal);
        button.AddThemeStyleboxOverride("pressed", held);
        button.AddThemeStyleboxOverride("hover_pressed", held);
        MatchHoverInk(button);
    }

    /// <summary>Hover text and icon colours repeat the resting ones, so nothing lights up under the pointer.</summary>
    public static void MatchHoverInk(Control button)
    {
        button.AddThemeColorOverride("font_hover_color", button.GetThemeColor("font_color"));
        button.AddThemeColorOverride("font_hover_pressed_color", button.GetThemeColor("font_pressed_color"));
        button.AddThemeColorOverride("icon_hover_color", button.GetThemeColor("icon_normal_color"));
        button.AddThemeColorOverride("icon_hover_pressed_color", button.GetThemeColor("icon_pressed_color"));
    }

    /// <summary>Marks a button whose art is painted beneath it (null corner clears the mark).</summary>
    public static void PaintBelow(BaseButton button, float? corner)
    {
        if (corner is { } radius) button.SetMeta(Painted, radius);
        else if (button.HasMeta(Painted)) button.RemoveMeta(Painted);
    }

    /// <summary>
    /// Holds or lets go of a button whose touches another control reads. <paramref name="acted"/> on letting go
    /// means the touch landed, so the button springs up as a release that ran its action.
    /// </summary>
    public static void Hold(BaseButton button, bool held, bool acted = false)
    {
        if (!GodotObject.IsInstanceValid(button)) return;
        Bind(button);
        var press = Find(button, held);
        if (press == null) return;
        press.Forced = held;
        if (!held && acted && press.Held) press.Acted = true;
        press.SetHeld(held || Pressing(button));
    }

    /// <summary>How far in a button is pressed: 1 fully held, 0 at rest, below 0 while it springs up past rest.</summary>
    public static float Amount(BaseButton button) =>
        GodotObject.IsInstanceValid(button) && Presses.TryGetValue(button.GetInstanceId(), out var press) ? press.Amount : 0;

    /// <summary>The brightness a press gives: darker while held, a touch brighter as it springs up.</summary>
    public static float Lightness(float amount) => amount > 0 ? 1 - Darken * amount : 1 - Brighten * amount;

    /// <summary>The fraction a held button shrinks: a few pixels at any size.</summary>
    public static float Shrink(Vector2 size) => Mathf.Min(.06f, 10f / Mathf.Max(1, Mathf.Max(size.X, size.Y)));

    private static void Bind(BaseButton button)
    {
        if (button.HasMeta(Bound)) return;
        button.SetMeta(Bound, true);
        button.Draw += () => { var press = Find(button, Pressing(button)); press?.SetHeld(press.Forced || Pressing(button)); };
        // Emitted on the release that acts, before the button redraws as risen.
        button.Pressed += () => { if (Presses.TryGetValue(button.GetInstanceId(), out var press) && press.Held) press.Acted = true; };
        button.VisibilityChanged += () => { if (!button.IsVisibleInTree()) Settle(button); };
        button.TreeExiting += () => Settle(button);
    }

    private static Press Find(BaseButton button, bool create)
    {
        var id = button.GetInstanceId();
        if (Presses.TryGetValue(id, out var press)) return press;
        if (!create) return null;
        if (Presses.Count == 0) _lastTick = Time.GetTicksUsec();
        return Presses[id] = new Press(button);
    }

    /// <summary>Whether the button is held with the pointer still over it.</summary>
    private static bool Pressing(BaseButton button)
    {
        var mode = button.GetDrawMode();
        // A toggled-on button previews its next state while held: it draws as normal.
        return button.ToggleMode && button.ButtonPressed ? mode == BaseButton.DrawMode.Normal
            : mode is BaseButton.DrawMode.Pressed or BaseButton.DrawMode.HoverPressed;
    }

    /// <summary>Drops a press at once: the button left the tree or was hidden.</summary>
    private static void Settle(BaseButton button)
    {
        if (!Presses.Remove(button.GetInstanceId(), out var press)) return;
        press.Amount = 0;
        Show(press);
    }

    private static void Step()
    {
        if (Presses.Count == 0) return;
        var now = Time.GetTicksUsec();
        var delta = Mathf.Min(.1f, (now - _lastTick) / 1e6f);
        _lastTick = now;
        foreach (var (id, press) in Presses)
        {
            if (!GodotObject.IsInstanceValid(press.Button) || !press.Button.IsInsideTree()) { Finished.Add(id); continue; }
            // Catches a press Godot dropped without a redraw (a popup taking the pointer, a scroll starting).
            if (!press.Forced) press.SetHeld(Pressing(press.Button));
            var moving = press.Advance(delta);
            Show(press);
            if (!moving) Finished.Add(id);
        }
        foreach (var id in Finished) Presses.Remove(id);
        Finished.Clear();
    }

    private static void Show(Press press)
    {
        var button = press.Button;
        if (!GodotObject.IsInstanceValid(button) || button.HasMeta(Off)) return;
        if (button.HasMeta(Painted)) { ShowPainted(button, press.Amount); return; }
        var amount = press.Amount;
        var transform = button.GetTransform();
        var modulate = button.Modulate;
        if (Mathf.IsZeroApprox(amount))
        {
            // Back to exactly what Godot draws at rest, pixel snapping included.
            if (button.GetViewport()?.GuiSnapControlsToPixels != false && Mathf.IsZeroApprox(Mathf.Sin(button.Rotation * 4)))
                transform.Origin = (transform.Origin + new Vector2(.5f, .5f)).Floor();
        }
        else
        {
            var scale = 1 - amount * Shrink(button.Size);
            var middle = button.Size / 2;
            transform *= new Transform2D(0, new Vector2(scale, scale), 0, middle * (1 - scale) + new Vector2(0, Depth * amount));
            var light = Lightness(amount);
            modulate = new Color(modulate.R * light, modulate.G * light, modulate.B * light, modulate.A);
        }
        RenderingServer.CanvasItemSetTransform(button.GetCanvasItem(), transform);
        RenderingServer.CanvasItemSetModulate(button.GetCanvasItem(), modulate);
    }

    private static void ShowPainted(BaseButton button, float amount)
    {
        var art = button.GetChildren(true).OfType<PressedArt>().FirstOrDefault();
        if (art == null)
        {
            if (amount <= 0) return;
            // Internal and last, so it draws over the button's caption and glyph and never shows up as content.
            art = new PressedArt();
            button.AddChild(art, false, Node.InternalMode.Back);
        }
        art.Show(amount, button.Size, Shrink(button.Size), button.GetMeta(Painted).AsSingle());
    }

    private static IEnumerable<Node> Walk(Node node)
    {
        yield return node;
        foreach (var child in node.GetChildren())
        foreach (var descendant in Walk(child)) yield return descendant;
    }

    private sealed class Press
    {
        public readonly BaseButton Button;
        public bool Held, Forced, Acted;
        public float Amount;
        private float _from, _to, _elapsed, _seconds;
        private bool _popping, _settling;

        public Press(BaseButton button) => Button = button;

        public void SetHeld(bool held)
        {
            if (held == Held) return;
            Held = held;
            if (held) { Acted = false; Go(1, PressSeconds); }
            else if (Acted) { Acted = false; Go(-Overshoot, PopSeconds); _popping = true; }
            else Go(0, RiseSeconds);
        }

        private void Go(float to, float seconds)
        {
            _from = Amount; _to = to; _elapsed = 0; _seconds = seconds; _popping = _settling = false;
        }

        /// <summary>Moves the press along; false once it rests and nothing holds it.</summary>
        public bool Advance(float delta)
        {
            _elapsed += delta;
            var t = Mathf.Clamp(_elapsed / _seconds, 0, 1);
            // Quick to go in and to spring up; the spring then eases back to rest.
            var eased = _settling ? t * t * (3 - 2 * t) : 1 - (1 - t) * (1 - t) * (1 - t);
            Amount = Mathf.Lerp(_from, _to, eased);
            if (t < 1) return true;
            if (_popping) { Go(0, SettleSeconds); _settling = true; return true; }
            return Held;
        }
    }
}

/// <summary>
/// Presses in art painted beneath a button: it redraws what lies under the button with its face shrunk and dropped
/// into a shadowed socket (<c>button_press.gdshader</c>). It copies the screen under the button just before it draws,
/// so it sees the button's own caption and glyph, and two buttons held at once each see their own art.
/// </summary>
public partial class PressedArt : BackBufferCopy
{
    private static Shader _shader;
    private readonly Control _face;
    private readonly ShaderMaterial _material;

    public PressedArt()
    {
        Name = "PressedArt";
        Visible = false;
        // The whole screen: a rect copy lands offset from the button once it sits inside transformed controls.
        CopyMode = CopyModeEnum.Viewport;
        _shader ??= ResourceLoader.Load<Shader>("res://assets/shaders/button_press.gdshader");
        _face = new Control { Name = "Face", MouseFilter = Control.MouseFilterEnum.Ignore, Material = _material = new ShaderMaterial { Shader = _shader } };
        _face.Draw += () => _face.DrawRect(new Rect2(Vector2.Zero, _face.Size), Colors.White);
        AddChild(_face);
    }

    public void Show(float amount, Vector2 size, float shrink, float corner)
    {
        amount = Mathf.Max(0, amount);
        Visible = amount > 0;
        if (!Visible) return;
        _face.Size = size;
        _material.SetShaderParameter("amount", amount);
        _material.SetShaderParameter("size", size);
        _material.SetShaderParameter("shrink", shrink);
        _material.SetShaderParameter("depth", ButtonFeel.Depth);
        _material.SetShaderParameter("corner", corner);
    }
}
