"""Rotbound scenes: endless (cursed road at night), raid (undead citadel at dusk),
battle summary (battlefield aftermath at sunset)."""
import math
import random

from mathutils import Vector

from rk import arch, env
from rk import dressing as P
from rk.palette import PLAGUE

from .common import dusk, golden, night, rect_area, rocks, trees, tufts


def _blight_ground(name='Blighted earth'):
    return env.ground(name, grass=('3a3a28', '4a4630', '5e5638'), dry='6a5e44', dirt=('4a3e30', '3a3026'),
                      rock='5a5650', flowers=0.0, dry_amount=0.5, ash=0.5, ash_color='2e2a28')


def endless(scene):
    road = [(0, -40), (0, 400)]

    def height(x, y):
        z = env.fbm(x, y, 0.03, 4, 23) * 2.0 * env.smoothstep(6, 18, abs(x))
        return z + 0.002 * x * x
    env.terrain((240, 520), (200, 400), (0, 210), height, mat=_blight_ground(),
                masks={'path': env.road_mask(road, 6.0, 2.0)})
    night(scene, moon_az=96, moon_el=14, strength=0.9, haze_density=0.012, haze_color='6a7a8a', zenith='0e1424',
          horizon='34405a', glow='b0c8e8', clouds=0.6, light_strength=1.0)
    env.far_ground(fog='2a3446', inner=200)
    env.mountains(10, 520, (40, 80), arc=(-1.2, 1.2), base_z=-5, seed=41, direction=90, fog='3a4458', fog_dist=500)
    M = P.pm()
    rng = random.Random(4)
    # crooked lanterns along the road, fading into the distance; plague glow at the horizon
    for i in range(14):
        y = -6 + i * 14
        for side in (-1, 1):
            x = side * (4.6 + rng.uniform(-0.3, 0.3))
            lit = i < 8 or rng.random() < 0.3
            col = 'ffb04a' if i < 6 else ('9cf25c' if rng.random() < 0.6 else 'ffb04a')
            P.lantern_post((x, y, height(x, y)), (0.0 if side < 0 else math.pi) + rng.uniform(-0.2, 0.2), power=35 if lit else 0,
                           color=col, h=2.4)
    P.blight_pool((0, 318, 0.2), 14.0, PLAGUE, power=12000)
    env.point((0, 322, 10), 30000, PLAGUE, 20, name='Blight horizon')
    env.haze((0, 315, -6), (200, 90, 34), 0.006, '9cf25c', 0.3, falloff=None, name='Blight fog', soft=True,
             emission=0.012)
    arch.gatehouse((0, 300, height(0, 300)), 0.0, w=14, d=6, h=12, gate_w=4.5, gate_h=6.0, style='grim', seed=9,
                   lit=1.0)
    # dead woods and graves either side
    trees(90, lambda r: (r.choice((-1, 1)) * r.uniform(8, 60), r.uniform(-10, 260)), height, seed=5, kinds=('dead',),
          min_gap=4, scale=(0.8, 1.6))
    for i in range(40):
        side = rng.choice((-1, 1))
        x, y = side * rng.uniform(7, 22), rng.uniform(-4, 90)
        P.gravestone((x, y, height(x, y)), rng.uniform(-0.4, 0.4) + (0 if side < 0 else 0), seed=i, M=M)
    for side in (-1, 1):
        P.fence((side * 6.5, -10), (side * 6.5, 40), 1.0, height_fn=height, posts=2.2, rails=1)
    for i in range(6):
        x, y = rng.choice((-1, 1)) * rng.uniform(7, 14), rng.uniform(6, 50)
        P.bones_pile((x, y, height(x, y)), 0.6, 8, M, seed=i, skulls=1)
    P.torn_banner((-5.6, 10, height(-5.6, 10)), M['crimson'], M, seed=1)
    P.torn_banner((5.8, 24, height(5.8, 24)), M['crimson'], M, seed=2)
    rocks(30, rect_area(-30, 30, -10, 80), height, seed=6, avoid=lambda x, y: abs(x) < 6, moss=0.1)
    tufts(120, rect_area(-14, 14, -20, 12), height, seed=7, avoid=lambda x, y: abs(x) < 3.5,
          color=('4a4a30', '6a6440'))
    env.camera((0, -18, 5.0), (0, 60, 3.2), lens=30)
    return dict(bloom=0.26, bloom_threshold=0.55, vignette=0.42, saturation=1.05, shadows='0e1620')


