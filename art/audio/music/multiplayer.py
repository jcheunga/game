"""Multiplayer: "The Tourney Field" - banners, rivals and trumpets. D major, 112 bpm, 28 bars."""
from ak import song as S
from music.common import arpeggio, bass, chord_at, chord_root, ostinato, pad, roll

BPM = 112
CHORDS_A = [("D", 4), ("G", 4), ("D", 4), ("A", 4), ("D", 4), ("G", 4), ("Em", 2), ("A", 2), ("D", 4)]
TUNE_A = ("D5:.5 D5:.5 A4:1 D5:1 F#5:1 G5:1.5 F#5:.5 E5:1 D5:1 F#5:1 E5:.5 D5:.5 A4:1 D5:1 E5:4 "
          "D5:.5 D5:.5 A4:1 D5:1 F#5:1 B5:1.5 A5:.5 G5:1 B5:1 A5:1 G5:.5 F#5:.5 E5:1 C#5:1 D5:4")
CHORDS_B = [("Bm", 4), ("G", 4), ("D", 4), ("A", 4), ("Bm", 4), ("G", 4), ("Em", 4), ("A", 4)]
TUNE_B = ("F#5:2 D5:1 F#5:1 G5:2 B5:2 A5:2 F#5:1 D5:1 E5:4 "
          "F#5:2 D5:1 B4:1 G5:2 D5:2 E5:1.5 F#5:.5 G5:1 B5:1 A5:4")


def drums(s, start_bar, bars, vel=0.5):
    s.pattern("snare", start_bar, bars, "x.xxx.x.x.xxX.xx", steps_per_beat=4, vel=vel)
    s.pattern("bass_drum", start_bar, bars, "x...x...", steps_per_beat=2, vel=vel + 0.1)
    s.pattern("tamb", start_bar, bars, "..x...x.", steps_per_beat=2, vel=vel - 0.1)


def build():
    s = S.Song("multiplayer", BPM, bars=28, seed=61)
    s.part("trumpets", "trumpet", gain_db=-6, pan=0.15, send=0.32)
    s.part("trumpets_stac", "trumpet_stac", gain_db=-8, pan=0.2, send=0.32)
    s.part("horns", "horn", gain_db=-7, pan=-0.25, send=0.36)
    s.part("violins", "violins", gain_db=-8, pan=-0.35, send=0.28)
    s.part("violas_spic", "violas_spic", gain_db=-10, pan=0.25, send=0.22)
    s.part("cellos", "cellos_spic", gain_db=-8, pan=0.3, send=0.22)
    s.part("basses", "basses_pizz", gain_db=-8, pan=0.3, send=0.2)
    s.part("flute", "piccolo", gain_db=-14, pan=-0.3, send=0.3)
    s.part("snare", "snare_rope", gain_db=-13, pan=-0.2, send=0.18, human=0.004)
    s.part("bass_drum", "bass_drum", gain_db=-11, send=0.25, human=0.004)
    s.part("tamb", "tambourine", gain_db=-20, pan=0.45, send=0.2)
    s.part("timp", "timpani_tuned", gain_db=-8, send=0.3, human=0.004)
    s.part("cymbal", "crash", gain_db=-18, pan=0.3, send=0.35)

    # Intro (bars 0-1): heralds' fanfare.
    s.line("trumpets_stac", 0, "A4:.333 A4:.333 A4:.334 D5:1 A4:.5 D5:.5 F#5:2 r:.5 A4:.5 D5:3 r:.5", vel=0.75)
    s.line("horns", 0, "[D4,F#4]:3 [F#4,A4]:3 r:2", vel=0.6)
    roll(s, "snare", 6, 2, 0.2, 0.7, rate=8)
    s.n("timp", 0, "D3", 1, 0.7)
    s.n("timp", 4, "A2", 1, 0.6)

    a1, b1, a2 = 8, 40, 72
    # A1: the trumpet's tourney tune.
    s.line("trumpets", a1, TUNE_A, vel=0.72)
    ostinato(s, "cellos", a1, CHORDS_A, [0, 7, 12, 7], step=0.5, base_low=38, base_high=50, vel=0.55)
    bass(s, "basses", a1, CHORDS_A, rhythm=((0, 1), (2, 1)), low=33, high=45, vel=0.6, fifth_on=(2,))
    pad(s, "horns", a1, CHORDS_A, 50, 65, count=3, vel=0.45)
    drums(s, 2, 8, 0.48)
    s.hit("cymbal", a1, 0.6)

    # B1: horns and violins sing the second strain; violas drive.
    s.line("horns", b1, TUNE_B, vel=0.68, transpose=-12)
    s.line("violins", b1, TUNE_B, vel=0.7)
    ostinato(s, "violas_spic", b1, CHORDS_B, [0, 7, 12, 7], step=0.5, base_low=50, base_high=62, vel=0.5)
    ostinato(s, "cellos", b1, CHORDS_B, [0, 0, 7, 0], step=0.5, base_low=38, base_high=50, vel=0.55)
    bass(s, "basses", b1, CHORDS_B, rhythm=((0, 1), (2, 1)), low=33, high=45, vel=0.6, fifth_on=(2,))
    drums(s, 10, 8, 0.52)
    for bar in range(8):
        s.n("timp", b1 + bar * 4, chord_root(chord_at(CHORDS_B, bar * 4), 41, 55), 1, 0.5)
    roll(s, "snare", b1 + 30, 2, 0.3, 0.85, rate=8)

    # A2: tutti - trumpets with violins and piccolo, timpani.
    s.line("trumpets", a2, TUNE_A, vel=0.8)
    s.line("violins", a2, TUNE_A, vel=0.66)
    s.line("flute", a2, TUNE_A, vel=0.5, transpose=12)
    pad(s, "horns", a2, CHORDS_A, 50, 65, count=3, vel=0.6)
    ostinato(s, "violas_spic", a2, CHORDS_A, [0, 7, 12, 7], step=0.5, base_low=50, base_high=62, vel=0.52)
    ostinato(s, "cellos", a2, CHORDS_A, [0, 7, 12, 7], step=0.5, base_low=38, base_high=50, vel=0.6)
    bass(s, "basses", a2, CHORDS_A, rhythm=((0, 1), (2, 1)), low=33, high=45, vel=0.62, fifth_on=(2,))
    drums(s, 18, 8, 0.56)
    s.hit("cymbal", a2, 0.7)
    for bar in range(8):
        s.n("timp", a2 + bar * 4, chord_root(chord_at(CHORDS_A, bar * 4), 41, 55), 1, 0.6)

    # Outro (bars 26-27): a cadence that hands back to the fanfare.
    o = 104
    s.line("trumpets", o, "[D5,F#5]:2 [C#5,E5]:2 [D5,F#5]:3 r:1", vel=0.7)
    pad(s, "horns", o, [("D", 2), ("A", 2), ("D", 4)], 50, 65, count=3, vel=0.55)
    bass(s, "basses", o, [("D", 2), ("A", 2), ("D", 4)], rhythm=((0, 1),), low=33, high=45, vel=0.6)
    s.n("timp", o, "D3", 1, 0.6)
    s.n("timp", o + 2, "A2", 1, 0.55)
    s.n("timp", o + 4, "D3", 1, 0.65)
    return s
