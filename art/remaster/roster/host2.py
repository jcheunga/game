"""The rest of the Rotbound Host in the roster-sheet style: regional dead and their living allies.

Bodies: 'bones' (stylised skeleton, garments on a thin ghost), 'flesh' (sculpted undead flesh) or 'human'
(living bandits and zealots). Each zone has its own palette band (rk/palette.HOST_ZONES).
"""
import math

import numpy as np
from mathutils import Matrix, Vector

from rk import arms, bones, faces, garb, geo, helms, palette, plate, pose as PS, shaders as S
from rk.anim import P
from rk.character import Character
from rk.rig import LAT, YAW, ROLL

from . import unit
from .caravan import (BRACE, EASY, GUARD, LUNGE, ONE_HAND, RAISED, SHIELD_C, STRIDE, WIDE, CROUCH_R, _cape_bones,
                      add_head, arm_shield, deg, dress_head, head_frame, shield_arm, two_hand, wield)
from .common import finish, profile_of
from .host import ghost_head_field, skull_head, waist_wrap

# Great weapons carried on the right shoulder, haft back: the head sits clear of the face in the 3/4 view.
SHOULDERED = (Vector((0.24, -0.26, 1.05)), deg(48, -150))

HUNCHED = P(hips=[(-10, YAW), (-14, LAT)], spine=[(-14, LAT), (-10, YAW)], chest=[(-10, LAT), (-8, YAW)],
            neck=[(16, LAT)], head=[(20, LAT), (22, YAW)])


def setup(kind, ident, title, base, spec=None, R=None, L=None, cape=False, zone=None, feet=None, thick=1.45,
          M=None, **pkw):
    M = M or palette.zone_host(zone, **pkw)
    ch = Character(ident, title, dict(dict(bulk=0.95 if kind == 'bones' else 1.0), **(spec or {})), rig_kind='hero',
                   extras=_cape_bones if cape else None)
    if isinstance(feet, dict) and 'drop' in feet:
        base = dict(base)
        mv = dict(base.get('@move', {}))
        mv['root'] = (0, 0, -feet['drop'])
        base['@move'] = mv
        ch.front_foot = feet.get('front', 'L')
        feet = PS.planted(ch, **feet)
    st = PS.stance(ch, base, R=R, L=L, feet=feet)
    ch.set_stance(st)
    ch.feet = feet
    ch.holds = dict(R=R, L=L)
    if kind == 'bones':
        ch.build_bones(M['bone'], thick=thick)
    else:
        ch.build_body(M['flesh'] if kind == 'flesh' else M['skin'])
    ch.dress()
    K = garb.Kit(ch, M)
    return M, ch, st, K, plate.Smith(K)


def human_palette(zone=None, skin='b98a6a', hair='2a2420'):
    M = palette.zone_host(zone)
    M['skin'] = S.st_skin(skin)
    M['hair'] = S.st_hair(hair)
    M['eye'] = S.iris_eye('3a2a1a')
    return M


def skull(ch, M, **kw):
    C, u, _ = skull_head(ch, M, **kw)
    ghost_head_field(ch, C, u)
    return C, u


def glow_at(K, ch, M, p, r, mat='glow', bone='chest', name='Glow'):
    g = garb.F.Field(name)
    g.add(garb.F.Sphere(p, r), 0.0)
    K.rigid(g.mesh(max(0.002, r * 0.12), ch.coll, M[mat], name=name, smooth=1), bone)


# ------------------------------------------------------------------ city: the core of the Host
@unit('enemy_brute')
def grave_brute(ident, title):
    G, D = Vector((0.08, -0.5, 1.28)), deg(80, -30)
    M, ch, st, K, A = setup('flesh', ident, title, BRACE, dict(bulk=1.4, chest_w=1.18, arm=1.35, forearm=1.4, hand=1.3,
                                                                belly=0.5, traps=1.4, leg=1.2),
                            R=PS.Grip(G, D, pole=(-0.3, -0.9, 1.1)),
                            L=PS.Reach(Vector((0.42, 0.34, 0.98)), pole=(0.0, 0.8, 1.0), fingers=Vector((0.6, 0, -0.7)),
                                       palm=Vector((0, -1, 0)), pose='fist'), feet=WIDE)
    K.trousers('trousers', bottom=0.45, baggy=0.6)
    K.boots('leather_dark', height=0.45)
    K.loincloth('cloth', length=0.5, width=0.2, sides=1, tatter=1.3)
    K.belt('leather_dark', buckle='dark_steel', width=0.09, t=0.06)
    A.pauldron('R', size=1.25, lames=2, spikes=2)
    for side in ('R', 'L'):
        A.vambrace(side, start=0.35, end=0.95, t=0.025)
    K.chain([(ch.Jp['chest'].z + 0.3, 95), (ch.Jp['chest'].z, 20), (ch.Jp['pelvis'].z + 0.2, -50)], hug=0.01)
    C, u = head_frame(ch, 0.95)
    add_head(ch, faces.monster_head(C + Vector((0.06, 0, -0.06)), u, M, ch.coll, kind='brute'))
    add_head(ch, helms.skull_cap(C + Vector((0.06, 0, -0.06)), u, M, ch.coll, t=0.12, nasal=False))
    wield(ch, arms.axe(M, ch.coll, haft_len=0.95, head=1.6, beard=True), M, G, D, glove='flesh')
    ch.rest_hand('L', M['flesh'], pose='fist', size=1.25)
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.3)


@unit('enemy_saboteur')
def sapper(ident, title):
    G, D = Vector((0.0, -0.42, 1.32)), deg(80, -35)
    M, ch, st, K, A = setup('bones', ident, title, CROUCH_R, R=PS.Grip(G, D, pole=(-0.4, -0.7, 1.2)),
                            L=PS.Reach(Vector((0.36, 0.22, 0.98)), pole=(0.0, 0.7, 1.0), fingers=Vector((0.6, 0, -0.6)),
                                       palm=Vector((0, -1, 0)), pose='claw'),
                            feet=dict(drop=0.2, spread=0.34, stride=0.6, front='L', knees_out=0.45))
    K.loincloth('cloth', length=0.4, width=0.14, sides=1, tatter=1.4)
    waist_wrap(K, ch, M, mat='cloth_dark', girth=1.35)
    K.belt('leather_dark', buckle='trim', pouches=[(-80, 1.0), (60, 1.0)], t=0.05)
    K.strap('leather_dark', ch.Jp['shoulder.L'] + Vector((0.05, -0.04, 0.08)),
            Vector((ch.Jp['pelvis'].x + 0.1, -0.2, ch.Jp['pelvis'].z + 0.06)), width=0.04)
    C, u = skull(ch, M, jaw_open=0.35)
    add_head(ch, helms.hood(C, u, M['cloth_dark'], ch.coll, point=0.5, cowl=0.9))
    bm = arms.bomb(M, ch.coll, size=1.7)
    wield(ch, bm, M, G, D, glove='bone')
    ch.rest_hand('L', M['bone'], pose='claw')
    return ch, finish(ch, st, 'flask-toss', gait='prowl', release=bm['objs'])


@unit('enemy_howler')
def dread_herald(ident, title):
    G, D = Vector((0.1, -0.34, 1.0)), deg(86, 0)
    M, ch, st, K, A = setup('bones', ident, title, BRACE, dict(bulk=1.0), R=PS.Grip(G, D, pole=(-0.3, -0.75, 1.0)),
                            L=PS.Reach(Vector((0.3, 0.12, 1.62)), pole=(0.1, 0.7, 1.3), fingers=Vector((0.2, -0.3, 0.9)),
                                       palm=Vector((-1, 0, 0)), pose='grip'), feet=GUARD, cape=True)
    K.loincloth('cloth', length=0.5, width=0.15, sides=1, tatter=1.2)
    A.cuirass(t=0.055)
    K.belt('leather_dark', buckle='trim', t=0.08)
    for side in ('R', 'L'):
        A.pauldron(side, size=1.0, lames=2, spikes=1)
    K.cape('cape', length=ch.Jp['knee.R'].z, flare=0.25, tatter=1.6)
    C, u = skull(ch, M, jaw_open=0.6)
    add_head(ch, helms.great_helm(C, u, M, ch.coll, cross=False, t=0.24))
    std = arms.standard(M, ch.coll, M['cloth'], emblem='rot', emblem_mat='linen', height=2.4, finial='spike',
                        trim=None, tatter=1.2)
    sres = ch.hold_at(std, PS.weapon_frame(G, D), 'R')
    ch.grip(sres, 'R', M['bone'])
    hn = garb.F.Field('War horn')
    Lw = ch.hand_frames['L'].translation
    hn.add(garb.F.Chain([Lw + Vector((0.0, 0.0, 0.06)), Lw + Vector((0.06, -0.08, 0.14)), Lw + Vector((0.14, -0.12, 0.18))],
                        [0.025, 0.045, 0.07]), 0.0)
    conv, _ = ch._conv('hand.L')
    o = hn.mesh(0.003, ch.coll, M['bone'], name='War horn')
    geo.transform(o, conv)
    ch.parts.append((o, 'hand.L'))
    ch.rest_hand('L', M['bone'], pose='grip')
    return ch, finish(ch, st, profile_of(ident))


