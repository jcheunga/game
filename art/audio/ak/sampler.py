"""Multi-sample instrument player for the CC0 libraries in art/audio/samples.

Each Instrument scans folders of recordings, reads pitch / velocity layer / round robin from the
filenames, verifies the pitch of every recording (fixing octave naming conventions and tuning
drift), levels the keyboard, and renders notes with release envelopes and seamless sustain extension.
"""
import json
import re
from functools import lru_cache
from pathlib import Path

import numpy as np
import soundfile as sf
from scipy import signal

from . import dsp
from .dsp import SR

SAMPLES = Path(__file__).resolve().parents[1] / "samples"
CACHE = Path(__file__).resolve().parents[1] / ".cache"
NOTE_TOKEN = re.compile(r"^([A-Ga-g])(#|b)?(-?\d)$")
DYNAMICS = {"ppp": 1, "pp": 2, "p": 3, "mp": 4, "mf": 5, "f": 6, "ff": 7, "fff": 8}


def _note_value(letter, acc, octave):
    base = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}[letter.upper()]
    return base + (1 if acc == "#" else -1 if acc == "b" else 0) + (int(octave) + 1) * 12


def parse_name(stem):
    tokens = re.split(r"[_\s\-]+", stem)
    midi = layer = rr = None
    for tok in tokens:
        m = NOTE_TOKEN.match(tok)
        if m and midi is None:
            midi = _note_value(*m.groups())
            continue
        low = tok.lower()
        if re.fullmatch(r"v[l]?\d", low) and layer is None:
            layer = int(low[-1])
        elif low in DYNAMICS and layer is None:
            layer = DYNAMICS[low]
        elif re.fullmatch(r"(rr|r)\d+", low):
            rr = int(re.sub(r"\D", "", low))
    return midi, layer, rr


