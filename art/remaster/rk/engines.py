"""Siege engines in the roster-sheet style: chunky rounded oak beams with iron straps and rivets, big
iron-shod spoked wheels, rope torsion bundles, banners and plague-green ammunition.

Builders add (object, bone) parts to a machine Character (rk/machine.py skeleton: body, arm, slider/
barrel, wheel.*). Coordinates: x forward, y left, z up; the engine faces +X.
"""
import math

import numpy as np
from mathutils import Matrix, Vector

from . import sdf as F
from . import sigil

V = lambda *a: Vector(a)


def _frame(a, b, up=(0, 0, 1)):
    x = (Vector(b) - Vector(a)).normalized()
    u = Vector(up)
    if abs(x.dot(u)) > 0.95:
        u = Vector((0, 1, 0))
    y = u.cross(x).normalized()
    z = x.cross(y).normalized()
    return Matrix((x, y, z)).transposed()


def beam(f, a, b, w=0.12, h=0.12, r=0.025, up=(0, 0, 1), k=0.0, tag=None):
    a, b = Vector(a), Vector(b)
    L = (b - a).length
    R = _frame(a, b, up)
    f.add(F.Box((a + b) * 0.5, (L * 0.5 + w * 0.15, w * 0.5, h * 0.5), R, r=r), k, tag)


def grain(scale=1.0):
    return lambda P: F.noise3(P * np.array([1, 1, 6.0]), 30.0 * scale, 2, 1) * 0.0015


