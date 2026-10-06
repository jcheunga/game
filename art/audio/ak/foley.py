"""Foley and sound-design building blocks. All functions return mono float arrays at dsp.SR unless noted."""
import numpy as np
from scipy import signal

from . import dsp, instruments
from .dsp import SR


def _rng(rng):
    return rng if rng is not None else dsp.RNG


def _n(seconds):
    return max(1, dsp.samples(seconds))


def shaped_noise(seconds, lo=None, hi=None, color="white", rng=None):
    x = dsp.noise(seconds, color, _rng(rng))
    if lo and hi:
        x = dsp.bandpass(x, lo, hi, 2)
    elif lo:
        x = dsp.highpass(x, lo, 2)
    elif hi:
        x = dsp.lowpass(x, hi, 2)
    return x


def env_ar(n, attack, release, curve=2.0):
    """Asymmetric swell: rises over `attack` fraction then falls (both as fractions of n)."""
    t = np.linspace(0, 1, n)
    a = np.clip(t / max(attack, 1e-4), 0, 1) ** curve
    r = np.clip((1 - t) / max(release, 1e-4), 0, 1) ** curve
    return np.minimum(a, r)


def sample(name, velocity=0.8, detune=0.0, seconds=None, rng=None, midi=60):
    """One recorded hit from the instrument registry as mono, optionally pitch-shifted and truncated."""
    inst = instruments.get(name)
    x = inst.note(midi, velocity, seconds or 1.0, rng=_rng(rng), detune=detune)
    x = dsp.to_mono(x)
    if seconds:
        n = _n(seconds)
        x = x[:n]
        if len(x) > 64:
            x = dsp.fade(x, 0, min(seconds * 0.4, 0.15))
    return x


def resonant_burst(freqs, qs, gains, seconds=0.2, excite=0.004, rng=None, color="white"):
    """Excite parallel resonators with a short noise burst - natural 'tok', 'tink' and 'clack' sounds."""
    rng = _rng(rng)
    n = _n(seconds)
    exc = np.zeros(n)
    m = _n(excite)
    burst = dsp.noise(excite, color, rng)[:m] * np.hanning(m * 2)[m:]
    exc[:m] = burst
    out = np.zeros(n)
    for f, q, g in zip(freqs, qs, gains):
        if f < SR * 0.45:
            out += dsp.resonator(exc, f, q) * g
    return out / (np.abs(out).max() + 1e-9)


def grains(seconds, count, make, rng=None, curve=1.4, gain_range=(0.3, 1.0)):
    """Scatter `make(rng)` grains across `seconds` (front-loaded by `curve`)."""
    rng = _rng(rng)
    n = _n(seconds)
    out = np.zeros(n + _n(0.3))
    for _ in range(count):
        g = make(rng)
        dsp.place(out, g, rng.uniform(0, 1) ** curve * seconds, rng.uniform(*gain_range))
    return out


# ---------------------------------------------------------------- air & motion
def whoosh(seconds=0.35, f_start=500, f_end=2600, q=1.1, peak=0.45, color="white", rng=None):
    """Swept band-passed noise with an asymmetric swell (swings, throws, passes)."""
    x = shaped_noise(seconds, color=color, rng=rng)
    n = len(x)
    t = np.arange(n) / SR
    freqs = f_start * (f_end / f_start) ** (t / max(seconds, 1e-3))
    y = dsp.sweep_filter(x, freqs, q=q, kind="bandpass")
    return y * env_ar(n, peak, 1 - peak, 1.6)


def blade_swish(seconds=0.28, rng=None, ring=0.15):
    rng = _rng(rng)
    w = whoosh(seconds, rng.uniform(700, 1100), rng.uniform(2800, 4200), q=1.6, peak=0.55, rng=rng)
    # A faint singing edge: a thin high partial riding the swing.
    n = len(w)
    t = np.arange(n) / SR
    edge = np.sin(2 * np.pi * rng.uniform(3200, 4800) * t) * env_ar(n, 0.6, 0.4, 2) * ring * 0.15
    return w + edge


def heavy_swish(seconds=0.42, rng=None):
    rng = _rng(rng)
    w = whoosh(seconds, rng.uniform(220, 320), rng.uniform(900, 1400), q=0.9, peak=0.6, color="pink", rng=rng)
    return w * 1.4


