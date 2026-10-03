"""Armour plates, garments and body props.

Each builder returns a list of (object, binding) pairs. binding is either a bone
name (rigid) or a dict for skinned garments: {'skin': restrict_bones|None,
'fn': explicit_weight_fn|None}. `character.Character.attach` applies bindings.
"""
import math

from mathutils import Matrix, Vector

from . import geo
from .body import ellipse, torso_sections


def _v(*a):
    return Vector(a)


def _solid(obj, t, offset=-1, rim=True):
    m = obj.modifiers.new('Thickness', 'SOLIDIFY')
    m.thickness = t
    m.offset = offset
    m.use_even_offset = False
    m.use_rim = rim
    return obj


def _sub(obj, n=1):
    m = obj.modifiers.new('Smooth', 'SUBSURF')
    m.levels = n
    m.render_levels = n
    return obj


def _bevel(obj, w, seg=2):
    m = obj.modifiers.new('Bevel', 'BEVEL')
    m.width = w
    m.segments = seg
    m.limit_method = 'ANGLE'
    return obj


def ring_tube(name, section, radius, mat, coll, scale=1.0, z=None, sides=8):
    pts = [(p - _v(p.x, p.y, 0)) for p in section]
    c = sum(section, Vector()) / len(section)
    loop = [c + (p - c) * scale for p in section]
    if z is not None:
        loop = [_v(p.x, p.y, z) for p in loop]
    loop.append(loop[0])
    loop.insert(0, loop[-2])
    obj = geo.tube(name, loop[1:], radius, mat, coll, sides=sides, cap=False)
    return obj


# ------------------------------------------------------------------ torso
def breastplate(s, J, mats, coll, inflate=0.035, ridge=0.025, fauld=2, trim=True, top=None, plackart=False):
    out = []
    z0 = J['pelvis'].z + 0.06
    z1 = (top or J['neck'].z - 0.03)
    secs = torso_sections(s, J, inflate, z_from=z0 - .2, z_to=z1, n=28)
    secs = [sec for sec in secs if z0 - 0.01 <= sec[0].z <= z1 + 0.01]
    plate = geo.loft('Breastplate', secs, mats['steel'], coll, cap=False)
    # central keel and flared waist
    for v in plate.data.vertices:
        if v.co.x > 0:
            k = max(0.0, 1 - abs(v.co.y) / 0.09)
            v.co.x += ridge * k * k
    plate.data.update()
    geo.subdivide(plate, 1)
    _solid(plate, 0.022)
    _bevel(plate, 0.008)
    out.append((plate, {'skin': ['hips', 'spine', 'chest']}))
    if trim:
        out.append((ring_tube('Neckline roll', secs[-1], 0.018, mats['trim'], coll, 1.02), {'skin': ['chest']}))
        out.append((ring_tube('Waist roll', secs[0], 0.016, mats['trim'], coll, 1.03), {'skin': ['hips', 'spine']}))
    if plackart:
        pl = geo.extrude('Plackart', [(-0.13, 0), (0.13, 0), (0.08, 0.2), (0, 0.24), (-0.08, 0.2)], 0.02,
                         mats['steel'], coll, plane='YZ', bevel=0.006)
        geo.place(pl, (J['spine'].x + .2 * s['bulk'] + inflate, 0, z0 + .01), rotation=(0, -12, 0))
        out.append((pl, {'skin': ['hips', 'spine']}))
    for i in range(fauld):
        zt = z0 - 0.02 - i * 0.065
        top_sec = ellipse(J['pelvis'].x, zt, 0.18 * s['bulk'] + inflate + i * .012, 0.215 * s['bulk'] + inflate + i * .012, 28,
                          .1, 0)
        bot_sec = ellipse(J['pelvis'].x, zt - 0.085, 0.2 * s['bulk'] + inflate + i * .016,
                          0.235 * s['bulk'] + inflate + i * .016, 28, .1, 0)
        f = geo.loft('Fauld lame', [bot_sec, top_sec], mats['steel'], coll, cap=False)
        _solid(f, 0.016)
        _bevel(f, 0.006)
        out.append((f, {'skin': ['hips']}))
    return out


