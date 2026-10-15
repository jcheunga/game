using System;
using System.Linq;
using Godot;

// What each unit ability looks like when it goes off. The rules stay in BattleController.Content.cs; these helpers
// give the ranged abilities real shots (damage lands when the shot does), bolts and beams for the magic, light
// and numbers for heals, a run for charges, and a mark for hexes, built on the spell kit (SpellVfx).
public partial class BattleController
{
    /// <summary>A shot of the unit's own kind (arrow, bolt, flask...) that deals `damage` when it lands.</summary>
    private void FireContentShot(Unit source, Unit target, Func<float> onArrive, float speedScale = 1.15f, ProjectileStyle style = null,
        float delay = 0f)
    {
        if (!IsInstanceValid(target) || target.IsDead) return;
        void Launch()
        {
            if (!IsInstanceValid(source) || source.IsDead || !IsInstanceValid(target) || target.IsDead) return;
            var life = target.CombatLifetime;
            var projectile = ProjectilePool.Acquire();
            AddChild(projectile);
            projectile.GlobalPosition = source.WeaponContactPosition;
            projectile.ShouldPause = () => _battlePaused || _endlessCheckpointActive || _battleEnded;
            var shot = style ?? ProjectileStyles.ForUnit(source.DefinitionId, source.MotionProfile);
            projectile.SetStyle(shot);
            projectile.LaunchGroundY = source.GlobalPosition.Y;
            AudioDirector.Instance?.PlayLaunch(source, shot.Id);
            var speed = (source.ProjectileSpeed > 0f ? source.ProjectileSpeed : 230f) * speedScale;
            projectile.Setup(target, 0f, speed, source.Tint.Lightened(.25f),
                _ => IsInstanceValid(target) && target.CombatLifetime == life ? onArrive() : 0f,
                () => !IsInstanceValid(target) || target.IsDead || target.CombatLifetime != life,
                (pos, dmg, _) => { if (dmg > 0 && IsInstanceValid(source)) ApplyImpactReaction(source, target, dmg, true); });
        }
        if (delay > 0f) SpellVfx.Later(this, delay, Launch); else Launch();
    }

    /// <summary>A crackling bolt from the caster's hands or weapon to a target, with a flash where it strikes.</summary>
    private void ContentBolt(Unit source, Vector2 to, Color color, float width = 3f)
    {
        var from = source.WeaponContactPosition;
        SpellVfx.Bolt(this, from, to, color, .3f, width);
        SpellVfx.Flash(this, from, color, 10f, .25f);
        SpellVfx.Flash(this, to, color, 16f, .3f);
        SpellVfx.Burst(this, to, "p_spark_bolt", 8, .35f, SpellVfx.Fade(color.Lightened(.4f), color), 2f, 4.5f, 40f, 110f);
    }

    /// <summary>Healing light on an ally, with the amount it restored rising off it.</summary>
    private void ContentHealFx(Unit healer, Unit patient, float amount)
    {
        if (amount <= .05f || !IsInstanceValid(patient)) return;
        var gold = new Color("ffd77a");
        if (healer != null && IsInstanceValid(healer) && healer != patient)
            SpellVfx.Bolt(this, healer.BodyContactPosition, patient.BodyContactPosition, new Color(gold, .7f), .28f, 2f);
        SpellVfx.Flash(this, patient.BodyContactPosition, gold, 18f, .4f);
        SpellVfx.Burst(this, patient.Position + new Vector2(0, -6), "p_star", 7, .8f, SpellVfx.Fade(gold.Lightened(.3f), gold), 2.5f, 5f,
            14f, 34f, 30f, Vector2.Up, gravity: -30f);
        SpawnCombatNumber(patient.Position + new Vector2(0, -24), "+" + Mathf.RoundToInt(amount), new Color("9ff08a"), .8f);
    }

    /// <summary>A hex or binding on a victim: a shadow sigil at its feet and a pulse of the caster's colour.</summary>
    private void ContentMarkFx(Unit victim, Color color, string school = "shadow")
    {
        if (!IsInstanceValid(victim)) return;
        SpellVfx.Sigil(this, victim.Position, school, color, Mathf.Max(18f, victim.Radius * 1.3f), .9f, .6f);
        SpellVfx.Flash(this, victim.BodyContactPosition, color, 14f, .3f);
    }

