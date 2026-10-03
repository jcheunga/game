"""Shared scene, camera and light setup for battle structures.

Each structure keeps the camera, canvas and ground placement of the asset it
replaces (see build_structures.py CONTRACT), so it drops into the fixed runtime
rects (wagon 180x140, gatehouse 180x160, mounts 60x75) without re-tuning.
"""
import math
import time

import bpy
from mathutils import Vector

from rk import core


# id -> camera location, target, ortho scale, canvas
CAMERAS = {
    'wagon': dict(loc=(7.5, -32.0, 12.0), target=(0.12, 0.0, 2.65), scale=8.7, res=(1440, 1120)),
    'gatehouse': dict(loc=(7.0, -30.0, 12.0), target=(0.0, 0.0, 2.1), scale=6.4, res=(1440, 1280)),
    'mount': dict(loc=(5.5, -20.0, 6.7), target=(0.18, 0.0, 1.28), scale=4.1, res=(384, 480)),
    # battle-v2 presentation (assets/structures/battle-v2/README.md): one orthographic camera at 22 degrees
    'battle_wagon': dict(loc=(0.0, -32.0, 15.2), target=(0.0, 0.0, 2.25), scale=8.25, res=(1024, 1024)),
    'battle_gatehouse': dict(loc=(0.0, -32.0, 15.2), target=(0.0, 0.0, 2.25), scale=6.25, res=(1024, 1024)),
}


def scene_setup(kind, samples, exposure=0.0):
    scene = bpy.context.scene
    core.setup_render(scene, samples, exposure=exposure)
    cfg = CAMERAS[kind]
    scene.render.resolution_x, scene.render.resolution_y = cfg['res']
    core.studio_world(scene, top='3d4a5a', bottom='2a2119', strength=0.5)
    rig = core.collection('Cameras and lights')
    cam = core.ortho_camera('CAMERA battle • ' + kind, cfg['loc'], cfg['target'], cfg['scale'], rig)
    scene.camera = cam
    return scene, cam, rig


STRUCTURE_FILL = dict(fill_power=1000, sky_power=700)   # big camera-facing facades need more fill than rounded units


def light_rig(coll, center, scale=1.0, warm='ffecd6', rim='aecbff', key_power=1150, rim_power=1100, fill_power=320,
              sky_power=520):
    """Battle sun, identical in direction/colour to rk.character.light_rig (the unit rig): key from
    the upper LEFT and slightly behind (the game projects every silhouette's ground shadow down-right),
    a soft camera-side fill, a cool back-right rim and a broad sky. Neutral-warm key: the game adds a
    per-zone tint. Positions scale with the structure; powers scale with distance squared."""
    c = Vector(center)
    s = scale
    core.area_light('Key • sun upper left', c + Vector((-6.5, 1.0, 7.5)) * s, c, key_power * s * s, core.srgb(warm),
                    3.5 * s, coll)
    core.area_light('Fill • camera side', c + Vector((5.5, -6.5, 2.5)) * s, c, fill_power * s * s, core.srgb('fff1e2'),
                    6.0 * s, coll)
    core.area_light('Rim • cool back right', c + Vector((4.5, 5.5, 4.5)) * s, c, rim_power * s * s, core.srgb(rim), 3.0 * s, coll)
    core.area_light('Sky • soft top', c + Vector((0.5, -1.0, 8.0)) * s, c, sky_power * s * s, core.srgb('dfe9ff'), 8.0 * s, coll)


def no_shadow(objs):
    for o in objs:
        if o is None:
            continue
        o.visible_shadow = False
    return objs


