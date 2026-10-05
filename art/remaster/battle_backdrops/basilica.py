"""Hollow Basilica: a flagstone processional at dusk. Gothic piers, saints on plinths and candles line it; behind,
a cloister arcade and a graveyard of cypresses; far off, the great ruined basilica on its hill."""
import math
import random

from rk import arch, env
from rk import dressing as P
from battle_scenes import kit as K

from . import common as CM

MOOD = dict(zenith='262a4e', horizon='f2b07c', mid='e6a586', upper='65588a', clouds=0.75, cloud_lit='ffb48c',
            cloud_dark='463a58', cloud_scale=1.1, sun='ffb47e', strength=3.7, sky_light=0.55, glow='ffc08a',
            glow_amount=1.15, glow_el=3.5)
LOOK = dict(far=dict(saturation=1.04, bloom=0.18, split=0.1), mid=dict(saturation=1.0, bloom=0.1, split=0.1),
            near=dict(saturation=1.02, split=0.12, contrast=1.03, bloom=0.12))
FOG = dict(far_dist=1400.0, mid=(0.24, 0.005, 0.6))
CYPRESS = ('1c2c1e', '2e4228', '56663a')
FLAGS = dict(grass=('66645c', '76736a', '86827a'), dry='86827a', dirt=('5e5650', '46403a'))


# ------------------------------------------------------------------ far: the great basilica on its hill
def far(L, scene, mood):
    bx, by = L.X(420, 1800), 1800.0

    def height(x, y):
        z = env.fbm(x, y, 0.004, 4, 61) * 16 + 30 * env.smoothstep(700, 3200, y)
        return z + 38 * math.exp(-((x - bx) ** 2 + (y - by) ** 2) / (2 * 280 ** 2))
    hills = env.ground('Dusk hills', grass=('4a5434', '5e6638', '7a7444'), dry='8a7a4a', dirt=('5a4a38', '40362a'),
                       dry_amount=0.4, flowers=0.0, scale=0.1)
    env.terrain((3600, 1700), (320, 160), (0, 980), height, mat=hills, name='Hills near')
    env.terrain((9000, 4000), (260, 110), (0, 3700), height, mat=hills, name='Hills far')
    env.far_ground(radius=12000, inner=5600, z=30, fog=mood['horizon'], fog_dist=1e7, color=('4a4a38', '5e5a40'))
    env.mountains(9, 6400, (160, 280), arc=(-0.6, 0.6), base_z=10, seed=43, direction=90, fog=mood['horizon'],
                  fog_dist=1e7, rock=('5a5462', '74707a'), green='5a5e4a', jag=0.45)
    M = arch.mats('grey')
    z0 = height(bx, by)
    # nave walls with pointed buttresses, two west towers and a broken crossing spire
    arch.wall((bx - 60, by), (bx + 40, by), 26.0, 4.0, crenels=False, style='grey', M=M, base_z=z0 - 2, walk=False)
    arch.wall((bx - 60, by + 26), (bx + 40, by + 26), 26.0, 4.0, crenels=False, style='grey', M=M, base_z=z0 - 2, walk=False)
    for k in range(7):
        x = bx - 55 + k * 15
        arch.tower((x, by - 3, z0 - 2), r=2.2, h=22.0, roof='cone', roof_h=6.0, style='grey', seed=k, lit=0.0, M=M)
    for dx in (-72, -52):
        arch.spire((bx + dx, by + 13, z0 - 2), r=7.0, h=58.0, M=M, style='grey', seed=int(-dx), ruin=0.12, lit=0.6)
    arch.spire((bx + 12, by + 13, z0 - 2), r=8.0, h=48.0, M=M, style='grey', seed=5, ruin=0.45, lit=0.3)
    cypress = CM.tree_protos('bsfar', 3, 16.0, CYPRESS, kind='cypress', seed=520)
    CM.woods(L, cypress, 70, (600, 2600), height, seed=4, count=(2, 7), spread=(30, 24),
             avoid=lambda x, y: abs(x - bx) < 130 and abs(y - by) < 90)


