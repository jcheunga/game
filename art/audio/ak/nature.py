"""Environmental textures: wind, water, fire, birds, insects and other living ambience. Stereo float arrays."""
import numpy as np
from scipy import signal

from . import dsp, instruments
from . import foley as F
from . import voice as V
from .dsp import SR


def slow_lfo(n, rate, rng, lo=0.0, hi=1.0):
    """Smooth random modulation in [lo, hi] (filtered noise, not periodic)."""
    x = signal.sosfilt(signal.butter(2, max(rate, 0.01) / (SR / 2), output="sos"), rng.standard_normal(n + SR))[SR:]
    x = (x - x.min()) / (x.max() - x.min() + 1e-9)
    return lo + (hi - lo) * x


def wind(seconds, rng, strength=1.0, gust=0.6, low=180, high=1400, whistle=0.0):
    """Stereo wind: decorrelated noise through moving band-pass filters, with gusts."""
    n = dsp.samples(seconds)
    out = np.zeros((n, 2))
    level = slow_lfo(n, 0.25, rng, 1 - gust, 1.0)
    for ch in range(2):
        src = dsp.noise(seconds, "pink", rng)
        centre = slow_lfo(n, 0.3, rng, low * 1.5, high)
        body = dsp.sweep_filter(src, centre, q=0.6, kind="bandpass")
        rumble = dsp.lowpass(dsp.noise(seconds, "brown", rng), low, 2) * 0.6
        out[:, ch] = (body + rumble) * level * strength
        if whistle:
            w_centre = slow_lfo(n, 0.2, rng, 900, 2200)
            out[:, ch] += dsp.sweep_filter(src, w_centre, q=12, kind="bandpass") * level ** 2 * whistle
    return out


def leaves(seconds, rng, amount=1.0):
    """Rustling leaves / grass: dense soft crackle following the wind."""
    n = dsp.samples(seconds)
    out = np.zeros((n, 2))
    level = slow_lfo(n, 0.4, rng, 0.2, 1.0)
    for ch in range(2):
        x = dsp.bandpass(rng.standard_normal(n), 1800, 9000, 2)
        am = np.abs(signal.sosfilt(signal.butter(1, 40 / (SR / 2), output="sos"), rng.standard_normal(n))) * 6
        out[:, ch] = x * np.clip(am, 0, 1.5) * level * 0.25 * amount
    return out


def surf(seconds, rng, calm=False):
    """Waves on a shore: recorded ocean-drum swells layered with breaking-noise swells."""
    n = dsp.samples(seconds)
    out = np.zeros((n + dsp.samples(30), 2))
    t = 0.0
    while t < seconds:
        swell = instruments.get("ocean_drum").note(60, rng.uniform(0.5, 0.9), 12, rng=rng)
        swell = dsp.lowpass(swell, 3500 if calm else 6000, 2)
        dsp.place(out, dsp.pan(dsp.to_mono(swell), rng.uniform(-0.6, 0.6)), t, rng.uniform(0.5, 0.9))
        t += rng.uniform(5, 9)
    breaker = np.zeros((n, 2))
    for ch in range(2):
        noise = dsp.noise(seconds, "pink", rng)
        env = slow_lfo(n, 0.12, rng, 0.1, 1.0) ** 2
        breaker[:, ch] = dsp.lowpass(noise, 2500 if calm else 4500, 2) * env * 0.4
    return out[:n] + breaker


def lapping(seconds, rng, density=2.0):
    """Small water laps against wood/stone."""
    out = np.zeros((dsp.samples(seconds + 1), 2))
    t = 0.0
    while t < seconds:
        lap = dsp.lowpass(F.splat(rng.uniform(0.3, 0.6), rng, 0.5), 1800, 2)
        dsp.place(out, dsp.pan(lap, rng.uniform(-0.7, 0.7)), t, rng.uniform(0.15, 0.4))
        t += rng.exponential(1 / density)
    return out[: dsp.samples(seconds)]


