"""Watercraft and water for the backdrops: a convincing sea surface, moored sailing ships and rowboats."""
import math
import random

import bpy
from mathutils import Vector

from rk import arch, env, geo
from rk import dressing as P
from rk.core import srgb


def sea(name='Sea', deep='0e2c36', wave=0.6, scale=1.0, rough=0.035):
    """Dark water that takes its colour from what it reflects: Fresnel sky, wave normals at two scales."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    N, Lk = nt.nodes, nt.links
    bsdf = next(n for n in N if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = (*srgb(deep)[:3], 1.0)
    bsdf.inputs['Roughness'].default_value = rough
    bsdf.inputs['IOR'].default_value = 1.33
    geo_n = N.new('ShaderNodeNewGeometry')
    big = N.new('ShaderNodeVectorMath')
    big.operation = 'MULTIPLY'
    big.inputs[1].default_value = (0.35 * scale, 1.4 * scale, 1.0)
    Lk.new(geo_n.outputs['Position'], big.inputs[0])
    n1 = N.new('ShaderNodeTexNoise')
    n1.inputs['Scale'].default_value = 1.2
    n1.inputs['Detail'].default_value = 6.0
    Lk.new(big.outputs[0], n1.inputs['Vector'])
    small = N.new('ShaderNodeVectorMath')
    small.operation = 'MULTIPLY'
    small.inputs[1].default_value = (2.0 * scale, 6.0 * scale, 1.0)
    Lk.new(geo_n.outputs['Position'], small.inputs[0])
    n2 = N.new('ShaderNodeTexNoise')
    n2.inputs['Scale'].default_value = 2.0
    n2.inputs['Detail'].default_value = 4.0
    Lk.new(small.outputs[0], n2.inputs['Vector'])
    add = N.new('ShaderNodeMath')
    add.operation = 'ADD'
    Lk.new(n1.outputs['Fac'], add.inputs[0])
    m2 = N.new('ShaderNodeMath')
    m2.operation = 'MULTIPLY'
    m2.inputs[1].default_value = 0.5
    Lk.new(n2.outputs['Fac'], m2.inputs[0])
    Lk.new(m2.outputs[0], add.inputs[1])
    bump = N.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = wave
    bump.inputs['Distance'].default_value = 0.08
    Lk.new(add.outputs[0], bump.inputs['Height'])
    Lk.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    return mat


def water(x0, x1, y0, y1, z=0.0, mat=None, name='Water', res=(8, 8)):
    mat = mat or sea()
    return geo.grid_sheet(name, 1, 1, res[0], res[1], lambda u, v: (x0 + (x1 - x0) * u, y0 + (y1 - y0) * v, z), mat,
                          env.C['Terrain'])


def _hull_sections(L, B, free, draft, n=13, castle=0.0):
    secs = []
    for i in range(n):
        t = i / (n - 1)                                   # 0 stern .. 1 bow
        x = -L / 2 + t * L
        bow = t ** 2.2
        w = B / 2 * (math.sin(math.pi * (0.12 + 0.88 * t)) ** 0.55) * (1 - 0.15 * bow) + 0.02
        top = free + 0.05 * L * (2 * t - 1) ** 2 + (castle if t < 0.2 else 0.0)
        keel = -draft * (math.sin(math.pi * (0.06 + 0.94 * t)) ** 0.4)
        secs.append([Vector((x, w, top)), Vector((x, w * 0.97, 0.0)), Vector((x, w * 0.62, keel * 0.75)),
                     Vector((x, 0.0, keel)), Vector((x, -w * 0.62, keel * 0.75)), Vector((x, -w * 0.97, 0.0)),
                     Vector((x, -w, top))])
    return secs


def ship(loc, L=16.0, rot=0.0, masts=2, seed=0, hull='3a2a1e', band='9a7a34', flag='1c6e69', sails=None):
    """A moored sailing ship with real freeboard: hull, stern castle, wale, masts, yards, furled sails,
    shrouds, a bowsprit and a pennant. Built at its waterline (loc z = water level)."""
    rng = random.Random(seed)
    M = P.pm()
    out = []
    B, free, draft = L * 0.27, L * 0.11, L * 0.07
    planks = env.planks(f'Hull {hull}', hull, board=0.22, length=3.0, scale=1.0)
    hull_o = geo.loft('Ship hull', _hull_sections(L, B, free, draft, castle=L * 0.05), planks, None, closed=True, cap=True)
    env.finish(hull_o, coll=env.C['Props'])
    out.append(hull_o)
    wale = env.flat(band, 0.6, f'Wale {band}')
    for s in (-1, 1):
        o = geo.box('Wale', (L * 0.86, 0.08, L * 0.018), (L * 0.02, s * (B / 2 * 0.98), free * 0.62), wale, None, bevel=0.02)
        env.finish(o, coll=env.C['Props'])
        out.append(o)
    canvas = sails or env.flat('a89a7e', 0.95, 'Furled canvas')
    wood = M['wood_dark']
    xs = [-0.2, 0.12, 0.36][:masts] if masts > 1 else [0.05]
    for i, fx in enumerate(xs):
        x = fx * L
        h = L * (1.0 if i == 1 or masts == 1 else 0.86) * rng.uniform(0.96, 1.04)
        base = free * 0.9
        out.append(env.finish(geo.cylinder('Mast', 0.022 * L, h, (x, 0, base + h / 2), wood, None, 10, bevel=0,
                                           radius2=0.012 * L), coll=env.C['Props']))
        for k, t in enumerate((0.42, 0.66, 0.86)):
            yard = L * (0.5 - k * 0.12)
            z = base + h * t
            out.append(env.finish(geo.cylinder('Yard', 0.009 * L, yard, (x, 0, z), wood, None, 6, bevel=0,
                                               rotation=(90, 0, 0)), coll=env.C['Props']))
            out.append(env.finish(geo.cylinder('Furled sail', 0.014 * L, yard * 0.86, (x + 0.02 * L, 0, z - 0.02 * L), canvas,
                                               None, 8, bevel=0, rotation=(90, 0, 0)), coll=env.C['Props']))
        top = Vector((x, 0, base + h * 0.9))
        for s in (-1, 1):
            for dx in (-0.05, 0.0, 0.05):
                arch.beam(top, Vector((x + dx * L, s * B / 2 * 0.95, free + 0.1)), 0.035, wood, out, 'Shroud')
        pen = geo.grid_sheet('Pennant', 1, 1, 6, 2, lambda u, v: (x - u * L * 0.12, 0.02 * math.sin(u * 6),
                                                                  base + h + 0.2 + (v - 0.5) * L * 0.025 * (1 - u)),
                             env.flat(flag, 0.8, f'Pennant {flag}'), None)
        env.finish(pen, coll=env.C['Props'])
        out.append(pen)
    bow = Vector((L / 2 - 0.2, 0, free + L * 0.05))
    out.append(env.finish(geo.cylinder('Bowsprit', 0.015 * L, L * 0.32, (0, 0, 0), wood, None, 8, bevel=0), coll=env.C['Props']))
    geo.transform(out[-1], _segment(bow, bow + Vector((L * 0.28, 0, L * 0.12))))
    if masts > 1:
        arch.beam(bow + Vector((L * 0.26, 0, L * 0.11)), Vector((xs[-1] * L, 0, free * 0.9 + L * 0.8)), 0.035, wood, out, 'Stay')
    env.group_xform(out, loc, rot)
    return out


def _segment(a, b):
    from mathutils import Matrix
    d = b - a
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    return Matrix.Translation((a + b) / 2) @ q.to_matrix().to_4x4()


def rowboat(loc, L=4.6, rot=0.0, seed=0, hull='5a4030'):
    """A solid little boat with a plank floor and thwarts, built at its waterline."""
    out = []
    B, free, draft = L * 0.34, L * 0.1, L * 0.06
    planks = env.planks(f'Boat {hull}', hull, board=0.12, length=2.0)
    secs = []
    for i in range(9):
        t = i / 8
        x = -L / 2 + t * L
        w = B / 2 * math.sin(math.pi * (0.1 + 0.8 * t)) ** 0.6 + 0.02
        top = free + 0.06 * L * (2 * t - 1) ** 2
        k = -draft * math.sin(math.pi * (0.05 + 0.9 * t)) ** 0.5
        secs.append([Vector((x, w, top)), Vector((x, w * 0.8, k * 0.6)), Vector((x, 0, k)), Vector((x, -w * 0.8, k * 0.6)),
                     Vector((x, -w, top))])
    shell = geo.loft('Rowboat', secs, planks, None, closed=False, cap=False)
    mod = shell.modifiers.new('Thick', 'SOLIDIFY')
    mod.thickness = 0.06
    env.finish(shell, coll=env.C['Props'])
    out.append(shell)
    floor = env.planks('Boat floor', '6a4c34', board=0.1, length=1.5)
    o = geo.box('Boat floor', (L * 0.7, B * 0.5, 0.04), (0, 0, -draft * 0.35), floor, None, bevel=0.01)
    env.finish(o, coll=env.C['Props'])
    out.append(o)
    for fx in (-0.18, 0.12):
        o = geo.box('Thwart', (0.22, B * 0.85, 0.05), (fx * L, 0, free * 0.55), floor, None, bevel=0.01)
        env.finish(o, coll=env.C['Props'])
        out.append(o)
    env.group_xform(out, loc, rot)
    return out
