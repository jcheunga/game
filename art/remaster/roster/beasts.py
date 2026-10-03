"""War hound, cavalry and siege engines."""
import math
import random

from mathutils import Matrix, Vector

from rk import anim, armor as A, beast as B, geo, heads as H, machine as MC, palette, shaders as S, undead as U, weapons as W
from rk.anim import P, STANCES
from rk.character import Character
from rk.rig import LAT, YAW, ROLL, add_poses, blend_poses, bind_rigid

from . import unit
from .common import add_head, boots, finish_clips, gloves, head_frame, plate_arms, plate_legs, profile_of


def lantern_emblem(M):
    return W.emblem_lantern(M['gold'], M['glow'])


# ------------------------------------------------------------------ war hound
@unit('player_hound')
def war_hound(ident, title):
    M = palette.lantern()
    fur = S.fur('5a3a24', name='Brindle coat', tip='7a5234', scale=1.4)
    muzzle = S.fur('241a14', name='Dark muzzle', tip='3e2c20', scale=1.4)
    M['fur'] = fur
    ch = Character(ident, title, skeleton_fn=B.skeleton('hound'))
    B.build_body(ch, fur, muzzle)
    B.head_details(ch, M, M['eye'])
    J = ch.J
    from rk import fur as F
    coat = S.fur('5e3c25', name='Coat strands', tip='9a7046', scale=1.0, rough=0.85)
    grp = F.density_group(ch.body, lambda co, mi: 0.25 if mi == 1 else (0.0 if co.z < 0.07 else 1.0))
    F.add_fur(ch.body, coat, length=0.035, count=5000, children=10, comb=(-0.55, 0, -0.35), normal=0.45, clump=0.45,
              radius=0.003, group=grp, seed=3)
    # armoured barding: teal caparison panel and steel back plates
    secs = []
    for i in range(7):
        t = i / 6
        x = -0.3 + 0.62 * t
        top = 0.8 + 0.04 * t
        sec = []
        for j in range(13):
            a = math.radians(-95 + j * 190 / 12)
            sec.append(Vector((x, math.sin(a) * 0.19, top - 0.17 + math.cos(a) * 0.2)))
        secs.append(sec)
    cap = geo.loft('Caparison', secs, M['cloth'], ch.coll, closed=False, cap=False)
    m = cap.modifiers.new('t', 'SOLIDIFY')
    m.thickness = 0.015
    ch.parts.append((cap, {'skin': ['hips', 'spine', 'chest']}))
    for i in range(4):
        c = Vector((-0.22 + i * 0.16, 0, 0.82 + 0.015 * i))
        sh = geo.shell_cap('Back plate', 0.15, 0.06, M['steel'], ch.coll, segments=16, rings=5, thickness=0.012)
        geo.transform(sh, Matrix.Translation(c) @ Matrix.Diagonal((0.8, 1.25, 1, 1)))
        ch.parts.append((sh, 'spine' if i < 2 else 'chest'))
    for sy in (-1, 1):
        c = Vector((0.0, sy * 0.2, 0.7))
        for o in W.emblem_lantern(M['gold'], M['glow'])(Vector((0, 0, 0)), ch.coll, 0.55):
            geo.transform(o, Matrix.Translation(c) @ Matrix.Rotation(math.radians(-90 * sy), 4, 'Z'))
            ch.parts.append((o, 'spine'))
    # spiked collar
    nc = J['neck'] + Vector((-0.03, 0, -0.02))
    col = geo.lathe('Spiked collar', [(0.15, -0.03), (0.155, 0.03)], 24, M['leather_dark'], ch.coll, close_top=False,
                    close_bottom=False)
    geo.transform(col, Matrix.Translation(nc) @ Matrix.Rotation(math.radians(-60), 4, 'Y'))
    mm = col.modifiers.new('t', 'SOLIDIFY')
    mm.thickness = 0.02
    ch.parts.append((col, 'neck'))
    for k in range(9):
        a = k * math.tau / 9
        d = Matrix.Rotation(math.radians(-60), 3, 'Y') @ Vector((math.cos(a), math.sin(a), 0))
        ch.parts.append((geo.tube('Collar spike', [nc + d * 0.15, nc + d * 0.21], [0.014, 0.001], M['steel'], ch.coll, sides=6),
                         'neck'))
    # steel chanfron on the muzzle
    ch.parts.append((geo.box('Chanfron', (0.22, 0.09, 0.03), Vector((0.76, 0, 1.03)), M['steel'], ch.coll, bevel=0.012,
                             rotation=(0, 12, 0)), 'head'))
    st, C = B.clips('hound')
    ch.tip_rest = J['snout'] + Vector((0.05, 0, -0.04))
    ch.tip_bone = 'head'
    return ch, finish_clips(ch, C, profile_of(ident), body_z=0.62, cam_z=0.65, min_scale=2.6,
                            portrait=dict(target=(0.35, 0, 0.75), distance=4.6))


