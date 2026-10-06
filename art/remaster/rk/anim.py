"""Stances and the game clips (idle 6, walk 8, attack 10, hit 2, death 6, deploy 4, and melee 7 for ranged units).

Units listed in roster/deaths.py replace the generic death below with a Performance (rk/death.py).

Poses are dictionaries of bone -> degrees (LAT) or [(deg, axis)], plus '@move'
(world offsets) and '@scale'. Clip poses are layered on top of a stance so the
same motion families work across differently armed characters.
Attack contact/release is local frame 4.
"""
import math

from .rig import LAT, YAW, ROLL, add_poses, blend_poses, smooth

# Loops keep their cycle time whatever their frame count (a 0.64 s breath, a 0.6 s stride).
IDLE_FRAMES = 6
WALK_FRAMES = 8
CLIPS = [('idle', IDLE_FRAMES, .64 / IDLE_FRAMES, True), ('walk', WALK_FRAMES, .6 / WALK_FRAMES, True),
         ('attack', 10, .07, False),
         ('hit', 2, .09, False), ('death', 6, .11, False), ('deploy', 4, .10, False), ('melee', 7, .065, False)]
CONTACT = 4
# Ranged units also carry a close-quarters strike, played when an enemy is inside melee reach.
# Other units leave it out; it is last, so their frame layout is unchanged.
OPTIONAL_CLIPS = {'melee'}
MELEE_CONTACT = 3
MELEE_PROFILE = {'bow-draw': 'front-kick', 'crossbow': 'spear-thrust', 'staff-cast': 'staff-strike',
                 'flask-toss': 'sword-cut', 'hammer-command': 'heavy-smash'}


def P(**kw):
    """Pose literal helper: P(thigh_R=10, spine=[(5, YAW)]) -> {'thigh.R': 10, ...}."""
    out = {}
    for k, v in kw.items():
        if k in ('move', 'scale'):
            out['@' + k] = {b.replace('_R', '.R').replace('_L', '.L'): val for b, val in v.items()}
            continue
        out[k.replace('_R', '.R').replace('_L', '.L')] = v
    return out


# ------------------------------------------------------------------ stances
LEGS_GUARD = P(thigh_L=16, shin_L=-18, foot_L=4, thigh_R=-10, shin_R=-14, foot_R=22,
               move={'root': (0, 0, -0.03)})
LEGS_WIDE = P(thigh_L=20, shin_L=-24, foot_L=6, thigh_R=-14, shin_R=-18, foot_R=30,
              move={'root': (0, 0, -0.06)})
LEGS_EASY = P(thigh_L=8, shin_L=-6, foot_L=-2, thigh_R=-4, shin_R=-6, foot_R=10)

