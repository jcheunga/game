"""Battle-v2 presentation of the remastered caravan, its skins and the gatehouse.

blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_battle_structures.py -- \
    [--ids all|war_wagon,gatehouse,...] [--samples 128] [--metadata-only] [--no-blend] [--out DIR]

Writes <out>/<id>.png (1024x1024 RGBA) + <id>.json in the runtime format read by
scripts/combat/BattleStructureArt.cs (see assets/structures/battle-v2/README.md):
  * one orthographic camera at 22 degrees: (0,-32,15.2) -> (0,0,2.25), ortho 8.25 wagons / 6.25 gatehouse
  * caravan in a horizontal side view; gatehouse turned -62 degrees about Z toward troops on its left
  * battle sun from the upper left (same rig as the units); NO baked ground or contact shadow
    (the game projects the alpha silhouette down-right and draws contacts and lamp pools itself)
  * silhouette inside the 1.5% safe frame (checked on the rendered alpha)
JSON points are normalised image coordinates projected with world_to_camera_view from the
remastered models' own geometry (wheel hubs, deck, wall-walk, lantern and brazier positions).
Editable scenes are saved to art/remaster/blend/structures/battle-v2/<id>.blend.
Default output: artifacts/remaster/structures/battle-v2/ (never assets/).
"""
import argparse
import json
import math
import sys
import time
import traceback
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import bpy  # noqa: E402
from bpy_extras.object_utils import world_to_camera_view  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

from rk import core, shaders  # noqa: E402
from siege import common  # noqa: E402

ROOT = HERE.parent.parent
SKINS = ['iron', 'royal', 'bone', 'flame', 'shadow', 'guild', 'legendary']
PROPS = ['fort_tower', 'fort_wall', 'fort_palisade', 'fort_brazier', 'fort_totem']   # siege/fortifications.py
ALL = ['war_wagon'] + [f'war_wagon_skin_{s}' for s in SKINS] + ['gatehouse'] + PROPS
PROP_PAD = 6
GATE_YAW = -62.0
SAFE = 0.015
GENERATOR = 'art/remaster/build_battle_structures.py'


def project(scene, cam, p):
    q = world_to_camera_view(scene, cam, Vector(p))
    return [round(q.x, 6), round(1 - q.y, 6)]


def wagon_points(info):
    from siege import wagon as Wg
    near, far = -Wg.WY, Wg.WY
    return dict(
        anchor=(0.0, near, 0.0),                                         # near-wheel ground line, wagon centre
        mounts=[(x, -0.25, Wg.DECK) for x in (-1.85, -0.6, 0.65)],        # deck sockets, clear of the shrine cupola
        contacts=[(x, y, 0.0) for y in (far, near) for x in Wg.WXS],      # four iron tyres on the ground
        lights=[(p.x, p.y, 0.0) for p in info['lamps']],                  # ground under the hanging lanterns
        smoke=(-0.5, -0.2, 3.0),                                          # body/deck, behind the arrow mount
        width=210,
    )


def gatehouse_points(R):
    from siege import gatehouse as G

    def r(p):
        return tuple(R @ Vector(p))
    footprint = [(x, y, 0.0) for x in (G.GX0, G.GX1) for y in (G.GY0 - 0.09, G.GY1)]
    footprint += [(x, y, 0.0) for x in (G.CX0, G.CX1 + 0.08) for y in (G.CY0 - 0.08, G.CY1)]
    footprint += [tuple(G.LT + Vector((math.cos(a), math.sin(a), 0)) * (G.LT_R + 0.12)) for a in
                  (i * math.tau / 24 for i in range(24))]
    front = min((R @ Vector(p)).y for p in footprint)
    return dict(
        anchor=(0.0, front, 0.0),                                         # front-most foundation line
        mounts=[r((0.8, 0.12, G.WALK))],                                  # curtain wall-walk
        contacts=[r((G.LT.x, G.LT.y, 0.0)), r(((G.GX0 + G.GX1) / 2, (G.GY0 + G.GY1) / 2, 0.0)),
                  r(((G.CX0 + G.CX1) / 2, (G.CY0 + G.CY1) / 2, 0.0))],
        lights=[r((G.GATE_C + sx * 0.78, G.GY0 - 0.4, 0.0)) for sx in (-1, 1)],  # gate braziers
        smoke=r((G.GATE_C, 0.05, G.GTOP + 0.3)),                           # gate-tower crown
        width=216,
    )


DOOR_FRAMES = (0.45, 1.0)     # ramp-door states rendered after the closed plate (0 = shut, 1 = lowered)


def build_wagon_scene(skin, samples, door=0.0):
    from siege import wagon
    core.reset()
    shaders.reset_cache()
    scene, cam, rig = common.scene_setup('battle_wagon', samples)
    coll = core.collection('Lantern Caravan • ' + skin)
    info = wagon.build(skin, coll, door=door)
    common.light_rig(rig, (0.0, 0.0, 2.4), scale=2.3, **common.STRUCTURE_FILL)
    common.no_shadow(info.get('flames', []))
    bpy.context.view_layer.update()
    return scene, cam, info


