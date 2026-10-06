"""Render the physical projectile sprites: shots seen side-on, lit like the units.

blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_projectiles.py -- \
    [--ids all|arrow,bolt] [--samples 64]

Long shots (arrows, bolts, harpoons, shards) are modelled along +X with the tip at +X; the game turns
them along their flight path. Thrown shots (flasks, pots, cogs, skulls, globs) are modelled upright and
spun or swayed in flight. Each sprite is a transparent PNG in assets/projectiles/, and
assets/projectiles/projectiles.json records its size, the normalised tip and centre, and its real length,
which ProjectileStyles.cs uses to scale and align it. Magic shots are drawn in game from the particle
textures and have no sprite here.
"""
import argparse
import json
import math
import random
import shutil
import sys
import time
import traceback
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import bpy  # noqa: E402
from bpy_extras.object_utils import world_to_camera_view  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

from rk import core, geo, heads as H, palette, shaders as S, weapons as W  # noqa: E402
from rk.character import light_rig  # noqa: E402
from rk.palette import LANTERN_TEAL, PLAGUE, SOUL  # noqa: E402

OUT = core.ROOT / 'assets/projectiles'
REVIEW = core.REVIEW / 'projectiles'
PX_PER_M = 560          # sprite pixels per metre of projectile
ELEVATION = 10.0        # degrees above side-on, enough to show the fletching's depth
ALONG_X = Matrix.Rotation(math.radians(90), 4, 'Y')   # builders model along +Z; long shots fly along +X
# Vane angles about the shaft that, once turned along +X, put one vane straight up in profile.
VANES3 = (180, 300, 60)
VANES2 = (180, 0)


def _v(*a):
    return Vector(a)


def _to_x(objs):
    for o in objs:
        geo.transform(o, ALONG_X)
    return objs


def _fletch(name, length, height, mat, coll, z0, angle, shape='feather'):
    if shape == 'feather':
        outline = [(0, 0), (height * 0.9, length * 0.18), (height, length * 0.85), (0.002, length)]
    else:
        outline = [(0, 0), (height, length * 0.25), (height, length * 0.75), (0, length)]
    fl = geo.extrude(name, outline, 0.003, mat, coll, plane='XZ', bevel=0)
    geo.place(fl, (0, 0, z0), rotation=(0, 0, angle))
    return fl


# ------------------------------------------------------------------ long shots (+X)
def arrow(coll):
    shaft = S.wood('b08a5a', name='Ash shaft', axis='Z', grain=9.0)
    steel = S.metal('a7b0b4', name='Arrowhead', rough=0.3)
    teal = S.cloth(LANTERN_TEAL, name='Fletching', sheen=0.9)
    cock = S.cloth('e8e2d0', name='Cock feather', sheen=0.9)
    L = 0.78
    # Shafts are drawn thicker than life so they still read a couple of screen pixels wide in battle.
    out = [geo.cylinder('Shaft', 0.014, L, (0, 0, L / 2), shaft, coll, 10, bevel=0)]
    out.append(geo.lathe('Bodkin', [(0.0, L + 0.085), (0.024, L + 0.03), (0.019, L + 0.005), (0.015, L - 0.02)], 4, steel,
                         coll, close_top=False))
    out.append(geo.cylinder('Nock', 0.016, 0.02, (0, 0, 0.01), S.flat('2a2420', name='Nock'), coll, 10, bevel=0))
    out.append(geo.cylinder('Whipping', 0.0155, 0.022, (0, 0, 0.18), S.flat('3a2c22', name='Whipping'), coll, 10, bevel=0))
    out.append(geo.cylinder('Head binding', 0.0155, 0.016, (0, 0, L - 0.03), S.flat('3a2c22', name='Whipping'), coll, 10,
                            bevel=0))
    for i, a in enumerate(VANES3):
        out.append(_fletch('Fletch', 0.16, 0.05, cock if i == 0 else teal, coll, 0.025, a))
    return dict(objs=_to_x(out))


