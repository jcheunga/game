"""Weapons and shields in the roster sheet's chunky style, sculpted as signed-distance fields.

Canonical frame (as rk/weapons.py): the primary grip at the origin, the working end toward +Z, blade
width along X, thickness along Y (flats face the battle camera). Builders return
dict(objs, tip, grip2, kind, grip_r) like the older kit, so Character.hold/hold_at work unchanged.
"""
import math

import numpy as np
from mathutils import Matrix, Vector

from . import sdf as F
from . import sigil

V = lambda *a: Vector(a)


def _m(f, h, coll, mat, name, smooth=2):
    return f.mesh(h, coll, mat, name=name, smooth=smooth)


def _blade_fn(z0, length, width, thick, tip=0.25, taper=0.25, leaf=0.0, fuller=0.3, single=False, curve=0.0,
              ridge=True):
    """Blade cross-section swept along +Z: lenticular, tapering to a point, optional leaf swell and fuller."""
    def fn(P):
        x, y, z = P[:, 0], P[:, 1], P[:, 2]
        t = np.clip((z - z0) / length, 0, 1)
        w = width * 0.5 * (1 - taper * t) * (1 + leaf * np.sin(np.pi * np.clip(t * 1.15, 0, 1)))
        tipk = np.clip((1 - t) / tip, 0, 1) ** 0.75
        w = w * np.where(t > 1 - tip, tipk, 1.0)
        cx = curve * length * t ** 2
        xx = x - cx
        if single:
            xx = xx + w * 0.3
        th = thick * 0.5 * (1 - 0.55 * t) * (np.clip(1 - np.abs(xx) / np.maximum(w, 1e-4), 0, 1) ** (0.6 if ridge else 0.2))
        # elliptical-ish section
        d = np.maximum(np.abs(xx) - w, np.abs(y) - th)
        if fuller:
            groove = (np.abs(xx) < w * 0.18) & (t < 0.7)
            d = d + groove * thick * 0.12 * fuller
        out = np.maximum(d, np.maximum(z0 - z, z - (z0 + length)))
        return out
    return fn


def _grip(f, z0, z1, r, wraps=True):
    f.add(F.RoundCone(V(0, 0, z0), V(0, 0, z1), r, r * 0.95), 0.0)
    if wraps:
        n = max(2, int((z1 - z0) / 0.028))
        for i in range(n):
            z = z0 + (i + 0.5) * (z1 - z0) / n
            f.add(F.Torus(V(0, 0, z), r * 0.95, r * 0.28, None, squash=1.0), 0.004)


def sword(M, coll, length=0.86, width=0.11, guard=0.3, grip=0.18, pommel=1.0, fuller=1.0, curve=0.0, single=False,
          guard_style='straight', two_hand=False, rune=None, blade='blade', hilt='hilt', grip_mat='grip'):
    out = []
    gz = grip * 0.5
    f = F.Field('Blade')
    f.add(F.Fn(_blade_fn(gz + 0.02, length, width, 0.026, tip=0.22, taper=0.3, fuller=fuller, single=single,
                         curve=curve), (-width - 0.2, -0.05, gz - 0.05), (width + 0.2, 0.05, gz + length + 0.05)), 0.0)
    out.append(_m(f, 0.0035, coll, M[blade], 'Blade', 1))
    h = F.Field('Hilt')
    if guard_style == 'straight':
        h.add(F.Chain([V(-guard / 2, 0, gz + 0.02), V(-guard * 0.25, 0, gz), V(guard * 0.25, 0, gz), V(guard / 2, 0, gz + 0.02)],
                      [0.02, 0.026, 0.026, 0.02], squash=(1.0, 0.75)), 0.01)
        for sx in (-1, 1):
            h.add(F.Sphere(V(sx * guard / 2, 0, gz + 0.025), 0.025), 0.01)
    else:
        h.add(F.Chain([V(-guard / 2, 0, gz + 0.06), V(-guard * 0.25, 0, gz), V(guard * 0.25, 0, gz), V(guard / 2, 0, gz + 0.06)],
                      [0.016, 0.026, 0.026, 0.016]), 0.01)
    h.add(F.Ellipsoid(V(0, 0, gz + 0.01), (0.04, 0.03, 0.03)), 0.01)
    h.add(F.Sphere(V(0, 0, -gz - 0.03), 0.037 * pommel), 0.01)
    h.add(F.Ellipsoid(V(0, 0, -gz - 0.03), (0.045 * pommel, 0.02, 0.03 * pommel)), 0.01)
    out.append(_m(h, 0.003, coll, M[hilt], 'Hilt'))
    g = F.Field('Grip')
    _grip(g, -gz, gz, 0.022)
    out.append(_m(g, 0.0025, coll, M[grip_mat], 'Grip'))
    tip = V(curve * length, 0, gz + length)
    return dict(objs=out, tip=tip, grip2=V(0, 0, -gz + 0.02) if two_hand else None, kind='sword', grip_r=0.022)


def spear(M, coll, length=2.2, head_len=0.36, head_w=0.11, grip_at=0.3, shaft_r=0.026, langets=True, banner=None,
          tassel=None, wood='wood', head_mat='blade', fitting='trim', butt=True, wings=False):
    out = []
    z0 = -length * grip_at
    z1 = z0 + length
    zs = z1 - head_len - 0.08
    f = F.Field('Spear shaft')
    f.add(F.RoundCone(V(0, 0, z0), V(0, 0, zs), shaft_r, shaft_r * 0.9), 0.0)
    out.append(_m(f, 0.004, coll, M[wood], 'Spear shaft'))
    h = F.Field('Spearhead')
    h.add(F.Fn(_blade_fn(z1 - head_len, head_len, head_w, 0.03, tip=0.45, taper=0.0, leaf=0.55, fuller=0.0),
               (-head_w, -0.05, z1 - head_len - 0.02), (head_w, 0.05, z1 + 0.02)), 0.0)
    h.add(F.RoundCone(V(0, 0, zs - 0.01), V(0, 0, z1 - head_len + 0.03), shaft_r * 1.1, shaft_r * 0.7), 0.01)
    if wings:
        for sx in (-1, 1):
            h.add(F.RoundCone(V(0, 0, z1 - head_len + 0.01), V(sx * 0.09, 0, z1 - head_len + 0.05), 0.016, 0.005), 0.01)
    out.append(_m(h, 0.003, coll, M[head_mat], 'Spearhead', 1))
    c = F.Field('Spear fittings')
    c.add(F.RoundCone(V(0, 0, zs - 0.03), V(0, 0, zs + 0.02), shaft_r * 1.35, shaft_r * 1.3), 0.004)
    c.add(F.Torus(V(0, 0, zs + 0.04), shaft_r * 1.05, 0.009), 0.004)
    if langets:
        for sx in (-1, 1):
            c.add(F.Box(V(sx * shaft_r * 0.95, 0, zs - 0.1), (0.006, 0.012, 0.07), r=0.003), 0.003)
    if butt:
        c.add(F.RoundCone(V(0, 0, z0 - 0.035), V(0, 0, z0 + 0.03), shaft_r * 1.0, shaft_r * 1.25), 0.004)
    out.append(_m(c, 0.0028, coll, M[fitting], 'Spear fittings'))
    for i in range(3):
        g = F.Field('Shaft wrap')
        z = (-0.08 + i * 0.07)
        g.add(F.Torus(V(0, 0, z), shaft_r * 1.0, 0.007), 0.0)
        out.append(_m(g, 0.0025, coll, M['grip'], 'Shaft wrap'))
    if banner is not None:
        out += pennon(banner, zs - 0.05, coll)
    if tassel is not None:
        t = F.Field('Tassel')
        for i in range(6):
            a = i * math.tau / 6
            p0 = V(0, 0, zs - 0.02)
            t.add(F.RoundCone(p0, p0 + V(math.cos(a) * .03 - 0.05, math.sin(a) * .03, -0.14), 0.012, 0.005), 0.01)
        out.append(_m(t, 0.003, coll, tassel, 'Tassel'))
    return dict(objs=out, tip=V(0, 0, z1), grip2=V(0, 0, 0.5), kind='spear', grip_r=shaft_r)


