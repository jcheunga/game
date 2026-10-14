"""Rotbound creatures, giants and engines in the roster-sheet style."""
import math

import numpy as np
from mathutils import Matrix, Vector

from rk import arms, beast as B, creatures as CR, engines as EN, faces, garb, geo, helms, machine as MC, palette
from rk import plate, pose as PS, shaders as S, steed
from rk.anim import P
from rk.character import Character
from rk.rig import LAT, YAW, ROLL

from . import unit
from .caravan import BRACE, EASY, GUARD, STRIDE, WIDE, CROUCH_R, add_head, deg, head_frame, two_hand, wield
from .common import finish, finish_clips, profile_of
from .host import ghost_head_field, waist_wrap
from .host2 import HUNCHED, SHOULDERED, glow_at, setup, skull


def V(*a):
    return Vector(a if len(a) > 1 else a[0])



def blob_skeleton(extra_bones=()):
    def extra(sk, J):
        sk.bone('heart', (0, 0, 0.3), (0, 0, 0.45), 'body')
        for name, a, b in extra_bones:
            sk.bone(name, a, b, 'body')
    return MC.skeleton([], extra)


def blob(ident, title, M, builder, profile, jaw=False, float_z=0.0, cam_z=0.4, scale=2.6):
    ch = Character(ident, title, skeleton_fn=blob_skeleton([('jaw', (0.1, 0, 0.22), (0.5, 0, 0.24))] if jaw else []))
    builder(ch, M)
    _, C = MC.clips('nest', ch.J)
    if jaw:
        for f, k in ((3, 0.3), (4, 1.0), (5, 0.8), (6, 0.5)):
            C['attack'][f]['jaw'] = [(-35 * k, LAT)]
    if float_z:
        for clip in ('idle', 'walk'):
            for i, pose in enumerate(C[clip]):
                mv = dict(pose.get('@move', {}))
                mv['root'] = (0, 0, float_z * math.sin(math.tau * i / len(C[clip])))
                pose['@move'] = mv
    if profile in ('staff-cast', 'bow-draw', 'crossbow', 'flask-toss'):
        # shooters fight hand to hand too: a 7-frame lunge cut from the attack (contact on frame 3)
        C['melee'] = [C['attack'][k] for k in (1, 2, 3, 4, 5, 6, 8)]
    ch.tip_rest = V((0.3, 0, cam_z))
    ch.tip_bone = 'body'
    return ch, finish_clips(ch, C, profile, body_z=cam_z, cam_z=cam_z, min_scale=scale,
                            portrait=dict(target=(0.0, 0, cam_z), distance=4.5))


@unit('enemy_splitter')
def bone_nest(ident, title):
    return blob(ident, title, palette.zone_host('city'), CR.bone_nest, 'nest-pulse', cam_z=0.35)


@unit('enemy_bog_spore')
def bog_spore(ident, title):
    M = palette.zone_host('mire')
    M['flesh'] = S.st_flesh('8a8a5a', name='Fungus skin', bruise='5a5a2a')
    return blob(ident, title, M, lambda ch, M_: CR.spore(ch, M_), 'claw-rake', cam_z=0.45)


@unit('enemy_swamp_maw')
def swamp_maw(ident, title):
    M = palette.zone_host('mire')
    M['mud'] = S.st_leather('4a4430', name='Swamp mud', rough=0.7, edge=0.2)
    return blob(ident, title, M, CR.swamp_maw, 'claw-rake', jaw=True, cam_z=0.25)


@unit('enemy_reliquary_eye')
def reliquary_eye(ident, title):
    M = palette.zone_host('basilica')
    M['stone'] = S.stone('8a8478', name='Reliquary stone')
    M['iris'] = S.emissive('ffb040', 3.0, name='Amber iris', core='ffe2a0', base='a06018')
    return blob(ident, title, M, CR.reliquary_eye, 'staff-cast', float_z=0.05, cam_z=1.0, scale=3.0)


@unit('enemy_mirror_wisp')
def mirror_wisp(ident, title):
    M = palette.zone_host('gloamwood')
    M['wisp'] = S.glass('c8a8ff', name='Wisp', glow=1.6)

    def build(ch, M_):
        CR.wisp(ch, M_, V(0, 0, 0), core=True, name='Wisp')
        CR.wisp(ch, M_, V(-0.3, 0.34, 0.26), core=True, name='Mirror wisp', size=0.72)
    return blob(ident, title, M, build, 'staff-cast', float_z=0.07, cam_z=1.0, scale=3.0)


def serpent(ident, title, M, mat, kind, radius, profile, extras=None):
    from rk import character as CH
    CH.set_facing(-12)   # long, low bodies read side-on, and stay above their ground anchor
    ch = Character(ident, title, skeleton_fn=CR.serpent_skeleton(length=1.5 if kind == 'eel' else 1.0,
                                                                 height=radius * 0.95, head=0.24 if kind == 'eel' else 0.16))
    f = CR.serpent_body(ch, ch.J, mat, radius=radius, kind=kind)
    if extras:
        extras(ch, M, f)
    _, C = CR.serpent_clips(ch.J, kind)
    ch.tip_rest = ch.J['head'][1]
    ch.tip_bone = 'head'
    return ch, finish_clips(ch, C, profile, body_z=radius, cam_z=0.35, min_scale=2.6,
                            portrait=dict(target=(0.4, 0, 0.3), distance=4.0))


