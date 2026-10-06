"""Lantern Caravan roster: humanoid soldiers, casters and specialists."""
import math

from mathutils import Matrix, Vector

from rk import armor as A, geo, heads as H, palette, shaders as S, undead as U, weapons as W
from rk.anim import P, STANCES
from rk.character import Character
from rk.rig import LAT, ROLL, add_poses

from . import unit
from .common import (add_head, boots, finish, gloves, head_frame, pelt, plate_arms, plate_legs, profile_of,
                     soldier_body)


def _cape(sk, J):
    A.cape_bones(sk, J)


def lantern_emblem(M):
    return W.emblem_lantern(M['gold'], M['glow'])


def nock_arrow(ch, bow_res, M, fletch=None):
    """Arrow resting on the bow, tail at the nock, carried by the drawing hand's IK target."""
    ar = W.arrow(M, ch.coll, length=0.78, fletch=fletch)
    nock = bow_res['nock']
    m = bow_res['matrix'] @ Matrix.Translation(nock) @ Matrix.Rotation(math.radians(90), 4, 'Y')
    for o in ar['objs']:
        geo.transform(o, m)
        ch.parts.append((o, 'grip.R'))
    return ar['objs']


def on_back(ch, objs, offset, rot_z=180, rot_y=0, bone='chest'):
    m = Matrix.Translation(ch.J['chest'] + Vector(offset)) @ Matrix.Rotation(math.radians(rot_z), 4, 'Z') @ \
        Matrix.Rotation(math.radians(rot_y), 4, 'Y')
    for o in objs:
        geo.transform(o, m)
        ch.parts.append((o, bone))


def hand_wisp(ch, mat, r=0.05):
    ch.add_rigid([geo.sphere('Hand wisp', r, ch.J['hand_tip.L'] + Vector((0.03, 0, 0.02)), mat, ch.coll, 12, 8)], 'hand.L')


# ------------------------------------------------------------------ line infantry
@unit('player_brawler')
def swordsman(ident, title):
    M = palette.lantern()
    ch = Character(ident, title, dict(bulk=1.05), extras=_cape)
    st = STANCES['sword_shield']
    ch.set_stance(st)
    soldier_body(ch, M, torso=M['linen'], sleeve=M['mail'], legs=M['trousers'])
    C, R = head_frame(ch)
    add_head(ch, H.human_head(C, R, M, ch.coll))
    add_head(ch, H.beard(C, R, M['hair'], ch.coll, length=0.25, width=0.95))
    add_head(ch, H.nasal_helm(C, R, M, ch.coll))
    ch.add(A.shell_torso('Mail hauberk', ch.s, ch.J, M['mail'], ch.coll, inflate=0.018, z_from=ch.J['pelvis'].z - 0.22))
    ch.add(A.skirt('Mail skirt', ch.J, M['mail'], ch.coll, ch.J['pelvis'].z - 0.02, ch.J['knee.R'].z + 0.12,
                   r_top=(0.2, 0.235), r_bottom=(0.25, 0.29), folds=0, fold_depth=0))
    ch.add(A.tabard(ch.J, ch.s, M['cloth'], ch.coll, width=0.19, trim=M['trim'], length=ch.J['knee.R'].z + 0.14,
                    emblem=lantern_emblem(M), hem='point'))
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=1))
    ch.add(A.cape(ch.J, ch.s, M['cape'], ch.coll, length=0.5, width=0.85, collar=M['wool']))
    plate_arms(ch, M, lames=2, couters=False, size=0.88)
    gloves(ch, M)
    for side in ('R', 'L'):
        ch.add(A.leg_plates(ch.J, side, M, ch.coll, cuisse=False, poleyn=True, greave=False))
    boots(ch, M, cuff=M['leather'])
    ch.add(A.scabbard(ch.J, M, ch.coll))
    ch.hold(W.sword(M, ch.coll, length=0.78, width=0.07, guard=0.24), 'R', pitch=58, yaw=-4)
    ch.strap(W.heater_shield(M, ch.coll, size=1.0, emblem=lantern_emblem(M)), 'L', forward=0.17, up=0.02, yaw=-22)
    return ch, finish(ch, st, profile_of(ident))


