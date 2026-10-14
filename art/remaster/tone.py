#!/usr/bin/env python3
"""Roster-sheet tone: one curve, fitted so the shipped frames' tonal range matches the reference cells
(art/remaster/ref, crops of output/units.png), applied to every unit's frames and icon.

  python3 art/remaster/tone.py fit                 # writes art/remaster/tone_curve.json from the 16 reference units
  python3 art/remaster/tone.py preview player_spear,enemy_walker [--out sheet.png]
  python3 art/remaster/tone.py apply all           # artifacts/remaster/units/<id>/NNN.png + portrait.png, once each

The procedural renders the paintings are made from are darker than the sheet (paintings keep their model's values),
so the curve is a gamma lift plus a gentle contrast S, fitted to the reference figures' mean luminance and spread.
Black and white stay put, so glows and deep shadows keep their punch. Then bright, low-colour areas (linen, bone)
are warmed toward the sheet's cream (its whites are warm, ours rendered near-white); glows are masked out.
Colour is muted lightly toward the sheet's CIELAB chroma (13.7, ours 23.8); a full match greyed the units out at
battle size against the zone backdrops. One curve for every unit keeps the roster consistent with itself. Applying it records units/<id>/tone.json, so a unit is never toned twice.
"""
import argparse
import json
import sys
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
from look_stats import ref_pixels  # noqa: E402

UNITS = ROOT / 'artifacts/remaster/units'
CURVE = HERE / 'tone_curve.json'


def lum(p):
    return 0.2126 * p[0] + 0.7152 * p[1] + 0.0722 * p[2]


def frame_pixels(ident):
    im = Image.open(UNITS / ident / '000.png').convert('RGBA')
    im.thumbnail((240, 240))
    return [p[:3] for p in im.getdata() if p[3] > 200]


def cdf(values):
    hist = [0] * 256
    for v in values:
        hist[min(255, max(0, int(v)))] += 1
    total, run, out = len(values), 0, []
    for h in hist:
        run += h
        out.append(run / total)
    return out


def curve(g, c):
    """Gamma g, then contrast c about mid-grey (a gentle S): 0 and 255 stay put."""
    out = []
    for v in range(256):
        x = (v / 255) ** g
        y = 0.5 + c * (x - 0.5)
        # roll off the ends smoothly so contrast never clips glows or blacks
        y = x + (y - x) * (4 * x * (1 - x))
        out.append(round(255 * min(1.0, max(0.0, y))))
    for v in range(1, 256):
        out[v] = max(out[v], out[v - 1])
    return out


def fit():
    """Gamma and contrast fitted so the reference units' stance frames match the sheet's mean luminance and spread
    (a histogram match is too harsh: the reference crops lose their light-grey pixels to background removal)."""
    ids = [p.stem for p in sorted((HERE / 'ref').glob('*.png'))]
    ref_l, ours = [], []
    for i in ids:
        ref_l += [lum(p) for p in ref_pixels(HERE / 'ref' / f'{i}.png')]
        ours += frame_pixels(i)
    ours = ours[::3]
    rm = sum(ref_l) / len(ref_l)
    rs = (sum((x - rm) ** 2 for x in ref_l) / len(ref_l)) ** .5

    def stats_of(lut):
        ls = [lum([lut[c] for c in p]) for p in ours]
        m = sum(ls) / len(ls)
        return m, (sum((x - m) ** 2 for x in ls) / len(ls)) ** .5
    best = None
    for g in [0.55 + 0.025 * k for k in range(15)]:
        for c in [1.0 + 0.05 * k for k in range(13)]:
            lut = curve(g, c)
            m, sd = stats_of(lut)
            err = ((m - rm) / 10) ** 2 + ((sd - rs) / 10) ** 2
            if best is None or err < best[0]:
                best = (err, g, c, m, sd, lut)
    _, g, c, m, sd, lut = best
    import colorsys
    ref_px = [p for i in ids for p in ref_pixels(HERE / 'ref' / f'{i}.png')]
    rsat = sum(colorsys.rgb_to_hsv(*(x / 255 for x in p))[1] for p in ref_px) / len(ref_px)

    def sat_after(k):
        tot = 0.0
        for p in ours:
            q = desat([lut[ch] for ch in p], k)
            tot += colorsys.rgb_to_hsv(*(x / 255 for x in q))[1]
        return tot / len(ours)
    k = 1.0
    for cand in [1.0 - 0.025 * j for j in range(13)]:          # down to 0.7: never grey the roster out
        k = cand
        if sat_after(cand) <= rsat:
            break
    CURVE.write_text(json.dumps({'lut': lut, 'gamma': round(g, 3), 'contrast': round(c, 3), 'saturation': k,
                                 'units': ids}) + '\n')
    print(f'gamma {g:.3f} contrast {c:.2f} saturation {k:.3f}: mean {m:.1f} (ref {rm:.1f}), std {sd:.1f} '
          f'(ref {rs:.1f}), sat {sat_after(k):.3f} (ref {rsat:.3f})')
    return lut