def raid(scene):
    def height(x, y):
        crag = 26 * (1 - env.smoothstep(18, 46, math.hypot((x - 2) * 0.9, y - 92)))
        return env.fbm(x, y, 0.03, 5, 27) * 3.0 + crag + 0.003 * x * x
    causeway = [(0, -20), (0, 40), (1, 66)]
    env.terrain((300, 300), (220, 220), (0, 70), height, mat=_blight_ground('Grave earth'),
                masks={'path': env.road_mask(causeway, 5.0, 1.5)})
    dusk(scene, sun_az=95, sun_el=2.0, strength=3.2, haze_density=0.006, haze_color='a07080', clouds=0.9,
         zenith='241c38', horizon='d0583a', glow='ff7a4a', light_strength=0.5, sun_color='ff7048', cloud_lit='ff8060',
         cloud_dark='3a2438', haze_center=(0, 60, -2), haze_size=(500, 500, 40))
    env.far_ground(fog='5a3848', inner=150)
    env.mountains(9, 420, (50, 100), arc=(-1.2, 1.2), base_z=-5, seed=51, direction=90, fog='4a3048', fog_dist=600,
                  rock=('3a3638', '4a4446'), green='3a3a34')
    GM = arch.mats('grim')
    M = P.pm()
    cz = height(2, 92)
    # citadel: keep with spiky towers, curtain wall and gate
    arch.gatehouse((1, 70, height(1, 70) - 0.5), 0.0, w=12, d=6, h=11, gate_w=4.0, gate_h=5.4, style='grim', M=GM,
                   banner=M['crimson'], seed=2, lit=0.9)
    for (x, y, r, hh) in ((-16, 84, 3.4, 26), (18, 86, 3.2, 24), (2, 100, 5.0, 40), (-8, 104, 3.0, 30),
                          (12, 106, 3.0, 33), (-24, 74, 2.8, 16), (26, 74, 2.8, 16)):
        arch.tower((x, y, height(x, y) - 1), r, hh, 'cone', style='grim', M=GM, seed=int(x + y), lit=0.8,
                   banner=M['crimson'], roof_h=r * 3.6)
    for (p0, p1) in (((-6.5, 70), (-24, 74)), ((8.5, 70), (26, 74)), ((-24, 74), (-16, 84)), ((26, 74), (18, 86))):
        arch.wall(p0, p1, 9, 2.4, style='grim', M=GM, base_z=height(*p0) - 1)
    # crimson banners on the gate, plague glow in windows
    for x in (-3.6, 5.6):
        P.hanging_banner((x, 66.8, height(x, 66.8) + 9.4), 1.4, 4.2, M['crimson'], M,
                         emblem=None)
    env.point((1, 66, height(1, 66) + 3), 2500, PLAGUE, 1.5, name='Gate blight')
    env.sun((-0.7, -0.5, 0.45), 1.6, '8a9ad8', 2.0, name='Moon fill')
    for y in range(-4, 60, 9):
        for side in (-1, 1):
            x = side * 3.8
            P.brazier((x, y, height(x, y)), M, power=320, color=PLAGUE)
    env.haze((0, 30, -1.5), (120, 90, 5), 0.012, '9cf25c', 0.2, falloff=1.5, name='Blight mist', soft=True)
    # graveyard and dead trees before the walls
    rng = random.Random(6)
    for i in range(36):
        side = rng.choice((-1, 1))
        x, y = side * rng.uniform(6, 26), rng.uniform(-6, 48)
        P.gravestone((x, y, height(x, y)), rng.uniform(-0.4, 0.4), seed=i, M=M)
    trees(50, lambda r: (r.choice((-1, 1)) * r.uniform(8, 70), r.uniform(-10, 70)), height, seed=7, kinds=('dead',),
          min_gap=5, scale=(0.9, 1.7))
    for (x, y, r) in ((-12, 20, 3.0), (14, 34, 2.4), (-6, 50, 2.0), (9, 8, 2.2), (-15, 4, 2.6)):
        P.blight_pool((x, y, height(x, y)), r, PLAGUE, power=400)
    for i in range(5):
        x, y = rng.choice((-1, 1)) * rng.uniform(5, 14), rng.uniform(0, 40)
        P.torn_banner((x, y, height(x, y)), M['crimson'], M, seed=i + 3)
    rocks(30, rect_area(-40, 40, -10, 60), height, seed=8, avoid=lambda x, y: abs(x) < 5, moss=0.05)
    env.camera((-3, -10, height(-3, -10) + 4.0), (1, 60, 21), lens=28)
    return dict(bloom=0.24, bloom_threshold=0.58, vignette=0.42, saturation=1.08, shadows='1a1422')


