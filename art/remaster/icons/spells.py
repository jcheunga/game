"""Spell icons: one dynamic, glowing effect per spell."""
import math
import random

import bpy

from mathutils import Vector

from rk import geo
from . import itemkit as P
from rk.heads import catmull
from .base import icon, V, xform


# ----------------------------------------------------------------------------- fireball
@icon('spells', 'spell_fireball', bloom=0.6, threshold=0.6)
def spell_fireball(K):
    c = K.coll
    core = V(0.3, 0, -0.32)
    trail = V(-0.78, 0.3, 0.6).normalized()
    P.rock('Meteor core', core, 0.34, P.magma_rock('231512', 'ff5a10', strength=7, crack=0.06, scale=3.4, hot='ffc060'),
           c, seed=4, rough=0.25)
    outer = K.fire(strength=2.2, hot='ffc850', mid='ff6a10', outer='d0280a', smoke='4a0602', opacity=.95, soft=0.45,
                   breakup=0.35)
    inner = K.fire(strength=4.2, hot='fff8e0', mid='ffcc50', outer='ff8a20', smoke='d0400a', soft=0.5, breakup=0.25)
    rnd = random.Random(11)
    # flame jacket peeling back from the trailing hemisphere
    for i in range(18):
        d = Vector((rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, 1))).normalized()
        if d.dot(trail) < -0.05:
            d = (d + trail * 1.4).normalized()
        base = core + d * 0.27
        L = 0.45 + 0.75 * max(0.0, d.dot(trail)) + rnd.random() * 0.25
        P.flame(f'Jacket flame {i}', base, L, 0.15 + rnd.random() * 0.07, outer, c, seed=i,
                direction=(trail * 1.3 + d * .5), curl=.5, wobble=.35)
    # long trail tongues
    for i in range(6):
        off = Vector((rnd.gauss(0, .09), rnd.gauss(0, .09), rnd.gauss(0, .09)))
        P.flame(f'Trail flame {i}', core + off + trail * 0.12, 1.45 + rnd.random() * .55, 0.3 - i * .02, outer, c,
                seed=20 + i, direction=trail + off * 1.5, curl=.55, wobble=.4, twist=2.2)
    for i in range(7):
        off = Vector((rnd.gauss(0, .07), rnd.gauss(0, .07), rnd.gauss(0, .07)))
        P.flame(f'Core flame {i}', core + off + trail * .12, 0.7 + rnd.random() * .5, 0.21, inner, c, seed=40 + i,
                direction=trail + off, curl=.4, wobble=.3)
    P.halo_sphere('Trail heat', core + trail * 0.55, 0.55, K.halo('ff5a14', strength=1.2, opacity=.3, power=2.2), c,
                  scale=(1, 1, 1))
    P.sparks('Embers', core + trail * .7, 22, 0.25, 1.1, 0.14, 0.013, K.spark_mat('ff8a2a', hot='ffe2a0', strength=4), c,
             seed=3, direction=trail, spread=0.8)
    return dict(az=8, el=12)


# ----------------------------------------------------------------------------- shared bits
def _sparkles(K, pts, color, size=0.12, hot='ffffff', strength=7.0, name='Sparkle'):
    mat = K.spark_mat(color, hot=hot, strength=strength)
    out = []
    for i, (p, s) in enumerate(pts):
        out.append(P.star_flare(f'{name} {i}', p, size * s, mat, K.coll, facing=(0.37, -0.93, 0.3), rotation=0.0))
    return out


def _icosphere_hemi(name, radius, subdiv, mat, coll, zmin=-0.02):
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    kill = [v for v in bm.verts if v.co.z < zmin * radius]
    bmesh.ops.delete(bm, geom=kill, context='VERTS')
    return geo._finish_mesh(name, bm, mat, coll, smooth=True)