def cloth_flap(seconds=0.5, rate=11, rng=None):
    """Banner / cloak flutter: recorded cloth movement plus irregular soft snaps of heavy cloth."""
    rng = _rng(rng)
    base = sample("k_cloth", 0.8, detune=rng.uniform(-4, -1), rng=rng, seconds=seconds)

    def snap(r):
        d = r.uniform(0.03, 0.07)
        x = dsp.bandpass(r.standard_normal(_n(d)), 150, 2200, 1) * env_ar(_n(d), 0.15, 0.85, 2)
        return x

    count = max(2, int(seconds * rate))
    out = np.zeros(_n(seconds + 0.1))
    t = 0.0
    for _ in range(count):
        dsp.place(out, snap(rng), t, rng.uniform(0.4, 1.0))
        t += rng.uniform(0.6, 1.4) / rate
    out = out * env_ar(len(out), 0.15, 0.6, 1.0) * 0.6
    return dsp.mix(base, (out, 0)).mean(axis=1)


# ---------------------------------------------------------------- impacts
def thump(freq=70, seconds=0.3, drop=0.5, click=0.25, rng=None):
    """Low body thump: pitch-dropping sine + a short noise click."""
    n = _n(seconds)
    t = np.arange(n) / SR
    f = freq * (1 + drop * np.exp(-t * 40))
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / (seconds * 0.28))
    c = shaped_noise(0.012, 800, 6000, rng=rng)
    c = np.pad(c * np.exp(-np.arange(len(c)) / (SR * 0.003)), (0, n - len(c)))[:n]
    return body + c * click


def crunch(seconds=0.12, density=300, lo=1200, hi=6000, rng=None, decay=None):
    """Granular cracks (bone, splinters, gravel): short band-limited noise grains, not tones."""
    rng = _rng(rng)
    n = _n(seconds)
    out = np.zeros(n)
    count = int(density * seconds)
    base = dsp.bandpass(rng.standard_normal(n + 400), lo, hi, 2)
    for _ in range(count):
        pos = int(rng.uniform(0, 1) ** 1.8 * n)
        length = int(rng.uniform(0.0008, 0.005) * SR)
        g = rng.uniform(0.2, 1.0) ** 1.5
        src = int(rng.uniform(0, len(base) - length - 1))
        grain = base[src:src + length] * np.exp(-np.arange(length) / (length * 0.3)) * g
        end = min(n, pos + length)
        out[pos:end] += grain[: end - pos]
    if decay:
        out *= np.exp(-np.arange(n) / (SR * decay))
    return out


def metal_ring(f0=900, seconds=0.6, ratios=(1, 2.76, 5.40, 8.93, 13.34), decays=None, gains=None, rng=None, jitter=0.02):
    decays = decays or [seconds * d for d in (1.0, 0.7, 0.5, 0.35, 0.25)]
    gains = gains or [1, 0.6, 0.45, 0.3, 0.2]
    return dsp.modal([f0 * r for r in ratios], gains, decays, seconds, jitter=jitter, rng=_rng(rng))


def metal_hit(size=1.0, seconds=None, rng=None, with_sample=True):
    """Struck metal: synthesized modes + transient (+ a recorded anvil/brake-drum layer)."""
    rng = _rng(rng)
    seconds = seconds or 0.35 + 0.5 * size
    f0 = rng.uniform(380, 620) / size
    ring = metal_ring(f0, seconds, ratios=(1, 1.58, 2.33, 3.17, 4.41), rng=rng, jitter=0.03) * 0.35
    upper = metal_ring(f0 * rng.uniform(2.9, 3.6), seconds * 0.6, rng=rng) * 0.25
    ring[: len(upper)] += upper
    trans = shaped_noise(0.03, 1500, 9000, rng=rng) * np.exp(-np.arange(_n(0.03)) / (SR * 0.006))
    y = dsp.mix(ring, (trans * 0.8, 0)).mean(axis=1)
    if with_sample:
        layer = sample("anvil", rng.uniform(0.5, 0.9), detune=rng.uniform(-5, 2) - 6 * (size - 1), seconds=seconds,
                       rng=rng)
        y = dsp.mix(y, (layer * 0.7, 0)).mean(axis=1)
    return y