def shell_torso(name, s, J, mat, coll, inflate=0.02, z_from=None, z_to=None, thickness=0.015, quilt=False,
                bind=('hips', 'spine', 'chest'), sub=1):
    """Conforming garment/armour shell over the torso (gambeson, leather jerkin, mail)."""
    z0 = z_from if z_from is not None else J['pelvis'].z - 0.1
    z1 = z_to if z_to is not None else J['neck'].z - 0.02
    secs = [sec for sec in torso_sections(s, J, inflate, n=28) if z0 - .01 <= sec[0].z <= z1 + .01]
    sh = geo.loft(name, secs, mat, coll, cap=False)
    if sub:
        geo.subdivide(sh, sub)
    if quilt:
        for v in sh.data.vertices:
            v.co += v.normal * 0.006 * (math.sin(v.co.z * 70) * math.sin(math.atan2(v.co.y, v.co.x) * 12))
        sh.data.update()
        geo.store_rest(sh)
    _solid(sh, thickness)
    return [(sh, {'skin': list(bind)})]


def skirt(name, J, mat, coll, top_z, bottom_z, r_top=(0.2, 0.23), r_bottom=(0.3, 0.34), folds=9, fold_depth=0.018,
          front_open=0.0, split=0.0, segs=36, rows=8, hem_wave=0.02, thickness=0.012, x=0.0, seed=1,
          back_extra=0.0):
    """Hanging skirt/robe tube with folds; weights blend hips->thighs to follow the stride."""
    def fn(u, v):
        a = (u - 0.5) * math.tau
        rx = r_top[0] + (r_bottom[0] - r_top[0]) * v ** 1.2
        ry = r_top[1] + (r_bottom[1] - r_top[1]) * v ** 1.2
        fold = 1 + (fold_depth / max(rx, .01)) * math.sin(a * folds + seed) * (0.3 + 0.7 * v)
        z = top_z + (bottom_z - top_z) * v
        z += hem_wave * math.sin(a * 5 + seed * 2) * v ** 3
        if math.cos(a) < 0:
            z -= back_extra * v * (-math.cos(a))
        return (x + math.cos(a) * rx * fold, math.sin(a) * ry * fold, z)
    sk = geo.grid_sheet(name, 1, 1, segs, rows, fn, mat, coll, smooth=True)
    if front_open or split:
        import bmesh
        bm = bmesh.new()
        bm.from_mesh(sk.data)
        kill = []
        for f in bm.faces:
            c = f.calc_center_median()
            ang = math.atan2(c.y, c.x - x)
            if front_open and abs(ang) < front_open and c.z < top_z - 0.03:
                kill.append(f)
            if split and abs(ang) < 0.12 and c.z < top_z - (top_z - bottom_z) * (1 - split):
                kill.append(f)
        bmesh.ops.delete(bm, geom=list(set(kill)), context='FACES')
        bm.to_mesh(sk.data)
        bm.free()
        geo.store_rest(sk)
    _solid(sk, thickness, 0)
    _sub(sk, 1)
    pz = J['pelvis'].z
    knee = (J['knee.R'].z + J['knee.L'].z) / 2

    def weights(co):
        depth = max(0.0, min(1.0, (pz - co.z) / max(0.05, pz - knee)))
        side = 'thigh.R' if co.y < 0 else 'thigh.L'
        other = 'thigh.L' if co.y < 0 else 'thigh.R'
        lat = min(1.0, abs(co.y) / 0.16)
        w_leg = depth * (0.25 + 0.45 * lat)
        res = {'hips': 1 - w_leg, side: w_leg * 0.85, other: w_leg * 0.15}
        if co.z < knee:
            k = min(1.0, (knee - co.z) / 0.3)
            shin = 'shin.R' if co.y < 0 else 'shin.L'
            res[shin] = w_leg * 0.3 * k
        return res
    return [(sk, {'fn': weights})]


