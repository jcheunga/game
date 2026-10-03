"""Hand-finished PBR material library.

Every material reads a stable object-space coordinate (`rest` attribute, falling
back to object coordinates) so procedural detail never swims on animated parts.
Shared ingredients: cavity darkening (local AO), worn edge highlights (bevel
normal vs. true normal), a gentle painted top-light, and colour/roughness noise.
"""
import bpy

from .core import srgb, scale_rgb

_CACHE = {}


class NodeBuilder:
    def __init__(self, mat):
        self.mat = mat
        self.nt = mat.node_tree
        self.nodes = self.nt.nodes
        self.links = self.nt.links
        self.nodes.clear()
        self.out = self.node('ShaderNodeOutputMaterial')
        self._coord = None
        self._edge = {}
        self._ao = {}
        self._normal_z = None

    def node(self, kind, **props):
        n = self.nodes.new(kind)
        for k, v in props.items():
            setattr(n, k, v)
        return n

    def feed(self, socket, value):
        if value is None:
            return
        if isinstance(value, bpy.types.NodeSocket):
            self.links.new(value, socket)
        else:
            socket.default_value = value

    # ---------- coordinates and masks ----------
    def coord(self):
        if self._coord is None:
            attr = self.node('ShaderNodeAttribute', attribute_type='GEOMETRY', attribute_name='rest')
            tex = self.node('ShaderNodeTexCoord')
            # When `rest` is absent the attribute output is zero; fall back to object space.
            length = self.node('ShaderNodeVectorMath', operation='LENGTH')
            self.links.new(attr.outputs['Vector'], length.inputs[0])
            has = self.node('ShaderNodeMath', operation='GREATER_THAN')
            self.links.new(length.outputs['Value'], has.inputs[0])
            has.inputs[1].default_value = 1e-6
            mix = self.node('ShaderNodeMix', data_type='VECTOR')
            self.links.new(has.outputs[0], mix.inputs[0])
            self.links.new(tex.outputs['Object'], mix.inputs[4])
            self.links.new(attr.outputs['Vector'], mix.inputs[5])
            self._coord = mix.outputs[1]
        return self._coord

    def scaled(self, scale):
        if isinstance(scale, (int, float)):
            scale = (scale, scale, scale)
        m = self.node('ShaderNodeVectorMath', operation='MULTIPLY')
        self.links.new(self.coord(), m.inputs[0])
        m.inputs[1].default_value = scale
        return m.outputs[0]

    def noise(self, scale=4.0, detail=4.0, rough=0.55, vec=None, distortion=0.0, dims='3D'):
        n = self.node('ShaderNodeTexNoise', noise_dimensions=dims)
        self.links.new(vec if vec is not None else self.coord(), n.inputs['Vector'])
        n.inputs['Scale'].default_value = scale
        n.inputs['Detail'].default_value = detail
        n.inputs['Roughness'].default_value = rough
        n.inputs['Distortion'].default_value = distortion
        return n.outputs['Fac']

    def voronoi(self, scale=6.0, feature='F1', metric='EUCLIDEAN', vec=None, output='Distance', randomness=1.0):
        n = self.node('ShaderNodeTexVoronoi', feature=feature, distance=metric)
        self.links.new(vec if vec is not None else self.coord(), n.inputs['Vector'])
        n.inputs['Scale'].default_value = scale
        n.inputs['Randomness'].default_value = randomness
        return n.outputs[output]

    def wave(self, scale=3.0, distortion=4.0, detail=3.0, axis='Z', vec=None, kind='BANDS', profile='SIN'):
        n = self.node('ShaderNodeTexWave', wave_type=kind, wave_profile=profile)
        if kind == 'BANDS':
            n.bands_direction = axis
        else:
            n.rings_direction = axis
        self.links.new(vec if vec is not None else self.coord(), n.inputs['Vector'])
        n.inputs['Scale'].default_value = scale
        n.inputs['Distortion'].default_value = distortion
        n.inputs['Detail'].default_value = detail
        return n.outputs['Fac']

    def math(self, op, a, b=None, clamp=False):
        n = self.node('ShaderNodeMath', operation=op, use_clamp=clamp)
        self.feed(n.inputs[0], a)
        if b is not None:
            self.feed(n.inputs[1], b)
        return n.outputs[0]

    def maprange(self, value, a, b, c=0.0, d=1.0, clamp=True, interp='LINEAR'):
        n = self.node('ShaderNodeMapRange', interpolation_type=interp, clamp=clamp)
        self.feed(n.inputs['Value'], value)
        n.inputs['From Min'].default_value = a
        n.inputs['From Max'].default_value = b
        n.inputs['To Min'].default_value = c
        n.inputs['To Max'].default_value = d
        return n.outputs['Result']

    def ramp(self, fac, stops, interp='LINEAR'):
        n = self.node('ShaderNodeValToRGB')
        n.color_ramp.interpolation = interp
        els = n.color_ramp.elements
        rgba = lambda col: col if len(col) == 4 else tuple(col) + (1,)
        stops = sorted(stops, key=lambda s: s[0])
        # Ends first, then insert middle stops (Blender keeps the list sorted by position).
        els[0].position, els[0].color = stops[0][0], rgba(stops[0][1])
        els[-1].position, els[-1].color = stops[-1][0], rgba(stops[-1][1])
        for pos, col in stops[1:-1]:
            els.new(pos).color = rgba(col)
        self.feed(n.inputs['Fac'], fac)
        return n.outputs['Color']

    def mixc(self, fac, a, b, blend='MIX'):
        n = self.node('ShaderNodeMix', data_type='RGBA', blend_type=blend)
        self.feed(n.inputs[0], fac)
        self.feed(n.inputs[6], a)
        self.feed(n.inputs[7], b)
        return n.outputs[2]

    def mixf(self, fac, a, b):
        n = self.node('ShaderNodeMix', data_type='FLOAT')
        self.feed(n.inputs[0], fac)
        self.feed(n.inputs[2], a)
        self.feed(n.inputs[3], b)
        return n.outputs[0]

    def hsv(self, color, hue=0.5, sat=1.0, val=1.0, fac=1.0):
        n = self.node('ShaderNodeHueSaturation')
        self.feed(n.inputs['Hue'], hue)
        self.feed(n.inputs['Saturation'], sat)
        self.feed(n.inputs['Value'], val)
        self.feed(n.inputs['Fac'], fac)
        self.feed(n.inputs['Color'], color)
        return n.outputs['Color']

    def bump(self, height, strength=0.2, distance=0.02, normal=None):
        n = self.node('ShaderNodeBump')
        self.feed(n.inputs['Height'], height)
        n.inputs['Strength'].default_value = strength
        n.inputs['Distance'].default_value = distance
        if normal is not None:
            self.links.new(normal, n.inputs['Normal'])
        return n.outputs['Normal']

    def edge(self, radius=0.012, lo=0.02, hi=0.16):
        key = (radius, lo, hi)
        if key not in self._edge:
            bev = self.node('ShaderNodeBevel', samples=8)
            bev.inputs['Radius'].default_value = radius
            geo = self.node('ShaderNodeNewGeometry')
            dot = self.node('ShaderNodeVectorMath', operation='DOT_PRODUCT')
            self.links.new(bev.outputs['Normal'], dot.inputs[0])
            self.links.new(geo.outputs['Normal'], dot.inputs[1])
            inv = self.math('SUBTRACT', 1.0, dot.outputs['Value'])
            self._edge[key] = self.maprange(inv, lo, hi)
        return self._edge[key]

    def cavity(self, distance=0.12):
        if distance not in self._ao:
            ao = self.node('ShaderNodeAmbientOcclusion', only_local=True, samples=8)
            ao.inputs['Distance'].default_value = distance
            self._ao[distance] = ao.outputs['AO']
        return self._ao[distance]

    def normal_z(self):
        if self._normal_z is None:
            geo = self.node('ShaderNodeNewGeometry')
            sep = self.node('ShaderNodeSeparateXYZ')
            self.links.new(geo.outputs['Normal'], sep.inputs[0])
            self._normal_z = sep.outputs['Z']
        return self._normal_z

    def finish_color(self, base, cavity_dark=0.55, cavity_dist=0.12, top_light=0.12, edge_color=None,
                     edge_amount=0.0, edge_radius=0.012):
        """Apply cavity, painted top-light and worn edges to a base colour socket/value."""
        col = base
        if cavity_dark < 1.0:
            dark = self.mixc(1.0, col, (cavity_dark, cavity_dark, cavity_dark, 1), 'MULTIPLY')
            fac = self.maprange(self.cavity(cavity_dist), 0.25, 1.0)
            col = self.mixc(fac, dark, col)
        if top_light:
            fac = self.maprange(self.normal_z(), -0.2, 1.0, 0.0, top_light)
            col = self.mixc(fac, col, (1, 1, 1, 1), 'SCREEN')
        if edge_amount and edge_color is not None:
            fac = self.math('MULTIPLY', self.edge(edge_radius), edge_amount, clamp=True)
            col = self.mixc(fac, col, edge_color)
        return col

    def principled(self, **inputs):
        b = self.node('ShaderNodeBsdfPrincipled')
        for k, v in inputs.items():
            self.feed(b.inputs[k], v)
        self.links.new(b.outputs[0], self.out.inputs['Surface'])
        return b


