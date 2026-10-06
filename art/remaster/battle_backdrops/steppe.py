"""Sunfall Steppe: a dirt road through a war camp at sunset. Tents, banners, campfires and wagons stand behind it;
beyond, the camp's pavilions and stake palisade; far off, endless golden grassland under a huge sky."""
import random

from rk import env
from rk import dressing as P
from rk.palette import LANTERN_TEAL
from battle_scenes import kit as K

from . import common as CM

MOOD = dict(zenith='34487e', horizon='ffb46a', mid='f6b882', upper='9a8cb0', clouds=0.7, cloud_lit='ffc890',
            cloud_dark='7e5c6e', cloud_scale=1.2, sun='ffc282', strength=4.3, sky_light=0.6, glow='ffd08a',
            glow_amount=1.3, glow_el=3.0)
LOOK = dict(far=dict(saturation=1.06, bloom=0.2, split=0.1), mid=dict(saturation=1.04, split=0.1),
            near=dict(saturation=1.06, split=0.12, contrast=1.02, bloom=0.1))
GRASS = dict(grass=('8a8448', '9e9452', 'b8a660'), dry='c8b06a', dirt=('8a6a46', '6a5034'))


def _canvas(i):
    return [None, env.stripes('War tent', a='7a2a22', b='d9ccb0', count=1.6, axis='x', width=0.55),
            env.stripes('Camp tent', a=LANTERN_TEAL, b='d9ccb0', count=1.6, axis='x', width=0.55)][i % 3]


# ------------------------------------------------------------------ far: golden grassland to the horizon
def far(L, scene, mood):
    def height(x, y):
        return env.fbm(x, y, 0.003, 4, 81) * 22 + 20 * env.smoothstep(900, 3600, y)
    plain = env.ground('Golden plain', **GRASS, dry_amount=0.7, flowers=0.0, scale=0.08)
    env.terrain((3600, 1700), (320, 160), (0, 980), height, mat=plain, name='Plain near')
    env.terrain((9000, 4000), (260, 110), (0, 3700), height, mat=plain, name='Plain far')
    env.far_ground(radius=12000, inner=5600, z=25, fog=mood['horizon'], fog_dist=1e7, color=('9a8a52', 'b09c5e'))
    env.mountains(10, 7000, (110, 200), arc=(-0.6, 0.6), base_z=10, seed=53, direction=90, fog=mood['horizon'],
                  fog_dist=1e7, rock=('7a6460', '967e72'), green='8a7a52', jag=0.6)
    lone = CM.tree_protos('stlone', 3, 10.0, ('3a4424', '5e6630', '8a8a40'), seed=800)
    CM.woods(L, lone, 40, (500, 2600), height, seed=4, count=(1, 3), spread=(20, 14))
    # a distant war camp: a smudge of tents and banners
    rng = random.Random(6)
    cx, cy = L.X(700, 1500), 1500.0
    for i in range(26):
        x, y = cx + rng.uniform(-160, 160), cy + rng.uniform(-60, 60)
        P.ridge_tent((x, y, height(x, y)), rng.uniform(-0.5, 0.5), L=6.0, Wd=4.6, h=3.6, mat=_canvas(i))


# ------------------------------------------------------------------ mid: pavilions and the stake palisade
def mid(L, scene, mood):
    rng = random.Random(3)
    camp = env.ground('Camp grass', **GRASS, dry_amount=0.6, flowers=0.0)
    CM.ground(L, camp, -12, 40, fade=(20.0, 26.0), height=lambda x, y: env.fbm(x, y, 0.04, 3, 15) * 0.8)
    PM = P.pm()
    K.palisade((L.x0 - 12, 16.0), (L.x1 + 12, 16.5), h=3.8, M=PM, seed=8)
    for i, x in enumerate(L.row(9, rng, 0.3)):
        if i % 3 == 1:
            P.pavilion((x, 9.0, 0), 0.0, r=3.2, h_wall=2.6, h_roof=2.6, a=('7a2a22', LANTERN_TEAL)[i % 2], M=PM)
        else:
            P.ridge_tent((x, 7.0 + rng.uniform(-1, 3), 0), rng.uniform(-0.3, 0.3), L=3.6, Wd=2.8, h=2.3, mat=_canvas(i), M=PM)
    for x in L.row(6, random.Random(5), 0.3):
        P.banner_pole((x, 11.0, 0), 0.0, PM['crimson'] if 'crimson' in PM else None, h=6.0, width=1.1, drop=2.4, M=PM)
    for x in L.row(4, random.Random(7), 0.3):
        P.campfire((x, 4.0, 0), r=0.9, M=PM, power=700, seed=int(x) % 97)


# ------------------------------------------------------------------ near: the camp road
def near(fr, scene, mood):
    rng = random.Random(4)
    road = lambda x, y: env.smoothstep(fr.band_near - 0.8, fr.band_near + 0.6, y) * \
        (1 - env.smoothstep(fr.band_far - 0.6, fr.band_far + 0.8, y))
    mat = env.ground('Camp road', **GRASS, dry_amount=0.6, flowers=0.15)
    CM.ground(fr, mat, fr.near_y - 4, fr.back_y + 12, fade=(fr.back_y + 3.8, fr.back_y + 7.0), masks={
        'path': lambda x, y: road(x, y) * (0.8 + 0.2 * env.fbm(x, y, 0.3, 2, 6)), 'cobble': lambda x, y: 0.0})
    PM = P.pm()
    row = fr.back_y + 1.8
    for i, (x, w) in enumerate(CM.spans(fr, rng, (7, 12), (3, 7))):
        kind = i % 4
        if kind == 0:
            P.ridge_tent((x, row + 1.2, 0), rng.uniform(-0.15, 0.15), L=3.0, Wd=2.4, h=1.9, mat=_canvas(i), M=PM,
                         lit=40 if i % 8 == 0 else 0)
        elif kind == 1:
            P.campfire((x, row + 0.2, 0), r=0.8, M=PM, power=600, seed=i)
            P.bench((x - 1.8, row + 0.1, 0), 0.4, M=PM)
            P.bench((x + 1.8, row + 0.3, 0), -0.4, M=PM)
        elif kind == 2:
            P.wagon((x, row + 1.0, 0), 0.1, L=3.4, Wd=1.6, M=PM, seed=i, lantern=False)
        else:
            P.weapon_rack((x, row + 0.9, 0), 0.0, M=PM, seed=i)
            P.barrel((x + 1.6, row + 0.8, 0), 0.0, M=PM)
    for x in fr.row(6, random.Random(5), 0.3):
        P.banner_pole((x, fr.back_y + 0.6, 0), 0.0, PM['teal'], h=4.6, width=0.9, drop=1.9, M=PM)
    stakes = CM.protos('ststake', 2, lambda i: CM.one(K.cheval((0, 0, 0), 0.0, L=2.6 + 0.4 * i, M=PM)))
    tuft = CM.protos('sttuft', 3, lambda i: env.grass_tuft(30 + i, 0.7, ('8a8a44', 'c2b06a')))
    CM.front_scatter(fr, tuft, 220, (fr.front_y - 14, fr.front_y - 1.8), 0.7, seed=6, min_gap=0.6, coll='Vegetation')
    CM.front_scatter(fr, stakes, 7, (fr.front_y - 9, fr.front_y - 4), 1.2, seed=7, min_gap=6.0)
    rocks = CM.protos('strock', 3, lambda i: env.rock(160 + i, 0.6, moss=0.0))
    CM.front_scatter(fr, rocks, 9, (fr.front_y - 13, fr.front_y - 3), 0.5, seed=9, min_gap=4.0)
