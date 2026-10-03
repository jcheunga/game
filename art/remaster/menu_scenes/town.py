"""Town scenes: market (shop), tavern wanted-board (bounty), cloister courtyard (login calendar),
festival square (event)."""
import math
import random

from mathutils import Vector

from rk import arch, env
from rk import dressing as P
from rk.palette import LANTERN_TEAL, ROT_CRIMSON

from .common import dusk, golden, night, rect_area, rocks, rolling, trees, tufts


def _square(height, plaza_r=14, ring=((0, 26), (-18, 22), (18, 22), (-26, 8), (26, 8), (-30, -6), (30, -6)),
            seed=0, style='warm', lit=0.6, floors=(1, 2, 2), sizes=((5.5, 7.5), (4.5, 6))):
    """Houses facing a central plaza. ring: positions; each faces the plaza centre."""
    rng = random.Random(seed)
    out = []
    for i, (x, y) in enumerate(ring):
        rot = math.atan2(-y, -x) + math.pi / 2
        w = rng.uniform(*sizes[0])
        d = rng.uniform(*sizes[1])
        out += arch.house((x, y, height(x, y)), rot, w, d, floors=rng.choice(floors), seed=seed * 10 + i,
                          style=style, lit=lit, h1=rng.uniform(2.7, 3.2), h2=rng.uniform(2.3, 2.7),
                          pitch=rng.uniform(42, 55))
    return out


def street(height, xs=9.5, y0=-6, y1=60, seed=0, style='warm', lit=0.6, gap=0.6, skip=()):
    """Continuous house frontage on both sides of a street running along +Y."""
    rng = random.Random(seed)
    out = []
    for side in (-1, 1):
        y = y0
        i = 0
        while y < y1:
            w = rng.uniform(5.5, 7.5)
            d = rng.uniform(5.5, 7.0)
            if (side, i) not in skip:
                x = side * (xs + d / 2)
                out += arch.house((x, y + w / 2, height(x, y)), -side * math.pi / 2, w, d,
                                  floors=rng.choice((2, 2, 1)), seed=seed * 50 + i * 2 + (side > 0), style=style, lit=lit,
                                  h1=rng.uniform(2.8, 3.3), h2=rng.uniform(2.3, 2.8), pitch=rng.uniform(45, 56),
                                  jetty=rng.uniform(0.25, 0.45))
            y += w + gap
            i += 1
    return out


def shop(scene):
    height = lambda x, y: 0.0
    street_mask = lambda x, y: 1 - env.smoothstep(8.5, 9.5, abs(x))
    env.terrain((160, 200), (160, 200), (0, 40), height, masks={'cobble': street_mask})
    golden(scene, sun_az=212, sun_el=18, strength=4.4, haze_density=0.004, clouds=0.6, light_strength=0.6)
    env.far_ground(fog='dcc4a4', inner=100)
    M = P.pm()
    street(height, xs=9.2, y0=-14, y1=58, seed=3, lit=0.6)
    # town gate closes the vista
    arch.gatehouse((0, 66, 0), 0.0, w=12, d=6, h=10, gate_w=4.0, gate_h=5.0, banner=M['teal'], seed=7)
    # stalls along both kerbs
    rng = random.Random(5)
    for i, y in enumerate((-6, 1.5, 9, 16.5, 24)):
        for side in (-1, 1):
            if (i, side) in ((4, 1),):
                continue
            x = side * 6.6
            P.stall((x, y, 0), -side * math.pi / 2, w=3.2, d=1.7, seed=i * 2 + (side > 0), M=M)
            for k in range(2):
                fx = x - side * 2.3 + rng.uniform(-0.3, 0.3)
                fy = y + rng.uniform(-1.7, 1.7)
                c = rng.random()
                if c < 0.35:
                    P.barrel((fx, fy, 0), rng.uniform(0, 6), M=M)
                elif c < 0.65:
                    P.crate((fx, fy, 0), rng.uniform(0, 6), rng.uniform(0.5, 0.75), M)
                else:
                    P.sack((fx, fy, 0), rng.uniform(0, 6), 0.55, M, seed=k + i * 3)
    P.war_wagon((2.8, 31, 0), math.radians(100), seed=2, power=40)
    P.cart((5.5, 36, 0), math.radians(260), M, load='barrels')
    P.well((-3.5, 34, 0), 0.4, M)
    for y in (-10, 6, 21):
        for side in (-1, 1):
            P.lantern_post((side * 8.4, y, 0), 0.0 if side < 0 else math.pi, power=30)
    for y, z in ((-2, 6.4), (12, 6.6), (27, 6.8), (42, 7.0)):
        P.bunting((-9.5, y, z), (9.5, y + 1.0, z), sag=1.1, count=18)
    for y in (4, 34):
        P.hanging_banner((-9.4, y, 6.2), 1.0, 2.6, M['teal'], M, rot=math.pi / 2,
                         emblem=None)
        P.hanging_banner((9.4, y, 6.2), 1.0, 2.6, M['teal'], M, rot=-math.pi / 2, emblem=None)
    trees(26, rect_area(-60, 60, 70, 120), height, seed=3, kinds=('broad', 'cypress', 'olive'), min_gap=5)
    env.camera((0, -24, 7.5), (0, 20, 3.2), lens=28)
    return dict(bloom=0.16, vignette=0.34, saturation=1.1)


