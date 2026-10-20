#!/usr/bin/env python3
"""Cut a puppet painting into cutout-animation layers.

  art/remaster/puppet/run.sh art/remaster/puppet/layers.py <unit dir> [--preview out.png]

The unit dir holds master.png (the painting), landmarks.json, and Codex edits of the master:
  body.png    the same figure with the weapon and off-hand item painted out (absent for unarmed units)
  torso.png   body.png with both arms painted out, the torso behind them painted in (absent for armless kinds)
Edits drift a little (the torso edit rescales), so each is registered onto the master (SIFT + RANSAC similarity)
before diffing. What an edit removed is a layer, taken from the image before the edit:
  weapon.png / offhand.png   master pixels the body edit removed, split by the weapon line and the off-hand point
  arm_R.png / arm_L.png      body pixels the torso edit removed, split by the arm landmark chains
  base.png                   the body, with the torso edit painted in where the arms were
All layers share the master's frame, so the rig's landmarks hold for every layer. layers.json records the draw order.
"""
import argparse
import json
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

BG = np.array([221, 217, 209], np.float32)
N = 1024


def load(p):
    return np.array(Image.open(p).convert('RGBA').resize((N, N), Image.LANCZOS)).astype(np.float32)


def flat(im):
    a = im[..., 3:4] / 255
    return (im[..., :3] * a + BG * (1 - a)).astype(np.uint8)


def register(src, dst):
    """Similarity transform taking src onto dst; SIFT matches inside the figures, RANSAC rejects what the edit changed."""
    sift = cv2.SIFT_create(4000)
    g = lambda im: cv2.cvtColor(flat(im), cv2.COLOR_RGB2GRAY)
    ks, ds = sift.detectAndCompute(g(src), (src[..., 3] > 128).astype(np.uint8))
    kd, dd = sift.detectAndCompute(g(dst), (dst[..., 3] > 128).astype(np.uint8))
    good = [a for a, b in cv2.BFMatcher().knnMatch(ds, dd, k=2) if a.distance < 0.75 * b.distance]
    ps = np.float32([ks[m.queryIdx].pt for m in good])
    pd = np.float32([kd[m.trainIdx].pt for m in good])
    M, inl = cv2.estimateAffinePartial2D(ps, pd, method=cv2.RANSAC, ransacReprojThreshold=2.0)
    return M, int(inl.sum()), len(good)


def warp(im, M):
    return cv2.warpAffine(im, M, (N, N), flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=0)


def diff(x, y):
    lab = lambda im: cv2.cvtColor(flat(im), cv2.COLOR_RGB2LAB).astype(np.float32)
    d = np.linalg.norm(lab(x) - lab(y), axis=2)
    # one opaque where the other is clear always counts (the part stuck out past the figure)
    return np.maximum(d, np.abs(x[..., 3] - y[..., 3]) * 0.5)


def ell(r):
    return cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * r + 1, 2 * r + 1))


def fill_holes(m):
    h, w = m.shape
    ff = m.copy()
    mask = np.zeros((h + 2, w + 2), np.uint8)
    cv2.floodFill(ff, mask, (0, 0), 255)
    return m | cv2.bitwise_not(ff)


def components(m):
    n, lab, stats, _ = cv2.connectedComponentsWithStats(m, 8)
    return [(lab == i).astype(np.uint8) * 255 for i in range(1, n)], stats[1:]


def capsule(points, r):
    m = np.zeros((N, N), np.uint8)
    pts = [tuple(int(v) for v in p) for p in points if p]
    for a, b in zip(pts, pts[1:]):
        cv2.line(m, a, b, 255, int(2 * r))
    for p in pts:
        cv2.circle(m, p, int(r), 255, -1)
    return m


def cleaned(d, thresh, open_r, close_r):
    m = (d > thresh).astype(np.uint8) * 255
    m = cv2.morphologyEx(m, cv2.MORPH_OPEN, ell(open_r))      # edge lines from the edit's resampling go
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, ell(close_r))
    return fill_holes(m)


BLOB_WEAPONS = {'bow-draw', 'flask-toss', 'crossbow'}   # curved or compact things in the hand: no straight shaft to band


def profile_of(d):
    m = Path(d).parents[1] / 'units' / Path(d).name / 'metadata.json'
    return json.loads(m.read_text())['motion']['profile'] if m.exists() else ''


def blob_weapon(strong, lm, torso_len, solid=False, master=None):
    """A bow, flask or other held thing: the strong-diff pieces around its grip-to-tip span, holes filled."""
    grip, tip = lm.get('weapon_grip'), lm.get('weapon_tip')
    near = capsule([grip, tip], torso_len * 0.12)
    weapon = np.zeros((N, N), np.uint8)
    for c, st in zip(*components(strong)):
        if st[cv2.CC_STAT_AREA] >= 60 and (c & near).any():
            weapon |= c
    weapon = fill_holes(cv2.morphologyEx(weapon, cv2.MORPH_CLOSE, ell(4)))
    if solid and weapon.any() and master is not None:
        # a bomb, a flask, a rock: solid, so the diff's rim of it is filled to its hull (a bow is left as cut)
        hull = np.zeros((N, N), np.uint8)
        cv2.fillConvexPoly(hull, cv2.convexHull(np.argwhere(weapon > 0)[:, ::-1].astype(np.int32)), 255)
        weapon = hull & (master[..., 3] > 60).astype(np.uint8) * 255
        # a lit fuse: the bright glow touching it goes with it
        hsv = cv2.cvtColor(flat(master), cv2.COLOR_RGB2HSV)
        hue = hsv[..., 0]
        # a flame's yellow, orange or witch-fire green; never the crimson of the cloth around it
        glow = ((hsv[..., 2] > 150) & (hsv[..., 1] > 90) & (hue > 12) & (hue < 95)).astype(np.uint8) * 255
        glow &= capsule([grip, tip], torso_len * 0.3)
        glow = cv2.morphologyEx(glow, cv2.MORPH_CLOSE, ell(3))
        for c, st in zip(*components(glow)):
            if (c & cv2.dilate(weapon, ell(6))).any():
                weapon |= cv2.dilate(c, ell(3)) & (master[..., 3] > 30).astype(np.uint8) * 255
    g, t = np.array(grip, float), np.array(tip, float)
    return weapon, {'grip': [round(float(v), 1) for v in g], 'tip': [round(float(v), 1) for v in t],
                    'butt': [round(float(v), 1) for v in g], 'half_width': 0.0, 'blob': True}


HIDDEN = [None]     # the last banded weapon's band (its straight shaft or blade, whatever covers it)
FILL_REPORT = [1.0]  # the last filled weapon's continuity along its line (1 = whole)


