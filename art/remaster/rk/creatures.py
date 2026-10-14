"""Non-humanoid Host creatures in the roster-sheet style: bone nests, fungus sacs, swamp maws, the reliquary
eye, mirror wisps, and serpentine leeches and eels.

Blob-like creatures use the machine skeleton (rk/machine.py: root, body, heart/jaw) and its 'nest' clips;
serpents get their own spine chain and undulating clips.
"""
import math

import numpy as np
from mathutils import Matrix, Vector

from . import sdf as F
from .anim import IDLE_FRAMES, P, WALK_FRAMES
from .rig import LAT, ROLL, YAW, Skeleton, add_poses, blend_poses, smooth

V = lambda *a: Vector(a)


def mesh(ch, f, mat, bone, h=0.005, name=None, smooth=3):
    o = f.mesh(h, ch.coll, mat, name=name or f.name, smooth=smooth)
    ch.parts.append((o, bone))
    return o


# ------------------------------------------------------------------ blobs
def bone_nest(ch, M):
    """A mound of rib cages, long bones and skulls around a pulsing soul-heart."""
    rng = np.random.default_rng(7)
    f = F.Field('Bone nest')
    f.reach = 0.05
    for k in range(26):
        a = rng.uniform(0, math.tau)
        r = rng.uniform(0.05, 0.42)
        z = 0.08 + rng.uniform(0, 0.42) * (1 - r / 0.5)
        c = V(math.cos(a) * r, math.sin(a) * r * 0.85, z)
        d = V(rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.3, 0.6)).normalized()
        L = rng.uniform(0.18, 0.34)
        f.add(F.RoundCone(c - d * L * 0.5, c + d * L * 0.5, 0.028, 0.024), 0.01)
        for e in (c - d * L * 0.5, c + d * L * 0.5):
            f.add(F.Sphere(e, 0.042), 0.01)
    for k in range(7):
        a = k / 7 * math.tau + 0.3
        c = V(math.cos(a) * 0.3, math.sin(a) * 0.26, 0.12 + 0.08 * (k % 3))
        f.add(F.Ellipsoid(c, (0.075, 0.065, 0.07)), 0.02)
    f.cut(F.Ellipsoid(V(0, 0, 0.32), (0.22, 0.2, 0.18)), 0.04)
    f.cut(F.Plane(V(0, 0, 0), (0, 0, 1)), 0.003)
    mesh(ch, f, M['bone'], 'body', 0.005, 'Bone nest')
    sk = F.Field('Nest sockets')
    for k in range(7):
        a = k / 7 * math.tau + 0.3
        c = V(math.cos(a) * 0.3, math.sin(a) * 0.26, 0.12 + 0.08 * (k % 3))
        out = V(math.cos(a), math.sin(a), 0.2).normalized()
        for s in (-1, 1):
            sk.add(F.Sphere(c + out * 0.055 + out.cross(V(0, 0, 1)).normalized() * 0.025 * s + V(0, 0, 0.012), 0.017), 0.0)
    mesh(ch, sk, M['glow'], 'body', 0.002, 'Nest eyes', 1)
    h = F.Field('Soul heart')
    h.add(F.Sphere(V(0, 0, 0.3), 0.13), 0.0)
    h.disp = lambda P_: F.noise3(P_, 25.0, 3, 2) * 0.01
    mesh(ch, h, M['glow'], 'heart', 0.003, 'Soul heart', 1)


def spore(ch, M, spots='plague', skin=None):
    """A bloated fungus sac on stubby roots, spattered with glowing spots."""
    f = F.Field('Spore sac')
    f.add(F.Ellipsoid(V(0, 0, 0.42), (0.36, 0.34, 0.4)), 0.0)
    f.add(F.Ellipsoid(V(0.05, 0, 0.82), (0.16, 0.15, 0.1)), 0.08)
    rng = np.random.default_rng(3)
    for k in range(6):
        a = k / 6 * math.tau
        f.add(F.RoundCone(V(math.cos(a) * 0.2, math.sin(a) * 0.18, 0.15), V(math.cos(a) * 0.4, math.sin(a) * 0.36, 0.0),
                          0.07, 0.03), 0.05)
    f.disp = lambda P_: F.noise3(P_, 8.0, 5, 2) * 0.015
    f.cut(F.Plane(V(0, 0, 0), (0, 0, 1)), 0.003)
    mesh(ch, f, skin or M['flesh'], 'body', 0.006, 'Spore sac')
    s = F.Field('Spore spots')
    pts = []
    for k in range(16):
        a = rng.uniform(-1.2, 1.2) + 0.0
        z = rng.uniform(0.2, 0.75)
        el = math.asin((z - 0.42) / 0.4)
        p = V(math.cos(el) * math.cos(a) * 0.36, math.cos(el) * math.sin(a) * 0.34, z)
        s.add(F.Ellipsoid(p, (0.04, 0.05, 0.05)), 0.0)
    mesh(ch, s, M[spots], 'heart', 0.003, 'Spore spots', 1)


