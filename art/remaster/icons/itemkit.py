"""Collectible props, gems and magical effects for the item icons.

Lives inside the icons package (not rk/) so it never collides with other remaster work.
Geometry follows the rk convention: authored in world space with the object at the
origin and a `rest` attribute on every mesh. Effect meshes also carry two float point
attributes read by the `fx` materials:
  fx  - 0..1 along the effect (drives colour ramp / fade-out)
  fxw - 0..1 across the effect width (0.5 = centre; edges fade)
"""
import math
import random

import bmesh
from mathutils import Matrix, Vector, noise

from rk import geo
from rk import shaders as S
from rk.core import srgb, scale_rgb


def V(*a):
    return Vector(a)


# ===================================================================== attributes
def set_point_attr(obj, name, values):
    me = obj.data
    if name in me.attributes:
        me.attributes.remove(me.attributes[name])
    attr = me.attributes.new(name, 'FLOAT', 'POINT')
    attr.data.foreach_set('value', [float(v) for v in values])
    return obj


def fx_attrs(obj, fn):
    """fn(co: Vector) -> (fx, fxw) evaluated on current vertex positions."""
    a, b = [], []
    for v in obj.data.vertices:
        x, y = fn(v.co.copy())
        a.append(x)
        b.append(y)
    set_point_attr(obj, 'fx', a)
    set_point_attr(obj, 'fxw', b)
    return obj


def nofit(obj):
    obj['nofit'] = True
    return obj


# ===================================================================== materials
def _attr(nb, name):
    n = nb.node('ShaderNodeAttribute', attribute_type='GEOMETRY', attribute_name=name)
    return n.outputs['Fac']


def _ramp_node(nb, fac, stops, interp='LINEAR'):
    n = nb.node('ShaderNodeValToRGB')
    n.color_ramp.interpolation = interp
    els = n.color_ramp.elements
    while len(els) < len(stops):
        els.new(0.5)
    # assign in sorted order so re-sorting never moves an element under us
    for el, (pos, col) in zip(els, sorted(stops, key=lambda s: s[0])):
        el.position = pos
        el.color = col
    nb.feed(n.inputs['Fac'], fac)
    return n


def _stops(stops):
    out = []
    for s in stops:
        pos, hexc = s[0], s[1]
        alpha = s[2] if len(s) > 2 else 1.0
        out.append((pos, srgb(hexc, alpha)))
    return out


def _emit_mix(nb, alpha, color, strength):
    em = nb.node('ShaderNodeEmission')
    nb.feed(em.inputs['Color'], color)
    nb.feed(em.inputs['Strength'], strength)
    tr = nb.node('ShaderNodeBsdfTransparent')
    mix = nb.node('ShaderNodeMixShader')
    nb.feed(mix.inputs[0], alpha)
    nb.links.new(tr.outputs[0], mix.inputs[1])
    nb.links.new(em.outputs[0], mix.inputs[2])
    nb.links.new(mix.outputs[0], nb.out.inputs['Surface'])
    return mix


@S.cached
def fx(stops, strength=6.0, name='FX', soft=0.8, opacity=1.0, width_pow=1.6, noise_amt=0.0, noise_scale=5.0,
       noise_alpha=0.0):
    """Emissive effect with alpha. stops: ((pos, hex[, alpha]), ...) along `fx`.
    Alpha = ramp alpha * width fade (fxw) * facing softness * opacity."""
    m, nb = S._new(name)
    t = _attr(nb, 'fx')
    if noise_amt:
        n = nb.noise(noise_scale, 4, .6)
        t = nb.math('ADD', t, nb.math('MULTIPLY', nb.math('SUBTRACT', n, .5), noise_amt))
    r = _ramp_node(nb, t, _stops(stops))
    w = _attr(nb, 'fxw')
    edge = nb.math('ABSOLUTE', nb.math('SUBTRACT', nb.math('MULTIPLY', w, 2.0), 1.0))
    wf = nb.math('SUBTRACT', 1.0, nb.math('POWER', edge, width_pow), clamp=True)
    alpha = nb.math('MULTIPLY', r.outputs['Alpha'], wf)
    if soft:
        lw = nb.node('ShaderNodeLayerWeight')
        lw.inputs['Blend'].default_value = 0.5
        f = nb.math('POWER', nb.math('SUBTRACT', 1.0, lw.outputs['Facing'], clamp=True), soft)
        alpha = nb.math('MULTIPLY', alpha, f)
    if noise_alpha:
        n2 = nb.noise(noise_scale * 1.7, 3, .6)
        alpha = nb.math('MULTIPLY', alpha, nb.maprange(n2, .5 - noise_alpha * .5, .5 + noise_alpha * .3, 0, 1))
    alpha = nb.math('MULTIPLY', alpha, opacity, clamp=True)
    _emit_mix(nb, alpha, r.outputs['Color'], strength)
    return m


@S.cached
def halo(color, strength=2.0, power=2.2, opacity=0.6, core='', name='Halo'):
    """Soft glow shell: brightest where the surface faces the camera, clear at the silhouette."""
    m, nb = S._new(name)
    lw = nb.node('ShaderNodeLayerWeight')
    lw.inputs['Blend'].default_value = 0.5
    face = nb.math('SUBTRACT', 1.0, lw.outputs['Facing'], clamp=True)
    alpha = nb.math('MULTIPLY', nb.math('POWER', face, power), opacity, clamp=True)
    c = srgb(color)
    hot = srgb(core) if core else scale_rgb(c, 1.4)
    col = nb.mixc(nb.math('POWER', face, power * 2.5), c, hot)
    _emit_mix(nb, alpha, col, strength)
    return m


@S.cached
def glow_solid(color, strength=6.0, core='', name='Glow core'):
    """Opaque emissive surface, hotter (whiter) where it faces the camera."""
    m, nb = S._new(name)
    lw = nb.node('ShaderNodeLayerWeight')
    lw.inputs['Blend'].default_value = 0.4
    face = nb.math('SUBTRACT', 1.0, lw.outputs['Facing'], clamp=True)
    c = srgb(color)
    hot = srgb(core) if core else scale_rgb(c, 1.6)
    col = nb.mixc(nb.math('POWER', face, 1.5), c, hot)
    em = nb.node('ShaderNodeEmission')
    nb.feed(em.inputs['Color'], col)
    em.inputs['Strength'].default_value = strength
    nb.links.new(em.outputs[0], nb.out.inputs['Surface'])
    return m


