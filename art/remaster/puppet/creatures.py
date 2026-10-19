"""Puppet rigs for the units that are not standing people: four-legged beasts, serpents, blobs and floaters,
mounted riders and machines. Imported by build.py (Blender); same clip contract and render loop as the humanoids.

Each rig is a skeleton placed from the painting's landmarks (puppet/landmarks.py KINDS) and an Animator with the
humanoid one's interface: clear, idle, walk, attack, melee, hit, deploy, death, plus body_bone / contact_bone for the
metadata points. Layers (puppet/layers.py) are used when the unit has them: a mount's rider and lance, a machine's
wheels; otherwise the painting is one mesh weighted to the skeleton.
"""
import json
import math

import bpy
import numpy as np
from mathutils import Vector

import build as B

UP = np.array([0.0, -40.0])


def P(lm, k):
    return B.P(lm, k)


def mid(a, b, t=0.5):
    return a + (b - a) * t


# ------------------------------------------------------------------ skeletons (name, head, tail, parent, radius, deform)
def quad_bones(lm, foot='paw'):
    """A four-legged body: spine croup->withers, neck, head, two-piece tail, two-piece legs."""
    g = lambda k: P(lm, k)
    wi, cr = g('withers'), g('croup')
    poll, head = g('poll'), g('head')
    bones = [('body', cr, wi, 'root', 0.42, True)]
    if poll is not None:
        bones.append(('neck', wi, poll, 'body', 0.26, True))
        if head is not None:
            bones.append(('head', poll, head, 'neck', 0.24, True))
    tb, tt = P(lm, 'tail_base'), P(lm, 'tail_tip')
    if tb is not None and tt is not None:
        m = mid(tb, tt)
        bones += [('tail1', tb, m, 'body', 0.09, True), ('tail2', m, tt, 'tail1', 0.08, True)]
    for leg, top, joint in (('fl', 'shoulder', 'knee'), ('fr', 'shoulder', 'knee'), ('hl', 'hip', 'hock'), ('hr', 'hip', 'hock')):
        a, b, c = P(lm, f'{leg}_{top}'), P(lm, f'{leg}_{joint}'), P(lm, f'{leg}_{foot}')
        if a is None or b is None or c is None:
            continue
        bones += [(f'{leg}.upper', a, b, 'body', 0.12, True), (f'{leg}.lower', b, c, f'{leg}.upper', 0.1, True)]
    return bones


def serpent_bones(lm):
    g = lambda k: P(lm, k)
    pts = [g(k) for k in ('head', 'neck', 'mid1', 'mid2', 'mid3', 'tail_tip')]
    head, neck, m1, m2, m3, tail = pts
    return [('mid', m2, m1, 'root', 0.2, True), ('fore', m1, neck, 'mid', 0.19, True),
            ('head', neck, head, 'fore', 0.17, True), ('hind', m2, m3, 'root', 0.18, True),
            ('tail', m3, tail, 'hind', 0.13, True)]


def blob_bones(lm):
    top, bot, cen = P(lm, 'top'), P(lm, 'bottom'), P(lm, 'center')
    # upright bones (straight up from the ground under the centre): squash and stretch along them then stays
    # vertical instead of shearing the body sideways
    base = np.array([cen[0], bot[1]])
    crown = np.array([cen[0], top[1]])
    return [('body', base, cen, 'root', 0.55, True), ('crown', cen, crown, 'body', 0.45, True)]


def rider_bones(lm):
    g = lambda k: P(lm, k)
    pel, chest, neck, chin, top = g('pelvis'), g('chest'), g('neck'), g('chin'), g('head_top')
    bones = [('seat', pel, mid(pel, chest, 0.4), 'body', 0.35, True), ('spine', mid(pel, chest, 0.4), chest, 'seat', 0.35, True),
             ('chest', chest, neck, 'spine', 0.35, True), ('rneck', neck, chin, 'chest', 0.18, True),
             ('rhead', chin, top, 'rneck', 0.3, True)]
    for s in 'RL':
        sh, el, wr = g('shoulder_' + s), g('elbow_' + s), g('wrist_' + s)
        if sh is None or el is None or wr is None:
            continue
        bones += [('upper_arm.' + s, sh, el, 'chest', 0.17, True), ('forearm.' + s, el, wr, 'upper_arm.' + s, 0.15, True),
                  ('hand.' + s, wr, wr + (wr - el) * 0.35, 'forearm.' + s, 0.13, True)]
    return bones


def machine_bones(lm):
    g = lambda k: P(lm, k)
    body = g('body')
    bones = [('chassis', body + np.array([0.0, 60.0]), body - np.array([0.0, 60.0]), 'root', 0.9, True)]
    ap, at = g('arm_pivot'), g('arm_tip')
    if ap is not None and at is not None:
        bones.append(('arm', ap, at, 'chassis', 0.08, True))
    cp, ch = g('crew_pelvis'), g('crew_head')
    if cp is not None and ch is not None:
        bones.append(('crew', cp, ch, 'chassis', 0.12, True))
    return bones


def scale_of(kind, lm, paint):
    """The length the bone radii are fractions of."""
    def d(a, b):
        pa, pb = P(lm, a), P(lm, b)
        return float(np.linalg.norm(pa - pb)) if pa is not None and pb is not None else None
    if kind == 'quad':
        return d('withers', 'croup') or paint.fig_w * B.PPU * 0.5
    if kind == 'mount':
        return d('horse_withers', 'horse_croup') or paint.fig_w * B.PPU * 0.5
    if kind == 'serpent':
        pts = [P(lm, k) for k in ('head', 'neck', 'mid1', 'mid2', 'mid3', 'tail_tip')]
        return sum(float(np.linalg.norm(a - b)) for a, b in zip(pts, pts[1:])) * 0.5
    if kind == 'blob':
        return d('top', 'bottom') or paint.fig_h * B.PPU
    return max(paint.fig_w, paint.fig_h) * B.PPU * 0.5


