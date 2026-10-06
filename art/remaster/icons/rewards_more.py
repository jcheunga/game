"""Reward icons beyond gold: provisions, tomes, essence, sigils, shards, relic chest, season XP, unit, spell."""
import math
import random

import bpy

from rk import geo, weapons as W
from rk import shaders as S
from rk.heads import catmull
from . import itemkit as P
from .base import icon, V, xform, TEAL, AMBER


def sparkles(K, pts, color, size=0.12, hot='ffffff', strength=6.0):
    mat = K.spark_mat(color, hot=hot, strength=strength)
    return [P.star_flare(f'Sparkle {i}', p, size * s, mat, K.coll, facing=(0.37, -0.93, 0.3)) for i, (p, s) in
            enumerate(pts)]


@icon('rewards', 'food', bloom=0.3)
def food(K):
    c = K.coll
    crust = P.baked()
    loaf = geo.quadsphere('Loaf', 1.0, (0, 0, 0), crust, c, level=4)
    for v in loaf.data.vertices:
        x, y, z = v.co
        v.co = V(x * 0.5, y * 0.32, max(z, -0.35) * 0.27 + 0.08)
    loaf.data.update()
    geo.store_rest(loaf)
    geo.place(loaf, (-0.15, 0.22, -0.1), (0, 0, 18))
    meat = P.roast()
    bonem = K.bone('efe2c4')
    leg = geo.lathe('Roast leg', [(0, -0.42), (0.12, -0.4), (0.2, -0.3), (0.24, -0.15), (0.23, 0.0), (0.18, 0.12),
                                  (0.1, 0.2), (0.06, 0.26), (0.0, 0.27)], 24, meat, c)
    geo.displace_noise(leg, 0.012, 6, seed=2)
    bone = geo.lathe('Leg bone', [(0, 0.2), (0.045, 0.2), (0.04, 0.42), (0.055, 0.47), (0.0, 0.5)], 14, bonem, c)
    for sx in (-1, 1):
        geo.sphere(f'Bone knob {sx}', 0.055, (sx * 0.04, 0, 0.5), bonem, c, 14, 8)
    P.torus('Paper frill', 0.06, 0.02, P.parchment('f2ead6', name='Frill'), c, seg=20, sides=6, location=(0, 0, 0.3))
    xform([leg, bone] + [o for o in c.objects if o.name.startswith('Bone knob') or o.name.startswith('Paper')],
          loc=(0.12, -0.12, 0.02), rot=(0, 62, -28))
    apple = geo.quadsphere('Apple', 0.17, (0.48, 0.12, -0.12), P.fruit(), c, level=3, scale=(1, 1, 0.9))
    geo.sculpt(apple, (0.48, 0.12, 0.04), 0.08, (0, 0, -0.03))
    geo.tube('Apple stem', [V(0.48, 0.12, 0.02), V(0.49, 0.12, 0.1), V(0.52, 0.12, 0.14)], 0.01, K.wood('4a2e18'), c, sides=5)
    P.leaf('Apple leaf', V(0.5, 0.12, 0.11), V(1, -0.3, 0.4), 0.13, 0.06, P.enamel('3a7a2a', name='Leaf green'), c,
           normal=(0, -1, 0.3))
    wedge = geo.extrude('Cheese wedge', [(0, 0), (0.42, 0), (0.0, 0.22)], 0.2, P.enamel('f0c040', name='Cheese', rough=0.5),
                        c, plane='XY', bevel=0.015)
    geo.place(wedge, (-0.55, -0.32, -0.32), (0, 0, -20))
    for k in range(3):
        geo.sphere(f'Cheese hole {k}', 0.03, (-0.5 + k * 0.1, -0.36 - k * 0.03, -0.2 + (k % 2) * 0.05),
                   P.enamel('c8901a', name='Cheese hole'), c, 10, 6, scale=(1, .4, 1))
    return dict(el=24, az=18)


