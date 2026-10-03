"""Environment kit for painted-diorama menu backgrounds.

Scene/sky/haze/lighting presets, terrain with painted ground masks, patterned
architectural materials (UVs in metres), vegetation and rocks. Buildings live in
rk.arch, props in rk.dressing. Units are metres, Z up. Objects are instanced from
hidden prototypes where possible to keep scenes light.
"""
import math
import random

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector, noise

from . import core, geo
from . import shaders as S
from .core import srgb
from .nodekit import material

_MATS = {}
C = {}  # collections of the current scene


# ------------------------------------------------------------------ scene


def menu_scene(samples=48, exposure=0.0, look='AgX - Medium High Contrast', res=(1280, 720)):
    scene = core.reset()
    for w in list(bpy.data.worlds):
        if w.users == 0:
            bpy.data.worlds.remove(w)
    S.reset_cache()
    _MATS.clear()
    _PROTOS.clear()
    core.setup_render(scene, samples=samples, transparent=False, exposure=exposure, look=look)
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = 'RGB'
    scene.render.filter_size = 1.3
    scene.cycles.adaptive_threshold = 0.03
    scene.cycles.max_bounces = 6
    scene.cycles.diffuse_bounces = 2
    scene.cycles.glossy_bounces = 2
    scene.cycles.transmission_bounces = 4
    scene.cycles.transparent_max_bounces = 8
    scene.cycles.volume_bounces = 1
    scene.cycles.sample_clamp_direct = 0
    scene.cycles.sample_clamp_indirect = 3.0
    scene.cycles.blur_glossy = 1.0
    scene.cycles.use_denoising = True
    try:
        scene.cycles.denoising_input_passes = 'RGB_ALBEDO_NORMAL'
        scene.cycles.denoising_prefilter = 'ACCURATE'
        scene.cycles.denoising_quality = 'HIGH'
    except (AttributeError, TypeError):
        pass
    C.clear()
    for name in ('Terrain', 'Architecture', 'Props', 'Vegetation', 'FX', 'Lights', 'Atmosphere', 'Rig'):
        C[name] = core.collection(name)
    C['Protos'] = core.collection('Prototypes')
    C['Protos'].hide_render = True
    C['Protos'].hide_viewport = True
    return scene


def camera(location, target, lens=35, dof=None, shift=(0.0, 0.0)):
    cam = core.persp_camera('Camera', location, target, lens, C['Rig'], dof=dof)
    cam.data.sensor_width = 36
    cam.data.shift_x, cam.data.shift_y = shift
    cam.data.clip_end = 2000
    bpy.context.scene.camera = cam
    return cam


# ------------------------------------------------------------------ sky, sun and atmosphere


def sky(scene, zenith='2c3a52', horizon='e9b178', ground='4a3a2c', sun_dir=(-0.6, 0.6, 0.25), sun_glow='ffd59a',
        glow=1.0, glow_power=12.0, strength=1.0, clouds=0.0, cloud_lit='ffd8a8', cloud_dark='6a5a66',
        cloud_scale=1.0, band=0.28, horizon_power=0.55, light_strength=None, mid=None, upper=None):
    """Painted gradient sky with a sun bloom and an optional streaky cloud band.
    light_strength scales how much the sky lights the scene (camera rays still see `strength`)."""
    world = scene.world
    world.use_nodes = True
    from .nodekit import Nodes
    n = Nodes(world.node_tree, output='world')
    d = n.vmath('NORMALIZE', n.tc('Generated'))
    dx, dy, dz = n.xyz(d)
    up = n.math('MAXIMUM', dz, 0.0)
    if mid is None and upper is None:
        t = n.math('POWER', n.maprange(up, 0.0, 0.85), horizon_power)
        col = n.mixc(t, horizon, zenith)
    else:
        col = n.ramp(up, [(0.0, horizon), (0.07, mid or horizon), (0.32, upper or zenith), (0.9, zenith)])
    # warm haze hugging the horizon
    hz = n.math('POWER', n.maprange(n.math('ABSOLUTE', dz), 0.0, 0.12, 1.0, 0.0), 2.0)
    col = n.mixc(n.mul(hz, 0.45), col, horizon)
    sd = Vector(sun_dir).normalized()
    cosang = n.math('MAXIMUM', n.vmath('DOT_PRODUCT', d, tuple(sd)), 0.0)
    bloom = n.add(n.mul(n.math('POWER', cosang, glow_power), glow),
                  n.mul(n.math('POWER', cosang, glow_power * 18), glow * 2.5))
    col = n.mixc(n.math('MINIMUM', bloom, 1.0), col, sun_glow)
    if clouds:
        # project onto a cloud deck and streak horizontally
        k = n.math('DIVIDE', 1.0, n.add(dz, 0.06))
        uvx = n.mul(dx, k)
        uvy = n.mul(dy, k)
        vec = n.combine(n.mul(uvx, 0.35 * cloud_scale), n.mul(uvy, 1.6 * cloud_scale), 0.0)
        c1 = n.noise(vec, 1.4, 6, 0.62, distortion=0.25)
        c2 = n.noise(n.vmath('MULTIPLY', vec, (2.4, 1.0, 1.0)), 3.0, 4, 0.55)
        cl = n.add(n.mul(c1, 0.75), n.mul(c2, 0.35))
        mask = n.smooth(cl, 0.5, 0.78)
        deck = n.mul(n.smooth(dz, 0.015, 0.06), n.smooth(dz, band + 0.25, band, 0.0, 1.0))
        mask = n.mul(n.mul(mask, deck), clouds)
        lit = n.mixc(n.math('POWER', cosang, 3.0), cloud_dark, cloud_lit)
        lit = n.mixc(n.smooth(c2, 0.35, 0.7), cloud_dark, lit)
        col = n.mixc(mask, col, lit)
    below = n.smooth(dz, 0.0, -0.08, 0.0, 1.0)
    col = n.mixc(below, col, ground)
    bg = n.node('ShaderNodeBackground')
    n.feed(bg.inputs['Color'], col)
    n.feed(bg.inputs['Strength'], strength)
    if light_strength is not None and light_strength != strength:
        # camera sees the painted sky; everything else is lit by a dimmer copy
        bg2 = n.node('ShaderNodeBackground')
        n.feed(bg2.inputs['Color'], col)
        n.feed(bg2.inputs['Strength'], light_strength)
        lp = n.node('ShaderNodeLightPath')
        mix = n.node('ShaderNodeMixShader')
        n.links.new(lp.outputs['Is Camera Ray'], mix.inputs[0])
        n.links.new(bg2.outputs[0], mix.inputs[1])
        n.links.new(bg.outputs[0], mix.inputs[2])
        n.links.new(mix.outputs[0], n.out.inputs['Surface'])
    else:
        n.links.new(bg.outputs[0], n.out.inputs['Surface'])
    return world


def sun(direction, strength=4.0, color='ffc98a', angle=1.5, name='Sun'):
    return core.sun_light(name, Vector(direction).normalized(), strength, srgb(color), angle, C['Lights'])


def haze(center=(0, 0, 0), size=(200, 200, 40), density=0.02, color='e8c9a0', anisotropy=0.45, falloff=12.0,
         absorption=0.0, name='Haze', noise_amt=0.0, soft=False, emission=0.0):
    """Box of participating media; density decays with height for ground-hugging aerial perspective."""
    m, n = material(name)
    p = n.tc('Object')
    _, _, z = n.xyz(p)
    if falloff:
        zr = n.add(n.mul(z, size[2] / 2), size[2] / 2)  # metres above box bottom (object coords are -1..1)
        dens = n.mul(n.math('EXPONENT', n.mul(zr, -1.0 / falloff)), density)
    else:
        dens = density  # homogeneous: analytically sampled, much cheaper
    if soft:
        # ellipsoidal falloff to zero at the box faces: no visible volume boundaries
        r = n.vmath('LENGTH', p)
        dens = n.mul(dens, n.math('POWER', n.smooth(r, 1.0, 0.15), 1.5))
    if noise_amt:
        wp = n.tc('Object')
        nz = n.noise(n.vmath('MULTIPLY', wp, (size[0] / 40, size[1] / 40, size[2] / 15)), 1.5, 3, .5)
        dens = n.mul(dens, n.maprange(nz, 0.3, 0.7, 1.0 - noise_amt, 1.0 + noise_amt))
    n.volume(density=dens, color=color, absorption=(1 - absorption, 1 - absorption, 1 - absorption, 1),
             anisotropy=anisotropy, emission=n.mul(dens, emission / max(density, 1e-6)) if emission else 0.0,
             emission_color=color)
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=2.0)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    C['Atmosphere'].objects.link(obj)
    obj.location = (center[0], center[1], center[2] + size[2] / 2)
    obj.scale = (size[0] / 2, size[1] / 2, size[2] / 2)
    me.materials.append(m)
    obj.visible_shadow = False
    return obj