STANCES = {
    'sword_shield': add_poses(LEGS_GUARD, P(
        spine=[(-5, LAT), (-10, YAW)], chest=[(-4, YAW)], head=[(6, LAT), (12, YAW)], neck=3,
        upper_arm_R=[(18, LAT), (-8, ROLL)], forearm_R=62, hand_R=-12,
        upper_arm_L=[(38, LAT), (14, ROLL)], forearm_L=[(48, LAT), (-28, YAW)])),
    'onehand': add_poses(LEGS_GUARD, P(
        spine=[(-5, LAT), (-6, YAW)], head=[(5, LAT), (8, YAW)],
        upper_arm_R=[(16, LAT), (-8, ROLL)], forearm_R=58, hand_R=-10,
        upper_arm_L=[(14, LAT), (6, ROLL)], forearm_L=34)),
    'polearm': add_poses(LEGS_WIDE, P(
        spine=[(-6, LAT), (-14, YAW)], head=[(8, LAT), (16, YAW)],
        upper_arm_R=[(-12, LAT), (-14, ROLL)], forearm_R=78, hand_R=0,
        upper_arm_L=[(30, LAT)], forearm_L=30)),
    'great': add_poses(LEGS_WIDE, P(
        spine=[(-4, LAT), (-18, YAW)], head=[(6, LAT), (20, YAW)],
        upper_arm_R=[(10, LAT), (-12, ROLL)], forearm_R=70, hand_R=8,
        upper_arm_L=[(25, LAT)], forearm_L=40)),
    'bow': add_poses(LEGS_WIDE, P(
        spine=[(-2, LAT), (-26, YAW)], head=[(4, LAT), (30, YAW)],
        upper_arm_L=[(78, LAT), (8, ROLL)], forearm_L=6, hand_L=0,
        upper_arm_R=[(40, LAT)], forearm_R=70)),
    'crossbow': add_poses(LEGS_GUARD, P(
        spine=[(-4, LAT), (-10, YAW)], head=[(6, LAT), (10, YAW)],
        upper_arm_R=[(10, LAT), (-12, ROLL)], forearm_R=80, hand_R=-10,
        upper_arm_L=[(40, LAT)], forearm_L=40)),
    'staff': add_poses(LEGS_EASY, P(
        spine=[(-3, LAT), (-6, YAW)], head=[(4, LAT), (6, YAW)],
        upper_arm_R=[(14, LAT), (-6, ROLL)], forearm_R=60, hand_R=-4,
        upper_arm_L=[(22, LAT), (10, ROLL)], forearm_L=55, hand_L=-20)),
    'caster': add_poses(LEGS_EASY, P(
        spine=[(-3, LAT)], head=[(4, LAT)],
        upper_arm_R=[(24, LAT), (-10, ROLL)], forearm_R=60, hand_R=-10,
        upper_arm_L=[(24, LAT), (10, ROLL)], forearm_L=60, hand_L=-10)),
    'claws': add_poses(LEGS_WIDE, P(
        hips=[(-8, LAT)], spine=[(-14, LAT)], chest=[(-8, LAT)], head=[(22, LAT)], neck=6,
        upper_arm_R=[(34, LAT), (-12, ROLL)], forearm_R=32, hand_R=-10,
        upper_arm_L=[(26, LAT), (12, ROLL)], forearm_L=40, hand_L=-10)),
    'brute': add_poses(LEGS_WIDE, P(
        spine=[(-8, LAT), (-10, YAW)], chest=[(-4, LAT)], head=[(14, LAT), (10, YAW)],
        upper_arm_R=[(12, LAT), (-14, ROLL)], forearm_R=58, hand_R=-6,
        upper_arm_L=[(18, LAT), (14, ROLL)], forearm_L=30)),
    'thrower': add_poses(LEGS_GUARD, P(
        spine=[(-4, LAT), (-6, YAW)], head=[(5, LAT), (8, YAW)],
        upper_arm_R=[(12, LAT), (-8, ROLL)], forearm_R=70, hand_R=-20,
        upper_arm_L=[(24, LAT), (8, ROLL)], forearm_L=50)),
    'dual': add_poses(LEGS_WIDE, P(
        spine=[(-8, LAT), (-12, YAW)], head=[(10, LAT), (12, YAW)],
        upper_arm_R=[(10, LAT), (-10, ROLL)], forearm_R=70, hand_R=-30,
        upper_arm_L=[(30, LAT), (10, ROLL)], forearm_L=60, hand_L=-30)),
    'banner': add_poses(LEGS_GUARD, P(
        spine=[(-3, LAT), (-6, YAW)], head=[(5, LAT), (8, YAW)],
        upper_arm_R=[(16, LAT), (-8, ROLL)], forearm_R=58, hand_R=-10,
        upper_arm_L=[(22, LAT), (10, ROLL)], forearm_L=62)),
    'relaxed': add_poses(LEGS_EASY, P(
        upper_arm_R=[(6, LAT)], forearm_R=14, upper_arm_L=[(4, LAT)], forearm_L=12)),
    'float': P(hips=[(-4, LAT)], spine=[(-6, LAT)], head=[(8, LAT)],
               upper_arm_R=[(24, LAT), (-14, ROLL)], forearm_R=56,
               upper_arm_L=[(30, LAT), (14, ROLL)], forearm_L=62, hand_L=-24,
               thigh_R=10, shin_R=-30, thigh_L=-4, shin_L=-20, foot_R=30, foot_L=30),
}


