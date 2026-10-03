"""Landmark scenes: arena (colosseum ring), tower (ruined spire), skill tree (runic great tree)."""
import math
import random

from mathutils import Vector

from rk import arch, env
from rk import dressing as P
from rk.palette import LANTERN_TEAL

from .common import golden, night, rect_area, rocks, trees, tufts


def arena(scene):
    height = lambda x, y: 0.0
    sand = env.ground('Arena sand', grass=('b89a6a', 'c4a676', 'd2b486'), dry='c8aa78', dirt=('a88a5c', '8e7450'),
                      flowers=0.0, dry_amount=0.0)
    env.terrain((140, 140), (140, 140), (0, 20), height, mat=sand,
                masks={'path': lambda x, y: 0.35 + 0.65 * env.smoothstep(4, 18, math.hypot(x, y - 22))})
    golden(scene, sun_az=200, sun_el=24, strength=4.6, haze_density=0.003, clouds=0.6, light_strength=0.6)
    AM = arch.mats('warm')
    M = P.pm()
    arch.colosseum((0, 22, 0), 21.0, 33.0, 13.0, bays=44, M=AM, gates=(90.0, 30.0, 150.0), arc=(-25.0, 205.0))
    # banners on the podium wall between gates
    for deg in range(-10, 200, 16):
        a = math.radians(deg)
        x, y = math.cos(a) * 20.7, 22 + math.sin(a) * 20.7
        if any(abs(deg - g) < 9 for g in (90, 30, 150)):
            continue
        cl = M['teal'] if (deg // 16) % 2 else M['crimson']
        P.hanging_banner((x, y, 3.4), 1.1, 2.6, cl, M, rot=a - math.pi / 2 + math.pi, tails=True)
    # velarium: an even ring of masts on the attic, striped sails sloping in toward the sand
    rng = random.Random(2)
    angles = list(range(34, 150, 13))
    tops, inners = [], []
    for deg in angles:
        a = math.radians(deg)
        base = Vector((math.cos(a) * 32.0, 22 + math.sin(a) * 32.0, 19.0))
        env.inst(env.proto('mast', lambda: _mast()), tuple(base), 0.0, 1.0, env.C['Props'])
        tops.append(base + Vector((0, 0, 7.6)))
        inners.append(Vector((math.cos(a) * 25.0, 22 + math.sin(a) * 25.0, 22.4)))
    for i in range(len(angles) - 1):
        stripes = env.stripes(f'Velarium {i % 2}', a=LANTERN_TEAL if i % 2 else 'b0803a', b='e2d6b8', count=1.0,
                              axis='x', width=0.55)
        _sail(tops[i], tops[i + 1], inners[i + 1], inners[i], stripes)
    # crowd on the cavea, facing the sand
    pts = []
    for deg in range(-20, 200, 2):
        a = math.radians(deg + rng.uniform(-0.6, 0.6))
        for i in range(16):
            if rng.random() < 0.3:
                continue
            r = 21.0 + 1.2 + (33.0 - 21.0 - 3.0) * (i + 0.62) / 16
            z = 3.6 + (13.0 - 3.6) * (i + 1) / 16
            pts.append((math.cos(a) * r, 22 + math.sin(a) * r, z, a - math.pi / 2))
    P.crowd(pts, rng)
    # arena floor dressing: weapon racks, braziers, sand-raked lines, a central dueling ring
    for x in (-12, 12):
        P.weapon_rack((x, 12, 0), 0.0 if x < 0 else math.pi, M, seed=int(x), w=2.4)
        P.brazier((x * 0.75, 8, 0), M, power=160)
    for deg in (60, 120):
        a = math.radians(deg)
        P.banner_pole((math.cos(a) * 18, 22 + math.sin(a) * 18, 0), 0.0, M['teal'], h=6.0, width=1.0, drop=2.2)
    env.camera((0, -10, 7.5), (0, 26, 5.0), lens=26)
    return dict(bloom=0.16, vignette=0.34, saturation=1.1)


def _mast():
    from rk import geo
    m = P.pm()['wood_dark']
    return geo.cylinder('Velarium mast', 0.16, 8.0, (0, 0, 4.0), m, None, 8, bevel=0)


def _sail(a, b, c, d, mat):
    """Quad cloth awning a-b-c-d with a little sag."""
    from rk import geo
    a, b, c, d = Vector(a), Vector(b), Vector(c), Vector(d)

    def fn(u, v):
        top = a.lerp(b, u)
        bot = d.lerp(c, u)
        p = top.lerp(bot, v)
        p.z -= 0.45 * math.sin(math.pi * u) * math.sin(math.pi * v)
        return tuple(p)
    s = geo.grid_sheet('Velarium', 1, 1, 10, 6, fn, mat, env.C['Props'])
    mod = s.modifiers.new('Thick', 'SOLIDIFY')
    mod.thickness = 0.03
    # local coords for the stripe shader: u along the sail edge
    me = s.data
    attr = me.attributes.new('lc', 'FLOAT_VECTOR', 'POINT')
    vals = []
    for i, v in enumerate(me.vertices):
        vals += [(i % 11) / 10 * (b - a).length, (i // 11) / 6 * (d - a).length, 0.0]
    attr.data.foreach_set('vector', vals)
    return s


def tower(scene):
    def height(x, y):
        base = env.fbm(x, y, 0.02, 5, 3) * 10 + 0.004 * (x * x) + 18 * env.smoothstep(20, 70, y)
        d = math.hypot(x - 14, y - 52)
        plateau = 1 - env.smoothstep(12, 22, d)
        return base * (1 - plateau) + (18 * env.smoothstep(20, 70, 52) + 2) * plateau
    path = [(-4, -30), (-2, -5), (4, 15), (6, 30), (12, 42)]

    def h2(x, y):
        z = height(x, y)
        t = 1 - env.smoothstep(2.5, 5.0, env.poly_dist(x, y, path))
        return z * (1 - t * 0.6) + (z - 0.4) * t * 0.0
    env.terrain((260, 260), (240, 240), (0, 60), h2, masks={'path': env.road_mask(path, 3.6, 1.0)},
                mat=env.ground('Highland', grass=('4a5a2e', '667040', '8f8a50'), dry='a49060', rock='7a756c',
                               dry_amount=0.35))
    golden(scene, sun_az=300, sun_el=11, strength=5.0, haze_density=0.0035, clouds=0.95, light_strength=0.55,
           zenith='445a80', horizon='f2a86a', mid='f2c89a', upper='9eb0c8')
    env.far_ground(fog='c8b8a8', inner=140)
    env.haze((14, 52, 8), (90, 90, 14), 0.012, 'e8d0c0', 0.4, falloff=None, name='Spire mist')
    env.mountains(10, 420, (60, 120), arc=(-1.3, 1.3), base_z=-5, seed=21, direction=90, fog='aab0c0', fog_dist=600)
    AM = arch.mats('grey')
    pz = height(14, 52)
    arch.spire((14, 52, pz - 1.5), 5.2, 52, M=AM, seed=3, ruin=0.2, lit=0.6)
    # ruined outer walls and rubble around the spire
    for (p0, p1, hh) in (((2, 44), (8, 40), 4.0), ((20, 40), (26, 46), 3.0), ((26, 58), (22, 66), 5.0)):
        arch.wall(p0, p1, hh, 1.6, style='grey', base_z=pz - 1.0, M=AM)
    rocks(30, lambda r: (14 + r.uniform(-16, 16), 52 + r.uniform(-14, 14)), height, seed=7, size=(0.6, 2.0), moss=0.4)
    rocks(40, rect_area(-40, 40, -30, 40), h2, seed=8, size=(0.5, 2.5),
          avoid=lambda x, y: env.poly_dist(x, y, path) < 3, moss=0.4)
    trees(40, rect_area(-80, 80, -20, 120), h2, seed=5, kinds=('conifer',), min_gap=5,
          avoid=lambda x, y: env.poly_dist(x, y, path) < 5 or math.hypot(x - 14, y - 52) < 16)
    tufts(150, rect_area(-14, 10, -28, 10), h2, seed=3, avoid=lambda x, y: env.poly_dist(x, y, path) < 2.4)
    M = P.pm()
    for i, t in enumerate((0.2, 0.45, 0.7)):
        k = int(t * (len(path) - 1))
        x, y = path[k]
        P.lantern_post((x + 2.6, y, h2(x + 2.6, y)), math.pi, power=30)
    env.camera((-10, -34, h2(-10, -34) + 5.0), (8, 40, 24), lens=28)
    return dict(bloom=0.18, vignette=0.34, saturation=1.08)


def skill_tree(scene):
    def height(x, y):
        return env.fbm(x, y, 0.03, 4, 9) * 1.2 * env.smoothstep(14, 30, math.hypot(x, y - 22))
    shrine = lambda x, y: 1 - env.smoothstep(9.0, 10.0, math.hypot(x, y - 22))
    path = [(0, -30), (0, 12)]
    env.terrain((200, 200), (200, 200), (0, 30), height,
                masks={'cobble': lambda x, y: max(shrine(x, y), 1 - env.smoothstep(1.4, 2.2, env.poly_dist(x, y, path)))},
                mat=env.ground('Grove floor', grass=('2e4426', '486032', '6e7a3e'), dry='857a48', dry_amount=0.15,
                               flowers=0.8, flower_colors=('d8ecff', '9adcd0', 'b8a8e0')))
    night(scene, moon_az=120, moon_el=28, strength=0.8, haze_density=0.01, haze_color='8ab0b8', zenith='16244a',
          horizon='4a6a8a', glow='a0d0e0', clouds=0.4, light_strength=1.8)
    env.far_ground(fog='1c2838', inner=100)
    env.inst(env.proto('greattree', lambda: env.great_tree(3, 26.0)), (0, 26, -0.3), 0.4, 1.0, env.C['Vegetation'])
    M = P.pm()
    teal = '6ff0d2'
    rng = random.Random(5)
    for k in range(9):
        a = math.radians(-90 + (k - 4) * 26)
        r = 11.5
        x, y = math.cos(a + math.pi) * r * -1, 22 + math.sin(a) * r * -1
        x, y = math.cos(math.radians(-90 + (k - 4) * 26)) * r, 22 + math.sin(math.radians(-90 + (k - 4) * 26)) * r
        if y > 28:
            continue
        rot = math.atan2(22 - y, -x) - math.pi / 2 + math.pi
        P.rune_stone((x, y, height(x, y)), math.atan2(y - 22, x) + math.pi / 2, h=3.2 + 0.4 * (k % 2), seed=k,
                     power=60, color=teal)
    # shrine altar with a floating crystal at the foot of the tree
    altar = env.smooth_stone('8a8478', name='Altar stone', moss=0.3)
    arch.slab((0, 16.5, 0.35), (1, 0, 0), (0, 1, 0), 3.2, 2.0, 0.7, altar, [], 'Altar base', bevel=0.06)
    arch.slab((0, 16.5, 0.95), (1, 0, 0), (0, 1, 0), 2.2, 1.3, 0.5, altar, [], 'Altar top', bevel=0.05)
    P.gem_cluster((0, 16.5, 1.25), 9, '2fd1c0', seed=3, size=0.9, power=220)
    for x in (-2.4, 2.4):
        P.brazier((x, 14.8, 0), M, power=120, color='9ff5d8')
    # spirit-light: teal uplights into the crown and glowing orbs hanging among the leaves
    for k in range(6):
        a = k * math.tau / 6
        env.spot((math.cos(a) * 6, 26 + math.sin(a) * 6, 1.0), (math.cos(a) * 5, 26 + math.sin(a) * 5, 18), 2500, teal,
                 70, 0.8, 0.5, name='Crown uplight')
    orb = env.glow_mat('8ff8e0', 10.0, name='Spirit orb', core_color='f0fffa')
    for i in range(36):
        a = i * 2.39996  # golden-angle spiral
        r = 4 + (i % 9) * 1.4
        p = Vector((math.cos(a) * r, 26 + math.sin(a) * r * 0.9, 12.5 + (i % 5) * 1.3))
        from rk import geo as _g
        _g.sphere('Spirit orb', 0.18, tuple(p), orb, env.C['FX'], 10, 6)
        if i % 4 == 0:
            env.point(tuple(p), 60, teal, 0.2, name='Orb light')
    # fireflies
    ff = env.glow_mat('c8ffb0', 18.0, name='Firefly', core_color='ffffff')
    from rk import geo
    for i in range(70):
        p = Vector((rng.uniform(-16, 16), rng.uniform(4, 34), rng.uniform(0.6, 7.0)))
        geo.sphere('Firefly', rng.uniform(0.03, 0.06), tuple(p), ff, env.C['FX'], 6, 4)
    trees(40, rect_area(-70, 70, 20, 90), height, seed=8, kinds=('broad', 'conifer'), min_gap=5,
          avoid=lambda x, y: math.hypot(x, y - 26) < 22)
    trees(16, rect_area(-60, -20, -20, 20), height, seed=9, kinds=('broad', 'bush'), min_gap=4)
    trees(16, rect_area(20, 60, -20, 20), height, seed=10, kinds=('broad', 'bush'), min_gap=4)
    tufts(120, rect_area(-14, 14, -12, 10), height, seed=4, avoid=lambda x, y: abs(x) < 2.6,
          color=('3a5a34', '6a8a5a'))
    env.camera((0, -14, 6.5), (0, 22, 9.0), lens=24)
    return dict(bloom=0.3, bloom_threshold=0.5, vignette=0.4, saturation=1.1, shadows='122030')
