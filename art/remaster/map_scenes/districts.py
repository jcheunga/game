"""District map recipes (one per route id). recipe(scene) -> post-grade options."""
import math
import random

from mathutils import Vector

from rk import arch, env
from rk import dressing as P
from rk.palette import LANTERN_TEAL, PLAGUE, ROT_CRIMSON

from menu_scenes.common import dusk, golden, night

from battle_scenes import kit as K

from . import common as MC
from .common import Fields, Grid, Polyline, bridge, carve, flatten, forest, line_mask, smooth_path, village, \
    walled_town


def _hills(seed, amp=4.0, scale=0.018):
    return lambda x, y: env.fbm(x, y, scale, 4, seed) * amp


def city(scene):
    rng = random.Random(1)
    road = Polyline(smooth_path([(-90, -70), (-60, -42), (-30, -34), (-6, -16), (8, 2), (20, 16), (30, 24)]))
    road2 = Polyline(smooth_path([(30, 42), (24, 60), (10, 90)]))
    river = Polyline(smooth_path([(-95, 34), (-60, 22), (-30, 30), (-6, 14), (10, -8), (30, -32), (60, -46), (95, -58)]))
    base = _hills(3, 5.0)
    town = (32, 32)

    def h0(x, y):
        z = base(x, y) + 2.0
        t = 1 - env.smoothstep(14, 26, math.hypot(x - town[0], y - town[1]))
        z = z * (1 - t) + 4.5 * t
        return z
    h1 = flatten(h0, road, 5.0)
    h1 = flatten(h1, road2, 5.0)
    height = carve(h1, river, 7.0, 2.4, soft=3.5)
    fields = Fields([(-44, -46, 18), (8, -42, 16), (-30, 6, 12), (55, 0, 14)], seed=4)
    path = lambda x, y: max(line_mask(road, 3.6)(x, y), line_mask(road2, 3.6)(x, y))
    shore = lambda x, y: 1 - env.smoothstep(4.0, 6.0, river.dist(x, y, cutoff=8))
    cobble = lambda x, y: 1 - env.smoothstep(12, 14, math.hypot(x - town[0], y - town[1]))
    mat = MC.map_ground('City map ground')
    MC.build_terrain(height, mat, masks={'path': path, 'shore': shore, 'cobble': cobble}, fields=fields)
    MC.water(-0.9, name='City river', deep='24464a', shallow='5a8a7c')
    _map_light(scene)
    grid = Grid(4.0)
    M = arch.mats('warm')
    PM = P.pm()
    walled_town(town, 15.0, height, rng, M=M, n_houses=22, towers=8, banner=PM['teal'], grid=grid, gate_angles=(225,))
    # road villages and a hilltop keep
    road_yaw = lambda x, y: _road_yaw(road, x, y)
    for (c, n) in (((-44, -40), 9), ((6, -30), 8), ((-34, 10), 6)):
        village(c, n, height, rng, 9.0, M=M, grid=grid, avoid=lambda x, y: path(x, y) > 0.1 or shore(x, y) > 0.1,
                align=road_yaw, lit=0.2)
    kp = (-58, 48)
    arch.tower((kp[0], kp[1], height(*kp) - 0.5), 3.4, 13, 'cone', M=M, seed=3, banner=PM['teal'], roof_h=6)
    arch.wall((kp[0] - 6, kp[1] - 5), (kp[0] + 6, kp[1] - 5), 3.5, 1.4, M=M, base_z=height(*kp) - 1)
    grid.add(*kp, 8)
    # bridge where the road meets the river
    bridge((1.5, -10.5), (13.5, 5.5), 0.6, 5.0, M)
    avoid_all = lambda x, y: (path(x, y) > 0.05 or shore(x, y) > 0.2 or fields.sample(x, y)[0] > 0.3
                              or math.hypot(x - town[0], y - town[1]) < 19)
    forest(260, MC.rect(-90, -40, 20, 90), height, rng, ('broad', 'conifer'), grid=grid, avoid=avoid_all, seed=2)
    forest(200, MC.rect(40, 90, -75, -20), height, rng, ('broad', 'cypress'), grid=grid, avoid=avoid_all, seed=3)
    MC.copses(14, height, rng, MC.everywhere(), kinds=('broad', 'cypress'), grid=grid, avoid=avoid_all, seed=4)
    forest(30, MC.everywhere(), height, rng, ('olive', 'cypress'), grid=grid, avoid=avoid_all, gap=8.0, seed=5)
    MC.scatter_rocks(30, height, rng, avoid=avoid_all, grid=grid)
    MC.camera(2.0)
    return dict(bloom=0.1, vignette=0.22, saturation=1.08)


def _map_light(scene, az=215, el=40, strength=4.2, sun='ffd8a8', exposure=0.25, **kw):
    golden(scene, sun_az=az, sun_el=el, strength=strength, haze_density=0.0, clouds=0.4, light_strength=0.8, **kw)
    for o in scene.objects:
        if o.type == 'LIGHT' and o.data.type == 'SUN':
            o.data.color = env.srgb(sun)[:3]
    scene.view_settings.exposure = exposure


def _road_yaw(line, x, y):
    best = None
    for a, b in zip(line.pts, line.pts[1:]):
        d = env.seg_dist(x, y, *a, *b)
        if best is None or d < best[0]:
            best = (d, a, b)
    _, a, b = best
    return math.atan2(b[1] - a[1], b[0] - a[0])




def _gravestone_protos():
    return [MC.joined_proto(f'mapgrave{i}', lambda i=i: P.gravestone((0, 0, 0), 0.0, seed=i, kind=k))
            for i, k in enumerate(('round', 'cross', 'slab'))]


