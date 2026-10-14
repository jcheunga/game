"""Look-development renders: one unit's stance, tightly framed in the battle camera, beside its reference.

blender --background --factory-startup --python-exit-code 1 --python art/remaster/lookdev.py -- \
    --ids player_defender[,..] [--samples 64] [--size 900] [--frame 1] [--portrait]

Writes artifacts/remaster/lookdev/<id>.png (transparent render) and, when art/remaster/ref/<id>.png exists,
<id>-compare.png (reference | render on the reference's grey). Pass --frame to pose another frame
(1-based, e.g. an attack contact frame) and --portrait to also render the bust portrait camera.
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
from mathutils import Vector  # noqa: E402

from rk import core, shaders  # noqa: E402
from rk import character as CH  # noqa: E402
from roster import load_all  # noqa: E402

ROOT = HERE.parent.parent
OUT = core.REVIEW / 'lookdev'
parser = argparse.ArgumentParser()
parser.add_argument('--ids', required=True)
parser.add_argument('--samples', type=int, default=64)
parser.add_argument('--size', type=int, default=900)
parser.add_argument('--frame', type=str, default='1')
parser.add_argument('--portrait', action='store_true')
parser.add_argument('--tag', default='')
parser.add_argument('--save', action='store_true', help='also save the posed scene for relight.py')
args = parser.parse_args(core.args_after_dashes(sys.argv))

units = {u['Id']: u for u in json.loads((ROOT / 'data/units.json').read_text())['Units']}
registry = load_all()


def frame_of(ch, spec):
    """'12' -> frame 12; 'attack:4' -> local frame 4 of the attack clip."""
    if ':' in spec:
        clip, i = spec.split(':')
        return ch.clip_meta[clip]['start'] + 1 + int(i)
    return int(spec)


def render_stance(ch, path, frame, size, samples):
    scene = bpy.context.scene
    core.setup_render(scene, samples)
    CH.world_for(scene)
    CH.turn_toward_camera(scene)
    rc = core.collection('Camera and lighting')
    target = Vector((0.12, 0, 1.0))
    cam = core.ortho_camera('Lookdev camera', target + CH.CAM_DIR * 30, target, 4.0, rc)
    scene.camera = cam
    CH.light_rig(rc, Vector((0, 0, 1.1)))
    scene.frame_set(frame)
    objs = [o for o in bpy.data.objects if o.type == 'MESH' and not o.name.startswith('_')]
    # Tight framing: bounds of this frame only, centred.
    from bpy_extras.object_utils import world_to_camera_view
    scene.render.resolution_x = scene.render.resolution_y = size
    dg = bpy.context.evaluated_depsgraph_get()
    lo, hi = Vector((9, 9)), Vector((-9, -9))
    for o in objs:
        ev = o.evaluated_get(dg)
        if o.hide_render:
            continue
        for corner in ev.bound_box:
            p = world_to_camera_view(scene, cam, ev.matrix_world @ Vector(corner))
            lo.x, lo.y = min(lo.x, p.x), min(lo.y, p.y)
            hi.x, hi.y = max(hi.x, p.x), max(hi.y, p.y)
    span = max(hi.x - lo.x, hi.y - lo.y) * 1.08
    mid = (lo + hi) / 2
    # shift the camera so the figure is centred, then scale to fit
    right = cam.matrix_world.to_3x3() @ Vector((1, 0, 0))
    up = cam.matrix_world.to_3x3() @ Vector((0, 1, 0))
    cam.location += right * (mid.x - .5) * 4.0 + up * (mid.y - .5) * 4.0
    cam.data.ortho_scale = 4.0 * span
    core.render_to(scene, path, cam)
    return path


for ident in args.ids.split(','):
    t0 = time.time()
    try:
        core.reset()
        shaders.reset_cache()
        CH.set_facing()
        ch, opts = registry[ident](ident, units.get(ident, {}).get('DisplayName', ident))
        tag = ('-' + args.tag) if args.tag else ''
        out = OUT / f'{ident}{tag}.png'
        render_stance(ch, out, frame_of(ch, args.frame), args.size, args.samples)
        if args.save:
            core.save_blend(OUT / 'blend' / f'{ident}.blend')
        if args.portrait:
            CH.render_icon(ch, OUT / f'{ident}{tag}-icon.png', size=512, samples=args.samples)
        print(f'LOOKDEV_DONE {ident} {time.time() - t0:.1f}s', flush=True)
    except Exception:
        traceback.print_exc()
        print('LOOKDEV_FAILED ' + ident, flush=True)
