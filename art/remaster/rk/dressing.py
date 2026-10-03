"""Set dressing for the menu dioramas: market wares, caravan wagons, tents, lanterns,
camp/forge/library furniture, treasure and grave goods. Built at the origin, then
baked to a placement with env.group_xform. Each builder returns a list of objects."""
import math
import random

import bpy
from mathutils import Matrix, Vector

from . import env, geo
from . import shaders as S
from . import weapons as W
from .arch import beam, slab
from .env import C
from .palette import AMBER, BRASS, LANTERN_TEAL, LANTERN_TEAL_DARK, PLAGUE, ROT_CRIMSON


def _v(*a):
    return Vector(a)


_M = {}


def pm():
    """Prop material set (cached per scene)."""
    try:
        stale = not _M or any(k not in ('_scene', 'books') and m.name not in bpy.data.materials for k, m in _M.items())
    except ReferenceError:  # materials freed by a scene reset
        stale = True
    if stale:
        _M.clear()
        _M['_scene'] = bpy.context.scene
        _M.update(dict(
            wood=S.wood('6b4a2e', name='Prop oak', scale=0.8),
            wood_dark=S.wood('3e2b1c', name='Dark oak', scale=0.8, dark=0.5),
            wood_pale=S.wood('9a7a52', name='Pale pine', scale=0.8),
            planks=env.planks('Prop planks', '6e4c30', board=0.18, length=1.6),
            iron=S.metal('3f4246', name='Prop iron', rough=0.45, wear=0.15),
            brass=S.gold(BRASS, name='Prop brass'),
            gold=S.gold('f2c460', name='Coin gold', rough=0.3),
            canvas=S.cloth('d6c8a8', name='Canvas', var='a8987a', weave=30),
            teal=S.cloth(LANTERN_TEAL, name='Caravan teal', var=LANTERN_TEAL_DARK, weave=30),
            crimson=S.cloth(ROT_CRIMSON, name='Crimson cloth', var='3c0f18', weave=30),
            burlap=S.cloth('a08a62', name='Burlap', var='7a6646', weave=50),
            rope=S.rope(),
            leather=S.leather('5a3b24', name='Prop leather'),
            stone=env.smooth_stone('8a8478', name='Prop stone'),
            bone=S.bone(),
            parchment=_parchment(),
            wax=S.flat('e8dcc0', name='Candle wax', rough=0.5),
            flame=env.flame_mat('Flame', 1.8),
            lamp_glass=S.glass('ffc070', name='Lamp glass', glow=1.6),
            ember=S.ember_metal('2a2420', 'ff6a1a', name='Embers', strength=14.0, crack=0.05, scale=4.0),
            coal=S.flat('151210', name='Coal', rough=0.9),
            fruit_r=S.flat('a8322a', name='Apples', rough=0.4),
            fruit_y=S.flat('d9a83a', name='Pears', rough=0.45),
            green=S.flat('5d7a30', name='Greens', rough=0.7),
            clay=S.stone('a8643e', name='Terracotta', scale=1.5, edge=1.1, dark=0.6),
            plague=env.glow_mat(PLAGUE, 6.0, name='Plague glow', core_color='f4ffd0'),
            soul=env.glow_mat('6ff0d2', 6.0, name='Soul glow', core_color='e8fff8'),
        ))
    return _M


def _parchment():
    """Aged paper with faint ink lines (horizontal in world space) and foxed edges."""
    from .nodekit import material
    m, n = material('Parchment')
    p = n.rest()
    a = n.noise(p, 6.0, 4, 0.6)
    col = n.mixc(n.maprange(a, 0.3, 0.7), 'dccb9c', 'a88a5a')
    _, _, z = n.xyz(p)
    lines = n.smooth(n.math('ABSOLUTE', n.math('SINE', n.mul(z, 110.0))), 0.82, 0.95)
    gaps = n.smooth(n.noise(n.vmath('MULTIPLY', p, (9.0, 9.0, 0.5)), 3.0, 2, 0.5), 0.35, 0.5)
    ink = n.mul(lines, gaps)
    col = n.mixc(n.mul(ink, 0.55), col, '3a2a1c')
    n.principled(**{'Base Color': col, 'Roughness': 0.85})
    return m


def _fin(objs, loc, rot):
    for o in objs:
        if o.type == 'MESH' and 'lc' not in o.data.attributes:
            env.store_local(o)
    return env.group_xform(objs, loc, rot)


def _p(o, out, coll='Props', uv='box'):
    env.finish(o, coll=C[coll], uv=uv)
    out.append(o)
    return o


# ------------------------------------------------------------------ containers


def barrel(loc=(0, 0, 0), rot=0.0, h=0.95, r=0.32, M=None, lying=False):
    M = M or pm()
    out = []
    prof = [(0, 0), (r * 0.82, 0), (r * 0.86, 0.02)]
    for i in range(1, 8):
        t = i / 8
        prof.append((r * (0.86 + 0.14 * math.sin(math.pi * t)), h * t))
    prof += [(r * 0.86, h - 0.02), (r * 0.82, h), (0, h - 0.03)]
    b = geo.lathe('Barrel', prof, 20, M['wood'], C['Props'])
    _p(b, out, uv='cyl')
    for z in (0.12, 0.3, h - 0.3, h - 0.12):
        rr = r * (0.86 + 0.14 * math.sin(math.pi * z / h)) + 0.012
        hoop = geo.lathe('Hoop', [(rr, z - 0.03), (rr + 0.006, z), (rr, z + 0.03)], 20, M['iron'], C['Props'],
                         close_top=False, close_bottom=False)
        _p(hoop, out, uv='cyl')
    if lying:
        for o in out:
            geo.transform(o, Matrix.Translation((0, 0, r)) @ Matrix.Rotation(math.pi / 2, 4, 'Y') @
                          Matrix.Translation((0, 0, -h / 2)))
    return _fin(out, loc, rot)


def crate(loc=(0, 0, 0), rot=0.0, s=0.7, M=None, lid=True):
    M = M or pm()
    out = []
    _p(geo.box('Crate', (s, s, s), (0, 0, s / 2), M['planks'], C['Props'], bevel=0.015), out)
    t = 0.07
    for z in (t / 2 + 0.005, s - t / 2 - 0.005):
        for sy in (-1, 1):
            _p(geo.box('Slat', (s + 0.02, 0.03, t), (0, sy * (s / 2 + 0.005), z), M['wood_dark'], C['Props'], bevel=0.008),
               out)
            _p(geo.box('Slat', (0.03, s + 0.02, t), (sy * (s / 2 + 0.005), 0, z), M['wood_dark'], C['Props'], bevel=0.008),
               out)
    return _fin(out, loc, rot)


def sack(loc=(0, 0, 0), rot=0.0, s=0.55, M=None, seed=0, open_top=False, fill=None):
    M = M or pm()
    out = []
    o = geo.quadsphere('Sack', s * 0.5, (0, 0, 0), M['burlap'], C['Props'], level=3, scale=(1.0, 0.85, 1.25))
    for v in o.data.vertices:
        if v.co.z < -s * 0.3:
            v.co.z = -s * 0.3 + (v.co.z + s * 0.3) * 0.25
        if v.co.z > s * 0.45:
            v.co.x *= 0.55
            v.co.y *= 0.55
    o.data.update()
    geo.displace_noise(o, s * 0.05, 4.0 / s, seed=seed)
    geo.place(o, (0, 0, s * 0.3 + s * 0.08))
    _p(o, out)
    if fill is not None:
        _p(geo.quadsphere('Sack grain', s * 0.3, (0, 0, s * 0.95), fill, C['Props'], level=2, scale=(1, 1, 0.35)), out)
    return _fin(out, loc, rot)


def pot(loc=(0, 0, 0), rot=0.0, h=0.6, M=None, mat=None, kind='amphora'):
    M = M or pm()
    out = []
    if kind == 'amphora':
        prof = [(0, 0), (0.1, 0.0), (0.2, h * 0.25), (0.24, h * 0.5), (0.17, h * 0.8), (0.07, h * 0.88),
                (0.07, h * 0.98), (0.09, h)]
    else:
        prof = [(0, 0), (0.15, 0), (0.24, h * 0.35), (0.22, h * 0.75), (0.15, h * 0.9), (0.17, h)]
    _p(geo.lathe('Pot', prof, 18, mat or M['clay'], C['Props'], close_top=False), out, uv='cyl')
    return _fin(out, loc, rot)


def chest(loc=(0, 0, 0), rot=0.0, w=1.0, d=0.6, h=0.55, M=None, open_lid=0.0, gold=True, wood=None):
    M = M or pm()
    out = []
    _p(geo.box('Chest', (w, d, h * 0.62), (0, 0, h * 0.31), wood or M['planks'], C['Props'], bevel=0.02), out)
    lid = geo.lathe('Chest lid', [(d / 2, -w / 2), (d / 2, w / 2)], 16, wood or M['planks'], C['Props'],
                    angle=math.pi, close_top=True, close_bottom=True)
    # lathe around Z with angle pi gives a half-cylinder; orient its axis along X
    geo.transform(lid, Matrix.Rotation(math.pi / 2, 4, 'Y') @ Matrix.Rotation(-math.pi / 2, 4, 'Z'))
    for v in lid.data.vertices:
        v.co.z *= 0.55
    lid.data.update()
    hinge = Matrix.Translation((0, d / 2, h * 0.62)) @ Matrix.Rotation(-open_lid, 4, 'X') @ Matrix.Translation(
        (0, -d / 2, 0))
    geo.transform(lid, hinge)
    _p(lid, out)
    for x in (-w * 0.32, w * 0.32):
        _p(geo.box('Chest band', (0.06, d + 0.02, h * 0.62 + 0.01), (x, 0, h * 0.31), M['brass'], C['Props'], bevel=0.006),
           out)
    _p(geo.box('Lock', (0.12, 0.04, 0.14), (0, -d / 2 - 0.02, h * 0.5), M['brass'], C['Props'], bevel=0.01), out)
    if gold and open_lid > 0.3:
        mound = geo.quadsphere('Chest gold', 0.5, (0, 0, 0), M['gold'], C['Props'], level=3,
                               scale=(w * 0.92, d * 0.85, 0.35))
        geo.displace_noise(mound, 0.04, 6.0, seed=3)
        geo.place(mound, (0, 0, h * 0.58))
        _p(mound, out)
    return _fin(out, loc, rot)


def coin_pile(loc=(0, 0, 0), r=1.0, h=0.5, M=None, seed=0, coins=40):
    """Heap of gold with loose coins on top (coins as thin discs)."""
    M = M or pm()
    rng = random.Random(seed)
    out = []
    mound = geo.quadsphere('Gold heap', 1.0, (0, 0, 0), M['gold'], C['Props'], level=4, scale=(r, r * 0.9, h))
    for v in mound.data.vertices:
        if v.co.z < 0:
            v.co.z *= 0.05
    mound.data.update()
    geo.displace_noise(mound, 0.05 * r, 3.0 / r, seed=seed)
    _p(mound, out)
    proto = env.proto('coin', lambda: _coin_proto(M))
    for i in range(coins):
        a = rng.uniform(0, math.tau)
        rr = r * math.sqrt(rng.uniform(0, 1.1))
        z = h * max(0.0, 1 - (rr / r) ** 2) ** 0.5 + 0.01
        o = env.inst(proto, (loc[0] + math.cos(a) * rr, loc[1] + math.sin(a) * rr, loc[2] + z),
                     rng.uniform(0, 6.28), 1.0, C['Props'], tilt=(rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6)))
    return _fin(out, loc, 0.0)


def _coin_proto(M):
    o = geo.cylinder('Coin', 0.045, 0.008, (0, 0, 0), M['gold'], None, 14, bevel=0.002)
    return o


# ------------------------------------------------------------------ light sources


def lantern_post(loc=(0, 0, 0), rot=0.0, h=2.6, M=None, power=40.0, color=AMBER, arm=True):
    M = M or pm()
    out = []
    _p(geo.cylinder('Lantern post', 0.07, h, (0, 0, h / 2), M['wood_dark'], C['Props'], 8, bevel=0.01), out)
    _p(geo.cylinder('Post foot', 0.13, 0.25, (0, 0, 0.12), M['stone'], C['Props'], 8, bevel=0.02), out)
    mats = dict(hilt=M['iron'], glow=M['flame'], lamp_glass=M['lamp_glass'])
    if arm:
        _p(geo.cylinder('Lantern arm', 0.03, 0.55, (0.25, 0, h - 0.1), M['iron'], C['Props'], 6, rotation=(0, 90, 0),
                        bevel=0), out)
        base = _v(0.38, 0, h - 0.12)
        objs = W.lantern_head(mats, C['Props'], base, M['flame'], size=1.4)
        lamp = base + _v(0.17 * 1.4, 0, -0.05 * 1.4 - 0.02 * 1.4)
    else:
        base = _v(0, 0, h)
        objs = W.lantern_head(mats, C['Props'], base, M['flame'], size=1.4)
        lamp = base + _v(0.17 * 1.4, 0, -0.1)
    for o in objs:
        env.finish(o, uv=None)
        out.append(o)
    _fin(out, loc, rot)
    p = Matrix.Translation(Vector(loc)) @ Matrix.Rotation(rot, 4, 'Z') @ lamp
    env.point(p, power, color, 0.06, name='Lantern light')
    return out


def hanging_lantern(pos, M=None, power=25.0, color=AMBER, size=1.2, chain=0.4):
    M = M or pm()
    out = []
    pos = Vector(pos)
    mats = dict(hilt=M['iron'], glow=M['flame'], lamp_glass=M['lamp_glass'])
    c = pos - _v(0, 0, chain)
    _p(geo.cylinder('Lantern chain', 0.01, chain, pos - _v(0, 0, chain / 2), M['iron'], C['Props'], 5, bevel=0), out)
    cap = geo.lathe('Lantern cap', [(0, 0.1 * size), (0.08 * size, 0.04 * size), (0.09 * size, 0.0)], 8, M['iron'],
                    C['Props'], location=c)
    _p(cap, out)
    _p(geo.cylinder('Lantern glass', 0.07 * size, 0.16 * size, c - _v(0, 0, 0.09 * size), M['lamp_glass'], C['Props'], 8,
                    bevel=0), out)
    _p(geo.sphere('Lantern flame', 0.035 * size, c - _v(0, 0, 0.09 * size), M['flame'], C['Props'], 8, 6), out)
    _p(geo.cylinder('Lantern base', 0.08 * size, 0.03 * size, c - _v(0, 0, 0.18 * size), M['iron'], C['Props'], 8,
                    bevel=0), out)
    env.point(c - _v(0, 0, 0.09 * size), power, color, 0.05, name='Hanging lantern light')
    return out


