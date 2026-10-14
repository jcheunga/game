#!/usr/bin/env python3
"""Side-by-side lookdev sheets: reference cell | new render (on the reference's stone grey).

  python3 art/remaster/lookdev_compare.py player_defender[,..] [--tag x] [--out sheet.png]

Reads art/remaster/ref/<id>.png and artifacts/remaster/lookdev/<id>[-tag].png. With several ids,
also writes one combined sheet (default artifacts/remaster/lookdev/compare.png).
"""
import argparse
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
REF = ROOT / 'art/remaster/ref'
LOOK = ROOT / 'artifacts/remaster/lookdev'
GREY = (221, 217, 209)
H = 420


def fit(im, h):
    return im.resize((max(1, round(im.width * h / im.height)), h), Image.LANCZOS)


def trimmed(im):
    box = im.getchannel('A').point(lambda a: 255 if a > 8 else 0).getbbox()
    return im.crop(box) if box else im


def pair(ident, tag=''):
    render = LOOK / f'{ident}{"-" + tag if tag else ""}.png'
    if not render.exists():
        return None
    r = trimmed(Image.open(render).convert('RGBA'))
    r = fit(r, round(H * .86))
    panel = Image.new('RGB', (max(r.width + 40, 360), H), GREY)
    panel.paste(r, ((panel.width - r.width) // 2, H - r.height - 18), r)
    ref_path = REF / f'{ident}.png'
    if ref_path.exists():
        ref = fit(Image.open(ref_path).convert('RGB'), H)
        out = Image.new('RGB', (ref.width + panel.width + 6, H), (40, 36, 32))
        out.paste(ref, (0, 0))
        out.paste(panel, (ref.width + 6, 0))
    else:
        out = panel
    ImageDraw.Draw(out).text((8, 6), ident, fill=(30, 30, 30))
    return out


def main():
    p = argparse.ArgumentParser()
    p.add_argument('ids')
    p.add_argument('--tag', default='')
    p.add_argument('--out', default='')
    a = p.parse_args()
    rows = []
    for ident in a.ids.split(','):
        im = pair(ident, a.tag)
        if im is None:
            print('missing render', ident)
            continue
        im.save(LOOK / f'{ident}{"-" + a.tag if a.tag else ""}-compare.png')
        rows.append(im)
    if len(rows) > 1:
        cols = 2
        w = max(r.width for r in rows)
        sheet = Image.new('RGB', (w * cols, H * ((len(rows) + cols - 1) // cols)), (40, 36, 32))
        for i, r in enumerate(rows):
            sheet.paste(r, ((i % cols) * w, (i // cols) * H))
        out = Path(a.out) if a.out else LOOK / 'compare.png'
        sheet.save(out)
        print(out)


if __name__ == '__main__':
    main()
