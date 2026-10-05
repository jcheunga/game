"""King's Road: a cobbled high street on a golden afternoon. Cottages, gardens and market stalls line the road;
behind them the town wall and its rooftops; beyond, farmland rolls up to the king's castle under hazy hills."""
import math
import random

from rk import arch, env
from rk import dressing as P

from . import common as CM

MOOD = dict(zenith='2f5f99', horizon='f7c98c', mid='f9dcae', upper='86b4e4', clouds=0.9, cloud_lit='fff4e2',
            cloud_dark='a898a8', cloud_scale=1.3, sun='ffd8a4', strength=4.5, sky_light=0.62, glow='ffe0a8', glow_amount=0.9)
LOOK = dict(far=dict(saturation=1.04, bloom=0.14, split=0.08), mid=dict(saturation=1.0, split=0.08),
            near=dict(saturation=1.06, split=0.1, contrast=1.02))

FIELDS = dict(grass=('5d6a34', '7a8540', 'a29c52'), dry='caa95e', dirt=('7a5e40', '5c4632'))


def _trees(prefix, n, h, palette=('2f4220', '56692a', '98983c'), kind='broad'):
    if kind == 'conifer':
        return CM.protos(prefix, n, lambda i: env.conifer(400 + i, h, ('22381f', '35532c', '62743a')))
    return CM.protos(prefix, n, lambda i: env.broadleaf(300 + i * 7, h * (0.85 + 0.12 * i), 1.0 + 0.1 * i, palette))


# ------------------------------------------------------------------ far: farmland, the castle, hazy ranges
def far(L, scene, mood):
    castle_x, castle_y = L.X(650, 1900), 1900.0
    village_x, village_y = L.X(255, 640), 640.0

    def height(x, y):
        z = env.fbm(x, y, 0.0035, 4, 21) * 16 + 30 * env.smoothstep(700, 3400, y)
        z += 34 * math.exp(-((x - castle_x) ** 2 + (y - castle_y) ** 2) / (2 * 260 ** 2))
        return z
    fields = CM.patchwork('Far farmland', ('d0a650', '6f8a3a', 'a8a24e', '557334', 'e0bc66', '86703e', '4f6a30', '9aa45a'))
    env.terrain((3400, 1600), (340, 180), (0, 900), height, mat=fields, name='Farmland near')
    env.terrain((9000, 3800), (300, 120), (0, 3500), height, mat=fields, name='Farmland far')
    env.far_ground(radius=12000, inner=5300, z=40, fog=mood['horizon'], fog_dist=1e7, color=('6a7444', '8a8a52'))
    env.mountains(10, 6200, (170, 300), arc=(-0.55, 0.55), base_z=20, seed=12, direction=90, fog=mood['horizon'],
                  fog_dist=1e7, rock=('6a6a70', '84828a'), green='6a7650', jag=0.35)
    # woods and hedgerow trees on the rolling fields
    woods = _trees('fcwood', 4, 13.0)
    rng = random.Random(3)
    for cluster in range(80):
        cy = rng.uniform(650, 2800)
        cx = rng.uniform(-L.half_width(cy), L.half_width(cy))
        if abs(cx - castle_x) < 140 and abs(cy - castle_y) < 160:
            continue
        for _ in range(rng.randint(4, 14)):
            x, y = cx + rng.gauss(0, 22 + cy * 0.02), cy + rng.gauss(0, 18 + cy * 0.015)
            env.inst(rng.choice(woods), (x, y, height(x, y) - 0.5), rng.uniform(0, 6.28), rng.uniform(0.8, 1.3),
                     env.C['Vegetation'])
    # the king's castle on its hill
    z0 = height(castle_x, castle_y)
    M = arch.mats('grey')
    corners = [(-52, -36), (52, -36), (52, 36), (-52, 36)]
    for (ax, ay), (bx, by) in zip(corners, corners[1:] + corners[:1]):
        arch.wall((castle_x + ax, castle_y + ay), (castle_x + bx, castle_y + by), 13.0, 4.0, style='grey', M=M, base_z=z0 - 1)
    for i, (ax, ay) in enumerate(corners):
        arch.tower((castle_x + ax, castle_y + ay, z0 - 1), r=7.5, h=24.0, roof='cone', style='grey', seed=i, lit=0.3, M=M)
    arch.tower((castle_x + 8, castle_y + 6, z0 - 1), r=13.0, h=38.0, roof='cone', style='grey', seed=9, lit=0.4, M=M)
    arch.tower((castle_x - 22, castle_y + 14, z0 - 1), r=6.0, h=46.0, roof='cone', style='grey', seed=11, lit=0.3, M=M)
    arch.tower((castle_x + 30, castle_y + 18, z0 - 1), r=5.0, h=40.0, roof='cone', style='grey', seed=13, lit=0.3, M=M)
    arch.gatehouse((castle_x, castle_y - 36, z0 - 1), 0.0, w=20, d=12, h=17, style='grey', M=M)
    # a village with its church on the left
    W = arch.mats('warm')
    zv = height(village_x, village_y)
    for i in range(16):
        a = rng.uniform(0, 6.28)
        r = rng.uniform(10, 70)
        hx, hy = village_x + math.cos(a) * r, village_y + math.sin(a) * r * 0.6
        arch.house((hx, hy, height(hx, hy) - 0.3), rng.uniform(-0.4, 0.4), rng.uniform(6, 9), rng.uniform(5, 7),
                   floors=rng.choice((1, 2)), seed=60 + i, style='warm', lit=0.0, M=W)
    arch.tower((village_x + 8, village_y + 12, zv - 0.5), r=3.2, h=18.0, roof='cone', style='warm', seed=5, lit=0.0, M=W)