def point(location, power=60, color='ffb04a', radius=0.1, name='Lamp'):
    return core.point_light(name, location, power, srgb(color), radius, C['Lights'])


def spot(location, target, power=500, color='ffd29a', size_deg=40, blend=0.6, radius=0.2, name='Spot'):
    data = bpy.data.lights.new(name, 'SPOT')
    data.energy = power
    data.color = srgb(color)[:3]
    data.spot_size = math.radians(size_deg)
    data.spot_blend = blend
    data.shadow_soft_size = radius
    obj = bpy.data.objects.new(name, data)
    C['Lights'].objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector(target) - Vector(location)).to_track_quat('-Z', 'Y').to_euler()
    return obj


def area(location, target, power, color='ffffff', size=4.0, name='Area', shape='DISK', spread=None):
    return core.area_light(name, location, target, power, srgb(color), size, C['Lights'], shape=shape, spread=spread)


# ------------------------------------------------------------------ mesh utilities


def store_local(obj, name='lc'):
    """Freeze the current (local) vertex positions into an attribute that survives later transforms."""
    me = obj.data
    if name in me.attributes:
        me.attributes.remove(me.attributes[name])
    attr = me.attributes.new(name, 'FLOAT_VECTOR', 'POINT')
    co = [0.0] * (len(me.vertices) * 3)
    me.vertices.foreach_get('co', co)
    attr.data.foreach_set('vector', co)
    return obj


def uv_box(obj, scale=1.0, name='UVMap'):
    """Box-projected UVs in metres (pattern size independent of face size)."""
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    uv = bm.loops.layers.uv.get(name) or bm.loops.layers.uv.new(name)
    for f in bm.faces:
        nrm = f.normal
        ax = max(range(3), key=lambda i: abs(nrm[i]))
        for lp in f.loops:
            co = lp.vert.co
            if ax == 0:
                u, v = co.y * (1 if nrm.x > 0 else -1), co.z
            elif ax == 1:
                u, v = co.x * (-1 if nrm.y > 0 else 1), co.z
            else:
                u, v = co.x, co.y
            lp[uv].uv = (u * scale, v * scale)
    bm.to_mesh(me)
    bm.free()
    return obj


def uv_cyl(obj, radius=None, scale=1.0, center=(0, 0), name='UVMap'):
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    uv = bm.loops.layers.uv.get(name) or bm.loops.layers.uv.new(name)
    for f in bm.faces:
        nrm = f.normal
        if abs(nrm.z) > 0.8:
            for lp in f.loops:
                lp[uv].uv = (lp.vert.co.x * scale, lp.vert.co.y * scale)
            continue
        us = []
        for lp in f.loops:
            co = lp.vert.co
            r = radius or max(0.01, math.hypot(co.x - center[0], co.y - center[1]))
            us.append(math.atan2(co.y - center[1], co.x - center[0]) * (radius or r))
        span = (radius or 1.0) * math.pi
        if max(us) - min(us) > span:
            us = [u + 2 * span if u < 0 else u for u in us]
        for lp, u in zip(f.loops, us):
            lp[uv].uv = (u * scale, lp.vert.co.z * scale)
    bm.to_mesh(me)
    bm.free()
    return obj


def xform(obj, loc=(0, 0, 0), rot_z=0.0, scale=1.0, rot=None):
    """Bake a placement into mesh data (keeps the kit's world-space authoring convention)."""
    if isinstance(scale, (int, float)):
        scale = (scale, scale, scale)
    if rot is None:
        rot = (0, 0, rot_z)
    # NB: env rotations are radians (geo.place uses degrees)
    m = Matrix.LocRotScale(Vector(loc), Euler(rot, 'XYZ').to_quaternion(), Vector(scale))
    return geo.transform(obj, m)


def finish(obj, coll=None, uv='box', local=True, uv_scale=1.0, smooth=None):
    """Common post-step for authored-at-origin parts: local coords + metre UVs."""
    if local:
        store_local(obj)
    if uv == 'box':
        uv_box(obj, uv_scale)
    elif uv == 'cyl':
        uv_cyl(obj, scale=uv_scale)
    if smooth is not None:
        for p in obj.data.polygons:
            p.use_smooth = smooth
    if coll is not None:
        core.link(obj, coll)
    return obj


def group_xform(objs, loc=(0, 0, 0), rot_z=0.0, scale=1.0):
    for o in objs:
        if o.type == 'MESH':
            xform(o, loc, rot_z, scale)
        else:
            m = Matrix.LocRotScale(Vector(loc), Euler((0, 0, rot_z), 'XYZ').to_quaternion(), Vector((scale,) * 3))
            o.matrix_world = m @ o.matrix_world
    return objs


# ------------------------------------------------------------------ prototypes / instancing
_PROTOS = {}


def proto(key, builder):
    """Build once, keep hidden; instances share the mesh."""
    obj = _PROTOS.get(key)
    if obj is None or obj.name not in bpy.data.objects:
        obj = builder()
        core.link(obj, C['Protos'])
        _PROTOS[key] = obj
    return obj


def inst(src, loc, rot_z=0.0, scale=1.0, coll=None, tilt=(0.0, 0.0)):
    o = src.copy()
    (coll or C['Props']).objects.link(o)
    for c in list(o.users_collection):
        if c == C['Protos']:
            c.objects.unlink(o)
    o.location = loc
    o.rotation_euler = (tilt[0], tilt[1], rot_z)
    o.scale = (scale, scale, scale) if isinstance(scale, (int, float)) else scale
    return o


# ------------------------------------------------------------------ materials


def mat_cached(key, fn):
    m = _MATS.get(key)
    if m is None or m.name not in bpy.data.materials:
        m = fn()
        _MATS[key] = m
    return m


def _rest_or_obj(n):
    return n.rest()


def snow_color(n, p):
    """Wind-blown snow: streaks along X, sastrugi ripples, blue-grey hollows, bright crests."""
    streak = n.noise(n.vmath('MULTIPLY', p, (0.25, 2.2, 1.0)), 1.6, 4, 0.6)
    ripple = n.wave(n.vmath('MULTIPLY', p, (0.6, 1.0, 1.0)), 1.6, 6.0, 2.0, 'Y')
    tone = n.add(n.mul(streak, 0.7), n.mul(ripple, 0.3))
    col = n.ramp(tone, [(0.25, 'aebfd4'), (0.5, 'd6e0ea'), (0.72, 'f2f5f8'), (0.9, 'fbfcfd')])
    hollow = n.ao(1.2, local=False, samples=4)
    return n.mixc(n.maprange(hollow, 0.3, 1.0, 0.55, 0.0), col, '8ea4c4')


def snow_rock(name='Snow-capped rock', color='6e6c6a', cover=0.55):
    """Dark rock with snow settled on upward faces and in cracks."""
    def build():
        m, n = material(name)
        p = n.rest()
        big = n.noise(p, 0.8, 4, 0.6)
        fine = n.noise(p, 6.0, 4, 0.65)
        rc = n.mixc(n.maprange(big, 0.3, 0.7), n.hsv(srgb(color), 0.5, 0.9, 0.7), color)
        rc = n.mixc(n.maprange(fine, 0.35, 0.7, 0, 0.35), rc, n.hsv(srgb(color), 0.5, 0.8, 1.3))
        nz = n.xyz(n.geom('Normal'))[2]
        sm = n.smooth(n.add(nz, n.mul(n.add(big, -0.5), 0.6)), 0.75 - cover * 0.5, 0.85 - cover * 0.5)
        col = n.mixc(sm, rc, snow_color(n, p))
        h = n.add(big, n.mul(fine, 0.5))
        n.principled(**{'Base Color': col, 'Roughness': n.mixf(sm, 0.85, 0.55),
                        'Normal': n.bump(h, n.mixf(sm, 0.6, 0.15), 0.05)})
        return m
    return mat_cached(('snowrock', name, color, cover), build)


