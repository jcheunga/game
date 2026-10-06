"""Core DSP: buffers are float64 numpy arrays, mono (n,) or stereo (n, 2), at SR."""
from functools import lru_cache

import numpy as np
from scipy import signal

SR = 44100
RNG = np.random.default_rng(1)


def seed(value):
    global RNG
    RNG = np.random.default_rng(value)
    return RNG


def secs(n):
    return n / SR


def samples(seconds):
    return int(round(seconds * SR))


def t_axis(seconds):
    return np.arange(samples(seconds)) / SR


def silence(seconds, stereo=True):
    n = samples(seconds)
    return np.zeros((n, 2)) if stereo else np.zeros(n)


def to_stereo(x):
    return np.column_stack([x, x]) if x.ndim == 1 else x


def to_mono(x):
    return x.mean(axis=1) if x.ndim == 2 else x


def db(value):
    return 10 ** (value / 20)


def mtof(midi):
    return 440.0 * 2 ** ((np.asarray(midi, dtype=float) - 69) / 12)


def pan(x, position):
    """Equal-power pan of a mono or stereo buffer; position -1 (left) .. 1 (right)."""
    angle = (np.clip(position, -1, 1) + 1) * np.pi / 4
    left, right = np.cos(angle) * np.sqrt(2), np.sin(angle) * np.sqrt(2)
    if x.ndim == 1:
        return np.column_stack([x * left, x * right])
    return np.column_stack([x[:, 0] * left, x[:, 1] * right])


def width(x, amount):
    """Mid/side stereo width; 0 = mono, 1 = unchanged, >1 wider."""
    if x.ndim == 1:
        return to_stereo(x)
    mid, side = (x[:, 0] + x[:, 1]) / 2, (x[:, 0] - x[:, 1]) / 2 * amount
    return np.column_stack([mid + side, mid - side])


def mix(*parts, length=None):
    """Sum buffers (padding to the longest). Each part may be a buffer or (buffer, offset_seconds, gain)."""
    items = []
    for part in parts:
        if isinstance(part, tuple):
            buf, offset, gain = (part + (1.0,))[:3] if len(part) < 3 else part
        else:
            buf, offset, gain = part, 0.0, 1.0
        items.append((to_stereo(buf), samples(offset), gain))
    total = max((len(b) + o for b, o, _ in items), default=0)
    if length is not None:
        total = samples(length)
    out = np.zeros((total, 2))
    for buf, offset, gain in items:
        end = min(total, offset + len(buf))
        if end > offset:
            out[offset:end] += buf[: end - offset] * gain
    return out


def place(out, buf, offset_seconds, gain=1.0, wrap=False):
    """Add buf into out at offset (in place). With wrap, the overflow folds onto the start (seamless loops)."""
    buf = to_stereo(buf) if out.ndim == 2 else to_mono(buf)
    start = samples(offset_seconds)
    n = len(out)
    if wrap:
        start %= n
        idx = (np.arange(len(buf)) + start) % n
        np.add.at(out, idx, buf * gain)
        return out
    if start >= n:
        return out
    if start < 0:
        buf, start = buf[-start:], 0
    end = min(n, start + len(buf))
    out[start:end] += buf[: end - start] * gain
    return out


def fade(x, fade_in=0.0, fade_out=0.0, curve="cos"):
    x = x.copy()
    for seconds, head in ((fade_in, True), (fade_out, False)):
        n = min(len(x), samples(seconds))
        if n <= 1:
            continue
        ramp = np.linspace(0, 1, n)
        if curve == "cos":
            ramp = 0.5 - 0.5 * np.cos(np.pi * ramp)
        elif curve == "exp":
            ramp = ramp ** 2.2
        if not head:
            ramp = ramp[::-1]
        if x.ndim == 2:
            ramp = ramp[:, None]
        if head:
            x[:n] *= ramp
        else:
            x[-n:] *= ramp
    return x


def env_adsr(n, a, d, s, r, sr=SR):
    """ADSR envelope of n samples (a, d, r in seconds; release occupies the tail)."""
    a_n, d_n, r_n = int(a * sr), int(d * sr), int(r * sr)
    a_n = min(a_n, n)
    d_n = min(d_n, max(0, n - a_n))
    r_n = min(r_n, max(0, n - a_n - d_n))
    s_n = n - a_n - d_n - r_n
    parts = [np.linspace(0, 1, a_n, endpoint=False), np.linspace(1, s, d_n, endpoint=False),
             np.full(s_n, s), np.linspace(s, 0, r_n)]
    return np.concatenate(parts)[:n]


