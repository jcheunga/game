"""Weapons, shields, tools and magical implements.

Canonical frame: the primary grip is at the origin, the working end points +Z,
blade width runs along X and thickness along Y (flat faces the battle camera).
Builders return dict(objs=[...], tip=Vector, grip2=Vector|None, kind=str).
"""
import math

import bmesh
from mathutils import Matrix, Vector

from . import geo
from .heads import catmull


def _v(*a):
    return Vector(a)


def _solid(obj, t, offset=0):
    m = obj.modifiers.new('Thickness', 'SOLIDIFY')
    m.thickness = t
    m.offset = offset
    m.use_even_offset = True
    return obj


def _sub(obj, n=1):
    m = obj.modifiers.new('Smooth', 'SUBSURF')
    m.levels = n
    m.render_levels = n
    return obj


def blade(name, length, width, mat, coll, base_z=0.0, thickness=0.018, tip=0.22, taper=0.35, curve=0.0,
          fuller=True, single_edge=False, flare=0.0):
    """Lenticular blade lofted along +Z, optional curve (+X) and single edge."""
    secs = []
    n = 14
    for k in range(n + 1):
        t = k / n
        z = base_z + length * t
        w = width * (1 - taper * t) * (1 + flare * math.sin(math.pi * t))
        if t > 1 - tip:
            w *= max(0.02, (1 - t) / tip) ** 0.8
        th = thickness * (1 - 0.5 * t)
        cx = curve * length * (t ** 2)
        if single_edge:
            pts = [(-w * .5, 0), (-w * .45, th * .5), (w * .3, th * .45), (w * .5, 0), (w * .3, -th * .45),
                   (-w * .45, -th * .5)]
        else:
            pts = [(-w * .5, 0), (-w * .2, th * .5), (w * .2, th * .5), (w * .5, 0), (w * .2, -th * .5),
                   (-w * .2, -th * .5)]
        secs.append([_v(cx + x, y, z) for x, y in pts])
    b = geo.loft(name, secs, mat, coll, cap=True, smooth=False)
    m = b.modifiers.new('Weighted', 'WEIGHTED_NORMAL')
    m.keep_sharp = True
    for p in b.data.polygons:
        p.use_smooth = True
    return b


def _grip(length, radius, mats, coll, z0=-0.0, wrap=True):
    g = geo.cylinder('Grip', radius, length, (0, 0, z0), mats['grip'], coll, 12, bevel=0.003)
    out = [g]
    if wrap:
        for i in range(int(length / 0.03)):
            z = z0 - length / 2 + 0.015 + i * 0.03
            out.append(geo.lathe('Grip wrap', [(radius * 1.05, z - 0.008), (radius * 1.18, z), (radius * 1.05, z + 0.008)],
                                 12, mats['grip'], coll, close_top=False, close_bottom=False))
    return out


def sword(mats, coll, length=0.85, width=0.075, guard=0.26, grip=0.16, pommel='disc', curve=0.0, single_edge=False,
          guard_style='straight', flare=0.0, rune=None):
    out = []
    out += _grip(grip, 0.02, mats, coll, 0.0)
    gz = grip / 2 + 0.012
    if guard_style == 'straight':
        pts = [_v(-guard / 2, 0, gz - .01), _v(-guard * .3, 0, gz), _v(guard * .3, 0, gz), _v(guard / 2, 0, gz - .01)]
        out.append(geo.tube('Crossguard', catmull(pts, 3), geo.taper(10, 0.018, 0.024), mats['hilt'], coll, sides=8))
        for sx in (-1, 1):
            out.append(geo.sphere('Quillon finial', 0.022, (sx * guard / 2, 0, gz - .012), mats['hilt'], coll, 10, 6))
    elif guard_style == 'swept':
        for sx in (-1, 1):
            pts = [_v(0, 0, gz), _v(sx * guard * .35, 0, gz + .01), _v(sx * guard * .5, 0, gz + .06), _v(sx * guard * .42, 0, gz + .1)]
            out.append(geo.tube('Swept guard', catmull(pts, 3), geo.taper(10, 0.02, 0.01), mats['hilt'], coll, sides=8))
    elif guard_style == 'winged':
        for sx in (-1, 1):
            wing = geo.extrude('Guard wing', [(0, 0), (sx * guard * .5, 0.03), (sx * guard * .55, 0.08), (sx * guard * .2, 0.04)],
                               0.03, mats['hilt'], coll, plane='XZ', bevel=0.008)
            geo.place(wing, (0, 0, gz - .02))
            out.append(wing)
    out.append(geo.box('Guard block', (0.06, 0.045, 0.04), (0, 0, gz), mats['hilt'], coll, bevel=0.01))
    out.append(blade('Blade', length, width, mats['blade'], coll, base_z=gz + .01, curve=curve, single_edge=single_edge,
                     flare=flare))
    pz = -grip / 2 - 0.02
    if pommel == 'disc':
        out.append(geo.cylinder('Pommel', 0.035, 0.03, (0, 0, pz), mats['hilt'], coll, 16, rotation=(90, 0, 0), bevel=0.008))
    elif pommel == 'ball':
        out.append(geo.sphere('Pommel', 0.032, (0, 0, pz), mats['hilt'], coll, 14, 8))
    elif pommel == 'gem':
        out.append(geo.cylinder('Pommel cup', 0.03, 0.03, (0, 0, pz + .005), mats['hilt'], coll, 12, bevel=.006))
        out.append(geo.sphere('Pommel gem', 0.028, (0, 0, pz - .02), mats.get('gem', mats['hilt']), coll, 12, 8))
    if rune is not None:
        for i in range(4):
            out.append(geo.box('Blade rune', (0.012, 0.022, 0.035), (0, 0, gz + 0.12 + i * 0.09), rune, coll, bevel=0.003))
    return dict(objs=out, tip=_v(curve * length, 0, gz + length), grip2=_v(0, 0, -grip * .35), kind='blade')


