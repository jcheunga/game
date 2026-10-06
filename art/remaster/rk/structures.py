"""Battle-structure kit: batched hand-cut parts with per-part variation, structure
materials and reusable props (wheels, lanterns, banners, braziers, chains...).

Batch accumulates many small parts (planks, ashlar blocks, plates, studs) into one
mesh. Every part carries a `var` point attribute in [0, 1]; the structure materials
here read it to give each board / block / plate its own tone and grain offset, so a
wall reads as individually cut pieces instead of one procedural surface.

Geometry is authored in world space with objects at the origin (same convention as
geo.py); every mesh gets the `rest` attribute.
"""
import math
import random

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

from . import core, geo, shaders as S
from .core import scale_rgb, srgb
from .heads import catmull, skull_head


def V(*a):
    return Vector(a)


def euler(rot):
    return Euler(tuple(math.radians(r) for r in rot), 'XYZ')


def mat4(loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    if isinstance(scale, (int, float)):
        scale = (scale, scale, scale)
    return Matrix.LocRotScale(Vector(loc), euler(rot).to_quaternion(), Vector(scale))


def look_matrix(p0, p1, up=(0, 0, 1)):
    """Rotation whose local +Z runs from p0 to p1."""
    d = (Vector(p1) - Vector(p0)).normalized()
    return d.to_track_quat('Z', 'Y' if abs(d.dot(Vector(up))) > 0.95 else 'X').to_matrix()


# ====================================================================== batch
class Batch:
    """Many parts, one mesh. Each part gets a `var` value for per-part shading."""

    def __init__(self, name, coll, rng=None):
        self.name = name
        self.coll = coll
        self.bm = bmesh.new()
        self.var = self.bm.verts.layers.float.new('var')
        self.mats = []
        self.rng = rng or random.Random(hash(name) & 0xffff)

    def _mi(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def _tag(self, verts, mat, var=None, smooth=True):
        mi = self._mi(mat)
        if var is None:
            var = self.rng.random()
        faces = set()
        for v in verts:
            v[self.var] = var
            faces.update(v.link_faces)
        for f in faces:
            f.material_index = mi
            f.smooth = smooth
        return verts

    # ---------------------------------------------------------------- parts
    def box(self, size, loc, mat, rot=(0, 0, 0), var=None, matrix=None):
        M = mat4(loc, rot, size) if matrix is None else matrix @ Matrix.Diagonal(Vector(size)).to_4x4()
        r = bmesh.ops.create_cube(self.bm, size=1.0, matrix=M)
        return self._tag(r['verts'], mat, var)

    def wedge_box(self, p0, p1, w0, w1, h0, h1, mat, var=None, up=(0, 0, 1)):
        """Tapered beam from p0 to p1: width (across) and height (up) taper."""
        p0, p1 = Vector(p0), Vector(p1)
        d = (p1 - p0).normalized()
        u = Vector(up)
        s = d.cross(u)
        if s.length < 1e-6:
            s = d.cross(Vector((1, 0, 0)))
        s.normalize()
        u = s.cross(d).normalized()
        vs = []
        for p, w, h in ((p0, w0, h0), (p1, w1, h1)):
            for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                vs.append(self.bm.verts.new(p + s * (a * w / 2) + u * (b * h / 2)))
        f = self.bm.faces.new
        f((vs[0], vs[3], vs[2], vs[1]))
        f((vs[4], vs[5], vs[6], vs[7]))
        for i in range(4):
            j = (i + 1) % 4
            f((vs[i], vs[j], vs[4 + j], vs[4 + i]))
        return self._tag(vs, mat, var)

    def cyl(self, r, depth, loc, mat, rot=(0, 0, 0), segs=16, r2=None, var=None, cap=True, matrix=None):
        M = mat4(loc, rot) if matrix is None else matrix
        res = bmesh.ops.create_cone(self.bm, cap_ends=cap, cap_tris=False, segments=segs, radius1=r,
                                    radius2=r if r2 is None else r2, depth=depth, matrix=M)
        return self._tag(res['verts'], mat, var)

    def sphere(self, r, loc, mat, segs=12, rings=8, scale=(1, 1, 1), var=None, rot=(0, 0, 0)):
        M = mat4(loc, rot, scale)
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=segs, v_segments=rings, radius=r, matrix=M)
        return self._tag(res['verts'], mat, var)

    def mesh(self, verts, faces, mat, var=None, matrix=None):
        M = matrix or Matrix.Identity(4)
        bv = [self.bm.verts.new(M @ Vector(v)) for v in verts]
        for f in faces:
            try:
                self.bm.faces.new([bv[i] for i in f])
            except ValueError:
                pass
        return self._tag(bv, mat, var)

    def ring(self, center, r_in, r_out, width, mat, axis='Y', segs=32, a0=0.0, a1=math.tau, var=None,
             profile=None):
        """Rectangular-section ring (or arc) around `axis` through `center`."""
        full = abs(a1 - a0 - math.tau) < 1e-6
        n = segs if full else segs + 1
        prof = profile or [(r_in, -width / 2), (r_out, -width / 2), (r_out, width / 2), (r_in, width / 2)]
        rot = {'Y': Matrix.Rotation(math.radians(90), 4, 'X'), 'Z': Matrix.Identity(4),
               'X': Matrix.Rotation(math.radians(90), 4, 'Y')}[axis]
        M = Matrix.Translation(Vector(center)) @ rot
        rings = []
        for i in range(n):
            a = a0 + (a1 - a0) * i / segs
            ca, sa = math.cos(a), math.sin(a)
            rings.append([self.bm.verts.new(M @ Vector((r * ca, r * sa, z))) for r, z in prof])
        k = len(prof)
        for i in range(segs):
            ra, rb = rings[i], rings[(i + 1) % n]
            for j in range(k):
                jj = (j + 1) % k
                self.bm.faces.new((ra[j], rb[j], rb[jj], ra[jj]))
        if not full:
            self.bm.faces.new(list(reversed(rings[0])))
            self.bm.faces.new(rings[-1])
        verts = [v for r in rings for v in r]
        return self._tag(verts, mat, var)

    def lathe(self, profile, mat, matrix=None, segs=16, var=None, cap_bottom=True, cap_top=True):
        """Revolve (r, z) profile about local Z; matrix places it."""
        M = matrix or Matrix.Identity(4)
        rings = []
        for r, z in profile:
            if r <= 1e-6:
                rings.append([self.bm.verts.new(M @ Vector((0, 0, z)))])
            else:
                rings.append([self.bm.verts.new(M @ Vector((r * math.cos(a), r * math.sin(a), z)))
                              for a in (math.tau * i / segs for i in range(segs))])
        for ra, rb in zip(rings, rings[1:]):
            if len(ra) == 1 and len(rb) == 1:
                continue
            if len(ra) == 1:
                for i in range(segs):
                    self.bm.faces.new((ra[0], rb[i], rb[(i + 1) % segs]))
            elif len(rb) == 1:
                for i in range(segs):
                    self.bm.faces.new((ra[i], rb[0], ra[(i + 1) % segs]))
            else:
                for i in range(segs):
                    j = (i + 1) % segs
                    self.bm.faces.new((ra[i], ra[j], rb[j], rb[i]))
        if cap_bottom and len(rings[0]) > 1:
            self.bm.faces.new(list(reversed(rings[0])))
        if cap_top and len(rings[-1]) > 1:
            self.bm.faces.new(rings[-1])
        verts = [v for r in rings for v in r]
        return self._tag(verts, mat, var)

    def prism(self, pts2d, z0, z1, mat, matrix=None, var=None):
        """Extrude a closed (x, y) outline from z0 to z1 (then place with matrix)."""
        M = matrix or Matrix.Identity(4)
        n = len(pts2d)
        bot = [self.bm.verts.new(M @ Vector((x, y, z0))) for x, y in pts2d]
        top = [self.bm.verts.new(M @ Vector((x, y, z1))) for x, y in pts2d]
        self.bm.faces.new(list(reversed(bot)))
        self.bm.faces.new(top)
        for i in range(n):
            j = (i + 1) % n
            self.bm.faces.new((bot[i], bot[j], top[j], top[i]))
        return self._tag(bot + top, mat, var)

    def plank_disc(self, center, r, z0, z1, mat, planks=5, gap=0.01, rot=0.0, var=None):
        """Round platform made of parallel planks clipped to a circle."""
        c = Vector(center)
        Rz = Matrix.Translation(c) @ Matrix.Rotation(math.radians(rot), 4, 'Z')
        for i in range(planks):
            xa = -r + 2 * r * i / planks + gap / 2
            xb = -r + 2 * r * (i + 1) / planks - gap / 2
            pts = []
            for k in range(7):
                x = xa + (xb - xa) * k / 6
                pts.append((x, -math.sqrt(max(r * r - x * x, 1e-6))))
            for k in range(7):
                x = xb - (xb - xa) * k / 6
                pts.append((x, math.sqrt(max(r * r - x * x, 1e-6))))
            self.prism(pts, z0, z1, mat, matrix=Rz, var=var)

    def stud(self, p, n, r, mat, var=None, segs=8, height=0.55):
        n = Vector(n).normalized()
        rot = Vector((0, 0, 1)).rotation_difference(n).to_matrix().to_4x4()
        M = Matrix.Translation(Vector(p)) @ rot @ Matrix.Diagonal((1, 1, height, 1))
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=segs, v_segments=5, radius=r, matrix=M)
        return self._tag(res['verts'], mat, var)

    def torus(self, center, R, r, mat, rot=(0, 0, 0), scale=(1, 1, 1), segs=12, sides=6, var=None, matrix=None):
        M = (matrix if matrix is not None else mat4(center, rot)) @ Matrix.Diagonal(Vector(scale)).to_4x4()
        rings = []
        for i in range(segs):
            a = math.tau * i / segs
            c = Vector((math.cos(a), math.sin(a), 0))
            ring = []
            for j in range(sides):
                b = math.tau * j / sides
                p = c * (R + r * math.cos(b)) + Vector((0, 0, r * math.sin(b)))
                ring.append(self.bm.verts.new(M @ p))
            rings.append(ring)
        for i in range(segs):
            ra, rb = rings[i], rings[(i + 1) % segs]
            for j in range(sides):
                jj = (j + 1) % sides
                self.bm.faces.new((ra[j], rb[j], rb[jj], ra[jj]))
        return self._tag([v for r_ in rings for v in r_], mat, var)

    def absorb(self, obj, var=None, apply=True):
        """Pull an existing object's evaluated mesh (with its materials) into the batch."""
        if apply and obj.modifiers:
            geo.apply_modifiers(obj, keep=())
        me = obj.data
        tmp = bmesh.new()
        tmp.from_mesh(me)
        tmp.transform(obj.matrix_world)
        mats = list(me.materials)
        start = len(self.bm.verts)
        lut = {}
        for v in tmp.verts:
            lut[v.index] = self.bm.verts.new(v.co)
        for f in tmp.faces:
            try:
                nf = self.bm.faces.new([lut[v.index] for v in f.verts])
            except ValueError:
                continue
            nf.material_index = self._mi(mats[f.material_index]) if mats else 0
            nf.smooth = f.smooth
        tmp.free()
        vv = self.rng.random() if var is None else var
        for v in list(lut.values()):
            v[self.var] = vv
        bpy.data.objects.remove(obj, do_unlink=True)
        return start

    # ---------------------------------------------------------------- finish
    def finish(self, bevel=0.012, segments=2, angle=40, weighted=True, subsurf=0, smooth=True):
        if len(self.bm.verts) == 0:
            self.bm.free()
            return None
        me = bpy.data.meshes.new(self.name)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.normal_update()
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(m)
        obj = bpy.data.objects.new(self.name, me)
        self.coll.objects.link(obj)
        if not smooth:
            for p in me.polygons:
                p.use_smooth = False
        if bevel:
            mod = obj.modifiers.new('Crafted edges', 'BEVEL')
            mod.width = bevel
            mod.segments = segments
            mod.limit_method = 'ANGLE'
            mod.angle_limit = math.radians(angle)
            mod.harden_normals = False
        if subsurf:
            mod = obj.modifiers.new('Smooth form', 'SUBSURF')
            mod.levels = subsurf
            mod.render_levels = subsurf
        if weighted:
            mod = obj.modifiers.new('Weighted normals', 'WEIGHTED_NORMAL')
            mod.keep_sharp = True
        geo.store_rest(obj)
        return obj