def _new(name):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    return m, NodeBuilder(m)


def cached(fn):
    def wrapper(*args, **kwargs):
        key = (fn.__name__, args, tuple(sorted(kwargs.items())))
        mat = _CACHE.get(key)
        if mat is None or mat.name not in bpy.data.materials:
            mat = fn(*args, **kwargs)
            _CACHE[key] = mat
        return mat
    return wrapper


def reset_cache():
    _CACHE.clear()


@cached
def metal(color='8e969b', rough=0.3, name='Steel', edge=1.55, cavity=0.42, hammer=0.25, wear=0.0,
          tint_var=0.12, scale=1.0):
    m, nb = _new(name)
    base = srgb(color)
    var = nb.noise(5 * scale, 4, .6)
    col = nb.mixc(nb.maprange(var, .3, .7, 0, tint_var), base, scale_rgb(base, .72))
    if wear:
        rust = nb.noise(3 * scale, 6, .7, distortion=.4)
        mask = nb.math('MULTIPLY', nb.maprange(rust, .52, .62), wear, clamp=True)
        mask = nb.math('MAXIMUM', mask, nb.math('MULTIPLY', nb.maprange(nb.cavity(.1), .7, .2), wear))
        col = nb.mixc(mask, col, srgb('5a3a24'))
    col = nb.finish_color(col, cavity, .1, .08, scale_rgb(base, edge), .9)
    scratch = nb.noise(60 * scale, 2, .5, vec=nb.scaled((1, 1, 14)))
    r = nb.math('ADD', rough, nb.maprange(nb.noise(9 * scale, 3, .6), .3, .7, -.08, .1))
    r = nb.math('ADD', r, nb.maprange(scratch, .5, .8, 0, .12))
    if wear:
        r = nb.mixf(mask, r, .85)
    hammered = nb.voronoi(26 * scale, 'SMOOTH_F1')
    nrm = nb.bump(hammered, hammer * .3, .01)
    nb.principled(**{'Base Color': col, 'Metallic': 1.0 if not wear else nb.math('SUBTRACT', 1.0, mask),
                     'Roughness': r, 'Normal': nrm})
    return m


