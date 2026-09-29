"""Re-author attacks in saved, finished rigs, retaining all materials and other clips.

Blender --background --python art/blender/animate_combat.py -- --ids all --publish
Ten attack poses: anticipation, held aim, contact/release, overshoot, recovery.
The other 22 poses and portraits are copied losslessly, not rebuilt or relit.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import shutil
import sys
import time
import bpy
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT/'artifacts/blender/combat-motion'
REVISION = 'contact-v1'
parser = argparse.ArgumentParser()
parser.add_argument('--ids', default='all')
parser.add_argument('--publish', action='store_true')
parser.add_argument('--resume', action='store_true')
args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
specs = json.loads((ROOT/'data/units.json').read_text())['Units']
fingerprint = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()


def profile(spec):
    ident, name, cls = spec['Id'], spec['DisplayName'].lower(), spec['VisualClass']
    if cls == 'siegetower': return 'siege-deploy'
    if cls == 'splitter': return 'nest-pulse'
    if 'engine' in ident: return 'bombard'
    if 'ballista' in ident: return 'ballista'
    if cls == 'hound': return 'pounce'
    if ident in ('player_raider', 'enemy_boss_steppe'): return 'mounted-lance'
    if 'archer' in name: return 'bow-draw'
    if 'crossbow' in name: return 'crossbow'
    if 'spear' in name: return 'spear-thrust'
    if 'alchemist' in name or 'sapper' in name: return 'flask-toss'
    if 'engineer' in name: return 'hammer-command'
    if any(x in name for x in ('mage','monk','witch','lich','pontiff','necromancer','caster','hexer','stormcaller','tidecaller','archon','herald')):
        return 'staff-cast' if spec.get('UsesProjectile') else 'staff-strike'
    if any(x in name for x in ('halberd','berserker','chieftain')): return 'axe-cleave'
    if cls in ('brute','crusher'): return 'heavy-smash'
    if cls in ('bloater','runner') or 'mire' in ident: return 'claw-rake'
    if ident == 'player_rogue': return 'blade-stab'
    if cls in ('shield','mirror'): return 'guard-cut'
    if cls == 'boss': return 'royal-cleave'
    return 'sword-cut'


def capture(joints):
    return {name: [list(o.location), list(o.rotation_euler), list(o.scale)] for name, o in joints.items()}


def restore(joints, pose):
    for name, (loc, rot, scale) in pose.items():
        joints[name].location, joints[name].rotation_euler, joints[name].scale = loc, rot, scale


def keyed_pose(joints, rest, family, spec, stage):
    restore(joints, rest)
    if stage == 'rest': return capture(joints)
    wind = stage == 'wind'
    follow = stage == 'follow'
    # Deliberate weight/cadence variations keep light infantry, elites and bosses distinct.
    weight = 1.15 if spec['VisualClass'] in ('boss','crusher','brute') else .9 if spec['Id'] in ('player_rogue','enemy_runner') else 1
    def r(name, axis, val):
        if name in joints: joints[name].rotation_euler[axis] += val
    def l(name, axis, val):
        if name in joints: joints[name].location[axis] += val
    l('ROOT', 0, -.055 if wind else .16 if not follow else .09)
    r('Spine', 1, -.08 if wind else .13*weight)
    r('Head', 1, .07 if wind else -.06)
    if family in ('bow-draw','crossbow'):
        r('Near shoulder', 1, -.6); r('Near elbow', 1, -.2); r('Near hand', 1, .8)
        r('Far shoulder', 1, -.9); r('Far elbow', 1, -.55 if wind else -.1)
        l('Far hand', 0, -.2 if wind else .1)
        l('Near hand', 0, -.09 if follow else .04)
        l('ROOT', 0, -.16 if follow else -.08)
        r('Spine', 1, -.1 if follow else -.06)
    elif family in ('spear-thrust','mounted-lance','blade-stab'):
        r('Near shoulder', 1, -.35 if wind else -.9)
        r('Near elbow', 1, -.3 if wind else -.1)
        r('Near hand', 1, 1.65 if wind else 2.12)
        l('Near hand', 0, -.13 if wind else .10)
        r('Far shoulder', 1, -.25 if wind else -.7)
        r('Far elbow', 1, -.45)
        r('Spine', 1, -.08 if wind else .14)
        if family == 'mounted-lance':
            r('Beast torso', 1, -.035 if wind else .06)
            l('ROOT', 2, .035 if wind else 0)
    elif family in ('staff-cast','staff-strike','hammer-command','flask-toss'):
        r('Near shoulder', 1, -.55 if wind else -.85)
        r('Near elbow', 1, -.5 if wind else -.1)
        r('Near hand', 1, .9 if wind else 1.75 if family == 'staff-strike' else 1.2)
        r('Far shoulder', 1, -.65 if wind else -1.3)
        r('Far elbow', 1, -.8 if wind else -.15)
        l('ROOT', 2, .035 if wind else 0)
        if family == 'flask-toss':
            r('Near shoulder', 1, 1.1 if wind else -.45)
            r('Near hand', 1, -.9 if wind else -.3)
    elif family in ('ballista','bombard','siege-deploy','nest-pulse'):
        l('ROOT', 0, -.16 if follow else -.16 if wind else -.12)
        l('Weapon mount', 0, -.10 if wind else -.26 if follow else .02)
        r('Weapon mount', 1, -.08 if wind else .08 if follow else 0)
        if family == 'nest-pulse':
            joints['ROOT'].scale = Vector((1.06,1.06,.90) if wind else (.95,.95,1.10))
        if family == 'siege-deploy': r('ROOT', 1, -.025 if wind else .025)
    elif family == 'pounce':
        r('Beast torso', 1, -.13 if wind else .15)
        l('ROOT', 2, -.07 if wind else .12 if not follow else .02)
        l('ROOT', 0, -.12 if wind else .16)
        for name in joints:
            if name.startswith('Beast leg'): r(name, 1, .45 if wind else -.55)
    elif family == 'claw-rake':
        r('Near shoulder', 1, .15 if wind else -1.25)
        r('Near elbow', 1, -.9 if wind else -.15)
        r('Far shoulder', 1, -.9 if wind else -.35)
        r('Far elbow', 1, -.4)
        r('Spine', 2, -.10 if wind else .12)
    else:
        heavy = family in ('axe-cleave','heavy-smash','royal-cleave')
        r('Near shoulder', 1, -.55 if wind and heavy else .15 if wind else -.9)
        r('Near elbow', 1, -1.5 if wind and heavy else -1.05 if wind else -.15)
        r('Near hand', 1, 1.15 if wind and heavy else .4 if wind else 2.2 if not follow else 2.45)
        r('Spine', 2, -.12 if wind else .12)
        r('Far shoulder', 1, -.3 if wind else -.65)
        r('Far elbow', 1, -.65 if family == 'guard-cut' else -.25)
        if heavy: l('ROOT', 2, .055 if wind else -.025)
    # Large figures strike down into infantry, rather than swinging over their heads.
    low_strike = not spec.get('UsesProjectile') and (spec['VisualClass']=='boss' or spec['Id']=='enemy_catacomb_giant')
    if low_strike and not wind and 'Near hand' in joints:
        if family == 'claw-rake':
            joints['Near shoulder'].rotation_euler.y=.12
            joints['Near elbow'].rotation_euler.y=-.12
            l('Spine',2,-.2)
        else:
            joints['Near shoulder'].rotation_euler.y=.05
            joints['Near elbow'].rotation_euler.y=-.25
            joints['Near hand'].rotation_euler.y=(2.4 if family=='staff-strike' else 2.65)+(.12 if follow else 0)
        if family=='mounted-lance': l('ROOT',0,-.3)
    if 'Near hip' in joints and family != 'mounted-lance':
        r('Near hip', 1, .16 if wind else -.24)
        r('Near knee', 1, .20 if wind else .12)
        r('Far hip', 1, -.12 if wind else .2)
        r('Far knee', 1, .12)
    return capture(joints)


def interpolate(a, b, t):
    t = t*t*(3-2*t)
    return {n: [[x+(y-x)*t for x,y in zip(v,w)] for v,w in zip(a[n],b[n])] for n in a}


def process(spec):
    ident = spec['Id']; source = ROOT/'art/blender/units'/f'{ident}.blend'
    master = ROOT/'artifacts/blender/units'/ident
    folder = REVIEW/ident; folder.mkdir(parents=True, exist_ok=True)
    record_path = folder/'published.json'
    if args.resume and record_path.exists():
        record = json.loads(record_path.read_text())
        if record['fingerprint'] == fingerprint and record['source_hash'] == hashlib.sha256(source.read_bytes()).hexdigest():
            print('MOTION_SKIP '+ident, flush=True); return
    started = time.monotonic()
    bpy.ops.wm.open_mainfile(filepath=str(source))
    scene = bpy.context.scene
    joints = {o.name:o for o in scene.objects if o.type == 'EMPTY'}
    meta = json.loads((master/'metadata.json').read_text())
    # Retain a stable in-scene baseline so repeated authoring never compounds rotations.
    if 'motion_baseline' in scene:
        baseline = json.loads(scene['motion_baseline'])
    else:
        baseline = {}
        for state, clip in meta['animations'].items():
            baseline[state] = []
            for frame in range(clip['start'], clip['start']+clip['count']):
                scene.frame_set(frame+1); baseline[state].append(capture(joints))
        scene['motion_baseline'] = json.dumps(baseline)
    # Save old rendered clips before any overlapping indices are republished.
    preserved = {}
    for state, clip in meta['animations'].items():
        if state != 'attack':
            preserved[state] = [(master/f'{i:03}.png').read_bytes() for i in range(clip['start'],clip['start']+clip['count'])]
    for obj in joints.values(): obj.animation_data_clear()
    scene.timeline_markers.clear()
    family = profile(spec)
    rest = baseline['idle'][0]
    poses = {s:keyed_pose(joints,rest,family,spec,s) for s in ('rest','wind','contact','follow')}
    keys = [(0,'rest'),(2,'wind'),(3,'wind'),(4,'contact'),(5,'follow'),(9,'rest')]
    attacks = []
    for f in range(10):
        for (start,a),(end,b) in zip(keys,keys[1:]):
            if start <= f <= end:
                attacks.append(interpolate(poses[a],poses[b],(f-start)/(end-start))); break
    duration = min(.085, max(.046, float(spec['AttackCooldown'])*.068))
    if family in ('axe-cleave','heavy-smash','royal-cleave','bombard'): duration = min(.09,duration*1.14)
    new_clips = {}; f=0
    for state in ('idle','walk','attack','hit','death','deploy'):
        samples = attacks if state == 'attack' else baseline[state]
        clip = dict(meta['animations'][state], start=f, count=len(samples))
        if state == 'attack': clip.update(duration=round(duration,5),contactFrame=4)
        new_clips[state] = clip
        scene.timeline_markers.new(state, frame=f+1)
        for i, pose in enumerate(samples):
            restore(joints,pose)
            for obj in joints.values():
                for prop in ('location','rotation_euler','scale'): obj.keyframe_insert(data_path=prop,frame=f+1,group=state)
            if state != 'attack': (folder/f'{f:03}.png').write_bytes(preserved[state][i])
            f+=1
    scene.timeline_markers.new('CONTACT / RELEASE',frame=15)
    for obj in scene.objects:
        if obj.name.startswith(('Nocked arrow','Siege bolt')):
            for frame, hidden in ((1,False),(14,False),(15,True),(18,True),(19,False),(32,False)):
                obj.hide_render=hidden
                obj.keyframe_insert(data_path='hide_render',frame=frame)
    scene.frame_start,scene.frame_end=1,f
    envelope=1.0
    for frame in range(1,f+1):
        scene.frame_set(frame); bpy.context.view_layer.update()
        for obj in joints['ROOT'].children_recursive:
            if obj.type not in ('MESH','CURVE'): continue
            for corner in obj.bound_box:
                p=world_to_camera_view(scene,scene.camera,obj.matrix_world @ Vector(corner))
                envelope=max(envelope,abs(p.x-.5)*2/.94,abs(p.y-.5)*2/.94)
    reframed=envelope>1.001
    if reframed:
        scene.camera.data.ortho_scale*=envelope
        meta['drawScale']*=envelope
    scene.frame_set(1)
    ground = world_to_camera_view(scene,scene.camera,Vector((0,0,0)))
    meta['anchorY']=1-ground.y
    def projected(point):
        p = world_to_camera_view(scene,scene.camera,point)
        return [round(p.x-ground.x,6),round(ground.y-p.y,6)]
    body_z = 1.9 if family == 'mounted-lance' else .7 if family in ('pounce','ballista','bombard','nest-pulse') else 1.25
    body = projected(Vector((0,0,body_z)))
    scene.frame_set(15); bpy.context.view_layer.update()
    if 'Near hand' in joints:
        tip = Vector((0,0,1.05))
        if family in ('spear-thrust','mounted-lance'): tip.z=1.8
        elif family in ('staff-cast','staff-strike'): tip.z=1.05 if family=='staff-strike' and spec['VisualClass']=='boss' else 1.45
        elif family in ('bow-draw','crossbow'): tip=Vector((.7,0,.1))
        elif family in ('flask-toss','claw-rake'): tip=Vector((.20,0,0))
        elif family == 'hammer-command': tip.z=.7
        contact = projected(joints['Near hand'].matrix_world @ tip)
    else:
        contact = projected(Vector((1.12,0,.85 if family=='pounce' else 1.1)))
    meta.update(animations=new_clips,anchorX=round(ground.x,6),motion={'revision':REVISION,'profile':family,'body':body,'contact':contact})
    scene['animations']=json.dumps(new_clips)
    scene['combat_motion']=json.dumps(meta['motion'])
    scene['motion_notes']='Ten-frame authored attack; contact/release is local frame 4. Other clips and surface finish retained.'
    scene.render.resolution_x,scene.render.resolution_y=256,320
    scene.cycles.samples=64
    # Wider weapons need a larger canvas. Compensating drawScale preserves game size.
    # Re-render every pose in that case so no clip jumps between camera scales.
    for frame in (range(f) if reframed else range(10,20)):
        scene.frame_set(frame+1); bpy.context.view_layer.update()
        scene.render.filepath=str(folder/f'{frame:03}.png')
        bpy.ops.render.render(write_still=True)
    (folder/'metadata.json').write_text(json.dumps(meta,indent=2)+'\n')
    scene.frame_set(1)
    if args.publish:
        bpy.context.preferences.filepaths.save_version=0
        scene.render.filepath=str(master/'000.png')
        bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
        for frame in range(f): shutil.copy2(folder/f'{frame:03}.png',master/f'{frame:03}.png')
        shutil.copy2(folder/'metadata.json',master/'metadata.json')
        (master/'complete.json').write_text(json.dumps({'id':ident,'frames':f,'source':meta['source'],'motion':REVISION},indent=2)+'\n')
        record_path.write_text(json.dumps({'id':ident,'profile':family,'frames':f,'fingerprint':fingerprint,
            'source_hash':hashlib.sha256(source.read_bytes()).hexdigest(),'seconds':round(time.monotonic()-started,2)},indent=2)+'\n')
    print('MOTION_COMPLETE '+ident+' '+family,flush=True)


for spec in specs:
    if args.ids == 'all' or spec['Id'] in args.ids.split(','): process(spec)
