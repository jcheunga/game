"""Extra props for battlefield edges and district maps (docks, foundry, ward, chapel, mire, forest, ruins)."""
import math
import random

import bpy
from mathutils import Matrix, Vector

from rk import arch, env, geo
from rk import dressing as P
from rk import shaders as S
from rk.env import C
from rk.palette import PLAGUE, ROT_CRIMSON


def _v(*a):
    return Vector(a)


def _fin(objs, loc=(0, 0, 0), rot=0.0):
    for o in objs:
        if o.type == 'MESH':
            env.finish(o, coll=C['Props'], uv='box' if 'UVMap' not in o.data.uv_layers else None)
    return env.group_xform(objs, loc, rot)


def water_plane(x0, x1, y0, y1, z=-0.25, deep='1d3a40', shallow='4f7a72', name='Water'):
    mat = env.water(name, deep, shallow)
    o = geo.grid_sheet(name, 1, 1, 8, 8, lambda u, v: (x0 + (x1 - x0) * u, y0 + (y1 - y0) * v, z), mat, C['Terrain'])
    return o


def reeds(loc, n=14, h=1.4, seed=0, color='8a8a4a', r=0.5):
    rng = random.Random(seed)
    mat = env.foliage(f'Reeds {color}', '4a5a2a', color, 'b8a860', translucency=0.3, clump=6)
    out = []
    for i in range(n):
        a = rng.uniform(0, math.tau)
        rr = r * math.sqrt(rng.random())
        base = Vector(loc) + _v(math.cos(a) * rr, math.sin(a) * rr, -0.1)
        hh = h * rng.uniform(0.6, 1.2)
        tip = base + _v(rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2), hh)
        out.append(geo.tube('Reed', [base, base.lerp(tip, 0.6) + _v(0, 0, 0.05), tip], [0.02, 0.014, 0.003], mat, None,
                            sides=4))
        if rng.random() < 0.4:
            out.append(geo.sphere('Cattail', 0.035, tuple(base.lerp(tip, 0.8)), P.pm()['wood_dark'], None, 6, 4,
                                  scale=(1, 1, 3)))
    return _fin(out)


def bollard(loc, M=None):
    M = M or P.pm()
    out = [geo.lathe('Bollard', [(0.18, 0), (0.16, 0.5), (0.22, 0.6), (0.18, 0.7), (0, 0.72)], 12, M['iron'], None)]
    return _fin(out, loc)


def rope_coil(loc, r=0.35, M=None):
    M = M or P.pm()
    o = geo.lathe('Rope coil', [(r * 0.5, 0), (r, 0.02), (r, 0.14), (r * 0.5, 0.16)], 16, M['rope'], None,
                  close_top=True, close_bottom=True)
    return _fin([o], loc)


def crane(loc, rot=0.0, M=None, h=7.0):
    """Wooden harbour crane: A-frame mast, jib, treadwheel drum and a hanging hook load."""
    M = M or P.pm()
    out = []
    for s in (-1, 1):
        arch.beam((s * 1.2, 0, 0), (0, 0, h), 0.3, M['wood_dark'], out, 'Crane leg')
    arch.beam((0, -1.2, 0), (0, 0, h * 0.9), 0.26, M['wood_dark'], out, 'Crane back leg')
    arch.beam((0, 0, h), (0, -h * 0.55, h * 0.75), 0.24, M['wood'], out, 'Jib')
    tip = _v(0, -h * 0.55, h * 0.75)
    out.append(geo.tube('Hoist rope', [tip, tip - _v(0, 0, h * 0.4)], 0.02, M['rope'], None, sides=4))
    load = geo.box('Crane load', (1.0, 1.0, 0.8), tuple(tip - _v(0, 0, h * 0.4 + 0.45)), M['planks'], None, bevel=0.02)
    out.append(load)
    drum = geo.cylinder('Treadwheel', 1.4, 0.8, (0.9, 0.4, 1.5), M['wood'], None, 16, rotation=(0, 90, 0), bevel=0.02)
    out.append(drum)
    return _fin(out, loc, rot)


