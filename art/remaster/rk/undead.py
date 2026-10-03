"""Skeletal bodies and undead dressing (ribcages, exposed bones, rags)."""
import math

from mathutils import Vector

from . import geo
from .heads import catmull


def _v(*a):
    return Vector(a)


def _bone_tube(name, a, b, r, mat, coll, knob=1.6):
    """Long bone: shaft with knobbly ends."""
    out = [geo.tube(name, [a, a.lerp(b, .5), b], [r * 1.1, r * 0.8, r * 1.0], mat, coll, sides=8)]
    out.append(geo.sphere(name + ' knob', r * knob, a, mat, coll, 10, 6))
    out.append(geo.sphere(name + ' knob', r * knob * .9, b, mat, coll, 10, 6))
    return out


def skeleton_body(ch, M, bone_mat=None, ribs=5, thick=1.0, glow_core=None):
    """Full skeleton bound rigidly to the humanoid rig. Returns nothing (adds parts)."""
    J, coll = ch.J, ch.coll
    bm = bone_mat or M['bone']
    t = thick
    # spine column
    verts = []
    a, b = J['pelvis'] + _v(-0.04, 0, 0.02), J['neck']
    for i in range(9):
        p = a.lerp(b, i / 8) + _v(-0.04 * math.sin(i / 8 * math.pi), 0, 0)
        bone = 'hips' if i < 2 else 'spine' if i < 5 else 'chest'
        ch.parts.append((geo.sphere('Vertebra', 0.034 * t, p, bm, coll, 10, 6, scale=(1, 1.15, .75)), bone))
        ch.parts.append((geo.box('Spinous process', (0.05 * t, 0.02, 0.025), p + _v(-0.04, 0, 0), bm, coll, bevel=.008),
                         bone))
    # ribcage
    c = J['chest'] + _v(0.0, 0, -0.02)
    for i in range(ribs):
        z = c.z + 0.12 - i * 0.065
        w = (0.16 + 0.03 * math.sin((i + 1) / (ribs + 1) * math.pi)) * t
        for sy in (-1, 1):
            pts = [_v(-0.07, sy * 0.03, z + 0.02), _v(-0.06, sy * w * .9, z + 0.01), _v(0.06, sy * w, z - 0.03),
                   _v(0.15, sy * w * .55, z - 0.06), _v(0.17, sy * 0.03, z - 0.05)]
            pts = catmull([p + _v(c.x, 0, 0) for p in pts], 3)
            ch.parts.append((geo.tube('Rib', pts, 0.016 * t, bm, coll, sides=6, flatten=.6), 'chest'))
    ch.parts.append((geo.tube('Sternum', [c + _v(0.17, 0, 0.12), c + _v(0.16, 0, -0.17)], [0.025 * t, 0.018 * t], bm, coll,
                              sides=6, flatten=.5), 'chest'))
    for sy in (-1, 1):
        ch.parts.append((geo.tube('Clavicle', [J['neck'] + _v(0.06, sy * 0.04, -0.06), J['shoulder.' + ('R' if sy < 0 else 'L')] +
                                               _v(0.02, 0, 0.04)], 0.016 * t, bm, coll, sides=6), 'chest'))
    if glow_core is not None:
        ch.parts.append((geo.sphere('Soul core', 0.06, c + _v(0.03, 0, -0.02), glow_core, coll, 12, 8), 'chest'))
    # pelvis
    p = J['pelvis']
    pel = geo.quadsphere('Pelvis', 1.0, (0, 0, 0), bm, coll, level=3, scale=(0.11 * t, 0.17 * t, 0.09))
    geo.sculpt(pel, _v(0.11, 0, 0.0), 0.08, _v(-0.06, 0, 0))
    geo.place(pel, p + _v(0.0, 0, -0.02))
    ch.parts.append((pel, 'hips'))
    for side, sy in (('R', -1), ('L', 1)):
        sp, el, wr, ht = J['shoulder.' + side], J['elbow.' + side], J['wrist.' + side], J['hand_tip.' + side]
        ch.parts.append((geo.sphere('Scapula', 1, sp + _v(-0.07, -sy * 0.02, -0.03), bm, coll, 10, 6,
                                    scale=(0.03, 0.08, 0.1)), 'shoulder.' + side))
        for o in _bone_tube('Humerus', sp, el, 0.022 * t, bm, coll):
            ch.parts.append((o, 'upper_arm.' + side))
        for off in (-0.015, 0.015):
            ch.parts.append((geo.tube('Forearm bone', [el + _v(0, off, 0), wr + _v(0, off * .7, 0)], 0.014 * t, bm, coll,
                                      sides=6), 'forearm.' + side))
        ch.parts.append((geo.sphere('Wrist', 0.028 * t, wr, bm, coll, 8, 6), 'hand.' + side))
        d = (ht - wr).normalized()
        for k in range(4):
            yy = (k - 1.5) * 0.017
            base = wr + d * 0.04 + _v(0.01, yy, 0)
            ch.parts.append((geo.tube('Finger', catmull([base, base + d * 0.05 + _v(0.015, 0, 0), base + d * 0.09 + _v(0.03, 0, -0.01)],
                                                        2), 0.009 * t, bm, coll, sides=5), 'hand.' + side))
        ch.parts.append((geo.tube('Thumb', [wr + d * 0.03 + _v(0.02, -sy * .02, 0), wr + d * 0.07 + _v(0.05, -sy * .02, 0)],
                                  0.01 * t, bm, coll, sides=5), 'hand.' + side))
        hp, kn, an, toe = J['hip.' + side], J['knee.' + side], J['ankle.' + side], J['toe.' + side]
        for o in _bone_tube('Femur', hp, kn, 0.027 * t, bm, coll, 1.4):
            ch.parts.append((o, 'thigh.' + side))
        ch.parts.append((geo.sphere('Kneecap', 0.03 * t, kn + _v(0.035, 0, 0), bm, coll, 8, 6), 'shin.' + side))
        ch.parts.append((geo.tube('Tibia', [kn, an], [0.024 * t, 0.018 * t], bm, coll, sides=6), 'shin.' + side))
        ch.parts.append((geo.tube('Fibula', [kn + _v(-0.02, sy * 0.02, 0), an + _v(-0.02, sy * 0.02, 0)], 0.01 * t, bm, coll,
                                  sides=5), 'shin.' + side))
        foot = geo.quadsphere('Foot bones', 1.0, (0, 0, 0), bm, coll, level=2, scale=(0.12, 0.045, 0.035))
        geo.place(foot, an.lerp(toe, .55) + _v(0, 0, -0.04))
        ch.parts.append((foot, 'foot.' + side))
        for k in range(3):
            ch.parts.append((geo.tube('Toe', [toe + _v(-0.03, (k - 1) * 0.02, -0.01), toe + _v(0.04, (k - 1) * 0.022, -0.02)],
                                      0.009, bm, coll, sides=5), 'foot.' + side))
    return ch


def rags(ch, mat, coll, z_top=None, z_bottom=None, seed=3, tatter=1.0):
    """Tattered loincloth / tabard scraps hanging from the hips."""
    from .armor import skirt
    J = ch.J
    zt = J['pelvis'].z + 0.04 if z_top is None else z_top
    zb = J['knee.R'].z + 0.08 if z_bottom is None else z_bottom
    parts = skirt('Grave rags', J, mat, coll, zt, zb, r_top=(0.15, 0.19), r_bottom=(0.21, 0.25), folds=6,
                  fold_depth=0.02, hem_wave=0.08 * tatter, seed=seed, split=0.65)
    ch.parts.extend(parts)
    return parts
