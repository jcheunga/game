"""Horses in the roster-sheet style: a sculpted (SDF) horse on the beast skeleton (rk/beast.py), a skeletal
horse for the dead, and their tack and barding.

The sculpt is tagged with the beast bones (hips, spine, chest, neck, head, jaw, tail.*, fl.*/bl.* upper,
lower, foot), so it skins from its own primitives like the humanoid bodies.
"""
import math

import numpy as np
from mathutils import Matrix, Vector

from . import sdf as F
from . import sigil

V = lambda *a: Vector(a)


def _leg_tags(leg, part):
    return {f'{leg}.{part}': 1}


def horse_field(J, bulk=1.0, gaunt=0.0):
    """A stylised, heavy warhorse."""
    f = F.Field('Horse')
    f.reach = 0.12
    b = bulk
    g = 1 - 0.25 * gaunt
    pel, ch, nk, hd, sn = J['pelvis'], J['chest'], J['neck'], J['head'], J['snout']
    f.group = 'torso'
    f.add(F.Ellipsoid(V(-0.08, 0, 1.2), (0.62, 0.3 * b * g, 0.3 * b)), 0.0, F.along(pel, ch, 'hips', 'chest', 0.2, 0.8))
    f.add(F.Ellipsoid(V(0.4, 0, 1.24), (0.3, 0.3 * b * g, 0.32 * b)), 0.12, {'chest': 1})
    f.add(F.Ellipsoid(V(0.48, 0, 1.08), (0.18, 0.26 * b * g, 0.2)), 0.1, {'chest': 1})   # breast
    f.add(F.Ellipsoid(V(0.32, 0, 1.46), (0.22, 0.14 * b, 0.13)), 0.12, {'chest': 1})     # withers
    for sy in (-1, 1):
        f.add(F.Ellipsoid(V(-0.56, sy * 0.13 * b, 1.27), (0.3, 0.19 * b * g, 0.3)), 0.12, {'hips': 1})
        f.add(F.Ellipsoid(V(0.38, sy * 0.2 * b, 1.08), (0.16, 0.1, 0.2)), 0.08, {'chest': 1})   # shoulder muscle
    f.add(F.Ellipsoid(V(-0.72, 0, 1.36), (0.18, 0.18 * b, 0.14)), 0.12, {'hips': 1})        # croup
    # neck: tall and narrow with a crest
    f.group = 'neck'
    f.add(F.RoundCone(V(0.5, 0, 1.36), V(0.96, 0, 1.93), 0.31 * b, 0.15, squash=(0.7, 1.0), up=(0.6, 0, 0.8)), 0.1,
          F.along(V(0.52, 0, 1.38), V(0.96, 0, 1.93), 'chest', 'neck', 0.0, 0.5))
    f.add(F.Chain([V(0.4, 0, 1.55), V(0.62, 0, 1.8), V(0.88, 0, 2.02)], [0.13, 0.125, 0.09], squash=(0.6, 1.0),
                  up=(0, 0, 1)), 0.08, {'neck': 1})
    # head: broad forehead, long face, soft muzzle
    f.group = 'head'
    f.add(F.Ellipsoid(V(1.04, 0, 1.95), (0.17, 0.135, 0.155)), 0.06, {'head': 1})
    f.add(F.RoundCone(V(1.08, 0, 1.86), V(1.38, 0, 1.6), 0.145, 0.105, squash=(0.82, 1.0), up=(0.7, 0, 0.7)), 0.08,
          {'head': 1})
    f.add(F.Ellipsoid(V(1.42, 0, 1.56), (0.1, 0.11, 0.11)), 0.05, {'head': 1})
    for sy in (-1, 1):
        f.add(F.Ellipsoid(V(1.0, sy * 0.065, 1.8), (0.15, 0.09, 0.135)), 0.06, {'head': .5, 'jaw': .5})  # cheek
        f.cut(F.Sphere(V(1.5, sy * 0.05, 1.57), 0.027), 0.01)                                          # nostril
        f.add(F.RoundCone(V(0.96, sy * 0.07, 2.04), V(0.92, sy * 0.09, 2.2), 0.04, 0.012, squash=(0.6, 1.0),
                          up=(1, 0, 0)), 0.02, {'head': 1})                                            # ears
    f.cut(F.Box(V(1.49, 0, 1.48), (0.08, 0.13, 0.008), r=0.004), 0.01)                                   # mouth line
    # legs
    for side, sy in (('R', -1), ('L', 1)):
        fl = 'fl.' + side
        top, el, wr, paw = J[fl + '.top'], J[fl + '.elbow'], J[fl + '.wrist'], J[fl + '.paw']
        f.group = fl
        f.add(F.RoundCone(top + V(0, 0, 0.05), el, 0.17 * b, 0.1), 0.08, _leg_tags(fl, 'upper'))
        f.add(F.Ellipsoid(top.lerp(el, 0.45) + V(0.035, sy * 0.02, 0), (0.13, 0.105, 0.2)), 0.06, _leg_tags(fl, 'upper'))
        f.add(F.Ellipsoid(el.lerp(wr, 0.22) + V(0.01, 0, 0), (0.075, 0.07, 0.12)), 0.04, _leg_tags(fl, 'lower'))  # forearm
        f.add(F.RoundCone(el, wr, 0.08, 0.06), 0.04, _leg_tags(fl, 'lower'))
        f.add(F.Ellipsoid(wr, (0.07, 0.068, 0.075)), 0.03, _leg_tags(fl, 'lower'))                                # knee
        fet = paw + V(-0.02, 0, 0.12)
        f.add(F.RoundCone(wr, fet, 0.055, 0.056), 0.02, _leg_tags(fl, 'foot'))
        f.add(F.Sphere(fet, 0.07), 0.03, _leg_tags(fl, 'foot'))                                                   # fetlock
        f.add(F.RoundCone(fet, paw + V(0.02, 0, 0.05), 0.062, 0.068), 0.02, _leg_tags(fl, 'foot'))
        bl = 'bl.' + side
        top, kn, hk, paw = J[bl + '.top'], J[bl + '.knee'], J[bl + '.hock'], J[bl + '.paw']
        f.group = bl
        f.add(F.Ellipsoid(top.lerp(kn, 0.35) + V(-0.04, sy * 0.03, 0), (0.2 * b, 0.15 * b, 0.28)), 0.1,
              _leg_tags(bl, 'upper'))
        f.add(F.RoundCone(top, kn, 0.18 * b, 0.1), 0.08, _leg_tags(bl, 'upper'))
        f.add(F.Ellipsoid(kn.lerp(hk, 0.3) + V(-0.01, 0, 0), (0.08, 0.075, 0.13)), 0.04, _leg_tags(bl, 'lower'))  # gaskin
        f.add(F.RoundCone(kn, hk, 0.085, 0.062), 0.04, _leg_tags(bl, 'lower'))
        f.add(F.Ellipsoid(hk + V(-0.015, 0, 0.005), (0.075, 0.066, 0.07)), 0.03, {bl + '.lower': .5, bl + '.foot': .5})
        fet = paw + V(-0.02, 0, 0.12)
        f.add(F.RoundCone(hk, fet, 0.056, 0.056), 0.02, _leg_tags(bl, 'foot'))
        f.add(F.Sphere(fet, 0.07), 0.03, _leg_tags(bl, 'foot'))
        f.add(F.RoundCone(fet, paw + V(0.02, 0, 0.05), 0.062, 0.068), 0.02, _leg_tags(bl, 'foot'))
    f.group = None
    return f


