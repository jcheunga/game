"""Crownfall Citadel: "The Last Gate" - the caravan's theme against the host's motif. C minor, 136 bpm, 32 bars."""
from ak import song as S
from ak.song import ease_out
from music.battle_kit import drum_parts, fill, groove, hit, timpani
from music.common import (CROWN_A, CROWN_A_CHORDS, CROWN_B, CROWN_B_CHORDS, bass, chord_root, expand, ostinato, pad,
                          transpose_chords, transpose_line)

BPM = 136
THEME = transpose_line(CROWN_A, -2)
THEME_CHORDS = transpose_chords(CROWN_A_CHORDS, -2)
THEME_B = transpose_line(CROWN_B, -2)
THEME_B_CHORDS = transpose_chords(CROWN_B_CHORDS, -2)
SIEGE = [("Cm", 8), ("Ab", 4), ("G", 4)] * 2
ROT_STEPS = [0, 0, 1, 0, 0, 0, -6, 0]  # C C Db C C C Gb C - the Rotbound motif as a siege engine


def build():
    s = S.Song("battle_citadel", BPM, bars=32, seed=201)
    s.part("horns", "horn", gain_db=-4, pan=-0.15, send=0.34)
    s.part("trumpets", "trumpet", gain_db=-6, pan=0.2, send=0.34)
    s.part("tpt_stac", "trumpet_stac", gain_db=-9, pan=0.25, send=0.3)
    s.part("trombones", "trombone", gain_db=-7, pan=0.25, send=0.3)
    s.part("tuba", "tuba", gain_db=-9, pan=0.1, send=0.25)
    s.part("violins", "violins", gain_db=-6, pan=-0.35, send=0.3)
    s.part("violas", "violas_trem", gain_db=-11, pan=0.15, send=0.3)
    s.part("cellos", "cellos_spic", gain_db=-5, pan=0.25, send=0.2)
    s.part("basses", "basses_pizz", gain_db=-7, pan=0.3, send=0.2)
    s.part("organ", "organ", gain_db=-8, send=0.45, space="cathedral", width=1.3)
    s.part("bells", "tubular_bells", gain_db=-12, pan=0.45, send=0.5, space="cathedral", highcut=7000)
    drum_parts(s, level=1.0)

    def siege(start, prog, vel=0.64):
        # The host's motif hammered out in the low strings on each chord root.
        for beat, sym, beats in expand(prog):
            root = chord_root(sym, 42, 53)
            low = chord_root(sym, 31, 42)
            for k in range(int(beats * 2)):
                step = ROT_STEPS[k % 8]
                s.n("cellos", start + beat + k * 0.5, root + step, 0.45, vel + (0.12 if k % 4 == 0 else 0), damp=0.08)
            for k in range(0, int(beats), 2):
                s.n("basses", start + beat + k, low, 1, vel, damp=0.15)

    # S1: the siege engines - the Rotbound motif grinding below, trombones like a ram.
    siege(0, SIEGE, 0.6)
    s.line("trombones", 0, "C3:2 Db3:2 C3:2 Gb2:2 r:8 C3:2 Db3:2 C3:2 G2:2 r:8", vel=0.62)
    s.line("tuba", 0, "C2:8 Ab1:4 G1:4 C2:8 Ab1:4 G1:4", vel=0.5)
    groove(s, "driving", 0, 8, vel=0.58)
    timpani(s, 0, SIEGE, 8, every=2, vel=0.55, offbeat=False)

    hit(s, 0, crash=0.5, gong=0.45)
    fill(s, 30, 2, "big", 0.8)

    # S2: the caravan's theme answers on horns - in the minor, at the last gate.
    o = 32
    s.line("horns", o, transpose_line(THEME, -12), vel=0.84)
    ostinato(s, "cellos", o, THEME_CHORDS, [0, 0, 7, 0, 12, 0, 7, 0], step=0.5, base_low=36, base_high=48, vel=0.62)
    bass(s, "basses", o, THEME_CHORDS, rhythm=((0, 1), (2, 1)), low=28, high=40, vel=0.6)
    pad(s, "trombones", o, THEME_CHORDS, 41, 55, count=2, vel=0.52)
    pad(s, "violas", o, THEME_CHORDS, 55, 67, count=2, vel=0.42)
    pad(s, "organ", o, THEME_CHORDS, 55, 72, count=3, vel=0.4)
    groove(s, "march_full", 8, 8, vel=0.6)
    timpani(s, o, THEME_CHORDS, 8, vel=0.58)
    hit(s, o, crash=0.6)
    fill(s, o + 30, 2, "toms", 0.85)

    # S3: the breach - the second phrase on violins and trumpets, organ thundering.
    o = 64
    s.line("violins", o, THEME_B, vel=0.82)
    s.line("trumpets", o, THEME_B, vel=0.66, transpose=-12)
    pad(s, "organ", o, THEME_B_CHORDS, 55, 72, count=4, vel=0.55)
    pad(s, "horns", o, THEME_B_CHORDS, 50, 64, count=3, vel=0.58)
    ostinato(s, "cellos", o, THEME_B_CHORDS, [0, 0, 7, 0, 12, 0, 7, 0], step=0.5, base_low=36, base_high=48, vel=0.64)
    bass(s, "basses", o, THEME_B_CHORDS, rhythm=((0, 1), (2, 1)), low=28, high=40, vel=0.62)
    bass(s, "tuba", o, THEME_B_CHORDS, rhythm=((0, 4),), low=28, high=40, vel=0.55)
    groove(s, "taiko_full", 16, 8, vel=0.64)
    timpani(s, o, THEME_B_CHORDS, 8, vel=0.6)
    for bar, (beat, sym, beats) in enumerate(expand(THEME_B_CHORDS)):
        s.n("bells", o + beat, chord_root(sym, 60, 72), 3, 0.45, damp=0.5)  # the citadel's alarm bells
    hit(s, o, crash=0.7, gong=0.5)
    fill(s, o + 30, 2, "big", 0.9)

    # S4: crown against host - the theme on trumpets and violins while the motif grinds beneath.
    o = 96
    s.line("trumpets", o, THEME, vel=0.84)
    s.line("violins", o, THEME, vel=0.74)
    s.line("horns", o, transpose_line(THEME, -12), vel=0.72)
    siege(o, THEME_CHORDS, 0.62)
    pad(s, "organ", o, THEME_CHORDS, 55, 72, count=4, vel=0.5)
    pad(s, "trombones", o, THEME_CHORDS, 41, 55, count=2, vel=0.58)
    bass(s, "tuba", o, THEME_CHORDS, rhythm=((0, 2), (2, 2)), low=28, high=40, vel=0.58)
    groove(s, "driving_full", 24, 8, vel=0.66)
    timpani(s, o, THEME_CHORDS, 8, vel=0.64)
    hit(s, o, crash=0.75, gong=0.55)
    fill(s, o + 30, 2, "toms", 0.9)
    ease_out(s, ["violins", "trumpets", "horns", "organ"], o + 28, o + 32, -5)
    return s