# ----------------------------------------------------------------------------- heal
@icon('spells', 'spell_heal', bloom=0.7, threshold=0.6)
def spell_heal(K):
    c = K.coll
    g = K.gilt('e8b850', 0.2)
    prof = [(0, -0.8), (0.38, -0.8), (0.4, -0.76), (0.36, -0.71), (0.22, -0.64), (0.12, -0.54), (0.075, -0.42),
            (0.07, -0.3), (0.075, -0.16), (0.12, -0.09), (0.24, -0.04), (0.36, 0.05), (0.43, 0.17), (0.46, 0.3),
            (0.475, 0.33), (0.45, 0.335), (0.42, 0.24), (0.33, 0.1), (0.18, 0.02), (0, 0.0)]
    geo.lathe('Chalice', prof, 48, g, c)
    geo.sphere('Knop', 0.12, (0, 0, -0.33), g, c, 24, 14, scale=(1, 1, .8))
    for i in range(4):
        a = i * math.tau / 4 + 0.4
        n = V(math.cos(a), math.sin(a), 0)
        P.gem_cut(f'Knop gem {i}', 0.045, K.gem('2fd1c0', 1.6), c, location=n * 0.115 + V(0, 0, -0.33),
                  rotation=tuple(math.degrees(x) for x in V(0, 0, 1).rotation_difference(n).to_euler()), facets=8)
    P.torus('Bowl band', 0.395, 0.022, g, c, seg=48, sides=8, location=(0, 0, 0.12))
    P.torus('Lip', 0.465, 0.018, g, c, seg=48, sides=8, location=(0, 0, 0.33))
    for i in range(6):
        a = i * math.tau / 6
        n = V(math.cos(a), math.sin(a), 0.25).normalized()
        P.cabochon(f'Bowl gem {i}', 0.04, K.gem('3fe08a', 1.8), c, location=n * 0.4 + V(0, 0, 0.115),
                   rotation=tuple(math.degrees(x) for x in V(0, 0, 1).rotation_difference(n).to_euler()))
    geo.cylinder('Healing draught', 0.44, 0.02, (0, 0, 0.29), K.glow('4fe89a', 3.0, core='e0ffe8'), c, 48, bevel=0)
    # light welling up out of the cup
    up = K.energy('4ff0a0', strength=1.5, hot='c8ffe0', opacity=.75, soft=1.0, fade_in=0.05, fade_out=0.6)
    gold_up = K.energy('ffcc4a', strength=1.5, hot='fff2c0', opacity=.7, soft=1.0)
    rnd = random.Random(2)
    for i in range(7):
        a = i * math.tau / 7
        base = V(math.cos(a) * 0.24, math.sin(a) * 0.24, 0.3)
        P.flame(f'Light wisp {i}', base, 0.75 + rnd.random() * 0.45, 0.12, up if i % 2 else gold_up, c, seed=i,
                curl=.3, wobble=.5, twist=1.5, lean=(math.cos(a) * .15, math.sin(a) * .15))
    P.flame('Light core', V(0, 0, 0.3), 1.15, 0.2, K.energy('9cffc8', strength=2.2, hot='eafff2', opacity=.8, soft=1.2), c,
            seed=9, curl=.2, wobble=.25)
    rib = K.energy('3fe896', strength=2.4, hot='d0ffe4', opacity=.95, soft=0.0, fade_in=0.15, fade_out=0.7)
    for k in range(2):
        pts = P.spiral_points((0, 0, -0.55), 0.62, 0.42, 0.0, 1.65, 1.15, steps=60, phase=k * math.pi + 0.6)
        P.ribbon(f'Mending ribbon {k}', pts, [0.09 * math.sin(math.pi * i / 60) + 0.01 for i in range(61)], rib, c,
                 up=(0, 0, 1))
    P.halo_sphere('Grace halo', V(0, 0, 0.75), 0.6, K.halo('5ff0a8', strength=1.4, opacity=.3, power=1.8, core='d8ffe8'), c)
    P.motes('Life motes', V(0, 0, 0.75), 18, 0.8, 0.018, K.glow('c8ffd8', 6.0), c, seed=4, flatten=(1, 1, 1.4))
    _sparkles(K, [(V(-0.42, -0.3, 1.0), 1.0), (V(0.5, -0.2, 0.62), 0.7), (V(0.12, -0.4, 1.35), 0.55)], 'b8ffd8')
    return dict(el=14)


# ----------------------------------------------------------------------------- frost burst
@icon('spells', 'spell_frost_burst', bloom=0.6, threshold=0.62)
def spell_frost_burst(K):
    c = K.coll
    ice = P.ice('d8f6ff', deep='2a7cb4', glow='7bdff2', strength=1.6, name='Burst ice')
    ice2 = P.ice('eafcff', deep='3a9ad0', glow='a8ecff', strength=2.2, name='Burst ice bright')
    rnd = random.Random(6)
    golden = math.pi * (3 - math.sqrt(5))
    n = 26
    for i in range(n):
        z = 1 - (i / (n - 1)) * 2
        r = math.sqrt(1 - z * z)
        a = golden * i
        d = V(math.cos(a) * r, math.sin(a) * r, z)
        # favour the camera-facing hemisphere and flatten the burst a little
        if d.y > 0.55:
            continue
        d = V(d.x * 1.15, d.y, d.z * 0.9).normalized()
        L = rnd.uniform(0.55, 1.05) * (1.0 if abs(d.z) < .7 else 0.8)
        rad = rnd.uniform(0.07, 0.12)
        P.prism(f'Ice spike {i}', d * 0.12, d, L, rad, ice if i % 3 else ice2, c, sides=6, tip=0.42, taper=0.9,
                twist=rnd.uniform(-.3, .3), seed=i, jitter=.15)
    for i in range(7):
        d = V(rnd.gauss(0, 1), -abs(rnd.gauss(0, 1)) * .6, rnd.gauss(0, 1)).normalized()
        P.prism(f'Core shard {i}', V(0, 0, 0), d, 0.42, 0.13, ice2, c, sides=6, tip=0.5, seed=50 + i)
    P.halo_sphere('Cold core', V(0, -0.15, 0), 0.42, K.halo('9fe8ff', strength=4.0, opacity=.8, power=1.4, core='ffffff'), c)
    P.halo_sphere('Frost aura', V(0, 0.1, 0), 1.05, K.halo('5cc8ef', strength=1.5, opacity=.35, power=2.4), c)
    wave = K.wave('a8ecff', strength=2.6, opacity=.75)
    P.flat_ring('Frost wave', V(0, 0, -0.1), V(0, -0.25, 1), 1.15, 0.16, wave, c, seg=72)
    P.flat_ring('Frost wave inner', V(0, 0, -0.1), V(0, -0.25, 1), 0.78, 0.07, K.wave('e8fbff', strength=3, opacity=.6), c)
    for i in range(10):
        d = V(rnd.gauss(0, 1), -abs(rnd.gauss(0, .5)), rnd.gauss(0, 1)).normalized()
        P.prism(f'Ice chip {i}', d * rnd.uniform(1.0, 1.3), d, 0.12, 0.035, ice2, c, sides=5, tip=.5, seed=80 + i)
    _sparkles(K, [(V(-0.75, -0.5, 0.55), 1.0), (V(0.8, -0.4, -0.45), 0.8), (V(0.25, -0.6, 0.85), 0.6)], 'bff2ff')
    return dict(el=12)


