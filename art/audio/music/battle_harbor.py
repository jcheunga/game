"""Saltwake Docks: "Brine and Iron" - a shanty turned to war. A minor, 6/8 (eighth = 204), 32 bars."""
from ak import song as S
from music.battle_kit import drum_parts, groove
from music.common import arpeggio, bass, chord_at, chord_root, expand, pad, roll

BPM = 204
PROG_A = [("Am", 6), ("G", 6), ("Am", 6), ("Em", 6), ("Am", 6), ("G", 6), ("F", 6), ("E", 6)]
PROG_B = [("F", 6), ("G", 6), ("C", 6), ("Am", 6), ("F", 6), ("G", 6), ("Am", 6), ("E", 6)]
SHANTY = ("A4:2 A4:1 C5:2 E5:1 D5:3 B4:2 G4:1 A4:2 B4:1 C5:2 E5:1 E5:3 B4:3 "
          "A4:2 A4:1 C5:2 E5:1 G5:3 D5:2 B4:1 C5:2 A4:1 F4:2 A4:1 G#4:6")
REFRAIN = ("A5:3 G5:2 F5:1 G5:3 D5:3 E5:2 G5:1 C6:2 G5:1 A5:6 "
           "C6:3 A5:2 F5:1 D5:2 G5:1 B5:3 C6:2 B5:1 A5:2 E5:1 E5:6")


def build():
    s = S.Song("battle_harbor", BPM, bars=32, meter=6, seed=121)
    s.part("horns", "horn", gain_db=-5, pan=-0.2, send=0.34)
    s.part("trombones", "trombone", gain_db=-8, pan=0.25, send=0.3)
    s.part("tuba", "tuba", gain_db=-10, pan=0.1, send=0.25)
    s.part("violins", "violins", gain_db=-7, pan=-0.35, send=0.3)
    s.part("trem", "violins_trem", gain_db=-14, pan=-0.3, send=0.4)
    s.part("cellos", "cellos_pizz", gain_db=-6, pan=0.25, send=0.22)
    s.part("basses", "basses_pizz", gain_db=-7, pan=0.3, send=0.2)
    s.part("violas", "violas_spic", gain_db=-10, pan=0.15, send=0.22)
    s.part("bell", "tubular_bells", gain_db=-16, pan=-0.45, send=0.45, highcut=7000)
    s.part("waves", "ocean_drum", gain_db=-16, pan=0.0, send=0.2)
    s.part("tamb", "tambourine", gain_db=-20, pan=0.45, send=0.2)
    drum_parts(s)

    def rolling(start, prog, vel=0.62):
        # The swell: low strings rock root-fifth-octave like a hull in the swell.
        for beat, sym, beats in expand(prog):
            root = chord_root(sym, 33, 45)
            for k, (iv, v) in enumerate([(0, 1), (7, 0.7), (12, 0.8), (7, 0.7), (12, 0.75), (7, 0.65)]):
                s.n("cellos", start + beat + k, root + 12 + iv, 0.9, vel * v, damp=0.12)
            s.n("basses", start + beat, root, 2.5, vel, damp=0.2)
            s.n("basses", start + beat + 3, root + 7, 2.5, vel * 0.8, damp=0.2)

    for section in range(4):
        o = section * 48
        prog = PROG_B if section == 2 else PROG_A
        rolling(o, prog, 0.6 + 0.03 * section)
        # Ocean drum swell every four bars, a ship's bell at the phrase turn.
        for k in (0, 24):
            s.hit("waves", o + k, 0.5 + 0.1 * (section % 2), beats=12)
        s.n("bell", o + 42, "E5", 6, 0.35, damp=1.0)

    # S1: tuba and trombones growl the harmony, jig groove.
    pad(s, "trombones", 0, PROG_A, 41, 57, count=2, vel=0.5)
    bass(s, "tuba", 0, PROG_A, rhythm=((0, 3), (3, 3)), low=33, high=45, vel=0.55)
    groove(s, "jig", 0, 8, vel=0.55)

    # S2: the shanty on horns, violins answering with tremolo spray.
    o = 48
    s.line("horns", o, SHANTY, vel=0.78)
    pad(s, "trem", o, PROG_A, 64, 76, count=2, vel=0.4)
    pad(s, "trombones", o, PROG_A, 41, 57, count=2, vel=0.5)
    bass(s, "tuba", o, PROG_A, rhythm=((0, 3), (3, 3)), low=33, high=45, vel=0.58)
    groove(s, "jig_full", 8, 8, vel=0.58)
    s.pattern("tamb", 8, 8, "...x..", steps_per_beat=1, vel=0.45)

    # S3: the refrain - violins and horns, the whole crew singing.
    o = 96
    s.line("violins", o, REFRAIN, vel=0.8)
    s.line("horns", o, REFRAIN, vel=0.62, transpose=-12)
    pad(s, "trombones", o, PROG_B, 41, 57, count=2, vel=0.56)
    bass(s, "tuba", o, PROG_B, rhythm=((0, 3), (3, 3)), low=33, high=45, vel=0.6)
    arpeggio(s, "violas", o, PROG_B, order=(0, 1, 2, 1, 2, 1), step=1, low=55, high=69, count=3, vel=0.45, ring=1)
    groove(s, "jig_full", 16, 8, vel=0.62)
    s.pattern("tamb", 16, 8, "...x.x", steps_per_beat=1, vel=0.45)
    s.hit("crash", o, 0.6)
    roll(s, "snare", o + 42, 6, 0.3, 0.8, rate=4)

    # S4: the shanty, full broadside - violins and horns in octaves, timpani on every swell.
    o = 144
    s.line("violins", o, SHANTY, vel=0.8)
    s.line("horns", o, SHANTY, vel=0.7, transpose=-12)
    s.line("trombones", o, SHANTY, vel=0.5, transpose=-12)
    pad(s, "trem", o, PROG_A, 64, 76, count=2, vel=0.45)
    bass(s, "tuba", o, PROG_A, rhythm=((0, 3), (3, 3)), low=33, high=45, vel=0.62)
    groove(s, "jig_full", 24, 8, vel=0.64)
    for bar in range(8):
        s.n("timp", o + bar * 6, chord_root(chord_at(PROG_A, bar * 6), 41, 55), 1, 0.6)
        s.n("timp", o + bar * 6 + 3, chord_root(chord_at(PROG_A, bar * 6), 41, 55, prefer_fifth=True), 1, 0.45)
    s.hit("crash", o, 0.7)
    s.hit("gong", o, 0.3)
    roll(s, "timp", o + 42, 6, 0.3, 0.8, rate=4, pitch="E2")
    return s