def desat(rgb, k):
    l = lum(rgb)
    return [max(0, min(255, round(l + k * (x - l)))) for x in rgb]


def load():
    d = json.loads(CURVE.read_text())
    return d['lut'], d.get('saturation', 1.0)


def tone(im, curve):
    lut, k = curve
    im = im.convert('RGBA')
    r, g, b, a = im.split()
    rgb = Image.merge('RGB', (r.point(lut), g.point(lut), b.point(lut)))
    if k < 1.0:
        grey = rgb.convert('L').convert('RGB')
        rgb = Image.blend(grey, rgb, k)
    out = rgb.convert('RGBA')
    out.putalpha(a)
    return out


WARM = (0.97, 0.90, 0.76)   # the sheet's whites are warm cream and tan bone, not white


def warm_whites(im, tint=WARM, lo=150, f=2.0):
    """Bright, low-colour areas (linen, bone, pale cloth) pulled toward the sheet's warm cream; glows and coloured
    highlights (eyes, fire, teal, gold) are masked out by their colour and keep their brightness."""
    from PIL import ImageChops
    im = im.convert('RGBA')
    a = im.getchannel('A')
    rgb = im.convert('RGB')
    r, g, b = rgb.split()
    w = Image.merge('RGB', (r.point(lambda v: round(v * tint[0])), g.point(lambda v: round(v * tint[1])),
                            b.point(lambda v: round(v * tint[2]))))
    mx = ImageChops.lighter(ImageChops.lighter(r, g), b)
    mn = ImageChops.darker(ImageChops.darker(r, g), b)
    low = ImageChops.subtract(mx, mn).point(lambda c: max(0, 255 - int(c * f)))
    bright = rgb.convert('L').point(lambda v: 0 if v < lo else min(255, int((v - lo) * 255 / (225 - lo))))
    out = Image.composite(w, rgb, ImageChops.multiply(low, bright)).convert('RGBA')
    out.putalpha(a)
    return out


CHROMA = 0.85   # the sheet's colour is dusty (CIELAB chroma 13.7, ours 23.8). 0.58 matches it on paper but units read
#                 grey against the painted zone backdrops at battle size; 0.85 moves toward it and still reads


def mute(im, k=CHROMA, vivid=110):
    """Pull colour toward grey to the sheet's chroma; vivid pixels (max-min > `vivid`: glowing eyes, fire, plague
    light) keep theirs, as the sheet's glows stay saturated."""
    from PIL import ImageChops
    im = im.convert('RGBA')
    a = im.getchannel('A')
    rgb = im.convert('RGB')
    r, g, b = rgb.split()
    muted = Image.blend(rgb.convert('L').convert('RGB'), rgb, k)
    mx = ImageChops.lighter(ImageChops.lighter(r, g), b)
    mn = ImageChops.darker(ImageChops.darker(r, g), b)
    keep = ImageChops.subtract(mx, mn).point(lambda c: 0 if c < vivid else min(255, (c - vivid) * 6))
    out = Image.composite(rgb, muted, keep).convert('RGBA')
    out.putalpha(a)
    return out


