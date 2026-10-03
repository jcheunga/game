"""Wilderness scenes: expedition (mountain-pass camp), friends (campfire gathering in a clearing)."""
import math
import random

from rk import env
from rk import dressing as P
from rk.palette import LANTERN_TEAL

from .common import golden, night, rect_area, rocks, trees, tufts


def expedition(scene):
    """Lantern Caravan camp on an alpine shelf below a snowy pass."""
    road = [(-4, -40), (-2, -10), (1, 8), (-3, 30), (4, 58), (0, 100), (8, 150)]

    def height(x, y):
        walls = 0.0028 * x * x * env.smoothstep(0, 60, y) + 0.0012 * x * x
        rise = 30 * env.smoothstep(20, 170, y)
        z = env.fbm(x, y, 0.03, 5, 13) * 3.2 + walls + rise
        camp = 1 - env.smoothstep(10, 16, math.hypot(x - 1, y - 6))
        return z * (1 - camp) + (env.fbm(1, 6, 0.03, 5, 13) * 3.2 + 30 * env.smoothstep(20, 170, 6) + 0.1) * camp
    alpine = env.ground('Alpine meadow', grass=('4c5e32', '6a7440', '93904e'), dry='a49064', rock='7c7870',
                        dry_amount=0.35, flowers=0.7, flower_colors=('f2eee0', 'e8c84a', '8a7ac0'), snow_height=24.0)
    env.terrain((300, 320), (240, 260), (0, 80), height, mat=alpine, masks={'path': env.road_mask(road, 4.0, 1.4)})
    golden(scene, sun_az=228, sun_el=17, strength=4.8, haze_density=0.003, clouds=0.8, light_strength=0.6,
           zenith='3e5f8c', horizon='f2c48e', mid='f4dcb8', upper='a8c2da')
    env.far_ground(fog='c4c6cc', inner=170)
    env.mountains(11, 520, (90, 150), arc=(-1.0, 1.0), base_z=10, seed=31, direction=90, fog='b4c2d6', fog_dist=1400,
                  snow=0.5, rock=('5e6068', '7c7c84'), green='5e6a44', jag=1.0)
    M = P.pm()
    z = height(1, 6)
    P.war_wagon((7.5, 8.5, z), math.radians(205), M, seed=1, power=40)
    for i, (x, y, r) in enumerate(((-6.0, 9.0, 0.35), (-3.0, 14.0, 0.05), (2.5, 15.5, -0.25))):
        P.ridge_tent((x, y, z), r + math.pi / 2, L=3.0, Wd=2.4, h=1.9, M=M, lit=60 if i == 1 else 0,
                     mat=env.stripes('Tent stripes', a=LANTERN_TEAL, b='d9ccb0', count=1.6, axis='x', width=0.55)
                     if i == 1 else None)
    P.pavilion((-10.5, 15.5, z), 0.4, r=2.4, a=LANTERN_TEAL, b='e0d4b6', M=M)
    P.campfire((-0.5, 5.5, z), 0.8, M, power=500, smoke=True)
    for (x, y, r) in ((-2.8, 4.0, 0.4), (1.8, 3.8, -0.4)):
        P.bench((x, y, z), r, 1.6, M)
    for i, (x, y) in enumerate(((10.5, 4.5), (11.2, 5.5), (10.0, 6.0))):
        P.crate((x, y, z), i * 0.6, 0.7, M)
    P.barrel((-7.8, 4.2, z), 0.0, M=M)
    P.sack((-8.6, 4.8, z), 0.4, 0.6, M, seed=3)
    P.banner_pole((0.5, 12.5, z), 0.0, M['teal'], h=5.4, width=1.0, drop=2.0)
    P.lantern_post((4.5, 2.5, z), math.pi, power=25)
    trees(80, rect_area(-110, 110, -20, 150), height, seed=4, kinds=('conifer',), min_gap=4.5,
          avoid=lambda x, y: env.poly_dist(x, y, road) < 6 or math.hypot(x - 1, y - 6) < 16 or height(x, y) > 26)
    rocks(70, rect_area(-80, 80, -30, 140), height, seed=5, size=(0.6, 2.8),
          avoid=lambda x, y: env.poly_dist(x, y, road) < 4 or math.hypot(x - 1, y - 6) < 13, moss=0.35)
    tufts(180, rect_area(-18, 18, -24, 4), height, seed=6, avoid=lambda x, y: env.poly_dist(x, y, road) < 3)
    env.camera((-1.0, -17, height(-1, -17) + 6.5), (1.0, 30, z + 7.0), lens=26)
    return dict(bloom=0.18, vignette=0.34, saturation=1.08)


