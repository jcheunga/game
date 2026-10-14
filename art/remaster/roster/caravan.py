"""Lantern Caravan in the roster-sheet style (output/units.png): sculpted heroic bodies, layered garb,
teal and cream heraldry with the crowned lantern, blued steel and brass, worn leather."""
import math

from mathutils import Matrix, Vector

from rk import arms, faces, garb, geo, helms, palette, plate, pose as PS, weapons as W
from rk.rig import LAT, YAW, ROLL, add_poses
from rk.anim import P, STANCES
from rk.character import Character

import numpy as np

from . import unit
from .common import finish, finish_clips, profile_of


def head_frame(ch, size=1.0):
    """Head centre and unit for rk.faces (the sculpt spans about 2.2u chin to crown)."""
    J = ch.J
    u = 0.184 * size
    C = J['head'] + Vector((0.04, 0, 0.2 * size))
    return C, u


def add_head(ch, objs):
    for o in objs:
        ch.parts.append((o, 'head'))
    return objs


def human(ident, title, M, spec=None, face=None, hair=None, beard=None, cape=False, head_size=1.0):
    """Body, face, hair and beard. Returns (character, C, u)."""
    ch = Character(ident, title, spec or {}, rig_kind='hero',
                   extras=(lambda sk, J: _cape_bones(sk, J)) if cape else None)
    return ch


def _cape_bones(sk, J):
    from rk import armor as A
    A.cape_bones(sk, J)


def dress_head(ch, M, face=None, hair=None, beard=None, head_size=1.0, brows=True):
    if (face or {}).get('female'):
        head_size *= 0.9
    C, u = head_frame(ch, head_size)
    add_head(ch, faces.human_head(C, u, dict(skin=M['skin'], eye=M['eye'], brow=M['hair']), ch.coll, brows=brows,
                                  **(face or {})))
    if beard:
        add_head(ch, faces.beard(C, u, M['hair'], ch.coll, **beard))
    if hair:
        add_head(ch, faces.hair(C, u, M['hair'], ch.coll, **hair))
    return C, u


# ------------------------------------------------------------------ stances
LUNGE = P(thigh_L=[(34, LAT), (-15, ROLL)], shin_L=-50, foot_L=16,
          thigh_R=[(-26, LAT), (15, ROLL)], shin_R=-18, foot_R=40,
          hips=[(-14, YAW)], spine=[(2, LAT), (-14, YAW)], chest=[(2, LAT), (-12, YAW)], neck=[(-2, LAT)],
          head=[(-2, LAT), (30, YAW)], move={'root': (0, 0, -0.15)})
BRACE = P(thigh_L=[(24, LAT), (-14, ROLL)], shin_L=-36, foot_L=12,
          thigh_R=[(-18, LAT), (16, ROLL)], shin_R=-16, foot_R=30,
          hips=[(-16, YAW)], spine=[(1, LAT), (-12, YAW)], chest=[(0, LAT), (-10, YAW)],
          head=[(0, LAT), (34, YAW)], move={'root': (0, 0, -0.1)})
CROUCH_R = P(hips=[(-14, YAW), (6, LAT)], spine=[(-8, LAT), (-12, YAW)], chest=[(-4, LAT), (-10, YAW)],
             head=[(6, LAT), (28, YAW)])
STRIDE = P(thigh_L=[(22, LAT), (-12, ROLL)], shin_L=-30, foot_L=10,
           thigh_R=[(-16, LAT), (12, ROLL)], shin_R=-14, foot_R=26,
           hips=[(-10, YAW)], spine=[(0, LAT), (-10, YAW)], chest=[(0, LAT), (-8, YAW)],
           head=[(0, LAT), (24, YAW)], move={'root': (0, 0, -0.07)})


# Planted feet when a recipe names none: left foot forward, knees a little out (left to the base pose's
# leg rotations alone, IK would plant the feet where those put them, often crossed).
STANCE_FEET = dict(drop=0.11, spread=0.34, stride=0.44, front='L', knees_out=0.45)


def setup(ident, title, base, spec=None, R=None, L=None, cape=False, palette_fn=None, feet=None, **pkw):
    """Character in its stance with a dressed body: returns (M, ch, stance, kit, smith).
    feet: a dict of PS.Leg, or a dict of keyword arguments for PS.planted (with 'drop')."""
    M = (palette_fn or palette.caravan)(**pkw)
    ch = Character(ident, title, spec or {}, rig_kind='hero', extras=_cape_bones if cape else None)
    if feet is None:
        feet = dict(STANCE_FEET)
    if isinstance(feet, dict) and 'drop' in feet:
        base = dict(base)
        mv = dict(base.get('@move', {}))
        mv['root'] = (0, 0, -feet['drop'])
        base['@move'] = mv
        ch.front_foot = feet.get('front', 'L')
        feet = PS.planted(ch, **feet)
    st = PS.stance(ch, base, R=R, L=L, feet=feet)
    ch.feet = feet
    ch.holds = dict(R=R, L=L)
    ch.set_stance(st)
    ch.build_body(M['skin'])
    ch.dress()
    K = garb.Kit(ch, M)
    return M, ch, st, K, plate.Smith(K)


def deg(pitch, yaw=0.0):
    """Direction from pitch (up from horizontal) and yaw (toward +Y, the far side)."""
    p, y = math.radians(pitch), math.radians(yaw)
    return Vector((math.cos(p) * math.cos(y), math.cos(p) * math.sin(y), math.sin(p)))


# ------------------------------------------------------------------ Spearman
@unit('player_spear')
def spearman(ident, title):
    G = Vector((0.08, -0.22, 0.84))
    D = deg(27, 11)
    M, ch, st, K, A = setup(ident, title, LUNGE, dict(bulk=1.06), R=PS.Grip(G, D, pole=(-0.35, -0.6, 0.95)),
                            L=PS.Grip(G + D * 0.54, D, pole=(0.1, 0.6, 0.85)),
                            feet=dict(drop=0.17, spread=0.36, stride=0.62, front='L', knees_out=0.5))
    spear = arms.spear(M, ch.coll, length=2.3, head_len=0.44, head_w=0.13)
    spear['grip2'] = Vector((0, 0, 0.54))
    ch.holds['g2'] = 0.54
    K.shirt('linen', sleeves='wrist', t=0.008)
    K.leg_skirt('linen', length=0.5, t=0.02, flare=0.05, tatter=0.6)
    K.gambeson('leather_dark', t=0.03, skirt=0.16, sleeves='wrist', quilt=False)
    K.trousers('trousers_dark', bottom=0.5)
    K.boots('leather', cuff='leather_dark', height=0.62)
    K.tabard('cloth', trim='gold', emblem='lantern', emblem_mat='gold', glow_mat='enamel',
             length=ch.Jp['knee.R'].z + 0.02)
    K.belt('leather', buckle='trim', pouches=[(-70, 1.0)])
    K.bracers('linen', start=0.3, end=0.9, t=0.016, flare=0.006)
    K.scarf('cloth', drape=0.9)
    for side in ('R', 'L'):
        A.pauldron(side, size=0.98, lames=2)
        A.poleyn(side, size=0.95, mat='leather_dark')
    C, u = dress_head(ch, M, face=dict(frown=0.9, nose=1.05, jaw=1.08), beard=dict(style='short', length=0.15))
    add_head(ch, helms.burgonet(C, u, M, ch.coll))
    sp = ch.hold_at(spear, PS.weapon_frame(G, D), 'R')
    ch.grip(sp, 'R', M['leather_dark'])
    ch.grip2(sp, M['leather_dark'])
    return ch, finish(ch, st, profile_of(ident))


# ------------------------------------------------------------------ Shield Knight
@unit('player_defender')
def shield_knight(ident, title):
    G = Vector((0.3, -0.36, 0.86))
    D = deg(-26, -20)
    shield_c = Vector((0.36, 0.1, 1.18))
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=1.14, chest_w=1.08, arm=1.08, forearm=1.1),
                            R=PS.Grip(G, D, pole=(-0.2, -0.75, 1.0)),
                            L=PS.Reach(shield_c + Vector((-0.12, 0.02, -0.02)), pole=(0.0, 0.7, 0.9),
                                       fingers=Vector((0.1, -1.0, 0.25)), palm=Vector((-1, 0, 0)), pose='fist'))
    K.gambeson('cloth_dark', t=0.024, skirt=0.2, sleeves='wrist', quilt=False)
    K.trousers('trousers_dark', bottom=0.55)
    K.boots('leather_dark', height=0.5)
    A.cuirass(t=0.07)
    A.faulds(lames=2, t=0.085)
    K.tabard('cloth', trim='gold', emblem='lantern', emblem_mat='gold', glow_mat='enamel',
             length=ch.Jp['knee.R'].z + 0.06, t=0.09, width=0.16)
    K.belt('leather', buckle='trim', t=0.1, pouches=[(-80, 1.0)])
    K.scarf('cloth', size=1.08, drape=0.8, tail=0.0)
    for side in ('R', 'L'):
        A.pauldron(side, size=1.12, lames=3)
        A.couter(side)
        A.vambrace(side)
        A.gauntlet_cuff(side)
        A.cuisse(side)
        A.poleyn(side)
        A.greave(side)
    A.sabatons()
    C, u = dress_head(ch, M, face=dict(frown=1.0, jaw=1.15, brow=1.2, nose=1.1),
                      beard=dict(style='full', length=0.35), hair=dict(style='swept', volume=1.1))
    sword = arms.sword(M, ch.coll, length=0.92, width=0.12, guard=0.3)
    sw = ch.hold_at(sword, PS.weapon_frame(G, D), 'R')
    ch.grip(sw, 'R', M['dark_steel'])
    shield = arms.heater_shield(M, ch.coll, size=1.45, emblem='lantern', face='paint_blue')
    ch.strap_at(shield, PS.frame(shield_c, deg(4, -26), (0, 0, 1)), 'L')
    ch.rest_hand('L', M['dark_steel'], pose='fist')
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.1)


