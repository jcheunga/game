"""Lantern Caravan war wagon (player base) and its cosmetic skins.

Layout keeps the original footprint: wheels r0.9 at x=-1.76/1.66, cabin x -2.45..2.1,
deck at z~3.3 (wagon mounts land on it at x=-1.86/-0.53/0.82), drawbar to x~3.2,
flag streaming back to x~-3.5. +X faces the enemy; the battle camera sees the -Y side.
"""
import math
import random

from mathutils import Matrix, Vector

from rk import core, geo, shaders as S, structures as ST, weapons as W
from rk.heads import catmull
from rk.structures import Batch, V, mat4

from . import skins

WR = 0.9
WXS = (-1.76, 1.66)
WY = 1.3
X0, X1 = -2.45, 2.1          # cabin ends
SY = 1.0                     # cabin side plane |y|
WZ0, WZ1 = 1.48, 3.1         # wall boards
DECK = 3.3                   # deck top
PANELS = [-2.45, -1.4, -0.38, 0.68, 2.1]
POSTS = [-2.42, -1.55, -0.68, 0.19, 1.06, 1.93]


def _face_neg_y(objs, p, s=1.0, tilt=0.0):
    M = Matrix.Translation(Vector(p)) @ Matrix.Rotation(math.radians(-90), 4, 'Z') @ \
        Matrix.Rotation(math.radians(tilt), 4, 'Y') @ Matrix.Scale(s, 4)
    for o in objs:
        geo.transform(o, M)
    return objs


def _face_pos_x(objs, p, s=1.0):
    M = Matrix.Translation(Vector(p)) @ Matrix.Scale(s, 4)
    for o in objs:
        geo.transform(o, M)
    return objs


def emblem_coin(mat, glow=None):
    """Merchant guild sigil: a minted coin carrying a balance scale."""
    def build(origin, coll, s=1.0):
        out = [geo.cylinder('Coin sigil', 0.11 * s, 0.014, origin, mat, coll, 28, rotation=(0, 90, 0), bevel=0.004)]
        out.append(geo.lathe('Coin rim', [(0.1 * s, -0.012), (0.115 * s, -0.012), (0.115 * s, 0.012), (0.1 * s, 0.012)], 28,
                             mat, coll, rotation=(0, 90, 0), location=origin + V(0.006, 0, 0)))
        o = origin + V(0.012, 0, 0)
        out.append(geo.box('Scale post', (0.012, 0.012 * s, 0.13 * s), o + V(0, 0, -0.005 * s), mat, coll, bevel=0.002))
        out.append(geo.box('Scale beam', (0.012, 0.15 * s, 0.012 * s), o + V(0, 0, 0.05 * s), mat, coll, bevel=0.002))
        for sy in (-1, 1):
            out.append(geo.lathe('Scale pan', [(0, -0.01 * s), (0.032 * s, -0.004 * s), (0.036 * s, 0.002 * s)], 12, mat, coll,
                                 location=o + V(0, sy * 0.07 * s, -0.035 * s), rotation=(0, 90, 0)))
            out.append(geo.tube('Scale cord', [o + V(0, sy * 0.07 * s, 0.05 * s), o + V(0, sy * 0.07 * s, -0.03 * s)],
                                0.003 * s, mat, coll, sides=4))
        return out
    return build


def emblem_for(F, M):
    if F['emblem'] == 'coin':
        return emblem_coin(M['gilt'])
    if F['emblem'] == 'skull':
        return W.emblem_skull(M.get('skull_ink', M['bone']), M['glow'])
    return W.emblem_lantern(M['gilt'] if F['banner_trim'] == 'gilt' else M['brass'], M['glow'])


def heater(coll, M, F, p, size, emblem=True, tilt=0.0, face='paint'):
    mats = dict(paint=M['paint'], trim=M['brass'] if F['banner_trim'] != 'bone' else M['bone'], wood=M['wood'])
    if face != 'paint':
        mats['paint'] = M[face]
    wd = W.heater_shield(mats, coll, size=size, curve=0.1, emblem=emblem_for(F, M) if emblem else None, boss=False)
    return _face_neg_y(wd['objs'], p, 1.0, tilt)


def finial(coll, M, F, p, scale=1.0):
    p = Vector(p)
    s = scale
    kind = F['finial']
    out = [geo.lathe('Finial collar', [(0.07 * s, 0), (0.075 * s, 0.03 * s), (0.055 * s, 0.05 * s), (0.04 * s, 0.07 * s)], 12,
                     M['brass'], coll, location=p)]
    top = p + V(0, 0, 0.07 * s)
    if kind == 'spear':
        out.append(geo.lathe('Spear finial', [(0, 0), (0.05 * s, 0.03 * s), (0.065 * s, 0.1 * s), (0.0, 0.3 * s)], 4,
                             M['brass'], coll, location=top, rotation=(0, 0, 45)))
    elif kind == 'spike':
        out.append(geo.lathe('Iron spike', [(0, 0), (0.05 * s, 0.02 * s), (0.04 * s, 0.08 * s), (0.0, 0.28 * s)], 6,
                             M['iron'], coll, location=top))
    elif kind == 'skull':
        out += ST.oriented_skull('Trophy skull', coll, top + V(0, 0, 0.08 * s), 0.075 * s, dict(bone=M['bone'], dark=M['dark'],
                                 glow=M['glow']), facing=(0.25, -1, 0), jaw_open=0.3)
    elif kind == 'fleur':
        out.append(geo.sphere('Fleur bud', 0.04 * s, top + V(0, 0, 0.02 * s), M['gilt'], coll, 10, 6))
        out.append(geo.tube('Fleur centre', catmull([top, top + V(0, 0, .12 * s), top + V(0, 0, .26 * s)], 3), [.035 * s, .03 * s,
                            .004 * s, .002 * s, .002 * s, .001 * s, .001 * s][:7], M['gilt'], coll, sides=6))
        for sx in (-1, 1):
            pts = catmull([top + V(0, 0, .03 * s), top + V(sx * .06 * s, -sx * .03 * s, .1 * s),
                           top + V(sx * .1 * s, -sx * .05 * s, .07 * s), top + V(sx * .09 * s, -sx * .045 * s, .02 * s)], 3)
            out.append(geo.tube('Fleur petal', pts, [0.022 * s * (1 - i / len(pts)) + .004 for i in range(len(pts))],
                                M['gilt'], coll, sides=6))
    elif kind == 'crystal':
        out.append(geo.lathe('Crystal finial', [(0, -0.02 * s), (0.05 * s, 0.04 * s), (0.05 * s, 0.2 * s), (0, 0.32 * s)], 6,
                             M['crystal'], coll, location=top))
    elif kind == 'flame':
        out.append(geo.lathe('Fire cup', [(0.02 * s, 0), (0.07 * s, 0.05 * s), (0.08 * s, 0.09 * s), (0.07 * s, 0.085 * s),
                                          (0.0, 0.06 * s)], 10, M['iron'], coll, location=top))
        out += ST.flame_tongues('Post fire', coll, top + V(0, 0, 0.07 * s), 0.26 * s, 0.07 * s, M['flame'], seed=int(p.x * 10) % 7,
                                count=4)
    elif kind == 'orb':
        out.append(geo.sphere('Gilt orb', 0.06 * s, top + V(0, 0, 0.06 * s), M['gilt'], coll, 16, 10))
        out.append(geo.lathe('Orb spike', [(0.015 * s, 0.11 * s), (0.0, 0.2 * s)], 6, M['gilt'], coll, location=top))
    return out


