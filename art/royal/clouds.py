#!/usr/bin/env python3
"""Pack the painted cloud layers behind the campaign map's fog of war (assets/shaders/royal_fog.gdshader).

Codex paints each layer as white cloud on black (jobs/18-map-clouds.json, art/royal/gen/clouds). This squares
each painting, makes it repeat seamlessly and packs its brightness into one channel of fog-clouds.png: red is
the upper layer of fluffy cloudlets, green the thin veil drifting below it. A painting never tiles on its own,
so each axis in turn is cross-faded, in a narrow band, with a copy of itself shifted by half a tile, whose
content runs on unbroken across the tile's edge.

  python3 art/royal/clouds.py
"""
from pathlib import Path

from PIL import Image, ImageChops, ImageOps

ROOT = Path(__file__).resolve().parents[2]
GEN = ROOT / "art/royal/gen/clouds"
OUT = ROOT / "assets/world/royal/maps/fog-clouds.png"
SIZE = 1024
LAYERS = ("puffs", "veil")
# The width of the cross-fade, as a share of the tile.
BAND = 0.16


def square(image: Image.Image) -> Image.Image:
    side = min(image.size)
    return ImageOps.fit(image, (side, side), Image.LANCZOS).resize((SIZE, SIZE), Image.LANCZOS)


def ramp(axis: str) -> Image.Image:
    """1 across the middle of the tile, falling to 0 over a narrow band around each quarter line, so the
    shifted copy shows only near the tile's own edge."""
    values = []
    for i in range(SIZE):
        d = abs(i + 0.5 - SIZE / 2) / SIZE  # 0 at the middle, 0.5 at the edge
        t = min(1.0, max(0.0, (0.25 + BAND / 2 - d) / BAND))
        values.append(round(255 * t * t * (3 - 2 * t)))
    line = Image.new("L", (SIZE, 1) if axis == "x" else (1, SIZE))
    line.putdata(values)
    return line.resize((SIZE, SIZE), Image.NEAREST)


def seamless(image: Image.Image) -> Image.Image:
    for axis in ("x", "y"):
        shifted = ImageChops.offset(image, SIZE // 2 if axis == "x" else 0, SIZE // 2 if axis == "y" else 0)
        image = Image.composite(image, shifted, ramp(axis))
    return image


def layer(name: str) -> Image.Image:
    painting = Image.open(GEN / f"{name}.png").convert("RGB")
    # Brightness is the cloud; the painting's own blue-grey shading stays in it as darker values.
    return seamless(square(painting).convert("L"))


def main() -> None:
    puffs, veil = (layer(name) for name in LAYERS)
    blank = Image.new("L", (SIZE, SIZE))
    image = Image.merge("RGB", (puffs, veil, blank))
    image.save(OUT)
    pair = Image.new("RGB", (SIZE * 2, SIZE))
    pair.paste(image, (0, 0)); pair.paste(image, (SIZE, 0))
    left, right = pair.crop((SIZE - 1, 0, SIZE, SIZE)), pair.crop((SIZE, 0, SIZE + 1, SIZE))
    seam = max(ImageChops.difference(left, right).getextrema()[i][1] for i in range(3))
    print(f"wrote {OUT.relative_to(ROOT)} (largest step across the seam: {seam})")


if __name__ == "__main__":
    main()