def pennon(mat, z, coll, length=0.5, height=0.22, emblem=None, tails=2):
    """A swallow-tailed pennon on a pole (in the pole's frame, hanging toward -X)."""
    f = F.Field('Pennon')

    def fn(P):
        x, y, zz = -P[:, 0], P[:, 1], P[:, 2]
        u = np.clip(x / length, 0, 1)
        top = z
        bot = z - height * (1 - 0.35 * u)
        wave = 0.025 * np.sin(u * 7) * u
        d = np.maximum(np.abs(y - wave) - 0.006, np.maximum(zz - top, bot - zz))
        d = np.maximum(d, np.maximum(-x, x - length))
        if tails:
            notch = (u > 0.72) & (np.abs(zz - (z - height * 0.5 * (1 - 0.35 * u))) < height * 0.18 * (u - 0.72) / 0.28)
            d = np.where(notch, np.maximum(d, 0.004), d)
        return d
    f.add(F.Fn(fn, (-length - 0.05, -0.08, z - height - 0.05), (0.02, 0.08, z + 0.03)), 0.0)
    return [_m(f, 0.004, coll, mat, 'Pennon')]


def axe(M, coll, haft_len=0.78, head=1.0, beard=True, double=False, spike=False, two_hand=False, wood='wood',
        steel='blade', fitting='trim'):
    out = []
    s = head
    top = haft_len * 0.62
    f = F.Field('Axe haft')
    f.add(F.RoundCone(V(0, 0, -haft_len * 0.38), V(0, 0, top + 0.03), 0.024, 0.022), 0.0)
    out.append(_m(f, 0.004, coll, M[wood], 'Axe haft'))
    hz = top - 0.05 * s

    def head_fn(P, side=1):
        x, y, z = P[:, 0] * side, P[:, 1], P[:, 2] - hz
        # bearded blade: a wedge widening outward, curved edge
        u = np.clip(x / (0.2 * s), 0, 1)
        half_h = (0.05 + 0.09 * u ** 1.2) * s
        zc = -0.02 * s * u * (1.6 if beard else 0.4)
        th = 0.022 * (1 - 0.75 * u)
        edge = np.sqrt(np.maximum(0, 1 - ((z - zc) / np.maximum(half_h, 1e-3)) ** 2)) * 0.04 * s
        d = np.maximum.reduce([np.abs(z - zc) - half_h, np.abs(y) - th, -x + 0.0, x - 0.2 * s - edge])
        return d
    h = F.Field('Axe head')
    h.add(F.Fn(head_fn, (-0.05, -0.05, hz - 0.2 * s), (0.3 * s, 0.05, hz + 0.2 * s)), 0.0)
    if double:
        h.add(F.Fn(lambda P: head_fn(P, -1), (-0.3 * s, -0.05, hz - 0.2 * s), (0.05, 0.05, hz + 0.2 * s)), 0.01)
    h.add(F.Box(V(0, 0, hz), (0.04, 0.035, 0.07 * s), r=0.012), 0.012)
    if spike:
        h.add(F.RoundCone(V(-0.03, 0, hz), V(-0.14 * s, 0, hz + 0.02), 0.025, 0.004), 0.01)
    out.append(_m(h, 0.0032, coll, M[steel], 'Axe head', 1))
    c = F.Field('Axe fittings')
    c.add(F.Torus(V(0, 0, hz - 0.085 * s), 0.026, 0.008), 0.0)
    c.add(F.Sphere(V(0, 0, top + 0.035), 0.022), 0.0)
    out.append(_m(c, 0.0025, coll, M[fitting], 'Axe fittings'))
    g = F.Field('Axe grip')
    _grip(g, -0.1, 0.08, 0.026)
    out.append(_m(g, 0.0025, coll, M['grip'], 'Axe grip'))
    return dict(objs=out, tip=V(0.22 * s, 0, hz), grip2=V(0, 0, -0.26) if two_hand else None, kind='axe', grip_r=0.026)


def mace(M, coll, haft_len=0.62, head=1.0, flanges=7, spikes=False, wood='wood', steel='dark_steel', fitting='trim'):
    out = []
    s = head
    top = haft_len * 0.65
    f = F.Field('Mace haft')
    f.add(F.RoundCone(V(0, 0, -haft_len * 0.35), V(0, 0, top), 0.022, 0.02), 0.0)
    out.append(_m(f, 0.004, coll, M[wood], 'Mace haft'))
    h = F.Field('Mace head')
    c = V(0, 0, top + 0.03 * s)
    h.add(F.Ellipsoid(c, (0.055 * s, 0.055 * s, 0.07 * s)), 0.0)
    for i in range(flanges):
        a = i * math.tau / flanges
        d = V(math.cos(a), math.sin(a), 0)
        h.add(F.Box(c + d * 0.05 * s, (0.04 * s, 0.009, 0.075 * s), Matrix.Rotation(a, 3, 'Z'), r=0.006), 0.012)
    if spikes:
        h.add(F.RoundCone(c + V(0, 0, 0.06 * s), c + V(0, 0, 0.13 * s), 0.02, 0.003), 0.01)
    h.add(F.Sphere(c + V(0, 0, 0.085 * s), 0.025 * s), 0.01)
    out.append(_m(h, 0.003, coll, M[steel], 'Mace head', 1))
    g = F.Field('Mace grip')
    _grip(g, -0.12, 0.08, 0.024)
    g.add(F.Sphere(V(0, 0, -0.15), 0.03), 0.005)
    out.append(_m(g, 0.0025, coll, M['grip'], 'Mace grip'))
    k = F.Field('Mace collar')
    k.add(F.Torus(V(0, 0, top - 0.06 * s), 0.024, 0.009), 0.0)
    out.append(_m(k, 0.0025, coll, M[fitting], 'Mace collar'))
    return dict(objs=out, tip=c, grip2=None, kind='mace', grip_r=0.024)