@unit('enemy_bloated_eel')
def bloated_eel(ident, title):
    M = palette.zone_host('mire')
    skin = S.st_flesh('5a6a5a', name='Eel skin', bruise='3a4a3a', wet=0.7)

    def extras(ch, M_, f):
        hb, ht = ch.J['head']
        g = garb.F.Field('Gills')
        for sy in (-1, 1):
            for k in range(3):
                g.add(garb.F.Ellipsoid(hb + V(-0.02 - k * 0.05, sy * 0.13, 0.0), (0.03, 0.035, 0.07)), 0.01)
        ch.parts.append((g.mesh(0.003, ch.coll, S.st_flesh('c8b8a8', name='Pale gills', bruise='a07a7a'), name='Gills'),
                         'head'))
        e = garb.F.Field('Eel eyes')
        for sy in (-1, 1):
            e.add(garb.F.Sphere(hb.lerp(ht, 0.55) + V(0, sy * 0.1, 0.06), 0.03), 0.0)
        ch.parts.append((e.mesh(0.002, ch.coll, M_['glow'], name='Eel eyes', smooth=1), 'head'))
        # a continuous ragged dorsal fin with bony rays, tallest over the shoulders
        fin = garb.F.Field('Dorsal fin')
        segs = ch.J['segs']
        for i, s_ in enumerate(segs[1:]):
            a, b = ch.J[s_]
            hgt = 0.045 + 0.05 * math.sin(math.pi * (i + 1) / len(segs))
            L = (b - a).length
            fin.add(garb.F.Box((a + b) * 0.5 + V(0, 0, 0.13 + hgt * 0.5), (L * 0.5 + 0.03, 0.005, hgt), r=0.004), 0.03,
                    {s_: 1})
            for k in range(2):
                p = a.lerp(b, 0.25 + 0.5 * k) + V(0, 0, 0.12)
                fin.add(garb.F.RoundCone(p, p + V(-0.03, 0, hgt * 2.0 + 0.03), 0.009, 0.002), 0.01, {s_: 1})
        o = fin.mesh(0.003, ch.coll, S.st_flesh('3a4a40', name='Fin', bruise='2a3a2a'), name='Dorsal fin')
        ch.parts.append((o, {'fieldw': fin}))
        th = garb.F.Field('Eel teeth')
        for sy in (-1, 1):
            for k in range(6):
                p = hb.lerp(ht, 0.42 + 0.1 * k) + V(0.02, sy * 0.1 * (1 - 0.1 * k), -0.035)
                th.add(garb.F.RoundCone(p, p + V(0.01, 0, -0.05 + 0.004 * k), 0.012, 0.002), 0.0)
        ch.parts.append((th.mesh(0.0018, ch.coll, M_['bone'], name='Eel teeth'), 'head'))
    return serpent(ident, title, M, skin, 'eel', 0.14, 'claw-rake', extras)


@unit('enemy_plague_leech')
def plague_leech(ident, title):
    M = palette.zone_host('quarantine')
    skin = S.st_flesh('8a7a3a', name='Leech skin', bruise='5a3a1a', wet=0.8)

    def extras(ch, M_, f):
        sp = garb.F.Field('Leech spots')
        rng = np.random.default_rng(2)
        for s_ in ch.J['segs']:
            a, b = ch.J[s_]
            for k in range(2):
                c = a.lerp(b, rng.uniform(0.2, 0.8)) + V(0, rng.uniform(-0.1, 0.1), 0.15)
                sp.add(garb.F.Sphere(c, 0.035), 0.01, {s_: 1})
        o = sp.mesh(0.003, ch.coll, M_['pustule'], name='Leech spots', smooth=1)
        ch.parts.append((o, {'fieldw': sp}))
        t = garb.F.Field('Sucker teeth')
        hb, ht = ch.J['head']
        for k in range(10):
            a_ = k / 10 * math.tau
            p = ht + V(0.0, math.cos(a_) * 0.09, math.sin(a_) * 0.08)
            t.add(garb.F.RoundCone(p, p + V(-0.03, -math.cos(a_) * 0.03, -math.sin(a_) * 0.03), 0.012, 0.002), 0.0)
        ch.parts.append((t.mesh(0.002, ch.coll, M_['bone'], name='Sucker teeth'), 'head'))
    return serpent(ident, title, M, skin, 'leech', 0.16, 'claw-rake', extras)


def quad(ident, title, M, coat, kind='dog', bulk=1.0, extras=None, eye='glow', profile='pounce'):
    ch = Character(ident, title, skeleton_fn=B.skeleton('hound'))
    steed.build_hound(ch, M, coat, kind=kind, bulk=bulk, eye=eye)
    if extras:
        extras(ch, M)
    hst, HC = B.clips('hound')
    return ch, finish_clips(ch, HC, profile, body_z=0.65, cam_z=0.6, min_scale=2.6, stance=hst,
                            portrait=dict(target=(0.3, 0, 0.8), distance=4.0))


@unit('enemy_grave_rat')
def grave_rat(ident, title):
    M = palette.zone_host('city')
    coat = S.st_hair('4a4038', name='Mangy fur', tip='7a6a58', stripes=90.0)

    def extras(ch, M_):
        sp = garb.F.Field('Rat spine')
        for k in range(9):
            p = V(-0.4 + k * 0.1, 0, 0.83 - 0.02 * abs(k - 4))
            sp.add(garb.F.RoundCone(p, p + V(-0.03, 0, 0.06), 0.022, 0.006), 0.0, {'spine': 1})
        o = sp.mesh(0.003, ch.coll, M_['bone'], name='Rat spine')
        ch.parts.append((o, {'fieldw': ch.body_field, 'restrict': ['hips', 'spine', 'chest']}))
    return quad(ident, title, M, coat, 'rat', 1.0, extras)


@unit('enemy_ash_hound')
def ash_hound(ident, title):
    M = palette.zone_host('foundry')
    coat = S.ember_metal('1e1a18', glow='ff6a1a', name='Coal hide', strength=5.0, crack=0.02, coverage=0.5)

    def extras(ch, M_):
        j = garb.F.Field('Ember jaws')
        j.add(garb.F.Ellipsoid(V(0.8, 0, 0.885), (0.07, 0.035, 0.012)), 0.0)
        ch.parts.append((j.mesh(0.002, ch.coll, M_['glow'], name='Ember jaws', smooth=1), 'head'))
    return quad(ident, title, M, coat, 'dog', 1.08, extras)


