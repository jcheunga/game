"""Generate editable fallback battlefields and the enemy gatehouse."""
import argparse
import json
import math
from pathlib import Path
import random
import sys

import bpy
from mathutils import Vector

SOURCE=Path(__file__).resolve().parent
ROOT=SOURCE.parents[1]
sys.path.insert(0,str(SOURCE))
from crownroad_art import area_light,box,camera,collection,setup_render
from world_kit import WorldKit

parser=argparse.ArgumentParser()
parser.add_argument('--category',choices=['all','battlefields','gatehouse'],default='all')
parser.add_argument('--ids',default='all')
parser.add_argument('--samples',type=int,default=32)
parser.add_argument('--resume',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
stages=json.loads((ROOT/'data/stages.json').read_text())['Stages']
terrains={s['TerrainId']:s['MapId'] for s in stages}


def reset(ident):
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for col in list(bpy.data.collections): bpy.data.collections.remove(col)
    for blocks in (bpy.data.meshes,bpy.data.curves,bpy.data.materials,bpy.data.cameras,bpy.data.lights):
        for block in list(blocks):
            if block.users==0: blocks.remove(block)
    random.seed(ident)


def lighting(scene,rig,route,night=False):
    setup_render(scene,args.samples)
    scene.render.film_transparent=False
    scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.10,.16,.2,1) if night else (.35,.44,.48,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=.45 if night else .6
    area_light('Warm overcast key',(-15,-15,30),(0,7,0),8000 if night else 14000,(.62,.76,1) if night else (1,.79,.57),18,rig)
    area_light('Cool sky fill',(15,5,25),(0,8,2),4500,(.6,.78,1),22,rig)
    area_light('Horizon light',(-10,25,18),(0,8,3),10000,(1,.59,.32) if route in ('foundry','citadel') else (.76,.86,1),18,rig)
    scene.world.color=(.12,.18,.21)


def populate(kit,route,ident):
    kit.terrain()
    for i in range(10): kit.mountain(-40+i*9,39+random.random()*4,random.uniform(7,12),random.uniform(7,11))
    if route in ('city','gloamwood','thornwall','quarantine'):
        for _ in range(76):
            x=random.uniform(-32,32); y=random.uniform(26,36)
            kit.tree(x,y,random.uniform(.65,1.8),dead=route=='quarantine',broad=route=='gloamwood')
        for side in (-1,1):
            for _ in range(10):kit.tree(side*random.uniform(20,29),random.uniform(8,25),random.uniform(.8,1.6),dead=route=='quarantine',broad=route=='gloamwood')
    elif route in ('mire','basilica'):
        for _ in range(23): kit.tree(random.uniform(-27,27),random.uniform(8,25),random.uniform(.8,1.6),dead=True)
    if route=='city':
        kit.gatehouse(7,20,1.7)
        for i in range(5): kit.house(-13+i*5.5,12+(i%2)*4,1.1,ruined=ident=='urban')
        if ident=='highway': kit.shrine(-13,8,1.25)
    elif route=='harbor':
        box('Harbor water',(0,19,.015),(65,25,.04),kit.water,kit.coll,0)
        for i in range(4): kit.dock(-19+i*10,12+i%2*5,1.3,ship=ident!='swamp')
        if ident=='industrial': kit.forge(14,20,1.5)
        if ident=='swamp':
            for i in range(15): kit.tree(random.uniform(-24,24),random.uniform(10,27),random.uniform(.9,1.5),dead=True)
    elif route=='foundry':
        for i in range(5): kit.forge(-20+i*9,12+(i%2)*8,1.2+i*.12)
        for i in range(9): kit.rock(random.uniform(-25,25),random.uniform(8,25),scale=random.uniform(.7,1.7))
        if ident=='railyard':
            for x in (-.7,.7): box('Ore wagon rail',(x,14,.1),(.1,22,.1),kit.iron,kit.coll)
    elif route=='quarantine':
        for i in range(6): kit.house(-20+i*7,12+(i%2)*5,1.1,ruined=True)
        for i in range(7):
            x=-20+i*6
            kit.brazier(x,9,ghost=True)
            box('Quarantine barricade',(x,11,.8),(3.9,.35,1.6),kit.wood,kit.coll)
        if ident in ('lab','blacksite'): kit.cathedral(7,23,1)
    elif route=='thornwall':
        for x in (-20,-14,14,21): kit.rock(x,10,scale=4)
        if ident=='shrine': kit.shrine(7,14,2)
        else:
            kit.tower(-8,14,6,1.3,True); kit.gatehouse(8,20,1.65)
    elif route=='basilica':
        kit.cathedral(3,20,1.8)
        kit.graveyard(-14,9,1.25); kit.graveyard(15,9,1.15)
        if ident=='reliquary':
            for x in (-12,13): kit.shrine(x,15,1.7)
    elif route=='mire':
        for x in (-17,0,18): box('Mire water pool',(x,13,.01),(9,13,.03),kit.water,kit.coll,0)
        if ident=='ferry': kit.dock(4,13,1.8)
        elif ident=='chapel': kit.cathedral(7,21,1.15)
        else: kit.shrine(5,16,1.7)
    elif route=='steppe':
        for i in range(9): kit.tent(-22+i*5.5,11+(i%3)*4,random.uniform(.8,1.5))
        if ident=='waystation': kit.house(4,18,2)
        if ident=='siegecamp': kit.gatehouse(6,22,1.5)
    elif route=='gloamwood':
        if ident=='witchcircle':
            for i in range(9):
                a=i*math.tau/9
                ob=kit.rock(5+4*math.cos(a),13+3*math.sin(a),scale=1.2)
                ob.scale.z=3
                kit.brazier(5+3*math.cos(a),13+2*math.sin(a),ghost=True)
        elif ident=='grove': kit.shrine(4,14,1.6)
        else: kit.house(-6,13,1.3,True)
    elif route=='citadel':
        kit.gatehouse(2,19,2.4)
        for x in (-19,-11,14,22): kit.tower(x,22,random.uniform(7,10),1.6,True)
        for x in (-18,-8,8,18):
            box('Fortress curtain wall',(x,25,2.7),(10,1.3,5.4),kit.stone,kit.coll)
        if ident=='bridgefort':
            box('Bridge stone parapet',(0,7,.6),(22,.6,1.2),kit.stone,kit.coll)


def save_render(scene,ident,category,output):
    output.parent.mkdir(parents=True,exist_ok=True)
    source=SOURCE/category/(ident+'.blend')
    source.parent.mkdir(parents=True,exist_ok=True)
    scene['asset_id']=ident
    scene['asset_category']=category
    scene['source_generator']='art/blender/build_worlds.py'
    scene.render.filepath=str(output)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(source))
    bpy.ops.render.render(write_still=True)
    print('WORLD_COMPLETE '+category+'/'+ident,flush=True)