def book(K, name, size, cover, loc, rotz, emblem=None, clasp=True, ribbon=None):
    c = K.coll
    w, d, h = size
    t = 0.03
    brass = K.gilt('d0a048', 0.25)
    objs = [geo.box(f'{name} back board', (w, d, t), (0, 0, -h / 2 + t / 2), cover, c, bevel=0.012, segments=2),
            geo.box(f'{name} front board', (w, d, t), (0, 0, h / 2 - t / 2), cover, c, bevel=0.012, segments=2),
            geo.box(f'{name} pages', (w - 0.06, d - 0.05, h - 2 * t + 0.004), (0.02, 0, 0), P.page_block(), c, bevel=0.004)]
    spine = geo.cylinder(f'{name} spine', h / 2, d, (0, 0, 0), cover, c, 20, rotation=(90, 0, 0), bevel=0.008)
    for v in spine.data.vertices:
        if v.co.x > 0:
            v.co.x = 0
    spine.data.update()
    geo.store_rest(spine)
    geo.place(spine, (-w / 2 + 0.01, 0, 0))
    objs.append(spine)
    for sy in (-1, 1):
        cg = geo.extrude(f'{name} corner {sy}', [(0, 0), (-0.13, 0), (0, -0.13 * sy)] if sy > 0 else
                         [(0, 0), (0, 0.13), (-0.13, 0)], 0.014, brass, c, plane='XY', bevel=0.004)
        geo.place(cg, (w / 2 + 0.004, sy * (d / 2 + 0.004), h / 2 + 0.004))
        objs.append(cg)
    objs.append(geo.box(f'{name} spine band', (0.04, d + 0.012, 0.03), (-w / 2 + 0.16, 0, h / 2 + 0.004), brass, c,
                        bevel=0.008))
    if clasp:
        objs.append(geo.box(f'{name} clasp strap', (0.14, 0.09, h + 0.02), (w / 2 + 0.01, 0, 0), K.leather('3a2416'), c,
                            bevel=0.01))
        objs.append(geo.box(f'{name} clasp plate', (0.05, 0.11, 0.07), (w / 2 + 0.07, 0, 0.0), brass, c, bevel=0.01))
    if ribbon is not None:
        objs.append(P.ribbon(f'{name} ribbon', [V(w / 2 - 0.15 + 0.01 * k, -d / 2 - 0.01 - 0.012 * k, -0.01 - 0.035 * k)
                                               for k in range(6)], 0.05, ribbon, c, up=(1, 0, 0)))
    if emblem is not None:
        objs += emblem
    xform(objs, rot=(0, 0, rotz), loc=loc)
    return objs


@icon('rewards', 'tomes', bloom=0.45)
def tomes(K):
    c = K.coll
    teal = K.leather('1d6a64')
    red = K.leather('7a1e22')
    brown = K.leather('5a3a22')
    book(K, 'Base tome', (1.0, 0.72, 0.2), brown, (0.0, 0.08, -0.32), 6)
    book(K, 'Mid tome', (0.9, 0.66, 0.17), red, (-0.04, 0.02, -0.13), -9, clasp=False, ribbon=K.cloth('c8a040'))
    glow = K.glow(AMBER, 4.0, core='fff0c8')
    gold = K.gilt('e0b050', 0.22)
    em = [P.rune('Tome rune', 'othala', V(0.02, 0, 0.083), V(1, 0, 0), V(0, 1, 0), 0.13, 0.018, glow, c,
                 depth_axis=V(0, 0, 1)),
          P.torus('Tome seal ring', 0.2, 0.018, gold, c, seg=48, sides=6, location=(0.02, 0, 0.08)),
          P.torus('Tome seal ring 2', 0.25, 0.01, gold, c, seg=48, sides=6, location=(0.02, 0, 0.08))]
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        em.append(geo.sphere(f'Cover stud {k}', 0.025, (0.02 + math.cos(a) * 0.3, math.sin(a) * 0.22, 0.085), gold, c, 10, 6))
    book(K, 'Top tome', (0.84, 0.62, 0.16), teal, (0.03, -0.03, 0.05), 16, emblem=em)
    P.halo_sphere('Rune glow', V(0.05, -0.06, 0.16), 0.22, K.halo('ffb04a', strength=1.4, opacity=.4, power=1.8), c,
                  scale=(1, 1, 0.4))
    P.motes('Knowledge motes', V(0.05, -0.05, 0.45), 12, 0.45, 0.016, K.glow('ffe0a0', 5.0), c, seed=2, flatten=(1, 1, 0.6))
    sparkles(K, [(V(0.45, -0.3, 0.35), 0.7)], 'fff0c8')
    return dict(el=34, az=22)