# ------------------------------------------------------------------ attacks
def attack_keys(profile, heavy=1.0):
    """Return dict(wind, contact, follow) additive poses for a motion family.

    Torso twist convention: positive YAW turns the weapon (near/right) shoulder toward the
    enemy, so wind-ups twist negative and strikes twist positive.
    """
    keys = _attack_keys(profile, heavy)
    if profile not in ('bow-draw', 'crossbow'):
        for pose in keys.values():
            for bone, val in list(pose.items()):
                if bone.startswith('@') or isinstance(val, (int, float)):
                    continue
                pose[bone] = [(-d if tuple(ax) == tuple(YAW) else d, ax) for d, ax in val]
    return keys


def _attack_keys(profile, heavy=1.0):
    h = heavy
    if profile in ('sword-cut', 'guard-cut'):
        guard = profile == 'guard-cut'
        return dict(
            wind=P(upper_arm_R=[(110 * h, LAT), (-20, ROLL)], forearm_R=40, hand_R=30, spine=[(10, LAT), (18, YAW)],
                   chest=[(6, YAW)], thigh_L=-4, move={'root': (-0.04, 0, 0)}),
            contact=P(upper_arm_R=[(40, LAT), (-10, ROLL)], forearm_R=-38, hand_R=-55, spine=[(-14, LAT), (-18, YAW)],
                      chest=[(-6, YAW)], thigh_L=8, shin_L=-6, move={'root': (0.12, 0, -0.02)},
                      upper_arm_L=[(-6 if guard else -20, LAT)]),
            follow=P(upper_arm_R=[(10, LAT)], forearm_R=-50, hand_R=-70, spine=[(-18, LAT), (-24, YAW)],
                     move={'root': (0.1, 0, -0.03)}))
    if profile in ('axe-cleave', 'heavy-smash', 'royal-cleave'):
        return dict(
            wind=P(upper_arm_R=[(140, LAT)], forearm_R=30, hand_R=20, spine=[(16, LAT), (10, YAW)], chest=[(8, LAT)],
                   head=[(-6, LAT)], thigh_L=4, move={'root': (-0.06, 0, 0.02)}),
            contact=P(upper_arm_R=[(30, LAT)], forearm_R=-40, hand_R=-50, spine=[(-24, LAT), (-10, YAW)], chest=[(-10, LAT)],
                      head=[(10, LAT)], thigh_L=14, shin_L=-20, thigh_R=-6, move={'root': (0.14, 0, -0.08)}),
            follow=P(upper_arm_R=[(10, LAT)], forearm_R=-46, hand_R=-60, spine=[(-28, LAT)], chest=[(-12, LAT)],
                     thigh_L=16, shin_L=-24, move={'root': (0.13, 0, -0.1)}))
    if profile in ('spear-thrust', 'blade-stab', 'mounted-lance'):
        return dict(
            wind=P(upper_arm_R=[(-28, LAT)], forearm_R=24, spine=[(8, LAT), (14, YAW)], thigh_L=-4,
                   move={'root': (-0.1, 0, 0)}),
            contact=P(upper_arm_R=[(46, LAT)], forearm_R=-50, hand_R=-4, spine=[(-12, LAT), (-16, YAW)], thigh_L=18,
                      shin_L=-10, thigh_R=-14, foot_R=14, move={'root': (0.2, 0, -0.03)}),
            follow=P(upper_arm_R=[(40, LAT)], forearm_R=-44, spine=[(-10, LAT), (-12, YAW)], thigh_L=14,
                     move={'root': (0.16, 0, -0.02)}))
    if profile == 'bow-draw':
        return dict(
            wind=P(spine=[(4, LAT), (-4, YAW)], upper_arm_R=[(6, LAT), (-30, ROLL)], forearm_R=40,
                   move={'grip.R': (-0.32, 0, 0.04), 'root': (-0.02, 0, 0)}),
            contact=P(spine=[(2, LAT)], move={'grip.R': (-0.36, 0, 0.05), 'root': (-0.03, 0, 0)}),
            follow=P(spine=[(6, LAT)], upper_arm_R=[(-14, LAT)], forearm_R=-10,
                     move={'grip.R': (-0.28, 0.02, 0.1), 'root': (-0.04, 0, 0)}))
    if profile == 'crossbow':
        return dict(
            wind=P(spine=[(-3, LAT)], head=[(4, LAT)], move={'root': (0.01, 0, -0.01)}),
            contact=P(spine=[(10, LAT)], upper_arm_R=[(8, LAT)], forearm_R=12, head=[(-6, LAT)],
                      move={'root': (-0.06, 0, 0)}),
            follow=P(spine=[(8, LAT)], upper_arm_R=[(4, LAT)], move={'root': (-0.07, 0, 0)}))
    if profile in ('staff-cast', 'staff-strike'):
        if profile == 'staff-strike':
            return _attack_keys('sword-cut', 1.0)
        return dict(
            wind=P(upper_arm_R=[(70, LAT)], forearm_R=20, hand_R=10, upper_arm_L=[(-10, LAT)], forearm_L=40,
                   spine=[(10, LAT), (12, YAW)], head=[(-6, LAT)], move={'root': (-0.03, 0, 0.02)}),
            contact=P(upper_arm_R=[(54, LAT)], forearm_R=-36, hand_R=-24, upper_arm_L=[(62, LAT)], forearm_L=-40,
                      hand_L=-30, spine=[(-12, LAT), (-14, YAW)], thigh_L=10, move={'root': (0.08, 0, 0)}),
            follow=P(upper_arm_R=[(46, LAT)], forearm_R=-30, upper_arm_L=[(54, LAT)], forearm_L=-34,
                     spine=[(-10, LAT)], move={'root': (0.07, 0, 0)}))
    if profile == 'flask-toss':
        return dict(
            wind=P(upper_arm_R=[(-60, LAT), (-20, ROLL)], forearm_R=70, hand_R=30, spine=[(12, LAT), (22, YAW)],
                   upper_arm_L=[(40, LAT)], move={'root': (-0.05, 0, 0)}),
            contact=P(upper_arm_R=[(130, LAT)], forearm_R=-50, hand_R=-30, spine=[(-14, LAT), (-20, YAW)],
                      upper_arm_L=[(-10, LAT)], thigh_L=10, move={'root': (0.08, 0, 0)}),
            follow=P(upper_arm_R=[(70, LAT)], forearm_R=-50, hand_R=-40, spine=[(-18, LAT), (-24, YAW)],
                     move={'root': (0.09, 0, -0.02)}))
    if profile == 'hammer-command':
        return dict(
            wind=P(upper_arm_R=[(120, LAT)], forearm_R=40, spine=[(12, LAT), (12, YAW)], upper_arm_L=[(50, LAT)],
                   forearm_L=-30, move={'root': (-0.04, 0, 0.02)}),
            contact=P(upper_arm_R=[(30, LAT)], forearm_R=-40, hand_R=-40, spine=[(-20, LAT), (-12, YAW)],
                      upper_arm_L=[(70, LAT)], forearm_L=-40, thigh_L=10, move={'root': (0.1, 0, -0.04)}),
            follow=P(upper_arm_R=[(16, LAT)], forearm_R=-46, hand_R=-50, spine=[(-22, LAT)],
                     move={'root': (0.1, 0, -0.05)}))
    if profile == 'claw-rake':
        return dict(
            wind=P(upper_arm_R=[(100, LAT), (-20, ROLL)], forearm_R=50, upper_arm_L=[(70, LAT), (20, ROLL)], forearm_L=40,
                   spine=[(14, LAT), (14, YAW)], head=[(-10, LAT)], move={'root': (-0.05, 0, 0.03)}),
            contact=P(upper_arm_R=[(20, LAT)], forearm_R=-10, hand_R=-30, upper_arm_L=[(60, LAT)], forearm_L=30,
                      spine=[(-20, LAT), (-18, YAW)], head=[(14, LAT)], thigh_L=12, shin_L=-14,
                      move={'root': (0.16, 0, -0.04)}),
            follow=P(upper_arm_R=[(-10, LAT)], forearm_R=-10, upper_arm_L=[(20, LAT)], forearm_L=-10,
                     spine=[(-22, LAT), (-10, YAW)], move={'root': (0.14, 0, -0.05)}))
    # default: sword
    return _attack_keys('sword-cut', heavy)


