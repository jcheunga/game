using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// The look of every spell: a short wind-up (its sigil draws itself on the road, light gathers), the impact (a
/// rendered flipbook of fire, ice, rock, smoke or vapour, a shockwave, painted particles, a shake for heavy magic)
/// and an aftermath that lingers (flames licking on, dust settling, a scorch mark, frost).
/// Built from SpellVfx pieces; game rules stay in the Apply*Spell methods, which call these.
/// </summary>
public partial class BattleController
{
    // Palettes: hot core, body, deep edge.
    private static readonly Color FireHot = new(1f, .94f, .74f), FireBody = new(1f, .56f, .18f), FireDeep = new(.8f, .2f, .04f);
    private static readonly Color FrostHot = new(.9f, .98f, 1f), FrostBody = new(.56f, .83f, 1f), FrostDeep = new(.26f, .46f, .92f);
    private static readonly Color HolyHot = new(1f, .97f, .84f), HolyBody = new(1f, .83f, .44f), HolyDeep = new(.95f, .6f, .24f);
    private static readonly Color LifeHot = new(.9f, 1f, .78f), LifeBody = new(.5f, .92f, .5f), LifeDeep = new(.18f, .56f, .28f);
    private static readonly Color StormHot = new(.95f, .96f, 1f), StormBody = new(.62f, .7f, 1f), StormDeep = new(.55f, .35f, 1f);
    private static readonly Color VoidHot = new(.92f, .8f, 1f), VoidBody = new(.68f, .42f, 1f), VoidDeep = new(.3f, .12f, .58f);
    private static readonly Color WardBody = new(.6f, .78f, 1f), EarthBody = new(.86f, .72f, .5f), EarthDeep = new(.42f, .32f, .2f);
    private static readonly Color TideHot = new(.85f, 1f, 1f), TideBody = new(.35f, .86f, .86f), TideDeep = new(.12f, .45f, .62f);
    private static readonly Color GoldBody = new(1f, .84f, .4f), TealBody = new(.32f, .82f, .76f), MarkRed = new(1f, .36f, .3f);
    private static readonly Color[] SootRamp = { new(.18f, .15f, .14f, 0), new(.2f, .17f, .15f, .55f), new(.32f, .3f, .28f, .3f), new(.4f, .38f, .36f, 0) };
    private static readonly Color[] DustRamp = { new(.75f, .64f, .48f, 0), new(.78f, .67f, .5f, .6f), new(.8f, .72f, .58f, .25f), new(.82f, .75f, .62f, 0) };
    private static readonly Color[] PaleRamp = { new(1, 1, 1, 0), new(1, 1, 1, 1), new(1, 1, 1, .8f), new(1, 1, 1, 0) };
    // Tints for the white flipbooks (smoke, dust, vapour).
    private static readonly Color Dust = new(.8f, .69f, .53f, .85f), Soot = new(.3f, .26f, .24f, .85f), Mist = new(.84f, .93f, 1f, .7f);
    private static readonly Color Spray = new(.8f, .97f, 1f, .55f), Gloom = new(.36f, .24f, .5f, .45f), StormCloud = new(.3f, .32f, .4f, .9f);

    /// <summary>The rune circle and colour a spell is aimed and cast with.</summary>
    private static (string Sigil, Color Tint) SpellSchool(string effect) => effect switch
    {
        "fireball" or "meteor" or "molten_brand" or "tactic_firepot_mine" => ("fire", FireBody),
        "frost_burst" or "tactic_frost_trap" => ("frost", FrostBody),
        "lightning_strike" or "chain_lightning" or "tactic_grounding_rod" => ("storm", StormBody),
        "heal" or "verdant_renewal" or "bramble_snare" or "tactic_healing_shrine" => ("nature", LifeBody),
        "resurrect" or "radiant_rebuke" or "purifying_light" or "consecrated_ground" or "revealing_flare" or "aegis_bolt"
            or "tactic_purifying_censer" => ("holy", HolyBody),
        "earthquake" or "stone_barricade" or "tactic_palisade_kit" or "tactic_repair_cache" or "tactic_supply_crate"
            or "tactic_bolt_turret" => ("earth", EarthBody),
        "tidal_push" or "gale_step" => ("tide", TideBody),
        "war_cry" or "rallying_advance" or "royal_edict" or "tactic_war_horn" => ("war", GoldBody),
        "polymorph" or "anchor_hex" or "withering_seal" or "gravitation_well" or "soul_harvest" or "banish"
            or "tactic_smoke_pot" or "tactic_decoy_lantern" => ("shadow", VoidBody),
        _ => ("arcane", WardBody),
    };

    private static Vector2 Sz(float size) => Vector2.One * size;
    private static Vector2 Chest(Unit unit) => unit.Position + new Vector2(0, -Mathf.Max(18f, unit.Radius * 2.1f));

    private void SpellShake(float strength, float delay = 0f)
    {
        if (IsReducedMotionEnabled()) return;
        SpellVfx.Later(this, delay, () =>
        {
            _impactShakeStrength = Mathf.Max(_impactShakeStrength, strength);
            _impactShakeTimer = Mathf.Max(_impactShakeTimer, .2f);
        });
    }

    /// <summary>A spell's painted picture used in the world: a fireball in flight, a falling meteor, a ghostly sheep.</summary>
    private VfxSprite SpellPicture(string spellId, Vector2 at, float from, float to, float life, bool additive = false, float alpha = 1f, float delay = 0f)
    {
        var sprite = SpellVfx.Sprite(this, at, RoyalArt.Item(spellId), new Color(1, 1, 1, alpha), Sz(from), Sz(to), life, SpellVfx.AirLayer, additive, false, delay);
        if (sprite != null) sprite.SizeEase = 3f;
        return sprite;
    }

    /// <summary>A puff of white smoke from the road, tinted (dust, soot, mist, gloom). Over the troops when it should hide them.</summary>
    private VfxSprite Puff(Vector2 at, float radius, float life, Color tint, float delay = 0f, bool over = false) =>
        SpellVfx.Play(this, at, "smoke_puff", radius, life, tint, over ? SpellVfx.AirLayer : SpellVfx.TroopLayer, delay);

    /// <summary>A ring of dust thrown out along the road from a heavy landing.</summary>
    private VfxSprite DustRing(Vector2 at, float radius, Color tint, float delay = 0f, float life = 1.1f) =>
        SpellVfx.Play(this, at + new Vector2(0, 2), "dust_ring", radius, life, tint, SpellVfx.TroopLayer, delay);

    /// <summary>A burst of glowing vapour (light, void, life, storm), or a gathering one played backwards. Kept thin: it
    /// sits over the troops, who must still read through it.</summary>
    private VfxSprite Vapour(Vector2 feet, float radius, Color tint, float life = .8f, float delay = 0f, bool gather = false) =>
        SpellVfx.Play(this, feet, "magic_burst", radius, life, new Color(tint, tint.A * .7f), SpellVfx.AirLayer, delay, reverse: gather);