# ------------------------------------------------------------------ rig
def spoke_symmetry(path, center, axes):
    """The smallest turn (radians) that brings a painted wheel back onto itself: its spokes' spacing, sampled on the
    wheel's ellipse (it is painted in three-quarter view). A walk cycle that turns the wheel by whole steps of it
    loops without a jump. Cart wheels have 6-12 spokes; 8 when it cannot tell."""
    img, px = B.load_rgba(path)
    bpy.data.images.remove(img)
    lum = (px[..., :3].mean(axis=2) * (px[..., 3] > .5))
    cx, cy = center
    a, b = axes
    rr = np.linspace(.3, .8, 16)
    th = np.radians(np.arange(0, 360, 1.0))
    xs = (cx + rr[:, None] * a * np.cos(th)[None, :]).astype(int).clip(0, lum.shape[1] - 1)
    ys = (cy + rr[:, None] * b * np.sin(th)[None, :]).astype(int).clip(0, lum.shape[0] - 1)
    ring = lum[ys, xs]
    ring = ring - ring.mean(axis=1, keepdims=True)                 # polar samples: radius x angle
    score0 = float((ring * ring).sum()) or 1.0
    scores = {k: float((ring * np.roll(ring, int(round(360 / k)), axis=1)).sum()) / score0 for k in range(4, 17)}
    k = max(scores, key=scores.get)
    # a wheel that repeats every 360/k also repeats every 360/(k/2): prefer the finest spacing that scores nearly
    # as well
    fine = [q for q in scores if q > k and q % k == 0 and scores[q] > scores[k] - .1]
    k = max(fine) if fine else k
    return math.tau / k if scores[k] > .3 else math.tau / 8


def rig(ident, d, lm, paint, img, info):
    kind = lm.get('kind', 'blob')
    S = scale_of(kind, lm, paint)
    wpn, wl = None, None
    root = [('root', None, None, None, 0, False)]
    if kind == 'quad':
        bones = quad_bones(lm)
    elif kind == 'serpent':
        bones = serpent_bones(lm)
    elif kind == 'mount':
        # the mount's landmarks carry a horse_ prefix for the head and spine
        hb = quad_bones({**lm, 'withers': lm.get('horse_withers'), 'croup': lm.get('horse_croup'),
                         'poll': lm.get('horse_poll'), 'head': lm.get('horse_head')}, foot='hoof')
        bones = hb + rider_bones(lm)
        wl = (info or {}).get('weapon_line')
        if wl:
            wpn = (np.array(wl['grip'], float), np.array(wl['tip'], float))
        elif P(lm, 'weapon_grip') is not None and P(lm, 'weapon_tip') is not None:
            wpn = (P(lm, 'weapon_grip'), P(lm, 'weapon_tip'))
        if wpn is not None:
            side = min('RL', key=lambda s: np.linalg.norm(P(lm, 'wrist_' + s) - wpn[0])
                       if P(lm, 'wrist_' + s) is not None else 1e9)
            bones.append(('weapon', wpn[0], wpn[1], 'hand.' + side, 0.07, True))
    elif kind == 'machine':
        bones = machine_bones(lm)
    else:
        bones = blob_bones(lm)
    bones = root + bones
    arm = B.build_armature(paint, bones)
    layer_obs = []
    if info and info.get('order'):
        for k, name in enumerate(info['order']):
            li, lpx = B.load_rgba(d / f'{name}.png')
            ob, pix = B.build_mesh(paint, li, lpx[..., 3], name, depth=0.01 * k)
            allowed = LAYER_BONES.get(kind, {}).get(name.split('_')[0])
            if name.startswith('wheel'):
                allowed = ('chassis',)
            B.skin(ob, arm, pix, bones, S, allowed)
            layer_obs.append((name, ob))
    else:
        ob, pix = B.build_mesh(paint, img, paint.alpha)
        B.skin(ob, arm, pix, bones, S)
        layer_obs.append(('master', ob))
    wheels = []
    for name, ob in layer_obs:
        if name.startswith('wheel') and info.get(name, {}).get('spin'):
            wheels.append((ob, paint.to3(info[name]['center']), info[name]['radius'] / B.PPU))
            ax = info[name].get('axes') or [info[name]['radius']] * 2
            ob['axes'] = (ax[0] / B.PPU, ax[1] / B.PPU)
            ob['symmetry'] = spoke_symmetry(d / f'{name}.png', info[name]['center'], ax)
            me = ob.data
            co = np.empty(len(me.vertices) * 3)
            me.vertices.foreach_get('co', co)
            ob['rest_co'] = co.tolist()
    cls = {'quad': QuadAnim, 'serpent': SerpentAnim, 'mount': MountAnim, 'machine': MachineAnim}.get(kind, BlobAnim)
    return dict(arm=arm, layer_obs=layer_obs, side=None, two=False, wpn=wpn, line=wl,
                make_anim=lambda profile: cls(arm, paint, lm, profile, S, ident=ident, wheels=wheels, layer_obs=layer_obs))


LAYER_BONES = {
    'mount': {'base': ('body', 'neck', 'head', 'tail1', 'tail2') + tuple(f'{l}.{p}' for l in ('fl', 'fr', 'hl', 'hr')
                                                                       for p in ('upper', 'lower')),
              'rider': ('seat', 'spine', 'chest', 'rneck', 'rhead', 'upper_arm.R', 'forearm.R', 'hand.R',
                                      'upper_arm.L', 'forearm.L', 'hand.L'),
              'weapon': ('weapon',)},
}


