"""Open-road scenes: season pass (banner-lined royal road)."""
import math
import random

from mathutils import Vector

from rk import arch, env
from rk import dressing as P
from rk.palette import LANTERN_TEAL

from .common import golden, rect_area, rocks, rolling, trees, tufts


def season_pass(scene):
    road = [(0, -60), (0, -10), (2, 25), (8, 55), (14, 85), (16, 110)]
    hill = lambda x, y: 9.0 * env.smoothstep(70, 125, y) + 0.0012 * x * x * env.smoothstep(-20, 60, y)
    h = rolling(seed=4, amp=1.6, scale=0.03, rise=hill, flat=[(road, 7.0, None, 3.0)][:0])

    def height(x, y):
        z = h(x, y)
        d = env.poly_dist(x, y, road)
        t = 1 - env.smoothstep(3.5, 7.0, d)
        base = hill(x, y)
        return z * (1 - t) + (base - 0.05) * t
    env.terrain((220, 240), (220, 240), (0, 50), height, masks={'path': env.road_mask(road, 6.0, 1.6)})
    golden(scene, sun_az=228, sun_el=13.0, strength=4.6, haze_density=0.0025, clouds=0.7, light_strength=0.6)
    env.far_ground(fog='dcc4a4', inner=150)
    env.mountains(9, 480, (35, 70), arc=(-1.0, 1.0), base_z=-5, seed=5, direction=90, fog='b9b8c0', fog_dist=520)
    # castle on the far hill: gatehouse at the end of the road, keep and curtain walls behind
    hz = height(16, 112)
    M = P.pm()
    arch.gatehouse((16, 113, hz - 0.3), math.radians(-8), banner=M['teal'], seed=1)
    arch.tower((6, 128, hz + 1), 3.6, 24, 'cone', style='warm', seed=3, banner=M['teal'])
    arch.tower((28, 126, hz + 1), 3.0, 19, 'cone', style='warm', seed=4, banner=M['teal'])
    arch.tower((-8, 112, hz - 0.5), 2.6, 13, 'crenel', style='warm', seed=5)
    arch.tower((40, 108, hz - 0.5), 2.6, 13, 'crenel', style='warm', seed=6)
    arch.wall((-8, 112), (9.5, 114), 8, 2.2, base_z=hz - 1)
    arch.wall((22.5, 112), (40, 108), 8, 2.2, base_z=hz - 1)
    arch.wall((-8, 112), (6, 128), 9, 2.2, base_z=hz - 1)
    arch.wall((40, 108), (28, 126), 9, 2.2, base_z=hz - 1)
    arch.house((17, 124, hz + 0.5), 0.1, 9, 7, h1=4, h2=3.5, floors=2, seed=40, style='warm', lit=0.8)
    rng = random.Random(3)
    # village below the castle
    for i, (x, y, r) in enumerate([(-8, 78, 0.3), (-15, 70, -0.2), (26, 80, 2.8), (32, 70, 3.2), (-20, 88, 0.1),
                                   (30, 92, 2.9), (-4, 92, 0.2)]):
        arch.house((x, y, height(x, y)), r, rng.uniform(5, 7), rng.uniform(4.5, 6), floors=rng.choice((1, 2, 2)),
                   seed=10 + i, lit=0.5)
    # banner-lined road
    for i in range(9):
        y = -14 + i * 9
        cx = 0 + (2 * (y - 25) / 30 if y > 25 else 0) + (0.2 * (y - 25) if y > 25 else 0)
        # road centre x at this y (follow polyline)
        best = min(((env.seg_dist(0, y, *a, *b), a, b) for a, b in zip(road, road[1:])), key=lambda t: t[0])
        a, b = best[1], best[2]
        t = (y - a[1]) / max(1e-6, b[1] - a[1])
        cx = a[0] + (b[0] - a[0]) * t
        for side in (-1, 1):
            x = cx + side * 5.2
            P.banner_pole((x, y, height(x, y)), math.radians(0 if side < 0 else 180) * 0, M['teal'], 'lantern',
                          h=5.2, width=1.0, drop=1.9)
            if i % 2 == 0:
                P.lantern_post((cx + side * 4.0, y + 4.5, height(cx + side * 4.0, y + 4.5)), math.pi / 2 * side * -1 + (math.pi if side > 0 else 0) * 0,
                               power=35)
    # fields and woods
    on_road = lambda x, y: env.poly_dist(x, y, road) < 9
    trees(55, rect_area(-70, -10, -10, 100), height, seed=2, kinds=('broad', 'cypress', 'olive'), avoid=on_road, min_gap=4)
    trees(55, rect_area(10, 70, -10, 100), height, seed=6, kinds=('broad', 'cypress', 'olive'), avoid=on_road,
          min_gap=4)
    trees(30, rect_area(-90, 90, 100, 160), height, seed=9, kinds=('conifer', 'broad'), avoid=on_road, min_gap=5)
    trees(18, rect_area(-30, 30, -40, -14), height, seed=12, kinds=('bush',), avoid=on_road, min_gap=2.5,
          scale=(0.8, 1.6))
    rocks(25, rect_area(-30, 30, -30, 40), height, seed=4, avoid=on_road)
    tufts(260, rect_area(-22, 22, -32, 10), height, seed=1, avoid=lambda x, y: env.poly_dist(x, y, road) < 3.8)
    for side in (-1, 1):
        P.fence((side * 9, -30), (side * 9, 10), 1.1, height_fn=height)
    P.war_wagon((-1.2, 26, height(-1.2, 26)), math.radians(84), seed=1, power=40)
    env.camera((0.5, -34, 9.5), (2.5, 30, 7.0), lens=32)
    return dict(bloom=0.18, vignette=0.34, saturation=1.12)