@unit('player_shooter')
def archer(ident, title):
    M = palette.lantern()
    ch = Character(ident, title, dict(bulk=0.95, chest_w=0.95))
    st = STANCES['bow']
    ch.set_stance(st)
    soldier_body(ch, M, torso=M['linen'], sleeve=M['linen'], legs=M['trousers'], glove=M['leather'])
    C, R = head_frame(ch)
    add_head(ch, H.human_head(C, R, M, ch.coll, jaw=0.85))
    add_head(ch, H.beard(C, R, M['hair'], ch.coll, length=0.05, width=0.9, mustache=False))
    add_head(ch, H.hood(C, R, M['cloth'], ch.coll, pointed=0.25, cowl=1.0, opening=1.05))
    ch.add(A.shell_torso('Leather jerkin', ch.s, ch.J, M['leather'], ch.coll, inflate=0.02, z_from=ch.J['pelvis'].z - 0.18,
                         quilt=True))
    ch.add(A.skirt('Jerkin skirt', ch.J, M['leather'], ch.coll, ch.J['pelvis'].z, ch.J['knee.R'].z + 0.2,
                   r_top=(0.2, 0.23), r_bottom=(0.23, 0.27), folds=5, split=0.7))
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=2))
    ch.add(A.quiver(ch.J, M, ch.coll, fletch=M['accent']))
    for side in ('R', 'L'):
        ch.add(A.vambrace(ch.J, side, M, ch.coll, r=0.07, mat_key='leather_dark'))
    gloves(ch, M, 'leather')
    boots(ch, M, mat=M['leather'], shaft=0.26, cuff=M['leather_dark'])
    bow = ch.hold(W.bow(M, ch.coll, height=1.3), 'L', pitch=90, yaw=0, track_tip=False)
    ch.two_hand(bow['grip2_rest'], parent='hand.L', side='R', pole=(-0.45, 0.35, 0.05))
    arrow = nock_arrow(ch, bow, M, fletch=M['accent'])
    ch.tip_rest = bow['matrix'] @ Vector((0.0, 0, 0)) + Vector((0.12, 0, 0))
    ch.tip_bone = 'hand.L'
    return ch, finish(ch, st, profile_of(ident), release=arrow)


@unit('player_defender')
def shield_knight(ident, title):
    M = palette.lantern()
    ch = Character(ident, title, dict(bulk=1.12, chest_w=1.06), extras=_cape)
    st = STANCES['sword_shield']
    ch.set_stance(st)
    soldier_body(ch, M, torso=M['mail'], sleeve=M['mail'], legs=M['mail'])
    C, R = head_frame(ch)
    add_head(ch, H.great_helm(C + Vector((0, 0, R * .08)), R * 1.02, M, ch.coll, crest=M['cloth']))
    ch.add(A.breastplate(ch.s, ch.J, M, ch.coll, fauld=3, plackart=True))
    ch.add(A.tabard(ch.J, ch.s, M['cloth'], ch.coll, width=0.17, trim=M['trim'], length=ch.J['knee.R'].z + 0.06,
                    hem='dagged', emblem=lantern_emblem(M)))
    ch.add(A.gorget(ch.J, ch.s, M, ch.coll))
    ch.add(A.cape(ch.J, ch.s, M['cape'], ch.coll, length=0.36, width=0.9, collar=M['trim']))
    plate_arms(ch, M, lames=3, size=1.05, rerebrace=True)
    gloves(ch, M, 'steel', size=1.05)
    plate_legs(ch, M, size=1.05)
    boots(ch, M, mat=M['steel'], shaft=0.06, plate=M['steel'])
    ch.hold(W.mace(M, ch.coll, haft_len=0.62, head=1.1), 'R', pitch=62, yaw=-4)
    sh = W.heater_shield(M, ch.coll, size=1.35, emblem=lantern_emblem(M), boss=False, curve=0.16)
    ch.strap(sh, 'L', forward=0.19, up=0.0, yaw=-24)
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.1)


@unit('player_spear')
def spearman(ident, title):
    M = palette.lantern()
    ch = Character(ident, title, dict(bulk=1.0))
    st = STANCES['polearm']
    ch.set_stance(st)
    soldier_body(ch, M, torso=M['cloth_dark'], sleeve=M['mail'], legs=M['trousers'])
    C, R = head_frame(ch)
    add_head(ch, H.human_head(C, R, M, ch.coll, nose_kind='long'))
    add_head(ch, H.beard(C, R, M['hair'], ch.coll, length=0.1, mustache=True))
    add_head(ch, H.kettle_hat(C + Vector((0, 0, R * .12)), R, M, ch.coll))
    ch.add(A.shell_torso('Quilted gambeson', ch.s, ch.J, M['cloth'], ch.coll, inflate=0.03, z_from=ch.J['pelvis'].z - 0.25,
                         quilt=True, thickness=0.02))
    ch.add(A.skirt('Gambeson skirt', ch.J, M['cloth'], ch.coll, ch.J['pelvis'].z - 0.02, ch.J['knee.R'].z + 0.16,
                   r_top=(0.21, 0.245), r_bottom=(0.25, 0.29), folds=7, split=0.6))
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=1))
    ch.add(A.gorget(ch.J, ch.s, M, ch.coll))
    for side in ('R', 'L'):
        ch.add(A.vambrace(ch.J, side, M, ch.coll))
        ch.add(A.leg_plates(ch.J, side, M, ch.coll, cuisse=False))
    gloves(ch, M)
    boots(ch, M, shaft=0.12)
    sp = ch.hold(W.spear(M, ch.coll, length=2.1, tassel=M['accent'], banner=M['cloth']), 'R', pitch=14, yaw=-6)
    ch.two_hand(sp['grip2_rest'])
    on_back(ch, W.round_shield(M, ch.coll, radius=0.27, emblem=lantern_emblem(M), face_key='paint')['objs'],
            (-0.27, 0.0, -0.05))
    return ch, finish(ch, st, profile_of(ident))