# ------------------------------------------------------------------ animators
class CreatureAnim:
    body_bone = 'body'
    contact_bone = None

    def __init__(self, arm, paint, lm, profile, S, ident='', wheels=(), layer_obs=()):
        self.arm, self.paint, self.lm, self.profile = arm, paint, lm, profile
        self.pb = arm.pose.bones
        self.H = (paint.gy - paint.y0) / B.PPU
        self.S = S / B.PPU
        self.ident = ident
        self.wheels = wheels
        self.layer_obs = layer_obs
        self.release, self.wfall, self.drop = 0.0, 0.0, 0.0
        self.t_death, self.ti_death = 0.0, 0.5
        self.hand_rig = None

    def clear(self):
        self.release, self.wfall, self.drop = 0.0, 0.0, 0.0
        for pb in self.pb:
            pb.rotation_euler = (0, 0, 0)
            pb.location = (0, 0, 0)
            pb.scale = (1, 1, 1)
        for ob, c, r in self.wheels:
            ob.rotation_euler = (0, 0, 0)
            ob.location.x, ob.location.z = 0.0, 0.0
            if 'rest_co' in ob:
                ob.data.vertices.foreach_set('co', np.array(ob['rest_co']))
                ob.data.update()

    def rot(self, name, deg):
        if name in self.pb:
            self.pb[name].rotation_euler.z += math.radians(deg)

    def place(self, dx=0.0, dz=0.0, rot=0.0, sy=1.0, sx=1.0):
        r = self.pb['root']
        r.location = (dx * self.H, dz * self.H, 0.0)
        r.rotation_euler = (0, 0, math.radians(rot))
        r.scale = (sx, sy, 1.0)

    def deploy(self, i, n):
        t = i / max(1, n - 1)
        self.place(sy=0.86 + 0.14 * B.sm(t), sx=1.06 - 0.06 * B.sm(t))

    def melee(self, i, n, c):
        self.attack(i, n, c)

    @staticmethod
    def cast_curve(i, n, hold_from):
        t = min(1.0, i / max(1, hold_from))
        e = 1 - (1 - t) ** 3
        breathe = math.sin(math.tau * max(0, i - hold_from) / max(1, n - hold_from)) if i >= hold_from else 0.0
        return e, breathe

    def cast(self, i, n, hold_from=6):
        e, b = self.cast_curve(i, n, hold_from)
        self.place(sy=1 + .05 * e + .01 * b, sx=1 - .02 * e)

    def death_timing(self, i, n, impact):
        t = i / max(1, n - 1)
        ti = max(0.2, impact / max(1, n - 1))
        self.t_death, self.ti_death = t, ti
        u = min(1.0, max(0.0, t / ti))
        fall = u ** 2.4                     # accelerating like a body under gravity
        after = max(0.0, (t - ti) / max(1e-6, 1 - ti))
        bounce = math.sin(min(1.0, after * 2.4) * math.pi) * (1 - after)
        return t, ti, fall, after, bounce


