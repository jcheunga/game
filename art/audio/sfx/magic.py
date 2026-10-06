"""Spells, magic projectiles, active-ability flourishes and the host's rituals."""
import numpy as np

from ak import dsp, voice as V
from ak import foley as F
from sfx import cue
from sfx.kit import chord, mix, note, s, seq, space, spread

POS = dict(positional=True)
SPELL = dict(level="spell", cooldown=0.15, voices=3, pitch=0.02, stereo=True, positional=True)


def choir(rng, freqs, dur=1.6, vowel="a", spread_amt=0.5, attack=0.25):
    """Soft sustained 'aah' voices (holy magic)."""
    out = np.zeros((dsp.samples(dur + 0.3), 2))
    for j, f in enumerate(freqs):
        for d in (0.997, 1.004):
            v = V.creature(dur, [(0, f * d), (1, f * d)], [(0, "o"), (0.3, vowel), (1, vowel)], scale=1.05 if f > 300 else 1.0,
                           jitter=0.006, shimmer=0.03, breath=0.3, drive=1.05, attack=attack, release=0.5,
                           vibrato=(rng.uniform(4.5, 5.5), 0.01), rng=rng)
            dsp.place(out, dsp.pan(v, ((j + d * 3) % len(freqs) / max(1, len(freqs) - 1) - 0.5) * 2 * spread_amt), 0)
    return dsp.lowpass(out, 6000, 2)


def hum(freq, dur, rng, bright=0.3):
    n = dsp.samples(dur)
    t = np.arange(n) / dsp.SR
    x = sum(np.sin(2 * np.pi * freq * h * t * (1 + 0.003 * np.sin(2 * np.pi * 5 * t))) / h ** (1.5 - bright) for h in range(1, 6))
    return x * F.env_ar(n, 0.3, 0.5, 1.2)


# ---------------------------------------------------------------- spells
@cue("spell_fireball", 1, label="fireball explosion", **SPELL)
def spell_fireball(rng, k):
    rush = F.fire_whoosh(0.55, rng, 0.9)
    boom = F.explosion(1.1, rng)
    roar = F.fire_whoosh(1.4, rng, 1.0)
    return space(mix((spread(rush), 0, 0.7), (spread(boom), 0.45, 1.0), (spread(roar), 0.5, 0.6),
                     (spread(F.crackle(1.6, 50, rng)), 0.6, 0.35)), "open", 0.18)


@cue("spell_heal", 1, label="magical healing chime", **SPELL)
def spell_heal(rng, k):
    harp = seq("harp", [62, 66, 69, 74, 78, 81, 86, 90], 0.05, 0.55, 0.8, rng=rng, pan_spread=0.7)
    voices = choir(rng, [294, 370, 440], 1.8)
    sparkle = F.shimmer(1.0, (90, 93, 98, 102), rng=rng, spread=0.7)
    return space(mix((harp, 0, 0.6), (voices, 0.1, 0.5), (sparkle, 0.35, 0.35)), "hall", 0.28)


@cue("spell_frost_burst", 1, label="ice shattering", **SPELL)
def spell_frost_burst(rng, k):
    wind = F.whoosh(0.6, 1500, 6000, q=0.8, peak=0.7, rng=rng)
    crack = F.ice_shatter(rng, 1.0)
    chimes = seq("glockenspiel", [98, 95, 100, 93, 97], 0.04, 0.35, 0.1, release=0.6, rng=rng, pan_spread=0.8)
    thump = F.thump(70, 0.35, drop=0.3, click=0.5, rng=rng)
    return space(mix((spread(wind), 0, 0.6), (spread(crack), 0.5, 1.0), (thump, 0.5, 0.5), (chimes, 0.55, 0.35)), "hall", 0.2)


@cue("spell_lightning_strike", 1, label="lightning strike and thunder", **SPELL)
def spell_lightning_strike(rng, k):
    charge = F.zap(0.35, rng)
    thunder = F.thunder(2.4, rng, crack=1.4)
    return space(mix((spread(charge), 0, 0.5), (spread(thunder), 0.25, 1.0), (spread(F.zap(0.25, rng)), 0.27, 0.6)), "open", 0.25)


