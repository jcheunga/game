"""Read every finished native scene and audit its self-contained material setup."""
from collections import Counter
import argparse
import hashlib
import json
from pathlib import Path
import struct
import sys
import bpy

SOURCE=Path(__file__).resolve().parent
ROOT=SOURCE.parents[1]
sys.path.insert(0,str(SOURCE))
from surface_finish import REVISION

parser=argparse.ArgumentParser()
parser.add_argument('--ids',default='all')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])

REVIEW=ROOT/'artifacts/blender/polish-v2'
failures=[]
families=Counter()
scenes=0
materials=0


def check(condition,label):
    if not condition: failures.append(label)


def geometry_signature(scene):
    digest=hashlib.sha256()
    for obj in sorted(scene.objects,key=lambda item:item.name):
        if obj.type not in ('MESH','CURVE','EMPTY','ARMATURE'): continue
        digest.update((obj.name+(obj.parent.name if obj.parent else '')).encode())
        for row in obj.matrix_local: digest.update(struct.pack('4f',*row))
        if obj.type=='MESH':
            for vertex in obj.data.vertices: digest.update(struct.pack('3f',*vertex.co))
            for poly in obj.data.polygons: digest.update(struct.pack(str(len(poly.vertices))+'I',*poly.vertices))
        elif obj.type=='CURVE':
            for spline in obj.data.splines:
                for point in spline.points: digest.update(struct.pack('4f',*point.co))
    return digest.hexdigest()


expected=[('caravan',SOURCE/'lantern_caravan.blend')]
for category in ('units','caravans','gatehouse','mounts','battlefields','maps','menus','items'):
    expected += [(category,path) for path in sorted((SOURCE/category).glob('*.blend'))]
for category,source in expected:
    if args.ids!='all' and source.stem not in args.ids.split(','): continue
    record_path=REVIEW/category/source.stem/'published.json'
    if not record_path.exists():
        check(False,'Missing publish record: '+str(source))
        continue
    record=json.loads(record_path.read_text())
    motion_record=ROOT/'artifacts/blender/combat-motion'/source.stem/'published.json'
    known_hashes={record['source_hash']}
    if category=='units' and motion_record.exists(): known_hashes.add(json.loads(motion_record.read_text())['source_hash'])
    check(hashlib.sha256(source.read_bytes()).hexdigest() in known_hashes,source.stem+': current source hash')
    bpy.ops.wm.open_mainfile(filepath=str(source))
    scene=bpy.context.scene
    scene.frame_set(1)
    scenes+=1
    check(scene.get('crownroad_finish')==REVISION,source.stem+': finish revision')
    check(geometry_signature(scene)==record['geometry_signature'],source.stem+': saved geometry and pose')
    check(scene.camera is not None and scene.camera.data.type=='ORTHO',source.stem+': fixed orthographic camera')
    check(scene.render.film_transparent==(category not in ('battlefields','maps','menus')),source.stem+': transparency')
    check(scene.cycles.samples>=64 and scene.cycles.use_denoising,source.stem+': render quality')
    check(scene.view_settings.view_transform=='AgX',source.stem+': highlight handling')
    if category=='units':
        clips=json.loads(scene['animations'])
        published=json.loads((ROOT/'artifacts/blender/units'/source.stem/'metadata.json').read_text())
        check(clips==published['animations'],source.stem+': native and published clips match')
        check(scene.frame_start==1 and scene.frame_end==sum(c['count'] for c in clips.values()),source.stem+': full animation range')
        starts={name:clip['start']+1 for name,clip in clips.items()}
        check(all(any(m.name==name and m.frame==frame for m in scene.timeline_markers)
                  for name,frame in starts.items()),source.stem+': animation markers')
    for mat in bpy.data.materials:
        if not mat.users or not mat.use_nodes: continue
        nodes=mat.node_tree.nodes
        if not any(n.type=='BSDF_PRINCIPLED' for n in nodes): continue  # Retained distance haze.
        materials+=1
        check(mat.get('crownroad_finish')==REVISION,source.stem+'/'+mat.name+': surface finish')
        family=mat.get('surface_family','missing')
        families[family]+=1
        check(not any(n.type=='TEX_IMAGE' for n in nodes),source.stem+'/'+mat.name+': no external texture dependency')
        if family not in ('emissive','quiet'):
            check(any(n.type=='BUMP' for n in nodes),source.stem+'/'+mat.name+': shallow relief')
            check(any(n.type=='MAP_RANGE' for n in nodes),source.stem+'/'+mat.name+': roughness variation')
    print('FINISH_NATIVE_CHECK '+category+'/'+source.stem,flush=True)

report={'scenes_checked':scenes,'materials_checked':materials,'surface_families':dict(families),'failures':failures}
(REVIEW/('native-audit.json' if args.ids=='all' else 'native-audit-sample.json')).write_text(json.dumps(report,indent=2)+'\n')
print('FINISH_NATIVE_RESULT '+json.dumps(report),flush=True)
if failures: raise RuntimeError('Native material audit failed')
