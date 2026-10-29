"""Build the animated spell flipbooks: sprite sheets the battle plays frame by frame (scripts/combat/SpellVfx.cs).

blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_spell_flipbooks.py -- \
    [--ids fire_burst,smoke_puff] [--no-pack] [--rerender]

Each flipbook is a Blender fluid simulation or an animated model, rendered side-on from the battle camera's angle
(25 degrees down, which is what BattleGroundPlane.DepthScale .42 implies) on transparency, then packed as a
premultiplied-alpha sheet: smoke and solid pieces cover what is behind them (alpha = coverage) and fire or glow adds
light (colour above alpha). Grey ones are tinted per spell in the game.

  frames   artifacts/remaster/flipbooks/<id>/####.exr (lit) and ####_e.exr (sun and sky off: only emitted light)
  review   artifacts/remaster/flipbooks/<id>_preview.png (every other frame over the battle road)
  game     assets/fx/spells/fb_<id>.png + assets/fx/spells/flipbooks.json (grid, frames, pivot, size in metres)
"""
import argparse
import json
import math
import random
import shutil
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import bpy  # noqa: E402
import numpy as np  # noqa: E402
import OpenImageIO as oiio  # noqa: E402
from mathutils import Vector  # noqa: E402

from rk import core  # noqa: E402
from rk.nodekit import material  # noqa: E402

FRAMES_DIR = core.REVIEW / 'flipbooks'
ASSETS = core.ROOT / 'assets/fx/spells'
META = ASSETS / 'flipbooks.json'
ROAD = core.ROOT / 'art/remaster/ref/flipbook_road.png'
PITCH = math.radians(25)
RENDER = 384
QUICK = False  # --quick: coarse sims and renders while tuning


# ------------------------------------------------------------------ scene pieces
def scene_base(frames, samples=48, neutral=False):
    scene = core.reset()
    core.setup_render(scene, samples=samples, transparent=True, look='None')
    scene.view_settings.view_transform = 'Standard'
    scene.render.resolution_x = scene.render.resolution_y = RENDER
    scene.render.image_settings.media_type = 'IMAGE'
    scene.render.image_settings.file_format = 'OPEN_EXR'
    scene.render.image_settings.color_depth = '16'
    scene.cycles.volume_bounces = 1
    scene.cycles.use_denoising = True
    scene.frame_start, scene.frame_end = 1, frames
    world = scene.world
    world.use_nodes = True
    nt = world.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputWorld')
    bg = nt.nodes.new('ShaderNodeBackground')
    bg.inputs['Color'].default_value = (0.66, 0.66, 0.66, 1) if neutral else (0.62, 0.66, 0.74, 1)
    bg.inputs['Strength'].default_value = 0.65
    nt.links.new(bg.outputs[0], out.inputs[0])
    # The battle's light: warm late sun from the upper left, a little in front.
    sun = bpy.data.lights.new('Sun', 'SUN')
    sun.energy = 3.2
    sun.color = (1.0, 1.0, 1.0) if neutral else (1.0, 0.93, 0.82)
    sun.angle = math.radians(10)
    so = bpy.data.objects.new('Sun', sun)
    scene.collection.objects.link(so)
    so.rotation_euler = (math.radians(48), math.radians(-32), math.radians(-24))
    return scene


def camera(scene, scale, aim_z):
    data = bpy.data.cameras.new('Battle camera')
    data.type = 'ORTHO'
    data.ortho_scale = scale
    data.clip_start, data.clip_end = 0.1, 80
    cam = bpy.data.objects.new('Battle camera', data)
    scene.collection.objects.link(cam)
    cam.location = (0, -30 * math.cos(PITCH), aim_z + 30 * math.sin(PITCH))
    cam.rotation_euler = (math.radians(90) - PITCH, 0, 0)
    scene.camera = cam
    return cam


def holdout_ground(size=30):
    """The road: hides whatever sinks below it and catches nothing."""
    bpy.ops.mesh.primitive_plane_add(size=size, location=(0, 0, 0))
    ground = bpy.context.object
    ground.name = 'Ground'
    m = bpy.data.materials.new('Holdout')
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    o = nt.nodes.new('ShaderNodeOutputMaterial')
    h = nt.nodes.new('ShaderNodeHoldout')
    nt.links.new(h.outputs[0], o.inputs[0])
    ground.data.materials.append(m)
    ground.visible_shadow = False
    return ground


