#!/usr/bin/env python3
"""Install painted card pictures: hub activities (assets/ui/royal/items/activity-<name>.png) and war wagon
upgrades (assets/ui/royal/items/upgrade_<id>.png), resized for their cards.

  python3 art/royal/pictures.py
"""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
GEN = ROOT / "art/royal/gen"
OUT = ROOT / "assets/ui/royal/items"


def install(folder, prefix, width):
    for path in sorted((GEN / folder).glob("*.png")):
        image = Image.open(path).convert("RGB")
        if image.width > width:
            image = image.resize((width, round(image.height * width / image.width)), Image.LANCZOS)
        image.save(OUT / f"{prefix}{path.stem}.png", optimize=True)
        print(prefix + path.stem)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    install("activities", "activity-", 1100)
    install("upgrades", "upgrade_", 640)


if __name__ == "__main__":
    main()
