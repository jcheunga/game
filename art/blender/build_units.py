"""Author articulated Crownroad characters and render six animation states.

Blender --background --factory-startup --python art/blender/build_units.py --
  --ids player_brawler,player_shooter,enemy_walker --samples 24
"""
import argparse
import json
import math
from pathlib import Path
import random
import sys

import bpy
from mathutils import Vector

SOURCE = Path(__file__).resolve().parent
ROOT = SOURCE.parent.parent
sys.path.insert(0, str(SOURCE))
from crownroad_art import (area_light, beam, box, camera, collection, cylinder,
                          finish, material, mesh, ring, setup_render, tube)

STATES = [('idle', 4, .16, True), ('walk', 6, .10, True), ('attack', 6, .075, False),
          ('hit', 2, .09, False), ('death', 6, .11, False), ('deploy', 4, .10, False)]
parser = argparse.ArgumentParser()
parser.add_argument('--ids', default='all')
parser.add_argument('--samples', type=int, default=24)
parser.add_argument('--frame-size', type=int, default=256)
parser.add_argument('--resume', action='store_true')
parser.add_argument('--portrait-only', action='store_true')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
units = json.loads((ROOT / 'data/units.json').read_text())['Units']
if args.ids != 'all':
    ids = args.ids.split(',')
    units = [u for u in units if u['Id'] in ids]


def clear():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for col in list(bpy.data.collections):
        bpy.data.collections.remove(col)
    for blocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights, bpy.data.actions):
        for block in list(blocks):
            if not block.users:
                blocks.remove(block)


def ellipsoid(name, loc, size, mat, coll, segments=20):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=12, radius=1, location=loc)
    obj = bpy.context.object
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, mat, coll, smooth=True)


def cone(name, loc, r1, r2, depth, mat, coll, vertices=12):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r1, radius2=r2, depth=depth, location=loc)
    return finish(bpy.context.object, name, mat, coll, .012)


