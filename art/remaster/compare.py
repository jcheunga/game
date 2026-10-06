"""Build comparison media (old vs remaster) for the review page.

python3 art/remaster/compare.py   ->  artifacts/remaster/compare/{units,battle,items,structures,particles}/...
"""
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / 'artifacts/remaster'
# Originals come from git HEAD (assets/ may already hold the remaster in this branch).
OLD = REVIEW / 'old'
OUT = REVIEW / 'compare'
UNITS = json.loads((ROOT / 'data/units.json').read_text())['Units']
STAGE_BG = (38, 42, 46)


def font(size):
    for f in ('/System/Library/Fonts/Supplemental/Arial.ttf', '/System/Library/Fonts/Helvetica.ttc'):
        try:
            return ImageFont.truetype(f, size)
        except OSError:
            pass
    return ImageFont.load_default()


def atlas_frames(png, meta):
    sheet = Image.open(png).convert('RGBA')
    w, h = meta['frameWidth'], meta['frameHeight']
    cols = sheet.width // w
    count = sum(a['count'] for a in meta['animations'].values())
    return [sheet.crop(((i % cols) * w, (i // cols) * h, (i % cols + 1) * w, (i // cols + 1) * h)) for i in range(count)]


def unit_strip(ident, panel=(300, 300), px_per_draw=58.0):
    """Animated WebP: current | remaster at true relative in-game scale."""
    old_meta = json.loads((OLD / 'assets/units' / f'{ident}.json').read_text())
    new_dir = REVIEW / 'stage/assets/units'
    if not (new_dir / f'{ident}.json').exists():
        return None
    new_meta = json.loads((new_dir / f'{ident}.json').read_text())
    old = atlas_frames(OLD / 'assets/units' / f'{ident}.png', old_meta)
    new = atlas_frames(new_dir / f'{ident}.png', new_meta)
    pw, ph = panel
    # Same scale for both sides (true relative size), chosen per unit to fill the panel.
    px_per_draw = min(115.0, 400.0 / max(old_meta['drawScale'], new_meta['drawScale']))
    frames, durations = [], []
    clips = new_meta['animations']
    order = ['idle', 'idle', 'walk', 'walk', 'attack', 'hit', 'idle', 'death', 'deploy']
    for clip in order:
        c = clips[clip]
        oc = old_meta['animations'].get(clip, c)
        for k in range(c['count']):
            canvas = Image.new('RGBA', (pw * 2, ph), STAGE_BG + (255,))
            for side, (fr_list, meta, cc) in enumerate(((old, old_meta, oc), (new, new_meta, c))):
                idx = cc['start'] + min(k, cc['count'] - 1)
                fr = fr_list[idx]
                width = meta['drawScale'] * px_per_draw
                s = width / fr.width
                im = fr.resize((max(1, int(fr.width * s)), max(1, int(fr.height * s))), Image.LANCZOS)
                ax, ay = meta.get('anchorX', 0.5), meta['anchorY']
                gx, gy = pw * side + pw * 0.45, ph * 0.9
                canvas.alpha_composite(im, (int(gx - ax * im.width), int(gy - ay * im.height)))
            frames.append(canvas.convert('RGB'))
            durations.append(int(max(70, c['duration'] * 1000 * 1.6)))
    path = OUT / 'units' / f'{ident}.webp'
    path.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(path, save_all=True, append_images=frames[1:], duration=durations, loop=0, quality=82, method=4)
    # still for thumbnails / reduced motion
    frames[0].save(OUT / 'units' / f'{ident}.jpg', quality=85)
    return path


def battle_scene(ids, out_name, stage_png='assets/world/royal/city_ground.png', frame_idx=0, zoom=2.0, crop=None):
    """Old and new rosters standing on the same painted road at runtime scale."""
    bg = Image.open(ROOT / stage_png).convert('RGBA')
    if crop:
        bg = bg.crop(crop)
    bg = bg.resize((int(bg.width * zoom / 2), int(bg.height * zoom / 2)))
    out = []
    for label, src in (('current', 'old'), ('remaster', 'new')):
        canvas = bg.copy()
        step = canvas.width / (len(ids) + 1)
        for i, ident in enumerate(ids):
            u = next(x for x in UNITS if x['Id'] == ident)
            vs = u.get('VisualScale') or 1.0
            r = 14 * vs + (6 if u['VisualClass'] == 'boss' else 0)
            if src == 'old':
                meta = json.loads((OLD / 'assets/units' / f'{ident}.json').read_text())
                frames = atlas_frames(OLD / 'assets/units' / f'{ident}.png', meta)
            else:
                p = REVIEW / 'stage/assets/units' / f'{ident}.json'
                if not p.exists():
                    continue
                meta = json.loads(p.read_text())
                frames = atlas_frames(REVIEW / 'stage/assets/units' / f'{ident}.png', meta)
            fr = frames[frame_idx]
            width = r * 2 * vs * meta['drawScale'] * zoom
            s = width / fr.width
            im = fr.resize((max(1, int(fr.width * s)), max(1, int(fr.height * s))), Image.LANCZOS)
            flip = ident.startswith('enemy')
            ax = meta.get('anchorX', 0.5)
            if flip:
                im = im.transpose(Image.FLIP_LEFT_RIGHT)
                ax = 1 - ax
            x = step * (i + 1)
            g = canvas.height * (0.74 + 0.1 * ((i % 2) * 2 - 1) * 0.5)
            canvas.alpha_composite(im, (int(x - ax * im.width), int(g - meta['anchorY'] * im.height)))
        path = OUT / 'battle' / f'{out_name}-{label}.jpg'
        path.parent.mkdir(parents=True, exist_ok=True)
        canvas.convert('RGB').save(path, quality=88)
        out.append(path)
    return out


def pair_sheet(pairs, out_path, cell=200, cols=6, label_h=26, small=None):
    """Grid of (title, old_path, new_path) shown as old|new per cell."""
    rows = math.ceil(len(pairs) / cols)
    cw = cell * 2 + 12
    ch = cell + label_h + (small or 0)
    sheet = Image.new('RGB', (cols * cw, rows * ch), STAGE_BG)
    d = ImageDraw.Draw(sheet)
    f = font(14)
    for i, (title, old, new) in enumerate(pairs):
        x, y = (i % cols) * cw, (i // cols) * ch
        for j, p in enumerate((old, new)):
            if p and Path(p).exists():
                im = Image.open(p).convert('RGBA')
                im.thumbnail((cell - 10, cell - 10), Image.LANCZOS)
                sheet.paste(im, (x + j * (cell + 4) + (cell - im.width) // 2, y + (cell - im.height) // 2), im)
                if small:
                    sm = Image.open(p).convert('RGBA')
                    sm.thumbnail((small - 4, small - 4), Image.LANCZOS)
                    sheet.paste(sm, (x + j * (cell + 4) + (cell - sm.width) // 2, y + cell + label_h - 2), sm)
        d.text((x + 6, y + cell + 4), title[:30], fill=(225, 214, 190), font=f)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path, quality=88)
    return out_path


def items():
    data = {
        'spells': json.loads((ROOT / 'data/spells.json').read_text())['Spells'],
        'relics': json.loads((ROOT / 'data/equipment.json').read_text())['Equipment'],
    }
    out = {}
    for cat in ('spells', 'relics', 'rewards', 'meta'):
        ids = [x['Id'] for x in data[cat]] if cat in data else sorted(p.stem for p in (OLD / 'assets/ui/icons' / cat).glob('*.png'))
        names = {x['Id']: x['DisplayName'] for x in data.get(cat, [])}
        pairs = [(names.get(i, i.replace('_', ' ').title()), OLD / 'assets/ui/icons' / cat / f'{i}.png',
                  REVIEW / 'items' / cat / f'{i}.png') for i in ids]
        if any(p[2].exists() for p in pairs):
            out[cat] = pair_sheet(pairs, OUT / 'items' / f'{cat}.jpg', cell=180, cols=4, small=64)
    return out


def structures():
    ids = ['war_wagon'] + [f'war_wagon_skin_{s}' for s in ('iron', 'royal', 'bone', 'flame', 'shadow', 'guild', 'legendary')] + \
          ['gatehouse'] + [f'mount_{m}' for m in ('arrows', 'ballista', 'firepot')]
    res = []
    for i in ids:
        old, new = OLD / 'assets/structures' / f'{i}.png', REVIEW / 'structures' / f'{i}.png'
        if not new.exists():
            continue
        a, b = Image.open(old).convert('RGBA'), Image.open(new).convert('RGBA')
        h = 420
        a = a.resize((int(a.width * h / a.height), h), Image.LANCZOS)
        b = b.resize((int(b.width * h / b.height), h), Image.LANCZOS)
        c = Image.new('RGB', (a.width + b.width + 24, h + 20), STAGE_BG)
        c.paste(a, (8, 10), a)
        c.paste(b, (a.width + 16, 10), b)
        p = OUT / 'structures' / f'{i}.jpg'
        p.parent.mkdir(parents=True, exist_ok=True)
        c.save(p, quality=88)
        res.append(p)
    return res


def battle_v2():
    """Battle presentation structures: current battle-v2 (git HEAD) vs remaster, side by side."""
    res = []
    for new in sorted((REVIEW / 'structures/battle-v2').glob('*.png')) if (REVIEW / 'structures/battle-v2').exists() else []:
        old = OLD / 'assets/structures/battle-v2' / new.name
        if not old.exists() or new.stem.startswith('contact'):
            continue
        h = 420
        a = Image.open(old).convert('RGBA').resize((h, h), Image.LANCZOS)
        b = Image.open(new).convert('RGBA').resize((h, h), Image.LANCZOS)
        c = Image.new('RGB', (h * 2 + 24, h + 20), STAGE_BG)
        c.paste(a, (8, 10), a)
        c.paste(b, (h + 16, 10), b)
        p = OUT / 'structures' / f'battle-{new.stem}.jpg'
        p.parent.mkdir(parents=True, exist_ok=True)
        c.save(p, quality=88)
        res.append(p)
    return res


def particles():
    pairs = []
    for new in sorted((REVIEW / 'particles').glob('particle_*.png')) if (REVIEW / 'particles').exists() else []:
        pairs.append((new.stem.replace('particle_', ''), OLD / 'assets/particles' / new.name, new))
    if pairs:
        return pair_sheet(pairs, OUT / 'particles' / 'particles.jpg', cell=150, cols=4)


def ingame():
    """Real engine captures (BlenderAssetSmoke --screenshots): current above, remaster below."""
    res = []
    for name in ('assets-in-battle-1', 'assets-in-battle-24', 'assets-in-battle-60', 'codex-in-game'):
        old, new = REVIEW / 'ingame/old' / f'{name}.png', REVIEW / 'ingame/new' / f'{name}.png'
        if not (old.exists() and new.exists()):
            continue
        a = Image.open(old).convert('RGB')
        b = Image.open(new).convert('RGB')
        w = 1600
        a = a.resize((w, int(a.height * w / a.width)), Image.LANCZOS)
        b = b.resize((w, int(b.height * w / b.width)), Image.LANCZOS)
        c = Image.new('RGB', (w, a.height + b.height + 16), STAGE_BG)
        c.paste(a, (0, 0))
        c.paste(b, (0, a.height + 16))
        p = OUT / 'ingame' / f'{name}.jpg'
        p.parent.mkdir(parents=True, exist_ok=True)
        c.save(p, quality=84)
        res.append(p)
    return res


if __name__ == '__main__':
    print('ingame', len(ingame()))
    made = []
    for u in UNITS:
        p = unit_strip(u['Id'])
        if p:
            made.append(p)
    print('unit strips', len(made))
    battle_scene(['player_brawler', 'player_shooter', 'player_defender', 'player_spear', 'player_ranger', 'player_marksman',
                  'player_banner', 'player_raider', 'player_hound'], 'kings-road-lantern')
    battle_scene(['enemy_walker', 'enemy_runner', 'enemy_bloater', 'enemy_brute', 'enemy_spitter', 'enemy_shieldwall', 'enemy_lich',
                  'enemy_crusher', 'enemy_boss'], 'kings-road-rotbound')
    print('items', items())
    print('structures', len(structures()), 'battle-v2', len(battle_v2()))
    print('particles', particles())