# ------------------------------------------------------------------ Peasant
@unit('player_peasant')
def peasant(ident, title):
    G = Vector((0.02, -0.46, 1.36))
    D = deg(108, -12)
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=1.0, forearm=1.15, hand=1.08, belly=0.15),
                            R=PS.Grip(G, D, pole=(-0.3, -0.8, 1.2)),
                            L=PS.Reach(Vector((0.38, 0.26, 1.02)), pole=(0.0, 0.75, 1.0),
                                       fingers=Vector((0.5, -0.3, -0.8)), palm=Vector((-0.3, -1, 0.2)), pose='fist'),
                            skin='c4835e')
    K.shirt('linen', sleeves='elbow', t=0.012, roll='linen', loose=0.6)
    K.trousers('trousers', t=0.016, bottom=0.42, baggy=1.0)
    K.boots('leather_dark', cuff='leather', height=0.45)
    K.tabard('cloth', trim='linen', emblem='lantern', emblem_mat='gold', width=0.12, back=False,
             top=ch.Jp['pelvis'].z + 0.1, length=ch.Jp['knee.R'].z + 0.12, t=0.05, hem='tattered')
    K.belt('leather', buckle='trim', pouches=[(-75, 1.1), (60, 0.9)])
    K.strap('leather_dark', ch.Jp['shoulder.L'] + Vector((0.05, -0.05, 0.08)),
            Vector((ch.Jp['pelvis'].x + 0.1, -0.2, ch.Jp['pelvis'].z + 0.05)), width=0.04)
    K.scarf('cloth', size=0.95, tail=0.0, drape=0.0)
    C, u = dress_head(ch, M, face=dict(frown=1.0, jaw=1.1, nose=1.1, brow=1.15),
                      beard=dict(style='short', length=0.25), hair=dict(style='short'))
    add_head(ch, helms.bandana(C, u, M['cloth'], ch.coll))
    club = arms.club(M, ch.coll, length=0.72)
    cl = ch.hold_at(club, PS.weapon_frame(G, D), 'R')
    ch.grip(cl, 'R', M['skin'])
    ch.rest_hand('L', M['skin'], pose='fist')
    return ch, finish(ch, st, profile_of(ident))


# ------------------------------------------------------------------ Archer
@unit('player_shooter')
def archer(ident, title):
    aim = deg(4, -6)
    bow_c = Vector((0.62, -0.02, 1.48))
    draw = Vector((0.02, -0.12, 1.6))
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=0.9, female=1.0, chest_w=0.92, hand=0.92, arm=0.9),
                            R=PS.Reach(draw, pole=(-0.45, -0.35, 1.75), fingers=aim, palm=Vector((0, 1, -0.3)),
                                       pose='grip'),
                            L=PS.Grip(bow_c, Vector((0.1, 0.05, 1)), pole=(0.2, 0.6, 1.2)), cape=True,
                            hair='5a3420')
    K.shirt('linen', sleeves='wrist', t=0.01)
    K.gambeson('leather', t=0.02, skirt=0.3, sleeves='none', quilt=False, collar=False)
    K.trousers('trousers_dark', bottom=0.55)
    K.boots('leather', cuff='leather_dark', height=0.62)
    K.belt('leather_dark', buckle='trim', pouches=[(-80, 0.9)])
    K.strap('leather_dark', ch.Jp['shoulder.R'] + Vector((0.06, 0.04, 0.06)),
            Vector((ch.Jp['pelvis'].x + 0.12, 0.18, ch.Jp['pelvis'].z + 0.1)), width=0.04)
    K.bracers('leather', start=0.35, end=0.95)
    K.quiver()
    K.cape('cape', length=0.55, flare=0.18, tatter=1.0, collar=None)
    C, u = dress_head(ch, M, face=dict(female=1.0, frown=0.3), hair=dict(style='framed', length=0.5))
    add_head(ch, helms.hood(C, u, M['cloth'], ch.coll, point=0.4, cowl=1.0))
    bw = arms.bow(M, ch.coll, height=1.4)
    Mb = PS.frame(bow_c, aim, Vector((0.1, 0.05, 1)))
    bres = ch.hold_at(bw, Mb, 'L', track_tip=False)
    ch.grip(bres, 'L', M['leather_dark'])
    ch.draw_hand(bres, M['leather_dark'])
    ar = arms.arrow(M, ch.coll, length=0.82)
    Ma = PS.frame(ch.draw_frame.translation + aim * 0.08, Vector((0, 0, 1)).cross(aim).cross(aim) * -1, aim)
    Ma = PS.weapon_frame(ch.draw_frame.translation + Vector((0.0, 0.0, -0.01)), aim)
    conv, _ = ch._conv('hand.L')
    for o in ar['objs']:
        geo.transform(o, conv @ Ma)
        ch.parts.append((o, 'grip.R'))
    ch.tip_rest = conv @ (Ma @ ar['tip'])
    ch.tip_bone = 'hand.L'
    return ch, finish(ch, st, profile_of(ident), release=ar['objs'])


# ------------------------------------------------------------------ Mage
@unit('player_marksman')
def mage(ident, title):
    G = Vector((0.12, -0.36, 1.12))
    D = deg(86, 0)
    M, ch, st, K, A = setup(ident, title, STRIDE, dict(bulk=0.98, belly=0.1),
                            R=PS.Grip(G, D, pole=(-0.3, -0.75, 1.0)),
                            L=PS.Reach(Vector((0.5, 0.18, 1.3)), pole=(0.05, 0.7, 1.0),
                                       fingers=Vector((1, 0, 0.35)), palm=Vector((0, 0, 1)), pose='cup'),
                            hair='b9b3aa', skin='c98d6a')
    K.shirt('linen', sleeves='wrist', t=0.01)
    K.skirt('cloth', length=0.92, flare=0.2, t=0.012, tatter=0.3, split=0.4)
    K.gambeson('cloth', t=0.03, skirt=0.25, sleeves='wrist', quilt=False)
    K.tabard('linen', trim='gold', emblem='lantern', emblem_mat='gold', glow_mat='enamel', width=0.13,
             length=ch.Jp['knee.R'].z - 0.05, t=0.05)
    K.belt('leather', buckle='trim', pouches=[(-80, 1.0), (70, 0.9)])
    K.strap('leather', ch.Jp['shoulder.R'] + Vector((0.06, 0.04, 0.06)),
            Vector((ch.Jp['pelvis'].x + 0.12, 0.2, ch.Jp['pelvis'].z + 0.1)), width=0.045)
    K.boots('leather', cuff='leather_dark', height=0.5)
    K.scarf('cloth', size=1.05, drape=1.0)
    C, u = dress_head(ch, M, face=dict(frown=1.0, age=0.6, nose=1.15, hook=0.5, brow=1.1),
                      beard=dict(style='short', length=0.3), hair=dict(style='swept', volume=1.2))
    stf = arms.staff(M, ch.coll, length=1.85, grip_at=0.5, head='lantern')
    sres = ch.hold_at(stf, PS.weapon_frame(G, D), 'R')
    ch.grip(sres, 'R', M['skin'])
    hand = ch.rest_hand('L', M['skin'], pose='cup')
    fl = arms.flame(M, ch.coll, ch.hand_frames['L'].translation + Vector((0.08, 0, 0.06)), size=1.1, color='flame')
    conv, _ = ch._conv('hand.L')
    for o in fl:
        geo.transform(o, conv)
        ch.parts.append((o, 'hand.L'))
    return ch, finish(ch, st, profile_of(ident))


# ------------------------------------------------------------------ Field Medic
@unit('player_field_medic')
def field_medic(ident, title):
    G = Vector((0.2, -0.3, 1.1))
    D = deg(84, 4)
    M, ch, st, K, A = setup(ident, title, STRIDE, dict(bulk=0.88, female=1.0, chest_w=0.9, hand=0.92),
                            R=PS.Grip(G, D, pole=(-0.3, -0.7, 1.0)),
                            L=PS.Reach(Vector((0.12, 0.3, 0.98)), pole=(-0.1, 0.6, 1.1),
                                       fingers=Vector((0.2, 0.1, -1)), palm=Vector((0, -1, 0)), pose='relaxed'),
                            hair='6a3e22')
    K.shirt('linen', sleeves='wrist', t=0.012)
    K.skirt('linen', length=0.78, flare=0.18, t=0.012, tatter=0.5, split=0.3)
    K.gambeson('linen', t=0.026, skirt=0.2, sleeves='forearm', quilt=False)
    K.tabard('cloth', trim='gold', emblem='lantern', emblem_mat='gold', glow_mat='enamel', width=0.14,
             length=ch.Jp['knee.R'].z - 0.02, t=0.05)
    K.belt('leather', buckle='trim')
    K.satchel('leather', side='L')
    K.bracers('leather_dark', start=0.4, end=0.95)
    K.boots('leather', cuff='leather_dark', height=0.55)
    K.cape('cape', length=0.62, flare=0.14, tatter=0.8)
    C, u = dress_head(ch, M, face=dict(female=1.0, frown=0.1), hair=dict(style='framed', length=0.4))
    add_head(ch, helms.hood(C, u, M['cloth'], ch.coll, point=0.3, cowl=1.05))
    stf = arms.staff(M, ch.coll, length=1.75, grip_at=0.45, head='lantern', gnarl=0.3)
    sres = ch.hold_at(stf, PS.weapon_frame(G, D), 'R')
    ch.grip(sres, 'R', M['skin'])
    ch.rest_hand('L', M['skin'], pose='relaxed')
    return ch, finish(ch, st, profile_of(ident))