def gas_domain(size, height, res, frames, cache, **settings):
    bpy.ops.mesh.primitive_cube_add(size=1)
    dom = bpy.context.object
    dom.name = 'Domain'
    dom.scale = (size, size, height)
    dom.location = (0, 0, height / 2 - 0.02)
    bpy.ops.object.transform_apply(scale=True)
    mod = dom.modifiers.new('Fluid', 'FLUID')
    mod.fluid_type = 'DOMAIN'
    d = mod.domain_settings
    d.domain_type = 'GAS'
    d.resolution_max = round(res * 0.7) if QUICK else res
    d.cache_directory = str(cache)
    d.cache_type = 'ALL'
    d.cache_frame_start, d.cache_frame_end = 1, frames
    d.use_adaptive_domain = False
    d.use_collision_border_bottom = True
    for key, value in settings.items():
        setattr(d, key, value)
    if QUICK:
        d.use_noise = False
    return dom


def flow(obj, kind='SMOKE', frames_on=(1, 3), visible=False, **settings):
    """Makes obj an inflow that runs only on frames_on (inclusive)."""
    mod = obj.modifiers.new('Fluid', 'FLUID')
    mod.fluid_type = 'FLOW'
    f = mod.flow_settings
    f.flow_type = kind
    f.flow_behavior = 'INFLOW'
    f.flow_source = 'MESH'
    for key, value in settings.items():
        setattr(f, key, value)
    if frames_on:
        first, last = frames_on
        for frame, on in ((max(0, first - 1), first <= 1), (first, True), (last, True), (last + 1, False)):
            f.use_inflow = on
            f.keyframe_insert('use_inflow', frame=frame)
    obj.hide_render = not visible
    return f


def effector(obj):
    mod = obj.modifiers.new('Fluid', 'FLUID')
    mod.fluid_type = 'EFFECTOR'
    return mod.effector_settings


def bake(dom, scene):
    t = time.time()
    with bpy.context.temp_override(scene=scene, object=dom, active_object=dom, selected_objects=[dom]):
        bpy.ops.fluid.bake_all()
    print(f'  baked in {time.time() - t:.0f}s', flush=True)


def sphere(radius, location, subdiv=3):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=radius, location=location)
    return bpy.context.object


def lumpy(obj, strength=0.25, scale=0.6, seed=0):
    tex = bpy.data.textures.new(f'Lumps {obj.name}', 'CLOUDS')
    tex.noise_scale = scale
    tex.noise_depth = 2
    disp = obj.modifiers.new('Lumps', 'DISPLACE')
    disp.texture = tex
    disp.strength = strength
    disp.mid_level = 0.5
    disp.texture_coords = 'GLOBAL'
    obj.location += Vector((seed * 7.1, 0, 0)) * 0
    return obj


def gas_material(dom, density=8.0, color=(0.1, 0.09, 0.085, 1), anisotropy=0.25, fire=None, fire_strength=6.0,
                 fire_body=0.0, thin=1.0, glow=None, glow_strength=0.0, absorption=(0, 0, 0, 1)):
    """Principled volume over the fluid grids.
    fire: colour stops on the 'flame' grid; fire_body: how much the flame itself hides what is behind it.
    thin: power on the smoke density (<1 lifts thin wisps so they read). glow: emission from density instead."""
    m, n = material('Gas')
    dens = n.attr('density', out='Fac')
    if thin != 1.0:
        dens = n.math('POWER', n.math('MAXIMUM', dens, 0.0), thin)
    v = n.node('ShaderNodeVolumePrincipled')
    n.feed(v.inputs['Color'], color)
    n.feed(v.inputs['Absorption Color'], absorption)
    n.feed(v.inputs['Anisotropy'], anisotropy)
    amount = n.mul(dens, density)
    if fire:
        flame = n.attr('flame', out='Fac')
        n.feed(v.inputs['Emission Color'], n.ramp(flame, fire))
        n.feed(v.inputs['Emission Strength'], n.mul(n.math('POWER', flame, 1.3), fire_strength))
        if fire_body:
            amount = n.add(amount, n.mul(flame, fire_body))
    elif glow is not None:
        n.feed(v.inputs['Emission Color'], glow)
        n.feed(v.inputs['Emission Strength'], n.mul(dens, glow_strength))
    n.feed(v.inputs['Density'], amount)
    n.volume_out(v.outputs[0])
    dom.data.materials.append(m)
    return m