# ------------------------------------------------------------------ cavalry
def horse(ident, title, M, coat, mane, skeletal=False, barding=None, emblem=None):
    M = dict(M)
    M['fur'] = coat
    hc = Character(ident, title, skeleton_fn=B.skeleton('horse'))
    J = hc.J
    if skeletal:
        bm = M['bone']
        # spine, ribs, skull, legs from bone tubes
        for i in range(10):
            p = J['pelvis'].lerp(J['chest'], i / 9) + Vector((0, 0, 0.12))
            hc.parts.append((geo.sphere('Vertebra', 0.05, p, bm, hc.coll, 10, 6, scale=(1, 1.2, .8)),
                             'hips' if i < 4 else 'spine' if i < 7 else 'chest'))
        for i in range(7):
            x = J['pelvis'].x + 0.45 + i * 0.09
            for sy in (-1, 1):
                pts = [Vector((x, sy * 0.03, J['chest'].z + 0.1)), Vector((x, sy * 0.22, J['chest'].z - 0.05)),
                       Vector((x + 0.04, sy * 0.18, J['chest'].z - 0.3)), Vector((x + 0.06, sy * 0.04, J['chest'].z - 0.38))]
                hc.parts.append((geo.tube('Rib', H.catmull(pts, 3), 0.02, bm, hc.coll, sides=6, flatten=.6), 'spine' if i < 4 else 'chest'))
        pel = geo.quadsphere('Pelvis', 1.0, J['pelvis'] + Vector((-0.05, 0, 0)), bm, hc.coll, 3, scale=(0.22, 0.24, 0.14))
        hc.parts.append((pel, 'hips'))
        for i in range(6):
            p = J['chest'].lerp(J['head'], i / 5)
            hc.parts.append((geo.sphere('Neck vertebra', 0.055, p, bm, hc.coll, 10, 6), 'neck' if i > 1 else 'chest'))
        sk = geo.quadsphere('Horse skull', 1.0, (0, 0, 0), bm, hc.coll, 3, scale=(0.3, 0.1, 0.11))
        hd = (J['snout'] - J['head']).normalized()
        geo.transform(sk, Matrix.Translation(J['head'].lerp(J['snout'], .45)) @
                      Vector((1, 0, 0)).rotation_difference(hd).to_matrix().to_4x4())
        hc.parts.append((sk, 'head'))
        for sy in (-1, 1):
            hc.parts.append((geo.sphere('Ghost eye', 0.035, J['head'] + hd * 0.1 + Vector((0, sy * 0.09, 0.06)), M['glow'],
                                        hc.coll, 10, 6), 'head'))
        for leg in ('fl.R', 'fl.L', 'bl.R', 'bl.L'):
            keys = ['top', 'elbow', 'wrist', 'paw'] if leg.startswith('fl') else ['top', 'knee', 'hock', 'paw']
            bones = ['upper', 'lower', 'foot']
            for a, b, bn in zip(keys, keys[1:], bones):
                pa, pb = J[leg + '.' + a], J[leg + '.' + b]
                hc.parts.append((geo.tube('Leg bone', [pa, pb], [0.04, 0.03], bm, hc.coll, sides=8), leg + '.' + bn))
                hc.parts.append((geo.sphere('Joint', 0.05, pb, bm, hc.coll, 8, 6), leg + '.' + bn))
        flame = mane
        for i in range(8):
            p = J['chest'].lerp(J['head'], i / 7) + Vector((-0.06, 0, 0.12))
            hc.parts.append((geo.tube('Ghost mane', [p, p + Vector((-0.18, 0, 0.18))], [0.05, 0.005], flame, hc.coll, sides=6),
                             'neck' if i > 2 else 'chest'))
        hc.parts.append((geo.tube('Ghost tail', [J['tail0'], J['tail1'], J['tail2'] + Vector((-0.1, 0, -0.2))],
                                  [0.07, 0.05, 0.01], flame, hc.coll, sides=8), 'tail.0'))
    else:
        B.build_body(hc, coat, coat)
        B.head_details(hc, M, M['eye'], teeth=False, horse=True)
        from rk import fur as F
        grp = F.density_group(hc.body, lambda co, mi: 0.0 if co.z < 0.1 else 1.0)
        F.add_fur(hc.body, coat, length=0.014, count=9000, children=6, comb=(-0.7, 0, -0.5), normal=0.3, clump=0.25,
                  radius=0.002, group=grp, seed=5)
        for i in range(10):
            p = J['chest'].lerp(J['head'], i / 9) + Vector((-0.1, 0, 0.13 - 0.02 * i))
            hc.parts.append((geo.tube('Mane', [p, p + Vector((-0.12, 0.04 * (-1) ** i, -0.12))], [0.05, 0.01], mane, hc.coll,
                                      sides=6, flatten=.5), 'neck' if i > 3 else 'chest'))
        hc.parts.append((geo.tube('Horse tail', [J['tail0'], J['tail1'], J['tail2'] + Vector((-0.05, 0, -0.45))],
                                  [0.07, 0.06, 0.02], mane, hc.coll, sides=10), 'tail.0'))
    # saddle and caparison
    sp = J['spine'] + Vector((0.05, 0, 0.22))
    hc.parts.append((geo.box('Saddle', (0.5, 0.42, 0.1), sp, M['leather'], hc.coll, bevel=0.04, subsurf=1), 'spine'))
    hc.parts.append((geo.box('Cantle', (0.06, 0.34, 0.16), sp + Vector((-0.24, 0, 0.08)), M['leather'], hc.coll, bevel=0.03),
                     'spine'))
    hc.parts.append((geo.box('Pommel', (0.06, 0.26, 0.14), sp + Vector((0.24, 0, 0.07)), M['leather'], hc.coll, bevel=0.03),
                     'spine'))
    if barding is not None:
        secs = []
        for i in range(9):
            t = i / 8
            x = -0.62 + 1.12 * t
            sec = []
            for j in range(15):
                a = math.radians(-100 + j * 200 / 14)
                r = 0.36 + 0.02 * math.sin(j * 1.7 + i)
                sec.append(Vector((x, math.sin(a) * r, 1.3 + math.cos(a) * 0.22 - max(0.0, abs(a) - 0.9) * 0.62)))
            secs.append(sec)
        cap = geo.loft('Caparison', secs, barding, hc.coll, closed=False, cap=False)
        mm = cap.modifiers.new('t', 'SOLIDIFY')
        mm.thickness = 0.015
        hc.parts.append((cap, {'skin': ['hips', 'spine', 'chest']}))
        if emblem is not None:
            for sy in (-1, 1):
                c = J['spine'] + Vector((0.0, sy * 0.33, -0.08))
                for o in emblem(Vector((0, 0, 0)), hc.coll, 1.1):
                    geo.transform(o, Matrix.Translation(c) @ Matrix.Rotation(math.radians(-90 * sy), 4, 'Z'))
                    hc.parts.append((o, 'spine'))
        # chanfron
        hd = (J['snout'] - J['head']).normalized()
        ch_ = geo.box('Chanfron', (0.42, 0.16, 0.04), (0, 0, 0), M['steel'], hc.coll, bevel=0.015)
        geo.transform(ch_, Matrix.Translation(J['head'].lerp(J['snout'], .45) + Vector((0, 0, 0.11))) @
                      Vector((1, 0, 0)).rotation_difference(hd).to_matrix().to_4x4())
        hc.parts.append((ch_, 'head'))
        hc.parts.append((geo.tube('Chanfron spike', [J['head'] + Vector((0.05, 0, 0.16)), J['head'] + Vector((0.18, 0, 0.32))],
                                  [0.03, 0.001], M['steel'], hc.coll, sides=6), 'head'))
    # bridle
    hd = (J['snout'] - J['head']).normalized()
    for sy in (-1, 1):
        hc.parts.append((geo.tube('Rein', [J['snout'] - hd * 0.1 + Vector((0, sy * 0.08, -0.02)), sp + Vector((0.3, sy * 0.15, 0.1))],
                                  0.008, M['leather_dark'], hc.coll, sides=4), 'neck'))
    return hc


