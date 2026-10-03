"""Heads, faces, skulls, hair, beards, hoods, helmets and crowns.

All builders take the head centre `C` (world Vector) and head radius `R` and
return a list of mesh objects (unbound). The caller binds them to the head bone.
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

from . import geo


def _v(*a):
    return Vector(a)


def _delete_verts(obj, keep_fn):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    kill = [v for v in bm.verts if not keep_fn(v.co)]
    bmesh.ops.delete(bm, geom=kill, context='VERTS')
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    geo.store_rest(obj)
    return obj


def _solidify(obj, t, offset=-1):
    mod = obj.modifiers.new('Thickness', 'SOLIDIFY')
    mod.thickness = t
    mod.offset = offset
    mod.use_even_offset = False
    mod.use_rim = True
    return obj


def _subsurf(obj, levels=1):
    mod = obj.modifiers.new('Smooth', 'SUBSURF')
    mod.levels = levels
    mod.render_levels = levels
    return obj


# ------------------------------------------------------------------ faces
def _surface_x(obj, origin, direction=(-1, 0, 0)):
    """World point where a ray from `origin` hits obj (mesh at identity)."""
    from mathutils.bvhtree import BVHTree
    me = obj.data
    tree = BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [tuple(p.vertices) for p in me.polygons])
    hit = tree.ray_cast(Vector(origin), Vector(direction).normalized())
    return hit[0]


def _head_base(R, level=5):
    return geo.quadsphere('Head', 1.0, (0, 0, 0), None, None, level=level, scale=(R * 1.0, R * .84, R * 1.06))


def human_head(C, R, mats, coll, jaw=1.0, nose=1.0, brow=1.0, ears=True, gaunt=0.0, eye_glow=None,
               age=0.0, nose_kind='straight', female=False):
    """Stylised heroic head sculpted from a dense cube-sphere. mats: skin, eye, hair (brows)."""
    out = []
    h = _head_base(R)
    h.data.materials.append(mats['skin'])
    for c in list(h.users_collection):
        c.objects.unlink(h)
    coll.objects.link(h)
    S = geo.sculpt
    j = jaw * (0.6 if female else 1.0)
    # skull and temples
    S(h, _v(-R * .55, 0, R * .35), R * .7, _v(-R * .06, 0, R * .04))
    for sy in (-1, 1):
        S(h, _v(R * .5, sy * R * .78, R * .32), R * .38, _v(0, -sy * R * .05, 0))
        # square jaw and cheeks
        S(h, _v(R * .35, sy * R * .62, -R * .62), R * .42, _v(R * .02, sy * R * .05 * j, -R * .02))
        S(h, _v(R * .62, sy * R * .5, -R * .05), R * .3, R * (.04 - gaunt * .1), mode='inflate')
        S(h, _v(R * .55, sy * R * .5, -R * .42), R * .3, R * (-.01 - gaunt * .12), mode='inflate')
        # eye sockets and brow overhang
        S(h, _v(R * .92, sy * R * .34, R * .08), R * .2, _v(-R * .08, 0, 0))
        S(h, _v(R * .95, sy * R * .3, R * .27), R * .2, _v(R * .05 * brow, 0, -R * .01))
    # brow ridge (central)
    S(h, _v(R * .95, 0, R * .3), R * .36, _v(R * .05 * brow, 0, 0))
    # chin and jaw front
    S(h, _v(R * .8, 0, -R * .82), R * .38, _v(R * .1 * j, 0, -R * .08 * j))
    S(h, _v(R * .88, 0, -R * .9), R * .18, _v(R * .05 * j, 0, -R * .02))
    # nose: bridge, tip and wings
    nl = 1.15 if nose_kind == 'long' else 0.85 if nose_kind == 'small' else 1.0
    S(h, _v(R * .97, 0, R * .1), R * .12, _v(R * .08 * nose, 0, 0))
    S(h, _v(R * .99, 0, -R * .08), R * .14, _v(R * .16 * nose * nl, 0, -R * .02))
    S(h, _v(R * 1.0, 0, -R * .2), R * .14, _v(R * .14 * nose * nl, 0, -R * .03))
    for sy in (-1, 1):
        S(h, _v(R * .97, sy * R * .09, -R * .25), R * .09, _v(R * .05 * nose, sy * R * .02, 0))
    # mouth: groove, lips
    S(h, _v(R * 1.0, 0, -R * .46), R * .16, _v(-R * .035, 0, 0))
    S(h, _v(R * 1.0, 0, -R * .4), R * .1, _v(R * .025, 0, 0))
    S(h, _v(R * .98, 0, -R * .53), R * .09, _v(R * .02, 0, 0))
    if gaunt:
        for sy in (-1, 1):
            S(h, _v(R * .7, sy * R * .45, -R * .3), R * .3, _v(-R * .06 * gaunt, -sy * R * .04 * gaunt, 0))
    geo.subdivide(h, 1)
    if age:
        geo.displace_noise(h, R * .006 * age, 40)
    geo.place(h, C)
    out.append(h)
    for sy in (-1, 1):
        hit = _surface_x(h, C + _v(R * 2, sy * R * .33, R * .07))
        ec = (hit if hit is not None else C + _v(R * .85, sy * R * .33, R * .07)) - _v(R * .06, 0, 0)
        eye = geo.sphere('Eye', R * .11, ec, mats.get('eye') if eye_glow is None else eye_glow, coll, 14, 10,
                         scale=(0.7, 1, 0.9))
        out.append(eye)
        if mats.get('hair') is not None:
            pts = []
            for k, (yy, zz) in enumerate(((.1, .28), (.3, .31), (.5, .25))):
                hp = _surface_x(h, C + _v(R * 2, sy * R * yy, R * zz))
                pts.append((hp if hp is not None else C + _v(R * .95, sy * R * yy, R * zz)) + _v(R * .01, 0, 0))
            out.append(geo.tube('Brow', pts, [R * .05, R * .065, R * .035], mats['hair'], coll, sides=8, flatten=.55))
        if ears:
            ear = geo.sphere('Ear', 1, C + _v(-R * .02, sy * R * .83, -R * .02), mats['skin'], coll, 12, 8,
                             scale=(R * .17, R * .07, R * .26))
            out.append(ear)
    h['face_mesh'] = True
    return out


def _conform_shell(head, name, mat, coll, keep, inflate, solid, noise=0.0, noise_scale=10.0, seed=1, post=None):
    """Copy the face mesh region `keep(rel)` and push it outward: beards and hair that hug the head."""
    me = head.data.copy()
    obj = bpy.data.objects.new(name, me)
    coll.objects.link(obj)
    me.materials.clear()
    me.materials.append(mat)
    c = sum((v.co for v in me.vertices), Vector()) / len(me.vertices)
    R = max(v.co.z - c.z for v in me.vertices) / 1.06
    _delete_verts(obj, lambda co: keep((co - c) / R))
    import bmesh as _bm
    bm = _bm.new()
    bm.from_mesh(obj.data)
    loose = [v for v in bm.verts if not v.link_faces]
    _bm.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    # Push outward radially from the head centre (stable even on cut borders).
    for v in obj.data.vertices:
        d = (v.co - c)
        v.co += d.normalized() * inflate * R
    if post:
        for v in obj.data.vertices:
            v.co = post(v.co, c, R)
    obj.data.update()
    geo.store_rest(obj)
    if noise:
        from mathutils import noise as _n
        off = Vector((seed * 3.1, seed * 1.7, seed * 5.3))
        for v in obj.data.vertices:
            d = (v.co - c).normalized()
            v.co += d * _n.noise(v.co / R * noise_scale * .3 + off) * noise * R
        obj.data.update()
        geo.store_rest(obj)
    _solidify(obj, solid * R, -1)
    _subsurf(obj, 1)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def beard(C, R, mat, coll, length=1.0, width=1.0, braid=False, mustache=True, seed=3, head=None, fork=False):
    """Beard conforming to the jaw; `length` extends it below the chin."""
    out = []
    if head is None:
        head = next((o for o in coll.objects if o.get('face_mesh') and (o.matrix_world.translation - C).length < 1e-3
                     or (o.get('face_mesh') and (sum((v.co for v in o.data.vertices), Vector()) / len(o.data.vertices) - C).length < R * .3)), None)
    if head is None:
        return out

    def keep(rel):
        mouth = rel.x > .75 and abs(rel.y) < .2 and -0.5 < rel.z < -0.3
        line = -0.42 + 0.4 * max(0.0, min(1.0, (0.85 - rel.x) / 0.8))
        return rel.x > -0.2 and rel.z < line and abs(rel.y) < 0.92 * width and not mouth

    def post(co, c, Rr):
        rel = (co - c) / Rr
        if rel.z < -0.5 and rel.x > 0.0:
            k = min(1.0, (-0.5 - rel.z) / 0.45) * max(0, 1 - abs(rel.y) / (0.95 * width))
            d = length * 0.9 * k
            if fork:
                d *= 0.7 + 0.3 * abs(math.sin(rel.y * 6))
            co = co + _v(Rr * d * .25, 0, -Rr * d)
        return co
    out.append(_conform_shell(head, 'Beard', mat, coll, keep, .06, .1, noise=.035, seed=seed, post=post))
    if mustache:
        for sy in (-1, 1):
            p0 = _surface_x(head, C + _v(R * 2, sy * R * .02, -R * .34))
            p1 = _surface_x(head, C + _v(R * 2, sy * R * .25, -R * .42))
            if p0 is None or p1 is None:
                continue
            pts = [p0 + _v(R * .03, 0, 0), p1 + _v(R * .04, 0, 0), p1 + _v(-R * .05, sy * R * .2, -R * .22)]
            out.append(geo.tube('Moustache', pts, [R * .07, R * .075, R * .025], mat, coll, sides=8))
    if braid:
        tipz = -0.9 - length * 0.9
        pts = [C + _v(R * .85, 0, R * (tipz + .1)), C + _v(R * .8, 0, R * (tipz - .35))]
        out.append(geo.tube('Beard braid', pts, [R * .1, R * .05], mat, coll, sides=8))
        out.append(geo.cylinder('Braid ring', R * .085, R * .08, C + _v(R * .83, 0, R * (tipz - .05)), mat, coll, 12))
    return out


def skull_head(C, R, mats, coll, jaw_open=0.0, eye_glow=True, cracked=0.0, horns=None):
    """Undead skull: bone cranium, deep sockets with soul-light, teeth and mandible."""
    out = []
    bonemat = mats['bone']
    k = geo.quadsphere('Skull', 1.0, (0, 0, 0), bonemat, coll, level=4, scale=(R * 1.05, R * .82, R * .98))
    S = geo.sculpt
    for sy in (-1, 1):
        S(k, _v(R * .9, sy * R * .36, R * .02), R * .3, _v(-R * .26, 0, 0))     # sockets
        S(k, _v(R * .7, sy * R * .62, -R * .25), R * .25, R * .07, mode='inflate')  # zygoma
        S(k, _v(R * .45, sy * R * .75, R * .15), R * .32, _v(0, -sy * R * .1, 0))   # temples
        S(k, _v(R * .55, sy * R * .55, -R * .62), R * .3, _v(0, -sy * R * .12, R * .05))
    S(k, _v(R * .95, 0, R * .32), R * .45, _v(R * .08, 0, 0))   # brow ridge
    S(k, _v(R * .95, 0, -R * .28), R * .16, _v(-R * .16, 0, 0))   # nasal cavity
    S(k, _v(R * .7, 0, -R * .8), R * .45, _v(R * .12, 0, R * .1))  # maxilla
    S(k, _v(-R * .4, 0, -R * .7), R * .5, _v(R * .1, 0, R * .2))
    geo.subdivide(k, 1)
    if cracked:
        geo.displace_noise(k, R * .025 * cracked, 7)
    geo.place(k, C)
    out.append(k)
    dark = mats.get('dark')
    for sy in (-1, 1):
        sock = geo.sphere('Socket shadow', R * .2, C + _v(R * .62, sy * R * .34, R * .02), dark, coll, 12, 8,
                          scale=(.6, 1, 1))
        out.append(sock)
        if eye_glow and mats.get('glow') is not None:
            out.append(geo.sphere('Soul light', R * .085, C + _v(R * .74, sy * R * .33, R * .03), mats['glow'],
                                  coll, 10, 6))
    nasal = geo.extrude('Nasal cavity', [(-.09 * R, 0), (.09 * R, 0), (0, .2 * R)], R * .12, dark, coll,
                        plane='YZ', bevel=0)
    geo.place(nasal, C + _v(R * .9, 0, -R * .42))
    out.append(nasal)
    # upper teeth
    for i in range(8):
        a = (i - 3.5) / 3.5 * 0.85
        p = C + _v(R * (.86 * math.cos(a) + .02), R * .6 * math.sin(a), -R * .72)
        t = geo.box('Tooth', (R * .1, R * .1, R * .16), p, bonemat, coll, bevel=R * .02,
                    rotation=(0, 0, math.degrees(a)))
        out.append(t)
    # mandible
    jaw_pts = [C + _v(R * .05, -R * .62, -R * .55), C + _v(R * .55, -R * .55, -R * .95),
               C + _v(R * .88, 0, -R * 1.02 - jaw_open * R * .2),
               C + _v(R * .55, R * .55, -R * .95), C + _v(R * .05, R * .62, -R * .55)]
    jaw = geo.tube('Mandible', geo.bezier_points(jaw_pts[0], jaw_pts[1], jaw_pts[3], jaw_pts[4], 14)
                   if False else _catmull(jaw_pts, 4), R * .13, bonemat, coll, sides=8, flatten=.7)
    out.append(jaw)
    for i in range(7):
        a = (i - 3) / 3 * 0.8
        p = C + _v(R * (.8 * math.cos(a) + .02), R * .55 * math.sin(a), -R * .88 - jaw_open * R * .2)
        out.append(geo.box('Lower tooth', (R * .09, R * .09, R * .12), p, bonemat, coll, bevel=R * .02,
                           rotation=(0, 0, math.degrees(a))))
    if horns:
        out += horns_pair(C, R, mats.get('horn', bonemat), coll, **horns)
    return out


def _catmull(points, steps=6):
    pts = [Vector(p) for p in points]
    pts = [pts[0]] + pts + [pts[-1]]
    out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[i + 1], pts[i + 2]
        for s in range(steps):
            t = s / steps
            t2, t3 = t * t, t * t * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 +
                              (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    out.append(pts[-2])
    return out


catmull = _catmull


def ghoul_head(C, R, mats, coll):
    """Gaunt, long-jawed ghoul with pointed ears and needle teeth."""
    out = []
    h = geo.quadsphere('Ghoul head', 1.0, (0, 0, 0), mats['skin'], coll, level=4, scale=(R * 1.15, R * .78, R * .95))
    S = geo.sculpt
    S(h, _v(R * .8, 0, -R * .6), R * .7, _v(R * .35, 0, -R * .2))  # long snout jaw
    for sy in (-1, 1):
        S(h, _v(R * .85, sy * R * .35, R * .15), R * .25, _v(-R * .14, 0, 0))
        S(h, _v(R * .5, sy * R * .55, -R * .35), R * .35, -R * .12, mode='inflate')
    S(h, _v(R * .9, 0, R * .35), R * .5, _v(R * .1, 0, -R * .05))
    S(h, _v(-R * .8, 0, R * .2), R * .6, _v(-R * .2, 0, R * .05))
    geo.subdivide(h, 1)
    geo.place(h, C)
    out.append(h)
    for sy in (-1, 1):
        out.append(geo.sphere('Eye', R * .1, C + _v(R * .78, sy * R * .32, R * .14), mats['glow'], coll, 10, 6))
        ear = geo.tube('Bat ear', [C + _v(-R * .1, sy * R * .7, R * .3), C + _v(-R * .5, sy * R * 1.25, R * .85)],
                       [R * .2, R * .01], mats['skin'], coll, sides=6, flatten=.35)
        out.append(ear)
    for i in range(9):
        a = (i - 4) / 4 * .7
        p = C + _v(R * (1.1 * math.cos(a) + .15), R * .45 * math.sin(a), -R * .72)
        out.append(geo.cylinder('Needle tooth', R * .035, R * .18, p, mats['bone'], coll, 6, radius2=0.002,
                                rotation=(180, 0, 0), bevel=0))
    return out


# ------------------------------------------------------------------ hair
def hair_cap(C, R, mat, coll, front=0.5, back=-0.55, length=0.0, volume=1.0, strands=1.0, seed=1, head=None,
             topknot=False, mohawk=False):
    """Hair conforming to the scalp above a hairline; optional length down the back."""
    out = []
    if head is None:
        head = next((o for o in coll.objects if o.get('face_mesh') and
                     (sum((v.co for v in o.data.vertices), Vector()) / len(o.data.vertices) - C).length < R * .3), None)
    if head is None:
        return out

    if mohawk:
        n = 9
        for i in range(n):
            t = i / (n - 1)
            a = math.radians(70 - 150 * t)
            base = C + _v(math.cos(a) * R * 1.0, 0, math.sin(a) * R * 1.02)
            out_dir = _v(math.cos(a), 0, math.sin(a))
            tip = base + out_dir * R * (.55 + .2 * math.sin(t * math.pi)) + _v(-R * .25, 0, R * .1)
            out.append(geo.tube('Mohawk tuft', [base - out_dir * R * .05, base.lerp(tip, .5) + _v(0, 0, R * .05), tip],
                                [R * .16, R * .1, R * .01], mat, coll, sides=8, flatten=.45))
        return out

    def keep(rel):
        line = front + (back - front) * (0.5 - 0.5 * max(-1, min(1, rel.x)))
        side_burn = rel.x > -0.1 and rel.x < 0.45 and abs(rel.y) > 0.7 and rel.z > -0.25
        return rel.z > line or side_burn or (rel.x < -0.25 and rel.z > line - length)

    def post(co, c, Rr):
        rel = (co - c) / Rr
        if mohawk:
            return co + _v(0, 0, Rr * .35 * max(0, rel.z + .2))
        if length and rel.x < -0.2:
            co = co + _v(-Rr * .1 * length, 0, -Rr * length * max(0, -rel.x - .2) * .9)
        return co
    out.append(_conform_shell(head, 'Hair', mat, coll, keep, .05 * volume, .08, noise=.05 * strands, seed=seed,
                              post=post))
    if topknot:
        out.append(geo.sphere('Topknot', R * .2, C + _v(-R * .45, 0, R * 1.0), mat, coll, 12, 8))
    return out


# ------------------------------------------------------------------ cloth headwear
def hood(C, R, mat, coll, pointed=0.0, cowl=1.0, opening=1.0, droop=0.0, shadow=None, seed=2):
    """Cloth hood with a face opening and a cowl that drapes onto the shoulders."""
    out = []
    h = geo.quadsphere('Hood', 1.0, (0, 0, 0), mat, coll, level=4, scale=(R * 1.22, R * 1.12, R * 1.2))

    def keep(co):
        x, y, z = co.x / R, co.y / R, co.z / R
        face = x > 0.45 and abs(y) < 0.72 * opening and -1.1 < z < 0.55 * opening + droop * .3
        bottom = z < -0.75 and x > -0.1
        return not face and not bottom
    _delete_verts(h, keep)
    for v in h.data.vertices:
        x, y, z = v.co / R
        if z < -0.2:
            k = (-0.2 - z)
            v.co.y *= 1 + 0.6 * k * cowl
            v.co.x *= 1 + 0.3 * k * cowl
            v.co.z -= R * 0.35 * k * cowl
        if pointed and z > 0.3:
            v.co.z += R * pointed * (z - .3) ** 2 * 1.4
            v.co.x -= R * pointed * (z - .3) ** 2 * .9
        if droop and x > 0.2 and z > 0.3:
            v.co.x += R * droop * (z - .3)
    h.data.update()
    geo.store_rest(h)
    geo.displace_noise(h, R * .04, 5, seed)
    _solidify(h, R * .06, 1)
    _subsurf(h, 1)
    geo.place(h, C + _v(-R * .05, 0, R * .05))
    out.append(h)
    return out


def cap_with_goggles(C, R, cap_mat, strap_mat, lens_mat, rim_mat, coll):
    out = []
    cap = geo.lathe('Leather cap', [(0, R * 1.12), (R * .55, R * 1.05), (R * .92, R * .72), (R * 1.04, R * .3),
                                    (R * 1.06, R * .12)], 24, cap_mat, coll, close_bottom=False)
    _solidify(cap, R * .05)
    _subsurf(cap, 1)
    geo.place(cap, C, scale=(1.02, .95, 1))
    out.append(cap)
    band = geo.lathe('Goggle strap', [(R * 1.07, R * .42), (R * 1.08, R * .3)], 32, strap_mat, coll,
                     close_bottom=False, close_top=False)
    _solidify(band, R * .04)
    geo.place(band, C, scale=(1.02, .95, 1))
    out.append(band)
    for sy in (-1, 1):
        rim = geo.cylinder('Goggle rim', R * .22, R * .16, C + _v(R * .98, sy * R * .3, R * .38), rim_mat, coll, 16,
                           rotation=(0, 75, 0), bevel=R * .02)
        lens = geo.cylinder('Goggle lens', R * .17, R * .02, C + _v(R * 1.06, sy * R * .3, R * .4), lens_mat, coll, 16,
                            rotation=(0, 75, 0), bevel=0)
        out += [rim, lens]
    return out


# ------------------------------------------------------------------ helmets
def _helm_lathe(name, C, R, prof, mat, coll, thickness=0.06, sx=1.0, sy=.92, sz=1.0, segments=32, sub=1):
    obj = geo.lathe(name, [(r * R, z * R) for r, z in prof], segments, mat, coll, close_bottom=False)
    _solidify(obj, R * thickness)
    if sub:
        _subsurf(obj, sub)
    geo.place(obj, C, scale=(sx, sy, sz))
    return obj


def kettle_hat(C, R, mats, coll):
    out = []
    prof = [(0, 1.22), (.35, 1.2), (.7, 1.08), (.95, .82), (1.06, .42), (1.08, .28), (1.4, .16), (1.62, .02),
            (1.66, -.02)]
    out.append(_helm_lathe('Kettle hat', C + _v(0, 0, R * .05), R, prof, mats['steel'], coll, .05))
    rim = geo.lathe('Kettle rim', [(R * 1.64, -R * .01), (R * 1.7, -R * .01)], 40, mats['trim'], coll,
                    close_bottom=False, close_top=False)
    _solidify(rim, R * .06, 0)
    geo.place(rim, C + _v(0, 0, R * .05), scale=(1, .92, 1))
    out.append(rim)
    # crest ridge
    out.append(geo.tube('Crest ridge', [C + _v(-R * .95, 0, R * .9), C + _v(0, 0, R * 1.3), C + _v(R * .95, 0, R * .9)],
                        R * .07, mats['trim'], coll, sides=8))
    return out


def nasal_helm(C, R, mats, coll, cheeks=True, aventail=None):
    out = []
    lift = R * .16
    prof = [(0, 1.42), (.3, 1.32), (.62, 1.08), (.88, .72), (1.02, .32), (1.06, .05)]
    out.append(_helm_lathe('Spangenhelm', C + _v(0, 0, lift), R, prof, mats['steel'], coll, .05))
    band = geo.lathe('Brow band', [(R * 1.08, R * .2), (R * 1.09, R * .02)], 40, mats['trim'], coll,
                     close_bottom=False, close_top=False)
    _solidify(band, R * .05, 0)
    geo.place(band, C + _v(0, 0, lift), scale=(1, .92, 1))
    out.append(band)
    for a in (0, 90, 180, 270):
        ang = math.radians(a)
        pts = [C + _v(math.cos(ang) * R * 1.06 * r, math.sin(ang) * R * .98 * r, R * z + lift) for r, z in
               ((1.0, .1), (.92, .7), (.65, 1.08), (.3, 1.33), (0.02, 1.44))]
        out.append(geo.tube('Helm rib', pts, R * .05, mats['trim'], coll, sides=6))
    out.append(geo.box('Nasal bar', (R * .08, R * .12, R * .5), C + _v(R * 1.1, 0, R * .02), mats['trim'], coll,
                       bevel=R * .03, rotation=(0, -8, 0)))
    if cheeks:
        for sy in (-1, 1):
            cp = geo.extrude('Cheek plate', [(0, 0), (R * .5, 0), (R * .42, -R * .62), (R * .1, -R * .75)], R * .05,
                             mats['steel'], coll, plane='XZ', bevel=R * .015)
            geo.place(cp, C + _v(R * .05, sy * R * .9, R * .12), rotation=(0, 0, -sy * 8))
            out.append(cp)
    if aventail is not None:
        out += mail_coif(C, R, aventail, coll, face=False)
    return out


def mail_coif(C, R, mat, coll, face=True):
    h = geo.quadsphere('Mail coif', 1.0, (0, 0, 0), mat, coll, level=3, scale=(R * 1.12, R * 1.0, R * 1.1))

    def keep(co):
        x, y, z = co / R
        if face and x > 0.5 and abs(y) < .65 and -0.9 < z < 0.55:
            return False
        return z < 0.35
    _delete_verts(h, keep)
    for v in h.data.vertices:
        if v.co.z < -0.3 * R:
            k = (-0.3 * R - v.co.z) / R
            v.co.y *= 1 + .9 * k
            v.co.x *= 1 + .5 * k
    h.data.update()
    geo.store_rest(h)
    _solidify(h, R * .05, 1)
    _subsurf(h, 1)
    geo.place(h, C)
    return [h]


def great_helm(C, R, mats, coll, crest=None, cross=True, horns=None):
    out = []
    prof = [(0, 1.18), (.55, 1.17), (.92, 1.1), (1.08, .95), (1.14, .55), (1.14, -.2), (1.1, -.72), (1.04, -.95)]
    helm = _helm_lathe('Great helm', C, R, prof, mats['steel'], coll, .05, sx=1.05, sy=.95, segments=24)
    out.append(helm)
    # visor slits and breaths
    for sy in (-1, 1):
        out.append(geo.box('Eye slit', (R * .12, R * .52, R * .07), C + _v(R * 1.12, sy * R * .3, R * .22),
                           mats['dark'], coll, bevel=R * .02, rotation=(0, 0, -sy * 12)))
    for i in range(4):
        for j in range(2):
            out.append(geo.cylinder('Breath hole', R * .035, R * .1,
                                    C + _v(R * 1.0, -R * (.45 + j * .14), -R * (.15 + i * .14)),
                                    mats['dark'], coll, 8, rotation=(0, 90, -40), bevel=0))
    if cross:
        out.append(geo.box('Brow plate', (R * .1, R * 1.25, R * .14), C + _v(R * 1.1, 0, R * .42), mats['trim'], coll,
                           bevel=R * .03))
        out.append(geo.box('Cross vertical', (R * .09, R * .16, R * 1.15), C + _v(R * 1.16, 0, -R * .2), mats['trim'],
                           coll, bevel=R * .03))
    band = geo.lathe('Helm top band', [(R * 1.12, R * .97), (R * 1.13, R * .85)], 32, mats['trim'], coll,
                     close_bottom=False, close_top=False)
    _solidify(band, R * .05, 0)
    geo.place(band, C, scale=(1.05, .95, 1))
    out.append(band)
    if crest:
        out += crest_plume(C + _v(0, 0, R * 1.15), R, crest, coll)
    if horns:
        out += horns_pair(C, R, horns.pop('mat'), coll, **horns)
    return out


def bascinet(C, R, mats, coll, visor=True):
    out = []
    prof = [(0, 1.55), (.2, 1.42), (.55, 1.1), (.88, .7), (1.04, .25), (1.06, -.3), (1.02, -.62)]
    out.append(_helm_lathe('Bascinet', C, R, prof, mats['steel'], coll, .05, sx=1.02))
    if visor:
        sn = geo.lathe('Hounskull visor', [(0, R * .95), (R * .22, R * .82), (R * .48, R * .5), (R * .68, R * .2),
                                           (R * .78, 0)], 20, mats['steel'], coll, close_bottom=False)
        _solidify(sn, R * .04)
        _subsurf(sn, 1)
        geo.place(sn, C + _v(R * .75, 0, -R * .08), rotation=(0, 90, 0), scale=(1.05, .9, 1))
        out.append(sn)
        for sy in (-1, 1):
            out.append(geo.box('Visor slit', (R * .08, R * .38, R * .06), C + _v(R * 1.22, sy * R * .24, R * .18),
                               mats['dark'], coll, bevel=R * .015, rotation=(0, -20, -sy * 20)))
        for i in range(5):
            out.append(geo.cylinder('Visor breath', R * .025, R * .1, C + _v(R * 1.38, -R * (.08 + i * .07), -R * .18),
                                    mats['dark'], coll, 6, rotation=(0, 90, -30), bevel=0))
        for sy in (-1, 1):
            out.append(geo.cylinder('Visor pivot', R * .08, R * .06, C + _v(R * .1, sy * R * .98, R * .1), mats['trim'],
                                    coll, 12, rotation=(90, 0, 0), bevel=R * .015))
    out += mail_coif(C + _v(0, 0, -R * .2), R * .98, mats.get('mail', mats['steel']), coll, face=False)
    return out


def sallet(C, R, mats, coll, visor_up=False):
    out = []
    prof = [(0, 1.2), (.5, 1.14), (.9, .85), (1.08, .4), (1.12, 0.05), (1.14, -.15)]
    s = _helm_lathe('Sallet', C, R, prof, mats['steel'], coll, .05, sub=0)
    for v in s.data.vertices:
        if v.co.x < C.x - R * .2 and v.co.z < C.z + R * .4:
            k = (C.x - R * .2 - v.co.x) / R
            v.co.x -= R * .5 * k
            v.co.z -= R * .25 * k
    s.data.update()
    geo.store_rest(s)
    _subsurf(s, 1)
    out.append(s)
    for sy in (-1, 1):
        out.append(geo.box('Sight slit', (R * .1, R * .45, R * .06), C + _v(R * 1.08, sy * R * .25, R * .18),
                           mats['dark'], coll, bevel=R * .015, rotation=(0, 0, -sy * 14)))
    return out


def barbute(C, R, mats, coll):
    out = []
    prof = [(0, 1.25), (.5, 1.18), (.92, .85), (1.08, .3), (1.1, -.4), (1.0, -.95)]
    out.append(_helm_lathe('Barbute', C, R, prof, mats['steel'], coll, .05, sy=.95))
    out.append(geo.box('T-opening', (R * .2, R * .62, R * .17), C + _v(R * 1.0, 0, R * .18), mats['dark'], coll,
                       bevel=R * .04))
    out.append(geo.box('T-opening stem', (R * .2, R * .22, R * .65), C + _v(R * 1.0, 0, -R * .2), mats['dark'], coll,
                       bevel=R * .04))
    out.append(geo.tube('Barbute ridge', [C + _v(-R * 1.0, 0, R * .4), C + _v(0, 0, R * 1.3), C + _v(R * 1.0, 0, R * .55)],
                        R * .05, mats['trim'], coll, sides=6))
    return out


def crown(C, R, mats, coll, points=7, height=0.55, radius=1.02, z=0.75, spikes=False, gems=True, tilt=0.0):
    out = []
    band = geo.lathe('Crown band', [(R * radius, R * z), (R * radius * 1.02, R * (z + .22))], 48, mats['gold'], coll,
                     close_top=False, close_bottom=False)
    _solidify(band, R * .06, 0)
    geo.place(band, C, rotation=(0, tilt, 0))
    out.append(band)
    rot = Matrix.Rotation(math.radians(tilt), 4, 'Y')
    for i in range(points):
        a = i * math.tau / points
        base = C + _v(math.cos(a) * R * radius, math.sin(a) * R * radius * .98, R * (z + .2))
        if spikes:
            tip = base * 1.0 + _v(math.cos(a) * R * .12, math.sin(a) * R * .12, R * height * 1.3)
            p = geo.tube('Crown spike', [base, base.lerp(tip, .5), tip], [R * .09, R * .06, R * .005], mats['gold'],
                         coll, sides=6)
        else:
            pts2 = [(-R * .14, 0), (R * .14, 0), (R * .07, R * height * .55), (0, R * height),
                    (-R * .07, R * height * .55)]
            p = geo.extrude('Crown point', pts2, R * .05, mats['gold'], coll, plane='XZ', bevel=R * .015)
            geo.place(p, base, rotation=(0, 0, math.degrees(a) + 90))
        geo.transform(p, Matrix.Translation(C) @ rot @ Matrix.Translation(-C))
        out.append(p)
        if gems and mats.get('gem') is not None:
            g = geo.sphere('Crown jewel', R * .07, base + _v(math.cos(a) * R * .03, math.sin(a) * R * .03, -R * .09),
                           mats['gem'], coll, 10, 6)
            geo.transform(g, Matrix.Translation(C) @ rot @ Matrix.Translation(-C))
            out.append(g)
    return out


def mitre(C, R, mats, coll, height=1.9):
    """Two-peaked bishop's mitre: tapering front/back panels with gold orphreys."""
    out = []
    secs = []
    n = 9
    for k in range(n + 1):
        t = k / n
        z = R * (.55 + height * t)
        half_w = R * (.98 - .55 * t ** 1.6)          # side-to-side
        depth = R * (1.0 - .82 * t ** 1.2)           # front-to-back
        pts = []
        for i in range(16):
            a = i * math.tau / 16
            pts.append(C + _v(math.cos(a) * depth - R * .05, math.sin(a) * half_w, z))
        secs.append(pts)
    m = geo.loft('Mitre', secs, mats['cloth'], coll)
    # notch between the two peaks (front/back panels separate at the top)
    for v in m.data.vertices:
        rel = (v.co - C) / R
        if rel.z > .55 + height * .55 and abs(rel.x + .05) < .25:
            v.co.z -= R * .35 * (rel.z - .55 - height * .55) / (height * .45) * (1 - abs(rel.x + .05) / .25)
    m.data.update()
    geo.store_rest(m)
    _subsurf(m, 1)
    out.append(m)
    for sx in (-1, 1):
        pts = [C + _v(sx * R * .98 - R * .05, 0, R * .6), C + _v(sx * R * .55 - R * .05, 0, R * (.55 + height * .7)),
               C + _v(sx * R * .2 - R * .05, 0, R * (.55 + height * .98))]
        out.append(geo.tube('Mitre orphrey', pts, R * .08, mats['gold'], coll, sides=6, flatten=.5))
    band = geo.lathe('Mitre band', [(R * 1.0, R * .5), (R * 1.0, R * .82)], 32, mats['gold'], coll,
                     close_top=False, close_bottom=False)
    _solidify(band, R * .06, 0)
    geo.place(band, C + _v(-R * .05, 0, 0), scale=(1.02, 1.0, 1))
    out.append(band)
    if mats.get('gem') is not None:
        out.append(geo.sphere('Mitre jewel', R * .13, C + _v(R * .92, 0, R * 1.25), mats['gem'], coll, 12, 8))
    return out


