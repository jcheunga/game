"""Quadrupeds: war hounds and horses (living or skeletal), with rig, fused body and clips."""
import math

from mathutils import Vector
from mathutils.bvhtree import BVHTree

from . import geo
from .anim import P
from .heads import catmull
from .rig import LAT, YAW, ROLL, Skeleton, add_poses, blend_poses, smooth, weight_by_bones

# Side-profile anatomy tables (x forward, z up). Barrel rows: (x, top z, bottom z, half-width).
HOUND = dict(
    barrel=[(-0.5, 0.74, 0.5, 0.11), (-0.4, 0.8, 0.46, 0.15), (-0.25, 0.78, 0.5, 0.15), (-0.1, 0.77, 0.52, 0.14),
            (0.05, 0.79, 0.44, 0.15), (0.2, 0.83, 0.36, 0.17), (0.32, 0.84, 0.38, 0.17), (0.42, 0.8, 0.46, 0.14)],
    neck=[(0.36, 0.78, 0.13), (0.48, 0.86, 0.12), (0.58, 0.94, 0.105)],
    head=dict(poll=(0.6, 1.02), skull=(0.66, 0.97, 0.13, 0.12, 0.115), muzzle=((0.76, 0.93), (0.9, 0.9), 0.085, 0.07),
              snout=(0.93, 0.9), ear=0.09),
    pelvis=(-0.36, 0.7), chest=(0.3, 0.72), neckb=(0.44, 0.84), headb=(0.6, 1.0), snout=(0.95, 0.9),
    fore=[(0.3, 0.68), (0.27, 0.4), (0.3, 0.14), (0.37, 0.035)], hind=[(-0.38, 0.7), (-0.24, 0.42), (-0.44, 0.2), (-0.38, 0.035)],
    fore_r=(0.085, 0.06, 0.048, 0.05), hind_r=(0.11, 0.07, 0.05, 0.05), sh_w=0.14, hip_w=0.12, tail=0.42, neck_r=0.12,
)
HORSE = dict(
    barrel=[(-0.82, 1.38, 1.12, 0.17), (-0.7, 1.47, 1.02, 0.27), (-0.5, 1.46, 0.98, 0.3), (-0.25, 1.4, 0.95, 0.31),
            (0.0, 1.38, 0.9, 0.32), (0.25, 1.42, 0.86, 0.32), (0.45, 1.52, 0.9, 0.29), (0.62, 1.5, 1.0, 0.22)],
    neck=[(0.5, 1.45, 0.24), (0.7, 1.65, 0.19), (0.85, 1.85, 0.15), (0.98, 1.98, 0.12)],
    head=dict(poll=(1.0, 2.02), skull=(1.06, 1.92, 0.22, 0.12, 0.13), muzzle=((1.18, 1.78), (1.38, 1.56), 0.095, 0.085),
              snout=(1.42, 1.54), ear=0.14),
    pelvis=(-0.62, 1.3), chest=(0.45, 1.3), neckb=(0.72, 1.66), headb=(1.0, 1.98), snout=(1.42, 1.55),
    fore=[(0.48, 1.12), (0.44, 0.82), (0.5, 0.42), (0.55, 0.04)], hind=[(-0.62, 1.2), (-0.45, 0.82), (-0.74, 0.5), (-0.68, 0.04)],
    fore_r=(0.13, 0.08, 0.05, 0.055), hind_r=(0.17, 0.1, 0.055, 0.055), sh_w=0.2, hip_w=0.18, tail=0.55, neck_r=0.2,
)


def _v(*a):
    return Vector(a)


def joints(kind):
    k = dict(HOUND if kind == 'hound' else HORSE)
    J = {}
    J['pelvis'] = _v(k['pelvis'][0], 0, k['pelvis'][1])
    J['chest'] = _v(k['chest'][0], 0, k['chest'][1])
    J['spine'] = J['pelvis'].lerp(J['chest'], .5) + _v(0, 0, 0.02)
    J['neck'] = _v(k['neckb'][0], 0, k['neckb'][1])
    J['head'] = _v(k['headb'][0], 0, k['headb'][1])
    J['snout'] = _v(k['snout'][0], 0, k['snout'][1])
    for side, sy in (('R', -1), ('L', 1)):
        names = ['top', 'elbow', 'wrist', 'paw']
        for n, (x, z) in zip(names, k['fore']):
            J['fl.%s.%s' % (side, n)] = _v(x, sy * k['sh_w'], z)
        names = ['top', 'knee', 'hock', 'paw']
        for n, (x, z) in zip(names, k['hind']):
            J['bl.%s.%s' % (side, n)] = _v(x, sy * k['hip_w'], z)
    back = k['barrel'][0]
    J['tail0'] = _v(back[0] + 0.02, 0, back[1] - 0.03)
    J['tail1'] = J['tail0'] + _v(-k['tail'] * .5, 0, 0.02)
    J['tail2'] = J['tail1'] + _v(-k['tail'] * .5, 0, -0.1)
    J['kind'] = kind
    J['k'] = k
    return J


