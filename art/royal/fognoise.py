#!/usr/bin/env python3
"""Bake the tileable cloud noise behind the campaign map's fog of war (assets/shaders/royal_fog.gdshader).

Each channel is an independent fractal value noise that repeats seamlessly across the 256 px tile:
red and green steer the domain warp, blue shapes the billows and alpha adds the fine wisps.

  python3 art/royal/fognoise.py
"""
import random
from pathlib import Path

from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "assets/world/royal/maps/fog-noise.png"
SIZE = 256


def octave(period: int, rng: random.Random) -> Image.Image:
    """Smooth random lattice of period x period cells, wrapped so the tile repeats."""
    lattice = Image.new("F", (period, period))
    lattice.putdata([rng.random() for _ in range(period * period)])
    tiled = Image.new("F", (period * 3, period * 3))
    for y in range(3):
        for x in range(3):
            tiled.paste(lattice, (x * period, y * period))
    big = tiled.resize((SIZE * 3, SIZE * 3), Image.BICUBIC)
    return big.crop((SIZE, SIZE, SIZE * 2, SIZE * 2))


def fractal(seed: int, periods, gain: float) -> Image.Image:
    rng = random.Random(seed)
    total = [0.0] * (SIZE * SIZE)
    amplitude, weight = 1.0, 0.0
    for period in periods:
        layer = list(octave(period, rng).get_flattened_data())
        total = [t + v * amplitude for t, v in zip(total, layer)]
        weight += amplitude
        amplitude *= gain
    total = [t / weight for t in total]
    low, high = min(total), max(total)
    # Stretch to the full range so the shader's thresholds mean the same thing on every channel.
    channel = Image.new("L", (SIZE, SIZE))
    channel.putdata([round((t - low) / (high - low) * 255) for t in total])
    return channel


def main() -> None:
    warp_x = fractal(11, (4, 8, 16), .5)
    warp_y = fractal(23, (4, 8, 16), .5)
    billow = fractal(37, (4, 8, 16, 32, 64), .52)
    wisps = fractal(41, (8, 16, 32, 64), .6)
    image = Image.merge("RGBA", (warp_x, warp_y, billow, wisps))
    OUT.parent.mkdir(parents=True, exist_ok=True)
    image.save(OUT)
    # A repeat check: the seam between two copies must be invisible.
    pair = Image.new("RGBA", (SIZE * 2, SIZE))
    pair.paste(image, (0, 0)); pair.paste(image, (SIZE, 0))
    left, right = pair.crop((SIZE - 1, 0, SIZE, SIZE)), pair.crop((SIZE, 0, SIZE + 1, SIZE))
    seam = max(ImageChops.difference(left, right).getextrema()[i][1] for i in range(4))
    print(f"wrote {OUT.relative_to(ROOT)} (largest step across the seam: {seam})")


if __name__ == "__main__":
    main()