def rider_on(horse_ch, rider):
    """Parent the rider armature to the horse saddle so it follows every gait."""
    J = horse_ch.J
    seat = J['spine'] + Vector((0.02, 0, 0.27))
    pel = rider.J['pelvis']
    rider.arm.location = (seat.x - pel.x, 0, seat.z - pel.z)
    import bpy
    bpy.context.view_layer.update()
    mw = rider.arm.matrix_world.copy()
    bone = horse_ch.arm.data.bones['saddle']
    rider.arm.parent = horse_ch.arm
    rider.arm.parent_type = 'BONE'
    rider.arm.parent_bone = 'saddle'
    parent = horse_ch.arm.matrix_world @ bone.matrix_local @ Matrix.Translation((0, bone.length, 0))
    rider.arm.matrix_parent_inverse = parent.inverted()
    rider.arm.matrix_basis = mw


RIDING = P(thigh_R=[(72, LAT), (-14, ROLL)], thigh_L=[(72, LAT), (14, ROLL)], shin_R=-78, shin_L=-78, foot_R=10,
           foot_L=10, spine=[(-4, LAT)], head=[(4, LAT)])


def rider_clips(stance, profile):
    C = {}
    C['idle'] = anim.idle_frames(stance, 4, cape=True)
    walk = []
    for i in range(6):
        ph = math.tau * i / 6
        walk.append(add_poses(stance, P(spine=[(-3 + 3 * math.sin(2 * ph), LAT)], head=[(-2 * math.sin(2 * ph), LAT)],
                                        move={'root': (0, 0, 0.02 * math.sin(2 * ph))}),
                              {'cape.0': 10 + 4 * math.sin(ph), 'cape.1': 12 + 5 * math.sin(ph - .8), 'cape.2': 14}))
    C['walk'] = walk
    C['attack'] = anim.attack_frames(stance, profile)
    C['hit'] = anim.hit_frames(stance)
    death = []
    for i in range(6):
        t = i / 5
        death.append(add_poses(stance, P(spine=[(30 * t, LAT)], head=[(20 * t, LAT)], upper_arm_R=[(-40 * t, LAT)],
                                         upper_arm_L=[(-30 * t, LAT)], move={'root': (-0.2 * t, 0, -0.1 * t)})))
    C['death'] = death
    C['deploy'] = [add_poses(stance, P(spine=[(-12, LAT)])), add_poses(stance, P(spine=[(-6, LAT)])),
                   add_poses(stance, P(upper_arm_R=[(30, LAT)], spine=[(4, LAT)])), stance]
    return C