def hull_on_stocks(loc, rot=0.0, L=14.0, M=None, planked=0.55):
    """Ship under construction: keel, curved ribs, partial planking, stocks and scaffold."""
    M = M or P.pm()
    out = []
    beam_w = L * 0.22
    arch.beam((-L / 2, 0, 1.0), (L / 2, 0, 1.0), 0.35, M['wood_dark'], out, 'Keel')
    n = 13
    for i in range(n):
        t = i / (n - 1)
        x = -L / 2 + 0.6 + t * (L - 1.2)
        w = beam_w * math.sin(math.pi * (0.1 + 0.8 * t)) ** 0.6
        pts = []
        for k in range(9):
            a = math.pi * k / 8
            pts.append(_v(x, -math.cos(a) * w, 1.0 + math.sin(a) * w * 0.55 + (abs(math.cos(a)) ** 3) * w * 0.6))
        out.append(geo.tube('Rib', pts, 0.12, M['wood'], None, sides=5))
    # planking on the lower part
    def fn(u, v):
        x = -L / 2 + 0.8 + u * (L - 1.6) * planked
        w = beam_w * math.sin(math.pi * (0.1 + 0.8 * ((x + L / 2 - 0.6) / (L - 1.2)))) ** 0.6
        a = math.pi * (0.15 + 0.25 * v)
        return (x, -math.cos(a) * w * 1.01, 1.0 + math.sin(a) * w * 0.55 + (abs(math.cos(a)) ** 3) * w * 0.6)
    pl = geo.grid_sheet('Planking', 1, 1, 12, 4, fn, env.planks('Hull planks', '6a4a2e', board=0.25, length=4.0),
                        None)
    mod = pl.modifiers.new('Thick', 'SOLIDIFY')
    mod.thickness = 0.06
    out.append(pl)
    for x in (-L / 3, 0, L / 3):
        out.append(geo.box('Stock', (0.6, beam_w * 1.6, 0.8), (x, 0, 0.4), M['wood_dark'], None, bevel=0.03))
    for x in (-L / 2 + 1, L / 2 - 1):
        for s in (-1, 1):
            arch.beam((x, s * (beam_w + 0.8), 0), (x, s * (beam_w + 0.8), beam_w * 1.4), 0.15, M['wood_pale'], out, 'Scaffold')
    out.append(geo.box('Scaffold deck', (L - 1.5, 0.8, 0.08), (0, -(beam_w + 0.8), beam_w * 0.9), M['planks'], None,
                       bevel=0.01))
    return _fin(out, loc, rot)


def boat(loc, rot=0.0, L=5.0, M=None, sunk=0.0, mast=True, sail=None):
    """Small clinker boat (optionally half-sunk and tilted)."""
    M = M or P.pm()
    out = []
    secs = []
    for i in range(9):
        t = i / 8
        x = -L / 2 + t * L
        w = L * 0.18 * math.sin(math.pi * t) ** 0.7 + 0.02
        d = L * 0.1 * math.sin(math.pi * t) ** 0.5 + 0.05
        secs.append([_v(x, -w, 0.5 + 0.06 * abs(t - 0.5) * 4), _v(x, -w * 0.7, 0.5 - d), _v(x, 0, 0.5 - d * 1.2),
                     _v(x, w * 0.7, 0.5 - d), _v(x, w, 0.5 + 0.06 * abs(t - 0.5) * 4)])
    hull = geo.loft('Boat hull', secs, env.planks('Boat planks', '5a4030', board=0.12, length=2.0), None, closed=False,
                    cap=False)
    mod = hull.modifiers.new('Thick', 'SOLIDIFY')
    mod.thickness = 0.05
    out.append(hull)
    if mast:
        out.append(geo.cylinder('Mast', 0.06, L * 0.9, (0, 0, L * 0.45), M['wood_dark'], None, 8, bevel=0))
        if sail is not None:
            sh = geo.grid_sheet('Sail', 1, 1, 6, 6, lambda u, v: (0.1, (u - 0.5) * L * 0.5 + 0.15 * math.sin(v * 3),
                                                                   L * 0.85 - v * L * 0.5), sail, None)
            out.append(sh)
    tilt = Matrix.Rotation(sunk * 0.5, 4, 'X') @ Matrix.Translation((0, 0, -sunk * 0.6))
    for o in out:
        geo.transform(o, tilt)
    return _fin(out, loc, rot)


