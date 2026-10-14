"""Rotbound Host in the roster-sheet style (output/units.png): stylised skeletons in rust-eaten plate and
ruined crimson heraldry, gaunt ghouls and bloated hulks with plague-green light."""
import math

import numpy as np
from mathutils import Matrix, Vector

from rk import arms, bones, faces, garb, geo, helms, palette, plate, pose as PS
from rk.anim import P
from rk.character import Character
from rk.rig import LAT, YAW, ROLL

from . import unit
from .caravan import BRACE, LUNGE, STRIDE, _cape_bones, add_head, deg, head_frame
from .common import finish, profile_of

CROUCH = P(thigh_L=[(30, LAT), (-18, ROLL)], shin_L=-48, foot_L=16,
           thigh_R=[(-14, LAT), (18, ROLL)], shin_R=-30, foot_R=36,
           hips=[(-12, YAW), (6, LAT)], spine=[(-8, LAT), (-12, YAW)], chest=[(-4, LAT), (-10, YAW)],
           head=[(8, LAT), (28, YAW)], move={'root': (0, 0, -0.16)})
HUNCH = P(thigh_L=[(52, LAT), (-20, ROLL)], shin_L=-84, foot_L=30,
          thigh_R=[(18, LAT), (20, ROLL)], shin_R=-74, foot_R=46,
          hips=[(-10, YAW), (-26, LAT)], spine=[(-20, LAT), (-10, YAW)], chest=[(-14, LAT), (-8, YAW)],
          neck=[(26, LAT)], head=[(34, LAT), (22, YAW)], move={'root': (0, 0, -0.3)})


def _plant(ch, base, feet):
    """(base, feet) with planted feet: the stance drops by feet['drop'] and IK sets the legs."""
    if not (isinstance(feet, dict) and 'drop' in feet):
        return base, feet
    base = dict(base)
    mv = dict(base.get('@move', {}))
    mv['root'] = (0, 0, -feet['drop'])
    base['@move'] = mv
    ch.front_foot = feet.get('front', 'L')
    return base, PS.planted(ch, **feet)


def setup_dead(ident, title, base, spec=None, R=None, L=None, cape=False, thick=1.0, feet=None, **pkw):
    """A skeleton in its stance, dressed: returns (M, ch, stance, kit, smith)."""
    from .caravan import STANCE_FEET
    M = palette.host(**pkw)
    ch = Character(ident, title, dict(dict(bulk=0.95), **(spec or {})), rig_kind='hero',
                   extras=_cape_bones if cape else None)
    base, feet = _plant(ch, base, dict(STANCE_FEET) if feet is None else feet)
    st = PS.stance(ch, base, R=R, L=L, feet=feet)
    ch.feet = feet
    ch.holds = dict(R=R, L=L)
    ch.set_stance(st)
    ch.build_bones(M['bone'], thick=thick)
    ch.dress()
    K = garb.Kit(ch, M)
    return M, ch, st, K, plate.Smith(K)


def setup_flesh(ident, title, base, spec=None, R=None, L=None, cape=False, feet=None, **pkw):
    M = palette.host(**pkw)
    ch = Character(ident, title, spec or {}, rig_kind='hero', extras=_cape_bones if cape else None)
    base, feet = _plant(ch, base, feet)
    st = PS.stance(ch, base, R=R, L=L, feet=feet)
    if feet is not None:
        ch.feet = feet
    ch.holds = dict(R=R, L=L)
    ch.set_stance(st)
    ch.build_body(M['flesh'])
    ch.dress()
    K = garb.Kit(ch, M)
    return M, ch, st, K, plate.Smith(K)


def skull_head(ch, M, size=0.9, **kw):
    C, u = head_frame(ch, size)
    C = C + Vector((-0.01, 0, -0.01))
    return C, u, add_head(ch, bones.skull(C, u, M, ch.coll, **kw))


def rags(K, M, length=0.5, tatter=1.4, mat='cloth', t=0.016, flare=0.06):
    return K.leg_skirt(mat, length=length, t=t, flare=flare, tatter=tatter)