@unit('player_raider')
def cavalry_rider(ident, title):
    M = palette.lantern()
    coat = S.fur('4e2a16', name='Bay coat', tip='6a3a1e', scale=1.6)
    mane = S.fur('1e1612', name='Black mane', tip='3b2a20', scale=1.0)
    hc = horse(ident, title, M, coat, mane, barding=M['cloth'], emblem=lantern_emblem(M))
    rider = Character(ident + '_rider', title + ' rider', dict(bulk=1.0), coll=hc.coll, extras=lambda sk, J: A.cape_bones(sk, J))
    st = add_poses(RIDING, P(upper_arm_R=[(-6, LAT), (-10, ROLL)], forearm_R=70, hand_R=-5,
                             upper_arm_L=[(30, LAT), (10, ROLL)], forearm_L=50))
    rider.set_stance(st)
    rider.build_body(dict(skin=M['skin'], torso=M['mail'], sleeve=M['mail'], glove=M['leather_dark'], legs=M['mail']))
    C, R = head_frame(rider)
    add_head(rider, H.human_head(C, R, M, rider.coll))
    add_head(rider, H.sallet(C + Vector((0, 0, R * .12)), R, M, rider.coll))
    add_head(rider, H.crest_plume(C + Vector((-R * .3, 0, R * 1.25)), R, M['cloth'], rider.coll, length=1.2))
    rider.add(A.breastplate(rider.s, rider.J, M, rider.coll, fauld=2))
    rider.add(A.cape(rider.J, rider.s, M['cape'], rider.coll, length=0.6, width=0.9, collar=M['trim']))
    plate_arms(rider, M, lames=3, size=0.95)
    gloves(rider, M, 'steel')
    plate_legs(rider, M)
    boots(rider, M, mat=M['steel'], shaft=0.06, plate=M['steel'])
    ln = rider.hold(W.lance(M, rider.coll, length=2.7, paint=S.paint(palette.LANTERN_TEAL, name='Lance paint')), 'R',
                    pitch=8, yaw=-4)
    rider.strap(W.heater_shield(M, rider.coll, size=0.85, emblem=lantern_emblem(M)), 'L', forward=0.12, yaw=-20)
    _, HC = B.clips('horse')
    RC = rider_clips(st, profile_of(ident))
    opts = finish_clips(hc, HC, profile_of(ident), body_z=1.9, cam_z=1.6, min_scale=4.4,
                        companions=[(rider, RC)], portrait=dict(target=(0.5, 0, 2.05), distance=7.0))
    rider_on(hc, rider)
    hc.tip_bone_owner = rider
    return hc, opts


# ------------------------------------------------------------------ siege engines
def _wheel(name, c, r, M, coll, spokes=8, bone_mat=None, width=0.09):
    out = []
    rim = geo.lathe(name + ' rim', [(r * 0.82, -width / 2), (r, -width / 2), (r, width / 2), (r * 0.82, width / 2)], 28,
                    bone_mat or M['wood'], coll, close_top=False, close_bottom=False, rotation=(90, 0, 0), location=c)
    out.append(rim)
    tire = geo.lathe(name + ' tire', [(r * .99, -width / 2 - .005), (r * 1.04, -width / 2 - .005), (r * 1.04, width / 2 + .005),
                                      (r * .99, width / 2 + .005)], 28, M['dark_steel'], coll, close_top=False,
                     close_bottom=False, rotation=(90, 0, 0), location=c)
    out.append(tire)
    out.append(geo.cylinder(name + ' hub', r * 0.18, width * 1.6, c, M['dark_steel'], coll, 12, rotation=(90, 0, 0)))
    for k in range(spokes):
        a = k * math.tau / spokes
        d = Vector((math.cos(a), 0, math.sin(a)))
        out.append(geo.tube(name + ' spoke', [Vector(c) + d * r * 0.15, Vector(c) + d * r * 0.85], 0.018, bone_mat or M['wood'],
                            coll, sides=6))
    return out


