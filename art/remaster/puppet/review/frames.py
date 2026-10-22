#!/usr/bin/env python3
"""Chosen frames at full resolution, a row per spec, cropped to their union box with the ground line drawn: the zoom
for anything a sheet shows small.

  art/remaster/puppet/run.sh art/remaster/puppet/review/frames.py out.png enemy_shade:attack player_spear:56,60
  ... frames.py out.png enemy_lich:death:.5,1 --scale 1.5 --src DIR --battle

Specs: id:i,j,k (absolute frame indices) · id:clip (the whole clip) · id:clip:f1,f2 (fractions through the clip).
--battle scales every unit to its true size on a phone (3 device px per battle px) instead of a fixed scale.
"""
import argparse
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
import common as C  # noqa: E402

ap = argparse.ArgumentParser()
ap.add_argument('out')
ap.add_argument('specs', nargs='+')
ap.add_argument('--scale', type=float, default=1.0)
ap.add_argument('--src', default=str(C.UNITS_OUT))
ap.add_argument('--battle', action='store_true')
a = ap.parse_args()

if a.battle:
    sys.path.insert(0, str(C.ROOT / 'art/remaster'))
    import density
    UNITS = {u['Id']: u for u in json.loads((C.ROOT / 'data/units.json').read_text())['Units']}

rows = []
for spec in a.specs:
    parts = spec.split(':')
    u = parts[0]
    m = C.meta(u, a.src)
    if len(parts) == 2 and parts[1] in m['animations']:
        idx = C.clip_range(u, parts[1], a.src)
    elif len(parts) == 3:
        rng = C.clip_range(u, parts[1], a.src)
        idx = [rng[min(len(rng) - 1, round(float(f) * (len(rng) - 1)))] for f in parts[2].split(',')]
    else:
        idx = [int(x) for x in parts[1].split(',')]
    fr = [C.frame(u, i, a.src) for i in idx]
    gy = m['anchorY'] * m['frameHeight']
    bb = C.union_box(fr, floor=gy)
    s = a.scale
    if a.battle:
        s = 3.0 * density.frame_canvas_width(UNITS[u], m['drawScale']) / m['frameWidth']
    row = []
    for f in fr:
        bg = C.on_bg(f)
        ImageDraw.Draw(bg).line([(0, gy), (f.width, gy)], fill=C.GROUND, width=2)
        t = bg.crop(bb)
        row.append(t.resize((max(1, int(t.width * s)), max(1, int(t.height * s))), Image.LANCZOS).convert('RGB'))
    r = Image.new('RGB', (sum(t.width + 8 for t in row), max(t.height for t in row)), C.PAGE)
    x = 0
    for t in row:
        r.paste(t, (x, r.height - t.height))
        x += t.width + 8
    rows.append(C.label(r, f'{C.short(u)} {",".join(map(str, idx))}'))
out = C.stack(rows)
out.save(a.out)
print(a.out, out.size)
