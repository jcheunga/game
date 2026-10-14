"""2D heraldic shapes as signed-distance functions (numpy), for embroidered and painted emblems.

Coordinates are in emblem units: the shape spans roughly -1..1 horizontally (u) and vertically (v).
Each emblem is a function (u, v) -> signed distance (negative inside), composed from boxes, circles,
segments and polygons.
"""
import numpy as np


def circle(cx, cy, r):
    return lambda u, v: np.hypot(u - cx, v - cy) - r


def box(cx, cy, hw, hh, r=0.0):
    def fn(u, v):
        qx = np.abs(u - cx) - hw + r
        qy = np.abs(v - cy) - hh + r
        return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r
    return fn


def segment(ax, ay, bx, by, r):
    def fn(u, v):
        pax, pay = u - ax, v - ay
        bax, bay = bx - ax, by - ay
        h = np.clip((pax * bax + pay * bay) / (bax * bax + bay * bay), 0, 1)
        return np.hypot(pax - bax * h, pay - bay * h) - r
    return fn


def polygon(pts):
    pts = np.asarray(pts, dtype=np.float64)

    def fn(u, v):
        d = np.full(u.shape, 1e9)
        inside = np.zeros(u.shape, bool)
        n = len(pts)
        for i in range(n):
            ax, ay = pts[i]
            bx, by = pts[(i + 1) % n]
            pax, pay = u - ax, v - ay
            bax, bay = bx - ax, by - ay
            h = np.clip((pax * bax + pay * bay) / (bax * bax + bay * bay), 0, 1)
            d = np.minimum(d, np.hypot(pax - bax * h, pay - bay * h))
            cond = ((ay > v) != (by > v)) & (u < (bx - ax) * (v - ay) / (by - ay + 1e-12) + ax)
            inside ^= cond
        return np.where(inside, -d, d)
    return fn


def union(*fns):
    return lambda u, v: np.min([f(u, v) for f in fns], axis=0)


def sub(a, *bs):
    return lambda u, v: np.max([a(u, v)] + [-b(u, v) for b in bs], axis=0)


def outline(fn, w):
    """A ring of width w around the shape's edge."""
    return lambda u, v: np.abs(fn(u, v)) - w


# ------------------------------------------------------------------ the factions' marks
def crowned_lantern():
    """Lantern Caravan: a lantern under a three-pointed crown (the banner mark on the roster sheet)."""
    band = box(0, 0.58, 0.46, 0.07, 0.02)
    points = union(polygon([(-0.46, 0.6), (-0.46, 0.92), (-0.2, 0.7)]),
                   polygon([(-0.18, 0.62), (0.0, 1.0), (0.18, 0.62)]),
                   polygon([(0.46, 0.6), (0.46, 0.92), (0.2, 0.7)]))
    orbs = union(circle(-0.44, 0.93, 0.07), circle(0.0, 1.02, 0.075), circle(0.44, 0.93, 0.07))
    crown = union(band, points, orbs)
    cap = union(polygon([(-0.38, 0.32), (0.38, 0.32), (0.2, 0.44), (-0.2, 0.44)]), box(0, 0.47, 0.12, 0.04, 0.02))
    frame = sub(box(0, -0.08, 0.34, 0.4, 0.05), box(0, -0.08, 0.22, 0.29, 0.03))
    bar = box(0, -0.08, 0.035, 0.29)
    base = union(box(0, -0.53, 0.42, 0.06, 0.02), box(0, -0.66, 0.28, 0.06, 0.02))
    return union(crown, cap, frame, bar, base)


def lantern_flame():
    """The lantern's glowing panes (rendered as lit enamel behind the frame)."""
    return box(0, -0.08, 0.23, 0.3, 0.03)


def rot_star():
    """Rotbound Host: a cross whose arms flare to points, with short diagonal rays (the crimson banners)."""
    arms = []
    for ang in (0, 90, 180, 270):
        a = np.radians(ang)
        c, s = np.cos(a), np.sin(a)
        L = 0.98 if ang in (90, 270) else 0.8
        pts = [(0.0, 0.13), (L * 0.62, 0.08), (L * 0.86, 0.2), (L, 0.0), (L * 0.86, -0.2), (L * 0.62, -0.08),
               (0.0, -0.13)]
        arms.append(polygon([(x * c - y * s, x * s + y * c) for x, y in pts]))
    diag = []
    for ang in (45, 135, 225, 315):
        a = np.radians(ang)
        c, s = np.cos(a), np.sin(a)
        pts = [(0.1, 0.07), (0.5, 0.0), (0.1, -0.07)]
        diag.append(polygon([(x * c - y * s, x * s + y * c) for x, y in pts]))
    return sub(union(*arms, *diag, circle(0, 0, 0.2)), circle(0, 0, 0.08))


def skull():
    head = union(circle(0, 0.18, 0.5), box(0, -0.25, 0.3, 0.2, 0.08))
    eyes = union(circle(-0.2, 0.12, 0.14), circle(0.2, 0.12, 0.14))
    nose = polygon([(0, -0.02), (0.07, -0.15), (-0.07, -0.15)])
    teeth = union(*[box(x, -0.38, 0.035, 0.08, 0.01) for x in (-0.15, -0.05, 0.05, 0.15)])
    return sub(head, eyes, nose, teeth)


def chevron():
    return polygon([(-0.8, -0.3), (0.0, 0.5), (0.8, -0.3), (0.8, 0.0), (0.0, 0.8), (-0.8, 0.0)])


def sun():
    rays = []
    for i in range(12):
        a = i * np.pi / 6
        rays.append(polygon([(np.cos(a) * 0.95, np.sin(a) * 0.95), (np.cos(a + 0.18) * 0.45, np.sin(a + 0.18) * 0.45),
                             (np.cos(a - 0.18) * 0.45, np.sin(a - 0.18) * 0.45)]))
    return union(circle(0, 0, 0.42), *rays)


EMBLEMS = {'lantern': crowned_lantern, 'rot': rot_star, 'skull': skull, 'chevron': chevron, 'sun': sun}