@unit('player_ranger')
def crossbowman(ident, title):
    M = palette.lantern()
    ch = Character(ident, title, dict(bulk=1.02))
    st = STANCES['crossbow']
    ch.set_stance(st)
    soldier_body(ch, M, torso=M['linen'], sleeve=M['cloth_dark'], legs=M['trousers'])
    C, R = head_frame(ch)
    add_head(ch, H.human_head(C, R, M, ch.coll, nose_kind='small'))
    add_head(ch, H.beard(C, R, M['hair'], ch.coll, length=0.15, width=0.95))
    add_head(ch, H.sallet(C + Vector((0, 0, R * .1)), R, M, ch.coll))
    ch.add(A.shell_torso('Brigandine', ch.s, ch.J, M['cloth'], ch.coll, inflate=0.03, z_from=ch.J['pelvis'].z - 0.1,
                         thickness=0.02))
    ch.add(A.skirt('Brigandine skirt', ch.J, M['cloth_dark'], ch.coll, ch.J['pelvis'].z, ch.J['knee.R'].z + 0.18,
                   r_top=(0.21, 0.245), r_bottom=(0.24, 0.28), folds=6, split=0.6))
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=2))
    for side in ('R', 'L'):
        ch.add(A.pauldron(ch.J, side, M, ch.coll, size=0.7, lames=2))
        ch.add(A.vambrace(ch.J, side, M, ch.coll, mat_key='leather_dark'))
    gloves(ch, M)
    boots(ch, M, shaft=0.22, cuff=M['leather'])
    on_back(ch, W.tower_shield(M, ch.coll, width=0.48, height=0.9, emblem=lantern_emblem(M))['objs'],
            (-0.3, 0.0, -0.18), rot_y=-8)
    cb = ch.hold(W.crossbow(M, ch.coll), 'R', pitch=4, yaw=-4)
    ch.two_hand(cb['grip2_rest'], pole=(-0.2, 0.3, -0.35))
    bolts = [o for o in cb['objs'] if o.name.startswith('Bolt')]
    return ch, finish(ch, st, profile_of(ident), release=bolts)


@unit('player_breacher')
def halberdier(ident, title):
    M = palette.lantern()
    ch = Character(ident, title, dict(bulk=1.06))
    st = STANCES['great']
    ch.set_stance(st)
    soldier_body(ch, M, torso=M['mail'], sleeve=M['cloth'], legs=M['trousers'])
    C, R = head_frame(ch)
    add_head(ch, H.human_head(C, R, M, ch.coll))
    add_head(ch, H.beard(C, R, M['hair'], ch.coll, length=0.6, fork=True))
    add_head(ch, H.barbute(C + Vector((0, 0, R * .04)), R * 1.03, M, ch.coll))
    ch.add(A.breastplate(ch.s, ch.J, M, ch.coll, fauld=2))
    ch.add(A.skirt('Slashed skirt', ch.J, M['cloth'], ch.coll, ch.J['pelvis'].z - 0.1, ch.J['knee.R'].z + 0.1,
                   r_top=(0.21, 0.25), r_bottom=(0.26, 0.3), folds=10, fold_depth=0.025, split=0.6))
    plate_arms(ch, M, lames=3, size=0.95, couters=True)
    gloves(ch, M, 'steel')
    plate_legs(ch, M, cuisse=False)
    boots(ch, M)
    hb = ch.hold(W.halberd(M, ch.coll, length=2.0, grip_at=0.32), 'R', pitch=74, yaw=-8)
    ch.two_hand(hb['grip2_rest'])
    return ch, finish(ch, st, profile_of(ident), heavy=1.05)


# ------------------------------------------------------------------ casters and specialists
def robe_body(ch, M, robe, sleeve=None, inner=None, length=0.06):
    ch.build_body(dict(skin=M['skin'], torso=robe, sleeve=sleeve or robe, glove=M['skin'], legs=inner or M['trousers']))
    ch.add(A.shell_torso('Robe', ch.s, ch.J, robe, ch.coll, inflate=0.025, z_from=ch.J['pelvis'].z - 0.1))
    ch.add(A.skirt('Robe skirt', ch.J, robe, ch.coll, ch.J['pelvis'].z + 0.02, length,
                   r_top=(0.2, 0.235), r_bottom=(0.34, 0.38), folds=9, fold_depth=0.022, split=0.0, segs=40, rows=10))
    for side in ('R', 'L'):
        ch.add(A.wide_sleeve(ch.J, side, sleeve or robe, ch.coll))