# ------------------------------------------------------------------ the flipbooks
FIRE_STOPS = [(0.0, (0, 0, 0, 1)), (0.12, (0.35, 0.03, 0.0, 1)), (0.3, (0.9, 0.22, 0.02, 1)),
              (0.55, (1.0, 0.52, 0.08, 1)), (0.8, (1.0, 0.8, 0.35, 1)), (1.0, (1.0, 0.95, 0.75, 1))]


FLAME_STOPS = [(0.0, (0, 0, 0, 1)), (0.15, (0.4, 0.04, 0.0, 1)), (0.4, (0.95, 0.28, 0.03, 1)),
               (0.75, (1.0, 0.55, 0.1, 1)), (1.0, (1.0, 0.78, 0.35, 1))]


def build_fire_burst(scene, cache):
    """A fireball bursting on the road: a roiling ball of flame that climbs and rolls into dark smoke."""
    frames = 40
    dom = gas_domain(6.0, 5.0, 120, frames, cache, vorticity=0.35, alpha=0.0, beta=1.2, burning_rate=0.55,
                     flame_smoke=3.0, flame_vorticity=0.8, flame_ignition=1.25, flame_max_temp=3.0,
                     use_dissolve_smoke=True, dissolve_speed=55, use_dissolve_smoke_log=False,
                     timesteps_max=10, time_scale=1.6, use_noise=True, noise_scale=2, noise_strength=1.6)
    dom.modifiers['Fluid'].domain_settings.flame_smoke_color = (0.12, 0.1, 0.09)
    rng = random.Random(4)
    emitters = [sphere(0.42, (0, 0, 0.42))]
    for i in range(7):
        a = i / 7 * math.tau + rng.uniform(-0.3, 0.3)
        up = rng.uniform(0.15, 0.9)
        emitters.append(sphere(rng.uniform(0.15, 0.25), (math.cos(a) * 0.5, math.sin(a) * 0.5, 0.3 + up * 0.5)))
    for i, e in enumerate(emitters):
        lumpy(e, 0.18, 0.35)
        flow(e, 'BOTH', (1, 4), fuel_amount=3.0, temperature=3.0, density=0.2, surface_distance=0.25,
             use_initial_velocity=True, velocity_normal=26.0 if i == 0 else rng.uniform(18, 34), velocity_factor=0.0)
    ground = holdout_ground()
    effector(ground)
    bake(dom, scene)
    gas_material(dom, density=24.0, color=(0.3, 0.27, 0.25, 1), anisotropy=0.3, fire=FIRE_STOPS, fire_strength=9.0,
                 thin=0.65)
    camera(scene, 5.0, 2.0)
    return dict(frames=frames, radius=1.2, kind='color', passes='both', fire_alpha=0.85)


def build_smoke_puff(scene, cache):
    """A white puff of smoke that billows out and drifts up; tinted in the game (dust, soot, mist, shadow)."""
    frames = 36
    dom = gas_domain(5.0, 4.5, 104, frames, cache, vorticity=0.5, alpha=0.25, beta=0.3, use_dissolve_smoke=False,
                     timesteps_max=8, time_scale=1.3,
                     use_noise=True, noise_scale=2, noise_strength=1.4)
    rng = random.Random(9)
    emitters = [sphere(0.4, (0, 0, 0.45))]
    for i in range(5):
        a = i / 5 * math.tau + rng.uniform(-0.4, 0.4)
        emitters.append(sphere(rng.uniform(0.14, 0.22), (math.cos(a) * 0.4, math.sin(a) * 0.4, rng.uniform(0.25, 0.8))))
    for i, e in enumerate(emitters):
        lumpy(e, 0.15, 0.3)
        flow(e, 'SMOKE', (1, 3), density=1.0, temperature=1.0, surface_distance=0.25, use_initial_velocity=True,
             velocity_normal=12.0 if i == 0 else rng.uniform(8, 16), velocity_factor=0.0)
    ground = holdout_ground()
    effector(ground)
    bake(dom, scene)
    gas_material(dom, density=70.0, color=(0.9, 0.9, 0.9, 1), anisotropy=0.2, thin=0.55)
    camera(scene, 4.6, 1.7)
    return dict(frames=frames, radius=1.0, kind='tint', passes='lit')


