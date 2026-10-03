"""Rotbound Host roster: risen infantry, plague beasts, ritual casters and grave elites."""
import math
import random

from mathutils import Matrix, Vector

from rk import armor as A, geo, heads as H, palette, shaders as S, undead as U, weapons as W
from rk.anim import P, STANCES
from rk.character import Character
from rk.rig import LAT, YAW, ROLL, add_poses

from . import unit
from .common import add_head, boots, finish, gloves, head_frame, pelt, plate_arms, plate_legs, profile_of


def _cape(sk, J):
    A.cape_bones(sk, J)


def rotten_head(ch, M, C, R, gaunt=1.0, jawless=True, hair=None, glow=None):
    out = H.human_head(C, R, dict(M, eye=glow or M['glow']), ch.coll, gaunt=gaunt, nose=0.6, brow=1.3, ears=False,
                       eye_glow=glow or M['glow'])
    if jawless:
        for i in range(7):
            a = (i - 3) / 3 * 0.75
            p = C + Vector((R * (.92 * math.cos(a) + .02), R * .55 * math.sin(a), -R * .5))
            out.append(geo.box('Bared tooth', (R * .1, R * .09, R * .16), p, M['bone'], ch.coll, bevel=R * .02,
                               rotation=(0, 0, math.degrees(a))))
    if hair is not None:
        out += H.hair_cap(C, R, hair, ch.coll, front=0.35, back=-0.7, length=0.6, strands=2.0)
    add_head(ch, out)
    return out


def grave_emblem(M):
    return W.emblem_skull(M['bone'], M['glow'])


def flesh_body(ch, M, torso=None, sleeve=None, legs=None, glove=None):
    return ch.build_body(dict(skin=M['skin'], torso=torso or M['skin'], sleeve=sleeve or M['skin'], glove=glove or M['skin'],
                              legs=legs or M['trousers']))


def claws(ch, M, side, length=0.16, count=4, mat=None):
    wr, ht = ch.J['wrist.' + side], ch.J['hand_tip.' + side]
    d = (ht - wr).normalized()
    out = []
    for k in range(count):
        base = wr.lerp(ht, .8) + Vector((0.03, (k - (count - 1) / 2) * 0.03, 0))
        tip = base + d * length + Vector((length * .5, 0, -length * .2))
        mid = base.lerp(tip, .5) + Vector((length * .15, 0, 0))
        out.append((geo.tube('Claw', [base, mid, tip], [0.016, 0.012, 0.001], mat or M['bone_dark'], ch.coll, sides=6),
                    'hand.' + side))
    ch.add(out)


def pustules(ch, M, center, radius, n, bone='chest', mat=None, seed=1, size=0.05):
    rnd = random.Random(seed)
    out = []
    for _ in range(n):
        a = rnd.uniform(-1.1, 1.1)
        z = rnd.uniform(-1, 1)
        p = Vector(center) + Vector((math.cos(a) * radius[0], math.sin(a) * radius[1], z * radius[2]))
        out.append((geo.sphere('Plague pustule', size * rnd.uniform(.6, 1.3), p, mat or M['plague'], ch.coll, 10, 6), bone))
    ch.add(out)


# ------------------------------------------------------------------ infantry
@unit('enemy_walker')
def risen(ident, title):
    M = palette.rotbound()
    ch = Character(ident, title, dict(bulk=0.95, thin=0.55))
    st = add_poses(STANCES['onehand'], P(spine=[(-8, LAT), (6, ROLL)], head=[(14, LAT), (-10, ROLL)],
                                         upper_arm_L=[(24, LAT)], forearm_L=10))
    ch.set_stance(st)
    flesh_body(ch, M, torso=M['linen'], sleeve=M['skin'], legs=M['trousers'])
    C, R = head_frame(ch)
    rotten_head(ch, M, C, R, hair=M['hair'])
    add_head(ch, H.kettle_hat(C + Vector((-R * .05, R * .05, R * .2)), R, M, ch.coll))
    ch.add(A.shell_torso('Rusted mail', ch.s, ch.J, M['mail'], ch.coll, inflate=0.018, z_from=ch.J['pelvis'].z - 0.2))
    ch.add(A.tabard(ch.J, ch.s, M['cloth'], ch.coll, width=0.17, length=ch.J['knee.R'].z + 0.1, hem='dagged',
                    emblem=grave_emblem(M)))
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=1))
    ch.add(A.pauldron(ch.J, 'R', M, ch.coll, size=0.8, lames=2))
    ch.add(A.vambrace(ch.J, 'R', M, ch.coll))
    ch.add(A.gauntlet(ch.J, 'R', M, ch.coll, mat_key='leather_dark'))
    boots(ch, M, mat=M['leather_dark'], shaft=0.18)
    ch.hold(W.sword(dict(M, blade=M['blade']), ch.coll, length=0.7, width=0.075, guard=0.2, pommel='disc',
                    single_edge=True, curve=0.05), 'R', pitch=40, yaw=-4)
    return ch, finish(ch, st, profile_of(ident), gait='shamble', death='forward')


