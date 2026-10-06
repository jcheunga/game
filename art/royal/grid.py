#!/usr/bin/env python3
"""Crop a region of a 1280x720 reference (or capture) and overlay a labelled coordinate grid.

  python3 art/royal/grid.py art/royal/ref/ui-07-warband-armory.png x y w h [scale] [out.png] [step]

Coordinates are canvas units (1280x720). Major lines every 50, minor every `step` (default 10).
"""
import sys
from PIL import Image, ImageDraw, ImageFont


def main():
    path = sys.argv[1]
    x, y, w, h = (int(v) for v in sys.argv[2:6])
    scale = float(sys.argv[6]) if len(sys.argv) > 6 else 3
    out = sys.argv[7] if len(sys.argv) > 7 else "/tmp/grid.png"
    step = int(sys.argv[8]) if len(sys.argv) > 8 else 10
    image = Image.open(path).convert("RGB")
    if image.size != (1280, 720):
        image = image.resize((1280, 720), Image.LANCZOS)
    crop = image.crop((x, y, x + w, y + h)).resize((int(w * scale), int(h * scale)), Image.NEAREST if scale >= 4 else Image.LANCZOS)
    draw = ImageDraw.Draw(crop, "RGBA")
    try:
        font = ImageFont.truetype("/System/Library/Fonts/Menlo.ttc", 12)
    except OSError:
        font = ImageFont.load_default()
    for gx in range((x // step) * step, x + w + 1, step):
        px = (gx - x) * scale
        major = gx % 50 == 0
        draw.line([(px, 0), (px, crop.height)], fill=(0, 255, 255, 150 if major else 50), width=1)
        if major:
            draw.text((px + 2, 2), str(gx), fill=(0, 255, 255, 255), font=font)
    for gy in range((y // step) * step, y + h + 1, step):
        py = (gy - y) * scale
        major = gy % 50 == 0
        draw.line([(0, py), (crop.width, py)], fill=(255, 0, 255, 150 if major else 50), width=1)
        if major:
            draw.text((2, py + 2), str(gy), fill=(255, 0, 255, 255), font=font)
    crop.save(out)


if __name__ == "__main__":
    main()