@icon('rewards', 'essence', bloom=0.6)
def essence(K):
    c = K.coll
    glass = S.glass('e8f4ff', name='Flask glass', rough=0.02)
    brass = K.gilt('d0a048', 0.24)
    prof = [(0, -0.62), (0.3, -0.6), (0.45, -0.45), (0.5, -0.2), (0.45, 0.05), (0.3, 0.2), (0.13, 0.28), (0.1, 0.36),
            (0.1, 0.55), (0.13, 0.58)]
    geo.lathe('Flask', prof, 40, glass, c, close_top=False)
    inner = [(r * 0.9, z) for r, z in prof[:6]]
    inner[-1] = (inner[-1][0] * 0.85, inner[-1][1] - 0.05)
    liq = P.crystal('9a3aff', deep='2a0458', glow=1.2, name='Essence', transmission=0.3, core='e090ff', inner=1.0, coat=0.2)
    geo.lathe('Essence liquid', inner, 40, liq, c)
    swirl = K.energy('7ff0e0', strength=3.0, hot='eafffa', opacity=.95, soft=0.0, fade_in=0.15, fade_out=0.75)
    pts = P.spiral_points(V(0, 0, -0.5), 0.3, 0.12, 0.0, 0.6, 1.6, steps=60)
    P.ribbon('Essence swirl', pts, [0.06 * math.sin(math.pi * i / 60) + 0.004 for i in range(61)], swirl, c, up=(0, 0, 1))
    P.torus('Neck collar', 0.115, 0.025, brass, c, seg=32, sides=8, location=(0, 0, 0.3))
    P.torus('Lip', 0.13, 0.02, brass, c, seg=32, sides=8, location=(0, 0, 0.58))
    geo.lathe('Cork', [(0, 0.5), (0.095, 0.5), (0.11, 0.66), (0.12, 0.7), (0, 0.72)], 20, K.wood('a0703a'), c)
    geo.quadsphere('Wax seal', 0.13, (0, 0, 0.72), P.wax('5a1a8a', name='Violet wax'), c, level=2, scale=(1, 1, 0.45))
    for k in range(3):
        a = k * math.tau / 3 + 0.4
        geo.tube(f'Cage strap {k}', catmull([V(math.cos(a) * 0.12, math.sin(a) * 0.12, 0.3),
                                             V(math.cos(a) * 0.36, math.sin(a) * 0.36, 0.12),
                                             V(math.cos(a) * 0.52, math.sin(a) * 0.52, -0.2),
                                             V(math.cos(a) * 0.32, math.sin(a) * 0.32, -0.6)], 5), 0.018, brass, c, sides=6)
    P.torus('Cage belt', 0.505, 0.022, brass, c, seg=48, sides=8, location=(0, 0, -0.2))
    P.halo_sphere('Essence glow', V(0, 0, -0.2), 0.62, K.halo('9a3aff', strength=1.0, opacity=.25, power=2.4), c)
    P.motes('Essence motes', V(0, 0, 0.3), 14, 0.7, 0.016, K.glow('e8c0ff', 5.0), c, seed=3)
    sparkles(K, [(V(-0.22, -0.45, 0.0), 0.7), (V(0.45, -0.3, 0.55), 0.5)], 'f0d8ff')
    return dict(el=14)