def blade_clash(rng=None, seconds=0.7):
    """Steel on steel: a recorded triangle/cymbal 'shing', a grinding scrape and a dull body knock."""
    rng = _rng(rng)
    ring = sample("triangle", rng.uniform(0.5, 0.9), detune=rng.uniform(-9, -3), seconds=seconds, rng=rng)
    tick = sample("crash", rng.uniform(0.6, 0.9), detune=rng.uniform(4, 9), seconds=0.25, rng=rng)
    tick = dsp.highpass(tick, 2500, 2) * np.exp(-np.arange(len(tick)) / (SR * 0.05))
    m = resonant_burst([rng.uniform(1800, 2600) * r for r in (1, 1.41, 2.13, 2.92)], [60, 50, 40, 30], [1, 0.7, 0.5, 0.3],
                       seconds * 0.6, 0.002, rng)
    scrape = shaped_noise(0.14, 2500, 9000, rng=rng) * np.linspace(1, 0, _n(0.14)) ** 1.5
    knock = thump(rng.uniform(140, 200), 0.12, drop=0.3, click=0.5, rng=rng)
    return dsp.mix(ring * 0.9, (tick * 0.7, 0), (m * 0.35, 0), (scrape * 0.45, 0.004), (knock * 0.4, 0)).mean(axis=1)


def flesh_hit(weight=1.0, rng=None):
    """Body impact: a recorded punch, deepened with a sub thud for heavier blows."""
    rng = _rng(rng)
    punch = sample("k_punch_heavy" if weight > 1.1 else "k_punch", 0.85, detune=rng.uniform(-2, 1) - 3 * (weight - 1),
                   rng=rng, seconds=0.45)
    body = thump(rng.uniform(55, 80) / weight ** 0.3, 0.2 + 0.1 * weight, drop=0.6, click=0.0, rng=rng)
    return dsp.mix(punch, (body * 0.35 * weight, 0)).mean(axis=1)


def wood_hit(size=1.0, rng=None, seconds=0.3):
    rng = _rng(rng)
    name = "k_wood_heavy" if size > 1.3 else "k_wood_light" if size < 0.8 else "k_wood"
    real = sample(name, 0.8, detune=rng.uniform(-2, 2) - 4 * np.log2(max(size, 0.25)), rng=rng, seconds=seconds + 0.1)
    return real


def splinter(seconds=0.25, rng=None):
    """Wood breaking: dense cracks plus fibrous tearing noise."""
    rng = _rng(rng)
    cr = crunch(seconds, 900, 600, 4500, rng=rng, decay=seconds * 0.4)
    tear = shaped_noise(seconds, 400, 3000, rng=rng) * env_ar(_n(seconds), 0.05, 0.9, 1.5) * 0.4
    return cr + tear


def stone_hit(size=1.0, rng=None):
    rng = _rng(rng)
    rock = sample("k_mining", 0.8, detune=rng.uniform(-9, -5) - 3 * (size - 1), rng=rng, seconds=0.5 * size + 0.2)
    rock = dsp.lowpass(rock, 5000, 2)
    body = thump(rng.uniform(45, 65) / size ** 0.3, 0.35 * size, drop=0.3, click=0.3, rng=rng)
    grit = shaped_noise(0.25 * size, 300, 2500, color="pink", rng=rng) * np.exp(-np.arange(_n(0.25 * size)) / (SR * 0.06))
    debris = crunch(0.4 * size, 160, 500, 3000, rng=rng, decay=0.15 * size)
    return dsp.mix(rock * 0.9, (body * 0.8, 0), (grit * 0.4, 0), (debris * 0.3, 0.02)).mean(axis=1)


def bone_rattle(seconds=0.4, density=90, rng=None, pitch=1.0):
    """Clattering bones: recorded dice clatter pitched down to hollow bone, plus scattered claves/woodblock knocks."""
    rng = _rng(rng)
    clatter = sample(rng.choice(["k_dice_throw", "k_dice_shake"]), 0.8, detune=rng.uniform(-9, -4) + 12 * np.log2(pitch),
                     rng=rng, seconds=max(0.3, seconds))
    clatter = dsp.eq(clatter, ("peak", 900, 1.0, 4))

    def knock(r):
        if r.random() < 0.55:
            return sample(r.choice(["claves", "woodblock"]), r.uniform(0.3, 0.8), detune=r.uniform(-4, 7) + 12 * np.log2(pitch),
                          seconds=0.06, rng=r)
        f = r.uniform(1100, 2800) * pitch
        return resonant_burst([f, f * 1.73, f * 2.6], [18, 14, 10], [1, 0.5, 0.3], 0.04, 0.0015, r)

    knocks = grains(seconds, int(density * seconds * 0.4), knock, rng, curve=1.4, gain_range=(0.2, 0.7))
    return dsp.mix(clatter, (knocks, 0)).mean(axis=1)