    /// <summary>Patches of flame left burning on the road, dying down one after another.</summary>
    private void Flames(Vector2 at, float radius, int count, float life, float delay = 0f)
    {
        for (var i = 0; i < count; i++)
        {
            var spread = count == 1 ? 0f : (i / (count - 1f) - .5f) * radius * 1.1f;
            var flame = SpellVfx.Play(this, at + new Vector2(spread, (i % 2 == 0 ? -1 : 1) * radius * .12f), "flame_loop",
                radius * (i % 2 == 0 ? .36f : .28f), life * (1f - .18f * (i % 3)), delay: delay + i * .05f, flip: i % 2 == 1);
            flame?.Envelope(.12f, .35f);
        }
    }

    /// <summary>Stone thrust up through the road; it sinks back at the end.</summary>
    private void RockBurst(Vector2 at, float radius, float life, float delay = 0f, bool flip = false)
    {
        var rock = SpellVfx.Play(this, at, "rock_burst", radius, life, delay: delay, play: .4f, flip: flip);
        if (rock != null) { rock.Sink = .3f; rock.Clip = 1f; rock.Envelope(0, .1f); }
    }

    /// <summary>A protective bubble around a unit: a bright rim and a faint fill.</summary>
    private void WardBubble(Unit unit, Color color, float life = .9f, float delay = 0f)
    {
        var size = Mathf.Max(56f, unit.Radius * 4.4f);
        SpellVfx.Sprite(this, Chest(unit) + new Vector2(0, 8), SpellVfx.Ring, color, Sz(size * .6f), Sz(size), life, SpellVfx.AirLayer, true, false, delay)?.Envelope(.08f, .5f);
        SpellVfx.Sprite(this, Chest(unit) + new Vector2(0, 8), SpellVfx.Glow, new Color(color, .35f), Sz(size * .5f), Sz(size * .95f), life, SpellVfx.AirLayer, true, false, delay)?.Envelope(.1f, .5f);
    }

    /// <summary>Concentric rings rolling out through the air, like the sound of a horn.</summary>
    private void SoundRings(Vector2 at, Color color, int count, float reach)
    {
        for (var i = 0; i < count; i++)
            SpellVfx.Sprite(this, at, SpellVfx.Ring, color, Sz(reach * .15f), Sz(reach), .7f, SpellVfx.AirLayer, true, false, i * .12f)?.Envelope(.05f, .7f);
    }

    /// <summary>Fire landing: a rolling fireball that climbs into smoke, a ring of soot, embers, flames left burning, a scorch.</summary>
    private void FireImpactVfx(Vector2 at, float radius, float delay = 0f, float shake = .55f)
    {
        // Over the troops: the blast engulfs everyone it catches.
        SpellVfx.Play(this, at, "fire_burst", radius * 1.05f, 1.5f, layer: SpellVfx.AirLayer, delay: delay);
        DustRing(at, radius * 1.3f, Soot, delay);
        Flames(at, radius, 3, 1.7f, delay + .2f);
        SpellVfx.Flash(this, at + new Vector2(0, -12), FireHot, radius * .8f, .25f, delay: delay);
        SpellVfx.Shockwave(this, at, FireBody, radius * 1.15f, .45f, delay);
        SpellVfx.Burst(this, at, "p_ember", 26, .95f, SpellVfx.Fade(FireHot, FireBody), 4, 9, 120, 270, spread: 75, gravity: 420, delay: delay);
        SpellVfx.Decal(this, at, "scorch", Colors.White, radius * .85f, 5f, delay);
        SpellShake(shake, delay);
    }

    private void FireballVfx(Vector2 at, float radius)
    {
        const float travel = .13f;
        var start = at + new Vector2(-150, -250);
        SpellVfx.Sigil(this, at, "fire", FireBody, radius * .9f, 1f, .9f, flareAt: travel);
        var ball = SpellPicture("spell_fireball", start, 50, 46, travel);
        if (ball != null) { ball.Velocity = (at - start) / travel; ball.Envelope(0, 0); }
        var streak = SpellVfx.Sprite(this, start, SpellVfx.Streak, FireBody, new Vector2(170, 30), new Vector2(150, 24), travel);
        if (streak != null) { streak.Velocity = (at - start) / travel; streak.Angle = (at - start).Angle(); streak.Pivot = new Vector2(.92f, .5f); streak.Envelope(0, 0); }
        SpellVfx.Burst(this, start, "p_flame", 10, .3f, SpellVfx.Fade(FireHot, FireDeep), 14, 24, 10, 40, gravity: -60, explosiveness: 0, spin: 90);
        FireImpactVfx(at, radius, travel);
    }

    private void HealVfx(Vector2 at, float radius, IEnumerable<Unit> healed)
    {
        SpellVfx.Sigil(this, at, "nature", LifeBody, radius * .9f, 1.3f, .35f, draw: .3f, flareAt: .3f);
        Vapour(at, radius * .8f, new Color(LifeBody, .6f), 1f, .2f);
        SpellVfx.Pillar(this, at, new Color(LifeHot, .7f), radius * 1.4f, 170, 1f, .15f);
        SpellVfx.Burst(this, at, "p_orb", 22, 1.25f, SpellVfx.Fade(LifeHot, LifeBody), 5, 11, 20, 60, spread: 30, gravity: -50,
            groundRadius: radius * .8f, explosiveness: .6f, delay: .15f);
        SpellVfx.Burst(this, at, "p_leaf", 10, 1.4f, PaleRamp, 9, 13, 30, 70, spread: 60, gravity: -18, additive: false,
            groundRadius: radius * .6f, spin: 180, layer: SpellVfx.AirLayer, delay: .15f);
        foreach (var ally in healed)
        {
            SpellVfx.Pillar(this, ally.Position, new Color(LifeHot, .8f), 26, 90, .8f, .3f);
            SpellVfx.Burst(this, Chest(ally), "p_star", 5, .6f, SpellVfx.Fade(LifeHot), 8, 14, 20, 60, delay: .3f);
        }
    }

    private void FrostVfx(Vector2 at, float radius, IEnumerable<Unit> chilled, float shake = .3f)
    {
        const float hold = 1.5f;
        SpellVfx.Sigil(this, at, "frost", FrostBody, radius * .95f, 1.4f, -.4f, draw: .16f, flareAt: .12f);
        SpellVfx.Flash(this, at + new Vector2(0, -16), FrostHot, radius * .7f, .25f, delay: .1f);
        SpellVfx.Shockwave(this, at, FrostBody, radius * 1.1f, .5f, .1f);
        // The ice bursts up through the road, stands, then shatters.
        var ice = SpellVfx.Play(this, at, "ice_burst", radius * 1.05f, hold, delay: .08f, play: .5f);
        ice?.Envelope(0, .08f);
        Puff(at, radius * .85f, 1.6f, Mist, .12f);
        SpellVfx.Burst(this, at + new Vector2(0, -14), "p_shard", 18, .75f, PaleRamp, 8, 16, 120, 260, spread: 80, gravity: 440,
            additive: false, spin: 360, delay: .08f + hold - .12f, groundRadius: radius * .5f);
        SpellVfx.Burst(this, at, "p_star", 12, .9f, SpellVfx.Fade(FrostHot, FrostBody), 6, 12, 20, 90, gravity: -20, groundRadius: radius * .6f, delay: .1f);
        SpellVfx.Decal(this, at, "frost", Colors.White, radius * .95f, 5.5f, .1f);
        foreach (var unit in chilled) SpellVfx.Flash(this, Chest(unit), FrostBody, 26, .4f, delay: .15f);
        SpellShake(shake, .1f);
    }

