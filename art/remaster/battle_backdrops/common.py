"""Shared pieces for the layered zone backdrops: lighting, ground with a soft far edge, rows and scatter."""
import math
import random

from mathutils import Vector

from rk import arch, env
from rk.core import srgb
from battle_scenes.common import SUN_AZ

# The near layer is lit exactly like the sprites; distant layers see the same sun lower in the sky, as a viewer
# looking across a valley would, so far scenery is warm and backlit.
SUN_EL = {'near': 50.0, 'mid': 34.0, 'far': 14.0}


def sun_vec(az, el):
    a, e = math.radians(az), math.radians(el)
    return Vector((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e)))


def light(scene, layer, mood):
    """Painted sky (the glow sits low on the left, where the sun is) and the battle sun for this layer."""
    m = dict(zenith='4a6488', horizon='f4c48a', mid='f6dcb0', upper='a9bccb', ground='4a3a2c', glow='ffe2b0',
             glow_amount=0.85, glow_el=6.0, glow_az=104.0, clouds=0.6, cloud_lit='fff0d8', cloud_dark='b49a98', sun='ffd9a8',
             strength=4.4, sky_light=0.6, cloud_scale=1.0)
    m.update(mood)
    env.sky(scene, zenith=m['zenith'], horizon=m['horizon'], ground=m['ground'], sun_dir=tuple(sun_vec(m['glow_az'], m['glow_el'])),
            sun_glow=m['glow'], glow=m['glow_amount'], clouds=m['clouds'], cloud_lit=m['cloud_lit'],
            cloud_dark=m['cloud_dark'], strength=1.0, light_strength=m['sky_light'], mid=m['mid'], upper=m['upper'],
            cloud_scale=m['cloud_scale'])
    el = m.get('sun_el', {}).get(layer, SUN_EL[layer]) if isinstance(m.get('sun_el'), dict) else SUN_EL[layer]
    env.sun(tuple(sun_vec(SUN_AZ, el)), m['strength'], m['sun'], 2.5)
    return m


