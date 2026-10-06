"""Boss: "The Grave Lords" - the Rotbound host's motif at full terror. D minor/Phrygian, 140 bpm, 32 bars.

The `final` variant (Crownfall's sovereigns) drops a tone, adds the organ and tubular bells, and states the
caravan's theme against the host in the last section."""
from ak import song as S
from ak.song import ease_out
from music.battle_kit import drum_parts, fill, groove, hit, timpani
from music.common import (CROWN_HEAD, bass, chord_root, expand, pad, transpose_chords, transpose_line)

BPM = 140
PROG_A = [("Dm", 4), ("Bb", 4), ("Gm", 4), ("Eb", 4), ("Dm", 4), ("Bb", 4), ("Gm", 4), ("A", 4)]
LEAD = ("D5:1.5 Eb5:.5 D5:1 A4:1 Bb4:1.5 A4:.5 F4:1 D4:1 G4:1 Bb4:1 D5:1 G5:1 Eb5:2 G5:1 Bb5:1 "
        "A5:2 F5:1 D5:1 D5:1.5 C5:.5 Bb4:1 F4:1 G4:1 A4:1 Bb4:1 D5:1 C#5:2 E5:1 A4:1")
# Harmonised under the swollen motif (D Eb D Ab D Eb E C#): chromatic-mediant dread.
PEDAL = [("Dm", 4), ("Eb", 4), ("Dm", 4), ("Ab", 4), ("Dm", 4), ("Eb", 4), ("A", 4), ("A", 4)]
ROT_STEPS = [0, 0, 1, 0, 0, 0, 6, 0]