@cached
def gold(color='c9973f', rough=0.26, name='Gilded brass', edge=1.45, cavity=0.38):
    m, nb = _new(name)
    base = srgb(color)
    var = nb.noise(7, 3, .5)
    col = nb.mixc(nb.maprange(var, .35, .65, 0, .25), base, scale_rgb(srgb('7a4d1c'), 1))
    col = nb.finish_color(col, cavity, .08, .06, scale_rgb(base, edge), 1.0)
    r = nb.math('ADD', rough, nb.maprange(nb.noise(14, 3, .6), .3, .7, -.08, .12))
    nb.principled(**{'Base Color': col, 'Metallic': 1.0, 'Roughness': r,
                     'Normal': nb.bump(nb.noise(30, 2, .5), .05, .01)})
    return m


@cached
def paint(color, name='Enamel paint', under='6d7275', chip=0.35, rough=0.42, scale=1.0):
    """Painted heraldic surfaces: satin paint chipped at the edges back to steel."""
    m, nb = _new(name)
    base = srgb(color)
    var = nb.noise(4 * scale, 3, .5)
    col = nb.mixc(nb.maprange(var, .3, .7, 0, .18), base, scale_rgb(base, .78))
    edgem = nb.edge(.014, .02, .14)
    chips = nb.maprange(nb.noise(18 * scale, 5, .7), .45, .62)
    mask = nb.math('MULTIPLY', nb.math('MULTIPLY', edgem, chips), chip * 2.5, clamp=True)
    col = nb.finish_color(col, .55, .1, .1)
    col = nb.mixc(mask, col, srgb(under))
    nb.principled(**{'Base Color': col, 'Metallic': mask, 'Roughness': nb.mixf(mask, rough, .3),
                     'Coat Weight': .15, 'Coat Roughness': .3,
                     'Normal': nb.bump(nb.noise(40, 2, .5), .03, .01)})
    return m


