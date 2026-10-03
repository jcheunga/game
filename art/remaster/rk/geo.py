"""Mesh construction toolkit (bmesh / data API; no operator context required).

Shapes are authored in world space with the object origin at the world origin,
then bound to bones while preserving their world transform. Every mesh gets a
`rest` point attribute so shaders keep stable, non-swimming coordinates.
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector, noise
from mathutils.bvhtree import BVHTree


def _finish_mesh(name, bm, mat=None, coll=None, smooth=True, subsurf=0, bevel=0.0, bevel_segments=2,
                 solidify=0.0, weighted=False, auto_smooth=None):
    me = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    (coll or bpy.context.scene.collection).objects.link(obj)
    if mat is not None:
        if isinstance(mat, (list, tuple)):
            for m in mat:
                me.materials.append(m)
        else:
            me.materials.append(mat)
    for p in me.polygons:
        p.use_smooth = smooth
    if solidify:
        mod = obj.modifiers.new('Thickness', 'SOLIDIFY')
        mod.thickness = solidify
        mod.offset = 0
        mod.use_even_offset = True
    if bevel:
        mod = obj.modifiers.new('Crafted edges', 'BEVEL')
        mod.width = bevel
        mod.segments = bevel_segments
        mod.limit_method = 'ANGLE'
        mod.angle_limit = math.radians(35)
        mod.harden_normals = False
    if subsurf:
        mod = obj.modifiers.new('Smooth form', 'SUBSURF')
        mod.levels = subsurf
        mod.render_levels = subsurf
    if weighted:
        mod = obj.modifiers.new('Weighted normals', 'WEIGHTED_NORMAL')
        mod.keep_sharp = True
    store_rest(obj)
    return obj


def store_rest(obj):
    me = obj.data
    if me is None or not hasattr(me, 'attributes'):
        return
    if 'rest' in me.attributes:
        me.attributes.remove(me.attributes['rest'])
    attr = me.attributes.new('rest', 'FLOAT_VECTOR', 'POINT')
    co = [0.0] * (len(me.vertices) * 3)
    me.vertices.foreach_get('co', co)
    attr.data.foreach_set('vector', co)


def apply_modifiers(obj, keep=('ARMATURE',)):
    """Bake the modifier stack into the mesh (context-free)."""
    dg = bpy.context.evaluated_depsgraph_get()
    saved = []
    for mod in obj.modifiers:
        if mod.type in keep:
            saved.append(mod)
            mod.show_viewport = False
    dg.update()
    ev = obj.evaluated_get(dg)
    me = bpy.data.meshes.new_from_object(ev, preserve_all_data_layers=True, depsgraph=dg)
    old = obj.data
    obj.data = me
    for mod in list(obj.modifiers):
        if mod.type not in keep:
            obj.modifiers.remove(mod)
    for mod in saved:
        mod.show_viewport = True
    if old.users == 0:
        bpy.data.meshes.remove(old)
    store_rest(obj)
    return obj


def transform(obj, matrix):
    obj.data.transform(matrix)
    obj.data.update()
    store_rest(obj)
    return obj


def place(obj, location=(0, 0, 0), rotation=(0, 0, 0), scale=(1, 1, 1)):
    """Bake a transform into mesh data (object stays at origin)."""
    if isinstance(scale, (int, float)):
        scale = (scale, scale, scale)
    m = Matrix.LocRotScale(Vector(location), _euler(rotation).to_quaternion(), Vector(scale))
    return transform(obj, m)


def _euler(rot):
    from mathutils import Euler
    return Euler(tuple(math.radians(r) for r in rot), 'XYZ')


# ---------------------------------------------------------------- primitives
def box(name, size, location=(0, 0, 0), mat=None, coll=None, bevel=0.02, rotation=(0, 0, 0), subsurf=0,
        segments=2):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    obj = _finish_mesh(name, bm, mat, coll, smooth=bool(subsurf), subsurf=subsurf, bevel=bevel,
                       bevel_segments=segments, weighted=bool(bevel) and not subsurf)
    return place(obj, location, rotation)


def sphere(name, radius=1.0, location=(0, 0, 0), mat=None, coll=None, segments=24, rings=14, scale=(1, 1, 1),
           rotation=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=radius)
    obj = _finish_mesh(name, bm, mat, coll)
    return place(obj, location, rotation, scale)


def quadsphere(name, radius=1.0, location=(0, 0, 0), mat=None, coll=None, level=3, scale=(1, 1, 1),
               rotation=(0, 0, 0)):
    """Evenly subdivided cube-sphere: the clean base for sculpting heads and creatures."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=2.0)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges, cuts=2 ** level - 1, use_grid_fill=True)
    for v in bm.verts:
        v.co = v.co.normalized() * radius
    obj = _finish_mesh(name, bm, mat, coll)
    return place(obj, location, rotation, scale)


