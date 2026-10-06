"""Pack remastered renders into runtime assets.

python3 art/remaster/pack.py stage            # build artifacts/remaster/stage/assets/... (no game files touched)
python3 art/remaster/pack.py apply            # copy the staged files over assets/ (review first!)
python3 art/remaster/pack.py stage --only units,items

Runtime contracts follow art/blender/pack_assets.py: 256 px unit icons, 512 px Codex
portraits, shared visual-class fallbacks and Codex aliases. Unit atlases differ (see
density.py): large masters are cropped to the animation envelope and packed at
TARGET_DENSITY, or as close as the atlas limit allows, so frame size, column count,
drawScale and the normalised anchors vary per unit. The game reads all of these from the
JSON, and the model viewer reuses the cropped battle frames. Units that still qualify for
the legacy layout get 192x240 frames in an 8-column atlas.
"""
import argparse
import json
import math
import re
import shutil
from pathlib import Path

from PIL import Image

import density

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / 'artifacts/remaster'
STAGE = REVIEW / 'stage'
UNITS = json.loads((ROOT / 'data/units.json').read_text())['Units']
SPELLS = json.loads((ROOT / 'data/spells.json').read_text())['Spells']
RELICS = json.loads((ROOT / 'data/equipment.json').read_text())['Equipment']
CODEX = re.findall(r'new\("([^"]+)", "([^"]+)", "([^"]+)"', (ROOT / 'scripts/core/CodexCatalog.cs').read_text())
ALIASES = {
    'enemy_heavy': 'enemy_brute', 'enemy_exploder': 'enemy_bloater', 'enemy_ranged': 'enemy_spitter',
    'enemy_rusher': 'enemy_saboteur', 'enemy_buffer': 'enemy_howler', 'enemy_hexer': 'enemy_jammer', 'enemy_tank': 'enemy_crusher',
    'boss_grave_lord': 'enemy_boss', 'boss_tidecaller': 'enemy_boss_docks', 'boss_iron_warden': 'enemy_boss_forge',
    'boss_plague_archon': 'enemy_boss_ward', 'boss_thornwall': 'enemy_boss_pass', 'boss_bone_pontiff': 'enemy_boss_basilica',
    'boss_mire_behemoth': 'enemy_boss_mire', 'boss_steppe_warlord': 'enemy_boss_steppe',
    'boss_gloamwood_witch': 'enemy_boss_verge', 'boss_dread_sovereign': 'enemy_boss_citadel',
    'spell_frost': 'spell_frost_burst', 'spell_lightning': 'spell_lightning_strike', 'spell_barrier': 'spell_barrier_ward',
    'spell_barricade': 'spell_stone_barricade', 'spell_warcry': 'spell_war_cry',
}


