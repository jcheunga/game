"""Mounted units in the roster-sheet style: the Lantern Caravan's cavalry rider on an armoured bay warhorse,
and the Rotbound bone lancer on a skeletal horse."""
import math

from mathutils import Matrix, Vector

from rk import arms, beast as B, faces, garb, geo, helms, palette, plate, pose as PS, shaders as S, steed
from rk import character as CH
from rk.anim import P
from rk.character import Character
from rk.rig import LAT, YAW, ROLL

from . import unit
from .beasts import rider_clips, rider_on
from .caravan import _cape_bones, add_head, deg, dress_head, head_frame
from .common import finish_clips, profile_of
from .host import ghost_head_field, skull_head

SEAT_OFFSET = None
MOUNT_FACING = -24   # nearer profile than foot troops, so the horse's length reads (as on the roster sheet)


def _seat(hc):
    J = hc.J
    return J['spine'] + Vector((0.02, 0, 0.27))


def rider_setup(hc, ident, title, M, spec, R=None, L=None, bones=False, cape=True, lean=0.0):
    """A rider seated in the saddle, feet in the stirrups (built at the origin, mounted later)."""
    ch = Character(ident + '_rider', title + ' rider', spec, rig_kind='hero', coll=hc.coll,
                   extras=_cape_bones if cape else None)
    seat = _seat(hc)
    pel = ch.J['pelvis']
    off = Vector((seat.x - pel.x, 0, seat.z - pel.z))
    feet = {}
    for side, sy in (('R', -1), ('L', 1)):
        stir = Vector((0.04, sy * 0.37, 1.04)) - off
        hip = ch.J['hip.' + side]
        feet[side] = PS.Leg(stir, pole=hip + Vector((0.7, sy * 0.5, -0.2)), toe=(1, sy * 0.25, 0))
    base = P(spine=[(lean - 2, LAT), (-10, YAW)], chest=[(-2, LAT), (-8, YAW)], head=[(2, LAT), (24, YAW)])
    st = PS.stance(ch, base, R=R, L=L, feet=feet)
    ch.set_stance(st)
    if bones:
        ch.build_bones(M['bone'], thick=1.3)
    else:
        ch.build_body(M['skin'])
    ch.dress()
    K = garb.Kit(ch, M)
    return ch, st, K, plate.Smith(K), off


def lance(M, coll, length=2.9, paint='paint', pennon=None, emblem=None, head='blade', bone=False):
    res = arms.spear(M, coll, length=length, head_len=0.3, head_w=0.08, grip_at=0.22, shaft_r=0.03, langets=False,
                     butt=True)
    f = garb.F.Field('Vamplate')
    f.add(garb.F.RoundCone(Vector((0, 0, 0.07)), Vector((0, 0, 0.3)), 0.11, 0.032), 0.0)
    f.cut(garb.F.RoundCone(Vector((0, 0, 0.05)), Vector((0, 0, 0.28)), 0.1, 0.022), 0.003)
    res['objs'].append(f.mesh(0.004, coll, M['dark_steel'], name='Vamplate'))
    if pennon is not None:
        z = length * 0.78 - length * 0.22 - 0.35
        res['objs'] += arms.pennon(pennon, z, coll, length=0.6, height=0.24)
    return res