def weapon_masks(master, body, lm, torso_len, method='band', mount=False, shield=False, flask=False):
    """The body edit removed the weapon and any off-hand item. A diff alone misses a shaft where it crosses paint of
    its own colour (a wooden haft over a leather belt), so a straight weapon is a band along its fitted centre line,
    as wide as the shaft measures where the diff is clear; heads, guards and pommels are the strong-diff pieces on it.
    Returns (weapon mask, off-hand mask, line) with line = {grip, butt, tip, half_width} or None."""
    strong = (diff(master, body) > 50).astype(np.uint8) * 255
    strong = cv2.morphologyEx(cv2.morphologyEx(strong, cv2.MORPH_OPEN, ell(2)), cv2.MORPH_CLOSE, ell(3))
    weapon, offhand, line = np.zeros((N, N), np.uint8), np.zeros((N, N), np.uint8), None
    band_mask = None
    grip, tip = lm.get('weapon_grip'), lm.get('weapon_tip')
    # the off-hand item (a shield the blade lies against): the weapon's line never runs on into it
    off_region = np.zeros((N, N), np.uint8)
    if lm.get('offhand_item'):
        offd = capsule([lm['offhand_item']], torso_len * 0.12)
        for c, st in zip(*components(strong)):
            if st[cv2.CC_STAT_AREA] >= 150 and (c & offd).any():
                off_region |= c
        off_region = fill_holes(cv2.morphologyEx(off_region, cv2.MORPH_CLOSE, ell(5)))
        if grip:
            # a sword held against the shield touches it: keep the weapon's own paint near the grip out of it
            off_region &= ~capsule([grip], torso_len * 0.18)
    strong_w = strong & ~off_region
    if grip and tip and method == 'blob':
        weapon, line = blob_weapon(strong, lm, torso_len, solid=flask, master=master)
    elif grip and tip:
        g, t = np.array(grip, float), np.array(tip, float)
        u0 = (t - g) / max(1e-6, np.linalg.norm(t - g))
        # the shaft or blade: the straight line through the grip with the most removed paint along it (the tip
        # landmark can sit on a hook or spike off the axis, so it only says which way is forward)
        ys, xs = np.nonzero(strong_w)
        pts_all = np.stack([xs, ys], 1).astype(np.float32)
        rel = pts_all - g
        best = None
        # within 30 degrees of the grip-to-tip line: a sword's crossguard holds more removed paint near the grip
        # than its blade does far along it, so a free search can lock onto the guard
        base_deg = float(np.degrees(np.arctan2(u0[1], u0[0])))
        span = float(np.linalg.norm(t - g))
        for deg in np.arange(base_deg - 30.0, base_deg + 30.5, 1.0):
            u_ = np.array([np.cos(np.radians(deg)), np.sin(np.radians(deg))])
            dd = np.abs(rel @ np.array([-u_[1], u_[0]]))
            ss = rel @ u_
            # only along the weapon's own span (a shield beside the grip lies on the infinite line too)
            score = int(((dd < 9) & (ss > -0.35 * span) & (ss < 1.15 * span)).sum())
            if best is None or score > best[0]:
                best = (score, u_)
        u_b = best[1] if best[1] @ u0 > 0 else -best[1]
        band0 = np.abs(rel @ np.array([-u_b[1], u_b[0]])) < max(14.0, torso_len * 0.04)
        if band0.sum() > 50:
            pts = pts_all[band0]
            vx, vy, x0, y0 = cv2.fitLine(pts, cv2.DIST_HUBER, 0, 0.01, 0.01).ravel()
            u = np.array([vx, vy], float)
            u = u if u @ u_b > 0 else -u
            nrm = np.array([-u[1], u[0]])
            p0 = np.array([x0, y0], float)
            cap = torso_len * 0.07
            D = (pts_all - p0) @ nrm
            pts = pts_all[np.abs(D) < cap]       # all the paint near the fitted line, however thick the shaft
            S = (pts - p0) @ u
            D = (pts - p0) @ nrm
            # half-width: per 6px slice along the axis, how far the paint reaches from the centre line; the median
            # slice is shaft (or blade), whatever the head does
            widths = []
            for lo in np.arange(S.min(), S.max(), 6):
                sel = (S >= lo) & (S < lo + 6) & (np.abs(D) < cap)
                if sel.sum() >= 12:
                    widths.append(np.percentile(np.abs(D[sel]), 90))
            hw = float(max(3.0, np.median(widths))) if widths else 6.0
            on = np.abs(D) <= hw * 1.5
            # the weapon runs out from the grip as far as removed paint continues along its line: a long gap
            # (the torso between a sword's pommel and a shield behind it) ends it
            gs = float((g - p0) @ u)
            bins = np.zeros(int(2 * N / 4) + 2, bool)
            idx = ((S[on] + N) / 4).astype(int).clip(0, len(bins) - 1)
            bins[idx] = True
            gap = max(3 * hw, torso_len * 0.4) / 4
            # toward the tip the landmark says how far the weapon reaches: a haft passing behind a pouch or the body
            # leaves a longer gap in the removed paint than a pommel's distance to a shield behind it
            gap_fwd = max(gap, 0.6 * float(np.linalg.norm(t - g)) / 4)
            g_bin = int((gs + N) / 4)

            def run(step):
                k, last = g_bin, g_bin
                while 0 <= k < len(bins) and (k - last) * step <= (gap_fwd if step > 0 else gap):
                    if bins[k]:
                        last = k
                    k += step
                return last
            s_butt = min(gs, run(-1) * 4 - N)
            s_tip = max(gs, run(1) * 4 - N + 4)
            if mount and np.linalg.norm(t - g) > 1.5 * torso_len:
                # a rider's lance crosses the rider (a teal shaft over a teal tabard leaves no diff) and nothing
                # sits behind it to mistake for it: it runs as far as its removed paint does, both ways
                s_butt = min(s_butt, float(np.percentile(S[on], 0.3)))
                s_tip = max(s_tip, float(np.percentile(S[on], 99.7)))
            yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
            SS = (xx - p0[0]) * u[0] + (yy - p0[1]) * u[1]
            DD = (xx - p0[0]) * nrm[0] + (yy - p0[1]) * nrm[1]
            band = ((np.abs(DD) <= hw) & (SS >= s_butt) & (SS <= s_tip) & (master[..., 3] > 8) & (off_region == 0))
            # where the band crosses paint the edit kept (a fist, a pouch in front of the haft) it is not weapon:
            # those stretches are painted in as shaft (fill_under_hands), not cut from what covers them
            band = band.astype(np.uint8) * 255
            HIDDEN[0] = band.copy()           # the whole band: build() works out what covers the weapon in it
            gs_ = float((g - p0) @ u)
            # near the shaft anywhere; on the head half (axe bits, hammer heads, polearm blades) as far as it goes
            if mount and np.linalg.norm(t - g) > 1.5 * torso_len:   # pennants and banners hang from behind the grip; the head is a slim point
                reach = (((np.abs(DD) <= max(torso_len * 0.2, 3 * hw)) | (SS <= gs_))
                         & (SS >= s_butt - torso_len) & (SS <= s_tip + hw))
            else:
                reach = (((np.abs(DD) <= max(torso_len * 0.2, 3 * hw)) | (SS >= gs_ + 0.3 * (s_tip - gs_)))
                         & (SS >= s_butt - hw) & (SS <= s_tip + torso_len * 0.6))
            weapon = band.copy()
            band_mask = band
            # heads, guards, pommels: strong-diff pieces on the band, but not between two hands on a shaft, where
            # the only thing besides shaft is the body the edit repainted
            wrists = [np.array(lm[f'wrist_{k}'], float) for k in 'RL' if lm.get(f'wrist_{k}')]
            ws = sorted(float((w - p0) @ u) for w in wrists if abs((w - p0) @ nrm) < torso_len * 0.15)
            between = (ws[0] + hw, ws[-1] - hw) if len(ws) == 2 else None
            off_pt = lm.get('offhand_item')
            off_zone = capsule([off_pt], torso_len * 0.12) if off_pt else None
            for c, st in zip(*components(strong_w & (reach.astype(np.uint8) * 255))):
                if st[cv2.CC_STAT_AREA] < 40 or not (c & cv2.dilate(band, ell(2))).any():
                    continue
                if off_zone is not None and (c & off_zone).any():
                    continue                  # the shield the blade lies against is the off-hand item
                cs = float(SS[c > 0].mean())
                if between and between[0] < cs < between[1]:
                    continue
                weapon |= c
            gs = (g - p0) @ u
            line = {'grip': [round(float(v), 1) for v in p0 + u * gs], 'tip': [round(float(v), 1) for v in p0 + u * s_tip],
                    'butt': [round(float(v), 1) for v in p0 + u * s_butt], 'half_width': round(hw, 1)}
    off = lm.get('offhand_item')
    if off:
        offd = capsule([off], torso_len * 0.12)
        for c, st in zip(*components(strong)):
            if st[cv2.CC_STAT_AREA] >= 150 and (c & offd).any():
                offhand |= c & (255 - weapon)
        offhand &= ~cv2.dilate(weapon, ell(3))
        if shield:
            # a shield is one piece: bits the edit removed apart from it (a plume, a weapon's head beside it) are not
            # part of it (tower shields are large, so no limit on its size)
            comps, stats = components(offhand)
            if comps:
                offhand = comps[int(np.argmax(stats[:, cv2.CC_STAT_AREA]))]
        offhand = fill_holes(cv2.morphologyEx(offhand, cv2.MORPH_CLOSE, ell(5)))
        # an off-hand item on a pole (a standard, a lantern staff): the pole runs on through the fist and below it.
        # Pieces taken for the weapon that lie along that pole (crossing the weapon's line) are the pole's
        off_side = None
        g_ = lm.get('weapon_grip')
        for s_ in 'RL':
            w_ = lm.get(f'wrist_{s_}')
            if w_ and (not g_ or np.hypot(*np.subtract(w_, g_)) > torso_len * 0.15):
                off_side = s_ if off_side is None or np.hypot(*np.subtract(w_, off)) < np.hypot(
                    *np.subtract(lm[f'wrist_{off_side}'], off)) else off_side
        if off_side and np.hypot(*np.subtract(lm[f'wrist_{off_side}'], off)) > torso_len * 0.6 and weapon.any():
            wo = np.array(lm[f'wrist_{off_side}'], float)
            po = np.array(off, float)
            pole = capsule([list(po), list(wo + (wo - po) * 0.9)], torso_len * 0.06)
            core = band_mask if band_mask is not None else np.zeros((N, N), np.uint8)
            for c, st in zip(*components(weapon & ~cv2.dilate(core, ell(1)))):
                inside = float(((c & pole) > 0).sum()) / max(1, float((c > 0).sum()))
                if inside > 0.7:
                    weapon &= ~c
                    offhand |= c
    return weapon, offhand, line


def mend_shield(master, before, after, weapon, line, torso_len):
    """A weapon lying across a shield takes a band out of it when the weapon alone replaces it, and cuts it in two
    (the shield is kept as its one largest piece, so the part beyond the band would be lost). What the weapon covered
    between the shield's pieces is painted in from either side of the band, across it (a rim or a plank runs on
    through). Returns (shield mask, pixels to cut it from)."""
    comps, st = components(after)
    if not comps or not line:
        return after, master
    big = st[:, cv2.CC_STAT_AREA].max()
    pieces = np.zeros((N, N), np.uint8)
    for c, s_ in zip(comps, st):
        if s_[cv2.CC_STAT_AREA] >= 0.08 * big:
            pieces |= c
    hull = np.zeros((N, N), np.uint8)
    cv2.fillConvexPoly(hull, cv2.convexHull(cv2.findNonZero(pieces)), 255)
    gap = hull & ~pieces & (before | cv2.dilate(weapon, ell(8))) & (master[..., 3] > 30).astype(np.uint8) * 255
    ys, xs = np.nonzero(gap)
    if len(xs) < 50:
        return after, master
    u = np.subtract(line['tip'], line['butt']).astype(float)
    u /= max(1e-6, np.linalg.norm(u))
    nrm = np.array([-u[1], u[0]])
    # across the band along the shield's own length where it has one (its rim and planks run that way), else straight
    # across the weapon
    hy, hx = np.nonzero(hull)
    ev, evec = np.linalg.eigh(np.cov(np.stack([hx, hy]).astype(float)))
    if ev[1] > 1.7 * ev[0] and abs(float(evec[:, 1] @ nrm)) > 0.5:
        nrm = evec[:, 1]
    # the shield's own paint, off the band's soft edge (the weapon's shadow and outline)
    src_ok = cv2.erode(pieces, ell(2)) > 0
    rgb = master[..., :3]
    found = []
    for sign in (1, -1):
        k_at = np.full(len(xs), np.inf)
        col = np.zeros((len(xs), 3))
        for k in range(1, int(torso_len * 0.6)):
            px = np.round(xs + sign * nrm[0] * k).astype(int)
            py = np.round(ys + sign * nrm[1] * k).astype(int)
            inb = (px >= 0) & (px < N) & (py >= 0) & (py < N) & np.isinf(k_at)
            hit = np.zeros(len(xs), bool)
            hit[inb] = src_ok[py[inb], px[inb]]
            k_at[hit] = k
            col[hit] = rgb[py[hit], px[hit]]
        found.append((k_at, col))
    (k1, c1), (k2, c2) = found
    # only between two pieces of the shield (not a sliver of the hull beside its rim)
    ok = np.isfinite(k1) & np.isfinite(k2)
    w1 = np.where(ok, k2 / np.maximum(1, np.where(ok, k1 + k2, 1)), 0)[:, None]
    src = master.copy()
    src[ys[ok], xs[ok], :3] = c1[ok] * w1[ok] + c2[ok] * (1 - w1[ok])
    src[ys[ok], xs[ok], 3] = 255
    filled = np.zeros((N, N), np.uint8)
    filled[ys[ok], xs[ok]] = 255
    # soften the streaks the stretch leaves, inside the band only
    soft = cv2.GaussianBlur(src[..., :3], (0, 0), 1.2)
    band = filled > 0
    src[..., :3][band] = soft[band]
    return pieces | filled, src


def axis_align(im, line):
    """A similarity transform taking a weapon painted alone (at any size and place) onto the weapon's line in the
    master: its long axis's ends, the heavier end (the head) onto the line's tip and the other onto its butt."""
    a = (im[..., 3] > 128)
    ys, xs = np.nonzero(a)
    if len(xs) < 200:
        return None
    pts = np.stack([xs, ys], 1).astype(np.float64)
    c = pts.mean(axis=0)
    w, v = np.linalg.eigh(np.cov((pts - c).T))
    ax = v[:, int(np.argmax(w))]
    t = (pts - c) @ ax
    lo, hi = t.min(), t.max()
    span = hi - lo
    mass_lo = int((t < lo + 0.2 * span).sum())
    mass_hi = int((t > hi - 0.2 * span).sum())
    e_lo, e_hi = c + ax * lo, c + ax * hi
    head, butt = (e_hi, e_lo) if mass_hi >= mass_lo else (e_lo, e_hi)
    T, B = np.array(line['tip'], float), np.array(line['butt'], float)
    src = np.float32([butt, head])
    dst = np.float32([B, T])
    # two points fix a similarity: solve for scale, rotation, shift
    vs, vd = src[1] - src[0], dst[1] - dst[0]
    sc = np.linalg.norm(vd) / max(1e-6, np.linalg.norm(vs))
    ang = np.arctan2(vd[1], vd[0]) - np.arctan2(vs[1], vs[0])
    R = sc * np.array([[np.cos(ang), -np.sin(ang)], [np.sin(ang), np.cos(ang)]])
    tvec = dst[0] - R @ src[0]
    return np.float32(np.hstack([R, tvec[:, None]])), float(sc)