def _tent_proto(a, b, key):
    return MC.joined_proto(key, lambda: P.pavilion((0, 0, 0), 0.0, r=2.0, a=a, b=b, door=False, count=12))


# ------------------------------------------------------------------ Saltwake Docks
def harbor(scene):
    rng = random.Random(2)
    coast = Polyline(smooth_path([(-95, 14), (-60, 20), (-36, 8), (-12, 16), (10, 10), (34, 18), (52, 30), (70, 22),
                                  (95, 28)]))
    road = Polyline(smooth_path([(-90, -70), (-60, -40), (-34, -18), (-12, 0), (6, 2)]))
    road2 = Polyline(smooth_path([(6, 2), (30, -6), (58, 4), (66, 14)]))

    def h0(x, y):
        # land below the coastline, sea above it
        cy = _y_on(coast, x)
        z = env.fbm(x, y, 0.02, 4, 8) * 3.5 + 2.5
        if y > cy:
            z = z - (y - cy) * 0.9 - 2.0
        else:
            z += 0.0 + 4.0 * env.smoothstep(60, 95, x) * env.smoothstep(cy - 30, cy, y)  # headland cliffs
        return z
    height = flatten(flatten(h0, road, 5.0), road2, 5.0)
    path = lambda x, y: max(line_mask(road, 3.4)(x, y), line_mask(road2, 3.4)(x, y))
    shore = lambda x, y: 1 - env.smoothstep(2.0, 4.0, abs(_y_on(coast, x) - y)) if y < _y_on(coast, x) + 2 else 1.0
    town = (6, 0)
    cobble = lambda x, y: 1 - env.smoothstep(11, 13, math.hypot(x - town[0], (y - town[1]) * 1.3))
    fields = Fields([(-50, -52, 18), (30, -50, 18)], seed=6)
    mat = MC.map_ground('Harbor ground', grass=('4a5a34', '5e6e40', '7a8050'), dry='9a9068', sand='cdbb90',
                        field_colors=('b8a050', '6e7e40', '7a6448', '9a9858', '5a6e3a'))
    MC.build_terrain(height, mat, masks={'path': path, 'shore': shore, 'cobble': cobble}, fields=fields)
    MC.water(-0.6, name='Sea', deep='1e3c48', shallow='4a7a7a')
    _map_light(scene, az=225, el=38, strength=3.8, sun='ffe6c8', zenith='5a7090', horizon='e8d8c0', mid='eee4d0',
               upper='a8bccc')
    grid = Grid(4.0)
    M = arch.mats('grey')
    PM = P.pm()
    village(town, 20, height, rng, 11.0, style='grey', M=M, grid=grid, avoid=lambda x, y: y > _y_on(coast, x) - 1.5,
            lit=0.3, size=(4.0, 5.5))
    # quays: piers into the sea with boats and cranes
    for x in (-8, 2, 12, 22):
        y0 = _y_on(coast, x) - 1.0
        MC.pier((x, y0), (x + rng.uniform(-2, 2), y0 + 14), 2.4, 0.4, PM)
        K.boat((x + 3.2, y0 + 9 + rng.uniform(-2, 2), -0.6), math.pi / 2 + rng.uniform(-0.2, 0.2), L=6.5, M=PM,
               sail=PM['canvas'] if rng.random() < 0.5 else PM['teal'])
    for x in (-3, 17):
        K.crane((x, _y_on(coast, x) - 0.5, height(x, _y_on(coast, x) - 0.5)), 0.0, PM, h=7.0)
    for i in range(5):
        K.boat((rng.uniform(-60, 80), rng.uniform(45, 75), -0.6), rng.uniform(0, 6.28), L=rng.uniform(6, 10), M=PM,
               sail=rng.choice((PM['canvas'], PM['teal'], PM['crimson'])))
    # lighthouse on the headland
    lx, ly = 78, _y_on(coast, 78) - 4
    arch.tower((lx, ly, height(lx, ly) - 0.5), 2.4, 14, 'crenel', style='grey', M=M, seed=9)
    env.point((lx, ly, height(lx, ly) + 15.5), 2500, 'ffb04a', 1.0, name='Lighthouse')
    from rk import geo
    geo.sphere('Beacon', 1.0, (lx, ly, height(lx, ly) + 15.0), env.glow_mat('ffb04a', 10, name='Beacon fire'),
               env.C['FX'], 12, 8)
    grid.add(lx, ly, 6)
    land = lambda x, y: y > _y_on(coast, x) - 3
    avoid = lambda x, y: land(x, y) or path(x, y) > 0.05 or fields.sample(x, y)[0] > 0.3 or math.hypot(
        x - town[0], y - town[1]) < 16
    MC.copses(12, height, rng, MC.rect(-90, 90, -75, 0), kinds=('conifer', 'broad'), grid=grid, avoid=avoid, seed=21)
    forest(40, MC.rect(-90, 90, -75, 10), height, rng, ('conifer',), grid=grid, avoid=avoid, gap=7, seed=22)
    MC.scatter_rocks(40, height, rng, area=MC.rect(-90, 90, -20, 40),
                     avoid=lambda x, y: abs(y - _y_on(coast, x)) > 4 or path(x, y) > 0.1, size=(0.8, 2.0), moss=0.1)
    MC.camera(1.0)
    return dict(bloom=0.12, vignette=0.22, saturation=1.02)


def _y_on(line, x):
    pts = line.pts
    for a, b in zip(pts, pts[1:]):
        if a[0] <= x <= b[0] or b[0] <= x <= a[0]:
            t = (x - a[0]) / ((b[0] - a[0]) or 1e-6)
            return a[1] + (b[1] - a[1]) * t
    return pts[0][1] if x < pts[0][0] else pts[-1][1]