    /// <summary>A charge: the unit runs the distance over a short time (not a teleport), throwing up dust behind,
    /// and the hits land when it arrives.</summary>
    private void ContentChargeRun(Unit unit, float dx, Action arrive)
    {
        const float seconds = .28f;
        var life = unit.CombatLifetime;
        var start = unit.Position;
        var steps = 7;
        for (var k = 1; k <= steps; k++)
        {
            var t = k / (float)steps;
            var last = k == steps;
            SpellVfx.Later(this, seconds * t, () =>
            {
                if (!IsInstanceValid(unit) || unit.IsDead || unit.CombatLifetime != life) return;
                var e = 1 - (1 - t) * (1 - t);
                var want = start + new Vector2(dx * e, 0);
                OffsetUnitWithinBattlefield(unit, want - unit.Position);
                SpellVfx.Burst(this, unit.Position, "p_dust", 2, .5f, SpellVfx.Fade(new Color(.62f, .55f, .44f, .55f)), 4f, 8f, 6f, 18f,
                    40f, new Vector2(-Mathf.Sign(dx), -.4f), additive: false, layer: SpellVfx.TroopLayer);
                if (last) arrive();
            });
        }
    }
}

public partial class BattleController
{
    // Passive abilities that ride on ordinary blows: each gets a beat of its own so it reads in the melee.

    private void ContentSparks(Vector2 at, Color color, int count = 8, Vector2? toward = null, float speed = 120f)
        => SpellVfx.Burst(this, at, "p_spark_bolt", count, .3f, SpellVfx.Fade(color.Lightened(.45f), color), 2f, 4.5f, speed * .4f, speed,
            toward == null ? 180f : 40f, toward, gravity: 140f);

    /// <summary>A crescent of light where a sweeping blow carries through into the next foe.</summary>
    private void ContentSlashArc(Unit source, float reach, Color color)
    {
        if (!IsInstanceValid(source)) return;
        var facing = source.GetFacing() < 0 ? -1f : 1f;
        var slash = new VfxSlash { Radius = Mathf.Max(26f, reach), Tint = color, Facing = facing, Width = Mathf.Clamp(reach * .22f, 6f, 13f) };
        AddChild(slash);
        slash.GlobalPosition = source.GlobalPosition + new Vector2(facing * 4f, -source.ChestHeight * .9f);
    }

    /// <summary>A hook or harpoon line from the weapon to the victim, snapping taut as it drags them in.</summary>
    private void ContentHookChain(Unit source, Unit target)
    {
        var from = source.WeaponContactPosition;
        var chain = new VfxChains { Hold = target.BodyContactPosition - from, Tint = new Color(.62f, .6f, .56f), Life = .38f,
            ZAsRelative = false, ZIndex = SpellVfx.AirLayer };
        chain.Anchors.Add(Vector2.Zero);
        AddChild(chain);
        chain.GlobalPosition = from;
        ContentSparks(target.BodyContactPosition, new Color("d8c9a8"), 5);
    }

    /// <summary>Plate cracking off a foe: steel shards flung away from the blow.</summary>
    private void ContentShardsFx(Unit source, Unit target, Color color, int count = 9)
    {
        var side = target.Position.X >= source.Position.X ? 1f : -1f;
        var away = new Vector2(side, -.6f).Normalized();
        SpellVfx.Burst(this, target.BodyContactPosition, "p_shard", count, .55f, SpellVfx.Fade(color.Lightened(.2f), color.Darkened(.3f)),
            3f, 6.5f, 60f, 150f, 50f, away, gravity: 420f, additive: false, spin: 540f, layer: SpellVfx.TroopLayer + 1);
        SpellVfx.Flash(this, target.BodyContactPosition, new Color("ffcf8a"), 12f, .22f);
    }

    /// <summary>A blow that finds the gap: a bright streak driven through the target and out the far side.</summary>
    private void ContentPierceFx(Unit source, Unit target)
    {
        var at = target.BodyContactPosition;
        var dir = (at - source.WeaponContactPosition).Normalized();
        if (dir == Vector2.Zero) dir = new Vector2(source.GetFacing(), 0);
        SpellVfx.Burst(this, at + dir * 4f, "p_spark_bolt", 7, .22f, SpellVfx.Fade(new Color("fff1c8"), new Color("e0a050")), 2f, 5f, 160f, 260f,
            10f, dir, explosiveness: 1f);
        SpellVfx.Flash(this, at, new Color("fff1c8"), 10f, .18f);
    }

