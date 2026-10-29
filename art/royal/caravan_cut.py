#!/usr/bin/env python3
"""Cuts the paintings from caravan.py into game assets.

Every wagon edit (open door, damage, overlays, skins) is registered back onto the clean wagon painting, so one set
of points (measured by eye on `caravan.py sheet wagon`, kept in WAGON_POINTS) serves every variant.

  art/remaster/puppet/run.sh art/royal/caravan_cut.py [wagon|mounts|barricades|decals|banner|flag ...]
  art/remaster/puppet/run.sh art/royal/caravan_cut.py review     before/after and layer sheets in gen/caravan/review

Outputs:
  assets/structures/battle-v2/war_wagon[_<skin>].{png,json}, _door.png (open-door strip), _damaged.png
  assets/structures/battle-v2/war_wagon_{armour,snow,mud}.png   full-frame overlays for every skin
  assets/structures/mount_*.png                                  roof weapons (ballista stand + bow split)
  assets/structures/barricades/<zone>_<n>.png + <zone>.json      zone barricade pieces
  assets/structures/base_decals.png + .json                      wall cracks, scorch, chips
  assets/structures/base_banner.png + .json                      stronghold health banner
  assets/structures/victory_flag.png + .json                     the caravan's standard
"""
import json
import sys
from pathlib import Path

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
GEN = ROOT / "art/royal/gen/caravan"
STRUCT = ROOT / "assets/structures"
BATTLE = STRUCT / "battle-v2"
REVIEW = GEN / "review"
SKINS = ["skin_iron", "skin_royal", "skin_bone", "skin_flame", "skin_shadow", "skin_guild", "skin_legendary"]
ZONES = ["city", "harbor", "foundry", "quarantine", "thornwall", "basilica", "mire", "steppe", "gloamwood", "citadel"]

# Read by eye on the 1254 px clean wagon painting (gen/caravan/sheets/wagon.png).
WAGON_POINTS = {
    "ground": 1095,                       # wheel bottoms
    "wheels": [352, 925],                 # wheel centres (x)
    "mounts": [[352, 446], [548, 446], [714, 446]],   # roof deck: archers' post, ballista, firepot
    "lamps": [[152, 668], [1093, 668]],   # the hanging lanterns' glass
    "lantern": [905, 395],                # the cupola's lamp
    "flag": [30, 192, 256, 388],          # the flag's cloth (x0, y0, x1, y1); its pole stands at x1
    "awning": [756, 598, 952, 690],
    "hitch": [1222, 862],                 # the shafts' ring at the front
    "smoke": [560, 430],
}
# The old render's wheel span (0.63 of a 210-wide plate) keeps the wagon its old size in battle.
OLD_WIDTH, OLD_SPAN = 210.0, 0.63

ALPHA_FLOOR = 26 / 255      # Codex's transparent paintings carry a faint glow halo below this
PLATE = 1024                # wagon plates ship at the old renders' size


def plate_size(rgba):
    return cv2.resize(rgba, (PLATE, PLATE), interpolation=cv2.INTER_AREA)


def read(path):
    image = cv2.imread(str(path), cv2.IMREAD_UNCHANGED)
    if image.shape[2] == 3:
        image = np.dstack([image, np.full(image.shape[:2], 255, np.uint8)])
    return cv2.cvtColor(image, cv2.COLOR_BGRA2RGBA).astype(np.float32) / 255


def write(path, rgba):
    path.parent.mkdir(parents=True, exist_ok=True)
    out = (np.clip(rgba, 0, 1) * 255).round().astype(np.uint8)
    cv2.imwrite(str(path), cv2.cvtColor(out, cv2.COLOR_RGBA2BGRA))


