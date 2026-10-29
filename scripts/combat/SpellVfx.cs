using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

/// <summary>
/// Building blocks for spell effects: rendered flipbooks (fire, smoke, dust, ice and rock bursting up), glows,
/// ground sigils that draw themselves, shockwaves, rising light beams, lightning, decals, things erupting from the
/// road and painted particle bursts. Everything lies on the battlefield's shallow ground plane (or stands up from it),
/// freezes while the battle is paused and runs at the battle's speed.
/// The recipe for each spell lives in BattleController.SpellVfx.cs; the painted textures come from
/// art/royal/vfx.py and the flipbooks from art/remaster/build_spell_flipbooks.py (both in assets/fx/spells).
/// </summary>
public static class SpellVfx
{
    // Layers, against the battle's own: decals with the gore stains, sigils under the troops, eruptions
    // depth-sorted with the troops, light and sky effects over them.
    public const int DecalLayer = -3, GroundLayer = -2, TroopLayer = 0, AirLayer = 24, FlashLayer = 26;
    private const string TexturePath = "res://assets/fx/spells/";
    private static readonly Dictionary<string, Texture2D> Textures = new();
    private static Dictionary<string, Flipbook> _books;
    private static CanvasItemMaterial _additive, _premultiplied;
    private static Shader _sigilShader, _beamShader;
    private static Texture2D _glow, _ring, _beam, _streak, _disc;