# ------------------------------------------------------------------ shared outfits
def footman(K, M, A, ch, coat='gambeson', coat_mat='leather_dark', sleeves='wrist', tabard='cloth', tabard_trim='gold',
            emblem='lantern', trousers='trousers_dark', boots='leather', cuff='leather_dark', bracers='linen',
            pauldrons=0, poleyns=False, scarf=True, pouches=((-70, 1.0),), skirt_len=0.16, under='linen', t=0.026,
            tabard_len=0.02):
    """The Caravan's padded foot soldier: shirt, coat, trousers, boots, tabard, belt, scarf."""
    K.shirt(under, sleeves='wrist', t=0.008)
    if coat == 'gambeson':
        K.gambeson(coat_mat, t=t, skirt=skirt_len, sleeves=sleeves, quilt=False)
    K.trousers(trousers, bottom=0.5)
    K.boots(boots, cuff=cuff, height=0.6)
    if tabard:
        K.tabard(tabard, trim=tabard_trim, emblem=emblem, emblem_mat='gold', glow_mat='enamel',
                 length=ch.Jp['knee.R'].z + tabard_len, t=t + 0.012 + (0.04 if pauldrons and False else 0))
    K.belt('leather', buckle='trim', pouches=list(pouches), t=t + 0.03)
    if bracers:
        K.bracers(bracers, start=0.3, end=0.9, t=0.016, flare=0.006)
    if scarf:
        K.scarf('cloth', drape=0.8, tail=0.18)
    for side in ('R', 'L')[:2 if pauldrons else 0]:
        A.pauldron(side, size=pauldrons, lames=2)
    if poleyns:
        for side in ('R', 'L'):
            A.poleyn(side, size=0.95, mat=poleyns if isinstance(poleyns, str) else None)


def knight(K, M, A, ch, tabard='cloth', emblem='lantern', pauldron=1.08, lames=3, cape=None, cape_len=0.3,
           scarf=True, coat='cloth_dark', tabard_trim='gold', emblem_mat='gold', faulds=2, belt_t=0.1):
    """Full plate over a padded coat with a heraldic tabard."""
    K.gambeson(coat, t=0.024, skirt=0.2, sleeves='wrist', quilt=False)
    K.trousers('trousers_dark', bottom=0.55)
    K.boots('leather_dark', height=0.5)
    A.cuirass(t=0.07)
    if faulds:
        A.faulds(lames=faulds, t=0.085)
    if tabard:
        K.tabard(tabard, trim=tabard_trim, emblem=emblem, emblem_mat=emblem_mat, glow_mat='enamel',
                 length=ch.Jp['knee.R'].z + 0.06, t=0.09, width=0.16)
    K.belt('leather', buckle='trim', t=belt_t, pouches=[(-80, 1.0)])
    if scarf:
        K.scarf('cloth', size=1.06, drape=0.8, tail=0.0)
    for side in ('R', 'L'):
        A.pauldron(side, size=pauldron, lames=lames)
        A.couter(side)
        A.vambrace(side)
        A.gauntlet_cuff(side)
        A.cuisse(side)
        A.poleyn(side)
        A.greave(side)
    A.sabatons()
    if cape:
        K.cape(cape, length=cape_len, flare=0.2, tatter=0.6)


ONE_HAND = dict(G=Vector((0.3, -0.34, 1.0)), D=deg(38, -14), pole=(-0.2, -0.75, 1.0))
RAISED = dict(G=Vector((0.1, -0.42, 1.2)), D=deg(72, -22), pole=(-0.3, -0.8, 1.1))
SHIELD_C = Vector((0.36, 0.14, 1.12))


def shield_arm(center=SHIELD_C, pose='fist'):
    return PS.Reach(center + Vector((-0.12, 0.02, -0.02)), pole=(0.0, 0.7, 0.9), fingers=Vector((0.1, -1.0, 0.25)),
                    palm=Vector((-1, 0, 0)), pose=pose)


def two_hand(G, D, g2, poleR=(-0.3, -0.7, 0.95), poleL=(0.1, 0.65, 0.85)):
    return PS.Grip(G, D, pole=poleR), PS.Grip(G + D * g2, D, pole=poleL)


def arm_shield(ch, shield, M, center=SHIELD_C, facing=deg(4, -26), hand='leather_dark'):
    ch.strap_at(shield, PS.frame(center, facing, (0, 0, 1)), 'L')
    ch.rest_hand('L', M[hand] if isinstance(hand, str) else hand, pose='fist')


LONG_KINDS = ('spear', 'halberd', 'bill', 'poleaxe', 'pike', 'standard', 'staff', 'crossbow', 'bow')


def wield(ch, wd, M, G, D, glove='leather_dark', g2=None, scale=None):
    """The main-hand weapon at grip G along D (scaled about the grip; g2 is the off hand's distance along the
    weapon in metres, after scaling). Short arms are drawn larger, as on the roster sheet."""
    if scale is None:
        scale = 1.06 if wd.get('kind') in LONG_KINDS else 1.28
    res = ch.hold_at(wd, PS.weapon_frame(G, D), 'R', scale=scale)
    ch.grip(res, 'R', M[glove] if isinstance(glove, str) else glove)
    if g2 is not None:
        wd['grip2'] = Vector((0, 0, g2 / scale))
        res['grip2'] = Vector((0, 0, g2 / scale))
        ch.holds['g2'] = g2
        ch.grip2(res, M[glove] if isinstance(glove, str) else glove)
    return res


WIDE = dict(drop=0.15, spread=0.36, stride=0.55, front='L', knees_out=0.5)
GUARD = dict(drop=0.13, spread=0.38, stride=0.4, front='L', knees_out=0.55)
EASY = dict(drop=0.06, spread=0.26, stride=0.3, front='L', knees_out=0.35)


# ------------------------------------------------------------------ infantry
@unit('player_militia')
def militia(ident, title):
    G, D = Vector((0.2, -0.3, 1.0)), deg(28, -6)
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=0.98), R=PS.Grip(G, D, pole=(-0.3, -0.7, 1.0)),
                            L=PS.Reach(Vector((0.36, 0.2, 1.06)), pole=(0.0, 0.7, 1.0), fingers=Vector((0.6, -0.2, -0.6)),
                                       palm=Vector((0, -1, 0)), pose='fist'), feet=GUARD, skin='c08060')
    K.shirt('linen', sleeves='wrist', t=0.01)
    K.gambeson('linen', t=0.028, skirt=0.22, sleeves='forearm', quilt=True)
    K.trousers('trousers', bottom=0.45, baggy=0.6)
    K.boots('leather', cuff='leather_dark', height=0.5)
    K.belt('leather', buckle='trim', pouches=[(-70, 1.0)])
    K.scarf('cloth', size=0.95, tail=0.3, drape=0.0)
    C, u = dress_head(ch, M, face=dict(frown=0.6, nose=1.1), beard=dict(style='short', length=0.1))
    add_head(ch, helms.kettle(C, u, M, ch.coll, brim=1.0))
    wield(ch, arms.spear(M, ch.coll, length=1.6, head_len=0.3, head_w=0.1, grip_at=0.35, langets=False), M, G, D)
    ch.rest_hand('L', M['skin'], pose='fist')
    return ch, finish(ch, st, profile_of(ident))


@unit('player_brawler')
def swordsman(ident, title):
    G, D = ONE_HAND['G'], ONE_HAND['D']
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=1.04), R=PS.Grip(G, D, pole=ONE_HAND['pole']),
                            L=shield_arm(), feet=GUARD)
    footman(K, M, A, ch, coat_mat='trousers', pauldrons=0.95, poleyns='leather_dark')
    C, u = dress_head(ch, M, face=dict(frown=0.8, jaw=1.08), beard=dict(style='full', length=0.2), hair=dict(style='short'))
    add_head(ch, helms.skull_cap(C, u, M, ch.coll, t=0.13))
    wield(ch, arms.sword(M, ch.coll, length=0.82, width=0.1), M, G, D)
    arm_shield(ch, arms.round_shield(M, ch.coll, radius=0.3, face='paint', rim='dark_steel', emblem='lantern',
                                     emblem_mat='gold'), M)
    return ch, finish(ch, st, profile_of(ident))


@unit('player_axeman')
def axeman(ident, title):
    G, D = RAISED['G'], RAISED['D']
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=1.08, arm=1.08), R=PS.Grip(G, D, pole=RAISED['pole']),
                            L=shield_arm(), feet=GUARD, hair='5a2e18')
    K.shirt('linen', sleeves='wrist', t=0.008)
    K.gambeson('mail' if False else 'leather', t=0.026, skirt=0.2, sleeves='elbow', quilt=False)
    K.trousers('trousers', bottom=0.5)
    K.boots('leather', cuff='leather_dark', height=0.6)
    K.tabard('cloth', trim='gold', emblem='lantern', emblem_mat='gold', glow_mat='enamel', length=ch.Jp['knee.R'].z + 0.1,
             t=0.04, width=0.15)
    K.belt('leather_dark', buckle='trim', pouches=[(-75, 1.0)])
    K.bracers('leather_dark')
    K.scarf('cloth', size=1.0, tail=0.2, drape=0.5)
    C, u = dress_head(ch, M, face=dict(frown=1.0, jaw=1.15, nose=1.1), beard=dict(style='forked', length=0.6),
                      hair=dict(style='swept', volume=1.1))
    wield(ch, arms.axe(M, ch.coll, haft_len=0.72, head=1.15), M, G, D)
    arm_shield(ch, arms.round_shield(M, ch.coll, radius=0.29, face='wood', rim='dark_steel', emblem=None), M)
    return ch, finish(ch, st, profile_of(ident))


