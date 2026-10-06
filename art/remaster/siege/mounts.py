"""Weapon mounts placed on the wagon roof (arrows, ballista, firepot); the enemy stronghold has
none. +X is the firing direction; base sits at z=0 centred near x=0.15 so it lands on the
runtime anchor (mount pixel 192, base row ~390).

Mounts stay faction-neutral (oak, blackened iron, brass, rope).
Weapons aim ~25 degrees toward the camera so prods, arms and lenses read in 3/4.
"""
import math
import random

from mathutils import Matrix, Vector

from rk import core, geo, shaders as S, structures as ST, weapons as W
from rk.heads import catmull
from rk.structures import Batch, V

CX = 0.15        # footprint centre (x)
YAW = -25.0      # aim toward camera-right


def mats():
    return dict(
        wood=ST.board('6a4528', name='Oak', axis='X', dark=.55),
        wood_v=ST.board('5a3a22', name='Oak posts', axis='Z', dark=.55),
        wood_y=ST.board('624027', name='Oak cross timbers', axis='Y', dark=.55),
        wood_dark=ST.board('2f2219', name='Tarred timber', axis='X', dark=.6),
        iron=ST.plate('262d33', name='Blackened iron', rough=.42, edge=2.3),
        stud=S.gold('a07a3c', name='Rivet brass', rough=.34),
        brass=S.gold('c89a48', name='Antique brass'),
        rope=S.rope('9b7b52'),
        sinew=S.rope('6e5a40', name='Sinew skein'),
        leather=S.leather('4a3020', name='Leather'),
        blade=S.metal('c9d0d3', name='Bright steel', rough=.26, edge=1.3, hammer=0.0),
        string=S.flat('d8cfb8', name='Bowstring', rough=.7),
        feather=S.cloth('e2d8c4', name='Goose fletching', var='a89a80', weave=140, sheen=.4),
        clay=ST.board('9a5a34', name='Fired clay', axis='Z', grain=1.5, dark=.7, rough=.85),
        dark=S.flat('080605', name='Shadow'),
    )