def bolt(coll):
    shaft = S.wood('8a6440', name='Bolt shaft', axis='Z', grain=8.0)
    steel = S.metal('4a5257', name='Quarrel head', rough=0.34, edge=1.9)
    vane = S.leather('5a3a22', name='Leather vane')
    L = 0.36
    out = [geo.cylinder('Shaft', 0.02, L, (0, 0, L / 2), shaft, coll, 10, bevel=0)]
    out.append(geo.lathe('Quarrel', [(0.0, L + 0.08), (0.03, L + 0.03), (0.026, L), (0.02, L - 0.012)], 4, steel, coll,
                         close_top=False))
    for a in VANES2:
        out.append(_fletch('Vane', 0.1, 0.04, vane, coll, 0.01, a, shape='vane'))
    return dict(objs=_to_x(out))


def ballista_bolt(coll):
    shaft = S.wood('7a5636', name='Ballista shaft', axis='Z', grain=5.0)
    iron = S.metal('5a6166', name='Ballista iron', rough=0.36, edge=1.8)
    paint = S.paint(LANTERN_TEAL, name='Ballista paint')
    vane = S.wood('9a7448', name='Ballista vane', axis='Z')
    L = 1.35
    out = [geo.cylinder('Shaft', 0.03, L, (0, 0, L / 2), shaft, coll, 12, bevel=0.004)]
    out.append(geo.lathe('Head', [(0.0, L + 0.22), (0.05, L + 0.08), (0.055, L + 0.02), (0.035, L - 0.02),
                                  (0.033, L - 0.12)], 4, iron, coll, close_top=False))
    for z in (0.12, L - 0.18):
        out.append(geo.cylinder('Band', 0.034, 0.03, (0, 0, z), iron, coll, 12, bevel=0.004))
    out.append(geo.cylinder('Paint band', 0.0315, 0.12, (0, 0, L * 0.55), paint, coll, 12, bevel=0))
    for a in VANES3:
        out.append(_fletch('Vane', 0.3, 0.075, vane, coll, 0.03, a, shape='vane'))
    return dict(objs=_to_x(out))


def bone_bolt(coll):
    bone = S.bone('d8cba6', name='Bolt bone', crack=0.7)
    dark = S.bone('8a7c60', name='Old bone', crack=0.4, stain='4a3a28')
    glow = S.emissive(SOUL, strength=7.0, name='Soul rune')
    L = 1.25
    pts = [_v(0, 0, 0), _v(0.004, 0, L * 0.3), _v(-0.004, 0, L * 0.65), _v(0, 0, L)]
    out = [geo.tube('Bone shaft', pts, [0.034, 0.028, 0.03, 0.026], bone, coll, sides=10)]
    out.append(geo.lathe('Bone tip', [(0.0, L + 0.24), (0.03, L + 0.12), (0.05, L + 0.04), (0.03, L - 0.03)], 5, dark, coll,
                         close_top=False))
    for k, z in enumerate((L + 0.05, L + 0.12)):
        for sy in (-1, 1):
            out.append(geo.tube('Barb', [_v(0, 0, z), _v(0.0, sy * 0.06, z - 0.07)], [0.012, 0.002], dark, coll, sides=5))
    for z in (0.22, 0.6, 0.95):
        out.append(geo.cylinder('Knuckle', 0.04, 0.05, (0, 0, z), dark, coll, 10, bevel=0.01))
    out.append(geo.cylinder('Rune band', 0.036, 0.05, (0, 0, L * 0.8), glow, coll, 10, bevel=0))
    for a in VANES3:
        out.append(_fletch('Bone vane', 0.26, 0.065, dark, coll, 0.04, a, shape='vane'))
    return dict(objs=_to_x(out))


