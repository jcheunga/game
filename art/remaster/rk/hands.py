"""Sculpted hands: fists, weapon grips, open casting hands and claws (signed-distance sculpts).

Hands are built in a local frame: origin at the wrist, +Y toward the fingertips, +Z out of the back of
the hand, and the thumb on the `thumb` side of X (-1 or +1). `place_grip` wraps a hand around a weapon
axis in the stance pose; `place_rest` sets a hand on the hand bone. Both return world-space meshes for
the caller to bind rigidly to the hand bone.
"""
import math

import numpy as np
from mathutils import Matrix, Vector

from . import sdf as F

POSES = {
    #          curl (MCP, PIP, DIP) per finger           thumb (abduct, curl)   spread
    'fist': dict(curl=(88, 100, 70), thumb=(50, 60), spread=0.0),
    'grip': dict(curl=(70, 85, 55), thumb=(55, 40), spread=0.0),
    'relaxed': dict(curl=(25, 35, 20), thumb=(25, 15), spread=0.08),
    'open': dict(curl=(8, 10, 6), thumb=(40, 5), spread=0.18),
    'claw': dict(curl=(25, 60, 55), thumb=(45, 35), spread=0.3),
    'point': dict(curl=(88, 100, 70), thumb=(50, 60), spread=0.0, point=True),
    'cup': dict(curl=(30, 30, 20), thumb=(55, 20), spread=0.05),
}


def hand_field(size=1.0, pose='fist', thumb=-1, grip_r=0.02, knuckles=1.0, claws=0.0, thin=0.0, name='Hand'):
    """Signed-distance hand in its local frame. Returns (field, grip_point_local)."""
    p = POSES[pose] if isinstance(pose, str) else pose
    s = size
    th = 1 - 0.35 * thin
    f = F.Field(name)
    tag = {'hand': 1}
    # palm: a thick rounded slab, wider at the knuckles
    f.add(F.Box(Vector((0, 0.052 * s, -0.004 * s)), (0.042 * s * th, 0.046 * s, 0.018 * s * th), r=0.014 * s), 0.0, tag)
    f.add(F.Ellipsoid(Vector((0, 0.085 * s, 0.0)), (0.05 * s * th, 0.02 * s, 0.02 * s * th)), 0.012 * s, tag)
    # wrist stub so the hand seats into the sleeve or forearm
    f.add(F.RoundCone(Vector((0, -0.03 * s, 0)), Vector((0, 0.02 * s, 0)), 0.03 * s * th, 0.034 * s * th,
                      squash=(1.25, 0.85), up=(0, 0, 1)), 0.015 * s, tag)
    # thenar pad at the thumb root
    f.add(F.Ellipsoid(Vector((-thumb * -0.028 * s, 0.03 * s, -0.016 * s)), (0.022 * s, 0.032 * s, 0.018 * s)), 0.012 * s,
          tag)
    lengths = [(0.046, 0.03, 0.024), (0.05, 0.033, 0.026), (0.047, 0.031, 0.024), (0.038, 0.025, 0.021)]
    radii = [0.0115, 0.012, 0.0112, 0.0098]
    spread = p.get('spread', 0.0)
    for i in range(4):
        # index finger nearest the thumb
        x = (-thumb) * (0.03 - i * 0.02) * s * th
        k = Vector((x, 0.095 * s - (0.004 if i in (0, 3) else 0) * s, 0.002 * s))
        d = Vector((math.sin(spread * (1.5 - i)) * -thumb * 0.7, math.cos(spread * (1.5 - i)), 0)).normalized()
        curl = p['curl']
        if p.get('point') and i == 0:
            curl = (5, 5, 0)
        pts = [k]
        ang = 0.0
        for j in range(3):
            ang += math.radians(curl[j])
            seg = lengths[i][j] * s
            # rotate the direction toward the palm (-Z) about the knuckle axis
            dj = d * math.cos(ang) + Vector((0, 0, -1)) * math.sin(ang)
            pts.append(pts[-1] + dj * seg)
        r = radii[i] * s * th
        f.add(F.Chain(pts, [r * 1.08, r, r * 0.95, r * 0.85], squash=(1.0, 0.92)), 0.006 * s, tag)
        if knuckles:
            f.add(F.Sphere(k + Vector((0, 0, 0.006 * s)), r * 1.05 * knuckles), 0.006 * s, tag)
        if claws:
            tip = pts[-1]
            dirn = (pts[-1] - pts[-2]).normalized()
            f.group = 'claw'
            f.add(F.RoundCone(tip, tip + dirn * 0.03 * s * claws, r * 0.7, 0.002 * s), 0.003 * s, tag)
            f.group = None
    # thumb: from the base of the palm, abducted forward and curled over the fingers
    ab, tc = math.radians(p['thumb'][0]), math.radians(p['thumb'][1])
    t0 = Vector(((-thumb) * -0.032 * s, 0.022 * s, -0.012 * s))
    tdir = Vector((-(-thumb) * math.cos(ab) * 0.55, math.sin(ab) * 0.9 + 0.45, -0.35)).normalized()
    t1 = t0 + tdir * 0.042 * s
    t2 = t1 + (tdir * math.cos(tc) + Vector((thumb * 0.6, 0, -0.6)).normalized() * math.sin(tc)).normalized() * 0.032 * s
    t3 = t2 + (tdir * math.cos(tc * 1.6) + Vector((thumb * 0.7, 0, -0.5)).normalized() * math.sin(tc * 1.6)).normalized() \
        * 0.026 * s
    f.add(F.Chain([t0, t1, t2, t3], [0.017 * s * th, 0.014 * s * th, 0.0125 * s * th, 0.011 * s * th]), 0.01 * s, tag)
    if claws:
        f.group = 'claw'
        f.add(F.RoundCone(t3, t3 + (t3 - t2).normalized() * 0.025 * s * claws, 0.008 * s, 0.002 * s), 0.003 * s, tag)
        f.group = None
    grip_local = Vector((0, 0.072 * s, -0.035 * s))
    return f, grip_local