def env_exp(n, decay_seconds, attack=0.001):
    """Percussive envelope: short linear attack then exponential decay (-60 dB at decay_seconds)."""
    t = np.arange(n) / SR
    env = np.exp(-6.9 * t / max(decay_seconds, 1e-4))
    a_n = max(1, int(attack * SR))
    env[:a_n] *= np.linspace(0, 1, a_n)
    return env


def env_points(n, points):
    """Piecewise-linear envelope from [(time_seconds, value), ...]."""
    times = np.array([p[0] for p in points]) * SR
    values = np.array([p[1] for p in points])
    return np.interp(np.arange(n), times, values)


# ---------------------------------------------------------------- filters

@lru_cache(maxsize=512)
def _sos(kind, freq, q_or_order, sr):
    nyq = sr / 2
    if kind in ("lowpass", "highpass"):
        f = min(max(freq, 10), nyq * 0.98)
        return signal.butter(q_or_order, f / nyq, btype=kind, output="sos")
    if kind == "bandpass":
        lo, hi = freq
        return signal.butter(q_or_order, [max(lo, 10) / nyq, min(hi, nyq * 0.98) / nyq], btype="band", output="sos")
    raise ValueError(kind)


def _apply_sos(sos, x):
    return signal.sosfilt(sos, x, axis=0)


def lowpass(x, freq, order=2):
    return _apply_sos(_sos("lowpass", float(freq), order, SR), x)


def highpass(x, freq, order=2):
    return _apply_sos(_sos("highpass", float(freq), order, SR), x)


def bandpass(x, lo, hi, order=2):
    return _apply_sos(_sos("bandpass", (float(lo), float(hi)), order, SR), x)


def _biquad(kind, freq, q, gain_db):
    """RBJ cookbook biquads as SOS rows."""
    a_lin = 10 ** (gain_db / 40)
    w0 = 2 * np.pi * min(freq, SR * 0.49) / SR
    cos_w, sin_w = np.cos(w0), np.sin(w0)
    alpha = sin_w / (2 * q)
    if kind == "peak":
        b = [1 + alpha * a_lin, -2 * cos_w, 1 - alpha * a_lin]
        a = [1 + alpha / a_lin, -2 * cos_w, 1 - alpha / a_lin]
    elif kind == "lowshelf":
        sq = 2 * np.sqrt(a_lin) * alpha
        b = [a_lin * ((a_lin + 1) - (a_lin - 1) * cos_w + sq), 2 * a_lin * ((a_lin - 1) - (a_lin + 1) * cos_w),
             a_lin * ((a_lin + 1) - (a_lin - 1) * cos_w - sq)]
        a = [(a_lin + 1) + (a_lin - 1) * cos_w + sq, -2 * ((a_lin - 1) + (a_lin + 1) * cos_w),
             (a_lin + 1) + (a_lin - 1) * cos_w - sq]
    elif kind == "highshelf":
        sq = 2 * np.sqrt(a_lin) * alpha
        b = [a_lin * ((a_lin + 1) + (a_lin - 1) * cos_w + sq), -2 * a_lin * ((a_lin - 1) + (a_lin + 1) * cos_w),
             a_lin * ((a_lin + 1) + (a_lin - 1) * cos_w - sq)]
        a = [(a_lin + 1) - (a_lin - 1) * cos_w + sq, 2 * ((a_lin - 1) - (a_lin + 1) * cos_w),
             (a_lin + 1) - (a_lin - 1) * cos_w - sq]
    elif kind == "resonant_lp":
        b = [(1 - cos_w) / 2, 1 - cos_w, (1 - cos_w) / 2]
        a = [1 + alpha, -2 * cos_w, 1 - alpha]
    elif kind == "resonant_bp":
        b = [alpha, 0, -alpha]
        a = [1 + alpha, -2 * cos_w, 1 - alpha]
    else:
        raise ValueError(kind)
    b, a = np.array(b) / a[0], np.array(a) / a[0]
    return np.concatenate([b, a])[None, :]


def eq(x, *bands):
    """bands: ("peak", f, q, gain_db) | ("lowshelf", f, q, gain_db) | ("highshelf", f, q, gain_db)."""
    for kind, freq, q, gain in bands:
        x = signal.sosfilt(_biquad(kind, freq, q, gain), x, axis=0)
    return x


def resonator(x, freq, q):
    """Constant-peak-gain resonant band-pass (formants, body modes)."""
    return signal.sosfilt(_biquad("resonant_bp", freq, q, 0), x, axis=0)


