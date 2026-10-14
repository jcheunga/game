"""Siege engines in the roster-sheet style: the Lantern Caravan's ballista and crewman, the Rotbound
plague catapult and bone ballista."""
import math

from mathutils import Matrix, Vector

from rk import anim, arms, engines as EN, garb, helms, machine as MC, palette, plate, pose as PS
from rk.anim import P
from rk import character as CH
from rk.character import Character
from rk.rig import LAT, YAW, ROLL, add_poses

from . import unit
from .caravan import BRACE, add_head, deg, dress_head, setup
from .common import finish_clips, profile_of

ENGINE_FACING = -24   # nearer profile, so the carriage's length reads (as on the roster sheet)
BALLISTA_SCALE = 1.5   # modelled at crew scale; the sheet's engines stand as tall as the crewman's chest
CATAPULT_SCALE = 1.45

WHEELS4 = lambda r=0.3, x=0.42, y=0.44: [('wheel.FR', (x, -y, r)), ('wheel.FL', (x, y, r)),
                                         ('wheel.BR', (-x, -y, r)), ('wheel.BL', (-x, y, r))]


def ballista_skeleton():
    def extra(sk, J):
        sk.bone('arm', (0.05, 0, 0.7), (0.05, 0, 0.95), 'body')
        sk.bone('slider', (-0.12, 0, 0.9), (0.08, 0, 0.9), 'arm')
    return MC.skeleton(WHEELS4(), extra)


def catapult_skeleton():
    def extra(sk, J):
        sk.bone('arm', (-0.05, 0, 0.86), (0.3, 0, 1.06), 'body')
    return MC.skeleton(WHEELS4(0.34, 0.48, 0.5), extra)


@unit('player_ballista')
def ballista_crew(ident, title):
    CH.set_facing(ENGINE_FACING)
    M = palette.caravan()
    ch = Character(ident, title, skeleton_fn=ballista_skeleton())
    bolt, tip = EN.ballista(ch, M, banner=M['cloth'], emblem='lantern', emblem_mat=M['gold'])
    ch.tip_rest = tip
    ch.tip_bone = 'slider'
    # the crewman works the near winch crank from behind the engine, leaning in with both hands
    k = BALLISTA_SCALE
    off = Vector((-0.62 * k - 0.45, -0.22 * k - 0.3, 0))
    hub = Vector((-0.62, -0.22 - 0.03, 0.82)) * k - off
    cM, crew, cst, K, A = setup(ident + '_crew', 'Ballista crewman', BRACE, dict(bulk=1.04),
                                R=PS.Grip(hub + Vector((-0.04, -0.02, 0.15 * k)), Vector((0, 1, 0)), pole=(-0.2, -0.7, 0.9)),
                                L=PS.Grip(hub + Vector((0.1 * k, -0.02, -0.02)), Vector((0, 1, 0)), pole=(0.2, 0.5, 0.8)),
                                feet=dict(drop=0.14, spread=0.32, stride=0.62, front='L', knees_out=0.4))
    crew.coll = ch.coll
    K.shirt('linen', sleeves='forearm', t=0.01, roll='linen')
    K.gambeson('leather', t=0.022, skirt=0.2, sleeves='none', quilt=False)
    K.trousers('trousers_dark', bottom=0.5)
    K.boots('leather', cuff='leather_dark', height=0.6)
    K.tabard('cloth', trim='gold', emblem='lantern', emblem_mat='gold', width=0.13, length=crew.Jp['knee.R'].z + 0.1,
             t=0.05)
    K.belt('leather_dark', buckle='trim', pouches=[(-80, 1.0)])
    K.bracers('leather_dark')
    A.pauldron('R', size=0.9, lames=1)
    C, u = dress_head(crew, cM, face=dict(frown=0.7), beard=dict(style='short', length=0.2))
    add_head(crew, helms.burgonet(C, u, cM, crew.coll, cheeks=False, comb=False))
    crew.rest_hand('R', cM['leather_dark'], pose='grip')
    crew.rest_hand('L', cM['leather_dark'], pose='grip')
    _, MCc = MC.clips('ballista', ch.J, recoil=0.1)
    cc = {
        'idle': anim.idle_frames(cst, anim.IDLE_FRAMES, cape=False),
        'walk': anim.walk_frames(cst, anim.WALK_FRAMES, stride=18, cape=False),
        'attack': [add_poses(cst, P(spine=[(5 * math.sin(f / 9 * math.pi), LAT)],
                                    upper_arm_R=[(16 * math.sin(f / 9 * math.tau), LAT)],
                                    upper_arm_L=[(-16 * math.sin(f / 9 * math.tau), LAT)])) for f in range(10)],
        'hit': anim.hit_frames(cst), 'death': anim.death_frames(cst), 'deploy': anim.deploy_frames(cst),
        'melee': anim.melee_frames(cst, 'bow-draw'),
    }
    opts = finish_clips(ch, MCc, profile_of(ident), release=bolt, body_z=0.75 * k, cam_z=0.75 * k, min_scale=3.4,
                        companions=[(crew, cc, cst)], portrait=dict(target=(0.0, 0, 0.8 * k), distance=7.5), scale=k)
    crew.arm.location = tuple(off)
    return ch, opts


@unit('enemy_plague_engine')
def plague_engine(ident, title):
    CH.set_facing(ENGINE_FACING)
    M = palette.host()
    ch = Character(ident, title, skeleton_fn=catapult_skeleton())
    ball, ball_c = EN.catapult(ch, M, banner=M['cloth'], emblem='rot', emblem_mat=M['linen'])
    ch.tip_rest = ball_c
    ch.tip_bone = 'arm'
    _, MCc = MC.clips('catapult', ch.J, recoil=0.1, wheel_r=0.34)
    k = CATAPULT_SCALE
    return ch, finish_clips(ch, MCc, profile_of(ident), release=ball, body_z=0.8 * k, cam_z=0.9 * k, min_scale=3.6,
                            portrait=dict(target=(0.0, 0, 0.9 * k), distance=7.0), scale=k)


@unit('enemy_boneballista')
def bone_ballista(ident, title):
    CH.set_facing(ENGINE_FACING)
    M = palette.host()
    ch = Character(ident, title, skeleton_fn=ballista_skeleton())
    bolt, tip = EN.ballista(ch, M, wood='wood', bone_mat=M['bone'], banner=M['cloth'], emblem='rot',
                            emblem_mat=M['linen'], skulls=True)
    ch.tip_rest = tip
    ch.tip_bone = 'slider'
    _, MCc = MC.clips('ballista', ch.J, recoil=0.1)
    k = BALLISTA_SCALE
    return ch, finish_clips(ch, MCc, profile_of(ident), release=bolt, body_z=0.75 * k, cam_z=0.75 * k, min_scale=3.2,
                            portrait=dict(target=(0.0, 0, 0.7 * k), distance=6.5), scale=k)
