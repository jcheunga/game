"""Mire of Saints: a causeway through a foggy swamp. Reeds, mooring posts and lanterns line it; behind, dead trees
and a drowned chapel stand in still water; far off, a misty treeline and a sunken bell tower."""
import math
import random

from rk import arch, env, geo
from rk import dressing as P
from battle_scenes import kit as K

from . import common as CM
from . import ships as S

MOOD = dict(zenith='566670', horizon='d2d2be', mid='c6cab6', upper='88989a', clouds=0.85, cloud_lit='e8e8d8',
            cloud_dark='5a6466', cloud_scale=0.9, sun='f2eed2', strength=3.4, sky_light=0.85, glow='f0ecd4',
            glow_amount=0.5)
LOOK = dict(far=dict(saturation=0.92, split=0.06), mid=dict(saturation=0.94, split=0.06),
            near=dict(saturation=0.98, split=0.08, contrast=1.02, bloom=0.08))
FOG = dict(far_dist=900.0, mid=(0.17, 0.005, 0.55), near=(0.03, 0.004, 0.25))
BOG = dict(grass=('4a5432', '5a6236', '6e6c3e'), dry='6e6c3e', dirt=('5a4a34', '3e3426'))


def _swamp():
    return S.sea('Swamp water', deep='1e2a22', wave=0.15, scale=0.4, rough=0.08)


# ------------------------------------------------------------------ far: misty treeline and a sunken bell tower
def far(L, scene, mood):
    S.water(-7000, 7000, 120, 15000, z=0.0, mat=_swamp(), name='Far swamp')

    def isle(x, y):
        return -1.0 + 3.0 * (env.fbm(x, y, 0.006, 4, 71) > 0.15)
    bank = env.ground('Far banks', **BOG, dry_amount=0.0, flowers=0.0, scale=0.1)
    env.terrain((5000, 3000), (240, 140), (0, 2200), lambda x, y: env.fbm(x, y, 0.004, 4, 71) * 10 - 3 +
                10 * env.smoothstep(1500, 3800, y), mat=bank, name='Banks')
    dead = CM.tree_protos('mrfdead', 3, 12.0, kind='dead', seed=700)
    pines = CM.tree_protos('mrfpine', 3, 18.0, ('1e2c22', '2e3e2c', '4a5a3a'), kind='conifer', seed=720)
    CM.woods(L, pines, 90, (1400, 3600), lambda x, y: env.fbm(x, y, 0.004, 4, 71) * 10 - 3 + 10 * env.smoothstep(1500, 3800, y),
             seed=3, count=(8, 20), spread=(40, 30))
    CM.woods(L, dead, 50, (400, 1400), lambda x, y: -1.0, seed=5, count=(1, 4), spread=(30, 20))
    M = arch.mats('grim')
    tx, ty = L.X(640, 1100), 1100.0
    arch.tower((tx, ty, -6.0), r=5.0, h=28.0, roof='cone', roof_h=8.0, style='grim', seed=4, lit=0.4, M=M, ruined=0.2)


# ------------------------------------------------------------------ mid: still water, dead trees, the drowned chapel
def mid(L, scene, mood):
    rng = random.Random(9)
    S.water(L.x0 - 20, L.x1 + 20, -14, 50, z=-0.4, mat=CM.soft_edge(_swamp(), 30.0, 38.0), name='Mire water')
    bank = env.ground('Mire banks', **BOG, dry_amount=0.0, flowers=0.0)
    CM.ground(L, bank, -12, 30, fade=(18.0, 24.0), height=lambda x, y: env.fbm(x, y, 0.05, 3, 21) * 1.4 - 0.9)
    M = arch.mats('grim')
    PM = P.pm()
    cx = L.X(380)
    # the drowned chapel: a broken spire and the stumps of its nave walls standing in the water
    arch.spire((cx, 13.0, -2.0), r=2.4, h=13.0, M=M, style='grim', seed=31, ruin=0.4, lit=0.6)
    for dx, h in ((-7, 3.2), (-3, 4.4), (4, 2.6), (8, 3.6)):
        arch.wall((cx + dx - 1.6, 11.0), (cx + dx + 1.6, 11.0), h, 0.7, crenels=False, style='grim', M=M, base_z=-1.4, walk=False)
    K.bell_frame((L.X(700), 6.0, -0.3), 0.1, M=PM, h=4.2)
    dead = CM.tree_protos('mrmdead', 4, 8.0, kind='dead', seed=740)
    CM.scatter(L, dead, 26, (2.0, 16.0), seed=4, scale=(0.8, 1.3), min_gap=5.0)
    for x in L.row(30, rng, 0.45):
        K.reeds((x, rng.uniform(0.5, 9.0), -0.4), n=24, h=1.8, seed=int(x * 7) % 997)