def clean(rgba, floor=ALPHA_FLOOR, min_area=200):
    """Drops the halo and specks: alpha below the floor goes, the rest is remapped, small islands are removed."""
    out = rgba.copy()
    alpha = np.clip((rgba[..., 3] - floor) / (1 - floor), 0, 1)
    solid = (alpha > .5).astype(np.uint8)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(solid, connectivity=8)
    keep = np.zeros_like(solid)
    for label in range(1, count):
        if stats[label, cv2.CC_STAT_AREA] >= min_area:
            keep[labels == label] = 1
    alpha *= cv2.dilate(keep, np.ones((5, 5), np.uint8)).astype(np.float32)
    out[..., 3] = alpha
    return out


def register(moving, fixed, label):
    """Warps `moving` onto `fixed` with a similarity transform from SIFT matches inside both silhouettes."""
    grey = lambda image: cv2.cvtColor((np.clip(image[..., :3], 0, 1) * 255).astype(np.uint8), cv2.COLOR_RGB2GRAY)
    mask = lambda image: ((image[..., 3] > .5) * 255).astype(np.uint8)
    if moving.shape[:2] != fixed.shape[:2]:
        moving = cv2.resize(moving, fixed.shape[1::-1], interpolation=cv2.INTER_AREA)
    sift = cv2.SIFT_create(5000)
    km, dm = sift.detectAndCompute(grey(moving), mask(moving))
    kf, df = sift.detectAndCompute(grey(fixed), mask(fixed))
    good = [a for a, b in cv2.BFMatcher().knnMatch(dm, df, k=2) if a.distance < .75 * b.distance]
    if len(good) < 12:
        print(f"{label}: only {len(good)} matches, left unregistered")
        return moving
    matrix, inliers = cv2.estimateAffinePartial2D(np.float32([km[m.queryIdx].pt for m in good]),
                                                  np.float32([kf[m.trainIdx].pt for m in good]),
                                                  method=cv2.RANSAC, ransacReprojThreshold=2.0)
    scale = float(np.hypot(matrix[0, 0], matrix[0, 1])) if matrix is not None else 0.0
    h, w = fixed.shape[:2]
    if not .5 < scale < 1.6 or int(inliers.sum()) < 10:
        # Too different to match features (a repainted prop): line the silhouettes' boxes up by height, bottom centre.
        box = lambda image: cv2.boundingRect((image[..., 3] > .5).astype(np.uint8))
        mx, my, mw, mh = box(moving)
        fx, fy, fw, fh = box(fixed)
        scale = fh / mh
        matrix = np.float32([[scale, 0, fx + fw / 2 - (mx + mw / 2) * scale], [0, scale, fy + fh - (my + mh) * scale]])
        print(f"{label}: aligned by silhouette, scale {scale:.3f}")
    else:
        print(f"{label}: registered scale {scale:.3f} shift {matrix[0, 2]:+.1f},{matrix[1, 2]:+.1f} ({int(inliers.sum())} inliers)")
    return cv2.warpAffine(moving, matrix, (w, h), flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=(0, 0, 0, 0))


def difference(edit, base, threshold=.11):
    """Soft mask of where an aligned edit differs from its base (colour or coverage)."""
    colour = np.abs(edit[..., :3] - base[..., :3]).max(axis=2) * np.maximum(edit[..., 3], base[..., 3])
    coverage = np.abs(edit[..., 3] - base[..., 3])
    raw = np.maximum(colour, coverage) > threshold
    raw = cv2.morphologyEx(raw.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    raw = cv2.morphologyEx(raw, cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8))
    return cv2.GaussianBlur(raw.astype(np.float32), (0, 0), 1.5)


# ------------------------------------------------------------------ the wagon