@unit('enemy_runner')
def ghoul(ident, title):
    M = palette.rotbound(flesh='8a9479')
    ch = Character(ident, title, dict(bulk=0.82, thin=0.8, hunch=28, arm_len=1.22, hand=1.25, leg_len=0.92))
    st = STANCES['claws']
    ch.set_stance(st)
    flesh_body(ch, M, legs=M['skin'])
    C, R = head_frame(ch)
    add_head(ch, H.ghoul_head(C, R * 1.05, dict(skin=M['skin'], glow=M['glow'], bone=M['bone']), ch.coll))
    for side in ('R', 'L'):
        claws(ch, M, side, 0.17)
    U.rags(ch, M['cloth_dark'], ch.coll, z_bottom=ch.J['knee.R'].z + 0.2, tatter=1.5)
    # protruding spine ridge
    for i in range(6):
        p = ch.J['spine'].lerp(ch.J['neck'], i / 5) + Vector((-0.17, 0, 0))
        ch.parts.append((geo.cylinder('Spine spike', 0.025, 0.08, p, M['bone_dark'], ch.coll, 6, radius2=0.003,
                                      rotation=(0, -70, 0), bevel=0), 'chest' if i > 2 else 'spine'))
    for side in ('R', 'L'):
        ch.add(A.boot(ch.J, side, M['skin'], ch.coll, shaft=0, toe='pointed'))
    return ch, finish(ch, st, profile_of(ident), gait='prowl', death='forward', cape=False)


@unit('enemy_bloater')
def rot_hulk(ident, title):
    M = palette.rotbound(flesh='77845f')
    ch = Character(ident, title, dict(bulk=1.55, chest_w=1.2, belly=1.3, hunch=12, hand=1.3, shoulder=0.3,
                                      arm_len=1.1))
    st = STANCES['brute']
    ch.set_stance(st)
    flesh_body(ch, M, legs=M['trousers'])
    C, R = head_frame(ch, 0.92)
    rotten_head(ch, M, C + Vector((0.04, 0, -0.06)), R, gaunt=0.0, glow=M['plague'])
    # stitched belly seam and pustules
    belly = ch.J['spine'] + Vector((0.42, 0, -0.08))
    seam = [belly + Vector((0, -0.18 + 0.36 * i / 7, 0.12 * math.sin(i))) for i in range(8)]
    ch.parts.append((geo.tube('Stitch seam', seam, 0.012, M['leather_dark'], ch.coll, sides=5), {'skin': ['spine', 'hips']}))
    for i in range(7):
        p = seam[i]
        ch.parts.append((geo.box('Stitch', (0.02, 0.012, 0.09), p + Vector((0.01, 0, 0)), M['rope'], ch.coll, bevel=.004,
                                 rotation=(0, 0, 0)), {'skin': ['spine', 'hips']}))
    pustules(ch, M, ch.J['spine'] + Vector((0.22, 0, 0.1)), (0.3, 0.32, 0.25), 14, 'spine', seed=4, size=0.05)
    pustules(ch, M, ch.J['chest'] + Vector((0.0, 0, 0.05)), (0.25, 0.35, 0.15), 8, 'chest', seed=7, size=0.045)
    U.rags(ch, M['cloth'], ch.coll, z_bottom=ch.J['knee.R'].z + 0.05)
    ch.add(A.belt(ch.J, dict(ch.s, belly=1.3), dict(M, leather=M['rope']), ch.coll, pouches=0))
    ch.add(A.pauldron(ch.J, 'L', dict(M, steel=M['steel']), ch.coll, size=1.2, lames=2, spikes=3))
    for side in ('R', 'L'):
        claws(ch, M, side, 0.12, 3)
        ch.add(A.boot(ch.J, side, M['skin'], ch.coll, shaft=0, size=1.3))
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.2, death='forward', cape=False)