def haft(length, radius, mats, coll, z0, z1, rings=None, mat_key='wood', taper=1.0):
    out = [geo.cylinder('Haft', radius, z1 - z0, (0, 0, (z0 + z1) / 2), mats[mat_key], coll, 12, radius2=radius * taper,
                        bevel=0.004)]
    for z in (rings or []):
        out.append(geo.cylinder('Haft band', radius * 1.3, 0.035, (0, 0, z), mats['hilt'], coll, 12, bevel=0.006))
    return out


def axe(mats, coll, haft_len=0.75, head=1.0, double=False, beard=True, two_hand=False, spike=False):
    out = []
    top = haft_len * (0.72 if not two_hand else 0.8)
    out += haft(haft_len, 0.022, mats, coll, -haft_len * (0.28 if not two_hand else 0.2), top + 0.05,
                rings=[top - 0.02, -haft_len * .2])
    h = head
    outline = [(0.02, 0.08 * h), (0.16 * h, 0.12 * h), (0.27 * h, 0.2 * h), (0.3 * h, 0.05 * h), (0.29 * h, -0.08 * h),
               (0.24 * h, -0.17 * h) if beard else (0.27 * h, -0.1 * h), (0.15 * h, -0.08 * h), (0.02, -0.05 * h)]
    sides = (1, -1) if double else (1,)
    for sx in sides:
        pts = [(sx * x, z) for x, z in outline]
        if sx < 0:
            pts = list(reversed(pts))
        bl = geo.extrude('Axe bit', pts, 0.024, mats['blade'], coll, plane='XZ', bevel=0.006)
        # thin the cutting edge
        for v in bl.data.vertices:
            k = min(1.0, abs(v.co.x) / (0.3 * h))
            v.co.y *= 1 - 0.75 * k ** 2
        bl.data.update()
        geo.store_rest(bl)
        geo.place(bl, (0, 0, top))
        out.append(bl)
    out.append(geo.box('Axe eye', (0.06, 0.05, 0.13 * h), (0, 0, top), mats['steel'], coll, bevel=0.01))
    if spike:
        out.append(geo.cylinder('Top spike', 0.02, 0.14, (0, 0, top + 0.12), mats['steel'], coll, 8, radius2=0.002, bevel=0))
    if not double:
        out.append(geo.cylinder('Axe poll', 0.024, 0.07, (-0.045, 0, top), mats['steel'], coll, 8, rotation=(0, 90, 0)))
    return dict(objs=out, tip=_v(0.3 * h, 0, top), grip2=_v(0, 0, -0.3) if two_hand else None, kind='axe')


def mace(mats, coll, haft_len=0.6, flanges=7, head=1.0, spikes=False):
    out = []
    top = haft_len * 0.7
    out += haft(haft_len, 0.02, mats, coll, -haft_len * .3, top, rings=[-haft_len * .25], mat_key='steel')
    core = geo.lathe('Mace core', [(0, top + .14 * head), (0.04 * head, top + .12 * head), (0.05 * head, top),
                                   (0.035 * head, top - 0.08 * head), (0, top - .1 * head)], 16, mats['steel'], coll)
    out.append(core)
    for i in range(flanges):
        a = i * math.tau / flanges
        fl = geo.extrude('Mace flange', [(0, -0.08 * head), (0.07 * head, -0.03 * head), (0.075 * head, 0.06 * head),
                                         (0, 0.1 * head)], 0.012, mats['steel'], coll, plane='XZ', bevel=0.004)
        geo.place(fl, (0, 0, top + 0.02 * head), rotation=(0, 0, math.degrees(a)))
        out.append(fl)
    if spikes:
        for i in range(10):
            a = i * 2.4
            d = _v(math.cos(a), math.sin(a), (i % 3 - 1) * .6).normalized()
            base = _v(0, 0, top + .02) + d * 0.06 * head
            out.append(geo.tube('Spike', [base, base + d * 0.08 * head], [0.018 * head, 0.001], mats['steel'], coll, sides=6))
    return dict(objs=out, tip=_v(0.06, 0, top + 0.05), grip2=None, kind='mace')


def hammer(mats, coll, haft_len=0.8, head=1.0, two_hand=False, spike=True, head_mat='steel', block=False):
    out = []
    top = haft_len * (0.75 if not two_hand else 0.82)
    out += haft(haft_len, 0.024, mats, coll, -haft_len * (0.25 if not two_hand else 0.18), top + 0.06,
                rings=[top - 0.08, -haft_len * .18])
    if block:
        out.append(geo.box('Maul head', (0.36 * head, 0.2 * head, 0.2 * head), (0, 0, top), mats[head_mat], coll,
                           bevel=0.03 * head))
        for sx in (-1, 1):
            out.append(geo.box('Maul band', (0.04 * head, 0.22 * head, 0.22 * head), (sx * 0.1 * head, 0, top), mats['hilt'],
                               coll, bevel=0.01))
    else:
        out.append(geo.cylinder('Hammer head', 0.065 * head, 0.24 * head, (0.04 * head, 0, top), mats[head_mat], coll, 8,
                                rotation=(0, 90, 0), bevel=0.012))
        out.append(geo.cylinder('Hammer face', 0.075 * head, 0.03 * head, (0.165 * head, 0, top), mats[head_mat], coll, 8,
                                rotation=(0, 90, 0), bevel=0.008))
        if spike:
            out.append(geo.tube('Hammer beak', [_v(-0.07 * head, 0, top), _v(-0.17 * head, 0, top - 0.02),
                                                _v(-0.24 * head, 0, top - 0.08 * head)], [0.04 * head, 0.025 * head, 0.003],
                                mats[head_mat], coll, sides=6))
    return dict(objs=out, tip=_v(0.18 * head, 0, top), grip2=_v(0, 0, -0.3) if two_hand else None,
                kind='hammer')