def build_dust_ring(scene, cache):
    """A ring of dust thrown out along the road from a heavy landing; tinted in the game."""
    frames = 32
    dom = gas_domain(7.0, 2.6, 128, frames, cache, vorticity=0.55, alpha=-0.08, beta=0.15, use_dissolve_smoke=False,
                     timesteps_max=8, time_scale=1.3,
                     use_noise=True, noise_scale=2, noise_strength=1.5)
    rng = random.Random(3)
    count = 16
    for i in range(count):
        a = i / count * math.tau + rng.uniform(-0.12, 0.12)
        e = sphere(rng.uniform(0.11, 0.17), (math.cos(a) * 0.38, math.sin(a) * 0.38, 0.14), subdiv=2)
        speed = rng.uniform(15, 21)
        flow(e, 'SMOKE', (1, 3), density=1.0, temperature=0.4, surface_distance=0.18, use_initial_velocity=True,
             velocity_normal=2.0, velocity_factor=0.0,
             velocity_coord=(math.cos(a) * speed, math.sin(a) * speed, rng.uniform(1.0, 3.0)))
    ground = holdout_ground()
    effector(ground)
    bake(dom, scene)
    gas_material(dom, density=70.0, color=(0.9, 0.9, 0.9, 1), anisotropy=0.15, thin=0.55)
    camera(scene, 6.6, 0.8)
    return dict(frames=frames, radius=2.4, kind='tint', passes='lit')


def build_flame_loop(scene, cache):
    """A patch of burning ground: tongues of flame licking up, looped for as long as the fire lasts."""
    frames = 68
    dom = gas_domain(2.6, 4.0, 104, frames, cache, vorticity=0.3, alpha=0.0, beta=1.0, burning_rate=0.72,
                     flame_smoke=0.5, flame_vorticity=0.7, flame_ignition=1.25, flame_max_temp=3.0,
                     use_dissolve_smoke=True, dissolve_speed=30, use_dissolve_smoke_log=True,
                     timesteps_max=8, time_scale=1.0, use_noise=True, noise_scale=2, noise_strength=1.8)
    dom.modifiers['Fluid'].domain_settings.flame_smoke_color = (0.15, 0.12, 0.1)
    base = sphere(0.5, (0, 0, 0.02))
    base.scale = (1.0, 1.0, 0.28)
    lumpy(base, 0.12, 0.25)
    flow(base, 'FIRE', None, fuel_amount=1.3, temperature=2.2, surface_distance=0.12, use_initial_velocity=True,
         velocity_normal=1.5, velocity_factor=0.0)
    ground = holdout_ground()
    effector(ground)
    bake(dom, scene)
    gas_material(dom, density=10.0, color=(0.2, 0.18, 0.17, 1), anisotropy=0.3, fire=FLAME_STOPS, fire_strength=12.0,
                 thin=0.7)
    camera(scene, 2.8, 1.25)
    return dict(frames=frames, first=29, radius=0.55, kind='color', passes='both', fire_alpha=0.8, loop=10)


def build_magic_burst(scene, cache):
    """A swirling burst of glowing vapour for spells of light, void and nature; white, tinted in the game."""
    frames = 30
    dom = gas_domain(5.0, 5.0, 104, frames, cache, vorticity=1.0, alpha=0.1, beta=0.2, use_dissolve_smoke=False,
                     timesteps_max=8, time_scale=1.4,
                     use_noise=True, noise_scale=2, noise_strength=2.0)
    rng = random.Random(12)
    emitters = [sphere(0.3, (0, 0, 0.9))]
    for i in range(6):
        a = i / 6 * math.tau
        emitters.append(sphere(0.12, (math.cos(a) * 0.3, math.sin(a) * 0.3, 0.9 + rng.uniform(-0.25, 0.25)), subdiv=2))
    for i, e in enumerate(emitters):
        lumpy(e, 0.12, 0.25)
        flow(e, 'SMOKE', (1, 2), density=1.0, temperature=0.6, surface_distance=0.2, use_initial_velocity=True,
             velocity_normal=16.0 if i == 0 else rng.uniform(12, 22), velocity_factor=0.0)
    holdout_ground()
    bake(dom, scene)
    gas_material(dom, density=5.0, color=(1, 1, 1, 1), anisotropy=0.0, thin=1.1, glow=(1, 1, 1, 1), glow_strength=1.6)
    camera(scene, 4.2, 1.0)
    return dict(frames=frames, radius=1.0, kind='tint', passes='both', fire_alpha=0.5)