def attack_frames(stance, profile, heavy=1.0, low_strike=False, overrides=None):
    keys = attack_keys(profile, heavy)
    if overrides:
        keys = {k: overrides.get(k, v) for k, v in keys.items()}
    if low_strike and profile not in ('bow-draw', 'crossbow', 'staff-cast', 'flask-toss'):
        # Large figures strike down into infantry rather than overhead.
        keys['contact'] = add_poses(keys['contact'], P(spine=[(-10, LAT)], upper_arm_R=[(-18, LAT)], hand_R=-15,
                                                       move={'root': (0, 0, -0.04)}))
    rest = {}
    seq = [(0, rest), (1, blend_poses(rest, keys['wind'], .45)), (3, keys['wind']), (4, keys['contact']),
           (6, keys['follow']), (9, rest)]
    frames = []
    for f in range(10):
        for (a, pa), (b, pb) in zip(seq, seq[1:]):
            if a <= f <= b:
                t = 0 if b == a else (f - a) / (b - a)
                frames.append(add_poses(stance, blend_poses(pa, pb, smooth(t))))
                break
    return frames


def melee_keys(profile):
    if profile == 'front-kick':
        # The off hand holds a bow and the draw hand is tied to the string, so archers kick.
        return dict(
            wind=P(thigh_R=80, shin_R=-110, foot_R=20, thigh_L=6, spine=[(12, LAT)], head=[(-6, LAT)],
                   upper_arm_L=[(-24, LAT)], move={'root': (-0.05, 0, 0.03)}),
            contact=P(thigh_R=100, shin_R=8, foot_R=35, thigh_L=-14, shin_L=-6, spine=[(22, LAT)], head=[(-14, LAT)],
                      upper_arm_L=[(-30, LAT)], move={'root': (0.1, 0, 0.02)}),
            follow=P(thigh_R=60, shin_R=-60, foot_R=15, spine=[(10, LAT)], upper_arm_L=[(-14, LAT)],
                     move={'root': (0.07, 0, 0.01)}))
    return attack_keys(profile)


