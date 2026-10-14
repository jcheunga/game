"""Signed-distance sculpting: organic forms from smoothly blended primitives, meshed with surface nets.

A Field is an ordered list of operations over primitives: smooth union ('add'), smooth subtraction
('cut') and smooth intersection ('keep'). Primitives are vectorised over numpy point arrays.
`Field.mesh()` samples the field in a narrow band around the surface, extracts a quad surface with
naive surface nets, projects the vertices onto the zero set, relaxes it with Taubin smoothing and
returns a Blender mesh object.

Union primitives may carry bone weights: a {bone: weight} dict, or a function (points) -> {bone: array}.
`Field.weights()` blends them by each primitive's distance, so skinning follows the sculpt: a deltoid
tagged half chest / half upper arm bends the way a shoulder does.
"""
import math

import bpy
import numpy as np

BIG = 1e3
FIELDS = {}   # object name -> the Field it was meshed from (garment coverage, body masking)


def _v(x):
    return np.asarray(tuple(x), dtype=np.float64)


def _mat(m):
    """3x3 rotation (mathutils Matrix / nested list / None) as numpy, or None."""
    if m is None:
        return None
    return np.array([[m[i][j] for j in range(3)] for i in range(3)], dtype=np.float64)


# ------------------------------------------------------------------ primitives
class Prim:
    lip = 1.0     # Lipschitz correction for non-uniformly scaled shapes

    def bbox(self):
        raise NotImplementedError

    def d(self, P):
        raise NotImplementedError

    def transformed(self, M):
        """This primitive moved by a rigid 4x4 (numpy). Default: evaluate through the inverse."""
        return Moved(self, M)


def _rigid(M):
    M = np.asarray(M, dtype=np.float64)
    return M[:3, :3], M[:3, 3]


class Moved(Prim):
    def __init__(self, prim, M):
        self.prim = prim
        self.R, self.t = _rigid(M)
        self.lip = prim.lip

    def bbox(self):
        lo, hi = self.prim.bbox()
        corners = np.array([[x, y, z] for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])])
        w = corners @ self.R.T + self.t
        return w.min(0), w.max(0)

    def d(self, P):
        return self.prim.d((P - self.t) @ self.R)


class Sphere(Prim):
    def __init__(self, c, r):
        self.c, self.r = _v(c), float(r)

    def bbox(self):
        return self.c - self.r, self.c + self.r

    def d(self, P):
        return np.linalg.norm(P - self.c, axis=1) - self.r

    def transformed(self, M):
        R, t = _rigid(M)
        return Sphere(R @ self.c + t, self.r)


class Ellipsoid(Prim):
    """Ellipsoid with radii along the columns of rotation R (approximate distance, exact sign)."""

    def __init__(self, c, radii, R=None):
        self.c, self.r, self.R = _v(c), _v(radii), _mat(R)

    def bbox(self):
        if self.R is None:
            e = self.r
        else:
            e = np.sqrt(((self.R * self.r[None, :]) ** 2).sum(1))
        return self.c - e, self.c + e

    def d(self, P):
        q = P - self.c
        if self.R is not None:
            q = q @ self.R
        k0 = np.linalg.norm(q / self.r, axis=1)
        k1 = np.linalg.norm(q / (self.r * self.r), axis=1)
        return np.where(k1 > 1e-9, k0 * (k0 - 1.0) / np.maximum(k1, 1e-9), -self.r.min())

    def transformed(self, M):
        R, t = _rigid(M)
        e = Ellipsoid.__new__(Ellipsoid)
        e.c, e.r = R @ self.c + t, self.r.copy()
        e.R = R if self.R is None else R @ self.R
        return e