def ground(name='Ground', grass=('4f6230', '6f7d3a', '9a9446'), dry='b39a58', dirt=('7c5f42', '5e4632'),
           cobble=('8f8576', '6e665a'), rock='7d776c', snow=0.0, scale=1.0, moss=0.0, wet=0.0, ash=0.0,
           ash_color='3a3330', flowers=0.5, dry_amount=0.25, cobble_scale=4.2, snow_height=None, flower_colors=('efe6c8', 'e3c24a', '9a86b8')):
    """Painterly ground: patchy green/golden meadow with wildflowers, dirt where attribute `path`,
    cobbles where `cobble`, rock on slopes. Attributes are optional (0 when absent)."""
    def build():
        m, n = material(name)
        p = n.rest()
        ps = n.vmath('SCALE', p, scale=scale)
        large = n.noise(ps, 0.05, 3, 0.55)
        patch = n.noise(ps, 0.12, 4, 0.6, distortion=0.4)
        mid = n.noise(ps, 0.45, 4, 0.6)
        fine = n.noise(ps, 3.0, 3, 0.6)
        g = n.ramp(n.add(n.mul(large, 0.5), n.mul(mid, 0.5)), [(0.3, grass[0]), (0.5, grass[1]), (0.72, grass[2])])
        drym = n.smooth(n.add(patch, n.mul(mid, 0.25)), 0.62 - dry_amount * 0.25, 0.72 - dry_amount * 0.25)
        g = n.mixc(n.mul(drym, 0.85), g, dry)
        streak = n.noise(n.vmath('MULTIPLY', ps, (6.0, 1.2, 1.0)), 2.0, 3, 0.6)
        g = n.mixc(n.maprange(n.add(n.mul(fine, 0.6), n.mul(streak, 0.4)), 0.3, 0.7, 0.0, 0.3), g,
                   n.hsv(g, 0.5, 1.05, 0.65))
        tuft = n.maprange(n.voronoi(ps, 2.2, 'F1'), 0.0, 0.2, 1.0, 0.0)
        g = n.mixc(n.mul(tuft, 0.3), g, n.hsv(g, 0.5, 1.15, 0.55))
        if flowers:
            fv = n.voronoi(ps, 7.0, 'F1')
            fc = n.xyz(n.voronoi(ps, 7.0, 'F1', out='Color'))
            fm = n.mul(n.smooth(fv, 0.09, 0.05), n.smooth(n.noise(ps, 0.3, 2, 0.5), 0.5, 0.62))
            fm = n.mul(fm, n.mul(flowers, n.math('SUBTRACT', 1.0, drym)))
            fcol = n.mixc(n.smooth(fc[0], 0.33, 0.34), flower_colors[0], flower_colors[1])
            fcol = n.mixc(n.smooth(fc[0], 0.75, 0.76), fcol, flower_colors[2])
            g = n.mixc(fm, g, fcol)
        # dirt
        dmix = n.noise(ps, 1.2, 4, 0.6)
        dcol = n.mixc(n.smooth(dmix, 0.4, 0.6), dirt[0], dirt[1])
        peb = n.maprange(n.voronoi(ps, 9.0, 'F1'), 0.0, 0.16, 1.0, 0.0)
        dcol = n.mixc(n.mul(peb, 0.35), dcol, n.hsv(dcol, 0.5, 0.6, 1.4))
        rut = n.noise(n.vmath('MULTIPLY', ps, (3.0, 0.15, 1.0)), 1.5, 3, 0.5)
        dcol = n.mixc(n.mul(n.smooth(rut, 0.55, 0.7), 0.4), dcol, n.hsv(dcol, 0.5, 1.0, 0.7))
        path = n.attr('path', out='Fac')
        pmask = n.smooth(n.add(path, n.mul(n.add(mid, -0.5), 0.6)), 0.35, 0.6)
        col = n.mixc(pmask, g, dcol)
        # cobbles
        cob = n.attr('cobble', out='Fac')
        cv = n.vmath('MULTIPLY', ps, (1, 1, 0.2))
        cell = n.voronoi(cv, cobble_scale, 'F1', out='Color')
        edge = n.voronoi(cv, cobble_scale, 'DISTANCE_TO_EDGE')
        stone_c = n.mixc(n.xyz(cell)[0], cobble[0], cobble[1])
        stone_c = n.mixc(n.maprange(fine, 0.3, 0.7, 0, 0.3), stone_c, n.hsv(stone_c, 0.5, 0.8, 0.75))
        mortar = n.smooth(edge, 0.03, 0.09, 1.0, 0.0)
        stone_c = n.mixc(n.mul(mortar, 0.85), stone_c, dcol)
        cmask = n.smooth(n.add(cob, n.mul(n.add(mid, -0.5), 0.5)), 0.4, 0.55)
        col = n.mixc(cmask, col, stone_c)
        nz = n.xyz(n.geom('Normal'))[2]
        slope = n.smooth(nz, 0.86, 0.7)
        rcol = n.mixc(n.maprange(fine, 0.3, 0.7), rock, n.hsv(srgb(rock), 0.5, 0.9, 0.7))
        col = n.mixc(slope, col, rcol)
        if snow:
            sm = n.mul(n.smooth(nz, 0.6, 0.9), n.smooth(n.add(large, n.mul(fine, 0.2)), 0.55 - snow * 0.4,
                                                       0.65 - snow * 0.4))
            col = n.mixc(sm, col, snow_color(n, ps))
        if snow_height is not None:
            _, _, zz = n.xyz(p)
            line = n.add(zz, n.mul(n.add(mid, -0.5), 10.0))
            sm = n.mul(n.smooth(line, snow_height - 2.0, snow_height + 2.0), n.smooth(nz, 0.45, 0.75))
            col = n.mixc(sm, col, 'eef2f6')
        if ash:
            am = n.smooth(n.add(large, n.mul(mid, 0.3)), 0.62 - ash * 0.4, 0.75 - ash * 0.4)
            col = n.mixc(n.mul(am, ash), col, ash_color)
        h = n.add(n.mul(fine, 0.4), n.mul(tuft, 0.3))
        h = n.add(h, n.mul(streak, 0.2))
        h = n.mixf(cmask, h, n.add(n.mul(mortar, -0.6), n.mul(fine, 0.15)))
        h = n.mixf(pmask, h, n.add(n.mul(peb, 0.4), n.mul(rut, 0.3)))
        rough = n.mixf(cmask, 0.92, 0.75)
        if wet:
            rough = n.mixf(n.mul(pmask, wet), rough, 0.25)
        n.principled(**{'Base Color': col, 'Roughness': rough, 'Normal': n.bump(h, 0.4, 0.08)})
        return m
    return mat_cached(('ground', name), build)


def masonry(name='Masonry', color='9a8f7c', color2='7d7364', mortar='5a5248', block=(0.62, 0.3), scale=1.0,
            moss=0.0, soot=0.0, rough=0.88, mortar_size=0.022, dirt_bottom=0.35):
    """Coursed ashlar on metre UVs: per-block colour, chipped edges, grime toward the ground."""
    def build():
        m, n = material(name)
        uv = n.vmath('SCALE', n.tc('UV'), scale=scale)
        p = n.rest()
        b = n.node('ShaderNodeTexBrick', offset=0.5, offset_frequency=2, squash=1.0, squash_frequency=2)
        n.feed(b.inputs['Vector'], uv)
        n.feed(b.inputs['Color1'], color)
        n.feed(b.inputs['Color2'], color2)
        n.feed(b.inputs['Mortar'], mortar)
        b.inputs['Scale'].default_value = 1.0
        b.inputs['Mortar Size'].default_value = mortar_size
        b.inputs['Mortar Smooth'].default_value = 0.35
        b.inputs['Bias'].default_value = 0.0
        b.inputs['Brick Width'].default_value = block[0]
        b.inputs['Row Height'].default_value = block[1]
        col = b.outputs['Color']
        mort = b.outputs['Factor']
        fine = n.noise(p, 6.0, 4, 0.6)
        big = n.noise(p, 0.6, 3, 0.55)
        col = n.mixc(n.maprange(fine, 0.3, 0.7, 0.0, 0.35), col, n.hsv(col, 0.5, 0.85, 0.72))
        col = n.mixc(n.maprange(big, 0.35, 0.7, 0.0, 0.3), col, n.hsv(col, 0.52, 1.1, 1.15))
        _, _, z = n.xyz(p)
        if dirt_bottom:
            grime = n.mul(n.smooth(z, 1.2, 0.0), dirt_bottom)
            col = n.mixc(grime, col, '4a3e30')
        if soot:
            sm = n.mul(n.smooth(big, 0.45, 0.7), soot)
            col = n.mixc(sm, col, '1e1b19')
        if moss:
            mm = n.mul(n.smooth(n.add(big, n.mul(fine, 0.4)), 0.55, 0.75), moss)
            col = n.mixc(mm, col, '55622f')
        h = n.add(n.mul(mort, -1.0), n.mul(fine, 0.35))
        n.principled(**{'Base Color': col, 'Roughness': rough, 'Normal': n.bump(h, 0.45, 0.04)})
        return m
    return mat_cached(('masonry', name), build)


def plaster(name='Plaster', color='d6c7a6', dirt='8c7a5c', rough=0.9):
    def build():
        m, n = material(name)
        p = n.rest()
        a = n.noise(p, 1.2, 4, 0.6)
        b = n.noise(p, 9.0, 3, 0.6)
        col = n.mixc(n.maprange(a, 0.35, 0.7, 0.0, 0.5), color, dirt)
        col = n.mixc(n.maprange(b, 0.4, 0.7, 0.0, 0.15), col, n.hsv(srgb(color), 0.5, 1.0, 0.8))
        _, _, z = n.xyz(p)
        col = n.mixc(n.mul(n.smooth(z, 1.5, 0.0), 0.4), col, dirt)
        n.principled(**{'Base Color': col, 'Roughness': rough, 'Normal': n.bump(n.add(a, n.mul(b, 0.5)), 0.12, 0.03)})
        return m
    return mat_cached(('plaster', name), build)