def melee_frames(stance, ranged_profile):
    """Seven frames, contact on local frame 3: a quick strike with whatever the ranged unit holds."""
    keys = melee_keys(MELEE_PROFILE.get(ranged_profile, 'sword-cut'))
    rest = {}
    seq = [(0, rest), (1, blend_poses(rest, keys['wind'], .6)), (2, keys['wind']), (3, keys['contact']),
           (4, keys['follow']), (6, rest)]
    frames = []
    for f in range(7):
        for (a, pa), (b, pb) in zip(seq, seq[1:]):
            if a <= f <= b:
                t = 0 if b == a else (f - a) / (b - a)
                frames.append(add_poses(stance, blend_poses(pa, pb, smooth(t))))
                break
    return frames


# ------------------------------------------------------------------ locomotion and reactions
def idle_frames(stance, n=IDLE_FRAMES, amount=1.0, cape=True):
    out = []
    for i in range(n):
        ph = math.tau * i / n
        b = math.sin(ph)
        c = math.cos(ph)
        pose = P(chest=[(-1.6 * b * amount, LAT)], spine=[(0.8 * b * amount, LAT)], head=[(1.5 * b * amount, LAT)],
                 upper_arm_R=[(2.0 * c * amount, LAT)], upper_arm_L=[(-2.0 * c * amount, LAT)],
                 forearm_R=2.0 * b * amount, forearm_L=2.5 * c * amount,
                 move={'root': (0, 0, -0.008 * (1 - c) * amount)})
        if cape:
            pose.update({'cape.0': 2 * b, 'cape.1': 3 * math.sin(ph - .6), 'cape.2': 4 * math.sin(ph - 1.2)})
        out.append(add_poses(stance, pose))
    return out