@cached
def cloth(color, name='Woven cloth', var='', weave=60.0, sheen=0.6, rough=0.86, edge_light=0.12,
          cavity=0.5, emissive_trim=None, scale=1.0):
    m, nb = _new(name)
    base = srgb(color)
    alt = srgb(var) if var else scale_rgb(base, .7)
    n = nb.noise(3.2 * scale, 4, .6)
    col = nb.mixc(nb.maprange(n, .25, .75), base, alt)
    col = nb.mixc(nb.maprange(nb.noise(16 * scale, 3, .6), .4, .7, 0, .12), col, scale_rgb(base, 1.25))
    col = nb.finish_color(col, cavity, .14, .1, scale_rgb(base, 1.35), edge_light, .02)
    w1 = nb.wave(weave * scale, 0, 0, 'X', profile='SIN')
    w2 = nb.wave(weave * scale, 0, 0, 'Y', profile='SIN')
    h = nb.math('MULTIPLY', nb.math('ADD', w1, w2), .5)
    h = nb.math('ADD', h, nb.math('MULTIPLY', nb.noise(10 * scale, 3, .6), .6))
    nb.principled(**{'Base Color': col, 'Roughness': rough, 'Sheen Weight': sheen,
                     'Sheen Roughness': .45, 'Sheen Tint': scale_rgb(base, 1.6),
                     'Normal': nb.bump(h, .12, .01)})
    return m


@cached
def leather(color='4a3324', name='Worked leather', rough=0.58, edge=1.5, scale=1.0):
    m, nb = _new(name)
    base = srgb(color)
    n = nb.noise(6 * scale, 5, .65)
    col = nb.mixc(nb.maprange(n, .3, .7, 0, .35), base, scale_rgb(base, .62))
    col = nb.finish_color(col, .5, .1, .1, scale_rgb(base, edge), .7, .016)
    pores = nb.voronoi(70 * scale, 'F1')
    h = nb.math('ADD', nb.math('MULTIPLY', pores, .4), nb.math('MULTIPLY', n, .6))
    r = nb.math('ADD', rough, nb.maprange(n, .3, .7, -.12, .12))
    nb.principled(**{'Base Color': col, 'Roughness': r, 'Sheen Weight': .15, 'Normal': nb.bump(h, .18, .01)})
    return m


@cached
def wood(color='6b4a2e', name='Oak', axis='Z', grain=6.0, rough=0.7, dark=0.55, edge=1.35, scale=1.0, worn=0.6):
    m, nb = _new(name)
    base = srgb(color)
    stretch = {'X': (1, 7, 7), 'Y': (7, 1, 7), 'Z': (7, 7, 1)}[axis]
    vec = nb.scaled(tuple(s * scale for s in stretch))
    g = nb.noise(grain, 6, .62, vec=vec, distortion=1.6)
    rings = nb.maprange(g, .38, .62)
    col = nb.ramp(rings, [(0, scale_rgb(base, dark)), (.55, base), (1, scale_rgb(base, 1.25))])
    knots = nb.maprange(nb.noise(2.2 * scale, 2, .5), .62, .72)
    col = nb.mixc(nb.math('MULTIPLY', knots, .45), col, scale_rgb(base, .45))
    col = nb.finish_color(col, .5, .12, .12, scale_rgb(base, edge), worn, .015)
    fib = nb.noise(grain * 6, 3, .6, vec=vec)
    h = nb.math('ADD', nb.math('MULTIPLY', rings, .6), nb.math('MULTIPLY', fib, .4))
    nb.principled(**{'Base Color': col, 'Roughness': nb.math('ADD', rough, nb.maprange(fib, .3, .7, -.06, .08)),
                     'Normal': nb.bump(h, .22, .012)})
    return m