@unit('enemy_brute')
def grave_brute(ident, title):
    M = palette.rotbound(flesh='8d8a78')
    ch = Character(ident, title, dict(bulk=1.45, chest_w=1.3, shoulder=0.32, hunch=18, hand=1.45, arm_len=1.15))
    st = STANCES['brute']
    ch.set_stance(st)
    flesh_body(ch, M, legs=M['trousers'])
    C, R = head_frame(ch, 0.9)
    C = C + Vector((0.06, 0, -0.08))
    rotten_head(ch, M, C, R, gaunt=0.2)
    add_head(ch, [geo.box('Iron jaw', (R * .9, R * 1.5, R * .55), C + Vector((R * .45, 0, -R * .75)), M['dark_steel'], ch.coll,
                          bevel=R * .1)])
    add_head(ch, H.horns_pair(C, R, M['horn'], ch.coll, length=1.1, up=0.3, back=0.6, thick=0.25))
    ch.add(pelt(A.mantle(ch.J, ch.s, S.fur('2e2a26', name='Black pelt', tip='5a524a'), ch.coll, thickness=0.06, ruff=1.4),
                S.fur('2b2724', name='Black pelt strands', tip='6a625a', rough=0.85), length=0.07))
    for side in ('R', 'L'):
        ch.add(A.pauldron(ch.J, side, dict(M, steel=M['dark_steel'], spike=M['bone']), ch.coll, size=1.35, lames=2, spikes=2))
        ch.add(A.vambrace(ch.J, side, M, ch.coll, r=0.1, mat_key='dark_steel'))
        ch.add(A.boot(ch.J, side, M['leather_dark'], ch.coll, shaft=0.15, size=1.35))
    ch.add(A.skirt('Hide skirt', ch.J, S.fur('4a3a2c', name='Hide'), ch.coll, ch.J['pelvis'].z + 0.04, ch.J['knee.R'].z,
                   r_top=(0.26, 0.3), r_bottom=(0.32, 0.37), folds=8, split=0.6))
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=0))
    club = W.hammer(dict(M, steel=M['bone']), ch.coll, haft_len=1.0, head=1.6, two_hand=False, block=True,
                    head_mat='steel')
    ch.hold(club, 'R', pitch=60, yaw=-6)
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.25, death='forward')


@unit('enemy_crusher')
def bone_juggernaut(ident, title):
    M = palette.rotbound()
    ch = Character(ident, title, dict(bulk=1.55, chest_w=1.35, shoulder=0.33, hunch=10, hand=1.4))
    st = STANCES['brute']
    ch.set_stance(st)
    ch.build_body(dict(skin=M['bone_dark'], torso=M['mail'], sleeve=M['mail'], glove=M['bone_dark'], legs=M['mail']))
    C, R = head_frame(ch, 0.95)
    add_head(ch, H.great_helm(C, R, dict(M, steel=M['dark_steel'], trim=M['bone']), ch.coll, cross=False,
                              horns=dict(mat=M['bone'], length=1.6, up=1.0, thick=0.28)))
    BM = dict(M, steel=M['dark_steel'], trim=M['bone'], spike=M['bone'])
    ch.add(A.breastplate(ch.s, ch.J, BM, ch.coll, fauld=3, ridge=0.04))
    # rib plates on the breastplate
    for i in range(4):
        z = ch.J['chest'].z + 0.1 - i * 0.09
        for sy in (-1, 1):
            pts = [Vector((0.3 * ch.s['bulk'] * .85, sy * 0.02, z)), Vector((0.28 * ch.s['bulk'] * .85, sy * 0.18, z - 0.03)),
                   Vector((0.15, sy * 0.33, z - 0.06))]
            ch.parts.append((geo.tube('Rib plate', pts, 0.022, M['bone'], ch.coll, sides=6, flatten=.6), 'chest'))
    for side in ('R', 'L'):
        ch.add(A.pauldron(ch.J, side, BM, ch.coll, size=1.5, lames=3, spikes=3))
        sk = geo.quadsphere('Pauldron skull', 0.09, ch.J['shoulder.' + side] + Vector((0.06, (-1 if side == 'R' else 1) * 0.2, 0.0)),
                            M['bone'], ch.coll, 3, scale=(1.1, .85, 1))
        ch.parts.append((sk, 'upper_arm.' + side))
        ch.add(A.vambrace(ch.J, side, BM, ch.coll, r=0.105))
        ch.add(A.gauntlet(ch.J, side, BM, ch.coll, size=1.4))
    plate_legs(ch, BM, size=1.35)
    boots(ch, M, mat=M['bone_dark'], shaft=0.06, plate=M['bone_dark'])
    ch.add(A.tabard(ch.J, ch.s, M['cloth'], ch.coll, width=0.2, length=ch.J['knee.R'].z, hem='dagged', back=False))
    maul = W.hammer(dict(M, steel=M['dark_steel']), ch.coll, haft_len=1.4, head=1.9, two_hand=True, block=True)
    mr = ch.hold(maul, 'R', pitch=70, yaw=-10)
    ch.two_hand(mr['grip2_rest'])
    return ch, finish(ch, add_poses(st, P()), profile_of(ident), gait='heavy', heavy=1.3, death='forward', cape=False)