def walk_frames(stance, n=WALK_FRAMES, stride=26.0, arm_swing=(14, 14), bob=0.03, lean=-4.0, style='march', cape=True):
    out = []
    for i in range(n):
        ph = math.tau * i / n
        s, c = math.sin(ph), math.cos(ph)
        st = stride
        if style == 'shamble':
            st *= 0.7
        legs = P(thigh_R=st * s, thigh_L=-st * s,
                 shin_R=-(8 + 42 * max(0, c) ** 1.2), shin_L=-(8 + 42 * max(0, -c) ** 1.2),
                 foot_R=6 * max(0, -c) - 10 * max(0, s), foot_L=6 * max(0, c) + 10 * max(0, s) * 0)
        body = P(spine=[(lean, LAT), (4 * s, YAW)], chest=[(-5 * s, YAW)], hips=[(3 * s, YAW), (2 * c, ROLL)],
                 head=[(-lean * .6, LAT), (2 * s, YAW)],
                 upper_arm_R=[(-arm_swing[0] * s, LAT)], upper_arm_L=[(arm_swing[1] * s, LAT)],
                 forearm_R=4 + 6 * max(0, -s), forearm_L=4 + 6 * max(0, s),
                 move={'root': (0, 0, -bob * math.cos(2 * ph) - bob * .5)})
        if style == 'shamble':
            body = add_poses(body, P(spine=[(6 * s, ROLL)], head=[(10 * s, ROLL), (8, LAT)]))
        if style == 'prowl':
            body = add_poses(body, P(spine=[(-6, LAT)], move={'root': (0, 0, -0.04)}))
        if cape:
            body.update({'cape.0': 8 + 3 * s, 'cape.1': 8 + 5 * math.sin(ph - .8), 'cape.2': 8 + 6 * math.sin(ph - 1.6)})
        out.append(add_poses(stance, legs, body))
    return out


def float_frames(stance, n, kind='idle'):
    out = []
    for i in range(n):
        ph = math.tau * i / n
        s = math.sin(ph)
        pose = P(move={'root': (0.02 * s if kind == 'walk' else 0, 0, 0.04 * s)}, spine=[(2 * s, LAT)],
                 thigh_R=4 * s, thigh_L=-4 * s, upper_arm_L=[(4 * s, LAT)])
        pose.update({'cape.0': 6 + 3 * s, 'cape.1': 8 + 4 * math.sin(ph - .8), 'cape.2': 10 + 5 * math.sin(ph - 1.6)})
        out.append(add_poses(stance, pose))
    return out


def hit_frames(stance):
    hit = P(spine=[(12, LAT)], chest=[(8, LAT)], head=[(14, LAT)], upper_arm_R=[(-10, LAT)], upper_arm_L=[(-14, LAT)],
            thigh_L=-6, move={'root': (-0.06, 0, 0)})
    return [add_poses(stance, hit), add_poses(stance, blend_poses({}, hit, .45))]


