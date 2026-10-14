"""Stylised faces, hair and beards sculpted as signed-distance fields.

Heads follow the roster sheet's animated-fantasy faces: a heavy brow, deep-set eyes, a strong straight
nose, defined cheekbones and a broad jaw for the men; finer jaw, fuller lips and larger eyes for the
women. Every builder takes the head centre C (world Vector, roughly the middle of the cranium at eye
height) and the unit u (about 1/2.2 of the chin-to-crown height) and returns mesh objects for the head
bone. Hair and beards are clumped sculpts hugging the same skull.
"""
import math

import numpy as np
from mathutils import Matrix, Vector

from . import geo
from . import sdf as F

DEFAULT = dict(
    jaw=1.0,        # jaw width
    chin=1.0,       # chin size
    brow=1.0,       # brow ridge weight
    frown=0.6,      # brows pulled down toward the nose (stern)
    nose=1.0,       # nose size
    hook=0.0,       # aquiline bridge
    cheek=1.0,      # cheekbones
    lips=1.0,
    eye=1.0,        # eye size
    ears=1.0,
    gaunt=0.0,      # hollow cheeks
    age=0.0,        # 0..1 lines and sag
    female=0.0,
    smile=0.0,
    open_mouth=0.0,
    elf=0.0,        # pointed ears
    cranium=1.0,
)


LAST = {}   # the most recent head sculpt: hair and beards conform to it


def _p(C, u, x, y, z):
    return C + Vector((x * u, y * u, z * u))


def face_params(**kw):
    s = dict(DEFAULT)
    s.update(kw)
    return s


