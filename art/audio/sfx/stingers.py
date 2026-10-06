"""Short orchestral stingers for big moments, written with the same song engine as the score."""
from ak import dsp
from ak import song as S
from ak import foley as F
from music.common import roll
from sfx import cue
from sfx.kit import mix, s, space

STING = dict(level="stinger", cooldown=1.0, voices=1, pitch=0.0, stereo=True)


def _render(song, tail=3.0):
    return song.render(loop=False, tail=tail)


@cue("victory", 1, label="triumphant orchestral fanfare", **STING)
def victory(rng, k):
    sg = S.Song("victory", 100, bars=3, seed=301)
    sg.part("trumpets", "trumpet", gain_db=-3, pan=0.15, send=0.35)
    sg.part("tpt_stac", "trumpet_stac", gain_db=-4, pan=0.2, send=0.35)
    sg.part("horns", "horn", gain_db=-5, pan=-0.2, send=0.38)
    sg.part("violins", "violins_trem", gain_db=-9, pan=-0.35, send=0.35)
    sg.part("cellos", "cellos", gain_db=-8, pan=0.3, send=0.3)
    sg.part("harp", "harp", gain_db=-8, pan=0.4, send=0.4)
    sg.part("glock", "glockenspiel", gain_db=-16, pan=-0.4, send=0.45)
    sg.part("timp", "timpani_tuned", gain_db=-4, send=0.3, human=0.003)
    sg.part("crash", "crash", gain_db=-12, pan=0.3, send=0.4)
    sg.line("tpt_stac", 0, "A4:.333 A4:.333 A4:.334 D5:.5 F#5:.5", vel=0.8)
    sg.line("trumpets", 2, "A5:1 F#5:.5 A5:.5 D5:4", vel=0.85)
    sg.line("horns", 0, "[D4,F#4]:1 r:1 [E4,A4]:1 [C#4,E4]:1 [D4,F#4,A4]:6", vel=0.75)
    sg.line("cellos", 0, "D3:2 A2:2 D2:6", vel=0.7)
    sg.line("violins", 4, "[A4,D5,F#5]:5", vel=0.6)
    sg.line("harp", 4, "D4:.125 F#4:.125 A4:.125 D5:.125 F#5:.125 A5:.125 D6:.125 F#6:2", vel=0.6, damp=1.5)
    sg.line("glock", 4, "A6:2 D7:2", vel=0.5, damp=1.0)
    roll(sg, "timp", 1, 3, 0.25, 0.85, rate=12, pitch="A2")
    sg.n("timp", 4, "D3", 2, 0.95)
    sg.hit("crash", 4, 0.8)
    return _render(sg)


@cue("defeat", 1, label="sad orchestral ending", **STING)
def defeat(rng, k):
    sg = S.Song("defeat", 80, bars=3, seed=302)
    sg.part("horn", "horn", gain_db=-5, pan=-0.15, send=0.45, space="cathedral")
    sg.part("cellos", "cellos", gain_db=-7, pan=0.25, send=0.4, space="cathedral")
    sg.part("basses", "basses", gain_db=-9, pan=0.3, send=0.35, space="cathedral")
    sg.part("violas", "violas", gain_db=-11, pan=0.1, send=0.45, space="cathedral")
    sg.part("bell", "tubular_bells", gain_db=-10, pan=-0.4, send=0.5, space="cathedral", highcut=6500)
    sg.part("timp", "timpani_roll", gain_db=-16, send=0.4, space="cathedral")
    sg.line("horn", 0, "A4:1.5 G4:.5 F4:1 E4:1 D4:2 C#4:2 D4:4", vel=0.7)
    sg.line("cellos", 0, "[D3,A3]:4 [G2,D3]:2 [A2,E3]:2 [D3,A3]:4", vel=0.55)
    sg.line("basses", 0, "D2:4 G1:2 A1:2 D2:4", vel=0.55)
    sg.line("violas", 4, "Bb3:2 C#4:2 F4:4", vel=0.45)
    sg.n("bell", 0, "D4", 3, 0.6, damp=2.0)
    sg.n("bell", 8, "D4", 3, 0.45, damp=2.5)
    sg.hit("timp", 7, 0.4, beats=5)
    return _render(sg, 3.0)


@cue("boss_spawn", 1, label="ominous gong and low brass", **STING)
def boss_spawn(rng, k):
    sg = S.Song("boss_spawn", 120, bars=2, seed=303)
    sg.part("trombones", "trombone", gain_db=-3, pan=0.2, send=0.35)
    sg.part("tuba", "tuba", gain_db=-5, pan=0.1, send=0.3)
    sg.part("horns", "horn", gain_db=-5, pan=-0.2, send=0.38)
    sg.part("trem", "violins_trem", gain_db=-10, pan=-0.35, send=0.35)
    sg.part("timp", "timpani_tuned", gain_db=-3, send=0.3, human=0.003)
    sg.part("gong", "gong", gain_db=-6, pan=-0.1, send=0.45)
    sg.part("bd", "big_drum", gain_db=-6, send=0.3, lowcut=40)
    sg.line("trombones", 0, "D3:.5 Eb3:.5 D3:.5 Ab2:2.5", vel=0.85)
    sg.line("tuba", 0, "D2:.5 Eb2:.5 D2:.5 Ab1:2.5", vel=0.8)
    sg.line("horns", 1.5, "[D4,Ab4]:3", vel=0.75)
    sg.line("trem", 0, "[D5,Eb5]:5", vel=0.55)
    roll(sg, "timp", 0, 1.5, 0.3, 0.9, rate=12, pitch="D3")
    sg.n("timp", 1.5, "D3", 2, 1.0)
    sg.hit("gong", 1.5, 0.9, beats=4)
    sg.hit("bd", 1.5, 0.95)
    sg.hit("bd", 3, 0.8)
    return _render(sg, 3.5)