def rails(x0, x1, y, z=0.0, M=None, gauge=1.0):
    M = M or P.pm()
    out = []
    for s in (-1, 1):
        out.append(geo.box('Rail', (x1 - x0, 0.08, 0.1), ((x0 + x1) / 2, y + s * gauge / 2, z + 0.15), M['iron'], None,
                           bevel=0.01))
    n = int((x1 - x0) / 0.7)
    for i in range(n):
        x = x0 + 0.35 + i * 0.7
        out.append(geo.box('Sleeper', (0.25, gauge + 0.6, 0.12), (x, y, z + 0.06), M['wood_dark'], None, bevel=0.01))
    return _fin(out)


def ore_cart(loc, rot=0.0, M=None, load='coal'):
    M = M or P.pm()
    out = []
    body = geo.lathe('Cart tub', [(0.0, 0.45), (0.55, 0.45), (0.75, 1.2), (0.0, 1.2)], 4, M['iron'], None,
                     close_top=False)
    geo.transform(body, Matrix.Rotation(math.pi / 4, 4, 'Z') @ Matrix.Scale(1.0, 4))
    for v in body.data.vertices:
        v.co.x *= 1.3
    body.data.update()
    out.append(body)
    fill = geo.quadsphere('Ore', 0.6, (0, 0, 1.15), M['coal'] if load == 'coal' else M['stone'], None, level=2,
                          scale=(1.1, 0.8, 0.35))
    geo.displace_noise(fill, 0.08, 5, seed=3)
    out.append(fill)
    for sx in (-0.5, 0.5):
        for sy in (-0.5, 0.5):
            out.append(geo.cylinder('Cart wheel', 0.22, 0.08, (sx, sy, 0.25), M['iron'], None, 12, rotation=(90, 0, 0),
                                    bevel=0.01))
    return _fin(out, loc, rot)


def chimney_stack(loc, h=12.0, r=0.9, M=None, smoke=True, glow=True):
    M = M or P.pm()
    brick = env.masonry('Stack brick', '7a4a34', '5e3a2a', '2a1e18', block=(0.3, 0.12), soot=0.7, dirt_bottom=0.2)
    out = [geo.lathe('Chimney stack', [(r * 1.3, 0), (r * 1.15, h * 0.1), (r, h * 0.95), (r * 1.15, h), (r * 0.9, h)],
                     16, brick, None, close_top=False)]
    env.finish(out[0], uv='cyl')
    res = _fin(out, loc)
    if glow:
        env.point(Vector(loc) + _v(0, 0, h + 0.3), 400, 'ff6a1a', 0.6, name='Stack glow')
    if smoke:
        res += P.smoke_column(Vector(loc) + _v(0, 0, h), height=16, radius=0.9, density=0.6, color='4a4440',
                              drift=(4, 6), name='Stack smoke')
    return res


def furnace(loc, rot=0.0, M=None, w=3.4, h=4.2, power=900):
    """Brick smelting furnace with a glowing arched mouth and a molten spill channel (faces -Y)."""
    M = M or P.pm()
    brick = env.masonry('Furnace brick', '7a4a34', '5e3a2a', '2a1e18', block=(0.3, 0.12), soot=0.6, dirt_bottom=0.2)
    out = []
    body = geo.lathe('Furnace', [(w * 0.55, 0), (w * 0.55, h * 0.55), (w * 0.38, h * 0.85), (w * 0.22, h)], 12, brick,
                     None, close_top=True)
    for v in body.data.vertices:
        v.co.y *= 0.8
    body.data.update()
    env.finish(body, uv='cyl')
    out.append(body)
    mouth = geo.box('Furnace mouth', (w * 0.45, 0.2, h * 0.3), (0, -w * 0.43, h * 0.2), M['ember'], None, bevel=0.03)
    out.append(mouth)
    chan = geo.box('Molten channel', (w * 1.4, 0.4, 0.06), (0, -w * 0.43 - 0.25, 0.03),
                   env.glow_mat('ff7a1a', 4.0, name='Molten metal', core_color='ffd080'), None, bevel=0)
    out.append(chan)
    res = _fin(out, loc, rot)
    mw = Matrix.Translation(Vector(loc)) @ Matrix.Rotation(rot, 4, 'Z')
    env.point(mw @ _v(0, -w * 0.6, h * 0.25), power, 'ff6a1a', 0.5, name='Furnace glow')
    return res