def head_field(C, u, **kw):
    """The skin sculpt of a head (no eyes or hair). Returns (field, landmarks)."""
    s = face_params(**kw)
    fem = s['female']
    P = lambda x, y, z: _p(C, u, x, y, z)
    f = F.Field('Head')
    f.reach = 0.09   # hair, beards and helmets are offset from this sculpt
    tag = {'head': 1}
    j = s['jaw'] * (1 - 0.18 * fem)
    cr = s['cranium']
    # cranium and the back of the skull
    f.add(F.Ellipsoid(P(-0.1, 0, 0.12), (0.88 * u * cr, 0.76 * u * cr, 0.86 * u * cr)), 0.0, tag)
    f.add(F.Ellipsoid(P(-0.3, 0, -0.12), (0.56 * u, 0.58 * u, 0.58 * u)), 0.2 * u, tag)
    # mid-face mass
    f.add(F.Ellipsoid(P(0.42, 0, -0.22), (0.52 * u, 0.6 * u * (1 - 0.12 * fem), 0.58 * u * (1 - 0.05 * fem))),
          0.22 * u, tag)
    # mandible: jaw angle to chin on each side gives a clean jawline
    ch = s['chin'] * (1 - 0.22 * fem)
    for sy in (-1, 1):
        f.add(F.RoundCone(P(-0.08 + 0.05 * fem, sy * (0.56 - 0.08 * fem) * j, -0.5 + 0.06 * fem),
                          P(0.62, sy * (0.2 - 0.06 * fem) * j, -0.88 - 0.04 * ch),
                          (0.2 - 0.07 * fem) * u * j, (0.16 - 0.06 * fem) * u * ch), 0.14 * u, tag)
    f.add(F.Ellipsoid(P(0.66, 0, -0.9), (0.22 * u * ch, (0.27 - 0.06 * fem) * u * j * ch, 0.19 * u * ch)), 0.12 * u, tag)
    # cheekbones (and, for heavy men, a little cheek mass)
    for sy in (-1, 1):
        f.add(F.Ellipsoid(P(0.58, sy * 0.5, -0.12 + 0.04 * fem), (0.19 * u, 0.17 * u * s['cheek'], 0.13 * u)),
              0.14 * u, tag)
        if not s['gaunt'] and not fem:
            f.add(F.Ellipsoid(P(0.62, sy * 0.4, -0.5), (0.17 * u, 0.17 * u, 0.17 * u)), 0.16 * u, tag)
    # brow ridge: three-point bar, dipping at the centre for a stern look
    b = s['brow'] * (1 - 0.65 * fem)
    fr = s['frown'] * (1 - 0.6 * fem)
    br = F.Chain([P(0.6, -0.6, 0.24), P(0.84, -0.3, 0.22 - 0.04 * fr), P(0.9, 0, 0.14 - 0.03 * fr),
                  P(0.84, 0.3, 0.22 - 0.04 * fr), P(0.6, 0.6, 0.24)],
                 [0.08 * u * b + 0.02 * u, 0.12 * u * b + 0.02 * u, 0.11 * u * b + 0.02 * u, 0.12 * u * b + 0.02 * u,
                  0.08 * u * b + 0.02 * u], squash=(1.0, 0.8))
    f.add(br, 0.12 * u, tag)
    f.add(F.Ellipsoid(P(0.45, 0, 0.42), (0.42 * u, 0.6 * u, 0.42 * u)), 0.25 * u, tag)
    # eye sockets
    e = s['eye'] * (1 + 0.24 * fem)
    eyes = []
    for sy in (-1, 1):
        ec = P(0.66, sy * 0.32, 0.0)
        eyes.append(ec)
        f.cut(F.Sphere(P(0.84, sy * 0.32, 0.0), 0.17 * u * e), 0.07 * u)
    # nose
    n = s['nose'] * (1 - 0.38 * fem)
    hk = s['hook']
    f.add(F.RoundCone(P(0.9, 0, 0.12), P(1.04 + 0.05 * n, 0, -0.26), 0.065 * u * n, 0.085 * u * n), 0.06 * u, tag)
    if hk:
        f.add(F.Sphere(P(1.0 + 0.03 * n, 0, -0.04), 0.07 * u * hk * n), 0.05 * u, tag)
    f.add(F.Sphere(P(1.06 + 0.05 * n, 0, -0.29), 0.1 * u * n), 0.05 * u, tag)
    for sy in (-1, 1):
        f.add(F.Ellipsoid(P(0.96 + 0.02 * n, sy * 0.115 * n, -0.33), (0.065 * u * n, 0.07 * u * n, 0.06 * u * n)),
              0.04 * u, tag)
        f.cut(F.Sphere(P(0.99 + 0.03 * n, sy * 0.072 * n, -0.39), 0.033 * u * n), 0.02 * u)
    # lips and mouth
    lp = s['lips'] * (1 + 0.4 * fem)
    sm = s['smile']
    mo = s['open_mouth']
    f.add(F.Chain([P(0.86, -0.19, -0.53 + 0.04 * sm), P(0.97, 0, -0.5), P(0.86, 0.19, -0.53 + 0.04 * sm)],
                  0.048 * u * lp), 0.05 * u, tag)
    f.add(F.Chain([P(0.84, -0.14, -0.6 - mo * 0.08), P(0.93, 0, -0.62 - mo * 0.1), P(0.84, 0.14, -0.6 - mo * 0.08)],
                  0.058 * u * lp), 0.05 * u, tag)
    f.cut(F.Chain([P(0.86, -0.22, -0.56 + 0.05 * sm), P(1.0, 0, -0.565 - mo * 0.05),
                   P(0.86, 0.22, -0.56 + 0.05 * sm)], (0.012 + mo * 0.05) * u), 0.012 * u)
    # creases only where age or a scowl puts them
    crease = max(s['age'], 0.4 * (1 - fem) * s['frown'] - 0.2)
    if crease > 0:
        for sy in (-1, 1):
            f.cut(F.Chain([P(0.93, sy * 0.19, -0.36), P(0.86, sy * 0.29, -0.56), P(0.8, sy * 0.29, -0.7)],
                          0.016 * u * crease), 0.04 * u)
    if s['gaunt']:
        g = s['gaunt']
        for sy in (-1, 1):
            f.cut(F.Ellipsoid(P(0.72, sy * 0.5, -0.38), (0.16 * u, 0.12 * u, 0.2 * u)), 0.12 * u * g)
    # ears
    ea = s['ears'] * (1 - 0.12 * fem)
    for sy in (-1, 1):
        R = Matrix.Rotation(math.radians(-sy * 12), 3, 'X') @ Matrix.Rotation(math.radians(14), 3, 'Y')
        f.add(F.Ellipsoid(P(-0.02, sy * 0.76, -0.1 + 0.08 * s['elf']), (0.15 * u * ea, 0.06 * u * ea,
                                                                         (0.24 + 0.1 * s['elf']) * u * ea), R),
              0.06 * u, tag)
        f.cut(F.Ellipsoid(P(0.0, sy * 0.82, -0.1), (0.08 * u * ea, 0.04 * u * ea, 0.13 * u * ea), R), 0.03 * u)
    marks = dict(eyes=eyes, nose_tip=P(1.16, 0, -0.29), chin=P(0.75, 0, -1.08), mouth=P(0.95, 0, -0.56),
                 crown=P(-0.08, 0, 1.04), brow=P(0.9, 0, 0.18))
    return f, marks