def hooves(J, mat, coll):
    f = F.Field('Hooves')
    for leg in ('fl.R', 'fl.L', 'bl.R', 'bl.L'):
        paw = J[leg + '.paw']
        f.add(F.RoundCone(paw + V(0.02, 0, 0.065), paw + V(0.035, 0, -0.01), 0.07, 0.084), 0.0, {leg + '.foot': 1})
    f.cut(F.Plane(V(0, 0, 0.0), (0, 0, 1)), 0.003)
    return f


def mane_tail(J, mat, coll, mane=True, tail=True, seed=3, ghost=False):
    """Sculpted mane along the neck crest and a flowing tail (separate hair meshes)."""
    out = []
    rng = np.random.default_rng(seed)
    if mane:
        f = F.Field('Mane')
        f.reach = 0.05
        for i in range(9):
            t = i / 8
            p = V(0.4, 0, 1.58).lerp(V(0.92, 0, 2.06), t)
            tip = p + V(-0.12 - 0.04 * rng.uniform(), (-1) ** i * 0.06, -0.16 - 0.05 * rng.uniform())
            f.add(F.RoundCone(p, tip, 0.06, 0.012, squash=(0.6, 1.0), up=(0, 1, 0)), 0.035,
                  {'neck': t, 'chest': 1 - t})
        f.add(F.RoundCone(V(0.98, 0, 2.06), V(1.12, 0, 2.02), 0.05, 0.02), 0.03, {'head': 1})   # forelock
        f.disp = lambda P: 0.004 * (np.abs(np.sin(P[:, 1] * 120 + P[:, 2] * 30)) ** 0.6 - 0.5)
        out.append(('mane', f))
    if tail:
        t0 = J['tail0']
        f = F.Field('Tail')
        f.reach = 0.05
        pts = [t0, t0 + V(-0.14, 0, -0.02), t0 + V(-0.24, 0, -0.22), t0 + V(-0.26, 0, -0.55), t0 + V(-0.2, 0, -0.85)]
        f.add(F.Chain(pts, [0.06, 0.09, 0.1, 0.09, 0.03], squash=(0.7, 1.0), up=(0, 1, 0)), 0.0,
              lambda P: {'tail.0': np.clip(1 - (t0.z - P[:, 2]) / 0.3, 0, 1),
                         'tail.1': np.clip((t0.z - P[:, 2]) / 0.3, 0, 1)})
        for k in range(4):
            off = V(0, (k - 1.5) * 0.035, 0)
            f.add(F.RoundCone(pts[3] + off, pts[4] + off + V(rng.uniform(-0.05, 0.05), 0, -0.1), 0.04, 0.008),
                  0.03, {'tail.1': 1})
        f.disp = lambda P: 0.005 * (np.abs(np.sin(P[:, 1] * 100 + P[:, 0] * 20)) ** 0.6 - 0.5)
        out.append(('tail', f))
    return out


