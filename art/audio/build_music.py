"""Render the score: python build_music.py [track ...] [--review DIR]."""
import argparse
import importlib
import json
import time
from pathlib import Path

from ak import io, lint
from ak import song as S

ROOT = Path(__file__).resolve().parents[2]
MUSIC_DIR = ROOT / "assets" / "music"
REVIEW = ROOT / "artifacts" / "audio-review" / "music"

# track id -> (module, kwargs, loudness target)
TRACKS = {
    "title": ("music.title", {}, -17.0),
    "campaign": ("music.campaign", {}, -17.5),
    "shop": ("music.shop", {}, -17.5),
    "loadout": ("music.loadout", {}, -17.0),
    "endless_prep": ("music.endless_prep", {}, -19.0),
    "multiplayer": ("music.multiplayer", {}, -16.5),
    "battle": ("music.battle", {}, -16.0),
    "battle_road": ("music.battle_road", {}, -16.0),
    "battle_harbor": ("music.battle_harbor", {}, -16.0),
    "battle_foundry": ("music.battle_foundry", {}, -15.5),
    "battle_quarantine": ("music.battle_quarantine", {}, -16.5),
    "battle_pass": ("music.battle_pass", {}, -16.0),
    "battle_basilica": ("music.battle_basilica", {}, -16.5),
    "battle_mire": ("music.battle_mire", {}, -16.5),
    "battle_steppe": ("music.battle_steppe", {}, -16.0),
    "battle_gloamwood": ("music.battle_gloamwood", {}, -16.5),
    "battle_citadel": ("music.battle_citadel", {}, -15.5),
    "battle_boss": ("music.battle_boss", {}, -15.5),
    "battle_boss_final": ("music.battle_boss", {"final": True}, -15.5),
}


def render(track_id):
    module, kwargs, lufs = TRACKS[track_id]
    mod = importlib.import_module(module)
    song = mod.build(**kwargs)
    x = song.render(loop=True)
    x = S.master(x, lufs)
    meta = {"bpm": song.bpm, "bars": song.bars, "seconds": round(song.length, 3)}
    for line in lint.report(song):
        print(line)
    return x, meta


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("tracks", nargs="*")
    parser.add_argument("--review", default=str(REVIEW))
    parser.add_argument("--no-assets", action="store_true")
    args = parser.parse_args()
    ids = args.tracks or list(TRACKS)
    manifest_path = MUSIC_DIR / "tracks.json"
    manifest = json.loads(manifest_path.read_text()) if manifest_path.exists() else {}
    for track_id in ids:
        t0 = time.time()
        x, meta = render(track_id)
        st = io.stats(x, loop=True)
        review = Path(args.review)
        io.write(review / f"{track_id}.ogg", x, quality=0.7)
        io.spectrogram(review / f"{track_id}.png", x, f"{track_id}  {meta['bpm']} bpm  {st['lufs']} LUFS")
        if not args.no_assets:
            io.write(MUSIC_DIR / f"{track_id}.ogg", x, quality=0.35)
            manifest[track_id] = meta
        print(f"{track_id:20s} {time.time() - t0:5.1f}s {st}", flush=True)
    if not args.no_assets:
        manifest_path.write_text(json.dumps(dict(sorted(manifest.items())), indent=2) + "\n")


if __name__ == "__main__":
    main()
