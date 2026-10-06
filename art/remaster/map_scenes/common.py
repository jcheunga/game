"""District-map kit: a tilted orthographic miniature of a whole district (1280x960).

The map is a height field with painted masks (roads, rivers/coast, patchwork fields with hedgerows,
snow, ash), water planes, forests scattered with a spatial hash, and low-detail map-scale buildings
(minihouse) grouped into villages, walled towns and keeps. One unit = one metre; the camera frames
about 150 m of terrain so houses read at ~50 px.
"""
import math
import random

from mathutils import Matrix, Vector

from rk import arch, env, geo
from rk import dressing as P
from rk.core import srgb
from rk.env import C
from rk.nodekit import material

VIEW_W = 150.0
PITCH = 58.0
ASPECT = 960 / 1280


# ------------------------------------------------------------------ frame
def view_rect(margin=6.0):
    """Ground-plane extents the camera sees (x0, x1, y0, y1), padded by margin."""
    depth = VIEW_W * ASPECT / math.sin(math.radians(PITCH))
    return -VIEW_W / 2 - margin, VIEW_W / 2 + margin, -depth / 2 - margin, depth / 2 + margin + 18


def camera(target_z=0.0):
    th = math.radians(PITCH)
    d = Vector((0, math.cos(th), -math.sin(th)))
    target = Vector((0, 0, target_z))
    cam = env.camera(tuple(target - d * 160), tuple(target), lens=50)
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = VIEW_W
    cam.data.clip_end = 800
    return cam


# ------------------------------------------------------------------ geometry helpers
def smooth_path(pts, steps=6):
    """Catmull-Rom through control points -> dense polyline."""
    pts = [Vector((p[0], p[1], 0)) for p in pts]
    out = []
    ext = [pts[0] * 2 - pts[1]] + pts + [pts[-1] * 2 - pts[-2]]
    for i in range(1, len(ext) - 2):
        p0, p1, p2, p3 = ext[i - 1], ext[i], ext[i + 1], ext[i + 2]
        for k in range(steps):
            t = k / steps
            t2, t3 = t * t, t * t * t
            q = 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)
            out.append((q.x, q.y))
    out.append((pts[-1].x, pts[-1].y))
    return out


class Grid:
    """Spatial hash for fast min-distance tests."""
    def __init__(self, cell=4.0):
        self.cell = cell
        self.g = {}

    def _k(self, x, y):
        return int(math.floor(x / self.cell)), int(math.floor(y / self.cell))

    def add(self, x, y, r=0.0):
        self.g.setdefault(self._k(x, y), []).append((x, y, r))

    def near(self, x, y, r):
        kx, ky = self._k(x, y)
        n = int(math.ceil((r + 6) / self.cell))
        for i in range(kx - n, kx + n + 1):
            for j in range(ky - n, ky + n + 1):
                for (px, py, pr) in self.g.get((i, j), ()):
                    if (px - x) ** 2 + (py - y) ** 2 < (r + pr) ** 2:
                        return True
        return False


class Polyline:
    def __init__(self, pts):
        self.pts = pts
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        self.bb = (min(xs), max(xs), min(ys), max(ys))

    def dist(self, x, y, cutoff=1e9):
        b = self.bb
        if x < b[0] - cutoff or x > b[1] + cutoff or y < b[2] - cutoff or y > b[3] + cutoff:
            return cutoff
        return env.poly_dist(x, y, self.pts)


# ------------------------------------------------------------------ fields
class Fields:
    """Patchwork farmland: voronoi parcels inside given (cx, cy, r) areas."""
    def __init__(self, areas, seed=0, parcel=9.0):
        rng = random.Random(seed)
        self.seeds = []
        self.areas = areas
        for (cx, cy, r) in areas:
            n = max(3, int((r * r * math.pi) / (parcel * parcel)))
            for i in range(n):
                a = rng.uniform(0, math.tau)
                rr = r * math.sqrt(rng.random())
                self.seeds.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr, rng.random()))

    def sample(self, x, y):
        """(inside 0..1, variant 0..1, hedge 0..1)."""
        inside = 0.0
        for (cx, cy, r) in self.areas:
            d = math.hypot(x - cx, y - cy)
            inside = max(inside, 1 - env.smoothstep(r * 0.82, r, d))
        if inside <= 0:
            return 0.0, 0.0, 0.0
        best = second = 1e9
        var = 0.0
        for (sx, sy, v) in self.seeds:
            d = (x - sx) ** 2 + (y - sy) ** 2
            if d < best:
                second, best, var = best, d, v
            elif d < second:
                second = d
        edge = math.sqrt(second) - math.sqrt(best)
        hedge = 1 - env.smoothstep(0.25, 0.7, edge)
        return inside, var, hedge * inside


