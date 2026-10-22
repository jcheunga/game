#!/usr/bin/env python3
"""Before/after stills: the shipped atlas at a git revision (top row) over the working tree's (bottom row), the same
frames of a clip, the ground line red. For review pages and for proving a fix.

  art/remaster/puppet/run.sh art/remaster/puppet/review/stills.py OUTDIR --rev 25152403 \\
      player_militia:walk:0,.2,.3,.5,.7,.8 enemy_shade:attack:0,.13,.27

Each spec writes OUTDIR/<id>_<clip>.webp. Reads packed atlases (assets/units), so run pack.py apply first.
"""
import argparse
import sys
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parent))
import common as C  # noqa: E402
import compare_reel as R  # noqa: E402

ap = argparse.ArgumentParser()
ap.add_argument('outdir')
ap.add_argument('specs', nargs='+')
ap.add_argument('--rev', default='HEAD')
ap.add_argument('--height', type=int, default=300)
a = ap.parse_args()
Path(a.outdir).mkdir(parents=True, exist_ok=True)


def row(meta, pages, clip, fr, H):
    A = meta['animations'][clip]
    h = meta['frameHeight']
    ims = [R.frame(meta, pages, A['start'] + min(A['count'] - 1, round(f * (A['count'] - 1)))) for f in fr]
    gy = meta['anchorY'] * h
    bb = C.union_box(ims, pad=6, floor=gy + 8)
    s = H / (bb[3] - bb[1])
    tiles = []
    for im in ims:
        bg = C.on_bg(im)
        ImageDraw.Draw(bg).line([(0, gy), (im.size[0], gy)], fill=(196, 52, 40), width=3)
        t = bg.crop(bb)
        tiles.append(t.resize((int(t.width * s), H), Image.LANCZOS))
    o = Image.new('RGB', (sum(t.width for t in tiles) + 8 * (len(tiles) - 1), H), C.BG)
    x = 0
    for t in tiles:
        o.paste(t, (x, 0))
        x += t.width + 8
    return o


for spec in a.specs:
    u, clip, fr = spec.split(':')
    fr = [float(f) for f in fr.split(',')]
    rows = [row(*R.load_sheet(u, a.rev), clip, fr, a.height), row(*R.load_sheet(u), clip, fr, a.height)]
    o = Image.new('RGB', (max(r.width for r in rows), a.height * 2 + 34), (236, 238, 236))
    for j, r in enumerate(rows):
        o.paste(r, (0, j * (a.height + 17) + 17))
    p = Path(a.outdir) / f'{u}_{clip}.webp'
    o.save(p, quality=86, method=5)
    print(p, o.size)