def ballista_frame(ch, M, skull=False, wood=None, arm_mat=None, glow=None):
    """Returns tip (rest) for the loaded bolt."""
    J = ch.J
    wd = wood or M['wood']
    am = arm_mat or M['dark_steel']
    parts = []
    parts.append((geo.box('Chassis beam', (1.2, 0.12, 0.12), (0, -0.28, 0.42), wd, ch.coll, bevel=0.02), 'body'))
    parts.append((geo.box('Chassis beam', (1.2, 0.12, 0.12), (0, 0.28, 0.42), wd, ch.coll, bevel=0.02), 'body'))
    for x in (-0.45, 0.0, 0.45):
        parts.append((geo.box('Cross beam', (0.12, 0.7, 0.1), (x, 0, 0.44), wd, ch.coll, bevel=0.02), 'body'))
    parts.append((geo.box('Turntable', (0.3, 0.3, 0.3), (0.05, 0, 0.62), wd, ch.coll, bevel=0.03), 'body'))
    # stock / track
    parts.append((geo.box('Track', (1.35, 0.14, 0.1), (0.05, 0, 0.82), wd, ch.coll, bevel=0.02), 'arm'))
    parts.append((geo.box('Track rail', (1.3, 0.04, 0.03), (0.05, 0, 0.885), am, ch.coll, bevel=0.008), 'arm'))
    # torsion springs and arms
    for sy in (-1, 1):
        parts.append((geo.cylinder('Torsion bundle', 0.07, 0.24, (0.5, sy * 0.16, 0.86), M['rope'], ch.coll, 12), 'arm'))
        pts = [Vector((0.5, sy * 0.16, 0.86)), Vector((0.42, sy * 0.45, 0.88)), Vector((0.28, sy * 0.68, 0.9))]
        parts.append((geo.tube('Ballista arm', pts, [0.05, 0.04, 0.03], am, ch.coll, sides=8), 'arm'))
    parts.append((geo.box('Spring frame', (0.14, 0.5, 0.3), (0.5, 0, 0.86), wd, ch.coll, bevel=0.02), 'arm'))
    # string from arm tips to slider
    sl = Vector((-0.1, 0, 0.9))
    for sy in (-1, 1):
        parts.append((geo.tube('Ballista string', [Vector((0.28, sy * 0.68, 0.9)), sl], 0.008, M['string'], ch.coll, sides=4),
                      'slider'))
    parts.append((geo.box('Slider claw', (0.12, 0.1, 0.06), sl + Vector((0, 0, 0.0)), am, ch.coll, bevel=0.01), 'slider'))
    bolt = [geo.cylinder('Siege bolt', 0.022, 1.0, (0.35, 0, 0.92), M['wood'], ch.coll, 8, rotation=(0, 90, 0), bevel=0)]
    head = W.blade('Bolt head', 0.16, 0.07, M['blade'], ch.coll, base_z=0, thickness=0.03, tip=0.6, taper=0)
    geo.transform(head, Matrix.Translation((0.85, 0, 0.92)) @ Matrix.Rotation(math.radians(90), 4, 'Y'))
    bolt.append(head)
    for o in bolt:
        parts.append((o, 'slider'))
    # winch
    parts.append((geo.cylinder('Winch drum', 0.07, 0.3, (-0.55, 0, 0.82), wd, ch.coll, 12, rotation=(90, 0, 0)), 'arm'))
    for sy in (-1, 1):
        parts.append((geo.box('Winch handle', (0.04, 0.03, 0.22), (-0.55, sy * 0.18, 0.82), am, ch.coll, bevel=0.008), 'arm'))
    if skull:
        for k, x in enumerate((-0.35, 0.15)):
            for sy in (-1, 1):
                sk = geo.quadsphere('Trophy skull', 0.07, (x, sy * 0.36, 0.5), M['bone'], ch.coll, 3, scale=(1.1, .85, 1))
                parts.append((sk, 'body'))
                if glow:
                    parts.append((geo.sphere('Skull glow', 0.018, (x + 0.06, sy * 0.36 - 0.02, 0.51), glow, ch.coll, 8, 6),
                                  'body'))
        for sy in (-1, 1):
            parts.append((geo.tube('Rib arm', [Vector((0.5, sy * 0.2, 0.88)), Vector((0.38, sy * 0.52, 1.0)),
                                               Vector((0.3, sy * 0.72, 0.95))], 0.03, M['bone'], ch.coll, sides=6), 'arm'))
    ch.add(parts)
    return bolt, Vector((1.05, 0, 0.92))


def ballista_skeleton():
    def extra(sk, J):
        sk.bone('arm', (0.05, 0, 0.7), (0.05, 0, 0.95), 'body')
        sk.bone('slider', (-0.1, 0, 0.9), (0.1, 0, 0.9), 'arm')
    return MC.skeleton([('wheel.FR', (0.4, -0.42, 0.3)), ('wheel.FL', (0.4, 0.42, 0.3)),
                        ('wheel.BR', (-0.4, -0.42, 0.3)), ('wheel.BL', (-0.4, 0.42, 0.3))], extra)


@unit('player_ballista')
def ballista_crew(ident, title):
    M = palette.lantern()
    ch = Character(ident, title, skeleton_fn=ballista_skeleton())
    bolt, tip = ballista_frame(ch, M)
    for w in ch.J['wheels']:
        for o in _wheel(w, ch.J[w], 0.3, M, ch.coll):
            ch.parts.append((o, w))
    # teal mantlet with lantern sigil on the front
    mant = W.tower_shield(M, ch.coll, width=0.62, height=0.5, emblem=lantern_emblem(M), ridge=False)
    for o in mant['objs']:
        geo.transform(o, Matrix.Translation((0.62, 0, 0.62)) @ Matrix.Rotation(math.radians(-12), 4, 'Y'))
        ch.parts.append((o, 'body'))
    for o in W.lantern_head(M, ch.coll, Vector((-0.6, 0.3, 0.55)), M['glow'], size=1.0):
        ch.parts.append((o, 'body'))
    ch.parts.append((geo.cylinder('Lantern post', 0.02, 0.4, (-0.6, 0.3, 0.4), M['wood'], ch.coll, 8), 'body'))
    ch.tip_rest = tip
    ch.tip_bone = 'slider'
    # crewman winding the winch behind the engine
    crew = Character(ident + '_crew', 'Ballista crewman', dict(bulk=0.98), coll=ch.coll)
    cst = add_poses(STANCES['onehand'], P(spine=[(-14, LAT)], upper_arm_R=[(40, LAT)], forearm_R=40,
                                          upper_arm_L=[(44, LAT)], forearm_L=40))
    crew.set_stance(cst)
    crew.build_body(dict(skin=M['skin'], torso=M['cloth'], sleeve=M['linen'], glove=M['leather_dark'], legs=M['trousers']))
    C, R = head_frame(crew)
    add_head(crew, H.human_head(C, R, M, crew.coll))
    add_head(crew, H.beard(C, R, M['hair'], crew.coll, length=0.3))
    add_head(crew, H.kettle_hat(C + Vector((0, 0, R * .12)), R, M, crew.coll))
    crew.add(A.shell_torso('Gambeson', crew.s, crew.J, M['cloth'], crew.coll, inflate=0.025, z_from=crew.J['pelvis'].z - 0.2,
                           quilt=True))
    crew.add(A.belt(crew.J, crew.s, M, crew.coll, pouches=2))
    gloves(crew, M)
    boots(crew, M, shaft=0.2)
    _, MCc = MC.clips('ballista', ch.J, recoil=0.1)
    cc = {
        'idle': anim.idle_frames(cst, 4, cape=False),
        'walk': anim.walk_frames(cst, 6, stride=20, cape=False),
        'attack': [add_poses(cst, P(upper_arm_R=[(20 * math.sin(f / 9 * math.tau), LAT)], spine=[(4 * math.sin(f / 9 * math.pi), LAT)]))
                   for f in range(10)],
        'hit': anim.hit_frames(cst), 'death': anim.death_frames(cst), 'deploy': anim.deploy_frames(cst),
    }
    opts = finish_clips(ch, MCc, profile_of(ident), release=bolt, body_z=0.75, cam_z=0.75, min_scale=3.4,
                        companions=[(crew, cc)], portrait=dict(target=(0.0, 0, 0.8), distance=7.5))
    crew.arm.location = (-0.55, -0.62, 0)
    return ch, opts