def spike_mesh(name, radius, height, sides, rng, jag=0.0):
    """A pointed crystal or rock spike standing on its base: a ring, a shoulder ring, an apex."""
    import bmesh
    bm = bmesh.new()
    turn = rng.uniform(0, math.tau)
    rings = [(0.0, radius), (height * rng.uniform(0.55, 0.75), radius * rng.uniform(0.7, 0.92))]
    loops = []
    for z, r in rings:
        loop = []
        for k in range(sides):
            a = turn + k / sides * math.tau
            rr = r * (1 + rng.uniform(-jag, jag))
            loop.append(bm.verts.new((math.cos(a) * rr, math.sin(a) * rr, z)))
        loops.append(loop)
    tip = bm.verts.new((rng.uniform(-0.1, 0.1) * radius, rng.uniform(-0.1, 0.1) * radius, height))
    bm.faces.new(list(reversed(loops[0])))
    for k in range(sides):
        a0, a1 = loops[0][k], loops[0][(k + 1) % sides]
        b0, b1 = loops[1][k], loops[1][(k + 1) % sides]
        bm.faces.new((a0, a1, b1, b0))
        bm.faces.new((b0, b1, tip))
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def grow(obj, start, rise, overshoot=1.12, settle=3):
    """Shoots obj up out of the road from frame start: flat, past full height, back."""
    for frame, (sxy, sz) in ((start - 1, (0.55, 0.0)), (start, (0.6, 0.04)), (start + rise, (1.0, overshoot)),
                             (start + rise + settle, (1.0, 1.0))):
        obj.scale = (sxy, sxy, sz)
        obj.keyframe_insert('scale', frame=max(0, frame))


def ice_material():
    m, n = material('Ice')
    frost = n.noise(n.tc('Object'), scale=9.0, detail=6.0, rough=0.6)
    n.principled(**{'Base Color': n.ramp(frost, [(0.3, (0.55, 0.78, 0.95, 1)), (0.8, (0.9, 0.97, 1.0, 1))]),
                    'Roughness': n.maprange(frost, 0.3, 0.8, 0.05, 0.35),
                    'Transmission Weight': 0.55, 'IOR': 1.31, 'Coat Weight': 0.6, 'Coat Roughness': 0.05,
                    'Emission Color': (0.45, 0.75, 1.0, 1), 'Emission Strength': 0.25})
    return m


def rock_material():
    m, n = material('Rock')
    p = n.tc('Object')
    grain = n.noise(p, scale=7.0, detail=8.0, rough=0.65)
    cracks = n.voronoi(p, scale=5.0, feature='DISTANCE_TO_EDGE')
    base = n.ramp(grain, [(0.3, (0.07, 0.05, 0.04, 1)), (0.5, (0.17, 0.12, 0.085, 1)), (0.72, (0.3, 0.23, 0.16, 1))])
    dark = n.mixc(n.maprange(cracks, 0.0, 0.035, 0.55, 0.0), base, (0.05, 0.035, 0.03, 1), 'MULTIPLY')
    n.principled(**{'Base Color': dark, 'Roughness': 0.9,
                    'Normal': n.bump(n.add(grain, n.mul(cracks, 0.8)), strength=0.45, distance=0.05)})
    return m


def build_ice_burst(scene, cache):
    """Ice crystals bursting up out of the road in a ring that spreads from the centre."""
    frames = 22
    rng = random.Random(5)
    mat = ice_material()
    scene.cycles.film_transparent_glass = True
    pieces = [(0, 0, 1.0)]
    for ring, (dist, count) in enumerate(((0.45, 6), (0.85, 9), (1.15, 8))):
        for k in range(count):
            a = k / count * math.tau + rng.uniform(-0.25, 0.25) + ring
            pieces.append((math.cos(a) * dist * rng.uniform(0.85, 1.1), math.sin(a) * dist * rng.uniform(0.85, 1.1),
                           1.0 - dist * 0.55))
    for i, (x, y, scale) in enumerate(pieces):
        h = rng.uniform(0.9, 1.5) * scale * 1.4
        spike = spike_mesh(f'Ice {i}', rng.uniform(0.09, 0.15) * (0.6 + scale * 0.5), h, 6, rng)
        spike.data.materials.append(mat)
        spike.location = (x, y, -0.04)
        out = math.atan2(y, x)
        tilt = min(0.75, math.hypot(x, y) * 0.55) + rng.uniform(-0.08, 0.12)
        spike.rotation_euler = (-math.sin(out) * tilt, math.cos(out) * tilt, rng.uniform(0, math.tau))
        grow(spike, 1 + round(math.hypot(x, y) * 4.5 + rng.uniform(0, 1.5)), 4)
    holdout_ground()
    camera(scene, 3.6, 0.95)
    return dict(frames=frames, radius=1.2, kind='color', passes='lit', hold=True)