@unit('enemy_jammer')
def hexer(ident, title):
    G, D = Vector((0.12, -0.36, 1.08)), deg(84, 2)
    M = palette.zone_host('city', glow='b98aff')
    M2, ch, st, K, A = setup('bones', ident, title, STRIDE, R=PS.Grip(G, D, pole=(-0.3, -0.75, 1.0)),
                             L=PS.Reach(Vector((0.48, 0.2, 1.28)), pole=(0.05, 0.7, 1.0), fingers=Vector((1, 0.1, 0.4)),
                                        palm=Vector((0, 0, 1)), pose='claw'), feet=EASY, M=M)
    K.skirt('cloth_dark', length=0.9, flare=0.2, t=0.012, tatter=1.4, split=0.4)
    K.gambeson('cloth_dark', t=0.03, skirt=0.25, sleeves='wrist', quilt=False, collar=False)
    K.belt('leather_dark', buckle='trim', pouches=[(-80, 0.9)])
    K.scarf('cloth', size=1.0, tail=0.35, drape=0.6)
    C, u = skull(ch, M, jaw_open=0.2)
    add_head(ch, helms.hood(C, u, M['cloth_dark'], ch.coll, point=0.8, cowl=1.0))
    stf = arms.staff(M, ch.coll, length=1.7, grip_at=0.5, head='lantern', glow='glow', glass='lamp_glass')
    sres = ch.hold_at(stf, PS.weapon_frame(G, D), 'R')
    ch.grip(sres, 'R', M['bone'])
    ch.rest_hand('L', M['bone'], pose='claw')
    fl = arms.flame(M, ch.coll, ch.hand_frames['L'].translation + Vector((0.08, 0, 0.05)), size=0.9, color='flame')
    conv, _ = ch._conv('hand.L')
    for o in fl:
        geo.transform(o, conv)
        ch.parts.append((o, 'hand.L'))
    return ch, finish(ch, st, profile_of(ident))


@unit('enemy_crusher')
def bone_juggernaut(ident, title):
    G, D = SHOULDERED
    R, L = two_hand(G, D, -0.3)
    M, ch, st, K, A = setup('bones', ident, title, BRACE, dict(bulk=1.3, scale=1.08), R=R, L=L, feet=WIDE, thick=2.1)
    K.loincloth('cloth', length=0.5, width=0.2, sides=1, tatter=1.3, t=0.06)
    A.cuirass(t=0.08)
    A.faulds(lames=2, t=0.09)
    K.belt('leather_dark', buckle='dark_steel', t=0.11, width=0.09)
    for side in ('R', 'L'):
        A.pauldron(side, size=1.35, lames=3, spikes=2)
        A.couter(side, size=1.2)
        A.vambrace(side, t=0.04)
        A.poleyn(side, size=1.15)
        A.greave(side, t=0.045)
    C, u = skull(ch, M, jaw_open=0.3, size=1.0)
    add_head(ch, helms.great_helm(C, u, M, ch.coll, cross=False, t=0.24, mat='dark_steel'))
    wield(ch, arms.hammer(M, ch.coll, haft_len=1.15, head=1.7), M, G, D, glove='bone', g2=-0.3)
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.35)


@unit('enemy_shieldwall')
def shield_wall(ident, title):
    G, D = Vector((0.18, -0.4, 1.0)), deg(10, -10)
    sc = Vector((0.5, 0.0, 1.0))
    M, ch, st, K, A = setup('bones', ident, title, BRACE, dict(bulk=1.1), R=PS.Grip(G, D, pole=(-0.3, -0.8, 1.0)),
                            L=shield_arm(sc), feet=WIDE, thick=1.7)
    K.loincloth('cloth', length=0.5, width=0.18, sides=1, tatter=1.2)
    A.cuirass(t=0.06)
    K.belt('leather_dark', buckle='trim', t=0.09)
    for side in ('R', 'L'):
        A.pauldron(side, size=1.15, lames=2)
        A.greave(side, t=0.03)
    C, u = skull(ch, M, jaw_open=0.15)
    add_head(ch, helms.burgonet(C, u, M, ch.coll, cheeks=True, peak=True, comb=True, mat='dark_steel'))
    wield(ch, arms.spear(M, ch.coll, length=1.7, head_len=0.3, head_w=0.1), M, G, D, glove='bone')
    tw = arms.tower_shield(M, ch.coll, width=0.68, height=1.25, face='paint', rim='dark_steel', emblem='rot',
                           emblem_mat='linen')
    arm_shield(ch, tw, M, sc, facing=deg(2, -20), hand='bone')
    return ch, finish(ch, st, profile_of(ident), gait='heavy')


@unit('enemy_lich')
def lich(ident, title):
    from .caravan import caster
    M = palette.zone_host('city')
    M['crystal'] = M['glow']
    M2, ch, st, K, A = setup('bones', ident, title, STRIDE, dict(bulk=1.0),
                             R=PS.Grip(Vector((0.14, -0.36, 1.1)), deg(86, 2), pole=(-0.3, -0.75, 1.0)),
                             L=PS.Reach(Vector((0.5, 0.2, 1.32)), pole=(0.05, 0.7, 1.0), fingers=Vector((1, 0.1, 0.4)),
                                        palm=Vector((0, 0, 1)), pose='claw'), feet=EASY, cape=True, M=M)
    K.skirt('cloth', length=0.95, flare=0.25, t=0.012, tatter=1.5, split=0.4)
    K.gambeson('cloth_dark', t=0.032, skirt=0.3, sleeves='wrist', quilt=False, collar=False)
    K.tabard('cloth', trim='gold', emblem='rot', emblem_mat='gold', width=0.12, length=ch.Jp['knee.R'].z - 0.1,
             t=0.05, hem='tattered')
    K.belt('leather_dark', buckle='gold')
    K.cape('cape', length=ch.Jp['pelvis'].z - 0.6, flare=0.3, tatter=1.6, collar='cloth_dark')
    C, u = skull(ch, M, jaw_open=0.25)
    add_head(ch, helms.crown(C, u, M['gold'], ch.coll, points=7, height=0.45, spikes=True, gems=M['glow']))
    stf = arms.staff(M, ch.coll, length=1.95, grip_at=0.5, head='orb', crystal=M['glow'], gnarl=0.6)
    sres = ch.hold_at(stf, PS.weapon_frame(Vector((0.14, -0.36, 1.1)), deg(86, 2)), 'R')
    ch.grip(sres, 'R', M['bone'])
    ch.rest_hand('L', M['bone'], pose='claw')
    fl = arms.flame(M, ch.coll, ch.hand_frames['L'].translation + Vector((0.08, 0, 0.05)), size=1.1, color='flame')
    conv, _ = ch._conv('hand.L')
    for o in fl:
        geo.transform(o, conv)
        ch.parts.append((o, 'hand.L'))
    return ch, finish(ch, st, profile_of(ident))


@unit('enemy_mirror')
def mirror_knight(ident, title):
    G, D = ONE_HAND['G'], ONE_HAND['D']
    M = palette.zone_host('city')
    M['dark_steel'] = S.st_metal('aeb6bf', name='Mirror steel', rough=0.12, edge_col='ffffff', edge=0.6, cavity=0.35)
    M['mirror'] = S.st_metal('d6dde4', name='Mirror face', rough=0.05, edge_col='ffffff', edge=0.3, cavity=0.2)
    M2, ch, st, K, A = setup('bones', ident, title, BRACE, dict(bulk=1.08), R=PS.Grip(G, D, pole=ONE_HAND['pole']),
                             L=shield_arm(), feet=GUARD, cape=True, M=M, thick=1.6)
    K.loincloth('cloth', length=0.5, width=0.16, sides=1, tatter=1.0)
    A.cuirass(t=0.065)
    A.faulds(lames=2, t=0.08)
    K.belt('leather_dark', buckle='trim', t=0.1)
    for side in ('R', 'L'):
        A.pauldron(side, size=1.1, lames=3)
        A.vambrace(side)
        A.poleyn(side)
        A.greave(side)
    K.cape('cape', length=ch.Jp['knee.R'].z, flare=0.2, tatter=1.2)
    C, u = skull(ch, M, jaw_open=0.1)
    add_head(ch, helms.armet(C, u, M, ch.coll, mat='dark_steel', trim='trim'))
    wield(ch, arms.sword(M, ch.coll, length=0.9, width=0.1), M, G, D, glove='bone')
    arm_shield(ch, arms.heater_shield(M, ch.coll, size=1.3, emblem=None, face='mirror', rim='trim', studs=True), M,
               hand='bone')
    return ch, finish(ch, st, profile_of(ident))