@unit('player_raider')
def cavalry_rider(ident, title):
    CH.set_facing(MOUNT_FACING)
    M = palette.caravan()
    coat = S.st_leather('6a3a1e', name='Bay coat', rough=0.55, edge=0.25, sheen=0.25)
    mane = S.st_hair('241812', name='Black mane', tip='4a3424')
    hoof = S.st_leather('2a2420', name='Hoof', rough=0.4)
    hc = Character(ident, title, skeleton_fn=B.skeleton('horse'))
    steed.build(hc, M, 'living', coat=coat, mane=mane, hoof=hoof)
    steed.barding(hc, M, M['cloth'], trim=M['gold'], emblem='lantern', emblem_mat=M['gold'], plate=True, low=0.84)
    # rider: lance couched under the right arm, reins in the left hand
    G = Vector((0.18, -0.24, 1.08))
    D = deg(4, 4)
    rider, st, K, A, off = rider_setup(hc, ident, title, M, dict(bulk=1.04),
                                       R=PS.Grip(G, D, pole=(-0.3, -0.7, 1.0)),
                                       L=PS.Reach(Vector((0.4, 0.14, 1.12)), pole=(0.0, 0.6, 1.0),
                                                  fingers=Vector((0.4, -0.3, -0.6)), palm=Vector((0, -1, 0)), pose='fist'))
    K.gambeson('cloth_dark', t=0.024, skirt=0.12, sleeves='wrist', quilt=False)
    K.trousers('trousers_dark', bottom=0.6)
    K.boots('leather_dark', height=0.5)
    A.cuirass(t=0.07)
    K.tabard('cloth', trim='gold', emblem='lantern', emblem_mat='gold', glow_mat='enamel', width=0.15,
             length=rider.Jp['pelvis'].z - 0.3, t=0.09)
    K.belt('leather', buckle='trim', t=0.1)
    for side in ('R', 'L'):
        A.pauldron(side, size=1.02, lames=3)
        A.couter(side)
        A.vambrace(side)
        A.gauntlet_cuff(side)
        A.cuisse(side)
        A.poleyn(side)
        A.greave(side)
    A.sabatons()
    K.cape('cape', length=rider.Jp['pelvis'].z - 0.2, flare=0.2, tatter=0.6)
    C, u = dress_head(rider, M, face=dict(frown=0.8))
    add_head(rider, helms.great_helm(C, u, M, rider.coll, mat='dark_steel', cross=True, crest=M['cloth']))
    ln = lance(M, rider.coll, pennon=M['cloth'])
    lr = rider.hold_at(ln, PS.weapon_frame(G, D), 'R')
    rider.grip(lr, 'R', M['dark_steel'])
    rider.rest_hand('L', M['dark_steel'], pose='fist')
    hst, HC = B.clips('horse')
    RC = rider_clips(st, profile_of(ident))
    rider_on(hc, rider)
    opts = finish_clips(hc, HC, profile_of(ident), body_z=1.9, cam_z=1.6, min_scale=4.4, stance=hst,
                        companions=[(rider, RC, st)], portrait=dict(target=(0.5, 0, 2.05), distance=7.0))
    hc.tip_bone_owner = rider
    return hc, opts


@unit('enemy_bone_lancer')
def bone_lancer(ident, title):
    CH.set_facing(MOUNT_FACING)
    M = palette.host()
    hc = Character(ident, title, skeleton_fn=B.skeleton('horse'))
    steed.build(hc, M, 'bone', mane=M['cloth_dark'])
    steed.barding(hc, M, M['cloth'], trim=None, emblem=None, plate=False, tatter=1.6, low=0.88, saddle='leather_dark')
    G = Vector((0.16, -0.24, 1.12))
    D = deg(10, 4)
    rider, st, K, A, off = rider_setup(hc, ident, title, M, dict(bulk=0.95), bones=True,
                                       R=PS.Grip(G, D, pole=(-0.3, -0.7, 1.0)),
                                       L=PS.Reach(Vector((0.4, 0.14, 1.14)), pole=(0.0, 0.6, 1.0),
                                                  fingers=Vector((0.4, -0.3, -0.6)), palm=Vector((0, -1, 0)), pose='fist'))
    K.loincloth('cloth', length=0.3, width=0.16, sides=1, tatter=1.3)
    A.cuirass(t=0.055)
    K.belt('leather_dark', buckle='trim', t=0.08)
    for side in ('R', 'L'):
        A.pauldron(side, size=0.95, lames=2)
        A.vambrace(side, t=0.02)
        A.greave(side, t=0.025)
    K.cape('cape', length=rider.Jp['pelvis'].z - 0.25, flare=0.25, tatter=1.8)
    C, u, _ = skull_head(rider, M, jaw_open=0.2)
    ghost_head_field(rider, C, u)
    add_head(rider, helms.skull_cap(C, u, M, rider.coll, t=0.13, nasal=True))
    ln = lance(M, rider.coll, length=2.8, pennon=M['cloth'])
    lr = rider.hold_at(ln, PS.weapon_frame(G, D), 'R')
    rider.grip(lr, 'R', M['bone'])
    rider.rest_hand('L', M['bone'], pose='fist')
    hst, HC = B.clips('horse')
    RC = rider_clips(st, profile_of(ident))
    rider_on(hc, rider)
    opts = finish_clips(hc, HC, profile_of(ident), body_z=1.9, cam_z=1.6, min_scale=4.4, stance=hst,
                        companions=[(rider, RC, st)], portrait=dict(target=(0.5, 0, 2.05), distance=7.0))
    hc.tip_bone_owner = rider
    return hc, opts
