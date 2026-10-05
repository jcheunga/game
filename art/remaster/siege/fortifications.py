"""Rotbound outworks: dressing that flanks the gatehouse in battle.

Each prop shares the gatehouse's materials, camera and scale, so it sits beside it as one fortress.
They are pure scenery at runtime: troops walk past them, nothing targets them.
  fort_tower     round watchtower crowned by a plague-fire beacon
  fort_wall      crenellated curtain segment with pikes and a ruined banner
  fort_palisade  leaning stake barricade lashed with rope, skulls on the tallest stakes
  fort_brazier   soul-fire brazier on a stone pedestal
  fort_totem     bone totem: horned skull, tattered banner, soul light
Every build returns {'anchor': ground point, 'lights': ground points under glows, 'flames': [...]}.
"""
import math
import random

from mathutils import Vector

from rk import core, geo, structures as ST
from rk.structures import Batch, V

from . import gatehouse as G

PROPS = ('fort_tower', 'fort_wall', 'fort_palisade', 'fort_brazier', 'fort_totem')


def build(ident, coll):
    return {'fort_tower': tower, 'fort_wall': curtain, 'fort_palisade': palisade, 'fort_brazier': brazier,
            'fort_totem': totem}[ident](coll, G.mats())


def tower(coll, M):
    rng = random.Random(31)
    B = Batch('Tower ashlar', coll, random.Random(1))
    T = Batch('Tower trim', coll, random.Random(2))
    c, r, h = V(0, 0, 0), 0.46, 2.7
    G.round_tower(B, c, r, 0.0, h, M['stone'], course=G.COURSE, batter=(0.1, 0.7))
    core_ = Batch('Tower core', coll)
    core_.cyl(r - 0.05, h + 0.05, c + V(0, 0, h / 2), M['mortar'], segs=28)
    core_.finish(bevel=0, weighted=False)
    # corbelled parapet and merlons
    for i in range(18):
        a = math.tau * i / 18
        d = V(math.cos(a), math.sin(a), 0)
        for k in range(3):
            T.box((0.09, 0.09 + 0.04 * k, 0.06), c + d * (r + 0.01 + 0.03 * k) + V(0, 0, h - 0.18 + 0.062 * k), M['trim'],
                  rot=(0, 0, math.degrees(a) + 90))
    ST.stone_course_ring(T, c, r + 0.09, h, 0.12, M['trim'], depth=0.16, gap=0.01)
    for i in range(9):
        a0 = math.tau * i / 9 + 0.1
        ST.stone_course_ring(B, c, r + 0.09, h + 0.12, 0.24 if i != 5 else 0.12, M['stone'], count=2, depth=0.14,
                             a0=a0, a1=a0 + math.tau / 9 * 0.58)
    for z, a in ((0.9, -96), (1.8, -70)):
        d = V(math.cos(math.radians(a)), math.sin(math.radians(a)), 0)
        G.slit(T, M, c + d * (r - 0.01) + V(0, 0, z), d, glow=M['plague'])
    B.finish(bevel=0.012)
    T.finish(bevel=0.01)
    # plague-fire beacon on the roof
    objs, _ = ST.brazier('Beacon', coll, c + V(0, 0, h + 0.12), dict(iron=M['dark_iron']), M['plague_fire'],
                         ST.coals(G.PLAGUE, strength=5, name='Beacon embers'), light=core.srgb(G.PLAGUE), size=0.75,
                         height=0.42, light_power=70, seed=3, flame_h=0.85)
    flames = [o for o in objs if 'fire' in o.name]
    for a in (-2.2, -0.9):
        d = V(math.cos(a), math.sin(a), 0)
        G.skull_pike(coll, M, c + d * (r + 0.06) + V(0, 0, h + 0.34), 0.22, 0.055, M['soul'], seed=int(a * 10))
    # banner hanging from the parapet, vines and blight at the foot
    top = c + V(0.04, -r - 0.14, h - 0.12)
    geo.tube('Banner rod', [top + V(-0.16, 0, 0.02), top + V(0.16, 0, 0.02)], 0.013, M['iron'], coll, sides=6)
    ST.banner_sheet('Tower banner', coll, top, 0.27, 1.05, M['cloth'], tails=2, tail_depth=0.2, tatter=1.1, holes=0.1,
                    seed=33, wave=0.02, nx=8, ny=14, bulge=0.02)
    G.growth(coll, M, (0.18, -r - 0.08, 0.04), 0.1, 81)
    _ground_scatter(coll, M, rng, (-0.7, 0.7), (-0.75, -0.5))
    core.point_light('Beacon spill', c + V(0, -0.4, h + 0.9), 25, core.srgb(G.PLAGUE), 0.3, coll)
    return dict(anchor=(0.0, -r - 0.1, 0.0), lights=[(0.0, -r - 0.3, 0.0)], flames=flames)