class QuadAnim(CreatureAnim):
    """Trot (diagonal pairs), crouch-and-pounce with a snap of the head, a stumble when hit, and a collapse."""
    contact_bone = 'head'

    @property
    def stubby(self):
        """A squat beast whose short legs hide under a broad body (landmarks: stubby): it steps, but lunges and
        collapses on its belly instead of flinging its legs out, which would tear them off the painting."""
        return bool(self.lm.get('stubby'))

    def legs(self, ph, amp=22.0, flex=28.0):
        for leg, off, hind in (('fl', 0.0, False), ('hr', 0.0, True), ('fr', math.pi, False), ('hl', math.pi, True)):
            s = math.sin(ph + off)
            self.rot(f'{leg}.upper', amp * s)
            lift = max(0.0, math.cos(ph + off))
            # front knees fold back, hocks fold forward, as the foot lifts
            self.rot(f'{leg}.lower', (flex if hind else -flex) * lift)

    def idle(self, i, n):
        ph = math.tau * i / n
        s = math.sin(ph)
        self.pb['body'].scale = (1.0 + 0.012 * s, 1.0 + 0.02 * s, 1.0)
        self.rot('neck', 2.0 * math.sin(ph + 0.5))
        self.rot('head', 2.5 * math.sin(ph + 1.0))
        self.rot('tail1', 8 * math.sin(ph))
        self.rot('tail2', 10 * math.sin(ph - 0.8))

    STANCE = .62

    def step_legs(self, cyc, stride=.42, lift=.2):
        """A trot driven by the paws: diagonal pairs (front-left with hind-right) move together; each paw is planted
        and slides back under the body, then lifts and swings forward. Upper and lower leg follow by planar two-bone
        IK, each joint bending the way it does at rest (knees forward, hocks back). Returns ground covered per cycle."""
        B_ = self.arm.data.bones
        legs = []
        for leg, off in (('fl', 0.0), ('hr', 0.0), ('fr', .5), ('hl', .5)):
            ub, lb = f'{leg}.upper', f'{leg}.lower'
            if ub not in B_ or lb not in B_:
                continue
            top, knee, paw = B_[ub].head_local, B_[lb].head_local, B_[lb].tail_local
            legs.append((leg, off, top, knee, paw))
        if not legs:
            return 0.0
        leg_len = sum((k - t).length + (p - k).length for _, _, t, k, p in legs) / len(legs)
        S = stride * leg_len
        for leg, off, top, knee, paw in legs:
            L1, L2 = (knee - top).length, (paw - knee).length
            u = (cyc + off) % 1.0
            if u < self.STANCE:
                fx, up = S * (.5 - u / self.STANCE), 0.0
            else:
                q = (u - self.STANCE) / (1 - self.STANCE)
                fx = S * (-.5 + q * q * (3 - 2 * q))
                up = lift * leg_len * math.sin(math.pi * q) ** .8
            target = Vector((paw.x + fx, 0, paw.z + up))
            d = target - top
            dist = max(1e-6, min(d.length, (L1 + L2) * .999))
            dirv = d.normalized()
            a = math.acos(max(-1.0, min(1.0, (L1 * L1 + dist * dist - L2 * L2) / (2 * L1 * dist))))
            # bend the way the rest pose bends: the joint on the same side of the top-paw line
            line = (paw - top).normalized()
            side = (knee - top).x * line.z - (knee - top).z * line.x
            sign = -1.0 if side > 0 else 1.0
            base = math.atan2(dirv.x, -dirv.z)
            up_abs = math.degrees(base + sign * a)
            k = top + Vector((math.sin(math.radians(up_abs)), 0, -math.cos(math.radians(up_abs)))) * L1
            sv = (top + dirv * dist) - k
            lo_abs = math.degrees(math.atan2(sv.x, -sv.z))
            r_up = math.degrees(math.atan2((knee - top).x, -(knee - top).z))
            r_lo = math.degrees(math.atan2((paw - knee).x, -(paw - knee).z))
            self.rot(f'{leg}.upper', up_abs - r_up)
            self.rot(f'{leg}.lower', (lo_abs - up_abs) - (r_lo - r_up))
        return S / self.STANCE

    # Gaits sized to the beast's battle speed (speed_lps: leg lengths per second, set by build). Each: the natural
    # cycle time, the share of the cycle a paw is down (below 0.5 a moment in the air), each paw's phase, the
    # paw's lift (fraction of the leg), how far the body rises in flight, the spine's flex (deg). The cycle is
    # shortened if the paws would have to sweep further than a leg can reach.
    QGAITS = {
        'walk': dict(T=1.0, beta=.66, phase={'hl': 0.0, 'fl': .25, 'hr': .5, 'fr': .75}, lift=.14, air=0.0, flex=1.0),
        'trot': dict(T=.55, beta=.45, phase={'fl': 0.0, 'hr': 0.0, 'fr': .5, 'hl': .5}, lift=.2, air=.02, flex=1.5),
        'canter': dict(T=.6, beta=.4, phase={'hl': 0.0, 'hr': .3, 'fl': .3, 'fr': .55}, lift=.24, air=.03, flex=3.0),
        'gallop': dict(T=.42, beta=.3, phase={'hl': 0.0, 'hr': .1, 'fr': .45, 'fl': .55}, lift=.3, air=.05, flex=6.0),
    }
    speed_lps = 0.0
    MAX_SWEEP = 1.05        # leg lengths a planted paw travels under the body

    def leg_length(self):
        B_ = self.arm.data.bones
        L = [(B_[f'{l}.upper'].length + B_[f'{l}.lower'].length) for l in ('fl', 'fr', 'hl', 'hr') if f'{l}.lower' in B_]
        return sum(L) / len(L) if L else 0.0

    def qgait(self, mount=False):
        v = self.speed_lps
        name = ('canter' if mount else 'gallop') if v > 2.6 else 'trot' if v > 1.1 else 'walk'
        if self.stubby and name == 'gallop':
            name = 'trot'                     # a squat beast lumbers along: it never gallops
        g = dict(self.QGAITS[name])
        if v > 0:
            T = g['T']
            sweep = v * T * g['beta']
            if sweep > self.MAX_SWEEP:
                T = self.MAX_SWEEP / (v * g['beta'])
            # the game shows each of the n frames for 0.025-0.16 s: inside that, the planted paw stays put
            T = max(.27, min(1.55, T))
            g['sweep'] = min(self.MAX_SWEEP, v * T * g['beta'])
        else:
            g['sweep'] = .42
        g['name'] = name
        self.gait_info = {'gait': name, 'sweep': round(g['sweep'], 3)}
        return g

    def gait_legs(self, cyc, g, lift_body=0.0):
        """Every paw by IK along its own path (planted and sweeping back, then lifted and carried forward), with the
        body raised by lift_body (armature units) so planted paws stay on the ground. Returns ground per cycle."""
        B_ = self.arm.data.bones
        legs = []
        for leg in ('fl', 'fr', 'hl', 'hr'):
            ub, lb = f'{leg}.upper', f'{leg}.lower'
            if ub in B_ and lb in B_:
                legs.append((leg, B_[ub].head_local, B_[lb].head_local, B_[lb].tail_local))
        if not legs:
            return 0.0
        Lleg = self.leg_length()
        S = g['sweep'] * Lleg
        beta = g['beta']
        for leg, top, knee, paw in legs:
            L1, L2 = (knee - top).length, (paw - knee).length
            u = (cyc - g['phase'].get(leg, 0.0)) % 1.0
            if u < beta:
                fx, up = S * (.5 - u / beta), 0.0
            else:
                q = (u - beta) / (1 - beta)
                fx = S * (-.5 + q * q * (3 - 2 * q))
                up = g['lift'] * Lleg * math.sin(math.pi * q) ** .8
            top_ = top + Vector((0, 0, lift_body))
            target = Vector((paw.x + fx, 0, paw.z + up))
            d = target - top_
            dist = max(1e-6, min(d.length, (L1 + L2) * .999))
            dirv = d.normalized()
            a = math.acos(max(-1.0, min(1.0, (L1 * L1 + dist * dist - L2 * L2) / (2 * L1 * dist))))
            line = (paw - top).normalized()
            side = (knee - top).x * line.z - (knee - top).z * line.x
            sign = -1.0 if side > 0 else 1.0
            base = math.atan2(dirv.x, -dirv.z)
            up_abs = math.degrees(base + sign * a)
            k = top_ + Vector((math.sin(math.radians(up_abs)), 0, -math.cos(math.radians(up_abs)))) * L1
            sv = (top_ + dirv * dist) - k
            lo_abs = math.degrees(math.atan2(sv.x, -sv.z))
            r_up = math.degrees(math.atan2((knee - top).x, -(knee - top).z))
            r_lo = math.degrees(math.atan2((paw - knee).x, -(paw - knee).z))
            self.rot(f'{leg}.upper', up_abs - r_up)
            self.rot(f'{leg}.lower', (lo_abs - up_abs) - (r_lo - r_up))
        return S / beta

    def flight(self, cyc, g):
        """How far the body is off the ground at cycle phase cyc: zero while any paw is planted, an arc between."""
        down = [((cyc - ph) % 1.0) < g['beta'] for ph in g['phase'].values()]
        if any(down) or not g['air']:
            return 0.0
        # the current airborne stretch: from the last lift-off to the next landing
        lifts = sorted(((ph + g['beta']) % 1.0 for ph in g['phase'].values()))
        lands = sorted(g['phase'].values())
        t0 = max((l for l in lifts if l <= cyc), default=max(lifts) - 1.0)
        t1 = min((l for l in lands if l > cyc), default=min(lands) + 1.0)
        q = (cyc - t0) / max(1e-6, t1 - t0)
        return math.sin(math.pi * max(0.0, min(1.0, q)))

    def walk(self, i, n):
        cyc = i / n
        ph = math.tau * cyc
        if self.speed_lps > 0:
            g = self.qgait()
            air = self.flight(cyc, g) * g['air'] * self.H
            self.walk_stride = self.gait_legs(cyc, g, lift_body=-air)
            self.place(dz=air / self.H)
            # the spine gathers as the hind paws reach under and stretches out as they push off
            hind = g['phase'].get('hl', 0.0)
            flex = math.sin(math.tau * (cyc - hind) + 1.2)
            self.rot('body', g['flex'] * .5 * flex)
            self.rot('neck', -g['flex'] * .8 * flex)
            self.rot('head', 2.0 * math.sin(math.tau * cyc + 1.0))
            self.rot('tail1', 10 * math.sin(ph) - 6 * (g['name'] == 'gallop'))
            self.rot('tail2', 12 * math.sin(ph - 0.8))
            return
        self.walk_stride = self.step_legs(cyc)
        self.place(dz=0.012 * (0.5 - 0.5 * math.cos(2 * ph)))
        self.rot('body', 1.5 * math.sin(2 * ph))
        self.rot('neck', -2.5 * math.sin(2 * ph + .4))
        self.rot('head', 2.0 * math.sin(2 * ph + 1.0))
        self.rot('tail1', 10 * math.sin(ph))
        self.rot('tail2', 12 * math.sin(ph - 0.8))

    def attack(self, i, n, c):
        tc = c / max(1, n - 1)
        keys = [(0, {}), (tc * 0.55, {'dx': -0.03, 'dz': -0.05, 'pitch': 6, 'fu': 18, 'fl': -30, 'hu': -14, 'hl': 26, 'hd': 6}),
                (tc, {'dx': 0.22, 'dz': 0.07, 'pitch': -10, 'fu': -40, 'fl': -10, 'hu': 30, 'hl': 0, 'hd': -14}),
                (min(1, tc + 0.25), {'dx': 0.14, 'dz': 0.0, 'pitch': -3, 'fu': -10, 'hu': 8, 'hd': -4}), (1, {})]
        p = B.ease_keys(keys, i / max(1, n - 1))
        if self.stubby:
            # a lunge and a snap of the jaws: the body surges forward and down over planted legs
            k_ = .35
            self.place(dx=p.get('dx', 0.0) * .45, dz=p.get('dz', 0.0) * .3)
            self.rot('body', p.get('pitch', 0.0) * .8)
            self.rot('head', p.get('hd', 0.0) * 1.6)
            self.rot('neck', p.get('hd', 0.0) * .6)
            for leg in ('fl', 'fr'):
                self.rot(f'{leg}.upper', p.get('fu', 0.0) * k_)
            return
        self.place(dx=p.get('dx', 0.0), dz=p.get('dz', 0.0))
        self.rot('body', p.get('pitch', 0.0))
        for leg in ('fl', 'fr'):
            self.rot(f'{leg}.upper', p.get('fu', 0.0))
            self.rot(f'{leg}.lower', p.get('fl', 0.0))
        for leg in ('hl', 'hr'):
            self.rot(f'{leg}.upper', p.get('hu', 0.0))
            self.rot(f'{leg}.lower', p.get('hl', 0.0))
        self.rot('head', p.get('hd', 0.0))
        self.rot('tail1', -p.get('pitch', 0.0) * 1.5)

    def hit(self, i, n):
        k = 1.0 if i == 0 else 0.45
        self.place(dx=-0.03 * k)
        self.rot('body', 5 * k)
        self.rot('head', 9 * k)
        self.rot('neck', 5 * k)

    def cast(self, i, n, hold_from=6):
        """Rears back on the haunches and throws the head up: a howl, a battle cry."""
        e, b = self.cast_curve(i, n, hold_from)
        self.rot('body', 9 * e + 1.2 * b)
        self.rot('neck', 22 * e + 2 * b)
        self.rot('head', 14 * e)
        for leg in ('fl', 'fr'):
            self.rot(f'{leg}.upper', -14 * e)
            self.rot(f'{leg}.lower', 20 * e)
        self.rot('tail1', 18 * e)

    def death(self, i, n, impact, forward):
        t, ti, fall, after, bounce = self.death_timing(i, n, impact)
        if self.stubby:
            # the legs fold under the body and it drops onto its belly, rolling a little onto its side, the head
            # sagging to the ground
            for leg, hind in (('fl', False), ('fr', False), ('hl', True), ('hr', True)):
                self.rot(f'{leg}.upper', (8 if not hind else -8) * fall)
                self.rot(f'{leg}.lower', (-10 if not hind else 10) * fall)
            stagger = B.sm(min(1.0, t / (ti * 0.35)))
            self.rot('body', 3 * stagger * (1 - fall) - 4 * fall + 2 * bounce)
            self.rot('neck', -10 * fall)
            self.rot('head', -12 * fall)
            sag = float(self.lm.get('sag', 0.07))         # a soft body spreads as it settles (landmarks: sag)
            self.place(dz=-0.10 * fall - 0.015 * bounce, dx=-0.02 * stagger * (1 - fall), sy=1.0 - sag * fall,
                       sx=1.0 + sag * 0.55 * fall)
            return
        # legs give way and splay (forelegs out ahead, hind legs out behind), the belly comes down to the ground
        # and the head lolls onto it: a sprawl that reads as dead from the side
        for leg, hind in (('fl', False), ('fr', False), ('hl', True), ('hr', True)):
            self.rot(f'{leg}.upper', (62 if not hind else -58) * fall)
            self.rot(f'{leg}.lower', (18 if not hind else -14) * fall)
        stagger = B.sm(min(1.0, t / (ti * 0.35)))
        self.rot('body', 4 * stagger * (1 - fall) - 3 * fall + 2 * bounce)
        self.rot('neck', -45 * fall)
        self.rot('head', -22 * fall)
        self.rot('tail1', -35 * fall)
        self.rot('tail2', -15 * fall)
        self.place(dz=-0.02 * bounce, dx=-0.02 * stagger * (1 - fall))