    public static bool Reduced => GameState.Instance?.ReducedMotion ?? false;
    public static CanvasItemMaterial Additive => _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
    /// <summary>For the flipbooks: their colour is stored premultiplied, so smoke covers the road and fire adds light to it.</summary>
    public static CanvasItemMaterial Premultiplied => _premultiplied ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.PremultAlpha };

    /// <summary>A rendered flipbook by name (assets/fx/spells/flipbooks.json), or null when it is missing.</summary>
    public static Flipbook Book(string name)
    {
        if (_books == null)
        {
            _books = new();
            try
            {
                using var file = FileAccess.Open(TexturePath + "flipbooks.json", FileAccess.ModeFlags.Read);
                if (file != null)
                {
                    using var doc = JsonDocument.Parse(file.GetAsText());
                    foreach (var entry in doc.RootElement.EnumerateObject())
                    {
                        var e = entry.Value;
                        var path = TexturePath + "fb_" + entry.Name + ".png";
                        if (!ResourceLoader.Exists(path)) continue;
                        float F(JsonElement v) => v.GetSingle();
                        _books[entry.Name] = new Flipbook
                        {
                            Sheet = ResourceLoader.Load<Texture2D>(path), Cols = e.GetProperty("cols").GetInt32(), Frames = e.GetProperty("frames").GetInt32(),
                            Cell = new Vector2(F(e.GetProperty("cell")[0]), F(e.GetProperty("cell")[1])),
                            Pivot = new Vector2(F(e.GetProperty("pivot")[0]), F(e.GetProperty("pivot")[1])),
                            Width = F(e.GetProperty("width")), Radius = F(e.GetProperty("radius")),
                            Loop = e.TryGetProperty("loop", out var loop) && loop.GetBoolean(),
                            Hold = e.TryGetProperty("hold", out var hold) && hold.GetBoolean(),
                        };
                    }
                }
            }
            catch (Exception)
            {
                // Missing or broken metadata: the recipes fall back to their painted pieces.
            }
        }
        return _books.TryGetValue(name, out var book) ? book : null;
    }

    /// <summary>A painted effect texture, or a soft glow when it is missing.</summary>
    public static Texture2D Tex(string name)
    {
        if (Textures.TryGetValue(name, out var cached)) return cached;
        var path = TexturePath + name + ".png";
        var texture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        return Textures[name] = texture ?? Glow;
    }

    /// <summary>A soft round light: bright core, long smooth falloff.</summary>
    public static Texture2D Glow => _glow ??= Procedural(128, 128, (x, y) =>
    {
        var d = Mathf.Min(1f, new Vector2(x - .5f, y - .5f).Length() * 2f);
        return Mathf.Pow(1f - d, 2.2f);
    });

    /// <summary>A soft ring, brightest at 85% of the radius.</summary>
    public static Texture2D Ring => _ring ??= Procedural(256, 256, (x, y) =>
    {
        var d = new Vector2(x - .5f, y - .5f).Length() * 2f;
        return Mathf.Exp(-Mathf.Pow((d - .85f) / .07f, 2f)) + .18f * Mathf.Exp(-Mathf.Pow((d - .72f) / .16f, 2f)) * (d < 1 ? 1 : 0);
    });

    /// <summary>A filled disc with a soft edge, for darkening or lighting an area evenly.</summary>
    public static Texture2D Disc => _disc ??= Procedural(128, 128, (x, y) =>
        Mathf.SmoothStep(1f, .62f, new Vector2(x - .5f, y - .5f).Length() * 2f));

    /// <summary>A vertical light column: soft across, fading out towards the top.</summary>
    public static Texture2D Beam => _beam ??= Procedural(64, 256, (x, y) =>
        Mathf.Exp(-Mathf.Pow((x - .5f) / .2f, 2f)) * Mathf.SmoothStep(0f, .55f, y) * Mathf.SmoothStep(1f, .9f, y));

    /// <summary>A horizontal streak: bright head on the right, tapering tail on the left.</summary>
    public static Texture2D Streak => _streak ??= Procedural(256, 32, (x, y) =>
        Mathf.Exp(-Mathf.Pow((y - .5f) / (.08f + .2f * x), 2f)) * Mathf.Pow(x, 1.6f) * Mathf.SmoothStep(1f, .94f, x));

    private static Texture2D Procedural(int width, int height, Func<float, float, float> alpha)
    {
        using var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                image.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp(alpha((x + .5f) / width, (y + .5f) / height), 0, 1)));
        return ImageTexture.CreateFromImage(image);
    }

    public static void ClearCache()
    {
        Textures.Clear();
        _books = null;
        _glow = _ring = _beam = _streak = _disc = null;
    }

    private static ShaderMaterial ShaderOf(ref Shader shader, string name, float seed)
    {
        shader ??= ResourceLoader.Load<Shader>($"res://assets/shaders/{name}.gdshader");
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("seed", seed);
        return material;
    }

    private static T Add<T>(Node parent, T node, Vector2 at, int layer) where T : Node2D
    {
        node.ZIndex = layer;
        node.ProcessMode = Node.ProcessModeEnum.Pausable;
        parent.AddChild(node);
        node.Position = at;
        return node;
    }

    /// <summary>A sprite that grows, fades, spins and drifts over its life. The base of every piece below.</summary>
    public static VfxSprite Sprite(Node parent, Vector2 at, Texture2D texture, Color color, Vector2 from, Vector2 to, float life,
        int layer = AirLayer, bool additive = true, bool ground = false, float delay = 0f)
    {
        if (parent == null) return null;
        var sprite = new VfxSprite { Texture = texture, Tint = color, SizeFrom = from, SizeTo = to, Life = Reduced ? Mathf.Min(life, .25f) : life,
            Ground = ground, Delay = delay };
        if (additive) sprite.Material = Additive;
        return Add(parent, sprite, at, layer);
    }

    /// <summary>
    /// Plays a flipbook standing on the road at <paramref name="at"/>, sized so its body spans <paramref name="radius"/>
    /// battle pixels either side. Tintable books are white and take the tint's colour; coloured ones (fire, ice,
    /// rock) are best left white. A looping book repeats for the whole life; the others play once over
    /// <paramref name="play"/> seconds (the life when 0), then hold their last frame or end.
    /// </summary>
    public static VfxSprite Play(Node parent, Vector2 at, string name, float radius, float life, Color? tint = null, int layer = TroopLayer,
        float delay = 0f, float play = 0f, bool flip = false, bool reverse = false)
    {
        var book = Book(name);
        if (parent == null || book == null) return null;
        var width = book.Width * radius / book.Radius;
        var size = new Vector2(width, width * book.Cell.Y / book.Cell.X);
        var sprite = new VfxSprite
        {
            Texture = book.Sheet, Book = book, Tint = tint ?? Colors.White, SizeFrom = size, SizeTo = size, Pivot = book.Pivot,
            Life = Reduced ? Mathf.Min(life, .5f) : life, PlayTime = play, Delay = delay, Flip = flip, Reverse = reverse,
            Material = Premultiplied, FadeIn = .02f, FadeOut = book.Hold || book.Loop ? .25f : .12f,
        };
        return Add(parent, sprite, at, layer);
    }

    /// <summary>A bloom of light, round in the air or flattened on the ground.</summary>
    public static VfxSprite Flash(Node parent, Vector2 at, Color color, float radius, float life = .35f, bool ground = false, float delay = 0f) =>
        // Added light washes out quickly over the sunlit road, so flashes stay at half strength.
        Sprite(parent, at, Glow, new Color(color, color.A * .5f), Vector2.One * radius * 1.1f, Vector2.One * radius * 2f, life, ground ? GroundLayer : FlashLayer, true, ground, delay)
            ?.Envelope(.08f, .2f);

    /// <summary>
    /// A painted rune circle drawn onto the ground: the ink runs round it over <paramref name="draw"/> seconds, flares
    /// as the spell lands (<paramref name="flareAt"/> seconds in, if set), then it turns slowly, shimmers and fades.
    /// </summary>
    public static VfxSprite Sigil(Node parent, Vector2 at, string school, Color color, float radius, float life = 1.1f, float spin = .5f, float delay = 0f,
        float draw = .22f, float flareAt = -1f)
    {
        var sigil = Sprite(parent, at, Tex("sigil_" + school), color, Vector2.One * radius * 1.75f, Vector2.One * radius * 2.1f, life,
            GroundLayer, true, true, delay);
        if (sigil == null) return null;
        sigil.Spin = Reduced ? 0 : spin; sigil.SizeEase = 4f;
        sigil.Material = ShaderOf(ref _sigilShader, "spell_sigil", (float)GD.RandRange(0, 100));
        sigil.DrawTime = Reduced ? 0 : draw; sigil.FlareAt = flareAt;
        return sigil.Envelope(.04f, .4f);
    }

    /// <summary>An expanding ring across the ground.</summary>
    public static VfxSprite Shockwave(Node parent, Vector2 at, Color color, float radius, float life = .45f, float delay = 0f) =>
        Sprite(parent, at, Ring, color, Vector2.One * radius * .3f, Vector2.One * radius * 2.4f, life, GroundLayer, true, true, delay)
            ?.Envelope(.02f, .7f);

    /// <summary>A column of light standing on the ground.</summary>
    public static VfxSprite Pillar(Node parent, Vector2 at, Color color, float width, float height, float life = .7f, float delay = 0f)
    {
        var pillar = Sprite(parent, at, Beam, new Color(color, color.A * .55f), new Vector2(width * 1.3f, height * .7f), new Vector2(width * .35f, height), life, AirLayer, true, false, delay);
        if (pillar == null) return null;
        pillar.Pivot = new Vector2(.5f, 1f);
        pillar.Material = ShaderOf(ref _beamShader, "spell_beam", (float)GD.RandRange(0, 100));
        return pillar.Envelope(.1f, .55f);
    }

    /// <summary>A painted mark left on the road, under everything standing on it.</summary>
    public static VfxSprite Decal(Node parent, Vector2 at, string name, Color color, float radius, float life = 3f, float delay = 0f)
    {
        var decal = Sprite(parent, at, Tex("decal_" + name), color, Vector2.One * radius * 1.7f, Vector2.One * radius * 2.1f, life,
            DecalLayer, false, true, delay);
        if (decal == null) return null;
        decal.SizeEase = 6f; decal.Angle = (float)GD.RandRange(0, Mathf.Tau);
        return decal.Envelope(.04f, .4f);
    }

    /// <summary>
    /// A painted element that bursts up out of the ground (brambles, a wave): it pushes up through the road with a
    /// little overshoot, stands, and at the end sinks back into it.
    /// </summary>
    public static VfxSprite Rise(Node parent, Vector2 at, string name, float width, float life = 1.2f, float delay = 0f, bool flip = false,
        Color? color = null, float emerge = .16f)
    {
        var rise = Sprite(parent, at, Tex("rise_" + name), color ?? Colors.White, new Vector2(width * .9f, width), new Vector2(width, width), life,
            TroopLayer, false, false, delay);
        if (rise == null) return null;
        rise.Pivot = new Vector2(.5f, .96f); rise.SizeEase = 5f; rise.Flip = flip;
        rise.Emerge = Reduced ? 0 : emerge; rise.Sink = Mathf.Min(.3f, life * .25f);
        return rise.Envelope(.02f, .2f);
    }

    /// <summary>A jagged bolt of lightning between two points, re-forking as it flickers.</summary>
    public static VfxBolt Bolt(Node parent, Vector2 from, Vector2 to, Color color, float life = .32f, float width = 3f, float delay = 0f)
    {
        if (parent == null) return null;
        var bolt = new VfxBolt { From = from, To = to, Tint = color, Life = Reduced ? .12f : life, Width = width, Delay = delay, Material = Additive };
        return Add(parent, bolt, Vector2.Zero, AirLayer);
    }

    /// <summary>Spectral chains from points on the ground around <paramref name="feet"/> up to <paramref name="hold"/>.</summary>
    public static VfxChains Chains(Node parent, Vector2 feet, Vector2 hold, Color color, int count, float spread, float life, float delay = 0f)
    {
        if (parent == null) return null;
        var chains = new VfxChains { Hold = hold - feet, Tint = color, Life = Reduced ? Mathf.Min(life, .4f) : life, Delay = delay, Material = Additive };
        for (var i = 0; i < count; i++)
        {
            var angle = Mathf.Pi * (.15f + .7f * i / Mathf.Max(1, count - 1)) + (i % 2 == 0 ? 0 : Mathf.Pi);
            chains.Anchors.Add(new Vector2(Mathf.Cos(angle) * spread, Mathf.Sin(angle) * spread * BattleGroundPlane.DepthScale));
        }
        return Add(parent, chains, feet, TroopLayer);
    }

    /// <summary>Runs an action after a delay that keeps battle time: it waits through pauses and fast-forward.</summary>
    public static void Later(Node parent, float delay, Action action)
    {
        if (parent == null || !GodotObject.IsInstanceValid(parent)) return;
        if (delay <= 0f) { action(); return; }
        var timer = parent.GetTree().CreateTimer(delay, processAlways: false);
        timer.Timeout += () => { if (GodotObject.IsInstanceValid(parent)) action(); };
    }

    /// <summary>A one-shot burst of painted particles. Sizes are in battle pixels; velocities in pixels per second.</summary>
    public static CpuParticles2D Burst(Node parent, Vector2 at, string texture, int count, float life, Color[] ramp,
        float sizeMin, float sizeMax, float speedMin, float speedMax, float spread = 180f, Vector2? direction = null,
        float gravity = 0f, bool additive = true, float groundRadius = 0f, float spin = 0f, float explosiveness = .9f,
        int layer = AirLayer, float delay = 0f, float damping = 0f, bool shrink = true, float pull = 0f)
    {
        if (parent == null) return null;
        if (Reduced) count = Math.Max(1, count / 4);
        var tex = texture.StartsWith("p_") || texture.StartsWith("decal_") || texture.StartsWith("sigil_") || texture.StartsWith("rise_")
            ? Tex(texture) : ParticleTextureLoader.TryLoad(texture) ?? Glow;
        var scale = 1f / Mathf.Max(1, Mathf.Max(tex.GetWidth(), tex.GetHeight()));
        var particles = new CpuParticles2D
        {
            Amount = Math.Max(1, count), Lifetime = life, OneShot = true, Explosiveness = explosiveness, Emitting = false,
            Texture = tex, LocalCoords = false, Direction = direction ?? Vector2.Up, Spread = spread,
            InitialVelocityMin = speedMin, InitialVelocityMax = speedMax, Gravity = new Vector2(0, gravity),
            ScaleAmountMin = sizeMin * scale, ScaleAmountMax = sizeMax * scale, DampingMin = damping, DampingMax = damping,
            AngleMin = 0, AngleMax = 360, AngularVelocityMin = -spin, AngularVelocityMax = spin,
            ColorRamp = Ramp(ramp), FixedFps = 0, RadialAccelMin = -pull, RadialAccelMax = -pull,
        };
        if (shrink)
        {
            var curve = new Curve();
            curve.AddPoint(new Vector2(0, .55f)); curve.AddPoint(new Vector2(.18f, 1f)); curve.AddPoint(new Vector2(1, .35f));
            particles.ScaleAmountCurve = curve;
        }
        if (groundRadius > 0f)
        {
            // Emit from an ellipse on the ground plane rather than a single point.
            var points = new Vector2[24];
            for (var i = 0; i < points.Length; i++)
            {
                var angle = GD.Randf() * Mathf.Tau; var distance = Mathf.Sqrt(GD.Randf()) * groundRadius;
                points[i] = new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance * BattleGroundPlane.DepthScale);
            }
            particles.EmissionShape = CpuParticles2D.EmissionShapeEnum.Points;
            particles.EmissionPoints = points;
        }
        if (additive) particles.Material = Additive;
        Add(parent, particles, at, layer);
        Later(parent, delay, () =>
        {
            if (!GodotObject.IsInstanceValid(particles)) return;
            particles.Emitting = true;
            Later(particles, life + .3f, particles.QueueFree);
        });
        return particles;
    }

    private static Gradient Ramp(Color[] colors)
    {
        var gradient = new Gradient();
        if (colors == null || colors.Length == 0) colors = new[] { Colors.White, new Color(1, 1, 1, 0) };
        gradient.SetColor(0, colors[0]);
        gradient.SetOffset(0, 0);
        if (colors.Length == 1) { gradient.SetColor(1, new Color(colors[0], 0)); return gradient; }
        gradient.SetColor(1, colors[^1]);
        gradient.SetOffset(1, 1);
        for (var i = 1; i < colors.Length - 1; i++) gradient.AddPoint(i / (float)(colors.Length - 1), colors[i]);
        return gradient;
    }

    /// <summary>Colour ramp helper: a hot start fading to transparent.</summary>
    public static Color[] Fade(Color start, Color end) => new[] { new Color(start, 0), start, start.Lerp(end, .5f), new Color(end, 0) };
    public static Color[] Fade(Color color) => Fade(color, color);
}

