"""Creature voices of the Rotbound host, the caravan's hounds, and the grave lords."""
import numpy as np

from ak import dsp, voice as V
from ak import foley as F
from sfx import cue
from sfx.kit import mix, note, s, space

POS = dict(positional=True)
VOICE = dict(level="voice", cooldown=0.35, voices=3, pitch=0.06, **POS)


def groan(rng, f=90, dur=1.3, scale=0.92):
    return V.creature(dur, [(0, f * 0.9), (0.3, f * 1.15), (0.7, f), (1, f * 0.75)],
                      [(0, "uh"), (0.4, rng.choice(["aw", "a"])), (0.8, "o"), (1, "u")], scale=scale,
                      jitter=0.05, shimmer=0.2, subharmonic=0.35, breath=0.35, roughness=0.5, rough_rate=rng.uniform(24, 32),
                      drive=2.5, attack=0.12, release=0.4, rng=rng)


def roar(rng, f=80, dur=1.6, scale=0.68, drive=4.0, rough=0.7):
    return V.creature(dur, [(0, f * 0.85), (0.2, f * 1.35), (0.6, f * 1.15), (1, f * 0.75)],
                      [(0, "uh"), (0.2, "a"), (0.7, "aw"), (1, "o")], scale=scale, jitter=0.06, shimmer=0.25,
                      subharmonic=0.5, breath=0.45, roughness=rough, rough_rate=rng.uniform(30, 40), drive=drive,
                      attack=0.06, release=0.5, rng=rng)


def bark(rng, count=1):
    """A hound's bark: a hard, noisy onset and a fast falling 'rowf' rather than a held vowel."""
    out = np.zeros(dsp.samples(0.3 * count + 0.3))
    for j in range(count):
        f = rng.uniform(380, 480)
        b = V.creature(rng.uniform(0.09, 0.12), [(0, f * 1.25), (0.25, f * 1.1), (1, f * 0.55)],
                       [(0, "a"), (0.5, "aw"), (1, "u")], scale=1.3, jitter=0.07, shimmer=0.3, subharmonic=0.25,
                       breath=0.55, roughness=0.5, rough_rate=55, drive=3.5, attack=0.003, release=0.04, rng=rng)
        chest = F.thump(rng.uniform(140, 180), 0.08, drop=0.4, click=0.6, rng=rng) * 0.5
        burst = dsp.bandpass(rng.standard_normal(dsp.samples(0.03)), 800, 4000, 2) * np.hanning(dsp.samples(0.03)) * 0.4
        dsp.place(out, b, j * rng.uniform(0.2, 0.26))
        dsp.place(out, chest, j * rng.uniform(0.2, 0.26))
        dsp.place(out, burst, j * 0.23)
    return out


@cue("risen_groan", 6, **VOICE, label="zombie groaning")
def risen_groan(rng, k):
    return groan(rng, rng.uniform(78, 105), rng.uniform(1.0, 1.5))


@cue("ghoul_shriek", 4, **VOICE, label="monster screaming")
def ghoul_shriek(rng, k):
    f = rng.uniform(450, 560)
    return V.creature(rng.uniform(0.5, 0.75), [(0, f), (0.2, f * 1.8), (0.6, f * 1.6), (1, f * 1.1)],
                      [(0, "e"), (0.3, "i"), (1, "e")], scale=1.15, jitter=0.06, shimmer=0.25, subharmonic=0.2, breath=0.5,
                      roughness=0.6, rough_rate=60, drive=4.0, attack=0.02, release=0.2, rng=rng)


@cue("hulk_gurgle", 3, **VOICE, label="monster gurgling")
def hulk_gurgle(rng, k):
    g = groan(rng, rng.uniform(62, 75), 1.3, scale=0.8)
    g = dsp.lowpass(g, 2200, 2)
    return mix((g, 0, 0.9), (F.bubbles(1.2, 18, rng, size=1.6), 0.1, 0.5), (F.splat(0.4, rng, 0.8), 0.4, 0.3))


@cue("brute_roar", 3, level="voice", cooldown=0.5, voices=2, pitch=0.05, label="monster roaring", **POS)
def brute_roar(rng, k):
    return roar(rng, rng.uniform(85, 105), rng.uniform(1.0, 1.3), 0.78)