# ================================================================== materials
def _attr(nb, name='var'):
    return nb.node('ShaderNodeAttribute', attribute_type='GEOMETRY', attribute_name=name).outputs['Fac']


def _offset(nb, var, k=(17.31, 9.17, 5.73), stretch=None):
    m = nb.node('ShaderNodeVectorMath', operation='MULTIPLY_ADD')
    nb.links.new(var, m.inputs[0])
    m.inputs[1].default_value = k
    nb.links.new(nb.coord(), m.inputs[2])
    v = m.outputs[0]
    if stretch:
        s = nb.node('ShaderNodeVectorMath', operation='MULTIPLY')
        nb.links.new(v, s.inputs[0])
        s.inputs[1].default_value = stretch
        v = s.outputs[0]
    return v


def _rest_z(nb):
    sep = nb.node('ShaderNodeSeparateXYZ')
    nb.links.new(nb.coord(), sep.inputs[0])
    return sep.outputs['Z']


@S.cached
def board(color='6b4a2e', name='Oak boards', axis='X', grain=5.0, rough=0.66, dark=0.56, var=0.24, worn=0.55,
          edge=1.45, scale=1.0, alt='', grime=0.0, grime_height=1.6, char=0.0, ember='', ember_strength=6.0,
          ember_share=1.0):
    """Planked timber: each part (var) gets its own grain offset and tone."""
    m, nb = S._new(name)
    base = srgb(color)
    vv = _attr(nb)
    stretch = {'X': (1, 9, 9), 'Y': (9, 1, 9), 'Z': (9, 9, 1)}[axis]
    vec = _offset(nb, vv, stretch=tuple(s * scale for s in stretch))
    g = nb.noise(grain, 6, .62, vec=vec, distortion=1.5)
    rings = nb.maprange(g, .36, .64)
    col = nb.ramp(rings, [(0, scale_rgb(base, dark)), (.55, base), (1, scale_rgb(base, 1.22))])
    if alt:
        col = nb.mixc(nb.maprange(vv, .62, .9), col, srgb(alt))
    col = nb.hsv(col, nb.maprange(vv, 0, 1, .49, .51), nb.maprange(vv, 0, 1, .74, .94),
                 nb.maprange(vv, 0, 1, 1 - var, 1 + var * .7))
    knots = nb.maprange(nb.noise(2.4 * scale, 2, .5, vec=_offset(nb, vv, (3.3, 7.1, 1.9))), .64, .74)
    col = nb.mixc(nb.math('MULTIPLY', knots, .5), col, scale_rgb(base, .4))
    if char:
        ch = nb.maprange(nb.noise(3.5 * scale, 5, .65, vec=vec), .42, .6, 0, char)
        col = nb.mixc(ch, col, srgb('15100c'))
    col = nb.finish_color(col, .5, .1, .1, scale_rgb(base, edge), worn, .014)
    if grime:
        gm = nb.maprange(_rest_z(nb), 0.2, grime_height, grime, 0)
        col = nb.mixc(gm, col, srgb('2a2119'))
    fib = nb.noise(grain * 6, 3, .6, vec=vec)
    h = nb.math('ADD', nb.math('MULTIPLY', rings, .6), nb.math('MULTIPLY', fib, .4))
    kw = {'Base Color': col, 'Roughness': nb.math('ADD', rough, nb.maprange(fib, .3, .7, -.06, .08)),
          'Normal': nb.bump(h, .26, .012)}
    if ember:
        cr = nb.maprange(nb.voronoi(4.5 * scale, 'DISTANCE_TO_EDGE', vec=vec), 0, .03 * min(1.0, ember_share + .4), 1, 0)
        cr = nb.math('MULTIPLY', cr, nb.maprange(nb.noise(2, 3, .5, vec=vec), .45, .6))
        if ember_share < 1.0:
            cr = nb.math('MULTIPLY', cr, nb.maprange(vv, 1 - ember_share, 1 - ember_share + .12))
        kw['Base Color'] = nb.mixc(cr, col, srgb(ember))
        kw['Emission Color'] = srgb(ember)
        kw['Emission Strength'] = nb.math('MULTIPLY', cr, ember_strength)
    nb.principled(**kw)
    return m


