"""Medieval architecture for the menu dioramas: timber-framed houses, towers, curtain walls,
gatehouses, colonnades and ruins. Everything is authored in a local frame (front faces -Y)
and then baked into world space with env.group_xform."""
import math
import random

from mathutils import Matrix, Vector

from . import env, geo
from . import shaders as S
from .env import C


def _v(*a):
    return Vector(a)


# ------------------------------------------------------------------ materials (cached per scene)


def mats(style='warm'):
    """Material set for a settlement style: warm (golden southern), grey (northern), grim (undead)."""
    if style == 'grim':
        return dict(
            stone=env.masonry('Grim masonry', '5d5852', '48443f', '2a2724', moss=0.25, soot=0.5),
            stone2=env.masonry('Grim rubble', '4f4a44', '3c3833', '24211e', block=(0.45, 0.28), moss=0.3, soot=0.4),
            plaster=env.plaster('Grim plaster', '8a8273', '4a4236'),
            timber=S.wood('2e231b', name='Grim timber', axis='Z', scale=0.4, dark=0.5),
            roof=env.roof_tiles('Grim slate', '3b3d44', '2c2e34', tile=(0.32, 0.22), moss=0.2, curve=False),
            door=env.planks('Grim door', '3a2a1e', board=0.16, length=3.0, axis_v=True),
            glass=env.window_glass('9cf25c', 2.2, 'Plague window'),
            dark=env.flat('0b0908', name='Void'),
            iron=S.metal('3a3a3c', name='Black iron', rough=0.5, wear=0.3),
            trim=S.gold('8a7346', name='Tarnished brass', rough=0.4),
        )
    if style == 'grey':
        return dict(
            stone=env.masonry('Grey masonry', '8d8a82', '726f68', '4c4a45', moss=0.2),
            stone2=env.masonry('Grey rubble', '7e7a70', '66625a', '45423c', block=(0.45, 0.28), moss=0.25),
            plaster=env.plaster('Grey plaster', 'cfc6b2', '857a66'),
            timber=S.wood('3b2a1c', name='Dark oak beams', axis='Z', scale=0.4, dark=0.55),
            roof=env.roof_tiles('Slate roof', '4d5560', '3a414b', tile=(0.3, 0.2), moss=0.15, curve=False),
            door=env.planks('Oak door', '5a3c26', board=0.16, length=3.0, axis_v=True),
            glass=env.window_glass('ffb04a', 3.0),
            dark=env.flat('0b0908', name='Void'),
            iron=S.metal('3c3f42', name='Black iron', rough=0.45),
            trim=S.gold('c89a48', name='Antique brass'),
        )
    return dict(
        stone=env.masonry('Warm masonry', 'b09e80', '948468', '5e5244', moss=0.12),
        stone2=env.masonry('Warm rubble', 'a08e72', '83745c', '554a3e', block=(0.45, 0.28), moss=0.15),
        plaster=env.plaster('Warm plaster', 'dccba4', '8c7656'),
        timber=S.wood('4a3220', name='Oak beams', axis='Z', scale=0.4, dark=0.55),
        roof=env.roof_tiles('Terracotta', 'a5523a', '84402c', tile=(0.26, 0.2), moss=0.12),
        door=env.planks('Oak door', '6a4528', board=0.16, length=3.0, axis_v=True),
        glass=env.window_glass('ffb04a', 3.0),
        dark=env.flat('0b0908', name='Void'),
        iron=S.metal('3c3f42', name='Black iron', rough=0.45),
        trim=S.gold('c89a48', name='Antique brass'),
    )


# ------------------------------------------------------------------ primitives in a local frame


def box(name, size, loc, mat, bevel=0.03, out=None, uv=True):
    o = geo.box(name, size, loc, mat, C['Architecture'], bevel=bevel)
    env.finish(o, uv='box' if uv else None)
    if out is not None:
        out.append(o)
    return o


