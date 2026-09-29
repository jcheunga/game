"""Upgrade saved sources without rebuilding models; preview first, publish explicitly.

Blender --background --python art/blender/polish_assets.py -- --ids player_brawler
Add --publish to save finished .blend scenes and publish all animation frames.
Each asset stages its renders before replacing its shipped images.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import struct
import sys
import time
import bpy

SOURCE = Path(__file__).resolve().parent
ROOT = SOURCE.parents[1]
sys.path.insert(0, str(SOURCE))
from crownroad_art import camera, setup_render
from surface_finish import REVISION, finish_scene_materials, light_finish

parser = argparse.ArgumentParser()
parser.add_argument('--category', choices=['all','units','caravan','caravans','gatehouse','mounts','battlefields','maps','menus','items'], default='all')
parser.add_argument('--ids', default='all')
parser.add_argument('--samples', type=int, default=64)
parser.add_argument('--publish', action='store_true')
parser.add_argument('--resume', action='store_true')
args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
REVIEW = ROOT/'artifacts/blender/polish-v2'
REVIEW.mkdir(parents=True, exist_ok=True)
fingerprint = hashlib.sha256((SOURCE/'surface_finish.py').read_bytes() + Path(__file__).read_bytes()).hexdigest()
unit_specs = {u['Id']:u for u in json.loads((ROOT/'data/units.json').read_text())['Units']}
terrains = {s['TerrainId']:s['MapId'] for s in json.loads((ROOT/'data/stages.json').read_text())['Stages']}
menu_routes = {'loadout':'city','shop':'harbor','cash_shop':'citadel','endless':'gloamwood','multiplayer':'steppe',
    'lan_race':'city','arena':'citadel','battle_summary':'city','bounty':'thornwall','codex':'basilica',
    'event':'steppe','expedition':'harbor','forge':'foundry','friends':'city','guild':'citadel',
    'leaderboard':'basilica','login_calendar':'city','profile':'city','raid':'quarantine',
    'season_pass':'steppe','skill_tree':'gloamwood','settings':'basilica','tower':'thornwall'}


def render(scene, path):
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    if not path.exists() or path.stat().st_size < 100: raise RuntimeError('Missing render: ' + str(path))


def geometry_signature(scene):
    """Guard the saved geometry, rigid-joint hierarchy and frame-one pose."""
    digest = hashlib.sha256()
    for obj in sorted(scene.objects, key=lambda item:item.name):
        if obj.type not in ('MESH','CURVE','EMPTY','ARMATURE'): continue
        digest.update((obj.name + (obj.parent.name if obj.parent else '')).encode())
        for row in obj.matrix_local:
            digest.update(struct.pack('4f', *row))
        if obj.type == 'MESH':
            for vertex in obj.data.vertices: digest.update(struct.pack('3f', *vertex.co))
            for poly in obj.data.polygons:
                digest.update(struct.pack(str(len(poly.vertices))+'I', *poly.vertices))
        elif obj.type == 'CURVE':
            for spline in obj.data.splines:
                for point in spline.points: digest.update(struct.pack('4f', *point.co))
    return digest.hexdigest()


def process(source, category):
    start = time.monotonic()
    ident = source.stem
    folder = REVIEW/category/ident
    folder.mkdir(parents=True, exist_ok=True)
    marker = folder/'published.json'
    if args.publish and args.resume and marker.exists():
        record = json.loads(marker.read_text())
        if (record.get('fingerprint') == fingerprint and record.get('samples') == args.samples
                and record.get('source_hash') == hashlib.sha256(source.read_bytes()).hexdigest()
                and all((ROOT/path).exists() for path in record.get('outputs', []))):
            print('POLISH_SKIP '+category+'/'+ident, flush=True)
            return
    bpy.ops.wm.open_mainfile(filepath=str(source))
    scene = bpy.context.scene
    scene.frame_set(1)
    original_geometry = geometry_signature(scene)
    original_source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
    original_camera = scene.camera
    original_size = (scene.render.resolution_x, scene.render.resolution_y)
    original_path = scene.render.filepath
    setup_render(scene, args.samples)
    scene.cycles.use_adaptive_sampling = True
    scene.cycles.adaptive_threshold = .015
    # Opaque worlds must stay opaque; setup_render's default is for sprites.
    scene.render.film_transparent = category not in ('battlefields','maps','menus')
    material_count = finish_scene_materials(scene)
    route = terrains.get(ident, '') if category == 'battlefields' else ident if category == 'maps' else menu_routes.get(ident, '')
    light_finish(scene, category, ident in ('night','witchcircle','marsh','blacksite','endless','raid'), route)
    staged = []
    if category == 'units':
        frame_folder = ROOT/'artifacts/blender/units'/ident
        metadata = json.loads((frame_folder/'metadata.json').read_text())
        frames = sum(clip['count'] for clip in metadata['animations'].values())
        if frames != scene.frame_end: raise RuntimeError('Source/metadata animation contract mismatch: '+ident)
        scene.render.resolution_x, scene.render.resolution_y = 256, 320
        for frame in range(frames if args.publish else 1):
            scene.frame_set(frame+1)
            path = folder/f'{frame:03}.png'
            render(scene, path)
            staged.append((path, frame_folder/path.name))
        scene.frame_set(1)
        spec = unit_specs[ident]
        cls = spec['VisualClass']
        mounted = ident in ('player_raider','enemy_boss_steppe')
        machine = cls in ('siegetower','splitter') or 'ballista' in ident or 'engine' in ident
        beast = cls == 'hound'
        target = 1.5 if mounted or cls == 'siegetower' else 1.45 if not machine and not beast else .7
        scale = 4.2 if mounted else 3.7 if cls == 'siegetower' else 2.0 if not machine and not beast else 2.8
        portrait = camera('Finish review portrait', (5,-8,4), (0,0,target), scale, original_camera.users_collection[0])
        scene.camera = portrait
        scene.render.resolution_x = scene.render.resolution_y = 512
        render(scene, folder/'portrait.png')
        staged.append((folder/'portrait.png', frame_folder/'portrait.png'))
        scene.camera = original_camera
        bpy.data.objects.remove(portrait, do_unlink=True)
    else:
        render(scene, folder/'render.png')
        # Saved scenes retain their original output target. Validate its scope.
        output = Path(bpy.path.abspath(original_path)).resolve()
        if not output.is_relative_to(ROOT/'assets'): raise RuntimeError('Unscoped output: '+str(output))
        staged.append((folder/'render.png', output))
    scene.camera = original_camera
    scene.render.resolution_x, scene.render.resolution_y = original_size
    scene.render.filepath = original_path
    scene.frame_set(1)
    if geometry_signature(scene) != original_geometry:
        raise RuntimeError('Geometry or frame-one pose changed: '+ident)
    scene['finish_notes'] = 'Road-worn royal miniatures: restrained local grain, distinct roughness, warm key/cool rim. Geometry and animation unchanged.'
    report = dict(id=ident, category=category, revision=REVISION, materials=material_count,
                  samples=args.samples, fingerprint=fingerprint, seconds=round(time.monotonic()-start,2),
                  outputs=[str(dst.relative_to(ROOT)) for _,dst in staged], source=str(source.relative_to(ROOT)),
                  geometry_preserved=True, geometry_signature=original_geometry)
    if args.publish:
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(source), compress=True)
        for src, dst in staged:
            dst.parent.mkdir(parents=True, exist_ok=True)
            temporary = dst.with_name(dst.stem+'.polish-tmp.png')
            shutil.copy2(src, temporary)
            temporary.replace(dst)
        report['source_hash'] = hashlib.sha256(source.read_bytes()).hexdigest()
        report['input_source_hash'] = original_source_hash
        marker.write_text(json.dumps(report, indent=2)+'\n')
    else:
        (folder/'preview.json').write_text(json.dumps(report, indent=2)+'\n')
    print('POLISH_COMPLETE '+json.dumps(report), flush=True)


jobs = [('caravan', SOURCE/'lantern_caravan.blend')]
for category in ('units','caravans','gatehouse','mounts','battlefields','maps','menus','items'):
    jobs += [(category,path) for path in sorted((SOURCE/category).glob('*.blend'))]
selected = [(category,path) for category,path in jobs
            if (args.category == 'all' or args.category == category)
            and (args.ids == 'all' or path.stem in args.ids.split(','))]
if not selected: raise RuntimeError('No matching source scenes')
for category, source in selected: process(source, category)
print('POLISH_BATCH_COMPLETE '+str(len(selected)), flush=True)
