using Godot;

/// <summary>
/// Death presentation particles, timed to the authored clip rather than the moment of death:
/// a small burst at the chest when the unit falls, dust thrown along the ground on the frame
/// the body lands, and motes carried up while the body dissolves. Families come from
/// DeathStyle.Fx. Reduced motion skips all of them.
/// </summary>
public static class BattleDeathEffects
{
    private static readonly Color Dust = new(.6f, .54f, .44f);
    private static readonly Color Bone = new(.9f, .86f, .74f);

    public static void SpawnDeathMoment(Node parent, Vector2 chest, DeathStyle style)
    {
        if (Reduced) return;
        var k = Scale(style);
        var c = style.PlayerSide ? style.Mote : style.Fx switch
        {
            "ash" or "dust" => new Color(.2f, .19f, .19f),
            _ => style.Mote,
        };
        var (texture, gravity, speed) = style.Fx switch
        {
            "embers" => ("particle_spark", 220f, 110f),
            "glass" => ("particle_spark", 320f, 150f),
            "water" => ("particle_soft", 380f, 130f),
            "gas" => ("particle_smoke", -12f, 30f),
            "storm" => ("particle_lightning", 0f, 70f),
            "arcane" => ("particle_arcane", -20f, 50f),
            "holy" => ("particle_heal", -30f, 45f),
            "debris" => ("particle_stone", 360f, 100f),
            _ when style.PlayerSide => ("particle_soft", -40f, 40f),
            "bones" => ("particle_soft", -35f, 35f),
            _ => ("particle_smoke", 50f, 55f),
        };
        var p = Emitter(parent, chest, Mathf.RoundToInt(10 * k), texture, style.Boss ? .8f : .55f);
        p.Explosiveness = .95f;
        p.Spread = 180f;
        p.InitialVelocityMin = speed * .35f * k;
        p.InitialVelocityMax = speed * k;
        p.Gravity = new Vector2(0, gravity);
        p.ScaleAmountMin = 3f * k;
        p.ScaleAmountMax = (texture == "particle_smoke" ? 12f : 6f) * k;
        p.ColorRamp = Ramp(c, .9f);
        Start(p, (float)p.Lifetime + .2f);
    }

    public static void SpawnImpact(Node parent, UnitDeathVisual corpse)
    {
        if (Reduced) return;
        var style = corpse.Style;
        var k = Scale(style);
        var at = corpse.ImpactPosition;
        var spread = new Vector2(Mathf.Max(8f, corpse.BodyLength * .5f), 2f);

        // Dust kicked up along the length of the body.
        var dust = Emitter(parent, at, Mathf.RoundToInt((style.Heavy ? 22 : 14) * k), "particle_smoke", style.Boss ? 1.15f : .75f);
        dust.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
        dust.EmissionRectExtents = spread;
        dust.Explosiveness = .9f;
        dust.Direction = new Vector2(0, -1);
        dust.Spread = 75f;
        dust.InitialVelocityMin = 18f * k;
        dust.InitialVelocityMax = 62f * k;
        dust.Gravity = new Vector2(0, 26f);
        dust.DampingMin = 20f;
        dust.DampingMax = 40f;
        dust.ScaleAmountMin = 9f * k;
        dust.ScaleAmountMax = 22f * k;
        dust.ColorRamp = Ramp(style.Fx == "gas" ? new Color(.5f, .6f, .32f) : Dust, .55f);
        Start(dust, (float)dust.Lifetime + .2f);

        var (count, texture, color, up, gravity) = style.Fx switch
        {
            "bones" => (8, "particle_stone", Bone, 110f, 430f),
            "debris" => (12, "particle_stone", new Color(.42f, .36f, .3f), 120f, 450f),
            "water" => (16, "particle_soft", new Color(.66f, .86f, 1f), 170f, 540f),
            "embers" => (14, "particle_spark", style.Mote, 120f, 260f),
            "glass" => (12, "particle_spark", style.Mote, 140f, 360f),
            "gas" => (10, "particle_smoke", style.Mote, 26f, -8f),
            "holy" or "arcane" or "storm" => (8, style.Fx == "holy" ? "particle_heal" : "particle_arcane", style.Mote, 60f, -20f),
            _ => (0, "", Colors.White, 0f, 0f),
        };
        if (count == 0) return;
        var extra = Emitter(parent, at + new Vector2(0, -2), Mathf.RoundToInt(count * k), texture, style.Fx == "gas" ? 1.3f : .6f);
        extra.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
        extra.EmissionRectExtents = spread * .8f;
        extra.Explosiveness = .95f;
        extra.Direction = new Vector2(0, -1);
        extra.Spread = style.Fx == "gas" ? 90f : 55f;
        extra.InitialVelocityMin = up * .4f * k;
        extra.InitialVelocityMax = up * k;
        extra.Gravity = new Vector2(0, gravity);
        extra.ScaleAmountMin = (texture == "particle_smoke" ? 10f : 2.5f) * k;
        extra.ScaleAmountMax = (texture == "particle_smoke" ? 20f : 5.5f) * k;
        extra.ColorRamp = Ramp(color, .9f);
        Start(extra, (float)extra.Lifetime + .2f);
    }