def spear(mats, coll, length=2.0, head_len=0.32, head_w=0.09, grip_at=0.35, tassel=None, wings=False, banner=None):
    out = []
    z0 = -length * grip_at
    z1 = z0 + length
    out += haft(length, 0.022, mats, coll, z0, z1 - head_len * 0.15, rings=[z1 - head_len - 0.04])
    out.append(geo.lathe('Spear socket', [(0.024, z1 - head_len - 0.08), (0.03, z1 - head_len - 0.02),
                                          (0.022, z1 - head_len + 0.02)], 12, mats['steel'], coll))
    out.append(blade('Spearhead', head_len, head_w, mats['blade'], coll, base_z=z1 - head_len, thickness=0.025, tip=0.45,
                     taper=0.0, flare=0.6))
    if wings:
        for sx in (-1, 1):
            out.append(geo.tube('Spear wing', [_v(0, 0, z1 - head_len - 0.02), _v(sx * 0.07, 0, z1 - head_len + 0.01)],
                                [0.014, 0.004], mats['steel'], coll, sides=6))
    if tassel is not None:
        for i in range(6):
            a = i * math.tau / 6
            p0 = _v(0, 0, z1 - head_len - 0.06)
            out.append(geo.tube('Tassel', [p0, p0 + _v(math.cos(a) * .03 - 0.05, math.sin(a) * .03, -0.12)], [0.012, 0.004],
                                tassel, coll, sides=5))
    if banner is not None:
        out += _pennon(banner, z1 - head_len - 0.08, coll)
    return dict(objs=out, tip=_v(0, 0, z1), grip2=_v(0, 0, 0.34), kind='spear')


def _pennon(mat, z, coll, length=0.42, height=0.16):
    def fn(u, v):
        x = -u * length
        zz = z - v * height * (1 - 0.4 * u)
        if u > .75:
            zz += (v - .5) * height * 0.35 * (u - .75) / .25
        y = 0.025 * math.sin(u * 7) * u
        return (x, y, zz)
    p = geo.grid_sheet('Pennon', 1, 1, 10, 3, fn, mat, coll)
    _solid(p, 0.006)
    return [p]


def halberd(mats, coll, length=2.0, grip_at=0.35):
    out = []
    z0 = -length * grip_at
    z1 = z0 + length
    out += haft(length, 0.022, mats, coll, z0, z1 - 0.2, rings=[z1 - 0.45, z1 - 0.2])
    out.append(blade('Halberd spike', 0.28, 0.05, mats['blade'], coll, base_z=z1 - 0.26, thickness=0.02, tip=0.5, taper=0))
    bl = geo.extrude('Halberd axe', [(0.02, -0.1), (0.2, -0.16), (0.27, -0.08), (0.28, 0.06), (0.24, 0.14), (0.02, 0.06)],
                     0.022, mats['blade'], coll, plane='XZ', bevel=0.006)
    for v in bl.data.vertices:
        k = min(1.0, abs(v.co.x) / 0.28)
        v.co.y *= 1 - 0.7 * k ** 2
    bl.data.update()
    geo.store_rest(bl)
    geo.place(bl, (0, 0, z1 - 0.38))
    out.append(bl)
    out.append(geo.tube('Back hook', catmull([_v(-0.02, 0, z1 - 0.36), _v(-0.12, 0, z1 - 0.33), _v(-0.17, 0, z1 - 0.42)], 3),
                        [0.022, 0.016, 0.012, 0.008, 0.005, 0.003, 0.002], mats['steel'], coll, sides=6))
    out.append(geo.box('Langet', (0.045, 0.04, 0.22), (0, 0, z1 - 0.38), mats['steel'], coll, bevel=0.008))
    return dict(objs=out, tip=_v(0.28, 0, z1 - 0.38), grip2=_v(0, 0, 0.36), kind='polearm')


def lance(mats, coll, length=2.6, paint=None):
    out = []
    z0 = -0.35
    shaft = geo.lathe('Lance', [(0.02, z0 - 0.05), (0.045, z0 + 0.15), (0.05, z0 + 0.3), (0.04, z0 + length * 0.5),
                                (0.022, z0 + length * 0.9), (0, z0 + length)], 16, paint or mats['wood'], coll)
    out.append(shaft)
    out.append(geo.lathe('Vamplate', [(0.03, z0 + 0.42), (0.13, z0 + 0.3), (0.14, z0 + 0.27), (0.03, z0 + 0.25)], 20,
                         mats['steel'], coll))
    out.append(blade('Lance head', 0.18, 0.05, mats['blade'], coll, base_z=z0 + length - 0.04, thickness=0.03, tip=0.6,
                     taper=0))
    return dict(objs=out, tip=_v(0, 0, z0 + length + 0.14), grip2=None, kind='lance')