def flag(name, coll, pole_top, height, length, mat, direction=(-1, 0, 0), wave=0.08, seed=1, tails=True, tatter=0.0):
    """Banner streaming from a pole. fn(u, v): u along the fly (0 at the pole), v down (0 at the top)."""
    import bmesh
    import bpy
    pt = Vector(pole_top)
    d = Vector(direction).normalized()
    side = Vector((-d.y, d.x, 0))          # wave across the fly; (0,-1,0) for a flag flying toward -X
    rng = random.Random(seed)
    rag = [rng.random() for _ in range(40)]

    def fn(u, v):
        x = u * length
        z = -v * height
        z -= 0.12 * height * u * u            # gentle droop toward the fly
        if tails and u > 0.72:
            k = (u - 0.72) / 0.28
            # swallow tail: notch centred on v=0.5
            cut = k * 0.42 * (1 - abs(v - 0.5) * 2)
            x -= cut * length * 0.32
        if tatter:
            x -= tatter * length * 0.06 * rag[int(v * 39)] * u ** 4
        y = wave * math.sin(u * 7.5 - 1.2 + seed) * (0.15 + u) * (1 - 0.25 * v) + 0.04 * math.sin(v * 4 + u * 3) * u
        return pt + d * x + V(0, 0, z) + side * y
    nx, ny = 22, 12
    bm = bmesh.new()
    verts = [[bm.verts.new(fn(i / nx, j / ny)) for i in range(nx + 1)] for j in range(ny + 1)]
    for j in range(ny):
        for i in range(nx):
            bm.faces.new((verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]))
    me = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    obj = bpy.data.objects.new(name, me)
    coll.objects.link(obj)
    for p in me.polygons:
        p.use_smooth = True
    sol = obj.modifiers.new('Cloth thickness', 'SOLIDIFY')
    sol.thickness = 0.014
    sol.offset = 0
    sol.use_even_offset = False
    sub = obj.modifiers.new('Soft drape', 'SUBSURF')
    sub.levels = sub.render_levels = 1
    geo.store_rest(obj)
    return obj, fn


def conform(objs, fn, uc, vc, su, sv, lift=0.012, toward=(0, -1, 0)):
    """Wrap emblem geometry built facing +X at the origin onto a parametric sheet.
    Local y -> u (scaled by su), local z -> -v (scaled by sv), local x -> offset along the sheet normal."""
    t = Vector(toward)
    eps = 1e-3
    for o in objs:
        if o.modifiers:
            geo.apply_modifiers(o, keep=())
        me = o.data
        for v in me.vertices:
            x, y, z = v.co
            u = uc + y / su
            w = vc - z / sv
            p = Vector(fn(u, w))
            du = Vector(fn(u + eps, w)) - Vector(fn(u - eps, w))
            dv = Vector(fn(u, w + eps)) - Vector(fn(u, w - eps))
            n = du.cross(dv).normalized()
            if n.dot(t) < 0:
                n = -n
            v.co = p + n * (x + lift)
        me.update()
        geo.store_rest(o)
    return objs


# ---------------------------------------------------------------------------
# Troop hold slung between the wheels, with a drop-down ramp door on the near side. Troops leave the
# caravan through it: they step out of the doorway and walk down the ramp onto the battle line.
HOLD_X0, HOLD_X1 = -0.74, 0.64
HOLD_Y = 0.92
HOLD_Z0, HOLD_Z1 = 0.16, 1.3
DOOR_X, DOOR_W = -0.05, 0.7
DOOR_Z0, DOOR_Z1 = 0.2, 1.22
DOOR_OPEN = 100.0            # degrees from upright; the tip rests on the ground
DOOR_EXIT = (DOOR_X, -0.7, 0.0)     # where a troop first appears, just inside the doorway
DOOR_FOOT = (DOOR_X, -2.02, 0.0)    # the foot of the lowered ramp


