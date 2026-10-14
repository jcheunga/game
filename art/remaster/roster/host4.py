"""Rotbound bosses in the roster-sheet style: each zone's lord, larger and more ornate than its army."""
import math

import numpy as np
from mathutils import Matrix, Vector

from rk import arms, beast as B, faces, garb, geo, helms, palette, plate, pose as PS, shaders as S, steed
from rk.anim import P
from rk.character import Character
from rk.rig import LAT, YAW, ROLL

from . import unit
from .caravan import BRACE, EASY, GUARD, STRIDE, WIDE, add_head, deg, dress_head, head_frame, two_hand, wield
from .common import finish, finish_clips, profile_of
from .host2 import HUNCHED, SHOULDERED, glow_at, setup, skull
from .host3 import moth_wings

BOSS_FEET = dict(drop=0.16, spread=0.42, stride=0.6, front='L', knees_out=0.55)


def boss_armor(K, A, ch, M, pauldron=1.3, spikes=2, cape='cape', cape_len=0.1, tabard='cloth', emblem='rot',
               emblem_mat='gold', trim='gold', faulds=3, collar='cloth_dark', tatter=1.6):
    A.cuirass(t=0.075)
    if faulds:
        A.faulds(lames=faulds, t=0.09)
    if tabard:
        K.tabard(tabard, trim=trim, emblem=emblem, emblem_mat=emblem_mat, width=0.16,
                 length=ch.Jp['knee.R'].z - 0.05, t=0.1, hem='tattered')
    K.belt('leather_dark', buckle='gold', t=0.11, width=0.08)
    for side in ('R', 'L'):
        A.pauldron(side, size=pauldron, lames=3, spikes=spikes)
        A.couter(side, size=1.15)
        A.vambrace(side, t=0.035)
        A.gauntlet_cuff(side)
        A.poleyn(side, size=1.1)
        A.greave(side, t=0.04)
    if cape:
        K.cape(cape, length=cape_len, flare=0.35, tatter=tatter, collar=collar)


@unit('enemy_boss')
def grave_lord(ident, title):
    G, D = SHOULDERED
    R, L = two_hand(G, D, -0.3)
    M, ch, st, K, A = setup('bones', ident, title, BRACE, dict(bulk=1.2, scale=1.18), R=R, L=L, feet=BOSS_FEET,
                            cape=True, thick=1.8)
    K.loincloth('cloth', length=0.6, width=0.2, sides=1, tatter=1.4, t=0.06)
    boss_armor(K, A, ch, M)
    C, u = skull(ch, M, jaw_open=0.3, size=1.0)
    add_head(ch, helms.great_helm(C, u, M, ch.coll, cross=False, t=0.24))
    hn = garb.F.Field('Lord horns')
    for sy in (-1, 1):
        pts = [C + Vector((-0.1 * u, sy * 0.9 * u, 0.6 * u)), C + Vector((-0.2 * u, sy * 1.5 * u, 1.0 * u)),
               C + Vector((0.1 * u, sy * 1.7 * u, 1.7 * u))]
        hn.add(garb.F.Chain(pts, [0.2 * u, 0.13 * u, 0.02 * u]), 0.04 * u)
    add_head(ch, [hn.mesh(0.003, ch.coll, M['bone'], name='Lord horns')])
    add_head(ch, helms.crown(C, u, M['gold'], ch.coll, points=5, height=0.45, z=1.15, radius=0.8, spikes=True,
                             gems=M['glow']))
    wield(ch, arms.axe(M, ch.coll, haft_len=1.3, head=2.0, double=True, two_hand=True), M, G, D, glove='bone', g2=-0.3)
    return ch, finish(ch, st, 'royal-cleave', gait='heavy', heavy=1.3)


