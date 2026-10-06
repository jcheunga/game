using System;
using Godot;

/// <summary>Trails, launch flashes and impacts for each projectile style. Sizes are world-pixel diameters.</summary>
public static class ProjectileEffects
{
    private static bool Reduced => GameState.Instance != null && GameState.Instance.ReducedMotion;

    private static CpuParticles2D Emitter(string texture, int amount, float lifetime)
    {
        var particles = new CpuParticles2D
        {
            Amount = Mathf.Max(1, amount),
            Lifetime = lifetime,
            LocalCoords = false,
            ZIndex = -1,
            Gravity = Vector2.Zero,
            Texture = ParticleTextureLoader.TryLoad(texture) ?? ParticleTextureLoader.SoftTexture
        };
        return particles;
    }

    private static void Size(CpuParticles2D particles, float min, float max)
    {
        var pixels = Mathf.Max(1, Mathf.Max(particles.Texture.GetWidth(), particles.Texture.GetHeight()));
        particles.ScaleAmountMin = min / pixels;
        particles.ScaleAmountMax = max / pixels;
    }

    private static Gradient Fade(Color color, float alpha = .8f, Color? hot = null)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(hot ?? color.Lightened(.35f), alpha));
        gradient.AddPoint(.45f, new Color(color, alpha * .55f));
        gradient.SetColor(gradient.GetPointCount() - 1, new Color(color.Darkened(.25f), 0f));
        return gradient;
    }

    private static Curve Grow(float from = .6f, float to = 1.6f)
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0, from));
        curve.AddPoint(new Vector2(1, to));
        return curve;
    }

    private static Material Additive => new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

    /// <summary>A trailing emitter for <paramref name="owner"/>; the projectile keeps it at the drawn head.</summary>
    public static CpuParticles2D SpawnTrail(Node2D owner, ProjectileStyle style)
    {
        if (Reduced || style.Trail is ProjectileTrail.None or ProjectileTrail.Streak) return null;
        var color = style.TrailTint;
        CpuParticles2D p;
        switch (style.Trail)
        {
            case ProjectileTrail.Embers:
                p = Emitter("particle_spark", 16, .4f);
                p.Direction = Vector2.Up; p.Spread = 70; p.InitialVelocityMin = 6; p.InitialVelocityMax = 22;
                p.Gravity = new Vector2(0, -18); Size(p, 1.6f, 3.2f);
                p.ColorRamp = Fade(color, .95f, new Color("fff0b8"));
                p.Material = Additive;
                break;
            case ProjectileTrail.Smoke:
                p = Emitter("particle_smoke", 12, .7f);
                p.Direction = Vector2.Up; p.Spread = 50; p.InitialVelocityMin = 3; p.InitialVelocityMax = 9;
                Size(p, 4f, 7f); p.ScaleAmountCurve = Grow(.5f, 1.8f);
                p.ColorRamp = Fade(color, .45f, color.Lightened(.1f));
                break;
            case ProjectileTrail.Glow:
                p = Emitter("particle_soft", 14, .32f);
                p.Spread = 180; p.InitialVelocityMin = 2; p.InitialVelocityMax = 8; Size(p, 3f, 6f);
                p.ScaleAmountCurve = Grow(1f, .3f);
                p.ColorRamp = Fade(color, .7f);
                p.Material = Additive;
                break;
            case ProjectileTrail.Sparkle:
                p = Emitter("particle_spark", 14, .36f);
                p.Spread = 180; p.InitialVelocityMin = 6; p.InitialVelocityMax = 20; Size(p, 1.4f, 3f);
                p.ColorRamp = Fade(color, .95f, Colors.White);
                p.Material = Additive;
                break;
            case ProjectileTrail.Drips:
                p = Emitter("particle_soft", 9, .45f);
                p.Direction = Vector2.Down; p.Spread = 30; p.InitialVelocityMin = 4; p.InitialVelocityMax = 14;
                p.Gravity = new Vector2(0, 170); Size(p, 1.6f, 2.6f);
                p.ColorRamp = Fade(color, .9f, color.Lightened(.2f));
                break;
            case ProjectileTrail.Frost:
                p = Emitter("particle_frost", 9, .4f);
                p.Spread = 180; p.InitialVelocityMin = 3; p.InitialVelocityMax = 10; Size(p, 2f, 3.6f);
                p.AngleMin = 0; p.AngleMax = 360;
                p.ColorRamp = Fade(color, .85f, Colors.White);
                p.Material = Additive;
                break;
            default: // sparks
                p = Emitter("particle_spark", 6, .22f);
                p.Spread = 180; p.InitialVelocityMin = 10; p.InitialVelocityMax = 34; p.Gravity = new Vector2(0, 200);
                Size(p, 1.2f, 2.2f);
                p.ColorRamp = Fade(color, .95f, Colors.White);
                p.Material = Additive;
                break;
        }
        p.Emitting = true;
        owner.AddChild(p);
        return p;
    }

    /// <summary>Stops a trail and leaves its particles to finish in the world.</summary>
    public static void ReleaseTrail(CpuParticles2D trail, Node world)
    {
        if (trail == null || !GodotObject.IsInstanceValid(trail)) return;
        if (world != null && GodotObject.IsInstanceValid(world) && trail.GetParent() != world)
        {
            var at = trail.GlobalPosition;
            // Keep the depth it drew at in flight, where its z was relative to the projectile's.
            var z = trail.ZIndex + (trail.ZAsRelative && trail.GetParent() is CanvasItem owner ? owner.ZIndex : 0);
            trail.GetParent()?.RemoveChild(trail);
            world.AddChild(trail);
            trail.GlobalPosition = at;
            trail.ZIndex = z;
        }
        // Only once it is back in the tree: an emitter that re-enters the tree already stopped never
        // processes again, so its last particles would freeze instead of fading.
        trail.Emitting = false;
        FreeAfter(trail, trail.Lifetime + .05f);
    }

    private static async void FreeAfter(Node node, double seconds)
    {
        if (!GodotObject.IsInstanceValid(node) || !node.IsInsideTree()) { node?.QueueFree(); return; }
        await node.ToSignal(node.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        if (GodotObject.IsInstanceValid(node)) node.QueueFree();
    }

    private static void Burst(Node parent, Vector2 at, string texture, int amount, float lifetime, Color color,
        float sizeMin, float sizeMax, float speedMin, float speedMax, float spread = 180, Vector2? direction = null,
        float gravity = 0, bool additive = false, float alpha = .9f, Color? hot = null, Curve scale = null, bool spinParticles = false)
    {
        var p = Emitter(texture, amount, lifetime);
        p.OneShot = true;
        p.Explosiveness = 1;
        p.ZIndex = 100;
        p.LocalCoords = false;
        p.Direction = direction ?? Vector2.Up;
        p.Spread = spread;
        p.InitialVelocityMin = speedMin;
        p.InitialVelocityMax = speedMax;
        p.Gravity = new Vector2(0, gravity);
        Size(p, sizeMin, sizeMax);
        if (scale != null) p.ScaleAmountCurve = scale;
        if (spinParticles) { p.AngleMin = 0; p.AngleMax = 360; p.AngularVelocityMin = -240; p.AngularVelocityMax = 240; }
        p.ColorRamp = Fade(color, alpha, hot);
        if (additive) p.Material = Additive;
        parent.AddChild(p);
        p.GlobalPosition = at;
        p.Emitting = true;
        FreeAfter(p, lifetime + .1f);
    }

    private static void Ring(Node parent, Vector2 at, Color color, float from, float to, float seconds, bool filled = false)
    {
        var ring = new BattleEffect();
        ring.Setup(color, from, to, seconds, filled);
        parent.AddChild(ring);
        ring.GlobalPosition = at;
        ring.ZIndex = 21;
    }

    public static void SpawnLaunch(Node parent, ProjectileStyle style, Vector2 at, Vector2 direction)
    {
        if (parent == null || Reduced) return;
        switch (style.Launch)
        {
            case ProjectileLaunch.Flash:
                Burst(parent, at, "particle_soft", 1, .14f, style.GlowColor, style.GlowRadius * 1.6f, style.GlowRadius * 1.9f,
                    0, 0, additive: true, alpha: .9f, hot: Colors.White, scale: Grow(.4f, 1.2f));
                Burst(parent, at, "particle_spark", 6, .22f, style.GlowColor, 1.4f, 2.6f, 18, 46, 50, direction, additive: true,
                    hot: Colors.White);
                break;
            case ProjectileLaunch.Smoke:
                Burst(parent, at, "particle_soft", 1, .1f, new Color("ffd28a"), 8, 10, 0, 0, additive: true, alpha: .8f);
                Burst(parent, at, "particle_smoke", 6, .7f, style.TrailTint.Lerp(new Color("6a6660"), .5f), 4, 8, 6, 22, 40, direction,
                    gravity: -12, alpha: .5f, scale: Grow(.6f, 2f));
                break;
            case ProjectileLaunch.Dust:
                Burst(parent, at, "particle_smoke", 4, .45f, new Color("b8a888"), 3, 6, 6, 18, 60, -direction, alpha: .35f,
                    scale: Grow(.6f, 1.6f));
                break;
        }
    }

    public static void SpawnImpact(Node parent, ProjectileStyle style, Vector2 at, Vector2 direction, float damage, Node2D target,
        Func<bool> shouldPause = null)
    {
        if (parent == null) return;
        if (Reduced)
        {
            Ring(parent, at, style.GlowColor.A > 0 ? style.GlowColor : new Color("f1e6cf"), 2, 7, .12f, true);
            return;
        }
        var heavy = Mathf.Clamp(damage / 24f, .5f, 1.6f);
        var back = -direction;
        switch (style.Impact)
        {
            case ProjectileImpact.Pierce:
                Burst(parent, at, "particle_smoke", 3, .35f, new Color("bba98c"), 2.5f, 4.5f, 6, 16, 70, back, alpha: .4f,
                    scale: Grow(.6f, 1.5f));
                Burst(parent, at, "particle_spark", 5, .22f, new Color("e8d6b0"), 1f, 2f, 30, 70, 80, back, gravity: 220,
                    hot: Colors.White);
                break;
            case ProjectileImpact.Heavy:
                Burst(parent, at, "particle_soft", 1, .1f, new Color("fff2d8"), 12, 14, 0, 0, additive: true, alpha: .7f);
                Burst(parent, at, "particle_smoke", 7, .55f, new Color("a89a80"), 4, 8, 10, 30, 120, back, alpha: .45f,
                    scale: Grow(.6f, 1.8f));
                Burst(parent, at, "particle_stone", (int)(5 * heavy), .5f, new Color("6e6252"), 1.6f, 3.2f, 40, 90, 90, back,
                    gravity: 320, alpha: 1, spinParticles: true);
                Burst(parent, at, "particle_spark", 6, .2f, new Color("ffe6b0"), 1.2f, 2.4f, 40, 90, 120, back, gravity: 200,
                    additive: true, hot: Colors.White);
                break;
            case ProjectileImpact.Shatter:
                Burst(parent, at, "particle_soft", 1, .14f, new Color("ffb860"), 16, 20, 0, 0, additive: true, alpha: .9f,
                    hot: Colors.White, scale: Grow(.5f, 1.3f));
                Burst(parent, at, "particle_fire", 10, .45f, new Color("ff8a2a"), 4, 8, 14, 40, 160, Vector2.Up, gravity: -40,
                    additive: true, hot: new Color("ffe2a0"), scale: Grow(1f, .3f), spinParticles: true);
                Burst(parent, at, "particle_spark", 10, .35f, new Color("e6f4ff"), 1f, 2.2f, 50, 110, 150, Vector2.Up,
                    gravity: 360, hot: Colors.White);
                Burst(parent, at, "particle_smoke", 5, .8f, new Color("5a524a"), 5, 9, 4, 14, 80, Vector2.Up, gravity: -16,
                    alpha: .45f, scale: Grow(.6f, 2f));
                break;
            case ProjectileImpact.Splash:
                Burst(parent, at, "particle_soft", 12, .5f, style.TrailTint, 1.6f, 3.2f, 40, 95, 150, Vector2.Up, gravity: 300,
                    alpha: .95f, hot: style.TrailTint.Lightened(.3f));
                Burst(parent, at, "particle_smoke", 6, .9f, style.TrailTint.Darkened(.2f), 5, 10, 4, 12, 120, Vector2.Up,
                    gravity: -10, alpha: .4f, scale: Grow(.6f, 2.2f));
                Ring(parent, at, style.TrailTint.Lightened(.1f), 3, 12 * heavy, .22f);
                break;
            case ProjectileImpact.Fire:
                Burst(parent, at, "particle_soft", 1, .16f, new Color("ffb24a"), 18, 22, 0, 0, additive: true, alpha: .9f,
                    hot: Colors.White, scale: Grow(.5f, 1.4f));
                Burst(parent, at, "particle_fire", 14, .55f, new Color("ff7a2a"), 4, 9, 16, 46, 170, Vector2.Up, gravity: -50,
                    additive: true, hot: new Color("ffe2a0"), scale: Grow(1f, .3f), spinParticles: true);
                Burst(parent, at, "particle_spark", 12, .6f, new Color("ffb24a"), 1.2f, 2.4f, 30, 80, 140, Vector2.Up,
                    gravity: 120, additive: true, hot: Colors.White);
                Burst(parent, at, "particle_smoke", 6, 1f, new Color("4a4440"), 6, 11, 4, 14, 80, Vector2.Up, gravity: -20,
                    alpha: .45f, scale: Grow(.6f, 2.2f));
                break;
            case ProjectileImpact.Frost:
                Burst(parent, at, "particle_soft", 1, .14f, new Color("bff4ff"), 12, 15, 0, 0, additive: true, alpha: .8f,
                    hot: Colors.White);
                Burst(parent, at, "particle_frost", 9, .5f, new Color("bff4ff"), 2.4f, 4.4f, 24, 60, 160, back, gravity: 120,
                    additive: true, hot: Colors.White, spinParticles: true);
                Burst(parent, at, "particle_spark", 8, .4f, new Color("e6fbff"), 1, 2, 30, 70, 180, back, gravity: 160,
                    additive: true, hot: Colors.White);
                break;
            case ProjectileImpact.Metal:
                Burst(parent, at, "particle_spark", 12, .28f, new Color("ffd27a"), 1, 2.2f, 50, 120, 110, back, gravity: 360,
                    additive: true, hot: Colors.White);
                Burst(parent, at, "particle_soft", 1, .08f, new Color("fff0c8"), 8, 10, 0, 0, additive: true, alpha: .8f);
                break;
            case ProjectileImpact.Lightning:
                Burst(parent, at, "particle_soft", 1, .12f, new Color("e8f6ff"), 22, 26, 0, 0, additive: true, alpha: 1,
                    hot: Colors.White, scale: Grow(.6f, 1.2f));
                Burst(parent, at, "particle_lightning", 5, .2f, style.GlowColor, 6, 11, 4, 16, 180, additive: true,
                    hot: Colors.White, spinParticles: true);
                Burst(parent, at, "particle_spark", 14, .3f, style.GlowColor, 1.2f, 2.4f, 60, 140, 180, gravity: 120,
                    additive: true, hot: Colors.White);
                Ring(parent, at, style.GlowColor.Lightened(.3f), 3, 16, .18f);
                break;
            case ProjectileImpact.Soul:
                Burst(parent, at, "particle_soft", 1, .14f, style.GlowColor, 16, 20, 0, 0, additive: true, alpha: .85f,
                    hot: Colors.White);
                Burst(parent, at, "particle_soft", 9, .8f, style.GlowColor, 3, 6, 6, 22, 70, Vector2.Up, gravity: -40,
                    additive: true, alpha: .8f, scale: Grow(1f, .2f));
                Burst(parent, at, "particle_spark", 8, .35f, style.GlowColor, 1.2f, 2.4f, 30, 70, 180, additive: true,
                    hot: Colors.White);
                break;
            default: // burst: a bright flash, a ring and a scatter of sparks in the spell's colour
                Burst(parent, at, "particle_soft", 1, .14f, style.GlowColor, style.GlowRadius * 1.5f, style.GlowRadius * 1.8f,
                    0, 0, additive: true, alpha: .95f, hot: Colors.White, scale: Grow(.5f, 1.3f));
                Burst(parent, at, "particle_spark", 12, .36f, style.GlowColor, 1.2f, 2.8f, 36, 90, 180, gravity: 60,
                    additive: true, hot: Colors.White);
                Ring(parent, at, style.GlowColor.Lightened(.25f), 3, 13, .2f);
                break;
        }
        if (style.Sticks && target is Unit unit && !unit.IsDead)
        {
            var stuck = new StuckProjectile { ShouldPause = shouldPause };
            stuck.Setup(unit, style, direction.Angle(), at);
            parent.AddChild(stuck);
        }
    }
}