def human_head(C, u, mats, coll, h=None, brows=True, **kw):
    """Head mesh with eyes, eyelids and eyebrows. mats: skin, eye (sclera/iris), brow (hair)."""
    s = face_params(**kw)
    f, marks = head_field(C, u, **kw)
    # eyelids hug the eyeball: an upper lid shell and a lower lid rim
    e = s['eye'] * (1 + 0.24 * s['female'])
    er = 0.12 * u * e
    lid_drop = (0.25 + 0.25 * s['frown']) * (1 - 0.75 * s['female'])
    for ec in marks['eyes']:
        ball = ec + Vector((0.06 * u, 0, 0))
        upper = F.Fn(lambda P, c=ball, r=er, d=lid_drop: _lid(P, c, r, d, upper=True),
                     ball - Vector((er, er, er)) * 1.4, ball + Vector((er, er, er)) * 1.4)
        lower = F.Fn(lambda P, c=ball, r=er: _lid(P, c, r, 0.0, upper=False),
                     ball - Vector((er, er, er)) * 1.4, ball + Vector((er, er, er)) * 1.4)
        f.add(upper, 0.015 * u, {'head': 1})
        f.add(lower, 0.015 * u, {'head': 1})
    head = f.mesh(h or u * 0.016, coll, mats['skin'], name='Head', smooth=4)
    head['face_mesh'] = True
    head['head_u'] = u
    LAST['field'] = f
    LAST['C'], LAST['u'] = C, u
    out = [head]
    for ec in marks['eyes']:
        ball = ec + Vector((0.06 * u, 0, 0))
        out.append(eyeball(ball, er, mats['eye'], coll))
        if mats.get('brow') is not None:
            # lash line along the upper lid (bold for women, a thin dark rim for men)
            lf = F.Field('Lashes')
            th = (0.022 if s['female'] else 0.012) * u
            pts = []
            for k in range(7):
                a = math.radians(-70 + k * 140 / 6)
                zc = er * (0.62 - lid_drop) * 1.0
                rr = er * 1.22
                y = math.sin(a) * rr * 0.95
                z = zc + (rr - abs(y)) * 0.18 + 0.01 * u
                x = math.sqrt(max(0.0, rr * rr - y * y - (z) ** 2)) + 0.004 * u
                pts.append(ball + Vector((x, y, z)))
            lf.add(F.Chain(pts, th), 0.0)
            if s['female']:
                sy = 1 if pts[-1].y > ball.y else -1
                outer = pts[-1] if abs(pts[-1].y - C.y) > abs(pts[0].y - C.y) else pts[0]
                lf.add(F.RoundCone(outer, outer + Vector((-0.02 * u, (outer.y - ball.y) * 0.5, 0.05 * u)), th, th * 0.3),
                       0.004 * u)
            out.append(lf.mesh(u * 0.008, coll, mats['brow'], name='Lashes', smooth=1))
    if brows and mats.get('brow') is not None:
        out += eyebrows(C, u, mats['brow'], coll, s)
    return out