def wagon_meta(size, door):
    h, w = size
    points = WAGON_POINTS
    uv = lambda p: [round(p[0] / w, 6), round(p[1] / h, 6)]
    width = OLD_WIDTH * OLD_SPAN / ((points["wheels"][1] - points["wheels"][0] + 300) / w)
    ground = points["ground"] / h
    # Far wheels touch down a little higher than the near ones, seen from 22 degrees above.
    far = (points["ground"] - 60) / h
    meta = {
        "source": "art/royal/caravan.py (painted)", "generator": "art/royal/caravan_cut.py", "cameraElevation": 22,
        "anchor": [.5, round(ground, 6)], "width": round(width, 2),
        "mounts": [uv(p) for p in points["mounts"]],
        "contacts": [[round(x / w, 6), round(far, 6)] for x in points["wheels"]] +
                    [[round(x / w, 6), round(ground, 6)] for x in points["wheels"]],
        "lights": [[round(p[0] / w, 6), round((points["ground"] - 40) / h, 6)] for p in points["lamps"]],
        "lamps": [uv(p) for p in points["lamps"]], "lantern": uv(points["lantern"]),
        "flag": [round(v / (w if i % 2 == 0 else h), 6) for i, v in enumerate(points["flag"])],
        "awning": [round(v / (w if i % 2 == 0 else h), 6) for i, v in enumerate(points["awning"])],
        "hitch": uv(points["hitch"]), "smoke": uv(points["smoke"]), "door": door,
        "damaged": True, "overlays": {"armour": [], "snow": True, "mud": True},
    }
    return meta


def cut_wagon():
    base = clean(read(GEN / "wagon.png"))
    h, w = base.shape[:2]
    ys, xs = np.nonzero(base[..., 3] > .5)
    centre = [round(float(xs.mean() / w), 6), round(float(ys.mean() / h), 6)]

    # The open door: where the door edit differs, in the lower middle of the wagon.
    door_edit = clean(register(read(GEN / "wagon_door.png"), base, "door"))
    changed = difference(door_edit, base)
    changed[: int(h * .62)] = 0
    changed[:, : int(w * .3)] = 0
    changed[:, int(w * .75):] = 0
    ys, xs = np.nonzero(changed > .5)
    pad = 8
    x0, x1 = max(0, xs.min() - pad), min(w, xs.max() + pad)
    y0, y1 = max(0, ys.min() - pad), min(h, ys.max() + pad)
    # The doorway's lit interior is where troops appear; the ramp's lowest point is where they reach the ground.
    interior = changed[y0:y1, x0:x1] > .5
    lit = door_edit[y0:y1, x0:x1, :3].mean(axis=2) * interior
    iy, ix = np.unravel_index(np.argmax(cv2.GaussianBlur(lit, (0, 0), 12)), lit.shape)
    # The ramp's foot is the middle of its lowest boards: the edit's box also takes in what the repaint touched
    # beside the ramp, so the box's middle is not the ramp's.
    bottom = xs[ys >= ys.max() - 12]
    door = {"rect": [round(x0 / w, 6), round(y0 / h, 6), round(x1 / w, 6), round(y1 / h, 6)], "frames": 2,
            "exit": [round((x0 + ix) / w, 6), round((y0 + iy) / h, 6)],
            "foot": [round((bottom.min() + bottom.max()) * .5 / w, 6), round((ys.max() - 6) / h, 6)]}

    meta = wagon_meta((h, w), door)
    meta["centre"] = centre

    def save(name, image, door_image):
        write(BATTLE / f"{name}.png", plate_size(image))
        opened = door_image[y0:y1, x0:x1].copy()
        # Feather the strip's edges into the plate under it, so its rectangle never shows.
        feather = np.ones(opened.shape[:2], np.float32)
        ramp = np.linspace(0, 1, 6, dtype=np.float32)
        feather[:, :6] *= ramp[None, :]
        feather[:, -6:] *= ramp[None, ::-1]
        feather[:6] *= ramp[:, None]
        opened[..., 3] *= np.maximum(feather, (changed[y0:y1, x0:x1] > .5).astype(np.float32))
        # Two frames: the door half-way (a dissolve between shut and open over the swing) and fully open.
        shut = image[y0:y1, x0:x1]
        half = opened.copy()
        half[..., 3] = opened[..., 3] * .5 + shut[..., 3] * .5 * feather
        half[..., :3] = (opened[..., :3] * opened[..., 3:] + shut[..., :3] * shut[..., 3:]) / np.maximum(opened[..., 3:] + shut[..., 3:], 1e-4)
        strip = np.hstack([half, opened])
        k = PLATE / w
        write(BATTLE / f"{name}_door.png", cv2.resize(strip, (round(strip.shape[1] * k), round(strip.shape[0] * k)), interpolation=cv2.INTER_AREA))
        (BATTLE / f"{name}.json").write_text(json.dumps(meta, indent=2) + "\n")

    damaged = clean(register(read(GEN / "wagon_damaged.png"), base, "damaged"))
    save("war_wagon", base, door_edit)
    write(BATTLE / "war_wagon_damaged.png", plate_size(damaged))

    # Overlays shared by every skin: only what the edit added, at the edit's colour.
    for name in ("armour", "snow", "mud"):
        edit = clean(register(read(GEN / f"wagon_{name}.png"), base, name))
        mask = difference(edit, base, .09 if name == "snow" else .12)
        overlay = edit.copy()
        overlay[..., 3] = edit[..., 3] * mask
        write(BATTLE / f"war_wagon_{name}.png", plate_size(overlay))
    # Armour arrives by level: the wheels first, then the lower hull, then the body (rows of the plate).
    wheel_top = (WAGON_POINTS["ground"] - 310) / h
    meta["overlays"]["armour"] = [round(wheel_top, 4), round(.62, 4), 0.0]
    (BATTLE / "war_wagon.json").write_text(json.dumps(meta, indent=2) + "\n")

    for skin in SKINS:
        if not (GEN / f"{skin}.png").exists():
            print(f"{skin}: not painted yet")
            continue
        image = clean(register(read(GEN / f"{skin}.png"), base, skin))
        door_image = clean(register(read(GEN / f"{skin}_door.png"), image, skin + " door")) if (GEN / f"{skin}_door.png").exists() else door_edit
        save(f"war_wagon_{skin}", image, door_image)
        if (GEN / f"{skin}_damaged.png").exists():
            write(BATTLE / f"war_wagon_{skin}_damaged.png", plate_size(clean(register(read(GEN / f"{skin}_damaged.png"), image, skin + " damaged"))))
    print("wagon:", json.dumps({k: meta[k] for k in ("anchor", "width", "door", "centre")}))


