"""Garments grown from the body sculpt (rk/hero.py): shirts, gambesons, trousers, tabards, belts, boots,
bracers, scarves and capes in the roster sheet's chunky animated-fantasy style.

Every piece is a signed-distance shell: the body (or a subset of its parts) offset by the garment's
thickness, clipped by flat limb cuts and regions, with sculpted folds. Hanging cloth (skirts, tabard
tails, capes) is grown from simple drape volumes so it bridges the legs instead of wrapping each one.
Pieces are bound with {'sdf': True} (weights from the body sculpt), optionally adjusted per vertex.
"""
import math

import numpy as np
from mathutils import Matrix, Vector

from . import sdf as F
from . import sigil


def _np(v):
    return np.asarray(tuple(v), dtype=np.float64)


def _unit(v):
    v = _np(v)
    return v / max(1e-9, np.linalg.norm(v))


# ------------------------------------------------------------------ cut and region primitives
def beyond(p, axis, r, length=1.2):
    """Removes everything past the plane through p (normal `axis`), within radius r of the axis line."""
    p, ax = _np(p), _unit(axis)
    far = p + ax * length

    def fn(P):
        q = P - p
        along = q @ ax
        radial = np.linalg.norm(q - along[:, None] * ax, axis=1)
        return np.maximum(np.maximum(-along, radial - r), along - length)
    lo = np.minimum(p, far) - r
    hi = np.maximum(p, far) + r
    return F.Fn(fn, lo, hi)


def slab(z0, z1, r=1.0, cx=0.0, cy=0.0):
    """Horizontal slab z0..z1 within radius r of a vertical axis (negative inside)."""
    def fn(P):
        return np.maximum(np.maximum(z0 - P[:, 2], P[:, 2] - z1), np.hypot(P[:, 0] - cx, P[:, 1] - cy) - r)
    return F.Fn(fn, (cx - r, cy - r, z0), (cx + r, cy + r, z1))


def offset(field, t, pad=0.12):
    lo, hi = field.bounds(pad)
    if callable(t):
        return F.Fn(lambda P: field.eval(P) - t(P), lo, hi)
    return F.Fn(lambda P: field.eval(P) - t, lo, hi)


def fold_disp(center, freq=9.0, depth=0.008, z_top=1.2, z_bottom=0.5, seed=1, power=1.2):
    """Vertical drape folds around a vertical axis through `center`, deepening downward."""
    c = _np(center)

    def disp(P):
        a = np.arctan2(P[:, 1] - c[1], P[:, 0] - c[0])
        k = np.clip((z_top - P[:, 2]) / max(1e-6, z_top - z_bottom), 0, 1) ** power
        wob = F.noise3(P, 3.0, seed, 1) * 1.4
        return depth * k * np.sin(a * freq + wob)
    return disp


def ring_disp(a, b, freq=40.0, depth=0.004, t0=0.0, t1=1.0, seed=2):
    """Compression rings along a limb (sleeve bunching, boot creases)."""
    a, b = _np(a), _np(b)
    ab = b - a
    L2 = max(float(ab @ ab), 1e-6)

    def disp(P):
        t = ((P - a) @ ab) / L2
        on = ((t > t0) & (t < t1)).astype(np.float64)
        wob = F.noise3(P, 8.0, seed, 1)
        wob2 = F.noise3(P, 3.0, seed + 7, 1)
        return depth * on * np.sin(t * freq + wob * 3.0 + wob2 * 2.0) * (0.55 + 0.45 * wob)
    return disp


def quilt_disp(depth=0.004, scale=28.0):
    """Diamond quilting for gambesons."""
    def disp(P):
        a = (P[:, 2] + P[:, 1]) * scale
        b = (P[:, 2] - P[:, 1]) * scale
        return -depth * (np.abs(np.sin(a)) ** 6 + np.abs(np.sin(b)) ** 6)
    return disp


def combine(*ds):
    ds = [d for d in ds if d is not None]
    if not ds:
        return None
    return lambda P: sum(d(P) for d in ds)


# ------------------------------------------------------------------ weights
def skirt_weights(J, top_z=None):
    """Hanging cloth below the hips follows the hips, drifting toward the near thigh lower down."""
    pz = J['pelvis'].z if top_z is None else top_z

    def adjust(V, W):
        depth = np.clip((pz - V[:, 2]) / 0.45, 0, 1)
        hips = 1 - depth * 0.55
        right = np.clip(0.5 - V[:, 1] / 0.3, 0, 1)
        out = {k: v * (1 - depth) for k, v in W.items()}
        below = V[:, 2] < pz
        out = {k: np.where(below, v, W[k]) for k, v in out.items()}
        add = {'hips': hips * depth, 'thigh.R': depth * 0.45 * right, 'thigh.L': depth * 0.45 * (1 - right)}
        for k, v in add.items():
            out[k] = out.get(k, 0) + np.where(below, v, 0)
        return out
    return adjust


def only(bones):
    """Restrict weights to the given bones (renormalised)."""
    def adjust(V, W):
        out = {k: v for k, v in W.items() if k in bones}
        if not out:
            out = {bones[0]: np.ones(len(V))}
        return out
    return adjust