@S.cached
def ashlar(color='77736a', name='Ashlar', var=0.32, warm='857561', cool='5d646a', moss=0.0, blight=0.0,
           crack=0.6, crack_glow='', glow_strength=5.0, grime=0.55, grime_height=1.4, scale=1.0, rough=0.9,
           edge=1.32, grime_color='2b251d'):
    """Dressed stone blocks; `var` picks each block's tint and pattern offset."""
    m, nb = S._new(name)
    base = srgb(color)
    vv = _attr(nb)
    tone = nb.ramp(vv, [(0, scale_rgb(srgb(cool), .9)), (.3, base), (.62, srgb(warm)), (.82, scale_rgb(base, 1.12)),
                        (1, scale_rgb(srgb(cool), 1.1))])
    tone = nb.hsv(tone, .5, 1.0, nb.maprange(vv, 0, 1, 1 - var * .6, 1 + var * .5))
    vec = _offset(nb, vv, (3.1, 1.7, 2.3))
    n = nb.noise(3 * scale, 6, .65, vec=vec)
    fine = nb.noise(22 * scale, 4, .7, vec=vec)
    pits = nb.voronoi(16 * scale, 'F1', vec=vec)
    col = nb.mixc(nb.maprange(n, .3, .72, 0, .5), tone, nb.mixc(1.0, tone, (.6, .6, .6, 1), 'MULTIPLY'))
    col = nb.mixc(nb.maprange(fine, .35, .7, 0, .2), col, nb.mixc(1.0, tone, (1.25, 1.2, 1.15, 1), 'MULTIPLY'))
    pit = nb.maprange(pits, 0, .12, .5, 0)
    col = nb.mixc(pit, col, scale_rgb(base, .35))
    crk = nb.maprange(nb.voronoi(1.6 * scale, 'DISTANCE_TO_EDGE', vec=vec), 0, .012, 1, 0)
    crk = nb.math('MULTIPLY', crk, nb.maprange(nb.noise(1.3 * scale, 3, .5, vec=vec), .52, .6))
    col = nb.mixc(nb.math('MULTIPLY', crk, crack), col, srgb('17130f'))
    col = nb.finish_color(col, .42, .22, .12, scale_rgb(base, edge), .85, .03)
    if grime:
        gm = nb.maprange(_rest_z(nb), 0.0, grime_height, grime, 0)
        gm = nb.math('MULTIPLY', gm, nb.maprange(n, .2, .6, .6, 1.0))
        col = nb.mixc(gm, col, srgb(grime_color))
    if moss:
        up = nb.maprange(nb.normal_z(), .25, .8)
        mm = nb.math('MULTIPLY', nb.math('MULTIPLY', up, nb.maprange(nb.noise(5 * scale, 4, .6), .45, .6)), moss)
        col = nb.mixc(mm, col, srgb('4b5a2e'))
    if blight:
        bl = nb.maprange(nb.noise(2.2 * scale, 5, .7, distortion=.6), .5, .66, 0, blight)
        bl = nb.math('MULTIPLY', bl, nb.maprange(_rest_z(nb), 2.6, .2, .25, 1))
        col = nb.mixc(bl, col, srgb('1f2a17'))
    h = nb.math('ADD', nb.math('MULTIPLY', n, .6), nb.math('MULTIPLY', fine, .25))
    h = nb.math('SUBTRACT', h, nb.math('MULTIPLY', nb.math('ADD', pit, nb.math('MULTIPLY', crk, crack)), .5))
    kw = {'Base Color': col, 'Roughness': nb.math('ADD', rough, nb.maprange(fine, .3, .7, -.08, .05)),
          'Normal': nb.bump(h, .45, .02)}
    if crack_glow:
        gl = nb.math('MULTIPLY', crk, nb.maprange(nb.noise(.9 * scale, 2, .5, vec=vec), .6, .645))
        kw['Base Color'] = nb.mixc(gl, col, srgb(crack_glow))
        kw['Emission Color'] = srgb(crack_glow)
        kw['Emission Strength'] = nb.math('MULTIPLY', gl, glow_strength)
    nb.principled(**kw)
    return m