@unit('player_maceman')
def maceman(ident, title):
    G, D = RAISED['G'], RAISED['D']
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=1.12, belly=0.25, forearm=1.1),
                            R=PS.Grip(G, D, pole=RAISED['pole']),
                            L=PS.Reach(Vector((0.34, 0.22, 1.02)), pole=(0.0, 0.7, 1.0), fingers=Vector((0.6, -0.2, -0.6)),
                                       palm=Vector((0, -1, 0)), pose='fist'), feet=GUARD)
    K.shirt('linen', sleeves='wrist', t=0.008)
    K.gambeson('cloth_dark', t=0.026, skirt=0.2, sleeves='wrist', quilt=True)
    K.trousers('trousers_dark', bottom=0.5)
    K.boots('leather', cuff='leather_dark', height=0.6)
    A.cuirass(t=0.07)
    K.belt('leather', buckle='trim', t=0.1, pouches=[(-80, 1.0)])
    A.pauldron('R', size=1.05, lames=2)
    A.pauldron('L', size=1.0, lames=1)
    for side in ('R', 'L'):
        A.vambrace(side)
        A.poleyn(side)
    C, u = dress_head(ch, M, face=dict(frown=0.9, jaw=1.2, nose=1.2), beard=dict(style='short', length=0.2))
    add_head(ch, helms.kettle(C, u, M, ch.coll, brim=0.7))
    wield(ch, arms.mace(M, ch.coll, haft_len=0.66, head=1.35, flanges=8), M, G, D, glove='dark_steel')
    ch.rest_hand('L', M['dark_steel'], pose='fist')
    return ch, finish(ch, st, profile_of(ident), gait='heavy')


@unit('player_man_at_arms')
def man_at_arms(ident, title):
    G, D = ONE_HAND['G'], ONE_HAND['D']
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=1.1), R=PS.Grip(G, D, pole=ONE_HAND['pole']),
                            L=shield_arm(), feet=GUARD)
    K.gambeson('cloth_dark', t=0.026, skirt=0.24, sleeves='wrist', quilt=False)
    K.trousers('trousers_dark', bottom=0.55)
    K.boots('leather_dark', cuff='leather', height=0.55)
    A.cuirass(t=0.07)
    K.tabard('cloth', trim='gold', emblem='lantern', emblem_mat='gold', glow_mat='enamel', length=ch.Jp['knee.R'].z + 0.08,
             t=0.09, width=0.15)
    K.belt('leather', buckle='trim', t=0.1)
    for side in ('R', 'L'):
        A.pauldron(side, size=1.0, lames=2)
        A.vambrace(side)
        A.poleyn(side)
    C, u = dress_head(ch, M, face=dict(frown=0.8), beard=dict(style='short', length=0.1))
    add_head(ch, helms.burgonet(C, u, M, ch.coll, cheeks=True, peak=False))
    wield(ch, arms.sword(M, ch.coll, length=0.86, width=0.1), M, G, D, glove='dark_steel')
    arm_shield(ch, arms.heater_shield(M, ch.coll, size=1.15, emblem='lantern'), M, hand='dark_steel')
    return ch, finish(ch, st, profile_of(ident))


@unit('player_billman')
def billman(ident, title):
    G, D = Vector((0.1, -0.22, 0.95)), deg(52, 10)
    R, L = two_hand(G, D, 0.5)
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=1.04), R=R, L=L, feet=WIDE)
    footman(K, M, A, ch, coat_mat='leather', pauldrons=0.95, bracers='leather_dark', scarf=False)
    C, u = dress_head(ch, M, face=dict(frown=0.7, nose=1.1), beard=dict(style='short', length=0.15))
    add_head(ch, helms.kettle(C, u, M, ch.coll, brim=0.8))
    wield(ch, arms.polearm(M, ch.coll, 'bill', length=2.1), M, G, D, g2=0.5)
    return ch, finish(ch, st, profile_of(ident))


@unit('player_pikeman')
def pikeman(ident, title):
    G, D = Vector((0.0, -0.2, 0.94)), deg(9, 8)
    R, L = two_hand(G, D, 0.62)
    M, ch, st, K, A = setup(ident, title, LUNGE, dict(bulk=1.02), R=R, L=L,
                            feet=dict(drop=0.17, spread=0.36, stride=0.68, front='L', knees_out=0.5))
    footman(K, M, A, ch, coat_mat='trousers', pauldrons=0.9, bracers='linen', scarf=True)
    C, u = dress_head(ch, M, face=dict(frown=0.8), beard=dict(style='short', length=0.1))
    add_head(ch, helms.burgonet(C, u, M, ch.coll, cheeks=True, peak=True, comb=True))
    wield(ch, arms.polearm(M, ch.coll, 'pike', length=3.0, grip_at=0.25), M, G, D, g2=0.62)
    return ch, finish(ch, st, profile_of(ident))


@unit('player_breacher')
def halberdier(ident, title):
    G, D = Vector((0.12, -0.24, 1.0)), deg(58, 8)
    R, L = two_hand(G, D, 0.48)
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=1.1), R=R, L=L, feet=WIDE)
    K.shirt('linen', sleeves='wrist', t=0.008)
    K.gambeson('cloth', t=0.03, skirt=0.24, sleeves='wrist', quilt=True)
    K.trousers('trousers_dark', bottom=0.5)
    K.boots('leather_dark', cuff='leather', height=0.6)
    A.cuirass(t=0.075)
    A.faulds(lames=2, t=0.09)
    K.belt('leather', buckle='trim', t=0.1)
    for side in ('R', 'L'):
        A.pauldron(side, size=1.02, lames=2)
        A.poleyn(side)
    C, u = dress_head(ch, M, face=dict(frown=0.9, jaw=1.1), beard=dict(style='full', length=0.5))
    add_head(ch, helms.burgonet(C, u, M, ch.coll, cheeks=False, peak=True, comb=True))
    wield(ch, arms.polearm(M, ch.coll, 'halberd', length=2.15), M, G, D, g2=0.48)
    return ch, finish(ch, st, profile_of(ident), heavy=1.1)


@unit('player_greatswordsman')
def greatswordsman(ident, title):
    G, D = Vector((0.24, -0.2, 1.05)), deg(62, 12)
    R, L = two_hand(G, D, -0.16)
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=1.1, arm=1.06), R=R, L=L, feet=WIDE)
    K.shirt('linen', sleeves='wrist', t=0.01)
    K.gambeson('cloth', t=0.03, skirt=0.26, sleeves='wrist', quilt=True)
    K.trousers('trousers', bottom=0.5)
    K.boots('leather', cuff='leather_dark', height=0.6)
    K.belt('leather_dark', buckle='trim', t=0.07, pouches=[(-80, 1.0)])
    K.strap('leather_dark', ch.Jp['shoulder.L'] + Vector((0.05, -0.04, 0.08)),
            Vector((ch.Jp['pelvis'].x + 0.1, -0.2, ch.Jp['pelvis'].z + 0.06)), width=0.045)
    for side in ('R', 'L'):
        A.pauldron(side, size=0.95, lames=2)
        A.poleyn(side)
    C, u = dress_head(ch, M, face=dict(frown=0.9, jaw=1.12), beard=dict(style='short', length=0.3),
                      hair=dict(style='swept', volume=1.0))
    wield(ch, arms.sword(M, ch.coll, length=1.2, width=0.12, guard=0.38, grip=0.3, two_hand=True), M, G, D, g2=-0.16)
    return ch, finish(ch, st, profile_of(ident), heavy=1.15)


@unit('player_warhammer_knight')
def warhammer_knight(ident, title):
    G, D = Vector((0.24, -0.26, 1.05)), deg(48, -150)   # shouldered: the head stays clear of the helm
    R, L = two_hand(G, D, -0.28)
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=1.15, chest_w=1.06), R=R, L=L, feet=WIDE)
    knight(K, M, A, ch, pauldron=1.1)
    C, u = dress_head(ch, M, face=dict(frown=1.0), beard=dict(style='short'))
    add_head(ch, helms.great_helm(C, u, M, ch.coll, crest=M['cloth']))
    wield(ch, arms.hammer(M, ch.coll, haft_len=1.0, head=1.25), M, G, D, glove='dark_steel', g2=-0.28)
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.2)


@unit('player_poleaxe_knight')
def poleaxe_knight(ident, title):
    G, D = Vector((0.14, -0.22, 0.98)), deg(56, 10)
    R, L = two_hand(G, D, 0.5)
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=1.12), R=R, L=L, feet=WIDE)
    knight(K, M, A, ch, pauldron=1.05, tabard='cloth_dark')
    C, u = dress_head(ch, M)
    add_head(ch, helms.armet(C, u, M, ch.coll, mat='dark_steel', plume=M['cloth']))
    wield(ch, arms.polearm(M, ch.coll, 'poleaxe', length=1.85), M, G, D, glove='dark_steel', g2=0.5)
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.1)