def bounty(scene):
    road = [(-60, -14), (-10, -6), (20, 4), (70, 20)]
    h = rolling(seed=11, amp=1.0, scale=0.04)
    height = lambda x, y: h(x, y) * env.smoothstep(14, 30, math.hypot(x, y - 6))
    env.terrain((200, 200), (200, 200), (0, 30), height,
                masks={'path': env.road_mask(road, 7.0, 2.0), 'cobble': lambda x, y: 1 - env.smoothstep(
                    3.0, 4.0, math.hypot((x - 1) * 0.6, y - 10.5))})
    dusk(scene, sun_az=200, sun_el=3.5, strength=2.6, haze_density=0.004, clouds=0.7, light_strength=0.5,
         haze_center=(0, 30, -2), haze_size=(500, 500, 30))
    env.far_ground(fog='8a6a78', inner=100)
    env.mountains(8, 420, (35, 70), arc=(-1.2, 1.2), base_z=-5, seed=12, direction=90, fog='6e5a74', fog_dist=600)
    M = P.pm()
    AM = arch.mats('warm')
    sign = env.planks('Tavern sign', '3a2a1e', board=0.12, length=1.0)
    arch.house((0, 14, 0), 0.0, 11, 7.5, h1=3.4, h2=3.0, floors=2, seed=21, lit=1.0, sign=sign, M=AM)
    arch.house((-12, 16, 0), 0.25, 6.5, 6, floors=2, seed=22, lit=0.7, M=AM)
    arch.house((12.5, 17, 0), -0.3, 7, 6, floors=1, seed=23, lit=0.6, M=AM)
    arch.house((-22, 22, 0), 0.4, 6, 5.5, floors=2, seed=24, lit=0.5, M=AM)
    # warm spill from the open door and windows
    env.point((0.5, 9.8, 1.4), 220, 'ffa04a', 0.6, name='Door spill')
    for x in (-3.6, 3.6):
        P.torch((x, 10.0, 1.2), M=M, wall=(0, -1, 0), power=80)
    # wanted board, lit by a lantern
    P.noticeboard((-7.5, 4.5, height(-7.5, 4.5)), 0.25, M, seed=3, w=2.8, h=1.8, notices=10, wanted=True)
    P.lantern_post((-4.8, 3.6, 0), math.pi, power=70)
    P.lantern_post((6.0, 5.5, 0), 0.0, power=45)
    for i, (x, y) in enumerate(((4.6, 9.4), (5.4, 9.8), (5.0, 8.6))):
        P.barrel((x, y, 0), i * 1.3, M=M)
    P.barrel((5.0, 9.2, 0.95), 0.3, M=M)
    P.crate((-4.0, 9.6, 0), 0.3, 0.7, M)
    P.crate((-4.2, 9.5, 0.7), 0.9, 0.5, M)
    P.cart((10, 6, 0), math.radians(160), M, load='sacks')
    P.fence((-14, 6), (-10.5, 7.5), 1.0)
    P.bench((2.6, 9.4, 0), 0.0, 1.6, M)
    trees(20, rect_area(-60, 60, 28, 80), height, seed=5, kinds=('broad', 'conifer'), min_gap=5)
    trees(10, rect_area(-60, -24, -10, 28), height, seed=7, kinds=('broad', 'bush'), min_gap=4)
    trees(10, rect_area(22, 60, -10, 28), height, seed=8, kinds=('broad', 'bush'), min_gap=4)
    rocks(12, rect_area(-25, 25, -15, 2), height, seed=3, avoid=lambda x, y: env.poly_dist(x, y, road) < 4.5)
    tufts(160, rect_area(-25, 25, -18, 6), height, seed=2, avoid=lambda x, y: env.poly_dist(x, y, road) < 3.4)
    env.camera((-3.5, -16, 6.0), (0.5, 12, 3.4), lens=30)
    return dict(bloom=0.22, vignette=0.36, saturation=1.08)


