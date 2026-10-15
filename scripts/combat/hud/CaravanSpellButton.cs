using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// The caravan spell's slot on the right of the card dock. It stays tucked behind the dock until the mana bar
/// is full, then slides out with a burst of light and keeps a slow violet pulse, a heartbeat ring and sparks
/// circling the card while it is ready. Presentation only: the owning <see cref="Card"/> button carries input
/// and BattleController decides when the slot is open and whether it can be cast.
/// </summary>
public partial class CaravanSpellButton : Control
{
    private struct Mote
    {
        public Vector2 Position, Velocity;
        public float Age, Life, Size;
        public Color Color;
    }

    private const float OpenSeconds = .42f, CloseSeconds = .3f, Heartbeat = 1.5f;
    private static readonly Color Violet = new(.74f, .5f, 1f), Ice = new(.62f, .82f, 1f), Hot = new(1f, .96f, 1f);
    private readonly Panel _backing;
    private readonly Control _aura, _sparkle;
    private readonly List<Mote> _motes = new();
    private readonly RandomNumberGenerator _rng = new();
    private Rect2 _rest;
    private float _openAmount, _clock, _readyAmount, _flash;
    private bool _open, _ready, _burstDone;

    public Button Card { get; }
    public BattleActionCard Art { get; }
    /// <summary>True from the moment the slot starts to open until it has fully closed again.</summary>
    public bool Shown => _open || _openAmount > .001f;
    public bool IsOpen => _open;
    /// <summary>Runs once each time the slot finishes sliding out (the ready chime).</summary>
    public Action Opened;

    public CaravanSpellButton(Button card, BattleActionCard art)
    {
        Name = "CaravanSpell";
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        _rng.Randomize();
        Card = card; Art = art;
        // The aura sits behind the slot; the sparkle layer over it adds light.
        _aura = new Control { Name = "Aura", MouseFilter = MouseFilterEnum.Ignore };
        _aura.Draw += DrawAura;
        AddChild(_aura);
        _backing = new Panel { Name = "Backing", MouseFilter = MouseFilterEnum.Ignore };
        var dock = RoyalKit.Slice("hud-dock", 30, 22, 30, 22);
        _backing.AddThemeStyleboxOverride("panel", dock);
        AddChild(_backing);
        AddChild(card);
        _sparkle = new Control { Name = "Sparkle", MouseFilter = MouseFilterEnum.Ignore, Material = SpellVfx.Additive };
        _sparkle.Draw += DrawSparkle;
        AddChild(_sparkle);
        Art.CostPlate.Visible = false;
        Art.FullArt = true;
    }

    /// <summary>Where the open slot rests, in the HUD's coordinates.</summary>
    public void Place(Rect2 rest)
    {
        _rest = rest;
        Size = rest.Size;
        PivotOffset = new Vector2(0, rest.Size.Y);
        _backing.Position = Vector2.Zero; _backing.Size = rest.Size;
        Card.Position = new Vector2(9, 9); Card.Size = rest.Size - new Vector2(18, 20);
        Card.CustomMinimumSize = Card.Size;
        _aura.Position = _sparkle.Position = Vector2.Zero;
        _aura.Size = _sparkle.Size = rest.Size;
        Apply();
    }

    public Rect2 RestRect => _rest;

    public void SetState(bool open, bool ready)
    {
        if (open && !_open) { _burstDone = false; }
        if (!open && _open) _flash = 1f;
        _open = open;
        _ready = ready && open;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _clock += dt;
        var target = _open ? 1f : 0f;
        _openAmount = Mathf.MoveToward(_openAmount, target, dt / (_open ? OpenSeconds : CloseSeconds));
        _readyAmount = Mathf.MoveToward(_readyAmount, _ready ? 1f : 0f, dt * 3f);
        _flash = Mathf.MoveToward(_flash, 0f, dt * 2.6f);
        if (_open && !_burstDone && _openAmount >= .82f)
        {
            _burstDone = true;
            _flash = 1f;
            Burst();
            Opened?.Invoke();
        }
        if (_ready && !SpellVfx.Reduced && _rng.Randf() < dt * 9f)
        {
            // Motes rise off the card's edge while it waits.
            var edge = new Vector2(_rng.RandfRange(12f, Size.X - 12f), _rng.RandfRange(Size.Y * .2f, Size.Y - 14f));
            AddMote(edge, new Vector2(_rng.RandfRange(-8f, 8f), _rng.RandfRange(-46f, -22f)), _rng.RandfRange(.7f, 1.3f), _rng.RandfRange(3f, 6f),
                _rng.Randf() < .5f ? Violet : Ice);
        }
        for (var i = _motes.Count - 1; i >= 0; i--)
        {
            var mote = _motes[i];
            mote.Age += dt;
            if (mote.Age >= mote.Life) { _motes.RemoveAt(i); continue; }
            mote.Velocity *= 1f - Mathf.Min(1f, 1.8f * dt);
            mote.Position += mote.Velocity * dt;
            _motes[i] = mote;
        }
        Apply();
        _aura.QueueRedraw();
        _sparkle.QueueRedraw();
    }

    private void Apply()
    {
        Visible = Shown || _motes.Count > 0 || _flash > .01f;
        // A springy slide out from behind the dock's right edge, and a quick fade back in when spent.
        var t = _openAmount;
        var slide = _open ? BackOut(t) : t * t;
        Position = _rest.Position + new Vector2(-_rest.Size.X * .7f * (1f - slide), 0);
        var pop = _open ? .72f + .28f * BackOut(t) : .85f + .15f * t;
        Scale = Vector2.One * pop;
        Modulate = new Color(1, 1, 1, Mathf.Clamp(t * 2.2f, 0f, 1f));
        Card.Disabled = !_ready;
        Card.MouseFilter = _open ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
    }