@icon('rewards', 'sigils', bloom=0.45)
def sigils(K):
    c = K.coll
    gold = K.gilt('e6b44c', 0.2)
    teal = P.enamel(TEAL, name='Sigil enamel')
    rib1 = P.velvet('a01828', name='Ribbon red', sheen=0.4)
    rib2 = P.velvet(TEAL, name='Ribbon teal', sheen=0.4)
    P.star_solid('Sigil star', 0.5, 0.3, 0.08, gold, c, points=8, location=(0, 0, -0.05))
    geo.cylinder('Sigil disc', 0.3, 0.1, (0, -0.02, -0.05), gold, c, 48, rotation=(90, 0, 0), bevel=0.02)
    geo.cylinder('Sigil field', 0.24, 0.04, (0, -0.075, -0.05), teal, c, 48, rotation=(90, 0, 0), bevel=0.008)
    P.torus('Sigil ring', 0.245, 0.018, gold, c, seg=48, sides=8, location=(0, -0.09, -0.05), rotation=(90, 0, 0))
    P.rune('Promotion rune', 'tiwaz', V(0, -0.1, -0.05), V(1, 0, 0), V(0, 0, 1), 0.15, 0.024,
           K.glow('ffd27a', 5.0, core='fff6e0'), c)
    geo.box('Ribbon bar', (0.5, 0.08, 0.16), (0, 0.04, 0.56), rib2, c, bevel=0.02)
    geo.box('Bar trim', (0.54, 0.06, 0.04), (0, 0.0, 0.48), gold, c, bevel=0.01)
    for sx in (-1, 1):
        pts = [V(sx * (0.12 + 0.05 * t), 0.05, 0.5 - 0.36 * t) for t in [k / 6 for k in range(7)]]
        P.ribbon(f'Ribbon tail {sx}', pts, 0.16, rib1 if sx < 0 else rib2, c, up=(0, -1, 0))
    geo.box('Ribbon knot', (0.12, 0.08, 0.12), (0, 0.02, 0.43), rib1, c, bevel=0.03)
    xform(list(c.objects), rot=(4, 0, -10))
    sparkles(K, [(V(0.32, -0.25, 0.15), 0.9)], 'fff0c8')
    return dict(el=8)


@icon('rewards', 'shards', bloom=0.6)
def shards(K):
    c = K.coll
    cr = P.crystal('a84aff', deep='1e0448', glow=1.0, name='Relic shard', inner=1.2)
    cr2 = P.crystal('3fe0d0', deep='063a36', glow=0.9, name='Relic shard teal', inner=1.2)
    rnd = random.Random(5)
    for i, (b, d, L, r, m) in enumerate([(V(0, 0, -0.5), V(0.05, -0.1, 1), 1.15, 0.2, cr),
                                         (V(-0.12, 0.05, -0.5), V(-0.55, 0.1, 1), 0.8, 0.16, cr),
                                         (V(0.12, 0.0, -0.5), V(0.6, -0.05, 1), 0.72, 0.15, cr2),
                                         (V(-0.05, -0.12, -0.5), V(-0.2, -0.6, 1), 0.5, 0.12, cr2),
                                         (V(0.08, 0.12, -0.5), V(0.25, 0.5, 1), 0.6, 0.12, cr)]):
        P.prism(f'Shard {i}', b, d, L, r, m, c, sides=5, tip=0.5, taper=0.8, seed=i, jitter=0.25, twist=0.4)
    for i in range(6):
        p = V(rnd.uniform(-0.6, 0.6), rnd.uniform(-0.4, 0.0), rnd.uniform(-0.6, -0.45))
        P.prism(f'Chip {i}', p, V(rnd.gauss(0, 1), rnd.gauss(0, 1), 1), 0.14, 0.05, cr if i % 2 else cr2, c, sides=4,
                tip=0.5, seed=10 + i)
    P.halo_sphere('Shard glow', V(0, 0, -0.1), 0.6, K.halo('9a4aff', strength=1.0, opacity=.25, power=2.4), c)
    P.motes('Shard dust', V(0, 0, 0.1), 16, 0.7, 0.016, K.glow('e8d0ff', 5.0), c, seed=4)
    sparkles(K, [(V(0.05, -0.3, 0.62), 1.0), (V(-0.45, -0.3, 0.15), 0.5)], 'f0e0ff')
    return dict(el=12)