/// <summary>The fireball spell's arrival: a comet streak falling from the upper left onto the blast, drawn as
/// the burst goes off so the damage keeps its timing.</summary>
public partial class FireballStreak : Node2D
{
    private const float Life = .32f;
    private float _age;
    private Vector2 _from;
    private Color _color;
    private Func<bool> _shouldPause;

    public static void Spawn(Node parent, Vector2 at, Color color, Func<bool> shouldPause = null)
    {
        if (parent == null || (GameState.Instance?.ReducedMotion ?? false)) return;
        var streak = new FireballStreak { _from = new Vector2(-70, -150), _color = color, _shouldPause = shouldPause, ZIndex = 22 };
        streak.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        parent.AddChild(streak);
        streak.GlobalPosition = at;
    }

    public override void _Process(double delta)
    {
        if (_shouldPause?.Invoke() ?? false) return;
        _age += (float)delta;
        if (_age >= Life) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var t = _age / Life;
        var fade = 1 - t;
        // The streak drains into the impact point: its tail shortens as it fades.
        var tail = _from * (1 - t * .7f);
        var normal = (-_from).Normalized().Orthogonal();
        for (var i = 0; i < 3; i++)
        {
            var w = (9f - i * 3f) * fade;
            var color = i == 2 ? new Color(1, .96f, .82f, .9f * fade) : new Color(_color.Lerp(new Color("ff8a2a"), .4f), (.35f + i * .2f) * fade);
            DrawColoredPolygon(new[] { tail, -normal * w * .5f, normal * w * .5f }, color);
        }
        var r = 16f * (1 - t * .5f);
        DrawTextureRect(ParticleTextureLoader.SoftTexture, new Rect2(-Vector2.One * r, Vector2.One * r * 2), false,
            new Color(1f, .78f, .45f, .9f * fade));
    }
}