# ------------------------------------------------------------------ giants and brutes
@unit('enemy_catacomb_giant')
def catacomb_giant(ident, title):
    G, D = Vector((0.06, -0.52, 1.26)), deg(85, -28)
    M, ch, st, K, A = setup('bones', ident, title, BRACE, dict(bulk=1.3, scale=1.12), R=PS.Grip(G, D, pole=(-0.3, -0.9, 1.1)),
                            L=PS.Reach(Vector((0.42, 0.36, 0.96)), pole=(0.0, 0.8, 1.0), fingers=Vector((0.6, 0, -0.7)),
                                       palm=Vector((0, -1, 0)), pose='fist'), feet=WIDE, thick=2.2)
    K.loincloth('cloth', length=0.55, width=0.22, sides=1, tatter=1.6, t=0.06)
    waist_wrap(K, ch, M, mat='cloth_dark', girth=1.6)
    K.belt('leather_dark', buckle='dark_steel', width=0.09, t=0.07)
    K.chain([(ch.Jp['chest'].z + 0.3, 95), (ch.Jp['chest'].z, 15), (ch.Jp['pelvis'].z + 0.2, -60)], hug=0.03)
    A.pauldron('L', size=1.3, lames=2, spikes=2)
    C, u = skull(ch, M, jaw_open=0.5, size=1.0)
    club = arms.club(M, ch.coll, length=1.1, head=1.6)
    wield(ch, club, M, G, D, glove='bone')
    ch.rest_hand('L', M['bone'], pose='fist', size=1.2)
    return ch, finish(ch, st, 'heavy-smash', gait='heavy', heavy=1.4)


@unit('enemy_thorn_brute')
def thorn_brute(ident, title):
    M = palette.zone_host('thornwall')
    M['flesh'] = S.st_wood('5a4630', name='Bark hide', grain=4.0)
    M2, ch, st, K, A = setup('flesh', ident, title, BRACE, dict(bulk=1.35, arm=1.35, forearm=1.45, hand=1.3, chest_w=1.15),
                             M=M, R=PS.Reach(Vector((0.36, -0.52, 1.0)), pole=(-0.1, -0.9, 1.0),
                                             fingers=Vector((0.6, 0, -0.6)), palm=Vector((0, 1, 0)), pose='claw'),
                             L=PS.Reach(Vector((0.46, 0.4, 1.02)), pole=(0.0, 0.9, 1.0), fingers=Vector((0.6, 0, -0.6)),
                                        palm=Vector((0, -1, 0)), pose='claw'), feet=WIDE)
    ch.claws = 1.4
    K.loincloth('cloth', length=0.5, width=0.2, sides=1, tatter=1.4)
    K.belt('rope', buckle=None, width=0.04, t=0.05)
    th = garb.F.Field('Thorns')
    rng = np.random.default_rng(6)
    J = ch.Jp
    for side in ('R', 'L'):
        a, b = J['elbow.' + side], J['wrist.' + side]
        sh = J['shoulder.' + side]
        for k in range(6):
            p = (sh.lerp(a, rng.uniform(0.2, 1.0)) if k < 2 else a.lerp(b, rng.uniform(0.1, 0.9)))
            d = Vector((rng.uniform(-0.5, 0.2), (1 if side == 'L' else -1) * rng.uniform(0.6, 1.0), rng.uniform(0.0, 0.8))).normalized()
            th.add(garb.F.RoundCone(p + d * 0.05, p + d * 0.17, 0.025, 0.002), 0.005)
    for k in range(7):
        p = K.around([(J['chest'].z + rng.uniform(-0.1, 0.3), rng.uniform(100, 260))], 0.0)[0]
        th.add(garb.F.RoundCone(p, p + Vector((-0.12, rng.uniform(-0.05, 0.05), 0.08)), 0.03, 0.002), 0.005)
    K.put(th.mesh(0.003, ch.coll, M['bone'], name='Thorns'))
    C, u = head_frame(ch, 0.95)
    add_head(ch, faces.monster_head(C + Vector((0.06, 0, -0.06)), u, M, ch.coll, kind='brute'))
    for side in ('R', 'L'):
        ch.rest_hand(side, M['flesh'], pose='claw', size=1.2)
    return ch, finish(ch, st, 'claw-rake', gait='heavy', heavy=1.3)


@unit('enemy_avalanche_bearer')
def avalanche_bearer(ident, title):
    M = palette.zone_host('thornwall')
    M['flesh'] = S.st_flesh('8a8a7e', name='Ogre hide', bruise='5a5a52')
    M['rock'] = S.stone('7a7468', name='Boulder', scale=1.5)
    M2, ch, st, K, A = setup('flesh', ident, title, BRACE, dict(bulk=1.45, belly=0.8, arm=1.4, forearm=1.45, hand=1.35,
                                                                scale=0.9), M=M,
                             R=PS.Reach(Vector((0.06, -0.24, 1.98)), pole=(-0.3, -0.9, 1.45), fingers=Vector((0, 0.3, 1)),
                                        palm=Vector((0, 0.6, 0.8)), pose='open'),
                             L=PS.Reach(Vector((0.06, 0.24, 1.98)), pole=(-0.3, 0.9, 1.45), fingers=Vector((0, -0.3, 1)),
                                        palm=Vector((0, -0.6, 0.8)), pose='open'), feet=WIDE)
    K.loincloth('wool', length=0.5, width=0.24, sides=1, tatter=1.2)
    K.cape('wool', length=ch.Jp['chest'].z, width=0.9, flare=0.1, tatter=1.0, collar='wool')
    C, u = head_frame(ch, 1.1)
    add_head(ch, faces.monster_head(C + Vector((0.07, 0, 0.0)), u, M, ch.coll, kind='brute'))
    rk = garb.F.Field('Boulder')
    rc = Vector((0.08, 0, 2.27))
    rk.add(garb.F.Box(rc, (0.32, 0.38, 0.28), Matrix.Rotation(0.4, 3, 'Z') @ Matrix.Rotation(0.3, 3, 'X'), r=0.06), 0.0)
    rk.add(garb.F.Box(rc + Vector((0.05, 0.1, 0.12)), (0.2, 0.2, 0.18), Matrix.Rotation(-0.5, 3, 'Y'), r=0.05), 0.06)
    rk.disp = lambda P_: garb.F.noise3(P_, 6.0, 4, 2) * 0.03
    conv, _ = ch._conv('hand.R')
    o = rk.mesh(0.008, ch.coll, M['rock'], name='Boulder')
    geo.transform(o, conv)
    ch.parts.append((o, 'hand.R'))
    for side in ('R', 'L'):
        ch.rest_hand(side, M['flesh'], pose='open', size=1.3)
    return ch, finish(ch, st, 'flask-toss', gait='heavy', heavy=1.3, release=[o])