@unit('enemy_shieldwall')
def shield_wall(ident, title):
    M = palette.rotbound()
    ch = Character(ident, title, dict(bulk=1.15, chest_w=1.08), extras=_cape)
    st = add_poses(STANCES['sword_shield'], P(spine=[(-6, LAT)], upper_arm_L=[(-6, LAT)]))
    ch.set_stance(st)
    ch.build_body(dict(skin=M['skin'], torso=M['mail'], sleeve=M['mail'], glove=M['leather_dark'], legs=M['mail']))
    C, R = head_frame(ch)
    add_head(ch, H.skull_head(C, R * .95, M, ch.coll, cracked=0.4))
    add_head(ch, H.great_helm(C + Vector((0, 0, R * .06)), R * 1.04, M, ch.coll, cross=True))
    ch.add(A.breastplate(ch.s, ch.J, M, ch.coll, fauld=2))
    ch.add(A.tabard(ch.J, ch.s, M['cloth'], ch.coll, width=0.17, length=ch.J['knee.R'].z + 0.05, hem='dagged',
                    emblem=grave_emblem(M)))
    ch.add(A.cape(ch.J, ch.s, M['cloth_dark'], ch.coll, length=0.35, tattered=1.5))
    plate_arms(ch, M, lames=3, size=1.05)
    gloves(ch, M, 'steel')
    plate_legs(ch, M)
    boots(ch, M, mat=M['steel'], shaft=0.06, plate=M['steel'])
    ch.hold(W.spear(M, ch.coll, length=1.5, head_len=0.26, grip_at=0.5), 'R', pitch=30, yaw=-4)
    ch.strap(W.tower_shield(M, ch.coll, width=0.62, height=1.2, emblem=grave_emblem(M), spikes=3), 'L', forward=0.22,
             up=-0.08, yaw=-18)
    return ch, finish(ch, st, profile_of(ident), gait='heavy')


@unit('enemy_mirror')
def mirror_knight(ident, title):
    M = palette.rotbound(cloth='3f3a6e', glow='b48cff')
    mirror = S.metal('d9d4f2', name='Mirror silver', rough=0.12, edge=1.2, hammer=0.0, cavity=0.9, tint_var=0.0)
    ch = Character(ident, title, dict(bulk=1.08), extras=_cape)
    st = STANCES['sword_shield']
    ch.set_stance(st)
    SM = dict(M, steel=S.metal('b9bccc', name='Polished silver', rough=0.24), trim=M['gold'])
    ch.build_body(dict(skin=M['skin'], torso=M['mail'], sleeve=M['mail'], glove=M['leather_dark'], legs=M['mail']))
    C, R = head_frame(ch)
    add_head(ch, H.bascinet(C + Vector((0, 0, R * .05)), R, SM, ch.coll))
    add_head(ch, H.crest_plume(C + Vector((-R * .2, 0, R * 1.45)), R, M['cloth'], ch.coll, length=1.5))
    ch.add(A.breastplate(ch.s, ch.J, SM, ch.coll, fauld=3))
    ch.add(A.cape(ch.J, ch.s, M['cloth'], ch.coll, length=0.32, collar=M['gold']))
    plate_arms(ch, SM, lames=3, size=1.05, couters=True)
    gloves(ch, SM, 'steel')
    plate_legs(ch, SM)
    boots(ch, SM, mat=SM['steel'], shaft=0.06, plate=SM['steel'])
    ch.hold(W.sword(dict(M, blade=SM['steel'], hilt=M['gold']), ch.coll, length=0.85, width=0.07, guard_style='winged',
                    pommel='gem'), 'R', pitch=58, yaw=-4)
    sh = W.heater_shield(dict(M, paint=mirror), ch.coll, size=1.25, curve=0.05, emblem=None)
    ch.strap(sh, 'L', forward=0.18, yaw=-22)
    return ch, finish(ch, st, profile_of(ident))