def _paint_claws(obj, field):
    """Claws (ops in group 'claw') get a dark horn material, so they read against the skin."""
    import numpy as np
    claw = [p for (m, p, k, t, d), g in zip(field.ops, field.groups) if m == 'add' and g == 'claw']
    rest = [p for (m, p, k, t, d), g in zip(field.ops, field.groups) if m == 'add' and g != 'claw']
    if not claw or not rest:
        return
    from . import shaders as S
    me = obj.data
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get('co', co)
    co = co.reshape(-1, 3)
    dc = np.min([p.d(co) for p in claw], 0)
    dr = np.min([p.d(co) for p in rest], 0)
    is_claw = dc < dr - 0.0015
    me.materials.append(S.st_bone('2a221c', name='Claw horn', stain='0e0b08', crack=0.0))
    for poly in me.polygons:
        if all(is_claw[v] for v in poly.vertices):
            poly.material_index = len(me.materials) - 1


def build(field, mat, coll, M=None, h=0.0024, name='Hand'):
    obj = field.mesh(h, coll, mat, name=name, smooth=3)
    _paint_claws(obj, field)
    if M is not None:
        from . import geo
        geo.transform(obj, M)
    return obj


def grip_frame(side, axis, grip_center, wrist, grip_local, toward_tip=True):
    """World matrix placing a grip hand so its curled fingers wrap the weapon axis at grip_center."""
    A = Vector(axis).normalized()
    if not toward_tip:
        A = -A
    thumb = -1 if side == 'R' else 1
    # thumb side points along the weapon toward its business end
    X = -A if thumb < 0 else A
    n = (Vector(wrist) - Vector(grip_center))
    n = n - A * n.dot(A)
    if n.length < 1e-6:
        n = Vector((0, 0, -1)) - A * Vector((0, 0, -1)).dot(A)
    Y = -n.normalized()
    Z = X.cross(Y).normalized()
    Y = Z.cross(X).normalized()
    R = Matrix((X, Y, Z)).transposed()
    # put the local grip point on the axis
    origin = Vector(grip_center) - R @ Vector((0, grip_local.y, grip_local.z))
    M = R.to_4x4()
    M.translation = origin
    return M, thumb


def rest_frame(J, side, turn_palm=0.0):
    """World matrix for a hand hanging on its bone in the rest pose (back of the hand outward)."""
    wr, ht = J['wrist.' + side], J['hand_tip.' + side]
    Y = (ht - wr).normalized()
    out = Vector((0, -1 if side == 'R' else 1, 0))
    Z = (out - Y * out.dot(Y)).normalized()
    if turn_palm:
        Z = Matrix.Rotation(math.radians(turn_palm) * (1 if side == 'R' else -1), 3, Y) @ Z
    X = Y.cross(Z).normalized()
    R = Matrix((X, Y, Z)).transposed()
    M = R.to_4x4()
    M.translation = wr
    thumb = -1 if side == 'R' else 1
    return M, thumb
