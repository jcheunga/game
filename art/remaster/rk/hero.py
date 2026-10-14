"""Stylised heroic anatomy: proportions, skeleton and a sculpted (SDF) body.

The look follows the Crownroad roster sheet (output/units.png): about 5.4 heads tall, a broad
chest and shoulders, thick forearms, big hands and sturdy legs. The skeleton keeps the bone names and
roll conventions of `body.skeleton`, so stances, clips and the death solver work unchanged.

The body is a signed-distance sculpt (rk/sdf.py): every muscle mass is a primitive tagged with the
bones it follows, so skin weights come from the sculpt itself. `body_field` is also the base that
garments and armour are offset from (rk/garb.py).
"""
import math

import numpy as np
from mathutils import Matrix, Vector

from . import sdf as F
from .rig import Skeleton

DEFAULT = dict(
    scale=1.0,       # uniform size (1.0: about 2.05 m to the crown)
    pelvis=0.98,     # hip bone head
    chest=1.36,      # chest bone head
    neck=1.61,       # neck base
    head=0.37,       # chin to crown
    shoulder=0.29,   # shoulder joint half-width
    hip=0.115,       # hip joint half-width
    bulk=1.0,        # overall muscle girth
    chest_w=1.0,     # chest breadth
    chest_d=1.0,     # chest depth
    waist=1.0,       # waist girth
    belly=0.0,       # paunch (0..1)
    arm=1.0,         # upper-arm girth
    forearm=1.0,     # forearm girth (stylised: thick)
    leg=1.0,         # thigh girth
    calf=1.0,
    arm_len=1.0,
    leg_len=1.05,
    hand=1.0,        # hand size
    foot=1.0,
    neck_w=1.0,
    traps=1.0,
    hunch=0.0,       # forward hunch of the upper body (deg)
    spread=18.0,     # rest A-pose: upper arm from vertical (deg)
    stance=0.0,      # extra foot spread (m)
    thin=0.0,        # 0..1 emaciated (ghouls, risen)
    female=0.0,      # 0..1 narrower shoulders and waist, fuller hips
    digitigrade=False,
)


def spec(**kw):
    s = dict(DEFAULT)
    s.update(kw)
    return s


def joints(s):
    k = s['scale']
    ll, al = s['leg_len'], s['arm_len']
    fem = s['female']
    pel = s['pelvis'] * ll
    lift = pel - s['pelvis']
    chest = s['chest'] + lift
    neck = s['neck'] + lift
    hunch = math.radians(s['hunch'])

    def hz(z, base=pel):
        d = z - base
        return Vector((math.sin(hunch) * d * (d / max(0.01, neck - base)), 0, base + math.cos(hunch * .5) * d))

    J = {'root': Vector((0, 0, 0)), 'pelvis': Vector((0, 0, pel))}
    J['spine'] = hz(pel + 0.19)
    J['chest'] = hz(chest)
    J['neck'] = hz(neck)
    J['head'] = J['neck'] + Vector((0.02, 0, 0.08))
    J['head_top'] = J['head'] + Vector((0.01, 0, s['head'] + 0.02))
    sh = s['shoulder'] * (1 - 0.14 * fem) * (0.94 + 0.06 * s['chest_w'])
    spread = math.radians(s['spread'])
    for side, sy in (('R', -1), ('L', 1)):
        sp = J['chest'] + Vector((-0.01, sy * sh, 0.165))
        upper, fore = 0.33 * al, 0.30 * al
        el = sp + Vector((0.0, sy * math.sin(spread) * upper, -math.cos(spread) * upper))
        wr = el + Vector((0.045, sy * math.sin(spread * .55) * fore, -math.cos(spread * .55) * fore))
        ht = wr + Vector((0.02, sy * 0.012, -0.17 * s['hand']))
        J['shoulder.' + side] = sp
        J['clav.' + side] = J['chest'] + Vector((0.01, sy * 0.06, 0.17))
        J['elbow.' + side] = el
        J['wrist.' + side] = wr
        J['hand_tip.' + side] = ht
        hip = s['hip'] * (1 + 0.12 * fem)
        hp = J['pelvis'] + Vector((0, sy * hip, -0.06))
        thigh, shin = 0.41 * ll, 0.42 * ll
        foot_y = sy * (hip + 0.03 + s['stance'])
        kn = Vector((0.04, foot_y * .97, hp.z - thigh))
        an = Vector((-0.01, foot_y, max(0.09, kn.z - shin)))
        if s['digitigrade']:
            kn = Vector((0.1, foot_y, hp.z - thigh * .9))
            an = Vector((-0.08, foot_y, 0.2))
        J['hip.' + side] = hp
        J['knee.' + side] = kn
        J['ankle.' + side] = an
        J['toe.' + side] = Vector((an.x + 0.22 * s['foot'], foot_y, 0.035))
    if k != 1.0:
        for key in J:
            J[key] = J[key] * k
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