def cylinder(name, radius, depth, location=(0, 0, 0), mat=None, coll=None, segments=24, rotation=(0, 0, 0),
             bevel=0.01, radius2=None, cap=True):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=segments, radius1=radius,
                          radius2=radius if radius2 is None else radius2, depth=depth)
    obj = _finish_mesh(name, bm, mat, coll, smooth=True, bevel=bevel, weighted=bool(bevel))
    return place(obj, location, rotation)


# ---------------------------------------------------------------- profile tools
def lathe(name, profile, segments=32, mat=None, coll=None, location=(0, 0, 0), rotation=(0, 0, 0),
          subsurf=0, smooth=True, close_top=True, close_bottom=True, scale=(1, 1, 1), bevel=0.0,
          angle=math.tau):
    """Revolve a (radius, z) profile about Z. Radius 0 points become poles."""
    bm = bmesh.new()
    full = abs(angle - math.tau) < 1e-6
    steps = segments if full else segments + 1
    rings = []
    for r, z in profile:
        ring = []
        if r <= 1e-6:
            ring = [bm.verts.new((0, 0, z))]
        else:
            for i in range(steps):
                a = angle * i / segments
                ring.append(bm.verts.new((r * math.cos(a), r * math.sin(a), z)))
        rings.append(ring)
    for ra, rb in zip(rings, rings[1:]):
        if len(ra) == 1 and len(rb) == 1:
            continue
        if len(ra) == 1:
            for i in range(len(rb) - (0 if full else 1)):
                bm.faces.new((ra[0], rb[i], rb[(i + 1) % len(rb)]))
        elif len(rb) == 1:
            for i in range(len(ra) - (0 if full else 1)):
                bm.faces.new((ra[i], rb[0], ra[(i + 1) % len(ra)]))
        else:
            for i in range(len(ra) - (0 if full else 1)):
                j = (i + 1) % len(ra)
                bm.faces.new((ra[i], ra[j], rb[j], rb[i]))
    if full and close_bottom and len(rings[0]) > 1:
        bm.faces.new(list(reversed(rings[0])))
    if full and close_top and len(rings[-1]) > 1:
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = _finish_mesh(name, bm, mat, coll, smooth=smooth, subsurf=subsurf, bevel=bevel)
    return place(obj, location, rotation, scale)


