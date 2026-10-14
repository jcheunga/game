"""Clips for the roster-sheet humanoids with planted feet.

Every frame's legs are solved by IK (rk/pose.py) against foot targets on the ground, so feet never slide or
sink: the walk is a real stride (stance and swing phases, heel lift, hip roll and counter-rotating shoulders),
the idle shifts weight between planted feet, and attacks drive the hips into the strike while the feet hold
(the front foot steps in on heavy blows). Upper-body motion reuses the attack families of rk/anim.py.

Clip contract (rk/anim.py): idle 6, walk 8, attack 10 (contact 4), hit 2, deploy 4, melee 7 for ranged units;
the death performance comes from roster/deaths.py.
"""
import math

import bpy
from mathutils import Vector

from . import anim, pose as PS
from .anim import P
from .rig import LAT, YAW, ROLL, add_poses, blend_poses, smooth

LEG_BONES = ('thigh', 'shin', 'foot')


def _strip_legs(pose):
    out = {}
    for k, v in pose.items():
        if k == '@quat':
            out[k] = {b: q for b, q in v.items() if b.split('.')[0] not in LEG_BONES}
        elif k.split('.')[0] in LEG_BONES:
            continue
        else:
            out[k] = v
    return out


def plant(ch, pose, feet):
    """The pose with its legs solved to reach `feet` ({'R': Leg, 'L': Leg})."""
    base = _strip_legs(pose)
    P_ = ch.poser
    P_.clear()
    P_.apply(base)
    bpy.context.view_layer.update()
    q = {}
    for side, leg in feet.items():
        q.update(PS.solve_leg(ch, side, leg.ankle, leg.pole, leg.toe))
    out = dict(base)
    out['@quat'] = dict(base.get('@quat', {}))
    out['@quat'].update(q)
    return out


def _move(pose, root=(0, 0, 0)):
    p = dict(pose)
    mv = dict(p.get('@move', {}))
    cur = Vector(mv.get('root', (0, 0, 0)))
    mv['root'] = tuple(cur + Vector(root))
    p['@move'] = mv
    return p


def _leg(base_leg, dx=0.0, dy=0.0, dz=0.0, toe=None, knee=None):
    a = base_leg.ankle + Vector((dx, dy, dz))
    pole = Vector(base_leg.pole) + Vector((dx, dy, dz * 0.5)) if base_leg.pole is not None else None
    if knee is not None and pole is not None:
        pole = pole + Vector(knee)
    return PS.Leg(a, pole, toe if toe is not None else base_leg.toe)


def idle(ch, stance, feet, n=anim.IDLE_FRAMES, amount=1.0, cape=True):
    out = []
    for i in range(n):
        ph = math.tau * i / n
        s, c = math.sin(ph), math.cos(ph)
        d = P(chest=[(-1.8 * s * amount, LAT)], spine=[(0.9 * s * amount, LAT)], head=[(1.6 * s * amount, LAT),
                                                                                    (1.2 * c * amount, YAW)],
              hips=[(1.4 * c * amount, ROLL)])
        if cape:
            d.update({'cape.0': 2 * s, 'cape.1': 3 * math.sin(ph - .6), 'cape.2': 4 * math.sin(ph - 1.2)})
        pose = _move(add_poses(stance, d), (0.0, 0.012 * c * amount, -0.012 * (1 - c) * amount))
        out.append(plant(ch, pose, feet))
    return out