@cue("giant_roar", 2, level="voice_big", cooldown=0.8, voices=2, pitch=0.04, label="giant monster roaring", **POS)
def giant_roar(rng, k):
    r = roar(rng, rng.uniform(55, 65), 1.8, 0.56, drive=4.5)
    sub = F.thump(40, 1.2, drop=0.1, click=0.0, rng=rng) * 0.4
    return mix((r, 0, 1.0), (sub, 0.05, 0.6))


@cue("caster_hiss", 3, **VOICE, label="creature hissing")
def caster_hiss(rng, k):
    hiss = F.hiss(0.9, rng, 2500)
    throat = V.creature(0.8, [(0, 70), (1, 60)], [(0, "i"), (1, "e")], scale=1.0, jitter=0.1, shimmer=0.4, subharmonic=0.5,
                        breath=0.8, roughness=0.8, rough_rate=22, drive=2.0, attack=0.05, release=0.3, rng=rng)
    return mix((hiss, 0, 0.6), (throat, 0, 0.6))


@cue("hex_whisper", 3, level="voice", cooldown=0.5, voices=2, pitch=0.03, stereo=True, label="people whispering")
def hex_whisper(rng, k):
    out = np.zeros((dsp.samples(1.6), 2))
    for j in range(3):
        w = V.whisper(1.3, rng=rng, scale=rng.uniform(0.9, 1.15))
        dsp.place(out, dsp.pan(w, (j - 1) * 0.6), j * 0.08, 0.7)
    glass = note("wine_glass", [78, 79, 81][k], 0.35, 0.6, release=0.6, rng=rng)
    return space(mix((out, 0, 1.0), (glass, 0.1, 0.1)), "chamber", 0.18)


@cue("lich_wail", 2, level="voice", cooldown=0.8, voices=1, pitch=0.03, stereo=True, label="ghost wailing")
def lich_wail(rng, k):
    wail = V.creature(1.8, [(0, 300), (0.2, 520), (0.7, 480), (1, 260)], [(0, "u"), (0.3, "o"), (0.7, "a"), (1, "u")],
                      scale=1.1, jitter=0.01, shimmer=0.05, breath=0.4, drive=1.2, attack=0.3, release=0.6,
                      vibrato=(5.5, 0.02), rng=rng)
    whisper = V.whisper(1.5, rng=rng)
    return space(mix((F.reverse_swell(wail, 0.6), 0, 0.4), (wail, 0.5, 0.8), (whisper, 0.6, 0.3)), "cathedral", 0.35)


@cue("herald_howl", 2, level="voice_big", cooldown=1.0, voices=1, pitch=0.03, stereo=True, label="dark war horn blowing")
def herald_howl(rng, k):
    horn = note("horn", 38 + k, 0.9, 1.4, release=0.6, rng=rng)
    horn = dsp.lowpass(dsp.saturate(horn * 2.0, 1.6), 3500, 2)
    howl = V.creature(1.6, [(0, 160), (0.2, 240), (0.8, 230), (1, 140)], [(0, "u"), (0.3, "o"), (1, "u")], scale=0.9,
                      jitter=0.03, shimmer=0.1, subharmonic=0.3, breath=0.4, roughness=0.3, drive=2.0, attack=0.15,
                      release=0.5, rng=rng)
    return space(mix((horn, 0, 0.9), (howl, 0.1, 0.4)), "great_hall", 0.3)


@cue("captain_shout", 2, level="voice", cooldown=0.8, voices=1, pitch=0.03, label="ghostly commander shouting", **POS)
def captain_shout(rng, k):
    r = roar(rng, rng.uniform(110, 130), 0.9, 0.9, drive=2.5, rough=0.4)
    return mix((r, 0, 1.0), (F.reverse_swell(r, 0.4), 0, 0.3))


@cue("sapper_fuse", 2, level="impact", cooldown=0.4, voices=2, pitch=0.04, label="fuse sizzling", **POS)
def sapper_fuse(rng, k):
    return mix((F.hiss(1.0, rng, 3000), 0, 0.6), (F.crackle(1.0, 120, rng), 0, 0.8))


@cue("plague_engine", 2, level="impact", cooldown=0.5, voices=2, pitch=0.04, label="machine clanking and hissing steam", **POS)
def plague_engine(rng, k):
    return mix((s("k_metal_pot", 0.8, detune=-5, seconds=0.4, rng=rng), 0, 0.8), (F.ratchet(0.4, rng), 0.1, 0.5),
               (F.hiss(0.7, rng, 1500), 0.25, 0.6), (s("k_metal_pot", 0.7, detune=-7, seconds=0.4, rng=rng), 0.45, 0.6))