def fire_bed(seconds, rng, size=1.0):
    """Brazier / hearth: low roar plus pops and ticks."""
    n = dsp.samples(seconds)
    out = np.zeros((n, 2))
    for ch in range(2):
        roar = dsp.lowpass(dsp.noise(seconds, "brown", rng), 350 * size, 2) * slow_lfo(n, 1.5, rng, 0.6, 1.0)
        out[:, ch] = roar * 0.8 + F.crackle(seconds, 18 * size, rng) * 0.35
    return out


def furnace(seconds, rng):
    n = dsp.samples(seconds)
    out = np.zeros((n, 2))
    for ch in range(2):
        roar = dsp.bandpass(dsp.noise(seconds, "brown", rng), 40, 600, 2) * slow_lfo(n, 0.5, rng, 0.6, 1.0) * 2.0
        hiss = dsp.bandpass(dsp.noise(seconds, "white", rng), 1500, 5000, 2) * slow_lfo(n, 0.8, rng, 0.0, 0.25)
        out[:, ch] = roar + hiss + F.crackle(seconds, 25, rng) * 0.3
    return out


def bellows(rng, seconds=1.6):
    n = dsp.samples(seconds)
    x = dsp.bandpass(rng.standard_normal(n), 120, 1200, 2) * np.sin(np.linspace(0, np.pi, n)) ** 2
    return dsp.to_stereo(x * 0.8)


def chirp(rng, f0, f1, dur, vib=0.0):
    """One syrinx note: a gliding tone with its octave, a little breath and amplitude flutter (not a pure beep)."""
    n = dsp.samples(dur)
    t = np.arange(n) / SR
    f = f0 * (f1 / f0) ** (t / dur)
    if vib:
        f = f * (1 + vib * np.sin(2 * np.pi * rng.uniform(25, 45) * t))
    phase = 2 * np.pi * np.cumsum(f) / SR
    tone = np.sin(phase) + 0.25 * np.sin(2 * phase + 0.5) + 0.08 * np.sin(3 * phase)
    flutter = 1 + 0.25 * np.sin(2 * np.pi * rng.uniform(60, 120) * t)
    breath = dsp.bandpass(rng.standard_normal(n), max(500, f0 * 0.7), min(SR * 0.45, f0 * 1.6), 2) * 0.35
    return (tone * flutter + breath) * np.sin(np.pi * np.arange(n) / n) ** 1.5


def glide_sample(x, r0, r1):
    """Resample a mono buffer with a playback rate gliding from r0 to r1 (fast pitch sweeps)."""
    n_in = len(x)
    rates = np.linspace(r0, r1, int(n_in / ((r0 + r1) / 2)))
    pos = np.cumsum(rates)
    pos = pos[pos < n_in - 1]
    return np.interp(pos, np.arange(n_in), x)


def whistle_note(rng, rate0, rate1, seconds):
    """A real breathy whistle (soprano recorder staccato) sped up 1-2 octaves with a glide - a bird's note."""
    src = dsp.to_mono(instruments.get("recorder_soprano_stac").note(int(rng.integers(84, 92)), rng.uniform(0.6, 0.9), 0.2,
                                                                    rng=rng))
    seg = src[: dsp.samples(seconds * (rate0 + rate1) / 2)]
    y = glide_sample(seg, rate0, rate1)
    return y * np.sin(np.pi * np.arange(len(y)) / max(1, len(y))) ** 0.8


def songbird_recorded(rng, seconds=1.2):
    out = np.zeros(dsp.samples(seconds + 0.4))
    t = 0.0
    style = rng.integers(3)
    while t < seconds:
        d = rng.uniform(0.04, 0.11)
        if style == 0:
            note_ = whistle_note(rng, rng.uniform(2.0, 2.6), rng.uniform(3.0, 4.0), d)
        elif style == 1:
            note_ = whistle_note(rng, rng.uniform(3.4, 4.0), rng.uniform(2.2, 2.8), d)
        else:
            note_ = whistle_note(rng, rng.uniform(2.6, 3.0), rng.uniform(2.6, 3.2), d * 0.6)
        dsp.place(out, note_, t, rng.uniform(0.5, 1.0))
        t += rng.uniform(0.06, 0.15)
    return dsp.highpass(out, 1500, 2)


