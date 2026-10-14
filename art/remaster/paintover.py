#!/usr/bin/env python3
"""Paint-over: each unit's stance frame repainted in the roster sheet's style (output/units.png) by Codex image
generation, then projected back onto the rigged model (rk/paint.py) so every animation frame carries the paint.

  python3 art/remaster/paintover.py prep  player_spear,enemy_walker     # guide.png + crop.json from units/<id>/000.png
  python3 art/remaster/paintover.py paint player_spear --jobs 3          # Codex: guide.png -> painted.png (skips done)
  python3 art/remaster/paintover.py sheet player_spear                   # guide | painted review sheet
  python3 art/remaster/paintover.py icons player_spear                   # units/<id>/portrait.png from the painting

Files live in artifacts/remaster/paint/<id>/. `build_units.py --paint` renders the painted masters.
"""
import argparse
import concurrent.futures as cf
import json
import subprocess
import time
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / 'artifacts/remaster'
PAINT = REVIEW / 'paint'
REF = ROOT / 'output/units.png'
GREY = (221, 217, 209)
SIZE = 1024
UNITS = {u['Id']: u for u in json.loads((ROOT / 'data/units.json').read_text())['Units']}

FACTION = {
    'player': ('a soldier of the Lantern Caravan, a royal human army: teal and cream heraldry with a gold crowned-lantern '
               'emblem, blued steel armour with brass and gold trim, warm brown leather, oak'),
    'enemy': ('a creature of the Rotbound Host, an undead army: ruined crimson rags and heraldry, weathered bone, '
              'rust-eaten black iron with tarnished brass, sickly pale-green flesh, eerie plague-green glowing eyes and lights'),
}


def ids_arg(s):
    if s == 'all':
        return [i for i in UNITS if (REVIEW / 'units' / i / 'metadata.json').exists()]
    return s.split(',')


def prep(ident, margin=0.1, force=False):
    """Square crop around the stance figure, upscaled to SIZE on the sheet's grey. Made once per model: after the
    painted render, units/<id>/000.png is painted itself (use --force after changing the model)."""
    out = PAINT / ident
    if (out / 'crop.json').exists() and not force:
        return out
    src = REVIEW / 'units' / ident
    meta = json.loads((src / 'metadata.json').read_text())
    frame = Image.open(src / '000.png').convert('RGBA')
    W, H = frame.size
    x0, y0, x1, y1 = frame.getchannel('A').point(lambda a: 255 if a > 8 else 0).getbbox()
    side = max(x1 - x0, y1 - y0) * (1 + 2 * margin)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    box = (round(cx - side / 2), round(cy - side / 2), round(cx + side / 2), round(cy + side / 2))
    crop = frame.crop(box).resize((SIZE, SIZE), Image.LANCZOS)   # outside the frame comes back transparent
    out.mkdir(parents=True, exist_ok=True)
    guide = Image.new('RGBA', (SIZE, SIZE), GREY + (255,))
    guide.alpha_composite(crop)
    guide.convert('RGB').save(out / 'guide.png')
    crop.getchannel('A').save(out / 'guide_alpha.png')
    frame.save(out / 'frame000.png')
    (out / 'crop.json').write_text(json.dumps({'box': box, 'frame': [W, H], 'size': SIZE,
                                               'drawScale': meta['drawScale']}, indent=2) + '\n')
    return out


def prep_back(ident, margin=0.1, force=False):
    """The back guide (units/<id>/back000.png, build_units.py --back-guide) cropped and upscaled like the front."""
    out = PAINT / ident
    if (out / 'back_crop.json').exists() and not force:
        return out
    frame = Image.open(REVIEW / 'units' / ident / 'back000.png').convert('RGBA')
    W, H = frame.size
    x0, y0, x1, y1 = frame.getchannel('A').point(lambda a: 255 if a > 8 else 0).getbbox()
    side = max(x1 - x0, y1 - y0) * (1 + 2 * margin)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    box = (round(cx - side / 2), round(cy - side / 2), round(cx + side / 2), round(cy + side / 2))
    crop = frame.crop(box).resize((SIZE, SIZE), Image.LANCZOS)
    guide = Image.new('RGBA', (SIZE, SIZE), GREY + (255,))
    guide.alpha_composite(crop)
    guide.convert('RGB').save(out / 'back_guide.png')
    crop.getchannel('A').save(out / 'back_guide_alpha.png')
    (out / 'back_crop.json').write_text(json.dumps({'box': box, 'frame': [W, H], 'size': SIZE}, indent=2) + '\n')
    return out