@cue("spell_barrier_ward", 1, label="magic shield forming", **SPELL)
def spell_barrier_ward(rng, k):
    rise = F.reverse_swell(note("hand_chimes", 79, 0.6, 0.1, rng=rng), 0.6)
    dome = mix((note("wine_glass", 79, 0.6, 1.2, release=0.8, rng=rng), 0, 0.6), (spread(hum(98, 1.6, rng)), 0, 0.4))
    clang = chord("hand_chimes", [67, 74, 79], 0.6, 0.2, release=1.2, rng=rng)
    return space(mix((rise, 0, 0.6), (clang, 0.55, 0.6), (dome, 0.55, 0.6)), "hall", 0.3)


@cue("spell_stone_barricade", 1, label="stone wall rising from the earth", **SPELL)
def spell_stone_barricade(rng, k):
    rumble = dsp.lowpass(F.shaped_noise(1.2, color="brown", rng=rng), 200, 2) * 3 * F.env_ar(dsp.samples(1.2), 0.5, 0.5)
    slabs = mix((F.stone_hit(1.5, rng), 0.6, 1.0), (F.stone_hit(1.2, rng), 0.85, 0.7))
    debris = F.crunch(1.2, 160, 300, 3000, rng=rng, decay=0.5)
    return space(mix((spread(rumble), 0, 0.8), (spread(slabs), 0, 1.0), (spread(debris), 0.65, 0.5)), "open", 0.2)


@cue("spell_war_cry", 1, label="crowd of men shouting a battle cry", **SPELL)
def spell_war_cry(rng, k):
    crowd = V.crowd_shout(1.4, voices=16, base_f0=150, rng=rng)
    horn = note("horn", 50, 0.85, 1.0, release=0.5, rng=rng)
    drums = mix((s("big_drum", 0.9, seconds=1.0, rng=rng), 0, 0.9), (s("big_drum", 0.8, seconds=1.0, rng=rng), 0.3, 0.7))
    return space(mix((horn, 0, 0.5), (crowd, 0.15, 1.0), (spread(drums), 0.1, 0.6)), "open", 0.2)


@cue("spell_earthquake", 1, label="earthquake rumbling", **SPELL)
def spell_earthquake(rng, k):
    n = dsp.samples(2.6)
    t = np.arange(n) / dsp.SR
    rumble = dsp.lowpass(F.shaped_noise(2.6, color="brown", rng=rng), 140, 2)
    rumble *= (0.6 + 0.4 * np.abs(np.sin(2 * np.pi * 3.5 * t))) * F.env_ar(n, 0.2, 0.6, 1.0) * 3
    cracks = mix((F.stone_hit(1.6, rng), 0.3, 1.0), (F.stone_hit(1.3, rng), 0.9, 0.8), (F.splinter(0.5, rng), 1.2, 0.4))
    return space(mix((spread(rumble), 0, 1.0), (spread(cracks), 0, 0.8), (spread(F.crunch(2.0, 120, 300, 2500, rng=rng, decay=1.0)), 0.3, 0.5)), "open", 0.2)


@cue("spell_polymorph", 1, label="magical poof", **SPELL)
def spell_polymorph(rng, k):
    gliss = seq("glockenspiel", [84, 88, 91, 96, 100, 103], 0.03, 0.4, 0.1, release=0.5, rng=rng, pan_spread=0.6)
    puff = F.whoosh(0.4, 400, 2000, q=0.7, peak=0.2, color="pink", rng=rng)
    pop = s("woodblock", 0.8, detune=7, seconds=0.1, rng=rng)
    return space(mix((gliss, 0, 0.5), (pop, 0.2, 0.6), (spread(puff), 0.2, 0.8), (F.shimmer(0.6, (93, 98, 100), rng=rng), 0.3, 0.3)), "hall", 0.2)


@cue("spell_resurrect", 1, label="holy choir and bells", **SPELL)
def spell_resurrect(rng, k):
    voices = choir(rng, [220, 277, 330, 440], 2.0, attack=0.6)
    bell = note("tubular_bells", 69, 0.7, 0.5, release=2.0, rng=rng)
    rise = F.reverse_swell(note("hand_chimes", 81, 0.6, 0.1, rng=rng), 0.8)
    return space(mix((rise, 0, 0.5), (voices, 0.3, 0.8), (bell, 0.8, 0.5), (F.shimmer(1.0, (88, 93, 96), rng=rng), 0.9, 0.3)), "cathedral", 0.3)