def tabard(J, s, mat, coll, length=None, width=0.2, emblem=None, back=True, trim=None, hem='straight',
           x_front=None):
    """Front/back heraldic panels from the chest to the knee."""
    out = []
    top = J['chest'].z + 0.1
    bottom = (J['knee.R'].z + 0.02) if length is None else length
    pz = J['pelvis'].z
    b = s['bulk']

    def front_x(z):
        if z > pz + 0.05:
            return (0.22 * b if z > J['spine'].z else 0.2 * b + 0.05 * s['belly']) + 0.035
        return 0.2 * b + 0.035 + (pz + 0.05 - z) * 0.12

    for sign, name in ((1, 'Tabard front'), (-1, 'Tabard back')):
        if sign < 0 and not back:
            continue

        def fn(u, v, sign=sign):
            z = top + (bottom - top) * v
            w = width * b * (1 + 0.2 * v)
            y = (u - 0.5) * 2 * w
            fx = (front_x(z) if sign > 0 else 0.19 * b + 0.035 + (0.05 if z < pz else 0))
            x = sign * (fx - 0.04 * ((u - 0.5) * 2) ** 2)
            x += sign * 0.01 * math.sin(u * math.pi * 5) * v
            if hem == 'dagged':
                z += 0.035 * (0.5 + 0.5 * math.cos(u * math.pi * 10)) * max(0, v - .85) / .15
            elif hem == 'point':
                z -= 0.08 * (1 - abs(u - .5) * 2) * max(0, v - .7) / .3
            return (x, y, z)
        panel = geo.grid_sheet(name, 1, 1, 10, 14, fn, mat, coll)
        _solid(panel, 0.012, 0)
        _sub(panel, 1)

        def weights(co):
            if co.z > pz:
                k = min(1.0, (co.z - pz) / 0.25)
                return {'chest': k * .6, 'spine': .4 + k * .1, 'hips': (1 - k) * .6}
            depth = min(1.0, (pz - co.z) / 0.35)
            side = 'thigh.R' if co.y < 0 else 'thigh.L'
            other = 'thigh.L' if co.y < 0 else 'thigh.R'
            return {'hips': 1 - depth * .55, side: depth * .4, other: depth * .15}
        out.append((panel, {'fn': weights}))
        if trim is not None:
            pts = [_v(*fn(u / 10, 1.0)) + _v(sign * .006, 0, 0) for u in range(11)]
            out.append((geo.tube('Tabard hem', pts, 0.012, trim, coll, sides=6), {'fn': weights}))
            for sy in (-1, 1):
                pts = [_v(*fn(0.5 + sy * .5, v / 12)) + _v(sign * .004, 0, 0) for v in range(13)]
                out.append((geo.tube('Tabard edge', pts, 0.009, trim, coll, sides=6), {'fn': weights}))
    if emblem is not None:
        ez = J['chest'].z - 0.02
        em = emblem(_v(front_x(ez) + 0.012, 0, ez), coll)
        for o in em:
            out.append((o, {'skin': ['chest', 'spine']}))
    return out


def cape(J, s, mat, coll, length=None, width=1.0, folds=7, flare=1.0, collar=None, tattered=0.0, lining=None,
         seed=4):
    """Back cape on its own bone chain (cape.0..3) for secondary motion."""
    out = []
    top = J['neck'].z - 0.02
    bottom = 0.32 if length is None else length
    b = s['bulk'] * s['chest_w']

    def fn(u, v):
        a = (u - 0.5) * math.radians(190) * width
        r = (0.2 * b + 0.06) + (0.2 + 0.12 * flare) * v
        fold = 0.02 * math.sin(a * folds + seed) * (0.2 + v)
        x = -math.cos(a) * (r + fold) * (0.85 if v < .2 else 1.0) - 0.04 - 0.12 * v
        y = math.sin(a) * (r * 1.05 + fold)
        z = top + (bottom - top) * v
        if tattered:
            z += tattered * 0.06 * (math.sin(a * 13 + seed) + 1) * v ** 4
        return (x + J['chest'].x, y, z)
    cp = geo.grid_sheet('Cape', 1, 1, 22, 14, fn, mat, coll)
    _solid(cp, 0.014, 0)
    _sub(cp, 1)

    def weights(co):
        t = max(0.0, min(1.0, (top - co.z) / max(0.1, top - bottom)))
        idx = t * 3.0
        res = {}
        if t < 0.08:
            return {'chest': 1.0}
        lo = int(min(2, math.floor(idx)))
        f = idx - lo
        res['cape.%d' % lo] = 1 - f
        res['cape.%d' % (lo + 1)] = f
        if t < 0.25:
            res['chest'] = (0.25 - t) * 3
        return res
    out.append((cp, {'fn': weights}))
    if collar is not None:
        pts = [_v(*fn(u / 16, 0.0)) for u in range(17)]
        out.append((geo.tube('Cape collar', pts, 0.045, collar, coll, sides=10), 'chest'))
    return out


