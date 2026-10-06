"""Hollow Basilica: "Ossuary Chant" - a profaned cathedral. D minor (Dorian), 100 bpm, 32 bars."""
from ak import song as S
from ak.song import ease_out
from music.battle_kit import drum_parts, fill, groove, hit, timpani
from music.common import bass, chord_at, chord_root, ostinato, pad

BPM = 100
PROG_A = [("Dm", 4), ("C", 4), ("Gm", 4), ("Dm", 4), ("Bb", 4), ("F", 4), ("Gm", 4), ("A", 4)]
CHANT = ("D4:2 F4:1 E4:1 G4:2 E4:1 G4:1 Bb4:2 A4:1 G4:1 A4:3 F4:1 "
         "D5:2 C5:1 Bb4:1 A4:2 C5:1 A4:1 G4:1 A4:1 Bb4:1 G4:1 A4:2 E4:1 C#4:1")
PROG_B = [("Gm", 4), ("Eb", 4), ("Bb", 4), ("F", 4), ("Gm", 4), ("Eb", 4), ("A", 4), ("A", 4)]
TUNE_B = "D5:2 Bb4:1 G4:1 G5:2 Eb5:1 Bb4:1 F5:2 D5:1 F5:1 A5:2 F5:2 Bb5:2 A5:1 G5:1 G5:2 Bb5:2 A5:2 C#6:2 E5:4"


def build(organ="organ", organ_db=-7):
    s = S.Song("battle_basilica", BPM, bars=32, seed=161)
    s.part("organ", organ, gain_db=organ_db, pan=0.0, send=0.5, space="cathedral", width=1.3)
    s.part("pedal", "organ", gain_db=-12, pan=0.0, send=0.4, space="cathedral", lowcut=35)
    s.part("horns", "horn", gain_db=-5, pan=-0.2, send=0.45, space="cathedral")
    s.part("trombones", "trombone", gain_db=-8, pan=0.25, send=0.4, space="cathedral")
    s.part("violins", "violins", gain_db=-7, pan=-0.35, send=0.4, space="cathedral")
    s.part("violas", "violas", gain_db=-11, pan=0.15, send=0.4, space="cathedral")
    s.part("cellos", "cellos_spic", gain_db=-10, pan=0.25, send=0.3, space="great_hall")
    s.part("basses", "basses", gain_db=-11, pan=0.3, send=0.3, space="great_hall")
    s.part("bells", "tubular_bells", gain_db=-11, pan=0.45, send=0.5, space="cathedral", highcut=7000)
    drum_parts(s, space="great_hall")

    def tolls(start, prog, every=2, vel=0.45):
        for bar in range(0, 8, every):
            s.n("bells", start + bar * 4, chord_root(chord_at(prog, bar * 4), 60, 72), 3, vel, damp=0.5)

    # S1: the nave - organ and pedal, distant bells, a slow ritual drum.
    pad(s, "organ", 0, PROG_A, 57, 74, count=3, vel=0.5)
    pad(s, "pedal", 0, PROG_A, 38, 50, count=1, vel=0.5)
    tolls(0, PROG_A, 4)
    groove(s, "halftime", 0, 8, vel=0.58)
    timpani(s, 0, PROG_A, 8, every=2, vel=0.5, offbeat=False)
    fill(s, 30, 2, "timp", 0.7)

    # S2: the chant on horns and trombones in unison; spiccato strings begin to march.
    o = 32
    s.line("horns", o, CHANT, vel=0.8)
    s.line("trombones", o, CHANT, vel=0.6, transpose=-12)
    pad(s, "organ", o, PROG_A, 57, 74, count=3, vel=0.45)
    pad(s, "pedal", o, PROG_A, 38, 50, count=1, vel=0.5)
    ostinato(s, "cellos", o, PROG_A, [0, 0, 7, 0, 12, 0, 7, 0], step=0.5, base_low=38, base_high=50, vel=0.6)
    bass(s, "basses", o, PROG_A, rhythm=((0, 4),), low=31, high=43, vel=0.52)
    groove(s, "taiko", 8, 8, vel=0.6)
    timpani(s, o, PROG_A, 8, vel=0.55)
    tolls(o, PROG_A, 2, 0.42)
    hit(s, o, crash=0.5, gong=0.4)
    fill(s, o + 30, 2, "toms", 0.8)

    # S3: the reliquary opens - violins over full organ, bells on every bar.
    o = 64
    s.line("violins", o, TUNE_B, vel=0.8)
    pad(s, "organ", o, PROG_B, 55, 74, count=4, vel=0.55)
    pad(s, "pedal", o, PROG_B, 36, 48, count=1, vel=0.55)
    pad(s, "horns", o, PROG_B, 50, 64, count=3, vel=0.55)
    ostinato(s, "cellos", o, PROG_B, [0, 0, 7, 0, 12, 0, 7, 0], step=0.5, base_low=38, base_high=50, vel=0.62)
    bass(s, "basses", o, PROG_B, rhythm=((0, 2), (2, 2)), low=31, high=43, vel=0.56)
    groove(s, "driving", 16, 8, vel=0.6)
    timpani(s, o, PROG_B, 8, vel=0.58)
    tolls(o, PROG_B, 1, 0.4)
    hit(s, o, crash=0.6, gong=0.45)
    fill(s, o + 30, 2, "big", 0.85)

    # S4: the chant returns in full - horns, violins an octave above, organ and gong.
    o = 96
    s.line("horns", o, CHANT, vel=0.82)
    s.line("violins", o, CHANT, vel=0.72, transpose=12)
    s.line("trombones", o, CHANT, vel=0.62, transpose=-12)
    pad(s, "organ", o, PROG_A, 57, 76, count=4, vel=0.55)
    pad(s, "pedal", o, PROG_A, 36, 48, count=1, vel=0.55)
    pad(s, "violas", o, PROG_A, 55, 67, count=2, vel=0.5)
    ostinato(s, "cellos", o, PROG_A, [0, 0, 7, 0, 12, 0, 7, 0], step=0.5, base_low=38, base_high=50, vel=0.65)
    bass(s, "basses", o, PROG_A, rhythm=((0, 2), (2, 2)), low=31, high=43, vel=0.6)
    groove(s, "taiko_full", 24, 8, vel=0.64)
    timpani(s, o, PROG_A, 8, vel=0.62)
    tolls(o, PROG_A, 2, 0.45)
    hit(s, o, crash=0.7, gong=0.5)
    fill(s, o + 30, 2, "timp", 0.8)
    ease_out(s, ["violins", "horns", "trombones", "violas", "cellos"], o + 28, o + 32, -6)
    return s