def bone_horse_field(J, thick=1.0):
    """A skeletal warhorse: long skull, vertebral column, ribs, pelvis and leg bones."""
    f = F.Field('Bone horse')
    f.reach = 0.05
    t = thick
    pel, ch = J['pelvis'], J['chest']
    f.group = 'torso'
    for i in range(14):
        x = pel.x - 0.1 + i * (ch.x - pel.x + 0.3) / 13
        bone = 'hips' if i < 5 else 'spine' if i < 9 else 'chest'
        p = V(x, 0, 1.42 + 0.03 * math.sin(i / 13 * math.pi))
        f.add(F.Ellipsoid(p, (0.045 * t, 0.05 * t, 0.04 * t)), 0.01, {bone: 1})
        f.add(F.RoundCone(p, p + V(-0.02, 0, 0.09 * t), 0.018 * t, 0.008 * t), 0.006, {bone: 1})
    for i in range(8):
        x = -0.05 + i * 0.075
        for sy in (-1, 1):
            pts = [V(x, sy * 0.03, 1.42), V(x + 0.01, sy * 0.24 * t, 1.32), V(x + 0.04, sy * 0.25 * t, 1.08),
                   V(x + 0.06, sy * 0.08, 0.96)]
            f.add(F.Chain(pts, [0.017 * t] * 4, squash=(1.0, 0.7)), 0.008, {'spine' if i < 4 else 'chest': 1})
    f.add(F.RoundCone(V(0.0, 0, 0.95), V(0.56, 0, 0.98), 0.03 * t, 0.035 * t, squash=(1, 0.5), up=(0, 0, 1)), 0.01,
          {'spine': .5, 'chest': .5})
    f.group = 'hips'
    for sy in (-1, 1):
        f.add(F.Ellipsoid(V(-0.62, sy * 0.14, 1.3), (0.2 * t, 0.07 * t, 0.12 * t)), 0.03, {'hips': 1})
        f.add(F.RoundCone(V(-0.7, sy * 0.12, 1.25), V(-0.5, sy * 0.1, 1.08), 0.04 * t, 0.03 * t), 0.02, {'hips': 1})
    # neck vertebrae and the long skull
    f.group = 'neck'
    for i in range(7):
        p = V(0.5, 0, 1.45).lerp(V(0.95, 0, 1.95), i / 6)
        f.add(F.Ellipsoid(p, (0.05 * t, 0.045 * t, 0.045 * t)), 0.01, {'neck' if i > 2 else 'chest': 1})
    f.group = 'head'
    f.add(F.Ellipsoid(V(1.03, 0, 1.94), (0.12, 0.1 * t, 0.11)), 0.0, {'head': 1})
    f.add(F.RoundCone(V(1.08, 0, 1.86), V(1.42, 0, 1.6), 0.09, 0.06, squash=(0.8, 1.0), up=(0.7, 0, 0.7)), 0.06,
          {'head': 1})
    for sy in (-1, 1):
        f.cut(F.Ellipsoid(V(1.04, sy * 0.1, 1.95), (0.06, 0.06, 0.055)), 0.02)   # eye sockets
        f.add(F.RoundCone(V(1.02, sy * 0.07, 1.82), V(1.36, sy * 0.04, 1.52), 0.035, 0.025), 0.03,
              {'head': .4, 'jaw': .6})
    f.cut(F.Ellipsoid(V(1.36, 0, 1.66), (0.08, 0.03, 0.03)), 0.01)
    for leg in ('fl.R', 'fl.L', 'bl.R', 'bl.L'):
        keys = ['top', 'elbow', 'wrist', 'paw'] if leg.startswith('fl') else ['top', 'knee', 'hock', 'paw']
        f.group = leg
        for a, b_, bn, r in zip(keys, keys[1:], ['upper', 'lower', 'foot'], (0.04, 0.032, 0.028)):
            pa, pb = J[leg + '.' + a], J[leg + '.' + b_]
            f.add(F.RoundCone(pa, pb, r * t, r * 0.9 * t), 0.012, {leg + '.' + bn: 1})
            f.add(F.Sphere(pb, r * 1.5 * t), 0.012, {leg + '.' + bn: 1})
        paw = J[leg + '.paw']
        f.add(F.RoundCone(paw + V(0.02, 0, 0.05), paw + V(0.03, 0, -0.005), 0.045, 0.055), 0.01, {leg + '.foot': 1})
    f.cut(F.Plane(V(0, 0, 0.0), (0, 0, 1)), 0.003)
    f.group = None
    return f


