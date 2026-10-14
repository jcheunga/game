#!/usr/bin/env python3
"""Review reel for the 2D puppets: per unit an animated WebP of idle, walk, attack, hit and death (on the sheet's grey,
with the ground line), plus its icon, collected in artifacts/remaster/puppet_reel/.

  python3 art/remaster/puppet/reel.py [ids|all]
"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[3]
SRC = ROOT / 'artifacts/remaster/puppet_units'
OUT = ROOT / 'artifacts/remaster/puppet_reel'
H = int(__import__("os").environ.get("REEL_H", "220"))
BG = (221, 217, 209)
CLIPS = ('idle', 'walk', 'attack', 'hit', 'death')


def reel(ident):
    d = SRC / ident
    m = json.loads((d / 'metadata.json').read_text())
    clips = [(c, m['animations'][c]) for c in CLIPS if c in m['animations']]
    frames = []
    for name, c in clips:
        reps = 2 if name in ('idle', 'walk') else 1
        idx = list(range(c['start'], c['start'] + c['count'])) * reps
        if name == 'death':
            idx += [idx[-1]] * 6
        frames += [(name, i, c['duration']) for i in idx]
    ims = {i: Image.open(d / f'{i:03}.png').convert('RGBA') for _, i, _ in frames}
    box = None
    for im in ims.values():
        b = im.getchannel('A').getbbox()
        if b:
            box = b if box is None else (min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
    W0, H0 = next(iter(ims.values())).size
    gy = int(m['anchorY'] * H0)
    box = (max(0, box[0] - 8), max(0, box[1] - 8), min(W0, box[2] + 8), min(H0, max(box[3], gy) + 8))
    s = H / (box[3] - box[1])
    w = int((box[2] - box[0]) * s)
    out, durs = [], []
    for name, i, dur in frames:
        f = Image.new('RGBA', (w, H + 18), BG + (255,))
        fr = ims[i].crop(box).resize((w, H), Image.LANCZOS)
        dr = ImageDraw.Draw(f)
        y = 18 + int((gy - box[1]) * s)
        dr.line([(0, y), (w, y)], fill=(170, 160, 150, 255))
        f.alpha_composite(fr, (0, 18))
        dr.text((4, 3), name, fill=(60, 50, 40, 255))
        out.append(f.convert('RGB'))
        durs.append(int(dur * 1000))
    OUT.mkdir(parents=True, exist_ok=True)
    out[0].save(OUT / f'{ident}.webp', save_all=True, append_images=out[1:], duration=durs, loop=0, quality=int(__import__('os').environ.get('REEL_Q', '80')), method=4)
    if (d / 'portrait.png').exists():
        Image.open(d / 'portrait.png').convert('RGBA').resize((160, 160), Image.LANCZOS).save(OUT / f'{ident}_icon.png')
    return w


if __name__ == '__main__':
    arg = sys.argv[1] if len(sys.argv) > 1 else 'all'
    ids = [p.name for p in sorted(SRC.iterdir()) if (p / 'complete.json').exists()] if arg == 'all' else arg.split(',')
    for i in ids:
        print(i, reel(i), flush=True)