def planks(name='Planks', color='6e4c30', board=0.22, length=2.4, gap='241a12', rough=0.75, axis_v=False, scale=1.0,
           weather=0.2):
    """Boards along U (or V) on metre UVs."""
    def build():
        m, n = material(name)
        uv = n.vmath('SCALE', n.tc('UV'), scale=scale)
        if axis_v:
            ux, uy, _ = n.xyz(uv)
            uv = n.combine(uy, ux, 0.0)
        b = n.node('ShaderNodeTexBrick', offset=0.37, offset_frequency=1, squash=1.0, squash_frequency=1)
        n.feed(b.inputs['Vector'], uv)
        base = srgb(color)
        n.feed(b.inputs['Color1'], base)
        n.feed(b.inputs['Color2'], tuple(c * 0.72 for c in base[:3]) + (1,))
        n.feed(b.inputs['Mortar'], gap)
        b.inputs['Scale'].default_value = 1.0
        b.inputs['Mortar Size'].default_value = 0.012
        b.inputs['Mortar Smooth'].default_value = 0.2
        b.inputs['Brick Width'].default_value = length
        b.inputs['Row Height'].default_value = board
        col = b.outputs['Color']
        ux, uy, _ = n.xyz(uv)
        grain = n.noise(n.combine(n.mul(ux, 0.6), n.mul(uy, 14.0), 0), 3.0, 5, 0.6, distortion=1.0)
        col = n.mixc(n.maprange(grain, 0.3, 0.7, 0.0, 0.45), col, n.hsv(col, 0.5, 0.8, 0.6))
        if weather:
            w = n.noise(n.rest(), 1.5, 3, 0.5)
            col = n.mixc(n.mul(n.smooth(w, 0.5, 0.75), weather), col, '8a8478')
        h = n.add(n.mul(b.outputs['Factor'], -1.0), n.mul(grain, 0.3))
        n.principled(**{'Base Color': col, 'Roughness': rough, 'Normal': n.bump(h, 0.4, 0.02)})
        return m
    return mat_cached(('planks', name), build)


def roof_tiles(name='Roof tiles', color='9c4a2e', color2='7a3622', tile=(0.26, 0.2), moss=0.15, curve=True,
               rough=0.7, gap='3a1e14'):
    """Clay tiles (curve=True gives barrel-tile ridges) or slates (curve=False) on metre UVs."""
    def build():
        m, n = material(name)
        uv = n.tc('UV')
        p = n.rest()
        b = n.node('ShaderNodeTexBrick', offset=0.5, offset_frequency=2, squash=1.0, squash_frequency=2)
        n.feed(b.inputs['Vector'], uv)
        n.feed(b.inputs['Color1'], color)
        n.feed(b.inputs['Color2'], color2)
        n.feed(b.inputs['Mortar'], gap)
        b.inputs['Scale'].default_value = 1.0
        b.inputs['Mortar Size'].default_value = 0.012
        b.inputs['Mortar Smooth'].default_value = 0.5
        b.inputs['Brick Width'].default_value = tile[0]
        b.inputs['Row Height'].default_value = tile[1]
        col = b.outputs['Color']
        fine = n.noise(p, 5.0, 4, 0.6)
        big = n.noise(p, 0.5, 3, 0.5)
        col = n.mixc(n.maprange(fine, 0.3, 0.7, 0.0, 0.3), col, n.hsv(col, 0.5, 0.8, 0.7))
        col = n.mixc(n.maprange(big, 0.35, 0.7, 0.0, 0.35), col, n.hsv(col, 0.48, 0.7, 1.25))
        if moss:
            mm = n.mul(n.smooth(n.add(big, n.mul(fine, 0.5)), 0.5, 0.75), moss)
            col = n.mixc(mm, col, '5d6233')
        ux, uy, _ = n.xyz(uv)
        h = n.mul(b.outputs['Factor'], -1.0)
        if curve:
            ridge = n.math('SINE', n.mul(ux, math.tau / tile[0]))
            h = n.add(h, n.mul(ridge, 0.5))
        # row overlap shading: darker at the top of each course
        lap = n.math('FRACT', n.math('DIVIDE', uy, tile[1]))
        col = n.mixc(n.mul(n.smooth(lap, 0.6, 1.0), 0.35), col, n.hsv(col, 0.5, 1.0, 0.55))
        h = n.add(h, n.mul(lap, 0.6))
        n.principled(**{'Base Color': col, 'Roughness': rough, 'Normal': n.bump(h, 0.5, 0.03)})
        return m
    return mat_cached(('tiles', name), build)


def thatch(name='Thatch', color='a88a52', dark='5e4a2a'):
    def build():
        m, n = material(name)
        uv = n.tc('UV')
        ux, uy, _ = n.xyz(uv)
        straw = n.noise(n.combine(n.mul(ux, 18.0), n.mul(uy, 1.2), 0), 4.0, 4, 0.6)
        clump = n.noise(n.rest(), 1.5, 3, 0.55)
        col = n.mixc(n.smooth(straw, 0.3, 0.7), dark, color)
        col = n.mixc(n.maprange(clump, 0.4, 0.7, 0.0, 0.4), col, '6a6a3a')
        n.principled(**{'Base Color': col, 'Roughness': 0.95, 'Normal': n.bump(straw, 0.6, 0.05)})
        return m
    return mat_cached(('thatch', name), build)


def stripes(name='Stripes', a='1c6e69', b='d9ccb0', count=16, axis='angle', width=0.5, trim=None, sheen=0.5):
    """Cloth stripes from the local-coordinate attribute: radial (`angle`) or linear along X/Y."""
    def build():
        m, n = material(name)
        lc = n.attr('lc')
        x, y, z = n.xyz(lc)
        if axis == 'angle':
            t = n.mul(n.math('ARCTAN2', y, x), count / math.tau)
        elif axis == 'x':  # linear stripes: `count` per metre
            t = n.mul(x, count)
        elif axis == 'z':
            t = n.mul(z, count)
        else:
            t = n.mul(y, count)
        f = n.math('FRACT', t)
        band = n.smooth(f, width - 0.02, width + 0.02)
        col = n.mixc(band, a, b)
        weave = n.noise(n.rest(), 30.0, 2, 0.5)
        dirt = n.noise(n.rest(), 1.0, 3, 0.55)
        col = n.mixc(n.maprange(dirt, 0.4, 0.7, 0.0, 0.3), col, n.hsv(col, 0.5, 0.8, 0.65))
        n.principled(**{'Base Color': col, 'Roughness': 0.85, 'Sheen Weight': sheen, 'Sheen Roughness': 0.4,
                        'Normal': n.bump(weave, 0.08, 0.01)})
        return m
    return mat_cached(('stripes', name), build)


def foliage(name='Foliage', a='2f4220', b='5a6e2a', c='9a9a3a', translucency=0.3, clump=3.0, hue_var=0.0,
            leaf=14.0, sheen=0.25):
    """Leafy canopy shading: per-clump colour, leaf-scale breakup, inner-canopy occlusion, back-light glow."""
    def build():
        m, n = material(name)
        p = n.rest()
        cells = n.voronoi(p, clump, 'F1', out='Color')
        cl = n.xyz(cells)[0]
        large = n.noise(p, 0.3, 3, 0.5)
        col = n.ramp(n.add(n.mul(cl, 0.6), n.mul(large, 0.4)), [(0.2, a), (0.55, b), (0.9, c)])
        lf = n.voronoi(p, leaf, 'F1')
        lcol = n.voronoi(p, leaf, 'F1', out='Color')
        col = n.mixc(n.maprange(n.xyz(lcol)[1], 0.0, 1.0, -0.15, 0.25), col, n.hsv(col, 0.5, 1.05, 1.35))
        col = n.mixc(n.mul(n.smooth(lf, 0.25, 0.55), 0.55), col, n.hsv(col, 0.5, 1.15, 0.5))
        occ = n.attr('occ', out='Fac')  # baked crown-depth occlusion (1 = deep inside)
        col = n.mixc(n.maprange(occ, 0.25, 1.0, 0.0, 0.8), col, n.hsv(col, 0.53, 1.25, 0.3))
        h = n.add(n.mul(lf, -1.0), n.mul(n.voronoi(p, clump * 2.5, 'F1'), -0.6))
        bsdf = n.principled(connect=False, **{'Base Color': col, 'Roughness': 0.62, 'Specular IOR Level': 0.35,
                                              'Sheen Weight': sheen, 'Normal': n.bump(h, 0.85, 0.06)})
        tr = n.node('ShaderNodeBsdfTranslucent')
        n.feed(tr.inputs['Color'], n.hsv(col, 0.46, 1.4, 1.1))
        n.surface(n.mix_shader(translucency, bsdf, tr.outputs[0]))
        return m
    return mat_cached(('foliage', name), build)