def skeleton(kind):
    def build(ident, spec, coll):
        J = joints(kind)
        sk = Skeleton(ident, coll)
        sk.bone('root', (0, 0, 0), (0, 0, 0.25), deform=False)
        sk.bone('hips', J['pelvis'], J['spine'], 'root')
        sk.bone('spine', J['spine'], J['chest'], 'hips', connect=True)
        sk.bone('chest', J['chest'], J['neck'], 'spine', connect=True)
        sk.bone('neck', J['neck'], J['head'], 'chest', connect=True)
        sk.bone('head', J['head'], J['snout'], 'neck', connect=True)
        sk.bone('jaw', J['head'] + _v(0, 0, -0.05), J['snout'] + _v(-0.03, 0, -0.08), 'head')
        sk.bone('tail.0', J['tail0'], J['tail1'], 'hips')
        sk.bone('tail.1', J['tail1'], J['tail2'], 'tail.0', connect=True)
        for side in ('R', 'L'):
            f = 'fl.' + side
            sk.bone(f + '.upper', J[f + '.top'], J[f + '.elbow'], 'chest')
            sk.bone(f + '.lower', J[f + '.elbow'], J[f + '.wrist'], f + '.upper', connect=True)
            sk.bone(f + '.foot', J[f + '.wrist'], J[f + '.paw'], f + '.lower', connect=True)
            b = 'bl.' + side
            sk.bone(b + '.upper', J[b + '.top'], J[b + '.knee'], 'hips')
            sk.bone(b + '.lower', J[b + '.knee'], J[b + '.hock'], b + '.upper', connect=True)
            sk.bone(b + '.foot', J[b + '.hock'], J[b + '.paw'], b + '.lower', connect=True)
        sk.bone('saddle', J['spine'] + _v(0.05, 0, 0.1), J['spine'] + _v(0.05, 0, 0.3), 'spine', deform=False)
        arm = sk.build()
        return arm, J
    return build


TAGS = {
    'torso': ['hips', 'spine', 'chest'], 'neck': ['chest', 'neck', 'head'], 'head': ['neck', 'head'],
    'jaw': ['head', 'jaw'], 'tail': ['hips', 'tail.0', 'tail.1'],
}
for _s in ('R', 'L'):
    TAGS['fl.' + _s] = ['chest', 'fl.%s.upper' % _s, 'fl.%s.lower' % _s, 'fl.%s.foot' % _s]
    TAGS['bl.' + _s] = ['hips', 'bl.%s.upper' % _s, 'bl.%s.lower' % _s, 'bl.%s.foot' % _s]
DEFORM = sorted({b for v in TAGS.values() for b in v})