def cape_bones(sk, J, length=None):
    top = J['neck'] + _v(-0.22, 0, -0.04)
    bottom_z = 0.32 if length is None else length
    prev = 'chest'
    pts = [top + _v(-0.06 * i, 0, -(top.z - bottom_z) * i / 3) for i in range(4)]
    for i in range(3):
        sk.bone('cape.%d' % i, pts[i], pts[i + 1], prev, deform=True, connect=i > 0)
        prev = 'cape.%d' % i
    sk.bone('cape.3', pts[3], pts[3] + _v(-0.02, 0, -0.1), prev, connect=True)


def belt(J, s, mats, coll, z=None, buckle=True, pouches=1, sash=False):
    out = []
    zc = (J['pelvis'].z + 0.05) if z is None else z
    sec = ellipse(J['pelvis'].x, zc, 0.185 * s['bulk'] + 0.04 + 0.05 * s['belly'], 0.215 * s['bulk'] + 0.035, 32,
                  .2 + s['belly'] * .4, 0)
    band = geo.loft('Belt', [[p - _v(0, 0, 0.03) for p in sec], [p + _v(0, 0, 0.03) for p in sec]],
                    mats['leather'], coll, cap=False)
    _solid(band, 0.014, 0)
    out.append((band, 'hips'))
    front = max(sec, key=lambda p: p.x)
    if buckle:
        out.append((geo.box('Belt buckle', (0.025, 0.075, 0.07), front + _v(0.012, 0, 0), mats['trim'], coll,
                            bevel=0.008), 'hips'))
    for i in range(pouches):
        ang = math.radians(-50 - i * 35)
        p = _v(J['pelvis'].x + math.cos(ang) * (0.2 * s['bulk'] + 0.05), math.sin(ang) * (0.23 * s['bulk'] + .05), zc - 0.07)
        pouch = geo.box('Belt pouch', (0.08, 0.06, 0.1), p, mats['leather'], coll, bevel=0.02,
                        rotation=(0, 0, math.degrees(ang)))
        out.append((pouch, 'hips'))
        out.append((geo.box('Pouch flap', (0.085, 0.065, 0.04), p + _v(0, 0, 0.045), mats['leather'], coll,
                            bevel=0.012, rotation=(0, 0, math.degrees(ang))), 'hips'))
    if sash:
        pts = [front + _v(0.02, -0.05, 0), front + _v(0.0, -0.12, -0.15), front + _v(-0.03, -0.14, -0.3)]
        out.append((geo.tube('Sash tail', pts, 0.03, sash, coll, sides=6, flatten=.35), {'skin': ['hips', 'thigh.R']}))
    return out


def gorget(J, s, mats, coll, high=False):
    sec = ellipse(J['neck'].x, J['neck'].z, 0.13, 0.16, 24)
    top = ellipse(J['neck'].x + .01, J['neck'].z + (0.12 if high else 0.07), 0.1, 0.11, 24)
    low = ellipse(J['neck'].x - .005, J['neck'].z - 0.05, 0.2 * s['bulk'], 0.24 * s['bulk'] * s['chest_w'], 24)
    g = geo.loft('Gorget', [low, sec, top], mats['steel'], coll, cap=False)
    _solid(g, 0.016)
    _sub(g, 1)
    return [(g, 'chest')]


