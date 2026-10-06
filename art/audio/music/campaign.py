"""Campaign / home map: "The Caravan Road" - a travelling tune. D Mixolydian, 6/8 (eighth = 198), 40 bars."""
from ak import song as S
from music.common import arpeggio, bass, chord_at, expand, pad, roll

BPM = 198  # eighth notes; one bar = 6 eighths

TUNE_A = ("D5:2 E5:1 F#5:2 G5:1 A5:3 F#5:2 D5:1 E5:2 F#5:1 G5:2 E5:1 D5:3 A4:3 "
          "D5:2 E5:1 F#5:2 A5:1 B5:3 A5:2 F#5:1 G5:2 F#5:1 E5:2 C5:1 D5:6")
CHORDS_A = [("D", 6), ("D", 6), ("Em", 6), ("D", 6), ("D", 6), ("G", 6), ("C", 6), ("D", 6)]
TUNE_B = ("A5:2 B5:1 C6:2 B5:1 A5:3 G5:3 F#5:2 G5:1 A5:2 F#5:1 E5:6 "
          "G5:2 A5:1 B5:2 G5:1 A5:2 F#5:1 D5:3 E5:2 C5:1 E5:2 F#5:1 D5:6")
CHORDS_B = [("Am", 6), ("G", 6), ("D", 6), ("A", 6), ("G", 6), ("D", 6), ("C", 6), ("D", 6)]
# A lower harmony to TUNE_B (mostly thirds/sixths below) for the second pass.
TUNE_B_LOW = ("F#5:2 G5:1 A5:2 G5:1 E5:3 D5:3 D5:2 E5:1 F#5:2 D5:1 C#5:6 "
              "D5:2 F#5:1 G5:2 D5:1 F#5:2 D5:1 A4:3 C5:2 A4:1 C5:2 A4:1 A4:6")
INTRO = [("D", 6), ("C", 6), ("G", 6), ("D", 6)]