class Shop:
    """Mesh helpers for one machine."""

    def __init__(self, ch, M):
        self.ch, self.M, self.coll = ch, M, ch.coll

    def mat(self, m):
        return self.M[m] if isinstance(m, str) else m

    def add(self, f, mat, bone, h=0.006, name=None, smooth=2):
        o = f.mesh(h, self.coll, self.mat(mat), name=name or f.name, smooth=smooth)
        self.ch.parts.append((o, bone))
        return o

    def beams(self, specs, mat='wood', bone='body', name='Timber', h=0.006):
        """specs: list of (a, b, w, h[, up])."""
        f = F.Field(name)
        for sp in specs:
            a, b, w, hh = sp[:4]
            beam(f, a, b, w, hh, r=min(w, hh) * 0.2, up=sp[4] if len(sp) > 4 else (0, 0, 1))
        f.disp = grain()
        return self.add(f, mat, bone, h, name)

    def straps(self, specs, mat='dark_steel', bone='body', name='Iron strap', rivets=True):
        """specs: (a, b, w, h) beams to wrap with an iron band at their middle (and rivets)."""
        f = F.Field(name)
        rv = F.Field(name + ' rivets')
        for sp in specs:
            a, b, w, hh = (Vector(sp[0]), Vector(sp[1]), sp[2], sp[3])
            for t in sp[4] if len(sp) > 4 else (0.5,):
                c = a.lerp(b, t)
                R = _frame(a, b)
                f.add(F.Box(c, (0.022, w * 0.5 + 0.008, hh * 0.5 + 0.008), R, r=0.006), 0.0)
                if rivets:
                    for s in (-1, 1):
                        rv.add(F.Sphere(c + R @ V(0, s * (w * 0.5 + 0.01), 0), 0.011), 0.0)
                        rv.add(F.Sphere(c + R @ V(0, 0, s * (hh * 0.5 + 0.01)), 0.011), 0.0)
        out = [self.add(f, mat, bone, 0.004, name)]
        if rivets and rv.ops:
            out.append(self.add(rv, mat, bone, 0.0035, name + ' rivets', 1))
        return out

    def wheel(self, name, c, r, width=0.1, spokes=8, wood='wood', iron='dark_steel', bone=None):
        bone = bone or name
        c = Vector(c)
        Rw = Matrix.Rotation(math.radians(90), 3, 'X')   # wheel axis along Y
        f = F.Field(name + ' felloe')
        f.add(F.Torus(c, r * 0.86, r * 0.1, Rw, squash=width / (r * 0.2)), 0.0)
        for k in range(spokes):
            a = k * math.tau / spokes
            d = V(math.cos(a), 0, math.sin(a))
            f.add(F.RoundCone(c + d * r * 0.18, c + d * r * 0.82, r * 0.06, r * 0.045, squash=(1.0, 0.7), up=(0, 1, 0)),
                  0.01)
        f.add(F.RoundCone(c - V(0, width * 0.7, 0), c + V(0, width * 0.7, 0), r * 0.17, r * 0.17), 0.02)
        f.disp = grain()
        out = [self.add(f, wood, bone, 0.005, name + ' wheel')]
        t = F.Field(name + ' tire')
        t.add(F.Torus(c, r * 0.985, r * 0.045, Rw, squash=width * 0.55 / (r * 0.09)), 0.0)
        t.add(F.RoundCone(c - V(0, width * 0.85, 0), c + V(0, width * 0.85, 0), r * 0.11, r * 0.11), 0.0)
        for k in range(spokes * 2):
            a = (k + 0.5) * math.tau / (spokes * 2)
            for s in (-1, 1):
                t.add(F.Sphere(c + V(math.cos(a) * r * 0.985, s * width * 0.5, math.sin(a) * r * 0.985), r * 0.035), 0.0)
        out.append(self.add(t, iron, bone, 0.004, name + ' tire'))
        return out

    def banner(self, base, height, mat, emblem=None, emblem_mat=None, width=0.32, drop=0.55, bone='body',
               pole='wood', finial='trim', tatter=0.0, seed=2):
        """A pole with a hanging banner (faces -Y, toward the camera side)."""
        base = Vector(base)
        top = base + V(0, 0, height)
        p = F.Field('Banner pole')
        p.add(F.RoundCone(base, top, 0.022, 0.02), 0.0)
        p.add(F.RoundCone(top + V(-0.02, 0, -0.02), top + V(width + 0.06, 0, -0.02), 0.014, 0.014), 0.0)
        out = [self.add(p, pole, bone, 0.004, 'Banner pole')]
        k = F.Field('Banner finial')
        k.add(F.RoundCone(top, top + V(0, 0, 0.12), 0.03, 0.002), 0.0)
        k.add(F.Sphere(top, 0.03), 0.0)
        out.append(self.add(k, finial, bone, 0.003, 'Banner finial'))
        x0, z0 = top.x + 0.02, top.z - 0.03
        rng = np.random.default_rng(seed)
        ph = rng.uniform(0, 6.28, 3)

        def cloth(P):
            u = (P[:, 0] - x0) / width
            v = (z0 - P[:, 2]) / drop
            wave = 0.02 * np.sin(u * 7 + v * 3)
            hem = 1.0 - 0.18 * np.clip(1 - np.abs(u - 0.5) * 2, 0, 1) - tatter * 0.08 * np.abs(np.sin(u * 13 + ph[0]))
            d = np.maximum.reduce([np.abs(P[:, 1] - base.y - wave) - 0.008, -u * width, (u - 1) * width, -v * drop,
                                   (v - hem) * drop])
            return d
        lo = np.array([x0 - 0.05, base.y - 0.1, z0 - drop - 0.05])
        hi = np.array([x0 + width + 0.05, base.y + 0.1, z0 + 0.05])
        c = F.Field('Banner')
        c.add(F.Fn(cloth, lo, hi), 0.0)
        out.append(self.add(c, mat, bone, 0.004, 'Banner'))
        if emblem:
            shape = sigil.EMBLEMS[emblem]()
            sc = width * 0.36
            cx, cz = x0 + width * 0.5, z0 - drop * 0.42

            def efn(P):
                u = (P[:, 0] - cx) / sc
                v = (P[:, 2] - cz) / sc
                wave = 0.02 * np.sin((P[:, 0] - x0) / width * 7 + (z0 - P[:, 2]) / drop * 3)
                return np.maximum(shape(u, v) * sc, np.abs(P[:, 1] - base.y - wave + 0.009) - 0.004)
            e = F.Field('Banner emblem')
            e.add(F.Fn(efn, lo, hi), 0.0)
            out.append(self.add(e, emblem_mat or finial, bone, 0.003, 'Banner emblem', 1))
        return out