@unit('enemy_drowned_colossus')
def drowned_colossus(ident, title):
    M = palette.zone_host('harbor')
    M['weed'] = S.st_cloth('3a5a3a', name='Weed', shadow='1e3a1e', sheen=0.4)
    M['water'] = S.glass('5fa8b8', name='Barrel water', glow=0.6)
    M2, ch, st, K, A = setup('flesh', ident, title, BRACE, dict(bulk=1.5, chest_w=1.25, arm=1.45, forearm=1.5, hand=1.4,
                                                                 belly=0.6, scale=1.1), M=M,
                             R=PS.Reach(Vector((0.3, -0.62, 0.96)), pole=(-0.1, -0.9, 1.0), fingers=Vector((0.4, 0, -0.9)),
                                        palm=Vector((0, 1, 0)), pose='fist'),
                             L=PS.Reach(Vector((0.4, 0.56, 0.96)), pole=(0.0, 0.9, 1.0), fingers=Vector((0.4, 0, -0.9)),
                                        palm=Vector((0, -1, 0)), pose='fist'), feet=WIDE)
    K.leg_skirt('weed', length=0.85, t=0.03, flare=0.1, tatter=2.0)
    K.belt('rope', buckle=None, width=0.05, t=0.08)
    J = ch.Jp
    bc = K._surface_point(J['chest'] * 0.6 + J['pelvis'] * 0.4, Vector((1, 0, 0)), K.base(['torso', 'hips']), -0.05)
    br = garb.F.Field('Chest barrel')
    br.add(garb.F.RoundCone(bc - Vector((0, 0, 0.24)), bc + Vector((0, 0, 0.24)), 0.24, 0.24), 0.0)
    br.cut(garb.F.RoundCone(bc - Vector((0, 0, 0.2)) + Vector((0.05, 0, 0)), bc + Vector((0, 0, 0.2)) + Vector((0.05, 0, 0)),
                            0.2, 0.2), 0.004)
    K.rigid(br.mesh(0.005, ch.coll, M['wood'], name='Chest barrel'), 'chest')
    wt = garb.F.Field('Barrel water')
    wt.add(garb.F.RoundCone(bc - Vector((0, 0, 0.2)), bc + Vector((0, 0, 0.12)), 0.2, 0.2), 0.0)
    K.rigid(wt.mesh(0.005, ch.coll, M['water'], name='Barrel water', smooth=1), 'chest')
    hp = garb.F.Field('Barrel hoops')
    for z in (-0.18, 0.0, 0.18):
        hp.add(garb.F.Torus(bc + Vector((0, 0, z)), 0.245, 0.014), 0.0)
    K.rigid(hp.mesh(0.003, ch.coll, M['dark_steel'], name='Barrel hoops'), 'chest')
    C, u = head_frame(ch, 0.85)
    add_head(ch, faces.monster_head(C + Vector((0.1, 0, -0.12)), u, M, ch.coll, kind='brute'))
    for side in ('R', 'L'):
        ch.rest_hand(side, M['flesh'], pose='fist', size=1.3)
    return ch, finish(ch, st, 'heavy-smash', gait='heavy', heavy=1.4)