@S.cached
def ice(color='cdf3ff', deep='2c86b8', glow='7bdff2', strength=1.4, name='Ice', crack=0.6, rough=0.08):
    m, nb = S._new(name)
    lw = nb.node('ShaderNodeLayerWeight')
    lw.inputs['Blend'].default_value = 0.35
    fres = lw.outputs['Facing']
    base = nb.mixc(nb.math('POWER', fres, 1.4), srgb(deep), srgb(color))
    cr = nb.maprange(nb.voronoi(5.5, 'DISTANCE_TO_EDGE'), 0.0, 0.03, 1, 0)
    cr = nb.math('MULTIPLY', cr, nb.maprange(nb.noise(3, 3, .5), .4, .6))
    base = nb.mixc(nb.math('MULTIPLY', cr, crack), base, (1, 1, 1, 1))
    frost = nb.maprange(nb.noise(9, 5, .7), .45, .75, 0, 1)
    em_fac = nb.math('ADD', nb.math('MULTIPLY', fres, .8), nb.math('MULTIPLY', cr, crack))
    nb.principled(**{'Base Color': base, 'Transmission Weight': .55, 'IOR': 1.31,
                     'Roughness': nb.math('ADD', rough, nb.math('MULTIPLY', frost, .18)),
                     'Coat Weight': .8, 'Coat Roughness': .03, 'Specular IOR Level': .7,
                     'Emission Color': srgb(glow), 'Emission Strength': nb.math('MULTIPLY', em_fac, strength),
                     'Normal': nb.bump(nb.math('ADD', frost, cr), .12, .01)})
    return m


@S.cached
def crystal(color, deep='', glow=2.0, name='Crystal', transmission=0.35, core='', inner=0.6, coat=0.6):
    """Faceted magical crystal: saturated body, hot inner core, bright facet rims."""
    m, nb = S._new(name)
    c = srgb(color)
    d = srgb(deep) if deep else scale_rgb(c, .18)
    hot = srgb(core) if core else scale_rgb(c, 1.8)
    lw = nb.node('ShaderNodeLayerWeight')
    lw.inputs['Blend'].default_value = 0.3
    face = nb.math('SUBTRACT', 1.0, lw.outputs['Facing'], clamp=True)
    base = nb.mixc(nb.math('POWER', face, 1.2), d, c)
    em_col = nb.mixc(nb.math('POWER', face, 3.0), c, hot)
    em = nb.math('MULTIPLY', glow, nb.math('ADD', nb.math('MULTIPLY', nb.math('POWER', face, 2.0), inner), .25))
    nb.principled(**{'Base Color': base, 'Transmission Weight': transmission, 'IOR': 1.75, 'Roughness': .05,
                     'Coat Weight': coat, 'Coat Roughness': .02, 'Specular IOR Level': .7,
                     'Emission Color': em_col, 'Emission Strength': em})
    return m


@S.cached
def obsidian(color='15111a', vein='ff2a3a', strength=7.0, name='Obsidian', crack=0.03, scale=5.0):
    m, nb = S._new(name)
    cr = nb.maprange(nb.voronoi(scale, 'DISTANCE_TO_EDGE'), 0.0, crack, 1, 0)
    cr = nb.math('MULTIPLY', cr, nb.maprange(nb.noise(2.5, 3, .5), .38, .58))
    lw = nb.node('ShaderNodeLayerWeight')
    lw.inputs['Blend'].default_value = .3
    base = nb.mixc(lw.outputs['Fresnel'], srgb(color), srgb('3b3346'))
    nb.principled(**{'Base Color': nb.mixc(cr, base, srgb(vein)), 'Roughness': .12, 'Metallic': .2,
                     'Coat Weight': 1.0, 'Coat Roughness': .04, 'Specular IOR Level': .8,
                     'Emission Color': srgb(vein), 'Emission Strength': nb.math('MULTIPLY', cr, strength),
                     'Normal': nb.bump(nb.math('SUBTRACT', 0, cr), .2, .01)})
    return m


@S.cached
def enamel(color, name='Enamel', rough=0.22, glow=0.0):
    """Glossy vitreous enamel inlay, deeper colour in the cavities."""
    m, nb = S._new(name)
    c = srgb(color)
    n = nb.noise(5, 3, .5)
    col = nb.mixc(nb.maprange(n, .3, .7, 0, .3), c, scale_rgb(c, .6))
    col = nb.finish_color(col, .5, .06, .08)
    nb.principled(**{'Base Color': col, 'Roughness': rough, 'Coat Weight': 1.0, 'Coat Roughness': .05,
                     'Specular IOR Level': .6, 'Emission Color': c, 'Emission Strength': glow})
    return m


@S.cached
def parchment(color='e3cf9f', name='Parchment', stain='9c7440'):
    m, nb = S._new(name)
    n = nb.noise(4, 5, .65)
    col = nb.mixc(nb.maprange(n, .35, .75, 0, .55), srgb(color), srgb(stain))
    edge = nb.maprange(nb.noise(12, 4, .7), .5, .7, 0, .3)
    col = nb.mixc(edge, col, srgb('7a5a2e'))
    col = nb.finish_color(col, .6, .08, .1)
    nb.principled(**{'Base Color': col, 'Roughness': .85, 'Subsurface Weight': .1,
                     'Subsurface Radius': (1, .8, .5), 'Subsurface Scale': .02,
                     'Normal': nb.bump(nb.noise(30, 4, .6), .08, .01)})
    return m


@S.cached
def masonry(color='8a8070', name='Masonry', dark=0.5, crack=0.5, scale=1.0, moss=0.0, edge=1.3, rough=0.85):
    """Dressed stone without hue noise: value variation, speckle, hairline cracks, worn light edges."""
    m, nb = S._new(name)
    base = srgb(color)
    n1 = nb.noise(2.2 * scale, 4, .6)
    col = nb.mixc(nb.maprange(n1, .3, .7), scale_rgb(base, .72), scale_rgb(base, 1.12))
    sp = nb.noise(28 * scale, 2, .5)
    col = nb.mixc(nb.maprange(sp, .35, .65, 0, .25), col, scale_rgb(base, .6))
    cr = nb.maprange(nb.voronoi(3.2 * scale, 'DISTANCE_TO_EDGE'), 0.0, 0.018, 1, 0)
    cr = nb.math('MULTIPLY', cr, nb.maprange(nb.noise(2.0 * scale, 3, .5), .48, .6))
    col = nb.mixc(nb.math('MULTIPLY', cr, crack), col, srgb('1c1712'))
    if moss:
        up = nb.maprange(nb.normal_z(), .3, .85)
        mm = nb.math('MULTIPLY', nb.math('MULTIPLY', up, nb.maprange(nb.noise(4 * scale, 4, .6), .42, .6)), moss)
        col = nb.mixc(mm, col, srgb('55652f'))
    col = nb.finish_color(col, dark, .15, .06, scale_rgb(base, edge), .9, .02)
    h = nb.math('ADD', nb.math('MULTIPLY', nb.noise(12 * scale, 4, .65), .6), nb.math('MULTIPLY', cr, -.8 * crack))
    nb.principled(**{'Base Color': col, 'Roughness': rough, 'Specular IOR Level': .35, 'Normal': nb.bump(h, .3, .02)})
    return m


