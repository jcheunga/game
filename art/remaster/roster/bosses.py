"""Rotbound bosses: fourteen grave lords, each with its own silhouette."""
import math
import random

from mathutils import Matrix, Vector

from rk import anim, armor as A, beast as B, geo, heads as H, palette, shaders as S, undead as U, weapons as W
from rk.anim import P, STANCES
from rk.character import Character
from rk.rig import LAT, YAW, ROLL, add_poses

from . import unit
from .common import (add_head, boots, finish, finish_clips, gloves, head_frame, pelt, plate_arms, plate_legs,
                     profile_of)
from .enemy import claws, grave_emblem, pustules, rotten_head, undead_robe


def _cape(sk, J):
    A.cape_bones(sk, J)


def royal_cape(ch, M, cloth, collar=None, length=0.2, tattered=1.5):
    ch.add(A.cape(ch.J, ch.s, cloth, ch.coll, length=length, width=1.15, flare=1.4, tattered=tattered,
                  collar=collar or S.ermine()))


def armored_lord(ch, M, armor_mats, cloth, emblem=None, tabard=True, legs=True, size=1.15, spikes=3):
    ch.build_body(dict(skin=M['skin'], torso=M['mail'], sleeve=M['mail'], glove=M['leather_dark'], legs=M['mail']))
    ch.add(A.breastplate(ch.s, ch.J, armor_mats, ch.coll, fauld=3, ridge=0.035, plackart=True))
    ch.add(A.gorget(ch.J, ch.s, armor_mats, ch.coll, high=True))
    if tabard:
        ch.add(A.tabard(ch.J, ch.s, cloth, ch.coll, width=0.19, length=ch.J['knee.R'].z - 0.05, hem='dagged', emblem=emblem,
                        trim=armor_mats.get('trim')))
    plate_arms(ch, armor_mats, lames=3, size=size, spikes=spikes, couters=True, rerebrace=True)
    gloves(ch, armor_mats, 'steel', size=1.15)
    if legs:
        plate_legs(ch, armor_mats, size=1.12)
        boots(ch, armor_mats, mat=armor_mats['steel'], shaft=0.06, plate=armor_mats['steel'])


# ------------------------------------------------------------------ Grave Lord
@unit('enemy_boss')
def grave_lord(ident, title):
    M = palette.rotbound()
    ch = Character(ident, title, dict(bulk=1.25, chest_w=1.12, shoulder=0.29), extras=_cape)
    st = STANCES['great']
    ch.set_stance(st)
    AM = dict(M, steel=M['dark_steel'], trim=M['gold'], spike=M['bone'])
    armored_lord(ch, M, AM, M['cloth'], emblem=grave_emblem(M))
    C, R = head_frame(ch)
    add_head(ch, H.skull_head(C, R, M, ch.coll, cracked=0.8))
    add_head(ch, H.crown(C, R * 1.05, M, ch.coll, points=7, height=0.9, spikes=True, z=0.55))
    royal_cape(ch, M, M['cloth'])
    sw = ch.hold(W.sword(dict(M, blade=M['dark_steel'], hilt=M['gold']), ch.coll, length=1.25, width=0.13, guard=0.42,
                         grip=0.3, guard_style='winged', pommel='gem', rune=M['glow'], flare=0.15), 'R', pitch=80, yaw=-8)
    ch.two_hand(sw['matrix'] @ Vector((0, 0, -0.12)))
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.2, low_strike=True)


# ------------------------------------------------------------------ Dread Sovereign
@unit('enemy_boss_citadel')
def dread_sovereign(ident, title):
    M = palette.rotbound(cloth=palette.ROT_PURPLE, glow='b36cff')
    soul = S.emissive('b36cff', 12, name='Sovereign soulfire', core='f3e6ff')
    ch = Character(ident, title, dict(bulk=1.3, chest_w=1.15, shoulder=0.3), extras=_cape)
    st = STANCES['sword_shield']
    ch.set_stance(st)
    AM = dict(M, steel=S.metal('22232a', name='Night plate', rough=0.3, edge=2.4), trim=M['gold'], spike=M['gold'])
    armored_lord(ch, M, AM, M['cloth'], emblem=grave_emblem(dict(M, glow=soul)), spikes=4, size=1.25)
    C, R = head_frame(ch)
    add_head(ch, H.great_helm(C + Vector((0, 0, R * .06)), R * 1.08, dict(AM, dark=soul), ch.coll, cross=True,
                              horns=dict(mat=M['bone'], length=1.8, up=1.2, thick=0.25, back=0.5)))
    add_head(ch, H.crown(C + Vector((0, 0, R * .25)), R * 1.12, dict(M, gem=soul), ch.coll, points=9, height=0.7, spikes=True,
                         z=0.85))
    royal_cape(ch, M, M['cloth'], length=0.12)
    ch.hold(W.sword(dict(M, blade=AM['steel'], hilt=M['gold'], gem=soul), ch.coll, length=1.05, width=0.1, guard=0.36,
                    guard_style='swept', pommel='gem', rune=soul), 'R', pitch=60, yaw=-4)
    ch.strap(W.tower_shield(dict(M, paint=AM['steel'], trim=M['gold']), ch.coll, width=0.6, height=1.15,
                            emblem=grave_emblem(dict(M, glow=soul)), spikes=0), 'L', forward=0.22, yaw=-22)
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.25, low_strike=True)


