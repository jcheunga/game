"""Scene, colour and render setup shared by every remaster build script."""
import math
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
REMASTER = ROOT / 'art/remaster'
REVIEW = ROOT / 'artifacts/remaster'


def srgb(hex_color, alpha=1.0):
    """Hex sRGB to linear RGBA."""
    hex_color = hex_color.lstrip('#')
    c = [int(hex_color[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in c) + (alpha,)


def mix(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def scale_rgb(c, k):
    return tuple(min(1.0, v * k) for v in c[:3]) + (c[3] if len(c) > 3 else 1.0,)


def reset():
    """Empty the current file without relying on operator context."""
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for coll in list(bpy.data.collections):
        bpy.data.collections.remove(coll)
    for blocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras,
                   bpy.data.lights, bpy.data.actions, bpy.data.armatures, bpy.data.node_groups,
                   bpy.data.images, bpy.data.textures):
        for block in list(blocks):
            blocks.remove(block)
    scene = bpy.context.scene
    scene.timeline_markers.clear()
    for key in list(scene.keys()):
        del scene[key]
    return scene


def collection(name, parent=None):
    coll = bpy.data.collections.new(name)
    (parent or bpy.context.scene.collection).children.link(coll)
    return coll


def link(obj, coll):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    coll.objects.link(obj)
    return obj


def setup_render(scene, samples=96, transparent=True, exposure=0.0, look='AgX - Medium High Contrast'):
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = samples
    scene.cycles.use_adaptive_sampling = True
    scene.cycles.adaptive_threshold = 0.02
    scene.cycles.use_denoising = True
    try:
        scene.cycles.denoiser = 'OPENIMAGEDENOISE'
    except TypeError:
        pass
    scene.cycles.max_bounces = 8
    scene.cycles.diffuse_bounces = 3
    scene.cycles.glossy_bounces = 3
    scene.cycles.transmission_bounces = 6
    scene.cycles.transparent_max_bounces = 8
    scene.cycles.volume_bounces = 1
    scene.cycles.sample_clamp_indirect = 6.0
    scene.cycles.blur_glossy = 0.6
    scene.cycles.caustics_reflective = False
    scene.cycles.caustics_refractive = False
    prefs = bpy.context.preferences.addons['cycles'].preferences
    import os
    if os.environ.get('RK_DEVICE', '').upper() == 'CPU':
        scene.cycles.device = 'CPU'
        prefs = None
    try:
        if prefs is None:
            raise RuntimeError('cpu requested')
        prefs.compute_device_type = 'METAL'
        prefs.get_devices()
        metal = [d for d in prefs.devices if d.type == 'METAL']
        for d in prefs.devices:
            d.use = d.type == 'METAL'
        scene.cycles.device = 'GPU' if metal else 'CPU'
    except (TypeError, RuntimeError):
        scene.cycles.device = 'CPU'
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.render.image_settings.color_depth = '8'
    scene.render.image_settings.compression = 30
    scene.render.film_transparent = transparent
    scene.render.filter_size = 1.2
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = 'AgX'
    try:
        scene.view_settings.look = look
    except TypeError:
        pass
    scene.view_settings.exposure = exposure
    scene.view_settings.gamma = 1.0
    if scene.world is None:
        scene.world = bpy.data.worlds.new('Studio world')
    return scene


def studio_world(scene, top='3a4654', bottom='1d1a17', strength=0.55, reflect=2.2, band='f2e6d2'):
    """Soft gradient environment: cool sky above, warm bounce below. Reflections see a brighter copy with a
    soft horizon band, the way a softbox lights a studio model, so metal and gloss read instead of going black."""
    world = scene.world
    world.use_nodes = True
    nt = world.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputWorld')
    bg = nt.nodes.new('ShaderNodeBackground')
    coord = nt.nodes.new('ShaderNodeTexCoord')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    nt.links.new(coord.outputs['Generated'], sep.inputs[0])
    nt.links.new(sep.outputs['Z'], ramp.inputs['Fac'])
    ramp.color_ramp.elements[0].position = 0.42
    ramp.color_ramp.elements[0].color = srgb(bottom)
    ramp.color_ramp.elements[1].position = 0.62
    ramp.color_ramp.elements[1].color = srgb(top)
    nt.links.new(ramp.outputs['Color'], bg.inputs['Color'])
    bg.inputs['Strength'].default_value = strength
    if reflect:
        glossy = nt.nodes.new('ShaderNodeValToRGB')
        nt.links.new(sep.outputs['Z'], glossy.inputs['Fac'])
        els = glossy.color_ramp.elements
        els[0].position, els[0].color = 0.40, srgb(bottom)
        els[1].position, els[1].color = 0.70, srgb(top)
        mid = els.new(0.53)
        mid.color = srgb(band)
        bg2 = nt.nodes.new('ShaderNodeBackground')
        nt.links.new(glossy.outputs['Color'], bg2.inputs['Color'])
        bg2.inputs['Strength'].default_value = strength * reflect
        lp = nt.nodes.new('ShaderNodeLightPath')
        mix = nt.nodes.new('ShaderNodeMixShader')
        nt.links.new(lp.outputs['Is Glossy Ray'], mix.inputs[0])
        nt.links.new(bg.outputs[0], mix.inputs[1])
        nt.links.new(bg2.outputs[0], mix.inputs[2])
        nt.links.new(mix.outputs[0], out.inputs[0])
    else:
        nt.links.new(bg.outputs[0], out.inputs[0])
    return world


def area_light(name, location, target, power, color, size, coll, shape='DISK', spread=None):
    data = bpy.data.lights.new(name, 'AREA')
    data.energy = power
    data.color = color[:3] if len(color) > 3 else color
    data.shape = shape
    data.size = size
    if spread is not None:
        data.spread = spread
    obj = bpy.data.objects.new(name, data)
    coll.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector(target) - Vector(location)).to_track_quat('-Z', 'Y').to_euler()
    return obj


def point_light(name, location, power, color, radius, coll):
    data = bpy.data.lights.new(name, 'POINT')
    data.energy = power
    data.color = color[:3]
    data.shadow_soft_size = radius
    obj = bpy.data.objects.new(name, data)
    coll.objects.link(obj)
    obj.location = location
    return obj


def sun_light(name, direction_target, strength, color, angle_deg, coll):
    data = bpy.data.lights.new(name, 'SUN')
    data.energy = strength
    data.color = color[:3]
    data.angle = math.radians(angle_deg)
    obj = bpy.data.objects.new(name, data)
    coll.objects.link(obj)
    obj.rotation_euler = (-Vector(direction_target)).to_track_quat('-Z', 'Y').to_euler()
    return obj


def ortho_camera(name, location, target, scale, coll):
    data = bpy.data.cameras.new(name)
    data.type = 'ORTHO'
    data.ortho_scale = scale
    data.clip_start = 0.1
    data.clip_end = 200
    obj = bpy.data.objects.new(name, data)
    coll.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector(target) - Vector(location)).to_track_quat('-Z', 'Y').to_euler()
    return obj


def persp_camera(name, location, target, lens, coll, dof=None):
    data = bpy.data.cameras.new(name)
    data.lens = lens
    data.clip_start = 0.05
    data.clip_end = 500
    if dof:
        data.dof.use_dof = True
        data.dof.focus_distance = (Vector(target) - Vector(location)).length
        data.dof.aperture_fstop = dof
    obj = bpy.data.objects.new(name, data)
    coll.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector(target) - Vector(location)).to_track_quat('-Z', 'Y').to_euler()
    return obj


def render_to(scene, path, camera=None):
    if camera is not None:
        scene.camera = camera
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def save_blend(path):
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(path), compress=True)


def args_after_dashes(argv):
    return argv[argv.index('--') + 1:] if '--' in argv else []