# ------------------------------------------------------------------ Emberforge March
def foundry(scene):
    rng = random.Random(3)
    lava = Polyline(smooth_path([(-95, 40), (-60, 30), (-30, 44), (0, 26), (20, 6), (50, 0), (95, -10)]))
    road = Polyline(smooth_path([(-95, -50), (-50, -30), (-20, -26), (8, -12), (30, -20), (60, -34), (95, -40)]))
    base = lambda x, y: env.fbm(x, y, 0.025, 5, 31) * 6.0 + 3.0 + 6 * env.smoothstep(30, 80, y)
    h1 = flatten(base, road, 5.0)
    height = carve(h1, lava, 3.6, 3.0, soft=2.0)
    path = line_mask(road, 3.6)
    mat = MC.map_ground('Foundry ground', grass=('4a4036', '5a4c40', '6a5a4a'), dry='6e5e4a', dry_amount=0.4,
                        dirt=('5a4a3a', '3e322a'), rock='5a5450', ash=0.6, ash_color='2a2624')
    MC.build_terrain(height, mat, masks={'path': path})
    MC.water(-1.2, mat=MC.lava_mat(), name='Lava')
    _map_light(scene, az=230, el=34, strength=3.6, sun='ffc8a0', exposure=0.35, zenith='3a3440', horizon='c87a4a',
               mid='d89a6a', upper='7a6a70')
    grid = Grid(4.0)
    M = arch.mats('grey')
    M['stone'] = env.masonry('Foundry walls', '6e5e50', '5a4c40', '2e2620', soot=0.7)
    M['roof'] = env.roof_tiles('Soot slate', '3a3a3e', '2e2e32', tile=(0.3, 0.2), moss=0.0, curve=False)
    PM = P.pm()
    for (x, y) in ((-46, -14), (-10, -2), (26, -40), (54, -18), (-70, -40)):
        z = height(x, y)
        K.furnace((x, y, z), rng.uniform(-0.4, 0.4), PM, w=4.4, h=6.0, power=1600)
        K.chimney_stack((x + 5, y + 3, z), 16.0, 1.2, PM)
        grid.add(x, y, 7)
        for k in range(3):
            K.slag_heap((x + rng.uniform(-8, 8), y + rng.uniform(-7, -3), z), rng.uniform(1.2, 2.2), seed=k)
    village((-16, -40), 10, height, rng, 10, style='grey', M=M, grid=grid, avoid=lambda x, y: path(x, y) > 0.05,
            lit=0.8, align=lambda x, y: _road_yaw(road, x, y))
    village((40, -2), 8, height, rng, 9, style='grey', M=M, grid=grid, avoid=lambda x, y: path(x, y) > 0.05, lit=0.8)
    walled_town((-48, 52), 12.0, height, rng, style='grey', M=M, n_houses=10, towers=6, banner=PM['teal'], grid=grid,
                wall_h=4.5, keep_h=13)
    village((66, -46), 7, height, rng, 8, style='grey', M=M, grid=grid, lit=0.8, avoid=lambda x, y: path(x, y) > 0.05)
    avoid = lambda x, y: path(x, y) > 0.05 or lava.dist(x, y, cutoff=6) < 5
    forest(70, MC.everywhere(), height, rng, ('dead',), grid=grid, avoid=avoid, gap=5, seed=31, scale=(1.0, 1.5))
    MC.scatter_rocks(70, height, rng, avoid=avoid, grid=grid, moss=0.0, size=(1.0, 2.8))
    MC.camera(3.0)
    return dict(bloom=0.24, bloom_threshold=0.55, vignette=0.24, saturation=1.06)


# ------------------------------------------------------------------ Ashen Ward
def quarantine(scene):
    rng = random.Random(4)
    road = Polyline(smooth_path([(-95, -64), (-50, -40), (-20, -22), (0, -6)]))
    road2 = Polyline(smooth_path([(26, 18), (50, 30), (95, 40)]))
    base = _hills(41, 3.0)
    ward = (8, 6)
    h0 = lambda x, y: base(x, y) + 2.0
    height = flatten(flatten(h0, road, 5.0), road2, 5.0)
    path = lambda x, y: max(line_mask(road, 3.4)(x, y), line_mask(road2, 3.4)(x, y))
    cobble = lambda x, y: 1 - env.smoothstep(17, 19, math.hypot(x - ward[0], y - ward[1]))
    fields = None
    mat = MC.map_ground('Ward ground', grass=('4e5640', '5e644a', '6e7254'), dry='7a7860', dry_amount=0.4,
                        ash=0.4, ash_color='4a4840', field_colors=('6a6448', '585a40', '5e5038', '6e6a4c', '4e5438'),
                        cobble=('6e6c64', '5a5852'))
    MC.build_terrain(height, mat, masks={'path': path, 'cobble': cobble})
    _map_light(scene, az=230, el=40, strength=3.4, sun='e8e0d0', exposure=0.3, zenith='5a6470', horizon='c8c0a8',
               mid='d0c8b4', upper='98a0a4')
    grid = Grid(4.0)
    M = arch.mats('grim')
    PM = P.pm()
    walled_town(ward, 18.0, height, rng, style='grim', M=M, n_houses=26, towers=10, banner=PM['crimson'], grid=grid,
                gate_angles=(225, 45), keep_h=14)
    # outer palisade ring with checkpoint gates and purge fires
    for k in range(18):
        a0, a1 = k * math.tau / 18, (k + 0.8) * math.tau / 18
        p0 = (ward[0] + math.cos(a0) * 27, ward[1] + math.sin(a0) * 27)
        p1 = (ward[0] + math.cos(a1) * 27, ward[1] + math.sin(a1) * 27)
        if any(abs(((math.degrees(a0) - g + 180) % 360) - 180) < 12 for g in (225, 45)):
            P.brazier((p0[0], p0[1], height(*p0)), PM, power=400, h=1.0)
            continue
        K.palisade(p0, p1, 3.0, PM, seed=k, height_fn=height)
        grid.add(*p0, 2)
    for (x, y, r) in ((-40, -12, 3), (40, -12, 2.5), (-28, 34, 2.4), (52, 14, 2.2)):
        P.blight_pool((x, y, height(x, y)), r, PLAGUE, power=200)
        grid.add(x, y, r + 1)
    for (x, y) in ((-34, -50), (28, -34), (-60, 40), (60, 50)):
        K.ward_sigil((x, y, height(x, y) + 0.05), 5.0, PLAGUE, power=150)
    gp = _gravestone_protos()
    for i in range(70):
        x, y = rng.uniform(30, 75), rng.uniform(-60, -22)
        if path(x, y) > 0.1:
            continue
        MC.place(rng.choice(gp), x, y, height, rng.uniform(-0.3, 0.3) + math.pi, 1.4)
    village((-52, 20), 8, height, rng, 9, style='grim', M=M, grid=grid, lit=0.6, avoid=lambda x, y: path(x, y) > 0.05)
    avoid = lambda x, y: path(x, y) > 0.05 or math.hypot(x - ward[0], y - ward[1]) < 30
    forest(160, MC.everywhere(), height, rng, ('dead',), grid=grid, avoid=avoid, gap=4.0, seed=41, scale=(1.0, 1.6))
    MC.copses(6, height, rng, MC.everywhere(), kinds=('conifer',), grid=grid, avoid=avoid, seed=42,
              palette=('2a3428', '3a4434', '5a5a40'))
    MC.camera(2.0)
    return dict(bloom=0.18, vignette=0.24, saturation=0.92)