@unit('enemy_tunneler')
def tunneler(ident, title):
    G, D = Vector((0.12, -0.44, 1.25)), deg(75, -25)
    M, ch, st, K, A = setup('flesh', ident, title, HUNCHED, dict(thin=0.5, arm_len=1.1, hand=1.2, hunch=12),
                            R=PS.Grip(G, D, pole=(-0.3, -0.8, 1.1)),
                            L=PS.Reach(Vector((0.5, 0.3, 0.7)), pole=(0.0, 0.8, 1.0), fingers=Vector((0.6, 0, -0.8)),
                                       palm=Vector((0, -1, -0.3)), pose='claw'),
                            feet=dict(drop=0.24, spread=0.34, stride=0.55, front='L', knees_out=0.5), flesh='8f8a72')
    ch.claws = 1.2
    K.loincloth('cloth', length=0.4, width=0.14, sides=1, tatter=1.5, t=0.03)
    K.belt('rope', buckle=None, width=0.03, t=0.03)
    K.trousers('trousers', bottom=0.4, baggy=0.4, t=0.012)
    C, u = head_frame(ch, 1.0)
    add_head(ch, faces.monster_head(C, u, M, ch.coll, kind='ghoul'))
    add_head(ch, helms.skull_cap(C, u, M, ch.coll, t=0.12, nasal=False))
    for o in arms.lantern(M, ch.coll, C + Vector((0.55 * u, 0, 0.95 * u)), hang=False, size=0.7):
        add_head(ch, [o])
    pick = arms.polearm(M, ch.coll, 'poleaxe', length=0.95, grip_at=0.3)
    wield(ch, pick, M, G, D, glove='flesh')
    ch.rest_hand('L', M['flesh'], pose='claw', size=1.1)
    return ch, finish(ch, st, 'claw-rake', gait='prowl')


@unit('enemy_revenant_captain')
def revenant_captain(ident, title):
    G, D = Vector((0.24, -0.4, 1.16)), deg(55, -20)
    M, ch, st, K, A = setup('bones', ident, title, BRACE, dict(bulk=1.12), R=PS.Grip(G, D, pole=(-0.3, -0.8, 1.0)),
                            L=PS.Reach(Vector((0.38, 0.24, 1.18)), pole=(0.0, 0.7, 1.0), fingers=Vector((1, -0.3, 0.2)),
                                       palm=Vector((0, -1, 0)), pose='open'), feet=GUARD, cape=True, thick=1.6)
    K.loincloth('cloth', length=0.55, width=0.16, sides=1, tatter=1.0)
    A.cuirass(t=0.065)
    A.faulds(lames=2, t=0.08)
    K.tabard('cloth', trim='trim', emblem='rot', emblem_mat='linen', width=0.14, length=ch.Jp['knee.R'].z + 0.02, t=0.09,
             hem='tattered')
    K.belt('leather_dark', buckle='gold', t=0.1)
    for side in ('R', 'L'):
        A.pauldron(side, size=1.15, lames=3, spikes=1)
        A.vambrace(side)
        A.poleyn(side)
        A.greave(side)
    K.cape('cape', length=ch.Jp['pelvis'].z - 0.3, flare=0.3, tatter=1.6, collar='cloth_dark')
    C, u = skull(ch, M, jaw_open=0.3)
    add_head(ch, helms.burgonet(C, u, M, ch.coll, cheeks=True, peak=True, comb=True))
    add_head(ch, helms.crown(C, u, M['bone'], ch.coll, points=2, height=0.9, z=0.75, spikes=True))
    wield(ch, arms.sword(M, ch.coll, length=1.0, width=0.11, curve=0.08), M, G, D, glove='bone')
    ch.rest_hand('L', M['bone'], pose='open')
    return ch, finish(ch, st, profile_of(ident))


# ------------------------------------------------------------------ late additions across the zones
@unit('enemy_bellringer')
def bellringer(ident, title):
    """A hooded skeletal monk tolling a hand bell overhead, a guttering soul-lantern in the other hand."""
    G, D = Vector((0.3, -0.3, 1.5)), deg(82, -10)
    M, ch, st, K, A = setup('bones', ident, title, STRIDE, dict(bulk=0.9),
                            R=PS.Grip(G, D, pole=(-0.2, -0.8, 1.0)),
                            L=PS.Reach(Vector((0.42, 0.24, 1.0)), pole=(0.0, 0.7, 0.9), fingers=Vector((0.3, 0.0, -1)),
                                       palm=Vector((-1, 0, 0)), pose='grip'), feet=EASY, thick=1.3)
    K.skirt('cloth_dark', length=0.85, flare=0.2, t=0.012, tatter=1.4)
    K.gambeson('cloth_dark', t=0.03, skirt=0.3, sleeves='wrist', quilt=False, collar=False)
    K.belt('rope', buckle=None, width=0.03, t=0.05)
    C, u = skull(ch, M, jaw_open=0.4)
    add_head(ch, helms.hood(C, u, M['cloth'], ch.coll, point=1.2, cowl=1.1))
    # the hand bell: handle in the fist, the bell hanging below it
    s_ = 1.15
    b = arms.bell(M, ch.coll, size=s_)
    for o in b['objs']:
        geo.transform(o, Matrix.Translation(Vector((0, 0, -0.3 * s_))))
    hd = garb.F.Field('Bell handle')
    hd.add(garb.F.RoundCone(Vector((0, 0, -0.06)), Vector((0, 0, 0.1)), 0.018, 0.016), 0.0)
    hd.add(garb.F.Sphere(Vector((0, 0, 0.11)), 0.024), 0.004)
    b['objs'].append(hd.mesh(0.0025, ch.coll, M['wood'], name='Bell handle'))
    wd = dict(objs=b['objs'], tip=Vector((0, 0, -0.32 * s_)), grip2=None, kind='bell', grip_r=0.017)
    res = ch.hold_at(wd, PS.weapon_frame(G, D), 'R')
    ch.grip(res, 'R', M['bone'])
    lt = arms.lantern(M, ch.coll, Vector((0, 0, 0)), hang=True, size=0.9, glow='glow', glass='lamp_glass',
                      fitting='trim', frame='dark_steel')
    lw = dict(objs=lt, tip=None, grip2=None, kind='lantern', grip_r=0.012)
    lres = ch.hold_at(lw, PS.weapon_frame(Vector((0.42, 0.24, 1.0)), Vector((0, 0, 1)), flat=(1, 0, 0)), 'L',
                      track_tip=False)
    ch.grip(lres, 'L', M['bone'])
    return ch, finish(ch, st, profile_of(ident), gait='shamble')


@unit('enemy_highwayman')
def highwayman(ident, title):
    G, D = Vector((0.3, -0.22, 1.3)), deg(3, -4)
    R, L = two_hand(G, D, 0.3, poleR=(-0.3, -0.8, 1.1), poleL=(0.2, 0.6, 1.0))
    M = human_palette('city', skin='b48464', hair='2a2018')
    M['coat'] = S.st_cloth('4e4844', name='Gray coat', shadow='2e2a28')
    M2, ch, st, K, A = setup('human', ident, title, BRACE, dict(bulk=0.98), R=R, L=L, feet=GUARD, M=M, cape=True)
    K.shirt('linen', sleeves='wrist', t=0.01)
    K.gambeson('coat', t=0.026, skirt=0.42, sleeves='wrist', quilt=False, collar=True)
    K.trousers('trousers_dark', bottom=0.55)
    K.boots('leather_dark', cuff='leather', height=0.7)
    K.belt('leather', buckle='trim', pouches=[(-75, 1.0), (60, 1.0)])
    K.strap('leather_dark', ch.Jp['shoulder.L'] + Vector((0.05, -0.04, 0.08)),
            Vector((ch.Jp['pelvis'].x + 0.1, -0.2, ch.Jp['pelvis'].z + 0.06)), width=0.04)
    K.scarf('coat', size=1.05, tail=0.35, drape=0.4)
    C, u = dress_head(ch, M, face=dict(frown=1.0, gaunt=0.3, nose=1.15, hook=0.6),
                      beard=dict(style='short', length=0.2), hair=dict(style='shaggy'))
    hat = helms.kettle(C, u, dict(dark_steel=M['leather_dark'], trim=M['leather_dark']), ch.coll, brim=1.4, t=0.12)
    for o in hat:
        geo.transform(o, Matrix.Translation(C) @ Matrix.Rotation(math.radians(-12), 4, 'X') @ Matrix.Translation(-C))
    add_head(ch, hat)
    cb = arms.crossbow(M, ch.coll)
    wield(ch, cb, M, G, D, g2=0.3)
    return ch, finish(ch, st, 'crossbow', release=[o for o in cb['objs'] if o.name.startswith('Bolt')])


