"""Plate armour in the roster-sheet style: big rounded pauldrons with stacked lames, a ridged cuirass,
faulds and tassets, couters and poleyns with fans, vambraces, greaves, sabatons and gauntlet cuffs.

Every plate is a signed-distance shell (an ellipsoid or the body sculpt grown outward), clipped to its
footprint, with a raised brass trim along its edges and rivets. Plates on one limb segment are bound
rigidly to that bone; body-hugging plates (cuirass, faulds) take sculpt weights limited to the torso.
"""
import math

import numpy as np
from mathutils import Matrix, Vector

from . import sdf as F
from .garb import _np, _unit, beyond, offset, only, skirt_weights


class Frame:
    """Local frame: origin o, columns x, y, z (world)."""

    def __init__(self, o, x, y, z=None):
        x = Vector(x).normalized()
        y = Vector(y)
        y = (y - x * y.dot(x)).normalized()
        z = x.cross(y).normalized() if z is None else Vector(z).normalized()
        self.o = Vector(o)
        self.R = Matrix((x, y, z)).transposed()
        self.Rn = np.array([[self.R[i][j] for j in range(3)] for i in range(3)])
        self.on = _np(o)

    def local(self, P):
        return (P - self.on) @ self.Rn

    def world(self, x, y, z):
        return self.o + self.R @ Vector((x, y, z))


def shell_of(dist_fn, t):
    """Thickened surface: |d| - t/2."""
    return lambda P: np.abs(dist_fn(P)) - t * 0.5


