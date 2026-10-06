"""King's Road: "Banners on the King's Road" - bright, heroic, pastoral steel. D Dorian, 126 bpm, 32 bars."""
from ak import song as S
from music.battle_kit import drum_parts, fill, groove, hit, timpani
from music.common import bass, ostinato, pad, transpose_line

BPM = 126
PROG_A = [("Dm", 4), ("C", 4), ("Bb", 4), ("C", 4), ("Dm", 4), ("C", 4), ("Bb", 4), ("A", 4)]
PROG_B = [("F", 4), ("C", 4), ("Gm", 4), ("A", 4), ("F", 4), ("C", 4), ("Bb", 4), ("A", 4)]
HORN_A = ("D4:1.5 E4:.5 F4:1 G4:1 A4:2 G4:1 E4:1 F4:1.5 G4:.5 A4:1 D5:1 C5:4 "
          "D5:1.5 C5:.5 A4:1 F4:1 G4:2 E4:1 C4:1 D4:1.5 E4:.5 F4:1 Bb4:1 A4:4")
TUNE_B = ("A5:2 C6:2 G5:2 E5:2 Bb5:1.5 A5:.5 G5:1 D5:1 C#5:2 E5:2 "
          "F5:1 G5:1 A5:2 G5:1 F5:1 E5:2 D5:1 E5:1 F5:1 G5:1 A5:4")
RECORDER = "A5:1 G5:1 F5:1 E5:1 G5:2 E5:2 F5:1 D5:1 F5:1 A5:1 G5:4 A5:1 G5:1 F5:1 D5:1 E5:2 C5:2 D5:4 C#5:4"


def build():
    s = S.Song("battle_road", BPM, bars=32, seed=111)
    s.part("violins", "violins", gain_db=-7, pan=-0.35, send=0.28)
    s.part("vln_spic", "violins_spic", gain_db=-11, pan=-0.3, send=0.22)
    s.part("vla_spic", "violas_spic", gain_db=-10, pan=0.2, send=0.22)
    s.part("cellos", "cellos_spic", gain_db=-7, pan=0.25, send=0.2)
    s.part("basses", "basses", gain_db=-10, pan=0.3, send=0.2)
    s.part("horns", "horn", gain_db=-5, pan=-0.15, send=0.34)
    s.part("trumpets", "trumpet", gain_db=-7, pan=0.2, send=0.34)
    s.part("tpt_stac", "trumpet_stac", gain_db=-10, pan=0.25, send=0.3)
    s.part("trombones", "trombone", gain_db=-9, pan=0.25, send=0.3)
    s.part("recorder", "recorder_soprano", gain_db=-10, pan=-0.45, send=0.3)
    drum_parts(s)

    # S1: marching column - cello engine, trumpet stabs on the off-beats, snare march.
    ostinato(s, "cellos", 0, PROG_A, [0, 0, 12, 0, 7, 0, 12, 7], step=0.5, base_low=38, base_high=50, vel=0.6)
    ostinato(s, "vla_spic", 0, PROG_A, [None, "3", 7, "3"], step=0.5, base_low=50, base_high=62, vel=0.48)
    bass(s, "basses", 0, PROG_A, rhythm=((0, 4),), low=31, high=43, vel=0.52)
    pad(s, "trombones", 0, PROG_A, 41, 58, count=2, vel=0.45)
    stab_chords = {"Dm": ["D4", "F4", "A4"], "C": ["C4", "E4", "G4"], "Bb": ["D4", "F4", "Bb4"], "A": ["C#4", "E4", "A4"]}
    for bar, (sym, _) in enumerate(PROG_A):
        s.n("tpt_stac", bar * 4 + 1.5, stab_chords[sym], 0.5, 0.5, damp=0.12)
        s.n("tpt_stac", bar * 4 + 3.5, stab_chords[sym], 0.4, 0.45, damp=0.12)
    groove(s, "march", 0, 8, vel=0.55)
    timpani(s, 0, PROG_A, 8, every=2, vel=0.5, offbeat=False)
    fill(s, 30, 2, "snare", 0.7)

    # S2: the horns' road tune, recorder piping above in the second half.
    o = 32
    s.line("horns", o, HORN_A, vel=0.78)
    s.line("recorder", o + 16, RECORDER[RECORDER.index("A5:1 G5:1 F5:1 D5:1"):], vel=0.5)
    ostinato(s, "cellos", o, PROG_A, [0, 0, 12, 0, 7, 0, 12, 7], step=0.5, base_low=38, base_high=50, vel=0.62)
    ostinato(s, "vla_spic", o, PROG_A, [0, "3", 7, "3"], step=0.5, base_low=50, base_high=62, vel=0.5)
    bass(s, "basses", o, PROG_A, rhythm=((0, 2), (2, 2)), low=31, high=43, vel=0.56)
    groove(s, "march_full", 8, 8, vel=0.58)
    timpani(s, o, PROG_A, 8, vel=0.55)
    hit(s, o, crash=0.6)
    fill(s, o + 30, 2, "toms", 0.75)

    # S3: the banner strain - violins and trumpets, horns in harmony.
    o = 64
    s.line("violins", o, TUNE_B, vel=0.78)
    s.line("trumpets", o, TUNE_B, vel=0.62, transpose=-12)
    pad(s, "horns", o, PROG_B, 50, 65, count=3, vel=0.56)
    ostinato(s, "cellos", o, PROG_B, [0, 0, 12, 0, 7, 0, 12, 7], step=0.5, base_low=38, base_high=50, vel=0.62)
    ostinato(s, "vln_spic", o, PROG_B, [12, 7, "10", 7], step=0.5, base_low=62, base_high=74, vel=0.4)
    bass(s, "basses", o, PROG_B, rhythm=((0, 2), (2, 2)), low=31, high=43, vel=0.58)
    groove(s, "driving", 16, 8, vel=0.6)
    timpani(s, o, PROG_B, 8, vel=0.58)
    hit(s, o, crash=0.65)
    fill(s, o + 30, 2, "big", 0.85)

    # S4: full charge - the horn tune on trumpets and violins, recorder descant, timpani.
    o = 96
    s.line("trumpets", o, transpose_line(HORN_A, 12), vel=0.8)
    s.line("violins", o, transpose_line(HORN_A, 12), vel=0.7)
    s.line("horns", o, HORN_A, vel=0.66)
    s.line("recorder", o, RECORDER, vel=0.48)
    pad(s, "trombones", o, PROG_A, 41, 58, count=2, vel=0.58)
    ostinato(s, "cellos", o, PROG_A, [0, 0, 12, 0, 7, 0, 12, 7], step=0.5, base_low=38, base_high=50, vel=0.66)
    ostinato(s, "vla_spic", o, PROG_A, [0, "3", 7, "3"], step=0.5, base_low=50, base_high=62, vel=0.55)
    bass(s, "basses", o, PROG_A, rhythm=((0, 2), (2, 2)), low=31, high=43, vel=0.62)
    groove(s, "taiko_full", 24, 8, vel=0.64)
    timpani(s, o, PROG_A, 8, vel=0.62)
    hit(s, o, crash=0.75, gong=0.35)
    fill(s, o + 30, 2, "toms", 0.8)
    return s