def ballista(ch, M, wood='wood', iron='dark_steel', rope='rope', bone_mat=None, banner=None, emblem='lantern',
             emblem_mat='gold', skulls=False):
    """The roster sheet's ballista: a heavy oak carriage, torsion arms, a winch at the rear and a loaded
    bolt on the track. Returns (bolt objects, tip)."""
    S = Shop(ch, M)
    W = wood
    # carriage
    S.beams([(V(-0.62, -0.3, 0.42), V(0.62, -0.3, 0.42), 0.13, 0.13), (V(-0.62, 0.3, 0.42), V(0.62, 0.3, 0.42), 0.13, 0.13),
             (V(-0.5, -0.36, 0.44), V(-0.5, 0.36, 0.44), 0.12, 0.12), (V(0.0, -0.36, 0.44), V(0.0, 0.36, 0.44), 0.12, 0.12),
             (V(0.5, -0.36, 0.44), V(0.5, 0.36, 0.44), 0.12, 0.12),
             (V(0.05, -0.12, 0.48), V(0.05, -0.12, 0.74), 0.12, 0.12), (V(0.05, 0.12, 0.48), V(0.05, 0.12, 0.74), 0.12, 0.12)],
            W, 'body', 'Carriage')
    S.straps([(V(-0.62, -0.3, 0.42), V(0.62, -0.3, 0.42), 0.13, 0.13, (0.12, 0.88)),
              (V(-0.62, 0.3, 0.42), V(0.62, 0.3, 0.42), 0.13, 0.13, (0.12, 0.88))], iron, 'body')
    # stock and torsion frame
    S.beams([(V(-0.7, 0, 0.82), V(0.75, 0, 0.82), 0.16, 0.11), (V(0.42, -0.24, 0.66), V(0.42, -0.24, 1.08), 0.1, 0.1),
             (V(0.42, 0.24, 0.66), V(0.42, 0.24, 1.08), 0.1, 0.1), (V(0.42, -0.3, 1.06), V(0.42, 0.3, 1.06), 0.1, 0.1),
             (V(0.42, -0.3, 0.68), V(0.42, 0.3, 0.68), 0.1, 0.1)], W, 'arm', 'Stock')
    S.straps([(V(-0.7, 0, 0.82), V(0.75, 0, 0.82), 0.16, 0.11, (0.15, 0.5, 0.85))], iron, 'arm')
    rp = F.Field('Torsion rope')
    for sy in (-1, 1):
        rp.add(F.RoundCone(V(0.42, sy * 0.16, 0.7), V(0.42, sy * 0.16, 1.04), 0.065, 0.065), 0.0)
    rp.disp = lambda P: 0.004 * np.sin(P[:, 2] * 160 + np.arctan2(P[:, 1], P[:, 0] - 0.42) * 2)
    S.add(rp, rope, 'arm', 0.004, 'Torsion rope')
    arms_f = F.Field('Bow arms')
    tips = []
    for sy in (-1, 1):
        a = V(0.44, sy * 0.16, 0.88)
        m = V(0.36, sy * 0.5, 0.9)
        t = V(0.2, sy * 0.78, 0.9)
        arms_f.add(F.Chain([a, m, t], [0.055, 0.045, 0.032], squash=(1.0, 0.8)), 0.0)
        tips.append(t)
    arms_f.disp = grain()
    S.add(arms_f, bone_mat or W, 'arm', 0.005, 'Bow arms')
    caps = F.Field('Arm caps')
    for t in tips:
        caps.add(F.Sphere(t, 0.04), 0.0)
    S.add(caps, iron, 'arm', 0.004, 'Arm caps')
    # bowstring to the slider, the slider claw and a heavy bolt
    sl = V(-0.12, 0, 0.9)
    st = F.Field('Ballista string')
    for t in tips:
        st.add(F.RoundCone(t, sl, 0.009, 0.009), 0.0)
    S.add(st, 'string', 'slider', 0.003, 'Ballista string', 1)
    cl = F.Field('Slider')
    cl.add(F.Box(sl + V(0, 0, 0.0), (0.09, 0.07, 0.04), r=0.015), 0.0)
    S.add(cl, iron, 'slider', 0.004, 'Slider')
    bolt = F.Field('Siege bolt')
    bolt.add(F.RoundCone(V(-0.05, 0, 0.93), V(0.95, 0, 0.93), 0.026, 0.026), 0.0)
    for k in range(3):
        a = k * math.tau / 3
        d = V(0, math.cos(a), math.sin(a))
        bolt.add(F.Box(V(0.02, 0, 0.93) + d * 0.04, (0.07, 0.003, 0.03), _frame(V(0, 0, 0), V(1, 0, 0), up=tuple(d)),
                       r=0.003), 0.004)
    b1 = S.add(bolt, W, 'slider', 0.004, 'Siege bolt')
    hd = F.Field('Bolt head')
    hd.add(F.RoundCone(V(0.92, 0, 0.93), V(1.12, 0, 0.93), 0.05, 0.004), 0.0)
    hd.add(F.RoundCone(V(0.88, 0, 0.93), V(0.95, 0, 0.93), 0.034, 0.034), 0.01)
    b2 = S.add(hd, 'blade', 'slider', 0.003, 'Bolt head', 1)
    # winch
    S.beams([(V(-0.62, -0.18, 0.82), V(-0.62, 0.18, 0.82), 0.1, 0.1, (1, 0, 0))], W, 'arm', 'Winch')
    # winch: a spoked hand wheel each side (the crewman's), handles on the rim
    cr = F.Field('Winch wheels')
    hd = F.Field('Winch handles')
    Rx = Matrix.Rotation(math.radians(90), 3, 'X')
    for sy in (-1, 1):
        c = V(-0.62, sy * 0.24, 0.82)
        for k in range(6):
            a = k * math.tau / 6
            d = V(math.cos(a), 0, math.sin(a))
            cr.add(F.RoundCone(c, c + d * 0.17, 0.016, 0.012), 0.004)
            hd.add(F.RoundCone(c + d * 0.18, c + d * 0.25, 0.018, 0.022), 0.0)
        cr.add(F.Torus(c, 0.165, 0.016, Rx), 0.004)
        cr.add(F.Torus(c, 0.035, 0.016, Rx), 0.004)
    S.add(cr, W, 'arm', 0.004, 'Winch wheels')
    S.add(hd, iron, 'arm', 0.003, 'Winch handles')
    for w in ch.J['wheels']:
        S.wheel(w, ch.J[w], 0.3, width=0.1, wood=bone_mat or W, iron=iron)
    if banner is not None:
        S.banner(V(-0.42, -0.36, 0.48), 0.95, banner, emblem=emblem, emblem_mat=emblem_mat, width=0.3, drop=0.5)
    if skulls:
        sk = F.Field('Trophy skulls')
        for x in (-0.35, 0.25):
            for sy in (-1, 1):
                sk.add(F.Ellipsoid(V(x, sy * 0.38, 0.44), (0.075, 0.065, 0.07)), 0.0)
        S.add(sk, 'bone', 'body', 0.004, 'Trophy skulls')
    return [b1, b2], V(1.12, 0, 0.93)


