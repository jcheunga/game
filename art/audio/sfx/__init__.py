"""Sound-effect catalogue: every cue the game plays, with its variants, loudness target and runtime settings.

Modules register cues with @cue; build_sfx.py renders them to assets/sfx/<id>_<n>.ogg and writes sfx.json.
"""
from dataclasses import dataclass, field

import numpy as np

from ak import dsp, reverb

CUES = {}

# Loudness targets (max short-term/momentary LUFS for one-shots, integrated for loops) per category.
LEVELS = {
    "ui_quiet": -27.0, "ui": -24.0, "ui_bright": -21.0, "reward": -19.0,
    "swing": -23.0, "hit": -19.5, "hit_heavy": -17.0, "launch": -21.0, "impact": -19.5, "impact_big": -16.0,
    "death": -20.0, "fall": -22.0, "voice": -20.5, "voice_big": -16.5, "deploy": -21.0, "ability": -18.0,
    "spell": -16.5, "base": -18.0, "event": -15.0, "stinger": -13.5, "bed": -30.0, "detail": -27.0,
}


@dataclass
class Cue:
    id: str
    fn: object
    variants: int = 1
    level: str = "hit"
    bus: str = "Effects"
    stereo: bool = False
    positional: bool = False
    cooldown: float = 0.03
    voices: int = 4
    pitch: float = 0.03
    volume_db: float = 0.0
    loop: bool = False
    label: str = ""
    tail: float = 0.0
    space: str = None
    wet: float = 0.0
    extra: dict = field(default_factory=dict)


def cue(id, variants=1, level="hit", bus="Effects", stereo=False, positional=False, cooldown=0.03, voices=4,
        pitch=0.03, volume_db=0.0, loop=False, label="", space=None, wet=0.0):
    """Register fn(rng, k) -> buffer as cue `id` with `variants` renders."""
    def wrap(fn):
        CUES[id] = Cue(id, fn, variants, level, bus, stereo, positional, cooldown, voices, pitch, volume_db, loop, label,
                       space=space, wet=wet)
        return fn
    return wrap


def finish(c, x):
    """Space, cleanup and loudness for one rendered variant."""
    x = np.asarray(x, dtype=float)
    if c.space and c.wet:
        x = reverb.reverb(dsp.to_stereo(x), c.space, wet=c.wet)
    if not c.stereo:
        x = dsp.to_mono(x)
    if not c.loop:
        x = dsp.trim_silence(x, -54, pad=0.002)
        x = dsp.fade(x, 0.0015, min(0.25, len(x) / dsp.SR * 0.2))
    if not c.loop:
        x = dsp.highpass(x, 28, 2)
    x = x - x.mean(axis=0)
    target = LEVELS[c.level]
    if c.loop:
        return x * dsp.db(target - dsp.loudness(x))
    return dsp.to_lufs(x, target, ceiling_db=-1.0, measure=dsp.short_term_max)
