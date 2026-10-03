"""Contact sheet for remastered structures: OLD vs NEW, plus in-game-size battle composites.

python3 art/remaster/structures_sheet.py [--dir artifacts/remaster/structures] [--out contact.png] [--ids a,b]

Rows: one per id with OLD | NEW at review size, then OLD/NEW drawn at the runtime rect size
(wagon 180x140, gatehouse 180x160, mount 60x75) over a crop of a battle backdrop, at 1x and 2x.
Footer: two battle strips (old / new) placing wagon + its three mounts and the gatehouse +
stronghold mount exactly as BattleController draws them, so footprint/anchor drift is obvious.
"""
import argparse
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OLD = ROOT / 'assets/structures'
BG = ROOT / 'assets/world/battles/stage-01.png'
SKINS = ['iron', 'royal', 'bone', 'flame', 'shadow', 'guild', 'legendary']
MOUNTS = ['arrows', 'ballista', 'firepot', 'frost', 'hex']
ALL = ['war_wagon'] + [f'war_wagon_skin_{s}' for s in SKINS] + ['gatehouse'] + [f'mount_{m}' for m in MOUNTS]
PANEL = (46, 52, 58, 255)
INK = (232, 222, 196, 255)


def rect_size(ident):
    if ident.startswith('war_wagon'):
        return 180, 140
    if ident == 'gatehouse':
        return 180, 160
    return 60, 75


def old_path(ident):
    return OLD / f'{ident}.png'


def load(p):
    return Image.open(p).convert('RGBA') if Path(p).exists() else None


def backdrop(w, h, offset=0):
    bg = Image.open(BG).convert('RGBA')
    W, H = bg.size
    box = (int(W * .18) + offset, int(H * .40), int(W * .18) + offset + int(w * W / 1983 * 1.6), int(H * .40) + int(h * W / 1983 * 1.6))
    return bg.crop(box).resize((w, h), Image.LANCZOS)


def fit(im, w, h):
    k = min(w / im.width, h / im.height)
    return im.resize((max(1, int(im.width * k)), max(1, int(im.height * k))), Image.LANCZOS)


def alpha_bbox(im):
    return im.split()[3].point(lambda a: 255 if a > 8 else 0).getbbox()


def ingame(im, ident, scale):
    w, h = rect_size(ident)
    small = im.resize((w, h), Image.LANCZOS)
    pad = 10
    tile = backdrop(w + 2 * pad, h + 2 * pad, offset=hash(ident) % 200)
    tile.alpha_composite(small, (pad, pad))
    return tile.resize((tile.width * scale, tile.height * scale), Image.NEAREST)


def row(ident, new_dir):
    old, new = load(old_path(ident)), load(Path(new_dir) / f'{ident}.png')
    big_w, big_h = (420, 330) if not ident.startswith('mount') else (200, 250)
    tiles = []
    for label, im in (('OLD', old), ('NEW', new)):
        cell = Image.new('RGBA', (big_w, big_h + 18), PANEL)
        if im is not None:
            f = fit(im, big_w, big_h)
            chk = Image.new('RGBA', f.size, (70, 76, 82, 255))
            chk.alpha_composite(f)
            cell.alpha_composite(chk, ((big_w - f.width) // 2, 18))
            bb = alpha_bbox(im)
            ImageDraw.Draw(cell).text((4, 3), f'{label} {im.width}x{im.height} bbox {bb}', fill=INK)
        else:
            ImageDraw.Draw(cell).text((4, 3), f'{label} missing', fill=INK)
        tiles.append(cell)
    for im in (old, new):
        if im is not None:
            tiles.append(ingame(im, ident, 1))
    for im in (old, new):
        if im is not None:
            tiles.append(ingame(im, ident, 2))
    w = sum(t.width for t in tiles) + 10 * len(tiles)
    h = max(t.height for t in tiles) + 24
    canvas = Image.new('RGBA', (w, h), PANEL)
    d = ImageDraw.Draw(canvas)
    d.text((6, 4), ident + '   (OLD | NEW | in-game 1x old/new | 2x old/new)', fill=INK)
    x = 0
    for t in tiles:
        canvas.alpha_composite(t, (x, 22))
        x += t.width + 10
    return canvas


def strip(src, label, wagon='war_wagon'):
    """Wagon + mounts at the left, gatehouse + stronghold mount at the right, runtime coordinates."""
    W, H = 760, 300
    k = 2
    canvas = backdrop(W, H, 40)
    cy = 170
    px, ex = 130, W - 140

    def get(ident):
        p = Path(src) / f'{ident}.png' if src != 'old' else old_path(ident)
        return load(p) or load(old_path(ident))
    wag = get(wagon)
    canvas.alpha_composite(wag.resize((180, 140), Image.LANCZOS), (px - 70, cy - 80))
    for i, m in enumerate(('arrows', 'ballista', 'firepot')):
        mi = get(f'mount_{m}').resize((60, 75), Image.LANCZOS)
        canvas.alpha_composite(mi, (px - 20 + i * 27 - 30, cy - 38 - 42))
    gate = get('gatehouse')
    canvas.alpha_composite(gate.resize((180, 160), Image.LANCZOS), (ex - 80, cy - 100))
    sm = get('mount_ballista').resize((60, 75), Image.LANCZOS).transpose(Image.FLIP_LEFT_RIGHT)
    # mirrored around the anchor: x in [-30, 30] maps to [30, -30]
    canvas.alpha_composite(sm, (ex + 32 - 30, cy - 62 - 42))
    d = ImageDraw.Draw(canvas)
    d.rectangle((px - 70, cy - 80, px + 110, cy + 60), outline=(255, 255, 255, 60))
    d.rectangle((ex - 80, cy - 100, ex + 100, cy + 60), outline=(255, 255, 255, 60))
    for x0 in (px + 8 - 66, ex + 10 - 66):
        d.rectangle((x0, (cy - 80 if x0 < W / 2 else cy - 100) - 22, x0 + 132, (cy - 80 if x0 < W / 2 else cy - 100) - 16),
                    fill=(60, 200, 90, 220))
    d.text((6, 4), label, fill=INK)
    return canvas.resize((W * k, H * k), Image.NEAREST)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--dir', default=str(ROOT / 'artifacts/remaster/structures'))
    ap.add_argument('--out', default=None)
    ap.add_argument('--ids', default='all')
    a = ap.parse_args()
    ids = ALL if a.ids == 'all' else a.ids.split(',')
    rows = [row(i, a.dir) for i in ids]
    strips = [strip('old', 'OLD in battle (runtime rects, mounts at their anchors)'), strip(a.dir, 'NEW in battle')]
    W = max([r.width for r in rows] + [s.width for s in strips])
    H = sum(r.height for r in rows) + sum(s.height + 10 for s in strips)
    sheet = Image.new('RGBA', (W, H), PANEL)
    y = 0
    for s in strips:
        sheet.alpha_composite(s, (0, y))
        y += s.height + 10
    for r in rows:
        sheet.alpha_composite(r, (0, y))
        y += r.height
    out = Path(a.out) if a.out else Path(a.dir) / 'contact.png'
    sheet.convert('RGB').save(out)
    print(out)


if __name__ == '__main__':
    main()