def songbird(rng, seconds=1.2):
    if rng.random() < 0.65:
        return songbird_recorded(rng, seconds)
    return songbird_synth(rng, seconds)


def songbird_synth(rng, seconds=1.2):
    """A small songbird phrase: trills and sweeping chirps."""
    out = np.zeros(dsp.samples(seconds + 0.3))
    t = 0.0
    base = rng.uniform(2800, 4200)
    style = rng.integers(3)
    while t < seconds:
        if style == 0:
            c = chirp(rng, base * rng.uniform(0.9, 1.3), base * rng.uniform(1.2, 1.8), rng.uniform(0.04, 0.09))
        elif style == 1:
            c = chirp(rng, base * 1.5, base * 0.8, rng.uniform(0.06, 0.12), vib=0.04)
        else:
            c = chirp(rng, base, base * 1.05, rng.uniform(0.02, 0.04))
        dsp.place(out, c, t, rng.uniform(0.4, 1.0))
        t += rng.uniform(0.05, 0.16)
    return dsp.highpass(out, 1500, 2)


def crow(rng, count=None):
    out = np.zeros(dsp.samples(1.8))
    for j in range(count or int(rng.integers(1, 4))):
        f = rng.uniform(420, 560)
        caw = V.creature(rng.uniform(0.22, 0.32), [(0, f * 0.9), (0.3, f * 1.1), (1, f * 0.75)], [(0, "a"), (1, "aw")],
                         scale=1.6, jitter=0.08, shimmer=0.3, subharmonic=0.3, breath=0.5, roughness=0.8, rough_rate=70,
                         drive=3.0, attack=0.01, release=0.06, rng=rng)
        dsp.place(out, caw, j * rng.uniform(0.38, 0.5))
    return dsp.highpass(out, 400, 2)


def gull(rng):
    f = rng.uniform(1300, 1600)
    cry = V.creature(rng.uniform(0.45, 0.65), [(0, f * 0.7), (0.25, f * 1.2), (1, f * 0.6)], [(0, "i"), (0.4, "e"), (1, "aw")],
                     scale=1.9, jitter=0.02, shimmer=0.1, breath=0.4, roughness=0.3, rough_rate=90, drive=2.0, attack=0.02,
                     release=0.15, rng=rng)
    out = np.zeros(dsp.samples(1.4))
    dsp.place(out, cry, 0)
    if rng.random() < 0.6:
        dsp.place(out, cry * 0.7, rng.uniform(0.5, 0.7))
    return dsp.highpass(out, 500, 2)


def raptor(rng):
    """Hawk / eagle scream: a high breathy descending cry."""
    f = rng.uniform(2200, 2800)
    return dsp.highpass(V.creature(rng.uniform(0.8, 1.1), [(0, f * 1.1), (0.15, f * 1.25), (1, f * 0.7)],
                                   [(0, "i"), (1, "e")], scale=2.2, jitter=0.02, shimmer=0.1, breath=0.6, roughness=0.2,
                                   drive=1.6, attack=0.03, release=0.4, rng=rng), 800, 2)


