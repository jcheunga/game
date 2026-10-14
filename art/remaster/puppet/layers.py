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


BLOB_WEAPONS = {'bow-draw', 'flask-toss'}   # curved or compact things in the hand: no straight shaft to band


def profile_of(d):
    m = Path(d).parents[1] / 'units' / Path(d).name / 'metadata.json'
    return json.loads(m.read_text())['motion']['profile'] if m.exists() else ''


def blob_weapon(strong, lm, torso_len):
    """A bow, flask or other held thing: the strong-diff pieces around its grip-to-tip span, holes filled."""
    grip, tip = lm.get('weapon_grip'), lm.get('weapon_tip')
    near = capsule([grip, tip], torso_len * 0.12)
    weapon = np.zeros((N, N), np.uint8)
    for c, st in zip(*components(strong)):
        if st[cv2.CC_STAT_AREA] >= 60 and (c & near).any():
            weapon |= c
    weapon = fill_holes(cv2.morphologyEx(weapon, cv2.MORPH_CLOSE, ell(4)))
    g, t = np.array(grip, float), np.array(tip, float)
    return weapon, {'grip': [round(float(v), 1) for v in g], 'tip': [round(float(v), 1) for v in t],
                    'butt': [round(float(v), 1) for v in g], 'half_width': 0.0, 'blob': True}


def weapon_masks(master, body, lm, torso_len, method='band', mount=False):
    """The body edit removed the weapon and any off-hand item. A diff alone misses a shaft where it crosses paint of
    its own colour (a wooden haft over a leather belt), so a straight weapon is a band along its fitted centre line,
    as wide as the shaft measures where the diff is clear; heads, guards and pommels are the strong-diff pieces on it.
    Returns (weapon mask, off-hand mask, line) with line = {grip, butt, tip, half_width} or None."""
    strong = (diff(master, body) > 50).astype(np.uint8) * 255
    strong = cv2.morphologyEx(cv2.morphologyEx(strong, cv2.MORPH_OPEN, ell(2)), cv2.MORPH_CLOSE, ell(3))
    weapon, offhand, line = np.zeros((N, N), np.uint8), np.zeros((N, N), np.uint8), None
    grip, tip = lm.get('weapon_grip'), lm.get('weapon_tip')
    if grip and tip and method == 'blob':
        weapon, line = blob_weapon(strong, lm, torso_len)
    elif grip and tip:
        g, t = np.array(grip, float), np.array(tip, float)
        u0 = (t - g) / max(1e-6, np.linalg.norm(t - g))
        # the shaft or blade: the straight line through the grip with the most removed paint along it (the tip
        # landmark can sit on a hook or spike off the axis, so it only says which way is forward)
        ys, xs = np.nonzero(strong)
        pts_all = np.stack([xs, ys], 1).astype(np.float32)
        rel = pts_all - g
        best = None
        for deg in np.arange(0.0, 180.0, 1.0):
            u_ = np.array([np.cos(np.radians(deg)), np.sin(np.radians(deg))])
            dd = np.abs(rel @ np.array([-u_[1], u_[0]]))
            score = int((dd < 9).sum())
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
            g_bin = int((gs + N) / 4)

            def run(step):
                k, last = g_bin, g_bin
                while 0 <= k < len(bins) and (k - last) * step <= gap:
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
            band = ((np.abs(DD) <= hw) & (SS >= s_butt) & (SS <= s_tip) & (master[..., 3] > 8))
            band = band.astype(np.uint8) * 255
            gs_ = float((g - p0) @ u)
            # near the shaft anywhere; on the head half (axe bits, hammer heads, polearm blades) as far as it goes
            if mount and np.linalg.norm(t - g) > 1.5 * torso_len:   # pennants and banners hang from behind the grip; the head is a slim point
                reach = (((np.abs(DD) <= max(torso_len * 0.2, 3 * hw)) | (SS <= gs_))
                         & (SS >= s_butt - torso_len) & (SS <= s_tip + hw))
            else:
                reach = (((np.abs(DD) <= max(torso_len * 0.2, 3 * hw)) | (SS >= gs_ + 0.3 * (s_tip - gs_)))
                         & (SS >= s_butt - hw) & (SS <= s_tip + torso_len * 0.6))
            weapon = band.copy()
            # heads, guards, pommels: strong-diff pieces on the band, but not between two hands on a shaft, where
            # the only thing besides shaft is the body the edit repainted
            wrists = [np.array(lm[f'wrist_{k}'], float) for k in 'RL' if lm.get(f'wrist_{k}')]
            ws = sorted(float((w - p0) @ u) for w in wrists if abs((w - p0) @ nrm) < torso_len * 0.15)
            between = (ws[0] + hw, ws[-1] - hw) if len(ws) == 2 else None
            for c, st in zip(*components(strong & (reach.astype(np.uint8) * 255))):
                if st[cv2.CC_STAT_AREA] < 40 or not (c & cv2.dilate(band, ell(2))).any():
                    continue
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
        offhand = fill_holes(cv2.morphologyEx(offhand, cv2.MORPH_CLOSE, ell(5)))
    return weapon, offhand, line


def hand_point(lm, side):
    w, e = lm.get(f'wrist_{side}'), lm.get(f'elbow_{side}')
    if not (w and e):
        return w
    return [w[0] + (w[0] - e[0]) * 0.4, w[1] + (w[1] - e[1]) * 0.4]


