"""Rotbound Host gatehouse (enemy stronghold).

Asymmetric undead-held fortress gate, facade toward the battle camera (-Y):
  left   round tower, battered base, ruined slate cone with exposed rafters
  centre gate tower: pointed arch, half-raised portcullis, soul-light within, machicolations,
         horned-skull keystone, ruined crimson banners
  right  lower curtain whose wall-walk (z=3.05 around x=0.8) is where the stronghold mount
         lands (runtime anchor gatehouse px 896,304 -> mount base at px ~896,448), ending in a
         corbelled corner turret
Plague-green / soul-teal braziers, blight vines, bones and rubble ground it.
"""
import math
import random

from mathutils import Matrix, Vector, noise

from rk import core, geo, shaders as S, structures as ST, weapons as W
from rk.structures import Batch, V

from .wagon import conform

CRIMSON = '7a1e30'
PLAGUE = '9cf25c'
SOUL = '6ff0d2'

# layout
LT = V(-1.8, 0.1, 0)          # left tower centre
LT_R = 0.52
GX0, GX1 = -1.3, 0.3          # gate tower span
GY0, GY1 = -0.62, 0.7         # gate tower front/back faces
GATE_C, GATE_W, GATE_S = -0.5, 0.96, 1.12    # arch centre x, span, springing z
GTOP = 3.3                    # gate tower machicolation base
CX0, CX1 = 0.3, 1.84          # right curtain span
CY0, CY1 = -0.38, 0.62        # curtain front/back
WALK = 3.05                   # curtain wall-walk floor (mount lands here)
TT = V(2.0, -0.2, 0)          # corner turret centre
TT_R = 0.34
COURSE = 0.17
BLOCK = (0.15, 0.44)


def mats():
    return dict(
        stone=ST.ashlar('58595a', name='Weathered ashlar', warm='625a50', cool='4a5159', blight=.35, crack=.7,
                        crack_glow=SOUL, glow_strength=1.6, grime=.6, grime_height=1.5, var=.4, grime_color='1d2026'),
        trim=ST.ashlar('646460', name='Dressed trim stone', warm='6c6458', cool='565c62', crack=.4, grime=.45, var=.22,
                       grime_color='1d2026'),
        dark_stone=ST.ashlar('3d3d3c', name='Footing stone', warm='443f39', cool='363b40', crack=.6, grime=.8, grime_height=.6,
                             grime_color='16181c'),
        rubble=ST.ashlar('4e4e4c', name='Rubble', warm='58524a', cool='454a4f', crack=.3, grime=.7, grime_height=.4,
                         grime_color='1d2026'),
        slate=ST.ashlar('31353a', name='Slate shingles', warm='37383a', cool='2b3036', crack=.2, grime=0.0, var=.35, edge=1.7,
                        moss=.4),
        mortar=S.flat('1d1c1a', name='Mortar shadow', rough=.95),
        void=S.flat('040303', name='Void', rough=1.0),
        wood=ST.board('3e2e22', name='Grave timber', axis='Z', dark=.5, grime=.4),
        iron=ST.plate('35322e', name='Rusted iron', rough=.55, edge=1.9, wear=.4),
        dark_iron=ST.plate('1e1d1c', name='Black iron', rough=.48, edge=2.4, wear=.25),
        bone=S.bone('c4c0b0', name='Old bone'),
        bone_dark=S.bone('a8987a', name='Grave bone', stain='3b2c1c'),
        horn=S.bone('3a3029', name='Horn', stain='15100c', crack=.2),
        cloth=S.cloth('6e1a30', name='Ruined heraldry', var='2c0c16', weave=40, sheen=.25),
        soul=S.emissive(SOUL, 7, name='Soul light', core='c8fff2'),
        soul_dim=S.emissive('3fbfa6', 2.5, name='Soul light dim', core='8ff0dc'),
        plague=S.emissive(PLAGUE, 5, name='Plague light', core='d8ffb0'),
        soul_fire=ST.flame('12a88a', '4ff0c8', 1.25, name='Soul fire'),
        plague_fire=ST.flame('4ea81e', 'a8f060', 1.25, name='Plague fire'),
        vine=S.leather('141a10', name='Blight vine', rough=.7),
        leaf=S.leather('26361e', name='Blight ivy leaf', rough=.5),
        growth=ST.ashlar('1d2617', name='Blight growth', warm='26301b', cool='161d14', crack=.0, grime=0, var=.4, edge=1.8,
                         crack_glow=PLAGUE, glow_strength=3.0),
        rope=S.rope('6e5a40'),
        dark=S.flat('050404', name='Socket void'),
    )


def emblem_crowned_skull(bone, glow, iron):
    """Rotbound sigil: a skull under a three-pointed grave crown."""
    def build(origin, coll, s=1.0):
        out = W.emblem_skull(bone, glow)(origin, coll, s)
        out = [o for o in out if not o.name.startswith('Emblem bone')]
        pts = [(-0.085, 0.085), (0.085, 0.085), (0.085, 0.115), (0.075, 0.175), (0.045, 0.125), (0.0, 0.19), (-0.045, 0.125),
               (-0.075, 0.175), (-0.085, 0.115)]
        cr = geo.extrude('Grave crown', [(x * s, z * s) for x, z in pts], 0.014, iron, coll, plane='YZ', bevel=0.003)
        geo.place(cr, origin + V(0.004, 0, -0.005 * s))
        out.append(cr)
        return out
    return build


def skull_mats(M, glow):
    return dict(bone=M['bone'], dark=M['dark'], glow=glow, horn=M['horn'])