def torch(loc=(0, 0, 0), h=1.8, M=None, power=60.0, color='ff9a3a', wall=None):
    M = M or pm()
    out = []
    loc = Vector(loc)
    if wall is None:
        _p(geo.cylinder('Torch staff', 0.035, h, loc + _v(0, 0, h / 2), M['wood_dark'], C['Props'], 6, bevel=0), out)
        top = loc + _v(0, 0, h)
    else:
        nrm = Vector(wall).normalized()
        _p(geo.cylinder('Sconce', 0.03, 0.5, loc + nrm * 0.18 + _v(0, 0, 0.1), M['iron'], C['Props'], 6, bevel=0,
                        rotation=(0, 0, 0)), out)
        top = loc + nrm * 0.18 + _v(0, 0, 0.4)
    _p(geo.cylinder('Torch head', 0.06, 0.18, top, M['rope'], C['Props'], 8, bevel=0), out)
    fl = geo.lathe('Torch flame', [(0, 0), (0.07, 0.05), (0.06, 0.16), (0.02, 0.3), (0, 0.36)], 8, M['flame'],
                   C['FX'], location=top + _v(0, 0, 0.06))
    geo.displace_noise(fl, 0.015, 12, seed=int(loc.x * 7 + loc.y * 3) % 50)
    out.append(fl)
    env.point(top + _v(0, 0, 0.25), power, color, 0.08, name='Torch light')
    return out


def candle(loc, h=0.2, r=0.025, M=None, power=0.0, flame=True):
    M = M or pm()
    out = []
    loc = Vector(loc)
    _p(geo.cylinder('Candle', r, h, loc + _v(0, 0, h / 2), M['wax'], C['Props'], 8, bevel=0.003), out)
    if flame:
        fl = geo.lathe('Candle flame', [(0, 0), (r * 0.6, r * 0.8), (r * 0.35, r * 2.2), (0, r * 3.2)], 8, M['flame'],
                       C['FX'], location=loc + _v(0, 0, h + 0.005))
        out.append(fl)
        if power:
            env.point(loc + _v(0, 0, h + r * 1.5), power, 'ffb45a', 0.02, name='Candle light')
    return out


def campfire(loc=(0, 0, 0), r=0.9, M=None, power=900.0, seed=0, smoke=True):
    M = M or pm()
    rng = random.Random(seed)
    out = []
    loc = Vector(loc)
    for k in range(10):
        a = k * math.tau / 10
        st = geo.quadsphere('Fire stone', 0.16, (0, 0, 0), M['stone'], C['Props'], level=2,
                            scale=(1.2, 1, 0.8))
        geo.displace_noise(st, 0.04, 6, seed=k)
        geo.place(st, loc + _v(math.cos(a) * r, math.sin(a) * r, 0.08))
        _p(st, out)
    for k in range(5):
        a = k * math.tau / 5 + rng.uniform(-0.2, 0.2)
        p0 = loc + _v(math.cos(a) * r * 0.75, math.sin(a) * r * 0.75, 0.05)
        p1 = loc + _v(math.cos(a) * 0.05, math.sin(a) * 0.05, 0.55)
        _p(geo.tube('Log', [p0, p1], [0.07, 0.05], M['wood_dark'], C['Props'], sides=7), out)
    bed = geo.quadsphere('Ember bed', r * 0.6, (0, 0, 0), M['ember'], C['Props'], level=2, scale=(1, 1, 0.18))
    geo.place(bed, loc + _v(0, 0, 0.05))
    _p(bed, out)
    for k in range(4):
        a = k * math.tau / 4 + 0.4
        hh = rng.uniform(0.7, 1.1)
        fl = geo.lathe('Fire tongue', [(0, 0), (0.22, 0.12), (0.18, hh * 0.45), (0.06, hh * 0.85), (0, hh)], 10,
                       M['flame'], C['FX'])
        geo.displace_noise(fl, 0.05, 6, seed=seed + k)
        for v in fl.data.vertices:
            v.co.x += math.sin(v.co.z * 4 + k) * 0.05 * v.co.z
        fl.data.update()
        geo.place(fl, loc + _v(math.cos(a) * 0.12, math.sin(a) * 0.12, 0.08))
        out.append(fl)
    env.point(loc + _v(0, 0, 0.7), power, 'ff8a32', 0.25, name='Campfire light')
    if smoke:
        sm = smoke_column(loc + _v(0, 0, 1.0), height=6.0, radius=0.7, density=0.6)
        out += sm
    return out


def smoke_column(base, height=8.0, radius=1.0, density=0.5, color='9a9088', drift=(1.5, 0.5), name='Smoke'):
    """Rising volumetric smoke plume (noise-eroded cone)."""
    from .nodekit import material
    m, n = material(name)
    p = n.tc('Object')
    x, y, z = n.xyz(p)
    zz = n.maprange(z, -1.0, 1.0, 0.0, 1.0)
    dx = n.mul(n.mul(zz, zz), drift[0] / (radius * 3))
    dy = n.mul(n.mul(zz, zz), drift[1] / (radius * 3))
    q = n.combine(n.math('SUBTRACT', x, dx), n.math('SUBTRACT', y, dy), z)
    r = n.vmath('LENGTH', n.combine(n.xyz(q)[0], n.xyz(q)[1], 0))
    width = n.add(0.25, n.mul(zz, 0.55))
    body = n.math('SUBTRACT', 1.0, n.math('DIVIDE', r, width))
    nz = n.noise(n.vmath('MULTIPLY', p, (2.0, 2.0, 0.8)), 2.5, 5, 0.6)
    f = n.add(body, n.mul(n.add(nz, -0.5), 1.2))
    dens = n.mul(n.mul(n.smooth(f, 0.1, 0.6), n.smooth(zz, 0.0, 0.08)), n.smooth(zz, 1.0, 0.6, 0.0, 1.0))
    n.volume(density=n.mul(dens, density), color=color, absorption=(0.6, 0.6, 0.6, 1), anisotropy=0.3)
    me = bpy.data.meshes.new(name)
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=2.0)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    C['Atmosphere'].objects.link(obj)
    w = radius * 3 + max(abs(drift[0]), abs(drift[1]))
    obj.location = Vector(base) + _v(drift[0] / 2, drift[1] / 2, height / 2)
    obj.scale = (w, w, height / 2)
    me.materials.append(m)
    return [obj]


# ------------------------------------------------------------------ banners & heraldry


def banner_pole(loc=(0, 0, 0), rot=0.0, cloth=None, emblem='lantern', h=4.2, M=None, width=0.9, drop=1.6):
    M = M or pm()
    cloth = cloth or M['teal']
    mats = dict(wood=M['wood_dark'], hilt=M['brass'], blade=M['iron'], glow=M['flame'], lamp_glass=M['lamp_glass'])
    em = None
    if emblem == 'lantern':
        em = W.emblem_lantern(M['brass'], M['flame'])
    elif emblem == 'skull':
        em = W.emblem_skull(M['bone'], M['plague'])
    elif emblem == 'sun':
        em = W.emblem_sun(M['brass'])
    d = W.standard(mats, C['Props'], cloth, emblem=None, height=h, grip_at=0.0, width=width, drop=drop,
                   finial='lantern' if emblem == 'lantern' else 'spike', tails=2, trim=M['brass'])
    out = list(d['objs'])
    if em is not None:
        # emblem faces -Y (toward camera); weapons emblems face +X, so rotate
        eo = em(_v(0.0, 0, 0), C['Props'], 1.8)
        for o in eo:
            geo.transform(o, Matrix.Translation((0.02 + width * 0.5, -0.03, h - 0.13 - drop * 0.42)) @
                          Matrix.Rotation(-math.pi / 2, 4, 'Z'))
        out += eo
    for o in out:
        env.finish(o, uv=None)
    return env.group_xform(out, loc, rot)


def hanging_banner(top_center, width=1.2, length=3.0, cloth=None, M=None, emblem=None, rot=0.0, tails=True,
                   trim=None, sway=0.0):
    """Vertical banner hanging from a rod (faces -Y)."""
    M = M or pm()
    cloth = cloth or M['teal']
    out = []
    tc = Vector(top_center)

    def fn(u, v):
        x = (u - 0.5) * width
        z = -v * length
        if v > 0.82:
            k = (v - 0.82) / 0.18
            z += k * length * 0.15 * (1 - 2 * abs(u - 0.5)) if tails else 0
        y = 0.03 * math.sin(u * 7 + v * 3) + sway * v * v
        return (x, y, z)
    sh = geo.grid_sheet('Banner', 1, 1, 10, 16, fn, cloth, C['Props'])
    mod = sh.modifiers.new('Thickness', 'SOLIDIFY')
    mod.thickness = 0.02
    env.finish(sh, uv=None)
    out.append(sh)
    rod = geo.cylinder('Banner rod', 0.025, width + 0.3, (0, 0, 0.02), M['wood_dark'], C['Props'], 8, rotation=(0, 90, 0),
                       bevel=0)
    _p(rod, out)
    for s in (-1, 1):
        _p(geo.sphere('Rod finial', 0.045, (s * (width / 2 + 0.17), 0, 0.02), M['brass'], C['Props'], 8, 6), out)
    if trim is not None:
        _p(geo.box('Banner trim', (width, 0.03, 0.08), (0, -0.02, -0.06), trim, C['Props'], bevel=0.005), out)
    if emblem is not None:
        eo = emblem(_v(0, 0, 0), C['Props'], width / 0.55)
        for o in eo:
            geo.transform(o, Matrix.Translation((0, -0.04, -length * 0.38)) @ Matrix.Rotation(-math.pi / 2, 4, 'Z'))
            env.finish(o, uv=None)
        out += eo
    return env.group_xform(out, tc, rot)


def bunting(p0, p1, sag=0.6, count=10, colors=None, M=None, size=0.28):
    """String of triangular pennants between two points."""
    M = M or pm()
    colors = colors or [M['teal'], M['canvas'], M['crimson']]
    out = []
    p0, p1 = Vector(p0), Vector(p1)
    pts = []
    for i in range(count * 2 + 1):
        t = i / (count * 2)
        pts.append(p0.lerp(p1, t) - _v(0, 0, sag * 4 * t * (1 - t)))
    _p(geo.tube('Bunting line', pts, 0.008, M['rope'], C['Props'], sides=4), out)
    d = (p1 - p0)
    u = Vector((d.x, d.y, 0)).normalized()
    for i in range(count):
        t = (i + 0.5) / count
        c = p0.lerp(p1, t) - _v(0, 0, sag * 4 * t * (1 - t))
        a = c - u * size * 0.45
        b = c + u * size * 0.45
        tip = c - _v(0, 0, size * 1.2)
        o = geo.extrude('Pennant', [(0, 0), (1, 0), (0.5, -1)], 0.01, colors[i % len(colors)], C['Props'], plane='XZ',
                        bevel=0)
        # map the unit triangle (x along a->b, -z toward the tip) onto the pennant corners
        for v in o.data.vertices:
            x, y, z = v.co
            v.co = a + (b - a) * x + (tip - (a + b) / 2) * (-z)
        o.data.update()
        geo.store_rest(o)
        env.finish(o, uv=None)
        out.append(o)
    return out


# ------------------------------------------------------------------ market & caravan


def stall(loc=(0, 0, 0), rot=0.0, w=2.6, d=1.4, M=None, awning=None, seed=0, wares='mixed'):
    M = M or pm()
    rng = random.Random(seed)
    out = []
    awning = awning or env.stripes(f'Awning {seed % 3}', a=[LANTERN_TEAL, ROT_CRIMSON, 'b0803a'][seed % 3], b='e2d6b8',
                                   count=2.2, axis='x')
    hf, hb = 2.5, 2.1
    for sx in (-1, 1):
        for sy, hh in ((-1, hf), (1, hb)):
            _p(geo.cylinder('Stall post', 0.06, hh, (sx * w / 2, sy * d / 2, hh / 2), M['wood_dark'], C['Props'], 8,
                            bevel=0.01), out)
    _p(geo.box('Counter', (w, 0.6, 0.9), (0, -d / 2 + 0.3, 0.45), M['planks'], C['Props'], bevel=0.02), out)
    _p(geo.box('Counter top', (w + 0.1, 0.7, 0.06), (0, -d / 2 + 0.3, 0.93), M['wood'], C['Props'], bevel=0.01), out)

    def fn(u, v):
        x = (u - 0.5) * (w + 0.4)
        y = -d / 2 - 0.5 + v * (d + 0.5)
        z = hf + 0.05 - (hf - hb) * v - 0.12 * math.sin(math.pi * v) * 0 + 0.06 * math.sin(u * math.pi * 6) * (1 - v)
        return (x, y, z)
    aw = geo.grid_sheet('Awning', 1, 1, 18, 6, fn, awning, C['Props'])
    mod = aw.modifiers.new('Thickness', 'SOLIDIFY')
    mod.thickness = 0.02
    env.store_local(aw)
    out.append(aw)
    # scalloped valance
    for i in range(8):
        x = -(w + 0.4) / 2 + (i + 0.5) * (w + 0.4) / 8
        o = geo.lathe('Scallop', [(0.16, 0), (0.0, -0.22)], 10, awning, C['Props'], angle=math.pi, close_top=False,
                      close_bottom=False)
        geo.transform(o, Matrix.Translation((x, -d / 2 - 0.5, hf + 0.04)) @ Matrix.Rotation(math.pi / 2, 4, 'X') @
                      Matrix.Rotation(0, 4, 'Z'))
        env.store_local(o)
        out.append(o)
    # wares on the counter
    top = 0.96
    for i in range(int(w / 0.45)):
        x = -w / 2 + 0.3 + i * 0.45
        kind = rng.choice(['fruit', 'pot', 'cloth', 'fruit', 'crate']) if wares == 'mixed' else wares
        if kind == 'fruit':
            bowl = geo.lathe('Basket', [(0, 0), (0.14, 0.01), (0.2, 0.12)], 12, M['wood_pale'], C['Props'],
                             close_top=False, location=(x, -d / 2 + 0.3, top))
            _p(bowl, out, uv='cyl')
            fm = rng.choice([M['fruit_r'], M['fruit_y'], M['green']])
            for k in range(6):
                a = k * 1.1
                _p(geo.sphere('Fruit', 0.055, (x + math.cos(a) * 0.08, -d / 2 + 0.3 + math.sin(a) * 0.08,
                                               top + 0.12 + (k % 2) * 0.05), fm, C['Props'], 8, 6), out)
        elif kind == 'pot':
            out += pot((x, -d / 2 + 0.3, top), rng.uniform(0, 6), h=rng.uniform(0.25, 0.4), M=M,
                       kind=rng.choice(['amphora', 'jar']))
        elif kind == 'cloth':
            for k in range(3):
                _p(geo.box('Bolt of cloth', (0.36, 0.5, 0.08), (x, -d / 2 + 0.32, top + 0.04 + k * 0.085),
                           rng.choice([M['teal'], M['crimson'], M['canvas']]), C['Props'], bevel=0.03), out)
        else:
            out += crate((x, -d / 2 + 0.3, top), rng.uniform(-0.3, 0.3), 0.32, M)
    # hanging goods
    for i in range(3):
        x = -w / 2 + 0.5 + i * (w - 1.0) / 2
        out += hanging_lantern((x, -d / 2 - 0.2, hf - 0.05), M, power=8.0, size=0.9, chain=0.25) if i == 1 else []
    return _fin(out, loc, rot)


