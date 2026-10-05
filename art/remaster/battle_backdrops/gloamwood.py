"""Gloamwood Verge: a forest road at dusk. Roots, stumps, ferns and glowing mushrooms line it; behind, the trunks of
great trees fade into mist; far off, forested ridges under a fading sky and a witch's tower with a lit window."""
import math
import random

from rk import arch, env
from rk import dressing as P
from battle_scenes import kit as K

from . import common as CM

MOOD = dict(zenith='1c2840', horizon='c4b892', mid='9eaa9c', upper='3c5068', clouds=0.45, cloud_lit='e8d4b0',
            cloud_dark='2e3848', cloud_scale=1.0, sun='f6e0b6', strength=4.8, sky_light=0.95, glow='f4d8a0',
            glow_amount=0.9, glow_el=3.0)
LOOK = dict(far=dict(saturation=0.96, bloom=0.14, split=0.08), mid=dict(saturation=0.95, bloom=0.12, split=0.08),
            near=dict(saturation=1.04, split=0.1, contrast=1.02, bloom=0.16, lift=0.03))
FOG = dict(far_dist=1100.0, mid=(0.14, 0.006, 0.5))
LEAVES = ('22341e', '35502a', '5a6e36')
PINES = ('16261a', '243a24', '3e5232')
FOREST = dict(grass=('3e4c30', '52603a', '6e7444'), dry='6e6a40', dirt=('5a4836', '3e3226'))


# ------------------------------------------------------------------ far: forested ridges and the witch's tower
def far(L, scene, mood):
    def height(x, y):
        return env.fbm(x, y, 0.004, 5, 91) * 18 + 22 * env.smoothstep(600, 3400, y)
    floor = env.ground('Far forest floor', **FOREST, dry_amount=0.0, flowers=0.0, scale=0.1)
    env.terrain((3600, 1700), (320, 160), (0, 980), height, mat=floor, name='Ridges near')
    env.terrain((9000, 4000), (260, 110), (0, 3700), height, mat=floor, name='Ridges far')
    env.far_ground(radius=12000, inner=5600, z=60, fog=mood['horizon'], fog_dist=1e7, color=('2a3424', '3a4430'))
    env.mountains(10, 6400, (150, 230), arc=(-0.6, 0.6), base_z=10, seed=63, direction=90, fog=mood['horizon'],
                  fog_dist=1e7, rock=('3e4648', '56605e'), green='34442e', jag=0.4)
    pines = CM.tree_protos('glfar', 3, 22.0, PINES, kind='conifer', seed=900)
    broad = CM.tree_protos('glfarb', 2, 18.0, LEAVES, seed=920)
    CM.woods(L, pines + broad, 120, (700, 3000), height, seed=5, count=(8, 20), spread=(36, 26))
    M = arch.mats('grim')
    tx, ty = L.X(260, 1500), 1500.0
    arch.tower((tx, ty, height(tx, ty) - 2), r=6.0, h=42.0, roof='cone', roof_h=14.0, style='grim', seed=7, lit=0.0,
               M=M, ruined=0.1)
    env.point((tx, ty - 6.5, height(tx, ty) + 30), power=2e5, color='b48cff', radius=2.0, name='Witch window')


# ------------------------------------------------------------------ mid: great trunks in the mist
def mid(L, scene, mood):
    rng = random.Random(8)
    floor = env.ground('Forest floor', **FOREST, dry_amount=0.0, flowers=0.0)
    CM.ground(L, floor, -12, 40, fade=(22.0, 28.0), height=lambda x, y: env.fbm(x, y, 0.05, 3, 19) * 0.8)
    great = CM.protos('glgreat', 3, lambda i: env.great_tree(30 + i * 7, 13.0 + 2 * i, LEAVES, 1.0))
    CM.scatter(L, great, 7, (8.0, 16.0), seed=3, scale=(0.75, 0.95), min_gap=16.0)
    pines = CM.tree_protos('glmid', 3, 9.0, PINES, kind='conifer', seed=940)
    groves = [x for x, _ in CM.spans(L, rng, (8, 18), (10, 22))]
    CM.scatter(L, pines, 34, (10.0, 22.0), seed=4, scale=(0.8, 1.2), min_gap=3.2,
               avoid=lambda x, y: min(abs(x - g) for g in groves) > 7)
    ferns = CM.protos('glfern', 3, lambda i: env.bush(300 + i, 1.0 + 0.2 * i, ('2a3c22', '3e5a2c', '62783a')))
    CM.scatter(L, ferns, 60, (1.0, 14.0), seed=5, scale=(0.8, 1.3), min_gap=2.0)
    for x in L.row(10, rng, 0.4):
        K.mushrooms((x, rng.uniform(2, 10), 0), n=10, seed=int(x * 5) % 97, color='9ff5d8', glow=7.0, r=0.6)