def slag_heap(loc, r=1.6, seed=0):
    mat = S.ember_metal('2a2420', 'ff5a10', name='Slag', strength=3.0, crack=0.012, scale=7.0)
    o = geo.quadsphere('Slag heap', 1.0, (0, 0, 0), mat, None, level=3, scale=(r, r * 0.8, r * 0.45))
    for v in o.data.vertices:
        if v.co.z < 0:
            v.co.z *= 0.05
    o.data.update()
    geo.displace_noise(o, r * 0.15, 2.0 / r, seed=seed)
    return _fin([o], loc)


def palisade(p0, p1, h=3.2, M=None, seed=0, height_fn=None):
    M = M or P.pm()
    rng = random.Random(seed)
    out = []
    p0, p1 = Vector(p0).to_3d(), Vector(p1).to_3d()
    d = p1 - p0
    n = max(1, int(d.length / 0.32))
    hf = height_fn or (lambda x, y: 0.0)
    for i in range(n + 1):
        c = p0 + d * (i / n)
        hh = h * rng.uniform(0.9, 1.08)
        base = c + _v(0, 0, hf(c.x, c.y) - 0.3)
        out.append(geo.tube('Stake', [base, base + _v(0, 0, hh - 0.3), base + _v(0, 0, hh)], [0.15, 0.15, 0.01],
                            M['wood_dark'], None, sides=7))
    u = d.normalized()
    for z in (0.8, h * 0.7):
        arch.beam(p0 + _v(0, 0, z), p1 + _v(0, 0, z), 0.12, M['wood'], out, 'Palisade rail')
    return _fin(out)


def cheval(loc, rot=0.0, L=3.0, M=None):
    """Cheval-de-frise barricade: a log bristling with crossed sharpened stakes."""
    M = M or P.pm()
    out = [geo.cylinder('Cheval log', 0.16, L, (0, 0, 0.55), M['wood_dark'], None, 10, rotation=(0, 90, 0), bevel=0.01)]
    n = int(L / 0.45)
    for i in range(n):
        x = -L / 2 + 0.25 + i * 0.45
        for s in (-1, 1):
            a = math.radians(40) * s
            d = _v(0, math.sin(a), math.cos(a))
            base = _v(x, 0, 0.55) - d * 0.7
            out.append(geo.tube('Spike stake', [base, base + d * 1.5], [0.05, 0.005], M['wood'], None, sides=5))
    return _fin(out, loc, rot)


def pew(loc, rot=0.0, w=2.6, M=None, broken=0.0):
    M = M or P.pm()
    out = []
    out.append(geo.box('Pew seat', (w, 0.45, 0.07), (0, 0, 0.45), M['wood_dark'], None, bevel=0.01))
    out.append(geo.box('Pew back', (w, 0.07, 0.6), (0, 0.22, 0.8), M['wood_dark'], None, bevel=0.01))
    for x in (-w / 2, w / 2):
        out.append(geo.box('Pew end', (0.08, 0.5, 1.05), (x, 0.0, 0.52), M['wood'], None, bevel=0.02))
    res = _fin(out, loc, rot)
    if broken:
        for o in res:
            geo.transform(o, Matrix.Translation(Vector(loc)) @ Matrix.Rotation(broken, 4, 'Y') @
                          Matrix.Translation(-Vector(loc)))
    return res


def bone_wall(p0, p1, h=3.0, seed=0, M=None):
    """Ossuary wall: stone with niches packed with skulls and long bones."""
    M = M or P.pm()
    rng = random.Random(seed)
    out = []
    p0, p1 = Vector(p0).to_3d(), Vector(p1).to_3d()
    d = p1 - p0
    L = d.length
    u = d.normalized()
    nrm = _v(u.y, -u.x, 0)  # faces the camera (-Y) for walls drawn left to right
    stone = env.masonry('Ossuary stone', '6e665a', '5a544a', '2e2a26', block=(0.5, 0.3), moss=0.2)
    arch.slab(p0 + d / 2 + _v(0, 0, h / 2) - nrm * 0.3, u, (0, 0, 1), L, h, 0.6, stone, out, 'Ossuary wall')
    rows = int(h / 0.32)
    for r in range(rows):
        z = 0.25 + r * 0.32
        n = int(L / 0.3)
        for i in range(n):
            c = p0 + u * (0.15 + i * 0.3) + nrm * 0.02 + _v(0, 0, z)
            if r % 3 == 1:
                out.append(geo.tube('Stacked bone', [c - u * 0.14, c + u * 0.14], [0.05, 0.035, 0.05], M['bone'], None,
                                    sides=6))
            elif rng.random() < 0.92:
                out.append(geo.quadsphere('Ossuary skull', 0.12, tuple(c), M['bone'], None, level=2,
                                          scale=(0.85, 0.95, 0.85)))
                for s in (-1, 1):
                    out.append(geo.sphere('Socket', 0.03, tuple(c + u * (s * 0.04) + nrm * 0.1 + _v(0, 0, 0.01)),
                                          M['coal'], None, 6, 4))
    return _fin(out)