def _lid(P, c, r, drop, upper=True):
    """Shell just outside the eyeball, kept above (upper) or below (lower) a tilted cut."""
    q = P - np.asarray(tuple(c))
    shell = np.abs(np.linalg.norm(q, axis=1) - r * 1.22) - r * 0.13
    if upper:
        cut = -(q[:, 2] - r * (0.62 - drop) + q[:, 0] * 0.0)
    else:
        cut = q[:, 2] + r * 0.62
    front = -q[:, 0] + r * 0.05
    return np.maximum(np.maximum(shell, cut), front)


def eyeball(c, r, mat, coll):
    e = geo.sphere('Eye', r, c, mat, coll, 24, 16)
    me = e.data
    loc = me.attributes.new('eye_local', 'FLOAT_VECTOR', 'POINT')
    co = np.zeros(len(me.vertices) * 3)
    me.vertices.foreach_get('co', co)
    co = (co.reshape(-1, 3) - np.asarray(tuple(c))) / r
    loc.data.foreach_set('vector', co.ravel())
    return e


def eyebrows(C, u, mat, coll, s):
    out = []
    fr = s['frown']
    th = 1 - 0.45 * s['female']
    for sy in (-1, 1):
        pts = [_p(C, u, 0.97, sy * 0.1, 0.17 - 0.07 * fr), _p(C, u, 0.95, sy * 0.3, 0.24 - 0.03 * fr),
               _p(C, u, 0.82, sy * 0.54, 0.27)]
        f = F.Field('Brow')
        f.add(F.Chain(pts, [0.075 * u * th, 0.085 * u * th, 0.045 * u * th], squash=(1.0, 0.6)), 0.02 * u)
        out.append(f.mesh(u * 0.02, coll, mat, name='Eyebrow', smooth=2))
    return out


# ------------------------------------------------------------------ hair and beards
def _head_offset(field, t, C, u, region=None, fade=0.25, boost=None):
    """The head grown by `t`, tapering to the skin across `fade` (in u) inside `region` (negative inside),
    so hairlines and beard edges feather instead of ending in a ledge."""
    lo = np.asarray(tuple(C)) - 1.8 * u
    hi = np.asarray(tuple(C)) + 1.8 * u
    if region is None:
        return F.Fn(lambda P: field.eval(P) - t, lo, hi)

    def fn(P):
        hd = field.eval(P)
        rd = region(P)
        w = np.clip(-rd / (fade * u), 0, 1)
        w = w * w * (3 - 2 * w)
        tt = t * (1 + boost(P)) if boost is not None else t
        return np.maximum(hd - tt * w + 0.003 * (1 - w), rd - 0.004)
    return F.Fn(fn, lo, hi)


def grooves(axis_fn, freq, depth, seed=0):
    """Sculpted strand grooves: axis_fn(P) -> the coordinate that varies across the strands."""
    def disp(P):
        a = axis_fn(P) * freq + F.noise3(P, 6.0, seed, 1) * 1.2
        return depth * (np.abs(np.sin(a)) ** 0.6 - 0.5)
    return disp


def _local(C, u):
    c = np.asarray(tuple(C))
    return lambda P: (P - c) / u