def hammer(M, coll, haft_len=0.9, head=1.0, two_hand=True, spike=True, wood='wood', steel='dark_steel', fitting='trim'):
    out = []
    s = head
    top = haft_len * 0.62
    f = F.Field('Hammer haft')
    f.add(F.RoundCone(V(0, 0, -haft_len * 0.38), V(0, 0, top + 0.04), 0.025, 0.023), 0.0)
    out.append(_m(f, 0.004, coll, M[wood], 'Hammer haft'))
    h = F.Field('Hammer head')
    c = V(0, 0, top)
    h.add(F.Box(c + V(0.06 * s, 0, 0), (0.08 * s, 0.055 * s, 0.065 * s), r=0.016), 0.0)
    h.add(F.Box(c + V(0.15 * s, 0, 0), (0.025 * s, 0.065 * s, 0.075 * s), r=0.012), 0.01)
    if spike:
        h.add(F.RoundCone(c - V(0.02, 0, 0), c - V(0.16 * s, 0, 0.04), 0.035 * s, 0.004), 0.012)
    out.append(_m(h, 0.0032, coll, M[steel], 'Hammer head', 1))
    k = F.Field('Hammer bands')
    k.add(F.Box(c + V(0.06 * s, 0, 0), (0.012, 0.06 * s, 0.07 * s), r=0.004), 0.0)
    out.append(_m(k, 0.0028, coll, M[fitting], 'Hammer bands'))
    g = F.Field('Hammer grip')
    _grip(g, -0.12, 0.1, 0.027)
    out.append(_m(g, 0.0025, coll, M['grip'], 'Hammer grip'))
    return dict(objs=out, tip=c + V(0.17 * s, 0, 0), grip2=V(0, 0, -0.3) if two_hand else None, kind='hammer',
                grip_r=0.027)


def club(M, coll, length=0.7, head=1.0, nails=True, wood='wood', nail='dark_steel'):
    """A knotted cudgel (the peasant's)."""
    out = []
    s = head
    f = F.Field('Club')
    f.add(F.RoundCone(V(0, 0, -0.12), V(0, 0, length * 0.62), 0.03, 0.065 * s), 0.0)
    rng = np.random.default_rng(3)
    for i in range(6):
        z = rng.uniform(0.1, length * 0.6)
        a = rng.uniform(0, math.tau)
        f.add(F.Sphere(V(math.cos(a) * 0.045, math.sin(a) * 0.045, z), 0.022 * s), 0.02)
    f.add(F.Sphere(V(0, 0, length * 0.62), 0.066 * s), 0.03)
    f.disp = lambda P: F.noise3(P, 30.0, 4, 2) * 0.003
    out.append(_m(f, 0.004, coll, M[wood], 'Club'))
    if nails:
        n = F.Field('Club nails')
        for i in range(9):
            z = 0.28 + (i % 3) * 0.1
            a = i * 2.3
            r = 0.04 + z * 0.04
            p = V(math.cos(a) * r, math.sin(a) * r, z)
            n.add(F.RoundCone(p - V(math.cos(a), math.sin(a), 0) * 0.01, p + V(math.cos(a), math.sin(a), 0) * 0.025,
                              0.006, 0.002), 0.0)
        out.append(_m(n, 0.002, coll, M[nail], 'Club nails'))
    g = F.Field('Club wrap')
    _grip(g, -0.11, 0.06, 0.032)
    out.append(_m(g, 0.003, coll, M['grip'], 'Club wrap'))
    return dict(objs=out, tip=V(0, 0, length * 0.68), grip2=None, kind='club', grip_r=0.032)


def staff(M, coll, length=1.8, grip_at=0.42, head='lantern', wood='wood', fitting='trim', glow='glow', glass='lamp_glass',
          crystal=None, gnarl=0.6):
    """A walking staff with a lantern, crystal, crook or orb head."""
    out = []
    z0 = -length * grip_at
    z1 = z0 + length
    f = F.Field('Staff')
    pts = [V(0.008 * math.sin(i * 1.7) * gnarl, 0.006 * math.cos(i * 2.1) * gnarl, z0 + (z1 - z0) * i / 6)
           for i in range(7)]
    f.add(F.Chain(pts, [0.024, 0.025, 0.024, 0.026, 0.025, 0.027, 0.03]), 0.0)
    if gnarl:
        f.disp = lambda P: F.noise3(P, 25.0, 6, 2) * 0.004 * gnarl
    out.append(_m(f, 0.004, coll, M[wood], 'Staff'))
    k = F.Field('Staff fittings')
    k.add(F.Torus(V(0, 0, z1 - 0.05), 0.027, 0.008), 0.0)
    k.add(F.Torus(V(0, 0, z0 + 0.04), 0.025, 0.008), 0.0)
    out.append(_m(k, 0.0028, coll, M[fitting], 'Staff fittings'))
    tip = V(0, 0, z1 + 0.1)
    if head == 'lantern':
        out += lantern(M, coll, V(0.0, 0, z1), hang=True, glow=glow, glass=glass, fitting=fitting)
        tip = V(0.14, 0, z1 - 0.12)
    elif head == 'crook':
        c = F.Field('Crook')
        pts = [V(0, 0, z1), V(0.02, 0, z1 + 0.12), V(0.12, 0, z1 + 0.2), V(0.2, 0, z1 + 0.12), V(0.17, 0, z1 + 0.04)]
        c.add(F.Chain(pts, [0.028, 0.026, 0.024, 0.022, 0.02]), 0.0)
        out.append(_m(c, 0.004, coll, M[wood], 'Crook'))
        tip = V(0.17, 0, z1 + 0.04)
    elif head in ('crystal', 'orb'):
        c = F.Field('Staff claw')
        for i in range(4):
            a = i * math.tau / 4
            c.add(F.Chain([V(0, 0, z1), V(math.cos(a) * 0.05, math.sin(a) * 0.05, z1 + 0.08),
                           V(math.cos(a) * 0.035, math.sin(a) * 0.035, z1 + 0.16)], [0.014, 0.012, 0.006]), 0.008)
        out.append(_m(c, 0.003, coll, M[fitting], 'Staff claw'))
        g = F.Field('Staff gem')
        if head == 'crystal':
            g.add(F.RoundCone(V(0, 0, z1 + 0.04), V(0, 0, z1 + 0.2), 0.045, 0.005), 0.0)
        else:
            g.add(F.Sphere(V(0, 0, z1 + 0.1), 0.05), 0.0)
        out.append(_m(g, 0.003, coll, crystal or M[glow], 'Staff gem', 1))
        tip = V(0, 0, z1 + 0.12)
    return dict(objs=out, tip=tip, grip2=V(0, 0, -0.32), kind='staff', grip_r=0.026)