def eye_glow_points(J):
    return [V(1.07, sy * 0.1, 1.95) for sy in (-1, 1)]


def build(ch, M, kind='living', coat=None, mane=None, hoof=None, bulk=1.0):
    """Mesh and skin the horse on a beast-skeleton Character (rest pose)."""
    from . import hero
    J = ch.J
    if kind == 'living':
        field = horse_field(J, bulk)
        body = field.mesh(0.006, ch.coll, coat, name=ch.ident + ' horse', smooth=6)
        ch.body, ch.body_field = body, field
        ch.body_skin = lambda obj, arm, f=field: hero.skin_from_field(obj, arm, f, sigma=0.04)
        hf = hooves(J, hoof, ch.coll)
        ho = hf.mesh(0.004, ch.coll, hoof, name='Hooves', smooth=2)
        ch.parts.append((ho, {'fieldw': hf}))
        for name, f in mane_tail(J, mane, ch.coll):
            o = f.mesh(0.006, ch.coll, mane, name=name.title(), smooth=3)
            ch.parts.append((o, {'fieldw': f}))
        eyes = F.Field('Horse eyes')
        for sy in (-1, 1):
            eyes.add(F.Ellipsoid(V(1.1, sy * 0.118, 1.97), (0.034, 0.024, 0.036)), 0.0, {'head': 1})
        ch.parts.append((eyes.mesh(0.003, ch.coll, M['eye'], name='Horse eyes', smooth=1), 'head'))
    else:
        field = bone_horse_field(J)
        body = field.mesh(0.0055, ch.coll, M['bone'], name=ch.ident + ' bones', smooth=2)
        # garments hang on a lean ghost of the living horse
        ch.body, ch.body_field = body, horse_field(J, 0.85, gaunt=0.6)
        ch.body_skin = lambda obj, arm, f=field: hero.skin_from_field(obj, arm, f, sigma=0.015, smooth=1)
        g = F.Field('Ghost eyes')
        for p in eye_glow_points(J):
            g.add(F.Sphere(p, 0.035), 0.0, {'head': 1})
        ch.parts.append((g.mesh(0.003, ch.coll, M['glow'], name='Ghost eyes', smooth=1), 'head'))
        if mane is not None:
            for name, f in mane_tail(J, mane, ch.coll, ghost=True):
                o = f.mesh(0.006, ch.coll, mane, name='Ghost ' + name, smooth=3)
                ch.parts.append((o, {'fieldw': f}))
    return ch.body