def arm_masks(body, torso, lm, torso_len):
    """The torso edit removed both arms: its diff inside each arm's capsule, the nearer chain taking any overlap."""
    d = diff(body, torso)
    m = cleaned(d, 30, 3, 9)
    out, dist = {}, {}
    for side in 'RL':
        chain = [lm.get(f'shoulder_{side}'), lm.get(f'elbow_{side}'), lm.get(f'wrist_{side}'), hand_point(lm, side)]
        if not all(chain[:3]):
            continue
        cap = capsule(chain, torso_len * 0.45)
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
    return out


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
    if (d / 'body.png').exists():
        M, inl, good = register(load(d / 'body.png'), master)
        info['registration']['body'] = {'scale': round(float(np.hypot(M[0, 0], M[0, 1])), 4),
                                        'shift': [round(float(v), 1) for v in M[:, 2]], 'inliers': inl, 'matches': good}
        body = warp(load(d / 'body.png'), M)
        # bows, flasks and other compact things are cut whole; a long straight thing (a harpoon or spear carried by a
        # thrower) is banded along its shaft whatever the profile
        wlen = float(np.hypot(*np.subtract(lm['weapon_tip'], lm['weapon_grip']))) \
            if lm.get('weapon_tip') and lm.get('weapon_grip') else 0.0
        method = 'blob' if profile_of(d) == 'bow-draw' or (profile_of(d) in BLOB_WEAPONS and wlen < 1.2 * torso_len) \
            else 'band'
        weapon, offhand, wline = weapon_masks(master, body, lm, torso_len, method)
    else:
        weapon = offhand = np.zeros((N, N), np.uint8)
        wline = None
    arms = {}
    base = body
    if (d / 'torso.png').exists():
        M, inl, good = register(load(d / 'torso.png'), body)
        info['registration']['torso'] = {'scale': round(float(np.hypot(M[0, 0], M[0, 1])), 4),
                                         'shift': [round(float(v), 1) for v in M[:, 2]], 'inliers': inl, 'matches': good}
        torso = warp(load(d / 'torso.png'), M)
        arms = arm_masks(body, torso, lm, torso_len)
        # the torso edit's paint where the arms were (feathered); the body's own paint everywhere else
        under = np.zeros((N, N), np.uint8)
        for m in arms.values():
            under |= m
        k = np.clip(cv2.GaussianBlur(cv2.dilate(under, ell(int(torso_len * 0.05))).astype(np.float32), (0, 0),
                                     torso_len * 0.02) / 255, 0, 1)[..., None]
        base = body * (1 - k) + torso * k
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
        lay = layer(master, weapon)
        span = wline
        if span and span.get('blob'):
            # held in one hand: the one nearest its grip
            info['weapon_line'] = span
            g = np.array(span['grip'], float)
            grips = [min(arms, key=lambda s_: np.linalg.norm(np.array(hand_point(lm, s_), float) - g))] if arms else []
            info['grips'] = grips
        elif span:
            lay = fill_under_hands(lay, span, hands)
            info['weapon_line'] = span
            # the hand nearest the grip holds it; the other too if its wrist sits on the weapon's line (two-handed)
            g, b, t = (np.array(span[k], float) for k in ('grip', 'butt', 'tip'))
            for side in arms:
                w = np.array(lm[f'wrist_{side}'], float)
                hp = np.array(hand_point(lm, side), float)
                dseg = min(seg_point(w, b, t), seg_point(hp, b, t))
                if dseg < torso_len * 0.14:
                    grips.append(side)
            info['grips'] = grips
        save(lay, d / 'weapon.png')
        info['weapon'] = {'pixels': int((weapon > 0).sum())}
    if (offhand > 0).sum() > 200:
        save(layer(master, offhand), d / 'offhand.png')
        info['offhand'] = {'pixels': int((offhand > 0).sum())}
    # draw order, back to front: arms hanging off the far side, the body, arms over the torso, the weapon, the hands
    # that hold it (a fist closes over the shaft); an off-hand item (shield, lantern) goes under the weapon, which
    # strikes past it
    free = [s for s in 'LR' if f'arm_{s}' in info and s not in grips]
    info['order'] = ([f'arm_{s}' for s in free if not info[f'arm_{s}']['front']] + ['base'] +
                     [f'arm_{s}' for s in free if info[f'arm_{s}']['front']] + (['offhand'] if 'offhand' in info else []) +
                     (['weapon'] if 'weapon' in info else []) + [f'arm_{s}' for s in 'LR' if s in grips])
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
            save(layer(master, c), d / f'{name}.png')
            info[name] = {'center': [round(float(cen[0]), 1), round(float(cen[1]), 1)], 'radius': round(float(r), 1),
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


def fill_under_hands(lay, span, hands):
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
    hid = cv2.warpAffine(cv2.dilate(hands, ell(8)), R, (C, C), flags=cv2.INTER_NEAREST, borderValue=0) > 0
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
        # the row's own shaft colour (its median along the whole shaft: rings and ferrules are a small part), not
        # the pixels at the gap's edge, which can be the fingers that covered it
        out[y, xs[gap]] = np.median(rot[y, xs[ok]], axis=0)
        filled[y, xs[gap]] = True
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
    a = ap.parse_args()
    print(json.dumps(build(a.unit, a.preview)))