@unit('enemy_revenant_captain')
def revenant_captain(ident, title):
    M = palette.rotbound(cloth='3a5560', glow='8ff6ff')
    ghost = S.emissive('8ff6ff', 6, name='Revenant glow', core='e9ffff', flicker=0.5)
    ch = Character(ident, title, dict(bulk=1.1), extras=_cape)
    st = STANCES['sword_shield']
    ch.set_stance(st)
    ch.build_body(dict(skin=M['skin'], torso=M['mail'], sleeve=M['mail'], glove=M['leather_dark'], legs=M['mail']))
    C, R = head_frame(ch)
    add_head(ch, H.skull_head(C, R * .95, dict(M, glow=ghost), ch.coll))
    add_head(ch, H.crown(C, R, dict(M, gem=ghost), ch.coll, points=6, height=0.4, spikes=True, z=0.55))
    ch.add(A.breastplate(ch.s, ch.J, dict(M, steel=M['dark_steel']), ch.coll, fauld=3))
    ch.add(A.tabard(ch.J, ch.s, M['cloth'], ch.coll, width=0.17, length=ch.J['knee.R'].z, hem='dagged',
                    emblem=grave_emblem(dict(M, glow=ghost))))
    ch.add(A.cape(ch.J, ch.s, M['cloth_dark'], ch.coll, length=0.25, tattered=2.0, collar=M['gold']))
    plate_arms(ch, dict(M, steel=M['dark_steel']), lames=3, spikes=2)
    gloves(ch, M, 'dark_steel')
    plate_legs(ch, dict(M, steel=M['dark_steel']))
    boots(ch, M, mat=M['dark_steel'], shaft=0.06, plate=M['dark_steel'])
    ch.hold(W.sword(M, ch.coll, length=0.92, width=0.075, guard_style='swept', pommel='gem', rune=ghost), 'R', pitch=58)
    std = W.standard(dict(M, glow=ghost), ch.coll, M['cloth'], emblem=grave_emblem(dict(M, glow=ghost)), height=1.9, width=0.4,
                     drop=0.6, finial='spike')
    on_back = Matrix.Translation(ch.J['chest'] + Vector((-0.22, 0.12, -0.45))) @ Matrix.Rotation(math.radians(-8), 4, 'Y')
    for o in std['objs']:
        geo.transform(o, on_back)
        ch.parts.append((o, 'chest'))
    ch.strap(W.round_shield(M, ch.coll, radius=0.28, face_key='dark_steel', emblem=grave_emblem(dict(M, glow=ghost))), 'L',
             forward=0.16, yaw=-22)
    return ch, finish(ch, st, profile_of(ident))


# ------------------------------------------------------------------ casters
def undead_robe(ch, M, robe, length=0.06, sleeve=None):
    ch.build_body(dict(skin=M['skin'], torso=robe, sleeve=sleeve or robe, glove=M['skin'], legs=M['trousers']))
    ch.add(A.shell_torso('Robe', ch.s, ch.J, robe, ch.coll, inflate=0.025, z_from=ch.J['pelvis'].z - 0.1))
    ch.add(A.skirt('Robe skirt', ch.J, robe, ch.coll, ch.J['pelvis'].z + 0.02, length, r_top=(0.2, 0.235),
                   r_bottom=(0.34, 0.38), folds=9, fold_depth=0.024, hem_wave=0.06, segs=40, rows=10))
    for side in ('R', 'L'):
        ch.add(A.wide_sleeve(ch.J, side, sleeve or robe, ch.coll, r1=0.19))


@unit('enemy_spitter')
def blight_caster(ident, title):
    M = palette.rotbound(cloth='4a5530', glow=palette.PLAGUE)
    ch = Character(ident, title, dict(bulk=0.95, thin=0.5, hunch=10), extras=_cape)
    st = STANCES['staff']
    ch.set_stance(st)
    undead_robe(ch, M, M['cloth'])
    C, R = head_frame(ch)
    add_head(ch, H.skull_head(C, R * .95, dict(M, glow=M['plague']), ch.coll))
    add_head(ch, H.hood(C, R, M['cloth_dark'], ch.coll, pointed=0.8, cowl=1.2, droop=0.3))
    ch.add(A.cape(ch.J, ch.s, M['cloth_dark'], ch.coll, length=0.1, tattered=2.0))
    ch.add(A.belt(ch.J, ch.s, dict(M, leather=M['rope']), ch.coll, pouches=2))
    pustules(ch, M, ch.J['chest'] + Vector((0.1, 0, 0.0)), (0.2, 0.28, 0.12), 5, 'chest', seed=3, size=0.035)
    st_res = W.staff(dict(M, glow=M['plague']), ch.coll, length=1.85, head='censer', crystal=M['plague'])
    ch.hold(st_res, 'R', pitch=86)
    ch.add_rigid([geo.sphere('Blight wisp', 0.05, ch.J['hand_tip.L'], M['plague'], ch.coll, 10, 6)], 'hand.L')
    return ch, finish(ch, st, profile_of(ident), gait='shamble')


