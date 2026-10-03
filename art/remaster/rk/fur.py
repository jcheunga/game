"""Particle-hair fur for coats, manes and pelts.

The particle system sits after the Armature modifier, so strands follow every
animation pose. Density is shaped by a vertex group; strands are combed by an
object-space direction and clumped for a groomed, stylised read.
"""
import bpy
from mathutils import Vector


def density_group(obj, fn, name='fur_density'):
    """fn(world co, material_index) -> 0..1 strand density."""
    g = obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
    me = obj.data
    vmat = {}
    for p in me.polygons:
        for vi in p.vertices:
            vmat.setdefault(vi, p.material_index)
    for v in me.vertices:
        w = max(0.0, min(1.0, fn(obj.matrix_world @ v.co, vmat.get(v.index, 0))))
        g.add([v.index], w, 'REPLACE')
    return name


def add_fur(obj, material, length=0.03, count=3000, children=8, comb=(-0.6, 0, -0.3), normal=0.6, clump=0.35,
            rough=0.012, radius=0.0035, tip=0.0, group=None, seed=0, kink=0.0, name='Fur'):
    if material.name not in [m.name for m in obj.data.materials if m]:
        obj.data.materials.append(material)
    mat_index = [m.name if m else '' for m in obj.data.materials].index(material.name)
    mod = obj.modifiers.new(name, 'PARTICLE_SYSTEM')
    ps = mod.particle_system
    ps.seed = seed
    s = ps.settings
    s.name = name + ' settings'
    s.type = 'HAIR'
    s.use_advanced_hair = True
    s.count = count
    s.emit_from = 'FACE'
    s.use_emit_random = True
    s.use_even_distribution = True
    # Blender stores hair length as normal_factor * 4, and in advanced mode the strand vector is
    # (normal * N + object_align); scale both so the strand is `length` long.
    k = length / (4.0 * max(1e-6, (normal ** 2 + Vector(comb).length_squared) ** 0.5))
    s.normal_factor = normal * k
    s.object_align_factor = Vector(comb) * k
    s.child_type = 'INTERPOLATED'
    s.child_percent = 2
    s.rendered_child_count = children
    s.child_radius = 0.06
    s.clump_factor = clump
    s.clump_shape = 0.2
    s.roughness_1 = rough
    s.roughness_1_size = 0.4
    s.roughness_endpoint = rough * 0.6
    if kink:
        s.kink = 'CURL'
        s.kink_amplitude = kink
        s.kink_frequency = 2.0
    s.material = mat_index + 1
    s.display_step = 3
    s.render_step = 3
    s.root_radius = 1.0
    s.tip_radius = tip
    s.radius_scale = radius
    s.use_close_tip = True
    if group:
        ps.vertex_group_density = group
        ps.vertex_group_length = group
    # The particle modifier must evaluate after deformation.
    return mod


def ensure_after_armature(obj):
    names = [m.name for m in obj.modifiers]
    for m in list(obj.modifiers):
        if m.type == 'ARMATURE':
            idx = obj.modifiers.find(m.name)
            if idx != 0:
                obj.modifiers.move(idx, 0)