class SerpentAnim(CreatureAnim):
    """A travelling wave down the body; rears and strikes; goes slack and curls in death."""
    body_bone = 'mid'
    contact_bone = 'head'
    CHAIN = ('head', 'fore', 'mid', 'hind', 'tail')

    def wave(self, ph, amp):
        for k, b in enumerate(self.CHAIN):
            self.rot(b, amp * math.sin(ph - k * 1.1) * (0.5 if b == 'head' else 1.0))

    def idle(self, i, n):
        self.wave(math.tau * i / n, 4.0)
        self.pb['mid'].scale = (1.0, 1.0 + 0.015 * math.sin(math.tau * i / n), 1.0)

    def walk(self, i, n):
        ph = math.tau * i / n
        self.wave(ph, 11.0)
        self.place(dz=0.006 * math.sin(2 * ph))

    def attack(self, i, n, c):
        tc = c / max(1, n - 1)
        keys = [(0, {}), (tc * 0.6, {'fore': 18, 'head': 22, 'dx': -0.04, 'tail': -10}),
                (tc, {'fore': -18, 'head': -24, 'dx': 0.16, 'tail': 8, 'mid': -6}),
                (min(1, tc + 0.25), {'fore': -6, 'head': -8, 'dx': 0.08}), (1, {})]
        p = B.ease_keys(keys, i / max(1, n - 1))
        self.place(dx=p.get('dx', 0.0))
        for b in ('fore', 'head', 'tail', 'mid'):
            self.rot(b, p.get(b, 0.0))

    def cast(self, i, n, hold_from=6):
        """Rears its fore-body high and sways, hood up."""
        e, b = self.cast_curve(i, n, hold_from)
        self.rot('fore', 26 * e + 3 * b)
        self.rot('head', -12 * e)
        self.rot('tail', -8 * e)

    def hit(self, i, n):
        k = 1.0 if i == 0 else 0.45
        self.place(dx=-0.03 * k)
        self.rot('fore', 10 * k)
        self.rot('head', 12 * k)

    def death(self, i, n, impact, forward):
        """Thrashes, then the raised fore-body drops to the ground and the whole length goes slack and flat."""
        t, ti, fall, after, bounce = self.death_timing(i, n, impact)
        self.wave(math.tau * t * 2.0, 18.0 * (1 - fall))
        # each raised part is turned back toward the line of the ground by however far it rose
        for b, k in (('fore', 1.0), ('head', 1.0), ('tail', 0.8), ('hind', 0.6)):
            if b in self.pb:
                bone = self.arm.data.bones[b]
                d = bone.tail_local - bone.head_local
                ang = math.degrees(math.atan2(d.z, abs(d.x) + 1e-6))
                self.rot(b, -ang * k * fall * (1 if d.x >= 0 else -1))
        self.pb['mid'].scale = (1.0 + 0.04 * fall, 1.0 - 0.18 * fall, 1.0)
        self.place(dz=-0.01 * bounce)