def lantern(M, coll, top, hang=True, size=1.0, glow='glow', glass='lamp_glass', fitting='trim', frame='dark_steel'):
    """A caravan lantern hanging from a hook (or standing): cage, glass and a flame."""
    s = size
    out = []
    hook = F.Field('Lantern hook')
    if hang:
        hook.add(F.Chain([top, top + V(0.04, 0, 0.06) * s, top + V(0.12, 0, 0.07) * s, top + V(0.15, 0, 0.02) * s],
                         0.012 * s), 0.0)
        c = top + V(0.15, 0, -0.14) * s
        out.append(_m(hook, 0.003, coll, M[frame], 'Lantern hook'))
    else:
        c = top + V(0, 0, 0.12) * s
    cage = F.Field('Lantern cage')
    cage.add(F.RoundCone(c + V(0, 0, 0.08) * s, c + V(0, 0, 0.13) * s, 0.06 * s, 0.025 * s), 0.0)
    cage.add(F.Torus(c + V(0, 0, 0.13) * s, 0.02 * s, 0.008 * s), 0.004)
    cage.add(F.Box(c - V(0, 0, 0.08) * s, (0.055 * s, 0.055 * s, 0.014 * s), r=0.008 * s), 0.004)
    for i in range(4):
        a = i * math.tau / 4 + math.pi / 4
        p = V(math.cos(a), math.sin(a), 0) * 0.052 * s
        cage.add(F.RoundCone(c + p - V(0, 0, 0.075) * s, c + p + V(0, 0, 0.08) * s, 0.008 * s, 0.008 * s), 0.004)
    out.append(_m(cage, 0.0025, coll, M[fitting], 'Lantern cage'))
    gl = F.Field('Lantern glass')
    gl.add(F.Box(c, (0.045 * s, 0.045 * s, 0.075 * s), r=0.012 * s), 0.0)
    out.append(_m(gl, 0.003, coll, M[glass], 'Lantern glass', 1))
    fl = F.Field('Lantern flame')
    fl.add(F.RoundCone(c - V(0, 0, 0.02) * s, c + V(0, 0, 0.045) * s, 0.022 * s, 0.004 * s), 0.0)
    out.append(_m(fl, 0.002, coll, M[glow], 'Lantern flame', 1))
    return out


def heater_shield(M, coll, size=1.0, curve=0.16, emblem='lantern', face='paint', rim='trim', boss=False,
                  emblem_mat='gold', back='wood', field2=None, studs=True):
    """Heater shield: curved face, a raised brass rim, the faction emblem and rim studs. Face toward +X
    (the shield's own frame: width along Y, height along Z)."""
    s = size
    W, H = 0.3 * s, 0.4 * s
    out = []

    def outline(P):
        y, z = P[:, 1] / W, P[:, 2] / H
        # flat top, curved sides converging to a point at the bottom
        top = z - 0.62
        side = np.abs(y) - np.where(z > -0.05, 1.0, np.sqrt(np.clip(1 - ((z + 0.05) / 1.0) ** 2, 0, 1)) * 1.0)
        bot = -1.05 - z + np.abs(y) * 0.2
        return np.maximum.reduce([top * H, side * W, bot * H])

    def surface(P):
        # cylindrical curve about Z: x = -curve * y^2
        return P[:, 0] + curve * (P[:, 1] / W) ** 2 * W
    lo, hi = np.array([-0.2 * s, -W - 0.05, -H * 1.1 - 0.05]), np.array([0.1 * s, W + 0.05, H * 0.7 + 0.05])
    f = F.Field('Shield face')
    f.add(F.Fn(lambda P: np.maximum(np.abs(surface(P)) - 0.018 * s, outline(P)), lo, hi), 0.0)
    out.append(_m(f, 0.004 * s, coll, M[face], 'Shield face'))
    r = F.Field('Shield rim')
    r.add(F.Fn(lambda P: np.maximum(np.abs(surface(P) - 0.004) - 0.026 * s, np.abs(outline(P) + 0.016 * s) - 0.016 * s),
               lo, hi), 0.0)
    out.append(_m(r, 0.0035 * s, coll, M[rim], 'Shield rim'))
    if emblem:
        shape = sigil.EMBLEMS[emblem]() if isinstance(emblem, str) else emblem
        e = F.Field('Shield emblem')
        sc = 0.62 * W

        def efn(P):
            u, v = P[:, 1] / sc, (P[:, 2] + 0.12 * H) / sc
            return np.maximum(shape(u, v) * sc, np.abs(surface(P) - 0.022 * s) - 0.006 * s)
        e.add(F.Fn(efn, lo, hi), 0.0)
        out.append(_m(e, 0.003 * s, coll, M[emblem_mat], 'Shield emblem', 1))
    if studs:
        st = F.Field('Shield studs')
        for (yy, zz) in [(-0.84, 0.5), (0.84, 0.5), (-0.9, 0.0), (0.9, 0.0), (-0.66, -0.55), (0.66, -0.55), (0, -0.98),
                         (-0.4, 0.55), (0.4, 0.55)]:
            p = V(-curve * yy ** 2 * W + 0.03 * s, yy * W, zz * H)
            st.add(F.Sphere(p, 0.012 * s), 0.0)
        out.append(_m(st, 0.0025 * s, coll, M[rim], 'Shield studs', 1))
    if boss:
        b = F.Field('Shield boss')
        b.add(F.Ellipsoid(V(0.02 * s, 0, -0.05 * H), (0.04 * s, 0.075 * s, 0.075 * s)), 0.0)
        out.append(_m(b, 0.003 * s, coll, M['steel'], 'Shield boss'))
    bk = F.Field('Shield straps')
    bk.add(F.Box(V(-0.05 * s, 0, 0.0), (0.012, 0.18 * s, 0.025), r=0.006), 0.0)
    out.append(_m(bk, 0.004, coll, M['leather_dark'], 'Shield straps'))
    return dict(objs=out, kind='shield')