/// <summary>A rendered flipbook sheet (art/remaster/build_spell_flipbooks.py): where the road origin sits in a frame,
/// how many metres a frame spans and the body radius in metres it was built around.</summary>
public sealed class Flipbook
{
    public Texture2D Sheet;
    public int Cols, Frames;
    public Vector2 Cell, Pivot;
    public float Width, Radius;
    public bool Loop, Hold;
    public const float LoopFps = 24f;

    public Rect2 Frame(int index) => new(index % Cols * Cell.X, index / Cols * Cell.Y, Cell.X, Cell.Y);
}

/// <summary>
/// A textured sprite with a life: size, alpha, spin and drift over time, or the frames of a flipbook. It can also
/// push up out of the road and sink back (<see cref="Emerge"/>), or draw itself on as a sigil. Built by <see cref="SpellVfx"/>.
/// </summary>
public partial class VfxSprite : Node2D
{
    public Texture2D Texture;
    public Flipbook Book;
    public Color Tint = Colors.White;
    public Vector2 SizeFrom, SizeTo, Pivot = new(.5f, .5f), Velocity, Gravity;
    public float Life = .5f, Delay, Spin, Angle, SizeEase = 2f, FadeIn = .1f, FadeOut = .5f, Flicker;
    /// <summary>Flipbooks: seconds to play the frames over (0 = the whole life).</summary>
    public float PlayTime;
    /// <summary>Seconds to push up out of the road, and to sink back into it at the end (0 = neither).</summary>
    public float Emerge, Sink;
    /// <summary>Where the road hides what is below it, as a fraction down the texture (-1 = at the pivot). A flipbook's
    /// pivot is the road origin, with nearer things below it, so its books clip at their bottom edge instead.</summary>
    public float Clip = -1f;
    /// <summary>Sigils: seconds for the ink to run round the circle, and when it flares (-1 = never).</summary>
    public float DrawTime, FlareAt = -1f;
    public bool Ground, Flip, Reverse;
    private float _age;