def _dust(base, length=10.0, name='Dust'):
    """Low trailing dust cloud behind a racing wagon (drifts back along -Y)."""
    return P.smoke_column(base, height=3.0, radius=1.6, density=0.35, color='c8b090', drift=(0.0, -length), name=name)


def _race_road(seed=0):
    road = [(-60, -30), (-20, -8), (0, 6), (18, 26), (30, 60), (26, 110)]
    h = rolling(seed=seed, amp=2.2, scale=0.025)

    def height(x, y):
        z = h(x, y)
        t = 1 - env.smoothstep(4.0, 8.0, env.poly_dist(x, y, road))
        return z * (1 - t * 0.7)
    return road, height


def multiplayer(scene):
    road, height = _race_road(14)
    env.terrain((260, 260), (220, 220), (0, 40), height, masks={'path': env.road_mask(road, 8.0, 2.0)})
    golden(scene, sun_az=230, sun_el=14, strength=4.6, haze_density=0.003, clouds=0.75, light_strength=0.6)
    env.far_ground(fog='dcc4a4', inner=140)
    env.mountains(9, 460, (40, 90), arc=(-1.2, 1.2), base_z=-5, seed=15, direction=90, fog='b9b8c0', fog_dist=650)
    M = P.pm()
    rival_cloth = P.S.cloth('b0803a', name='Rival gold', var='7a5a26', weave=30)
    rival_paint = P.S.paint('7a1e30', name='Rival enamel')
    # two war wagons neck and neck, racing toward the camera with dust trailing
    fwd = Vector((-0.745, -0.667, 0))
    lat = Vector((-0.667, 0.745, 0))
    heading = math.atan2(fwd.y, fwd.x)
    for (off, ahead, cloth, paint, sd) in ((-2.4, 0.0, M['teal'], None, 1), (2.6, -3.2, rival_cloth, rival_paint, 2)):
        c = Vector((1.0, 7.0, 0)) + lat * off + fwd * ahead
        P.war_wagon((c.x, c.y, height(c.x, c.y)), heading, M, cloth=cloth, paint=paint, seed=sd, power=30,
                    wheel_spin=sd)
        dpos = c - fwd * 3.2
        P.smoke_column((dpos.x, dpos.y, height(dpos.x, dpos.y) - 0.3), height=2.6, radius=1.3, density=0.3,
                       color='c8b090', drift=(-fwd.x * 9, -fwd.y * 9), name=f'Dust {sd}')
    for i, (x, y) in enumerate(((-26, -2), (-4, 12), (8, -4), (26, 14), (12, 30), (34, 40), (-16, -18))):
        P.banner_pole((x, y, height(x, y)), 0.0, M['teal'] if i % 2 else rival_cloth, h=4.6, width=0.9, drop=1.8)
    trees(60, rect_area(-90, 90, -20, 140), height, seed=8, kinds=('broad', 'cypress', 'olive'), min_gap=5,
          avoid=lambda x, y: env.poly_dist(x, y, road) < 10 or math.hypot(x + 9, y + 10) < 16)
    rocks(26, rect_area(-40, 40, -20, 50), height, seed=9, avoid=lambda x, y: env.poly_dist(x, y, road) < 6)
    tufts(200, rect_area(-30, 30, -30, 0), height, seed=10, avoid=lambda x, y: env.poly_dist(x, y, road) < 4.5)
    P.fence((-36, -24), (-20, -20), 1.1, height_fn=height)
    P.fence((10, -16), (30, -6), 1.1, height_fn=height)
    env.camera((-10.4, 6.2, height(-10.4, 6.2) + 2.6), (1.2, 6.2, 1.9), lens=26)
    return dict(bloom=0.18, vignette=0.34, saturation=1.1)