@unit('player_marksman')
def mage(ident, title):
    M = palette.lantern(cloth='2f5d86', cloth2='1b3753')
    arcane = S.emissive('7fd3ff', 14, name='Arcane light', core='e8fbff')
    ch = Character(ident, title, dict(bulk=0.95), extras=_cape)
    st = STANCES['staff']
    ch.set_stance(st)
    robe_body(ch, M, M['cloth'], inner=M['cloth_dark'])
    C, R = head_frame(ch)
    grey = S.fur('c9c3b8', name='Grey hair', tip='f2eee6', scale=0.6)
    add_head(ch, H.human_head(C, R, dict(M, hair=grey), ch.coll, age=1.0, nose_kind='long'))
    add_head(ch, H.beard(C, R, grey, ch.coll, length=1.1, width=1.0, braid=True))
    add_head(ch, H.hood(C, R, M['cloth'], ch.coll, pointed=1.4, cowl=1.1, opening=1.05))
    ch.add(A.mantle(ch.J, ch.s, M['cloth_dark'], ch.coll))
    ch.add(A.belt(ch.J, ch.s, dict(M, leather=M['accent']), ch.coll, pouches=1, sash=M['accent']))
    ch.add(A.cape(ch.J, ch.s, M['cloth_dark'], ch.coll, length=0.12, width=1.0, collar=M['trim']))
    boots(ch, M, shaft=0.05, toe='pointed')
    ch.hold(W.staff(dict(M, glow=arcane), ch.coll, length=1.85, head='crystal', crystal=arcane), 'R', pitch=86, yaw=0)
    hand_wisp(ch, arcane)
    return ch, finish(ch, st, profile_of(ident))


@unit('player_stormcaller')
def stormcaller(ident, title):
    M = palette.lantern(cloth='3f4d78', cloth2='252c47')
    storm = S.emissive('a9e6ff', 16, name='Storm light', core='ffffff')
    ch = Character(ident, title, dict(bulk=1.0), extras=_cape)
    st = STANCES['staff']
    ch.set_stance(st)
    robe_body(ch, M, M['cloth'], sleeve=M['cloth_dark'], inner=M['cloth_dark'], length=0.12)
    C, R = head_frame(ch)
    white = S.fur('e6e8ec', name='Storm-white hair', scale=0.6)
    add_head(ch, H.human_head(C, R, dict(M, hair=white), ch.coll, jaw=1.1))
    add_head(ch, H.hair_cap(C, R, white, ch.coll, length=0.9))
    add_head(ch, H.crown(C + Vector((0, 0, -R * .2)), R, dict(M, gem=storm), ch.coll, points=5, height=0.35))
    ch.add(A.mantle(ch.J, ch.s, S.fur('3a3f4a', name='Storm fur', tip='7a8090'), ch.coll))
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=1))
    ch.add(A.cape(ch.J, ch.s, M['cloth_dark'], ch.coll, length=0.18, width=1.05, tattered=1.0))
    for side in ('R', 'L'):
        ch.add(A.vambrace(ch.J, side, M, ch.coll, r=0.07))
    boots(ch, M, shaft=0.18)
    ch.hold(W.staff(dict(M, glow=storm), ch.coll, length=1.9, head='orb', crystal=storm), 'R', pitch=86)
    hand_wisp(ch, storm, 0.045)
    return ch, finish(ch, st, profile_of(ident))


@unit('player_necromancer')
def necromancer(ident, title):
    M = palette.lantern(cloth='3c5a50', cloth2='1d2b27')
    soul = S.emissive('7cf5d0', 12, name='Bound soul', core='e9fff8')
    ch = Character(ident, title, dict(bulk=0.92, thin=0.3), extras=_cape)
    st = STANCES['staff']
    ch.set_stance(st)
    robe_body(ch, M, M['cloth_dark'], sleeve=M['cloth_dark'], inner=M['trousers'])
    C, R = head_frame(ch)
    pale = S.skin('b9ab9c', name='Pale skin', sss=0.08)
    add_head(ch, H.human_head(C, R, dict(M, skin=pale, eye=soul), ch.coll, gaunt=0.8, nose_kind='long'))
    add_head(ch, H.hood(C, R, M['cloth_dark'], ch.coll, pointed=0.6, cowl=1.2, droop=0.25))
    ch.add(A.mantle(ch.J, ch.s, M['cloth'], ch.coll))
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=2, sash=M['cloth']))
    ch.add(A.cape(ch.J, ch.s, M['cloth'], ch.coll, length=0.08, width=1.1, tattered=1.5))
    for k in range(3):
        sk = geo.quadsphere('Belt skull', 0.045, ch.J['pelvis'] + Vector((0.2, -0.12 + k * 0.12, -0.02)), M['bone'],
                            ch.coll, 2, scale=(1.1, .9, 1))
        ch.parts.append((sk, 'hips'))
    boots(ch, M, shaft=0.05, toe='pointed')
    ch.hold(W.staff(dict(M, glow=soul, bone=M['bone']), ch.coll, length=1.8, head='skull', crystal=soul), 'R', pitch=86)
    hand_wisp(ch, soul)
    return ch, finish(ch, st, profile_of(ident))