@cached
def skin(color='c3927a', name='Skin', sss=0.1, rough=0.55, blush='a8604f', undead=False):
    m, nb = _new(name)
    base = srgb(color)
    n = nb.noise(5, 4, .6)
    col = nb.mixc(nb.maprange(n, .35, .7, 0, .18), base, srgb(blush))
    if undead:
        veins = nb.maprange(nb.voronoi(9, 'DISTANCE_TO_EDGE'), 0.0, .05, 1, 0)
        col = nb.mixc(nb.math('MULTIPLY', veins, .35), col, srgb('3c2a3a'))
    col = nb.finish_color(col, .6, .08, .1)
    nb.principled(**{'Base Color': col, 'Roughness': nb.math('ADD', rough, nb.maprange(n, .3, .7, -.1, .1)),
                     'Subsurface Weight': sss, 'Subsurface Radius': (.9, .35, .2), 'Subsurface Scale': .03,
                     'Normal': nb.bump(nb.noise(40, 3, .6), .05, .01)})
    return m


@cached
def bone(color='d8cba6', name='Old bone', rough=0.62, crack=0.5, stain='6b5332'):
    m, nb = _new(name)
    base = srgb(color)
    n = nb.noise(4, 5, .65)
    col = nb.mixc(nb.maprange(n, .3, .75, 0, .5), base, srgb(stain))
    cr = nb.maprange(nb.voronoi(7, 'DISTANCE_TO_EDGE', vec=None), 0, .025, 1, 0)
    crn = nb.math('MULTIPLY', cr, nb.maprange(nb.noise(3, 2, .5), .45, .6))
    col = nb.mixc(nb.math('MULTIPLY', crn, crack), col, srgb('3b2b1c'))
    col = nb.finish_color(col, .38, .14, .1, scale_rgb(base, 1.15), .4)
    h = nb.math('SUBTRACT', nb.math('MULTIPLY', n, .5), nb.math('MULTIPLY', crn, crack * .8))
    nb.principled(**{'Base Color': col, 'Roughness': rough, 'Subsurface Weight': .08,
                     'Subsurface Radius': (.6, .5, .3), 'Subsurface Scale': .02,
                     'Normal': nb.bump(h, .25, .012)})
    return m


@cached
def stone(color='8a8478', name='Dressed stone', rough=0.86, moss=0.0, scale=1.0, edge=1.3, dark=0.45):
    m, nb = _new(name)
    base = srgb(color)
    n = nb.noise(3 * scale, 6, .65)
    # Per-stone tonal variation (grey value per cell, never random hues).
    cell = nb.voronoi(5 * scale, 'F1', output='Distance')
    tone = nb.noise(1.0, 2, .5, vec=nb.voronoi(5 * scale, 'F1', output='Position'))
    col = nb.mixc(nb.maprange(tone, .3, .7, 0, .35), base, scale_rgb(base, .72))
    col = nb.mixc(nb.maprange(cell, .0, .6, 0, .15), col, scale_rgb(base, 1.15))
    col = nb.mixc(nb.maprange(n, .3, .7, 0, .45), col, scale_rgb(base, .6))
    if moss:
        up = nb.maprange(nb.normal_z(), .2, .8)
        mm = nb.math('MULTIPLY', nb.math('MULTIPLY', up, nb.maprange(nb.noise(5 * scale, 4, .6), .45, .6)), moss)
        col = nb.mixc(mm, col, srgb('4f6232'))
    col = nb.finish_color(col, dark, .2, .1, scale_rgb(base, edge), .8, .02)
    h = nb.math('ADD', n, nb.math('MULTIPLY', nb.noise(20 * scale, 4, .7), .4))
    nb.principled(**{'Base Color': col, 'Roughness': rough, 'Normal': nb.bump(h, .35, .03)})
    return m


@cached
def emissive(color, strength=6.0, name='Glow', core='', flicker=0.35, base=''):
    m, nb = _new(name)
    c = srgb(color)
    hot = srgb(core) if core else scale_rgb(c, 1.6)
    n = nb.noise(6, 3, .55)
    em = nb.mixc(nb.maprange(n, .35, .7), c, hot)
    st = nb.math('MULTIPLY', strength, nb.maprange(n, .2, .8, 1 - flicker, 1 + flicker))
    nb.principled(**{'Base Color': srgb(base) if base else scale_rgb(c, .6), 'Roughness': .35,
                     'Emission Color': em, 'Emission Strength': st})
    return m