def horns_pair(C, R, mat, coll, length=1.4, curl=0.8, up=0.6, back=0.3, thick=0.22, base_y=0.75, base_z=0.55,
               ram=False, segments=10):
    out = []
    for sy in (-1, 1):
        p0 = C + _v(-R * .05, sy * R * base_y, R * base_z)
        if ram:
            pts = [p0, p0 + _v(-R * .6, sy * R * .5, R * .2), p0 + _v(-R * .3, sy * R * .9, -R * .6),
                   p0 + _v(R * .4, sy * R * .8, -R * .5)]
        else:
            pts = [p0, p0 + _v(-R * back * .5, sy * R * length * .55, R * up * .3),
                   p0 + _v(-R * back, sy * R * length * .8, R * up * .9),
                   p0 + _v(-R * back * .6 + R * curl * .2, sy * R * length * .85, R * (up * 1.6 + curl * .3))]
        pts = _catmull(pts, segments // 2)
        out.append(geo.tube('Horn', pts, geo.taper(len(pts), R * thick, R * .01, 1.2), mat, coll, sides=10))
    return out


def crest_plume(base, R, mat, coll, length=1.2, count=7):
    out = []
    for i in range(count):
        t = i / max(1, count - 1)
        p0 = base + _v(R * (.5 - t), 0, 0)
        pts = [p0, p0 + _v(-R * .3, 0, R * .35), p0 + _v(-R * .9 * length, 0, R * .25)]
        out.append(geo.tube('Plume', pts, [R * .1, R * .14, R * .02], mat, coll, sides=6, flatten=.4))
    return out


def antlers(C, R, mat, coll, spread=1.0, tines=3):
    out = []
    for sy in (-1, 1):
        p0 = C + _v(-R * .1, sy * R * .55, R * .75)
        main = [p0, p0 + _v(-R * .3, sy * R * .7 * spread, R * .8), p0 + _v(-R * .1, sy * R * 1.2 * spread, R * 1.7),
                p0 + _v(R * .1, sy * R * 1.35 * spread, R * 2.3)]
        main = _catmull(main, 4)
        out.append(geo.tube('Antler', main, geo.taper(len(main), R * .12, R * .03), mat, coll, sides=8))
        for k in range(tines):
            idx = int(len(main) * (0.35 + 0.18 * k))
            q = main[min(idx, len(main) - 1)]
            tip = q + _v(R * (.4 - .1 * k), sy * R * .2, R * .55)
            out.append(geo.tube('Antler tine', [q, q.lerp(tip, .5) + _v(0, 0, R * .1), tip], [R * .07, R * .05, R * .01],
                                mat, coll, sides=6))
    return out