@unit('player_coordinator')
def battle_monk(ident, title):
    M = palette.lantern(cloth='b08440', cloth2='6e4f22')
    ch = Character(ident, title, dict(bulk=1.08, chest_w=1.05))
    st = STANCES['staff']
    ch.set_stance(st)
    ch.build_body(dict(skin=M['skin'], torso=M['cloth'], sleeve=M['skin'], glove=M['skin'], legs=M['cloth_dark']))
    C, R = head_frame(ch)
    add_head(ch, H.human_head(C, R, M, ch.coll, jaw=1.15, brow=1.2))
    add_head(ch, H.beard(C, R, M['hair'], ch.coll, length=0.45, width=1.0))
    add_head(ch, H.hair_cap(C, R, M['hair'], ch.coll, front=0.2, back=-0.1, topknot=True, volume=0.6))
    ch.add(A.shell_torso('Monk robe', ch.s, ch.J, M['cloth'], ch.coll, inflate=0.02, z_from=ch.J['pelvis'].z - 0.1))
    ch.add(A.skirt('Monk skirt', ch.J, M['cloth'], ch.coll, ch.J['pelvis'].z + 0.02, ch.J['knee.R'].z - 0.2,
                   r_top=(0.2, 0.235), r_bottom=(0.3, 0.34), folds=8, split=0.5))
    ch.add(A.belt(ch.J, ch.s, dict(M, leather=M['rope']), ch.coll, pouches=0, sash=M['cloth_dark']))
    pts = [ch.J['shoulder.L'] + Vector((0.06, -0.02, 0.06)), ch.J['chest'] + Vector((0.23, 0.0, -0.05)),
           ch.J['pelvis'] + Vector((0.18, -0.16, 0.12))]
    for i in range(14):
        t = i / 13
        p = pts[0].lerp(pts[1], min(1, t * 2)) if t < .5 else pts[1].lerp(pts[2], (t - .5) * 2)
        ch.parts.append((geo.sphere('Prayer bead', 0.025, p, M['wood'], ch.coll, 8, 6), 'chest'))
    for side in ('R', 'L'):
        ch.add(A.vambrace(ch.J, side, M, ch.coll, r=0.072, mat_key='linen'))
    boots(ch, M, mat=M['leather'], shaft=0.06)
    ch.hold(W.staff(M, ch.coll, length=1.85, head='lantern'), 'R', pitch=86)
    return ch, finish(ch, st, profile_of(ident))


@unit('player_grenadier')
def alchemist(ident, title):
    M = palette.lantern(cloth='57734b', cloth2='33452c')
    brew = S.liquid('ff8a2a', name='Fire brew', glow=6)
    acid = S.liquid('8cff5a', name='Green brew', glow=5)
    glass = S.glass('d8f2ff', name='Flask glass')
    ch = Character(ident, title, dict(bulk=1.0, belly=0.25))
    st = STANCES['thrower']
    ch.set_stance(st)
    ch.build_body(dict(skin=M['skin'], torso=M['linen'], sleeve=M['cloth'], glove=M['leather_dark'], legs=M['trousers']))
    C, R = head_frame(ch)
    add_head(ch, H.human_head(C, R, M, ch.coll, nose_kind='long'))
    add_head(ch, H.beard(C, R, M['hair'], ch.coll, length=0.2, width=0.9))
    add_head(ch, H.cap_with_goggles(C, R, M['leather'], M['leather_dark'], S.glass('ffb04a', name='Goggle lens', glow=1.5),
                                    M['trim'], ch.coll))
    ch.add(A.shell_torso('Alchemist coat', ch.s, ch.J, M['cloth'], ch.coll, inflate=0.025, z_from=ch.J['pelvis'].z - 0.1))
    ch.add(A.skirt('Coat tails', ch.J, M['cloth'], ch.coll, ch.J['pelvis'].z, ch.J['knee.R'].z,
                   r_top=(0.22, 0.25), r_bottom=(0.28, 0.32), folds=6, front_open=0.55))
    ch.add(A.tabard(ch.J, ch.s, M['leather'], ch.coll, width=0.17, back=False, length=ch.J['knee.R'].z + 0.05))
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=2))
    a, b = ch.J['shoulder.L'] + Vector((0.05, 0, 0.08)), ch.J['pelvis'] + Vector((0.2, -0.18, 0.08))
    ch.parts.append((geo.tube('Bandolier', [a, a.lerp(b, .5) + Vector((0.2, 0, 0)), b], 0.022, M['leather_dark'], ch.coll,
                              sides=6, flatten=.4), {'skin': ['chest', 'spine']}))
    for i, liq in enumerate((brew, acid, brew, acid)):
        t = 0.2 + i * 0.18
        p = a.lerp(b, t) + Vector((0.22 * math.sin(t * math.pi) + 0.03, 0, 0))
        for o in W.flask(M, ch.coll, size=0.55, liquid=liq, glass=glass)['objs']:
            geo.transform(o, Matrix.Translation(p))
            ch.parts.append((o, 'chest'))
    for side in ('R', 'L'):
        ch.add(A.vambrace(ch.J, side, M, ch.coll, mat_key='leather_dark'))
    gloves(ch, M)
    boots(ch, M, shaft=0.2, cuff=M['leather'])
    fl = ch.hold(W.flask(M, ch.coll, size=1.0, liquid=brew, glass=glass), 'R', pitch=90, offset=(0.0, 0, 0.03))
    return ch, finish(ch, st, profile_of(ident), release=list(fl['objs']))