def build_rock_burst(scene, cache):
    """Jagged stone spikes thrusting up through the road."""
    frames = 18
    rng = random.Random(8)
    mat = rock_material()
    pieces = [(0.0, 0.0, 1.0), (-0.42, 0.12, 0.72), (0.4, -0.1, 0.78), (0.15, 0.38, 0.6), (-0.2, -0.35, 0.62),
              (0.72, 0.25, 0.45), (-0.75, -0.15, 0.48), (0.3, -0.55, 0.42), (-0.55, -0.5, 0.38), (0.62, -0.42, 0.36)]
    for i, (x, y, scale) in enumerate(pieces):
        spike = spike_mesh(f'Rock {i}', rng.uniform(0.2, 0.26) * (0.55 + scale * 0.6), rng.uniform(0.9, 1.25) * scale * 1.5,
                           rng.choice((5, 6, 7)), rng, jag=0.25)
        tex = bpy.data.textures.new(f'Rock lumps {i}', 'CLOUDS')
        tex.noise_scale = 0.18
        sub = spike.modifiers.new('Sub', 'SUBSURF')
        sub.levels = sub.render_levels = 2
        sub.subdivision_type = 'SIMPLE'
        disp = spike.modifiers.new('Lumps', 'DISPLACE')
        disp.texture = tex
        disp.strength = 0.07
        spike.data.materials.append(mat)
        spike.location = (x, y, -0.06)
        out = math.atan2(y, x)
        tilt = min(0.5, math.hypot(x, y) * 0.6) + rng.uniform(-0.05, 0.1)
        spike.rotation_euler = (-math.sin(out) * tilt, math.cos(out) * tilt, rng.uniform(0, math.tau))
        grow(spike, 1 + round(math.hypot(x, y) * 3 + rng.uniform(0, 1)), 3, overshoot=1.06, settle=2)
    holdout_ground()
    camera(scene, 3.4, 0.9)
    return dict(frames=frames, radius=0.9, kind='color', passes='lit', hold=True)


TINTED = {'smoke_puff', 'dust_ring', 'magic_burst'}
# Cell size (longest side, pixels) by how big each shows in battle: soft smoke and small flames need less.
CELLS = {'smoke_puff': 192, 'magic_burst': 192, 'flame_loop': 128}
# How much of the emitted light to keep: the vapour glows enough to saturate, which hides its swirls.
EMIT_GAIN = {'magic_burst': 0.6}
BOOKS = {
    'fire_burst': build_fire_burst,
    'smoke_puff': build_smoke_puff,
    'dust_ring': build_dust_ring,
    'flame_loop': build_flame_loop,
    'magic_burst': build_magic_burst,
    'ice_burst': build_ice_burst,
    'rock_burst': build_rock_burst,
}


# ------------------------------------------------------------------ rendering and packing
def render(name, build, rerender):
    out = FRAMES_DIR / name
    meta_path = out / 'meta.json'
    if out.exists() and meta_path.exists() and not rerender:
        print(f'{name}: frames cached', flush=True)
        return json.loads(meta_path.read_text())
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)
    cache = FRAMES_DIR / f'_cache_{name}'
    if cache.exists():
        shutil.rmtree(cache)
    scene = scene_base(1, samples=16 if QUICK else 48, neutral=name in TINTED)
    info = build(scene, cache)
    scene.frame_start, scene.frame_end = 1, info['frames']
    cam = scene.camera
    t = time.time()
    passes = info.get('passes', 'lit')
    lights = [o.data for o in scene.objects if o.type == 'LIGHT']
    sky = scene.world.node_tree.nodes['Background'].inputs['Strength']
    first = info.get('first', 1)
    for index, frame in enumerate(range(first, info['frames'] + 1), 1):
        scene.frame_set(frame)
        if passes in ('lit', 'both'):
            scene.render.filepath = str(out / f'{index:04d}.exr')
            bpy.ops.render.render(write_still=True)
        if passes in ('emit', 'both'):
            # Only the light the effect gives off, still shaded by its own smoke.
            energy, strength = [l.energy for l in lights], sky.default_value
            for l in lights:
                l.energy = 0
            sky.default_value = 0
            scene.render.filepath = str(out / f'{index:04d}_e.exr')
            bpy.ops.render.render(write_still=True)
            for l, e in zip(lights, energy):
                l.energy = e
            sky.default_value = strength
    info['frames'] = info['frames'] - first + 1
    print(f'  rendered {info["frames"]} frames in {time.time() - t:.0f}s', flush=True)
    # Where the road origin lands in the frame, and how many metres the frame spans.
    origin = cam.matrix_world.inverted() @ Vector((0, 0, 0))
    scale = cam.data.ortho_scale
    info['pivot'] = [0.5 + origin.x / scale, 0.5 - origin.y / scale]
    info['span'] = scale
    meta_path.write_text(json.dumps(info))
    shutil.rmtree(cache, ignore_errors=True)
    return info