@unit('enemy_boneballista')
def bone_ballista(ident, title):
    M = palette.rotbound()
    ch = Character(ident, title, skeleton_fn=ballista_skeleton())
    bolt, tip = ballista_frame(ch, M, skull=True, wood=M['bone_dark'], arm_mat=M['bone'], glow=M['glow'])
    for w in ch.J['wheels']:
        for o in _wheel(w, ch.J[w], 0.3, M, ch.coll, spokes=6, bone_mat=M['bone_dark']):
            ch.parts.append((o, w))
    for sy in (-1, 1):
        ch.parts.append((geo.tube('Spine ridge', [Vector((-0.6, sy * 0.3, 0.5)), Vector((-0.2, sy * 0.32, 0.75)),
                                                  Vector((0.3, sy * 0.3, 0.55))], 0.035, M['bone'], ch.coll, sides=6), 'body'))
    std = W.standard(M, ch.coll, M['cloth'], emblem=W.emblem_skull(M['bone'], M['glow']), height=1.6, width=0.36, drop=0.5,
                     finial='spike')
    for o in std['objs']:
        geo.transform(o, Matrix.Translation((-0.62, -0.3, 0.6)))
        ch.parts.append((o, 'body'))
    ch.tip_rest = tip
    ch.tip_bone = 'slider'
    _, MCc = MC.clips('ballista', ch.J, recoil=0.1)
    return ch, finish_clips(ch, MCc, profile_of(ident), release=bolt, body_z=0.75, cam_z=0.75, min_scale=3.2,
                            portrait=dict(target=(0.0, 0, 0.7), distance=6.5))