def wheel(center, r=0.55, width=0.1, M=None, spokes=10, axis='Y'):
    M = M or pm()
    out = []
    rim = geo.lathe('Wheel rim', [(r - 0.06, -width / 2), (r, -width / 2), (r, width / 2), (r - 0.06, width / 2)], 28,
                    M['wood_dark'], C['Props'], close_top=False, close_bottom=False)
    mod = rim.modifiers.new('Rim', 'SOLIDIFY')
    mod.thickness = 0.02
    out.append(rim)
    tyre = geo.lathe('Tyre', [(r, -width / 2 - 0.005), (r + 0.02, -width / 2), (r + 0.02, width / 2),
                              (r, width / 2 + 0.005)], 28, M['iron'], C['Props'], close_top=False, close_bottom=False)
    out.append(tyre)
    out.append(geo.cylinder('Hub', 0.09, width * 1.8, (0, 0, 0), M['wood'], C['Props'], 10, bevel=0.01))
    for k in range(spokes):
        a = k * math.tau / spokes
        out.append(geo.tube('Spoke', [_v(math.cos(a) * 0.08, math.sin(a) * 0.08, 0),
                                      _v(math.cos(a) * (r - 0.04), math.sin(a) * (r - 0.04), 0)], 0.022,
                            M['wood'], C['Props'], sides=5))
    rotm = Matrix.Rotation(math.pi / 2, 4, 'X') if axis == 'Y' else Matrix.Rotation(math.pi / 2, 4, 'Y')
    for o in out:
        geo.transform(o, Matrix.Translation(Vector(center)) @ rotm)
        env.finish(o, coll=C['Props'], uv=None)
    return out


def wagon(loc=(0, 0, 0), rot=0.0, L=3.6, Wd=1.7, M=None, cover=None, lantern=True, seed=0, cargo=True, power=30.0,
          hoops=5):
    """Lantern Caravan covered wagon: plank bed on spoked wheels, arched teal canvas, brass lantern."""
    M = M or pm()
    rng = random.Random(seed)
    cover = cover or env.stripes('Wagon canvas', a=LANTERN_TEAL, b='d9ccb0', count=1.1, axis='x', width=0.72)
    out = []
    bed_z = 0.85
    _p(geo.box('Wagon bed', (L, Wd, 0.14), (0, 0, bed_z), M['planks'], C['Props'], bevel=0.02), out)
    for sy in (-1, 1):
        _p(geo.box('Side board', (L, 0.08, 0.5), (0, sy * (Wd / 2 - 0.04), bed_z + 0.32), M['planks'], C['Props'],
                   bevel=0.015), out)
        _p(geo.box('Side rail', (L + 0.1, 0.1, 0.08), (0, sy * (Wd / 2 - 0.04), bed_z + 0.6), M['wood_dark'], C['Props'],
                   bevel=0.01), out)
    for sx in (-1, 1):
        _p(geo.box('End board', (0.08, Wd, 0.45), (sx * (L / 2 - 0.04), 0, bed_z + 0.3), M['planks'], C['Props'],
                   bevel=0.015), out)
    # canvas: arched sheet along X
    cov_r = Wd / 2 + 0.05

    def fn(u, v):
        x = (u - 0.5) * (L - 0.3)
        a = math.pi * v
        sag = 0.05 * math.sin(u * math.pi * (hoops - 1) * 2) ** 2
        y = -math.cos(a) * (cov_r - sag * 0.3)
        z = bed_z + 0.6 + math.sin(a) * (cov_r * 1.05 - sag)
        return (x, y, z)
    cv = geo.grid_sheet('Canvas cover', 1, 1, 24, 14, fn, cover, C['Props'])
    mod = cv.modifiers.new('Thickness', 'SOLIDIFY')
    mod.thickness = 0.025
    env.store_local(cv)
    out.append(cv)
    for i in range(hoops):
        x = -(L - 0.3) / 2 + i * (L - 0.3) / (hoops - 1)
        pts = [_v(x, -math.cos(math.pi * t / 12) * (cov_r + 0.02),
                  bed_z + 0.6 + math.sin(math.pi * t / 12) * (cov_r * 1.05 + 0.02)) for t in range(13)]
        _p(geo.tube('Hoop', pts, 0.025, M['wood_dark'], C['Props'], sides=6), out, uv=None)
    for sx in (-1, 1):
        for sy in (-1, 1):
            out += wheel((sx * L * 0.32, sy * (Wd / 2 + 0.12), 0.55), 0.55, 0.1, M)
    _p(geo.cylinder('Axle', 0.05, Wd + 0.4, (L * 0.32, 0, 0.55), M['wood_dark'], C['Props'], 8, rotation=(90, 0, 0)), out)
    _p(geo.cylinder('Axle', 0.05, Wd + 0.4, (-L * 0.32, 0, 0.55), M['wood_dark'], C['Props'], 8, rotation=(90, 0, 0)), out)
    for sy in (-1, 1):
        _p(geo.tube('Shaft', [_v(L / 2, sy * 0.45, bed_z - 0.05), _v(L / 2 + 2.2, sy * 0.5, 0.75)], 0.045,
                    M['wood_dark'], C['Props'], sides=6), out, uv=None)
    if cargo:
        out += crate((L / 2 - 0.5, 0.25, bed_z + 0.07), 0.2, 0.45, M)
        out += sack((L / 2 - 0.45, -0.35, bed_z + 0.07), 0.5, 0.5, M, seed=seed)
    lamp_local = None
    if lantern:
        mats = dict(hilt=M['brass'], glow=M['flame'], lamp_glass=M['lamp_glass'])
        _p(geo.cylinder('Lantern pole', 0.035, 1.6, (L / 2 - 0.1, Wd / 2 - 0.1, bed_z + 1.1), M['wood_dark'], C['Props'],
                        6, bevel=0), out)
        base = _v(L / 2 - 0.1, Wd / 2 - 0.1, bed_z + 1.9)
        for o in W.lantern_head(mats, C['Props'], base, M['flame'], size=1.5):
            env.finish(o, uv=None)
            out.append(o)
        lamp_local = base + _v(0.17 * 1.5, 0, -0.1)
    _fin(out, loc, rot)
    if lamp_local is not None and power:
        p = Matrix.Translation(Vector(loc)) @ Matrix.Rotation(rot, 4, 'Z') @ lamp_local
        env.point(p, power, AMBER, 0.06, name='Wagon lantern')
    return out


def pavilion(loc=(0, 0, 0), rot=0.0, r=2.2, h_wall=2.0, h_roof=1.8, a=LANTERN_TEAL, b='e0d4b6', M=None, door=True,
             pennant=True, count=16, scallops=True, pole=True, width=0.5):
    """Round medieval pavilion tent with striped walls, conical roof, scalloped valance and finial pennant."""
    M = M or pm()
    out = []
    mat = env.stripes(f'Pavilion {a} {b} {width}', a=a, b=b, count=count, axis='angle', width=width)
    prof_w = [(r * 0.98, 0.0), (r, h_wall * 0.5), (r * 0.97, h_wall)]
    wall_o = geo.lathe('Pavilion wall', prof_w, 48, mat, C['Props'], close_top=False, close_bottom=False)
    mod = wall_o.modifiers.new('Thickness', 'SOLIDIFY')
    mod.thickness = 0.03
    if door:
        # cut a doorway facing -Y by pulling wall verts aside
        for v in wall_o.data.vertices:
            ang = math.atan2(v.co.y, v.co.x)
            if abs(ang + math.pi / 2) < 0.28 and v.co.z < h_wall * 0.9:
                v.co.z = min(v.co.z, 0.0) if v.co.z < h_wall * 0.95 else v.co.z
        wall_o.data.update()
    env.store_local(wall_o)
    out.append(wall_o)
    prof_r = [(0, h_wall + h_roof), (r * 0.25, h_wall + h_roof * 0.78), (r * 0.7, h_wall + h_roof * 0.3),
              (r + 0.35, h_wall - 0.05)]
    roof = geo.lathe('Pavilion roof', prof_r, 48, mat, C['Props'], close_top=False, close_bottom=False)
    mod = roof.modifiers.new('Thickness', 'SOLIDIFY')
    mod.thickness = 0.03
    env.store_local(roof)
    out.append(roof)
    if scallops:
        n = count
        for k in range(n):
            ang = (k + 0.5) * math.tau / n
            o = geo.lathe('Valance', [(0.22, 0), (0.0, -0.32)], 10, mat, C['Props'], angle=math.pi, close_top=False,
                          close_bottom=False)
            rr = r + 0.36
            geo.transform(o, Matrix.Translation((math.cos(ang) * rr, math.sin(ang) * rr, h_wall - 0.03)) @
                          Matrix.Rotation(ang + math.pi / 2, 4, 'Z') @ Matrix.Rotation(math.pi / 2, 4, 'X'))
            env.store_local(o)
            out.append(o)
    if pole:
        _p(geo.cylinder('Tent pole', 0.05, h_wall + h_roof + 0.8, (0, 0, (h_wall + h_roof + 0.8) / 2), M['wood_dark'],
                        C['Props'], 8, bevel=0), out)
    _p(geo.sphere('Tent finial', 0.1, (0, 0, h_wall + h_roof + 0.82), M['brass'], C['Props'], 10, 6), out)
    if pennant:
        top = h_wall + h_roof + 0.7
        pn = geo.grid_sheet('Pennant', 1, 1, 10, 2, lambda u, v: (0.05 + u * 1.1, 0.06 * math.sin(u * 5),
                                                                   top - v * 0.35 * (1 - 0.8 * u)), M['teal'], C['Props'])
        env.finish(pn, uv=None)
        out.append(pn)
    return _fin(out, loc, rot)


def ridge_tent(loc=(0, 0, 0), rot=0.0, L=2.6, Wd=2.0, h=1.7, mat=None, M=None, lit=0.0):
    """A-frame canvas tent (ridge along X): sagging sheets, back wall, tied-back door flaps and dark interior.
    The open door faces -Y... rotated so the entrance faces the tent's local -X end."""
    M = M or pm()
    mat = mat or M['canvas']
    out = []
    half = Wd / 2

    def sheet(sign):
        def fn(u, v):
            x = (u - 0.5) * L
            t = v
            y = sign * half * t
            z = h * (1 - t)
            sag = 0.07 * math.sin(math.pi * u) * math.sin(math.pi * t)
            return (x, y - sign * sag * 0.5, z - sag)
        o = geo.grid_sheet('Tent sheet', 1, 1, 10, 6, fn, mat, C['Props'])
        mod = o.modifiers.new('Thick', 'SOLIDIFY')
        mod.thickness = 0.02
        return o
    for sg in (-1, 1):
        o = sheet(sg)
        env.store_local(o)
        out.append(o)
    back = geo.extrude('Tent back', [(-half, 0), (half, 0), (0, h)], 0.02, mat, C['Props'], plane='YZ', bevel=0)
    geo.place(back, (L / 2, 0, 0))
    env.store_local(back)
    out.append(back)
    dark = geo.extrude('Tent shadow', [(-half * 0.9, 0), (half * 0.9, 0), (0, h * 0.9)], 0.02, M['coal'], C['Props'],
                       plane='YZ', bevel=0)
    geo.place(dark, (L / 2 - 0.1, 0, 0))
    out.append(dark)
    for sg in (-1, 1):
        flap = geo.extrude('Door flap', [(0, 0), (sg * half * 0.75, 0), (0, h * 0.95)], 0.02, mat, C['Props'],
                           plane='YZ', bevel=0)
        geo.transform(flap, Matrix.Translation((-L / 2, sg * 0.05, 0)) @ Matrix.Rotation(sg * 0.9, 4, 'Z'))
        env.store_local(flap)
        out.append(flap)
    for sx in (-1, 1):
        _p(geo.cylinder('Tent pole', 0.035, h + 0.25, (sx * (L / 2 + 0.02), 0, (h + 0.25) / 2), M['wood_dark'], C['Props'],
                        6, bevel=0), out, uv=None)
    _p(geo.cylinder('Ridge pole', 0.03, L + 0.1, (0, 0, h + 0.02), M['wood_dark'], C['Props'], 6, rotation=(0, 90, 0),
                    bevel=0), out, uv=None)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p0 = _v(sx * L * 0.35, sy * half * 0.98, 0.05)
            out.append(geo.tube('Guy rope', [_v(sx * (L / 2), 0, h), _v(sx * (L / 2 + 0.9), sy * 0.6, 0.0)], 0.008,
                                M['rope'], C['Props'], sides=4))
    _fin(out, loc, rot)
    if lit:
        mw = Matrix.Translation(Vector(loc)) @ Matrix.Rotation(rot, 4, 'Z')
        env.point(mw @ _v(0, 0, h * 0.5), lit, 'ffb05a', 0.3, name='Tent lamp')
    return out


def fence(p0, p1, h=1.0, M=None, posts=1.8, rails=2, height_fn=None):
    M = M or pm()
    out = []
    p0, p1 = Vector(p0), Vector(p1)
    n = max(1, int((p1 - p0).length / posts))
    pts = [p0.lerp(p1, i / n) for i in range(n + 1)]
    hf = height_fn or (lambda x, y: 0.0)
    for p in pts:
        z = hf(p.x, p.y)
        _p(geo.cylinder('Fence post', 0.06, h + 0.2, (p.x, p.y, z + (h + 0.2) / 2 - 0.15), M['wood_dark'], C['Props'], 6,
                        bevel=0.008), out, uv=None)
    for a, b in zip(pts, pts[1:]):
        za, zb = hf(a.x, a.y), hf(b.x, b.y)
        for k in range(rails):
            hh = h * (0.4 + 0.5 * k / max(1, rails - 1))
            beam(_v(a.x, a.y, za + hh), _v(b.x, b.y, zb + hh), 0.07, M['wood'], out, 'Fence rail')
    return out


def well(loc=(0, 0, 0), rot=0.0, M=None, stone=None):
    M = M or pm()
    out = []
    st = stone or env.masonry('Well stone', 'a49478', '86786a', '5a5046', block=(0.35, 0.22))
    ring = geo.lathe('Well', [(0.9, 0), (0.9, 0.85), (0.7, 0.85), (0.7, 0.2)], 24, st, C['Props'], close_top=False)
    _p(ring, out, uv='cyl')
    for s in (-1, 1):
        _p(geo.box('Well post', (0.14, 0.14, 2.2), (s * 0.8, 0, 1.1), M['wood_dark'], C['Props'], bevel=0.01), out)
    roof = geo.extrude('Well roof', [(-1.1, 0), (0, 0.8), (1.1, 0)], 1.4, env.roof_tiles('Well roof', '8a4a32', '6a3826'),
                       C['Props'], plane='YZ', bevel=0.02)
    geo.place(roof, (0, 0, 2.1), rotation=(0, 0, 90))
    _p(roof, out)
    _p(geo.cylinder('Windlass', 0.08, 1.7, (0, 0, 1.55), M['wood'], C['Props'], 8, rotation=(0, 90, 0)), out)
    _p(geo.cylinder('Bucket', 0.17, 0.3, (0.25, 0, 0.98), M['wood'], C['Props'], 10, bevel=0.01), out, uv='cyl')
    return _fin(out, loc, rot)