def mushrooms(loc, n=6, seed=0, color='9ff5d8', glow=4.0, r=0.4):
    rng = random.Random(seed)
    stem = S.flat('cfc6b0', name='Mushroom stem', rough=0.6)
    cap = env.mat_cached(('mushcap', color), lambda: S.emissive(color, glow, name=f'Glowcap {color}', base='3a4a44'))
    out = []
    for i in range(n):
        a = rng.uniform(0, math.tau)
        rr = r * math.sqrt(rng.random())
        c = Vector(loc) + _v(math.cos(a) * rr, math.sin(a) * rr, 0)
        hh = rng.uniform(0.08, 0.25)
        out.append(geo.cylinder('Stem', 0.02, hh, tuple(c + _v(0, 0, hh / 2)), stem, None, 6, bevel=0))
        out.append(geo.lathe('Cap', [(0, hh + 0.06), (0.07, hh + 0.03), (0.08, hh)], 8, cap, None,
                             location=tuple(c)))
    return _fin(out)


def log_stack(loc, rot=0.0, n=9, L=4.0, r=0.22, M=None):
    M = M or P.pm()
    out = []
    rows = [4, 3, 2]
    for k, cnt in enumerate(rows):
        for i in range(cnt):
            y = (i - (cnt - 1) / 2) * r * 2.05
            z = r + k * r * 1.75
            out.append(geo.cylinder('Log', r, L, (0, y, z), env.bark(), None, 10, rotation=(0, 90, 0), bevel=0.02))
            for s in (-1, 1):
                out.append(geo.cylinder('Log end', r * 0.95, 0.02, (s * L / 2, y, z), M['wood_pale'], None, 10,
                                        rotation=(0, 90, 0), bevel=0))
    return _fin(out, loc, rot)


def stump(loc, r=0.35, M=None):
    M = M or P.pm()
    out = [geo.cylinder('Stump', r, 0.5, (0, 0, 0.2), env.bark(), None, 12, bevel=0.03),
           geo.cylinder('Stump top', r * 0.92, 0.02, (0, 0, 0.45), M['wood_pale'], None, 12, bevel=0)]
    return _fin(out, loc)


def rubble(loc, r=2.0, n=14, seed=0, mat=None):
    rng = random.Random(seed)
    mat = mat or env.masonry('Rubble', '8e867a', '766e62', '4a443c', block=(0.5, 0.3))
    out = []
    for i in range(n):
        a = rng.uniform(0, math.tau)
        rr = r * math.sqrt(rng.random())
        s = rng.uniform(0.25, 0.6)
        o = geo.box('Rubble block', (s * 1.4, s, s * 0.7), (0, 0, 0), mat, None, bevel=0.05)
        geo.transform(o, Matrix.Translation(Vector(loc) + _v(math.cos(a) * rr, math.sin(a) * rr, s * 0.3 * (1 - rr / r) + 0.1)) @
                      Matrix.Rotation(rng.uniform(0, 6.28), 4, 'Z') @ Matrix.Rotation(rng.uniform(-0.5, 0.5), 4, 'X'))
        out.append(o)
    return _fin(out)


def prayer_flags(p0, p1, sag=0.5, count=12):
    M = P.pm()
    cols = [M['teal'], M['canvas'], M['crimson'], env.mat_cached(('pf', 'gold'), lambda: S.cloth('d8b040', name='Flag gold'))]
    return P.bunting(p0, p1, sag=sag, count=count, colors=cols, size=0.32)


