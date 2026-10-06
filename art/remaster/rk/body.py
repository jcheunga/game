"""Humanoid proportions, skeleton and sculpted base body.

The body is assembled from lofted torso sections and tapered limb tubes, fused
with a voxel remesh and relaxed, giving one clean organic surface. It is skinned
with distance weights and topological smoothing. Armour and props attach rigidly
to bones; garments copy weights from the body.
"""
import math

import bpy
from mathutils import Vector

from . import geo
from .rig import Skeleton, weight_by_bones

DEFAULT = dict(
    scale=1.0,          # uniform size
    pelvis=0.94,        # pelvis height
    chest=1.40,         # chest centre
    neck=1.60,          # base of neck
    head=0.40,          # head height
    shoulder=0.27,      # shoulder joint half-width
    hip=0.115,          # hip joint half-width
    bulk=1.0,           # limb/torso girth multiplier
    chest_w=1.0,        # chest breadth multiplier
    belly=0.0,          # belly forward bulge
    hunch=0.0,          # forward hunch of upper body (deg) applied in rest
    arm_len=1.0,
    leg_len=1.0,
    hand=1.0,           # hand size multiplier
    thin=0.0,           # 0..1, skeletal/emaciated
    spread=14.0,        # A-pose arm angle from vertical (deg)
    stance=0.0,         # extra leg spread (m)
    digitigrade=False,
    muscle=0.0,         # 0..1 heroic V-taper: broad pecs and lats, narrow waist
)


def spec(**kw):
    s = dict(DEFAULT)
    s.update(kw)
    return s


def joints(s):
    """World-space joint positions for a humanoid spec (rest pose)."""
    k = s['scale']
    ll = s['leg_len']
    al = s['arm_len']
    pel = s['pelvis'] * ll
    lift = pel - s['pelvis']
    chest = s['chest'] + lift
    neck = s['neck'] + lift
    hunch = math.radians(s['hunch'])

    def hz(z, base=pel):
        # Bend the upper body forward about the pelvis for hunched figures.
        d = z - base
        return Vector((math.sin(hunch) * d * (d / max(0.01, neck - base)), 0, base + math.cos(hunch * .5) * d))

    J = {}
    J['root'] = Vector((0, 0, 0))
    J['pelvis'] = Vector((0, 0, pel))
    J['spine'] = hz(pel + 0.2)
    J['chest'] = hz(chest)
    J['neck'] = hz(neck)
    J['head'] = J['neck'] + Vector((0.015, 0, 0.09))
    J['head_top'] = J['head'] + Vector((0.02, 0, s['head']))
    sh = s['shoulder'] * s['chest_w']
    spread = math.radians(s['spread'])
    for side, sy in (('R', -1), ('L', 1)):
        sp = J['chest'] + Vector((0, sy * sh, 0.13))
        upper = 0.33 * al
        fore = 0.30 * al
        el = sp + Vector((0.01, sy * math.sin(spread) * upper, -math.cos(spread) * upper))
        wr = el + Vector((0.05, sy * math.sin(spread * .6) * fore, -math.cos(spread * .6) * fore))
        ht = wr + Vector((0.025, sy * 0.01, -0.13 * s['hand']))
        J['shoulder.' + side] = sp
        J['clav.' + side] = J['chest'] + Vector((0, sy * 0.06, 0.12))
        J['elbow.' + side] = el
        J['wrist.' + side] = wr
        J['hand_tip.' + side] = ht
        hp = J['pelvis'] + Vector((0, sy * s['hip'], -0.04))
        thigh = 0.42 * ll
        shin = 0.41 * ll
        foot_y = sy * (s['hip'] + 0.015 + s['stance'])
        kn = Vector((0.035, foot_y * .98, hp.z - thigh))
        an = Vector((-0.005, foot_y, max(0.08, kn.z - shin)))
        if s['digitigrade']:
            kn = Vector((0.09, foot_y, hp.z - thigh * .9))
            an = Vector((-0.08, foot_y, 0.2))
        J['hip.' + side] = hp
        J['knee.' + side] = kn
        J['ankle.' + side] = an
        J['toe.' + side] = Vector((an.x + 0.2, foot_y, 0.03))
    return J