def barding(ch, M, cloth, trim=None, emblem=None, emblem_mat=None, plate=True, tatter=0.0, low=0.72, seed=4,
            saddle='leather', chanfron='steel'):
    """Saddle, a caparison over the barrel (trimmed, with the faction emblem on each flank), and steel
    chanfron and peytral."""
    J = ch.J
    torso = ch.body_field.subset(['torso'])
    torso.reach = 0.2
    out = []
    lo, hi = torso.bounds(0.35)
    rng = np.random.default_rng(seed)
    ph = rng.uniform(0, 6.28, 4)
    # drape: the barrel grown downward into a hanging skirt
    hang = F.RoundCone(V(-0.66, 0, 1.22), V(0.5, 0, 1.22), 0.34, 0.36, squash=(1.0, 1.35), up=(0, 0, 1))
    support = F.Field('Caparison support')
    support.reach = 0.25
    support.ops = list(torso.ops) + [('add', hang, 0.12, None, None)]
    support.groups = [None] * len(support.ops)

    def sstep(a, b, x):
        u = np.clip((x - a) / (b - a), 0, 1)
        return u * u * (3 - 2 * u)

    def hem(P):
        x = P[:, 0]
        # hangs to the knees along the barrel, sweeps up over the chest and a little at the rump; dagged edge
        z = (low + 0.3 * sstep(0.12, 0.52, x) + 0.12 * sstep(-0.62, -0.9, x)
             + 0.028 * np.abs(np.sin(x * 15 + ph[2]))
             + tatter * 0.07 * (np.abs(np.sin(x * 16 + ph[0])) ** 0.5 + 0.6 * np.abs(np.sin(x * 31 + ph[1]))))
        return z - P[:, 2]

    def region(P):
        return np.maximum.reduce([hem(P), -1.12 - P[:, 0], P[:, 0] - 0.56, P[:, 2] - 1.62])
    t = 0.04
    surf = lambda P: support.eval(P) - t + 0.014 * np.sin(P[:, 0] * 17 + F.noise3(P, 4.0, seed, 1) * 2.0) * \
        np.clip((1.3 - P[:, 2]) / 0.45, 0, 1)
    f = F.Field('Caparison')
    f.add(F.Fn(lambda P: np.abs(surf(P)) - 0.008, lo, hi), 0.0)
    f.keep(F.Fn(region, lo, hi), 0.004)
    cap = f.mesh(0.006, ch.coll, cloth, name='Caparison')
    ch.parts.append((cap, {'fieldw': ch.body_field, 'restrict': ['hips', 'spine', 'chest']}))
    out.append(cap)
    if trim is not None:
        tf = F.Field('Caparison trim')
        tf.add(F.Fn(lambda P: np.abs(surf(P)) - 0.012, lo, hi), 0.0)
        tf.keep(F.Fn(lambda P: np.abs(region(P) + 0.015) - 0.015, lo, hi), 0.003)
        to = tf.mesh(0.004, ch.coll, trim, name='Caparison trim')
        ch.parts.append((to, {'fieldw': ch.body_field, 'restrict': ['hips', 'spine', 'chest']}))
        out.append(to)
    if emblem:
        shape = sigil.EMBLEMS[emblem]()
        for sy in (-1, 1):
            c = np.array([-0.05, 0.0, 1.0])
            size = 0.17

            def efn(P, sy=sy):
                u = (P[:, 0] - c[0]) / size * -sy
                v = (P[:, 2] - c[2]) / size
                return np.maximum.reduce([shape(u, v) * size, np.abs(surf(P) - 0.012) - 0.004, -sy * P[:, 1]])
            e = F.Field('Caparison emblem')
            e.add(F.Fn(efn, lo, hi), 0.0)
            eo = e.mesh(0.004, ch.coll, emblem_mat or trim, name='Caparison emblem', smooth=1)
            ch.parts.append((eo, {'fieldw': ch.body_field, 'restrict': ['spine', 'chest']}))
            out.append(eo)
    # saddle
    s = F.Field('Saddle')
    sp = V(0.0, 0, 1.52)
    s.add(F.Box(sp, (0.26, 0.2, 0.05), r=0.04), 0.0)
    s.add(F.Box(sp + V(-0.22, 0, 0.07), (0.04, 0.16, 0.08), r=0.03), 0.04)
    s.add(F.Box(sp + V(0.22, 0, 0.06), (0.05, 0.12, 0.07), r=0.03), 0.04)
    s.cut(F.Ellipsoid(sp + V(0, 0, 0.1), (0.18, 0.3, 0.06)), 0.03)
    so = s.mesh(0.004, ch.coll, M[saddle], name='Saddle')
    ch.parts.append((so, 'spine'))
    out.append(so)
    for sy in (-1, 1):
        st = F.Field('Stirrup')
        p = V(0.02, sy * 0.36, 0.96)
        st.add(F.RoundCone(V(0.02, sy * 0.3, 1.5), p + V(0, 0, 0.06), 0.012, 0.012), 0.0)
        st.add(F.Torus(p, 0.04, 0.009, Matrix.Rotation(math.radians(90), 3, 'X')), 0.004)
        sto = st.mesh(0.003, ch.coll, M['dark_steel'], name='Stirrup')
        ch.parts.append((sto, 'spine'))
        out.append(sto)
    if plate:
        pm = M[chanfron]
        c = F.Field('Chanfron')
        hd = V(1.2, 0, 1.75)
        c.add(F.Fn(lambda P: np.abs(_head_surface(P) - 0.03) - 0.01, hd - V(0.4, 0.3, 0.4), hd + V(0.4, 0.3, 0.4)), 0.0)
        c.keep(F.Fn(lambda P: np.maximum.reduce([0.92 - P[:, 0], P[:, 0] - 1.45, 1.66 - P[:, 2] - (P[:, 0] - 0.95) * 0.0,
                                                np.abs(P[:, 1]) - 0.11]), hd - V(0.5, 0.4, 0.5), hd + V(0.5, 0.4, 0.5)),
               0.006)
        c.add(F.RoundCone(V(1.0, 0, 2.06), V(1.08, 0, 2.18), 0.03, 0.006), 0.01)
        co = c.mesh(0.004, ch.coll, pm, name='Chanfron')
        ch.parts.append((co, 'head'))
        out.append(co)
        pf = F.Field('Peytral')
        pf.add(F.Fn(lambda P: np.abs(torso.eval(P) - 0.06) - 0.012, lo, hi), 0.0)
        pf.keep(F.Fn(lambda P: np.maximum.reduce([0.5 - P[:, 0], P[:, 2] - 1.38, 0.92 - P[:, 2]]), lo, hi), 0.006)
        po = pf.mesh(0.005, ch.coll, pm, name='Peytral')
        ch.parts.append((po, 'chest'))
        out.append(po)
        if trim is not None:
            tp = F.Field('Peytral trim')
            tp.add(F.Fn(lambda P: np.abs(torso.eval(P) - 0.064) - 0.014, lo, hi), 0.0)
            tp.keep(F.Fn(lambda P: np.abs(np.maximum.reduce([0.5 - P[:, 0], P[:, 2] - 1.38, 0.92 - P[:, 2]]) + 0.012)
                         - 0.012, lo, hi), 0.003)
            tpo = tp.mesh(0.004, ch.coll, trim, name='Peytral trim')
            ch.parts.append((tpo, 'chest'))
            out.append(tpo)
    # bridle and reins
    r = F.Field('Bridle')
    for sy in (-1, 1):
        r.add(F.Chain([V(1.4, sy * 0.095, 1.62), V(1.1, sy * 0.135, 1.86), V(0.98, sy * 0.12, 2.04)], 0.011), 0.0)
        r.add(F.Chain([V(1.4, sy * 0.095, 1.6), V(0.9, sy * 0.17, 1.55), V(0.3, sy * 0.15, 1.62)], 0.008), 0.0)
    ro = r.mesh(0.0025, ch.coll, M['leather_dark'], name='Bridle', smooth=1)
    ch.parts.append((ro, 'neck'))
    out.append(ro)
    return out