@S.cached
def plate(color='2c343a', name='Riveted plate', var=0.18, rough=0.42, edge=2.1, wear=0.0, scale=1.0, tint='',
          enamel='', metallic=1.0):
    """Forged plates; per-plate (var) temper colour and roughness drift."""
    m, nb = S._new(name)
    base = srgb(color)
    vv = _attr(nb)
    vec = _offset(nb, vv, (5.3, 2.9, 4.1))
    col = nb.hsv(base, nb.maprange(vv, 0, 1, .48, .52), 1.0, nb.maprange(vv, 0, 1, 1 - var, 1 + var))
    if tint:
        col = nb.mixc(nb.maprange(nb.noise(2 * scale, 3, .5, vec=vec), .45, .7, 0, .5), col, srgb(tint))
    n = nb.noise(6 * scale, 4, .6, vec=vec)
    col = nb.mixc(nb.maprange(n, .3, .7, 0, .25), col, scale_rgb(base, .7))
    met = metallic
    if wear:
        rust = nb.maprange(nb.noise(3 * scale, 6, .7, distortion=.4, vec=vec), .55, .66, 0, wear)
        col = nb.mixc(rust, col, srgb('4e3322'))
        met = nb.math('MULTIPLY', nb.math('SUBTRACT', 1.0, rust), metallic)
    col = nb.finish_color(col, .4, .1, .08, scale_rgb(base, edge), .95, .012)
    ham = nb.voronoi(22 * scale, 'SMOOTH_F1', vec=vec)
    r = nb.math('ADD', rough, nb.maprange(n, .3, .7, -.1, .12))
    nb.principled(**{'Base Color': col, 'Metallic': met, 'Roughness': r,
                     'Normal': nb.bump(nb.math('ADD', ham, nb.math('MULTIPLY', n, .5)), .12, .01)})
    return m


@S.cached
def enamel(color, name='Enamel panel', under='6b4a2e', chip=0.3, rough=0.4, var=0.08, gloss=0.25):
    """Painted boards/panels: satin paint, var tone shift, chips back to the substrate."""
    m, nb = S._new(name)
    base = srgb(color)
    vv = _attr(nb)
    vec = _offset(nb, vv, (2.3, 5.1, 3.7))
    col = nb.hsv(base, .5, 1.0, nb.maprange(vv, 0, 1, 1 - var, 1 + var))
    col = nb.mixc(nb.maprange(nb.noise(4, 3, .5, vec=vec), .3, .7, 0, .15), col, scale_rgb(base, .8))
    edgem = nb.edge(.016, .02, .14)
    chips = nb.maprange(nb.noise(16, 5, .7, vec=vec), .45, .62)
    mask = nb.math('MULTIPLY', nb.math('MULTIPLY', edgem, chips), chip * 2.5, clamp=True)
    col = nb.finish_color(col, .55, .1, .1)
    col = nb.mixc(mask, col, srgb(under))
    nb.principled(**{'Base Color': col, 'Roughness': nb.mixf(mask, rough, .7), 'Coat Weight': gloss,
                     'Coat Roughness': .25, 'Normal': nb.bump(nb.noise(30, 2, .5, vec=vec), .04, .01)})
    return m


@S.cached
def verdigris(color='2d6f66', copper='8a5a34', name='Verdigris copper', amount=0.75):
    """Weathered copper roofing: teal patina in the open, warm copper on worn edges."""
    m, nb = S._new(name)
    vv = _attr(nb)
    vec = _offset(nb, vv, (2.1, 3.3, 1.7))
    n = nb.noise(5, 5, .65, vec=vec)
    pat = srgb(color)
    col = nb.mixc(nb.maprange(n, .3, .7), scale_rgb(pat, .7), scale_rgb(pat, 1.15))
    streak = nb.noise(3, 4, .6, vec=nb.scaled((7, 7, .8)))
    col = nb.mixc(nb.maprange(streak, .45, .7, 0, .25), col, srgb('7fae9e'))
    edge = nb.math('MULTIPLY', nb.edge(.015, .02, .16), 1.0 - amount + .6, clamp=True)
    col = nb.mixc(edge, col, srgb(copper))
    col = nb.finish_color(col, .5, .1, .1)
    nb.principled(**{'Base Color': col, 'Metallic': nb.mixf(edge, .15, 1.0), 'Roughness': nb.mixf(edge, .7, .35),
                     'Normal': nb.bump(n, .2, .01)})
    return m


@S.cached
def flame(color='ffb04a', core='fff2c8', strength=14.0, name='Flame'):
    """Emission-only fire: bright core where the surface faces the camera, coloured rims."""
    m, nb = S._new(name)
    lw = nb.node('ShaderNodeLayerWeight')
    lw.inputs['Blend'].default_value = .45
    fac = lw.outputs['Facing']
    col = nb.mixc(nb.maprange(fac, .1, .75), srgb(core), srgb(color))
    em = nb.node('ShaderNodeEmission')
    nb.feed(em.inputs['Color'], col)
    nb.feed(em.inputs['Strength'], nb.math('MULTIPLY', strength, nb.maprange(fac, 0, 1, 1.4, .55)))
    tr = nb.node('ShaderNodeBsdfTransparent')
    mix = nb.node('ShaderNodeMixShader')
    nb.feed(mix.inputs[0], nb.maprange(fac, .55, .95, 1, .35))
    nb.links.new(tr.outputs[0], mix.inputs[1])
    nb.links.new(em.outputs[0], mix.inputs[2])
    nb.links.new(mix.outputs[0], nb.out.inputs['Surface'])
    return m


@S.cached
def soot(name='Soot', color='120f0d'):
    m, nb = S._new(name)
    n = nb.noise(8, 4, .6)
    col = nb.mixc(nb.maprange(n, .3, .7), srgb(color), scale_rgb(srgb(color), 2.2))
    nb.principled(**{'Base Color': col, 'Roughness': .95, 'Normal': nb.bump(n, .4, .02)})
    return m


@S.cached
def coals(glow='ff6a1a', name='Embers', strength=8.0):
    m, nb = S._new(name)
    v = nb.voronoi(18, 'F1')
    hot = nb.maprange(v, .05, .32, 1, 0)
    hot = nb.math('MULTIPLY', hot, nb.maprange(nb.noise(5, 3, .5), .35, .65, .3, 1))
    col = nb.mixc(hot, srgb('1a1412'), srgb(glow))
    nb.principled(**{'Base Color': col, 'Roughness': .8, 'Emission Color': srgb(glow),
                     'Emission Strength': nb.math('MULTIPLY', hot, strength),
                     'Normal': nb.bump(v, .5, .02)})
    return m