def sweep_filter(x, freqs, q=0.7, kind="lowpass", block=256):
    """Time-varying filter; freqs is an array (one value per sample) or callable(t)."""
    mono = x.ndim == 1
    data = to_stereo(x) if mono else x
    n = len(data)
    if callable(freqs):
        freqs = freqs(np.arange(n) / SR)
    freqs = np.broadcast_to(np.asarray(freqs, dtype=float), (n,))
    out = np.zeros_like(data)
    zi = np.zeros((1, 2, 2))
    for start in range(0, n, block):
        end = min(n, start + block)
        f = float(np.clip(freqs[start], 20, SR * 0.45))
        if kind == "lowpass":
            sos = _biquad("resonant_lp", f, q, 0)
        elif kind == "bandpass":
            sos = _biquad("resonant_bp", f, q, 0)
        else:
            w0 = 2 * np.pi * f / SR
            alpha = np.sin(w0) / (2 * q)
            cw = np.cos(w0)
            b = np.array([(1 + cw) / 2, -(1 + cw), (1 + cw) / 2]) / (1 + alpha)
            a = np.array([1 + alpha, -2 * cw, 1 - alpha]) / (1 + alpha)
            sos = np.concatenate([b, a])[None, :]
        out[start:end], zi = signal.sosfilt(sos, data[start:end], axis=0, zi=zi)
    return out[:, 0] if mono else out


# ---------------------------------------------------------------- sources

def noise(seconds, color="white", rng=None):
    rng = rng or RNG
    n = samples(seconds)
    white = rng.standard_normal(n)
    if color == "white":
        return white * 0.3
    spectrum = np.fft.rfft(white)
    f = np.fft.rfftfreq(n, 1 / SR)
    f[0] = f[1] if n > 1 else 1
    exponent = {"pink": 0.5, "brown": 1.0, "blue": -0.5}[color]
    shaped = np.fft.irfft(spectrum / f ** exponent, n)
    return shaped / (np.std(shaped) + 1e-12) * 0.3


def stereo_noise(seconds, color="white", correlation=0.0, rng=None):
    rng = rng or RNG
    a, b = noise(seconds, color, rng), noise(seconds, color, rng)
    return np.column_stack([a, correlation * a + np.sqrt(1 - correlation ** 2) * b])


def osc(freq, seconds, shape="sine", phase=0.0):
    """Oscillator with per-sample frequency (scalar, array or callable(t)). Band-limited via PolyBLEP for saw/square."""
    n = samples(seconds)
    t = np.arange(n) / SR
    f = freq(t) if callable(freq) else np.broadcast_to(np.asarray(freq, dtype=float), (n,))
    ph = (phase + np.cumsum(f) / SR) % 1.0
    if shape == "sine":
        return np.sin(2 * np.pi * ph)
    dt = np.clip(f / SR, 1e-6, 0.5)

    def blep(p):
        y = np.zeros_like(p)
        m = p < dt
        q = p[m] / dt[m]
        y[m] = q + q - q * q - 1
        m2 = p > 1 - dt
        q2 = (p[m2] - 1) / dt[m2]
        y[m2] = q2 * q2 + q2 + q2 + 1
        return y

    if shape == "saw":
        return (2 * ph - 1) - blep(ph)
    if shape == "square":
        sq = np.where(ph < 0.5, 1.0, -1.0)
        return sq + blep(ph) - blep((ph + 0.5) % 1.0)
    if shape == "triangle":
        return 2 * np.abs(2 * ph - 1) - 1
    raise ValueError(shape)


def glide(start, end, seconds, curve=1.0):
    """Frequency trajectory callable for osc()."""
    def fn(t):
        u = np.clip(t / max(seconds, 1e-6), 0, 1) ** curve
        return start * (end / start) ** u
    return fn


def modal(freqs, gains, decays, seconds, jitter=0.0, rng=None):
    """Sum of exponentially decaying sinusoids (bells, metal, wood, membranes)."""
    rng = rng or RNG
    n = samples(seconds)
    t = np.arange(n) / SR
    out = np.zeros(n)
    for f, g, d in zip(freqs, gains, decays):
        f = f * (1 + rng.uniform(-jitter, jitter)) if jitter else f
        if f >= SR / 2:
            continue
        out += g * np.sin(2 * np.pi * f * t + rng.uniform(0, 2 * np.pi)) * np.exp(-6.9 * t / d)
    return out