@unit('enemy_boss_docks')
def tidecaller(ident, title):
    M = palette.zone_host('harbor')
    M['coral'] = S.coral('b86a5a', name='Coral')
    M['water'] = S.emissive('7ff0ff', 7, name='Tide light', core='e8ffff')
    M['crystal'] = M['water']
    M2, ch, st, K, A = setup('flesh', ident, title, STRIDE, dict(bulk=1.25, belly=0.5, scale=1.16), M=M, cape=True,
                             R=PS.Grip(Vector((0.18, -0.42, 1.2)), deg(86, 2), pole=(-0.3, -0.8, 1.0)),
                             L=PS.Reach(Vector((0.56, 0.24, 1.42)), pole=(0.05, 0.8, 1.1), fingers=Vector((1, 0.1, 0.4)),
                                        palm=Vector((0, 0, 1)), pose='claw'), feet=BOSS_FEET)
    K.skirt('cloth', length=0.95, flare=0.3, t=0.014, tatter=2.0, split=0.4)
    K.gambeson('cloth_dark', t=0.035, skirt=0.3, sleeves='wrist', quilt=False, collar=False)
    K.belt('rope', buckle=None, width=0.05, t=0.08)
    K.chain([(ch.Jp['chest'].z + 0.3, 95), (ch.Jp['chest'].z + 0.05, 10), (ch.Jp['pelvis'].z + 0.15, -70)], hug=0.02,
            mat='rope', link=0.04, thick=0.014, name='Kelp rope')
    K.cape('cloth', length=ch.Jp['pelvis'].z - 0.6, flare=0.35, tatter=2.0, collar='coral')
    C, u = head_frame(ch, 1.0)
    add_head(ch, faces.monster_head(C, u, M, ch.coll, kind='ghoul'))
    cr = garb.F.Field('Coral crown')
    rng = np.random.default_rng(8)
    for k in range(9):
        a = (k / 8 - 0.5) * math.radians(240)
        base = C + Vector((math.cos(a) * 0.85 * u - 0.1 * u, math.sin(a) * 0.85 * u, 0.55 * u))
        tip = base + Vector((math.cos(a) * 0.3 * u, math.sin(a) * 0.3 * u, rng.uniform(0.6, 1.1) * u))
        cr.add(garb.F.Chain([base, base.lerp(tip, 0.5) + Vector((0.05 * u, 0, 0)), tip], [0.12 * u, 0.08 * u, 0.03 * u]),
               0.04 * u)
    add_head(ch, [cr.mesh(0.003, ch.coll, M['coral'], name='Coral crown')])
    stf = arms.staff(M, ch.coll, length=2.2, grip_at=0.5, head='orb', crystal=M['water'], gnarl=1.0)
    sres = ch.hold_at(stf, PS.weapon_frame(Vector((0.18, -0.42, 1.2)), deg(86, 2)), 'R', scale=1.1)
    ch.grip(sres, 'R', M['flesh'])
    ch.rest_hand('L', M['flesh'], pose='claw', size=1.1)
    fl = arms.flame(M, ch.coll, ch.hand_frames['L'].translation + Vector((0.08, 0, 0.05)), size=1.3, color='water')
    conv, _ = ch._conv('hand.L')
    for o in fl:
        geo.transform(o, conv)
        ch.parts.append((o, 'hand.L'))
    return ch, finish(ch, st, 'staff-cast', gait='heavy')


@unit('enemy_boss_forge')
def iron_warden(ident, title):
    G, D = SHOULDERED
    R, L = two_hand(G, D, -0.32)
    M = palette.zone_host('foundry')
    M['dark_steel'] = S.ember_metal('2a2420', glow='ff7a2a', name='Ember iron', strength=8.0, crack=0.025, coverage=0.4)
    M2, ch, st, K, A = setup('bones', ident, title, BRACE, dict(bulk=1.35, chest_w=1.15, scale=1.2), M=M, R=R, L=L,
                             feet=BOSS_FEET, cape=True, thick=2.0)
    boss_armor(K, A, ch, M, pauldron=1.45, spikes=0, cape=None, tabard=None)
    J = ch.Jp
    for side in ('R', 'L'):
        p = J['shoulder.' + side] + Vector((-0.04, (-1 if side == 'R' else 1) * 0.08, 0.2))
        stk = garb.F.Field('Smoke stack')
        stk.add(garb.F.RoundCone(p, p + Vector((-0.06, 0, 0.32)), 0.06, 0.05), 0.0)
        stk.cut(garb.F.RoundCone(p + Vector((0, 0, 0.1)), p + Vector((-0.06, 0, 0.36)), 0.04, 0.035), 0.003)
        K.rigid(stk.mesh(0.004, ch.coll, M['dark_steel'], name='Smoke stack'), 'upper_arm.' + side)
    glow_at(K, ch, M, J['chest'] + Vector((0.18, 0, 0.05)), 0.09, 'glow', 'chest', 'Forge heart')
    C, u = skull(ch, M, jaw_open=0.0, size=1.0)
    add_head(ch, helms.great_helm(C, u, M, ch.coll, cross=False, t=0.26, mat='dark_steel'))
    glow_at(K, ch, M, C + Vector((1.0 * u, 0, 0.05 * u)), 0.16 * u, 'glow', 'head', 'Visor fire')
    wield(ch, arms.hammer(M, ch.coll, haft_len=1.3, head=2.1), M, G, D, glove='dark_steel', g2=-0.32)
    return ch, finish(ch, st, 'royal-cleave', gait='heavy', heavy=1.4)


