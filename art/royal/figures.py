#!/usr/bin/env python3
"""Resting-pose figures for unit cards (assets/ui/royal/figures/<id>.png).

Takes the first idle frame of each unit's preview sheet (assets/ui/models/<id>.png + .json), crops it to
the figure (ignoring soft shadows) and saves it, so screens draw one small texture per card instead of
decompressing battle atlases.
"""
import json
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
MODELS = ROOT / "assets/ui/models"
OUT = ROOT / "assets/ui/royal/figures"


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for meta_path in sorted(MODELS.glob("*.json")):
        meta = json.loads(meta_path.read_text())
        sheet = Image.open(meta_path.with_suffix(".png")).convert("RGBA")
        fw, fh = meta["frameWidth"], meta["frameHeight"]
        start = meta.get("animations", {}).get("idle", {}).get("start", 0)
        columns = max(1, sheet.width // fw)
        x, y = start % columns * fw, start // columns * fh
        frame = sheet.crop((x, y, x + fw, y + fh))
        alpha = frame.getchannel("A").point(lambda a: 255 if a > 140 else 0)
        box = alpha.getbbox()
        if not box:
            continue
        pad = 3
        box = (max(0, box[0] - pad), max(0, box[1] - pad), min(fw, box[2] + pad), min(fh, box[3] + pad))
        frame.crop(box).save(OUT / f"{meta_path.stem}.png", optimize=True)
        print(meta_path.stem, box)


if __name__ == "__main__":
    main()
