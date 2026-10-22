#!/usr/bin/env python3
"""A unit's painting with its landmarks (joints, weapon grip and tip, off-hand item) on a labelled grid, in TRUE
painting coordinates: the view to check and place joints by eye before setlm.py.

  art/remaster/puppet/run.sh art/remaster/puppet/review/landmarks_view.py enemy_furnace_knight out.png
  ... landmarks_view.py enemy_furnace_knight out.png --legs      (zoom on the legs, finer grid)

Things to check: knees on the knee joint (Codex often puts them on boot cuffs), hips under the pelvis (--anatomy
solves them from the knees), staff/weapon tips on the HEAD of the weapon (Codex often marks a staff's foot),
offhand_item only on something held (not a belt lantern), wrists at the wrist.
"""
import argparse
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
import common as C  # noqa: E402

ap = argparse.ArgumentParser()
ap.add_argument('ident')
ap.add_argument('out')
ap.add_argument('--legs', action='store_true')
a = ap.parse_args()
d = C.source_dir(a.ident)
lm = json.loads((d / 'landmarks.json').read_text())
im = C.on_bg(Image.open(d / 'master.png').convert('RGBA'), (225, 222, 215))
dr = ImageDraw.Draw(im)
step = 16 if a.legs else 32
for v in range(0, im.width, step):
    major = v % (step * 4) == 0
    dr.line([(v, 0), (v, im.height)], fill=(200, 120, 220) if major else (232, 200, 236), width=1)
    dr.line([(0, v), (im.width, v)], fill=(200, 120, 220) if major else (232, 200, 236), width=1)
    if major:
        dr.text((v + 2, 2), str(v), fill=(120, 0, 140))
        dr.text((2, v + 2), str(v), fill=(120, 0, 140))
chains = [('R', (255, 60, 60)), ('L', (60, 110, 255))]
for s_, col in chains:
    arm = [lm.get(f'{k}_{s_}') for k in ('shoulder', 'elbow', 'wrist')]
    leg = [lm.get(f'{k}_{s_}') for k in ('hip', 'knee', 'ankle', 'toe')]
    for ch in (arm, leg):
        pts = [tuple(p) for p in ch if p]
        if len(pts) > 1:
            dr.line(pts, fill=col, width=3)
spine = [tuple(lm[k]) for k in ('head_top', 'chin', 'neck', 'chest', 'pelvis') if lm.get(k)]
if len(spine) > 1:
    dr.line(spine, fill=(255, 255, 255), width=3)
if lm.get('weapon_grip') and lm.get('weapon_tip'):
    dr.line([tuple(lm['weapon_grip']), tuple(lm['weapon_tip'])], fill=(255, 200, 0), width=3)
for k, v in lm.items():
    if isinstance(v, list) and len(v) == 2 and all(isinstance(x, (int, float)) for x in v):
        col = (0, 160, 0) if k == 'offhand_item' else (255, 200, 0) if k.startswith('weapon') else \
            (255, 60, 60) if k.endswith('_R') else (60, 110, 255) if k.endswith('_L') else (255, 255, 255)
        x, y = v
        dr.ellipse((x - 7, y - 7, x + 7, y + 7), outline=col, width=3)
        dr.text((x + 9, y - 6), f'{k} {x},{y}' if a.legs else k, fill=(0, 0, 0))
if a.legs:
    ys = [lm[k][1] for k in lm if k.startswith(('hip_', 'knee_', 'ankle_', 'toe_')) and lm.get(k)]
    xs = [lm[k][0] for k in lm if k.startswith(('hip_', 'knee_', 'ankle_', 'toe_')) and lm.get(k)]
    box = (max(0, min(xs) - 120), max(0, lm['pelvis'][1] - 120), min(im.width, max(xs) + 120), min(im.height, max(ys) + 60))
    im = im.crop(box)
    s = 880 / im.height
    im = im.resize((int(im.width * s), 880), Image.LANCZOS)
    dr = ImageDraw.Draw(im)
    for v in range(0, 1024, step * 2):          # true painting coordinates along the cropped edges
        if box[0] <= v <= box[2]:
            dr.text(((v - box[0]) * s + 2, 2), str(v), fill=(120, 0, 140))
        if box[1] <= v <= box[3]:
            dr.text((2, (v - box[1]) * s + 2), str(v), fill=(120, 0, 140))
im.convert('RGB').save(a.out)
print(a.out, {k: v for k, v in lm.items() if k.startswith(('hip', 'knee', 'ankle', 'weapon', 'offhand', 'wrist'))})