@unit('enemy_boss_ward')
def plague_archon(ident, title):
    G, D = SHOULDERED
    R, L = two_hand(G, D, -0.35)
    M = palette.zone_host('quarantine')
    # separate the reads: dark plague-green vestments, a near-black hood, sickly pale flesh
    M['cloth'] = S.st_cloth('3c4a26', name='Plague vestment', shadow='1e2612', sheen=0.4)
    M['cloth_dark'] = S.st_cloth('1c2216', name='Plague hood', shadow='0c100a', sheen=0.3)
    M['flesh'] = S.st_flesh('b4b49a', name='Archon flesh', bruise='6a7a4a')
    M2, ch, st, K, A = setup('flesh', ident, title, BRACE, dict(bulk=1.45, belly=1.0, chest_w=1.2, arm=1.3, scale=1.18),
                             M=M, R=R, L=L, feet=BOSS_FEET, cape=True)
    K.skirt('cloth', length=0.9, flare=0.3, t=0.014, tatter=1.6, split=0.45)
    K.belt('leather_dark', buckle='gold', width=0.09, t=0.08)
    K.cape('cloth_dark', length=ch.Jp['pelvis'].z - 0.5, flare=0.35, tatter=1.6, collar='gold')
    J = ch.Jp
    pf = garb.F.Field('Pustules')
    for z, a, r in [(J['chest'].z + 0.05, 30, 0.07), (J['pelvis'].z + 0.25, 10, 0.08), (J['chest'].z + 0.2, -50, 0.06),
                    (J['pelvis'].z + 0.15, 55, 0.05), (J['chest'].z - 0.12, -15, 0.06)]:
        pf.add(garb.F.Sphere(K.around([(z, a)], 0.0)[0], r * 0.75), 0.015)
    K.put(pf.mesh(0.004, ch.coll, M['pustule'], name='Pustules', smooth=1))
    for side, sy in (('R', -1), ('L', 1)):
        c = J['hip.' + side] + Vector((0.05, sy * 0.28, 0.0))
        cs = garb.F.Field('Censer')
        cs.add(garb.F.Sphere(c, 0.09), 0.0)
        cs.add(garb.F.RoundCone(c + Vector((0, 0, 0.08)), c + Vector((0, 0, 0.25)), 0.008, 0.008), 0.0)
        K.rigid(cs.mesh(0.003, ch.coll, M['gold'], name='Censer'), 'hips')
        glow_at(K, ch, M, c + Vector((0.05, 0, 0.02)), 0.04, 'glow', 'hips', 'Censer fume')
    C, u = head_frame(ch, 1.0)
    add_head(ch, faces.monster_head(C, u, M, ch.coll, kind='brute'))
    bk = garb.F.Field('Plague mask')
    bk.add(garb.F.RoundCone(C + Vector((0.5 * u, 0, -0.1 * u)), C + Vector((2.0 * u, 0, -0.7 * u)), 0.55 * u, 0.08 * u),
           0.0)
    for sy in (-1, 1):
        bk.cut(garb.F.Sphere(C + Vector((0.9 * u, sy * 0.35 * u, 0.05 * u)), 0.14 * u), 0.01 * u)
    add_head(ch, [bk.mesh(0.004, ch.coll, M['leather_dark'], name='Plague mask')])
    add_head(ch, helms.hood(C, u, M['cloth_dark'], ch.coll, point=0.8, cowl=1.2, opening=1.0))
    sc = arms.polearm(M, ch.coll, 'halberd', length=2.3)
    wield(ch, sc, M, G, D, glove='leather_dark', g2=-0.35)
    return ch, finish(ch, st, 'royal-cleave', gait='heavy', heavy=1.3)