@unit('player_banner')
def banner_knight(ident, title):
    G, D = ONE_HAND['G'], ONE_HAND['D']
    S_ = Vector((0.22, 0.24, 0.92))
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=1.12), R=PS.Grip(G, D, pole=ONE_HAND['pole']),
                            L=PS.Grip(S_, deg(86, 0), pole=(0.0, 0.7, 0.9)), feet=GUARD, cape=True)
    knight(K, M, A, ch, pauldron=1.08, cape='cape', cape_len=0.35)
    C, u = dress_head(ch, M, face=dict(frown=0.8, jaw=1.1), beard=dict(style='full', length=0.3),
                      hair=dict(style='swept'))
    wield(ch, arms.sword(M, ch.coll, length=0.86, width=0.1), M, G, D, glove='dark_steel')
    std = arms.standard(M, ch.coll, M['cloth'], height=2.5, grip_at=0.3)
    sres = ch.hold_at(std, PS.weapon_frame(S_, deg(86, 0)), 'L', track_tip=False)
    ch.grip(sres, 'L', M['dark_steel'])
    return ch, finish(ch, st, profile_of(ident), gait='heavy')


@unit('player_paladin')
def paladin(ident, title):
    G, D = RAISED['G'], RAISED['D']
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=1.1), R=PS.Grip(G, D, pole=RAISED['pole']),
                            L=shield_arm(), feet=GUARD, cape=True)
    M['radiant'] = __import__('rk.shaders', fromlist=['x']).emissive('ffd98a', 5, name='Radiance', core='fffbe8')
    knight(K, M, A, ch, tabard='linen', emblem='sun', pauldron=1.08, cape='linen', cape_len=0.3, scarf=False,
           tabard_trim='gold')
    C, u = dress_head(ch, M, face=dict(frown=0.4, jaw=1.1), beard=dict(style='short', length=0.2),
                      hair=dict(style='swept', volume=1.1))
    add_head(ch, helms.crown(C, u, M['gold'], ch.coll, points=8, height=0.18, z=0.75, radius=1.0, gems=M['radiant']))
    sw = arms.sword(M, ch.coll, length=0.78, width=0.11, blade='blade')
    wield(ch, sw, M, G, D, glove='steel')
    arm_shield(ch, arms.heater_shield(M, ch.coll, size=1.3, emblem='sun', face='linen', emblem_mat='gold'), M,
               hand='steel')
    return ch, finish(ch, st, profile_of(ident))


@unit('player_gravebound_knight')
def gravebound_knight(ident, title):
    G, D = ONE_HAND['G'], ONE_HAND['D']
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=1.1), R=PS.Grip(G, D, pole=ONE_HAND['pole']),
                            L=shield_arm(), feet=GUARD, cape=True, skin='9a9a92')
    from rk import shaders as S_
    M['soul'] = S_.emissive('5ff2d8', 9, name='Heart light', core='e6fffa')
    M['cracked'] = S_.ember_metal('3c4650', glow='4ee6cc', name='Cracked steel', strength=6.0, crack=0.016, coverage=0.55)
    M['dark_steel'] = M['cracked']
    knight(K, M, A, ch, tabard='cloth_dark', emblem='lantern', pauldron=1.1, cape='cape', cape_len=0.25,
           tabard_trim='trim', emblem_mat='trim')
    hl = garb.F.Field('Heart light')
    hl.add(garb.F.Sphere(K.around([(ch.Jp['chest'].z + 0.08, 15)], 0.08)[0], 0.045), 0.0)
    K.rigid(hl.mesh(0.003, ch.coll, M['soul'], name='Heart light', smooth=1), 'chest')
    C, u = dress_head(ch, M)
    add_head(ch, helms.great_helm(C, u, M, ch.coll, mat='cracked', cross=False, eyes=M['soul']))
    wield(ch, arms.sword(M, ch.coll, length=0.9, width=0.1), M, G, D, glove='cracked')
    arm_shield(ch, arms.heater_shield(M, ch.coll, size=1.2, emblem='lantern', face='cloth_dark', emblem_mat='trim'),
               M, hand='cracked')
    return ch, finish(ch, st, profile_of(ident), gait='heavy')


@unit('player_duelist')
def duelist(ident, title):
    G, D = Vector((0.42, -0.28, 1.2)), deg(10, -6)
    M, ch, st, K, A = setup(ident, title, LUNGE, dict(bulk=0.96), R=PS.Grip(G, D, pole=(-0.1, -0.8, 1.0)),
                            L=PS.Grip(Vector((0.12, 0.26, 1.08)), deg(40, 20), pole=(-0.1, 0.7, 0.9)),
                            feet=dict(drop=0.13, spread=0.22, stride=0.7, front='L', knees_out=0.35), cape=True,
                            hair='2a1a10')
    K.shirt('linen', sleeves='wrist', t=0.012, loose=0.5)
    K.gambeson('cloth', t=0.022, skirt=0.24, sleeves='forearm', quilt=False, collar=False)
    K.trousers('trousers_dark', bottom=0.55)
    K.boots('leather_dark', cuff='leather', height=0.7)
    K.belt('leather', buckle='trim')
    K.cape('cape', length=ch.Jp['pelvis'].z + 0.15, width=0.7, flare=0.12, tatter=0.0)
    C, u = dress_head(ch, M, face=dict(frown=0.5, nose=1.0, jaw=0.95), beard=dict(style='goatee', length=0.4),
                      hair=dict(style='swept', volume=1.2))
    wield(ch, arms.rapier(M, ch.coll), M, G, D)
    dg = arms.dagger(M, ch.coll, length=0.28)
    dres = ch.hold_at(dg, PS.weapon_frame(Vector((0.12, 0.26, 1.08)), deg(40, 20)), 'L', track_tip=False)
    ch.grip(dres, 'L', M['leather_dark'])
    return ch, finish(ch, st, 'blade-stab')


@unit('player_rogue')
def rogue(ident, title):
    G, D = Vector((0.36, -0.3, 0.96)), deg(-10, -10)
    M, ch, st, K, A = setup(ident, title, CROUCH_R, dict(bulk=0.92), R=PS.Grip(G, D, pole=(-0.2, -0.8, 1.0)),
                            L=PS.Grip(Vector((0.26, 0.28, 1.08)), deg(-20, 15), pole=(0.0, 0.7, 0.9)),
                            feet=dict(drop=0.2, spread=0.34, stride=0.6, front='L', knees_out=0.45), cape=True)
    K.shirt('cloth_dark', sleeves='wrist', t=0.01)
    K.gambeson('leather_dark', t=0.024, skirt=0.26, sleeves='wrist', quilt=False, collar=False)
    K.trousers('trousers_dark', bottom=0.55)
    K.boots('leather_dark', height=0.7)
    K.belt('leather', buckle='trim', pouches=[(-80, 0.9), (70, 0.9)])
    K.strap('leather', ch.Jp['shoulder.R'] + Vector((0.06, 0.04, 0.06)),
            Vector((ch.Jp['pelvis'].x + 0.12, 0.2, ch.Jp['pelvis'].z + 0.1)), width=0.04)
    K.bracers('leather')
    K.cape('cape', length=ch.Jp['pelvis'].z + 0.05, flare=0.15, tatter=0.8)
    C, u = dress_head(ch, M, face=dict(frown=0.9, jaw=0.95, nose=1.0), hair=dict(style='framed'))
    add_head(ch, helms.hood(C, u, M['cloth_dark'], ch.coll, point=0.6, cowl=0.9, opening=1.0))
    wield(ch, arms.dagger(M, ch.coll, length=0.32), M, G, D)
    dres = ch.hold_at(arms.dagger(M, ch.coll, length=0.3), PS.weapon_frame(Vector((0.26, 0.28, 1.08)), deg(-20, 15)),
                      'L', track_tip=False)
    ch.grip(dres, 'L', M['leather_dark'])
    return ch, finish(ch, st, profile_of(ident), gait='prowl')


@unit('player_berserker')
def berserker(ident, title):
    G, D = Vector((0.06, -0.46, 1.28)), deg(95, -30)
    G2 = Vector((0.4, 0.3, 0.98))
    M, ch, st, K, A = setup(ident, title, CROUCH_R, dict(bulk=1.22, chest_w=1.1, arm=1.2, forearm=1.2, belly=0.1),
                            R=PS.Grip(G, D, pole=(-0.3, -0.8, 1.1)),
                            L=PS.Grip(G2, deg(10, 30), pole=(0.0, 0.8, 0.9)), feet=WIDE, hair='8a3a18',
                            skin='c88560')
    K.trousers('trousers', bottom=0.5, baggy=0.8)
    K.boots('leather', cuff='wool', height=0.5)
    K.belt('leather_dark', buckle='trim', width=0.08, pouches=[(-80, 1.0)])
    K.loincloth('cloth', length=0.4, width=0.16, sides=0, tatter=1.0, t=0.07)
    K.strap('leather_dark', ch.Jp['shoulder.L'] + Vector((0.05, -0.04, 0.08)),
            Vector((ch.Jp['pelvis'].x + 0.1, -0.2, ch.Jp['pelvis'].z + 0.06)), width=0.05)
    K.cape('wool', length=ch.Jp['chest'].z - 0.05, width=0.9, flare=0.1, tatter=1.2, collar='wool')
    for side in ('R', 'L'):
        K.bracers('leather_dark', sides=(side,))
    C, u = dress_head(ch, M, face=dict(frown=1.2, jaw=1.2, brow=1.3, open_mouth=0.6),
                      beard=dict(style='full', length=0.7, braids=2), hair=dict(style='shaggy', volume=1.2))
    wield(ch, arms.axe(M, ch.coll, haft_len=0.78, head=1.25, beard=True), M, G, D, glove='skin')
    ax2 = ch.hold_at(arms.axe(M, ch.coll, haft_len=0.66, head=1.0), PS.weapon_frame(G2, deg(10, 30)), 'L',
                     track_tip=False)
    ch.grip(ax2, 'L', M['skin'])
    return ch, finish(ch, st, 'axe-cleave' if profile_of(ident) == 'axe-cleave' else profile_of(ident), heavy=1.2)