    /// <summary>A storm cloud that gathers above a spot and gives off the bolt.</summary>
    private Vector2 StormCloudVfx(Vector2 at, float width)
    {
        var sky = at + new Vector2((float)GD.RandRange(-20, 20), -165);
        // The puff billows up from its foot, so the foot sits a little under where the bolt leaves it.
        foreach (var (offset, size, delay) in new[] { (new Vector2(0, 40), width, 0f), (new Vector2(width * .55f, 46), width * .7f, .04f),
                     (new Vector2(-width * .5f, 48), width * .6f, .07f) })
            SpellVfx.Play(this, sky + offset, "smoke_puff", size, 1.2f, StormCloud, SpellVfx.AirLayer, delay, play: .5f)?.Envelope(.05f, .35f);
        return sky;
    }

    private void LightningVfx(Vector2 at, float radius, IReadOnlyList<Unit> struck)
    {
        const float strike = .12f;
        var sky = StormCloudVfx(at, radius * .8f);
        SpellVfx.Sigil(this, at, "storm", StormBody, radius * .6f, .9f, 1.2f, draw: .12f, flareAt: strike);
        SpellVfx.Bolt(this, sky, at + new Vector2(0, -4), StormBody, .34f, 3.4f, strike);
        SpellVfx.Bolt(this, sky + new Vector2(30, 0), at, StormDeep, .22f, 2f, strike + .07f);
        SpellVfx.Flash(this, sky, StormHot, 60, .25f, delay: strike);
        SpellVfx.Flash(this, at + new Vector2(0, -30), StormHot, 60, .3f, delay: strike);
        Vapour(at, radius * .55f, StormBody, .5f, strike);
        DustRing(at, radius * .9f, Soot, strike, .9f);
        SpellVfx.Shockwave(this, at, StormBody, radius * .7f, .4f, strike);
        SpellVfx.Burst(this, at + new Vector2(0, -10), "p_spark_bolt", 8, .26f, SpellVfx.Fade(StormHot, StormBody), 12, 22, 60, 160, delay: strike);
        SpellVfx.Burst(this, at, "p_ember", 18, .6f, SpellVfx.Fade(StormHot, StormBody), 3, 6, 120, 270, spread: 80, gravity: 320, delay: strike);
        SpellVfx.Decal(this, at, "scorch", new Color(.75f, .75f, .85f), radius * .35f, 3f, strike);
        var previous = at;
        for (var i = 1; i < struck.Count; i++)
        {
            var next = Chest(struck[i]);
            SpellVfx.Bolt(this, previous + new Vector2(0, -20), next, StormBody, .28f, 2.4f, strike + .06f * i);
            SpellVfx.Flash(this, next, StormHot, 34, .3f, delay: strike + .06f * i);
            previous = struck[i].Position;
        }
        SpellShake(.45f, strike);
    }

    private void WardVfx(Vector2 at, float radius, IEnumerable<Unit> warded)
    {
        SpellVfx.Sigil(this, at, "arcane", WardBody, radius * .9f, 1.5f, .3f, draw: .25f, flareAt: .25f);
        SpellVfx.Shockwave(this, at, WardBody, radius, .5f, .2f);
        Vapour(at, radius * .7f, new Color(WardBody, .7f), .9f, .18f);
        SpellVfx.Burst(this, at, "p_rune", 8, 1.2f, SpellVfx.Fade(new Color(.9f, .95f, 1f), WardBody), 10, 16, 20, 50, spread: 20, gravity: -40,
            groundRadius: radius * .7f, explosiveness: .5f, delay: .2f);
        foreach (var ally in warded) WardBubble(ally, WardBody, 1.1f, .25f);
    }

    private void BarricadeVfx(Vector2 at)
    {
        SpellVfx.Shockwave(this, at, EarthBody, 46, .45f);
        DustRing(at, 52, Dust);
        Puff(at, 30, 1.2f, Dust, .05f);
        SpellVfx.Decal(this, at, "cracks", Colors.White, 40, 5f);
        SpellVfx.Burst(this, at + new Vector2(0, -10), "p_rubble", 12, .9f, PaleRamp, 7, 14, 120, 230, spread: 55, gravity: 600, additive: false, spin: 300);
        SpellShake(.35f);
    }

    private void WarCryVfx(Vector2 core, IEnumerable<Unit> rallied)
    {
        SpellVfx.Sigil(this, core + new Vector2(0, 14), "war", GoldBody, 80, 1.4f, .4f, draw: .2f, flareAt: .15f);
        SoundRings(core + new Vector2(0, -50), GoldBody, 3, 240);
        DustRing(core + new Vector2(70, 18), 70, new Color(Dust, .5f), .08f, .9f);
        SpellVfx.Flash(this, core + new Vector2(0, -40), HolyHot, 60, .35f);
        foreach (var ally in rallied)
        {
            SpellVfx.Flash(this, Chest(ally), GoldBody, 26, .4f, delay: .15f);
            SpellVfx.Pillar(this, ally.Position, new Color(GoldBody, .7f), 20, 80, .7f, .15f);
            SpellVfx.Burst(this, Chest(ally), "p_star", 4, .55f, SpellVfx.Fade(HolyHot, GoldBody), 7, 12, 30, 70, delay: .15f);
        }
    }