@unit('enemy_drowned_harpooner')
def drowned_harpooner(ident, title):
    G, D = Vector((-0.04, -0.42, 1.36)), deg(14, -6)
    M, ch, st, K, A = setup('flesh', ident, title, BRACE, dict(bulk=1.05, belly=0.3), zone='harbor',
                            R=PS.Grip(G, D, pole=(-0.4, -0.8, 1.2)),
                            L=PS.Reach(Vector((0.42, 0.18, 1.12)), pole=(0.0, 0.7, 1.0), fingers=Vector((1, -0.2, 0.1)),
                                       palm=Vector((0, -1, 0)), pose='open'), feet=GUARD)
    K.trousers('trousers', bottom=0.45, baggy=0.8)
    K.boots('leather_dark', height=0.45)
    K.loincloth('cloth', length=0.5, width=0.2, sides=1, tatter=1.6)
    K.belt('rope', buckle=None, width=0.035, t=0.06)
    K.chain([(ch.Jp['chest'].z + 0.3, 95), (ch.Jp['chest'].z + 0.05, 10), (ch.Jp['pelvis'].z + 0.15, -70)], hug=0.012,
            mat='rope', link=0.035, thick=0.012, name='Rope coil')
    C, u = head_frame(ch, 1.0)
    add_head(ch, faces.monster_head(C, u, M, ch.coll, kind='ghoul'))
    add_head(ch, helms.bandana(C, u, M['cloth'], ch.coll, knot=True, tails=True))
    hp = arms.spear(M, ch.coll, length=2.5, head_len=0.32, head_w=0.12, grip_at=0.28, wings=True)
    wield(ch, hp, M, G, D, glove='flesh')
    ch.rest_hand('L', M['flesh'], pose='open')
    return ch, finish(ch, st, 'flask-toss', release=hp['objs'])


@unit('enemy_salt_wraith')
def salt_wraith(ident, title):
    M = palette.zone_host('harbor')
    M['wraith'] = S.st_cloth('8ea4a4', name='Sailcloth', shadow='4a5a5c', sheen=0.3)
    M2, ch, st, K, A = setup('bones', ident, title, HUNCHED, dict(bulk=0.9, thin=0.5),
                             R=PS.Reach(Vector((0.5, -0.38, 1.0)), pole=(0.0, -0.8, 1.1), fingers=Vector((0.8, 0, -0.5)),
                                        palm=Vector((0, 1, -0.4)), pose='claw'),
                             L=PS.Reach(Vector((0.6, 0.3, 1.12)), pole=(0.1, 0.8, 1.1), fingers=Vector((0.8, 0, -0.4)),
                                        palm=Vector((0, -1, -0.4)), pose='claw'), M=M, cape=True,
                             feet=dict(drop=0.06, spread=0.2, stride=0.3, front='L', knees_out=0.3))
    K.skirt('wraith', length=1.2, flare=0.32, t=0.012, tatter=2.0, top=ch.Jp['chest'].z + 0.1)
    K.cape('wraith', length=0.1, flare=0.35, tatter=2.0)
    C, u = skull(ch, M, jaw_open=0.5)
    add_head(ch, helms.hood(C, u, M['wraith'], ch.coll, point=1.0, cowl=1.2, opening=1.1))
    for side in ('R', 'L'):
        ch.rest_hand(side, M['bone'], pose='claw')
    return ch, finish(ch, st, 'claw-rake', float_mode=True)


@unit('enemy_barnacle_guard')
def barnacle_guard(ident, title):
    G, D = Vector((0.18, -0.4, 1.0)), deg(30, -12)
    sc = Vector((0.44, 0.1, 1.06))
    M = palette.zone_host('harbor')
    M['shell'] = S.coral('5a6a5a', name='Barnacle shell')
    M2, ch, st, K, A = setup('flesh', ident, title, BRACE, dict(bulk=1.25, chest_w=1.15, arm=1.2), M=M,
                             R=PS.Grip(G, D, pole=(-0.3, -0.8, 1.0)), L=shield_arm(sc), feet=WIDE)
    K.trousers('trousers', bottom=0.45, baggy=0.5)
    K.boots('leather_dark', height=0.45)
    K.loincloth('cloth', length=0.45, width=0.18, sides=1, tatter=1.4)
    K.belt('leather_dark', buckle='dark_steel', t=0.06)
    for side in ('R', 'L'):
        A.pauldron(side, size=1.3, lames=2)
    sh = garb.F.Field('Barnacles')
    rng = np.random.default_rng(4)
    for side in ('R', 'L'):
        for k in range(10):
            p = K.around([(ch.Jp['shoulder.' + side].z + rng.uniform(-0.05, 0.12),
                           (95 if side == 'L' else -95) + rng.uniform(-45, 45))], 0.06)[0]
            sh.add(garb.F.RoundCone(p, p + Vector((0, 0, 0.035)), 0.03, 0.018), 0.01)
    K.put(sh.mesh(0.003, ch.coll, M['shell'], name='Barnacles'))
    C, u = head_frame(ch, 0.95)
    add_head(ch, faces.monster_head(C, u, M, ch.coll, kind='brute'))
    wield(ch, arms.mace(M, ch.coll, haft_len=0.6, head=1.3, spikes=True), M, G, D, glove='flesh')
    anchor = garb.F.Field('Anchor shield')
    anchor.add(garb.F.Box(Vector((0, 0, 0.15)), (0.02, 0.06, 0.32), r=0.02), 0.0)
    anchor.add(garb.F.Chain([Vector((0, -0.3, -0.05)), Vector((0, -0.22, -0.2)), Vector((0, 0, -0.26)),
                             Vector((0, 0.22, -0.2)), Vector((0, 0.3, -0.05))], 0.04), 0.02)
    anchor.add(garb.F.Torus(Vector((0, 0, 0.5)), 0.07, 0.022, Matrix.Rotation(math.radians(90), 3, 'Y')), 0.01)
    anchor.add(garb.F.Box(Vector((0, 0, 0.32)), (0.02, 0.2, 0.025), r=0.015), 0.01)
    ao = anchor.mesh(0.004, ch.coll, M['dark_steel'], name='Anchor shield')
    arm_shield(ch, dict(objs=[ao], kind='shield'), M, sc, hand='flesh')
    return ch, finish(ch, st, profile_of(ident), gait='heavy')


@unit('enemy_hookblade_raider')
def hookblade_raider(ident, title):
    G, D = Vector((0.36, -0.34, 1.18)), deg(60, -10)
    G2 = Vector((0.34, 0.26, 1.06))
    M, ch, st, K, A = setup('flesh', ident, title, CROUCH_R, dict(bulk=0.98, thin=0.25), zone='harbor',
                            R=PS.Grip(G, D, pole=(-0.2, -0.8, 1.0)), L=PS.Grip(G2, deg(40, 20), pole=(0.0, 0.8, 0.9)),
                            feet=dict(drop=0.2, spread=0.34, stride=0.6, front='L', knees_out=0.45))
    K.shirt('linen', sleeves='elbow', t=0.01)
    K.gambeson('cloth', t=0.024, skirt=0.25, sleeves='none', quilt=False, collar=True)
    K.trousers('trousers', bottom=0.5, baggy=0.4)
    K.boots('leather_dark', height=0.5)
    K.belt('leather', buckle='trim')
    C, u = head_frame(ch, 1.0)
    add_head(ch, faces.monster_head(C, u, M, ch.coll, kind='ghoul'))
    add_head(ch, helms.bandana(C, u, M['cloth_dark'], ch.coll))
    for side, Gx, Dx in (('R', G, D), ('L', G2, deg(40, 20))):
        hk = arms.sword(M, ch.coll, length=0.5, width=0.07, curve=0.35, single=True, guard=0.1)
        res = ch.hold_at(hk, PS.weapon_frame(Gx, Dx), side, track_tip=side == 'R')
        ch.grip(res, side, M['flesh'])
    return ch, finish(ch, st, 'blade-stab', gait='prowl')