    /// <summary>Sets the fade-in and fade-out as fractions of the life.</summary>
    public VfxSprite Envelope(float fadeIn, float fadeOut) { FadeIn = fadeIn; FadeOut = fadeOut; return this; }

    /// <summary>Stretches the life so it lasts at least <paramref name="seconds"/> more (a burning field that was refreshed).</summary>
    public void KeepFor(float seconds) => Life = Mathf.Max(Life, Mathf.Max(0, _age - Delay) + seconds);

    /// <summary>Cuts the life short so it fades out over the next <paramref name="seconds"/>.</summary>
    public void EndIn(float seconds) => Life = Mathf.Min(Life, Mathf.Max(0, _age - Delay) + seconds);

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _age += dt;
        if (_age < Delay) { Visible = false; return; }
        Visible = true;
        if (_age - Delay >= Life) { QueueFree(); return; }
        Position += Velocity * dt;
        Velocity += Gravity * dt;
        Angle += Spin * dt;
        if (Material is ShaderMaterial shader && (DrawTime > 0 || FlareAt >= 0))
        {
            var age = _age - Delay;
            shader.SetShaderParameter("draw", DrawTime <= 0 ? 1f : Mathf.Clamp(age / DrawTime, 0, 1));
            shader.SetShaderParameter("flare", FlareAt < 0 ? 0f : 1.6f * Mathf.Exp(-Mathf.Pow((age - FlareAt) / .07f, 2)) + (age > FlareAt ? .35f * Mathf.Exp(-(age - FlareAt) * 4f) : 0f));
        }
        QueueRedraw();
    }

    private int FrameIndex(float age)
    {
        if (Book.Loop) return (int)(age * Flipbook.LoopFps + GetInstanceId() % 7) % Book.Frames;
        var k = Mathf.Clamp(age / (PlayTime > 0 ? PlayTime : Life), 0, .9999f);
        if (Reverse) k = .9999f - k;
        return (int)(k * Book.Frames);
    }

    /// <summary>How much of an emerging sprite stands above the road: shoots up past full height, settles, sinks at the end.</summary>
    private float Standing(float age)
    {
        var up = Emerge <= 0 ? 1f : Mathf.Clamp(age / Emerge, 0, 1);
        // An overshooting ease: past full height and back.
        var rise = 1f + 2.4f * Mathf.Pow(up - 1f, 3) + 1.4f * Mathf.Pow(up - 1f, 2);
        var down = Sink <= 0 ? 1f : Mathf.Clamp((Life - age) / Sink, 0, 1);
        return Mathf.Clamp(rise, 0, 1.1f) * Mathf.SmoothStep(0, 1, down);
    }

    public override void _Draw()
    {
        if (Texture == null || _age < Delay) return;
        var t = Mathf.Clamp((_age - Delay) / Life, 0, 1);
        var grow = 1f - Mathf.Pow(1f - t, SizeEase);
        var size = SizeFrom.Lerp(SizeTo, grow);
        var alpha = Mathf.Min(FadeIn <= 0 ? 1 : t / FadeIn, FadeOut <= 0 ? 1 : (1 - t) / FadeOut);
        alpha = Mathf.Clamp(alpha, 0, 1);
        if (Flicker > 0) alpha *= 1 - Flicker * .5f * (1 + Mathf.Sin(_age * 37f + GetInstanceId() % 13));
        var basis = new Transform2D(Angle, Vector2.Zero);
        if (Ground) basis = Transform2D.Identity.Scaled(new Vector2(1, BattleGroundPlane.DepthScale)) * basis;
        DrawSetTransformMatrix(basis);
        var rect = new Rect2(-size * Pivot, size);
        var source = new Rect2(Vector2.Zero, Texture.GetSize());
        if (Book != null) source = Book.Frame(FrameIndex(_age - Delay));
        if (Emerge > 0 || Sink > 0)
        {
            // Only what has come up through the road shows; the rest is still below it.
            var standing = Standing(_age - Delay);
            var clip = Clip < 0 ? Pivot.Y : Clip;
            var shown = Mathf.Min(1f, standing * clip);
            if (shown <= .001f) { DrawSetTransform(Vector2.Zero); return; }
            var top = -size.Y * Pivot.Y + (1 - standing) * clip * size.Y;
            rect = new Rect2(rect.Position.X, top, size.X, size.Y * shown);
            source = new Rect2(source.Position, new Vector2(source.Size.X, source.Size.Y * shown));
        }
        if (Flip) rect = new Rect2(rect.Position.X + rect.Size.X, rect.Position.Y, -rect.Size.X, rect.Size.Y);
        var a = Tint.A * alpha;
        // Premultiplied books fade by scaling their colour with their cover.
        var color = Book != null ? new Color(Tint.R * a, Tint.G * a, Tint.B * a, a) : new Color(Tint, a);
        DrawTextureRectRegion(Texture, rect, source, color);
        DrawSetTransform(Vector2.Zero);
    }
}

