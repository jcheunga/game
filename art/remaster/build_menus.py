"""Build the 23 menu backgrounds (1280x720 opaque PNG) as painted medieval dioramas.

blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_menus.py -- \
    [--ids all|forge,shop] [--samples 48] [--no-blend] [--scale 100] [--out preview_dir]

Each scene recipe composes rk.env (sky, haze, terrain, vegetation), rk.arch (buildings)
and rk.dressing (set dressing) around a calm central stage where the UI panels sit, then
renders with Cycles + OIDN and applies a light grade/bloom/vignette (rk.post).
Renders: artifacts/remaster/menus/<id>.png   Sources: art/remaster/blend/menus/<id>.blend
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
from menu_scenes import SCENES  # noqa: E402

OUT = core.REVIEW / 'menus'
BLEND = HERE / 'blend' / 'menus'
IDS = ['arena', 'battle_summary', 'bounty', 'cash_shop', 'codex', 'endless', 'event', 'expedition', 'forge',
       'friends', 'guild', 'lan_race', 'leaderboard', 'loadout', 'login_calendar', 'multiplayer', 'profile', 'raid',
       'season_pass', 'settings', 'shop', 'skill_tree', 'tower']


def build(ident, samples, save_blend, scale, out_dir=OUT):
    t0 = time.time()
    recipe = SCENES[ident]
    scene = env.menu_scene(samples=samples)
    grade = recipe(scene) or {}
    scene.render.resolution_percentage = scale
    t1 = time.time()
    raw = out_dir / 'raw' / f'{ident}.png'
    scene.render.image_settings.color_depth = '16'
    env.render_safe(scene, raw)
    t2 = time.time()
    post.finish(raw, out_dir / f'{ident}.png', **grade)
    if save_blend:
        scene.render.image_settings.color_depth = '8'
        scene['post_grade'] = repr(grade)
        txt = bpy.data.texts.new('README_menu')
        txt.write(f'Menu background "{ident}". Rebuild: art/remaster/build_menus.py --ids {ident}. The final PNG is this '
                  f'render passed through art/remaster/rk/post.py finish() with scene["post_grade"].\n')
        core.save_blend(BLEND / f'{ident}.blend')
    return t1 - t0, t2 - t1


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--ids', default='all')
    parser.add_argument('--samples', type=int, default=48)
    parser.add_argument('--scale', type=int, default=100)
    parser.add_argument('--no-blend', action='store_true')
    parser.add_argument('--out', default=None, help='write renders here instead of artifacts/remaster/menus (previews)')
    args = parser.parse_args(core.args_after_dashes(sys.argv))
    ids = IDS if args.ids == 'all' else args.ids.split(',')
    failed = []
    for ident in ids:
        try:
            tb, tr = build(ident, args.samples, not args.no_blend, args.scale,
                           Path(args.out) if args.out else OUT)
            print(f'MENU_COMPLETE {ident} build={tb:.1f}s render={tr:.1f}s', flush=True)
        except Exception:
            traceback.print_exc()
            print('MENU_FAILED ' + ident, flush=True)
            failed.append(ident)
    if failed:
        sys.exit(1)


main()