def extrude(name, outline, depth, mat=None, coll=None, plane='XZ', offset=0.0, bevel=0.01, segments=2,
            location=(0, 0, 0), rotation=(0, 0, 0), smooth=False, subsurf=0, center=True):
    """Extrude a closed 2D outline. plane: 'XZ' extrudes along Y, 'XY' along Z, 'YZ' along X."""
    bm = bmesh.new()

    def to3(u, v, w):
        return {'XZ': (u, w, v), 'XY': (u, v, w), 'YZ': (w, u, v)}[plane]
    base = -depth / 2 if center else 0
    front = [bm.verts.new(to3(u, v, base + offset)) for u, v in outline]
    face = bm.faces.new(front)
    bmesh.ops.recalc_face_normals(bm, faces=[face])
    res = bmesh.ops.extrude_face_region(bm, geom=[face])
    verts = [e for e in res['geom'] if isinstance(e, bmesh.types.BMVert)]
    d = {'XZ': Vector((0, depth, 0)), 'XY': Vector((0, 0, depth)), 'YZ': Vector((depth, 0, 0))}[plane]
    bmesh.ops.translate(bm, vec=d, verts=verts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = _finish_mesh(name, bm, mat, coll, smooth=smooth, bevel=bevel, bevel_segments=segments,
                       subsurf=subsurf, weighted=bool(bevel))
    return place(obj, location, rotation)


def loft(name, sections, mat=None, coll=None, closed=True, cap=True, smooth=True, subsurf=0, bevel=0.0):
    """Skin between cross-sections (each a list of 3D points with equal counts)."""
    bm = bmesh.new()
    rings = [[bm.verts.new(p) for p in sec] for sec in sections]
    n = len(sections[0])
    for ra, rb in zip(rings, rings[1:]):
        for i in range(n if closed else n - 1):
            j = (i + 1) % n
            bm.faces.new((ra[i], ra[j], rb[j], rb[i]))
    if cap and closed:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _finish_mesh(name, bm, mat, coll, smooth=smooth, subsurf=subsurf, bevel=bevel)


def _frames(points):
    """Parallel-transport frames along a polyline."""
    pts = [Vector(p) for p in points]
    tangents = []
    for i in range(len(pts)):
        a = pts[max(0, i - 1)]
        b = pts[min(len(pts) - 1, i + 1)]
        t = (b - a)
        tangents.append(t.normalized() if t.length > 1e-9 else Vector((0, 0, 1)))
    ref = Vector((0, 0, 1)) if abs(tangents[0].z) < 0.9 else Vector((1, 0, 0))
    n = tangents[0].cross(ref).normalized()
    frames = []
    for i, t in enumerate(tangents):
        if i > 0:
            axis = tangents[i - 1].cross(t)
            if axis.length > 1e-9:
                ang = tangents[i - 1].angle(t)
                n = Matrix.Rotation(ang, 3, axis.normalized()) @ n
        b = t.cross(n).normalized()
        frames.append((pts[i], t, n, b))
    return frames


def tube(name, points, radii, mat=None, coll=None, sides=10, cap=True, smooth=True, subsurf=0,
         profile=None, twist=0.0, flatten=1.0):
    """Tapered tube along points. radii: float or per-point list. profile: optional 2D ring (unit)."""
    if isinstance(radii, (int, float)):
        radii = [radii] * len(points)
    ring2d = profile or [(math.cos(i * math.tau / sides), math.sin(i * math.tau / sides)) for i in range(sides)]
    sections = []
    for i, (p, t, n, b) in enumerate(_frames(points)):
        r = radii[i]
        rot = twist * i / max(1, len(points) - 1)
        sec = []
        for u, v in ring2d:
            cu, cv = u * math.cos(rot) - v * math.sin(rot), u * math.sin(rot) + v * math.cos(rot)
            sec.append(p + n * (cu * r) + b * (cv * r * flatten))
        sections.append(sec)
    obj = loft(name, sections, mat, coll, True, cap, smooth, subsurf)
    return obj


def bezier_points(p0, p1, p2, p3=None, steps=12):
    p0, p1, p2 = Vector(p0), Vector(p1), Vector(p2)
    out = []
    for i in range(steps + 1):
        t = i / steps
        if p3 is None:
            out.append((1 - t) ** 2 * p0 + 2 * (1 - t) * t * p1 + t * t * p2)
        else:
            q3 = Vector(p3)
            out.append((1 - t) ** 3 * p0 + 3 * (1 - t) ** 2 * t * p1 + 3 * (1 - t) * t * t * p2 + t ** 3 * q3)
    return out


def taper(n, r0, r1, power=1.0):
    return [r0 + (r1 - r0) * (i / max(1, n - 1)) ** power for i in range(n)]


# ---------------------------------------------------------------- sheets / plates
def grid_sheet(name, width, height, nx, ny, fn, mat=None, coll=None, solidify=0.0, subsurf=0, smooth=True,
               bevel=0.0):
    """Parametric sheet: fn(u, v) -> 3D point for u, v in [0, 1]."""
    bm = bmesh.new()
    verts = [[bm.verts.new(fn(i / nx, j / ny)) for i in range(nx + 1)] for j in range(ny + 1)]
    for j in range(ny):
        for i in range(nx):
            bm.faces.new((verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]))
    return _finish_mesh(name, bm, mat, coll, smooth=smooth, solidify=solidify, subsurf=subsurf, bevel=bevel)