# ------------------------------------------------------------------ terrain
def map_ground(name, grass=('3f5a2a', '5a7434', '84904a'), dry='a89a5a', dry_amount=0.2, dirt=('8c6c48', '6a5038'),
               rock='7d776c', snow_height=None, ash=0.0, ash_color='3a3330', sand='c8b488', field_colors=None,
               cobble=('8f8576', '6e665a'), mud=None):
    """Map-scale painted ground driven by vertex attributes:
    path (roads), field/fieldv/hedge (patchwork), shore (beach), cobble (town paving)."""
    fc = field_colors or ('c9a64a', '7a8a3a', '8a6a44', 'a8a050', '5e7a34')

    def build():
        m, n = material(name)
        p = n.rest()
        large = n.noise(p, 0.02, 3, 0.55)
        mid = n.noise(p, 0.12, 4, 0.6)
        fine = n.noise(p, 1.2, 3, 0.6)
        g = n.ramp(n.add(n.mul(large, 0.6), n.mul(mid, 0.4)), [(0.3, grass[0]), (0.5, grass[1]), (0.72, grass[2])])
        drym = n.smooth(n.add(mid, n.mul(large, 0.3)), 0.62 - dry_amount * 0.3, 0.75 - dry_amount * 0.3)
        g = n.mixc(n.mul(drym, 0.8), g, dry)
        g = n.mixc(n.maprange(fine, 0.3, 0.7, 0.0, 0.25), g, n.hsv(g, 0.5, 1.05, 0.72))
        # patchwork fields with furrows and hedgerows
        fin = n.attr('field', out='Fac')
        fv = n.attr('fieldv', out='Fac')
        fcol = n.ramp(fv, [(0.0, fc[0]), (0.25, fc[1]), (0.5, fc[2]), (0.75, fc[3]), (1.0, fc[4])], interp='CONSTANT')
        x, y, _ = n.xyz(p)
        ang = n.mul(fv, 9.0)
        u = n.add(n.mul(x, n.math('COSINE', ang)), n.mul(y, n.math('SINE', ang)))
        furrow = n.smooth(n.math('ABSOLUTE', n.math('SINE', n.mul(u, 3.2))), 0.4, 1.0)
        fcol = n.mixc(n.mul(furrow, 0.22), fcol, n.hsv(fcol, 0.5, 1.0, 0.7))
        fcol = n.mixc(n.maprange(fine, 0.3, 0.7, 0.0, 0.2), fcol, n.hsv(fcol, 0.5, 1.0, 1.2))
        col = n.mixc(n.smooth(fin, 0.4, 0.6), g, fcol)
        hedge = n.attr('hedge', out='Fac')
        col = n.mixc(n.smooth(hedge, 0.3, 0.7), col, '34401e')
        # beach / shore
        shore = n.attr('shore', out='Fac')
        scol = n.mixc(n.maprange(fine, 0.3, 0.7), sand, n.hsv(srgb(sand), 0.5, 1.0, 0.85))
        col = n.mixc(n.smooth(shore, 0.3, 0.7), col, scol)
        if mud:
            mm = n.attr('mud', out='Fac')
            col = n.mixc(n.smooth(mm, 0.3, 0.7), col, n.mixc(n.maprange(fine, 0.3, 0.7), mud[0], mud[1]))
        # cobbles (town paving)
        cob = n.attr('cobble', out='Fac')
        cv = n.vmath('MULTIPLY', p, (1, 1, 0.2))
        cell = n.voronoi(cv, 2.2, 'F1', out='Color')
        edge = n.voronoi(cv, 2.2, 'DISTANCE_TO_EDGE')
        stc = n.mixc(n.xyz(cell)[0], cobble[0], cobble[1])
        stc = n.mixc(n.smooth(edge, 0.08, 0.02), stc, n.hsv(srgb(cobble[1]), 0.5, 1.0, 0.55))
        col = n.mixc(n.smooth(cob, 0.4, 0.6), col, stc)
        # roads
        path = n.attr('path', out='Fac')
        dcol = n.mixc(n.smooth(n.noise(p, 0.6, 3, 0.6), 0.4, 0.6), dirt[0], dirt[1])
        rut = n.noise(n.vmath('MULTIPLY', p, (2.0, 2.0, 1.0)), 1.5, 3, 0.5)
        dcol = n.mixc(n.mul(n.smooth(rut, 0.5, 0.7), 0.3), dcol, n.hsv(srgb(dirt[1]), 0.5, 1.0, 0.8))
        col = n.mixc(n.smooth(n.add(path, n.mul(n.add(fine, -0.5), 0.3)), 0.4, 0.6), col, dcol)
        # slopes / snow / ash
        nz = n.xyz(n.geom('Normal'))[2]
        slope = n.smooth(nz, 0.8, 0.6)
        rcol = n.mixc(n.maprange(fine, 0.3, 0.7), rock, n.hsv(srgb(rock), 0.5, 0.9, 0.7))
        col = n.mixc(slope, col, rcol)
        _, _, z = n.xyz(p)
        if snow_height is not None:
            line = n.add(z, n.mul(n.add(mid, -0.5), 8.0))
            sm = n.mul(n.smooth(line, snow_height - 1.5, snow_height + 1.5), n.smooth(nz, 0.35, 0.65))
            col = n.mixc(sm, col, env.snow_color(n, p))
        if ash:
            am = n.smooth(n.add(large, n.mul(mid, 0.4)), 0.65 - ash * 0.4, 0.8 - ash * 0.4)
            col = n.mixc(n.mul(am, ash), col, ash_color)
        h = n.add(n.mul(fine, 0.4), n.mul(furrow, n.mul(fin, 0.3)))
        h = n.add(h, n.mul(hedge, 0.8))
        n.principled(**{'Base Color': col, 'Roughness': 0.9, 'Normal': n.bump(h, 0.3, 0.3)})
        return m
    return env.mat_cached(('mapground', name), build)


