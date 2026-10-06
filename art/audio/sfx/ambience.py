"""Ambience: a seamless bed per place, and detail one-shots the game scatters around it (see AMBIENCE)."""
import numpy as np

from ak import dsp, nature as N, reverb, voice as V
from ak import foley as F
from sfx import cue
from sfx.kit import mix, note, s, space

BED = dict(level="bed", bus="Ambience", stereo=True, loop=True, cooldown=0.0, voices=1, pitch=0.0)
DET = dict(level="detail", bus="Ambience", stereo=True, cooldown=2.0, voices=2, pitch=0.04)
LOOP = 45.0
XF = 3.0


def scatter(total, make, every, rng, pan=0.8, gain=(0.4, 1.0)):
    """Bake occasional events into a bed."""
    out = np.zeros((dsp.samples(total + 3), 2))
    t = rng.uniform(0, every)
    while t < total - 1:
        x = make(rng)
        x = dsp.pan(dsp.to_mono(x), rng.uniform(-pan, pan)) if np.ndim(x) == 1 or x.shape[1] == 1 else x
        dsp.place(out, x, t, rng.uniform(*gain))
        t += rng.uniform(every * 0.6, every * 1.5)
    return out[: dsp.samples(total)]


def bed(*layers):
    total = dsp.samples(LOOP + XF)
    out = np.zeros((total, 2))
    for layer, gain in layers:
        out[: min(total, len(layer))] += layer[:total] * gain
    return N.loop_bed(out, XF)


T = LOOP + XF


# ---------------------------------------------------------------- beds
@cue("amb_home", 1, label="gentle wind and birdsong in a meadow", **BED)
def amb_home(rng, k):
    birds = scatter(T, lambda r: reverb.reverb(dsp.to_stereo(N.songbird(r, r.uniform(0.8, 1.6))), "forest", 0.4), 5.0, rng, gain=(0.25, 0.6))
    return bed((N.wind(T, rng, 0.45, 0.5, 200, 1100), 1.0), (N.leaves(T, rng, 0.6), 1.0), (birds, 0.5), (N.lapping(T, rng, 2.5), 0.25))


@cue("amb_road", 1, label="wind over fields with birds", **BED)
def amb_road(rng, k):
    birds = scatter(T, lambda r: reverb.reverb(dsp.to_stereo(N.songbird(r, r.uniform(0.6, 1.2))), "open", 0.4), 7.0, rng, gain=(0.2, 0.5))
    return bed((N.wind(T, rng, 0.65, 0.55, 200, 1300), 1.0), (N.leaves(T, rng, 0.9), 1.0), (birds, 0.45))


@cue("amb_harbor", 1, label="ocean waves and wind at a harbor", **BED)
def amb_harbor(rng, k):
    creaks = scatter(T, lambda r: N.distant(F.creak_sound(r.uniform(0.6, 1.0), r.uniform(0.6, 0.9), r), 2500, "open", 0.3), 9.0, rng, gain=(0.1, 0.25))
    return bed((N.surf(T, rng), 1.0), (N.wind(T, rng, 0.5, 0.5, 250, 1500, whistle=0.06), 0.8), (N.lapping(T, rng, 2.0), 0.6), (creaks, 1.0))


@cue("amb_foundry", 1, label="furnace roaring and fire crackling", **BED)
def amb_foundry(rng, k):
    pumps = scatter(T, lambda r: N.bellows(r), 5.0, rng, pan=0.5, gain=(0.15, 0.3))
    return bed((N.furnace(T, rng), 1.0), (pumps, 1.0), (N.wind(T, rng, 0.3, 0.4, 150, 800), 0.6))


@cue("amb_ward", 1, label="eerie wind with buzzing flies", **BED)
def amb_ward(rng, k):
    return bed((N.wind(T, rng, 0.55, 0.6, 150, 900, whistle=0.15), 1.0), (N.flies(T, rng, 2), 0.7), (N.fire_bed(T, rng, 0.5), 0.25))


@cue("amb_pass", 1, label="strong mountain wind howling", **BED)
def amb_pass(rng, k):
    return bed((N.wind(T, rng, 1.0, 0.85, 120, 1600, whistle=0.25), 1.0))


@cue("amb_basilica", 1, label="echoing cathedral interior with dripping water", **BED)
def amb_basilica(rng, k):
    n = dsp.samples(T)
    tone = np.column_stack([dsp.lowpass(dsp.noise(T, "brown", rng), 120, 2) for _ in range(2)]) * 0.6
    drips = scatter(T, lambda r: reverb.reverb(dsp.to_stereo(N.drip(r)), "cathedral", 0.7, 0.4), 3.5, rng, gain=(0.2, 0.5))
    draught = N.wind(T, rng, 0.3, 0.5, 400, 2500, whistle=0.2)
    return bed((tone, 1.0), (drips, 0.8), (draught, 0.6))