def staff(mats, coll, length=1.75, grip_at=0.42, head='crystal', glow=None, crystal=None, twist=True):
    out = []
    z0 = -length * grip_at
    z1 = z0 + length
    pts = [_v(math.sin(i * 1.3) * 0.012 * (1 if twist else 0), math.cos(i * 1.1) * 0.01 * (1 if twist else 0),
              z0 + length * i / 8) for i in range(9)]
    out.append(geo.tube('Staff', pts, geo.taper(9, 0.026, 0.03), mats['wood'], coll, sides=10))
    out.append(geo.cylinder('Staff ferrule', 0.03, 0.06, (0, 0, z0 + 0.02), mats['hilt'], coll, 10))
    tip = _v(0, 0, z1 + 0.12)
    if head == 'crystal':
        for i in range(4):
            a = i * math.tau / 4 + .4
            pts = [_v(0, 0, z1 - 0.06), _v(math.cos(a) * 0.08, math.sin(a) * 0.08, z1 + 0.06),
                   _v(math.cos(a) * 0.05, math.sin(a) * 0.05, z1 + 0.2), _v(math.cos(a) * 0.015, math.sin(a) * 0.015, z1 + 0.26)]
            out.append(geo.tube('Staff prong', catmull(pts, 3), geo.taper(10, 0.018, 0.006), mats['hilt'], coll, sides=6))
        cr = geo.lathe('Staff crystal', [(0, z1 + 0.02), (0.055, z1 + 0.08), (0.06, z1 + 0.16), (0, z1 + 0.3)], 6,
                       crystal or mats['glow'], coll, smooth=False)
        out.append(cr)
        tip = _v(0, 0, z1 + 0.16)
    elif head == 'orb':
        out.append(geo.sphere('Staff orb', 0.08, (0, 0, z1 + 0.1), crystal or mats['glow'], coll, 20, 12))
        for i in range(3):
            a = i * math.tau / 3
            pts = [_v(0, 0, z1 - 0.04), _v(math.cos(a) * .1, math.sin(a) * .1, z1 + .05), _v(math.cos(a) * .07, math.sin(a) * .07, z1 + .17),
                   _v(0, 0, z1 + 0.21)]
            out.append(geo.tube('Orb cage', catmull(pts, 3), 0.012, mats['hilt'], coll, sides=6))
        tip = _v(0, 0, z1 + 0.1)
    elif head == 'skull':
        sk = geo.quadsphere('Staff skull', 0.07, (0, 0, z1 + 0.08), mats['bone'], coll, 3, scale=(1.1, .85, 1))
        out.append(sk)
        for sy in (-1, 1):
            out.append(geo.sphere('Staff skull eye', 0.018, (0.06, sy * 0.025, z1 + 0.09), mats['glow'], coll, 8, 6))
        tip = _v(0.04, 0, z1 + 0.1)
    elif head == 'crook':
        pts = [_v(0, 0, z1 - 0.02), _v(0.02, 0, z1 + 0.15), _v(0.14, 0, z1 + 0.26), _v(0.22, 0, z1 + 0.16), _v(0.17, 0, z1 + 0.06)]
        out.append(geo.tube('Crosier crook', catmull(pts, 4), geo.taper(17, 0.03, 0.018), mats['hilt'], coll, sides=10))
        out.append(geo.sphere('Crook jewel', 0.035, (0.17, 0, z1 + 0.06), crystal or mats['glow'], coll, 10, 6))
        tip = _v(0.12, 0, z1 + 0.22)
    elif head == 'branch':
        for i in range(5):
            a = i * 1.3
            pts = [_v(0, 0, z1 - 0.05), _v(math.cos(a) * 0.07, math.sin(a) * 0.05, z1 + 0.1),
                   _v(math.cos(a) * 0.12, math.sin(a) * 0.08, z1 + 0.22 + 0.04 * (i % 2))]
            out.append(geo.tube('Branch tine', catmull(pts, 3), geo.taper(7, 0.022, 0.004), mats['wood'], coll, sides=6))
        out.append(geo.sphere('Witchlight', 0.05, (0.0, 0, z1 + 0.15), crystal or mats['glow'], coll, 14, 8))
        tip = _v(0, 0, z1 + 0.15)
    elif head == 'lantern':
        out += lantern_head(mats, coll, _v(0, 0, z1), glow)
        tip = _v(0.0, 0, z1 + 0.05)
    elif head == 'trident':
        out.append(geo.box('Trident bar', (0.24, 0.025, 0.03), (0, 0, z1), mats['steel'], coll, bevel=0.006))
        for sx in (-1, 0, 1):
            out.append(blade('Trident tine', 0.26 if sx == 0 else 0.2, 0.035, mats['blade'], coll, base_z=z1, thickness=0.018,
                             tip=0.4, taper=0.1))
            out[-1].location.x = sx * 0.1
            geo.transform(out[-1], Matrix.Translation((sx * 0.1, 0, 0)))
            out[-1].location.x = 0
        tip = _v(0, 0, z1 + 0.26)
    elif head == 'censer':
        out.append(geo.tube('Censer chain', [_v(0, 0, z1), _v(0.12, 0, z1 + 0.05), _v(0.2, 0, z1 - 0.12)], 0.008, mats['hilt'],
                            coll, sides=5))
        out.append(geo.lathe('Censer', [(0, z1 - 0.32), (0.06, z1 - 0.3), (0.085, z1 - 0.22), (0.07, z1 - 0.14), (0.03, z1 - 0.11),
                                        (0, z1 - 0.1)], 12, mats['hilt'], coll))
        geo.transform(out[-1], Matrix.Translation((0.2, 0, 0.1)))
        out.append(geo.sphere('Censer ember', 0.045, (0.2, 0, z1 - 0.12), crystal or mats['glow'], coll, 10, 6))
        tip = _v(0.2, 0, z1 - 0.12)
    return dict(objs=out, tip=tip, grip2=_v(0, 0, 0.3), kind='staff')


