"""Interface sounds: wood, leather, parchment and brass - the caravan quartermaster's desk."""
import numpy as np

from ak import dsp
from ak import foley as F
from sfx import cue
from sfx.kit import chord, mix, note, s, seq, space, spread

UI = dict(bus="Interface", stereo=True)


@cue("ui_tap", 4, level="ui", cooldown=0.04, voices=3, pitch=0.02, label="soft wooden button click", **UI)
def ui_tap(rng, k):
    wood = s("woodblock", rng.uniform(0.35, 0.5), detune=rng.uniform(4, 8), seconds=0.08, rng=rng)
    click = dsp.lowpass(s("k_ui_click", 0.8, detune=rng.uniform(-3, 0), seconds=0.07, rng=rng), 4200, 2)
    return space(mix((wood, 0, 0.8), (click, 0.002, 0.45)), "room", 0.05)


@cue("ui_confirm", 3, level="ui", cooldown=0.05, voices=3, pitch=0.015, label="button click with a soft bell", **UI)
def ui_confirm(rng, k):
    wood = s("woodblock", 0.55, detune=rng.uniform(1, 3), seconds=0.1, rng=rng)
    bell = note("hand_chimes", [84, 86, 88][k], 0.35, 0.05, release=0.35, rng=rng)
    return space(mix((wood, 0, 0.9), (bell, 0.004, 0.22)), "room", 0.06)


@cue("ui_back", 3, level="ui", cooldown=0.05, voices=2, pitch=0.02, label="soft low click", **UI)
def ui_back(rng, k):
    wood = s("woodblock", 0.4, detune=rng.uniform(-6, -3), seconds=0.1, rng=rng)
    swish = s("k_card_shove", 0.6, detune=rng.uniform(-5, -2), seconds=0.14, rng=rng)
    return space(mix((swish, 0, 0.5), (wood, 0.02, 0.9)), "room", 0.05)


@cue("ui_tab", 3, level="ui", cooldown=0.05, voices=2, pitch=0.02, label="page turning", **UI)
def ui_tab(rng, k):
    flip = s("k_book_flip", 0.8, detune=rng.uniform(1, 4), seconds=0.2, rng=rng)
    tick = s("woodblock", 0.3, detune=rng.uniform(8, 11), seconds=0.05, rng=rng)
    return space(mix((flip, 0, 0.8), (tick, 0.03, 0.35)), "room", 0.05)


@cue("ui_toggle_on", 1, level="ui", cooldown=0.05, voices=2, pitch=0.01, label="metal latch click", **UI)
def ui_toggle_on(rng, k):
    return space(mix((s("k_metal_latch", 0.8, detune=2, seconds=0.25, rng=rng), 0, 0.9),
                     (s("woodblock", 0.3, detune=7, seconds=0.05, rng=rng), 0.0, 0.3)), "room", 0.05)


@cue("ui_toggle_off", 1, level="ui", cooldown=0.05, voices=2, pitch=0.01, label="metal latch click", **UI)
def ui_toggle_off(rng, k):
    return space(mix((s("k_metal_click", 0.8, detune=-2, seconds=0.25, rng=rng), 0, 0.9),
                     (s("woodblock", 0.3, detune=2, seconds=0.05, rng=rng), 0.0, 0.3)), "room", 0.05)


@cue("ui_slider", 3, level="ui_quiet", cooldown=0.06, voices=1, pitch=0.03, label="tiny tick", **UI)
def ui_slider(rng, k):
    return space(s("woodblock", 0.25, detune=rng.uniform(10, 13), seconds=0.04, rng=rng), "room", 0.04)


@cue("ui_hover", 2, level="ui_quiet", cooldown=0.06, voices=1, pitch=0.02, label="faint tick", **UI)
def ui_hover(rng, k):
    return dsp.lowpass(dsp.to_stereo(s("k_ui_rollover", 0.6, detune=-4, seconds=0.08, rng=rng)), 3000, 2)


@cue("ui_error", 2, level="ui", cooldown=0.2, voices=1, pitch=0.01, label="dull double knock", **UI)
def ui_error(rng, k):
    a = s("frame_drum_muted", 0.5, detune=-2, seconds=0.12, rng=rng)
    b = s("woodblock", 0.4, detune=-8, seconds=0.1, rng=rng)
    return space(mix((a, 0, 0.9), (b, 0.0, 0.5), (a, 0.11, 0.7), (b, 0.11, 0.4)), "room", 0.05)


@cue("modal_open", 2, level="ui", cooldown=0.1, voices=2, pitch=0.01, label="book opening", **UI)
def modal_open(rng, k):
    book = s("k_book_open", 0.8, detune=rng.uniform(-1, 1), seconds=0.4, rng=rng)
    page = F.paper(0.22, rng) * 0.5
    glint = note("glockenspiel", [91, 93][k], 0.25, 0.05, release=0.5, rng=rng)
    return space(mix((book, 0, 0.9), (page, 0.05, 0.6), (glint, 0.06, 0.12)), "room", 0.07)