def round_shield(M, coll, radius=0.32, face='paint', rim='dark_steel', emblem=None, emblem_mat='trim', boss='steel',
                 planks=True, wood='wood'):
    out = []
    R = radius
    lo, hi = np.array([-0.12, -R - 0.05, -R - 0.05]), np.array([0.12, R + 0.05, R + 0.05])

    def dome(P):
        r = np.hypot(P[:, 1], P[:, 2]) / R
        return P[:, 0] + 0.06 * R * r * r
    f = F.Field('Round shield')
    f.add(F.Fn(lambda P: np.maximum(np.abs(dome(P)) - 0.018, np.hypot(P[:, 1], P[:, 2]) - R), lo, hi), 0.0)
    if planks:
        f.disp = lambda P: -0.003 * (np.abs(np.sin(P[:, 1] / R * 9)) ** 8)
    out.append(_m(f, 0.004, coll, M[face], 'Round shield'))
    r = F.Field('Shield rim')
    r.add(F.Fn(lambda P: np.maximum(np.abs(dome(P) - 0.004) - 0.026, np.abs(np.hypot(P[:, 1], P[:, 2]) - R + 0.014) - 0.016),
               lo, hi), 0.0)
    out.append(_m(r, 0.0035, coll, M[rim], 'Shield rim'))
    if boss:
        b = F.Field('Shield boss')
        b.add(F.Ellipsoid(V(0.03, 0, 0), (0.05, 0.085, 0.085)), 0.0)
        out.append(_m(b, 0.003, coll, M[boss], 'Shield boss'))
    if emblem:
        shape = sigil.EMBLEMS[emblem]() if isinstance(emblem, str) else emblem
        e = F.Field('Shield emblem')
        sc = 0.62 * R
        e.add(F.Fn(lambda P: np.maximum(shape(P[:, 1] / sc, P[:, 2] / sc) * sc, np.abs(dome(P) - 0.022) - 0.006), lo, hi), 0.0)
        out.append(_m(e, 0.003, coll, M[emblem_mat], 'Shield emblem', 1))
    return dict(objs=out, kind='shield')


def tower_shield(M, coll, width=0.52, height=1.05, face='paint', rim='dark_steel', emblem=None, emblem_mat='trim',
                 studs=True, bands=2, curve=0.06):
    out = []
    W, H = width / 2, height / 2
    lo, hi = np.array([-0.15, -W - 0.05, -H - 0.05]), np.array([0.12, W + 0.05, H + 0.05])
    surf = lambda P: P[:, 0] + curve * (P[:, 1] / W) ** 2
    rect = lambda P: np.maximum(np.abs(P[:, 1]) - W, np.abs(P[:, 2]) - H)
    f = F.Field('Tower shield')
    f.add(F.Fn(lambda P: np.maximum(np.abs(surf(P)) - 0.02, rect(P) + 0.004), lo, hi), 0.0)
    f.disp = lambda P: -0.0025 * (np.abs(np.sin(P[:, 1] / W * 6.5)) ** 10)
    out.append(_m(f, 0.005, coll, M[face], 'Tower shield'))
    r = F.Field('Tower rim')
    r.add(F.Fn(lambda P: np.maximum(np.abs(surf(P) - 0.004) - 0.03, np.abs(rect(P) + 0.02) - 0.02), lo, hi), 0.0)
    for i in range(bands):
        z = (i + 1) / (bands + 1) * 2 * H - H
        r.add(F.Fn(lambda P, z=z: np.maximum.reduce([np.abs(surf(P) - 0.004) - 0.028, np.abs(P[:, 2] - z) - 0.022,
                                                    np.abs(P[:, 1]) - W]), lo, hi), 0.0)
    out.append(_m(r, 0.004, coll, M[rim], 'Tower rim'))
    if emblem:
        shape = sigil.EMBLEMS[emblem]() if isinstance(emblem, str) else emblem
        e = F.Field('Tower emblem')
        sc = 0.75 * W
        e.add(F.Fn(lambda P: np.maximum(shape(P[:, 1] / sc, P[:, 2] / sc) * sc, np.abs(surf(P) - 0.024) - 0.006), lo, hi),
              0.0)
        out.append(_m(e, 0.0035, coll, M[emblem_mat], 'Tower emblem', 1))
    if studs:
        st = F.Field('Tower studs')
        for sy in (-1, 1):
            for k in range(6):
                z = -H + 0.06 + k * (2 * H - 0.12) / 5
                st.add(F.Sphere(V(-curve + 0.035, sy * (W - 0.02), z), 0.012), 0.0)
        out.append(_m(st, 0.003, coll, M['trim'], 'Tower studs', 1))
    return dict(objs=out, kind='shield')


def bow(M, coll, height=1.3, recurve=0.08, wood='wood', grip_mat='grip', string='string', tips='trim'):
    """Recurve bow in the X-Z plane, grip at the origin, string on -X (toward the archer)."""
    out = []
    h = height / 2
    pts = []
    for i in range(13):
        t = i / 12 * 2 - 1
        z = t * h
        x = 0.1 * (1 - t * t) - recurve * max(0, abs(t) - 0.8) / 0.2 * 1.0
        pts.append(V(x * 1.2 - 0.02, 0, z))
    f = F.Field('Bow')
    radii = [0.012 + 0.012 * (1 - abs(i / 6 - 1)) for i in range(13)]
    f.add(F.Chain(pts, radii, squash=(0.8, 1.0)), 0.0)
    out.append(_m(f, 0.0032, coll, M[wood], 'Bow'))
    g = F.Field('Bow grip')
    _grip(g, -0.07, 0.07, 0.026)
    g.ops = [(m, F.Moved(p, np.array([[1, 0, 0, pts[6].x], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1.0]])), k, t, d)
             for m, p, k, t, d in g.ops]
    out.append(_m(g, 0.0025, coll, M[grip_mat], 'Bow grip'))
    s = F.Field('Bowstring')
    s.add(F.RoundCone(pts[0], pts[-1], 0.0035, 0.0035), 0.0)
    out.append(_m(s, 0.0018, coll, M[string], 'Bowstring', 1))
    nock = V(pts[0].x, 0, 0)
    return dict(objs=out, tip=V(0.1, 0, 0), grip2=nock, kind='bow', nock=nock, grip_r=0.026)


def arrow(M, coll, length=0.8, fletch=None, head='blade', shaft='wood'):
    out = []
    f = F.Field('Arrow shaft')
    f.add(F.RoundCone(V(0, 0, 0), V(0, 0, length - 0.05), 0.008, 0.008), 0.0)
    out.append(_m(f, 0.002, coll, M[shaft], 'Arrow shaft'))
    h = F.Field('Arrowhead')
    h.add(F.Fn(_blade_fn(length - 0.07, 0.09, 0.05, 0.012, tip=0.6, taper=0.0, leaf=0.3, fuller=0),
               (-0.05, -0.02, length - 0.1), (0.05, 0.02, length + 0.03)), 0.0)
    out.append(_m(h, 0.0018, coll, M[head], 'Arrowhead', 1))
    fl = F.Field('Fletching')
    for a in (0, 120, 240):
        r = math.radians(a)
        d = V(math.cos(r), math.sin(r), 0)
        fl.add(F.Box(V(0, 0, 0.08) + d * 0.02, (0.018, 0.002, 0.055), Matrix.Rotation(r, 3, 'Z'), r=0.002), 0.0)
    out.append(_m(fl, 0.0018, coll, fletch or M['linen'], 'Fletching'))
    return dict(objs=out, tip=V(0, 0, length), kind='arrow')