def beam(p0, p1, size, mat, out, name='Beam', bevel=0.015):
    """Square-section timber between two points."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    ln = d.length
    o = geo.box(name, (ln, size, size), (0, 0, 0), mat, C['Architecture'], bevel=bevel)
    env.finish(o)
    q = Vector((1, 0, 0)).rotation_difference(d.normalized())
    geo.transform(o, Matrix.Translation((p0 + p1) / 2) @ q.to_matrix().to_4x4())
    out.append(o)
    return o


def slab(center, u, v, su, sv, thick, mat, out, name='Slab', bevel=0.01):
    """Oriented board: u/v unit directions (UVs follow u, v in metres)."""
    o = geo.box(name, (su, sv, thick), (0, 0, 0), mat, C['Architecture'], bevel=bevel)
    env.finish(o)
    u, v = Vector(u).normalized(), Vector(v).normalized()
    w = u.cross(v).normalized()
    rot = Matrix((u, v, w)).transposed().to_4x4()
    geo.transform(o, Matrix.Translation(Vector(center)) @ rot)
    out.append(o)
    return o


def gable_roof(w, d, z, pitch, overhang, mat, out, thick=0.16, ridge_mat=None, hip=False):
    """Two roof planes, ridge along X."""
    run = d / 2 + overhang
    rise = (d / 2) * math.tan(math.radians(pitch))
    slope = math.hypot(run, run * math.tan(math.radians(pitch)))
    L = w + 2 * overhang
    a = math.radians(pitch)
    for s in (-1, 1):
        u = (1, 0, 0)
        v = (0, -s * math.cos(a), math.sin(a))
        # slab centre: halfway along the slope from the eave to the ridge
        eave = Vector((0, s * run, z + rise - run * math.tan(a)))
        ridge = Vector((0, 0, z + rise))
        c = (eave + ridge) / 2 + Vector((0, 0, thick / 2)) * math.cos(a)
        slab(c, u, v, L, (ridge - eave).length + 0.08, thick, mat, out, name='Roof plane')
    if ridge_mat is not None:
        o = geo.cylinder('Ridge', 0.12, L + 0.1, (0, 0, z + rise + thick * 0.8), ridge_mat, C['Architecture'], 10,
                         rotation=(0, 90, 0), bevel=0)
        env.finish(o, uv='box')
        out.append(o)
    return rise


def gable_wall(w, z, rise, y, mat, out, thick=0.25):
    o = geo.extrude('Gable', [(-w / 2, 0), (w / 2, 0), (0, rise)], thick, mat, C['Architecture'], plane='XZ', bevel=0.02)
    geo.place(o, (0, y, z))
    env.finish(o, local=True)
    out.append(o)
    return o


def window(center, normal, w, h, mats, out, lit=True, shutters=True, arch=False, frame=0.08, rng=None):
    """Window on a wall: frame, glazing (warm if lit) and optional shutters. normal: outward (XY)."""
    nrm = Vector(normal).normalized()
    u = Vector((-nrm.y, nrm.x, 0))
    c = Vector(center)
    glass = mats['glass'] if lit else mats['dark']
    slab(c + nrm * 0.02, u, (0, 0, 1), w, h, 0.06, glass, out, name='Glazing', bevel=0)
    t = frame
    for s in (-1, 1):
        beam(c + nrm * 0.07 + u * s * (w / 2 + t / 2) - Vector((0, 0, h / 2 + t)),
             c + nrm * 0.07 + u * s * (w / 2 + t / 2) + Vector((0, 0, h / 2 + t)), t, mats['timber'], out, 'Jamb')
    beam(c + nrm * 0.07 - u * (w / 2 + t) + Vector((0, 0, h / 2 + t / 2)),
         c + nrm * 0.07 + u * (w / 2 + t) + Vector((0, 0, h / 2 + t / 2)), t, mats['timber'], out, 'Lintel')
    beam(c + nrm * 0.1 - u * (w / 2 + t * 1.5) - Vector((0, 0, h / 2 + t / 2)),
         c + nrm * 0.1 + u * (w / 2 + t * 1.5) - Vector((0, 0, h / 2 + t / 2)), t * 1.2, mats['timber'], out, 'Sill')
    beam(c + nrm * 0.06 - Vector((0, 0, h / 2)), c + nrm * 0.06 + Vector((0, 0, h / 2)), 0.035, mats['timber'], out,
         'Mullion')
    if shutters:
        ang = (rng.uniform(0.3, 1.2) if rng else 0.7)
        for s in (-1, 1):
            hinge = c + nrm * 0.08 + u * s * (w / 2 + t)
            dirv = (u * s * math.cos(ang) + nrm * math.sin(ang)).normalized()
            slab(hinge + dirv * w / 4, dirv, (0, 0, 1), w / 2, h + 0.05, 0.04, mats['door'], out, 'Shutter')


def door(center, normal, w, h, mats, out, arch=True):
    nrm = Vector(normal).normalized()
    u = Vector((-nrm.y, nrm.x, 0))
    c = Vector(center)
    slab(c + nrm * 0.04, u, (0, 0, 1), w, h, 0.08, mats['door'], out, name='Door')
    for s in (-1, 1):
        box_c = c + nrm * 0.1 + u * s * (w / 2 + 0.12)
        slab(box_c, u, (0, 0, 1), 0.24, h + 0.1, 0.22, mats['stone'], out, name='Door jamb', bevel=0.03)
    slab(c + nrm * 0.1 + Vector((0, 0, h / 2 + 0.14)), u, (0, 0, 1), w + 0.6, 0.28, 0.24, mats['stone'], out,
         name='Door head', bevel=0.03)
    for k in (-1, 1):
        slab(c + nrm * 0.1 + Vector((0, 0, k * h * 0.25)), u, (0, 0, 1), w * 0.9, 0.05, 0.03, mats['iron'], out,
             name='Strap', bevel=0.005)


def timber_frame(face_c, u, nrm, width, z0, z1, mats, out, rng, posts=1.3, braces=True):
    """Half-timbering on a plaster face. face_c: point on the face at mid-width (z ignored)."""
    u = Vector(u).normalized()
    nrm = Vector(nrm).normalized()
    base = Vector((face_c[0], face_c[1], 0)) + nrm * 0.03
    s = 0.17
    n_p = max(2, int(round(width / posts)) + 1)
    xs = [-width / 2 + width * i / (n_p - 1) for i in range(n_p)]
    for x in xs:
        beam(base + u * x + Vector((0, 0, z0)), base + u * x + Vector((0, 0, z1)), s, mats['timber'], out, 'Post')
    for z in (z0 + s / 2, z1 - s / 2):
        beam(base + u * (-width / 2) + Vector((0, 0, z)), base + u * (width / 2) + Vector((0, 0, z)), s, mats['timber'], out,
             'Plate')
    mid = z0 + (z1 - z0) * 0.42
    beam(base + u * (-width / 2) + Vector((0, 0, mid)), base + u * (width / 2) + Vector((0, 0, mid)), s * 0.8,
         mats['timber'], out, 'Rail')
    if braces:
        for i in range(n_p - 1):
            if rng.random() < 0.6:
                a, b = xs[i], xs[i + 1]
                if rng.random() < 0.5:
                    a, b = b, a
                beam(base + u * a + Vector((0, 0, z0 + s)), base + u * b + Vector((0, 0, mid)), s * 0.75, mats['timber'],
                     out, 'Brace')


# ------------------------------------------------------------------ buildings


def house(loc=(0, 0, 0), rot=0.0, w=6.0, d=5.0, h1=2.8, h2=2.4, floors=2, pitch=48, style='warm', seed=0,
          lit=0.6, roof=None, chimney=True, jetty=0.35, stone_ground=True, sign=None, M=None):
    rng = random.Random(seed)
    M = M or mats(style)
    roof_mat = roof or M['roof']
    out = []
    z = 0.0
    box('Plinth', (w + 0.25, d + 0.25, 0.45), (0, 0, 0.1), M['stone2'], 0.04, out)
    z = 0.32
    box('Ground floor', (w, d, h1), (0, 0, z + h1 / 2), M['stone'] if stone_ground else M['plaster'], 0.04, out)
    # ground floor openings
    door((rng.uniform(-w * 0.2, w * 0.2), -d / 2, z + 1.05), (0, -1, 0), 1.0, 2.0, M, out)
    for sx in (-1, 1):
        if rng.random() < 0.85:
            window((sx * w * 0.34, -d / 2, z + 1.5), (0, -1, 0), 0.6, 0.8, M, out, lit=rng.random() < lit, rng=rng)
    window((w / 2, rng.uniform(-d * 0.2, d * 0.2), z + 1.5), (1, 0, 0), 0.55, 0.75, M, out, lit=rng.random() < lit,
           rng=rng)
    window((-w / 2, rng.uniform(-d * 0.2, d * 0.2), z + 1.5), (-1, 0, 0), 0.55, 0.75, M, out,
           lit=rng.random() < lit, rng=rng)
    z += h1
    W, D = w, d
    if floors >= 2:
        W, D = w + 2 * jetty, d + 2 * jetty
        # joist ends under the jetty
        for i in range(int(W / 0.5)):
            x = -W / 2 + 0.25 + i * 0.5
            beam((x, -D / 2 - 0.02, z + 0.08), (x, -d / 2 + 0.3, z + 0.08), 0.14, M['timber'], out, 'Joist')
        box('Upper floor', (W, D, h2), (0, 0, z + 0.16 + h2 / 2), M['plaster'], 0.02, out)
        zz0, zz1 = z + 0.16, z + 0.16 + h2
        timber_frame((0, -D / 2), (1, 0, 0), (0, -1, 0), W, zz0, zz1, M, out, rng)
        timber_frame((0, D / 2), (-1, 0, 0), (0, 1, 0), W, zz0, zz1, M, out, rng)
        timber_frame((W / 2, 0), (0, 1, 0), (1, 0, 0), D, zz0, zz1, M, out, rng, braces=False)
        timber_frame((-W / 2, 0), (0, -1, 0), (-1, 0, 0), D, zz0, zz1, M, out, rng, braces=False)
        for i in range(max(1, int(W / 2.2))):
            x = -W / 2 + W * (i + 0.5) / max(1, int(W / 2.2))
            window((x, -D / 2 - 0.02, zz0 + h2 * 0.55), (0, -1, 0), 0.55, 0.7, M, out, lit=rng.random() < lit,
                   shutters=rng.random() < 0.5, rng=rng)
        z = zz1
    rise = gable_roof(W, D, z, pitch, 0.45, roof_mat, out, ridge_mat=roof_mat)
    # gable ends (side walls): triangles on the X faces
    for s in (-1, 1):
        g = geo.extrude('Gable', [(-D / 2, 0), (D / 2, 0), (0, rise)], 0.22, M['plaster'], C['Architecture'], plane='YZ',
                        bevel=0.02)
        geo.place(g, (s * (W / 2 - 0.11), 0, z))
        env.finish(g)
        out.append(g)
        beam((s * (W / 2 + 0.02), -D / 2, z + 0.08), (s * (W / 2 + 0.02), 0, z + rise), 0.16, M['timber'], out, 'Barge')
        beam((s * (W / 2 + 0.02), D / 2, z + 0.08), (s * (W / 2 + 0.02), 0, z + rise), 0.16, M['timber'], out, 'Barge')
        beam((s * (W / 2 + 0.02), 0, z + 0.1), (s * (W / 2 + 0.02), 0, z + rise), 0.14, M['timber'], out, 'King post')
    if chimney:
        cx = rng.choice((-1, 1)) * W * 0.3
        box('Chimney', (0.7, 0.7, rise + 1.6), (cx, D * 0.18, z + (rise + 1.6) / 2 + 0.2), M['stone2'], 0.03, out)
        box('Chimney cap', (0.9, 0.9, 0.15), (cx, D * 0.18, z + rise + 1.85), M['stone'], 0.03, out)
    if sign is not None:
        beam((w * 0.15, -d / 2, z - 0.6), (w * 0.15, -d / 2 - 1.0, z - 0.6), 0.08, M['iron'], out, 'Sign bracket')
        slab((w * 0.15, -d / 2 - 0.7, z - 1.1), (0, 1, 0), (0, 0, 1), 0.7, 0.55, 0.05, sign, out, 'Sign')
    env.group_xform(out, loc, rot)
    return out


def tower(loc=(0, 0, 0), r=2.6, h=12.0, roof='cone', style='warm', seed=0, lit=0.5, batter=0.12, M=None,
          banner=None, ruined=0.0, roof_h=None, windows=3):
    rng = random.Random(seed)
    M = M or mats(style)
    out = []
    prof = [(r * (1 + batter), 0), (r * (1 + batter * 0.6), h * 0.12)] + [(r, h * (0.25 + 0.75 * i / 10))
                                                                            for i in range(11)]
    body = geo.lathe('Tower', prof, 40, M['stone'], C['Architecture'], close_bottom=False, close_top=not ruined)
    if ruined:
        for v in body.data.vertices:
            if v.co.z > h * 0.6:
                a = math.atan2(v.co.y, v.co.x)
                cut = h - ruined * h * (0.35 + 0.35 * (0.5 + 0.5 * math.sin(a * 3 + seed)) + 0.15 * math.sin(a * 11))
                v.co.z = min(v.co.z, cut)
        body.data.update()
    env.finish(body, uv='cyl')
    out.append(body)
    ring = geo.lathe('String course', [(r - 0.05, h * 0.25 - 0.12), (r + 0.12, h * 0.25 - 0.06), (r + 0.12, h * 0.25 + 0.06),
                                       (r - 0.05, h * 0.25 + 0.12)], 32, M['stone2'], C['Architecture'],
                     close_top=False, close_bottom=False)
    env.finish(ring, uv='cyl')
    out.append(ring)
    for i in range(windows):
        a = rng.uniform(-0.8, 0.8) - math.pi / 2
        z = h * (0.35 + 0.5 * i / max(1, windows))
        if ruined and z > h * (1 - ruined * 0.8):
            continue
        c = Vector((math.cos(a) * r, math.sin(a) * r, z))
        nrm = Vector((math.cos(a), math.sin(a), 0))
        slab(c + nrm * 0.02, (-nrm.y, nrm.x, 0), (0, 0, 1), 0.28 if i == 0 else 0.5, 0.9, 0.12,
             M['glass'] if rng.random() < lit else M['dark'], out, 'Slit', bevel=0)
    top = h
    if not ruined:
        if roof in ('crenel', 'cone'):
            corb = geo.lathe('Machicolation', [(r - 0.1, h - 0.1), (r + 0.4, h + 0.3), (r + 0.4, h + 0.9), (r - 0.1, h + 0.9)],
                             32, M['stone2'], C['Architecture'], close_top=True, close_bottom=False)
            env.finish(corb, uv='cyl')
            out.append(corb)
            top = h + 0.9
            n_m = int(2 * math.pi * (r + 0.3) / 1.0)
            for k in range(n_m):
                if k % 2:
                    continue
                a = k * math.tau / n_m
                c = Vector((math.cos(a) * (r + 0.22), math.sin(a) * (r + 0.22), top + 0.45))
                o = geo.box('Merlon', (0.5, 0.36, 0.9), (0, 0, 0), M['stone'], C['Architecture'], bevel=0.03)
                env.finish(o)
                geo.transform(o, Matrix.Translation(c) @ Matrix.Rotation(a + math.pi / 2, 4, 'Z'))
                out.append(o)
        if roof == 'cone':
            rh = roof_h or r * 2.6
            cone = geo.lathe('Spire roof', [(0, top + rh), (r * 0.25, top + rh * 0.7), (r * 0.75, top + rh * 0.2),
                                            (r + 0.55, top - 0.15), (r - 0.1, top - 0.2)], 32, M['roof'],
                             C['Architecture'], close_bottom=True)
            env.finish(cone, uv='cyl')
            out.append(cone)
            fin = geo.cylinder('Finial', 0.05, 1.4, (0, 0, top + rh + 0.6), M['iron'], C['Architecture'], 8, bevel=0)
            env.finish(fin)
            out.append(fin)
            if banner is not None:
                p = geo.grid_sheet('Pennant', 1, 1, 8, 2, lambda u, v: (0.05 + u * 1.3, 0.04 * math.sin(u * 6),
                                                                         top + rh + 1.2 - v * 0.45 * (1 - 0.7 * u)),
                                   banner, C['Architecture'])
                env.finish(p)
                out.append(p)
    env.group_xform(out, loc, 0.0)
    return out


def wall(p0, p1, h=5.0, t=1.8, style='warm', crenels=True, M=None, base_z=0.0, walk=True):
    M = M or mats(style)
    out = []
    p0, p1 = Vector(p0), Vector(p1)
    d = (p1 - p0)
    ln = d.length
    ang = math.atan2(d.y, d.x)
    o = geo.box('Curtain wall', (ln, t, h), (0, 0, h / 2), M['stone'], C['Architecture'], bevel=0.04)
    env.finish(o)
    parts = [o]
    if crenels:
        n_m = int(ln / 1.1)
        for k in range(n_m):
            if k % 2:
                continue
            x = -ln / 2 + (k + 0.5) * ln / n_m
            for sy in ((-1,) if not walk else (-1, 1)):
                m = geo.box('Merlon', (ln / n_m, 0.45, 0.95), (x, sy * (t / 2 - 0.22), h + 0.47), M['stone'],
                            C['Architecture'], bevel=0.03)
                env.finish(m)
                parts.append(m)
    for o in parts:
        geo.transform(o, Matrix.Translation(((p0 + p1) / 2).to_3d() + Vector((0, 0, base_z))) @
                      Matrix.Rotation(ang, 4, 'Z'))
    out += parts
    return out


def arch_opening(center, normal, w, h, thick, mat, out, segments=10, name='Arch'):
    """Semicircular stone arch ring (voussoirs) standing on two piers."""
    nrm = Vector(normal).normalized()
    u = Vector((-nrm.y, nrm.x, 0))
    c = Vector(center)
    r = w / 2
    spring = h - r
    for s in (-1, 1):
        slab(c + u * s * (r + 0.3) + Vector((0, 0, spring / 2)), u, (0, 0, 1), 0.6, spring, thick, mat, out, 'Pier',
             bevel=0.03)
    for i in range(segments):
        a0 = math.pi * i / segments
        a1 = math.pi * (i + 1) / segments
        am = (a0 + a1) / 2
        rr = r + 0.3
        p = c + u * (-math.cos(am) * rr) + Vector((0, 0, spring + math.sin(am) * rr))
        tang = (u * math.sin(am) + Vector((0, 0, math.cos(am)))).normalized()
        radial = (u * -math.cos(am) + Vector((0, 0, math.sin(am)))).normalized()
        o = geo.box(name + ' voussoir', (0.6, rr * (a1 - a0) * 0.97, thick), (0, 0, 0), mat, C['Architecture'], bevel=0.025)
        env.finish(o)
        rot = Matrix((radial, tang, nrm)).transposed().to_4x4()
        geo.transform(o, Matrix.Translation(p) @ rot)
        out.append(o)
    return out


def column(loc, h=5.0, r=0.32, mat=None, capital=True, out=None):
    out = out if out is not None else []
    o = geo.lathe('Column', [(r * 1.35, 0), (r * 1.35, 0.25), (r * 1.1, 0.35), (r, 0.5), (r * 0.92, h - 0.5),
                             (r * 1.05, h - 0.4), (r * 1.4, h - 0.2), (r * 1.4, h)], 20, mat, C['Architecture'])
    env.finish(o, uv='cyl')
    xform = Matrix.Translation(Vector(loc))
    geo.transform(o, xform)
    out.append(o)
    return out


def gatehouse(loc=(0, 0, 0), rot=0.0, w=9.0, d=6.0, h=9.0, gate_w=3.4, gate_h=4.6, style='warm', M=None,
              banner=None, towers=True, seed=0, lit=0.6):
    """Fortified gate: arched passage with doors and raised portcullis, crenellated parapet, flanking towers."""
    M = M or mats(style)
    out = []
    side = (w - gate_w) / 2
    for s in (-1, 1):
        box('Gate pier', (side, d, h), (s * (gate_w / 2 + side / 2), 0, h / 2), M['stone'], 0.04, out)
    box('Gate lintel block', (gate_w + 0.02, d, h - gate_h - gate_w / 2 + 0.3), (0, 0, gate_h + gate_w / 2 + (h - gate_h - gate_w / 2) / 2 - 0.15),
        M['stone'], 0.04, out)
    # arch fill (spandrels) as a thin wall with the voussoir ring in front
    arch_opening((0, -d / 2 - 0.05, 0), (0, -1, 0), gate_w, gate_h + gate_w / 2, 0.5, M['stone2'], out)
    # passage darkness and doors
    box('Passage', (gate_w, d - 0.2, gate_h + gate_w / 2), (0, 0.1, (gate_h + gate_w / 2) / 2), M['dark'], 0, out, uv=False)
    for s in (-1, 1):
        slab((s * (gate_w / 2 - 0.2), -d / 2 + 0.9, gate_h / 2 + 0.2), (math.cos(math.radians(70)) * -s, math.sin(math.radians(70)), 0),
             (0, 0, 1), gate_w / 2, gate_h, 0.14, M['door'], out, 'Gate door')
    # portcullis: iron grid hanging in the top of the arch
    for i in range(7):
        x = -gate_w / 2 + 0.25 + i * (gate_w - 0.5) / 6
        beam((x, -d / 2 + 0.4, gate_h + gate_w / 2 - 0.1), (x, -d / 2 + 0.4, gate_h + gate_w / 2 - 1.6), 0.07, M['iron'],
             out, 'Portcullis bar')
    for k in range(3):
        z = gate_h + gate_w / 2 - 0.4 - k * 0.5
        beam((-gate_w / 2 + 0.2, -d / 2 + 0.4, z), (gate_w / 2 - 0.2, -d / 2 + 0.4, z), 0.06, M['iron'], out, 'Portcullis rail')
    # parapet
    n_m = int(w / 1.0)
    for k in range(n_m):
        if k % 2:
            continue
        x = -w / 2 + (k + 0.5) * w / n_m
        for sy in (-1, 1):
            box('Merlon', (w / n_m, 0.45, 0.95), (x, sy * (d / 2 - 0.22), h + 0.47), M['stone'], 0.03, out)
    box('Corbel course', (w + 0.3, d + 0.3, 0.3), (0, 0, h - 0.15), M['stone2'], 0.03, out)
    for s in (-1, 1):
        window((s * (gate_w / 2 + side / 2), -d / 2, h * 0.72), (0, -1, 0), 0.4, 0.9, M, out, lit=True, shutters=False)
    env.group_xform(out, loc, rot)
    if towers:
        for s in (-1, 1):
            c = Vector(loc) + Matrix.Rotation(rot, 3, 'Z') @ Vector((s * (w / 2 + 1.6), 0.4, 0))
            out += tower(tuple(c), 2.8, h + 4.5, 'cone', style=style, seed=seed + (s > 0), banner=banner, M=M, lit=lit)
    return out


def arcade(p0, p1, bays=5, h=4.2, depth=3.0, style='warm', M=None, roof=True, back_wall=True, lit=0.5, seed=0):
    """Cloister walk: columns carrying round arches along p0->p1 (front faces the left-hand normal),
    a lean-to tiled roof and a back wall with doors/windows."""
    M = M or mats(style)
    rng = random.Random(seed)
    out = []
    p0, p1 = Vector(p0).to_3d(), Vector(p1).to_3d()
    d = p1 - p0
    L = d.length
    u = d.normalized()
    nrm = Vector((-u.y, u.x, 0))  # front (courtyard side)
    span = L / bays
    r = span / 2 - 0.35
    spring = h - r - 0.6
    for i in range(bays + 1):
        c = p0 + u * (i * span)
        column(c, spring, 0.28, M['stone'], out=out)
        slab(c + Vector((0, 0, spring + 0.12)), u, (0, 0, 1), 0.75, 0.24, 0.75, M['stone2'], out, 'Impost', bevel=0.03)
    for i in range(bays):
        c = p0 + u * ((i + 0.5) * span)
        segs = 9
        for k in range(segs):
            a0, a1 = math.pi * k / segs, math.pi * (k + 1) / segs
            am = (a0 + a1) / 2
            rr = r + 0.18
            pnt = c + u * (-math.cos(am) * rr) + Vector((0, 0, spring + 0.24 + math.sin(am) * rr))
            tang = (u * math.sin(am) + Vector((0, 0, math.cos(am)))).normalized()
            rad = (u * -math.cos(am) + Vector((0, 0, math.sin(am)))).normalized()
            o = geo.box('Arch stone', (0.36, rr * (a1 - a0) * 0.96, 0.62), (0, 0, 0), M['stone'], C['Architecture'],
                        bevel=0.02)
            env.finish(o)
            geo.transform(o, Matrix.Translation(pnt) @ Matrix.Rotation(0, 4, 'Z') @ Matrix((rad, tang, nrm)).transposed().to_4x4())
            out.append(o)
    # wall above the arches
    top = h
    slab(p0 + u * (L / 2) + Vector((0, 0, top - 0.35)), u, (0, 0, 1), L + 0.6, 0.9, 0.6, M['stone'], out, 'Arcade frieze',
         bevel=0.03)
    if back_wall:
        bw = p0 + u * (L / 2) - nrm * depth + Vector((0, 0, h / 2 + 0.6))
        slab(bw, u, (0, 0, 1), L + 0.6, h + 1.2, 0.5, M['stone'], out, 'Cloister back wall', bevel=0.03)
        for i in range(bays):
            c = p0 + u * ((i + 0.5) * span) - nrm * (depth - 0.26)
            if i % 2 == 0:
                door(c + Vector((0, 0, 1.05)), nrm, 1.0, 2.0, M, out)
            else:
                window(c + Vector((0, 0, 1.9)), nrm, 0.6, 1.0, M, out, lit=rng.random() < lit, shutters=False)
    if roof:
        a = math.radians(22)
        run = depth + 0.8
        rise = run * math.tan(a)
        eave = p0 + u * (L / 2) + nrm * 0.6 + Vector((0, 0, h + 0.15))
        ridge = p0 + u * (L / 2) - nrm * (depth + 0.2) + Vector((0, 0, h + 0.15 + rise))
        v = (ridge - eave).normalized()
        slab((eave + ridge) / 2, u, v, L + 1.0, (ridge - eave).length, 0.16, M['roof'], out, 'Arcade roof')
    # vault ceiling shadow plane
    slab(p0 + u * (L / 2) - nrm * (depth / 2) + Vector((0, 0, h - 0.1)), u, (nrm * -1), L, depth, 0.2, M['stone2'], out,
         'Walk ceiling', bevel=0)
    # floor
    slab(p0 + u * (L / 2) - nrm * (depth / 2 - 0.3) + Vector((0, 0, 0.05)), u, nrm * -1, L + 0.6, depth + 0.6, 0.1,
         M['stone2'], out, 'Walk floor', bevel=0.02)
    return out


# ------------------------------------------------------------------ interiors


def floor_mats(kind='flag', style='warm'):
    if kind == 'flag':
        return env.masonry(f'Flagstones {style}', 'a09482' if style == 'warm' else '7f7a72',
                           '867a6a' if style == 'warm' else '68645e', '4a4038', block=(0.9, 0.62), mortar_size=0.012,
                           dirt_bottom=0.0, moss=0.0)
    if kind == 'boards':
        return env.planks('Floorboards', '6a4a30', board=0.24, length=3.2)
    return env.masonry('Vault floor', '6e665c', '5a544c', '2e2a26', block=(0.7, 0.7), mortar_size=0.015,
                       dirt_bottom=0.0)


def wall_with_openings(p0, p1, h, thick, mat, out, openings=(), name='Wall', arch_mat=None, glass=None, base_z=0.0,
                       sill=None):
    """Straight wall from p0 to p1 (XY) with rectangular/round-headed openings.
    openings: list of (u_centre_metres, bottom, width, height, round_top). Solid parts are boxes."""
    p0, p1 = Vector(p0).to_3d(), Vector(p1).to_3d()
    d = p1 - p0
    L = d.length
    u = d.normalized()
    nrm = Vector((-u.y, u.x, 0))
    ops = sorted(openings, key=lambda o: o[0])
    cuts = [0.0]
    for (uc, b, w, hh, rt) in ops:
        cuts += [uc - w / 2, uc + w / 2]
    cuts.append(L)
    # full-height piers between openings
    for i in range(0, len(cuts), 2):
        a, bb = cuts[i], cuts[i + 1]
        if bb - a > 0.01:
            slab(p0 + u * ((a + bb) / 2) + Vector((0, 0, base_z + h / 2)), u, (0, 0, 1), bb - a, h, thick, mat, out,
                 name, bevel=0.02)
    for (uc, b, w, hh, rt) in ops:
        c = p0 + u * uc
        top_open = b + hh + (w / 2 if rt else 0)
        if b > 0.01:
            slab(c + Vector((0, 0, base_z + b / 2)), u, (0, 0, 1), w, b, thick, mat, out, name + ' below', bevel=0.02)
        if h - top_open > 0.01:
            slab(c + Vector((0, 0, base_z + (top_open + h) / 2)), u, (0, 0, 1), w, h - top_open, thick, mat, out,
                 name + ' above', bevel=0.02)
        if rt:
            # spandrel: wall infill above a smooth semicircular head (one extruded concave outline)
            r = w / 2
            spring = b + hh
            outline = [(-r, spring), (-r, top_open), (r, top_open), (r, spring)]
            outline += [(r * math.cos(t * math.pi / 24), spring + r * math.sin(t * math.pi / 24)) for t in range(1, 24)]
            sp = geo.extrude(name + ' spandrel', outline, thick, mat, C['Architecture'], plane='XZ', bevel=0)
            env.finish(sp)
            zax = Vector((0, 0, 1))
            yax = zax.cross(u)
            geo.transform(sp, Matrix.Translation(c + Vector((0, 0, base_z))) @ Matrix((u, yax, zax)).transposed().to_4x4())
            out.append(sp)
            if arch_mat is not None:
                arch_opening(c + nrm * (thick / 2 + 0.05) + Vector((0, 0, base_z + b)), nrm, w, hh + w / 2, 0.25,
                             arch_mat, out)
        if glass is not None:
            slab(c + Vector((0, 0, base_z + b + (hh + (w / 2 if rt else 0)) / 2)), u, (0, 0, 1), w,
                 hh + (w / 2 if rt else 0), 0.04, glass, out, 'Window glass', bevel=0)
        if sill is not None:
            slab(c + nrm * (thick / 2 + 0.1) + Vector((0, 0, base_z + b - 0.06)), u, (0, 0, 1), w + 0.3, 0.12, 0.3,
                 sill, out, 'Sill', bevel=0.02)
    return out


def room(w=16.0, d=14.0, h=7.0, style='warm', floor='flag', M=None, back_openings=(), left_openings=(),
         right_openings=(), beams=True, ceiling=True, wall_mat=None, glass=None, front=-2.0, arch_mat=None):
    """Open-fronted hall: floor, three walls (back at +Y), timber roof beams. Camera looks in from -Y."""
    M = M or mats(style)
    out = []
    wall = wall_mat or M['stone']
    fm = floor_mats(floor, style)
    slab((0, (d + front) / 2, -0.1), (1, 0, 0), (0, 1, 0), w + 2, d - front + 2, 0.2, fm, out, 'Floor', bevel=0)
    t = 0.7
    # openings are given in world terms: back (x, ...), left/right (y, ...); walls run so their normal faces inside
    back = [(w / 2 - o[0],) + tuple(o[1:]) for o in back_openings]
    left = [(d - o[0],) + tuple(o[1:]) for o in left_openings]
    right = [(o[0] - front,) + tuple(o[1:]) for o in right_openings]
    back = [(o[0] + t,) + tuple(o[1:]) for o in back]
    wall_with_openings((w / 2 + t, d + t / 2), (-w / 2 - t, d + t / 2), h, t, wall, out, back, 'Back wall', arch_mat,
                       glass)
    wall_with_openings((-w / 2 - t / 2, d), (-w / 2 - t / 2, front), h, t, wall, out, left, 'Left wall', arch_mat, glass)
    wall_with_openings((w / 2 + t / 2, front), (w / 2 + t / 2, d), h, t, wall, out, right, 'Right wall', arch_mat, glass)
    if beams:
        n = int((d - front) / 2.6)
        for i in range(n + 1):
            y = front + 0.8 + i * (d - front - 1.6) / max(1, n)
            beam((-w / 2, y, h - 0.25), (w / 2, y, h - 0.25), 0.36, M['timber'], out, 'Tie beam')
            for s in (-1, 1):
                beam((s * (w / 2 - 0.2), y, h - 1.4), (s * (w / 2 - 1.2), y, h - 0.4), 0.22, M['timber'], out, 'Brace')
    if ceiling:
        slab((0, (d + front) / 2, h + 0.15), (1, 0, 0), (0, 1, 0), w + 2, d - front + 2, 0.3, M['door'], out,
             'Ceiling', bevel=0)
    return out


def vault(w=12.0, d=16.0, h=6.0, M=None, style='grey', front=-2.0, ribs=5, floor='vault', wall_mat=None):
    """Barrel-vaulted stone chamber (axis along Y) with transverse ribs."""
    M = M or mats(style)
    out = []
    wall = wall_mat or M['stone']
    fm = floor_mats(floor, style)
    slab((0, (d + front) / 2, -0.1), (1, 0, 0), (0, 1, 0), w + 2, d - front + 2, 0.2, fm, out, 'Floor', bevel=0)
    spring = h - w / 2
    r = w / 2
    prof = []
    for i in range(17):
        a = math.pi * i / 16
        prof.append((-math.cos(a) * r, spring + math.sin(a) * r))
    # vault shell as a lofted strip along Y
    secs = []
    for y in (front, d):
        secs.append([Vector((x, y, z)) for x, z in prof])
    shell = geo.loft('Vault shell', secs, wall, C['Architecture'], closed=False, cap=False, smooth=True)
    mod = shell.modifiers.new('Thickness', 'SOLIDIFY')
    mod.thickness = 0.6
    mod.offset = 1
    env.finish(shell)
    out.append(shell)
    for s in (-1, 1):
        slab((s * (w / 2 + 0.3), (d + front) / 2, spring / 2), (0, 1, 0), (0, 0, 1), d - front, spring, 0.6, wall, out,
             'Side wall', bevel=0.02)
    slab((0, d + 0.3, h / 2 + 0.5), (1, 0, 0), (0, 0, 1), w + 1.2, h + 1.0, 0.6, wall, out, 'End wall', bevel=0.02)
    for k in range(ribs):
        y = front + 1.2 + k * (d - front - 2.0) / max(1, ribs - 1)
        rib = [Vector((x * 0.97, y, z - 0.02)) for x, z in prof]
        o = geo.tube('Vault rib', rib, 0.22, M['stone2'], C['Architecture'], sides=6)
        env.finish(o)
        out.append(o)
        for s in (-1, 1):
            slab((s * (w / 2 - 0.15), y, spring / 2), (0, 1, 0), (0, 0, 1), 0.55, spring, 0.4, M['stone2'], out,
                 'Pilaster', bevel=0.03)
    return out


def colosseum(center=(0, 0, 0), r_in=20.0, r_out=31.0, height=14.0, bays=40, M=None, style='warm', seat_mat=None,
              podium_h=3.6, gates=(270.0,), arc=None):
    """Arena bowl seen from inside: podium wall with gates, stepped cavea and a top arcade ring.
    arc=(a0, a1) degrees limits the built sector (the camera side can be omitted)."""
    M = M or mats(style)
    out = []
    cx, cy, cz = center
    seat_mat = seat_mat or env.masonry('Cavea stone', 'b4a68c', '9a8e78', '6a6050', block=(1.2, 0.5), mortar_size=0.01,
                                       dirt_bottom=0.0)
    a0, a1 = (0.0, 360.0) if arc is None else arc
    ang0, ang1 = math.radians(a0), math.radians(a1)
    span = ang1 - ang0
    segs = max(8, int(abs(span) / math.tau * 96))
    # podium wall
    pod = geo.lathe('Podium', [(r_in, 0), (r_in, podium_h), (r_in + 1.2, podium_h), (r_in + 1.2, 0)], segs,
                    M['stone'], C['Architecture'], close_top=False, close_bottom=False, angle=span)
    geo.transform(pod, Matrix.Translation((cx, cy, cz)) @ Matrix.Rotation(ang0, 4, 'Z'))
    env.finish(pod, uv='cyl', local=False)
    out.append(pod)
    # stepped cavea
    steps = 16
    prof = [(r_in + 1.2, podium_h)]
    for i in range(steps):
        r = r_in + 1.2 + (r_out - r_in - 3.0) * (i + 1) / steps
        z = podium_h + (height - podium_h) * i / steps
        z2 = podium_h + (height - podium_h) * (i + 1) / steps
        prof += [(prof[-1][0], z2), (r, z2)]
    prof += [(r_out, height), (r_out, 0)]
    cav = geo.lathe('Cavea', prof, segs, seat_mat, C['Architecture'], close_top=False, close_bottom=False, angle=span)
    geo.transform(cav, Matrix.Translation((cx, cy, cz)) @ Matrix.Rotation(ang0, 4, 'Z'))
    env.finish(cav, uv='cyl', local=False)
    out.append(cav)
    # top arcade ring: piers + arches + attic
    n = int(bays * abs(span) / math.tau)
    rr = r_out - 1.0
    for i in range(n + 1):
        a = ang0 + span * i / max(1, n)
        c = Vector((cx + math.cos(a) * rr, cy + math.sin(a) * rr, cz + height))
        nrm = Vector((-math.cos(a), -math.sin(a), 0))
        slab(c + Vector((0, 0, 2.2)), Vector((-nrm.y, nrm.x, 0)), (0, 0, 1), 0.9, 4.4, 1.6, M['stone'], out, 'Arcade pier',
             bevel=0.03)
    for i in range(n):
        a = ang0 + span * (i + 0.5) / max(1, n)
        c = Vector((cx + math.cos(a) * rr, cy + math.sin(a) * rr, cz + height))
        nrm = Vector((-math.cos(a), -math.sin(a), 0))
        chord = 2 * rr * math.sin(abs(span) / max(1, n) / 2)
        wdt = chord - 0.9
        segs_a = 7
        rad_ = wdt / 2
        u = Vector((-nrm.y, nrm.x, 0))
        for k in range(segs_a):
            b0, b1 = math.pi * k / segs_a, math.pi * (k + 1) / segs_a
            bm = (b0 + b1) / 2
            p = c + u * (-math.cos(bm) * (rad_ + 0.2)) + Vector((0, 0, 3.0 + math.sin(bm) * (rad_ + 0.2)))
            tang = (u * math.sin(bm) + Vector((0, 0, math.cos(bm)))).normalized()
            radial = (u * -math.cos(bm) + Vector((0, 0, math.sin(bm)))).normalized()
            o = geo.box('Arcade arch', (0.4, (rad_ + 0.2) * (b1 - b0) * 0.97, 1.5), (0, 0, 0), M['stone'],
                        C['Architecture'], bevel=0.02)
            env.finish(o)
            geo.transform(o, Matrix.Translation(p) @ Matrix((radial, tang, nrm)).transposed().to_4x4())
            out.append(o)
    attic = geo.lathe('Attic', [(rr - 0.8, height + 4.4), (rr + 0.8, height + 4.4), (rr + 0.8, height + 6.0),
                                (rr - 0.8, height + 6.0)], segs, M['stone'], C['Architecture'], angle=span)
    geo.transform(attic, Matrix.Translation((cx, cy, cz)) @ Matrix.Rotation(ang0, 4, 'Z'))
    env.finish(attic, uv='cyl', local=False)
    out.append(attic)
    # gates in the podium
    for g in gates:
        a = math.radians(g)
        c = Vector((cx + math.cos(a) * (r_in - 0.05), cy + math.sin(a) * (r_in - 0.05), cz))
        nrm = Vector((-math.cos(a), -math.sin(a), 0))
        u = Vector((-nrm.y, nrm.x, 0))
        slab(c + Vector((0, 0, 1.5)), u, (0, 0, 1), 2.6, 3.0, 0.1, M['dark'], out, 'Gate dark', bevel=0)
        arch_opening(c + nrm * 0.15, nrm, 2.6, 3.2, 0.45, M['stone2'], out)
    return out


def spire(loc=(0, 0, 0), r=5.0, h=46.0, M=None, style='grey', seed=0, ruin=0.22, lit=0.5, buttresses=6):
    """Tall ruined spire: battered base, tapering shaft with string courses, buttresses, broken crown."""
    M = M or mats(style)
    rng = random.Random(seed)
    out = []
    prof = [(r * 1.35, 0), (r * 1.2, h * 0.08)]
    for i in range(1, 15):
        t = i / 14
        prof.append((r * (1.1 - 0.42 * t), h * (0.08 + 0.92 * t)))
    body = geo.lathe('Spire shaft', prof, 40, M['stone'], C['Architecture'], close_bottom=False, close_top=False)
    for v in body.data.vertices:
        if v.co.z > h * (1 - ruin):
            a = math.atan2(v.co.y, v.co.x)
            cut = h * (1 - ruin * (0.25 + 0.75 * (0.5 + 0.5 * math.sin(a * 2 + seed) * math.cos(a * 5))))
            v.co.z = min(v.co.z, cut)
    body.data.update()
    mod = body.modifiers.new('Wall thickness', 'SOLIDIFY')
    mod.thickness = 0.9
    env.finish(body, uv='cyl')
    out.append(body)
    for t in (0.08, 0.3, 0.52, 0.7):
        z = h * t
        rr = r * (1.1 - 0.42 * (t - 0.08) / 0.92) if t > 0.08 else r * 1.2
        ring = geo.lathe('Course', [(rr - 0.1, z - 0.25), (rr + 0.35, z - 0.12), (rr + 0.35, z + 0.12), (rr - 0.1, z + 0.25)],
                         40, M['stone2'], C['Architecture'], close_top=False, close_bottom=False)
        env.finish(ring, uv='cyl')
        out.append(ring)
    for k in range(buttresses):
        a = k * math.tau / buttresses + 0.3
        u = Vector((math.cos(a), math.sin(a), 0))
        pts = [u * (r * 1.6) + Vector((0, 0, 0)), u * (r * 1.25) + Vector((0, 0, h * 0.2)),
               u * (r * 1.0) + Vector((0, 0, h * 0.42))]
        o = geo.tube('Buttress', pts, [1.0, 0.7, 0.4], M['stone2'], C['Architecture'], sides=4)
        env.finish(o)
        out.append(o)
    for i in range(14):
        t = rng.uniform(0.12, 1 - ruin - 0.05)
        z = h * t
        a = rng.uniform(-math.pi * 0.9, -math.pi * 0.1)
        rr = r * (1.1 - 0.42 * t) + 0.05
        c = Vector((math.cos(a) * rr, math.sin(a) * rr, z))
        nrm = Vector((math.cos(a), math.sin(a), 0))
        slab(c, (-nrm.y, nrm.x, 0), (0, 0, 1), 0.7, 1.5, 0.3, M['glass'] if rng.random() < lit else M['dark'], out,
             'Spire window', bevel=0)
    door((0, -r * 1.3, 1.4), (0, -1, 0), 1.6, 2.8, M, out)
    env.group_xform(out, loc, 0.0)
    return out
