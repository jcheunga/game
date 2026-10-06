"""Relic icons: precious artifacts, rarity carried by glow and gilding."""
import math
import random

from mathutils import Vector

from rk import geo
from . import itemkit as P
from rk.heads import catmull
from .base import icon, V, wrap_cylinder


def band(name, R, h, mat, coll, lip=0.03, inner=0.035, z0=0.0, seg=64):
    """Solid crown band with rolled lips (closed profile revolved around Z)."""
    prof = [(R - inner, z0), (R + lip * .3, z0 - lip * .3), (R + lip, z0 + lip * .4), (R + lip * .5, z0 + lip),
            (R + lip * .5, z0 + h - lip), (R + lip, z0 + h - lip * .4), (R + lip * .3, z0 + h + lip * .3),
            (R - inner, z0 + h), (R - inner, z0)]
    return geo.lathe(name, prof, seg, mat, coll, close_top=False, close_bottom=False)


def fleur(name, w, h, depth, mat, coll):
    o = [(-0.11, 0), (-0.12, 0.05), (-0.18, 0.1), (-0.19, 0.17), (-0.14, 0.2), (-0.08, 0.15), (-0.05, 0.25), (0, 0.37),
         (0.05, 0.25), (0.08, 0.15), (0.14, 0.2), (0.19, 0.17), (0.18, 0.1), (0.12, 0.05), (0.11, 0)]
    pts = [(x * w / 0.38, z * h / 0.37) for x, z in o]
    return geo.extrude(name, pts, depth, mat, coll, plane='XZ', bevel=depth * .4, segments=3)


def setting(name, pos, nrm, size, gem_mat, metal, coll, oval=1.0, facets=10, prongs=0):
    nrm = Vector(nrm).normalized()
    rot = Vector((0, 0, 1)).rotation_difference(nrm).to_euler()
    rot = tuple(math.degrees(x) for x in rot)
    P.gem_cut(name, size, gem_mat, coll, location=pos, rotation=rot, facets=facets, scale=(1, oval, 1))
    P.torus(name + ' bezel', size * 1.06, size * .16, metal, coll, seg=28, sides=8, location=Vector(pos) - nrm * size * .05,
            rotation=rot, scale=(1, oval, 1))


@icon('relics', 'relic_crown_of_valor', bloom=0.45)
def relic_crown_of_valor(K):
    c = K.coll
    g = K.gilt('e6b44e', 0.18)
    pearl = P.pearl()
    R, H = 0.55, 0.24
    Rw = R + 0.02
    FL = 0.28
    band('Crown band', R, H, g, c)
    n = 5
    for i in range(n):
        a = i * math.tau / n
        f = fleur(f'Fleur {i}', 0.34, 0.44, 0.05, g, c)
        geo.place(f, (a * Rw, 0, H - 0.01))
        wrap_cylinder(f, Rw, flare=FL, z0=H)
        out = V(math.sin(a), -math.cos(a), 0)
        geo.sphere(f'Pearl {i}', 0.038, out * (Rw + FL * 0.45) + V(0, 0, H + 0.45), pearl, c, 14, 8)
        setting(f'Fleur gem {i}', out * (Rw + 0.035 + FL * .14) + V(0, 0, H + 0.14), (out * 1 + V(0, 0, FL)), 0.04,
                K.gem('2fd1c0', 1.2), g, c)
        b = a + math.pi / n
        s = geo.extrude(f'Spike {i}', [(-0.05, 0), (0.05, 0), (0.012, 0.17), (0.0, 0.2), (-0.012, 0.17)], 0.035, g, c,
                        plane='XZ', bevel=0.01)
        geo.place(s, (b * Rw, 0, H - 0.01))
        wrap_cylinder(s, Rw, flare=FL, z0=H)
        geo.sphere(f'Spike pearl {i}', 0.03, V(math.sin(b), -math.cos(b), 0) * (Rw + FL * .21) + V(0, 0, H + 0.22), pearl,
                   c, 12, 8)
    for i in range(n * 2):
        a = i * math.tau / (n * 2)
        big = i % 2 == 0
        out = V(math.sin(a), -math.cos(a), 0)
        setting(f'Band gem {i}', out * (R + 0.05) + V(0, 0, H * .5), out, 0.075 if big else 0.045,
                P.crystal('d0102a', deep='3a0008', glow=1.6, name='Ruby', inner=.9) if big else K.gem('2fd1c0', 1.0), g, c, oval=1.3 if big else 1.0)
    geo.lathe('Velvet cap', [(0, 0.34), (R * .5, 0.3), (R * .85, 0.18), (R - 0.03, 0.04)], 40,
              P.velvet('6a0c1c'), c, close_top=False, close_bottom=False)
    return dict(el=16)


# ============================================================================ shared relic helpers
from rk import weapons as W  # noqa: E402
from rk import shaders as S  # noqa: E402
from .base import xform, aim_rot, AMBER, TEAL, SOUL  # noqa: E402


def kit_mats(K, **over):
    m = dict(grip=K.leather('3a2418'), hilt=K.gilt('d9a845', 0.24), blade=K.steel('d6dce0', 0.16), gem=K.gem('2fd1c0', 1.4),
             trim=K.gilt('d9a845', 0.24), wood=K.wood('5a3a22'), steel=K.steel(), dark=K.L['dark'], bone=K.bone(),
             glow=K.glow(AMBER, 6.0), paint=P.enamel(TEAL, name='Caravan enamel'), rope=K.L['rope'], iron=K.iron())
    m.update(over)
    return m


def sparkles(K, pts, color, size=0.12, hot='ffffff', strength=6.0):
    mat = K.spark_mat(color, hot=hot, strength=strength)
    return [P.star_flare(f'Sparkle {i}', p, size * s, mat, K.coll, facing=(0.37, -0.93, 0.3)) for i, (p, s) in
            enumerate(pts)]


def aura(K, center, radius, color, opacity=0.3, strength=1.2, scale=(1, 1, 1)):
    # kept deliberately faint: a hint of rarity colour behind the silhouette, never a disc
    return P.halo_sphere('Rarity aura', center, radius * 0.85, K.halo(color, strength=strength * 0.8, opacity=opacity * 0.4,
                                                                      power=3.0), K.coll, scale=scale)


def bail_and_chain(K, top, metal, chain_mat=None, spread=0.55, height=0.75, link=0.035, wire=0.011):
    """Pendant bail at `top` with a chain rising in a V and looping behind."""
    c = K.coll
    P.torus('Bail', 0.07, 0.022, metal, c, seg=24, sides=8, location=top + V(0, 0, 0.06), rotation=(0, 90, 0))
    cm = chain_mat or metal
    for sx in (-1, 1):
        pts = catmull([top + V(0, 0, 0.12), top + V(sx * spread * .45, 0.05, height * .55),
                       top + V(sx * spread * .55, 0.18, height), top + V(sx * spread * .2, 0.32, height * 1.15)], 6)
        P.chain(f'Chain {sx}', pts, link, wire, cm, c)


# ============================================================================ commons
@icon('relics', 'relic_iron_pendant', bloom=0.35)
def relic_iron_pendant(K):
    c = K.coll
    iron = K.iron('5d646a', 0.36)
    brass = K.gilt('c9973f', 0.28)
    prof = [(0, -0.05), (0.42, -0.05), (0.46, -0.02), (0.46, 0.03), (0.42, 0.06), (0.36, 0.06), (0.33, 0.03),
            (0.2, 0.035), (0, 0.04)]
    med = geo.lathe('Medallion', prof, 48, iron, c)
    P.torus('Medallion rope rim', 0.395, 0.02, brass, c, seg=60, sides=8, location=(0, 0, 0.05))
    for i in range(8):
        a = i * math.tau / 8 + math.pi / 8
        geo.sphere(f'Rivet {i}', 0.028, (math.cos(a) * 0.3, math.sin(a) * 0.3, 0.045), brass, c, 10, 6, scale=(1, 1, .6))
    # cross-shaped bracing
    for k in range(4):
        a = k * math.pi / 2
        arm = geo.extrude(f'Brace {k}', [(-0.05, 0.12), (0.05, 0.12), (0.035, 0.33), (-0.035, 0.33)], 0.03, iron, c,
                          plane='XY', bevel=0.008)
        geo.place(arm, (0, 0, 0.055), (0, 0, math.degrees(a)))
    P.torus('Gem bezel', 0.13, 0.03, brass, c, seg=36, sides=8, location=(0, 0, 0.06))
    P.cabochon('Heart stone', 0.125, K.gem('2fd1c0', 1.0), c, location=(0, 0, 0.05), height=0.6)
    objs = list(c.objects)
    xform(objs, rot=(90, 0, 0))  # face -Y
    xform(objs, rot=(0, 0, 0), loc=(0, 0, 0))
    bail_and_chain(K, V(0, 0, 0.47), brass, chain_mat=iron, spread=0.6, height=0.55, link=0.04, wire=0.012)
    xform(list(c.objects), rot=(8, 0, -14))
    return dict(el=12)


@icon('relics', 'relic_sharpened_edge', bloom=0.45)
def relic_sharpened_edge(K):
    c = K.coll
    mats = kit_mats(K, blade=K.steel('e2e8ec', 0.12))
    sw = W.sword(mats, c, length=0.9, width=0.11, guard=0.34, grip=0.2, pommel='disc', guard_style='straight')
    xform(sw['objs'], loc=(0, 0, -0.45))
    stone = P.block('Whetstone', (0.18, 0.13, 0.6), K.stone('8a96a2', crack=0.15), c, round_=0.3, chip=0.008, seed=2)
    geo.place(stone, (0.12, -0.12, -0.05), (0, 62, 0))
    P.sparks('Honing sparks', V(0.05, -0.1, -0.1), 12, 0.05, 0.3, 0.08, 0.008, K.spark_mat('ffc860', hot='fff4d0'), c,
             seed=3, direction=(1, -0.3, 0.6), spread=0.6)
    # honed edge gleam
    edge_m = K.beam('cfefff', strength=3.0, hot='ffffff', opacity=.8, soft=0.2)
    gz = 0.2 / 2 + 0.012 - 0.45
    for sx in (-1, 1):
        pts = [V(sx * 0.054 * (1 - 0.35 * t) * (1 if t < .78 else max(0.02, (1 - t) / .22) ** .8), -0.002,
                 gz + 0.01 + 0.9 * t) for t in [k / 30 for k in range(31)]]
        P.fx_tube(f'Honed edge {sx}', pts, 0.006, edge_m, c, sides=5)
    objs = [o for o in c.objects if o.name != 'Whetstone']
    xform(list(c.objects), rot=(0, -42, 0))
    sparkles(K, [(V(0.5, -0.1, 0.42), 1.0), (V(0.12, -0.1, 0.05), 0.55)], 'd8f4ff', size=0.15)
    return dict(el=14, az=14)