# ------------------------------------------------------------------ ranged, casters and specialists
@unit('player_ranger')
def crossbowman(ident, title):
    G, D = Vector((0.28, -0.22, 1.32)), deg(3, -4)
    R, L = two_hand(G, D, 0.3, poleR=(-0.3, -0.8, 1.1), poleL=(0.2, 0.6, 1.0))
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=1.04), R=R, L=L, feet=GUARD)
    footman(K, M, A, ch, coat_mat='leather', pauldrons=0.85, bracers='leather_dark', pouches=((-75, 1.0), (60, 1.0)))
    tw = arms.tower_shield(M, ch.coll, width=0.5, height=0.86, face='paint', rim='dark_steel', emblem='lantern',
                           emblem_mat='gold', studs=False)
    K.rigid  # pavise on the back
    back = K._surface_point(ch.Jp['chest'], Vector((-1, 0, 0)), K.base(['torso']), 0.1)
    Mp = PS.frame(back + Vector((-0.06, 0, -0.12)), Vector((-1, 0.0, 0.0)), Vector((0, 0, 1)))
    for o in tw['objs']:
        geo.transform(o, Mp)
        K.rigid(o, 'chest')
    C, u = dress_head(ch, M, face=dict(frown=0.9), beard=dict(style='short', length=0.15))
    add_head(ch, helms.kettle(C, u, M, ch.coll, brim=0.9))
    cb = arms.crossbow(M, ch.coll)
    res = wield(ch, cb, M, G, D, g2=0.3)
    bolt = [o for o in cb['objs'] if o.name.startswith('Bolt')]
    return ch, finish(ch, st, 'crossbow', release=bolt)


@unit('player_longbowman')
def longbowman(ident, title):
    aim = deg(6, -6)
    bow_c = Vector((0.64, -0.02, 1.52))
    draw = Vector((0.0, -0.13, 1.64))
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=1.0, arm=1.05),
                            R=PS.Reach(draw, pole=(-0.45, -0.35, 1.75), fingers=aim, palm=Vector((0, 1, -0.3)),
                                       pose='grip'),
                            L=PS.Grip(bow_c, Vector((0.08, 0.05, 1)), pole=(0.2, 0.6, 1.2)), feet=GUARD, cape=True,
                            hair='3a2414')
    M['salt'] = __import__('rk.shaders', fromlist=['x']).st_cloth('5d8796', name='Salt-blue wool', shadow='3a5a68')
    K.shirt('linen', sleeves='wrist', t=0.01)
    K.gambeson('leather', t=0.02, skirt=0.3, sleeves='none', quilt=False, collar=False)
    K.trousers('trousers', bottom=0.55)
    K.boots('leather', cuff='leather_dark', height=0.62)
    K.belt('leather_dark', buckle='trim', pouches=[(-80, 0.9)])
    K.bracers('leather', start=0.35, end=0.95)
    K.quiver(fletch='linen')
    K.cape('salt', length=ch.Jp['pelvis'].z - 0.05, flare=0.14, tatter=0.6)
    C, u = dress_head(ch, M, face=dict(frown=0.6, nose=1.05), beard=dict(style='short', length=0.25),
                      hair=dict(style='framed'))
    add_head(ch, helms.hood(C, u, M['salt'], ch.coll, point=0.5, cowl=1.0))
    bw = arms.bow(M, ch.coll, height=1.75, recurve=0.05)
    bres = ch.hold_at(bw, PS.frame(bow_c, aim, Vector((0.08, 0.05, 1))), 'L', track_tip=False)
    ch.grip(bres, 'L', M['leather_dark'])
    ch.draw_hand(bres, M['leather_dark'])
    ar = arms.arrow(M, ch.coll, length=0.92)
    Ma = PS.weapon_frame(ch.draw_frame.translation + Vector((0.0, 0.0, -0.01)), aim)
    conv, _ = ch._conv('hand.L')
    for o in ar['objs']:
        geo.transform(o, conv @ Ma)
        ch.parts.append((o, 'grip.R'))
    ch.tip_rest = conv @ (Ma @ ar['tip'])
    ch.tip_bone = 'hand.L'
    return ch, finish(ch, st, 'bow-draw', release=ar['objs'])


def caster(ident, title, M=None, staff_head='lantern', robe='cloth', robe2='linen', hand_pose='cup', flame=None,
           face=None, beard=None, hair=None, hood=None, cape=None, staff_kw=None, spec=None, **pkw):
    """Robed staff-bearer with a free spell hand."""
    G = Vector((0.14, -0.36, 1.1))
    D = deg(86, 2)
    M_, ch, st, K, A = setup(ident, title, STRIDE, dict(dict(bulk=0.98), **(spec or {})),
                             R=PS.Grip(G, D, pole=(-0.3, -0.75, 1.0)),
                             L=PS.Reach(Vector((0.5, 0.18, 1.3)), pole=(0.05, 0.7, 1.0), fingers=Vector((1, 0, 0.35)),
                                        palm=Vector((0, 0, 1)), pose=hand_pose), feet=EASY, cape=bool(cape),
                             palette_fn=(lambda **kw: M) if M is not None else None, **pkw)
    M = M_
    K.shirt('linen', sleeves='wrist', t=0.01)
    K.skirt(robe, length=0.9, flare=0.2, t=0.012, tatter=0.3, split=0.4)
    K.gambeson(robe, t=0.03, skirt=0.25, sleeves='wrist', quilt=False)
    if robe2:
        K.tabard(robe2, trim='gold', emblem='lantern', emblem_mat='gold', glow_mat='enamel', width=0.13,
                 length=ch.Jp['knee.R'].z - 0.05, t=0.05)
    K.belt('leather', buckle='trim', pouches=[(-80, 1.0), (70, 0.9)])
    K.strap('leather', ch.Jp['shoulder.R'] + Vector((0.06, 0.04, 0.06)),
            Vector((ch.Jp['pelvis'].x + 0.12, 0.2, ch.Jp['pelvis'].z + 0.1)), width=0.045)
    K.boots('leather', cuff='leather_dark', height=0.5)
    if cape:
        K.cape(cape, length=ch.Jp['pelvis'].z - 0.4, flare=0.2, tatter=0.4)
    else:
        K.scarf('cloth', size=1.05, drape=1.0)
    C, u = dress_head(ch, M, face=face or dict(frown=0.9), beard=beard, hair=hair)
    if hood:
        add_head(ch, helms.hood(C, u, M[hood], ch.coll, point=0.6, cowl=1.05))
    stf = arms.staff(M, ch.coll, **dict(dict(length=1.85, grip_at=0.5, head=staff_head), **(staff_kw or {})))
    sres = ch.hold_at(stf, PS.weapon_frame(G, D), 'R')
    ch.grip(sres, 'R', M['skin'])
    ch.rest_hand('L', M['skin'], pose=hand_pose)
    if flame:
        fl = arms.flame(M, ch.coll, ch.hand_frames['L'].translation + Vector((0.08, 0, 0.06)), size=1.0, color=flame)
        conv, _ = ch._conv('hand.L')
        for o in fl:
            geo.transform(o, conv)
            ch.parts.append((o, 'hand.L'))
    return M, ch, st, K, A


@unit('player_coordinator')
def battle_monk(ident, title):
    from rk import shaders as S_
    M = palette.caravan(skin='b87a58')
    M['habit'] = S_.st_cloth('6a4a30', name='Monk habit', shadow='45301e')
    M['holy'] = S_.emissive('ffe9a8', 7, name='Holy light', core='fffbe6')
    M_, ch, st, K, A = caster(ident, title, M=M, staff_head='crook', robe='habit', robe2='cloth', hand_pose='open',
                              flame='holy', face=dict(frown=0.4, age=0.4, jaw=1.1), beard=dict(style='full', length=0.5),
                              hair=dict(style='bald_ring'), spec=dict(bulk=1.06, belly=0.2))
    return ch, finish(ch, st, profile_of(ident))


@unit('player_necromancer')
def necromancer(ident, title):
    from rk import shaders as S_
    M = palette.caravan(skin='b89a88', hair='1e1a18')
    M['deathcloth'] = S_.st_cloth('1f3a40', name='Grave teal', shadow='0e2226')
    M['soulfire'] = S_.emissive('6ff0d2', 10, name='Soul fire', core='e8fff8')
    M['crystal'] = M['soulfire']
    M_, ch, st, K, A = caster(ident, title, M=M, staff_head='orb', robe='deathcloth', robe2=None, hand_pose='claw',
                              flame='soulfire', face=dict(frown=1.0, gaunt=0.6, nose=1.1, hook=0.6), hood='deathcloth',
                              hair=dict(style='framed'), cape='deathcloth', staff_kw=dict(crystal=M['soulfire']),
                              spec=dict(bulk=0.9, thin=0.2))
    return ch, finish(ch, st, profile_of(ident))