def death_frames(stance, style='back', lie_height=0.16, slide=0.75):
    """Six poses ending on the ground. style: back | forward | crumble."""
    out = []
    if style == 'crumble':
        for i in range(6):
            t = smooth(i / 5)
            out.append(add_poses(blend_poses(stance, {}, t * .6), P(
                thigh_R=50 * t, thigh_L=60 * t, shin_R=-110 * t, shin_L=-120 * t, foot_R=40 * t, foot_L=50 * t,
                spine=[(-30 * t, LAT), (10 * t, ROLL)], head=[(30 * t, LAT), (-20 * t, ROLL)],
                upper_arm_R=[(-20 * t, LAT)], upper_arm_L=[(-10 * t, LAT)],
                move={'root': (0.05 * t, 0, -0.62 * t)})))
        return out
    sign = 1 if style == 'back' else -1
    keys = [
        (0, P(spine=[(14 * sign, LAT)], head=[(18 * sign, LAT)], move={'root': (-0.05 * sign, 0, 0)})),
        (1, P(spine=[(18 * sign, LAT)], head=[(20 * sign, LAT)], thigh_R=20, thigh_L=26, shin_R=-40, shin_L=-50,
              upper_arm_R=[(-30, LAT)], upper_arm_L=[(-20, LAT)], move={'root': (-0.08 * sign, 0, -0.12)})),
        (3, P(root=[(55 * sign, LAT)], spine=[(10 * sign, LAT)], head=[(14 * sign, LAT)], thigh_R=40, thigh_L=30,
              shin_R=-60, shin_L=-40, upper_arm_R=[(60 * sign, LAT)], upper_arm_L=[(80 * sign, LAT)],
              move={'root': (slide * 0.4 * sign, 0, 0.0)})),
        (5, P(root=[(88 * sign, LAT)], spine=[(-4 * sign, LAT)], head=[(-12 * sign, LAT)], thigh_R=10, thigh_L=24,
              shin_R=-18, shin_L=-36, upper_arm_R=[(90 * sign, LAT)], upper_arm_L=[(110 * sign, LAT)],
              forearm_R=10, move={'root': (slide * sign, 0, lie_height)})),
    ]
    for i in range(6):
        for (a, pa), (b, pb) in zip(keys, keys[1:]):
            if a <= i <= b:
                t = 0 if a == b else (i - a) / (b - a)
                fade = blend_poses(stance, {}, min(1, i / 3))
                out.append(add_poses(fade, blend_poses(pa, pb, smooth(t))))
                break
    return out


def deploy_frames(stance, flourish=None):
    crouch = P(thigh_R=40, thigh_L=46, shin_R=-80, shin_L=-90, foot_R=40, foot_L=44, spine=[(-20, LAT)],
               head=[(16, LAT)], upper_arm_R=[(-10, LAT)], move={'root': (0, 0, -0.26)})
    rise = blend_poses({}, crouch, .45)
    flo = flourish or P(upper_arm_R=[(30, LAT)], forearm_R=-10, spine=[(6, LAT)], head=[(-6, LAT)],
                        move={'root': (0, 0, 0.03)})
    return [add_poses(stance, crouch, P(scale={'root': 0.92})), add_poses(stance, rise),
            add_poses(stance, flo), add_poses(stance, blend_poses({}, flo, .2))]


def all_clips(stance, profile, gait='march', heavy=1.0, low_strike=False, death='back', attack_override=None,
              float_mode=False, cape=True, flourish=None, melee=False):
    clips = {}
    if melee and profile in MELEE_PROFILE:
        clips['melee'] = melee_frames(stance, profile)
    if float_mode:
        clips['idle'] = float_frames(stance, IDLE_FRAMES)
        clips['walk'] = float_frames(stance, WALK_FRAMES, 'walk')
    else:
        clips['idle'] = idle_frames(stance, IDLE_FRAMES, cape=cape)
        if gait == 'heavy':
            clips['walk'] = walk_frames(stance, WALK_FRAMES, stride=22, bob=0.04, lean=-6, cape=cape)
        elif gait == 'shamble':
            clips['walk'] = walk_frames(stance, WALK_FRAMES, stride=20, bob=0.025, lean=-10, style='shamble', cape=cape)
        elif gait == 'prowl':
            clips['walk'] = walk_frames(stance, WALK_FRAMES, stride=30, bob=0.035, lean=-10, style='prowl', cape=cape)
        else:
            clips['walk'] = walk_frames(stance, WALK_FRAMES, cape=cape)
    clips['attack'] = attack_frames(stance, profile, heavy, low_strike, attack_override)
    clips['hit'] = hit_frames(stance)
    if isinstance(death, dict):
        from .death import performance
        clips['death'] = performance(stance, death)
    else:
        clips['death'] = death_frames(stance, death)
    clips['deploy'] = deploy_frames(stance, flourish)
    return clips