@icon('relics', 'relic_swift_boots', bloom=0.4)
def relic_swift_boots(K):
    c = K.coll
    lea = K.leather('7a4a28')
    dark = K.leather('3a2416')
    teal = K.cloth(TEAL, '0e3c39', sheen=0.6)
    brass = K.gilt('d0a048', 0.26)
    shaft = geo.lathe('Boot shaft', [(0.15, 0.2), (0.155, 0.45), (0.165, 0.7), (0.185, 0.92)], 28, lea, c,
                      close_top=False, close_bottom=False)
    P.solid_mod(shaft, 0.025, offset=-1)
    P.subsurf_mod(shaft, 1)
    geo.place(shaft, scale=(1, 0.9, 1))
    foot = geo.quadsphere('Boot foot', 1.0, (0, 0, 0), lea, c, level=3)
    for v in foot.data.vertices:
        x, y, z = v.co
        v.co = V(x * 0.33 + 0.16, y * 0.15, max(z, -0.55) * 0.15 + 0.12 + 0.03 * max(0, x) ** 2)
    foot.data.update()
    geo.store_rest(foot)
    P.subsurf_mod(foot, 1)
    geo.quadsphere('Boot ankle', 0.155, (0.0, 0, 0.22), lea, c, level=3, scale=(1.05, 0.92, 1.1))
    sole = geo.extrude('Sole', [(-0.16, -0.12), (0.2, -0.13), (0.42, -0.11), (0.5, -0.04), (0.5, 0.04), (0.42, 0.11),
                                (0.2, 0.13), (-0.16, 0.12)], 0.05, dark, c, plane='XY', bevel=0.015)
    geo.place(sole, (0, 0, 0.0))
    geo.box('Heel', (0.2, 0.22, 0.09), (-0.06, 0, -0.04), dark, c, bevel=0.02)
    cuff = geo.lathe('Cuff', [(0.17, 0.82), (0.215, 0.86), (0.235, 0.95), (0.2, 1.0), (0.17, 0.98)], 32, teal, c)
    geo.place(cuff, scale=(1, 0.9, 1))
    P.torus('Cuff trim', 0.222, 0.012, brass, c, seg=40, sides=6, location=(0, 0, 0.87), scale=(1, 0.9, 1))
    for z in (0.34, 0.58):
        P.torus(f'Strap {z}', 0.158 + (z - 0.3) * 0.04, 0.018, dark, c, seg=40, sides=6,
                location=(0, 0, z), scale=(1, 0.9, 0.6))
        geo.box(f'Buckle {z}', (0.07, 0.025, 0.06), (0.02, -0.15, z), brass, c, bevel=0.01, rotation=(0, 0, -10))
    # wing at the ankle
    wing = P.fx(((0.0, 'ffffff', 1.0), (0.6, 'eaf6ff', 1.0), (1.0, 'b8dcff', 0.85)), strength=0.6, name='Wing feather',
                soft=0.0)
    feather = K.cloth('f2efe6', 'cfd8e0', sheen=0.8)
    root = V(-0.08, -0.17, 0.5)
    tip_m = K.gilt('e0b050', 0.25)
    for k in range(7):
        t = k / 6
        ang = math.radians(8 + 78 * t)
        d = V(-math.cos(ang), -0.2, math.sin(ang)).normalized()
        L = 0.42 + 0.22 * math.sin(math.pi * (0.2 + 0.7 * t))
        P.leaf(f'Feather {k}', root + V(0.02 * k, -0.005 * k, -0.035 * k), d, L, 0.13, feather, c,
               normal=(0, -1, 0), bend=0.18, fold=0.2, seg=8)
        P.leaf(f'Feather tip {k}', root + V(0.02 * k, -0.012 * k - 0.01, -0.035 * k) + d * (L * 0.62), d, L * 0.36, 0.1,
               tip_m, c, normal=(0, -1, 0), bend=0.1, fold=0.2, seg=6)
    geo.sphere('Wing root', 0.045, root + V(0, 0.01, -0.08), brass, c, 14, 8)
    swoosh = K.energy('bfe8ff', strength=1.6, hot='ffffff', opacity=.75, soft=0.0, fade_in=0.2, fade_out=0.6)
    for k, z in enumerate((0.15, 0.4, 0.68)):
        pts = [V(-0.3 - 0.55 * t, 0.15, z + 0.06 * math.sin(t * 3)) for t in [i / 20 for i in range(21)]]
        P.ribbon(f'Speed line {k}', pts, 0.035, swoosh, c, up=(0, -1, 0), fx_range=(0.0, 1.0))
    xform(list(c.objects), loc=(0, 0, -0.45), rot=(0, 0, -28))
    return dict(el=14, az=20)


@icon('relics', 'relic_battle_drum', bloom=0.3)
def relic_battle_drum(K):
    c = K.coll
    wood = K.wood('7a4c2a', axis='Z')
    paint = P.enamel('8a1e24', name='Drum paint', rough=0.4)
    hide = P.parchment('dcc9a0', name='Drum hide', stain='a0804c')
    brass = K.gilt('c99a45', 0.28)
    R, H = 0.5, 0.62
    geo.lathe('Drum shell', [(R * 0.96, -H / 2), (R, -H / 4), (R * 1.03, 0), (R, H / 4), (R * 0.96, H / 2)], 48, wood, c,
              close_top=False, close_bottom=False)
    geo.lathe('Paint band', [(R * 1.006, -0.1), (R * 1.032, 0), (R * 1.006, 0.1)], 48, paint, c, close_top=False,
              close_bottom=False)
    geo.cylinder('Drum head', R * 0.97, 0.02, (0, 0, H / 2 + 0.005), hide, c, 48, bevel=0.004)
    geo.cylinder('Drum bottom', R * 0.97, 0.02, (0, 0, -H / 2 - 0.005), hide, c, 48, bevel=0.004)
    for z in (H / 2 + 0.02, -H / 2 - 0.02):
        P.torus(f'Hoop {z}', R * 0.99, 0.04, K.wood('4a2e1a'), c, seg=48, sides=10, location=(0, 0, z),
                scale=(1, 1, 1.2))
    n = 12
    rope = K.L['rope']
    for i in range(n):
        a0 = i * math.tau / n
        a1 = (i + 0.5) * math.tau / n
        p0 = V(math.cos(a0) * R * 1.06, math.sin(a0) * R * 1.06, H / 2 - 0.02)
        p1 = V(math.cos(a1) * R * 1.06, math.sin(a1) * R * 1.06, -H / 2 + 0.02)
        a2 = (i + 1) * math.tau / n
        p2 = V(math.cos(a2) * R * 1.06, math.sin(a2) * R * 1.06, H / 2 - 0.02)
        geo.tube(f'Cord {i}a', [p0, p0.lerp(p1, .5) * 1.0 + V(0, 0, 0) + (p0.lerp(p1, .5)).normalized() * 0.03 * 0, p1],
                 0.014, rope, c, sides=6)
        geo.tube(f'Cord {i}b', [p1, p2], 0.014, rope, c, sides=6)
        mid = p0.lerp(p1, 0.5)
        geo.box(f'Tension tab {i}', (0.05, 0.035, 0.08), mid * 1.02, K.leather('3a2416'), c, bevel=0.01,
                rotation=(0, 0, math.degrees(a0 + math.pi / n / 2) + 90))
    P.torus('Head ring', 0.3, 0.02, P.enamel('8a1e24', name='Head paint'), c, seg=48, sides=4, location=(0, 0, H / 2 + 0.012),
            scale=(1, 1, 0.3))
    P.rune('Head sigil', 'tiwaz', V(0, 0, H / 2 + 0.018), V(1, 0, 0), V(0, 1, 0), 0.16, 0.022,
           P.enamel('8a1e24', name='Head paint'), c, depth_axis=V(0, 0, 1))
    for k, (rot, off) in enumerate([((0, 78, 35), V(-0.05, -0.12, 0.42)), ((0, 78, -50), V(0.06, -0.1, 0.44))]):
        st = geo.lathe(f'Drumstick {k}', [(0, -0.42), (0.03, -0.4), (0.028, 0.25), (0.045, 0.32), (0.05, 0.38),
                                         (0.03, 0.43), (0, 0.44)], 14, K.wood('c8a070'), c)
        geo.place(st, off, rot)
    return dict(el=28, az=20)


# ============================================================================ rares
@icon('relics', 'relic_guardian_shield', bloom=0.4)
def relic_guardian_shield(K):
    c = K.coll
    mats = kit_mats(K, paint=S.paint(TEAL, name='Guardian enamel', chip=0.55), trim=K.gilt('d8a848', 0.25))
    em_build = W.emblem_lantern(K.gilt('e8bc58', 0.2), K.glow(AMBER, 5.0))
    sh = W.heater_shield(mats, c, size=1.65, curve=0.16, emblem=None, boss=False, rim=True, face_key='paint')
    curve = 0.16

    def face_x(y):
        return 0.035 - curve * (y / (0.3 * 1.65)) ** 2 * 0.3 * 1.65
    # brass cross-bands following the curved face
    band_m = mats['trim']
    ys = [y / 20 * 0.44 - 0.22 for y in range(21)]
    geo.tube('Band horizontal', [V(face_x(y * 2), y * 2, 0.2) for y in ys], 0.022, band_m, c, sides=6, flatten=0.5)
    geo.tube('Band vertical', [V(0.035, 0, z) for z in [0.47 - k * 0.06 for k in range(18)]], 0.022, band_m, c, sides=6,
             flatten=0.5)
    for y in (-0.36, -0.18, 0.18, 0.36):
        geo.sphere(f'Band rivet {y}', 0.024, (face_x(y) + 0.012, y, 0.2), band_m, c, 10, 6, scale=(.6, 1, 1))
    for z in (0.42, -0.05, -0.32):
        geo.sphere(f'Band rivet v{z}', 0.024, (0.047, 0, z), band_m, c, 10, 6, scale=(.6, 1, 1))
    geo.sphere('Emblem boss', 0.17, (0.0, 0, 0.2), mats['trim'], c, 32, 16, scale=(0.35, 1, 1))
    for o in em_build(V(0.065, 0, 0.19), c, 1.15):
        pass
    xform(list(c.objects), rot=(0, 0, -100))
    xform(list(c.objects), rot=(-6, 4, 0))
    aura(K, V(0, 0.2, 0.05), 0.8, '3fd8c8', opacity=.18)
    sparkles(K, [(V(-0.42, -0.25, 0.55), 0.8), (V(0.4, -0.2, -0.35), 0.5)], 'c8fff4', size=0.13)
    return dict(el=12, az=14)


@icon('relics', 'relic_war_brand', bloom=0.6)
def relic_war_brand(K):
    c = K.coll
    ember = S.ember_metal('4a4542', 'ff6a1a', name='Brand steel', strength=12.0, crack=0.03, scale=4.0)
    mats = kit_mats(K, blade=ember, hilt=K.black_iron(), grip=K.leather('2a1810'))
    sw = W.sword(mats, c, length=1.05, width=0.27, guard=0.56, grip=0.26, pommel='ball', guard_style='winged', flare=0.25)
    edge = K.energy('ff8a2a', strength=2.2, hot='ffd890', opacity=.35, soft=1.6, fade_in=0.05, fade_out=0.9)
    gz = 0.24 / 2 + 0.022
    spine = [V(0, 0, gz + 1.0 * t) for t in [k / 20 for k in range(21)]]
    xform(list(c.objects), loc=(0, 0, -0.5), rot=(0, -40, 0))
    # blade centre after the transform is near (-0.39, 0, -0.04); embers peel off its upper half
    P.sparks('Embers', V(-0.5, -0.08, 0.2), 22, 0.05, 0.42, 0.08, 0.012, K.spark_mat('ff7a2a', hot='ffd890'), c, seed=4,
             direction=(0.25, 0, 1), spread=0.8)
    return dict(el=12, az=14)


def cloak_sheet(name, mat, coll, width=1.0, length=1.3, billow=0.35, folds=7, tatter=0.0, wind=0.0, seed=0, hood=True,
                nx=28, ny=24, thickness=0.025):
    """Cloak hanging from a neck point at the origin, opening downwards; front faces -Y."""
    rnd = random.Random(seed)
    phases = [rnd.random() * 6 for _ in range(4)]

    def fn(u, v):
        a = (u - 0.5) * math.pi * (0.55 + 0.75 * v)       # wrap angle around the body
        r = 0.09 + v * width * 0.55
        x = math.sin(a) * r
        y = -math.cos(a) * r * 0.55 + 0.2 * v
        fold = math.sin(u * folds * math.tau + phases[0]) * 0.035 * v * (1 + billow)
        x += fold * math.cos(a)
        y += fold * 0.8
        z = -v * length
        # tattered hem
        if tatter:
            k = abs(((u * 9 + phases[1]) % 1.0) - 0.5) * 2
            z += tatter * (k ** 2) * 0.2 * v ** 6
        # wind sweep
        x += wind * (v ** 2) * 0.5
        y += wind * (v ** 2) * 0.15 * math.sin(u * 5 + phases[2])
        z += wind * (v ** 2) * 0.18
        return (x, y, z)
    return P.sheet(name, fn, nx, ny, mat, coll, thickness=thickness, subsurf=1)