# ------------------------------------------------------------------ roof weapons

def cut_mounts():
    for kind in ("arrows", "ballista", "firepot"):
        old = read(STRUCT / f"mount_{kind}.png") if not (GEN / f"mount_{kind}_old.png").exists() else read(GEN / f"mount_{kind}_old.png")
        if not (GEN / f"mount_{kind}_old.png").exists():
            write(GEN / f"mount_{kind}_old.png", old)
        image = clean(register(read(GEN / f"mount_{kind}.png"), old, kind), min_area=60)
        write(STRUCT / f"mount_{kind}.png", image)
        if kind == "ballista" and (GEN / "mount_ballista_stand.png").exists():
            stand = clean(register(read(GEN / "mount_ballista_stand.png"), image, "ballista stand"), min_area=60)
            bow = image.copy()
            bow[..., 3] *= difference(image, stand, .1)
            bow = clean(bow, min_area=60)
            write(STRUCT / "mount_ballista_stand.png", stand)
            write(STRUCT / "mount_ballista_bow.png", bow)


# ------------------------------------------------------------------ sheets of pieces

def pieces(rgba, expected, min_area=2000):
    """The sheet's separate pieces, largest first, as (x0, y0, x1, y1) boxes in reading order."""
    solid = (rgba[..., 3] > .5).astype(np.uint8)
    solid = cv2.morphologyEx(solid, cv2.MORPH_CLOSE, np.ones((15, 15), np.uint8))
    count, labels, stats, _ = cv2.connectedComponentsWithStats(solid, connectivity=8)
    boxes = sorted(range(1, count), key=lambda l: -stats[l, cv2.CC_STAT_AREA])[:expected]
    boxes = [stats[l] for l in boxes if stats[l, cv2.CC_STAT_AREA] >= min_area]
    rows = sorted(boxes, key=lambda s: (int(s[1] / (rgba.shape[0] / 2.5)), s[0]))
    return [(int(s[0]), int(s[1]), int(s[0] + s[2]), int(s[1] + s[3])) for s in rows]