def bell_frame(loc, rot=0.0, M=None, h=3.2):
    M = M or P.pm()
    out = []
    for s in (-1, 1):
        arch.beam((s * 1.0, 0, 0), (s * 0.8, 0, h), 0.22, M['wood_dark'], out, 'Bell post')
    arch.beam((-1.1, 0, h), (1.1, 0, h), 0.24, M['wood_dark'], out, 'Bell beam')
    bell = geo.lathe('Bell', [(0, h - 0.1), (0.25, h - 0.2), (0.32, h - 0.6), (0.45, h - 0.95), (0.42, h - 1.0)], 16,
                     S.gold('8a6a34', name='Bell bronze', rough=0.35), None, close_top=True, close_bottom=False)
    out.append(bell)
    roof = geo.extrude('Bell roof', [(-1.0, 0), (0, 0.6), (1.0, 0)], 2.6,
                       env.roof_tiles('Bell shingles', '5a4a3a', '4a3a2c', tile=(0.2, 0.14), curve=False), None,
                       plane='YZ', bevel=0.01)
    geo.place(roof, (0, 0, h + 0.1), rotation=(0, 0, 90))
    out.append(roof)
    return _fin(out, loc, rot)


def vat(loc, r=0.8, M=None, liquid='9cf25c', power=120.0):
    """Alchemical vat with glowing brew and a copper still coil."""
    M = M or P.pm()
    out = []
    out.append(geo.lathe('Vat', [(0, 0), (r, 0.05), (r * 1.05, 1.0), (r, 1.1)], 16, P.pm()['planks'], None,
                         close_top=False))
    env.finish(out[-1], uv='cyl')
    out.append(geo.cylinder('Brew', r * 0.95, 0.03, (0, 0, 0.95), env.glow_mat(liquid, 3.0, name=f'Brew {liquid}'), None, 16,
                            bevel=0))
    for z in (0.25, 0.75):
        out.append(geo.lathe('Vat band', [(r * 1.03, z - 0.04), (r * 1.08, z), (r * 1.03, z + 0.04)], 16, M['iron'], None,
                             close_top=False, close_bottom=False))
    copper = S.gold('b86a3a', name='Copper', rough=0.3)
    coil = [Vector((math.cos(t) * 0.25 + r + 0.4, math.sin(t) * 0.25, 0.2 + t * 0.12)) for t in [i * 0.5 for i in range(20)]]
    out.append(geo.tube('Still coil', coil, 0.035, copper, None, sides=6))
    res = _fin(out, loc)
    if power:
        env.point(Vector(loc) + _v(0, 0, 1.6), power, liquid, 0.4, name='Vat glow')
    return res


def alembic_table(loc, rot=0.0, M=None, seed=0):
    M = M or P.pm()
    rng = random.Random(seed)
    out = P.table((0, 0, 0), 0.0, 2.0, 0.8, 0.85, M)
    glass = S.glass('cfe8d0', name='Lab glass', glow=0.2)
    liquids = ['9cf25c', '6ff0d2', 'c23a4a', 'e0b040']
    for i in range(5):
        x = -0.75 + i * 0.37
        c = liquids[rng.randrange(len(liquids))]
        prof = [(0, 0), (0.08, 0.0), (0.12, 0.08), (0.11, 0.18), (0.03, 0.28), (0.03, 0.42)]
        fl = geo.lathe('Flask', prof, 12, glass, None, close_top=False, location=(x, 0, 0.85))
        out.append(fl)
        out.append(geo.lathe('Flask brew', [(0, 0.01), (0.07, 0.01), (0.1, 0.08), (0.09, 0.14), (0, 0.14)], 12,
                             env.glow_mat(c, 2.0, name=f'Potion {c}'), None, location=(x, 0, 0.85)))
    return _fin(out, loc, rot)


def ward_sigil(loc, r=1.2, color='9cf25c', power=60.0):
    """Glowing ring sigil on the ground (purge wards / witch circles)."""
    mat = env.glow_mat(color, 4.0, name=f'Sigil {color}')
    out = []
    for rr in (r, r * 0.8):
        o = geo.lathe('Sigil ring', [(rr - 0.03, 0.02), (rr + 0.03, 0.02)], 48, mat, None, close_top=False,
                      close_bottom=False)
        out.append(o)
    for k in range(6):
        a = k * math.tau / 6
        p0 = _v(math.cos(a) * r * 0.8, math.sin(a) * r * 0.8, 0.02)
        p1 = _v(math.cos(a + 2.1) * r * 0.8, math.sin(a + 2.1) * r * 0.8, 0.02)
        out.append(geo.box('Sigil stroke', ((p1 - p0).length, 0.05, 0.01), (0, 0, 0), mat, None, bevel=0))
        q = Vector((1, 0, 0)).rotation_difference((p1 - p0).normalized())
        geo.transform(out[-1], Matrix.Translation((p0 + p1) / 2) @ q.to_matrix().to_4x4())
    res = _fin(out, loc)
    if power:
        env.point(Vector(loc) + _v(0, 0, 0.5), power, color, r * 0.5, name='Sigil glow')
    return res