# ------------------------------------------------------------------ near: the causeway
def near(fr, scene, mood):
    rng = random.Random(6)
    bank_far, bank_near = fr.back_y + 2.0, fr.front_y - 2.0
    road = lambda x, y: env.smoothstep(fr.band_near - 0.8, fr.band_near + 0.6, y) * \
        (1 - env.smoothstep(fr.band_far - 0.6, fr.band_far + 0.8, y))
    mat = env.ground('Causeway', **BOG, dry_amount=0.0, flowers=0.0, wet=0.4)

    def bank(x, y):
        rough = env.fbm(x, y, 0.15, 3, 31) * 0.25
        return rough - 1.0 * (env.smoothstep(bank_far - 0.6, bank_far + 1.2, y) + env.smoothstep(bank_near + 0.6, bank_near - 1.2, y))
    CM.ground(fr, mat, bank_near - 3.5, bank_far + 3.5, height=bank, masks={
        'path': lambda x, y: road(x, y) * (0.8 + 0.2 * env.fbm(x, y, 0.4, 2, 5)), 'cobble': lambda x, y: 0.0})
    water = _swamp()
    S.water(fr.x0 - 14, fr.x1 + 14, bank_far - 0.5, fr.back_y + 12, z=-0.55, mat=CM.soft_edge(water, bank_far + 1.6, bank_far + 4.5))
    S.water(fr.x0 - 14, fr.x1 + 14, fr.near_y - 8, bank_near + 0.5, z=-0.55, mat=water)
    PM = P.pm()
    for x in fr.row(34, rng, 0.45):
        K.reeds((x, bank_far + rng.uniform(0.0, 1.6), -0.5), n=26, h=1.7, seed=int(x * 11) % 997)
    for x in fr.row(9, random.Random(2), 0.3):
        P.lantern_post((x, fr.back_y + 0.6, 0), 0.0, h=2.8, M=PM, power=20, color='e8f0b0')
    posts = CM.protos('mrpost', 2, lambda i: env.finish(geo.cylinder('Mooring post', 0.13, 2.2 + 0.4 * i, (0, 0, 0.6),
                                                                     PM['wood_dark'], None, 8, bevel=0.02)))
    for x in fr.row(14, random.Random(3), 0.4):
        env.inst(posts[int(x) % 2], (x, bank_far + 0.4, -0.8), 0.0, 1.0, env.C['Props'], tilt=(rng.uniform(-0.12, 0.12), rng.uniform(-0.1, 0.1)))
    for x in fr.row(3, random.Random(4), 0.3):
        S.rowboat((x, bank_far + 2.6, -0.55), L=4.4, rot=rng.uniform(-0.3, 0.3), seed=int(x), hull='3e3226')
    K.shrine_altar((fr.X(520), bank_far - 0.2, 0), 0.0, M=PM, seed=2)
    # in front: reeds and drowned stumps kept below the band
    for x in fr.row(26, random.Random(7), 0.45):
        K.reeds((x, bank_near - rng.uniform(0.3, 2.4), -0.5), n=18, h=1.5 * fr.fit(bank_near - 1.5, 1.5), seed=int(x * 5) % 997)
    stumps = CM.protos('mrstump', 2, lambda i: CM.one(K.stump((0, 0, 0), r=0.35 + 0.1 * i, M=PM)))
    CM.front_scatter(fr, stumps, 8, (fr.front_y - 12, fr.front_y - 4), 0.6, seed=8, min_gap=4.0)