class BlobAnim(CreatureAnim):
    """Squash and stretch: a creeping pulse, a swell and lunge to attack, a collapse to death. Floaters (the
    reliquary eye, the mirror wisp) hover and bob instead of creeping."""
    contact_bone = 'crown'
    FLOAT = {'enemy_reliquary_eye', 'enemy_mirror_wisp'}

    def squash(self, k, lean=0.0):
        self.pb['body'].scale = (1.0 + 0.6 * k, 1.0 - k, 1.0)
        self.rot('crown', lean)

    def idle(self, i, n):
        ph = math.tau * i / n
        s = math.sin(ph)
        self.squash(0.03 * s, 2.0 * math.sin(ph + 0.7))
        if self.ident in self.FLOAT:
            self.place(dz=0.02 * s)

    def walk(self, i, n):
        ph = math.tau * i / n
        s = math.sin(ph)
        if self.ident in self.FLOAT:
            self.place(dz=0.025 * math.sin(ph), rot=-3 + 1.5 * s)
            self.squash(0.02 * s)
        else:
            self.squash(0.07 * math.sin(2 * ph), -4 + 3 * s)
            self.place(dx=0.01 * s)

    def attack(self, i, n, c):
        tc = c / max(1, n - 1)
        keys = [(0, {}), (tc * 0.7, {'sq': -0.12, 'lean': 6, 'dx': -0.02}), (tc, {'sq': 0.14, 'lean': -12, 'dx': 0.06}),
                (min(1, tc + 0.25), {'sq': 0.04, 'lean': -4, 'dx': 0.03}), (1, {})]
        p = B.ease_keys(keys, i / max(1, n - 1))
        self.squash(p.get('sq', 0.0), p.get('lean', 0.0))
        self.place(dx=p.get('dx', 0.0), dz=0.0)

    def cast(self, i, n, hold_from=6):
        """Swells and pulses with whatever it is channelling."""
        e, b = self.cast_curve(i, n, hold_from)
        self.squash(-.1 * e - .03 * b)
        if self.ident in self.FLOAT:
            self.place(dz=.04 * e + .01 * b)

    def hit(self, i, n):
        k = 1.0 if i == 0 else 0.45
        self.squash(0.1 * k, 6 * k)
        self.place(dx=-0.02 * k)

    def death(self, i, n, impact, forward):
        t, ti, fall, after, bounce = self.death_timing(i, n, impact)
        # sags and spreads where it sits (floaters drop to the ground first)
        if self.ident in self.FLOAT:
            self.place(dz=-0.05 * fall)
        self.pb['body'].scale = (1.0 + 0.22 * fall, 1.0 - 0.45 * fall + 0.06 * bounce, 1.0)
        self.pb['crown'].scale = (1.0 + 0.12 * fall, 1.0 - 0.25 * fall, 1.0)


