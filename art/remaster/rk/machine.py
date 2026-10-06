"""Siege machines and inanimate hosts: ballistae, bombards, siege towers and bone nests."""
import math

from mathutils import Vector

from .anim import IDLE_FRAMES, P, WALK_FRAMES
from .rig import LAT, ROLL, Skeleton, blend_poses, smooth


def _v(*a):
    return Vector(a)


def skeleton(wheels=(), extra=None):
    """wheels: list of (name, centre). extra(sk) adds machine-specific bones."""
    def build(ident, spec, coll):
        sk = Skeleton(ident, coll)
        sk.bone('root', (0, 0, 0), (0, 0, 0.25), deform=False)
        sk.bone('body', (0, 0, 0.4), (0, 0, 0.8), 'root')
        J = {'wheels': []}
        for name, c in wheels:
            c = _v(*c)
            sk.bone(name, c, c + _v(0, 0, 0.15), 'body')
            J['wheels'].append(name)
            J[name] = c
        if extra:
            extra(sk, J)
        arm = sk.build()
        return arm, J
    return build


def clips(kind, J, recoil=0.12, wheel_r=0.3):
    """kind: ballista | bombard | siege | nest."""
    C = {}
    rest = {}
    wheels = J.get('wheels', [])

    def roll(pose, dist):
        ang = -math.degrees(dist / max(0.05, wheel_r))
        for w in wheels:
            pose[w] = [(ang, LAT)]
        return pose
    idle = []
    for i in range(IDLE_FRAMES):
        ph = math.tau * i / IDLE_FRAMES
        s = math.sin(ph)
        if kind == 'nest':
            pose = P(scale={'body': (1 + 0.04 * s, 1 + 0.04 * s, 1 - 0.035 * s)})
            pose['heart'] = [(0, LAT)]
            pose['@scale']['heart'] = 1 + 0.12 * s
        else:
            pose = P(body=[(0.6 * s, LAT)], move={'body': (0, 0, 0.004 * s)})
            if 'arm' in J:
                pose['arm'] = [(1.0 * s, LAT)]
        idle.append(pose)
    C['idle'] = idle
    walk = []
    for i in range(WALK_FRAMES):
        ph = math.tau * i / WALK_FRAMES
        if kind == 'nest':
            pose = P(body=[(4 * math.sin(ph), LAT)], scale={'body': (1 + 0.06 * math.sin(ph * 2), 1, 1 - 0.05 * math.sin(ph * 2))},
                     move={'root': (0.03 * math.sin(ph), 0, 0)})
        else:
            pose = P(body=[(1.2 * math.sin(ph * 2), LAT)], move={'body': (0, 0, 0.012 * abs(math.sin(ph * 2)))})
            roll(pose, 0.54 * i / WALK_FRAMES)
        walk.append(pose)
    C['walk'] = walk
    if kind == 'ballista':
        wind = P(move={'slider': (-0.32, 0, 0)}, arm=[(-3, LAT)])
        contact = P(move={'slider': (0.02, 0, 0), 'body': (-recoil, 0, 0)}, arm=[(4, LAT)], body=[(-3, LAT)])
        follow = P(move={'slider': (0.0, 0, 0), 'body': (-recoil * .6, 0, 0)}, arm=[(2, LAT)], body=[(-1.5, LAT)])
    elif kind == 'bombard':
        wind = P(arm=[(6, LAT)], move={'body': (0.02, 0, 0)})
        contact = P(arm=[(10, LAT)], move={'barrel': (-0.2, 0, 0), 'body': (-recoil, 0, 0)}, body=[(-4, LAT)],
                    scale={'barrel': (1.0, 1.08, 1.0)})
        follow = P(arm=[(7, LAT)], move={'barrel': (-0.1, 0, 0), 'body': (-recoil * .7, 0, 0)}, body=[(-2, LAT)])
    elif kind == 'siege':
        wind = P(body=[(4, LAT)], move={'body': (-0.06, 0, 0)})
        contact = P(body=[(-5, LAT)], ramp=[(-80, LAT)], move={'body': (0.14, 0, 0)})
        follow = P(body=[(-3, LAT)], ramp=[(-70, LAT)], move={'body': (0.1, 0, 0)})
    else:  # nest
        wind = P(scale={'body': (0.9, 0.9, 1.12)}, move={'body': (0, 0, 0.03)})
        contact = P(scale={'body': (1.18, 1.18, 0.82)}, move={'body': (0.06, 0, -0.03)})
        follow = P(scale={'body': (1.08, 1.08, 0.92)})
        for p in (wind, contact, follow):
            p['heart'] = [(0, LAT)]
        wind['@scale']['heart'] = 0.85
        contact['@scale']['heart'] = 1.35
        follow['@scale']['heart'] = 1.15
    seq = [(0, rest), (1, blend_poses(rest, wind, .5)), (3, wind), (4, contact), (6, follow), (9, rest)]
    att = []
    for f in range(10):
        for (a, pa), (b, pb) in zip(seq, seq[1:]):
            if a <= f <= b:
                t = 0 if a == b else (f - a) / (b - a)
                att.append(blend_poses(pa, pb, smooth(t)))
                break
    C['attack'] = att
    if kind in ('ballista', 'bombard'):
        # Close quarters: the engine bucks back, then rams forward on its wheels (the bombard clubs with its barrel).
        m_wind = roll(P(body=[(3, LAT)], move={'body': (-0.05, 0, 0.01)}), -0.05)
        m_contact = roll(P(body=[(-5, LAT)], move={'body': (0.16, 0, -0.01)}), 0.16)
        if kind == 'bombard':
            m_contact['arm'] = [(-9, LAT)]
        m_follow = roll(P(body=[(-2, LAT)], move={'body': (0.1, 0, 0)}), 0.1)
        mseq = [(0, rest), (1, blend_poses(rest, m_wind, .6)), (2, m_wind), (3, m_contact), (4, m_follow), (6, rest)]
        melee = []
        for f in range(7):
            for (a, pa), (b, pb) in zip(mseq, mseq[1:]):
                if a <= f <= b:
                    t = 0 if a == b else (f - a) / (b - a)
                    melee.append(blend_poses(pa, pb, smooth(t)))
                    break
        C['melee'] = melee
    hit = P(body=[(5, LAT), (3, ROLL)], move={'body': (-0.05, 0, 0.02)})
    C['hit'] = [hit, blend_poses({}, hit, .4)]
    death = []
    for i in range(6):
        t = smooth(i / 5)
        if kind == 'nest':
            pose = P(scale={'body': (1 + 0.3 * t, 1 + 0.3 * t, 1 - 0.75 * t)}, move={'body': (0, 0, -0.1 * t)})
            pose['heart'] = [(0, LAT)]
            pose['@scale']['heart'] = max(0.01, 1 - t)
        else:
            pose = P(body=[(-18 * t, LAT), (14 * t, ROLL)], move={'body': (0.05 * t, 0, -0.18 * t)},
                     scale={'body': (1, 1, 1 - 0.25 * t)})
            for w in wheels:
                pose[w] = [(25 * t, ROLL)]
        death.append(pose)
    C['death'] = death
    drop = P(move={'root': (0, 0, 0.25)}, scale={'root': 0.94})
    land = P(move={'root': (0, 0, -0.02)}, scale={'root': (1.03, 1.03, 0.94)})
    C['deploy'] = [drop, land, blend_poses({}, land, .4), {}]
    return {}, C