    public static void SpawnDissolve(Node parent, UnitDeathVisual corpse, float seconds)
    {
        if (Reduced) return;
        var style = corpse.Style;
        var k = Scale(style);
        var at = corpse.ImpactPosition + new Vector2(0, -6f * k);
        var extents = new Vector2(Mathf.Max(8f, corpse.BodyLength * .45f), 6f * k);
        var water = style.Fx == "water";
        var motes = Continuous(parent, at, Mathf.RoundToInt((style.Boss ? 34 : 14) * k), style.PlayerSide ? "particle_soft"
            : style.Fx is "ash" or "dust" ? "particle_smoke" : style.Fx == "embers" ? "particle_spark" : "particle_soft",
            water ? .7f : 1.25f);
        motes.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
        motes.EmissionRectExtents = extents;
        // Caravan souls drift back toward the wagon (left), as if returning to the lantern.
        motes.Direction = style.PlayerSide ? new Vector2(-.35f, -1f).Normalized() : new Vector2(0, water ? 1 : -1);
        motes.Spread = 25f;
        motes.InitialVelocityMin = 10f * k;
        motes.InitialVelocityMax = 34f * k;
        motes.Gravity = new Vector2(0, water ? 90f : -14f);
        motes.ScaleAmountMin = 2f * k;
        motes.ScaleAmountMax = (style.Fx is "ash" or "dust" && !style.PlayerSide ? 7f : 4.5f) * k;
        motes.ColorRamp = Ramp(style.PlayerSide || !(style.Fx is "ash" or "dust") ? style.Mote : new Color(.22f, .2f, .2f), .85f, fadeIn: true);
        Run(motes, seconds);
        // Ash carries a few live embers with it.
        if (!style.PlayerSide && style.Fx is "ash" or "dust")
        {
            var embers = Continuous(parent, at, Mathf.RoundToInt((style.Boss ? 14 : 5) * k), "particle_spark", 1.0f);
            embers.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
            embers.EmissionRectExtents = extents;
            embers.Direction = new Vector2(0, -1);
            embers.Spread = 30f;
            embers.InitialVelocityMin = 14f * k;
            embers.InitialVelocityMax = 40f * k;
            embers.Gravity = new Vector2(0, -10f);
            embers.ScaleAmountMin = 1.5f * k;
            embers.ScaleAmountMax = 3f * k;
            embers.ColorRamp = Ramp(new Color(1f, .5f, .18f), 1f, fadeIn: true);
            Run(embers, seconds);
        }
    }

    private static bool Reduced => GameState.Instance?.ReducedMotion ?? false;

    private static float Scale(DeathStyle style) => Mathf.Clamp(style.Radius / 15f, .75f, 2.2f) * (style.Boss ? 1.15f : 1f);

    private static Gradient Ramp(Color c, float alpha, bool fadeIn = false) => fadeIn
        ? new Gradient { Offsets = new[] { 0f, .15f, .5f, 1f },
            Colors = new[] { new Color(c.Lightened(.15f), 0f), new Color(c.Lightened(.1f), alpha), new Color(c, alpha * .65f), new Color(c.Darkened(.3f), 0f) } }
        : new Gradient { Offsets = new[] { 0f, .5f, 1f },
            Colors = new[] { new Color(c.Lightened(.15f), alpha), new Color(c, alpha * .65f), new Color(c.Darkened(.3f), 0f) } };

    private static CpuParticles2D Emitter(Node parent, Vector2 at, int amount, string texture, float lifetime)
    {
        var p = new CpuParticles2D
        {
            Amount = Mathf.Max(1, amount), OneShot = true, Emitting = false, Position = at, ZIndex = 100,
            Lifetime = lifetime, LocalCoords = false,
            Texture = ParticleTextureLoader.TryLoad(texture) ?? ParticleTextureLoader.SoftTexture,
        };
        parent.AddChild(p);
        return p;
    }

    private static CpuParticles2D Continuous(Node parent, Vector2 at, int amount, string texture, float lifetime)
    {
        var p = Emitter(parent, at, amount, texture, lifetime);
        p.OneShot = false;
        p.Explosiveness = 0f;
        return p;
    }

    private static async void Run(CpuParticles2D p, float seconds)
    {
        Normalize(p);
        p.Emitting = true;
        await p.ToSignal(p.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        if (!GodotObject.IsInstanceValid(p)) return;
        p.Emitting = false;
        await p.ToSignal(p.GetTree().CreateTimer(p.Lifetime + .1f), SceneTreeTimer.SignalName.Timeout);
        if (GodotObject.IsInstanceValid(p)) p.QueueFree();
    }

    private static void Normalize(CpuParticles2D p)
    {
        // Authored sizes are world-pixel diameters, as in BattleParticles.
        if (p.Texture == null) return;
        var pixels = Mathf.Max(1, Mathf.Max(p.Texture.GetWidth(), p.Texture.GetHeight()));
        p.ScaleAmountMin /= pixels;
        p.ScaleAmountMax /= pixels;
    }

    private static async void Start(CpuParticles2D p, float freeAfter)
    {
        Normalize(p);
        p.Emitting = true;
        await p.ToSignal(p.GetTree().CreateTimer(freeAfter), SceneTreeTimer.SignalName.Timeout);
        if (GodotObject.IsInstanceValid(p)) p.QueueFree();
    }

}
