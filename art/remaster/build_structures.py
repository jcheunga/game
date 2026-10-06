"""Build and render the remastered battle structures.

blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_structures.py -- \
    --ids all|war_wagon,gatehouse,mount_ballista,... [--samples 128] [--no-blend] [--out DIR] [--sheet]

Outputs
  artifacts/remaster/structures/<id>.png        exact runtime canvas, RGBA transparent
  art/remaster/blend/structures/<id>.blend      editable source scene (unless --no-blend)
  artifacts/remaster/structures/contact.png     OLD vs NEW + in-game-size composite (--sheet, needs python3+Pillow)

CONTRACT (scripts/combat/BattleController.cs, BattleController.BaseWeapons.cs)
  war_wagon[_skin_*]  1440x1120, drawn into 180x140; wagon mounts are anchored at wagon
                      pixels x=400/616/832, y=336 and land on the deck front (y~490).
  gatehouse           1440x1280, drawn into 180x160; the stronghold mount is anchored at
                      gatehouse pixel (896, 304) and lands on the right bastion (y~459).
  mount_*             384x480, drawn into 60x75 around anchor pixel (192, 269); base at y~390;
                      mirrored horizontally on the enemy side. +X is the firing direction.
  Cameras/canvases are unchanged from the original assets so footprints line up. Lighting is the
  battle sun shared with the units (key from the upper left); no ground shadow is baked by default
  because the game projects silhouettes and draws contact shadows itself (--contact-shadow opts in).
  The battle-v2 runtime format is produced by build_battle_structures.py.
"""
import argparse
import subprocess
import sys
import time
import traceback
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import bpy  # noqa: E402

from rk import core, shaders  # noqa: E402
from siege import common  # noqa: E402

ROOT = HERE.parent.parent
SKINS = ['iron', 'royal', 'bone', 'flame', 'shadow', 'guild', 'legendary']
MOUNTS = ['arrows', 'ballista', 'firepot']
ALL = ['war_wagon'] + [f'war_wagon_skin_{s}' for s in SKINS] + ['gatehouse'] + [f'mount_{m}' for m in MOUNTS]


def build_one(ident, samples, out_dir, blend_dir, contact_shadow=False):
    catcher = None
    core.reset()
    shaders.reset_cache()
    if ident.startswith('war_wagon'):
        from siege import wagon
        skin = ident[len('war_wagon_skin_'):] if ident.startswith('war_wagon_skin_') else 'default'
        scene, cam, rig = common.scene_setup('wagon', samples)
        coll = core.collection('Lantern Caravan • ' + skin)
        info = wagon.build(skin, coll)
        common.light_rig(rig, (0.0, 0.0, 2.4), scale=2.3, **common.STRUCTURE_FILL)
        if contact_shadow:
            catcher = common.ground_catcher(rig, (0.1, 0.4, 0.0), (10.0, 6.4), fade=(0.55, 0.98))
    elif ident == 'gatehouse':
        from siege import gatehouse
        scene, cam, rig = common.scene_setup('gatehouse', samples)
        coll = core.collection('Rotbound gatehouse')
        info = gatehouse.build(coll)
        common.light_rig(rig, (0.0, 0.0, 2.0), scale=2.1, **common.STRUCTURE_FILL)
        if contact_shadow:
            catcher = common.ground_catcher(rig, (0.0, 0.0, 0.0), (6.6, 4.2), light_height=8.0, light_size=5.0, fade=(0.55, 0.98))
    elif ident.startswith('mount_'):
        from siege import mounts
        scene, cam, rig = common.scene_setup('mount', samples)
        coll = core.collection('Mount • ' + ident)
        info = mounts.build(ident[len('mount_'):], coll)
        common.light_rig(rig, (0.15, 0.0, 0.9), scale=1.0, fill_power=750, sky_power=650)
    else:
        raise KeyError(ident)
    common.exclude_local_lights(catcher)
    common.no_shadow(info.get('flames', []))
    for o in bpy.data.objects:
        if o.type == 'MESH' and any(k in o.name.lower() for k in ('flame', 'fire', 'tongue')):
            o.visible_shadow = False
    scene['asset_id'] = ident
    out = Path(out_dir) / f'{ident}.png'
    common.render_robust(scene, out, cam)
    common.clean_alpha(out)
    if blend_dir:
        core.save_blend(Path(blend_dir) / f'{ident}.blend')
    return out


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--ids', default='all')
    p.add_argument('--samples', type=int, default=128)
    p.add_argument('--out', default=str(ROOT / 'artifacts/remaster/structures'))
    p.add_argument('--no-blend', action='store_true')
    p.add_argument('--sheet', action='store_true')
    p.add_argument('--contact-shadow', action='store_true',
                   help='bake a soft contact shadow into the wagon/gatehouse renders (off: the game draws its own)')
    args = p.parse_args(core.args_after_dashes(sys.argv))
    ids = ALL if args.ids == 'all' else [i.strip() for i in args.ids.split(',') if i.strip()]
    blend_dir = None if args.no_blend else HERE / 'blend' / 'structures'
    failed = []
    for ident in ids:
        t0 = time.time()
        try:
            out = build_one(ident, args.samples, args.out, blend_dir, args.contact_shadow)
            print(f'STRUCTURE_COMPLETE {ident} {time.time() - t0:.1f}s -> {out}', flush=True)
        except Exception:
            traceback.print_exc()
            failed.append(ident)
            print('STRUCTURE_FAILED ' + ident, flush=True)
    if args.sheet:
        try:
            subprocess.run(['python3', str(HERE / 'structures_sheet.py'), '--dir', args.out], check=True)
        except Exception as exc:  # Pillow lives outside Blender's python
            print('SHEET_SKIPPED', exc, flush=True)
    if failed:
        sys.exit(1)


if __name__ == '__main__':
    main()
