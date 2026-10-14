#!/usr/bin/env python3
"""Cuts the painted effect sheets (artifacts/remaster/fx/*_sheet.png, Codex, in the roster sheet's style) into the
sprites the battle loads: assets/projectiles/<name>.png (+ projectiles.json tip/centre) and assets/fx/<name>.png.

  python3 art/remaster/fx_cut.py
"""
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / 'artifacts/remaster/fx'

PROJECTILES = [['arrow', 'bolt', 'ballista_bolt', 'bone_bolt'],
               ['harpoon', 'javelin', 'cog', 'flask'],
               ['firepot', 'plague_pot', 'bomb', 'boulder'],
               ['skull', 'blight_glob', 'hand_axe', 'pitch']]
SPIN = {'cog', 'flask', 'firepot', 'plague_pot', 'bomb', 'boulder', 'skull', 'hand_axe', 'pitch'}
GORE = [['stain_1', 'stain_2', 'stain_3', 'stain_4'],
        ['spray_1', 'spray_2', 'spray_3', 'spray_4'],
        ['drop_1', 'drop_2', 'drop_3', 'drop_4'],
        ['bone_1', 'bone_2', 'splinter_1', 'splinter_2']]


def cells(sheet, grid):
    im = Image.open(sheet).convert('RGBA')
    w, h = im.size
    cw, ch = w // 4, h // 4
    for r, row in enumerate(grid):
        for c, name in enumerate(row):
            cell = im.crop((c * cw, r * ch, (c + 1) * cw, (r + 1) * ch))
            a = cell.getchannel('A').point(lambda v: 255 if v > 24 else 0)
            box = a.getbbox()
            if box:
                pad = 4
                box = (max(0, box[0] - pad), max(0, box[1] - pad), min(cw, box[2] + pad), min(ch, box[3] + pad))
                yield name, cell.crop(box)


def main():
    meta = {}
    old = json.loads((ROOT / 'assets/projectiles/projectiles.json').read_text())
    for name, im in cells(SRC / 'projectiles_sheet.png', PROJECTILES):
        im.save(ROOT / f'assets/projectiles/{name}.png', optimize=True)
        a = im.getchannel('A')
        w, h = im.size
        # the tip is the rightmost painted column's middle; the centre is the mass centre on that row
        px = a.load()
        rows = [y for y in range(h) if px[w - 6, y] > 40] or [h // 2]
        tip_y = sum(rows) / len(rows) / h
        meta[name] = {'size': [w, h], 'tip': [0.97, round(tip_y, 5)], 'centre': [0.5, round(tip_y, 5)],
                      'length': old.get(name, {}).get('length', round(w / 600, 3)), 'spin': name in SPIN}
    (ROOT / 'assets/projectiles/projectiles.json').write_text(json.dumps({**old, **meta}, indent=2) + '\n')
    for name, im in cells(SRC / 'gore_sheet.png', GORE):
        # blood is painted deep red; the game tints it per creature, so store it brightened toward white-red
        im.save(ROOT / f'assets/fx/{name}.png', optimize=True)
    print('projectiles', sorted(meta), 'fx', len(list((ROOT / 'assets/fx').glob('*.png'))))


if __name__ == '__main__':
    main()