def beard(C, u, mat, coll, style='full', length=1.0, width=1.0, seed=3, moustache=True, braids=0, head=None,
          thickness=0.11):
    """Beard grown over the head sculpt (`head` field, default the last head). style: full | short |
    goatee | forked | chinstrap."""
    head = head or LAST['field']
    L = length
    loc = _local(C, u)
    f = F.Field('Beard')
    if style in ('full', 'short', 'forked', 'chinstrap'):
        t = (thickness if style != 'chinstrap' else 0.06) * u
        cheek = 0.0 if style != 'short' else -0.08

        def region(P):
            q = loc(P)
            # cheek line from the sideburn (z -0.05 at the ear) down to the mouth corners
            line = -0.05 + cheek - np.clip(q[:, 0] + 0.05, 0, 1) * 0.42 * (1 - 0.3 * width)
            below = q[:, 2] - line
            front = -0.12 - q[:, 0]
            side = np.abs(q[:, 1]) - 0.95 * width
            return np.maximum(np.maximum(below, front), side) * u
        f.add(_head_offset(head, t, C, u, region, 0.4), 0.0)
        # length below the chin
        if style != 'chinstrap':
            drop = 0.2 + 0.55 * L if style != 'short' else 0.1
            P = lambda x, y, z: _p(C, u, x, y, z)
            f.add(F.Ellipsoid(P(0.66, 0, -0.98 - drop * 0.5), (0.28 * u, 0.4 * u * width, (0.22 + drop * 0.5) * u)),
                  0.18 * u)
            if style == 'forked':
                for sy in (-1, 1):
                    f.add(F.RoundCone(P(0.7, sy * 0.15, -1.0), P(0.78, sy * 0.2, -1.2 - drop), 0.16 * u, 0.05 * u),
                          0.1 * u)
            else:
                f.add(F.RoundCone(P(0.66, 0, -0.95), P(0.76, 0, -1.08 - drop), 0.24 * u * width, 0.07 * u), 0.14 * u)
            # pointed tufts along the lower edge
            rng = np.random.default_rng(seed)
            for i in range(5):
                a = (i / 4 - 0.5) * 1.6 * width
                top = P(0.55 + 0.25 * math.cos(a), 0.45 * math.sin(a), -0.85 - drop * 0.3)
                tip = P(0.62 + 0.3 * math.cos(a), 0.55 * math.sin(a), -1.0 - drop * rng.uniform(0.75, 1.05))
                f.add(F.RoundCone(top, tip, 0.14 * u, 0.03 * u), 0.1 * u)
        # keep the mouth clear
        f.cut(F.Ellipsoid(_p(C, u, 1.0, 0, -0.58), (0.2 * u, 0.2 * u, 0.07 * u)), 0.04 * u)
    elif style == 'goatee':
        P = lambda x, y, z: _p(C, u, x, y, z)
        f.add(F.RoundCone(P(0.84, 0, -0.7), P(0.88, 0, -1.08 - 0.35 * L), 0.15 * u, 0.06 * u), 0.05 * u)
    if moustache and style != 'chinstrap':
        P = lambda x, y, z: _p(C, u, x, y, z)
        for sy in (-1, 1):
            f.add(F.Chain([P(1.04, sy * 0.02, -0.43), P(1.0, sy * 0.19, -0.48), P(0.9, sy * 0.3, -0.62 - 0.08 * L)],
                          [0.065 * u, 0.06 * u, 0.028 * u], squash=(1.0, 0.75)), 0.05 * u)
    for k in range(braids):
        P = lambda x, y, z: _p(C, u, x, y, z)
        sy = (k - (braids - 1) / 2) * 0.25
        f.add(F.Chain([P(0.8, sy, -1.1 - 0.4 * L), P(0.84, sy, -1.5 - 0.5 * L)], [0.08 * u, 0.04 * u]), 0.04 * u)
    if not f.ops:
        return []
    # strands run downward: grooves vary around the jaw
    c = np.asarray(tuple(C))
    f.disp = grooves(lambda P: np.arctan2(P[:, 1] - c[1], P[:, 0] - c[0] + 0.3 * u), 9.0, 0.016 * u, seed)
    return [f.mesh(u * 0.018, coll, mat, name='Beard', smooth=3)]


