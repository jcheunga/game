"""Helmets and headwear grown from the head sculpt (rk/faces.py), so every piece fits the stylised skull.

Builders take the head centre C and unit u (as faces) and return mesh objects for the head bone.
Head-local coordinates: x forward, y left, z up, in units of u; brows at about z 0.2, eyes at 0, chin at
-1.08, crown at 1.04, ears at y +-0.8.
"""
import math

import numpy as np
from mathutils import Matrix, Vector

from . import faces
from . import sdf as F


def _loc(C, u):
    c = np.asarray(tuple(C))
    return lambda P: (P - c) / u


def _P(C, u):
    return lambda x, y, z: C + Vector((x * u, y * u, z * u))


def _box(C, u, r=2.0):
    c = np.asarray(tuple(C))
    return c - r * u, c + r * u


def _head():
    return faces.LAST['field']


def _mesh(f, h, coll, mat, name, smooth=3):
    return f.mesh(h, coll, mat, name=name, smooth=smooth)


def _shell(head, t, th):
    """Head offset by t (to its middle), thickness th."""
    return lambda P: np.abs(head.eval(P) - t) - th * 0.5


def _trim_band(head, t, th, keep, w, extra=0.004):
    return (lambda P: np.abs(head.eval(P) - t) - th * 0.5 - extra,
            lambda P: np.abs(keep(P) + w * 0.5) - w * 0.5)


def _plate(C, u, coll, name, shell, keep, mat, trim_mat=None, trim_w=None, k=None, h=None, disp=None, trim_t=0.004):
    lo, hi = _box(C, u, 2.2)
    out = []
    f = F.Field(name)
    f.add(F.Fn(shell, lo, hi), 0.0)
    f.keep(F.Fn(keep, lo, hi), (k if k is not None else 0.02 * u))
    f.disp = disp
    out.append(_mesh(f, h or u * 0.03, coll, mat, name))
    if trim_mat is not None:
        w = trim_w or 0.1 * u
        tf = F.Field(name + ' trim')
        tf.add(F.Fn(lambda P: shell(P) - trim_t, lo, hi), 0.0)
        tf.keep(F.Fn(lambda P: np.abs(keep(P) + w * 0.5) - w * 0.5, lo, hi), 0.01 * u)
        out.append(_mesh(tf, (h or u * 0.03) * 0.8, coll, trim_mat, name + ' trim', 2))
    return out


def rivets(points, r, mat, coll):
    f = F.Field('Rivets')
    for p in points:
        f.add(F.Sphere(p, r), 0.0)
    return [f.mesh(r * 0.4, coll, mat, name='Rivets', smooth=1)]


def _on_mesh(obj, origin, d, far):
    """Outermost point of a mesh along a ray from origin (None if the ray misses it)."""
    from mathutils.bvhtree import BVHTree
    me = obj.data
    tree = BVHTree.FromPolygons([obj.matrix_world @ v.co for v in me.vertices], [p.vertices[:] for p in me.polygons])
    d = Vector(tuple(d)).normalized()
    hit = tree.ray_cast(Vector(tuple(origin)) + d * far, -d, far)
    return hit[0] if hit[0] is not None else None


def _surface(fn, origin, d, hi=0.5, steps=64):
    """First inside-to-outside crossing of fn along the ray (fn negative inside)."""
    o = np.asarray(tuple(origin))
    d = np.asarray(tuple(d)) / np.linalg.norm(tuple(d))
    ts = np.linspace(0.0, hi, steps)
    vals = fn(o[None, :] + ts[:, None] * d[None, :])
    idx = np.nonzero((vals[:-1] < 0) & (vals[1:] >= 0))[0]
    if len(idx) == 0:
        return None
    a, b = ts[idx[0]], ts[idx[0] + 1]
    for _ in range(24):
        m = (a + b) / 2
        if fn((o + d * m)[None, :])[0] < 0:
            a = m
        else:
            b = m
    return Vector(tuple(o + d * a))