def mantle(name, outer, lining, coll, length=1.3, neck=0.13, shoulder=0.42, flare=0.2, folds=9, fold_amp=0.035,
           gap=55.0, wind=0.0, tatter=0.0, seed=0, nu=48, nv=30, thickness=0.03):
    """Cloak worn by an invisible figure: shoulder yoke, hanging body, open front showing the lining.
    u runs from the front-left edge around the back to the front-right edge; v from collar to hem."""
    rnd = random.Random(seed)
    ph = [rnd.random() * 6 for _ in range(3)]
    half = math.radians(180 - gap / 2)
    yoke = 0.2

    def fn(u, v):
        a = -half + 2 * half * u              # 0 = centre back (+Y), +-180 = front
        if v < yoke:
            k = v / yoke
            r = neck + (shoulder - neck) * math.sin(k * math.pi / 2)
            z = -0.1 * k * k
        else:
            k = (v - yoke) / (1 - yoke)
            r = shoulder + flare * k ** 1.3
            r += fold_amp * math.sin(a * folds + ph[0]) * min(1.0, k * 3) * (0.6 + k)
            z = -0.1 - k * length
            if tatter:
                t = abs(((u * 11 + ph[1]) % 1.0) - 0.5) * 2
                z += tatter * 0.16 * (t ** 2) * k ** 8
            if wind:
                r += wind * 0.25 * k ** 2 * (1 + math.sin(a + ph[2]))
                z += wind * 0.12 * k ** 2
        x = math.sin(a) * r * 1.05
        y = math.cos(a) * r * 0.62
        if v >= yoke and wind:
            k = (v - yoke) / (1 - yoke)
            x += wind * 0.45 * k ** 2
            y += wind * 0.1 * k ** 2
        # front edges fall a little inward
        edge = max(0.0, abs(a) / half - 0.85) / 0.15
        y -= 0.04 * edge * (v > yoke)
        return (x, y, z)
    obj = geo.grid_sheet(name, 1, 1, nu, nv, fn, [outer, lining] if lining is not None else outer, coll)
    if thickness:
        m = obj.modifiers.new('Cloth thickness', 'SOLIDIFY')
        m.thickness = thickness
        m.offset = 1.0
        m.use_even_offset = False
        m.use_rim = True
        if lining is not None:
            m.material_offset = 1
            m.material_offset_rim = 1
    P.subsurf_mod(obj, 1)
    return obj, fn


@icon('relics', 'relic_windrunner_cloak', bloom=0.45)
def relic_windrunner_cloak(K):
    c = K.coll
    cloth = K.cloth('1f7a72', '0f3e3b', sheen=0.9)
    lining = K.cloth('c9a45a', '7a5a26', sheen=0.6)
    cl, fn = mantle('Cloak', cloth, lining, c, length=1.25, flare=0.32, folds=8, fold_amp=0.045, gap=70, wind=0.9,
                    tatter=1.0, seed=3)
    collar = geo.lathe('High collar', [(0.13, -0.02), (0.15, 0.08), (0.2, 0.17)], 32, cloth, c, close_top=False,
                       close_bottom=False, angle=math.radians(250))
    P.solid_mod(collar, 0.02)
    geo.place(collar, (0, 0, 0), (0, 0, -35), scale=(1.05, 0.65, 1))
    hood = geo.quadsphere('Hood lying back', 0.17, (0, 0.17, -0.02), cloth, c, level=3, scale=(1.3, 0.8, 0.6))
    clasp = K.gilt('dcae52', 0.22)
    for sx in (-1, 1):
        p = V(*fn(0.0 if sx < 0 else 1.0, 0.22)) + V(0, -0.03, 0)
        P.torus(f'Clasp ring {sx}', 0.055, 0.018, clasp, c, seg=24, sides=8, location=p, rotation=(90, 0, 0))
        P.gem_cut(f'Clasp gem {sx}', 0.042, K.gem('2fd1c0', 1.5), c, location=p + V(0, -0.02, 0), rotation=(90, 0, 0),
                  facets=10)
    P.chain('Clasp chain', [V(*fn(0.0, 0.22)) + V(0, -0.04, 0), V(0, -0.36, -0.36), V(*fn(1.0, 0.22)) + V(0, -0.04, 0)],
            0.025, 0.008, clasp, c)
    wind = K.energy('c8f8ff', strength=1.8, hot='ffffff', opacity=.85, soft=0.0, fade_in=0.15, fade_out=0.65)
    for k in range(3):
        pts = P.spiral_points(V(0.15, 0.0, -0.75 + k * 0.32), 0.7 - k * 0.08, 0.55, 0.0, 0.18, 0.75, steps=50,
                              phase=k * 1.7)
        P.ribbon(f'Wind {k}', pts, [0.05 * math.sin(math.pi * i / 50) + 0.004 for i in range(51)], wind, c, up=(0, 0, 1))
    xform(list(c.objects), loc=(-0.1, 0, 0.62), rot=(0, 0, 22))
    sparkles(K, [(V(0.75, -0.4, 0.2), 0.7), (V(-0.55, -0.4, -0.5), 0.5)], 'd8fbff', size=0.12)
    return dict(el=12, az=18)


@icon('relics', 'relic_sages_ring', bloom=0.5)
def relic_sages_ring(K):
    c = K.coll
    silver = K.silver('dde2e8', 0.16)
    prof = [(math.cos(a) * 1.0, math.sin(a) * 0.55) for a in [i * math.tau / 16 for i in range(16)]]
    P.torus('Ring band', 0.42, 0.075, silver, c, seg=64, profile=prof, rotation=(90, 0, 0))
    glow = K.glow('9fe0ff', 4.0, core='ffffff')
    for i, g in enumerate(['raido', 'kenaz', 'ingwaz', 'fehu', 'sowilo']):
        a = math.radians(200 + i * 28)
        o = V(math.cos(a) * 0.495, -0.0, math.sin(a) * 0.495)
        tdir = V(-math.sin(a), 0, math.cos(a))
        P.rune(f'Band rune {i}', g, o + V(0, -0.0, 0), tdir, V(0, -1, 0), 0.03, 0.006, glow, c,
               depth_axis=V(math.cos(a), 0, math.sin(a)))
    top = V(0, 0, 0.5)
    geo.lathe('Setting', [(0.05, 0.42), (0.12, 0.47), (0.15, 0.53), (0.13, 0.56)], 24, silver, c,
              close_top=False, close_bottom=True)
    for i in range(6):
        a = i * math.tau / 6
        geo.tube(f'Prong {i}', [V(math.cos(a) * 0.12, math.sin(a) * 0.12, 0.5), V(math.cos(a) * 0.17, math.sin(a) * 0.17, 0.62),
                                V(math.cos(a) * 0.13, math.sin(a) * 0.13, 0.69)], [0.02, 0.018, 0.012], silver, c, sides=6)
    P.gem_cut('Sage stone', 0.17, P.crystal('7fd8ff', deep='0e3a6a', glow=2.4, name='Sage stone', inner=1.0), c,
              location=(0, 0, 0.62), facets=12, crown=0.45, pavilion=0.7)
    xform(list(c.objects), rot=(0, 0, 28))
    xform(list(c.objects), rot=(-14, 0, 0))
    aura(K, V(0, 0.1, 0.55), 0.55, '7fd8ff', opacity=.25)
    sparkles(K, [(V(0.18, -0.25, 0.85), 1.0), (V(-0.5, -0.2, -0.3), 0.5)], 'd8f4ff', size=0.14)
    return dict(el=14)


# ============================================================================ epics
@icon('relics', 'relic_blade_of_ruin', bloom=0.55)
def relic_blade_of_ruin(K):
    c = K.coll
    obs = P.obsidian('17121c', 'ff2a44', strength=6.5, crack=0.018, scale=3.5)
    iron = K.black_iron()
    L, Wd = 1.22, 0.36
    out = []
    pts = [(-Wd * 0.5, 0.0)]
    n = 7
    for k in range(n):
        z0 = 0.05 + k * (L * 0.72 / n)
        w = Wd * 0.5 * (1 - 0.25 * k / n)
        pts += [(-w, z0), (-w - 0.05, z0 + 0.04), (-w + 0.01, z0 + 0.09)]
    pts += [(-0.02, L * 0.92), (0.0, L), (0.02, L * 0.92)]
    right = []
    for k in reversed(range(n)):
        z0 = 0.05 + k * (L * 0.72 / n)
        w = Wd * 0.5 * (1 - 0.25 * k / n)
        right += [(w - 0.01, z0 + 0.09), (w + 0.05, z0 + 0.04), (w, z0)]
    pts += [(w0, z) for w0, z in [(x, z) for x, z in right]]
    pts += [(Wd * 0.5, 0.0)]
    blade = geo.extrude('Ruin blade', pts, 0.05, obs, c, plane='XZ', bevel=0.012, segments=2)
    for v in blade.data.vertices:
        k = min(1.0, abs(v.co.x) / (Wd * 0.5))
        v.co.y *= 1 - 0.8 * k ** 1.5
    blade.data.update()
    geo.store_rest(blade)
    geo.place(blade, (0, 0, 0.14))
    vein = K.beam('ff2a40', strength=4.0, hot='ffb0b8', soft=0.3)
    P.fx_tube('Blood fuller', [V(0, -0.016, 0.25 + t * 0.75) for t in [k / 12 for k in range(13)]], 0.02, vein, c, sides=6)
    P.halo_sphere('Ruin glow', V(0, 0.12, 0.7), 0.3, K.halo('ff1e3a', strength=1.0, opacity=.16, power=2.4), c,
                  scale=(0.8, 0.5, 2.0))
    for sx in (-1, 1):
        g = geo.tube(f'Guard horn {sx}', catmull([V(0, 0, 0.12), V(sx * 0.2, 0, 0.11), V(sx * 0.36, 0, 0.2),
                                                  V(sx * 0.42, 0, 0.36)], 4), geo.taper(13, 0.07, 0.008), iron, c, sides=8)
    geo.box('Guard block', (0.14, 0.09, 0.1), (0, 0, 0.11), iron, c, bevel=0.02)
    P.gem_cut('Guard gem', 0.045, P.crystal('ff1e3a', deep='3a0008', glow=2.5, name='Ruin gem'), c, location=(0, -0.05, 0.11),
              rotation=(90, 0, 0), facets=8)
    geo.cylinder('Grip', 0.03, 0.26, (0, 0, -0.07), K.leather('1e1416'), c, 12)
    for i in range(6):
        P.torus(f'Grip wrap {i}', 0.032, 0.008, iron, c, seg=16, sides=6, location=(0, 0, -0.18 + i * 0.045))
    P.prism('Pommel spike', V(0, 0, -0.2), V(0, 0, -1), 0.14, 0.045, iron, c, sides=6, tip=0.6)
    xform(list(c.objects), loc=(0, 0, -0.5), rot=(0, -40, 0))
    P.sparks('Ruin embers', V(0.1, 0, 0.2), 16, 0.2, 0.7, 0.08, 0.012, K.spark_mat('ff3048', hot='ffb0c0'), c, seed=5,
             direction=(0, 0, 1), spread=1.0)
    return dict(el=12, az=14)


@icon('relics', 'relic_phantom_mantle', bloom=0.6)
def relic_phantom_mantle(K):
    c = K.coll
    gh = P.ghost('8ff0e0', strength=1.5, opacity=.9, name='Phantom shroud', power=1.1, base_alpha=0.3)
    cl, fn = mantle('Shroud', gh, None, c, length=1.3, flare=0.3, folds=7, fold_amp=0.05, gap=40, wind=0.2, tatter=1.8,
                    seed=5, thickness=0.0)
    P.set_point_attr(cl, 'fx', [min(1.0, max(0.0, (-v.co.z - 0.2) / 1.3)) ** 1.4 for v in cl.data.vertices])
    # raised hood: an open-fronted shell around an empty face
    hood = geo.lathe('Phantom hood', [(0.0, 0.42), (0.12, 0.4), (0.2, 0.32), (0.23, 0.18), (0.22, 0.02), (0.17, -0.08)],
                     32, gh, c, close_top=False, close_bottom=False, angle=math.radians(290))
    geo.place(hood, (0, 0.02, 0.0), (0, 0, 125))
    for v in hood.data.vertices:
        if v.co.z > 0.3:
            v.co.y += (v.co.z - 0.3) * 0.5
    hood.data.update()
    geo.store_rest(hood)
    P.set_point_attr(hood, 'fx', [0.0] * len(hood.data.vertices))
    void = geo.quadsphere('Hood void', 0.17, (0, 0.04, 0.17), K.L['dark'], c, level=3, scale=(1.0, 0.7, 1.15))
    eye = K.glow('aaffee', 8.0, core='ffffff')
    for sx in (-1, 1):
        geo.sphere(f'Phantom eye {sx}', 0.024, (sx * 0.065, -0.08, 0.17), eye, c, 12, 8, scale=(1.4, 1, 0.7))
    P.halo_sphere('Eye glow', V(0, -0.12, 0.17), 0.14, K.halo('7fffe0', strength=2.0, opacity=.55, power=1.5), c)
    wisp = K.energy('9ff8e8', strength=1.6, hot='eafffa', opacity=.7, soft=0.0, fade_in=0.1, fade_out=0.6)
    for k in range(3):
        pts = P.spiral_points(V(0, 0.1, -1.0 + k * 0.38), 0.68, 0.55, 0.0, 0.25, 0.7, steps=48, phase=k * 2.1)
        P.ribbon(f'Phantom wisp {k}', pts, [0.06 * math.sin(math.pi * i / 48) + 0.004 for i in range(49)], wisp, c,
                 up=(0, 0, 1))
    xform(list(c.objects), loc=(0, 0, 0.6))
    aura(K, V(0, 0.25, 0.0), 0.95, '5fe0d0', opacity=.22)
    P.motes('Phantom motes', V(0, 0, -0.2), 18, 0.8, 0.016, K.glow('bfffee', 5.0), c, seed=3)
    return dict(el=10, az=16)