def strum(s, part, start, prog, vel=0.55, light=True):
    """Strumstick: down-strum on 1, lighter up-strum on 4, a brush on 6."""
    from ak import theory as T
    prev = None
    for beat, sym, beats in expand(prog):
        tones = T.voice(sym, 50, 69, 3, prev)
        prev = tones
        for bar in range(int(beats // 6)):
            b = start + beat + bar * 6
            s.n(part, b, tones, 3, vel, damp=0.25)
            s.n(part, b + 3, list(reversed(tones)), 2, vel * 0.75, damp=0.25)
            if light:
                s.n(part, b + 5, tones[-2:], 1, vel * 0.5, damp=0.2)


def build():
    s = S.Song("campaign", BPM, bars=40, meter=6, seed=21)
    s.part("strum", "strumstick", gain_db=-7, pan=0.3, send=0.22, strum=0.014, space="chamber")
    s.part("harp", "folk_harp", gain_db=-8, pan=-0.3, send=0.28, space="chamber")
    s.part("alto", "recorder_alto", gain_db=-3, pan=-0.1, send=0.26, space="chamber")
    s.part("soprano", "recorder_soprano", gain_db=-6, pan=0.15, send=0.28, space="chamber")
    s.part("drone", "cellos", gain_db=-13, pan=0.0, send=0.25, space="chamber")
    s.part("violas", "violas", gain_db=-12, pan=0.25, send=0.3, space="chamber")
    s.part("pizz", "cellos_pizz", gain_db=-8, pan=0.1, send=0.2, space="chamber")
    s.part("frame", "frame_drum", gain_db=-10, pan=-0.15, send=0.18, space="chamber", human=0.006)
    s.part("tamb", "tambourine", gain_db=-20, pan=0.4, send=0.2, space="chamber")
    s.part("psaltery", "psaltery_pluck", gain_db=-11, pan=0.35, send=0.3, space="chamber")

    # Intro (bars 0-3): strumstick and harp over the open-fifth drone.
    strum(s, "strum", 0, INTRO, vel=0.5)
    arpeggio(s, "harp", 0, INTRO, order=(0, 1, 2, 3, 2, 1), step=1, low=55, high=79, vel=0.42, ring=3)
    pad(s, "drone", 0, [("D5", 24)], 38, 50, count=2, vel=0.45)

    a1, b1, a2, b2, out = 24, 72, 120, 168, 216
    # A1: the tune on alto recorder.
    s.line("alto", a1, TUNE_A, vel=0.72)
    strum(s, "strum", a1, CHORDS_A)
    pad(s, "drone", a1, [("D5", 48)], 38, 50, count=2, vel=0.45)
    bass(s, "pizz", a1, CHORDS_A, rhythm=((0, 2), (3, 2)), low=38, high=50, vel=0.6, fifth_on=(3,))
    s.pattern("frame", a1 // 6, 8, "X..x..", steps_per_beat=1, vel=0.55)

    # B1: soprano recorder answers; violas hum underneath.
    s.line("soprano", b1, TUNE_B, vel=0.66)
    strum(s, "strum", b1, CHORDS_B)
    arpeggio(s, "harp", b1, CHORDS_B, order=(0, 2, 1, 3, 2, 1), step=1, low=55, high=79, vel=0.4, ring=3)
    pad(s, "violas", b1, CHORDS_B, 55, 69, count=2, vel=0.42)
    bass(s, "pizz", b1, CHORDS_B, rhythm=((0, 2), (3, 2)), low=38, high=50, vel=0.6, fifth_on=(3,))
    s.pattern("frame", b1 // 6, 8, "X..x.o", steps_per_beat=1, vel=0.55)

    # A2: the tune again with psaltery doubling an octave up and the full band.
    s.line("alto", a2, TUNE_A, vel=0.76)
    s.line("psaltery", a2, TUNE_A, vel=0.5, damp=0.3)
    strum(s, "strum", a2, CHORDS_A, vel=0.6)
    pad(s, "drone", a2, [("D5", 48)], 38, 50, count=2, vel=0.5)
    pad(s, "violas", a2, CHORDS_A, 55, 69, count=2, vel=0.45)
    bass(s, "pizz", a2, CHORDS_A, rhythm=((0, 2), (3, 1), (5, 1)), low=38, high=50, vel=0.62, fifth_on=(3,))
    s.pattern("frame", a2 // 6, 8, "X.ox.o", steps_per_beat=1, vel=0.6)
    s.pattern("tamb", a2 // 6, 8, "...x..", steps_per_beat=1, vel=0.5)

    # B2: both recorders in thirds; harp brighter.
    s.line("soprano", b2, TUNE_B, vel=0.68)
    s.line("alto", b2, TUNE_B_LOW, vel=0.58)
    strum(s, "strum", b2, CHORDS_B, vel=0.6)
    arpeggio(s, "harp", b2, CHORDS_B, order=(0, 1, 2, 3, 2, 1), step=1, low=60, high=84, vel=0.42, ring=3)
    pad(s, "violas", b2, CHORDS_B, 55, 69, count=3, vel=0.48)
    bass(s, "pizz", b2, CHORDS_B, rhythm=((0, 2), (3, 1), (5, 1)), low=38, high=50, vel=0.62, fifth_on=(3,))
    s.pattern("frame", b2 // 6, 8, "X.ox.o", steps_per_beat=1, vel=0.6)
    s.pattern("tamb", b2 // 6, 8, "...x.x", steps_per_beat=1, vel=0.45)

    # Outro (bars 36-39): the road winds on - harp and strumstick, a last recorder phrase.
    strum(s, "strum", out, INTRO, vel=0.5)
    arpeggio(s, "harp", out, INTRO, order=(0, 1, 2, 3, 2, 1), step=1, low=55, high=79, vel=0.4, ring=3)
    pad(s, "drone", out, [("D5", 24)], 38, 50, count=2, vel=0.42)
    s.line("alto", out, "A5:3 F#5:3 E5:2 D5:1 C5:3 B4:2 C5:1 D5:3 A4:6 r:3", vel=0.58)
    return s