# ------------------------------------------------------------------ helpers
def arch_curve(xc, w, zs, k=0.86, n=8):
    r = k * w
    cl = xc - w / 2 + r
    th = math.acos((xc - cl) / r)              # angle of apex seen from the left centre
    left = [(cl + r * math.cos(a), zs + r * math.sin(a)) for a in
            (math.pi - (math.pi - th) * i / n for i in range(n + 1))]
    right = [(2 * xc - x, z) for x, z in reversed(left[:-1])]     # mirror for exact symmetry
    return left + right, r


def inside_arch(x, z, xc, w, zs, k=0.86):
    if abs(x - xc) > w / 2:
        return False
    if z <= zs:
        return True
    r = k * w
    c = xc - w / 2 + r if x < xc else xc + w / 2 - r
    return (x - c) ** 2 + (z - zs) ** 2 <= r * r


def voussoirs(B, xc, w, zs, y_face, depth, mat, k=0.86, n=7, thick=0.2, proud=0.035, key=None):
    pts, r = arch_curve(xc, w, zs, k, n)
    cl = xc - w / 2 + r
    cr = xc + w / 2 - r
    for i in range(len(pts) - 1):
        (x0, z0), (x1, z1) = pts[i], pts[i + 1]
        c0 = cl if i < n else cr
        c1 = cl if i + 1 <= n else cr
        def out(x, z, c):
            d = Vector((x - c, z - zs))
            d.normalize()
            return x + d.x * thick, z + d.y * thick
        ox0, oz0 = out(x0, z0, c0)
        ox1, oz1 = out(x1, z1, c1 if i + 1 != n else c0)
        if i + 1 == n:
            ox1, oz1 = x1, z1 + thick
        if i == n:
            ox0, oz0 = x0, z0 + thick
        g = 0.008
        yf = y_face - proud
        verts = [(x0 + g, yf, z0), (x1 - g, yf, z1), (ox1 - g, yf, oz1), (ox0 + g, yf, oz0),
                 (x0 + g, yf + depth, z0), (x1 - g, yf + depth, z1), (ox1 - g, yf + depth, oz1), (ox0 + g, yf + depth, oz0)]
        B.mesh(verts, [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)], mat)
    apex = pts[n]
    if key is not None:
        B.box((0.16, depth + 0.05, thick * 1.35), (apex[0], y_face - proud - 0.025 + (depth + 0.05) / 2, apex[1] + thick * 0.55),
              key)
    return pts


def merlons(B, x0, x1, y_face, z0, h, mat, count, depth=0.22, gap_ratio=0.42, skip=(), broken=(), along='X', rng=None):
    """Crenellation: `count` merlons from x0..x1 on a face. broken: indices with a toppled top."""
    rng = rng or B.rng
    pitch = (x1 - x0) / count
    w = pitch * (1 - gap_ratio)
    for i in range(count):
        if i in skip:
            continue
        cx = x0 + pitch * (i + 0.5)
        hh = h * (0.55 if i in broken else 1.0)
        for k, (za, zb) in enumerate(((z0, z0 + hh * 0.52), (z0 + hh * 0.52, z0 + hh))):
            ww = w - 0.012 + rng.uniform(-0.01, 0.01)
            if along == 'X':
                B.box((ww, depth, zb - za - 0.012), (cx, y_face + depth / 2, (za + zb) / 2), mat,
                      rot=(rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1.5, 1.5)) if i not in broken else (0, rng.uniform(-6, 6), 0))
            else:
                B.box((depth, ww, zb - za - 0.012), (y_face - depth / 2, cx, (za + zb) / 2), mat,
                      rot=(rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1.5, 1.5)))


def round_tower(B, c, r, z0, z1, mat, course=0.27, batter=(0.0, 0.0), rng=None):
    """Courses of curved ashlar; batter=(extra radius at the base, height of the batter)."""
    rng = rng or B.rng
    z = z0
    k = 0
    while z < z1 - 1e-4:
        h = min(course * rng.uniform(.9, 1.1), z1 - z)
        if z1 - (z + h) < course * .4:
            h = z1 - z
        extra = 0.0
        if batter[1] > 0 and z < batter[1]:
            extra = batter[0] * (1 - (z + h * .5) / batter[1])
        ST.stone_course_ring(B, c, r + extra, z, h, mat, depth=0.24, gap=0.014, offset=0.5 * (k % 2), jitter=0.012,
                             batter=batter[0] * h / batter[1] if batter[1] and z < batter[1] else 0.0)
        z += h
        k += 1


def blight_vine(coll, M, start, up_dir, length, seed, wall_normal=(0, -1, 0), pustules=True, thickness=0.022, leaves=True):
    """Blighted ivy hugging a flat wall: a climbing stem, short tendrils and clusters of dark leaves."""
    rng = random.Random(seed)
    p = Vector(start)
    n = Vector(wall_normal).normalized()
    pts = [p.copy()]
    d = Vector(up_dir).normalized()
    steps = 14
    for i in range(steps):
        q = noise.noise_vector(p * 2.3 + Vector((seed, seed * .7, 0)))
        side = d.cross(n).normalized()
        d = (d + side * q.x * 0.7).normalized()
        d = d - n * d.dot(n)                     # stay on the wall plane
        if d.z < 0.7:
            d.z = 0.7
        d.normalize()
        p = p + d * (length / steps)
        pts.append(p.copy())
    radii = [thickness * (1 - 0.75 * i / steps) + 0.004 for i in range(len(pts))]
    objs = [geo.tube('Blight vine', pts, radii, M['vine'], coll, sides=6)]
    if leaves:
        L = Batch('Blight ivy', coll, random.Random(seed))
        for i, c in enumerate(pts[1:], 1):
            for k in range(3):
                side = d.cross(n).normalized()
                off = side * rng.uniform(-0.09, 0.09) * (1 - i / (steps + 4)) + Vector((0, 0, rng.uniform(-0.04, 0.04)))
                q = c + off + n * 0.022
                rot = n.to_track_quat('Z', 'Y').to_matrix().to_4x4()
                M4 = Matrix.Translation(q) @ rot @ Matrix.Rotation(rng.uniform(0, math.tau), 4, 'Z') @ \
                    Matrix.Rotation(math.radians(rng.uniform(-35, 35)), 4, 'X')
                s_ = rng.uniform(0.75, 1.25) * (1 - 0.4 * i / steps)
                L.mesh([(0, 0, 0), (0.03 * s_, 0.025 * s_, 0.006), (0.0, 0.07 * s_, 0.01), (-0.03 * s_, 0.025 * s_, 0.006)],
                       [(0, 1, 2, 3)], M['leaf'], matrix=M4)
        objs.append(L.finish(bevel=0, weighted=False))
    if pustules:
        for k in range(2):
            j = rng.randint(2, steps - 4)
            pp = pts[j] + n * 0.02
            objs.append(geo.sphere('Blight pustule', 0.012 + 0.006 * rng.random(), pp, M['plague'], coll, 8, 6))
    return objs