def karplus(freq, seconds, brightness=0.5, decay=0.996, rng=None):
    """Karplus-Strong plucked string."""
    rng = rng or RNG
    n = samples(seconds)
    period = max(2, int(SR / freq))
    buf = rng.uniform(-1, 1, period)
    buf = lowpass(buf, 1000 + brightness * 9000, 1)
    out = np.zeros(n)
    for i in range(n):
        j = i % period
        out[i] = buf[j]
        buf[j] = decay * 0.5 * (buf[j] + buf[(j + 1) % period])
    return out


# ---------------------------------------------------------------- dynamics & shaping

def saturate(x, drive=1.0):
    return np.tanh(x * drive) / np.tanh(drive) if drive > 0 else x


def envelope_follower(x, attack=0.005, release=0.08):
    level = np.abs(to_mono(x))
    a = np.exp(-1 / (attack * SR))
    r = np.exp(-1 / (release * SR))
    # Separate attack/release smoothing done with two IIR passes approximates a peak follower cheaply.
    rise = signal.lfilter([1 - a], [1, -a], level)
    return np.maximum(signal.lfilter([1 - r], [1, -r], np.maximum(level, rise)), 1e-9)


def compress(x, threshold_db=-18, ratio=3.0, attack=0.01, release=0.15, makeup_db=0.0, knee_db=6.0):
    env_db = 20 * np.log10(envelope_follower(x, attack, release))
    over = env_db - threshold_db
    gain_db = np.where(over <= -knee_db / 2, 0.0,
                       np.where(over >= knee_db / 2, -over * (1 - 1 / ratio),
                                -((over + knee_db / 2) ** 2) / (2 * knee_db) * (1 - 1 / ratio)))
    gain = db(gain_db + makeup_db)
    return x * (gain[:, None] if x.ndim == 2 else gain)


def limit(x, ceiling_db=-1.0, lookahead=0.004, release=0.06):
    """Look-ahead brickwall limiter with a smooth release."""
    ceiling = db(ceiling_db)
    peak = np.abs(x).max(axis=1) if x.ndim == 2 else np.abs(x)
    la = max(1, samples(lookahead))
    # Max over the look-ahead window, then smooth the gain reduction.
    padded = np.concatenate([peak, np.zeros(la)])
    windowed = np.lib.stride_tricks.sliding_window_view(padded, la + 1).max(axis=1)[: len(peak)]
    target = np.minimum(1.0, ceiling / np.maximum(windowed, 1e-9))
    r = np.exp(-1 / (release * SR))
    gain = np.empty_like(target)
    g = 1.0
    for i, tg in enumerate(target):
        g = tg if tg < g else tg + (g - tg) * r
        gain[i] = g
    gain = np.convolve(gain, np.ones(la) / la, mode="same")
    gain = np.minimum(gain, target)
    y = x * (gain[:, None] if x.ndim == 2 else gain)
    return np.clip(y, -ceiling, ceiling)


def normalize_peak(x, peak_db=-1.0):
    peak = np.abs(x).max()
    return x if peak < 1e-9 else x * (db(peak_db) / peak)


def trim_silence(x, threshold_db=-60, pad=0.005):
    level = np.abs(to_mono(x))
    above = np.nonzero(level > db(threshold_db) * level.max())[0]
    if len(above) == 0:
        return x[:1]
    start = max(0, above[0] - samples(pad))
    end = min(len(x), above[-1] + samples(pad))
    return x[start:end]


# ---------------------------------------------------------------- loudness (ITU-R BS.1770-4)

@lru_cache(maxsize=4)
def _k_weighting(sr):
    pre = _biquad_raw_highshelf(sr)
    rlb = signal.butter(2, 38.0 / (sr / 2), btype="highpass", output="sos")
    return np.vstack([pre, rlb])


def _biquad_raw_highshelf(sr):
    f0, gain, q = 1681.974450955533, 3.999843853973347, 0.7071752369554196
    k = np.tan(np.pi * f0 / sr)
    vh, vb = 10 ** (gain / 20), 10 ** (gain / 20) ** 0.4996667741545416
    a0 = 1 + k / q + k * k
    b = [(vh + vb * k / q + k * k) / a0, 2 * (k * k - vh) / a0, (vh - vb * k / q + k * k) / a0]
    a = [1, 2 * (k * k - 1) / a0, (1 - k / q + k * k) / a0]
    return np.array([b + a])


