"""Thornwall Pass: "Avalanche Horns" - cliffs, watch-fires and echoing calls. G minor, 116 bpm, 32 bars."""
from ak import song as S
from ak import dsp
from ak.song import ease_out
from music.battle_kit import drum_parts, fill, groove, hit, timpani
from music.common import bass, ostinato, pad

BPM = 116
PROG_A = [("Gm", 4), ("Eb", 4), ("Bb", 4), ("F", 4), ("Gm", 4), ("Eb", 4), ("Cm", 4), ("D", 4)]
PROG_B = [("Cm", 4), ("Gm", 4), ("Eb", 4), ("D", 4), ("Cm", 4), ("Bb", 4), ("Eb", 4), ("D", 4)]
CALL = "D4:1.5 G4:.5 G4:2 Eb4:1.5 G4:.5 Bb4:2"  # the watch-fire call (used in echoes)
HORN = ("G4:1.5 A4:.5 Bb4:1 D5:1 Eb5:2 D5:1 Bb4:1 D5:1.5 C5:.5 Bb4:1 F4:1 A4:4 "
        "G4:1.5 A4:.5 Bb4:1 D5:1 G5:2 F5:1 Eb5:1 Eb5:1.5 D5:.5 C5:1 G4:1 F#4:4")
TUNE_B = ("G5:2 Eb5:1 C5:1 D5:2 Bb4:2 Eb5:1.5 F5:.5 G5:1 Bb5:1 A5:4 "
          "G5:2 Eb5:1 C5:1 D5:2 F5:2 G5:1.5 F5:.5 Eb5:1 Bb4:1 A4:2 F#5:2")


def build():
    s = S.Song("battle_pass", BPM, bars=32, seed=151)
    s.part("horns", "horn", gain_db=-4, pan=-0.2, send=0.42, space="great_hall")
    s.part("horns_echo", "horn", gain_db=-14, pan=0.6, send=0.6, space="great_hall", highcut=3500)
    s.part("trumpets", "trumpet", gain_db=-8, pan=0.2, send=0.4, space="great_hall")
    s.part("trombones", "trombone", gain_db=-8, pan=0.25, send=0.32, space="great_hall")
    s.part("violins", "violins", gain_db=-7, pan=-0.35, send=0.32, space="great_hall")
    s.part("violas", "violas", gain_db=-11, pan=0.15, send=0.32, space="great_hall")
    s.part("cellos", "cellos_spic", gain_db=-7, pan=0.25, send=0.25, space="great_hall")
    s.part("basses", "basses", gain_db=-10, pan=0.3, send=0.25, space="great_hall")
    s.part("flute", "flute", gain_db=-10, pan=-0.45, send=0.4, space="great_hall")
    drum_parts(s, space="great_hall", level=1.0)

    def taiko_strings(start, prog, vel=0.6):
        # 3+3+2 sixteenths on root, fifth and octave - war drums in the strings.
        ostinato(s, "cellos", start, prog, [0, None, None, 7, None, None, 12, None], step=0.25, base_low=38,
                 base_high=50, vel=vel)

    def call(beat, vel=0.7):
        # A horn call answered by its echo off the far cliff.
        s.line("horns", beat, CALL, vel=vel)
        s.line("horns_echo", beat + 1.5, CALL, vel=vel * 0.8)

    # S1: open fifths across the valley, the call and its echo, war drums far off.
    pad(s, "violas", 0, PROG_A, 55, 67, count=2, vel=0.42)
    pad(s, "trombones", 0, PROG_A, 41, 55, count=2, vel=0.45)
    bass(s, "basses", 0, PROG_A, rhythm=((0, 4),), low=31, high=43, vel=0.5)
    call(0, 0.62)
    call(16, 0.66)
    groove(s, "taiko", 0, 8, vel=0.52, skip=("sticks",))
    timpani(s, 0, PROG_A, 8, every=2, vel=0.45, offbeat=False)
    fill(s, 30, 2, "toms", 0.75)

    # S2: horns climb the pass, flute like wind over the ridge.
    o = 32
    s.line("horns", o, HORN, vel=0.8)
    s.line("flute", o + 16, "D6:4 Bb5:4 C6:4 A5:4", vel=0.5)
    taiko_strings(o, PROG_A, 0.62)
    pad(s, "trombones", o, PROG_A, 41, 55, count=2, vel=0.5)
    bass(s, "basses", o, PROG_A, rhythm=((0, 2), (2, 2)), low=31, high=43, vel=0.56)
    groove(s, "taiko_full", 8, 8, vel=0.6, skip=("snare",))
    timpani(s, o, PROG_A, 8, vel=0.55)
    hit(s, o, crash=0.6)
    fill(s, o + 30, 2, "toms", 0.85)

    # S3: the watch-forts answer - violins and trumpets, calls ringing behind.
    o = 64
    s.line("violins", o, TUNE_B, vel=0.8)
    s.line("trumpets", o, TUNE_B, vel=0.6, transpose=-12)
    pad(s, "horns", o, PROG_B, 50, 64, count=3, vel=0.55)
    taiko_strings(o, PROG_B, 0.64)
    bass(s, "basses", o, PROG_B, rhythm=((0, 2), (2, 2)), low=31, high=43, vel=0.58)
    groove(s, "taiko_full", 16, 8, vel=0.62, skip=("snare",))
    timpani(s, o, PROG_B, 8, vel=0.58)
    hit(s, o, crash=0.65)
    fill(s, o + 30, 2, "toms", 0.85)

    # S4: avalanche - the horn tune in full with violins an octave up, gong at the crest.
    o = 96
    s.line("horns", o, HORN, vel=0.84)
    s.line("violins", o, HORN, vel=0.74)
    s.line("flute", o, HORN, vel=0.48, transpose=12)
    taiko_strings(o, PROG_A, 0.66)
    pad(s, "trombones", o, PROG_A, 41, 55, count=2, vel=0.6)
    bass(s, "basses", o, PROG_A, rhythm=((0, 2), (2, 2)), low=31, high=43, vel=0.62)
    groove(s, "taiko_full", 24, 8, vel=0.66, skip=("snare",))
    timpani(s, o, PROG_A, 8, vel=0.64)
    hit(s, o, crash=0.75, gong=0.5)
    fill(s, o + 30, 2, "big", 0.9)
    ease_out(s, ["violins", "horns", "flute", "trombones", "cellos"], o + 28, o + 32, -6)
    return s