class RoundCone(Prim):
    """Capsule with different end radii (exact). squash=(sy, sz) flattens the cross-section in the
    frame given by `up` (a vector roughly perpendicular to the axis)."""

    def __init__(self, a, b, ra, rb=None, squash=None, up=(0, 0, 1)):
        self.a, self.b = _v(a), _v(b)
        self.ra, self.rb = float(ra), float(ra if rb is None else rb)
        self.squash = None
        if squash is not None and (squash[0] != 1 or squash[1] != 1):
            ax = self.b - self.a
            ax = ax / max(1e-9, np.linalg.norm(ax))
            u = _v(up) - ax * np.dot(_v(up), ax)
            if np.linalg.norm(u) < 1e-6:
                u = np.cross(ax, [1, 0, 0])
            u /= np.linalg.norm(u)
            w = np.cross(ax, u)
            self.frame = np.stack([ax, w, u], 1)     # columns: axis, side, up
            self.squash = (float(squash[0]), float(squash[1]))
            self.lip = min(1.0, *self.squash)

    def bbox(self):
        r = max(self.ra, self.rb)
        return np.minimum(self.a, self.b) - r, np.maximum(self.a, self.b) + r

    def d(self, P):
        a, b = self.a, self.b
        if self.squash is not None:
            # scale the cross-section about the axis (in the shape's own frame)
            q = (P - a) @ self.frame
            q[:, 1] /= self.squash[0]
            q[:, 2] /= self.squash[1]
            P = a + q @ self.frame.T
        ba = b - a
        l2 = float(ba @ ba)
        rr = self.ra - self.rb
        a2 = l2 - rr * rr
        il2 = 1.0 / max(l2, 1e-12)
        pa = P - a
        y = pa @ ba
        z = y - l2
        x2 = ((pa * l2 - ba[None, :] * y[:, None]) ** 2).sum(1)
        y2 = y * y * l2
        z2 = z * z * l2
        k = math.copysign(1.0, rr) * rr * rr * x2
        d3 = (np.sqrt(np.maximum(x2 * a2 * il2, 0)) + y * rr) * il2 - self.ra
        d1 = np.sqrt(x2 + z2) * il2 - self.rb
        d2 = np.sqrt(x2 + y2) * il2 - self.ra
        out = np.where(np.sign(y) * a2 * y2 < k, d2, d3)
        out = np.where(np.sign(z) * a2 * z2 > k, d1, out)
        return out * self.lip

    def transformed(self, M):
        R, t = _rigid(M)
        c = RoundCone.__new__(RoundCone)
        c.a, c.b, c.ra, c.rb, c.lip = R @ self.a + t, R @ self.b + t, self.ra, self.rb, self.lip
        c.squash = self.squash
        if self.squash is not None:
            c.frame = R @ self.frame
        return c


class Box(Prim):
    """Rounded box: half extents (including the rounding), rotation R (columns are the box axes), rounding r."""

    def __init__(self, c, half, R=None, r=0.0):
        self.c, self.h, self.R, self.rr = _v(c), _v(half), _mat(R), float(r)

    def bbox(self):
        e = self.h.copy()
        if self.R is not None:
            e = np.abs(self.R) @ e
        return self.c - e, self.c + e

    def d(self, P):
        q = P - self.c
        if self.R is not None:
            q = q @ self.R
        q = np.abs(q) - (self.h - self.rr)
        outside = np.linalg.norm(np.maximum(q, 0), axis=1)
        inside = np.minimum(q.max(1), 0)
        return outside + inside - self.rr

    def transformed(self, M):
        R, t = _rigid(M)
        b = Box.__new__(Box)
        b.c, b.h, b.rr = R @ self.c + t, self.h.copy(), self.rr
        b.R = R if self.R is None else R @ self.R
        return b


