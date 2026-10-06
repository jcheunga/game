#!/usr/bin/env python3
"""Install the painted relic and spell images (art/royal/gen/items) as 512 px square, transparent
pictures in assets/ui/royal/items/<id>.png, trimmed to the object with a small even margin.

  python3 art/royal/items.py
"""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "art/royal/gen/items"
OUT = ROOT / "assets/ui/royal/items"


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for path in sorted(SOURCE.glob("*.png")):
        image = Image.open(path).convert("RGBA")
        box = image.getchannel("A").point(lambda a: 255 if a > 16 else 0).getbbox()
        if not box:
            continue
        image = image.crop(box)
        side = round(max(image.size) * 1.08)
        square = Image.new("RGBA", (side, side))
        square.paste(image, ((side - image.width) // 2, (side - image.height) // 2))
        square.resize((512, 512), Image.LANCZOS).save(OUT / path.name, optimize=True)
        print(path.stem)


if __name__ == "__main__":
    main()