def bark(name='Bark', color='4a3a2c'):
    def build():
        m, n = material(name)
        p = n.rest()
        streak = n.noise(n.vmath('MULTIPLY', p, (6, 6, 0.8)), 2.0, 5, 0.6, distortion=0.6)
        col = n.mixc(n.smooth(streak, 0.3, 0.7), n.hsv(srgb(color), 0.5, 1.0, 0.55), color)
        n.principled(**{'Base Color': col, 'Roughness': 0.9, 'Normal': n.bump(streak, 0.6, 0.05)})
        return m
    return mat_cached(('bark', name), build)


def water(name='Water', deep='1d3a40', shallow='4f7a72'):
    def build():
        m, n = material(name)
        p = n.rest()
        w = n.noise(n.vmath('MULTIPLY', p, (0.6, 2.0, 1.0)), 2.0, 4, 0.55)
        col = n.mixc(n.maprange(w, 0.3, 0.7), deep, shallow)
        n.principled(**{'Base Color': col, 'Roughness': 0.06, 'Specular IOR Level': 0.6, 'Coat Weight': 0.3,
                        'Normal': n.bump(w, 0.25, 0.1)})
        return m
    return mat_cached(('water', name), build)


def glow_mat(color='ffb04a', strength=8.0, name=None, core_color='fff0c4'):
    return mat_cached(('glow', color, strength, name), lambda: S.emissive(color, strength, name=name or f'Glow {color}',
                                                                       core=core_color))


def window_glass(color='ffb85c', strength=3.0, name='Lit window'):
    """Warm interior glow behind leaded panes."""
    def build():
        m, n = material(name)
        uv = n.tc('UV')
        ux, uy, _ = n.xyz(uv)
        lead = n.math('MAXIMUM', n.smooth(n.math('ABSOLUTE', n.math('SINE', n.mul(n.add(ux, uy), 22.0))), 0.92, 1.0),
                      n.smooth(n.math('ABSOLUTE', n.math('SINE', n.mul(n.math('SUBTRACT', ux, uy), 22.0))), 0.92, 1.0))
        flick = n.noise(n.rest(), 0.8, 2, 0.5)
        st = n.mul(n.maprange(flick, 0.3, 0.7, 0.6, 1.2), strength)
        st = n.mul(st, n.math('SUBTRACT', 1.0, lead))
        n.principled(**{'Base Color': (0.05, 0.04, 0.03, 1), 'Roughness': 0.3, 'Emission Color': color,
                        'Emission Strength': st})
        return m
    return mat_cached(('window', color, strength, name), build)


def flat(color, rough=0.8, name=None, metallic=0.0):
    return mat_cached(('flat', color, rough, metallic), lambda: S.flat(color, name=name or f'Flat {color}', rough=rough,
                                                                     metallic=metallic))


# ------------------------------------------------------------------ terrain


def terrain(size=(160, 120), res=(200, 150), center=(0, 0), height=None, masks=None, mat=None, name='Terrain'):
    """Height-field ground. height(x, y) -> z; masks: {attr_name: fn(x, y) -> 0..1}."""
    sx, sy = size
    cx, cy = center
    hf = height or (lambda x, y: 0.0)

    def fn(u, v):
        x = cx + (u - 0.5) * sx
        y = cy + (v - 0.5) * sy
        return (x, y, hf(x, y))
    obj = geo.grid_sheet(name, sx, sy, res[0], res[1], fn, mat or ground(), C['Terrain'])
    me = obj.data
    for key, mf in (masks or {}).items():
        attr = me.attributes.new(key, 'FLOAT', 'POINT')
        vals = [float(mf(v.co.x, v.co.y)) for v in me.vertices]
        attr.data.foreach_set('value', vals)
    return obj


def fbm(x, y, scale=0.05, octaves=4, seed=0):
    v = 0.0
    amp = 1.0
    f = scale
    for i in range(octaves):
        v += amp * noise.noise(Vector((x * f + seed * 17.3, y * f + seed * 5.1, seed * 3.7 + i * 11.0)))
        amp *= 0.5
        f *= 2.0
    return v


def seg_dist(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / max(1e-9, dx * dx + dy * dy)))
    return math.hypot(px - ax - t * dx, py - ay - t * dy)


def poly_dist(px, py, pts):
    return min(seg_dist(px, py, *a, *b) for a, b in zip(pts, pts[1:]))


def smoothstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def road_mask(pts, width, soft=1.5):
    def f(x, y):
        return 1.0 - smoothstep(width / 2, width / 2 + soft, poly_dist(x, y, pts))
    return f


def mountains(n=5, radius=260, height=(40, 90), arc=(-0.9, 0.9), base_z=-2, mat=None, seed=3, center=(0, 0),
              direction=90, depth=1, name='Mountains', fog='c9b49a', fog_dist=420.0, rock=('5a5650', '7a7266'),
              green='5f6a3c', snow=0.0, jag=0.0):
    """Ring of jagged displaced ranges far behind the scene with painted aerial perspective."""
    rng = random.Random(seed)
    objs = []
    mat = mat or mat_cached(('mountain', name, fog, fog_dist), lambda: aerial_mat(name, fog, fog_dist, rock, green,
                                                                                    snow))
    for i in range(n):
        t = arc[0] + (arc[1] - arc[0]) * (i + rng.uniform(0.2, 0.8)) / n
        ang = math.radians(direction) + t
        r = radius * rng.uniform(0.85, 1.15)
        h = rng.uniform(*height)
        w = h * rng.uniform(1.6, 2.6)
        o = geo.quadsphere(f'{name} {i}', 1.0, (0, 0, 0), mat, C['Terrain'], level=4)
        geo.displace_noise(o, 0.18, 2.2, seed=seed + i)
        geo.displace_noise(o, 0.06, 7.0, seed=seed + i + 9)
        if jag:
            # sharpen into peaks and ridges: pinch the upper half toward a few crests
            for v in o.data.vertices:
                if v.co.z > 0:
                    t = v.co.z
                    k = 1 - jag * 0.55 * t
                    v.co.x *= k
                    v.co.y *= k
                    v.co.z = t ** (1 + 0.4 * jag) * (1 + 0.25 * jag * noise.noise(Vector((v.co.x * 3, v.co.y * 3, seed))))
        for v in o.data.vertices:
            if v.co.z < 0:
                v.co.z *= 0.1
        o.data.update()
        xform(o, (center[0] + math.cos(ang) * r, center[1] + math.sin(ang) * r, base_z), rng.uniform(0, 6.28),
              (w, w * 0.7, h))
        o.visible_shadow = False  # distant ranges must never eclipse a low sun
        objs.append(o)
    return objs


def _blob(name, r, loc, mat, seed, amt=0.25, sc=1.3, level=3, squash=1.0):
    o = geo.quadsphere(name, r, (0, 0, 0), mat, None, level=level, scale=(1, 1, squash))
    geo.displace_noise(o, r * amt, sc / r, seed=seed)
    geo.displace_noise(o, r * amt * 0.35, sc * 3.0 / r, seed=seed + 5)
    geo.place(o, loc)
    return o


def conifer(seed=0, h=9.0, palette=('22381f', '35532c', '62743a')):
    rng = random.Random(seed)
    fol = foliage(f'Pine needles {palette[0]}', *palette, translucency=0.15, clump=3.0, leaf=24.0)
    parts = [geo.cylinder('Trunk', h * 0.025, h * 0.5, (0, 0, h * 0.22), bark(), None, 8, bevel=0, radius2=h * 0.012)]
    tiers = 6
    tier_objs = []
    for i in range(tiers):
        t = i / (tiers - 1)
        z0 = h * (0.18 + 0.72 * t)
        r = h * 0.24 * (1 - 0.82 * t) + 0.25
        th = h * 0.26 * (1 - 0.35 * t)
        prof = [(0, z0 + th), (r * 0.55, z0 + th * 0.45), (r, z0 + th * 0.05), (r * 0.8, z0 - th * 0.08),
                (0, z0 - th * 0.02)]
        cone = geo.lathe('Tier', prof, 14, fol, None)
        geo.subdivide(cone, 1)
        geo.displace_noise(cone, r * 0.18, 2.2 / r, seed=seed * 7 + i)
        for v in cone.data.vertices:
            a = math.atan2(v.co.y, v.co.x)
            v.co.z -= 0.18 * r * (0.5 + 0.5 * math.sin(a * 7 + seed + i)) * max(0, 1 - abs(v.co.z - z0) / th)
        cone.data.update()
        geo.store_rest(cone)
        tier_objs.append(cone)
    leaves = leaf_cards(tier_objs, fol, seed, size=h * 0.05, density=0.7, elong=1.8, out_bias=0.45)
    o = geo.join(parts + tier_objs + [leaves], f'Conifer {seed}')
    return bake_occlusion(o, [(0, 0, h * (0.2 + 0.7 * i / 6)) for i in range(7)], h * 0.22)