def build_body(ch, mat, head_mat=None, voxel=0.02, gaunt=0.0, mane=None):
    """Fused quadruped body from the side-profile tables. Sets ch.body and ch.body_skin."""
    J = ch.J
    k = J['k']
    horse = J['kind'] == 'horse'
    g = 1 - 0.3 * gaunt
    parts = []
    secs = []
    for x, top, bot, hw in k['barrel']:
        cz, hz = (top + bot) / 2, (top - bot) / 2
        sec = []
        for j in range(22):
            a = j * math.tau / 22
            yy = math.sin(a) * hw * g
            c = math.cos(a)
            zz = c * hz * (1.0 if c > 0 else 0.95)
            # flatter back, rounder belly
            if c > 0:
                yy *= 1.0 - 0.12 * c
            sec.append(_v(x, yy, cz + zz))
        secs.append(sec)
    parts.append((geo.loft('Barrel', secs, None, ch.coll), 'torso'))
    nk = [_v(x, 0, z) for x, z, _ in k['neck']]
    nr = [r * g for _, _, r in k['neck']]
    parts.append((geo.tube('Neck', nk, nr, None, ch.coll, sides=16, flatten=0.8), 'neck'))
    hd = k['head']
    sx, sz, rx, ry, rz = hd['skull']
    skull = geo.quadsphere('Skull', 1.0, (0, 0, 0), None, ch.coll, level=3, scale=(rx, ry, rz))
    geo.place(skull, _v(sx, 0, sz))
    parts.append((skull, 'head'))
    (m0x, m0z), (m1x, m1z), r0, r1 = hd['muzzle']
    parts.append((geo.tube('Muzzle', [_v(m0x, 0, m0z), _v(m1x, 0, m1z)], [r0, r1], None, ch.coll, sides=14, flatten=1.25 if horse else 0.95),
                  'head'))
    if not horse:
        for sy in (-1, 1):  # mastiff jowls and cheek muscle
            parts.append((geo.sphere('Jowl', 1, _v(m0x + 0.02, sy * 0.06, m0z - 0.05), None, ch.coll, 12, 8,
                                     scale=(0.07, 0.045, 0.06)), 'jaw'))
            parts.append((geo.sphere('Cheek', 1, _v(sx, sy * 0.08, sz - 0.02), None, ch.coll, 12, 8, scale=(0.07, 0.05, 0.07)), 'head'))
    jaw_a = _v(m0x - 0.04, 0, m0z - (0.07 if horse else 0.06))
    jaw_b = _v(m1x - 0.02, 0, m1z - (0.06 if horse else 0.05))
    parts.append((geo.tube('Lower jaw', [jaw_a, jaw_b], [r0 * .6, r1 * .5], None, ch.coll, sides=10), 'jaw'))
    tl = [J['tail0'], J['tail1'], J['tail2']]
    parts.append((geo.tube('Tail', tl, [0.045, 0.035, 0.015] if not horse else [0.07, 0.05, 0.03], None, ch.coll, sides=8), 'tail'))
    for side in ('R', 'L'):
        f = 'fl.' + side
        fr = k['fore_r']
        pts = [J[f + '.top'] + _v(0.02, 0, 0.1), J[f + '.elbow'], J[f + '.wrist'], J[f + '.paw'] + _v(0, 0, 0.04)]
        parts.append((geo.tube('Foreleg', pts, [fr[0] * g, fr[1], fr[2], fr[3]], None, ch.coll, sides=12), f))
        parts.append((geo.sphere('Shoulder muscle', 1, J[f + '.top'] + _v(0.0, 0, -0.04), None, ch.coll, 12, 8,
                                 scale=(fr[0] * 1.3, fr[0] * .8, fr[0] * 2.0)), f))
        b = 'bl.' + side
        hr = k['hind_r']
        pts = [J[b + '.top'] + _v(0.04, 0, 0.08), J[b + '.knee'], J[b + '.hock'], J[b + '.paw'] + _v(0, 0, 0.04)]
        parts.append((geo.tube('Hindleg', pts, [hr[0] * g, hr[1], hr[2], hr[3]], None, ch.coll, sides=12), b))
        parts.append((geo.sphere('Haunch', 1, J[b + '.top'] + _v(0.03, 0, -0.06), None, ch.coll, 12, 8,
                                 scale=(hr[0] * 1.4, hr[0] * .85, hr[0] * 1.8)), b))
        if not horse:
            for leg in (f, b):
                parts.append((geo.sphere('Paw', 1, J[leg + '.paw'] + _v(0.03, 0, 0.0), None, ch.coll, 12, 8,
                                         scale=(0.065, 0.05, 0.04)), leg))
    trees = []
    for obj, tag in parts:
        me = obj.data
        trees.append((BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [tuple(p.vertices) for p in me.polygons]), tag))
    bod = geo.join([o for o, _ in parts], ch.ident + ' body')
    mod = bod.modifiers.new('Fuse', 'REMESH')
    mod.mode = 'VOXEL'
    mod.voxel_size = voxel * (1.4 if horse else 1.0)
    sm = bod.modifiers.new('Relax', 'SMOOTH')
    sm.iterations = 10
    sm.factor = .5
    geo.apply_modifiers(bod)
    for p in bod.data.polygons:
        p.use_smooth = True
    bod.data.materials.append(mat)
    bod.data.materials.append(head_mat or mat)
    tags = []
    for v in bod.data.vertices:
        best, tag = 1e9, 'torso'
        for tree, t in trees:
            hit = tree.find_nearest(v.co)
            if hit[0] is not None and hit[3] < best:
                best, tag = hit[3], t
        tags.append(tag)
    for p in bod.data.polygons:
        if tags[p.vertices[0]] in ('head', 'jaw') and p.center.x > hd['muzzle'][0][0]:
            p.material_index = 1
    geo.store_rest(bod)
    ch.body = bod

    def skin(body_obj, arm, tags=tags):
        allowed = [set(TAGS.get(t, DEFORM)) for t in tags]
        nb = [[] for _ in range(len(body_obj.data.vertices))]
        for e in body_obj.data.edges:
            a, b = e.vertices
            nb[a].append(b)
            nb[b].append(a)
        for _ in range(3):
            allowed = [allowed[i].union(*[allowed[j] for j in nb[i]]) if nb[i] else allowed[i] for i in range(len(nb))]
        weight_by_bones(body_obj, arm, DEFORM, power=5.0, smooth=8, smooth_factor=.5, allow=allowed)
    ch.body_skin = skin
    return bod