def iron_door(loc, rot=0.0, w=2.4, h=3.4, M=None, glow=PLAGUE):
    """Sealed vault door with chains and a glowing seal (faces -Y)."""
    M = M or P.pm()
    out = []
    out.append(geo.box('Vault door', (w, 0.3, h), (0, 0, h / 2), M['iron'], None, bevel=0.03))
    for z in (h * 0.3, h * 0.7):
        out.append(geo.box('Door band', (w + 0.1, 0.36, 0.14), (0, 0, z), M['iron'], None, bevel=0.02))
    for s in (-1, 1):
        pts = [_v(s * w * 0.45, -0.2, h * 0.85), _v(0, -0.28, h * 0.5), _v(-s * w * 0.45, -0.2, h * 0.15)]
        out.append(geo.tube('Chain', geo.bezier_points(*pts, steps=10), 0.04, M['iron'], None, sides=5))
    seal = geo.cylinder('Seal', 0.35, 0.08, (0, -0.2, h * 0.5), env.glow_mat(glow, 4.0, name=f'Seal {glow}'), None, 16,
                        rotation=(90, 0, 0), bevel=0.01)
    out.append(seal)
    res = _fin(out, loc, rot)
    mw = Matrix.Translation(Vector(loc)) @ Matrix.Rotation(rot, 4, 'Z')
    env.point(mw @ _v(0, -0.8, h * 0.5), 60, glow, 0.3, name='Seal glow')
    return res


def gothic_pier(loc, h=9.0, r=0.55, mat=None):
    """Clustered nave pier: core column with four engaged shafts."""
    mat = mat or env.smooth_stone('a49a88', name='Nave stone')
    out = []
    out.append(geo.lathe('Pier core', [(r * 1.5, 0), (r * 1.4, 0.4), (r, 0.6), (r, h - 0.6), (r * 1.4, h - 0.3),
                                       (r * 1.5, h)], 16, mat, None))
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        out.append(geo.cylinder('Shaft', r * 0.32, h - 1.0, (math.cos(a) * r, math.sin(a) * r, h / 2), mat, None, 10,
                                bevel=0))
    return _fin(out, loc)


def candle_cluster(loc, n=7, seed=0, power=25.0):
    rng = random.Random(seed)
    out = []
    for i in range(n):
        a = rng.uniform(0, math.tau)
        rr = rng.uniform(0, 0.35)
        out += P.candle(Vector(loc) + _v(math.cos(a) * rr, math.sin(a) * rr, 0), rng.uniform(0.15, 0.45), 0.035)
    env.point(Vector(loc) + _v(0, 0, 0.6), power, 'ffb45a', 0.2, name='Candles')
    return out


def cairn(loc, h=1.1, seed=0, mat=None):
    """Stacked prayer stones."""
    rng = random.Random(seed)
    mat = mat or env.field_stone(0.3, '8a8478')
    out = []
    z = 0.0
    r = 0.42
    while z < h:
        s = r * rng.uniform(0.85, 1.1)
        o = geo.quadsphere('Cairn stone', 1.0, (0, 0, 0), mat, None, level=2, scale=(s, s * 0.85, s * 0.5))
        geo.displace_noise(o, 0.05, 4, seed=seed * 10 + int(z * 10))
        geo.place(o, Vector(loc) + _v(rng.uniform(-0.05, 0.05), rng.uniform(-0.05, 0.05), z + s * 0.4))
        out.append(o)
        z += s * 0.8
        r *= 0.8
    return _fin(out)


def shrine_altar(loc, rot=0.0, M=None, seed=0):
    M = M or P.pm()
    st = env.smooth_stone('a8a49a', name='Shrine stone', moss=0.2)
    out = [geo.box('Altar', (1.6, 0.8, 0.9), (0, 0, 0.45), st, None, bevel=0.05),
           geo.box('Altar top', (1.8, 0.95, 0.12), (0, 0, 0.96), st, None, bevel=0.03)]
    res = _fin(out, loc, rot)
    res += candle_cluster(Vector(loc) + _v(0, 0, 1.02), 6, seed=seed, power=18)
    return res