@unit('enemy_boss_pass')
def thornwall_chieftain(ident, title):
    G, D = Vector((0.08, -0.46, 1.4)), deg(95, -25)
    M = palette.zone_host('thornwall')
    M['flesh'] = S.st_flesh('8a7a5a', name='Thorn hide', bruise='5a3a2a')
    M['fur'] = S.st_hair('5a4632', name='Fur mantle', tip='8a7258', stripes=60.0)
    M2, ch, st, K, A = setup('flesh', ident, title, BRACE, dict(bulk=1.4, chest_w=1.2, arm=1.35, forearm=1.4, scale=1.18),
                             M=M, R=PS.Grip(G, D, pole=(-0.3, -0.9, 1.2)),
                             L=PS.Reach(Vector((0.5, 0.4, 1.1)), pole=(0.0, 0.9, 1.0), fingers=Vector((0.7, -0.2, -0.5)),
                                        palm=Vector((0, -1, 0)), pose='fist'), feet=BOSS_FEET, cape=True)
    K.trousers('trousers', bottom=0.45, baggy=0.6)
    K.boots('leather_dark', cuff='fur', height=0.5)
    K.loincloth('cloth', length=0.55, width=0.22, sides=1, tatter=1.2)
    K.belt('leather_dark', buckle='gold', width=0.1, t=0.08)
    K.cape('fur', length=ch.Jp['knee.R'].z + 0.1, width=1.1, flare=0.25, tatter=1.8, collar='fur')
    A.pauldron('R', size=1.3, lames=2, spikes=3)
    # a wolf pelt slung over the left shoulder
    pl = garb.F.Field('Pelt')
    sh = ch.Jp['shoulder.L']
    pl.add(garb.F.Ellipsoid(sh + Vector((-0.02, 0.04, 0.06)), (0.2, 0.17, 0.11)), 0.0)
    pl.add(garb.F.RoundCone(sh + Vector((0.08, 0.02, 0.0)), sh + Vector((0.2, -0.06, -0.24)), 0.09, 0.05), 0.06)
    pl.add(garb.F.RoundCone(sh + Vector((-0.12, 0.06, 0.0)), sh + Vector((-0.2, 0.02, -0.3)), 0.09, 0.04), 0.06)
    pl.disp = lambda P_: garb.F.noise3(P_ * np.array([1, 1, 3.0]), 40.0, 3, 2) * 0.01
    K.rigid(pl.mesh(0.004, ch.coll, M['fur'], name='Pelt'), 'upper_arm.L')
    for side in ('R', 'L'):
        A.vambrace(side, t=0.03)
    C, u = head_frame(ch, 1.0)
    add_head(ch, faces.monster_head(C + Vector((0.05, 0, -0.05)), u, M, ch.coll, kind='brute'))
    add_head(ch, helms.crown(C, u, M['wood'], ch.coll, points=9, height=0.85, spikes=True, z=0.55, radius=0.95))
    hn = garb.F.Field('Chieftain horns')
    for sy in (-1, 1):
        pts = [C + Vector((-0.1 * u, sy * 0.85 * u, 0.55 * u)), C + Vector((-0.45 * u, sy * 1.55 * u, 0.8 * u)),
               C + Vector((-0.2 * u, sy * 2.0 * u, 0.2 * u)), C + Vector((0.35 * u, sy * 1.9 * u, -0.1 * u))]
        hn.add(garb.F.Chain(pts, [0.4 * u, 0.3 * u, 0.18 * u, 0.03 * u]), 0.05 * u)
    hn.disp = lambda P_: 0.004 * np.sin(np.linalg.norm(P_ - np.array(tuple(C)), axis=1) * 160)
    add_head(ch, [hn.mesh(0.003, ch.coll, M['horn'] if 'horn' in M else M['bone'], name='Chieftain horns')])
    wield(ch, arms.axe(M, ch.coll, haft_len=1.0, head=1.9, beard=True), M, G, D, glove='flesh')
    ch.rest_hand('L', M['flesh'], pose='fist', size=1.25)
    return ch, finish(ch, st, 'axe-cleave', gait='heavy', heavy=1.35)


@unit('enemy_boss_basilica')
def bone_pontiff(ident, title):
    M = palette.zone_host('basilica')
    M['holy'] = S.emissive('ffd98a', 6, name='Unholy gold light', core='fffbe6')
    M2, ch, st, K, A = setup('bones', ident, title, STRIDE, dict(bulk=1.1, scale=1.2), M=M, cape=True, thick=1.6,
                             R=PS.Grip(Vector((0.16, -0.4, 1.2)), deg(86, 2), pole=(-0.3, -0.8, 1.0)),
                             L=PS.Reach(Vector((0.52, 0.24, 1.42)), pole=(0.05, 0.8, 1.1), fingers=Vector((1, 0.1, 0.5)),
                                        palm=Vector((0, -0.3, 1)), pose='open'), feet=BOSS_FEET)
    K.skirt('linen', length=1.0, flare=0.3, t=0.014, tatter=0.6, split=0.4)
    K.gambeson('cloth', t=0.035, skirt=0.35, sleeves='wrist', quilt=False, collar=False)
    K.tabard('gold', trim='linen', emblem='rot', emblem_mat='cloth', width=0.15, length=ch.Jp['knee.R'].z - 0.1, t=0.06)
    K.belt('leather_dark', buckle='gold', t=0.08)
    K.cape('cloth', length=ch.Jp['pelvis'].z - 0.7, flare=0.4, tatter=0.8, collar='gold')
    C, u = skull(ch, M, jaw_open=0.4, size=1.0)
    add_head(ch, helms.mitre(C, u, M, ch.coll) if hasattr(helms, 'mitre') else
             helms.crown(C, u, M['gold'], ch.coll, points=2, height=1.6, z=0.6, radius=0.85))
    hl = garb.F.Field('Halo')
    hl.add(garb.F.Torus(C + Vector((-0.45 * u, 0, 0.6 * u)), 1.4 * u, 0.06 * u, Matrix.Rotation(math.radians(75), 3, 'Y')),
           0.0)
    add_head(ch, [hl.mesh(0.003, ch.coll, M['gold'], name='Halo')])
    stf = arms.staff(M, ch.coll, length=2.3, grip_at=0.5, head='crook', gnarl=0.0, wood='gold')
    sres = ch.hold_at(stf, PS.weapon_frame(Vector((0.16, -0.4, 1.2)), deg(86, 2)), 'R', scale=1.1)
    ch.grip(sres, 'R', M['bone'])
    ch.rest_hand('L', M['bone'], pose='open', size=1.1)
    fl = arms.flame(M, ch.coll, ch.hand_frames['L'].translation + Vector((0.06, 0, 0.08)), size=1.2, color='holy')
    conv, _ = ch._conv('hand.L')
    for o in fl:
        geo.transform(o, conv)
        ch.parts.append((o, 'hand.L'))
    return ch, finish(ch, st, 'staff-cast', gait='heavy')