def login_calendar(scene):
    height = lambda x, y: 0.0
    lawn = lambda x, y: 0.0
    paths = lambda x, y: max(1 - env.smoothstep(1.2, 1.8, abs(x)), 1 - env.smoothstep(1.2, 1.8, abs(y - 12)),
                             1 - env.smoothstep(2.2, 3.0, math.hypot(x, y - 12)))
    env.terrain((160, 160), (160, 160), (0, 20), height, masks={'cobble': paths})
    golden(scene, sun_az=235, sun_el=24, strength=4.4, haze_density=0.003, clouds=0.5, light_strength=0.65)
    env.far_ground(fog='dcc4a4', inner=90)
    M = P.pm()
    AM = arch.mats('warm')
    # three cloister walks around the lawn (open toward the camera)
    arch.arcade((12, 26), (-12, 26), bays=6, h=4.6, depth=3.4, M=AM, seed=1)
    arch.arcade((-12, 26), (-12, -2), bays=7, h=4.6, depth=3.4, M=AM, seed=2)
    arch.arcade((12, -2), (12, 26), bays=7, h=4.6, depth=3.4, M=AM, seed=3)
    arch.tower((-23, 42, 0), 2.6, 9.5, 'cone', M=AM, seed=5, banner=M['teal'], roof_h=5.0)
    arch.house((9, 36, 0), 0.0, 14, 7, h1=4, h2=3, floors=2, seed=8, M=AM, lit=0.7)
    P.sundial((0, 12, 0), M, scale=1.6)
    P.noticeboard((-5.5, 4.0, 0), 0.35, M, seed=9, w=2.4, h=1.5, notices=6)
    P.noticeboard((5.5, 4.0, 0), -0.35, M, seed=10, w=2.4, h=1.5, notices=5)
    for x, y in ((-6, 18), (6, 18), (-6, 6.5), (6, 6.5)):
        env.inst(env.proto('topiary', lambda: env.bush(41, 0.9, palette=('24361a', '3f5a22', '6e7a30'))), (x, y, 0), 0.0,
                 1.0, env.C['Vegetation'])
    for x in (-10.5, 10.5):
        for y in (4, 10, 16, 22):
            P.pot((x * 0.95, y, 0), 0, 0.5, M, kind='jar')
    for x in (-9.0, 9.0):
        env.inst(env.proto('cyp_cl', lambda: env.cypress(77, 8.5)), (x, 22.5, 0), 0.0, 1.0, env.C['Vegetation'])
    trees(30, rect_area(-60, 60, 40, 90), height, seed=3, kinds=('broad', 'cypress', 'olive'), min_gap=5)
    P.lantern_post((-2.5, 1.0, 0), math.pi, power=25)
    P.lantern_post((2.5, 1.0, 0), 0.0, power=25)
    tufts(120, rect_area(-10, 10, 2, 24), height, seed=6, avoid=lambda x, y: paths(x, y) > 0.3)
    env.camera((0, -13, 9.0), (0, 16, 1.8), lens=28)
    return dict(bloom=0.15, vignette=0.32, saturation=1.1)


