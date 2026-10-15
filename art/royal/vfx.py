#!/usr/bin/env python3
"""Install the painted spell-effect textures (art/royal/gen/vfx, from art/royal/jobs/10-spell-vfx.json) into
assets/fx/spells for scripts/combat/SpellVfx.cs.

  sigil_*   rune circles painted white on black -> white light with alpha from brightness (tinted in code)
  decal_*   ground marks on transparency        -> trimmed, centred, square
  rise_*    side-view eruptions on transparency -> trimmed, base on the bottom edge, square
  sheet_*   3x3 particle sheets                 -> p_<name> sprites; the "glow" sheet is keyed from black and
                                                   keeps its colours, the others are trimmed on their alpha

  python3 art/royal/vfx.py
"""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "art/royal/gen/vfx"
OUT = ROOT / "assets/fx/spells"
SHEETS = {
    "glow": ["flame", "flare", "ember", "star", "orb", "wisp", "spark_bolt", "rune", "swirl"],
    "matter": ["smoke", "soot", "dust", "shard", "shards", "rubble", "pebble", "leaf", "petal"],
    "bits": ["feather", "splash", "vine", "splinter", "ash", "shadow", "chain", "droplet", "grass"],
}


def square(image, size, bottom=False, margin=1.04):
    """Pads to a square around the content (or with the content standing on the bottom edge) and resizes."""
    box = image.getchannel("A").point(lambda a: 255 if a > 10 else 0).getbbox()
    if not box:
        return None
    image = image.crop(box)
    side = round(max(image.size) * margin)
    canvas = Image.new("RGBA", (side, side))
    y = side - image.height if bottom else (side - image.height) // 2
    canvas.paste(image, ((side - image.width) // 2, y))
    return canvas.resize((size, size), Image.LANCZOS)


def light_key(image, floor=30, ceiling=235, keep_colour=False):
    """Light painted on black (or on transparency) -> alpha from brightness. White light, or its own colour unpremultiplied."""
    flat = Image.new("RGBA", image.size, (0, 0, 0, 255))
    flat.alpha_composite(image.convert("RGBA"))
    rgb = flat.convert("RGB")
    out = Image.new("RGBA", rgb.size)
    pixels = []
    for r, g, b in rgb.get_flattened_data():
        peak = max(r, g, b)
        a = max(0.0, min(1.0, (peak - floor) / (ceiling - floor)))
        if keep_colour and peak > 0:
            scale = 255 / peak
            pixels.append((min(255, round(r * scale)), min(255, round(g * scale)), min(255, round(b * scale)), round(a * 255)))
        else:
            pixels.append((255, 255, 255, round(a * 255)))
    out.putdata(pixels)
    return out


def save(image, name):
    if image is None:
        print("empty", name)
        return
    image.save(OUT / f"{name}.png", optimize=True)
    print(name, image.size)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for path in sorted(SOURCE.glob("*.png")):
        name = path.stem
        image = Image.open(path).convert("RGBA")
        if name.startswith("sigil_"):
            save(square(light_key(image), 512, margin=1.0), name)
        elif name.startswith("decal_"):
            save(square(image, 512), name)
        elif name.startswith("rise_"):
            save(square(image, 512, bottom=True), name)
        elif name.startswith("sheet_"):
            kind = name[len("sheet_"):]
            w, h = image.width / 3, image.height / 3
            for index, part in enumerate(SHEETS[kind]):
                cell = image.crop((round(index % 3 * w), round(index // 3 * h), round((index % 3 + 1) * w), round((index // 3 + 1) * h)))
                if kind == "glow":
                    cell = light_key(cell, floor=14, ceiling=220, keep_colour=True)
                save(square(cell, 128, margin=1.1), "p_" + part)


if __name__ == "__main__":
    main()
