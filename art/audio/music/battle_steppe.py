"""Sunfall Steppe: "Riders of the Burned Plain" - open sky and galloping hooves. E Dorian, 138 bpm, 32 bars."""
from ak import song as S
from ak.song import ease_out
from music.battle_kit import drum_parts, fill, hit, timpani
from music.common import bass, expand, ostinato, pad
from ak import theory as T

BPM = 138
PROG_A = [("Em", 4), ("D", 4), ("C", 4), ("D", 4), ("Em", 4), ("D", 4), ("C", 4), ("B", 4)]
FIFE = ("E5:1 G5:.5 A5:.5 B5:1 E6:1 D6:1.5 C#6:.5 B5:1 A5:1 G5:1.5 A5:.5 G5:1 E5:1 F#5:4 "
        "E5:1 G5:.5 A5:.5 B5:1 E6:1 F#6:1.5 E6:.5 D6:1 A5:1 G5:1 E5:1 C6:1 B5:1 B5:2 D#6:2")
PROG_B = [("C", 4), ("G", 4), ("D", 4), ("Em", 4), ("C", 4), ("G", 4), ("Am", 4), ("B", 4)]
HORN_B = "E4:2 G4:2 D4:2 B3:2 F#4:2 A4:2 G4:2 E4:2 E4:2 C5:2 B4:2 D5:2 C5:2 A4:2 B4:2 D#4:2"


def build():
    s = S.Song("battle_steppe", BPM, bars=32, seed=181)
    s.part("piccolo", "piccolo", gain_db=-12, pan=-0.3, send=0.3, space="hall")
    s.part("flute", "flute", gain_db=-6, pan=-0.3, send=0.3, space="hall")
    s.part("horns", "horn", gain_db=-5, pan=-0.15, send=0.35)
    s.part("trumpets", "trumpet_stac", gain_db=-9, pan=0.25, send=0.3)
    s.part("violins", "violins", gain_db=-7, pan=-0.35, send=0.28)
    s.part("vln_spic", "violins_spic", gain_db=-10, pan=-0.3, send=0.22)
    s.part("cellos", "cellos_spic", gain_db=-6, pan=0.25, send=0.2)
    s.part("basses", "basses_pizz", gain_db=-7, pan=0.3, send=0.2)
    s.part("darbuka", "darbuka", gain_db=-9, pan=0.2, send=0.15, human=0.003)
    s.part("tek", "darbuka_high", gain_db=-12, pan=-0.2, send=0.15, human=0.003)
    s.part("frame", "frame_drum", gain_db=-10, pan=0.0, send=0.2, human=0.004)
    s.part("tamb", "tambourine", gain_db=-20, pan=0.45, send=0.2)
    drum_parts(s)

    def gallop(start_bar, bars, vel=0.6, full=False):
        s.pattern("darbuka", start_bar, bars, "X.xxX.xxX.xxX.xx", steps_per_beat=4, vel=vel)
        s.pattern("tek", start_bar, bars, "..x...x...x...x." if not full else ".xx..xx..xx..xx.", steps_per_beat=4,
                  vel=vel * 0.7)
        s.pattern("frame", start_bar, bars, "X.......X.......", steps_per_beat=4, vel=vel)
        if full:
            s.pattern("bd", start_bar, bars, "X.......X..x....", steps_per_beat=4, vel=vel)
            s.pattern("tamb", start_bar, bars, "..x...x...x...x.", steps_per_beat=4, vel=vel * 0.7)

    def hooves(start, prog, vel=0.58):
        # Cellos gallop root-root-fifth in a dotted 16th rhythm.
        for beat, sym, beats in expand(prog):
            root = T.bass_note(sym, 38, 50)
            for b in range(int(beats)):
                s.n("cellos", start + beat + b, root, 0.4, vel + 0.1, damp=0.08)
                s.n("cellos", start + beat + b + 0.5, root, 0.2, vel * 0.8, damp=0.06)
                s.n("cellos", start + beat + b + 0.75, root + 7 if b % 2 else root + 12, 0.2, vel * 0.8, damp=0.06)

    # S1: riders on the horizon - galloping drums and cellos, open-fifth horns.
    hooves(0, PROG_A)
    bass(s, "basses", 0, PROG_A, rhythm=((0, 1), (2, 1)), low=33, high=45, vel=0.6, fifth_on=(2,))
    pad(s, "horns", 0, PROG_A, 50, 64, count=2, vel=0.45)
    gallop(0, 8, 0.55)
    fill(s, 30, 2, "snare", 0.7)

    # S2: the fife tune on flute (piccolo shadowing), trumpet stabs.
    o = 32
    s.line("flute", o, FIFE, vel=0.78)
    s.line("piccolo", o + 16, FIFE[FIFE.index("E5:1 G5:.5 A5:.5 B5:1 E6:1 F#6"):], vel=0.5)
    hooves(o, PROG_A, 0.6)
    bass(s, "basses", o, PROG_A, rhythm=((0, 1), (2, 1)), low=33, high=45, vel=0.62, fifth_on=(2,))
    pad(s, "horns", o, PROG_A, 50, 64, count=3, vel=0.5)
    for bar, (sym, _) in enumerate(PROG_A):
        tones = T.voice(sym, 59, 72, 3)
        s.n("trumpets", o + bar * 4 + 1.5, tones, 0.5, 0.5, damp=0.1)
        s.n("trumpets", o + bar * 4 + 3.5, tones, 0.5, 0.5, damp=0.1)
    gallop(8, 8, 0.6, full=True)
    timpani(s, o, PROG_A, 8, every=2, vel=0.5, offbeat=False)
    hit(s, o, crash=0.55)
    fill(s, o + 30, 2, "toms", 0.8)

    # S3: the charge - horns call across the plain, violins race.
    o = 64
    s.line("horns", o, HORN_B, vel=0.82)
    ostinato(s, "vln_spic", o, PROG_B, [12, 7, "10", 7], step=0.5, base_low=62, base_high=74, vel=0.45)
    hooves(o, PROG_B, 0.62)
    bass(s, "basses", o, PROG_B, rhythm=((0, 1), (2, 1)), low=33, high=45, vel=0.62, fifth_on=(2,))
    pad(s, "violins", o, PROG_B, 64, 79, count=3, vel=0.45)
    gallop(16, 8, 0.64, full=True)
    timpani(s, o, PROG_B, 8, vel=0.55)
    hit(s, o, crash=0.65)
    fill(s, o + 30, 2, "big", 0.85)

    # S4: full tilt - fife tune on violins, flute and piccolo; horns in harmony.
    o = 96
    s.line("violins", o, FIFE, vel=0.78)
    s.line("flute", o, FIFE, vel=0.66)
    pad(s, "horns", o, PROG_A, 50, 64, count=3, vel=0.6)
    ostinato(s, "vln_spic", o, PROG_A, [12, 7, "10", 7], step=0.5, base_low=62, base_high=74, vel=0.42)
    hooves(o, PROG_A, 0.64)
    bass(s, "basses", o, PROG_A, rhythm=((0, 1), (2, 1)), low=33, high=45, vel=0.64, fifth_on=(2,))
    gallop(24, 8, 0.66, full=True)
    timpani(s, o, PROG_A, 8, vel=0.6)
    hit(s, o, crash=0.7, gong=0.3)
    fill(s, o + 30, 2, "toms", 0.85)
    ease_out(s, ["violins", "flute", "horns"], o + 28, o + 32, -5)
    return s