@cue("amb_mire", 1, label="swamp at night with frogs and insects", **BED)
def amb_mire(rng, k):
    frogs = scatter(T, lambda r: reverb.reverb(dsp.to_stereo(N.frog(r)), "forest", 0.3), 0.9, rng, gain=(0.15, 0.6))
    bubbles = scatter(T, lambda r: dsp.to_stereo(F.bubbles(0.8, 6, r, 1.6)), 4.0, rng, gain=(0.1, 0.3))
    return bed((N.crickets(T, rng, 7, 0.8), 1.0), (frogs, 0.8), (N.lapping(T, rng, 1.5), 0.4), (bubbles, 0.6),
               (N.wind(T, rng, 0.25, 0.4, 150, 700), 0.6))


@cue("amb_steppe", 1, label="dry wind across open grassland", **BED)
def amb_steppe(rng, k):
    return bed((N.wind(T, rng, 0.8, 0.6, 250, 1900), 1.0), (N.leaves(T, rng, 1.4), 1.0), (N.fire_bed(T, rng, 0.4), 0.15))


@cue("amb_gloamwood", 1, label="dark forest at night with crickets", **BED)
def amb_gloamwood(rng, k):
    creaks = scatter(T, lambda r: N.distant(F.creak_sound(r.uniform(0.5, 1.0), r.uniform(0.7, 1.1), r), 3000, "forest", 0.4), 8.0, rng, gain=(0.1, 0.25))
    return bed((N.wind(T, rng, 0.45, 0.5, 200, 1200), 1.0), (N.leaves(T, rng, 1.0), 1.0), (N.crickets(T, rng, 5, 0.6), 1.0), (creaks, 1.0))


@cue("amb_citadel", 1, label="wind on castle battlements with flags flapping", **BED)
def amb_citadel(rng, k):
    flags = scatter(T, lambda r: dsp.to_stereo(F.cloth_flap(r.uniform(1.0, 2.0), 10, r)), 4.0, rng, gain=(0.15, 0.35))
    return bed((N.wind(T, rng, 0.75, 0.7, 200, 1600, whistle=0.2), 1.0), (flags, 1.0), (N.fire_bed(T, rng, 0.6), 0.2))


@cue("amb_shop", 1, label="crackling brazier indoors", **BED)
def amb_shop(rng, k):
    outside = dsp.lowpass(N.wind(T, rng, 0.5, 0.5, 150, 800), 600, 2)
    return bed((N.fire_bed(T, rng, 0.8), 0.9), (outside, 0.7))


@cue("amb_endless", 1, label="cold night wind with distant moans", **BED)
def amb_endless(rng, k):
    moans = scatter(T, lambda r: N.distant(V.creature(1.4, [(0, 80), (0.5, 95), (1, 65)], [(0, "uh"), (0.5, "aw"), (1, "u")],
                                                      scale=0.9, jitter=0.05, shimmer=0.2, subharmonic=0.3, breath=0.4,
                                                      roughness=0.4, drive=2.0, attack=0.2, release=0.5, rng=r), 900, "cathedral", 0.6), 7.0, rng, gain=(0.08, 0.2))
    return bed((N.wind(T, rng, 0.6, 0.7, 150, 1000, whistle=0.12), 1.0), (N.crickets(T, rng, 3, 0.4), 1.0), (moans, 1.0))


@cue("amb_tourney", 1, label="wind with banners flapping", **BED)
def amb_tourney(rng, k):
    flags = scatter(T, lambda r: dsp.to_stereo(F.cloth_flap(r.uniform(1.0, 2.0), 11, r)), 3.0, rng, gain=(0.2, 0.4))
    return bed((N.wind(T, rng, 0.55, 0.5, 200, 1400), 1.0), (flags, 1.0), (N.leaves(T, rng, 0.5), 1.0))


@cue("battle_din", 1, label="distant battle with swords clashing and shouting", **BED)
def battle_din(rng, k):
    clashes = scatter(T, lambda r: N.distant(F.blade_clash(r, 0.5), 2500, "open", 0.5), 0.7, rng, gain=(0.15, 0.5))
    shouts = scatter(T, lambda r: N.distant(V.crowd_shout(r.uniform(0.8, 1.4), voices=6, base_f0=r.uniform(130, 170), rng=r), 1400, "open", 0.6), 3.0, rng, gain=(0.15, 0.4))
    drums = scatter(T, lambda r: N.distant(s("big_drum", r.uniform(0.6, 0.9), seconds=1.0, rng=r), 600, "open", 0.5), 1.6, rng, gain=(0.2, 0.5))
    return bed((clashes, 1.0), (shouts, 0.8), (drums, 0.7), (N.wind(T, rng, 0.3, 0.5, 150, 900), 0.5))


