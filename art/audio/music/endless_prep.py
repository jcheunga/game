"""Endless preparation: "The Long Night" - the host never sleeps. A Phrygian, 66 bpm, 16 bars."""
from ak import song as S
from music.common import pad

BPM = 66
PROG = [("Am", 8), ("Bb", 8), ("Am", 8), ("Gm", 4), ("A", 4)]  # 32 beats, played twice


def build(lament="violins"):
    s = S.Song("endless_prep", BPM, bars=16, seed=51)
    s.part("drone", "cellos_trem", gain_db=-15, pan=0.2, send=0.4, space="cathedral", eq=[("peak", 280, 0.9, -3)])
    s.part("basses", "basses", gain_db=-17, pan=0.3, send=0.3, space="cathedral")
    s.part("psaltery", lament, gain_db=-10, pan=-0.25, send=0.45, space="cathedral")
    s.part("glass", "wine_glass", gain_db=-24, pan=0.5, send=0.5, space="cathedral")
    s.part("bells", "tubular_bells", gain_db=-17, pan=-0.4, send=0.45, space="cathedral", highcut=6500)
    s.part("harp", "folk_harp", gain_db=-12, pan=0.35, send=0.4, space="cathedral")
    s.part("horn", "horn", gain_db=-11, pan=-0.1, send=0.45, space="cathedral")
    s.part("violas", "violas", gain_db=-16, pan=0.1, send=0.45, space="cathedral", lowcut=200)
    s.part("oboe", "oboe", gain_db=-9, pan=0.3, send=0.42, space="cathedral")

    for rep in (0, 32):
        pad(s, "drone", rep, PROG, 45, 57, count=2, vel=0.4)
        pad(s, "basses", rep, PROG, 28, 40, count=1, vel=0.42)
        pad(s, "violas", rep, PROG, 57, 69, count=2, vel=0.3)
        for k in (0, 16):
            s.n("bells", rep + k, "A4", 6, 0.42 if k == 0 else 0.32, damp=1.5)
        # Low harp heartbeat: two plucks per bar on the root and fifth.
        for bar in range(8):
            root = {0: "A2", 1: "A2", 2: "Bb2", 3: "Bb2", 4: "A2", 5: "A2", 6: "G2", 7: "A2"}[bar]
            s.n("harp", rep + bar * 4, root, 1.5, 0.5, damp=0.6)
            s.n("harp", rep + bar * 4 + 1.5, "E3" if root == "A2" else "F3" if root == "Bb2" else "D3", 1.5, 0.35, damp=0.6)
    # The bowed psaltery's lament, then the Rotbound sigh on low horn.
    s.line("psaltery", 0, "E5:3 F5:1 E5:4 D5:2 C5:2 Bb4:4 C5:2 D5:2 E5:3 F5:1 E5:4 r:4", vel=0.55)
    s.line("glass", 16, "E5:16", vel=0.5)
    s.line("oboe", 16, "A4:2 C5:2 E5:3 D5:1 D5:2 Bb4:2 C#5:4", vel=0.55)
    s.line("horn", 32, "A2:2 Bb2:2 A2:2 Eb2:2 r:8 A2:2 Bb2:2 A2:2 E2:6 r:2", vel=0.55)
    s.line("psaltery", 48, "A5:4 Bb5:2 A5:2 G5:4 E5:4", vel=0.45)
    return s
