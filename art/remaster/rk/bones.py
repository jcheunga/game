"""The Rotbound Host's dead: stylised skeletons and skulls sculpted as signed-distance fields.

`skeleton_field` builds the bones of a humanoid on the hero skeleton (rk/hero.py), each tagged with its
bone so it skins cleanly; `skull` builds the roster sheet's skull (deep sockets with soul-light, heavy
cheekbones, a toothy grin and a hinged jaw). Garments for the dead are grown from a thin human body
(hero.body_field with thin=1), so rags and rusted plate hang as if over flesh.
"""
import math

import numpy as np
from mathutils import Matrix, Vector

from . import sdf as F


def _bone(f, a, b, r, tag, knob=1.55, k=0.012, knobs=(True, True), squash=None):
    f.add(F.RoundCone(a, b, r, r * 0.92, squash=squash), k, tag)
    if knobs[0]:
        f.add(F.Sphere(a, r * knob), k, tag)
    if knobs[1]:
        f.add(F.Sphere(b, r * knob * 0.9), k, tag)


def skeleton_field(s, J, thick=1.45, ribs=6, hands=True, feet=True, broken=0.0, seed=4):
    """Bones on the hero skeleton. Returns a Field tagged with bone weights."""
    f = F.Field('Bones')
    f.reach = 0.05
    t = thick
    pel, sp, ch, nk = J['pelvis'], J['spine'], J['chest'], J['neck']
    # spine: a stack of vertebrae from the pelvis to the skull
    f.group = 'torso'
    n = 9
    for i in range(n):
        z = i / (n - 1)
        a = pel.lerp(nk, z) + Vector((-0.04 - 0.02 * math.sin(z * math.pi), 0, 0))
        bone = 'hips' if z < 0.2 else 'spine' if z < 0.5 else 'chest'
        f.add(F.Ellipsoid(a, (0.03 * t, 0.034 * t, 0.022 * t)), 0.01, {bone: 1})
        f.add(F.RoundCone(a, a + Vector((-0.04 * t, 0, -0.01)), 0.012 * t, 0.008 * t), 0.006, {bone: 1})
    # ribcage: curved ribs around a hollow chest
    cc = ch + Vector((0.0, 0, 0.06))
    for i in range(ribs):
        z = cc.z + 0.13 - i * 0.045
        w = (0.15 + 0.035 * math.sin((i + 1) / (ribs + 1) * math.pi)) * t
        depth = (0.12 + 0.02 * math.sin((i + 1) / (ribs + 1) * math.pi)) * t
        for sy in (-1, 1):
            pts = []
            for k in range(7):
                a = k / 6 * math.pi * 0.92
                pts.append(Vector((cc.x - 0.05 - math.cos(a) * depth * 0.55 + (0.07 * t if k == 6 else 0),
                                   sy * math.sin(a) * w, z - 0.03 * math.sin(a))))
            f.add(F.Chain(pts, [0.011 * t] * 7, squash=(1.0, 0.7)), 0.008, {'chest': 1})
    # sternum
    f.add(F.RoundCone(cc + Vector((0.11 * t, 0, 0.13)), cc + Vector((0.12 * t, 0, -0.1)), 0.02 * t, 0.014 * t,
                      squash=(1.0, 0.45), up=(1, 0, 0)), 0.01, {'chest': 1})
    # collarbones and shoulder blades
    for side, sy in (('R', -1), ('L', 1)):
        shj = J['shoulder.' + side]
        f.add(F.RoundCone(cc + Vector((0.09, sy * 0.03, 0.15)), shj + Vector((0.02, 0, 0.02)), 0.014 * t, 0.012 * t), 0.01,
              {'chest': .6, 'shoulder.' + side: .4})
        f.add(F.Ellipsoid(shj + Vector((-0.1, -sy * 0.06, -0.06)), (0.02 * t, 0.07 * t, 0.09 * t)), 0.01,
              {'chest': 1})
    # pelvis: two flared wings and a sacrum
    f.group = 'hips'
    for sy in (-1, 1):
        R = Matrix.Rotation(math.radians(sy * 25), 3, 'X')
        f.add(F.Ellipsoid(pel + Vector((-0.01, sy * 0.1, 0.02)), (0.07 * t, 0.06 * t, 0.075 * t), R), 0.02, {'hips': 1})
    f.add(F.Ellipsoid(pel + Vector((0.02, 0, -0.06)), (0.05 * t, 0.12 * t, 0.035 * t)), 0.03, {'hips': 1})
    f.cut(F.Ellipsoid(pel + Vector((0.05, 0, 0.01)), (0.04 * t, 0.07 * t, 0.06 * t)), 0.01)
    # limbs
    for side, sy in (('R', -1), ('L', 1)):
        f.group = 'arm.' + side
        sh, el, wr, ht = J['shoulder.' + side], J['elbow.' + side], J['wrist.' + side], J['hand_tip.' + side]
        f.add(F.Sphere(sh, 0.04 * t), 0.012, {'upper_arm.' + side: .7, 'shoulder.' + side: .3})
        _bone(f, sh, el, 0.018 * t, {'upper_arm.' + side: 1}, knobs=(False, True))
        for off in (-0.012, 0.012):
            f.add(F.RoundCone(el + Vector((0, off * t, 0)), wr + Vector((0.006, off * t * 1.2, 0)), 0.013 * t, 0.011 * t),
                  0.008, {'forearm.' + side: 1})
        f.add(F.Sphere(wr, 0.02 * t), 0.008, {'forearm.' + side: .5, 'hand.' + side: .5})
        f.group = 'leg.' + side
        hp, kn, an, toe = J['hip.' + side], J['knee.' + side], J['ankle.' + side], J['toe.' + side]
        f.add(F.Sphere(hp, 0.035 * t), 0.012, {'thigh.' + side: .7, 'hips': .3})
        _bone(f, hp, kn, 0.022 * t, {'thigh.' + side: 1}, knobs=(False, True), knob=1.7)
        f.add(F.Ellipsoid(kn + Vector((0.03, 0, 0)), (0.022 * t, 0.03 * t, 0.03 * t)), 0.008,
              {'shin.' + side: .5, 'thigh.' + side: .5})
        _bone(f, kn, an, 0.019 * t, {'shin.' + side: 1}, knobs=(False, True), knob=1.4)
        f.add(F.RoundCone(kn + Vector((-0.01, sy * 0.018, 0)), an + Vector((-0.01, sy * 0.022, 0.02)), 0.009 * t,
                          0.008 * t), 0.006, {'shin.' + side: 1})
        if feet:
            # a compact, chunky foot: heel, a solid tarsal block and three short toes held together
            f.group = 'foot.' + side
            tag = {'foot.' + side: 1}
            fwd = toe - an
            fwd.z = 0.0
            fwd = fwd.normalized() if fwd.length > 1e-4 else Vector((1, 0, 0))
            lat = Vector((0, 0, 1)).cross(fwd).normalized()
            down = Vector((0, 0, -1))
            L_ = max(0.12, (toe - an).length) * 0.85
            f.add(F.Ellipsoid(an - fwd * 0.03 + down * 0.05, (0.042 * t, 0.032 * t, 0.03 * t)), 0.012, tag)
            f.add(F.RoundCone(an + down * 0.035, an + fwd * L_ * 0.4 + down * 0.055, 0.03 * t, 0.026 * t,
                              squash=(1.0, 0.72), up=(0, 0, 1)), 0.012, tag)
            for k in (-1, 0, 1):
                y = lat * (k * 0.027 * t)
                a = an + fwd * L_ * 0.38 + y * 0.7 + down * 0.06
                m = an + fwd * L_ * 0.7 + y * 1.05 + down * 0.068
                b = an + fwd * (L_ * 0.95 - abs(k) * 0.02) + y * 1.15 + down * 0.074
                f.add(F.Chain([a, m, b], [0.0105 * t, 0.0095 * t, 0.009 * t]), 0.003, tag)
                f.add(F.Sphere(m, 0.0115 * t), 0.002, tag)
                f.add(F.Sphere(b, 0.011 * t), 0.002, tag)
    f.group = None
    return f