def loudness(x, gated=True):
    """Integrated loudness in LUFS."""
    data = to_stereo(x)
    weighted = signal.sosfilt(_k_weighting(SR), data, axis=0)
    block, hop = samples(0.4), samples(0.1)
    if len(weighted) < block:
        power = np.mean(weighted ** 2, axis=0).sum()
        return -0.691 + 10 * np.log10(max(power, 1e-12))
    powers = np.array([np.mean(weighted[i:i + block] ** 2, axis=0).sum()
                       for i in range(0, len(weighted) - block + 1, hop)])
    lk = -0.691 + 10 * np.log10(np.maximum(powers, 1e-12))
    if not gated:
        return float(-0.691 + 10 * np.log10(max(powers.mean(), 1e-12)))
    abs_gate = powers[lk > -70]
    if len(abs_gate) == 0:
        return -70.0
    rel = -0.691 + 10 * np.log10(abs_gate.mean()) - 10
    final = powers[(lk > -70) & (lk > rel)]
    return float(-0.691 + 10 * np.log10(max(final.mean(), 1e-12)))


def short_term_max(x):
    """Max short-term (3 s) loudness; 'momentary' (0.4 s) for clips shorter than 3 s. Good for one-shots."""
    data = to_stereo(x)
    weighted = signal.sosfilt(_k_weighting(SR), data, axis=0)
    window = samples(3.0) if len(data) > samples(3.0) else samples(0.4)
    window = min(window, len(weighted))
    power = (weighted ** 2).sum(axis=1)
    csum = np.concatenate([[0], np.cumsum(power)])
    hop = max(1, samples(0.05))
    means = [(csum[i + window] - csum[i]) / window for i in range(0, len(power) - window + 1, hop)]
    return float(-0.691 + 10 * np.log10(max(max(means), 1e-12)))


def to_lufs(x, target, ceiling_db=-1.0, measure=loudness):
    """Gain to a loudness target, then limit to the true-peak-ish ceiling."""
    current = measure(x)
    y = x * db(target - current)
    return limit(y, ceiling_db) if np.abs(y).max() > db(ceiling_db) else y


# ---------------------------------------------------------------- resampling

def resample_ratio(x, ratio):
    """Change playback speed by ratio (>1 = higher/faster) with 4-point cubic interpolation."""
    if abs(ratio - 1) < 1e-6:
        return x.copy()
    n_out = int((len(x) - 3) / ratio)
    if n_out <= 0:
        return x[:1] * 0
    pos = np.arange(n_out) * ratio + 1
    i = pos.astype(int)
    f = pos - i
    if x.ndim == 2:
        f = f[:, None]
    xm1, x0, x1, x2 = x[i - 1], x[i], x[i + 1], x[np.minimum(i + 2, len(x) - 1)]
    c0 = x0
    c1 = 0.5 * (x1 - xm1)
    c2 = xm1 - 2.5 * x0 + 2 * x1 - 0.5 * x2
    c3 = 0.5 * (x2 - xm1) + 1.5 * (x0 - x1)
    y = ((c3 * f + c2) * f + c1) * f + c0
    if ratio > 1.02:
        y = lowpass(y, SR / 2 / ratio * 0.95, 4)
    return y


def pitch_shift_speed(x, semitones):
    return resample_ratio(x, 2 ** (semitones / 12))


def delay(x, seconds, feedback=0.3, mix_amount=0.3, lp=6000):
    d = samples(seconds)
    out = to_stereo(x).copy()
    tail = np.zeros((len(out) + d * 6, 2))
    tail[: len(out)] = out
    echo = np.zeros_like(tail)
    for k in range(1, 7):
        gain = mix_amount * feedback ** (k - 1)
        if gain < 0.01:
            break
        src = lowpass(out, lp / k ** 0.3, 1) * gain
        seg = src[:, ::-1] if k % 2 else src
        echo[d * k: d * k + len(out)] += seg
    return tail + echo


def chorus(x, depth_ms=6.0, rate=0.6, voices=3, mix_amount=0.5, rng=None):
    rng = rng or RNG
    data = to_stereo(x)
    n = len(data)
    t = np.arange(n) / SR
    out = data.copy()
    for v in range(voices):
        for ch in range(2):
            lfo = (depth_ms / 1000) * SR * (1 + np.sin(2 * np.pi * rate * (1 + 0.13 * v) * t + rng.uniform(0, 6.28))) / 2
            idx = np.arange(n) - lfo - SR * 0.008
            out[:, ch] += np.interp(idx, np.arange(n), data[:, ch], left=0) * mix_amount / voices
    return out


def spectrum_tilt(x, db_per_octave):
    """Gentle tilt EQ around 1 kHz via shelves."""
    return eq(x, ("lowshelf", 250, 0.5, -db_per_octave * 2), ("highshelf", 4000, 0.5, db_per_octave * 2))