def mantle(J, s, mat, coll, thickness=0.08, seed=5, drop=0.2, ruff=1.0):
    """Fur/cloth mantle hugging the shoulders (collar into a short cape over the deltoids)."""
    sh = s['shoulder'] * s['chest_w'] + 0.1
    fx = 0.2 * s['bulk'] + 0.07
    rows = [(0.07, 0.11, 0.12), (0.01, 0.16, 0.19), (-0.06, fx * .9, sh * .85), (-0.12, fx, sh),
            (-0.12 - drop * .6, fx * 1.02, sh * 1.02), (-0.12 - drop, fx * 1.03, sh * .98)]
    secs = [ellipse(J['neck'].x - .02, J['neck'].z + dz, rx, ry, 32, -.05, .15) for dz, rx, ry in rows]
    m = geo.loft('Mantle', secs, mat, coll, cap=False)
    geo.subdivide(m, 1)
    from mathutils import noise as _n
    for v in m.data.vertices:
        rel = v.co - J['neck']
        n = _n.noise(v.co * 14 + Vector((seed, seed, seed)))
        v.co += Vector((rel.x, rel.y, 0)).normalized() * (0.012 * ruff + 0.02 * ruff * n)
        # tufted hem
        if rel.z < -0.12 - drop * .8:
            v.co.z -= 0.03 * ruff * (0.5 + 0.5 * math.sin(math.atan2(rel.y, rel.x) * 11 + seed))
    m.data.update()
    geo.store_rest(m)
    _solid(m, thickness * .35, 1)
    return [(m, {'skin': ['chest', 'neck', 'shoulder.R', 'shoulder.L', 'upper_arm.R', 'upper_arm.L']})]


# ------------------------------------------------------------------ limbs
def pauldron(J, side, mats, coll, size=1.0, lames=3, spikes=0, style='round', trim=True):
    sy = -1 if side == 'R' else 1
    sp = J['shoulder.' + side]
    out = []
    axis = _v(0, sy * 0.75, 0.66).normalized()
    rot = Vector((0, 0, 1)).rotation_difference(axis).to_matrix().to_4x4()
    for i in range(lames):
        r = 0.16 * size * (1 - i * 0.07)
        h = 0.12 * size * (1 - i * 0.1)
        shell = geo.shell_cap('Pauldron lame', r, h, mats['steel'], coll, segments=24, rings=8, thickness=0.018,
                              lip=0.0)
        geo.transform(shell, Matrix.Translation(sp + _v(0.0, sy * (0.035 - i * 0.03), 0.04 - i * 0.075 * size)) @ rot @
                      Matrix.Scale(1.0, 4))
        out.append((shell, 'upper_arm.' + side))
        if trim and i == 0:
            pass
    if style == 'flared':
        wing = geo.shell_cap('Pauldron haute', 0.1 * size, 0.04, mats['steel'], coll, segments=16, rings=5,
                             thickness=0.016)
        geo.transform(wing, Matrix.Translation(sp + _v(-0.02, sy * 0.05, 0.16 * size)) @
                      Vector((0, 0, 1)).rotation_difference(_v(0, sy * .3, 1)).to_matrix().to_4x4() @
                      Matrix.Diagonal((1.3, 0.35, 1, 1)))
        out.append((wing, 'shoulder.' + side))
    for k in range(spikes):
        a = (k - (spikes - 1) / 2) * 0.5
        base = sp + _v(math.sin(a) * 0.08, sy * 0.13 * size, 0.1 * size)
        tip = base + _v(math.sin(a) * 0.05, sy * 0.04, 0.16 * size)
        out.append((geo.tube('Pauldron spike', [base, tip], [0.035 * size, 0.003], mats.get('spike', mats['steel']),
                             coll, sides=8), 'upper_arm.' + side))
    return out


def vambrace(J, side, mats, coll, r=0.072, flare=0.025, length=0.9, mat_key='steel'):
    el, wr = J['elbow.' + side], J['wrist.' + side]
    a = el.lerp(wr, 1 - length)
    pts = [a, a.lerp(wr, .5), wr + (wr - el).normalized() * 0.015]
    v = geo.tube('Vambrace', pts, [r * .95, r, r + flare], mats[mat_key], coll, sides=14, cap=False)
    _solid(v, 0.014, 0)
    _bevel(v, 0.004)
    return [(v, 'forearm.' + side)]


def couter(J, side, mats, coll, size=1.0):
    sy = -1 if side == 'R' else 1
    el = J['elbow.' + side]
    c = geo.shell_cap('Couter', 0.07 * size, 0.055 * size, mats['steel'], coll, segments=16, rings=6, thickness=0.014)
    geo.transform(c, Matrix.Translation(el + _v(-0.03, sy * 0.02, 0)) @
                  Vector((0, 0, 1)).rotation_difference(_v(-0.8, sy * .5, -0.1)).to_matrix().to_4x4())
    fan = geo.shell_cap('Couter fan', 0.06 * size, 0.02, mats['steel'], coll, segments=12, rings=4, thickness=0.01)
    geo.transform(fan, Matrix.Translation(el + _v(0, sy * 0.07, 0)) @
                  Vector((0, 0, 1)).rotation_difference(_v(0, sy, 0)).to_matrix().to_4x4() @ Matrix.Diagonal((1, 1.3, 1, 1)))
    return [(c, 'upper_arm.' + side), (fan, 'upper_arm.' + side)]


