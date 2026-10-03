"""Build the 31 battlefield fallback backgrounds (1280x720 opaque PNG, one per TerrainId in data/stages.json).

blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_battlefields.py -- \
    [--ids all|grassland,urban] [--samples 64] [--no-blend] [--out preview_dir]

These sit behind the battle lane when no painted stage art exists (see battle_scenes/common.py for the
runtime layout): an orthographic 3/4 view whose central band (image rows 96..584) is quiet ground,
with route scenery behind the far edge (top band) and in front of the near edge (bottom band).
The image tiles in mirrored panels, so no vignette is applied.
Renders: artifacts/remaster/battlefields/<terrain>.png   Sources: art/remaster/blend/battlefields/<terrain>.blend
Set RK_DEVICE=CPU to render on the CPU (renders also fall back automatically on GPU out-of-memory).
"""
import argparse
import json
import sys
import time
import traceback
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import bpy  # noqa: E402

from rk import core, env, post  # noqa: E402
from battle_scenes import TERRAINS  # noqa: E402
from battle_scenes.common import relight  # noqa: E402

OUT = core.REVIEW / 'battlefields'
BLEND = HERE / 'blend' / 'battlefields'


def terrain_ids():
    stages = json.loads((core.ROOT / 'data/stages.json').read_text())['Stages']
    return sorted({s['TerrainId'] for s in stages})


def build(ident, samples, save_blend, out_dir=OUT):
    t0 = time.time()
    scene = env.menu_scene(samples=samples)
    grade = TERRAINS[ident](scene) or {}
    grade.setdefault('vignette', 0.0)
    relight(scene, ident, grade.pop('sun_strength', None))
    t1 = time.time()
    raw = out_dir / 'raw' / f'{ident}.png'
    scene.render.image_settings.color_depth = '16'
    env.render_safe(scene, raw)
    t2 = time.time()
    post.finish(raw, out_dir / f'{ident}.png', **grade)
    if save_blend:
        scene.render.image_settings.color_depth = '8'
        scene['post_grade'] = repr(grade)
        txt = bpy.data.texts.new('README_battlefield')
        txt.write(f'Battlefield fallback "{ident}". Rebuild: art/remaster/build_battlefields.py --ids {ident}. '
                  'Image rows 96..584 are covered by the procedural combat ground at runtime; only the top and '
                  'bottom bands show. Final PNG = this render through rk/post.py finish() with scene["post_grade"].\n')
        core.save_blend(BLEND / f'{ident}.blend')
    return t1 - t0, t2 - t1


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--ids', default='all')
    parser.add_argument('--samples', type=int, default=64)
    parser.add_argument('--no-blend', action='store_true')
    parser.add_argument('--out', default=None, help='write renders here instead of artifacts/remaster/battlefields')
    args = parser.parse_args(core.args_after_dashes(sys.argv))
    ids = terrain_ids() if args.ids == 'all' else args.ids.split(',')
    missing = [i for i in ids if i not in TERRAINS]
    if missing:
        print('BATTLEFIELD_NO_RECIPE ' + ','.join(missing), flush=True)
    failed = []
    for ident in ids:
        if ident not in TERRAINS:
            continue
        try:
            tb, tr = build(ident, args.samples, not args.no_blend, Path(args.out) if args.out else OUT)
            print(f'BATTLEFIELD_COMPLETE {ident} build={tb:.1f}s render={tr:.1f}s', flush=True)
        except Exception:
            traceback.print_exc()
            print('BATTLEFIELD_FAILED ' + ident, flush=True)
            failed.append(ident)
    if failed or missing:
        sys.exit(1)


main()