def harpoon(coll):
    iron = S.metal('5f676c', name='Harpoon iron', rough=0.4, wear=0.25)
    rope = S.rope('9b7b52', name='Harpoon line')
    L = 1.3
    out = [geo.cylinder('Shaft', 0.022, L, (0, 0, L / 2), iron, coll, 10, bevel=0.004)]
    out.append(geo.lathe('Point', [(0.0, L + 0.2), (0.04, L + 0.06), (0.026, L)], 6, iron, coll, close_top=False))
    for sy in (-1, 1):
        out.append(geo.tube('Barb', [_v(0, 0, L + 0.08), _v(0, sy * 0.07, L - 0.04), _v(0, sy * 0.05, L - 0.08)],
                            [0.014, 0.01, 0.003], iron, coll, sides=6))
    ring = geo.lathe('Line eye', [(0.035, -0.015), (0.05, 0.0), (0.035, 0.015)], 16, iron, coll, close_top=False,
                     close_bottom=False, rotation=(90, 0, 0), location=(0, 0, -0.04))
    out.append(ring)
    coil = [_v(0.02 * math.sin(t * 2.2), 0.02 * math.cos(t * 2.2), -0.06 - 0.03 * t) for t in [i * 0.5 for i in range(7)]]
    out.append(geo.tube('Line', coil, 0.009, rope, coll, sides=6))
    return dict(objs=_to_x(out))


def frost_shard(coll):
    ice = S.glass('bff4ff', name='Frost crystal', ior=1.31, rough=0.12, glow=0.6)
    core_glow = S.emissive('d8fbff', strength=3.0, name='Frost core')
    L = 0.42
    out = [geo.lathe('Shard', [(0.0, 0.0), (0.045, 0.1), (0.05, 0.24), (0.0, L)], 6, ice, coll)]
    out.append(geo.lathe('Core', [(0.0, 0.06), (0.014, 0.14), (0.0, 0.3)], 6, core_glow, coll))
    rnd = random.Random(4)
    for k in range(4):
        z = 0.08 + 0.05 * k
        a = rnd.uniform(0, math.tau)
        d = _v(math.cos(a), math.sin(a), -0.6).normalized()
        p0 = _v(0, 0, z)
        out.append(geo.tube('Spur', [p0, p0 + d * 0.09], [0.018, 0.001], ice, coll, sides=5))
    return dict(objs=_to_x(out))


# ------------------------------------------------------------------ thrown shots (upright, spun or swayed)
def flask(coll):
    M = palette.lantern()
    glass = S.glass('ffe9c8', name='Flask glass', ior=1.45, rough=0.05, tint_density=0.4)
    fire = S.emissive('ff8a2a', strength=7.0, name="Alchemist's fire", core='ffd27a')
    f = W.flask(M, coll, size=1.7, glass=glass, liquid=fire)
    rag = S.cloth('c8b48a', name='Fuse rag')
    f['objs'].append(geo.tube('Rag', [_v(0, 0, 0.23), _v(0.03, 0, 0.27), _v(0.07, 0.01, 0.26)], [0.016, 0.012, 0.006], rag,
                              coll, sides=6))
    return dict(objs=f['objs'], spin=True)


def plague_pot(coll):
    clay = S.stone('7a5238', name='Fired clay', rough=0.8, scale=3.0, dark=0.6)
    rope = S.rope('8a6a42', name='Pot cord')
    ooze = S.emissive(PLAGUE, strength=4.0, name='Plague ooze', core='e0ffb0')
    out = [geo.lathe('Pot', [(0.0, -0.13), (0.08, -0.125), (0.13, -0.06), (0.135, 0.03), (0.1, 0.1), (0.055, 0.13),
                             (0.06, 0.15), (0.0, 0.15)], 20, clay, coll)]
    for z in (-0.03, 0.06):
        out.append(geo.lathe('Cord', [(0.137 - abs(z) * 0.2, z - 0.008), (0.142 - abs(z) * 0.2, z), (0.137 - abs(z) * 0.2, z + 0.008)],
                             20, rope, coll, close_top=False, close_bottom=False))
    out.append(geo.sphere('Ooze cap', 0.05, (0, 0, 0.16), ooze, coll, 12, 8, scale=(1, 1, 0.5)))
    rnd = random.Random(7)
    for k in range(5):
        a = rnd.uniform(0, math.tau)
        p = _v(math.cos(a) * 0.12, math.sin(a) * 0.12, 0.05 - k * 0.03)
        out.append(geo.tube('Drip', [p + _v(0, 0, 0.04), p, p + _v(0, 0, -0.05 - 0.02 * k)], [0.012, 0.015, 0.004], ooze,
                            coll, sides=6))
    return dict(objs=out, spin=True)