def upper_arm_plate(J, side, mats, coll, r=0.088):
    sp, el = J['shoulder.' + side], J['elbow.' + side]
    pts = [sp.lerp(el, .3), el.lerp(sp, .1)]
    v = geo.tube('Rerebrace', pts, [r, r * .92], mats['steel'], coll, sides=14, cap=False)
    _solid(v, 0.012, 0)
    return [(v, 'upper_arm.' + side)]


def gauntlet(J, side, mats, coll, size=1.0, mat_key='steel', cuff=True):
    sy = -1 if side == 'R' else 1
    wr, ht = J['wrist.' + side], J['hand_tip.' + side]
    c = wr.lerp(ht, 0.55)
    d = (ht - wr).normalized()
    fist = geo.box('Gauntlet fist', (0.11 * size, 0.085 * size, 0.13 * size), (0, 0, 0), mats[mat_key], coll,
                   bevel=0.0, subsurf=2)
    geo.transform(fist, Matrix.Translation(c + _v(0.01, 0, 0)) @ Vector((0, 0, -1)).rotation_difference(d).to_matrix().to_4x4())
    out = [(fist, 'hand.' + side)]
    # knuckle plate
    kn = geo.box('Knuckle plate', (0.03, 0.095 * size, 0.06 * size), c + _v(0.055 * size, 0, -0.02), mats[mat_key], coll,
                 bevel=0.01)
    out.append((kn, 'hand.' + side))
    thumb = geo.tube('Gauntlet thumb', [c + _v(0.03, -sy * 0.0, 0.03), c + _v(0.08, -sy * 0.02, -0.0)],
                     [0.026, 0.022], mats[mat_key], coll, sides=8)
    out.append((thumb, 'hand.' + side))
    if cuff:
        cf = geo.lathe('Gauntlet cuff', [(0.055, 0.0), (0.075, 0.07), (0.085, 0.09)], 18, mats[mat_key], coll,
                       close_top=False, close_bottom=False)
        _solid(cf, 0.01)
        geo.transform(cf, Matrix.Translation(wr - d * 0.02) @ Vector((0, 0, 1)).rotation_difference(-d).to_matrix().to_4x4())
        out.append((cf, 'hand.' + side))
    return out


def glove(J, side, mat, coll, size=1.0):
    return gauntlet(J, side, {'g': mat}, coll, size, 'g', cuff=True)


def leg_plates(J, side, mats, coll, cuisse=True, poleyn=True, greave=True, size=1.0):
    sy = -1 if side == 'R' else 1
    hp, kn, an = J['hip.' + side], J['knee.' + side], J['ankle.' + side]
    out = []
    if cuisse:
        pts = [hp.lerp(kn, .25), kn.lerp(hp, .12)]
        c = geo.tube('Cuisse', pts, [0.13 * size, 0.1 * size], mats['steel'], coll, sides=16, cap=False)
        geo.bend_fn(c, lambda co: co if co.x > hp.x - 0.02 else _v(hp.x - 0.02 + (co.x - hp.x + 0.02) * 0.3, co.y, co.z))
        _solid(c, 0.012, 0)
        out.append((c, 'thigh.' + side))
    if poleyn:
        p = geo.shell_cap('Poleyn', 0.085 * size, 0.06 * size, mats['steel'], coll, segments=16, rings=6, thickness=0.014)
        geo.transform(p, Matrix.Translation(kn + _v(0.05, 0, 0.0)) @
                      Vector((0, 0, 1)).rotation_difference(_v(1, 0, 0.1)).to_matrix().to_4x4())
        out.append((p, 'shin.' + side))
        w = geo.shell_cap('Poleyn wing', 0.055 * size, 0.02, mats['steel'], coll, segments=12, rings=4, thickness=0.01)
        geo.transform(w, Matrix.Translation(kn + _v(0.02, sy * 0.075, 0)) @
                      Vector((0, 0, 1)).rotation_difference(_v(0.2, sy, 0)).to_matrix().to_4x4() @ Matrix.Diagonal((1, 1.3, 1, 1)))
        out.append((w, 'shin.' + side))
    if greave:
        pts = [kn.lerp(an, .1), kn.lerp(an, .5) + _v(0.012, 0, 0), an + _v(0, 0, 0.05)]
        g = geo.tube('Greave', pts, [0.088 * size, 0.092 * size, 0.07 * size], mats['steel'], coll, sides=16, cap=False)
        _solid(g, 0.012, 0)
        out.append((g, 'shin.' + side))
    return out