def event(scene):
    height = lambda x, y: 0.0
    env.terrain((180, 180), (180, 180), (0, 30), height,
                masks={'cobble': lambda x, y: 1 - env.smoothstep(15, 17, math.hypot(x, (y - 12) * 1.15))})
    night(scene, moon_az=60, moon_el=30, strength=0.6, haze_density=0.006, haze_color='a08a7a', zenith='1a2748',
          horizon='8a5a5a', glow='f0a070', clouds=0.5, light_strength=1.6)
    env.far_ground(fog='3a3044', inner=100)
    M = P.pm()
    AM = arch.mats('warm')
    _square(height, seed=6, lit=0.95, ring=((-7, 32), (8, 33), (-22, 27), (22, 28), (-29, 14), (30, 15), (-31, 0),
                                             (32, 1)))
    # pavilions in Lantern Caravan colours
    for (x, y, a, b, r) in ((-11, 17, LANTERN_TEAL, 'e0d4b6', 2.2), (11, 18, 'b0803a', 'e8dcc0', 2.0),
                            (-14, 6, ROT_CRIMSON, 'e0d4b6', 1.8), (14, 7, LANTERN_TEAL, 'e8dcc0', 1.9)):
        P.pavilion((x, y, 0), math.atan2(12 - y, -x) - math.pi / 2, r=r, a=a, b=b, M=M)
        env.point((x, y, 1.6), 260, 'ffb05a', 0.5, name='Pavilion glow')
        env.point((x, y - r - 1.5, 2.6), 90, 'ffb05a', 0.3, name='Pavilion front lamp')
    # maypole with ribbons at the heart of the square
    P.banner_pole((0, 14, 0), 0.0, M['teal'], h=7.5, width=1.0, drop=2.2)
    for k in range(10):
        a = k * math.tau / 10
        P.bunting((0, 14, 7.2), (math.cos(a) * 14, 14 + math.sin(a) * 12, 4.2), sag=0.8, count=8,
                  colors=[M['teal'], M['canvas'], M['crimson']])
    # strings of lanterns
    rng = random.Random(4)
    for (p0, p1) in (((-16, 4, 5.0), (16, 4, 5.0)), ((-18, 22, 5.5), (18, 24, 5.5)), ((-16, 4, 5.0), (-18, 22, 5.5)),
                     ((16, 4, 5.0), (18, 24, 5.5))):
        p0, p1 = Vector(p0), Vector(p1)
        P.bunting(p0, p1, sag=1.0, count=1, colors=[M['rope']], size=0.01)
        for i in range(9):
            t = (i + 0.5) / 9
            pp = p0.lerp(p1, t) - Vector((0, 0, 1.0 * 4 * t * (1 - t)))
            P.hanging_lantern(pp, M, power=45.0, size=1.0, chain=0.15)
    for x, y in ((-16, 4), (16, 4), (-18, 22), (18, 24)):
        P.banner_pole((x, y, 0), 0.0, M['teal'], h=5.6, width=0.8, drop=1.6)
    P.campfire((0, 27.0, 0), 1.2, M, power=1600, smoke=True)
    for (x, y, a, b, r) in ((-19, -2, 'b0803a', 'e8dcc0', 2.4), (19, -1, ROT_CRIMSON, 'e0d4b6', 2.4)):
        P.pavilion((x, y, 0), math.atan2(12 - y, -x) - math.pi / 2, r=r, a=a, b=b, M=M)
        env.point((x, y, 1.6), 260, 'ffb05a', 0.5, name='Pavilion glow')
    for (p0, p1) in (((-20, -6, 5.2), (20, -6, 5.2)),):
        p0, p1 = Vector(p0), Vector(p1)
        P.bunting(p0, p1, sag=1.2, count=1, colors=[M['rope']], size=0.01)
        for i in range(12):
            t = (i + 0.5) / 12
            pp = p0.lerp(p1, t) - Vector((0, 0, 1.2 * 4 * t * (1 - t)))
            P.hanging_lantern(pp, M, power=45.0, size=1.1, chain=0.15)
    for i, (x, y) in enumerate(((-8, 9), (8, 10), (-6, 22), (6, 23))):
        P.stall((x, y, 0), math.atan2(12 - y, -x) - math.pi / 2, w=2.6, d=1.4, seed=i, M=M)
    trees(24, rect_area(-60, 60, 38, 90), height, seed=3, kinds=('broad', 'cypress'), min_gap=5)
    env.camera((0, -16, 8.5), (0, 15, 3.0), lens=30)
    return dict(bloom=0.3, bloom_threshold=0.55, vignette=0.38, saturation=1.12)