/// <summary>A flickering lightning bolt between two points in its parent's space: a soft glow, a hot core and small forks.</summary>
public partial class VfxBolt : Node2D
{
    public Vector2 From, To;
    public Color Tint = Colors.White;
    public float Life = .3f, Width = 3f, Delay;
    private float _age, _reshape;
    private Vector2[] _path = Array.Empty<Vector2>();
    private readonly List<Vector2[]> _forks = new();

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age < Delay) { Visible = false; return; }
        Visible = true;
        if (_age - Delay >= Life) { QueueFree(); return; }
        if ((_reshape -= (float)delta) <= 0) { Reshape(); _reshape = .045f; }
        QueueRedraw();
    }

    private void Reshape()
    {
        var points = new List<Vector2> { From, To };
        var jitter = From.DistanceTo(To) * .22f;
        for (var level = 0; level < 5; level++)
        {
            for (var i = points.Count - 1; i > 0; i--)
            {
                var a = points[i - 1]; var b = points[i];
                var normal = (b - a).Orthogonal().Normalized();
                points.Insert(i, (a + b) / 2 + normal * (float)GD.RandRange(-jitter, jitter));
            }
            jitter *= .52f;
        }
        _path = points.ToArray();
        _forks.Clear();
        for (var f = 0; f < 3; f++)
        {
            var start = _path[GD.RandRange(_path.Length / 5, _path.Length * 4 / 5)];
            var direction = (To - From).Normalized().Rotated((float)GD.RandRange(-.9, .9)) * From.DistanceTo(To) * (float)GD.RandRange(.12, .26);
            var fork = new Vector2[5];
            for (var i = 0; i < fork.Length; i++)
                fork[i] = start + direction * i / (fork.Length - 1) + direction.Orthogonal().Normalized() * (float)GD.RandRange(-6, 6) * i / 4f;
            _forks.Add(fork);
        }
    }

    public override void _Draw()
    {
        if (_path.Length < 2) return;
        var t = Mathf.Clamp((_age - Delay) / Life, 0, 1);
        var fade = (1 - t) * (.75f + .25f * Mathf.Sin(_age * 90f));
        var local = new Vector2[_path.Length];
        for (var i = 0; i < _path.Length; i++) local[i] = _path[i] - Position;
        DrawPolyline(local, new Color(Tint, .22f * fade), Width * 6f, true);
        DrawPolyline(local, new Color(Tint, .55f * fade), Width * 2.2f, true);
        DrawPolyline(local, new Color(1, 1, 1, .95f * fade), Width * .8f, true);
        foreach (var fork in _forks)
        {
            var points = new Vector2[fork.Length];
            for (var i = 0; i < fork.Length; i++) points[i] = fork[i] - Position;
            DrawPolyline(points, new Color(Tint, .45f * fade), Width * 1.1f, true);
            DrawPolyline(points, new Color(1, 1, 1, .7f * fade), Width * .4f, true);
        }
    }
}

