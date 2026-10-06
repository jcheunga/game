"""Battle foley: weapons, impacts, projectiles, deaths, the war wagon and the gatehouse."""
import numpy as np

from ak import dsp, voice as V
from ak import foley as F
from sfx import cue
from sfx.kit import chord, mix, note, s, space

POS = dict(positional=True)


def _m(x):
    return dsp.to_mono(x)


# ---------------------------------------------------------------- swings
@cue("swing_light", 4, level="swing", cooldown=0.03, voices=6, pitch=0.05, label="sword swinging through the air", **POS)
def swing_light(rng, k):
    return mix((F.blade_swish(rng.uniform(0.22, 0.3), rng), 0, 1.0), (s("k_cloth", 0.4, detune=-2, seconds=0.2, rng=rng), 0, 0.25))


@cue("swing_heavy", 4, level="swing", cooldown=0.04, voices=5, pitch=0.05, label="heavy axe swinging", **POS)
def swing_heavy(rng, k):
    return mix((F.heavy_swish(rng.uniform(0.36, 0.46), rng), 0, 1.0),
               (F.armor_rattle(0.3, rng, 0.6), 0.0, 0.25))


@cue("thrust", 3, level="swing", cooldown=0.03, voices=5, pitch=0.05, label="spear thrust whoosh", **POS)
def thrust(rng, k):
    return mix((F.whoosh(0.17, 900, 3200, q=1.4, peak=0.7, rng=rng), 0, 1.0),
               (s("k_cloth", 0.5, detune=2, seconds=0.12, rng=rng), 0, 0.3))


@cue("swing_dagger", 3, level="swing", cooldown=0.03, voices=4, pitch=0.05, label="quick knife swipes", **POS)
def swing_dagger(rng, k):
    a = F.blade_swish(0.13, rng, ring=0.3)
    b = F.blade_swish(0.12, rng, ring=0.3)
    return mix((a, 0, 0.9), (b, rng.uniform(0.09, 0.12), 0.8))


@cue("swing_claw", 3, level="swing", cooldown=0.03, voices=5, pitch=0.06, label="claw swipe", **POS)
def swing_claw(rng, k):
    w = F.whoosh(0.2, 600, 2400, q=1.0, peak=0.6, rng=rng)
    rag = 0.6 + 0.4 * (rng.random(len(w)) > 0.5)
    return dsp.lowpass(w * rag, 5000, 2)


# ---------------------------------------------------------------- hits
@cue("hit_blade", 5, level="hit", cooldown=0.025, voices=6, pitch=0.05, label="sword hitting armor", **POS)
def hit_blade(rng, k):
    slice_ = s("k_knife_slice", 0.8, detune=rng.uniform(-6, -2), seconds=0.4, rng=rng)
    clash = F.blade_clash(rng, 0.4)
    body = F.flesh_hit(0.8, rng)
    return mix((body, 0, 0.8), (slice_, 0, 0.6), (clash, 0.003, 0.3))


@cue("hit_blunt", 4, level="hit", cooldown=0.025, voices=6, pitch=0.05, label="club hitting a body", **POS)
def hit_blunt(rng, k):
    body = F.flesh_hit(1.25, rng)
    crack = s("k_wood_light", 0.7, detune=rng.uniform(-6, -3), seconds=0.25, rng=rng)
    crunch = F.crunch(0.08, 600, 700, 3500, rng=rng, decay=0.03)
    return mix((body, 0, 1.0), (crack, 0, 0.45), (crunch, 0.004, 0.3))


@cue("hit_pierce", 4, level="hit", cooldown=0.025, voices=6, pitch=0.05, label="spear stab", **POS)
def hit_pierce(rng, k):
    stab = s("k_knife_slice", 0.8, detune=rng.uniform(-9, -5), seconds=0.3, rng=rng)
    body = F.flesh_hit(0.9, rng)
    return mix((body, 0, 0.8), (stab, 0, 0.7), (s("k_plate_light", 0.4, detune=-3, seconds=0.2, rng=rng), 0, 0.2))