def fit(src, size):
    image = Image.open(src).convert('RGBA')
    image.thumbnail((size, size), Image.Resampling.LANCZOS)
    canvas = Image.new('RGBA', (size, size))
    canvas.alpha_composite(image, ((size - image.width) // 2, (size - image.height) // 2))
    return canvas


def put(img_or_path, rel, size=None):
    target = STAGE / rel
    target.parent.mkdir(parents=True, exist_ok=True)
    if isinstance(img_or_path, Image.Image):
        img_or_path.save(target, optimize=True)
    elif size:
        fit(img_or_path, size).save(target, optimize=True)
    else:
        shutil.copy2(img_or_path, target)
    return target


HIRES_PAD = 6  # transparent atlas pixels around the envelope, for filtering and mipmaps


def atlas_columns(count, w, h, limit=density.MAX_ATLAS):
    """Column count with the smallest atlas inside the texture limit (8 when equally good)."""
    best = None
    for cols in range(1, count + 1):
        rows = math.ceil(count / cols)
        if cols * w <= limit and rows * h <= limit:
            key = (cols * w * rows * h, abs(cols - 8))
            best = min(best or (key, cols), (key, cols))
    return best and best[1]


def hires_frames(unit, meta, frames, envelope):
    """Crop to the animation envelope and resample to TARGET_DENSITY (less if the atlas limit forces it);
    returns frames, metadata, column count and the density reached."""
    sw, sh = meta['frameWidth'], meta['frameHeight']
    x0, y0, x1, y1 = envelope
    scale = density.TARGET_DENSITY * density.frame_canvas_width(unit, meta['drawScale']) / sw
    if scale > 1:
        print(f'WARN {unit["Id"]}: {sw}x{sh} masters are below the target density; '
              'rerender with build_units.py (automatic res scale)')
        scale = 1.0
    while True:
        w = math.ceil((x1 - x0) * scale) + 2 * HIRES_PAD
        h = math.ceil((y1 - y0) * scale) + 2 * HIRES_PAD
        cols = atlas_columns(len(frames), w, h)
        if cols:
            break
        scale *= 0.95
    # Crop box in master pixels covering exactly w x h output pixels, centred on the envelope.
    bw, bh = w / scale, h / scale
    bx, by = (x0 + x1 - bw) / 2, (y0 + y1 - bh) / 2
    m = math.ceil(max(bw - (x1 - x0), bh - (y1 - y0))) + 2
    out = []
    for frame in frames:
        padded = Image.new('RGBA', (sw + 2 * m, sh + 2 * m))
        padded.paste(frame, (m, m))
        out.append(padded.resize((w, h), Image.Resampling.LANCZOS, box=(bx + m, by + m, bx + m + bw, by + m + bh)))
    # Re-express the normalised frame coordinates against the crop box.
    motion = dict(meta['motion'])
    for key in ('body', 'contact'):
        motion[key] = [round(motion[key][0] * sw / bw, 6), round(motion[key][1] * sh / bh, 6)]
    animations = {k: dict(v) for k, v in meta['animations'].items()}
    death = animations.get('death', {})
    if 'impactPoint' in death:
        death['impactPoint'] = [round(death['impactPoint'][0] * sw / bw, 6), round(death['impactPoint'][1] * sh / bh, 6)]
    packed = dict(meta, frameWidth=w, frameHeight=h, drawScale=meta['drawScale'] * bw / sw,
                  anchorX=round((meta['anchorX'] * sw - bx) / bw, 6),
                  anchorY=round((meta['anchorY'] * sh - by) / bh, 6),
                  healthBarY=round(meta['healthBarY'] * sh / bh, 6), motion=motion, animations=animations)
    reached = scale * sw / density.frame_canvas_width(unit, meta['drawScale'])
    return out, packed, cols, reached


def pack_units():
    packed, clipped, hires = [], [], {}
    for u in UNITS:
        ident = u['Id']
        src = REVIEW / 'units' / ident
        if not (src / 'complete.json').exists():
            continue
        meta = json.loads((src / 'metadata.json').read_text())
        sw, sh = meta['frameWidth'], meta['frameHeight']
        count = sum(a['count'] for a in meta['animations'].values())
        frames, envelope = [], None
        for i in range(count):
            frame = Image.open(src / f'{i:03}.png').convert('RGBA')
            if frame.size != (sw, sh):
                raise ValueError(f'{ident}: frame {i} size {frame.size}')
            bounds = frame.getchannel('A').point(lambda x: 255 if x > 8 else 0).getbbox()
            if not bounds:
                raise ValueError(f'{ident}: empty frame {i}')
            if i == 0:
                meta['healthBarY'] = round(meta['anchorY'] - bounds[1] / sh, 6)
            if bounds[0] == 0 or bounds[1] == 0 or bounds[2] == sw or bounds[3] == sh:
                clipped.append({'id': ident, 'frame': i, 'bbox': bounds})
            envelope = bounds if envelope is None else (min(envelope[0], bounds[0]), min(envelope[1], bounds[1]),
                                                        max(envelope[2], bounds[2]), max(envelope[3], bounds[3]))
            frames.append(frame)
        if density.needs_hires(u, meta['drawScale']):
            cells, out, cols, reached = hires_frames(u, meta, frames, envelope)
            w, h = out['frameWidth'], out['frameHeight']
            if reached < density.TARGET_DENSITY - 0.05:
                print(f'NOTE {ident}: the {density.MAX_ATLAS} px atlas limit holds it to density {reached:.2f}')
            hires[ident] = {'frame': [w, h], 'columns': cols, 'master': [sw, sh],
                            'density': round(reached, 2),
                            'was': round(density.standard_density(u, meta['drawScale']), 2)}
        else:
            w, h, cols = *density.STANDARD_FRAME, 8
            cells = [frame.resize((w, h), Image.Resampling.LANCZOS) for frame in frames]
            out = dict(meta, frameWidth=w, frameHeight=h)
        sheet = Image.new('RGBA', (w * cols, h * math.ceil(count / cols)))
        for i, cell in enumerate(cells):
            sheet.paste(cell, ((i % cols) * w, (i // cols) * h))
        put(sheet, f'assets/units/{ident}.png')
        (STAGE / f'assets/units/{ident}.json').write_text(json.dumps(out, indent=2) + '\n')
        # Model-viewer preview: native 256x320 idle/walk/attack, no upscaling. Hi-res units reuse
        # their cropped battle frames, which are already sharper than a 256x320 master.
        pm = dict(meta if ident not in hires else out)
        pm['animations'] = {k: v for k, v in meta['animations'].items() if k in ('idle', 'walk', 'attack')}
        pcount = max(c['start'] + c['count'] for c in pm['animations'].values())
        pw, ph = (sw, sh) if ident not in hires else (w, h)
        prev = Image.new('RGBA', (pw * 5, ph * math.ceil(pcount / 5)))
        for i in range(pcount):
            prev.paste(frames[i] if ident not in hires else cells[i], ((i % 5) * pw, (i // 5) * ph))
        put(prev, f'assets/ui/models/{ident}.png')
        (STAGE / f'assets/ui/models/{ident}.json').write_text(json.dumps(pm, indent=2) + '\n')
        put(src / 'portrait.png', f'assets/ui/icons/units/{ident}.png', 256)
        put(src / 'portrait.png', f'assets/ui/portraits/codex/{ident}.png', 512)
        packed.append(ident)
    reps = {}
    for u in UNITS:
        if u['Id'] in packed:
            reps.setdefault(u['VisualClass'], u['Id'])
    for cls, ident in reps.items():
        for suffix in ('.png', '.json'):
            shutil.copy2(STAGE / f'assets/units/{ident}{suffix}', STAGE / f'assets/units/{cls}{suffix}')
    print(json.dumps({'units': len(packed), 'class_fallbacks': len(reps), 'clipped_frames': len(clipped),
                      'hires': len(hires)}))
    (REVIEW / 'unit-framing-report.json').write_text(json.dumps({'packed': packed, 'clipped': clipped, 'hires': hires},
                                                                indent=2) + '\n')
    return packed


def pack_items():
    n = 0
    for cat in ('spells', 'relics', 'rewards', 'meta'):
        for src in sorted((REVIEW / 'items' / cat).glob('*.png')) if (REVIEW / 'items' / cat).exists() else []:
            if src.stem == 'contact':
                continue
            put(src, f'assets/ui/icons/{cat}/{src.name}', 512)
            n += 1
    print(json.dumps({'item_icons': n}))


def pack_structures():
    sizes = {'war_wagon': (1440, 1120), 'gatehouse': (1440, 1280)}
    n = 0
    folder = REVIEW / 'structures'
    for src in sorted(folder.glob('*.png')) if folder.exists() else []:
        if src.stem.startswith('contact'):
            continue
        want = sizes.get(src.stem, (1440, 1120) if src.stem.startswith('war_wagon') else (384, 480))
        im = Image.open(src)
        if im.size != want:
            print('SKIP size mismatch', src.name, im.size, want)
            continue
        put(src, f'assets/structures/{src.name}')
        n += 1
    # Battle presentation (assets/structures/battle-v2): 1024x1024 sprite + projected attachment JSON.
    v2 = folder / 'battle-v2'
    m = 0
    for src in sorted(v2.glob('*.png')) if v2.exists() else []:
        meta = src.with_suffix('.json')
        if src.stem.startswith('contact') or not meta.exists():
            continue
        if Image.open(src).size != (1024, 1024):
            print('SKIP size mismatch', src.name)
            continue
        put(src, f'assets/structures/battle-v2/{src.name}')
        put(meta, f'assets/structures/battle-v2/{meta.name}')
        m += 1
    print(json.dumps({'structures': n, 'battle_v2': m}))


def pack_simple(folder, rel, size):
    if not (ROOT / rel).exists():
        # The game no longer ships this category (e.g. menu/map backdrops were retired).
        print(json.dumps({folder: 'skipped: ' + rel + ' is not part of the game'}))
        return
    n = 0
    src_dir = REVIEW / folder
    for src in sorted(src_dir.glob('*.png')) if src_dir.exists() else []:
        if src.stem.startswith('contact'):
            continue
        if Image.open(src).size != size:
            print('SKIP size mismatch', src.name)
            continue
        put(src, f'{rel}/{src.name}')
        n += 1
    print(json.dumps({folder: n}))


def pack_codex():
    """Codex icons/portraits resolve through aliases to the staged (or existing) art."""
    missing = []
    for ident, kind, _ in CODEX:
        resolved = ALIASES.get(ident, ident)
        cat = 'units' if kind in ('unit', 'enemy', 'boss') else 'spells' if kind == 'spell' else 'relics'
        icon = STAGE / f'assets/ui/icons/{cat}/{resolved}.png'
        portrait = STAGE / f'assets/ui/portraits/codex/{resolved}.png'
        if not icon.exists():
            missing.append(ident)
            continue
        put(icon, f'assets/ui/icons/codex/{ident}.png')
        src = portrait if portrait.exists() else icon
        if src != STAGE / f'assets/ui/portraits/codex/{ident}.png':
            put(src, f'assets/ui/portraits/codex/{ident}.png', 512)
    print(json.dumps({'codex_entries': len(CODEX) - len(missing), 'codex_unresolved_in_stage': len(missing)}))


def apply(allow_new=False):
    # Only shipped art/metadata; never Godot .import sidecars (they carry UIDs). By default only
    # files the game already ships are replaced, so retired assets are never resurrected.
    files = [p for p in STAGE.rglob('*') if p.is_file() and p.suffix in ('.png', '.json')]
    applied, skipped = 0, []
    for p in files:
        target = ROOT / p.relative_to(STAGE)
        if not target.exists() and not allow_new:
            skipped.append(str(p.relative_to(STAGE)))
            continue
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(p, target)
        applied += 1
    print(json.dumps({'applied_files': applied, 'skipped_not_in_game': len(skipped), 'examples': skipped[:5]}))


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('action', choices=['stage', 'apply'])
    ap.add_argument('--only', default='units,items,structures,particles,codex')
    ap.add_argument('--allow-new', action='store_true', help='also create files the game does not ship yet')
    a = ap.parse_args()
    if a.action == 'apply':
        apply(a.allow_new)
    else:
        only = a.only.split(',')
        if 'units' in only:
            pack_units()
        if 'items' in only:
            pack_items()
        if 'structures' in only:
            pack_structures()
        if 'particles' in only:
            pack_simple('particles', 'assets/particles', (128, 128))
        if 'codex' in only:
            pack_codex()
