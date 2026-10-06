"""Meta / social icons."""
import math
import random

from rk import geo
from . import itemkit as P
from rk.heads import catmull
from .base import icon, V

from rk import weapons as W  # noqa: E402
from rk import shaders as S  # noqa: E402
from .base import xform, TEAL, AMBER, SOUL  # noqa: E402


def sparkles(K, pts, color, size=0.12, hot='ffffff', strength=6.0):
    mat = K.spark_mat(color, hot=hot, strength=strength)
    return [P.star_flare(f'Sparkle {i}', p, size * s, mat, K.coll, facing=(0.37, -0.93, 0.3)) for i, (p, s) in
            enumerate(pts)]


def heater(K, name, face, trim, emblem_fn=None, size=1.0, loc=(0, 0, 0), rot=(0, 0, 0)):
    c = K.coll
    before = set(c.objects)
    sh = W.heater_shield(dict(paint=face, trim=trim), c, size=size, curve=0.14, emblem=None, boss=False, rim=True,
                         face_key='paint')
    if emblem_fn is not None:
        emblem_fn(size)
    objs = [o for o in c.objects if o not in before]
    xform(objs, rot=(0, 0, -90))
    xform(objs, rot=rot, loc=loc)
    return objs


@icon('meta', 'arena_rating', bloom=0.5)
def arena_rating(K):
    c = K.coll
    gold = K.gilt('e8b44a', 0.2)
    for sx in (-1, 1):
        stem = [V(sx * (0.12 + 0.55 * math.sin(t * 1.35)), 0.02, -0.55 + 1.05 * (1 - math.cos(t * 1.35)) * 0.95)
                for t in [k / 16 for k in range(17)]]
        geo.tube(f'Laurel stem {sx}', stem, geo.taper(len(stem), 0.025, 0.01), gold, c, sides=6)
        for k in range(1, 15):
            p = stem[k]
            d = (stem[k + 1] - stem[k - 1]).normalized()
            out = V(sx, 0, 0) if k < 9 else V(sx * 0.4, 0, 1)
            for side in (-1, 1):
                ld = (d + (out if side > 0 else -out * 0.6) * 0.7).normalized()
                P.leaf(f'Laurel leaf {sx}{k}{side}', p, ld, 0.17 - 0.004 * k, 0.07, gold, c, normal=(0, -1, 0), bend=0.15,
                       fold=0.3)
    geo.cylinder('Medal', 0.32, 0.08, (0, 0, 0.0), gold, c, 48, rotation=(90, 0, 0), bevel=0.02)
    geo.cylinder('Medal field', 0.26, 0.04, (0, -0.035, 0.0), P.enamel('8a1e24', name='Arena enamel'), c, 48,
                 rotation=(90, 0, 0), bevel=0.008)
    P.torus('Medal ring', 0.265, 0.016, gold, c, seg=48, sides=8, location=(0, -0.05, 0), rotation=(90, 0, 0))
    P.star_solid('Rank star', 0.2, 0.085, 0.05, gold, c, points=5, location=(0, -0.06, 0.0))
    for sx in (-1, 1):
        sw = W.sword(dict(grip=K.leather('3a2418'), hilt=gold, blade=K.steel('d6dce0', 0.16)), c, length=0.75, width=0.08,
                     guard=0.26, grip=0.16, pommel='disc')
        xform(sw['objs'], loc=(0, 0.1, -0.5), rot=(0, sx * 38, 0))
    P.ribbon('Medal ribbon', [V(-0.3 + 0.6 * t, -0.04, -0.62 + 0.04 * math.sin(math.pi * t)) for t in [k / 10 for k in range(11)]],
             0.12, P.velvet('8a1e24', name='Ribbon red'), c, up=(0, -1, 0))
    sparkles(K, [(V(0.25, -0.3, 0.55), 0.9), (V(-0.5, -0.3, -0.2), 0.5)], 'fff0c8')
    return dict(el=8)