@cue("siege_tower_roll", 2, level="impact", cooldown=1.0, voices=1, pitch=0.03, label="heavy wooden wheels rolling and creaking", **POS)
def siege_tower_roll(rng, k):
    rumble = dsp.lowpass(F.shaped_noise(1.6, color="brown", rng=rng), 180, 2) * 2.5
    return mix((rumble, 0, 0.8), (F.creak_sound(1.2, 0.6, rng), 0.1, 0.7), (s("k_wood", 0.5, detune=-8, seconds=0.3, rng=rng), 0.6, 0.5))


@cue("bone_nest_crack", 2, level="impact", cooldown=0.3, voices=2, pitch=0.05, label="bones cracking apart", **POS)
def bone_nest_crack(rng, k):
    return mix((F.crunch(0.25, 900, 800, 5000, rng=rng, decay=0.1), 0, 0.9), (F.bone_rattle(0.7, 80, rng), 0.05, 0.8))


@cue("mirror_hum", 1, level="impact", cooldown=0.6, voices=1, pitch=0.02, label="glass humming", **POS)
def mirror_hum(rng, k):
    return dsp.to_mono(note("wine_glass", 79, 0.6, 0.8, release=0.6, rng=rng))


@cue("hound_bark", 3, level="voice", cooldown=0.4, voices=2, pitch=0.05, label="dog barking", **POS)
def hound_bark(rng, k):
    return bark(rng, 1 + k % 2)


@cue("hound_growl", 2, level="voice", cooldown=0.5, voices=2, pitch=0.04, label="dog growling", **POS)
def hound_growl(rng, k):
    return V.creature(1.0, [(0, 100), (0.5, 120), (1, 95)], [(0, "uh"), (0.5, "aw"), (1, "uh")], scale=1.25, jitter=0.08,
                      shimmer=0.3, subharmonic=0.4, breath=0.55, roughness=0.85, rough_rate=30, drive=3.0, attack=0.1,
                      release=0.2, rng=rng)


@cue("hound_howl", 1, level="voice", cooldown=1.5, voices=1, pitch=0.02, stereo=True, label="wolf howling")
def hound_howl(rng, k):
    h = V.creature(2.0, [(0, 380), (0.15, 560), (0.7, 600), (1, 420)], [(0, "u"), (0.2, "o"), (0.6, "o"), (1, "u")],
                   scale=1.25, jitter=0.01, shimmer=0.05, breath=0.15, drive=1.5, attack=0.15, release=0.6,
                   vibrato=(5, 0.012), rng=rng)
    return space(h, "hall", 0.3)


@cue("berserker_roar", 2, level="voice", cooldown=0.8, voices=1, pitch=0.03, label="man shouting a battle cry", **POS)
def berserker_roar(rng, k):
    f = rng.uniform(150, 175)
    return V.creature(0.9, [(0, f * 0.85), (0.15, f * 1.3), (0.7, f * 1.15), (1, f * 0.8)], [(0, "uh"), (0.15, "a"), (1, "o")],
                      scale=1.0, jitter=0.02, shimmer=0.1, breath=0.3, roughness=0.35, rough_rate=50, drive=2.4,
                      attack=0.04, release=0.25, rng=rng)


# ---------------------------------------------------------------- grave lords (one roar per lineage)
BOSS = dict(level="voice_big", cooldown=1.5, voices=1, pitch=0.0, stereo=True)


def _boss_space(x, wet=0.28, space_name="great_hall"):
    return space(x, space_name, wet)


@cue("boss_roar_grave", 1, label="monster roaring", **BOSS)
def boss_roar_grave(rng, k):
    return _boss_space(mix((roar(rng, 62, 2.0, 0.6), 0, 1.0), (F.bone_rattle(0.8, 60, rng), 0.3, 0.3)))


@cue("boss_roar_tide", 1, label="sea monster roaring", **BOSS)
def boss_roar_tide(rng, k):
    r = roar(rng, 58, 2.0, 0.58)
    wave = s("ocean_drum", 0.8, seconds=2.0, rng=rng)
    return _boss_space(mix((r, 0, 1.0), (wave, 0, 0.5), (F.bubbles(1.5, 20, rng, 1.5), 0.2, 0.3)))