def head_details(ch, M, eye_mat, ear=True, teeth=True, horse=False, glow_eyes=False):
    J = ch.J
    k = J['k']
    hd = k['head']
    sx, sz, rx, ry, rz = hd['skull']
    out = []
    for sy in (-1, 1):
        e = _v(sx + rx * (0.55 if not horse else 0.25), sy * ry * 0.85, sz + rz * 0.35)
        out.append((geo.sphere('Eye', ry * .22, e, eye_mat, ch.coll, 10, 8), 'head'))
        if ear:
            base = _v(sx - rx * .35, sy * ry * .7, sz + rz * .8)
            if horse:
                tip = base + _v(0.0, sy * 0.03, hd['ear'])
                out.append((geo.tube('Ear', [base, base.lerp(tip, .5), tip], [0.035, 0.03, 0.004], M['fur'], ch.coll, sides=8,
                                     flatten=.5), 'head'))
            else:
                tip = base + _v(0.03, sy * 0.06, -0.02)
                out.append((geo.tube('Folded ear', [base, base + _v(-0.01, sy * 0.04, 0.03), tip], [0.035, 0.04, 0.01], M['fur'],
                                     ch.coll, sides=8, flatten=.35), 'head'))
    if teeth:
        for sy in (-1, 1):
            p = _v(hd['snout'][0] - 0.05, sy * 0.035, hd['snout'][1] - 0.06)
            out.append((geo.cylinder('Fang', 0.011, 0.06, p, M['bone'], ch.coll, 6, radius2=0.002, rotation=(180, 0, 0),
                                     bevel=0), 'head'))
    nose = geo.sphere('Nose', 0.035 if not horse else 0.05, _v(hd['snout'][0], 0, hd['snout'][1] + 0.01), M['dark'], ch.coll, 10, 8,
                      scale=(0.8, 1.2, 0.8))
    out.append((nose, 'head'))
    if horse:
        for sy in (-1, 1):
            out.append((geo.sphere('Nostril', 0.02, _v(hd['snout'][0] - 0.01, sy * 0.04, hd['snout'][1] + 0.01), M['dark'], ch.coll,
                                   8, 6), 'head'))
    ch.add(out)


# ------------------------------------------------------------------ clips
def stance(kind):
    if kind == 'hound':
        return P(neck=[(4, LAT)], head=[(6, LAT)], tail_0=0)
    return P(neck=[(-4, LAT)], head=[(10, LAT)])


def _legs(pose, ph, amount, gallop=False):
    """Diagonal-pair trot: FR with BL, FL with BR."""
    out = {}
    for side, off in (('R', 0.0), ('L', math.pi)):
        fph = ph + off
        bph = ph + off + math.pi
        out['fl.%s.upper' % side] = amount * 22 * math.sin(fph)
        out['fl.%s.lower' % side] = amount * (-8 + 30 * max(0, math.cos(fph)))
        out['fl.%s.foot' % side] = amount * (-25 * max(0, -math.sin(fph)))
        out['bl.%s.upper' % side] = amount * 20 * math.sin(bph)
        out['bl.%s.lower' % side] = amount * (-6 - 26 * max(0, math.cos(bph)))
        out['bl.%s.foot' % side] = amount * (20 * max(0, math.cos(bph)))
    return add_poses(pose, out)