def firepot(coll):
    clay = S.stone('8a5a3a', name='Firepot clay', rough=0.85, scale=3.0, dark=0.55)
    pitch = S.flat('1c1612', name='Pitch', rough=0.4)
    ember = S.emissive('ff7a2a', strength=8.0, name='Firepot ember', core='ffd27a')
    out = [geo.lathe('Pot', [(0.0, -0.11), (0.09, -0.1), (0.12, -0.02), (0.1, 0.07), (0.05, 0.1), (0.0, 0.1)], 20, clay,
                     coll)]
    out.append(geo.sphere('Pitch seal', 0.055, (0, 0, 0.1), pitch, coll, 12, 8, scale=(1, 1, 0.45)))
    out.append(geo.tube('Wick', [_v(0, 0, 0.11), _v(0.02, 0, 0.16), _v(0.05, 0, 0.18)], [0.012, 0.01, 0.008],
                        S.cloth('a89070', name='Wick rag'), coll, sides=6))
    out.append(geo.sphere('Ember', 0.024, (0.05, 0, 0.18), ember, coll, 10, 6))
    return dict(objs=out, spin=True)


def cog(coll):
    iron = S.metal('6a6f73', name='Cog iron', rough=0.38, wear=0.3, edge=1.8)
    brass = S.gold('b0884a', name='Cog brass', rough=0.3)
    out = [geo.lathe('Cog rim', [(0.04, -0.018), (0.09, -0.018), (0.09, 0.018), (0.04, 0.018)], 32, iron, coll,
                     close_top=False, close_bottom=False, rotation=(90, 0, 0))]
    for i in range(12):
        a = i * math.tau / 12
        out.append(geo.box('Tooth', (0.032, 0.036, 0.03), (math.cos(a) * 0.1, 0, math.sin(a) * 0.1), iron, coll, bevel=0.005,
                           rotation=(0, -math.degrees(a), 0)))
    out.append(geo.cylinder('Hub', 0.028, 0.044, (0, 0, 0), brass, coll, 12, rotation=(90, 0, 0), bevel=0.005))
    for i in range(4):
        a = i * math.tau / 4 + 0.4
        out.append(geo.box('Spoke', (0.06, 0.02, 0.016), (math.cos(a) * 0.06, 0, math.sin(a) * 0.06), iron, coll,
                           bevel=0.004, rotation=(0, -math.degrees(a), 0)))
    return dict(objs=out, spin=True)


def skull(coll):
    M = palette.rotbound()
    mats = dict(M, bone=S.bone('ece6d4', name='Ghost bone', crack=0.3, stain='9a9480'),
                glow=S.emissive('f4fff8', strength=9.0, name='Skull glow'))
    objs = H.skull_head(_v(0, 0, 0), 0.11, mats, coll, eye_glow=True)
    return dict(objs=objs, spin=False)


def blight_glob(coll):
    bile = S.flesh('6f8f34', name='Blight bile', wet=0.95, rot='3a4a12', vein='a8d050')
    glow = S.emissive(PLAGUE, strength=3.0, name='Bile glow')
    out = [geo.lathe('Glob', [(0.0, -0.11), (0.07, -0.08), (0.09, 0.0), (0.06, 0.09), (0.02, 0.15), (0.0, 0.17)], 18, bile,
                     coll, rotation=(0, -90, 0))]
    rnd = random.Random(3)
    for k in range(4):
        p = _v(-0.16 - 0.06 * k, rnd.uniform(-0.03, 0.03), rnd.uniform(-0.03, 0.03))
        out.append(geo.sphere('Droplet', 0.026 - 0.004 * k, p, bile, coll, 10, 6))
    for k in range(5):
        a = rnd.uniform(0, math.tau)
        out.append(geo.sphere('Bubble', 0.014, (rnd.uniform(-0.05, 0.06), math.cos(a) * 0.075, math.sin(a) * 0.075), glow,
                              coll, 8, 6))
    return dict(objs=out, spin=False)