@S.cached
def ice(color='a8e8ff', name='Frost crystal', glow=2.0, core='e8fbff'):
    m, nb = S._new(name)
    c = srgb(color)
    lw = nb.node('ShaderNodeLayerWeight')
    lw.inputs['Blend'].default_value = .4
    col = nb.mixc(nb.maprange(lw.outputs['Fresnel'], 0, .7), c, srgb(core))
    n = nb.noise(6, 4, .6)
    nb.principled(**{'Base Color': col, 'Transmission Weight': .55, 'IOR': 1.31, 'Roughness': nb.maprange(n, .3, .7, .02, .2),
                     'Emission Color': c, 'Emission Strength': nb.math('MULTIPLY', glow, nb.maprange(lw.outputs['Facing'], 0, 1, .4, 1.4)),
                     'Coat Weight': .5, 'Coat Roughness': .05, 'Normal': nb.bump(n, .2, .01)})
    return m


@S.cached
def frost_rime(color='d8f2ff', name='Rime', base='5f7e8c'):
    m, nb = S._new(name)
    n = nb.noise(14, 5, .7)
    col = nb.mixc(nb.maprange(n, .35, .65), srgb(base), srgb(color))
    col = nb.finish_color(col, .6, .08, .15)
    nb.principled(**{'Base Color': col, 'Roughness': .45, 'Subsurface Weight': .3, 'Subsurface Radius': (.5, .7, 1.0),
                     'Subsurface Scale': .03, 'Coat Weight': .4, 'Normal': nb.bump(n, .5, .01)})
    return m


# ================================================================== props
def _rot_y_axis():
    """Matrix taking local Z to world -Y (faces toward the battle camera)."""
    return Matrix.Rotation(math.radians(90), 4, 'X')


def spoked_wheel(name, coll, center, radius, mats, spokes=12, width=0.22, dish=0.06, side=-1, studs=True,
                 hub_spike=True, rim_mat=None, spoke_mat=None, hub_mat=None, cap_mat=None, detail=True, seed=1,
                 bone=False, plate_disc=None):
    """Iron-shod spoked war wheel in the XZ plane, axle along Y. side=-1: outer face toward -Y."""
    c = Vector(center)
    B = Batch(name, coll, random.Random(seed))
    iron = mats['iron']
    oak = spoke_mat or mats['wood']
    rim = rim_mat or mats['wood']
    R = radius
    t_out, t_in = R, R - 0.07
    f_out, f_in = R - 0.07, R * 0.74
    # iron tyre with a slight crown
    B.ring(c, t_in, t_out, width + 0.02, iron, 'Y', 64, var=0.5,
           profile=[(t_in, -width / 2 - .01), (t_out - .012, -width / 2 - .01), (t_out, -width / 2 + .02),
                    (t_out, width / 2 - .02), (t_out - .012, width / 2 + .01), (t_in, width / 2 + .01)])
    # felloes in 6 arcs, each its own board
    nf = 6
    gap = 0.012
    for i in range(nf):
        a0 = math.tau * i / nf + gap
        a1 = math.tau * (i + 1) / nf - gap
        B.ring(c, f_in, f_out, width, rim, 'Y', 12, a0, a1)
    # felloe joint clamps
    if detail:
        for i in range(nf):
            a = math.tau * i / nf
            p = c + V(math.cos(a) * (f_in + f_out) / 2, side * (width / 2 + 0.008), math.sin(a) * (f_in + f_out) / 2)
            B.box((0.06, 0.02, (f_out - f_in) * 1.05), p, iron, rot=(0, -math.degrees(a) + 90, 0))
    # spokes, dished: hub sits inboard of the rim
    hub_r = R * 0.2
    for i in range(spokes):
        a = math.tau * (i + 0.5) / spokes
        d = V(math.cos(a), 0, math.sin(a))
        p0 = c + d * (hub_r * 0.8) + V(0, -side * dish, 0)
        p1 = c + d * (f_in + 0.03)
        B.wedge_box(p0, p1, 0.075, 0.05, 0.085, 0.06, oak, up=(0, 1, 0))
    # nave (hub) along Y
    Mh = Matrix.Translation(c + V(0, -side * dish, 0)) @ Matrix.Rotation(math.radians(90), 4, 'X') @ \
        Matrix.Diagonal((1, 1, -side, 1))
    prof = [(0, -0.24), (hub_r * .55, -0.24), (hub_r * .8, -0.2), (hub_r, -0.1), (hub_r * 1.05, 0.0), (hub_r, 0.12),
            (hub_r * .8, 0.24), (0, 0.24)]
    B.lathe(prof, hub_mat or mats['wood_dark'], Mh, 20)
    for z, rr in ((-0.16, hub_r * .93), (0.09, hub_r * 1.04), (0.18, hub_r * .9)):
        B.lathe([(rr * .96, z - .022), (rr + .018, z - .02), (rr + .018, z + .02), (rr * .96, z + .022)], iron, Mh, 20)
    # outer cap and spike
    cap = cap_mat or mats['brass']
    B.lathe([(0, 0.24), (hub_r * .78, 0.24), (hub_r * .8, 0.27), (hub_r * .6, 0.31), (0, 0.32)], cap, Mh, 20)
    if hub_spike:
        B.lathe([(hub_r * .42, 0.31), (hub_r * .34, 0.36), (0, 0.52)], iron, Mh, 12)
    obj = B.finish(bevel=0.008, segments=2)
    out = [obj]
    if studs:
        pts = []
        for i in range(28):
            a = math.tau * (i + .5) / 28
            pts.append((c + V(math.cos(a) * (t_in + t_out) / 2, side * (width / 2 + 0.012), math.sin(a) * (t_in + t_out) / 2),
                        V(0, side, 0)))
        for i in range(spokes):
            a = math.tau * (i + 0.5) / spokes
            pts.append((c + V(math.cos(a) * (f_in + 0.05), side * (width / 2 + 0.004), math.sin(a) * (f_in + 0.05)), V(0, side, 0)))
        out.append(geo.rivets(name + ' studs', pts, 0.018, mats.get('stud', iron), coll))
    return out