# ------------------------------------------------------------------ furniture & interiors


def table(loc=(0, 0, 0), rot=0.0, w=1.8, d=0.9, h=0.8, M=None):
    M = M or pm()
    out = []
    _p(geo.box('Table top', (w, d, 0.08), (0, 0, h - 0.04), M['planks'], C['Props'], bevel=0.015), out)
    for sx in (-1, 1):
        for sy in (-1, 1):
            _p(geo.box('Table leg', (0.09, 0.09, h - 0.08), (sx * (w / 2 - 0.12), sy * (d / 2 - 0.1), (h - 0.08) / 2),
                       M['wood_dark'], C['Props'], bevel=0.01), out)
    return _fin(out, loc, rot)


def bench(loc=(0, 0, 0), rot=0.0, w=1.6, M=None):
    M = M or pm()
    out = []
    _p(geo.box('Bench seat', (w, 0.35, 0.06), (0, 0, 0.45), M['planks'], C['Props'], bevel=0.01), out)
    for sx in (-1, 1):
        _p(geo.box('Bench leg', (0.07, 0.3, 0.42), (sx * (w / 2 - 0.15), 0, 0.21), M['wood_dark'], C['Props'], bevel=0.01),
           out)
    return _fin(out, loc, rot)


def book_mats(M):
    if 'books' not in M:
        cols = ['5a1e22', '1c4a46', '3a2a5a', '6a4a1e', '2a3a24', '7a5a2e', '4a2418', '233040']
        M['books'] = [S.leather(c, name=f'Book {c}', rough=0.6) for c in cols]
    return M['books']


def bookshelf(loc=(0, 0, 0), rot=0.0, w=2.0, h=3.0, d=0.45, shelves=6, M=None, seed=0, fill=0.9):
    M = M or pm()
    rng = random.Random(seed)
    books = book_mats(M)
    out = []
    t = 0.06
    _p(geo.box('Shelf back', (w, 0.04, h), (0, d / 2 - 0.02, h / 2), M['wood_dark'], C['Props'], bevel=0.005), out)
    for sx in (-1, 1):
        _p(geo.box('Shelf side', (t, d, h), (sx * (w / 2 - t / 2), 0, h / 2), M['wood'], C['Props'], bevel=0.01), out)
    for i in range(shelves + 1):
        z = 0.05 + i * (h - 0.1) / shelves
        _p(geo.box('Shelf', (w, d, 0.04), (0, 0, z), M['wood'], C['Props'], bevel=0.005), out)
        if i == shelves:
            break
        x = -w / 2 + t + 0.02
        gap = (h - 0.1) / shelves - 0.06
        while x < w / 2 - t - 0.06:
            if rng.random() > fill:
                x += rng.uniform(0.08, 0.25)
                continue
            bw = rng.uniform(0.04, 0.08)
            bh = gap * rng.uniform(0.62, 0.95)
            lean = 0 if rng.random() < 0.85 else rng.uniform(-0.25, 0.25)
            o = geo.box('Book', (bw, d * rng.uniform(0.6, 0.85), bh), (0, 0, 0), rng.choice(books), C['Props'],
                        bevel=0.004)
            geo.transform(o, Matrix.Translation((x + bw / 2, -0.02, z + 0.02 + bh / 2)) @ Matrix.Rotation(lean, 4, 'Y'))
            _p(o, out)
            x += bw + 0.004
    return _fin(out, loc, rot)


def book_stack(loc, n=4, M=None, seed=0, open_book=False):
    M = M or pm()
    rng = random.Random(seed)
    books = book_mats(M)
    out = []
    z = 0
    for i in range(n):
        th = rng.uniform(0.04, 0.08)
        o = geo.box('Stacked book', (rng.uniform(0.25, 0.35), rng.uniform(0.18, 0.24), th), (0, 0, 0),
                    rng.choice(books), C['Props'], bevel=0.006)
        geo.transform(o, Matrix.Translation((0, 0, z + th / 2)) @ Matrix.Rotation(rng.uniform(-0.3, 0.3), 4, 'Z'))
        _p(o, out)
        z += th
    if open_book:
        for s in (-1, 1):
            pg = geo.grid_sheet('Open page', 1, 1, 6, 2, lambda u, v, s=s: (s * u * 0.22, (v - 0.5) * 0.3,
                                                                              0.03 * math.sin(math.pi * u)),
                                M['parchment'], C['Props'])
            geo.place(pg, (0, 0, z + 0.01))
            _p(pg, out, uv=None)
    return _fin(out, loc, 0)


def anvil(loc=(0, 0, 0), rot=0.0, M=None, s=1.0):
    M = M or pm()
    out = []
    prof = [(-0.42, 0.0), (0.18, 0.0), (0.3, 0.06), (0.42, 0.06), (0.26, 0.12), (0.26, 0.16), (-0.3, 0.16), (-0.36, 0.1)]
    top = geo.extrude('Anvil face', [(x * s, z * s) for x, z in prof], 0.18 * s, M['iron'], C['Props'], plane='XZ',
                      bevel=0.012)
    geo.place(top, (0, 0, 0.62 * s))
    _p(top, out)
    _p(geo.box('Anvil waist', (0.22 * s, 0.13 * s, 0.22 * s), (0, 0, 0.52 * s), M['iron'], C['Props'], bevel=0.02), out)
    _p(geo.box('Anvil foot', (0.42 * s, 0.26 * s, 0.1 * s), (0, 0, 0.38 * s), M['iron'], C['Props'], bevel=0.02), out)
    _p(geo.cylinder('Anvil stump', 0.3 * s, 0.34 * s, (0, 0, 0.17 * s), M['wood_dark'], C['Props'], 14, bevel=0.02), out,
       uv='cyl')
    return _fin(out, loc, rot)


def forge_hearth(loc=(0, 0, 0), rot=0.0, M=None, w=2.4, d=1.6, power=1800.0, stone=None):
    """Brick forge with glowing coal bed, hood and chimney."""
    M = M or pm()
    out = []
    st = stone or env.masonry('Forge brick', '7a4a34', '5e3a2a', '2a1e18', block=(0.3, 0.12), soot=0.6,
                              dirt_bottom=0.2)
    _p(geo.box('Hearth', (w, d, 0.95), (0, 0, 0.475), st, C['Props'], bevel=0.03), out)
    bed = geo.box('Coal bed', (w * 0.65, d * 0.6, 0.12), (0, 0, 0.98), M['ember'], C['Props'], bevel=0.02)
    geo.displace_noise(bed, 0.03, 8, seed=2)
    _p(bed, out)
    hood = geo.lathe('Hood', [(w * 0.55, 0), (w * 0.3, 1.1), (w * 0.22, 1.2)], 4, st, C['Props'], close_top=False,
                     close_bottom=False)
    geo.transform(hood, Matrix.Translation((0, 0, 2.0)) @ Matrix.Rotation(math.pi / 4, 4, 'Z') @
                  Matrix.Scale(1.0, 4))
    mod = hood.modifiers.new('Thickness', 'SOLIDIFY')
    mod.thickness = 0.12
    _p(hood, out)
    _p(geo.box('Chimney', (w * 0.32, w * 0.32, 4.0), (0, 0, 5.1), st, C['Props'], bevel=0.03), out)
    for sx in (-1, 1):
        _p(geo.box('Hood post', (0.18, 0.18, 1.1), (sx * (w / 2 - 0.2), -(d / 2 - 0.2), 1.5), st, C['Props'],
                   bevel=0.02), out)
    _fin(out, loc, rot)
    p = Matrix.Translation(Vector(loc)) @ Matrix.Rotation(rot, 4, 'Z') @ _v(0, 0, 1.25)
    env.point(p, power, 'ff6a1a', 0.5, name='Forge glow')
    return out


def weapon_rack(loc=(0, 0, 0), rot=0.0, M=None, seed=0, w=2.0):
    """A-frame rack with swords, spears and axes leaning on it."""
    M = M or pm()
    rng = random.Random(seed)
    out = []
    for sx in (-1, 1):
        _p(geo.box('Rack upright', (0.08, 0.08, 1.5), (sx * w / 2, 0, 0.75), M['wood_dark'], C['Props'], bevel=0.01), out)
    _p(geo.box('Rack bar', (w + 0.1, 0.08, 0.08), (0, 0, 1.35), M['wood_dark'], C['Props'], bevel=0.01), out)
    _p(geo.box('Rack base', (w + 0.1, 0.4, 0.08), (0, 0.1, 0.05), M['wood_dark'], C['Props'], bevel=0.01), out)
    wm = dict(grip=M['leather'], hilt=M['brass'], blade=S.metal('c9cfd2', name='Rack blade', rough=0.25),
              steel=M['iron'], wood=M['wood'], glow=M['flame'], cloth=M['teal'])
    n = int(w / 0.28)
    for i in range(n):
        x = -w / 2 + 0.2 + i * (w - 0.4) / max(1, n - 1)
        kind = rng.choice(['sword', 'spear', 'axe', 'sword', 'halberd'])
        if kind == 'sword':
            d = W.sword(wm, C['Props'], length=0.9, guard=0.24)
        elif kind == 'spear':
            d = W.spear(wm, C['Props'], length=2.0, grip_at=0.0)
        elif kind == 'halberd':
            d = W.halberd(wm, C['Props'], length=2.0, grip_at=0.0)
        else:
            d = W.axe(wm, C['Props'], haft_len=0.9)
        objs = d['objs']
        lean = rng.uniform(0.12, 0.2)
        base_z = 0.12 if kind in ('spear', 'halberd') else 0.1
        for o in objs:
            if kind == 'sword' or kind == 'axe':
                geo.transform(o, Matrix.Translation((x, 0.08, base_z + 0.1)) @ Matrix.Rotation(lean, 4, 'X') @
                              Matrix.Translation((0, 0, 0.15)))
            else:
                geo.transform(o, Matrix.Translation((x, 0.12, base_z)) @ Matrix.Rotation(lean * 0.7, 4, 'X'))
            env.finish(o, uv=None)
            out.append(o)
    return _fin(out, loc, rot)


def armour_stand(loc=(0, 0, 0), rot=0.0, M=None, plate=None, cloth=None, scale=1.0):
    """Plate harness on a wooden stand (stylised: cuirass, pauldrons, helm, tabard)."""
    M = M or pm()
    plate = plate or S.metal('a7b0b4', name='Display plate', rough=0.25)
    cloth = cloth or M['teal']
    out = []
    _p(geo.cylinder('Stand pole', 0.04, 1.9, (0, 0, 0.95), M['wood_dark'], C['Props'], 8, bevel=0), out)
    _p(geo.cylinder('Stand base', 0.32, 0.08, (0, 0, 0.04), M['wood_dark'], C['Props'], 14, bevel=0.01), out)
    cu = geo.lathe('Cuirass', [(0.18, 0.95), (0.25, 1.05), (0.27, 1.25), (0.25, 1.42), (0.15, 1.52), (0.08, 1.55)], 20,
                   plate, C['Props'], close_bottom=False)
    for v in cu.data.vertices:
        v.co.y *= 0.72
        if v.co.y < 0:
            v.co.y *= 1.15
    cu.data.update()
    _p(cu, out, uv=None)
    for s in (-1, 1):
        pa = geo.quadsphere('Pauldron', 0.15, (0, 0, 0), plate, C['Props'], level=3, scale=(1.1, 1.0, 0.75))
        geo.place(pa, (s * 0.3, 0, 1.47))
        _p(pa, out, uv=None)
    helm = geo.lathe('Helm', [(0, 1.97), (0.1, 1.95), (0.14, 1.86), (0.145, 1.74), (0.13, 1.66), (0.0, 1.64)], 18, plate,
                     C['Props'])
    _p(helm, out, uv=None)
    _p(geo.box('Visor slit', (0.16, 0.05, 0.015), (0, -0.13, 1.82), M['coal'], C['Props'], bevel=0), out)
    tab = geo.grid_sheet('Tabard', 1, 1, 6, 8, lambda u, v: ((u - 0.5) * 0.36, -0.2 - 0.02 * math.sin(u * 3),
                                                             1.3 - v * 0.75), cloth, C['Props'])
    _p(tab, out, uv=None)
    for s in (-1, 1):
        arm = geo.tube('Vambrace', [_v(s * 0.34, 0, 1.42), _v(s * 0.38, -0.04, 1.12), _v(s * 0.33, -0.08, 0.9)],
                       [0.075, 0.065, 0.06], plate, C['Props'], sides=10)
        _p(arm, out, uv=None)
        _p(geo.quadsphere('Gauntlet', 0.07, (s * 0.33, -0.09, 0.84), plate, C['Props'], level=2), out, uv=None)
    _p(geo.lathe('Fauld', [(0.26, 0.98), (0.3, 0.88), (0.31, 0.78)], 20, plate, C['Props'], close_top=False,
                 close_bottom=False), out, uv=None)
    if scale != 1.0:
        for o in out:
            geo.transform(o, Matrix.Scale(scale, 4))
    return _fin(out, loc, rot)


def statue(loc=(0, 0, 0), rot=0.0, mat=None, h=3.2, pose='sword', plinth=True, M=None, plinth_mat=None):
    """Knight effigy on a moulded plinth (figure from rk.figures)."""
    from . import figures
    M = M or pm()
    mat = mat or env.smooth_stone('c2b9a6', name='Statue marble', veins=0.4)
    out = []
    z0 = 0.0
    if plinth:
        pm_ = plinth_mat or mat
        _p(geo.box('Plinth base', (1.6, 1.6, 0.3), (0, 0, 0.15), pm_, C['Props'], bevel=0.04), out)
        _p(geo.box('Plinth', (1.3, 1.3, 0.9), (0, 0, 0.75), pm_, C['Props'], bevel=0.05), out)
        _p(geo.box('Plinth cap', (1.5, 1.5, 0.16), (0, 0, 1.28), pm_, C['Props'], bevel=0.04), out)
        z0 = 1.36
    fig = figures.knight_statue(mat, h, pose)
    geo.transform(fig, Matrix.Translation((0, 0, z0)))
    _p(fig, out, uv=None)
    return _fin(out, loc, rot)


def trophy_cup(loc, h=0.6, M=None, mat=None):
    M = M or pm()
    out = []
    prof = [(0, 0), (0.16 * h, 0), (0.16 * h, 0.04 * h), (0.05 * h, 0.1 * h), (0.04 * h, 0.4 * h), (0.08 * h, 0.48 * h),
            (0.26 * h, 0.62 * h), (0.3 * h, 0.95 * h), (0.27 * h, 1.0 * h)]
    o = geo.lathe('Trophy', prof, 20, mat or M['gold'], C['Props'], close_top=False, location=loc)
    _p(o, out, uv=None)
    for s in (-1, 1):
        hd = geo.tube('Trophy handle', [Vector(loc) + _v(s * 0.26 * h, 0, 0.9 * h), Vector(loc) + _v(s * 0.42 * h, 0, 0.8 * h),
                                        Vector(loc) + _v(s * 0.24 * h, 0, 0.6 * h)], 0.025 * h, mat or M['gold'],
                      C['Props'], sides=6)
        _p(hd, out, uv=None)
    return out