def bone_hand_field(size=1.0, pose='claw', thumb=-1):
    """Skeletal hand in the rk.hands local frame (wrist at origin, +Y to the fingertips)."""
    from .hands import POSES
    p = POSES[pose] if isinstance(pose, str) else pose
    s = size
    f = F.Field('Bone hand')
    f.add(F.Ellipsoid(Vector((0, 0.02 * s, 0)), (0.026 * s, 0.022 * s, 0.014 * s)), 0.0)
    lengths = [(0.042, 0.028, 0.022), (0.046, 0.03, 0.024), (0.043, 0.028, 0.022), (0.035, 0.022, 0.019)]
    for i in range(4):
        x = (-thumb) * (0.026 - i * 0.017) * s
        k = Vector((x * 0.6, 0.03 * s, 0))
        mc = Vector((x, 0.085 * s, 0.002 * s))
        f.add(F.RoundCone(k, mc, 0.006 * s, 0.006 * s), 0.003 * s)
        f.add(F.Sphere(mc, 0.009 * s), 0.003 * s)
        d = Vector((0, 1, 0))
        pts = [mc]
        ang = 0.0
        for j in range(3):
            ang += math.radians(p['curl'][j])
            dj = d * math.cos(ang) + Vector((0, 0, -1)) * math.sin(ang)
            pts.append(pts[-1] + dj * lengths[i][j] * s)
        for a, b in zip(pts, pts[1:]):
            f.add(F.RoundCone(a, b, 0.0065 * s, 0.0055 * s), 0.002 * s)
            f.add(F.Sphere(b, 0.0075 * s), 0.002 * s)
        tip = pts[-1]
        f.add(F.RoundCone(tip, tip + (pts[-1] - pts[-2]).normalized() * 0.014 * s, 0.006 * s, 0.001 * s), 0.002 * s)
    t0 = Vector(((-thumb) * -0.024 * s, 0.018 * s, -0.008 * s))
    ab = math.radians(p['thumb'][0])
    tdir = Vector((-(-thumb) * math.cos(ab) * 0.6, math.sin(ab) * 0.9 + 0.4, -0.35)).normalized()
    f.add(F.Chain([t0, t0 + tdir * 0.04 * s, t0 + tdir * 0.07 * s], [0.007 * s, 0.006 * s, 0.005 * s]), 0.002 * s)
    return f, Vector((0, 0.07 * s, -0.03 * s))