BUILDERS = dict(arrow=arrow, bolt=bolt, ballista_bolt=ballista_bolt, bone_bolt=bone_bolt, harpoon=harpoon,
                frost_shard=frost_shard, flask=flask, plague_pot=plague_pot, firepot=firepot, cog=cog, skull=skull,
                blight_glob=blight_glob)
# Modelled along +X with the shaft on the X axis; the rest are thrown shots modelled upright.
LONG_SHOTS = {'arrow', 'bolt', 'ballista_bolt', 'bone_bolt', 'harpoon', 'frost_shard'}


def _bounds(objs):
    pts = [o.matrix_world @ v.co for o in objs if o.type == 'MESH' for v in o.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def build(ident, samples):
    core.reset()
    S.reset_cache()
    scene = bpy.context.scene
    core.setup_render(scene, samples=samples)
    core.studio_world(scene, top='7a8a9c', bottom='3a3128', strength=0.75)
    coll = core.collection(ident)
    spec = BUILDERS[ident](coll)
    objs = [o for o in coll.all_objects if o.type == 'MESH']
    lo, hi = _bounds(objs)
    centre = (lo + hi) / 2
    size = hi - lo
    rig = core.collection('Lights')
    light_rig(rig, center=centre, scale=max(0.12, size.length * 0.25))
    e = math.radians(ELEVATION)
    fwd = Vector((0, math.cos(e), -math.sin(e)))
    width = size.x * 1.08 + 0.02
    height = (size.z * math.cos(e) + size.y * math.sin(e)) * 1.12 + 0.02
    cam = core.ortho_camera('Cam', centre - fwd * 10, centre, max(width, height), scene.collection)
    scene.camera = cam
    scene.render.resolution_x = max(16, round(width * PX_PER_M))
    scene.render.resolution_y = max(16, round(height * PX_PER_M))
    bpy.context.view_layer.update()

    def uv(p):
        q = world_to_camera_view(scene, cam, p)
        return [round(q.x, 5), round(1 - q.y, 5)]
    # A long shot's tip and centre sit on its shaft, not the middle of its bounds: fletching that reaches
    # higher above the shaft than below lifts the box.
    axis = Vector((centre.x, 0, 0)) if ident in LONG_SHOTS else centre
    tip = uv(Vector((hi.x, axis.y, axis.z)))
    REVIEW.mkdir(parents=True, exist_ok=True)
    path = REVIEW / f'{ident}.png'
    core.render_to(scene, path, cam)
    return dict(size=[scene.render.resolution_x, scene.render.resolution_y], tip=tip, centre=uv(axis),
                length=round(size.x, 4), spin=bool(spec.get('spin', False)))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--ids', default='all')
    parser.add_argument('--samples', type=int, default=64)
    args = parser.parse_args(core.args_after_dashes(sys.argv))
    ids = list(BUILDERS) if args.ids == 'all' else args.ids.split(',')
    OUT.mkdir(parents=True, exist_ok=True)
    meta_path = OUT / 'projectiles.json'
    meta = json.loads(meta_path.read_text()) if meta_path.exists() else {}
    failed = []
    for ident in ids:
        t0 = time.time()
        try:
            meta[ident] = build(ident, args.samples)
            shutil.copy2(REVIEW / f'{ident}.png', OUT / f'{ident}.png')
            print(f'PROJECTILE_COMPLETE {ident} {time.time() - t0:.1f}s {meta[ident]}', flush=True)
        except Exception:
            traceback.print_exc()
            print('PROJECTILE_FAILED ' + ident, flush=True)
            failed.append(ident)
    meta_path.write_text(json.dumps(dict(sorted(meta.items())), indent=2) + '\n')
    if failed:
        sys.exit(1)


main()
