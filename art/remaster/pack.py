"""Pack remastered renders into runtime assets.

python3 art/remaster/pack.py stage            # build artifacts/remaster/stage/assets/... (no game files touched)
python3 art/remaster/pack.py apply            # copy the staged files over assets/ (review first!)
python3 art/remaster/pack.py stage --only units,items

Runtime contracts are identical to art/blender/pack_assets.py: 192x240 battle frames
in an 8-column atlas, 256x320 model-viewer frames in a 5-column atlas, 256 px unit
icons, 512 px Codex portraits, shared visual-class fallbacks and Codex aliases.
"""
import argparse
import json
import math
import re
import shutil
from pathlib import Path

from PIL import Image

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


def pack_units():
    packed, clipped = [], []
    for u in UNITS:
        ident = u['Id']
        src = REVIEW / 'units' / ident
        if not (src / 'complete.json').exists():
            continue
        meta = json.loads((src / 'metadata.json').read_text())
        sw, sh = meta['frameWidth'], meta['frameHeight']
        w, h = 192, 240
        count = sum(a['count'] for a in meta['animations'].values())
        sheet = Image.new('RGBA', (w * 8, h * math.ceil(count / 8)))
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
            sheet.paste(frame.resize((w, h), Image.Resampling.LANCZOS), ((i % 8) * w, (i // 8) * h))
        out = dict(meta, frameWidth=w, frameHeight=h)
        put(sheet, f'assets/units/{ident}.png')
        (STAGE / f'assets/units/{ident}.json').write_text(json.dumps(out, indent=2) + '\n')
        # Model-viewer preview: native 256x320 idle/walk/attack, no upscaling.
        pm = dict(meta)
        pm['animations'] = {k: v for k, v in meta['animations'].items() if k in ('idle', 'walk', 'attack')}
        pcount = max(c['start'] + c['count'] for c in pm['animations'].values())
        prev = Image.new('RGBA', (sw * 5, sh * math.ceil(pcount / 5)))
        for i in range(pcount):
            prev.paste(Image.open(src / f'{i:03}.png').convert('RGBA'), ((i % 5) * sw, (i // 5) * sh))
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
    print(json.dumps({'units': len(packed), 'class_fallbacks': len(reps), 'clipped_frames': len(clipped)}))
    (REVIEW / 'unit-framing-report.json').write_text(json.dumps({'packed': packed, 'clipped': clipped}, indent=2) + '\n')
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


def apply():
    # Only shipped art/metadata; never Godot .import sidecars (they carry UIDs).
    files = [p for p in STAGE.rglob('*') if p.is_file() and p.suffix in ('.png', '.json')]
    for p in files:
        target = ROOT / p.relative_to(STAGE)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(p, target)
    print(json.dumps({'applied_files': len(files)}))


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('action', choices=['stage', 'apply'])
    ap.add_argument('--only', default='units,items,structures,particles,menus,battlefields,maps,codex')
    a = ap.parse_args()
    if a.action == 'apply':
        apply()
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
        if 'menus' in only:
            pack_simple('menus', 'assets/ui/backgrounds', (1280, 720))
        if 'battlefields' in only:
            pack_simple('battlefields', 'assets/backgrounds', (1280, 720))
        if 'maps' in only:
            pack_simple('maps', 'assets/map/backgrounds', (1280, 960))
        if 'codex' in only:
            pack_codex()