def troop_hold(coll, M, F, door, studs, lights):
    """Lower hold and its ramp door. `door` runs 0 (shut, upright) to 1 (lowered onto the ground)."""
    rng = random.Random(23)
    H = Batch('Troop hold', coll, random.Random(23))
    y0 = -HOLD_Y
    depth = 0.5                          # doorway recess between the facade and the hold's body
    # body behind the recess; the facade planks close it off either side of the doorway
    H.box((HOLD_X1 - HOLD_X0, HOLD_Y - depth + HOLD_Y, HOLD_Z1 - HOLD_Z0), ((HOLD_X0 + HOLD_X1) / 2, depth / 2, (HOLD_Z0 + HOLD_Z1) / 2),
          M['wood_dark'], var=0.3)
    rows = 4
    rh = (HOLD_Z1 - HOLD_Z0) / rows
    dx0, dx1 = DOOR_X - DOOR_W / 2, DOOR_X + DOOR_W / 2
    for r in range(rows):
        z = HOLD_Z0 + r * rh + rh / 2
        for xa, xb in ((HOLD_X0, dx0 - 0.06), (dx1 + 0.06, HOLD_X1)):
            H.box((xb - xa - 0.01, 0.05, rh - 0.012), ((xa + xb) / 2, y0 - 0.02 + rng.uniform(-0.004, 0.004), z), M['wood'],
                  rot=(0, rng.uniform(-0.4, 0.4), 0))
    lintel_z = (DOOR_Z1 + HOLD_Z1) / 2
    H.box((dx1 - dx0 + 0.12, 0.05, HOLD_Z1 - DOOR_Z1), (DOOR_X, y0 - 0.02, lintel_z), M['wood'])
    # doorway: a lamp-lit recess (like the cabin windows), threshold, ceiling and a heavy frame
    inside = S.emissive(F['light'], 0.55, name='Hold interior', core=F['light'], flicker=.25, base='120c08')
    H.box((DOOR_W, 0.02, DOOR_Z1 - DOOR_Z0), (DOOR_X, y0 + depth - 0.01, (DOOR_Z0 + DOOR_Z1) / 2), inside)
    H.box((DOOR_W, depth, 0.04), (DOOR_X, y0 + depth / 2, DOOR_Z0 - 0.02), M['wood_dark'])
    H.box((DOOR_W + 0.1, depth, 0.04), (DOOR_X, y0 + depth / 2, DOOR_Z1 + 0.02), M['wood_dark'])
    for sx in (-1, 1):
        H.box((0.07, depth, DOOR_Z1 - DOOR_Z0), (DOOR_X + sx * (DOOR_W / 2 + 0.035), y0 + depth / 2, (DOOR_Z0 + DOOR_Z1) / 2),
              M['wood_dark'])
        H.box((0.12, 0.09, DOOR_Z1 - DOOR_Z0 + 0.1), (DOOR_X + sx * (DOOR_W / 2 + 0.06), y0 - 0.06, (DOOR_Z0 + DOOR_Z1) / 2 + 0.03),
              M['wood_v'])
        H.box((0.06, 0.03, DOOR_Z1 - DOOR_Z0 + 0.08), (DOOR_X + sx * (DOOR_W / 2 + 0.06), y0 - 0.115, (DOOR_Z0 + DOOR_Z1) / 2 + 0.03),
              M['iron'])
    H.box((DOOR_W + 0.32, 0.1, 0.12), (DOOR_X, y0 - 0.07, DOOR_Z1 + 0.06), M['wood_v'])
    H.box((DOOR_W + 0.34, 0.03, 0.05), (DOOR_X, y0 - 0.125, DOOR_Z1 + 0.06), M['iron'])
    # iron bands and corner straps around the hold
    for z in (HOLD_Z0 + 0.06, HOLD_Z1 - 0.06):
        for xa, xb in ((HOLD_X0 - 0.02, dx0 - 0.12), (dx1 + 0.12, HOLD_X1 + 0.02)):
            H.box((xb - xa, 0.03, 0.06), ((xa + xb) / 2, y0 - 0.05, z), M['iron'])
            for i in range(4):
                studs.append(((xa + 0.04 + i * (xb - xa - 0.08) / 3, y0 - 0.068, z), (0, -1, 0)))
    for x in (HOLD_X0, HOLD_X1):
        H.box((0.08, 0.08, HOLD_Z1 - HOLD_Z0 + 0.04), (x, y0 - 0.02, (HOLD_Z0 + HOLD_Z1) / 2), M['iron'])
    H.finish(bevel=0.009)

    # the ramp door, hinged along its bottom edge just inside the frame
    angle = math.radians(DOOR_OPEN * door)
    hinge = V(DOOR_X, y0 - 0.04, DOOR_Z0)
    leaf_len = DOOR_Z1 - DOOR_Z0 + 0.02
    Msw = Matrix.Translation(hinge) @ Matrix.Rotation(angle, 4, 'X')      # tips outward, toward the camera
    R = Batch('Ramp door', coll, random.Random(29))

    def leaf(size, loc, mat, rot=(0, 0, 0)):
        # parts are laid out on the upright door, then swung about the hinge
        R.box(size, (0, 0, 0), mat, matrix=Msw @ mat4(loc, rot))
    planks = 5
    for i in range(planks):
        xa = -DOOR_W / 2 + DOOR_W * i / planks + 0.006
        xb = -DOOR_W / 2 + DOOR_W * (i + 1) / planks - 0.006
        leaf((xb - xa, 0.07, leaf_len), ((xa + xb) / 2, 0, leaf_len / 2), M['wood_v'], rot=(0, rng.uniform(-0.3, 0.3), 0))
    for t in (0.18, 0.5, 0.82):
        leaf((DOOR_W + 0.02, 0.025, 0.07), (0, -0.05, leaf_len * t), M['iron'])
        if door > 0.01:
            # treads across the inner face, so the lowered ramp reads as a gangway
            leaf((DOOR_W - 0.06, 0.03, 0.04), (0, 0.05, leaf_len * (t + 0.08)), M['wood_dark'])
    leaf((0.06, 0.03, leaf_len - 0.06), (0, -0.05, leaf_len / 2), M['iron'])
    R.finish(bevel=0.006)
    for t in (0.18, 0.5, 0.82):
        for sx in (-1, 1):
            studs.append((tuple(Msw @ V(sx * (DOOR_W / 2 - 0.05), -0.068, leaf_len * t)), tuple((Msw.to_3x3() @ V(0, -1, 0)))))
    for o in emblem_for(F, M)(V(0, 0, 0), coll, 0.42):
        _face_neg_y([o], (0, 0, 0))
        o.matrix_world = Msw @ Matrix.Translation((0, -0.07, leaf_len * 0.66)) @ o.matrix_world
    # chains from the frame to the ramp tip
    for sx in (-1, 1):
        top = V(DOOR_X + sx * (DOOR_W / 2 + 0.04), y0 - 0.1, DOOR_Z1 + 0.02)
        tip = Msw @ V(sx * (DOOR_W / 2 - 0.04), -0.05, leaf_len - 0.04)
        ST.chain('Ramp chain', coll, top, tip, M['iron'], sag=0.04 + 0.1 * (1 - door), link=0.04, wire=0.008)
    if door > 0.01:
        # warm light from inside the hold falls across the lowered ramp
        lights.append(core.point_light('Hold glow', (DOOR_X, y0 + depth * 0.6, 0.75), 30 * F['light_power'] * door,
                                       core.srgb(F['light']), 0.15, coll))


