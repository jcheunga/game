#!/usr/bin/env python3
"""Painted enemy strongholds: one per stage, painted into its zone's battle scene.

Each stage's stronghold is painted by Codex straight into a guide: the zone's own backdrop layers composed at
the enemy end of the field, exactly as the battle camera sees them (minus the front foliage). Painting into the
scene keeps the zone's light, palette and brushwork. A second Codex edit keys the stronghold out on flat magenta;
this script turns the pair into a cut-out plate with its painted ground shadow, and writes the runtime JSON.

  python3 art/royal/bases.py guide [zone ...]       compose guides into art/royal/gen/bases/guides
  python3 art/royal/bases.py run [stage ...]        Codex: paint, then key, every stage missing either (resumes,
                                                    waits out usage limits); briefs in bases_briefs.py
  python3 art/royal/bases.py cut [stage ...]        build assets/structures/bases/stage_NNN.{png,json}
  python3 art/royal/bases.py preview [stage ...]    composite cut plates back into the full battle view
  python3 art/royal/bases.py sheet stage ...        painting + key with a ruler, for reviewing a stronghold
  python3 art/royal/bases.py gates stage ...        stacked gate strips with a fine ruler, for reading GATES
  python3 art/royal/bases.py setgate 12=780 ...     record gate centres (painting px) into GATES
"""
import json
import sys
import warnings
warnings.filterwarnings("ignore", category=DeprecationWarning)
from pathlib import Path
from PIL import Image

sys.path.insert(0, str(Path(__file__).parent))
from bases_briefs import BASES, SIZE, ZONE_LOOK, size_of  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
WORLD = ROOT / "assets/world/royal"
GEN = ROOT / "art/royal/gen/bases"
GUIDES = GEN / "guides"

ZONES = ["city", "harbor", "foundry", "quarantine", "thornwall", "basilica", "mire", "steppe", "gloamwood", "citadel"]

# The field (data/combat_config.json): plates are laid out against its EnemyBaseX and record it, so the runtime can
# follow a later change to the field's length without a recut.
COMBAT = json.loads((ROOT / "data/combat_config.json").read_text())["Combat"]
ENEMY_BASE_X = COMBAT["EnemyBaseX"]
# The battle camera at the enemy end (its view ends at the field's right edge), layers scrolled for that position.
WORLD_CENTRE_X = (COMBAT["BattlefieldLeft"] + COMBAT["BattlefieldRight"]) * .5
CAMERA_X = COMBAT["BattlefieldLeft"] + COMBAT["BattlefieldRight"] - COMBAT["ViewWidth"] * .5
# The painted region around the stronghold, in world units: the gate faces the road just short of EnemyBaseX, its
# keep runs off the field's right edge and walls trail back along the road. (The first 100 guides were composed
# for the two-screen field: EnemyBaseX 1152, camera x 900.)
REGION = (ENEMY_BASE_X - 216.0, 120.0, ENEMY_BASE_X + 104.0, 440.0)
GUIDE_PX = 1024


def load_backdrop(zone):
    spec = json.loads((WORLD / f"{zone}.json").read_text())
    layers = []
    for entry in spec["layers"]:
        layers.append(dict(entry, image=Image.open(WORLD / entry["file"]).convert("RGBA")))
    return layers, "#" + spec["sky"]


def tiles(layer, camera_x, left, right):
    """(x, y, w, h, mirrored) copies of a layer covering left..right. The far panorama is a single painting; past
    its ends (only reachable while a drag stretches the view) it continues mirrored so guides have no gap."""
    import math
    x, y, w, h = layer["rect"]
    x += (1 - layer["parallax"]) * (camera_x - WORLD_CENTRE_X)
    for k in range(math.floor((left - x) / w), math.ceil((right - x) / w) + 1):
        mirrored = not layer["tile"] and k % 2 != 0
        yield x + k * w, y, w + 0.5, h, mirrored