def swamp_maw(ch, M):
    """A low mud mound split by a toothy maw (jaw bone opens on the attack)."""
    f = F.Field('Mud mound')
    f.add(F.Ellipsoid(V(0, 0, 0.12), (0.62, 0.5, 0.32)), 0.0)
    f.cut(F.Ellipsoid(V(0.32, 0, 0.18), (0.36, 0.32, 0.1)), 0.04)
    f.disp = lambda P_: F.noise3(P_, 6.0, 7, 3) * 0.03
    f.cut(F.Plane(V(0, 0, 0), (0, 0, 1)), 0.003)
    mesh(ch, f, M['mud'], 'body', 0.008, 'Mud mound')
    j = F.Field('Maw lip')
    j.add(F.Ellipsoid(V(0.3, 0, 0.24), (0.34, 0.3, 0.06)), 0.0)
    j.cut(F.Ellipsoid(V(0.32, 0, 0.2), (0.3, 0.26, 0.08)), 0.02)
    j.disp = lambda P_: F.noise3(P_, 10.0, 2, 2) * 0.01
    mesh(ch, j, M['mud'], 'jaw', 0.006, 'Maw lip')
    t = F.Field('Maw teeth')
    for k in range(12):
        a = (k / 11 - 0.5) * 2.6
        p = V(0.3 + math.cos(a) * 0.3, math.sin(a) * 0.27, 0.2)
        t.add(F.RoundCone(p, p + V(-math.cos(a) * 0.03, -math.sin(a) * 0.03, -0.09), 0.03, 0.004), 0.0)
    mesh(ch, t, M['bone'], 'jaw', 0.003, 'Maw teeth')
    b = F.Field('Lower teeth')
    for k in range(10):
        a = (k / 9 - 0.5) * 2.4
        p = V(0.3 + math.cos(a) * 0.26, math.sin(a) * 0.23, 0.1)
        b.add(F.RoundCone(p, p + V(0, 0, 0.08), 0.026, 0.004), 0.0)
    mesh(ch, b, M['bone'], 'body', 0.003, 'Lower teeth')
    e = F.Field('Maw eyes')
    for k, (y, z) in enumerate([(-0.18, 0.36), (0.2, 0.34), (0.0, 0.42)]):
        e.add(F.Sphere(V(0.12, y, z), 0.035), 0.0)
    mesh(ch, e, M['glow'], 'body', 0.002, 'Maw eyes', 1)


def reliquary_eye(ch, M):
    """A floating stone orb with an amber iris, inside a ring of gilded reliquary plates."""
    f = F.Field('Eye stone')
    c = V(0, 0, 1.0)
    f.add(F.Sphere(c, 0.36), 0.0)
    f.cut(F.Ellipsoid(c + V(0.34, 0, 0), (0.12, 0.26, 0.2)), 0.04)
    f.disp = lambda P_: F.noise3(P_, 9.0, 3, 2) * 0.008
    mesh(ch, f, M['stone'], 'body', 0.006, 'Eye stone')
    i = F.Field('Iris')
    i.add(F.Sphere(c + V(0.2, 0, 0), 0.2), 0.0)
    mesh(ch, i, M['iris'], 'heart', 0.003, 'Iris', 1)
    p = F.Field('Pupil')
    p.add(F.Ellipsoid(c + V(0.39, 0, 0), (0.02, 0.035, 0.09)), 0.0)
    mesh(ch, p, M['dark'], 'heart', 0.002, 'Pupil', 1)
    r = F.Field('Reliquary ring')
    r.add(F.Torus(c, 0.5, 0.035, Matrix.Rotation(math.radians(70), 3, 'Y') @ Matrix.Rotation(math.radians(20), 3, 'X'),
                  squash=1.0), 0.0)
    for k in range(6):
        a = k / 6 * math.tau
        q = Matrix.Rotation(math.radians(70), 3, 'Y') @ Matrix.Rotation(math.radians(20), 3, 'X') @ \
            V(math.cos(a) * 0.5, math.sin(a) * 0.5, 0)
        r.add(F.Box(c + q, (0.05, 0.05, 0.05), r=0.015), 0.01)
    mesh(ch, r, M['gold'], 'body', 0.004, 'Reliquary ring')


