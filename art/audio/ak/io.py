"""Writing OGG/WAV and visual/numeric inspection of rendered audio."""
from pathlib import Path

import numpy as np
import soundfile as sf

from . import dsp
from .dsp import SR


def write(path, x, quality=0.6):
    """Write stereo float audio. .ogg uses Vorbis (quality 0..1), .wav writes 16-bit PCM."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    data = np.clip(dsp.to_stereo(x), -1, 1).astype(np.float32)
    if path.suffix == ".ogg":
        # libsndfile's Vorbis encoder crashes on very large single writes; feed it in blocks.
        with sf.SoundFile(str(path), "w", SR, 2, format="OGG", subtype="VORBIS",
                          compression_level=1 - quality) as handle:
            for start in range(0, len(data), 32768):
                handle.write(data[start:start + 32768])
    else:
        sf.write(str(path), data, SR, subtype="PCM_16")
    return path


def stats(x, loop=False):
    data = dsp.to_stereo(x)
    peak = float(np.abs(data).max())
    out = {
        "seconds": round(len(data) / SR, 2),
        "lufs": round(dsp.loudness(data), 1) if len(data) > dsp.samples(0.4) else None,
        "st_max": round(dsp.short_term_max(data), 1),
        "peak_db": round(20 * np.log10(peak + 1e-12), 2),
        "dc": round(float(np.abs(data.mean(axis=0)).max()), 5),
        "corr": round(float(np.corrcoef(data[:, 0], data[:, 1])[0, 1]), 2) if data[:, 0].std() > 0 else 1.0,
    }
    if loop:
        # Seam check: jump at the wrap compared with typical sample-to-sample movement.
        jump = np.abs(data[0] - data[-1]).max()
        typical = np.percentile(np.abs(np.diff(data, axis=0)).max(axis=1), 99.5)
        out["seam_ratio"] = round(float(jump / (typical + 1e-9)), 2)
        a = dsp.loudness(data[-dsp.samples(3):]) if len(data) > dsp.samples(3) else None
        b = dsp.loudness(data[: dsp.samples(3)]) if len(data) > dsp.samples(3) else None
        out["seam_lufs_step"] = round(abs(a - b), 1) if a is not None else None
    return out


def spectrogram(path, x, title="", seconds=None):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from scipy import signal

    mono = dsp.to_mono(x)
    if seconds:
        mono = mono[: dsp.samples(seconds)]
    f, t, s = signal.spectrogram(mono, SR, nperseg=2048, noverlap=1536, scaling="spectrum")
    s_db = 10 * np.log10(s + 1e-14)
    fig, (ax1, ax2) = plt.subplots(2, 1, figsize=(14, 6), gridspec_kw={"height_ratios": [3, 1]}, sharex=True)
    ax1.pcolormesh(t, f, s_db, shading="auto", vmin=s_db.max() - 90, vmax=s_db.max(), cmap="magma")
    ax1.set_yscale("symlog", linthresh=200)
    ax1.set_ylim(30, 20000)
    ax1.set_ylabel("Hz")
    ax1.set_title(title)
    hop = dsp.samples(0.05)
    env = [20 * np.log10(np.sqrt(np.mean(mono[i:i + hop] ** 2)) + 1e-9) for i in range(0, len(mono) - hop, hop)]
    ax2.plot(np.arange(len(env)) * 0.05, env, lw=0.8)
    ax2.set_ylim(-70, 0)
    ax2.set_ylabel("dBFS rms")
    ax2.set_xlabel("s")
    fig.tight_layout()
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(path, dpi=80)
    plt.close(fig)
    return path


def contact_sheet(paths, out_path, cols=6, title=""):
    """Grid of small spectrograms + envelopes for many short sounds."""
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from scipy import signal

    rows = (len(paths) + cols - 1) // cols
    fig, axes = plt.subplots(rows, cols, figsize=(cols * 2.6, rows * 1.9), squeeze=False)
    for ax in axes.flat:
        ax.axis("off")
    for ax, path in zip(axes.flat, paths):
        x, rate = sf.read(str(path), always_2d=True)
        mono = x.mean(axis=1)
        if len(mono) < 512:
            continue
        f, t, s = signal.spectrogram(mono, rate, nperseg=512, noverlap=384)
        s_db = 10 * np.log10(s + 1e-14)
        ax.pcolormesh(t, f, s_db, shading="auto", vmin=s_db.max() - 80, vmax=s_db.max(), cmap="magma")
        ax.set_yscale("symlog", linthresh=300)
        ax.set_ylim(40, 20000)
        ax.set_title(Path(path).stem[:24], fontsize=7)
        ax.axis("on")
        ax.tick_params(labelsize=5)
    if title:
        fig.suptitle(title, fontsize=9)
    fig.tight_layout()
    fig.savefig(out_path, dpi=70)
    plt.close(fig)
    return out_path
