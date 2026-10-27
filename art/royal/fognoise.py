#!/usr/bin/env python3
"""Bake the tileable cloud noise behind the campaign map's fog of war (assets/shaders/royal_fog.gdshader).

Every channel is smooth fractal value noise that repeats seamlessly across the 512 px tile. Red and green
steer the domain warp; blue is the soft billows that push the cloud bank's outline in and out and shade its
top; alpha is the finer wisps for the veil beyond the outline and the frontier mist. Each octave is blurred
a little after it is enlarged, so the lattice it grew from never shows as a grid.

  python3 art/royal/fognoise.py
"""
import random
from pathlib import Path

from PIL import Image, ImageChops, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "assets/world/royal/maps/fog-noise.png"
SIZE = 512


def octave(period: int, rng: random.Random, soften: float) -> list[float]:
    """Smooth random lattice of period x period cells, wrapped so the tile repeats. soften is the blur,
    as a share of one cell, that rounds off the lattice's square cells."""
    lattice = Image.new("F", (period, period))
    lattice.putdata([rng.random() for _ in range(period * period)])
    tiled = Image.new("F", (period * 3, period * 3))
    for y in range(3):
        for x in range(3):
            tiled.paste(lattice, (x * period, y * period))
    big = tiled.resize((SIZE * 3, SIZE * 3), Image.BICUBIC)
    if soften:
        big = big.point(lambda v: v * 255).convert("L").filter(ImageFilter.GaussianBlur(SIZE / period * soften))
    return [v / 255 if soften else v for v in big.crop((SIZE, SIZE, SIZE * 2, SIZE * 2)).get_flattened_data()]


def fractal(seed: int, periods, gain: float, soften: float = 0) -> Image.Image:
    rng = random.Random(seed)
    total = [0.0] * (SIZE * SIZE)
    amplitude = 1.0
    for period in periods:
        total = [t + v * amplitude for t, v in zip(total, octave(period, rng, soften))]
        amplitude *= gain
    # Stretch to the full range, so the shader's thresholds mean the same thing on every channel; the
    # extreme 1% at each end saturates, so a rare extreme cannot flatten the rest.
    ranked = sorted(total)
    low, high = ranked[len(ranked) // 100], ranked[-len(ranked) // 100]
    channel = Image.new("L", (SIZE, SIZE))
    channel.putdata([min(255, max(0, round((t - low) / (high - low) * 255))) for t in total])
    return channel


def main() -> None:
    warp_x = fractal(11, (3, 6, 12), .5)
    warp_y = fractal(23, (3, 6, 12), .5)
    billows = fractal(37, (3, 6, 12, 24), .5, .3)
    wisps = fractal(41, (6, 12, 24, 48), .6, .3)
    image = Image.merge("RGBA", (warp_x, warp_y, billows, wisps))
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