# ------------------------------------------------------------------ steel helmets
def burgonet(C, u, M, coll, cheeks=True, peak=True, comb=True, neck=True, t=0.16, mat='dark_steel', trim='trim'):
    """Open-faced helmet: a rounded skull with a low comb, a peak over the brows, hinged cheek guards and
    a flared neck guard (the roster sheet's spearman)."""
    head = _head()
    loc = _loc(C, u)
    P = _P(C, u)
    th = 0.09 * u
    tt = t * u
    out = []

    def skull_keep(P_):
        q = loc(P_)
        # brow line in front, down over the temples, low at the nape
        z0 = 0.3 - 0.75 * np.clip(-(q[:, 0] - 0.55) / 1.3, 0, 1)
        return (z0 - q[:, 2]) * u
    shell = _shell(head, tt, th)
    out += _plate(C, u, coll, 'Helm skull', shell, skull_keep, M[mat], M[trim], 0.09 * u)
    if comb:
        f = F.Field('Helm comb')
        f.add(F.Chain([P(0.55, 0, 0.9 + t), P(0.0, 0, 1.2 + t), P(-0.6, 0, 0.95 + t)], [0.06 * u, 0.07 * u, 0.05 * u],
                      squash=(0.45, 1.0), up=(0, 1, 0)), 0.04 * u)
        f.cut(F.Fn(lambda P_: -(head.eval(P_) - tt - th * 0.3), *_box(C, u)), 0.01 * u)
        out.append(_mesh(f, u * 0.025, coll, M[mat], 'Helm comb'))
    if peak:
        def peak_d(P_):
            q = loc(P_)
            return (np.linalg.norm((q - [0.25, 0, 0.3]) / [1.05, 0.98, 0.5], axis=1) - 1.0) * u * 0.5

        def peak_keep(P_):
            q = loc(P_)
            return np.maximum.reduce([(0.18 - q[:, 2]) * u, (q[:, 2] - 0.42) * u, (0.55 - q[:, 0]) * u])
        out += _plate(C, u, coll, 'Helm peak', lambda P_: np.abs(peak_d(P_)) - 0.035 * u, peak_keep, M[mat], M[trim],
                      0.06 * u)
    if cheeks:
        cheek_t = tt + 0.06 * u

        def cheek_keep(P_):
            q = loc(P_)
            return np.maximum.reduce([(0.42 - np.abs(q[:, 1])) * u, (q[:, 2] - 0.25) * u, (-0.95 - q[:, 2]) * u,
                                      (q[:, 0] - 0.62 + 0.25 * np.clip(-q[:, 2] - 0.4, 0, 1)) * u, (-0.25 - q[:, 0]) * u])
        out += _plate(C, u, coll, 'Cheek guard', _shell(head, cheek_t, th), cheek_keep, M[mat], M[trim], 0.07 * u)
    if neck:
        def neck_d(P_):
            q = loc(P_)
            return (np.linalg.norm((q - [-0.2, 0, -0.1]) / [1.12, 1.02, 1.1], axis=1) - 1.0) * u * 0.9

        def neck_keep(P_):
            q = loc(P_)
            return np.maximum.reduce([(q[:, 0] + 0.15) * u, (q[:, 2] + 0.1) * u, (-0.65 - q[:, 2]) * u,
                                      (np.abs(q[:, 1]) - 0.9) * u])
        out += _plate(C, u, coll, 'Neck guard', lambda P_: np.abs(neck_d(P_)) - 0.04 * u, neck_keep, M[mat], M[trim],
                      0.06 * u)
    # rivets along the brow band, seated on the skull plate itself
    pts = []
    for a in (-60, -30, 0, 30, 60):
        d = Vector((math.cos(math.radians(a)), math.sin(math.radians(a)), 0.28)).normalized()
        p = _on_mesh(out[0], C, d, 3.0 * u)
        if p is not None:
            pts.append(p - d * 0.01 * u)
    if pts:
        out += rivets(pts, 0.045 * u, M[trim], coll)
    return out


