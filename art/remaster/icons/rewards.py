"""Reward icons: currencies and reward kinds."""
import random

from mathutils import Vector

from rk import geo
from . import itemkit as P
from .base import icon


def crown_relief(name, center, normal, up, size, mat, coll, depth=0.012):
    """Small three-point crown relief for coin faces / seals."""
    n = Vector(normal).normalized()
    u = Vector(up).normalized()
    r = u.cross(n).normalized()
    outline = [(-0.5, -0.35), (0.5, -0.35), (0.55, 0.35), (0.28, 0.05), (0.0, 0.5), (-0.28, 0.05), (-0.55, 0.35)]
    o = geo.extrude(name, [(x * size, y * size) for x, y in outline], depth, mat, coll, plane='XZ', bevel=depth * .3)
    # map XZ plane -> (r, u), Y -> n
    me = o.data
    c = Vector(center)
    for v in me.vertices:
        x, y, z = v.co
        v.co = c + r * x + u * z - n * y
    me.update()
    geo.store_rest(o)
    return o


def gold_coin(name, loc, rot, K, r=0.22, t=0.045, emblem=True):
    g = K.gilt('eab64a', 0.26)
    objs = [P.coin(name, r, t, g, K.coll, seg=40)]
    if emblem:
        objs.append(crown_relief(name + ' crown', (0, 0, t * .36), (0, 0, 1), (0, 1, 0), r * .62, g, K.coll, depth=t * .5))
        objs.append(P.torus(name + ' bead ring', r * .8, t * .08, g, K.coll, seg=40, sides=6, location=(0, 0, t * .4)))
    o = geo.join(objs, name) if len(objs) > 1 else objs[0]
    geo.place(o, loc, rot)
    return o


def coin_stack(name, base, n, K, r=0.2, t=0.045, seed=0, lean=0.0):
    rnd = random.Random(seed)
    for i in range(n):
        off = (rnd.uniform(-.012, .012) + lean * i * .004, rnd.uniform(-.012, .012), 0)
        gold_coin(f'{name} {i}', (base[0] + off[0], base[1] + off[1], base[2] + i * t * 1.02),
                  (rnd.uniform(-2, 2), rnd.uniform(-2, 2), rnd.uniform(0, 360)), K, r=r, t=t, emblem=False)


@icon('rewards', 'gold', bloom=0.4)
def gold(K):
    rnd = random.Random(5)
    coin_stack('Stack back', (-0.08, 0.22, 0), 14, K, seed=1)
    coin_stack('Stack left', (-0.38, 0.0, 0), 10, K, seed=2, lean=-1)
    coin_stack('Stack right', (0.34, 0.1, 0), 11, K, seed=3, lean=1)
    # loose coins on the ground
    for k, (x, y, rx, ry) in enumerate([(-0.46, -0.3, 6, -8), (0.44, -0.28, -4, 10), (0.15, -0.5, 8, 4),
                                        (-0.18, -0.52, -10, -6)]):
        gold_coin(f'Loose coin {k}', (x, y, 0.02), (rx, ry, rnd.uniform(0, 360)), K, emblem=False)
    # hero coin standing up front
    gold_coin('Hero coin', (0.0, -0.32, 0.3), (74, 0, 6), K, r=0.3, t=0.06)
    P.gem_cut('Coin ruby', 0.07, P.crystal('d0102a', deep='3a0008', glow=1.4, name='Ruby'), K.coll,
              location=(0.38, -0.42, 0.06), rotation=(20, 10, 0), facets=10)
    P.star_flare('Glint', (0.17, -0.42, 0.47), 0.13, K.spark_mat('ffe9b0', hot='fffdf4', strength=6), K.coll,
                 facing=(0.35, -1, 0.3))
    return dict(el=22)
