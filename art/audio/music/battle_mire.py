"""Mire of Saints: "Drowned Chapels" - a heavy, lurching slog through the bog. C minor, 92 bpm, 32 bars."""
from ak import song as S
from ak.song import ease_out
from music.battle_kit import drum_parts, fill, groove, hit, timpani
from music.common import bass, chord_at, chord_root, expand, pad

BPM = 92
PROG_A = [("Cm", 4), ("Ab", 2), ("Cm", 2), ("Fm", 2), ("G", 2), ("Cm", 4),
          ("Cm", 4), ("Bb", 2), ("Ab", 2), ("G7", 4), ("Cm", 4)]
BASSOON = ("C3:1.5 Eb3:.5 F3:1 G3:1 Ab3:2 G3:2 F3:1.5 Eb3:.5 D3:1 B2:1 C3:4 "
           "C3:1.5 Eb3:.5 G3:1 C4:1 Bb3:2 Ab3:2 G3:1.5 F3:.5 D3:1 B2:1 C3:4")
PROG_B = [("Fm", 4), ("Ab", 4), ("Eb", 4), ("G", 4), ("Fm", 4), ("Db", 4), ("G", 4), ("G", 4)]
HORN_B = "F4:2 Ab4:2 Eb4:2 C4:2 Bb3:2 G3:2 B3:2 D4:2 F4:2 C4:2 Db4:2 F4:2 D4:2 B3:2 G3:4"


def build():
    s = S.Song("battle_mire", BPM, bars=32, seed=171)
    s.part("bassoon", "bassoon", gain_db=-3, pan=-0.15, send=0.35, space="cave")
    s.part("horns", "horn", gain_db=-6, pan=-0.2, send=0.4, space="cave")
    s.part("tuba", "tuba", gain_db=-9, pan=0.1, send=0.3, space="cave")
    s.part("basses", "basses", gain_db=-10, pan=0.3, send=0.3, space="cave")
    s.part("pizz", "cellos_pizz", gain_db=-5, pan=0.25, send=0.3, space="cave")
    s.part("bpizz", "basses_pizz", gain_db=-6, pan=0.3, send=0.25, space="cave")
    s.part("trem", "violins_trem", gain_db=-15, pan=-0.35, send=0.45, space="cave")
    s.part("violas", "violas", gain_db=-12, pan=0.15, send=0.4, space="cave")
    s.part("violins", "violins", gain_db=-8, pan=-0.35, send=0.4, space="cave")
    s.part("glass", "wine_glass", gain_db=-24, pan=0.5, send=0.5, space="cave")
    s.part("bowed", "brake_drum_bowed", gain_db=-24, pan=-0.5, send=0.5, space="cave")
    s.part("muted", "frame_drum_muted", gain_db=-9, pan=-0.15, send=0.25, space="cave", human=0.006)
    drum_parts(s, space="cave")

    def lurch(start, prog, vel=0.62):
        # 3+3+2: the bog sucks at every step.
        for beat, sym, beats in expand(prog):
            root = chord_root(sym, 36, 47)
            low = chord_root(sym, 31, 42)
            for bar in range(int(beats // 4) or 1):
                for off, iv, v in ((0, 0, 1), (1.5, 0, 0.75), (3, 7, 0.85)):
                    pos = bar * 4 + off
                    if pos < beats:
                        s.n("pizz", start + beat + pos, root + iv, 1, vel * v, damp=0.2)
                        s.n("bpizz", start + beat + pos, low + iv, 1, vel * v, damp=0.25)
            s.pattern("muted", int((start + beat) // 4), max(1, int(beats // 4)), "X.....x.....x...", steps_per_beat=4,
                      vel=0.55)

    # S1: the bog at dusk - lurching pizzicato, a bowed-iron moan, high tremolo mist.
    lurch(0, PROG_A, 0.58)
    pad(s, "basses", 0, PROG_A, 28, 40, count=1, vel=0.45)
    pad(s, "trem", 0, PROG_A, 67, 79, count=2, vel=0.35)
    s.hit("bowed", 8, 0.5, beats=8)
    groove(s, "halftime", 0, 8, vel=0.55, skip=("snare",))
    fill(s, 30, 2, "toms", 0.7)

    # S2: the bassoon trudges in with the tune; tuba below.
    o = 32
    s.line("bassoon", o, BASSOON, vel=0.82)
    lurch(o, PROG_A, 0.62)
    bass(s, "tuba", o, PROG_A, rhythm=((0, 4),), low=28, high=40, vel=0.55)
    pad(s, "violas", o, PROG_A, 53, 65, count=2, vel=0.4)
    groove(s, "halftime", 8, 8, vel=0.6)
    timpani(s, o, PROG_A, 8, every=2, vel=0.5, offbeat=False)
    s.line("glass", o + 16, "G5:6 r:10", vel=0.45, damp=0.3)
    fill(s, o + 30, 2, "big", 0.8)

    # S3: drowned chapels - horns in the fog, strings rising.
    o = 64
    s.line("horns", o, HORN_B, vel=0.78)
    pad(s, "violas", o, PROG_B, 53, 67, count=2, vel=0.48)
    pad(s, "trem", o, PROG_B, 67, 79, count=2, vel=0.42)
    lurch(o, PROG_B, 0.64)
    bass(s, "tuba", o, PROG_B, rhythm=((0, 2), (2, 2)), low=28, high=40, vel=0.58)
    groove(s, "taiko", 16, 8, vel=0.6)
    timpani(s, o, PROG_B, 8, vel=0.55)
    hit(s, o, crash=0.5, gong=0.4)
    s.hit("bowed", o + 16, 0.5, beats=8)
    fill(s, o + 30, 2, "toms", 0.85)

    # S4: the tune in full - bassoon, violins and horns, the swamp heaving.
    o = 96
    s.line("bassoon", o, BASSOON, vel=0.8)
    s.line("violins", o, BASSOON, vel=0.74, transpose=24)
    s.line("horns", o, BASSOON, vel=0.66, transpose=12)
    lurch(o, PROG_A, 0.66)
    bass(s, "tuba", o, PROG_A, rhythm=((0, 2), (2, 2)), low=28, high=40, vel=0.6)
    pad(s, "violas", o, PROG_A, 53, 65, count=2, vel=0.48)
    groove(s, "taiko_full", 24, 8, vel=0.62)
    timpani(s, o, PROG_A, 8, vel=0.6)
    hit(s, o, crash=0.65, gong=0.5)
    fill(s, o + 30, 2, "big", 0.85)
    ease_out(s, ["violins", "horns", "bassoon", "violas", "tuba"], o + 28, o + 32, -6)
    return s
