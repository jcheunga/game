"""Model and render Crownroad relics, spell emblems, reward badges and particles."""
import argparse
import json
import math
from pathlib import Path
import random
import re
import sys
import bpy
from mathutils import Vector

SOURCE=Path(__file__).resolve().parent
ROOT=SOURCE.parents[1]
sys.path.insert(0,str(SOURCE))
from crownroad_art import area_light,beam,box,camera,collection,cylinder,finish,material,mesh,ring,setup_render,tube

parser=argparse.ArgumentParser()
parser.add_argument('--category',choices=['all','icons','particles'],default='all')
parser.add_argument('--ids',default='all')
parser.add_argument('--samples',type=int,default=32)
parser.add_argument('--resume',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])


def reset():
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for col in list(bpy.data.collections): bpy.data.collections.remove(col)
    for blocks in (bpy.data.meshes,bpy.data.curves,bpy.data.materials,bpy.data.cameras,bpy.data.lights):
        for block in list(blocks):
            if not block.users: blocks.remove(block)


def catalog(name):
    text=(ROOT/'scripts/core/AssetCoverageCatalog.cs').read_text()
    block=re.search(rf'{name}\s*=\s*\{{(.*?)\}};',text,re.S)
    return re.findall(r'"([^"]+)"',block.group(1))