@icon('relics', 'relic_dragon_heart', bloom=0.6)
def relic_dragon_heart(K):
    c = K.coll
    heart = P.heart_shape('Dragon heart', 0.42, P.crystal('e8200c', deep='3a0200', glow=1.1, name='Heart crystal',
                                                          core='ffb040', inner=1.4, transmission=0.2, coat=0.3), c,
                          depth=0.62, level=2)
    for p in heart.data.polygons:
        p.use_smooth = False
    geo.place(heart, (0, 0, 0.12))
    P.halo_sphere('Heart fire', V(0, -0.05, 0.12), 0.24, K.halo('ff6a1a', strength=1.8, opacity=.45, power=1.6,
                                                                  core='ffc070'), c)
    gold = K.gilt('e2b04a', 0.22)
    geo.lathe('Claw mount', [(0, -0.62), (0.26, -0.62), (0.28, -0.58), (0.18, -0.52), (0.1, -0.42), (0.12, -0.36),
                             (0.08, -0.3), (0, -0.28)], 32, gold, c)
    for i in range(4):
        a = i * math.tau / 4 + math.pi / 4
        d = V(math.cos(a), math.sin(a), 0)
        pts = catmull([V(0, 0, -0.3) + d * 0.06, V(0, 0, -0.32) + d * 0.3, V(0, 0, -0.05) + d * 0.43,
                       V(0, 0, 0.22) + d * 0.36, V(0, 0, 0.32) + d * 0.22], 5)
        geo.tube(f'Talon {i}', pts, geo.taper(len(pts), 0.075, 0.01, 0.8), gold, c, sides=10)
    for i in range(5):
        P.flame(f'Heart flame {i}', V(-0.12 + i * 0.06, 0.0, 0.36), 0.45 + (i % 2) * 0.18, 0.07,
                K.fire(strength=2.0, hot='ffe08a', mid='ff8a20', outer='e03a0a', smoke='5a0a02'), c, seed=i, curl=.5)
    aura(K, V(0, 0.2, 0.1), 0.85, 'ff4a1a', opacity=.25)
    P.sparks('Heart embers', V(0, 0, 0.5), 14, 0.1, 0.6, 0.07, 0.011, K.spark_mat('ff9a3a'), c, seed=2,
             direction=(0, 0, 1), spread=0.8)
    return dict(el=12)


@icon('relics', 'relic_wolftooth_charm', bloom=0.3)
def relic_wolftooth_charm(K):
    c = K.coll
    tooth = K.bone('eee2c4')
    spine = catmull([V(0, 0, 0.32), V(0.02, 0, 0.0), V(-0.06, 0, -0.35), V(-0.22, 0, -0.62)], 6)
    fang = geo.tube('Wolf fang', spine, geo.taper(len(spine), 0.17, 0.005, 0.85), tooth, c, sides=18, flatten=0.75)
    brass = K.gilt('c99a45', 0.28)
    geo.lathe('Fang cap', [(0.15, 0.26), (0.19, 0.3), (0.19, 0.38), (0.15, 0.43), (0.07, 0.46)], 24, brass, c)
    P.torus('Cap ring', 0.175, 0.02, brass, c, seg=32, sides=6, location=(0, 0, 0.3))
    P.cabochon('Cap gem', 0.05, K.gem('2fd1c0', 1.2), c, location=(0, -0.185, 0.34), rotation=(90, 0, 0))
    P.torus('Loop', 0.06, 0.018, brass, c, seg=24, sides=8, location=(0, 0, 0.5), rotation=(90, 0, 0))
    cord = K.leather('5a3420')
    for sx in (-1, 1):
        pts = catmull([V(0, 0, 0.52), V(sx * 0.17, 0.02, 0.6), V(sx * 0.3, 0.08, 0.74), V(sx * 0.32, 0.18, 0.86)], 6)
        geo.tube(f'Cord {sx}', pts, 0.018, cord, c, sides=8)
        for k, t in enumerate((0.45, 0.75)):
            p = pts[int(t * (len(pts) - 1))]
            geo.sphere(f'Bead {sx}{k}', 0.045 if k == 0 else 0.038, p,
                       K.gem('2fd1c0', 0.4) if k == 0 else K.wood('8a5a30'), c, 16, 10)
    # carved notches
    for i in range(3):
        P.torus(f'Notch {i}', 0.105 - i * 0.02, 0.008, K.leather('3a2416'), c, seg=24, sides=5,
                location=spine[3 + i * 2], rotation=aim_rot(spine[4 + i * 2] - spine[2 + i * 2]))
    xform(list(c.objects), rot=(0, 0, -18))
    xform(list(c.objects), rot=(0, 18, 0))
    return dict(el=10)


@icon('relics', 'relic_tombstone_shard', bloom=0.45)
def relic_tombstone_shard(K):
    c = K.coll
    stone = K.stone('8e8a80', moss=0.3)
    outline = [(-0.42, -0.6), (-0.1, -0.68), (0.12, -0.5), (0.35, -0.62), (0.44, -0.2), (0.38, 0.12), (0.46, 0.3),
               (0.3, 0.52), (0.05, 0.62), (-0.2, 0.58), (-0.36, 0.42), (-0.3, 0.18), (-0.48, -0.05)]
    shard = geo.extrude('Grave shard', outline, 0.2, stone, c, plane='XZ', bevel=0.03, segments=2)
    geo.displace_noise(shard, 0.02, 4, seed=7)
    for v in shard.data.vertices:
        if v.co.y > 0:
            v.co.y += 0.04 * math.sin(v.co.x * 5) + 0.03
    shard.data.update()
    geo.store_rest(shard)
    glow = K.glow('7dff9a', 6.0, core='eaffe8')
    sk = P.rune('Grave mark', 'othala', V(0.0, -0.115, 0.02), V(1, 0, 0), V(0, 0, 1), 0.24, 0.034, glow, c)
    P.halo_sphere('Mark glow', V(0, -0.15, 0.02), 0.36, K.halo('5fe07a', strength=1.8, opacity=.5, power=1.5), c)
    drain = K.energy('c0183a', strength=2.2, hot='ff8090', opacity=.9, soft=0.0, fade_in=0.08, fade_out=0.85)
    drain2 = K.energy('7dff9a', strength=2.2, hot='eaffea', opacity=.9, soft=0.0, fade_in=0.08, fade_out=0.85)
    for k in range(3):
        pts = P.spiral_points(V(0, -0.05, 0.0), 0.95 - k * 0.12, 0.15, -0.5 + k * 0.35, 0.05, 0.8, steps=56,
                              phase=k * 2.0)
        P.ribbon(f'Life drain {k}', pts, [0.085 * math.sin(math.pi * i / 56) + 0.006 for i in range(57)],
                 drain if k % 2 == 0 else drain2, c, up=(0, -0.4, 1))
    xform(list(c.objects), rot=(0, -8, 10))
    P.motes('Drain motes', V(0, -0.1, 0), 14, 0.8, 0.016, K.glow('ff5070', 4.0), c, seed=8)
    return dict(el=10)


@icon('relics', 'relic_stormcaller_sigil', bloom=0.6)
def relic_stormcaller_sigil(K):
    c = K.coll
    slate = K.stone('4f5a66', crack=0.3)
    outline = [(-0.36, -0.55), (0.36, -0.55), (0.42, -0.47), (0.42, 0.42), (0.3, 0.58), (0.0, 0.66), (-0.3, 0.58),
               (-0.42, 0.42), (-0.42, -0.47)]
    tab = geo.extrude('Rune tablet', outline, 0.14, slate, c, plane='XZ', bevel=0.03, segments=3)
    geo.displace_noise(tab, 0.008, 6, seed=2)
    brass = K.gilt('c9a050', 0.26)
    frame = [V(x * 1.04, 0, z * 1.04 + 0.0) for x, z in outline] + [V(outline[0][0] * 1.04, 0, outline[0][1] * 1.04)]
    geo.tube('Tablet frame', [p + V(0, -0.075, 0) for p in frame], 0.022, brass, c, sides=8)
    for x, z in outline[::2]:
        geo.sphere(f'Frame stud {x}{z}', 0.03, (x * 1.04, -0.09, z * 1.04), brass, c, 10, 6)
    bolt_m = K.glow('8fd8ff', 7.0, core='ffffff')
    P.rune('Storm rune', 'bolt', V(0, -0.075, 0.03), V(1, 0, 0), V(0, 0, 1), 0.42, 0.034, bolt_m, c)
    P.halo_sphere('Rune glow', V(0, -0.15, 0.03), 0.42, K.halo('5ab8ff', strength=1.8, opacity=.45, power=1.6), c)
    core = K.beam('bfe8ff', strength=4.0, hot='ffffff', soft=0.3)
    gl = K.beam('4aa8ff', strength=1.6, opacity=.4, soft=1.5)
    rnd = random.Random(3)
    for k, (a, b) in enumerate([(V(-0.42, -0.1, 0.45), V(-0.78, -0.1, 0.7)), (V(0.42, -0.1, 0.2), V(0.82, -0.12, 0.42)),
                                (V(0.36, -0.1, -0.5), V(0.7, -0.15, -0.72)), (V(-0.4, -0.1, -0.3), V(-0.8, -0.1, -0.42)),
                                (V(0.0, -0.1, 0.66), V(0.12, -0.1, 0.98))]):
        pts = P.jag_path(a, b, 6, 0.22, seed=k)
        P.fx_tube(f'Arc core {k}', pts, geo.taper(len(pts), 0.016, 0.004), core, c, sides=6)
        P.fx_tube(f'Arc glow {k}', pts, geo.taper(len(pts), 0.05, 0.015), gl, c, sides=8)
    P.sparks('Static', V(0, -0.1, 0), 14, 0.5, 0.85, 0.05, 0.01, K.spark_mat('bfe8ff', strength=6), c, seed=6)
    xform(list(c.objects), rot=(0, 0, 12))
    return dict(el=10)


@icon('relics', 'relic_siege_hammer', bloom=0.3)
def relic_siege_hammer(K):
    c = K.coll
    lead = K.iron('4c5257', 0.42)
    iron = K.black_iron()
    brass = K.gilt('c08f40', 0.3)
    geo.cylinder('Haft', 0.05, 1.5, (0, 0, -0.15), K.wood('5a3a22'), c, 12, radius2=0.042, bevel=0.01)
    geo.cylinder('Grip', 0.056, 0.36, (0, 0, -0.7), K.leather('3a2416'), c, 12, bevel=0.01)
    for i in range(7):
        P.torus(f'Grip wrap {i}', 0.058, 0.01, K.leather('2a1810'), c, seg=16, sides=6, location=(0, 0, -0.86 + i * 0.05))
    geo.cylinder('Pommel', 0.075, 0.08, (0, 0, -0.92), brass, c, 12, bevel=0.015)
    head = P.block('Maul head', (0.78, 0.36, 0.38), lead, c, location=(0, 0, 0.62), round_=0.1, chip=0.012, seed=3)
    for sx in (-1, 1):
        geo.box(f'Head band {sx}', (0.06, 0.4, 0.42), (sx * 0.2, 0, 0.62), iron, c, bevel=0.015)
        geo.box(f'Face plate {sx}', (0.06, 0.32, 0.34), (sx * 0.4, 0, 0.62), iron, c, bevel=0.02)
        for dy in (-1, 1):
            for dz in (-1, 1):
                geo.sphere(f'Face rivet {sx}{dy}{dz}', 0.022, (sx * 0.435, dy * 0.11, 0.62 + dz * 0.11), brass, c, 10, 6)
        for dz in (-1, 1):
            geo.sphere(f'Band rivet {sx}{dz}', 0.02, (sx * 0.2, -0.2, 0.62 + dz * 0.12), brass, c, 10, 6)
    for sy in (-1, 1):
        geo.box(f'Langet {sy}', (0.03, 0.012, 0.42), (0, sy * 0.052, 0.3), iron, c, bevel=0.005)
    geo.box('Collar', (0.14, 0.14, 0.06), (0, 0, 0.4), brass, c, bevel=0.015)
    xform(list(c.objects), rot=(0, 34, 0))
    xform(list(c.objects), rot=(0, 0, -16))
    sparkles(K, [(V(0.6, -0.3, 0.3), 0.6)], 'fff0d0', size=0.12)
    return dict(el=12)