@cue("hit_claw", 4, level="hit", cooldown=0.025, voices=6, pitch=0.06, label="claws tearing", **POS)
def hit_claw(rng, k):
    rip = s("k_cloth", 0.9, detune=rng.uniform(3, 6), seconds=0.2, rng=rng)
    tear = F.crunch(0.14, 900, 900, 5000, rng=rng, decay=0.05)
    body = F.flesh_hit(0.7, rng)
    return mix((body, 0, 0.7), (rip, 0, 0.6), (tear, 0.005, 0.5))


@cue("hit_bite", 3, level="hit", cooldown=0.04, voices=4, pitch=0.06, label="dog biting and snarling", **POS)
def hit_bite(rng, k):
    snap = s("woodblock", 0.6, detune=rng.uniform(-4, 0), seconds=0.06, rng=rng)
    body = F.flesh_hit(0.7, rng)
    snarl = V.creature(0.28, [(0, 160), (0.5, 220), (1, 140)], [(0, "aw"), (1, "uh")], scale=1.25, jitter=0.08,
                       shimmer=0.3, breath=0.5, roughness=0.8, rough_rate=34, drive=3.0, attack=0.01, release=0.1, rng=rng)
    return mix((snap, 0, 0.6), (body, 0.005, 0.7), (snarl, 0.02, 0.35))


@cue("hit_shield", 4, level="hit_heavy", cooldown=0.04, voices=4, pitch=0.04, label="weapon striking a shield", **POS)
def hit_shield(rng, k):
    wood = s("k_wood_heavy", 0.85, detune=rng.uniform(-3, 0), seconds=0.5, rng=rng)
    rim = s("k_plate", 0.7, detune=rng.uniform(-4, -1), seconds=0.5, rng=rng)
    boom = F.thump(rng.uniform(70, 95), 0.25, drop=0.3, click=0.0, rng=rng)
    return mix((wood, 0, 0.9), (rim, 0, 0.5), (boom, 0, 0.4))


@cue("hit_bone", 4, level="hit", cooldown=0.04, voices=4, pitch=0.06, label="bones cracking", **POS)
def hit_bone(rng, k):
    crack = F.crunch(0.07, 1400, 900, 5000, rng=rng, decay=0.025)
    clatter = F.bone_rattle(0.25, 70, rng, pitch=1.1)
    knock = s("claves", 0.6, detune=rng.uniform(-3, 2), seconds=0.08, rng=rng)
    return mix((crack, 0, 0.8), (knock, 0, 0.5), (clatter, 0.02, 0.45))


@cue("hit_heavy", 3, level="hit_heavy", cooldown=0.05, voices=4, pitch=0.04, label="giant smashing the ground", **POS)
def hit_heavy(rng, k):
    boom = F.thump(rng.uniform(42, 55), 0.5, drop=0.6, click=0.4, rng=rng)
    rock = F.stone_hit(1.3, rng)
    body = F.flesh_hit(1.4, rng)
    return mix((boom, 0, 1.0), (rock, 0, 0.6), (body, 0, 0.5))


@cue("hit_lance", 2, level="hit_heavy", cooldown=0.06, voices=3, pitch=0.04, label="lance impact", **POS)
def hit_lance(rng, k):
    return mix((F.flesh_hit(1.3, rng), 0, 0.9), (F.splinter(0.25, rng), 0.005, 0.5),
               (s("k_plate_heavy", 0.7, detune=-2, seconds=0.4, rng=rng), 0, 0.4))


@cue("shield_block", 3, level="hit", cooldown=0.05, voices=4, pitch=0.04, label="arrow blocked by a shield", **POS)
def shield_block(rng, k):
    return mix((s("k_wood_heavy", 0.8, detune=rng.uniform(-1, 2), seconds=0.4, rng=rng), 0, 0.9),
               (s("k_plate_light", 0.7, detune=rng.uniform(0, 3), seconds=0.4, rng=rng), 0, 0.45))


# ---------------------------------------------------------------- launches
@cue("bow_release", 4, level="launch", cooldown=0.03, voices=6, pitch=0.05, label="arrow shot from a bow", **POS)
def bow_release(rng, k):
    return F.bow_release(rng)


