"""Background figures for the dioramas: stylised crowd people (seated or standing, several cheering
poses, flags and scarves) and heroic knight statues. Built at the origin facing -Y."""
import math
import random

from mathutils import Vector

from . import env, geo
from . import shaders as S

CLOTH = ['1c6e69', '7a1e30', 'b0803a', 'd9ccb0', '3a4a6a', '5a3b24', '4a6a34', '6a3a5a', 'c8b070', '2e4a48']
SKIN = ['d0a080', 'b98d77', '9a6a50', '6e4a36']
HAIR = ['2a1c12', '4a2e1a', '8a6a3a', 'b0a080', '1a1a1a', '6a6a66']
POSES = ['down', 'cheer', 'both', 'flag', 'scarf', 'clap']


def _v(*a):
    return Vector(a)


def _mats(cloth, trim, skin, hair, legs='2e2a2a'):
    return dict(
        cloth=env.mat_cached(('figcloth', cloth), lambda: S.cloth(cloth, name=f'Fig cloth {cloth}', weave=20)),
        trim=env.mat_cached(('figcloth', trim), lambda: S.cloth(trim, name=f'Fig cloth {trim}', weave=20)),
        skin=env.mat_cached(('figskin', skin), lambda: S.flat(skin, name=f'Fig skin {skin}', rough=0.6)),
        hair=env.mat_cached(('fighair', hair), lambda: S.flat(hair, name=f'Fig hair {hair}', rough=0.8)),
        legs=env.mat_cached(('figlegs', legs), lambda: S.flat(legs, name=f'Fig legs {legs}', rough=0.85)),
    )


def person(seed=0, pose=None, cloth=None, trim=None, skin=None, hair=None, seated=True, flag=None, headwear=None):
    """A crowd figure (~1.75 m standing). Returns one joined mesh object (not linked to a visible collection)."""
    rng = random.Random(seed)
    pose = pose or rng.choice(POSES)
    cloth = cloth or rng.choice(CLOTH)
    trim = trim or rng.choice([c for c in CLOTH if c != cloth])
    skin = skin or rng.choice(SKIN)
    hair = hair or rng.choice(HAIR)
    M = _mats(cloth, trim, skin, hair)
    parts = []
    hip = 0.0 if seated else 0.9
    # legs
    for s in (-1, 1):
        if seated:
            pts = [_v(s * 0.09, 0.0, hip + 0.02), _v(s * 0.1, -0.4, hip + 0.04), _v(s * 0.1, -0.44, hip - 0.4)]
        else:
            pts = [_v(s * 0.09, 0, hip), _v(s * 0.1, -0.02, hip * 0.5), _v(s * 0.1, 0, 0.06)]
        parts.append(geo.tube('Leg', pts, [0.075, 0.06, 0.05], M['legs'], None, sides=7))
        foot = pts[-1] + _v(0, -0.06, -0.03)
        parts.append(geo.sphere('Foot', 0.06, tuple(foot), M['legs'], None, 8, 5, scale=(0.9, 1.5, 0.6)))
    # torso: tunic with a contrasting belt/sash
    prof = [(0.17, hip - 0.04), (0.16, hip + 0.12), (0.19, hip + 0.38), (0.21, hip + 0.52), (0.13, hip + 0.6),
            (0.06, hip + 0.64)]
    torso = geo.lathe('Torso', prof, 12, M['cloth'], None)
    for v in torso.data.vertices:
        v.co.y *= 0.72
    torso.data.update()
    parts.append(torso)
    parts.append(geo.lathe('Belt', [(0.165, hip + 0.1), (0.175, hip + 0.13), (0.165, hip + 0.16)], 12, M['trim'], None,
                           close_top=False, close_bottom=False, scale=(1, 0.74, 1)))
    head_c = _v(0, -0.01, hip + 0.75)
    parts.append(geo.sphere('Neck', 0.05, tuple(_v(0, 0, hip + 0.63)), M['skin'], None, 8, 5))
    parts.append(geo.quadsphere('Head', 0.105, tuple(head_c), M['skin'], None, level=2, scale=(0.92, 1.0, 1.08)))
    hw = headwear or rng.choice(['hair', 'hair', 'hood', 'cap', 'hair'])
    if hw == 'hair':
        h = geo.quadsphere('Hair', 0.112, tuple(head_c + _v(0, 0.02, 0.03)), M['hair'], None, level=2,
                           scale=(0.95, 0.95, 0.85))
        parts.append(h)
    elif hw == 'hood':
        parts.append(geo.quadsphere('Hood', 0.13, tuple(head_c + _v(0, 0.03, 0.02)), M['cloth'], None, level=2,
                                    scale=(1.0, 1.0, 1.05)))
    else:
        parts.append(geo.lathe('Cap', [(0.12, head_c.z + 0.04), (0.11, head_c.z + 0.1), (0.0, head_c.z + 0.13)], 10,
                               M['trim'], None))
    # arms: shoulder -> elbow -> hand
    sh = hip + 0.52
    arms = {
        'down': [(_v(-0.24, -0.05, sh - 0.22), _v(-0.14, -0.2, sh - 0.36)), (_v(0.24, -0.05, sh - 0.22), _v(0.14, -0.2, sh - 0.36))],
        'cheer': [(_v(-0.24, -0.05, sh - 0.22), _v(-0.14, -0.2, sh - 0.36)), (_v(0.3, -0.02, sh + 0.2), _v(0.32, -0.04, sh + 0.46))],
        'both': [(_v(-0.3, -0.02, sh + 0.2), _v(-0.36, -0.04, sh + 0.44)), (_v(0.3, -0.02, sh + 0.2), _v(0.36, -0.04, sh + 0.44))],
        'flag': [(_v(-0.24, -0.05, sh - 0.22), _v(-0.14, -0.2, sh - 0.36)), (_v(0.3, -0.04, sh + 0.16), _v(0.3, -0.1, sh + 0.4))],
        'scarf': [(_v(-0.28, -0.06, sh + 0.18), _v(-0.3, -0.1, sh + 0.42)), (_v(0.28, -0.06, sh + 0.18), _v(0.3, -0.1, sh + 0.42))],
        'clap': [(_v(-0.2, -0.16, sh - 0.12), _v(-0.03, -0.3, sh - 0.02)), (_v(0.2, -0.16, sh - 0.12), _v(0.03, -0.3, sh - 0.02))],
    }[pose]
    hands = []
    for s, (elbow, hand) in zip((-1, 1), arms):
        shoulder = _v(s * 0.2, 0, sh)
        parts.append(geo.tube('Arm', [shoulder, elbow, hand], [0.05, 0.045, 0.038], M['cloth'], None, sides=7))
        parts.append(geo.sphere('Hand', 0.042, tuple(hand), M['skin'], None, 8, 5))
        hands.append(hand)
    if pose == 'flag':
        fl = flag or M['trim']
        h = hands[1]
        parts.append(geo.tube('Flag stick', [h - _v(0, 0, 0.1), h + _v(0, 0, 0.75)], 0.012,
                              env.mat_cached(('figstick',), lambda: S.flat('4a3424', name='Flag stick')), None, sides=5))
        top = h + _v(0, 0, 0.72)
        sheet = geo.grid_sheet('Flag', 1, 1, 6, 2, lambda u, v: (top.x + 0.02 + u * 0.42, top.y + 0.05 * math.sin(u * 5),
                                                                 top.z - v * 0.26 * (1 - 0.5 * u)), fl, None)
        parts.append(sheet)
    elif pose == 'scarf':
        a, b = hands
        pts = [a.lerp(b, t) + _v(0, -0.02, -0.08 * math.sin(math.pi * t)) for t in (0, 0.25, 0.5, 0.75, 1.0)]
        parts.append(geo.tube('Scarf', pts, 0.035, M['trim'], None, sides=5, flatten=0.35))
    obj = geo.join(parts, f'Person {seed} {pose}')
    return obj


