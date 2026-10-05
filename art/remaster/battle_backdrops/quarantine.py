"""Ashen Ward: a quarantine yard under a sick overcast. Barricades, plague vats and green braziers stand behind
the road; beyond them the ward's grim inner wall and its watchtowers; far off, a walled plague city on its hill."""
import math
import random

from rk import arch, env, geo
from rk import dressing as P
from battle_scenes import kit as K

from . import common as CM

MOOD = dict(zenith='44505a', horizon='c4c8ac', mid='b4bca6', upper='748288', clouds=1.0, cloud_lit='dce0cc',
            cloud_dark='4c545a', cloud_scale=0.9, sun='e8eed2', strength=3.1, sky_light=0.85, glow='e8ecd0',
            glow_amount=0.35)
LOOK = dict(far=dict(saturation=0.92, split=0.06), mid=dict(saturation=0.92, split=0.06),
            near=dict(saturation=0.96, split=0.08, contrast=1.03, bloom=0.1))
FOG = dict(far_dist=1100.0, mid=(0.28, 0.005, 0.65))
PLAGUE = '9cf25c'


# ------------------------------------------------------------------ far: the plague city and dead hills
def far(L, scene, mood):
    city_x, city_y = L.X(560, 1900), 1900.0

    def height(x, y):
        z = env.fbm(x, y, 0.004, 4, 41) * 14 + 30 * env.smoothstep(700, 3200, y)
        return z + 40 * math.exp(-((x - city_x) ** 2 + (y - city_y) ** 2) / (2 * 300 ** 2))
    moor = env.ground('Grey moor', grass=('505646', '5e6450', '72745a'), dry='72745a', dirt=('4a4438', '38322a'),
                      dry_amount=0.0, flowers=0.0, scale=0.1)
    env.terrain((3600, 1700), (320, 160), (0, 980), height, mat=moor, name='Moor near')
    env.terrain((9000, 4000), (260, 110), (0, 3700), height, mat=moor, name='Moor far')
    env.far_ground(radius=12000, inner=5600, z=30, fog=mood['horizon'], fog_dist=1e7, color=('50564a', '62664e'))
    env.mountains(9, 6400, (130, 230), arc=(-0.6, 0.6), base_z=10, seed=23, direction=90, fog=mood['horizon'],
                  fog_dist=1e7, rock=('5a5c5c', '6e7070'), green='5a6052', jag=0.3)
    M = arch.mats('grim')
    z0 = height(city_x, city_y)
    ring = [(math.cos(a) * 120, math.sin(a) * 70) for a in [i * math.tau / 10 for i in range(10)]]
    for (ax, ay), (bx, by) in zip(ring, ring[1:] + ring[:1]):
        arch.wall((city_x + ax, city_y + ay), (city_x + bx, city_y + by), 14.0, 4.0, style='grim', M=M, base_z=z0 - 2)
    for i, (ax, ay) in enumerate(ring[::2]):
        arch.tower((city_x + ax, city_y + ay, z0 - 2), r=7.0, h=24.0, roof='cone', style='grim', seed=i, lit=0.6, M=M)
    rng = random.Random(4)
    for i in range(22):
        a, r = rng.uniform(0, 6.28), rng.uniform(0, 95)
        hx, hy = city_x + math.cos(a) * r, city_y + math.sin(a) * r * 0.55
        arch.house((hx, hy, height(hx, hy) - 0.5), rng.uniform(-0.3, 0.3), rng.uniform(8, 12), rng.uniform(6, 9),
                   floors=rng.choice((2, 3)), seed=70 + i, style='grim', lit=0.5, M=M)
    arch.tower((city_x + 10, city_y + 10, z0 - 2), r=9.0, h=52.0, roof='cone', roof_h=14.0, style='grim', seed=9,
               lit=1.0, M=M)
    env.point((city_x + 10, city_y + 10, z0 + 46), power=3e5, color=PLAGUE, radius=5.0, name='Plague beacon')
    dead = CM.tree_protos('qrdead', 3, 9.0, kind='dead', seed=640)
    CM.woods(L, dead, 60, (650, 2800), height, seed=7, count=(3, 9),
             avoid=lambda x, y: abs(x - city_x) < 170 and abs(y - city_y) < 130)


# ------------------------------------------------------------------ mid: the ward's inner wall and watchtowers
def mid(L, scene, mood):
    rng = random.Random(6)
    flags = env.ground('Ward yard', grass=('5a5a50', '66665a', '76745e'), dry='76745e', dirt=('5a5248', '423c34'),
                       dry_amount=0.0, flowers=0.0)
    CM.ground(L, flags, -12, 40, fade=(20.0, 26.0))
    M = arch.mats('grim')
    PM = P.pm()
    wy = 15.0
    arch.wall((L.x0 - 10, wy), (L.x1 + 10, wy), 5.0, 2.2, style='grim', M=M)
    for i, x in enumerate(L.row(6, rng, 0.25)):
        arch.tower((x, wy + 0.6, 0), r=2.8, h=7.0, roof='cone', roof_h=3.0, style='grim', seed=i, lit=0.7, M=M)
    for x in L.row(5, random.Random(3), 0.3):
        K.iron_door((x, wy - 1.15, 0), 0.0, w=2.4, h=3.4, M=PM)
    runs = [(x - w / 2, x + w / 2) for x, w in CM.spans(L, rng, (12, 22), (8, 16))]
    CM.houses(L, rng, 4.0, style='grim', floors=(1, 2), lit=0.5, seed=9, x_runs=runs, M=M)
    for x in L.row(4, random.Random(8), 0.3):
        P.brazier((x, 9.0, 0), PM, power=500, color=PLAGUE, h=1.0)
    dead = CM.tree_protos('qmdead', 3, 6.5, kind='dead', seed=660)
    CM.scatter(L, dead, 14, (2.0, 13.0), seed=5, min_gap=6.0)