def cut_barricades():
    for zone in ZONES:
        path = GEN / f"barricade_{zone}.png"
        if not path.exists():
            print(f"barricade {zone}: not painted yet")
            continue
        sheet = clean(read(path), min_area=400)
        out = []
        for i, (x0, y0, x1, y1) in enumerate(pieces(sheet, 4)):
            piece = sheet[max(0, y0 - 4): y1 + 4, max(0, x0 - 4): x1 + 4]
            write(STRUCT / f"barricades/{zone}_{i}.png", piece)
            rows = np.nonzero((piece[..., 3] > .5).any(axis=1))[0]
            out.append({"file": f"{zone}_{i}.png", "size": [piece.shape[1], piece.shape[0]], "foot": int(rows.max())})
        (STRUCT / f"barricades/{zone}.json").write_text(json.dumps({"zone": zone, "pieces": out}, indent=2) + "\n")
        print(f"barricade {zone}: {len(out)} pieces")


def cut_decals():
    sheet = clean(read(GEN / "decals.png"), floor=60 / 255, min_area=300)
    boxes = pieces(sheet, 8)
    # Pack the eight pieces into one atlas row by row.
    cell = max(max(x1 - x0, y1 - y0) for x0, y0, x1, y1 in boxes) + 8
    atlas = np.zeros((cell * 2, cell * 4, 4), np.float32)
    rects = []
    for i, (x0, y0, x1, y1) in enumerate(boxes):
        piece = sheet[y0:y1, x0:x1]
        ox, oy = (i % 4) * cell + (cell - (x1 - x0)) // 2, (i // 4) * cell + (cell - (y1 - y0)) // 2
        atlas[oy: oy + y1 - y0, ox: ox + x1 - x0] = piece
        rects.append([ox, oy, x1 - x0, y1 - y0])
    write(STRUCT / "base_decals.png", atlas)
    # Reading order on the sheet: four cracks, two scorch marks, two chipped holes.
    kinds = ["crack"] * 4 + ["scorch"] * 2 + ["chip"] * 2
    (STRUCT / "base_decals.json").write_text(json.dumps({"rects": rects, "kinds": kinds[: len(rects)]}, indent=2) + "\n")
    print("decals:", len(rects))


def cut_banner():
    banner = clean(read(GEN / "banner.png"), min_area=400)
    ys, xs = np.nonzero(banner[..., 3] > .5)
    x0, y0, x1, y1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
    banner = banner[y0:y1, x0:x1]
    h, w = banner.shape[:2]
    # The rod is the dark iron band across the top; the cloth hangs below it. The emblem sits at the left end.
    rows = banner[..., 3] > .5
    widths = rows.mean(axis=1)
    rod_bottom = int(np.argmax(widths > .85)) + 1
    while rod_bottom < h and widths[rod_bottom] > .85:
        rod_bottom += 1
    cloth = rows[rod_bottom:].any(axis=0)
    cols = np.nonzero(cloth)[0]
    write(STRUCT / "base_banner.png", banner)
    meta = {"size": [w, h], "rod": [0, 0, w, rod_bottom], "cloth": [int(cols.min()), rod_bottom, int(cols.max() + 1), h],
            "emblem": int(cols.min() + (cols.max() - cols.min()) * .11)}
    (STRUCT / "base_banner.json").write_text(json.dumps(meta, indent=2) + "\n")
    print("banner:", meta)


# Read by eye on the 1024x1536 flag painting: the pole's centre line, the cloth, and the lantern finial.
FLAG_POINTS = {"pole": 285, "cloth": [296, 322, 1000, 1072], "lamp": [270, 165]}