def soft_edge(mat, y0, y1, axis='Y'):
    """Copy of mat that fades to transparent from world coordinate y0 (opaque) to y1 (gone)."""
    m = mat.copy()
    m.name = mat.name + ' soft edge'
    nt = m.node_tree
    out = next((n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL' and n.is_active_output), None) or \
        next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
    src = out.inputs['Surface'].links[0].from_socket
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    rng = nt.nodes.new('ShaderNodeMapRange')
    rng.interpolation_type = 'SMOOTHSTEP'
    rng.clamp = True
    rng.inputs['From Min'].default_value = y0
    rng.inputs['From Max'].default_value = y1
    clear = nt.nodes.new('ShaderNodeBsdfTransparent')
    mix = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(geo.outputs['Position'], sep.inputs[0])
    nt.links.new(sep.outputs[axis], rng.inputs['Value'])
    nt.links.new(rng.outputs['Result'], mix.inputs[0])
    nt.links.new(src, mix.inputs[1])
    nt.links.new(clear.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs['Surface'])
    return m


def ground(L, mat, y0, y1, fade=None, height=None, masks=None, res=4.0, margin=12.0):
    """Ground plane from depth y0 to y1 across the layer; fade=(a, b) dissolves it between depths a and b."""
    if fade:
        mat = soft_edge(mat, *fade)
    w = L.W + margin * 2
    return env.terrain((w, y1 - y0), (max(8, int(w * res / 2)), max(8, int((y1 - y0) * res / 2))), (0, (y0 + y1) / 2),
                       height or (lambda x, y: 0.0), mat=mat, masks=masks or {})


def protos(prefix, n, fn):
    return [env.proto(f'{prefix}{i}', lambda i=i: fn(i)) for i in range(n)]


def scatter(L, items, n, y_range, seed=0, scale=(0.85, 1.15), min_gap=1.4, x_range=None, height=None, avoid=None,
            coll='Vegetation', rot=True):
    """Instance prototypes across the layer; avoid(x, y) -> True skips a spot."""
    rng = random.Random(seed)
    x0, x1 = x_range or (L.x0 - 4, L.x1 + 4)
    placed, tries = [], 0
    while len(placed) < n and tries < n * 60:
        tries += 1
        x, y = rng.uniform(x0, x1), rng.uniform(*y_range)
        if avoid and avoid(x, y):
            continue
        if any((x - a) ** 2 + (y - b) ** 2 < min_gap ** 2 for a, b in placed):
            continue
        placed.append((x, y))
        z = height(x, y) if height else 0.0
        env.inst(rng.choice(items), (x, y, z - 0.04), rng.uniform(0, math.tau) if rot else 0.0, rng.uniform(*scale),
                 env.C[coll])
    return placed


def front_scatter(fr, items, n, y_range, nominal_h, seed=0, **kw):
    """Scatter in front of the road, each instance scaled so it never rises over the band on screen."""
    rng = random.Random(seed)
    placed = []
    x0, x1 = fr.x0 - 3, fr.x1 + 3
    tries = 0
    while len(placed) < n and tries < n * 60:
        tries += 1
        x, y = rng.uniform(x0, x1), rng.uniform(*y_range)
        if any((x - a) ** 2 + (y - b) ** 2 < kw.get('min_gap', 1.6) ** 2 for a, b in placed):
            continue
        s = rng.uniform(*kw.get('scale', (0.85, 1.15))) * fr.fit(y, nominal_h)
        if s < 0.35:
            continue
        placed.append((x, y))
        env.inst(rng.choice(items), (x, y, -0.03), rng.uniform(0, math.tau), s, env.C[kw.get('coll', 'Props')])
    return placed


def spans(L, rng, widths, gaps, x0=None, x1=None):
    """Consecutive (centre, width) runs across the layer: the rhythm of a street frontage."""
    x = (L.x0 - 6) if x0 is None else x0
    end = (L.x1 + 6) if x1 is None else x1
    out = []
    while x < end:
        w = rng.uniform(*widths)
        out.append((x + w / 2, w))
        x += w + rng.uniform(*gaps)
    return out


def houses(L, rng, y, style='warm', floors=(1, 2), lit=0.5, seed=0, widths=(5.0, 7.0), gaps=(0.2, 0.8), M=None,
           x_runs=None, depth=(5.0, 6.5), pitch=(46, 56), scale=1.0):
    """A frontage of houses facing the camera along depth y; x_runs limits it to [(x0, x1), ...]."""
    M = M or arch.mats(style)
    out = []
    runs = x_runs or [(L.x0 - 6, L.x1 + 6)]
    i = 0
    for a, b in runs:
        x = a
        while x < b:
            w = rng.uniform(*widths) * scale
            d = rng.uniform(*depth) * scale
            out += arch.house((x + w / 2, y + d / 2, 0), 0.0, w, d, floors=rng.choice(floors), seed=seed * 50 + i,
                              style=style, lit=lit, h1=rng.uniform(2.8, 3.2) * scale, h2=rng.uniform(2.3, 2.7) * scale,
                              pitch=rng.uniform(*pitch), jetty=rng.uniform(0.2, 0.4) * scale, M=M)
            x += w + rng.uniform(*gaps)
            i += 1
    return out


def color(hexs):
    return srgb(hexs)[:3]


def patchwork(name, colors, cell=80.0, stretch=(1.0, 0.45), hedge='34421f', hedge_w=0.035, seed=0.0):
    """Distant farmland: fields in a palette, broken by dark hedgerows, with furrow streaks."""
    import bpy
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    N, Lk = nt.nodes, nt.links
    bsdf = next(n for n in N if n.type == 'BSDF_PRINCIPLED')
    geo = N.new('ShaderNodeNewGeometry')
    scale = N.new('ShaderNodeVectorMath')
    scale.operation = 'MULTIPLY'
    scale.inputs[1].default_value = (stretch[0] / cell, stretch[1] / cell, 0.0)
    Lk.new(geo.outputs['Position'], scale.inputs[0])
    off = N.new('ShaderNodeVectorMath')
    off.operation = 'ADD'
    off.inputs[1].default_value = (seed, seed * 0.7, 0.0)
    Lk.new(scale.outputs[0], off.inputs[0])
    cells = N.new('ShaderNodeTexVoronoi')
    cells.feature = 'F1'
    cells.inputs['Scale'].default_value = 1.0
    Lk.new(off.outputs[0], cells.inputs['Vector'])
    sep = N.new('ShaderNodeSeparateColor')
    Lk.new(cells.outputs['Color'], sep.inputs[0])
    ramp = N.new('ShaderNodeValToRGB')
    ramp.color_ramp.interpolation = 'CONSTANT'
    els = ramp.color_ramp.elements
    for i, c in enumerate(colors):
        e = els[i] if i < 2 else els.new(0.0)
        e.position = i / len(colors)
        e.color = (*srgb(c)[:3], 1.0)
    Lk.new(sep.outputs[0], ramp.inputs[0])
    edge = N.new('ShaderNodeTexVoronoi')
    edge.feature = 'DISTANCE_TO_EDGE'
    edge.inputs['Scale'].default_value = 1.0
    Lk.new(off.outputs[0], edge.inputs['Vector'])
    hedge_m = N.new('ShaderNodeMapRange')
    hedge_m.clamp = True
    hedge_m.inputs['From Min'].default_value = 0.0
    hedge_m.inputs['From Max'].default_value = hedge_w
    hedge_m.inputs['To Min'].default_value = 1.0
    hedge_m.inputs['To Max'].default_value = 0.0
    Lk.new(edge.outputs['Distance'], hedge_m.inputs['Value'])
    streak = N.new('ShaderNodeTexWave')
    streak.inputs['Scale'].default_value = 40.0
    streak.inputs['Distortion'].default_value = 2.0
    Lk.new(off.outputs[0], streak.inputs['Vector'])
    weak = N.new('ShaderNodeMath')
    weak.operation = 'MULTIPLY'
    weak.inputs[1].default_value = 0.1
    Lk.new(streak.outputs['Fac'], weak.inputs[0])
    shade = N.new('ShaderNodeMix')
    shade.data_type = 'RGBA'
    Lk.new(ramp.outputs['Color'], shade.inputs['A'])
    shade.inputs['B'].default_value = (0.02, 0.02, 0.01, 1.0)
    Lk.new(weak.outputs[0], shade.inputs['Factor'])
    mix = N.new('ShaderNodeMix')
    mix.data_type = 'RGBA'
    Lk.new(hedge_m.outputs['Result'], mix.inputs['Factor'])
    Lk.new(shade.outputs['Result'], mix.inputs['A'])
    mix.inputs['B'].default_value = (*srgb(hedge)[:3], 1.0)
    Lk.new(mix.outputs['Result'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.95
    return mat


def tree_protos(prefix, n, h, palette=('2f4220', '56692a', '98983c'), kind='broad', seed=300):
    """n tree prototypes of nominal height h: broad, conifer, cypress or dead."""
    if kind == 'conifer':
        return protos(prefix, n, lambda i: env.conifer(seed + i * 5, h * (0.85 + 0.12 * i), palette))
    if kind == 'cypress':
        return protos(prefix, n, lambda i: env.cypress(seed + i * 5, h * (0.85 + 0.12 * i), palette))
    if kind == 'dead':
        return protos(prefix, n, lambda i: env.dead_tree(seed + i * 5, h * (0.85 + 0.12 * i)))
    return protos(prefix, n, lambda i: env.broadleaf(seed + i * 7, h * (0.85 + 0.12 * i), 1.0 + 0.1 * i, palette))


def woods(L, items, clusters, depth, height, seed=0, spread=(22, 18), count=(4, 14), avoid=None, scale=(0.8, 1.3)):
    """Clumps of trees across a perspective vista (sizes stay real; distance does the shrinking)."""
    rng = random.Random(seed)
    for _ in range(clusters):
        cy = rng.uniform(*depth)
        cx = rng.uniform(-L.half_width(cy), L.half_width(cy))
        if avoid and avoid(cx, cy):
            continue
        for _ in range(rng.randint(*count)):
            x, y = cx + rng.gauss(0, spread[0] + cy * 0.02), cy + rng.gauss(0, spread[1] + cy * 0.015)
            env.inst(rng.choice(items), (x, y, height(x, y) - 0.5), rng.uniform(0, 6.28), rng.uniform(*scale),
                     env.C['Vegetation'])


def one(objs):
    """Join a builder's meshes into one object, for prototypes."""
    from rk import geo
    objs = list(objs) if isinstance(objs, (list, tuple)) else [objs]
    meshes = [o for o in objs if o.type == 'MESH']
    return geo.join(meshes, 'Proto') if len(meshes) > 1 else meshes[0]