def kettle(C, u, M, coll, brim=1.0, t=0.16, mat='dark_steel', trim='trim'):
    """Kettle hat: rounded skull and a broad down-turned brim."""
    head = _head()
    loc = _loc(C, u)
    th = 0.09 * u
    tt = t * u
    out = []

    def skull_keep(P_):
        q = loc(P_)
        return (0.32 - q[:, 2]) * u
    out += _plate(C, u, coll, 'Kettle skull', _shell(head, tt, th), skull_keep, M[mat], M[trim], 0.08 * u)

    def brim_d(P_):
        q = loc(P_)
        r = np.hypot((q[:, 0] + 0.05) / 1.0, q[:, 1] / 0.9)
        zc = 0.33 - 0.25 * np.clip(r - 0.95, 0, 1) * brim
        return np.maximum((np.abs(q[:, 2] - zc) - 0.035) * u, (r - 0.95 - 0.55 * brim) * u)
    lo, hi = _box(C, u, 2.4)
    f = F.Field('Kettle brim')
    f.add(F.Fn(brim_d, lo, hi), 0.0)
    f.cut(F.Fn(lambda P_: head.eval(P_) - tt * 0.6, lo, hi), 0.01 * u)
    out.append(_mesh(f, u * 0.025, coll, M[mat], 'Kettle brim'))
    tf = F.Field('Kettle brim edge')
    tf.add(F.Fn(lambda P_: np.maximum(brim_d(P_) - 0.012 * u, -(np.hypot((loc(P_)[:, 0] + 0.05), loc(P_)[:, 1] / 0.9)
                                                                - (0.9 + 0.55 * brim)) * u), lo, hi), 0.0)
    out.append(_mesh(tf, u * 0.02, coll, M[trim], 'Kettle brim edge', 2))
    return out


def great_helm(C, u, M, coll, t=0.22, mat='dark_steel', trim='trim', cross=True, crest=None, eyes=None):
    """Enclosed barrel helm with an eye slit, breaths and a brass cross (eyes: a glow material seen
    through the slit)."""
    head = _head()
    loc = _loc(C, u)
    P = _P(C, u)
    out = []
    lo, hi = _box(C, u, 2.4)

    def barrel(P_):
        q = loc(P_)
        r = np.hypot((q[:, 0] - 0.12) / 1.12, q[:, 1] / 0.98)
        side = (r - 1.0) * u
        top = (q[:, 2] - 1.08 + 0.3 * r ** 2) * u
        bot = (-1.25 - q[:, 2]) * u
        return np.maximum.reduce([side, top, bot])
    f = F.Field('Great helm')
    f.add(F.Fn(lambda P_: np.abs(barrel(P_) + 0.05 * u) - 0.05 * u, lo, hi), 0.0)
    f.cut(F.Box(P(1.0, 0, 0.05), (0.5 * u, 0.62 * u, 0.075 * u), r=0.02 * u), 0.01 * u)
    for i in range(5):
        for sy in (-1, 1):
            f.cut(F.Sphere(P(1.05, sy * (0.18 + 0.1 * i), -0.42 - 0.08 * i), 0.035 * u), 0.004 * u)
    out.append(_mesh(f, u * 0.025, coll, M[mat], 'Great helm'))
    # a dark liner, so the slit reads as shadow rather than showing the face
    dk = F.Field('Helm liner')
    dk.add(F.Fn(lambda P_: barrel(P_) + 0.12 * u, lo, hi), 0.0)
    out.append(_mesh(dk, u * 0.04, coll, M.get('dark', M['leather_dark']), 'Helm liner', 1))
    if eyes is not None:
        g = F.Field('Helm eyes')
        for sy in (-1, 1):
            g.add(F.Ellipsoid(P(0.9, sy * 0.3, 0.05), (0.08 * u, 0.12 * u, 0.05 * u)), 0.0)
        out.append(_mesh(g, u * 0.015, coll, eyes, 'Helm eyes', 1))
    if cross:
        g = F.Field('Helm cross')
        g.add(F.Box(P(1.06, 0, -0.35), (0.1 * u, 0.08 * u, 0.7 * u), r=0.02 * u), 0.0)
        g.add(F.Box(P(1.03, 0, 0.17), (0.12 * u, 0.7 * u, 0.06 * u), r=0.02 * u), 0.0)
        g.keep(F.Fn(lambda P_: np.abs(barrel(P_) + 0.02 * u) - 0.04 * u, lo, hi), 0.01 * u)
        g.cut(F.Box(P(1.0, 0, 0.05), (0.5 * u, 0.62 * u, 0.06 * u), r=0.02 * u), 0.01 * u)
        out.append(_mesh(g, u * 0.02, coll, M[trim], 'Helm cross', 2))
    rim = F.Field('Helm rim')
    rim.add(F.Fn(lambda P_: np.maximum(np.abs(barrel(P_) + 0.02 * u) - 0.045 * u,
                                       np.abs(loc(P_)[:, 2] + 1.17) * u - 0.07 * u), lo, hi), 0.0)
    out.append(_mesh(rim, u * 0.02, coll, M[trim], 'Helm rim', 2))
    if crest is not None:
        c = F.Field('Crest')
        c.add(F.Chain([P(0.35, 0, 1.0), P(-0.1, 0, 1.6), P(-0.9, 0, 1.75), P(-1.7, 0, 1.2), P(-2.1, 0, 0.4)],
                      [0.16 * u, 0.3 * u, 0.3 * u, 0.2 * u, 0.05 * u], squash=(0.55, 1.0), up=(0, 1, 0)), 0.08 * u)
        c.disp = lambda P_: F.noise3(P_ * np.array([1.0, 1.0, 3.0]), 40.0, 3, 1) * 0.006
        out.append(_mesh(c, u * 0.025, coll, crest, 'Crest'))
    return out