@icon('meta', 'tower_floor', bloom=0.45)
def tower_floor(K):
    c = K.coll
    stone = K.stone('9a907e', moss=0.2)
    stone2 = K.stone('847a6a', moss=0.1)
    roof = P.enamel('1c6e69', name='Tower roof', rough=0.35)
    glow = K.glow(AMBER, 6.5, core='fff4d8')
    geo.lathe('Tower', [(0, -0.75), (0.36, -0.75), (0.34, -0.6), (0.3, 0.45), (0.35, 0.5), (0.35, 0.6), (0, 0.6)], 32,
              stone, c)
    for z in (-0.35, 0.05, 0.38):
        P.torus(f'Course {z}', 0.3 - (z + 0.6) * 0.02, 0.02, stone2, c, seg=40, sides=6, location=(0, 0, z))
    for i in range(8):
        a = i * math.tau / 8
        geo.box(f'Merlon {i}', (0.13, 0.1, 0.13), (math.cos(a) * 0.3, math.sin(a) * 0.3, 0.66), stone2, c, bevel=0.015,
                rotation=(0, 0, math.degrees(a) + 90))
    geo.lathe('Roof', [(0.0, 0.62), (0.3, 0.64), (0.0, 1.2)], 32, roof, c)
    geo.cylinder('Flag pole', 0.012, 0.35, (0, 0, 1.33), K.wood('4a3020'), c, 8)
    P.ribbon('Pennant', [V(0.0 + 0.07 * t, 0, 1.46 - 0.006 * t * t) for t in range(6)], [0.12 - 0.02 * t for t in range(6)],
             K.cloth('c8a040', '7a5a26'), c, up=(0, -1, 0))
    for k, (z, a) in enumerate([(-0.4, -0.2), (-0.05, 0.4), (0.3, -0.1)]):
        d = V(math.sin(a), -math.cos(a), 0)
        win = geo.extrude(f'Window {k}', [(-0.05, 0), (0.05, 0), (0.05, 0.1), (0, 0.15), (-0.05, 0.1)], 0.02, glow, c,
                          plane='XZ', bevel=0.004)
        geo.place(win, (0, -0.29 - 0.005 * k, z), (0, 0, math.degrees(a)))
    door = geo.extrude('Door', [(-0.1, 0), (0.1, 0), (0.1, 0.18), (0, 0.26), (-0.1, 0.18)], 0.03, K.wood('4a2e1a'), c,
                       plane='XZ', bevel=0.006)
    geo.place(door, (0.05, -0.31, -0.75), (0, 0, 10))
    for i in range(7):
        a = i * math.tau / 7
        P.rock(f'Base rock {i}', V(math.cos(a) * 0.42, math.sin(a) * 0.3, -0.75), 0.1 + (i % 3) * 0.03, stone2, c, seed=i,
               rough=0.3, scale=(1.3, 1, 0.6))
    # floor counter steps spiralling up
    for i in range(6):
        a = -1.6 + i * 0.35
        geo.box(f'Stair {i}', (0.14, 0.12, 0.05), (math.cos(a) * 0.38, math.sin(a) * 0.38, -0.6 + i * 0.1), stone2, c,
                bevel=0.01, rotation=(0, 0, math.degrees(a)))
    # side turret broadens the silhouette into a keep
    tp = V(-0.4, 0.18, 0)
    geo.lathe('Side turret', [(0, -0.75), (0.19, -0.75), (0.18, -0.6), (0.16, 0.05), (0.2, 0.09), (0.2, 0.16), (0, 0.16)],
              24, stone2, c, location=(tp.x, tp.y, 0))
    for i in range(6):
        a = i * math.tau / 6
        geo.box(f'Turret merlon {i}', (0.08, 0.06, 0.08), (tp.x + math.cos(a) * 0.18, tp.y + math.sin(a) * 0.18, 0.2),
                stone, c, bevel=0.01, rotation=(0, 0, math.degrees(a) + 90))
    geo.lathe('Turret roof', [(0.0, 0.16), (0.2, 0.18), (0.0, 0.62)], 24, roof, c, location=(tp.x, tp.y, 0))
    geo.sphere('Turret finial', 0.03, (tp.x, tp.y, 0.64), K.gilt('dcae4a', 0.22), c, 10, 6)
    tw = geo.extrude('Turret window', [(-0.035, 0), (0.035, 0), (0.035, 0.08), (0, 0.11), (-0.035, 0.08)], 0.02, glow, c,
                     plane='XZ', bevel=0.003)
    geo.place(tw, (tp.x + 0.02, tp.y - 0.175, -0.22))
    P.ribbon('Tower banner', [V(0.08 + 0.0 * t, -0.335, 0.42 - 0.07 * t) for t in range(8)],
             [0.2 - 0.004 * t for t in range(8)], K.cloth('1c6e69', '0e3c39', sheen=0.8), c, up=(0, 1, 0))
    P.halo_sphere('Window warmth', V(0, -0.32, -0.05), 0.4, K.halo(AMBER, strength=1.0, opacity=.22, power=2.2), c,
                  scale=(0.7, 0.5, 1.4))
    sparkles(K, [(V(0.1, -0.2, 1.3), 0.7)], 'fff0c8')
    return dict(el=12)


