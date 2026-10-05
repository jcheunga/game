"""Thornwall Pass: a packed-snow road on a crisp morning. Rocks, young pines, cairns and prayer flags line it;
behind, a pine forest climbs to a palisade watchtower; far off, a snow range and a stone fort on its ridge."""
import math
import random

from rk import arch, env
from rk import dressing as P
from battle_scenes import kit as K

from . import common as CM

MOOD = dict(zenith='245a9c', horizon='eef0ec', mid='dde8f0', upper='76aee4', clouds=0.55, cloud_lit='ffffff',
            cloud_dark='9aaabb', cloud_scale=1.2, sun='fff4e6', strength=4.9, sky_light=0.75, glow='fff6ea',
            glow_amount=0.55)
LOOK = dict(far=dict(saturation=1.04, bloom=0.12, split=0.05), mid=dict(saturation=1.0, split=0.05),
            near=dict(saturation=1.02, split=0.06, contrast=1.03))
FOG = dict(far_dist=2200.0, mid=(0.2, 0.004, 0.55))
SNOW = dict(grass=('c9d2da', 'dfe5ea', 'b4bec8'), dry='dfe5ea', dirt=('7a7066', '5e564e'))
PINES = ('1e3420', '2f4a2c', '587040')


def _snow_peaks():
    return env.aerial_mat('Snow range', 'ffffff', 1e7, rock=('5a5e66', '767a82'), green='6a7464', snow=0.62)


# ------------------------------------------------------------------ far: the snow range and a ridge fort
def far(L, scene, mood):
    fort_x, fort_y = L.X(300, 1600), 1600.0

    def height(x, y):
        z = env.fbm(x, y, 0.004, 5, 51) * 16 + 30 * env.smoothstep(600, 3600, y)
        return z + 60 * math.exp(-((x - fort_x) ** 2 + (y - fort_y) ** 2) / (2 * 220 ** 2))
    snowfield = env.ground('Far snow', **SNOW, dry_amount=0.0, flowers=0.0, scale=0.1, rock='7a7c80', snow=0.7)
    env.terrain((3600, 1700), (320, 160), (0, 980), height, mat=snowfield, name='Slopes near')
    env.terrain((9000, 4000), (260, 110), (0, 3700), height, mat=snowfield, name='Slopes far')
    env.far_ground(radius=12000, inner=5600, z=120, fog=mood['horizon'], fog_dist=1e7, color=('c8d0d8', 'dde2e8'))
    env.mountains(12, 6600, (230, 320), arc=(-0.62, 0.62), base_z=10, seed=31, direction=90, mat=_snow_peaks(), jag=1.0)
    pines = CM.tree_protos('thfar', 3, 16.0, PINES, kind='conifer', seed=420)
    CM.woods(L, pines, 55, (700, 2600), height, seed=6, count=(5, 14), spread=(28, 20),
             avoid=lambda x, y: abs(x - fort_x) < 120 and abs(y - fort_y) < 110)
    M = arch.mats('grey')
    z0 = height(fort_x, fort_y)
    corners = [(-40, -26), (40, -26), (40, 26), (-40, 26)]
    for (ax, ay), (bx, by) in zip(corners, corners[1:] + corners[:1]):
        arch.wall((fort_x + ax, fort_y + ay), (fort_x + bx, fort_y + by), 11.0, 3.5, style='grey', M=M, base_z=z0 - 2)
    for i, (ax, ay) in enumerate(corners):
        arch.tower((fort_x + ax, fort_y + ay, z0 - 2), r=6.0, h=20.0, roof='cone', style='grey', seed=i, lit=0.4, M=M)
    arch.tower((fort_x + 5, fort_y + 4, z0 - 2), r=9.0, h=32.0, roof='cone', roof_h=10.0, style='grey', seed=8, lit=0.5, M=M)