def build_terrain(height, mat, masks=None, fields=None, res=1.6):
    x0, x1, y0, y1 = view_rect()
    sx, sy = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    masks = dict(masks or {})
    obj = env.terrain((sx, sy), (int(sx * res / 2) * 2, int(sy * res / 2) * 2), (cx, cy), height, mat=mat,
                      masks=masks)
    if fields is not None:
        me = obj.data
        a_in = me.attributes.new('field', 'FLOAT', 'POINT')
        a_v = me.attributes.new('fieldv', 'FLOAT', 'POINT')
        a_h = me.attributes.new('hedge', 'FLOAT', 'POINT')
        vin, vv, vh = [], [], []
        avoid = masks.get('path')
        for v in me.vertices:
            i, var, hd = fields.sample(v.co.x, v.co.y)
            if avoid is not None and i > 0:
                i *= 1 - avoid(v.co.x, v.co.y)
            vin.append(i)
            vv.append(var)
            vh.append(hd)
        a_in.data.foreach_set('value', vin)
        a_v.data.foreach_set('value', vv)
        a_h.data.foreach_set('value', vh)
    return obj


def water(z, mat=None, name='Map water', deep='1d3a40', shallow='4f7a72'):
    x0, x1, y0, y1 = view_rect(20)
    m = mat or env.water(name, deep, shallow)
    o = geo.grid_sheet(name, 1, 1, 4, 4, lambda u, v: (x0 + (x1 - x0) * u, y0 + (y1 - y0) * v, z), m, C['Terrain'])
    return o