@unit('enemy_boss_mire')
def mire_behemoth(ident, title):
    M = palette.zone_host('mire')
    M['flesh'] = S.st_flesh('5a6a4a', name='Mire hide', bruise='3a4a2a', wet=0.6)
    M['moss'] = S.st_hair('4a5a2a', name='Moss', tip='7a8a4a', stripes=40.0)
    M2, ch, st, K, A = setup('flesh', ident, title, HUNCHED, dict(bulk=1.6, belly=1.1, chest_w=1.3, arm=1.6, forearm=1.7,
                                                                  hand=1.6, leg=1.4, leg_len=0.86, scale=1.22, hunch=12),
                             M=M, R=PS.Reach(Vector((0.42, -0.66, 0.66)), pole=(0.0, -0.9, 1.0),
                                             fingers=Vector((0.5, 0, -0.8)), palm=Vector((0, 1, -0.2)), pose='claw'),
                             L=PS.Reach(Vector((0.62, 0.52, 0.7)), pole=(0.1, 0.9, 1.0), fingers=Vector((0.5, 0, -0.8)),
                                        palm=Vector((0, -1, -0.2)), pose='claw'), feet=BOSS_FEET)
    ch.claws = 1.6
    J = ch.Jp
    ms = garb.F.Field('Moss mantle')
    base = K.base(['torso', 'neck', 'arm'])
    lo, hi = base.bounds(0.2)
    ms.add(garb.F.Fn(lambda P_: base.eval(P_) - 0.05 - 0.04 * np.clip((P_[:, 2] - J['chest'].z) / 0.3, 0, 1), lo, hi), 0.0)
    ms.keep(garb.F.Fn(lambda P_: J['chest'].z - 0.05 - P_[:, 2] + 0.08 * np.sin(P_[:, 1] * 20), lo, hi), 0.02)
    ms.disp = lambda P_: garb.F.noise3(P_, 10.0, 3, 2) * 0.02
    K.put(ms.mesh(0.006, ch.coll, M['moss'], name='Moss mantle'))
    K.leg_skirt('moss', length=0.7, t=0.03, flare=0.12, tatter=2.0)
    C, u = head_frame(ch, 1.0)
    add_head(ch, faces.monster_head(C + Vector((0.12, 0, -0.15)), u, M, ch.coll, kind='brute'))
    for side in ('R', 'L'):
        ch.rest_hand(side, M['flesh'], pose='claw', size=1.5)
    return ch, finish(ch, st, 'claw-rake', gait='heavy', heavy=1.4)


