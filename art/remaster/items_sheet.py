"""OLD vs NEW contact sheet for the item icons (system python3 + Pillow).

python3 art/remaster/items_sheet.py                 -> artifacts/remaster/items/contact.png
python3 art/remaster/items_sheet.py --review spells -> artifacts/remaster/items/review_spells.png (new only, big + small)
"""
import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
NEW = ROOT / 'artifacts/remaster/items'
OLD = ROOT / 'assets/ui/icons'
CATS = ['spells', 'relics', 'rewards', 'meta']
BG = (31, 34, 39, 255)
CARD = (45, 49, 56, 255)
LIGHT = (196, 186, 166, 255)
TEXT = (232, 220, 190, 255)
DIM = (150, 150, 150, 255)


def font(size):
    for p in ('/System/Library/Fonts/SFNSMono.ttf', '/System/Library/Fonts/Menlo.ttc',
              '/System/Library/Fonts/Supplemental/Arial.ttf'):
        try:
            return ImageFont.truetype(p, size)
        except OSError:
            continue
    return ImageFont.load_default()


def ids_for(cat):
    return sorted(p.stem for p in (NEW / cat).glob('*.png'))


def load(path, size):
    if not path.exists():
        return None
    return Image.open(path).convert('RGBA').resize((size, size), Image.LANCZOS)


def tile(canvas, im, x, y, size, bg):
    d = ImageDraw.Draw(canvas)
    d.rectangle((x, y, x + size - 1, y + size - 1), fill=bg)
    if im is not None:
        canvas.alpha_composite(im, (x, y))


def contact(out):
    big, small, tiny = 176, 64, 48
    cw, ch = big * 2 + 30, 30 + big + 12 + small + 14
    entries = [(c, i) for c in CATS for i in ids_for(c)]
    cols = 6
    rows = (len(entries) + cols - 1) // cols
    head = 70
    canvas = Image.new('RGBA', (cols * cw + 20, head + rows * ch + 10), BG)
    d = ImageDraw.Draw(canvas)
    d.text((14, 12), 'CROWNROAD item icons  -  OLD (left)  vs  NEW (right)   |   small row: old 64, new 64, new 48, '
           'new 64 on parchment', fill=TEXT, font=font(20))
    f = font(13)
    for k, (cat, ident) in enumerate(entries):
        x = 10 + (k % cols) * cw
        y = head + (k // cols) * ch
        d.rectangle((x, y, x + cw - 8, y + ch - 8), fill=CARD)
        d.text((x + 8, y + 6), f'{cat}/{ident}', fill=TEXT, font=f)
        old_p, new_p = OLD / cat / f'{ident}.png', NEW / cat / f'{ident}.png'
        tile(canvas, load(old_p, big), x + 8, y + 26, big, BG)
        tile(canvas, load(new_p, big), x + 16 + big, y + 26, big, BG)
        sy = y + 26 + big + 8
        sx = x + 8
        for p, s, bg in ((old_p, small, BG), (new_p, small, BG), (new_p, tiny, BG), (new_p, small, LIGHT)):
            tile(canvas, load(p, s), sx, sy, s, bg)
            sx += s + 10
    canvas.save(out)
    return out


def review(cat, out, ids=None):
    ids = ids or ids_for(cat)
    big, small = 256, 64
    cw = big + 10
    cols = min(6, len(ids))
    rows = (len(ids) + cols - 1) // cols
    ch = big + small + 40
    canvas = Image.new('RGBA', (cols * cw + 10, rows * ch + 10), BG)
    d = ImageDraw.Draw(canvas)
    f = font(13)
    for k, ident in enumerate(ids):
        x = 10 + (k % cols) * cw
        y = 10 + (k // cols) * ch
        p = NEW / cat / f'{ident}.png'
        tile(canvas, load(p, big), x, y, big, CARD)
        tile(canvas, load(p, small), x, y + big + 6, small, BG)
        tile(canvas, load(p, 48), x + small + 8, y + big + 6, 48, BG)
        tile(canvas, load(p, small), x + small + 64, y + big + 6, small, LIGHT)
        tile(canvas, load(OLD / cat / f'{ident}.png', small), x + 2 * small + 72, y + big + 6, small, (60, 40, 40, 255))
        d.text((x, y + big + small + 10), ident, fill=TEXT, font=f)
    canvas.save(out)
    return out


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--review', default='')
    ap.add_argument('--ids', default='')
    ap.add_argument('--out', default='')
    a = ap.parse_args()
    if a.review:
        ids = [i for i in a.ids.split(',') if i] or None
        print(review(a.review, Path(a.out) if a.out else NEW / f'review_{a.review}.png', ids))
    else:
        print(contact(Path(a.out) if a.out else NEW / 'contact.png'))