def catapult(ch, M, wood='wood', iron='dark_steel', ammo='plague', banner=None, emblem='rot', emblem_mat='linen',
             chain='chain'):
    """The plague engine: a wheeled catapult whose arm swings a chained, glowing plague ball."""
    S = Shop(ch, M)
    W = wood
    S.beams([(V(-0.7, -0.34, 0.44), V(0.7, -0.34, 0.44), 0.14, 0.14), (V(-0.7, 0.34, 0.44), V(0.7, 0.34, 0.44), 0.14, 0.14),
             (V(-0.6, -0.4, 0.46), V(-0.6, 0.4, 0.46), 0.13, 0.13), (V(0.0, -0.4, 0.46), V(0.0, 0.4, 0.46), 0.13, 0.13),
             (V(0.6, -0.4, 0.46), V(0.6, 0.4, 0.46), 0.13, 0.13)], W, 'body', 'Catapult bed')
    # A-frame uprights and the cross beam (the arm's stop)
    S.beams([(V(-0.15, -0.32, 0.5), V(0.1, -0.3, 1.45), 0.11, 0.11), (V(0.35, -0.32, 0.5), V(0.1, -0.3, 1.45), 0.11, 0.11),
             (V(-0.15, 0.32, 0.5), V(0.1, 0.3, 1.45), 0.11, 0.11), (V(0.35, 0.32, 0.5), V(0.1, 0.3, 1.45), 0.11, 0.11),
             (V(0.1, -0.38, 1.38), V(0.1, 0.38, 1.38), 0.12, 0.12), (V(0.22, -0.38, 1.06), V(0.22, 0.38, 1.06), 0.1, 0.1)],
            W, 'body', 'Catapult frame')
    S.straps([(V(-0.7, -0.34, 0.44), V(0.7, -0.34, 0.44), 0.14, 0.14, (0.15, 0.85)),
              (V(-0.7, 0.34, 0.44), V(0.7, 0.34, 0.44), 0.14, 0.14, (0.15, 0.85)),
              (V(0.1, -0.38, 1.38), V(0.1, 0.38, 1.38), 0.12, 0.12, (0.2, 0.8))], iron, 'body')
    # throwing arm (bone 'arm' pivots at the axle) with a cup and the chained ball
    S.beams([(V(-0.62, 0, 0.6), V(0.62, 0, 1.42), 0.11, 0.1)], W, 'arm', 'Throwing arm')
    S.straps([(V(-0.62, 0, 0.6), V(0.62, 0, 1.42), 0.11, 0.1, (0.3, 0.6))], iron, 'arm')
    ax = F.Field('Axle')
    ax.add(F.RoundCone(V(-0.05, -0.38, 0.86), V(-0.05, 0.38, 0.86), 0.05, 0.05), 0.0)
    S.add(ax, iron, 'body', 0.004, 'Axle')
    top = V(0.62, 0, 1.42)
    hook = top + V(0.04, 0, -0.05)
    ball_c = hook + V(0.06, 0, -0.5)
    rb = 0.25
    ch_ = F.Field('Sling chain')
    n = 9
    for k in range(n):
        p = hook.lerp(ball_c + V(0, 0, rb + 0.06), (k + 0.5) / n)
        R = Matrix.Rotation(math.radians(90 * (k % 2)), 3, 'Z')
        ch_.add(F.Torus(p, 0.03, 0.01, R @ Matrix.Rotation(math.radians(90), 3, 'Y')), 0.0)
    S.add(ch_, chain if chain in M else iron, 'arm', 0.003, 'Sling chain', 1)
    cage = F.Field('Ball cage')
    for k in range(5):
        a = k * math.tau / 5
        cage.add(F.Chain([ball_c + V(0, 0, rb + 0.05), ball_c + V(math.cos(a) * (rb + 0.03), math.sin(a) * (rb + 0.03), 0.04),
                          ball_c + V(math.cos(a) * rb * 0.7, math.sin(a) * rb * 0.7, -rb * 0.85)], 0.016), 0.004)
    cage.add(F.Torus(ball_c + V(0, 0, 0.04), rb + 0.03, 0.016), 0.004)
    cage.add(F.Sphere(ball_c + V(0, 0, rb + 0.06), 0.04), 0.01)
    S.add(cage, iron, 'arm', 0.003, 'Ball cage')
    bf = F.Field('Plague ball')
    bf.add(F.Sphere(ball_c, rb), 0.0)
    bf.disp = lambda P: F.noise3(P, 22.0, 4, 2) * 0.012
    ball = S.add(bf, ammo, 'arm', 0.004, 'Plague ball', 1)
    drip = F.Field('Plague drip')
    for k, (dx, dy, ln) in enumerate([(0.07, 0.04, 0.16), (-0.08, 0.03, 0.1), (0.0, -0.08, 0.22), (0.1, -0.05, 0.08)]):
        p = ball_c + V(dx, dy, -rb * 0.8)
        drip.add(F.RoundCone(p, p + V(0, 0, -ln), 0.035, 0.014), 0.025)
        drip.add(F.Sphere(p + V(0, 0, -ln - 0.01), 0.022), 0.012)
    S.add(drip, ammo, 'arm', 0.003, 'Plague drip', 1)
    # bone stakes and a horned skull on the bow of the bed
    sp = F.Field('Bone stakes')
    for sy, ln in ((-0.3, 0.42), (0.0, 0.34), (0.3, 0.42)):
        sp.add(F.RoundCone(V(0.66, sy, 0.48), V(0.66 + ln, sy * 1.15, 0.26), 0.05, 0.006), 0.0)
    sp.disp = lambda P: F.noise3(P, 40.0, 2, 3) * 0.004
    S.add(sp, 'bone', 'body', 0.004, 'Bone stakes')
    sk = F.Field('Horned skull')
    c = V(0.78, 0, 0.66)
    sk.add(F.Ellipsoid(c, (0.1, 0.11, 0.1)), 0.0)
    sk.add(F.RoundCone(c + V(0.02, 0, -0.03), c + V(0.15, 0, -0.08), 0.07, 0.045, squash=(0.85, 1.0), up=(0, 1, 0)), 0.03)
    for sy in (-1, 1):
        sk.cut(F.Ellipsoid(c + V(0.09, sy * 0.045, 0.0), (0.035, 0.03, 0.03)), 0.012)
        sk.add(F.Chain([c + V(-0.02, sy * 0.08, 0.06), c + V(-0.05, sy * 0.22, 0.12), c + V(0.05, sy * 0.3, 0.2),
                        c + V(0.14, sy * 0.27, 0.18)], [0.04, 0.032, 0.02, 0.006]), 0.02)
    sk.cut(F.Ellipsoid(c + V(0.16, 0, -0.07), (0.02, 0.012, 0.012)), 0.006)
    S.add(sk, 'bone', 'body', 0.004, 'Horned skull')
    eg = F.Field('Skull glow')
    for sy in (-1, 1):
        eg.add(F.Sphere(c + V(0.075, sy * 0.045, 0.0), 0.018), 0.0)
    S.add(eg, ammo, 'body', 0.002, 'Skull glow', 1)
    cw = F.Field('Counterweight')
    cw.add(F.Box(V(-0.6, 0, 0.5), (0.14, 0.18, 0.16), r=0.03), 0.0)
    S.add(cw, iron, 'arm', 0.005, 'Counterweight')
    for w in ch.J['wheels']:
        S.wheel(w, ch.J[w], 0.34, width=0.11, wood=W, iron=iron)
    if banner is not None:
        S.banner(V(-0.6, -0.4, 0.5), 1.45, banner, emblem=emblem, emblem_mat=emblem_mat, width=0.36, drop=0.7,
                 tatter=1.0)
    return [ball], ball_c