@icon('relics', 'relic_spectral_lantern', bloom=0.6)
def relic_spectral_lantern(K):
    c = K.coll
    brass = K.gilt('c99a45', 0.26)
    glass = K.L['lamp_glass'] if False else S.glass('a8fff0', name='Ghost glass', glow=0.6)
    n = 6
    R = 0.3
    geo.lathe('Lantern base', [(0, -0.55), (0.38, -0.55), (0.4, -0.5), (0.36, -0.45), (0.33, -0.42), (0, -0.42)], n,
              brass, c, smooth=False)
    geo.lathe('Lantern roof', [(0.38, 0.38), (0.4, 0.42), (0.3, 0.5), (0.16, 0.62), (0.07, 0.72), (0.05, 0.78), (0, 0.78)],
              n, brass, c, smooth=False)
    geo.lathe('Roof cap', [(0, 0.76), (0.06, 0.76), (0.08, 0.82), (0.05, 0.88), (0, 0.9)], 12, brass, c)
    P.torus('Handle ring', 0.12, 0.025, brass, c, seg=32, sides=8, location=(0, 0, 1.0), rotation=(90, 0, 0))
    for i in range(n):
        a = i * math.tau / n
        p = V(math.cos(a) * R * 1.08, math.sin(a) * R * 1.08, 0)
        geo.box(f'Lantern post {i}', (0.045, 0.045, 0.86), p, brass, c, bevel=0.01, rotation=(0, 0, math.degrees(a)))
        a2 = a + math.pi / n
        pane = geo.box(f'Pane {i}', (0.012, 2 * R * math.sin(math.pi / n) * 1.02, 0.78),
                       (math.cos(a2) * R * math.cos(math.pi / n), math.sin(a2) * R * math.cos(math.pi / n), 0), glass, c,
                       bevel=0, rotation=(0, 0, math.degrees(a2)))
        # filigree on each pane
        geo.tube(f'Filigree {i}', catmull([V(0, 0, -0.3), V(0, 0, 0.0), V(0, 0, 0.3)], 3), 0.0001, brass, c)
    for z in (-0.4, 0.37):
        P.torus(f'Band {z}', R * 1.1, 0.025, brass, c, seg=n, sides=6, location=(0, 0, z), rotation=(0, 0, 0))
    ghost = K.energy('6ff0d2', strength=3.0, hot='eafffa', opacity=1.0, soft=0.7, fade_in=0.02, fade_out=0.75)
    P.flame('Soul flame', V(0, 0, -0.38), 0.68, 0.16, ghost, c, seed=2, curl=.4, wobble=.4, twist=2)
    P.flame('Soul flame core', V(0, 0, -0.36), 0.42, 0.08, K.energy('dafff6', strength=4.0, hot='ffffff', soft=0.5), c,
            seed=4, curl=.3)
    P.halo_sphere('Lantern glow', V(0, 0, -0.05), 0.42, K.halo('6ff0d2', strength=2.0, opacity=.55, power=1.4,
                                                                core='dafff6'), c)
    wisp = K.energy('8ff8e0', strength=1.8, hot='eafffa', opacity=.75, soft=0.0, fade_in=0.1, fade_out=0.6)
    for k in range(2):
        pts = P.spiral_points(V(0, 0, 0.5), 0.2, 0.55, 0.0, 0.75, 0.6, steps=40, phase=k * math.pi + 0.5)
        P.ribbon(f'Escaping wisp {k}', pts, [0.06 * math.sin(math.pi * i / 40) + 0.004 for i in range(41)], wisp, c,
                 up=(0, 0, 1))
    aura(K, V(0, 0.25, 0.0), 0.95, '4fd8c0', opacity=.18)
    P.motes('Lantern motes', V(0, 0, 0.3), 14, 0.8, 0.016, K.glow('bfffee', 5.0), c, seed=4)
    xform(list(c.objects), rot=(0, 0, 10))
    return dict(el=14)


# ============================================================================ epics (2)
@icon('relics', 'relic_immortal_wreath', bloom=0.5)
def relic_immortal_wreath(K):
    c = K.coll
    leaf_g = P.enamel('2f7a3e', name='Deathless leaf', rough=0.3)
    leaf_d = P.enamel('1f5a2e', name='Deathless leaf dark', rough=0.35)
    gold = K.gilt('e4b450', 0.22)
    vine = K.wood('3a4a24')
    R = 0.55
    rnd = random.Random(4)
    for k in range(2):
        pts = [V(math.cos(a) * (R + 0.03 * math.sin(a * 9 + k * 3)), math.sin(a) * (R + 0.03 * math.sin(a * 9 + k * 3)),
                 0.03 * math.cos(a * 9 + k * 3)) for a in [i * math.tau / 96 + k * 0.4 for i in range(97)]]
        geo.tube(f'Vine {k}', pts, 0.022, vine, c, sides=8)
    n = 30
    for i in range(n):
        a = i * math.tau / n
        if abs(math.sin(a / 2)) < 0.12:
            continue  # small gap at the front for the bow
        pos = V(math.cos(a) * R, math.sin(a) * R, 0)
        tang = V(-math.sin(a), math.cos(a), 0)
        outw = V(math.cos(a), math.sin(a), 0)
        for side in (-1, 1):
            d = (tang * 1.0 + outw * side * 0.65 + V(0, 0, 0.25)).normalized()
            mat = gold if (i + (side > 0)) % 4 == 0 else (leaf_g if (i + side) % 3 else leaf_d)
            P.leaf(f'Leaf {i}{side}', pos + outw * side * 0.03, d, 0.26 + rnd.uniform(-.03, .04), 0.11, mat, c,
                   normal=(0, 0, 1), bend=0.25, fold=0.35, seg=8)
        if i % 3 == 0:
            geo.sphere(f'Life bud {i}', 0.03, pos + V(0, 0, 0.06) + outw * 0.04, K.glow('fff2c0', 4.0, core='ffffff'), c, 10, 6)
    bow = P.velvet('a01828', name='Bow velvet', sheen=0.4)
    front = V(0, -R, 0.0)
    for sx in (-1, 1):
        loop = catmull([front, front + V(sx * 0.12, -0.04, 0.08), front + V(sx * 0.2, -0.02, 0.0),
                        front + V(sx * 0.1, -0.04, -0.04), front], 5)
        geo.tube(f'Bow loop {sx}', loop, 0.03, bow, c, sides=8, flatten=0.35)
        tail = catmull([front, front + V(sx * 0.06, -0.06, -0.15), front + V(sx * 0.14, -0.05, -0.32)], 5)
        geo.tube(f'Bow tail {sx}', tail, geo.taper(len(tail), 0.035, 0.02), bow, c, sides=8, flatten=0.3)
    geo.sphere('Bow knot', 0.04, front + V(0, -0.03, 0), bow, c, 12, 8)
    xform(list(c.objects), rot=(62, 0, 0))
    P.motes('Life motes', V(0, 0, 0.1), 20, 0.75, 0.016, K.glow('e8ffd0', 5.0), c, seed=2)
    aura(K, V(0, 0.3, 0.0), 0.95, '8aff9a', opacity=.25)
    sparkles(K, [(V(0.45, -0.4, 0.5), 0.8), (V(-0.55, -0.3, -0.35), 0.5)], 'f0ffd8', size=0.12)
    return dict(el=12)


@icon('relics', 'relic_frostbound_crown', bloom=0.55)
def relic_frostbound_crown(K):
    c = K.coll
    silver = K.silver('d8e6f0', 0.18)
    ice = P.ice('e2f8ff', deep='3a8ac0', glow='8fe4ff', strength=1.8, name='Crown ice')
    R, H = 0.52, 0.2
    band('Frost band', R, H, silver, c)
    rnd = random.Random(9)
    n = 11
    for i in range(n):
        a = i * math.tau / n
        out = V(math.sin(a), -math.cos(a), 0)
        front = max(0.0, math.cos(a))
        L = 0.28 + 0.42 * front ** 2 + rnd.uniform(-0.05, 0.08)
        d = (V(0, 0, 1) + out * 0.25 + V(rnd.uniform(-.1, .1), 0, 0)).normalized()
        P.prism(f'Ice spike {i}', out * (R + 0.01) + V(0, 0, H - 0.05), d, L, 0.075 + 0.03 * front, ice, c, sides=5,
                tip=0.55, taper=0.8, seed=i, jitter=0.2, twist=0.3)
        if i % 2 == 0:
            P.prism(f'Ice sliver {i}', out * (R + 0.02) + V(0, 0, H - 0.03), (d + out * 0.6).normalized(), L * 0.55, 0.04,
                    ice, c, sides=5, tip=0.6, seed=i + 40)
    for i in range(6):
        a = i * math.tau / 6
        out = V(math.sin(a), -math.cos(a), 0)
        setting(f'Frost gem {i}', out * (R + 0.05) + V(0, 0, H * 0.5), out, 0.05,
                P.crystal('5ac8ff', deep='0a2a5a', glow=1.6, name='Frost sapphire'), silver, c)
    P.halo_sphere('Frost core', V(0, 0, 0.45), 0.6, K.halo('8fe0ff', strength=1.2, opacity=.25, power=2.0), c)
    P.motes('Snow', V(0, 0, 0.4), 22, 0.85, 0.014, K.glow('eafaff', 4.0), c, seed=3)
    sparkles(K, [(V(0.1, -0.6, 0.95), 1.0), (V(-0.6, -0.3, 0.6), 0.55), (V(0.62, -0.35, 0.4), 0.5)], 'dff6ff', size=0.13)
    return dict(el=16)


@icon('relics', 'relic_moonfire_talisman', bloom=0.55)
def relic_moonfire_talisman(K):
    c = K.coll
    gold = K.gilt('e2ae4a', 0.22)
    outer = [(math.cos(a) * 0.5, math.sin(a) * 0.5) for a in [math.radians(50 + k * 260 / 40) for k in range(41)]]
    ic, ir = (0.2, 0.0), 0.4
    pts_in = []
    for k in range(41):
        a = math.radians(310 - k * 260 / 40)
        pts_in.append((ic[0] + math.cos(a) * ir, ic[1] + math.sin(a) * ir))
    pts_in = [p for p in pts_in if p[0] ** 2 + p[1] ** 2 < 0.49 ** 2]
    cres = geo.extrude('Crescent', outer + pts_in, 0.1, gold, c, plane='XZ', bevel=0.025, segments=3)
    for i, g in enumerate(['sowilo', 'kenaz', 'raido']):
        a = math.radians(150 + i * 30)
        o = V(math.cos(a) * 0.4, -0.052, math.sin(a) * 0.4)
        P.rune(f'Moon rune {i}', g, o, V(1, 0, 0), V(0, 0, 1), 0.04, 0.008, K.glow('ff4a3a', 4.0), c)
    gem = P.crystal('c8102a', deep='300004', glow=1.8, name='Blood moonstone', core='ff7a5a', inner=1.2, coat=0.3)
    P.gem_cut('Moonfire gem', 0.2, gem, c, location=(0.13, -0.06, 0.0), rotation=(90, 0, 0), facets=12, crown=0.42,
              pavilion=0.7)
    P.torus('Gem bezel', 0.21, 0.025, gold, c, seg=40, sides=8, location=(0.13, -0.03, 0.0), rotation=(90, 0, 0))
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        geo.tube(f'Gem prong {k}', [V(0.13 + math.cos(a) * 0.2, -0.04, math.sin(a) * 0.2),
                                    V(0.13 + math.cos(a) * 0.17, -0.12, math.sin(a) * 0.17)], [0.02, 0.012], gold, c, sides=6)
    fire = K.fire(strength=2.0, hot='ffd0a0', mid='ff4a2a', outer='c0102a', smoke='4a0008')
    for i, (a, L) in enumerate([(100, 0.5), (130, 0.36), (70, 0.4), (160, 0.28)]):
        r = math.radians(a)
        P.flame(f'Moonfire {i}', V(math.cos(r) * 0.42, 0.0, math.sin(r) * 0.42), L, 0.07, fire, c, seed=i, curl=.5,
                direction=(math.cos(r) * 0.4, 0, 1))
    bail_and_chain(K, V(0.0, 0, 0.5), gold, spread=0.5, height=0.45)
    P.halo_sphere('Blood glow', V(0.13, -0.1, 0.0), 0.3, K.halo('ff2a3a', strength=1.6, opacity=.45, power=1.6), c)
    aura(K, V(0, 0.3, 0.05), 0.9, 'ff5a2a', opacity=.22)
    xform(list(c.objects), rot=(0, 0, 14))
    return dict(el=10)