def noticeboard(loc=(0, 0, 0), rot=0.0, M=None, seed=0, w=2.4, h=1.6, notices=9, wanted=False):
    M = M or pm()
    rng = random.Random(seed)
    out = []
    for sx in (-1, 1):
        _p(geo.box('Board post', (0.14, 0.14, h + 1.3), (sx * (w / 2 + 0.05), 0, (h + 1.3) / 2), M['wood_dark'],
                   C['Props'], bevel=0.015), out)
    _p(geo.box('Board', (w, 0.08, h), (0, 0, 1.0 + h / 2), M['planks'], C['Props'], bevel=0.015), out)
    roof = geo.extrude('Board roof', [(-0.5, 0), (0, 0.35), (0.5, 0)], w + 0.6,
                       env.roof_tiles('Board shingles', '5a4a3a', '4a3a2c', tile=(0.2, 0.14), curve=False),
                       C['Props'], plane='YZ', bevel=0.01)
    geo.place(roof, (0, 0, 1.0 + h + 0.05), rotation=(0, 0, 90))
    _p(roof, out)
    cols = max(1, int(w / 0.55))
    rows = max(1, int(h / 0.62))
    cells = [(c, r) for c in range(cols) for r in range(rows)]
    rng.shuffle(cells)
    for i, (c, r) in enumerate(cells[:notices]):
        pw = rng.uniform(0.32, 0.42)
        ph = pw * rng.uniform(1.2, 1.35)
        x = -w / 2 + (c + 0.5) * w / cols + rng.uniform(-0.06, 0.06)
        z = 1.0 + (r + 0.5) * h / rows + rng.uniform(-0.05, 0.05)
        o = geo.box('Notice', (pw, 0.01, ph), (0, 0, 0), M['parchment'], C['Props'], bevel=0)
        geo.transform(o, Matrix.Translation((x, -0.05 - i * 0.002, z)) @ Matrix.Rotation(rng.uniform(-0.1, 0.1), 4, 'Y'))
        _p(o, out)
        if wanted and rng.random() < 0.75:
            pt = geo.box('Portrait', (pw * 0.62, 0.012, ph * 0.42), (0, 0, 0), M['coal'], C['Props'], bevel=0)
            geo.transform(pt, Matrix.Translation((x, -0.06 - i * 0.002, z + ph * 0.1)))
            _p(pt, out)
            sl = geo.cylinder('Seal', 0.04, 0.01, (x + pw * 0.28, -0.065, z - ph * 0.36), M['crimson'], C['Props'], 10,
                              rotation=(90, 0, 0), bevel=0)
            _p(sl, out)
        _p(geo.sphere('Nail', 0.015, (x, -0.07, z + ph * 0.44), M['iron'], C['Props'], 6, 4), out, uv=None)
    return _fin(out, loc, rot)


def gravestone(loc=(0, 0, 0), rot=0.0, mat=None, seed=0, kind=None, M=None):
    M = M or pm()
    rng = random.Random(seed)
    mat = mat or env.smooth_stone('7a7670', name='Grave stone', moss=0.5)
    out = []
    kind = kind or rng.choice(['round', 'cross', 'slab'])
    tilt = rng.uniform(-0.18, 0.18)
    if kind == 'round':
        o = geo.extrude('Headstone', [(-0.3, 0), (0.3, 0), (0.3, 0.7)] +
                        [(math.cos(a) * 0.3, 0.7 + math.sin(a) * 0.3) for a in [i * math.pi / 8 for i in range(1, 8)]] +
                        [(-0.3, 0.7)], 0.14, mat, C['Props'], plane='XZ', bevel=0.02)
    elif kind == 'cross':
        o = geo.extrude('Grave cross', [(-0.07, 0), (0.07, 0), (0.07, 0.75), (0.28, 0.75), (0.28, 0.9), (0.07, 0.9),
                                        (0.07, 1.15), (-0.07, 1.15), (-0.07, 0.9), (-0.28, 0.9), (-0.28, 0.75),
                                        (-0.07, 0.75)], 0.12, mat, C['Props'], plane='XZ', bevel=0.015)
    else:
        o = geo.box('Grave slab', (0.6, 0.16, 0.9), (0, 0, 0.45), mat, C['Props'], bevel=0.03)
    geo.displace_noise(o, 0.01, 8, seed=seed)
    geo.transform(o, Matrix.Rotation(tilt, 4, 'X') @ Matrix.Rotation(tilt * 0.5, 4, 'Y'))
    _p(o, out)
    return _fin(out, loc, rot)


def skull(loc, s=0.12, M=None, mat=None, rot=0.0, glow=None):
    M = M or pm()
    mat = mat or M['bone']
    out = []
    cr = geo.quadsphere('Skull', s, (0, 0, s * 0.9), mat, C['Props'], level=3, scale=(0.85, 1.0, 0.9))
    _p(cr, out, uv=None)
    _p(geo.box('Jaw', (s * 1.1, s * 0.7, s * 0.45), (0, -s * 0.45, s * 0.35), mat, C['Props'], bevel=s * 0.15), out,
       uv=None)
    for sx in (-1, 1):
        _p(geo.sphere('Socket', s * 0.22, (sx * s * 0.33, -s * 0.78, s * 0.95), glow or M['coal'], C['Props'], 8, 6), out,
           uv=None)
    return _fin(out, loc, rot)


def bones_pile(loc, r=0.8, n=14, M=None, seed=0, skulls=3):
    M = M or pm()
    rng = random.Random(seed)
    out = []
    for i in range(n):
        a = rng.uniform(0, math.tau)
        rr = rng.uniform(0, r)
        c = Vector(loc) + _v(math.cos(a) * rr, math.sin(a) * rr, 0.05)
        d = _v(math.cos(a + rng.uniform(-2, 2)), math.sin(a + rng.uniform(-2, 2)), rng.uniform(-0.1, 0.3)).normalized()
        L = rng.uniform(0.25, 0.5)
        _p(geo.tube('Bone', [c - d * L / 2, c + d * L / 2], [0.035, 0.022, 0.035], M['bone'], C['Props'], sides=6), out,
           uv=None)
    for k in range(skulls):
        a = rng.uniform(0, math.tau)
        rr = rng.uniform(0, r * 0.7)
        out += skull(Vector(loc) + _v(math.cos(a) * rr, math.sin(a) * rr, 0.05), 0.12, M, rot=rng.uniform(-1, 1))
    return out


def sundial(loc=(0, 0, 0), M=None, mat=None, scale=1.0):
    M = M or pm()
    mat = mat or env.smooth_stone('b0a690', name='Sundial stone')
    out = []
    _p(geo.lathe('Sundial pedestal', [(0.4, 0), (0.4, 0.15), (0.22, 0.3), (0.18, 0.9), (0.3, 1.0), (0.55, 1.05),
                                      (0.55, 1.15), (0, 1.15)], 20, mat, C['Props']), out, uv=None)
    _p(geo.cylinder('Sundial step', 0.9, 0.18, (0, 0, 0.09), mat, C['Props'], 24, bevel=0.02), out, uv=None)
    gn = geo.extrude('Gnomon', [(0, 0), (0.42, 0), (0, 0.32)], 0.03, M['brass'], C['Props'], plane='XZ', bevel=0.004)
    geo.place(gn, (-0.2, 0, 1.15))
    _p(gn, out, uv=None)
    for k in range(12):
        a = k * math.tau / 12
        _p(geo.box('Hour mark', (0.08, 0.02, 0.01), (math.cos(a) * 0.45, math.sin(a) * 0.45, 1.155), M['brass'],
                   C['Props'], bevel=0), out, uv=None)
    if scale != 1.0:
        for o in out:
            geo.transform(o, Matrix.Scale(scale, 4))
    return _fin(out, loc, 0)


def cauldron(loc, r=0.5, M=None, liquid=None, power=0.0, color=PLAGUE):
    M = M or pm()
    out = []
    _p(geo.lathe('Cauldron', [(0.0, 0.1), (r * 0.6, 0.12), (r, r * 0.6), (r * 0.95, r * 1.15), (r * 0.85, r * 1.25)], 20,
                 M['iron'], C['Props'], close_top=False, location=loc), out, uv=None)
    if liquid is not None:
        _p(geo.cylinder('Brew', r * 0.86, 0.02, Vector(loc) + _v(0, 0, r * 1.1), liquid, C['Props'], 20, bevel=0), out,
           uv=None)
    for k in range(3):
        a = k * math.tau / 3
        _p(geo.cylinder('Cauldron foot', 0.05, 0.25, Vector(loc) + _v(math.cos(a) * r * 0.5, math.sin(a) * r * 0.5, 0.1),
                        M['iron'], C['Props'], 6, bevel=0), out, uv=None)
    if power:
        env.point(Vector(loc) + _v(0, 0, r * 1.5), power, color, 0.2, name='Cauldron glow')
    return out


def rug(loc, w=3.0, d=2.0, a=LANTERN_TEAL, b='b0803a', rot=0.0, field=None):
    """Bordered woven rug (`a` sets the field tone, `b` the border)."""
    fieldc = field or {LANTERN_TEAL: '123230', ROT_CRIMSON: '4a141e'}.get(a, a)
    mat = env.rug_mat(fieldc, b, 'd9ccb0', w=w, d=d)
    o = geo.box('Rug', (w, d, 0.02), (0, 0, 0.012), mat, C['Props'], bevel=0.005)
    env.store_local(o)
    env.finish(o, uv=None, local=False)
    return env.group_xform([o], loc, rot)


def cart(loc=(0, 0, 0), rot=0.0, M=None, seed=0, load='sacks'):
    """Two-wheeled hand cart."""
    M = M or pm()
    out = []
    _p(geo.box('Cart bed', (1.8, 1.2, 0.1), (0, 0, 0.75), M['planks'], C['Props'], bevel=0.015), out)
    for sy in (-1, 1):
        _p(geo.box('Cart side', (1.8, 0.06, 0.35), (0, sy * 0.6, 0.95), M['planks'], C['Props'], bevel=0.01), out)
    out += wheel((0, 0.72, 0.55), 0.55, 0.08, M)
    out += wheel((0, -0.72, 0.55), 0.55, 0.08, M)
    for sy in (-1, 1):
        _p(geo.tube('Handle', [_v(0.9, sy * 0.45, 0.78), _v(2.1, sy * 0.4, 0.25)], 0.04, M['wood_dark'], C['Props'],
                    sides=6), out, uv=None)
    if load == 'sacks':
        out += sack((-0.4, 0.2, 0.8), 0.3, 0.5, M, seed=seed)
        out += sack((0.3, -0.2, 0.8), 1.2, 0.5, M, seed=seed + 1)
    elif load == 'barrels':
        out += barrel((-0.3, 0.0, 0.8), 0, 0.6, 0.24, M)
        out += barrel((0.4, 0.15, 0.8), 0, 0.6, 0.24, M)
    return _fin(out, loc, rot)


def signpost(loc=(0, 0, 0), rot=0.0, M=None, arms=2):
    M = M or pm()
    out = []
    _p(geo.box('Signpost', (0.12, 0.12, 2.6), (0, 0, 1.3), M['wood_dark'], C['Props'], bevel=0.01), out)
    for i in range(arms):
        a = 0.6 if i % 2 else -0.5
        o = geo.extrude('Sign arm', [(0, -0.12), (0.9, -0.12), (1.05, 0), (0.9, 0.12), (0, 0.12)], 0.05, M['wood'],
                        C['Props'], plane='XZ', bevel=0.01)
        geo.transform(o, Matrix.Translation((0, 0, 2.2 - i * 0.35)) @ Matrix.Rotation(a, 4, 'Z'))
        _p(o, out)
    return _fin(out, loc, rot)


# ------------------------------------------------------------------ interior extras


def chandelier(pos, r=1.0, candles=10, M=None, power=60.0):
    """Iron ring chandelier with candles, hung on chains."""
    M = M or pm()
    out = []
    pos = Vector(pos)
    ring = geo.lathe('Chandelier ring', [(r - 0.04, -0.03), (r + 0.04, -0.03), (r + 0.04, 0.03), (r - 0.04, 0.03)], 32,
                     M['iron'], C['Props'], close_top=False, close_bottom=False, location=pos)
    mod = ring.modifiers.new('Solid', 'SOLIDIFY')
    mod.thickness = 0.03
    out.append(ring)
    for k in range(3):
        a = k * math.tau / 3
        out.append(geo.tube('Chain', [pos + _v(math.cos(a) * r, math.sin(a) * r, 0), pos + _v(0, 0, 1.6)], 0.012,
                            M['iron'], C['Props'], sides=4))
    out.append(geo.tube('Chain', [pos + _v(0, 0, 1.6), pos + _v(0, 0, 6.0)], 0.015, M['iron'], C['Props'], sides=4))
    for k in range(candles):
        a = k * math.tau / candles
        out += candle(pos + _v(math.cos(a) * r, math.sin(a) * r, 0.03), 0.16, 0.025, M)
    env.point(pos + _v(0, 0, 0.3), power, 'ffb45a', r * 0.8, name='Chandelier light')
    return out


def brazier(loc, M=None, power=300.0, color='ff8a32', h=1.0):
    M = M or pm()
    out = []
    flame = M['flame'] if color == 'ff8a32' else env.flame_mat(f'Witchfire {color}', 1.8, edge=color, mid=color,
                                                               core='d8ffd0')
    loc = Vector(loc)
    bowl = geo.lathe('Brazier bowl', [(0.12, h - 0.25), (0.42, h - 0.05), (0.5, h + 0.1)], 16, M['iron'], C['Props'],
                     close_top=False, location=loc)
    mod = bowl.modifiers.new('Solid', 'SOLIDIFY')
    mod.thickness = 0.03
    out.append(bowl)
    for k in range(3):
        a = k * math.tau / 3
        out.append(geo.tube('Brazier leg', [loc + _v(math.cos(a) * 0.36, math.sin(a) * 0.36, 0),
                                            loc + _v(math.cos(a) * 0.2, math.sin(a) * 0.2, h - 0.2)], 0.03, M['iron'],
                            C['Props'], sides=5))
    coals = geo.quadsphere('Brazier coals', 0.4, (0, 0, 0), M['ember'], C['Props'], level=2, scale=(1, 1, 0.3))
    geo.displace_noise(coals, 0.05, 6, seed=1)
    geo.place(coals, loc + _v(0, 0, h + 0.02))
    out.append(coals)
    for k in range(3):
        a = k * 2.1
        fl = geo.lathe('Brazier flame', [(0, 0), (0.16, 0.1), (0.12, 0.35), (0.04, 0.6), (0, 0.7)], 8, flame,
                       C['FX'], location=loc + _v(math.cos(a) * 0.12, math.sin(a) * 0.12, h + 0.05))
        geo.displace_noise(fl, 0.04, 8, seed=k)
        out.append(fl)
    env.point(loc + _v(0, 0, h + 0.6), power, color, 0.25, name='Brazier light')
    return out