def waist_wrap(K, ch, M, mat='cloth', top=0.3, girth=1.0):
    """Skeletons: a solid cloth wrap gathered round the lumbar spine, so belts and hanging rags have a waist
    to sit on (garments grown from the invisible ghost body would show their hollow inside)."""
    pel = ch.Jp['pelvis']
    sp = ch.Jp['spine']
    up = (sp - pel).normalized()
    g = girth
    f = garb.F.Field('Waist wrap')
    f.add(garb.F.Ellipsoid(pel + up * 0.04, (0.14 * g, 0.175 * g, 0.11)), 0.0)
    f.add(garb.F.RoundCone(pel + up * 0.06 + Vector((-0.01, 0, 0)), pel + up * top + Vector((-0.045, 0, 0)), 0.15 * g,
                           0.045, squash=(1.0, 0.82), up=(1, 0, 0)), 0.06)
    f.disp = lambda P: 0.006 * np.sin(P[:, 2] * 70 + np.arctan2(P[:, 1], P[:, 0] - pel.x) * 3) * \
        (0.5 + 0.5 * garb.F.noise3(P, 8.0, 3, 1))
    o = f.mesh(0.004, ch.coll, M[mat], name='Waist wrap')
    K.put(o, garb.only(['hips', 'spine']))
    return [o]


def ghost_head_field(ch, C, u):
    """Hair, hoods and helmets on a skull are grown from a plain head sculpt of the same size."""
    f, _ = faces.head_field(C, u, jaw=0.9, nose=0.2)
    faces.LAST['field'], faces.LAST['C'], faces.LAST['u'] = f, C, u
    return f


# ------------------------------------------------------------------ Risen
@unit('enemy_walker')
def risen(ident, title):
    """The roster sheet's risen: a wide, low guard behind a round shield, sword ready, in a ruined crimson
    skirt and scarf with one rusted pauldron."""
    from .host2 import setup as setup2
    G, D = Vector((-0.02, -0.5, 1.0)), deg(12, -140)
    shield_c = Vector((0.46, 0.28, 1.06))
    M, ch, st, K, A = setup2('bones', ident, title, BRACE, dict(bulk=1.0),
                             R=PS.Grip(G, D, pole=(-0.1, -0.9, 0.7)),
                             L=PS.Reach(shield_c + Vector((-0.12, 0.0, -0.02)), pole=(0.0, 0.7, 0.9),
                                        fingers=Vector((0.1, -1.0, 0.2)), palm=Vector((-1, 0, 0)), pose='fist'),
                             feet=dict(drop=0.17, spread=0.42, stride=0.5, front='L', knees_out=0.6), thick=1.65)
    # a cloth wrap round the hips under the belt, ragged panels hanging from it
    K.leg_skirt('cloth', length=0.22, t=0.016, flare=0.03, tatter=0.8)
    waist_wrap(K, ch, M, mat='cloth_dark', girth=1.35)
    K.loincloth('cloth', length=0.62, width=0.17, sides=1, tatter=1.7)
    K.belt('leather_dark', buckle='trim', t=0.085, pouches=[(-80, 0.8)])
    A.pauldron('R', size=1.0, lames=2)
    for side in ('R', 'L'):
        A.vambrace(side, start=0.3, end=0.9, t=0.02)
    A.poleyn('L', size=0.85)
    C, u, _ = skull_head(ch, M, size=0.86, jaw_open=0.25)
    sword = arms.sword(M, ch.coll, length=0.74, width=0.11, guard=0.2)
    sw = ch.hold_at(sword, PS.weapon_frame(G, D), 'R', scale=1.15)
    ch.grip(sw, 'R', M['bone'])
    sh = arms.round_shield(M, ch.coll, radius=0.33, face='paint', rim='dark_steel', emblem='rot', emblem_mat='linen')
    ch.strap_at(sh, PS.frame(shield_c, deg(6, -10), (0, 0, 1)), 'L')
    ch.rest_hand('L', M['bone'], pose='fist')
    return ch, finish(ch, st, profile_of(ident), gait='shamble')