def read_rgba(path):
    import numpy as np
    img = bpy.data.images.load(str(path))
    w, h = img.size
    px = np.empty(len(img.pixels), dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(h, w, 4)[::-1].copy()      # rows top-down


def write_rgba(path, rgba):
    import numpy as np
    h, w = rgba.shape[:2]
    img = bpy.data.images.new(Path(path).stem, w, h, alpha=True)
    img.pixels.foreach_set(np.ascontiguousarray(rgba[::-1]).ravel())
    img.filepath_raw = str(path)
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


def render_door_strip(ident, skin, samples, out, closed_png):
    """Render the ramp door's opening states and keep only the region that differs from the closed
    plate, as a horizontal strip the runtime draws over it. Returns the door metadata."""
    import numpy as np
    from siege import wagon
    base = read_rgba(closed_png)
    frames = []
    for k, door in enumerate(DOOR_FRAMES):
        scene, cam, _ = build_wagon_scene(skin, samples, door)
        tmp = out / f'{ident}_door{k}.tmp.png'
        common.render_robust(scene, tmp, cam)
        common.clean_alpha(tmp)
        frames.append(read_rgba(tmp))
        tmp.unlink()
    diff = np.zeros(base.shape[:2], dtype=bool)
    for f in frames:
        diff |= (np.abs(f - base) > 12 / 255).any(axis=2)
    diff = settled_change(diff)
    ys, xs = np.nonzero(diff)
    h, w = base.shape[:2]
    pad = 10
    x0, x1 = max(0, xs.min() - pad), min(w, xs.max() + 1 + pad)
    y0, y1 = max(0, ys.min() - pad), min(h, ys.max() + 1 + pad)
    strip = np.concatenate([f[y0:y1, x0:x1] for f in frames], axis=1)
    write_rgba(out / f'{ident}_door.png', strip)
    return dict(rect=[round(x0 / w, 6), round(y0 / h, 6), round(x1 / w, 6), round(y1 / h, 6)], frames=len(frames),
                exit=project(scene, cam, wagon.DOOR_EXIT), foot=project(scene, cam, wagon.DOOR_FOOT))


def settled_change(mask):
    """Drop isolated sampling-noise specks (erode 3x3), then grow what is left back (dilate 9x9), so the
    door crop covers the door and the light it throws, not render noise spread over the whole plate."""
    import numpy as np

    def shift_reduce(m, r, op):
        out = m.copy()
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                out = op(out, np.roll(np.roll(m, dy, 0), dx, 1))
        return out
    return shift_reduce(shift_reduce(mask, 1, np.logical_and), 4, np.logical_or)


def build_one(ident, samples, out_dir, blend_dir, metadata_only):
    core.reset()
    shaders.reset_cache()
    if ident.startswith('war_wagon'):
        skin = ident[len('war_wagon_skin_'):] if ident.startswith('war_wagon_skin_') else 'default'
        scene, cam, info = build_wagon_scene(skin, samples)
        pts = wagon_points(info)
        R = Matrix.Identity(4)
    elif ident.startswith('fort_'):
        # outworks share the gatehouse camera and scale, so they stand beside it as one fortress
        from siege import fortifications
        scene, cam, rig = common.scene_setup('battle_gatehouse', samples)
        coll = core.collection('Rotbound outworks • ' + ident)
        info = fortifications.build(ident, coll)
        common.light_rig(rig, (0.0, 0.0, 1.4), scale=2.1, **common.STRUCTURE_FILL)
        pts = dict(anchor=info['anchor'], mounts=[], contacts=[], lights=info['lights'], smoke=info['anchor'], width=216)
        R = Matrix.Identity(4)
    else:
        from siege import gatehouse
        scene, cam, rig = common.scene_setup('battle_gatehouse', samples)
        coll = core.collection('Rotbound gatehouse')
        info = gatehouse.build(coll, yaw=GATE_YAW)
        R = Matrix.Rotation(math.radians(GATE_YAW), 4, 'Z')
        # turn the whole model (meshes and its own lamps) about the world origin; `rest`
        # coordinates stay in model space so procedural surfaces do not swim
        for o in coll.all_objects:
            o.matrix_world = R @ o.matrix_world
        common.light_rig(rig, (0.0, 0.0, 2.0), scale=2.1, **common.STRUCTURE_FILL)
        pts = gatehouse_points(R)
    common.no_shadow(info.get('flames', []))
    for o in bpy.data.objects:
        if o.type == 'MESH' and any(k in o.name.lower() for k in ('flame', 'fire', 'tongue')):
            o.visible_shadow = False
    bpy.context.view_layer.update()
    blend = (Path(blend_dir) / f'{ident}.blend') if blend_dir else None
    meta = {
        'source': str(blend.relative_to(ROOT)) if blend else f'art/remaster/siege ({ident})',
        'generator': GENERATOR,
        'cameraElevation': 22,
        'anchor': project(scene, cam, pts['anchor']),
        'mounts': [project(scene, cam, p) for p in pts['mounts']],
        'contacts': [project(scene, cam, p) for p in pts['contacts']],
        'width': pts['width'],
        'lights': [project(scene, cam, p) for p in pts['lights']],
        'smoke': project(scene, cam, pts['smoke']),
    }
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    png = out / f'{ident}.png'
    if not metadata_only:
        scene['asset_id'] = ident
        scene['battle_v2'] = json.dumps(meta)
        common.render_robust(scene, png, cam)
        common.clean_alpha(png)
        bbox = alpha_bbox(png)
        if bbox is None or bbox[0] < SAFE or bbox[1] < SAFE or bbox[2] > 1 - SAFE or bbox[3] > 1 - SAFE:
            raise RuntimeError(f'{ident}: silhouette {bbox} outside the {SAFE:.1%} safe frame')
        print(f'SAFE_FRAME {ident} bbox={[round(v, 4) for v in bbox]}', flush=True)
        meta['centre'] = alpha_centroid(png)
        if ident.startswith('fort_'):
            crop_to_silhouette(png, meta, bbox)
        if ident.startswith('war_wagon'):
            if blend:
                core.save_blend(blend)
                blend = None
            meta['door'] = render_door_strip(ident, skin, samples, out, png)
        if blend:
            core.save_blend(blend)
    if metadata_only:
        if ident.startswith('fort_'):
            raise RuntimeError(f'{ident}: props are cropped after rendering; --metadata-only cannot place their points')
        previous = out / f'{ident}.json'
        if previous.exists():
            # door frames and the centroid come from rendered pixels; keep them from the last render
            old = json.loads(previous.read_text())
            meta.update({k: old[k] for k in ('centre', 'door') if k in old})
    (out / f'{ident}.json').write_text(json.dumps(meta, indent=2) + '\n')
    return png


def crop_to_silhouette(png, meta, bbox):
    """Props are small on the shared 1024 canvas: keep only their silhouette (plus a margin) and
    re-express every normalised point, and the world width, against the cropped image."""
    rgba = read_rgba(png)
    h, w = rgba.shape[:2]
    x0, y0 = max(0, int(bbox[0] * w) - PROP_PAD), max(0, int(bbox[1] * h) - PROP_PAD)
    x1, y1 = min(w, int(math.ceil(bbox[2] * w)) + PROP_PAD), min(h, int(math.ceil(bbox[3] * h)) + PROP_PAD)
    write_rgba(png, rgba[y0:y1, x0:x1])

    def remap(p):
        return [round((p[0] * w - x0) / (x1 - x0), 6), round((p[1] * h - y0) / (y1 - y0), 6)]
    for key in ('anchor', 'smoke', 'centre'):
        meta[key] = remap(meta[key])
    meta['lights'] = [remap(p) for p in meta['lights']]
    meta['width'] = round(meta['width'] * (x1 - x0) / w, 3)


def alpha_centroid(path):
    """Alpha-weighted centroid (x, y), normalised with image y down: where a structure's visual mass sits."""
    import numpy as np
    img = bpy.data.images.load(str(path))
    w, h = img.size
    px = np.empty(len(img.pixels), dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    a = px.reshape(h, w, 4)[::-1, :, 3]
    total = a.sum()
    ys, xs = np.mgrid[0:h, 0:w]
    return [round(float((xs * a).sum() / total / w), 6), round(float((ys * a).sum() / total / h), 6)]


def alpha_bbox(path, threshold=0.03):
    """Normalised (x0, y0, x1, y1) of pixels with alpha above threshold, image y down."""
    import numpy as np
    img = bpy.data.images.load(str(path))
    w, h = img.size
    px = np.empty(len(img.pixels), dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    a = px.reshape(h, w, 4)[::-1, :, 3]          # Blender rows start at the bottom
    ys, xs = np.nonzero(a > threshold)
    if len(xs) == 0:
        return None
    return (xs.min() / w, ys.min() / h, (xs.max() + 1) / w, (ys.max() + 1) / h)


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--ids', default='all')
    p.add_argument('--samples', type=int, default=128)
    p.add_argument('--out', default=str(ROOT / 'artifacts/remaster/structures/battle-v2'))
    p.add_argument('--metadata-only', action='store_true')
    p.add_argument('--no-blend', action='store_true')
    args = p.parse_args(core.args_after_dashes(sys.argv))
    ids = ALL if args.ids == 'all' else [i.strip() for i in args.ids.split(',') if i.strip()]
    blend_dir = None if args.no_blend else HERE / 'blend' / 'structures' / 'battle-v2'
    failed = []
    for ident in ids:
        t0 = time.time()
        try:
            out = build_one(ident, args.samples, args.out, blend_dir, args.metadata_only)
            print(f'BATTLE_STRUCTURE_COMPLETE {ident} {time.time() - t0:.1f}s -> {out}', flush=True)
        except Exception:
            traceback.print_exc()
            failed.append(ident)
            print('BATTLE_STRUCTURE_FAILED ' + ident, flush=True)
    if failed:
        sys.exit(1)


if __name__ == '__main__':
    main()