class MountAnim(QuadAnim):
    """A rider on a four-legged mount: a canter, the lance lowered and driven home with the mount's lunge, and a
    collapse that pitches the mount forward and throws the rider."""
    contact_bone = 'weapon'

    def __init__(self, *a, **kw):
        super().__init__(*a, **kw)
        if 'weapon' not in self.pb:
            self.contact_bone = 'head'

    def idle(self, i, n):
        super().idle(i, n)
        ph = math.tau * i / n
        self.rot('spine', 1.2 * math.sin(ph))
        self.rot('rhead', 1.5 * math.sin(ph + 1.0))

    def walk(self, i, n):
        cyc = i / n
        ph = math.tau * cyc
        if self.speed_lps > 0:
            g = self.qgait(mount=True)
            air = self.flight(cyc, g) * g['air'] * self.H
            self.walk_stride = self.gait_legs(cyc, g, lift_body=-air)
            self.place(dz=air / self.H)
            # the canter's rocking: the forehand rises as the hind paws land, dips onto the leading foreleg; the
            # rider sits it, the seat rolling against the horse
            rock = math.sin(math.tau * (cyc - .1))
            self.rot('body', g['flex'] * .7 * rock)
            self.rot('neck', -g['flex'] * 1.2 * rock)
            self.rot('seat', -g['flex'] * .6 * rock)
            self.rot('tail1', 8 * math.sin(ph))
            return
        self.walk_stride = self.step_legs(cyc, stride=.5, lift=.24)
        self.place(dz=0.02 * (0.5 - 0.5 * math.cos(2 * ph)))
        self.rot('body', 2.5 * math.sin(2 * ph))
        self.rot('neck', -4.0 * math.sin(2 * ph))
        self.rot('seat', -2.5 * math.sin(2 * ph))
        self.rot('tail1', 8 * math.sin(ph))

    def attack(self, i, n, c):
        tc = c / max(1, n - 1)
        if self.profile == 'mounted-lance':
            # a couched lance stays nearly level: the charge drives it home, the point dips only a little
            keys = [(0, {}), (tc * 0.6, {'w': 4, 'lean': 5, 'dx': -0.03, 'pitch': 3}),
                    (tc, {'w': -5, 'lean': -9, 'dx': 0.16, 'pitch': -4, 'fu': -22, 'hu': 16}),
                    (min(1, tc + 0.25), {'w': -2, 'lean': -4, 'dx': 0.09, 'pitch': -2}), (1, {})]
        else:
            keys = [(0, {}), (tc * 0.6, {'w': 12, 'lean': 5, 'dx': -0.02, 'pitch': 3}),
                    (tc, {'w': -16, 'lean': -10, 'dx': 0.12, 'pitch': -4, 'fu': -18, 'hu': 14}),
                    (min(1, tc + 0.25), {'w': -8, 'lean': -5, 'dx': 0.07, 'pitch': -2}), (1, {})]
        p = B.ease_keys(keys, i / max(1, n - 1))
        self.place(dx=p.get('dx', 0.0))
        self.rot('body', p.get('pitch', 0.0))
        self.rot('spine', p.get('lean', 0.0))
        self.rot('weapon', p.get('w', 0.0))
        for leg in ('fl', 'fr'):
            self.rot(f'{leg}.upper', p.get('fu', 0.0))
        for leg in ('hl', 'hr'):
            self.rot(f'{leg}.upper', p.get('hu', 0.0))

    def cast(self, i, n, hold_from=6):
        """The mount rears, the rider raises the weapon."""
        e, b = self.cast_curve(i, n, hold_from)
        self.rot('body', 14 * e + 1.5 * b)
        self.rot('neck', 18 * e)
        for leg in ('fl', 'fr'):
            self.rot(f'{leg}.upper', -30 * e)
            self.rot(f'{leg}.lower', 55 * e)
        self.rot('seat', -12 * e)
        self.rot('weapon', 26 * e)

    def hit(self, i, n):
        super().hit(i, n)
        k = 1.0 if i == 0 else 0.45
        self.rot('spine', 8 * k)

    def death(self, i, n, impact, forward):
        t, ti, fall, after, bounce = self.death_timing(i, n, impact)
        # the mount's forelegs buckle and it pitches onto its chest, then rolls flat; the rider is thrown forward
        # legs fold right under it (forelegs knees-first, hind legs on the hocks) so the belly reaches the ground
        for leg in ('fl', 'fr'):
            self.rot(f'{leg}.upper', -55 * fall)
            self.rot(f'{leg}.lower', 115 * fall)
        for leg in ('hl', 'hr'):
            self.rot(f'{leg}.upper', 50 * fall)
            self.rot(f'{leg}.lower', -110 * fall)
        self.rot('body', -8 * fall + 3 * bounce)
        self.rot('neck', -30 * fall)
        self.rot('seat', -70 * fall)
        self.rot('rhead', -15 * fall)
        # the lance is let go and falls flat, not carried round into the ground with the pitching rider
        self.rot('weapon', 52 * fall)
        self.place(dx=0.04 * fall)