def crickets(seconds, rng, count=6, level=1.0):
    n = dsp.samples(seconds)
    out = np.zeros((n, 2))
    for _ in range(count):
        f = rng.uniform(3800, 5200)
        rate = rng.uniform(1.5, 3.0)
        pulses = rng.integers(3, 6)
        p = rng.uniform(-0.8, 0.8)
        voice_ = np.zeros(n)
        t = rng.uniform(0, 1)
        while t < seconds:
            for j in range(pulses):
                # A wing stroke: a scrape (noise burst) ringing a resonant wing membrane.
                c = F.resonant_burst([f, f * 2.02], [35, 25], [1, 0.25], 0.025, 0.006, rng)
                dsp.place(voice_, c * rng.uniform(0.7, 1.0), t + j * rng.uniform(0.026, 0.031))
            t += 1 / rate * rng.uniform(0.9, 1.1)
        out += dsp.pan(voice_ * rng.uniform(0.3, 1.0), p)
    return out * level * 0.3


def frog(rng):
    """A croak: a buzzy pulse train through throat resonances."""
    dur = rng.uniform(0.18, 0.35)
    n = dsp.samples(dur)
    rate = rng.uniform(25, 45)
    pulses = (np.diff(np.floor(np.cumsum(np.full(n, rate)) / SR), prepend=0) > 0).astype(float)
    body = sum(dsp.resonator(pulses, f, 8) * g for f, g in ((rng.uniform(450, 650), 1.0), (rng.uniform(1100, 1500), 0.5)))
    return body * np.sin(np.pi * np.arange(n) / n) ** 0.7


def owl(rng):
    """Tawny-owl style hoots: a breathy, hollow 'hoo' voice rather than a sine."""
    out = np.zeros(dsp.samples(2.2))
    f = rng.uniform(360, 430)
    for j, (d, g) in enumerate(((0.38, 1.0), (0.18, 0.7), (0.55, 0.9))):
        hoot = V.creature(d, [(0, f * 1.02), (1, f * 0.94)], [(0, "u"), (1, "u")], scale=1.4, jitter=0.01, shimmer=0.05,
                          breath=0.45, drive=1.1, attack=0.05, release=0.12, vibrato=(7, 0.01) if j == 2 else (0, 0), rng=rng)
        dsp.place(out, dsp.lowpass(hoot, 1800, 2) * g, [0, 0.55, 0.85][j])
    return out


def flies(seconds, rng, count=2):
    n = dsp.samples(seconds)
    out = np.zeros((n, 2))
    for _ in range(count):
        f = slow_lfo(n, 2.0, rng, 170, 260)
        buzz = dsp.osc(f, seconds, "saw") * slow_lfo(n, 0.8, rng, 0.0, 1.0) ** 3
        buzz = dsp.bandpass(buzz, 200, 4000, 1)
        out += dsp.pan(buzz * 0.15, rng.uniform(-0.8, 0.8))
    return out


def drip(rng):
    f0 = rng.uniform(900, 1800)
    n = dsp.samples(0.12)
    t = np.arange(n) / SR
    plink = np.sin(2 * np.pi * np.cumsum(f0 * (1 + 3 * t)) / SR) * np.exp(-t / 0.025)
    return plink * 0.6


def bell_far(rng, midi=62, space_name="cathedral"):
    from . import reverb
    b = instruments.get("tubular_bells").note(midi, rng.uniform(0.5, 0.8), 1.0, rng=rng, release=3.0)
    b = dsp.lowpass(dsp.to_stereo(b), 3500, 2)
    return reverb.reverb(b, space_name, wet=0.6, dry=0.5)


def distant(x, cutoff=1500, space_name="open", wet=0.5):
    from . import reverb
    return reverb.reverb(dsp.lowpass(dsp.to_stereo(x), cutoff, 2), space_name, wet=wet, dry=0.6)


def loop_bed(x, xfade=3.0):
    """Make a seamless loop: crossfade the extra tail back over the start (x must be longer than the loop by xfade)."""
    n = len(x) - dsp.samples(xfade)
    m = dsp.samples(xfade)
    out = x[:n].copy()
    ramp = np.sin(np.linspace(0, np.pi / 2, m))[:, None]
    out[:m] = out[:m] * ramp + x[n:n + m] * np.cos(np.linspace(0, np.pi / 2, m))[:, None]
    return out