def cut_flag():
    flag = clean(read(GEN / "flag.png"), min_area=400)
    ys, xs = np.nonzero(flag[..., 3] > .5)
    x0, y0, x1, y1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
    flag = flag[y0:y1, x0:x1]
    h, w = flag.shape[:2]
    p = FLAG_POINTS
    write(STRUCT / "victory_flag.png", flag)
    meta = {"size": [w, h], "foot": [int(p["pole"] - x0), h - 1], "pole": int(p["pole"] - x0),
            "cloth": [int(p["cloth"][0] - x0), int(p["cloth"][1] - y0), int(min(w, p["cloth"][2] - x0)), int(p["cloth"][3] - y0)],
            "lamp": [int(p["lamp"][0] - x0), int(p["lamp"][1] - y0)]}
    (STRUCT / "victory_flag.json").write_text(json.dumps(meta, indent=2) + "\n")
    print("flag:", meta)


# ------------------------------------------------------------------ review

def review():
    """Old render beside the painted wagon, every skin, the door strip in place and the layer splits."""
    REVIEW.mkdir(parents=True, exist_ok=True)
    board = lambda rgba, colour=(.32, .36, .34): (np.dstack([np.full(rgba.shape[:2], c, np.float32) for c in colour])
                                                   * (1 - rgba[..., 3:]) + rgba[..., :3] * rgba[..., 3:])
    tiles = []
    for name in ["war_wagon"] + [f"war_wagon_{s}" for s in SKINS]:
        path = BATTLE / f"{name}.png"
        if not path.exists():
            continue
        plate = read(path)
        meta = json.loads((BATTLE / f"{name}.json").read_text())
        opened = plate.copy()
        door = read(BATTLE / f"{name}_door.png")
        door = door[:, door.shape[1] // 2:]   # the open frame
        r = meta["door"]["rect"]
        h, w = plate.shape[:2]
        x0, y0 = int(r[0] * w), int(r[1] * h)
        region = opened[y0: y0 + door.shape[0], x0: x0 + door.shape[1]]
        region[..., :3] = region[..., :3] * (1 - door[..., 3:]) + door[..., :3] * door[..., 3:]
        region[..., 3] = np.maximum(region[..., 3], door[..., 3])
        row = [board(plate), board(opened)]
        damaged = BATTLE / f"{name}_damaged.png"
        if damaged.exists():
            row.append(board(read(damaged)))
        tiles.append(np.hstack([cv2.resize(t, (500, 500)) for t in row] + [np.zeros((500, 500 * (3 - len(row)), 3), np.float32)]))
    cv2.imwrite(str(REVIEW / "wagons.jpg"), cv2.cvtColor((np.vstack(tiles) * 255).astype(np.uint8), cv2.COLOR_RGB2BGR))
    base = read(BATTLE / "war_wagon.png")
    overlays = [board(base)]
    for name in ("armour", "snow", "mud"):
        path = BATTLE / f"war_wagon_{name}.png"
        if path.exists():
            over = read(path)
            mixed = base.copy()
            mixed[..., :3] = base[..., :3] * (1 - over[..., 3:]) + over[..., :3] * over[..., 3:]
            mixed[..., 3] = np.maximum(base[..., 3], over[..., 3])
            overlays.append(board(mixed))
    cv2.imwrite(str(REVIEW / "overlays.jpg"), cv2.cvtColor((np.hstack([cv2.resize(t, (500, 500)) for t in overlays]) * 255)
                                                           .astype(np.uint8), cv2.COLOR_RGB2BGR))
    print("review ->", REVIEW.relative_to(ROOT))


def main():
    steps = {"wagon": cut_wagon, "mounts": cut_mounts, "barricades": cut_barricades,
             "decals": cut_decals, "banner": cut_banner, "flag": cut_flag, "review": review}
    for name in sys.argv[1:] or [n for n in steps if n != "review"]:
        steps[name]()


if __name__ == "__main__":
    main()