def shell_cap(name, radius, height, mat=None, coll=None, segments=24, rings=8, thickness=0.02, lip=0.0,
              location=(0, 0, 0), rotation=(0, 0, 0), scale=(1, 1, 1), open_angle=math.tau):
    """Domed shell, pole at +Z, open rim at Z=0 (pauldron lames, couters, poleyns)."""
    prof = []
    for i in range(rings + 1):
        a = (math.pi / 2) * i / rings
        prof.append((radius * math.sin(a), height * math.cos(a)))
    if lip:
        prof.append((radius + lip, -lip * 0.3))
    obj = lathe(name, prof, segments, mat, coll, close_top=False, close_bottom=False, angle=open_angle)
    mod = obj.modifiers.new('Plate thickness', 'SOLIDIFY')
    mod.thickness = thickness
    mod.offset = -1
    mod.use_even_offset = True
    bev = obj.modifiers.new('Rolled edge', 'BEVEL')
    bev.width = thickness * .45
    bev.segments = 2
    bev.limit_method = 'ANGLE'
    return place(obj, location, rotation, scale)


# ---------------------------------------------------------------- sculpting
def sculpt(obj, center, radius, offset, falloff=2.0, mode='move'):
    """Soft-brush deformation in object space. mode: move | inflate | flatten | pinch."""
    me = obj.data
    c = Vector(center)
    off = Vector(offset) if not isinstance(offset, (int, float)) else offset
    for v in me.vertices:
        d = (v.co - c).length
        if d >= radius:
            continue
        w = (1 - (d / radius) ** 2) ** falloff
        if mode == 'move':
            v.co += off * w
        elif mode == 'inflate':
            v.co += v.normal * (off * w)
        elif mode == 'pinch':
            v.co = v.co.lerp(c, off * w)
        elif mode == 'flatten':
            n = Vector(offset).normalized()
            dist = (v.co - c).dot(n)
            v.co -= n * dist * w
    me.update()
    store_rest(obj)
    return obj


def displace_noise(obj, amount, scale=3.0, seed=0, axis_mask=(1, 1, 1), normal=True):
    me = obj.data
    off = Vector((seed * 13.1, seed * 7.7, seed * 3.3))
    for v in me.vertices:
        n = noise.noise(v.co * scale + off)
        if normal:
            v.co += v.normal * n * amount
        else:
            nv = noise.noise_vector(v.co * scale + off)
            v.co += Vector((nv.x * axis_mask[0], nv.y * axis_mask[1], nv.z * axis_mask[2])) * amount
    me.update()
    store_rest(obj)
    return obj


def bend_fn(obj, fn):
    """Arbitrary per-vertex warp: fn(Vector) -> Vector."""
    me = obj.data
    for v in me.vertices:
        v.co = Vector(fn(v.co.copy()))
    me.update()
    store_rest(obj)
    return obj


def subdivide(obj, levels=1, smooth=True):
    mod = obj.modifiers.new('sub', 'SUBSURF')
    mod.levels = levels
    mod.render_levels = levels
    apply_modifiers(obj)
    if smooth:
        for p in obj.data.polygons:
            p.use_smooth = True
    return obj