def walk(ch, stance, feet, n=anim.WALK_FRAMES, stride=0.56, lift=0.13, bob=0.035, sway=0.02, lean=4.0,
         style='march', cape=True, narrow=0.55, arm_swing=8.0, rise=0.6):
    """A planted stride. Feet come in from the fighting stance toward the body line (`narrow`), keep their
    stagger, and travel back along the ground in the stance phase, then arc forward."""
    out = []
    base_feet = {}
    for side, leg in feet.items():
        sy = -1 if side == 'R' else 1
        a = Vector(leg.ankle)
        a.y = sy * max(0.1, abs(a.y) * narrow)
        a.x = ch.J['ankle.' + side].x
        base_feet[side] = PS.Leg(a, leg.pole, Vector((1, sy * 0.15, 0)))
    duty = 0.56
    for i in range(n):
        ph = math.tau * i / n
        legs = {}
        for side, off in (('R', 0.0), ('L', 0.5)):
            u = (i / n + off) % 1.0
            bl = base_feet[side]
            if u < duty:
                t = u / duty
                dx = stride * (0.5 - t)
                dz = 0.0
                heel = 0.0
            else:
                t = (u - duty) / (1 - duty)
                e = smooth(t)
                dx = stride * (-0.5 + e)
                dz = lift * math.sin(math.pi * t) * (1.25 if style == 'heavy' else 1.0)
                heel = 0.0
            if style == 'shamble' and side == 'L':
                dz *= 0.5
            legs[side] = _leg(bl, dx, 0.0, dz, knee=(0.15, 0, 0.0))
        s2 = math.cos(2 * ph)
        d = P(hips=[(6 * math.sin(ph), YAW), (2.5 * math.cos(ph), ROLL)], spine=[(lean, LAT), (-4 * math.sin(ph), YAW)],
              chest=[(-5 * math.sin(ph), YAW)], head=[(-lean * 0.6, LAT), (3 * math.sin(ph), YAW)],
              upper_arm_R=[(-arm_swing * math.sin(ph), LAT)], upper_arm_L=[(arm_swing * math.sin(ph), LAT)])
        if style == 'shamble':
            d = add_poses(d, P(spine=[(5 * math.sin(ph), ROLL)], head=[(9 * math.sin(ph), ROLL), (8, LAT)]))
        if style == 'prowl':
            d = add_poses(d, P(spine=[(-4, LAT)], head=[(4, LAT)]))
        if cape:
            d.update({'cape.0': 8 + 3 * math.sin(ph), 'cape.1': 8 + 5 * math.sin(ph - .8),
                      'cape.2': 8 + 6 * math.sin(ph - 1.6)})
        drop = -stance.get('@move', {}).get('root', (0, 0, 0))[2]
        pose = _move(add_poses(stance, d), (0.0, sway * math.sin(ph), drop * rise - bob * 0.5 - bob * 0.5 * s2))
        out.append(plant(ch, pose, legs))
    return out


def attack(ch, stance, feet, profile, heavy=1.0, low_strike=False, overrides=None, step=None, front='L'):
    """Upper body from rk/anim's attack families; the hips drive into the strike over planted feet, and the
    front foot steps in on heavy blows."""
    keys = anim.attack_keys(profile, heavy)
    if overrides:
        keys = {k: overrides.get(k, v) for k, v in keys.items()}
    if low_strike and profile not in ('bow-draw', 'crossbow', 'staff-cast', 'flask-toss'):
        keys['contact'] = add_poses(keys['contact'], P(spine=[(-10, LAT)], upper_arm_R=[(-18, LAT)], hand_R=-15,
                                                       move={'root': (0, 0, -0.04)}))
    # legs are planted: strip the families' leg rotations, keep their root travel (it bends the knees)
    for k in list(keys):
        keys[k] = {b: v for b, v in keys[k].items() if b.split('.')[0] not in LEG_BONES}
    step = (0.12 * heavy if profile not in ('bow-draw', 'crossbow', 'staff-cast', 'flask-toss') else 0.0) \
        if step is None else step
    rest = {}
    seq = [(0, rest, 0.0), (1, blend_poses(rest, keys['wind'], .45), 0.0), (3, keys['wind'], -0.2),
           (4, keys['contact'], 1.0), (6, keys['follow'], 1.0), (9, rest, 0.0)]
    out = []
    for f in range(10):
        for (a, pa, sa), (b, pb, sb) in zip(seq, seq[1:]):
            if a <= f <= b:
                t = 0 if b == a else (f - a) / (b - a)
                e = smooth(t)
                pose = add_poses(stance, blend_poses(pa, pb, e))
                st = sa + (sb - sa) * e
                legs = dict(feet)
                if step:
                    fl = feet[front]
                    legs[front] = _leg(fl, step * max(0.0, st), 0.0, 0.05 * step * max(0.0, math.sin(math.pi * e))
                                       if a == 3 else 0.0)
                out.append(plant(ch, pose, legs))
                break
    return out