@unit('enemy_cinder_spitter')
def cinder_spitter(ident, title):
    G, D = Vector((-0.02, -0.44, 1.4)), deg(70, -40)
    M, ch, st, K, A = setup('bones', ident, title, BRACE, dict(bulk=0.98), zone='foundry',
                            R=PS.Reach(G, pole=(-0.4, -0.7, 1.2), fingers=Vector((0, 0, 1)), palm=Vector((1, 0, 0)),
                                       pose='cup'),
                            L=PS.Reach(Vector((0.4, 0.2, 1.06)), pole=(0.0, 0.7, 1.0), fingers=Vector((0.8, 0, -0.4)),
                                       palm=Vector((0, -1, 0)), pose='claw'), feet=GUARD)
    K.loincloth('cloth', length=0.45, width=0.15, sides=1, tatter=1.3)
    waist_wrap(K, ch, M, mat='cloth_dark', girth=1.35)
    K.belt('leather_dark', buckle='trim', pouches=[(-80, 1.0)])
    C, u = skull(ch, M, jaw_open=0.6)
    J = ch.Jp
    glow_at(K, ch, M, J['chest'] + Vector((0.0, 0, 0.06)), 0.1, 'glow', 'chest', 'Furnace core')
    ch.rest_hand('R', M['bone'], pose='cup')
    ch.rest_hand('L', M['bone'], pose='claw')
    fl = arms.flame(M, ch.coll, ch.hand_frames['R'].translation + Vector((0.0, 0, 0.1)), size=1.0, color='flame')
    conv, _ = ch._conv('hand.R')
    for o in fl:
        geo.transform(o, conv)
        ch.parts.append((o, 'hand.R'))
    return ch, finish(ch, st, 'flask-toss', release=fl)


@unit('enemy_furnace_knight')
def furnace_knight(ident, title):
    G, D = SHOULDERED
    R, L = two_hand(G, D, -0.3)
    M = palette.zone_host('foundry')
    M['dark_steel'] = S.ember_metal('2c2522', glow='ff7a2a', name='Ember iron', strength=7.0, crack=0.025, coverage=0.45)
    M2, ch, st, K, A = setup('bones', ident, title, BRACE, dict(bulk=1.2, scale=1.04), R=R, L=L, feet=WIDE, M=M,
                             thick=1.8)
    K.loincloth('cloth', length=0.5, width=0.18, sides=1, tatter=1.0, t=0.06)
    A.cuirass(t=0.075)
    A.faulds(lames=3, t=0.09)
    K.belt('leather_dark', buckle='trim', t=0.1)
    for side in ('R', 'L'):
        A.pauldron(side, size=1.3, lames=3)
        A.couter(side, size=1.15)
        A.vambrace(side, t=0.04)
        A.poleyn(side, size=1.1)
        A.greave(side, t=0.04)
    C, u = skull(ch, M, jaw_open=0.0)
    add_head(ch, helms.great_helm(C, u, M, ch.coll, cross=False, t=0.25, mat='dark_steel'))
    glow_at(K, ch, M, C + Vector((0.95 * u, 0, 0.05 * u)), 0.06 * u * 3, 'glow', 'head', 'Visor fire')
    wield(ch, arms.hammer(M, ch.coll, haft_len=1.1, head=1.6), M, G, D, glove='dark_steel', g2=-0.3)
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.3)


@unit('enemy_powder_bandit')
def powder_bandit(ident, title):
    G, D = Vector((0.0, -0.44, 1.36)), deg(78, -35)
    M = human_palette('foundry', skin='a87858', hair='1e1a18')
    M['mask'] = S.st_cloth('5a5652', name='Gray mask', shadow='3a3634')
    M2, ch, st, K, A = setup('human', ident, title, BRACE, dict(bulk=0.98), M=M, R=PS.Grip(G, D, pole=(-0.4, -0.7, 1.2)),
                             L=PS.Grip(Vector((0.38, 0.2, 1.1)), deg(60, 10), pole=(0.0, 0.7, 1.0)), feet=GUARD)
    K.shirt('linen', sleeves='wrist', t=0.01)
    K.gambeson('mask', t=0.026, skirt=0.3, sleeves='forearm', quilt=False)
    K.trousers('trousers_dark', bottom=0.5, baggy=0.5)
    K.boots('leather_dark', cuff='leather', height=0.6)
    K.belt('leather', buckle='trim', pouches=[(-75, 1.2), (-40, 1.0), (60, 1.1)])
    K.satchel('leather', side='L')
    C, u = dress_head(ch, M, face=dict(frown=1.0), hair=dict(style='short'))
    add_head(ch, helms.bandana(C, u, M['mask'], ch.coll))
    mk = garb.F.Field('Face mask')
    hf = faces.LAST['field']
    lo, hi = np.asarray(tuple(C)) - 2 * u, np.asarray(tuple(C)) + 2 * u
    mk.add(garb.F.Fn(lambda P: np.abs(hf.eval(P) - 0.08 * u) - 0.04 * u, lo, hi), 0.0)
    mk.keep(garb.F.Fn(lambda P: np.maximum.reduce([(P[:, 2] - (C.z - 0.25 * u)), (C.z - 1.25 * u) - P[:, 2],
                                                   (C.x + 0.0 * u) - P[:, 0]]), lo, hi), 0.02 * u)
    add_head(ch, [mk.mesh(0.003, ch.coll, M['mask'], name='Face mask')])
    bm = arms.bomb(M, ch.coll, size=1.8)
    wield(ch, bm, M, G, D)
    tr = arms.staff(M, ch.coll, length=0.45, grip_at=0.4, head='orb', crystal=M['glow'], gnarl=0.0)
    res = ch.hold_at(tr, PS.weapon_frame(Vector((0.38, 0.2, 1.1)), deg(60, 10)), 'L', track_tip=False)
    ch.grip(res, 'L', M['skin'])
    return ch, finish(ch, st, 'flask-toss', release=bm['objs'])


@unit('enemy_miasma_priest')
def miasma_priest(ident, title):
    G, D = Vector((0.16, -0.36, 1.1)), deg(84, 3)
    M, ch, st, K, A = setup('flesh', ident, title, STRIDE, dict(bulk=0.95, thin=0.3), zone='quarantine',
                            R=PS.Grip(G, D, pole=(-0.3, -0.75, 1.0)),
                            L=PS.Reach(Vector((0.46, 0.2, 1.26)), pole=(0.05, 0.7, 1.0), fingers=Vector((1, 0.1, 0.3)),
                                       palm=Vector((0, 0, 1)), pose='open'), feet=EASY, cape=True)
    K.skirt('cloth', length=0.95, flare=0.22, t=0.012, tatter=1.0, split=0.35)
    K.gambeson('cloth_dark', t=0.03, skirt=0.3, sleeves='wrist', quilt=False, collar=False)
    K.belt('leather_dark', buckle='trim', width=0.08, t=0.075, z=ch.Jp['pelvis'].z + 0.1, pouches=[(-70, 0.9)])
    K.cape('cloth', length=ch.Jp['pelvis'].z - 0.5, flare=0.2, tatter=1.0)
    C, u = head_frame(ch, 1.0)
    add_head(ch, faces.monster_head(C, u, M, ch.coll, kind='ghoul'))
    add_head(ch, helms.hood(C, u, M['cloth'], ch.coll, point=0.7, cowl=1.0, opening=0.9))
    stf = arms.staff(M, ch.coll, length=2.0, grip_at=0.5, head='lantern', glow='glow', glass='lamp_glass', gnarl=0.4)
    sres = ch.hold_at(stf, PS.weapon_frame(G, D), 'R')
    ch.grip(sres, 'R', M['flesh'])
    ch.rest_hand('L', M['flesh'], pose='open')
    return ch, finish(ch, st, 'staff-cast')