@icon('meta', 'endless_wave', bloom=0.6)
def endless_wave(K):
    from rk import heads
    c = K.coll
    brass = K.gilt('c99a45', 0.26)
    wood = K.wood('3e2a1c')
    glass = S.glass('e8fffa', name='Hourglass glass', rough=0.02)
    sand = K.glow(SOUL, 3.0, core='eafffa')
    bulb = [(0.04, 0.0), (0.12, 0.05), (0.3, 0.2), (0.34, 0.36), (0.3, 0.5), (0.26, 0.56)]
    geo.lathe('Bulb top', [(r, z) for r, z in bulb], 32, glass, c, close_top=False, close_bottom=False)
    geo.lathe('Bulb bottom', [(r, -z) for r, z in bulb], 32, glass, c, close_top=False, close_bottom=False)
    geo.lathe('Sand top', [(0.0, 0.08), (0.12, 0.12), (0.24, 0.24), (0.25, 0.3), (0.0, 0.3)], 32, sand, c)
    geo.lathe('Sand pile', [(0.0, -0.32), (0.28, -0.46), (0.29, -0.52), (0.0, -0.52)], 32, sand, c)
    geo.cylinder('Sand stream', 0.012, 0.36, (0, 0, -0.12), sand, c, 8, bevel=0)
    for z in (0.6, -0.6):
        geo.cylinder(f'Plate {z}', 0.42, 0.08, (0, 0, z), wood, c, 6, bevel=0.015)
        P.torus(f'Plate trim {z}', 0.4, 0.018, brass, c, seg=6, sides=6, location=(0, 0, z + (0.045 if z > 0 else -0.045)))
    for i in range(3):
        a = i * math.tau / 3 + 0.5
        p = V(math.cos(a) * 0.36, math.sin(a) * 0.36, 0)
        geo.lathe(f'Post {i}', [(0.03, -0.56), (0.04, -0.4), (0.028, -0.2), (0.045, 0.0), (0.028, 0.2), (0.04, 0.4),
                                (0.03, 0.56)], 12, brass, c, location=(p.x, p.y, 0))
    sk = heads.skull_head(V(0, 0, 0), 0.15, dict(bone=K.bone('dccfae'), dark=K.L['dark'], glow=K.glow(SOUL, 6.0)), c)
    xform(sk, rot=(0, 0, -90))
    xform(sk, loc=(0, -0.02, 0.8))
    P.halo_sphere('Sand glow', V(0, -0.1, -0.35), 0.4, K.halo(SOUL, strength=1.4, opacity=.4, power=1.8), c)
    wave = K.wave(SOUL, strength=2.0, opacity=.6)
    P.flat_ring('Endless ring', V(0, 0, 0.0), V(0, -0.25, 1), 0.68, 0.05, wave, c, seg=96)
    P.motes('Soul sand motes', V(0, 0, 0), 14, 0.75, 0.014, K.glow('bfffee', 5.0), c, seed=2)
    xform(list(c.objects), rot=(0, 0, 14))
    return dict(el=10)