@cue("spell_denied", 1, level="ui", bus="Interface", stereo=True, cooldown=0.3, voices=1, pitch=0.0, label="magic fizzling out")
def spell_denied(rng, k):
    sputter = mix((F.crackle(0.3, 60, rng), 0, 0.6), (F.hiss(0.35, rng, 2000), 0, 0.4), (s("woodblock", 0.4, detune=-8, seconds=0.1, rng=rng), 0.05, 0.5))
    return space(sputter, "room", 0.08)


# ---------------------------------------------------------------- magic projectiles
LAUNCH = dict(level="launch", cooldown=0.04, voices=5, pitch=0.05, **POS)
IMPACT = dict(level="impact", cooldown=0.04, voices=5, pitch=0.05, **POS)


@cue("cast_arcane", 3, label="magic bolt whoosh", **LAUNCH)
def cast_arcane(rng, k):
    n = dsp.samples(0.4)
    t = np.arange(n) / dsp.SR
    sweep = np.sin(2 * np.pi * np.cumsum(600 + 1400 * t / 0.4) / dsp.SR) * F.env_ar(n, 0.2, 0.8) * 0.3
    return mix((F.whoosh(0.4, 900, 3500, q=1.2, peak=0.3, rng=rng), 0, 0.8), (sweep, 0, 0.4),
               (dsp.to_mono(note("glockenspiel", [86, 88, 91][k], 0.4, 0.05, release=0.4, rng=rng)), 0, 0.3))


@cue("cast_holy", 2, label="bright chime whoosh", **LAUNCH)
def cast_holy(rng, k):
    return mix((F.whoosh(0.35, 1200, 4000, q=1.3, peak=0.3, rng=rng), 0, 0.7),
               (dsp.to_mono(note("hand_chimes", [84, 88][k], 0.5, 0.1, release=0.6, rng=rng)), 0, 0.5))


@cue("cast_soul", 3, label="ghostly whoosh", **LAUNCH)
def cast_soul(rng, k):
    w = F.whoosh(0.5, 300, 1400, q=1.0, peak=0.4, color="pink", rng=rng)
    ghost = V.creature(0.45, [(0, 260), (1, 200)], [(0, "u"), (1, "o")], scale=1.1, jitter=0.01, breath=0.7, drive=1.1,
                       attack=0.1, release=0.2, vibrato=(6, 0.02), rng=rng)
    return mix((w, 0, 0.8), (ghost, 0.02, 0.35), (V.whisper(0.4, rng=rng), 0.05, 0.2))


@cue("cast_hex", 2, label="eerie magic whoosh", **LAUNCH)
def cast_hex(rng, k):
    a = dsp.to_mono(note("wine_glass", 78, 0.5, 0.3, release=0.3, rng=rng))
    b = dsp.to_mono(note("wine_glass", 79, 0.5, 0.3, release=0.3, rng=rng, detune=0.35))
    return mix((F.whoosh(0.4, 600, 2400, q=1.0, peak=0.3, rng=rng), 0, 0.7), (a, 0, 0.3), (b, 0, 0.3), (V.whisper(0.35, rng=rng), 0, 0.25))


@cue("cast_storm", 2, label="electric zap", **LAUNCH)
def cast_storm(rng, k):
    return mix((F.zap(0.35, rng), 0, 1.0), (F.whoosh(0.3, 1500, 5000, q=1.5, peak=0.3, rng=rng), 0, 0.4))


@cue("cast_water", 2, label="water splash whoosh", **LAUNCH)
def cast_water(rng, k):
    return mix((F.whoosh(0.4, 500, 2200, q=0.9, peak=0.35, rng=rng), 0, 0.6), (F.splat(0.3, rng, 1.0), 0, 0.5), (F.bubbles(0.4, 8, rng), 0.05, 0.3))


@cue("cast_blight", 3, label="creature spitting", **LAUNCH)
def cast_blight(rng, k):
    spit = V.creature(0.18, [(0, 300), (1, 200)], [(0, "i"), (1, "u")], scale=1.1, jitter=0.1, breath=0.85, drive=2.0,
                      attack=0.005, release=0.05, rng=rng)
    return mix((spit, 0, 0.5), (F.splat(0.3, rng, 0.8), 0.02, 0.6), (F.whoosh(0.3, 500, 1800, q=1.0, peak=0.3, rng=rng), 0.03, 0.5))


@cue("cast_frost", 2, label="icy whoosh", **LAUNCH)
def cast_frost(rng, k):
    return mix((F.whoosh(0.35, 2000, 7000, q=1.2, peak=0.3, rng=rng), 0, 0.8),
               (dsp.to_mono(seq("glockenspiel", [96, 100], 0.04, 0.3, 0.05, release=0.3, rng=rng)), 0, 0.3))