@cached
def glass(color='9fe6ff', name='Glass', ior=1.5, rough=0.04, glow=0.0, tint_density=1.0):
    m, nb = _new(name)
    c = srgb(color)
    nb.principled(**{'Base Color': c, 'Transmission Weight': 1.0, 'IOR': ior, 'Roughness': rough,
                     'Emission Color': c, 'Emission Strength': glow, 'Coat Weight': .3})
    return m


@cached
def gem(color='c23a4a', name='Gem', glow=1.2):
    m, nb = _new(name)
    c = srgb(color)
    edge = nb.edge(.01, .02, .2)
    col = nb.mixc(edge, c, scale_rgb(c, 1.8))
    nb.principled(**{'Base Color': col, 'Transmission Weight': .65, 'IOR': 1.9, 'Roughness': .06,
                     'Specular IOR Level': .8, 'Emission Color': c, 'Emission Strength': glow,
                     'Coat Weight': .6, 'Coat Roughness': .02})
    return m


@cached
def mail(color='8a9196', name='Chainmail', rough=0.38, ring=180.0, wear=0.0):
    """Riveted mail: fine ring texture, dark gaps, soft sparkle; reads as grey sheen at sprite size."""
    m, nb = _new(name)
    base = srgb(color)
    rings = nb.voronoi(ring, 'DISTANCE_TO_EDGE', vec=nb.scaled((1, 1, 1.6)))
    gap = nb.maprange(rings, 0.0, 0.12, 0, 1)
    col = nb.mixc(gap, scale_rgb(base, .25), base)
    if wear:
        rust = nb.maprange(nb.noise(4, 5, .65), .5, .62, 0, wear)
        col = nb.mixc(rust, col, srgb('5c3a22'))
    col = nb.finish_color(col, .5, .12, .06)
    nb.principled(**{'Base Color': col, 'Metallic': nb.mixf(gap, .3, 1.0), 'Roughness': nb.mixf(gap, .8, rough),
                     'Normal': nb.bump(gap, .35, .004)})
    return m


@cached
def flesh(color='7c8a6e', name='Rotting flesh', wet=0.4, rot='4a3f2c', vein='5d2a3a'):
    m, nb = _new(name)
    base = srgb(color)
    n = nb.noise(4, 5, .65)
    col = nb.mixc(nb.maprange(n, .3, .75, 0, .6), base, srgb(rot))
    veins = nb.maprange(nb.voronoi(6, 'DISTANCE_TO_EDGE'), 0, .04, 1, 0)
    col = nb.mixc(nb.math('MULTIPLY', veins, .5), col, srgb(vein))
    col = nb.finish_color(col, .45, .12, .08)
    wetm = nb.maprange(nb.noise(7, 3, .6), .5, .65)
    r = nb.mixf(nb.math('MULTIPLY', wetm, wet), .7, .18)
    nb.principled(**{'Base Color': col, 'Roughness': r, 'Subsurface Weight': .12,
                     'Subsurface Radius': (.7, .45, .25), 'Subsurface Scale': .03,
                     'Normal': nb.bump(nb.math('ADD', n, nb.math('MULTIPLY', veins, .3)), .3, .02)})
    return m


@cached
def fur(color='5b4532', name='Fur', tip='', rough=0.9, scale=1.0):
    m, nb = _new(name)
    base = srgb(color)
    strands = nb.noise(90 * scale, 2, .5, vec=nb.scaled((1, 1, 9)))
    col = nb.mixc(nb.maprange(strands, .3, .8), scale_rgb(base, .6), srgb(tip) if tip else scale_rgb(base, 1.15))
    col = nb.mixc(nb.maprange(nb.noise(4, 3, .5), .3, .7, 0, .3), col, scale_rgb(base, .7))
    col = nb.finish_color(col, .45, .15, .05)
    nb.principled(**{'Base Color': col, 'Roughness': rough, 'Sheen Weight': .35, 'Sheen Roughness': .5,
                     'Sheen Tint': scale_rgb(base, 1.4), 'Normal': nb.bump(strands, .45, .02)})
    return m