# ---------------------------------------------------------------- detail one-shots
def far(x, cutoff=3000, place="open", wet=0.45):
    return N.distant(x, cutoff, place, wet)


@cue("det_songbird", 4, label="bird chirping", **DET)
def det_songbird(rng, k):
    return far(N.songbird(rng, rng.uniform(0.8, 1.6)), 9000, "forest", 0.3)


@cue("det_crow", 3, label="crow cawing", **DET)
def det_crow(rng, k):
    return far(N.crow(rng), 5000, "open", 0.35)


@cue("det_bell_far", 2, label="distant church bell", **DET)
def det_bell_far(rng, k):
    return N.bell_far(rng, [62, 67][k], "cathedral")


@cue("det_cart", 2, label="wooden cart creaking past", **DET)
def det_cart(rng, k):
    return far(mix((F.creak_sound(1.2, 0.7, rng), 0, 0.8), (F.footsteps(4, 0.4, "dirt", 1.2, rng), 0.1, 0.5)), 2500, "open", 0.4)


@cue("det_gull", 3, label="seagulls crying", **DET)
def det_gull(rng, k):
    return far(N.gull(rng), 7000, "open", 0.35)


@cue("det_ship_bell", 2, label="ship bell ringing", **DET)
def det_ship_bell(rng, k):
    b = mix((note("tubular_bells", 74, 0.6, 0.3, release=2.0, rng=rng), 0), (note("tubular_bells", 74, 0.5, 0.3, release=2.0, rng=rng), 0.45))
    return far(b, 4500, "open", 0.4)


@cue("det_rope_creak", 2, label="ship ropes creaking", **DET)
def det_rope_creak(rng, k):
    return far(F.creak_sound(1.4, 0.9, rng), 3500, "open", 0.3)


@cue("det_anvil_far", 3, label="distant blacksmith hammering", **DET)
def det_anvil_far(rng, k):
    out = np.zeros(dsp.samples(2.5))
    for j in range(int(rng.integers(2, 5))):
        dsp.place(out, s("anvil", rng.uniform(0.5, 0.9), detune=rng.uniform(-3, 1), seconds=0.6, rng=rng), j * 0.55, 0.8)
    return far(out, 3500, "great_hall", 0.5)


@cue("det_steam", 2, label="steam hissing", **DET)
def det_steam(rng, k):
    return far(F.hiss(rng.uniform(1.0, 1.8), rng, 1200), 6000, "great_hall", 0.3)


@cue("det_chains", 2, label="chains rattling", **DET)
def det_chains(rng, k):
    return far(F.chain_jingle(1.0, 160, rng, 1200, 5000), 5000, "great_hall", 0.4)


@cue("det_toll", 2, label="slow funeral bell tolling", **DET)
def det_toll(rng, k):
    out = np.zeros((dsp.samples(5.5), 2))
    for j in range(2):
        dsp.place(out, N.bell_far(rng, [57, 59][k], "cathedral"), j * 2.2)
    return out


@cue("det_moan_far", 3, label="distant zombie moaning", **DET)
def det_moan_far(rng, k):
    g = V.creature(1.6, [(0, 85), (0.4, 100), (1, 65)], [(0, "uh"), (0.5, "aw"), (1, "u")], scale=0.9, jitter=0.05, shimmer=0.2,
                   subharmonic=0.3, breath=0.4, roughness=0.4, drive=2.0, attack=0.25, release=0.5, rng=rng)
    return far(g, 1200, "cathedral", 0.6)


@cue("det_raptor", 2, label="hawk screeching", **DET)
def det_raptor(rng, k):
    return far(N.raptor(rng), 8000, "open", 0.35)


@cue("det_rockfall", 2, label="rocks falling", **DET)
def det_rockfall(rng, k):
    return far(mix((F.crunch(1.2, 120, 300, 3000, rng=rng, decay=0.5), 0, 0.8), (F.stone_hit(0.8, rng), 0.2, 0.6), (F.stone_hit(0.6, rng), 0.6, 0.4)), 3000, "great_hall", 0.45)


@cue("det_gust", 2, label="strong gust of wind", **DET)
def det_gust(rng, k):
    n = dsp.samples(3.0)
    g = N.wind(3.0, rng, 1.0, 0.0, 200, 1800, whistle=0.2) * F.env_ar(n, 0.35, 0.65, 1.5)[:, None]
    return g


@cue("det_drip", 3, label="water dripping in a cave", **DET)
def det_drip(rng, k):
    out = np.zeros(dsp.samples(1.2))
    for j in range(int(rng.integers(1, 3))):
        dsp.place(out, N.drip(rng), j * rng.uniform(0.3, 0.5))
    return reverb.reverb(dsp.to_stereo(out), "cathedral", 0.6, 0.5)


