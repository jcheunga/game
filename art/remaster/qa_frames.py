#!/usr/bin/env python3
"""Automated checks over rendered unit masters (artifacts/remaster/units/<id>), before packing.

  python3 art/remaster/qa_frames.py [ids|all] [--folder units]

Flags, per unit:
  empty      a frame with nothing in it (a NaN pose, a lost rig)
  clipped    figure touching the frame edge
  float      idle frame 0: the figure's lowest pixel above the ground line (feet should touch it)
  sunk       idle frame 0: the figure far below the ground line
  hover      last death frame: the body (largest blob; dropped props are separate blobs) far above the ground line
  airborne   last death frame: a dropped prop still well above the ground line
  wide death props flung so far in the death clip that the frame (and its texture density) suffers
The ground line is the origin's projection: under the 22-degree camera a foot planted toward the camera draws a
little below it and a body lying farther back a little above it, so only large gaps are flagged.
"""
import argparse
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / 'artifacts/remaster'


def blobs(alpha, thresh=40):
    """Connected components of the opaque mask on a downsampled grid: list of (size, bbox)."""
    w, h = alpha.size
    k = max(1, w // 160)
    small = alpha.resize((w // k, h // k))
    W, H = small.size
    px = small.load()
    seen = bytearray(W * H)
    out = []
    for y in range(H):
        for x in range(W):
            if seen[y * W + x] or px[x, y] <= thresh:
                continue
            stack = [(x, y)]
            seen[y * W + x] = 1
            n, x0, y0, x1, y1 = 0, x, y, x, y
            while stack:
                cx, cy = stack.pop()
                n += 1
                x0, y0, x1, y1 = min(x0, cx), min(y0, cy), max(x1, cx), max(y1, cy)
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if 0 <= nx < W and 0 <= ny < H and not seen[ny * W + nx] and px[nx, ny] > thresh:
                        seen[ny * W + nx] = 1
                        stack.append((nx, ny))
            out.append((n, (x0 * k, y0 * k, (x1 + 1) * k, (y1 + 1) * k)))
    return sorted(out, reverse=True)


def check(ident, folder='units'):
    d = REVIEW / folder / ident
    meta = json.loads((d / 'metadata.json').read_text())
    W, H = meta['frameWidth'], meta['frameHeight']
    ground = meta['anchorY'] * H
    issues = []
    total = sum(c['count'] for c in meta['animations'].values())
    death = meta['animations'].get('death')
    dying = set(range(death['start'], death['start'] + death['count'])) if death else set()
    spans = {True: None, False: None}
    for i in range(total):
        p = d / f'{i:03}.png'
        if not p.exists():
            issues.append(f'missing {i}')
            continue
        a = Image.open(p).getchannel('A')
        box = a.point(lambda v: 255 if v > 8 else 0).getbbox()
        if not box:
            issues.append(f'empty {i}')
            continue
        if box[0] == 0 or box[1] == 0 or box[2] == W or box[3] == H:
            issues.append(f'clipped {i}')
        k = i in dying
        sp = spans[k]
        spans[k] = box if sp is None else (min(sp[0], box[0]), min(sp[1], box[1]), max(sp[2], box[2]), max(sp[3], box[3]))
    # props flung far in the death clip widen the frame (and cost every frame texture density)
    if spans[True] and spans[False]:
        live = spans[False][2] - spans[False][0]
        allw = max(spans[True][2], spans[False][2]) - min(spans[True][0], spans[False][0])
        if allw > 2.0 * live:   # a body lying down is naturally ~1.6x as wide as standing
            issues.append(f'wide death x{allw / live:.2f}')
    idle = meta['animations']['idle']['start']
    a = Image.open(d / f'{idle:03}.png').getchannel('A')
    box = a.point(lambda v: 255 if v > 60 else 0).getbbox()
    if box:
        gap = (ground - box[3]) / H
        if gap > 0.02:
            issues.append(f'float idle {gap:.3f}')
        elif gap < -0.06:
            issues.append(f'sunk idle {-gap:.3f}')
    if death:
        last = death['start'] + death['count'] - 1
        a = Image.open(d / f'{last:03}.png').getchannel('A')
        bl = blobs(a)
        if bl:
            body = bl[0][1]
            gap = (ground - body[3]) / H
            if gap > 0.07:
                issues.append(f'hover death {gap:.3f}')
            # loose props (crowns, bombs, skulls) should have landed by the last frame
            for n, box in bl[1:]:
                if n < 6:
                    continue
                g = (ground - box[3]) / H
                if g > 0.08:
                    issues.append(f'airborne prop {g:.3f}')
                    break
    return issues


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('ids', nargs='?', default='all')
    ap.add_argument('--folder', default='units')
    a = ap.parse_args()
    if a.ids == 'all':
        ids = [u['Id'] for u in json.loads((ROOT / 'data/units.json').read_text())['Units']]
        ids = [i for i in ids if (REVIEW / a.folder / i / 'complete.json').exists()]
    else:
        ids = a.ids.split(',')
    bad = 0
    for ident in ids:
        try:
            issues = check(ident, a.folder)
        except FileNotFoundError as e:
            issues = [f'unreadable: {e}']
        if issues:
            bad += 1
            print(ident, '|', ', '.join(issues))
    print(f'{len(ids)} units checked, {bad} with issues')


if __name__ == '__main__':
    main()
