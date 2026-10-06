"""Note names, scales, chords and voice leading."""
import re

NOTE_OFFSETS = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
NOTE_RE = re.compile(r"^([A-Ga-g])([#b]?)(-?\d)$")

MODES = {
    "ionian": [0, 2, 4, 5, 7, 9, 11], "major": [0, 2, 4, 5, 7, 9, 11],
    "dorian": [0, 2, 3, 5, 7, 9, 10], "phrygian": [0, 1, 3, 5, 7, 8, 10],
    "lydian": [0, 2, 4, 6, 7, 9, 11], "mixolydian": [0, 2, 4, 5, 7, 9, 10],
    "aeolian": [0, 2, 3, 5, 7, 8, 10], "minor": [0, 2, 3, 5, 7, 8, 10],
    "harmonic": [0, 2, 3, 5, 7, 8, 11], "locrian": [0, 1, 3, 5, 6, 8, 10],
    "phrygian_dominant": [0, 1, 4, 5, 7, 8, 10],
}

CHORD_QUALITIES = {
    "": [0, 4, 7], "m": [0, 3, 7], "dim": [0, 3, 6], "aug": [0, 4, 8], "sus2": [0, 2, 7], "sus4": [0, 5, 7],
    "5": [0, 7], "7": [0, 4, 7, 10], "m7": [0, 3, 7, 10], "maj7": [0, 4, 7, 11], "add9": [0, 4, 7, 14],
    "madd9": [0, 3, 7, 14], "m6": [0, 3, 7, 9], "6": [0, 4, 7, 9], "msus4": [0, 5, 7],
}


def note(name):
    """'D4' -> 62 (C4 = 60). Integers pass through."""
    if isinstance(name, (int, float)):
        return int(name)
    m = NOTE_RE.match(name.strip())
    if not m:
        raise ValueError(f"bad note {name!r}")
    letter, acc, octave = m.groups()
    value = NOTE_OFFSETS[letter.upper()] + (1 if acc == "#" else -1 if acc == "b" else 0)
    return value + (int(octave) + 1) * 12


def pc(name):
    letter = name[0].upper()
    acc = name[1:2]
    return (NOTE_OFFSETS[letter] + (1 if acc == "#" else -1 if acc == "b" else 0)) % 12


def chord(symbol):
    """'Dm', 'Bb', 'F#m7', 'Gsus4', 'C/E' -> (root_pc, [intervals], bass_pc)."""
    main, _, bass = symbol.partition("/")
    root_len = 2 if len(main) > 1 and main[1] in "#b" else 1
    root = pc(main[:root_len])
    quality = main[root_len:]
    intervals = CHORD_QUALITIES[quality]
    return root, intervals, pc(bass) if bass else root


def chord_tones(symbol):
    root, intervals, _ = chord(symbol)
    return [(root + i) % 12 for i in intervals]


def voice(symbol, low, high, count=4, previous=None, include_bass=False):
    """Choose `count` chord tones in [low, high] that move least from `previous` (smooth voice leading)."""
    root, intervals, bass = chord(symbol)
    pcs = [(root + i) % 12 for i in intervals]
    candidates = [m for m in range(low, high + 1) if m % 12 in pcs]
    best, best_cost = None, None
    import itertools
    for combo in itertools.combinations(candidates, count):
        present = {m % 12 for m in combo}
        # Must contain root and third (or all tones for small chords); two voices need the root plus another tone.
        needed = set(pcs[:3]) if len(pcs) >= 3 else set(pcs)
        if count >= len(needed) and not needed <= present:
            continue
        if count < len(needed) and (pcs[0] not in present or len(present) < count):
            continue
        spread = combo[-1] - combo[0]
        if spread > 19:
            continue
        gaps = [b - a for a, b in zip(combo, combo[1:])]
        if any(g < 2 for g in gaps):
            continue
        if previous:
            cost = sum(min(abs(c - p) for p in previous) for c in combo) + sum(min(abs(c - p) for c in combo) for p in previous)
        else:
            centre = (low + high) / 2
            cost = abs(sum(combo) / count - centre) * 2
        cost += max(0, 3 - min(gaps or [12])) * 2
        if best_cost is None or cost < best_cost:
            best, best_cost = combo, cost
    if best is None:
        best = tuple(candidates[:count])
    return list(best)


def bass_note(symbol, low=36, high=50):
    _, _, bass = chord(symbol)
    for m in range(low, high + 1):
        if m % 12 == bass:
            return m
    return low + bass


def scale_notes(tonic, mode, low, high):
    root = pc(tonic)
    steps = MODES[mode]
    return [m for m in range(low, high + 1) if (m - root) % 12 in steps]


def parse_line(text, default_octave=4):
    """Melody notation: 'D5:1 F5:.5 E5:.5 r:2 [D4,F4,A4]:4 D5:1:0.8' -> [(midi|list|None, beats, vel)]."""
    out = []
    for token in text.split():
        parts = token.split(":")
        head = parts[0]
        beats = float(parts[1]) if len(parts) > 1 and parts[1] else 1.0
        vel = float(parts[2]) if len(parts) > 2 else None
        if head in ("r", "-"):
            pitch = None
        elif head.startswith("["):
            pitch = [note(p) for p in head.strip("[]").split(",")]
        else:
            pitch = note(head)
        out.append((pitch, beats, vel))
    return out
