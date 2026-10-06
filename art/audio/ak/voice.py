"""Source-filter creature and crowd voices: glottal pulses with jitter, shimmer and subharmonics through moving formants."""
import numpy as np
from scipy import signal

from . import dsp
from .dsp import SR

# Adult male formant centres (Hz) and bandwidths for the vowels creature calls glide between.
VOWELS = {
    "a": ([730, 1090, 2440, 3300, 3750], [90, 110, 160, 250, 300]),
    "o": ([570, 840, 2410, 3300, 3750], [80, 100, 160, 250, 300]),
    "u": ([300, 870, 2240, 3300, 3750], [60, 100, 160, 250, 300]),
    "e": ([530, 1840, 2480, 3300, 3750], [70, 120, 160, 250, 300]),
    "i": ([270, 2290, 3010, 3500, 3900], [60, 120, 180, 250, 300]),
    "uh": ([520, 1190, 2390, 3300, 3750], [80, 110, 160, 250, 300]),
    "aw": ([640, 920, 2400, 3300, 3750], [90, 100, 160, 250, 300]),
    "ng": ([250, 1200, 2300, 3300, 3750], [50, 200, 250, 300, 350]),
}
FORMANT_GAINS = [1.0, 0.7, 0.32, 0.16, 0.08]


def _trajectory(points, n):
    """points: list of (time_fraction, value) -> per-sample array."""
    xs = np.array([p[0] for p in points]) * (n - 1)
    ys = np.array([p[1] for p in points], dtype=float)
    return np.interp(np.arange(n), xs, ys)


def glottal(f0, seconds, jitter=0.01, shimmer=0.05, subharmonic=0.0, open_quotient=0.6, rng=None):
    """Rosenberg-style pulse train from an f0 contour (array or scalar), with period-level perturbations."""
    rng = rng or dsp.RNG
    n = dsp.samples(seconds)
    f0 = np.broadcast_to(np.asarray(f0, dtype=float), (n,))
    out = np.zeros(n)
    pos = 0.0
    k = 0
    while pos < n:
        i = int(pos)
        f = max(20.0, f0[min(i, n - 1)] * (1 + rng.normal(0, jitter)))
        period = SR / f
        amp = max(0.0, 1 + rng.normal(0, shimmer))
        if subharmonic and k % 2:
            amp *= 1 - subharmonic
        length = int(period)
        tp = max(2, int(length * open_quotient * 0.66))
        tn = max(2, int(length * open_quotient * 0.34))
        pulse = np.concatenate([0.5 * (1 - np.cos(np.pi * np.arange(tp) / tp)), np.cos(0.5 * np.pi * np.arange(tn) / tn)])
        end = min(n, i + len(pulse))
        out[i:end] += pulse[: end - i] * amp
        pos += period * (1 + (subharmonic * 0.5 if subharmonic and k % 2 else 0))
        k += 1
    # Differentiate (lip radiation) and remove DC.
    out = np.diff(out, prepend=0)
    return out / (np.abs(out).max() + 1e-9)


def formant_filter(source, vowel_path, scale=1.0, block=256, bandwidth_scale=1.0):
    """Parallel formant bank whose centres glide along vowel_path [(t_frac, vowel), ...]; scale <1 = bigger creature."""
    n = len(source)
    xs = np.array([p[0] for p in vowel_path]) * (n - 1)
    centres = np.array([VOWELS[v][0] for _, v in vowel_path], dtype=float) * scale
    widths = np.array([VOWELS[v][1] for _, v in vowel_path], dtype=float) * bandwidth_scale
    out = np.zeros(n)
    for k in range(5):
        fc = np.interp(np.arange(n), xs, centres[:, k])
        bw = np.interp(np.arange(n), xs, widths[:, k])
        zi = np.zeros(2)
        y = np.zeros(n)
        for start in range(0, n, block):
            end = min(n, start + block)
            f = float(np.clip(fc[start], 60, SR * 0.45))
            q = f / max(20.0, float(bw[start]))
            w0 = 2 * np.pi * f / SR
            alpha = np.sin(w0) / (2 * q)
            b = np.array([alpha, 0, -alpha]) / (1 + alpha)
            a = np.array([1, -2 * np.cos(w0) / (1 + alpha), (1 - alpha) / (1 + alpha)])
            y[start:end], zi = signal.lfilter(b, a, source[start:end], zi=zi)
        out += y * FORMANT_GAINS[k]
    return out