def lan_race(scene):
    road = [(0, -40), (0, 120)]
    h = rolling(seed=21, amp=1.4, scale=0.03)

    def height(x, y):
        t = 1 - env.smoothstep(5.0, 9.0, abs(x))
        return h(x, y) * (1 - t)
    env.terrain((220, 260), (200, 220), (0, 60), height, masks={'path': env.road_mask(road, 9.0, 2.0)})
    golden(scene, sun_az=205, sun_el=20, strength=4.5, haze_density=0.003, clouds=0.6, light_strength=0.6)
    env.far_ground(fog='dcc4a4', inner=140)
    env.mountains(8, 460, (40, 80), arc=(-1.1, 1.1), base_z=-5, seed=22, direction=90, fog='b9b8c0', fog_dist=650)
    M = P.pm()
    rival_cloth = P.S.cloth('b0803a', name='Rival gold', var='7a5a26', weave=30)
    rival_paint = P.S.paint('2a3a5a', name='Rival blue enamel')
    # finish arch across the road with a long banner
    from rk import geo
    for x in (-6.5, 6.5):
        geo.cylinder('Arch post', 0.22, 7.5, (x, 30, 3.75), M['wood_dark'], env.C['Props'], 10, bevel=0.02)
        P.hanging_lantern((x * 0.92, 29.6, 6.6), M, power=40, size=1.3, chain=0.2)
    arch.slab((0, 30, 7.3), (1, 0, 0), (0, 0, 1), 14.0, 0.4, 0.4, M['wood_dark'], [], 'Arch beam')
    P.hanging_banner((0, 29.7, 7.1), 9.0, 1.6, env.stripes('Finish banner', a='1a1a1a', b='e8e0d0', count=2.0, axis='x',
                                                              width=0.5), M, tails=False)
    P.bunting((-12, 30, 6.2), (-6.5, 30, 7.0), sag=0.4, count=6)
    P.bunting((6.5, 30, 7.0), (12, 30, 6.2), sag=0.4, count=6)
    # racing wagons approaching the line
    for (x, y, cloth, paint, sd) in ((-2.7, 22.0, M['teal'], None, 1), (2.9, 25.5, rival_cloth, rival_paint, 3)):
        P.war_wagon((x, y, height(x, y)), -math.pi / 2 + (0.04 if x < 0 else -0.04), M, cloth=cloth, paint=paint,
                    seed=sd, power=30)
        P.smoke_column((x, y + 3.0, height(x, y) - 0.3), height=2.6, radius=1.1, density=0.18, color='c8b090',
                       drift=(0.0, 9.0), name=f'Dust {sd}')
    # spectator pavilions and crowds along the course
    rng = random.Random(4)
    for i, y in enumerate((-2, 10, 22, 40, 52)):
        for side in (-1, 1):
            if rng.random() < 0.25:
                continue
            a = LANTERN_TEAL if (i + (side > 0)) % 2 else 'b0803a'
            P.pavilion((side * 15.5, y, height(side * 15.5, y)), 0.0, r=2.2, a=a, b='e0d4b6', M=M)
    pts = []
    for y in range(8, 60, 2):
        for side in (-1, 1):
            for k in range(2):
                if rng.random() < 0.4:
                    continue
                x = side * (9.6 + k * 1.1 + rng.uniform(-0.3, 0.3))
                pts.append((x, y + rng.uniform(-0.6, 0.6), height(x, y), math.pi / 2 if side < 0 else -math.pi / 2))
    P.crowd(pts, rng, seated=False)
    for side in (-1, 1):
        P.fence((side * 9.0, -20), (side * 9.0, 60), 1.0, height_fn=height, posts=2.4)
    trees(40, rect_area(-90, 90, 30, 140), height, seed=5, kinds=('broad', 'cypress'), min_gap=5,
          avoid=lambda x, y: abs(x) < 20)
    trees(20, rect_area(-90, -22, -20, 40), height, seed=6, kinds=('broad', 'olive'), min_gap=5)
    trees(20, rect_area(22, 90, -20, 40), height, seed=7, kinds=('broad', 'olive'), min_gap=5)
    env.camera((4.5, 2.0, 4.2), (0, 24, 2.6), lens=28)
    return dict(bloom=0.18, vignette=0.34, saturation=1.1)