@unit('enemy_rot_surgeon')
def rot_surgeon(ident, title):
    G, D = Vector((0.3, -0.38, 1.12)), deg(30, -15)
    M, ch, st, K, A = setup('flesh', ident, title, STRIDE, dict(bulk=1.0, belly=0.3), zone='quarantine',
                            R=PS.Grip(G, D, pole=(-0.3, -0.8, 1.0)),
                            L=PS.Grip(Vector((0.36, 0.2, 1.2)), deg(80, 0), pole=(0.0, 0.7, 1.0)), feet=EASY)
    K.shirt('linen', sleeves='wrist', t=0.012)
    K.gambeson('cloth_dark', t=0.026, skirt=0.4, sleeves='wrist', quilt=False)
    K.loincloth('linen', length=0.6, width=0.15, back=False, tatter=0.6, t=0.07)
    K.trousers('trousers_dark', bottom=0.5)
    K.boots('leather_dark', height=0.6)
    K.belt('leather', buckle='trim', pouches=[(-80, 1.0), (60, 1.0)])
    C, u = head_frame(ch, 1.0)
    add_head(ch, faces.monster_head(C, u, M, ch.coll, kind='ghoul'))
    bk = garb.F.Field('Beak mask')
    bk.add(garb.F.RoundCone(C + Vector((0.6 * u, 0, -0.2 * u)), C + Vector((1.9 * u, 0, -0.7 * u)), 0.45 * u, 0.06 * u),
           0.0)
    add_head(ch, [bk.mesh(0.003, ch.coll, M['leather'], name='Beak mask')])
    add_head(ch, helms.hood(C, u, M['cloth_dark'], ch.coll, point=0.3, cowl=0.9, opening=1.1))
    saw = arms.sword(M, ch.coll, length=0.55, width=0.1, guard=0.08, single=True, blade='blade')
    wield(ch, saw, M, G, D, glove='leather_dark')
    vial = arms.flask(M, ch.coll, size=1.2, liquid='plague')
    vres = ch.hold_at(vial, PS.weapon_frame(Vector((0.36, 0.2, 1.2)), deg(80, 0)), 'L', track_tip=False)
    ch.grip(vres, 'L', M['leather_dark'])
    return ch, finish(ch, st, 'staff-cast')


@unit('enemy_sealbreaker')
def sealbreaker(ident, title):
    G, D = Vector((0.08, -0.22, 0.92)), deg(18, 8)
    R, L = two_hand(G, D, 0.55)
    M = human_palette('quarantine', skin='b08a6a')
    M['zealot'] = S.st_cloth('6a6660', name='Zealot gray', shadow='3e3c38')
    M2, ch, st, K, A = setup('human', ident, title, LUNGE, dict(bulk=1.0), R=R, L=L, M=M,
                             feet=dict(drop=0.16, spread=0.34, stride=0.62, front='L', knees_out=0.45))
    K.shirt('linen', sleeves='wrist', t=0.01)
    K.gambeson('zealot', t=0.026, skirt=0.4, sleeves='wrist', quilt=True)
    K.trousers('trousers_dark', bottom=0.5)
    K.boots('leather_dark', height=0.6)
    K.belt('rope', buckle=None, width=0.03, t=0.05)
    K.scarf('zealot', size=1.0, tail=0.3, drape=0.6)
    C, u = dress_head(ch, M, face=dict(frown=1.0))
    mk = garb.F.Field('Zealot mask')
    hf = faces.LAST['field']
    lo, hi = np.asarray(tuple(C)) - 2 * u, np.asarray(tuple(C)) + 2 * u
    mk.add(garb.F.Fn(lambda P: np.abs(hf.eval(P) - 0.1 * u) - 0.05 * u, lo, hi), 0.0)
    mk.keep(garb.F.Fn(lambda P: (C.x + 0.2 * u) - P[:, 0], lo, hi), 0.02 * u)
    for sy in (-1, 1):
        mk.cut(garb.F.Ellipsoid(C + Vector((0.9 * u, sy * 0.32 * u, 0.0)), (0.3 * u, 0.12 * u, 0.06 * u)), 0.01 * u)
    add_head(ch, [mk.mesh(0.003, ch.coll, M['steel'], name='Zealot mask')])
    add_head(ch, helms.hood(C, u, M['zealot'], ch.coll, point=0.5, cowl=1.0))
    sp = arms.polearm(M, ch.coll, 'bill', length=2.2)
    wield(ch, sp, M, G, D, g2=0.55)
    return ch, finish(ch, st, 'spear-thrust')


@unit('enemy_ossuary_archer')
def ossuary_archer(ident, title):
    aim = deg(6, -6)
    bow_c = Vector((0.6, -0.02, 1.48))
    draw = Vector((0.02, -0.13, 1.6))
    M, ch, st, K, A = setup('bones', ident, title, BRACE, dict(bulk=0.95), zone='basilica',
                            R=PS.Reach(draw, pole=(-0.45, -0.35, 1.75), fingers=aim, palm=Vector((0, 1, -0.3)),
                                       pose='grip'),
                            L=PS.Grip(bow_c, Vector((0.08, 0.05, 1)), pole=(0.2, 0.6, 1.2)), feet=GUARD, cape=True)
    K.loincloth('cloth', length=0.5, width=0.15, sides=1, tatter=1.2)
    waist_wrap(K, ch, M, mat='cloth_dark', girth=1.35)
    K.belt('leather_dark', buckle='trim')
    K.quiver(mat='bone', fletch='bone', trim='trim')
    K.cape('cape', length=ch.Jp['pelvis'].z - 0.1, flare=0.15, tatter=1.4)
    C, u = skull(ch, M, jaw_open=0.2)
    add_head(ch, helms.hood(C, u, M['cloth'], ch.coll, point=0.5, cowl=0.9))
    bw = arms.bow(M, ch.coll, height=1.7, recurve=0.1, wood='bone')
    bres = ch.hold_at(bw, PS.frame(bow_c, aim, Vector((0.08, 0.05, 1))), 'L', track_tip=False)
    ch.grip(bres, 'L', M['bone'])
    ch.draw_hand(bres, M['bone'])
    ar = arms.arrow(M, ch.coll, length=0.92, fletch=M['bone'], shaft='bone')
    Ma = PS.weapon_frame(ch.draw_frame.translation + Vector((0.0, 0.0, -0.01)), aim)
    conv, _ = ch._conv('hand.L')
    for o in ar['objs']:
        geo.transform(o, conv @ Ma)
        ch.parts.append((o, 'grip.R'))
    ch.tip_rest = conv @ (Ma @ ar['tip'])
    ch.tip_bone = 'hand.L'
    return ch, finish(ch, st, 'bow-draw', release=ar['objs'])


@unit('enemy_bone_cantor')
def bone_cantor(ident, title):
    M, ch, st, K, A = setup('bones', ident, title, STRIDE, dict(bulk=0.95), zone='basilica', cape=True,
                            R=PS.Reach(Vector((0.42, -0.3, 1.4)), pole=(-0.1, -0.8, 1.0), fingers=Vector((0.6, 0, 0.8)),
                                       palm=Vector((0, 0.3, 1)), pose='open'),
                            L=PS.Reach(Vector((0.46, 0.28, 1.42)), pole=(0.1, 0.8, 1.0), fingers=Vector((0.6, 0, 0.8)),
                                       palm=Vector((0, -0.3, 1)), pose='open'), feet=EASY)
    K.skirt('linen', length=0.95, flare=0.22, t=0.012, tatter=0.8, split=0.3)
    K.gambeson('cloth', t=0.03, skirt=0.3, sleeves='wrist', quilt=False, collar=False)
    K.tabard('linen', trim='gold', emblem='rot', emblem_mat='gold', width=0.12, length=ch.Jp['knee.R'].z - 0.05, t=0.05)
    K.belt('leather_dark', buckle='gold')
    K.cape('cloth', length=ch.Jp['pelvis'].z - 0.4, flare=0.2, tatter=1.0, collar='gold')
    C, u = skull(ch, M, jaw_open=0.8)
    add_head(ch, helms.crown(C, u, M['gold'], ch.coll, points=6, height=0.3))
    # the hymn: soul-light in the open jaw
    glow_at(K, ch, M, C + Vector((0.55 * u, 0, -0.45 * u)), 0.18 * u, 'glow', 'head', 'Hymn light')
    for side in ('R', 'L'):
        ch.rest_hand(side, M['bone'], pose='open')
    return ch, finish(ch, st, 'staff-cast')


