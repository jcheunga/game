#!/usr/bin/env python3
"""Install the painted map landmarks (castles, caches and finds drawn over the zone maps).

Landmarks (art/royal/gen/landmarks/*.png) are trimmed to their content. The zone maps themselves are painted in
sections and installed by art/royal/mapsections.py.

  python3 art/royal/maps.py
"""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
GEN = ROOT / "art/royal/gen"
OUT = ROOT / "assets/world/royal/maps"


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    landmarks = OUT / "landmarks"
    landmarks.mkdir(exist_ok=True)
    # The sites the map draws (MapPathCanvas.LandmarkName); other generations are not installed.
    used = {"boss", "leader", "camp", "watchtower", "shrine", "food", "gold", "essence", "survey"}
    for source in sorted((GEN / "landmarks").glob("*.png")):
        if source.stem not in used:
            continue
        image = Image.open(source).convert("RGBA")
        box = image.getchannel("A").point(lambda a: 255 if a > 20 else 0).getbbox()
        if box:
            image = image.crop(box)
        if image.height > 512:
            image = image.resize((round(image.width * 512 / image.height), 512), Image.LANCZOS)
        image.save(landmarks / source.name, optimize=True)
        print("landmark", source.stem, image.size)


if __name__ == "__main__":
    main()