# ------------------------------------------------------------------ Iron Warden
@unit('enemy_boss_forge')
def iron_warden(ident, title):
    M = palette.rotbound(glow='ff7a2a')
    fire = S.emissive('ff7a1e', 14, name='Furnace fire', core='fff0c0')
    ember = S.ember_metal('3a3531', 'ff6a1a', name='Forge iron', strength=12, crack=0.02, scale=9, coverage=0.56)
    ch = Character(ident, title, dict(bulk=1.5, chest_w=1.3, shoulder=0.33, hand=1.5, hunch=6))
    st = STANCES['brute']
    ch.set_stance(st)
    AM = dict(M, steel=ember, trim=M['dark_steel'], spike=M['dark_steel'])
    armored_lord(ch, M, AM, M['cloth'], tabard=False, size=1.5, spikes=0)
    # furnace grille in the chest
    c = ch.J['chest'] + Vector((0.26 * ch.s['bulk'], 0, -0.05))
    ch.parts.append((geo.sphere('Furnace core', 0.14, c, fire, ch.coll, 16, 10, scale=(0.4, 1.2, 1)), 'chest'))
    for k in range(5):
        ch.parts.append((geo.box('Grille bar', (0.03, 0.03, 0.3), c + Vector((0.06, -0.12 + k * 0.06, 0)), M['dark_steel'], ch.coll,
                                 bevel=0.006), 'chest'))
    C, R = head_frame(ch, 0.92)
    helm = H.great_helm(C + Vector((0.02, 0, -0.04)), R * 1.05, dict(AM, dark=fire, trim=M['dark_steel']), ch.coll, cross=False)
    add_head(ch, helm)
    for i in range(3):
        add_head(ch, [geo.cylinder('Smoke stack', 0.04, 0.32, ch.J['chest'] + Vector((-0.22, -0.12 + i * 0.12, 0.3)), M['dark_steel'],
                                   ch.coll, 10)])
    hm = ch.hold(W.hammer(dict(M, steel=ember), ch.coll, haft_len=1.35, head=2.0, two_hand=True, block=True), 'R', pitch=72,
                 yaw=-10)
    ch.two_hand(hm['grip2_rest'])
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.35, low_strike=True, cape=False)


# ------------------------------------------------------------------ Ashen Regent
@unit('enemy_boss_ashen_regent')
def ashen_regent(ident, title):
    M = palette.rotbound(cloth='4a4642', glow='ff8a3a')
    ember = S.ember_metal('3e3a37', 'ff5a12', name='Ashen plate', strength=11, crack=0.018, scale=11, coverage=0.58)
    flame = S.emissive('ff8a3a', 16, name='Regent flame', core='ffe9b0', flicker=0.6)
    ch = Character(ident, title, dict(bulk=1.25, chest_w=1.1), extras=_cape)
    st = STANCES['onehand']
    ch.set_stance(st)
    AM = dict(M, steel=ember, trim=S.gold('6f6a62', name='Ash-dulled gold', rough=0.5), spike=M['dark_steel'])
    armored_lord(ch, M, AM, S.cloth('5a5550', name='Ash cloth', var='3a3632'), spikes=2)
    C, R = head_frame(ch)
    add_head(ch, H.skull_head(C, R, dict(M, bone=S.bone('6a645c', name='Charred bone', stain='1a1410'), glow=flame), ch.coll,
                              cracked=1.0))
    add_head(ch, H.crown(C, R * 1.05, dict(M, gold=AM['trim'], gem=flame), ch.coll, points=7, height=0.75, spikes=True, z=0.55))
    for k in range(7):
        a = k * math.tau / 7
        p = C + Vector((math.cos(a) * R * .95, math.sin(a) * R * .95, R * 1.25))
        add_head(ch, [geo.sphere('Crown flame', R * .14, p, flame, ch.coll, 8, 6, scale=(1, 1, 2.2))])
    royal_cape(ch, M, S.cloth('3a3633', name='Ash cape', var='1e1c1a'), collar=S.fur('5a524a', name='Ash fur'), tattered=2.5)
    sw = W.sword(dict(M, blade=ember, hilt=AM['trim']), ch.coll, length=1.05, width=0.1, guard=0.34, guard_style='winged',
                 rune=flame, flare=0.25)
    ch.hold(sw, 'R', pitch=60, yaw=-4)
    ch.add_rigid([geo.sphere('Palm flame', 0.07, ch.J['hand_tip.L'], flame, ch.coll, 10, 6, scale=(1, 1, 1.6))], 'hand.L')
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.2, low_strike=True)