def carve(h_fn, line, width, depth, soft=3.0):
    """Wrap a height function so it dips into a channel along a Polyline."""
    def h(x, y):
        z = h_fn(x, y)
        d = line.dist(x, y, cutoff=width + soft + 2)
        t = 1 - env.smoothstep(width / 2, width / 2 + soft, d)
        return z * (1 - t) + (min(z, 0.0) - depth) * t
    return h


def flatten(h_fn, line, width, soft=2.5, z_fn=None):
    """Smooth a road bed along a Polyline (keeps the road at local ground height)."""
    def h(x, y):
        z = h_fn(x, y)
        d = line.dist(x, y, cutoff=width + soft + 2)
        if d > width / 2 + soft:
            return z
        t = 1 - env.smoothstep(width / 2, width / 2 + soft, d)
        target = z_fn(x, y) if z_fn else z
        return z * (1 - t) + target * t
    return h


def line_mask(line, width, soft=1.0):
    def f(x, y):
        return 1 - env.smoothstep(width / 2, width / 2 + soft, line.dist(x, y, cutoff=width + soft + 1))
    return f


# ------------------------------------------------------------------ buildings at map scale
def _mm(style):
    return arch.mats(style)


def minihouse(loc, rot=0.0, w=5.0, d=4.0, h=3.0, M=None, roof=None, style='warm', pitch=48, chimney=True, lit=False,
              timber=True):
    """Low-poly map house: walls, timber bands, overhanging gable roof, chimney, lit windows."""
    M = M or _mm(style)
    out = []
    wall = M['plaster'] if timber else M['stone']
    out.append(arch.box('Map house', (w, d, h), (0, 0, h / 2), wall, 0.03, out=None))
    if timber:
        out.append(arch.box('Map plinth', (w + 0.1, d + 0.1, 0.8), (0, 0, 0.4), M['stone2'], 0.02, out=None))
        for sy in (-1, 1):
            out.append(arch.box('Map beam', (w + 0.04, 0.1, 0.18), (0, sy * (d / 2 + 0.02), h * 0.55), M['timber'], 0,
                                out=None))
    rise = (d / 2) * math.tan(math.radians(pitch))
    ov = 0.35
    for s in (-1, 1):
        a = math.radians(pitch)
        L = math.hypot(d / 2 + ov, (d / 2 + ov) * math.tan(a))
        c = Vector((0, s * (d / 2 + ov) / 2, h + rise - (d / 2 + ov) * math.tan(a) / 2 + 0.05))
        o = geo.box('Map roof', (w + 2 * ov, L, 0.18), (0, 0, 0), roof or M['roof'], C['Architecture'], bevel=0.02)
        env.finish(o)
        geo.transform(o, Matrix.Translation(c) @ Matrix.Rotation(-s * a, 4, 'X'))
        out.append(o)
    gab = geo.extrude('Map gable', [(-d / 2, 0), (d / 2, 0), (0, rise)], w - 0.04, wall, C['Architecture'], plane='YZ',
                      bevel=0)
    geo.place(gab, (0, 0, h))
    env.finish(gab)
    out.append(gab)
    if chimney:
        out.append(arch.box('Map chimney', (0.5, 0.5, rise + 1.0), (w * 0.28, d * 0.15, h + (rise + 1.0) / 2), M['stone2'],
                            0.02, out=None))
    if lit:
        out.append(arch.box('Map window', (0.5, 0.06, 0.55), (-w * 0.2, -d / 2 - 0.03, h * 0.45), M['glass'], 0,
                            out=None))
    return env.group_xform(out, loc, rot)