def chain_jingle(seconds=0.35, density=120, rng=None, lo=3000, hi=8500):
    """Mail and buckles: recorded tambourine/sleigh-bell jingles shortened and scattered, plus tiny metal ticks."""
    rng = _rng(rng)

    def jingle(r):
        src = sample(r.choice(["tambourine", "sleigh_bells", "k_belt"]), r.uniform(0.2, 0.6), detune=r.uniform(-9, 0),
                     seconds=r.uniform(0.05, 0.12), rng=r)
        return dsp.lowpass(dsp.highpass(src, 1200, 2), 8000, 2)

    return grains(seconds, max(3, int(density * seconds * 0.3)), jingle, rng, curve=1.2, gain_range=(0.2, 0.8))


def armor_rattle(seconds=0.45, rng=None, weight=1.0):
    """Soldier's kit on the move: recorded belt/buckle handling, leather, mail jingle and a plate knock."""
    rng = _rng(rng)
    belt = sample(rng.choice(["k_belt", "k_cloth_belt"]), 0.8, detune=rng.uniform(-2, 1), rng=rng, seconds=seconds + 0.2)
    leather = sample("k_cloth", 0.6, detune=rng.uniform(-3, 0), rng=rng, seconds=seconds) * 0.6
    mail = chain_jingle(seconds, 120, rng) * 0.5
    plate = sample("k_plate_light", 0.6, detune=rng.uniform(-4, 0) - 3 * (weight - 1), rng=rng, seconds=0.3) * 0.5 * weight
    return dsp.mix(belt, (leather, 0.02), (mail, 0.01), (plate, rng.uniform(0.02, 0.12))).mean(axis=1)


STEP_SOURCES = {"dirt": "k_step_dirt", "grass": "k_step_grass", "stone": "k_step_concrete", "wood": "k_step_wood",
                "snow": "k_step_snow", "mud": "k_step_snow"}


def footstep(surface="dirt", weight=1.0, rng=None):
    """A real footstep recording, weighted heavier by pitching down and adding a low heel thud."""
    rng = _rng(rng)
    step = sample(STEP_SOURCES.get(surface, "k_step_dirt"), 0.8, detune=rng.uniform(-1.5, 1.5) - 3 * (weight - 1), rng=rng,
                  seconds=0.35)
    if weight > 1.2:
        heel = thump(rng.uniform(55, 75), 0.15, drop=0.2, click=0.0, rng=rng) * 0.4 * (weight - 1)
        step = dsp.mix(step, (heel, 0)).mean(axis=1)
    return step


def footsteps(count=3, interval=0.32, surface="dirt", weight=1.0, rng=None):
    rng = _rng(rng)
    out = np.zeros(_n(count * interval + 0.2))
    for k in range(count):
        dsp.place(out, footstep(surface, weight, rng), k * interval + rng.uniform(-0.02, 0.02), rng.uniform(0.7, 1.0))
    return out


def hoof(rng=None, weight=1.0):
    rng = _rng(rng)
    clop = wood_hit(0.8, rng, 0.12) * 0.6
    body = thump(rng.uniform(60, 85), 0.12, drop=0.4, click=0.2, rng=rng) * 0.8 * weight
    dirt = crunch(0.07, 500, 700, 4000, rng=rng, decay=0.03) * 0.3
    return dsp.mix(clop, (body, 0), (dirt, 0.005)).mean(axis=1)


def gallop(seconds=1.0, rate=2.6, rng=None):
    """Four-beat gallop: three quick hooves then a gap, repeating."""
    rng = _rng(rng)
    out = np.zeros(_n(seconds + 0.2))
    cycle = 1 / rate
    t = 0.0
    while t < seconds:
        for off in (0, 0.11, 0.22, 0.30):
            dsp.place(out, hoof(rng), t + off * cycle * 2.2, rng.uniform(0.6, 1.0))
        t += cycle
    return out