def skeleton(name, s, coll, extras=None):
    J = joints(s)
    sk = Skeleton(name, coll)
    sk.bone('root', (0, 0, 0), (0, 0, 0.25), deform=False)
    sk.bone('hips', J['pelvis'], J['spine'], 'root')
    sk.bone('spine', J['spine'], J['chest'], 'hips', connect=True)
    sk.bone('chest', J['chest'], J['neck'], 'spine', connect=True)
    sk.bone('neck', J['neck'], J['head'], 'chest', connect=True)
    sk.bone('head', J['head'], J['head_top'], 'neck', connect=True)
    for side in ('R', 'L'):
        sk.bone('shoulder.' + side, J['clav.' + side], J['shoulder.' + side], 'chest')
        sk.bone('upper_arm.' + side, J['shoulder.' + side], J['elbow.' + side], 'shoulder.' + side, connect=True)
        sk.bone('forearm.' + side, J['elbow.' + side], J['wrist.' + side], 'upper_arm.' + side, connect=True)
        sk.bone('hand.' + side, J['wrist.' + side], J['hand_tip.' + side], 'forearm.' + side, connect=True)
        sk.bone('thigh.' + side, J['hip.' + side], J['knee.' + side], 'hips')
        sk.bone('shin.' + side, J['knee.' + side], J['ankle.' + side], 'thigh.' + side, connect=True)
        sk.bone('foot.' + side, J['ankle.' + side], J['toe.' + side], 'shin.' + side, connect=True)
    if extras:
        extras(sk, J)
    arm = sk.build()
    return arm, J


DEFORM = ['hips', 'spine', 'chest', 'neck', 'head',
          'shoulder.R', 'upper_arm.R', 'forearm.R', 'hand.R', 'thigh.R', 'shin.R', 'foot.R',
          'shoulder.L', 'upper_arm.L', 'forearm.L', 'hand.L', 'thigh.L', 'shin.L', 'foot.L']


def ellipse(cx, cz, rx, ry, n=20, front=0.0, back=0.0, y0=0.0):
    """Horizontal cross-section; front/back push the +X / -X sides."""
    pts = []
    for i in range(n):
        a = i * math.tau / n
        x = math.cos(a) * rx
        y = math.sin(a) * ry
        if x > 0:
            x *= 1 + front * (x / rx) ** 2
        else:
            x *= 1 + back * (x / rx) ** 2
        pts.append(Vector((cx + x, y0 + y, cz)))
    return pts


def torso_sections(s, J, inflate=0.0, z_from=None, z_to=None, n=24):
    """Torso loft profile from hips to neck; also reused (inflated) for armour."""
    b = s['bulk']
    cw = s['chest_w']
    th = 1 - 0.35 * s['thin']
    mu = s.get('muscle', 0.0)
    pel, chest, neck = J['pelvis'], J['chest'], J['neck']
    rows = [
        (pel.z - 0.12, pel.x, 0.15, 0.19, 0.0, 0.1),
        (pel.z - 0.02, pel.x, 0.165, 0.205, 0.0, 0.15),
        (pel.z + 0.12, pel.x + 0.01, 0.15 + 0.06 * s['belly'] - 0.02 * mu, 0.18 * (1 - 0.12 * mu), 0.15 + s['belly'], 0.0),
        (J['spine'].z + 0.06, J['spine'].x + 0.01, 0.155 + 0.05 * s['belly'], 0.19 * cw * (1 - 0.06 * mu),
         0.2 + s['belly'] * .6, 0.0),
        (chest.z - 0.02, chest.x, 0.17 + 0.02 * mu, 0.235 * cw * (1 + 0.1 * mu), 0.22 + 0.35 * mu, 0.05),
        (chest.z + 0.1, chest.x - 0.005, 0.165 + 0.015 * mu, 0.25 * cw * (1 + 0.12 * mu), 0.15 + 0.3 * mu, 0.08),
        (neck.z - 0.03, neck.x - 0.01, 0.125, 0.2 * cw, 0.05, 0.1),
        (neck.z + 0.02, neck.x, 0.08, 0.1, 0.0, 0.0),
    ]
    secs = []
    for z, x, rx, ry, front, back in rows:
        if z_from is not None and z < z_from - 1e-6:
            continue
        if z_to is not None and z > z_to + 1e-6:
            continue
        rx = rx * b * th + inflate
        ry = ry * b * th + inflate
        secs.append(ellipse(x, z, rx, ry, n, front, back))
    return secs