@cue("crossbow_release", 3, level="launch", cooldown=0.04, voices=4, pitch=0.04, label="crossbow firing", **POS)
def crossbow_release(rng, k):
    return F.crossbow_release(rng)


@cue("ballista_release", 2, level="base", cooldown=0.1, voices=3, pitch=0.03, label="siege ballista firing", **POS)
def ballista_release(rng, k):
    return F.ballista_release(rng)


@cue("harpoon_release", 2, level="base", cooldown=0.1, voices=2, pitch=0.03, label="harpoon gun firing with chain", **POS)
def harpoon_release(rng, k):
    return mix((F.ballista_release(rng), 0, 1.0), (F.chain_jingle(0.5, 160, rng, 1500, 5000), 0.05, 0.5))


@cue("bone_bolt_release", 2, level="base", cooldown=0.1, voices=3, pitch=0.04, label="ballista firing", **POS)
def bone_bolt_release(rng, k):
    return mix((F.ballista_release(rng), 0, 1.0), (F.bone_rattle(0.35, 60, rng), 0.02, 0.4))


@cue("flask_throw", 3, level="launch", cooldown=0.05, voices=3, pitch=0.05, label="bottle thrown", **POS)
def flask_throw(rng, k):
    w = F.whoosh(0.35, 700, 2200, q=1.2, peak=0.4, rng=rng)
    tumble = 0.6 + 0.4 * np.abs(np.sin(2 * np.pi * rng.uniform(9, 13) * np.arange(len(w)) / dsp.SR))
    clink = s("k_glass_light", 0.5, detune=rng.uniform(4, 8), seconds=0.15, rng=rng)
    return mix((w * tumble, 0, 1.0), (clink, 0, 0.35))


@cue("pot_throw", 2, level="launch", cooldown=0.06, voices=3, pitch=0.04, label="heavy object thrown", **POS)
def pot_throw(rng, k):
    w = F.whoosh(0.45, 250, 1000, q=0.9, peak=0.45, color="pink", rng=rng)
    tumble = 0.6 + 0.4 * np.abs(np.sin(2 * np.pi * rng.uniform(5, 7) * np.arange(len(w)) / dsp.SR))
    knock = s("k_wood_light", 0.6, detune=-6, seconds=0.15, rng=rng)
    return mix((knock, 0, 0.5), (w * tumble, 0.02, 1.2))


@cue("cog_throw", 2, level="launch", cooldown=0.05, voices=3, pitch=0.05, label="spinning metal whir", **POS)
def cog_throw(rng, k):
    n = dsp.samples(0.4)
    t = np.arange(n) / dsp.SR
    whir = dsp.bandpass(rng.standard_normal(n), 1200, 4500, 2) * (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 22 * t)))
    whir *= F.env_ar(n, 0.3, 0.7, 1.4)
    return mix((s("k_metal_click", 0.7, detune=-2, seconds=0.15, rng=rng), 0, 0.7), (whir, 0.02, 0.6))


# ---------------------------------------------------------------- projectile impacts
@cue("impact_arrow", 4, level="impact", cooldown=0.03, voices=6, pitch=0.06, label="arrow hitting a target", **POS)
def impact_arrow(rng, k):
    thunk = s("k_wood_light", 0.8, detune=rng.uniform(-8, -4), seconds=0.25, rng=rng)
    body = F.flesh_hit(0.6, rng)
    quiver = s("basses_pizz", 0.4, seconds=0.12, rng=rng) if k % 2 else np.zeros(10)
    return mix((thunk, 0, 0.8), (body, 0, 0.6), (dsp.highpass(_m(quiver), 300, 2), 0.004, 0.3))


@cue("impact_heavy_bolt", 3, level="impact_big", cooldown=0.06, voices=4, pitch=0.04, label="heavy bolt slamming into wood", **POS)
def impact_heavy_bolt(rng, k):
    return mix((F.thump(rng.uniform(55, 70), 0.35, drop=0.5, click=0.5, rng=rng), 0, 0.9),
               (s("k_wood_heavy", 0.9, detune=rng.uniform(-5, -2), seconds=0.5, rng=rng), 0, 0.8),
               (F.splinter(0.3, rng), 0.005, 0.5))