def curtain(coll, M):
    rng = random.Random(41)
    B = Batch('Curtain ashlar', coll, random.Random(1))
    T = Batch('Curtain trim', coll, random.Random(2))
    x0, x1, y0, y1, h = -1.2, 1.2, -0.3, 0.3, 1.9
    G.wall(B, x0, x1, 0.0, h, y0, M['stone'])
    core_ = Batch('Curtain core', coll)
    core_.box((x1 - x0 - 0.04, y1 - y0 - 0.04, h), (0, 0, h / 2), M['mortar'])
    core_.finish(bevel=0, weighted=False)
    G.wall(B, x0, x1, 0.0, 0.32, y0 - 0.06, M['dark_stone'], depth=0.12)          # footing course
    T.box((x1 - x0 + 0.1, 0.16, 0.1), (0, y0 - 0.04, h + 0.05), M['trim'])
    G.merlons(B, x0, x1, y0 - 0.06, h + 0.1, 0.36, M['stone'], 7, broken=(4,), rng=rng)
    for x in (x0, x1):
        T.box((0.24, y1 - y0 + 0.14, h + 0.22), (x, 0, (h + 0.22) / 2), M['trim'])  # end buttresses
    G.slit(T, M, V(-0.55, y0 - 0.02, 1.15), (0, -1, 0), glow=M['soul'])
    B.finish(bevel=0.012)
    T.finish(bevel=0.01)
    for x, g in ((-0.85, M['plague']), (0.15, M['soul']), (0.95, M['plague'])):
        G.skull_pike(coll, M, (x, y0 - 0.06, h + 0.1), 0.24, 0.055, g, seed=int(x * 10) + 5)
    top = V(0.45, y0 - 0.16, h - 0.08)
    geo.tube('Banner rod', [top + V(-0.18, 0, 0.02), top + V(0.18, 0, 0.02)], 0.013, M['iron'], coll, sides=6)
    ST.banner_sheet('Curtain banner', coll, top, 0.3, 1.2, M['cloth'], tails=2, tail_depth=0.22, tatter=1.2, holes=0.12,
                    seed=43, wave=0.02, nx=8, ny=16, bulge=0.015)
    G.gibbet(coll, M, V(-0.15, y0 - 0.3, h - 0.05), seed=4)
    T2 = Batch('Gibbet arm', coll, random.Random(5))
    T2.box((0.08, 0.36, 0.08), (-0.15, y0 - 0.14, h - 0.02), M['wood'])
    T2.finish(bevel=0.005)
    G.blight_vine(coll, M, (0.7, y0 - 0.035, 0.0), (-0.06, 0, 1), 1.1, 52, thickness=0.024)
    G.growth(coll, M, (0.75, y0 - 0.1, 0.04), 0.1, 83)
    _ground_scatter(coll, M, rng, (-1.2, 1.2), (-0.7, -0.4))
    return dict(anchor=(0.0, y0 - 0.1, 0.0), lights=[], flames=[])


def palisade(coll, M):
    rng = random.Random(51)
    W = Batch('Palisade timber', coll, random.Random(1))
    stakes = []
    for i in range(11):
        x = -1.0 + i * 0.2 + rng.uniform(-0.03, 0.03)
        hh = rng.uniform(0.75, 1.05) * (1.15 if i in (3, 7) else 1.0)
        lean = math.radians(rng.uniform(-8, 8) - 18)        # leaning out toward the approach (-X)
        base = V(x, rng.uniform(-0.04, 0.04), 0.0)
        tip = base + V(math.sin(lean) * hh, -0.12, math.cos(lean) * hh)
        r0 = rng.uniform(0.045, 0.06)
        geo.tube('Stake', [base, base + (tip - base) * 0.82], [r0, r0 * 0.9], M['wood'], coll, sides=7)
        geo.lathe('Stake point', [(r0 * 0.9, 0), (0.0, (tip - base).length * 0.2)], 7, M['wood'], coll,
                  location=base + (tip - base) * 0.82, rotation=_euler_toward(tip - base))
        stakes.append((base, tip))
    for z in (0.3, 0.62):
        a = stakes[0][0] + V(-0.06, -0.08, z)
        b = stakes[-1][0] + V(0.06, -0.12, z - 0.02)
        W.wedge_box(a, b, 0.07, 0.07, 0.06, 0.06, M['wood'])
    W.finish(bevel=0.006)
    L = Batch('Rope lashing', coll, random.Random(4))
    for (base, tip) in stakes[1::3]:
        L.torus(base + (tip - base) * 0.45, 0.066, 0.012, M['rope'], rot=_euler_toward(tip - base))
    L.finish(bevel=0, weighted=False)
    for k, i in enumerate((3, 7)):
        base, tip = stakes[i]
        ST.oriented_skull('Stake skull', coll, tip - (tip - base).normalized() * 0.12, 0.07,
                          G.skull_mats(M, M['soul'] if k else M['plague']), facing=(-0.4, -1, 0), jaw_open=0.4, tilt=-8 + 16 * k)
    for k in range(4):
        p = V(rng.uniform(-0.9, 0.9), rng.uniform(-0.45, -0.2), 0.02)
        a = rng.uniform(0, math.pi)
        geo.tube('Scattered bone', [p - V(math.cos(a), math.sin(a), 0) * 0.08, p + V(math.cos(a), math.sin(a), 0) * 0.08],
                 [0.018, 0.012, 0.018], M['bone_dark'], coll, sides=6)
    _ground_scatter(coll, M, rng, (-1.0, 1.0), (-0.5, -0.15), count=12)
    return dict(anchor=(0.0, -0.18, 0.0), lights=[], flames=[])


