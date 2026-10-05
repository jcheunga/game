"""Crownfall Citadel: a breached courtyard under a storm-lit evening. Rubble, barricades, braziers and banners line
it; behind, the outer curtain wall, its towers and gatehouse; far off, the great citadel on its crag."""
import math
import random

from rk import arch, env
from rk import dressing as P
from battle_scenes import kit as K

from . import common as CM

MOOD = dict(zenith='262c46', horizon='f2a06e', mid='e29872', upper='626486', clouds=0.95, cloud_lit='ffa676',
            cloud_dark='38303e', cloud_scale=1.1, sun='ffac72', strength=3.8, sky_light=0.55, glow='ffb27a',
            glow_amount=1.2, glow_el=3.5)
LOOK = dict(far=dict(saturation=1.04, bloom=0.18, split=0.1), mid=dict(saturation=1.0, bloom=0.1, split=0.1),
            near=dict(saturation=1.02, split=0.12, contrast=1.04, bloom=0.12))
FOG = dict(far_dist=1500.0, mid=(0.24, 0.005, 0.6))
COURT = dict(grass=('62625e', '72706a', '84807a'), dry='84807a', dirt=('5e5852', '46423c'))


def _crag():
    return env.aerial_mat('Citadel crag', 'ffffff', 1e7, rock=('4e4a4c', '6a6466'), green='4a4e40')


# ------------------------------------------------------------------ far: the great citadel on its crag
def far(L, scene, mood):
    cx, cy = L.X(600, 2300), 2300.0

    def height(x, y):
        z = env.fbm(x, y, 0.004, 4, 101) * 18 + 40 * env.smoothstep(700, 3400, y)
        d = math.hypot((x - cx) / 1.6, y - cy)
        return z + 95 * (1 - env.smoothstep(60, 360, d)) + env.fbm(x, y, 0.02, 4, 5) * 18 * (1 - env.smoothstep(80, 360, d))
    land = env.ground('Citadel lands', grass=('4a4c3a', '5a5a44', '6e6a4e'), dry='6e6a4e', dirt=('4a4238', '38322a'),
                      dry_amount=0.0, flowers=0.0, scale=0.1, rock='5a5654')
    env.terrain((3600, 1700), (360, 180), (0, 1300), height, mat=land, name='Lands near')
    env.terrain((9000, 4000), (260, 110), (0, 4000), height, mat=land, name='Lands far')
    env.far_ground(radius=12000, inner=5900, z=40, fog=mood['horizon'], fog_dist=1e7, color=('3e3e32', '4e4c3c'))
    env.mountains(10, 6800, (180, 320), arc=(-0.6, 0.6), base_z=10, seed=73, direction=90, mat=_crag(), jag=0.9)
    M = arch.mats('grey')
    z0 = height(cx, cy)
    rng = random.Random(3)
    ring = [(math.cos(a) * 110, math.sin(a) * 60) for a in [i * math.tau / 12 for i in range(12)]]
    for (ax, ay), (bx, by) in zip(ring, ring[1:] + ring[:1]):
        arch.wall((cx + ax, cy + ay), (cx + bx, cy + by), 18.0, 5.0, style='grey', M=M, base_z=z0 - 14)
    for i, (ax, ay) in enumerate(ring[::2]):
        arch.tower((cx + ax, cy + ay, z0 - 14), r=8.0, h=32.0, roof='cone', roof_h=12.0, style='grey', seed=i, lit=0.6, M=M)
    for i in range(7):
        a, r = rng.uniform(0, 6.28), rng.uniform(0, 55)
        arch.tower((cx + math.cos(a) * r, cy + math.sin(a) * r * 0.6, z0 - 6), r=rng.uniform(6, 10), h=rng.uniform(34, 58),
                   roof='cone', roof_h=rng.uniform(12, 20), style='grey', seed=20 + i, lit=0.7, M=M)
    arch.tower((cx, cy + 10, z0 - 6), r=16.0, h=64.0, roof='cone', roof_h=24.0, style='grey', seed=40, lit=0.8, M=M)
    for k in range(5):
        env.point((cx + rng.uniform(-60, 60), cy + rng.uniform(-30, 30), z0 + rng.uniform(10, 40)), power=8e4,
                  color='ffb060', radius=4.0, name='Citadel fire')


