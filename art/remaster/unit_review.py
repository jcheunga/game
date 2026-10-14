#!/usr/bin/env python3
"""Review sheet for rendered unit masters: icon, idle, walk, attack wind-up and contact, hit and the last
death frame of each unit, on the roster sheet's stone grey, with a ground line.

  python3 art/remaster/unit_review.py player_militia,enemy_walker [--out sheet.png] [--folder units]

Reads artifacts/remaster/<folder>/<id>/ (metadata.json, NNN.png, portrait.png); default output
artifacts/remaster/unit-review.png.
"""
import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / 'artifacts/remaster'
GREY = (221, 217, 209)
CELL = 220
# (label, clip, local frame; negative counts from the end)
PICKS = [('idle', 'idle', 0), ('walk', 'walk', 2), ('wind-up', 'attack', 2), ('contact', 'attack', 4),
         ('hit', 'hit', 0), ('death', 'death', -1)]


def row(ident, folder):
    d = REVIEW / folder / ident
    meta = json.loads((d / 'metadata.json').read_text())
    anims = meta['animations']
    cells = []
    icon = d / 'portrait.png'
    if icon.exists():
        im = Image.open(icon).convert('RGBA')
        im.thumbnail((CELL, CELL), Image.LANCZOS)
        cells.append(('icon', im, None))
    gy = meta['anchorY']   # ground, as a fraction of the frame from the top
    for label, clip, i in PICKS:
        c = anims.get(clip)
        if not c:
            continue
        f = c['start'] + (i % c['count'])
        p = d / f'{f:03}.png'
        if not p.exists():
            continue
        im = Image.open(p).convert('RGBA')
        s = CELL / max(im.width, im.height)
        im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
        cells.append((label, im, gy))
    out = Image.new('RGB', (CELL * len(cells) + 200, CELL + 20), GREY)
    dr = ImageDraw.Draw(out)
    dr.text((6, 6), ident, fill=(30, 30, 30))
    dr.text((6, 22), f"scale {meta['drawScale']:.2f}", fill=(80, 80, 80))
    for k, (label, im, gy) in enumerate(cells):
        x = 200 + k * CELL
        if gy is not None:
            y0 = 20 + round(gy * im.height) + (CELL - im.height) // 2
            dr.line([(x + 4, y0), (x + CELL - 4, y0)], fill=(170, 160, 150))
        out.paste(im, (x + (CELL - im.width) // 2, 20 + (CELL - im.height) // 2), im)
        dr.text((x + 4, 4), label, fill=(60, 60, 60))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('ids')
    ap.add_argument('--out', default=str(REVIEW / 'unit-review.png'))
    ap.add_argument('--folder', default='units')
    a = ap.parse_args()
    rows = []
    for ident in a.ids.split(','):
        try:
            rows.append(row(ident, a.folder))
        except FileNotFoundError:
            print('missing', ident)
    if not rows:
        return
    W = max(r.width for r in rows)
    sheet = Image.new('RGB', (W, sum(r.height for r in rows)), GREY)
    y = 0
    for r in rows:
        sheet.paste(r, (0, y))
        y += r.height
    sheet.save(a.out)
    print(a.out)


if __name__ == '__main__':
    main()
