"""Small helpers shared by the cue modules."""
import numpy as np

from ak import dsp, instruments, reverb
from ak import foley as F


def note(inst, midi, vel=0.7, dur=0.4, release=None, detune=0.0, rng=None):
    return dsp.to_stereo(instruments.get(inst).note(midi, vel, dur, rng=rng, release=release, detune=detune))


def seq(inst, notes, step=0.08, vel=0.7, dur=0.3, release=None, rng=None, pan_spread=0.0):
    """Quick run of notes (arpeggios, glissandi)."""
    out = np.zeros((dsp.samples(step * len(notes) + dur + 2.0), 2))
    for k, m in enumerate(notes):
        x = note(inst, m, vel, dur, release, rng=rng)
        if pan_spread:
            x = dsp.pan(dsp.to_mono(x), (k / max(1, len(notes) - 1) - 0.5) * 2 * pan_spread)
        dsp.place(out, x, k * step)
    return dsp.trim_silence(out, -70)


def chord(inst, notes, vel=0.7, dur=0.5, release=None, strum=0.0, rng=None):
    out = np.zeros((dsp.samples(dur + strum * len(notes) + 3.0), 2))
    for k, m in enumerate(notes):
        dsp.place(out, note(inst, m, vel, dur, release, rng=rng), k * strum, 1 / len(notes) ** 0.5)
    return dsp.trim_silence(out, -70)


def s(name, vel=0.8, detune=0.0, seconds=None, rng=None):
    """Mono one-shot from an unpitched instrument/foley group."""
    return F.sample(name, vel, detune=detune, seconds=seconds, rng=rng)


def mix(*parts):
    return dsp.mix(*parts)


def space(x, name="room", wet=0.1):
    return reverb.reverb(dsp.to_stereo(x), name, wet=wet)


def spread(x, amount=0.35, rng=None):
    """Mono source to a slightly wide stereo image (decorrelated short delay)."""
    x = dsp.to_mono(x)
    d = dsp.samples(0.007)
    right = np.concatenate([np.zeros(d), x])[: len(x)]
    return np.column_stack([x, x * (1 - amount) + right * amount])
