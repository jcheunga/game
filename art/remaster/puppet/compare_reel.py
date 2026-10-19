#!/usr/bin/env python3
"""Before/after reel for unit animation work: per unit an animated WebP with the shipped atlas from a git revision on
the left and the current assets/units atlas on the right, each played the way the game plays it (the walk at the
cadence its stride and the unit's speed give, over ground marks scrolling at that speed, so a sliding foot shows),
then an attack and the death. Output: artifacts/remaster/compare_reel/<id>.webp

  python3 art/remaster/pack.py ... first;  python3 art/remaster/puppet/compare_reel.py ids [--rev HEAD]
"""
import argparse
import io
import json
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / 'art/remaster'))
import density  # noqa: E402

OUT = ROOT / 'artifacts/remaster/compare_reel'
UNITS = {u['Id']: u for u in json.loads((ROOT / 'data/units.json').read_text())['Units']}
FPS = 30
SCALE = 4.0          # output pixels per battle pixel
BG = (214, 206, 190)


def git_bytes(rev, rel):
    p = subprocess.run(['git', '-C', str(ROOT), 'show', f'{rev}:{rel}'], capture_output=True)
    return p.stdout if p.returncode == 0 else None


def load_sheet(ident, rev=None):
    """(meta, pages) of a packed unit atlas, from the working tree or a git revision."""
    rel = f'assets/units/{ident}'
    if rev:
        meta = json.loads(git_bytes(rev, rel + '.json'))
        pages, k = [], 0
        while True:
            b = git_bytes(rev, f'{rel}{"" if k == 0 else f"_{k}"}.png')
            if b is None:
                break
            pages.append(Image.open(io.BytesIO(b)).convert('RGBA'))
            k += 1
    else:
        meta = json.loads((ROOT / (rel + '.json')).read_text())
        pages, k = [], 0
        while (ROOT / f'{rel}{"" if k == 0 else f"_{k}"}.png').exists():
            pages.append(Image.open(ROOT / f'{rel}{"" if k == 0 else f"_{k}"}.png').convert('RGBA'))
            k += 1
    return meta, pages


def frame(meta, pages, i):
    """Logical frame i (frameWidth x frameHeight) of a packed atlas."""
    W, H = meta['frameWidth'], meta['frameHeight']
    im = Image.new('RGBA', (W, H))
    if meta.get('frames'):
        pg, x, y, w, h, ox, oy = meta['frames'][i]
        im.alpha_composite(pages[pg].crop((x, y, x + w, y + h)), (ox, oy))
    else:
        cols = pages[0].width // W
        im = pages[0].crop(((i % cols) * W, (i // cols) * H, (i % cols + 1) * W, (i // cols + 1) * H))
    return im


def timeline(meta, unit):
    """(clip, frame index, seconds into the reel, ground offset in battle px) at FPS for walk, attack, death."""
    A = meta['animations']
    canvas = density.frame_canvas_width(unit, meta['drawScale'])
    speed = float(unit.get('Speed', 30))
    out, t = [], 0.0
    w = A['walk']
    # the game: one cycle covers stride frame widths; frame time from that and the unit's speed, clamped
    cyc_world = w.get('stride', 0) * canvas
    fd = max(.025, min(.16, cyc_world / speed / w['count'])) if cyc_world > 0 else w['duration']
    for k in range(int(3.2 * FPS)):
        tt = k / FPS
        out.append(('walk', w['start'] + int(tt / fd) % w['count'], speed * tt))
    for name, reps, hold in (('attack', 2, 0), ('death', 1, 14)):
        c = A[name]
        for _ in range(reps):
            n = int(c['count'] * c['duration'] * FPS)
            for k in range(n):
                out.append((name, c['start'] + min(c['count'] - 1, int(k / FPS / c['duration'])), None))
        out += [(name, c['start'] + c['count'] - 1, None)] * hold
    return out, canvas, fd


def panel(meta, pages, unit, label):
    tl, canvas, fd = timeline(meta, unit)
    s = SCALE * canvas / meta['frameWidth']
    W, H = meta['frameWidth'], meta['frameHeight']
    ax, ay = meta['anchorX'] * W, meta['anchorY'] * H
    cache = {}
    frames = []
    for name, i, ground in tl:
        if i not in cache:
            f = frame(meta, pages, i)
            cache[i] = f.resize((max(1, int(W * s)), max(1, int(H * s))), Image.LANCZOS)
        frames.append((name, cache[i], ground))
    return frames, (ax * s, ay * s), fd


def compose(ident, rev):
    unit = UNITS[ident]
    before = panel(*load_sheet(ident, rev), unit, 'before')
    after = panel(*load_sheet(ident), unit, 'after')
    pw = int(max(b.width for _, b, _ in before[0] + after[0]) * 1.1)
    ph = int(max(b.height for _, b, _ in before[0] + after[0]) * 1.05) + 30
    n = max(len(before[0]), len(after[0]))
    out = []
    for k in range(n):
        im = Image.new('RGB', (pw * 2 + 6, ph), BG)
        d = ImageDraw.Draw(im)
        for j, (frames, (ax, ay), fd) in enumerate((before, after)):
            name, f, ground = frames[min(k, len(frames) - 1)]
            x0 = j * (pw + 6)
            gy = ph - 22
            d.line([(x0, gy), (x0 + pw, gy)], fill=(150, 135, 115), width=2)
            if ground is not None:
                # ground marks scroll back at the unit's speed: a planted foot keeps pace with them
                step = 12 * SCALE
                off = (ground * SCALE) % step
                xm = x0 - off
                while xm < x0 + pw:
                    if xm >= x0:
                        d.line([(xm, gy), (xm, gy + 6)], fill=(120, 105, 90), width=2)
                    xm += step
            im.paste(f, (int(x0 + pw / 2 - ax), int(gy - ay)), f)
            d.text((x0 + 6, 4), f"{'before' if j == 0 else 'after'} · {name}"
                   + (f" · {fd * meta_count(frames):.2f}s cycle" if name == 'walk' else ''), fill=(60, 50, 40))
        out.append(im)
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / f'{ident}.webp'
    out[0].save(path, save_all=True, append_images=out[1:], duration=int(1000 / FPS), loop=0, quality=78, method=4)
    return path


def meta_count(frames):
    return len({id(f) for n, f, _ in frames if n == 'walk'})


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('ids')
    ap.add_argument('--rev', default='HEAD')
    a = ap.parse_args()
    for i in a.ids.split(','):
        print(compose(i, a.rev), flush=True)
