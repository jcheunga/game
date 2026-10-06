"""Ashen Ward: "The Purge Bell" - plague cloisters and sealed vaults. B minor/Locrian colour, 112 bpm, 32 bars."""
from ak import song as S
from music.battle_kit import drum_parts, fill, groove, hit, timpani
from ak.song import ease_out
from music.common import bass, ostinato, pad

BPM = 104
PROG_A = [("Bm", 4), ("C", 4), ("F#", 2), ("Bm", 2), ("G", 2), ("F#", 2)] * 2
PROG_B = [("Em", 4), ("C", 4), ("Bm", 4), ("F#", 4), ("Em", 4), ("G", 4), ("C", 4), ("F#", 4)]
OBOE = ("B4:2 C5:1 B4:1 D5:2 C5:1 B4:1 A#4:2 B4:2 G4:2 F#4:2 "
        "B4:2 C5:1 D5:1 E5:2 D5:1 C5:1 A#4:1 C#5:1 B4:1 F#4:1 G4:2 F#4:2")
TUNE_B = ("G5:2 F#5:1 E5:1 G5:2 E5:2 F#5:1.5 E5:.5 D5:1 B4:1 C#5:2 F#5:2 "
          "G5:2 E5:1 B4:1 D5:2 B5:2 C6:1.5 B5:.5 G5:1 E5:1 A#5:4")


def build():
    s = S.Song("battle_quarantine", BPM, bars=32, seed=141)
    s.part("trem_hi", "violins_trem", gain_db=-11, pan=-0.35, send=0.4)
    s.part("trem_lo", "cellos_trem", gain_db=-10, pan=0.3, send=0.35)
    s.part("spic", "violins_spic", gain_db=-9, pan=-0.2, send=0.3)
    s.part("cellos", "cellos_spic", gain_db=-7, pan=0.25, send=0.25)
    s.part("basses", "basses", gain_db=-10, pan=0.3, send=0.25)
    s.part("oboe", "oboe", gain_db=-6, pan=0.2, send=0.38)
    s.part("violins", "violins", gain_db=-7, pan=-0.35, send=0.32)
    s.part("bell", "tubular_bells", gain_db=-12, pan=0.45, send=0.5, highcut=6500)
    s.part("glass", "wine_glass", gain_db=-24, pan=-0.5, send=0.5)
    s.part("ratchet", "ratchet", gain_db=-22, pan=0.5, send=0.35)
    drum_parts(s, space="great_hall")

    def heartbeat(start, prog, vel=0.58):
        ostinato(s, "cellos", start, prog, [0, None, 0, 0, None, None, 7, None], step=0.5, base_low=35, base_high=47,
                 vel=vel)

    # S1: the ward at night - tremolo mist, a plague bell, a heartbeat in the cellos.
    pad(s, "trem_lo", 0, PROG_A, 42, 54, count=2, vel=0.45)
    pad(s, "trem_hi", 0, PROG_A, 66, 78, count=2, vel=0.38)
    heartbeat(0, PROG_A)
    for k in (0, 16):
        s.n("bell", k, "B4", 3, 0.45, damp=0.4)
    s.line("glass", 0, "F#5:4 r:12 F#5:4 r:12", vel=0.45, damp=0.3)
    groove(s, "halftime", 0, 8, vel=0.6)
    s.hit("ratchet", 28, 0.4)

    # S2: the oboe's lament over the pulse; spiccato needles.
    o = 32
    s.line("oboe", o, OBOE, vel=0.74)
    pad(s, "trem_lo", o, PROG_A, 42, 54, count=2, vel=0.45)
    heartbeat(o, PROG_A, 0.62)
    ostinato(s, "spic", o, PROG_A, [12, 7, 12, "10", 12, 7, 12, "10"], step=0.5, base_low=59, base_high=71, vel=0.42)
    bass(s, "basses", o, PROG_A, rhythm=((0, 4),), low=31, high=43, vel=0.5)
    groove(s, "taiko", 8, 8, vel=0.58, skip=("sticks", "snare"))
    timpani(s, o, PROG_A, 8, every=2, vel=0.5, offbeat=False)
    s.n("bell", o, "B4", 3, 0.4, damp=0.4)
    fill(s, o + 30, 2, "toms", 0.75)

    # S3: purge - violins cry out over trembling clusters; the drums break loose.
    o = 64
    s.line("violins", o, TUNE_B, vel=0.8)
    pad(s, "trem_hi", o, PROG_B, 64, 79, count=3, vel=0.48)
    pad(s, "trem_lo", o, PROG_B, 40, 54, count=2, vel=0.52)
    ostinato(s, "cellos", o, PROG_B, [0, None, 0, 0, None, None, 7, None], step=0.5, base_low=35, base_high=47, vel=0.66)
    bass(s, "basses", o, PROG_B, rhythm=((0, 2), (2, 2)), low=28, high=40, vel=0.56)
    s.line("glass", o + 8, "F#5:8 r:12 F#5:4", vel=0.45, damp=0.3)
    for k in (0, 8, 16):
        s.n("bell", o + k, "B4", 3, 0.4, damp=0.4)
    groove(s, "taiko_full", 16, 8, vel=0.62, skip=("sticks", "snare"))
    timpani(s, o, PROG_B, 8, vel=0.58)
    hit(s, o, crash=0.65)
    s.hit("ratchet", o + 28, 0.5)
    fill(s, o + 30, 2, "big", 0.8)

    # S4: the oboe's lament, now on violins in octaves with the oboe; the bell over it all.
    o = 96
    s.line("violins", o, OBOE, vel=0.78, transpose=12)
    s.line("oboe", o, OBOE, vel=0.66)
    pad(s, "trem_lo", o, PROG_A, 42, 54, count=2, vel=0.5)
    pad(s, "trem_hi", o, PROG_A, 66, 78, count=2, vel=0.42)
    heartbeat(o, PROG_A, 0.66)
    bass(s, "basses", o, PROG_A, rhythm=((0, 2), (2, 2)), low=31, high=43, vel=0.58)
    groove(s, "taiko_full", 24, 8, vel=0.62, skip=("sticks", "snare"))
    timpani(s, o, PROG_A, 8, vel=0.6)
    for k in (0, 16):
        s.n("bell", o + k, "B4", 3, 0.45, damp=0.4)
    hit(s, o, crash=0.7, gong=0.4)
    fill(s, o + 30, 2, "toms", 0.8)
    ease_out(s, ["violins", "oboe", "trem_hi", "trem_lo", "basses"], o + 28, o + 32, -7)
    return s
