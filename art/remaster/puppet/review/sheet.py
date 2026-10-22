#!/usr/bin/env python3
"""Contact sheet of one clip for many units: a row per unit, evenly sampled frames (first, last and the contact
frame, outlined red), the ground line drawn. The fastest whole-roster eyeball.

  art/remaster/puppet/run.sh art/remaster/puppet/review/sheet.py walk out.png all
  ... sheet.py death out.png enemy_lich,player_spear --cols 9 --cell 220 [--src DIR]

Split 111 units into sheets of ~14 rows to read them at a useful size.
"""
import argparse
import sys
from pathlib import Path

from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
import common as C  # noqa: E402

ap = argparse.ArgumentParser()
ap.add_argument('clip')
ap.add_argument('out')
ap.add_argument('ids', nargs='+')
ap.add_argument('--cols', type=int, default=7)
ap.add_argument('--cell', type=int, default=170)
ap.add_argument('--src', default=str(C.UNITS_OUT))
a = ap.parse_args()
ids = C.ids_arg(a.ids)
cell = a.cell
img = Image.new('RGB', (cell * a.cols + 120, cell * len(ids)), C.BG)
d = ImageDraw.Draw(img)
for r, u in enumerate(ids):
    m = C.meta(u, a.src)
    clip = m['animations'].get(a.clip)
    if not clip:
        d.text((4, r * cell + 4), f'{u} (no {a.clip})', fill=(0, 0, 0))
        continue
    n = clip['count']
    idx = sorted({round(k * (n - 1) / max(1, a.cols - 1)) for k in range(a.cols)})
    if 'contactFrame' in clip and clip['contactFrame'] not in idx:
        idx = sorted(idx[:-1] + [clip['contactFrame']])
    fr = [C.frame(u, clip['start'] + i, a.src) for i in idx]
    gy = m['anchorY'] * m['frameHeight']
    bb = C.union_box(fr, floor=gy)
    for c, (i, f) in enumerate(zip(idx, fr)):
        t = f.crop(bb)
        s = min((cell - 6) / t.width, (cell - 12) / t.height)
        t = t.resize((max(1, int(t.width * s)), max(1, int(t.height * s))), Image.LANCZOS)
        x, y = 120 + c * cell + (cell - t.width) // 2, r * cell + cell - 4 - t.height
        img.paste(t, (x, y), t)
        g = y + int((gy - bb[1]) * s)
        d.line([(120 + c * cell, g), (120 + c * cell + cell, g)], fill=C.GROUND)
        if i == clip.get('contactFrame'):
            d.rectangle([120 + c * cell, r * cell, 120 + c * cell + cell - 1, r * cell + cell - 1], outline=C.GROUND)
    d.text((4, r * cell + 4), C.short(u), fill=(0, 0, 0))
img.save(a.out)
print(a.out, img.size)