# ------------------------------------------------------------------ Rustguard
@unit('enemy_rustguard')
def rustguard(ident, title):
    G = Vector((0.05, -0.42, 1.18))
    D = deg(60, -30)
    shield_c = Vector((0.42, 0.12, 1.0))
    M, ch, st, K, A = setup_dead(ident, title, BRACE, dict(bulk=1.1), R=PS.Grip(G, D, pole=(-0.3, -0.8, 1.1)),
                                 L=PS.Reach(shield_c + Vector((-0.12, 0.0, 0.05)), pole=(0.0, 0.7, 0.9),
                                            fingers=Vector((0.0, -1.0, 0.3)), palm=Vector((-1, 0, 0)), pose='fist'),
                                 thick=1.25)
    rags(K, M, length=0.6, tatter=1.2)
    A.cuirass(t=0.06)
    A.faulds(lames=2, t=0.075)
    K.tabard('cloth', trim=None, emblem=None, width=0.14, length=ch.Jp['knee.R'].z + 0.05, t=0.08, hem='tattered')
    K.belt('leather_dark', buckle='trim', t=0.09)
    K.scarf('cloth', size=1.0, tail=0.25, drape=0.7)
    for side in ('R', 'L'):
        A.pauldron(side, size=1.05, lames=2, spikes=1 if side == 'R' else 0)
        A.vambrace(side)
        A.poleyn(side, size=0.9)
        A.greave(side, t=0.03)
    C, u, _ = skull_head(ch, M, jaw_open=0.1)
    ghost_head_field(ch, C, u)
    add_head(ch, helms.skull_cap(C, u, M, ch.coll, t=0.14, nasal=True))
    ax = arms.axe(M, ch.coll, haft_len=0.8, head=1.15)
    ar = ch.hold_at(ax, PS.weapon_frame(G, D), 'R')
    ch.grip(ar, 'R', M['bone'])
    tw = arms.tower_shield(M, ch.coll, width=0.56, height=1.1, face='paint', rim='dark_steel', emblem='rot',
                           emblem_mat='linen')
    ch.strap_at(tw, PS.frame(shield_c, deg(4, -28), (0, 0, 1)), 'L')
    ch.rest_hand('L', M['bone'], pose='fist')
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.1)


# ------------------------------------------------------------------ Blight Caster
@unit('enemy_spitter')
def blight_caster(ident, title):
    G = Vector((0.14, -0.34, 1.08))
    D = deg(84, 4)
    M, ch, st, K, A = setup_dead(ident, title, STRIDE, R=PS.Grip(G, D, pole=(-0.3, -0.75, 1.0)),
                                 L=PS.Reach(Vector((0.5, 0.2, 1.24)), pole=(0.05, 0.7, 1.0),
                                            fingers=Vector((1, 0.1, 0.4)), palm=Vector((0, 0, 1)), pose='claw'),
                                 cape=True)
    K.skirt('cloth', length=0.9, flare=0.22, t=0.012, tatter=1.6, split=0.5)
    K.gambeson('cloth_dark', t=0.03, skirt=0.3, sleeves='wrist', quilt=False, collar=False)
    K.tabard('cloth', trim='trim', emblem=None, width=0.12, length=ch.Jp['knee.R'].z - 0.1, t=0.05, hem='tattered')
    K.belt('leather_dark', buckle='trim', pouches=[(-80, 0.9)])
    K.strap('leather_dark', ch.Jp['shoulder.R'] + Vector((0.06, 0.04, 0.06)),
            Vector((ch.Jp['pelvis'].x + 0.12, 0.2, ch.Jp['pelvis'].z + 0.1)), width=0.04)
    K.cape('cape', length=0.3, flare=0.2, tatter=1.6)
    C, u, _ = skull_head(ch, M, jaw_open=0.3)
    ghost_head_field(ch, C, u)
    add_head(ch, helms.hood(C, u, M['cloth'], ch.coll, point=0.7, cowl=1.1, opening=1.05))
    from rk import shaders as S_
    M['blightfire'] = S_.emissive('7cf03c', 5.5, name='Blight fire', core='e0ffb0')
    stf = arms.staff(M, ch.coll, length=1.8, grip_at=0.5, head='orb', crystal=M['flame'], gnarl=1.0)
    # green fire licking up from the staff head: part of the staff, so it falls with it
    stf['objs'] += arms.flame(M, ch.coll, Vector((0, 0, 0.95)), size=2.6, color='blightfire', name='Staff fire')
    sres = ch.hold_at(stf, PS.weapon_frame(G, D), 'R')
    ch.grip(sres, 'R', M['bone'])
    ch.rest_hand('L', M['bone'], pose='claw')
    # and cupped in the claw (snuffed out by the death performance)
    fl = arms.flame(M, ch.coll, ch.hand_frames['L'].translation + Vector((0.08, 0, 0.05)), size=2.2, color='blightfire',
                    name='Hand fire')
    conv, _ = ch._conv('hand.L')
    for o in fl:
        geo.transform(o, conv)
        ch.parts.append((o, 'hand.L'))
    return ch, finish(ch, st, profile_of(ident))


