"""Build and render the remastered item icons (spells, relics, rewards, meta).

Run from the repo root:
  blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_items.py -- \
      --ids all|id1,id2 [--category spells|relics|rewards|meta] [--samples 128] [--ss 2] [--no-blend] [--post-only]

Outputs
  artifacts/remaster/items/<category>/<id>.png   512x512 RGBA (bloom + separation shadow applied)
  artifacts/remaster/items/_raw/<category>/<id>.png  raw Cycles render (supersampled)
  art/remaster/blend/items/<id>.blend            editable source scene
  artifacts/remaster/items/contact.png           OLD vs NEW sheet (written by items_sheet.py)
"""
import argparse
import json
import subprocess
import sys
import time
import traceback
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import bpy  # noqa: E402

from rk import core, shaders  # noqa: E402
from icons import stage  # noqa: E402
from icons.base import REGISTRY, ORDER, Kit  # noqa: E402
import icons.spells  # noqa: E402,F401
import icons.relics  # noqa: E402,F401
import icons.rewards  # noqa: E402,F401
import icons.rewards_more  # noqa: E402,F401
import icons.meta  # noqa: E402,F401

ROOT = HERE.parent.parent
OUT = ROOT / 'artifacts/remaster/items'
BLEND = HERE / 'blend/items'

parser = argparse.ArgumentParser()
parser.add_argument('--ids', default='all')
parser.add_argument('--category', default='all')
parser.add_argument('--samples', type=int, default=128)
parser.add_argument('--res', type=int, default=512)
parser.add_argument('--ss', type=int, default=2, help='supersample factor (render res*ss, downsample in post)')
parser.add_argument('--no-blend', action='store_true')
parser.add_argument('--post-only', action='store_true', help='re-run post on existing raw renders')
parser.add_argument('--no-sheet', action='store_true')
args = parser.parse_args(core.args_after_dashes(sys.argv))

ids = ORDER if args.ids == 'all' else [i.strip() for i in args.ids.split(',') if i.strip()]
if args.category != 'all':
    ids = [i for i in ids if REGISTRY.get(i, ('?',))[0] == args.category]

DEFAULTS = dict(az=22.0, el=18.0, fill=0.84, lens=70.0, shift=(0.0, 0.0), bloom=0.55, threshold=0.62, shadow=0.38,
                key=1.0, rim=1.0, fill_light=1.0, world=1.0, exposure=0.0, glow_tint=None, aspect_bias=1.0,
                radii=(3, 9, 24), weights=(0.5, 0.35, 0.25), view='Khronos PBR Neutral', look='None')
POST_KEYS = ('bloom', 'threshold', 'shadow', 'glow_tint', 'radii', 'weights')


def check_render(path):
    """A shared, memory-starved GPU can return blank or opaque-black frames without raising; treat those as
    failures so the retry / CPU fallback kicks in."""
    a = stage.load_rgba(path)
    alpha = a[..., 3]
    cover = float((alpha > 0.5).mean())
    lum = float(a[..., :3][alpha > 0.5].mean()) if cover > 0 else 0.0
    if cover < 0.01 or cover > 0.97 or lum < 0.01:
        raise RuntimeError(f'suspicious render {path}: coverage={cover:.3f} luminance={lum:.3f}')


def build(ident, cpu=False):
    category, fn, deco_opts = REGISTRY[ident]
    core.reset()
    shaders.reset_cache()
    scene = bpy.context.scene
    core.setup_render(scene, samples=args.samples, transparent=True)
    if cpu:
        scene.cycles.device = 'CPU'
    scene.render.resolution_x = scene.render.resolution_y = args.res * args.ss
    scene.cycles.adaptive_threshold = 0.015
    scene.cycles.sample_clamp_indirect = 10.0
    coll = core.collection('Icon ' + ident)
    K = Kit(ident, coll)
    opts = dict(DEFAULTS)
    opts.update(deco_opts)
    ret = fn(K) or {}
    opts.update(ret)
    scene.view_settings.exposure = opts['exposure']
    try:
        scene.view_settings.view_transform = opts['view']
        scene.view_settings.look = opts['look']
    except TypeError as exc:
        print('VIEW_TRANSFORM_FALLBACK', exc)
    stage_coll = core.collection('Stage')
    objs = list(coll.all_objects)
    stage.rig(scene, stage_coll, az=opts['az'], el=opts['el'], key=opts['key'], rim=opts['rim'],
              fill=opts['fill_light'], world_strength=opts['world'], target=opts.get('light_target', (0, 0, 0)))
    cam = core.persp_camera('Icon camera', (0, -10, 0), (0, 0, 0), opts['lens'], stage_coll)
    stage.fit_camera(scene, cam, objs, az=opts['az'], el=opts['el'], fill=opts['fill'], lens=opts['lens'],
                     shift=opts['shift'], aspect_bias=opts['aspect_bias'])
    scene.camera = cam
    raw = OUT / '_raw' / category / f'{ident}.png'
    core.render_to(scene, raw, cam)
    check_render(raw)
    final = OUT / category / f'{ident}.png'
    stage.post(raw, final, bloom=opts['bloom'], threshold=opts['threshold'], shadow=opts['shadow'], ss=args.ss,
               glow_tint=opts['glow_tint'], radii=opts['radii'], weights=opts['weights'])
    post_opts = {k: opts[k] for k in POST_KEYS}
    raw.with_suffix('.json').write_text(json.dumps(post_opts))
    scene['icon_post'] = json.dumps(post_opts)
    if not args.no_blend:
        scene.render.filepath = str(final)
        core.save_blend(BLEND / f'{ident}.blend')
    return final


def post_only(ident):
    category, fn, deco_opts = REGISTRY[ident]
    opts = dict(DEFAULTS)
    raw = OUT / '_raw' / category / f'{ident}.png'
    side = raw.with_suffix('.json')
    if side.exists():
        opts.update(json.loads(side.read_text()))
    opts.update(deco_opts)  # post settings live on the @icon decorator, so edits there win
    final = OUT / category / f'{ident}.png'
    stage.post(raw, final, bloom=opts['bloom'], threshold=opts['threshold'], shadow=opts['shadow'], ss=args.ss,
               glow_tint=opts['glow_tint'], radii=opts['radii'], weights=opts['weights'])
    return final


failed = []
for ident in ids:
    if ident not in REGISTRY:
        print('ICON_MISSING_RECIPE ' + ident, flush=True)
        failed.append(ident)
        continue
    t0 = time.time()
    for attempt in range(3):
        try:
            # attempt 3 falls back to CPU: the GPU is shared with other renders
            path = post_only(ident) if args.post_only else build(ident, cpu=attempt == 2)
            print(f'ICON_COMPLETE {ident} {time.time() - t0:.1f}s -> {path}', flush=True)
            break
        except Exception:
            traceback.print_exc()
            print(f'ICON_RETRY {ident} attempt {attempt + 1}', flush=True)
            time.sleep(5)
    else:
        print('ICON_FAILED ' + ident, flush=True)
        failed.append(ident)

if not args.no_sheet:
    try:
        subprocess.run(['python3', str(HERE / 'items_sheet.py')], check=False)
    except OSError as exc:
        print('SHEET_SKIPPED', exc)

if failed:
    print('FAILED: ' + ','.join(failed))
    sys.exit(1)