@icon('rewards', 'relic', bloom=0.6, threshold=0.6)
def relic(K):
    c = K.coll
    wood = K.wood('5a3218', axis='X')
    brass = K.gilt('d4a448', 0.24)
    W_, D, Hh = 1.0, 0.62, 0.42
    geo.box('Chest body', (W_, D, Hh), (0, 0, -0.2), wood, c, bevel=0.025)
    for x in (-0.38, 0.0, 0.38):
        geo.box(f'Body band {x}', (0.07, D + 0.03, Hh + 0.03), (x, 0, -0.2), brass, c, bevel=0.01)
    geo.box('Body rim', (W_ + 0.03, D + 0.03, 0.06), (0, 0, 0.0), brass, c, bevel=0.012)
    lid = geo.cylinder('Lid', D / 2, W_, (0, 0, 0), wood, c, 24, rotation=(0, 90, 0), bevel=0.02)
    for v in lid.data.vertices:
        if v.co.z < 0:
            v.co.z = 0
    lid.data.update()
    geo.store_rest(lid)
    lid_objs = [lid]
    for x in (-0.38, 0.0, 0.38):
        arc = [V(x, -math.cos(a) * (D / 2 + 0.012), math.sin(a) * (D / 2 + 0.012)) for a in [k * math.pi / 16 for k in range(17)]]
        lid_objs.append(geo.tube(f'Lid band {x}', arc, 0.03, brass, c, sides=6, flatten=0.5))
    xform(lid_objs, loc=(0, 0, 0))
    open_ang = 58
    xform(lid_objs, loc=(0, -D / 2, 0))
    xform(lid_objs, rot=(-open_ang, 0, 0))
    xform(lid_objs, loc=(0, D / 2, 0.03))
    for o in lid_objs:
        for v in o.data.vertices:
            pass
    geo.box('Lock plate', (0.18, 0.04, 0.2), (0, -D / 2 - 0.02, -0.06), brass, c, bevel=0.015)
    P.gem_cut('Lock gem', 0.05, K.gem('2fd1c0', 1.6), c, location=(0, -D / 2 - 0.05, -0.05), rotation=(90, 0, 0), facets=8)
    for sx in (-1, 1):
        for sy in (-1, 1):
            geo.box(f'Corner {sx}{sy}', (0.1, 0.1, Hh + 0.04), (sx * (W_ / 2 - 0.03), sy * (D / 2 - 0.03), -0.2), brass,
                    c, bevel=0.012)
    geo.box('Treasure light', (W_ - 0.1, D - 0.1, 0.02), (0, 0, 0.02), K.glow('ffd27a', 5.0, core='fffaf0'), c, bevel=0)
    rays = K.energy('ffd060', strength=1.4, hot='fff6d8', opacity=.4, soft=0.0, fade_in=0.05, fade_out=0.45)
    for k in range(4):
        x = -0.3 + k * 0.2
        d = V(x * 0.5, -0.25, 1).normalized()
        P.ribbon(f'Light ray {k}', [V(x, -0.05, 0.03) + d * 0.07 * t for t in range(11)], [0.07 + 0.01 * t for t in range(11)],
                 rays, c, up=(0, -1, 0))
    P.halo_sphere('Chest glow', V(0, 0.0, 0.12), 0.42, K.halo('ffc84a', strength=1.4, opacity=.4, power=1.8), c,
                  scale=(1.3, 0.8, 0.5))
    for i in range(7):
        geo.cylinder(f'Hoard coin {i}', 0.07, 0.02, (-0.36 + i * 0.12, -0.05 + 0.08 * (i % 2), 0.06 + 0.03 * (i % 3)),
                     K.gilt('eab64a', 0.24), c, 20, rotation=(10 * (i % 3), 15 * (i % 2), 0), bevel=0.005)
    for i, (p, s) in enumerate([(V(-0.3, -0.05, 0.1), 0.09), (V(0.25, 0.0, 0.08), 0.08)]):
        geo.cylinder(f'Spill coin {i}', s, 0.025, p, K.gilt('eab64a', 0.24), c, 24, rotation=(70, 10 * i, 20), bevel=0.006)
    sparkles(K, [(V(0.2, -0.3, 0.62), 1.0), (V(-0.45, -0.3, 0.45), 0.6)], 'fff0c8')
    return dict(el=22, az=22)