def skull_pike(coll, M, base, height, R, glow, facing=(0.2, -1, 0), seed=0):
    b = Vector(base)
    top = b + V(0, 0, height)
    objs = [geo.tube('Pike shaft', [b, top], [0.016, 0.012], M['wood'], coll, sides=6)]
    objs += ST.oriented_skull('Piked skull', coll, top - V(0, 0, R * 0.2), R, skull_mats(M, glow), facing=facing,
                              jaw_open=0.35, tilt=random.Random(seed).uniform(-12, 12))
    objs.append(geo.lathe('Pike tip', [(0.012, 0), (0.0, 0.07)], 6, M['iron'], coll, location=top + V(0, 0, R * 0.7)))
    return objs


# ------------------------------------------------------------------ build
def wall(B, x0, x1, z0, z1, face, mat, skip=None, depth=0.2, along='X', normal=-1, course=COURSE, block=BLOCK):
    ST.ashlar_wall(B, x0, x1, z0, z1, face, mat, course=course, block=block, depth=depth, gap=0.009, jitter=0.014,
                   skip=skip, along=along, normal=normal, tilt=0.7)


def slit(T, M, p, d, h=0.34, glow=None):
    """Arrow slit in a wall facing `d` (unit, horizontal)."""
    d = Vector(d).normalized()
    a = math.degrees(math.atan2(d.y, d.x)) + 90
    T.box((0.14, 0.05, h + 0.1), p + d * 0.01, M['trim'], rot=(0, 0, a))
    T.box((0.038, 0.06, h), p + d * 0.016, M['void'], rot=(0, 0, a))
    if glow is not None:
        T.box((0.02, 0.062, h * 0.45), p + d * 0.018 - V(0, 0, h * 0.18), glow, rot=(0, 0, a))


def gibbet(coll, M, hang, seed=1):
    """Iron gibbet cage hanging from a chain, a skull peering out."""
    h = Vector(hang)
    objs = [ST.chain('Gibbet chain', coll, h, h - V(0, 0, 0.32), M['iron'], sag=0.0, link=0.035, wire=0.007)]
    c = h - V(0, 0, 0.62)
    G = Batch('Gibbet cage', coll, random.Random(seed))
    G.ring(c + V(0, 0, 0.26), 0.13, 0.15, 0.025, M['dark_iron'], 'Z', 20)
    G.ring(c - V(0, 0, 0.22), 0.11, 0.13, 0.025, M['dark_iron'], 'Z', 20)
    for k in range(8):
        a = math.tau * k / 8
        p0 = c + V(math.cos(a) * 0.14, math.sin(a) * 0.14, 0.26)
        p1 = c + V(math.cos(a) * 0.12, math.sin(a) * 0.12, -0.22)
        G.wedge_box(p0, p1, 0.016, 0.016, 0.016, 0.016, M['dark_iron'])
        G.wedge_box(p0, c + V(0, 0, 0.36), 0.014, 0.014, 0.014, 0.014, M['dark_iron'])
    G.finish(bevel=0.003)
    objs += ST.oriented_skull('Gibbet skull', coll, c + V(0.0, -0.02, 0.06), 0.075, skull_mats(M, M['soul']),
                              facing=(0.3, -1, 0), jaw_open=0.5, tilt=-10)
    for k in range(3):
        a = 1.2 + k * 0.9
        geo.tube('Gibbet rib', [c + V(math.cos(a) * 0.08, math.sin(a) * 0.08, -0.04), c + V(math.cos(a) * 0.1, math.sin(a) * 0.1, -0.16)],
                 0.012, M['bone_dark'], coll, sides=5)
    return objs


def growth(coll, M, center, size, seed):
    """Lumpy blight fungus clinging to the base of a wall."""
    rng = random.Random(seed)
    out = []
    for k in range(5):
        p = Vector(center) + V(rng.uniform(-size, size), rng.uniform(-size * .3, size * .3), rng.uniform(0, size * .5))
        o = geo.quadsphere('Blight growth', size * rng.uniform(.35, .6), p, M['growth'], coll, 2, scale=(1.3, .7, .8))
        geo.displace_noise(o, size * 0.18, 6.0, seed + k)
        out.append(o)
    return out