def search_align(im, master, body, lm, avoid=None):
    """Place a weapon painted alone (at any size, angle and place) where it best covers the paint the body edit
    removed from the master, and least covers paint the edit kept: a search over angle (near the grip-to-tip
    landmarks') and scale, the shift found by correlation at each. Returns (2x3 transform, score) or None."""
    S = 4                                            # search at a quarter size
    n = N // S
    a = cv2.resize((im[..., 3] > 128).astype(np.float32), (n, n), interpolation=cv2.INTER_AREA)
    ys, xs = np.nonzero(a > 0.5)
    if len(xs) < 20:
        return None
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    tmpl0 = a[y0:y1, x0:x1]
    removed = cv2.resize(((diff(master, body) > 40) & (master[..., 3] > 128)).astype(np.float32), (n, n),
                         interpolation=cv2.INTER_AREA)
    kept = cv2.resize(((diff(master, body) < 15) & (master[..., 3] > 128)).astype(np.float32), (n, n),
                      interpolation=cv2.INTER_AREA)
    bg = cv2.resize((master[..., 3] < 128).astype(np.float32), (n, n), interpolation=cv2.INTER_AREA)
    target = removed - 1.5 * kept - 1.0 * bg
    if avoid is not None:                            # the off-hand item the edit also removed (a shield)
        target -= 2.5 * cv2.resize((avoid > 0).astype(np.float32), (n, n), interpolation=cv2.INTER_AREA)
    g, t = lm.get('weapon_grip'), lm.get('weapon_tip')
    want = np.degrees(np.arctan2(t[1] - g[1], t[0] - g[0])) if g and t else None
    # the painted-alone weapon's own axis angle
    pts = np.stack([xs, ys], 1).astype(np.float64)
    w, v = np.linalg.eigh(np.cov((pts - pts.mean(0)).T))
    ax = v[:, int(np.argmax(w))]
    # the axis pointing at the heavier end (the head): it must turn to point the way grip -> tip does
    tt = (pts - pts.mean(0)) @ ax
    span = tt.max() - tt.min()
    if int((tt > tt.max() - 0.25 * span).sum()) < int((tt < tt.min() + 0.25 * span).sum()):
        ax = -ax
    own = np.degrees(np.arctan2(ax[1], ax[0]))
    best = None
    for flip in ((0.0,) if want is not None else (0.0, 180.0)):
        base_rot = ((want if want is not None else own) - own + flip)
        for dr in np.arange(-24, 25, 3):
            rot = base_rot + dr
            for sc in np.arange(0.45, 1.25, 0.05):
                h_, w_ = tmpl0.shape
                M = cv2.getRotationMatrix2D((w_ / 2, h_ / 2), -rot, sc)
                cos, sin = abs(M[0, 0]), abs(M[0, 1])
                nw, nh = int(h_ * sin + w_ * cos) + 2, int(h_ * cos + w_ * sin) + 2
                M[0, 2] += nw / 2 - w_ / 2
                M[1, 2] += nh / 2 - h_ / 2
                tm = cv2.warpAffine(tmpl0, M, (nw, nh))
                if tm.shape[0] >= n or tm.shape[1] >= n or tm.sum() < 5:
                    continue
                res = cv2.matchTemplate(target.astype(np.float32), tm.astype(np.float32), cv2.TM_CCORR)
                _, mv, _, ml = cv2.minMaxLoc(res)
                # the most removed paint covered in all (a per-pixel average would favour a small placement that
                # fits neatly inside the removed paint)
                score = mv
                if best is None or score > best[0]:
                    best = (score, rot, sc, ml, (w_, h_), M, mv / max(1e-6, float(tm.sum())))
    if best is None:
        return None
    score, rot, sc, ml, (w_, h_), M, avg = best
    # compose: full-size crop offset -> rotate/scale about the crop -> place at ml, all at quarter size; then scale up
    T1 = np.array([[1, 0, -x0 * S], [0, 1, -y0 * S], [0, 0, 1]], float)
    Mf = M.copy()
    Mf[:, 2] *= S
    Mf = np.vstack([Mf, [0, 0, 1]])
    T2 = np.array([[1, 0, ml[0] * S], [0, 1, ml[1] * S], [0, 0, 1]], float)
    A = (T2 @ Mf @ T1)[:2]
    # refine at full size: small turns and scales about the coarse fit, the shift by correlation in a window
    full_a = (im[..., 3] > 128).astype(np.float32)
    rem_f = ((diff(master, body) > 40) & (master[..., 3] > 128)).astype(np.float32)
    kept_f = ((diff(master, body) < 15) & (master[..., 3] > 128)).astype(np.float32)
    tgt_f = rem_f - 1.5 * kept_f - 1.0 * (master[..., 3] < 128).astype(np.float32)
    if avoid is not None:
        tgt_f -= 2.5 * (avoid > 0).astype(np.float32)
    A3 = np.vstack([A, [0, 0, 1]])
    cx_, cy_ = A @ np.array([full_a.shape[1] / 2, full_a.shape[0] / 2, 1.0])
    best_f = (score * S * S, A, avg)
    pad = 28
    for dr in np.arange(-3, 3.5, 1.0):
        for ds in np.arange(-0.04, 0.045, 0.02):
            Rm = cv2.getRotationMatrix2D((float(cx_), float(cy_)), -dr, 1.0 + ds)
            B = (np.vstack([Rm, [0, 0, 1]]) @ A3)[:2]
            w_ = cv2.warpAffine(full_a, B, (N, N))
            ys2, xs2 = np.nonzero(w_ > 0.5)
            if len(xs2) < 50:
                continue
            bx0, bx1, by0, by1 = xs2.min(), xs2.max() + 1, ys2.min(), ys2.max() + 1
            tm = w_[by0:by1, bx0:bx1]
            wx0, wy0 = max(0, bx0 - pad), max(0, by0 - pad)
            win = tgt_f[wy0:min(N, by1 + pad), wx0:min(N, bx1 + pad)]
            if win.shape[0] < tm.shape[0] or win.shape[1] < tm.shape[1]:
                continue
            res = cv2.matchTemplate(win, tm, cv2.TM_CCORR)
            _, mv, _, ml2 = cv2.minMaxLoc(res)
            if mv > best_f[0]:
                Bs = B.copy()
                Bs[0, 2] += (wx0 + ml2[0]) - bx0
                Bs[1, 2] += (wy0 + ml2[1]) - by0
                best_f = (mv, Bs, mv / max(1e-6, float(tm.sum())))
    return np.float32(best_f[1]), float(best_f[2])


def held_weapon(d, master, lm, torso_len, info, method, cut=None, line=None, body=None, avoid=None):
    """held.png (Codex: the weapon alone, complete where the hands covered it) registered onto the master: the
    weapon layer itself, and its line (the straight line through the grip with the most of its paint on it, within
    25 degrees of the grip-to-tip landmarks, as long as its paint runs). (None, None) if it is unusable."""
    im = load(d / 'held.png')
    # Codex often paints the weapon alone larger, filling the frame: register at any scale, then check it lands on
    # the weapon the body edit took out of the master (most of that cut covered by it)
    try:
        M_, inl, good = register(im, master)
        sc = float(np.hypot(M_[0, 0], M_[0, 1]))
        if inl < 8 or not 0.2 < sc < 1.8:
            raise ValueError(f'{inl} inliers, scale {sc:.3f}')
        w_ = warp(im, M_)
        if cut is not None and (cut > 0).sum() > 200:
            cover = float(((w_[..., 3] > 64) & (cut > 0)).sum()) / float((cut > 0).sum())
            if cover < 0.55:
                raise ValueError(f'lands off the weapon ({cover:.2f} of it covered)')
        else:
            cover = None
        im = w_
        info['registration']['held'] = {'scale': round(sc, 4), 'shift': [round(float(v), 1) for v in M_[:, 2]],
                                        'inliers': inl, 'matches': good, 'covers_cut': cover}
    except Exception as e:  # noqa: BLE001
        # painted at another size or place: align it along the weapon's line instead, and keep it only if it then
        # lands on the weapon the body edit took out
        ok = False
        if lm.get('weapon_butt') and lm.get('weapon_end'):
            # its two ends placed by eye on the master: exact
            al = axis_align(im, {'butt': lm['weapon_butt'], 'tip': lm['weapon_end']})
            if al is not None:
                im, ok = warp(im, al[0]), True
                info['registration']['held'] = {'end_aligned': True, 'scale': round(al[1], 4)}
        al = None if ok else (search_align(im, master, body, lm, avoid) if body is not None else None)
        if al is not None:
            w_ = warp(im, al[0])
            cover = float(((w_[..., 3] > 64) & (cut > 0)).sum()) / max(1.0, float((cut > 0).sum())) \
                if cut is not None else 0.0
            # most of it on paint the edit removed, little on paint it kept (the cut itself may be what failed)
            if al[1] >= 0.3 or cover >= 0.55:
                im, ok = w_, True
                info['registration']['held'] = {'search_aligned': True, 'score': round(al[1], 3),
                                                'covers_cut': round(cover, 3)}
        if not ok:
            info['registration']['held'] = f'unusable ({e}; search score {al[1] if al else None})'
            return None, None
    grip, tip = lm.get('weapon_grip'), lm.get('weapon_tip')
    a = (im[..., 3] > 128).astype(np.uint8) * 255
    # stray bits Codex left away from the weapon go
    near = capsule([grip, tip], torso_len * 0.35)
    keep = np.zeros((N, N), np.uint8)
    for c, st in zip(*components(cv2.dilate(a, ell(3)))):
        if st[cv2.CC_STAT_AREA] >= 150 and (c & near).any():
            keep |= c
    im[..., 3] *= (keep > 0)
    a &= keep
    if (a > 0).sum() < 200:
        return None, None
    g, t = np.array(grip, float), np.array(tip, float)
    if method == 'blob':
        return im, {'grip': [round(float(v), 1) for v in g], 'tip': [round(float(v), 1) for v in t],
                    'butt': [round(float(v), 1) for v in g], 'half_width': 0.0, 'blob': True}
    ys, xs = np.nonzero(a)
    pts = np.stack([xs, ys], 1).astype(np.float32)
    rel = pts - g
    u0 = (t - g) / max(1e-6, np.linalg.norm(t - g))
    span = float(np.linalg.norm(t - g))
    base = float(np.degrees(np.arctan2(u0[1], u0[0])))
    best = None
    for deg in np.arange(base - 25.0, base + 25.5, 0.5):
        u_ = np.array([np.cos(np.radians(deg)), np.sin(np.radians(deg))])
        dd = np.abs(rel @ np.array([-u_[1], u_[0]]))
        ss = rel @ u_
        score = int(((dd < 7) & (ss > -0.6 * span) & (ss < 1.2 * span)).sum())
        if best is None or score > best[0]:
            best = (score, u_)
    u = best[1]
    nrm = np.array([-u[1], u[0]])
    S, D = rel @ u, rel @ nrm
    on = np.abs(D) < torso_len * 0.07
    widths = []
    for lo in np.arange(S[on].min(), S[on].max(), 6):
        sel = on & (S >= lo) & (S < lo + 6)
        if sel.sum() >= 8:
            widths.append(np.percentile(np.abs(D[sel]), 90))
    hw = float(max(3.0, np.median(widths))) if widths else 6.0
    shaft = np.abs(D) <= max(hw * 2.5, torso_len * 0.05)
    s_tip, s_butt = float(S[shaft].max()), float(min(0.0, S[shaft].min()))
    return im, {'grip': [round(float(v), 1) for v in g], 'tip': [round(float(v), 1) for v in g + u * s_tip],
                'butt': [round(float(v), 1) for v in g + u * s_butt], 'half_width': round(hw, 1)}