# ------------------------------------------------------------------ mid: town wall and rooftops
def mid(L, scene, mood):
    rng = random.Random(8)
    town = env.ground('Town yards', grass=('5f6a3c', '74803e', '948e52'), dry='ad9658', dirt=('8a6c4a', '6a5038'),
                      dry_amount=0.25, flowers=0.0)
    CM.ground(L, town, -12, 40, fade=(20.0, 26.0))
    M = arch.mats('warm')
    G = arch.mats('grey')
    # crenellated town wall with towers, the skyline the castle shows over
    wy = 16.0
    arch.wall((L.x0 - 10, wy), (L.x1 + 10, wy), 4.5, 2.2, style='grey', M=G)
    for i, x in enumerate(L.row(6, rng, 0.25)):
        arch.tower((x, wy + 0.6, 0), r=2.7, h=6.2, roof='cone', roof_h=3.0, style='grey', seed=i, lit=0.4, M=G)
    # rooftops in front of the wall, broken by gardens and trees
    runs = [(x - w / 2, x + w / 2) for x, w in CM.spans(L, rng, (14, 30), (8, 18))]
    CM.houses(L, rng, 4.0, floors=(1, 1, 2), lit=0.5, seed=3, x_runs=runs, M=M)
    trees = _trees('mctree', 4, 6.5)
    CM.scatter(L, trees, 30, (3.0, 14.0), seed=4, scale=(0.85, 1.15), min_gap=4.5)
    # the town church, the tallest accent
    cx = L.X(380)
    # the parish church's spired bell tower rises over the roofs
    arch.tower((cx, 7.0, 0), r=2.2, h=8.0, roof='cone', roof_h=6.5, style='grey', seed=93, lit=0.6, M=G)


