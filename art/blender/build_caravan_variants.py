"""Editable caravan cosmetics and roof/stronghold weapon mounts."""
import argparse
import math
from pathlib import Path
import sys
import bpy
from mathutils import Vector

SOURCE=Path(__file__).resolve().parent
ROOT=SOURCE.parents[1]
sys.path.insert(0,str(SOURCE))
from crownroad_art import area_light,beam,box,camera,collection,cylinder,material,mesh,rgba,ring,setup_render

parser=argparse.ArgumentParser()
parser.add_argument('--samples',type=int,default=48)
parser.add_argument('--category',choices=['all','skins','mounts'],default='all')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])

SKINS={
    'skin_iron':('40576a','8090a0','78838b'),
    'skin_royal':('67313a','ad7142','d9b451'),
    'skin_bone':('6e6856','b4ad91','cfc4a5'),
    'skin_flame':('883a21','d56830','c08046'),
    'skin_shadow':('222233','484665','625976'),
    'skin_guild':('165b3a','38955f','b59d61'),
    'skin_legendary':('624477','af87be','d5b96e'),
}

def recolor(mat,base,light=None):
    mat.diffuse_color=rgba(base)
    for node in mat.node_tree.nodes:
        if node.type=='BSDF_PRINCIPLED': node.inputs['Base Color'].default_value=rgba(base)
        elif node.type=='VALTORGB':
            node.color_ramp.elements[0].color=rgba(base)
            node.color_ramp.elements[-1].color=rgba(light or base)

def publish(ident,source_folder):
    scene=bpy.context.scene
    setup_render(scene,args.samples)
    target=ROOT/'assets/structures'/(ident+'.png')
    folder=SOURCE/source_folder;folder.mkdir(exist_ok=True)
    scene.render.filepath=str(target)
    scene['asset_id']=ident
    scene['source_generator']='art/blender/build_caravan_variants.py'
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(folder/(ident+'.blend')),compress=True)
    bpy.ops.render.render(write_still=True)
    print('VARIANT_COMPLETE '+ident,flush=True)

if args.category in ('all','skins'):
    for ident,(base,light,trim) in SKINS.items():
        bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'lantern_caravan.blend'))
        for mat in bpy.data.materials:
            if mat.name.startswith(('Canvas','Paint')):recolor(mat,base,light)
            if mat.name.startswith('Brass'):recolor(mat,trim)
            if ident=='skin_shadow' and mat.name.startswith('Oak'):recolor(mat,'1a1821','3f3544')
        extras=collection('10 • Cosmetic details')
        metal=material('Skin accent',trim,.45,.65)
        if ident=='skin_iron':
            for x in (-1.95,-1.1,.6,1.45):
                box('Forged side reinforcement',(x,-1.2,2.35),(.46,.06,.68),metal,extras)
                for dx in (-.16,.16):
                    for dz in (-.25,.25):cylinder('Armor rivet',(x+dx,-1.244,2.35+dz),.031,.025,metal,extras,'Y',8)
        if ident=='skin_bone':
            bone=material('Bone trophies','bcb294',.85)
            for x in (-1.7,-.9,.6,1.4):
                for slope in (-1,1):beam('Crossed trophy bone',(x-.18,-1.26,2.34-slope*.18),(x+.18,-1.26,2.34+slope*.18),.07,.065,bone,extras)
        if ident in ('skin_royal','skin_legendary'):
            for x in (-1.8,-.9,0,.9,1.8):
                mesh('Gilt heraldic lozenge',[(x,-1.26,1.68),(x+.12,-1.26,1.85),(x,-1.26,2.02),(x-.12,-1.26,1.85)],[(0,1,2,3)],metal,extras)
        if ident=='skin_flame':
            glow=material('Ember seams','ff7c2c',.5,emission=1.2)
            for x in (-2,-1.25,.8,1.55):
                mesh('Fire-forged chevron',[(x-.16,-1.24,1.66),(x,-1.24,2.12),(x+.16,-1.24,1.66)],[(0,1,2)],glow,extras)
        publish('war_wagon_'+ident,'caravans')

if args.category in ('all','mounts'):
    for ident,unit in [('arrows','player_shooter'),('ballista','player_ballista'),('frost','player_stormcaller'),('hex','enemy_lich')]:
        bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'units'/(unit+'.blend')))
        scene=bpy.context.scene;scene.frame_set(1)
        # Battle scenes retain their authored right-facing camera and natural ground pivot.
        scene.render.resolution_x,scene.render.resolution_y=384,480
        publish('mount_'+ident,'mounts')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll=collection('Editable firepot mortar');rig=collection('Camera and lights')
    wood=material('Mortar oak','513721',.85,variation='896443',grain=(1,16,16))
    iron=material('Mortar barrel','35424a',.49,.7)
    brass=material('Mortar binding','9c7945',.44,.65)
    ember=material('Mortar ember','ff963e',.5,emission=1.5)
    for y in (-.3,.3):
        box('Mortar skid',(0,y,.12),(1.5,.2,.23),wood,coll)
        beam('Mortar brace',(-.5,y,.2),(.05,y,.73),.13,.13,wood,coll)
    cylinder('Mortar trunnion',(0,0,.56),.16,1,brass,coll,'Y')
    direction=Vector((.65,0,.76));center=Vector((.14,0,.79))
    barrel=cylinder('Hollow mortar barrel',center,.31,.85,iron,coll)
    barrel.rotation_euler=direction.to_track_quat('Z','Y').to_euler()
    mouth=center+direction*.43
    rim=ring('Mortar flared muzzle',mouth,.35,.23,.13,brass,coll,'Z',32)
    rim.rotation_euler=barrel.rotation_euler
    opening=cylinder('Muzzle hot core',mouth-direction*.035,.225,.025,ember,coll)
    opening.rotation_euler=barrel.rotation_euler
    scene=bpy.context.scene
    scene.camera=camera('Mount camera',(5.5,-20,6.7),(.18,0,1.28),4.1,rig)
    scene.render.resolution_x,scene.render.resolution_y=384,480
    area_light('Key',(-3,-5,7),(0,0,1),600,(1,.83,.65),4,rig)
    area_light('Fill',(4,-2,4),(0,0,1),350,(.63,.8,1),3,rig)
    publish('mount_firepot','mounts')