def unmute(im, k=CHROMA, vivid=110):
    """Undo mute(): pixels it muted fully (now chroma < vivid*k) are pushed back out by 1/k; the few it blended
    partially get the mean factor; vivid ones were left alone."""
    from PIL import ImageChops
    im = im.convert('RGBA')
    a = im.getchannel('A')
    rgb = im.convert('RGB')
    r, g, b = rgb.split()
    grey = rgb.convert('L').convert('RGB')
    full_ = Image.blend(grey, rgb, 1 / k)
    part = Image.blend(grey, rgb, 2 / (1 + k))
    c = ImageChops.subtract(ImageChops.lighter(ImageChops.lighter(r, g), b), ImageChops.darker(ImageChops.darker(r, g), b))
    lo = round(vivid * k)
    hi = vivid + 255 // 6
    m_full = c.point(lambda v: 255 if v < lo else 0)
    m_part = c.point(lambda v: 255 if lo <= v < hi else 0)
    out = Image.composite(part, rgb, m_part)
    out = Image.composite(full_, out, m_full).convert('RGBA')
    out.putalpha(a)
    return out


VERSION = 4   # 1: curve; 2: + warm whites; 4: + light chroma mute (3 was a 0.58 mute, withdrawn: read grey in battle)


def full(im, curve):
    return mute(warm_whites(tone(im, curve)))


def apply_unit(ident, curve):
    d = UNITS / ident
    marker = d / 'tone.json'
    done = json.loads(marker.read_text()).get('version', 1) if marker.exists() else 0
    if done == VERSION:
        return 'already toned'
    steps = {0: lambda im: full(im, curve), 1: lambda im: mute(warm_whites(im)), 2: lambda im: mute(im),
             3: lambda im: mute(unmute(im, 0.58))}
    step = steps[done]
    meta = json.loads((d / 'metadata.json').read_text())
    total = sum(c['count'] for c in meta['animations'].values())
    for i in range(total):
        p = d / f'{i:03}.png'
        step(Image.open(p)).save(p)
    if (d / 'portrait.png').exists():
        step(Image.open(d / 'portrait.png')).save(d / 'portrait.png')
    marker.write_text(json.dumps({'curve': str(CURVE.relative_to(ROOT)), 'version': VERSION, 'frames': total}) + '\n')
    return f'{total} frames' + ({1: ' (warm whites, chroma)', 2: ' (chroma)'}.get(done, ''))


def preview(ids, out, lut):
    rows = []
    for i in ids:
        cells = []
        ref = HERE / 'ref' / f'{i}.png'
        if ref.exists():
            cells.append(Image.open(ref).convert('RGBA'))
        fr = Image.open(UNITS / i / '000.png').convert('RGBA')
        box = fr.getchannel('A').getbbox()
        fr = fr.crop(box)
        cells += [fr, full(fr, lut)]
        h = 300
        cells = [c.resize((max(1, round(c.width * h / c.height)), h)) for c in cells]
        row = Image.new('RGBA', (sum(c.width for c in cells) + 20 * len(cells), h), (221, 217, 209, 255))
        x = 0
        for c in cells:
            row.alpha_composite(c, (x, 0))
            x += c.width + 20
        rows.append(row)
    W = max(r.width for r in rows)
    sheet = Image.new('RGB', (W, sum(r.height + 10 for r in rows)), (221, 217, 209))
    y = 0
    for r in rows:
        sheet.paste(r, (0, y), r)
        y += r.height + 10
    sheet.save(out)
    print(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('action', choices=['fit', 'preview', 'apply'])
    ap.add_argument('ids', nargs='?', default='all')
    ap.add_argument('--out', default=str(ROOT / 'artifacts/remaster/tone-preview.png'))
    a = ap.parse_args()
    if a.action == 'fit':
        fit()
        return
    lut = load()
    ids = [p.name for p in sorted(UNITS.iterdir()) if (p / 'complete.json').exists()] if a.ids == 'all' else a.ids.split(',')
    if a.action == 'preview':
        preview(ids, a.out, lut)
    else:
        for i in ids:
            print('TONE', i, apply_unit(i, lut), flush=True)


if __name__ == '__main__':
    main()
