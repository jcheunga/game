"""Title: "The Crownroad" - the caravan's theme in full. D minor, 76 bpm, 32 bars."""
from ak import song as S
from ak import theory as T
from music.common import (CROWN_A, CROWN_A_CHORDS, CROWN_B, CROWN_B_CHORDS, arpeggio, bass, chord_at, chord_root,
                          melody, pad, roll, transpose_line)

BPM = 76


def build():
    s = S.Song("title", BPM, bars=32, seed=11)
    s.part("harp", "folk_harp", gain_db=-5, pan=0.25, send=0.32)
    s.part("recorder", "recorder_alto", gain_db=-3, pan=-0.15, send=0.34)
    s.part("violins", "violins", gain_db=-7, pan=-0.35, send=0.3)
    s.part("violas", "violas", gain_db=-9, pan=0.15, send=0.3)
    s.part("cellos", "cellos", gain_db=-6, pan=0.3, send=0.28)
    s.part("basses", "basses", gain_db=-9, pan=0.35, send=0.25)
    s.part("horns", "horn", gain_db=-6, pan=-0.2, send=0.38)
    s.part("flute", "flute", gain_db=-8, pan=-0.3, send=0.36)
    s.part("timp", "timpani_tuned", gain_db=-5, pan=0.0, send=0.3, human=0.004)
    s.part("drum", "big_drum", gain_db=-9, send=0.35, human=0.004)
    s.part("cymbal", "sus_cymbal", gain_db=-16, pan=0.4, send=0.4)
    s.part("chimes", "hand_chimes", gain_db=-14, pan=0.5, send=0.5)

    intro = [("Dm", 4), ("Bb", 4), ("Gm", 4), ("A", 4)]

    # Intro (bars 0-3): rocking harp, cello/bass drone, a breath of chimes, timpani swell into the theme.
    arpeggio(s, "harp", 0, intro, order=(0, 2, 1, 3, 2, 1, 0, 2), step=0.5, low=50, high=74, vel=0.5, ring=2.5)
    pad(s, "cellos", 0, intro, 38, 52, count=1, vel=0.4)
    pad(s, "violas", 0, intro, 53, 67, count=2, vel=0.35)
    s.automate("violas", [(0, -6), (16, 0)])
    s.line("chimes", 0, "A5:4 F5:4 D5:4 C#5:4", vel=0.35, damp=0.8)
    roll(s, "timp", 13, 3, 0.15, 0.55, rate=10, pitch="A2")

    # A1 (bars 4-11): the theme on solo recorder over harp and soft strings.
    a1 = 16
    melody(s, "recorder", a1, CROWN_A, vel=0.72)
    arpeggio(s, "harp", a1, CROWN_A_CHORDS, order=(0, 1, 2, 3, 2, 1, 2, 1), step=0.5, low=50, high=74, vel=0.48, ring=2.5)
    pad(s, "violas", a1, CROWN_A_CHORDS, 53, 69, count=3, vel=0.42)
    pad(s, "cellos", a1, CROWN_A_CHORDS, 36, 52, count=1, vel=0.5)

    # B1 (bars 12-19): horns carry the second phrase; strings rise beneath.
    b1 = 48
    melody(s, "horns", b1, transpose_line(CROWN_B, -12), vel=0.68)
    arpeggio(s, "harp", b1, CROWN_B_CHORDS, order=(0, 2, 1, 3), step=0.5, low=50, high=76, vel=0.45, ring=2.5)
    pad(s, "violins", b1, CROWN_B_CHORDS, 60, 76, count=3, vel=0.45)
    pad(s, "violas", b1, CROWN_B_CHORDS, 53, 67, count=2, vel=0.45)
    bass(s, "cellos", b1, CROWN_B_CHORDS, rhythm=((0, 2), (2, 2)), low=36, high=50, vel=0.55)
    bass(s, "basses", b1, CROWN_B_CHORDS, rhythm=((0, 4),), low=31, high=43, vel=0.5)
    s.automate("violins", [(b1, -4), (b1 + 28, 1)])
    for bar in range(8):
        s.n("timp", b1 + bar * 4, chord_root(chord_at(CROWN_B_CHORDS, bar * 4), 41, 55), 1, 0.4)
    roll(s, "timp", b1 + 28, 4, 0.2, 0.8, rate=12, pitch="A2")
    roll(s, "cymbal", b1 + 29, 3, 0.1, 0.6, rate=12)

    # A2 (bars 20-27): tutti statement - violins and flute in octaves, horns in harmony, drums.
    a2 = 80
    melody(s, "violins", a2, CROWN_A, vel=0.82)
    melody(s, "flute", a2, transpose_line(CROWN_A, 12), vel=0.62)
    pad(s, "horns", a2, CROWN_A_CHORDS, 50, 65, count=3, vel=0.66)
    pad(s, "violas", a2, CROWN_A_CHORDS, 55, 69, count=2, vel=0.6, rearticulate=2)
    arpeggio(s, "harp", a2, CROWN_A_CHORDS, order=(0, 1, 2, 3), step=0.5, low=55, high=81, vel=0.5, ring=2.0)
    bass(s, "cellos", a2, CROWN_A_CHORDS, rhythm=((0, 1.5), (1.5, 0.5), (2, 2)), low=38, high=50, vel=0.7,
         octave_on=(1.5,))
    bass(s, "basses", a2, CROWN_A_CHORDS, rhythm=((0, 2), (2, 2)), low=31, high=43, vel=0.65)
    s.hit("cymbal", a2, 0.75)
    for bar in range(8):
        first, third = chord_at(CROWN_A_CHORDS, bar * 4), chord_at(CROWN_A_CHORDS, bar * 4 + 2)
        s.n("timp", a2 + bar * 4, chord_root(first, 41, 55), 1, 0.7)
        s.n("timp", a2 + bar * 4 + 2.5, chord_root(third, 41, 55, prefer_fifth=True), 0.5, 0.45)
        s.n("timp", a2 + bar * 4 + 3, chord_root(third, 41, 55), 1, 0.55)
        s.hit("drum", a2 + bar * 4, 0.7)
    s.hit("drum", a2 + 30, 0.8)
    s.hit("drum", a2 + 31, 0.9)

    # Coda (bars 28-31): the head motif echoes on recorder as the harp settles back to the intro.
    c = 112
    arpeggio(s, "harp", c, intro, order=(0, 2, 1, 3, 2, 1, 0, 2), step=0.5, low=50, high=74, vel=0.45, ring=2.5)
    melody(s, "recorder", c, "A5:1.5 G5:.5 F5:1 E5:1 D5:3 F5:1 G5:1.5 F5:.5 D5:1 Bb4:1 C#5:3 r:1", vel=0.6)
    pad(s, "cellos", c, intro, 38, 52, count=1, vel=0.42)
    pad(s, "violas", c, intro, 53, 67, count=2, vel=0.38)
    s.line("chimes", c + 12, "E5:2 C#5:2", vel=0.3, damp=0.8)
    return s