def boot(J, side, mat, coll, size=1.0, shaft=0.18, cuff=None, toe='round', sole=None, plate=None):
    sy = -1 if side == 'R' else 1
    an, toe_p = J['ankle.' + side], J['toe.' + side]
    out = []
    L = (toe_p - an).length * 1.25 * size
    W = 0.068 * size
    secs = []
    n = 8
    for k in range(n + 1):
        t = k / n
        x = an.x - 0.07 * size + L * t
        h = (0.13 * size) * (1 - 0.55 * t ** 1.5) if t > 0.25 else 0.13 * size * (0.85 + 0.15 * (t / .25))
        w = W * (0.85 + 0.35 * math.sin(math.pi * min(1, t * 1.1)) if t < .9 else W * (1.0 - (t - .9) * 4))
        if toe == 'pointed' and t > .7:
            w *= 1 - (t - .7) * 2.2
        sec = []
        for i in range(12):
            a = i * math.tau / 12
            yy = math.cos(a) * w
            zz = 0.01 + (math.sin(a) * 0.5 + 0.5) * h
            sec.append(_v(x, an.y + yy, zz))
        secs.append(sec)
    foot = geo.loft('Boot', secs, plate or mat, coll)
    _sub(foot, 1)
    out.append((foot, 'foot.' + side))
    if shaft:
        pts = [an + _v(-0.01, 0, 0.0), an + _v(0.0, 0, shaft)]
        sh = geo.tube('Boot shaft', pts, [0.075 * size, 0.08 * size], mat, coll, sides=14, cap=False)
        _solid(sh, 0.012, 0)
        out.append((sh, 'shin.' + side))
        if cuff is not None:
            cf = geo.lathe('Boot cuff', [(0.08 * size, 0), (0.095 * size, 0.05)], 16, cuff, coll, close_top=False,
                           close_bottom=False)
            _solid(cf, 0.012, 0)
            geo.place(cf, an + _v(0, 0, shaft - 0.02))
            out.append((cf, 'shin.' + side))
    if sole is not None:
        so = geo.box('Sole', (L * 1.02, W * 2.1, 0.025), _v(an.x - 0.07 + L * 0.5, an.y, 0.012), sole, coll, bevel=0.01)
        out.append((so, 'foot.' + side))
    return out


def sleeve_cuff(J, side, mat, coll, r=0.08):
    wr, el = J['wrist.' + side], J['elbow.' + side]
    d = (wr - el).normalized()
    cf = geo.lathe('Sleeve cuff', [(r * .9, 0), (r * 1.15, 0.06)], 16, mat, coll, close_top=False, close_bottom=False)
    _solid(cf, 0.01, 0)
    geo.transform(cf, Matrix.Translation(wr - d * 0.08) @ Vector((0, 0, 1)).rotation_difference(d).to_matrix().to_4x4())
    return [(cf, 'forearm.' + side)]


def wide_sleeve(J, side, mat, coll, r0=0.1, r1=0.17, length=1.0):
    """Bell sleeve for robes (upper arm to past the wrist)."""
    sp, el, wr = J['shoulder.' + side], J['elbow.' + side], J['wrist.' + side]
    d = (wr - el).normalized()
    pts = [sp + _v(0, 0, 0.02), el, el.lerp(wr, .6), wr + d * 0.02 * length]
    s1 = geo.tube('Upper sleeve', pts[:2], [r0 * 1.05, r0 * 1.0], mat, coll, sides=14, cap=False)
    s2 = geo.tube('Bell sleeve', pts[1:], [r0, r0 * 1.2, r1], mat, coll, sides=14, cap=False)
    for s_ in (s1, s2):
        geo.displace_noise(s_, 0.008, 18)
        _solid(s_, 0.012, 0)
        _sub(s_, 1)
    return [(s1, 'upper_arm.' + side), (s2, 'forearm.' + side)]