def lantern_head(mats, coll, base, glow=None, size=1.0):
    out = []
    s = size
    out.append(geo.tube('Lantern hook', catmull([base, base + _v(0.05 * s, 0, 0.12 * s), base + _v(0.14 * s, 0, 0.14 * s),
                                                 base + _v(0.17 * s, 0, 0.08 * s)], 3), 0.012 * s, mats['hilt'], coll, sides=6))
    c = base + _v(0.17 * s, 0, -0.05 * s)
    out.append(geo.lathe('Lantern cap', [(0, 0.1 * s), (0.04 * s, 0.09 * s), (0.075 * s, 0.05 * s), (0.08 * s, 0.035 * s)],
                         8, mats['hilt'], coll, location=c))
    out.append(geo.lathe('Lantern base', [(0.075 * s, -0.08 * s), (0.08 * s, -0.095 * s), (0.04 * s, -0.11 * s),
                                          (0, -0.12 * s)], 8, mats['hilt'], coll, location=c))
    out.append(geo.cylinder('Lantern glass', 0.065 * s, 0.13 * s, c + _v(0, 0, -0.02 * s), mats.get('lamp_glass', mats['glow']),
                            coll, 8, bevel=0))
    for i in range(8):
        a = i * math.tau / 8
        out.append(geo.cylinder('Lantern bar', 0.007 * s, 0.14 * s, c + _v(math.cos(a) * 0.07 * s, math.sin(a) * 0.07 * s, -0.02 * s),
                                mats['hilt'], coll, 6, bevel=0))
    out.append(geo.sphere('Lantern flame', 0.035 * s, c + _v(0, 0, -0.02 * s), glow or mats['glow'], coll, 10, 6))
    return out


def bow(mats, coll, height=1.25, draw=0.0, recurve=0.06, string_mat=None):
    """Bow held at its grip; limbs along Z, belly faces -X, string at -X."""
    out = []
    pts = []
    n = 16
    for i in range(n + 1):
        t = i / n * 2 - 1
        z = t * height / 2
        x = 0.12 * (1 - t * t) - recurve * (abs(t) ** 6) * 1.6 + 0.0
        pts.append(_v(x - 0.06, 0, z))
    limb = geo.tube('Bow limbs', pts, [0.03 * (1 - 0.6 * abs(i / n * 2 - 1)) + 0.008 for i in range(n + 1)], mats['wood'],
                    coll, sides=8, flatten=0.6)
    out.append(limb)
    out.append(geo.cylinder('Bow grip wrap', 0.032, 0.12, (0.05, 0, 0), mats['grip'], coll, 10))
    top, bot = pts[-1], pts[0]
    nock = _v(top.x - 0.06 - draw, 0, 0)
    out.append(geo.tube('Bowstring', [top, nock, bot], 0.004, string_mat or mats.get('string', mats['grip']), coll, sides=4))
    return dict(objs=out, tip=_v(0.12, 0, 0), grip2=nock, kind='bow', nock=nock, limb_top=top, limb_bottom=bot)


def arrow(mats, coll, length=0.75, fletch=None):
    out = [geo.cylinder('Arrow', 0.008, length, (0, 0, length / 2), mats['wood'], coll, 6, bevel=0)]
    out.append(blade('Arrowhead', 0.07, 0.03, mats['steel'], coll, base_z=length, thickness=0.01, tip=0.6, taper=0))
    for a in (0, 120, 240):
        fl = geo.extrude('Fletch', [(0, 0), (0.028, 0.02), (0.03, 0.12), (0, 0.1)], 0.003, fletch or mats['cloth'], coll,
                         plane='XZ', bevel=0)
        geo.place(fl, (0, 0, 0.02), rotation=(0, 0, a))
        out.append(fl)
    return dict(objs=out, tip=_v(0, 0, length + 0.07), grip2=None, kind='arrow')


def crossbow(mats, coll, loaded=True):
    out = []
    stock = geo.extrude('Crossbow stock', [(-0.32, -0.04), (-0.05, -0.03), (0.0, -0.09), (0.06, -0.09), (0.08, -0.02),
                                           (0.45, -0.02), (0.47, 0.03), (-0.3, 0.03)], 0.055, mats['wood'], coll, plane='XZ',
                        bevel=0.012)
    geo.place(stock, (0, 0, 0), rotation=(0, -90, 0))
    out.append(stock)
    prod = []
    for i in range(13):
        t = i / 12 * 2 - 1
        prod.append(_v(0.025 * (1 - t * t) - 0.01, t * 0.36, 0.43 - 0.06 * t * t))
    out.append(geo.tube('Crossbow prod', prod, [0.022 * (1 - .5 * abs(i / 6 - 1)) + .008 for i in range(13)], mats['steel'],
                        coll, sides=8, flatten=.6))
    out.append(geo.tube('Crossbow string', [prod[0], _v(0.0, 0, 0.2), prod[-1]], 0.004, mats.get('string', mats['grip']), coll,
                        sides=4))
    out.append(geo.lathe('Stirrup', [(0.03, 0.45), (0.06, 0.5)], 12, mats['steel'], coll, close_top=False, close_bottom=False))
    out.append(geo.box('Trigger', (0.02, 0.012, 0.07), (-0.04, 0, 0.0), mats['steel'], coll, bevel=0.004))
    if loaded:
        out.append(geo.cylinder('Bolt', 0.009, 0.3, (0.0, 0, 0.33), mats['wood'], coll, 6, bevel=0))
        out.append(blade('Bolt head', 0.05, 0.03, mats['steel'], coll, base_z=0.48, thickness=0.012, tip=0.6, taper=0))
    return dict(objs=out, tip=_v(0.0, 0, 0.53), grip2=_v(0.0, 0, 0.22), kind='crossbow')