def read_exr(path):
    return np.asarray(oiio.ImageBuf(str(path)).get_pixels(oiio.FLOAT), dtype=np.float32)


def read_frame(lit_path, emit_path):
    """Premultiplied linear (scattered colour, emitted light, alpha) from the two renders of a frame."""
    lit = read_exr(lit_path) if lit_path.exists() else None
    glow = read_exr(emit_path) if emit_path.exists() else None
    if lit is None:
        return np.zeros_like(glow[..., :3]), glow[..., :3], glow[..., 3]
    emit = glow[..., :3] if glow is not None else np.zeros_like(lit[..., :3])
    return np.clip(lit[..., :3] - emit, 0, None), emit, lit[..., 3]


def to_srgb(x):
    x = np.clip(x, 0.0, None)
    return np.where(x <= 0.0031308, x * 12.92, 1.055 * np.power(x, 1 / 2.4) - 0.055)


def premultiplied(scatter, emit, alpha, emit_gain=1.0, fire_alpha=0.0, lift=1.0):
    """Premultiplied sRGB for blending in the game's sRGB canvas: covered colour plus added light.
    fire_alpha lets bright light cover the road behind it too, so flames read as solid rather than as a glow."""
    straight = scatter / np.maximum(alpha, 1e-4)[..., None] * lift
    covered = to_srgb(np.clip(straight, 0, 1)) * alpha[..., None]
    light = to_srgb(emit * emit_gain)
    if fire_alpha:
        hot = np.clip(light.max(axis=2) * 1.4, 0, 1) * fire_alpha
        alpha = alpha + hot * (1 - alpha)
    rgb = np.clip(covered + light, 0, 1)
    return np.concatenate([rgb, np.clip(alpha, 0, 1)[..., None]], axis=2)


def resize(arr, w, h):
    spec = oiio.ImageSpec(arr.shape[1], arr.shape[0], arr.shape[2], oiio.FLOAT)
    src = oiio.ImageBuf(spec)
    src.set_pixels(oiio.ROI(), np.ascontiguousarray(arr, dtype=np.float32))
    dst = oiio.ImageBufAlgo.resize(src, roi=oiio.ROI(0, w, 0, h, 0, 1, 0, arr.shape[2]))
    return np.asarray(dst.get_pixels(oiio.FLOAT), dtype=np.float32)


def save_png(arr, path, unassociated=True):
    path.parent.mkdir(parents=True, exist_ok=True)
    data = np.clip(np.round(arr * 255.0), 0, 255).astype(np.uint8)
    spec = oiio.ImageSpec(arr.shape[1], arr.shape[0], arr.shape[2], oiio.UINT8)
    if arr.shape[2] == 4:
        # Stored as is: the colour is already premultiplied and must not be touched on the way out.
        spec.attribute('oiio:UnassociatedAlpha', 1)
    out = oiio.ImageOutput.create(str(path))
    out.open(str(path), spec)
    out.write_image(data)
    out.close()


def road(w, h):
    buf = oiio.ImageBuf(str(ROAD))
    img = np.asarray(buf.get_pixels(oiio.FLOAT), dtype=np.float32)[..., :3]
    return resize(img, w, h)