def _head_surface(P):
    """Approximate distance to the horse's face (for the chanfron)."""
    a = np.array([1.08, 0, 1.86])
    b = np.array([1.4, 0, 1.6])
    ab = b - a
    t = np.clip(((P - a) @ ab) / (ab @ ab), 0, 1)
    q = P - (a + t[:, None] * ab)
    q[:, 1] /= 0.82
    return np.linalg.norm(q, axis=1) - (0.145 + (0.105 - 0.145) * t)


def _paw(f, wr, paw, tag, b=1.0, toes=4):
    """A round paw with toe pads, under a slim pastern (wr: wrist/hock, paw: ground point)."""
    fet = paw + V(0.035, 0, 0.022)
    f.add(F.RoundCone(wr, fet + V(-0.02, 0, 0.02), 0.034 * b, 0.036 * b), 0.02, tag)
    f.add(F.Ellipsoid(fet, (0.058 * b, 0.048 * b, 0.032)), 0.02, tag)
    for k in range(toes):
        y = (k - (toes - 1) / 2) * 0.024 * b
        f.add(F.Sphere(fet + V(0.045 * b - abs(y) * 0.5, y, -0.006), 0.019 * b), 0.008, tag)


def hound_field(J, bulk=1.0, kind='dog'):
    """A stylised war hound (or, kind='rat', a grave rat) on the beast skeleton's hound tables: a deep
    chest and tucked waist, heavy shoulders and haunches, a broad head, real paws."""
    f = F.Field('Hound')
    f.reach = 0.1
    b = bulk
    rat = kind == 'rat'
    pel, ch = J['pelvis'], J['chest']
    f.group = 'torso'
    torso_w = F.along(pel, ch, 'hips', 'chest', 0.2, 0.8)
    f.add(F.Ellipsoid(V(-0.05, 0, 0.7), (0.3, 0.145 * b, 0.155 * b)), 0.0, torso_w)                 # back and ribs
    f.add(F.Ellipsoid(V(0.2, 0, 0.66), (0.22, 0.165 * b, 0.21 * b)), 0.1, {'chest': 1})             # deep chest
    f.add(F.Ellipsoid(V(0.36, 0, 0.62), (0.1, 0.12 * b, 0.14 * b)), 0.07, {'chest': 1})             # breast
    f.add(F.Ellipsoid(V(-0.2, 0, 0.73), (0.15, 0.11 * b, 0.1 * b)), 0.08, {'hips': .5, 'spine': .5})  # tucked waist
    f.add(F.Ellipsoid(V(-0.36, 0, 0.73), (0.15, 0.135 * b, 0.14 * b)), 0.09, {'hips': 1})           # croup
    for sy in (-1, 1):
        f.add(F.Ellipsoid(V(0.25, sy * 0.115 * b, 0.69), (0.11, 0.06, 0.15)), 0.07, {'chest': 1})    # shoulder
        f.add(F.Ellipsoid(V(-0.37, sy * 0.105 * b, 0.64), (0.13, 0.07, 0.16)), 0.08,
              {'hips': .6, 'bl.%s.upper' % ('R' if sy < 0 else 'L'): .4})                         # haunch
    if rat:
        f.add(F.Ellipsoid(V(-0.1, 0, 0.74), (0.4, 0.16 * b, 0.18 * b)), 0.1, {'spine': 1})          # hunched back
    f.group = 'neck'
    f.add(F.RoundCone(V(0.3, 0, 0.74), V(0.56, 0, 0.92), 0.15 * b, 0.11 * b), 0.07,
          F.along(V(0.3, 0, 0.74), V(0.56, 0, 0.92), 'chest', 'neck'))
    f.add(F.Ellipsoid(V(0.42, 0, 0.86), (0.13, 0.11 * b, 0.09)), 0.06, {'neck': 1})                 # scruff
    f.group = 'head'
    if rat:
        f.add(F.Ellipsoid(V(0.64, 0, 0.98), (0.12, 0.1, 0.095)), 0.04, {'head': 1})
        f.add(F.RoundCone(V(0.68, 0, 0.95), V(0.88, 0, 0.9), 0.085, 0.03), 0.04, {'head': 1})
        f.add(F.RoundCone(V(0.66, 0, 0.9), V(0.82, 0, 0.87), 0.05, 0.025), 0.03, {'jaw': 1})
        for sy in (-1, 1):
            f.add(F.Ellipsoid(V(0.58, sy * 0.1, 1.07), (0.025, 0.065, 0.065)), 0.02, {'head': 1})
    else:
        f.add(F.Ellipsoid(V(0.63, 0, 1.0), (0.125, 0.115, 0.105)), 0.04, {'head': 1})               # cranium
        f.add(F.Ellipsoid(V(0.7, 0, 1.025), (0.06, 0.1, 0.05)), 0.03, {'head': 1})                  # brow
        f.add(F.RoundCone(V(0.7, 0, 0.965), V(0.865, 0, 0.935), 0.085, 0.068, squash=(1.0, 0.86), up=(0, 0, 1)), 0.04,
              {'head': 1})                                                                         # broad muzzle
        f.add(F.RoundCone(V(0.66, 0, 0.9), V(0.84, 0, 0.875), 0.062, 0.045), 0.03, {'jaw': 1})
        for sy in (-1, 1):
            f.add(F.Ellipsoid(V(0.79, sy * 0.055, 0.9), (0.075, 0.042, 0.055)), 0.03, {'head': .5, 'jaw': .5})  # jowls
            f.add(F.Ellipsoid(V(0.66, sy * 0.085, 0.95), (0.06, 0.04, 0.06)), 0.03, {'head': .6, 'jaw': .4})    # cheeks
            f.add(F.RoundCone(V(0.6, sy * 0.085, 1.07), V(0.63, sy * 0.135, 1.14), 0.042, 0.012, squash=(0.45, 1.0),
                              up=(1, 0, 0)), 0.02, {'head': 1})                                     # cropped ears
        f.cut(F.Box(V(0.86, 0, 0.9), (0.06, 0.09, 0.006), r=0.003), 0.008)                          # mouth line
    for side, sy in (('R', -1), ('L', 1)):
        fl = 'fl.' + side
        top, el, wr, paw = J[fl + '.top'], J[fl + '.elbow'], J[fl + '.wrist'], J[fl + '.paw']
        f.group = fl
        f.add(F.RoundCone(top + V(0, 0, 0.04), el, 0.085 * b, 0.052), 0.05, {fl + '.upper': 1})
        f.add(F.Ellipsoid(top.lerp(el, 0.35) + V(0.015, 0, 0), (0.075 * b, 0.06 * b, 0.11)), 0.04, {fl + '.upper': 1})
        f.add(F.RoundCone(el, wr, 0.046, 0.034), 0.025, {fl + '.lower': 1})
        f.add(F.Sphere(wr, 0.037), 0.015, {fl + '.lower': .5, fl + '.foot': .5})
        _paw(f, wr, paw, {fl + '.foot': 1}, b)
        bl = 'bl.' + side
        top, kn, hk, paw = J[bl + '.top'], J[bl + '.knee'], J[bl + '.hock'], J[bl + '.paw']
        f.group = bl
        f.add(F.Ellipsoid(top.lerp(kn, 0.42) + V(-0.02, 0, 0), (0.11 * b, 0.075 * b, 0.15)), 0.05, {bl + '.upper': 1})
        f.add(F.RoundCone(top, kn, 0.1 * b, 0.055), 0.05, {bl + '.upper': 1})
        f.add(F.RoundCone(kn, hk, 0.048, 0.034), 0.025, {bl + '.lower': 1})
        f.add(F.Sphere(hk + V(-0.012, 0, 0.005), 0.036), 0.015, {bl + '.lower': .5, bl + '.foot': .5})   # hock
        _paw(f, hk, paw, {bl + '.foot': 1}, b)
    f.group = 'tail'
    t0 = J['tail0']
    if rat:
        f.add(F.Chain([t0, t0 + V(-0.2, 0, -0.08), t0 + V(-0.42, 0.06, -0.18), t0 + V(-0.62, 0.1, -0.2)],
                      [0.035, 0.025, 0.015, 0.006]), 0.02, lambda P: {'tail.0': np.clip(1 - (t0.x - P[:, 0]) / 0.3, 0, 1),
                                                                     'tail.1': np.clip((t0.x - P[:, 0]) / 0.3, 0, 1)})
    else:
        f.add(F.Chain([t0 + V(0.03, 0, -0.01), t0 + V(-0.1, 0, 0.05), t0 + V(-0.2, 0, 0.13), t0 + V(-0.26, 0, 0.24)],
                      [0.045, 0.036, 0.026, 0.01]), 0.03,
              lambda P: {'tail.0': np.clip(1 - (t0.x - P[:, 0]) / 0.15, 0, 1),
                         'tail.1': np.clip((t0.x - P[:, 0]) / 0.15, 0, 1)})
    f.group = None
    # a short-coated hide: soft muscle-line clumps, not a smooth balloon
    f.disp = lambda P: F.noise3(P * np.array([1.0, 1.0, 2.0]), 14.0, 3, 2) * 0.0035
    return f