# ----------------------------------------------------------------------------- lightning strike
@icon('spells', 'spell_lightning_strike', bloom=0.85, threshold=0.58)
def spell_lightning_strike(K):
    c = K.coll
    top = V(-0.5, 0.1, 1.35)
    hit = V(0.12, -0.05, -0.62)
    core = K.beam('ffe04a', strength=4.0, hot='fffbe0', soft=0.25)
    glow = K.beam('ffc800', strength=1.8, hot='ffe680', opacity=.45, soft=1.6)
    main = P.jag_path(top, hit, 13, 0.11, seed=5)
    P.fx_tube('Bolt core', main, geo.taper(len(main), 0.04, 0.026), core, c, sides=8)
    P.fx_tube('Bolt glow', main, geo.taper(len(main), 0.13, 0.09), glow, c, sides=10)
    rnd = random.Random(3)
    for k, (idx, dirv, L) in enumerate([(3, V(-0.7, 0, -0.4), .55), (5, V(0.8, 0, -0.3), .6), (7, V(-0.5, 0, -0.8), .45),
                                         (9, V(0.7, 0, -0.6), .35), (2, V(0.6, 0, 0.1), .4), (1, V(-0.9, 0, 0.2), .45),
                                         (1, V(0.9, 0, 0.35), .55)]):
        p = main[idx]
        br = P.jag_path(p, p + dirv.normalized() * L, 7, 0.18, seed=10 + k)
        P.fx_tube(f'Branch core {k}', br, geo.taper(len(br), 0.022, 0.004), core, c, sides=6)
        P.fx_tube(f'Branch glow {k}', br, geo.taper(len(br), 0.07, 0.02), glow, c, sides=8)
        if k < 3:
            q = br[len(br) // 2]
            b2 = P.jag_path(q, q + (dirv + V(0, 0, -0.6)).normalized() * L * .4, 4, .2, seed=30 + k)
            P.fx_tube(f'Twig {k}', b2, geo.taper(len(b2), 0.012, 0.003), core, c, sides=5)
    # impact
    P.halo_sphere('Impact flash', hit, 0.26, K.halo('ffe680', strength=3.0, opacity=.8, power=1.3, core='fffbe8'), c)
    P.halo_sphere('Impact glow', hit + V(0, 0.1, 0.1), 0.6, K.halo('ffc22a', strength=1.2, opacity=.25, power=2.2), c)
    P.star_flare('Impact star', hit + V(0, -0.2, 0.02), 0.55, K.spark_mat('ffe680', hot='ffffff', strength=8), c,
                 facing=(0.37, -0.93, 0.3), rotation=0.3, rays=8, thin=0.05)
    P.sparks('Impact sparks', hit, 26, 0.15, 0.75, 0.18, 0.014, K.spark_mat('ffd84a', strength=7), c, seed=2,
             direction=(0, -0.2, 1), spread=1.2)
    rock = K.stone('4e4a46')
    for i in range(6):
        a = i * math.tau / 6 + 0.3
        p = hit + V(math.cos(a) * rnd.uniform(.35, .6), math.sin(a) * .25 - .1, rnd.uniform(.0, .35))
        P.rock(f'Debris {i}', p, rnd.uniform(.07, .12), rock, c, seed=i, rough=.35)
    P.flat_ring('Shock ring', hit, V(0, -0.2, 1), 0.6, 0.08, K.wave('fff0a0', strength=3, opacity=.7), c)
    return dict(el=10, az=16)


# ----------------------------------------------------------------------------- barrier ward
@icon('spells', 'spell_barrier_ward', bloom=0.6)
def spell_barrier_ward(K):
    from rk import weapons as W
    c = K.coll
    lav = 'd9b8ff'
    dome = _icosphere_hemi('Ward lattice', 1.0, 2, K.glow('c7a4ff', 4.0, core='f6eaff'), c)
    m = dome.modifiers.new('Lattice', 'WIREFRAME')
    m.thickness = 0.022
    m.use_replace = True
    m.use_even_offset = False
    geo.place(dome, (0, 0, -0.55), scale=(1.05, 1.05, 1.15))
    shell = _icosphere_hemi('Ward shell', 1.0, 4, P.ghost('c9a6ff', strength=1.8, opacity=.55, name='Ward shell'), c)
    geo.place(shell, (0, 0, -0.55), scale=(1.04, 1.04, 1.14))
    P.flat_ring('Ward circle', V(0, 0, -0.55), V(0, 0, 1), 1.06, 0.07, K.beam(lav, strength=4, hot='ffffff', soft=0), c,
                seg=96)
    P.flat_ring('Ward circle inner', V(0, 0, -0.55), V(0, 0, 1), 0.86, 0.03, K.beam(lav, strength=3, soft=0), c, seg=96)
    for i in range(10):
        a = i * math.tau / 10
        o = V(math.cos(a) * 0.96, math.sin(a) * 0.96, -0.545)
        P.rune(f'Ward rune {i}', ['algiz', 'tiwaz', 'othala', 'ingwaz', 'dagaz'][i % 5], o,
               V(-math.sin(a), math.cos(a), 0), V(math.cos(a), math.sin(a), 0), 0.045, 0.008,
               K.glow(lav, 6.0, core='ffffff'), c)
    mats = dict(paint=P.enamel('4a2a8a', name='Ward enamel'), trim=K.gilt('e8c070'))
    sh = W.heater_shield(mats, c, size=1.45, curve=0.14, boss=False, rim=True, face_key='paint')
    sh['objs'].append(P.rune('Shield rune', 'algiz', V(0.06, 0, 0.0), V(0, 1, 0), V(0, 0, 1), 0.24, 0.028,
                             K.glow('f2e4ff', 5.0, core='ffffff'), c))
    xform(sh['objs'], loc=(0, -0.3, 0.05), rot=(0, 0, -90))
    P.halo_sphere('Shield aura', V(0, 0.0, 0.0), 0.55, K.halo('b48cff', strength=2.0, opacity=.5, power=1.6), c)
    _sparkles(K, [(V(-0.62, -0.75, 0.45), 0.9), (V(0.7, -0.65, -0.1), 0.6)], 'e6d4ff')
    return dict(el=16)


# ----------------------------------------------------------------------------- stone barricade
@icon('spells', 'spell_stone_barricade', bloom=0.45)
def spell_stone_barricade(K):
    c = K.coll
    rnd = random.Random(8)
    stone = K.stone('9a8c76', moss=0.15)
    stone2 = K.stone('857868', moss=0.1)
    dark = K.stone('4f4438', crack=0.3)
    R = 2.6

    def put(name, x, z, w, h, d, mat):
        a = x / R
        P.block(name, (w - 0.035, d, h - 0.035), mat, c, location=(R * math.sin(a), R - R * math.cos(a), z),
                rotation=(rnd.uniform(-2.5, 2.5), rnd.uniform(-3, 3), math.degrees(a) + rnd.uniform(-2, 2)),
                round_=0.1, chip=0.022, seed=rnd.randint(0, 999))
    rows = [(-0.5, 0.36, 0.0), (-0.14, 0.34, 0.22), (0.2, 0.34, 0.06)]
    for r, (z, h, off) in enumerate(rows):
        x = -1.0 + off
        k = 0
        while x < 0.98:
            w = rnd.uniform(0.38, 0.52)
            if x + w > 1.08:
                w = 1.08 - x
            if w > 0.14:
                put(f'Block {r}-{k}', x + w / 2, z + h / 2, w, h, 0.44, stone if (r + k) % 3 else stone2)
            x += w
            k += 1
    for i, x in enumerate((-0.8, -0.26, 0.28, 0.82)):
        put(f'Merlon {i}', x, 0.54 + 0.19, 0.38, 0.38, 0.46, stone2 if i % 2 else stone)
    # glowing ward rune on the keystone
    P.rune('Ward rune', 'othala', V(0.02, -0.245, 0.03), V(1, 0, 0), V(0, 0, 1), 0.1, 0.017,
           K.glow('ffb04a', 5.0, core='fff0c8'), c)
    P.halo_sphere('Rune glow', V(0.02, -0.32, 0.03), 0.2, K.halo('ff9a30', strength=1.6, opacity=.45, power=1.6), c)
    # ground heaving and cracking around the rising wall
    earth = K.stone('5a4a3a', moss=0.5)
    for i in range(9):
        x = -1.1 + i * 0.27 + rnd.uniform(-.05, .05)
        side = -1 if i % 3 else 1
        y = side * rnd.uniform(0.3, 0.42) + (R - R * math.cos(x / R))
        P.rock(f'Heaved earth {i}', V(x, y, -0.66), rnd.uniform(.15, .21), earth, c, seed=i * 3, rough=.22,
               scale=(1.5, 1.0, 0.55), rotation=(side * -rnd.uniform(18, 32), rnd.uniform(-15, 15), rnd.uniform(-20, 20)))
    for i in range(12):
        p = V(rnd.uniform(-1.05, 1.05), rnd.uniform(-0.6, -0.32), rnd.uniform(-0.55, 0.65))
        P.rock(f'Rubble {i}', p, rnd.uniform(.035, .07), stone2 if i % 2 else dark, c, seed=20 + i, rough=.35)
    P.sparks('Rising dust', V(0, -0.35, -0.6), 14, 0.05, 1.0, 0.25, 0.01, K.spark_mat('d8c0a0', hot='fff0d8', strength=1.5),
             c, seed=5, direction=(0, 0, 1), spread=0.25)
    return dict(el=14, az=26)


# ----------------------------------------------------------------------------- war cry
@icon('spells', 'spell_war_cry', bloom=0.6)
def spell_war_cry(K):
    from rk import weapons as W
    from .base import aim_rot, TEAL
    c = K.coll
    ivory = K.bone('efdcb0')
    g = K.gilt('e2b04a', 0.22)
    # rallying spears crossed behind the horn, teal pennants flying
    pennant = K.cloth(TEAL, '0e3c39', sheen=0.8)
    for sx in (-1, 1):
        sp = W.spear(dict(wood=K.wood('5a3a22'), hilt=g, steel=K.steel(), blade=K.steel('d8dee2', 0.16)), c, length=2.6,
                     head_len=0.36, head_w=0.13, grip_at=0.5)
        z1 = 1.3

        def fn(u, v, z1=z1):
            x = -u * 0.62
            zz = z1 - 0.42 - v * 0.28 * (1 - 0.45 * u)
            if u > 0.7:
                zz += (v - 0.5) * 0.28 * 0.4 * (u - 0.7) / 0.3
            y = 0.04 * math.sin(u * 6 + v) * u
            return (x, y, zz)
        pen = P.sheet(f'Pennant {sx}', fn, 12, 4, pennant, c, thickness=0.008, subsurf=1)
        if sx > 0:
            geo.place(pen, scale=(-1, 1, 1))
            for poly in pen.data.polygons:
                poly.flip()
        sp['objs'].append(pen)
        xform(sp['objs'], loc=(-0.3, 0.5, -0.05), rot=(0, sx * 40, 0))
    spine = catmull([V(-0.9, -0.05, -0.66), V(-0.58, -0.08, -0.66), V(-0.22, -0.1, -0.48), V(0.1, -0.12, -0.12),
                     V(0.28, -0.14, 0.26)], 6)
    n = len(spine)
    radii = [0.065 + 0.32 * (i / (n - 1)) ** 2.1 for i in range(n)]
    horn = geo.tube('War horn', spine, radii, ivory, c, sides=28, cap=False)
    P.solid_mod(horn, 0.02, offset=-1)
    P.subsurf_mod(horn, 1)
    bell = spine[-1]
    axis = (spine[-1] - spine[-2]).normalized()
    for t in (0.2, 0.47, 0.74):
        i = int(t * (n - 1))
        d = (spine[min(n - 1, i + 1)] - spine[max(0, i - 1)]).normalized()
        P.torus(f'Horn band {t}', radii[i] + 0.014, 0.034, g, c, seg=36, sides=8, location=spine[i], rotation=aim_rot(d))
    P.torus('Bell rim', radii[-1] + 0.008, 0.046, g, c, seg=56, sides=8, location=bell, rotation=aim_rot(axis))
    geo.cylinder('Bell throat', radii[-1] * 0.8, 0.02, bell - axis * 0.06, K.L['dark'], c, 40, rotation=aim_rot(axis),
                 bevel=0)
    mp = geo.lathe('Mouthpiece', [(0.0, -0.12), (0.06, -0.12), (0.055, -0.07), (0.035, 0.0), (0.05, 0.07)], 16, g, c)
    geo.place(mp, spine[0], aim_rot((spine[0] - spine[1]).normalized()))
    strap = [spine[int(.2 * (n - 1))] + V(0, 0, -0.05), V(-0.3, -0.12, -0.92), V(0.08, -0.14, -0.66),
             spine[int(.74 * (n - 1))] + V(0, 0, -0.12)]
    geo.tube('Strap', catmull(strap, 6), 0.026, K.leather('6a3a20'), c, sides=6, flatten=.4)
    # golden shock-wave rings blasting from the bell
    for k, (d, r, op) in enumerate([(0.18, 0.46, 1.0), (0.5, 0.66, .85), (0.84, 0.86, .6)]):
        P.flat_ring(f'Shout ring {k}', bell + axis * d, axis, r, 0.15 - k * 0.025,
                    K.wave('ffb42a', strength=2.4, opacity=op), c, seg=80)
        P.flat_ring(f'Shout ring hot {k}', bell + axis * d, axis, r, 0.045, K.wave('fff0b8', strength=3.2, opacity=op), c,
                    seg=80)
    P.sparks('Shout streaks', bell + axis * 0.55, 18, 0.25, 0.75, 0.3, 0.013, K.spark_mat('ffc860', hot='fff6d8'), c,
             seed=4, direction=axis, spread=0.45)
    P.halo_sphere('Bell blast', bell + axis * 0.12, 0.26, K.halo('ffb030', strength=1.2, opacity=.35, power=1.8,
                                                               core='ffe0a0'), c)
    return dict(el=12, az=18)


# ----------------------------------------------------------------------------- earthquake
@icon('spells', 'spell_earthquake', bloom=0.55)
def spell_earthquake(K):
    from mathutils import Matrix
    c = K.coll
    rnd = random.Random(12)
    top = K.stone('5a5a38', moss=0.85, crack=0.5)
    side = K.stone('4a3a2e', crack=0.6)
    spike = P.magma_rock('221c18', 'ff6a2a', strength=5.0, crack=0.06, scale=2.6, hot='ffc080', name='Spike rock')
    fis = [(0.0, -1.05), (0.07, -0.75), (-0.05, -0.45), (0.06, -0.15), (-0.06, 0.15), (0.05, 0.45), (-0.04, 0.75),
           (0.02, 1.05)]

    def rim(sign):
        pts = []
        for k in range(1, 12):
            a = -math.pi / 2 + math.pi * k / 12
            r = 1.05 + 0.08 * math.sin(k * 2.7 + sign)
            pts.append((sign * math.cos(a) * r * 0.95, math.sin(a) * r))
        return pts
    halves = []
    for sign in (-1, 1):
        edge = [(x - sign * 0.0, y) for x, y in fis]
        outline = edge + list(reversed(rim(sign))) if sign > 0 else list(reversed(edge)) + rim(sign)
        if sign > 0:
            outline = list(reversed(edge)) + rim(sign)
            outline = list(reversed(outline))
        th = 0.38
        o = geo.extrude(f'Ground half {sign}', outline, th, [top, side], c, plane='XY', bevel=0.025)
        geo.place(o, (0, 0, -th))
        for v in o.data.vertices:
            if v.co.z < -0.15:
                v.co.z -= 0.12 * (0.5 + 0.5 * math.sin(v.co.x * 6 + v.co.y * 4))
        o.data.update()
        geo.assign_material_by(o, lambda ctr, nrm: 0 if nrm.z > 0.6 else 1)
        geo.displace_noise(o, 0.014, 5, seed=3 + sign)
        pivot = V(sign * 0.95, 0, 0)
        ang = math.radians(-30 if sign < 0 else 28)
        M = Matrix.Translation(V(sign * 0.1, 0, 0)) @ Matrix.Translation(pivot) @ Matrix.Rotation(ang, 4, 'Y') @ \
            Matrix.Translation(-pivot)
        geo.transform(o, M)
        halves.append(o)
    glow = K.glow('ff6a1a', 7.0, core='ffe8b0')
    P.ribbon('Fissure magma', [V(x, y, -0.12) for x, y in fis], 0.4, glow, c, up=(1, 0, 0))
    # branching glowing cracks across both slabs, projected onto their tilted tops
    from mathutils.bvhtree import BVHTree
    dg = bpy.context.evaluated_depsgraph_get()
    trees = [BVHTree.FromObject(h, dg) for h in halves]
    crack_m = K.glow('ff7a2a', 4.0, core='ffd890')
    for k, (sx, y0, ang, L) in enumerate([(-1, -0.45, 200, 0.7), (-1, 0.35, 160, 0.55), (1, -0.3, -25, 0.65),
                                           (1, 0.5, 20, 0.5), (-1, -0.05, 185, 0.4), (1, 0.1, 5, 0.45)]):
        a = math.radians(ang)
        start = V(sx * 0.16, y0, 0)
        end = start + V(math.cos(a), math.sin(a), 0) * L
        pts2 = P.jag_path(start, end, 9, 0.12, seed=40 + k)
        pts = []
        for p in pts2:
            best = None
            for tr in trees:
                hit = tr.ray_cast(V(p.x, p.y, 3.0), V(0, 0, -1))
                if hit[0] is not None and (best is None or hit[0].z > best.z):
                    best = hit[0]
            if best is not None:
                pts.append(best + V(0, 0, 0.004))
        if len(pts) > 2:
            P.fx_tube(f'Surface crack {k}', pts, geo.taper(len(pts), 0.016, 0.004), crack_m, c, sides=5)
    geo.cylinder('Magma below', 0.4, 0.04, (0, 0, -0.32), glow, c, 32, bevel=0, )
    P.halo_sphere('Fissure glow', V(0, -0.1, 0.15), 0.45, K.halo('ff5a20', strength=1.6, opacity=.32, power=2.0,
                                                                core='ffb060'), c, scale=(0.45, 1.9, 0.6))
    for i, (b, d, L, r) in enumerate([(V(0.0, -0.2, -0.3), V(-0.12, -0.1, 1), 1.5, 0.26),
                                      (V(0.02, 0.3, -0.3), V(0.3, 0.25, 1), 0.95, 0.17),
                                      (V(-0.02, -0.6, -0.3), V(-0.25, -0.35, 1), 0.75, 0.15),
                                      (V(0.0, 0.7, -0.3), V(0.15, 0.4, 1), 0.6, 0.12)]):
        P.prism(f'Rock spike {i}', b, d, L, r, spike, c, sides=6, tip=0.4, taper=0.8, seed=i, jitter=.3, twist=.4)
    chunk = K.stone('8a7660', moss=0.3)
    hot_chunk = P.magma_rock('3a2c24', 'ff6a2a', strength=4.0, crack=0.07, scale=4.0, hot='ffc080', name='Hot chunk')
    for i, (p, r) in enumerate([(V(-0.55, -0.35, 1.0), .15), (V(0.55, 0.1, 1.15), .13), (V(-0.12, 0.3, 1.45), .1),
                                (V(0.62, -0.5, 0.62), .11), (V(-0.78, 0.15, 0.55), .1), (V(0.2, -0.45, 1.3), .08),
                                (V(-0.35, -0.6, 0.45), .07)]):
        P.rock(f'Boulder {i}', p, r, hot_chunk if i % 3 == 1 else chunk, c, seed=i, rough=.3)
    dust = P.dust('c8b08e', name='Quake dust', opacity=0.85)
    for i, (p, r) in enumerate([(V(-0.45, -0.95, -0.05), 0.2), (V(0.55, -0.8, -0.1), 0.18), (V(-0.1, 0.95, 0.15), 0.22),
                                (V(0.12, 0.8, 0.3), 0.18), (V(-0.95, -0.2, -0.25), 0.2), (V(1.0, 0.25, -0.2), 0.2),
                                ]):
        P.puff(f'Dust burst {i}', p, r, dust, c, seed=i, lumps=4, squash=0.85)
    P.sparks('Embers', V(0, -0.1, 0.3), 16, 0.1, 0.9, 0.16, 0.012, K.spark_mat('ff8a4a'), c, seed=7, direction=(0, 0, 1),
             spread=0.6)
    P.sparks('Grit', V(0, 0, 0.4), 18, 0.2, 1.1, 0.06, 0.016, K.spark_mat('a08870', hot='c8b090', strength=0.6), c,
             seed=9, direction=(0, 0, 1), spread=1.0)
    xform(list(c.objects), rot=(0, 0, 22))
    return dict(el=18, az=20)


# ----------------------------------------------------------------------------- polymorph
@icon('spells', 'spell_polymorph', bloom=0.6)
def spell_polymorph(K):
    c = K.coll
    wool = P.wool('f3ece0', shade='c9b8a0')
    face = S_skin = K.leather('2d2420')
    rnd = random.Random(4)
    body_c = V(-0.05, 0, 0.0)
    geo.quadsphere('Fleece', 0.42, body_c, wool, c, level=3, scale=(1.25, 0.95, 0.85))
    for i in range(22):
        d = V(rnd.gauss(0, 1), rnd.gauss(0, 1), abs(rnd.gauss(0, 1)) * .8 + .1).normalized()
        p = body_c + V(d.x * 0.5, d.y * 0.38, d.z * 0.34)
        geo.quadsphere(f'Fleece puff {i}', rnd.uniform(0.13, 0.19), p, wool, c, level=2)
    head = V(0.55, -0.12, 0.2)
    geo.quadsphere('Head', 0.16, head, face, c, level=3, scale=(1.25, 0.85, 0.95), rotation=(0, 20, -15))
    geo.quadsphere('Head tuft', 0.12, head + V(-0.08, 0.02, 0.13), wool, c, level=2)
    for sy in (-1, 1):
        ear = geo.quadsphere(f'Ear {sy}', 0.07, head + V(-0.06, sy * 0.15, 0.05), face, c, level=2,
                             scale=(0.6, 1.4, 0.35), rotation=(sy * 25, 0, -15))
        eye = head + V(0.09, sy * 0.1 - 0.02, 0.06)
        geo.sphere(f'Eye {sy}', 0.03, eye, P.pearl('f6f2ea', name='Eye white'), c, 12, 8)
        geo.sphere(f'Pupil {sy}', 0.017, eye + V(0.015, -0.012, 0.0), K.L['eye'], c, 10, 6)
    for i, (x, y) in enumerate([(0.28, -0.18), (0.28, 0.18), (-0.38, -0.18), (-0.38, 0.18)]):
        geo.cylinder(f'Leg {i}', 0.045, 0.32, (x, y, -0.38), face, c, 10, radius2=0.04)
        geo.cylinder(f'Hoof {i}', 0.05, 0.05, (x, y, -0.55), K.L['dark'], c, 10)
    # transformation vortex
    vio = K.energy('b07cff', strength=3.5, hot='f4e8ff', opacity=.95, soft=0.0, fade_in=0.12, fade_out=0.7)
    vio2 = K.energy('ff7ce8', strength=3.0, hot='ffe8fa', opacity=.8, soft=0.0, fade_in=0.12, fade_out=0.7)
    for k in range(3):
        pts = P.spiral_points((0, 0, -0.62), 0.85, 0.55, 0.0, 1.35, 1.25, steps=72, phase=k * math.tau / 3)
        P.ribbon(f'Vortex ribbon {k}', pts, [0.11 * math.sin(math.pi * i / 72) ** .7 + 0.005 for i in range(73)],
                 vio if k != 1 else vio2, c, up=(0, 0, 1))
    P.halo_sphere('Hex glow', V(0, 0.15, 0.0), 0.95, K.halo('9a6aff', strength=1.4, opacity=.35, power=2.0), c)
    P.motes('Hex motes', V(0, 0, 0.1), 22, 0.95, 0.02, K.glow('e2ccff', 6.0), c, seed=2)
    _sparkles(K, [(V(-0.7, -0.4, 0.5), 1.0), (V(0.75, -0.45, 0.55), 0.7), (V(0.2, -0.6, -0.45), 0.6),
                  (V(-0.25, -0.5, 0.8), 0.5)], 'dcc8ff')
    return dict(el=14, az=18)


# ----------------------------------------------------------------------------- resurrect
def feather_wing(K, shoulder, side, mat, scale=1.0, n=7):
    out = []
    for k in range(n):
        t = k / (n - 1)
        ang = math.radians(-12 + 92 * t)
        L = (0.42 + 0.42 * math.sin(math.pi * (0.25 + 0.6 * t))) * scale
        d = V(side * math.cos(ang), 0.12, math.sin(ang)).normalized()
        f = P.leaf(f'Feather {side} {k}', shoulder + V(side * 0.03 * k, 0.01 * k, 0.02 * k), d, L, 0.15 * scale, mat,
                   K.coll, normal=(0, -1, 0), bend=-0.12, fold=0.15, seg=10, curl=side * 0.05)
        base = shoulder
        P.fx_attrs(f, lambda co, b=base, L=L: (min(1.0, (co - b).length / (L * 1.05)), 0.5))
        out.append(f)
    return out


@icon('spells', 'spell_resurrect', bloom=0.6, threshold=0.6)
def spell_resurrect(K):
    c = K.coll
    outline = [(-0.38, -0.7), (0.38, -0.7), (0.38, 0.22)] + \
              [(0.38 * math.cos(a), 0.22 + 0.38 * math.sin(a)) for a in [math.pi * k / 12 for k in range(1, 12)]] + \
              [(-0.38, 0.22)]
    stone = K.stone('8a857a', moss=0.4)
    ts = geo.extrude('Tombstone', outline, 0.17, stone, c, plane='XZ', bevel=0.035, segments=3)
    geo.displace_noise(ts, 0.014, 5, seed=3)
    geo.sculpt(ts, (0.38, 0, 0.45), 0.18, (-0.08, 0, -0.06))
    P.torus('Carved ring', 0.17, 0.016, stone, c, seg=40, sides=6, location=(0, -0.09, 0.2), rotation=(90, 0, 0))
    cross = [geo.box('Carved cross v', (0.05, 0.03, 0.42), (0, -0.09, 0.12), stone, c, bevel=0.01),
             geo.box('Carved cross h', (0.28, 0.03, 0.05), (0, -0.09, 0.22), stone, c, bevel=0.01)]
    xform([o for o in c.objects], loc=(-0.48, 0.5, -0.05), rot=(-6, 5, 16))
    earth = K.stone('4a3e32', moss=0.3)
    for i in range(9):
        a = i * math.tau / 9
        P.rock(f'Grave earth {i}', V(0.12 + math.cos(a) * 0.46, -0.1 + math.sin(a) * 0.26, -0.74), 0.1 + (i % 3) * .03,
               earth, c, seed=i, rough=.3, scale=(1.3, 1, .6))
    base = V(0.12, -0.1, -0.72)
    gold = 'ffcc3a'
    P.halo_sphere('Grave light', base + V(0, 0, 0.06), 0.4, K.halo(gold, strength=2.0, opacity=.55, power=1.2,
                                                                     core='fff2c0'), c, scale=(1.3, 0.8, 0.45))
    rays = K.energy(gold, strength=1.5, hot='fff2c0', opacity=.4, soft=0.0, fade_in=0.05, fade_out=0.5)
    for k in range(5):
        a = (k - 2) * 0.24
        d = V(math.sin(a), 0, math.cos(a))
        P.ribbon(f'Holy ray {k}', [base + d * 0.1 * t for t in range(14)], [0.08 + 0.012 * t for t in range(14)], rays,
                 c, up=(0, -1, 0))
    soul_c = base + V(0.06, -0.08, 1.05)
    wing = P.fx(((0.0, 'fff8e0', 1.0), (0.4, 'ffcc3a', 1.0), (1.0, 'ff8a10', 0.6)), strength=1.5, name='Wing light',
                soft=0.0)
    for side in (-1, 1):
        feather_wing(K, soul_c + V(side * 0.08, 0.02, 0.0), side, wing, scale=0.82)
    tail = K.energy('ffd04a', strength=2.4, hot='fff6d8', opacity=.95, soft=0.8, fade_in=0.02, fade_out=0.55)
    P.flame('Soul tail', soul_c + V(0, 0, 0.05), 1.0, 0.2, tail, c, seed=3, curl=.2, wobble=.5, twist=1.0,
            direction=(0.05, 0, -1), lean=(0.15, 0))
    P.halo_sphere('Soul core', soul_c, 0.13, K.halo('fff6d0', strength=4.0, opacity=1.0, power=0.8, core='ffffff'), c)
    geo.sphere('Soul orb', 0.085, soul_c, K.glow('ffe680', 6.0, core='ffffff'), c, 24, 14)
    P.halo_sphere('Soul aura', soul_c, 0.5, K.halo(gold, strength=1.2, opacity=.4, power=2.0), c)
    P.motes('Soul motes', soul_c + V(0, 0, -0.2), 16, 0.75, 0.018, K.glow('fff0b0', 5.0), c, seed=5, flatten=(1, .6, 1.2))
    _sparkles(K, [(V(0.78, -0.4, 0.6), 0.75), (V(-0.6, -0.45, 0.85), 0.55)], 'fff0b0')
    return dict(el=12, az=20)
