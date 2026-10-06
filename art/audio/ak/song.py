"""Song engine: parts, note events, humanisation, mixing, reverb sends, seamless loops and mastering."""
from collections import defaultdict

import numpy as np

from . import dsp, instruments, reverb, theory
from .dsp import SR


class Part:
    def __init__(self, name, instrument, gain_db=0.0, pan=0.0, width=1.0, space="hall", send=0.22, eq=None,
                 lowcut=None, highcut=None, human=0.008, vel_human=0.05, strum=0.0, release=None, comp=None,
                 send_lowcut=180):
        self.name = name
        self.instrument = instruments.get(instrument) if isinstance(instrument, str) else instrument
        self.gain = gain_db
        self.pan = pan
        self.width = width
        self.space = space
        self.send = send
        self.eq = eq or []
        self.lowcut = lowcut
        self.highcut = highcut
        self.human = human
        self.vel_human = vel_human
        self.strum = strum
        self.release = release
        self.comp = comp
        self.send_lowcut = send_lowcut
        self.events = []
        self.automation = []  # (beat, db)


class Song:
    def __init__(self, name, bpm, bars, meter=4, swing=0.0, seed=1, pickup_beats=0):
        self.name = name
        self.bpm = bpm
        self.bars = bars
        self.meter = meter
        self.swing = swing
        self.parts = {}
        self.rng = np.random.default_rng(seed)
        self.beat_s = 60.0 / bpm
        self.custom_layers = []  # (buffer, beat, gain_db, space, send)

    # ------------------------------------------------------------ timing
    @property
    def beats(self):
        return self.bars * self.meter

    @property
    def length(self):
        return self.beats * self.beat_s

    def time(self, beat):
        if self.swing:
            whole = np.floor(beat * 2) / 2
            frac = beat * 2 - np.floor(beat * 2)
            if abs((whole * 2) % 2 - 1) < 1e-6 and frac < 1e-6:
                beat = beat + self.swing * 0.5
        return beat * self.beat_s

    def bar(self, index, beat=0.0):
        return index * self.meter + beat

    # ------------------------------------------------------------ authoring
    def part(self, name, instrument, **kwargs):
        self.parts[name] = Part(name, instrument, **kwargs)
        return self.parts[name]

    def n(self, part, beat, pitch, beats, vel=0.8, legato=1.0, detune=0.0, damp=None):
        """Add a note (pitch int/name) or chord (list) at beat lasting beats.

        damp (seconds): for plucked/struck instruments, stop the ring this long after the note ends."""
        if pitch is None:
            return
        pitches = pitch if isinstance(pitch, (list, tuple)) else [pitch]
        p = self.parts[part]
        for i, m in enumerate(pitches):
            offset = i * p.strum if p.strum else 0.0
            p.events.append((beat, theory.note(m), beats * legato, vel, offset, detune, damp))

    def line(self, part, beat, text, vel=0.8, legato=0.98, transpose=0, accent_downbeats=0.0, damp=None):
        """Write a melody in notation (see theory.parse_line); returns the beat after the line."""
        pos = beat
        for pitch, beats, v in theory.parse_line(text):
            if pitch is not None:
                pv = v if v is not None else vel
                if accent_downbeats and abs(pos - round(pos)) < 1e-6 and int(round(pos)) % self.meter == 0:
                    pv = min(1.0, pv + accent_downbeats)
                if isinstance(pitch, list):
                    self.n(part, pos, [m + transpose for m in pitch], beats, pv, legato, damp=damp)
                else:
                    self.n(part, pos, pitch + transpose, beats, pv, legato, damp=damp)
            pos += beats
        return pos

    def hit(self, part, beat, vel=0.8, detune=0.0, beats=0.5, pitch=60):
        self.parts[part].events.append((beat, theory.note(pitch), beats, vel, 0.0, detune, None))

    def pattern(self, part, start_bar, bars, grid, steps_per_beat=2, vel=0.8, accents=None, detune=0.0, humanize=True):
        """Drum grid: 'x' hit, 'X' accent, '.' rest, 'o' soft; repeats every len(grid) steps."""
        step = 1.0 / steps_per_beat
        total = int(bars * self.meter * steps_per_beat)
        for k in range(total):
            ch = grid[k % len(grid)]
            if ch == ".":
                continue
            v = {"x": vel, "X": min(1.0, vel + 0.18), "o": vel * 0.55, "g": vel * 0.3}.get(ch, vel)
            self.hit(part, start_bar * self.meter + k * step, v, detune)

    def automate(self, part, points):
        """Gain automation for a part: [(beat, db), ...] (linear in dB between points)."""
        self.parts[part].automation = sorted(points)

    def layer(self, buffer, beat, gain_db=0.0, space=None, send=0.0):
        """Mix a pre-rendered buffer (e.g. a synthesized texture) at a beat."""
        self.custom_layers.append((buffer, beat, gain_db, space, send))

    # ------------------------------------------------------------ rendering
    def _render_part(self, p, total_n):
        buf = np.zeros((total_n, 2))
        rng = self.rng
        for beat, midi, beats, vel, offset, detune, damp in p.events:
            v = float(np.clip(vel + rng.normal(0, p.vel_human), 0.05, 1.0))
            dur = beats * self.beat_s
            start = self.time(beat) + offset + (rng.normal(0, p.human) if p.human else 0.0)
            start = max(0.0, start) if beat == 0 and not offset else start
            note = p.instrument.note(midi, v, dur, rng=rng, release=p.release if damp is None else damp, detune=detune)
            dsp.place(buf, note, start % self.length if start < 0 else start, 1.0, wrap=False)
        if p.lowcut:
            buf = dsp.highpass(buf, p.lowcut, 2)
        if p.highcut:
            buf = dsp.lowpass(buf, p.highcut, 2)
        for band in p.eq:
            buf = dsp.eq(buf, band)
        if p.comp:
            buf = dsp.compress(buf, **p.comp)
        if p.automation:
            beats_axis = np.arange(total_n) / SR / self.beat_s
            xs = [b for b, _ in p.automation]
            ys = [d for _, d in p.automation]
            buf *= dsp.db(np.interp(beats_axis, xs, ys))[:, None]
        if p.width != 1.0:
            buf = dsp.width(buf, p.width)
        if p.pan:
            buf = balance(buf, p.pan)
        return buf * dsp.db(p.gain)

    def overflow(self):
        """Notes written past the end of the song (they would wrap onto the loop start)."""
        return [(name, e[0]) for name, p in self.parts.items() for e in p.events if e[0] >= self.beats - 1e-6]

    def render(self, loop=True, tail=7.0, stems=False):
        loop_n = dsp.samples(self.length)
        total_n = loop_n + dsp.samples(tail)
        dry = np.zeros((total_n, 2))
        sends = defaultdict(lambda: np.zeros((total_n, 2)))
        send_cut = {}
        rendered = {}
        for name, p in self.parts.items():
            if not p.events:
                continue
            buf = self._render_part(p, total_n)
            rendered[name] = buf
            dry += buf
            if p.space and p.send > 0:
                sends[p.space] += buf * p.send
                send_cut[p.space] = min(send_cut.get(p.space, 1e9), p.send_lowcut)
        for buffer, beat, gain_db, space, send in self.custom_layers:
            tmp = np.zeros((total_n, 2))
            dsp.place(tmp, buffer, self.time(beat), dsp.db(gain_db))
            dry += tmp
            if space and send:
                sends[space] += tmp * send
        for space, bus in sends.items():
            wet = reverb.reverb(bus, space, wet=1.0, dry=0.0, tail=False, lowcut=send_cut.get(space, 180))
            dry += wet[:total_n]
        if loop:
            out = dry[:loop_n].copy()
            over = dry[loop_n:]
            k = 0
            while k < len(over):
                seg = over[k:k + loop_n]
                out[: len(seg)] += seg
                k += loop_n
            dry = out
        return (dry, rendered) if stems else dry


