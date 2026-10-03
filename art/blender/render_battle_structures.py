"""Rerender editable bases for the battle ground plane, with projected attachment data.

Original models and published sprites stay intact. Run with Blender --background
--python art/blender/render_battle_structures.py -- --samples 48.
"""
import argparse
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector
from bpy_extras.object_utils import world_to_camera_view

SOURCE = Path(__file__).resolve().parent
ROOT = SOURCE.parents[1]
sys.path.insert(0, str(SOURCE))
from crownroad_art import area_light, camera, collection, setup_render

parser = argparse.ArgumentParser()
parser.add_argument('--samples', type=int, default=48)
parser.add_argument('--ids', default='all')
parser.add_argument('--metadata-only', action='store_true')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
OUTPUT = ROOT / 'assets/structures/battle-v2'
OUTPUT.mkdir(parents=True, exist_ok=True)
jobs = [('war_wagon', SOURCE / 'lantern_caravan.blend'),
        ('gatehouse', SOURCE / 'gatehouse/gatehouse.blend')]
jobs += [(path.stem, path) for path in sorted((SOURCE / 'caravans').glob('*.blend'))]

for ident, source in jobs:
    if args.ids != 'all' and ident not in args.ids.split(','):
        continue
    bpy.ops.wm.open_mainfile(filepath=str(source))
    scene = bpy.context.scene
    # Reuse the mesh/material source, replacing only the presentation rig.
    for obj in list(scene.objects):
        if obj.type in ('LIGHT', 'CAMERA'):
            bpy.data.objects.remove(obj, do_unlink=True)
    rotation = Matrix.Rotation(math.radians(-62), 4, 'Z') if ident == 'gatehouse' else Matrix.Identity(4)
    if ident == 'gatehouse':
        for obj in scene.objects:
            if obj.type in ('MESH', 'CURVE') and not obj.hide_render and obj.parent is None:
                obj.matrix_world = rotation @ obj.matrix_world
    rig = collection('Battle plane presentation v2')
    setup_render(scene, args.samples)
    scene.render.resolution_x = scene.render.resolution_y = 1024
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .65
    scene.camera = camera('Battle plane • orthographic 22 degrees', (0, -32, 15.2), (0, 0, 2.25),
                          6.25 if ident == 'gatehouse' else 8.25, rig)
    # At this camera angle the sun projects ground shadows down/right at roughly
    # (.77, .29) per pixel of height, matching BattleLighting's daylight profile.
    area_light('Sun • upper left', (-10, 10, 14), (0, 0, 2), 3200, (1, .87, .70), 5, rig)
    area_light('Sky • soft fill', (6, -6, 8), (0, 0, 2), 1100, (.78, .85, 1), 7, rig)
    area_light('Ground • gentle warm bounce', (-1, -8, 3), (0, 0, 2), 250, (1, .93, .82), 6, rig)
    bpy.context.view_layer.update()

    def project(point):
        p = world_to_camera_view(scene, scene.camera, Vector(point))
        return [round(p.x, 6), round(1 - p.y, 6)]

    anchor = project((0, -1.05 if ident != 'gatehouse' else -1.75, 0))
    # Crew feet rest on the walkway, below the crenellation tips.
    sockets = [project(rotation @ Vector((0, .1, 3.4)))] if ident == 'gatehouse' else [
        project((x, -.2, 3.3)) for x in (-1.25, 0, 1.25)]
    contacts = [project(rotation @ Vector((x, -.05, 0))) for x in (-1.5, 0, 1.5)] if ident == 'gatehouse' else [
        project((x, y, 0)) for y in (.95, -1.05) for x in (-1.64, 1.42)]
    lights = [project(rotation @ Vector((x, -.94, 0))) for x in (-1.55, 1.55)] if ident == 'gatehouse' else [
        project((x, -1.08, 0)) for x in (-2.99, 2.73)]
    points = [world_to_camera_view(scene, scene.camera, obj.matrix_world @ Vector(corner))
              for obj in scene.objects if obj.type in ('MESH', 'CURVE') and not obj.hide_render
              for corner in obj.bound_box]
    if any(min(p[i] for p in points) < .015 or max(p[i] for p in points) > .985 for i in (0, 1)):
        raise RuntimeError(f'{ident}: silhouette outside safe frame')
    metadata = {'source': str(source.relative_to(ROOT)), 'generator': 'art/blender/render_battle_structures.py',
                'cameraElevation': 22, 'anchor': anchor, 'mounts': sockets, 'contacts': contacts,
                'width': 210 if ident != 'gatehouse' else 216, 'lights': lights,
                'smoke': project(rotation @ Vector((-.25, -.1, 2.9)))}
    (OUTPUT / (ident + '.json')).write_text(json.dumps(metadata, indent=2) + '\n')
    scene.render.filepath = str(OUTPUT / (ident + '.png'))
    if not args.metadata_only:
        bpy.ops.render.render(write_still=True)
    print('BATTLE_STRUCTURE_COMPLETE ' + ident, flush=True)