@S.cached
def pearl(color='f3ece0', name='Pearl', tint='ffd6e8'):
    m, nb = S._new(name)
    lw = nb.node('ShaderNodeLayerWeight')
    lw.inputs['Blend'].default_value = .4
    col = nb.mixc(lw.outputs['Facing'], srgb(color), srgb(tint))
    nb.principled(**{'Base Color': col, 'Roughness': .3, 'Coat Weight': 1.0, 'Coat Roughness': .08,
                     'Subsurface Weight': .2, 'Subsurface Radius': (1, .9, .8), 'Subsurface Scale': .02,
                     'Thin Film Thickness': 380.0, 'Thin Film IOR': 1.5})
    return m


@S.cached
def velvet(color='5c0c1a', name='Velvet', sheen=0.35, dark=''):
    m, nb = S._new(name)
    c = srgb(color)
    d = srgb(dark) if dark else scale_rgb(c, .45)
    col = nb.mixc(nb.maprange(nb.noise(5, 3, .6), .3, .7), c, d)
    col = nb.finish_color(col, .5, .12, .05)
    nb.principled(**{'Base Color': col, 'Roughness': .95, 'Specular IOR Level': .08, 'Sheen Weight': sheen,
                     'Sheen Roughness': .4, 'Sheen Tint': scale_rgb(c, 2.2),
                     'Normal': nb.bump(nb.noise(60, 2, .5), .1, .01)})
    return m


@S.cached
def wax(color='9a1f2a', name='Sealing wax'):
    m, nb = S._new(name)
    c = srgb(color)
    col = nb.finish_color(c, .45, .05, .1, scale_rgb(c, 1.5), .5)
    nb.principled(**{'Base Color': col, 'Roughness': .32, 'Subsurface Weight': .25,
                     'Subsurface Radius': (1, .3, .2), 'Subsurface Scale': .02, 'Coat Weight': .4,
                     'Normal': nb.bump(nb.noise(18, 3, .6), .1, .01)})
    return m


@S.cached
def wool(color='efe6d6', name='Wool', shade='b9a88c'):
    m, nb = S._new(name)
    v = nb.voronoi(22, 'F1')
    curl = nb.maprange(v, 0.0, .5, 1, 0)
    col = nb.mixc(nb.maprange(nb.noise(5, 3, .6), .3, .7, 0, .4), srgb(color), srgb(shade))
    col = nb.finish_color(col, .45, .12, .15)
    nb.principled(**{'Base Color': col, 'Roughness': .95, 'Sheen Weight': 1.0, 'Sheen Roughness': .35,
                     'Sheen Tint': srgb('fff6e8'), 'Subsurface Weight': .15, 'Subsurface Radius': (1, .9, .7),
                     'Subsurface Scale': .05, 'Normal': nb.bump(curl, .6, .03)})
    return m


@S.cached
def magma_rock(color='2a1d18', glow='ff6a1a', name='Magma rock', strength=12.0, crack=0.06, scale=3.2, hot='ffd08a'):
    """Charred rock with glowing fissures (hotter core colour at the fissure centre)."""
    m, nb = S._new(name)
    d = nb.voronoi(scale, 'DISTANCE_TO_EDGE')
    cr = nb.maprange(d, 0.0, crack, 1, 0)
    cr = nb.math('MULTIPLY', cr, nb.maprange(nb.noise(2.0, 3, .5), .3, .55))
    core = nb.math('POWER', cr, 3.0)
    base = srgb(color)
    col = nb.finish_color(nb.mixc(nb.maprange(nb.noise(6, 5, .65), .3, .7, 0, .5), base, scale_rgb(base, 2.2)),
                          .45, .1, .1, scale_rgb(base, 2.6), .7)
    ec = nb.mixc(core, srgb(glow), srgb(hot))
    nb.principled(**{'Base Color': nb.mixc(cr, col, srgb(glow)), 'Roughness': .8,
                     'Emission Color': ec, 'Emission Strength': nb.math('MULTIPLY', cr, strength),
                     'Normal': nb.bump(nb.math('ADD', nb.math('SUBTRACT', 0, cr), nb.noise(14, 4, .6)), .45, .02)})
    return m


@S.cached
def ghost(color='6ff0d2', strength=2.5, opacity=0.65, name='Ghost cloth', power=1.6, base_alpha=0.12):
    """Spectral translucent surface: clear-ish facing the camera, glowing at grazing edges.
    `fx` > 0.7 fades the surface out (tails)."""
    m, nb = S._new(name)
    lw = nb.node('ShaderNodeLayerWeight')
    lw.inputs['Blend'].default_value = 0.5
    fac = lw.outputs['Facing']
    n = nb.noise(4, 4, .6)
    a = nb.math('ADD', nb.math('MULTIPLY', nb.math('POWER', fac, power), opacity), base_alpha)
    a = nb.math('MULTIPLY', a, nb.maprange(n, .25, .65, .55, 1.0), clamp=True)
    t = _attr(nb, 'fx')
    a = nb.math('MULTIPLY', a, nb.maprange(t, .7, 1.0, 1.0, 0.0))
    c = srgb(color)
    col = nb.mixc(nb.math('POWER', fac, 2), scale_rgb(c, .55), scale_rgb(c, 1.5))
    _emit_mix(nb, a, col, strength)
    return m


@S.cached
def metal_tint(color, rough=0.25, name='Tinted metal', edge=1.5):
    """Clean polished metal (silver, gilt, black iron) with less hammer noise than the kit steel."""
    m, nb = S._new(name)
    base = srgb(color)
    var = nb.noise(6, 3, .55)
    col = nb.mixc(nb.maprange(var, .3, .7, 0, .15), base, scale_rgb(base, .75))
    col = nb.finish_color(col, .45, .08, .06, scale_rgb(base, edge), .8, .01)
    r = nb.math('ADD', rough, nb.maprange(nb.noise(12, 3, .6), .3, .7, -.06, .08))
    nb.principled(**{'Base Color': col, 'Metallic': 1.0, 'Roughness': r,
                     'Normal': nb.bump(nb.noise(40, 2, .5), .03, .01)})
    return m