def load_wav(path):
    data, rate = sf.read(str(path), always_2d=True, dtype="float32")
    if data.shape[1] == 1:
        data = np.repeat(data, 2, axis=1)
    data = data[:, :2]
    if rate != SR:
        g = np.gcd(rate, SR)
        data = signal.resample_poly(data, SR // g, rate // g, axis=0).astype(np.float32)
    return data


def onset_index(x, threshold_db=-42):
    level = np.abs(x).max(axis=1)
    peak = level.max()
    if peak <= 0:
        return 0
    idx = np.nonzero(level > peak * dsp.db(threshold_db))[0]
    return max(0, int(idx[0]) - dsp.samples(0.003)) if len(idx) else 0


def _band_peak(mag, freqs, centre, cents=60):
    lo, hi = centre * 2 ** (-cents / 1200), centre * 2 ** (cents / 1200)
    i, j = np.searchsorted(freqs, lo), np.searchsorted(freqs, hi)
    return float(mag[i:j].max()) if j > i else 0.0


def octave_vote(x, start, named_midi):
    """Octave correction (semitones, multiple of 12) for a recording labelled named_midi.

    The true fundamental is the lowest octave candidate whose odd partials (f, 3f) are present;
    odd partials of a candidate an octave too low are absent, which rejects subharmonic guesses."""
    mono = (x[:, 0] + x[:, 1]).astype(np.float64)
    seg = mono[start + dsp.samples(0.1): start + dsp.samples(0.1) + 32768]
    if len(seg) < 4096:
        return None
    seg = seg * np.hanning(len(seg))
    mag = np.abs(np.fft.rfft(seg, 1 << 18))
    freqs = np.fft.rfftfreq(1 << 18, 1 / SR)
    peak = mag.max() + 1e-12
    for k in (-24, -12, 0, 12, 24):
        f = float(dsp.mtof(named_midi + k))
        if f < 20 or f > SR * 0.4:
            continue
        odd = max(_band_peak(mag, freqs, f), _band_peak(mag, freqs, 3 * f) if 3 * f < SR * 0.45 else 0.0) / peak
        if odd > 0.1:
            return k
    return None


def measure_cents(x, start, nominal_midi, harmonics=5, search_cents=90):
    """Tuning of a recording relative to its nominal pitch, from long-window FFT peaks near each harmonic.

    Searching only near the expected partials avoids octave and harmonic slips; returns (cents, strength)."""
    mono = (x[:, 0] + x[:, 1]).astype(np.float64)
    begin = start + dsp.samples(0.15)
    seg = mono[begin:begin + dsp.samples(1.6)]
    if len(seg) < dsp.samples(0.25):
        seg = mono[start:start + dsp.samples(1.0)]
    if len(seg) < 2048:
        return 0.0, 0.0
    seg = seg * np.hanning(len(seg))
    nfft = 1 << 19
    mag = np.abs(np.fft.rfft(seg, nfft))
    bin_hz = SR / nfft
    f0 = float(dsp.mtof(nominal_midi))
    floor = np.median(mag) + 1e-12
    cents, weights = [], []
    for h in range(1, harmonics + 1):
        centre = f0 * h
        if centre > SR * 0.45:
            break
        lo = int(centre * 2 ** (-search_cents / 1200) / bin_hz)
        hi = int(centre * 2 ** (search_cents / 1200) / bin_hz) + 1
        band = mag[lo:hi]
        if len(band) < 3:
            continue
        k = int(np.argmax(band))
        if 0 < k < len(band) - 1:
            a, b, c = np.log(band[k - 1] + 1e-12), np.log(band[k] + 1e-12), np.log(band[k + 1] + 1e-12)
            off = 0.5 * (a - c) / (a - 2 * b + c) if (a - 2 * b + c) != 0 else 0.0
        else:
            off = 0.0
        freq = (lo + k + off) * bin_hz
        strength = band[k] / floor
        if strength < 8:
            continue
        cents.append(1200 * np.log2(freq / centre))
        weights.append(strength / h)
    if not cents:
        return 0.0, 0.0
    order = np.argsort(cents)
    cents, weights = np.array(cents)[order], np.array(weights)[order]
    cum = np.cumsum(weights) / weights.sum()
    return float(cents[np.searchsorted(cum, 0.5)]), float(weights.sum())


class Zone:
    __slots__ = ("path", "midi", "layer", "rr", "cents", "gain", "onset", "length")

    def __init__(self, path, midi, layer, rr):
        self.path, self.midi, self.layer, self.rr = path, midi, layer, rr
        self.cents, self.gain, self.onset, self.length = 0.0, 1.0, 0, 0


class Instrument:
    """A pitched (or unpitched) multi-sample instrument.

    folders: paths relative to art/audio/samples; include/exclude: regexes on the relative path.
    sustain: True for bowed/blown/organ tones (note length is honoured and extended), False for plucked/struck.
    """

    def __init__(self, name, folders, include=None, exclude=None, sustain=True, release=0.25, gain_db=0.0,
                 tune=True, unpitched=False, max_ring=None, attack_trim=0.0, fade_in=0.0, lowcut=None,
                 tone=None, layer_curve=1.0, pitch_hint=None, pitch_map=None):
        self.name = name
        self.folders = [folders] if isinstance(folders, str) else folders
        self.include = re.compile(include) if include else None
        self.exclude = re.compile(exclude) if exclude else None
        self.sustain = sustain
        self.release = release
        self.gain = dsp.db(gain_db)
        self.tune = tune and not unpitched
        self.unpitched = unpitched
        self.max_ring = max_ring
        self.attack_trim = attack_trim
        self.fade_in = fade_in
        self.lowcut = lowcut
        self.tone = tone or []
        self.layer_curve = layer_curve
        self.pitch_hint = pitch_hint
        # {regex: measured midi (float)} for recordings without note names (e.g. timpani drums).
        self.pitch_map = pitch_map or {}
        self._zones = None
        self._audio = {}
        self._last_rr = {}
        self._rng = np.random.default_rng(abs(hash(name)) % (2 ** 32))

    # ------------------------------------------------------------ scanning & analysis
    def _scan(self):
        files = []
        for folder in self.folders:
            base = SAMPLES / folder
            for path in sorted(list(base.rglob("*.wav")) + list(base.rglob("*.ogg"))):
                rel = str(path.relative_to(SAMPLES))
                if self.include and not self.include.search(rel):
                    continue
                if self.exclude and self.exclude.search(rel):
                    continue
                files.append(path)
        if not files:
            raise FileNotFoundError(f"{self.name}: no samples in {self.folders}")
        zones = []
        for i, path in enumerate(files):
            midi, layer, rr = parse_name(path.stem)
            cents = 0.0
            for pattern, value in self.pitch_map.items():
                if re.search(pattern, path.name):
                    midi, cents = int(round(value)), (value - round(value)) * 100
            if self.unpitched:
                midi = 60
            if midi is None:
                continue
            zone = Zone(path, midi, layer or 1, rr if rr is not None else i)
            zone.cents = cents
            zones.append(zone)
        return zones

    @property
    def zones(self):
        if self._zones is None:
            self._zones = self._scan()
            self._analyse()
        return self._zones

    def _cache_file(self):
        CACHE.mkdir(parents=True, exist_ok=True)
        return CACHE / f"inst_{re.sub(r'[^a-z0-9]+', '_', self.name.lower())}.json"

    def _analyse(self):
        cache_path = self._cache_file()
        key = [str(z.path.relative_to(SAMPLES)) for z in self._zones]
        cached = json.loads(cache_path.read_text()) if cache_path.exists() else None
        if cached and cached.get("files") == key and cached.get("version") == 6:
            for z, info in zip(self._zones, cached["zones"]):
                z.midi, z.cents, z.gain, z.onset, z.length = info
            return
        votes = []
        rms = []
        for z in self._zones:
            x = load_wav(z.path)
            z.onset = onset_index(x)
            z.length = len(x) - z.onset
            body = x[z.onset: z.onset + dsp.samples(0.6)]
            rms.append(float(np.sqrt(np.mean(body.astype(np.float64) ** 2)) + 1e-9))
            if self.tune:
                vote = octave_vote(x, z.onset, z.midi)
                if vote is not None:
                    votes.append(vote)
        if not self.tune and self.pitch_hint:
            # Trusted names in a library-specific octave convention (e.g. VCSL idiophones: C3 = middle C).
            for z in self._zones:
                z.midi += self.pitch_hint
        if self.tune:
            octave = max(set(votes), key=votes.count) if votes else 0
            if self.pitch_hint is not None:
                octave = self.pitch_hint
            for z in self._zones:
                z.midi += octave
                cents, strength = measure_cents(load_wav(z.path), z.onset, z.midi)
                z.cents = cents if strength > 0 else 0.0
        # Level the keyboard: the loudest layer of each pitch is matched, quieter layers keep their ratio.
        by_note = {}
        for z, r in zip(self._zones, rms):
            by_note.setdefault(z.midi, []).append((z, r))
        for midi, items in by_note.items():
            top_layer = max(z.layer for z, _ in items)
            top = np.mean([r for z, r in items if z.layer == top_layer])
            for z, r in items:
                z.gain = float(0.1 / top)
        cache_path.write_text(json.dumps({"version": 6, "files": key,
                                          "zones": [[z.midi, z.cents, z.gain, z.onset, z.length] for z in self._zones]}))

    def report(self):
        zs = self.zones
        layers = sorted({z.layer for z in zs})
        notes = sorted({z.midi for z in zs})
        cents = [z.cents for z in zs]
        return (f"{self.name:24s} zones={len(zs):3d} layers={layers} range={notes[0]}-{notes[-1]} "
                f"cents[{min(cents):+.0f},{max(cents):+.0f}] len~{np.median([z.length for z in zs]) / SR:.1f}s")

    # ------------------------------------------------------------ playback
    def _audio_for(self, z):
        if z.path not in self._audio:
            x = load_wav(z.path)[z.onset:]
            start = dsp.samples(self.attack_trim)
            x = x[start:].astype(np.float64) * z.gain * self.gain
            if self.lowcut:
                x = dsp.highpass(x, self.lowcut, 2)
            for band in self.tone:
                x = dsp.eq(x, band)
            fi = max(dsp.samples(self.fade_in), 32)
            x[:fi] *= np.linspace(0, 1, fi)[:, None]
            self._audio[z.path] = x
        return self._audio[z.path]

    def layers(self):
        return sorted({z.layer for z in self.zones})

    def pick(self, midi, velocity, rng=None):
        rng = rng or self._rng
        layers = self.layers()
        pos = np.clip(velocity, 0, 1) ** self.layer_curve * (len(layers) - 1)
        lo = int(np.floor(pos))
        frac = pos - lo
        idx = lo + (1 if rng.random() < frac else 0)
        layer = layers[min(idx, len(layers) - 1)]
        candidates = [z for z in self.zones if z.layer == layer]
        nearest = min(abs(z.midi - midi) for z in candidates)
        pool = [z for z in candidates if abs(z.midi - midi) == nearest]
        # Prefer sampling down (pitching a recording up slightly sounds more natural than down).
        below = [z for z in pool if z.midi <= midi]
        pool = below or pool
        last = self._last_rr.get((midi, layer))
        choices = [z for z in pool if z.path != last] or pool
        z = choices[int(rng.integers(len(choices)))]
        self._last_rr[(midi, layer)] = z.path
        nominal = idx / max(1, len(layers) - 1) if len(layers) > 1 else velocity
        return z, nominal

    def note(self, midi, velocity=0.8, duration=1.0, rng=None, release=None, detune=0.0):
        """Render one note. duration is the held length in seconds; output includes the release/ring.

        detune adds semitones (used to pitch unpitched hits up or down)."""
        z, nominal = self.pick(midi, velocity, rng)
        src = self._audio_for(z)
        shift = (0.0 if self.unpitched else (midi - z.midi) - z.cents / 100) + detune
        x = shifted(src, round(shift, 3), id(src))
        # Fine velocity shading between layers.
        x = x * ((0.25 + velocity) / (0.25 + nominal)) ** 0.9 if len(self.layers()) > 1 else x * (0.2 + 0.8 * velocity) ** 1.3
        release = self.release if release is None else release
        if self.sustain:
            need = dsp.samples(duration + release)
            if len(x) < need:
                x = extend(x, need)
            held = dsp.samples(duration)
            out = x[:need].copy()
            rel_n = need - held
            if rel_n > 0:
                curve = np.exp(-np.linspace(0, 6.5, rel_n))
                out[held:] *= curve[:, None]
            return out
        if release is not None:
            # Damped plucked/struck note: ring until duration, then die away over `release` seconds.
            n = min(len(x), dsp.samples(max(duration, 0.02) + release))
            out = x[:n].copy()
            held = min(n, dsp.samples(max(duration, 0.02)))
            if n > held:
                out[held:] *= np.exp(-np.linspace(0, 6.5, n - held))[:, None]
            return out
        ring = self.max_ring
        if ring is not None:
            n = min(len(x), dsp.samples(max(duration, 0.05) + ring))
            out = x[:n].copy()
            fade_n = min(n, dsp.samples(min(ring, 0.6)))
            out[-fade_n:] *= np.linspace(1, 0, fade_n)[:, None] ** 2
            return out
        return x.copy()




_SHIFT_CACHE = {}


def shifted(src, semitones, src_id):
    key = (src_id, semitones)
    hit = _SHIFT_CACHE.get(key)
    if hit is not None:
        return hit
    y = dsp.resample_ratio(src, 2 ** (semitones / 12)) if abs(semitones) > 1e-4 else src
    if len(_SHIFT_CACHE) > 6000:
        _SHIFT_CACHE.clear()
    _SHIFT_CACHE[key] = y
    return y


def extend(x, need, xfade=0.18):
    """Lengthen a sustained recording by crossfading segments from its stable middle (no audible loop point)."""
    n = len(x)
    lo, hi = int(n * 0.35), int(n * 0.85)
    if hi - lo < dsp.samples(0.4):
        lo, hi = int(n * 0.2), n - dsp.samples(0.05)
    fade = min(dsp.samples(xfade), (hi - lo) // 3)
    out = np.zeros((need, 2))
    head = x[:hi]
    out[: min(need, len(head))] = head[:need]
    pos = len(head) - fade
    rng = np.random.default_rng(len(x))
    ramp_in = np.sin(np.linspace(0, np.pi / 2, fade))[:, None]
    ramp_out = np.cos(np.linspace(0, np.pi / 2, fade))[:, None]
    while pos < need:
        seg_len = int(rng.integers((hi - lo) // 2, hi - lo))
        start = int(rng.integers(lo, max(lo + 1, hi - seg_len)))
        seg = x[start:start + seg_len].copy()
        seg[:fade] *= ramp_in
        end = min(need, pos + len(seg))
        out[pos:pos + fade] *= ramp_out[: max(0, min(fade, need - pos))]
        out[pos:end] += seg[: end - pos]
        pos = end - fade
        if end >= need:
            break
    return out