# ------------------------------------------------------------------ near: the forest road
def near(fr, scene, mood):
    rng = random.Random(7)
    road = lambda x, y: env.smoothstep(fr.band_near - 0.8, fr.band_near + 0.6, y) * \
        (1 - env.smoothstep(fr.band_far - 0.6, fr.band_far + 0.8, y))
    mat = env.ground('Forest road', **FOREST, dry_amount=0.0, flowers=0.05)
    CM.ground(fr, mat, fr.near_y - 4, fr.back_y + 12, fade=(fr.back_y + 3.8, fr.back_y + 7.0), masks={
        'path': lambda x, y: road(x, y) * (0.75 + 0.25 * env.fbm(x, y, 0.3, 2, 8)), 'cobble': lambda x, y: 0.0})
    PM = P.pm()
    row = fr.back_y + 1.8
    ferns = CM.protos('glnfern', 3, lambda i: env.bush(320 + i, 0.7 + 0.15 * i, ('3e5a2c', '5e7e3c', '92a64c')))
    for i, (x, w) in enumerate(CM.spans(fr, rng, (6, 11), (2, 6))):
        kind = i % 4
        if kind == 0:
            K.stump((x, row + 0.8, 0), r=0.55, M=PM)
            K.mushrooms((x + 0.9, row + 0.5, 0), n=8, seed=i, color='9ff5d8', glow=6.0, r=0.5)
        elif kind == 1:
            K.log_stack((x, row + 1.0, 0), 0.05, n=9, L=3.4, M=PM)
        elif kind == 2:
            for k in range(6):
                a = k * math.tau / 6
                K.cairn((x + math.cos(a) * 1.8, row + 1.2 + math.sin(a) * 0.8, 0), h=0.9, seed=k)
            K.ward_sigil((x, row + 1.2, 0.02), r=1.4, color='b48cff', power=50)
        else:
            K.mushrooms((x, row + 0.6, 0), n=12, seed=i + 3, color='b48cff', glow=5.0, r=0.7)
        for k in range(rng.randint(2, 4)):
            env.inst(rng.choice(ferns), (x + rng.uniform(-w / 2, w / 2), row + rng.uniform(0.6, 2.2), 0),
                     rng.uniform(0, 6.28), rng.uniform(0.8, 1.2), env.C['Vegetation'])
    for x in fr.row(16, random.Random(3), 0.45):
        K.mushrooms((x, row + rng.uniform(0.2, 2.4), 0), n=9, seed=int(x * 7) % 97, color=('9ff5d8', 'b48cff')[int(x) % 2],
                    glow=8.0, r=0.6)
    for x in fr.row(7, random.Random(4), 0.3):
        P.lantern_post((x, fr.back_y + 0.3, 0), 0.0, h=2.6, M=PM, power=16, color='c8f0d8')
    # in front: ferns, mushrooms and stumps kept below the band
    CM.front_scatter(fr, ferns, 22, (fr.front_y - 13, fr.front_y - 2.6), 0.9, seed=5, min_gap=2.4, coll='Vegetation')
    for x in fr.row(8, random.Random(6), 0.4):
        K.mushrooms((x, fr.front_y - rng.uniform(3, 9), 0), n=7, seed=int(x) % 97, color='9ff5d8', glow=5.0,
                    r=0.5 * fr.fit(fr.front_y - 5, 0.4))
