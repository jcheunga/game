#!/usr/bin/env python3
"""How far each painting strayed from its model's colours: mean hue/saturation shift over the silhouette.

  python3 art/remaster/paint_drift.py [ids|all] [--limit 0.1]
"""
import argparse
import colorsys
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
PAINT = ROOT / 'artifacts/remaster/paint'


def drift(ident, n=64):
    d = PAINT / ident
    g = Image.open(d / 'guide.png').convert('RGB').resize((n, n))
    p = Image.open(d / 'painted.png').convert('RGB').resize((n, n))
    a = Image.open(d / 'guide_alpha.png').convert('L').resize((n, n))
    tot, cnt = 0.0, 0
    for (gr, gg, gb), (pr, pg, pb), al in zip(g.getdata(), p.getdata(), a.getdata()):
        if al < 200:
            continue
        h1, s1, v1 = colorsys.rgb_to_hsv(gr / 255, gg / 255, gb / 255)
        h2, s2, v2 = colorsys.rgb_to_hsv(pr / 255, pg / 255, pb / 255)
        dh = min(abs(h1 - h2), 1 - abs(h1 - h2)) * 2 * min(s1, s2)   # hue shift matters only where coloured
        tot += dh + abs(s1 - s2) * 0.5
        cnt += 1
    return tot / max(cnt, 1)


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('ids', nargs='?', default='all')
    ap.add_argument('--limit', type=float, default=0.0)
    a = ap.parse_args()
    ids = sorted(p.name for p in PAINT.iterdir() if (p / 'painted.png').exists()) if a.ids == 'all' else a.ids.split(',')
    rows = sorted(((drift(i), i) for i in ids), reverse=True)
    for v, i in rows:
        if v >= a.limit:
            print(f'{v:.3f} {i}')