@unit('enemy_plague_engine')
def plague_engine(ident, title):
    M = palette.rotbound(glow=palette.PLAGUE)
    def extra(sk, J):
        sk.bone('arm', (0.1, 0, 0.75), (0.1, 0, 1.0), 'body')
        sk.bone('barrel', (0.1, 0, 1.0), (0.5, 0, 1.0), 'arm')
    ch = Character(ident, title, skeleton_fn=MC.skeleton([('wheel.FR', (0.45, -0.5, 0.36)), ('wheel.FL', (0.45, 0.5, 0.36)),
                                                          ('wheel.BR', (-0.5, -0.5, 0.36)), ('wheel.BL', (-0.5, 0.5, 0.36))],
                                                         extra))
    for w in ch.J['wheels']:
        for o in _wheel(w, ch.J[w], 0.36, M, ch.coll, spokes=8):
            ch.parts.append((o, w))
    ch.parts.append((geo.box('Engine bed', (1.4, 0.8, 0.18), (0, 0, 0.5), M['wood'], ch.coll, bevel=0.03), 'body'))
    ch.parts.append((geo.box('Gun carriage', (0.6, 0.5, 0.35), (0.05, 0, 0.75), M['wood'], ch.coll, bevel=0.03), 'arm'))
    barrel = geo.lathe('Plague bombard', [(0.0, -0.45), (0.2, -0.45), (0.24, -0.35), (0.2, -0.2), (0.17, 0.4), (0.21, 0.5),
                                          (0.21, 0.56), (0.13, 0.56), (0.13, 0.3)], 24, M['trim'], ch.coll, close_top=False)
    geo.transform(barrel, Matrix.Translation((0.15, 0, 1.0)) @ Matrix.Rotation(math.radians(80), 4, 'Y'))
    ch.parts.append((barrel, 'barrel'))
    for z in (-0.2, 0.15, 0.45):
        band = geo.lathe('Barrel band', [(0.19 + (0.02 if z > 0.4 else 0), z - 0.03), (0.2 + (0.02 if z > 0.4 else 0), z + 0.03)], 24,
                         M['dark_steel'], ch.coll, close_top=False, close_bottom=False)
        geo.transform(band, Matrix.Translation((0.15, 0, 1.0)) @ Matrix.Rotation(math.radians(80), 4, 'Y'))
        ch.parts.append((band, 'barrel'))
    ch.parts.append((geo.sphere('Muzzle glow', 0.12, (0.72, 0, 1.1), M['plague'], ch.coll, 14, 8, scale=(0.4, 1, 1)), 'barrel'))
    # plague cauldron with fumes on the rear deck
    cauldron = geo.lathe('Cauldron', [(0, 0), (0.18, 0.02), (0.25, 0.15), (0.24, 0.3), (0.26, 0.32)], 20, M['dark_steel'], ch.coll)
    geo.place(cauldron, (-0.45, 0, 0.6))
    ch.parts.append((cauldron, 'body'))
    ch.parts.append((geo.cylinder('Brew', 0.23, 0.02, (-0.45, 0, 0.9), M['plague'], ch.coll, 20), 'body'))
    rnd = random.Random(3)
    for k in range(6):
        p = Vector((-0.45 + rnd.uniform(-0.12, 0.12), rnd.uniform(-0.12, 0.12), 0.95 + k * 0.05))
        ch.parts.append((geo.sphere('Plague bubble', 0.04 + 0.02 * rnd.random(), p, M['plague'], ch.coll, 8, 6), 'body'))
    for sy in (-1, 1):
        for x in (-0.6, 0.0, 0.6):
            sk = geo.quadsphere('Skull ornament', 0.065, (x, sy * 0.42, 0.52), M['bone'], ch.coll, 3, scale=(1.1, .85, 1))
            ch.parts.append((sk, 'body'))
        ch.parts.append((geo.tube('Chain', [Vector((-0.65, sy * 0.42, 0.62)), Vector((0, sy * 0.45, 0.45)), Vector((0.65, sy * 0.42, 0.62))],
                                  0.015, M['dark_steel'], ch.coll, sides=6), 'body'))
    ch.tip_rest = Vector((0.75, 0, 1.1))
    ch.tip_bone = 'barrel'
    _, MCc = MC.clips('bombard', ch.J, recoil=0.12, wheel_r=0.36)
    return ch, finish_clips(ch, MCc, profile_of(ident), body_z=0.8, cam_z=0.85, min_scale=3.4,
                            portrait=dict(target=(0.0, 0, 0.85), distance=7.0))


@unit('enemy_siegetower')
def siege_tower(ident, title):
    M = palette.rotbound()
    def extra(sk, J):
        sk.bone('ramp', (0.62, 0, 2.15), (0.62, 0, 2.85), 'body')
    ch = Character(ident, title, skeleton_fn=MC.skeleton([('wheel.FR', (0.5, -0.5, 0.3)), ('wheel.FL', (0.5, 0.5, 0.3)),
                                                          ('wheel.BR', (-0.5, -0.5, 0.3)), ('wheel.BL', (-0.5, 0.5, 0.3))],
                                                         extra))
    wd = M['wood']
    for w in ch.J['wheels']:
        for o in _wheel(w, ch.J[w], 0.3, M, ch.coll, spokes=6):
            ch.parts.append((o, w))
    P_ = ch.parts
    P_.append((geo.box('Tower base', (1.3, 1.0, 0.16), (0, 0, 0.5), wd, ch.coll, bevel=0.02), 'body'))
    for x in (-0.55, 0.55):
        for y in (-0.42, 0.42):
            P_.append((geo.box('Tower post', (0.12, 0.12, 2.4), (x * (1 - 0.05), y * 0.95, 1.7), wd, ch.coll, bevel=0.02), 'body'))
    for z in (1.1, 1.75, 2.35):
        P_.append((geo.box('Floor', (1.25, 0.95, 0.08), (0, 0, z), wd, ch.coll, bevel=0.015), 'body'))
    # hide-covered walls with planks and skull trophies
    hide = S.leather('6e5640', name='Stretched hide', rough=0.75)
    for y in (-0.48, 0.48):
        wall = geo.box('Hide wall', (1.1, 0.03, 1.75), (0, y, 1.5), hide, ch.coll, bevel=0.01)
        geo.displace_noise(wall, 0.01, 8)
        P_.append((wall, 'body'))
        for z in (0.9, 1.45, 2.0):
            P_.append((geo.box('Wall plank', (1.2, 0.05, 0.1), (0, y * 1.02, z), wd, ch.coll, bevel=0.012), 'body'))
        for x in (-0.3, 0.3):
            sk = geo.quadsphere('Trophy skull', 0.08, (x, y * 1.08, 1.75), M['bone'], ch.coll, 3, scale=(1, .85, 1))
            P_.append((sk, 'body'))
    P_.append((geo.box('Back wall', (0.03, 0.95, 1.75), (-0.58, 0, 1.5), hide, ch.coll, bevel=0.01), 'body'))
    for k in range(5):
        y = -0.4 + k * 0.2
        P_.append((geo.box('Merlon', (0.14, 0.14, 0.24), (-0.55, y, 2.62), wd, ch.coll, bevel=0.02), 'body'))
    for sy in (-1, 1):
        for k in range(4):
            x = -0.45 + k * 0.3
            P_.append((geo.box('Side merlon', (0.14, 0.12, 0.22), (x, sy * 0.47, 2.62), wd, ch.coll, bevel=0.02), 'body'))
    # drawbridge ramp (hinged at the top front, raised)
    ramp = geo.box('Drawbridge', (0.08, 0.86, 0.75), (0.64, 0, 2.5), wd, ch.coll, bevel=0.015)
    P_.append((ramp, 'ramp'))
    for z in (2.25, 2.5, 2.75):
        P_.append((geo.box('Ramp batten', (0.05, 0.9, 0.06), (0.7, 0, z), M['dark_steel'], ch.coll, bevel=0.01), 'ramp'))
    for sy in (-1, 1):
        P_.append((geo.tube('Ramp chain', [Vector((0.66, sy * 0.4, 2.85)), Vector((0.5, sy * 0.45, 2.75)), Vector((0.3, sy * 0.45, 2.55))],
                            0.012, M['dark_steel'], ch.coll, sides=5), 'body'))
    # Banner hangs from the back wall rather than towering above it, keeping the health bar in view.
    std = W.standard(M, ch.coll, M['cloth'], emblem=W.emblem_skull(M['bone'], M['glow']), height=0.75, width=0.4, drop=0.5,
                     finial='spike', grip_at=0.0)
    for o in std['objs']:
        geo.transform(o, Matrix.Translation((-0.5, 0.35, 2.3)))
        P_.append((o, 'body'))
    ch.tip_rest = Vector((1.1, 0, 2.2))
    ch.tip_bone = 'ramp'
    _, MCc = MC.clips('siege', ch.J, recoil=0.0, wheel_r=0.3)
    return ch, finish_clips(ch, MCc, profile_of(ident), body_z=1.4, cam_z=1.6, min_scale=4.6,
                            portrait=dict(target=(0.0, 0, 1.5), distance=10.0))