def village(center, n, height, rng, radius=10.0, style='warm', M=None, avoid=None, grid=None, lit=0.3,
            size=(4.0, 6.0), roof_mats=None, align=None):
    """Cluster of minihouses; align(x, y) -> yaw (e.g. along a road)."""
    M = M or _mm(style)
    placed = []
    tries = 0
    while len(placed) < n and tries < n * 40:
        tries += 1
        a = rng.uniform(0, math.tau)
        r = radius * math.sqrt(rng.random())
        x, y = center[0] + math.cos(a) * r, center[1] + math.sin(a) * r
        w = rng.uniform(*size)
        if avoid and avoid(x, y):
            continue
        if grid and grid.near(x, y, w * 0.7):
            continue
        rot = align(x, y) if align else rng.choice((0, math.pi / 2)) + rng.uniform(-0.15, 0.15)
        roof = rng.choice(roof_mats) if roof_mats else None
        minihouse((x, y, height(x, y) - 0.2), rot, w, w * rng.uniform(0.7, 0.85), rng.uniform(2.6, 3.4), M, roof=roof,
                  style=style, lit=rng.random() < lit)
        if grid:
            grid.add(x, y, w * 0.7)
        placed.append((x, y))
    return placed


def walled_town(center, radius, height, rng, style='warm', M=None, n_houses=18, towers=8, keep=True, banner=None,
                wall_h=5.0, grid=None, gate_angles=(), keep_h=16.0):
    """Ring wall with towers, an inner keep and packed houses."""
    M = M or _mm(style)
    cx, cy = center
    pts = []
    for k in range(towers):
        a = k * math.tau / towers + rng.uniform(-0.1, 0.1)
        rr = radius * rng.uniform(0.92, 1.08)
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    for i in range(towers):
        p0, p1 = pts[i], pts[(i + 1) % towers]
        mid = ((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2)
        ang = math.degrees(math.atan2(mid[1] - cy, mid[0] - cx)) % 360
        z = min(height(*p0), height(*p1))
        if any(abs(((ang - g + 180) % 360) - 180) < 360 / towers / 2 for g in gate_angles):
            arch.gatehouse((mid[0], mid[1], height(*mid) - 0.3), math.atan2(mid[1] - cy, mid[0] - cx) - math.pi / 2,
                           w=8, d=5, h=wall_h + 1.5, gate_w=3.0, gate_h=3.6, style=style, M=M, towers=False, banner=banner)
            for q in (p0, p1):
                dx, dy = q[0] - mid[0], q[1] - mid[1]
                ln = math.hypot(dx, dy) or 1.0
                arch.wall(q, (mid[0] + dx / ln * 4.0, mid[1] + dy / ln * 4.0), wall_h, 1.8, M=M, base_z=z - 0.5)
        else:
            arch.wall(p0, p1, wall_h, 1.8, M=M, base_z=z - 0.5)
        if grid:
            for t in (0.0, 0.25, 0.5, 0.75):
                grid.add(p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t, 2.0)
    for (x, y) in pts:
        arch.tower((x, y, height(x, y) - 0.5), 2.2, wall_h + 3.5, 'cone', style=style, M=M, seed=int(x * 7 + y),
                   banner=banner, roof_h=5.0, windows=1)
        if grid:
            grid.add(x, y, 3.0)
    if keep:
        kz = height(cx, cy)
        arch.tower((cx, cy, kz - 0.5), 4.0, keep_h, 'cone', style=style, M=M, seed=5, banner=banner, roof_h=8.0)
        for k in range(3):
            a = k * math.tau / 3 + 0.5
            arch.tower((cx + math.cos(a) * 5.0, cy + math.sin(a) * 5.0, kz - 0.5), 2.0, keep_h * 0.65, 'cone', style=style,
                       M=M, seed=6 + k, roof_h=4.5, windows=1)
        if grid:
            grid.add(cx, cy, 8.0)
    village((cx, cy), n_houses, height, rng, radius * 0.8, style, M, grid=grid, lit=0.4, size=(3.6, 5.0))


def bridge(p0, p1, z, width=4.0, M=None, style='warm'):
    """Stone arch bridge deck between two points at height z."""
    M = M or _mm(style)
    out = []
    p0, p1 = Vector((p0[0], p0[1], z)), Vector((p1[0], p1[1], z))
    d = p1 - p0
    u = d.normalized()
    arch.slab((p0 + p1) / 2, u, Vector((-u.y, u.x, 0)), d.length, width, 0.6, M['stone'], out, 'Bridge deck')
    nrm = Vector((-u.y, u.x, 0))
    for s in (-1, 1):
        arch.slab((p0 + p1) / 2 + nrm * s * (width / 2) + Vector((0, 0, 0.6)), u, (0, 0, 1), d.length, 0.9, 0.35,
                  M['stone2'], out, 'Bridge parapet')
    for t in (0.25, 0.5, 0.75):
        c = p0.lerp(p1, t)
        arch.slab(c - Vector((0, 0, 2.0)), u, nrm, 1.2, width * 0.9, 3.6, M['stone2'], out, 'Bridge pier')
    return out


def forest(n, area, height, rng, kinds=('broad', 'conifer'), scale=(1.2, 1.8), grid=None, avoid=None, gap=2.6,
           palette=None, seed=0, coll=None):
    """Dense woodland using shared prototypes and a spatial hash."""
    protos = []
    for k in kinds:
        for i in range(2):
            key = f'map{k}{i}{palette[0] if palette else ""}'
            if k == 'broad':
                protos.append(env.proto(key, lambda i=i: env.broadleaf(seed * 10 + i, 7.0 + i,
                                                                       **({'palette': palette} if palette else {}))))
            elif k == 'conifer':
                protos.append(env.proto(key, lambda i=i: env.conifer(seed * 10 + i + 3, 9.0 + i * 2,
                                                                     **({'palette': palette} if palette else {}))))
            elif k == 'cypress':
                protos.append(env.proto(key, lambda i=i: env.cypress(seed * 10 + i + 5, 9.0)))
            elif k == 'olive':
                protos.append(env.proto(key, lambda i=i: env.olive(seed * 10 + i + 7, 5.0)))
            elif k == 'dead':
                protos.append(env.proto(key, lambda i=i: env.dead_tree(seed * 10 + i + 9, 6.0 + i * 2)))
            elif k == 'bush':
                protos.append(env.proto(key, lambda i=i: env.bush(seed * 10 + i + 11, 1.2)))
    g = grid or Grid(4.0)
    placed = 0
    tries = 0
    while placed < n and tries < n * 12:
        tries += 1
        x, y = area(rng)
        if avoid and avoid(x, y):
            continue
        if g.near(x, y, gap * 0.5):
            continue
        g.add(x, y, gap * 0.5)
        env.inst(rng.choice(protos), (x, y, height(x, y) - 0.1), rng.uniform(0, math.tau), rng.uniform(*scale),
                 coll or C['Vegetation'])
        placed += 1
    return placed


def rect(x0, x1, y0, y1):
    return lambda r: (r.uniform(x0, x1), r.uniform(y0, y1))


def everywhere():
    x0, x1, y0, y1 = view_rect(4)
    return rect(x0, x1, y0, y1)


def scatter_rocks(n, height, rng, area=None, avoid=None, size=(0.8, 2.2), moss=0.3, grid=None):
    protos = [env.proto(f'maprock{i}{moss}', lambda i=i: env.rock(200 + i, 1.0, moss=moss)) for i in range(3)]
    area = area or everywhere()
    k = 0
    tries = 0
    while k < n and tries < n * 20:
        tries += 1
        x, y = area(rng)
        if avoid and avoid(x, y):
            continue
        if grid and grid.near(x, y, 1.5):
            continue
        env.inst(rng.choice(protos), (x, y, height(x, y) - 0.2), rng.uniform(0, 6.28), rng.uniform(*size), C['Props'])
        k += 1


def copses(n, height, rng, area, size=(5, 12), radius=(5.0, 9.0), kinds=('broad', 'conifer'), grid=None, avoid=None,
           palette=None, seed=0, scale=(1.1, 1.7)):
    """Small woods: n clusters of trees."""
    g = grid or Grid(4.0)
    total = 0
    for k in range(n):
        cx, cy = area(rng)
        if avoid and avoid(cx, cy):
            continue
        r = rng.uniform(*radius)
        m = rng.randint(*size)
        total += forest(m, lambda rr, cx=cx, cy=cy, r=r: (cx + rr.gauss(0, r * 0.5), cy + rr.gauss(0, r * 0.5)), height,
                        rng, kinds, scale=scale, grid=g, avoid=avoid, gap=2.6, palette=palette, seed=seed)
    return total


def joined_proto(key, builder):
    """Prototype from a multi-object builder (joins its parts into one mesh)."""
    def make():
        objs = [o for o in builder() if o.type == 'MESH']
        return geo.join(objs, key)
    return env.proto(key, make)


def place(proto, x, y, height, rot=0.0, s=1.0, coll=None, sink=0.1):
    return env.inst(proto, (x, y, height(x, y) - sink), rot, s, coll or C['Props'])


def pier(p0, p1, width=2.4, z=0.4, M=None):
    """Plank jetty on posts from p0 (shore) to p1 (water)."""
    M = M or P.pm()
    out = []
    p0, p1 = Vector((p0[0], p0[1], z)), Vector((p1[0], p1[1], z))
    d = p1 - p0
    u = d.normalized()
    nrm = Vector((-u.y, u.x, 0))
    arch.slab((p0 + p1) / 2, u, nrm, d.length, width, 0.2, env.planks('Pier planks', '6a5038', board=0.25, length=3.0),
              out, 'Pier deck', bevel=0)
    n = int(d.length / 2.5)
    for i in range(n + 1):
        c = p0.lerp(p1, i / max(1, n))
        for s in (-1, 1):
            out.append(geo.cylinder('Pier post', 0.18, 3.0, tuple(c + nrm * s * width / 2 - Vector((0, 0, 1.2))),
                                    M['wood_dark'], C['Props'], 6, bevel=0))
    return out


def lava_mat():
    def build():
        m, n = material('Lava')
        p = n.rest()
        a = n.noise(n.vmath('MULTIPLY', p, (0.3, 0.3, 1.0)), 1.5, 5, 0.6, distortion=0.8)
        crust = n.smooth(n.voronoi(p, 0.8, 'DISTANCE_TO_EDGE'), 0.0, 0.08, 0.0, 1.0)
        col = n.ramp(a, [(0.3, 'b02a08'), (0.55, 'ff6a14'), (0.75, 'ffc060')])
        st = n.mul(n.maprange(a, 0.3, 0.8, 0.6, 3.2), n.add(0.15, n.mul(n.math('SUBTRACT', 1.0, crust), 0.85)))
        n.principled(**{'Base Color': '2a1a14', 'Roughness': 0.6, 'Emission Color': col, 'Emission Strength': st})
        return m
    return env.mat_cached(('lava',), build)


def glow_water_mat(color='9cf25c', deep='14261c', strength=0.6, name='Blight water'):
    def build():
        m, n = material(name)
        p = n.rest()
        w = n.noise(n.vmath('MULTIPLY', p, (0.4, 0.4, 1.0)), 1.5, 4, 0.55)
        em = n.mul(n.smooth(w, 0.55, 0.8), strength)
        n.principled(**{'Base Color': deep, 'Roughness': 0.1, 'Coat Weight': 0.3, 'Emission Color': color,
                        'Emission Strength': em, 'Normal': n.bump(w, 0.2, 0.1)})
        return m
    return env.mat_cached(('glowwater', color, deep, strength), build)


def causeway(line, z=0.15, width=2.6, M=None, step=1.6):
    """Plank road on stilts following a Polyline across water."""
    M = M or P.pm()
    pts = line.pts
    out = []
    mat = env.planks('Causeway planks', '5a4632', board=0.22, length=2.4)
    acc = 0.0
    for a, b in zip(pts, pts[1:]):
        pa, pb = Vector((a[0], a[1], z)), Vector((b[0], b[1], z))
        d = pb - pa
        if d.length < 1e-3:
            continue
        u = d.normalized()
        arch.slab((pa + pb) / 2, u, Vector((-u.y, u.x, 0)), d.length + 0.1, width, 0.16, mat, out, 'Causeway', bevel=0)
        acc += d.length
        if acc > step:
            acc = 0.0
            nrm = Vector((-u.y, u.x, 0))
            for s in (-1, 1):
                out.append(geo.cylinder('Stilt', 0.12, 2.2, tuple(pa + nrm * s * width / 2 - Vector((0, 0, 0.9))),
                                        M['wood_dark'], C['Props'], 6, bevel=0))
    return out