@icon('meta', 'daily_streak', bloom=0.6, threshold=0.6)
def daily_streak(K):
    c = K.coll
    iron = K.black_iron()
    brass = K.gilt('d0a048', 0.24)
    geo.lathe('Brazier bowl', [(0, -0.2), (0.2, -0.22), (0.4, -0.12), (0.5, 0.02), (0.52, 0.06), (0.46, 0.06), (0.38, -0.04),
                               (0.2, -0.1), (0, -0.1)], 40, iron, c)
    P.torus('Bowl rim', 0.5, 0.03, brass, c, seg=48, sides=8, location=(0, 0, 0.06))
    for i in range(3):
        a = i * math.tau / 3 + 0.3
        d = V(math.cos(a), math.sin(a), 0)
        pts = catmull([d * 0.25 + V(0, 0, -0.18), d * 0.42 + V(0, 0, -0.45), d * 0.36 + V(0, 0, -0.75),
                       d * 0.48 + V(0, 0, -0.85)], 5)
        geo.tube(f'Leg {i}', pts, geo.taper(len(pts), 0.04, 0.025), iron, c, sides=8)
        geo.sphere(f'Foot {i}', 0.045, pts[-1], brass, c, 12, 8)
    P.torus('Leg ring', 0.36, 0.02, brass, c, seg=36, sides=6, location=(0, 0, -0.5))
    coal = P.magma_rock('2a1a14', 'ff5a10', strength=6, crack=0.08, scale=6, hot='ffc060')
    for i in range(9):
        a = i * math.tau / 9
        P.rock(f'Coal {i}', V(math.cos(a) * 0.25, math.sin(a) * 0.25, 0.04), 0.1, coal, c, seed=i, rough=0.3)
    outer = K.fire(strength=2.0, hot='ffd36a', mid='ff8a1e', outer='e0420c', smoke='5a0a02')
    inner = K.fire(strength=3.6, hot='fff6d8', mid='ffc040', outer='ff7a18', smoke='c02a06')
    rnd = random.Random(3)
    for i in range(9):
        a = i * math.tau / 9
        P.flame(f'Flame {i}', V(math.cos(a) * 0.2, math.sin(a) * 0.2, 0.05), 0.75 + rnd.random() * 0.45, 0.17, outer, c,
                seed=i, curl=.5, wobble=.4, lean=(-math.cos(a) * 0.1, -math.sin(a) * 0.1))
    P.flame('Flame core', V(0, 0, 0.05), 1.2, 0.24, outer, c, seed=20, curl=.4, wobble=.3)
    for i in range(4):
        P.flame(f'Hot core {i}', V(0.04 * math.cos(i * 1.6), 0.04 * math.sin(i * 1.6), 0.05), 0.7, 0.13, inner, c,
                seed=30 + i, curl=.3)
    P.sparks('Embers', V(0, 0, 0.9), 18, 0.1, 0.6, 0.08, 0.012, K.spark_mat('ff9a3a'), c, seed=5, direction=(0, 0, 1),
             spread=0.6)
    return dict(el=14)