def crossbow(M, coll, wood='wood', steel='dark_steel', fitting='trim'):
    """Stock along +Z (bolt toward +Z), prod across Y at the front."""
    out = []
    f = F.Field('Crossbow stock')
    f.add(F.Box(V(0, 0, 0.12), (0.035, 0.03, 0.3), r=0.015), 0.0)
    f.add(F.RoundCone(V(-0.02, 0, -0.16), V(-0.06, 0, -0.3), 0.04, 0.05, squash=(0.6, 1.0), up=(1, 0, 0)), 0.03)
    out.append(_m(f, 0.004, coll, M[wood], 'Crossbow stock'))
    p = F.Field('Crossbow prod')
    pts = [V(0.0, -0.32, 0.36), V(0.02, -0.16, 0.41), V(0.03, 0, 0.42), V(0.02, 0.16, 0.41), V(0.0, 0.32, 0.36)]
    p.add(F.Chain(pts, [0.008, 0.014, 0.018, 0.014, 0.008], squash=(1.0, 0.6)), 0.0)
    out.append(_m(p, 0.0028, coll, M[steel], 'Crossbow prod'))
    k = F.Field('Crossbow fittings')
    k.add(F.Box(V(0.0, 0, 0.42), (0.04, 0.04, 0.025), r=0.008), 0.0)
    k.add(F.RoundCone(V(-0.035, 0, 0.0), V(-0.07, 0, 0.06), 0.008, 0.006), 0.004)
    out.append(_m(k, 0.0025, coll, M[fitting], 'Crossbow fittings'))
    s = F.Field('Crossbow string')
    s.add(F.Chain([pts[0], V(0.0, 0, 0.12), pts[-1]], 0.003), 0.0)
    out.append(_m(s, 0.0016, coll, M['string'], 'Crossbow string', 1))
    b = F.Field('Bolt')
    b.add(F.RoundCone(V(0.04, 0, 0.12), V(0.04, 0, 0.5), 0.008, 0.008), 0.0)
    b.add(F.RoundCone(V(0.04, 0, 0.5), V(0.04, 0, 0.56), 0.014, 0.002), 0.003)
    bolt = _m(b, 0.002, coll, M[steel], 'Bolt')
    out.append(bolt)
    return dict(objs=out, tip=V(0.04, 0, 0.56), grip2=V(0.0, 0, 0.3), kind='crossbow', grip_r=0.03)


def dagger(M, coll, length=0.3, curve=0.0, blade='blade', hilt='hilt'):
    out = []
    f = F.Field('Dagger')
    f.add(F.Fn(_blade_fn(0.06, length, 0.05, 0.016, tip=0.35, taper=0.25, curve=curve), (-0.1, -0.03, 0.03),
               (0.1, 0.03, length + 0.1)), 0.0)
    out.append(_m(f, 0.0022, coll, M[blade], 'Dagger', 1))
    h = F.Field('Dagger hilt')
    h.add(F.Chain([V(-0.05, 0, 0.055), V(0, 0, 0.05), V(0.05, 0, 0.055)], 0.012), 0.0)
    h.add(F.Sphere(V(0, 0, -0.065), 0.022), 0.0)
    out.append(_m(h, 0.0022, coll, M[hilt], 'Dagger hilt'))
    g = F.Field('Dagger grip')
    _grip(g, -0.05, 0.045, 0.018)
    out.append(_m(g, 0.002, coll, M['grip'], 'Dagger grip'))
    return dict(objs=out, tip=V(curve * length, 0, 0.06 + length), grip2=None, kind='dagger', grip_r=0.018)


def flame(M, coll, base, size=1.0, color='glow', name='Hand flame'):
    """A cupped magical flame rising from a point (licking tongues)."""
    s = size
    f = F.Field(name)
    f.add(F.RoundCone(base, base + V(0, 0, 0.16 * s), 0.05 * s, 0.006 * s), 0.0)
    for i, (dx, dy, h) in enumerate([(0.03, 0.02, 0.12), (-0.03, -0.02, 0.1), (0.0, 0.035, 0.09), (0.02, -0.03, 0.11)]):
        f.add(F.RoundCone(base + V(dx, dy, 0.0) * s, base + V(dx * 1.6, dy * 1.6, h) * s, 0.032 * s, 0.004 * s),
              0.02 * s)
    f.disp = lambda P: F.noise3(P, 40.0, 3, 2) * 0.004 * s
    return [_m(f, 0.003 * s, coll, M[color] if isinstance(color, str) else color, name, 1)]