    private void EarthquakeVfx(Vector2 at, float radius)
    {
        SpellVfx.Sigil(this, at, "earth", EarthBody, radius * .85f, 1.7f, .2f, draw: .2f, flareAt: .18f);
        SpellVfx.Decal(this, at, "cracks", Colors.White, radius, 6f);
        // The road splits: stone thrusts up in a ragged ring, then the next wave behind it.
        for (var i = 0; i < 5; i++)
        {
            var angle = i * Mathf.Tau / 5 + (float)GD.RandRange(-.4, .4);
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * BattleGroundPlane.DepthScale) * radius * (float)GD.RandRange(.2, .65);
            RockBurst(at + offset, (float)GD.RandRange(26, 36), 1.6f, .08f + i * .07f, i % 2 == 1);
        }
        DustRing(at, radius * 1.15f, Dust, .05f, 1.3f);
        DustRing(at, radius * .85f, Dust, .3f, 1.1f);
        Puff(at, radius * .5f, 1.6f, Dust, .1f);
        SpellVfx.Shockwave(this, at, EarthBody, radius * 1.1f, .55f, .05f);
        SpellVfx.Shockwave(this, at, EarthBody, radius * .9f, .55f, .3f);
        SpellVfx.Burst(this, at + new Vector2(0, -12), "p_rubble", 22, 1f, PaleRamp, 8, 18, 140, 290, spread: 50, gravity: 640, additive: false, spin: 300,
            groundRadius: radius * .5f, delay: .08f);
        SpellShake(.8f, .08f);
        SpellShake(.5f, .35f);
    }

    private void PolymorphVfx(Unit target)
    {
        var chest = Chest(target);
        Vapour(target.Position, 34, new Color(1f, .62f, 1f), .7f);
        Puff(target.Position, 30, 1f, new Color(.95f, .82f, 1f, .8f), .04f, true);
        SpellVfx.Burst(this, chest, "p_swirl", 6, .7f, SpellVfx.Fade(VoidHot, VoidBody), 22, 34, 10, 30, spin: 400);
        SpellVfx.Burst(this, chest, "p_star", 14, .7f, SpellVfx.Fade(VoidHot, VoidBody), 6, 12, 60, 150);
        var sheep = SpellPicture("spell_polymorph", chest + new Vector2(0, -22), 26, 62, 1.15f, delay: .08f);
        if (sheep != null) { sheep.Velocity = new Vector2(0, -28); sheep.Envelope(.08f, .4f); }
    }

    private void ResurrectVfx(Vector2 at)
    {
        SpellVfx.Sigil(this, at, "holy", HolyBody, 55, 1.6f, .5f, draw: .3f, flareAt: .3f);
        SpellVfx.Pillar(this, at, HolyHot, 70, 280, 1.4f, .2f);
        Vapour(at, 52, HolyBody, 1f, .3f);
        var angel = SpellPicture("spell_resurrect", at + new Vector2(0, -80), 90, 130, 1.4f, true, .55f, .25f);
        if (angel != null) { angel.Velocity = new Vector2(0, -24); angel.Envelope(.2f, .5f); }
        SpellVfx.Burst(this, at + new Vector2(0, -90), "p_feather", 10, 1.9f, PaleRamp, 10, 16, 20, 60, spread: 120, gravity: 26, additive: false,
            spin: 120, delay: .4f, explosiveness: .4f);
        SpellVfx.Burst(this, at, "p_orb", 16, 1.3f, SpellVfx.Fade(HolyHot, HolyBody), 5, 10, 30, 80, spread: 25, gravity: -40, groundRadius: 30, explosiveness: .5f, delay: .3f);
    }

    /// <summary>The meteor lands where its warning circle filled: the boulder drops trailing fire, the road bursts.</summary>
    private void MeteorImpactVfx(Vector2 at, float radius)
    {
        const float fall = .22f;
        var start = at + new Vector2(30, -420);
        var rock = SpellPicture("spell_meteor", start, 100, 90, fall);
        if (rock != null) { rock.Velocity = (at - start) / fall; rock.Envelope(0, 0); }
        // A trail of smoke hanging where it fell.
        for (var i = 1; i <= 4; i++)
            Puff(start.Lerp(at, i / 5f), 22 + i * 4, 1f, Soot, fall * i / 5f, true);
        FireImpactVfx(at, radius * 1.5f, fall, 1f);
        RockBurst(at + new Vector2(0, 4), radius * .9f, 2f, fall);
        DustRing(at, radius * 2.2f, Dust, fall + .03f, 1.3f);
        SpellVfx.Flash(this, at + new Vector2(0, -30), FireHot, radius * 1.3f, .4f, delay: fall);
        SpellVfx.Burst(this, at + new Vector2(0, -10), "p_rubble", 14, 1f, PaleRamp, 8, 16, 150, 300, spread: 55, gravity: 640, additive: false, spin: 300, delay: fall);
        SpellVfx.Shockwave(this, at, FireHot, radius * 1.6f, .7f, fall + .05f);
        SpellShake(.6f, fall + .25f);
        SpellVfx.Later(this, fall, () => AudioDirector.Instance?.PlaySpellCast("earthquake", at));
    }

    /// <summary>The spells of the content expansion and the tactics, keyed by effect.</summary>
    private void ExpandedSpellVfx(ResolvedSpellStats spell, Vector2 point, Unit target, Unit ally, IReadOnlyList<Unit> affected,
        IReadOnlyList<Vector2> path, Vector2 from)
    {
        var radius = Mathf.Max(24f, spell.Radius);
        switch (spell.EffectType)
        {
            case "aegis_bolt":
                if (ally == null) break;
                {
                    var start = Chest(ally) + new Vector2(-140, -160); const float travel = .16f;
                    var bolt = SpellVfx.Sprite(this, start, SpellVfx.Streak, HolyBody, new Vector2(120, 22), new Vector2(110, 18), travel);
                    if (bolt != null) { bolt.Velocity = (Chest(ally) - start) / travel; bolt.Angle = (Chest(ally) - start).Angle(); bolt.Pivot = new Vector2(.92f, .5f); bolt.Envelope(0, 0); }
                    Vapour(ally.Position, 30, HolyBody, .6f, travel);
                    WardBubble(ally, HolyBody, 1.1f, travel);
                    SpellVfx.Burst(this, Chest(ally), "p_star", 8, .6f, SpellVfx.Fade(HolyHot, HolyBody), 7, 13, 40, 110, delay: travel);
                    SpellVfx.Sigil(this, ally.Position, "holy", HolyBody, 30, 1f, .8f, travel, flareAt: 0f);
                }
                break;
            case "radiant_rebuke":
                if (target == null) break;
                SpellVfx.Sigil(this, target.Position, "holy", HolyBody, 32, .9f, 1.4f, draw: .1f, flareAt: .1f);
                SpellVfx.Pillar(this, target.Position, HolyHot, 34, 260, .5f, .08f);
                Vapour(target.Position, 30, HolyBody, .55f, .1f);
                SpellVfx.Flash(this, Chest(target), HolyHot, 40, .3f, delay: .1f);
                SpellVfx.Shockwave(this, target.Position, HolyBody, 40, .4f, .1f);
                SpellVfx.Burst(this, Chest(target), "p_rune", 6, .55f, SpellVfx.Fade(VoidHot, VoidBody), 8, 14, 80, 160, gravity: 200, delay: .1f);
                SpellVfx.Burst(this, Chest(target), "p_star", 10, .5f, SpellVfx.Fade(HolyHot, HolyBody), 6, 12, 60, 150, delay: .1f);
                break;
            case "tidal_push":
                {
                    SpellVfx.Sigil(this, point, "tide", TideBody, radius * .9f, 1.1f, -.6f, draw: .15f);
                    var wave = SpellVfx.Rise(this, point + new Vector2(-radius * .6f, 8), "wave", radius * 1.5f, 1f, emerge: .2f);
                    if (wave != null) wave.Velocity = new Vector2(170, 0);
                    DustRing(point + new Vector2(-radius * .2f, 0), radius * 1.1f, Spray, .1f, .9f);
                    SpellVfx.Burst(this, point + new Vector2(0, -20), "p_splash", 10, .8f, PaleRamp, 18, 30, 80, 200, 50, new Vector2(1, -1), 500, false, spin: 90, delay: .15f);
                    SpellVfx.Burst(this, point + new Vector2(0, -20), "p_droplet", 22, .9f, PaleRamp, 4, 8, 120, 260, 60, new Vector2(1, -1), 600, false, delay: .15f);
                    SpellVfx.Shockwave(this, point, TideBody, radius, .5f, .1f);
                }
                break;
            case "anchor_hex":
                if (target == null) break;
                {
                    const float drop = .2f;
                    var anchor = SpellPicture("spell_anchor_hex", target.Position + new Vector2(0, -260), 54, 50, drop);
                    if (anchor != null) { anchor.Velocity = new Vector2(0, 230 / drop); anchor.Envelope(0, 0); }
                    SpellVfx.Sigil(this, target.Position, "shadow", VoidBody, 34, spell.Duration + .5f, 1f, drop, flareAt: 0f);
                    SpellVfx.Chains(this, target.Position, Chest(target) + new Vector2(0, 10), VoidHot, 5, 38, spell.Duration + .3f, drop);
                    SpellVfx.Flash(this, target.Position, VoidBody, 40, .4f, true, drop);
                    SpellVfx.Shockwave(this, target.Position, VoidBody, 44, .4f, drop);
                    DustRing(target.Position, 46, Dust, drop, .9f);
                    SpellShake(.3f, drop);
                }
                break;
            case "molten_brand":
                if (target == null) break;
                {
                    // The branding iron strikes, then the brand burns on.
                    const float strike = .12f;
                    var iron = SpellPicture("spell_molten_brand", Chest(target) + new Vector2(-70, -60), 56, 52, strike + .1f);
                    if (iron != null) { iron.Velocity = new Vector2(70, 60) / strike; iron.Envelope(0, .3f); }
                    SpellVfx.Flash(this, Chest(target), FireHot, 50, .3f, delay: strike);
                    SpellVfx.Sigil(this, target.Position, "fire", MarkRed, 40, 1.4f, 1.1f, strike, flareAt: 0f);
                    var brand = SpellVfx.Sprite(this, Chest(target), SpellVfx.Tex("p_rune"), FireHot, Sz(64), Sz(44), 1.6f, delay: strike);
                    if (brand != null) { brand.Flicker = .3f; brand.Envelope(.04f, .45f); }
                    var burning = SpellVfx.Play(this, target.Position + new Vector2(0, 2), "flame_loop", 16, Mathf.Min(2.4f, spell.Duration), delay: strike);
                    burning?.Envelope(.08f, .3f);
                    SpellVfx.Burst(this, Chest(target), "p_ember", 24, .9f, SpellVfx.Fade(FireHot, FireBody), 3, 7, 70, 200, gravity: 260, delay: strike);
                    SpellVfx.Burst(this, Chest(target), "p_soot", 6, 1.3f, SootRamp, 20, 32, 10, 30, spread: 30, gravity: -30, additive: false, delay: strike);
                }
                break;
            case "meteor":
                SpellVfx.Flash(this, point, MarkRed, radius, .5f, true);
                break;
            case "purifying_light":
                SpellVfx.Sigil(this, point, "holy", HolyBody, radius * .9f, 1.3f, .5f, draw: .2f, flareAt: .2f);
                SpellVfx.Pillar(this, point, HolyHot, radius * 1.6f, 260, 1.1f, .1f);
                Vapour(point, radius * .8f, HolyBody, .9f, .18f);
                foreach (var unit in affected)
                {
                    // The shadow is driven out of each body and burns away.
                    Puff(unit.Position + new Vector2(0, -6), 16, 1.1f, Gloom, .2f, true);
                    SpellVfx.Burst(this, Chest(unit), "p_star", 6, .6f, SpellVfx.Fade(HolyHot, HolyBody), 7, 12, 40, 100, delay: .3f);
                }
                break;
            case "withering_seal":
                SpellVfx.Sigil(this, point, "shadow", VoidBody, radius * 1.05f, 1.8f, -.5f, draw: .3f, flareAt: .3f);
                SpellVfx.Decal(this, point, "blight", Colors.White, radius * .95f, 5f, .2f);
                Puff(point, radius * .9f, 1.8f, new Color(Gloom, .4f), .15f);
                SpellVfx.Burst(this, point + new Vector2(0, -30), "p_ash", 14, 1.6f, PaleRamp, 5, 10, 10, 40, spread: 160, gravity: 30, additive: false,
                    groundRadius: radius * .6f, spin: 120, explosiveness: .5f, delay: .2f);
                foreach (var unit in affected)
                {
                    SpellVfx.Flash(this, Chest(unit), VoidBody, 40, .45f, delay: .3f);
                    var seal = SpellVfx.Sprite(this, Chest(unit) + new Vector2(0, -30), SpellVfx.Tex("p_rune"), new Color(.75f, .55f, 1f), Sz(40), Sz(30),
                        Mathf.Min(2.4f, spell.Duration), delay: .35f);
                    if (seal != null) seal.Flicker = .25f;
                    Puff(unit.Position + new Vector2(0, -6), 14, 1.2f, Gloom, .3f, true);
                }
                break;
            case "gale_step":
                if (ally == null) break;
                DustRing(from, 44, new Color(Dust, .7f), 0, .8f);
                SpellVfx.Burst(this, from + new Vector2(0, -20), "p_leaf", 20, .9f, PaleRamp, 9, 14, 160, 300, 25, Vector2.Right, 0, false, 20, 320);
                for (var i = 0; i < 5; i++)
                {
                    var streak = SpellVfx.Sprite(this, from + new Vector2(-30 + i * 6, -6 - i * 12), SpellVfx.Streak, new Color(TideHot, .85f),
                        new Vector2(150, 16), new Vector2(120, 10), .45f, delay: i * .035f);
                    if (streak != null) streak.Velocity = new Vector2(260, 0);
                }
                SpellVfx.Burst(this, Chest(ally), "p_swirl", 5, .7f, SpellVfx.Fade(TideHot, TealBody), 30, 44, 10, 20, spin: 500, delay: .1f);
                DustRing(ally.Position, 40, new Color(Dust, .7f), .1f, .8f);
                SpellVfx.Shockwave(this, ally.Position, TealBody, 40, .4f, .1f);
                break;
            case "gravitation_well":
                {
                    var life = spell.Duration + .3f;
                    // Vapour sucked in to a point, then a dark eye on the road with a turning swirl of light around it.
                    Vapour(point, radius * 1.1f, new Color(VoidBody, .6f), .5f, gather: true);
                    SpellVfx.Sigil(this, point, "shadow", VoidBody, radius * .8f, life, -2.2f, draw: .3f);
                    var core = SpellVfx.Sprite(this, point, SpellVfx.Disc, new Color(.1f, .03f, .18f, .62f), Sz(radius * 2.5f), Sz(radius * 1.9f), life,
                        SpellVfx.GroundLayer, false, true);
                    core?.Envelope(.12f, .25f);
                    // In front of the bodies it catches: a translucent vortex turning at waist height.
                    for (var band = 0; band < 2; band++)
                    {
                        var vortex = SpellVfx.Sprite(this, point + new Vector2(0, -14 - band * 16), SpellVfx.Tex("p_swirl"), new Color(band == 0 ? VoidBody : VoidHot, .5f),
                            Sz(radius * (2.6f - band * .6f)), Sz(radius * (1.8f - band * .4f)), life, SpellVfx.AirLayer, true, true);
                        if (vortex != null) { vortex.Spin = band == 0 ? -6f : -9f; vortex.Angle = band * 2.1f; vortex.Envelope(.1f, .25f); }
                    }
                    for (var arm = 0; arm < 2; arm++)
                    {
                        var swirl = SpellVfx.Sprite(this, point + new Vector2(0, -4), SpellVfx.Tex("p_swirl"), arm == 0 ? VoidBody : VoidHot,
                            Sz(radius * (2.8f - arm * .7f)), Sz(radius * (1.3f - arm * .3f)), life, SpellVfx.GroundLayer, true, true);
                        if (swirl != null) { swirl.Spin = arm == 0 ? -4.5f : -7f; swirl.Angle = arm * 1.7f; swirl.Envelope(.08f, .25f); }
                    }
                    // Streaks of light and grit sucked in from the rim, wave after wave.
                    for (var wave = 0; wave < 3; wave++)
                        for (var i = 0; i < 8; i++)
                        {
                            var angle = i * Mathf.Tau / 8 + wave * .4f;
                            var rim = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * BattleGroundPlane.DepthScale) * radius * 1.25f + new Vector2(0, -8);
                            var streak = SpellVfx.Sprite(this, rim, SpellVfx.Streak, new Color(VoidHot, .8f), new Vector2(70, 10), new Vector2(40, 6), .5f,
                                delay: wave * (spell.Duration / 3f));
                            if (streak == null) continue;
                            streak.Velocity = (point + new Vector2(0, -8) - rim) / .5f * .9f; streak.Angle = (point - rim).Angle();
                            streak.Pivot = new Vector2(.92f, .5f); streak.Envelope(.1f, .35f);
                        }
                    SpellVfx.Burst(this, point, "p_pebble", 24, .9f, PaleRamp, 6, 12, 0, 10, additive: false, groundRadius: radius * 1.2f, pull: 300, spin: 260);
                    SpellVfx.Burst(this, point, "p_wisp", 12, .9f, SpellVfx.Fade(VoidHot, VoidBody), 12, 20, 0, 10, groundRadius: radius * 1.1f, pull: 280,
                        spin: 200, delay: spell.Duration / 2f);
                    // When it lets go, the held vapour bursts back out.
                    Vapour(point, radius * .9f, VoidBody, .6f, spell.Duration);
                    SpellShake(.25f);
                }
                break;
            case "soul_harvest":
                SpellVfx.Sigil(this, point, "shadow", new Color(.5f, .78f, 1f), radius * .8f, 1.3f, .8f, draw: .2f);
                for (var i = 0; i < path.Count; i++)
                {
                    Vapour(path[i], 22, new Color(.55f, .82f, 1f), .7f, .1f + i * .12f);
                    var wisp = SpellVfx.Sprite(this, path[i] + new Vector2(0, -16), SpellVfx.Tex("p_wisp"), new Color(.65f, .88f, 1f), Sz(30), Sz(40), 1.1f, delay: .1f + i * .12f);
                    if (wisp != null) { wisp.Velocity = new Vector2(-30, -120); wisp.Gravity = new Vector2(-90, -120); wisp.Envelope(.1f, .4f); }
                }
                SpellVfx.Burst(this, point, "p_orb", 12, 1f, SpellVfx.Fade(new Color(.8f, .95f, 1f), new Color(.5f, .78f, 1f)), 5, 10, 20, 50,
                    gravity: -60, groundRadius: radius * .5f, explosiveness: .5f, delay: .1f);
                break;
            case "consecrated_ground":
                SpellVfx.Sigil(this, point, "holy", HolyBody, radius * .9f, 1f, .4f, draw: .25f, flareAt: .25f);
                SpellVfx.Pillar(this, point, new Color(HolyHot, .8f), radius * 1.4f, 220, .9f, .15f);
                Vapour(point, radius * .8f, HolyBody, .8f, .2f);
                SpellVfx.Shockwave(this, point, HolyBody, radius, .5f, .25f);
                SpellVfx.Burst(this, point, "p_star", 12, 1f, SpellVfx.Fade(HolyHot, HolyBody), 6, 11, 20, 70, gravity: -30, groundRadius: radius * .7f, delay: .2f);
                break;
            case "verdant_renewal":
                if (ally == null) break;
                SpellVfx.Sigil(this, ally.Position, "nature", LifeBody, 44, 1.6f, .6f, draw: .25f, flareAt: .25f);
                SpellVfx.Pillar(this, ally.Position, LifeHot, 30, 120, 1f, .15f);
                Vapour(ally.Position, 32, new Color(LifeBody, .7f), .8f, .2f);
                SpellVfx.Burst(this, ally.Position, "p_leaf", 16, 1.4f, PaleRamp, 9, 13, 30, 80, spread: 40, gravity: -45, additive: false, groundRadius: 22, spin: 260, delay: .2f);
                SpellVfx.Burst(this, ally.Position, "p_orb", 18, 1.2f, SpellVfx.Fade(LifeHot, LifeBody), 5, 10, 20, 60, spread: 30, gravity: -55, groundRadius: 24, delay: .2f);
                break;
            case "bramble_snare":
                SpellVfx.Sigil(this, point, "nature", LifeDeep, radius * .9f, spell.Duration + .4f, .2f, draw: .2f);
                SpellVfx.Decal(this, point, "moss", new Color(.85f, .9f, .8f), radius * .9f, spell.Duration + 1.5f);
                foreach (var unit in affected)
                    SpellVfx.Rise(this, unit.Position + new Vector2(0, 3), "bramble", Mathf.Max(56f, unit.Radius * 3.6f), spell.Duration + .4f,
                        .1f + (float)GD.RandRange(0, .1), GD.Randf() < .5f, emerge: .22f);
                DustRing(point, radius * .9f, new Color(.55f, .52f, .38f, .7f), .1f, .9f);
                SpellVfx.Burst(this, point, "p_leaf", 10, 1f, PaleRamp, 7, 11, 60, 140, spread: 70, gravity: 200, additive: false, groundRadius: radius * .5f, spin: 300, delay: .1f);
                SpellVfx.Burst(this, point, "p_vine", 6, .8f, PaleRamp, 14, 22, 60, 120, spread: 60, gravity: 260, additive: false, groundRadius: radius * .5f, spin: 200, delay: .1f);
                break;
            case "chain_lightning":
                if (path.Count == 0) break;
                {
                    const float strike = .12f;
                    var sky = StormCloudVfx(path[0], 50);
                    SpellVfx.Bolt(this, sky, path[0], StormBody, .34f, 3.2f, strike);
                    SpellVfx.Flash(this, path[0], StormHot, 50, .3f, delay: strike);
                    SpellVfx.Sigil(this, point, "storm", StormDeep, 40, .9f, 1.6f, draw: .12f, flareAt: strike);
                    Vapour(path[0] + new Vector2(0, 20), 26, StormBody, .45f, strike);
                    for (var i = 1; i < path.Count; i++)
                    {
                        var at = strike + .07f * i;
                        SpellVfx.Bolt(this, path[i - 1], path[i], StormBody, .3f, 2.6f - i * .3f, at);
                        SpellVfx.Flash(this, path[i], StormHot, 40, .3f, delay: at);
                        Vapour(path[i] + new Vector2(0, 20), 20, StormBody, .4f, at);
                        SpellVfx.Burst(this, path[i], "p_ember", 8, .5f, SpellVfx.Fade(StormHot, StormBody), 3, 6, 100, 200, gravity: 300, delay: at);
                    }
                    SpellVfx.Burst(this, path[0], "p_spark_bolt", 6, .25f, SpellVfx.Fade(StormHot, StormBody), 10, 18, 60, 140, delay: strike);
                    SpellShake(.35f, strike);
                }
                break;
            case "rallying_advance":
                SpellVfx.Sigil(this, point, "war", TealBody, radius * .9f, 1.3f, .5f, draw: .2f, flareAt: .2f);
                SpellVfx.Shockwave(this, point, GoldBody, radius, .5f, .2f);
                {
                    var pennant = SpellPicture("spell_rallying_advance", point + new Vector2(0, -70), 30, 70, 1.1f);
                    if (pennant != null) { pennant.Velocity = new Vector2(40, -20); pennant.Envelope(.08f, .4f); }
                }
                foreach (var unit in affected)
                {
                    // They kick off: a scuff of dust behind each, a gold streak forward.
                    Puff(unit.Position + new Vector2(-12, 0), 16, .8f, new Color(Dust, .7f), .2f);
                    var streak = SpellVfx.Sprite(this, Chest(unit) + new Vector2(-34, 6), SpellVfx.Streak, new Color(GoldBody, .8f), new Vector2(70, 12), new Vector2(60, 8), .5f, delay: .2f);
                    if (streak != null) streak.Velocity = new Vector2(120, 0);
                    SpellVfx.Burst(this, Chest(unit), "p_star", 4, .5f, SpellVfx.Fade(HolyHot, GoldBody), 7, 12, 30, 80, delay: .2f);
                }
                break;
            case "revealing_flare":
                {
                    const float climb = .22f, hang = 1.9f;
                    var sky = point + new Vector2(0, -105);
                    var flare = SpellVfx.Sprite(this, point, SpellVfx.Streak, HolyHot, new Vector2(70, 14), new Vector2(90, 10), climb);
                    if (flare != null) { flare.Angle = -Mathf.Pi / 2; flare.Pivot = new Vector2(.92f, .5f); flare.Velocity = (sky - point) / climb; flare.Envelope(0, 0); }
                    // Its smoke trail hangs where it climbed.
                    for (var i = 0; i < 4; i++) Puff(point.Lerp(sky, i / 4f) + new Vector2(0, 14), 10 + i * 2, 1.2f, new Color(.9f, .88f, .84f, .55f), climb * i / 4f, true);
                    SpellVfx.Burst(this, point, "p_ember", 10, .4f, SpellVfx.Fade(HolyHot, FireBody), 3, 6, 40, 90, spread: 30, gravity: 200);
                    // The burst, then the flare itself hanging in the air, flickering as it sinks.
                    SpellVfx.Flash(this, sky, HolyHot, 120, .45f, delay: climb);
                    Vapour(sky + new Vector2(0, 30), 50, HolyHot, .7f, climb);
                    var star = SpellVfx.Sprite(this, sky, SpellVfx.Tex("p_star"), HolyHot, Sz(56), Sz(34), hang, delay: climb);
                    if (star != null) { star.Velocity = new Vector2(6, 26); star.Spin = 1.2f; star.Flicker = .3f; star.Envelope(.04f, .35f); }
                    var halo = SpellVfx.Sprite(this, sky, SpellVfx.Glow, new Color(HolyHot, .7f), Sz(130), Sz(90), hang, delay: climb);
                    if (halo != null) { halo.Velocity = new Vector2(6, 26); halo.Flicker = .2f; halo.Envelope(.04f, .35f); }
                    // Its light falls on the area: a soft cone and a lit circle on the road.
                    SpellVfx.Pillar(this, point, new Color(HolyHot, .7f), radius * 2f, 110, hang, climb);
                    SpellVfx.Flash(this, point, HolyBody, radius * 1.1f, hang, true, climb);
                    SpellVfx.Sigil(this, point, "holy", HolyBody, radius, hang, .4f, climb, flareAt: 0f);
                    SpellVfx.Burst(this, sky, "p_star", 30, 1.7f, SpellVfx.Fade(HolyHot, HolyBody), 5, 10, 40, 150, gravity: 80, delay: climb, damping: 40);
                    // Every hidden thing it finds is picked out in light.
                    foreach (var unit in affected)
                    {
                        var outline = SpellVfx.Sprite(this, Chest(unit) + new Vector2(0, 8), SpellVfx.Ring, new Color(HolyHot, .6f), Sz(unit.Radius * 2.6f),
                            Sz(unit.Radius * 3.6f), .6f, delay: climb + .05f);
                        outline?.Envelope(.1f, .6f);
                        SpellVfx.Flash(this, Chest(unit), HolyHot, 24, .4f, delay: climb + .05f);
                    }
                }
                break;
            case "mirror_ward":
                SpellVfx.Sigil(this, point, "arcane", new Color(.85f, .95f, 1f), radius * .8f, 1.2f, -.6f, draw: .2f, flareAt: .2f);
                Vapour(point, radius * .6f, new Color(.85f, .95f, 1f, .7f), .8f, .15f);
                foreach (var unit in affected)
                {
                    WardBubble(unit, new Color(.85f, .95f, 1f), 1f, .2f);
                    var glint = SpellVfx.Sprite(this, Chest(unit) + new Vector2(10, -6), SpellVfx.Tex("p_star"), Colors.White, Sz(20), Sz(44), .7f, delay: .35f);
                    if (glint != null) glint.Spin = 3f;
                }
                break;
            case "banish":
                if (target == null) break;
                {
                    const float open = .5f;
                    var at = Chest(target) + new Vector2(0, 6);
                    var tall = Mathf.Max(100f, target.Radius * 5f);
                    SpellVfx.Sigil(this, target.Position, "shadow", VoidBody, 50, open + .8f, 3f, draw: .2f, flareAt: open);
                    // The rift: a dark slit with a burning violet rim that widens, then snaps shut.
                    var dark = SpellVfx.Sprite(this, at, SpellVfx.Glow, new Color(.08f, .02f, .14f, .95f), new Vector2(6, tall * .8f), new Vector2(54, tall), open,
                        SpellVfx.AirLayer, false);
                    if (dark != null) { dark.SizeEase = 3f; dark.Envelope(.1f, 0); }
                    var rim = SpellVfx.Sprite(this, at, SpellVfx.Ring, VoidHot, new Vector2(8, tall * .85f), new Vector2(62, tall * 1.06f), open);
                    if (rim != null) { rim.SizeEase = 3f; rim.Flicker = .2f; rim.Envelope(.1f, 0); }
                    var shutDark = SpellVfx.Sprite(this, at, SpellVfx.Glow, new Color(.08f, .02f, .14f, .95f), new Vector2(54, tall), new Vector2(2, tall * 1.1f), .16f,
                        SpellVfx.AirLayer, false, delay: open);
                    shutDark?.Envelope(0, .3f);
                    var shutRim = SpellVfx.Sprite(this, at, SpellVfx.Ring, VoidHot, new Vector2(62, tall * 1.06f), new Vector2(3, tall * 1.2f), .16f, delay: open);
                    shutRim?.Envelope(0, .3f);
                    // Vapour, wisps and shadow stream into it from all around.
                    Vapour(target.Position, 46, VoidBody, open, gather: true);
                    SpellVfx.Burst(this, at, "p_wisp", 16, .5f, SpellVfx.Fade(VoidHot, VoidBody), 14, 24, 0, 20, groundRadius: 70, pull: 700, spin: 300, explosiveness: .5f);
                    SpellVfx.Burst(this, at, "p_shadow", 8, .5f, new[] { new Color(.2f, .1f, .3f, 0), new Color(.2f, .1f, .3f, .8f), new Color(.2f, .1f, .3f, 0) },
                        18, 30, 0, 20, additive: false, groundRadius: 60, pull: 600, spin: 200, explosiveness: .6f);
                    // Shut: a flash, a line of light where it was, and a ring rolling out.
                    SpellVfx.Flash(this, at, VoidHot, 70, .35f, delay: open);
                    var seam = SpellVfx.Sprite(this, at, SpellVfx.Beam, VoidHot, new Vector2(10, tall * 1.3f), new Vector2(2, tall * 1.5f), .3f, delay: open + .05f);
                    if (seam != null) seam.Pivot = new Vector2(.5f, .5f);
                    SpellVfx.Shockwave(this, target.Position, VoidBody, 70, .5f, open);
                    DustRing(target.Position, 60, new Color(Gloom, .7f), open, .8f);
                    SpellVfx.Burst(this, at, "p_star", 12, .5f, SpellVfx.Fade(VoidHot, VoidBody), 6, 11, 80, 180, delay: open);
                    SpellShake(.35f, open);
                }
                break;
            case "royal_edict":
                SpellVfx.Sigil(this, point, "war", GoldBody, radius, 1.4f, .3f, draw: .25f, flareAt: .25f);
                SpellVfx.Shockwave(this, point, GoldBody, radius * 1.1f, .5f, .25f);
                Vapour(point, radius * .7f, new Color(GoldBody, .6f), .8f, .25f);
                {
                    var seal = SpellPicture("spell_royal_edict", point + new Vector2(0, -80), 30, 84, 1.3f);
                    seal?.Envelope(.08f, .4f);
                }
                foreach (var unit in affected)
                {
                    var glyph = SpellVfx.Sprite(this, Chest(unit) + new Vector2(0, -26), SpellVfx.Tex("p_rune"), GoldBody, Sz(30), Sz(22), Mathf.Min(2.5f, spell.Duration), delay: .3f);
                    if (glyph != null) glyph.Flicker = .2f;
                    SpellVfx.Flash(this, Chest(unit), GoldBody, 28, .4f, delay: .3f);
                }
                break;
            case "tactic_smoke_pot":
                // The pot cracks and a bank of smoke rolls out over the troops.
                for (var i = 0; i < 4; i++)
                {
                    var offset = new Vector2((i - 1.5f) * radius * .35f, (i % 2 == 0 ? -1 : 1) * radius * .12f);
                    Puff(point + offset, radius * .55f, 2.6f - i * .2f, new Color(.74f, .74f, .72f, .78f), i * .12f, true)?.Envelope(.04f, .4f);
                }
                DustRing(point, radius * .9f, new Color(.7f, .7f, .68f, .7f), 0, 1f);
                break;
            case "tactic_war_horn":
                SoundRings(point + new Vector2(0, -30), new Color(MarkRed, .45f), 2, 150);
                foreach (var unit in affected)
                {
                    SpellVfx.Sigil(this, unit.Position, "war", MarkRed, 30, 1.4f, 1f, draw: .15f, flareAt: .15f);
                    SpellVfx.Flash(this, Chest(unit), MarkRed, 30, .45f, delay: .1f);
                }
                break;
            default:
                if (spell.Definition.CardKind == "tactical") TacticPlacedVfx(spell.EffectType, point);
                break;
        }
    }

    /// <summary>A tactic set down on the road: a puff of dust and a small mark of what it is.</summary>
    private void TacticPlacedVfx(string effect, Vector2 at)
    {
        Puff(at, 24, 1f, Dust);
        DustRing(at, 34, new Color(Dust, .7f), 0, .8f);
        SpellVfx.Shockwave(this, at, EarthBody, 30, .35f);
        switch (effect)
        {
            case "tactic_palisade_kit": SpellVfx.Burst(this, at + new Vector2(0, -14), "p_splinter", 8, .7f, PaleRamp, 8, 12, 80, 160, 60, null, 500, false, spin: 400); break;
            case "tactic_healing_shrine": SpellVfx.Sigil(this, at, "nature", LifeBody, 34, 1.2f, .4f); break;
            case "tactic_frost_trap": SpellVfx.Decal(this, at, "frost", Colors.White, 26, 8f); break;
            case "tactic_decoy_lantern": SpellVfx.Flash(this, at + new Vector2(0, -26), FireHot, 40, .6f); break;
            case "tactic_purifying_censer": SpellVfx.Sigil(this, at, "holy", FrostBody, 34, 1.2f, .4f); break;
            case "tactic_grounding_rod": SpellVfx.Bolt(this, at + new Vector2(0, -260), at + new Vector2(0, -44), StormBody, .2f, 2f); break;
        }
    }

    /// <summary>A placed tactic doing its work: a mine going off, a shrine pulsing, a rod drawing a spell.</summary>
    private void TacticPulseVfx(string effect, Unit obj, Unit other = null)
    {
        var at = obj.Position;
        switch (effect)
        {
            case "tactic_firepot_mine": FireImpactVfx(at, 42f, 0, .5f); AudioDirector.Instance?.PlaySpellCast("fireball", at); break;
            case "tactic_frost_trap":
                FrostVfx(at, 34f, other == null ? System.Array.Empty<Unit>() : new[] { other }, .2f);
                AudioDirector.Instance?.PlaySpellCast("frost_burst", at);
                break;
            case "tactic_healing_shrine":
                SpellVfx.Shockwave(this, at, LifeBody, 58, .6f);
                Vapour(at, 26, new Color(LifeBody, .6f), .7f);
                SpellVfx.Burst(this, at + new Vector2(0, -24), "p_orb", 8, 1f, SpellVfx.Fade(LifeHot, LifeBody), 5, 9, 20, 50, spread: 40, gravity: -50);
                break;
            case "tactic_purifying_censer":
                SpellVfx.Shockwave(this, at, FrostBody, 58, .6f);
                Puff(at + new Vector2(0, -20), 22, 1.5f, new Color(.95f, .95f, .92f, .6f), over: true);
                break;
            case "tactic_repair_cache":
                SpellVfx.Burst(this, PlayerBaseCorePosition + new Vector2(0, -30), "p_ember", 10, .6f, SpellVfx.Fade(HolyHot, GoldBody), 3, 6, 80, 180, gravity: 300);
                SpellVfx.Flash(this, PlayerBaseCorePosition + new Vector2(0, -30), GoldBody, 40, .4f);
                break;
            case "tactic_supply_crate":
                SpellVfx.Burst(this, at + new Vector2(0, -24), "p_star", 6, .7f, SpellVfx.Fade(HolyHot, GoldBody), 7, 12, 30, 70);
                break;
            case "tactic_grounding_rod":
                if (other == null) break;
                SpellVfx.Bolt(this, Chest(other), at + new Vector2(0, -44), StormBody, .3f, 2.2f);
                SpellVfx.Flash(this, at + new Vector2(0, -44), StormHot, 34, .3f);
                Vapour(at + new Vector2(0, -10), 18, StormBody, .4f);
                break;
        }
    }
}