def lantern(name, coll, base, mats, size=1.0, glow=None, light=None, light_power=12.0, cage=None, hook=True,
            sides=6, glass=None):
    """Hexagonal hanging lantern; `base` is the bottom centre. Returns (objs, flame_centre)."""
    s = size
    b = Vector(base)
    cage = cage or mats['brass']
    B = Batch(name, coll, random.Random(7))
    M = Matrix.Translation(b)
    h = 0.26 * s
    r = 0.085 * s
    B.lathe([(0, 0), (r * .7, 0.0), (r * 1.15, 0.02 * s), (r * 1.2, 0.045 * s), (r * .95, 0.055 * s), (0, 0.055 * s)], cage, M, sides)
    B.lathe([(0, -0.035 * s), (r * .35, -0.03 * s), (r * .55, 0.0)], cage, M, sides, cap_top=False)
    top = 0.055 * s + h
    B.lathe([(0, top - .01 * s), (r * 1.3, top), (r * 1.25, top + .02 * s), (r * .5, top + .09 * s), (r * .25, top + .1 * s),
             (0, top + .11 * s)], cage, M, sides)
    for i in range(sides):
        a = math.tau * (i + .5) / sides
        p0 = b + V(math.cos(a) * r * 1.05, math.sin(a) * r * 1.05, 0.05 * s)
        B.wedge_box(p0, p0 + V(0, 0, h + .01 * s), 0.016 * s, 0.016 * s, 0.016 * s, 0.016 * s, cage)
    if hook:
        B.torus(b + V(0, 0, top + .14 * s), 0.035 * s, 0.008 * s, cage, rot=(90, 0, 0), segs=12, sides=5)
    objs = [B.finish(bevel=0.003 * s, segments=1)]
    gl = geo.cylinder(name + ' glass', r * 1.0, h, b + V(0, 0, 0.055 * s + h / 2), glass or mats['lamp_glass'], coll, sides,
                      bevel=0)
    objs.append(gl)
    fc = b + V(0, 0, 0.055 * s + h * .42)
    fl = geo.lathe(name + ' flame', [(0, -0.05 * s), (0.028 * s, -0.03 * s), (0.032 * s, 0.0), (0.02 * s, 0.04 * s), (0, 0.09 * s)],
                   10, glow or mats['glow'], coll, location=fc)
    objs.append(fl)
    if light is not None:
        core.point_light(name + ' light', fc, light_power * s * s, light, 0.06 * s, coll)
    return objs, fc


def bracket(name, coll, wall_point, out_dir, length, mat, drop=0.12, thickness=0.025):
    """Wrought iron scroll bracket from a wall; returns the hanging point."""
    p0 = Vector(wall_point)
    d = Vector(out_dir).normalized()
    pts = [p0, p0 + d * length * .5 + V(0, 0, .06), p0 + d * length + V(0, 0, .02), p0 + d * length + V(0, 0, -drop * .3)]
    objs = [geo.tube(name, catmull(pts, 5), thickness, mat, coll, sides=6)]
    brace = [p0 + V(0, 0, -length * .55), p0 + d * length * .45 + V(0, 0, -length * .1), p0 + d * length * .62 + V(0, 0, .05)]
    objs.append(geo.tube(name + ' brace', catmull(brace, 4), thickness * .7, mat, coll, sides=6))
    curl = [p0 + d * length * .5 + V(0, 0, -.05)]
    for i in range(9):
        a = i / 8 * math.tau * .9
        rr = .06 * (1 - i / 10)
        curl.append(p0 + d * (length * .3 + math.cos(a) * rr) + V(0, 0, -.06 + math.sin(a) * rr))
    objs.append(geo.tube(name + ' scroll', curl, thickness * .55, mat, coll, sides=5))
    return objs, p0 + d * length + V(0, 0, -drop * .3)


def barrel(B, center, radius, height, wood, iron, axis='Z', rot=(0, 0, 0), var=None, staves=16, lid=None):
    """Bulged barrel with hoops into a batch. center: middle of the barrel."""
    M = mat4(center, rot)
    if axis == 'X':
        M = M @ Matrix.Rotation(math.radians(90), 4, 'Y')
    elif axis == 'Y':
        M = M @ Matrix.Rotation(math.radians(90), 4, 'X')
    h = height / 2
    prof = [(radius * .86, -h), (radius * .96, -h * .6), (radius, 0), (radius * .96, h * .6), (radius * .86, h)]
    vv = B.rng.random() if var is None else var
    for i in range(staves):
        a0 = math.tau * i / staves + .006
        a1 = math.tau * (i + 1) / staves - .006
        vs = []
        rings = []
        for r, z in prof:
            rings.append([B.bm.verts.new(M @ V(r * math.cos(a), r * math.sin(a), z)) for a in (a0, (a0 + a1) / 2, a1)])
        inner = []
        for r, z in prof:
            inner.append([B.bm.verts.new(M @ V(r * .9 * math.cos(a), r * .9 * math.sin(a), z)) for a in (a0, a1)])
        for ra, rb in zip(rings, rings[1:]):
            for j in range(2):
                B.bm.faces.new((ra[j], ra[j + 1], rb[j + 1], rb[j]))
        for ia, ib in zip(inner, inner[1:]):
            B.bm.faces.new((ia[1], ia[0], ib[0], ib[1]))
        # stave sides and ends
        for ra, rb, ia, ib in zip(rings, rings[1:], inner, inner[1:]):
            B.bm.faces.new((ra[0], rb[0], ib[0], ia[0]))
            B.bm.faces.new((ia[1], ib[1], rb[2], ra[2]))
        for ring, inn, flip in ((rings[0], inner[0], False), (rings[-1], inner[-1], True)):
            q = [ring[0], ring[1], ring[2], inn[1], inn[0]]
            B.bm.faces.new(q if flip else list(reversed(q)))
        for row in rings + inner:
            vs += row
        B._tag(vs, wood, (vv + i * .137) % 1.0)
    for z, rr in ((-h * .78, .93), (-h * .45, .985), (h * .45, .985), (h * .78, .93)):
        B.lathe([(radius * rr + .004, z - .025), (radius * rr + .016, z - .022), (radius * rr + .016, z + .022),
                 (radius * rr + .004, z + .025)], iron, M, 24, var=vv, cap_bottom=False, cap_top=False)
    B.cyl(radius * .84, .02, (0, 0, 0), lid or wood, var=vv, segs=24, matrix=M @ Matrix.Translation((0, 0, h - .05)))
    B.cyl(radius * .84, .02, (0, 0, 0), lid or wood, var=vv, segs=24, matrix=M @ Matrix.Translation((0, 0, -h + .05)))