def cypress(seed=0, h=9.0, palette=('1e301c', '35512a', '6a7a34')):
    """Italian cypress: dense flame-shaped column with vertical tufting and a frond fringe."""
    fol = foliage(f'Cypress {palette[0]}', *palette, translucency=0.2, clump=3.0, leaf=20.0)
    r = h * 0.11
    prof = [(0, h)]
    for i in range(1, 12):
        t = i / 12
        prof.append((r * (math.sin(math.pi * (1 - t) ** 0.8) ** 0.7) * (1.0 if t > 0.1 else 0.8), h * (1 - t)))
    prof.append((0, h * 0.02))
    body = geo.lathe('Cypress body', prof, 16, fol, None)
    geo.subdivide(body, 2)
    off = Vector((seed * 3.1, seed * 1.7, 0.0))
    for v in body.data.vertices:
        q = Vector((v.co.x * 2.2 / r, v.co.y * 2.2 / r, v.co.z * 0.9)) + off
        d = noise.noise(q) * 0.3 + noise.noise(q * 2.7) * 0.15
        flame = 0.1 * math.sin(math.atan2(v.co.y, v.co.x) * 7 + v.co.z * 1.3 + seed)
        v.co += v.normal * r * (d + flame)
    body.data.update()
    geo.store_rest(body)
    leaves = leaf_cards([body], fol, seed, size=h * 0.045, density=0.8, elong=1.6, out_bias=0.5)
    trunk = geo.cylinder('Trunk', 0.12, h * 0.12, (0, 0, h * 0.04), bark(), None, 6, bevel=0)
    o = geo.join([trunk, body, leaves], f'Cypress {seed}')
    return bake_occlusion(o, [(0, 0, h * (0.1 + 0.8 * i / 8)) for i in range(9)], r * 1.1)


def broadleaf(seed=0, h=7.0, spread=1.0, palette=('2f4220', '56692a', '98983c'), autumn=False):
    rng = random.Random(seed)
    if autumn:
        palette = ('5a2e16', 'a0521e', 'd8963a')
    fol = foliage(f'Leaves {palette[0]}', *palette, translucency=0.3, clump=1.6)
    parts = []
    lean = Vector((rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25), 0))
    top = Vector((0, 0, h * 0.46)) + lean * h * 0.08
    trunk_pts = [Vector((0, 0, -0.2)), Vector((0, 0, h * 0.22)) + lean * h * 0.03, top]
    parts.append(geo.tube('Trunk', trunk_pts, [h * 0.06, h * 0.042, h * 0.03], bark(), None, sides=9))
    for k in range(4):
        a = k * math.tau / 4 + rng.uniform(-0.4, 0.4)
        d = Vector((math.cos(a), math.sin(a), 0.8)).normalized()
        base = top - Vector((0, 0, h * rng.uniform(0.04, 0.12)))
        parts.append(geo.tube('Branch', [base, base + d * h * 0.22, base + d * h * 0.3 + Vector((0, 0, h * 0.06))],
                              [h * 0.022, h * 0.012, h * 0.006], bark(), None, sides=6))
    cr = h * 0.36 * spread
    centre = top + Vector((0, 0, cr * 0.5))
    cl = _clumps(centre, (cr, cr * 0.92, cr * 0.72), 24, fol, seed, size=(0.28, 0.42), level=3)
    leaves = leaf_cards(cl, fol, seed, size=h * 0.05, density=0.55)
    o = geo.join(parts + cl + [leaves], f'Broadleaf {seed}')
    return bake_occlusion(o, [centre], cr * 1.05)


def bush(seed=0, r=1.0, palette=('2c3e1c', '4f6226', '86883a'), flowers=None):
    fol = foliage(f'Shrub {palette[0]}', *palette, translucency=0.28, clump=3.0)
    cl = _clumps((0, 0, r * 0.45), (r, r * 0.9, r * 0.55), 9, fol, seed, size=(0.35, 0.5), flat=0.8, level=3)
    leaves = leaf_cards(cl, fol, seed, size=r * 0.22, density=0.6)
    o = geo.join(cl + [leaves], f'Bush {seed}')
    return bake_occlusion(o, [(0, 0, r * 0.45)], r * 1.05)


def dead_tree(seed=0, h=6.0, mat=None):
    rng = random.Random(seed)
    mat = mat or bark('Dead bark', )
    parts = []

    def branch(p, d, length, r, depth):
        pts = [p]
        q = Vector(p)
        dd = Vector(d)
        for i in range(4):
            dd = (dd + Vector((rng.uniform(-.35, .35), rng.uniform(-.35, .35), rng.uniform(-.1, .2)))).normalized()
            q = q + dd * length / 4
            pts.append(q.copy())
        parts.append(geo.tube('Dead branch', pts, geo.taper(len(pts), r, r * 0.35), mat, None, sides=6))
        if depth > 0:
            for k in range(rng.randint(2, 3)):
                t = rng.uniform(0.45, 0.95)
                idx = min(len(pts) - 1, int(t * (len(pts) - 1)))
                a = rng.uniform(0, math.tau)
                nd = (dd + Vector((math.cos(a), math.sin(a), 0.3)) * 0.9).normalized()
                branch(pts[idx], nd, length * rng.uniform(0.45, 0.65), r * 0.5, depth - 1)
    branch(Vector((0, 0, -0.2)), Vector((0, 0, 1)), h * 0.55, h * 0.04, 3)
    return geo.join(parts, f'Dead tree {seed}')


def rock(seed=0, size=1.0, mat=None, flat=0.6, moss=0.3):
    """Angular field stone: convex hull of jittered points, chamfered and weathered."""
    mat = mat or field_stone(moss)
    rng = random.Random(seed)
    bm = bmesh.new()
    for i in range(18):
        th = rng.uniform(0, math.tau)
        ph = math.acos(rng.uniform(-1, 1))
        rr = rng.uniform(0.75, 1.0)
        bm.verts.new((rr * math.sin(ph) * math.cos(th) * size, rr * math.sin(ph) * math.sin(th) * size * 0.8,
                      rr * math.cos(ph) * size * flat))
    bmesh.ops.convex_hull(bm, input=bm.verts)
    me = bpy.data.meshes.new(f'Rock {seed}')
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(f'Rock {seed}', me)
    bpy.context.scene.collection.objects.link(o)
    me.materials.append(mat)
    mod = o.modifiers.new('Sub', 'SUBSURF')
    mod.levels = mod.render_levels = 2
    mod.subdivision_type = 'SIMPLE'
    geo.apply_modifiers(o)
    geo.displace_noise(o, size * 0.06, 2.5 / size, seed=seed)
    geo.displace_noise(o, size * 0.025, 9.0 / size, seed=seed + 4)
    for v in o.data.vertices:
        if v.co.z < -size * flat * 0.3:
            v.co.z = -size * flat * 0.3 + (v.co.z + size * flat * 0.3) * 0.2
    o.data.update()
    for p in o.data.polygons:
        p.use_smooth = False
    geo.store_rest(o)
    return o


def grass_tuft(seed=0, h=0.45, color=('6b7a34', 'a69a52')):
    """Clump of tapered blades (single mesh) for scattering at the camera's feet."""
    rng = random.Random(seed)
    mat = foliage(f'Grass blades {color[0]}', color[0], color[1], 'c2b46a', translucency=0.3, clump=8)
    bm = bmesh.new()
    for i in range(14):
        a = rng.uniform(0, math.tau)
        r = rng.uniform(0, 0.12)
        base = Vector((math.cos(a) * r, math.sin(a) * r, 0))
        lean = Vector((math.cos(a), math.sin(a), 0)) * rng.uniform(0.05, 0.25)
        hh = h * rng.uniform(0.6, 1.2)
        w = 0.025
        side = Vector((-math.sin(a), math.cos(a), 0)) * w
        v0 = bm.verts.new(base - side)
        v1 = bm.verts.new(base + side)
        mid = base + lean * 0.5 + Vector((0, 0, hh * 0.55))
        v2 = bm.verts.new(mid + side * 0.6)
        v3 = bm.verts.new(mid - side * 0.6)
        v4 = bm.verts.new(base + lean * 1.4 + Vector((0, 0, hh)))
        bm.faces.new((v0, v1, v2, v3))
        bm.faces.new((v3, v2, v4))
    me = bpy.data.meshes.new('Grass tuft')
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(f'Grass tuft {seed}', me)
    bpy.context.scene.collection.objects.link(o)
    me.materials.append(mat)
    geo.store_rest(o)
    return o