class Torus(Prim):
    """Ring of major radius `R0` and tube radius `r` around axis column 2 of rotation R."""

    def __init__(self, c, R0, r, R=None, squash=1.0):
        self.c, self.R0, self.r, self.R = _v(c), float(R0), float(r), _mat(R)
        self.sq = float(squash)

    def bbox(self):
        e = np.array([self.R0 + self.r, self.R0 + self.r, self.r * self.sq])
        if self.R is not None:
            e = np.abs(self.R) @ e
        return self.c - e, self.c + e

    def d(self, P):
        q = P - self.c
        if self.R is not None:
            q = q @ self.R
        qx = np.linalg.norm(q[:, :2], axis=1) - self.R0
        return np.sqrt(qx * qx + (q[:, 2] / self.sq) ** 2) * min(1.0, self.sq) - self.r

    def transformed(self, M):
        R, t = _rigid(M)
        o = Torus.__new__(Torus)
        o.c, o.R0, o.r, o.sq = R @ self.c + t, self.R0, self.r, self.sq
        o.R = R if self.R is None else R @ self.R
        return o


class Plane(Prim):
    """Half-space: negative behind the plane through `p` with outward normal `n`."""

    def __init__(self, p, n):
        n = _v(n)
        self.n = n / np.linalg.norm(n)
        self.p = _v(p)

    def bbox(self):
        return np.full(3, -BIG), np.full(3, BIG)

    def d(self, P):
        return (P - self.p) @ self.n


class Chain(Prim):
    """Tube through points with per-point radii (union of round cones)."""

    def __init__(self, points, radii, squash=None, up=(0, 0, 1)):
        pts = [_v(p) for p in points]
        if isinstance(radii, (int, float)):
            radii = [radii] * len(pts)
        self.parts = [RoundCone(a, b, ra, rb, squash, up) for a, b, ra, rb in zip(pts, pts[1:], radii, radii[1:])]

    def bbox(self):
        lo = np.min([p.bbox()[0] for p in self.parts], 0)
        hi = np.max([p.bbox()[1] for p in self.parts], 0)
        return lo, hi

    def d(self, P):
        out = np.full(len(P), BIG)
        for p in self.parts:
            out = np.minimum(out, p.d(P))
        return out

    def transformed(self, M):
        c = Chain.__new__(Chain)
        c.parts = [p.transformed(M) for p in self.parts]
        return c


class Fn(Prim):
    """Arbitrary distance function with an explicit bounding box."""

    def __init__(self, fn, lo, hi):
        self.fn, self.lo, self.hi = fn, _v(lo), _v(hi)

    def bbox(self):
        return self.lo, self.hi

    def d(self, P):
        return self.fn(P)


# ------------------------------------------------------------------ blending
def smin(a, b, k):
    if k <= 0:
        return np.minimum(a, b)
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b + (a - b) * h - k * h * (1 - h)


def smax(a, b, k):
    return -smin(-a, -b, k)