def crate(B, center, size, wood, iron=None, rot=(0, 0, 0), var=None):
    sx, sy, sz = size
    M = mat4(center, rot)
    vv = B.rng.random() if var is None else var
    B.box((sx - .04, sy - .04, sz - .04), (0, 0, 0), wood, var=vv, matrix=M)
    t = .045
    for dz in (-1, 1):
        for dy in (-1, 1):
            B.box((sx, t, t), (0, 0, 0), wood, var=(vv + .3) % 1, matrix=M @ Matrix.Translation((0, dy * (sy / 2 - t / 2), dz * (sz / 2 - t / 2))))
        for dx in (-1, 1):
            B.box((t, sy, t), (0, 0, 0), wood, var=(vv + .5) % 1, matrix=M @ Matrix.Translation((dx * (sx / 2 - t / 2), 0, dz * (sz / 2 - t / 2))))
    for dx in (-1, 1):
        for dy in (-1, 1):
            B.box((t, t, sz), (0, 0, 0), wood, var=(vv + .7) % 1, matrix=M @ Matrix.Translation((dx * (sx / 2 - t / 2), dy * (sy / 2 - t / 2), 0)))
    # diagonal brace on the faces
    for dy in (-1, 1):
        L = math.hypot(sx, sz) - .08
        ang = math.degrees(math.atan2(sz, sx))
        B.box((L, t * .8, t), (0, 0, 0), wood, var=(vv + .2) % 1,
              matrix=M @ Matrix.Translation((0, dy * (sy / 2 - .005), 0)) @ Matrix.Rotation(math.radians(-ang), 4, 'Y'))
    if iron is not None:
        for dx in (-1, 1):
            for dz in (-1, 1):
                B.box((.06, sy + .01, .06), (0, 0, 0), iron, var=vv,
                      matrix=M @ Matrix.Translation((dx * (sx / 2 - .02), 0, dz * (sz / 2 - .02))))


def chain(name, coll, p0, p1, mat, sag=0.15, link=0.045, wire=0.009):
    """Iron chain along a sagging curve between two points."""
    p0, p1 = Vector(p0), Vector(p1)
    L = (p1 - p0).length
    n = max(3, int(L / (link * 1.55)))
    pts = []
    for i in range(n + 1):
        t = i / n
        pts.append(p0.lerp(p1, t) + V(0, 0, -sag * 4 * t * (1 - t)))
    B = Batch(name, coll, random.Random(3))
    for i in range(n):
        a, b = pts[i], pts[i + 1]
        d = (b - a)
        if d.length < 1e-6:
            continue
        q = d.to_track_quat('X', 'Z')
        M = Matrix.Translation((a + b) / 2) @ q.to_matrix().to_4x4() @ Matrix.Rotation(math.radians(90 * (i % 2)), 4, 'X')
        B.torus((0, 0, 0), link * .5, wire, mat, scale=(1.45, 1, 1), segs=10, sides=5, matrix=M)
    return B.finish(bevel=0, weighted=False)


def rope_coil(name, coll, center, radius, mat, turns=4, thick=0.035):
    c = Vector(center)
    pts = []
    for i in range(turns * 16 + 1):
        a = i / 16 * math.tau
        rr = radius * (1 - 0.18 * (i / (turns * 16)))
        pts.append(c + V(math.cos(a) * rr, math.sin(a) * rr, thick * (i / 16) * .55))
    return geo.tube(name, pts, thick, mat, coll, sides=6)


def banner_sheet(name, coll, top_center, width, drop, mat, axis_dir=(1, 0, 0), normal=(0, -1, 0), wave=0.04,
                 tails=2, tail_depth=0.22, tatter=0.0, seed=1, nx=14, ny=18, thickness=0.012, sway=0.0, holes=0.0,
                 bulge=0.0, subsurf=1):
    """Hanging banner: top edge centred at top_center spanning `width` along axis_dir, hanging `drop`.
    tails: swallow-tail count (0 = straight/pointed bottom handled by tail_depth<0).
    tatter: ragged bottom edge; holes: fraction of interior faces removed (moth-eaten)."""
    rng = random.Random(seed)
    tc = Vector(top_center)
    ax = Vector(axis_dir).normalized()
    nrm = Vector(normal).normalized()
    rag = [rng.uniform(0, 1) for _ in range(nx + 1)]

    def fn(u, v):
        x = (u - .5) * width
        z = -v * drop
        if tails and v > 1 - tail_depth:
            k = (v - (1 - tail_depth)) / tail_depth
            ph = (u * tails) % 1.0
            z += k * drop * tail_depth * (1 - abs(ph - 0.5) * 2) * 1.0
        if tatter:
            z += tatter * drop * .12 * rag[int(round(u * nx))] * (v ** 3)
        y = wave * math.sin(u * 6.0 + v * 2.5 + seed) * (0.25 + v) + bulge * math.sin(math.pi * u) * (1 - v * .5)
        sw = sway * v * v
        p = tc + ax * (x + sw) + V(0, 0, z) + nrm * (-y)
        return tuple(p)
    bm = bmesh.new()
    verts = [[bm.verts.new(fn(i / nx, j / ny)) for i in range(nx + 1)] for j in range(ny + 1)]
    for j in range(ny):
        for i in range(nx):
            if holes and 1 < i < nx - 2 and 2 < j < ny - 1 and rng.random() < holes * (j / ny):
                continue
            bm.faces.new((verts[j][i], verts[j + 1][i], verts[j + 1][i + 1], verts[j][i + 1]))
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    me = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    obj = bpy.data.objects.new(name, me)
    coll.objects.link(obj)
    for p in me.polygons:
        p.use_smooth = True
    sol = obj.modifiers.new('Cloth thickness', 'SOLIDIFY')
    sol.thickness = thickness
    sol.offset = 0
    sol.use_even_offset = False
    if subsurf:
        sub = obj.modifiers.new('Soft drape', 'SUBSURF')
        sub.levels = subsurf
        sub.render_levels = subsurf
    geo.store_rest(obj)
    return obj, fn


def flame_tongues(name, coll, base, height, radius, mat, seed=1, count=5, core_mat=None, lean=(0, 0, 0)):
    """Stylised fire: several twisted teardrop tongues around a hot core."""
    rng = random.Random(seed)
    b = Vector(base)
    objs = []
    for k in range(count):
        a = math.tau * k / count + rng.uniform(-.3, .3)
        off = V(math.cos(a), math.sin(a), 0) * radius * rng.uniform(.25, .55)
        h = height * rng.uniform(.55, 1.0) * (1.0 if k else 1.15)
        r = radius * rng.uniform(.38, .55)
        pts = []
        n = 7
        for i in range(n + 1):
            t = i / n
            sway = V(math.sin(t * 3 + k) * .12, math.cos(t * 2.5 + k * 1.7) * .08, 0) * h * t + Vector(lean) * h * t * t
            pts.append(b + off * (1 - t * .7) + V(0, 0, t * h) + sway)
        radii = [r * (0.55 + 1.6 * t) * (1 - t) ** 1.35 + .004 for t in (i / n for i in range(n + 1))]
        objs.append(geo.tube(f'{name} tongue', pts, radii, mat, coll, sides=8))
    if core_mat is not None:
        objs.append(geo.sphere(name + ' core', radius * .5, b + V(0, 0, radius * .35), core_mat, coll, 12, 8,
                               scale=(1, 1, 1.3)))
    return objs