def wisp(ch, M, offset=V(0, 0, 0), core=True, name='Wisp', bone='body', size=1.0):
    """A will-o'-wisp: a flame-shaped ghost streaming up and back, a hollow face lit by its core."""
    s = size
    f = F.Field(name)
    c = V(0, 0, 1.0) + offset
    f.add(F.Sphere(c, 0.16 * s), 0.0)
    f.add(F.Chain([c, c + V(-0.16, 0, 0.1) * s, c + V(-0.3, 0.04, 0.24) * s, c + V(-0.36, -0.02, 0.44) * s],
                  [0.14 * s, 0.1 * s, 0.055 * s, 0.012 * s]), 0.07 * s)
    for sy in (-1, 1):
        f.add(F.Chain([c + V(-0.04, sy * 0.06, -0.08) * s, c + V(-0.12, sy * 0.1, -0.2) * s, c + V(-0.24, sy * 0.08, -0.26) * s],
                      [0.06 * s, 0.035 * s, 0.008 * s]), 0.05 * s)
    for sy in (-1, 1):
        f.cut(F.Ellipsoid(c + V(0.14, sy * 0.06, 0.03) * s, (0.04 * s, 0.032 * s, 0.045 * s)), 0.012 * s)
    f.cut(F.Ellipsoid(c + V(0.15, 0, -0.065) * s, (0.04 * s, 0.045 * s, 0.03 * s)), 0.012 * s)
    f.disp = lambda P_: F.noise3(P_, 12.0, 2, 2) * 0.008
    mesh(ch, f, M['wisp'], bone, 0.004, name, 2)
    if core:
        k = F.Field(name + ' core')
        k.add(F.Sphere(c, 0.085 * s), 0.0)
        mesh(ch, k, M['glow'], bone, 0.002, name + ' core', 1)


# ------------------------------------------------------------------ serpents
SEGMENTS = 8


def serpent_skeleton(length=1.6, height=0.18, head=0.25):
    """root, body, seg.0 (tail) .. seg.N-1 (neck) and head, laid along +X at `height`."""
    def build(ident, spec, coll):
        sk = Skeleton(ident, coll)
        sk.bone('root', (0, 0, 0), (0, 0, 0.25), deform=False)
        sk.bone('body', (0, 0, height), (0.1, 0, height), 'root', deform=False)
        x0 = -length * 0.55
        step = length / SEGMENTS
        prev = 'body'
        J = {'segs': []}
        for i in range(SEGMENTS):
            a = V(x0 + i * step, 0, height)
            b = V(x0 + (i + 1) * step, 0, height)
            name = f'seg.{i}'
            sk.bone(name, a, b, prev, connect=i > 0)
            J[name] = (a, b)
            J['segs'].append(name)
            prev = name
        hb = V(x0 + length, 0, height)
        sk.bone('head', hb, hb + V(head, 0, 0.02), prev, connect=True)
        sk.bone('jaw', hb + V(0.02, 0, -0.04), hb + V(head * 0.9, 0, -0.06), 'head')
        J['head'] = (hb, hb + V(head, 0, 0.02))
        J['x0'], J['length'] = x0, length
        arm = sk.build()
        return arm, J
    return build