# ------------------------------------------------------------------ the sculpt
def _rz(deg):
    return Matrix.Rotation(math.radians(deg), 3, 'Z')


def _frame(x, y=None, z=None):
    """Rotation whose columns are the given axes (orthonormalised)."""
    x = Vector(x).normalized()
    if z is not None:
        z = Vector(z)
        y = z.cross(x).normalized()
        z = x.cross(y).normalized()
    else:
        y = Vector(y)
        z = x.cross(y).normalized()
        y = z.cross(x).normalized()
    return Matrix((x, y, z)).transposed()


def body_field(s, J, hands=False, feet=True, head=False):
    """The bare body as a Field (rest pose). Muscles are tagged with bones for skinning."""
    f = F.Field('Body')
    f.reach = 0.12   # garments and armour are offset from this sculpt
    b, th = s['bulk'], 1 - 0.42 * s['thin']
    fem = s['female']
    cw, cd, wa = s['chest_w'], s['chest_d'], s['waist']
    pel, sp, ch, nk = J['pelvis'], J['spine'], J['chest'], J['neck']
    g = b * th
    f.group = 'hips'
    # pelvis and glutes
    f.add(F.Ellipsoid(pel + Vector((0.0, 0, -0.02)), (0.15 * g, (0.19 + 0.03 * fem) * g, 0.13)), 0.06, {'hips': 1})
    for sy in (-1, 1):
        f.add(F.Ellipsoid(pel + Vector((-0.07, sy * 0.085, -0.05)), (0.085 * g, 0.09 * g, 0.1)), 0.05, {'hips': 1})
    # abdomen (paunch) and waist
    belly = s['belly']
    f.add(F.RoundCone(pel + Vector((0.01 + 0.03 * belly, 0, 0.04)), sp + Vector((0.02 + 0.04 * belly, 0, 0.06)),
                      (0.15 + 0.06 * belly) * g * wa, (0.155 + 0.05 * belly) * g * wa,
                      squash=(1.18 - 0.12 * fem, 0.98), up=(0, 0, 1)), 0.07, F.along(pel, sp, 'hips', 'spine'))
    if belly > 0:
        f.add(F.Ellipsoid(pel + Vector((0.08 + 0.06 * belly, 0, 0.12)), (0.13 * belly + 0.05, 0.16 * belly + 0.08,
                                                                       0.14 * belly + 0.06)), 0.08, {'hips': .5, 'spine': .5})
    f.group = 'torso'
    # ribcage, upper back and shoulder girdle
    f.add(F.Ellipsoid(ch + Vector((0.0, 0, 0.05)), (0.17 * g * cd, 0.22 * g * cw * (1 - .1 * fem), 0.2)), 0.07,
          F.along(sp, nk, 'spine', 'chest', 0.0, 0.5))
    f.add(F.Ellipsoid(ch + Vector((-0.025, 0, 0.16)), (0.14 * g * cd, 0.25 * g * cw * (1 - .12 * fem), 0.1)), 0.07,
          {'chest': 1})
    # pectorals / bust
    for sy in (-1, 1):
        sd = 'R' if sy < 0 else 'L'
        R = _frame((1, 0, 0), (0, 1, 0)) @ Matrix.Rotation(math.radians(sy * 12), 3, 'X')
        f.add(F.Ellipsoid(ch + Vector((0.1 * cd * g, sy * 0.095 * cw * g, 0.1)),
                          (0.075 * g, 0.11 * g * cw, 0.08), R), 0.05, {'chest': 1})
        if fem:
            f.add(F.Sphere(ch + Vector((0.13 * g, sy * 0.085, 0.05)), 0.065 * fem), 0.05, {'chest': 1})
        # lats: wedge from armpit to waist gives the V-taper
        f.add(F.RoundCone(ch + Vector((-0.04, sy * 0.17 * cw * g, 0.1)), sp + Vector((-0.03, sy * 0.13 * g * wa, 0.0)),
                          0.085 * g, 0.06 * g, squash=(1.0, 0.6), up=(0, sy, 0)), 0.06,
              F.along(ch, sp, 'chest', 'spine'))
        # trapezius: the slope from the neck to the shoulder
        shj = J['shoulder.' + sd]
        f.add(F.RoundCone(nk + Vector((-0.05, sy * 0.02, 0.05)), Vector((shj.x - 0.03, shj.y * 0.72, shj.z + 0.045)),
                          0.085 * s['traps'] * g, 0.06 * g, squash=(1.0, 0.8)), 0.07, {'chest': .8, 'neck': .2})
    # neck
    f.group = 'neck'
    f.add(F.RoundCone(nk + Vector((-0.01, 0, -0.04)), J['head'] + Vector((0.0, 0, 0.05)), 0.085 * s['neck_w'] * g,
                      0.072 * s['neck_w'] * g), 0.05, F.along(nk, J['head'], 'chest', 'neck', 0.0, 0.6))
    for side, sy in (('R', -1), ('L', 1)):
        f.group = 'arm.' + side
        _arm(f, s, J, side, sy, g, hands)
        f.group = 'leg.' + side
        _leg(f, s, J, side, sy, b * (1 - 0.3 * s['thin']), feet)
    f.group = 'head'
    if head:
        f.add(F.Ellipsoid(J['head'] + Vector((0.03, 0, 0.17)), (0.15, 0.13, 0.19)), 0.05, {'head': 1})
    return f