def cut_arrow(held, lm, torso_len):
    """A bow's nocked arrow, out of the weapon-alone painting: the thin straight run of paint along the line from
    the arrowhead back through the grip (as far as it continues), and the head itself. The stretch over the bow's
    grip stays with the bow (the bow hand covers it). Returns (arrow mask, nock, tip) or None."""
    g, t = np.array(lm['weapon_grip'], float), np.array(lm['weapon_tip'], float)
    if lm.get('arrow_nock'):
        # placed by eye: the arrow runs nock to tip (its line need not pass through the bow's grip)
        nk = np.array(lm['arrow_nock'], float)
        u = (t - nk) / max(1e-6, np.linalg.norm(t - nk))
        g = nk + u * float((g - nk) @ u)
    else:
        u = (t - g) / max(1e-6, np.linalg.norm(t - g))
    a = held[..., 3] > 128
    # the painted shaft: of the lines through the arrowhead within 8 degrees of the guide, the one with the most
    # paint on it (the guide from landmarks can be off the shaft by more than the shaft is wide)
    ys_, xs_ = np.nonzero(a)
    rel_ = np.stack([xs_, ys_], 1).astype(np.float32) - t
    base_deg = float(np.degrees(np.arctan2(-u[1], -u[0])))     # from the tip back toward the nock
    span = float(np.linalg.norm(t - g)) + torso_len
    best = None
    for deg in np.arange(base_deg - 8.0, base_deg + 8.05, 0.25):
        v = np.array([np.cos(np.radians(deg)), np.sin(np.radians(deg))])
        dd = np.abs(rel_ @ np.array([-v[1], v[0]]))
        ss = rel_ @ v
        score = int(((dd < 2.5) & (ss > torso_len * 0.12) & (ss < span)).sum())
        if best is None or score > best[0]:
            best = (score, v)
    u = -best[1]
    g = t - u * float((t - g) @ u)
    nrm = np.array([-u[1], u[0]])
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    S = (xx - g[0]) * u[0] + (yy - g[1]) * u[1]
    D = (xx - g[0]) * nrm[0] + (yy - g[1]) * nrm[1]
    band = a & (np.abs(D) <= 5)
    # run back from the grip as long as the shaft continues
    s_tip = float(np.linalg.norm(t - g))
    s_back = 0.0
    step, miss = 3.0, 0
    s_ = -step
    while s_ > -N:
        sl = band & (S > s_ - step) & (S <= s_)
        if sl.any():
            s_back, miss = s_, 0
        else:
            miss += 1
            if miss > max(5, int(torso_len * 0.14 / step)):      # across the bow hand
                break
        s_ -= step
    if lm.get('arrow_nock'):
        s_back = min(s_back, float((np.array(lm['arrow_nock'], float) - g) @ u))
    if s_tip - s_back < torso_len * 0.4:
        return None
    grip_zone = torso_len * 0.09
    m = band & (S >= s_back) & (S <= s_tip + 6) & (np.abs(S) > grip_zone)
    # the fletching: wider paint at the nock end
    m |= a & (S >= s_back - 4) & (S <= s_back + torso_len * 0.16) & (np.abs(D) <= torso_len * 0.045)
    s_t = float((t - g) @ u)
    head = a & (S >= s_t - torso_len * 0.26) & (S <= s_t + 8) & (np.abs(D) < torso_len * 0.085)
    m = (m | head).astype(np.uint8) * 255
    nock = g + u * s_back
    return m, [round(float(v), 1) for v in nock], [round(float(v), 1) for v in t]


def hand_point(lm, side):
    w, e = lm.get(f'wrist_{side}'), lm.get(f'elbow_{side}')
    if not (w and e):
        return w
    return [w[0] + (w[0] - e[0]) * 0.4, w[1] + (w[1] - e[1]) * 0.4]


def limb_owner(lm, torso_len):
    """For one edit that took arms and legs off together (limbs.png): which pixels lie nearer an arm's chain than a
    leg's. Returns (arm side mask, leg side mask)."""
    arms_d, legs_d = [], []
    for side in 'RL':
        ch = [lm.get(f'shoulder_{side}'), lm.get(f'elbow_{side}'), lm.get(f'wrist_{side}'), hand_point(lm, side)]
        if all(ch[:3]):
            arms_d.append(cv2.distanceTransform(255 - capsule([p for p in ch if p], 2), cv2.DIST_L2, 5))
        ch = [lm.get(f'{k}_{side}') for k in LEG_CHAIN]
        if all(ch[:3]):
            legs_d.append(cv2.distanceTransform(255 - capsule([p for p in ch if p], 2), cv2.DIST_L2, 5))
    if not arms_d or not legs_d:
        return None, None
    a, l_ = np.min(np.stack(arms_d), 0), np.min(np.stack(legs_d), 0)
    return (a <= l_).astype(np.uint8) * 255, (l_ < a).astype(np.uint8) * 255


def arm_masks(body, torso, lm, torso_len, allow=None):
    """The torso edit removed both arms: its diff inside each arm's capsule, the nearer chain taking any overlap."""
    d = diff(body, torso)
    m = cleaned(d, 30, 3, 9)
    if allow is not None:
        m &= allow
    out, dist = {}, {}
    for side in 'RL':
        chain = [lm.get(f'shoulder_{side}'), lm.get(f'elbow_{side}'), lm.get(f'wrist_{side}'), hand_point(lm, side)]
        if not all(chain[:3]):
            continue
        # how far from its bones an arm's paint can reach: wide for great pauldrons; set narrower by eye
        # (landmarks arm_reach) where a cape or a robe hangs right behind the arm
        cap = capsule(chain, torso_len * float(lm.get('arm_reach', 0.45)))
        line = capsule(chain, 2)
        dist[side] = cv2.distanceTransform(255 - line, cv2.DIST_L2, 5)
        comps, stats = components(m & cap)
        keep = np.zeros((N, N), np.uint8)
        for c, s in zip(comps, stats):     # the pieces along the arm's own chain
            if s[cv2.CC_STAT_AREA] > 300 and (c & capsule(chain, torso_len * 0.08)).any():
                keep |= c
        # the shoulder end tucks under the torso's shoulder cap: reach up to the joint within the body's paint
        sh = capsule([chain[0]], torso_len * 0.13) & (body[..., 3] > 20).astype(np.uint8) * 255
        out[side] = fill_holes(cv2.morphologyEx(keep | (sh & cv2.dilate(keep, ell(int(torso_len * 0.12)))),
                                                cv2.MORPH_CLOSE, ell(5)))
    # paint the edit erased outright (opaque in the body, clear in the torso) belongs to the nearer arm, or it would
    # stay behind on the body as a ghost of the sleeve's edge
    gone = ((body[..., 3] > 40) & (torso[..., 3] < 40)).astype(np.uint8) * 255
    gone = cv2.morphologyEx(gone, cv2.MORPH_OPEN, ell(1))
    if allow is not None:
        gone &= allow
    if out:
        sides = list(out)
        nearest = np.argmin(np.stack([dist[s_] for s_ in sides]), axis=0)
        for k, s_ in enumerate(sides):
            reach = (dist[s_] < torso_len * 0.6) & (nearest == k)
            out[s_] |= gone & reach.astype(np.uint8) * 255
    if len(out) == 2:
        both = (out['R'] > 0) & (out['L'] > 0)
        nearer_r = dist['R'] <= dist['L']
        out['R'][both & ~nearer_r] = 0
        out['L'][both & nearer_r] = 0
    for s_ in list(out):
        # thin strands (cape tatters, straps, a hem's fringe) swept in along the arm: an arm has nothing that thin,
        # so the mask opened with a small disc keeps the arm, and the arm is what is near that
        opened = cv2.morphologyEx(out[s_], cv2.MORPH_OPEN, ell(4))
        comps0, st0 = components(opened)
        if comps0:
            main = np.zeros((N, N), np.uint8)
            big = st0[:, cv2.CC_STAT_AREA].max()
            for c, st in zip(comps0, st0):
                if st[cv2.CC_STAT_AREA] >= 0.04 * big:
                    main |= c
            out[s_] = out[s_] & cv2.dilate(main, ell(4))
        # around the hand: paint the edit left unchanged (the cloth or belt the fist rests against, seen between and
        # beside the fingers) is the body's, swept into the arm by the mask's closing
        hp = hand_point(lm, s_)
        if hp:
            near_hand = capsule([hp], torso_len * 0.17)
            same = ((d < 14) & (body[..., 3] > 128) & (torso[..., 3] > 128)).astype(np.uint8) * 255
            same = cv2.morphologyEx(same, cv2.MORPH_OPEN, ell(2))
            out[s_] &= ~(same & near_hand)
            comps3, st3 = components(out[s_])
            if len(comps3) > 1:
                big = st3[:, cv2.CC_STAT_AREA].max()
                keep = np.zeros((N, N), np.uint8)
                for c, st in zip(comps3, st3):
                    if st[cv2.CC_STAT_AREA] >= 0.04 * big:
                        keep |= c
                out[s_] = keep
    # slivers of the edit's resampling (a torso edge, a strap) that joined an arm by nearness ride along with it as
    # it swings: an arm is its main piece and whatever is a sizeable part of it
    for s_, m_ in out.items():
        comps, stats = components(m_)
        if len(comps) > 1:
            big = max(st[cv2.CC_STAT_AREA] for st in stats)
            keep = np.zeros((N, N), np.uint8)
            for c, st in zip(comps, stats):
                if st[cv2.CC_STAT_AREA] >= 0.08 * big:
                    keep |= c
            out[s_] = keep
    # the head is never an arm's (a hunched brute's shoulder sits beside it, and an edit that repainted the face
    # put it in the arm's reach: the face then bent with the arm). A pauldron's top by the joint stays the arm's
    if lm.get('chin') and lm.get('head_top'):
        dhead = cv2.distanceTransform(255 - capsule([lm['chin'], lm['head_top']], 2), cv2.DIST_L2, 5)
        for s_ in list(out):
            # in the head's reach and far nearer its axis than the arm's own bones (a face, a jaw, a hood; not the
            # top of a pauldron by the helmet, which lies as near the shoulder as the head)
            head = capsule([lm['chin'], lm['head_top']], torso_len * 0.28) & \
                (dhead < 0.5 * dist[s_]).astype(np.uint8) * 255
            took = out[s_] & head
            if (took > 0).sum() > 400:
                rest = out[s_] & ~head
                comps, stats = components(rest)
                if comps:
                    big = stats[:, cv2.CC_STAT_AREA].max()
                    out[s_] = np.zeros((N, N), np.uint8)
                    for c, st in zip(comps, stats):
                        if st[cv2.CC_STAT_AREA] >= 0.08 * big:
                            out[s_] |= c
                    HEAD_TRIM[s_] = int((took > 0).sum())
    return out