def melee(ch, stance, feet, ranged_profile):
    keys = anim.melee_keys(anim.MELEE_PROFILE.get(ranged_profile, 'sword-cut'))
    kick = ranged_profile == 'bow-draw'
    for k in list(keys):
        if not kick:
            keys[k] = {b: v for b, v in keys[k].items() if b.split('.')[0] not in LEG_BONES}
    rest = {}
    seq = [(0, rest), (1, blend_poses(rest, keys['wind'], .6)), (2, keys['wind']), (3, keys['contact']),
           (4, keys['follow']), (6, rest)]
    out = []
    for f in range(7):
        for (a, pa), (b, pb) in zip(seq, seq[1:]):
            if a <= f <= b:
                t = 0 if b == a else (f - a) / (b - a)
                pose = add_poses(stance, blend_poses(pa, pb, smooth(t)))
                if kick:
                    # the kicking leg is free; the standing leg stays planted
                    planted = {'L': feet['L']}
                    base = _strip_legs(pose)
                    kpose = {k: v for k, v in pose.items() if k.split('.')[0] in LEG_BONES and k.endswith('.R')}
                    p2 = plant(ch, add_poses(base, kpose), planted)
                    out.append(p2)
                else:
                    out.append(plant(ch, pose, feet))
                break
    return out


def hit(ch, stance, feet):
    h = P(spine=[(12, LAT)], chest=[(8, LAT)], head=[(14, LAT)], upper_arm_R=[(-10, LAT)], upper_arm_L=[(-14, LAT)],
          move={'root': (-0.07, 0, -0.02)})
    return [plant(ch, add_poses(stance, h), feet), plant(ch, add_poses(stance, blend_poses({}, h, .45)), feet)]


def deploy(ch, stance, feet, flourish=None):
    crouch = P(spine=[(-18, LAT)], head=[(14, LAT)], upper_arm_R=[(-10, LAT)], move={'root': (0, 0, -0.2)})
    rise = blend_poses({}, crouch, .4)
    flo = flourish or P(upper_arm_R=[(26, LAT)], forearm_R=-10, spine=[(6, LAT)], head=[(-6, LAT)],
                        move={'root': (0, 0, 0.03)})
    return [plant(ch, add_poses(stance, crouch), feet), plant(ch, add_poses(stance, rise), feet),
            plant(ch, add_poses(stance, flo), feet), plant(ch, add_poses(stance, blend_poses({}, flo, .2)), feet)]


def clips(ch, stance, feet, profile, gait='march', heavy=1.0, low_strike=False, death='back', attack_override=None,
          cape=True, flourish=None, melee_clip=False, front='L', step=None, float_mode=False):
    # solving touches the armature every frame: keep skinned meshes out of the evaluation meanwhile
    mods = [m for o in bpy.data.objects if o.type == 'MESH' for m in o.modifiers if m.type == 'ARMATURE' and m.show_viewport]
    for m in mods:
        m.show_viewport = False
    try:
        return _clips(ch, stance, feet, profile, gait, heavy, low_strike, death, attack_override, cape, flourish,
                      melee_clip, front, step, float_mode)
    finally:
        for m in mods:
            m.show_viewport = True


def _clips(ch, stance, feet, profile, gait, heavy, low_strike, death, attack_override, cape, flourish, melee_clip,
           front, step, float_mode):
    C = {}
    if melee_clip and profile in anim.MELEE_PROFILE:
        C['melee'] = melee(ch, stance, feet, profile)
    if float_mode:
        C['idle'] = anim.float_frames(stance, anim.IDLE_FRAMES)
        C['walk'] = anim.float_frames(stance, anim.WALK_FRAMES, 'walk')
    else:
        C['idle'] = idle(ch, stance, feet, cape=cape)
        kw = dict(march={}, heavy=dict(stride=0.46, bob=0.045, lift=0.1, lean=6),
                  shamble=dict(stride=0.36, bob=0.03, lift=0.07, lean=10, style='shamble', rise=0.3),
                  prowl=dict(stride=0.6, bob=0.04, lift=0.14, lean=8, style='prowl', rise=0.2))[gait]
        kw.setdefault('style', gait if gait in ('heavy', 'shamble', 'prowl') else 'march')
        C['walk'] = walk(ch, stance, feet, cape=cape, **kw)
    if getattr(ch, 'holds', None) and isinstance(ch.holds.get('R'), PS.Grip) and profile in WEAPON_KEYS \
            and not attack_override:
        C['attack'] = weapon_attack(ch, stance, feet, profile, heavy, step=step, front=front)
    else:
        C['attack'] = attack(ch, stance, feet, profile, heavy, low_strike, attack_override, step=step, front=front)
    C['hit'] = hit(ch, stance, feet)
    if isinstance(death, dict):
        from .death import performance
        C['death'] = performance(stance, death)
    else:
        C['death'] = anim.death_frames(stance, death)
    C['deploy'] = deploy(ch, stance, feet, flourish)
    return C