class Field:
    def __init__(self, name='Sculpt'):
        self.name = name
        self.ops = []     # (mode, prim, k, tag, disp)
        self.groups = []  # group label per op (None when unlabelled)
        self.group = None  # label applied to ops added from now on
        self.disp = None  # global displacement fn(P) -> offset added to the distance
        self.reach = 0.02  # how far from its primitives the distance stays trustworthy (raise for offsets)

    def add(self, prim, k=0.02, tag=None, disp=None):
        self.ops.append(('add', prim, float(k), tag, disp))
        self.groups.append(self.group)
        return prim

    def cut(self, prim, k=0.01, disp=None):
        self.ops.append(('cut', prim, float(k), None, disp))
        self.groups.append(self.group)
        return prim

    def keep(self, prim, k=0.0):
        self.ops.append(('keep', prim, float(k), None, None))
        self.groups.append(self.group)
        return prim

    def extend(self, other):
        self.ops.extend(other.ops)
        self.groups.extend(other.groups)
        return self

    def posed(self, mats, name=None):
        """The field with every primitive carried rigidly by its dominant bone. mats: {bone: 4x4 numpy}
        (armature-space deformation, pose @ rest^-1). Tags travel with their primitives."""
        out = Field(name or self.name + ' (posed)')
        out.reach, out.disp = self.reach, self.disp
        for (mode, prim, k, tag, disp), g in zip(self.ops, self.groups):
            bone = _dominant(tag, prim)
            M = mats.get(bone) if bone else None
            if M is None:
                out.ops.append((mode, prim, k, tag, disp))
            else:
                moved = prim.transformed(M)
                if callable(tag):
                    R, t = _rigid(M)
                    tag = (lambda fn, R, t: (lambda P: fn((P - t) @ R)))(tag, R, t)
                if disp is not None:
                    R, t = _rigid(M)
                    disp = (lambda fn, R, t: (lambda P: fn((P - t) @ R)))(disp, R, t)
                out.ops.append((mode, moved, k, tag, disp))
            out.groups.append(g)
        return out

    def subset(self, groups, name=None):
        """A field made of the ops whose group is in `groups` (prefix match: 'arm' matches 'arm.R')."""
        out = Field(name or self.name)
        out.reach = self.reach
        for op, g in zip(self.ops, self.groups):
            if g is not None and any(g == x or g.startswith(x + '.') for x in groups):
                out.ops.append(op)
                out.groups.append(g)
        return out

    def bounds(self, pad=0.0):
        boxes = [p.bbox() for m, p, k, t, dsp in self.ops if m == 'add']
        lo = np.min([b[0] for b in boxes], 0) - pad
        hi = np.max([b[1] for b in boxes], 0) + pad
        return lo, hi

    def eval(self, P):
        P = np.asarray(P, dtype=np.float64)
        d = np.full(len(P), BIG)
        for mode, prim, k, tag, disp in self.ops:
            if mode == 'keep':
                pd = prim.d(P)
                d = smax(d, pd, k)
                continue
            lo, hi = prim.bbox()
            m = k + self.reach + (0.03 if disp is not None else 0)
            sel = np.all((P >= lo - m) & (P <= hi + m), axis=1)
            if not sel.any():
                continue
            Q = P[sel]
            pd = prim.d(Q)
            if disp is not None:
                pd = pd + disp(Q)
            if mode == 'add':
                d[sel] = smin(d[sel], pd, k)
            else:
                d[sel] = smax(d[sel], -pd, k)
        if self.disp is not None:
            near = np.abs(d) < 0.05
            if near.any():
                d[near] = d[near] + self.disp(P[near])
        return d

    def grad(self, P, eps=4e-4):
        g = np.zeros_like(P)
        for i in range(3):
            e = np.zeros(3)
            e[i] = eps
            g[:, i] = (self.eval(P + e) - self.eval(P - e)) / (2 * eps)
        return g

    # -------------------------------------------------------------- meshing
    def sample(self, h, pad=None):
        """Narrow-band sampling: a coarse pass finds the cells the surface can cross."""
        pad = 3 * h if pad is None else pad
        lo, hi = self.bounds(pad)
        n = np.maximum(np.ceil((hi - lo) / h).astype(int) + 1, 2)
        H = h * 4
        nc = np.maximum(np.ceil((hi - lo) / H).astype(int) + 2, 2)
        cx = [lo[i] + np.arange(nc[i]) * H for i in range(3)]
        CP = np.stack(np.meshgrid(*cx, indexing='ij'), -1).reshape(-1, 3)
        dc = self.eval(CP).reshape(tuple(nc))
        band = np.abs(dc) < H * 1.8
        # dilate the band by one coarse cell
        b2 = band.copy()
        for ax in range(3):
            b2 |= np.roll(band, 1, ax) | np.roll(band, -1, ax)
        fx = [lo[i] + np.arange(n[i]) * h for i in range(3)]
        idx = [np.clip(np.round((fx[i] - lo[i]) / H).astype(int), 0, nc[i] - 1) for i in range(3)]
        I, J, K = np.meshgrid(*idx, indexing='ij')
        mask = b2[I, J, K]
        d = np.where(dc[I, J, K] < 0, -H, H).astype(np.float64)
        FP = np.stack(np.meshgrid(*fx, indexing='ij'), -1)
        d[mask] = self.eval(FP[mask])
        # a bad sample (0/0 in some displacement) counts as outside rather than poisoning the mesh
        d = np.where(np.isfinite(d), d, H)
        return lo, h, d

    def mesh(self, h=0.006, coll=None, mat=None, smooth=6, project=2, name=None):
        lo, h, d = self.sample(h)
        verts, faces = surface_nets(d, lo, h)
        if len(verts) == 0:
            raise ValueError(f'{self.name}: empty field')
        for _ in range(project):
            verts = self._project(verts)
        if smooth:
            verts = taubin(verts, faces, smooth)
            verts = self._project(verts, damp=0.5)
        obj = to_object(name or self.name, verts, faces, coll, mat)
        FIELDS[obj.name] = self
        return obj

    def _project(self, V, damp=1.0):
        dv = np.nan_to_num(self.eval(V), nan=0.0, posinf=0.0, neginf=0.0)
        g = np.nan_to_num(self.grad(V), nan=0.0, posinf=0.0, neginf=0.0)
        gl = np.maximum((g * g).sum(1), 1e-6)
        step = (dv / gl)[:, None] * g
        # never move a vertex further than a voxel-ish distance in one step
        sl = np.linalg.norm(step, axis=1)
        lim = 0.01
        step *= np.minimum(1.0, lim / np.maximum(sl, 1e-12))[:, None]
        return V - damp * step

    # -------------------------------------------------------------- skinning
    def weights(self, V, sigma=0.025):
        """Bone weights at points V from the tagged union primitives: {bone: array}."""
        tagged = [(p, t) for m, p, k, t, dsp in self.ops if m == 'add' and t is not None]
        D = np.stack([p.d(V) for p, t in tagged], 1)
        dmin = D.min(1, keepdims=True)
        S = np.exp(-(D - dmin) / sigma)
        out = {}
        for j, (p, t) in enumerate(tagged):
            tw = t(V) if callable(t) else t
            for bone, w in tw.items():
                out[bone] = out.get(bone, 0) + S[:, j] * w
        tot = sum(out.values())
        return {b: w / np.maximum(tot, 1e-9) for b, w in out.items()}


