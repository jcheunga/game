"""Editable medieval environment kit shared by battlefields, maps, and structures."""
import math
import random
import bpy
from mathutils import Vector
from crownroad_art import box, beam, cylinder, material, mesh, tube, finish, ring


class WorldKit:
    def __init__(self,coll,palette='city'):
        self.coll=coll
        self.palette=palette
        self.stone=material('Weathered limestone','444e50',.91,variation='747b73')
        self.stone_light=material('Worn stone edges','78847e',.91)
        self.stone_dark=material('Damp stone','303c40',.98)
        self.wood=material('Rough oak','392c20',.86,variation='70573a',grain=(2,2,18))
        self.iron=material('Forged iron','273238',.52,.6)
        self.brass=material('Aged brass','9d793a',.47,.6)
        self.roof=material('Slate roof','253e45',.94,variation='445b5a')
        self.cloth=material('Banners','244d49' if palette not in ('citadel','basilica') else '622f3a',.9)
        self.bone=material('Old ivory','a09a7f',.85)
        self.dark=material('Deep archways','131f23',1)
        self.green=material('Ghostfire','65dbb3',.5,emission=2)
        self.fire=material('Embers','ff9b38',.4,emission=2.5)
        ground={'harbor':('344a48','77817a'),'foundry':('353536','5b4b3d'),'quarantine':('3d4840','727965'),
                'thornwall':('495352','7e8882'),'basilica':('3a4447','6d7774'),'mire':('293e37','52634c'),
                'steppe':('64523a','97834f'),'gloamwood':('293e39','4f6450'),'citadel':('363d43','64706e')}.get(palette,('414b35','818163'))
        self.ground=material('Earth and moss',ground[0],.98,variation=ground[1],grain=(9,9,2))
        self.road=material('Road dust','655f50',1,variation='817963')
        self.foliage=material('Foliage','263e31',.97,variation='4a6350')
        self.water=material('Still water','243f45',.24,.25)

    def cone(self,name,loc,r1,r2,height,mat,vertices=12):
        bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=r1,radius2=r2,depth=height,location=loc)
        return finish(bpy.context.object,name,mat,self.coll,.014)

    def sphere(self,name,loc,size,mat,subdivisions=1):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions,radius=1,location=loc)
        ob=bpy.context.object
        ob.scale=size
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        return finish(ob,name,mat,self.coll)

    def rock(self,x,y,z=0,scale=1):
        ob=self.sphere('Fractured rock',(x,y,z+.3*scale),(scale*.7,scale*.6,scale*.5),self.stone,2)
        for vertex in ob.data.vertices:
            vertex.co*=random.uniform(.85,1.12)
        ob.rotation_euler.z=random.random()*math.tau
        return ob

    def tree(self,x,y,scale=1,dead=False,broad=False):
        self.cone('Tree trunk',(x,y,scale*1.2),.14*scale,.065*scale,2.4*scale,self.wood,8)
        if dead or broad:
            for i in range(5):
                a=i*2.4
                start=(x,y,scale*(1.2+i*.15))
                tip=(x+math.cos(a)*scale*.8,y+math.sin(a)*scale*.8,scale*(2+i*.17))
                beam('Tree branch',start,tip,.095*scale,.08*scale,self.wood,self.coll)
                if not dead:
                    self.sphere('Leaf crown',tip,(.8*scale,.75*scale,.66*scale),self.foliage,2)
        else:
            for i in range(4):
                self.cone('Pine layered needles',(x,y,scale*(1.1+i*.48)),scale*(.91-i*.16),0,scale*1.4,self.foliage,9)

    def terrain(self,width=100,depth=120):
        verts=[]
        nx,ny=49,49
        for j in range(ny):
            y=-65+j*depth/(ny-1)
            for i in range(nx):
                x=-width/2+i*width/(nx-1)
                z=random.uniform(-.15,-.09)+max(0,y-7)*.01*math.sin(x*.25+y*.31)
                verts.append((x,y,z))
        faces=[]
        for j in range(ny-1):
            for i in range(nx-1):
                k=j*nx+i
                faces.extend([(k,k+1,k+nx),(k+1,k+nx+1,k+nx)])
        mesh('Broad quiet battlefield terrain',verts,faces,self.ground,self.coll,smooth=True)
        roadverts=[]
        for i in range(41):
            x=-width/2+i*width/40
            for side in (-1,1): roadverts.append((x,-4+side*3.1+random.uniform(-.65,.65),-.075))
        road=mesh('Worn horizontal pilgrim road',roadverts,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(40)],self.road,self.coll)
        road.visible_shadow=False
        for _ in range(100):
            x,y=random.uniform(-30,30),random.uniform(-18,20)
            self.rock(x,y,scale=random.uniform(.04,.19))

    def mountain(self,x,y,height=8,width=8):
        ob=self.sphere('Distant rugged mountain',(x,y,height*.14),(width,width*.75,height*.62),self.stone_dark,2)
        for vertex in ob.data.vertices:
            vertex.co*=random.uniform(.78,1.17)
        ob.rotation_euler.z=random.uniform(-.4,.4)

    def window(self,x,y,z,w=.4,h=.75,glow=False):
        verts=[(x-w/2,y,z),(x+w/2,y,z),(x+w/2,y,z+h*.67),(x,y,z+h),(x-w/2,y,z+h*.67)]
        mesh('Pointed window inset',verts,[tuple(range(5))],self.fire if glow else self.dark,self.coll)
        tube('Window archivolt',verts,.035,self.stone_light,self.coll,True)
        beam('Window mullion',(x,y-.025,z+.05),(x,y-.025,z+h*.82),.027,.025,self.iron,self.coll)

    def banner(self,x,y,z,w=.6,h=1.2):
        verts=[]
        for j in range(6):
            t=j/5
            for i in range(7):
                u=i/6
                verts.append((x+(u-.5)*w,y-.035*math.sin(u*math.pi*4),z-t*h+.1*math.sin(u*math.pi)*t))
        faces=[(j*7+i,j*7+i+1,(j+1)*7+i+1,(j+1)*7+i) for j in range(5) for i in range(6)]
        mesh('Hanging cloth banner',verts,faces,self.cloth,self.coll,smooth=True)
        beam('Banner pole',(x-w*.6,y,z),(x+w*.6,y,z),.045,.045,self.brass,self.coll)

    def tower(self,x,y,height=3.5,r=.7,roof=False):
        cylinder('Tower stone core',(x,y,height/2),r,height,self.stone,self.coll,vertices=12,bevel=.025)
        rows=max(4,int(height/.38))
        for j in range(rows):
            for i in range(10):
                a=(i+(j%2)*.5)*math.tau/10
                stone=box('Individual tower masonry',(x+math.cos(a)*r,y+math.sin(a)*r,(j+.5)*height/rows),
                          (.43*r,.11,.9*height/rows),self.stone_light if (i+j)%7==0 else self.stone,self.coll,.025)
                stone.rotation_euler.z=a+math.pi/2
        for z in (.18,height-.18):
            cylinder('Tower cornice',(x,y,z),r*1.1,.16,self.stone_light,self.coll,vertices=12)
        if roof:
            self.cone('Conical slate spire',(x,y,height+.65),r*1.23,0,1.45,self.roof,12)
            self.cone('Spire finial',(x,y,height+1.5),.065,0,.24,self.brass,6)
        else:
            for i in range(8):
                a=i*math.tau/8
                ob=box('Tower merlon',(x+math.cos(a)*r,y+math.sin(a)*r,height+.22),(.32,.28,.44),self.stone,self.coll,.03)
                ob.rotation_euler.z=a
        for z in (height*.32,height*.64): self.window(x,y-r-.09,z,.25,.63)

    def house(self,x,y,scale=1,ruined=False):
        w,d,h=2.3*scale,1.8*scale,1.6*scale
        box('Village plaster walls',(x,y,h/2),(w,d,h),self.stone,self.coll,.06)
        for xx in (-w*.47,0,w*.47):
            box('Village timber upright',(x+xx,y-d*.52,h*.5),(.11*scale,.09*scale,h),self.wood,self.coll)
        verts=[(x-w*.59,y-d*.65,h),(x+w*.59,y-d*.65,h),(x+w*.59,y+d*.65,h),(x-w*.59,y+d*.65,h),
               (x,y-d*.65,h+.85*scale),(x,y+d*.65,h+.85*scale)]
        mesh('Steep gabled slate roof',verts,[(0,1,4),(3,5,2),(0,4,5,3),(1,2,5,4)],self.roof,self.coll)
        box('House chimney',(x+w*.24,y+.3*scale,h+.7*scale),(.32*scale,.34*scale,1*scale),self.stone_dark,self.coll)
        self.window(x-.6*scale,y-d*.51,.65*scale,.4*scale,.65*scale,True)
        box('Village door',(x+.38*scale,y-d*.515,.5*scale),(.54*scale,.04,1*scale),self.wood,self.coll)
        if ruined:
            for i in range(5): self.rock(x+random.uniform(-w,w),y-d,scale=.2*scale)

    def gatehouse(self,x=0,y=0,scale=1):
        start=set(self.coll.objects)
        for xx in (-1.5,1.5): self.tower(xx,0,3.8,.68)
        box('Gatehouse wall',(0,.1,1.7),(2.4,1.3,3.4),self.stone,self.coll,.055)
        for row in range(8):
            for col in range(5):
                box('Gate facade ashlar',(-.98+col*.49,-.585,.22+row*.405),(.465,.14,.37),
                    self.stone_light if (col+row)%8==0 else self.stone,self.coll,.022)
        verts=[(-.71,-.68,.05),(.71,-.68,.05),(.71,-.68,1.86),(0,-.68,2.51),(-.71,-.68,1.86)]
        mesh('Deep pointed gateway',verts,[tuple(range(5))],self.dark,self.coll)
        tube('Gate arch stone rim',verts,.105,self.stone_light,self.coll,True)
        for xx in (-.5,-.25,0,.25,.5):
            beam('Iron portcullis bar',(xx,-.72,.12),(xx,-.72,2.15-abs(xx)*.45),.065,.045,self.iron,self.coll)
        for z in (.55,1.08,1.58): beam('Portcullis crossbar',(-.59,-.755,z),(.59,-.755,z),.055,.04,self.iron,self.coll)
        for xx in (-.95,-.47,0,.47,.95): box('Upper battlement',(xx,-.48,3.65),(.29,.46,.5),self.stone,self.coll)
        self.banner(0,-.78,3.31,.64,.53)
        for xx in (-1.55,1.55):
            self.brazier(xx,-.94,.48,ghost=True)
            for _ in range(3): self.rock(xx+random.uniform(-.6,.6),-.2+random.uniform(-.5,.5),scale=.26)
        for ob in set(self.coll.objects)-start:
            ob.location*=scale
            ob.location+=Vector((x,y,0))
            ob.scale*=scale

    def brazier(self,x,y,z=0,ghost=False):
        cylinder('Brazier foot',(x,y,z+.09),.19,.17,self.stone,self.coll,vertices=8)
        cylinder('Brazier column',(x,y,z+.5),.08,.78,self.iron,self.coll,vertices=8)
        self.cone('Brazier bowl',(x,y,z+.87),.13,.27,.22,self.iron,10)
        for i in range(5):
            a=i*math.tau/5
            self.cone('Flame tongue',(x+.1*math.cos(a),y+.1*math.sin(a),z+1.12),.07,0,.47, self.green if ghost else self.fire,5)

    def shrine(self,x,y,scale=1):
        for i in range(3): box('Shrine step',(x,y,.12+i*.18),(2.2*scale-i*.25,1.65*scale-i*.2,.2),self.stone,self.coll)
        for dx in (-.7,.7):
            cylinder('Shrine column',(x+dx*scale,y,1.15*scale),.13*scale,1.6*scale,self.stone_light,self.coll,vertices=10)
        box('Shrine lintel',(x,y,2*scale),(1.9*scale,.45*scale,.22*scale),self.stone,self.coll)
        self.cone('Shrine roof',(x,y,2.32*scale),1.2*scale,0,.65*scale,self.roof,4)
        self.sphere('Shrine relic',(x,y-.03,1.3*scale),(.2*scale,.19*scale,.36*scale),self.brass,2)

    def cathedral(self,x,y,scale=1):
        box('Cathedral nave',(x,y,2.4*scale),(4*scale,3*scale,4.8*scale),self.stone,self.coll,.08)
        for dx in (-2.2,2.2):
            self.tower(x+dx*scale,y-.55*scale,6*scale,.52*scale,True)
        for dx in (-1.35,-.45,.45,1.35):
            self.window(x+dx*scale,y-1.52*scale,2.1*scale,.5*scale,1.5*scale,True)
        for dx in (-1.6,-.8,0,.8,1.6):
            box('Cathedral flying buttress',(x+dx*scale,y-1.73*scale,1.5*scale),(.18*scale,.48*scale,3*scale),self.stone_dark,self.coll)
        self.cone('Cathedral steep roof',(x,y,5.4*scale),3*scale,0,2*scale,self.roof,4)

    def dock(self,x,y,scale=1,ship=True):
        for i in range(12): box('Dock decking',(x,y+(i-5.5)*.25*scale,.4),(2.3*scale,.23*scale,.14),self.wood,self.coll)
        for dx in (-1,1):
            for dy in (-1.3,0,1.3): cylinder('Dock pile',(x+dx*scale,y+dy*scale,.45),.095*scale,1.3,self.wood,self.coll,vertices=8)
        if ship:
            xx=x+3.1*scale
            verts=[(xx-1.35*scale,y-.6*scale,.3),(xx+1.35*scale,y-.6*scale,.3),(xx+1.8*scale,y,1.1),
                   (xx+1.35*scale,y+.6*scale,.3),(xx-1.35*scale,y+.6*scale,.3),(xx-1.65*scale,y,.9),
                   (xx-1.1*scale,y,0),(xx+1.1*scale,y,0)]
            mesh('Boat oak hull',verts,[(0,1,7,6),(1,2,3,7),(3,4,6,7),(4,5,0,6)],self.wood,self.coll)
            cylinder('Boat mast',(xx,y,1.75*scale),.065,3.5*scale,self.wood,self.coll,vertices=8)
            mesh('Ship sail',[(xx,y,3.3*scale),(xx+1.4*scale,y,1.7*scale),(xx,y,1.7*scale)],[(0,1,2)],self.cloth,self.coll)

    def tent(self,x,y,scale=1):
        verts=[(x-scale,y-scale,0),(x+scale,y-scale,0),(x+scale,y+scale,0),(x-scale,y+scale,0),
               (x,y-scale,1.8*scale),(x,y+scale,1.8*scale)]
        mesh('Army ridge tent',verts,[(0,4,5,3),(1,2,5,4),(3,5,2)],self.cloth,self.coll)
        beam('Tent ridge pole',(x,y-1.2*scale,1.8*scale),(x,y+1.2*scale,1.8*scale),.06,.06,self.wood,self.coll)

    def forge(self,x,y,scale=1):
        box('Furnace body',(x,y,1.25*scale),(2*scale,1.7*scale,2.5*scale),self.stone_dark,self.coll,.08)
        self.window(x,y-.87*scale,.35*scale,.95*scale,1.5*scale,True)
        for dx in (-.55,.55): box('Forge chimney',(x+dx*scale,y+.15*scale,3*scale),(.4*scale,.45*scale,2.2*scale),self.stone,self.coll)
        box('Anvil block',(x+1.6*scale,y-1*scale,.7*scale),(.85*scale,.6*scale,.28*scale),self.iron,self.coll,.045)
        box('Anvil stump',(x+1.6*scale,y-1*scale,.32*scale),(.53*scale,.5*scale,.55*scale),self.wood,self.coll)

    def graveyard(self,x,y,scale=1):
        for i in range(9):
            xx=x+(i%3-1)*.9*scale; yy=y+(i//3)*1.1*scale
            box('Gravestone',(xx,yy,.4*scale),(.4*scale,.13*scale,.8*scale),self.stone_light,self.coll,.06)
            beam('Gravestone cross',(xx,yy-.09*scale,.2*scale),(xx,yy-.09*scale,.65*scale),.032,.03,self.stone_dark,self.coll)
            beam('Gravestone crossbar',(xx-.11*scale,yy-.09*scale,.49*scale),(xx+.11*scale,yy-.09*scale,.49*scale),.026,.03,self.stone_dark,self.coll)
