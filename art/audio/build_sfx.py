"""Render the sound-effect catalogue: python build_sfx.py [cue_or_prefix ...] [--clean] [--no-assets]."""
import argparse
import importlib
import json
import time
import zlib
from pathlib import Path

import numpy as np

import sfx
from ak import dsp, io

ROOT = Path(__file__).resolve().parents[2]
SFX_DIR = ROOT / "assets" / "sfx"
REVIEW = ROOT / "artifacts" / "audio-review" / "sfx"
MODULES = ["sfx.ui", "sfx.combat", "sfx.creatures", "sfx.magic", "sfx.stingers", "sfx.ambience"]


def load_modules():
    for name in MODULES:
        try:
            importlib.import_module(name)
        except ModuleNotFoundError as exc:
            if exc.name != name:
                raise


def file_names(c):
    # Loops stay Vorbis; one-shots ship as WAV, which Godot imports as QOA and mixes for far less CPU than Vorbis.
    ext = ".ogg" if c.loop else ".wav"
    return [f"{c.id}{ext}"] if c.variants == 1 else [f"{c.id}_{k + 1}{ext}" for k in range(c.variants)]


def render(c):
    out = []
    for k in range(c.variants):
        rng = np.random.default_rng(zlib.crc32(f"{c.id}:{k}".encode()))
        dsp.seed(zlib.crc32(f"{c.id}:{k}:dsp".encode()))
        out.append(sfx.finish(c, c.fn(rng, k)))
    return out


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("cues", nargs="*")
    parser.add_argument("--clean", action="store_true", help="delete assets/sfx files no longer in the catalogue")
    parser.add_argument("--no-assets", action="store_true")
    args = parser.parse_args()
    load_modules()
    selected = [c for c in sfx.CUES.values() if not args.cues or any(c.id == p or c.id.startswith(p) for p in args.cues)]
    manifest_path = SFX_DIR / "sfx.json"
    manifest = json.loads(manifest_path.read_text()) if manifest_path.exists() else {}
    labels_path = REVIEW / "labels.json"
    labels = json.loads(labels_path.read_text()) if labels_path.exists() else {}
    for c in selected:
        t0 = time.time()
        buffers = render(c)
        names = file_names(c)
        stats = []
        for name, x in zip(names, buffers):
            review = REVIEW / Path(name).with_suffix(".ogg")
            io.write(review, x, quality=0.6)
            if not args.no_assets:
                # A mono cue written as stereo makes the phone decode every sample twice.
                io.write(SFX_DIR / name, x, quality=0.35, mono=not c.stereo)
            st = io.stats(x, loop=c.loop)
            stats.append(st)
            labels[str(review)] = c.label
        manifest[c.id] = {
            "files": [f"res://assets/sfx/{n}" for n in names], "bus": c.bus, "positional": c.positional,
            "cooldown": c.cooldown, "voices": c.voices, "pitch": c.pitch, "volume_db": c.volume_db, "loop": c.loop,
        }
        lengths = [s["seconds"] for s in stats]
        print(f"{c.id:26s} x{c.variants} {time.time() - t0:5.1f}s len {min(lengths):.2f}-{max(lengths):.2f}s "
              f"st_max {stats[0]['st_max']} peak {stats[0]['peak_db']}", flush=True)
    if not args.no_assets:
        from sfx.ambience import AMBIENCE
        (SFX_DIR / "ambience.json").write_text(json.dumps(
            {ctx: {"bed": bed, "details": det, "gap": list(gap)} for ctx, (bed, det, gap) in AMBIENCE.items()}, indent=1) + "\n")
        manifest = {k: v for k, v in sorted(manifest.items()) if k in sfx.CUES}
        manifest_path.write_text(json.dumps(manifest, indent=1) + "\n")
        if args.clean:
            keep = {Path(f).name for v in manifest.values() for f in v["files"]}
            for path in [*SFX_DIR.glob("*.ogg"), *SFX_DIR.glob("*.wav")]:
                if path.name not in keep:
                    path.unlink()
                    imp = path.with_name(path.name + ".import")
                    if imp.exists():
                        imp.unlink()
                    print("removed", path.name)
    REVIEW.mkdir(parents=True, exist_ok=True)
    labels_path.write_text(json.dumps(dict(sorted(labels.items())), indent=1))


if __name__ == "__main__":
    main()