def _dominant(tag, prim):
    if tag is None:
        return None
    if callable(tag):
        lo, hi = prim.bbox()
        c = ((lo + hi) / 2)[None, :]
        tag = {b: float(np.asarray(w).ravel()[0]) for b, w in tag(c).items()}
    return max(tag, key=lambda b: tag[b]) if tag else None


# ------------------------------------------------------------------ surface nets
def surface_nets(d, lo, h):
    inside = d < 0
    nx, ny, nz = d.shape
    sh = (nx - 1, ny - 1, nz - 1)
    acc = np.zeros(sh + (3,))
    cnt = np.zeros(sh)

    def edges(axis):
        a = [slice(None)] * 3
        b = [slice(None)] * 3
        a[axis] = slice(0, -1)
        b[axis] = slice(1, None)
        d0, d1 = d[tuple(a)], d[tuple(b)]
        m = inside[tuple(a)] != inside[tuple(b)]
        t = np.where(m, d0 / np.where(m, d0 - d1, 1.0), 0.0)
        return m, t

    grid_idx = np.stack(np.meshgrid(np.arange(nx), np.arange(ny), np.arange(nz), indexing='ij'), -1)
    E = []
    for axis in range(3):
        m, t = edges(axis)
        sl = [slice(None)] * 3
        sl[axis] = slice(0, -1)
        base = grid_idx[tuple(sl)].astype(np.float64)
        pos = base.copy()
        pos[..., axis] += t
        E.append((m, pos))
        others = [o for o in range(3) if o != axis]
        for da in (0, 1):
            for db in (0, 1):
                s = [slice(None)] * 3
                s[others[0]] = slice(da, da + sh[others[0]])
                s[others[1]] = slice(db, db + sh[others[1]])
                s[axis] = slice(0, sh[axis])
                mm = m[tuple(s)]
                acc += pos[tuple(s)] * mm[..., None]
                cnt += mm
    active = cnt > 0
    index = -np.ones(sh, dtype=np.int64)
    n = int(active.sum())
    index[active] = np.arange(n)
    verts = acc[active] / cnt[active][:, None]
    verts = lo + verts * h
    quads = []
    for axis in range(3):
        m, _ = E[axis]
        o1, o2 = [o for o in range(3) if o != axis]
        # interior edges only (each touches four cells)
        s = [slice(None)] * 3
        s[o1] = slice(1, -1)
        s[o2] = slice(1, -1)
        mm = m[tuple(s)]
        ins = inside[tuple([slice(0, -1) if i == axis else s[i] for i in range(3)])]
        e = np.argwhere(mm)
        if len(e) == 0:
            continue
        e[:, o1] += 1
        e[:, o2] += 1
        flip = ins[mm]

        def cell(dA, dB):
            c = e.copy()
            c[:, o1] -= dA
            c[:, o2] -= dB
            return index[c[:, 0], c[:, 1], c[:, 2]]
        # counter-clockwise about +axis in the (o1, o2) plane, (o1, o2) cyclic after axis
        cyc = (axis + 1) % 3 == o1
        q = np.stack([cell(1, 1), cell(0, 1), cell(0, 0), cell(1, 0)], 1) if cyc else \
            np.stack([cell(1, 1), cell(1, 0), cell(0, 0), cell(0, 1)], 1)
        q = np.where(flip[:, None], q, q[:, ::-1])
        quads.append(q)
    faces = np.concatenate(quads, 0) if quads else np.zeros((0, 4), np.int64)
    faces = faces[(faces >= 0).all(1)]
    return verts, faces