def _arm(f, s, J, side, sy, g, hands):
    sh, el, wr, ht = J['shoulder.' + side], J['elbow.' + side], J['wrist.' + side], J['hand_tip.' + side]
    a, fa = s['arm'] * g, s['forearm'] * g
    ua = el - sh
    # deltoid cap: blends chest and upper arm
    f.add(F.Ellipsoid(sh + Vector((0.0, sy * 0.03, -0.035)), (0.1 * a, 0.088 * a, 0.105 * a),
                      _frame(Vector((1, 0, 0)), None, -ua)), 0.05,
          {'shoulder.' + side: .25, 'upper_arm.' + side: .6, 'chest': .15})
    # upper arm, biceps and triceps
    f.add(F.RoundCone(sh, el, 0.085 * a, 0.068 * a), 0.04, {'upper_arm.' + side: 1})
    f.add(F.Ellipsoid(sh.lerp(el, .55) + Vector((0.045 * a, 0, 0)), (0.05 * a, 0.055 * a, 0.1 * a),
                      _frame(Vector((1, 0, 0)), None, -ua)), 0.035, {'upper_arm.' + side: 1})
    f.add(F.Ellipsoid(sh.lerp(el, .45) + Vector((-0.035 * a, 0, 0)), (0.05 * a, 0.055 * a, 0.12 * a),
                      _frame(Vector((1, 0, 0)), None, -ua)), 0.035, {'upper_arm.' + side: 1})
    # elbow
    f.add(F.Sphere(el + Vector((-0.012, 0, 0)), 0.064 * a), 0.03, {'upper_arm.' + side: .5, 'forearm.' + side: .5})
    # forearm: heavy just below the elbow, slim wrist (stylised Popeye taper)
    fv = wr - el
    f.add(F.RoundCone(el, wr, 0.07 * fa, 0.05 * fa), 0.03, F.along(el, wr, 'forearm.' + side, 'forearm.' + side))
    f.add(F.Ellipsoid(el.lerp(wr, .3) + Vector((0.012, 0, 0)), (0.075 * fa, 0.07 * fa, 0.12 * fa),
                      _frame(Vector((1, 0, 0)), None, -fv)), 0.04, {'forearm.' + side: 1})
    if hands:
        f.group = 'hand.' + side
        f.add(F.Ellipsoid(wr.lerp(ht, .45), (0.055 * s['hand'], 0.035 * s['hand'], 0.085 * s['hand'])), 0.03,
              {'hand.' + side: 1})