def build_hound(ch, M, coat, kind='dog', bulk=1.0, eye='eye', teeth=True):
    from . import hero
    J = ch.J
    field = hound_field(J, bulk, kind)
    body = field.mesh(0.005, ch.coll, coat, name=ch.ident + ' body', smooth=5)
    ch.body, ch.body_field = body, field
    ch.body_skin = lambda obj, arm, f=field: hero.skin_from_field(obj, arm, f, sigma=0.03)
    e = F.Field('Hound eyes')
    for sy in (-1, 1):
        e.add(F.Sphere(V(0.725, sy * 0.072, 1.0) if kind != 'rat' else V(0.71, sy * 0.075, 1.0), 0.024), 0.0)
    ch.parts.append((e.mesh(0.002, ch.coll, M[eye], name='Hound eyes', smooth=1), 'head'))
    n = F.Field('Hound nose')
    n.add(F.Ellipsoid(V(0.925, 0, 0.95) if kind != 'rat' else V(0.86, 0, 0.92), (0.032, 0.042, 0.026)), 0.0)
    ch.parts.append((n.mesh(0.002, ch.coll, M['dark'], name='Hound nose', smooth=1), 'head'))
    if teeth:
        t = F.Field('Fangs')
        for sy in (-1, 1):
            p = V(0.84, sy * 0.04, 0.89) if kind != 'rat' else V(0.82, sy * 0.03, 0.88)
            t.add(F.RoundCone(p, p + V(0.005, 0, -0.045), 0.011, 0.002), 0.0)
        ch.parts.append((t.mesh(0.0015, ch.coll, M['bone'], name='Fangs', smooth=1), 'head'))
    return body