@cue("impact_glass", 3, level="impact", cooldown=0.05, voices=4, pitch=0.05, label="glass bottle shattering", **POS)
def impact_glass(rng, k):
    return mix((F.glass_break(rng, 0.7), 0, 0.9), (F.splat(0.3, rng, 0.6), 0.01, 0.4), (F.hiss(0.6, rng, 2500), 0.05, 0.25))


@cue("impact_splash", 3, level="impact", cooldown=0.05, voices=4, pitch=0.06, label="wet splat", **POS)
def impact_splash(rng, k):
    return mix((F.splat(0.45, rng, 1.2), 0, 1.0), (F.hiss(0.5, rng, 1800), 0.04, 0.2))


@cue("impact_fire", 3, level="impact_big", cooldown=0.06, voices=4, pitch=0.05, label="fire burst", **POS)
def impact_fire(rng, k):
    return mix((F.fire_whoosh(0.7, rng, 1.2), 0, 1.0), (F.thump(rng.uniform(55, 75), 0.3, drop=0.4, click=0.2, rng=rng), 0, 0.6),
               (F.glass_break(rng, 0.4), 0, 0.15 if k == 0 else 0.0))


@cue("impact_metal", 3, level="impact", cooldown=0.04, voices=4, pitch=0.05, label="metal clang", **POS)
def impact_metal(rng, k):
    return mix((s("k_metal", 0.8, detune=rng.uniform(-2, 2), seconds=0.6, rng=rng), 0, 0.9),
               (F.flesh_hit(0.6, rng), 0, 0.3))


# ---------------------------------------------------------------- deaths and falls
@cue("death_soldier", 4, level="death", cooldown=0.05, voices=4, pitch=0.04, label="armored soldier collapsing", **POS)
def death_soldier(rng, k):
    gear = F.armor_rattle(0.6, rng, 1.0)
    drop = s("k_metal_light", 0.7, detune=rng.uniform(-3, 1), seconds=0.4, rng=rng)
    breath = V.creature(0.35, [(0, 170), (1, 110)], [(0, "a"), (1, "uh")], scale=1.0, jitter=0.03, shimmer=0.1,
                        breath=0.85, drive=1.2, attack=0.02, release=0.25, rng=rng)
    return mix((breath, 0, 0.25), (gear, 0.05, 0.8), (drop, 0.3, 0.5))


@cue("death_heavy", 2, level="death", cooldown=0.08, voices=3, pitch=0.03, label="heavy armored knight collapsing", **POS)
def death_heavy(rng, k):
    return mix((F.armor_rattle(0.7, rng, 1.6), 0, 0.9), (s("k_plate_heavy", 0.8, detune=-3, seconds=0.5, rng=rng), 0.35, 0.7),
               (F.thump(55, 0.3, drop=0.3, click=0.0, rng=rng), 0.35, 0.5))


@cue("death_hound", 1, level="death", cooldown=0.2, voices=2, pitch=0.03, label="dog whimpering", **POS)
def death_hound(rng, k):
    whimper = V.creature(0.55, [(0, 900), (0.3, 1100), (1, 520)], [(0, "u"), (0.5, "i"), (1, "u")], scale=1.5, jitter=0.02,
                         shimmer=0.1, breath=0.35, drive=1.4, attack=0.03, release=0.2, rng=rng)
    return mix((whimper, 0, 0.7), (F.thump(80, 0.2, drop=0.2, click=0.0, rng=rng), 0.4, 0.5))


@cue("death_risen", 4, level="death", cooldown=0.05, voices=4, pitch=0.05, label="zombie dying into ash", **POS)
def death_risen(rng, k):
    groan = V.creature(0.8, [(0, 95), (0.5, 80), (1, 55)], [(0, "aw"), (0.6, "o"), (1, "u")], scale=0.9, jitter=0.06,
                       shimmer=0.25, subharmonic=0.4, breath=0.45, roughness=0.5, rough_rate=26, drive=2.2, attack=0.03,
                       release=0.4, rng=rng)
    ash = F.crunch(0.8, 260, 500, 3500, rng=rng, decay=0.3)
    sift = F.hiss(0.9, rng, 1200) * 0.4
    return mix((groan, 0, 0.6), (ash, 0.15, 0.5), (sift, 0.2, 0.4), (F.bone_rattle(0.3, 40, rng), 0.25, 0.35))


