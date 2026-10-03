"""Small, self-contained shader-node builder used by the FX and environment kits.

Independent from shaders.NodeBuilder on purpose (that class is shared by the unit
pipeline); this one adds volume outputs, explicit coordinate spaces and vector maths.
"""
import bpy

from .core import srgb


class Nodes:
    def __init__(self, mat_or_tree, output='material'):
        if isinstance(mat_or_tree, bpy.types.Material):
            mat_or_tree.use_nodes = True
            self.nt = mat_or_tree.node_tree
            self.nt.nodes.clear()
            self.out = self.nt.nodes.new('ShaderNodeOutputMaterial')
        else:
            self.nt = mat_or_tree
            self.nt.nodes.clear()
            self.out = self.nt.nodes.new('ShaderNodeOutputWorld')
        self.nodes = self.nt.nodes
        self.links = self.nt.links
        self._tc = None
        self._geo = None

    # -------------------------------------------------------------- basics
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
        elif isinstance(value, str):
            socket.default_value = srgb(value)
        else:
            socket.default_value = value

    def tc(self, which='Object'):
        if self._tc is None:
            self._tc = self.node('ShaderNodeTexCoord')
        return self._tc.outputs[which]

    def geom(self, which):
        if self._geo is None:
            self._geo = self.node('ShaderNodeNewGeometry')
        return self._geo.outputs[which]

    def attr(self, name, kind='GEOMETRY', out='Vector'):
        a = self.node('ShaderNodeAttribute', attribute_type=kind, attribute_name=name)
        return a.outputs[out]

    def rest(self):
        """`rest` attribute when present, else object coordinates (matches the kit convention)."""
        attr = self.attr('rest')
        ln = self.vmath('LENGTH', attr)
        has = self.math('GREATER_THAN', ln, 1e-6)
        return self.mixv(has, self.tc('Object'), attr)

    # -------------------------------------------------------------- maths
    def math(self, op, a, b=None, c=None, clamp=False):
        n = self.node('ShaderNodeMath', operation=op, use_clamp=clamp)
        self.feed(n.inputs[0], a)
        if b is not None:
            self.feed(n.inputs[1], b)
        if c is not None:
            self.feed(n.inputs[2], c)
        return n.outputs[0]

    def add(self, a, b):
        return self.math('ADD', a, b)

    def mul(self, a, b, clamp=False):
        return self.math('MULTIPLY', a, b, clamp=clamp)

    def vmath(self, op, a, b=None, scale=None, out=None):
        n = self.node('ShaderNodeVectorMath', operation=op)
        self.feed(n.inputs[0], a)
        if b is not None:
            self.feed(n.inputs[1], b)
        if scale is not None:
            self.feed(n.inputs['Scale'], scale)
        if out is None:
            out = 'Value' if op in ('LENGTH', 'DOT_PRODUCT', 'DISTANCE') else 'Vector'
        return n.outputs[out]

    def xyz(self, vec):
        n = self.node('ShaderNodeSeparateXYZ')
        self.feed(n.inputs[0], vec)
        return n.outputs['X'], n.outputs['Y'], n.outputs['Z']

    def combine(self, x=0.0, y=0.0, z=0.0):
        n = self.node('ShaderNodeCombineXYZ')
        self.feed(n.inputs[0], x)
        self.feed(n.inputs[1], y)
        self.feed(n.inputs[2], z)
        return n.outputs[0]

    def maprange(self, value, a, b, c=0.0, d=1.0, clamp=True, interp='LINEAR'):
        n = self.node('ShaderNodeMapRange', interpolation_type=interp, clamp=clamp)
        self.feed(n.inputs['Value'], value)
        for key, v in (('From Min', a), ('From Max', b), ('To Min', c), ('To Max', d)):
            self.feed(n.inputs[key], v)
        return n.outputs['Result']

    def smooth(self, value, a, b, c=0.0, d=1.0):
        return self.maprange(value, a, b, c, d, interp='SMOOTHSTEP')

    def mixf(self, fac, a, b):
        n = self.node('ShaderNodeMix', data_type='FLOAT')
        self.feed(n.inputs[0], fac)
        self.feed(n.inputs[2], a)
        self.feed(n.inputs[3], b)
        return n.outputs[0]

    def mixv(self, fac, a, b):
        n = self.node('ShaderNodeMix', data_type='VECTOR')
        self.feed(n.inputs[0], fac)
        self.feed(n.inputs[4], a)
        self.feed(n.inputs[5], b)
        return n.outputs[1]

    def mixc(self, fac, a, b, blend='MIX'):
        n = self.node('ShaderNodeMix', data_type='RGBA', blend_type=blend)
        self.feed(n.inputs[0], fac)
        self.feed(n.inputs[6], a)
        self.feed(n.inputs[7], b)
        return n.outputs[2]

    def ramp(self, fac, stops, interp='LINEAR'):
        n = self.node('ShaderNodeValToRGB')
        n.color_ramp.interpolation = interp
        els = n.color_ramp.elements
        while len(els) < len(stops):
            els.new(0.5)
        for el, (pos, col) in zip(els, stops):
            el.position = pos
            if isinstance(col, str):
                col = srgb(col)
            elif isinstance(col, (int, float)):
                col = (col, col, col, 1.0)
            el.color = tuple(col) + ((1.0,) if len(col) == 3 else ())
        self.feed(n.inputs['Fac'], fac)
        return n.outputs['Color']

    def hsv(self, color, hue=0.5, sat=1.0, val=1.0):
        n = self.node('ShaderNodeHueSaturation')
        self.feed(n.inputs['Hue'], hue)
        self.feed(n.inputs['Saturation'], sat)
        self.feed(n.inputs['Value'], val)
        self.feed(n.inputs['Color'], color)
        return n.outputs['Color']

    # -------------------------------------------------------------- textures
    def noise(self, vec, scale=4.0, detail=4.0, rough=0.55, distortion=0.0, dims='3D', lac=2.0, out='Fac', w=None):
        n = self.node('ShaderNodeTexNoise', noise_dimensions=dims)
        self.feed(n.inputs['Vector'], vec)
        n.inputs['Scale'].default_value = scale
        n.inputs['Detail'].default_value = detail
        n.inputs['Roughness'].default_value = rough
        n.inputs['Lacunarity'].default_value = lac
        n.inputs['Distortion'].default_value = distortion
        if w is not None and 'W' in n.inputs:
            n.inputs['W'].default_value = w
        return n.outputs[out]

    def voronoi(self, vec, scale=6.0, feature='F1', metric='EUCLIDEAN', out='Distance', randomness=1.0, dims='3D'):
        n = self.node('ShaderNodeTexVoronoi', feature=feature, distance=metric, voronoi_dimensions=dims)
        self.feed(n.inputs['Vector'], vec)
        n.inputs['Scale'].default_value = scale
        n.inputs['Randomness'].default_value = randomness
        return n.outputs[out]

    def wave(self, vec, scale=3.0, distortion=0.0, detail=0.0, axis='X', kind='BANDS', profile='SIN', phase=0.0):
        n = self.node('ShaderNodeTexWave', wave_type=kind, wave_profile=profile)
        if kind == 'BANDS':
            n.bands_direction = axis
        else:
            n.rings_direction = axis
        self.feed(n.inputs['Vector'], vec)
        n.inputs['Scale'].default_value = scale
        n.inputs['Distortion'].default_value = distortion
        n.inputs['Detail'].default_value = detail
        n.inputs['Phase Offset'].default_value = phase
        return n.outputs['Fac']

    def gradient(self, vec, kind='LINEAR'):
        n = self.node('ShaderNodeTexGradient', gradient_type=kind)
        self.feed(n.inputs['Vector'], vec)
        return n.outputs['Fac']

    def bump(self, height, strength=0.2, distance=0.02, normal=None):
        n = self.node('ShaderNodeBump')
        self.feed(n.inputs['Height'], height)
        self.feed(n.inputs['Strength'], strength)
        self.feed(n.inputs['Distance'], distance)
        if normal is not None:
            self.feed(n.inputs['Normal'], normal)
        return n.outputs['Normal']

    def ao(self, distance=0.3, local=False, samples=8):
        n = self.node('ShaderNodeAmbientOcclusion', only_local=local, samples=samples)
        n.inputs['Distance'].default_value = distance
        return n.outputs['AO']

    def layer_weight(self, blend=0.3, out='Facing'):
        n = self.node('ShaderNodeLayerWeight')
        n.inputs['Blend'].default_value = blend
        return n.outputs[out]

    # -------------------------------------------------------------- shaders
    def principled(self, connect=True, **inputs):
        b = self.node('ShaderNodeBsdfPrincipled')
        for k, v in inputs.items():
            self.feed(b.inputs[k], v)
        if connect:
            self.links.new(b.outputs[0], self.out.inputs['Surface'])
        return b.outputs[0]

    def emission(self, color=(1, 1, 1, 1), strength=1.0):
        e = self.node('ShaderNodeEmission')
        self.feed(e.inputs['Color'], color)
        self.feed(e.inputs['Strength'], strength)
        return e.outputs[0]

    def transparent(self, color=(1, 1, 1, 1)):
        t = self.node('ShaderNodeBsdfTransparent')
        self.feed(t.inputs['Color'], color)
        return t.outputs[0]

    def add_shader(self, a, b):
        n = self.node('ShaderNodeAddShader')
        self.links.new(a, n.inputs[0])
        self.links.new(b, n.inputs[1])
        return n.outputs[0]

    def mix_shader(self, fac, a, b):
        n = self.node('ShaderNodeMixShader')
        self.feed(n.inputs[0], fac)
        self.links.new(a, n.inputs[1])
        self.links.new(b, n.inputs[2])
        return n.outputs[0]

    def volume(self, density=1.0, color=(1, 1, 1, 1), absorption=(0, 0, 0, 1), anisotropy=0.0, emission=0.0,
               emission_color=(1, 1, 1, 1), connect=True):
        v = self.node('ShaderNodeVolumePrincipled')
        self.feed(v.inputs['Density'], density)
        self.feed(v.inputs['Color'], color)
        self.feed(v.inputs['Absorption Color'], absorption)
        self.feed(v.inputs['Anisotropy'], anisotropy)
        self.feed(v.inputs['Emission Strength'], emission)
        self.feed(v.inputs['Emission Color'], emission_color)
        if connect:
            self.links.new(v.outputs[0], self.out.inputs['Volume'])
        return v.outputs[0]

    def surface(self, shader):
        self.links.new(shader, self.out.inputs['Surface'])

    def volume_out(self, shader):
        self.links.new(shader, self.out.inputs['Volume'])


def material(name):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    return m, Nodes(m)