# ------------------------------------------------------------------ weapon-space attacks
def _pitch_yaw(D):
    D = Vector(D).normalized()
    return math.degrees(math.asin(max(-1, min(1, D.z)))), math.degrees(math.atan2(D.y, D.x))


def _dir(pitch, yaw):
    p, y = math.radians(pitch), math.radians(yaw)
    return Vector((math.cos(p) * math.cos(y), math.cos(p) * math.sin(y), math.sin(p)))


# Each family: keys (wind, contact, follow) of
#   grip: offset of the main-hand grip (x fwd, y left, z up) or ('along', d) to slide along the weapon
#   aim: (pitch, yaw) absolute, or ('rel', dpitch, dyaw) relative to the stance aim
#   body: torso pose; root: pelvis travel
WEAPON_KEYS = {
    'spear-thrust': [
        dict(grip=('along', -0.2), aim=('rel', 6, -4), body=P(spine=[(-14, YAW), (4, LAT)], chest=[(-8, YAW)]),
             root=(-0.05, 0, 0)),
        dict(grip=('along', 0.46), aim=('rel', -3, 2), body=P(spine=[(14, YAW), (-8, LAT)], chest=[(8, YAW)]),
             root=(0.14, 0, -0.03)),
        dict(grip=('along', 0.34), aim=('rel', -6, 2), body=P(spine=[(10, YAW), (-6, LAT)], chest=[(6, YAW)]),
             root=(0.11, 0, -0.03))],
    'mounted-lance': [
        dict(grip=('along', -0.12), aim=('rel', 4, 0), body=P(spine=[(-8, YAW), (6, LAT)]), root=(0, 0, 0)),
        dict(grip=('along', 0.3), aim=('rel', -4, 0), body=P(spine=[(10, YAW), (-10, LAT)]), root=(0, 0, 0)),
        dict(grip=('along', 0.22), aim=('rel', -5, 0), body=P(spine=[(8, YAW), (-8, LAT)]), root=(0, 0, 0))],
    'blade-stab': [
        dict(grip=(-0.12, -0.02, 0.06), aim=('rel', 10, 0), body=P(spine=[(-12, YAW)]), root=(-0.04, 0, 0)),
        dict(grip=(0.4, 0.06, 0.06), aim=(2, 4), body=P(spine=[(16, YAW), (-10, LAT)]), root=(0.14, 0, -0.03)),
        dict(grip=(0.32, 0.06, 0.0), aim=(-8, 4), body=P(spine=[(12, YAW), (-8, LAT)]), root=(0.11, 0, -0.03))],
    'sword-cut': [
        dict(grip=(-0.16, -0.02, 0.5), aim=(118, -12), body=P(spine=[(-18, YAW), (8, LAT)], chest=[(-8, YAW)]),
             root=(-0.05, 0, 0.01)),
        dict(grip=(0.36, 0.06, 0.1), aim=(-12, 6), body=P(spine=[(18, YAW), (-12, LAT)], chest=[(8, YAW)]),
             root=(0.13, 0, -0.04)),
        dict(grip=(0.26, 0.2, -0.16), aim=(-52, 34), body=P(spine=[(24, YAW), (-14, LAT)], chest=[(10, YAW)]),
             root=(0.11, 0, -0.05))],
    'guard-cut': [
        dict(grip=(-0.1, -0.02, 0.42), aim=(110, -10), body=P(spine=[(-14, YAW), (6, LAT)]), root=(-0.04, 0, 0.0)),
        dict(grip=(0.3, 0.04, 0.12), aim=(-10, 6), body=P(spine=[(14, YAW), (-10, LAT)]), root=(0.1, 0, -0.03)),
        dict(grip=(0.22, 0.16, -0.12), aim=(-48, 30), body=P(spine=[(18, YAW), (-12, LAT)]), root=(0.08, 0, -0.04))],
    'axe-cleave': [
        dict(grip=(-0.22, 0.02, 0.62), aim=(135, -6), body=P(spine=[(-12, YAW), (14, LAT)], chest=[(6, LAT)]),
             root=(-0.06, 0, 0.02)),
        dict(grip=(0.34, 0.04, 0.1), aim=(-30, 4), body=P(spine=[(14, YAW), (-20, LAT)], chest=[(-8, LAT)]),
             root=(0.15, 0, -0.08)),
        dict(grip=(0.28, 0.06, -0.1), aim=(-62, 6), body=P(spine=[(16, YAW), (-26, LAT)], chest=[(-10, LAT)]),
             root=(0.13, 0, -0.1))],
}
WEAPON_KEYS['heavy-smash'] = WEAPON_KEYS['axe-cleave']
WEAPON_KEYS['royal-cleave'] = WEAPON_KEYS['axe-cleave']
WEAPON_KEYS['staff-strike'] = WEAPON_KEYS['sword-cut']
WEAPON_KEYS['hammer-command'] = WEAPON_KEYS['axe-cleave']


