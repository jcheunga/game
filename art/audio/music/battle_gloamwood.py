"""Gloamwood Verge: "Witch-Lights" - haunted timber roads and circling harps. E minor, 108 bpm, 32 bars."""
from ak import song as S
from ak.song import ease_out
from music.battle_kit import drum_parts, fill, groove, hit, timpani
from music.common import arpeggio, bass, ostinato, pad

BPM = 108
PROG_A = [("Em", 4), ("C", 4), ("Am", 4), ("B", 4), ("Em", 4), ("G", 4), ("Am", 4), ("B", 4)]
OCARINA = ("B4:1.5 E5:.5 G5:1 F#5:1 E5:2 G5:1 E5:1 C5:1.5 B4:.5 A4:1 C5:1 B4:2 D#5:2 "
           "E5:1.5 F#5:.5 G5:1 B5:1 D6:2 B5:1 G5:1 C6:1.5 B5:.5 A5:1 E5:1 F#5:2 D#5:2")
PROG_B = [("Am", 4), ("Em", 4), ("C", 4), ("B", 4), ("Am", 4), ("Em", 4), ("C", 4), ("B", 4)]
TUNE_B = "A5:2 C6:2 B5:2 G5:2 E5:2 G5:2 F#5:2 D#5:2 A5:2 E6:2 G5:2 B5:2 C6:1 B5:1 A5:1 G5:1 F#5:4"


def build(lead="ocarina"):
    s = S.Song("battle_gloamwood", BPM, bars=32, seed=191)
    s.part("harp", "folk_harp", gain_db=-5, pan=0.3, send=0.35, space="forest")
    s.part("lead", lead, gain_db=-5, pan=-0.15, send=0.4, space="forest")
    s.part("glock", "glockenspiel", gain_db=-17, pan=0.45, send=0.45, space="forest")
    s.part("vln_pizz", "violins_pizz", gain_db=-10, pan=-0.35, send=0.3, space="forest")
    s.part("vla_pizz", "violas_pizz", gain_db=-10, pan=0.2, send=0.3, space="forest")
    s.part("violins", "violins", gain_db=-7, pan=-0.35, send=0.35, space="forest")
    s.part("violas", "violas", gain_db=-12, pan=0.15, send=0.35, space="forest")
    s.part("cellos", "cellos", gain_db=-10, pan=0.25, send=0.3, space="forest")
    s.part("cello_spic", "cellos_spic", gain_db=-8, pan=0.25, send=0.25, space="forest")
    s.part("basses", "basses", gain_db=-11, pan=0.3, send=0.3, space="forest")
    s.part("horns", "horn", gain_db=-7, pan=-0.2, send=0.4, space="forest")
    s.part("wood", "woodblock", gain_db=-16, pan=0.4, send=0.3, space="forest", human=0.004)
    s.part("claves", "claves", gain_db=-18, pan=-0.4, send=0.3, space="forest", human=0.004)
    drum_parts(s, space="forest")

    def circles(start, prog, vel=0.5):
        # Triplet harp circles - the witch-lights.
        arpeggio(s, "harp", start, prog, order=(0, 1, 2, 3, 2, 1), step=1 / 3, low=52, high=79, count=4, vel=vel,
                 ring=1.5)

    def knocks(start_bar, bars, vel=0.5):
        s.pattern("wood", start_bar, bars, "x..x..x.....x.x.", steps_per_beat=4, vel=vel)
        s.pattern("claves", start_bar, bars, "......x.......x.", steps_per_beat=4, vel=vel)

    # S1: the verge at dusk - harp circles, pizzicato footsteps, knocks in the trees.
    circles(0, PROG_A, 0.48)
    pad(s, "cellos", 0, PROG_A, 40, 52, count=1, vel=0.45)
    ostinato(s, "vla_pizz", 0, PROG_A, [None, "3", None, 7], step=0.5, base_low=52, base_high=64, vel=0.45)
    knocks(0, 8, 0.45)
    s.line("glock", 12, "D#6:2 r:14 F#6:2 r:14", vel=0.4, damp=0.6)
    groove(s, "pulse", 0, 8, vel=0.5, skip=("bd",))
    fill(s, 30, 2, "toms", 0.65)

    # S2: the lead's spell over the circling harp.
    o = 32
    s.line("lead", o, OCARINA, vel=0.78)
    circles(o, PROG_A, 0.5)
    ostinato(s, "vln_pizz", o, PROG_A, [12, None, "10", None, 7, None, "10", None], step=0.5, base_low=59, base_high=71,
             vel=0.45)
    ostinato(s, "cello_spic", o, PROG_A, [0, None, 0, 7], step=0.5, base_low=40, base_high=52, vel=0.55)
    bass(s, "basses", o, PROG_A, rhythm=((0, 4),), low=28, high=40, vel=0.5)
    knocks(8, 8, 0.5)
    groove(s, "taiko", 8, 8, vel=0.55)
    timpani(s, o, PROG_A, 8, every=2, vel=0.45, offbeat=False)
    s.line("glock", o + 12, "D#6:2 r:14 F#6:2 r:14", vel=0.4, damp=0.6)
    fill(s, o + 30, 2, "toms", 0.75)

    # S3: the circle closes - violins and horns, harp racing.
    o = 64
    s.line("violins", o, TUNE_B, vel=0.8)
    pad(s, "horns", o, PROG_B, 50, 64, count=3, vel=0.52)
    circles(o, PROG_B, 0.52)
    ostinato(s, "cello_spic", o, PROG_B, [0, 0, 7, 0, 12, 0, 7, 0], step=0.5, base_low=40, base_high=52, vel=0.6)
    bass(s, "basses", o, PROG_B, rhythm=((0, 2), (2, 2)), low=28, high=40, vel=0.55)
    groove(s, "driving", 16, 8, vel=0.58)
    timpani(s, o, PROG_B, 8, vel=0.55)
    hit(s, o, crash=0.55)
    fill(s, o + 30, 2, "big", 0.8)

    # S4: the spell in full - lead and violins, glockenspiel glints, everything turning.
    o = 96
    s.line("lead", o, OCARINA, vel=0.8)
    s.line("violins", o, OCARINA, vel=0.66)
    s.line("glock", o, OCARINA, vel=0.32, transpose=12, damp=0.4)
    circles(o, PROG_A, 0.52)
    pad(s, "violas", o, PROG_A, 55, 67, count=2, vel=0.45)
    ostinato(s, "cello_spic", o, PROG_A, [0, 0, 7, 0, 12, 0, 7, 0], step=0.5, base_low=40, base_high=52, vel=0.62)
    bass(s, "basses", o, PROG_A, rhythm=((0, 2), (2, 2)), low=28, high=40, vel=0.58)
    knocks(24, 8, 0.5)
    groove(s, "taiko_full", 24, 8, vel=0.6)
    timpani(s, o, PROG_A, 8, vel=0.58)
    hit(s, o, crash=0.6, gong=0.3)
    fill(s, o + 30, 2, "toms", 0.8)
    ease_out(s, ["violins", "lead", "violas", "glock"], o + 28, o + 32, -6)
    return s