def compose(zone, region=REGION, px=GUIDE_PX, front=False, camera_x=CAMERA_X):
    """The zone's backdrop over a world rect, as the battle camera at camera_x draws it."""
    x0, y0, x1, y1 = region
    scale = px / (x1 - x0)
    size = (px, round((y1 - y0) * scale))
    layers, sky = load_backdrop(zone)
    canvas = Image.new("RGBA", size, sky)
    for layer in layers:
        if layer["front"] and not front:
            continue
        for tx, ty, tw, th, mirrored in tiles(layer, camera_x, x0, x1):
            box = (round((tx - x0) * scale), round((ty - y0) * scale), round((tx + tw - x0) * scale), round((ty + th - y0) * scale))
            if box[2] <= 0 or box[0] >= size[0]:
                continue
            piece = layer["image"].resize((box[2] - box[0], box[3] - box[1]), Image.LANCZOS)
            if mirrored:
                piece = piece.transpose(Image.FLIP_LEFT_RIGHT)
            canvas.alpha_composite(piece, (box[0], box[1]))
    return canvas


FACTION = ("The enemy is the Rotbound Host, an undead siege army that has seized this place and fortified it: "
           "its marks are ruined, tattered crimson-and-black heraldry bearing a pale skull under a broken crown, "
           "bone trophies, iron spikes, and braziers or lanterns burning with sickly green witch-fire. Use these as "
           "accents only; the building itself must clearly belong to this zone and this stage.")


def prompt(stage):
    title, brief = BASES[stage]
    zone = stage_zone(stage)
    return f"""Use case: in-scene asset painting (edit the attached image).
The attached image is a clean painted backdrop from a medieval fantasy side-scrolling strategy game (Crownroad: Siege of Ash): the RIGHT-HAND END of a battle road. The camera looks at the scene from the side and slightly from above. The cobbled road crossing the image horizontally (from about 60% to 77% of the image height) is where the armies march: the player's troops come from the LEFT and fight their way to the enemy stronghold at the RIGHT end of the road.

Task: paint the enemy stronghold of the stage "{title}" into this scene.
Zone: {ZONE_LOOK[zone]}
Stronghold: {brief}
{FACTION}

Placement and scale (important):
- The stronghold stands along the FAR edge of the road at its right end, its front facing the viewer (a slight three-quarter turn toward the left is fine). Its foundation runs along the far edge of the road, at about 60-62% of the image height; nothing of it stands on the near half of the road.
- Its gate or main door opens at road level onto the road, centred at about 60% of the image width, roughly in the middle of the main building. Its troops come out of it and the player's troops attack it there.
- {SIZE[size_of(stage)]}
- The main building lies between about 38% and 82% of the image width and is complete: nothing is cut off at the top, and there is a little sky around it. Lower walls or towers may continue from it to the right edge.
- Lower walls, a tower, palisades or props may trail LEFT from it along the far side of the road, behind the road, reaching no further left than about 15% of the image width.
- The road left of the gate must stay completely clear and unchanged.
- A soldier standing on the road would be about 13% of the image height tall; scale doors, steps and battlements to that.
- Ground it in the scene: its foundation sits on the ground with soft contact shadows and a little rubble or dirt.

Style: identical to the backdrop: the same painterly brushwork, palette, saturation and level of detail, lit by the same light as the rest of the scene. It must look as if it was painted as part of the original image, not pasted on.
Keep everything outside the stronghold unchanged (sky, distant scenery, buildings behind the road, road, grass, fence).
No people, no soldiers, no creatures, no text, no watermark."""