def scatter(protos, n, area_fn, height, rng, scale=(0.8, 1.2), coll=None, avoid=None, min_gap=0.0, tilt=0.0):
    """Instance random prototypes at points from area_fn(rng)->(x,y) on the height field."""
    placed = []
    tries = 0
    while len(placed) < n and tries < n * 30:
        tries += 1
        x, y = area_fn(rng)
        if avoid and avoid(x, y):
            continue
        if min_gap and any((x - px) ** 2 + (y - py) ** 2 < min_gap ** 2 for px, py in placed):
            continue
        placed.append((x, y))
        src = rng.choice(protos)
        inst(src, (x, y, height(x, y) - 0.05), rng.uniform(0, math.tau), rng.uniform(*scale), coll or C['Vegetation'],
             tilt=(rng.uniform(-tilt, tilt), rng.uniform(-tilt, tilt)))
    return placed


def _clumps(centre, radii, n, mat, seed, size=(0.22, 0.34), surface_bias=0.65, flat=0.8, level=2, amt=0.28):
    """Crown made of many displaced leaf clumps scattered inside an ellipsoid (biased to its surface)."""
    rng = random.Random(seed)
    parts = []
    rx, ry, rz = radii
    rmax = max(radii)
    for k in range(n):
        while True:
            v = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1)))
            if v.length <= 1.0:
                break
        v = v.normalized() * (v.length ** (1 - surface_bias))
        loc = Vector(centre) + Vector((v.x * rx, v.y * ry, v.z * rz))
        r = rmax * rng.uniform(*size) * (0.85 + 0.3 * max(0.0, v.z))
        parts.append(_blob('Clump', r, loc, mat, seed * 31 + k, amt=amt, sc=1.4, level=level, squash=flat))
    parts.append(_blob('Crown core', rmax * 0.7, centre, mat, seed * 31 + 99, amt=0.15, sc=1.2, level=2,
                       squash=rz / rmax))
    return parts


def olive(seed=0, h=5.0, palette=('46523a', '74805a', 'a6aa80')):
    """Gnarled olive: twisting split trunk and a wide, silvery crown."""
    rng = random.Random(seed)
    fol = foliage(f'Olive leaves {palette[0]}', *palette, translucency=0.22, clump=2.2, sheen=0.5)
    parts = []
    tops = []
    for k in range(3):
        a = k * math.tau / 3 + rng.uniform(-0.3, 0.3)
        out = Vector((math.cos(a), math.sin(a), 0))
        pts = [Vector((0, 0, -0.2)) + out * 0.08, Vector((0, 0, h * 0.18)) + out * h * 0.05,
               Vector((0, 0, h * 0.36)) + out * h * 0.14 + Vector((rng.uniform(-.2, .2), rng.uniform(-.2, .2), 0)),
               Vector((0, 0, h * 0.52)) + out * h * 0.24]
        parts.append(geo.tube('Olive limb', geo.bezier_points(*pts, steps=8), geo.taper(9, h * 0.05, h * 0.02),
                              bark('Olive bark', '5a4a3a'), None, sides=8))
        tops.append(pts[-1])
    cr = h * 0.42
    cl = []
    for k, t in enumerate(tops):
        cl += _clumps(t + Vector((0, 0, cr * 0.25)), (cr * 0.7, cr * 0.7, cr * 0.42), 9, fol, seed * 5 + k,
                      size=(0.3, 0.45), flat=0.7, level=3)
    leaves = leaf_cards(cl, fol, seed, size=h * 0.05, density=0.5, elong=1.4)
    o = geo.join(parts + cl + [leaves], f'Olive {seed}')
    return bake_occlusion(o, [t + Vector((0, 0, cr * 0.25)) for t in tops], cr * 0.75)


def bake_occlusion(obj, centres, radius, power=1.2):
    """Per-vertex crown occlusion: 0 on the outer shell, 1 at the centre (cheap stand-in for AO)."""
    me = obj.data
    if 'occ' in me.attributes:
        me.attributes.remove(me.attributes['occ'])
    attr = me.attributes.new('occ', 'FLOAT', 'POINT')
    cs = [Vector(c) for c in centres]
    vals = []
    for v in me.vertices:
        d = min((v.co - c).length for c in cs) / radius
        top = max(0.0, min(1.0, (v.co.z - min(c.z for c in cs)) / radius))
        vals.append(max(0.0, min(1.0, max(0.0, 1 - d) ** power * 1.3 + 0.35 * (1 - top) - 0.15)))
    attr.data.foreach_set('value', vals)
    return obj


def leaf_cards(src_objs, mat, seed=0, size=0.32, density=0.75, elong=1.0, out_bias=0.6, name='Leaves'):
    """Fringe of small diamond leaf cards on the surface of crown clumps (gives a leafy silhouette)."""
    rng = random.Random(seed)
    bm = bmesh.new()
    for o in src_objs:
        me = o.data
        for v in me.vertices:
            if rng.random() > density:
                continue
            p = v.co.copy()
            nrm = v.normal.copy()
            rnd = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1)))
            d = (nrm * out_bias + rnd * (1 - out_bias)).normalized()
            t = d.cross(Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))).normalized()
            b = d.cross(t).normalized()
            ang = rng.uniform(0, math.tau)
            t, b = t * math.cos(ang) + b * math.sin(ang), -t * math.sin(ang) + b * math.cos(ang)
            s = size * rng.uniform(0.7, 1.25)
            c = p + d * s * 0.25
            vs = [bm.verts.new(c - t * s * 0.5 * elong), bm.verts.new(c - b * s * 0.32 + d * s * 0.05),
                  bm.verts.new(c + t * s * 0.5 * elong), bm.verts.new(c + b * s * 0.32 + d * s * 0.05)]
            bm.faces.new(vs)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    me.materials.append(mat)
    for p in me.polygons:
        p.use_smooth = False
    geo.store_rest(o)
    return o


def field_stone(moss=0.3, color='7d776c'):
    def build():
        m, n = material(f'Field stone {moss}')
        p = n.rest()
        big = n.noise(p, 0.8, 4, 0.6)
        fine = n.noise(p, 7.0, 4, 0.65)
        col = n.mixc(n.maprange(big, 0.3, 0.7), n.hsv(srgb(color), 0.5, 0.9, 0.7), color)
        col = n.mixc(n.maprange(fine, 0.35, 0.7, 0, 0.35), col, n.hsv(srgb(color), 0.5, 0.8, 1.3))
        nz = n.xyz(n.geom('Normal'))[2]
        if moss:
            mm = n.mul(n.mul(n.smooth(nz, 0.3, 0.85), n.smooth(n.add(big, n.mul(fine, 0.3)), 0.45, 0.6)), moss)
            col = n.mixc(mm, col, '4e5a2a')
        ao = n.ao(0.4, local=True, samples=6)
        col = n.mixc(n.maprange(ao, 0.3, 1.0, 0.6, 0.0), col, n.hsv(col, 0.5, 1.0, 0.4))
        n.principled(**{'Base Color': col, 'Roughness': 0.9, 'Normal': n.bump(n.add(big, n.mul(fine, 0.5)), 0.5, 0.05)})
        return m
    return mat_cached(('fieldstone', moss, color), build)


def aerial_mat(name, fog='c9b49a', fog_dist=420.0, rock=('5a5650', '7a7266'), green='5f6a3c', snow=0.0, flat=False):
    """Distant-scenery shader: rock/grass colour fading into a self-lit haze colour with camera distance."""
    m, n = material(name + ' aerial')
    p = n.rest()
    big = n.noise(n.vmath('MULTIPLY', p, (0.02, 0.02, 0.05)), 1.0, 5, 0.6)
    nz = n.xyz(n.geom('Normal'))[2]
    col = n.mixc(n.smooth(big, 0.35, 0.65), rock[0], rock[1])
    col = n.mixc(n.smooth(nz, 0.55, 0.85), col, green)
    if snow:
        _, _, z = n.xyz(p)
        col = n.mixc(n.mul(n.smooth(z, 40 * (1 - snow), 60 * (1 - snow)), n.smooth(nz, 0.3, 0.6)), col, 'e6e9ee')
    bsdf = n.principled(connect=False, **{'Base Color': col, 'Roughness': 0.95,
                                          'Normal': n.bump(big, 0.6, 1.0)})
    lp = n.node('ShaderNodeLightPath')
    dist = lp.outputs['Ray Length']
    f = n.math('SUBTRACT', 1.0, n.math('EXPONENT', n.math('DIVIDE', n.mul(dist, -1.0), fog_dist)))
    f = n.math('MINIMUM', f, 0.92)
    em = n.emission(fog, 1.0)
    n.surface(n.mix_shader(f, bsdf, em))
    return m