# ------------------------------------------------------------------ Thornwall Pass
def thornwall(scene):
    rng = random.Random(5)
    road = Polyline(smooth_path([(-20, -80), (-10, -50), (8, -36), (-6, -18), (10, -2), (-4, 14), (8, 30), (2, 50),
                                 (14, 80)], steps=8))
    lake = (-46, -30)
    lx, ly = lake
    LAKE_Z = (env.fbm(lx, ly, 0.03, 5, 51) * 6.0 + 0.006 * lx * lx * (1 + 0.6 * env.smoothstep(-60, 60, ly))
              + 10 * env.smoothstep(-70, 80, ly)) - 0.6

    def h0(x, y):
        valley = 0.006 * x * x * (1 + 0.6 * env.smoothstep(-60, 60, y))
        z = env.fbm(x, y, 0.03, 5, 51) * 6.0 + valley + 10 * env.smoothstep(-70, 80, y)
        peaks = max(0.0, env.fbm(x, y, 0.012, 3, 52)) * 30 * env.smoothstep(25, 60, abs(x))
        z += peaks
        lk = 1 - env.smoothstep(13, 30, math.hypot((x - lake[0]) * 0.8, y - lake[1]))
        lk = lk * lk * (3 - 2 * lk)
        return z * (1 - lk) + (LAKE_Z - 0.8) * lk
    height = flatten(h0, road, 4.5)
    path = line_mask(road, 3.0)
    shore = lambda x, y: 1 - env.smoothstep(15, 18, math.hypot((x - lake[0]) * 0.8, y - lake[1]))
    mat = MC.map_ground('Pass ground', grass=('4a5a38', '5e6a44', '7a7a52'), dry='8a8460', dry_amount=0.3,
                        rock='7a7672', snow_height=16.0, sand='9a9488')
    MC.build_terrain(height, mat, masks={'path': path, 'shore': shore})
    from rk import geo as _geo
    _geo.cylinder('Frozen lake', 15.5, 0.1, (lake[0], lake[1], LAKE_Z), P.S.flat('c4d4dc', name='Lake ice', rough=0.3),
                  env.C['Terrain'], 48, bevel=0)
    env.C['Terrain'].objects[-1].scale = (1.25, 1.0, 1.0)
    _map_light(scene, az=228, el=34, strength=4.0, sun='fff0e0', zenith='4a6890', horizon='e8dcd0', mid='eee6dc',
               upper='a8bcd0', exposure=0.1)
    grid = Grid(4.0)
    M = arch.mats('grey')
    PM = P.pm()
    for (x, y) in ((8, -36), (10, -2), (8, 30)):
        tx, ty = x + 9, y + 2
        z = height(tx, ty)
        arch.tower((tx, ty, z - 0.5), 2.6, 10, 'cone', style='grey', M=M, seed=int(y), banner=PM['teal'], roof_h=5)
        arch.wall((tx - 6, ty - 3), (tx + 6, ty - 3), 3.5, 1.4, M=M, base_z=z - 1)
        P.brazier((tx - 3, ty - 4.5, height(tx - 3, ty - 4.5)), PM, power=300)
        grid.add(tx, ty, 8)
    # the Thornwall gate across the top of the pass
    gz = height(4, 58)
    arch.gatehouse((4, 58, gz - 0.5), 0.0, w=10, d=6, h=9, gate_w=3.4, gate_h=4.2, style='grey', M=M,
                   banner=PM['teal'], seed=5)
    arch.wall((-30, 60), (-3, 58), 7, 2.2, M=M, base_z=gz - 1)
    arch.wall((11, 58), (40, 62), 7, 2.2, M=M, base_z=gz - 1)
    grid.add(4, 58, 14)
    village((-36, -50), 7, height, rng, 8, style='grey', M=M, grid=grid, lit=0.6,
            avoid=lambda x, y: path(x, y) > 0.05 or shore(x, y) > 0.2)
    avoid = lambda x, y: road.dist(x, y, cutoff=12) < 9 or shore(x, y) > 0.1 or height(x, y) > 24
    forest(240, MC.everywhere(), height, rng, ('conifer',), grid=grid, avoid=avoid, gap=3.4, seed=51,
           palette=('1e3020', '2e4428', '4e5e34'))
    MC.scatter_rocks(140, height, rng, avoid=lambda x, y: path(x, y) > 0.05 or shore(x, y) > 0.3, grid=grid, moss=0.1,
                     size=(1.2, 3.4))
    MC.camera(10.0)
    return dict(bloom=0.12, vignette=0.24, saturation=1.0)