# ---------------------------------------------------------------- strings & mechanisms
def bow_release(rng=None, tension=1.0):
    """Bowstring 'thwip': a recorded contrabass pizzicato (real gut-string slap) shortened, with the arrow's hiss."""
    rng = _rng(rng)
    string = sample("basses_pizz", 1.0, seconds=0.2, rng=rng, midi=int(rng.integers(38, 44)) + int(4 * (tension - 1)))
    string = dsp.eq(string, ("peak", 200, 1.0, 4), ("highshelf", 2500, 0.7, 6))
    string *= np.exp(-np.arange(len(string)) / (SR * 0.045))
    nock = sample("claves", 0.5, detune=rng.uniform(-14, -10), seconds=0.04, rng=rng)
    arrow = whoosh(0.2, 2600, 5200, q=2.4, peak=0.12, rng=rng) * 0.25
    return dsp.mix(string * 1.3, (nock * 0.35, 0), (arrow, 0.015)).mean(axis=1)


def crossbow_release(rng=None):
    """Crossbow: a hard latch click, a heavy string snap and the stock's knock, then the bolt's hiss."""
    rng = _rng(rng)
    latch = sample("claves", rng.uniform(0.6, 0.9), detune=rng.uniform(2, 6), seconds=0.06, rng=rng) * 0.7
    string = sample("basses_pizz", 0.95, seconds=0.18, rng=rng, midi=int(rng.integers(33, 38)))
    string = dsp.eq(string, ("highshelf", 2500, 0.7, 6)) * np.exp(-np.arange(_n(0.18))[: len(string)] / (SR * 0.035))
    stock = sample("woodblock", 0.7, detune=rng.uniform(-10, -6), seconds=0.1, rng=rng) * 0.5
    bolt = whoosh(0.2, 2000, 4500, q=2.0, peak=0.15, rng=rng) * 0.45
    return dsp.mix(latch, (string, 0.008), (stock, 0.008), (bolt, 0.02)).mean(axis=1)


def ballista_release(rng=None):
    rng = _rng(rng)
    arm = thump(rng.uniform(45, 60), 0.4, drop=0.4, click=0.5, rng=rng) * 1.2
    rope = dsp.karplus(rng.uniform(48, 62), 0.6, brightness=0.5, decay=0.99, rng=rng) * np.exp(-np.arange(_n(0.6)) / (SR * 0.12))
    frame = wood_hit(1.6, rng, 0.35)
    creak = creak_sound(0.3, 1.2, rng) * 0.25
    bolt = whoosh(0.35, 400, 1500, q=1.0, peak=0.25, color="pink", rng=rng) * 0.6
    return dsp.mix(arm, (rope * 0.5, 0.005), (frame, 0), (creak, 0.05), (bolt, 0.02)).mean(axis=1)


def creak_sound(seconds=0.6, pitch=1.0, rng=None, recorded=True):
    """Timber/rope creak: a recorded creak (pitched), or stick-slip friction synthesis when recorded=False."""
    rng = _rng(rng)
    if recorded:
        return sample("k_creak", 0.8, detune=12 * np.log2(pitch) + rng.uniform(-1, 1), rng=rng, seconds=seconds)
    n = _n(seconds)
    rate = 40 * pitch * (1 + 0.6 * np.sin(np.linspace(0, rng.uniform(2, 5), n)) + rng.normal(0, 0.05, n).cumsum() / n * 5)
    rate = np.clip(rate, 10, 400)
    phase = np.cumsum(rate) / SR
    pulses = (np.diff(np.floor(phase), prepend=0) > 0).astype(float)
    pulses *= rng.uniform(0.5, 1.0, n)
    body = np.zeros(n)
    for f, q in ((420 * pitch, 8), (980 * pitch, 6), (1900 * pitch, 5), (3100 * pitch, 4)):
        body += dsp.resonator(pulses, f, q) * (1.0 if f < 1000 * pitch else 0.6)
    return body * env_ar(n, 0.15, 0.4, 1.0)