def flask(mats, coll, size=1.0, liquid=None, glass=None, shape='round'):
    s = size
    out = []
    if shape == 'round':
        prof = [(0, -0.07 * s), (0.05 * s, -0.065 * s), (0.07 * s, -0.03 * s), (0.068 * s, 0.02 * s), (0.04 * s, 0.06 * s),
                (0.02 * s, 0.075 * s), (0.02 * s, 0.11 * s), (0.026 * s, 0.12 * s)]
    else:
        prof = [(0, -0.07 * s), (0.04 * s, -0.07 * s), (0.045 * s, 0.03 * s), (0.02 * s, 0.07 * s), (0.02 * s, 0.11 * s),
                (0.026 * s, 0.12 * s)]
    out.append(geo.lathe('Flask', prof, 16, glass or mats.get('glass'), coll, close_top=True))
    inner = [(r * 0.86, z) for r, z in prof[:5]]
    inner[-1] = (inner[-1][0], inner[-1][1] - 0.005)
    out.append(geo.lathe('Flask liquid', inner, 16, liquid or mats['glow'], coll))
    out.append(geo.cylinder('Cork', 0.022 * s, 0.04 * s, (0, 0, 0.125 * s), mats['wood'], coll, 10, bevel=0.004))
    return dict(objs=out, tip=_v(0, 0, 0.0), grip2=None, kind='flask')


def bomb(mats, coll, size=1.0, fuse_glow=None):
    out = [geo.sphere('Bomb', 0.09 * size, (0, 0, 0), mats['iron'], coll, 16, 10)]
    out.append(geo.cylinder('Bomb neck', 0.03 * size, 0.04 * size, (0, 0, 0.09 * size), mats['iron'], coll, 10))
    out.append(geo.tube('Fuse', [_v(0, 0, 0.1 * size), _v(0.03, 0, 0.16 * size), _v(0.07, 0, 0.18 * size)], 0.008, mats['rope'],
                        coll, sides=5))
    if fuse_glow is not None:
        out.append(geo.sphere('Fuse spark', 0.022, (0.07, 0, 0.18 * size), fuse_glow, coll, 8, 6))
    return dict(objs=out, tip=_v(0, 0, 0), grip2=None, kind='bomb')


def wrench(mats, coll, length=0.6):
    out = []
    out.append(geo.box('Wrench bar', (0.04, 0.025, length), (0, 0, length * 0.3), mats['steel'], coll, bevel=0.008))
    jaw = geo.extrude('Wrench jaw', [(-0.09, 0), (0.09, 0), (0.09, 0.06), (0.04, 0.06), (0.04, 0.12), (-0.04, 0.12),
                                     (-0.04, 0.06), (-0.09, 0.06)], 0.03, mats['steel'], coll, plane='XZ', bevel=0.01)
    geo.place(jaw, (0, 0, length * 0.78))
    out.append(jaw)
    out += _grip(0.18, 0.024, mats, coll, -0.02)
    return dict(objs=out, tip=_v(0, 0, length * .85), grip2=None, kind='hammer')


def horn(mats, coll, size=1.0):
    pts = catmull([_v(0, 0, 0), _v(0.12, 0, 0.1), _v(0.2, 0, 0.3), _v(0.15, 0, 0.45)], 4)
    h = geo.tube('War horn', pts, geo.taper(len(pts), 0.025 * size, 0.09 * size, 2.2), mats['bone'], coll, sides=14, cap=False)
    _solid(h, 0.01)
    out = [h]
    for t in (0.3, 0.7):
        p = pts[int(len(pts) * t)]
        out.append(geo.sphere('Horn band', 0.05 * size * (0.6 + t), p, mats['hilt'], coll, 12, 6, scale=(1, 1, .3)))
    return dict(objs=out, tip=pts[-1], grip2=None, kind='horn')