def far_ground(radius=900, z=-1.5, fog='c9b49a', fog_dist=420.0, inner=110, color=('55602f', '7a7a3c'), name='Far ground'):
    """Huge flat annulus beyond the terrain so the horizon is never empty."""
    mat = mat_cached(('farground', fog, fog_dist), lambda: aerial_mat(name, fog, fog_dist, (color[0], color[1]),
                                                                      color[1]))
    bm = bmesh.new()
    seg = 96
    ring0, ring1 = [], []
    for i in range(seg):
        a = i * math.tau / seg
        ring0.append(bm.verts.new((math.cos(a) * inner, math.sin(a) * inner, z)))
        ring1.append(bm.verts.new((math.cos(a) * radius, math.sin(a) * radius, z)))
    for i in range(seg):
        j = (i + 1) % seg
        bm.faces.new((ring0[i], ring0[j], ring1[j], ring1[i]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    C['Terrain'].objects.link(o)
    me.materials.append(mat)
    o.visible_shadow = False
    geo.store_rest(o)
    return o


def smooth_stone(color='a8a090', name=None, veins=0.0, rough=0.7, scale=1.0, moss=0.0):
    """Dressed/polished stone without a block pattern (columns, statues, plinths); optional marble veins."""
    def build():
        m, n = material(name or f'Smooth stone {color}')
        p = n.vmath('SCALE', n.rest(), scale=scale)
        big = n.noise(p, 0.8, 4, 0.6)
        fine = n.noise(p, 9.0, 4, 0.65)
        base = srgb(color)
        col = n.mixc(n.maprange(big, 0.3, 0.7, 0.0, 0.35), base, n.hsv(base, 0.5, 0.9, 0.78))
        col = n.mixc(n.maprange(fine, 0.35, 0.7, 0.0, 0.25), col, n.hsv(base, 0.5, 0.9, 1.15))
        if veins:
            v = n.wave(p, 2.0, 12.0, 4.0, 'X', 'BANDS', 'SIN')
            vm = n.mul(n.smooth(v, 0.93, 1.0), veins)
            col = n.mixc(vm, col, n.hsv(base, 0.5, 0.8, 0.6))
        if moss:
            nz = n.xyz(n.geom('Normal'))[2]
            col = n.mixc(n.mul(n.mul(n.smooth(nz, 0.4, 0.9), n.smooth(big, 0.5, 0.65)), moss), col, '4e5a2a')
        ao = n.ao(0.3, local=True, samples=6)
        col = n.mixc(n.maprange(ao, 0.3, 1.0, 0.55, 0.0), col, n.hsv(col, 0.5, 1.0, 0.45))
        n.principled(**{'Base Color': col, 'Roughness': rough, 'Normal': n.bump(n.add(big, n.mul(fine, 0.4)), 0.25, 0.03)})
        return m
    return mat_cached(('smoothstone', color, name, veins, moss), build)


def rug_mat(field='1c4a46', border='b0803a', line='d9ccb0', name=None, w=3.0, d=2.0, band=0.28):
    """Woven rug: plain field, patterned border band and inner pinstripe, from local coordinates."""
    def build():
        m, n = material(name or f'Rug {field}')
        lc = n.attr('lc')
        x, y, _ = n.xyz(lc)
        ex = n.math('SUBTRACT', w / 2, n.math('ABSOLUTE', x))
        ey = n.math('SUBTRACT', d / 2, n.math('ABSOLUTE', y))
        e = n.math('MINIMUM', ex, ey)
        bmask = n.smooth(e, band + 0.02, band - 0.02)
        lmask = n.mul(n.smooth(e, band + 0.1, band + 0.07), n.smooth(e, band + 0.03, band + 0.06))
        motif = n.smooth(n.math('ABSOLUTE', n.math('SINE', n.mul(n.add(x, y), 9.0))), 0.6, 0.9)
        col = n.mixc(bmask, field, n.mixc(n.mul(motif, 0.6), border, field))
        col = n.mixc(lmask, col, line)
        fringe = n.smooth(e, 0.03, 0.0)
        col = n.mixc(fringe, col, line)
        weave = n.noise(n.rest(), 40.0, 2, 0.5)
        wear = n.noise(n.rest(), 1.2, 3, 0.55)
        col = n.mixc(n.maprange(wear, 0.45, 0.7, 0.0, 0.3), col, n.hsv(col, 0.5, 0.7, 1.15))
        n.principled(**{'Base Color': col, 'Roughness': 0.92, 'Sheen Weight': 0.6, 'Normal': n.bump(weave, 0.15, 0.01)})
        return m
    return mat_cached(('rug', field, border, w, d), build)


def great_tree(seed=0, h=24.0, palette=('22402e', '3f6a3a', '8aa04a'), spread=1.0):
    """Ancient world-tree: flared buttress roots, twisting trunk, heavy limbs and a vast crown."""
    rng = random.Random(seed)
    bk = bark('Ancient bark', '4a3c30')
    fol = foliage(f'Great tree leaves {palette[0]}', *palette, translucency=0.32, clump=0.8, leaf=8.0)
    parts = []
    # trunk: twisting tube
    pts = []
    for i in range(9):
        t = i / 8
        pts.append(Vector((math.sin(t * 2.2 + seed) * h * 0.03, math.cos(t * 1.7 + seed) * h * 0.025, -0.5 + t * h * 0.55)))
    radii = [h * 0.11 * (1 - 0.45 * (i / 8)) for i in range(9)]
    trunk = geo.tube('Great trunk', pts, radii, bk, None, sides=18)
    geo.subdivide(trunk, 1)
    geo.displace_noise(trunk, h * 0.012, 0.6, seed=seed, normal=True)
    parts.append(trunk)
    # buttress roots
    for k in range(9):
        a = k * math.tau / 9 + rng.uniform(-0.2, 0.2)
        d = Vector((math.cos(a), math.sin(a), 0))
        L = h * rng.uniform(0.28, 0.42)
        rp = [Vector((0, 0, h * 0.12)) + d * h * 0.05, d * h * 0.14 + Vector((0, 0, h * 0.03)),
              d * L * 0.7 + Vector((0, 0, -0.1)), d * L + Vector((0, 0, -0.6))]
        parts.append(geo.tube('Root', geo.bezier_points(*rp, steps=10), geo.taper(11, h * 0.05, h * 0.008, 0.8), bk,
                              None, sides=8, flatten=0.6))
    # limbs
    top = pts[-1]
    crowns = []
    for k in range(6):
        a = k * math.tau / 6 + rng.uniform(-0.3, 0.3)
        d = Vector((math.cos(a), math.sin(a), 0.55)).normalized()
        L = h * rng.uniform(0.32, 0.42) * spread
        base = top - Vector((0, 0, h * rng.uniform(0.02, 0.12)))
        tip = base + d * L
        mid = base + d * L * 0.5 + Vector((0, 0, h * 0.05))
        parts.append(geo.tube('Limb', geo.bezier_points(base, mid, tip, steps=8), geo.taper(9, h * 0.035, h * 0.008), bk,
                              None, sides=8))
        crowns.append(tip + Vector((0, 0, h * 0.05)))
    cr = h * 0.42 * spread
    centre = top + Vector((0, 0, h * 0.14))
    cl = _clumps(centre, (cr, cr * 0.95, cr * 0.5), 46, fol, seed, size=(0.16, 0.26), level=3, surface_bias=0.75)
    for k, c in enumerate(crowns):
        cl += _clumps(c, (cr * 0.42, cr * 0.42, cr * 0.26), 8, fol, seed * 11 + k, size=(0.3, 0.45), level=3)
    leaves = leaf_cards(cl, fol, seed, size=h * 0.03, density=0.5)
    o = geo.join(parts + cl + [leaves], f'Great tree {seed}')
    return bake_occlusion(o, [centre] + crowns, cr * 0.6)


def flame_mat(name='Flame', strength=3.0, edge='e02800', mid='ff6a10', core='ffb040', soft=0.8):
    """Translucent fire: hot facing core, deep-orange soft edges that fade to transparent (no opaque white blobs)."""
    def build():
        m, n = material(name)
        facing = n.layer_weight(0.45, 'Facing')
        c = n.math('SUBTRACT', 1.0, facing)
        col = n.ramp(c, [(0.0, edge), (0.55, mid), (1.0, core)])
        st = n.mul(n.math('POWER', c, 2.2), strength)
        em = n.emission(col, st)
        tr = n.transparent()
        n.surface(n.mix_shader(n.math('MINIMUM', n.mul(c, soft * 1.4), soft), tr, em))
        return m
    return mat_cached(('flame', name, strength, edge, mid, core), build)


def render_safe(scene, path):
    """Render; if the shared GPU runs out of memory, retry the same frame on the CPU."""
    try:
        core.render_to(scene, path)
    except RuntimeError as err:
        msg = str(err)
        if scene.cycles.device != 'GPU' or not any(k in msg for k in ('Memory', 'Command buffer', 'memory')):
            raise
        print('RENDER_GPU_OOM falling back to CPU', flush=True)
        scene.cycles.device = 'CPU'
        core.render_to(scene, path)
