"""Loadout / preparation: "War Council" - resolve before the march. D minor, 84 bpm, 24 bars."""
from ak import song as S
from music.common import arpeggio, bass, chord_at, chord_root, ostinato, pad, roll

BPM = 84
CYCLE = [("Dm", 8), ("Bb", 8), ("Gm", 8), ("A", 8)]
SWELL = [("F", 4), ("C", 4), ("Dm", 4), ("Bb", 4), ("Gm", 4), ("A", 4), ("Dm", 4), ("A", 4)]
SWELL_TUNE = ("A5:2 G5:1 F5:1 E5:2 G5:2 F5:1.5 E5:.5 D5:1 A5:1 Bb5:3 A5:1 "
              "G5:1.5 A5:.5 Bb5:1 D6:1 C#6:2 E5:2 D6:3 A5:1 A5:2 G5:1 E5:1")


def build():
    s = S.Song("loadout", BPM, bars=24, seed=41)
    s.part("spic", "cellos_spic", gain_db=-7, pan=0.25, send=0.22)
    s.part("violas", "violas", gain_db=-11, pan=0.15, send=0.3)
    s.part("violins", "violins", gain_db=-7, pan=-0.3, send=0.3)
    s.part("basses", "basses", gain_db=-10, pan=0.3, send=0.25)
    s.part("harp", "harp", gain_db=-12, pan=-0.4, send=0.35)
    s.part("horns", "horn", gain_db=-5, pan=-0.2, send=0.38)
    s.part("trumpet", "trumpet", gain_db=-9, pan=0.2, send=0.4)
    s.part("timp", "timpani_tuned", gain_db=-6, send=0.3, human=0.004)
    s.part("snare", "snare_rope", gain_db=-15, pan=-0.25, send=0.2, human=0.005)

    # S1 (bars 0-7): spiccato heartbeat, harp glints, soft pads.
    ostinato(s, "spic", 0, CYCLE, [0, 0, "3", 0, 7, 0, "3", 0], step=0.5, base_low=38, base_high=50, vel=0.55)
    pad(s, "violas", 0, CYCLE, 53, 67, count=2, vel=0.38)
    arpeggio(s, "harp", 0, CYCLE, order=(2, 3, 1, 3), step=2, low=62, high=86, vel=0.4,
             ring=3)
    for bar in range(0, 8, 2):
        s.n("timp", bar * 4, chord_root(chord_at(CYCLE, bar * 4), 41, 55), 2, 0.4)

    # S2 (bars 8-15): the horns sound the caravan's call; a trumpet answers; a march begins.
    o = 32
    ostinato(s, "spic", o, CYCLE, [0, 0, "3", 0, 7, 0, "3", 0], step=0.5, base_low=38, base_high=50, vel=0.62)
    pad(s, "violas", o, CYCLE, 53, 67, count=3, vel=0.45)
    bass(s, "basses", o, CYCLE, rhythm=((0, 4),), low=31, high=43, vel=0.5)
    s.line("horns", o, "D4:1.5 E4:.5 F4:1 G4:1 A4:4 r:2 A4:1 G4:1 F4:2 D4:2", vel=0.7)
    s.line("trumpet", o + 20, "G4:1.5 A4:.5 Bb4:1 C5:1 C#5:4", vel=0.62)
    s.line("horns", o + 24, "C#4:2 E4:2 A3:4", vel=0.6)
    s.pattern("snare", 8, 8, "x.xxx.x.", steps_per_beat=2, vel=0.4)
    for bar in range(8, 16):
        s.n("timp", bar * 4, chord_root(chord_at(CYCLE, (bar - 8) * 4), 41, 55), 1, 0.5)
    roll(s, "timp", o + 29, 3, 0.3, 0.75, rate=10, pitch="A2")

    # S3 (bars 16-23): violins lift a broad line over the full council; settles back to the heartbeat.
    o = 64
    s.line("violins", o, SWELL_TUNE, vel=0.78)
    pad(s, "horns", o, SWELL, 50, 65, count=3, vel=0.6)
    pad(s, "violas", o, SWELL, 55, 67, count=2, vel=0.5, rearticulate=2)
    ostinato(s, "spic", o, SWELL, [0, 0, 7, 0, 12, 0, 7, 0], step=0.5, base_low=38, base_high=50, vel=0.62)
    bass(s, "basses", o, SWELL, rhythm=((0, 2), (2, 2)), low=31, high=43, vel=0.58)
    s.pattern("snare", 16, 6, "x.xxx.x.", steps_per_beat=2, vel=0.45)
    for bar in range(16, 23):
        s.n("timp", bar * 4, chord_root(chord_at(SWELL, (bar - 16) * 4), 41, 55), 1, 0.6)
        s.n("timp", bar * 4 + 2, chord_root(chord_at(SWELL, (bar - 16) * 4 + 2), 41, 55, prefer_fifth=True), 1, 0.45)
    s.n("timp", 92, "A2", 1, 0.4)
    # The council settles: the last two bars thin out so the loop returns to the quiet heartbeat.
    for part in ("violins", "horns", "violas", "spic", "basses"):
        s.automate(part, [(0, 0), (88, 0), (96, -9)])
    return s