def wall_shield(pos, normal=(0, -1, 0), M=None, size=1.0, emblem='lantern', paint=None):
    """Heater shield mounted on a wall, face along `normal`."""
    M = M or pm()
    mats = dict(paint=paint or S.paint(LANTERN_TEAL, name='Shield teal'), trim=M['brass'])
    em = W.emblem_lantern(M['brass'], M['flame']) if emblem == 'lantern' else (
        W.emblem_skull(M['bone'], M['plague']) if emblem == 'skull' else W.emblem_sun(M['brass']))
    d = W.heater_shield(mats, C['Props'], size=size * 1.6, emblem=em)
    nrm = Vector(normal).normalized()
    ang = math.atan2(nrm.y, nrm.x)
    for o in d['objs']:
        geo.transform(o, Matrix.Translation(Vector(pos)) @ Matrix.Rotation(ang, 4, 'Z'))
        env.finish(o, uv=None)
    return d['objs']


def crossed_swords(pos, normal=(0, -1, 0), M=None, length=1.0):
    M = M or pm()
    out = []
    wm = dict(grip=M['leather'], hilt=M['brass'], blade=S.metal('c9cfd2', name='Wall blade', rough=0.22))
    nrm = Vector(normal).normalized()
    ang = math.atan2(nrm.y, nrm.x)
    for s in (-1, 1):
        d = W.sword(wm, C['Props'], length=length, guard=0.26)
        for o in d['objs']:
            # sword built along +Z facing +X; tilt in the wall plane
            geo.transform(o, Matrix.Translation(Vector(pos)) @ Matrix.Rotation(ang, 4, 'Z') @
                          Matrix.Rotation(s * 0.6, 4, 'X') @ Matrix.Translation((0, 0, -length * 0.45)))
            env.finish(o, uv=None)
            out.append(o)
    return out


def workbench(loc=(0, 0, 0), rot=0.0, M=None, seed=0, w=2.4):
    M = M or pm()
    rng = random.Random(seed)
    out = table((0, 0, 0), 0.0, w, 0.9, 0.9, M)
    tools = dict(grip=M['leather'], hilt=M['iron'], blade=M['iron'], steel=M['iron'], wood=M['wood'])
    for i in range(3):
        d = W.hammer(tools, C['Props'], haft_len=0.45, head=0.6) if i % 2 == 0 else W.dagger(tools, C['Props'], 0.35)
        for o in d['objs']:
            geo.transform(o, Matrix.Translation((-w / 2 + 0.5 + i * 0.7, rng.uniform(-0.2, 0.2), 0.93)) @
                          Matrix.Rotation(math.pi / 2, 4, 'Y') @ Matrix.Rotation(rng.uniform(-0.4, 0.4), 4, 'X'))
            env.finish(o, uv=None)
            out.append(o)
    # pegboard with hanging tools
    _p(geo.box('Tool board', (w, 0.06, 1.2), (0, 0.5, 1.7), M['planks'], C['Props'], bevel=0.01), out)
    for i in range(5):
        x = -w / 2 + 0.3 + i * (w - 0.6) / 4
        _p(geo.cylinder('Tool handle', 0.025, 0.55, (x, 0.42, 1.65), M['wood'], C['Props'], 6, bevel=0), out, uv=None)
        _p(geo.box('Tool head', (0.14, 0.04, 0.08), (x, 0.42, 1.95), M['iron'], C['Props'], bevel=0.008), out, uv=None)
    return _fin(out, loc, rot)


def grindstone(loc=(0, 0, 0), rot=0.0, M=None):
    M = M or pm()
    out = []
    _p(geo.cylinder('Grindstone', 0.5, 0.16, (0, 0, 0.95), M['stone'], C['Props'], 24, rotation=(90, 0, 0), bevel=0.02),
       out, uv=None)
    for s in (-1, 1):
        _p(geo.box('Grind frame', (0.1, 0.1, 1.0), (0, s * 0.18, 0.5), M['wood_dark'], C['Props'], bevel=0.01), out)
    _p(geo.cylinder('Grind axle', 0.04, 0.6, (0, 0, 0.95), M['iron'], C['Props'], 8, rotation=(90, 0, 0)), out, uv=None)
    _p(geo.box('Trough', (1.1, 0.5, 0.3), (0, 0, 0.3), M['planks'], C['Props'], bevel=0.02), out)
    return _fin(out, loc, rot)


def sparks(centre, n=40, radius=1.2, seed=0, M=None, rise=1.2):
    """Tiny glowing streaks flying off an anvil or forge."""
    M = M or pm()
    rng = random.Random(seed)
    out = []
    mat = env.glow_mat('ffb04a', 25.0, name='Spark', core_color='fff2c0')
    c = Vector(centre)
    for i in range(n):
        a = rng.uniform(0, math.tau)
        el = rng.uniform(0.1, 1.2)
        dvec = Vector((math.cos(a) * math.cos(el), math.sin(a) * math.cos(el), math.sin(el)))
        p0 = c + dvec * rng.uniform(0.05, radius)
        p0.z += rng.uniform(0, rise) * (p0 - c).length / radius
        p1 = p0 + dvec * rng.uniform(0.04, 0.12)
        out.append(geo.tube('Spark', [p0, p1], 0.008, mat, C['FX'], sides=4))
    return out


def map_table(loc=(0, 0, 0), rot=0.0, M=None, seed=0):
    """Campaign table with an unrolled map, pins, candles and a dagger."""
    M = M or pm()
    rng = random.Random(seed)
    out = table((0, 0, 0), 0.0, 2.4, 1.4, 0.9, M)
    mp = geo.grid_sheet('Campaign map', 1, 1, 12, 8, lambda u, v: ((u - 0.5) * 2.0, (v - 0.5) * 1.2,
                                                                     0.92 + 0.03 * (math.exp(-((u - 0.0) * 12) ** 2) +
                                                                                    math.exp(-((u - 1.0) * 12) ** 2))),
                         _map_mat(), C['Props'])
    _p(mp, out, uv=None)
    for s in (-1, 1):
        _p(geo.cylinder('Map roll', 0.045, 1.25, (s * 1.0, 0, 0.96), M['parchment'], C['Props'], 10, rotation=(90, 0, 0),
                        bevel=0), out, uv=None)
    for i in range(7):
        _p(geo.cylinder('Map pin', 0.03, 0.08, (rng.uniform(-0.8, 0.8), rng.uniform(-0.45, 0.45), 0.96),
                        rng.choice([M['teal'], M['crimson'], M['brass']]), C['Props'], 8, bevel=0.01), out, uv=None)
    for x in (-0.9, 0.9):
        out += candle(_v(x, 0.55, 0.92), 0.22, 0.03, M)
    return _fin(out, loc, rot)


def _map_mat():
    from .nodekit import material
    m, n = material('Campaign map')
    p = n.rest()
    base = n.noise(p, 3.0, 4, 0.6)
    col = n.mixc(n.maprange(base, 0.3, 0.7), 'd8c79a', 'b49a68')
    land = n.smooth(n.noise(p, 1.4, 5, 0.6), 0.48, 0.5)
    col = n.mixc(n.mul(land, 0.5), col, '8a8a5a')
    coast = n.mul(n.smooth(n.noise(p, 1.4, 5, 0.6), 0.47, 0.49), n.smooth(n.noise(p, 1.4, 5, 0.6), 0.51, 0.49))
    col = n.mixc(n.mul(coast, 0.8), col, '4a3a28')
    roads = n.smooth(n.voronoi(p, 5.0, 'DISTANCE_TO_EDGE'), 0.012, 0.0)
    col = n.mixc(n.mul(roads, 0.6), col, '7a2a20')
    n.principled(**{'Base Color': col, 'Roughness': 0.8})
    return m


def globe(loc, M=None, r=0.35):
    M = M or pm()
    out = []
    loc = Vector(loc)
    out += [_p(geo.lathe('Globe stand', [(0.3, 0), (0.3, 0.05), (0.06, 0.12), (0.05, 0.75), (0.1, 0.8)], 14, M['wood_dark'],
                         C['Props'], location=loc), [], uv=None)]
    g = geo.sphere('Globe', r, loc + _v(0, 0, 0.8 + r), _map_mat(), C['Props'], 24, 14)
    _p(g, out, uv=None)
    mer = geo.lathe('Meridian', [(r + 0.03, -0.015), (r + 0.05, 0), (r + 0.03, 0.015)], 32, M['brass'], C['Props'],
                    close_top=False, close_bottom=False)
    geo.transform(mer, Matrix.Translation(loc + _v(0, 0, 0.8 + r)) @ Matrix.Rotation(math.pi / 2, 4, 'X'))
    _p(mer, out, uv=None)
    return out


def chair(loc=(0, 0, 0), rot=0.0, M=None, high=False):
    M = M or pm()
    out = []
    _p(geo.box('Seat', (0.5, 0.5, 0.06), (0, 0, 0.47), M['wood'], C['Props'], bevel=0.01), out)
    for sx in (-1, 1):
        for sy in (-1, 1):
            _p(geo.box('Chair leg', (0.05, 0.05, 0.47), (sx * 0.21, sy * 0.21, 0.235), M['wood_dark'], C['Props'],
                       bevel=0.005), out)
    hh = 1.4 if high else 0.95
    _p(geo.box('Chair back', (0.5, 0.06, hh - 0.5), (0, 0.22, 0.5 + (hh - 0.5) / 2), M['wood_dark'], C['Props'],
               bevel=0.01), out)
    return _fin(out, loc, rot)


def lectern(loc=(0, 0, 0), rot=0.0, M=None):
    M = M or pm()
    out = []
    _p(geo.box('Lectern post', (0.18, 0.18, 1.0), (0, 0, 0.5), M['wood_dark'], C['Props'], bevel=0.01), out)
    _p(geo.box('Lectern foot', (0.6, 0.45, 0.08), (0, 0, 0.04), M['wood_dark'], C['Props'], bevel=0.01), out)
    top = geo.box('Lectern top', (0.7, 0.5, 0.05), (0, 0, 0), M['wood'], C['Props'], bevel=0.01)
    geo.transform(top, Matrix.Translation((0, 0, 1.1)) @ Matrix.Rotation(-0.45, 4, 'X'))
    _p(top, out)
    bk = book_stack((0, 0, 0), 1, M, seed=3, open_book=True)
    for o in bk:
        geo.transform(o, Matrix.Translation((0, -0.02, 1.08)) @ Matrix.Rotation(-0.45, 4, 'X'))
    out += bk
    return _fin(out, loc, rot)


