#!/usr/bin/env python3
"""Install the painted battlefield pickups (art/royal/gen/courage, from art/royal/jobs/12-courage-pack.json) into
assets/fx/pickups for scripts/combat/CouragePickup.cs.

  courage_ember   the courage pack a fallen enemy sometimes drops: trimmed on its alpha and centred on a square
                  canvas, so the code can spin and scale it about its middle

  python3 art/royal/pickups.py
"""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "art/royal/gen/courage"
OUT = ROOT / "assets/fx/pickups"
PICKUPS = {"courage_ember": ("ember.png", 256)}


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, (source, size) in PICKUPS.items():
        image = Image.open(SOURCE / source).convert("RGBA")
        # Codex leaves a faint haze in the alpha; drop it so the trimmed box hugs the painting.
        alpha = image.getchannel("A").point(lambda a: 0 if a < 8 else a)
        image.putalpha(alpha)
        image = image.crop(alpha.point(lambda a: 255 if a > 24 else 0).getbbox())
        side = round(max(image.size) * 1.02)
        canvas = Image.new("RGBA", (side, side))
        canvas.paste(image, ((side - image.width) // 2, (side - image.height) // 2))
        canvas = canvas.resize((size, size), Image.LANCZOS)
        canvas.save(OUT / f"{name}.png", optimize=True)
        print(name, canvas.size, "content", image.size)


if __name__ == "__main__":
    main()