def crowd_protos(n=24, seated=True, seed=0, colors=None):
    rng = random.Random(seed)
    out = []
    for i in range(n):
        pose = POSES[i % len(POSES)] if i < len(POSES) * 2 else rng.choice(POSES)
        cloth = (colors or CLOTH)[i % len(colors or CLOTH)]
        key = f'person{seated}{seed}{i}'
        out.append(env.proto(key, lambda i=i, pose=pose, cloth=cloth: person(seed * 100 + i, pose, cloth, seated=seated)))
    return out


def knight_statue(mat, h=3.2, pose='sword', cloak=True):
    """Heroic knight effigy (feet at z=0, faces -Y): armoured figure resting both hands on a sword
    planted point-down, or holding a lantern aloft; heavy cloak behind."""
    s = h / 3.2
    parts = []

    def V(x, y, z):
        return _v(x * s, y * s, z * s)
    # legs and sabatons
    for sg in (-1, 1):
        parts.append(geo.tube('Leg', [V(sg * 0.16, 0, 1.45), V(sg * 0.18, -0.03, 0.75), V(sg * 0.17, 0, 0.1)],
                              [0.13 * s, 0.11 * s, 0.09 * s], mat, None, sides=10))
        parts.append(geo.sphere('Knee', 0.12 * s, tuple(V(sg * 0.18, -0.06, 0.78)), mat, None, 10, 6))
        parts.append(geo.sphere('Foot', 0.1 * s, tuple(V(sg * 0.17, -0.1, 0.06)), mat, None, 10, 6,
                                scale=(0.9, 1.7, 0.6)))
    # skirt of the surcoat
    parts.append(geo.lathe('Surcoat skirt', [(0.34 * s, 0.75 * s), (0.36 * s, 1.0 * s), (0.31 * s, 1.5 * s),
                                             (0.29 * s, 1.6 * s)], 20, mat, None, close_top=False, close_bottom=False,
                           scale=(1, 0.78, 1)))
    # torso / breastplate
    parts.append(geo.lathe('Torso', [(0.29 * s, 1.55 * s), (0.31 * s, 1.85 * s), (0.36 * s, 2.15 * s),
                                     (0.37 * s, 2.32 * s), (0.2 * s, 2.45 * s), (0.1 * s, 2.5 * s)], 20, mat, None,
                           scale=(1, 0.7, 1)))
    parts.append(geo.lathe('Belt', [(0.3 * s, 1.55 * s), (0.315 * s, 1.6 * s), (0.3 * s, 1.65 * s)], 20, mat, None,
                           close_top=False, close_bottom=False, scale=(1, 0.72, 1)))
    for sg in (-1, 1):
        parts.append(geo.quadsphere('Pauldron', 0.19 * s, tuple(V(sg * 0.4, 0.0, 2.3)), mat, None, level=3,
                                    scale=(1.15, 1.0, 0.75)))
    # head with great helm and crest
    parts.append(geo.lathe('Helm', [(0.0, 2.92 * s), (0.13 * s, 2.9 * s), (0.17 * s, 2.78 * s), (0.17 * s, 2.58 * s),
                                    (0.14 * s, 2.5 * s), (0.0, 2.49 * s)], 18, mat, None))
    parts.append(geo.box('Visor', (0.24 * s, 0.03 * s, 0.03 * s), tuple(V(0, -0.165, 2.73)), mat, None, bevel=0.005))
    crest = [(y * s, z * s) for y, z in ((-0.12, 2.86), (-0.06, 3.1), (0.06, 3.14), (0.16, 2.92))]
    parts.append(geo.extrude('Crest', crest, 0.04 * s, mat, None, plane='YZ', bevel=0))
    if pose == 'sword':
        hands = V(0, -0.34, 1.62)
        for sg in (-1, 1):
            parts.append(geo.tube('Arm', [V(sg * 0.42, 0, 2.22), V(sg * 0.38, -0.18, 1.86), hands + V(sg * 0.05, 0, 0)],
                                  [0.1 * s, 0.09 * s, 0.075 * s], mat, None, sides=10))
            parts.append(geo.sphere('Gauntlet', 0.08 * s, tuple(hands + V(sg * 0.05, 0, 0)), mat, None, 10, 6))
        parts.append(geo.box('Blade', (0.11 * s, 0.035 * s, 1.5 * s), tuple(V(0, -0.36, 0.8)), mat, None, bevel=0.01))
        parts.append(geo.box('Guard', (0.5 * s, 0.07 * s, 0.07 * s), tuple(V(0, -0.36, 1.55)), mat, None, bevel=0.01))
        parts.append(geo.cylinder('Grip', 0.035 * s, 0.22 * s, tuple(V(0, -0.36, 1.72)), mat, None, 8, bevel=0))
        parts.append(geo.sphere('Pommel', 0.06 * s, tuple(V(0, -0.36, 1.86)), mat, None, 10, 6))
    else:
        for sg, (elbow, hand) in zip((-1, 1), ((V(-0.42, -0.1, 1.85), V(-0.18, -0.3, 1.7)),
                                               (V(0.55, -0.05, 2.55), V(0.5, -0.08, 2.95)))):
            parts.append(geo.tube('Arm', [V(sg * 0.42, 0, 2.22), elbow, hand], [0.1 * s, 0.09 * s, 0.075 * s], mat,
                                  None, sides=10))
            parts.append(geo.sphere('Gauntlet', 0.08 * s, tuple(hand), mat, None, 10, 6))
        lc = V(0.5, -0.1, 2.75)
        parts.append(geo.lathe('Lantern', [(0, -0.22 * s), (0.11 * s, -0.18 * s), (0.12 * s, 0.08 * s),
                                           (0.06 * s, 0.16 * s), (0, 0.18 * s)], 8, mat, None, location=tuple(lc)))
    if cloak:
        def fn(u, v):
            a = math.pi * (0.15 + 0.7 * u)
            r = (0.42 + 0.22 * v) * s
            x = -math.cos(a) * r
            y = math.sin(a) * r * 0.75 + 0.06 * s
            z = (2.38 - 2.25 * v) * s
            y += 0.04 * s * math.sin(u * 14) * v
            return (x, y, z)
        cl = geo.grid_sheet('Cloak', 1, 1, 14, 10, fn, mat, None)
        mod = cl.modifiers.new('Thick', 'SOLIDIFY')
        mod.thickness = 0.05 * s
        geo.apply_modifiers(cl)
        parts.append(cl)
    o = geo.join(parts, 'Knight statue')
    for p in o.data.polygons:
        p.use_smooth = True
    return o