@cue("cast_relic", 2, label="holy fire whoosh", **LAUNCH)
def cast_relic(rng, k):
    return mix((F.fire_whoosh(0.45, rng, 0.8), 0, 0.8), (dsp.to_mono(note("hand_chimes", 81, 0.5, 0.1, release=0.5, rng=rng)), 0, 0.35))


@cue("impact_arcane", 3, label="magical burst", **IMPACT)
def impact_arcane(rng, k):
    return mix((F.thump(rng.uniform(80, 110), 0.25, drop=0.5, click=0.3, rng=rng), 0, 0.7),
               (dsp.to_mono(F.shimmer(0.3, (88, 93, 96), rng=rng, spread=0)), 0, 0.5), (F.whoosh(0.25, 3000, 800, q=1.0, peak=0.1, rng=rng), 0, 0.5))


@cue("impact_holy", 2, label="bell ringing burst", **IMPACT)
def impact_holy(rng, k):
    return mix((dsp.to_mono(note("hand_chimes", [79, 84][k], 0.7, 0.2, release=0.8, rng=rng)), 0, 0.7),
               (F.whoosh(0.3, 3000, 800, q=1.0, peak=0.1, rng=rng), 0, 0.5), (F.thump(110, 0.15, drop=0.3, click=0.2, rng=rng), 0, 0.4))


@cue("impact_soul", 3, label="ghostly burst", **IMPACT)
def impact_soul(rng, k):
    burst = F.whoosh(0.4, 2000, 300, q=0.9, peak=0.15, color="pink", rng=rng)
    return mix((burst, 0, 0.8), (F.thump(70, 0.25, drop=0.4, click=0.1, rng=rng), 0, 0.5), (V.whisper(0.4, rng=rng), 0.02, 0.3))


@cue("impact_hex", 2, label="dark magic burst", **IMPACT)
def impact_hex(rng, k):
    a = dsp.to_mono(note("wine_glass", 76, 0.6, 0.2, release=0.4, rng=rng, detune=-0.3))
    return mix((F.thump(65, 0.3, drop=0.5, click=0.2, rng=rng), 0, 0.7), (a, 0, 0.35), (F.hiss(0.4, rng, 1500), 0, 0.35))


@cue("impact_lightning", 3, label="electric zap", **IMPACT)
def impact_lightning(rng, k):
    return mix((F.zap(0.3, rng), 0, 0.9), (F.thunder(0.9, rng, crack=1.2), 0, 0.6))


@cue("impact_water", 2, label="water splash", **IMPACT)
def impact_water(rng, k):
    return mix((F.splat(0.5, rng, 2.0), 0, 0.9), (s("ocean_drum", 0.6, seconds=0.6, rng=rng), 0, 0.4))


@cue("impact_frost", 3, label="ice shattering", **IMPACT)
def impact_frost(rng, k):
    return F.ice_shatter(rng, 0.7)


# ---------------------------------------------------------------- ability flourishes & rituals
ABILITY = dict(level="ability", cooldown=0.3, voices=2, pitch=0.02, stereo=True, positional=True)


@cue("ability_flourish", 2, label="heroic brass stab", **ABILITY)
def ability_flourish(rng, k):
    rise = F.whoosh(0.35, 400, 2500, q=0.8, peak=0.9, rng=rng)
    stab = chord("horn_stac", [62, 66, 69] if k == 0 else [64, 67, 71], 0.85, 0.3, rng=rng)
    return space(mix((spread(rise), 0, 0.6), (stab, 0.3, 0.9), (dsp.to_stereo(s("crash", 0.4, seconds=0.8, rng=rng)), 0.3, 0.15)), "hall", 0.18)


@cue("ability_beam", 1, label="magic beam humming", **ABILITY)
def ability_beam(rng, k):
    build = F.reverse_swell(note("glockenspiel", 91, 0.5, 0.05, rng=rng), 0.5)
    beam = hum(220, 1.0, rng, bright=0.8) + hum(330, 1.0, rng, bright=0.8) * 0.6
    return space(mix((build, 0, 0.5), (spread(beam), 0.45, 0.6), (spread(F.zap(0.8, rng)), 0.45, 0.25)), "hall", 0.2)


