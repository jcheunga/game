"""Battle (general): "For the Crownroad" - the caravan's theme at full stride. D minor, 128 bpm, 32 bars."""
from ak import song as S
from music.battle_kit import drum_parts, fill, groove, hit, timpani
from music.common import CROWN_A, CROWN_A_CHORDS, CROWN_B, CROWN_B_CHORDS, bass, ostinato, pad, transpose_line

BPM = 128


def build():
    s = S.Song("battle", BPM, bars=32, seed=101)
    s.part("violins", "violins", gain_db=-7, pan=-0.35, send=0.28)
    s.part("vln_spic", "violins_spic", gain_db=-11, pan=-0.3, send=0.22)
    s.part("vla_spic", "violas_spic", gain_db=-10, pan=0.2, send=0.22)
    s.part("cellos", "cellos_spic", gain_db=-7, pan=0.25, send=0.2)
    s.part("basses", "basses", gain_db=-10, pan=0.3, send=0.2)
    s.part("horns", "horn", gain_db=-5, pan=-0.15, send=0.34)
    s.part("trumpets", "trumpet", gain_db=-7, pan=0.2, send=0.34)
    s.part("trombones", "trombone", gain_db=-9, pan=0.25, send=0.3)
    s.part("flute", "flute", gain_db=-11, pan=-0.4, send=0.32)
    drum_parts(s)

    # S1: the column forms - spiccato engine, low brass, taiko.
    ostinato(s, "cellos", 0, CROWN_A_CHORDS, [0, 0, 7, 0, 12, 0, 7, 0], step=0.5, base_low=38, base_high=50, vel=0.6)
    ostinato(s, "vla_spic", 0, CROWN_A_CHORDS, [None, "3", None, 7, None, "3", None, 7], step=0.5, base_low=50,
             base_high=62, vel=0.48)
    pad(s, "trombones", 0, CROWN_A_CHORDS, 41, 58, count=2, vel=0.5)
    bass(s, "basses", 0, CROWN_A_CHORDS, rhythm=((0, 2), (2, 2)), low=31, high=43, vel=0.55)
    groove(s, "taiko", 0, 8, vel=0.55)
    timpani(s, 0, CROWN_A_CHORDS, 8, vel=0.5)
    fill(s, 30, 2, "toms", 0.7)

    # S2: horns announce the theme.
    o = 32
    s.line("horns", o, transpose_line(CROWN_A, -12), vel=0.78)
    ostinato(s, "cellos", o, CROWN_A_CHORDS, [0, 0, 7, 0, 12, 0, 7, 0], step=0.5, base_low=38, base_high=50, vel=0.62)
    ostinato(s, "vla_spic", o, CROWN_A_CHORDS, [0, "3", 7, "3"], step=0.5, base_low=50, base_high=62, vel=0.5)
    pad(s, "trombones", o, CROWN_A_CHORDS, 41, 58, count=2, vel=0.52)
    bass(s, "basses", o, CROWN_A_CHORDS, rhythm=((0, 2), (2, 2)), low=31, high=43, vel=0.58)
    groove(s, "march", 8, 8, vel=0.58)
    timpani(s, o, CROWN_A_CHORDS, 8, vel=0.55)
    hit(s, o, crash=0.6)
    fill(s, o + 30, 2, "snare", 0.75)

    # S3: violins and flute take the second phrase; horns hold the harmony.
    o = 64
    s.line("violins", o, CROWN_B, vel=0.78)
    s.line("flute", o, CROWN_B, vel=0.55, transpose=12)
    pad(s, "horns", o, CROWN_B_CHORDS, 50, 65, count=3, vel=0.58)
    ostinato(s, "cellos", o, CROWN_B_CHORDS, [0, 0, 7, 0, 12, 0, 7, 0], step=0.5, base_low=38, base_high=50, vel=0.62)
    ostinato(s, "vln_spic", o, CROWN_B_CHORDS, [12, 7, "10", 7], step=0.5, base_low=62, base_high=74, vel=0.42)
    bass(s, "basses", o, CROWN_B_CHORDS, rhythm=((0, 2), (2, 2)), low=31, high=43, vel=0.6)
    groove(s, "driving", 16, 8, vel=0.6)
    timpani(s, o, CROWN_B_CHORDS, 8, vel=0.55)
    hit(s, o, crash=0.65)
    fill(s, o + 30, 2, "big", 0.85)

    # S4: tutti - trumpets and violins on the theme, horns beneath, everything driving.
    o = 96
    s.line("trumpets", o, CROWN_A, vel=0.8)
    s.line("violins", o, CROWN_A, vel=0.72)
    s.line("horns", o, transpose_line(CROWN_A, -12), vel=0.7)
    pad(s, "trombones", o, CROWN_A_CHORDS, 41, 58, count=2, vel=0.6)
    ostinato(s, "cellos", o, CROWN_A_CHORDS, [0, 0, 7, 0, 12, 0, 7, 0], step=0.5, base_low=38, base_high=50, vel=0.66)
    ostinato(s, "vla_spic", o, CROWN_A_CHORDS, [0, "3", 7, "3"], step=0.5, base_low=50, base_high=62, vel=0.55)
    bass(s, "basses", o, CROWN_A_CHORDS, rhythm=((0, 2), (2, 2)), low=31, high=43, vel=0.62)
    groove(s, "taiko_full", 24, 8, vel=0.64)
    timpani(s, o, CROWN_A_CHORDS, 8, vel=0.62)
    hit(s, o, crash=0.75, gong=0.4)
    fill(s, o + 30, 2, "toms", 0.8)
    return s