def scroll_pile(loc, n=6, M=None, seed=0):
    M = M or pm()
    rng = random.Random(seed)
    out = []
    for i in range(n):
        a = rng.uniform(0, math.pi)
        z = 0.05 + (i // 3) * 0.09
        o = geo.cylinder('Scroll', 0.045, rng.uniform(0.4, 0.6), (0, 0, 0), M['parchment'], C['Props'], 10,
                         rotation=(90, 0, 0), bevel=0.005)
        geo.transform(o, Matrix.Translation(Vector(loc) + _v(rng.uniform(-0.15, 0.15), rng.uniform(-0.1, 0.1), z)) @
                      Matrix.Rotation(a, 4, 'Z'))
        _p(o, out, uv=None)
    return out


def inkwell(loc, M=None):
    M = M or pm()
    out = []
    loc = Vector(loc)
    _p(geo.cylinder('Inkwell', 0.04, 0.06, loc + _v(0, 0, 0.03), M['coal'], C['Props'], 10, bevel=0.005), out, uv=None)
    _p(geo.tube('Quill', [loc + _v(0, 0, 0.04), loc + _v(0.05, 0.02, 0.32)], [0.004, 0.02], M['canvas'], C['Props'],
                sides=4, flatten=0.25), out, uv=None)
    return out


def gem_cluster(loc, n=7, color='2fd1c0', seed=0, size=0.35, power=0.0):
    """Faceted glowing crystals (vault gems / runic shards)."""
    rng = random.Random(seed)
    mat = S.gem(color, name=f'Gem {color}', glow=0.8)
    out = []
    loc = Vector(loc)
    for i in range(n):
        a = rng.uniform(0, math.tau)
        tilt = rng.uniform(0.0, 0.6)
        L = size * rng.uniform(0.5, 1.2)
        r = L * 0.22
        o = geo.lathe('Crystal', [(0, -0.02), (r, L * 0.15), (r, L * 0.75), (0, L)], 6, mat, C['Props'], smooth=False)
        geo.transform(o, Matrix.Translation(loc + _v(math.cos(a) * size * 0.25, math.sin(a) * size * 0.25, 0)) @
                      Matrix.Rotation(a, 4, 'Z') @ Matrix.Rotation(tilt, 4, 'Y'))
        _p(o, out, uv=None)
    if power:
        env.point(loc + _v(0, 0, size * 0.6), power, color, 0.2, name='Gem glow')
    return out


def goblet(loc, M=None, h=0.22):
    M = M or pm()
    o = geo.lathe('Goblet', [(0, 0), (0.06 * h / 0.22, 0), (0.015, 0.03), (0.012, h * 0.5), (0.06, h * 0.7),
                             (0.07, h)], 14, M['gold'], C['Props'], close_top=False, location=loc)
    return [_p(o, [], uv=None)]


def vault_door(loc=(0, 0, 0), rot=0.0, M=None, r=2.2):
    """Round iron vault door with radial bolts and a brass wheel, set in a stone ring (faces -Y)."""
    M = M or pm()
    out = []
    ring = geo.cylinder('Vault ring', r + 0.5, 0.6, (0, 0, 0), env.masonry('Vault ring', '6a645c', '5a554e', '2e2a26',
                                                                          block=(0.5, 0.35)),
                        C['Props'], 40, rotation=(90, 0, 0), bevel=0.04)
    _p(ring, out)
    door = geo.cylinder('Vault door', r, 0.4, (0, -0.25, 0), M['iron'], C['Props'], 40, rotation=(90, 0, 0), bevel=0.05)
    _p(door, out, uv=None)
    for k in range(16):
        a = k * math.tau / 16
        _p(geo.cylinder('Bolt', 0.09, 0.12, (math.cos(a) * (r - 0.25), -0.48, math.sin(a) * (r - 0.25)), M['brass'],
                        C['Props'], 10, rotation=(90, 0, 0), bevel=0.02), out, uv=None)
    wheel_ = geo.lathe('Wheel', [(0.7, -0.03), (0.78, 0), (0.7, 0.03)], 32, M['brass'], C['Props'], close_top=False,
                       close_bottom=False)
    geo.transform(wheel_, Matrix.Translation((0, -0.6, 0)) @ Matrix.Rotation(math.pi / 2, 4, 'X'))
    mod = wheel_.modifiers.new('Solid', 'SOLIDIFY')
    mod.thickness = 0.05
    _p(wheel_, out, uv=None)
    for k in range(6):
        a = k * math.tau / 6
        _p(geo.tube('Spoke', [_v(0, -0.6, 0), _v(math.cos(a) * 0.72, -0.6, math.sin(a) * 0.72)], 0.035, M['brass'],
                    C['Props'], sides=6), out, uv=None)
    _p(geo.sphere('Hub', 0.14, (0, -0.62, 0), M['brass'], C['Props'], 12, 8), out, uv=None)
    for o in out:
        geo.transform(o, Matrix.Translation((0, 0, r + 0.5)))
    return _fin(out, loc, rot)


def ladder(p0, p1, M=None, w=0.5):
    M = M or pm()
    out = []
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    side = Vector((-d.y, d.x, 0)).normalized() * (w / 2)
    if side.length < 1e-6:
        side = Vector((w / 2, 0, 0))
    for s in (-1, 1):
        out.append(geo.tube('Ladder rail', [p0 + side * s, p1 + side * s], 0.03, M['wood'], C['Props'], sides=6))
    n = int(d.length / 0.32)
    for i in range(1, n):
        q = p0.lerp(p1, i / n)
        out.append(geo.tube('Rung', [q - side, q + side], 0.02, M['wood'], C['Props'], sides=5))
    for o in out:
        env.finish(o, coll=C['Props'], uv=None)
    return out


# ------------------------------------------------------------------ the Lantern Caravan war wagon


def war_wagon(loc=(0, 0, 0), rot=0.0, M=None, cloth=None, trim=None, seed=0, power=35.0, wheel_spin=0.0,
              paint=None, emblem='lantern'):
    """The caravan's iconic war wagon: iron-strapped plank house on spoked wheels, railed top deck with
    brass spike finials, cargo, a pennant, heraldic shields and corner lanterns. Long axis X, door side -Y."""
    M = M or pm()
    rng = random.Random(seed)
    cloth = cloth or M['teal']
    trim = trim or M['brass']
    out = []
    L, Wd = 4.4, 2.2
    z0, hb = 0.95, 2.0
    _p(geo.box('Wagon body', (L, Wd, hb), (0, 0, z0 + hb / 2), M['planks'], C['Props'], bevel=0.03), out)
    _p(geo.box('Wagon floor', (L + 0.3, Wd + 0.2, 0.18), (0, 0, z0 - 0.05), M['wood_dark'], C['Props'], bevel=0.02), out)
    # iron strap frame
    for x in (-L / 2, -L / 6, L / 6, L / 2):
        for sy in (-1, 1):
            beam((x, sy * (Wd / 2 + 0.03), z0), (x, sy * (Wd / 2 + 0.03), z0 + hb), 0.09, M['iron'], out, 'Strap')
    for z in (z0 + 0.12, z0 + hb * 0.55, z0 + hb - 0.08):
        for sy in (-1, 1):
            beam((-L / 2, sy * (Wd / 2 + 0.03), z), (L / 2, sy * (Wd / 2 + 0.03), z), 0.08, M['iron'], out, 'Band')
    # door and windows on the -Y side
    slab((-0.1, -Wd / 2 - 0.05, z0 + 0.85), (1, 0, 0), (0, 0, 1), 0.8, 1.4, 0.08, M['wood'], out, 'Wagon door')
    for sy in (-1, 1):
        for x in (-1.45, 1.25):
            slab((x, sy * (Wd / 2 + 0.04), z0 + 1.2), (1, 0, 0), (0, 0, 1), 0.55, 0.55, 0.06,
                 env.window_glass('ffb04a', 2.5), out, 'Wagon window', bevel=0)
            slab((x, sy * (Wd / 2 + 0.07), z0 + 0.88), (1, 0, 0), (0, 0, 1), 0.75, 0.08, 0.14, M['wood_dark'], out,
                 'Sill')
    # awning over the right window
    aw = geo.grid_sheet('Wagon awning', 1, 1, 8, 3, lambda u, v: (1.25 + (u - 0.5) * 1.1, -Wd / 2 - 0.05 - v * 0.45,
                                                                  z0 + 1.68 - v * 0.25), cloth, C['Props'])
    mod = aw.modifiers.new('Thick', 'SOLIDIFY')
    mod.thickness = 0.02
    out.append(aw)
    # top deck with rail and spikes
    zt = z0 + hb
    _p(geo.box('Deck', (L + 0.25, Wd + 0.25, 0.14), (0, 0, zt + 0.07), M['wood_dark'], C['Props'], bevel=0.02), out)
    posts = []
    for x in (-L / 2, -L / 4, 0, L / 4, L / 2):
        for sy in (-1, 1):
            posts.append((x, sy * (Wd / 2 + 0.05)))
    for sx in (-1, 1):
        for y in (-Wd / 4, Wd / 4):
            posts.append((sx * (L / 2 + 0.05), y))
    for (x, y) in posts:
        _p(geo.cylinder('Rail post', 0.04, 0.6, (x, y, zt + 0.44), M['iron'], C['Props'], 6, bevel=0), out, uv=None)
        _p(geo.lathe('Spike', [(0.06, 0), (0.07, 0.05), (0.0, 0.26)], 8, trim, C['Props'], location=(x, y, zt + 0.74)),
           out, uv=None)
    for sy in (-1, 1):
        beam((-L / 2, sy * (Wd / 2 + 0.05), zt + 0.62), (L / 2, sy * (Wd / 2 + 0.05), zt + 0.62), 0.06, M['iron'], out,
             'Rail')
        beam((-L / 2, sy * (Wd / 2 + 0.05), zt + 0.36), (L / 2, sy * (Wd / 2 + 0.05), zt + 0.36), 0.05, M['wood'], out,
             'Rail')
    for sx in (-1, 1):
        beam((sx * (L / 2 + 0.05), -Wd / 2, zt + 0.62), (sx * (L / 2 + 0.05), Wd / 2, zt + 0.62), 0.06, M['iron'], out,
             'Rail')
    # cargo on deck
    out += barrel((-L / 2 + 0.5, 0.45, zt + 0.14), 0, 0.6, 0.24, M)
    out += barrel((-L / 2 + 0.5, -0.2, zt + 0.14), 0, 0.6, 0.24, M)
    out += chest((0.1, 0.1, zt + 0.14), 0.0, 0.8, 0.55, 0.5, M)
    out += chest((0.95, 0.1, zt + 0.14), 0.0, 0.8, 0.55, 0.5, M)
    roll = geo.cylinder('Bedroll', 0.2, 1.0, (0.5, 0.0, zt + 0.85), cloth, C['Props'], 12, rotation=(0, 90, 0), bevel=0.04)
    _p(roll, out, uv=None)
    # pennant
    _p(geo.cylinder('Flag pole', 0.04, 2.4, (-L / 2 + 1.1, 0.4, zt + 1.3), M['wood_dark'], C['Props'], 6, bevel=0), out,
       uv=None)
    _p(geo.lathe('Pole tip', [(0.06, 0), (0.0, 0.25)], 8, trim, C['Props'], location=(-L / 2 + 1.1, 0.4, zt + 2.5)), out,
       uv=None)
    fl = geo.grid_sheet('Pennant', 1, 1, 12, 4, lambda u, v: (-L / 2 + 1.12 + u * 1.7,
                                                               0.4 + 0.12 * math.sin(u * 5.0 + wheel_spin) * u,
                                                               zt + 2.35 - v * 0.8 * (1 - 0.35 * u) -
                                                               (0.25 * u * (1 if v > 0.5 else 0) * (v - 0.5) * 2)),
                        cloth, C['Props'])
    mod = fl.modifiers.new('Thick', 'SOLIDIFY')
    mod.thickness = 0.02
    out.append(fl)
    # heraldic shields on the flanks
    em = W.emblem_lantern(trim, M['flame']) if emblem == 'lantern' else W.emblem_sun(trim)
    shield_paint = paint or S.paint(LANTERN_TEAL, name='Wagon shield')
    for sy in (-1, 1):
        for x in (-1.0, 0.9):
            d = W.heater_shield(dict(paint=shield_paint, trim=trim), C['Props'], size=1.0, emblem=em)
            for o in d['objs']:
                geo.transform(o, Matrix.Translation((x, sy * (Wd / 2 + 0.12), zt + 0.12)) @
                              Matrix.Rotation(sy * math.pi / 2, 4, 'Z'))
                env.finish(o, uv=None)
                out.append(o)
    # front: driver's footboard, lamp hooks and a crest shield
    _p(geo.box('Footboard', (0.4, Wd + 0.3, 0.08), (L / 2 + 0.25, 0, z0 + 0.25), M['wood_dark'], C['Props'], bevel=0.01),
       out)
    d = W.heater_shield(dict(paint=shield_paint, trim=trim), C['Props'], size=1.1, emblem=em)
    for o in d['objs']:
        geo.transform(o, Matrix.Translation((L / 2 + 0.12, 0, z0 + hb * 0.6)))
        env.finish(o, uv=None)
        out.append(o)
    for sy in (-1, 1):
        base = _v(L / 2 + 0.05, sy * (Wd / 2 - 0.15), zt + 0.1)
        out += hanging_lantern(base + _v(0.25, 0, 0.0), M, power=0.0, size=1.1, chain=0.15)
        _p(geo.tube('Front bracket', [base, base + _v(0.28, 0, 0.1)], 0.02, M['iron'], C['Props'], sides=5), out, uv=None)
    # wheels and hitch
    for sx in (-1, 1):
        for sy in (-1, 1):
            out += wheel((sx * L * 0.3, sy * (Wd / 2 + 0.16), 0.78), 0.78, 0.14, M, spokes=12)
    _p(geo.box('Hitch', (1.4, 0.18, 0.14), (L / 2 + 0.6, 0, 0.6), M['wood_dark'], C['Props'], bevel=0.02), out)
    _p(geo.box('Step', (0.9, 0.35, 0.08), (-0.1, -Wd / 2 - 0.25, z0 - 0.2), M['wood_dark'], C['Props'], bevel=0.01), out)
    # corner lanterns
    lamps = []
    mats = dict(hilt=trim, glow=M['flame'], lamp_glass=M['lamp_glass'])
    for (x, y) in ((-L / 2 - 0.05, -Wd / 2 - 0.05), (L / 2 + 0.05, -Wd / 2 - 0.05)):
        base = _v(x, y, z0 + hb - 0.25)
        _p(geo.tube('Lamp bracket', [base, base + _v(0.0, -0.3, 0.1)], 0.025, M['iron'], C['Props'], sides=5), out, uv=None)
        objs = hanging_lantern(base + _v(0, -0.32, 0.1), M, power=0.0, size=1.2, chain=0.12)
        out += objs
        lamps.append(base + _v(0, -0.32, -0.1))
    # remove the auto lights created by hanging_lantern (power 0) and add our own after the transform
    _fin(out, loc, rot)
    mw = Matrix.Translation(Vector(loc)) @ Matrix.Rotation(rot, 4, 'Z')
    for p in lamps:
        if power:
            env.point(mw @ p, power, AMBER, 0.06, name='Wagon lamp')
    return out


# ------------------------------------------------------------------ crowds, runes, battlefield & blight


def crowd(points, rng, colors=None, scale=(0.92, 1.08), seated=True, protos=None):
    """Instance varied cheering figures at (x, y, z, facing) points (facing = yaw so local -Y looks that way)."""
    from . import figures
    protos = protos or figures.crowd_protos(24, seated=seated, seed=7 if seated else 8, colors=colors)
    for (x, y, z, face) in points:
        env.inst(rng.choice(protos), (x, y, z), face + rng.uniform(-0.3, 0.3), rng.uniform(*scale), C['Props'])


GLYPHS = [
    [[(0.2, 0), (0.2, 1)], [(0.2, 0.62), (0.75, 0.95)], [(0.2, 0.32), (0.75, 0.65)]],
    [[(0.25, 0), (0.25, 1)], [(0.25, 0.75), (0.75, 0.5), (0.25, 0.25)]],
    [[(0.75, 1), (0.25, 0.5), (0.75, 0)]],
    [[(0.5, 0), (0.5, 1)], [(0.15, 0.66), (0.5, 1), (0.85, 0.66)]],
    [[(0.5, 0), (0.5, 1)], [(0.12, 1), (0.5, 0.55), (0.88, 1)]],
    [[(0.75, 1), (0.25, 0.62), (0.75, 0.38), (0.25, 0)]],
    [[(0.15, 0), (0.75, 0.55), (0.5, 0.95), (0.25, 0.55), (0.85, 0)]],
    [[(0.5, 0.05), (0.85, 0.5), (0.5, 0.95), (0.15, 0.5), (0.5, 0.05)]],
]


def glyph_strokes(origin, u, v, size, glyph, mat, width=0.05, out=None):
    """Emissive rune strokes on a plane spanned by unit vectors u (right) and v (up)."""
    out = out if out is not None else []
    o = Vector(origin)
    u, v = Vector(u), Vector(v)
    nrm = u.cross(v).normalized()
    for stroke in glyph:
        pts = [o + u * ((gx - 0.5) * size * 0.6) + v * (gy * size) - nrm * 0.0 for gx, gy in stroke]
        for a, b in zip(pts, pts[1:]):
            d = b - a
            if d.length < 1e-4:
                continue
            ob = geo.box('Rune stroke', (d.length + width, width, 0.03), (0, 0, 0), mat, C['FX'], bevel=0)
            q = Vector((1, 0, 0)).rotation_difference(d.normalized())
            m = Matrix.Translation((a + b) / 2) @ q.to_matrix().to_4x4()
            geo.transform(ob, m)
            out.append(ob)
    return out


def rune_stone(loc=(0, 0, 0), rot=0.0, h=3.0, glow=None, seed=0, power=40.0, color='6ff0d2', mat=None):
    """Standing stone with glowing runes carved on its camera-facing side (-Y)."""
    rng = random.Random(seed)
    glow = glow or env.glow_mat(color, 6.0, name=f'Rune glow {color}', core_color='e8fff8')
    mat = mat or env.smooth_stone('6e6a62', name='Rune stone', moss=0.6)
    out = []
    o = geo.box('Standing stone', (1.0, 0.6, h), (0, 0, h / 2 - 0.2), mat, C['Props'], bevel=0.12)
    geo.apply_modifiers(o)
    for v in o.data.vertices:
        t = max(0.0, v.co.z / h)
        v.co.x *= 1 - 0.3 * t
        v.co.y *= 1 - 0.2 * t
        if v.co.z > h * 0.8:
            v.co.z -= abs(v.co.x) * 0.6
    o.data.update()
    geo.displace_noise(o, 0.08, 1.5, seed=seed)
    _p(o, out, uv=None)
    for i in range(3):
        g = GLYPHS[(seed + i) % len(GLYPHS)]
        glyph_strokes((0, -0.31 + 0.0, h * (0.2 + i * 0.24)), (1, 0, 0), (0, 0, 1), h * 0.16, g, glow, 0.045, out)
    _fin(out, loc, rot)
    if power:
        p = Matrix.Translation(Vector(loc)) @ Matrix.Rotation(rot, 4, 'Z') @ _v(0, -0.9, h * 0.45)
        env.point(p, power, color, 0.3, name='Rune glow')
    return out


def stuck_weapon(loc, kind='sword', M=None, seed=0, lean=None):
    """A weapon driven into the ground at an angle (battlefield litter)."""
    M = M or pm()
    rng = random.Random(seed)
    wm = dict(grip=M['leather'], hilt=M['brass'], blade=S.metal('9a9e9a', name='Battle blade', rough=0.35, wear=0.2),
              steel=M['iron'], wood=M['wood_dark'], cloth=M['teal'])
    if kind == 'sword':
        d = W.sword(wm, C['Props'], length=0.95)
        flip = True
    elif kind == 'spear':
        d = W.spear(wm, C['Props'], length=2.2, grip_at=0.0)
        flip = False
    else:
        d = W.axe(wm, C['Props'], haft_len=1.0)
        flip = False
    lean = lean if lean is not None else rng.uniform(0.15, 0.5)
    a = rng.uniform(0, math.tau)
    out = []
    for o in d['objs']:
        m = Matrix.Translation(Vector(loc)) @ Matrix.Rotation(a, 4, 'Z') @ Matrix.Rotation(lean, 4, 'X')
        if flip:
            m = m @ Matrix.Translation((0, 0, 0.75)) @ Matrix.Rotation(math.pi, 4, 'X')
        else:
            m = m @ Matrix.Translation((0, 0, -0.3))
        geo.transform(o, m)
        env.finish(o, uv=None)
        out.append(o)
    return out


def fallen_shield(loc, M=None, seed=0, paint=None, emblem='lantern'):
    M = M or pm()
    rng = random.Random(seed)
    paint = paint or S.paint(LANTERN_TEAL if emblem == 'lantern' else ROT_CRIMSON, name=f'Fallen shield {emblem}',
                             chip=0.8)
    em = W.emblem_lantern(M['brass']) if emblem == 'lantern' else W.emblem_skull(M['bone'])
    d = W.heater_shield(dict(paint=paint, trim=M['brass'] if emblem == 'lantern' else M['iron']), C['Props'], size=1.3,
                        emblem=em)
    out = []
    tilt = rng.uniform(1.2, 1.5)
    for o in d['objs']:
        geo.transform(o, Matrix.Translation(Vector(loc) + _v(0, 0, 0.12)) @ Matrix.Rotation(rng.uniform(0, 6.28), 4, 'Z') @
                      Matrix.Rotation(tilt, 4, 'Y'))
        env.finish(o, uv=None)
        out.append(o)
    return out


def torn_banner(loc, cloth=None, M=None, h=3.6, seed=0, lean=0.2):
    """Broken standard: snapped pole leaning, tattered cloth."""
    M = M or pm()
    rng = random.Random(seed)
    cloth = cloth or M['teal']
    out = []
    a = rng.uniform(0, math.tau)
    top = Vector(loc) + _v(math.cos(a) * h * lean, math.sin(a) * h * lean, h)
    out.append(geo.tube('Broken pole', [Vector(loc) - _v(0, 0, 0.3), top], [0.05, 0.04], M['wood_dark'], C['Props'],
                        sides=6))

    def fn(u, v):
        x = 0.05 + u * 1.0
        z = -v * 1.5
        if v > 0.55:
            k = (v - 0.55) / 0.45
            z += k * 0.6 * abs(math.sin(u * 9 + seed))
        y = 0.08 * math.sin(u * 4 + v * 3)
        return (x, y, z)
    sh = geo.grid_sheet('Torn cloth', 1, 1, 10, 10, fn, cloth, C['Props'])
    mod = sh.modifiers.new('Thick', 'SOLIDIFY')
    mod.thickness = 0.015
    geo.transform(sh, Matrix.Translation(top - _v(0, 0, 0.1)) @ Matrix.Rotation(a + math.pi / 2, 4, 'Z'))
    out.append(sh)
    for o in out:
        env.finish(o, coll=C['Props'], uv=None)
    return out


def blight_pool(loc, r=2.0, color='9cf25c', power=80.0, name='Blight pool'):
    """Glowing plague ooze puddle with a sickly light."""
    from .nodekit import material
    m, n = material(name)
    p = n.rest()
    nz = n.noise(p, 1.5, 4, 0.6)
    em = n.maprange(nz, 0.35, 0.7, 0.4, 2.2)
    n.principled(**{'Base Color': '2a3a14', 'Roughness': 0.12, 'Emission Color': color, 'Emission Strength': em,
                    'Coat Weight': 0.5})
    o = geo.quadsphere(name, 1.0, (0, 0, 0), m, C['Props'], level=3, scale=(r, r * 0.8, 0.05))
    geo.displace_noise(o, 0.15, 1.5, seed=int(r * 7))
    geo.place(o, Vector(loc) + _v(0, 0, 0.02))
    env.point(Vector(loc) + _v(0, 0, 0.6), power, color, r * 0.5, name='Blight glow')
    return [o]


def coffin(loc, rot=0.0, M=None, open_=False):
    M = M or pm()
    out = []
    o = geo.extrude('Coffin', [(-0.25, -0.95), (0.25, -0.95), (0.35, 0.45), (0.22, 0.95), (-0.22, 0.95), (-0.35, 0.45)],
                    0.45, M['wood_dark'], C['Props'], plane='XY', bevel=0.02)
    geo.place(o, (0, 0, 0.22))
    _p(o, out)
    return _fin(out, loc, rot)


def catapult_wreck(loc, rot=0.0, M=None, seed=0):
    """Burnt, collapsed siege engine frame."""
    M = M or pm()
    rng = random.Random(seed)
    charred = S.wood('2a1e16', name='Charred timber', dark=0.4, scale=0.6)
    out = []
    for sy in (-1, 1):
        beam((-2.0, sy * 0.9, 0.3), (2.0, sy * 0.9, 0.3), 0.3, charred, out, 'Frame rail')
        beam((-0.4, sy * 0.9, 0.3), (0.3, sy * 0.8, 2.6), 0.25, charred, out, 'Upright')
    beam((0.3, -0.9, 2.5), (0.3, 0.9, 2.5), 0.25, charred, out, 'Cross')
    beam((0.3, 0, 2.4), (-2.6, 0.3, 0.2), 0.3, charred, out, 'Throwing arm')
    for sx in (-1.6, 1.6):
        out += wheel((sx, -1.1, 0.5), 0.5, 0.12, M)
    for i in range(6):
        beam((rng.uniform(-2.5, 2.5), rng.uniform(-2, 2), 0.1), (rng.uniform(-2.5, 2.5), rng.uniform(-2, 2), 0.15), 0.18,
             charred, out, 'Splinter')
    return _fin(out, loc, rot)


def tool_wall(pos, normal=(0, -1, 0), w=2.4, M=None, seed=0):
    """Smith's rack on a wall: a rail with hanging tongs, hammers and finished blades."""
    M = M or pm()
    rng = random.Random(seed)
    out = []
    nrm = Vector(normal).normalized()
    u = Vector((-nrm.y, nrm.x, 0))
    c = Vector(pos)
    beam(c - u * (w / 2), c + u * (w / 2), 0.1, M['wood_dark'], out, 'Tool rail')
    tools = dict(grip=M['leather'], hilt=M['iron'], blade=S.metal('b8bec2', name='Fresh blade', rough=0.3),
                 steel=M['iron'], wood=M['wood'])
    n = int(w / 0.32)
    for i in range(n):
        p = c - u * (w / 2 - 0.2) + u * (i * (w - 0.4) / max(1, n - 1)) + nrm * 0.08
        k = rng.choice(['tongs', 'hammer', 'blade', 'tongs'])
        if k == 'tongs':
            for s in (-1, 1):
                out.append(geo.tube('Tongs', [p, p + u * (s * 0.04) - Vector((0, 0, 0.55))], 0.012, M['iron'],
                                    C['Props'], sides=4))
        elif k == 'hammer':
            out.append(geo.tube('Hammer haft', [p, p - Vector((0, 0, 0.45))], 0.018, M['wood'], C['Props'], sides=5))
            out.append(geo.box('Hammer head', (0.16, 0.06, 0.07), tuple(p - Vector((0, 0, 0.47))), M['iron'],
                               C['Props'], bevel=0.01))
        else:
            out.append(geo.box('Hanging blade', (0.06, 0.012, 0.8), tuple(p - Vector((0, 0, 0.45))), tools['blade'],
                               C['Props'], bevel=0.004))
    for o in out:
        env.finish(o, coll=C['Props'], uv=None)
    return out


def coal_heap(loc, r=0.9, M=None, seed=0):
    M = M or pm()
    o = geo.quadsphere('Coal heap', 1.0, (0, 0, 0), M['coal'], C['Props'], level=3, scale=(r, r * 0.8, r * 0.45))
    for v in o.data.vertices:
        if v.co.z < 0:
            v.co.z *= 0.05
    o.data.update()
    geo.displace_noise(o, r * 0.12, 3.0 / r, seed=seed)
    geo.place(o, loc)
    return [_p(o, [], uv=None)]


def trough(loc, rot=0.0, M=None, L=1.6):
    """Quench trough: plank box with dark water."""
    M = M or pm()
    out = []
    _p(geo.box('Trough', (L, 0.6, 0.55), (0, 0, 0.275), M['planks'], C['Props'], bevel=0.02), out)
    _p(geo.box('Quench water', (L - 0.12, 0.48, 0.02), (0, 0, 0.5), env.water('Quench water', '14201e', '2a3a36'),
               C['Props'], bevel=0), out, uv=None)
    for x in (-L / 2 + 0.1, L / 2 - 0.1):
        _p(geo.box('Trough band', (0.05, 0.64, 0.58), (x, 0, 0.28), M['iron'], C['Props'], bevel=0.005), out, uv=None)
    return _fin(out, loc, rot)


def bellows(loc, rot=0.0, M=None):
    M = M or pm()
    out = []
    body = geo.extrude('Bellows', [(-0.5, 0), (0.45, -0.32), (0.55, 0), (0.45, 0.32)], 0.28, M['leather'], C['Props'],
                       plane='XY', bevel=0.03)
    geo.place(body, (0, 0, 0.95))
    _p(body, out, uv=None)
    _p(geo.tube('Nozzle', [_v(-0.5, 0, 0.95), _v(-0.95, 0, 0.9)], [0.06, 0.03], M['brass'], C['Props'], sides=8), out,
       uv=None)
    for sx in (-0.3, 0.3):
        _p(geo.box('Bellows stand', (0.08, 0.5, 0.8), (sx, 0, 0.4), M['wood_dark'], C['Props'], bevel=0.01), out)
    return _fin(out, loc, rot)


# ------------------------------------------------------------------ field-edge dressing (battlefields & maps)


def dry_wall(p0, p1, h=0.9, M=None, seed=0, height_fn=None, mat=None):
    """Rough dry-stone field wall: a row of chunky jittered stones capped with flat slabs."""
    rng = random.Random(seed)
    mat = mat or env.field_stone(0.4, '8a8274')
    out = []
    p0, p1 = Vector(p0).to_3d(), Vector(p1).to_3d()
    d = p1 - p0
    L = d.length
    u = d.normalized()
    nrm = Vector((-u.y, u.x, 0))
    hf = height_fn or (lambda x, y: 0.0)
    n = max(2, int(L / 0.42))
    for layer in range(3):
        z = 0.12 + layer * h / 3
        off = (layer % 2) * 0.21
        for i in range(n):
            t = (i * 0.42 + off) / L
            if t > 1:
                break
            c = p0 + d * t
            s = rng.uniform(0.3, 0.42)
            o = geo.box('Wall stone', (s * 1.2, 0.5, h / 3 + 0.05), (0, 0, 0), mat, C['Props'], bevel=0.06)
            geo.apply_modifiers(o)
            geo.displace_noise(o, 0.04, 6, seed=seed * 100 + layer * 37 + i)
            geo.transform(o, Matrix.Translation(c + Vector((0, 0, hf(c.x, c.y) + z)) + nrm * rng.uniform(-0.05, 0.05)) @
                          Matrix.Rotation(math.atan2(u.y, u.x) + rng.uniform(-0.15, 0.15), 4, 'Z'))
            _p(o, out, uv=None)
    return out


def haystack(loc, r=1.4, h=2.2, M=None, seed=0):
    mat = env.thatch('Hay', 'c8a85a', '7a6438')
    o = geo.lathe('Haystack', [(r, 0), (r * 1.02, h * 0.35), (r * 0.8, h * 0.7), (r * 0.35, h * 0.95), (0, h)], 16, mat,
                  C['Props'])
    geo.subdivide(o, 1)
    geo.displace_noise(o, r * 0.06, 2.0, seed=seed)
    env.store_local(o)
    env.uv_cyl(o, scale=1.0)
    geo.place(o, loc)
    return [_p(o, [], uv=None)]


def stake_line(p0, p1, M=None, spacing=0.45, h=1.4, seed=0, height_fn=None):
    """Sharpened anti-cavalry stakes / palisade stubs leaning outward."""
    M = M or pm()
    rng = random.Random(seed)
    out = []
    p0, p1 = Vector(p0).to_3d(), Vector(p1).to_3d()
    d = p1 - p0
    n = max(1, int(d.length / spacing))
    hf = height_fn or (lambda x, y: 0.0)
    nrm = Vector((-d.y, d.x, 0)).normalized()
    for i in range(n + 1):
        c = p0 + d * (i / n)
        base = c + Vector((0, 0, hf(c.x, c.y) - 0.2))
        top = base + Vector((0, 0, h * rng.uniform(0.8, 1.1))) + nrm * rng.uniform(0.2, 0.45)
        out.append(geo.tube('Stake', [base, top - (top - base).normalized() * 0.25, top], [0.07, 0.07, 0.005],
                            M['wood_dark'], C['Props'], sides=6))
    for o in out:
        env.finish(o, coll=C['Props'], uv=None)
    return out


def standing_stone(loc, h=2.4, seed=0, mat=None, lean=0.08):
    rng = random.Random(seed)
    mat = mat or env.smooth_stone('7e786c', name='Menhir', moss=0.6)
    o = geo.box('Standing stone', (0.9, 0.6, h), (0, 0, h / 2 - 0.2), mat, C['Props'], bevel=0.12)
    geo.apply_modifiers(o)
    for v in o.data.vertices:
        t = max(0.0, v.co.z / h)
        v.co.x *= 1 - 0.35 * t
        v.co.y *= 1 - 0.25 * t
    o.data.update()
    geo.displace_noise(o, 0.07, 1.6, seed=seed)
    geo.transform(o, Matrix.Translation(Vector(loc)) @ Matrix.Rotation(rng.uniform(0, 6.28), 4, 'Z') @
                  Matrix.Rotation(rng.uniform(-lean, lean), 4, 'X'))
    return [_p(o, [], uv=None)]
