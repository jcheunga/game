"""Shop / armory: "The Quartermaster's Wagon" - a branle-like dance. G Dorian, 116 bpm, 32 bars."""
from ak import song as S
from ak import theory as T
from music.common import arpeggio, bass, expand, pad

BPM = 116
TUNE_A = ("G4:1 Bb4:.5 C5:.5 D5:1 D5:1 C5:.5 Bb4:.5 A4:.5 Bb4:.5 C5:1 A4:1 Bb4:1 A4:.5 G4:.5 F4:1 A4:1 G4:2 D4:2 "
          "G4:1 Bb4:.5 C5:.5 D5:1 F5:1 E5:.5 D5:.5 C5:.5 D5:.5 E5:1 C5:1 D5:1 C5:.5 Bb4:.5 A4:1 F4:1 G4:4")
CHORDS_A = [("Gm", 4), ("F", 4), ("Gm", 2), ("F", 2), ("Gm", 4), ("Gm", 4), ("C", 4), ("Gm", 2), ("F", 2), ("Gm", 4)]
TUNE_B = ("D5:1 F5:1 Bb5:2 A5:1 G5:.5 F5:.5 C5:2 E5:1 G5:1 C6:1 Bb5:.5 A5:.5 A5:2 F#5:2 "
          "D5:1 F5:1 Bb5:1 D6:1 C6:1 A5:1 F5:2 G5:1 E5:.5 F5:.5 G5:1 C5:1 D5:2 F#5:1 A5:1")
CHORDS_B = [("Bb", 4), ("F", 4), ("C", 4), ("D", 4), ("Bb", 4), ("F", 4), ("C", 4), ("D", 4)]


def strum(s, part, start, prog, vel=0.5):
    prev = None
    for beat, sym, beats in expand(prog):
        tones = T.voice(sym, 50, 69, 3, prev)
        prev = tones
        pos = 0
        while pos < beats - 1e-6:
            s.n(part, start + beat + pos, tones, min(1.5, beats - pos), vel if pos % 2 == 0 else vel * 0.7, damp=0.2)
            s.n(part, start + beat + pos + 1.5, tones[-2:], 0.5, vel * 0.45, damp=0.15)
            pos += 2


def groove(s, start_bar, bars, full=True):
    s.pattern("doum", start_bar, bars, "X...X.x.", steps_per_beat=2, vel=0.62)
    s.pattern("tek", start_bar, bars, "..x...x." if not full else "..x.x.xx", steps_per_beat=2, vel=0.45)
    if full:
        s.pattern("tamb", start_bar, bars, ".x.x.x.x", steps_per_beat=2, vel=0.4)


def build():
    s = S.Song("shop", BPM, bars=32, seed=31)
    s.part("psaltery", "psaltery_pluck", gain_db=-6, pan=0.2, send=0.2, space="room")
    s.part("recorder", "recorder_alto", gain_db=-5, pan=-0.2, send=0.22, space="room")
    s.part("fiddle", "solo_violin", gain_db=-8, pan=-0.1, send=0.24, space="room")
    s.part("strum", "strumstick", gain_db=-8, pan=0.35, send=0.18, strum=0.012, space="room")
    s.part("harp", "folk_harp", gain_db=-10, pan=-0.35, send=0.22, space="room")
    s.part("pizz", "cellos_pizz", gain_db=-8, pan=0.05, send=0.15, space="room")
    s.part("violas", "violas", gain_db=-14, pan=0.2, send=0.25, space="room")
    s.part("doum", "darbuka", gain_db=-9, pan=-0.1, send=0.12, space="room", human=0.005)
    s.part("tek", "darbuka_high", gain_db=-12, pan=0.15, send=0.12, space="room", human=0.005)
    s.part("tamb", "tambourine", gain_db=-20, pan=0.45, send=0.15, space="room")

    a1, b1, a2, b2 = 0, 32, 64, 96
    # A1: psaltery leads an octave up, strumstick and darbuka keep the dance.
    s.line("psaltery", a1, TUNE_A, vel=0.7, transpose=12, damp=0.25)
    strum(s, "strum", a1, CHORDS_A)
    bass(s, "pizz", a1, CHORDS_A, rhythm=((0, 1), (2, 1)), low=38, high=50, vel=0.62, fifth_on=(2,))
    groove(s, 0, 8, full=False)

    # B1: recorder takes the bright second strain; harp and violas fill in.
    s.line("recorder", b1, TUNE_B, vel=0.7)
    strum(s, "strum", b1, CHORDS_B)
    arpeggio(s, "harp", b1, CHORDS_B, order=(0, 2, 1, 3), step=0.5, low=55, high=79, vel=0.4, ring=1.5)
    pad(s, "violas", b1, CHORDS_B, 55, 67, count=2, vel=0.4)
    bass(s, "pizz", b1, CHORDS_B, rhythm=((0, 1), (2, 1)), low=38, high=50, vel=0.62, fifth_on=(2,))
    groove(s, 8, 8)

    # A2: the fiddle plays the tune, psaltery answers at the octave.
    s.line("fiddle", a2, TUNE_A, vel=0.72, transpose=12)
    s.line("psaltery", a2, TUNE_A, vel=0.5, transpose=12, damp=0.25)
    strum(s, "strum", a2, CHORDS_A, vel=0.55)
    bass(s, "pizz", a2, CHORDS_A, rhythm=((0, 1), (1.5, 0.5), (2, 1)), low=38, high=50, vel=0.64, fifth_on=(2,))
    groove(s, 16, 8)

    # B2: everyone - recorder and fiddle in unison, psaltery sparkles.
    s.line("recorder", b2, TUNE_B, vel=0.72)
    s.line("fiddle", b2, TUNE_B, vel=0.6)
    s.line("psaltery", b2, TUNE_B, vel=0.45, damp=0.25)
    strum(s, "strum", b2, CHORDS_B, vel=0.55)
    arpeggio(s, "harp", b2, CHORDS_B, order=(0, 1, 2, 3), step=0.5, low=55, high=79, vel=0.4, ring=1.5)
    pad(s, "violas", b2, CHORDS_B, 55, 67, count=2, vel=0.42)
    bass(s, "pizz", b2, CHORDS_B, rhythm=((0, 1), (1.5, 0.5), (2, 1)), low=38, high=50, vel=0.64, fifth_on=(2,))
    groove(s, 24, 8)
    return s