@unit('player_stormcaller')
def stormcaller(ident, title):
    from rk import shaders as S_
    M = palette.caravan(hair='d8d4cc')
    M['storm'] = S_.emissive('9fd8ff', 12, name='Storm light', core='f2fbff')
    M['stormcloth'] = S_.st_cloth('2a4f74', name='Storm blue', shadow='182f48')
    M_, ch, st, K, A = caster(ident, title, M=M, staff_head='crystal', robe='stormcloth', robe2='linen', hand_pose='open',
                              flame='storm', face=dict(frown=0.8, age=0.3), beard=dict(style='full', length=0.9),
                              hair=dict(style='long', length=0.8), cape='stormcloth',
                              staff_kw=dict(crystal=M['storm']))
    return ch, finish(ch, st, profile_of(ident))


@unit('player_mechanic')
def engineer(ident, title):
    G, D = Vector((0.1, -0.42, 1.2)), deg(75, -22)
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=1.04, belly=0.2, forearm=1.1),
                            R=PS.Grip(G, D, pole=(-0.3, -0.8, 1.1)),
                            L=PS.Reach(Vector((0.4, 0.2, 1.08)), pole=(0.0, 0.7, 1.0), fingers=Vector((0.8, -0.2, -0.4)),
                                       palm=Vector((0, -1, 0)), pose='grip'), feet=GUARD, hair='4a2a18')
    K.shirt('linen', sleeves='elbow', t=0.012, roll='linen')
    K.gambeson('leather', t=0.022, skirt=0.2, sleeves='none', quilt=False, collar=False)
    K.trousers('trousers', bottom=0.5)
    K.boots('leather_dark', cuff='leather', height=0.55)
    K.loincloth('leather', length=0.45, width=0.16, back=False, tatter=0.0, t=0.06)
    K.belt('leather_dark', buckle='trim', pouches=[(-75, 1.0), (-40, 0.8), (60, 1.0)])
    K.bracers('leather_dark')
    C, u = dress_head(ch, M, face=dict(frown=0.6, nose=1.15), beard=dict(style='full', length=0.3))
    add_head(ch, helms.bandana(C, u, M['cloth'], ch.coll, knot=True, tails=False))
    gg = garb.F.Field('Goggles')
    for sy in (-1, 1):
        gg.add(garb.F.Torus(C + Vector((0.55 * u, sy * 0.32 * u, 0.62 * u)), 0.15 * u, 0.05 * u,
                            Matrix.Rotation(math.radians(70), 3, 'Y')), 0.0)
    gg.add(garb.F.Torus(C + Vector((-0.05 * u, 0, 0.55 * u)), 0.85 * u, 0.04 * u, Matrix.Rotation(math.radians(-15), 3, 'Y'),
                        squash=1.3), 0.0)
    add_head(ch, [gg.mesh(0.003, ch.coll, M['trim'], name='Goggles')])
    wield(ch, arms.tool_hammer(M, ch.coll, length=0.6, head=1.4), M, G, D)
    ch.rest_hand('L', M['leather_dark'], pose='grip')
    return ch, finish(ch, st, profile_of(ident))


@unit('player_quartermaster')
def quartermaster(ident, title):
    G, D = Vector((0.16, -0.38, 1.12)), deg(70, -20)
    M, ch, st, K, A = setup(ident, title, STRIDE, dict(bulk=1.1, belly=0.6), R=PS.Grip(G, D, pole=(-0.3, -0.8, 1.0)),
                            L=PS.Reach(Vector((0.44, 0.16, 1.18)), pole=(0.0, 0.7, 1.0), fingers=Vector((0.6, -0.2, 0.2)),
                                       palm=Vector((0, -1, 0.3)), pose='grip'), feet=EASY, hair='6a5a4a')
    K.shirt('linen', sleeves='forearm', t=0.012, roll='linen', loose=0.5)
    K.gambeson('cloth', t=0.026, skirt=0.3, sleeves='none', quilt=True, collar=False)
    K.trousers('trousers', bottom=0.5, baggy=0.7)
    K.boots('leather', cuff='leather_dark', height=0.5)
    K.belt('leather_dark', buckle='trim', pouches=[(-80, 1.1), (-45, 0.9), (60, 1.0)])
    C, u = dress_head(ch, M, face=dict(frown=0.3, age=0.5, smile=0.4, nose=1.2), beard=dict(style='short', length=0.3),
                      hair=dict(style='bald_ring'))
    # bulky pack on the back: barrel, sack and bedroll
    J = ch.Jp
    back = K._surface_point(J['chest'], Vector((-1, 0, 0)), K.base(['torso']), 0.06)
    pk = garb.F.Field('Pack')
    pk.add(garb.F.Box(back + Vector((-0.14, 0, -0.08)), (0.13, 0.2, 0.26), r=0.05), 0.0)
    pk.add(garb.F.RoundCone(back + Vector((-0.16, -0.22, 0.24)), back + Vector((-0.16, 0.22, 0.24)), 0.08, 0.08), 0.03)
    pk.disp = lambda P: garb.F.noise3(P, 20.0, 3, 1) * 0.004
    K.rigid(pk.mesh(0.005, ch.coll, M['leather'], name='Pack'), 'chest')
    bl = garb.F.Field('Bedroll straps')
    for y in (-0.12, 0.12):
        bl.add(garb.F.Torus(back + Vector((-0.16, y, 0.24)), 0.085, 0.012, Matrix.Rotation(math.radians(90), 3, 'X')), 0.0)
    K.rigid(bl.mesh(0.003, ch.coll, M['leather_dark'], name='Bedroll straps'), 'chest')
    tb = garb.F.Field('Tally board')
    Lw = ch.hand_frames['L'].translation
    tb.add(garb.F.Box(Lw + Vector((0.1, 0.0, 0.08)), (0.015, 0.11, 0.15), Matrix.Rotation(math.radians(-20), 3, 'Z'),
                      r=0.01), 0.0)
    conv, _ = ch._conv('hand.L')
    o = tb.mesh(0.003, ch.coll, M['wood'], name='Tally board')
    geo.transform(o, conv)
    ch.parts.append((o, 'hand.L'))
    wield(ch, arms.tool_hammer(M, ch.coll, length=0.5), M, G, D, glove='skin')
    ch.rest_hand('L', M['skin'], pose='grip')
    return ch, finish(ch, st, profile_of(ident))


@unit('player_grenadier')
def alchemist(ident, title):
    G, D = Vector((-0.02, -0.44, 1.38)), deg(70, -40)
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=0.98), R=PS.Grip(G, D, pole=(-0.4, -0.7, 1.2)),
                            L=PS.Reach(Vector((0.42, 0.18, 1.12)), pole=(0.0, 0.7, 1.0), fingers=Vector((1, 0, 0.2)),
                                       palm=Vector((0, -1, 0)), pose='open'), feet=GUARD, hair='3a2416')
    from rk import shaders as S_
    M['brew'] = S_.emissive('8af06a', 4, name='Brew', core='e6ffd0')
    K.shirt('linen', sleeves='elbow', t=0.012, roll='linen')
    K.gambeson('leather', t=0.024, skirt=0.3, sleeves='none', quilt=False, collar=False)
    K.trousers('trousers_dark', bottom=0.5)
    K.boots('leather', cuff='leather_dark', height=0.6)
    K.loincloth('leather_dark', length=0.55, width=0.17, back=False, t=0.07)
    K.belt('leather', buckle='trim', pouches=[(-80, 1.0), (60, 0.9)])
    K.strap('leather_dark', ch.Jp['shoulder.L'] + Vector((0.05, -0.04, 0.08)),
            Vector((ch.Jp['pelvis'].x + 0.1, -0.2, ch.Jp['pelvis'].z + 0.06)), width=0.05)
    K.bracers('leather_dark')
    C, u = dress_head(ch, M, face=dict(frown=0.4, smile=0.5, nose=1.1), beard=dict(style='goatee', length=0.3),
                      hair=dict(style='shaggy'))
    gg = garb.F.Field('Goggles')
    for sy in (-1, 1):
        gg.add(garb.F.Torus(C + Vector((0.5 * u, sy * 0.32 * u, 0.62 * u)), 0.16 * u, 0.055 * u,
                            Matrix.Rotation(math.radians(70), 3, 'Y')), 0.0)
    gg.add(garb.F.Torus(C + Vector((-0.05 * u, 0, 0.58 * u)), 0.88 * u, 0.04 * u, Matrix.Rotation(math.radians(-15), 3, 'Y'),
                        squash=1.3), 0.0)
    add_head(ch, [gg.mesh(0.003, ch.coll, M['trim'], name='Goggles')])
    fl = arms.flask(M, ch.coll, size=1.6, liquid='brew')
    res = wield(ch, fl, M, G, D)
    return ch, finish(ch, st, 'flask-toss', release=fl['objs'])