def ratchet(seconds=0.3, rng=None):
    rng = _rng(rng)
    return sample("ratchet", rng.uniform(0.5, 0.9), detune=rng.uniform(-2, 2), seconds=seconds, rng=rng)


# ---------------------------------------------------------------- elements
def crackle(seconds=1.0, density=40, rng=None):
    """Fire pops and ticks."""
    rng = _rng(rng)
    n = _n(seconds)
    out = np.zeros(n)
    for _ in range(int(density * seconds)):
        pos = int(rng.uniform(0, n))
        length = int(rng.uniform(0.001, 0.006) * SR)
        g = rng.uniform(0.1, 1.0) ** 2
        burst = rng.standard_normal(length) * np.exp(-np.arange(length) / (length * 0.25)) * g
        end = min(n, pos + length)
        out[pos:end] += burst[: end - pos]
    return dsp.highpass(out, 900, 2)


def fire_whoosh(seconds=0.8, rng=None, intensity=1.0):
    """Igniting flame: a low, breathy 'whoomph' with a soft turbulent flutter and a few embers."""
    rng = _rng(rng)
    n = _n(seconds)
    t = np.arange(n) / SR
    roar = shaped_noise(seconds, color="brown", rng=rng)
    turb = signal.sosfilt(signal.butter(1, 18 / (SR / 2), output="sos"), rng.standard_normal(n))
    turb = 0.75 + 0.25 * turb / (np.abs(turb).max() + 1e-9)
    roar = dsp.sweep_filter(roar, lambda tt: 300 + 1600 * np.exp(-tt * 2.5), q=0.6) * turb
    body = dsp.lowpass(shaped_noise(seconds, color="pink", rng=rng), 900, 2) * 0.6
    y = (roar * 1.6 + body) * env_ar(n, 0.12, 0.88, 1.3) * intensity
    return y + crackle(seconds, 25, rng) * 0.12 * np.exp(-t / (seconds * 0.6))


def explosion(size=1.0, rng=None):
    rng = _rng(rng)
    seconds = 0.9 + 0.8 * size
    n = _n(seconds)
    t = np.arange(n) / SR
    boom = np.sin(2 * np.pi * np.cumsum(28 + 90 * np.exp(-t * 18)) / SR) * np.exp(-t / (0.25 * size))
    blast = shaped_noise(seconds, color="pink", rng=rng)
    blast = dsp.sweep_filter(blast, lambda tt: 200 + 5000 * np.exp(-tt * 9), q=0.6) * np.exp(-t / (0.18 * size))
    debris = crunch(seconds, 140, 400, 3500, rng=rng, decay=0.4 * size) * 0.3
    return boom * 1.2 + blast * 1.4 + debris + crackle(seconds, 30, rng) * 0.15 * np.exp(-t / 0.5)


def ice_shatter(rng=None, seconds=0.8):
    """Shattering ice: a sharp crack, a dense burst of noisy shards and a few falling fragments."""
    rng = _rng(rng)
    crack = shaped_noise(0.06, 1500, 14000, rng=rng) * np.exp(-np.arange(_n(0.06)) / (SR * 0.01))
    body = crunch(0.25, 1400, 1500, 11000, rng=rng, decay=0.07)

    def shard(r):
        f = r.uniform(2500, 9500)
        return resonant_burst([f, f * 1.53, f * 2.31], [35, 28, 20], [1, 0.5, 0.3], r.uniform(0.04, 0.12), 0.002, r)

    shards = grains(seconds, 24, shard, rng, curve=2.2, gain_range=(0.1, 0.45))
    thud = thump(rng.uniform(90, 130), 0.12, drop=0.2, click=0.3, rng=rng) * 0.4
    real = sample("k_glass", 0.8, detune=rng.uniform(1, 5), rng=rng, seconds=0.6) * 0.7
    return dsp.mix(real, (crack * 1.0, 0), (body * 0.7, 0.001), (shards * 0.8, 0.01), (thud, 0)).mean(axis=1)