@cue("death_bones", 3, level="death", cooldown=0.06, voices=4, pitch=0.05, label="skeleton collapsing into bones", **POS)
def death_bones(rng, k):
    return mix((F.bone_rattle(0.9, 70, rng, 0.95), 0, 1.0), (s("k_dice_throw", 0.8, detune=-7, seconds=0.8, rng=rng), 0.08, 0.6))


@cue("death_gas", 2, level="death", cooldown=0.08, voices=3, pitch=0.04, label="bloated corpse bursting", **POS)
def death_gas(rng, k):
    pop = F.splat(0.6, rng, 1.6)
    boom = F.thump(60, 0.3, drop=0.4, click=0.3, rng=rng)
    gas = F.hiss(1.4, rng, 900) * 0.6
    return mix((boom, 0, 0.7), (pop, 0, 1.0), (gas, 0.1, 0.5), (F.bubbles(0.8, 10, rng), 0.15, 0.3))


@cue("death_embers", 2, level="death", cooldown=0.08, voices=3, pitch=0.04, label="fire going out", **POS)
def death_embers(rng, k):
    return mix((F.fire_whoosh(0.6, rng, 0.8)[::-1], 0, 0.6), (F.crackle(1.0, 50, rng), 0.3, 0.5), (F.hiss(0.8, rng, 2000), 0.5, 0.4))


@cue("death_water", 2, level="death", cooldown=0.08, voices=3, pitch=0.04, label="splash of water", **POS)
def death_water(rng, k):
    wave = s("ocean_drum", 0.7, seconds=0.9, rng=rng)
    return mix((F.splat(0.5, rng, 2.0), 0, 0.9), (wave, 0.05, 0.6), (F.bubbles(0.8, 14, rng), 0.1, 0.4))


@cue("death_debris", 2, level="death", cooldown=0.1, voices=3, pitch=0.03, label="wooden structure collapsing", **POS)
def death_debris(rng, k):
    return mix((F.splinter(0.5, rng), 0, 0.8), (s("k_creak", 0.8, detune=-5, seconds=0.6, rng=rng), 0, 0.5),
               (s("k_wood_heavy", 0.9, detune=-5, seconds=0.5, rng=rng), 0.35, 0.8),
               (s("k_wood_heavy", 0.8, detune=-7, seconds=0.5, rng=rng), 0.55, 0.6), (F.crunch(0.8, 120, 300, 2500, rng=rng, decay=0.3), 0.4, 0.4))


@cue("death_glass", 2, level="death", cooldown=0.1, voices=2, pitch=0.03, label="mirror shattering", **POS)
def death_glass(rng, k):
    return mix((F.glass_break(rng, 1.2), 0, 1.0), (s("k_glass_heavy", 0.9, detune=-3, seconds=0.8, rng=rng), 0, 0.6))


@cue("death_alchemy", 1, level="death", cooldown=0.1, voices=2, pitch=0.04, label="glass vials breaking and fizzing", **POS)
def death_alchemy(rng, k):
    return mix((F.glass_break(rng, 0.6), 0, 0.8), (F.hiss(0.8, rng, 2500), 0.05, 0.4), (F.bubbles(0.6, 10, rng), 0.05, 0.3))


@cue("body_fall_light", 3, level="fall", cooldown=0.04, voices=4, pitch=0.05, label="body falling on the ground", **POS)
def body_fall_light(rng, k):
    return mix((s("k_soft_heavy", 0.8, detune=rng.uniform(-4, -1), seconds=0.4, rng=rng), 0, 0.9),
               (s("k_cloth", 0.5, detune=-3, seconds=0.3, rng=rng), 0, 0.4), (F.crunch(0.2, 200, 400, 2500, rng=rng, decay=0.08), 0.01, 0.3))


