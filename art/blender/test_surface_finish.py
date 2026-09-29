"""Non-destructive finish tests against a saved representative source."""
import hashlib
import json
from pathlib import Path
import sys
import bpy

SOURCE=Path(__file__).resolve().parent
ROOT=SOURCE.parents[1]
sys.path.insert(0,str(SOURCE))
from surface_finish import finish_scene_materials, light_finish, profile_for

source=SOURCE/'units/player_brawler.blend'
original_hash=hashlib.sha256(source.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(source))
scene=bpy.context.scene
checks=[]


def check(condition,label):
    checks.append({'check':label,'passed':bool(condition)})
    print('SURFACE_CHECK '+('PASS ' if condition else 'FAIL ')+label,flush=True)


def snapshot():
    return {m.name:(m.get('crownroad_surface_recipe'),len(m.node_tree.nodes),len(m.node_tree.links))
            for m in bpy.data.materials if m.users and m.use_nodes}


def lights():
    return [(o.name,tuple(o.location),tuple(o.rotation_euler),o.data.energy,tuple(o.data.color),o.data.size)
            for o in scene.objects if o.type=='LIGHT' and o.data.type=='AREA']


check(profile_for('Tempered armor',.65,0)=='metal','Armor receives the metal surface family')
check(profile_for('Faction cloth',0,0)=='cloth','Faction fabric receives weave, not metal')
check(profile_for('Old ivory',0,0)=='bone','Ivory receives the bone surface family')
check(profile_for('Still water',.25,0)=='water','Water is not treated as metal')
check(profile_for('Ember seams',0,1.2)=='emissive','Magic remains emissive')
finish_scene_materials(scene)
first=snapshot()
finish_scene_materials(scene)
check(snapshot()==first,'Repeated finishing does not accumulate nodes or compound pigment changes')
painted=[m for m in bpy.data.materials if m.users and m.get('surface_family')=='paint']
check(len(painted)==1,'Shield has one distinct painted material, without duplicate copies')
cloth=[m for m in bpy.data.materials if m.users and m.get('surface_family')=='cloth']
check(any(any(n.type=='TEX_WAVE' for n in m.node_tree.nodes) for m in cloth),'Cloth retains woven relief')
check(all(not any(n.type=='TEX_WAVE' for n in m.node_tree.nodes) for m in painted),'Shield paint does not inherit cloth weave')
light_finish(scene,'units')
first_lights=lights()
light_finish(scene,'units')
check(lights()==first_lights,'Repeated lighting finish does not compound exposure or transforms')
check(hashlib.sha256(source.read_bytes()).hexdigest()==original_hash,'Tests leave the native source unchanged')
output={'checks':checks,'failures':sum(not c['passed'] for c in checks)}
(ROOT/'artifacts/blender/polish-v2/surface-tests.json').write_text(json.dumps(output,indent=2)+'\n')
if output['failures']: raise RuntimeError('Surface tests failed')