def serpent_clips(J, kind='eel'):
    segs = J['segs']
    n = len(segs)
    C = {}

    def wave(ph, amp, lift=0.0):
        pose = {}
        for i, s in enumerate(segs):
            pose[s] = [(amp * math.sin(ph - i * 0.8), YAW), (lift * math.sin(ph - i * 0.8 + 1.2), LAT)]
        pose['head'] = [(-amp * 0.6 * math.sin(ph - n * 0.8), YAW)]
        return pose
    # the resting coil: an S along the ground, the forequarters reared and the head level, ready to strike
    rear = 11 if kind == 'eel' else 6
    st = wave(0.6, 16)
    for i, s in enumerate(segs[-3:]):
        st[s] = st[s] + [(rear * (1 + i * 0.3), LAT)]
    st['head'] = st['head'] + [(-rear * 3.3 * 0.85, LAT)]
    C['idle'] = [wave(math.tau * i / IDLE_FRAMES, 5, 2) for i in range(IDLE_FRAMES)]
    C['walk'] = [wave(math.tau * i / WALK_FRAMES, 14, 4) for i in range(WALK_FRAMES)]
    coil = {s: [(-(3 if i > len(segs) // 2 else 2), LAT)] for i, s in enumerate(segs)}
    coil['head'] = [(12, LAT)]
    coil['@move'] = {'root': (-0.12, 0, 0)}
    strike = {s: [(6 if i > len(segs) // 2 else 0, LAT)] for i, s in enumerate(segs)}
    strike['head'] = [(6, LAT)]
    strike['jaw'] = [(-35, LAT)]
    strike['@move'] = {'root': (0.32, 0, 0)}
    follow = {'jaw': [(-10, LAT)], '@move': {'root': (0.2, 0, 0)}}
    rest = {}
    seq = [(0, rest), (1, blend_poses(rest, coil, .5)), (3, coil), (4, strike), (6, follow), (9, rest)]
    att = []
    for f in range(10):
        for (a, pa), (b, pb) in zip(seq, seq[1:]):
            if a <= f <= b:
                t = 0 if a == b else (f - a) / (b - a)
                att.append(blend_poses(pa, pb, smooth(t)))
                break
    C['attack'] = att
    hit = {s: [(-6, LAT)] for s in segs[-3:]}
    hit['@move'] = {'root': (-0.06, 0, 0)}
    C['hit'] = [hit, blend_poses({}, hit, .4)]
    death = []
    for i in range(6):
        t = smooth(i / 5)
        pose = wave(i * 1.2, 20 * t)
        pose['body'] = [(-55 * t, ROLL)]   # rolls away from the camera, belly up, staying on the ground
        death.append(pose)
    C['death'] = death
    # deploy: uncoils from a low, flattened crouch (nothing goes below the ground line)
    low = {s: [(-4, LAT)] for s in segs[-3:]}
    low['head'] = [(14, LAT)]
    low['@scale'] = {'root': (1.0, 1.0, 0.8)}
    C['deploy'] = [low, blend_poses(low, {}, 0.5), wave(0.5, 10), {}]
    for k in C:
        if k != 'death':
            C[k] = [add_poses(st, p) for p in C[k]]
    C['death'] = [add_poses(blend_poses(st, {}, i / max(1, len(C['death']) - 1)), p) for i, p in enumerate(C['death'])]
    return st, C


def serpent_body(ch, J, mat, radius=0.13, belly=None, kind='eel', fins=None, gills=None):
    """A sinuous body along the spine chain (thickest behind the head), with fins or a leech's sucker."""
    f = F.Field('Serpent')
    f.reach = 0.08
    segs = J['segs']
    n = len(segs)
    pts = []
    radii = []
    for i, s in enumerate(segs):
        a, b = J[s]
        t = i / (n - 1)
        r = radius * (0.35 + 0.65 * math.sin(math.pi * (0.15 + 0.75 * t)))
        f.add(F.RoundCone(a, b, r * 0.95, r), 0.06, {s: 1})
    hb, ht = J['head']
    if kind == 'eel':
        f.add(F.Ellipsoid(hb.lerp(ht, 0.5), (0.17, radius * 1.05, radius * 0.85)), 0.06, {'head': 1})
        f.add(F.RoundCone(hb.lerp(ht, 0.5), ht + V(0.05, 0, -0.03), radius * 0.8, radius * 0.45), 0.05, {'head': 1})
        f.add(F.RoundCone(hb.lerp(ht, 0.45) + V(0, 0, -radius * 0.35), ht + V(0.03, 0, -radius * 0.5), radius * 0.6,
                          radius * 0.35), 0.04, {'jaw': 1})
        f.cut(F.Box(hb.lerp(ht, 0.75) + V(0.02, 0, -radius * 0.3), (0.14, radius * 0.9, 0.012), r=0.006), 0.012)
    else:
        f.add(F.Ellipsoid(hb.lerp(ht, 0.4), (0.12, radius * 1.1, radius * 1.0)), 0.06, {'head': 1})
        f.cut(F.Ellipsoid(ht + V(0.02, 0, 0), (0.06, radius * 0.7, radius * 0.6)), 0.03)
    f.disp = (lambda P_: 0.006 * np.sin(P_[:, 0] * 70)) if kind == 'leech' else None
    f.cut(F.Plane(V(0, 0, 0), (0, 0, 1)), 0.003)
    body = f.mesh(0.005, ch.coll, mat, name='Serpent body', smooth=4)
    from . import hero
    ch.body, ch.body_field = body, f
    ch.body_skin = lambda obj, arm, fld=f: hero.skin_from_field(obj, arm, fld, sigma=0.05)
    return f
