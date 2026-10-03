"""OLD vs NEW contact sheets for the particle and menu-background remasters (system python + Pillow).

python3 art/remaster/contact_sheets.py particles|menus|battlefields|maps|all [--ids a,b]
  particles    -> artifacts/remaster/particles/contact.png
  menus        -> artifacts/remaster/menus/contact.png
  battlefields -> artifacts/remaster/battlefields/contact.png
  maps         -> artifacts/remaster/maps/contact.png
OLD images always come from git HEAD.
"""
import sys
from pathlib import Path

import io
import subprocess

from PIL import Image, ImageChops, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / 'artifacts/remaster'
PARTICLES = ['particle_soft', 'particle_deploy', 'particle_smoke', 'particle_spark', 'particle_fire', 'particle_heal',
             'particle_frost', 'particle_lightning', 'particle_arcane', 'particle_stone', 'particle_trail']
MENUS = ['arena', 'battle_summary', 'bounty', 'cash_shop', 'codex', 'endless', 'event', 'expedition', 'forge',
         'friends', 'guild', 'lan_race', 'leaderboard', 'loadout', 'login_calendar', 'multiplayer', 'profile', 'raid',
         'season_pass', 'settings', 'shop', 'skill_tree', 'tower']
TINTS = [('amber', (255, 176, 74)), ('teal', (60, 214, 196)), ('plague', (156, 242, 92)), ('crimson', (226, 70, 96))]
INK = (236, 222, 190, 255)
DIM = (150, 140, 120, 255)


def old_asset(rel):
    """The committed (pre-remaster) asset from git HEAD, so OLD stays OLD even after new art is packed in."""
    try:
        data = subprocess.run(['git', '-C', str(ROOT), 'show', f'HEAD:{rel}'], capture_output=True, check=True).stdout
        return Image.open(io.BytesIO(data))
    except (subprocess.CalledProcessError, OSError):
        p = ROOT / rel
        return Image.open(p) if p.exists() else None


def font(size):
    for name in ('/System/Library/Fonts/Supplemental/Georgia.ttf', '/System/Library/Fonts/Helvetica.ttc',
                 '/Library/Fonts/Arial.ttf'):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def tinted(im, rgb):
    r, g, b, a = im.split()
    col = Image.new('RGB', im.size, rgb)
    rgb_im = ImageChops.multiply(Image.merge('RGB', (r, g, b)), col)
    return Image.merge('RGBA', (*rgb_im.split(), a))


def checker(size, a=(34, 32, 38), b=(44, 42, 50), cell=8):
    im = Image.new('RGB', size, a)
    d = ImageDraw.Draw(im)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2:
                d.rectangle([x, y, x + cell - 1, y + cell - 1], fill=b)
    return im.convert('RGBA')


def tile(im, size, bg=(22, 20, 26)):
    t = Image.new('RGBA', (size, size), bg + (255,))
    im = im.resize((size, size), Image.LANCZOS) if im.size != (size, size) else im
    t.alpha_composite(im)
    return t