def armet(C, u, M, coll, t=0.17, mat='steel', trim='trim', plume=None):
    """Close helm with a pointed visor and two eye slits (the cavalry rider)."""
    head = _head()
    loc = _loc(C, u)
    P = _P(C, u)
    out = []
    lo, hi = _box(C, u, 2.4)
    tt, th = t * u, 0.09 * u
    skull = _shell(head, tt, th)

    def keep(P_):
        q = loc(P_)
        return (-0.75 - q[:, 2] + 0.25 * np.clip(-q[:, 0], 0, 1)) * u * -1
    out += _plate(C, u, coll, 'Armet', skull, lambda P_: (-(loc(P_)[:, 2] + 0.95)) * u, M[mat])

    def visor_d(P_):
        q = loc(P_)
        return (np.linalg.norm((q - [0.35, 0, -0.25]) / [0.95, 0.92, 0.95], axis=1) - 1.0) * u * 0.6
    f = F.Field('Visor')
    f.add(F.Fn(lambda P_: np.abs(visor_d(P_)) - 0.04 * u, lo, hi), 0.0)
    f.keep(F.Fn(lambda P_: np.maximum((0.25 - loc(P_)[:, 0]) * u, (loc(P_)[:, 2] - 0.3) * u), lo, hi), 0.02 * u)
    f.add(F.RoundCone(P(1.1, 0, -0.1), P(1.45, 0, -0.3), 0.18 * u, 0.04 * u, squash=(1.0, 0.7)), 0.08 * u)
    for sy in (-1, 1):
        f.cut(F.Box(P(1.0, sy * 0.3, 0.08), (0.5 * u, 0.2 * u, 0.04 * u), r=0.015 * u), 0.008 * u)
    for i in range(4):
        f.cut(F.Box(P(1.15, -0.42, -0.4 - i * 0.12), (0.4 * u, 0.1 * u, 0.025 * u), r=0.01 * u), 0.004 * u)
    out.append(_mesh(f, u * 0.025, coll, M[mat], 'Visor'))
    r = F.Field('Armet rim')
    r.add(F.Fn(lambda P_: np.maximum(np.abs(head.eval(P_) - tt) - th * 0.5 - 0.005 * u,
                                     np.abs(loc(P_)[:, 2] + 0.92) * u - 0.05 * u), lo, hi), 0.0)
    out.append(_mesh(r, u * 0.02, coll, M[trim], 'Armet rim', 2))
    if plume is not None:
        c = F.Field('Plume')
        c.add(F.Chain([P(-0.3, 0, 1.05), P(-0.6, 0, 1.4), P(-1.2, 0, 1.3), P(-1.6, 0, 0.9)],
                      [0.1 * u, 0.16 * u, 0.12 * u, 0.04 * u], squash=(0.7, 1.0), up=(0, 1, 0)), 0.05 * u)
        c.disp = lambda P_: F.noise3(P_, 50.0, 3, 1) * 0.005
        out.append(_mesh(c, u * 0.025, coll, plume, 'Plume'))
    return out