def stage_zone(stage):
    return ZONES[(stage - 1) // 10]


KEY_PROMPT = """Use case: background removal (edit the attached image).
Edit the attached painting. Keep ONLY the enemy stronghold on the right: the fortified building with its towers, walls, battlements, roofs, gate, banners, lanterns, braziers, spikes and skull trophies, the walls or palisades trailing left from it, and anything piled against it (barricades, carts, barrels, crates, rubble).
Replace EVERYTHING else with a flat, perfectly uniform pure magenta background (#FF00FF): the sky and clouds, mountains, distant castles, the trees, houses, market stalls and other scenery behind it that are not part of the stronghold, the road, the grass and the fence.
Keep the stronghold's pixels exactly as they are: the same position, size, colours and details. Do not repaint, move, crop, rescale or recolour it, and do not add anything. Clean crisp edges against the magenta, with no glow, haze or shadow spilling onto the magenta."""


def paint_path(stage):
    return GEN / f"paint/stage_{stage:03d}.png"


def key_path(stage):
    return GEN / f"key/stage_{stage:03d}.png"


def priority(stages):
    """Every zone's first and final strongholds come first, so each zone's look can be judged early."""
    head = [s for s in stages if s % 10 in (0, 1)]
    return sorted(head, key=lambda s: (s % 10 != 0, s)) + [s for s in stages if s not in head]


def run(stages, workers=2):
    """Paints, then keys, every stage still missing either; waits out Codex usage limits and resumes."""
    import concurrent.futures
    import datetime as dt
    import re
    import subprocess
    import threading
    import time
    sys.path.insert(0, str(Path(__file__).parent))
    from codexgen import MODEL, WRAPPER
    logs = ROOT / "art/royal/gen/logs"
    logs.mkdir(parents=True, exist_ok=True)
    lock, wake = threading.Lock(), [None]

    def wait_for_limit(text):
        found = re.search(r'resets_at\\?"?:\s*(\d{9,})', text)
        clock = re.search(r"try again at (\d{1,2}):(\d{2})\s*([AP]M)", text)
        now = dt.datetime.now()
        if found:
            when = dt.datetime.fromtimestamp(int(found.group(1))) + dt.timedelta(minutes=1)
        elif clock:
            hour = int(clock.group(1)) % 12 + (12 if clock.group(3) == "PM" else 0)
            when = now.replace(hour=hour, minute=int(clock.group(2)), second=0, microsecond=0) + dt.timedelta(minutes=1)
            when += dt.timedelta(days=1) if when < now else dt.timedelta()
        else:
            when = now + dt.timedelta(minutes=20)
        with lock:
            if wake[0] is None or wake[0] < now:
                wake[0] = when
                print(f"{now:%H:%M} usage limit, waiting until {when:%H:%M}", flush=True)
            when = wake[0]
        # Poll the wall clock: one long sleep stops counting while the machine sleeps.
        while dt.datetime.now() < when:
            time.sleep(min(60.0, max(1.0, (when - dt.datetime.now()).total_seconds())))

    def codex(name, text, image, out, tries=4):
        out.parent.mkdir(parents=True, exist_ok=True)
        for _ in range(tries):
            command = ["codex", "exec", "-m", MODEL, "-c", 'model_reasoning_effort="low"', "--skip-git-repo-check",
                       "-C", str(ROOT), "--sandbox", "workspace-write", "-i", str(image), "-"]
            body = WRAPPER.format(out=out, prompt=text.strip(), edit_hint=" in edit mode: the FIRST attached image is the edit target")
            started = time.time()
            result = subprocess.run(command, input=body, text=True, capture_output=True, cwd=ROOT)
            (logs / f"{name}.log").write_text(result.stdout[-6000:] + "\n--- stderr ---\n" + result.stderr[-3000:])
            if out.exists():
                print(f"{name}: ok in {time.time() - started:.0f}s", flush=True)
                return True
            if "usage limit" in (result.stdout + result.stderr).lower() or "usage_limit_reached" in result.stdout + result.stderr:
                wait_for_limit(result.stdout + result.stderr)
        print(f"{name}: failed", flush=True)
        return False

    def work(stage):
        if not paint_path(stage).exists():
            codex(f"base-{stage:03d}", prompt(stage), GUIDES / f"{stage_zone(stage)}.png", paint_path(stage))
        if paint_path(stage).exists() and not key_path(stage).exists():
            codex(f"base-{stage:03d}-key", KEY_PROMPT, paint_path(stage), key_path(stage))

    pending = [s for s in priority(stages or sorted(BASES)) if not key_path(s).exists()]
    print(f"{len(pending)} strongholds to paint or key", flush=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as pool:
        list(pool.map(work, pending))


OUT = ROOT / "assets/structures/bases"
# Melee troops stop near here to strike the stronghold (EnemyBaseX less BaseCoreRadius and their reach) and its troops
# spawn here; each plate shifts so its gate centre stands here, which also brings the whole facade into the end view.
GATE_X = ENEMY_BASE_X - 24.0
# Gate centre, in painting pixels, measured by eye on each painting (bases.py sheet draws the guides).
GATES = {
    1: 905, 2: 832, 3: 799, 4: 808, 5: 858, 6: 775, 7: 804, 8: 840, 9: 836, 10: 972, 11: 757, 12: 750, 13: 795,
    14: 792, 15: 795, 16: 838, 17: 821, 18: 799, 19: 720, 20: 862, 21: 864, 22: 778, 23: 738, 24: 788, 25: 791,
    26: 838, 27: 850, 28: 834, 29: 775, 30: 862, 31: 827, 32: 845, 33: 769, 34: 760, 35: 797, 36: 847, 37: 790,
    38: 772, 39: 830, 40: 880, 41: 802, 42: 769, 43: 775, 44: 823, 45: 827, 46: 811, 47: 797, 48: 827, 49: 763,
    50: 900, 51: 819, 52: 779, 53: 732, 54: 756, 55: 830, 56: 832, 57: 832, 58: 741, 59: 808, 60: 937, 61: 733,
    62: 821, 63: 771, 64: 741, 65: 832, 66: 872, 67: 805, 68: 805, 69: 775, 70: 897, 71: 787, 72: 771, 73: 800,
    74: 805, 75: 785, 76: 776, 77: 738, 78: 753, 79: 756, 80: 887, 81: 843, 82: 745, 83: 784, 84: 815, 85: 752,
    86: 823, 87: 772, 88: 805, 89: 823, 90: 937, 91: 700, 92: 760, 93: 805, 94: 836, 95: 829, 96: 768, 97: 805,
    98: 879, 99: 820, 100: 781,
}
# Shadows fall only on the ground: rows at or below this world y (the grass behind the road starts near 300).
GROUND_Y = 300.0


def world_scale(width):
    return width / (REGION[2] - REGION[0])


def registered(key, paint, stage):
    """The key warped onto the painting. Key edits usually come back pixel-aligned, but some are rescaled and
    shifted (stage 41: x1.15); a similarity transform from SIFT matches inside the stronghold puts them back."""
    import numpy as np
    import cv2
    magenta = np.array([1, 0, 1], np.float32)
    mask = (np.linalg.norm(key - magenta, axis=2) > .5).astype(np.uint8)
    core = cv2.erode(mask, np.ones((7, 7), np.uint8)) > 0
    if not core.any() or float(np.abs(paint - key)[core].mean()) < .08:
        return key
    grey = lambda image: cv2.cvtColor((np.clip(image, 0, 1) * 255).astype(np.uint8), cv2.COLOR_RGB2GRAY)
    sift = cv2.SIFT_create(4000)
    kk, dk = sift.detectAndCompute(grey(key), mask)
    kp, dp = sift.detectAndCompute(grey(paint), None)
    good = [a for a, b in cv2.BFMatcher().knnMatch(dk, dp, k=2) if a.distance < .75 * b.distance]
    if len(good) < 12:
        return key
    matrix, inliers = cv2.estimateAffinePartial2D(np.float32([kk[m.queryIdx].pt for m in good]),
                                                  np.float32([kp[m.trainIdx].pt for m in good]),
                                                  method=cv2.RANSAC, ransacReprojThreshold=2.0)
    h, w = paint.shape[:2]
    warped = cv2.warpAffine(key, matrix, (w, h), flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT,
                            borderValue=(1.0, 0.0, 1.0))
    print(f"stage {stage}: key registered (scale {np.hypot(matrix[0, 0], matrix[0, 1]):.3f}, "
          f"shift {matrix[0, 2]:+.0f},{matrix[1, 2]:+.0f}, {int(inliers.sum())} inliers)")
    return warped


def cut(stages):
    """Paint + key -> trimmed RGBA plate with a soft contact shadow, and its JSON."""
    import numpy as np
    import cv2
    OUT.mkdir(parents=True, exist_ok=True)
    for stage in stages or sorted(BASES):
        paint_path = GEN / f"paint/stage_{stage:03d}.png"
        key_path = GEN / f"key/stage_{stage:03d}.png"
        if not paint_path.exists() or not key_path.exists():
            print(f"stage {stage}: painting or key missing")
            continue
        paint = cv2.imread(str(paint_path), cv2.IMREAD_COLOR)[..., ::-1].astype(np.float32) / 255
        key = cv2.imread(str(key_path), cv2.IMREAD_UNCHANGED).astype(np.float32) / 255
        if key.shape[2] == 4:
            # Codex sometimes answers the key edit with a transparent background instead of magenta.
            key = key[..., :3] * key[..., 3:] + np.array([1, 0, 1], np.float32) * (1 - key[..., 3:])
        key = key[..., ::-1].copy()
        if key.shape != paint.shape:
            key = cv2.resize(key, paint.shape[1::-1], interpolation=cv2.INTER_LINEAR)
        key = registered(key, paint, stage)
        h, w = paint.shape[:2]
        scale = world_scale(w)

        # Alpha from the distance to magenta, specks dropped, edges pulled in half a pixel so the painting's own
        # background (sky, stalls) doesn't fringe the silhouette.
        distance = np.linalg.norm(key - np.array([1, 0, 1], np.float32), axis=2)
        alpha = np.clip((distance - .2) / (.55 - .2), 0, 1)
        solid = (alpha > .5).astype(np.uint8)
        count, labels, stats, _ = cv2.connectedComponentsWithStats(solid, connectivity=8)
        keep = np.zeros_like(solid)
        for label in range(1, count):
            if stats[label, cv2.CC_STAT_AREA] >= 300:
                keep[labels == label] = 1
        alpha *= cv2.dilate(keep, np.ones((5, 5), np.uint8)).astype(np.float32)
        alpha = cv2.GaussianBlur(cv2.erode(alpha, np.ones((3, 3), np.uint8)), (0, 0), .7)
        # Walls that run off the painting's side would end in a hard cut over the backdrop: fade them out over the
        # last 24 world units instead.
        fade = 24 * scale
        cols = np.arange(w, dtype=np.float32)
        if (alpha[:, :4] > .5).any():
            alpha *= np.clip(cols / fade, 0, 1)[None, :]
        if (alpha[:, -4:] > .5).any():
            alpha *= np.clip((w - 1 - cols) / fade, 0, 1)[None, :]
        solid = alpha > .5
        # The key must be the painting with its background replaced; a repainted or rescaled key shows up here.
        core = cv2.erode(solid.astype(np.uint8), np.ones((7, 7), np.uint8)) > 0
        drift = float(np.abs(paint - key)[core].mean()) if core.any() else 0.0
        if drift > .10:
            print(f"stage {stage}: WARNING key differs from the painting by {drift:.3f} (misaligned or repainted)")

        # Contact shadow: the silhouette dropped a few pixels and blurred, plus a wide faint occlusion, only on the
        # ground and only outside the building.
        ground_row = int((GROUND_Y - REGION[1]) * scale)
        drop = np.zeros((h, w), np.float32)
        base = solid.astype(np.float32)
        for k in range(1, 13):
            shifted = np.zeros_like(base)
            shifted[k:] = base[:-k]
            drop = np.maximum(drop, shifted * (1 - k / 14))
        contact = cv2.GaussianBlur(drop, (0, 0), 4) * .62
        occlusion = cv2.GaussianBlur(drop, (0, 0), 16) * .30
        shadow = np.clip(np.maximum(contact, occlusion), 0, 1)
        shadow[:ground_row] = 0
        shadow *= 1 - alpha

        ink = np.array([.02, .028, .045], np.float32)
        out_alpha = alpha + shadow * (1 - alpha)
        colour = (paint * alpha[..., None] + ink * (shadow * (1 - alpha))[..., None]) / np.maximum(out_alpha, 1e-4)[..., None]
        rgba = np.dstack([colour, out_alpha])
        ys, xs = np.nonzero(out_alpha > .01)
        x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
        plate = (np.clip(rgba[y0:y1, x0:x1], 0, 1) * 255).round().astype(np.uint8)
        cv2.imwrite(str(OUT / f"stage_{stage:03d}.png"), cv2.cvtColor(plate, cv2.COLOR_RGBA2BGRA))

        shift = GATE_X - (REGION[0] + GATES[stage] / scale) if stage in GATES else 0.0
        to_world = lambda px, py: [round(float(REGION[0] + px / scale + shift), 2), round(float(REGION[1] + py / scale), 2)]

        # Witch-fire lanterns and braziers: bright saturated yellow-green to green blobs inside the building (warm
        # flames are too easily confused with sunlit stone and hay to find by colour). Each keeps its own colour.
        hsv = cv2.cvtColor((paint * 255).astype(np.uint8), cv2.COLOR_RGB2HSV)
        flame = ((hsv[..., 0] >= 30) & (hsv[..., 0] <= 90) & (hsv[..., 1] > 120) & (hsv[..., 2] > 225) & solid).astype(np.uint8)
        count, labels, stats, centroids = cv2.connectedComponentsWithStats(cv2.dilate(flame, np.ones((9, 9), np.uint8)), connectivity=8)
        found = sorted((-stats[label, cv2.CC_STAT_AREA], label) for label in range(1, count)
                       if stats[label, cv2.CC_STAT_AREA] >= 150)
        lights, placed = [], []
        for _, label in found:
            centre = centroids[label]
            if len(placed) < 12 and all(np.hypot(centre[0] - p[0], centre[1] - p[1]) > 40 for p in placed):
                placed.append(centre)
                colour = paint[(labels == label) & (flame > 0)].mean(axis=0)
                colour = colour / max(colour.max(), 1e-3)
                lights.append(to_world(*centre) + ["".join(f"{round(float(c) * 255):02x}" for c in colour)])

        # Damage smoke rises from the tallest roofs and parapets: peaks of the silhouette's top edge once spires, flag
        # poles and spikes (anything under 6 world units wide) are opened away, at least 30 apart.
        bulk = cv2.morphologyEx(solid.astype(np.uint8), cv2.MORPH_OPEN, np.ones((1, int(6 * scale)), np.uint8)) > 0
        top = np.where(bulk.any(axis=0), bulk.argmax(axis=0), h)
        smoke, taken, spacing = [], np.zeros(w, bool), int(30 * scale)
        for col in np.argsort(top, kind="stable"):
            if top[col] >= h or len(smoke) == 3:
                break
            if taken[max(0, col - spacing):col + spacing].any():
                continue
            taken[col] = True
            smoke.append(to_world(col, top[col] + 3 * scale))

        world_rect = to_world(x0, y0) + [round(float((x1 - x0) / scale), 2), round(float((y1 - y0) / scale), 2)]
        # Where the building meets the ground: the lowest solid row (its shadow reaches a little further).
        foot = to_world(0, int(np.nonzero(solid.any(axis=1))[0].max()))[1]
        title = BASES.get(stage, ("", ""))[0]
        # The gate's centre, where troops strike and the health meter hangs (unmeasured plates keep their place).
        gate_x = round(float(GATE_X if stage in GATES else REGION[0] + (REGION[2] - REGION[0]) * .6), 2)
        meta = {"stage": stage, "zone": stage_zone(stage), "title": title, "rect": world_rect, "foot": foot, "gate": gate_x, "enemyBaseX": ENEMY_BASE_X,
                "lights": lights, "smoke": smoke, "generator": "art/royal/bases.py"}
        (OUT / f"stage_{stage:03d}.json").write_text(json.dumps(meta, indent=1) + "\n")
        print(f"stage {stage}: plate {x1 - x0}x{y1 - y0}, rect {world_rect}, {len(lights)} lights, shift {shift:+.1f}")


def preview(stages):
    """Each plate composited into the desktop battle view at the enemy end (1280x720), front foliage on top."""
    out_dir = GEN / "preview"
    out_dir.mkdir(parents=True, exist_ok=True)
    for stage in stages or sorted(BASES):
        meta_path = OUT / f"stage_{stage:03d}.json"
        if not meta_path.exists():
            continue
        meta = json.loads(meta_path.read_text())
        zone = meta["zone"]
        # Desktop camera: ViewWidth 600 over 1280 px, band centre (y 340) at screen row 464.6.
        zoom = 1280 / 600
        top = 340 - 464.6 / zoom
        region = (CAMERA_X - 300, top, CAMERA_X + 300, top + 720 / zoom)
        px = 1280
        view = compose(zone, region, px)
        x, y, w, h = meta["rect"]
        plate = Image.open(OUT / f"stage_{stage:03d}.png").convert("RGBA")
        plate = plate.resize((round(w * zoom), round(h * zoom)), Image.LANCZOS)
        view.alpha_composite(plate, (round((x - region[0]) * zoom), round((y - region[1]) * zoom)))
        front = compose_front(zone, region, px)
        view.alpha_composite(front)
        view.convert("RGB").save(out_dir / f"stage_{stage:03d}.png")
        print("preview", stage)


def sheet(stages):
    """Review sheet: paintings (left) and cut plates over a checker (right), with a ruler every 50 painting pixels
    across the lower half, for reading each gate's centre into GATES."""
    from PIL import ImageDraw, ImageFont
    font = ImageFont.truetype(str(next((ROOT / "assets/fonts").glob("*.ttf"))), 30)
    out_dir = GEN / "sheets"
    out_dir.mkdir(parents=True, exist_ok=True)
    for stage in stages:
        if not paint_path(stage).exists():
            continue
        paint = Image.open(paint_path(stage)).convert("RGB")
        w, h = paint.size
        draw = ImageDraw.Draw(paint)
        for x in range(0, w, 50):
            colour = (255, 255, 0) if x % 100 == 0 else (0, 255, 255)
            draw.line([(x, h * 0.45), (x, h * 0.80)], fill=colour, width=1)
            if x % 100 == 0:
                draw.text((x + 3, h * 0.80 + 4), str(x), fill=(255, 255, 0), font=font, stroke_width=3, stroke_fill=(0, 0, 0))
        road = [(int((y - REGION[1]) * world_scale(w))) for y in (313, 367)]
        for y in road:
            draw.line([(0, y), (w, y)], fill=(255, 0, 0), width=1)
        canvas = Image.new("RGB", (w * 2, h), (40, 40, 40))
        canvas.paste(paint, (0, 0))
        if key_path(stage).exists():
            key = Image.open(key_path(stage)).convert("RGBA")
            key = Image.alpha_composite(Image.new("RGBA", key.size, (255, 0, 255, 255)), key).convert("RGB")
            canvas.paste(key.resize((w, h)), (w, 0))
        canvas.resize((w * 4 // 5, h * 2 // 5)).save(out_dir / f"stage_{stage:03d}.png")
    print("sheets ->", out_dir.relative_to(ROOT))


def gates_view(stages):
    """Gate-reading strips: the band where gates meet the road (cols 30-100%), a labelled ruler every 25 painting
    pixels, several stages stacked in one image (art/royal/gen/bases/sheets/gates_<first>.png)."""
    from PIL import ImageDraw, ImageFont
    font = ImageFont.truetype(str(next((ROOT / "assets/fonts").glob("*.ttf"))), 22)
    strips = []
    for stage in stages:
        if not paint_path(stage).exists():
            continue
        paint = Image.open(paint_path(stage)).convert("RGB")
        w, h = paint.size
        x0, y0, y1 = int(w * .3), int(h * .36), int(h * .66)
        strip = paint.crop((x0, y0, w, y1))
        draw = ImageDraw.Draw(strip)
        for x in range((x0 // 25 + 1) * 25, w, 25):
            major = x % 100 == 0
            draw.line([(x - x0, strip.height * (.55 if major else .75)), (x - x0, strip.height)],
                      fill=(255, 255, 0) if major else (0, 255, 255), width=2 if major else 1)
            if major:
                draw.text((x - x0 + 3, strip.height - 26), str(x), fill=(255, 255, 0), font=font, stroke_width=3, stroke_fill=(0, 0, 0))
        draw.text((6, 4), str(stage), fill=(255, 255, 255), font=font, stroke_width=3, stroke_fill=(0, 0, 0))
        strips.append(strip)
    if not strips:
        return
    sheet = Image.new("RGB", (strips[0].width, sum(s.height for s in strips)))
    y = 0
    for strip in strips:
        sheet.paste(strip, (0, y))
        y += strip.height
    out = GEN / f"sheets/gates_{stages[0]:03d}.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)
    print("gates ->", out.relative_to(ROOT))


def compose_front(zone, region, px):
    x0, y0, x1, y1 = region
    scale = px / (x1 - x0)
    size = (px, round((y1 - y0) * scale))
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    layers, _ = load_backdrop(zone)
    for layer in layers:
        if not layer["front"]:
            continue
        for tx, ty, tw, th, mirrored in tiles(layer, CAMERA_X, x0, x1):
            box = (round((tx - x0) * scale), round((ty - y0) * scale), round((tx + tw - x0) * scale), round((ty + th - y0) * scale))
            if box[2] <= 0 or box[0] >= size[0]:
                continue
            canvas.alpha_composite(layer["image"].resize((box[2] - box[0], box[3] - box[1]), Image.LANCZOS), (box[0], box[1]))
    return canvas


def guide(zones):
    GUIDES.mkdir(parents=True, exist_ok=True)
    for zone in zones or ZONES:
        image = compose(zone)
        image.convert("RGB").save(GUIDES / f"{zone}.png")
        print("guide", zone, image.size)


def set_gates(pairs):
    """Rewrites the GATES table in this file with stage=pixel pairs added or replaced."""
    import ast
    path = Path(__file__)
    text = path.read_text()
    start = text.index("GATES = {")
    end = text.index("}", start) + 1
    gates = ast.literal_eval(text[start + len("GATES = "):end])
    gates.update({int(k): int(v) for k, v in (pair.split("=") for pair in pairs)})
    lines, line = [], "   "
    for stage, px in sorted(gates.items()):
        piece = f" {stage}: {px},"
        if len(line) + len(piece) > 116:
            lines.append(line)
            line = "   "
        line += piece
    lines.append(line)
    path.write_text(text[:start] + "GATES = {\n" + "\n".join(lines) + "\n}" + text[end:])
    print(f"{len(gates)} gates")


def main():
    command, args = (sys.argv[1], sys.argv[2:]) if len(sys.argv) > 1 else ("guide", [])
    if command == "guide":
        guide(args)
    elif command == "run":
        run([int(s) for s in args])
    elif command == "cut":
        cut([int(s) for s in args])
    elif command == "preview":
        preview([int(s) for s in args])
    elif command == "sheet":
        sheet([int(s) for s in args])
    elif command == "gates":
        gates_view([int(s) for s in args])
    elif command == "setgate":
        set_gates(args)
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()