def scabbard(J, mats, coll, side='L', length=0.8):
    sy = -1 if side == 'R' else 1
    p = J['pelvis'] + _v(-0.05, sy * 0.22, -0.02)
    tip = p + _v(-0.35, sy * 0.05, -length * 0.85)
    s = geo.tube('Scabbard', [p, p.lerp(tip, .5), tip], [0.035, 0.032, 0.022], mats['leather'], coll, sides=8, flatten=.45)
    chape = geo.tube('Chape', [p.lerp(tip, .88), tip + (tip - p).normalized() * 0.02], [0.034, 0.02], mats['trim'], coll, sides=8,
                     flatten=.45)
    hilt = geo.tube('Sheathed hilt', [p, p + (p - tip).normalized() * 0.18], 0.022, mats['grip'], coll, sides=8)
    guard = geo.box('Sheathed guard', (0.04, 0.16, 0.03), p + (p - tip).normalized() * 0.02, mats['trim'], coll, bevel=.008)
    pommel = geo.sphere('Sheathed pommel', 0.035, p + (p - tip).normalized() * 0.2, mats['trim'], coll, 10, 6)
    return [(s, 'hips'), (chape, 'hips'), (hilt, 'hips'), (guard, 'hips'), (pommel, 'hips')]


def quiver(J, mats, coll, arrows=7, fletch=None):
    out = []
    base = J['chest'] + _v(-0.24, -0.05, -0.32)
    top = base + _v(0.08, -0.14, 0.55)
    q = geo.tube('Quiver', [base, top], [0.075, 0.085], mats['leather'], coll, sides=14)
    out.append((q, 'chest'))
    out.append((geo.tube('Quiver rim', [top - (top - base).normalized() * 0.03, top], 0.09, mats['trim'], coll, sides=14),
                'chest'))
    d = (top - base).normalized()
    for i in range(arrows):
        a = i * 2.3
        off = _v(math.cos(a) * 0.035, math.sin(a) * 0.035, 0)
        s = top + off - d * 0.05
        e = s + d * (0.16 + 0.03 * math.sin(i * 1.7))
        out.append((geo.tube('Arrow shaft', [s, e], 0.008, mats['wood'], coll, sides=5), 'chest'))
        fl = geo.extrude('Fletching', [(0, 0), (0.025, 0.02), (0.025, 0.09), (0, 0.07)], 0.004, fletch or mats['cloth'],
                         coll, plane='XZ', bevel=0)
        geo.transform(fl, Matrix.Translation(e - d * 0.09) @ Vector((0, 0, 1)).rotation_difference(d).to_matrix().to_4x4() @
                      Matrix.Rotation(a, 4, 'Z'))
        out.append((fl, 'chest'))
    strap = [J['chest'] + _v(0.21, -0.2, 0.15), J['chest'] + _v(0.2, 0.0, -0.05), J['chest'] + _v(0.17, 0.2, -0.25)]
    out.append((geo.tube('Quiver strap', strap, 0.018, mats['leather'], coll, sides=6, flatten=.4), {'skin': ['chest', 'spine']}))
    return out


def backpack(J, mats, coll, size=1.0, bedroll=None):
    out = []
    c = J['chest'] + _v(-0.3 * size, 0, -0.05)
    out.append((geo.box('Pack', (0.2 * size, 0.36 * size, 0.42 * size), c, mats['leather'], coll, bevel=0.04,
                        subsurf=1), 'chest'))
    out.append((geo.box('Pack flap', (0.22 * size, 0.37 * size, 0.12 * size), c + _v(0.0, 0, 0.18 * size), mats['leather'],
                        coll, bevel=0.03), 'chest'))
    if bedroll is not None:
        out.append((geo.cylinder('Bedroll', 0.08 * size, 0.5 * size, c + _v(-0.02, 0, 0.3 * size), bedroll, coll, 16,
                                 rotation=(90, 0, 0)), 'chest'))
    return out