@unit('enemy_chain_penitent')
def chain_penitent(ident, title):
    G, D = Vector((0.12, -0.46, 1.1)), deg(-50, -40)
    M, ch, st, K, A = setup('flesh', ident, title, HUNCHED, dict(bulk=1.18, arm=1.2, forearm=1.2), zone='basilica',
                            R=PS.Grip(G, D, pole=(-0.3, -0.8, 1.0)),
                            L=PS.Reach(Vector((0.4, 0.32, 0.98)), pole=(0.0, 0.8, 1.0), fingers=Vector((0.6, 0, -0.7)),
                                       palm=Vector((0, -1, 0)), pose='fist'), feet=WIDE)
    K.skirt('cloth', length=0.8, flare=0.18, t=0.012, tatter=1.2, top=ch.Jp['pelvis'].z + 0.05)
    K.belt('rope', buckle=None, width=0.04, t=0.05)
    for side in ('R', 'L'):
        K.bracers('linen', sides=(side,), start=0.1, end=0.95)
    C, u = head_frame(ch, 1.0)
    add_head(ch, faces.monster_head(C, u, M, ch.coll, kind='ghoul'))
    hood = helms.hood(C, u, M['cloth'], ch.coll, point=0.8, cowl=1.1, opening=0.8)
    add_head(ch, hood)
    cd = garb.F.Field('Candles')
    for k in range(5):
        a = math.radians(-60 + k * 30)
        p = C + Vector((-0.3 * u + math.cos(a) * 0.9 * u * 0.2, math.sin(a) * 0.95 * u, 1.05 * u))
        cd.add(garb.F.RoundCone(p, p + Vector((0, 0, 0.06)), 0.012, 0.011), 0.0)
    add_head(ch, [cd.mesh(0.002, ch.coll, M['linen'], name='Candles')])
    fz = garb.F.Field('Candle flames')
    for k in range(5):
        a = math.radians(-60 + k * 30)
        p = C + Vector((-0.3 * u + math.cos(a) * 0.9 * u * 0.2, math.sin(a) * 0.95 * u, 1.05 * u)) + Vector((0, 0, 0.075))
        fz.add(garb.F.RoundCone(p, p + Vector((0, 0, 0.03)), 0.009, 0.001), 0.0)
    add_head(ch, [fz.mesh(0.0015, ch.coll, M['glow'], name='Candle flames', smooth=1)])
    Gw = G + D * 0.0
    K.chain([Gw + Vector((0.0, 0, 0.0)), Gw + D * 0.3 + Vector((0.05, 0, -0.1)), Vector((Gw.x + 0.4, Gw.y, 0.08)),
             Vector((Gw.x + 0.7, Gw.y + 0.05, 0.04))], link=0.06, thick=0.014, bone='hand.R', name='Penitent chain')
    ch.rest_hand('R', M['flesh'], pose='fist', size=1.1)
    ch.rest_hand('L', M['flesh'], pose='fist', size=1.1)
    return ch, finish(ch, st, 'heavy-smash', gait='shamble')


@unit('enemy_dust_reaver')
def dust_reaver(ident, title):
    G, D = Vector((-0.04, -0.44, 1.4)), deg(14, -6)
    M, ch, st, K, A = setup('bones', ident, title, BRACE, dict(bulk=0.98), zone='steppe', cape=True,
                            R=PS.Grip(G, D, pole=(-0.4, -0.8, 1.2)),
                            L=PS.Reach(Vector((0.4, 0.2, 1.1)), pole=(0.0, 0.7, 1.0), fingers=Vector((1, -0.2, 0.1)),
                                       palm=Vector((0, -1, 0)), pose='open'), feet=GUARD)
    K.loincloth('cloth', length=0.5, width=0.16, sides=2, tatter=1.6)
    waist_wrap(K, ch, M, mat='cloth_dark', girth=1.35)
    K.belt('leather_dark', buckle='trim')
    K.cape('cloth', length=ch.Jp['pelvis'].z, flare=0.2, tatter=1.8)
    C, u = skull(ch, M, jaw_open=0.2)
    add_head(ch, helms.hood(C, u, M['cloth'], ch.coll, point=0.4, cowl=1.0))
    J = ch.Jp
    back = K._surface_point(J['chest'], Vector((-1, 0, 0)), K.base(['torso']), 0.06)
    for k in range(3):
        jv = arms.spear(M, ch.coll, length=1.2, head_len=0.2, head_w=0.07, grip_at=0.5, langets=False, butt=False)
        Mj = PS.weapon_frame(back + Vector((-0.05, -0.08 + k * 0.06, 0.0)), deg(70, 180 - 20 + k * 10))
        for o in jv['objs']:
            geo.transform(o, Mj)
            K.rigid(o, 'chest')
    jv = arms.spear(M, ch.coll, length=1.3, head_len=0.2, head_w=0.07, grip_at=0.4, langets=False)
    wield(ch, jv, M, G, D, glove='bone')
    ch.rest_hand('L', M['bone'], pose='open')
    return ch, finish(ch, st, 'flask-toss', release=jv['objs'])


@unit('enemy_carrion_banner')
def carrion_banner(ident, title):
    G, D = Vector((0.12, -0.34, 1.0)), deg(86, 0)
    M, ch, st, K, A = setup('bones', ident, title, BRACE, dict(bulk=1.0), zone='steppe',
                            R=PS.Grip(G, D, pole=(-0.3, -0.75, 1.0)),
                            L=PS.Reach(Vector((0.36, 0.22, 1.02)), pole=(0.0, 0.7, 1.0), fingers=Vector((0.6, 0, -0.6)),
                                       palm=Vector((0, -1, 0)), pose='fist'), feet=GUARD)
    K.loincloth('cloth', length=0.5, width=0.16, sides=1, tatter=1.6)
    A.cuirass(t=0.05)
    K.belt('leather_dark', buckle='trim', t=0.08)
    A.pauldron('R', size=1.0, lames=2, spikes=2)
    C, u = skull(ch, M, jaw_open=0.4)
    add_head(ch, helms.skull_cap(C, u, M, ch.coll, t=0.12))
    std = arms.standard(M, ch.coll, M['cloth'], emblem='skull', emblem_mat='bone', height=2.6, finial='spike',
                        trim=None, tatter=1.8, width=0.55, drop=0.8)
    sres = ch.hold_at(std, PS.weapon_frame(G, D), 'R')
    sk = garb.F.Field('Hanging skulls')
    top = G + D * (2.6 * 0.7 - 0.12)
    for k in range(3):
        c = top + Vector((0.12 + k * 0.17, 0.0, -0.92 - 0.05 * (k % 2)))
        sk.add(garb.F.Ellipsoid(c, (0.06, 0.05, 0.065)), 0.0)
    conv, _ = ch._conv('hand.R')
    o = sk.mesh(0.003, ch.coll, M['bone'], name='Hanging skulls')
    geo.transform(o, conv)
    ch.parts.append((o, 'hand.R'))
    ch.grip(sres, 'R', M['bone'])
    ch.rest_hand('L', M['bone'], pose='fist')
    return ch, finish(ch, st, profile_of(ident))


@unit('enemy_war_drummer')
def war_drummer(ident, title):
    M, ch, st, K, A = setup('bones', ident, title, BRACE, dict(bulk=1.0), zone='steppe', cape=True,
                            R=PS.Grip(Vector((0.32, -0.2, 1.24)), deg(30, 20), pole=(-0.3, -0.7, 1.2)),
                            L=PS.Grip(Vector((0.3, 0.24, 1.2)), deg(30, -20), pole=(0.0, 0.7, 1.2)), feet=GUARD)
    K.loincloth('cloth', length=0.5, width=0.16, sides=1, tatter=1.4)
    waist_wrap(K, ch, M, mat='cloth_dark', girth=1.35)
    K.belt('leather_dark', buckle='trim', t=0.06)
    K.cape('cloth', length=ch.Jp['pelvis'].z + 0.2, width=0.8, flare=0.15, tatter=1.4)
    C, u = skull(ch, M, jaw_open=0.5)
    add_head(ch, helms.kettle(C, u, M, ch.coll, brim=0.6))
    J = ch.Jp
    dc = Vector((J['pelvis'].x + 0.3, -0.02, J['pelvis'].z + 0.05))
    dr = garb.F.Field('War drum')
    dr.add(garb.F.RoundCone(dc - Vector((0, 0, 0.16)), dc + Vector((0, 0, 0.16)), 0.25, 0.25), 0.0)
    K.rigid(dr.mesh(0.005, ch.coll, M['wood'], name='War drum'), 'hips')
    hd = garb.F.Field('Drum heads')
    for z in (-0.17, 0.17):
        hd.add(garb.F.Torus(dc + Vector((0, 0, z)), 0.235, 0.022), 0.0)
    hd.add(garb.F.RoundCone(dc + Vector((0, 0, 0.165)), dc + Vector((0, 0, 0.17)), 0.23, 0.23), 0.0)
    K.rigid(hd.mesh(0.004, ch.coll, M['linen'], name='Drum heads'), 'hips')
    for side, Gx, Dx in (('R', Vector((0.32, -0.2, 1.24)), deg(30, 20)), ('L', Vector((0.3, 0.24, 1.2)), deg(30, -20))):
        stick = arms.staff(M, ch.coll, length=0.42, grip_at=0.3, head='orb', crystal=M['bone'], gnarl=0.0)
        res = ch.hold_at(stick, PS.weapon_frame(Gx, Dx), side, track_tip=side == 'R')
        ch.grip(res, side, M['bone'])
    return ch, finish(ch, st, 'hammer-command')