def skull_cap(C, u, M, coll, t=0.12, mat='dark_steel', trim='trim', nasal=True):
    """A simple round cap (nasal helm) for infantry and undead."""
    head = _head()
    loc = _loc(C, u)
    P = _P(C, u)
    out = _plate(C, u, coll, 'Cap', _shell(head, t * u, 0.08 * u), lambda P_: (0.25 - loc(P_)[:, 2]) * u, M[mat],
                 M[trim], 0.09 * u)
    if nasal:
        f = F.Field('Nasal')
        f.add(F.RoundCone(P(1.02, 0, 0.35), P(1.18, 0, -0.18), 0.06 * u, 0.045 * u, squash=(0.45, 1.0), up=(1, 0, 0)),
              0.0)
        out.append(_mesh(f, u * 0.02, coll, M[mat], 'Nasal'))
    return out


# ------------------------------------------------------------------ cloth headwear
def hood(C, u, mat, coll, t=0.14, cowl=1.0, point=0.6, opening=1.0, drape=1.0, seed=2, lining=None):
    """Cloth hood with a face opening, a soft point at the back and a cowl onto the shoulders."""
    head = _head()
    loc = _loc(C, u)
    P = _P(C, u)
    lo, hi = _box(C, u, 3.2)
    lo = lo - np.array([0, 0, 1.5 * u])
    f = F.Field('Hood')
    base = F.Field('Hood volume')
    base.reach = 0.2
    base.add(F.Fn(lambda P_: head.eval(P_) - t * u, *_box(C, u, 2.4)), 0.0)
    base.add(F.RoundCone(P(-0.3, 0, 0.6), P(-0.95 - 0.3 * point, 0, 0.3 + 0.6 * point), 0.75 * u, 0.12 * u), 0.3 * u)
    base.add(F.RoundCone(P(-0.05, 0, -0.6), P(-0.05, 0, -1.35), 0.95 * u, 1.35 * u * cowl, squash=(0.85, 1.0),
                         up=(1, 0, 0)), 0.35 * u)
    wob = lambda P_: F.noise3(P_, 10.0, seed, 2) * 0.008 * drape
    f.add(F.Fn(lambda P_: np.abs(base.eval(P_) + wob(P_)) - 0.05 * u, lo, hi), 0.0)

    def face_open(P_):
        q = loc(P_)
        e = np.linalg.norm((q - [1.1, 0, -0.25]) / [0.9, 0.62 * opening, 0.95 * opening], axis=1) - 1.0
        return e * u
    f.cut(F.Fn(face_open, lo, hi), 0.06 * u)
    f.cut(F.Fn(lambda P_: (loc(P_)[:, 2] + 1.45) * u, lo, hi), 0.03 * u)
    return [_mesh(f, u * 0.035, coll, mat, 'Hood')]


def bandana(C, u, mat, coll, t=0.07, knot=True, tails=True, seed=4):
    """A cloth wrap over the crown tied at the back (the roster sheet's peasant)."""
    head = _head()
    loc = _loc(C, u)
    P = _P(C, u)
    lo, hi = _box(C, u, 2.4)
    f = F.Field('Bandana')
    f.add(F.Fn(lambda P_: head.eval(P_) - t * u, lo, hi), 0.0)

    def keep(P_):
        q = loc(P_)
        z0 = 0.42 - 0.6 * np.clip(-(q[:, 0] - 0.5) / 1.2, 0, 1)
        return (z0 - q[:, 2]) * u
    f.keep(F.Fn(keep, lo, hi), 0.03 * u)
    f.add(F.Torus(P(0.0, 0, 0.35), 0.9 * u, 0.07 * u, Matrix.Rotation(math.radians(-22), 3, 'Y'), squash=1.3), 0.05 * u)
    if knot:
        f.add(F.Ellipsoid(P(-0.92, 0, 0.12), (0.14 * u, 0.2 * u, 0.13 * u)), 0.06 * u)
    if tails:
        for sy in (-1, 1):
            f.add(F.RoundCone(P(-0.95, sy * 0.08, 0.05), P(-1.25, sy * 0.25, -0.5), 0.1 * u, 0.06 * u,
                              squash=(0.4, 1.0), up=(0, 1, 0)), 0.05 * u)
    f.disp = lambda P_: F.noise3(P_, 14.0, seed, 2) * 0.004
    return [_mesh(f, u * 0.025, coll, mat, 'Bandana')]