def adjacency(faces, n):
    e = np.concatenate([faces[:, [0, 1]], faces[:, [1, 2]], faces[:, [2, 3]], faces[:, [3, 0]]], 0)
    e = np.sort(e, 1)
    e = np.unique(e, axis=0)
    return e


def taubin(V, faces, iterations=6, lam=0.5, mu=-0.53):
    e = adjacency(faces, len(V))
    deg = np.zeros(len(V))
    np.add.at(deg, e[:, 0], 1)
    np.add.at(deg, e[:, 1], 1)
    deg = np.maximum(deg, 1)
    for _ in range(iterations):
        for f in (lam, mu):
            s = np.zeros_like(V)
            np.add.at(s, e[:, 0], V[e[:, 1]])
            np.add.at(s, e[:, 1], V[e[:, 0]])
            V = V + f * (s / deg[:, None] - V)
    return V


def smooth_values(W, faces, iterations=4, factor=0.5):
    """Topological smoothing of per-vertex values (N,) or (N, k)."""
    e = adjacency(faces, len(W))
    deg = np.zeros(len(W))
    np.add.at(deg, e[:, 0], 1)
    np.add.at(deg, e[:, 1], 1)
    deg = np.maximum(deg, 1)
    W = W.astype(np.float64)
    for _ in range(iterations):
        s = np.zeros_like(W)
        np.add.at(s, e[:, 0], W[e[:, 1]])
        np.add.at(s, e[:, 1], W[e[:, 0]])
        avg = s / (deg if W.ndim == 1 else deg[:, None])
        W = W * (1 - factor) + avg * factor
    return W