def glass_break(rng=None, seconds=1.0):
    """Breaking glass: impact crack, a noisy shatter and fragments tinkling down."""
    rng = _rng(rng)
    shards = ice_shatter(rng, seconds * 0.6)

    def frag(r):
        f = r.uniform(3500, 9000)
        return resonant_burst([f, f * 2.37], [60, 40], [1, 0.4], r.uniform(0.05, 0.15), 0.0015, r)

    tinkle = grains(seconds, 18, frag, rng, curve=0.8, gain_range=(0.05, 0.3))
    real = sample("k_glass_heavy", 0.9, detune=rng.uniform(-2, 2), rng=rng, seconds=seconds)
    return dsp.mix(real, (shards * 0.5, 0), (tinkle * 0.6, 0.06)).mean(axis=1)


def zap(seconds=0.4, rng=None):
    """Electric arc: jittery buzz with crackles."""
    rng = _rng(rng)
    n = _n(seconds)
    f = 90 + 220 * np.abs(signal.sosfilt(signal.butter(1, 30 / (SR / 2), output="sos"), rng.standard_normal(n))) * 6
    buzz = dsp.osc(lambda t: np.interp(t * SR, np.arange(n), f), seconds, "saw")
    buzz = dsp.highpass(buzz, 300, 2) * (0.5 + 0.5 * (rng.random(n) > 0.35))
    sparks = crackle(seconds, 160, rng) * 1.5
    return (buzz * 0.4 + sparks) * env_ar(n, 0.02, 0.8, 1.0)


def thunder(seconds=2.5, rng=None, crack=1.0):
    rng = _rng(rng)
    n = _n(seconds)
    t = np.arange(n) / SR
    snap = shaped_noise(0.08, 1500, 12000, rng=rng) * np.exp(-np.arange(_n(0.08)) / (SR * 0.01)) * crack
    rumble = shaped_noise(seconds, color="brown", rng=rng)
    rumble = dsp.lowpass(rumble, 260, 2)
    am = 0.55 + 0.45 * np.abs(signal.sosfilt(signal.butter(1, 6 / (SR / 2), output="sos"), rng.standard_normal(n))) * 8
    rumble = rumble * np.clip(am, 0, 1.5) * np.exp(-t / (seconds * 0.35)) * np.clip(t / 0.06, 0, 1)
    return dsp.mix(snap * 1.5, (rumble * 2.5, 0.01)).mean(axis=1)


def splat(seconds=0.35, rng=None, wet=1.0):
    """Wet impact: a soft slap, a sloshing low-mid burst and a few droplets."""
    rng = _rng(rng)
    n = _n(seconds)
    t = np.arange(n) / SR
    slosh = dsp.lowpass(shaped_noise(seconds, color="pink", rng=rng), 1600, 2)
    slosh = dsp.sweep_filter(slosh, lambda tt: 1800 * np.exp(-tt * 6) + 250, q=1.2) * np.exp(-t / (seconds * 0.3))
    slap = flesh_hit(0.8, rng) * 0.6

    def drop(r):
        return resonant_burst([r.uniform(700, 1800)], [12], [1], 0.04, 0.003, r)

    drops = grains(seconds, int(8 * wet), drop, rng, curve=1.0, gain_range=(0.05, 0.25))
    return dsp.mix(slap, (slosh * 1.5, 0), (drops, 0.03)).mean(axis=1)


def bubbles(seconds=1.0, count=10, rng=None, size=1.0):
    """Minnaert bubbles: short rising sine chirps."""
    rng = _rng(rng)
    out = np.zeros(_n(seconds))
    for _ in range(count):
        f0 = rng.uniform(300, 1400) / size
        length = rng.uniform(0.02, 0.08) * size
        tt = np.arange(_n(length)) / SR
        chirp = np.sin(2 * np.pi * np.cumsum(f0 * (1 + tt * rng.uniform(4, 12))) / SR) * np.exp(-tt / (length * 0.3))
        dsp.place(out, chirp * rng.uniform(0.3, 1.0), rng.uniform(0, max(0.01, seconds - length)))
    return out


def hiss(seconds=0.8, rng=None, lo=1800):
    n = _n(seconds)
    return shaped_noise(seconds, lo, 12000, rng=rng) * env_ar(n, 0.15, 0.7, 1.3)