@cue("body_fall_heavy", 3, level="fall", cooldown=0.05, voices=4, pitch=0.04, label="heavy body crashing down", **POS)
def body_fall_heavy(rng, k):
    return mix((F.thump(rng.uniform(45, 60), 0.4, drop=0.4, click=0.3, rng=rng), 0, 0.9),
               (s("k_soft_heavy", 0.9, detune=-6, seconds=0.4, rng=rng), 0, 0.8), (F.armor_rattle(0.4, rng, 1.2), 0.0, 0.4),
               (F.crunch(0.4, 200, 300, 2500, rng=rng, decay=0.15), 0.02, 0.4))


@cue("body_fall_bones", 2, level="fall", cooldown=0.05, voices=4, pitch=0.05, label="bones falling on the ground", **POS)
def body_fall_bones(rng, k):
    return mix((s("k_soft", 0.6, detune=-4, seconds=0.25, rng=rng), 0, 0.6), (F.bone_rattle(0.5, 60, rng), 0, 0.8))


# ---------------------------------------------------------------- wagon, gatehouse and the field
@cue("bus_hit", 4, level="hit_heavy", cooldown=0.08, voices=3, pitch=0.04, label="heavy blow on a wooden wagon", **POS)
def bus_hit(rng, k):
    return mix((s("k_wood_heavy", 0.9, detune=rng.uniform(-6, -3), seconds=0.6, rng=rng), 0, 1.0),
               (s("k_plate_heavy", 0.7, detune=rng.uniform(-5, -2), seconds=0.5, rng=rng), 0, 0.5),
               (F.splinter(0.25, rng), 0.004, 0.4), (F.thump(rng.uniform(50, 62), 0.35, drop=0.3, click=0.0, rng=rng), 0, 0.5))


@cue("barricade_hit", 4, level="hit_heavy", cooldown=0.08, voices=3, pitch=0.04, label="battering a stone gatehouse", **POS)
def barricade_hit(rng, k):
    return mix((F.stone_hit(1.3, rng), 0, 1.0), (s("k_wood_heavy", 0.9, detune=rng.uniform(-8, -5), seconds=0.6, rng=rng), 0, 0.6))


@cue("repair", 3, level="base", cooldown=0.12, voices=2, pitch=0.03, label="hammering nails into wood", **POS)
def repair(rng, k):
    out = np.zeros(dsp.samples(1.0))
    for j, t in enumerate((0.0, 0.22, 0.42)):
        tap = mix((s("k_wood_light", 0.8, detune=rng.uniform(-1, 2), seconds=0.2, rng=rng), 0, 0.8),
                  (s("k_metal_light", 0.6, detune=rng.uniform(5, 9), seconds=0.15, rng=rng), 0, 0.35))
        dsp.place(out, _m(tap), t, 0.8 + 0.1 * j)
    return mix((out, 0, 1.0), (F.ratchet(0.35, rng), 0.6, 0.35))


@cue("wagon_door_open", 1, level="base", cooldown=0.4, voices=1, pitch=0.02, label="heavy wooden door creaking open", **POS)
def wagon_door_open(rng, k):
    return mix((s("k_door_open", 0.9, detune=-5, seconds=1.2, rng=rng), 0, 0.9), (F.creak_sound(0.9, 0.7, rng), 0.05, 0.5),
               (F.chain_jingle(0.6, 100, rng, 1800, 5000), 0.1, 0.35))


@cue("wagon_door_close", 1, level="base", cooldown=0.4, voices=1, pitch=0.02, label="heavy wooden door slamming shut", **POS)
def wagon_door_close(rng, k):
    return mix((s("k_door_close", 0.9, detune=-4, seconds=0.9, rng=rng), 0, 0.9), (F.thump(60, 0.3, drop=0.3, click=0.2, rng=rng), 0.02, 0.5),
               (F.chain_jingle(0.4, 80, rng, 1800, 5000), 0.0, 0.25))


@cue("base_arrows", 2, level="base", cooldown=0.2, voices=2, pitch=0.03, label="volley of arrows", **POS)
def base_arrows(rng, k):
    out = np.zeros(dsp.samples(1.0))
    for j in range(5):
        dsp.place(out, F.bow_release(rng), rng.uniform(0, 0.25), rng.uniform(0.5, 0.9))
    return out