@unit('enemy_boss_verge')
def gloamwood_witch(ident, title):
    M = palette.zone_host('gloamwood')
    M['skin'] = S.st_skin('a898b0', blush='7a5a8a')
    M['wing'] = S.st_cloth('5a4a6a', name='Night wing', shadow='2a2034', sheen=0.6)
    M['crystal'] = M['glow']
    M2, ch, st, K, A = setup('human', ident, title, STRIDE, dict(bulk=1.0, female=1.0, scale=1.2), M=M, cape=True,
                             R=PS.Grip(Vector((0.16, -0.38, 1.18)), deg(86, 2), pole=(-0.3, -0.8, 1.0)),
                             L=PS.Reach(Vector((0.54, 0.24, 1.4)), pole=(0.05, 0.8, 1.1), fingers=Vector((1, 0.1, 0.5)),
                                        palm=Vector((0, 0, 1)), pose='claw'), feet=BOSS_FEET)
    K.skirt('cloth', length=1.0, flare=0.32, t=0.014, tatter=1.8, split=0.45)
    K.gambeson('cloth_dark', t=0.03, skirt=0.3, sleeves='wrist', quilt=False, collar=False)
    K.belt('leather_dark', buckle='gold')
    moth_wings(ch, K, M, M['wing'], span=1.15)
    C, u = dress_head(ch, M, face=dict(female=1.0, frown=1.0, gaunt=0.4, nose=1.1), hair=dict(style='long', length=1.0))
    add_head(ch, helms.crown(C, u, M['wood'], ch.coll, points=2, height=1.6, spikes=True, z=0.7, radius=0.7))
    stf = arms.staff(M, ch.coll, length=2.2, grip_at=0.5, head='orb', crystal=M['glow'], gnarl=1.5)
    sres = ch.hold_at(stf, PS.weapon_frame(Vector((0.16, -0.38, 1.18)), deg(86, 2)), 'R', scale=1.1)
    ch.grip(sres, 'R', M['skin'])
    ch.rest_hand('L', M['skin'], pose='claw')
    fl = arms.flame(M, ch.coll, ch.hand_frames['L'].translation + Vector((0.08, 0, 0.05)), size=1.2, color='flame')
    conv, _ = ch._conv('hand.L')
    for o in fl:
        geo.transform(o, conv)
        ch.parts.append((o, 'hand.L'))
    return ch, finish(ch, st, 'staff-cast')


@unit('enemy_boss_citadel')
def dread_sovereign(ident, title):
    G, D = Vector((0.22, -0.2, 1.1)), deg(62, 14)
    R, L = two_hand(G, D, -0.2)
    M, ch, st, K, A = setup('bones', ident, title, BRACE, dict(bulk=1.25, scale=1.22), zone='citadel', R=R, L=L,
                            feet=BOSS_FEET, cape=True, thick=1.8)
    K.loincloth('cloth', length=0.6, width=0.2, sides=1, tatter=1.2, t=0.06)
    boss_armor(K, A, ch, M, pauldron=1.4, spikes=3, cape='cloth', cape_len=0.1, tabard='cloth_dark', emblem='rot')
    C, u = skull(ch, M, jaw_open=0.2, size=1.0)
    add_head(ch, helms.armet(C, u, M, ch.coll, mat='dark_steel', trim='gold'))
    add_head(ch, helms.crown(C, u, M['gold'], ch.coll, points=7, height=0.6, z=0.95, radius=1.05, spikes=True,
                             gems=M['glow']))
    wield(ch, arms.sword(M, ch.coll, length=1.3, width=0.18, guard=0.44, grip=0.3, two_hand=True), M, G, D,
          glove='dark_steel', g2=-0.2)
    return ch, finish(ch, st, 'royal-cleave', gait='heavy', heavy=1.3)


@unit('enemy_boss_reliquary')
def reliquary_tyrant(ident, title):
    M = palette.zone_host('basilica')
    M['relic'] = S.emissive('ffb040', 6, name='Relic light', core='fff0c0')
    M['crystal'] = M['relic']
    M2, ch, st, K, A = setup('bones', ident, title, STRIDE, dict(bulk=1.15, scale=1.2), M=M, cape=True, thick=1.6,
                             R=PS.Grip(Vector((0.18, -0.4, 1.2)), deg(86, 2), pole=(-0.3, -0.8, 1.0)),
                             L=PS.Reach(Vector((0.52, 0.24, 1.36)), pole=(0.05, 0.8, 1.1), fingers=Vector((1, 0.1, 0.4)),
                                        palm=Vector((0, 0, 1)), pose='claw'), feet=BOSS_FEET)
    K.skirt('cloth', length=0.95, flare=0.3, t=0.014, tatter=1.0, split=0.4)
    K.gambeson('cloth_dark', t=0.035, skirt=0.35, sleeves='wrist', quilt=False, collar=False)
    A.cuirass(t=0.07)
    K.belt('leather_dark', buckle='gold', t=0.09)
    for side in ('R', 'L'):
        A.pauldron(side, size=1.25, lames=2)
    K.cape('cloth', length=ch.Jp['pelvis'].z - 0.6, flare=0.35, tatter=1.2, collar='gold')
    J = ch.Jp
    back = K._surface_point(J['chest'], Vector((-1, 0, 0)), K.base(['torso']), 0.1)
    rb = garb.F.Field('Reliquary')
    rc = back + Vector((-0.2, 0, 0.1))
    rb.add(garb.F.Box(rc, (0.16, 0.26, 0.24), r=0.03), 0.0)
    rb.add(garb.F.Box(rc + Vector((0, 0, 0.3)), (0.12, 0.2, 0.08), r=0.03), 0.02)
    for sy in (-1, 1):
        rb.add(garb.F.RoundCone(rc + Vector((0, sy * 0.24, 0.24)), rc + Vector((0, sy * 0.24, 0.56)), 0.03, 0.005), 0.01)
    K.rigid(rb.mesh(0.004, ch.coll, M['gold'], name='Reliquary'), 'chest')
    glow_at(K, ch, M, rc + Vector((0.165, 0, 0.0)), 0.07, 'relic', 'chest', 'Relic window')
    C, u = skull(ch, M, jaw_open=0.3, size=1.0)
    add_head(ch, helms.crown(C, u, M['gold'], ch.coll, points=5, height=0.6, gems=M['relic']))
    stf = arms.staff(M, ch.coll, length=2.2, grip_at=0.5, head='orb', crystal=M['relic'], gnarl=0.0, wood='gold')
    sres = ch.hold_at(stf, PS.weapon_frame(Vector((0.18, -0.4, 1.2)), deg(86, 2)), 'R', scale=1.1)
    ch.grip(sres, 'R', M['bone'])
    ch.rest_hand('L', M['bone'], pose='claw', size=1.1)
    return ch, finish(ch, st, 'staff-cast', gait='heavy', float_mode=False)