@unit('player_mechanic')
def siege_engineer(ident, title):
    M = palette.lantern(cloth='6b4a2e', cloth2='3e2a1a')
    ch = Character(ident, title, dict(bulk=1.1, belly=0.2, chest_w=1.05))
    st = STANCES['onehand']
    ch.set_stance(st)
    ch.build_body(dict(skin=M['skin'], torso=M['linen'], sleeve=M['linen'], glove=M['leather_dark'], legs=M['trousers']))
    C, R = head_frame(ch)
    ginger = S.fur('8a4a24', name='Ginger beard', tip='c27a3e', scale=0.6)
    add_head(ch, H.human_head(C, R, dict(M, hair=ginger), ch.coll, jaw=1.1))
    add_head(ch, H.beard(C, R, ginger, ch.coll, length=0.7, width=1.05, braid=True))
    add_head(ch, H.cap_with_goggles(C, R, M['cloth_dark'], M['leather_dark'], S.glass('7fe0ff', name='Lens', glow=1.0),
                                    M['trim'], ch.coll))
    ch.add(A.shell_torso('Work shirt', ch.s, ch.J, M['linen'], ch.coll, inflate=0.015, z_from=ch.J['pelvis'].z - 0.05))
    ch.add(A.tabard(ch.J, ch.s, M['leather'], ch.coll, width=0.2, back=False, length=ch.J['knee.R'].z + 0.02))
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=3))
    ch.add(A.backpack(ch.J, M, ch.coll, size=1.0, bedroll=M['cloth']))
    ch.add(A.pauldron(ch.J, 'R', M, ch.coll, size=0.8, lames=2))
    for side in ('R', 'L'):
        ch.add(A.sleeve_cuff(ch.J, side, M['linen'], ch.coll))
    gloves(ch, M)
    boots(ch, M, shaft=0.22, cuff=M['leather'])
    ch.hold(W.hammer(M, ch.coll, haft_len=0.7, head=1.0, spike=False, block=True), 'R', pitch=35, yaw=-8)
    ch.hold(W.wrench(M, ch.coll, length=0.45), 'L', pitch=40, track_tip=False)
    return ch, finish(ch, st, profile_of(ident), heavy=1.05)


@unit('player_banner')
def banner_knight(ident, title):
    M = palette.lantern()
    ch = Character(ident, title, dict(bulk=1.08), extras=_cape)
    st = STANCES['banner']
    ch.set_stance(st)
    soldier_body(ch, M, torso=M['mail'], sleeve=M['mail'], legs=M['mail'])
    C, R = head_frame(ch)
    add_head(ch, H.bascinet(C + Vector((0, 0, R * .05)), R, M, ch.coll, visor=True))
    add_head(ch, H.crest_plume(C + Vector((-R * .2, 0, R * 1.45)), R, M['cloth'], ch.coll, length=1.4))
    ch.add(A.breastplate(ch.s, ch.J, M, ch.coll, fauld=3))
    ch.add(A.tabard(ch.J, ch.s, M['cloth'], ch.coll, width=0.17, trim=M['trim'], length=ch.J['knee.R'].z + 0.1,
                    hem='dagged', emblem=lantern_emblem(M)))
    ch.add(A.cape(ch.J, ch.s, M['cape'], ch.coll, length=0.3, width=0.95, collar=M['trim']))
    plate_arms(ch, M, lames=3, size=1.0, couters=True)
    gloves(ch, M, 'steel')
    plate_legs(ch, M)
    boots(ch, M, mat=M['steel'], shaft=0.06, plate=M['steel'])
    ch.hold(W.sword(M, ch.coll, length=0.8, width=0.072, guard=0.26, pommel='gem'), 'R', pitch=58, yaw=-4)
    ch.hold(W.standard(M, ch.coll, M['cloth'], emblem=lantern_emblem(M), height=2.4, width=0.55, drop=0.72,
                       trim=M['trim']), 'L', pitch=90, yaw=0, track_tip=False)
    return ch, finish(ch, st, profile_of(ident))