HEAD_TRIM = {}
LEG_CHAIN = ('hip', 'knee', 'ankle', 'toe')


def leg_masks(torso, hips, lm, torso_len, allow=None):
    """The hips edit took both legs off: its diff, split between the two legs' landmark chains (the nearer chain takes
    what both reach). Returns {side: mask}."""
    m = cleaned(diff(torso, hips), 30, 3, 7)
    gone = ((torso[..., 3] > 40) & (hips[..., 3] < 40)).astype(np.uint8) * 255
    m |= cv2.morphologyEx(gone, cv2.MORPH_OPEN, ell(1))
    if allow is not None:
        m &= allow
    chains, dist = {}, {}
    for side in 'RL':
        chain = [lm.get(f'{k}_{side}') for k in LEG_CHAIN]
        if all(chain[:3]):
            chains[side] = [p for p in chain if p]
            dist[side] = cv2.distanceTransform(255 - capsule(chains[side], 2), cv2.DIST_L2, 5)
    out = {}
    if not chains:
        return out
    sides = list(chains)
    nearest = np.argmin(np.stack([dist[s_] for s_ in sides]), axis=0)
    # where the edit left nothing, the leg is everything near its chain; where it painted cloth over the leg (a
    # robe or tabard extended down), only a leg's width along the bones is leg: the rest is the cloth repainted
    clear = hips[..., 3] < 60
    for k, s_ in enumerate(sides):
        reach = ((dist[s_] < torso_len * 0.32) & clear | (dist[s_] < torso_len * 0.11)) & (nearest == k)
        cand = m & reach.astype(np.uint8) * 255
        keep = np.zeros((N, N), np.uint8)
        for c, st in zip(*components(cand)):
            if st[cv2.CC_STAT_AREA] > 300 and (c & capsule(chains[s_], torso_len * 0.06)).any():
                keep |= c
        out[s_] = fill_holes(cv2.morphologyEx(keep, cv2.MORPH_CLOSE, ell(5)))
    return out


