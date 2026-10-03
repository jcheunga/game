"""Build the 10 district map fallbacks (1280x960 opaque PNG, one per route id).

blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_maps.py -- \
    [--ids all|city,harbor] [--samples 64] [--no-blend] [--out preview_dir]

Each district is a tilted orthographic miniature (map_scenes/common.py): painted height-field terrain
with roads, rivers/coast and patchwork fields, forests, villages, walled towns and route landmarks.
Renders: artifacts/remaster/maps/<route>.png   Sources: art/remaster/blend/maps/<route>.blend
Set RK_DEVICE=CPU to render on the CPU (renders also fall back automatically on GPU out-of-memory).
"""
import argparse
import sys
import time
import traceback
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import bpy  # noqa: E402

from rk import core, env, post  # noqa: E402
from map_scenes import MAPS  # noqa: E402

OUT = core.REVIEW / 'maps'
BLEND = HERE / 'blend' / 'maps'
IDS = ['city', 'harbor', 'foundry', 'quarantine', 'thornwall', 'basilica', 'mire', 'steppe', 'gloamwood', 'citadel']


def build(ident, samples, save_blend, out_dir=OUT):
    t0 = time.time()
    scene = env.menu_scene(samples=samples, res=(1280, 960))
    grade = MAPS[ident](scene) or {}
    t1 = time.time()
    raw = out_dir / 'raw' / f'{ident}.png'
    scene.render.image_settings.color_depth = '16'
    env.render_safe(scene, raw)
    t2 = time.time()
    post.finish(raw, out_dir / f'{ident}.png', **grade)
    if save_blend:
        scene.render.image_settings.color_depth = '8'
        scene['post_grade'] = repr(grade)
        txt = bpy.data.texts.new('README_map')
        txt.write(f'District map "{ident}". Rebuild: art/remaster/build_maps.py --ids {ident}. Final PNG = this render '
                  'through art/remaster/rk/post.py finish() with scene["post_grade"].\n')
        core.save_blend(BLEND / f'{ident}.blend')
    return t1 - t0, t2 - t1


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--ids', default='all')
    parser.add_argument('--samples', type=int, default=64)
    parser.add_argument('--no-blend', action='store_true')
    parser.add_argument('--out', default=None, help='write renders here instead of artifacts/remaster/maps')
    args = parser.parse_args(core.args_after_dashes(sys.argv))
    ids = IDS if args.ids == 'all' else args.ids.split(',')
    failed = []
    for ident in ids:
        if ident not in MAPS:
            print('MAP_NO_RECIPE ' + ident, flush=True)
            failed.append(ident)
            continue
        try:
            tb, tr = build(ident, args.samples, not args.no_blend, Path(args.out) if args.out else OUT)
            print(f'MAP_COMPLETE {ident} build={tb:.1f}s render={tr:.1f}s', flush=True)
        except Exception:
            traceback.print_exc()
            print('MAP_FAILED ' + ident, flush=True)
            failed.append(ident)
    if failed:
        sys.exit(1)


main()