def dig(seconds=0.6, rng=None):
    """Clawing through earth: muffled thuds, gritty scrapes and trickling soil."""
    rng = _rng(rng)
    out = np.zeros(_n(seconds + 0.2))
    for k in range(int(seconds * 5)):
        thud = sample("k_soft", 0.6, detune=rng.uniform(-6, -2), seconds=0.2, rng=rng)
        scrape = sample("k_step_snow", 0.8, detune=rng.uniform(-7, -3), seconds=0.25, rng=rng)
        dsp.place(out, thud, k / 5 + rng.uniform(0, 0.03), rng.uniform(0.5, 0.9))
        dsp.place(out, scrape, k / 5 + 0.01, rng.uniform(0.5, 0.9))
    trickle = crunch(seconds, 300, 1000, 5000, rng=rng) * 0.25
    dsp.place(out, trickle, 0.05)
    return out


# ---------------------------------------------------------------- magic & tone
def shimmer(seconds=1.2, notes=(84, 88, 91, 96), rng=None, rise=True, spread=0.5):
    """Sparkling chime cluster (glockenspiel / hand chimes + sine glints)."""
    rng = _rng(rng)
    out = np.zeros((_n(seconds + 1.5), 2))
    order = list(notes) if rise else list(reversed(notes))
    for k, m in enumerate(order):
        when = k * seconds / max(1, len(order)) * 0.7
        inst = "glockenspiel" if m >= 79 else "hand_chimes"
        note = instruments.get(inst).note(m, rng.uniform(0.4, 0.7), 0.5, rng=rng, release=0.8)
        dsp.place(out, dsp.pan(dsp.to_mono(note), rng.uniform(-spread, spread)), when, 0.6)
    glint_n = _n(seconds)
    t = np.arange(glint_n) / SR
    glints = sum(np.sin(2 * np.pi * dsp.mtof(m + 12) * t) * env_ar(glint_n, 0.3, 0.7) * 0.05 for m in notes[:3])
    dsp.place(out, dsp.to_stereo(glints), 0)
    return out


def reverse_swell(x, seconds=None):
    """Reverse-reverb swell leading into a sound (stereo in, stereo out)."""
    from . import reverb
    x = dsp.to_stereo(x)
    wet = reverb.reverb(x[::-1], "hall", wet=1.0, dry=0.0)
    wet = wet[::-1]
    if seconds:
        wet = wet[-dsp.samples(seconds):]
    return dsp.fade(wet, 0.1, 0.0)


def tone_pad(midis, seconds, instrument="violins", velocity=0.5, release=0.6):
    out = np.zeros((_n(seconds + release + 0.5), 2))
    for m in midis:
        dsp.place(out, instruments.get(instrument).note(m, velocity, seconds, release=release), 0, 1 / len(midis) ** 0.5)
    return out


def coin_clink(rng=None, size=1.0):
    """A coin striking coins: noise-excited, slightly detuned high modes (no pure beeps)."""
    rng = _rng(rng)
    f = rng.uniform(3600, 5200) / size
    ring = resonant_burst([f, f * 1.43, f * 2.29, f * 2.97, f * 3.8], [140, 120, 90, 70, 50], [1, 0.8, 0.5, 0.35, 0.2],
                          0.25 * size, 0.0012, rng)
    tick = shaped_noise(0.004, 3000, 12000, rng=rng)
    return dsp.mix(ring * 0.7, (tick * 0.5, 0)).mean(axis=1)


def coins(count=6, seconds=0.6, rng=None):
    """A handful of coins: recorded coin handling with extra clinks scattered over it."""
    rng = _rng(rng)
    base = sample("k_coins", 0.9, detune=rng.uniform(-1, 2), rng=rng, seconds=seconds + 0.3)
    out = np.zeros(max(len(base), _n(seconds + 0.35)))
    out[: len(base)] += base
    for _ in range(max(0, count - 3)):
        dsp.place(out, coin_clink(rng, rng.uniform(0.8, 1.2)), rng.uniform(0, seconds) ** 1.3, rng.uniform(0.15, 0.4))
    return out


def paper(seconds=0.3, rng=None, density=320):
    """Parchment handling: a recorded page flip, with a little extra crinkle for older vellum."""
    rng = _rng(rng)
    flip = sample("k_book_flip", 0.8, detune=rng.uniform(-2, 1), rng=rng, seconds=max(seconds, 0.25))
    crinkle = crunch(seconds, density * 0.4, 900, 5000, rng=rng) * 0.25
    return dsp.mix(flip, (crinkle, 0.01)).mean(axis=1)