def extend_top(lay, hip, knee, half_w, reach):
    """Carry a leg's paint up under the skirts to its hip joint: in the thigh's own frame (hip above knee), each column
    of the leg's top edge is continued upward to `reach` px above the hip. The base hides this at rest; it fills the
    gap a swinging thigh would open under the hem."""
    hip, knee = np.array(hip, float), np.array(knee, float)
    ang = np.degrees(np.arctan2(knee[0] - hip[0], knee[1] - hip[1]))      # thigh from straight down
    C = 2 * N
    R = cv2.getRotationMatrix2D((float(hip[0]), float(hip[1])), -ang, 1.0)
    R[:, 2] += (C / 2 - hip[0], C / 2 - hip[1])
    rot = cv2.warpAffine(lay, R, (C, C), flags=cv2.INTER_LINEAR, borderValue=0)
    out = rot.copy()
    cx, cy = C // 2, C // 2
    thigh = int(np.linalg.norm(knee - hip))
    lo = cy - int(reach)
    region = rot[lo: cy + thigh, int(cx - half_w): int(cx + half_w) + 1, 3] > 200
    rows = np.nonzero(region.any(axis=1))[0]
    if not len(rows):
        return lay
    top0 = int(rows[0])
    # only the columns the thigh itself fills just under its top edge: the extension is as wide as the thigh
    band = region[top0: top0 + max(6, thigh // 8)].any(axis=0)
    for j in np.nonzero(band)[0]:
        x = int(cx - half_w) + int(j)
        ys = np.nonzero(region[:, j])[0]
        top = lo + int(ys[0])
        if top <= lo:
            continue
        # the thigh's own colour, below the shadow the hem casts on it
        src = np.median(rot[top + 10: top + 28, x], axis=0) if top + 28 < C else rot[top + 2, x].copy()
        # under the skirts the thigh is in shadow: a dark leg where it shows through a slit or past a hem, never a
        # copy of the cloth that happened to lie at the leg's top
        src[:3] *= 0.45
        src[3] = 255
        out[lo: top + 2, x] = src
    Ri = cv2.invertAffineTransform(R)
    back = cv2.warpAffine(out, Ri, (N, N), flags=cv2.INTER_LINEAR, borderValue=0)
    return back


def cut_legs(d, body, torso, lm, torso_len, info, hips=None, allow=None):
    """Each leg as a layer of its own (so the legs can pass each other walking): the nearer, fully painted leg is cut
    from the body; the far leg, half hidden behind it, is drawn as a shaded copy of the near one moved onto its hip
    (in a side view the two legs are the same leg). Returns (base paint where the legs were, near-leg mask)."""
    if hips is None:
        hips = registered(d, 'hips.png', body, info)
    masks = leg_masks(torso, hips, lm, torso_len, allow)
    if not masks:
        return None, None
    near = max(masks, key=lambda s_: int((masks[s_] > 0).sum()))
    far = 'L' if near == 'R' else 'R'
    m = masks[near]
    lay = layer(body, m)
    hip, knee = lm[f'hip_{near}'], lm[f'knee_{near}']
    widths = (m > 0).sum(axis=1)
    half_w = float(np.percentile(widths[widths > 0], 60)) * 0.5 if (widths > 0).any() else torso_len * 0.1
    lay = extend_top(lay, hip, knee, half_w * 1.4, reach=torso_len * 0.08)
    save(lay, d / f'leg_{near}.png')
    # the far hip: in a three-quarter profile facing right the far hip shows a little forward of the near one (the
    # body turned toward the viewer) at the same height, whatever the landmarks say
    off = np.array([torso_len * 0.12, 0.0])
    shift = np.float32([[1, 0, off[0]], [0, 1, off[1]]])
    copy = warp(lay, shift)
    shade = np.array([0.74, 0.74, 0.8], np.float32)          # the far side is in the body's shadow, a touch cooler
    copy[..., :3] = copy[..., :3] * shade
    save(copy, d / f'leg_{far}.png')
    info['legs'] = {'near': near, 'far': far, 'far_offset': [round(float(v), 1) for v in off],
                    'pixels': int((m > 0).sum()), 'far_painted': int((masks.get(far, np.zeros(1)) > 0).sum())}
    # a robe that hides the legs down to the shins: the legs walk behind it. Otherwise the near leg draws in front
    # of the body (a cape hangs behind it), and what covered it in the painting (skirts, a tabard's front) is cut
    # from the body again as an overlay drawn over the leg
    ys = np.nonzero((m > 0).any(axis=1))[0]
    robed = bool(len(ys) and ys.min() > lm[f'knee_{near}'][1] - 0.15 * abs(lm[f'ankle_{near}'][1] - lm[f'knee_{near}'][1]))
    info['legs'].update(robed=robed, near_front=not robed)
    under = m.copy()
    if far in masks:
        under |= masks[far]
    k = np.clip(cv2.GaussianBlur(cv2.dilate(under, ell(int(torso_len * 0.03))).astype(np.float32), (0, 0),
                                 torso_len * 0.012) / 255, 0, 1)[..., None]
    cover = None
    if not robed:
        full = (lay[..., 3] > 30).astype(np.uint8) * 255                     # the leg as if nothing covered it
        cover = cv2.erode(full & ~cv2.dilate(m, ell(2)), ell(1))
        keep = np.zeros((N, N), np.uint8)
        for c, st in zip(*components(cover)):                              # slivers draw seams, not cloth
            if st[cv2.CC_STAT_AREA] > 400:
                keep |= c
        cover = cv2.dilate(keep, ell(1))
    return (hips, k), cover


def layer(src, mask, grow=1):
    """src's pixels inside mask; grown a pixel into src's own paint so no seam of the layer below shows."""
    m = cv2.dilate(mask, ell(grow)) if grow else mask
    m = cv2.GaussianBlur(m.astype(np.float32), (3, 3), 0) / 255
    out = src.copy()
    out[..., 3] = src[..., 3] * m
    return out


def save(im, p):
    Image.fromarray(np.clip(im, 0, 255).astype(np.uint8), 'RGBA').save(p)


def build(d, preview=None):
    d = Path(d)
    lm = json.loads((d / 'landmarks.json').read_text())
    kind = lm.get('kind', 'humanoid')
    if kind == 'mount':
        return finish(d, build_mount(d, lm), preview)
    if kind == 'machine':
        return finish(d, build_machine(d, lm), preview)
    master = load(d / 'master.png')
    torso_len = float(np.hypot(*(np.subtract(lm['neck'], lm['pelvis'])))) if lm.get('neck') and lm.get('pelvis') else 300
    info = {'order': [], 'registration': {}}
    body = master
    held = None
    if (d / 'body.png').exists():
        M, inl, good = register(load(d / 'body.png'), master)
        info['registration']['body'] = {'scale': round(float(np.hypot(M[0, 0], M[0, 1])), 4),
                                        'shift': [round(float(v), 1) for v in M[:, 2]], 'inliers': inl, 'matches': good}
        body = warp(load(d / 'body.png'), M)
        # bows, flasks and other compact things are cut whole; a long straight thing (a harpoon or spear carried by a
        # thrower) is banded along its shaft whatever the profile
        wlen = float(np.hypot(*np.subtract(lm['weapon_tip'], lm['weapon_grip']))) \
            if lm.get('weapon_tip') and lm.get('weapon_grip') else 0.0
        # a bomb, flask or rock is cut whole; anything as long as a shovel or a harpoon is banded along its haft
        method = 'blob' if profile_of(d) in ('bow-draw', 'crossbow') or (profile_of(d) in BLOB_WEAPONS and wlen < 0.75 * torso_len) \
            else 'band'
        HIDDEN[0] = None
        off_src = master
        weapon, offhand, wline = weapon_masks(master, body, lm, torso_len, method, shield=profile_of(d) == 'guard-cut',
                                              flask=profile_of(d) == 'flask-toss')
        if (weapon > 0).any():
            # what the edit removed in one connected piece with the weapon is the weapon (a lantern hanging from a
            # staff's hook, an axe's beard beyond the band): the band alone would cut it off and leave it hanging
            gone_ = cleaned(diff(master, body), 40, 3, 5)
            wd = cv2.dilate(weapon, ell(4))
            od = cv2.dilate(offhand, ell(4)) if (offhand > 0).any() else np.zeros_like(weapon)
            for c, st in zip(*components(gone_)):
                if (c & wd).any() and not (c & od).any() and st[cv2.CC_STAT_AREA] < 6 * (weapon > 0).sum():
                    weapon |= c
        if (d / 'held.png').exists() and lm.get('weapon_tip'):
            # avoid the off-hand item, but not near the weapon's own head (the two may have been cut as one)
            avoid = offhand & ~capsule([lm['weapon_tip']], torso_len * 0.25) if lm.get('weapon_tip') else offhand
            held, hline = held_weapon(d, master, lm, torso_len, info, method, cut=weapon, line=wline, body=body,
                                      avoid=avoid)
            if held is not None:
                weapon = (held[..., 3] > 8).astype(np.uint8) * 255
                off0 = offhand
                offhand = offhand & ~cv2.dilate(weapon, ell(8))
                if profile_of(d) == 'guard-cut' and (offhand > 0).any():
                    offhand, off_src = mend_shield(master, off0, offhand, weapon, hline, torso_len)
                    info['shield_mended'] = off_src is not master
                wline = hline
                info["weapon_source"] = "held"
        if (offhand > 0).any():
            # never the head (a helmet the edit happened to repaint) and, for a shield, one piece
            if lm.get('chin') and lm.get('head_top'):
                offhand &= ~capsule([lm['chin'], lm['head_top']], torso_len * 0.32)
            if profile_of(d) == 'guard-cut':
                comps_s, st_s = components(offhand)
                if comps_s:
                    offhand = comps_s[int(np.argmax(st_s[:, cv2.CC_STAT_AREA]))]
    else:
        weapon = offhand = np.zeros((N, N), np.uint8)
        wline = None
        off_src = master
    arms = {}
    base = body
    # torso.png took the arms off (and hips.png then the legs), or limbs.png took both off in one edit
    tname = 'torso.png' if (d / 'torso.png').exists() else 'limbs.png' if (d / 'limbs.png').exists() else None
    if tname:
        M, inl, good = register(load(d / tname), body)
        info['registration'][tname[:-4]] = {'scale': round(float(np.hypot(M[0, 0], M[0, 1])), 4),
                                            'shift': [round(float(v), 1) for v in M[:, 2]], 'inliers': inl, 'matches': good}
        torso = warp(load(d / tname), M)
        arm_ok, leg_ok = limb_owner(lm, torso_len) if tname == 'limbs.png' else (None, None)
        HEAD_TRIM.clear()
        arms = arm_masks(body, torso, lm, torso_len, arm_ok)
        if HEAD_TRIM:
            info['arm_head_trim'] = dict(HEAD_TRIM)
        # arm_cloth=R (set by eye, for bare or steel arms only): crimson tatters behind the fist that the edit
        # repainted came off with the arm; they are the body's and stay with it (else they flap off the swinging fist)
        for s_ in str(lm.get('arm_cloth') or ''):
            if s_ in arms:
                hsv = cv2.cvtColor(flat(body), cv2.COLOR_RGB2HSV)
                red = (((hsv[..., 0] < 8) | (hsv[..., 0] > 165)) & (hsv[..., 1] > 110) & (hsv[..., 2] > 45))
                red = cv2.morphologyEx(red.astype(np.uint8) * 255, cv2.MORPH_OPEN, ell(2))
                cloth = arms[s_] & cv2.dilate(red, ell(2))
                # sores and patches inside the arm are its own: only cloth at its edge goes
                rest = fill_holes(arms[s_] & ~cloth) & arms[s_]
                comps_a, st_a = components(cv2.morphologyEx(rest, cv2.MORPH_OPEN, ell(2)))
                if comps_a:
                    arms[s_] = comps_a[int(np.argmax(st_a[:, cv2.CC_STAT_AREA]))] & rest
                    info[f'arm_{s_}_cloth'] = int(((cloth > 0) & ~(arms[s_] > 0)).sum())
        # the torso edit's paint where the arms were (feathered); the body's own paint everywhere else
        under = np.zeros((N, N), np.uint8)
        for m in arms.values():
            under |= m
        k = np.clip(cv2.GaussianBlur(cv2.dilate(under, ell(int(torso_len * 0.05))).astype(np.float32), (0, 0),
                                     torso_len * 0.02) / 255, 0, 1)[..., None]
        base = body * (1 - k) + torso * k
        if tname == 'limbs.png' and leg_ok is not None:
            legs, cover = cut_legs(d, body, body, lm, torso_len, info, hips=torso, allow=leg_ok)
        elif (d / 'hips.png').exists():
            legs, cover = cut_legs(d, body, torso, lm, torso_len, info)
        else:
            legs, cover = None, None
        if legs is not None:
            if legs:
                hips, kl = legs
                base = base * (1 - kl) + hips * kl
                # the feather leaves faint traces of the legs' outlines where the edit painted nothing: invisible
                # standing, but they hang below a fallen body and prop it off the ground
                residue = (kl[..., 0] > 0.02) & (hips[..., 3] < 30)
                base[..., 3][residue] = 0
                if cover is not None and (cover > 0).sum() > 100:
                    save(layer(base, cover & (base[..., 3] > 20).astype(np.uint8) * 255, grow=0), d / 'skirt.png')
                    info['skirt'] = {'pixels': int((cover > 0).sum())}
    if arms:
        # islands of the body's paint cut off from it (claw tips, finger ends the arm cut missed) belong to the arm
        # beside them: left in the base they would hang in the air when the arm moves
        solid = (base[..., 3] > 40).astype(np.uint8) * 255
        comps, stats = components(solid)
        if len(comps) > 1:
            main = int(np.argmax(stats[:, cv2.CC_STAT_AREA]))
            near = {s_: cv2.distanceTransform(255 - m_, cv2.DIST_L2, 5) for s_, m_ in arms.items()}
            for k_, (c, st) in enumerate(zip(comps, stats)):
                if k_ == main or st[cv2.CC_STAT_AREA] > 0.05 * stats[main, cv2.CC_STAT_AREA]:
                    continue
                ys_, xs_ = np.nonzero(c)
                s_ = min(near, key=lambda q: float(near[q][ys_, xs_].min()))
                if float(near[s_][ys_, xs_].min()) < torso_len * 0.25:
                    grown = cv2.dilate(c, ell(2))
                    arms[s_] = arms[s_] | grown
                    base[..., 3][grown > 0] = 0
    if 'legs' in info:
        # scraps of the painted legs the leg cut missed (a heel, a toe) are islands of the base below the knees: the
        # legs are drawn by their own layers, so these would hang in the air as the legs swing
        solid = (base[..., 3] > 40).astype(np.uint8) * 255
        comps, stats = components(solid)
        if len(comps) > 1:
            main = int(np.argmax(stats[:, cv2.CC_STAT_AREA]))
            knee_y = min(lm[f'knee_{s_}'][1] for s_ in 'RL' if lm.get(f'knee_{s_}'))
            for k_, (c, st) in enumerate(zip(comps, stats)):
                if k_ == main or st[cv2.CC_STAT_AREA] > 0.05 * stats[main, cv2.CC_STAT_AREA]:
                    continue
                if st[cv2.CC_STAT_TOP] + st[cv2.CC_STAT_HEIGHT] / 2 > knee_y:
                    base[..., 3][cv2.dilate(c, ell(2)) > 0] = 0
                    info.setdefault('scraps', 0)
                    info['scraps'] += int(st[cv2.CC_STAT_AREA])
    if body is not master:
        # the body edit takes away everything carried, but only what is held is a layer: a pavise or quiver on the
        # back, a pack, a sheathed sword stay with the body (large pieces away from the weapon and the hands)
        gone = cleaned(diff(master, body), 40, 3, 5)
        held_ = cv2.dilate(weapon | offhand, ell(int(torso_len * 0.06)))
        for s_ in 'RL':
            hp_ = hand_point(lm, s_)
            if hp_:
                held_ |= capsule([hp_], torso_len * 0.2)
        keep = np.zeros((N, N), np.uint8)
        for c, st in zip(*components(gone & ~cv2.dilate(weapon | offhand, ell(2)))):
            # whole pieces, not the weapon or the off-hand item: one mostly away from the hands (a drum slung at
            # the hip that a mallet hand rests on) is carried, not held
            inside = float(((c & held_) > 0).sum()) / max(1.0, float((c > 0).sum()))
            # and attached: a good stretch of its outline lies against the body's own paint (a drum at the hip, a
            # quiver on the back); something floating clear beside the body is held in a hand
            ring = cv2.dilate(c, ell(4)) & ~c
            against = float(((ring > 0) & (base[..., 3] > 128)).sum()) / max(1.0, float((ring > 0).sum()))
            on_line = wline is not None and not wline.get('blob') and float(((c & capsule(
                [wline['butt'], wline['tip']], torso_len * 0.09)) > 0).sum()) > 0.3 * float((c > 0).sum())
            forced = any(c[int(p_[1]), int(p_[0])] for p_ in lm.get('carried') or [])
            listed = 'carried' in lm            # set by eye: exactly these, or none
            if forced or (not listed and st[cv2.CC_STAT_AREA] > 2500 and inside < 0.3 and against > 0.2 and not on_line):
                keep |= c
        if keep.any():
            kk = cv2.GaussianBlur(cv2.dilate(keep, ell(2)).astype(np.float32), (0, 0), 1.5)[..., None] / 255
            base = base * (1 - kk) + master * kk
            info['restored'] = int((keep > 0).sum())
    off_pt0 = lm.get('offhand_item')
    if off_pt0 and arms and profile_of(d) not in ('guard-cut',):
        # a flame or a spell burning in the palm: bright, saturated, not crimson; the arm cut takes it with the hand,
        # and it would stretch as the arm turns. It is the off-hand item (rigid on its own bone)
        hsv = cv2.cvtColor(flat(master), cv2.COLOR_RGB2HSV)
        hue = hsv[..., 0]
        core = ((hsv[..., 2] > 160) & (hsv[..., 1] > 100) & (hue > 12) & (hue < 95)).astype(np.uint8) * 255
        core &= capsule([off_pt0], torso_len * 0.26)
        core = cv2.morphologyEx(core, cv2.MORPH_CLOSE, ell(4))
        seed = capsule([off_pt0], torso_len * 0.07)
        fl = np.zeros((N, N), np.uint8)
        for c, st in zip(*components(core)):
            if (c & seed).any():
                fl |= c
        # its fainter wisps and tongues, as far as they reach from the core
        wisp = ((hsv[..., 2] > 120) & (hsv[..., 1] > 70) & (hue > 12) & (hue < 95)).astype(np.uint8) * 255
        wisp &= capsule([off_pt0], torso_len * 0.45)
        for c, st in zip(*components(cv2.morphologyEx(wisp, cv2.MORPH_CLOSE, ell(3)))):
            if (c & cv2.dilate(fl, ell(2))).any():
                fl |= c
        if (fl > 0).sum() > 300:
            fl = cv2.dilate(fl, ell(3)) & (master[..., 3] > 30).astype(np.uint8) * 255
            # a green, blue or violet spell's faint halo and curling wisps (half clear, its own hue) stayed with the
            # arm and hung in the air over the corpse when the flame went out. Not for orange flames: skin is that hue
            raw = cv2.cvtColor(np.clip(master[..., :3], 0, 255).astype(np.uint8), cv2.COLOR_RGB2HSV)
            hmed = float(np.median(raw[..., 0][fl > 0]))
            if 30 < hmed < 170:
                halo = np.zeros((N, N), bool)
                # in the master and in the body edit the arms are cut from (it repaints the halo, often less clear)
                for src_ in (master, body):
                    hs_ = cv2.cvtColor(np.clip(src_[..., :3], 0, 255).astype(np.uint8), cv2.COLOR_RGB2HSV)
                    dh = np.abs(((hs_[..., 0].astype(int) - int(hmed) + 90) % 180) - 90)
                    halo |= (dh < 14) & (hs_[..., 1] > 60) & (hs_[..., 2] > 60) & (src_[..., 3] > 8)
                # only the half-clear haze: a sleeve or cuff lit by the flame is opaque paint, and the arm's own
                halo &= master[..., 3] < 225
                halo = halo.astype(np.uint8) * 255 & capsule([off_pt0], torso_len * 0.6)
                near_fl = cv2.dilate(fl, ell(int(torso_len * 0.1)))
                wisps = np.zeros((N, N), np.uint8)
                for c, st in zip(*components(cv2.morphologyEx(halo, cv2.MORPH_CLOSE, ell(3)))):
                    if (c & near_fl).any():
                        wisps |= cv2.dilate(c, ell(1))
                # the body keeps none of it either (a faint stroke beside the corpse)
                base[..., 3][(wisps > 0) & ~(fl > 0)] = 0
                fl |= wisps
                info['offhand_halo'] = True
            for s_ in arms:
                arms[s_] = arms[s_] & ~fl
                # crumbs of the cuff and fingers the flame's cut left beside the wrist
                solid_ = cv2.bitwise_and(arms[s_], (body[..., 3] > 40).astype(np.uint8) * 255)
                comps_f, st_f = components(solid_)
                if len(comps_f) > 1:
                    big_f = st_f[:, cv2.CC_STAT_AREA].max()
                    for c, st in zip(comps_f, st_f):
                        if st[cv2.CC_STAT_AREA] < 0.03 * big_f:
                            arms[s_] = arms[s_] & ~cv2.dilate(c, ell(2))
            offhand = offhand | fl
            info['offhand_flame'] = int((fl > 0).sum())
    if off_pt0 and (offhand > 0).sum() > 200:
        # what the item is: a flame or a spell (bright, nothing dark in it but the fingers' shadow) goes out when
        # its caster dies; a lantern, a vial, a book (a dark frame, glass, leather) falls with the body
        hsv_ = cv2.cvtColor(flat(master), cv2.COLOR_RGB2HSV)
        sel = offhand > 0
        dark = float((hsv_[..., 2][sel] < 95).mean())
        bright = float(((hsv_[..., 2][sel] > 160) & (hsv_[..., 1][sel] > 90)).mean())
        info['offhand_kind'] = {'dark': round(dark, 3), 'bright': round(bright, 3),
                                'flame': bool(bright > 0.45 and dark < 0.12)}
    save(base, d / 'base.png')
    hands = np.zeros((N, N), np.uint8)
    for side, m in arms.items():
        lay = layer(body, m)
        save(lay, d / f'arm_{side}.png')
        # an arm whose paint lies over the torso must draw in front of it (else it would hide behind the painted-in
        # torso); one that only hangs off the figure's side is the far arm and draws behind
        over = float(((m > 0) & (base[..., 3] > 128)).sum()) / max(1, (m > 0).sum())
        info[f'arm_{side}'] = {'over_torso': round(over, 3), 'front': bool(over > 0.12), 'pixels': int((m > 0).sum())}
        hp = hand_point(lm, side)
        if hp:
            hands |= capsule([hp], torso_len * 0.16) & cv2.dilate(m, ell(3))
    grips = []
    if (weapon > 0).sum() > 200:
        lay = layer(master, weapon) if held is None else held
        span = wline
        if span and span.get('blob'):
            # held in one hand: the one nearest its grip
            info['weapon_line'] = span
            g = np.array(span['grip'], float)
            grips = [min(arms, key=lambda s_: np.linalg.norm(np.array(hand_point(lm, s_), float) - g))] if arms else []
            info['grips'] = grips
        elif span and held is not None:
            info['weapon_line'] = span
            g, b, t = (np.array(span[k], float) for k in ('grip', 'butt', 'tip'))
            for side in arms:
                w = np.array(lm[f'wrist_{side}'], float)
                hp = np.array(hand_point(lm, side), float)
                if min(seg_point(w, b, t), seg_point(hp, b, t)) < torso_len * 0.14:
                    grips.append(side)
            info['grips'] = grips
        elif span:
            covered = None
            if HIDDEN[0] is not None:
                # the weapon is the paint the edit took away, less the arms (a fist over a haft is the arm's); the
                # rest of its band is covered (fists, a pouch or the body in front, a haft over a belt of its own
                # colour) and is painted in as shaft where the shaft runs on both sides of it
                arms_u = np.zeros((N, N), np.uint8)
                for m_ in arms.values():
                    arms_u |= m_
                solid = ((diff(master, body) > 20) & (cv2.dilate(arms_u, ell(2)) == 0)).astype(np.uint8) * 255
                band_ = HIDDEN[0]
                weapon = (weapon & ~band_ & (cv2.dilate(arms_u, ell(3)) == 0)) | (band_ & solid)
                # specks the edit's resampling left near the weapon are not part of it
                keep = np.zeros((N, N), np.uint8)
                for c_, st_ in zip(*components(weapon)):
                    if st_[cv2.CC_STAT_AREA] >= 120:
                        keep |= c_
                weapon = keep
                lay = layer(master, weapon)
                covered = band_ & ~weapon
            FILL_REPORT[0] = 1.0
            lay = fill_under_hands(lay, span, hands, covered)
            info['weapon_line'] = span
            info['weapon_whole'] = round(FILL_REPORT[0], 3)
            # the hand nearest the grip holds it; the other too if its wrist sits on the weapon's line (two-handed)
            g, b, t = (np.array(span[k], float) for k in ('grip', 'butt', 'tip'))
            for side in arms:
                w = np.array(lm[f'wrist_{side}'], float)
                hp = np.array(hand_point(lm, side), float)
                dseg = min(seg_point(w, b, t), seg_point(hp, b, t))
                if dseg < torso_len * 0.14:
                    grips.append(side)
            info['grips'] = grips
        if profile_of(d) == 'bow-draw' and lm.get('weapon_tip'):
            cut = cut_arrow(held if held is not None else lay, lm, torso_len)
            if cut:
                am, nock, tip = cut
                arrow = (held if held is not None else lay).copy()
                arrow[..., 3] *= (am > 0)
                save(arrow, d / 'arrow.png')
                lay = lay.copy()
                lay[..., 3] *= (cv2.dilate(am, ell(1)) == 0)
                info['arrow'] = {'nock': nock, 'tip': tip, 'pixels': int((am > 0).sum())}
        save(lay, d / 'weapon.png')
        info['weapon'] = {'pixels': int((weapon > 0).sum())}
    off_pt = lm.get('offhand_item')
    if off_pt and (offhand > 0).sum() > 200 and profile_of(d) == 'flask-toss':
        # a compact held thing (a bomb, a flask, a lantern) the diff caught only in part (its rim): the whole of it
        # is the hull of what was caught around its landmark
        near = capsule([off_pt], torso_len * 0.2)
        part = offhand & near
        hull = np.zeros((N, N), np.uint8)
        if part.any():
            cv2.fillConvexPoly(hull, cv2.convexHull(np.argwhere(part > 0)[:, ::-1].astype(np.int32)), 255)
        compact = part.any() and (part > 0).sum() > 0.7 * (offhand > 0).sum()
        hollow = (hull > 0).sum() > 1.5 * (part > 0).sum()        # caught only as a rim
        roundish = False
        if part.any():
            (_, _), (rw, rh), _ = cv2.minAreaRect(np.argwhere(hull > 0)[:, ::-1].astype(np.float32))
            roundish = max(rw, rh) < 1.8 * max(1.0, min(rw, rh))   # a bomb or a flask, not a blade
        if compact and hollow and roundish:
            # only the item itself: cloth the diff caught further out is not part of it
            offhand = hull & (master[..., 3] > 60).astype(np.uint8) * 255
            for m_ in arms.values():
                offhand &= ~m_
            # a burning fuse or a glow the edit left behind on the body, touching the item: bright and saturated
            hsv = cv2.cvtColor(flat(master), cv2.COLOR_RGB2HSV)
            glow = ((hsv[..., 2] > 150) & (hsv[..., 1] > 90)).astype(np.uint8) * 255
            glow &= capsule([off_pt], torso_len * 0.32)
            glow = cv2.morphologyEx(glow, cv2.MORPH_CLOSE, ell(3))
            touch = np.zeros((N, N), np.uint8)
            for c, st in zip(*components(glow)):
                if (c & cv2.dilate(offhand, ell(6))).any():
                    touch |= c
            touch = cv2.dilate(touch, ell(3)) & (master[..., 3] > 30).astype(np.uint8) * 255
            # the same glow the edit repainted on the body (often a little off from where it was): it goes too
            hsv_b = cv2.cvtColor(flat(base), cv2.COLOR_RGB2HSV)
            glow_b = ((hsv_b[..., 2] > 140) & (hsv_b[..., 1] > 80)).astype(np.uint8) * 255
            glow_b &= capsule([off_pt], torso_len * 0.5)
            left = np.zeros((N, N), np.uint8)
            for c, st in zip(*components(cv2.morphologyEx(glow_b, cv2.MORPH_CLOSE, ell(3)))):
                if (c & cv2.dilate(offhand | touch, ell(int(torso_len * 0.1)))).any():
                    left |= c
            if touch.any() or left.any():
                offhand |= touch
                # the glow's soft halo reaches past its bright core: paint the body in across all of it
                hole = cv2.dilate(touch | left, ell(int(torso_len * 0.05))) & ~cv2.dilate(offhand & ~touch, ell(1))
                rgb = np.clip(base[..., :3], 0, 255).astype(np.uint8)
                fill = cv2.inpaint(rgb, hole, 9, cv2.INPAINT_TELEA)
                base[..., :3] = np.where(hole[..., None] > 0, fill, base[..., :3])
                save(base, d / 'base.png')
                info['offhand_glow'] = int((touch > 0).sum())
    if (offhand > 0).sum() > 200:
        # specks the edits left around the item are not part of it
        comps_o, st_o = components(offhand)
        if len(comps_o) > 1:
            big_o = st_o[:, cv2.CC_STAT_AREA].max()
            keep_o = np.zeros((N, N), np.uint8)
            for c, st in zip(comps_o, st_o):
                if st[cv2.CC_STAT_AREA] >= 0.03 * big_o:
                    keep_o |= c
            offhand = keep_o
        save(layer(off_src, offhand), d / 'offhand.png')
        info['offhand'] = {'pixels': int((offhand > 0).sum())}
    # draw order, back to front: arms hanging off the far side, the body, arms over the torso, the weapon, the hands
    # that hold it (a fist closes over the shaft); an off-hand item (shield, lantern) goes under the weapon, which
    # strikes past it
    free = [s for s in 'LR' if f'arm_{s}' in info and s not in grips]
    back_legs, front_legs = [], []
    if 'legs' in info:
        back_legs = [f"leg_{info['legs']['far']}"]
        if info['legs']['near_front']:
            front_legs = [f"leg_{info['legs']['near']}"] + (['skirt'] if 'skirt' in info else [])
        else:
            back_legs.append(f"leg_{info['legs']['near']}")
    info['order'] = (back_legs + [f'arm_{s}' for s in free if not info[f'arm_{s}']['front']] + ['base'] + front_legs +
                     [f'arm_{s}' for s in free if info[f'arm_{s}']['front']] + (['offhand'] if 'offhand' in info else []) +
                     (['weapon'] if 'weapon' in info else []) + (['arrow'] if 'arrow' in info else []) +
                     [f'arm_{s}' for s in 'LR' if s in grips])
    (d / 'layers.json').write_text(json.dumps(info, indent=1) + '\n')
    if preview:
        contact(d, info, preview)
    return info


def finish(d, info, preview):
    (d / 'layers.json').write_text(json.dumps(info, indent=1) + '\n')
    if preview:
        contact(d, info, preview)
    return info


def registered(d, name, onto, info):
    M, inl, good = register(load(d / name), onto)
    info['registration'][name[:-4]] = {'scale': round(float(np.hypot(M[0, 0], M[0, 1])), 4),
                                       'shift': [round(float(v), 1) for v in M[:, 2]], 'inliers': inl, 'matches': good}
    return warp(load(d / name), M)


def build_mount(d, lm):
    """A mount: its rider (what horse.png painted out) over the mount, the lance or weapon (what body.png painted
    out) over both."""
    master = load(d / 'master.png')
    info = {'registration': {}}
    span = max(1.0, float(np.hypot(*np.subtract(lm['horse_withers'], lm['horse_croup'])))) \
        if lm.get('horse_withers') and lm.get('horse_croup') else 300.0
    body, weapon, wline = master, np.zeros((N, N), np.uint8), None
    if (d / 'body.png').exists():
        body = registered(d, 'body.png', master, info)
        weapon, _, wline = weapon_masks(master, body, lm, span, mount=True)
    base, order = body, []
    if (d / 'horse.png').exists():
        horse = registered(d, 'horse.png', body, info)
        m = cleaned(diff(body, horse), 30, 3, 9)
        chain = [lm.get(k) for k in ('pelvis', 'chest', 'neck', 'head_top')]
        near = capsule([p for p in chain if p], span * 0.45)
        for s_ in 'RL':
            arm_ = [lm.get(f'{k}_{s_}') for k in ('shoulder', 'elbow', 'wrist')]
            near |= capsule([p for p in arm_ if p], span * 0.25)
        rider = np.zeros((N, N), np.uint8)
        for c, st in zip(*components(m & near)):
            if st[cv2.CC_STAT_AREA] > 300:
                rider |= c
        gone = ((body[..., 3] > 40) & (horse[..., 3] < 40)).astype(np.uint8) * 255
        rider |= gone & near
        rider = fill_holes(cv2.morphologyEx(rider, cv2.MORPH_CLOSE, ell(5)))
        k = np.clip(cv2.GaussianBlur(cv2.dilate(rider, ell(int(span * 0.04))).astype(np.float32), (0, 0),
                                     span * 0.02) / 255, 0, 1)[..., None]
        base = body * (1 - k) + horse * k
        save(layer(body, rider), d / 'rider.png')
        info['rider'] = {'pixels': int((rider > 0).sum())}
        order.append('rider')
    save(base, d / 'base.png')
    if (weapon > 0).sum() > 200:
        save(layer(master, weapon), d / 'weapon.png')
        info['weapon'] = {'pixels': int((weapon > 0).sum())}
        info['weapon_line'] = wline
        order.append('weapon')
    info['order'] = ['base'] + order
    return info


def rim_ellipse(mask, r):
    """The near wheel's rim as an ellipse (RANSAC over its outline: a wheel behind it, joined to it in the edit's
    diff, bulges the outline but is not on the ellipse). ((cx, cy), (w, h), angle) or None."""
    cnts, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    if not cnts:
        return None
    pts = max(cnts, key=len).reshape(-1, 2).astype(np.float32)
    if len(pts) < 30:
        return None
    rng = np.random.default_rng(0)
    best = None
    for _ in range(600):
        sel = pts[rng.choice(len(pts), 6, replace=False)]
        try:
            (ex, ey), (ew, eh), ang = cv2.fitEllipse(sel)
        except cv2.error:
            continue
        lo_, hi_ = sorted((ew, eh))
        if hi_ < 1e-3 or lo_ / hi_ < 0.55 or not (0.4 * r < hi_ / 2 < 1.6 * r):
            continue
        t = np.radians(ang)
        dx, dy = pts[:, 0] - ex, pts[:, 1] - ey
        u = (dx * np.cos(t) + dy * np.sin(t)) / (ew / 2)
        v = (-dx * np.sin(t) + dy * np.cos(t)) / (eh / 2)
        inl = int((np.abs(np.hypot(u, v) - 1) < 0.035).sum())
        if best is None or inl > best[0]:
            best = (inl, ((ex, ey), (ew, eh), ang))
    return best[1] if best and best[0] > 0.35 * len(pts) * 0.5 else None


def build_machine(d, lm):
    """A machine: each wheel nowheels.png painted out is a layer of its own; a whole round one turns as the machine
    rolls (its hub is the piece's centre), one partly hidden by the frame stays still."""
    master = load(d / 'master.png')
    info = {'registration': {}}
    base, order = master, []
    if (d / 'nowheels.png').exists():
        bare = registered(d, 'nowheels.png', master, info)
        m = cleaned(diff(master, bare), 35, 3, 5)
        wheels = []
        for c, st in zip(*components(m)):
            x, y, w, h, area = st
            if area < 1500 or not 0.6 < w / max(1, h) < 1.7:
                continue
            r = (w + h) / 4
            wheels.append((c, (x + w / 2, y + h / 2), r, area / (np.pi * r * r)))
        under = np.zeros((N, N), np.uint8)
        for k, (c, cen, r, full) in enumerate(sorted(wheels, key=lambda w_: w_[1][0])):
            name = f'wheel_{k}'
            # the wheel's own disc: pieces of a neighbouring wheel or the frame joined to it by a thin bridge would
            # orbit the hub when it turns
            el = rim_ellipse(c, r)
            if el is not None:
                m_ = np.zeros((N, N), np.uint8)
                (ex, ey), (ea, eb), eang = el
                cv2.ellipse(m_, ((ex, ey), (ea * 1.03, eb * 1.03), eang), 255, -1)
                c = c & m_
            ys_, xs_ = np.nonzero(c)
            x0_, x1_, y0_, y1_ = xs_.min(), xs_.max(), ys_.min(), ys_.max()
            cen = ((x0_ + x1_) / 2, (y0_ + y1_) / 2)
            save(layer(master, c), d / f'{name}.png')
            info[name] = {'center': [round(float(cen[0]), 1), round(float(cen[1]), 1)], 'radius': round(float(r), 1),
                          'axes': [round(float(x1_ - x0_) / 2, 1), round(float(y1_ - y0_) / 2, 1)],
                          'spin': bool(full > 0.72)}
            under |= c
            order.append(name)
        k_ = np.clip(cv2.GaussianBlur(cv2.dilate(under, ell(6)).astype(np.float32), (0, 0), 3) / 255, 0, 1)[..., None]
        base = master * (1 - k_) + bare * k_
    save(base, d / 'base.png')
    info['order'] = ['base'] + order
    return info


def seg_point(p, a, b):
    ab = b - a
    t = np.clip(((p - a) @ ab) / max(1e-9, ab @ ab), 0, 1)
    return float(np.linalg.norm(p - (a + t * ab)))


def fill_under_hands(lay, span, hands, other=None):
    """Paint the shaft where a fist covered it: in the weapon's own frame (axis horizontal) each row of the band is
    bridged across the hand from the shaft paint either side, so a hand moving along the shaft never shows a hole or
    a copy of its fingers."""
    g, t = np.array(span['grip']), np.array(span['tip'])
    hw = span['half_width']
    ang = np.degrees(np.arctan2(t[1] - g[1], t[0] - g[0]))
    C = 2 * N
    R = cv2.getRotationMatrix2D((float(g[0]), float(g[1])), ang, 1.0)
    R[:, 2] += (C / 2 - g[0], C / 2 - g[1])
    rot = cv2.warpAffine(lay, R, (C, C), flags=cv2.INTER_LINEAR, borderValue=0)
    cover = cv2.dilate(hands, ell(8))      # past the fingers' soft edges
    if other is not None:
        cover |= cv2.dilate(other, ell(1))
    hid = cv2.warpAffine(cover, R, (C, C), flags=cv2.INTER_NEAREST, borderValue=0) > 0
    b = np.array(span['butt'])
    x_lo = int(C / 2 - np.linalg.norm(g - b)) + 2
    x_hi = int(C / 2 + np.linalg.norm(t - g)) - 2
    out = rot.copy()
    filled = np.zeros((C, C), bool)
    cy = C // 2
    for y in range(int(cy - hw), int(cy + hw) + 1):
        gap = hid[y, x_lo:x_hi]
        if not gap.any():
            continue
        xs = np.arange(x_lo, x_hi)
        ok = ~gap & (rot[y, x_lo:x_hi, 3] > 128)
        if ok.sum() < 8:
            continue
        # each covered stretch is bridged only where the weapon shows on both sides of it in this row (a row that
        # only the head or a guard reaches is not shaft), blending the paint nearest it either side
        edges = np.flatnonzero(np.diff(np.concatenate(([0], gap.astype(np.int8), [0]))))
        ok_idx = np.flatnonzero(ok)
        for a, b in zip(edges[::2], edges[1::2]):
            left = ok_idx[ok_idx < a][-12:]
            right = ok_idx[ok_idx >= b][:12]
            if len(left) < 3 or len(right) < 3:
                continue
            cl, cr = np.median(rot[y, xs[left]], axis=0), np.median(rot[y, xs[right]], axis=0)
            w = np.linspace(0.0, 1.0, b - a)[:, None]
            out[y, xs[a:b]] = cl * (1 - w) + cr * w
            filled[y, xs[a:b]] = True
    # how whole the weapon came out: the share of its length (butt to tip, along the centre line) with paint on it.
    # A cut that leaves holes (a haft behind the body, a hand the shaft runs past) is better painted by Codex
    # (stride.py held stage)
    line = (out[int(cy - 2):int(cy + 3), x_lo:x_hi, 3] > 128).any(axis=0)
    FILL_REPORT[0] = float(line.mean()) if line.size else 1.0
    Ri = cv2.invertAffineTransform(R)
    back = cv2.warpAffine(out, Ri, (N, N), flags=cv2.INTER_LINEAR, borderValue=0)
    fm = cv2.warpAffine(filled.astype(np.uint8) * 255, Ri, (N, N), flags=cv2.INTER_NEAREST, borderValue=0) > 0
    res = lay.copy()
    res[fm] = back[fm]
    return res


def contact(d, info, out):
    """Each layer on grey, then the stack recomposited, then the master: the last two should match."""
    tiles = []
    stack = np.zeros((N, N, 4), np.float32)
    for name in info['order']:
        im = load(d / f'{name}.png')
        tiles.append(flat(im))
        a = im[..., 3:4] / 255
        stack[..., :3] = im[..., :3] * a + stack[..., :3] * (1 - a)
        stack[..., 3:4] = 255 * (a + stack[..., 3:4] / 255 * (1 - a))
    tiles += [flat(stack), flat(load(d / 'master.png'))]
    row = np.concatenate([cv2.resize(t, (384, 384), interpolation=cv2.INTER_AREA) for t in tiles], axis=1)
    Image.fromarray(row).save(out)


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('unit')
    ap.add_argument('--preview')
    ap.add_argument('--legs', action='store_true', help='(hips.png present) cut each leg as a layer')
    a = ap.parse_args()
    print(json.dumps(build(a.unit, a.preview)))