def _leg(f, s, J, side, sy, g, feet):
    hp, kn, an, toe = J['hip.' + side], J['knee.' + side], J['ankle.' + side], J['toe.' + side]
    L, C = s['leg'] * g, s['calf'] * g
    tv = kn - hp
    f.add(F.RoundCone(hp + Vector((0, 0, 0.05)), kn, 0.128 * L, 0.082 * L), 0.05,
          F.along(hp, kn, 'thigh.' + side, 'thigh.' + side))
    # quadriceps and outer thigh sweep
    f.add(F.Ellipsoid(hp.lerp(kn, .45) + Vector((0.045 * L, sy * 0.012, 0)), (0.08 * L, 0.085 * L, 0.17),
                      _frame(Vector((1, 0, 0)), None, -tv)), 0.05, {'thigh.' + side: 1})
    f.add(F.Ellipsoid(hp.lerp(kn, .3) + Vector((0.0, sy * 0.04 * L, 0)), (0.08 * L, 0.07 * L, 0.15),
                      _frame(Vector((1, 0, 0)), None, -tv)), 0.05, {'thigh.' + side: .8, 'hips': .2})
    f.add(F.Sphere(kn + Vector((0.025, 0, 0)), 0.068 * L), 0.035, {'thigh.' + side: .5, 'shin.' + side: .5})
    sv = an - kn
    f.add(F.RoundCone(kn, an, 0.07 * C, 0.05 * C), 0.04, {'shin.' + side: 1})
    f.add(F.Ellipsoid(kn.lerp(an, .3) + Vector((-0.03 * C, 0, 0)), (0.065 * C, 0.068 * C, 0.13),
                      _frame(Vector((1, 0, 0)), None, -sv)), 0.04, {'shin.' + side: 1})
    if feet:
        f.group = 'foot.' + side
        fs = s['foot']
        f.add(F.RoundCone(an + Vector((-0.03, 0, -0.03)), toe + Vector((-0.03, 0, 0.0)), 0.055 * fs, 0.045 * fs,
                          squash=(1.1, 0.75)), 0.04, F.along(an, toe, 'foot.' + side, 'foot.' + side))


def build_body(name, s, J, coll, mat, h=0.006, hands=False, feet=True, extra=None):
    """Mesh the body sculpt; returns (object, field). extra(field) may add or cut primitives."""
    f = body_field(s, J, hands=hands, feet=feet)
    if extra:
        extra(f)
    obj = f.mesh(h, coll, mat, name=name)
    obj['sdf_body'] = True
    return obj, f


def skin_from_field(obj, arm, field, sigma=0.03, smooth=3):
    """Write the sculpt's own bone weights onto obj (rest pose) and add the armature modifier."""
    from .rig import _armature_mod
    V = F.verts(obj)
    W = field.weights(V, sigma)
    F.write_weights(obj, W, F.quads(obj) if _all_quads(obj) else None, smooth=smooth)
    _armature_mod(obj, arm)
    return obj


def _all_quads(obj):
    return len(obj.data.loops) == 4 * len(obj.data.polygons)