@cue("boss_death", 1, label="triumphant orchestral hit with bells", **STING)
def boss_death(rng, k):
    sg = S.Song("boss_death", 100, bars=2, seed=304)
    sg.part("brass", "horn", gain_db=-4, pan=-0.15, send=0.38)
    sg.part("trumpets", "trumpet", gain_db=-5, pan=0.2, send=0.38)
    sg.part("strings", "violins", gain_db=-8, pan=-0.3, send=0.35)
    sg.part("cellos", "cellos", gain_db=-8, pan=0.3, send=0.3)
    sg.part("bells", "tubular_bells", gain_db=-9, pan=0.4, send=0.45, highcut=7000)
    sg.part("timp", "timpani_tuned", gain_db=-3, send=0.3)
    sg.part("crash", "crash", gain_db=-10, pan=0.3, send=0.4)
    sg.part("harp", "harp", gain_db=-9, pan=-0.4, send=0.4)
    sg.line("brass", 0, "[A3,D4,F#4]:4", vel=0.85)
    sg.line("trumpets", 0, "[D5,F#5]:4", vel=0.8)
    sg.line("strings", 0, "[A4,D5,F#5]:4", vel=0.65)
    sg.line("cellos", 0, "D2:4", vel=0.7)
    sg.line("bells", 0, "D4:2 A4:2", vel=0.6, damp=2.0)
    sg.line("harp", 0.5, "D5:.125 F#5:.125 A5:.125 D6:.125 F#6:.125 A6:1.5", vel=0.55, damp=1.0)
    sg.n("timp", 0, "D3", 2, 1.0)
    sg.hit("crash", 0, 0.85)
    return _render(sg, 3.5)


@cue("boss_phase", 1, label="tense orchestral swell", **STING)
def boss_phase(rng, k):
    sg = S.Song("boss_phase", 120, bars=2, seed=305)
    sg.part("trem", "cellos_trem", gain_db=-6, pan=0.2, send=0.35)
    sg.part("vtrem", "violins_trem", gain_db=-9, pan=-0.3, send=0.35)
    sg.part("gong", "gong_scrape", gain_db=-8, send=0.4)
    sg.part("bd", "big_drum", gain_db=-6, send=0.3, lowcut=40)
    sg.line("trem", 0, "[D3,Eb3]:4", vel=0.6)
    sg.line("vtrem", 1, "[D5,Eb5,A5]:3", vel=0.5)
    sg.automate("trem", [(0, -12), (3.5, 0)])
    sg.automate("vtrem", [(0, -14), (3.5, 0)])
    sg.hit("gong", 0, 0.8, beats=4)
    for b, v in ((3.5, 0.85), (3.75, 0.9), (4, 1.0)):
        sg.hit("bd", b, v)
    return _render(sg, 2.5)


@cue("battle_start", 1, label="horn call with a snare drum roll", **STING)
def battle_start(rng, k):
    sg = S.Song("battle_start", 104, bars=2, seed=306)
    sg.part("horns", "horn", gain_db=-3, pan=-0.15, send=0.4, space="open")
    sg.part("snare", "snare_rope", gain_db=-8, pan=0.2, send=0.25, space="open")
    sg.part("crash", "crash", gain_db=-14, pan=0.3, send=0.35, space="open")
    sg.part("bd", "big_drum", gain_db=-8, send=0.3, lowcut=40, space="open")
    sg.line("horns", 0, "D4:.75 A4:.25 D5:2 r:1 A4:.5 D5:3", vel=0.85)
    roll(sg, "snare", 2, 2, 0.2, 0.85, rate=8)
    sg.hit("crash", 4, 0.7)
    sg.hit("bd", 4, 0.9)
    return _render(sg, 2.5)


@cue("wave_horn", 2, level="event", cooldown=2.0, voices=1, pitch=0.0, stereo=True, label="dark war horn blowing")
def wave_horn(rng, k):
    sg = S.Song("wave_horn", 80, bars=2, seed=307 + k)
    sg.part("horn", "horn", gain_db=-2, send=0.45, space="great_hall", highcut=3500)
    sg.part("horn2", "trombone", gain_db=-6, send=0.4, space="great_hall", highcut=2500)
    sg.part("bd", "big_drum", gain_db=-5, send=0.35, lowcut=35, space="great_hall")
    root = ["D2", "C2"][k]
    sg.line("horn", 0, f"{root}:3.5", vel=0.9)
    sg.line("horn2", 0.1, f"{root}:3.4", vel=0.8, transpose=12)
    sg.hit("bd", 0, 0.9)
    sg.hit("bd", 1, 0.8)
    x = _render(sg, 3.0)
    return dsp.saturate(x * 1.6, 1.4)


@cue("combo", 1, level="reward", cooldown=1.0, voices=1, pitch=0.0, stereo=True, label="bright chime fanfare")
def combo(rng, k):
    sg = S.Song("combo", 120, bars=1, seed=308)
    sg.part("harp", "harp", gain_db=-6, pan=0.3, send=0.35)
    sg.part("horns", "horn_stac", gain_db=-6, pan=-0.2, send=0.35)
    sg.part("glock", "glockenspiel", gain_db=-14, pan=-0.3, send=0.4)
    sg.line("harp", 0, "A4:.25 D5:.25 F#5:.25 A5:1", vel=0.6, damp=1.0)
    sg.line("horns", 0.75, "[D4,F#4,A4]:.5", vel=0.8)
    sg.line("glock", 0.75, "A6:1", vel=0.5, damp=1.0)
    return _render(sg, 2.0)
