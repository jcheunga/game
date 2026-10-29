#!/usr/bin/env python3
"""Life for the painted strongholds: where sentries stand, which windows are lit, and where damage can scar the walls.

Reads each cut plate (assets/structures/bases/stage_NNN.{png,json}) and adds to its JSON, in the same world units
as its lights (authored against the plate's enemyBaseX):
  posts    [x, y, half_width]  the top of a flat stretch of wall or parapet a sentry can stand behind (y = its top)
  warm     [x, y, r]           warm lights (lit windows, candles, braziers), for a slow candle flicker
  wall     [x, y]              points on solid facade near the gate where a crack or chip decal fits whole

  art/remaster/puppet/run.sh art/royal/bases_life.py [stage ...]     analyse (all stages by default)
  art/remaster/puppet/run.sh art/royal/bases_life.py sheet [stage ...]  contact sheet of the results
"""
import json
import sys
from pathlib import Path

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "assets/structures/bases"
SHEET = ROOT / "art/royal/gen/bases/sheets/life.png"

SENTRY_WIDTH = 16.0       # world units of flat wall a sentry needs
POST_HEIGHT = (28.0, 130.0)  # how high above the foot a post may stand
DECAL = 12.0              # world size of a wall decal


def load(stage):
    meta_path = OUT / f"stage_{stage:03d}.json"
    image_path = OUT / f"stage_{stage:03d}.png"
    if not meta_path.exists() or not image_path.exists():
        return None, None, None
    rgba = cv2.imread(str(image_path), cv2.IMREAD_UNCHANGED)
    rgba = cv2.cvtColor(rgba, cv2.COLOR_BGRA2RGBA).astype(np.float32) / 255
    return json.loads(meta_path.read_text()), rgba, meta_path