def back_prompt(ident, dest):
    title = UNITS.get(ident, {}).get('DisplayName', ident)
    return f"""Image 1 is a style reference sheet of fantasy game units. Image 2 is the finished painting of one unit, {title}, \
seen from the front. Image 3 is a 3D render of the SAME unit in the same pose, seen from BEHIND.
Use your image generation tool to create ONE new image: image 3 re-rendered at the quality and in the exact finish of \
image 2 - the same materials, colours, textures, wear and lighting as image 2, so the two read as one model.

Keep EVERYTHING else from image 3, unchanged: the exact pose, camera angle, framing, scale, proportions and \
silhouette (it is the back view: show the back of the head, the back of the armour and clothing, the cape or pack \
if any); every part keeps its own colour; do not add, remove or move any garment, emblem, weapon, prop or limb.
Plain flat light grey background (#DDD9D1) everywhere around the figure, no ground shadow, no text, no border, \
square 1024x1024.
Save the generated image to {dest} and reply with the path only."""


def paint_back(ident, model='gpt-5.6-sol', retries=1, force=False):
    out = PAINT / ident
    dest = out / 'back_painted.png'
    if dest.exists() and not force:
        return ident, 'exists'
    if not (REVIEW / 'units' / ident / 'back000.png').exists():
        return ident, 'no back guide (build_units.py --back-guide)'
    prep_back(ident)
    for attempt in range(retries + 1):
        t0 = time.time()
        p = subprocess.run(['codex', 'exec', '--skip-git-repo-check', '-m', model, '-c', 'model_reasoning_effort="low"',
                            '-s', 'workspace-write', '-C', str(out), '-i', str(REF), '-i', str(out / 'painted.png'),
                            '-i', str(out / 'back_guide.png'), '-'],
                           input=back_prompt(ident, dest), capture_output=True, text=True, timeout=1500)
        (out / 'codex_back.log').write_text(p.stdout[-6000:] + '\n--- stderr ---\n' + p.stderr[-3000:])
        if dest.exists():
            return ident, f'ok {time.time() - t0:.0f}s'
        if 'usage limit' in p.stdout + p.stderr:
            return ident, 'FAILED usage limit'
    return ident, 'FAILED (see codex_back.log)'


def prompt(ident, dest):
    u = UNITS.get(ident, {})
    title = u.get('DisplayName', ident)
    return f"""Image 1 is a style reference sheet of fantasy game units. Image 2 is a 3D render of one game unit, {title}.
Use your image generation tool to create ONE new image that is image 2 re-rendered at the quality of image 1.

Take ONLY the rendering quality from image 1: high-end stylised 3D game character rendering, rich hand-crafted texture \
detail (scratched and worn metal, creased leather, woven and frayed cloth, carved wood grain, aged bone, skin pores), \
crisp facial detail, soft warm studio key light from the upper left and a cool rim light.

Take EVERYTHING ELSE from image 2, unchanged:
- the exact same pose, camera angle, framing, scale, proportions and silhouette; nothing cropped;
- the exact same colour scheme: every part keeps its own hue and brightness. Do NOT recolour anything toward image \
1's palette (no new crimson, red or teal); greens stay green, purples stay purple, greys stay grey, orange glows stay \
orange, translucent or glassy parts stay translucent;
- the exact same equipment and clothing: do not add, remove or move any garment, tabard, emblem, hood, weapon, \
prop or limb, and do not reveal anything that image 2 covers. Glowing parts keep glowing in their own colour.

Plain flat light grey background (#DDD9D1) everywhere around the figure, no ground shadow, no text, no border, \
square 1024x1024.
Save the generated image to {dest} and reply with the path only."""


def paint(ident, model='gpt-5.6-sol', retries=1, force=False):
    out = PAINT / ident
    dest = out / 'painted.png'
    if dest.exists() and not force:
        return ident, 'exists'
    if dest.exists():
        n = 1
        while (out / f'painted_v{n}.png').exists():
            n += 1
        dest.rename(out / f'painted_v{n}.png')
    if not (out / 'guide.png').exists():
        prep(ident)
    for attempt in range(retries + 1):
        t0 = time.time()
        p = subprocess.run(['codex', 'exec', '--skip-git-repo-check', '-m', model, '-c', 'model_reasoning_effort="low"',
                            '-s', 'workspace-write', '-C', str(out), '-i', str(REF), '-i', str(out / 'guide.png'), '-'],
                           input=prompt(ident, dest), capture_output=True, text=True, timeout=1500)
        (out / 'codex.log').write_text(p.stdout[-6000:] + '\n--- stderr ---\n' + p.stderr[-3000:])
        if dest.exists():
            return ident, f'ok {time.time() - t0:.0f}s'
    return ident, 'FAILED (see codex.log)'