def build(coll, yaw=0.0):
    """yaw: presentation turn (degrees about Z) applied afterwards by the caller; only used so the
    tower-top banner keeps streaming toward screen-left and its sigil faces the camera."""
    M = mats()
    ry = math.radians(yaw)
    screen_left = V(-math.cos(ry), math.sin(ry), 0)       # model-space direction that ends up as world -X
    to_camera = V(-math.sin(ry), -math.cos(ry), 0)        # model-space direction that ends up as world -Y
    rng = random.Random(7)
    info = {'flames': []}
    B = Batch('Ashlar', coll, random.Random(1))
    T = Batch('Dressed trim', coll, random.Random(2))
    Mo = Batch('Mortar cores', coll, random.Random(3))
    crown = emblem_crowned_skull(M['bone'], M['soul'], M['dark_iron'])

    # ---------------------------------------------------------- left round tower
    round_tower(B, LT, LT_R, 0.0, 3.25, M['stone'], course=COURSE, batter=(0.12, 0.85))
    Mo.cyl(LT_R - 0.05, 3.3, LT + V(0, 0, 1.65), M['mortar'], segs=32)
    for i in range(20):
        a = math.tau * i / 20
        d = V(math.cos(a), math.sin(a), 0)
        for k in range(3):
            T.box((0.1, 0.1 + 0.05 * k, 0.07), LT + d * (LT_R + 0.01 + 0.035 * k) + V(0, 0, 3.08 + 0.072 * k), M['trim'],
                  rot=(0, 0, math.degrees(a) + 90))
    ST.stone_course_ring(T, LT, LT_R + 0.1, 3.3, 0.14, M['trim'], depth=0.18, gap=0.01)
    for i in range(11):
        if i in (4,):
            continue
        a0 = math.tau * i / 11 + 0.08
        ST.stone_course_ring(B, LT, LT_R + 0.1, 3.44, 0.26 if i not in (7,) else 0.13, M['stone'], count=2, depth=0.16,
                             a0=a0, a1=a0 + math.tau / 11 * 0.6)
    # ruined slate cone with exposed rafters
    cone_z0, cone_z1 = 3.55, 4.38
    Rr = LT_R + 0.14
    broken = (math.radians(-80), math.radians(-30))
    rows = 10
    Sh = Batch('Slate roof', coll, random.Random(4))
    slope = math.degrees(math.atan2(cone_z1 - cone_z0, Rr))
    for j in range(rows):
        t0 = j / rows
        z = cone_z0 + (cone_z1 - cone_z0) * t0
        r = Rr * (1 - t0) + 0.02
        n = max(5, int(math.tau * r / 0.11))
        for i in range(n):
            a = math.tau * (i + 0.5 * (j % 2)) / n
            aa = (a + math.pi) % math.tau - math.pi
            if j >= 3 and broken[0] < aa < broken[1] and rng.random() > 0.12:
                continue
            if j >= 2 and broken[0] - 0.3 < aa < broken[0] + 0.05 and rng.random() < 0.5:
                continue
            d = V(math.cos(a), math.sin(a), 0)
            p = LT + d * (r - 0.02) + V(0, 0, z + 0.05)
            Mx = Matrix.Translation(p) @ Matrix.Rotation(a, 4, 'Z') @ Matrix.Rotation(math.radians(slope - 90), 4, 'Y')
            Sh.box((0.022, math.tau * r / n * 1.06, (cone_z1 - cone_z0) / rows * 1.5), (0, 0, 0), M['slate'],
                   matrix=Mx @ Matrix.Rotation(math.radians(rng.uniform(-5, 5)), 4, 'X'))
    Sh.finish(bevel=0.004)
    geo.lathe('Roof interior', [(Rr - 0.04, cone_z0 + 0.02), (0.02, cone_z1 - 0.06)], 24, M['void'], coll,
              location=LT, close_top=False, close_bottom=False)
    Rf = Batch('Exposed rafters', coll, random.Random(5))
    for a in (broken[0] + 0.1, (broken[0] + broken[1]) / 2 + 0.05, broken[1] - 0.1):
        d = V(math.cos(a), math.sin(a), 0)
        Rf.wedge_box(LT + d * (Rr + 0.02) + V(0, 0, cone_z0 + 0.03), LT + V(0, 0, cone_z1 + 0.02) + d * 0.03, 0.045, 0.03, 0.045,
                     0.03, M['wood'])
    for z, r in ((cone_z0 + 0.3, Rr * 0.64), (cone_z0 + 0.58, Rr * 0.31)):
        Rf.ring(LT + V(0, 0, z), r - 0.02, r + 0.01, 0.035, M['wood'], 'Z', 20, broken[0] - 0.1, broken[1] + 0.1)
    Rf.finish(bevel=0.005)
    for z, a in ((1.35, -98), (2.35, -76)):
        d = V(math.cos(math.radians(a)), math.sin(math.radians(a)), 0)
        slit(T, M, LT + d * (LT_R - 0.01) + V(0, 0, z), d, glow=M['plague'])

    # ---------------------------------------------------------- gate tower
    def gate_skip(xa, xb, za, zb):
        xm = (xa + xb) / 2
        for x in (xa + .02, xb - .02, xm):
            for z in (za + .02, zb - .02, (za + zb) / 2):
                if inside_arch(x, z, GATE_C, GATE_W + 0.42, GATE_S + 0.0, 0.86):
                    return True
        if abs(xm - GATE_C) < 0.13 and 2.45 < (za + zb) / 2 < 2.95:
            return True
        return False
    for za, zb, out_ in ((0.0, 0.2, 0.09), (0.2, 0.4, 0.045)):
        wall(B, GX0 - out_, GX1 + out_, za, zb, GY0 - out_, M['dark_stone'], depth=0.24,
             skip=lambda xa, xb, za_, zb_: abs((xa + xb) / 2 - GATE_C) < GATE_W / 2 + 0.12)
    wall(B, GX0, GX1, 0.4, GTOP, GY0, M['stone'], skip=gate_skip)
    wall(B, GY0, GY1, 0.0, GTOP, GX1, M['stone'], along='Y', normal=1)
    # core with the gate passage left open
    hw = GATE_W / 2 + 0.02
    Mo.box((GATE_C - hw - GX0 - 0.06, GY1 - GY0 - 0.06, GTOP), ((GX0 + 0.03 + GATE_C - hw) / 2, (GY0 + GY1) / 2, GTOP / 2), M['mortar'])
    Mo.box((GX1 - 0.03 - GATE_C - hw, GY1 - GY0 - 0.06, GTOP), ((GX1 - 0.03 + GATE_C + hw) / 2, (GY0 + GY1) / 2, GTOP / 2), M['mortar'])
    Mo.box((2 * hw, GY1 - GY0 - 0.06, GTOP - 2.05), (GATE_C, (GY0 + GY1) / 2, (GTOP + 2.05) / 2), M['mortar'])
    pts = voussoirs(T, GATE_C, GATE_W, GATE_S, GY0, 0.34, M['trim'], n=7, thick=0.18, key=M['trim'])
    for sx in (-1, 1):
        x_in = GATE_C + sx * GATE_W / 2
        ST.ashlar_wall(T, min(x_in, x_in + sx * 0.17), max(x_in, x_in + sx * 0.17), 0.0, GATE_S, GY0 - 0.03, M['trim'],
                       course=0.16, block=(0.17, 0.17), depth=0.36)
    # passage: dark vault receding to a soul-lit inner door
    apts, _ = arch_curve(GATE_C, GATE_W, GATE_S, 0.86, 10)
    outline = [(GATE_C - GATE_W / 2, 0.0)] + apts + [(GATE_C + GATE_W / 2, 0.0)]
    for sx in (-1, 1):
        wall(B, GY0 + 0.3, GY1 - 0.1, 0.0, GATE_S + 0.2, GATE_C - sx * (GATE_W / 2), M['dark_stone'], along='Y', normal=sx,
             depth=0.12)
    vault = [(x, z) for x, z in apts]
    for i in range(len(vault) - 1):
        (x0, z0), (x1, z1) = vault[i], vault[i + 1]
        Mo.mesh([(x0, GY0 + 0.3, z0 + 0.02), (x1, GY0 + 0.3, z1 + 0.02), (x1, GY1 - 0.1, z1 + 0.02), (x0, GY1 - 0.1, z0 + 0.02)],
                [(0, 1, 2, 3)], M['dark_stone'])
    door = geo.extrude('Inner gate glow', [(x, z) for x, z in outline], 0.02, M['soul_dim'], coll, plane='XZ', bevel=0)
    geo.place(door, (0, GY1 - 0.12, 0))
    Mo.box((GATE_W + 0.1, 0.3, 0.04), (GATE_C, GY0 + 0.5, 0.0), M['dark_stone'])
    # half-raised portcullis
    P = Batch('Portcullis', coll, random.Random(6))
    pz = 0.62
    py = GY0 + 0.2
    nbars = 8
    for i in range(nbars):
        x = GATE_C - GATE_W / 2 + 0.07 + (GATE_W - 0.14) * i / (nbars - 1)
        P.box((0.034, 0.04, 2.2), (x, py, pz + 1.1), M['dark_iron'])
        P.mesh([(x - 0.021, py - 0.021, pz), (x + 0.021, py - 0.021, pz), (x + 0.021, py + 0.021, pz), (x - 0.021, py + 0.021, pz),
                (x, py, pz - 0.14)], [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4), (3, 2, 1, 0)], M['dark_iron'])
    for z in (pz + 0.1, pz + 0.42, pz + 0.74, pz + 1.06, pz + 1.38):
        P.box((GATE_W - 0.02, 0.03, 0.034), (GATE_C, py - 0.03, z), M['dark_iron'])
    P.finish(bevel=0.004)
    for sx in (-1, 1):
        ST.chain('Portcullis chain', coll, (GATE_C + sx * 0.3, py - 0.06, 1.85), (GATE_C + sx * 0.33, py - 0.06, pz + 1.38),
                 M['iron'], sag=0.02, link=0.04, wire=0.008)
    # horned skull keystone over the arch
    apex_z = max(z for _, z in apts)
    keyz = apex_z + 0.36
    T.box((0.48, 0.12, 0.07), (GATE_C, GY0 - 0.06, keyz - 0.24), M['trim'])
    ST.oriented_skull('Keystone skull', coll, (GATE_C, GY0 - 0.14, keyz), 0.17, skull_mats(M, M['soul']), facing=(0.12, -1, 0),
                      jaw_open=0.45, horns=dict(length=1.6, curl=1.0, thick=0.26, ram=True))
    for i, (x, z) in enumerate(pts[1:-1:2]):
        if abs(x - GATE_C) < 0.14:
            continue
        ST.oriented_skull('Arch skull', coll, (x, GY0 - 0.07, z + 0.09), 0.048, skull_mats(M, M['soul']),
                          facing=((x - GATE_C) * 0.6, -1, 0), jaw_open=0.25)
    slit(T, M, V(GATE_C, GY0 + 0.01, 2.7), (0, -1, 0), h=0.4, glow=M['plague'])
    # machicolation gallery
    mz = GTOP
    nc = 8
    for i in range(nc + 1):
        x = GX0 + (GX1 - GX0) * i / nc
        for k in range(3):
            T.box((0.11, 0.09 + 0.065 * k, 0.075), (x, GY0 - 0.025 - 0.033 * k, mz - 0.25 + 0.08 * k + 0.04), M['trim'])
    for i in range(nc):
        x = GX0 + (GX1 - GX0) * (i + 0.5) / nc
        Mo.box(((GX1 - GX0) / nc - 0.12, 0.12, 0.08), (x, GY0 - 0.075, mz - 0.03), M['void'])
    wall(T, GX0 - 0.07, GX1 + 0.07, mz, mz + 0.34, GY0 - 0.2, M['stone'], depth=0.18)
    T.box((GX1 - GX0 + 0.18, 0.24, 0.05), ((GX0 + GX1) / 2, GY0 - 0.09, mz + 0.36), M['trim'])
    merlons(B, GX0 - 0.07, GX1 + 0.07, GY0 - 0.2, mz + 0.385, 0.34, M['stone'], 6, depth=0.18, broken=(4,))
    merlons(B, GY0 - 0.2, GY1, GX1 + 0.07, mz + 0.385, 0.34, M['stone'], 4, along='Y', depth=0.16)
    wall(T, GY0 - 0.2, GY1, mz, mz + 0.34, GX1 + 0.07, M['stone'], along='Y', normal=1, depth=0.16)
    T.box((GX1 - GX0, GY1 - GY0 + 0.1, 0.1), ((GX0 + GX1) / 2, (GY0 + GY1) / 2 + 0.05, mz + 0.3), M['slate'])

    # ---------------------------------------------------------- right curtain (mount platform) + corner turret
    hole = (CX0 + 0.92, CX0 + 1.16, 1.98, 2.36)

    def cur_skip(xa, xb, za, zb):
        xm, zm = (xa + xb) / 2, (za + zb) / 2
        if hole[0] < xm < hole[1] and hole[2] < zm < hole[3]:
            return True
        for sx, sz in ((CX0 + 0.84, 1.45), (CX1 - 0.32, 1.7)):
            if abs(xm - sx) < 0.1 and abs(zm - sz) < 0.24:
                return True
        return False
    wall(B, CX0, CX1, 0.3, 2.74, CY0, M['stone'], skip=cur_skip)
    for za, zb, out_ in ((0.0, 0.17, 0.08), (0.17, 0.3, 0.04)):
        wall(B, CX0 - 0.02, CX1 + 0.05, za, zb, CY0 - out_, M['dark_stone'], depth=0.24)
    # mortar core stays behind both block faces (front at CY0, right end at CX1)
    Mo.box((CX1 - CX0 - 0.14, CY1 - CY0 - 0.12, 2.9), ((CX0 + CX1 - 0.14) / 2, (CY0 + CY1) / 2 + 0.06, 1.45), M['mortar'])
    for sx, sz in ((CX0 + 0.84, 1.45), (CX1 - 0.32, 1.7)):
        slit(T, M, V(sx, CY0 + 0.01, sz), (0, -1, 0), h=0.36, glow=M['plague'])
    # stepped buttress breaking up the curtain
    bx = CX0 + 0.5
    for k, (z0_, z1_, dy) in enumerate(((0.0, 0.9, 0.3), (0.9, 1.7, 0.22), (1.7, 2.35, 0.13))):
        wall(B, bx - 0.15, bx + 0.15, z0_, z1_, CY0 - dy, M['stone'], depth=dy + 0.02, block=(0.14, 0.3))
        wall(B, CY0 - dy, CY0, z0_, z1_, bx + 0.15, M['stone'], along='Y', normal=1, depth=0.12, block=(0.12, 0.3))
        T.box((0.34, dy + 0.06, 0.06), (bx, CY0 - dy / 2 - 0.01, z1_ + 0.02), M['trim'], rot=(-12, 0, 0))
    # right end of the curtain: faces the camera once the gatehouse turns toward the field
    wall(B, CY0, CY1, 0.3, 2.74, CX1, M['stone'], along='Y', normal=1)
    for za, zb, out_ in ((0.0, 0.17, 0.08), (0.17, 0.3, 0.04)):
        wall(B, CY0 - 0.06, CY1, za, zb, CX1 + out_, M['dark_stone'], along='Y', normal=1, depth=0.24)
    for i in range(7):
        y = CY0 + 0.06 + (CY1 - CY0 - 0.12) * i / 6
        for k in range(2):
            T.box((0.1 + 0.055 * k, 0.1, 0.075), (CX1 + 0.025 + 0.028 * k, y, 2.77 + 0.08 * k), M['trim'])
    T.box((0.2, CY1 - CY0 + 0.1, 0.1), (CX1 + 0.06, (CY0 + CY1) / 2 - 0.03, 2.98), M['trim'])
    merlons(B, CY0 - 0.12, CY1, CX1 + 0.12, WALK - 0.02, 0.32, M['stone'], 4, depth=0.16, along='Y')
    slit(T, M, V(CX1 - 0.01, 0.3, 1.25), (1, 0, 0), h=0.34, glow=M['plague'])
    # fallen block below the breach
    T.box((0.2, 0.16, 0.14), (hole[0] + 0.1, CY0 - 0.35, 0.08), M['stone'], rot=(12, 20, 30))
    T.box((0.15, 0.13, 0.12), (hole[0] + 0.3, CY0 - 0.5, 0.06), M['stone'], rot=(-8, 35, 70))
    for i in range(10):
        x = CX0 + 0.06 + (CX1 - CX0 - 0.12) * i / 9
        for k in range(2):
            T.box((0.1, 0.09 + 0.055 * k, 0.075), (x, CY0 - 0.025 - 0.028 * k, 2.77 + 0.08 * k), M['trim'])
    T.box((CX1 - CX0 + 0.04, 0.2, 0.1), ((CX0 + CX1) / 2, CY0 - 0.06, 2.98), M['trim'])
    Fl = Batch('Wall-walk flags', coll, random.Random(8))
    for i in range(7):
        for j in range(4):
            x0_ = CX0 + (CX1 - CX0) * i / 7
            y0_ = CY0 + 0.1 + (CY1 - CY0 - 0.1) * j / 4
            Fl.box(((CX1 - CX0) / 7 - 0.012, (CY1 - CY0 - 0.1) / 4 - 0.012, 0.06),
                   (x0_ + (CX1 - CX0) / 14, y0_ + (CY1 - CY0 - 0.1) / 8, WALK - 0.03 + rng.uniform(-0.006, 0.006)), M['trim'])
    Fl.finish(bevel=0.006)
    merlons(B, CX0, CX1, CY0 - 0.12, WALK - 0.02, 0.32, M['stone'], 6, depth=0.16, broken=(2,))
    merlons(B, CX0, CX1, CY1 - 0.16, WALK - 0.02, 0.28, M['stone'], 6, depth=0.14)
    # corner turret on corbels
    for k in range(4):
        ST.stone_course_ring(T, TT, TT_R * (0.5 + 0.17 * k), 1.36 + 0.09 * k, 0.09, M['trim'], depth=0.2, gap=0.01,
                             a0=-math.pi * 0.95, a1=math.pi * 0.45)
    round_tower(B, TT, TT_R, 1.72, 3.32, M['stone'], course=COURSE)
    Mo.cyl(TT_R - 0.04, 1.62, TT + V(0, 0, 2.52), M['mortar'], segs=24)
    ST.stone_course_ring(T, TT, TT_R + 0.05, 3.32, 0.11, M['trim'], depth=0.16)
    slit(T, M, TT + V(0.1, -TT_R + 0.01, 2.5), (0.25, -1, 0), h=0.3, glow=M['plague'])
    tz0, tz1 = 3.43, 4.15
    rows = 7
    slope = math.degrees(math.atan2(tz1 - tz0, TT_R + 0.1))
    for j in range(rows):
        t0 = j / rows
        z = tz0 + (tz1 - tz0) * t0
        r = (TT_R + 0.1) * (1 - t0) + 0.02
        n = max(5, int(math.tau * r / 0.1))
        for i in range(n):
            a = math.tau * (i + 0.5 * (j % 2)) / n
            d = V(math.cos(a), math.sin(a), 0)
            p = TT + d * (r - 0.02) + V(0, 0, z + 0.05)
            Mx = Matrix.Translation(p) @ Matrix.Rotation(a, 4, 'Z') @ Matrix.Rotation(math.radians(slope - 90), 4, 'Y')
            T.box((0.02, math.tau * r / n * 1.06, (tz1 - tz0) / rows * 1.5), (0, 0, 0), M['slate'],
                    matrix=Mx @ Matrix.Rotation(math.radians(rng.uniform(-4, 4)), 4, 'X'))
    skull_pike(coll, M, TT + V(0, 0, tz1 - 0.03), 0.28, 0.065, M['soul'], facing=(0.1, -1, 0), seed=3)
    objs, fb = ST.brazier('Wall-walk brazier', coll, (1.58, 0.2, WALK), dict(iron=M['dark_iron']), M['soul_fire'],
                          ST.coals(SOUL, strength=4, name='Soul embers'), light=core.srgb(SOUL), size=0.5, height=0.5,
                          light_power=30, seed=4)
    info['flames'] += [o for o in objs if 'fire' in o.name]

    # ---------------------------------------------------------- crown of the gate tower: banner pole, pikes
    pole = V(-0.75, 0.2, mz + 0.32)
    geo.tube('Banner pole', [pole, pole + V(0, 0, 0.8)], 0.024, M['wood'], coll, sides=8)
    geo.lathe('Pole spike', [(0.03, 0), (0.0, 0.12)], 6, M['iron'], coll, location=pole + V(0, 0, 0.8))
    fl, fn = _streamer(coll, pole + screen_left * 0.02 + V(0, 0, 0.76), 0.52, 0.88, M['cloth'], seed=5,
                       direction=tuple(screen_left))
    conform(crown(V(0, 0, 0), coll, 0.9), fn, 0.4, 0.48, 0.88, 0.52, lift=0.012, toward=tuple(to_camera))
    for x, g in ((GX0 + 0.1, M['plague']), (GX1 - 0.06, M['soul'])):
        skull_pike(coll, M, (x, GY0 - 0.11, mz + 0.72), 0.2, 0.06, g, seed=int(x * 10))
    skull_pike(coll, M, (CX0 + 0.1, CY0 - 0.04, WALK + 0.3), 0.18, 0.055, M['soul'], seed=9)

    # ---------------------------------------------------------- ruined banners hanging on the gate tower
    for k, x in enumerate((GATE_C - 0.6, GATE_C + 0.6)):
        top = V(x, GY0 - 0.22, mz - 0.3)
        geo.tube('Banner rod', [top + V(-0.21, 0, 0.02), top + V(0.21, 0, 0.02)], 0.015, M['iron'], coll, sides=6)
        for sx in (-1, 1):
            geo.tube('Banner cord', [top + V(sx * 0.19, 0, 0.02), top + V(sx * 0.12, 0.12, 0.25)], 0.006, M['rope'], coll, sides=4)
        sh, bfn = ST.banner_sheet('Ruined banner', coll, top, 0.34, 1.3, M['cloth'], tails=2, tail_depth=0.24, tatter=1.2,
                                  holes=0.1, seed=11 + k, wave=0.02, nx=10, ny=18, bulge=0.012)
        conform(crown(V(0, 0, 0), coll, 0.8), lambda u, v, f=bfn: f(u, v), 0.5, 0.3, 0.34, 1.3, lift=0.01)
    top = LT + V(0.06, -LT_R - 0.16, 3.2)
    geo.tube('Banner rod', [top + V(-0.17, 0, 0.02), top + V(0.17, 0, 0.02)], 0.013, M['iron'], coll, sides=6)
    ST.banner_sheet('Tower banner', coll, top, 0.28, 0.9, M['cloth'], tails=1, tail_depth=0.2, tatter=1.2, holes=0.12, seed=21,
                    wave=0.02, nx=8, ny=14, bulge=0.02)
    # gibbet cage swinging from a beam between the left tower and the gate
    beam0 = V(GX0 - 0.02, GY0 + 0.1, 2.75)
    T.box((0.5, 0.08, 0.08), beam0 + V(-0.12, -0.18, 0), M['trim'], rot=(0, 0, 20))
    gibbet(coll, M, beam0 + V(-0.28, -0.32, -0.02), seed=2)

    # ---------------------------------------------------------- gate braziers on pedestals
    for k, (x, fire, ember, col) in enumerate(((GATE_C - 0.78, M['plague_fire'], PLAGUE, PLAGUE),
                                                (GATE_C + 0.78, M['soul_fire'], SOUL, SOUL))):
        base = V(x, GY0 - 0.4, 0.0)
        T.box((0.26, 0.26, 0.3), base + V(0, 0, 0.15), M['trim'], rot=(0, 0, 6 * (k * 2 - 1)))
        T.box((0.32, 0.32, 0.05), base + V(0, 0, 0.325), M['trim'])
        objs, fb = ST.brazier('Gate brazier', coll, base + V(0, 0, 0.35), dict(iron=M['dark_iron']), fire,
                              ST.coals(ember, strength=4, name='Embers ' + ember), light=core.srgb(col), size=0.55, height=0.5,
                              light_power=45, seed=k + 1, flame_h=0.62)
        info['flames'] += [o for o in objs if 'fire' in o.name]

    # ---------------------------------------------------------- ground: causeway, rubble, bones, blight
    G = Batch('Ground rubble', coll, random.Random(9))
    for i in range(6):
        for j in range(3):
            x = GATE_C - 0.5 + i * 0.2 + (0.1 if j % 2 else 0)
            y = GY0 - 0.12 - j * 0.22
            if abs(x - GATE_C) > 0.55:
                continue
            G.box((0.18, 0.2, 0.05), (x, y, 0.02), M['dark_stone'], rot=(rng.uniform(-3, 3), rng.uniform(-3, 3), rng.uniform(-8, 8)))
    for k in range(34):
        x = rng.uniform(-2.25, 1.75)            # the far-right front corner leaves the safe frame once turned
        y = rng.uniform(-1.05, -0.5) if abs(x - GATE_C) > 0.55 else rng.uniform(-1.08, -0.98)
        if abs(x - LT.x) < LT_R + 0.15 and y > LT.y - LT_R - 0.15:
            continue
        sz = rng.uniform(0.04, 0.11)
        G.box((sz * 1.4, sz, sz * 0.8), (x, y, sz * 0.3), M['rubble'], rot=(rng.uniform(-30, 30), rng.uniform(-30, 30), rng.uniform(0, 90)))
    G.finish(bevel=0.01)
    for k in range(6):
        x = rng.uniform(-2.0, 1.9)
        if abs(x - GATE_C) < 0.55:
            continue
        p = V(x, rng.uniform(-1.1, -0.75), 0.02)
        a = rng.uniform(0, math.pi)
        geo.tube('Scattered bone', [p - V(math.cos(a), math.sin(a), 0) * 0.09, p + V(math.cos(a), math.sin(a), 0) * 0.09],
                 [0.02, 0.013, 0.02], M['bone_dark'], coll, sides=6)
    for k, (x, y) in enumerate(((-1.3, -0.98), (0.9, -0.8), (1.7, -0.92))):
        ST.oriented_skull('Ground skull', coll, (x, y, 0.055), 0.06, skull_mats(M, M['dark']), facing=(rng.uniform(-1, 1), -1, 0),
                          jaw_open=0.3, tilt=rng.uniform(-25, 25))
    for k, a in enumerate((-118, -92, -62)):
        d = V(math.cos(math.radians(a)), math.sin(math.radians(a)), 0)
        growth(coll, M, LT + d * (LT_R + 0.16) + V(0, 0, 0.04), 0.12, 50 + k)
    for k, (x, ln) in enumerate(((CX0 + 0.2, 1.1), (CX0 + 1.05, 0.7))):
        y = GY0 - 0.035 if x < GX1 else CY0 - 0.035
        blight_vine(coll, M, (x, y, 0.0), (0.06 * (k % 2 * 2 - 1), 0, 1), ln, 40 + k, pustules=True, thickness=0.026)
        growth(coll, M, (x, y - 0.06, 0.04), 0.11, 60 + k)
    growth(coll, M, (GATE_C + GATE_W / 2 + 0.28, GY0 - 0.1, 0.04), 0.09, 70)

    B.finish(bevel=0.012)
    T.finish(bevel=0.01)
    Mo.finish(bevel=0, weighted=False)
    core.point_light('Gate soul light', (GATE_C, GY0 + 0.55, 0.8), 45, core.srgb(SOUL), 0.2, coll)
    core.point_light('Gate soul spill', (GATE_C, GY0 - 0.35, 0.9), 12, core.srgb(SOUL), 0.3, coll)
    return info


def _streamer(coll, top, height, length, mat, seed=1, direction=(-1, 0, 0)):
    from siege.wagon import flag
    obj, fn = flag('Rotbound banner', coll, top, height, length, mat, direction=direction, wave=0.07, seed=seed, tails=True,
                   tatter=1.5)
    return obj, fn