# ------------------------------------------------------------------ Plague Archon
@unit('enemy_boss_ward')
def plague_archon(ident, title):
    M = palette.rotbound(cloth='2f3a2a', glow=palette.PLAGUE)
    ch = Character(ident, title, dict(bulk=1.1, thin=0.4, hunch=8, leg_len=1.1), extras=_cape)
    st = STANCES['staff']
    ch.set_stance(st)
    undead_robe(ch, M, M['cloth'], length=0.04)
    C, R = head_frame(ch)
    mask_mat = S.leather('3a2c22', name='Waxed leather')
    beak = geo.lathe('Plague beak', [(0, 0), (R * .5, 0.0), (R * .38, R * .8), (R * .12, R * 1.9), (0, R * 2.3)], 16, mask_mat, ch.coll)
    geo.transform(beak, Matrix.Translation(C + Vector((R * .7, 0, -R * .2))) @ Matrix.Rotation(math.radians(100), 4, 'Y'))
    add_head(ch, [beak])
    add_head(ch, H.human_head(C, R, dict(M, skin=mask_mat, eye=M['plague']), ch.coll, ears=False, eye_glow=M['plague']))
    for sy in (-1, 1):
        add_head(ch, [geo.cylinder('Mask lens', R * .2, R * .1, C + Vector((R * .82, sy * R * .36, R * .1)), M['trim'], ch.coll, 16,
                                   rotation=(0, 80, 0)),
                      geo.sphere('Lens glow', R * .14, C + Vector((R * .9, sy * R * .36, R * .1)), M['plague'], ch.coll, 10, 8,
                                 scale=(.4, 1, 1))])
    hat = geo.lathe('Plague hat', [(0, R * 1.9), (R * .9, R * 1.85), (R * .95, R * .75), (R * 2.1, R * .62), (R * 2.15, R * .55)],
                    32, M['leather_dark'], ch.coll, close_bottom=False)
    geo.place(hat, C)
    m = hat.modifiers.new('t', 'SOLIDIFY')
    m.thickness = 0.015
    add_head(ch, [hat])
    ch.add(A.mantle(ch.J, ch.s, M['leather_dark'], ch.coll, drop=0.3))
    ch.add(A.cape(ch.J, ch.s, M['cloth_dark'], ch.coll, length=0.06, width=1.15, tattered=1.0))
    pustules(ch, M, ch.J['chest'] + Vector((0.15, 0, -0.1)), (0.2, 0.25, 0.2), 6, 'chest', seed=9, size=0.04)
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=3))
    ch.hold(W.staff(dict(M, glow=M['plague']), ch.coll, length=2.1, head='censer', crystal=M['plague']), 'R', pitch=86)
    for side in ('R', 'L'):
        ch.add(A.gauntlet(ch.J, side, M, ch.coll, mat_key='leather_dark'))
    return ch, finish(ch, st, profile_of(ident), gait='march', low_strike=True)


# ------------------------------------------------------------------ Plague Monarch
@unit('enemy_boss_plague_monarch')
def plague_monarch(ident, title):
    M = palette.rotbound(cloth='3f5a2a', flesh='76875e', glow=palette.PLAGUE)
    ch = Character(ident, title, dict(bulk=1.55, chest_w=1.2, belly=1.4, hunch=8, hand=1.3), extras=_cape)
    st = STANCES['brute']
    ch.set_stance(st)
    ch.build_body(dict(skin=M['skin'], torso=M['skin'], sleeve=M['cloth'], glove=M['skin'], legs=M['cloth']))
    C, R = head_frame(ch, 0.95)
    C = C + Vector((0.05, 0, -0.04))
    rotten_head(ch, M, C, R, gaunt=0.0, glow=M['plague'])
    add_head(ch, H.crown(C, R * 1.08, M, ch.coll, points=8, height=0.75, spikes=False, z=0.6))
    pustules(ch, M, ch.J['spine'] + Vector((0.3, 0, 0.0)), (0.32, 0.36, 0.28), 12, 'spine', seed=5, size=0.04)
    ch.add(A.skirt('Regal robe', ch.J, M['cloth'], ch.coll, ch.J['pelvis'].z + 0.02, 0.05, r_top=(0.36, 0.38),
                   r_bottom=(0.46, 0.5), folds=10, fold_depth=0.03, segs=40, rows=10))
    ch.add(A.mantle(ch.J, dict(ch.s, bulk=1.6), S.cloth('8a6a2e', name='Plague gold brocade', var='5a4418', sheen=0.9), ch.coll, drop=0.12, ruff=0.6))
    royal_cape(ch, M, M['cloth'], length=0.06)
    ch.add(A.belt(ch.J, dict(ch.s, belly=1.4), dict(M, leather=M['gold']), ch.coll, pouches=0))
    sc = W.mace(dict(M, steel=M['gold']), ch.coll, haft_len=0.9, head=1.4, flanges=6)
    sc['objs'].append(geo.sphere('Scepter orb', 0.08, (0, 0, 0.78), M['plague'], ch.coll, 14, 8))
    ch.hold(sc, 'R', pitch=62, yaw=-4)
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.3, low_strike=True)