@unit('enemy_wicker_giant')
def wicker_giant(ident, title):
    M = palette.zone_host('gloamwood')
    M['wicker'] = S.st_wood('8a6a3e', name='Wicker', grain=12.0)
    M['ember'] = S.emissive('ff8a2a', 9, name='Ember heart', core='fff0c0')
    M2, ch, st, K, A = setup('bones', ident, title, BRACE, dict(bulk=1.3, scale=1.12), M=M,
                             R=PS.Reach(Vector((0.36, -0.56, 1.0)), pole=(-0.1, -0.9, 1.0), fingers=Vector((0.6, 0, -0.6)),
                                        palm=Vector((0, 1, 0)), pose='claw'),
                             L=PS.Reach(Vector((0.46, 0.48, 1.02)), pole=(0.0, 0.9, 1.0), fingers=Vector((0.6, 0, -0.6)),
                                        palm=Vector((0, -1, 0)), pose='claw'), feet=WIDE, thick=1.0)
    ch.body.data.materials.clear()
    ch.body.data.materials.append(M['wicker'])
    # an open lattice of woven withies over a ghost of the body
    base = K.base(['torso', 'hips', 'arm', 'leg', 'neck'])
    lo, hi = base.bounds(0.2)
    lat = garb.F.Field('Wicker lattice')

    def weave(P_):
        d = base.eval(P_) - 0.05
        a = np.abs(np.sin(P_[:, 2] * 22 + P_[:, 1] * 22)) ** 0.5
        b = np.abs(np.sin(P_[:, 2] * 22 - P_[:, 1] * 22 + P_[:, 0] * 10)) ** 0.5
        strands = np.minimum(a, b) - 0.22
        return np.maximum(np.abs(d) - 0.015, strands * 0.05)
    lat.add(garb.F.Fn(weave, lo, hi), 0.0)
    K.put(lat.mesh(0.005, ch.coll, M['wicker'], name='Wicker lattice', smooth=1))
    glow_at(K, ch, M, ch.Jp['chest'] + Vector((0.0, 0, 0.0)), 0.13, 'ember', 'chest', 'Ember heart')
    C, u = head_frame(ch, 1.0)
    hd = garb.F.Field('Wicker head')
    hd.add(garb.F.Fn(lambda P_: np.abs(np.linalg.norm((P_ - np.array(tuple(C))) / [1.0, 0.9, 1.1], axis=1) - 0.9 * u)
                     - 0.04 * u, np.array(tuple(C)) - 1.2 * u, np.array(tuple(C)) + 1.2 * u), 0.0)
    hd.keep(garb.F.Fn(lambda P_: np.minimum(np.abs(np.sin(P_[:, 2] * 40)) ** 0.5, np.abs(np.sin(P_[:, 1] * 40)) ** 0.5)
                      * 0.05 - 0.012, np.array(tuple(C)) - 1.2 * u, np.array(tuple(C)) + 1.2 * u), 0.0)
    add_head(ch, [hd.mesh(0.004, ch.coll, M['wicker'], name='Wicker head', smooth=1)])
    eyes = garb.F.Field('Wicker eyes')
    for sy in (-1, 1):
        eyes.add(garb.F.Sphere(C + Vector((0.7 * u, sy * 0.3 * u, 0.05 * u)), 0.12 * u), 0.0)
    add_head(ch, [eyes.mesh(0.003, ch.coll, M['ember'], name='Wicker eyes', smooth=1)])
    for side in ('R', 'L'):
        ch.rest_hand(side, M['wicker'], pose='claw', size=1.3)
    return ch, finish(ch, st, 'heavy-smash', gait='heavy', heavy=1.3)


@unit('enemy_gate_breaker')
def gate_breaker(ident, title):
    G, D = SHOULDERED
    R, L = two_hand(G, D, -0.32)
    M, ch, st, K, A = setup('flesh', ident, title, BRACE, dict(bulk=1.45, chest_w=1.2, arm=1.4, forearm=1.45, hand=1.3,
                                                                scale=1.1), zone='citadel', R=R, L=L, feet=WIDE)
    K.trousers('trousers_dark', bottom=0.45, baggy=0.5)
    K.boots('leather_dark', height=0.45)
    K.loincloth('cloth', length=0.55, width=0.22, sides=1, tatter=1.2)
    K.belt('leather_dark', buckle='dark_steel', width=0.1, t=0.07)
    for side in ('R', 'L'):
        A.pauldron(side, size=1.35, lames=2, spikes=2)
        A.vambrace(side, t=0.035)
    C, u = head_frame(ch, 1.0)
    add_head(ch, faces.monster_head(C + Vector((0.06, 0, -0.06)), u, M, ch.coll, kind='brute'))
    sk = garb.F.Field('Ram skull')
    sk.add(garb.F.Ellipsoid(C + Vector((0.35 * u, 0, 0.55 * u)), (0.9 * u, 0.85 * u, 0.6 * u)), 0.0)
    for sy in (-1, 1):
        pts = [C + Vector((0.0, sy * 0.6 * u, 0.7 * u)), C + Vector((-0.5 * u, sy * 1.3 * u, 0.6 * u)),
               C + Vector((-0.3 * u, sy * 1.55 * u, -0.2 * u)), C + Vector((0.3 * u, sy * 1.35 * u, -0.5 * u))]
        sk.add(garb.F.Chain(pts, [0.28 * u, 0.24 * u, 0.18 * u, 0.08 * u]), 0.06 * u)
    sk.cut(garb.F.Ellipsoid(C + Vector((0.2 * u, 0, -0.2 * u)), (0.9 * u, 0.75 * u, 0.75 * u)), 0.05 * u)
    sk.disp = lambda P_: 0.004 * np.sin(np.linalg.norm(P_ - np.array(tuple(C)), axis=1) * 300)
    add_head(ch, [sk.mesh(0.004, ch.coll, M['bone'], name='Ram skull')])
    wield(ch, arms.hammer(M, ch.coll, haft_len=1.2, head=1.8, spike=False), M, G, D, glove='flesh', g2=-0.32)
    return ch, finish(ch, st, 'heavy-smash', gait='heavy', heavy=1.4)


@unit('enemy_cliff_stalker')
def cliff_stalker(ident, title):
    M = palette.zone_host('thornwall')
    M['flesh'] = S.st_hair('6a5a46', name='Goat hide', tip='9a8a72', stripes=80.0)
    M2, ch, st, K, A = setup('flesh', ident, title, HUNCHED, dict(thin=0.4, arm_len=1.15, hand=1.25, digitigrade=True,
                                                                  leg_len=1.12), M=M,
                             R=PS.Reach(Vector((0.5, -0.4, 0.84)), pole=(0.0, -0.8, 1.1), fingers=Vector((0.7, 0, -0.6)),
                                        palm=Vector((0, 1, -0.3)), pose='claw'),
                             L=PS.Reach(Vector((0.62, 0.32, 0.9)), pole=(0.1, 0.8, 1.1), fingers=Vector((0.7, 0, -0.6)),
                                        palm=Vector((0, -1, -0.3)), pose='claw'))
    ch.claws = 2.0
    K.loincloth('cloth', length=0.35, width=0.14, sides=1, tatter=1.6, t=0.03)
    C, u = head_frame(ch, 1.0)
    add_head(ch, faces.monster_head(C, u, M, ch.coll, kind='ghoul'))
    hn = garb.F.Field('Horns')
    for sy in (-1, 1):
        pts = [C + Vector((0.2 * u, sy * 0.45 * u, 0.8 * u)), C + Vector((-0.4 * u, sy * 0.8 * u, 1.3 * u)),
               C + Vector((-1.0 * u, sy * 0.95 * u, 1.0 * u)), C + Vector((-1.1 * u, sy * 0.9 * u, 0.4 * u))]
        hn.add(garb.F.Chain(pts, [0.2 * u, 0.16 * u, 0.1 * u, 0.02 * u]), 0.05 * u)
    hn.disp = lambda P_: 0.003 * np.sin(P_[:, 2] * 260)
    add_head(ch, [hn.mesh(0.003, ch.coll, M['bone'], name='Horns')])
    for side in ('R', 'L'):
        ch.rest_hand(side, M['bone'], pose='claw', size=1.15)
    return ch, finish(ch, st, 'claw-rake', gait='prowl')