def pack(name, info, cell, emit_gain=1.0, fire_alpha=0.0):
    folder = FRAMES_DIR / name
    frames = [read_frame(folder / f'{i:04d}.exr', folder / f'{i:04d}_e.exr') for i in range(1, info['frames'] + 1)]
    lift = 1.0
    if info['kind'] == 'tint':
        # Tinted in the game by multiplying, so the lit side of the cloud should be near white.
        lit = np.concatenate([(sc / np.maximum(a, 1e-4)[..., None]).max(axis=2)[a > 0.3] for sc, _, a in frames])
        if lit.size:
            lift = 0.92 / max(1e-3, float(np.percentile(lit, 97)))
    layers = [premultiplied(*f, emit_gain=emit_gain, fire_alpha=fire_alpha, lift=lift) for f in frames]
    if info.get('loop'):
        # Seamless: the last frames dissolve into the first ones, then are dropped.
        fade = info['loop']
        tail = layers[-fade:]
        layers = layers[:-fade]
        for i in range(fade):
            k = (i + 1) / (fade + 1)
            layers[i] = tail[i] * (1 - k) + layers[i] * k
    # The union of everything any frame shows, so the cells share one frame of reference.
    seen = np.zeros(layers[0].shape[:2], bool)
    for f in layers:
        seen |= (f[..., 3] > 0.006) | (f[..., :3].max(axis=2) > 0.01)
    ys, xs = np.nonzero(seen)
    pad = 4
    x0, x1 = max(0, xs.min() - pad), min(RENDER, xs.max() + pad + 1)
    y0, y1 = max(0, ys.min() - pad), min(RENDER, ys.max() + pad + 1)
    cw, ch = x1 - x0, y1 - y0
    k = cell / max(cw, ch)
    w, h = max(4, round(cw * k)), max(4, round(ch * k))
    cols = math.ceil(math.sqrt(len(layers) * h / w))
    rows = math.ceil(len(layers) / cols)
    sheet = np.zeros((rows * h, cols * w, 4), np.float32)
    for i, f in enumerate(layers):
        r, c = divmod(i, cols)
        sheet[r * h:(r + 1) * h, c * w:(c + 1) * w] = np.clip(resize(f[y0:y1, x0:x1], w, h), 0, 1)
    save_png(sheet, ASSETS / f'fb_{name}.png')
    pivot_px = (info['pivot'][0] * RENDER, info['pivot'][1] * RENDER)
    # The body's radius in metres: half the widest the solid part of it ever gets.
    widest = 0
    for f in layers:
        solid = np.nonzero(((f[..., 3] > 0.3) | (f[..., :3].max(axis=2) > 0.35)).any(axis=0))[0]
        if solid.size:
            widest = max(widest, solid.max() - solid.min() + 1)
    radius = round(float(info['span'] * widest / RENDER / 2), 4) if widest else info['radius']
    entry = dict(cols=cols, rows=rows, frames=len(layers), cell=[w, h],
                 pivot=[round(float(pivot_px[0] - x0) / cw, 4), round(float(pivot_px[1] - y0) / ch, 4)],
                 width=round(float(info['span'] * cw / RENDER), 4), radius=radius, kind=info['kind'],
                 loop=bool(info.get('loop')), hold=info.get('hold', False))
    preview(name, layers, (x0, y0, x1, y1))
    return entry


def preview(name, layers, box):
    x0, y0, x1, y1 = box
    picks = layers[::2]
    cols = 7
    rows = math.ceil(len(picks) / cols)
    w, h = x1 - x0, y1 - y0
    back = road(w, h)
    sheet = np.zeros((rows * h, cols * w, 3), np.float32)
    for i, f in enumerate(picks):
        r, c = divmod(i, cols)
        crop = f[y0:y1, x0:x1]
        sheet[r * h:(r + 1) * h, c * w:(c + 1) * w] = crop[..., :3] + back * (1 - crop[..., 3:4])
    save_png(np.clip(sheet, 0, 1), FRAMES_DIR / f'{name}_preview.png')


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--ids', default='all')
    p.add_argument('--rerender', action='store_true')
    p.add_argument('--no-pack', action='store_true')
    p.add_argument('--quick', action='store_true')
    args = p.parse_args(core.args_after_dashes(sys.argv))
    # Mantaflow leaves its wavelet noise tile in the working folder; keep it with the frames.
    FRAMES_DIR.mkdir(parents=True, exist_ok=True)
    import os
    os.chdir(FRAMES_DIR)
    global QUICK
    QUICK = args.quick
    ids = list(BOOKS) if args.ids == 'all' else args.ids.split(',')
    meta = json.loads(META.read_text()) if META.exists() else {}
    for name in ids:
        print(f'{name}', flush=True)
        info = render(name, BOOKS[name], args.rerender)
        if not args.no_pack:
            meta[name] = pack(name, info, CELLS.get(name, 256), EMIT_GAIN.get(name, 1.0), info.get('fire_alpha', 0.0))
            print(f'  packed {meta[name]}', flush=True)
    if not args.no_pack:
        META.write_text(json.dumps(dict(sorted(meta.items())), indent=1) + '\n')


if __name__ == '__main__':
    main()