@icon('rewards', 'season_xp', bloom=0.55)
def season_xp(K):
    c = K.coll
    gold = K.gilt('ecb84a', 0.18)
    P.star_solid('Season star', 0.62, 0.27, 0.16, gold, c, points=5)
    P.star_solid('Star inlay', 0.44, 0.19, 0.14, P.enamel(TEAL, name='Star enamel'), c, points=5,
                 location=(0, -0.045, 0))
    P.gem_cut('Star gem', 0.12, P.crystal('7ff8e2', deep='0a4a40', glow=2.0, name='Season gem'), c,
              location=(0, -0.15, 0.02), rotation=(90, 0, 0), facets=10)
    P.torus('Gem bezel', 0.125, 0.02, gold, c, seg=32, sides=8, location=(0, -0.13, 0.02), rotation=(90, 0, 0))
    for sx in (-1, 1):
        for k in range(4):
            P.leaf(f'Laurel {sx}{k}', V(sx * (0.3 + 0.05 * k), 0.05, -0.38 + 0.1 * k),
                   V(sx * 0.6, 0, 1 - 0.1 * k), 0.22, 0.09, gold, c, normal=(0, -1, 0), bend=0.1)
    xform(list(c.objects), rot=(0, 0, 10))
    P.halo_sphere('Star glow', V(0, 0.2, 0), 0.75, K.halo('ffd27a', strength=1.2, opacity=.25, power=2.2), c)
    sparkles(K, [(V(0.0, -0.3, 0.7), 1.1), (V(0.6, -0.3, -0.35), 0.55), (V(-0.62, -0.3, 0.2), 0.45)], 'fff0c8', size=0.14)
    return dict(el=8)