def skull(C, u, mats, coll, jaw_open=0.15, glow=True, crack=0.0, tilt_jaw=0.0, brow=1.0, teeth=1.0, gaunt=1.0):
    """The roster sheet's skull: big cranium, deep sockets with soul-light, heavy cheekbones and a grin.
    C, u as rk.faces (u about 1/2.2 of chin-to-crown). Returns mesh objects for the head bone."""
    P = lambda x, y, z: C + Vector((x * u, y * u, z * u))
    out = []
    f = F.Field('Skull')
    f.reach = 0.06
    # cranium
    f.add(F.Ellipsoid(P(-0.12, 0, 0.18), (0.9 * u, 0.8 * u, 0.88 * u)), 0.0)
    # temples pinched, cheekbones flaring
    for sy in (-1, 1):
        f.cut(F.Ellipsoid(P(0.45, sy * 0.85, 0.0), (0.3 * u, 0.18 * u, 0.3 * u)), 0.1 * u)
        f.add(F.Ellipsoid(P(0.52, sy * 0.52, -0.22), (0.24 * u, 0.2 * u, 0.15 * u)), 0.1 * u)
        f.add(F.RoundCone(P(0.45, sy * 0.58, -0.25), P(0.0, sy * 0.72, -0.2), 0.08 * u, 0.06 * u), 0.06 * u)
    # face plate / maxilla
    f.add(F.Ellipsoid(P(0.55, 0, -0.32), (0.4 * u, 0.42 * u, 0.42 * u)), 0.18 * u)
    # brow ridge
    f.add(F.Chain([P(0.62, -0.55, 0.24), P(0.85, -0.25, 0.2), P(0.9, 0, 0.14), P(0.85, 0.25, 0.2), P(0.62, 0.55, 0.24)],
                  0.1 * u * brow, squash=(1.0, 0.8)), 0.1 * u)
    # deep sockets
    socks = []
    for sy in (-1, 1):
        sc = P(0.82, sy * 0.32, -0.02)
        socks.append(sc)
        f.cut(F.Ellipsoid(sc, (0.3 * u, 0.24 * u, 0.25 * u)), 0.05 * u)
    # nasal cavity: inverted heart
    f.cut(F.Ellipsoid(P(1.0, -0.05, -0.36), (0.18 * u, 0.06 * u, 0.13 * u)), 0.02 * u)
    f.cut(F.Ellipsoid(P(1.0, 0.05, -0.36), (0.18 * u, 0.06 * u, 0.13 * u)), 0.02 * u)
    # upper jaw: cut under the face for the teeth row
    f.cut(F.Ellipsoid(P(0.72, 0, -0.78), (0.4 * u, 0.5 * u, 0.13 * u)), 0.03 * u)
    if crack:
        f.disp = lambda Q: F.noise3(Q, 12.0, 4, 2) * 0.004 * crack
    out.append(f.mesh(u * 0.022, coll, mats['bone'], name='Skull', smooth=3))
    # sockets: darkness and the soul-light
    if mats.get('dark') is not None:
        g = F.Field('Socket dark')
        for sc in socks:
            g.add(F.Ellipsoid(sc + Vector((-0.1 * u, 0, 0)), (0.18 * u, 0.19 * u, 0.19 * u)), 0.0)
        out.append(g.mesh(u * 0.03, coll, mats['dark'], name='Socket dark', smooth=2))
    if glow and mats.get('glow') is not None:
        e = F.Field('Soul light')
        for sc in socks:
            e.add(F.Sphere(sc + Vector((0.03 * u, 0, -0.01 * u)), 0.085 * u), 0.0)
        out.append(e.mesh(u * 0.012, coll, mats['glow'], name='Soul light', smooth=1))
    # teeth: upper row
    t = F.Field('Teeth')
    for i in range(10):
        a = (i - 4.5) / 4.5 * 0.95
        p = P(0.8 * math.cos(a) + 0.08, 0.48 * math.sin(a), -0.72)
        R = Matrix.Rotation(a, 3, 'Z')
        t.add(F.Box(p, (0.055 * u, 0.045 * u * teeth, 0.085 * u), R, r=0.02 * u), 0.004 * u)
    # mandible with its own lower teeth, hinged open
    jo = jaw_open
    jaw = F.Field('Mandible')
    hinge = P(0.05, 0, -0.45)
    Rj = Matrix.Rotation(math.radians(18 * jo), 3, 'Y')
    J = lambda x, y, z: hinge + Rj @ (P(x, y, z) - hinge)
    for sy in (-1, 1):
        jaw.add(F.RoundCone(J(0.05, sy * 0.6, -0.42), J(0.12, sy * 0.58, -0.82), 0.09 * u, 0.1 * u), 0.05 * u)
        jaw.add(F.RoundCone(J(0.12, sy * 0.58, -0.86), J(0.7, sy * 0.22, -1.0), 0.1 * u, 0.12 * u), 0.06 * u)
    jaw.add(F.Ellipsoid(J(0.72, 0, -1.0), (0.18 * u, 0.28 * u, 0.13 * u)), 0.08 * u)
    jaw.cut(F.Ellipsoid(J(0.6, 0, -0.84), (0.42 * u, 0.42 * u, 0.1 * u)), 0.03 * u)
    out.append(jaw.mesh(u * 0.022, coll, mats['bone'], name='Mandible', smooth=3))
    for i in range(8):
        a = (i - 3.5) / 3.5 * 0.85
        p = J(0.72 * math.cos(a) + 0.06, 0.42 * math.sin(a), -0.88)
        R = Matrix.Rotation(a, 3, 'Z')
        t.add(F.Box(p, (0.05 * u, 0.042 * u * teeth, 0.07 * u), R, r=0.018 * u), 0.004 * u)
    out.append(t.mesh(u * 0.012, coll, mats['bone'], name='Teeth', smooth=2))
    return out