# ============================================================================ hardened tier
HARD_EMBER = 'ff4a1a'


def hard_mats(K):
    return dict(iron=K.black_iron(), gilt=K.gilt('ecb24a', 0.2), ember=K.glow(HARD_EMBER, 6.0, core='ffd27a'),
                cracked=S.ember_metal('1e1a1a', 'ff4a14', name='Hardened ember iron', strength=9.0, crack=0.03, scale=5.0))


@icon('relics', 'relic_hardened_bulwark', bloom=0.5)
def relic_hardened_bulwark(K):
    c = K.coll
    H = hard_mats(K)
    mats = kit_mats(K, paint=H['iron'], trim=H['gilt'])
    sh = W.tower_shield(mats, c, width=0.62, height=1.2, ridge=True, emblem=None, face_key='paint', spikes=0)
    for z in (0.5, -0.5):
        for y in (-0.26, 0.26):
            geo.sphere(f'Corner stud {z}{y}', 0.035, (0.05 - 0.08 * (y / 0.31) ** 2, y, z), H['gilt'], c, 12, 8,
                       scale=(.6, 1, 1))
    geo.box('Gilt cross v', (0.03, 0.08, 1.0), (0.07, 0, 0), H['gilt'], c, bevel=0.012)
    geo.tube('Gilt cross h', [V(0.066 - 0.08 * (y / 0.31) ** 2, y, 0.16) for y in [k / 20 * 0.56 - 0.28 for k in range(21)]],
             0.025, H['gilt'], c, sides=6, flatten=0.6)
    geo.lathe('Boss', [(0, 0.16), (0.2, 0.0), (0.17, 0.07), (0.1, 0.12), (0, 0.13)], 32, H['gilt'], c,
              rotation=(0, 90, 0), location=(0.06, 0, 0.16))
    P.gem_cut('Boss gem', 0.09, P.crystal('ff3a1a', deep='3a0400', glow=2.6, name='Ember heart gem', core='ffd070'), c,
              location=(0.2, 0, 0.16), rotation=(0, 90, 0), facets=10)
    for k, z in enumerate((-0.2, -0.36)):
        P.rune(f'Bulwark rune {k}', 'algiz' if k == 0 else 'tiwaz', V(0.065, 0, z), V(0, 1, 0), V(0, 0, 1), 0.07, 0.012,
               H['ember'], c)
    for sy in (-1, 1):
        P.prism(f'Shield spike {sy}', V(0.02, sy * 0.3, 0.6), V(0.3, sy * 0.4, 1), 0.25, 0.04, H['iron'], c, sides=5, tip=0.7)
    xform(list(c.objects), rot=(0, 0, -100))
    xform(list(c.objects), rot=(-4, 0, 0), scale=1.25)
    aura(K, V(0, 0.3, 0.1), 0.95, HARD_EMBER, opacity=.28)
    P.sparks('Embers', V(0, 0, 0.3), 16, 0.4, 0.9, 0.07, 0.012, K.spark_mat('ff7a2a'), c, seed=3, direction=(0, 0, 1),
             spread=1.0)
    return dict(el=10, az=16)


@icon('relics', 'relic_hardened_fang', bloom=0.55)
def relic_hardened_fang(K):
    c = K.coll
    H = hard_mats(K)
    back = [(0.34 * t ** 2.0, 1.1 * t) for t in [k / 24 for k in range(25)]]
    front = [(bx - 0.32 * (1 - t) ** 0.8 - 0.012, bz - 0.06 * (1 - t)) for (bx, bz), t in zip(back, [k / 24 for k in range(25)])]
    outline = back + list(reversed(front))
    bl = geo.extrude('Fang blade', outline, 0.06, H['cracked'], c, plane='XZ', bevel=0.012, segments=2)
    for v in bl.data.vertices:
        t = max(0.0, min(1.0, v.co.z / 1.1))
        bx = 0.34 * t ** 2.0
        w = 0.32 * (1 - t) ** 0.8 + 0.012
        k = max(0.0, min(1.0, (bx - v.co.x) / max(w, 1e-3)))
        v.co.y *= 1 - 0.85 * k ** 1.6
    bl.data.update()
    geo.store_rest(bl)
    geo.place(bl, (0.04, 0, 0.12))
    edge = K.beam('ff6a1a', strength=4.5, hot='ffe0a0', soft=0.3)
    P.fx_tube('Ember edge', [V(fx_ + 0.04, 0, fz + 0.12) for fx_, fz in front[:-1]],
              geo.taper(len(front) - 1, 0.016, 0.006), edge, c, sides=6)
    P.fx_tube('Ember edge glow', [V(fx_ + 0.04, 0, fz + 0.12) for fx_, fz in front[:-1]],
              geo.taper(len(front) - 1, 0.05, 0.02), K.beam('ff4a10', strength=1.6, opacity=.45, soft=1.5), c, sides=8)
    guard = geo.extrude('Fang guard', [(-0.2, -0.03), (0.2, -0.03), (0.26, 0.02), (0.12, 0.06), (-0.12, 0.06),
                                       (-0.26, 0.02)], 0.1, H['gilt'], c, plane='XZ', bevel=0.015)
    geo.place(guard, (-0.04, 0, 0.1))
    grip = geo.tube('Fang grip', [V(-0.04, 0, 0.08), V(-0.06, 0, -0.1), V(-0.1, 0, -0.26)], 0.045, K.bone('d8c8a0'), c,
                    sides=10)
    for i in range(4):
        P.torus(f'Grip band {i}', 0.05, 0.01, H['iron'], c, seg=16, sides=6,
                location=V(-0.045 - i * 0.015, 0, 0.04 - i * 0.08), rotation=(0, -10, 0))
    P.prism('Claw pommel', V(-0.1, 0, -0.26), V(-0.4, 0, -1), 0.16, 0.05, H['gilt'], c, sides=6, tip=0.6)
    P.gem_cut('Guard gem', 0.045, P.crystal('ff3a1a', deep='3a0400', glow=2.4, name='Ember heart gem', core='ffd070'), c,
              location=(-0.04, -0.06, 0.12), rotation=(90, 0, 0), facets=8)
    xform(list(c.objects), loc=(0, 0, -0.4), rot=(0, -30, 0))
    P.sparks('Embers', V(-0.22, -0.05, 0.42), 14, 0.05, 0.4, 0.07, 0.012, K.spark_mat('ff7a2a'), c, seed=8,
             direction=(0, 0, 1))
    return dict(el=10, az=14)


@icon('relics', 'relic_hardened_sigil', bloom=0.55)
def relic_hardened_sigil(K):
    c = K.coll
    H = hard_mats(K)
    geo.lathe('Sigil plate', [(0, -0.06), (0.5, -0.06), (0.54, -0.02), (0.54, 0.03), (0.5, 0.06), (0.42, 0.06),
                              (0.4, 0.03), (0, 0.03)], 8, H['iron'], c, smooth=False, rotation=(0, 0, 22.5))
    P.torus('Gilt rim', 0.46, 0.028, H['gilt'], c, seg=8, sides=8, location=(0, 0, 0.055), rotation=(0, 0, 22.5))
    for i in range(8):
        a = i * math.tau / 8 + math.radians(22.5)
        geo.sphere(f'Sigil stud {i}', 0.035, (math.cos(a) * 0.5, math.sin(a) * 0.5, 0.06), H['gilt'], c, 12, 8,
                   scale=(1, 1, .6))
        P.prism(f'Ray {i}', V(math.cos(a + math.pi / 8) * 0.53, math.sin(a + math.pi / 8) * 0.53, 0),
                V(math.cos(a + math.pi / 8), math.sin(a + math.pi / 8), 0), 0.16, 0.05, H['gilt'], c, sides=4, tip=0.9,
                squash=0.4)
    P.rune('War rune', 'tiwaz', V(0, 0, 0.045), V(1, 0, 0), V(0, 1, 0), 0.26, 0.035, H['ember'], c, depth_axis=V(0, 0, 1))
    P.torus('Inner ring', 0.36, 0.014, H['ember'], c, seg=64, sides=6, location=(0, 0, 0.04))
    objs = list(c.objects)
    xform(objs, rot=(90, 0, 0))
    xform(objs, rot=(-8, 0, -12))
    P.halo_sphere('Rune glow', V(0, -0.15, 0), 0.35, K.halo('ff5a1a', strength=1.6, opacity=.4, power=1.6), c)
    aura(K, V(0, 0.3, 0.0), 0.95, HARD_EMBER, opacity=.28)
    P.sparks('Embers', V(0, -0.05, 0.3), 14, 0.3, 0.8, 0.07, 0.012, K.spark_mat('ff7a2a'), c, seed=4, direction=(0, 0, 1))
    return dict(el=10)


@icon('relics', 'relic_hardened_crown', bloom=0.55)
def relic_hardened_crown(K):
    c = K.coll
    H = hard_mats(K)
    R, Hh = 0.52, 0.22
    band('Ash band', R, Hh, H['iron'], c)
    for z in (0.0, Hh):
        P.torus(f'Gilt lip {z}', R + 0.035, 0.022, H['gilt'], c, seg=64, sides=8, location=(0, 0, z))
    n = 9
    for i in range(n):
        a = i * math.tau / n
        out = V(math.sin(a), -math.cos(a), 0)
        front = max(0.0, math.cos(a))
        L = 0.32 + 0.3 * front ** 2 + (0.08 if i % 2 == 0 else -0.06)
        P.prism(f'Ash spike {i}', out * (R + 0.02) + V(0, 0, Hh - 0.04), (V(0, 0, 1) + out * 0.18).normalized(), L, 0.08,
                H['cracked'], c, sides=4, tip=0.75, taper=0.9, seed=i, jitter=0.1)
        setting(f'Ember gem {i}', out * (R + 0.05) + V(0, 0, Hh * 0.5), out, 0.04,
                P.crystal('ff3a1a', deep='3a0400', glow=2.0, name='Ember gem', core='ffd070'), H['gilt'], c)
    P.sparks('Ash embers', V(0, 0, 0.6), 20, 0.1, 0.8, 0.07, 0.012, K.spark_mat('ff7a2a'), c, seed=5, direction=(0, 0, 1))
    aura(K, V(0, 0.3, 0.3), 0.95, HARD_EMBER, opacity=.28)
    return dict(el=16)