@icon('rewards', 'unit', bloom=0.4)
def unit(K):
    """Lantern Caravan knight: sugarloaf great helm, gilt cross-keel, lantern sigil on the visor, teal horsehair plume."""
    c = K.coll
    steel = S.metal('8e979d', rough=0.34, name='Helm steel', edge=1.7, hammer=0.25, cavity=0.42)
    gold = K.gilt('dcae4a', 0.22)
    dark = K.L['dark']
    prof = [(0.0, 0.8), (0.1, 0.785), (0.22, 0.73), (0.33, 0.6), (0.39, 0.42), (0.41, 0.2), (0.41, -0.1), (0.4, -0.35),
            (0.41, -0.47), (0.43, -0.52)]
    helm = geo.lathe('Great helm', prof, 48, steel, c, close_top=True, close_bottom=False)
    P.solid_mod(helm, 0.03, offset=-1)
    P.subsurf_mod(helm, 1)
    geo.place(helm, scale=(1.0, 0.94, 1.0))
    RY = 0.94

    def surf(a_deg, z, out=0.0):
        """Point on the helm surface at azimuth a (0 = front, -Y) and height z."""
        r = None
        for (r0, z0), (r1, z1) in zip(prof, prof[1:]):
            if min(z0, z1) <= z <= max(z0, z1) and z0 != z1:
                t = (z - z0) / (z1 - z0)
                r = r0 + (r1 - r0) * t
                break
        r = (r or 0.41) + out
        a = math.radians(a_deg)
        return V(math.sin(a) * r, -math.cos(a) * r * RY, z)
    # gilt bands and cross-keel
    for z, w in ((0.24, 0.026), (-0.46, 0.03)):
        pts = [surf(a, z, 0.006) for a in range(-180, 181, 6)]
        geo.tube(f'Helm band {z}', pts, w, gold, c, sides=8)
    keel = [surf(0, z, 0.012) for z in [0.76 - k * 0.06 for k in range(22)]]
    geo.tube('Keel', keel, 0.03, gold, c, sides=8, flatten=0.6)
    # eye slits either side of the keel, with a gilt sight band above
    for sx in (-1, 1):
        slit = [surf(sx * a, 0.1, 0.004) for a in range(8, 62, 3)]
        geo.tube(f'Eye slit {sx}', slit, 0.026, dark, c, sides=8, flatten=0.45)
        brow = [surf(sx * a, 0.165, 0.01) for a in range(6, 64, 3)]
        geo.tube(f'Sight band {sx}', brow, 0.016, gold, c, sides=6)
    # breaths on the right cheek
    for i in range(4):
        for j in range(2):
            p = surf(28 + j * 12, -0.12 - i * 0.075, 0.002)
            geo.sphere(f'Breath {i}{j}', 0.016, p, dark, c, 8, 6, scale=(1, 0.5, 1))
    # lantern sigil on the lower visor
    em = W.emblem_lantern(gold, K.glow(AMBER, 5.0, core='fff0c8'))
    objs = em(V(0, 0, 0), c, 1.05)
    xform(objs, rot=(0, 0, -90))
    xform(objs, loc=surf(-16, -0.2, 0.03))
    P.halo_sphere('Sigil glow', surf(-16, -0.22, 0.05), 0.09, K.halo(AMBER, strength=1.2, opacity=.4, power=1.8), c)
    for z in (0.62, 0.44):
        for a in (-40, 40):
            geo.sphere(f'Rivet {z}{a}', 0.018, surf(a, z, 0.0), gold, c, 10, 6)
    # plume socket + teal horsehair
    geo.lathe('Plume socket', [(0.0, 0.76), (0.07, 0.76), (0.055, 0.86), (0.075, 0.9), (0.0, 0.92)], 20, gold, c)
    hair_a = S.fur('1f8a80', name='Plume horsehair', tip='7ff0e0', scale=1.6)
    hair_b = S.fur('136a62', name='Plume horsehair dark', tip='3fc8b8', scale=1.6)
    rnd = random.Random(6)
    strands_a, strands_b = [], []
    path = [V(0, 0, 0.9), V(0.02, 0.02, 1.16), V(0.28, 0.14, 1.24), V(0.55, 0.24, 1.0), V(0.68, 0.3, 0.6),
            V(0.74, 0.34, 0.15)]
    spine = catmull(path, 6)
    ns = len(spine)
    core = geo.tube('Plume core', spine, [0.14 * math.sin(math.pi * min(1.0, 0.15 + k / (ns - 1))) ** 0.6 *
                                          (1 - 0.55 * (k / (ns - 1))) + 0.01 for k in range(ns)], hair_a, c, sides=14)
    strands_a.append(core)
    frames = geo._frames(spine)
    for k in range(170):
        a = rnd.uniform(0, math.tau)
        rr = rnd.random() ** 0.5
        L = rnd.uniform(0.75, 1.0)
        jit = rnd.uniform(-0.03, 0.03)
        pts = []
        for q, (p, t_, n_, b_) in enumerate(frames):
            t = q / (ns - 1)
            if t > L:
                break
            fan = (0.05 + 0.3 * t ** 0.8) * rr
            wav = 0.025 * math.sin(t * 9 + k)
            pts.append(p + n_ * (math.cos(a) * fan + wav) + b_ * (math.sin(a) * fan * 1.4 + jit * t))
        if len(pts) < 4:
            continue
        o = geo.tube(f'Strand {k}', pts, geo.taper(len(pts), rnd.uniform(0.012, 0.02), 0.003, 1.2),
                     hair_a if k % 3 else hair_b, c, sides=5)
        (strands_a if k % 3 else strands_b).append(o)
    geo.join(strands_a, 'Plume')
    geo.join(strands_b, 'Plume dark')
    xform(list(c.objects), rot=(0, 0, 18))
    sparkles(K, [(V(-0.28, -0.42, 0.55), 0.6)], 'f0f8ff')
    return dict(el=8, az=22)