def clips(kind, profile='pounce', mounted=False):
    st = stance(kind)
    C = {}
    idle = []
    for i in range(4):
        ph = math.tau * i / 4
        s = math.sin(ph)
        idle.append(add_poses(st, P(spine=[(1.2 * s, LAT)], neck=[(2 * s, LAT)], head=[(-2 * s, LAT)],
                                    move={'root': (0, 0, -0.006 * (1 - math.cos(ph)))}),
                              {'tail.0': [(6 * s, YAW)], 'tail.1': [(8 * math.sin(ph - .8), YAW)]}))
    C['idle'] = idle
    walk = []
    for i in range(6):
        ph = math.tau * i / 6
        s = math.sin(ph)
        base = add_poses(st, P(spine=[(2 * math.sin(2 * ph), LAT)], neck=[(3 * math.sin(2 * ph), LAT)],
                               move={'root': (0, 0, 0.025 * abs(math.sin(ph)) - 0.01)}),
                         {'tail.0': [(10 * s, YAW), (6, LAT)], 'tail.1': [(14 * math.sin(ph - .8), YAW)]})
        walk.append(_legs(base, ph, 1.15 if kind == 'hound' else 1.0))
    C['walk'] = walk
    if kind == 'hound':
        wind = P(hips=[(-8, LAT)], spine=[(10, LAT)], neck=[(-10, LAT)], head=[(-8, LAT)],
                 move={'root': (-0.12, 0, -0.08)})
        wind.update({'bl.R.upper': -20, 'bl.L.upper': -20, 'bl.R.lower': -30, 'bl.L.lower': -30, 'bl.R.foot': 40,
                     'bl.L.foot': 40, 'fl.R.upper': 15, 'fl.L.upper': 15, 'fl.R.lower': 40, 'fl.L.lower': 40})
        contact = P(hips=[(6, LAT)], spine=[(-8, LAT)], neck=[(14, LAT)], head=[(10, LAT)], jaw=[(-28, LAT)],
                    move={'root': (0.32, 0, 0.12)})
        contact.update({'fl.R.upper': 60, 'fl.L.upper': 52, 'fl.R.lower': -10, 'fl.L.lower': -5, 'bl.R.upper': -35,
                        'bl.L.upper': -30, 'bl.R.foot': -20, 'bl.L.foot': -20})
        follow = P(spine=[(-4, LAT)], neck=[(18, LAT)], head=[(14, LAT)], jaw=[(4, LAT)], move={'root': (0.26, 0, 0.0)})
        follow.update({'fl.R.upper': 30, 'fl.L.upper': 25})
    else:
        # horse surges forward for the lance strike
        wind = P(hips=[(-4, LAT)], neck=[(8, LAT)], head=[(-6, LAT)], move={'root': (-0.1, 0, 0)})
        wind.update({'fl.R.upper': 20, 'fl.R.lower': 50, 'bl.R.upper': -10, 'bl.L.upper': -8})
        contact = P(hips=[(4, LAT)], spine=[(-3, LAT)], neck=[(-10, LAT)], head=[(8, LAT)], move={'root': (0.3, 0, 0.04)})
        contact.update({'fl.R.upper': 45, 'fl.L.upper': -10, 'fl.R.lower': 20, 'bl.R.upper': -25, 'bl.L.upper': 15})
        follow = P(neck=[(-6, LAT)], move={'root': (0.26, 0, 0.0)})
        follow.update({'fl.R.upper': 20, 'fl.L.upper': 10})
    rest = {}
    seq = [(0, rest), (1, blend_poses(rest, wind, .5)), (3, wind), (4, contact), (6, follow), (9, rest)]
    att = []
    for f in range(10):
        for (a, pa), (b, pb) in zip(seq, seq[1:]):
            if a <= f <= b:
                t = 0 if a == b else (f - a) / (b - a)
                att.append(add_poses(st, blend_poses(pa, pb, smooth(t))))
                break
    C['attack'] = att
    hit = P(spine=[(8, LAT)], neck=[(-14, LAT)], head=[(-10, LAT)], move={'root': (-0.06, 0, 0)})
    C['hit'] = [add_poses(st, hit), add_poses(st, blend_poses({}, hit, .45))]
    death = []
    for i in range(6):
        t = smooth(i / 5)
        pose = P(root=[(-82 * t, ROLL)], neck=[(-20 * t, LAT)], head=[(-15 * t, LAT)],
                 move={'root': (0.0, 0.35 * t * (1.0 if kind == 'hound' else 0.6), 0.03 * t)})
        legs = {}
        for leg in ('fl.R', 'fl.L', 'bl.R', 'bl.L'):
            legs[leg + '.upper'] = 25 * t * (1 if leg.startswith('fl') else -1)
            legs[leg + '.lower'] = -20 * t
        death.append(add_poses(blend_poses(st, {}, t), pose, legs))
    C['death'] = death
    crouch = P(move={'root': (0, 0, -0.1)}, scale={'root': 0.9}, neck=[(-10, LAT)])
    C['deploy'] = [add_poses(st, crouch), add_poses(st, blend_poses({}, crouch, .5)),
                   add_poses(st, P(neck=[(-12, LAT)], head=[(-10, LAT)], jaw=[(-20, LAT)], move={'root': (0, 0, 0.04)})),
                   add_poses(st, P(neck=[(-4, LAT)]))]
    return st, C


def fix_tail_keys(pose):
    return {('tail.0' if k == 'tail_0' else k): v for k, v in pose.items()}