@icon('relics', 'relic_hardened_soul', bloom=0.75, threshold=0.58)
def relic_hardened_soul(K):
    c = K.coll
    H = hard_mats(K)
    core_c = V(0, 0, 0.18)
    geo.sphere('Soul core', 0.17, core_c, K.glow('ffe6a0', 5.0, core='ffffff'), c, 32, 16)
    shell = P.gem_cut('Soul crystal', 0.3, P.crystal('ffb040', deep='5a1400', glow=0.8, name='Soul shell', transmission=0.7,
                                                      inner=0.4, coat=0.4), c, location=core_c - V(0, 0, 0.0),
                      facets=10, crown=0.9, pavilion=0.9, table=0.2)
    P.halo_sphere('Soul glow', core_c, 0.42, K.halo('ffb040', strength=2.4, opacity=.6, power=1.4, core='fff4d0'), c)
    geo.lathe('Soul mount', [(0, -0.7), (0.3, -0.7), (0.33, -0.66), (0.24, -0.6), (0.14, -0.5), (0.1, -0.32), (0.16, -0.22),
                             (0.08, -0.16), (0, -0.15)], 8, H['iron'], c, smooth=False)
    P.torus('Mount gilt', 0.31, 0.022, H['gilt'], c, seg=8, sides=6, location=(0, 0, -0.66), rotation=(0, 0, 22.5))
    P.torus('Collar gilt', 0.15, 0.02, H['gilt'], c, seg=24, sides=6, location=(0, 0, -0.22))
    for i in range(5):
        a = i * math.tau / 5 + 0.3
        d = V(math.cos(a), math.sin(a), 0)
        pts = catmull([V(0, 0, -0.2) + d * 0.1, V(0, 0, -0.12) + d * 0.32, V(0, 0, 0.18) + d * 0.42,
                       V(0, 0, 0.46) + d * 0.3, V(0, 0, 0.56) + d * 0.12], 5)
        geo.tube(f'Cage talon {i}', pts, geo.taper(len(pts), 0.055, 0.01, 0.9), H['iron'], c, sides=8)
        geo.sphere(f'Talon knuckle {i}', 0.05, pts[len(pts) // 2], H['gilt'], c, 12, 8)
    ring_m = K.beam('ffb040', strength=3.0, hot='fff2c0', soft=0.0)
    P.flat_ring('Rune orbit', core_c, V(0.25, -0.35, 1), 0.68, 0.04, ring_m, c, seg=96)
    for i, g in enumerate(['tiwaz', 'algiz', 'sowilo', 'dagaz', 'othala', 'ingwaz']):
        a = i * math.tau / 6
        nrm = V(0.25, -0.35, 1).normalized()
        u = nrm.cross(V(0, 0, 1)).normalized()
        w = nrm.cross(u).normalized()
        p = core_c + (u * math.cos(a) + w * math.sin(a)) * 0.68
        P.rune(f'Orbit rune {i}', g, p, u * math.cos(a + math.pi / 2) + w * math.sin(a + math.pi / 2), nrm, 0.05, 0.01,
               K.glow('ffc860', 5.0), c)
    P.sparks('Soul embers', core_c, 22, 0.35, 0.9, 0.08, 0.012, K.spark_mat('ffb040', hot='fff0c0'), c, seed=6)
    aura(K, V(0, 0.3, 0.1), 1.0, 'ff7a2a', opacity=.35)
    sparkles(K, [(V(0.0, -0.4, 0.62), 1.2), (V(0.6, -0.3, -0.2), 0.6), (V(-0.62, -0.3, 0.4), 0.55)], 'fff0c0', size=0.14)
    return dict(el=12)


# ============================================================================ raid relics
@icon('relics', 'relic_raid_grave_crown', bloom=0.55)
def relic_raid_grave_crown(K):
    from rk import heads
    c = K.coll
    iron = K.black_iron()
    bone = K.bone('d9cdaa')
    soul = K.glow(SOUL, 6.0, core='eafffa')
    R, H = 0.52, 0.2
    band('Grave band', R, H, iron, c)
    P.torus('Bone lip', R + 0.04, 0.025, bone, c, seg=64, sides=8, location=(0, 0, H))
    rnd = random.Random(7)
    n = 10
    for i in range(n):
        a = (i + 0.5) * math.tau / n
        out = V(math.sin(a), -math.cos(a), 0)
        L = rnd.uniform(0.32, 0.55) * (0.6 if i % 3 == 2 else 1.0)
        pts = catmull([out * (R + 0.02) + V(0, 0, H - 0.04), out * (R + 0.05) + V(0, 0, H + L * 0.5),
                       out * (R + 0.11) + V(rnd.uniform(-.04, .04), 0, H + L)], 4)
        geo.tube(f'Bone spike {i}', pts, geo.taper(len(pts), 0.06, 0.006, 1.2), bone, c, sides=7)
    sk = heads.skull_head(V(0, 0, 0), 0.17, dict(bone=bone, dark=K.L['dark'], glow=soul), c, jaw_open=0.0)
    xform(sk, rot=(0, 0, -90))
    xform(sk, loc=(0, -R - 0.12, H * 0.45 + 0.04))
    for i in range(6):
        a = (i + 1) * math.tau / 7
        out = V(math.sin(a), -math.cos(a), 0)
        setting(f'Soul gem {i}', out * (R + 0.05) + V(0, 0, H * 0.5), out, 0.045,
                P.crystal(SOUL, deep='0a3a34', glow=2.0, name='Soul gem'), iron, c)
    rag = K.cloth('5a1424', '2a0810', sheen=0.4)
    for k, a in enumerate((2.3, 3.6)):
        out = V(math.sin(a), -math.cos(a), 0)
        pts = [out * (R + 0.05) + V(0, 0, H * 0.3 - t * 0.5) + out * (0.05 * t) for t in [j / 8 for j in range(9)]]
        P.ribbon(f'Grave rag {k}', pts, [0.12 - 0.06 * (j / 8) for j in range(9)], rag, c, up=out)
    P.halo_sphere('Soul mist', V(0, 0, 0.35), 0.62, K.halo(SOUL, strength=1.2, opacity=.22, power=2.0), c)
    P.motes('Soul motes', V(0, 0, 0.4), 16, 0.75, 0.016, K.glow('bfffee', 5.0), c, seed=6)
    return dict(el=16)


@icon('relics', 'relic_raid_iron_heart', bloom=0.6)
def relic_raid_iron_heart(K):
    c = K.coll
    molten = S.ember_metal('26201e', 'ff5a10', name='Warden heart iron', strength=12.0, crack=0.04, scale=4.5)
    brass = K.gilt('c99a45', 0.28)
    iron = K.black_iron()
    heart = P.heart_shape('Iron heart', 0.46, molten, c, depth=0.7, level=4)
    geo.place(heart, (0, 0, 0.05))
    for k, (z, s) in enumerate([(0.18, 1.0), (-0.14, 0.82)]):
        ring = P.torus(f'Heart band {k}', 0.43 * s, 0.03, iron, c, seg=48, sides=8, location=(0, 0, z),
                       scale=(1.08, 0.72, 1))
        for i in range(10):
            a = i * math.tau / 10
            geo.sphere(f'Band rivet {k}{i}', 0.022, (math.cos(a) * 0.46 * s * 1.08, math.sin(a) * 0.46 * s * 0.72, z),
                       brass, c, 8, 6)
    grille_c = V(0.02, -0.33, 0.0)
    geo.cylinder('Furnace glow', 0.16, 0.04, grille_c + V(0, 0.03, 0), K.glow('ff7a1a', 6.0, core='ffe0a0'), c, 24,
                 rotation=(90, 0, 0), bevel=0)
    P.torus('Grille ring', 0.17, 0.025, brass, c, seg=32, sides=8, location=grille_c, rotation=(90, 0, 0))
    for k in range(4):
        x = -0.12 + k * 0.08
        geo.box(f'Grille bar {k}', (0.02, 0.03, 0.3), grille_c + V(x + 0.0, -0.01, 0), iron, c, bevel=0.005)
    for k, (dx, dy, h, r) in enumerate([(-0.15, 0.05, 0.5, 0.07), (0.05, 0.12, 0.62, 0.08), (0.2, 0.02, 0.42, 0.055)]):
        pts = catmull([V(dx, dy, 0.3), V(dx * 1.2, dy, 0.3 + h * 0.6), V(dx * 1.5 + 0.05, dy, 0.3 + h)], 5)
        geo.tube(f'Aorta pipe {k}', pts, r, brass, c, sides=12)
        P.torus(f'Pipe flange {k}', r * 1.1, r * 0.3, iron, c, seg=20, sides=6, location=pts[-1],
                rotation=aim_rot(pts[-1] - pts[-2]))
    P.halo_sphere('Forge heat', V(0, -0.1, 0.05), 0.55, K.halo('ff6a1a', strength=1.4, opacity=.35, power=1.8), c)
    P.sparks('Forge sparks', V(0, -0.2, 0.3), 18, 0.2, 0.8, 0.08, 0.012, K.spark_mat('ff8a2a'), c, seed=4,
             direction=(0, -0.2, 1), spread=0.8)
    xform(list(c.objects), rot=(0, 0, 12))
    return dict(el=12)


@icon('relics', 'relic_raid_sovereign_mantle', bloom=0.45)
def relic_raid_sovereign_mantle(K):
    c = K.coll
    vel = P.velvet('5a1030', name='Sovereign velvet', sheen=0.45)
    lining = P.velvet('b08a3a', name='Gold lining', sheen=0.3)
    gold = K.gilt('e2b04a', 0.22)
    cl, fn = mantle('Mantle', vel, lining, c, length=1.2, shoulder=0.46, flare=0.34, folds=10, fold_amp=0.04, gap=60,
                    seed=2)
    hem = [V(*fn(u / 60, 1.0)) for u in range(61)]
    geo.tube('Gold hem', hem, 0.026, gold, c, sides=8)
    for sx in (-1, 1):
        edge = [V(*fn(0.0 if sx < 0 else 1.0, 0.2 + 0.8 * v / 24)) for v in range(25)]
        geo.tube(f'Gold edge {sx}', edge, 0.022, gold, c, sides=8)
    collar = geo.lathe('Ermine collar', [(0.12, 0.04), (0.2, 0.1), (0.34, 0.06), (0.47, -0.04), (0.48, -0.12),
                                          (0.36, -0.1), (0.2, -0.04)], 48, S.ermine(), c, close_top=False,
                       close_bottom=False, angle=math.radians(300))
    geo.place(collar, (0, 0.0, 0.0), (0, 0, -60), scale=(1.05, 0.66, 1.0))
    for sx in (-1, 1):
        p = V(*fn(0.0 if sx < 0 else 1.0, 0.24)) + V(0, -0.04, 0)
        geo.lathe(f'Clasp {sx}', [(0, -0.02), (0.08, -0.02), (0.09, 0.0), (0.06, 0.03), (0, 0.035)], 24, gold, c,
                  location=p, rotation=(90, 0, 0))
        P.gem_cut(f'Clasp gem {sx}', 0.045, P.crystal('8a2aff', deep='1a0040', glow=1.8, name='Dominion gem'), c,
                  location=p + V(0, -0.035, 0), rotation=(90, 0, 0), facets=8)
    a, b = V(*fn(0.0, 0.24)) + V(0, -0.06, 0), V(*fn(1.0, 0.24)) + V(0, -0.06, 0)
    P.chain('Clasp chain', catmull([a, (a + b) / 2 + V(0, -0.05, -0.16), b], 8), 0.03, 0.009, gold, c)
    xform(list(c.objects), loc=(0, 0, 0.6), rot=(0, 0, 18))
    aura(K, V(0, 0.35, 0.0), 1.05, '8a3aff', opacity=.3)
    P.motes('Dominion motes', V(0, 0, -0.1), 14, 0.8, 0.016, K.glow('c8a0ff', 4.0), c, seed=2)
    return dict(el=12, az=18)


@icon('relics', 'relic_raid_plague_censer', bloom=0.55)
def relic_raid_plague_censer(K):
    c = K.coll
    brass = K.gilt('d0a048', 0.25)
    dark = K.iron('3a3a38')
    bowl = geo.lathe('Censer bowl', [(0, -0.42), (0.12, -0.42), (0.14, -0.38), (0.1, -0.3), (0.3, -0.18), (0.38, -0.02),
                                     (0.36, 0.04), (0.33, 0.06)], 32, brass, c, close_top=False)
    lid = geo.lathe('Censer lid', [(0.34, 0.04), (0.35, 0.08), (0.3, 0.2), (0.2, 0.32), (0.1, 0.4), (0.06, 0.48),
                                   (0.08, 0.52), (0.04, 0.58), (0, 0.6)], 32, brass, c)
    P.torus('Lid rim', 0.35, 0.025, brass, c, seg=48, sides=8, location=(0, 0, 0.06))
    light = K.glow('c8ff6a', 4.0, core='f4ffd0')
    for row, (z, r, n) in enumerate([(0.16, 0.32, 10), (0.3, 0.2, 7)]):
        for i in range(n):
            a = i * math.tau / n + row * 0.3
            p = V(math.cos(a) * r, math.sin(a) * r, z)
            geo.box(f'Vent {row}{i}', (0.03, 0.03, 0.08 - row * 0.02), p, light, c, bevel=0.01,
                    rotation=(0, -40 + row * 15, math.degrees(a)))
    for i in range(3):
        a = i * math.tau / 3 + 0.5
        p = V(math.cos(a) * 0.34, math.sin(a) * 0.34, 0.07)
        P.torus(f'Chain lug {i}', 0.035, 0.012, brass, c, seg=16, sides=6, location=p, rotation=(90, 0, math.degrees(a) + 90))
        P.chain(f'Censer chain {i}', [p + V(0, 0, 0.03), V(0, 0, 1.2)], 0.035, 0.01, brass, c)
    P.torus('Hang ring', 0.08, 0.02, brass, c, seg=24, sides=8, location=(0, 0, 1.25), rotation=(90, 0, 0))
    P.halo_sphere('Blessed light', V(0, -0.1, 0.2), 0.3, K.halo('d0ff8a', strength=1.2, opacity=.3, power=2.0,
                                                                core='f8ffe0'), c)
    smoke = K.energy('d8ffa0', strength=1.0, hot='f8fff0', opacity=.5, soft=1.2, fade_in=0.1, fade_out=0.6)
    for k in range(3):
        P.flame(f'Blessed smoke {k}', V(-0.05 + k * 0.05, 0, 0.55), 0.6 + k * 0.12, 0.12, smoke, c, seed=k, curl=.6,
                wobble=.7, twist=2.0, lean=(0.25 - k * 0.2, 0.0))
    P.motes('Blessed motes', V(0, 0, 0.4), 18, 0.75, 0.016, K.glow('eaffc0', 5.0), c, seed=3)
    xform(list(c.objects), rot=(0, 10, 8))
    aura(K, V(0, 0.3, 0.2), 1.0, 'c8ff7a', opacity=.2)
    return dict(el=14)


# ============================================================================ tower relics
TOWER_GLOW = '6ff0d2'


def tower_body(K, name, base, h, r, wall, trim, glow, merlons=8):
    c = K.coll
    out = []
    out.append(geo.lathe(name, [(0, base.z), (r * 1.15, base.z), (r * 1.15, base.z + 0.05), (r, base.z + 0.08),
                                (r * 0.94, base.z + h), (r * 1.08, base.z + h + 0.02), (r * 1.08, base.z + h + 0.08),
                                (0, base.z + h + 0.08)], 24, wall, c, location=(base.x, base.y, 0)))
    for i in range(merlons):
        a = i * math.tau / merlons
        out.append(geo.box(f'{name} merlon {i}', (r * 0.4, r * 0.25, r * 0.35),
                           V(base.x + math.cos(a) * r * 0.98, base.y + math.sin(a) * r * 0.98, base.z + h + 0.08 + r * 0.17),
                           trim, c, bevel=0.01, rotation=(0, 0, math.degrees(a) + 90)))
    win = geo.extrude(f'{name} window', [(-r * 0.22, 0), (r * 0.22, 0), (r * 0.22, r * 0.45), (0, r * 0.65),
                                         (-r * 0.22, r * 0.45)], 0.02, glow, c, plane='XZ', bevel=0.004)
    geo.place(win, (base.x, base.y - r * 0.95, base.z + h * 0.55))
    out.append(win)
    return out


@icon('relics', 'relic_tower_sentinel', bloom=0.5)
def relic_tower_sentinel(K):
    c = K.coll
    silver = K.silver('d2d9e0', 0.2)
    gold = K.gilt('e2b04a', 0.22)
    glow = K.glow(TOWER_GLOW, 5.0, core='eafffa')
    tower_body(K, 'Sentinel tower', V(0, 0, -0.55), 0.95, 0.24, silver, gold, glow)
    geo.lathe('Spire', [(0, 0.6), (0.2, 0.6), (0.12, 0.8), (0.04, 1.0), (0, 1.05)], 24, gold, c)
    P.gem_cut('Spire gem', 0.06, P.crystal(TOWER_GLOW, deep='08403a', glow=2.4, name='Sentinel gem'), c,
              location=(0, 0, 1.08), facets=8, pavilion=1.2, crown=0.6)
    for sx in (-1, 1):
        wing = [P.leaf(f'Ward wing {sx} {k}', V(sx * 0.24, 0, 0.15 - k * 0.1), V(sx * (1.0 - k * 0.15), 0, 0.8 - k * 0.35),
                       0.42 - k * 0.07, 0.13, gold, c, normal=(0, -1, 0), bend=-0.1, fold=0.2) for k in range(4)]
    geo.lathe('Sentinel base', [(0, -0.72), (0.34, -0.72), (0.36, -0.66), (0.3, -0.6), (0.3, -0.56), (0, -0.56)], 32,
              gold, c)
    P.halo_sphere('Window glow', V(0, -0.3, -0.0), 0.25, K.halo(TOWER_GLOW, strength=1.6, opacity=.45, power=1.6), c)
    aura(K, V(0, 0.3, 0.15), 1.0, TOWER_GLOW, opacity=.25)
    sparkles(K, [(V(0.0, -0.2, 1.22), 1.0), (V(0.55, -0.3, -0.4), 0.5)], 'dafff6', size=0.13)
    return dict(el=10)


@icon('relics', 'relic_tower_ascendant', bloom=0.5)
def relic_tower_ascendant(K):
    c = K.coll
    gold = K.gilt('e6b44c', 0.2)
    prof = [(1.0, -1.0), (1.0, 1.0), (-0.6, 1.0), (-1.0, 0.6), (-1.0, -0.6), (-0.6, -1.0)]
    P.torus('Signet band', 0.4, 0.1, gold, c, seg=64, profile=[(x * 0.7, y) for x, y in prof], rotation=(90, 0, 0))
    geo.lathe('Signet bezel', [(0, 0.38), (0.26, 0.38), (0.3, 0.44), (0.28, 0.5), (0.24, 0.52), (0, 0.52)], 8, gold, c,
              smooth=False, rotation=(0, 0, 22.5))
    face = geo.cylinder('Signet face', 0.22, 0.02, (0, 0, 0.525), P.enamel('17605a', name='Signet enamel'), c, 8,
                        rotation=(0, 0, 22.5), bevel=0.004)
    P.rune('Tower rune', 'tower', V(0, 0, 0.54), V(1, 0, 0), V(0, 1, 0), 0.16, 0.022, K.glow(TOWER_GLOW, 3.5, core='eafffa'), c,
           depth_axis=V(0, 0, 1))
    for sx in (-1, 1):
        P.gem_cut(f'Shoulder gem {sx}', 0.045, P.crystal(TOWER_GLOW, deep='08403a', glow=1.6, name='Shoulder gem'), c,
                  location=(sx * 0.3, -0.0, 0.36), rotation=(0, sx * 50, 0), facets=8)
    xform(list(c.objects), rot=(50, 0, 18))
    aura(K, V(0, 0.3, 0.3), 0.9, TOWER_GLOW, opacity=.25)
    sparkles(K, [(V(0.25, -0.5, 0.75), 0.9), (V(-0.55, -0.3, -0.3), 0.5)], 'dafff6', size=0.13)
    return dict(el=16)


@icon('relics', 'relic_tower_apex', bloom=0.65)
def relic_tower_apex(K):
    c = K.coll
    gold = K.gilt('ecb24a', 0.2)
    cr = P.crystal('7ff8e2', deep='0a4a40', glow=2.2, name='Apex crystal', inner=1.2)
    P.prism('Apex', V(0, 0, -0.32), V(0, 0, 1), 1.05, 0.4, cr, c, sides=4, tip=0.98, butt=0.0, taper=1.0, twist=0.0)
    P.prism('Apex root', V(0, 0, -0.32), V(0, 0, -1), 0.3, 0.4, cr, c, sides=4, tip=0.98, butt=0.0, taper=1.0)
    corners = [V(math.cos(a) * 0.4, math.sin(a) * 0.4, -0.32) for a in [k * math.pi / 2 for k in range(4)]]
    for k, p in enumerate(corners):
        geo.tube(f'Frame rib {k}', [p * 1.05 + V(0, 0, 0.0), V(0, 0, 0.78)], 0.026, gold, c, sides=8)
        geo.sphere(f'Corner orb {k}', 0.05, p * 1.08, gold, c, 14, 8)
    geo.tube('Girdle', [p * 1.08 for p in corners + corners[:1]], 0.035, gold, c, sides=8)
    geo.sphere('Apex star', 0.05, (0, 0, 0.8), K.glow('eafffa', 8.0), c, 14, 8)
    P.flat_ring('Ascent ring', V(0, 0, -0.1), V(0.1, -0.3, 1), 0.72, 0.035, K.beam(TOWER_GLOW, strength=3, soft=0), c)
    P.flat_ring('Ascent ring 2', V(0, 0, 0.35), V(-0.1, -0.3, 1), 0.5, 0.025, K.beam('ffd77a', strength=3, soft=0), c)
    aura(K, V(0, 0.3, 0.2), 1.0, TOWER_GLOW, opacity=.3)
    sparkles(K, [(V(0, -0.2, 0.84), 1.6), (V(0.55, -0.3, -0.35), 0.55), (V(-0.6, -0.3, 0.1), 0.45)], 'eafffa', size=0.13)
    xform(list(c.objects), rot=(0, 0, 20))
    return dict(el=14)


@icon('relics', 'relic_tower_pinnacle', bloom=0.6)
def relic_tower_pinnacle(K):
    c = K.coll
    gold = K.gilt('ecb24a', 0.2)
    teal = P.crystal(TOWER_GLOW, deep='08403a', glow=1.8, name='Pinnacle gem')
    R, H = 0.52, 0.22
    band('Pinnacle band', R, H, gold, c)
    n = 5
    for i in range(n):
        a = i * math.tau / n
        out = V(math.sin(a), -math.cos(a), 0)
        front = max(0.0, math.cos(a))
        h = 0.22 + 0.22 * front
        p = out * (R + 0.02)
        geo.lathe(f'Turret {i}', [(0, H - 0.02), (0.1, H - 0.02), (0.1, H + h), (0.12, H + h + 0.02), (0.12, H + h + 0.07),
                                  (0, H + h + 0.07)], 16, gold, c, location=(p.x, p.y, 0))
        for k in range(4):
            b = k * math.tau / 4
            geo.box(f'Turret merlon {i}{k}', (0.05, 0.035, 0.05),
                    (p.x + math.cos(b) * 0.1, p.y + math.sin(b) * 0.1, H + h + 0.09), gold, c, bevel=0.006)
        geo.lathe(f'Turret roof {i}', [(0.09, H + h + 0.07), (0.0, H + h + 0.3 + 0.1 * front)], 16,
                  P.enamel('0f4a44', name='Pinnacle enamel'), c, location=(p.x, p.y, 0), close_bottom=True)
        geo.sphere(f'Finial {i}', 0.03, (p.x, p.y, H + h + 0.32 + 0.1 * front), gold, c, 12, 8)
        setting(f'Turret gem {i}', out * (R + 0.11) + V(0, 0, H + h * 0.55), out, 0.035, teal, gold, c)
        b = a + math.pi / n
        out2 = V(math.sin(b), -math.cos(b), 0)
        setting(f'Band gem {i}', out2 * (R + 0.05) + V(0, 0, H * 0.5), out2, 0.06, teal, gold, c, oval=1.2)
    geo.lathe('Center spire', [(0, 0.1), (0.14, 0.1), (0.1, 0.5), (0.05, 0.9), (0, 1.0)], 16, gold, c)
    P.gem_cut('Spire star gem', 0.08, P.crystal('3fe0d0', deep='063a36', glow=0.7, name='Star gem'), c,
              location=(0, 0, 1.02), facets=8, pavilion=1.2, crown=0.6)
    P.halo_sphere('Pinnacle glow', V(0, 0, 1.02), 0.13, K.halo('dafff6', strength=1.6, opacity=.45, power=1.6), c)
    aura(K, V(0, 0.3, 0.45), 1.0, TOWER_GLOW, opacity=.3)
    sparkles(K, [(V(0.0, -0.2, 1.1), 1.4), (V(0.62, -0.3, 0.2), 0.5), (V(-0.6, -0.3, 0.55), 0.45)], 'eafffa', size=0.13)
    return dict(el=16)