# ------------------------------------------------------------------ Ghoul
@unit('enemy_runner')
def ghoul(ident, title):
    """The roster sheet's ghoul: a deep, wide crouch, long clawed arms flung out, the head thrust forward."""
    base = P(hips=[(-10, YAW), (-18, LAT)], spine=[(-22, LAT), (-8, YAW)], chest=[(-12, LAT), (-6, YAW)],
             neck=[(26, LAT)], head=[(30, LAT), (20, YAW)])
    M, ch, st, K, A = setup_flesh(ident, title, base, dict(thin=0.8, arm_len=1.26, hand=1.4, bulk=0.95, leg_len=1.1,
                                                         foot=1.0),
                                  R=PS.Reach(Vector((-0.02, -0.78, 0.66)), pole=(-0.1, -0.9, 1.2),
                                             fingers=Vector((0.2, -0.5, -0.85)), palm=Vector((0.2, 0.6, -0.6)), pose='claw'),
                                  L=PS.Reach(Vector((0.74, 0.46, 0.7)), pole=(0.3, 0.9, 1.1),
                                             fingers=Vector((0.7, 0.2, -0.7)), palm=Vector((-0.3, -1, -0.4)), pose='claw'),
                                  feet=dict(drop=0.3, spread=0.52, stride=0.56, front='L', knees_out=0.85),
                                  flesh='a8a68a')
    ch.claws = 2.2
    K.leg_skirt('cloth', length=0.42, t=0.016, flare=0.04, tatter=2.2, split=0.5)
    K.loincloth('cloth', length=0.5, width=0.14, sides=1, tatter=1.8, t=0.03)
    K.belt('leather_dark', buckle='trim', width=0.04, t=0.035)
    C, u = head_frame(ch, 1.0)
    add_head(ch, faces.monster_head(C, u, M, ch.coll, kind='ghoul'))
    add_head(ch, helms.bandana(C, u, M['cloth'], ch.coll))
    for side in ('R', 'L'):
        ch.rest_hand(side, M['flesh'], pose='claw', size=1.2)
    return ch, finish(ch, st, profile_of(ident), gait='prowl')