def to_object(name, verts, faces, coll=None, mat=None):
    me = bpy.data.meshes.new(name)
    nv, nf = len(verts), len(faces)
    me.vertices.add(nv)
    me.vertices.foreach_set('co', verts.astype(np.float32).ravel())
    me.loops.add(nf * 4)
    me.loops.foreach_set('vertex_index', faces.astype(np.int32).ravel())
    me.polygons.add(nf)
    me.polygons.foreach_set('loop_start', (np.arange(nf) * 4).astype(np.int32))
    me.polygons.foreach_set('loop_total', np.full(nf, 4, np.int32))
    me.update(calc_edges=True)
    me.validate(clean_customdata=False)
    me.polygons.foreach_set('use_smooth', np.ones(len(me.polygons), dtype=bool))
    obj = bpy.data.objects.new(name, me)
    (coll or bpy.context.scene.collection).objects.link(obj)
    if mat is not None:
        for m in (mat if isinstance(mat, (list, tuple)) else [mat]):
            me.materials.append(m)
    from . import geo
    geo.store_rest(obj)
    return obj


def quads(obj):
    """Face vertex indices of an all-quad mesh as an (F, 4) array."""
    me = obj.data
    L = np.zeros(len(me.loops), np.int64)
    me.loops.foreach_get('vertex_index', L)
    if len(L) != 4 * len(me.polygons):
        raise ValueError('mesh is not all quads')
    return L.reshape(-1, 4)


def verts(obj):
    me = obj.data
    V = np.zeros(len(me.vertices) * 3)
    me.vertices.foreach_get('co', V)
    return V.reshape(-1, 3)


def write_weights(obj, weights, faces=None, smooth=3, top=4, levels=256):
    """weights: {bone: (N,) array}. Smooths, keeps the strongest `top` influences and writes vertex
    groups in batches of equal (quantised) weight."""
    bones = list(weights)
    W = np.stack([weights[b] for b in bones], 1)
    if smooth and faces is not None:
        W = smooth_values(W, faces, smooth)
    if W.shape[1] > top:
        thresh = -np.sort(-W, 1)[:, top - 1:top]
        W = np.where(W >= thresh, W, 0)
    W = W / np.maximum(W.sum(1, keepdims=True), 1e-9)
    q = np.round(W * levels).astype(int)
    # the armature normalises by the weight sum: return exactly what it will use
    Wq = q / np.maximum(q.sum(1, keepdims=True), 1)
    for j, b in enumerate(bones):
        g = obj.vertex_groups.get(b) or obj.vertex_groups.new(name=b)
        col = q[:, j]
        for lvl in np.unique(col):
            if lvl <= 0:
                continue
            idx = np.nonzero(col == lvl)[0].tolist()
            g.add(idx, float(lvl) / levels, 'REPLACE')
    return bones, Wq


# ------------------------------------------------------------------ helpers
def along(a, b, bone_a, bone_b, t0=0.0, t1=1.0):
    """Weight function blending bone_a -> bone_b along segment a->b between fractions t0..t1."""
    a, b = _v(a), _v(b)
    ab = b - a
    l2 = max(float(ab @ ab), 1e-12)

    def fn(P):
        t = np.clip(((P - a) @ ab) / l2, 0, 1)
        s = np.clip((t - t0) / max(1e-6, t1 - t0), 0, 1)
        s = s * s * (3 - 2 * s)
        return {bone_a: 1 - s, bone_b: s}
    return fn


def noise3(P, scale=10.0, seed=0, octaves=2):
    """Cheap smooth value noise for sculpt displacement (numpy, deterministic)."""
    out = np.zeros(len(P))
    amp = 1.0
    for o in range(octaves):
        Q = P * scale * (2 ** o) + seed * 17.13 + o * 3.7
        out += amp * (np.sin(Q[:, 0] * 1.7 + np.sin(Q[:, 1] * 1.3)) * np.sin(Q[:, 1] * 1.9 + np.sin(Q[:, 2] * 1.1)) *
                      np.sin(Q[:, 2] * 1.5 + np.sin(Q[:, 0] * 0.9)))
        amp *= 0.5
    return out