@cue("modal_close", 2, level="ui", cooldown=0.1, voices=2, pitch=0.01, label="book closing", **UI)
def modal_close(rng, k):
    book = s("k_book_close", 0.8, detune=rng.uniform(-2, 0), seconds=0.4, rng=rng)
    return space(mix((book, 0, 1.0), (F.cloth_flap(0.15, 10, rng), 0.0, 0.25)), "room", 0.06)


@cue("scene_change", 2, level="ui", cooldown=0.3, voices=1, pitch=0.01, label="cloth whoosh", **UI)
def scene_change(rng, k):
    swoosh = F.whoosh(0.5, 300, 1600, q=0.8, peak=0.5, color="pink", rng=rng)
    flap = F.cloth_flap(0.45, 9, rng)
    drum = s("frame_drum", 0.35, detune=-3, seconds=0.6, rng=rng)
    return space(mix((spread(swoosh), 0, 0.8), (flap, 0.05, 0.6), (drum, 0.32, 0.45)), "chamber", 0.1)


@cue("purchase", 3, level="reward", cooldown=0.15, voices=2, pitch=0.02, label="coins dropped into a purse", **UI)
def purchase(rng, k):
    coins = F.coins(6, 0.45, rng)
    pouch = s("k_leather", 0.7, detune=rng.uniform(-2, 1), seconds=0.4, rng=rng)
    chip = s("k_chips_handle", 0.6, detune=rng.uniform(5, 8), seconds=0.4, rng=rng)
    return space(mix((coins, 0, 0.9), (chip, 0.02, 0.35), (pouch, 0.18, 0.6)), "room", 0.06)


@cue("coin", 4, level="ui", cooldown=0.04, voices=3, pitch=0.04, label="coin clink", **UI)
def coin(rng, k):
    return space(mix((F.coin_clink(rng, rng.uniform(0.9, 1.1)), 0, 0.8),
                     (s("k_chip_lay", 0.5, detune=rng.uniform(6, 9), seconds=0.15, rng=rng), 0, 0.3)), "room", 0.05)


@cue("gem", 2, level="ui_bright", cooldown=0.1, voices=2, pitch=0.01, label="crystal chime", **UI)
def gem(rng, k):
    a = note("hand_chimes", 88 + 2 * k, 0.45, 0.1, release=1.0, rng=rng)
    b = note("glockenspiel", 95 + 2 * k, 0.35, 0.1, release=0.8, rng=rng)
    glass = s("k_glass_light", 0.5, detune=rng.uniform(6, 9), seconds=0.25, rng=rng)
    return space(mix((glass, 0, 0.35), (a, 0.0, 0.6), (b, 0.07, 0.4)), "chamber", 0.15)


@cue("upgrade_confirm", 2, level="reward", cooldown=0.15, voices=2, pitch=0.01, label="hammer striking an anvil", **UI)
def upgrade_confirm(rng, k):
    anvil = s("anvil", 0.85, detune=rng.uniform(-2, 1), seconds=1.0, rng=rng)
    sparkle = seq("glockenspiel", [86, 90, 93, 98], 0.05, 0.35, 0.1, release=0.6, rng=rng, pan_spread=0.5)
    return space(mix((anvil, 0, 1.0), (sparkle, 0.12, 0.3)), "chamber", 0.12)


@cue("level_up", 1, level="reward", cooldown=0.3, voices=1, pitch=0.0, label="triumphant chime fanfare", **UI)
def level_up(rng, k):
    harp = seq("harp", [62, 66, 69, 74, 78, 81, 86], 0.035, 0.6, 0.6, rng=rng, pan_spread=0.6)
    brass = chord("horn_stac", [62, 66, 69], 0.8, 0.4, rng=rng)
    bell = note("glockenspiel", 93, 0.45, 0.2, release=1.2, rng=rng)
    return space(mix((harp, 0, 0.6), (brass, 0.25, 0.7), (bell, 0.25, 0.3)), "hall", 0.2)


@cue("reward_claim", 2, level="reward", cooldown=0.2, voices=2, pitch=0.01, label="bag of coins and a sparkle", **UI)
def reward_claim(rng, k):
    bag = s("k_leather", 0.8, detune=rng.uniform(-3, -1), seconds=0.4, rng=rng)
    coins = F.coins(5, 0.35, rng)
    sparkle = seq("glockenspiel", [88, 91, 95], 0.06, 0.35, 0.1, release=0.6, rng=rng, pan_spread=0.4)
    return space(mix((bag, 0, 0.8), (coins, 0.04, 0.7), (sparkle, 0.12, 0.3)), "chamber", 0.12)