    private static float BackOut(float t)
    {
        const float s = 1.6f;
        t -= 1f;
        return t * t * ((s + 1f) * t + s) + 1f;
    }

    private void Burst()
    {
        if (SpellVfx.Reduced) return;
        var centre = Size / 2;
        for (var i = 0; i < 22; i++)
        {
            var angle = _rng.RandfRange(0f, Mathf.Tau);
            AddMote(centre + Vector2.FromAngle(angle) * Size.X * .3f, Vector2.FromAngle(angle) * _rng.RandfRange(90f, 230f), _rng.RandfRange(.45f, .9f),
                _rng.RandfRange(4f, 9f), i % 3 == 0 ? Hot : i % 2 == 0 ? Violet : Ice);
        }
    }

    private void AddMote(Vector2 at, Vector2 velocity, float life, float size, Color color) =>
        _motes.Add(new Mote { Position = at, Velocity = velocity, Life = life, Size = size, Color = color });

    private readonly StyleBoxFlat _halo = new() { BgColor = new Color(.3f, .16f, .7f, 0f), DrawCenter = false, AntiAliasing = true },
        _pulse = new() { BgColor = Colors.Transparent, DrawCenter = false, AntiAliasing = true };

    // Painted in normal blend: added light turns white over the bright field, and the violet must read.
    private void DrawAura()
    {
        var breathe = .5f + .5f * Mathf.Sin(_clock * 2.6f);
        var strength = Mathf.Clamp(_readyAmount * (.75f + .25f * breathe) + _flash, 0f, 1.4f);
        var slot = new Rect2(Vector2.Zero, Size);
        if (strength > .01f)
        {
            // A soft violet halo hugging the slot, breathing in and out.
            _halo.ShadowColor = new Color(.46f, .24f, 1f, Mathf.Min(1f, .7f * strength));
            _halo.ShadowSize = Mathf.RoundToInt(16f + 10f * breathe + 14f * _flash);
            _halo.SetCornerRadiusAll(12);
            _aura.DrawStyleBox(_halo, slot.Grow(-4f));
        }
        if (_readyAmount <= .01f) return;
        // Two heartbeat outlines roll off the slot while the spell is ready.
        _pulse.SetCornerRadiusAll(10);
        for (var i = 0; i < 2; i++)
        {
            var beat = ((_clock + i * Heartbeat * .5f) % Heartbeat) / Heartbeat;
            _pulse.BorderColor = new Color(i == 0 ? Ice : Violet, .85f * (1f - beat) * (1f - beat) * _readyAmount);
            _pulse.SetBorderWidthAll(Mathf.Max(1, Mathf.RoundToInt(4f * (1f - beat))));
            _aura.DrawStyleBox(_pulse, slot.Grow(2f + 26f * (1f - Mathf.Pow(1f - beat, 2f))));
        }
    }

    private void DrawSparkle()
    {
        var star = SpellVfx.Tex("p_star");
        var glow = SpellVfx.Glow;
        // The open flash over the card.
        if (_flash > .01f)
        {
            var size = Size * (1.1f + .4f * (1f - _flash));
            _sparkle.DrawTextureRect(glow, new Rect2(Size / 2 - size / 2, size), false, new Color(Hot, .55f * _flash));
        }
        // Sparks running round the frame, each with a short tail.
        if (_readyAmount > .01f)
        {
            var frame = new Rect2(new Vector2(9, 9), Size - new Vector2(18, 20));
            for (var i = 0; i < 4; i++)
            {
                for (var tail = 0; tail < 8; tail++)
                {
                    var along = (_clock * .2f + i / 4f - tail * .008f + 1f) % 1f;
                    var at = Perimeter(frame, along);
                    var size = (13f - tail * 1.3f) * (1f + .2f * Mathf.Sin(_clock * 9f + i));
                    var color = new Color(tail == 0 ? Hot : tail < 3 ? Ice : Violet, (tail == 0 ? 1f : .7f - tail * .08f) * _readyAmount);
                    if (tail == 0 && star != null)
                    {
                        _sparkle.DrawSetTransform(at, _clock * 4f + i);
                        _sparkle.DrawTextureRect(star, new Rect2(-size, -size, size * 2, size * 2), false, color);
                        _sparkle.DrawSetTransform(Vector2.Zero);
                    }
                    else _sparkle.DrawTextureRect(glow, new Rect2(at - Vector2.One * size, Vector2.One * size * 2), false, color);
                }
            }
        }
        foreach (var mote in _motes)
        {
            var t = mote.Age / mote.Life;
            var alpha = Mathf.Min(1f, t * 6f) * (1f - t);
            var size = mote.Size * (1f - .4f * t);
            if (star != null) _sparkle.DrawTextureRect(star, new Rect2(mote.Position - Vector2.One * size, Vector2.One * size * 2), false, new Color(mote.Color, alpha));
        }
    }

    /// <summary>A point a fraction of the way round a rectangle's edge, clockwise from the top left.</summary>
    private static Vector2 Perimeter(Rect2 rect, float along)
    {
        var w = rect.Size.X; var h = rect.Size.Y;
        var d = along * 2f * (w + h);
        if (d < w) return rect.Position + new Vector2(d, 0);
        d -= w;
        if (d < h) return rect.Position + new Vector2(w, d);
        d -= h;
        if (d < w) return rect.Position + new Vector2(w - d, h);
        d -= w;
        return rect.Position + new Vector2(0, h - d);
    }
}