def crown(C, u, mat, coll, points=5, height=0.45, z=0.62, radius=0.98, gems=None, spikes=False, tilt=0.0):
    P = _P(C, u)
    f = F.Field('Crown')
    R = Matrix.Rotation(math.radians(tilt), 3, 'X')
    base = P(-0.08, 0, z)
    f.add(F.Torus(base, radius * u, 0.07 * u, R, squash=1.8), 0.0)
    for i in range(points):
        a = i / points * math.tau
        d = Vector((math.cos(a) * radius, math.sin(a) * radius * 0.9, 0))
        p0 = base + R @ (d * u)
        tip = p0 + R @ Vector((d.x * 0.15, d.y * 0.15, height)) * u
        if spikes:
            f.add(F.RoundCone(p0, tip, 0.1 * u, 0.012 * u), 0.03 * u)
        else:
            f.add(F.RoundCone(p0, tip, 0.13 * u, 0.04 * u, squash=(0.5, 1.0),
                              up=(math.cos(a), math.sin(a), 0)), 0.04 * u)
            f.add(F.Sphere(tip, 0.06 * u), 0.02 * u)
    out = [f.mesh(u * 0.02, coll, mat, name='Crown', smooth=2)]
    if gems is not None:
        g = F.Field('Crown gems')
        for i in range(points):
            a = (i + 0.5) / points * math.tau
            d = Vector((math.cos(a) * (radius + 0.07), math.sin(a) * (radius + 0.07) * 0.9, 0))
            g.add(F.Ellipsoid(base + R @ (d * u), (0.06 * u, 0.06 * u, 0.08 * u)), 0.0)
        out.append(g.mesh(u * 0.015, coll, gems, name='Crown gems', smooth=1))
    return out


def mitre(C, u, M, coll, height=1.9, mat='linen', trim='gold'):
    """A tall two-peaked bishop's mitre with gold orphreys, seated on the skull."""
    head = _head()
    loc = _loc(C, u)
    P = _P(C, u)
    lo, hi = _box(C, u, 3.0)
    hi = hi + np.array([0, 0, height * u])

    def shape(P_):
        q = loc(P_)
        z = q[:, 2] - 0.45
        w = 0.82 * np.clip(1 - np.clip(z, 0, None) / height, 0, 1) ** 0.6 + 0.02
        side = np.abs(q[:, 1]) - w
        depth = np.abs(q[:, 0] + 0.08) - 0.62 * np.clip(1 - np.clip(z, 0, None) / (height * 0.9), 0.12, 1)
        return np.maximum.reduce([side, depth, -z - 0.1, z - height]) * u
    f = F.Field('Mitre')
    f.add(F.Fn(shape, lo, hi), 0.04 * u)
    f.cut(F.Fn(lambda P_: head.eval(P_) - 0.02 * u, lo, hi), 0.02 * u)
    f.cut(F.Box(P(-0.08, 0, 0.45 + height * 0.92), (0.05 * u, 0.9 * u, height * 0.12 * u), r=0.02 * u), 0.04 * u)
    out = [_mesh(f, u * 0.03, coll, M[mat], 'Mitre')]
    g = F.Field('Mitre orphrey')
    g.add(F.Fn(lambda P_: np.maximum(shape(P_) - 0.012 * u, np.abs(loc(P_)[:, 1]) * u - 0.1 * u), lo, hi), 0.0)
    g.add(F.Fn(lambda P_: np.maximum(shape(P_) - 0.012 * u, np.abs(loc(P_)[:, 2] - 0.55) * u - 0.08 * u), lo, hi), 0.0)
    g.cut(F.Fn(lambda P_: head.eval(P_) - 0.02 * u, lo, hi), 0.02 * u)
    out.append(_mesh(g, u * 0.025, coll, M[trim], 'Mitre orphrey', 2))
    return out
