#!/usr/bin/env python3
"""Tone statistics of figure pixels: reference cells vs lookdev renders (mean/std luminance, saturation,
dark/bright fractions), to calibrate lighting and materials against output/units.png.

  python3 art/remaster/look_stats.py player_spear,enemy_walker --tag v3
  python3 art/remaster/look_stats.py ref --frames      # every reference cell vs its shipped stance frame
"""
import argparse
import colorsys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]


def chroma(p):
    """CIELAB C* of an sRGB pixel (perceptual colourfulness; HSV saturation overstates dark pixels)."""
    import math

    def lin(c):
        c /= 255
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    r, g, b = (lin(x) for x in p)
    X = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047
    Y = 0.2126 * r + 0.7152 * g + 0.0722 * b
    Z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883
    f = lambda t: t ** (1 / 3) if t > 0.008856 else 7.787 * t + 16 / 116
    return math.hypot(500 * (f(X) - f(Y)), 200 * (f(Y) - f(Z)))


def stats(pixels):
    n = len(pixels)
    lum = [0.2126 * r + 0.7152 * g + 0.0722 * b for r, g, b in pixels]
    mean = sum(lum) / n
    std = (sum((x - mean) ** 2 for x in lum) / n) ** .5
    sat = sum(colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)[1] for r, g, b in pixels) / n
    dark = sum(1 for x in lum if x < 60) / n
    bright = sum(1 for x in lum if x > 200) / n
    return mean, std, sat, dark, bright, sum(chroma(p) for p in pixels) / n


def ref_pixels(path, bg=(221, 217, 209), tol=28):
    im = Image.open(path).convert('RGB')
    im.thumbnail((240, 240))
    out = []
    for p in im.getdata():
        if sum(abs(a - b) for a, b in zip(p, bg)) > tol * 3:
            out.append(p)
    return out


def render_pixels(path):
    im = Image.open(path).convert('RGBA')
    im.thumbnail((240, 240))
    return [p[:3] for p in im.getdata() if p[3] > 200]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('ids')
    ap.add_argument('--tag', default='v3')
    ap.add_argument('--frames', action='store_true',
                    help='measure the shipped stance frame (artifacts/remaster/units/<id>/000.png) instead of lookdev')
    a = ap.parse_args()
    ids = [p.stem for p in sorted((ROOT / 'art/remaster/ref').glob('*.png'))] if a.ids == 'ref' else a.ids.split(',')
    print(f'{"unit":28} {"src":4} {"mean":>6} {"std":>6} {"sat":>6} {"dark":>6} {"brt":>6} {"C*":>6}')
    deltas = []
    for ident in ids:
        ref = ROOT / 'art/remaster/ref' / f'{ident}.png'
        ren = (ROOT / 'artifacts/remaster/units' / ident / '000.png') if a.frames else \
            (ROOT / 'artifacts/remaster/lookdev' / f'{ident}-{a.tag}.png')
        row = {}
        for label, px in (('ref', ref_pixels(ref) if ref.exists() else None),
                          ('new', render_pixels(ren) if ren.exists() else None)):
            if px:
                row[label] = stats(px)
                m, s, sat, d, b, c = row[label]
                print(f'{ident:28} {label:4} {m:6.1f} {s:6.1f} {sat:6.3f} {d:6.3f} {b:6.3f} {c:6.1f}')
        if len(row) == 2:
            deltas.append([n - r for n, r in zip(row['new'], row['ref'])])
    if deltas:
        k = len(deltas)
        mean = [sum(d[i] for d in deltas) / k for i in range(6)]
        absm = [sum(abs(d[i]) for d in deltas) / k for i in range(6)]
        print(f'{"mean delta (new-ref)":28} {"":4} {mean[0]:6.1f} {mean[1]:6.1f} {mean[2]:6.3f} {mean[3]:6.3f} {mean[4]:6.3f} {mean[5]:6.1f}')
        print(f'{"mean |delta|":28} {"":4} {absm[0]:6.1f} {absm[1]:6.1f} {absm[2]:6.3f} {absm[3]:6.3f} {absm[4]:6.3f} {absm[5]:6.1f}')


if __name__ == '__main__':
    main()