def aim_matrix(pivot, yaw=YAW, pitch=0.0):
    return Matrix.Translation(Vector(pivot)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z') @ \
        Matrix.Rotation(math.radians(-pitch), 4, 'Y')


def xf(objs, M):
    for o in objs:
        if o is not None:
            geo.transform(o, M)
    return objs


def turntable(coll, M, r=0.46, studs=None):
    """Oak platform on cross skids with an iron rim; returns top z."""
    B = Batch('Turntable', coll, random.Random(1))
    B.box((r * 2.3, 0.16, 0.1), (CX, 0, 0.05), M['wood_dark'])
    B.box((0.16, r * 2.0, 0.1), (CX, 0, 0.05), M['wood_dark'])
    for sx in (-1, 1):
        B.box((0.08, 0.2, 0.06), (CX + sx * r * 1.08, 0, 0.11), M['iron'])
    B.plank_disc((CX, 0, 0), r, 0.1, 0.19, M['wood'], planks=5, rot=YAW)
    B.ring((CX, 0, 0.145), r - 0.004, r + 0.022, 0.1, M['iron'], 'Z', 48)
    B.cyl(0.16, 0.08, (CX, 0, 0.23), M['iron'], segs=20)
    B.cyl(0.12, 0.05, (CX, 0, 0.29), M['brass'], segs=20)
    B.finish(bevel=0.008)
    pts = [((CX + math.cos(a) * (r + 0.024), math.sin(a) * (r + 0.024), 0.145), (math.cos(a), math.sin(a), 0))
           for a in (math.tau * i / 18 for i in range(18))]
    geo.rivets('Rim studs', pts, 0.016, M['stud'], coll)
    return 0.31


# =================================================================== arrows
def arrows(coll, M):
    top = turntable(coll, M)
    out = {'flames': []}
    B = Batch('Crossbow post', coll, random.Random(2))
    px = CX + 0.02
    B.box((0.15, 0.15, 0.62), (px, 0, top + 0.31), M['wood_v'])
    for z in (top + 0.08, top + 0.52):
        B.box((0.17, 0.17, 0.05), (px, 0, z), M['iron'])
    # braces
    for a in (0, 120, 240):
        d = V(math.cos(math.radians(a + YAW)), math.sin(math.radians(a + YAW)), 0)
        B.wedge_box(V(px, 0, top) + d * 0.36, V(px, 0, top + 0.4) + d * 0.06, 0.06, 0.05, 0.06, 0.05, M['wood_v'])
    B.cyl(0.07, 0.08, (px, 0, top + 0.66), M['brass'], segs=16)
    B.finish(bevel=0.008)
    # heavy swivel crossbow (built along local +X, pivot at origin)
    piv = V(px, 0, top + 0.78)
    A = Matrix.Identity(4)
    C = Batch('Arbalest', coll, random.Random(3))
    prof = [(-0.62, -0.06), (-0.62, 0.03), (-0.3, 0.05), (0.0, 0.05), (0.72, 0.04), (0.74, -0.02), (0.1, -0.04), (0.0, -0.12),
            (-0.1, -0.12), (-0.18, -0.05)]
    C.prism([(x, z * 1.3) for x, z in prof], -0.055, 0.055, M['wood'], matrix=Matrix.Rotation(math.radians(90), 4, 'X') @
            Matrix.Diagonal((1, 1, 1, 1)))
    C.box((1.22, 0.035, 0.015), (0.08, 0, 0.058), M['iron'])               # groove rail
    C.box((0.12, 0.13, 0.12), (0.0, 0, -0.06), M['iron'])                  # yoke block
    for sy in (-1, 1):
        C.box((0.14, 0.02, 0.2), (0.0, sy * 0.08, -0.1), M['iron'])        # yoke cheeks
    C.box((0.08, 0.1, 0.1), (0.62, 0, 0.02), M['iron'])                   # prod lath box
    C.box((0.06, 0.08, 0.05), (-0.05, 0, 0.08), M['brass'])               # nut
    C.torus((0.8, 0, 0.0), 0.06, 0.012, M['iron'], rot=(0, 90, 0), segs=12, sides=5)   # stirrup
    C.cyl(0.035, 0.2, (-0.5, 0, 0.0), M['iron'], rot=(90, 0, 0), segs=10)   # windlass axle
    for sy in (-1, 1):
        C.box((0.03, 0.02, 0.2), (-0.5, sy * 0.11, 0.06), M['iron'])
        C.cyl(0.018, 0.08, (-0.5, sy * 0.13, 0.15), M['wood_v'], rot=(90, 0, 0), segs=8)
    C.finish(bevel=0.006)
    objs = [o for o in coll.objects if o.name.startswith('Arbalest')]
    prod = []
    for i in range(15):
        t = i / 14 * 2 - 1
        prod.append(V(0.66 - 0.12 * t * t, t * 0.58, 0.02))
    pr = geo.tube('Steel prod', prod, [0.04 * (1 - .5 * abs(i / 7 - 1)) + .014 for i in range(15)], M['iron'], coll, sides=8,
                  flatten=.6)
    st = geo.tube('Arbalest string', [prod[0], V(-0.05, 0, 0.06), prod[-1]], 0.009, M['string'], coll, sides=4)
    bolt = [geo.cylinder('Loaded bolt', 0.014, 0.78, (0.3, 0, 0.08), M['wood'], coll, 8, rotation=(0, 90, 0), bevel=0)]
    hd = W.blade('Bolt head', 0.12, 0.05, M['blade'], coll, base_z=0, thickness=0.02, tip=0.6, taper=0)
    geo.transform(hd, Matrix.Translation((0.69, 0, 0.08)) @ Matrix.Rotation(math.radians(90), 4, 'Y'))
    bolt.append(hd)
    for k in range(3):
        f = geo.extrude('Bolt vane', [(0, 0), (0.09, 0.0), (0.07, 0.035), (0.0, 0.03)], 0.004, M['feather'], coll, plane='XZ', bevel=0)
        geo.place(f, (-0.08, 0, 0.08), rotation=(k * 120, 0, 0))
        bolt.append(f)
    xf(objs + [pr, st] + bolt, aim_matrix(piv, YAW, 9))
    # bolt barrel (quiver) behind and a short rack
    R = Batch('Bolt barrel', coll, random.Random(4))
    bc = V(CX - 0.32, 0.22, top + 0.22)
    ST.barrel(R, bc, 0.15, 0.42, M['wood'], M['iron'], var=0.4)
    R.finish(bevel=0.005)
    rng = random.Random(5)
    for k in range(13):
        a = rng.uniform(0, math.tau)
        rr = rng.uniform(0, 0.1)
        base = bc + V(math.cos(a) * rr, math.sin(a) * rr, 0.0)
        tip = base + V(rng.uniform(-0.12, 0.08), rng.uniform(-0.08, 0.08), 0.62 + rng.uniform(-0.05, 0.08))
        geo.tube('Spare bolt', [base, tip], 0.011, M['wood_v'], coll, sides=6)
        d = (tip - base).normalized()
        for j in range(2):
            vane = geo.extrude('Spare fletching', [(0, 0), (0.1, 0.0), (0.08, 0.04), (0.0, 0.035)], 0.004,
                               M['feather'], coll, plane='XZ', bevel=0)
            q = d.to_track_quat('X', 'Z').to_matrix().to_4x4()
            geo.transform(vane, Matrix.Translation(tip - d * 0.11) @ q @ Matrix.Rotation(math.radians(j * 90 + k * 23), 4, 'X'))
    # pavise leaning behind the post: a tall planked shield with an iron rim and brass boss
    Pv = Batch('Pavise', coll, random.Random(6))
    pw, ph = 0.56, 0.92
    Mp = Matrix.Translation((CX - 0.12, 0.34, top - 0.02)) @ Matrix.Rotation(math.radians(YAW + 10), 4, 'Z') @ \
        Matrix.Rotation(math.radians(-14), 4, 'X')
    for k in range(4):
        xa = -pw / 2 + pw * k / 4 + 0.004
        xb = -pw / 2 + pw * (k + 1) / 4 - 0.004
        Pv.box((xb - xa, 0.05, ph - 0.06 * abs(k - 1.5)), (0, 0, 0), M['wood_v'],
               matrix=Mp @ Matrix.Translation(((xa + xb) / 2, 0, (ph - 0.06 * abs(k - 1.5)) / 2)))
    for z in (0.12, ph - 0.16):
        Pv.box((pw + 0.03, 0.03, 0.06), (0, 0, 0), M['iron'], matrix=Mp @ Matrix.Translation((0, -0.035, z)))
    for sx in (-1, 1):
        Pv.box((0.04, 0.03, ph - 0.1), (0, 0, 0), M['iron'], matrix=Mp @ Matrix.Translation((sx * (pw / 2 + 0.005), -0.035, ph / 2 - 0.05)))
    Pv.lathe([(0, 0.06), (0.09, 0.03), (0.11, 0.0)], M['brass'],
             Mp @ Matrix.Translation((0, -0.04, ph * 0.52)) @ Matrix.Rotation(math.radians(90), 4, 'X'), 16, cap_top=False)
    Pv.finish(bevel=0.007)
    return out


# =================================================================== ballista
def ballista(coll, M):
    top = turntable(coll, M, r=0.5)
    out = {'flames': []}
    T = Batch('Ballista trestle', coll, random.Random(7))
    for sy in (-1, 1):
        for sx in (-1, 1):
            T.wedge_box((CX + sx * 0.36, sy * 0.3, top), (CX + sx * 0.06, sy * 0.12, top + 0.6), 0.08, 0.07, 0.08, 0.07, M['wood_v'])
        T.box((0.5, 0.06, 0.06), (CX, sy * 0.2, top + 0.24), M['wood_y'])
    T.box((0.22, 0.34, 0.12), (CX, 0, top + 0.62), M['wood_dark'])
    T.cyl(0.11, 0.06, (CX, 0, top + 0.71), M['iron'], segs=16)
    T.finish(bevel=0.008)
    piv = V(CX, 0, top + 0.78)
    Bb = Batch('Ballista body', coll, random.Random(8))
    Bb.box((1.6, 0.17, 0.1), (0.0, 0, 0.0), M['wood'])                    # stock / case
    Bb.box((1.5, 0.05, 0.02), (0.02, 0, 0.06), M['iron'])                 # slider groove
    Bb.box((0.36, 0.07, 0.07), (-0.28, 0, 0.09), M['wood_dark'])          # slider
    Bb.box((0.1, 0.09, 0.05), (-0.12, 0, 0.13), M['iron'])                # claw
    for x in (-0.6, -0.2, 0.25):
        Bb.box((0.05, 0.19, 0.12), (x, 0, 0.0), M['iron'])
    # field frame holding the torsion springs
    fx = 0.42
    Bb.box((0.14, 0.95, 0.08), (fx, 0, 0.26), M['wood_y'])
    Bb.box((0.14, 0.95, 0.08), (fx, 0, -0.12), M['wood_y'])
    for sy in (-0.47, -0.13, 0.13, 0.47):
        Bb.box((0.12, 0.07, 0.42), (fx, sy, 0.07), M['wood_v'])
    for sy in (-1, 1):
        Bb.cyl(0.075, 0.46, (fx, sy * 0.3, 0.07), M['sinew'], segs=14)
        for z in (0.3, -0.16):
            Bb.cyl(0.095, 0.035, (fx, sy * 0.3, z), M['brass'], segs=16)
            Bb.box((0.04, 0.2, 0.025), (fx, sy * 0.3, z + (0.025 if z > 0 else -0.025)), M['iron'])
    # front shield plate with the bolt window
    Bb.box((0.03, 0.2, 0.3), (fx + 0.09, 0, 0.07), M['iron'])
    Bb.box((0.035, 0.08, 0.07), (fx + 0.095, 0, 0.12), M['dark'])
    # winch
    Bb.cyl(0.07, 0.3, (-0.68, 0, 0.0), M['wood_v'], rot=(90, 0, 0), segs=14)
    for sy in (-1, 1):
        Bb.box((0.16, 0.03, 0.16), (-0.68, sy * 0.17, 0.0), M['iron'])
        for k in range(4):
            a = math.tau * k / 4 + 0.4
            Bb.wedge_box((-0.68, sy * 0.2, 0.0), (-0.68 + math.cos(a) * 0.17, sy * 0.2, math.sin(a) * 0.17), 0.025, 0.02, 0.025, 0.02,
                         M['wood_v'], up=(0, 1, 0))
    Bb.finish(bevel=0.006)
    objs = [o for o in coll.objects if o.name.startswith('Ballista body')]
    arms = []
    tips = []
    for sy in (-1, 1):
        pts = [V(fx, sy * 0.3, 0.07), V(fx - 0.05, sy * 0.52, 0.08), V(fx - 0.2, sy * 0.72, 0.08)]
        arms.append(geo.tube('Ballista arm', catmull(pts, 4), [0.06, 0.057, 0.054, 0.05, 0.047, 0.044, 0.04, 0.037, 0.034],
                             M['wood_v'], coll, sides=8))
        arms.append(geo.cylinder('Arm cap', 0.03, 0.05, pts[-1], M['iron'], coll, 8, bevel=0.003))
        tips.append(pts[-1])
    arms.append(geo.tube('Ballista string', [tips[0], V(-0.12, 0, 0.14), tips[1]], 0.011, M['string'], coll, sides=4))
    bolt = [geo.cylinder('Siege bolt', 0.022, 1.05, (0.18, 0, 0.14), M['wood'], coll, 8, rotation=(0, 90, 0), bevel=0)]
    hd = W.blade('Bolt head', 0.2, 0.085, M['blade'], coll, base_z=0, thickness=0.03, tip=0.6, taper=0)
    geo.transform(hd, Matrix.Translation((0.7, 0, 0.14)) @ Matrix.Rotation(math.radians(90), 4, 'Y'))
    bolt.append(hd)
    for k in range(3):
        f = geo.extrude('Bolt vane', [(0, 0), (0.14, 0.0), (0.1, 0.05), (0.0, 0.045)], 0.005, M['feather'], coll, plane='XZ', bevel=0)
        geo.place(f, (-0.34, 0, 0.14), rotation=(k * 120 + 30, 0, 0))
        bolt.append(f)
    xf(objs + arms + bolt, aim_matrix(piv, YAW, 7))
    # spare bolts lashed to the trestle
    for k in range(3):
        geo.cylinder('Spare siege bolt', 0.018, 0.9, (CX - 0.05 + k * 0.04, 0.33, top + 0.06 + k * 0.035), M['wood'], coll, 8,
                     rotation=(0, 90, 0), bevel=0)
    return out


# =================================================================== firepot
def firepot(coll, M):
    top = turntable(coll, M, r=0.5)
    out = {'flames': []}
    Mf = aim_matrix(V(CX, 0, top), YAW, 0)
    O = Batch('Onager frame', coll, random.Random(9))
    for sy in (-1, 1):
        O.box((1.2, 0.12, 0.14), (0.0, sy * 0.3, 0.07), M['wood'])
        O.wedge_box((0.42, sy * 0.3, 0.12), (0.3, sy * 0.24, 0.98), 0.1, 0.08, 0.1, 0.08, M['wood_v'])     # stop uprights
        O.wedge_box((0.8, sy * 0.3, 0.1), (0.36, sy * 0.25, 0.8), 0.07, 0.06, 0.07, 0.06, M['wood_v'])   # braces
        O.cyl(0.1, 0.05, (0.0, sy * 0.36, 0.26), M['brass'], rot=(90, 0, 0), segs=16)
        O.box((0.26, 0.06, 0.26), (0.0, sy * 0.3, 0.26), M['iron'])
    for x in (-0.5, 0.5):
        O.box((0.12, 0.72, 0.1), (x, 0, 0.07), M['wood_y'])
    O.box((0.16, 0.66, 0.14), (0.3, 0, 0.98), M['wood_y'])
    O.box((0.1, 0.5, 0.18), (0.22, 0, 0.98), M['leather'])               # padded stop
    O.cyl(0.11, 0.54, (0.0, 0, 0.26), M['sinew'], rot=(90, 0, 0), segs=16)  # torsion skein
    # winch at the rear
    O.cyl(0.06, 0.6, (-0.52, 0, 0.24), M['wood_v'], rot=(90, 0, 0), segs=12)
    for sy in (-1, 1):
        O.box((0.08, 0.05, 0.22), (-0.52, sy * 0.3, 0.17), M['wood_v'])
        O.box((0.03, 0.03, 0.2), (-0.52, sy * 0.34, 0.33), M['iron'])
    O.finish(bevel=0.007)
    objs = [o for o in coll.objects if o.name.startswith('Onager frame')]
    # cocked throwing arm with its cup
    p0 = V(0.0, 0, 0.26)
    p1 = V(-0.62, 0, 0.82)
    A = Batch('Throwing arm', coll, random.Random(10))
    A.wedge_box(p0 + V(0.06, 0, -0.05), p1, 0.11, 0.08, 0.12, 0.08, M['wood_v'], up=(0, 1, 0))
    for t in (0.35, 0.7):
        A.box((0.04, 0.12, 0.13), p0.lerp(p1, t), M['iron'], rot=(0, -42, 0))
    cup = p1 + V(-0.04, 0, 0.06)
    A.lathe([(0.0, -0.05), (0.13, -0.03), (0.18, 0.06), (0.17, 0.08), (0.12, 0.0), (0.0, -0.01)], M['iron'],
            Matrix.Translation(cup) @ Matrix.Rotation(math.radians(-25), 4, 'Y'), 16)
    A.finish(bevel=0.005)
    objs += [o for o in coll.objects if o.name.startswith('Throwing arm')]
    objs.append(geo.tube('Winch rope', [V(-0.52, 0, 0.3), V(-0.6, 0, 0.55), p0.lerp(p1, 0.85)], 0.014, M['rope'], coll, sides=6))
    # the burning fire pot
    pot_c = cup + V(0.0, 0, 0.12)
    pot = geo.lathe('Fire pot', [(0.0, -0.11), (0.09, -0.1), (0.13, -0.03), (0.12, 0.05), (0.06, 0.1), (0.05, 0.13), (0.0, 0.13)], 16,
                    ST.board('8a4a2a', name='Pitch-smeared clay', axis='Z', grain=1.2, dark=.6, ember='ff6a1a', ember_strength=6),
                    coll, location=pot_c)
    objs.append(pot)
    fl = ST.flame_tongues('Pot fire', coll, pot_c + V(0, 0, 0.12), 0.32, 0.07, M['flame'], seed=3, count=5, lean=(-0.15, 0, 0))
    objs += fl
    out['flames'] += fl
    xf(objs, Mf)
    w = Mf @ (pot_c + V(0, 0, 0.25))
    out['lights'] = [core.point_light('Firepot glow', w + V(0.0, -0.25, 0.05), 35, core.srgb('ff8a3a'), 0.06, coll)]
    # ammunition: stacked pots with rag fuses, and an ember brazier
    rng = random.Random(11)
    for k, (x, y, z) in enumerate(((CX + 0.3, -0.36, top + 0.1), (CX + 0.46, -0.18, top + 0.1), (CX + 0.37, -0.27, top + 0.3))):
        geo.lathe('Spare pot', [(0.0, -0.1), (0.08, -0.09), (0.11, -0.02), (0.1, 0.05), (0.05, 0.09), (0.045, 0.12), (0.0, 0.12)], 14,
                  M['clay'], coll, location=(x, y, z))
        geo.tube('Rag fuse', [V(x, y, z + 0.11), V(x + 0.03, y - 0.02, z + 0.18), V(x + 0.07, y - 0.01, z + 0.16)], 0.012,
                 S.cloth('b8a888', name='Rag'), coll, sides=5)
    bz_objs, fb = ST.brazier('Ember brazier', coll, (CX - 0.4, 0.32, top), dict(iron=M['iron']), M['flame'], ST.coals('ff6a1a', strength=9),
                             light=None, size=0.42, height=0.6, bowl=0.3, seed=2, flame_h=0.45)
    out['flames'] += [o for o in bz_objs if 'fire' in o.name]
    return out


SCALE = {'arrows': 1.32, 'ballista': 1.3, 'firepot': 1.28}


def build(kind, coll):
    M = mats()
    M['flame'] = ST.flame('ff6a1a', 'ffb040', 1.8, name='Fire')
    fn = {'arrows': arrows, 'ballista': ballista, 'firepot': firepot}[kind]
    out = fn(coll, M)
    # author at design scale, then grow about the footprint centre so the weapon reads at 60x75
    s = SCALE[kind]
    T = Matrix.Translation((CX, 0, 0)) @ Matrix.Scale(s, 4) @ Matrix.Translation((-CX, 0, 0))
    for o in list(coll.all_objects):
        if o.type == 'MESH':
            geo.transform(o, T)
            for m in o.modifiers:
                if m.type == 'BEVEL':
                    m.width *= s
                elif m.type == 'SOLIDIFY':
                    m.thickness *= s
        elif o.type == 'LIGHT':
            o.location = T @ o.location
            o.data.energy *= s * s
    return out