# ------------------------------------------------------------------ near: the quarantine yard
def near(fr, scene, mood):
    rng = random.Random(3)
    road = lambda x, y: env.smoothstep(fr.band_near - 0.6, fr.band_near + 0.4, y) * \
        (1 - env.smoothstep(fr.band_far - 0.4, fr.band_far + 0.6, y))
    mat = env.ground('Ward flags', grass=('585a4c', '64665a', '74725e'), dry='74725e', dirt=('5a5248', '423c34'),
                     dry_amount=0.0, flowers=0.0, cobble=('7c786c', '5c5850'), cobble_scale=3.8)
    CM.ground(fr, mat, fr.near_y - 4, fr.back_y + 12, fade=(fr.back_y + 3.8, fr.back_y + 7.0), masks={
        'cobble': lambda x, y: max(road(x, y), 1.0 if y > fr.band_far else 0.0), 'path': lambda x, y: 0.0})
    M = arch.mats('grim')
    PM = P.pm()
    row = fr.back_y + 1.6
    spots = [(x - w / 2, x + w / 2) for x, w in CM.spans(fr, rng, (8, 14), (3, 7))]
    for i, (a, b) in enumerate(spots):
        mid_x = (a + b) / 2
        kind = i % 4
        if kind == 0:
            arch.wall((a, row + 1.6), (b, row + 1.6), 1.6, 0.6, crenels=False, style='grim', M=M, walk=False)
            K.iron_door((mid_x, row + 1.25, 0), 0.0, w=1.8, h=1.6, M=PM)
        elif kind == 1:
            K.vat((mid_x - 1.2, row + 0.6, 0), r=0.8, M=PM, power=90)
            K.vat((mid_x + 1.0, row + 1.0, 0), r=0.65, M=PM, power=70)
        elif kind == 2:
            K.cheval((mid_x, row + 0.4, 0), 0.05, L=3.4, M=PM)
            P.brazier((mid_x + 2.6, row + 0.6, 0), PM, power=260, color=PLAGUE, h=1.0)
        else:
            K.alembic_table((mid_x, row + 0.5, 0), 0.0, M=PM, seed=i)
            P.cart((mid_x + 2.8, row + 0.8, 0), 0.2, PM, seed=i, load='sacks')
    wood = env.planks('Coffin wood', '3a2c22', board=0.14, length=2.0)
    for x in fr.row(6, random.Random(9), 0.35):
        for k in range(rng.randint(2, 3)):
            o = geo.box('Coffin', (1.9, 0.62, 0.42), (x + rng.uniform(-0.1, 0.1), row + 2.3 + rng.uniform(-0.05, 0.05), 0.21 + k * 0.43),
                        wood, None, bevel=0.03)
            env.finish(o, coll=env.C['Props'])
        P.sack((x + 1.5, row + 2.0, 0), 0.4, 0.6, PM, seed=int(x) % 97)
    for x in fr.row(3, random.Random(10), 0.3):
        out = []
        for dx in (-1.4, 1.4):
            arch.beam((x + dx, row + 2.6, 0), (x + dx, row + 2.6, 3.4), 0.22, PM['wood_dark'], out, 'Gallows post')
        arch.beam((x - 1.6, row + 2.6, 3.3), (x + 1.6, row + 2.6, 3.3), 0.2, PM['wood_dark'], out, 'Gallows beam')
    for x in fr.row(10, rng, 0.2):
        P.lantern_post((x, fr.back_y + 0.3, 0), 0.0, h=2.6, M=PM, power=12, color='b8f088')
    # in front: barricades and chalked ward circles, kept below the band
    for x in fr.row(7, random.Random(5), 0.3):
        K.cheval((x, fr.front_y - 2.8, 0), rng.uniform(-0.15, 0.15), L=2.8 * fr.fit(fr.front_y - 2.8, 1.2), M=PM)
    for x in fr.row(4, random.Random(6), 0.3):
        K.ward_sigil((x, fr.front_y - 8.0, 0.02), r=1.2)
        K.candle_cluster((x + 1.7, fr.front_y - 7.6, 0), n=5, seed=int(x * 3) % 97)
    sacks = CM.protos('qrsack', 2, lambda i: CM.one(P.sack((0, 0, 0), 0.0, 0.55, PM, seed=i)))
    CM.front_scatter(fr, sacks, 10, (fr.front_y - 12, fr.front_y - 4), 0.6, seed=7, min_gap=2.5)