def build(skin_name, coll, door=0.0):
    M, F = skins.skin(skin_name)
    rng = random.Random(11)
    lights = []
    flames = []

    # ------------------------------------------------------------ chassis
    C = Batch('Chassis • rails, bolsters, bed', coll, random.Random(1))
    for y in (-0.66, 0.66):
        C.box((5.0, 0.2, 0.26), (-0.12, y, 1.08), M['wood_dark'])
    for x in WXS:
        C.cyl(0.1, 2.9, (x, 0, WR), M['iron'], rot=(90, 0, 0), segs=16)
        C.box((0.34, 2.3, 0.2), (x, 0, 1.13), M['wood_dark'])
    for x in (-2.3, -1.1, 0.0, 1.0, 2.0):
        C.box((0.14, 2.2, 0.16), (x, 0, 1.22), M['wood_dark'])
    C.box((4.7, 2.18, 0.12), (-0.17, 0, 1.36), M['wood_dark'])
    # near bed sill (iron) and bed edge board
    C.box((4.66, 0.1, 0.2), (-0.17, -1.07, 1.4), M['wood_dark'])
    C.box((4.7, 0.04, 0.09), (-0.17, -1.135, 1.42), M['iron'])
    C.box((4.66, 0.1, 0.2), (-0.17, 1.07, 1.4), M['wood_dark'])
    C.finish(bevel=0.012)
    studs = []
    for i in range(24):
        x = -2.45 + i * 4.6 / 23
        studs.append(((x, -1.16, 1.42), (0, -1, 0)))
    # leaf springs
    for x in WXS:
        for y in (-0.66, 0.66):
            for layer in range(3):
                pts = [V(x + (i / 12 - .5) * (1.25 - layer * .18), y, 0.97 + layer * .045 + .14 * math.sin(math.pi * i / 12))
                       for i in range(13)]
                geo.tube('Leaf spring', pts, 0.024, M['iron'], coll, sides=6)

    # ------------------------------------------------------------ wheels
    for x in WXS:
        for side, y in ((-1, -WY), (1, WY)):
            near = side < 0
            ST.spoked_wheel(f'Wheel {x:+.1f} {"near" if near else "far"}', coll, (x, y, WR), WR, M, spokes=12, width=0.22,
                            side=side, studs=near, detail=near, hub_spike=near, seed=int(x * 10) + side,
                            cap_mat=M['brass'], hub_mat=M['wood_dark'])
            if F['wheel_disc'] and near:
                # armoured disc plate over the outer spokes
                D = Batch('Wheel armour disc', coll, random.Random(3))
                D.ring((x, y - 0.13, WR), 0.24, WR * 0.74, 0.025, M['plate'], 'Y', 40)
                D.finish(bevel=0.006)
                pts = [((x + math.cos(a) * WR * .68, y - 0.15, WR + math.sin(a) * WR * .68), (0, -1, 0))
                       for a in (math.tau * i / 16 for i in range(16))]
                geo.rivets('Disc rivets', pts, 0.02, M['stud'], coll)

    troop_hold(coll, M, F, door, studs, lights)

    # ------------------------------------------------------------ cabin walls
    Wb = Batch('Cabin boards', coll, random.Random(5))
    Wb.box((X1 - X0 - 0.06, 2 * SY - 0.06, WZ1 - WZ0 + 0.06), ((X0 + X1) / 2, 0, (WZ0 + WZ1) / 2), M['wood_dark'], var=0.3)
    rows = 7
    rh = (WZ1 - WZ0) / rows
    for side in (-1, 1):
        y = side * (SY + 0.03)
        for pa, pb in zip(PANELS, PANELS[1:]):
            if F['plated'] and side < 0:
                # overlapping riveted plates, two tiers, lower tier lapped outward
                for tier, (za, zb) in enumerate(((WZ0, WZ0 + 0.84), (WZ0 + 0.78, WZ1))):
                    n = 2 if pb - pa > 1.2 else 1
                    for k in range(n):
                        xa = pa + (pb - pa) * k / n + 0.01
                        xb = pa + (pb - pa) * (k + 1) / n - 0.01
                        Wb.box((xb - xa, 0.035, zb - za), ((xa + xb) / 2, y - 0.02 - 0.012 * (1 - tier), (za + zb) / 2),
                               M['plate'], rot=(-2.5 if tier == 0 else 0, 0, 0))
                        for zz in (za + 0.06, zb - 0.06):
                            for xx in [xa + 0.06 + i * (xb - xa - 0.12) / 3 for i in range(4)]:
                                studs.append(((xx, y - 0.045 - 0.012 * (1 - tier), zz), (0, -1, 0)))
                continue
            for r in range(rows):
                z = WZ0 + r * rh + rh / 2
                Wb.box((pb - pa - 0.012, 0.06, rh - 0.012), ((pa + pb) / 2, y + side * rng.uniform(-0.004, 0.006), z),
                       M['wood'], rot=(0, rng.uniform(-0.35, 0.35), 0))
    for x in (X0 - 0.03, X1 + 0.03):
        for r in range(rows):
            z = WZ0 + r * rh + rh / 2
            if F['plated'] and x > 0:
                continue
            Wb.box((0.06, 2 * SY + 0.04, rh - 0.012), (x, 0, z), M['wood'], rot=(rng.uniform(-0.35, 0.35), 0, 0))
    if F['plated']:
        Wb.box((0.04, 2 * SY + 0.06, WZ1 - WZ0), (X1 + 0.05, 0, (WZ0 + WZ1) / 2), M['plate'])
    # frame posts and iron straps (near side)
    for x in PANELS:
        Wb.box((0.18, 0.1, WZ1 - WZ0 + 0.12), (x, -SY - 0.06, (WZ0 + WZ1) / 2), M['wood_v'])
        Wb.box((0.11, 0.035, WZ1 - WZ0 + 0.1), (x, -SY - 0.125, (WZ0 + WZ1) / 2), M['iron'])
        for i in range(8):
            studs.append(((x, -SY - 0.145, WZ0 + 0.06 + i * (WZ1 - WZ0 - 0.1) / 7), (0, -1, 0)))
    for x in (X0, X1):
        Wb.box((0.18, 0.18, WZ1 - WZ0 + 0.12), (x, SY + 0.04, (WZ0 + WZ1) / 2), M['wood_v'])
        Wb.box((0.035, 0.12, WZ1 - WZ0 + 0.1), (x + (0.1 if x > 0 else -0.1), -SY - 0.04, (WZ0 + WZ1) / 2), M['iron'])
    # horizontal bands
    for z in (WZ0 + 0.07, WZ1 - 0.05):
        Wb.box((X1 - X0 + 0.1, 0.03, 0.085), ((X0 + X1) / 2, -SY - 0.105, z), M['iron'])
        for i in range(30):
            x = X0 + 0.05 + i * (X1 - X0 - 0.1) / 29
            if min(abs(x - p) for p in PANELS) > 0.08:
                studs.append(((x, -SY - 0.122, z), (0, -1, 0)))
        Wb.box((0.03, 2 * SY + 0.1, 0.085), (X1 + 0.075, 0, z), M['iron'])
    # top beam under the deck
    Wb.box((X1 - X0 + 0.22, 2 * SY + 0.26, 0.16), ((X0 + X1) / 2, 0, WZ1 + 0.1), M['wood_dark'])
    Wb.box((X1 - X0 + 0.24, 0.03, 0.1), ((X0 + X1) / 2, -SY - 0.145, WZ1 + 0.1), M['iron'])
    for i in range(26):
        studs.append(((X0 - 0.08 + i * (X1 - X0 + 0.16) / 25, -SY - 0.165, WZ1 + 0.1), (0, -1, 0)))
    Wb.finish(bevel=0.011)

    # ------------------------------------------------------------ panel A: arrow slit + brace
    def arrow_slit(cx, cz, h=0.72):
        A = Batch('Arrow loop', coll, random.Random(int(cx * 100)))
        A.box((0.3, 0.04, h + 0.12), (cx, -SY - 0.105, cz), M['iron'])
        A.box((0.05, 0.03, h - 0.04), (cx, -SY - 0.12, cz), M['void'])
        A.box((0.2, 0.03, 0.05), (cx, -SY - 0.12, cz + 0.08), M['void'])
        A.box((0.035, 0.02, h * 0.5), (cx, -SY - 0.128, cz - h * 0.12), M['window'])
        A.finish(bevel=0.006)
        for dz in (-1, 1):
            for dx in (-1, 1):
                studs.append(((cx + dx * 0.11, -SY - 0.13, cz + dz * (h / 2 + 0.02)), (0, -1, 0)))
    arrow_slit(-1.93, 2.3)
    arrow_slit(1.83, 2.3, 0.62)

    # ------------------------------------------------------------ panel B: heraldic shield and crossed spears
    hb = (-0.89, -SY - 0.2, 2.32)
    heater(coll, M, F, hb, 1.45)
    for sx in (-1, 1):
        a = V(hb[0] - sx * 0.48, -SY - 0.16, 1.68)
        b = V(hb[0] + sx * 0.46, -SY - 0.16, 2.98)
        geo.tube('Crossed spear haft', [a, b], 0.022, M['wood_v'], coll, sides=8)
        d = (b - a).normalized()
        head = W.blade('Spear head', 0.2, 0.07, M['blade'], coll, base_z=0, thickness=0.02, tip=0.5, taper=0.1)
        q = d.to_track_quat('Z', 'Y').to_matrix().to_4x4()
        geo.transform(head, Matrix.Translation(b) @ q)
        geo.cylinder('Spear socket', 0.026, 0.07, b - d * 0.02, M['brass'], coll, 8, bevel=0.003,
                     rotation=tuple(math.degrees(r) for r in d.to_track_quat('Z', 'Y').to_euler()))

    # ------------------------------------------------------------ panel C: arched door
    dc, dw, dz0, dh = 0.15, 0.78, WZ0 + 0.04, 1.2

    def arch_z(x):
        r = dw * 0.62
        dx = abs(x - dc)
        hx = dw / 2
        # pointed (equilateral-ish) arch rising above dz0+dh
        cx = dc + (hx - r) * (1 if x < dc else -1)
        return dz0 + dh + math.sqrt(max(0.0, r * r - (x - cx) ** 2)) * 0.82 if dx <= hx else dz0 + dh
    D = Batch('Door', coll, random.Random(9))
    nb = 5
    for i in range(nb):
        xa = dc - dw / 2 + dw * i / nb + 0.006
        xb = dc - dw / 2 + dw * (i + 1) / nb - 0.006
        outline = [(xa, dz0), (xb, dz0)]
        for k in range(5):
            xx = xb - (xb - xa) * k / 4
            outline.append((xx, arch_z(xx)))
        verts = [(u, -SY - 0.13, w) for u, w in outline] + [(u, -SY - 0.07, w) for u, w in outline]
        n = len(outline)
        faces = [list(range(n))[::-1], list(range(n, 2 * n))] + [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)]
        D.mesh(verts, faces, M['wood_v'])
    for z in (dz0 + 0.22, dz0 + 0.62, dz0 + 1.02):
        D.box((dw + 0.02, 0.025, 0.07), (dc, -SY - 0.145, z), M['iron'])
        for sx in (-1, 1):
            D.box((0.1, 0.025, 0.13), (dc + sx * (dw / 2 - 0.04), -SY - 0.148, z), M['iron'], rot=(0, 45, 0))
    D.finish(bevel=0.007)
    arch_pts = [V(dc - dw / 2 - 0.04, -SY - 0.14, dz0 - 0.02)]
    for k in range(17):
        xx = dc - dw / 2 + dw * k / 16
        arch_pts.append(V(xx + (-0.04 if k == 0 else 0.04 if k == 16 else 0), -SY - 0.14, arch_z(xx) + 0.05))
    arch_pts.append(V(dc + dw / 2 + 0.04, -SY - 0.14, dz0 - 0.02))
    geo.tube('Door arch band', arch_pts, 0.04, M['iron'], coll, sides=6)
    geo.lathe('Door ring', [(0.05, -0.01), (0.062, 0), (0.05, 0.01)], 14, M['brass'], coll, rotation=(90, 0, 0),
              location=(dc + 0.22, -SY - 0.16, dz0 + 0.62), close_top=False, close_bottom=False)
    for o in emblem_for(F, M)(V(0, 0, 0), coll, 0.9):
        _face_neg_y([o], (dc, -SY - 0.15, dz0 + dh - 0.05))

    # ------------------------------------------------------------ panel D: window with shutters and awning
    wc, wz, ww, wh = 1.22, 2.36, 0.6, 0.58
    Wn = Batch('Window', coll, random.Random(4))
    Wn.box((ww, 0.02, wh), (wc, -SY - 0.06, wz), M['window'], var=0.5)
    for sx in (-1, 1):
        Wn.box((0.08, 0.08, wh + 0.16), (wc + sx * (ww / 2 + 0.04), -SY - 0.1, wz), M['wood_v'])
    for sz in (-1, 1):
        Wn.box((ww + 0.24, 0.1 if sz < 0 else 0.08, 0.08), (wc, -SY - 0.11 - (0.03 if sz < 0 else 0), wz + sz * (wh / 2 + 0.04)),
               M['wood_v'])
    for i in range(4):
        Wn.cyl(0.014, wh, (wc - ww / 2 + ww * (i + 0.5) / 4, -SY - 0.1, wz), M['iron'], segs=8)
    Wn.box((ww, 0.025, 0.03), (wc, -SY - 0.1, wz + 0.08), M['iron'])
    for sx in (-1, 1):
        hinge = V(wc + sx * (ww / 2 + 0.08), -SY - 0.13, wz)
        ang = math.radians(sx * 38)
        Msh = Matrix.Translation(hinge) @ Matrix.Rotation(ang, 4, 'Z') @ Matrix.Translation((sx * 0.17, 0, 0))
        for k in range(3):
            Wn.box((0.105, 0.03, wh + 0.1), (0, 0, 0), M['wood_v'],
                   matrix=Msh @ Matrix.Translation((sx * (k - 1) * 0.11, 0, 0)))
        for zz in (-0.18, 0.18):
            Wn.box((0.3, 0.035, 0.04), (0, 0, 0), M['iron'], matrix=Msh @ Matrix.Translation((0, -0.01, zz)))
    Wn.finish(bevel=0.006)
    # awning (canvas sloping outward with scalloped valance)
    az0, ay0, az1, ay1 = wz + wh / 2 + 0.34, -SY - 0.14, wz + wh / 2 + 0.12, -SY - 0.52
    ax0, ax1 = wc - ww / 2 - 0.26, wc + ww / 2 + 0.26
    import bmesh
    import bpy

    def awning_fn(u, v):
        x = ax0 + (ax1 - ax0) * u
        y = ay0 + (ay1 - ay0) * v
        z = az0 + (az1 - az0) * v - 0.025 * math.sin(math.pi * v) + 0.012 * math.sin(u * math.pi * 10) * v
        return (x, y, z)
    strips = 7 if F['stripes'] else 1
    for k in range(strips):
        ua, ub = k / strips, (k + 1) / strips
        mat = (M['cloth'] if k % 2 == 0 else M['cloth2']) if strips > 1 else M['cloth']
        sheet = geo.grid_sheet('Awning canvas', 1, 1, max(2, 16 // strips), 6,
                               lambda u, v, ua=ua, ub=ub: awning_fn(ua + (ub - ua) * u, v), mat, coll)
        m = sheet.modifiers.new('Canvas', 'SOLIDIFY')
        m.thickness = 0.012
        m.use_even_offset = False
        # valance with scallops
        n = max(2, 12 // strips)

        def val_fn(u, v, ua=ua, ub=ub):
            uu = ua + (ub - ua) * u
            x, y, z = awning_fn(uu, 1.0)
            sc = 0.05 * abs(math.sin(uu * math.pi * 7))
            return (x, y - 0.004, z - v * (0.13 + sc * 0) - 0.0)
        val = geo.grid_sheet('Awning valance', 1, 1, n * 2, 2, val_fn, mat, coll)
        bm = bmesh.new()
        bm.from_mesh(val.data)
        for vt in bm.verts:
            co = vt.co
            uu = (co.x - ax0) / (ax1 - ax0)
            if co.z < az1 - 0.06:
                co.z -= 0.06 * abs(math.sin(uu * math.pi * 7))
        bm.to_mesh(val.data)
        bm.free()
        geo.store_rest(val)
        m = val.modifiers.new('Canvas', 'SOLIDIFY')
        m.thickness = 0.01
        m.use_even_offset = False
    geo.tube('Awning rod', [V(ax0 - 0.03, ay1, az1 + 0.01), V(ax1 + 0.03, ay1, az1 + 0.01)], 0.016, M['brass'], coll, sides=8)
    for x in (ax0 + 0.04, ax1 - 0.04):
        geo.tube('Awning strut', [V(x, -SY - 0.13, az1 - 0.25), V(x, ay1, az1)], 0.012, M['iron'], coll, sides=6)

    # ------------------------------------------------------------ lower skirt: armour plates between the wheels
    K = Batch('Skirt armour', coll, random.Random(8))
    xa, xb = -0.8, 0.7
    n = 4
    for i in range(n):
        x0_ = xa + (xb - xa) * i / n
        x1_ = xa + (xb - xa) * (i + 1) / n
        cx = (x0_ + x1_) / 2
        K.box((x1_ - x0_ + 0.03, 0.035, 0.46), (cx, -1.17 - 0.012 * (i % 2), 1.08), M['plate'], rot=(-9, 0, 0))
        K.mesh([(cx - 0.05, -1.24, 0.86), (cx + 0.05, -1.24, 0.86), (cx, -1.25, 0.72), (cx, -1.2, 0.86)],
               [(0, 1, 2), (1, 3, 2), (3, 0, 2), (0, 3, 1)], M['iron'])
        for dx in (-1, 1):
            for zz in (1.26, 0.92):
                studs.append(((cx + dx * (x1_ - x0_) * 0.36, -1.2 - 0.012 * (i % 2) - (1.26 - zz) * 0.16, zz), (0, -1, -0.15)))
    K.finish(bevel=0.006)

    # ------------------------------------------------------------ front: plough armour, drawbar, chains
    Fr = Batch('Prow and drawbar', coll, random.Random(12))
    # angled plough plates covering the front of the chassis
    for k, (ya, yb) in enumerate(((-1.08, -0.36), (-0.36, 0.36), (0.36, 1.08))):
        Fr.mesh([(X1 + 0.02, ya, 1.52), (X1 + 0.02, yb, 1.52), (X1 + 0.42, yb, 0.98), (X1 + 0.42, ya, 0.98),
                 (X1 - 0.02, ya, 1.48), (X1 - 0.02, yb, 1.48), (X1 + 0.38, yb, 0.96), (X1 + 0.38, ya, 0.96)],
                [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)], M['plate'])
    for ya in (-0.36, 0.36):
        Fr.wedge_box((X1 + 0.02, ya, 1.56), (X1 + 0.46, ya, 0.96), 0.05, 0.05, 0.05, 0.05, M['iron'])
    for y in (-0.72, 0.0, 0.72):
        Fr.mesh([(X1 + 0.38, y - 0.06, 0.99), (X1 + 0.38, y + 0.06, 0.99), (X1 + 0.32, y, 1.08), (X1 + 0.62, y, 0.94)],
                [(0, 1, 3), (1, 2, 3), (2, 0, 3), (0, 2, 1)], M['iron'])
    # tongue rails converge to the draw ring
    tip = V(3.3, 0, 0.82)
    for y in (-0.42, 0.42):
        Fr.wedge_box((1.95, y, 0.98), (tip.x - 0.12, y * 0.12, tip.z + 0.02), 0.11, 0.08, 0.12, 0.09, M['wood_dark'])
    Fr.box((0.1, 1.15, 0.09), (2.72, 0, 0.9), M['wood_dark'])
    Fr.box((0.12, 0.06, 0.12), (tip.x - 0.12, 0, tip.z + 0.02), M['iron'])
    Fr.torus(tip + V(0.07, 0, 0), 0.075, 0.018, M['iron'], rot=(90, 0, 0), segs=16, sides=6)
    Fr.finish(bevel=0.008)
    for i in range(10):
        studs.append(((X1 + 0.05 + i * 0.035, -1.1, 1.48 - i * 0.05), (0, -1, 0)))
    ST.chain('Drawbar chain', coll, (2.72, -0.55, 0.9), (tip.x + 0.02, -0.06, 0.8), M['iron'], sag=0.16)
    ST.chain('Drawbar chain', coll, (2.72, 0.55, 0.9), (tip.x + 0.02, 0.06, 0.8), M['iron'], sag=0.16)
    # front wall sigil plate
    if not F['plated']:
        geo.box('Front sigil plate', (0.035, 0.7, 0.8), (X1 + 0.11, -0.1, 2.32), M['iron'], coll, bevel=0.008)
    for o in emblem_for(F, M)(V(0, 0, 0), coll, 1.6):
        _face_pos_x([o], (X1 + 0.13, -0.1, 2.3))

    # ------------------------------------------------------------ rear: barrel rack, ladder
    R = Batch('Rear rack', coll, random.Random(14))
    R.box((0.48, 2.0, 0.08), (X0 - 0.27, 0, 1.36), M['wood_dark'])
    for y in (-0.9, 0.9):
        R.wedge_box((X0 - 0.48, y, 1.33), (X0 - 0.05, y, 1.0), 0.05, 0.05, 0.05, 0.05, M['iron'])
    R.box((0.04, 2.0, 0.05), (X0 - 0.49, 0, 1.75), M['iron'])
    for y in (-0.9, 0.0, 0.9):
        R.box((0.04, 0.04, 0.42), (X0 - 0.49, y, 1.57), M['iron'])
    ST.barrel(R, (X0 - 0.27, -0.56, 1.69), 0.2, 0.58, M['wood'], M['iron'], var=0.3)
    ST.barrel(R, (X0 - 0.27, -0.08, 1.69), 0.2, 0.58, M['wood'], M['iron'], var=0.7)
    ST.barrel(R, (X0 - 0.25, -0.33, 2.14), 0.17, 0.42, M['wood'], M['iron'], axis='Y', var=0.5)
    # ladder up the rear wall
    for y in (0.35, 0.85):
        R.box((0.05, 0.06, 2.75), (X0 - 0.1, y, 2.55), M['wood_v'])
    for i in range(9):
        R.cyl(0.02, 0.5, (X0 - 0.1, 0.6, 1.5 + i * 0.29), M['wood_v'], rot=(90, 0, 0), segs=8)
    R.finish(bevel=0.008)
    geo.tube('Barrel lashing', [V(X0 - 0.52, -0.9, 1.78), V(X0 - 0.48, -0.56, 1.82), V(X0 - 0.48, -0.08, 1.82),
                                V(X0 - 0.52, 0.3, 1.78)], 0.016, M['rope'], coll, sides=6)

    # ------------------------------------------------------------ deck and parapet
    Dk = Batch('Deck and parapet', coll, random.Random(21))
    for j in range(6):
        y = -SY + (2 * SY) * (j + 0.5) / 6
        split = rng.uniform(-0.6, 0.6)
        for xa_, xb_ in ((X0 - 0.08, split), (split, X1 + 0.08)):
            Dk.box((xb_ - xa_ - 0.01, 2 * SY / 6 - 0.012, 0.07), ((xa_ + xb_) / 2, y, DECK - 0.035), M['deck'])
    OH = 0.2                                   # hoarding overhang beyond the walls
    py = -SY - OH
    # deck lip over the corbels and the corbels themselves
    Dk.box((X1 - X0 + 0.26, OH + 0.05, 0.1), ((X0 + X1) / 2, -SY - OH / 2 + 0.02, DECK - 0.05), M['wood_dark'])
    for x in POSTS + [(a + b) / 2 for a, b in zip(POSTS, POSTS[1:])]:
        Dk.wedge_box((x, -SY - 0.13, WZ1 - 0.3), (x, py + 0.02, DECK - 0.1), 0.09, 0.09, 0.09, 0.1, M['wood_v'])
    for side in (-1, 1):
        yy = side * (SY + (OH if side < 0 else 0.07))
        for x in POSTS:
            Dk.box((0.12, 0.12, 0.72), (x, yy, DECK + 0.33), M['wood_v'])
            if side < 0:
                Dk.box((0.13, 0.03, 0.66), (x, yy - 0.065, DECK + 0.33), M['iron'])
        for xa_, xb_ in zip(POSTS, POSTS[1:]):
            for k in range(2):
                Dk.box((xb_ - xa_ - 0.11, 0.05, 0.155), ((xa_ + xb_) / 2, yy, DECK + 0.09 + k * 0.165), M['wood'])
            Dk.box((xb_ - xa_ + 0.02, 0.1, 0.07), ((xa_ + xb_) / 2, yy, DECK + 0.4), M['wood_dark'])
            Dk.box((xb_ - xa_ + 0.02, 0.11, 0.022), ((xa_ + xb_) / 2, yy, DECK + 0.44), M['brass'] if side < 0 else M['iron'])
    for x in (POSTS[0] - 0.02, POSTS[-1] + 0.05):
        for k in range(2):
            Dk.box((0.05, 2 * SY + 0.07 + OH, 0.155), (x, -(OH - 0.07) / 2, DECK + 0.09 + k * 0.165), M['wood'])
        Dk.box((0.1, 2 * SY + 0.17 + OH, 0.07), (x, -(OH - 0.07) / 2, DECK + 0.4), M['wood_dark'])
        for y in (-0.4, 0.4):
            Dk.box((0.12, 0.12, 0.72), (x, y, DECK + 0.33), M['wood_v'])
    Dk.finish(bevel=0.01)
    for x in POSTS:
        studs.append(((x, py - 0.085, DECK + 0.6), (0, -1, 0)))
        studs.append(((x, py - 0.085, DECK + 0.1), (0, -1, 0)))
    # finials on the near posts (the rear corner post carries the banner)
    for x in POSTS[1:]:
        finial(coll, M, F, (x, py, DECK + 0.69), 1.0)
    # shields along the parapet
    for i, (xa_, xb_) in enumerate(zip(POSTS, POSTS[1:])):
        heater(coll, M, F, ((xa_ + xb_) / 2, py - 0.06, DECK + 0.2), 0.82, emblem=True)

    # ------------------------------------------------------------ banner on the rear corner
    pole_x, pole_y = POSTS[0], py
    top = DECK + 2.42
    geo.cylinder('Banner pole', 0.03, top - DECK + 0.02, (pole_x, pole_y, (top + DECK) / 2), M['wood_v'], coll, 10, bevel=0.004)
    for z in (DECK + 0.75, top - 0.1):
        geo.cylinder('Pole band', 0.036, 0.05, (pole_x, pole_y, z), M['brass'], coll, 10, bevel=0.004)
    F2 = dict(F)
    F2['finial'] = 'spear' if F['finial'] in ('skull', 'flame', 'orb') else F['finial']
    finial(coll, M, F2, (pole_x, pole_y, top), 1.25)
    FH, FL = 0.96, 1.34
    fl, fn = flag('Caravan banner', coll, (pole_x - 0.03, pole_y, top - 0.08), FH, FL, M['cloth'], seed=2,
                  tatter=F['flag_tatter'])
    trim = M['gilt'] if F['banner_trim'] == 'gilt' else M['bone'] if F['banner_trim'] == 'bone' else M['brass']
    geo.tube('Banner hoist', [V(pole_x - 0.03, pole_y, top - 0.06), V(pole_x - 0.03, pole_y, top - 0.08 - FH)], 0.018, trim,
             coll, sides=6)
    flag_emblem = W.emblem_skull(M['bone'], M['glow']) if F['emblem'] == 'skull' else emblem_for(F, M)
    em = flag_emblem(V(0, 0, 0), coll, 2.0)
    conform(em, fn, 0.34, 0.5, FL, FH, lift=0.012)
    # trim band along the top edge of the flag
    geo.tube('Banner edge', [Vector(fn(u / 20, 0.02)) + V(0, -0.006, 0) for u in range(0, 19)], 0.012, trim, coll, sides=6)

    # ------------------------------------------------------------ shrine lantern cupola (front)
    cx, cy = 1.5, 0.06
    base = V(cx, cy, DECK)
    sh = Batch('Shrine cupola', coll, random.Random(31))
    sh.lathe([(0, 0), (0.56, 0), (0.56, 0.1), (0.5, 0.12), (0.5, 0.22), (0.54, 0.24), (0.54, 0.29), (0, 0.29)],
             M['wood_dark'], Matrix.Translation(base), 8)
    sh.lathe([(0.545, 0.085), (0.575, 0.09), (0.575, 0.12), (0.545, 0.125)], M['brass'], Matrix.Translation(base), 8)
    sh.lathe([(0.535, 0.24), (0.56, 0.245), (0.56, 0.29), (0.535, 0.295)], M['brass'], Matrix.Translation(base), 8)
    ptop = DECK + 1.12
    for i in range(8):
        a = math.tau * (i + 0.5) / 8
        p = base + V(math.cos(a) * 0.44, math.sin(a) * 0.44, 0.29)
        sh.box((0.07, 0.07, ptop - p.z), p + V(0, 0, (ptop - p.z) / 2), M['wood_v'], rot=(0, 0, math.degrees(a)))
        sh.box((0.09, 0.09, 0.05), p + V(0, 0, 0.04), M['brass'], rot=(0, 0, math.degrees(a)))
        sh.box((0.09, 0.09, 0.05), V(p.x, p.y, ptop - 0.04), M['brass'], rot=(0, 0, math.degrees(a)))
    sh.lathe([(0.0, ptop), (0.62, ptop), (0.62, ptop + 0.07), (0.0, ptop + 0.07)], M['wood_dark'], Matrix.Translation((cx, cy, 0)), 8)
    sh.lathe([(0.6, ptop - 0.01), (0.64, ptop - 0.005), (0.64, ptop + 0.02), (0.6, ptop + 0.025)], M['brass'],
             Matrix.Translation((cx, cy, 0)), 8)
    # ogee dome
    dome_mat = {'brass': M['verdigris'], 'gold': M['gilt'], 'iron': M['iron'], 'bone': M['bone'], 'plate': M['plate']}[F['dome']]
    prof = [(0.6, ptop + 0.07)]
    for i in range(1, 13):
        t = i / 12
        r = 0.6 * (1 - t) ** 0.9 * (1 + 0.32 * math.sin(math.pi * min(1, t * 1.6)))
        prof.append((max(r * (1 - t ** 4), 0.0), ptop + 0.07 + 0.62 * t))
    prof[-1] = (0.0, prof[-1][1])
    sh.lathe(prof, dome_mat, Matrix.Translation((cx, cy, 0)), 8)
    sh.finish(bevel=0.008)
    dome_top = V(cx, cy, ptop + 0.69)
    for i in range(8):
        a = math.tau * i / 8
        pts = [V(cx + math.cos(a) * r * 1.02, cy + math.sin(a) * r * 1.02, z) for r, z in prof[:-1]] + [dome_top]
        geo.tube('Dome rib', pts, 0.018, M['brass'], coll, sides=6)
    # the shrine lantern itself
    lobjs, lc = ST.lantern('Shrine lantern', coll, V(cx, cy, DECK + 0.4), M, size=2.25, light=None)
    geo.tube('Shrine chain', [V(cx, cy, ptop), V(cx, cy, DECK + 0.4 + 0.87)], 0.012, M['iron'], coll, sides=5)
    lights.append(core.point_light('Shrine glow', lc + V(0, -0.62, 0.1), 60 * F['light_power'], core.srgb(F['light']), 0.1, coll))
    lights.append(core.point_light('Shrine glow rear', lc + V(0, 0.62, 0.1), 30 * F['light_power'], core.srgb(F['light']), 0.1, coll))
    # finial atop the dome
    fz = dome_top.z
    if F['crown']:
        Cr = Batch('Crown finial', coll, random.Random(2))
        Cr.ring((cx, cy, fz + 0.06), 0.1, 0.13, 0.08, M['gilt'], 'Z', 24)
        for i in range(6):
            a = math.tau * i / 6
            Cr.wedge_box((cx + math.cos(a) * 0.115, cy + math.sin(a) * 0.115, fz + 0.1),
                         (cx + math.cos(a) * 0.13, cy + math.sin(a) * 0.13, fz + 0.24), 0.05, 0.01, 0.025, 0.01, M['gilt'])
            Cr.sphere(0.018, (cx + math.cos(a) * 0.13, cy + math.sin(a) * 0.13, fz + 0.25), M['gilt'])
        Cr.finish(bevel=0.003)
        geo.sphere('Crown jewel', 0.035, (cx, cy - 0.12, fz + 0.06), S.gem('2b6cff', name='Sapphire', glow=0.8), coll, 12, 8)
    elif F['crystals']:
        geo.lathe('Radiant crystal', [(0, 0), (0.09, 0.12), (0.09, 0.36), (0, 0.55)], 6, M['crystal'], coll, location=(cx, cy, fz))
        lights.append(core.point_light('Crystal glow', (cx, cy, fz + 0.3), 40, core.srgb('bff4ff'), 0.05, coll))
    elif F['trophies']:
        ST.oriented_skull('Dome skull', coll, (cx, cy, fz + 0.14), 0.13, dict(bone=M['bone'], dark=M['dark'], glow=M['glow']),
                          facing=(0.3, -1, 0), jaw_open=0.35)
    else:
        fin = W.emblem_sun(M['gilt'])(V(0, 0, 0), coll, 1.3)
        _face_neg_y(fin, (cx, cy - 0.02, fz + 0.2))
        geo.lathe('Sun finial stem', [(0.03, 0), (0.025, 0.1), (0, 0.12)], 8, M['gilt'], coll, location=(cx, cy, fz - 0.02))
    if F['halo']:
        geo.lathe('Halo ring', [(0.42, -0.012), (0.45, -0.012), (0.45, 0.012), (0.42, 0.012)], 48, M['gilt'], coll,
                  rotation=(90, 0, 0), location=(cx, cy + 0.12, fz + 0.32))
        for k in range(16):
            a = math.tau * k / 16
            geo.tube('Halo ray', [V(cx + math.cos(a) * 0.47, cy + 0.12, fz + 0.32 + math.sin(a) * 0.47),
                                  V(cx + math.cos(a) * (0.58 + 0.06 * (k % 2)), cy + 0.12, fz + 0.32 + math.sin(a) * (0.58 + 0.06 * (k % 2)))],
                     0.012, M['gilt'], coll, sides=4)

    # ------------------------------------------------------------ hanging lanterns
    lamps = []

    def wall_lantern(p, d, length=0.42, size=1.25):
        objs, hang = ST.bracket('Lantern bracket', coll, p, d, length, M['iron'])
        lo, fc = ST.lantern('Hanging lantern', coll, hang - V(0, 0, 0.2 + 0.43 * size), M, size=size)
        lamps.append(fc)
        lights.append(core.point_light('Lantern glow', fc + Vector(d).normalized() * 0.22 + V(0, -0.18, -0.1),
                                       22 * F['light_power'], core.srgb(F['light']), 0.05, coll))
        return fc
    wall_lantern(V(X0 - 0.08, -SY - 0.05, 3.0), V(-1, -0.15, 0), 0.5)
    wall_lantern(V(X1 + 0.12, -SY + 0.1, 3.0), V(1, -0.35, 0), 0.42)

    # ------------------------------------------------------------ deck dressing (mostly behind the parapet)
    Dd = Batch('Deck stores', coll, random.Random(41))
    ST.crate(Dd, (-2.05, 0.55, DECK + 0.22), (0.5, 0.48, 0.44), M['wood'], M['iron'], rot=(0, 0, 8))
    ST.barrel(Dd, (-1.55, 0.7, DECK + 0.27), 0.19, 0.54, M['wood'], M['iron'])
    ST.crate(Dd, (0.2, 0.62, DECK + 0.16), (0.42, 0.4, 0.32), M['wood'], None, rot=(0, 0, -12))
    Dd.finish(bevel=0.008)

    # ------------------------------------------------------------ skin extras
    if F['trophies']:
        bm = dict(bone=M['bone'], dark=M['dark'], glow=M['glow'], horn=S.bone('3a3029', name='Horn', stain='15100c', crack=.2))
        ST.oriented_skull('Prow trophy', coll, (X1 + 0.52, -0.0, 1.2), 0.2, bm, facing=(1, -0.35, 0), jaw_open=0.4,
                          horns=dict(length=1.5, curl=0.8, thick=0.24, ram=True))
        for x in (-1.93, 1.83):
            ST.oriented_skull('Wall skull', coll, (x, -SY - 0.2, 2.82), 0.09, bm, facing=(0.2, -1, 0), jaw_open=0.25)
        for x in (-0.89 - 0.5, -0.89 + 0.5):
            for k in range(4):
                geo.tube('Bone garland', [V(x - 0.04, -SY - 0.2, 2.95 - k * 0.12), V(x + 0.04, -SY - 0.2, 2.9 - k * 0.12)],
                         0.022, M['bone'], coll, sides=6)
        # ribs arching over the shrine
        for k in range(4):
            a = math.radians(-50 + k * 33)
            pts = [V(cx + math.cos(a) * 0.62, cy + math.sin(a) * 0.62, ptop + 0.05),
                   V(cx + math.cos(a) * 0.7, cy + math.sin(a) * 0.7, ptop + 0.35),
                   V(cx + math.cos(a) * 0.45, cy + math.sin(a) * 0.45, ptop + 0.7)]
            geo.tube('Rib arch', catmull(pts, 4), [0.03, 0.03, 0.028, 0.025, 0.022, 0.02, 0.016, 0.014, 0.012][:9], M['bone'],
                     coll, sides=6)
    if F['braziers']:
        cm = ST.coals('ff6a1a', strength=10)
        for p in ((-2.08, 0.45), (-0.6, 0.55)):
            objs, fb = ST.brazier('Deck brazier', coll, (p[0], p[1], DECK), dict(iron=M['iron']), M['flame'], cm,
                                  light=core.srgb('ff7a2a'), size=0.75, height=0.55, light_power=70, seed=int(p[0] * 7) % 5)
            flames.extend(o for o in objs if 'fire' in o.name)
        # ember vents along the bed
        for x in (-1.0, -0.2, 0.55):
            geo.box('Forge vent glow', (0.22, 0.02, 0.05), (x, -1.2, 1.39), S.emissive('ff6a1a', 6, name='Vent glow'), coll, bevel=0.005)
    if F['coins']:
        Cc = Batch('Coin chests', coll, random.Random(51))
        for (x, y, r) in ((-2.0, 0.5, 6), (-0.15, 0.55, -10)):
            Cc.box((0.56, 0.38, 0.26), (x, y, DECK + 0.13), M['wood'], rot=(0, 0, r))
            for dx in (-0.2, 0.2):
                Cc.box((0.05, 0.4, 0.28), (x + dx, y, DECK + 0.13), M['gilt'], rot=(0, 0, r))
            Cc.sphere(0.24, (x, y, DECK + 0.24), M['gilt'], segs=16, rings=8, scale=(1.05, .7, .35), rot=(0, 0, r))
        Cc.finish(bevel=0.006)
        for i in range(14):
            a = rng.uniform(0, math.tau)
            geo.cylinder('Loose coin', 0.035, 0.008, (-2.0 + math.cos(a) * 0.22, 0.5 + math.sin(a) * 0.16, DECK + 0.38 + rng.uniform(0, .05)),
                         M['gilt'], coll, 12, bevel=0.002, rotation=(rng.uniform(-30, 30), rng.uniform(-30, 30), 0))
    if F['crystals']:
        for x in (-1.93, 1.83):
            geo.lathe('Wall crystal', [(0, 0), (0.05, 0.08), (0.05, 0.22), (0, 0.32)], 6, M['crystal'], coll,
                      location=(x, -SY - 0.2, 2.75), rotation=(0, 0, 30))

    geo.rivets('Brass rivets', studs, 0.022, M['stud'], coll)
    return dict(lights=lights, flames=flames, lamps=lamps, shrine=lc)