def hair(C, u, mat, coll, style='short', length=1.0, volume=1.0, seed=1, part=0.0, hairline=0.0, head=None):
    """Hair grown over the head sculpt. style: short | swept | long | shaggy | bun | topknot | bald_ring |
    cropped."""
    head = head or LAST['field']
    P = lambda x, y, z: _p(C, u, x, y, z)
    loc = _local(C, u)
    f = F.Field('Hair')
    thick = {'cropped': 0.05, 'short': 0.1, 'swept': 0.13, 'shaggy': 0.15, 'long': 0.12, 'bun': 0.1,
             'topknot': 0.08, 'bald_ring': 0.09, 'framed': 0.1}[style] * volume
    hl = hairline

    def region(P_):
        q = loc(P_)
        # hairline: high at the forehead, down to the nape behind the ears
        z0 = 0.42 + hl - 0.55 * np.clip(-q[:, 0] + 0.25, 0, 1.4)
        r = -(q[:, 2] - z0)
        if style == 'bald_ring':
            r = np.maximum(r, 0.55 - np.hypot(q[:, 0] + 0.2, q[:, 1] * 0.9) - np.clip(q[:, 2] - 0.4, 0, 1) * 0.6)
        return r * u
    c0 = np.asarray(tuple(C))
    crown = {'swept': 0.7, 'shaggy': 0.6}.get(style, 0.3)

    def boost(P_):
        q = (P_ - c0) / u
        # volume over the crown and the back, thin where the hair meets the face
        up = crown * np.clip((q[:, 2] - 0.5) / 0.5, 0, 1) * np.clip(1 - np.abs(q[:, 1] - part * 0.4) / 0.9, 0, 1)
        return up - 0.55 * np.clip((q[:, 0] - 0.35) / 0.4, 0, 1)
    f.add(_head_offset(head, thick * u, C, u, region, 0.45, boost), 0.0)
    # clear the ears
    for sy in (-1, 1):
        f.cut(F.Ellipsoid(P(-0.02, sy * 0.8, -0.12), (0.22 * u, 0.2 * u, 0.3 * u)), 0.05 * u)
    rng = np.random.default_rng(seed)
    if style in ('long', 'shaggy'):
        L = 0.55 + 0.6 * length
        # a soft curtain down the back of the head to the shoulders
        f.add(F.Ellipsoid(P(-0.35, 0, -0.35 - L * 0.4), (0.55 * u, 0.82 * u, (0.6 + L * 0.5) * u)), 0.25 * u)
        f.cut(F.Ellipsoid(P(0.55, 0, -0.6 - L * 0.4), (0.6 * u, 0.7 * u, (0.65 + L * 0.5) * u)), 0.15 * u)
        # locks framing the face
        for sy in (-1, 1):
            f.add(F.RoundCone(P(0.35, sy * 0.72, 0.35), P(0.2, sy * 0.78, -0.75 - 0.3 * length), 0.17 * u, 0.09 * u,
                              squash=(0.55, 1.0), up=(0, 1, 0)), 0.12 * u)
    if style == 'framed':
        for sy in (-1, 1):
            f.add(F.RoundCone(P(0.5, sy * 0.55, 0.55), P(0.45, sy * 0.72, -0.55 - 0.3 * length), 0.16 * u, 0.07 * u,
                              squash=(0.5, 1.0), up=(0, 1, 0)), 0.12 * u)
    if style == 'shaggy':
        for sy in (-1, 1):
            f.add(F.RoundCone(P(0.45, sy * 0.4, 0.62), P(0.72, sy * 0.58, 0.28), 0.14 * u, 0.05 * u), 0.08 * u)
    if style == 'bun':
        f.add(F.Sphere(P(-0.75, 0, 0.55), 0.3 * u), 0.1 * u)
    if style == 'topknot':
        f.add(F.RoundCone(P(-0.3, 0, 0.9), P(-0.5, 0, 1.35), 0.2 * u, 0.12 * u), 0.08 * u)
    c = np.asarray(tuple(C))
    # strands sweep back over the crown: grooves vary side to side
    f.disp = grooves(lambda P_: (P_[:, 1] - c[1]) / u + 0.15 * (P_[:, 2] - c[2]) / u, 6.0, 0.02 * u, seed)
    return [f.mesh(u * 0.018, coll, mat, name='Hair', smooth=3)]