@unit('enemy_boss_tidemaster')
def harrow_tidemaster(ident, title):
    G, D = Vector((-0.04, -0.46, 1.44)), deg(16, -6)
    M = palette.zone_host('harbor')
    M['coat'] = S.st_cloth('2a3a44', name='Captain coat', shadow='141e24')
    M2, ch, st, K, A = setup('flesh', ident, title, BRACE, dict(bulk=1.3, belly=0.4, chest_w=1.15, scale=1.2), M=M,
                             cape=True, R=PS.Grip(G, D, pole=(-0.4, -0.85, 1.2)),
                             L=PS.Reach(Vector((0.48, 0.24, 1.2)), pole=(0.0, 0.8, 1.0), fingers=Vector((1, -0.2, 0.1)),
                                        palm=Vector((0, -1, 0)), pose='open'), feet=BOSS_FEET)
    K.shirt('linen', sleeves='wrist', t=0.012, loose=0.6)
    K.gambeson('coat', t=0.035, skirt=0.5, sleeves='wrist', quilt=False, collar=True)
    K.trousers('trousers_dark', bottom=0.5)
    K.boots('leather_dark', cuff='leather', height=0.7)
    K.belt('leather', buckle='gold', t=0.09, pouches=[(-80, 1.1)])
    K.strap('leather_dark', ch.Jp['shoulder.L'] + Vector((0.05, -0.04, 0.08)),
            Vector((ch.Jp['pelvis'].x + 0.1, -0.2, ch.Jp['pelvis'].z + 0.06)), width=0.05)
    K.cape('cloth', length=ch.Jp['pelvis'].z - 0.3, flare=0.3, tatter=1.8, collar='coat')
    C, u = head_frame(ch, 1.0)
    add_head(ch, faces.monster_head(C, u, M, ch.coll, kind='ghoul'))
    hat = helms.kettle(C, u, dict(dark_steel=M['coat'], trim=M['gold']), ch.coll, brim=1.6, t=0.13)
    add_head(ch, hat)
    hp = arms.spear(M, ch.coll, length=2.6, head_len=0.36, head_w=0.14, grip_at=0.28, wings=True)
    wield(ch, hp, M, G, D, glove='flesh', scale=1.15)
    ch.rest_hand('L', M['flesh'], pose='open', size=1.1)
    return ch, finish(ch, st, 'flask-toss', gait='heavy', release=hp['objs'])


