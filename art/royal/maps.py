#!/usr/bin/env python3
"""Install the painted zone maps and map landmarks.

Maps (art/royal/gen/maps/<zone>.png) were painted over layout guides cropped from the game's own
geography (art/royal/gen/map-guides/crops.json, world units), so each painting maps back onto the
same world rectangle. Landmarks (art/royal/gen/landmarks/*.png) are trimmed to their content.

  python3 art/royal/maps.py
"""
import json
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
GEN = ROOT / "art/royal/gen"
OUT = ROOT / "assets/world/royal/maps"


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    crops = json.loads((GEN / "map-guides/crops.json").read_text())
    for zone, rect in crops.items():
        source = GEN / f"maps/{zone}.png"
        if not source.exists():
            continue
        image = Image.open(source).convert("RGB")
        if image.width > 2560:
            image = image.resize((2560, round(image.height * 2560 / image.width)), Image.LANCZOS)
        image.save(OUT / f"{zone}.png", optimize=True)
        corners = [image.getpixel((4, 4)), image.getpixel((image.width - 5, 4)), image.getpixel((4, image.height - 5)), image.getpixel((image.width - 5, image.height - 5))]
        sea = tuple(sum(c[i] for c in corners) // 4 for i in range(3))
        (OUT / f"{zone}.json").write_text(json.dumps({"rect": rect, "sea": "%02x%02x%02x" % sea}, indent=1))
        print("map", zone, image.size)
    landmarks = OUT / "landmarks"
    landmarks.mkdir(exist_ok=True)
    # The sites the map draws (MapPathCanvas.LandmarkName); other generations are not installed.
    used = {"boss", "leader", "camp", "watchtower", "shrine", "food", "gold", "tomes", "essence", "survey"}
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