# ------------------------------------------------------------------ Thornwall Chieftain
@unit('enemy_boss_pass')
def thornwall_chieftain(ident, title):
    M = palette.rotbound(cloth='3e4a2c', flesh='7a8264')
    thorn = S.wood('3a2a1c', name='Thorn vine', grain=10)
    ch = Character(ident, title, dict(bulk=1.35, chest_w=1.2, shoulder=0.3, muscle=0.8, hunch=6), extras=_cape)
    st = STANCES['great']
    ch.set_stance(st)
    ch.build_body(dict(skin=M['skin'], torso=M['skin'], sleeve=M['skin'], glove=M['leather_dark'], legs=M['cloth']))
    C, R = head_frame(ch)
    rotten_head(ch, M, C, R, gaunt=0.6, hair=S.fur('2c2a24', name='Matted hair', tip='5a544a'))
    add_head(ch, H.antlers(C + Vector((0, 0, R * .1)), R * 1.2, M['bone'], ch.coll, spread=1.2, tines=3))
    add_head(ch, [geo.lathe('Bone circlet', [(R * 1.02, R * .55), (R * 1.04, R * .72)], 24, M['bone'], ch.coll,
                            close_top=False, close_bottom=False, location=C)])
    fur = S.fur('4a3c2e', name='Bear pelt', tip='8a7660')
    strands = S.fur('3e3226', name='Bear pelt strands', tip='8a7660', rough=0.85)
    ch.add(pelt(A.mantle(ch.J, ch.s, fur, ch.coll, drop=0.25, ruff=1.8), strands, length=0.08))
    ch.add(pelt(A.cape(ch.J, ch.s, fur, ch.coll, length=0.3, width=1.1, tattered=2.0), strands, length=0.05, count=3000))
    ch.add(A.skirt('Hide kilt', ch.J, M['cloth'], ch.coll, ch.J['pelvis'].z + 0.02, ch.J['knee.R'].z, r_top=(0.24, 0.28),
                   r_bottom=(0.3, 0.34), folds=9, split=0.6, hem_wave=0.05))
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=1))
    rnd = random.Random(4)
    for side in ('R', 'L'):
        sp, wr = ch.J['shoulder.' + side], ch.J['wrist.' + side]
        pts = [sp.lerp(wr, t) + Vector((0.08 * math.cos(t * 12), 0.08 * math.sin(t * 12), 0)) for t in [i / 10 for i in range(11)]]
        ch.parts.append((geo.tube('Thorn vine', pts[:6], 0.018, thorn, ch.coll, sides=5), 'upper_arm.' + side))
        ch.add(A.vambrace(ch.J, side, M, ch.coll, r=0.085, mat_key='leather_dark'))
        ch.add(A.boot(ch.J, side, M['leather_dark'], ch.coll, shaft=0.18, size=1.2))
    ax = ch.hold(W.axe(dict(M, blade=M['bone']), ch.coll, haft_len=1.4, head=1.7, double=False, two_hand=True, spike=True), 'R',
                 pitch=76, yaw=-10)
    ch.two_hand(ax['grip2_rest'])
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.25, low_strike=True)