def polearm(M, coll, kind='halberd', length=2.1, grip_at=0.3, wood='wood', steel='blade', fitting='trim'):
    """Halberd, bill, poleaxe or pike on a long haft (grip at the origin, head at +Z)."""
    out = []
    z0 = -length * grip_at
    z1 = z0 + length
    f = F.Field('Polearm haft')
    f.add(F.RoundCone(V(0, 0, z0), V(0, 0, z1 - 0.06), 0.025, 0.022), 0.0)
    out.append(_m(f, 0.004, coll, M[wood], 'Polearm haft'))
    h = F.Field('Polearm head')
    if kind == 'pike':
        h.add(F.Fn(_blade_fn(z1 - 0.2, 0.24, 0.07, 0.022, tip=0.5, taper=0.0, leaf=0.3, fuller=0),
                   (-0.08, -0.04, z1 - 0.25), (0.08, 0.04, z1 + 0.05)), 0.0)
        h.add(F.RoundCone(V(0, 0, z1 - 0.32), V(0, 0, z1 - 0.18), 0.03, 0.02), 0.01)
        tip = V(0, 0, z1 + 0.04)
    elif kind == 'halberd':
        zc = z1 - 0.3
        # axe blade forward (+X), back spike, top spear point
        h.add(F.Fn(lambda P: np.maximum.reduce([np.abs(P[:, 2] - zc) - (0.06 + 0.1 * np.clip(P[:, 0] / 0.2, 0, 1)),
                                                np.abs(P[:, 1]) - 0.012 * (1.2 - np.clip(P[:, 0] / 0.2, 0, 1)),
                                                -P[:, 0], P[:, 0] - 0.2 - 0.03 * np.cos(np.clip((P[:, 2] - zc) / 0.16, -1, 1) * 1.5)]),
                   (-0.05, -0.05, zc - 0.2), (0.3, 0.05, zc + 0.2)), 0.0)
        h.add(F.RoundCone(V(0, 0, zc), V(-0.15, 0, zc - 0.03), 0.025, 0.003), 0.01)
        h.add(F.Fn(_blade_fn(zc + 0.12, 0.26, 0.06, 0.02, tip=0.5, taper=0.0, fuller=0), (-0.06, -0.03, zc + 0.08),
                   (0.06, 0.03, zc + 0.42)), 0.01)
        h.add(F.Box(V(0, 0, zc), (0.03, 0.03, 0.09), r=0.01), 0.01)
        tip = V(0.2, 0, zc)
    elif kind == 'bill':
        zc = z1 - 0.28
        h.add(F.Fn(lambda P: np.maximum.reduce([np.abs(P[:, 2] - zc - 0.04 * np.clip(P[:, 0] / 0.12, 0, 1)) - 0.075,
                                                np.abs(P[:, 1]) - 0.011, -P[:, 0], P[:, 0] - 0.13]),
                   (-0.05, -0.05, zc - 0.15), (0.2, 0.05, zc + 0.15)), 0.0)
        h.add(F.Chain([V(0.12, 0, zc + 0.1), V(0.16, 0, zc + 0.04), V(0.13, 0, zc - 0.04)], [0.016, 0.012, 0.004],
                      squash=(1.0, 0.5), up=(0, 1, 0)), 0.01)
        h.add(F.Fn(_blade_fn(zc + 0.08, 0.22, 0.05, 0.018, tip=0.5, taper=0.0, fuller=0), (-0.05, -0.03, zc + 0.04),
                   (0.05, 0.03, zc + 0.34)), 0.01)
        h.add(F.RoundCone(V(0, 0, zc - 0.02), V(-0.11, 0, zc + 0.04), 0.02, 0.003), 0.01)
        tip = V(0.16, 0, zc + 0.04)
    else:  # poleaxe: hammer face, back fluke, top spike
        zc = z1 - 0.22
        h.add(F.Box(V(0.06, 0, zc), (0.06, 0.035, 0.035), r=0.008), 0.0)
        h.add(F.Box(V(0.12, 0, zc), (0.012, 0.045, 0.045), r=0.004), 0.004)
        h.add(F.RoundCone(V(0, 0, zc), V(-0.15, 0, zc - 0.04), 0.03, 0.003), 0.01)
        h.add(F.Fn(_blade_fn(zc + 0.05, 0.24, 0.05, 0.02, tip=0.6, taper=0.0, fuller=0), (-0.05, -0.03, zc),
                   (0.05, 0.03, zc + 0.32)), 0.01)
        h.add(F.Box(V(0, 0, zc), (0.03, 0.03, 0.06), r=0.01), 0.01)
        tip = V(0.13, 0, zc)
    out.append(_m(h, 0.003, coll, M[steel], 'Polearm head', 1))
    c = F.Field('Polearm fittings')
    for z in (z1 - (0.42 if kind != 'pike' else 0.34), -0.12, 0.5):
        c.add(F.Torus(V(0, 0, z), 0.026, 0.008), 0.0)
    c.add(F.RoundCone(V(0, 0, z0 - 0.02), V(0, 0, z0 + 0.04), 0.024, 0.028), 0.0)
    out.append(_m(c, 0.0028, coll, M[fitting], 'Polearm fittings'))
    return dict(objs=out, tip=tip, grip2=V(0, 0, 0.5), kind=kind, grip_r=0.025)


def buckler(M, coll, radius=0.17, face='steel', rim='trim', boss='trim'):
    out = []
    R = radius
    lo, hi = np.array([-0.08, -R - 0.03, -R - 0.03]), np.array([0.08, R + 0.03, R + 0.03])

    def dome(P):
        r = np.hypot(P[:, 1], P[:, 2]) / R
        return P[:, 0] + 0.1 * R * r * r
    f = F.Field('Buckler')
    f.add(F.Fn(lambda P: np.maximum(np.abs(dome(P)) - 0.012, np.hypot(P[:, 1], P[:, 2]) - R), lo, hi), 0.0)
    out.append(_m(f, 0.003, coll, M[face], 'Buckler'))
    r = F.Field('Buckler rim')
    r.add(F.Fn(lambda P: np.maximum(np.abs(dome(P) - 0.002) - 0.018, np.abs(np.hypot(P[:, 1], P[:, 2]) - R + 0.008) - 0.009),
               lo, hi), 0.0)
    r.add(F.Ellipsoid(V(0.03, 0, 0), (0.035, 0.05, 0.05)), 0.0)
    out.append(_m(r, 0.0028, coll, M[rim], 'Buckler rim'))
    return dict(objs=out, kind='shield')


def rapier(M, coll, length=0.95):
    out = []
    f = F.Field('Rapier blade')
    f.add(F.Fn(_blade_fn(0.1, length, 0.035, 0.014, tip=0.15, taper=0.35, fuller=0), (-0.05, -0.03, 0.05),
               (0.05, 0.03, length + 0.15)), 0.0)
    out.append(_m(f, 0.0022, coll, M['blade'], 'Rapier blade', 1))
    h = F.Field('Rapier hilt')
    h.add(F.Chain([V(-0.1, 0, 0.12), V(0, 0, 0.09), V(0.1, 0, 0.12)], 0.009), 0.0)
    h.add(F.Chain([V(0.0, 0, 0.09), V(0.07, -0.05, 0.04), V(0.04, -0.05, -0.08), V(0.0, 0, -0.1)], 0.008), 0.004)
    h.add(F.Chain([V(0.0, 0, 0.09), V(0.07, 0.05, 0.04), V(0.04, 0.05, -0.08), V(0.0, 0, -0.1)], 0.008), 0.004)
    h.add(F.Sphere(V(0, 0, -0.1), 0.022), 0.0)
    out.append(_m(h, 0.0022, coll, M['hilt'], 'Rapier hilt'))
    g = F.Field('Rapier grip')
    _grip(g, -0.08, 0.08, 0.017)
    out.append(_m(g, 0.002, coll, M['grip'], 'Rapier grip'))
    return dict(objs=out, tip=V(0, 0, length + 0.1), grip2=None, kind='sword', grip_r=0.017)