@unit('player_rogue')
def rogue(ident, title):
    M = palette.lantern(cloth='33434d', cloth2='1d262c')
    ch = Character(ident, title, dict(bulk=0.92, chest_w=0.94), extras=_cape)
    st = STANCES['dual']
    ch.set_stance(st)
    ch.build_body(dict(skin=M['skin'], torso=M['leather_dark'], sleeve=M['cloth_dark'], glove=M['leather_dark'],
                       legs=M['cloth_dark']))
    C, R = head_frame(ch)
    add_head(ch, H.human_head(C, R, M, ch.coll, jaw=0.9, nose_kind='small'))
    add_head(ch, H.hood(C, R, M['cloth'], ch.coll, pointed=0.5, cowl=1.0, droop=0.15))
    mask = geo.quadsphere('Face mask', 1.0, (0, 0, 0), M['cloth_dark'], ch.coll, level=3,
                          scale=(R * 1.06, R * .9, R * 1.08))
    H._delete_verts(mask, lambda co: co.z < -R * .12 and co.x > -R * .2)
    geo.place(mask, C)
    m = mask.modifiers.new('t', 'SOLIDIFY')
    m.thickness = 0.01
    add_head(ch, [mask])
    ch.add(A.shell_torso('Leather armour', ch.s, ch.J, M['leather_dark'], ch.coll, inflate=0.02,
                         z_from=ch.J['pelvis'].z - 0.12))
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=2, sash=M['accent']))
    ch.add(A.cape(ch.J, ch.s, M['cloth'], ch.coll, length=0.55, width=0.8, tattered=1.0))
    for side in ('R', 'L'):
        ch.add(A.vambrace(ch.J, side, M, ch.coll, mat_key='leather'))
    gloves(ch, M)
    boots(ch, M, shaft=0.3, cuff=M['leather'], toe='pointed')
    ch.hold(W.dagger(M, ch.coll, length=0.34), 'R', pitch=20, yaw=-6)
    ch.hold(W.dagger(M, ch.coll, length=0.3, curve=0.1), 'L', pitch=200, yaw=0, track_tip=False)
    return ch, finish(ch, st, profile_of(ident), gait='prowl')


@unit('player_berserker')
def berserker(ident, title):
    M = palette.lantern(cloth='8e4630', cloth2='4c2418')
    ch = Character(ident, title, dict(bulk=1.12, chest_w=1.1, shoulder=0.29, muscle=1.0))
    st = STANCES['great']
    ch.set_stance(st)
    paint = S.skin('a17a64', name='Weathered skin', blush='7a4434', sss=0.06)
    ch.build_body(dict(skin=paint, torso=paint, sleeve=paint, glove=M['leather_dark'], legs=M['cloth']))
    C, R = head_frame(ch)
    red = S.fur('9a4a22', name='Red hair', tip='d6853e', scale=0.6)
    add_head(ch, H.human_head(C, R, dict(M, hair=red), ch.coll, jaw=1.25, brow=1.4))
    add_head(ch, H.beard(C, R, red, ch.coll, length=1.0, width=1.05, braid=True, fork=True))
    add_head(ch, H.hair_cap(C, R, red, ch.coll, mohawk=True))
    fur = S.fur('4a3a2c', name='Wolf pelt', tip='7a6650')
    ch.add(pelt(A.mantle(ch.J, ch.s, fur, ch.coll, thickness=0.1, drop=0.08, ruff=1.6),
                S.fur('4a3a2c', name='Wolf pelt strands', tip='8a7660', rough=0.85), length=0.06))
    # leather harness across the bare chest and woad war-paint bands on the arms
    J = ch.J
    for sy in (-1, 1):
        a = J['shoulder.' + ('R' if sy < 0 else 'L')] + Vector((0.06, 0, 0.06))
        b = J['pelvis'] + Vector((0.2, -sy * 0.18, 0.12))
        mid = a.lerp(b, 0.5) + Vector((0.12, 0, 0))
        ch.parts.append((geo.tube('Harness strap', [a, mid, b], 0.022, M['leather_dark'], ch.coll, sides=6, flatten=.4),
                         {'skin': ['chest', 'spine', 'hips']}))
    ch.parts.append((geo.cylinder('Harness ring', 0.045, 0.02, J['chest'] + Vector((0.27, 0, -0.12)), M['trim'], ch.coll, 16,
                                  rotation=(0, 90, 0)), 'chest'))
    woad = S.paint('2d5a8a', name='Woad paint', chip=0.0)
    for side in ('R', 'L'):
        sp, el = J['shoulder.' + side], J['elbow.' + side]
        for t in (0.45, 0.62):
            c = sp.lerp(el, t)
            band = geo.lathe('Woad band', [(0.106, -0.012), (0.106, 0.012)], 20, woad, ch.coll, close_top=False,
                             close_bottom=False)
            from mathutils import Matrix as _M
            geo.transform(band, _M.Translation(c) @ Vector((0, 0, 1)).rotation_difference(el - sp).to_matrix().to_4x4())
            ch.parts.append((band, 'upper_arm.' + side))
    ch.add(A.skirt('War kilt', ch.J, M['cloth'], ch.coll, ch.J['pelvis'].z + 0.02, ch.J['knee.R'].z + 0.05,
                   r_top=(0.2, 0.24), r_bottom=(0.27, 0.31), folds=10, fold_depth=0.025, split=0.6))
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=1, z=ch.J['pelvis'].z + 0.04))
    for side in ('R', 'L'):
        ch.add(A.vambrace(ch.J, side, M, ch.coll, r=0.072, mat_key='leather_dark', length=0.6))
        ch.add(A.leg_plates(ch.J, side, dict(M, steel=fur), ch.coll, cuisse=False, poleyn=False, greave=True, size=1.15))
    boots(ch, M, mat=M['leather'], shaft=0.1)
    ax = ch.hold(W.axe(M, ch.coll, haft_len=1.25, head=1.35, double=True, two_hand=True, spike=True), 'R', pitch=76, yaw=-10)
    ch.two_hand(ax['grip2_rest'])
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.15)