def brazier(name, coll, base, mats, flame_mat, coal_mat, light=None, size=1.0, legs=3, height=0.9, bowl=0.28,
            light_power=60.0, seed=1, flame_h=None):
    """Iron tripod brazier: base at ground; returns objs and flame base point."""
    s = size
    b = Vector(base)
    B = Batch(name, coll, random.Random(seed))
    iron = mats['iron']
    top = b + V(0, 0, height * s)
    r = bowl * s
    M = Matrix.Translation(top)
    B.lathe([(r * .35, -r * .55), (r * .7, -r * .42), (r * .95, -r * .12), (r * 1.08, .02 * s), (r * 1.0, .03 * s),
             (r * .9, -r * .08), (r * .62, -r * .36), (r * .3, -r * .48)], iron, M, 20, cap_top=False)
    for i in range(8):
        a = math.tau * i / 8
        B.wedge_box(top + V(math.cos(a) * r * 1.02, math.sin(a) * r * 1.02, 0.0),
                    top + V(math.cos(a) * r * 1.18, math.sin(a) * r * 1.18, 0.12 * s), .03 * s, .012 * s, .02 * s, .01 * s, iron)
    for i in range(legs):
        a = math.tau * i / legs + .5
        foot = b + V(math.cos(a) * r * 1.25, math.sin(a) * r * 1.25, 0)
        knee = b + V(math.cos(a) * r * .55, math.sin(a) * r * .55, height * s * .45)
        hip = top + V(math.cos(a) * r * .55, math.sin(a) * r * .55, -r * .35)
        B.wedge_box(foot, knee, .035 * s, .03 * s, .035 * s, .03 * s, iron)
        B.wedge_box(knee, hip, .03 * s, .03 * s, .03 * s, .03 * s, iron)
        B.sphere(.03 * s, foot + V(0, 0, .02 * s), iron)
    B.torus(b + V(0, 0, height * s * .45), r * .55, .012 * s, iron, segs=20, sides=5)
    objs = [B.finish(bevel=.004 * s, segments=1)]
    coal = geo.sphere(name + ' coals', r * .92, top + V(0, 0, -r * .05), coal_mat, coll, 18, 8, scale=(1, 1, .32))
    geo.displace_noise(coal, .02 * s, 12, seed)
    objs.append(coal)
    fb = top + V(0, 0, 0.0)
    objs += flame_tongues(name + ' fire', coll, fb, (flame_h or .55) * s, r * .9, flame_mat, seed, count=6)
    if light is not None:
        core.point_light(name + ' light', fb + V(0, 0, .25 * s), light_power * s * s, light, .15 * s, coll)
    return objs, fb


def oriented_skull(name, coll, center, R, mats, facing=(0, -1, 0), tilt=0.0, roll=0.0, jaw_open=0.2, horns=None):
    """Kit skull (faces +X natively) re-aimed to `facing`."""
    objs = skull_head(V(0, 0, 0), R, mats, coll, jaw_open=jaw_open, eye_glow=True, horns=horns)
    f = Vector(facing).normalized()
    yaw = math.atan2(f.y, f.x)
    M = Matrix.Translation(Vector(center)) @ Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Rotation(math.radians(tilt), 4, 'Y') @ \
        Matrix.Rotation(math.radians(roll), 4, 'X')
    for o in objs:
        geo.transform(o, M)
    return objs


def stone_course_ring(B, center, r, z0, height, mat, count=None, depth=0.22, gap=0.012, offset=0.0, jitter=0.012,
                      a0=0.0, a1=math.tau, batter=0.0):
    """One course of curved ashlar blocks around a round tower (Z axis)."""
    c = Vector(center)
    span = a1 - a0
    if count is None:
        count = max(3, int(r * span / 0.42))
    for i in range(count):
        jit = B.rng.uniform(-.18, .18) / count * span
        aa = a0 + span * (i + offset) / count + (jit if 0 < i < count else 0)
        ab = a0 + span * (i + 1 + offset) / count
        if aa >= a1 - 1e-4:
            continue
        ab = min(ab, a1)
        dr = B.rng.uniform(-jitter, jitter)
        rr = r + dr
        ga = gap / max(rr, .1)
        segs = max(2, int((ab - aa) / .12))
        prof_h = height - gap
        zz = z0 + gap / 2
        ri, ro = rr - depth, rr
        prof = [(ri, zz), (ro - batter, zz), (ro, zz + prof_h * .0), (ro, zz + prof_h), (ri, zz + prof_h)]
        prof = [(ri, zz), (ro, zz), (ro - batter, zz + prof_h), (ri, zz + prof_h)]
        rings = []
        for k in range(segs + 1):
            a = aa + ga / 2 + (ab - aa - ga) * k / segs
            ca, sa = math.cos(a), math.sin(a)
            rings.append([B.bm.verts.new(c + V(rad * ca, rad * sa, z)) for rad, z in prof])
        for ra, rb in zip(rings, rings[1:]):
            for j in range(4):
                jj = (j + 1) % 4
                B.bm.faces.new((ra[j], ra[jj], rb[jj], rb[j]))
        B.bm.faces.new(rings[0])
        B.bm.faces.new(list(reversed(rings[-1])))
        B._tag([v for rr_ in rings for v in rr_], mat)


def ashlar_wall(B, x0, x1, z0, z1, y_face, mat, course=0.3, block=(0.38, 0.62), depth=0.25, gap=0.014, jitter=0.012,
                skip=None, tilt=0.0, normal=-1, rng=None, along='X', start_offset=None):
    """Running-bond ashlar face between x0..x1, z0..z1 at the plane y=y_face (normal toward -Y by default).
    skip(xa, xb, za, zb) -> True to omit a block (openings). along='Y' builds on an X-facing wall."""
    rng = rng or B.rng
    z = z0
    row = 0
    while z < z1 - 1e-4:
        h = min(course * rng.uniform(.88, 1.12), z1 - z)
        if z1 - (z + h) < course * .35:
            h = z1 - z
        x = x0 - (rng.uniform(0, block[0]) if start_offset is None else start_offset * (row % 2))
        while x < x1 - 1e-4:
            w = rng.uniform(*block)
            xa, xb = max(x, x0), min(x + w, x1)
            if x1 - xb < block[0] * .4:
                xb = x1
            if xb - xa > .05 and not (skip and skip(xa, xb, z, z + h)):
                dd = rng.uniform(-jitter, jitter)
                size = (xb - xa - gap, depth, h - gap)
                cx, cz = (xa + xb) / 2, z + h / 2
                cy = y_face - normal * (depth / 2) + normal * dd
                rot = (rng.uniform(-tilt, tilt), rng.uniform(-tilt, tilt), rng.uniform(-tilt, tilt))
                if along == 'X':
                    B.box(size, (cx, cy, cz), mat, rot=rot)
                else:
                    B.box((size[1], size[0], size[2]), (cy, cx, cz), mat, rot=rot)
            x = xb
        z += h
        row += 1