@unit('enemy_jammer')
def hexer(ident, title):
    M = palette.rotbound(cloth=palette.ROT_PURPLE, glow='c06cff')
    hexlight = S.emissive('c06cff', 12, name='Hex light', core='f4e6ff')
    ch = Character(ident, title, dict(bulk=0.9, thin=0.7, hunch=22, hand=1.2))
    st = add_poses(STANCES['staff'], P(head=[(12, LAT)]))
    ch.set_stance(st)
    undead_robe(ch, M, M['cloth'])
    C, R = head_frame(ch)
    hag = S.flesh('8a8f7c', name='Hag skin')
    add_head(ch, H.human_head(C, R, dict(M, skin=hag, eye=hexlight), ch.coll, gaunt=1.0, nose=1.6, nose_kind='long',
                              eye_glow=hexlight, ears=False))
    add_head(ch, H.hair_cap(C, R, S.fur('6e6a64', name='Hag hair', tip='b0aaa0'), ch.coll, length=1.4, strands=2.5))
    add_head(ch, H.hood(C, R * 1.05, M['cloth_dark'], ch.coll, pointed=1.0, cowl=1.3, droop=0.4))
    ch.add(A.mantle(ch.J, ch.s, S.fur('3b3530', name='Raven feathers', tip='5c5650'), ch.coll))
    # fetish charms on the belt
    ch.add(A.belt(ch.J, ch.s, dict(M, leather=M['rope']), ch.coll, pouches=1))
    for k in range(4):
        p = ch.J['pelvis'] + Vector((0.18, -0.15 + k * 0.1, -0.05))
        ch.parts.append((geo.tube('Fetish cord', [p, p + Vector((0.02, 0, -0.12))], 0.006, M['rope'], ch.coll, sides=4), 'hips'))
        ch.parts.append((geo.quadsphere('Fetish', 0.03, p + Vector((0.02, 0, -0.14)), M['bone'], ch.coll, 2), 'hips'))
    for side in ('R', 'L'):
        claws(ch, M, side, 0.08, 4, M['bone_dark'])
    lantern = W.staff(dict(M, glow=hexlight, lamp_glass=S.glass('d8a6ff', name='Hex glass', glow=3.0)), ch.coll,
                      length=1.7, head='lantern', glow=hexlight)
    ch.hold(lantern, 'R', pitch=86)
    return ch, finish(ch, st, profile_of(ident), gait='shamble', cape=False)


@unit('enemy_lich')
def lich(ident, title):
    M = palette.rotbound(cloth='4a2f6a', glow='7ff3e0')
    ch = Character(ident, title, dict(bulk=0.95, thin=1.0), extras=_cape)
    st = STANCES['float']
    ch.set_stance(st)
    U.skeleton_body(ch, M, glow_core=M['glow'])
    ch.add(A.shell_torso('Lich vestment', ch.s, ch.J, M['cloth'], ch.coll, inflate=0.06, z_from=ch.J['pelvis'].z - 0.05,
                         z_to=ch.J['chest'].z + 0.05))
    ch.add(A.skirt('Lich robe', ch.J, M['cloth'], ch.coll, ch.J['pelvis'].z + 0.05, 0.12, r_top=(0.2, 0.24),
                   r_bottom=(0.3, 0.36), folds=11, fold_depth=0.03, hem_wave=0.12, segs=40, rows=10, back_extra=0.15))
    C, R = head_frame(ch)
    add_head(ch, H.skull_head(C, R, M, ch.coll, cracked=0.8))
    add_head(ch, H.crown(C, R, dict(M, gem=M['glow']), ch.coll, points=7, height=0.7, spikes=True, z=0.6))
    ch.add(A.mantle(ch.J, ch.s, M['cloth_dark'], ch.coll))
    ch.add(A.cape(ch.J, ch.s, M['cloth_dark'], ch.coll, length=0.2, width=1.1, tattered=2.0, collar=M['gold']))
    for side in ('R', 'L'):
        ch.add(A.pauldron(ch.J, side, dict(M, steel=M['gold']), ch.coll, size=0.85, lames=1, spikes=2))
    ch.hold(W.staff(dict(M, glow=M['glow']), ch.coll, length=1.95, head='orb', crystal=M['glow']), 'R', pitch=86)
    ch.add_rigid([geo.sphere('Soul flame', 0.06, ch.J['hand_tip.L'], M['glow'], ch.coll, 12, 8)], 'hand.L')
    return ch, finish(ch, st, profile_of(ident), float_mode=True, death='crumble')