def analyse(stage):
    meta, rgba, meta_path = load(stage)
    if meta is None:
        return None
    h, w = rgba.shape[:2]
    x0, y0, rw, rh = meta["rect"]
    scale = w / rw                      # plate pixels per world unit
    to_world = lambda px, py: [round(float(x0 + px / scale), 2), round(float(y0 + py / scale), 2)]
    # The building proper: opaque paint, not the baked shadow (which is dark ink at partial alpha).
    alpha = rgba[..., 3]
    solid = alpha > .9
    foot_px = (meta["foot"] - y0) * scale

    # Sentry posts: the silhouette's top edge, with crenels closed (a sentry stands behind the merlons), flat over
    # a sentry's width, at wall height (not a spire, not ground level). The highest flats win, spaced apart.
    closed = cv2.morphologyEx(solid.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((1, max(3, int(7 * scale))), np.uint8))
    top = np.where(closed.any(axis=0), closed.argmax(axis=0), h).astype(np.float32)
    span = max(3, int(SENTRY_WIDTH * scale))
    posts, taken = [], np.zeros(w, bool)
    candidates = []
    for col in range(span // 2, w - span // 2):
        window = top[col - span // 2: col + span // 2]
        if window.max() >= h or window.max() - window.min() > 2.5 * scale:
            continue
        height = (foot_px - window.min()) / scale
        if POST_HEIGHT[0] <= height <= POST_HEIGHT[1]:
            candidates.append((float(window.min()), col))
    for row, col in sorted(candidates):
        if len(posts) == 3 or taken[max(0, col - span * 2): col + span * 2].any():
            continue
        # The wall under the post must be solid for a sentry's waist height, or the figure would show through, and
        # a little below the top it must already be wall (merlons), not a row of stakes or spikes.
        below = solid[int(row + 7 * scale): int(row + 16 * scale), col - span // 2: col + span // 2]
        if below.size == 0 or below.mean() < .9:
            continue
        taken[col] = True
        posts.append(to_world(col, row) + [round(SENTRY_WIDTH * .5, 1)])

    # Lit windows: warm, bright, saturated blobs inside the building (witch-fire is green and already a light).
    hsv = cv2.cvtColor((rgba[..., :3] * 255).astype(np.uint8), cv2.COLOR_RGB2HSV)
    # A lit window glows against the dark wall around it, so sunlit stone and straw (bright all round) don't count.
    value = hsv[..., 2].astype(np.float32)
    surround = cv2.blur(value, (int(14 * scale) | 1, int(14 * scale) | 1))
    # (Strict on purpose: a missed window costs nothing, a flickering haystack looks broken.)
    ring = cv2.blur(value, (int(24 * scale) | 1, int(24 * scale) | 1))
    warm = ((hsv[..., 0] >= 8) & (hsv[..., 0] <= 28) & (hsv[..., 1] > 150) & (hsv[..., 2] > 235)
            & (value - surround > 90) & (ring < 115) & solid).astype(np.uint8)
    count, labels, stats, centroids = cv2.connectedComponentsWithStats(cv2.dilate(warm, np.ones((5, 5), np.uint8)), connectivity=8)
    windows = []
    for label in sorted(range(1, count), key=lambda l: -stats[l, cv2.CC_STAT_AREA]):
        area = stats[label, cv2.CC_STAT_AREA] / scale ** 2
        if not (2 <= area <= 120) or len(windows) == 8:
            continue
        cx, cy = centroids[label]
        # Lanterns and braziers already flicker as lights.
        if any(np.hypot(cx - (l[0] - x0) * scale, cy - (l[1] - y0) * scale) < 10 * scale for l in meta["lights"]):
            continue
        if any(np.hypot(cx - (p[0] - x0) * scale, cy - (p[1] - y0) * scale) < 8 * scale for p in windows):
            continue
        radius = max(stats[label, cv2.CC_STAT_WIDTH], stats[label, cv2.CC_STAT_HEIGHT]) / scale * .7
        windows.append(to_world(cx, cy) + [round(float(np.clip(radius, 2.5, 9)), 1)])

    # Wall scars: points near the gate where a whole decal lies on opaque facade, nearest the gate first.
    gate_px = (meta["gate"] - x0) * scale
    half = int(DECAL * .5 * scale)
    inner = cv2.erode(solid.astype(np.uint8), np.ones((half * 2 + 1, half * 2 + 1), np.uint8)) > 0
    # The gate itself (about 40 wide and 55 tall) is left clear: its doors are where the troops come out.
    wall, step = [], max(2, int(4 * scale))
    rng = np.random.default_rng(stage)
    for py in range(int(foot_px - 120 * scale), int(foot_px - 8 * scale), step):
        for px in range(int(gate_px - 100 * scale), int(gate_px + 100 * scale), step):
            in_gate = abs(px - gate_px) < 22 * scale and py > foot_px - 58 * scale
            if 0 <= py < h and 0 <= px < w and inner[py, px] and not in_gate:
                wall.append((abs(px - gate_px) / scale + rng.uniform(0, 40), px, py))
    picked = []
    for _, px, py in sorted(wall):
        if len(picked) == 12:
            break
        if all(np.hypot(px - qx, py - qy) > 20 * scale for qx, qy in picked):
            picked.append((px, py))
    meta["posts"] = posts
    meta.pop("windows", None)
    meta["warm"] = windows
    meta["wall"] = [to_world(px, py) for px, py in picked]
    meta_path.write_text(json.dumps(meta, indent=1) + "\n")
    return meta


def sheet(stages, cols=10, tile=400):
    """Every plate with its posts (cyan), warm lights (yellow) and wall points (red), `cols` per row."""
    tiles = []
    for stage in stages:
        meta, rgba, _ = load(stage)
        if meta is None:
            continue
        h, w = rgba.shape[:2]
        x0, y0, rw, _ = meta["rect"]
        scale = w / rw
        board = np.full((h, w, 3), (.35, .4, .45), np.float32)
        board = board * (1 - rgba[..., 3:]) + rgba[..., :3] * rgba[..., 3:]
        board = (board * 255).astype(np.uint8).copy()
        P = lambda p: (int((p[0] - x0) * scale), int((p[1] - y0) * scale))
        for p in meta.get("posts", []):
            cx, cy = P(p)
            cv2.rectangle(board, (cx - int(p[2] * scale), cy - int(22 * scale)), (cx + int(p[2] * scale), cy), (0, 255, 255), 3)
        for p in meta.get("warm", []):
            cv2.circle(board, P(p), int(p[2] * scale), (255, 230, 0), 3)
        for p in meta.get("wall", []):
            cv2.circle(board, P(p), int(6 * scale), (255, 40, 40), 2)
        cv2.putText(board, str(stage), (10, 60), cv2.FONT_HERSHEY_SIMPLEX, 2, (255, 255, 255), 5)
        tiles.append(cv2.resize(board, (tile, int(tile * h / w))))
    rows = []
    for i in range(0, len(tiles), cols):
        row = tiles[i:i + cols]
        height = max(t.shape[0] for t in row)
        row = [np.pad(t, ((0, height - t.shape[0]), (0, 0), (0, 0))) for t in row]
        row += [np.zeros((height, tile, 3), np.uint8)] * (cols - len(row))
        rows.append(np.hstack(row))
    SHEET.parent.mkdir(parents=True, exist_ok=True)
    cv2.imwrite(str(SHEET), cv2.cvtColor(np.vstack(rows), cv2.COLOR_RGB2BGR))
    print("sheet ->", SHEET.relative_to(ROOT))


def main():
    args = sys.argv[1:]
    if args and args[0] == "sheet":
        stages = [int(s) for s in args[1:]] or list(range(1, 101))
        sheet(stages, *((2, 900) if len(stages) <= 4 else ()))
        return
    for stage in [int(s) for s in args] or range(1, 101):
        meta = analyse(stage)
        if meta:
            print(f"stage {stage}: {len(meta['posts'])} posts, {len(meta['warm'])} warm lights, {len(meta['wall'])} wall points")


if __name__ == "__main__":
    main()