@cue("achievement_unlock", 1, level="reward", cooldown=0.5, voices=1, pitch=0.0, label="short trumpet fanfare", **UI)
def achievement_unlock(rng, k):
    tpt = mix((note("trumpet_stac", 69, 0.8, 0.15, rng=rng), 0), (note("trumpet_stac", 74, 0.8, 0.15, rng=rng), 0.12),
              (note("trumpet", 78, 0.8, 0.55, release=0.4, rng=rng), 0.24), (note("trumpet", 74, 0.6, 0.55, release=0.4, rng=rng), 0.24))
    sparkle = seq("glockenspiel", [90, 93, 98, 102], 0.05, 0.35, 0.1, release=0.8, rng=rng, pan_spread=0.6)
    cymbal = dsp.to_stereo(s("sus_cymbal", 0.4, seconds=1.2, rng=rng))
    return space(mix((tpt, 0, 0.8), (sparkle, 0.24, 0.3), (cymbal, 0.24, 0.15)), "hall", 0.2)


@cue("relic_pickup", 1, level="reward", cooldown=0.3, voices=1, pitch=0.0, label="magical shimmer", **UI)
def relic_pickup(rng, k):
    sh = F.shimmer(0.9, (86, 90, 93, 98, 102), rng=rng, spread=0.7)
    glass = note("wine_glass", 81, 0.5, 0.6, release=0.8, rng=rng)
    return space(mix((sh, 0, 0.8), (glass, 0.0, 0.25)), "hall", 0.25)


@cue("map_travel", 3, level="ui", cooldown=0.4, voices=1, pitch=0.02, label="horse and cart on a dirt road", **UI)
def map_travel(rng, k):
    hooves = np.zeros(dsp.samples(0.9))
    for j in range(4):
        dsp.place(hooves, F.hoof(rng, 0.8), j * 0.17 + rng.uniform(-0.01, 0.01), rng.uniform(0.6, 1.0))
    cart = s("k_creak", 0.6, detune=rng.uniform(-6, -3), seconds=0.6, rng=rng)
    return space(mix((spread(hooves), 0, 0.9), (cart, 0.1, 0.35)), "open", 0.12)


@cue("map_select", 3, level="ui", cooldown=0.08, voices=2, pitch=0.02, label="parchment tap", **UI)
def map_select(rng, k):
    tap = s("k_book_place", 0.6, detune=rng.uniform(1, 4), seconds=0.2, rng=rng)
    page = F.paper(0.12, rng) * 0.4
    return space(mix((tap, 0, 0.8), (page, 0.0, 0.5)), "room", 0.05)


@cue("card_pickup", 3, level="ui", cooldown=0.05, voices=2, pitch=0.03, label="card sliding", **UI)
def card_pickup(rng, k):
    return space(s("k_card_slide", 0.8, detune=rng.uniform(-1, 2), seconds=0.25, rng=rng), "room", 0.04)


@cue("card_drop", 3, level="ui", cooldown=0.05, voices=2, pitch=0.03, label="card placed on a table", **UI)
def card_drop(rng, k):
    place = s("k_card_place", 0.85, detune=rng.uniform(-2, 1), seconds=0.25, rng=rng)
    thud = s("frame_drum_muted", 0.3, detune=-4, seconds=0.1, rng=rng)
    return space(mix((place, 0, 0.9), (thud, 0, 0.3)), "room", 0.05)


@cue("card_cancel", 2, level="ui", cooldown=0.05, voices=2, pitch=0.03, label="card slid back", **UI)
def card_cancel(rng, k):
    return space(s("k_card_shove", 0.7, detune=rng.uniform(-3, -1), seconds=0.3, rng=rng), "room", 0.04)


@cue("spell_arm", 2, level="ui_bright", cooldown=0.1, voices=1, pitch=0.01, label="magical hum rising", **UI)
def spell_arm(rng, k):
    sh = F.shimmer(0.5, (81, 86, 88) if k == 0 else (83, 86, 90), rng=rng, spread=0.4)
    swell = F.reverse_swell(note("glockenspiel", 86 + k, 0.4, 0.05, rng=rng), 0.5)
    return space(mix((swell, 0, 0.5), (sh, 0.35, 0.5)), "chamber", 0.12)


@cue("notification", 1, level="ui", cooldown=0.3, voices=1, pitch=0.0, label="small hand bell", **UI)
def notification(rng, k):
    return space(s("hand_bell", 0.6, seconds=1.2, rng=rng), "room", 0.08)


def _star(rng, midi, size):
    harp = note("harp", midi, 0.7, 0.4, rng=rng)
    glock = note("glockenspiel", midi + 24, 0.4 + 0.1 * size, 0.1, release=0.9, rng=rng)
    chime = note("hand_chimes", midi + 12, 0.4, 0.2, release=0.8, rng=rng)
    return space(mix((harp, 0, 0.6), (glock, 0, 0.45), (chime, 0.02, 0.4)), "hall", 0.18)


@cue("star_1", 1, level="reward", cooldown=0.1, voices=2, pitch=0.0, label="bright chime", **UI)
def star_1(rng, k):
    return _star(rng, 74, 0)


@cue("star_2", 1, level="reward", cooldown=0.1, voices=2, pitch=0.0, label="bright chime", **UI)
def star_2(rng, k):
    return _star(rng, 78, 1)


@cue("star_3", 1, level="reward", cooldown=0.1, voices=2, pitch=0.0, label="bright chime", **UI)
def star_3(rng, k):
    return _star(rng, 81, 2)