    /// <summary>A parried blow: the steel rings, sparks fly between the blades and the attacker is thrown off.</summary>
    private void ContentParryFx(Unit defender, Unit attacker)
    {
        var at = defender.WeaponContactPosition.Lerp(attacker.BodyContactPosition, .35f);
        SpellVfx.Flash(this, at, Colors.White, 16f, .2f);
        ContentSparks(at, new Color("ffe2a0"), 12, new Vector2(Mathf.Sign(attacker.Position.X - defender.Position.X), -.5f), 170f);
        SpawnCombatNumber(defender.Position + new Vector2(0, -26), "PARRY", new Color("f3e2b0"), .55f);
        AudioDirector.Instance?.PlayShieldBlock(at);
        attacker.HoldPose(.08f);
    }
}

/// <summary>A crescent of light along a weapon's sweep, drawn in the shallow 3/4 view (flattened), racing from
/// high in front of the swinger to low and fading behind its head.</summary>
public partial class VfxSlash : Node2D
{
    public float Radius = 30f, Width = 8f, Facing = 1f, Life = .2f;
    public Color Tint = Colors.White;
    private float _age;

    public override void _Ready() { ZAsRelative = false; ZIndex = SpellVfx.AirLayer; Material = SpellVfx.Additive; }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= Life) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var t = _age / Life;
        const float from = -1.25f, to = 1.05f;
        var head = from + (to - from) * Mathf.Min(1f, t * 2.2f);
        var tail = from + (to - from) * Mathf.Clamp(t * 1.7f - .45f, 0f, 1f);
        if (head - tail < .05f) return;
        const int steps = 14;
        var outer = new Vector2[steps + 1];
        var inner = new Vector2[steps + 1];
        for (var i = 0; i <= steps; i++)
        {
            var k = i / (float)steps;
            var a = tail + (head - tail) * k;
            var w = Width * Mathf.Sin(Mathf.Pi * Mathf.Min(1f, k * 1.15f)) * (1f - .5f * t);
            var dir = new Vector2(Mathf.Cos(a) * Facing, Mathf.Sin(a) * .55f);
            outer[i] = dir * Radius;
            inner[i] = dir * (Radius - w);
        }
        // quads along the sweep (a single polygon fails to triangulate where the crescent tapers to nothing)
        var alpha = 1f - t * t;
        for (var i = 0; i < steps; i++)
        {
            var k0 = i / (float)steps; var k1 = (i + 1) / (float)steps;
            var c0 = new Color(Tint.Lerp(Colors.White, k0 * .6f), alpha * (.15f + .75f * k0));
            var c1 = new Color(Tint.Lerp(Colors.White, k1 * .6f), alpha * (.15f + .75f * k1));
            DrawPrimitive(new[] { inner[i], outer[i], outer[i + 1], inner[i + 1] }, new[] { c0, c0, c1, c1 }, null);
        }
    }
}

/// <summary>Shows an aura around a unit that strengthens its neighbours (a war banner, a herald's howl): a faint
/// ring on the ground at the aura's reach, breathing slowly. Follows the unit, gone when it dies.</summary>
public partial class BattleAuraRing : Node2D
{
    public Unit Owner;
    public float Radius;
    public Color Tint;
    private uint _life;
    private float _age;

    public override void _Ready()
    {
        ZAsRelative = false; ZIndex = SpellVfx.GroundLayer;
        _life = Owner?.CombatLifetime ?? 0;
        Material = SpellVfx.Additive;
    }

    public override void _Process(double delta)
    {
        if (Owner == null || !IsInstanceValid(Owner) || Owner.IsDead || Owner.CombatLifetime != _life) { QueueFree(); return; }
        _age += (float)delta;
        Position = Owner.Position;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var b = .5f + .5f * Mathf.Sin(_age * 2.2f);
        DrawSetTransform(Vector2.Zero, 0, new Vector2(1, BattleGroundPlane.DepthScale));
        DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 56, new Color(Tint, .16f + .1f * b), 2f, true);
        DrawCircle(Vector2.Zero, Radius, new Color(Tint, .025f + .02f * b));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}