jobs=[]
if args.category in ('all','gatehouse'): jobs.append(('gatehouse','gatehouse','citadel'))
if args.category in ('all','battlefields'): jobs.extend(('battlefields',k,v) for k,v in terrains.items())
for category,ident,route in jobs:
    if args.ids!='all' and ident not in args.ids.split(','): continue
    folder={'gatehouse':'structures','battlefields':'backgrounds'}[category]
    output=ROOT/'assets'/folder/(ident+'.png')
    if args.resume and output.exists():
        print('WORLD_SKIP '+category+'/'+ident,flush=True); continue
    reset(category+ident)
    geometry=collection('Editable '+ident+' environment')
    rig=collection('Camera and light rig')
    kit=WorldKit(geometry,route)
    scene=bpy.context.scene
    lighting(scene,rig,route,ident in ('night','witchcircle','marsh','blacksite','endless','raid'))
    if category=='gatehouse':
        kit.gatehouse()
        scene.camera=camera('Gatehouse battle camera',(7,-30,12),(0,0,2.1),6.4,rig)
        scene.render.resolution_x,scene.render.resolution_y=1440,1280
        scene.render.film_transparent=True
        # Smaller asset needs correspondingly smaller, lower-energy studio lights.
        for obj in rig.objects:
            if obj.type=='LIGHT':
                obj.location*=.3; obj.data.energy*=.065; obj.data.size*=.3
    else:
        populate(kit,route,ident)
        if category=='battlefields':
            # Keep architecture behind the playable lanes, where it cannot read as an obstacle.
            for obj in geometry.objects:
                if not obj.name.startswith(('Broad quiet','Worn horizontal','Fractured rock','Distant rugged',
                                            'Tree ','Leaf crown','Pine layered','Harbor water','Mire water')):
                    obj.location.y+=12
        mist=bpy.data.materials.new('Distance haze')
        mist.use_nodes=True
        mist.node_tree.nodes.clear()
        volume=mist.node_tree.nodes.new('ShaderNodeVolumePrincipled')
        volume.inputs['Color'].default_value=(.46,.59,.62,1)
        volume.inputs['Density'].default_value=.014 if route not in ('mire','quarantine') else .024
        volume.inputs['Anisotropy'].default_value=.3
        out=mist.node_tree.nodes.new('ShaderNodeOutputMaterial')
        mist.node_tree.links.new(volume.outputs['Volume'],out.inputs['Volume'])
        box('Distant atmospheric haze',(0,31,11),(90,40,23),mist,geometry,0)
        scene.camera=camera('Battle side-view camera',(0,-38,18.5),(0,10,1.5),53,rig)
        scene.render.resolution_x,scene.render.resolution_y=1280,720
    save_render(scene,ident,category,output)