# ------------------------------------------------------------------ shields
def heater_shield(mats, coll, size=1.0, curve=0.12, emblem=None, boss=False, rim=True, face_key='paint'):
    """Face normal +X, long axis Z, centre at origin."""
    s = size
    outline = [(-0.27 * s, 0.3 * s), (0.27 * s, 0.3 * s), (0.28 * s, 0.0), (0.2 * s, -0.22 * s), (0.0, -0.4 * s),
               (-0.2 * s, -0.22 * s), (-0.28 * s, 0.0)]
    pts = catmull([_v(x, 0, z) for x, z in outline] + [_v(*([outline[0][0], 0, outline[0][1]]))], 5)
    pts2 = [(p.x, p.z) for p in pts[:-1]]
    face = geo.extrude('Shield face', pts2, 0.03 * s, mats[face_key], coll, plane='YZ', bevel=0.008)
    # curve the face around the vertical axis
    for v in face.data.vertices:
        v.co.x -= curve * (v.co.y / (0.3 * s)) ** 2 * 0.3 * s
    face.data.update()
    geo.store_rest(face)
    out = [face]
    if rim:
        loop = [_v(0.018 - curve * (p[0] / (0.3 * s)) ** 2 * 0.3 * s, p[0], p[1]) for p in pts2]
        loop.append(loop[0])
        out.append(geo.tube('Shield rim', loop, 0.016 * s, mats['trim'], coll, sides=6))
    if boss:
        out.append(geo.sphere('Shield boss', 0.07 * s, (0.03, 0, 0.02 * s), mats['trim'], coll, 16, 8, scale=(.6, 1, 1)))
    if emblem is not None:
        out += emblem(_v(0.022, 0, 0.02 * s), coll, s)
    return dict(objs=out, tip=_v(0.04, 0, 0), grip2=None, kind='shield')


def round_shield(mats, coll, radius=0.3, planks=True, boss=True, emblem=None, face_key='wood'):
    out = []
    disc = geo.cylinder('Round shield', radius, 0.03, (0, 0, 0), mats[face_key], coll, 32, rotation=(0, 90, 0), bevel=0.006)
    geo.sculpt(disc, (0.2, 0, 0), radius * 1.5, (0.03, 0, 0))
    out.append(disc)
    rim = geo.lathe('Shield rim', [(radius - 0.01, -0.025), (radius + 0.01, 0), (radius - 0.01, 0.025)], 40, mats['trim'], coll,
                    close_top=False, close_bottom=False, rotation=(0, 90, 0))
    _solid(rim, 0.012)
    out.append(rim)
    if boss:
        out.append(geo.lathe('Shield boss', [(0.09, 0), (0.08, 0.03), (0.05, 0.06), (0, 0.07)], 20, mats['trim'], coll,
                             rotation=(0, 90, 0), location=(0.02, 0, 0)))
    if emblem is not None:
        out += emblem(_v(0.025, 0, 0), coll, radius / 0.3)
    return dict(objs=out, tip=_v(0.05, 0, 0), grip2=None, kind='shield')


def tower_shield(mats, coll, width=0.5, height=1.05, ridge=True, emblem=None, face_key='paint', spikes=0):
    out = []
    outline = [(-width / 2, -height / 2), (width / 2, -height / 2), (width / 2, height / 2 - 0.08), (width * .35, height / 2),
               (-width * .35, height / 2), (-width / 2, height / 2 - 0.08)]
    face = geo.extrude('Tower shield', outline, 0.04, mats[face_key], coll, plane='YZ', bevel=0.01)
    for v in face.data.vertices:
        v.co.x -= 0.08 * (v.co.y / (width / 2)) ** 2
        if ridge:
            v.co.x += 0.04 * max(0, 1 - abs(v.co.y) / 0.08)
    face.data.update()
    geo.store_rest(face)
    out.append(face)
    loop = [_v(0.022 - 0.08 * (y / (width / 2)) ** 2, y, z) for y, z in outline] + []
    loop.append(loop[0])
    out.append(geo.tube('Tower rim', loop, 0.02, mats['trim'], coll, sides=6))
    if emblem is not None:
        out += emblem(_v(0.05, 0, 0.05), coll, 1.3)
    for i in range(spikes):
        z = (i - (spikes - 1) / 2) * 0.25
        out.append(geo.tube('Shield spike', [_v(0.04, 0, z), _v(0.18, 0, z)], [0.03, 0.002], mats['trim'], coll, sides=6))
    return dict(objs=out, tip=_v(0.06, 0, 0), grip2=None, kind='shield')


# ------------------------------------------------------------------ emblems
def emblem_lantern(mat, glow=None):
    """Lantern Caravan sigil: a hooded lantern within rays."""
    def build(origin, coll, s=1.0):
        out = []
        body = [(-0.05 * s, -0.08 * s), (0.05 * s, -0.08 * s), (0.065 * s, 0.04 * s), (0.0, 0.1 * s), (-0.065 * s, 0.04 * s)]
        e = geo.extrude('Emblem lantern', body, 0.012, mat, coll, plane='YZ', bevel=0.003)
        geo.place(e, origin)
        out.append(e)
        ring = geo.extrude('Emblem handle', [(-0.02 * s, 0.1 * s), (0.02 * s, 0.1 * s), (0.02 * s, 0.14 * s), (-0.02 * s, 0.14 * s)],
                           0.012, mat, coll, plane='YZ', bevel=0.002)
        geo.place(ring, origin)
        out.append(ring)
        if glow is not None:
            g = geo.extrude('Emblem flame', [(-0.025 * s, -0.04 * s), (0.025 * s, -0.04 * s), (0.0, 0.04 * s)], 0.016, glow, coll,
                            plane='YZ', bevel=0.002)
            geo.place(g, origin + _v(0.004, 0, 0))
            out.append(g)
        for k in range(5):
            a = math.radians(-60 + k * 30)
            r0, r1 = 0.13 * s, 0.17 * s
            ray = geo.extrude('Emblem ray', [(math.sin(a) * r0 - .006, math.cos(a) * r0), (math.sin(a) * r0 + .006, math.cos(a) * r0),
                                             (math.sin(a) * r1, math.cos(a) * r1)], 0.01, mat, coll, plane='YZ', bevel=0)
            geo.place(ray, origin + _v(0, 0, -0.02 * s))
            out.append(ray)
        return out
    return build