# ------------------------------------------------------------------ mid: cloister arcade and the graveyard
def mid(L, scene, mood):
    rng = random.Random(7)
    yard = env.ground('Cloister garth', grass=('4c5634', '5e6838', '7c7a46'), dry='8a7c4a', dirt=('5a4a38', '40362a'),
                      dry_amount=0.3, flowers=0.0)
    CM.ground(L, yard, -12, 40, fade=(20.0, 26.0))
    M = arch.mats('grey')
    PM = P.pm()
    ay = 15.0
    arch.arcade((L.x1 + 12, ay), (L.x0 - 12, ay), bays=24, h=5.2, depth=3.6, style='grey', M=M, lit=0.6, seed=6)
    for x in L.row(3, rng, 0.25):
        arch.spire((x, ay + 6, 0), r=2.6, h=13.0, M=M, style='grey', seed=int(x), ruin=0.25, lit=0.5)
    stones = CM.protos('bsgrave', 4, lambda i: CM.one(P.gravestone((0, 0, 0), 0.0, seed=i, M=PM)))
    CM.scatter(L, stones, 90, (2.0, 12.0), seed=3, scale=(0.9, 1.2), min_gap=1.6, coll='Props', rot=False)
    cypress = CM.tree_protos('bsmid', 3, 9.0, CYPRESS, kind='cypress', seed=540)
    CM.scatter(L, cypress, 26, (3.0, 12.5), seed=9, scale=(0.85, 1.2), min_gap=5.0)


# ------------------------------------------------------------------ near: the processional
def near(fr, scene, mood):
    rng = random.Random(5)
    mat = env.ground('Processional', **FLAGS, dry_amount=0.0, flowers=0.0, cobble=('8e8a80', '6c6860'), cobble_scale=2.6)
    CM.ground(fr, mat, fr.near_y - 4, fr.back_y + 12, fade=(fr.back_y + 3.8, fr.back_y + 7.0), masks={
        'cobble': lambda x, y: 1.0 if y > fr.front_y - 1.2 else 0.0, 'path': lambda x, y: 0.0})
    M = arch.mats('grey')
    PM = P.pm()
    row = fr.back_y + 1.8
    # a balustrade broken by piers, saints and candle shrines
    arch.wall((fr.x0 - 10, row + 1.4), (fr.x1 + 10, row + 1.4), 0.95, 0.45, crenels=False, style='grey', M=M, walk=False)
    xs = [x for x, _ in CM.spans(fr, rng, (5, 9), (2, 5))]
    stone = env.smooth_stone('a8a090', 'Saint stone')
    for i, x in enumerate(xs):
        kind = i % 5
        if kind in (0, 3):
            K.gothic_pier((x, row + 1.0, 0), h=rng.uniform(3.6, 4.6), r=0.42)
        elif kind == 1:
            P.statue((x, row + 0.9, 0), 0.0, mat=stone, h=2.6, pose='sword', M=PM)
        elif kind == 2:
            K.candle_cluster((x, row + 0.6, 0), n=9, seed=i)
            K.candle_cluster((x + 0.8, row + 0.9, 0), n=5, seed=i + 7)
        else:
            K.shrine_altar((x, row + 0.8, 0), 0.0, M=PM, seed=i)
    cypress = CM.tree_protos('bsnear', 2, 4.8, CYPRESS, kind='cypress', seed=560)
    for x in xs[2::4]:
        env.inst(cypress[int(x) % 2], (x + 2.0, row + 2.8, 0), x, 1.0, env.C['Vegetation'])
    for x in fr.row(9, random.Random(2), 0.25):
        P.torch((x, fr.back_y + 0.3, 0), h=2.0, M=PM, power=40)
    # in front: broken pews, rubble and candles, kept below the band
    graves = CM.protos('bsfgrave', 4, lambda i: CM.one(P.gravestone((0, 0, 0), 0.0, seed=10 + i, M=PM)))
    CM.front_scatter(fr, graves, 16, (fr.front_y - 12, fr.front_y - 3.5), 1.2, seed=11, min_gap=2.6)
    for x in fr.row(6, random.Random(4), 0.4):
        K.rubble((x, fr.front_y - 9.0, 0), r=1.3, n=10, seed=int(x * 3) % 97)
        K.candle_cluster((x + 2.0, fr.front_y - 8.0, 0), n=5, seed=int(x) % 97)
