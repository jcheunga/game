"""Shared battle arranging: drum parts, grooves, fills and section helpers."""
from music.common import chord_at, chord_root, roll


def drum_parts(s, space="hall", level=0.0):
    """Register the standard battle percussion section (levels in dB relative to `level`)."""
    s.part("bd", "big_drum", gain_db=-10 + level, send=0.3, space=space, human=0.004, lowcut=40,
           eq=[("peak", 90, 1.0, -3)])
    s.part("sticks", "big_drum_sticks", gain_db=-13 + level, pan=0.2, send=0.25, space=space, human=0.004)
    s.part("hand", "big_drum_hand", gain_db=-12 + level, pan=-0.2, send=0.25, space=space, human=0.004)
    s.part("snare", "snare_rope", gain_db=-14 + level, pan=-0.2, send=0.2, space=space, human=0.004)
    s.part("toms", "tom", gain_db=-11 + level, pan=0.15, send=0.25, space=space, human=0.004)
    s.part("timp", "timpani_tuned", gain_db=-6 + level, send=0.3, space=space, human=0.004)
    s.part("crash", "crash", gain_db=-17 + level, pan=0.3, send=0.35, space=space)
    s.part("cymbal", "sus_cymbal", gain_db=-18 + level, pan=-0.3, send=0.4, space=space)
    s.part("gong", "gong", gain_db=-14 + level, pan=-0.15, send=0.45, space=space)


GROOVES = {
    # 16th-note grids (16 steps per 4/4 bar)
    "march": {"snare": "x.xxx.x.x.xxX.x.", "bd": "X.......x.......", "sticks": "....x.......x..."},
    "march_full": {"snare": "x.xxx.xxx.xxX.xx", "bd": "X.....x.X.......", "sticks": "....X.......X..."},
    "taiko": {"bd": "X..x..X...x.X...", "hand": "..x...x...x...x.", "sticks": "....x.......x..x"},
    "taiko_full": {"bd": "X..x..X.x.x.X..x", "hand": "x.x.x.x.x.x.x.x.", "sticks": "....X..x....X.xx", "snare": "....x.......x..."},
    "driving": {"bd": "X.x.X.x.X.x.X.x.", "snare": "....X.......X...", "sticks": "..x...x...x...x."},
    "driving_full": {"bd": "X.xxX.x.X.xxX.x.", "snare": "....X..x....X.xx", "sticks": "x.x.x.x.x.x.x.x.", "hand": "..x...x...x...x."},
    "halftime": {"bd": "X.......X..x....", "snare": "........X.......", "hand": "....x.......x..."},
    "pulse": {"bd": "X...X...X...X...", "hand": "..x...x...x...x."},
    "forge": {"bd": "X..xX...X..xX...", "sticks": "....X.......X...", "snare": "..x...x...x...x."},
    "gallop": {"hand": "X.xxX.xxX.xxX.xx", "bd": "X.......X.......", "sticks": "....x.......x..."},
    # 6/8 grids (6 steps per bar, eighths)
    "jig": {"bd": "X..x..", "hand": "..x..x", "sticks": "...x.."},
    "jig_full": {"bd": "X.xX..", "hand": "x.xx.x", "sticks": "...X..", "snare": "..x..x"},
}


def groove(s, name, start_bar, bars, vel=0.62, skip=()):
    steps = 6 if name.startswith("jig") else 16
    per_beat = 1 if steps == 6 else 4
    for part, grid in GROOVES[name].items():
        if part in skip or part not in s.parts:
            continue
        s.pattern(part, start_bar, bars, grid, steps_per_beat=per_beat, vel=vel)


def timpani(s, start, prog, bars, every=1, beats_per_bar=4, vel=0.6, offbeat=True):
    """Timpani on chord roots each bar (and the fifth on beat 3 when offbeat)."""
    for bar in range(0, bars, every):
        b = bar * beats_per_bar
        s.n("timp", start + b, chord_root(chord_at(prog, b % sum(x for _, x in prog)), 41, 55), 1, vel)
        if offbeat and beats_per_bar == 4:
            s.n("timp", start + b + 2, chord_root(chord_at(prog, (b + 2) % sum(x for _, x in prog)), 41, 55,
                                                  prefer_fifth=True), 1, vel * 0.8)


def fill(s, beat, beats=2, kind="toms", vel=0.75):
    """A drum fill into the next section, ending on the downbeat after `beat + beats`."""
    if kind == "toms":
        n = int(beats * 4)
        for k in range(n):
            s.hit("toms", beat + k / 4, vel * (0.6 + 0.4 * k / n), detune=-3 * (k // 4))
        s.hit("bd", beat + beats - 0.5, vel)
    elif kind == "snare":
        roll(s, "snare", beat, beats, 0.3, vel, rate=8)
    elif kind == "timp":
        roll(s, "timp", beat, beats, 0.3, vel, rate=10, pitch="A2")
    elif kind == "big":
        for k, off in enumerate((0, 0.75, 1.5)):
            if off < beats:
                s.hit("bd", beat + off, vel)
        roll(s, "snare", beat + beats / 2, beats / 2, 0.3, vel, rate=8)


def hit(s, beat, crash=0.7, gong=0.0, bd=0.85):
    if crash:
        s.hit("crash", beat, crash)
    if gong:
        s.hit("gong", beat, gong)
    if bd:
        s.hit("bd", beat, bd)