@unit('enemy_shade')
def shade(ident, title):
    M = palette.zone_host('gloamwood')
    M['shadow'] = S.st_cloth('121016', name='Shadow', shadow='050408', sheen=0.2, edge=0.05)
    M['bright'] = S.emissive('e8d8ff', 8, name='Bright hands', core='ffffff')
    M2, ch, st, K, A = setup('flesh', ident, title, HUNCHED, dict(thin=0.8, arm_len=1.2, hand=1.15), M=M,
                             R=PS.Reach(Vector((0.5, -0.38, 1.1)), pole=(0.0, -0.8, 1.1), fingers=Vector((0.8, 0, -0.3)),
                                        palm=Vector((0, 1, -0.3)), pose='claw'),
                             L=PS.Reach(Vector((0.58, 0.32, 1.2)), pole=(0.1, 0.8, 1.1), fingers=Vector((0.8, 0, -0.2)),
                                        palm=Vector((0, -1, -0.3)), pose='claw'), cape=True)
    ch.body.data.materials.clear()
    ch.body.data.materials.append(M['shadow'])
    ch.visible['mat'] = M['shadow']
    K.skirt('shadow', length=1.1, flare=0.35, t=0.012, tatter=2.2, top=ch.Jp['chest'].z)
    K.cape('shadow', length=0.15, flare=0.4, tatter=2.2)
    C, u = head_frame(ch, 1.0)
    ghost_head_field(ch, C, u)   # the hood is grown from this unit's own (hidden) head
    add_head(ch, helms.hood(C, u, M['shadow'], ch.coll, point=1.2, cowl=1.2, opening=1.0))
    ey = garb.F.Field('Shade eyes')
    for sy in (-1, 1):
        ey.add(garb.F.Ellipsoid(C + Vector((0.6 * u, sy * 0.3 * u, 0.0)), (0.06 * u, 0.12 * u, 0.05 * u)), 0.0)
    add_head(ch, [ey.mesh(0.002, ch.coll, M['bright'], name='Shade eyes', smooth=1)])
    for side in ('R', 'L'):
        ch.rest_hand(side, M['bright'], pose='claw', size=1.1)
    return ch, finish(ch, st, 'claw-rake', float_mode=True)


@unit('enemy_briar_witch')
def briar_witch(ident, title):
    from .caravan import caster
    M = palette.zone_host('thornwall')
    M['skin'] = S.st_skin('a89a7a', blush='7a6a4a')
    M['crystal'] = M['glow']
    M2, ch, st, K, A = setup('human', ident, title, STRIDE, dict(bulk=0.9, female=1.0), M=M,
                             R=PS.Grip(Vector((0.14, -0.36, 1.1)), deg(86, 2), pole=(-0.3, -0.75, 1.0)),
                             L=PS.Reach(Vector((0.48, 0.2, 1.3)), pole=(0.05, 0.7, 1.0), fingers=Vector((1, 0.1, 0.4)),
                                        palm=Vector((0, 0, 1)), pose='claw'), feet=EASY, cape=True)
    K.skirt('cloth', length=0.95, flare=0.25, t=0.012, tatter=1.6, split=0.4)
    K.gambeson('cloth_dark', t=0.03, skirt=0.3, sleeves='wrist', quilt=False, collar=False)
    K.belt('rope', buckle=None, width=0.03, t=0.05)
    K.cape('cloth', length=ch.Jp['pelvis'].z - 0.5, flare=0.25, tatter=1.6)
    from .caravan import dress_head
    C, u = dress_head(ch, M, face=dict(female=1.0, frown=1.0, age=0.6, nose=1.3, hook=0.7),
                      hair=dict(style='long', length=0.9))
    cr = garb.F.Field('Branch crown')
    for k in range(7):
        a = (k / 6 - 0.5) * math.radians(200)
        base = C + Vector((math.cos(a) * 0.85 * u - 0.1 * u, math.sin(a) * 0.85 * u, 0.6 * u))
        tip = base + Vector((math.cos(a) * 0.2 * u, math.sin(a) * 0.2 * u, (0.5 + 0.3 * (k % 2)) * u))
        cr.add(garb.F.Chain([base, base.lerp(tip, 0.5) + Vector((0, 0, 0.05 * u)), tip], [0.07 * u, 0.05 * u, 0.015 * u]),
               0.03 * u)
    cr.add(garb.F.Torus(C + Vector((-0.1 * u, 0, 0.6 * u)), 0.86 * u, 0.06 * u), 0.03 * u)
    add_head(ch, [cr.mesh(0.003, ch.coll, M['wood'], name='Branch crown')])
    stf = arms.staff(M, ch.coll, length=1.9, grip_at=0.5, head='crook', gnarl=1.4)
    sres = ch.hold_at(stf, PS.weapon_frame(Vector((0.14, -0.36, 1.1)), deg(86, 2)), 'R')
    ch.grip(sres, 'R', M['skin'])
    ch.rest_hand('L', M['skin'], pose='claw')
    fl = arms.flame(M, ch.coll, ch.hand_frames['L'].translation + Vector((0.08, 0, 0.05)), size=0.9, color='flame')
    conv, _ = ch._conv('hand.L')
    for o in fl:
        geo.transform(o, conv)
        ch.parts.append((o, 'hand.L'))
    return ch, finish(ch, st, 'staff-cast')