def standard(M, coll, cloth, emblem='lantern', emblem_mat='gold', height=2.4, grip_at=0.32, width=0.5, drop=0.7,
             trim='gold', finial='lantern', tatter=0.0, seed=3):
    """A war standard: pole with a crossbar and a hanging banner (faces -Y)."""
    out = []
    z0 = -height * grip_at
    z1 = z0 + height
    p = F.Field('Standard pole')
    p.add(F.RoundCone(V(0, 0, z0), V(0, 0, z1), 0.024, 0.02), 0.0)
    p.add(F.RoundCone(V(-0.03, 0, z1 - 0.06), V(width + 0.06, 0, z1 - 0.06), 0.014, 0.014), 0.0)
    out.append(_m(p, 0.004, coll, M['wood'], 'Standard pole'))
    k = F.Field('Standard finial')
    if finial == 'lantern':
        out += lantern(M, coll, V(0, 0, z1), hang=False, size=0.9)
    else:
        k.add(F.RoundCone(V(0, 0, z1), V(0, 0, z1 + 0.16), 0.03, 0.002), 0.0)
    k.add(F.Torus(V(0, 0, z1 - 0.1), 0.024, 0.008), 0.0)
    out.append(_m(k, 0.003, coll, M[trim or 'trim'], 'Standard finial'))
    x0, zt = 0.02, z1 - 0.08
    rng = np.random.default_rng(seed)
    ph = rng.uniform(0, 6.28, 3)

    def wave(P):
        u = (P[:, 0] - x0) / width
        v = (zt - P[:, 2]) / drop
        return 0.03 * np.sin(u * 6 + v * 2.5) * (0.3 + v)

    def cloth_d(P):
        u = (P[:, 0] - x0) / width
        v = (zt - P[:, 2]) / drop
        hem = 1.0 - 0.2 * np.clip(1 - np.abs(u - 0.5) * 2, 0, 1) - tatter * 0.08 * np.abs(np.sin(u * 13 + ph[0]))
        return np.maximum.reduce([np.abs(P[:, 1] - wave(P)) - 0.008, -u * width, (u - 1) * width, -v * drop,
                                  (v - hem) * drop])
    lo = np.array([x0 - 0.05, -0.12, zt - drop - 0.05])
    hi = np.array([x0 + width + 0.05, 0.12, zt + 0.05])
    c = F.Field('Standard banner')
    c.add(F.Fn(cloth_d, lo, hi), 0.0)
    out.append(_m(c, 0.004, coll, cloth, 'Standard banner'))
    if trim:
        t = F.Field('Standard trim')
        t.add(F.Fn(lambda P: np.maximum(np.abs(P[:, 1] - wave(P)) - 0.011, np.abs(cloth_d(P) + 0.012) - 0.012 +
                                        0 * P[:, 0]), lo, hi), 0.0)
        out.append(_m(t, 0.003, coll, M[trim], 'Standard trim', 1))
    if emblem:
        shape = sigil.EMBLEMS[emblem]()
        sc = width * 0.36
        cx, cz = x0 + width * 0.5, zt - drop * 0.42
        e = F.Field('Standard emblem')
        e.add(F.Fn(lambda P: np.maximum(shape((P[:, 0] - cx) / sc, (P[:, 2] - cz) / sc) * sc,
                                        np.abs(P[:, 1] - wave(P) + 0.009) - 0.004), lo, hi), 0.0)
        out.append(_m(e, 0.003, coll, M[emblem_mat], 'Standard emblem', 1))
    return dict(objs=out, tip=V(0, 0, z1), grip2=V(0, 0, 0.45), kind='standard', grip_r=0.024)


def flask(M, coll, size=1.0, liquid='glow', glass='lamp_glass', cork='wood'):
    s = size
    out = []
    g = F.Field('Flask')
    g.add(F.Sphere(V(0, 0, 0.05 * s), 0.05 * s), 0.0)
    g.add(F.RoundCone(V(0, 0, 0.08 * s), V(0, 0, 0.14 * s), 0.02 * s, 0.016 * s), 0.01 * s)
    out.append(_m(g, 0.002 * s, coll, M[glass], 'Flask', 1))
    l = F.Field('Potion')
    l.add(F.Sphere(V(0, 0, 0.045 * s), 0.042 * s), 0.0)
    l.keep(F.Plane(V(0, 0, 0.065 * s), (0, 0, 1)), 0.002)
    out.append(_m(l, 0.002 * s, coll, M[liquid] if isinstance(liquid, str) else liquid, 'Potion liquid', 1))
    c = F.Field('Cork')
    c.add(F.RoundCone(V(0, 0, 0.135 * s), V(0, 0, 0.165 * s), 0.016 * s, 0.018 * s), 0.0)
    out.append(_m(c, 0.002 * s, coll, M[cork], 'Cork'))
    return dict(objs=out, tip=V(0, 0, 0.1 * s), grip2=None, kind='flask', grip_r=0.03)


def bomb(M, coll, size=1.0, fuse_glow='glow'):
    s = size
    out = []
    b = F.Field('Bomb')
    b.add(F.Sphere(V(0, 0, 0.06 * s), 0.065 * s), 0.0)
    b.add(F.RoundCone(V(0, 0, 0.11 * s), V(0, 0, 0.14 * s), 0.02 * s, 0.02 * s), 0.005)
    out.append(_m(b, 0.0025 * s, coll, M['dark_steel'], 'Bomb'))
    fz = F.Field('Fuse')
    fz.add(F.Chain([V(0, 0, 0.14 * s), V(0.02 * s, 0, 0.18 * s), V(0.01 * s, 0.01 * s, 0.21 * s)], 0.005 * s), 0.0)
    out.append(_m(fz, 0.0015 * s, coll, M['rope'], 'Fuse', 1))
    sp = F.Field('Fuse spark')
    sp.add(F.Sphere(V(0.01 * s, 0.01 * s, 0.215 * s), 0.014 * s), 0.0)
    out.append(_m(sp, 0.0015 * s, coll, M[fuse_glow], 'Fuse spark', 1))
    return dict(objs=out, tip=V(0, 0, 0.1 * s), grip2=None, kind='bomb', grip_r=0.04)


def tool_hammer(M, coll, length=0.5, head=1.0, wood='wood', steel='dark_steel'):
    out = []
    f = F.Field('Tool haft')
    f.add(F.RoundCone(V(0, 0, -0.1), V(0, 0, length * 0.7), 0.018, 0.016), 0.0)
    out.append(_m(f, 0.003, coll, M[wood], 'Tool haft'))
    h = F.Field('Tool head')
    c = V(0, 0, length * 0.7)
    h.add(F.Box(c, (0.07 * head, 0.026, 0.026), r=0.006), 0.0)
    h.add(F.RoundCone(c - V(0.06 * head, 0, 0), c - V(0.12 * head, 0, 0.03), 0.02, 0.004), 0.004)
    out.append(_m(h, 0.0025, coll, M[steel], 'Tool head'))
    return dict(objs=out, tip=c + V(0.07, 0, 0), grip2=None, kind='hammer', grip_r=0.018)


def bell(M, coll, size=1.0, mat='trim', clapper='dark_steel'):
    s = size
    out = []
    f = F.Field('Bell')
    f.add(F.RoundCone(V(0, 0, 0.0), V(0, 0, 0.22 * s), 0.16 * s, 0.07 * s), 0.0)
    f.cut(F.RoundCone(V(0, 0, -0.04 * s), V(0, 0, 0.18 * s), 0.145 * s, 0.055 * s), 0.004)
    f.add(F.Torus(V(0, 0, 0.0), 0.155 * s, 0.014 * s), 0.004)
    f.add(F.Torus(V(0, 0, 0.27 * s), 0.035 * s, 0.012 * s, Matrix.Rotation(math.radians(90), 3, 'X')), 0.004)
    out.append(_m(f, 0.003 * s, coll, M[mat], 'Bell'))
    c = F.Field('Clapper')
    c.add(F.RoundCone(V(0, 0, 0.18 * s), V(0, 0, 0.02 * s), 0.01 * s, 0.03 * s), 0.0)
    out.append(_m(c, 0.003 * s, coll, M[clapper], 'Clapper'))
    return dict(objs=out, kind='bell')