def battle_summary(scene):
    def height(x, y):
        return env.fbm(x, y, 0.035, 4, 33) * 0.8 + 5 * env.smoothstep(40, 110, y)
    env.terrain((260, 260), (220, 220), (0, 60), height,
                mat=env.ground('Trampled field', grass=('4e5a30', '6a6e3a', '8a8048'), dry='9a8656',
                               dirt=('6a5038', '4e3c2c'), flowers=0.1, dry_amount=0.35, ash=0.18),
                masks={'path': lambda x, y: 0.5 * (1 - env.smoothstep(4, 16, math.hypot((x - 1) * 0.7, y - 12)))})
    golden(scene, sun_az=150, sun_el=6.0, strength=4.6, haze_density=0.0025, haze_color='e0a070', clouds=0.95,
           light_strength=0.5, zenith='3e3c5c', horizon='f2904c', mid='f2b070', upper='8e8ea8')
    env.far_ground(fog='c49070', inner=140)
    env.mountains(8, 420, (40, 80), arc=(-1.2, 1.2), base_z=-5, seed=61, direction=90, fog='a48078', fog_dist=650)
    M = P.pm()
    AM = arch.mats('warm')
    rng = random.Random(9)
    kz = height(-34, 110)
    arch.tower((-34, 110, kz), 4.0, 22, 'cone', M=AM, seed=2, banner=M['teal'])
    arch.tower((-20, 114, kz), 3.0, 16, 'crenel', M=AM, seed=3)
    arch.wall((-34, 110), (-20, 114), 8, 2.2, M=AM, base_z=kz - 1)
    from rk import geo
    from mathutils import Matrix
    # wrecked war wagon, tipped onto its side wheels, still smouldering
    wz = height(10, 18)
    ww = P.war_wagon((10.0, 18.0, wz - 0.3), math.radians(200), M, seed=2, power=0)
    piv = Vector((10.0, 18.0, wz))
    for o in ww:
        if o.type == 'MESH':
            geo.transform(o, Matrix.Translation(piv) @ Matrix.Rotation(0.28, 4, 'Y') @ Matrix.Translation(-piv))
    P.catapult_wreck((-11, 24, height(-11, 24)), 0.6, M)
    P.smoke_column((-11, 24, height(-11, 24)), height=24, radius=0.9, density=0.7, color='3e3a38', drift=(5, 3))
    P.smoke_column((10, 19, wz), height=20, radius=0.7, density=0.7, color='3e3a38', drift=(4, 2), name='Smoke 2')
    P.smoke_column((30, 50, height(30, 50)), height=28, radius=1.1, density=0.5, color='4a4644', drift=(6, 3),
                   name='Smoke 3')
    for i, (x, y, cl) in enumerate(((-7, 14, 'teal'), (6, 9, 'crimson'), (-14, 6, 'crimson'), (15, 12, 'teal'),
                                    (-4, 26, 'crimson'), (18, 28, 'teal'))):
        P.torn_banner((x, y, height(x, y)), M[cl], M, seed=40 + i, h=3.6, lean=0.18)
    for i, (x, y) in enumerate(((-5, 4), (4.5, 3), (-9, 10), (7, 13), (-2, 17), (12, 6), (-12, 16), (3, 21))):
        P.fallen_shield((x, y, height(x, y)), M, seed=60 + i, emblem='lantern' if i % 2 == 0 else 'skull')
    for (x, y) in ((-11, 24), (10.5, 19)):
        P.campfire((x, y, height(x, y)), 0.6, M, power=600, smoke=False, seed=int(x))
    for i in range(70):
        x, y = rng.uniform(-20, 20), rng.uniform(-8, 34)
        if abs(x - 1) < 3.5 and y < 10:
            continue
        kind = rng.choice(['sword', 'spear', 'axe', 'sword', 'shield', 'shield2', 'banner', 'helm'])
        z = height(x, y)
        if kind in ('sword', 'spear', 'axe'):
            P.stuck_weapon((x, y, z), kind, M, seed=i)
        elif kind == 'shield':
            P.fallen_shield((x, y, z), M, seed=i, emblem='lantern')
        elif kind == 'shield2':
            P.fallen_shield((x, y, z), M, seed=i, emblem='skull')
        elif kind == 'banner' and i % 3 == 0:
            P.torn_banner((x, y, z), M['teal'] if rng.random() < 0.5 else M['crimson'], M, seed=i, h=3.4)
        else:
            P.skull((x, y, z), 0.14, M, rot=rng.uniform(0, 6))
    for i in range(8):
        x, y = rng.uniform(-18, 18), rng.uniform(2, 30)
        P.bones_pile((x, y, height(x, y)), 0.7, 8, M, seed=i + 20, skulls=1)
    # the Lantern standard still flies over the field
    P.banner_pole((-2.5, 8.0, height(-2.5, 8)), 0.15, M['teal'], h=5.6, width=1.1, drop=2.2)
    trees(30, rect_area(-90, 90, 40, 120), height, seed=10, kinds=('dead', 'broad'), min_gap=6)
    rocks(30, rect_area(-40, 40, -10, 60), height, seed=11, moss=0.15)
    tufts(160, rect_area(-16, 16, -14, 4), height, seed=12)
    env.camera((2, -11, height(2, -11) + 4.2), (1, 30, 3.5), lens=28)
    return dict(bloom=0.22, vignette=0.4, saturation=1.1)