# ===================================================================== geometry primitives
def torus(name, R, r, mat, coll, seg=48, sides=12, location=(0, 0, 0), rotation=(0, 0, 0), scale=(1, 1, 1),
          profile=None):
    """Torus around Z. profile: optional list of (dr, dz) ring points (unit) for non-round sections."""
    prof = profile or [(math.cos(i * math.tau / sides), math.sin(i * math.tau / sides)) for i in range(sides)]
    bm = bmesh.new()
    rings = []
    for i in range(seg):
        a = i * math.tau / seg
        ca, sa = math.cos(a), math.sin(a)
        ring = []
        for u, w in prof:
            rr = R + u * r
            ring.append(bm.verts.new((rr * ca, rr * sa, w * r)))
        rings.append(ring)
    n = len(prof)
    for i in range(seg):
        ra, rb = rings[i], rings[(i + 1) % seg]
        for j in range(n):
            k = (j + 1) % n
            bm.faces.new((ra[j], rb[j], rb[k], ra[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = geo._finish_mesh(name, bm, mat, coll, smooth=True)
    return geo.place(obj, location, rotation, scale)


def gem_cut(name, r, mat, coll, location=(0, 0, 0), rotation=(0, 0, 0), facets=12, table=0.55, crown=0.32,
            pavilion=0.85, girdle=0.06, scale=(1, 1, 1)):
    """Brilliant-style cut, table facing +Z. Flat shaded so facets sparkle."""
    prof = [(0, -pavilion * r), (r, -girdle * r * .5), (r, girdle * r * .5),
            (r * (table + (1 - table) * .45), crown * r * .7), (r * table, crown * r), (0, crown * r)]
    obj = geo.lathe(name, prof, facets, mat, coll, smooth=False)
    return geo.place(obj, location, rotation, scale)


def cabochon(name, r, mat, coll, location=(0, 0, 0), rotation=(0, 0, 0), height=0.55, scale=(1, 1, 1), seg=24):
    prof = [(0, 0)] + [(r * math.cos(a), height * r * math.sin(a)) for a in [i * math.pi / 2 / 8 for i in range(9)]]
    prof[-1] = (0, height * r)
    obj = geo.lathe(name, prof, seg, mat, coll)
    return geo.place(obj, location, rotation, scale)


def prism(name, base, direction, length, radius, mat, coll, sides=6, tip=0.35, butt=0.12, taper=0.85, twist=0.0,
          squash=1.0, seed=0, jitter=0.0):
    """Pointed crystal: polygonal prism with a pyramidal tip (and short butt) along `direction`."""
    d = Vector(direction).normalized()
    ref = V(0, 0, 1) if abs(d.z) < .9 else V(1, 0, 0)
    u = d.cross(ref).normalized()
    w = d.cross(u).normalized()
    rnd = random.Random(seed)
    prof = [(0.0, -butt), (1.0, 0.0), (taper, 1 - tip), (0.0, 1.0)]
    bm = bmesh.new()
    rings = []
    for rs, t in prof:
        z = t * length
        if rs <= 1e-6:
            rings.append([bm.verts.new(Vector(base) + d * z)])
            continue
        ring = []
        for i in range(sides):
            a = i * math.tau / sides + twist * t
            rr = radius * rs * (1 + (rnd.random() - .5) * jitter)
            p = Vector(base) + d * z + u * (math.cos(a) * rr) + w * (math.sin(a) * rr * squash)
            ring.append(bm.verts.new(p))
        rings.append(ring)
    for ra, rb in zip(rings, rings[1:]):
        if len(ra) == 1:
            for i in range(sides):
                bm.faces.new((ra[0], rb[(i + 1) % sides], rb[i]))
        elif len(rb) == 1:
            for i in range(sides):
                bm.faces.new((ra[i], ra[(i + 1) % sides], rb[0]))
        else:
            for i in range(sides):
                j = (i + 1) % sides
                bm.faces.new((ra[i], ra[j], rb[j], rb[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return geo._finish_mesh(name, bm, mat, coll, smooth=False)


def chain(name, points, link, wire, mat, coll, spacing=1.6):
    """Interlocking oval links along a polyline. link = link half-length, wire = wire radius."""
    pts = [Vector(p) for p in points]
    total = sum((b - a).length for a, b in zip(pts, pts[1:]))
    step = link * spacing
    n = max(1, int(total / step))
    out_bm = bmesh.new()

    def sample(s):
        acc = 0.0
        for a, b in zip(pts, pts[1:]):
            L = (b - a).length
            if acc + L >= s:
                t = (s - acc) / max(L, 1e-9)
                return a.lerp(b, t), (b - a).normalized()
            acc += L
        return pts[-1], (pts[-1] - pts[-2]).normalized()
    for i in range(n):
        p, d = sample(i * step + step * .5)
        ref = V(0, 0, 1) if abs(d.z) < .9 else V(1, 0, 0)
        side = d.cross(ref).normalized()
        if i % 2:
            side = d.cross(side).normalized()
        nrm = d.cross(side).normalized()
        seg, sides = 14, 6
        rings = []
        for k in range(seg):
            a = k * math.tau / seg
            c = p + d * (math.cos(a) * link) + side * (math.sin(a) * link * .55)
            tang = (-d * math.sin(a) * link + side * math.cos(a) * link * .55).normalized()
            radial = tang.cross(nrm).normalized()
            ring = []
            for j in range(sides):
                b = j * math.tau / sides
                ring.append(out_bm.verts.new(c + radial * (math.cos(b) * wire) + nrm * (math.sin(b) * wire)))
            rings.append(ring)
        for k in range(seg):
            ra, rb = rings[k], rings[(k + 1) % seg]
            for j in range(sides):
                jj = (j + 1) % sides
                out_bm.faces.new((ra[j], rb[j], rb[jj], ra[jj]))
    bmesh.ops.recalc_face_normals(out_bm, faces=out_bm.faces)
    return geo._finish_mesh(name, out_bm, mat, coll, smooth=True)


def coin(name, r, t, mat, coll, location=(0, 0, 0), rotation=(0, 0, 0), rim=0.12, seg=40):
    """Coin with a raised rim and a slightly domed field, face +Z."""
    prof = [(0, -t / 2), (r * .97, -t / 2), (r, -t * .35), (r, t * .35), (r * .97, t / 2), (r * (1 - rim), t / 2),
            (r * (1 - rim) * .98, t * .3), (r * .5, t * .38), (0, t * .42)]
    obj = geo.lathe(name, prof, seg, mat, coll)
    return geo.place(obj, location, rotation)


def disc(name, r, t, mat, coll, location=(0, 0, 0), rotation=(0, 0, 0), seg=40, bevel=0.2):
    b = t * bevel
    prof = [(0, -t / 2), (r - b, -t / 2), (r, -t / 2 + b), (r, t / 2 - b), (r - b, t / 2), (0, t / 2)]
    obj = geo.lathe(name, prof, seg, mat, coll)
    return geo.place(obj, location, rotation)


# ===================================================================== effects
def jag_path(a, b, segments=10, amp=0.12, seed=0, bias=None):
    """Lightning-like jagged polyline from a to b."""
    rnd = random.Random(seed)
    a, b = Vector(a), Vector(b)
    d = (b - a)
    L = d.length
    dn = d.normalized()
    ref = V(0, 1, 0) if abs(dn.y) < .9 else V(1, 0, 0)
    u = dn.cross(ref).normalized()
    w = dn.cross(u).normalized()
    pts = [a]
    for i in range(1, segments):
        t = i / segments
        env = math.sin(math.pi * t) ** .6
        off = u * (rnd.uniform(-1, 1) * amp * L * env) + w * (rnd.uniform(-1, 1) * amp * L * env * .5)
        if bias is not None:
            off += Vector(bias) * env * L
        pts.append(a + d * t + off)
    pts.append(b)
    return pts


def fx_tube(name, pts, radii, mat, coll, sides=10, fx_range=(0.0, 1.0), cap=True):
    """Tube with fx along its length (fx_range) and fxw = 0.5 (no width fade)."""
    obj = geo.tube(name, pts, radii, mat, coll, sides=sides, cap=cap)
    n = len(pts)
    vals = []
    for i in range(len(obj.data.vertices)):
        k = min(n - 1, i // sides)
        vals.append(fx_range[0] + (fx_range[1] - fx_range[0]) * k / max(1, n - 1))
    set_point_attr(obj, 'fx', vals)
    set_point_attr(obj, 'fxw', [0.5] * len(vals))
    return obj


def ribbon(name, pts, widths, mat, coll, up=None, fx_range=(0.0, 1.0), normals=None, twist=0.0):
    """Flat ribbon along pts. widths per point. up: fixed binormal hint."""
    pts = [Vector(p) for p in pts]
    if isinstance(widths, (int, float)):
        widths = [widths] * len(pts)
    frames = geo._frames(pts)
    bm = bmesh.new()
    rows = []
    fxv, fxw = [], []
    nw = 4
    for i, (p, t, n, b) in enumerate(frames):
        side = (Vector(up).cross(t).normalized() if up is not None else n)
        if normals is not None:
            side = Vector(normals[i]).cross(t).normalized()
        if twist:
            side = Matrix.Rotation(twist * i / max(1, len(pts) - 1), 3, t) @ side
        row = []
        for j in range(nw + 1):
            s = j / nw
            row.append(bm.verts.new(p + side * ((s - .5) * widths[i])))
            fxv.append(fx_range[0] + (fx_range[1] - fx_range[0]) * i / max(1, len(pts) - 1))
            fxw.append(s)
        rows.append(row)
    for ra, rb in zip(rows, rows[1:]):
        for j in range(nw):
            bm.faces.new((ra[j], ra[j + 1], rb[j + 1], rb[j]))
    obj = geo._finish_mesh(name, bm, mat, coll, smooth=True)
    set_point_attr(obj, 'fx', fxv)
    set_point_attr(obj, 'fxw', fxw)
    return obj


def spiral_points(center, r0, r1, z0, z1, turns, steps=64, phase=0.0, axis=None):
    c = Vector(center)
    out = []
    for i in range(steps + 1):
        t = i / steps
        a = phase + turns * math.tau * t
        r = r0 + (r1 - r0) * t
        out.append(c + V(math.cos(a) * r, math.sin(a) * r, z0 + (z1 - z0) * t))
    if axis is not None:
        rot = V(0, 0, 1).rotation_difference(Vector(axis).normalized()).to_matrix()
        out = [c + rot @ (p - c) for p in out]
    return out


def flat_ring(name, center, normal, radius, width, mat, coll, seg=64, arc=math.tau, phase=0.0, fx_fn=None):
    """Annulus in the plane with the given normal; fxw across the band, fx around (0..1)."""
    nrm = Vector(normal).normalized()
    ref = V(0, 0, 1) if abs(nrm.z) < .9 else V(1, 0, 0)
    u = nrm.cross(ref).normalized()
    w = nrm.cross(u).normalized()
    bm = bmesh.new()
    full = abs(arc - math.tau) < 1e-6
    steps = seg if full else seg + 1
    nw = 4
    rows, fxv, fxw = [], [], []
    for i in range(steps):
        a = phase + arc * i / seg
        row = []
        for j in range(nw + 1):
            s = j / nw
            r = radius + (s - .5) * width
            row.append(bm.verts.new(Vector(center) + u * (math.cos(a) * r) + w * (math.sin(a) * r)))
            fxv.append(i / max(1, steps - 1))
            fxw.append(s)
        rows.append(row)
    pairs = list(zip(rows, rows[1:])) + ([(rows[-1], rows[0])] if full else [])
    for ra, rb in pairs:
        for j in range(nw):
            bm.faces.new((ra[j], ra[j + 1], rb[j + 1], rb[j]))
    obj = geo._finish_mesh(name, bm, mat, coll, smooth=True)
    if fx_fn is not None:
        fxv = [fx_fn(v) for v in fxv]
    set_point_attr(obj, 'fx', fxv)
    set_point_attr(obj, 'fxw', fxw)
    return obj


def sparks(name, center, count, r0, r1, length, radius, mat, coll, seed=0, direction=None, spread=1.0,
           outward=True, size_var=0.6):
    """Streaked ember / spark particles as tiny tapered tubes (fx 0 at head, 1 at tail)."""
    rnd = random.Random(seed)
    objs = []
    c = Vector(center)
    for i in range(count):
        dvec = V(rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, 1)).normalized()
        if direction is not None:
            dvec = (Vector(direction).normalized() + dvec * spread).normalized()
        r = rnd.uniform(r0, r1)
        p = c + dvec * r
        mv = dvec if outward else -dvec
        k = 1 - size_var + rnd.random() * size_var
        tail = p - mv * length * k
        objs.append(fx_tube(f'{name} {i}', [p, p.lerp(tail, .35), tail], [radius * k, radius * k * .7, radius * .08], mat,
                            coll, sides=5))
    return [geo.join(objs, name)] if len(objs) > 1 else objs


def motes(name, center, count, radius, size, mat, coll, seed=0, flatten=(1, 1, 1), up=0.0):
    rnd = random.Random(seed)
    objs = []
    for i in range(count):
        d = V(rnd.gauss(0, 1) * flatten[0], rnd.gauss(0, 1) * flatten[1], rnd.gauss(0, 1) * flatten[2])
        p = Vector(center) + d.normalized() * radius * rnd.random() ** .5 + V(0, 0, up * rnd.random())
        s = size * (0.4 + rnd.random() * .8)
        objs.append(geo.sphere(f'{name} {i}', s, p, mat, coll, 8, 5))
    return [geo.join(objs, name)] if len(objs) > 1 else objs


def star_flare(name, center, size, mat, coll, rays=4, thin=0.08, rotation=0.0, facing=(0, -1, 0)):
    """Pointed sparkle (glint) facing the camera. fx: 0 centre -> 1 tips."""
    f = Vector(facing).normalized()
    ref = V(0, 0, 1) if abs(f.z) < .9 else V(1, 0, 0)
    u = f.cross(ref).normalized()
    w = f.cross(u).normalized()
    bm = bmesh.new()
    c = Vector(center)
    cv = bm.verts.new(c)
    fxv = [0.0]
    for k in range(rays):
        a = rotation + k * math.tau / rays
        L = size * (1.0 if k % 2 == 0 else .55) if rays > 4 else size
        tipd = u * math.cos(a) + w * math.sin(a)
        sided = u * math.cos(a + math.pi / 2) + w * math.sin(a + math.pi / 2)
        t = bm.verts.new(c + tipd * L)
        l = bm.verts.new(c + tipd * (L * .18) + sided * (L * thin))
        r = bm.verts.new(c + tipd * (L * .18) - sided * (L * thin))
        fxv += [1.0, .25, .25]
        bm.faces.new((cv, l, t))
        bm.faces.new((cv, t, r))
    obj = geo._finish_mesh(name, bm, mat, coll, smooth=True)
    set_point_attr(obj, 'fx', fxv)
    set_point_attr(obj, 'fxw', [0.5] * len(fxv))
    return obj


def flame(name, base, height, radius, mat, coll, lean=(0.0, 0.0), seed=0, curl=0.35, sides=20, rings=28,
          wobble=0.22, twist=1.2, tip_pow=1.0, direction=None):
    """Stylised flame tongue: teardrop swept along a wavy spine; fx = 0 at base -> 1 at tip."""
    rnd = random.Random(seed)
    ph = rnd.random() * 10
    bm = bmesh.new()
    rows = []
    fxv = []
    up = Vector(direction).normalized() if direction is not None else V(0, 0, 1)
    ref = V(1, 0, 0) if abs(up.x) < .9 else V(0, 1, 0)
    ux = up.cross(ref).normalized()
    uy = up.cross(ux).normalized()
    base = Vector(base)
    for k in range(rings + 1):
        t = k / rings
        prof = (math.sin(math.pi * min(1.0, t * 1.9 + .02)) ** .6) if t < .26 else (1 - (t - .26) / .74) ** (1.15 * tip_pow)
        r = radius * max(prof, 0.0)
        sx = lean[0] * t * t + wobble * radius * math.sin(t * 6.0 + ph) * t
        sy = lean[1] * t * t + wobble * radius * math.cos(t * 5.0 + ph * 1.3) * t
        center = base + up * (height * t) + ux * sx + uy * sy
        row = []
        for i in range(sides):
            a = i * math.tau / sides + twist * t
            n = noise.noise(V(math.cos(a) * 1.5, math.sin(a) * 1.5, t * 3 + ph))
            rr = r * (1 + curl * n * t)
            row.append(bm.verts.new(center + ux * (math.cos(a) * rr) + uy * (math.sin(a) * rr)))
            fxv.append(t)
        rows.append(row)
    for ra, rb in zip(rows, rows[1:]):
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((ra[i], ra[j], rb[j], rb[i]))
    bm.faces.new(list(reversed(rows[0])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = geo._finish_mesh(name, bm, mat, coll, smooth=True)
    set_point_attr(obj, 'fx', fxv)
    set_point_attr(obj, 'fxw', [0.5] * len(fxv))
    return obj


def halo_sphere(name, center, radius, mat, coll, scale=(1, 1, 1)):
    o = geo.sphere(name, radius, center, mat, coll, 32, 16, scale=scale)
    return nofit(o)


# ===================================================================== decorative
RUNES = {
    # strokes in a [-1, 1] box (x right, y up); angular so they read when tiny
    'fehu': [[(-0.4, -1), (-0.4, 1)], [(-0.4, 0.2), (0.5, 0.75)], [(-0.4, -0.25), (0.5, 0.3)]],
    'algiz': [[(0, -1), (0, 1)], [(0, 0.15), (-0.6, 0.8)], [(0, 0.15), (0.6, 0.8)]],
    'tiwaz': [[(0, -1), (0, 1)], [(0, 1), (-0.6, 0.4)], [(0, 1), (0.6, 0.4)]],
    'othala': [[(-0.6, -1), (0.5, 0.05), (0, 0.8), (-0.5, 0.05), (0.6, -1)]],
    'dagaz': [[(-0.7, -0.9), (-0.7, 0.9), (0.7, -0.9), (0.7, 0.9), (-0.7, -0.9)]],
    'sowilo': [[(0.4, 1), (-0.4, 0.3), (0.4, -0.3), (-0.4, -1)]],
    'ingwaz': [[(0, 1), (0.6, 0), (0, -1), (-0.6, 0), (0, 1)]],
    'thurisaz': [[(-0.3, -1), (-0.3, 1)], [(-0.3, 0.55), (0.45, 0), (-0.3, -0.55)]],
    'raido': [[(-0.4, -1), (-0.4, 1), (0.4, 0.5), (-0.4, 0.05), (0.45, -1)]],
    'kenaz': [[(0.4, 1), (-0.4, 0), (0.4, -1)]],
    'star': [[(0, -1), (0, 1)], [(-0.85, -0.5), (0.85, 0.5)], [(-0.85, 0.5), (0.85, -0.5)]],
    'eye': [[(-0.9, 0), (0, 0.55), (0.9, 0), (0, -0.55), (-0.9, 0)], [(0, -0.2), (0, 0.2)]],
    'bolt': [[(0.25, 1), (-0.35, 0.05), (0.25, 0.05), (-0.25, -1)]],
    'tower': [[(-0.45, -1), (-0.45, 0.55), (0.45, 0.55), (0.45, -1)], [(-0.6, 0.55), (-0.6, 0.95)],
              [(0, 0.55), (0, 0.95)], [(0.6, 0.55), (0.6, 0.95)], [(-0.15, -1), (-0.15, -0.5), (0.15, -0.5), (0.15, -1)]],
    'moon': [[(0.3, 0.95), (-0.35, 0.65), (-0.6, 0), (-0.35, -0.65), (0.3, -0.95), (-0.05, -0.45), (-0.15, 0),
              (-0.05, 0.45), (0.3, 0.95)]],
}


def rune(name, glyph, origin, u_axis, v_axis, size, radius, mat, coll, depth_axis=None, relief=0.0):
    """Rune strokes as round tubes on the plane spanned by u/v (world vectors)."""
    o = Vector(origin)
    u, v = Vector(u_axis).normalized(), Vector(v_axis).normalized()
    nrm = Vector(depth_axis).normalized() if depth_axis is not None else u.cross(v).normalized()
    objs = []
    for si, stroke in enumerate(RUNES[glyph]):
        pts = [o + u * (x * size) + v * (y * size) + nrm * relief for x, y in stroke]
        dense = []
        for a, b in zip(pts, pts[1:]):
            for k in range(4):
                dense.append(a.lerp(b, k / 4))
        dense.append(pts[-1])
        objs.append(geo.tube(f'{name} {si}', dense, radius, mat, coll, sides=6))
    return geo.join(objs, name) if len(objs) > 1 else objs[0]


def leaf(name, base, direction, length, width, mat, coll, normal=(0, -1, 0), bend=0.25, fold=0.35, seg=8, curl=0.0,
         thickness=0.08):
    """Lanceolate leaf with a centre fold, bending away from its normal."""
    d = Vector(direction).normalized()
    nrm = Vector(normal).normalized()
    side = d.cross(nrm).normalized()
    nrm = side.cross(d).normalized()
    bm = bmesh.new()
    rows = []
    for k in range(seg + 1):
        t = k / seg
        w = width * math.sin(math.pi * min(1.0, t * 1.05)) ** .8 * (1 - t * .15)
        c = Vector(base) + d * (length * t) + nrm * (-bend * length * t * t) + side * (curl * length * t * t)
        row = [bm.verts.new(c + side * (-w / 2) + nrm * (fold * w * .5)), bm.verts.new(c),
               bm.verts.new(c + side * (w / 2) + nrm * (fold * w * .5))]
        rows.append(row)
    for ra, rb in zip(rows, rows[1:]):
        for j in range(2):
            bm.faces.new((ra[j], ra[j + 1], rb[j + 1], rb[j]))
    obj = geo._finish_mesh(name, bm, mat, coll, smooth=True)
    if thickness:
        m = obj.modifiers.new('Leaf thickness', 'SOLIDIFY')
        m.thickness = width * thickness
        m.offset = 0
        m.use_even_offset = False
    return obj


def sheet(name, fn, nx, ny, mat, coll, thickness=0.02, subsurf=1, offset=0.0):
    """Cloth/parchment sheet from fn(u, v) with plain (non-even) solidify for stability."""
    obj = geo.grid_sheet(name, 1, 1, nx, ny, fn, mat, coll)
    if thickness:
        m = obj.modifiers.new('Cloth thickness', 'SOLIDIFY')
        m.thickness = thickness
        m.offset = offset
        m.use_even_offset = False
        m.use_rim = True
    if subsurf:
        s = obj.modifiers.new('Cloth smooth', 'SUBSURF')
        s.levels = subsurf
        s.render_levels = subsurf
    return obj


def bevel_mod(obj, width, segments=2, angle=35):
    m = obj.modifiers.new('Crafted edges', 'BEVEL')
    m.width = width
    m.segments = segments
    m.limit_method = 'ANGLE'
    m.angle_limit = math.radians(angle)
    return obj


def subsurf_mod(obj, levels=1):
    m = obj.modifiers.new('Smooth', 'SUBSURF')
    m.levels = levels
    m.render_levels = levels
    return obj


def solid_mod(obj, t, offset=0.0, rim=True):
    m = obj.modifiers.new('Thickness', 'SOLIDIFY')
    m.thickness = t
    m.offset = offset
    m.use_even_offset = False
    m.use_rim = rim
    return obj


def rock(name, center, radius, mat, coll, seed=0, rough=0.25, scale=(1, 1, 1), level=2, rotation=(0, 0, 0)):
    """Chunky faceted boulder."""
    o = geo.quadsphere(name, radius, (0, 0, 0), mat, coll, level=level)
    rnd = random.Random(seed)
    off = V(rnd.random() * 50, rnd.random() * 50, rnd.random() * 50)
    for v in o.data.vertices:
        n = noise.noise(v.co * (1.6 / radius) + off)
        n2 = noise.noise(v.co * (4.0 / radius) + off * 2)
        v.co *= 1 + rough * n + rough * .35 * n2
    for k in range(5):
        pn = V(rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, 1)).normalized()
        lim = radius * rnd.uniform(.6, .85)
        for v in o.data.vertices:
            dd = v.co.dot(pn)
            if dd > lim:
                v.co -= pn * (dd - lim)
    o.data.update()
    for p in o.data.polygons:
        p.use_smooth = False
    geo.store_rest(o)
    return geo.place(o, center, rotation, scale)


def block(name, size, mat, coll, location=(0, 0, 0), rotation=(0, 0, 0), round_=0.12, chip=0.03, seed=0, level=3,
          flat=False):
    """Chiselled block: cube-projected quad sphere with rounded edges and chipped faces."""
    o = geo.quadsphere(name, 1.0, (0, 0, 0), mat, coll, level=level)
    rnd = random.Random(seed)
    off = V(rnd.random() * 40, rnd.random() * 40, rnd.random() * 40)
    sx, sy, sz = size
    for v in o.data.vertices:
        sph = v.co.normalized()
        m = max(abs(sph.x), abs(sph.y), abs(sph.z))
        cube = sph / m
        p = cube.lerp(sph, round_)
        p = V(p.x * sx / 2, p.y * sy / 2, p.z * sz / 2)
        n = noise.noise(p * 3.0 + off) + 0.5 * noise.noise(p * 7.0 + off)
        v.co = p + sph * (chip * n)
    o.data.update()
    for poly in o.data.polygons:
        poly.use_smooth = not flat
    geo.store_rest(o)
    return geo.place(o, location, rotation)


def heart_shape(name, size, mat, coll, depth=0.55, level=4, location=(0, 0, 0), rotation=(0, 0, 0)):
    """Rounded symbolic heart (lobes up, point down), thickness along Y."""
    o = geo.quadsphere(name, 1.0, (0, 0, 0), mat, coll, level=level)
    for v in o.data.vertices:
        x, y, z = v.co
        if z < 0:
            k = -z
            x *= (1 - k) ** .8 + .02
            y *= (1 - k * .7)
            zz = z * 1.25
        else:
            x *= 1.12
            zz = z - .32 * math.exp(-(x * x) / .05) * z
        v.co = V(x * size, y * size * depth, zz * size)
    o.data.update()
    geo.store_rest(o)
    return geo.place(o, location, rotation)


# ===================================================================== food & misc materials
@S.cached
def baked(color='b8742c', top='e0a656', name='Crust', flour=0.25):
    """Bread crust: darker sides, golden top, faint flour dust."""
    m, nb = S._new(name)
    up = nb.maprange(nb.normal_z(), -0.2, 0.9)
    col = nb.mixc(up, srgb(color), srgb(top))
    col = nb.mixc(nb.maprange(nb.noise(8, 4, .6), .35, .7, 0, .35), col, scale_rgb(srgb(color), .55))
    col = nb.mixc(nb.math('MULTIPLY', nb.maprange(nb.noise(30, 2, .5), .6, .8), flour), col, srgb('f4ead6'))
    col = nb.finish_color(col, .5, .1, .08)
    nb.principled(**{'Base Color': col, 'Roughness': .7, 'Subsurface Weight': .1, 'Subsurface Radius': (1, .6, .3),
                     'Subsurface Scale': .03, 'Normal': nb.bump(nb.noise(16, 4, .65), .25, .015)})
    return m


@S.cached
def roast(color='8a3a1e', char='3a160a', name='Roast', gloss=0.35):
    """Caramelised roast meat glaze."""
    m, nb = S._new(name)
    col = nb.mixc(nb.maprange(nb.noise(5, 5, .65), .35, .7, 0, .7), srgb(color), srgb(char))
    col = nb.mixc(nb.maprange(nb.normal_z(), .2, .9, 0, .3), col, srgb('c8662e'))
    col = nb.finish_color(col, .5, .1, .06)
    nb.principled(**{'Base Color': col, 'Roughness': gloss, 'Coat Weight': .5, 'Coat Roughness': .2,
                     'Subsurface Weight': .1, 'Subsurface Radius': (1, .4, .2), 'Subsurface Scale': .03,
                     'Normal': nb.bump(nb.noise(12, 5, .7), .35, .015)})
    return m


@S.cached
def fruit(color='b8141e', name='Apple skin', streak='e8a030'):
    m, nb = S._new(name)
    col = nb.mixc(nb.maprange(nb.wave(6, 3, 2, 'Z'), .3, .8, 0, .4), srgb(color), srgb(streak))
    col = nb.finish_color(col, .5, .1, .1)
    nb.principled(**{'Base Color': col, 'Roughness': .3, 'Coat Weight': .6, 'Coat Roughness': .15,
                     'Subsurface Weight': .1, 'Subsurface Radius': (1, .3, .2), 'Subsurface Scale': .02})
    return m


@S.cached
def page_block(color='ece0c0', name='Page edges'):
    m, nb = S._new(name)
    lines = nb.wave(160, 1.5, 1, 'Z')
    col = nb.mixc(nb.maprange(lines, .2, .8, 0, .25), srgb(color), srgb('a08a60'))
    col = nb.finish_color(col, .5, .05, .05)
    nb.principled(**{'Base Color': col, 'Roughness': .85, 'Normal': nb.bump(lines, .1, .005)})
    return m


def star_solid(name, r_out, r_in, depth, mat, coll, points=5, location=(0, 0, 0), rotation=(0, 0, 0), bevel_ring=0.0):
    """Faceted 3D star (pyramidal on both faces), face normal -Y."""
    bm = bmesh.new()
    ring = []
    for k in range(points * 2):
        a = math.pi / 2 + k * math.pi / points
        r = r_out if k % 2 == 0 else r_in
        ring.append(bm.verts.new((math.cos(a) * r, 0.0, math.sin(a) * r)))
    front = bm.verts.new((0, -depth, 0))
    back = bm.verts.new((0, depth, 0))
    n = len(ring)
    for k in range(n):
        bm.faces.new((ring[k], ring[(k + 1) % n], front))
        bm.faces.new((ring[(k + 1) % n], ring[k], back))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = geo._finish_mesh(name, bm, mat, coll, smooth=False)
    return geo.place(obj, location, rotation)


@S.cached
def dust(color='9a8268', name='Dust', opacity=0.75, power=1.3, rough_noise=0.5):
    """Lit, soft-edged dust/smoke puff: diffuse shaded (so it picks up the key light) with alpha falling
    off towards the silhouette and broken up by noise."""
    m, nb = S._new(name)
    lw = nb.node('ShaderNodeLayerWeight')
    lw.inputs['Blend'].default_value = 0.5
    face = nb.math('SUBTRACT', 1.0, lw.outputs['Facing'], clamp=True)
    n = nb.noise(3.0, 4, .6)
    alpha = nb.math('MULTIPLY', nb.math('POWER', face, power), opacity)
    alpha = nb.math('MULTIPLY', alpha, nb.maprange(n, .3, .7, 1 - rough_noise, 1.0), clamp=True)
    c = srgb(color)
    col = nb.mixc(nb.maprange(nb.noise(6, 3, .6), .3, .7, 0, .35), c, scale_rgb(c, .65))
    b = nb.node('ShaderNodeBsdfPrincipled')
    nb.feed(b.inputs['Base Color'], col)
    b.inputs['Roughness'].default_value = 1.0
    b.inputs['Specular IOR Level'].default_value = 0.0
    tr = nb.node('ShaderNodeBsdfTransparent')
    mix = nb.node('ShaderNodeMixShader')
    nb.feed(mix.inputs[0], alpha)
    nb.links.new(tr.outputs[0], mix.inputs[1])
    nb.links.new(b.outputs[0], mix.inputs[2])
    nb.links.new(mix.outputs[0], nb.out.inputs['Surface'])
    return m


def puff(name, center, radius, mat, coll, seed=0, lumps=5, squash=1.0):
    """Cauliflower dust cloud: a few overlapping lumpy spheres."""
    rnd = random.Random(seed)
    objs = []
    for k in range(lumps):
        d = V(rnd.gauss(0, 1), rnd.gauss(0, 1), abs(rnd.gauss(0, 1)) * 0.6).normalized()
        r = radius * rnd.uniform(0.55, 0.9)
        o = geo.quadsphere(f'{name} {k}', r, Vector(center) + d * radius * 0.55, mat, coll, level=3,
                           scale=(1, 1, squash))
        objs.append(o)
    out = geo.join(objs, name) if len(objs) > 1 else objs[0]
    for v in out.data.vertices:
        v.co += v.normal * radius * 0.12 * noise.noise(v.co * (4.0 / radius) + V(seed, 0, 0))
    out.data.update()
    geo.store_rest(out)
    return nofit(out)