def _key_grip(hold, key, D0):
    g = key['grip']
    if isinstance(g, tuple) and g and g[0] == 'along':
        G = hold.point + D0 * g[1]
    else:
        G = hold.point + Vector(g)
    a = key['aim']
    if a[0] == 'rel':
        p0, y0 = _pitch_yaw(D0)
        D = _dir(p0 + a[1], y0 + a[2])
    else:
        D = _dir(a[0], a[1])
    return G, D


def weapon_attack(ch, stance, feet, profile, heavy=1.0, step=None, front='L'):
    """Attack keyed in weapon space: the main hand's grip and the weapon's aim per key, solved by IK; a
    two-handed off hand follows the weapon's second grip."""
    holds = ch.holds
    hold = holds['R']
    keys = WEAPON_KEYS[profile]
    D0 = hold.axis
    g2 = holds.get('g2')
    rest = dict(G=hold.point, D=D0, body={}, root=Vector((0, 0, 0)))
    ks = []
    for k in keys:
        G, D = _key_grip(hold, k, D0)
        ks.append(dict(G=G, D=D, body=k['body'], root=Vector(k['root']) * heavy if k['root'] else Vector()))
    seq = [(0, rest, 0.0), (1, None, 0.0), (3, ks[0], -0.2), (4, ks[1], 1.0), (6, ks[2], 1.0), (9, rest, 0.0)]
    seq[1] = (1, dict(G=rest['G'].lerp(ks[0]['G'], .45), D=rest['D'].lerp(ks[0]['D'], .45).normalized(),
                      body=blend_poses({}, ks[0]['body'], .45), root=ks[0]['root'] * .45), 0.0)
    step = (0.12 * heavy) if step is None else step
    out = []
    base_stance = {k: v for k, v in stance.items()}
    for f in range(10):
        for (a, ka, sa), (b, kb, sb) in zip(seq, seq[1:]):
            if a <= f <= b:
                t = 0 if b == a else (f - a) / (b - a)
                e = smooth(t)
                G = ka['G'].lerp(kb['G'], e)
                D = ka['D'].lerp(kb['D'], e).normalized()
                body = blend_poses(ka['body'], kb['body'], e)
                root = ka['root'].lerp(kb['root'], e)
                pose = _move(add_poses(base_stance, body), tuple(root))
                R = PS.Grip(G, D, pole=hold.pole, pose=hold.pose, toward_tip=hold.toward_tip, size=hold.size)
                L = None
                if g2 is not None and isinstance(holds.get('L'), PS.Grip):
                    Lh = holds['L']
                    L = PS.Grip(G + D * g2, D, pole=Lh.pole, pose=Lh.pose, toward_tip=Lh.toward_tip, size=Lh.size)
                st = sa + (sb - sa) * e
                legs = dict(feet)
                if step:
                    legs[front] = _leg(feet[front], step * max(0.0, st), 0.0, 0.0)
                saved = dict(getattr(ch, 'hand_frames', {}))
                solved = PS.stance(ch, _strip_legs(pose), R=R, L=L)
                ch.hand_frames = saved
                out.append(plant(ch, solved, legs))
                break
    return out