def build(final=False):
    shift = -2 if final else 0
    s = S.Song("battle_boss_final" if final else "battle_boss", 132 if final else BPM, bars=32, seed=211 + final)
    prog_a = transpose_chords(PROG_A, shift)
    pedal = transpose_chords(PEDAL, shift)
    lead = transpose_line(LEAD, shift)
    s.part("horns", "horn", gain_db=-4, pan=-0.15, send=0.32)
    s.part("trumpets", "trumpet", gain_db=-6, pan=0.2, send=0.32)
    s.part("tpt_stac", "trumpet_stac", gain_db=-8, pan=0.25, send=0.3)
    s.part("trombones", "trombone", gain_db=-6, pan=0.25, send=0.3)
    s.part("tuba", "tuba", gain_db=-8, pan=0.1, send=0.25)
    s.part("violins", "violins", gain_db=-6, pan=-0.35, send=0.3)
    s.part("trem", "violins_trem", gain_db=-10, pan=-0.3, send=0.35)
    s.part("vla_trem", "violas_trem", gain_db=-11, pan=0.2, send=0.3)
    s.part("cellos", "cellos_spic", gain_db=-4, pan=0.25, send=0.2)
    s.part("basses", "basses_pizz", gain_db=-6, pan=0.3, send=0.2)
    if final:
        s.part("organ", "organ", gain_db=-11, send=0.45, space="cathedral", width=1.3)
        s.part("bells", "tubular_bells", gain_db=-14, pan=0.45, send=0.5, space="cathedral", highcut=7000)
    drum_parts(s, level=2.0)

    def engine(start, prog, vel=0.66):
        for beat, sym, beats in expand(prog):
            root = chord_root(sym, 38, 49)
            low = chord_root(sym, 31, 42)
            for k in range(int(beats * 2)):
                s.n("cellos", start + beat + k * 0.5, root + ROT_STEPS[k % 8], 0.45, vel + (0.14 if k % 4 == 0 else 0),
                    damp=0.08)
            for k in range(0, int(beats), 2):
                s.n("basses", start + beat + k, low, 1, vel, damp=0.15)

    def stabs(start, prog, vel=0.55):
        from ak import theory as T
        for beat, sym, beats in expand(prog):
            tones = T.voice(sym, 58, 72, 3)
            for k in range(0, int(beats), 4):
                for off in (0, 0.75, 1.5, 3):
                    s.n("tpt_stac", start + beat + k + off, tones, 0.4, vel + (0.1 if off == 0 else 0), damp=0.08)

    # S1: the grave opens - the motif grinding, low brass sighs, war drums.
    engine(0, prog_a, 0.62)
    s.line("trombones", 0, transpose_line("D3:2 Eb3:2 D3:2 Ab2:2 r:8 D3:2 Eb3:2 D3:2 Bb2:2 r:8", shift), vel=0.66)
    bass(s, "tuba", 0, prog_a, rhythm=((0, 4),), low=28, high=40, vel=0.55)
    pad(s, "trem", 0, prog_a, 67, 79, count=2, vel=0.38)
    groove(s, "taiko", 0, 8, vel=0.62)
    timpani(s, 0, prog_a, 8, vel=0.55)
    hit(s, 0, crash=0.55, gong=0.55)
    fill(s, 30, 2, "big", 0.85)

    # S2: the lord's theme on horns and trumpets, stabs and timpani.
    o = 32
    s.line("horns", o, transpose_line(lead, -12), vel=0.86)
    s.line("trumpets", o + 16, transpose_line(LEAD[LEAD.index("A5:2 F5:1"):], shift - 12), vel=0.62)
    engine(o, prog_a, 0.66)
    stabs(o, prog_a, 0.5)
    bass(s, "tuba", o, prog_a, rhythm=((0, 2), (2, 2)), low=28, high=40, vel=0.6)
    groove(s, "taiko_full", 8, 8, vel=0.66)
    timpani(s, o, prog_a, 8, vel=0.6)
    hit(s, o, crash=0.65)
    fill(s, o + 30, 2, "toms", 0.9)

    # S3: the host gathers - tremolo over a pedal, the motif swollen in the brass.
    o = 64
    s.line("horns", o, transpose_line("D4:4 Eb4:4 D4:4 Ab3:4 D4:4 Eb4:4 E4:4 C#4:4", shift), vel=0.82)
    s.line("trombones", o, transpose_line("D3:4 Eb3:4 D3:4 Ab2:4 D3:4 Eb3:4 E3:4 C#3:4", shift), vel=0.7)
    pad(s, "vla_trem", o, pedal, 55, 67, count=2, vel=0.5)
    pad(s, "trem", o, pedal, 67, 79, count=2, vel=0.48)
    engine(o, pedal, 0.66)
    groove(s, "driving_full", 16, 8, vel=0.66)
    timpani(s, o, pedal, 8, vel=0.6)
    hit(s, o, crash=0.7, gong=0.6)
    if final:
        pad(s, "organ", o, pedal, 55, 72, count=4, vel=0.55)
        for bar in range(0, 8, 2):
            s.n("bells", o + bar * 4, chord_root(pedal[bar][0], 60, 72), 3, 0.45, damp=0.5)
    fill(s, o + 30, 2, "big", 0.95)

    # S4: the full onslaught - violins and trumpets on the lord's theme (the caravan's head motif answers in the final).
    o = 96
    s.line("trumpets", o, lead, vel=0.86)
    s.line("violins", o, lead, vel=0.76)
    if final:
        # The caravan's rising head motif, bent to fit the host's harmony each time it answers.
        for k, head in enumerate(("D4:1.5 E4:.5 F4:1 G4:1 Bb4:4", "G4:1.5 A4:.5 Bb4:1 C5:1 Eb5:4",
                                  "D4:1.5 E4:.5 F4:1 G4:1 Bb4:4", "G4:1.5 A4:.5 Bb4:1 C5:1 C#5:4")):
            s.line("horns", o + k * 8, transpose_line(head, shift), vel=0.8 + 0.02 * k)
        pad(s, "organ", o, prog_a, 55, 72, count=4, vel=0.5)
    else:
        s.line("horns", o, transpose_line(lead, -12), vel=0.72)
    engine(o, prog_a, 0.68)
    stabs(o, prog_a, 0.55)
    bass(s, "tuba", o, prog_a, rhythm=((0, 2), (2, 2)), low=28, high=40, vel=0.62)
    groove(s, "driving_full", 24, 8, vel=0.68)
    timpani(s, o, prog_a, 8, vel=0.64)
    hit(s, o, crash=0.75, gong=0.6)
    fill(s, o + 30, 2, "toms", 0.95)
    ease_out(s, ["violins", "trumpets", "horns"] + (["organ"] if final else []), o + 28, o + 32, -5)
    return s