@cue("base_firepot", 2, level="base", cooldown=0.2, voices=2, pitch=0.03, label="catapult launching a fire pot", **POS)
def base_firepot(rng, k):
    return mix((F.creak_sound(0.35, 0.9, rng), 0, 0.5), (F.thump(55, 0.35, drop=0.4, click=0.5, rng=rng), 0.2, 0.9),
               (s("k_wood_heavy", 0.8, detune=-6, seconds=0.4, rng=rng), 0.2, 0.6), (F.fire_whoosh(0.6, rng, 0.8), 0.22, 0.6))


@cue("hazard_warning", 1, level="event", stereo=True, cooldown=1.0, voices=1, pitch=0.0, label="alarm bell ringing")
def hazard_warning(rng, k):
    out = np.zeros((dsp.samples(2.4), 2))
    for j in range(3):
        dsp.place(out, note("tubular_bells", 72, 0.75, 0.3, release=0.6, rng=rng), j * 0.32)
    return space(out, "hall", 0.25)


@cue("hazard_strike", 1, level="impact_big", cooldown=0.3, voices=2, pitch=0.03, label="dark magic impact", **POS)
def hazard_strike(rng, k):
    return mix((F.thump(48, 0.5, drop=0.6, click=0.4, rng=rng), 0, 1.0), (F.hiss(0.8, rng, 1500), 0.0, 0.4),
               (V.whisper(0.8, [(0, "a"), (1, "u")], rng=rng), 0.05, 0.3))


@cue("explosion", 3, level="impact_big", cooldown=0.08, voices=3, pitch=0.04, label="explosion", **POS)
def explosion(rng, k):
    return mix((F.explosion(rng.uniform(0.9, 1.2), rng), 0, 1.0), (F.splinter(0.3, rng), 0.03, 0.3))


@cue("dig_burrow", 2, level="impact", cooldown=0.2, voices=2, pitch=0.04, label="digging in dirt", **POS)
def dig_burrow(rng, k):
    return F.dig(0.9, rng)


@cue("dig_emerge", 2, level="impact", cooldown=0.2, voices=2, pitch=0.04, label="creature bursting out of the ground", **POS)
def dig_emerge(rng, k):
    burst = dsp.lowpass(F.explosion(0.5, rng), 1800, 2)
    return mix((burst, 0, 0.8), (F.dig(0.5, rng), 0.05, 0.7), (F.crunch(0.6, 200, 300, 2500, rng=rng, decay=0.25), 0.1, 0.4))


@cue("tower_open", 1, level="base", cooldown=0.5, voices=1, pitch=0.02, label="siege tower ramp dropping", **POS)
def tower_open(rng, k):
    return mix((F.creak_sound(0.8, 0.6, rng), 0, 0.7), (s("k_wood_heavy", 0.95, detune=-8, seconds=0.6, rng=rng), 0.6, 1.0),
               (F.thump(48, 0.4, drop=0.4, click=0.3, rng=rng), 0.6, 0.7), (F.chain_jingle(0.6, 120, rng, 1500, 5000), 0.1, 0.4))


@cue("reflect", 2, level="impact", cooldown=0.08, voices=3, pitch=0.04, label="glass ringing", **POS)
def reflect(rng, k):
    ring = note("wine_glass", 81 + 2 * k, 0.6, 0.15, release=0.5, rng=rng)
    return mix((s("k_glass_light", 0.7, detune=rng.uniform(3, 6), seconds=0.3, rng=rng), 0, 0.7), (_m(ring), 0, 0.5),
               (F.whoosh(0.2, 2000, 5000, q=1.5, peak=0.3, rng=rng), 0.02, 0.3))


# ---------------------------------------------------------------- deploys
@cue("deploy_infantry", 3, level="deploy", cooldown=0.08, voices=3, pitch=0.04, label="soldier marching with armor", **POS)
def deploy_infantry(rng, k):
    return mix((F.footsteps(2, 0.28, "dirt", 1.0, rng), 0, 0.7), (F.armor_rattle(0.5, rng, 1.0), 0.02, 0.8),
               (s("k_draw_knife", 0.7, detune=-4, seconds=0.5, rng=rng) if k == 0 else np.zeros(10), 0.2, 0.5))