# ------------------------------------------------------------------ Bone Pontiff
@unit('enemy_boss_basilica')
def bone_pontiff(ident, title):
    M = palette.rotbound(cloth='d9cfb6', glow='ffe08a')
    holy = S.emissive('ffd36a', 12, name='Corrupted holy light', core='fff7da')
    ivory = S.cloth('e4dac2', name='Ivory vestment', var='b7a988')
    ch = Character(ident, title, dict(bulk=1.1, thin=1.0, hunch=6), extras=_cape)
    st = STANCES['staff']
    ch.set_stance(st)
    U.skeleton_body(ch, M, glow_core=holy)
    ch.add(A.shell_torso('Chasuble', ch.s, ch.J, ivory, ch.coll, inflate=0.07, z_from=ch.J['pelvis'].z - 0.05))
    ch.add(A.skirt('Alb', ch.J, ivory, ch.coll, ch.J['pelvis'].z + 0.04, 0.04, r_top=(0.24, 0.28), r_bottom=(0.36, 0.4), folds=11,
                   segs=40, rows=10))
    ch.add(A.tabard(ch.J, ch.s, M['gold'], ch.coll, width=0.09, length=0.15, emblem=W.emblem_sun(M['gold'])))
    for side in ('R', 'L'):
        ch.add(A.wide_sleeve(ch.J, side, ivory, ch.coll, r1=0.2))
    C, R = head_frame(ch)
    add_head(ch, H.skull_head(C, R, dict(M, glow=holy), ch.coll, cracked=0.5))
    add_head(ch, H.mitre(C + Vector((0, 0, R * .1)), R * 1.05, dict(M, cloth=ivory, gem=holy), ch.coll, height=2.0))
    ch.add(A.mantle(ch.J, ch.s, M['gold'], ch.coll, drop=0.15, ruff=0.4))
    royal_cape(ch, M, S.cloth('8a1e2a', name='Cardinal red', var='4a1018'), collar=M['gold'], length=0.06, tattered=0.5)
    # rosary of small skulls
    for i in range(9):
        t = i / 8
        p = ch.J['neck'] + Vector((0.22 + 0.05 * math.sin(t * math.pi), -0.22 + 0.44 * t, -0.18 - 0.22 * math.sin(t * math.pi)))
        ch.parts.append((geo.quadsphere('Rosary skull', 0.035, p, M['bone'], ch.coll, 2, scale=(1.1, .85, 1)), 'chest'))
    ch.hold(W.staff(dict(M, glow=holy, hilt=M['gold']), ch.coll, length=2.2, head='crook', crystal=holy), 'R', pitch=86)
    return ch, finish(ch, st, profile_of(ident), death='crumble', low_strike=True)


# ------------------------------------------------------------------ Reliquary Tyrant
@unit('enemy_boss_reliquary')
def reliquary_tyrant(ident, title):
    M = palette.rotbound(cloth='7a2030', glow='ffe2a0')
    holy = S.emissive('ffd98a', 10, name='Reliquary glow', core='fffbe8')
    gild = S.gold('d9a943', name='Reliquary gold', rough=0.22)
    ch = Character(ident, title, dict(bulk=1.3, chest_w=1.15), extras=_cape)
    st = STANCES['onehand']
    ch.set_stance(st)
    AM = dict(M, steel=gild, trim=S.gold('f0d27a', name='Bright gilt'), spike=gild)
    armored_lord(ch, M, AM, M['cloth'], emblem=W.emblem_sun(AM['trim']), spikes=0, size=1.2)
    C, R = head_frame(ch)
    add_head(ch, H.skull_head(C, R, dict(M, glow=holy), ch.coll))
    add_head(ch, H.crown(C, R * 1.05, dict(M, gold=gild, gem=S.gem('d93a4a', name='Ruby')), ch.coll, points=5, height=0.55))
    # halo of relic shards behind the head
    halo = geo.lathe('Halo', [(R * 2.1, -0.01), (R * 2.3, 0.01)], 48, gild, ch.coll, close_top=False, close_bottom=False)
    geo.transform(halo, Matrix.Translation(C + Vector((-R * .9, 0, R * .3))) @ Matrix.Rotation(math.radians(90), 4, 'Y'))
    mm = halo.modifiers.new('t', 'SOLIDIFY')
    mm.thickness = 0.03
    add_head(ch, [halo])
    for k in range(10):
        a = k * math.tau / 10
        p = C + Vector((-R * .9, math.cos(a) * R * 2.2, R * .3 + math.sin(a) * R * 2.2))
        add_head(ch, [geo.cylinder('Halo relic', R * .14, R * .08, p, holy if k % 2 else gild, ch.coll, 6, rotation=(0, 90, 0))])
    royal_cape(ch, M, M['cloth'], collar=S.ermine())
    fl = W.mace(dict(M, steel=gild), ch.coll, haft_len=0.75, head=1.5, flanges=8, spikes=True)
    ch.hold(fl, 'R', pitch=60, yaw=-4)
    ch.add_rigid([geo.lathe('Held reliquary', [(0, -0.08), (0.08, -0.08), (0.09, 0.05), (0.05, 0.12), (0, 0.16)], 8, gild, ch.coll,
                            location=ch.J['hand_tip.L'] + Vector((0.04, 0, 0.06))),
                  geo.sphere('Reliquary light', 0.05, ch.J['hand_tip.L'] + Vector((0.04, 0, 0.1)), holy, ch.coll, 10, 6)], 'hand.L')
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.2, low_strike=True)