@icon('meta', 'guild', bloom=0.4)
def guild(K):
    c = K.coll
    teal = K.cloth(TEAL, '0e3c39', sheen=0.8)
    gold = K.gilt('dcae4a', 0.22)

    def fn(u, v):
        x = (u - 0.5) * 1.05
        z = 0.55 - v * 1.35
        if v > 0.78:
            k = (v - 0.78) / 0.22
            z += k * 1.35 * 0.22 * (1 - abs(u - 0.5) * 2) * 0.9
        y = 0.035 * math.sin(u * 7 + v * 3) * (0.3 + v) + 0.05 * math.sin(v * 4)
        return (x, y, z)
    P.sheet('Guild banner', fn, 20, 28, teal, c, thickness=0.02, subsurf=1)
    geo.tube('Banner trim', [V(*fn(u / 20, 0.0)) + V(0, -0.01, -0.03) for u in range(21)], 0.025, gold, c, sides=6)
    for sx in (0, 1):
        geo.tube(f'Side trim {sx}', [V(*fn(sx, v / 20)) for v in range(16)], 0.018, gold, c, sides=6)
    geo.cylinder('Crossbar', 0.03, 1.3, (0, 0.0, 0.6), K.wood('4a3020'), c, 12, rotation=(0, 90, 0))
    for sx in (-1, 1):
        geo.sphere(f'Bar finial {sx}', 0.055, (sx * 0.67, 0, 0.6), gold, c, 14, 8)
    chev = [V(*fn(u / 20, 0.62 - 0.22 * abs(u / 20 - 0.5) * 2)) + V(0, -0.025, 0) for u in range(21)]
    geo.tube('Chevron', chev, 0.03, gold, c, sides=6, flatten=0.4)
    geo.cylinder('Pole', 0.035, 1.9, (0, 0.06, -0.1), K.wood('4a3020'), c, 12)
    geo.lathe('Pole top', [(0, 0.85), (0.06, 0.85), (0.05, 0.95), (0.08, 1.0), (0.0, 1.18)], 16, gold, c)
    for s in (W.emblem_lantern(gold, K.glow(AMBER, 5.0)),):
        objs = s(V(0, 0, 0), c, 2.6)
        xform(objs, rot=(0, 0, -90))
        xform(objs, loc=(0, -0.06, 0.02))
    xform(list(c.objects), rot=(0, 0, -16))
    sparkles(K, [(V(0.4, -0.3, 0.6), 0.6)], 'fff0c8')
    return dict(el=8)


@icon('meta', 'friends', bloom=0.45)
def friends(K):
    c = K.coll
    gold = K.gilt('e2b04a', 0.2)
    silver = K.silver('dfe4ea', 0.32)
    wine = P.crystal('8a0a1e', deep='2a0006', glow=0.4, name='Wine', transmission=0.2, coat=0.6)
    prof = [(0, -0.55), (0.24, -0.55), (0.25, -0.52), (0.1, -0.45), (0.05, -0.3), (0.05, -0.12), (0.09, -0.06),
            (0.06, -0.02), (0.12, 0.0), (0.24, 0.1), (0.3, 0.3), (0.31, 0.42), (0.29, 0.42), (0.27, 0.32), (0.2, 0.14),
            (0.0, 0.08)]
    for sx, mat, gemc in ((-1, gold, '2fd1c0'), (1, silver, 'e0233a')):
        objs = [geo.lathe(f'Goblet {sx}', prof, 40, mat, c)]
        objs.append(geo.cylinder(f'Wine {sx}', 0.27, 0.02, (0, 0, 0.34), wine, c, 36, bevel=0))
        objs.append(P.torus(f'Goblet band {sx}', 0.27, 0.018, mat, c, seg=40, sides=6, location=(0, 0, 0.2)))
        objs.append(geo.sphere(f'Knop {sx}', 0.075, (0, 0, -0.2), mat, c, 16, 10))
        for k in range(3):
            a = k * math.tau / 3 - math.pi / 2
            objs.append(P.cabochon(f'Goblet gem {sx}{k}', 0.035, K.gem(gemc, 1.2), c,
                                   location=(math.cos(a) * 0.255, math.sin(a) * 0.255, 0.18),
                                   rotation=(90, 0, math.degrees(a) + 90)))
        xform(objs, rot=(0, -sx * 24, 0))
        xform(objs, loc=(sx * 0.33, 0, -0.02))
    splash = P.crystal('b0142a', deep='3a0008', glow=0.6, name='Splash', transmission=0.2)
    rnd = random.Random(2)
    for i in range(9):
        p = V(rnd.uniform(-0.12, 0.12), rnd.uniform(-0.1, 0.05), 0.5 + rnd.uniform(0, 0.3))
        geo.sphere(f'Droplet {i}', rnd.uniform(0.02, 0.04), p, splash, c, 12, 8, scale=(1, 1, 1.4))
    P.star_flare('Clink', V(0, -0.15, 0.42), 0.32, K.spark_mat('fff2c8', strength=7), c, facing=(0.37, -0.93, 0.3), rays=8,
                 thin=0.06)
    return dict(el=10)