class MachineAnim(CreatureAnim):
    """Wheels roll as it moves (a painted wheel layer turns about its hub), the throwing arm or bolt draws and
    looses with a recoil, the crew braces; in death the frame lurches over and settles."""
    body_bone = 'chassis'
    contact_bone = 'arm'

    def __init__(self, *a, **kw):
        super().__init__(*a, **kw)
        if 'arm' not in self.pb:
            self.contact_bone = 'chassis'

    def idle(self, i, n):
        ph = math.tau * i / n
        self.rot('chassis', 0.4 * math.sin(ph))
        self.rot('crew', 1.5 * math.sin(ph + 0.8))

    speed_au = 0.0          # battle speed in armature units per second (set by build)

    def roll_cycle(self, n):
        """Ground rolled per walk cycle: whole spoke steps of the biggest wheel (the loop has no jump), as many as
        keep the game's 0.025-0.16 s per frame at the machine's speed (so the wheels turn as fast as it rolls)."""
        if not self.wheels:
            # nothing turns: the chassis' rock at an easy rate
            return max(.03 * n * self.speed_au, min(.15 * n * self.speed_au, .9 * self.speed_au)) \
                if self.speed_au > 0 else 0.36 * self.H
        ob, c, r = max(self.wheels, key=lambda w: w[2])
        step = r * float(ob.get('symmetry', math.tau))
        if self.speed_au <= 0:
            return step * max(1, round(0.36 * self.H / step))
        lo, hi = .03 * n * self.speed_au, .155 * n * self.speed_au
        m = max(1, math.ceil(lo / step))
        while step * (m + 1) <= min(hi, 1.0 * self.speed_au) and step * m < .7 * self.speed_au:
            m += 1                                              # toward a cycle of about 0.7-1 s
        return step * m

    def roll(self, dist, cycle=None):
        for ob, c, r in self.wheels:
            ang = -dist / max(1e-6, r)
            if cycle:
                # each wheel turns a whole number of its own spoke steps per cycle (a smaller wheel slips a hair)
                sym = float(ob.get('symmetry', math.tau))
                per = max(1, round(cycle / max(1e-6, r) / sym)) * sym
                ang = -per * dist / cycle
            if 'rest_co' in ob:
                # turned in the wheel's own plane: the painted ellipse squashed to a circle, turned, stretched back
                # (turned flat in the picture an ellipse would wobble)
                a, b = ob['axes']
                co = np.array(ob['rest_co']).reshape(-1, 3)
                x = (co[:, 0] - c.x) / max(1e-6, a)
                z = (co[:, 2] - c.z) / max(1e-6, b)
                ca, sa = math.cos(ang), math.sin(ang)
                co2 = co.copy()
                co2[:, 0] = c.x + (x * ca - z * sa) * a
                co2[:, 2] = c.z + (x * sa + z * ca) * b
                ob.data.vertices.foreach_set('co', co2.ravel())
                ob.data.update()
                continue
            ob.rotation_euler = (0, ang, 0)      # about the view axis (Y), clockwise as it rolls forward
            # rotate about the hub: shift so the hub stays put
            rc = Vector((c.x * math.cos(ang) + c.z * math.sin(ang), 0.0, -c.x * math.sin(ang) + c.z * math.cos(ang)))
            ob.location.x, ob.location.z = c.x - rc.x, c.z - rc.z

    def walk(self, i, n):
        ph = math.tau * i / n
        D = self.roll_cycle(n)
        self.walk_stride = D
        self.roll(D * i / n, cycle=D)
        self.place(dz=0.004 * abs(math.sin(2 * ph)))
        self.rot('chassis', 0.8 * math.sin(2 * ph))
        self.rot('crew', 2.0 * math.sin(ph))

    def attack(self, i, n, c):
        tc = c / max(1, n - 1)
        if self.profile == 'siege-deploy':
            keys = [(0, {}), (tc * 0.7, {'tilt': 3}), (tc, {'tilt': -5, 'dx': 0.02}), (min(1, tc + 0.3), {'tilt': -1}), (1, {})]
        else:
            keys = [(0, {}), (tc * 0.8, {'pull': 1.0, 'tilt': 1.5, 'sq': 0.02}),
                    (tc, {'pull': -0.6, 'tilt': -4.0, 'dx': -0.05, 'sq': -0.03}),
                    (min(1, tc + 0.3), {'pull': 0.0, 'tilt': 1.0, 'dx': -0.02}), (1, {})]
        p = B.ease_keys(keys, i / max(1, n - 1))
        self.place(dx=p.get('dx', 0.0), sx=1.0 + p.get('sq', 0.0), sy=1.0 - p.get('sq', 0.0))
        self.rot('chassis', p.get('tilt', 0.0))
        self.roll(p.get('dx', 0.0) * self.H)        # the wheels turn back with the recoil
        if 'arm' in self.pb:
            self.pb['arm'].location = (0.0, -0.08 * self.H * p.get('pull', 0.0), 0.0)
        self.rot('crew', 6 * p.get('pull', 0.0))

    def cast(self, i, n, hold_from=6):
        """Cranked back and braced: the arm drawn, the frame settling on its wheels."""
        e, b = self.cast_curve(i, n, hold_from)
        if 'arm' in self.pb:
            self.pb['arm'].location = (0.0, -0.09 * self.H * e, 0.0)
        self.rot('chassis', 1.5 * e + .4 * b)
        self.rot('crew', 10 * e + 2 * b)

    def hit(self, i, n):
        k = 1.0 if i == 0 else 0.45
        self.rot('chassis', 2.5 * k)
        self.place(dx=-0.012 * k)

    def death(self, i, n, impact, forward):
        t, ti, fall, after, bounce = self.death_timing(i, n, impact)
        # a wheel gives: the frame lurches, tips hard onto its broken side and settles low, the crew slumps
        jolt = math.sin(min(1.0, t / max(1e-6, ti * 0.4)) * math.pi) * (1 - fall)
        self.rot('chassis', (-1 if forward else 1) * (-26 * fall + 4 * jolt) + 3 * bounce)
        self.rot('arm', -25 * fall)
        self.rot('crew', -32 * fall)
        self.place(dz=-0.05 * fall, sy=1.0 - 0.08 * fall, sx=1.0 + 0.04 * fall)