# ------------------------------------------------------------------ Tidecaller and Harrow Tidemaster
@unit('enemy_boss_docks')
def tidecaller(ident, title):
    M = palette.rotbound(cloth='245a5e', flesh='5f7a72', glow='5ff0e0')
    sea = S.emissive('5ff0e0', 12, name='Tide light', core='e6fffb')
    coral = S.coral('3f7f78')
    ch = Character(ident, title, dict(bulk=1.15, thin=0.3), extras=_cape)
    st = STANCES['staff']
    ch.set_stance(st)
    undead_robe(ch, M, M['cloth'], length=0.03)
    C, R = head_frame(ch)
    rotten_head(ch, M, C, R, gaunt=0.7, glow=sea, hair=S.fur('2f4a3a', name='Kelp hair', tip='4f7a5a'))
    add_head(ch, H.crown(C, R * 1.05, dict(M, gold=coral, gem=sea), ch.coll, points=6, height=0.8, spikes=True, z=0.55))
    for side in ('R', 'L'):
        ch.add(A.pauldron(ch.J, side, dict(M, steel=coral, spike=coral), ch.coll, size=1.25, lames=2, spikes=3))
    ch.add(A.cape(ch.J, ch.s, S.cloth('1e3f40', name='Drowned cloth', var='0f2526'), ch.coll, length=0.04, width=1.15,
                  tattered=2.5))
    rnd = random.Random(2)
    for k in range(10):
        p = ch.J['pelvis'] + Vector((rnd.uniform(-0.2, 0.25), rnd.uniform(-0.3, 0.3), rnd.uniform(-0.6, 0.0)))
        ch.parts.append((geo.sphere('Barnacle', 0.025, p + Vector((0.18, 0, 0)), coral, ch.coll, 8, 5), 'hips'))
    ch.hold(W.staff(dict(M, glow=sea, steel=coral, blade=coral), ch.coll, length=2.15, head='trident', crystal=sea), 'R', pitch=86)
    ch.add_rigid([geo.sphere('Water orb', 0.08, ch.J['hand_tip.L'] + Vector((0.03, 0, 0.04)), sea, ch.coll, 14, 8)], 'hand.L')
    return ch, finish(ch, st, profile_of(ident))


@unit('enemy_boss_tidemaster')
def harrow_tidemaster(ident, title):
    M = palette.rotbound(cloth='1f3d52', flesh='667a72', glow='7ff0e8')
    sea = S.emissive('7ff0e8', 10, name='Drowned light', core='e6fffb')
    coat = S.cloth('1f3448', name='Admiral coat', var='101c28')
    ch = Character(ident, title, dict(bulk=1.3, chest_w=1.15, belly=0.3), extras=_cape)
    st = STANCES['great']
    ch.set_stance(st)
    ch.build_body(dict(skin=M['skin'], torso=M['linen'], sleeve=coat, glove=M['leather_dark'], legs=M['trousers']))
    ch.add(A.shell_torso('Admiral coat', ch.s, ch.J, coat, ch.coll, inflate=0.035, z_from=ch.J['pelvis'].z - 0.1))
    ch.add(A.skirt('Coat tails', ch.J, coat, ch.coll, ch.J['pelvis'].z, ch.J['knee.R'].z - 0.1, r_top=(0.24, 0.27),
                   r_bottom=(0.32, 0.36), folds=8, front_open=0.6))
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=2, sash=S.cloth('7a1e2a', name='Sash red')))
    for side in ('R', 'L'):
        ch.add(A.pauldron(ch.J, side, dict(M, steel=M['gold']), ch.coll, size=0.9, lames=1))
        ch.add(A.sleeve_cuff(ch.J, side, M['gold'], ch.coll))
    C, R = head_frame(ch)
    rotten_head(ch, M, C, R, gaunt=0.5, glow=sea)
    add_head(ch, H.beard(C, R, S.fur('2f4038', name='Weed beard', tip='4e6a5c'), ch.coll, length=1.2, braid=True))
    tri = geo.lathe('Tricorn', [(0, R * 1.5), (R * .9, R * 1.45), (R * 1.0, R * .8), (R * 1.9, R * .7), (R * 2.0, R * .9)], 3,
                    M['leather_dark'], ch.coll, close_bottom=False)
    geo.place(tri, C + Vector((0, 0, R * .05)), rotation=(0, 0, 60))
    mm = tri.modifiers.new('t', 'SOLIDIFY')
    mm.thickness = 0.015
    sub = tri.modifiers.new('s', 'SUBSURF')
    sub.levels = 2
    add_head(ch, [tri])
    royal_cape(ch, M, coat, collar=M['gold'], tattered=2.0, length=0.15)
    # anchor
    out = []
    out.append(geo.cylinder('Anchor shank', 0.05, 1.3, (0, 0, 0.35), M['dark_steel'], ch.coll, 12))
    out.append(geo.lathe('Anchor ring', [(0.14, -0.02), (0.17, 0.02)], 20, M['dark_steel'], ch.coll, close_top=False,
                         close_bottom=False, rotation=(90, 0, 0), location=(0, 0, -0.38)))
    out.append(geo.box('Anchor stock', (0.06, 0.6, 0.06), (0, 0, -0.22), M['wood'], ch.coll, bevel=0.015))
    pts = [Vector((-0.42, 0, 0.95)), Vector((-0.25, 0, 1.05)), Vector((0, 0, 1.08)), Vector((0.25, 0, 1.05)), Vector((0.42, 0, 0.95))]
    out.append(geo.tube('Anchor arms', H.catmull(pts, 4), 0.055, M['dark_steel'], ch.coll, sides=10))
    for sx in (-1, 1):
        out.append(W.blade('Anchor fluke', 0.18, 0.16, M['dark_steel'], ch.coll, base_z=0, thickness=0.04, tip=0.5, taper=0))
        geo.transform(out[-1], Matrix.Translation((sx * 0.42, 0, 0.95)) @ Matrix.Rotation(math.radians(-sx * 140), 4, 'Y'))
    for k in range(6):
        out.append(geo.sphere('Anchor barnacle', 0.03, (0.04 * (-1) ** k, 0.04, 0.2 + k * 0.13), S.coral('4f6f6a'), ch.coll, 8, 5))
    an = ch.hold(dict(objs=out, tip=Vector((0.42, 0, 0.95)), grip2=Vector((0, 0, -0.3)), kind='hammer'), 'R', pitch=74, yaw=-10)
    ch.two_hand(an['grip2_rest'])
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.3, low_strike=True)