@cue("det_choir_far", 2, label="distant ghostly choir", **DET)
def det_choir_far(rng, k):
    from sfx.magic import choir
    return far(choir(rng, [147, 175, 220] if k == 0 else [131, 165, 196], 3.0, vowel="o", attack=0.8), 1600, "cathedral", 0.7)


@cue("det_frog", 4, label="frog croaking", **DET)
def det_frog(rng, k):
    out = np.zeros(dsp.samples(1.5))
    for j in range(int(rng.integers(1, 4))):
        dsp.place(out, N.frog(rng), j * rng.uniform(0.3, 0.45))
    return far(out, 5000, "forest", 0.3)


@cue("det_bubbles", 2, label="mud bubbling", **DET)
def det_bubbles(rng, k):
    return far(F.bubbles(1.5, 14, rng, 2.0), 2500, "forest", 0.25)


@cue("det_owl", 2, label="owl hooting", **DET)
def det_owl(rng, k):
    return far(N.owl(rng), 3000, "forest", 0.5)


@cue("det_branch", 3, label="tree branch creaking", **DET)
def det_branch(rng, k):
    return far(F.creak_sound(rng.uniform(0.8, 1.4), rng.uniform(0.8, 1.2), rng), 3500, "forest", 0.4)


@cue("det_whisper_far", 2, label="eerie whispers", **DET)
def det_whisper_far(rng, k):
    return far(V.whisper(1.6, rng=rng), 5000, "cave", 0.5)


@cue("det_war_drums", 2, label="distant war drums", **DET)
def det_war_drums(rng, k):
    out = np.zeros(dsp.samples(3.0))
    for j, t in enumerate((0, 0.5, 1.0, 1.25, 1.5)):
        dsp.place(out, s("big_drum", rng.uniform(0.6, 0.9), seconds=1.0, rng=rng), t, 0.8)
    return far(out, 700, "open", 0.6)


@cue("det_banner", 2, label="flag flapping in the wind", **DET)
def det_banner(rng, k):
    return dsp.to_stereo(F.cloth_flap(1.6, 11, rng))


@cue("det_horse_far", 2, label="distant horses galloping", **DET)
def det_horse_far(rng, k):
    return far(F.gallop(2.0, 2.4, rng), 1500, "open", 0.5)


@cue("det_embers", 2, label="embers crackling", **DET)
def det_embers(rng, k):
    return dsp.to_stereo(F.crackle(1.5, 40, rng) * F.env_ar(dsp.samples(1.5), 0.2, 0.6))


@cue("det_coins", 1, label="coins being counted", **DET)
def det_coins(rng, k):
    return far(F.coins(8, 1.2, rng), 6000, "room", 0.2)


@cue("det_wagon_creak", 2, label="wooden wagon creaking", **DET)
def det_wagon_creak(rng, k):
    return far(F.creak_sound(1.0, 0.8, rng), 3000, "room", 0.2)


# Which bed and details play where: context -> (bed, [details], (min_gap, max_gap) seconds).
AMBIENCE = {
    "home": ("amb_home", ["det_songbird", "det_crow", "det_bell_far"], (6, 14)),
    "shop": ("amb_shop", ["det_coins", "det_wagon_creak"], (9, 18)),
    "endless": ("amb_endless", ["det_moan_far", "det_toll", "det_crow"], (7, 15)),
    "multiplayer": ("amb_tourney", ["det_banner", "det_horse_far", "det_war_drums"], (8, 16)),
    "city": ("amb_road", ["det_songbird", "det_crow", "det_bell_far", "det_cart"], (6, 14)),
    "harbor": ("amb_harbor", ["det_gull", "det_ship_bell", "det_rope_creak"], (5, 12)),
    "foundry": ("amb_foundry", ["det_anvil_far", "det_steam", "det_chains"], (5, 11)),
    "quarantine": ("amb_ward", ["det_toll", "det_moan_far", "det_crow"], (7, 15)),
    "thornwall": ("amb_pass", ["det_raptor", "det_rockfall", "det_gust"], (6, 13)),
    "basilica": ("amb_basilica", ["det_drip", "det_choir_far", "det_toll", "det_whisper_far"], (6, 13)),
    "mire": ("amb_mire", ["det_frog", "det_bubbles", "det_crow"], (4, 10)),
    "steppe": ("amb_steppe", ["det_raptor", "det_horse_far", "det_embers"], (7, 14)),
    "gloamwood": ("amb_gloamwood", ["det_owl", "det_branch", "det_whisper_far", "det_crow"], (6, 13)),
    "citadel": ("amb_citadel", ["det_war_drums", "det_chains", "det_banner", "det_bell_far"], (6, 13)),
}
