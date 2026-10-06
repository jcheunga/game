"""Emberforge March: "Hammer and Furnace" - iron, fire and toil. E Phrygian, 132 bpm, 32 bars."""
from ak import song as S
from ak import theory as T
from music.battle_kit import drum_parts, fill, groove, hit, timpani
from music.common import bass, chord_root, expand, pad

BPM = 132
PROG_A = [("Em", 4), ("F", 4), ("Em", 4), ("Dm", 2), ("F", 2)] * 2
PROG_B = [("C", 4), ("D", 4), ("Em", 4), ("F", 4), ("C", 4), ("D", 4), ("F", 4), ("E", 4)]
# The forge riff in E Phrygian scale steps from each chord's root (E E F E G F E D on Em).
RIFF_STEPS = [0, 0, 1, 0, 2, 1, 0, -1]
SCALE = T.scale_notes("E", "phrygian", 28, 70)
HORN = ("E4:1.5 F4:.5 G4:1 B4:1 C5:2 B4:1 A4:1 B4:1.5 A4:.5 G4:1 F4:1 D4:2 F4:2 "
        "E4:1.5 F4:.5 G4:1 B4:1 C5:2 D5:1 E5:1 D5:1 C5:1 B4:1 A4:1 F4:2 A4:2")
TUNE_B = ("G5:2 E5:1 C5:1 A5:2 F#5:2 B5:1.5 A5:.5 G5:1 E5:1 A5:2 F5:2 "
          "G5:2 E5:1 C5:1 F#5:2 A5:2 A5:1 G5:1 F5:1 C5:1 G#5:4")


def anvil_part(s):
    s.part("anvil", "anvil", gain_db=-15, pan=0.35, send=0.3, human=0.003)


def build():
    s = S.Song("battle_foundry", BPM, bars=32, seed=131)
    s.part("tuba", "tuba", gain_db=-8, pan=0.1, send=0.2)
    s.part("trombones", "trombone", gain_db=-7, pan=0.25, send=0.28)
    s.part("horns", "horn", gain_db=-5, pan=-0.2, send=0.32)
    s.part("trumpets", "trumpet_stac", gain_db=-9, pan=0.3, send=0.3)
    s.part("cellos", "cellos_spic", gain_db=-6, pan=0.2, send=0.2)
    s.part("basses", "basses_pizz", gain_db=-8, pan=0.3, send=0.2)
    s.part("violins", "violins", gain_db=-7, pan=-0.35, send=0.28)
    s.part("violas", "violas_trem", gain_db=-13, pan=0.15, send=0.3)
    anvil_part(s)
    drum_parts(s, level=1.0)

    def riff(start, prog, vel=0.62):
        for beat, sym, beats in expand(prog):
            root = chord_root(sym, 40, 51)
            idx = SCALE.index(root) if root in SCALE else min(range(len(SCALE)), key=lambda i: abs(SCALE[i] - root))
            for k in range(int(beats * 2)):
                m = SCALE[idx + RIFF_STEPS[k % 8]]
                s.n("cellos", start + beat + k * 0.5, m, 0.45, vel + (0.12 if k % 8 == 0 else 0), damp=0.1)
            for k in range(0, int(beats), 2):
                s.n("basses", start + beat + k, root - 12, 1, vel, damp=0.15)

    def hammers(start_bar, bars, vel=0.55):
        s.pattern("anvil", start_bar, bars, "....X.....x.X...", steps_per_beat=4, vel=vel)

    # S1: the forge wakes - riff, anvils, forge groove.
    riff(0, PROG_A, 0.6)
    pad(s, "trombones", 0, PROG_A, 40, 55, count=2, vel=0.48)
    hammers(0, 8, 0.5)
    groove(s, "forge", 0, 8, vel=0.58)
    fill(s, 30, 2, "toms", 0.75)

    # S2: horns forge the melody, tuba anchors.
    o = 32
    s.line("horns", o, HORN, vel=0.8)
    riff(o, PROG_A, 0.64)
    bass(s, "tuba", o, PROG_A, rhythm=((0, 2), (2, 2)), low=28, high=40, vel=0.6)
    pad(s, "trombones", o, PROG_A, 40, 55, count=2, vel=0.5)
    pad(s, "violas", o, PROG_A, 52, 64, count=2, vel=0.4)
    hammers(8, 8, 0.58)
    groove(s, "forge", 8, 8, vel=0.62)
    timpani(s, o, PROG_A, 8, vel=0.55)
    hit(s, o, crash=0.6)
    fill(s, o + 30, 2, "big", 0.85)

    # S3: furnace bridge - violins soar, trumpets hammer stabs.
    o = 64
    s.line("violins", o, TUNE_B, vel=0.8)
    pad(s, "horns", o, PROG_B, 50, 64, count=3, vel=0.58)
    bass(s, "tuba", o, PROG_B, rhythm=((0, 1.5), (1.5, 1), (2.5, 1.5)), low=28, high=40, vel=0.62)
    pad(s, "trombones", o, PROG_B, 40, 55, count=2, vel=0.55, rearticulate=2)
    for bar, (sym, _) in enumerate(PROG_B):
        stab = {"C": ["C4", "E4", "G4"], "D": ["D4", "F#4", "A4"], "Em": ["E4", "G4", "B4"], "F": ["F4", "A4", "C5"],
                "E": ["E4", "G#4", "B4"]}[sym]
        for off in (0, 1.5, 3):
            s.n("trumpets", o + bar * 4 + off, stab, 0.5, 0.55, damp=0.1)
    hammers(16, 8, 0.6)
    groove(s, "driving", 16, 8, vel=0.62)
    timpani(s, o, PROG_B, 8, vel=0.58)
    hit(s, o, crash=0.65)
    fill(s, o + 30, 2, "toms", 0.85)

    # S4: full pour - horns and violins in octaves over the riff; gong.
    o = 96
    s.line("horns", o, HORN, vel=0.82)
    s.line("violins", o, HORN, vel=0.72, transpose=12)
    riff(o, PROG_A, 0.68)
    bass(s, "tuba", o, PROG_A, rhythm=((0, 2), (2, 2)), low=28, high=40, vel=0.64)
    pad(s, "trombones", o, PROG_A, 40, 55, count=2, vel=0.58)
    pad(s, "violas", o, PROG_A, 52, 64, count=2, vel=0.45)
    hammers(24, 8, 0.62)
    groove(s, "driving_full", 24, 8, vel=0.64)
    timpani(s, o, PROG_A, 8, vel=0.62)
    hit(s, o, crash=0.75, gong=0.45)
    fill(s, o + 30, 2, "big", 0.85)
    return s