@icon('meta', 'challenge', bloom=0.4)
def challenge(K):
    c = K.coll
    gold = K.gilt('dcae4a', 0.22)
    for sx in (-1, 1):
        sw = W.sword(dict(grip=K.leather('3a2418'), hilt=gold, blade=K.steel('d8dee2', 0.14)), c, length=1.25, width=0.13,
                     guard=0.4, grip=0.22, pommel='disc')
        xform(sw['objs'], loc=(0, 0.12, -0.72), rot=(0, sx * 40, 0))
    face = S.paint('8a1e24', name='Challenge enamel', chip=0.4)
    geo.cylinder('Round shield', 0.42, 0.06, (0, 0, -0.02), face, c, 48, rotation=(90, 0, 0), bevel=0.02)
    for v in c.objects['Round shield'].data.vertices:
        if v.co.y < 0:
            v.co.y -= 0.08 * (1 - (v.co.x ** 2 + (v.co.z + 0.02) ** 2) / 0.18)
    c.objects['Round shield'].data.update()
    geo.store_rest(c.objects['Round shield'])
    P.torus('Shield rim', 0.42, 0.035, gold, c, seg=64, sides=8, location=(0, 0, -0.02), rotation=(90, 0, 0))
    geo.lathe('Shield boss', [(0.14, 0), (0.12, 0.05), (0.07, 0.1), (0, 0.11)], 24, gold, c, rotation=(90, 0, 0),
              location=(0, -0.08, -0.02))
    for i in range(8):
        a = i * math.tau / 8
        geo.sphere(f'Rim stud {i}', 0.03, (math.cos(a) * 0.34, -0.06, math.sin(a) * 0.34 - 0.02), gold, c, 10, 6)
    P.rune('Duel mark', 'dagaz', V(0, -0.075, 0.2), V(1, 0, 0), V(0, 0, 1), 0.06, 0.012, gold, c)
    xform(list(c.objects), rot=(0, 0, 10))
    sparkles(K, [(V(0.45, -0.3, 0.62), 0.8)], 'f0f8ff')
    return dict(el=8)


@icon('meta', 'members', bloom=0.35)
def members(K):
    c = K.coll
    gold = K.gilt('dcae4a', 0.22)
    silver = K.silver('d0d6dc', 0.22)
    specs = [(-1, P.enamel('8a1e24', name='House red'), silver, 'tiwaz'),
             (1, P.enamel('2a3a8a', name='House blue'), silver, 'algiz'),
             (0, P.enamel(TEAL, name='House teal'), gold, 'othala')]
    for sx, face, trim, glyph in specs:
        def emb(size, trim=trim, glyph=glyph):
            P.rune(f'House mark {glyph}', glyph, V(0.05, 0, 0.0), V(0, 1, 0), V(0, 0, 1), 0.13 * size, 0.022 * size, trim,
                   c)
        z = 0.08 if sx == 0 else -0.05
        heater(K, f'Shield {sx}', face, trim, emb, size=1.05 if sx == 0 else 0.9,
               loc=(sx * 0.42, 0.15 * abs(sx) - 0.08 * (sx == 0), z), rot=(0, sx * 18, sx * -12))
    sparkles(K, [(V(0.0, -0.4, 0.6), 0.6)], 'fff0c8')
    return dict(el=10)