/// <summary>Chains of glowing links pulled taut from the ground to a held point; they unspool, rattle and fade.</summary>
public partial class VfxChains : Node2D
{
    public readonly List<Vector2> Anchors = new();
    public Vector2 Hold;
    public Color Tint = Colors.White;
    public float Life = 1f, Delay;
    private float _age;

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age < Delay) { Visible = false; return; }
        Visible = true;
        if (_age - Delay >= Life) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var link = SpellVfx.Tex("p_chain");
        var age = _age - Delay;
        var reach = Mathf.Clamp(age / .16f, 0, 1);
        var alpha = Mathf.Clamp((Life - age) / (Life * .3f), 0, 1);
        const float pitch = 12f;
        foreach (var anchor in Anchors)
        {
            var end = anchor.Lerp(Hold, reach);
            var span = end - anchor;
            var steps = Mathf.Max(1, (int)(span.Length() / pitch));
            var angle = span.Angle();
            for (var i = 0; i <= steps; i++)
            {
                // A slight sag and rattle keeps the chains from looking like rods.
                var along = i / (float)steps;
                var sag = Mathf.Sin(along * Mathf.Pi) * 5f + Mathf.Sin(_age * 30f + i) * .8f;
                var at = anchor + span * along + span.Orthogonal().Normalized() * sag;
                DrawSetTransformMatrix(new Transform2D(angle + (i % 2 == 0 ? 0 : .25f), at));
                DrawTextureRect(link, new Rect2(-pitch * .75f, -pitch * .38f, pitch * 1.5f, pitch * .76f), false, new Color(Tint, alpha));
            }
        }
        DrawSetTransform(Vector2.Zero);
    }
}
