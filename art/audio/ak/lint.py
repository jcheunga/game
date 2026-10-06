"""Symbolic score checks: sustained semitone clashes between parts, out-of-range notes, empty parts."""
import numpy as np

from . import theory as T


def clashes(song, min_overlap=0.75, min_len=1.0, ignore=()):
    notes = []
    for name, p in song.parts.items():
        if name in ignore or getattr(p.instrument, "unpitched", False):
            continue
        ring = 0.0
        for beat, midi, beats, vel, offset, detune, damp in p.events:
            # Plucked/struck notes keep sounding after their written length unless damped.
            sounding = beats
            if not p.instrument.sustain and damp is None and p.instrument.max_ring:
                sounding = beats + min(p.instrument.max_ring, 2.0) / song.beat_s
            if sounding >= min_len:
                notes.append((beat, beat + sounding, midi, name))
    notes.sort()
    out = []
    for i, (s1, e1, m1, p1) in enumerate(notes):
        for s2, e2, m2, p2 in notes[i + 1:]:
            if s2 >= e1:
                break
            if p1 == p2:
                continue
            overlap = min(e1, e2) - max(s1, s2)
            if overlap >= min_overlap and abs(m1 - m2) % 12 in (1, 11):
                out.append((round(max(s1, s2), 2), p1, m1, p2, m2, round(overlap, 2)))
    return out


def ranges(song):
    issues = []
    for name, p in song.parts.items():
        inst = p.instrument
        if getattr(inst, "unpitched", False) or not p.events:
            continue
        sampled = sorted({z.midi for z in inst.zones})
        lo, hi = sampled[0] - 3, sampled[-1] + 4
        bad = sorted({e[1] for e in p.events if e[1] < lo or e[1] > hi})
        if bad:
            issues.append((name, inst.name, bad, (sampled[0], sampled[-1])))
    return issues


def report(song, ignore=()):
    lines = []
    for item in ranges(song):
        lines.append(f"  RANGE {item[0]} ({item[1]}) notes {item[2]} outside sampled {item[3]}")
    for name, beat in song.overflow()[:10]:
        lines.append(f"  OVERFLOW {name} note at beat {beat} (song has {song.beats} beats)")
    found = clashes(song, ignore=ignore)
    for beat, p1, m1, p2, m2, ov in found[:40]:
        bar = int(beat // song.meter)
        lines.append(f"  CLASH bar {bar + 1} beat {beat % song.meter + 1:.2f}: {p1} {m1} vs {p2} {m2} ({ov} beats)")
    if len(found) > 40:
        lines.append(f"  ... {len(found) - 40} more clashes")
    return lines