@cue("ability_overcharge", 1, label="electricity building up", **ABILITY)
def ability_overcharge(rng, k):
    n = dsp.samples(0.9)
    t = np.arange(n) / dsp.SR
    whine = np.sin(2 * np.pi * np.cumsum(200 + 1500 * (t / 0.9) ** 2) / dsp.SR) * (t / 0.9) * 0.3
    return space(mix((spread(whine), 0, 0.6), (spread(F.zap(0.9, rng)), 0, 0.6), (spread(F.thunder(1.2, rng, 1.0)), 0.85, 0.6)), "open", 0.2)


@cue("ability_blessing", 1, label="holy choir and bells", **ABILITY)
def ability_blessing(rng, k):
    return space(mix((choir(rng, [294, 370, 440], 1.4), 0, 0.8), (chord("hand_chimes", [74, 78, 81], 0.6, 0.2, release=1.0, strum=0.06, rng=rng), 0.2, 0.5)), "hall", 0.28)


@cue("ability_inspire", 1, label="horn call", **ABILITY)
def ability_inspire(rng, k):
    call = mix((note("horn", 57, 0.85, 0.35, rng=rng), 0), (note("horn", 62, 0.85, 0.35, rng=rng), 0.3), (note("horn", 66, 0.9, 0.9, release=0.5, rng=rng), 0.6))
    return space(mix((call, 0, 0.9), (spread(F.cloth_flap(1.2, 11, rng)), 0.1, 0.4)), "open", 0.25)


@cue("ability_vanish", 1, label="puff of smoke", **ABILITY)
def ability_vanish(rng, k):
    return space(mix((spread(F.whoosh(0.5, 2500, 500, q=0.7, peak=0.1, color="pink", rng=rng)), 0, 0.9),
                     (spread(F.hiss(0.6, rng, 1500)), 0.02, 0.5), (s("k_cloth", 0.7, detune=3, seconds=0.3, rng=rng), 0, 0.4)), "room", 0.1)


@cue("ability_charge", 1, label="cavalry charge with horn", **ABILITY)
def ability_charge(rng, k):
    horn = mix((note("horn", 62, 0.9, 0.3, rng=rng), 0), (note("horn", 69, 0.9, 0.6, release=0.4, rng=rng), 0.25))
    return space(mix((horn, 0, 0.8), (spread(F.gallop(1.4, 2.8, rng)), 0.1, 1.0)), "open", 0.2)


@cue("ability_shield", 1, label="shields slamming together", **ABILITY)
def ability_shield(rng, k):
    slam = mix((s("k_wood_heavy", 0.95, detune=-4, seconds=0.5, rng=rng), 0, 1.0), (s("k_plate_heavy", 0.9, detune=-3, seconds=0.5, rng=rng), 0.01, 0.6),
               (F.thump(60, 0.35, drop=0.4, click=0.4, rng=rng), 0, 0.6), (s("k_wood_heavy", 0.9, detune=-5, seconds=0.5, rng=rng), 0.12, 0.7))
    return space(mix((spread(slam), 0, 1.0), (dsp.to_stereo(s("crash", 0.35, seconds=0.9, rng=rng)), 0.0, 0.15)), "open", 0.15)


@cue("necro_raise", 2, level="ability", cooldown=0.6, voices=2, pitch=0.03, positional=True, label="undead rising from the grave")
def necro_raise(rng, k):
    swell = F.reverse_swell(dsp.to_mono(F.thump(55, 0.6, drop=0.2, click=0.5, rng=rng)), 0.8)
    return mix((dsp.to_mono(swell), 0, 0.6), (F.bone_rattle(0.8, 80, rng, 0.9), 0.6, 0.8), (V.whisper(0.8, rng=rng), 0.3, 0.3),
               (F.dig(0.5, rng), 0.6, 0.4))


@cue("hex_jam", 1, level="ability", cooldown=1.0, voices=1, pitch=0.0, stereo=True, label="eerie whispering curse")
def hex_jam(rng, k):
    out = np.zeros((dsp.samples(1.4), 2))
    for j in range(4):
        dsp.place(out, dsp.pan(V.whisper(1.1, rng=rng), (j - 1.5) * 0.5), j * 0.06, 0.7)
    a = note("wine_glass", 78, 0.6, 0.8, release=0.5, rng=rng)
    b = note("wine_glass", 79, 0.6, 0.8, release=0.5, rng=rng, detune=0.4)
    return space(mix((out, 0, 1.0), (a, 0, 0.25), (b, 0, 0.25)), "chamber", 0.2)