/// <summary>An arrow or bolt left standing in a soldier for a moment: it rides along with them, then fades.</summary>
public partial class StuckProjectile : Node2D
{
    private const float Life = .9f, FadeTime = .3f;
    private Unit _host;
    private uint _lifetime;
    private ProjectileStyle _style;
    private float _angle, _age;
    private Vector2 _offset;
    /// <summary>While true the arrow keeps riding its host but does not age, so a paused battle keeps it.</summary>
    public Func<bool> ShouldPause { get; set; }

    public void Setup(Unit host, ProjectileStyle style, float angle, Vector2 at)
    {
        _host = host;
        _lifetime = host.CombatLifetime;
        _style = style;
        _angle = angle;
        _offset = at - host.GlobalPosition;
        ZIndex = 9;
    }

    public override void _Ready() => GlobalPosition = _host.GlobalPosition + _offset;

    public override void _Process(double delta)
    {
        if (!(ShouldPause?.Invoke() ?? false)) _age += (float)delta;
        if (_age >= Life || !IsInstanceValid(_host) || _host.IsDead || _host.CombatLifetime != _lifetime)
        {
            QueueFree();
            return;
        }
        GlobalPosition = _host.GlobalPosition + _offset;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var texture = ProjectileStyles.Texture(_style.Sprite);
        if (texture == null) return;
        var meta = ProjectileStyles.Meta(_style.Sprite);
        var w = _style.Length;
        var h = w * texture.GetHeight() / texture.GetWidth();
        var alpha = Mathf.Clamp((Life - _age) / FadeTime, 0, 1);
        var flip = Mathf.Cos(_angle) < 0 ? -1f : 1f;
        DrawSetTransform(Vector2.Zero, _angle, new Vector2(1, flip));
        // The head is buried: show the shaft from a third of the way in, back to the fletching.
        var src = new Rect2(0, 0, texture.GetWidth() * (meta.Tip.X - .3f), texture.GetHeight());
        DrawTextureRectRegion(texture, new Rect2(-meta.Tip.X * w, -meta.Tip.Y * h, w * (meta.Tip.X - .3f), h), src,
            new Color(Projectile.FieldTint, alpha));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}