# ------------------------------------------------------------------ mid: pine forest and a palisade watchtower
def mid(L, scene, mood):
    rng = random.Random(5)
    snow = env.ground('Forest snow', **SNOW, dry_amount=0.0, flowers=0.0, rock='7a7c80')

    def rise(x, y):
        return 0.03 * max(0.0, y - 4) ** 1.1 + env.fbm(x, y, 0.06, 3, 9) * 0.8
    CM.ground(L, snow, -12, 46, fade=(26.0, 32.0), height=rise)
    pines = CM.tree_protos('thmid', 4, 7.5, PINES, kind='conifer', seed=440)
    groves = [x for x, _ in CM.spans(L, rng, (10, 24), (14, 30))]
    CM.scatter(L, pines, 46, (8.0, 22.0), seed=7, scale=(0.75, 1.2), min_gap=2.6, height=rise,
               avoid=lambda x, y: abs(x - L.X(640)) < 9 and y < 12 or min(abs(x - g) for g in groves) > 9)
    rocks = CM.protos('thmrock', 3, lambda i: env.rock(60 + i, 2.6, mat=env.snow_rock(), moss=0.0))
    CM.scatter(L, rocks, 14, (2.0, 14.0), seed=8, scale=(0.8, 1.5), min_gap=6.0, height=rise, coll='Props')
    PM = P.pm()
    px = L.X(640)
    K.palisade((px - 16, 7.0), (px + 16, 7.5), h=3.6, M=PM, seed=3, height_fn=rise)
    arch.tower((px, 8.0, rise(px, 8.0) - 0.3), r=2.4, h=6.5, roof='cone', roof_h=3.2, style='warm', seed=4, lit=0.6)


# ------------------------------------------------------------------ near: the pass road
def near(fr, scene, mood):
    rng = random.Random(4)
    road = lambda x, y: env.smoothstep(fr.band_near - 0.8, fr.band_near + 0.6, y) * \
        (1 - env.smoothstep(fr.band_far - 0.6, fr.band_far + 0.8, y))
    mat = env.ground('Pass snow', **SNOW, dry_amount=0.0, flowers=0.0, rock='7a7c80')

    def drift(x, y):
        return env.fbm(x, y, 0.08, 3, 12) * 0.5 * (1 - road(x, y))
    CM.ground(fr, mat, fr.near_y - 4, fr.back_y + 12, fade=(fr.back_y + 3.8, fr.back_y + 7.0), height=drift, masks={
        'path': lambda x, y: road(x, y) * (0.75 + 0.25 * env.fbm(x, y, 0.3, 2, 4)), 'cobble': lambda x, y: 0.0})
    PM = P.pm()
    row = fr.back_y + 1.8
    rocks = CM.protos('throck', 4, lambda i: env.rock(80 + i, 1.0 + 0.25 * i, mat=env.snow_rock(), moss=0.0))
    pines = CM.tree_protos('thnear', 3, 4.2, PINES, kind='conifer', seed=460)
    groups = [x for x, _ in CM.spans(fr, rng, (6, 12), (4, 9))]
    for i, x in enumerate(groups):
        kind = i % 4
        if kind in (0, 2):
            for k in range(rng.randint(2, 4)):
                env.inst(rng.choice(rocks), (x + rng.uniform(-2.5, 2.5), row + rng.uniform(-0.2, 1.6), -0.2),
                         rng.uniform(0, 6.28), rng.uniform(0.6, 1.2), env.C['Props'])
            env.inst(rng.choice(pines), (x + rng.uniform(-1.5, 1.5), row + 1.8, 0), rng.uniform(0, 6.28),
                     rng.uniform(0.8, 1.15), env.C['Vegetation'])
        elif kind == 1:
            K.cairn((x, row + 0.4, 0), h=1.3, seed=i)
            K.prayer_flags((x - 3.5, row + 0.6, 2.4), (x + 3.5, row + 0.9, 2.6), sag=0.5, count=11)
            for px in (x - 3.5, x + 3.5):
                P.fence((px, row + 0.6), (px + 0.01, row + 0.61), h=2.6, M=PM, posts=5, rails=0)
        else:
            P.fence((x - 3, row + 0.8), (x + 3, row + 1.0), h=1.1, M=PM, posts=1.5, rails=2)
            K.log_stack((x + 4.2, row + 0.9, 0), 0.1, n=7, L=2.6, M=PM)
    for x in fr.row(7, random.Random(6), 0.3):
        P.torch((x, fr.back_y + 0.3, 0), h=1.9, M=PM, power=30)
    # in front: drifts, rocks and young pines kept below the band
    CM.front_scatter(fr, rocks, 14, (fr.front_y - 13, fr.front_y - 2.6), 0.9, seed=5, min_gap=3.0)
    CM.front_scatter(fr, pines, 7, (fr.front_y - 13, fr.front_y - 7), 4.2, seed=9, min_gap=7.0, coll='Vegetation')