def moth_wings(ch, K, M, mat, span=0.9):
    """A pair of large hanging moth wings from the shoulder blades (rigid on the chest)."""
    J = ch.Jp
    back = K._surface_point(J['chest'] + Vector((0, 0, 0.1)), Vector((-1, 0, 0)), K.base(['torso']), 0.04)
    out = []
    for sy in (-1, 1):
        f = garb.F.Field('Moth wing')

        def wing(P_, sy=sy):
            q = P_ - np.array(tuple(back))
            u_ = -q[:, 0] * 0.4 + sy * q[:, 1]
            v_ = q[:, 2]
            ell = np.hypot((u_ - span * 0.5) / (span * 0.55), (v_ - 0.1) / 0.55) - 1
            ell2 = np.hypot((u_ - span * 0.35) / (span * 0.4), (v_ + 0.45) / 0.35) - 1
            shape = np.minimum(ell, ell2) * 0.3
            plane = np.abs(q[:, 0] + 0.08 + 0.15 * np.clip(sy * q[:, 1], 0, 1)) - 0.006
            return np.maximum(np.maximum(shape, plane), -sy * q[:, 1] + 0.02)
        lo = np.array(tuple(back)) - np.array([0.5, span + 0.3, 1.0])
        hi = np.array(tuple(back)) + np.array([0.3, span + 0.3, 0.9])
        f.add(garb.F.Fn(wing, lo, hi), 0.0)
        o = f.mesh(0.005, ch.coll, mat, name='Moth wing')
        K.rigid(o, 'chest')
        out.append(o)
    return out


@unit('enemy_dream_weaver')
def dream_weaver(ident, title):
    M = palette.zone_host('gloamwood')
    M['wing'] = S.st_cloth('b8a8c8', name='Moth wing', shadow='6a5a7a', sheen=0.6)
    M['spindle'] = S.emissive('d8b8ff', 8, name='Dream spindle', core='ffffff')
    M2, ch, st, K, A = setup('flesh', ident, title, STRIDE, dict(thin=0.4, bulk=0.9), M=M,
                             R=PS.Reach(Vector((0.42, -0.3, 1.32)), pole=(-0.1, -0.8, 1.0), fingers=Vector((1, 0, 0.3)),
                                        palm=Vector((0, 0, 1)), pose='cup'),
                             L=PS.Reach(Vector((0.4, 0.28, 1.3)), pole=(0.0, 0.8, 1.0), fingers=Vector((1, 0, 0.3)),
                                        palm=Vector((0, 0, 1)), pose='cup'), feet=EASY)
    K.skirt('cloth', length=1.0, flare=0.25, t=0.012, tatter=1.6)
    K.gambeson('cloth_dark', t=0.028, skirt=0.3, sleeves='wrist', quilt=False, collar=False)
    moth_wings(ch, K, M, M['wing'], span=0.95)
    C, u = head_frame(ch, 1.0)
    add_head(ch, faces.monster_head(C, u, M, ch.coll, kind='ghoul'))
    add_head(ch, helms.hood(C, u, M['cloth'], ch.coll, point=1.0, cowl=1.0))
    sp = garb.F.Field('Spindle')
    c = (ch.hand_frames['R'].translation + ch.hand_frames['L'].translation) * 0.5 + Vector((0.12, 0, 0.12))
    sp.add(garb.F.RoundCone(c - Vector((0, 0, 0.14)), c + Vector((0, 0, 0.14)), 0.02, 0.004), 0.0)
    sp.add(garb.F.Ellipsoid(c, (0.05, 0.05, 0.07)), 0.02)
    conv, _ = ch._conv('hand.R')
    o = sp.mesh(0.002, ch.coll, M['spindle'], name='Spindle', smooth=1)
    geo.transform(o, conv)
    ch.parts.append((o, 'hand.R'))
    for side in ('R', 'L'):
        ch.rest_hand(side, M['flesh'], pose='cup')
    return ch, finish(ch, st, 'staff-cast', float_mode=True)