@unit('player_sapper_crew')
def sapper_crew(ident, title):
    G, D = Vector((0.05, -0.42, 1.3)), deg(80, -30)
    M, ch, st, K, A = setup(ident, title, STRIDE, dict(bulk=1.02), R=PS.Grip(G, D, pole=(-0.4, -0.7, 1.2)),
                            L=PS.Reach(Vector((0.3, 0.22, 1.0)), pole=(0.0, 0.7, 1.0), fingers=Vector((0.6, 0, -0.6)),
                                       palm=Vector((0, -1, 0)), pose='grip'), feet=GUARD)
    K.shirt('linen', sleeves='wrist', t=0.01)
    K.gambeson('leather_dark', t=0.026, skirt=0.28, sleeves='wrist', quilt=True)
    K.trousers('trousers', bottom=0.5, baggy=0.5)
    K.boots('leather', cuff='leather_dark', height=0.6)
    K.belt('leather', buckle='trim', pouches=[(-80, 1.0), (60, 1.0)])
    C, u = dress_head(ch, M, face=dict(frown=0.7), beard=dict(style='short', length=0.2))
    add_head(ch, helms.hood(C, u, M['cloth'], ch.coll, point=0.4, cowl=1.0))
    J = ch.Jp
    back = K._surface_point(J['chest'], Vector((-1, 0, 0)), K.base(['torso']), 0.06)
    bc = back + Vector((-0.18, 0, -0.1))
    br = garb.F.Field('Powder keg')
    br.add(garb.F.RoundCone(bc - Vector((0, 0, 0.2)), bc + Vector((0, 0, 0.2)), 0.16, 0.16), 0.0)
    br.add(garb.F.Ellipsoid(bc, (0.18, 0.18, 0.12)), 0.08)
    br.disp = lambda P: -0.004 * (np.abs(np.sin(np.arctan2(P[:, 1] - bc.y, P[:, 0] - bc.x) * 9)) ** 6)
    K.rigid(br.mesh(0.005, ch.coll, M['wood'], name='Powder keg'), 'chest')
    hp = garb.F.Field('Keg hoops')
    for z in (-0.14, 0.14):
        hp.add(garb.F.Torus(bc + Vector((0, 0, z)), 0.172, 0.012), 0.0)
    K.rigid(hp.mesh(0.003, ch.coll, M['dark_steel'], name='Keg hoops'), 'chest')
    bm = arms.bomb(M, ch.coll, size=1.6)
    wield(ch, bm, M, G, D)
    ch.rest_hand('L', M['leather_dark'], pose='grip')
    return ch, finish(ch, st, 'flask-toss', release=bm['objs'])


@unit('player_lantern_guard')
def lantern_guard(ident, title):
    G, D = ONE_HAND['G'], ONE_HAND['D']
    M, ch, st, K, A = setup(ident, title, BRACE, dict(bulk=1.16, chest_w=1.08), R=PS.Grip(G, D, pole=ONE_HAND['pole']),
                            L=shield_arm(Vector((0.4, 0.14, 1.06))), feet=GUARD)
    knight(K, M, A, ch, pauldron=1.12, lames=2)
    C, u = dress_head(ch, M, face=dict(frown=0.8, jaw=1.15), beard=dict(style='full', length=0.4))
    add_head(ch, helms.burgonet(C, u, M, ch.coll, cheeks=True, peak=True, comb=True))
    J = ch.Jp
    back = K._surface_point(J['chest'], Vector((-1, 0, 0)), K.base(['torso']), 0.09)
    pole = garb.F.Field('Lantern pole')
    pole.add(garb.F.RoundCone(back + Vector((-0.04, 0.05, -0.3)), back + Vector((-0.08, 0.06, 0.62)), 0.02, 0.018), 0.0)
    K.rigid(pole.mesh(0.003, ch.coll, M['wood'], name='Lantern pole'), 'chest')
    for o in arms.lantern(M, ch.coll, back + Vector((-0.08, 0.06, 0.62)), hang=True, size=1.1):
        K.rigid(o, 'chest')
    wield(ch, arms.mace(M, ch.coll, haft_len=0.62, head=1.2), M, G, D, glove='dark_steel')
    arm_shield(ch, arms.tower_shield(M, ch.coll, width=0.58, height=1.0, face='paint', rim='trim', emblem='lantern',
                                     emblem_mat='gold'), M, Vector((0.4, 0.14, 1.06)), hand='dark_steel')
    return ch, finish(ch, st, profile_of(ident), gait='heavy')


@unit('player_hound')
def war_hound(ident, title):
    from rk import beast as B, shaders as S_, steed
    M = palette.caravan()
    coat = S_.st_leather('6e4426', name='Brindle coat', rough=0.62, edge=0.3, sheen=0.45)
    ch = Character(ident, title, skeleton_fn=B.skeleton('hound'))
    steed.build_hound(ch, M, coat)
    J = ch.J
    # teal caparison over the back and a studded collar
    cap = garb.F.Field('Hound coat')
    torso = ch.body_field.subset(['torso'])
    torso.reach = 0.15
    lo, hi = torso.bounds(0.2)
    cap.add(garb.F.Fn(lambda P: np.abs(torso.eval(P) - 0.02) - 0.008, lo, hi), 0.0)
    cap.keep(garb.F.Fn(lambda P: np.maximum.reduce([0.58 - P[:, 2], P[:, 0] - 0.34, -0.4 - P[:, 0]]), lo, hi), 0.004)
    co = cap.mesh(0.004, ch.coll, M['cloth'], name='Hound coat')
    ch.parts.append((co, {'fieldw': ch.body_field, 'restrict': ['hips', 'spine', 'chest']}))
    tr = garb.F.Field('Hound coat trim')
    tr.add(garb.F.Fn(lambda P: np.abs(torso.eval(P) - 0.022) - 0.011, lo, hi), 0.0)
    tr.keep(garb.F.Fn(lambda P: np.abs(np.maximum.reduce([0.58 - P[:, 2], P[:, 0] - 0.34, -0.4 - P[:, 0]]) + 0.012) - 0.012,
                      lo, hi), 0.003)
    to = tr.mesh(0.003, ch.coll, M['gold'], name='Hound coat trim')
    ch.parts.append((to, {'fieldw': ch.body_field, 'restrict': ['hips', 'spine', 'chest']}))
    col = garb.F.Field('Collar')
    col.add(garb.F.Torus(Vector((0.46, 0, 0.84)), 0.1, 0.022, Matrix.Rotation(math.radians(55), 3, 'Y')), 0.0)
    ch.parts.append((col.mesh(0.003, ch.coll, M['leather_dark'], name='Collar'), 'neck'))
    st_ = garb.F.Field('Collar studs')
    for k in range(8):
        a = k / 8 * math.tau
        st_.add(garb.F.Sphere(Vector((0.46, 0, 0.84)) + Matrix.Rotation(math.radians(55), 3, 'Y') @
                              Vector((math.cos(a) * 0.12, math.sin(a) * 0.12, 0)), 0.013), 0.0)
    ch.parts.append((st_.mesh(0.002, ch.coll, M['trim'], name='Collar studs'), 'neck'))
    hst, HC = B.clips('hound')
    return ch, finish_clips(ch, HC, profile_of(ident), body_z=0.65, cam_z=0.6, min_scale=2.6, stance=hst,
                            portrait=dict(target=(0.3, 0, 0.8), distance=4.0))


@unit('player_outrider')
def outrider(ident, title):
    from rk import beast as B, shaders as S_, steed
    from .mounts import MOUNT_FACING, rider_setup
    from .beasts import rider_clips, rider_on
    from rk import character as CH
    CH.set_facing(MOUNT_FACING)
    M = palette.caravan()
    coat = S_.st_leather('8a5a32', name='Chestnut coat', rough=0.55, edge=0.25, sheen=0.25)
    mane = S_.st_hair('3a2416', name='Dark mane', tip='6a4630')
    hoof = S_.st_leather('2a2420', name='Hoof', rough=0.4)
    hc = Character(ident, title, skeleton_fn=B.skeleton('horse'))
    steed.build(hc, M, 'living', coat=coat, mane=mane, hoof=hoof, bulk=0.88)
    steed.barding(hc, M, M['cloth'], trim=M['gold'], emblem='lantern', emblem_mat=M['gold'], plate=False, low=1.0)
    G = Vector((0.24, -0.34, 1.32))
    D = deg(62, -20)
    rider, st, K, A, off = rider_setup(hc, ident, title, M, dict(bulk=0.98), R=PS.Grip(G, D, pole=(-0.3, -0.8, 1.1)),
                                       L=PS.Reach(Vector((0.4, 0.14, 1.12)), pole=(0.0, 0.6, 1.0),
                                                  fingers=Vector((0.4, -0.3, -0.6)), palm=Vector((0, -1, 0)), pose='fist'))
    K.shirt('linen', sleeves='wrist', t=0.01)
    K.gambeson('leather', t=0.024, skirt=0.12, sleeves='wrist', quilt=False)
    K.trousers('trousers', bottom=0.6)
    K.boots('leather_dark', cuff='leather', height=0.62)
    K.tabard('cloth', trim='gold', emblem='lantern', emblem_mat='gold', width=0.14, length=rider.Jp['pelvis'].z - 0.3,
             t=0.05)
    K.belt('leather_dark', buckle='trim')
    K.cape('cape', length=rider.Jp['pelvis'].z - 0.1, flare=0.25, tatter=0.5)
    C, u = dress_head(rider, M, face=dict(frown=0.6), beard=dict(style='short', length=0.2))
    add_head(rider, helms.kettle(C, u, M, rider.coll, brim=0.6))
    sab = arms.sword(M, rider.coll, length=0.8, width=0.1, curve=0.12, single=True, guard=0.18)
    sres = rider.hold_at(sab, PS.weapon_frame(G, D), 'R')
    rider.grip(sres, 'R', M['leather_dark'])
    rider.rest_hand('L', M['leather_dark'], pose='fist')
    hst, HC = B.clips('horse')
    RC = rider_clips(st, 'sword-cut')
    rider_on(hc, rider)
    opts = finish_clips(hc, HC, 'sword-cut', body_z=1.9, cam_z=1.6, min_scale=4.4, stance=hst,
                        companions=[(rider, RC, st)], portrait=dict(target=(0.5, 0, 2.05), distance=7.0))
    hc.tip_bone_owner = rider
    return hc, opts