# ------------------------------------------------------------------ Gloamwood Witch
@unit('enemy_boss_verge')
def gloamwood_witch(ident, title):
    M = palette.rotbound(cloth='2a3a26', flesh='7c8a6c', glow='b6ff6a')
    wisp = S.emissive('b6ff6a', 12, name='Witchlight', core='f6ffe6')
    bark = S.wood('3d2c1e', name='Gloam bark', grain=8)
    ch = Character(ident, title, dict(bulk=0.95, thin=0.9, hunch=18, arm_len=1.2, hand=1.3, leg_len=1.15), extras=_cape)
    st = add_poses(STANCES['staff'], P(head=[(10, LAT)], upper_arm_L=[(20, LAT)], forearm_L=30))
    ch.set_stance(st)
    undead_robe(ch, M, M['cloth'], length=0.02)
    C, R = head_frame(ch)
    add_head(ch, H.human_head(C, R, dict(M, eye=wisp), ch.coll, gaunt=1.0, nose=1.8, nose_kind='long', ears=False, eye_glow=wisp))
    add_head(ch, H.hair_cap(C, R, S.fur('1e1c18', name='Witch hair', tip='3a3630'), ch.coll, length=2.2, strands=3.0))
    add_head(ch, H.antlers(C + Vector((0, 0, R * .2)), R * 1.4, bark, ch.coll, spread=1.4, tines=4))
    ch.add(A.mantle(ch.J, ch.s, S.fur('2e3a28', name='Moss shawl', tip='5a6a3e'), ch.coll, drop=0.35, ruff=2.0))
    ch.add(A.cape(ch.J, ch.s, M['cloth_dark'], ch.coll, length=0.02, width=1.2, tattered=3.0))
    for side in ('R', 'L'):
        claws(ch, M, side, 0.16, 4, M['bone_dark'])
    ch.hold(W.staff(dict(M, glow=wisp, wood=bark), ch.coll, length=2.2, head='branch', crystal=wisp), 'R', pitch=86)
    ch.add_rigid([geo.sphere('Hex wisp', 0.07, ch.J['hand_tip.L'] + Vector((0.05, 0, 0.05)), wisp, ch.coll, 12, 8)], 'hand.L')
    return ch, finish(ch, st, profile_of(ident), gait='shamble')


# ------------------------------------------------------------------ Mire Behemoth
@unit('enemy_boss_mire')
def mire_behemoth(ident, title):
    M = palette.rotbound(flesh='4c5e42', glow='c8ff7a')
    moss = S.fur('3e5a2a', name='Swamp moss', tip='7a9a4a', scale=1.5)
    mud = S.flesh('3b4632', name='Mire hide', rot='1f261a', vein='2a3a22', wet=0.8)
    ch = Character(ident, title, dict(bulk=1.7, chest_w=1.35, shoulder=0.34, hunch=26, arm_len=1.35, hand=1.7, leg_len=0.85,
                                      belly=0.6))
    st = STANCES['claws']
    ch.set_stance(st)
    ch.build_body(dict(skin=mud, torso=mud, sleeve=mud, glove=mud, legs=mud))
    C, R = head_frame(ch, 0.9)
    C = C + Vector((0.12, 0, -0.12))
    add_head(ch, H.ghoul_head(C, R * 1.2, dict(skin=mud, glow=M['glow'], bone=M['bone']), ch.coll))
    add_head(ch, H.horns_pair(C, R * 1.2, M['horn'], ch.coll, ram=True, thick=0.3))
    ch.add(pelt(A.mantle(ch.J, ch.s, moss, ch.coll, drop=0.35, ruff=2.4),
                S.fur('3a5626', name='Moss strands', tip='86a650', rough=0.9), length=0.09, count=3500, comb=(0, 0, -1)))
    for side in ('R', 'L'):
        claws(ch, M, side, 0.24, 4)
        ch.add(A.boot(ch.J, side, mud, ch.coll, shaft=0, size=1.6))
    # swamp lantern hung from a bone hook over the back
    lt = W.lantern_head(dict(M, hilt=M['dark_steel'], lamp_glass=S.glass('c8ff7a', name='Marsh glass', glow=3)), ch.coll,
                        ch.J['chest'] + Vector((-0.3, 0.1, 0.35)), M['glow'], size=1.6)
    for o in lt:
        ch.parts.append((o, 'chest'))
    pustules(ch, M, ch.J['chest'] + Vector((-0.1, 0, 0.1)), (0.3, 0.38, 0.2), 10, 'chest', mat=M['glow'], seed=8, size=0.04)
    rnd = random.Random(6)
    for k in range(8):
        p = ch.J['chest'] + Vector((rnd.uniform(-0.25, 0.25), rnd.uniform(-0.35, 0.35), -0.1))
        ch.parts.append((geo.tube('Hanging weed', [p, p + Vector((0.02, 0, -0.35 - rnd.random() * 0.2))], [0.03, 0.008], moss, ch.coll,
                                  sides=5), 'chest'))
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.35, death='forward', low_strike=True, cape=False)