# ------------------------------------------------------------------ the siege tower
@unit('enemy_siegetower')
def siege_tower(ident, title):
    from rk import character as CH
    CH.set_facing(-28)
    M = palette.zone_host('city')

    def extra(sk, J):
        sk.bone('ramp', (0.62, 0, 2.15), (0.62, 0, 2.85), 'body')
    ch = Character(ident, title, skeleton_fn=MC.skeleton([('wheel.FR', (0.5, -0.55, 0.32)), ('wheel.FL', (0.5, 0.55, 0.32)),
                                                          ('wheel.BR', (-0.5, -0.55, 0.32)), ('wheel.BL', (-0.5, 0.55, 0.32))],
                                                         extra))
    S_ = EN.Shop(ch, M)
    posts = []
    for x in (-0.55, 0.55):
        for y in (-0.5, 0.5):
            posts.append((Vector((x, y, 0.4)), Vector((x * 0.85, y * 0.85, 3.0)), 0.14, 0.14))
    floors = []
    for z in (0.45, 1.35, 2.2, 2.95):
        k = 1 - (z - 0.4) / 2.6 * 0.15
        for y in (-0.5 * k, 0.5 * k):
            floors.append((Vector((-0.6 * k, y, z)), Vector((0.6 * k, y, z)), 0.12, 0.12))
        for x in (-0.55 * k, 0.55 * k):
            floors.append((Vector((x, -0.55 * k, z)), Vector((x, 0.55 * k, z)), 0.12, 0.12))
    S_.beams(posts + floors, 'wood', 'body', 'Tower frame')
    S_.straps([(p[0], p[1], 0.14, 0.14, (0.2, 0.5, 0.8)) for p in posts], 'dark_steel', 'body')
    # planked walls on the two visible faces, iron-banded and studded, crimson banners and trophy skulls
    for face in ('front', 'side'):
        f = garb.F.Field('Tower wall')

        def wall(P_, face=face):
            if face == 'front':
                return np.maximum.reduce([np.abs(P_[:, 0] - 0.5 + 0.06 * (P_[:, 2] - 0.4)) - 0.02, np.abs(P_[:, 1]) - 0.48,
                                          0.6 - P_[:, 2], P_[:, 2] - 2.85])
            return np.maximum.reduce([np.abs(P_[:, 1] + 0.48 - 0.06 * (P_[:, 2] - 0.4)) - 0.02, np.abs(P_[:, 0]) - 0.52,
                                      0.6 - P_[:, 2], P_[:, 2] - 2.85])
        f.add(garb.F.Fn(wall, (-0.8, -0.8, 0.5), (0.8, 0.8, 3.0)), 0.0)
        f.disp = lambda P_: (-0.006 * np.clip(1 - np.abs(np.sin(P_[:, 2] * math.pi / 0.17)) * 7, 0, 1)
                             + garb.F.noise3(P_ * np.array([1, 1, 5.0]), 14.0, 3, 2) * 0.004)
        S_.add(f, 'wood', 'body', 0.007, 'Tower wall')
        bands = garb.F.Field('Wall bands')
        studs = garb.F.Field('Wall studs')
        for z in (1.0, 1.85, 2.65):
            if face == 'front':
                x = 0.5 - 0.06 * (z - 0.4) + 0.025
                bands.add(garb.F.Box(Vector((x, 0, z)), (0.012, 0.47, 0.035), r=0.006), 0.0)
                for y in np.linspace(-0.4, 0.4, 6):
                    studs.add(garb.F.Sphere(Vector((x + 0.012, y, z)), 0.018), 0.0)
            else:
                y = -0.48 + 0.06 * (z - 0.4) - 0.025
                bands.add(garb.F.Box(Vector((0, y, z)), (0.51, 0.012, 0.035), r=0.006), 0.0)
                for x in np.linspace(-0.42, 0.42, 6):
                    studs.add(garb.F.Sphere(Vector((x, y - 0.012, z)), 0.018), 0.0)
        S_.add(bands, 'dark_steel', 'body', 0.004, 'Wall bands')
        S_.add(studs, 'dark_steel', 'body', 0.003, 'Wall studs', 1)
    # crenellated fighting top
    cr = garb.F.Field('Battlement')
    k = 0.85
    for i in range(5):
        t = -0.5 + i * 0.25
        for side in (-1, 1):
            cr.add(garb.F.Box(Vector((t * k * 1.1, side * 0.5 * k, 3.1)), (0.07, 0.05, 0.12), r=0.015), 0.0)
            cr.add(garb.F.Box(Vector((side * 0.52 * k, t * k * 1.1, 3.1)), (0.05, 0.07, 0.12), r=0.015), 0.0)
    cr.disp = EN.grain()
    S_.add(cr, 'wood', 'body', 0.005, 'Battlement')
    # bone stakes along the base and trophy skulls on the face
    st_ = garb.F.Field('Bone stakes')
    for y in (-0.36, -0.12, 0.12, 0.36):
        st_.add(garb.F.RoundCone(Vector((0.55, y, 0.62)), Vector((0.95, y * 1.1, 0.42)), 0.045, 0.005), 0.0)
    S_.add(st_, 'bone', 'body', 0.004, 'Bone stakes')
    sk = garb.F.Field('Trophy skulls')
    for y, z in ((-0.26, 1.42), (0.22, 2.25)):
        c = Vector((0.5 - 0.06 * (z - 0.4) + 0.08, y, z))
        sk.add(garb.F.Ellipsoid(c, (0.075, 0.08, 0.085)), 0.0)
        sk.add(garb.F.RoundCone(c + Vector((0.02, 0, -0.04)), c + Vector((0.07, 0, -0.08)), 0.05, 0.035), 0.02)
        for sy in (-1, 1):
            sk.cut(garb.F.Sphere(c + Vector((0.06, sy * 0.032, 0.0)), 0.025), 0.008)
    S_.add(sk, 'bone', 'body', 0.003, 'Trophy skulls')
    S_.banner(Vector((0.0, -0.62, 3.05)), 0.95, M['cloth'], emblem='rot', emblem_mat=M['linen'], width=0.55, drop=1.1,
              tatter=1.2)
    rp = garb.F.Field('Ramp')
    rp.add(garb.F.Box(Vector((0.62, 0, 2.5)), (0.04, 0.42, 0.42), r=0.02), 0.0)
    rp.disp = EN.grain()
    S_.add(rp, 'wood', 'ramp', 0.006, 'Ramp')
    for w in ch.J['wheels']:
        S_.wheel(w, ch.J[w], 0.32, width=0.12)
    _, C = MC.clips('siege', ch.J, wheel_r=0.32)
    ch.tip_rest = Vector((0.7, 0, 2.6))
    ch.tip_bone = 'ramp'
    k = 1.15   # it should loom over the line (VisualScale already enlarges it in battle)
    return ch, finish_clips(ch, C, 'siege-deploy', body_z=1.5 * k, cam_z=1.6 * k, min_scale=5.0,
                            portrait=dict(target=(0.0, 0, 1.6 * k), distance=9.0), scale=k)