@unit('enemy_boss_plague_monarch')
def plague_monarch(ident, title):
    M = palette.zone_host('quarantine')
    M['royal'] = S.st_cloth('5a2a5a', name='Plague purple', shadow='2a102a')
    M2, ch, st, K, A = setup('flesh', ident, title, BRACE, dict(bulk=1.6, belly=1.5, chest_w=1.25, arm=1.4, forearm=1.45,
                                                                 hand=1.4, leg=1.3, leg_len=0.88, scale=1.2), M=M,
                             cape=True, R=PS.Grip(Vector((0.3, -0.62, 1.1)), deg(80, -10), pole=(-0.2, -0.9, 1.0)),
                             L=PS.Reach(Vector((0.56, 0.5, 1.12)), pole=(0.0, 0.9, 1.0), fingers=Vector((0.8, -0.2, 0.0)),
                                        palm=Vector((0, -1, 0)), pose='open'), feet=BOSS_FEET)
    K.leg_skirt('royal', length=0.7, t=0.03, flare=0.1, tatter=1.0)
    K.belt('leather_dark', buckle='gold', width=0.1, t=0.08)
    K.cape('royal', length=ch.Jp['pelvis'].z - 0.6, flare=0.4, tatter=1.2, collar='gold')
    J = ch.Jp
    pf = garb.F.Field('Pustules')
    for z, a, r in [(J['chest'].z + 0.05, 30, 0.08), (J['pelvis'].z + 0.3, 10, 0.1), (J['chest'].z + 0.25, -50, 0.07),
                    (J['pelvis'].z + 0.15, 55, 0.07), (J['chest'].z - 0.12, -15, 0.08), (J['pelvis'].z + 0.4, -60, 0.06),
                    (J['chest'].z + 0.15, 100, 0.07)]:
        pf.add(garb.F.Sphere(K.around([(z, a)], 0.0)[0], r * 0.75), 0.015)
    K.put(pf.mesh(0.004, ch.coll, M['pustule'], name='Pustules', smooth=1))
    C, u = head_frame(ch, 0.95)
    add_head(ch, faces.monster_head(C + Vector((0.1, 0, -0.1)), u, M, ch.coll, kind='brute'))
    add_head(ch, helms.crown(C + Vector((0.1, 0, -0.1)), u, M['gold'], ch.coll, points=7, height=0.55, gems=M['glow']))
    sc = arms.mace(M, ch.coll, haft_len=1.0, head=1.6, flanges=6, wood='gold', steel='gold')
    wield(ch, sc, M, Vector((0.3, -0.62, 1.1)), deg(80, -10), glove='flesh', scale=1.2)
    ch.rest_hand('L', M['flesh'], pose='open', size=1.3)
    return ch, finish(ch, st, 'heavy-smash', gait='heavy', heavy=1.4)


@unit('enemy_boss_steppe')
def steppe_warlord(ident, title):
    from .mounts import MOUNT_FACING, lance, rider_setup
    from .beasts import rider_clips, rider_on
    from rk import character as CH
    CH.set_facing(MOUNT_FACING)
    M = palette.zone_host('steppe')
    coat = S.st_leather('3a2e26', name='Black coat', rough=0.55, edge=0.25, sheen=0.25)
    mane = S.st_hair('1a1612', name='Black mane', tip='3a3028')
    hoof = S.st_leather('2a2420', name='Hoof', rough=0.4)
    hc = Character(ident, title, skeleton_fn=B.skeleton('horse'))
    steed.build(hc, M, 'living', coat=coat, mane=mane, hoof=hoof, bulk=1.12)
    steed.barding(hc, M, M['cloth'], trim=M['bone'], emblem='skull', emblem_mat=M['bone'], plate=True, tatter=1.4,
                  chanfron='dark_steel')
    G = Vector((0.18, -0.24, 1.1))
    D = deg(6, 4)
    rider, st, K, A, off = rider_setup(hc, ident, title, M, dict(bulk=1.2, scale=1.05), bones=True,
                                       R=PS.Grip(G, D, pole=(-0.3, -0.7, 1.0)),
                                       L=PS.Reach(Vector((0.4, 0.14, 1.12)), pole=(0.0, 0.6, 1.0),
                                                  fingers=Vector((0.4, -0.3, -0.6)), palm=Vector((0, -1, 0)), pose='fist'))
    K.loincloth('cloth', length=0.35, width=0.18, sides=1, tatter=1.3)
    A.cuirass(t=0.065)
    K.belt('leather_dark', buckle='gold', t=0.09)
    for side in ('R', 'L'):
        A.pauldron(side, size=1.3, lames=3, spikes=2)
        A.vambrace(side, t=0.03)
        A.greave(side, t=0.03)
    K.cape('cloth', length=rider.Jp['pelvis'].z - 0.2, flare=0.35, tatter=1.8, collar='cloth_dark')
    from .host import skull_head, ghost_head_field
    C, u, _ = skull_head(rider, M, jaw_open=0.3)
    ghost_head_field(rider, C, u)
    add_head(rider, helms.burgonet(C, u, M, rider.coll, cheeks=True, peak=True, comb=True))
    add_head(rider, helms.crown(C, u, M['bone'], rider.coll, points=2, height=1.0, spikes=True, z=0.8))
    ln = lance(M, rider.coll, length=3.0, pennon=M['cloth'])
    lr = rider.hold_at(ln, PS.weapon_frame(G, D), 'R')
    rider.grip(lr, 'R', M['bone'])
    rider.rest_hand('L', M['bone'], pose='fist')
    hst, HC = B.clips('horse')
    RC = rider_clips(st, 'mounted-lance')
    rider_on(hc, rider)
    opts = finish_clips(hc, HC, 'mounted-lance', body_z=1.9, cam_z=1.6, min_scale=4.6, stance=hst,
                        companions=[(rider, RC, st)], portrait=dict(target=(0.5, 0, 2.05), distance=7.5))
    hc.tip_bone_owner = rider
    return hc, opts