class Smith:
    """Plate builder bound to one character (uses the garment Kit for the body sculpt)."""

    def __init__(self, kit, mat='dark_steel', trim='trim', rivet='trim', h=0.004):
        self.K = kit
        self.ch = kit.ch
        self.J, self.s, self.coll = kit.J, kit.s, kit.coll
        self.mat, self.trim, self.rivet = mat, trim, rivet
        self.h = h

    def M(self, m):
        return self.K.mat(m)

    def _plate(self, name, shell, keep, lo, hi, mat=None, trim=None, trim_w=0.014, trim_t=0.004, k=0.004,
               disp=None, bind=None, h=None, trim_keep=None):
        """Mesh a plate (shell ∩ keep) and, optionally, a raised trim band along its edges."""
        out = []
        f = F.Field(name)
        f.add(F.Fn(shell, lo, hi), 0.0)
        f.keep(F.Fn(keep, lo, hi), k)
        f.disp = disp
        obj = f.mesh(h or self.h, self.coll, self.M(mat or self.mat), name=name, smooth=3)
        out.append(obj)
        if trim:
            tf = F.Field(name + ' trim')
            tf.add(F.Fn(lambda P: shell(P) - trim_t, lo, hi), 0.0)
            tf.keep(F.Fn(trim_keep if trim_keep is not None else
                         (lambda P: np.abs(keep(P) + trim_w * 0.5) - trim_w * 0.5), lo, hi), 0.002)
            tobj = tf.mesh((h or self.h) * 0.8, self.coll, self.M(trim), name=name + ' trim', smooth=2)
            out.append(tobj)
        for o in out:
            if bind is None:
                pass
            elif isinstance(bind, str):
                self.K.rigid(o, bind)
            else:
                self.K.put(o, bind)
        return out

    def rivets(self, points, r=0.008, bone=None, adjust=None):
        f = F.Field('Rivets')
        for p in points:
            f.add(F.Sphere(p, r), 0.0)
        obj = f.mesh(r * 0.35, self.coll, self.M(self.rivet), name='Rivets', smooth=1)
        if bone:
            self.K.rigid(obj, bone)
        elif adjust:
            self.K.put(obj, adjust)
        return [obj]

    def _ray(self, fn, origin, d, lo=0.0, hi=0.5):
        o, d = _np(origin), _unit(d)
        for _ in range(30):
            m = (lo + hi) / 2
            if fn((o + d * m)[None, :])[0] < 0:
                lo = m
            else:
                hi = m
        return Vector(tuple(o + d * lo))

    # ------------------------------------------------------- shoulders
    def pauldron(self, side, size=1.0, lames=2, t=0.016, trim=True, rivets=True, spikes=0, flare=1.0, drop=1.0,
                 style='round'):
        J = self.J
        sy = -1 if side == 'R' else 1
        sh, el = J['shoulder.' + side], J['elbow.' + side]
        down = (el - sh).normalized()
        out_dir = Vector((0, sy, 0))
        out_dir = (out_dir - down * out_dir.dot(down)).normalized()
        fr = Frame(sh + out_dir * 0.035 * size + down * 0.0 * size, Vector((1, 0, 0)), out_dir)
        # local: x forward, y outward, z = x × y (≈ up the arm for the right... normalise sign)
        upz = fr.R @ Vector((0, 0, 1))
        flip = 1 if upz.dot(-down) > 0 else -1
        S = size
        res = []
        # dome cap
        rx, ry, rz = 0.17 * S, 0.16 * S, 0.1 * S

        def dome_d(P):
            q = fr.local(P)
            q[:, 2] *= flip
            k0 = np.linalg.norm(q / [rx, ry, rz], axis=1)
            k1 = np.linalg.norm(q / np.array([rx * rx, ry * ry, rz * rz]), axis=1)
            return k0 * (k0 - 1) / np.maximum(k1, 1e-9)

        def dome_keep(P):
            q = fr.local(P)
            q[:, 2] *= flip
            # keep the outer, upper part: above a plane tilted down toward the outside
            return np.maximum(-(q[:, 2] + 0.05 * S + 0.035 * S * np.clip(q[:, 1] / (0.12 * S), 0, 1)),
                              -(q[:, 1] + 0.07 * S))
        lo, hi = _np(fr.o) - 0.3 * S, _np(fr.o) + 0.3 * S
        bone = 'upper_arm.' + side
        res += self._plate(f'Pauldron.{side}', shell_of(dome_d, t), dome_keep, lo, hi, trim='trim' if trim else None,
                           bind=bone, h=self.h)
        # lames below the dome, each a little wider and lower
        for i in range(1, lames + 1):
            g = 1 + 0.07 * i * flare
            off = -0.055 * i * S * drop

            def lame_d(P, g=g, off=off):
                q = fr.local(P)
                q[:, 2] *= flip
                q[:, 2] -= off
                r = np.array([rx * g, ry * g, rz * g * 1.1])
                k0 = np.linalg.norm(q / r, axis=1)
                k1 = np.linalg.norm(q / (r * r), axis=1)
                return k0 * (k0 - 1) / np.maximum(k1, 1e-9)

            def lame_keep(P, off=off):
                q = fr.local(P)
                q[:, 2] *= flip
                top = -0.03 * S + off + 0.02 * S
                bot = top - 0.07 * S
                return np.maximum.reduce([q[:, 2] - top, bot - q[:, 2], -(q[:, 1] + 0.02 * S)])
            res += self._plate(f'Pauldron lame.{side}', shell_of(lame_d, t * 0.9), lame_keep, lo - 0.1, hi + 0.1,
                               trim='trim' if trim else None, bind=bone, h=self.h)
        if rivets:
            pts = []
            for a in (-50, 0, 50):
                d = fr.R @ Vector((math.sin(math.radians(a)) * 0.8, math.cos(math.radians(a)), 0.25 * flip))
                pts.append(self._ray(lambda P: dome_d(P) - t * 0.5, fr.o, d, 0, 0.3))
            res += self.rivets(pts, 0.009 * S, bone)
        for k in range(spikes):
            base = fr.world(0.0 + (k - (spikes - 1) / 2) * 0.06, ry * 0.6, rz * 0.75 * flip)
            tip = base + (fr.R @ Vector((0, 0.35, 0.8 * flip))).normalized() * 0.09 * S
            sf = F.Field('Pauldron spike')
            sf.add(F.RoundCone(base, tip, 0.022 * S, 0.003), 0.0)
            o = sf.mesh(0.003, self.coll, self.M(self.mat), name='Pauldron spike')
            self.K.rigid(o, bone)
            res.append(o)
        return res

    # ------------------------------------------------------- torso
    def cuirass(self, t=0.06, top=None, bottom=None, ridge=True, trim=True, back=True, plackart=False, name='Cuirass'):
        J, s = self.J, self.s
        torso = self.K.base(['torso', 'hips', 'neck'])
        z0 = (J['pelvis'].z + 0.08) if bottom is None else bottom
        z1 = (J['neck'].z - 0.02) if top is None else top
        lo, hi = torso.bounds(0.15)
        surf = lambda P: torso.eval(P) - t
        nk = _np(J['neck'])
        shR, shL = _np(J['shoulder.R']), _np(J['shoulder.L'])

        def keep(P):
            k = np.maximum(z0 - P[:, 2], P[:, 2] - z1)
            # neckline scoop and armholes
            k = np.maximum(k, 0.15 - np.linalg.norm((P - nk) * [1.0, 1.15, 0.8], axis=1))
            for shp in (shR, shL):
                k = np.maximum(k, 0.17 - np.linalg.norm((P - shp - [0, 0, -0.03]) * [1.1, 1.0, 0.85], axis=1))
            if not back:
                k = np.maximum(k, -(P[:, 0] - J['pelvis'].x + 0.02))
            return k
        disp = None
        if ridge:
            cx = J['chest'].x

            def disp(P):
                # a keel down the front centre
                front = np.clip((P[:, 0] - cx) / 0.1, 0, 1)
                return -0.012 * front * np.exp(-(P[:, 1] / 0.05) ** 2)
        adjust = only(['hips', 'spine', 'chest'])
        out = self._plate(name, shell_of(surf, 0.016), keep, lo, hi, trim='trim' if trim else None, disp=disp,
                          bind=adjust, h=self.h * 1.4, trim_w=0.016)
        return out

    def faulds(self, lames=2, t=0.075, height=0.065, flare=0.03, trim=True, name='Fauld'):
        J, s = self.J, self.s
        pz = J['pelvis'].z
        out = []
        hips = self.K.base(['hips'])
        for i in range(lames):
            ztop = pz + 0.07 - i * height * 0.85
            zbot = ztop - height
            tt = t + i * flare * 0.5
            surf = lambda P, tt=tt: hips.eval(P) - tt - np.clip(ztop - P[:, 2], 0, 1) * flare

            def keep(P, ztop=ztop, zbot=zbot):
                return np.maximum(P[:, 2] - ztop, zbot - P[:, 2])

            def tkeep(P, zbot=zbot, ztop=ztop):
                return np.maximum(np.abs(P[:, 2] - zbot - 0.009) - 0.009, P[:, 2] - ztop)
            lo, hi = hips.bounds(0.2)
            out += self._plate(f'{name} {i}', shell_of(surf, 0.014), keep, lo, hi, trim='trim' if trim else None,
                               bind=skirt_weights(J), h=self.h * 1.3, trim_keep=tkeep)
        return out

    def tassets(self, width=0.12, length=0.2, t=0.1, lames=3, name='Tasset'):
        """Hanging plates over the thighs, each a stack of curved lames."""
        J = self.J
        out = []
        for side in ('R', 'L'):
            sy = -1 if side == 'R' else 1
            thigh = self.K.base(['leg.' + side, 'hips'])
            hp = J['hip.' + side]
            top = J['pelvis'].z - 0.02
            for i in range(lames):
                zt = top - i * length / lames * 0.9
                zb = zt - length / lames - 0.012
                surf = lambda P, i=i: thigh.eval(P) - t - i * 0.008

                def keep(P, zt=zt, zb=zb):
                    lat = np.abs(P[:, 1] - (hp.y + sy * 0.03)) - width
                    front = -(P[:, 0] - hp.x + 0.02)
                    return np.maximum.reduce([P[:, 2] - zt, zb - P[:, 2], lat, front])
                lo, hi = thigh.bounds(0.2)
                out += self._plate(f'{name}.{side}', shell_of(surf, 0.012), keep, lo, hi, trim='trim',
                                   bind=only(['thigh.' + side, 'hips']), h=self.h * 1.2, trim_w=0.01)
        return out

    def gorget(self, t=0.05, height=0.09, name='Gorget'):
        J = self.J
        nk = J['neck']
        base = self.K.base(['neck', 'torso'])
        surf = lambda P: base.eval(P) - t

        def keep(P):
            d = np.linalg.norm((P - _np(nk)) * [1, 1, 0.6], axis=1) - 0.2
            return np.maximum(np.maximum(d, P[:, 2] - (nk.z + height * 0.6)), (nk.z - height) - P[:, 2])
        lo, hi = base.bounds(0.15)
        return self._plate(name, shell_of(surf, 0.014), keep, lo, hi, trim='trim', bind=only(['chest', 'neck']),
                           h=self.h)

    # ------------------------------------------------------- arms
    def couter(self, side, size=1.0, fan=True):
        J = self.J
        sy = -1 if side == 'R' else 1
        el = J['elbow.' + side]
        ua = (J['shoulder.' + side] - el).normalized()
        fa = (J['wrist.' + side] - el).normalized()
        back = -(ua + fa).normalized() if (ua + fa).length > 1e-3 else Vector((-1, 0, 0))
        c = el + back * 0.03
        S = size
        r = 0.085 * S
        fn_d = lambda P: np.linalg.norm(P - _np(c), axis=1) - r

        def keep(P):
            q = P - _np(el)
            return -(q @ _np(back)) + 0.0
        lo, hi = _np(c) - 0.16, _np(c) + 0.16
        out = self._plate(f'Couter.{side}', shell_of(fn_d, 0.014), keep, lo, hi, trim='trim',
                          bind={'R': only(['forearm.R', 'upper_arm.R']), 'L': only(['forearm.L', 'upper_arm.L'])}[side])
        if fan:
            out_dir = Vector((0, sy, 0))
            fc = el + out_dir * 0.07 * S + back * 0.01
            ff = F.Field('Couter fan')
            ff.add(F.Ellipsoid(fc, (0.07 * S, 0.012 * S, 0.06 * S)), 0.0)
            o = ff.mesh(0.003, self.coll, self.M(self.mat), name='Couter fan')
            self.K.rigid(o, 'forearm.' + side)
            out.append(o)
        return out

    def vambrace(self, side, start=0.25, end=0.95, t=0.03, trim=True):
        J = self.J
        el, wr = J['elbow.' + side], J['wrist.' + side]
        arm = self.K.base(['arm.' + side])
        a, b = _np(el.lerp(wr, start)), _np(el.lerp(wr, end))
        ab = b - a
        L2 = float(ab @ ab)
        surf = lambda P: arm.eval(P) - t - 0.012 * np.clip(((P - a) @ ab) / L2, 0, 1) ** 2

        def keep(P):
            tt = ((P - a) @ ab) / L2
            q = P - a - tt[:, None] * ab
            return np.maximum.reduce([-tt * np.sqrt(L2), (tt - 1) * np.sqrt(L2), np.linalg.norm(q, axis=1) - 0.26])
        lo, hi = np.minimum(a, b) - 0.27, np.maximum(a, b) + 0.27
        return self._plate(f'Vambrace.{side}', shell_of(surf, 0.012), keep, lo, hi, trim='trim' if trim else None,
                           bind='forearm.' + side, trim_w=0.012)

    def rerebrace(self, side, start=0.35, end=0.92, t=0.035):
        J = self.J
        sh, el = J['shoulder.' + side], J['elbow.' + side]
        arm = self.K.base(['arm.' + side])
        a, b = _np(sh.lerp(el, start)), _np(sh.lerp(el, end))
        ab = b - a
        L2 = float(ab @ ab)
        surf = lambda P: arm.eval(P) - t

        def keep(P):
            tt = ((P - a) @ ab) / L2
            q = P - a - tt[:, None] * ab
            return np.maximum.reduce([-tt * np.sqrt(L2), (tt - 1) * np.sqrt(L2), np.linalg.norm(q, axis=1) - 0.15])
        lo, hi = np.minimum(a, b) - 0.16, np.maximum(a, b) + 0.16
        return self._plate(f'Rerebrace.{side}', shell_of(surf, 0.012), keep, lo, hi, trim='trim',
                           bind='upper_arm.' + side, trim_w=0.01)

    def gauntlet_cuff(self, side, t=0.03, length=0.07, flare=0.03):
        J = self.J
        el, wr = J['elbow.' + side], J['wrist.' + side]
        ax = (wr - el).normalized()
        a = wr - ax * length
        r0 = 0.058
        f = F.Field('Gauntlet cuff')
        f.add(F.RoundCone(a, wr + ax * 0.01, r0 + t * 0.4, r0 + flare + t * 0.4), 0.0)
        f.cut(F.RoundCone(a - ax * 0.02, wr + ax * 0.03, r0 + t * 0.4 - 0.012, r0 + flare + t * 0.4 - 0.012), 0.002)
        o = f.mesh(0.003, self.coll, self.M(self.mat), name='Gauntlet cuff')
        tf = F.Field('Gauntlet cuff trim')
        tf.add(F.Torus(wr + ax * 0.008, r0 + flare + t * 0.4 - 0.004, 0.007, _frame_z(ax)), 0.0)
        to = tf.mesh(0.0025, self.coll, self.M(self.trim), name='Gauntlet cuff trim')
        for x in (o, to):
            self.K.rigid(x, 'forearm.' + side)
        return [o, to]

    # ------------------------------------------------------- legs
    def poleyn(self, side, size=1.0, fan=True, mat=None):
        J = self.J
        sy = -1 if side == 'R' else 1
        kn = J['knee.' + side]
        hip, an = J['hip.' + side], J['ankle.' + side]
        # face forward between the thigh and shin directions
        fwd = ((kn - hip).normalized().cross(Vector((0, 1, 0))) + (an - kn).normalized().cross(Vector((0, 1, 0))))
        fwd = Vector((1, 0, 0)) if fwd.length < 1e-3 else fwd.normalized()
        if fwd.x < 0:
            fwd = -fwd
        c = kn + fwd * 0.04
        S = size
        r = 0.085 * S
        fn_d = lambda P: np.linalg.norm((P - _np(c)) * [1.6, 1.0, 0.9], axis=1) - r

        def keep(P):
            return -((P - _np(kn)) @ _np(fwd)) + 0.01
        lo, hi = _np(c) - 0.16, _np(c) + 0.16
        out = self._plate(f'Poleyn.{side}', shell_of(fn_d, 0.016), keep, lo, hi, trim='trim', mat=mat,
                          bind=only(['shin.' + side, 'thigh.' + side]))
        if fan:
            fc = kn + Vector((0.01, sy * 0.08 * S, 0.0))
            ff = F.Field('Poleyn fan')
            ff.add(F.Ellipsoid(fc, (0.065 * S, 0.012 * S, 0.06 * S)), 0.0)
            o = ff.mesh(0.003, self.coll, self.M(self.mat), name='Poleyn fan')
            self.K.rigid(o, 'shin.' + side)
            out.append(o)
        out += self.rivets([c + fwd * (r * 0.78)], 0.01, None, only(['shin.' + side, 'thigh.' + side]))
        return out

    def greave(self, side, t=0.04, start=0.1, end=0.75, front_only=False):
        J = self.J
        kn, an = J['knee.' + side], J['ankle.' + side]
        leg = self.K.base(['leg.' + side])
        a, b = _np(kn.lerp(an, start)), _np(kn.lerp(an, end))
        ab = b - a
        L2 = float(ab @ ab)
        surf = lambda P: leg.eval(P) - t

        ax = ab / math.sqrt(L2)
        fwd = np.array([1.0, 0, 0]) - ax * ax[0]
        fwd /= max(1e-6, np.linalg.norm(fwd))

        def keep(P):
            tt = ((P - a) @ ab) / L2
            q = P - a - tt[:, None] * ab
            k = np.maximum.reduce([-tt * np.sqrt(L2), (tt - 1) * np.sqrt(L2), np.linalg.norm(q, axis=1) - 0.4])
            if front_only:
                k = np.maximum(k, -(q @ fwd) - 0.03)
            return k
        lo, hi = np.minimum(a, b) - 0.24, np.maximum(a, b) + 0.24
        return self._plate(f'Greave.{side}', shell_of(surf, 0.012), keep, lo, hi, trim='trim',
                           bind='shin.' + side, trim_w=0.012)

    def cuisse(self, side, t=0.05, start=0.2, end=0.85):
        J = self.J
        hp, kn = J['hip.' + side], J['knee.' + side]
        leg = self.K.base(['leg.' + side])
        a, b = _np(hp.lerp(kn, start)), _np(hp.lerp(kn, end))
        ab = b - a
        L2 = float(ab @ ab)
        surf = lambda P: leg.eval(P) - t

        ax = ab / math.sqrt(L2)
        fwd = np.array([1.0, 0, 0]) - ax * ax[0]
        fwd /= max(1e-6, np.linalg.norm(fwd))

        def keep(P):
            tt = ((P - a) @ ab) / L2
            q = P - a - tt[:, None] * ab
            k = np.maximum.reduce([-tt * np.sqrt(L2), (tt - 1) * np.sqrt(L2), np.linalg.norm(q, axis=1) - 0.4])
            return np.maximum(k, -(q @ fwd) - 0.03)
        lo, hi = np.minimum(a, b) - 0.26, np.maximum(a, b) + 0.26
        return self._plate(f'Cuisse.{side}', shell_of(surf, 0.012), keep, lo, hi, trim='trim', bind='thigh.' + side,
                           trim_w=0.012)

    def sabatons(self, lames=3):
        J = self.J
        out = []
        for side in ('R', 'L'):
            an, toe = J['ankle.' + side], J['toe.' + side]
            for i in range(lames):
                t = 0.25 + i * 0.22
                c = an.lerp(toe, t) + Vector((0.01, 0, 0.045 - 0.012 * i))
                f = F.Field('Sabaton lame')
                f.add(F.Ellipsoid(c, (0.06, 0.068, 0.05)), 0.0)
                f.keep(F.Fn(lambda P, c=c: np.maximum(c.z - 0.005 - P[:, 2], np.abs(P[:, 0] - c.x) - 0.045),
                            _np(c) - 0.1, _np(c) + 0.1), 0.003)
                f.cut(F.Ellipsoid(c - Vector((0, 0, 0.012)), (0.05, 0.058, 0.04)), 0.002)
                o = f.mesh(0.003, self.coll, self.M(self.mat), name='Sabaton')
                self.K.rigid(o, 'foot.' + side)
                out.append(o)
        return out


def _frame_z(axis):
    z = Vector(tuple(axis)).normalized()
    x = Vector((1, 0, 0)) if abs(z.x) < 0.9 else Vector((0, 1, 0))
    y = z.cross(x).normalized()
    x = y.cross(z).normalized()
    return Matrix((x, y, z)).transposed()