def balance(x, position):
    """Stereo balance that keeps a stereo recording's image (unlike a mono pan)."""
    position = float(np.clip(position, -1, 1))
    left = np.sqrt(min(1.0, 1 - position)) if position > 0 else 1.0
    right = np.sqrt(min(1.0, 1 + position)) if position < 0 else 1.0
    out = x.copy()
    out[:, 0] *= left
    out[:, 1] *= right
    # Bleed a little of the attenuated side into the favoured side so nothing collapses.
    if position > 0:
        out[:, 1] += x[:, 0] * (1 - left) * 0.5
    elif position < 0:
        out[:, 0] += x[:, 1] * (1 - right) * 0.5
    return out


def master(x, lufs=-17.0, ceiling=-1.2, low=28, air=1.0, glue=True, loop=True):
    """Gentle mastering chain: rumble cut, tonal balance, glue compression, loudness target, true-peak-ish limit.

    For loops the chain runs on a circularly padded copy, so filter states and compressor/limiter envelopes
    at the start already 'know' the end - the seam stays sample-continuous."""
    pad = min(len(x), dsp.samples(3.0)) if loop else 0
    y = np.concatenate([x[-pad:], x, x[:pad]]) if pad else x
    y = dsp.highpass(y, low, 2)
    y = dsp.eq(y, ("lowshelf", 120, 0.7, 0.5), ("peak", 320, 0.8, -1.0), ("highshelf", 9000, 0.7, air))
    if glue:
        y = dsp.compress(y, threshold_db=-20, ratio=1.8, attack=0.03, release=0.25, makeup_db=0, knee_db=10)
    core = y[pad:pad + len(x)] if pad else y
    gain = dsp.db(lufs - dsp.loudness(core))
    y = y * gain
    if np.abs(y).max() > dsp.db(ceiling):
        y = dsp.limit(y, ceiling)
    y = y[pad:pad + len(x)] if pad else y
    return y - y.mean(axis=0) if loop else y


def ease_out(song, parts, start_beat, end_beat, db=-8.0):
    """Thin the named parts over the last beats so a loud ending hands back to a quiet loop start."""
    for name in parts:
        if name in song.parts:
            song.automate(name, [(0, 0), (start_beat, 0), (end_beat, db)])