# ---------------------------------------------------------------- skin bodies
def skin_body(name, nodes, edges, mat=None, coll=None, subsurf=2, smooth_iter=0, branch_smooth=0.6):
    """Organic body from a radius graph. nodes: list of (pos, (rx, ry)). Returns applied mesh."""
    me = bpy.data.meshes.new(name)
    me.from_pydata([p for p, _ in nodes], edges, [])
    me.update()
    obj = bpy.data.objects.new(name, me)
    (coll or bpy.context.scene.collection).objects.link(obj)
    skin = obj.modifiers.new('Skin', 'SKIN')
    skin.branch_smoothness = branch_smooth
    skin.use_smooth_shade = True
    for i, (_, r) in enumerate(nodes):
        me.skin_vertices[0].data[i].radius = r
    me.skin_vertices[0].data[0].use_root = True
    sub = obj.modifiers.new('Subdivision', 'SUBSURF')
    sub.levels = subsurf
    sub.render_levels = subsurf
    if smooth_iter:
        sm = obj.modifiers.new('Relax', 'SMOOTH')
        sm.iterations = smooth_iter
        sm.factor = 0.5
    apply_modifiers(obj)
    if mat is not None:
        if isinstance(mat, (list, tuple)):
            for m in mat:
                obj.data.materials.append(m)
        else:
            obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def assign_material_by(obj, fn):
    """fn(face_center Vector, normal) -> material slot index."""
    me = obj.data
    for p in me.polygons:
        p.material_index = fn(Vector(p.center), Vector(p.normal))
    me.update()
    return obj


def join(objects, name=None):
    """Join meshes into the first object (context-free via bmesh)."""
    target = objects[0]
    bm = bmesh.new()
    mats = []
    for obj in objects:
        me = obj.data
        local = bmesh.new()
        local.from_mesh(me)
        index_map = {}
        for i, m in enumerate(me.materials):
            if m not in mats:
                mats.append(m)
            index_map[i] = mats.index(m)
        for f in local.faces:
            f.material_index = index_map.get(f.material_index, 0)
        local.transform(obj.matrix_world)
        tmp = bpy.data.meshes.new('tmp')
        local.to_mesh(tmp)
        local.free()
        bm.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
    me = bpy.data.meshes.new(name or target.name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    mods = [(m.type, m.name) for m in target.modifiers]
    new = bpy.data.objects.new(name or target.name, me)
    for c in target.users_collection:
        c.objects.link(new)
    for obj in objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    store_rest(new)
    return new


def mirror_y(obj, name=None):
    """Duplicate mesh mirrored across the XZ plane (Y -> -Y)."""
    me = obj.data.copy()
    new = bpy.data.objects.new(name or obj.name, me)
    for c in obj.users_collection:
        c.objects.link(new)
    me.transform(Matrix.Scale(-1, 4, (0, 1, 0)))
    me.flip_normals()
    me.update()
    for mod in obj.modifiers:
        m = new.modifiers.new(mod.name, mod.type)
        for attr in ('thickness', 'offset', 'use_even_offset', 'width', 'segments', 'limit_method',
                     'angle_limit', 'levels', 'render_levels', 'keep_sharp'):
            if hasattr(mod, attr):
                setattr(m, attr, getattr(mod, attr))
    store_rest(new)
    return new


def duplicate(obj, name=None, matrix=None):
    me = obj.data.copy()
    new = bpy.data.objects.new(name or obj.name, me)
    for c in obj.users_collection:
        c.objects.link(new)
    for mod in obj.modifiers:
        m = new.modifiers.new(mod.name, mod.type)
        for attr in ('thickness', 'offset', 'use_even_offset', 'width', 'segments', 'limit_method',
                     'angle_limit', 'levels', 'render_levels', 'keep_sharp'):
            if hasattr(mod, attr):
                setattr(m, attr, getattr(mod, attr))
    if matrix is not None:
        transform(new, matrix)
    return new


def rivets(name, points, radius, mat, coll=None, normal_axis=None):
    """Small domed studs at points (list of (pos, normal))."""
    bm = bmesh.new()
    for p, n in points:
        geom = bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=5, radius=radius)
        vs = geom['verts']
        n = Vector(n).normalized()
        rot = Vector((0, 0, 1)).rotation_difference(n).to_matrix().to_4x4()
        for v in vs:
            co = v.co.copy()
            co.z *= 0.55
            v.co = (rot @ co) + Vector(p)
    return _finish_mesh(name, bm, mat, coll, smooth=True)


def ray_surface(obj, origin, direction):
    """Project a point onto a mesh surface (world space, object at identity)."""
    dg = bpy.context.evaluated_depsgraph_get()
    tree = BVHTree.FromObject(obj, dg)
    hit = tree.ray_cast(Vector(origin), Vector(direction).normalized())
    return hit