@unit('player_lantern_guard')
def lantern_guard(ident, title):
    M = palette.lantern()
    ch = Character(ident, title, dict(bulk=1.12, chest_w=1.05), extras=_cape)
    st = STANCES['sword_shield']
    ch.set_stance(st)
    soldier_body(ch, M, torso=M['mail'], sleeve=M['mail'], legs=M['mail'])
    C, R = head_frame(ch)
    add_head(ch, H.human_head(C, R, M, ch.coll))
    add_head(ch, H.sallet(C + Vector((0, 0, R * .12)), R * 1.04, M, ch.coll))
    D = dict(M, steel=M['dark_steel'])
    ch.add(A.breastplate(ch.s, ch.J, D, ch.coll, fauld=3))
    ch.add(A.gorget(ch.J, ch.s, D, ch.coll, high=True))
    ch.add(A.tabard(ch.J, ch.s, M['cloth'], ch.coll, width=0.16, trim=M['trim'], length=ch.J['knee.R'].z + 0.04,
                    back=False))
    ch.add(A.cape(ch.J, ch.s, M['cape'], ch.coll, length=0.3, width=1.0, collar=M['trim']))
    plate_arms(ch, D, lames=3, size=1.08, couters=True)
    gloves(ch, M, 'dark_steel')
    plate_legs(ch, D)
    boots(ch, M, mat=M['dark_steel'], shaft=0.06, plate=M['dark_steel'])
    lm = W.mace(M, ch.coll, haft_len=0.7, head=0.9)
    lm['objs'] += W.lantern_head(M, ch.coll, Vector((0, 0, 0.62)), M['glow'], size=0.9)
    ch.hold(lm, 'R', pitch=62, yaw=-4)
    ch.strap(W.tower_shield(M, ch.coll, width=0.55, height=1.05, emblem=lantern_emblem(M)), 'L', forward=0.2, up=-0.05,
             yaw=-22)
    return ch, finish(ch, st, profile_of(ident), gait='heavy')


@unit('player_skeleton')
def risen_thrall(ident, title):
    M = palette.rotbound(cloth=palette.LANTERN_TEAL)
    soul = S.emissive('6ff0d2', 12, name='Bound soul', core='e8fff8')
    ch = Character(ident, title, dict(bulk=0.8, thin=1.0))
    st = add_poses(STANCES['sword_shield'], P(spine=[(-6, LAT)], head=[(10, LAT), (8, ROLL)]))
    ch.set_stance(st)
    U.skeleton_body(ch, M, glow_core=soul)
    C, R = head_frame(ch)
    add_head(ch, H.skull_head(C, R * 0.95, dict(M, glow=soul), ch.coll, cracked=0.6))
    U.rags(ch, M['cloth'], ch.coll)
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=0))
    ch.hold(W.sword(M, ch.coll, length=0.62, width=0.065, guard=0.2, pommel='ball'), 'R', pitch=58, yaw=-4)
    ch.strap(W.round_shield(M, ch.coll, radius=0.24, emblem=W.emblem_lantern(M['trim'], soul)), 'L',
             forward=0.15, yaw=-22)
    return ch, finish(ch, st, profile_of(ident), gait='shamble', death='crumble', cape=False)