def friends(scene):
    def height(x, y):
        return env.fbm(x, y, 0.04, 4, 17) * 1.4 * env.smoothstep(10, 24, math.hypot(x, y - 10))
    env.terrain((180, 180), (180, 180), (0, 20), height,
                masks={'path': lambda x, y: (1 - env.smoothstep(4.0, 7.5, math.hypot(x + 1.5, y - 9))) * 0.9},
                mat=env.ground('Clearing', grass=('34482a', '506436', '76803e'), dry='8a7a48', dry_amount=0.2,
                               flowers=0.6))
    night(scene, moon_az=40, moon_el=26, strength=0.7, haze_density=0.006, haze_color='9aa0b8', zenith='1a2644',
          horizon='6a5068', glow='e0b090', clouds=0.5, light_strength=1.8)
    env.far_ground(fog='232a3a', inner=100)
    M = P.pm()
    P.campfire((-1.5, 9.0, 0.0), 1.0, M, power=2400, seed=2, smoke=True)
    rng = random.Random(3)
    for k in range(5):
        a = math.radians(-60 + k * 72)
        x, y = -1.5 + math.cos(a) * 3.4, 9.0 + math.sin(a) * 3.0
        o = P.bench((x, y, height(x, y)), a + math.pi / 2, 1.8, M)
    for i, (x, y, r) in enumerate(((-9.0, 15.5, 0.4), (6.5, 16.5, -0.5))):
        P.ridge_tent((x, y, height(x, y)), r + math.pi / 2, L=3.0, Wd=2.4, h=1.9, M=M, lit=90)
    P.pavilion((-1.0, 22.0, height(-1.0, 22.0)), 0.0, r=2.6, a=LANTERN_TEAL, b='e0d4b6', M=M)
    env.point((-1.0, 22.0, 1.6), 200, 'ffb05a', 0.5, name='Pavilion glow')
    P.war_wagon((9.5, 7.5, height(9.5, 7.5)), math.radians(110), M, seed=4, power=40)
    for (x, y) in ((-5.5, 5.5), (-4.6, 5.9)):
        P.barrel((x, y, height(x, y)), rng.uniform(0, 6), M=M)
    P.chest((3.0, 4.0, height(3.0, 4.0)), 0.6, 0.9, 0.55, 0.55, M, open_lid=0.0)
    # lantern strings between trees and poles
    from mathutils import Vector as V
    for (p0, p1) in (((-12, 8, 4.5), (-3, 18, 4.8)), ((-3, 18, 4.8), (8, 13, 4.5)), ((-12, 8, 4.5), (-8, 2, 4.0))):
        p0, p1 = V(p0), V(p1)
        P.bunting(p0, p1, sag=0.9, count=1, colors=[M['rope']], size=0.01)
        for i in range(7):
            t = (i + 0.5) / 7
            pp = p0.lerp(p1, t) - V((0, 0, 0.9 * 4 * t * (1 - t)))
            P.hanging_lantern(pp, M, power=55.0, size=1.0, chain=0.1)
    for (x, y, h) in ((-12, 8, 4.5), (-3, 18, 4.8), (8, 13, 4.5), (-8, 2, 4.0)):
        from rk import geo
        o = geo.cylinder('Lantern pole', 0.07, h + 0.5, (x, y, (h + 0.5) / 2), M['wood_dark'], env.C['Props'], 6, bevel=0)
    trees(60, lambda r: (r.uniform(-60, 60), r.uniform(-10, 70)), height, seed=6, kinds=('broad', 'conifer'),
          min_gap=4.0, avoid=lambda x, y: math.hypot(x + 1, y - 10) < 16 or (y < 2 and abs(x) < 26))
    trees(14, rect_area(-40, 40, -14, 2), height, seed=7, kinds=('bush',), min_gap=3,
          avoid=lambda x, y: abs(x) < 10)
    tufts(100, rect_area(-14, 14, -10, 4), height, seed=2, avoid=lambda x, y: math.hypot(x + 1.5, y - 9) < 5)
    rocks(10, rect_area(-14, 14, -8, 20), height, seed=3, avoid=lambda x, y: math.hypot(x + 1.5, y - 9) < 5)
    env.camera((1.5, -7.5, 4.2), (-0.8, 12.0, 1.9), lens=26)
    return dict(bloom=0.28, bloom_threshold=0.55, vignette=0.42, saturation=1.1)