# ------------------------------------------------------------------ Steppe Warlord (mounted)
@unit('enemy_boss_steppe')
def steppe_warlord(ident, title):
    from .beasts import RIDING, horse, rider_clips, rider_on
    M = palette.rotbound(cloth='7a3a1e', glow='ff9a4a')
    ghost = S.emissive('ff8a3a', 8, name='Ghost fire', core='ffe6b0', flicker=0.6)
    M['glow'] = ghost
    hc = horse(ident, title, M, M['bone'], ghost, skeletal=True, barding=S.cloth('5a2a18', name='Steppe barding', var='2e150c'),
               emblem=grave_emblem(M))
    rider = Character(ident + '_rider', title + ' rider', dict(bulk=1.2, chest_w=1.1), coll=hc.coll,
                      extras=lambda sk, J: A.cape_bones(sk, J))
    st = add_poses(RIDING, P(upper_arm_R=[(-6, LAT), (-10, ROLL)], forearm_R=70, hand_R=-5, upper_arm_L=[(30, LAT), (10, ROLL)],
                             forearm_L=50))
    rider.set_stance(st)
    lam = S.metal('6a4a2a', name='Lamellar', rough=0.4, hammer=2.0, wear=0.3)
    rider.build_body(dict(skin=M['skin'], torso=lam, sleeve=lam, glove=M['leather_dark'], legs=M['cloth']))
    C, R = head_frame(rider)
    add_head(rider, H.skull_head(C, R, dict(M, glow=ghost), rider.coll))
    helm = H.nasal_helm(C, R, dict(M, steel=M['dark_steel'], trim=M['gold']), rider.coll, cheeks=True)
    add_head(rider, helm)
    add_head(rider, [geo.tube('Horsehair plume', [C + Vector((0, 0, R * 1.6)), C + Vector((-R * .6, 0, R * 1.9)),
                                                  C + Vector((-R * 1.6, 0, R * .8))], [R * .12, R * .2, R * .05],
                              S.fur('2a1e18', name='Horsehair'), rider.coll, sides=8)])
    rider.add(A.shell_torso('Lamellar coat', rider.s, rider.J, lam, rider.coll, inflate=0.03, z_from=rider.J['pelvis'].z - 0.3))
    rider.add(A.mantle(rider.J, rider.s, S.fur('4a3a2a', name='Wolf fur'), rider.coll, drop=0.1, ruff=1.6))
    rider.add(A.cape(rider.J, rider.s, M['cloth'], rider.coll, length=0.6, width=1.0, tattered=2.0))
    for side in ('R', 'L'):
        rider.add(A.pauldron(rider.J, side, dict(M, steel=M['dark_steel'], spike=M['bone']), rider.coll, size=1.0, lames=3, spikes=2))
        rider.add(A.vambrace(rider.J, side, M, rider.coll, mat_key='dark_steel'))
    gloves(rider, M, 'leather_dark')
    boots(rider, M, shaft=0.25)
    rider.hold(W.lance(M, rider.coll, length=2.8, paint=S.paint('5a2a18', name='Lance paint')), 'R', pitch=8, yaw=-4)
    hst, HC = B.clips('horse')
    RC = rider_clips(st, profile_of(ident))
    # Mount first so the rider's death is solved against the horse going down.
    rider_on(hc, rider)
    opts = finish_clips(hc, HC, profile_of(ident), body_z=1.9, cam_z=1.6, min_scale=4.4, stance=hst,
                        companions=[(rider, RC, st)], portrait=dict(target=(0.5, 0, 2.05), distance=7.0))
    hc.tip_bone_owner = rider
    return hc, opts
