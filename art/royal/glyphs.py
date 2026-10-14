#!/usr/bin/env python3
"""Cut the painted glyph sheets (art/royal/gen/glyphs, from art/royal/jobs/09-glyphs.json) into kit icons.

Each sheet is a 3x3 grid of cream glyphs on black. A cell becomes assets/ui/royal/kit/<name>.png: alpha from
brightness and one flat cream ink, like the "key-light" cuts in cuts.py, at twice the concept density so it
stays sharp on phones.

  python3 art/royal/glyphs.py [name ...]
"""
import json
import sys
from pathlib import Path
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
JOBS = ROOT / "art/royal/jobs/09-glyphs.json"
OUT = ROOT / "assets/ui/royal/kit"
INK = (250, 238, 192)
SIDE = 52  # the concept glyphs are about 26 px on their longer side
WEIGHT = 1 / 40  # stroke thickening, as a fraction of the glyph, so thin lines survive at 20 px like the concept glyphs


def cut(cell):
    gray = cell.convert("L")
    # The glyph's box is its cream fill; dark outlines and stray specks stay outside it.
    box = gray.point(lambda v: 255 if v > 140 else 0).getbbox()
    if not box:
        return None
    pad = max(2, round(max(box[2] - box[0], box[3] - box[1]) * 0.04))
    box = (max(0, box[0] - pad), max(0, box[1] - pad), min(cell.width, box[2] + pad), min(cell.height, box[3] + pad))
    gray = gray.crop(box)
    values = gray.get_flattened_data()
    fill = sum(1 for v in values if v > 140)
    # Some sheets ring each glyph with a wide dark-grey stroke and a speckled light rim outside it. Those keep
    # only the cream fill (the stroke's interior lines are the glyph's detail); plain glyphs are thickened.
    outlined = sum(1 for v in values if 30 < v < 90) > 0.2 * fill
    ordered = sorted(values)
    lo = 120 if outlined else max(ordered[int(len(ordered) * 0.2)] + 12, 96)
    hi = max(lo + 1, ordered[int(len(ordered) * 0.995)])
    alpha = gray.point(lambda v: max(0, min(255, round((v - lo) * 255 / (hi - lo)))))
    alpha = alpha.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))  # drops specks, keeps strokes
    grow = round(max(alpha.size) * WEIGHT) | 1
    if not outlined and grow > 1:
        alpha = alpha.filter(ImageFilter.MaxFilter(grow))
    scale = SIDE / max(alpha.size)
    alpha = alpha.resize((max(1, round(alpha.width * scale)), max(1, round(alpha.height * scale))), Image.LANCZOS)
    glyph = Image.new("RGBA", alpha.size, INK + (0,))
    glyph.putalpha(alpha)
    return glyph


def main():
    wanted = set(sys.argv[1:])
    for job in json.loads(JOBS.read_text()):
        path = ROOT / job["out"]
        if not path.exists():
            print("missing", path.name)
            continue
        sheet = Image.open(path).convert("RGB")
        w, h = sheet.width / 3, sheet.height / 3
        for index, name in enumerate(job["glyphs"]):
            if wanted and name not in wanted:
                continue
            col, row = index % 3, index // 3
            glyph = cut(sheet.crop((round(col * w), round(row * h), round((col + 1) * w), round((row + 1) * h))))
            if glyph is None:
                print("empty", name)
                continue
            glyph.save(OUT / f"{name}.png", optimize=True)
            print(name, glyph.size)


if __name__ == "__main__":
    main()