@cue("deploy_heavy", 2, level="deploy", cooldown=0.1, voices=3, pitch=0.03, label="heavy armored knight stomping", **POS)
def deploy_heavy(rng, k):
    return mix((F.footsteps(2, 0.36, "dirt", 1.6, rng), 0, 0.9), (F.armor_rattle(0.6, rng, 1.6), 0.0, 0.8),
               (s("k_wood_heavy", 0.6, detune=-5, seconds=0.3, rng=rng), 0.5, 0.4))


@cue("deploy_archer", 2, level="deploy", cooldown=0.08, voices=3, pitch=0.04, label="arrows rattling in a quiver", **POS)
def deploy_archer(rng, k):
    quiver = F.bone_rattle(0.35, 60, rng, 1.4)
    return mix((F.footsteps(2, 0.26, "dirt", 0.9, rng), 0, 0.6), (quiver, 0.05, 0.6), (s("k_cloth", 0.6, seconds=0.3, rng=rng), 0, 0.5),
               (s("k_creak", 0.4, detune=8, seconds=0.25, rng=rng), 0.3, 0.3))


@cue("deploy_cavalry", 2, level="deploy", cooldown=0.15, voices=2, pitch=0.03, label="horse galloping", **POS)
def deploy_cavalry(rng, k):
    return mix((F.gallop(0.9, 2.6, rng), 0, 1.0), (F.armor_rattle(0.6, rng, 1.0), 0.05, 0.5))


@cue("deploy_hound", 2, level="deploy", cooldown=0.1, voices=2, pitch=0.04, label="dog barking", **POS)
def deploy_hound(rng, k):
    from sfx.creatures import bark
    paws = np.zeros(dsp.samples(0.6))
    for j in range(4):
        dsp.place(paws, s("k_step_grass", 0.4, detune=8, seconds=0.12, rng=rng), j * 0.11, 0.4)
    return mix((bark(rng, 2), 0, 1.0), (paws, 0.0, 0.5))


@cue("deploy_caster", 2, level="deploy", cooldown=0.1, voices=2, pitch=0.03, label="robes and a magic hum", **POS)
def deploy_caster(rng, k):
    sh = F.shimmer(0.4, (72, 76, 79) if k == 0 else (74, 77, 81), rng=rng, spread=0.0)
    return mix((s("k_cloth", 0.7, seconds=0.4, rng=rng), 0, 0.7), (_m(sh), 0.05, 0.5), (F.footsteps(2, 0.3, "dirt", 0.9, rng), 0, 0.4))


@cue("deploy_holy", 1, level="deploy", cooldown=0.1, voices=2, pitch=0.02, label="robes and a soft bell", **POS)
def deploy_holy(rng, k):
    return mix((s("k_cloth", 0.7, seconds=0.4, rng=rng), 0, 0.7), (_m(note("hand_chimes", 79, 0.4, 0.2, release=0.8, rng=rng)), 0.05, 0.5),
               (F.footsteps(2, 0.3, "dirt", 0.9, rng), 0, 0.4))


@cue("deploy_engine", 2, level="deploy", cooldown=0.12, voices=2, pitch=0.03, label="wooden cart creaking", **POS)
def deploy_engine(rng, k):
    return mix((F.creak_sound(0.7, 0.8, rng), 0, 0.7), (s("k_wood", 0.7, detune=-4, seconds=0.3, rng=rng), 0.1, 0.6),
               (s("k_wood", 0.6, detune=-6, seconds=0.3, rng=rng), 0.35, 0.5), (F.chain_jingle(0.4, 80, rng, 1500, 4000), 0.2, 0.3))


@cue("deploy_rogue", 1, level="deploy", cooldown=0.1, voices=2, pitch=0.04, label="dagger drawn from a sheath", **POS)
def deploy_rogue(rng, k):
    return mix((s("k_cloth", 0.6, detune=2, seconds=0.25, rng=rng), 0, 0.6), (s("k_draw_knife", 0.8, detune=1, seconds=0.5, rng=rng), 0.1, 0.9))