# ------------------------------------------------------------------ Hollow Basilica
def _cathedral(c, height, M, grid, L=34.0, W=14.0, H=14.0):
    """Ruined basilica: long nave with clustered piers and broken roof, transept, twin west towers, apse."""
    cx, cy = c
    z = height(cx, cy) - 0.4
    wall = M['stone']
    out = []
    for s in (-1, 1):
        arch.slab((cx + s * W / 2, cy, z + H / 2), (0, 1, 0), (0, 0, 1), L, H, 1.2, wall, out, 'Nave wall')
        for k in range(6):
            y = cy - L / 2 + 3 + k * (L - 6) / 5
            arch.slab((cx + s * (W / 2 + 0.8), y, z + H * 0.45), (0, 1, 0), (0, 0, 1), 1.2, H * 0.9, 1.6, M['stone2'],
                      out, 'Buttress')
            arch.slab((cx + s * (W / 2 + 0.62), y + 2.6, z + H * 0.55), (0, 1, 0), (0, 0, 1), 1.6, H * 0.5, 0.1,
                      M['glass'], out, 'Lancet')
    # broken roof: only the western half survives
    a = math.radians(50)
    for s in (-1, 1):
        run = W / 2 + 0.6
        arch.slab((cx + s * run / 2, cy - L / 4, z + H + run * math.tan(a) / 2), (0, 1, 0),
                  (-s * math.cos(a), 0, math.sin(a)), L / 2, run / math.cos(a), 0.4, M['roof'], out, 'Nave roof')
    # transept
    for s in (-1, 1):
        arch.slab((cx + s * (W / 2 + 5), cy + L * 0.15, z + H * 0.4), (1, 0, 0), (0, 0, 1), 10, H * 0.8, 1.0, wall, out,
                  'Transept wall')
    # apse
    arch.tower((cx, cy + L / 2, z), W / 2, H * 0.9, 'crenel', M=M, ruined=0.3, seed=7)
    # west towers
    for s in (-1, 1):
        arch.tower((cx + s * (W / 2 - 1), cy - L / 2, z), 3.4, H * 2.0, 'cone' if s < 0 else 'crenel', M=M, seed=8 + s,
                   ruined=0.0 if s < 0 else 0.35, roof_h=10)
    for k in range(4):
        y = cy - L / 2 + 4 + k * (L - 8) / 3
        for s in (-1, 1):
            K.gothic_pier((cx + s * W * 0.25, y, z), H * 0.95, 0.6)
    for t in range(-3, 4):
        grid.add(cx, cy + t * L / 7, W * 0.8)
    return out


def basilica(scene):
    rng = random.Random(6)
    road = Polyline(smooth_path([(-90, -70), (-50, -50), (-24, -36), (-6, -24)]))
    road2 = Polyline(smooth_path([(16, 26), (40, 40), (90, 54)]))
    c = (4, 4)
    base = _hills(61, 3.0)

    def h0(x, y):
        z = base(x, y) + 2.0
        t = 1 - env.smoothstep(24, 34, math.hypot(x - c[0], (y - c[1]) * 0.8))
        return z * (1 - t) + 5.0 * t
    height = flatten(flatten(h0, road, 5.0), road2, 5.0)
    path = lambda x, y: max(line_mask(road, 3.4)(x, y), line_mask(road2, 3.4)(x, y))
    cobble = lambda x, y: 1 - env.smoothstep(21, 23, math.hypot(x - c[0], (y - c[1]) * 0.75))
    mat = MC.map_ground('Basilica ground', grass=('4a5238', '5a6040', '72724e'), dry='86805e', dry_amount=0.4,
                        cobble=('8a8478', '726c62'), ash=0.2, ash_color='4a4640')
    MC.build_terrain(height, mat, masks={'path': path, 'cobble': cobble})
    _map_light(scene, az=230, el=36, strength=3.2, sun='f0dcc0', exposure=0.2, zenith='4a5468', horizon='d0c0a4',
               mid='d8ccb4', upper='8a90a0')
    grid = Grid(4.0)
    M = arch.mats('grey')
    M['glass'] = env.window_glass('ffcf80', 2.4, 'Stained glass')
    PM = P.pm()
    _cathedral(c, height, M, grid, L=44.0, W=17.0, H=16.0)
    for k in range(6):
        x, y = rng.uniform(-56, -22), rng.uniform(-14, 36)
        MC.minihouse((x, y, height(x, y) - 0.2), rng.choice((0, math.pi / 2)), 4.0, 3.0, 3.0, M, style='grey',
                     chimney=False, timber=False)
        grid.add(x, y, 3.5)
    arch.arcade((c[0] + 16, c[1] - 14), (c[0] + 16, c[1] + 10), bays=6, h=4.2, depth=3.0, M=M, seed=3)
    grid.add(c[0] + 16, c[1] - 2, 8)
    gp = _gravestone_protos()
    for i in range(160):
        x, y = rng.uniform(-60, -16), rng.uniform(-20, 40)
        if grid.near(x, y, 1.2) or path(x, y) > 0.1:
            continue
        MC.place(rng.choice(gp), x, y, height, math.pi + rng.uniform(-0.3, 0.3), 1.5)
        grid.add(x, y, 1.0)
    village((52, -36), 8, height, rng, 9, style='grey', M=M, grid=grid, lit=0.4, avoid=lambda x, y: path(x, y) > 0.05)
    avoid = lambda x, y: path(x, y) > 0.05 or math.hypot(x - c[0], (y - c[1]) * 0.8) < 28
    forest(120, MC.everywhere(), height, rng, ('dead',), grid=grid, avoid=avoid, gap=4.5, seed=61, scale=(1.1, 1.7))
    MC.copses(8, height, rng, MC.everywhere(), kinds=('cypress',), grid=grid, avoid=avoid, seed=62)
    MC.camera(4.0)
    return dict(bloom=0.16, vignette=0.24, saturation=0.96)