def monster_head(C, u, mats, coll, kind='ghoul', glow=True):
    """Ghoul (gaunt, long jaw, pointed ears, needle teeth) or brute (heavy brow, underbite, tusks)."""
    P = lambda x, y, z: _p(C, u, x, y, z)
    if kind == 'ghoul':
        kw = dict(gaunt=1.0, nose=0.35, ears=1.5, elf=1.3, jaw=0.9, chin=0.8, brow=1.3, frown=1.0, open_mouth=0.9,
                  cheek=1.3, cranium=0.95)
    else:
        kw = dict(nose=0.8, ears=0.7, jaw=1.5, chin=1.4, brow=1.8, frown=1.2, open_mouth=0.5, cheek=1.4, cranium=0.85,
                  age=0.4)
    f, marks = head_field(C, u, **kw)
    if kind == 'ghoul':
        # elongated skull and sunken temples
        f.add(F.Ellipsoid(P(-0.45, 0, 0.3), (0.62 * u, 0.55 * u, 0.6 * u)), 0.25 * u, {'head': 1})
        for sy in (-1, 1):
            f.cut(F.Ellipsoid(P(0.35, sy * 0.8, 0.15), (0.25 * u, 0.12 * u, 0.25 * u)), 0.08 * u)
        # mouth pulled wide
        f.cut(F.Ellipsoid(P(0.95, 0, -0.62), (0.18 * u, 0.42 * u, 0.12 * u)), 0.04 * u)
    else:
        f.add(F.Ellipsoid(P(0.55, 0, -0.85), (0.45 * u, 0.62 * u, 0.32 * u)), 0.2 * u, {'head': 1})
        f.cut(F.Ellipsoid(P(1.0, 0, -0.62), (0.15 * u, 0.4 * u, 0.08 * u)), 0.04 * u)
    head = f.mesh(u * 0.018, coll, mats['skin'], name='Head', smooth=4)
    head['face_mesh'] = True
    LAST['field'], LAST['C'], LAST['u'] = f, C, u
    out = [head]
    # glowing eyes deep in the sockets
    for ec in marks['eyes']:
        e = F.Field('Eye glow')
        e.add(F.Sphere(ec + Vector((0.08 * u, 0, -0.01 * u)), 0.085 * u), 0.0)
        out.append(e.mesh(u * 0.012, coll, mats['glow'] if glow else mats['eye'], name='Eye glow', smooth=1))
    t = F.Field('Teeth')
    if kind == 'ghoul':
        for row, z in ((1, -0.56), (-1, -0.7)):
            for i in range(9):
                a = (i - 4) / 4 * 0.9
                p = P(0.8 * math.cos(a) + 0.12, 0.42 * math.sin(a), z)
                t.add(F.RoundCone(p, p + Vector((0.02 * u, 0, -row * 0.09 * u)), 0.035 * u, 0.004 * u), 0.004 * u)
    else:
        for i in range(6):
            a = (i - 2.5) / 2.5 * 0.7
            p = P(0.85 * math.cos(a) + 0.12, 0.48 * math.sin(a), -0.7)
            t.add(F.RoundCone(p, p + Vector((0.02 * u, 0, 0.08 * u)), 0.045 * u, 0.02 * u), 0.004 * u)
        for sy in (-1, 1):
            p = P(0.85, sy * 0.38, -0.72)
            t.add(F.RoundCone(p, p + Vector((0.04 * u, sy * 0.04 * u, 0.3 * u)), 0.07 * u, 0.012 * u), 0.004 * u)
    out.append(t.mesh(u * 0.012, coll, mats.get('bone', mats['skin']), name='Teeth', smooth=2))
    return out