@unit('enemy_crown_executioner')
def crown_executioner(ident, title):
    G, D = SHOULDERED
    R, L = two_hand(G, D, -0.25)
    M = human_palette('citadel', skin='a88a72')
    M2, ch, st, K, A = setup('human', ident, title, BRACE, dict(bulk=1.25, chest_w=1.1, belly=0.3, arm=1.2), R=R, L=L,
                             M=M, feet=WIDE, cape=True)
    K.shirt('trousers_dark', sleeves='none', t=0.008)
    K.trousers('trousers_dark', bottom=0.5, baggy=0.4)
    K.boots('leather_dark', cuff='leather', height=0.6)
    K.loincloth('leather_dark', length=0.55, width=0.2, tatter=0.2, t=0.08)
    K.belt('leather_dark', buckle='dark_steel', width=0.1, t=0.09)
    K.cape('cloth', length=ch.Jp['pelvis'].z, width=0.6, flare=0.15, tatter=0.6, collar='cloth_dark')
    for side in ('R', 'L'):
        K.bracers('leather_dark', sides=(side,))
    C, u = dress_head(ch, M, face=dict(frown=1.0, jaw=1.2))
    add_head(ch, helms.hood(C, u, M['trousers_dark'], ch.coll, point=1.0, cowl=0.8, opening=0.6))
    cl = arms.axe(M, ch.coll, haft_len=1.1, head=2.2, beard=True, two_hand=True, steel='dark_steel')
    wield(ch, cl, M, G, D, glove='skin', g2=-0.25)
    return ch, finish(ch, st, 'axe-cleave', gait='heavy', heavy=1.3)


@unit('enemy_null_magister')
def null_magister(ident, title):
    M = human_palette('citadel', skin='b8a088', hair='5a5048')
    M['academic'] = S.st_cloth('2c2c34', name='Academic coat', shadow='18181e')
    M['rune'] = S.emissive('c8d0ff', 5, name='Null rune', core='ffffff')
    M2, ch, st, K, A = setup('human', ident, title, STRIDE, dict(bulk=0.92), M=M,
                             R=PS.Reach(Vector((0.44, -0.3, 1.28)), pole=(-0.1, -0.8, 1.0), fingers=Vector((1, 0, 0.3)),
                                        palm=Vector((0, 0, -1)), pose='open'),
                             L=PS.Reach(Vector((0.3, 0.26, 1.08)), pole=(0.0, 0.7, 1.0), fingers=Vector((0.6, 0, -0.6)),
                                        palm=Vector((0, -1, 0)), pose='relaxed'), feet=EASY)
    K.shirt('linen', sleeves='wrist', t=0.01)
    K.skirt('academic', length=0.95, flare=0.18, t=0.012, split=0.45)
    K.gambeson('academic', t=0.028, skirt=0.3, sleeves='wrist', quilt=False)
    K.belt('leather_dark', buckle='trim', pouches=[(-80, 0.9)])
    K.boots('leather_dark', height=0.5)
    C, u = dress_head(ch, M, face=dict(frown=0.8, age=0.5, gaunt=0.3, nose=1.15),
                      beard=dict(style='goatee', length=0.6), hair=dict(style='swept'))
    disc = garb.F.Field('Rune disc')
    Rw = ch.hand_frames['R'].translation
    dc = Rw + Vector((0.18, 0, 0.12))
    disc.add(garb.F.Torus(dc, 0.13, 0.016, Matrix.Rotation(math.radians(90), 3, 'Y')), 0.0)
    disc.add(garb.F.RoundCone(dc - Vector((0.008, 0, 0)), dc + Vector((0.008, 0, 0)), 0.1, 0.1), 0.004)
    conv, _ = ch._conv('hand.R')
    o = disc.mesh(0.003, ch.coll, M['steel'], name='Rune disc')
    geo.transform(o, conv)
    ch.parts.append((o, 'hand.R'))
    ch.rest_hand('R', M['skin'], pose='open')
    ch.rest_hand('L', M['skin'], pose='relaxed')
    return ch, finish(ch, st, 'staff-cast')


@unit('enemy_soul_standard')
def soul_standard(ident, title):
    G, D = Vector((0.12, -0.34, 1.0)), deg(86, 0)
    M, ch, st, K, A = setup('bones', ident, title, BRACE, dict(bulk=1.0), zone='citadel',
                            R=PS.Grip(G, D, pole=(-0.3, -0.75, 1.0)),
                            L=PS.Grip(G + D * 0.45, D, pole=(0.0, 0.7, 1.1)), feet=GUARD)
    K.loincloth('cloth', length=0.55, width=0.16, sides=1, tatter=1.2)
    A.cuirass(t=0.05)
    K.belt('leather_dark', buckle='trim', t=0.08)
    C, u = skull(ch, M, jaw_open=0.2)
    add_head(ch, helms.skull_cap(C, u, M, ch.coll, t=0.12))
    std = arms.standard(M, ch.coll, M['cloth'], emblem='rot', emblem_mat='linen', height=2.6, finial='spike',
                        trim=None, tatter=1.0, width=0.45, drop=0.65)
    sres = ch.hold_at(std, PS.weapon_frame(G, D), 'R')
    sres['grip2'] = Vector((0, 0, 0.45))
    cg = garb.F.Field('Soul cage')
    top = G + D * (2.6 * 0.7 + 0.15)
    for k in range(5):
        a = k / 5 * math.tau
        cg.add(garb.F.Chain([top + Vector((0, 0, -0.12)), top + Vector((math.cos(a) * 0.09, math.sin(a) * 0.09, 0.0)),
                             top + Vector((0, 0, 0.14))], 0.008), 0.004)
    conv, _ = ch._conv('hand.R')
    o = cg.mesh(0.002, ch.coll, M['dark_steel'], name='Soul cage')
    geo.transform(o, conv)
    ch.parts.append((o, 'hand.R'))
    gl = garb.F.Field('Soul vessel')
    gl.add(garb.F.Sphere(top, 0.06), 0.0)
    o = gl.mesh(0.002, ch.coll, M['glow'], name='Soul vessel', smooth=1)
    geo.transform(o, conv)
    ch.parts.append((o, 'hand.R'))
    ch.grip(sres, 'R', M['bone'])
    ch.grip2(sres, M['bone'])
    return ch, finish(ch, st, 'staff-cast')


@unit('enemy_ash_pilgrim')
def ash_pilgrim(ident, title):
    G, D = Vector((0.22, -0.3, 1.0)), deg(80, 4)
    M = human_palette('city', skin='9a8a7a', hair='6a6460')
    M['ash'] = S.st_cloth('6e665e', name='Ash robe', shadow='443e38')
    M2, ch, st, K, A = setup('human', ident, title, HUNCHED, dict(bulk=0.82, scale=0.86, hunch=18), M=M,
                             R=PS.Grip(G, D, pole=(-0.3, -0.7, 1.0)),
                             L=PS.Reach(Vector((0.3, 0.2, 0.9)), pole=(0.0, 0.7, 1.0), fingers=Vector((0.6, 0, -0.6)),
                                        palm=Vector((0, -1, 0)), pose='relaxed'), feet=EASY)
    K.skirt('ash', length=0.9, flare=0.2, t=0.012, tatter=1.0)
    K.gambeson('ash', t=0.03, skirt=0.3, sleeves='wrist', quilt=False)
    K.belt('rope', buckle=None, width=0.03, t=0.05)
    C, u = dress_head(ch, M, face=dict(age=1.0, frown=0.2, nose=1.3), beard=dict(style='full', length=0.9))
    add_head(ch, helms.hood(C, u, M['ash'], ch.coll, point=0.6, cowl=1.1, opening=0.9))
    stf = arms.staff(M, ch.coll, length=1.6, grip_at=0.5, head='lantern', gnarl=0.8)
    sres = ch.hold_at(stf, PS.weapon_frame(G, D), 'R')
    ch.grip(sres, 'R', M['skin'])
    ch.rest_hand('L', M['skin'], pose='relaxed')
    return ch, finish(ch, st, profile_of(ident), gait='shamble')