class Character:
    def __init__(self, spec):
        self.spec = spec
        self.id = spec['Id']
        self.name = spec['DisplayName']
        self.cls = spec['VisualClass']
        self.enemy = spec['Side'] == 'Enemy'
        self.boss = self.cls == 'boss'
        self.coll = collection(self.name + ' • articulated model')
        self.joints = {}
        self.root = self.joint('ROOT', (0, 0, 0))
        self.tint = self.choose_tint()
        self.metal = material('Tempered armor', '26363f' if not self.enemy else '24272c', .57, .65, '3d5059')
        self.edge = material('Armor edges', '94a29e', .36, .8)
        self.gold = material('Antique brass', 'a38043', .43, .68)
        self.fabric = material('Faction cloth', self.tint, .88, variation=self.tint)
        self.leather = material('Worked leather', '33251b', .8, variation='684b31')
        self.wood = material('Ash wood', '493422', .75, variation='937144', grain=(3, 3, 18))
        self.dark = material('Deep recesses', '111a20', .8)
        skin = 'b28e70' if not self.enemy else '819079'
        if self.cls in ('bloater',) or 'plague' in self.id:
            skin = '66734e'
        if 'mire' in self.id:
            skin = '384e3d'
        self.skin = material('Skin', skin, .88, variation=skin)
        self.bone = material('Old ivory', '968d70', .83, variation='c8bfa0')
        self.glow = material('Soul light', '81dab2' if self.enemy else 'ffc779', .5, emission=1.7)
        self.machine = self.cls in ('siegetower', 'splitter') or 'ballista' in self.id or 'engine' in self.id
        self.beast = self.cls == 'hound'
        self.mounted = self.id in ('player_raider', 'enemy_boss_steppe')
        self.monster = self.cls in ('brute', 'crusher', 'bloater', 'runner') or 'mire' in self.id
        if self.machine:
            self.build_machine()
        elif self.beast:
            self.build_beast()
        else:
            if self.mounted:
                self.build_beast(horse=True)
            self.build_humanoid()
        bpy.context.view_layer.update()
        self.rest = {k: (o.location.copy(), o.rotation_euler.copy(), o.scale.copy()) for k, o in self.joints.items()}

    def choose_tint(self):
        if not self.enemy:
            return {'player_marksman':'335b78', 'player_stormcaller':'4a567e', 'player_coordinator':'ac8546',
                    'player_necromancer':'4b665c', 'player_berserker':'974d34', 'player_rogue':'374853',
                    'player_grenadier':'57734b'}.get(self.id, '1f6660')
        if 'docks' in self.id or 'tidemaster' in self.id: return '2b6976'
        if 'forge' in self.id or 'ashen' in self.id: return '8b4935'
        if 'basilica' in self.id or 'reliquary' in self.id: return 'b7a780'
        if any(x in self.id for x in ('plague','ward','mire','verge')): return '596846'
        if 'mirror' in self.id: return '53578d'
        return '642f40'

    def joint(self, name, loc, parent=None):
        obj = bpy.data.objects.new(name, None)
        self.coll.objects.link(obj)
        obj.empty_display_type = 'PLAIN_AXES'
        obj.empty_display_size = .1
        obj.location = loc
        if parent is not None:
            obj.parent = parent
        self.joints[name] = obj
        return obj

    def bind(self, obj, joint):
        # Geometry is authored in world-space, then attached to the joint in rest pose.
        bpy.context.view_layer.update()
        matrix = obj.matrix_world.copy()
        obj.parent = joint
        obj.matrix_world = matrix
        return obj

    def sphere(self, name, loc, size, mat, joint=None):
        return self.bind(ellipsoid(name, loc, size, mat, self.coll), joint or self.root)

    def box(self, name, loc, size, mat, joint=None, bevel=.02):
        return self.bind(box(name, loc, size, mat, self.coll, bevel), joint or self.root)

    def beam(self, name, a, b, w, d, mat, joint=None):
        return self.bind(beam(name, a, b, w, d, mat, self.coll), joint or self.root)

    def curve(self, name, points, radius, mat, joint=None, cyclic=False):
        return self.bind(tube(name, points, radius, mat, self.coll, cyclic), joint or self.root)

    def build_humanoid(self):
        zoff = .82 if self.mounted else 0
        bulk = 1.32 if self.monster else 1.0
        if self.boss: bulk = 1.12
        torso = self.joint('Spine', (0, 0, .86 + zoff), self.root)
        head = self.joint('Head', (.035, 0, .85), torso)
        robe = any(x in self.name.lower() for x in ('mage','monk','witch','lich','pontiff','necromancer','caster','hexer','stormcaller','tidecaller','archon'))
        hood = robe or self.id in ('player_shooter', 'player_ranger', 'player_rogue', 'enemy_saboteur')
        undead = self.enemy and not self.monster
        chestmat = self.skin if self.monster else self.fabric if robe else self.metal
        self.sphere('Broad chest', (0,0,1.22+zoff), (.24*bulk,.31*bulk,.39), chestmat, torso)
        self.sphere('Waist', (0,0,.9+zoff), (.21*bulk,.255*bulk,.18), self.leather, torso)
        if robe:
            obj = cone('Heavy split robe', (0,0,.67+zoff), .34*bulk, .235*bulk, .78, self.fabric, self.coll)
            self.bind(obj, torso)
            for sy in (-1,1):
                self.beam('Robe gold piping', (.27,sy*.17,.36+zoff), (.24,sy*.14,1.18+zoff), .025,.027,self.gold,torso)
        else:
            for y in (-.2,0,.2):
                self.box('Hanging lamellar tasset', (.22,y,.76+zoff), (.075,.17,.29),self.metal,torso)
            self.box('Teal front tabard', (.252,0,1.11+zoff), (.022,.22,.68),self.fabric,torso,.006)
            self.box('Tabard gold crossbar', (.271,0,1.35+zoff), (.018,.225,.026),self.gold,torso,.004)
        for sy in (-1,1):
            side='Near' if sy<0 else 'Far'
            hip=self.joint(side+' hip',(0,sy*.17,0),torso)
            knee=self.joint(side+' knee',(.025,0,-.4),hip)
            self.beam('Trouser thigh',(0,sy*.17,.88+zoff),(.025,sy*.17,.46+zoff),.20*bulk,.21*bulk,self.leather,hip)
            self.sphere('Knee armor',(.05,sy*.17,.46+zoff),(.135,.14,.13),self.metal if not self.monster else self.skin,knee)
            self.beam('Lower leg',(.025,sy*.17,.46+zoff),(.02,sy*.17,.13+zoff),.16,.175,self.skin if self.monster else self.metal,knee)
            self.box('Toe-forward boot',(.13,sy*.17,.09+zoff),(.39,.22,.17),self.leather,knee,.045)
            shoulder=self.joint(side+' shoulder',(.015,sy*.34,.62),torso)
            elbow=self.joint(side+' elbow',(.055,0,-.32),shoulder)
            hand=self.joint(side+' hand',(.17,0,-.24),elbow)
            self.box('Faceted pauldron',(.015,sy*.35,1.48+zoff),(.32*bulk,.34*bulk,.23),chestmat,shoulder,.065)
            self.beam('Upper arm',(.015,sy*.35,1.46+zoff),(.07,sy*.35,1.16+zoff),.17*bulk,.19*bulk,self.skin if self.monster else self.fabric,shoulder)
            self.sphere('Elbow joint',(.07,sy*.35,1.16+zoff),(.115,.12,.115),self.leather,elbow)
            self.beam('Forearm vambrace',(.07,sy*.35,1.16+zoff),(.24,sy*.35,.93+zoff),.15*bulk,.17*bulk,self.skin if self.monster else self.metal,elbow)
            self.sphere('Gloved hand',(.24,sy*.35,.92+zoff),(.075,.085,.095),self.skin if self.monster else self.leather,hand)
            if self.monster:
                for j in range(3):
                    self.beam('Long curved claw',(.29,sy*.35+(j-1)*.055,.9+zoff),(.45,sy*.35+(j-1)*.055,.78+zoff),.029,.026,self.bone,hand)
        headz=1.73+zoff
        self.sphere('Head',(0.055,0,headz),(.205,.2,.255),self.bone if undead else self.skin,head)
        self.sphere('Jaw',(.085,0,headz-.15),(.15,.16,.105),self.bone if undead else self.skin,head)
        self.sphere('Nose',(.25,0,headz-.015),(.07,.055,.085),self.bone if undead else self.skin,head)
        for sy in (-1,1):
            self.sphere('Eye socket',(.225,sy*.105,headz+.035),(.022,.037,.023),self.dark,head)
            self.sphere('Eye',(.249,sy*.108,headz+.038),(.011,.021,.012),self.glow if undead else self.dark,head)
            self.beam('Brow',(.235,sy*.062,headz+.09),(.19,sy*.168,headz+.09),.025,.035,self.bone if undead else self.skin,head)
        if undead:
            for y in (-.105,-.06,-.015,.03,.075):
                self.box('Exposed tooth',(.229,y,headz-.12),(.035,.031,.05),self.bone,head,.004)
        if hood:
            self.sphere('Hood crown',(-.055,0,headz+.115),(.23,.237,.21),self.fabric,head)
            for sy in (-1,1):
                self.sphere('Hood side fold',(-.03,sy*.193,headz-.02),(.195,.075,.24),self.fabric,head)
        elif not self.monster:
            self.sphere('Helmet crown',(-.01,0,headz+.12),(.235,.235,.19),self.metal,head)
            self.box('Helmet nasal guard',(.262,0,headz+.015),(.04,.034,.22),self.gold,head,.01)
            for sy in (-1,1):
                self.box('Helmet cheek plate',(.055,sy*.202,headz-.09),(.21,.044,.24),self.metal,head)
            self.curve('Helmet brow band',[(.235*math.cos(a),.242*math.sin(a),headz+.11) for a in [i*math.tau/24 for i in range(25)]],.018,self.gold,head)
            if not undead:
                self.box('Closed steel visor',(.29,0,headz-.025),(.1,.34,.23),self.metal,head,.025)
                self.box('Visor sight slit',(.346,0,headz+.042),(.006,.265,.025),self.dark,head,.002)
                for yy in (-.095,-.05,0,.05,.095):
                    self.box('Visor breathing slot',(.346,yy,headz-.075),(.007,.014,.047),self.dark,head,.002)
        else:
            for y in (-.15,.15):
                self.curve('Cranial horns',[(-.08,y,headz+.16),(-.13,y*1.8,headz+.32),(-.06,y*2.1,headz+.49)],.07,self.bone,head)
        if self.boss or self.id in ('player_banner','player_defender','enemy_mirror','enemy_revenant_captain'):
            self.cape(torso,zoff)
        if self.boss:
            if 'basilica' in self.id:
                self.bind(cone('Pontiff ivory mitre',(.03,0,headz+.42),.24,.025,.7,self.bone,self.coll,4),head)
            else:
                for i in range(7):
                    a=i*math.tau/7
                    self.bind(cone('Royal crown point',(.02+.21*math.cos(a),.21*math.sin(a),headz+.36),.055,0,.28,self.gold,self.coll,5),head)
            if any(x in self.id for x in ('pass','verge')):
                for sy in (-1,1):
                    self.curve('Branching antler',[(-.05,sy*.16,headz+.2),(-.15,sy*.32,headz+.48),(-.02,sy*.46,headz+.7)],.042,self.wood,head)
            if 'ward' in self.id or 'plague' in self.id:
                for i in range(7):
                    self.sphere('Plague growth',(.17,random.uniform(-.3,.3),random.uniform(1.05,1.45)+zoff),(.09,.08,.08),self.glow,torso)
        weapon=self.weapon_kind()
        self.weapon(weapon,self.joints['Near hand'],(.24,-.35,.92+zoff))
        if self.cls in ('shield','mirror') or self.id in ('player_brawler','enemy_boss_citadel'):
            self.shield(self.joints['Far hand'],(.32,.38,1.08+zoff))
        elif self.id=='player_grenadier':
            self.sphere('Potion satchel',(-.08,-.33,.85),(.16,.1,.2),self.leather,torso)
            for y in (-.12,.12): self.sphere('Belt flask',(.26,y,.91),(.07,.07,.095),self.glow,torso)
        if self.id in ('player_banner','enemy_howler','enemy_revenant_captain'):
            self.banner(self.joints['Far hand'],zoff)
        if self.cls=='bloater':
            self.sphere('Diseased swollen belly',(.12,0,1.07),(.4,.39,.45),self.skin,torso)
            for i in range(10):
                self.sphere('Rot pustule',(.31,random.uniform(-.25,.25),random.uniform(.85,1.35)),(.06,.075,.07),self.bone,torso)
        head.scale=(.82,.82,.82)

    def cape(self,joint,zoff):
        verts=[]
        for j in range(7):
            t=j/6
            for i in range(9):
                u=i/8
                verts.append((-.23-.16*t-.045*math.sin(u*math.pi*6), (u-.5)*(.52+.24*t), 1.51-t*1.13+zoff))
        faces=[(j*9+i,j*9+i+1,(j+1)*9+i+1,(j+1)*9+i) for j in range(6) for i in range(8)]
        self.bind(mesh('Pleated traveling cape',verts,faces,self.fabric,self.coll,smooth=True),joint)

    def weapon_kind(self):
        name=self.name.lower()
        if 'archer' in name: return 'bow'
        if 'crossbow' in name: return 'crossbow'
        if any(x in name for x in ('spear','rider','warlord')): return 'spear'
        if any(x in name for x in ('halberd','berserker','chieftain')): return 'axe'
        if 'engineer' in name: return 'hammer'
        if 'alchemist' in name or 'sapper' in name: return 'flask'
        if any(x in name for x in ('mage','monk','witch','lich','pontiff','necromancer','caster','hexer','stormcaller','tidecaller','archon','herald')): return 'staff'
        if self.monster: return 'club' if self.cls in ('crusher','brute') else 'claws'
        return 'sword'

    def weapon(self,kind,joint,p):
        x,y,z=p
        if kind=='claws': return
        if kind=='bow':
            self.curve('Recurve bow',[(x+.24+.17*math.sin(t*math.pi),y,z-.35+t*.95) for t in [i/20 for i in range(21)]],.029,self.wood,joint)
            self.curve('Bow string',[(x+.24,y,z-.35),(x+.13,y,z+.1),(x+.24,y,z+.6)],.006,self.bone,joint)
            self.beam('Nocked arrow',(x-.1,y,z+.1),(x+.71,y,z+.1),.012,.012,self.wood,joint)
        elif kind=='crossbow':
            self.beam('Crossbow stock',(x-.12,y,z),(x+.58,y,z+.12),.07,.075,self.wood,joint)
            self.curve('Crossbow limbs',[(x+.4,y-.38,z+.12),(x+.53,y,z+.12),(x+.4,y+.38,z+.12)],.035,self.metal,joint)
            self.curve('Crossbow string',[(x+.4,y-.38,z+.12),(x+.12,y,z+.06),(x+.4,y+.38,z+.12)],.007,self.bone,joint)
        elif kind in ('staff','spear','axe','club','hammer'):
            height={'staff':1.45,'spear':1.65,'axe':1.05,'club':.85,'hammer':.7}[kind]
            self.beam('Weapon shaft',(x,y,z-.55),(x,y,z+height),.045 if kind!='club' else .105,.048,self.wood,joint)
            if kind=='staff':
                self.sphere('Staff crystal',(x,y,z+height),(.115,.11,.17),self.glow,joint)
                for sy in (-1,1):
                    self.curve('Staff fork',[(x,y,z+height-.2),(x, y+sy*.16,z+height),(x,y+sy*.11,z+height+.16)],.029,self.gold,joint)
            elif kind=='spear':
                self.bind(cone('Spearhead',(x,y,z+height+.12),.083,0,.35,self.edge,self.coll,4),joint)
            elif kind=='axe':
                verts=[(x,y-.025,z+height-.25),(x+.33,y-.025,z+height-.27),(x+.4,y-.025,z+height+.08),(x,y-.025,z+height+.13)]
                self.bind(mesh('Crescent axe blade',verts,[(0,1,2,3)],self.edge,self.coll),joint)
                self.beam('Axe spine',(x,y,z+height-.25),(x,y,z+height+.17),.075,.07,self.metal,joint)
            else:
                self.box('Weapon striking head',(x,y,z+height),(.36,.25,.31),self.metal if kind=='hammer' else self.bone,joint,.055)
        elif kind=='flask':
            self.sphere('Held volatile flask',(x+.12,y,z+.05),(.13,.12,.16),self.glow,joint)
            self.box('Bottle stopper',(x+.12,y,z+.23),(.075,.07,.09),self.wood,joint)
        else:
            self.beam('Sword leather grip',(x,y,z-.12),(x,y,z+.13),.07,.064,self.leather,joint)
            self.box('Sword quillon',(x,y,z+.15),(.11,.36,.045),self.gold,joint)
            verts=[(x-.035,y-.065,z+.17),(x+.035,y-.065,z+.17),(x+.035,y+.065,z+.17),
                   (x-.035,y+.065,z+.17),(x,y,z+1.05)]
            self.bind(mesh('Diamond section sword',verts,[(0,1,4),(1,2,4),(2,3,4),(3,0,4)],self.edge,self.coll),joint)
            self.sphere('Sword pommel',(x,y,z-.16),(.065,.065,.065),self.gold,joint)

    def shield(self,joint,p):
        x,y,z=p
        outline=[(-.28,.36),(.28,.36),(.3,-.07),(.17,-.31),(0,-.44),(-.17,-.31),(-.3,-.07)]
        # Shield sits on the forward side, visible along its near-facing face.
        coords=[(x+.075,y+a,z+b) for a,b in outline]
        self.bind(mesh('Kite shield face',coords,[tuple(range(7))],self.fabric,self.coll),joint)
        self.curve('Shield metal rim',coords,.028,self.gold,joint,True)
        self.beam('Shield central spine',(x+.09,y,z-.33),(x+.09,y,z+.27),.034,.045,self.gold,joint)
        self.beam('Shield crossbar',(x+.09,y-.18,z+.06),(x+.09,y+.18,z+.06),.029,.03,self.gold,joint)

    def banner(self,joint,zoff):
        x,y,z=.18,.35,.93+zoff
        self.beam('Standard pole',(x,y,z-.7),(x,y,z+1.85),.035,.038,self.wood,joint)
        verts=[(x+u*1.0,y+.05*math.sin(u*7),z+1.7-v*.55-.12*u) for v in (0,1) for u in (0,.25,.5,.75,1)]
        self.bind(mesh('Warband standard',verts,[(i,i+1,i+6,i+5) for i in range(4)],self.fabric,self.coll,smooth=True),joint)
        self.curve('Standard gold hem',verts[:5],.016,self.gold,joint)

    def build_beast(self,horse=False):
        torso=self.joint('Beast torso',(0,0,0),self.root)
        z=.92 if horse else .51
        fur=self.leather if horse else self.fabric
        self.sphere('Animal barrel',(0,0,z),(.73 if horse else .55,.25,.34 if horse else .24),fur,torso)
        self.beam('Rising animal neck',(.45,0,z),(.75,0,z+.45),.29,.31,fur,torso)
        self.sphere('Animal head',(.86,0,z+.42),(.28,.17,.2),fur,torso)
        self.sphere('Long muzzle',(1.03,0,z+.34),(.23,.145,.115),self.leather,torso)
        for sy in (-1,1):
            self.sphere('Animal eye',(.89,sy*.15,z+.49),(.027,.022,.028),self.dark,torso)
            self.bind(cone('Pointed ear',(.73,sy*.135,z+.68),.075,0,.23,fur,self.coll,4),torso)
            for sx in (-1,1):
                j=self.joint(f'Beast leg {sx} {sy}',(sx*.43,sy*.19,z-.12),torso)
                self.beam('Animal upper leg',(sx*.43,sy*.19,z-.12),(sx*.4,sy*.21,z*.42),.115,.12,fur,j)
                self.beam('Animal shin',(sx*.4,sy*.21,z*.42),(sx*.47,sy*.21,.09),.075,.09,fur,j)
                self.box('Hoof or paw',(sx*.48+.04,sy*.21,.065),(.2,.13,.13),self.dark,j)
        self.curve('Sweeping tail',[(-.62,0,z+.1),(-.85,0,z+.04),(-1.02,0,z-.22)],.09,fur,torso)
        if horse:
            self.box('Quilted saddle',(0,0,z+.29),(.56,.65,.09),self.fabric,torso)
            self.curve('Bridle',[(1.02,-.16,z+.36),(.8,-.19,z+.53),(.62,-.17,z+.35)],.018,self.gold,torso)
        else:
            self.sphere('Hound armored back',(-.05,0,z+.13),(.42,.29,.16),self.metal,torso)
            self.curve('Hound collar',[(.43,.27*math.cos(a),z+.15+.26*math.sin(a)) for a in [i*math.tau/20 for i in range(21)]],.038,self.leather,torso)

    def build_machine(self):
        tower=self.cls=='siegetower'
        nest=self.cls=='splitter'
        if nest:
            self.sphere('Ossuary root mound',(0,0,.4),(.67,.5,.4),self.leather)
            for i in range(13):
                a=i*math.tau/13
                self.sphere('Ossuary skull',(.5*math.cos(a),.37*math.sin(a),.5+random.random()*.45),(.15,.13,.19),self.bone)
                self.curve('Nest rib',[ (.5*math.cos(a),.4*math.sin(a),.15),(.68*math.cos(a),.55*math.sin(a),.63),(.42*math.cos(a),.3*math.sin(a),1.1)],.047,self.bone)
            self.sphere('Ossuary heart',(0,0,.66),(.25,.23,.32),self.glow)
            return
        self.box('Siege chassis',(0,0,.45),(1.6,.88,.23),self.wood)
        for x in (-.55,.55):
            for y in (-.51,.51):
                wheel=self.joint(f'Machine wheel {x} {y}',(x,y,.31),self.root)
                self.bind(cylinder('Siege wheel',(x,y,.31),.3,.12,self.wood,self.coll,'Y',16),wheel)
                self.bind(ring('Siege iron tire',(x,y,.31),.32,.275,.14,self.metal,self.coll),wheel)
                self.bind(cylinder('Siege hub',(x,y*1.08,.31),.085,.17,self.gold,self.coll,'Y'),wheel)
        if tower:
            for x in (-.61,.61):
                for y in (-.36,.36):
                    self.beam('Tower upright',(x,y,.48),(x*.7,y*.7,2.1),.12,.12,self.wood)
            for z in (.9,1.5,2.02):
                self.box('Tower platform',(0,0,z),(1.4,.94,.11),self.wood)
            for y in (-.46,.46):
                self.box('Tower battlement',(0,y,2.24),(1.41,.13,.4),self.bone)
                for x in (-.58,0,.58): self.box('Tower merlon',(x,y,2.53),(.24,.2,.23),self.bone)
            self.box('Tower hanging banner',(.71,0,1.69),(.018,.6,.73),self.fabric)
        else:
            mount=self.joint('Weapon mount',(0,0,.76),self.root)
            self.box('Weapon mounting block',(0,0,.72),(.37,.35,.52),self.metal,mount)
            if 'engine' in self.id:
                gun=cylinder('Plague bombard',(.2,0,1.02),.27,1.3,self.metal,self.coll,'X',16)
                self.bind(gun,mount)
                self.bind(ring('Bombard rim',(0,0,0),.3,.2,.1,self.bone,self.coll,'Z'),mount)
                self.sphere('Rot chamber',(-.42,0,1.02),(.35,.33,.33),self.glow,mount)
            else:
                self.beam('Ballista stock',(-.7,0,.95),(.88,0,1.11),.14,.17,self.wood,mount)
                self.curve('Ballista arms',[(.6,-.83,1.03),(.83,0,1.1),(.6,.83,1.03)],.085,self.bone if self.enemy else self.metal,mount)
                self.curve('Ballista cable',[(.6,-.83,1.03),(-.3,0,1),(.6,.83,1.03)],.012,self.leather,mount)
                self.beam('Siege bolt',(-.49,0,1.14),(1.11,0,1.25),.027,.027,self.wood,mount)

    def pose(self,state,t):
        for key,obj in self.joints.items():
            loc,rot,scale=self.rest[key]
            obj.location, obj.rotation_euler, obj.scale=loc.copy(),rot.copy(),scale.copy()
        phase=math.tau*t
        if state=='idle':
            self.root.location.z=.012*math.sin(phase)
            if 'Spine' in self.joints: self.joints['Spine'].rotation_euler.y=.02*math.sin(phase)
        elif state=='walk':
            self.root.location.z=.025*(1-math.cos(phase*2))
            for side,sign in (('Near',1),('Far',-1)):
                if side+' hip' in self.joints:
                    self.joints[side+' hip'].rotation_euler.y=sign*.52*math.sin(phase)
                    self.joints[side+' knee'].rotation_euler.y=max(0,-sign*math.sin(phase))*.62
                    self.joints[side+' shoulder'].rotation_euler.y=-sign*.20*math.sin(phase)
            for key,obj in self.joints.items():
                if key.startswith('Beast leg'):
                    sign=1 if key.endswith('-1 -1') or key.endswith('1 1') else -1
                    obj.rotation_euler.y=sign*.45*math.sin(phase)
                if key.startswith('Machine wheel'): obj.rotation_euler.y=phase
        elif state=='attack':
            swing=math.sin(math.pi*t)
            if 'Near shoulder' in self.joints:
                kind=self.weapon_kind()
                self.joints['Near shoulder'].rotation_euler.y=(-1.25 if kind in ('sword','axe','club','hammer') else -.65)*swing
                self.joints['Near elbow'].rotation_euler.y=-.4*swing
                self.joints['Spine'].rotation_euler.y=.16*swing
            if 'Weapon mount' in self.joints: self.joints['Weapon mount'].location.x-=.16*swing
            self.root.location.x=.12*swing
            if self.beast: self.root.rotation_euler.y=-.16*swing
        elif state=='hit':
            self.root.rotation_euler.y=-.14*math.sin(math.pi*(t*.7+.2))
            self.root.location.x=-.08
        elif state=='death':
            # Ease the whole figure onto its back, then settle.
            amount=math.sin(t*math.pi/2)
            self.root.rotation_euler.y=-1.42*amount
            self.root.location.z=.14*amount
            self.root.location.x=1.05*amount
        elif state=='deploy':
            amount=math.sin(t*math.pi/2)
            self.root.scale=Vector((.72+.28*amount,)*3)
            self.root.location.z=.12*math.sin(t*math.pi)
        if self.mounted:
            for side in ('Near','Far'):
                self.joints[side+' hip'].rotation_euler.y=-.6
                self.joints[side+' knee'].rotation_euler.y=1.05
        bpy.context.view_layer.update()