# ------------------------------------------------------------------ the kit
class Kit:
    """Garment builder bound to one character (needs ch.body_field from hero.build_body)."""

    def __init__(self, ch, M, h=0.006):
        self.ch, self.M = ch, M
        self.s, self.coll = ch.s, ch.coll
        self.posed = bool(getattr(ch, 'posed', False))
        # dressing in the stance (Character.dress) or, for statues and props, in the rest pose
        self.J = ch.Jp if self.posed else ch.J
        self.body = ch.posed_field if self.posed else ch.body_field
        self.h = h

    def base(self, groups):
        sub = self.body.subset(groups)
        sub.reach = 0.16
        return sub

    def put(self, obj, adjust=None, sigma=0.03):
        self.ch.parts.append((obj, {'sdf': True, 'adjust': adjust, 'sigma': sigma, 'posed': self.posed}))
        return obj

    def rigid(self, obj, bone):
        self.ch.parts.append((obj, {'rigid_posed': bone} if self.posed else bone))
        return obj

    def mat(self, m):
        return self.M[m] if isinstance(m, str) else m

    # ------------------------------------------------------- limb geometry
    def limb(self, side, seg):
        J = self.J
        a, b = {'upper': ('shoulder', 'elbow'), 'fore': ('elbow', 'wrist'), 'thigh': ('hip', 'knee'),
                'shin': ('knee', 'ankle'), 'foot': ('ankle', 'toe')}[seg]
        return J[a + '.' + side], J[b + '.' + side]

    def limb_point(self, side, seg, t):
        a, b = self.limb(side, seg)
        return a.lerp(b, t), (b - a).normalized()

    def drape(self, z_top, z_bottom, r_top, r_bottom, x=0.0, flat=0.82, front=0.0):
        """A hanging volume around the hips and legs (elliptical cone), for skirts and tails."""
        pel = self.J['pelvis']
        a = Vector((pel.x + x, 0, z_top))
        b = Vector((pel.x + x + front, 0, z_bottom))
        return F.RoundCone(a, b, r_top, r_bottom, squash=(1.0 / flat, 1.0), up=(1, 0, 0))

    # ------------------------------------------------------- garments
    def shirt(self, mat, sleeves='wrist', t=0.01, hem=None, collar=0.0, folds=1.0, roll=None, loose=0.0,
              neck_cut=0.05, name='Shirt'):
        """Shirt or tunic body. sleeves: wrist | forearm | elbow | short | none. roll: material for rolled cuffs."""
        J, s = self.J, self.s
        groups = ['torso', 'hips', 'neck'] + (['arm'] if sleeves != 'none' else [])
        base = self.base(groups)
        f = F.Field(name)
        tt = (lambda P: t + loose * np.clip((J['chest'].z + 0.1 - P[:, 2]) / 0.4, 0, 1) * 0.03) if loose else t
        f.add(offset(base, tt), 0.0)
        cuts = []
        sleeve_t = {'wrist': 0.92, 'forearm': 0.55, 'elbow': 0.08, 'short': -0.55, 'none': None}[sleeves]
        disp = []
        for side in ('R', 'L'):
            if sleeve_t is None:
                p, ax = self.limb_point(side, 'upper', 0.0)
                f.cut(beyond(p + ax * 0.02, ax, 0.16), 0.01)
                continue
            if sleeve_t >= 0:
                p, ax = self.limb_point(side, 'fore', sleeve_t)
            else:
                p, ax = self.limb_point(side, 'upper', 1 + sleeve_t)
            f.cut(beyond(p, ax, 0.14), 0.004)
            cuts.append((side, p, ax))
            if folds:
                a, b = self.limb(side, 'upper')
                disp.append(ring_disp(a, self.J['wrist.' + side], 36.0, 0.0035 * folds, 0.3, 1.0))
        # neckline
        nk = J['neck']
        f.cut(beyond(nk + Vector((0.0, 0, neck_cut)), Vector((0.08, 0, 1)), 0.2), 0.01)
        if hem is not None:
            f.cut(F.Fn(lambda P, z=hem: P[:, 2] - z, (-1.5, -1.5, -0.2), (1.5, 1.5, hem)), 0.004)
        if folds:
            disp.append(fold_disp(J['pelvis'], 7.0, 0.004 * folds, J['chest'].z, J['pelvis'].z - 0.2, seed=3))
        f.disp = combine(*disp)
        obj = f.mesh(self.h, self.coll, self.mat(mat), name=name)
        self.put(obj)
        out = [obj]
        if roll is not None:
            for side, p, ax in cuts:
                out.append(self.cuff(side, p, ax, roll, t))
        if collar:
            out.append(self.collar_roll(mat, t, collar))
        return out

    def cuff(self, side, p, ax, mat, t=0.01, r=None, width=0.03):
        """Rolled sleeve cuff: a soft torus around the limb at p."""
        r = r if r is not None else self._limb_radius(p) + t + 0.006
        R = _frame_z(ax)
        f = F.Field('Rolled cuff')
        f.add(F.Torus(p, r, width * 0.6, R, squash=1.4), 0.0)
        f.disp = lambda P: F.noise3(P, 25.0, 4, 1) * 0.003
        obj = f.mesh(self.h * 0.7, self.coll, self.mat(mat), name='Rolled cuff')
        self.put(obj)
        return obj

    def _limb_radius(self, p):
        """Approximate body thickness at p: distance from p to the body surface along a few rays."""
        P0 = _np(p)
        dirs = np.array([[1, 0, 0], [-1, 0, 0], [0, 1, 0], [0, -1, 0], [0.7, 0.7, 0], [-0.7, -0.7, 0]], float)
        best = []
        for d in dirs:
            lo, hi = 0.0, 0.3
            for _ in range(24):
                m = (lo + hi) / 2
                if self.body.eval((P0 + d * m)[None, :])[0] < 0:
                    lo = m
                else:
                    hi = m
            best.append(lo)
        return float(np.median(best))

    def collar_roll(self, mat, t, size=1.0):
        J = self.J
        nk = J['neck'] + Vector((0.0, 0, 0.03))
        f = F.Field('Collar')
        f.add(F.Torus(nk, 0.1 * size + t, 0.025 * size, None, squash=1.2), 0.0)
        obj = f.mesh(self.h, self.coll, self.mat(mat), name='Collar')
        self.put(obj)
        return obj

    def trousers(self, mat, t=0.012, bottom=0.45, baggy=0.0, waist=None, folds=1.0, name='Trousers'):
        """Legs and seat. bottom: fraction down the shin where the trousers end (tucked into boots)."""
        J = self.J
        base = self.base(['hips', 'leg'])
        f = F.Field(name)

        def tt(P):
            k = np.clip((J['knee.R'].z + 0.15 - np.abs(P[:, 2] - J['knee.R'].z - 0.15)) / 0.3, 0, 1)
            return t + baggy * 0.025 * k
        f.add(offset(base, tt if baggy else t), 0.0)
        wz = (J['pelvis'].z + 0.12) if waist is None else waist
        f.cut(F.Fn(lambda P: wz - P[:, 2], (-1.5, -1.5, wz), (1.5, 1.5, wz + 1.2)), 0.005)
        disp = []
        for side in ('R', 'L'):
            p, ax = self.limb_point(side, 'shin', bottom)
            f.cut(beyond(p, ax, 0.14), 0.004)
            a, b = self.limb(side, 'thigh')
            if folds:
                disp.append(ring_disp(J['knee.' + side] + Vector((0, 0, 0.12)), p, 17.0, 0.003 * folds * (1 + baggy),
                                      0.0, 1.0, seed=5 if side == 'R' else 6))
        f.disp = combine(*disp)
        obj = f.mesh(self.h, self.coll, self.mat(mat), name=name)
        self.put(obj)
        return [obj]

    def gambeson(self, mat, t=0.022, skirt=0.42, sleeves='wrist', quilt=True, split=True, collar=True,
                 flare=0.08, name='Gambeson'):
        """Padded coat: quilted torso and sleeves with a skirt to mid-thigh (split at the front)."""
        J = self.J
        base = self.base(['torso', 'hips', 'neck'] + (['arm'] if sleeves != 'none' else []))
        f = F.Field(name)
        f.add(offset(base, t), 0.0)
        pz = J['pelvis'].z
        hem = pz - skirt
        legs = self.base(['leg'])
        lo_, hi_ = legs.bounds(0.2)
        f.add(F.Fn(lambda P: legs.eval(P) - t - flare * np.clip((pz - P[:, 2]) / 0.4, 0, 1), lo_, hi_), 0.06)
        f.cut(F.Fn(lambda P: P[:, 2] - hem, (-1.5, -1.5, -0.2), (1.5, 1.5, hem)), 0.006)
        if split:
            px = J['pelvis'].x
            f.cut(F.Box(Vector((px + 0.35, 0, hem + 0.12)), (0.2, 0.025, 0.16), r=0.01), 0.01)
        sleeve_t = {'wrist': 0.9, 'forearm': 0.6, 'elbow': 0.08, 'short': -0.5}.get(sleeves)
        disp = [quilt_disp(0.0045) if quilt else None]
        for side in ('R', 'L'):
            if sleeve_t is None:
                p, ax = self.limb_point(side, 'upper', 0.0)
                f.cut(beyond(p + ax * 0.03, ax, 0.17), 0.01)
                continue
            p, ax = (self.limb_point(side, 'fore', sleeve_t) if sleeve_t >= 0 else
                     self.limb_point(side, 'upper', 1 + sleeve_t))
            f.cut(beyond(p, ax, 0.15), 0.004)
        nk = J['neck']
        f.cut(beyond(nk + Vector((0.0, 0, 0.03)), Vector((0.1, 0, 1)), 0.2), 0.012)
        disp.append(fold_disp(J['pelvis'], 8.0, 0.006, pz, hem, seed=4))
        f.disp = combine(*disp)
        obj = f.mesh(self.h, self.coll, self.mat(mat), name=name)
        self.put(obj, skirt_weights(J))
        out = [obj]
        if collar:
            out.append(self.collar_roll(mat, t, 1.05))
        return out

    def leg_skirt(self, mat, length=0.55, t=0.02, flare=0.05, top=None, tatter=0.0, split=0.0, folds=1.0, seed=7,
                  name='Tunic skirt', back_long=0.0):
        """Short skirt that follows the thighs (a gambeson or tunic skirt): the hips and thighs grown
        outward, flaring toward a hem `length` of the way down each thigh."""
        J = self.J
        base = self.base(['hips', 'leg'])
        pz = J['pelvis'].z
        z0 = (pz + 0.06) if top is None else top
        legs = [(_np(J['hip.' + sd]), _np(J['knee.' + sd])) for sd in ('R', 'L')]
        rng = np.random.default_rng(seed)
        ph = rng.uniform(0, 6.28, 4)

        def along(P):
            best = np.full(len(P), 9.0)
            par = np.zeros(len(P))
            for a, b in legs:
                ab = b - a
                tt = np.clip(((P - a) @ ab) / float(ab @ ab), -0.5, 1.5)
                d = np.linalg.norm(P - (a + np.clip(tt, 0, 1)[:, None] * ab), axis=1)
                take = d < best
                best = np.where(take, d, best)
                par = np.where(take, tt, par)
            return par

        def tt(P):
            k = np.clip((z0 - P[:, 2]) / 0.45, 0, 1)
            return t + flare * k ** 1.3
        fd = fold_disp(J['pelvis'], 11.0, 0.007 * folds, z0, pz - 0.4, seed=seed)
        lo, hi = base.bounds(0.2)
        f = F.Field(name)
        f.add(F.Fn(lambda P: base.eval(P) - tt(P) + fd(P), lo, hi), 0.0)

        def keep(P):
            a = np.arctan2(P[:, 1], P[:, 0] - J['pelvis'].x)
            hem = length + (tatter * 0.08 * (np.abs(np.sin(a * 7 + ph[0])) ** 0.5 + 0.5 * np.abs(np.sin(a * 13 + ph[1]))))
            hem = hem + back_long * np.clip(-np.cos(a), 0, 1)
            k = np.maximum((along(P) - hem) * 0.4, P[:, 2] - z0)
            return k
        f.keep(F.Fn(keep, lo, hi), 0.004)
        if split:
            px = J['pelvis'].x
            f.cut(F.Box(Vector((px + 0.35, 0, pz - 0.1 - split / 2)), (0.25, 0.02, split / 2 + 0.06), r=0.01), 0.01)
        f.cut(offset(base, t * 0.5), 0.004)
        obj = f.mesh(self.h, self.coll, self.mat(mat), name=name)
        self.put(obj, skirt_weights(J))
        return [obj]

    def loincloth(self, mat, length=0.42, width=0.15, front=True, back=True, sides=0, t=None, tatter=1.0, seed=11,
                  top=None, name='Loincloth'):
        """Hanging cloth flaps from the belt: tattered front and back panels (and narrow side strips)."""
        J, s = self.J, self.s
        pz = J['pelvis'].z
        z_top = (pz + 0.1) if top is None else top
        z_hem = pz - length
        t = (0.035 + 0.03 * s.get('belly', 0)) if t is None else t
        torso = self.base(['hips'])
        hip_r = 0.19 * s['bulk'] * s['waist'] + 0.035 + 0.05 * s.get('belly', 0)
        vol = self.drape(pz + 0.06, z_hem - 0.12, hip_r, hip_r + 0.05)
        support = F.Field('Loincloth support')
        support.reach = 0.2
        support.ops = list(torso.ops) + [('add', vol, 0.08, None, None)]
        support.groups = [None] * len(support.ops)
        cx = J['pelvis'].x
        rng = np.random.default_rng(seed)
        strips = rng.uniform(0.0, 1.0, 64)

        def hem(y, w):
            u = (y / w + 1) * 0.5 * 5.0
            i = np.clip(np.floor(u), 0, 63).astype(int)
            fr = u - np.floor(u)
            notch = np.clip((np.abs(fr - 0.5) - 0.38) / 0.12, 0, 1)
            return z_hem + tatter * (0.09 * strips[i] + 0.09 * notch)
        panels = []
        if front:
            panels.append(('front', 1, 0.0, width))
        if back:
            panels.append(('back', -1, 0.0, width * 1.05))
        for k in range(sides):
            for sy in (-1, 1):
                panels.append((f'strip {k}', 0, sy * (0.6 + 0.25 * k), 0.045))
        out = []
        lo, hi = support.bounds(0.25)
        for label, sign, ang, w in panels:
            fd = fold_disp(J['pelvis'], 4.0, 0.006, pz, z_hem, seed=seed + len(out))
            surf = lambda P, fd=fd: support.eval(P) - t + fd(P)

            def region(P, sign=sign, ang=ang, w=w):
                if sign:
                    side = np.abs(P[:, 1]) - w * (1 + 0.15 * np.clip((pz - P[:, 2]) / 0.4, 0, 1))
                    facing = -sign * (P[:, 0] - cx) + 0.03
                    yy = P[:, 1]
                else:
                    a = np.arctan2(P[:, 1], P[:, 0] - cx)
                    r = np.hypot(P[:, 0] - cx, P[:, 1])
                    side = np.abs(a - ang * np.pi) * r - w
                    facing = 0.05 - r
                    yy = (a - ang * np.pi) * r
                return np.maximum.reduce([side, facing, P[:, 2] - z_top, hem(yy, w) - P[:, 2]])
            f = F.Field(f'{name} {label}')
            f.add(F.Fn(lambda P, surf=surf: np.abs(surf(P)) - 0.007, lo, hi), 0.0)
            f.keep(F.Fn(region, lo, hi), 0.004)
            obj = f.mesh(self.h * 0.8, self.coll, self.mat(mat), name=f'{name} {label}')
            self.put(obj, skirt_weights(J))
            out.append(obj)
        return out

    def skirt(self, mat, top=None, length=0.45, r_top=None, flare=0.1, t=0.01, folds=1.0, tatter=0.0, split=0.0,
              seed=3, name='Skirt', front=0.02, back_long=0.0):
        """Hanging skirt or robe tail from the waist (bridges the legs)."""
        J = self.J
        pz = J['pelvis'].z
        z0 = pz + 0.1 if top is None else top
        hem = pz - length
        r0 = (0.2 * self.s['bulk'] * self.s['waist'] + t) if r_top is None else r_top
        f = F.Field(name)
        vol = self.drape(z0 + 0.12, hem - 0.4, r0 * 0.55, r0 + flare * 1.6, front=front)
        folds_fn = fold_disp(J['pelvis'], 9.0, 0.012 * folds, z0, hem, seed=seed)
        lo_, hi_ = vol.bbox()
        # the drape grows out of the waist (no gap between body and cloth at the top, seen from above)
        waist = self.base(['torso', 'hips'])
        support = F.Field(name + ' support')
        support.reach = 0.2
        support.ops = [(m, F.Fn(lambda P, p_=p_: p_.d(P) - 0.022, *p_.bbox()) if m == 'add' else p_, k, tg, dsp)
                       for m, p_, k, tg, dsp in waist.ops] + [('add', vol, 0.12, None, None)]
        support.groups = [None] * len(support.ops)
        f.add(F.Fn(lambda P: np.abs(support.eval(P) + folds_fn(P)) - t, lo_ - 0.03, hi_ + 0.03), 0.0)
        f.cut(F.Fn(lambda P: np.maximum(z0 - P[:, 2], np.hypot(P[:, 0], P[:, 1]) - 0.6), (-0.6, -0.6, z0), (0.6, 0.6, z0 + 0.5)),
              0.005)
        rng = np.random.default_rng(seed)
        ph = rng.uniform(0, 6.28, 4)

        def hem_fn(P):
            a = np.arctan2(P[:, 1], P[:, 0] - J['pelvis'].x)
            zz = hem - back_long * np.clip(-np.cos(a), 0, 1)
            if tatter:
                jag = (np.abs(np.sin(a * 7 + ph[0])) ** 0.5 * 0.6 + np.abs(np.sin(a * 13 + ph[1])) * 0.4)
                zz = zz + tatter * 0.07 * jag
            return P[:, 2] - zz
        # everything of the drape below the hem goes, wherever it reaches
        f.cut(F.Fn(hem_fn, (lo_[0] - 0.1, lo_[1] - 0.1, lo_[2] - 0.1), (hi_[0] + 0.1, hi_[1] + 0.1, hem + 0.2 + tatter * 0.1)),
              0.004)
        if split:
            f.cut(F.Box(Vector((J['pelvis'].x + 0.4, 0, hem + split / 2)), (0.25, 0.02, split / 2 + 0.02), r=0.01), 0.008)
        obj = f.mesh(self.h, self.coll, self.mat(mat), name=name)
        self.put(obj, skirt_weights(J))
        return [obj]

    def tabard(self, mat, trim=None, emblem='lantern', emblem_mat=None, width=0.17, top=None, length=None,
               hem='tattered', back=True, t=None, flare=0.05, seed=5, glow_mat=None, trim_w=0.022, name='Tabard'):
        """Heraldic panel hanging from the chest past the belt, with a trimmed border and embroidered emblem.

        Panels follow the chest, then hang straight in front of the legs on a drape volume."""
        J, s = self.J, self.s
        pz = J['pelvis'].z
        z_top = (J['chest'].z + 0.2) if top is None else top
        z_hem = (J['knee.R'].z + 0.05) if length is None else length
        t = (0.035 * s['bulk']) if t is None else t
        torso = self.base(['torso', 'hips'])
        hip_r = 0.2 * s['bulk'] * s['waist'] + 0.04 + 0.05 * s['belly']
        vol = self.drape(pz + 0.08, z_hem - 0.1, hip_r, hip_r + flare)
        support = F.Field('Tabard support')
        support.reach = 0.2
        support.ops = list(torso.ops) + [('add', vol, 0.08, None, None)]
        support.groups = [None] * len(support.ops)
        rng = np.random.default_rng(seed)
        ph = rng.uniform(0, 6.28, 6)
        cx = J['pelvis'].x

        def half_width(z):
            k = np.clip((z_top - z) / max(0.1, z_top - z_hem), 0, 1)
            return width * s['bulk'] * (0.92 + 0.18 * k)

        def hem_line(y):
            u = y / (width * s['bulk'])
            if hem == 'tattered':
                return z_hem + 0.05 * (np.abs(np.sin(u * 7.0 + ph[0])) ** 0.6) + 0.03 * np.abs(np.sin(u * 13 + ph[1]))
            if hem == 'dagged':
                return z_hem + 0.04 * (0.5 + 0.5 * np.cos(u * np.pi * 4))
            if hem == 'point':
                return z_hem - 0.08 * np.clip(1 - np.abs(u), 0, 1)
            if hem == 'swallow':
                return z_hem + 0.09 * np.clip(1 - np.abs(u) * 1.6, 0, 1)
            return np.full_like(y, z_hem)

        def region(P, sign):
            # front (sign 1) or back (sign -1) panel footprint; negative inside, metres
            side = np.abs(P[:, 1]) - half_width(P[:, 2])
            facing = -sign * (P[:, 0] - cx) + 0.02
            top_d = P[:, 2] - z_top
            bot_d = hem_line(P[:, 1]) - P[:, 2]
            return np.maximum.reduce([side, facing, top_d, bot_d])

        out = []
        for sign, label in ((1, 'front'), (-1, 'back')):
            if sign < 0 and not back:
                continue
            f = F.Field(f'{name} {label}')
            lo, hi = support.bounds(0.2)
            fd = fold_disp(J['pelvis'], 5.0, 0.007, pz, z_hem, seed=seed + sign)
            surf = lambda P, fd=fd: support.eval(P) - t + fd(P)
            shell = F.Fn(lambda P, surf=surf: np.abs(surf(P)) - 0.006, lo, hi)
            f.add(shell, 0.0)
            f.keep(F.Fn(lambda P, sg=sign: region(P, sg), lo, hi), 0.004)
            obj = f.mesh(self.h * 0.8, self.coll, self.mat(mat), name=f'{name} {label}')
            self.put(obj, skirt_weights(J))
            out.append(obj)
            if trim is not None:
                tf = F.Field(f'{name} trim {label}')
                tf.add(F.Fn(lambda P, surf=surf: np.abs(surf(P)) - 0.0095, lo, hi), 0.0)
                tf.keep(F.Fn(lambda P, sg=sign: np.abs(region(P, sg) + trim_w * 0.5) - trim_w * 0.5, lo, hi), 0.003)
                tobj = tf.mesh(self.h * 0.6, self.coll, self.mat(trim), name=f'{name} trim')
                self.put(tobj, skirt_weights(J))
                out.append(tobj)
            if emblem and sign > 0:
                ez = (J['chest'].z + J['pelvis'].z) * 0.5 - 0.02 if z_hem < pz - 0.2 else J['chest'].z
                out += self.emblem(support, t + 0.006, emblem, Vector((cx, 0, ez)), 0.15 * s['bulk'],
                                   emblem_mat or trim, glow_mat)
                if z_hem < pz - 0.25:
                    out += self.emblem(support, t + 0.006, emblem, Vector((cx, 0, z_hem + 0.17)), 0.1 * s['bulk'],
                                       emblem_mat or trim, glow_mat)
        return out

    def emblem(self, support, t, kind, center, size, mat, glow_mat=None, facing=(1, 0, 0)):
        """Embroidered emblem: the 2D sigil extruded through a thin band just above `support` offset by t."""
        shape = sigil.EMBLEMS[kind]() if isinstance(kind, str) else kind
        fx = _unit(facing)
        c = _np(self._surface_point(center, Vector(tuple(fx)), support, t))
        lo, hi = c - size * 1.6, c + size * 1.6
        c_back = c - fx * 0.03

        def fn(P):
            u = (P[:, 1] - c[1]) / size * (1 if fx[0] >= 0 else -1)
            v = (P[:, 2] - c[2]) / size
            d2 = shape(u, v) * size
            band = np.abs(support.eval(P) - t) - 0.0035
            front = -(P - c_back) @ fx
            return np.maximum(np.maximum(d2, band), front)
        f = F.Field('Emblem')
        f.add(F.Fn(fn, lo, hi), 0.0)
        obj = f.mesh(min(self.h, size * 0.035), self.coll, self.mat(mat), name='Emblem', smooth=2)
        self.put(obj, skirt_weights(self.J))
        out = [obj]
        if glow_mat is not None and kind == 'lantern':
            flame = sigil.lantern_flame()
            t = t - 0.0015

            def gfn(P):
                u = (P[:, 1] - c[1]) / size
                v = (P[:, 2] - c[2]) / size
                return np.maximum(np.maximum(flame(u, v) * size, np.abs(support.eval(P) - t) - 0.0045),
                                  -(P - c_back) @ fx)
            g = F.Field('Emblem flame')
            g.add(F.Fn(gfn, lo, hi), 0.0)
            gobj = g.mesh(min(self.h, size * 0.03), self.coll, self.mat(glow_mat), name='Emblem flame', smooth=1)
            self.put(gobj, skirt_weights(self.J))
            out.append(gobj)
        return out

    def belt(self, mat, buckle='trim', z=None, width=0.065, t=None, pouches=(), strap=None, studs=None,
             name='Belt'):
        """Wide belt around the waist (over everything worn so far) with a buckle and pouches.
        pouches: list of (angle_deg, size) around the waist (0 = front, -90 = near/right side)."""
        J, s = self.J, self.s
        zc = (J['pelvis'].z + 0.08) if z is None else z
        base = self.base(['hips', 'torso'])
        t = (0.045 + 0.04 * s['belly']) if t is None else t
        f = F.Field(name)
        f.add(offset(base, t), 0.0)
        f.keep(slab(zc - width / 2, zc + width / 2, 0.6), 0.006)
        f.cut(offset(base, t - 0.018), 0.003)
        obj = f.mesh(self.h * 0.7, self.coll, self.mat(mat), name=name)
        self.put(obj, only(['hips', 'spine']))
        out = [obj]
        front = self._surface_point(Vector((J['pelvis'].x, 0, zc)), Vector((1, 0, 0)), base, t)
        if buckle:
            bf = F.Field('Buckle')
            bf.add(F.Box(front + Vector((0.008, 0, 0)), (0.012, 0.045, 0.04), r=0.008), 0.0)
            bf.cut(F.Box(front + Vector((0.02, 0, 0)), (0.02, 0.026, 0.022), r=0.004), 0.002)
            bobj = bf.mesh(0.003, self.coll, self.mat(buckle), name='Buckle')
            self.rigid(bobj, 'hips')
            out.append(bobj)
            tongue = F.Field('Belt tongue')
            tongue.add(F.Box(front + Vector((0.012, 0.0, 0.0)), (0.006, 0.006, 0.026), r=0.002), 0.0)
            out.append(self.rigid(tongue.mesh(0.002, self.coll, self.mat(mat), name='Belt tongue'), 'hips'))
        for ang, size in pouches:
            out += self.pouch(ang, size, zc - width * 0.4, base, t)
        if studs:
            pass
        return out

    def _surface_point(self, origin, direction, field, t=0.0):
        P0, d = _np(origin), _unit(direction)
        lo, hi = 0.0, 0.6
        for _ in range(30):
            m = (lo + hi) / 2
            if field.eval((P0 + d * m)[None, :])[0] - t < 0:
                lo = m
            else:
                hi = m
        return Vector(tuple(P0 + d * lo))

    def pouch(self, ang, size, z, base, t, mat='leather', flap='leather_dark'):
        a = math.radians(ang)
        d = Vector((math.cos(a), math.sin(a), 0))
        p = self._surface_point(Vector((self.J['pelvis'].x, 0, z)), d, base, t)
        n = d
        R = _frame_xz(n)
        w, h, dp = 0.06 * size, 0.07 * size, 0.035 * size
        c = p + n * dp
        f = F.Field('Pouch')
        f.add(F.Box(c - Vector((0, 0, h * 0.2)), (dp, w, h), R, r=0.018 * size), 0.0)
        f.disp = lambda P: F.noise3(P, 30.0, 7, 1) * 0.0015
        body = f.mesh(0.004, self.coll, self.mat(mat), name='Pouch')
        g = F.Field('Pouch flap')
        g.add(F.Box(c + n * 0.006 + Vector((0, 0, h * 0.45)), (dp + 0.004, w + 0.004, h * 0.45), R, r=0.012 * size), 0.0)
        g.cut(F.Box(c + Vector((0, 0, -h * 0.05)), (dp * 3, w * 3, h * 0.5), R), 0.002)
        flap_o = g.mesh(0.004, self.coll, self.mat(flap), name='Pouch flap')
        bt = F.Field('Pouch button')
        bt.add(F.Sphere(c + n * (dp + 0.008) + Vector((0, 0, h * 0.05)), 0.009 * size), 0.0)
        btn = bt.mesh(0.002, self.coll, self.mat('trim'), name='Pouch button')
        for o in (body, flap_o, btn):
            self.rigid(o, 'hips')
        return [body, flap_o, btn]

    def strap(self, mat, a, b, width=0.05, t=None, buckle=None, name='Strap', groups=('torso', 'hips')):
        """Bandolier/baldric across the torso from point a to point b (both near the body surface)."""
        base = self.base(list(groups))
        t = 0.05 if t is None else t
        a, b = _np(a), _np(b)
        ab = b - a
        L = np.linalg.norm(ab)
        ax = ab / L

        def band(P):
            q = P - a
            along = q @ ax
            perp = q - along[:, None] * ax
            # keep a strip of the given width measured along the body's surface (approx: the y/z plane distance)
            side = np.abs(perp @ _unit(np.cross(ax, [1, 0, 0]) if abs(ax[0]) < 0.9 else [0, 1, 0])) - width / 2
            return np.maximum(side, np.maximum(-along - 0.2, along - L - 0.2))
        f = F.Field(name)
        f.add(offset(base, t), 0.0)
        f.cut(offset(base, t - 0.014), 0.002)
        lo, hi = base.bounds(0.2)
        f.keep(F.Fn(band, lo, hi), 0.004)
        obj = f.mesh(self.h * 0.7, self.coll, self.mat(mat), name=name)
        self.put(obj, only(['chest', 'spine', 'hips']))
        return [obj]

    def quiver(self, mat='leather', arrows=7, fletch='linen', strap='leather_dark', trim='trim', tilt=25.0):
        """A quiver slung diagonally across the back with arrows fanning out of it."""
        J = self.J
        torso = self.base(['torso'])
        back = self._surface_point(J['chest'] + Vector((0, 0, 0.0)), Vector((-1, 0, 0)), torso, 0.06)
        a = math.radians(tilt)
        axis = Vector((-0.25, math.sin(a), math.cos(a))).normalized()
        c = back + Vector((-0.06, 0, -0.02))
        f = F.Field('Quiver')
        f.add(F.RoundCone(c - axis * 0.26, c + axis * 0.2, 0.055, 0.07, squash=(1.0, 0.75), up=(1, 0, 0)), 0.0)
        f.cut(F.RoundCone(c + axis * 0.14, c + axis * 0.3, 0.05, 0.062), 0.004)
        f.disp = lambda P: F.noise3(P, 30.0, 5, 1) * 0.0015
        q = f.mesh(0.004, self.coll, self.mat(mat), name='Quiver')
        self.rigid(q, 'chest')
        out = [q]
        rim = F.Field('Quiver rim')
        rim.add(F.Torus(c + axis * 0.2, 0.066, 0.01, _frame_z(axis), squash=1.0), 0.0)
        rim.add(F.Torus(c - axis * 0.2, 0.058, 0.009, _frame_z(axis), squash=1.0), 0.0)
        o = rim.mesh(0.003, self.coll, self.mat(trim), name='Quiver rim')
        self.rigid(o, 'chest')
        out.append(o)
        sh = F.Field('Arrow shafts')
        fl = F.Field('Arrow fletching')
        rng = np.random.default_rng(3)
        for i in range(arrows):
            off = Vector((rng.uniform(-0.03, 0.03), rng.uniform(-0.035, 0.035), 0))
            d = (axis + Vector((rng.uniform(-0.12, 0.12), rng.uniform(-0.12, 0.12), 0))).normalized()
            base = c + axis * 0.05 + off
            top = base + d * 0.3
            sh.add(F.RoundCone(base, top, 0.006, 0.006), 0.0)
            for k in range(3):
                ang = k * math.tau / 3 + i
                w = _frame_z(d) @ Vector((math.cos(ang), math.sin(ang), 0))
                fl.add(F.RoundCone(top - d * 0.1 + w * 0.012, top - d * 0.02 + w * 0.016, 0.008, 0.004,
                                   squash=(1.0, 0.3), up=tuple(w.cross(d))), 0.004)
        out.append(self.rigid(sh.mesh(0.0025, self.coll, self.mat('wood'), name='Arrow shafts'), 'chest'))
        out.append(self.rigid(fl.mesh(0.0025, self.coll, self.mat(fletch), name='Arrow fletching'), 'chest'))
        return out

    def satchel(self, mat='leather', side='L', strap='leather_dark', size=1.0, flap='leather_dark'):
        """A shoulder bag at the hip with a strap across the chest to the opposite shoulder."""
        J = self.J
        sy = 1 if side == 'L' else -1
        hips = self.base(['hips'])
        p = self._surface_point(Vector((J['pelvis'].x + 0.02, 0, J['pelvis'].z - 0.02)), Vector((0.35, sy, 0)), hips, 0.06)
        n = Vector((0.35, sy, 0)).normalized()
        R = _frame_xz(n)
        S = size
        c = p + n * 0.05 * S
        f = F.Field('Satchel')
        f.add(F.Box(c, (0.05 * S, 0.12 * S, 0.1 * S), R, r=0.03 * S), 0.0)
        f.disp = lambda P: F.noise3(P, 25.0, 6, 1) * 0.002
        b = f.mesh(0.004, self.coll, self.mat(mat), name='Satchel')
        g = F.Field('Satchel flap')
        g.add(F.Box(c + n * 0.012 + Vector((0, 0, 0.04 * S)), (0.05 * S, 0.125 * S, 0.07 * S), R, r=0.025 * S), 0.0)
        g.cut(F.Box(c + Vector((0, 0, -0.05 * S)), (0.2 * S, 0.3 * S, 0.06 * S), R), 0.004)
        fo = g.mesh(0.004, self.coll, self.mat(flap), name='Satchel flap')
        k = F.Field('Satchel buckle')
        k.add(F.Box(c + n * 0.065 * S + Vector((0, 0, -0.005)), (0.006, 0.022 * S, 0.026 * S), R, r=0.004), 0.0)
        ko = k.mesh(0.002, self.coll, self.mat('trim'), name='Satchel buckle')
        for o in (b, fo, ko):
            self.put(o, only(['hips']))
        sh = J['shoulder.' + ('R' if side == 'L' else 'L')]
        out = [b, fo, ko]
        out += self.strap(strap, Vector((sh.x + 0.05, sh.y * 0.6, sh.z + 0.06)), c + Vector((0, 0, 0.08 * S)), width=0.045)
        return out

    def around(self, pts, t=0.01, groups=('torso', 'hips', 'neck')):
        """Surface points around the body: pts are (z, angle_deg) pairs (angle 0 = front, +90 = left), or
        world points (projected outward from the body axis at their height and bearing)."""
        base = self.base(list(groups))
        cx = self.J['pelvis'].x
        out = []
        for p in pts:
            if len(p) == 2:
                z, a = p
                d = Vector((math.cos(math.radians(a)), math.sin(math.radians(a)), 0))
            else:
                z = p[2]
                d = Vector((p[0] - cx, p[1], 0)).normalized()
            o = Vector((cx, 0, z))
            q = self._surface_point(o, d, base, t)
            out.append(q)
        # densify so links follow the curvature between waypoints
        dense = []
        for a, b in zip(out, out[1:]):
            for i in range(6):
                m = a.lerp(b, i / 6)
                dz = m.z
                dd = Vector((m.x - cx, m.y, 0))
                if dd.length > 1e-6:
                    dense.append(self._surface_point(Vector((cx, 0, dz)), dd.normalized(), base, t))
        dense.append(out[-1])
        return dense

    def chain(self, pts, mat='chain', link=0.045, thick=0.011, hug=None, bone=None, adjust=None, name='Chain'):
        """Interlocking iron links along a path (pts), optionally pushed onto the body surface by `hug` metres."""
        P = [Vector(p) for p in pts]
        if hug is not None:
            P = self.around(pts, hug)
        # resample evenly
        L = [0.0]
        for a, b in zip(P, P[1:]):
            L.append(L[-1] + (b - a).length)
        n = max(2, int(L[-1] / (link * 1.55)))
        f = F.Field(name)
        for i in range(n):
            d = L[-1] * (i + 0.5) / n
            k = max(j for j in range(len(L)) if L[j] <= d) if d < L[-1] else len(L) - 2
            k = min(k, len(P) - 2)
            t = (d - L[k]) / max(1e-9, L[k + 1] - L[k])
            p = P[k].lerp(P[k + 1], t)
            ax = (P[k + 1] - P[k]).normalized()
            # alternate link planes
            side = ax.cross(Vector((0, 0, 1)) if abs(ax.z) < 0.9 else Vector((1, 0, 0))).normalized()
            if i % 2:
                side = ax.cross(side).normalized()
            R = Matrix((ax, ax.cross(side).normalized(), side)).transposed()
            f.add(F.Torus(p, link * 0.5, thick, R, squash=1.0), 0.0)
        obj = f.mesh(0.003, self.coll, self.mat(mat), name=name, smooth=1)
        if bone:
            self.rigid(obj, bone)
        else:
            self.put(obj, adjust or only(['chest', 'spine', 'hips']))
        return [obj]

    def boots(self, mat, cuff=None, height=0.55, t=0.016, toe=1.0, sole='leather_dark', folds=1.0, wrap=None,
              name='Boot'):
        """Chunky boots: shaft to `height` up the shin (fraction from the ankle), big rounded toe, thick sole
        and an optional turned-down cuff."""
        J = self.J
        out = []
        for side in ('R', 'L'):
            kn, an, toe_p = J['knee.' + side], J['ankle.' + side], J['toe.' + side]
            leg = self.base(['leg.' + side, 'foot.' + side])
            f = F.Field(name)
            top = an.lerp(kn, height)
            f.add(offset(leg, t), 0.0)
            ax = (kn - an).normalized()
            f.cut(beyond(top, ax, 0.16, 0.8), 0.004)
            # chunky foot, on the foot's own ground (planted feet: z 0; riders' feet sit in the stirrups)
            z0 = an.z - self.ch.J['ankle.' + side].z
            fwd = Vector((toe_p.x - an.x, 0, 0)).normalized()
            heel = Vector((an.x - 0.055, an.y, z0 + 0.055))
            tip = Vector((toe_p.x + 0.035 * toe, toe_p.y, z0 + 0.05))
            f.add(F.RoundCone(heel, tip, 0.06, 0.058 * toe, squash=(1.18, 0.82), up=(0, 0, 1)), 0.05)
            f.add(F.Ellipsoid(Vector((an.x + 0.0, an.y, an.z + 0.02)), (0.075, 0.07, 0.08)), 0.04)
            f.cut(F.Plane(Vector((0, 0, z0 + 0.022)), (0, 0, 1)), 0.004)
            f.disp = combine(ring_disp(an, top, 22.0, 0.005 * folds, 0.15, 0.95, seed=8 if side == 'R' else 9),
                             lambda P: F.noise3(P, 18.0, 3, 1) * 0.0015)
            boot = f.mesh(self.h * 0.8, self.coll, self.mat(mat), name=f'{name}.{side}')
            self.put(boot, only(['shin.' + side, 'foot.' + side]))
            out.append(boot)
            # sole
            sf = F.Field('Sole')
            sf.add(F.RoundCone(heel + Vector((-0.012, 0, -0.025)), tip + Vector((0.012, 0, -0.022)), 0.062, 0.06 * toe,
                               squash=(1.22, 0.38), up=(0, 0, 1)), 0.0)
            sf.cut(F.Plane(Vector((0, 0, z0)), (0, 0, 1)), 0.002)
            sobj = sf.mesh(self.h * 0.7, self.coll, self.mat(sole), name=f'Sole.{side}')
            self.put(sobj, only(['foot.' + side, 'shin.' + side]))
            out.append(sobj)
            if cuff is not None:
                cf = F.Field('Boot cuff')
                r = self._limb_radius(top) + t + 0.012
                cf.add(F.RoundCone(top - ax * 0.055, top + ax * 0.01, r + 0.004, r + 0.016), 0.0)
                cf.cut(F.RoundCone(top - ax * 0.08, top + ax * 0.06, r - 0.004, r + 0.004), 0.003)
                cf.disp = lambda P: F.noise3(P, 20.0, 5, 1) * 0.002
                cobj = cf.mesh(self.h * 0.6, self.coll, self.mat(cuff), name=f'Boot cuff.{side}')
                self.put(cobj, only(['shin.' + side]))
                out.append(cobj)
            if wrap is not None:
                wf = F.Field('Shin wrap')
                lo_p = an.lerp(kn, 0.2)
                hi_p = an.lerp(kn, height + 0.1)
                r = self._limb_radius(an.lerp(kn, 0.5)) + t
                wf.add(offset(leg, t + 0.008), 0.0)
                wf.keep(F.Fn(lambda P, a=_np(lo_p), b=_np(hi_p): _between(P, a, b, 0.12), *_seg_box(lo_p, hi_p, 0.14)),
                        0.004)
                wf.disp = ring_disp(lo_p, hi_p, 60.0, 0.004, 0, 1, seed=11)
                wobj = wf.mesh(self.h * 0.6, self.coll, self.mat(wrap), name='Shin wrap')
                self.put(wobj, only(['shin.' + side]))
                out.append(wobj)
        return out

    def bracers(self, mat, start=0.35, end=0.97, t=0.014, flare=0.012, laces=None, sides=('R', 'L'), name='Bracer'):
        """Leather bracers over the forearms, flaring toward the wrist."""
        out = []
        for side in sides:
            el, wr = self.limb(side, 'fore')
            arm = self.base(['arm.' + side])
            a, b = el.lerp(wr, start), el.lerp(wr, end)
            f = F.Field(name)
            ab = _np(b - a)
            L2 = float(ab @ ab)

            def tt(P, a=_np(a), ab=ab, L2=L2):
                s_ = np.clip(((P - a) @ ab) / L2, 0, 1)
                return t + flare * s_ ** 2
            f.add(offset(arm, tt), 0.0)
            f.keep(F.Fn(lambda P, a=_np(a), b=_np(b): _between(P, a, b, 0.14), *_seg_box(a, b, 0.15)), 0.003)
            f.disp = lambda P: F.noise3(P, 30.0, 12, 1) * 0.0012
            obj = f.mesh(self.h * 0.6, self.coll, self.mat(mat), name=f'{name}.{side}')
            self.put(obj, only(['forearm.' + side]))
            out.append(obj)
            # edge rolls at both ends
            for p, extra in ((a, 0.0), (b, flare)):
                ax = (b - a).normalized()
                r = self._limb_radius(p) + t + extra + 0.002
                tor = F.Field('Bracer edge')
                tor.add(F.Torus(p, r, 0.006, _frame_z(ax)), 0.0)
                eobj = tor.mesh(0.003, self.coll, self.mat(laces or mat), name='Bracer edge')
                self.put(eobj, only(['forearm.' + side]))
                out.append(eobj)
        return out

    def scarf(self, mat, size=1.0, tail=0.25, drape=0.0, seed=6, name='Scarf'):
        """Thick cloth wound around the neck, bunching onto the shoulders, with a short tail on the chest."""
        J = self.J
        nk = J['neck'] + Vector((0.0, 0, 0.0))
        f = F.Field(name)
        rng = np.random.default_rng(seed)
        for i in range(10):
            a = i / 10 * math.tau
            r = (0.13 + 0.015 * math.sin(a * 3 + 1)) * size
            c = nk + Vector((math.cos(a) * r * 0.9 + 0.01, math.sin(a) * r * 1.08, -0.02 + 0.03 * math.cos(a * 2 + 0.5)))
            f.add(F.Sphere(c, (0.065 + 0.012 * rng.uniform()) * size), 0.05 * size)
        f.add(F.Torus(nk + Vector((0.0, 0, 0.045)), 0.105 * size, 0.04 * size, None, squash=1.0), 0.05)
        if tail:
            a = nk + Vector((0.15 * size, -0.05, -0.02))
            b = a + Vector((0.03, -0.03, -tail))
            f.add(F.RoundCone(a, b, 0.055 * size, 0.04 * size, squash=(1.6, 0.5), up=(1, 0, 0)), 0.04)
        if drape:
            f.add(F.Ellipsoid(J['chest'] + Vector((0.0, 0, 0.17)), (0.2 * size, 0.27 * size * drape, 0.07)), 0.06)
        f.cut(offset(self.base(['neck']), 0.004), 0.01)
        f.disp = lambda P: F.noise3(P, 9.0, seed, 2) * 0.006
        obj = f.mesh(self.h * 0.8, self.coll, self.mat(mat), name=name)
        self.put(obj, only(['chest', 'neck']))
        return [obj]

    def cape(self, mat, length=None, width=1.0, flare=0.12, t=0.01, tatter=0.0, collar=None, seed=4, lining=None,
             name='Cape'):
        """Back cape hanging from the shoulders on the cape bones (cape.0..3), with deep folds."""
        J, s = self.J, self.s
        top = J['neck'].z + 0.02
        bottom = 0.3 if length is None else length
        sh = abs(J['shoulder.R'].y) + 0.06
        rng = np.random.default_rng(seed)
        ph = rng.uniform(0, 6.28, 4)
        cx = J['chest'].x - 0.02
        torso = self.base(['torso', 'hips', 'neck'])

        def surface(P):
            # an open cone behind the body: radius grows downward
            k = np.clip((top - P[:, 2]) / (top - bottom), 0, 1.2)
            r_y = (sh * width) * (0.95 + 0.25 * k) + flare * k
            r_x = (0.2 * s['bulk'] + 0.06) + 0.14 * k + flare * 0.6 * k
            ell = np.sqrt(((P[:, 0] - cx) / r_x) ** 2 + (P[:, 1] / r_y) ** 2) - 1.0
            return ell * np.minimum(r_x, r_y)
        lo = np.array([cx - 0.7, -0.9, bottom - 0.2])
        hi = np.array([cx + 0.3, 0.9, top + 0.15])

        cfold = fold_disp(J['chest'] + Vector((-0.1, 0, 0)), 7.0, 0.02, top - 0.1, bottom, seed=seed, power=1.0)

        def shell(P):
            d = np.abs(surface(P) + cfold(P)) - t
            # wrap over the shoulders: never pass inside the torso
            return np.maximum(d, 0.02 - torso.eval(P))
        f = F.Field(name)
        f.add(F.Fn(shell, lo, hi), 0.0)

        def keep(P):
            behind = (P[:, 0] - cx) - 0.02 + 0.05 * np.clip((top - P[:, 2]) / 0.3, 0, 1)
            tz = P[:, 2] - top
            a = np.arctan2(P[:, 1], -(P[:, 0] - cx))
            hem = bottom + (tatter * 0.08 * (np.abs(np.sin(a * 6 + ph[0])) ** 0.5 + 0.6 * np.abs(np.sin(a * 11 + ph[1]))))
            bd = hem - P[:, 2]
            return np.maximum.reduce([behind, tz, bd])
        f.keep(F.Fn(keep, lo, hi), 0.006)
        obj = f.mesh(self.h, self.coll, self.mat(mat), name=name)

        def cape_w(V, W):
            tt = np.clip((top - V[:, 2]) / max(0.1, top - bottom), 0, 1)
            idx = tt * 3.0
            out = {}
            near = np.clip((0.12 - tt) / 0.12, 0, 1)
            for i in range(4):
                w = np.clip(1 - np.abs(idx - i), 0, 1) * (1 - near)
                out['cape.%d' % i] = w
            out['chest'] = near
            return out
        self.put(obj, cape_w)
        out = [obj]
        if collar is not None:
            cf = F.Field('Cape collar')
            nk = J['neck']
            for i in range(9):
                a = (i / 8 - 0.5) * math.radians(230)
                c = nk + Vector((-math.cos(a) * 0.14 - 0.02, math.sin(a) * 0.19, -0.03))
                cf.add(F.Sphere(c, 0.05), 0.05)
            cf.cut(offset(self.base(['neck', 'torso']), 0.01), 0.01)
            cf.disp = lambda P: F.noise3(P, 16.0, 9, 2) * 0.005
            cobj = cf.mesh(self.h, self.coll, self.mat(collar), name='Cape collar')
            self.put(cobj, only(['chest', 'neck']))
            out.append(cobj)
        return out


def _between(P, a, b, r):
    ab = b - a
    L2 = float(ab @ ab)
    t = ((P - a) @ ab) / L2
    q = P - a - t[:, None] * ab
    L = math.sqrt(L2)
    return np.maximum(np.maximum(-t * L, (t - 1) * L), np.linalg.norm(q, axis=1) - r)


def _seg_box(a, b, r):
    a, b = _np(a), _np(b)
    return np.minimum(a, b) - r, np.maximum(a, b) + r


def _frame_z(axis):
    """Rotation whose third column is `axis`."""
    z = Vector(tuple(axis)).normalized()
    x = Vector((1, 0, 0)) if abs(z.x) < 0.9 else Vector((0, 1, 0))
    y = z.cross(x).normalized()
    x = y.cross(z).normalized()
    return Matrix((x, y, z)).transposed()


def _frame_xz(n):
    """Rotation whose first column is the outward normal n and third column is up."""
    x = Vector(tuple(n)).normalized()
    z = Vector((0, 0, 1))
    y = z.cross(x).normalized()
    z = x.cross(y).normalized()
    return Matrix((x, y, z)).transposed()