@unit('enemy_howler')
def dread_herald(ident, title):
    M = palette.rotbound()
    ch = Character(ident, title, dict(bulk=1.0, thin=1.0), extras=_cape)
    st = STANCES['staff']
    ch.set_stance(st)
    U.skeleton_body(ch, M, glow_core=M['glow'])
    C, R = head_frame(ch)
    add_head(ch, H.skull_head(C, R, M, ch.coll, jaw_open=0.8))
    add_head(ch, H.crown(C, R, M, ch.coll, points=5, height=0.45, z=0.55))
    ch.add(A.tabard(ch.J, ch.s, M['cloth'], ch.coll, width=0.16, length=ch.J['knee.R'].z - 0.05, hem='dagged',
                    emblem=grave_emblem(M)))
    ch.add(A.cape(ch.J, ch.s, M['cloth_dark'], ch.coll, length=0.25, tattered=2.0, collar=M['gold']))
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=0))
    hn = W.horn(dict(M, hilt=M['gold']), ch.coll, size=1.3)
    m = Matrix.Translation(ch.J['pelvis'] + Vector((0.05, -0.24, 0.0))) @ Matrix.Rotation(math.radians(160), 4, 'X')
    for o in hn['objs']:
        geo.transform(o, m)
        ch.parts.append((o, 'hips'))
    ch.hold(W.standard(M, ch.coll, M['cloth'], emblem=grave_emblem(M), height=2.1, width=0.42, drop=0.65, finial='spike',
                       trim=M['gold']), 'R', pitch=86)
    return ch, finish(ch, st, profile_of(ident), death='crumble')


@unit('enemy_saboteur')
def sapper(ident, title):
    M = palette.rotbound(flesh='86907a')
    fuse = S.emissive('ffb347', 18, name='Fuse spark', core='ffffff')
    ch = Character(ident, title, dict(bulk=0.92, thin=0.6, hunch=24, leg_len=0.92))
    st = add_poses(STANCES['thrower'], P(spine=[(-10, LAT)]))
    ch.set_stance(st)
    flesh_body(ch, M, torso=M['leather_dark'], sleeve=M['skin'], legs=M['trousers'])
    C, R = head_frame(ch)
    rotten_head(ch, M, C, R, gaunt=0.8)
    add_head(ch, H.cap_with_goggles(C, R, M['leather'], M['leather_dark'], S.glass('ff9a3a', name='Goggle glass', glow=1.8),
                                    M['trim'], ch.coll))
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=3))
    keg_c = ch.J['chest'] + Vector((-0.3, 0, -0.08))
    keg = geo.lathe('Powder keg', [(0, -0.22), (0.15, -0.22), (0.18, 0), (0.15, 0.22), (0, 0.22)], 20, M['wood'], ch.coll)
    geo.place(keg, keg_c, rotation=(0, 0, 0))
    ch.parts.append((keg, 'chest'))
    for z in (-0.15, 0.15):
        hoop = geo.lathe('Keg hoop', [(0.168, z - 0.02), (0.172, z + 0.02)], 20, M['dark_steel'], ch.coll, close_top=False,
                         close_bottom=False)
        geo.place(hoop, keg_c)
        ch.parts.append((hoop, 'chest'))
    ch.parts.append((geo.tube('Keg fuse', [keg_c + Vector((0, 0, 0.22)), keg_c + Vector((0.05, 0, 0.32)),
                                           keg_c + Vector((0.12, 0, 0.3))], 0.01, M['rope'], ch.coll, sides=5), 'chest'))
    ch.parts.append((geo.sphere('Keg spark', 0.03, keg_c + Vector((0.12, 0, 0.3)), fuse, ch.coll, 8, 6), 'chest'))
    ch.add(A.vambrace(ch.J, 'R', M, ch.coll, mat_key='leather_dark'))
    boots(ch, M, mat=M['leather_dark'], shaft=0.15)
    bm = W.bomb(dict(M, iron=M['dark_steel']), ch.coll, size=1.0, fuse_glow=fuse)
    held = ch.hold(bm, 'R', pitch=90, offset=(0.02, 0, 0.05))
    return ch, finish(ch, st, profile_of(ident), gait='prowl', release=list(held['objs']), cape=False)