def brazier(coll, M):
    T = Batch('Pedestal', coll, random.Random(2))
    base = V(0, 0, 0)
    T.box((0.34, 0.34, 0.4), base + V(0, 0, 0.2), M['trim'], rot=(0, 0, 8))
    T.box((0.42, 0.42, 0.06), base + V(0, 0, 0.43), M['trim'])
    T.box((0.46, 0.46, 0.08), base + V(0, 0, 0.04), M['dark_stone'])
    T.finish(bevel=0.01)
    objs, _ = ST.brazier('Soul brazier', coll, base + V(0, 0, 0.46), dict(iron=M['dark_iron']), M['soul_fire'],
                         ST.coals(G.SOUL, strength=5, name='Soul embers'), light=core.srgb(G.SOUL), size=0.7, height=0.55,
                         light_power=60, seed=6, flame_h=0.75)
    for k, a in enumerate((-2.4, -0.7)):
        ST.oriented_skull('Pedestal skull', coll, (math.cos(a) * 0.32, math.sin(a) * 0.32 - 0.05, 0.06), 0.065,
                          G.skull_mats(M, M['dark']), facing=(math.cos(a), -1, 0), jaw_open=0.3, tilt=-15 + 30 * k)
    G.growth(coll, M, (0.22, -0.2, 0.03), 0.07, 85)
    return dict(anchor=(0.0, -0.23, 0.0), lights=[(0.0, -0.3, 0.0)], flames=[o for o in objs if 'fire' in o.name])


def totem(coll, M):
    rng = random.Random(61)
    pole = V(0, 0, 0)
    geo.tube('Totem pole', [pole, pole + V(0, 0, 2.1)], [0.05, 0.04], M['wood'], coll, sides=8)
    geo.tube('Totem crossbar', [pole + V(-0.32, 0, 1.62), pole + V(0.32, 0, 1.66)], 0.03, M['wood'], coll, sides=6)
    ST.oriented_skull('Totem skull', coll, pole + V(0, -0.05, 2.0), 0.13, G.skull_mats(M, M['soul']), facing=(-0.2, -1, 0),
                      jaw_open=0.45, horns=dict(length=0.9, curl=0.7, thick=0.12, ram=True))
    for sx in (-1, 1):
        ST.chain('Totem chain', coll, pole + V(sx * 0.3, -0.02, 1.62), pole + V(sx * 0.33, -0.02, 1.25), M['iron'], sag=0.0,
                 link=0.03, wire=0.006)
        ST.oriented_skull('Hanging skull', coll, pole + V(sx * 0.33, -0.04, 1.17), 0.06, G.skull_mats(M, M['dark']),
                          facing=(sx * 0.3, -1, 0), jaw_open=0.3, tilt=sx * 12)
    top = pole + V(0, -0.06, 1.55)
    ST.banner_sheet('Totem banner', coll, top, 0.26, 0.85, M['cloth'], tails=2, tail_depth=0.2, tatter=1.4, holes=0.15,
                    seed=63, wave=0.03, nx=8, ny=12, bulge=0.02)
    Bn = Batch('Bone cairn', coll, random.Random(3))
    for k in range(9):
        a = rng.uniform(0, math.tau)
        rr = rng.uniform(0.05, 0.26)
        Bn.box((0.12, 0.09, 0.07), (math.cos(a) * rr, math.sin(a) * rr, 0.04 + rng.uniform(0, 0.06)), M['rubble'],
               rot=(rng.uniform(-20, 20), rng.uniform(-20, 20), rng.uniform(0, 90)))
    Bn.finish(bevel=0.008)
    for k in range(3):
        a = -1.9 + k * 0.8
        ST.oriented_skull('Cairn skull', coll, (math.cos(a) * 0.22, math.sin(a) * 0.18, 0.07), 0.06, G.skull_mats(M, M['dark']),
                          facing=(math.cos(a), -1, 0), jaw_open=0.3, tilt=rng.uniform(-20, 20))
    core.point_light('Totem soul light', pole + V(0, -0.35, 1.9), 10, core.srgb(G.SOUL), 0.2, coll)
    return dict(anchor=(0.0, -0.28, 0.0), lights=[(0.0, -0.32, 0.0)], flames=[])


def _ground_scatter(coll, M, rng, xs, ys, count=18):
    S = Batch('Outwork rubble', coll, random.Random(rng.randint(0, 999)))
    for k in range(count):
        sz = rng.uniform(0.04, 0.1)
        S.box((sz * 1.4, sz, sz * 0.8), (rng.uniform(*xs), rng.uniform(*ys), sz * 0.3), M['rubble'],
              rot=(rng.uniform(-30, 30), rng.uniform(-30, 30), rng.uniform(0, 90)))
    S.finish(bevel=0.01)


def _euler_toward(d):
    q = Vector(d).normalized().to_track_quat('Z', 'Y').to_euler()
    return tuple(math.degrees(a) for a in q)