def ground_catcher(coll, center=(0, 0, 0), size=(8.0, 5.0), light_height=9.0, light_size=6.0, light_offset=(0.6, -0.8),
                   fade=(0.5, 0.97)):
    """Shadow catcher: bakes a soft contact shadow into the sprite alpha so the structure
    sits on the painted battlefield instead of floating. A dedicated soft overhead light
    reaches only the catcher; every other light is excluded from it (exclude_local_lights)."""
    import bmesh
    from mathutils import Matrix
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=0.5,
                          matrix=Matrix.Translation(center) @ Matrix.Diagonal((size[0], size[1], 1, 1)))
    me = bpy.data.meshes.new('Shadow catcher')
    bm.to_mesh(me)
    bm.free()
    me.materials.append(_catcher_material(fade))
    obj = bpy.data.objects.new('Shadow catcher', me)
    coll.objects.link(obj)
    obj.is_shadow_catcher = True
    obj.visible_glossy = False
    # two link sets: one INCLUDE list for the shadow light, one EXCLUDE list for everything else
    inc = bpy.data.collections.new('Link • catcher only')
    inc.objects.link(obj)
    exc = bpy.data.collections.new('Link • all but catcher')
    exc.objects.link(obj)
    for item in exc.collection_objects:
        item.light_linking.link_state = 'EXCLUDE'
    obj['link_exclude'] = exc.name
    c = Vector(center)
    sl = core.area_light('Shadow light • catcher only', c + Vector((light_offset[0], light_offset[1], light_height)), c,
                         1000.0, (1, 1, 1, 1), light_size, coll, shape='DISK')
    sl.light_linking.receiver_collection = inc
    return obj


def _catcher_material(fade=(0.5, 0.97)):
    """Catcher surface that turns transparent toward its border so the contact shadow
    dissolves instead of ending on the plane's hard edge."""
    m = bpy.data.materials.new('Shadow catcher fade')
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    tc = nt.nodes.new('ShaderNodeTexCoord')
    sub = nt.nodes.new('ShaderNodeVectorMath')
    sub.operation = 'SUBTRACT'
    sub.inputs[1].default_value = (0.5, 0.5, 0.0)
    nt.links.new(tc.outputs['Generated'], sub.inputs[0])
    mul = nt.nodes.new('ShaderNodeVectorMath')
    mul.operation = 'MULTIPLY'
    mul.inputs[1].default_value = (2.0, 2.0, 0.0)
    nt.links.new(sub.outputs[0], mul.inputs[0])
    ln = nt.nodes.new('ShaderNodeVectorMath')
    ln.operation = 'LENGTH'
    nt.links.new(mul.outputs[0], ln.inputs[0])
    mr = nt.nodes.new('ShaderNodeMapRange')
    mr.inputs['From Min'].default_value = fade[0]
    mr.inputs['From Max'].default_value = fade[1]
    mr.inputs['To Min'].default_value = 0.22       # never fully opaque: a soft contact shadow, not a black pool
    nt.links.new(ln.outputs['Value'], mr.inputs['Value'])
    diff = nt.nodes.new('ShaderNodeBsdfDiffuse')
    tr = nt.nodes.new('ShaderNodeBsdfTransparent')
    mix = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(mr.outputs['Result'], mix.inputs[0])
    nt.links.new(diff.outputs[0], mix.inputs[1])
    nt.links.new(tr.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs['Surface'])
    return m


def exclude_local_lights(catcher, keep=('Shadow light',)):
    """Only the dedicated shadow light reaches the catcher: rim/kicker/fill lights sit low or
    behind and would throw the structure's shadow toward the camera, and lantern lights sit inside
    cages (their occlusion would read as a shadow over the whole plane)."""
    if catcher is None:
        return
    exc = bpy.data.collections[catcher['link_exclude']]
    for o in bpy.data.objects:
        if o.type == 'LIGHT' and not o.name.startswith(keep):
            o.light_linking.receiver_collection = exc


def render_robust(scene, out, cam, attempts=3):
    """The Metal GPU is shared with other jobs: retry on out-of-memory, then fall back to CPU."""
    for k in range(attempts):
        try:
            core.render_to(scene, out, cam)
            return
        except RuntimeError as exc:
            print(f'RENDER_RETRY {k + 1}: {str(exc)[:120]}', flush=True)
            time.sleep(20 * (k + 1))
    scene.cycles.device = 'CPU'
    core.render_to(scene, out, cam)


def clean_alpha(path, floor=0.06):
    """Remap alpha so the faint far fringe of the contact-shadow plane and stray denoiser
    speckles become exactly transparent (a' = (a - floor) / (1 - floor))."""
    import numpy as np
    img = bpy.data.images.load(str(path))
    px = np.empty(len(img.pixels), dtype=np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(-1, 4)
    a = np.clip((px[:, 3] - floor) / (1 - floor), 0, 1)
    px[:, 3] = a
    px[a <= 0, :3] = 0
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = str(path)
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)