# ------------------------------------------------------------------ Rot Hulk
@unit('enemy_bloater')
def rot_hulk(ident, title):
    M, ch, st, K, A = setup_flesh(ident, title, BRACE, dict(bulk=1.55, belly=1.0, chest_w=1.25, arm=1.5, forearm=1.6,
                                                             hand=1.5, leg=1.35, calf=1.3, leg_len=0.86, neck_w=1.4,
                                                             traps=1.6, shoulder=0.33, spread=26),
                                  R=PS.Reach(Vector((0.18, -0.62, 0.86)), pole=(-0.2, -0.9, 1.0),
                                             fingers=Vector((0.3, 0.0, -1)), palm=Vector((0, 1, 0)), pose='fist'),
                                  L=PS.Reach(Vector((0.36, 0.52, 0.84)), pole=(0.1, 0.9, 1.0),
                                             fingers=Vector((0.3, 0.0, -1)), palm=Vector((0, -1, 0)), pose='fist'),
                                  feet=dict(drop=0.08, spread=0.44, stride=0.38, front='L', knees_out=0.6), flesh='949a7c')
    K.leg_skirt('cloth', length=0.45, t=0.018, flare=0.06, tatter=2.0, split=0.4)
    K.loincloth('cloth', length=0.5, width=0.2, sides=1, tatter=1.6)
    K.belt('leather_dark', buckle='dark_steel', width=0.09, t=0.06)
    for side in ('R', 'L'):
        A.vambrace(side, start=0.45, end=0.95, t=0.02)
    J = ch.Jp
    pz, cz = J['pelvis'].z, J['chest'].z
    K.chain([(cz + 0.32, 95), (cz + 0.15, 40), (cz - 0.05, 0), (pz + 0.25, -50), (pz + 0.1, -95)], hug=0.01)
    K.chain([(pz + 0.02, -100), (pz - 0.01, -40), (pz - 0.02, 20), (pz, 80)], hug=0.012, link=0.05)
    import numpy as np
    pf = garb.F.Field('Pustules')
    rng = np.random.default_rng(5)
    spots = [(cz + 0.05, 30, 0.06), (cz - 0.1, -20, 0.05), (pz + 0.25, 10, 0.07), (pz + 0.15, 50, 0.045),
             (cz + 0.25, -60, 0.05), (cz + 0.2, 110, 0.055), (pz + 0.3, -70, 0.04), (cz - 0.2, 70, 0.05),
             (cz + 0.12, -35, 0.045), (pz + 0.4, -15, 0.055), (cz - 0.02, 75, 0.04)]
    for z, a, r in spots:
        p = K.around([(z, a)], 0.0)[0]
        pf.add(garb.F.Sphere(p, r * 1.0), 0.015)
    K.put(pf.mesh(0.004, ch.coll, M['pustule'], name='Pustules', smooth=1))
    sore = garb.F.Field('Sores')
    for z, a, r in spots:
        p = K.around([(z, a)], 0.0)[0]
        sore.add(garb.F.Torus(p, r * 1.05, r * 0.32, plate._frame_z((p - Vector((J['pelvis'].x, 0, p.z))).normalized())),
                 0.01)
    K.put(sore.mesh(0.004, ch.coll, M['flesh'], name='Sores', smooth=2))
    C, u = head_frame(ch, 1.0)
    C = C + Vector((0.1, 0, -0.1))
    add_head(ch, faces.monster_head(C, u, M, ch.coll, kind='brute'))
    for side in ('R', 'L'):
        ch.rest_hand(side, M['flesh'], pose='fist', size=1.35)
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.3)


# ------------------------------------------------------------------ Ashen Regent
@unit('enemy_boss_ashen_regent')
def ashen_regent(ident, title):
    G = Vector((0.22, -0.4, 0.98))
    D = deg(-30, -30)
    M, ch, st, K, A = setup_dead(ident, title, BRACE, dict(bulk=1.15, scale=1.15), cape=True,
                                 R=PS.Grip(G, D, pole=(-0.25, -0.8, 1.0)),
                                 L=PS.Grip(G - D * 0.17, D, pole=(0.2, 0.7, 0.9)), thick=1.3)
    rags(K, M, length=0.7, tatter=1.2)
    A.cuirass(t=0.07)
    A.faulds(lames=3, t=0.085)
    K.tabard('cloth', trim='gold', emblem='rot', emblem_mat='gold', width=0.15, length=ch.Jp['knee.R'].z - 0.05,
             t=0.09, hem='tattered')
    K.belt('leather_dark', buckle='gold', t=0.1)
    for side in ('R', 'L'):
        A.pauldron(side, size=1.2, lames=3, spikes=2)
        A.couter(side)
        A.vambrace(side)
        A.poleyn(side)
        A.greave(side)
    K.cape('cape', length=0.15, flare=0.3, tatter=1.8, collar='cloth_dark')
    C, u, _ = skull_head(ch, M, jaw_open=0.2)
    ghost_head_field(ch, C, u)
    add_head(ch, helms.crown(C, u, M['gold'], ch.coll, points=5, height=0.55, spikes=True, gems=M['glow']))
    sword = arms.sword(M, ch.coll, length=1.2, width=0.16, guard=0.4, grip=0.28, two_hand=True, blade='blade')
    sw = ch.hold_at(sword, PS.weapon_frame(G, D), 'R')
    sw['grip2'] = Vector((0, 0, -0.17))
    ch.grip(sw, 'R', M['dark_steel'])
    ch.grip2(sw, M['dark_steel'])
    return ch, finish(ch, st, profile_of(ident), gait='heavy', heavy=1.2)