TAG_BONES = {
    'torso': ['hips', 'spine', 'chest'],
    'neck': ['chest', 'neck', 'head'],
}
for _side in ('R', 'L'):
    TAG_BONES['uarm.' + _side] = ['chest', 'shoulder.' + _side, 'upper_arm.' + _side, 'forearm.' + _side]
    TAG_BONES['farm.' + _side] = ['upper_arm.' + _side, 'forearm.' + _side, 'hand.' + _side]
    TAG_BONES['hand.' + _side] = ['forearm.' + _side, 'hand.' + _side]
    TAG_BONES['thigh.' + _side] = ['hips', 'thigh.' + _side, 'shin.' + _side]
    TAG_BONES['shin.' + _side] = ['thigh.' + _side, 'shin.' + _side, 'foot.' + _side]
TAG_MAT = {'torso': 1, 'neck': 0}
for _side in ('R', 'L'):
    TAG_MAT.update({'uarm.' + _side: 2, 'farm.' + _side: 2, 'hand.' + _side: 3,
                    'thigh.' + _side: 4, 'shin.' + _side: 4})


def build_body(name, s, J, coll, mats, voxel=0.018, smooth=8, extra_parts=None, forearm_mat=None):
    """mats: dict(skin, torso, sleeve, glove, legs) -> materials (may repeat).

    Returns the fused body. Each vertex remembers which source part it came
    from (`part` attribute) so weights and materials respect anatomy.
    """
    from mathutils.bvhtree import BVHTree
    b = s['bulk']
    th = 1 - 0.45 * s['thin']
    parts = []
    parts.append((geo.loft('torso', torso_sections(s, J), None, coll), 'torso'))
    parts.append((geo.tube('neck', [J['neck'] - Vector((0, 0, .05)), J['head'] + Vector((0, 0, .05))],
                           [0.085 * b * th, 0.075 * b * th], None, coll, sides=12), 'neck'))
    for side, sy in (('R', -1), ('L', 1)):
        sp, el, wr, ht = J['shoulder.' + side], J['elbow.' + side], J['wrist.' + side], J['hand_tip.' + side]
        # deltoid cap, biceps swell, then the narrow elbow
        pts = [sp + Vector((0, -sy * 0.03, 0.03)), sp.lerp(el, .2), sp.lerp(el, .45), sp.lerp(el, .7), el]
        parts.append((geo.tube('upper arm', pts, [0.108 * b * th, 0.096 * b * th, 0.094 * b * th, 0.082 * b * th,
                                                  0.066 * b * th], None, coll, sides=14), 'uarm.' + side))
        parts.append((geo.sphere('shoulder ball', 0.11 * b * th, sp + Vector((0, sy * .01, .01)), None, coll, 14, 10),
                      'uarm.' + side))
        # forearm swells just below the elbow and tapers to a slim wrist
        pts = [el, el.lerp(wr, .22), el.lerp(wr, .55), wr]
        parts.append((geo.tube('forearm', pts, [0.068 * b * th, 0.079 * b * th, 0.066 * b * th, 0.048 * b], None,
                               coll, sides=14), 'farm.' + side))
        hs = s['hand']
        parts.append((geo.sphere('hand', 1, wr.lerp(ht, .5), None, coll, 14, 10,
                                 scale=(0.06 * hs * b, 0.045 * hs * b, 0.085 * hs)), 'hand.' + side))
        parts.append((geo.tube('thumb', [wr.lerp(ht, .2) + Vector((.03, 0, 0)),
                                         wr.lerp(ht, .55) + Vector((.07 * hs, -sy * .01, 0))],
                               [0.026 * hs, 0.02 * hs], None, coll, sides=8), 'hand.' + side))
        hp, kn, an = J['hip.' + side], J['knee.' + side], J['ankle.' + side]
        pts = [hp + Vector((0, 0, 0.06)), hp.lerp(kn, .25), hp.lerp(kn, .55), hp.lerp(kn, .82), kn]
        parts.append((geo.tube('thigh', pts, [0.132 * b * th, 0.126 * b * th, 0.112 * b * th, 0.094 * b * th,
                                              0.082 * b * th], None, coll, sides=14), 'thigh.' + side))
        # calf swell high on the back of the shin, then a slim ankle
        pts = [kn, kn.lerp(an, .25) + Vector((-.016, 0, 0)), kn.lerp(an, .5) + Vector((-.008, 0, 0)), kn.lerp(an, .8), an]
        parts.append((geo.tube('shin', pts, [0.082 * b * th, 0.094 * b * th, 0.082 * b * th, 0.06 * b * th,
                                             0.054 * b], None, coll, sides=14), 'shin.' + side))
        parts.append((geo.sphere('knee', 0.085 * b * th, kn, None, coll, 12, 8), 'shin.' + side))
    if extra_parts:
        parts.extend(extra_parts)
    trees = []
    for obj, tag in parts:
        me = obj.data
        trees.append((BVHTree.FromPolygons([v.co.copy() for v in me.vertices],
                                           [tuple(p.vertices) for p in me.polygons]), tag))
    body = geo.join([o for o, _ in parts], name)
    mod = body.modifiers.new('Fuse', 'REMESH')
    mod.mode = 'VOXEL'
    mod.voxel_size = voxel
    mod.adaptivity = 0
    mod.use_smooth_shade = True
    sm = body.modifiers.new('Relax', 'SMOOTH')
    sm.iterations = smooth
    sm.factor = 0.5
    geo.apply_modifiers(body)
    for p in body.data.polygons:
        p.use_smooth = True
    tags = []
    for v in body.data.vertices:
        best, tag = 1e9, 'torso'
        for tree, t in trees:
            hit = tree.find_nearest(v.co)
            if hit[0] is not None and hit[3] < best:
                best, tag = hit[3], t
        tags.append(tag)
    body['part_tags'] = tags
    for key in ['skin', 'torso', 'sleeve', 'glove', 'legs', 'forearm']:
        body.data.materials.append(mats.get(key) or (mats.get('sleeve') if key == 'forearm' else mats['torso']))
    me = body.data
    for p in me.polygons:
        counts = {}
        for vi in p.vertices:
            counts[tags[vi]] = counts.get(tags[vi], 0) + 1
        tag = max(counts, key=counts.get)
        idx = TAG_MAT.get(tag, 1)
        if tag.startswith('farm') and mats.get('forearm'):
            idx = 5
        if tag == 'neck' and p.center.z < J['neck'].z - 0.01:
            idx = 1
        p.material_index = idx
    geo.store_rest(body)
    return body


def skin_body(body, arm, s, J):
    tags = list(body['part_tags'])
    verts = body.data.vertices

    # Explicit per-vertex allowance from the source anatomy.
    allowed = [set(TAG_BONES.get(t, DEFORM)) for t in tags]
    # widen near junctions: include neighbours' allowances (topological dilation)
    nb = [[] for _ in range(len(verts))]
    for e in body.data.edges:
        a, b = e.vertices
        nb[a].append(b)
        nb[b].append(a)
    for _ in range(3):
        allowed = [allowed[i].union(*[allowed[j] for j in nb[i]]) if nb[i] else allowed[i] for i in range(len(verts))]
    return weight_by_bones(body, arm, DEFORM, power=5.0, smooth=8, smooth_factor=.5, allow=allowed)