@icon('rewards', 'spell', bloom=0.55)
def spell(K):
    c = K.coll
    parch = P.parchment('ead8a8', name='Scroll parchment')
    wood = K.wood('5a3a22')
    brass = K.gilt('d0a048', 0.24)

    def fn(u, v):
        x = (u - 0.5) * 1.2
        y = -0.06 * math.sin(math.pi * u) + 0.02 * math.sin(v * 6)
        z = (v - 0.5) * 0.85 - 0.04 * math.sin(math.pi * u)
        return (x, y, z)
    P.sheet('Scroll sheet', fn, 24, 12, parch, c, thickness=0.01, subsurf=1)
    for sx in (-1, 1):
        x = sx * 0.62
        geo.cylinder(f'Roll {sx}', 0.09, 0.95, (x, 0.02, 0), parch, c, 24, bevel=0.01)
        geo.cylinder(f'Rod {sx}', 0.03, 1.12, (x, 0.02, 0), wood, c, 12, bevel=0.005)
        for sz in (-1, 1):
            geo.sphere(f'Rod knob {sx}{sz}', 0.05, (x, 0.02, sz * 0.58), brass, c, 14, 8)
    glow = K.glow('b48cff', 5.0, core='f4eaff')
    P.flat_ring('Spell circle', V(0, -0.075, 0.0), V(0, -1, 0), 0.3, 0.025, K.beam('b48cff', strength=3, soft=0), c)
    P.flat_ring('Spell circle 2', V(0, -0.075, 0.0), V(0, -1, 0), 0.22, 0.012, K.beam('d8c0ff', strength=3, soft=0), c)
    P.rune('Spell rune', 'star', V(0, -0.08, 0.0), V(1, 0, 0), V(0, 0, 1), 0.15, 0.018, glow, c)
    for i, g in enumerate(['kenaz', 'raido', 'sowilo', 'ingwaz', 'fehu', 'algiz']):
        a = i * math.tau / 6 + 0.3
        P.rune(f'Circle rune {i}', g, V(math.cos(a) * 0.38, -0.075, math.sin(a) * 0.36 * 0.95), V(1, 0, 0), V(0, 0, 1),
               0.035, 0.007, glow, c)
    geo.quadsphere('Seal', 0.09, (0.42, -0.06, -0.3), P.wax('8a1a2a'), c, level=2, scale=(1, 0.4, 1))
    P.halo_sphere('Spell glow', V(0, -0.15, 0.0), 0.4, K.halo('a070ff', strength=1.4, opacity=.4, power=1.8), c)
    P.motes('Spell motes', V(0, -0.25, 0.2), 14, 0.55, 0.016, K.glow('e2ccff', 5.0), c, seed=2)
    sparkles(K, [(V(0.25, -0.35, 0.42), 0.9), (V(-0.35, -0.3, -0.3), 0.5)], 'e8d8ff')
    xform(list(c.objects), rot=(0, 0, -12))
    return dict(el=10)