# ------------------------------------------------------------------ near: the high street
def near(fr, scene, mood):
    rng = random.Random(1)
    road = lambda x, y: env.smoothstep(fr.band_near - 0.6, fr.band_near + 0.4, y) * \
        (1 - env.smoothstep(fr.band_far - 0.4, fr.band_far + 0.6, y))
    walk_back = lambda x, y: 1.0 if fr.band_far < y < fr.back_y + 1.2 else 0.0
    walk_front = lambda x, y: 1.0 if fr.front_y - 1.6 < y < fr.band_near else 0.0
    mat = env.ground('High street', grass=('5f6a3c', '74803e', '948e52'), dry='ad9658', dirt=('8a6c4a', '6a5038'),
                     dry_amount=0.2, flowers=0.6, cobble=('8f8576', '6e665a'), cobble_scale=4.6)
    CM.ground(fr, mat, fr.near_y - 4, fr.back_y + 12, fade=(fr.back_y + 3.6, fr.back_y + 6.8), masks={
        'cobble': lambda x, y: max(road(x, y), walk_back(x, y), walk_front(x, y)), 'path': lambda x, y: 0.0})
    M = arch.mats('warm')
    PM = P.pm()
    row = fr.back_y + 1.8
    # a low frontage: garden walls, hedges, stalls and carts, so the town and the hills show above it
    open_spots = [(x - w / 2, x + w / 2) for x, w in CM.spans(fr, rng, (9, 16), (2.5, 6.0))]
    for i, (a, b) in enumerate(open_spots):
        arch.wall((a, row + 1.4), (b, row + 1.4), 1.0, 0.5, crenels=False, M=M, walk=False)
        mid_x = (a + b) / 2
        kind = rng.random()
        if kind < 0.45:
            for k in range(int((b - a) // 3.4)):
                P.stall((a + 1.8 + k * 3.4, row - 0.2, 0), math.pi, w=2.8, d=1.4, seed=i + k, M=PM)
        elif kind < 0.7:
            P.well((mid_x, row, 0), 0.0, PM)
            P.bench((mid_x + 2.4, row - 0.3, 0), 0.0, M=PM)
        else:
            P.cart((mid_x, row, 0), 0.15, PM, seed=i, load='sacks')
            P.barrel((mid_x + 2.0, row + 0.2, 0), 0.3, M=PM)
            P.crate((mid_x - 2.0, row + 0.3, 0), 0.2, 0.7, PM)
    hedge = CM.protos('nchedge', 3, lambda i: env.bush(200 + i, 0.8 + 0.15 * i, ('3e5a26', '6a8a36', 'a6b04c'),
                                                         flowers=('e8d8f0', 'f0c8d8') if i == 1 else None))
    for a, b in zip([x for _, x in open_spots[:-1]], [x for x, _ in open_spots[1:]]):
        x = a + 0.4
        while x < b - 0.2:
            env.inst(hedge[int(x * 7) % 3], (x, row + 1.6, 0), x, 0.85, env.C['Vegetation'])
            x += 1.1
    trees = _trees('nctree', 3, 4.4)
    for a, b in open_spots[1::3]:
        env.inst(trees[int(a) % 3], ((a + b) / 2 + 2.0, row + 3.0, 0), a, 1.0, env.C['Vegetation'])
    for x in fr.row(12, rng, 0.2):
        P.lantern_post((x, fr.back_y + 0.4, 0), 0.0, h=2.6, M=PM, power=14)
    for x in fr.row(5, random.Random(4), 0.3):
        P.banner_pole((x, fr.back_y + 1.2, 0), 0.0, PM['teal'], h=4.4, width=0.85, drop=1.8, M=PM)
    # in front of the road: kerb, flower verge and a picket fence, kept below the band
    arch.wall((fr.x0 - 6, fr.front_y - 1.9), (fr.x1 + 6, fr.front_y - 1.9), 0.3, 0.4, crenels=False, M=M, walk=False)
    P.fence((fr.x0 - 6, fr.front_y - 6.0), (fr.x1 + 6, fr.front_y - 6.0), h=0.9 * fr.fit(fr.front_y - 6.0, 0.9), M=PM,
            posts=2.2, rails=2)
    bushes = CM.protos('ncbush', 3, lambda i: env.bush(70 + i, 0.8 + 0.2 * i, ('44602a', '72923a', 'b2b852'),
                                                        flowers=('f4ecd8', 'e8b8c8', 'e8c84a')[i:i + 1] * 2))
    CM.front_scatter(fr, bushes, 12, (fr.front_y - 12, fr.front_y - 3.2), 1.1, seed=3, min_gap=3.5, coll='Vegetation')
    stones = CM.protos('ncstone', 3, lambda i: env.rock(90 + i, 0.5, moss=0.2))
    CM.front_scatter(fr, stones, 10, (fr.front_y - 12, fr.front_y - 3.0), 0.4, seed=8, min_gap=3.0)
    tuft = CM.protos('nctuft', 3, lambda i: env.grass_tuft(20 + i, 0.5))
    CM.front_scatter(fr, tuft, 140, (fr.front_y - 14, fr.front_y - 2.6), 0.5, seed=6, min_gap=0.7, coll='Vegetation')
