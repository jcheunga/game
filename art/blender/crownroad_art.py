"""Shared, dependency-free Blender construction and render helpers for Crownroad."""

import math
import bpy
from mathutils import Vector


def rgba(hex_color):
    c = [int(hex_color[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in c) + (1,)


def material(name, color, roughness=0.6, metallic=0.0, variation=None, grain=None, emission=0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = rgba(color)
    m.use_nodes = True
    nodes, links = m.node_tree.nodes, m.node_tree.links
    bsdf = nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = rgba(color)
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic
    if emission:
        bsdf.inputs['Emission Color'].default_value = rgba(color)
        bsdf.inputs['Emission Strength'].default_value = emission
    if variation:
        tex = nodes.new('ShaderNodeTexCoord')
        scale = nodes.new('ShaderNodeVectorMath')
        scale.operation = 'MULTIPLY'
        scale.inputs[1].default_value = grain or (4, 4, 4)
        links.new(tex.outputs['Generated'], scale.inputs[0])
        noise = nodes.new('ShaderNodeTexNoise')
        noise.inputs['Scale'].default_value = 3.0
        noise.inputs['Detail'].default_value = 3.2
        noise.inputs['Roughness'].default_value = 0.72
        links.new(scale.outputs[0], noise.inputs['Vector'])
        ramp = nodes.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].position = 0.18
        ramp.color_ramp.elements[0].color = rgba(color)
        ramp.color_ramp.elements[1].position = 0.83
        ramp.color_ramp.elements[1].color = rgba(variation)
        links.new(noise.outputs['Fac'], ramp.inputs[0])
        links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
        bump = nodes.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = 0.19 if grain else 0.11
        bump.inputs['Distance'].default_value = 0.07 if grain else 0.035
        links.new(noise.outputs['Fac'], bump.inputs['Height'])
        links.new(bump.outputs[0], bsdf.inputs['Normal'])
    return m


def collection(name):
    coll = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(coll)
    return coll


def finish(obj, name, mat, coll, bevel=0, smooth=False):
    obj.name = name
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    coll.objects.link(obj)
    if mat:
        obj.data.materials.append(mat)
    if bevel:
        mod = obj.modifiers.new('Soft crafted edges', 'BEVEL')
        mod.width = bevel
        mod.segments = 2
        mod = obj.modifiers.new('Weighted corner normals', 'WEIGHTED_NORMAL')
        mod.keep_sharp = True
    if smooth:
        for poly in obj.data.polygons:
            poly.use_smooth = True
    return obj


def box(name, location, size, mat, coll, bevel=0.025, rotation=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if rotation:
        obj.rotation_euler = rotation
    return finish(obj, name, mat, coll, bevel)


def cylinder(name, location, radius, depth, mat, coll, axis='Z', vertices=16, bevel=0.012):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location)
    obj = bpy.context.object
    if axis == 'Y':
        obj.rotation_euler.x = math.pi / 2
    elif axis == 'X':
        obj.rotation_euler.y = math.pi / 2
    return finish(obj, name, mat, coll, bevel, True)


def beam(name, start, end, width, depth, mat, coll, bevel=0.015):
    start, end = Vector(start), Vector(end)
    obj = box(name, (start + end) / 2, (width, depth, (end - start).length), mat, coll, bevel)
    obj.rotation_euler = (end - start).to_track_quat('Z', 'Y').to_euler()
    return obj


def mesh(name, verts, faces, mat, coll, bevel=0, smooth=False):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    coll.objects.link(obj)
    if mat:
        data.materials.append(mat)
    if bevel:
        mod = obj.modifiers.new('Edge thickness', 'BEVEL')
        mod.width, mod.segments = bevel, 2
    if smooth:
        for p in data.polygons:
            p.use_smooth = True
    return obj


def tube(name, coords, radius, mat, coll, cyclic=False):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.resolution_u = 2
    curve.bevel_depth = radius
    curve.bevel_resolution = 2
    poly = curve.splines.new('POLY')
    poly.points.add(len(coords) - 1)
    for p, co in zip(poly.points, coords):
        p.co = (*co, 1)
    poly.use_cyclic_u = cyclic
    obj = bpy.data.objects.new(name, curve)
    coll.objects.link(obj)
    curve.materials.append(mat)
    return obj


def ring(name, location, outer, inner, depth, mat, coll, axis='Y', segments=64):
    verts = []
    for z, r in ((-depth / 2, outer), (depth / 2, outer), (-depth / 2, inner), (depth / 2, inner)):
        for j in range(segments):
            a = j * 2 * math.pi / segments
            p = (r * math.cos(a), r * math.sin(a), z)
            if axis == 'Y':
                p = (p[0], p[2], p[1])
            verts.append(tuple(p[i] + location[i] for i in range(3)))
    faces = []
    for j in range(segments):
        n = (j + 1) % segments
        faces.extend([(j, n, segments + n, segments + j),
                      (2 * segments + j, 3 * segments + j, 3 * segments + n, 2 * segments + n),
                      (j, 2 * segments + j, 2 * segments + n, n),
                      (segments + j, segments + n, 3 * segments + n, 3 * segments + j)])
    return mesh(name, verts, faces, mat, coll, 0.008)


def area_light(name, location, target, power, color, size, coll):
    data = bpy.data.lights.new(name, 'AREA')
    data.energy, data.color, data.shape, data.size = power, color, 'DISK', size
    obj = bpy.data.objects.new(name, data)
    coll.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()
    return obj


def camera(name, location, target, scale, coll):
    data = bpy.data.cameras.new(name)
    data.type, data.ortho_scale, data.lens = 'ORTHO', scale, 50
    obj = bpy.data.objects.new(name, data)
    coll.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()
    return obj


def setup_render(scene, samples):
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 6
    scene.cycles.transparent_max_bounces = 4
    prefs = bpy.context.preferences.addons['cycles'].preferences
    try:
        prefs.compute_device_type = 'METAL'
        prefs.get_devices()
        available = [d for d in prefs.devices if d.type == 'METAL']
        if available:
            for d in prefs.devices:
                d.use = d.type == 'METAL'
            scene.cycles.device = 'GPU'
    except (TypeError, RuntimeError):
        scene.cycles.device = 'CPU'
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.render.image_settings.color_depth = '8'
    scene.render.image_settings.compression = 40
    scene.render.film_transparent = True
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.look = 'AgX - Medium High Contrast'
    if scene.world is None:
        scene.world = bpy.data.worlds.new('Studio world')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (0.18, 0.23, 0.28, 1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = 0.5