def emblem_cross(mat):
    def build(origin, coll, s=1.0):
        out = [geo.box('Emblem cross v', (0.012, 0.05 * s, 0.32 * s), origin, mat, coll, bevel=0.004),
               geo.box('Emblem cross h', (0.012, 0.24 * s, 0.05 * s), origin + _v(0, 0, 0.06 * s), mat, coll, bevel=0.004)]
        return out
    return build


def emblem_skull(mat, glow=None):
    def build(origin, coll, s=1.0):
        out = []
        sk = geo.quadsphere('Emblem skull', 0.07 * s, origin + _v(0.0, 0, 0.03 * s), mat, coll, 3, scale=(.4, 1, 1.05))
        out.append(sk)
        out.append(geo.box('Emblem jaw', (0.03, 0.08 * s, 0.05 * s), origin + _v(0.005, 0, -0.05 * s), mat, coll, bevel=0.006))
        for sy in (-1, 1):
            out.append(geo.sphere('Emblem eye', 0.017 * s, origin + _v(0.03 * s, sy * 0.028 * s, 0.03 * s),
                                  glow or mat, coll, 8, 6))
        for sy in (-1, 1):
            out.append(geo.tube('Emblem bone', [origin + _v(0, -0.16 * s, sy * 0.13 * s), origin + _v(0, 0.16 * s, -sy * 0.13 * s)],
                                0.014 * s, mat, coll, sides=6))
        return out
    return build


def emblem_sun(mat):
    def build(origin, coll, s=1.0):
        out = [geo.cylinder('Emblem sun', 0.06 * s, 0.012, origin, mat, coll, 20, rotation=(0, 90, 0), bevel=0.003)]
        for k in range(12):
            a = k * math.tau / 12
            out.append(geo.extrude('Sun ray', [(math.sin(a - .12) * .07 * s, math.cos(a - .12) * .07 * s),
                                               (math.sin(a + .12) * .07 * s, math.cos(a + .12) * .07 * s),
                                               (math.sin(a) * .13 * s, math.cos(a) * .13 * s)], 0.01, mat, coll, plane='YZ',
                                   bevel=0))
            geo.place(out[-1], origin)
        return out
    return build


def standard(mats, coll, cloth, emblem=None, height=2.3, grip_at=0.3, width=0.55, drop=0.75, finial='lantern',
             tails=2, trim=None):
    """Heraldic war banner on a crossbar pole; cloth hangs on the +X side."""
    out = []
    z0 = -height * grip_at
    z1 = z0 + height
    out += haft(height, 0.024, mats, coll, z0, z1, rings=[z1 - 0.06, z0 + 0.1])
    out.append(geo.cylinder('Banner crossbar', 0.016, width + 0.1, (width / 2, 0, z1 - 0.12), mats['wood'], coll, 8,
                            rotation=(0, 90, 0)))

    def fn(u, v):
        x = 0.02 + u * width
        z = z1 - 0.13 - v * drop
        if v > 0.75:
            k = (v - 0.75) / 0.25
            # swallow tails
            ph = (u * tails) % 1.0
            z += k * drop * 0.22 * (1 - abs(ph - 0.5) * 2)
        y = 0.03 * math.sin(u * 5 + v * 2) * (0.3 + v)
        return (x, y, z)
    sheet = geo.grid_sheet('Banner cloth', 1, 1, 12, 14, fn, cloth, coll)
    _solid(sheet, 0.01)
    _sub(sheet, 1)
    out.append(sheet)
    if trim is not None:
        out.append(geo.tube('Banner trim', [_v(*fn(u / 12, 0)) + _v(0, 0, 0.005) for u in range(13)], 0.012, trim, coll, sides=6))
    if emblem is not None:
        out += emblem(_v(0.02 + width * .5, -0.012, z1 - 0.13 - drop * 0.42), coll, 1.0)
        for o in out[-12:]:
            pass
    if finial == 'lantern':
        out += lantern_head(mats, coll, _v(0, 0, z1), mats.get('glow'), size=1.0)
    elif finial == 'spike':
        out.append(blade('Banner spike', 0.18, 0.05, mats['blade'], coll, base_z=z1, thickness=0.02, tip=0.5, taper=0))
    return dict(objs=out, tip=_v(0.2, 0, z1), grip2=_v(0, 0, z0 + height * 0.45), kind='standard')


def dagger(mats, coll, length=0.32, curve=0.0):
    d = sword(mats, coll, length=length, width=0.05, guard=0.12, grip=0.1, pommel='ball', curve=curve)
    d['kind'] = 'dagger'
    return d


def quarterstaff(mats, coll, length=1.8, grip_at=0.4, caps=True):
    out = []
    z0 = -length * grip_at
    z1 = z0 + length
    out.append(geo.cylinder('Quarterstaff', 0.026, length, (0, 0, (z0 + z1) / 2), mats['wood'], coll, 12, bevel=0.006))
    if caps:
        for z in (z0 + 0.06, z1 - 0.06):
            out.append(geo.cylinder('Staff cap', 0.033, 0.12, (0, 0, z), mats['hilt'], coll, 12, bevel=0.008))
    return dict(objs=out, tip=_v(0, 0, z1), grip2=_v(0, 0, 0.35), kind='staff')