@unit('enemy_tunneler')
def tunneler(ident, title):
    M = palette.rotbound(flesh='7b7a66')
    candle = S.emissive('ffc46b', 12, name='Candle flame', core='ffffff')
    ch = Character(ident, title, dict(bulk=0.95, thin=0.6, hunch=30, arm_len=1.18, hand=1.3, leg_len=0.9))
    st = STANCES['claws']
    ch.set_stance(st)
    flesh_body(ch, M, torso=M['leather_dark'], legs=M['trousers'])
    C, R = head_frame(ch)
    add_head(ch, H.ghoul_head(C, R, dict(skin=M['skin'], glow=M['glow'], bone=M['bone']), ch.coll))
    hat = H.kettle_hat(C + Vector((-R * .1, 0, R * .32)), R * .95, dict(M, steel=M['dark_steel']), ch.coll)
    hat.append(geo.cylinder('Helm candle', R * .1, R * .4, C + Vector((R * .55, 0, R * 1.35)), M['bone'], ch.coll, 10))
    hat.append(geo.sphere('Candle flame', R * .09, C + Vector((R * .55, 0, R * 1.62)), candle, ch.coll, 8, 6, scale=(1, 1, 1.6)))
    add_head(ch, hat)
    U.rags(ch, M['cloth_dark'], ch.coll, z_bottom=ch.J['knee.R'].z + 0.15, tatter=1.3)
    ch.add(A.belt(ch.J, ch.s, M, ch.coll, pouches=2))
    ch.add(A.backpack(ch.J, M, ch.coll, size=0.8))
    claws(ch, M, 'L', 0.18)
    pick = W.hammer(M, ch.coll, haft_len=0.7, head=1.1, spike=True)
    ch.hold(pick, 'R', pitch=55, yaw=-4)
    for side in ('R', 'L'):
        ch.add(A.boot(ch.J, side, M['leather_dark'], ch.coll, shaft=0.1))
    return ch, finish(ch, st, profile_of(ident), gait='prowl', death='forward', cape=False)


@unit('enemy_catacomb_giant')
def catacomb_giant(ident, title):
    M = palette.rotbound()
    ch = Character(ident, title, dict(bulk=1.3, thin=1.0, chest_w=1.25, shoulder=0.32, hunch=14, hand=1.5))
    st = STANCES['brute']
    ch.set_stance(st)
    U.skeleton_body(ch, M, thick=1.6, glow_core=M['glow'], ribs=6)
    C, R = head_frame(ch)
    add_head(ch, H.skull_head(C + Vector((0.04, 0, -0.05)), R * 1.2, M, ch.coll, cracked=1.0,
                              horns=dict(length=1.0, up=0.5, back=0.8, thick=0.2)))
    U.rags(ch, M['cloth_dark'], ch.coll, tatter=2.0)
    ch.add(A.belt(ch.J, ch.s, dict(M, leather=M['rope']), ch.coll, pouches=0))
    for side in ('R', 'L'):
        ch.add(A.pauldron(ch.J, side, dict(M, steel=M['dark_steel']), ch.coll, size=1.2, lames=2, spikes=2))
    # coffin-lid club
    lid = geo.extrude('Coffin lid', [(-0.12, -0.2), (0.12, -0.2), (0.2, 0.5), (0.12, 0.95), (-0.12, 0.95), (-0.2, 0.5)], 0.08,
                      M['wood'], ch.coll, plane='XZ', bevel=0.02)
    cross = geo.box('Lid cross', (0.05, 0.1, 0.5), (0, 0, 0.45), M['dark_steel'], ch.coll, bevel=0.01)
    cross2 = geo.box('Lid cross bar', (0.25, 0.1, 0.05), (0, 0, 0.6), M['dark_steel'], ch.coll, bevel=0.01)
    ch.hold(dict(objs=[lid, cross, cross2], tip=Vector((0, 0, 0.95)), grip2=None, kind='club'), 'R', pitch=62, yaw=-6)
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.3, death='crumble', low_strike=True, cape=False)
