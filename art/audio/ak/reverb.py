"""Convolution reverb with synthesized, rights-free impulse responses."""
from functools import lru_cache

import numpy as np
from scipy import signal

from . import dsp
from .dsp import SR

# rt60 (s), predelay (s), high-frequency damping (rt60 multiplier at 8 kHz), low multiplier, early reflection spread (s), density
SPACES = {
    "room":      dict(rt60=0.55, predelay=0.006, hf=0.45, lf=1.0, early=0.025, er_count=10, color=0.0),
    "chamber":   dict(rt60=1.2, predelay=0.012, hf=0.5, lf=1.05, early=0.04, er_count=14, color=0.0),
    "hall":      dict(rt60=2.3, predelay=0.022, hf=0.42, lf=1.15, early=0.06, er_count=18, color=0.0),
    "great_hall": dict(rt60=3.2, predelay=0.03, hf=0.38, lf=1.2, early=0.08, er_count=20, color=0.0),
    "cathedral": dict(rt60=5.5, predelay=0.045, hf=0.33, lf=1.25, early=0.11, er_count=24, color=0.0),
    "cave":      dict(rt60=2.8, predelay=0.03, hf=0.25, lf=1.3, early=0.07, er_count=16, color=-0.3),
    "forest":    dict(rt60=0.9, predelay=0.015, hf=0.35, lf=0.8, early=0.09, er_count=26, color=-0.2),
    "open":      dict(rt60=0.45, predelay=0.03, hf=0.4, lf=0.7, early=0.12, er_count=6, color=-0.1),
    "plate":     dict(rt60=1.8, predelay=0.0, hf=0.75, lf=0.9, early=0.0, er_count=0, color=0.2),
}

_BANDS = [(20, 180), (180, 500), (500, 1400), (1400, 4000), (4000, 9000), (9000, 20000)]


@lru_cache(maxsize=16)
def impulse(space, seed=7):
    p = SPACES[space]
    rng = np.random.default_rng(seed)
    length = p["rt60"] * 1.3 + p["predelay"] + 0.05
    n = dsp.samples(length)
    t = np.arange(n) / SR
    ir = np.zeros((n, 2))
    # Diffuse tail: decorrelated noise per channel, each band decaying at its own rate.
    for ch in range(2):
        white = rng.standard_normal(n)
        for lo, hi in _BANDS:
            centre = np.sqrt(lo * hi)
            # Interpolate the decay multiplier in log-frequency between low (100 Hz) and high (8 kHz).
            u = np.clip((np.log2(centre) - np.log2(100)) / (np.log2(8000) - np.log2(100)), 0, 1)
            mult = p["lf"] * (1 - u) + p["hf"] * u if centre > 100 else p["lf"]
            rt = max(0.05, p["rt60"] * mult)
            band = signal.sosfilt(signal.butter(2, [lo / (SR / 2), min(hi, SR / 2 * 0.98) / (SR / 2)], btype="band",
                                                output="sos"), white)
            tilt = 10 ** (p["color"] * (u - 0.5))
            ir[:, ch] += band * np.exp(-6.9 * t / rt) * tilt
    # Soft onset of the diffuse field after the predelay and early reflections.
    onset = dsp.samples(p["predelay"])
    build = dsp.samples(max(0.008, p["early"] * 0.7))
    ramp = np.zeros(n)
    ramp[onset:onset + build] = np.linspace(0, 1, min(build, n - onset)) ** 1.5
    ramp[onset + build:] = 1
    ir *= ramp[:, None]
    # Early reflections: sparse, filtered taps, slightly different per ear.
    for k in range(p["er_count"]):
        for ch in range(2):
            when = p["predelay"] + rng.uniform(0.002, max(0.003, p["early"]))
            gain = rng.uniform(0.25, 0.7) * (1 - when / (p["predelay"] + p["early"] + 0.01)) * 2.5
            idx = dsp.samples(when)
            if idx < n:
                ir[idx, ch] += gain * rng.choice([-1, 1])
    ir = signal.sosfilt(signal.butter(1, 30 / (SR / 2), btype="high", output="sos"), ir, axis=0)
    ir /= np.sqrt(np.sum(ir ** 2) / 2)
    return ir


def reverb(x, space="hall", wet=0.25, dry=1.0, tail=True, lowcut=180, highcut=None, seed=7):
    """Return x with a convolution reverb send mixed in. With tail, the output is lengthened by the reverb."""
    data = dsp.to_stereo(x)
    ir = impulse(space, seed)
    send = data
    if lowcut:
        send = dsp.highpass(send, lowcut, 2)
    if highcut:
        send = dsp.lowpass(send, highcut, 2)
    # Cross-feed so a hard-panned source still fills both sides of the room.
    send = np.column_stack([send[:, 0] * 0.8 + send[:, 1] * 0.2, send[:, 1] * 0.8 + send[:, 0] * 0.2])
    wet_l = signal.oaconvolve(send[:, 0], ir[:, 0])
    wet_r = signal.oaconvolve(send[:, 1], ir[:, 1])
    wet_sig = np.column_stack([wet_l, wet_r]) * wet
    n = len(wet_sig) if tail else len(data)
    out = np.zeros((n, 2))
    out[: len(data)] += data * dry
    out += wet_sig[:n]
    return out


def reverb_wrapped(x, space="hall", wet=0.25, dry=1.0, lowcut=180, highcut=None):
    """Loop-safe reverb: the tail past the end folds back onto the start."""
    n = len(x)
    full = reverb(x, space, wet, dry, True, lowcut, highcut)
    out = full[:n].copy()
    over = full[n:]
    k = 0
    while k < len(over):
        seg = over[k:k + n]
        out[: len(seg)] += seg
        k += n
    return out