@cached
def liquid(color='5fe08a', name='Potion', glow=2.5):
    m, nb = _new(name)
    c = srgb(color)
    nb.principled(**{'Base Color': c, 'Transmission Weight': .6, 'Roughness': .05, 'IOR': 1.33,
                     'Emission Color': c, 'Emission Strength': glow, 'Subsurface Weight': .3,
                     'Subsurface Radius': (1, 1, 1), 'Subsurface Scale': .05})
    return m


@cached
def eye(color='18120e', name='Eye', glow=0.0):
    m, nb = _new(name)
    c = srgb(color)
    nb.principled(**{'Base Color': c, 'Roughness': .08, 'Coat Weight': 1.0, 'Coat Roughness': .02,
                     'Emission Color': c, 'Emission Strength': glow})
    return m


@cached
def flat(color, name='Flat', rough=0.6, metallic=0.0):
    m, nb = _new(name)
    nb.principled(**{'Base Color': srgb(color), 'Roughness': rough, 'Metallic': metallic})
    return m


@cached
def rope(color='9b7b52', name='Rope'):
    m, nb = _new(name)
    base = srgb(color)
    twist = nb.wave(40, 2, 2, 'Z', profile='SAW')
    col = nb.mixc(nb.maprange(twist, .2, .9), scale_rgb(base, .6), base)
    col = nb.finish_color(col, .5, .05, .1)
    nb.principled(**{'Base Color': col, 'Roughness': .9, 'Normal': nb.bump(twist, .3, .01)})
    return m


@cached
def volume_glow(color, density=0.6, strength=3.0, name='Glow volume'):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    vol = nt.nodes.new('ShaderNodeVolumePrincipled')
    vol.inputs['Density'].default_value = density
    vol.inputs['Color'].default_value = srgb(color)
    vol.inputs['Emission Color'].default_value = srgb(color)
    vol.inputs['Emission Strength'].default_value = strength
    nt.links.new(vol.outputs[0], out.inputs['Volume'])
    return m


@cached
def ember_metal(color='2b2725', glow='ff6a1a', name='Ember-cracked iron', strength=9.0, crack=0.035, scale=6.0,
                coverage=0.35):
    """Charred plate with glowing cracks (forge and ash bosses). Higher coverage = sparser cracks."""
    m, nb = _new(name)
    base = srgb(color)
    cr = nb.maprange(nb.voronoi(scale, 'DISTANCE_TO_EDGE'), 0.0, crack, 1, 0)
    cr = nb.math('MULTIPLY', cr, nb.maprange(nb.noise(3, 3, .5), coverage, coverage + .2))
    col = nb.finish_color(nb.mixc(nb.maprange(nb.noise(8, 4, .6), .3, .7, 0, .4), base, scale_rgb(base, 1.8)),
                          .5, .1, .06, scale_rgb(base, 2.2), .6)
    nb.principled(**{'Base Color': nb.mixc(cr, col, srgb(glow)), 'Metallic': nb.mixf(cr, .85, 0), 'Roughness': .5,
                     'Emission Color': srgb(glow), 'Emission Strength': nb.math('MULTIPLY', cr, strength),
                     'Normal': nb.bump(nb.math('SUBTRACT', 0, cr), .3, .01)})
    return m


@cached
def ermine(name='Ermine'):
    m, nb = _new(name)
    spots = nb.maprange(nb.voronoi(18, 'F1'), 0.0, 0.12, 1, 0)
    col = nb.mixc(spots, srgb('cfc4b0'), srgb('1a1612'))
    col = nb.finish_color(col, .6, .1, .1)
    nb.principled(**{'Base Color': col, 'Roughness': .9, 'Sheen Weight': 1.0, 'Sheen Roughness': .4,
                     'Normal': nb.bump(nb.noise(120, 2, .5, vec=nb.scaled((1, 1, 6))), .4, .01)})
    return m


@cached
def coral(color='3f7f78', name='Barnacled coral'):
    m, nb = _new(name)
    base = srgb(color)
    v = nb.voronoi(14, 'F1')
    bump = nb.maprange(v, 0.0, 0.35, 1, 0)
    col = nb.mixc(nb.math('MULTIPLY', bump, .7), base, srgb('d8d0b8'))
    col = nb.finish_color(col, .4, .1, .08, scale_rgb(base, 1.5), .5)
    nb.principled(**{'Base Color': col, 'Roughness': .55, 'Coat Weight': .4, 'Coat Roughness': .2,
                     'Normal': nb.bump(bump, .5, .02)})
    return m