def sheet(ids, path, back=False):
    rows = []
    pre = 'back_' if back else ''
    for ident in ids:
        d = PAINT / ident
        if not (d / f'{pre}painted.png').exists():
            continue
        g = Image.open(d / f'{pre}guide.png').convert('RGB').resize((360, 360))
        p = Image.open(d / f'{pre}painted.png').convert('RGB').resize((360, 360))
        r = Image.new('RGB', (720, 380), GREY)
        r.paste(g, (0, 20))
        r.paste(p, (360, 20))
        ImageDraw.Draw(r).text((6, 4), ident, fill=(0, 0, 0))
        rows.append(r)
    if not rows:
        return
    cols = 2
    s = Image.new('RGB', (720 * cols, 380 * ((len(rows) + cols - 1) // cols)), GREY)
    for i, r in enumerate(rows):
        s.paste(r, ((i % cols) * 720, (i // cols) * 380))
    s.save(path)
    print(path)


def icon(ident, mode=None, size=1024):
    """The unit icon from its painting (cutout.png, written by the painted render): a head-and-shoulders crop for
    people, the whole figure for beasts, mounts and machines, on transparent ground."""
    from PIL import ImageChops
    d = PAINT / ident
    fig = Image.open(d / 'cutout.png').convert('RGBA')
    mask = fig.getchannel('A')
    x0, y0, x1, y1 = mask.point(lambda a: 255 if a > 40 else 0).getbbox()
    u = UNITS.get(ident, {})
    beast = mode == 'full' or u.get('VisualClass') in ('hound', 'ballista', 'siegetower', 'splitter') or ident in FULL_FIGURE
    if beast:
        side = max(x1 - x0, y1 - y0) * 1.06
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    else:
        hx, hy = head_of(ident, mask)
        side = (y1 - y0) * 0.5
        cx, cy = hx, hy + side * 0.24
    crop = fig.crop((round(cx - side / 2), round(cy - side / 2), round(cx + side / 2), round(cy + side / 2)))
    crop = crop.resize((size, size), Image.LANCZOS)
    if not beast:
        # bust: fade the bottom edge so cards frame it cleanly
        fade = Image.linear_gradient('L').resize((size, size))
        cut = fade.point(lambda v: 255 if v < 205 else max(0, 255 - (v - 205) * 5))
        crop.putalpha(ImageChops.multiply(crop.getchannel('A'), cut))
    dest = REVIEW / 'units' / ident / 'portrait.png'
    if (REVIEW / 'units' / ident / 'tone.json').exists():
        # the unit's frames already carry the roster-sheet tone (tone.py): give its icon the same
        import tone as T
        crop = T.full(crop, T.load())
    crop.save(dest)
    crop.save(d / 'icon.png')
    return dest


def head_of(ident, mask):
    """Where the head is in the painting: recorded by the painted render (rk/paint.py), else the top of the
    figure once thin parts (blades, poles, staves) are opened away."""
    from PIL import ImageFilter
    hp = PAINT / ident / 'head.json'
    if hp.exists():
        h = json.loads(hp.read_text())
        return h['x'], h['y']
    solid = mask.point(lambda a: 255 if a > 40 else 0).filter(ImageFilter.MinFilter(41)).filter(ImageFilter.MaxFilter(41))
    x0, y0, x1, y1 = solid.getbbox() or mask.getbbox()
    band = solid.crop((x0, y0, x1, round(y0 + (y1 - y0) * 0.08))).getbbox()
    hx = x0 + ((band[0] + band[2]) / 2 if band else (x1 - x0) / 2)
    return hx, y0 + (y1 - y0) * 0.07


FULL_FIGURE = {'player_ballista', 'player_hound', 'enemy_boss_mire', 'player_raider', 'player_outrider', 'enemy_bone_lancer', 'enemy_boss_steppe', 'enemy_plague_engine',
               'enemy_boneballista', 'enemy_reliquary_eye', 'enemy_swamp_maw', 'enemy_bog_spore', 'enemy_bloated_eel',
               'enemy_plague_leech', 'enemy_mirror_wisp', 'enemy_grave_rat', 'enemy_ash_hound', 'enemy_splitter'}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('action', choices=['prep', 'paint', 'sheet', 'icons', 'paint-back', 'sheet-back'])
    ap.add_argument('ids')
    ap.add_argument('--jobs', type=int, default=3)
    ap.add_argument('--out', default=str(PAINT / 'sheet.png'))
    ap.add_argument('--model', default='gpt-5.6-sol')
    ap.add_argument('--force', action='store_true', help='repaint (the old painting is kept as painted_vN.png)')
    a = ap.parse_args()
    ids = ids_arg(a.ids)
    if a.action == 'prep':
        for i in ids:
            print(prep(i))
    elif a.action == 'paint':
        for i in ids:
            prep(i)
        with cf.ThreadPoolExecutor(a.jobs) as ex:
            for ident, status in ex.map(lambda i: paint(i, a.model, force=a.force), ids):
                print('PAINT', ident, status, flush=True)
    elif a.action == 'paint-back':
        with cf.ThreadPoolExecutor(a.jobs) as ex:
            for ident, status in ex.map(lambda i: paint_back(i, a.model, force=a.force), ids):
                print('PAINT_BACK', ident, status, flush=True)
    elif a.action == 'sheet':
        sheet(ids, a.out)
    elif a.action == 'sheet-back':
        sheet(ids, a.out, back=True)
    else:
        for i in ids:
            print(icon(i))


if __name__ == '__main__':
    main()