def particles(ids=None):
    ids = ids or PARTICLES
    cell = 128
    cols = ['OLD', 'NEW', 'NEW alpha', 'NEW checker'] + [f'NEW x {n}' for n, _ in TINTS] + ['NEW 32px (x2)', 'OLD amber']
    lab_w = 150
    head = 44
    W = lab_w + cell * len(cols) + 8 * len(cols)
    H = head + (cell + 10) * len(ids) + 10
    sheet = Image.new('RGBA', (W, H), (16, 15, 19, 255))
    d = ImageDraw.Draw(sheet)
    f, fs = font(17), font(12)
    d.text((10, 12), 'Particles  OLD vs NEW  (straight alpha, tinted by multiply as CpuParticles2D does)', fill=INK,
           font=f)
    for c, name in enumerate(cols):
        d.text((lab_w + c * (cell + 8) + 2, head - 16), name, fill=DIM, font=fs)
    for r, ident in enumerate(ids):
        y = head + r * (cell + 10)
        d.text((10, y + cell // 2 - 8), ident.replace('particle_', ''), fill=INK, font=f)
        new_p = REVIEW / 'particles' / f'{ident}.png'
        old = old_asset(f'assets/particles/{ident}.png')
        old = old.convert('RGBA') if old is not None else None
        new = Image.open(new_p).convert('RGBA') if new_p.exists() else None
        tiles = []
        tiles.append(tile(old, cell) if old else None)
        tiles.append(tile(new, cell) if new else None)
        if new:
            a = new.getchannel('A')
            tiles.append(Image.merge('RGBA', (a, a, a, Image.new('L', a.size, 255))))
            ch = checker((cell, cell))
            ch.alpha_composite(new)
            tiles.append(ch)
            for _, rgb in TINTS:
                tiles.append(tile(tinted(new, rgb), cell))
            small = new.resize((32, 32), Image.LANCZOS)
            t = Image.new('RGBA', (cell, cell), (22, 20, 26, 255))
            s2 = tinted(small, TINTS[0][1]).resize((64, 64), Image.NEAREST)
            t.alpha_composite(s2, (32, 32))
            tiles.append(t)
        else:
            tiles += [None] * (len(cols) - 3)
        tiles.append(tile(tinted(old, TINTS[0][1]), cell) if old else None)
        for c, t in enumerate(tiles):
            x = lab_w + c * (cell + 8)
            if t is None:
                d.rectangle([x, y, x + cell, y + cell], outline=(80, 40, 40, 255))
            else:
                sheet.alpha_composite(t.convert('RGBA'), (x, y))
    out = REVIEW / 'particles' / 'contact.png'
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert('RGB').save(out)
    return out


def menus(ids=None, width=560):
    ids = ids or MENUS
    h = width * 9 // 16
    lab = 28
    W = 2 * width + 3 * 12
    rows = len(ids)
    H = 50 + rows * (h + lab + 10)
    sheet = Image.new('RGB', (W, H), (16, 15, 19))
    d = ImageDraw.Draw(sheet)
    f, fs = font(20), font(15)
    d.text((12, 14), 'Menu backgrounds  OLD (left) vs NEW (right)', fill=INK[:3], font=f)
    for r, ident in enumerate(ids):
        y = 50 + r * (h + lab + 10)
        d.text((12, y + 4), ident, fill=INK[:3], font=fs)
        new_p = REVIEW / 'menus' / f'{ident}.png'
        for c, im in enumerate((old_asset(f'assets/ui/backgrounds/{ident}.png'),
                                Image.open(new_p) if new_p.exists() else None)):
            x = 12 + c * (width + 12)
            if im is not None:
                sheet.paste(im.convert('RGB').resize((width, h), Image.LANCZOS), (x, y + lab))
            else:
                d.rectangle([x, y + lab, x + width, y + lab + h], outline=(90, 40, 40))
                d.text((x + 10, y + lab + 10), 'missing', fill=(200, 90, 90), font=fs)
    out = REVIEW / 'menus' / 'contact.png'
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)
    return out


BATTLE_ROUTES = [('city', ['urban', 'highway', 'night']), ('harbor', ['industrial', 'shipyard', 'swamp']),
                 ('foundry', ['railyard', 'smelter', 'foundry']),
                 ('quarantine', ['checkpoint', 'decon', 'lab', 'blacksite']), ('thornwall', ['pass', 'shrine', 'watchfort']),
                 ('basilica', ['cathedral', 'ossuary', 'reliquary']), ('mire', ['marsh', 'chapel', 'ferry']),
                 ('steppe', ['grassland', 'siegecamp', 'waystation']), ('gloamwood', ['grove', 'timberroad', 'witchcircle']),
                 ('citadel', ['bridgefort', 'breachyard', 'innerkeep'])]
EARTH = {'harbor': '63706c', 'foundry': '715c4a', 'quarantine': '646453', 'thornwall': '647363', 'basilica': '716d61',
         'mire': '46553c', 'steppe': '768253', 'gloamwood': '465e42', 'citadel': '626b6d', 'city': '6b7050'}


def in_game(im, route):
    """Approximate the runtime: combat rect (x 84.., y 96..584) is painted with the route's procedural earth."""
    im = im.convert('RGB').copy()
    d = ImageDraw.Draw(im)
    c = tuple(int(EARTH[route][i:i + 2], 16) for i in (0, 2, 4))
    dark = tuple(int(v * 0.8) for v in c)
    d.rectangle([84, 96, 1280, 584], fill=c)
    d.rectangle([84, 90, 1280, 95], fill=dark)
    d.rectangle([84, 585, 1280, 590], fill=dark)
    return im


def battlefields(width=400):
    h = width * 9 // 16
    lab = 24
    cols = ['OLD', 'NEW', 'NEW as seen in battle (lane covered)']
    W = 150 + 3 * (width + 10)
    rows = [(r, t) for r, ts in BATTLE_ROUTES for t in ts]
    H = 50 + len(rows) * (h + lab + 6)
    sheet = Image.new('RGB', (W, H), (16, 15, 19))
    d = ImageDraw.Draw(sheet)
    f, fs = font(20), font(14)
    d.text((12, 14), 'Battlefield fallbacks  OLD vs NEW  (31 terrain ids)', fill=INK[:3], font=f)
    for c, name in enumerate(cols):
        d.text((150 + c * (width + 10), 36), name, fill=DIM[:3], font=fs)
    for i, (route, t) in enumerate(rows):
        y = 50 + i * (h + lab + 6)
        d.text((10, y + lab + h // 2 - 16), t, fill=INK[:3], font=f)
        d.text((10, y + lab + h // 2 + 8), route, fill=DIM[:3], font=fs)
        old = old_asset(f'assets/backgrounds/{t}.png')
        p = REVIEW / 'battlefields' / f'{t}.png'
        new = Image.open(p) if p.exists() else None
        ims = [old, new, in_game(new, route) if new else None]
        for c, im in enumerate(ims):
            x = 150 + c * (width + 10)
            if im is not None:
                sheet.paste(im.convert('RGB').resize((width, h), Image.LANCZOS), (x, y + lab))
            else:
                d.rectangle([x, y + lab, x + width, y + lab + h], outline=(90, 40, 40))
    out = REVIEW / 'battlefields' / 'contact.png'
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)
    return out


MAP_ROUTES = ['city', 'harbor', 'foundry', 'quarantine', 'thornwall', 'basilica', 'mire', 'steppe', 'gloamwood',
              'citadel']


def maps(width=480):
    h = width * 3 // 4
    lab = 28
    W = 2 * width + 3 * 12
    H = 50 + len(MAP_ROUTES) * (h + lab + 10)
    sheet = Image.new('RGB', (W, H), (16, 15, 19))
    d = ImageDraw.Draw(sheet)
    f, fs = font(20), font(15)
    d.text((12, 14), 'District maps  OLD (left) vs NEW (right)', fill=INK[:3], font=f)
    for r, ident in enumerate(MAP_ROUTES):
        y = 50 + r * (h + lab + 10)
        d.text((12, y + 4), ident, fill=INK[:3], font=fs)
        p = REVIEW / 'maps' / f'{ident}.png'
        for c, im in enumerate((old_asset(f'assets/map/backgrounds/{ident}.png'), Image.open(p) if p.exists() else None)):
            x = 12 + c * (width + 12)
            if im is not None:
                sheet.paste(im.convert('RGB').resize((width, h), Image.LANCZOS), (x, y + lab))
            else:
                d.rectangle([x, y + lab, x + width, y + lab + h], outline=(90, 40, 40))
    out = REVIEW / 'maps' / 'contact.png'
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)
    return out


def menus_grid(ids=None, out_name='grid.png', cols=4, width=480):
    """Compact NEW-only overview (for quick review)."""
    ids = ids or MENUS
    h = width * 9 // 16
    rows = (len(ids) + cols - 1) // cols
    sheet = Image.new('RGB', (cols * width, rows * h), (0, 0, 0))
    d = ImageDraw.Draw(sheet)
    fs = font(15)
    for i, ident in enumerate(ids):
        p = REVIEW / 'menus' / f'{ident}.png'
        x, y = (i % cols) * width, (i // cols) * h
        if p.exists():
            sheet.paste(Image.open(p).convert('RGB').resize((width, h), Image.LANCZOS), (x, y))
        d.text((x + 6, y + 4), ident, fill=(255, 225, 150), font=fs)
    out = REVIEW / 'menus' / 'review' / out_name
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)
    return out


if __name__ == '__main__':
    what = sys.argv[1] if len(sys.argv) > 1 else 'all'
    ids = None
    if '--ids' in sys.argv:
        ids = sys.argv[sys.argv.index('--ids') + 1].split(',')
    if what in ('particles', 'all'):
        print(particles(ids if what == 'particles' else None))
    if what in ('menus', 'all'):
        print(menus(ids if what == 'menus' else None))
    if what in ('battlefields', 'all'):
        print(battlefields())
    if what in ('maps', 'all'):
        print(maps())
    if what == 'grid':
        print(menus_grid(ids))