def creature(seconds, f0_points, vowels, *, scale=1.0, jitter=0.02, shimmer=0.08, subharmonic=0.0,
             breath=0.2, roughness=0.0, rough_rate=40.0, drive=1.5, attack=0.03, release=0.25,
             vibrato=(0.0, 0.0), rng=None):
    """General creature/voice generator.

    f0_points: [(t_frac, hz)], vowels: [(t_frac, vowel)], roughness: amplitude modulation depth (growl),
    breath: aspiration noise level, drive: saturation.
    """
    rng = rng or dsp.RNG
    n = dsp.samples(seconds)
    f0 = _trajectory(f0_points, n)
    if vibrato[0] > 0:
        t = np.arange(n) / SR
        f0 = f0 * (1 + vibrato[1] * np.sin(2 * np.pi * vibrato[0] * t))
    # Slow random drift keeps sustained calls from sounding mechanical.
    drift = signal.sosfilt(signal.butter(1, 3 / (SR / 2), output="sos"), rng.standard_normal(n)) * 0.6
    f0 = f0 * (1 + drift * 0.04)
    voiced = glottal(f0, seconds, jitter, shimmer, subharmonic, rng=rng)
    asp = dsp.highpass(rng.standard_normal(n), 400) * 0.5
    asp *= 0.5 + 0.5 * np.abs(voiced) / (np.abs(voiced).max() + 1e-9)
    src = voiced * (1 - breath) + asp * breath
    if roughness > 0:
        t = np.arange(n) / SR
        rate = rough_rate * (1 + 0.15 * np.sin(2 * np.pi * 2.3 * t))
        src *= 1 - roughness * (0.5 + 0.5 * np.sin(2 * np.pi * np.cumsum(rate) / SR))
    y = formant_filter(src, vowels, scale)
    y = dsp.saturate(y / (np.abs(y).max() + 1e-9), drive)
    env = dsp.env_adsr(n, attack, 0.0, 1.0, release)
    y = y * env
    return dsp.highpass(y, 40 * scale, 2)


CONSONANTS = {"s": (4500, 10000, 0.07), "sh": (2200, 6000, 0.08), "f": (1500, 9000, 0.05), "h": (500, 3500, 0.05),
              "t": (2500, 9000, 0.015), "k": (1200, 4000, 0.02), "th": (3000, 8000, 0.05)}


def whisper(seconds, vowels=None, scale=1.0, rng=None, rate=4.5):
    """Whispered speech-like syllables: a fricative or plosive, then a breathy formant vowel."""
    rng = rng or dsp.RNG
    n = dsp.samples(seconds)
    out = np.zeros(n + dsp.samples(0.4))
    t = 0.0
    vowel_names = ["a", "e", "i", "o", "u", "uh", "aw"]
    while t < seconds - 0.1:
        con = CONSONANTS[rng.choice(["s", "s", "sh", "sh", "f", "h", "t", "k", "th"])]
        c_len = con[2] * rng.uniform(0.8, 1.3)
        c = dsp.bandpass(rng.standard_normal(dsp.samples(c_len)), con[0], con[1], 2)
        c *= np.hanning(len(c)) * rng.uniform(0.4, 0.8)
        v_len = rng.uniform(0.08, 0.2)
        a, b = rng.choice(vowel_names), rng.choice(vowel_names)
        v = formant_filter(rng.standard_normal(dsp.samples(v_len)), [(0, a), (1, b)], scale * rng.uniform(0.95, 1.1),
                           bandwidth_scale=1.6)
        # Whispered vowels carry little F1 energy; most of the identity is breath in the 1.5-5 kHz formants.
        v = dsp.eq(dsp.highpass(v, 700, 2), ("peak", 2800, 0.8, 5))
        v = v / (np.abs(v).max() + 1e-9) * np.hanning(len(v)) * rng.uniform(0.5, 0.9)
        dsp.place(out, c, t)
        dsp.place(out, v, t + c_len * 0.8)
        t += c_len + v_len + rng.uniform(0.02, 1 / rate)
    out = out[:n]
    return dsp.highpass(out, 300, 2) / (np.abs(out).max() + 1e-9) * dsp.env_adsr(n, 0.05, 0, 1, 0.2)


def crowd_shout(seconds, voices=14, base_f0=150, vowel="a", spread=0.35, rng=None):
    """Many detuned human voices shouting together (war cries): moderate grit, staggered entries."""
    rng = rng or dsp.RNG
    out = np.zeros((dsp.samples(seconds) + dsp.samples(0.2), 2))
    for _ in range(voices):
        f = base_f0 * rng.uniform(0.8, 1.35)
        delay = rng.uniform(0, 0.18)
        dur = seconds * rng.uniform(0.7, 1.0)
        v = creature(dur, [(0, f * 0.85), (0.12, f * 1.18), (0.6, f * 1.05), (1, f * 0.75)],
                     [(0, "uh"), (0.12, vowel), (0.8, vowel), (1, "o")], scale=rng.uniform(0.95, 1.1),
                     jitter=0.015, shimmer=0.08, breath=0.28, roughness=0.12, rough_rate=60, drive=1.3, attack=0.05,
                     release=0.25, rng=rng)
        dsp.place(out, dsp.pan(v, rng.uniform(-spread, spread) * 2), delay, rng.uniform(0.6, 1.0))
    return out