class Icon:
    def __init__(self,ident,category,rarity='rare'):
        self.ident=ident
        self.category=category
        self.coll=collection('Editable '+ident)
        colors={'common':'87928c','rare':'267b71','epic':'715893','legendary':'b18b3f','mythic':'ac4741'}
        self.accent_color=colors.get(rarity,'267b71')
        if category=='spells':
            self.accent_color=('c65022' if 'fire' in ident else '57a68c' if 'heal' in ident or 'resurrect' in ident
                               else '639daf' if 'frost' in ident else '9175ad' if 'lightning' in ident or 'polymorph' in ident
                               else 'bc9551')
        self.gold=material('Old gold','ab8747',.43,.68)
        self.metal=material('Iron and silver','657b83',.45,.7)
        self.dark=material('Recessed bronze','263535',.74,.2)
        self.wood=material('Walnut','4c3523',.8,variation='94754a',grain=(1,15,15))
        self.bone=material('Ivory','bdb495',.85)
        self.red=material('Oxblood leather','692e2b',.8)
        self.cloth=material('Artifact color',self.accent_color,.67,.12)
        self.gem=material('Enchanted crystal',self.accent_color,.23,.25,emission=.3)
        self.light=material('Magic core','b7e9d5',.4,emission=1.5)

    def sphere(self,name,loc,size,mat):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=12,radius=1,location=loc)
        o=bpy.context.object;o.scale=size
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        return finish(o,name,mat,self.coll,smooth=True)

    def cone(self,name,loc,r1,r2,h,mat,vertices=8):
        bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=r1,radius2=r2,depth=h,location=loc)
        return finish(bpy.context.object,name,mat,self.coll,.01)

    def box(self,name,p,s,mat): return box(name,p,s,mat,self.coll,.035)

    def crystal(self,x,y,z,size=1):
        self.cone('Faceted crystal',(x,y,z+.25*size),.17*size,0,.6*size,self.gem,6)
        self.cone('Crystal base',(x,y,z-.08*size),.08*size,.17*size,.16*size,self.gem,6)

    def medal_base(self):
        cylinder('Dark enamel medallion',(0,0,.035),1.04,.09,self.dark,self.coll,vertices=64)
        ring('Raised bronze rim',(0,0,.09),1.09,1.025,.08,self.gold,self.coll,'Z',64)
        ring('Inner engraved line',(0,0,.095),.95,.93,.016,self.metal,self.coll,'Z',64)
        for i in range(12):
            a=i*math.tau/12
            self.sphere('Edge rivet',(1.055*math.cos(a),1.055*math.sin(a),.145),(.025,.025,.025),self.gold)

    def sword(self,ruin=False):
        self.box('Sword wrapped grip',(0,0,.38),(.13,.13,.5),self.red)
        self.box('Sword guard',(0,0,.68),(.72,.17,.105),self.gold)
        verts=[(-.12,-.045,.74),(.12,-.045,.74),(.12,.045,.74),(-.12,.045,.74),(0,0,2.02)]
        mesh('Sword diamond blade',verts,[(0,1,4),(1,2,4),(2,3,4),(3,0,4)],self.metal,self.coll)
        self.sphere('Sword pommel',(0,0,.15),(.12,.12,.13),self.gold)
        if ruin:
            tube('Cursed blade inlay',[(-.015,-.049,.82),(.03,-.047,1.2),(-.025,-.04,1.58)],.018,self.gem,self.coll)

    def shield(self):
        verts=[(-.62,0,1.52),(.62,0,1.52),(.61,0,.88),(.33,0,.4),(0,0,.19),(-.33,0,.4),(-.61,0,.88)]
        mesh('Heraldic shield',verts,[tuple(range(7))],self.cloth,self.coll)
        tube('Shield bound rim',[(x,y-.06,z) for x,y,z in verts],.052,self.gold,self.coll,True)
        beam('Shield center',(0,-.065,.42),(0,-.065,1.4),.045,.04,self.gold,self.coll)
        beam('Shield crossbar',(-.39,-.065,1.14),(.39,-.065,1.14),.04,.04,self.gold,self.coll)
        self.sphere('Shield jewel',(0,-.11,1.12),(.12,.07,.12),self.gem)

    def crown(self,ice=False):
        ring('Royal crown band',(0,0,.56),.65,.54,.34,self.metal if ice else self.gold,self.coll,'Z',48)
        for i in range(7):
            a=i*math.tau/7
            x,y=.6*math.cos(a),.6*math.sin(a)
            self.cone('Crown fleur point',(x,y,.94),.15,.025,.53,self.metal if ice else self.gold,4)
            self.sphere('Crown cabochon',(x*1.07,y*1.07,.59),(.085,.085,.085),self.gem)
        if ice:
            for i in range(5):
                a=i*math.tau/5
                self.crystal(.45*math.cos(a),.45*math.sin(a),.91,.8)

    def book(self):
        self.box('Gilt book pages',(0,0,.49),(1.12,.81,.49),self.bone)
        for z in (.22,.77):self.box('Leather book cover',(0,0,z),(1.24,.9,.08),self.cloth)
        self.box('Book spine',(-.59,0,.49),(.13,.9,.52),self.red)
        for yy in (-.34,.34): self.box('Book clasp',(.48,yy,.77),(.18,.14,.11),self.gold)
        self.crystal(.04,0,.8,.6)

    def flask(self,censer=False):
        self.sphere('Potion vessel',(0,0,.79),(.43,.43,.56),self.gem)
        cylinder('Bottle neck',(0,0,1.3),.16,.24,self.gold,self.coll,vertices=16)
        cylinder('Bottle cork',(0,0,1.47),.17,.14,self.wood,self.coll,vertices=12)
        for i in range(5):
            a=i*math.tau/5
            tube('Bottle cage',[(.18*math.cos(a),.18*math.sin(a),1.25),(.43*math.cos(a),.43*math.sin(a),.82),
                               (.3*math.cos(a),.3*math.sin(a),.35)],.026,self.gold,self.coll)
        if censer:
            for i in range(3):
                a=i*math.tau/3
                tube('Censer chains',[(.4*math.cos(a),.4*math.sin(a),.9),(.12*math.cos(a),.12*math.sin(a),1.75)],.016,self.metal,self.coll)

    def heart(self):
        self.sphere('Heart left lobe',(-.23,0,1.12),(.35,.22,.35),self.gem)
        self.sphere('Heart right lobe',(.23,0,1.12),(.35,.22,.35),self.gem)
        self.cone('Heart lower point',(0,0,.75),.0,.51,.63,self.gem,12)

    def lightning(self):
        verts=[(.18,-.04,1.93),(-.5,-.04,.91),(-.08,-.04,.94),(-.25,-.04,.25),(.6,-.04,1.27),(.11,-.04,1.19)]
        mesh('Thunderbolt',verts,[tuple(range(6))],self.gem,self.coll)
        tube('Thunderbolt rim',verts,.025,self.gold,self.coll,True)

    def flame(self):
        hot=material('Golden flame heart','ffb338',.48,emission=.7)
        for i in range(6):
            a=i*math.tau/6
            x,y=.27*math.cos(a),.24*math.sin(a)
            height=1.1+random.random()*.5
            verts=[]
            for j in range(13):
                t=j/12
                radius=.25*math.sin(math.pi*(.12+.88*t))*(1-t*.7)
                for k in range(12):
                    angle=k*math.tau/12
                    verts.append((x+.24*math.sin(t*math.pi*1.4)+radius*math.cos(angle),
                                  y+.1*math.sin(t*math.pi)+radius*math.sin(angle),.25+t*height))
            faces=[(j*12+k,j*12+(k+1)%12,(j+1)*12+(k+1)%12,(j+1)*12+k) for j in range(12) for k in range(12)]
            mesh('Swept flame tongue',verts,faces,self.gem,self.coll,smooth=True)
        self.sphere('Fireball core',(0,-.07,.64),(.35,.3,.43),hot)

    def boots(self):
        for x,y in ((-.26,-.1),(.27,.17)):
            self.box('Boot sole',(x,y-.18,.25),(.42,.68,.13),self.dark)
            self.box('Leather foot',(x,y-.18,.38),(.39,.61,.2),self.wood)
            self.box('Armored greave',(x,y+.02,.88),(.32,.34,.83),self.metal)
            self.box('Greave colored cuff',(x,y+.015,1.23),(.38,.4,.1),self.cloth)
            for z in (.66,.97):self.box('Greave buckle',(x,y-.175,z),(.24,.045,.09),self.gold)

    def cloak(self):
        verts=[]
        for j in range(8):
            t=j/7
            for i in range(11):
                u=i/10
                verts.append(((u-.5)*(.5+1.1*t),.1+math.sin(u*math.pi*6)*.075+.12*t,1.85-t*1.5))
        mesh('Folded enchanted mantle',verts,[(j*11+i,j*11+i+1,(j+1)*11+i+1,(j+1)*11+i) for j in range(7) for i in range(10)],self.cloth,self.coll,smooth=True)
        tube('Cloak stitched hem',verts[-11:],.027,self.gold,self.coll)
        self.sphere('Cloak clasp',(0,-.025,1.7),(.09,.04,.09),self.gem)

    def coins(self):
        for j,(x,y,height) in enumerate(((-.36,-.25,4),(.25,-.14,6),(.02,.4,3))):
            for i in range(height):
                cylinder('Embossed gold coin',(x,y,.2+i*.105),.3,.085,self.gold,self.coll,vertices=32)
            self.box('Coin crown emboss',(x,y,.2+height*.105),(.17,.1,.025),self.gold)

    def people(self,number=2):
        for i in range(number):
            x=(i-(number-1)*.5)*.48
            self.sphere('Companion head',(x,0,1.14),(.18,.18,.2),self.bone)
            self.cone('Companion coat',(x,0,.64),.24,.15,.68,self.cloth,8)
            ring('Companion collar',(x,0,.96),.18,.14,.06,self.gold,self.coll,'Z',24)

    def tower(self):
        cylinder('Tower badge',(0,0,.8),.44,1.23,self.metal,self.coll,vertices=12)
        for i in range(6):
            a=i*math.tau/6
            self.box('Tower merlon',(.36*math.cos(a),.36*math.sin(a),1.5),(.2,.2,.27),self.gold)
        self.box('Tower door',(0,-.44,.54),(.22,.04,.46),self.dark)

    def build(self):
        self.medal_base()
        ident={'relic_tower_sentinel':'relic_sentinel_ward','relic_tower_ascendant':'relic_ascendant_signet',
               'relic_tower_apex':'relic_apex_talisman','relic_tower_pinnacle':'relic_pinnacle_crown'}.get(self.ident,self.ident)
        if ident=='spell_earthquake':
            for x,y,angle in ((-.38,-.12,-.2),(.37,.04,.2),(-.15,.45,-.15)):
                stone=self.box('Broken earth slab',(x,y,.38),(.67,.6,.37),self.metal)
                stone.rotation_euler=(angle*.4,angle,angle)
            tube('Earthquake fissure',[(0,-.67,.62),(-.15,-.25,.64),(.12,.07,.61),(-.12,.42,.68),(.04,.72,.64)],.035,self.light,self.coll)
        elif ident=='relic':
            self.box('Relic chest body',(0,0,.47),(1.2,.76,.61),self.wood)
            self.box('Relic chest lid',(0,0,.82),(1.27,.82,.15),self.cloth)
            for x in (-.43,.43):self.box('Chest gold strap',(x,0,.92),(.085,.83,.055),self.gold)
            self.box('Chest lock',(0,-.41,.67),(.2,.07,.23),self.gold)
        elif ident=='sigils':
            ring('Seal face',(0,0,.53),.62,.55,.14,self.gold,self.coll,'Y',48)
            points=[(.45*math.sin(i*math.tau*2/5),-.09,.53+.45*math.cos(i*math.tau*2/5)) for i in range(5)]
            tube('Five-point sigil',points,.035,self.gem,self.coll,True)
        elif ident=='shards':
            for x,y,z,s in ((-.4,.1,.49,1.1),(.1,.25,.65,1.5),(.4,-.3,.42,.75)):self.crystal(x,y,z,s)
        elif ident=='spell' and self.category=='rewards':
            self.box('Spell scroll parchment',(0,0,.6),(.88,.09,1.1),self.bone)
            for z in (.08,1.17):cylinder('Rolled scroll edge',(0,0,z),.12,1.12,self.gold,self.coll,'X',24)
            self.crystal(0,-.14,.55,.85)
        elif any(k in ident for k in ('sword','blade','edge','war_brand','fang')):
            self.sword('ruin' in ident)
            if 'war_brand' in ident:
                self.box('War-brand red pennant',(.29,.06,1.06),(.4,.07,.57),self.red)
            if 'hardened_fang' in ident:
                for z in (.9,1.08,1.26):self.box('Serrated fang tooth',(.14,0,z),(.14,.06,.09),self.metal)
        elif any(k in ident for k in ('shield','bulwark','ward','sentinel','barrier')): self.shield()
        elif any(k in ident for k in ('crown','arena_rating')): self.crown('frost' in ident)
        elif any(k in ident for k in ('boots',)): self.boots()
        elif any(k in ident for k in ('cloak','mantle')): self.cloak()
        elif any(k in ident for k in ('tome','book','spell')) and self.category!='spells': self.book()
        elif ident=='gold': self.coins()
        elif 'pendant' in ident:
            ring('Pendant chain',(0,0,1.16),.43,.407,.032,self.metal,self.coll,'Y',48)
            ring('Pendant crest',(0,-.06,.56),.31,.24,.085,self.metal,self.coll,'Y',32)
            self.sphere('Pendant central stone',(0,-.07,.56),(.16,.06,.2),self.gem)
        elif any(k in ident for k in ('heart','heal')): self.heart()
        elif any(k in ident for k in ('essence','soul','censer','talisman')): self.flask('censer' in ident)
        elif any(k in ident for k in ('lightning','stormcaller')): self.lightning()
        elif 'fireball' in ident or ident=='daily_streak': self.flame()
        elif any(k in ident for k in ('tower','endless_wave')): self.tower()
        elif ident in ('friends','guild','members','unit'): self.people(3 if ident in ('guild','members') else 2)
        elif 'drum' in ident:
            cylinder('War drum',(0,0,.75),.51,.8,self.wood,self.coll,vertices=24)
            for z in (.33,1.17):
                cylinder('Drum skin',(0,0,z),.54,.05,self.bone,self.coll,vertices=32)
                ring('Drum rim',(0,0,z),.56,.51,.07,self.gold,self.coll,'Z',32)
            for i in range(8):
                a=i*math.tau/8
                beam('Drum lacing',(.52*math.cos(a),.52*math.sin(a),.38),(.52*math.cos(a+.4),.52*math.sin(a+.4),1.13),.022,.023,self.gold,self.coll)
        elif 'hammer' in ident:
            beam('Hammer haft',(0,0,.22),(0,0,1.55),.11,.11,self.wood,self.coll)
            self.box('Hammer head',(0,0,1.49),(1.11,.4,.44),self.metal)
            self.box('Hammer inlay',(0,-.21,1.49),(.25,.035,.31),self.gold)
        elif any(k in ident for k in ('lantern','resurrect')):
            self.box('Lantern light',(0,0,.83),(.5,.4,.78),self.light)
            for x in (-.28,.28):
                for y in (-.22,.22):beam('Lantern post',(x,y,.38),(x,y,1.3),.045,.045,self.gold,self.coll)
            self.cone('Lantern hood',(0,0,1.45),.44,.13,.34,self.gold,4)
            ring('Lantern loop',(0,0,1.76),.14,.10,.045,self.gold,self.coll,'Y',24)
        elif ident=='food':
            self.sphere('Bread loaf',(-.15,0,.5),(.6,.31,.3),self.wood)
            for x in (-.39,-.13,.13): beam('Loaf scoring',(x,-.18,.74),(x+.17,.16,.74),.025,.025,self.bone,self.coll)
            self.sphere('Red apple',(.45,.19,.73),(.24,.24,.24),self.red)
            beam('Apple stem',(.45,.19,.94),(.49,.19,1.07),.032,.033,self.wood,self.coll)
        elif 'polymorph' in ident:
            self.sphere('Sheep wool',(0,0,.82),(.55,.32,.36),self.bone)
            self.sphere('Sheep head',(.49,-.02,1.01),(.2,.19,.23),self.dark)
            for x in (-.32,.32):
                for y in (-.19,.19): beam('Sheep leg',(x,y,.6),(x,y,.25),.075,.075,self.dark,self.coll)
            self.sphere('Sheep eye',(.59,-.18,1.07),(.027,.019,.028),self.gold)
        elif any(k in ident for k in ('barricade','earthquake','tombstone')):
            for i in range(4): self.box('Runic stone block',((i%2-.5)*.6,(i//2-.5)*.32,.47+(i//2)*.43),(.55,.42,.56),self.metal)
            tube('Glowing fracture',[(-.48,-.38,.8),(-.2,-.39,.55),(.03,-.38,.76),(.24,-.39,.49),(.5,-.38,.67)],.021,self.gem,self.coll)
        elif 'war_cry' in ident:
            tube('War horn',[(-.6,0,.4),(-.38,0,.63),(0,0,.9),(.43,0,1.35)],.17,self.gold,self.coll)
            self.cone('Horn bell',(.43,0,1.41),.16,.37,.25,self.gold,20)
        elif 'ring' in ident or 'signet' in ident:
            ring('Signet ring',(0,0,.87),.54,.38,.17,self.gold,self.coll,'Y',48)
            self.sphere('Signet gemstone',(0,-.035,1.42),(.24,.21,.19),self.gem)
        elif 'wreath' in ident:
            ring('Wreath stem',(0,0,.8),.64,.59,.07,self.wood,self.coll,'Y',40)
            for i in range(16):
                a=i*math.tau/16
                self.sphere('Laurel leaf',(.62*math.cos(a),-.03,.8+.62*math.sin(a)),(.12,.045,.21),self.cloth)
        elif ident=='challenge':
            self.sword()
            self.box('Challenge pennant',(.47,.04,1.05),(.5,.07,.65),self.cloth)
        elif ident=='season_xp':
            self.crown()
            self.crystal(0,0,1.05,.8)
        elif any(k in ident for k in ('fang','wolftooth')):
            self.cone('Fang charm',(0,0,.88),0,.3,1.05,self.bone,8)
            ring('Charm loop',(0,0,1.5),.16,.1,.055,self.gold,self.coll,'Y',24)
        else:
            for i in range(5):
                a=i*math.tau/5
                self.crystal(.36*math.cos(a),.32*math.sin(a),.52,.9+(i%2)*.35)
            ring('Arcane seal',(0,0,.34),.73,.67,.07,self.gold,self.coll,'Z',48)


def render_icon(ident,category,rarity):
    target=ROOT/'assets/ui/icons'/category/(ident+'.png')
    if args.resume and target.exists(): return
    reset();random.seed(ident)
    icon=Icon(ident,category,rarity);icon.build()
    rig=collection('Icon camera and lights')
    scene=bpy.context.scene;setup_render(scene,args.samples)
    scene.camera=camera('Artifact portrait',(3.6,-7,5.8),(0,0,.77),3.2,rig)
    area_light('Warm key',(-3,-4,6),(0,0,.7),480,(1,.81,.58),3,rig)
    area_light('Sky fill',(4,-2,3),(0,0,.7),220,(.6,.79,1),3,rig)
    area_light('Rim light',(0,3,5),(0,0,.7),550,(1,.76,.45),2,rig)
    scene.render.resolution_x=scene.render.resolution_y=512
    target.parent.mkdir(parents=True,exist_ok=True)
    source=SOURCE/'items'/(ident+'.blend');source.parent.mkdir(exist_ok=True)
    scene.render.filepath=str(target)
    scene['asset_id']=ident;scene['asset_category']=category
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(source));bpy.ops.render.render(write_still=True)
    print('ICON_COMPLETE '+category+'/'+ident,flush=True)


def render_particle(ident):
    target=ROOT/'assets/particles'/(ident+'.png')
    if args.resume and target.exists():return
    reset();random.seed(ident)
    coll=collection('Particle source');rig=collection('Particle camera')
    scene=bpy.context.scene;setup_render(scene,16)
    scene.view_settings.view_transform='Standard'
    scene.camera=camera('Particle orthographic',(0,0,5),(0,0,0),2.4,rig)
    scene.render.resolution_x=scene.render.resolution_y=128
    white=material('White tintable particle','ffffff',.7,emission=1)
    if ident in ('particle_soft','particle_smoke','particle_fire','particle_heal','particle_arcane','particle_trail'):
        mat=bpy.data.materials.new('Soft particle shader');mat.use_nodes=True
        n=mat.node_tree.nodes;l=mat.node_tree.links;n.clear()
        tex=n.new('ShaderNodeTexCoord')
        sub=n.new('ShaderNodeVectorMath');sub.operation='SUBTRACT';sub.inputs[1].default_value=(.5,.5,0)
        l.new(tex.outputs['UV'],sub.inputs[0])
        length=n.new('ShaderNodeVectorMath');length.operation='LENGTH';l.new(sub.outputs[0],length.inputs[0])
        ramp=n.new('ShaderNodeMapRange');ramp.clamp=True
        ramp.inputs['From Min'].default_value=.02;ramp.inputs['From Max'].default_value=.49
        ramp.inputs['To Min'].default_value=1;ramp.inputs['To Max'].default_value=0
        l.new(length.outputs['Value'],ramp.inputs['Value'])
        alpha=ramp.outputs['Result']
        if ident in ('particle_smoke','particle_fire'):
            noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=7;noise.inputs['Detail'].default_value=4
            l.new(tex.outputs['UV'],noise.inputs['Vector'])
            mult=n.new('ShaderNodeMath');mult.operation='MULTIPLY';l.new(alpha,mult.inputs[0]);l.new(noise.outputs['Fac'],mult.inputs[1]);alpha=mult.outputs[0]
        emission=n.new('ShaderNodeEmission');emission.inputs[0].default_value=(1,1,1,1)
        transparent=n.new('ShaderNodeBsdfTransparent');mix=n.new('ShaderNodeMixShader')
        l.new(alpha,mix.inputs[0]);l.new(transparent.outputs[0],mix.inputs[1]);l.new(emission.outputs[0],mix.inputs[2])
        out=n.new('ShaderNodeOutputMaterial');l.new(mix.outputs[0],out.inputs['Surface'])
        bpy.ops.mesh.primitive_plane_add(size=2)
        finish(bpy.context.object,'Soft emissive sprite',mat,coll)
    elif ident=='particle_lightning':
        tube('Forked lightning',[(-.35,.85,0),(.1,.29,0),(-.2,.12,0),(.35,-.78,0)],.055,white,coll)
        tube('Lightning branch',[(.1,.29,0),(.55,.4,0),(.78,.03,0)],.035,white,coll)
    elif ident=='particle_frost':
        for i in range(6):
            a=i*math.tau/6
            tube('Snowflake arm',[(0,0,0),(.82*math.cos(a),.82*math.sin(a),0)],.033,white,coll)
            for side in (-1,1):
                tube('Crystal fork',[(.48*math.cos(a),.48*math.sin(a),0),(.72*math.cos(a+side*.25),.72*math.sin(a+side*.25),0)],.026,white,coll)
    elif ident=='particle_stone':
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.67)
        finish(bpy.context.object,'Stone fragment',white,coll)
    else:
        verts=[(0,.86,0),(.15,.1,0),(.72,0,0),(.15,-.1,0),(0,-.86,0),(-.15,-.1,0),(-.72,0,0),(-.15,.1,0)]
        mesh('Spark star',verts,[tuple(range(8))],white,coll)
        if ident=='particle_deploy':ring('Deployment circle',(0,0,0),.83,.77,.03,white,coll,'Z',48)
    target.parent.mkdir(parents=True,exist_ok=True)
    scene.render.filepath=str(target)
    source=SOURCE/'particles'/(ident+'.blend');source.parent.mkdir(exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(source));bpy.ops.render.render(write_still=True)
    print('PARTICLE_COMPLETE '+ident,flush=True)


if args.category in ('all','icons'):
    jobs=[(r['Id'],'relics',r['Rarity']) for r in json.loads((ROOT/'data/equipment.json').read_text())['Equipment']]
    jobs += [(s['Id'],'spells','rare') for s in json.loads((ROOT/'data/spells.json').read_text())['Spells']]
    jobs += [(i,'rewards','legendary') for i in catalog('RewardIconIds')]
    jobs += [(i,'meta','rare') for i in catalog('MetaIconIds')]
    for ident,category,rarity in jobs:
        if args.ids=='all' or ident in args.ids.split(','):render_icon(ident,category,rarity)
if args.category in ('all','particles'):
    for ident in catalog('ParticleTextureIds'):
        if args.ids=='all' or ident in args.ids.split(','):render_particle(ident)