@unit('enemy_splitter')
def bone_nest(ident, title):
    M = palette.rotbound()
    def extra(sk, J):
        sk.bone('heart', (0, 0, 0.62), (0, 0, 0.82), 'body')
    ch = Character(ident, title, skeleton_fn=MC.skeleton([], extra))
    rnd = random.Random(11)
    mound = geo.quadsphere('Ossuary mound', 1.0, (0, 0, 0), M['flesh'] if 'flesh' in M else M['skin'], ch.coll, 4,
                           scale=(0.62, 0.5, 0.42))
    geo.displace_noise(mound, 0.05, 3, 2)
    geo.place(mound, (0, 0, 0.22))
    ch.parts.append((mound, 'body'))
    for k in range(16):
        a = k * math.tau / 16 + rnd.uniform(-0.15, 0.15)
        base = Vector((math.cos(a) * 0.45, math.sin(a) * 0.36, 0.12))
        top = Vector((math.cos(a) * 0.18, math.sin(a) * 0.14, 0.95 + rnd.uniform(-0.1, 0.15)))
        mid = base.lerp(top, .5) + Vector((math.cos(a) * 0.28, math.sin(a) * 0.22, 0))
        ch.parts.append((geo.tube('Nest rib', H.catmull([base, mid, top], 4), geo.taper(9, 0.05, 0.012), M['bone'], ch.coll, sides=8),
                         'body'))
    for k in range(9):
        a = rnd.uniform(0, math.tau)
        r = rnd.uniform(0.3, 0.55)
        p = Vector((math.cos(a) * r, math.sin(a) * r * 0.8, rnd.uniform(0.15, 0.45)))
        sk = geo.quadsphere('Nest skull', 0.09, (0, 0, 0), M['bone'], ch.coll, 3, scale=(1.1, .85, 1))
        geo.transform(sk, Matrix.Translation(p) @ Matrix.Rotation(rnd.uniform(-0.6, 0.6), 4, 'Z') @
                      Matrix.Rotation(rnd.uniform(-0.3, 0.3), 4, 'Y'))
        ch.parts.append((sk, 'body'))
        for sy in (-1, 1):
            e = p + Matrix.Rotation(0, 3, 'Z') @ Vector((0.075, sy * 0.03, 0.01))
            ch.parts.append((geo.sphere('Socket glow', 0.016, e, M['glow'], ch.coll, 6, 4), 'body'))
    heart = geo.quadsphere('Grave heart', 0.17, (0, 0, 0.62), M['glow'], ch.coll, 3, scale=(1, 0.9, 1.25))
    geo.displace_noise(heart, 0.02, 12, 3)
    ch.parts.append((heart, 'heart'))
    for k in range(5):
        a = k * math.tau / 5
        ch.parts.append((geo.tube('Vein', [Vector((0, 0, 0.62)), Vector((math.cos(a) * 0.3, math.sin(a) * 0.25, 0.4)),
                                           Vector((math.cos(a) * 0.5, math.sin(a) * 0.4, 0.15))], 0.022, M['plague'], ch.coll,
                                  sides=6), 'body'))
    ch.tip_rest = Vector((0.6, 0, 0.5))
    ch.tip_bone = 'heart'
    _, MCc = MC.clips('nest', ch.J)
    return ch, finish_clips(ch, MCc, profile_of(ident), body_z=0.45, cam_z=0.55, min_scale=2.8,
                            portrait=dict(target=(0.0, 0, 0.55), distance=5.5))