@cue("boss_roar_iron", 1, label="metallic monster roar", **BOSS)
def boss_roar_iron(rng, k):
    r = roar(rng, 70, 1.8, 0.62)
    clang = s("anvil", 0.9, detune=-12, seconds=1.5, rng=rng)
    return _boss_space(mix((clang, 0, 0.6), (r, 0.05, 1.0), (dsp.lowpass(r, 1200, 2) * 0.6 * np.sign(np.sin(np.arange(len(r)) * 0.02)), 0.05, 0.25)))


@cue("boss_roar_plague", 1, label="monster gurgling roar", **BOSS)
def boss_roar_plague(rng, k):
    return _boss_space(mix((roar(rng, 66, 1.9, 0.64, rough=0.85), 0, 1.0), (F.bubbles(1.6, 26, rng, 1.8), 0.1, 0.5),
                           (F.hiss(1.5, rng, 1200), 0.3, 0.3)))


@cue("boss_roar_beast", 1, label="giant beast roaring", **BOSS)
def boss_roar_beast(rng, k):
    return _boss_space(mix((roar(rng, 50, 2.2, 0.52, drive=4.5), 0, 1.0), (F.thump(38, 1.5, drop=0.1, click=0.0, rng=rng), 0.05, 0.5)))


@cue("boss_roar_witch", 1, label="witch cackling", **BOSS)
def boss_roar_witch(rng, k):
    out = np.zeros(dsp.samples(2.2))
    f = 330
    for j in range(6):
        cackle = V.creature(0.14, [(0, f * (1.3 - 0.05 * j)), (1, f * (1.1 - 0.05 * j))], [(0, "a"), (1, "e")],
                            scale=1.3, jitter=0.03, shimmer=0.15, breath=0.4, roughness=0.3, drive=2.0, attack=0.01,
                            release=0.05, rng=rng)
        dsp.place(out, cackle, 0.3 + j * 0.16)
    shriek = V.creature(0.5, [(0, 700), (0.3, 1100), (1, 600)], [(0, "e"), (1, "i")], scale=1.3, jitter=0.05, shimmer=0.2,
                        breath=0.5, roughness=0.4, drive=3.0, attack=0.02, release=0.2, rng=rng)
    return _boss_space(mix((shriek, 0, 0.8), (out, 0, 0.9), (V.whisper(1.5, rng=rng), 0.4, 0.3)), 0.35, "cave")


@cue("boss_roar_warlord", 1, label="warlord shouting with horses", **BOSS)
def boss_roar_warlord(rng, k):
    shout = roar(rng, 120, 1.2, 0.85, drive=3.0, rough=0.45)
    return _boss_space(mix((F.gallop(1.2, 2.6, rng), 0, 0.6), (shout, 0.2, 1.0),
                           (note("horn", 41, 0.8, 1.0, release=0.4, rng=rng), 0.1, 0.4)))


@cue("boss_roar_sovereign", 1, label="deep demonic roar", **BOSS)
def boss_roar_sovereign(rng, k):
    r = roar(rng, 48, 2.4, 0.5, drive=4.0)
    return space(mix((F.reverse_swell(r, 0.8), 0, 0.4), (r, 0.6, 1.0), (s("gong", 0.7, seconds=3.0, rng=rng), 0.6, 0.4)), "cathedral", 0.35)


@cue("boss_roar_pontiff", 1, label="ghostly choir wailing", **BOSS)
def boss_roar_pontiff(rng, k):
    out = np.zeros((dsp.samples(2.6), 2))
    for j, f in enumerate((147, 175, 220, 262)):
        v = V.creature(2.2, [(0, f), (0.5, f * 1.03), (1, f * 0.9)], [(0, "o"), (0.5, "a"), (1, "u")], scale=1.0,
                       jitter=0.01, shimmer=0.05, breath=0.35, drive=1.2, attack=0.4, release=0.6, vibrato=(5, 0.015), rng=rng)
        dsp.place(out, dsp.pan(v, (j - 1.5) * 0.4), j * 0.05)
    return space(mix((out, 0, 1.0), (roar(rng, 60, 1.8, 0.6), 0.5, 0.5), (note("tubular_bells", 62, 0.7, 0.5, release=2.0, rng=rng), 0.2, 0.3)), "cathedral", 0.4)