# ------------------------------------------------------------------ Mire of Saints
def mire(scene):
    rng = random.Random(7)
    route = Polyline(smooth_path([(-90, -60), (-56, -40), (-30, -32), (-8, -10), (14, 2), (36, 20), (60, 26),
                                  (90, 40)]))
    islands = [(-46, 24, 14), (30, -30, 16), (-8, 50, 12), (62, -2, 10), (-70, -30, 10), (6, -10, 8), (-30, -60, 9),
               (50, 50, 11), (78, -40, 9), (-80, 60, 10), (20, 18, 6), (-20, 36, 5)]

    def h0(x, y):
        z = env.fbm(x, y, 0.03, 4, 71) * 2.0 - 0.9
        for (ix, iy, r) in islands:
            t = 1 - env.smoothstep(r * 0.6, r, math.hypot(x - ix, y - iy))
            z = max(z, z * (1 - t) + (0.9 + env.fbm(x, y, 0.08, 2, 72) * 0.6) * t)
        return z
    height = h0
    path = lambda x, y: 0.0
    mudm = lambda x, y: 1 - env.smoothstep(-0.5, 0.2, height(x, y))
    mat = MC.map_ground('Mire ground', grass=('3e4c30', '4e5c36', '66703e'), dry='76744a', dry_amount=0.3,
                        sand='5a5236', mud=('3a3424', '4a422e'))
    MC.build_terrain(height, mat, masks={'mud': mudm})
    MC.water(-0.25, mat=MC.glow_water_mat(PLAGUE, '34442e', 0.06, 'Mire water'))
    _map_light(scene, az=232, el=40, strength=3.6, sun='eef0d0', exposure=0.5, zenith='4a5a5a', horizon='a8b090',
               mid='b8bca0', upper='7a8a86')
    grid = Grid(4.0)
    M = arch.mats('grim')
    PM = P.pm()
    MC.causeway(route, z=0.25, width=2.8, M=PM)
    for i in range(len(route.pts) - 1):
        a, b = route.pts[i], route.pts[i + 1]
        grid.add((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, 2.5)
    # drowned chapel and bell tower on the big island
    ix, iy, _ = islands[0]
    z = height(ix, iy)
    MC.minihouse((ix, iy, z - 0.8), 0.3, 10, 6, 5, M, style='grim', chimney=False, lit=True, timber=False)
    arch.tower((ix + 7, iy + 2, z - 1.0), 2.2, 13, 'cone', style='grim', M=M, seed=3, roof_h=6, lit=0.9)
    grid.add(ix, iy, 9)
    for (x, y, r) in ((30, -30, 9), (-8, 50, 7)):
        village((x, y), 6, height, rng, r * 0.6, style='grim', M=M, grid=grid, lit=0.7,
                avoid=lambda x, y: height(x, y) < 0.3)
    for k in range(60):
        x, y = rng.uniform(-90, 90), rng.uniform(-70, 80)
        if height(x, y) > -0.1 and height(x, y) < 0.6 and not grid.near(x, y, 2):
            K.reeds((x, y, height(x, y)), 26, 3.0, seed=k, r=2.0)
    for (x, y) in ((-20, 10), (44, 0), (-60, 50), (20, 40), (70, -50)):
        env.point((x, y, 1.5), 400, PLAGUE, 3.0, name='Marsh light')
    avoid = lambda x, y: height(x, y) < 0.2
    forest(220, MC.everywhere(), height, rng, ('dead', 'broad'), grid=grid, avoid=avoid, gap=3.2, seed=71,
           palette=('2a3a24', '3a4a2c', '56603a'))
    MC.camera(0.0)
    return dict(bloom=0.18, vignette=0.24, saturation=0.96)


# ------------------------------------------------------------------ Sunfall Steppe
def steppe(scene):
    rng = random.Random(8)
    road = Polyline(smooth_path([(-95, -40), (-60, -30), (-30, -36), (0, -20), (30, -26), (60, -10), (95, -16)]))
    river = Polyline(smooth_path([(-95, 50), (-50, 40), (-10, 52), (20, 36), (50, 46), (95, 30)]))
    base = lambda x, y: env.fbm(x, y, 0.015, 4, 81) * 2.4 + 3.0
    height = carve(flatten(base, road, 5.0), river, 6.0, 2.0)
    path = line_mask(road, 3.6)
    shore = lambda x, y: 1 - env.smoothstep(3.5, 5.0, river.dist(x, y, cutoff=6))
    fields = Fields([(56, 10, 14)], seed=9)
    mat = MC.map_ground('Steppe ground', grass=('5e6a34', '7e8240', 'a89a50'), dry='c4a660', dry_amount=0.55,
                        sand='c8b07a', field_colors=('d0b050', 'a89a48', '8a6a40', 'c4a854', '9a9040'))
    MC.build_terrain(height, mat, masks={'path': path, 'shore': shore}, fields=fields)
    MC.water(-0.6, name='Steppe river', deep='2a4a48', shallow='5a8070')
    _map_light(scene, az=222, el=28, strength=4.6, sun='ffcf98', exposure=0.15)
    grid = Grid(4.0)
    PM = P.pm()
    M = arch.mats('warm')
    # Steppe warlord's siege ring of crimson pavilions
    tp = [_tent_proto(ROT_CRIMSON, '3a3030', 'maptentr'), _tent_proto('6a2a20', 'c8b088', 'maptentr2')]
    sc = (-32, 8)
    for k in range(14):
        a = k * math.tau / 14
        x, y = sc[0] + math.cos(a) * 12, sc[1] + math.sin(a) * 10
        MC.place(rng.choice(tp), x, y, height, a + math.pi / 2, rng.uniform(0.9, 1.2))
        grid.add(x, y, 3)
    P.pavilion((sc[0], sc[1], height(*sc)), 0.0, r=3.6, h_wall=2.6, h_roof=2.8, a=ROT_CRIMSON, b='1a1414')
    P.campfire((sc[0] + 5, sc[1] - 3, height(sc[0] + 5, sc[1] - 3)), 1.0, PM, power=600, smoke=True)
    for k in range(5):
        a = k * math.tau / 5
        P.banner_pole((sc[0] + math.cos(a) * 6, sc[1] + math.sin(a) * 6, height(sc[0], sc[1])), 0.0, PM['crimson'],
                      'skull', h=6, width=1.2, drop=2.4)
    grid.add(*sc, 14)
    # burned waystation by the road
    AM = arch.mats('warm')
    AM['plaster'] = env.plaster('Scorched plaster', 'a89478', '3a2e24')
    AM['timber'] = P.S.wood('241a14', name='Charred beams', dark=0.4)
    village((22, -36), 7, height, rng, 8, M=AM, grid=grid, lit=0.0, avoid=lambda x, y: path(x, y) > 0.05,
            align=lambda x, y: _road_yaw(road, x, y))
    P.smoke_column((22, -36, height(22, -36)), height=30, radius=2.0, density=0.4, color='4a4440', drift=(10, 10))
    # standing stone circle
    for k in range(9):
        a = k * math.tau / 9
        x, y = 62 + math.cos(a) * 7, -44 + math.sin(a) * 6
        P.standing_stone((x, y, height(x, y)), 3.2, seed=k)
    grid.add(62, -44, 9)
    village((56, 14), 6, height, rng, 6, M=M, grid=grid, lit=0.3)
    avoid = lambda x, y: path(x, y) > 0.05 or shore(x, y) > 0.2 or fields.sample(x, y)[0] > 0.3
    MC.copses(7, height, rng, MC.everywhere(), size=(3, 7), kinds=('olive', 'broad'), grid=grid, avoid=avoid, seed=81)
    forest(40, MC.everywhere(), height, rng, ('olive', 'bush'), grid=grid, avoid=avoid, gap=9, seed=82)
    MC.scatter_rocks(40, height, rng, avoid=avoid, grid=grid, moss=0.0)
    MC.camera(1.0)
    return dict(bloom=0.12, vignette=0.22, saturation=1.08)


# ------------------------------------------------------------------ Gloamwood Verge
def gloamwood(scene):
    rng = random.Random(9)
    road = Polyline(smooth_path([(-95, -50), (-60, -36), (-30, -10), (-10, 4), (20, 0), (50, 16), (95, 24)]))
    circle = (34, -30)
    camp = (-36, 34)
    base = _hills(91, 3.5)
    height = flatten(base, road, 5.0)
    path = line_mask(road, 4.2)
    clear = lambda x, y: min(math.hypot(x - circle[0], y - circle[1]) - 14, math.hypot(x - camp[0], y - camp[1]) - 15,
                             math.hypot(x - 70, y - 10) - 12)
    mat = MC.map_ground('Gloam ground', grass=('34442a', '445632', '5e6a3c'), dry='6a6440', dry_amount=0.2,
                        dirt=('4a3e2e', '342a20'))
    MC.build_terrain(height, mat, masks={'path': path})
    _map_light(scene, az=232, el=42, strength=3.6, sun='f0d8b0', exposure=0.65, zenith='3a4a5a', horizon='9a9a88',
               mid='a8a894', upper='6a7a80')
    grid = Grid(4.0)
    PM = P.pm()
    M = arch.mats('grey')
    # witch circle
    for k in range(8):
        a = k * math.tau / 8
        x, y = circle[0] + math.cos(a) * 7, circle[1] + math.sin(a) * 6
        P.rune_stone((x, y, height(x, y)), a + math.pi / 2, h=3.4, seed=k, power=120, color='9cf25c')
    K.ward_sigil((circle[0], circle[1], height(*circle) + 0.05), 4.0, '9cf25c', power=300)
    grid.add(*circle, 11)
    # logging camp with lantern light
    for k in range(3):
        K.log_stack((camp[0] + rng.uniform(-6, 6), camp[1] + rng.uniform(-5, 5), height(*camp)), rng.uniform(0, 3),
                    L=5.0, r=0.4)
    for k in range(3):
        a = k * 2.1
        x, y = camp[0] + math.cos(a) * 7, camp[1] + math.sin(a) * 6
        P.ridge_tent((x, y, height(x, y)), a, L=3.4, Wd=2.6, h=2.0, M=PM, lit=120)
    P.campfire((camp[0], camp[1], height(*camp)), 1.0, PM, power=1200, smoke=True)
    for k in range(8):
        K.stump((camp[0] + rng.uniform(-10, 10), camp[1] + rng.uniform(-9, 9), height(*camp)))
    grid.add(*camp, 12)
    for t in (0.2, 0.45, 0.7, 0.9):
        x, y = road.pts[int(t * (len(road.pts) - 1))]
        P.lantern_post((x + 2.5, y, height(x + 2.5, y)), 0.0, power=80)
    village((70, 10), 6, height, rng, 7, style='grey', M=M, grid=grid, lit=0.9, avoid=lambda x, y: path(x, y) > 0.05)
    avoid = lambda x, y: path(x, y) > 0.05 or clear(x, y) < 0
    forest(560, MC.everywhere(), height, rng, ('broad', 'conifer'), grid=grid, avoid=avoid, gap=3.0, seed=91,
           palette=('2a3e22', '3e5a2e', '68803e'), scale=(1.2, 1.9))
    for k in range(30):
        x, y = rng.uniform(-90, 90), rng.uniform(-70, 80)
        if not avoid(x, y):
            K.mushrooms((x, y, height(x, y)), 8, seed=k, color='9ff5d8', glow=6.0, r=1.0)
    MC.camera(2.0)
    return dict(bloom=0.24, bloom_threshold=0.55, vignette=0.26, saturation=1.02)


# ------------------------------------------------------------------ Crownfall Citadel
def citadel(scene):
    rng = random.Random(10)
    c = (6, 14)
    road = Polyline(smooth_path([(-90, -74), (-50, -52), (-24, -30), (-10, -14)]))
    moat = Polyline([(c[0] + math.cos(a * math.tau / 48) * 36, c[1] + math.sin(a * math.tau / 48) * 30)
                     for a in range(49)])

    def h0(x, y):
        z = env.fbm(x, y, 0.02, 4, 101) * 3.0 + 2.0
        hill = 1 - env.smoothstep(18, 34, math.hypot(x - c[0], (y - c[1]) * 1.15))
        return z + hill * 7.0
    height = carve(flatten(h0, road, 5.0), moat, 6.0, 3.0)
    path = line_mask(road, 3.6)
    cobble = lambda x, y: 1 - env.smoothstep(26, 28, math.hypot(x - c[0], (y - c[1]) * 1.15))
    breach = lambda x, y: (1 - env.smoothstep(10, 16, math.hypot(x + 40, y + 34)))
    mat = MC.map_ground('Citadel ground', grass=('566048', '68704e', '7e7e5c'), dry='8a8268', dry_amount=0.4,
                        ash=0.35, ash_color='4a4846', cobble=('7e7a72', '666258'), mud=('4a4238', '3a342c'))
    MC.build_terrain(height, mat, masks={'path': path, 'cobble': cobble, 'mud': breach})
    MC.water(-0.4, name='Citadel moat', deep='243034', shallow='4a5a5a')
    _map_light(scene, az=228, el=38, strength=4.2, sun='ffdcc4', exposure=0.35, zenith='3e3a4a', horizon='c87a5a',
               mid='d8a080', upper='7a7082')
    grid = Grid(4.0)
    M = arch.mats('grim')
    PM = P.pm()
    walled_town(c, 25.0, height, rng, style='grim', M=M, n_houses=24, towers=12, banner=PM['crimson'], grid=grid,
                gate_angles=(225,), wall_h=7.0, keep_h=24)
    walled_town(c, 11.0, height, rng, style='grim', M=M, n_houses=0, towers=6, keep=False, banner=PM['crimson'],
                grid=grid, wall_h=8.0)
    bridge((-20, -12), (-12, -5), height(-12, -5) - 0.4, 5.0, M, style='grim')
    # breach yard: siege lines, wreckage and smoke outside the walls
    for k in range(4):
        x, y = -40 + rng.uniform(-10, 10), -34 + rng.uniform(-8, 8)
        P.catapult_wreck((x, y, height(x, y)), rng.uniform(0, 6), PM)
        grid.add(x, y, 4)
    for k in range(12):
        x, y = -40 + rng.uniform(-14, 14), -34 + rng.uniform(-12, 12)
        K.rubble((x, y, height(x, y)), 2.0, 8, seed=k)
    for (x, y) in ((-44, -30), (-10, 40), (30, -10)):
        P.smoke_column((x, y, height(x, y)), height=34, radius=2.0, density=0.4, color='3a3634', drift=(10, 12),
                       name=f'Smoke {x}')
    for k in range(6):
        x, y = -60 + k * 6, -52 + rng.uniform(-3, 3)
        P.pavilion((x, y, height(x, y)), 0.0, r=2.0, a=LANTERN_TEAL, b='e0d4b6', M=PM, door=False, count=12)
        grid.add(x, y, 3)
    P.banner_pole((-34, -46, height(-34, -46)), 0.0, PM['teal'], h=7, width=1.4, drop=2.6)
    avoid = lambda x, y: path(x, y) > 0.05 or math.hypot(x - c[0], (y - c[1]) * 1.15) < 40 or breach(x, y) > 0.2
    forest(140, MC.everywhere(), height, rng, ('dead',), grid=grid, avoid=avoid, gap=4.5, seed=101, scale=(1.1, 1.7))
    MC.scatter_rocks(40, height, rng, avoid=avoid, grid=grid, moss=0.05)
    MC.camera(4.0)
    return dict(bloom=0.18, vignette=0.24, saturation=1.0)


MAPS = {
    'city': city,
    'harbor': harbor,
    'foundry': foundry,
    'quarantine': quarantine,
    'thornwall': thornwall,
    'basilica': basilica,
    'mire': mire,
    'steppe': steppe,
    'gloamwood': gloamwood,
    'citadel': citadel,
}