for spec in units:
    ident=spec['Id']
    target=ROOT/'artifacts/blender/units'/ident
    complete=target/'complete.json'
    if args.resume and complete.exists():
        print('UNIT_SKIP '+ident,flush=True)
        continue
    clear()
    random.seed(ident)
    char=Character(spec)
    rig=collection('Camera and lighting')
    scene=bpy.context.scene
    setup_render(scene,args.samples)
    scene.render.resolution_x=args.frame_size
    scene.render.resolution_y=args.frame_size*5//4
    scene.render.fps=12
    # Fixed framing includes weapons in wind-up and a horizontal death pose.
    tall_weapon=not char.machine and not char.beast and char.weapon_kind() in ('staff','spear')
    scale=4.8 if char.mounted else 4.6 if char.boss or char.id=='player_banner' or tall_weapon else 4.1
    cam=camera('Battle camera • facing right',(5.5,-20,6.7),(.18,0,1.15 if char.machine or char.beast else 1.28),scale,rig)
    scene.camera=cam
    # Fit the complete animation envelope, including long spears and falling cavalry.
    from bpy_extras.object_utils import world_to_camera_view
    envelope=1.0
    for state,count,_,loop in STATES:
        for i in range(count):
            char.pose(state,i/count if loop else i/max(1,count-1))
            for obj in char.coll.objects:
                if obj.type not in ('MESH','CURVE'): continue
                for corner in obj.bound_box:
                    p=world_to_camera_view(scene,cam,obj.matrix_world @ Vector(corner))
                    envelope=max(envelope,abs(p.x-.5)*2/.92,abs(p.y-.5)*2/.92)
    cam.data.ortho_scale*=envelope
    char.pose('idle',0)
    area_light('Warm key',(-3,-5,7),(0,0,1),600,(1,.83,.65),4,rig)
    area_light('Cool fill',(4,-2,4),(0,0,1),350,(.63,.8,1),3,rig)
    area_light('Golden rim',(-1,4,5),(0,0,1),750,(1,.78,.46),3,rig)
    target.mkdir(parents=True,exist_ok=True)
    source_dir=SOURCE/'units'
    source_dir.mkdir(exist_ok=True)
    frame=1
    metadata={'frameWidth':args.frame_size,'frameHeight':args.frame_size*5//4,
              'drawScale':(2.0*scale/3.5 if not char.mounted else 2.8)*envelope,'animations':{},'assetId':ident,
              'title':char.name,'source':f'art/blender/units/{ident}.blend'}
    from bpy_extras.object_utils import world_to_camera_view
    bpy.context.view_layer.update()
    metadata['anchorY']=1-world_to_camera_view(scene,cam,Vector((0,0,0))).y
    for state,count,duration,loop in STATES:
        start=frame-1
        metadata['animations'][state]={'start':start,'count':count,'duration':duration,'loop':loop}
        scene.timeline_markers.new(state,frame=frame)
        for i in range(count):
            t=i/count if loop else i/max(1,count-1)
            char.pose(state,t)
            for obj in char.joints.values():
                for prop in ('location','rotation_euler','scale'):
                    obj.keyframe_insert(data_path=prop,frame=frame,group=state)
            frame+=1
    scene.frame_start,scene.frame_end=1,frame-1
    scene['asset_id']=ident
    scene['animations']=json.dumps(metadata['animations'])
    scene['notes']='Rigid articulated rig: move named joints; geometry is retained as editable parts.'
    bpy.context.preferences.filepaths.save_version=0
    scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=str(source_dir/(ident+'.blend')))
    if not args.portrait_only:
        for f in range(1,frame):
            scene.frame_set(f)
            scene.render.filepath=str(target/f'{f-1:03}.png')
            bpy.ops.render.render(write_still=True)
    scene.frame_set(1)
    # Portrait is a new render of the source, not an enlarged sprite crop.
    portrait_target=1.5 if char.mounted or char.cls=='siegetower' else 1.45 if not char.machine and not char.beast else .7
    portrait_scale=4.2 if char.mounted else 3.7 if char.cls=='siegetower' else 2.0 if not char.machine and not char.beast else 2.8
    portrait_camera=camera('Portrait camera',(5,-8,4),(0,0,portrait_target),portrait_scale,rig)
    scene.camera=portrait_camera
    scene.render.resolution_x=scene.render.resolution_y=512
    scene.render.filepath=str(target/'portrait.png')
    bpy.ops.render.render(write_still=True)
    (target/'metadata.json').write_text(json.dumps(metadata,indent=2)+'\n')
    complete.write_text(json.dumps({'id':ident,'frames':frame-1,'source':metadata['source']},indent=2)+'\n')
    print('UNIT_COMPLETE '+ident,flush=True)
