"""Composite old vs new idle sprites on a battle background at true runtime scale."""
import json
import sys
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
UNITS = {u['Id']: u for u in json.loads((ROOT / 'data/units.json').read_text())['Units']}


def radius(u):
    vs = u.get('VisualScale') or 1.0
    r = 14 * vs + (6 if u['VisualClass'] == 'boss' else 0)
    return r, vs


def old_frame(ident, idx=0):
    meta = json.loads((ROOT / 'assets/units' / f'{ident}.json').read_text())
    sheet = Image.open(ROOT / 'assets/units' / f'{ident}.png').convert('RGBA')
    w, h = meta['frameWidth'], meta['frameHeight']
    cols = sheet.width // w
    fr = sheet.crop(((idx % cols) * w, (idx // cols) * h, (idx % cols + 1) * w, (idx // cols + 1) * h))
    return fr, meta


def new_frame(ident, idx=0, folder='preview'):
    d = ROOT / 'artifacts/remaster' / folder / ident
    meta = json.loads((d / 'metadata.json').read_text())
    return Image.open(d / f'{idx:03}.png').convert('RGBA'), meta


def place(bg, fr, meta, ident, x, ground, flip=False, zoom=2.0):
    r, vs = radius(UNITS[ident])
    width = r * 2 * vs * meta['drawScale'] * zoom
    s = width / fr.width
    im = fr.resize((max(1, int(fr.width * s)), max(1, int(fr.height * s))), Image.LANCZOS)
    if flip:
        im = im.transpose(Image.FLIP_LEFT_RIGHT)
    ax = meta.get('anchorX', 0.5)
    ax = 1 - ax if flip else ax
    bg.alpha_composite(im, (int(x - ax * im.width), int(ground - meta['anchorY'] * im.height)))


def compose(ids, out, zoom=2.0, idx=0, folder='preview'):
    bg = Image.open(ROOT / 'assets/world/battles/stage-01.png').convert('RGBA')
    bg = bg.crop((200, 250, 1800, 650)).resize((int(1600 * zoom / 2), int(400 * zoom / 2)))
    canvas = Image.new('RGBA', (bg.width, bg.height * 2), (0, 0, 0, 255))
    canvas.alpha_composite(bg, (0, 0))
    canvas.alpha_composite(bg, (0, bg.height))
    d = ImageDraw.Draw(canvas)
    step = bg.width / (len(ids) + 1)
    for i, ident in enumerate(ids):
        x = step * (i + 1)
        flip = ident.startswith('enemy')
        try:
            fr, meta = old_frame(ident, idx)
            place(canvas, fr, meta, ident, x, bg.height * 0.78, flip, zoom)
        except Exception as e:
            print('old missing', ident, e)
        try:
            fr, meta = new_frame(ident, idx, folder)
            place(canvas, fr, meta, ident, x, bg.height * 1.78, flip, zoom)
        except Exception as e:
            print('new missing', ident, e)
    d.text((8, 8), 'CURRENT', fill=(255, 255, 255, 255))
    d.text((8, bg.height + 8), 'REMASTER', fill=(255, 255, 255, 255))
    canvas.convert('RGB').save(ROOT / 'artifacts/remaster' / out)


if __name__ == '__main__':
    compose(sys.argv[2].split(','), sys.argv[1], zoom=float(sys.argv[3]) if len(sys.argv) > 3 else 2.0)