# ------------------------------------------------------------------ mid: the outer curtain wall
def mid(L, scene, mood):
    rng = random.Random(5)
    ward = env.ground('Outer ward', **COURT, dry_amount=0.0, flowers=0.0)
    CM.ground(L, ward, -12, 40, fade=(20.0, 26.0))
    M = arch.mats('grey')
    PM = P.pm()
    wy = 14.0
    gx = L.X(470)
    arch.wall((L.x0 - 10, wy), (gx - 6, wy), 6.0, 2.6, style='grey', M=M)
    arch.wall((gx + 6, wy), (L.x1 + 10, wy), 6.0, 2.6, style='grey', M=M)
    arch.gatehouse((gx, wy + 0.2, 0), 0.0, w=11.0, d=7.0, h=9.0, style='grey', M=M, lit=0.7)
    for i, x in enumerate(L.row(6, rng, 0.2)):
        if abs(x - gx) < 12:
            continue
        arch.tower((x, wy + 0.8, 0), r=3.2, h=8.5, roof='cone', roof_h=4.0, style='grey', seed=i, lit=0.6, M=M)
    for x in L.row(8, random.Random(2), 0.25):
        P.hanging_banner((x, wy - 1.35, 5.6), width=1.2, length=3.0, cloth=PM['teal'], M=PM)
    for x in L.row(5, random.Random(4), 0.3):
        P.brazier((x, 6.0, 0), PM, power=600, h=1.0)
    for x in L.row(6, random.Random(6), 0.4):
        K.rubble((x, rng.uniform(3, 9), 0), r=2.2, n=14, seed=int(x * 3) % 97)


# ------------------------------------------------------------------ near: the breached courtyard
def near(fr, scene, mood):
    rng = random.Random(8)
    mat = env.ground('Courtyard', **COURT, dry_amount=0.0, flowers=0.0, cobble=('86827a', '64605a'), cobble_scale=3.2)
    CM.ground(fr, mat, fr.near_y - 4, fr.back_y + 12, fade=(fr.back_y + 3.8, fr.back_y + 7.0), masks={
        'cobble': lambda x, y: 1.0 if y > fr.front_y - 1.0 else 0.0, 'path': lambda x, y: 0.0})
    M = arch.mats('grey')
    PM = P.pm()
    row = fr.back_y + 1.8
    for i, (x, w) in enumerate(CM.spans(fr, rng, (7, 12), (3, 6))):
        kind = i % 4
        if kind == 0:
            arch.wall((x - w / 2, row + 1.6), (x + w / 2, row + 1.6), 1.8, 0.8, style='grey', M=M, walk=False)
            K.rubble((x + w / 2 + 0.8, row + 1.2, 0), r=1.4, n=10, seed=i)
        elif kind == 1:
            P.brazier((x, row + 0.4, 0), PM, power=420, h=1.0)
            P.weapon_rack((x + 1.8, row + 1.0, 0), 0.0, M=PM, seed=i)
        elif kind == 2:
            K.cheval((x, row + 0.6, 0), 0.05, L=3.4, M=PM)
            K.rubble((x + 2.6, row + 1.0, 0), r=1.2, n=9, seed=i + 5)
        else:
            P.cart((x, row + 0.9, 0), 0.25, PM, seed=i, load='sacks')
            P.barrel((x + 2.0, row + 0.8, 0), 0.0, M=PM)
    for x in fr.row(6, random.Random(5), 0.3):
        P.banner_pole((x, fr.back_y + 0.6, 0), 0.0, PM['teal'], h=4.8, width=0.9, drop=2.0, M=PM)
    # in front: broken stone and spent arrows' barricades, kept below the band
    for x in fr.row(7, random.Random(6), 0.4):
        K.rubble((x, fr.front_y - rng.uniform(3, 9), 0), r=1.2 * fr.fit(fr.front_y - 6, 0.8), n=10, seed=int(x * 7) % 97)
    for x in fr.row(5, random.Random(7), 0.3):
        K.cheval((x, fr.front_y - 3.0, 0), rng.uniform(-0.2, 0.2), L=2.8 * fr.fit(fr.front_y - 3.0, 1.2), M=PM)
