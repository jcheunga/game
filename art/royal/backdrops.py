#!/usr/bin/env python3
"""Assemble the painted battle backdrops (art/royal/gen/backdrops) into parallax layers.

Each zone has five generated paintings: far (sky and vista), mid (a row of distant scenery), near
(the row lining the road), ground (the road, its fence and verge) and front (close foliage drawn in
front of the troops). This script trims them, makes them tile without seams, and writes
assets/world/royal/<zone>_<layer>.png plus <zone>.json, laid out so the concept composition holds
at the desktop battle camera: road at screen rows 411-516, horizon near row 200.

  python3 art/royal/backdrops.py [zone ...]
"""
import json
import sys
import warnings
warnings.filterwarnings("ignore", category=DeprecationWarning)
from pathlib import Path
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "art/royal/gen/backdrops"
OUT = ROOT / "assets/world/royal"

# Desktop battle camera: ViewWidth 600 world units across 1280 px, band centre (world y 340) at screen 464.6.
ZOOM = 1280 / 600
CAMERA = (600.0, 340.0)
SCREEN_ANCHOR = (640.0, 464.6)
ROAD_TOP, ROAD_BOTTOM = 411, 516

# Where the road sits in each generated ground painting (fractions of its height): top of the
# cobbles, bottom of the road surface, and the top row to keep (grass behind the road).
GROUND = {
    "default": {"keep": 0.44, "road_top": 0.508, "road_bottom": 0.740},
    "harbor": {"keep": 0.50, "road_top": 0.555, "road_bottom": 0.800},
    "foundry": {"keep": 0.36, "road_top": 0.420, "road_bottom": 0.680},
    "quarantine": {"keep": 0.46, "road_top": 0.500, "road_bottom": 0.740},
    "thornwall": {"keep": 0.47, "road_top": 0.515, "road_bottom": 0.720},
    "basilica": {"keep": 0.30, "road_top": 0.330, "road_bottom": 0.670},
    "mire": {"keep": 0.37, "road_top": 0.420, "road_bottom": 0.590},
    "steppe": {"keep": 0.33, "road_top": 0.400, "road_bottom": 0.660},
    "gloamwood": {"keep": 0.30, "road_top": 0.360, "road_bottom": 0.600},
    "citadel": {"keep": 0.42, "road_top": 0.545, "road_bottom": 0.750},
}


def world(sx, sy):
    return CAMERA[0] + (sx - SCREEN_ANCHOR[0]) / ZOOM, CAMERA[1] + (sy - SCREEN_ANCHOR[1]) / ZOOM


def content_box(image):
    return image.getchannel("A").point(lambda a: 255 if a > 24 else 0).getbbox()


def seamless(image, blend):
    """Wraps the right edge into the left so copies placed side by side meet without a seam."""
    w, h = image.size
    left = image.crop((0, 0, blend, h))
    right = image.crop((w - blend, 0, w, h))
    mask = Image.linear_gradient("L").rotate(90, expand=True).resize((blend, h))
    mask = mask.transpose(Image.FLIP_LEFT_RIGHT)  # 255 at the left: right edge fades into the start
    joined = Image.composite(right, left, mask)
    result = image.crop((0, 0, w - blend, h))
    result.paste(joined, (0, 0))
    return result


def quiet_column(image, start, end, bottom_ignore=0.15):
    """The column in [start, end) with the least painted content (a gap between buildings)."""
    w, h = image.size
    alpha = image.getchannel("A").crop((0, 0, w, int(h * (1 - bottom_ignore))))
    best, score = start, None
    for x in range(start, end, 2):
        column = alpha.crop((x, 0, x + 2, alpha.height))
        total = sum(column.getdata())
        if score is None or total < score:
            best, score = x, total
    return best


def row_layer(image, quiet=True):
    """Trims a transparent row of scenery to its content and makes it repeat cleanly."""
    box = content_box(image)
    image = image.crop((0, box[1], image.width, image.height))
    if quiet:
        w = image.width
        left = quiet_column(image, 0, int(w * 0.18))
        right = quiet_column(image, int(w * 0.82), w)
        image = image.crop((left, 0, right, image.height))
    return seamless(image, max(24, image.width // 40))


def extend_down(image, extra):
    """Continues the bottom of a ground strip by mirroring its last rows."""
    w, h = image.size
    tail = image.crop((0, h - extra, w, h)).transpose(Image.FLIP_TOP_BOTTOM)
    result = Image.new(image.mode, (w, h + extra))
    result.paste(image, (0, 0))
    result.paste(tail, (0, h))
    return result


def compose(zone, placed):
    """A picture of the zone's battlefield from the centred camera, for mission and route cards."""
    canvas = Image.new("RGBA", (1280, 600), (0, 0, 0, 255))
    for image, (x, y, w, h), tile in placed:
        scaled = image.convert("RGBA").resize((max(1, round(w)), max(1, round(h))), Image.LANCZOS)
        start = x
        if tile:
            while start > 0:
                start -= w
        while start < 1280:
            canvas.alpha_composite(scaled, (round(start), round(y))) if 0 <= round(y) else canvas.alpha_composite(scaled.crop((0, -round(y), scaled.width, scaled.height)), (round(start), 0))
            if not tile:
                break
            start += w
    target = ROOT / "assets/ui/royal/missions"
    target.mkdir(parents=True, exist_ok=True)
    canvas.crop((0, 60, 1280, 600)).convert("RGB").resize((768, 324), Image.LANCZOS).save(target / f"{zone}.png", optimize=True)


def layer(name, image, screen_rect, parallax, tile=False, front=False):
    x, y, w, h = screen_rect
    wx, wy = world(x, y)
    return {"name": name, "rect": [round(wx, 2), round(wy, 2), round(w / ZOOM, 2), round(h / ZOOM, 2)], "parallax": parallax,
            "tile": tile, "front": front, "size": list(image.size)}


def build(zone):
    paths = {k: SOURCE / f"{zone}_{k}.png" for k in ("far", "mid", "near", "ground", "front")}
    if not all(p.exists() for p in paths.values()):
        print(f"{zone}: missing layers, skipped")
        return
    OUT.mkdir(parents=True, exist_ok=True)
    layers = []

    far = Image.open(paths["far"]).convert("RGB")
    far_w = 1640
    far_h = far_w * far.height / far.width
    far.save(OUT / f"{zone}_far.png", optimize=True)
    # The distant landmark rises from about a third of the painting; set its top near screen row 60.
    layers.append(layer("far", far, (640 - far_w / 2, 60 - far_h * 0.36, far_w, far_h), 0.12) | {"file": f"{zone}_far.png"})
    top_row = far.crop((0, 0, far.width, 6)).resize((1, 1), Image.LANCZOS).getpixel((0, 0))

    mid = row_layer(Image.open(paths["mid"]).convert("RGBA"))
    mid_h = 230.0
    mid_w = mid.width * mid_h / mid.height
    mid.save(OUT / f"{zone}_mid.png", optimize=True)
    layers.append(layer("mid", mid, (640 - mid_w / 2, 392 - mid_h, mid_w, mid_h), 0.42, tile=True) | {"file": f"{zone}_mid.png"})

    ground_raw = Image.open(paths["ground"]).convert("RGB")
    g = GROUND.get(zone, GROUND["default"])
    gh = ground_raw.height
    keep = int(gh * g["keep"])
    scale = (ROAD_BOTTOM - ROAD_TOP) / ((g["road_bottom"] - g["road_top"]) * gh)
    ground = ground_raw.crop((0, keep, ground_raw.width, gh))
    ground = extend_down(ground, int(ground.height * 0.45))
    ground = seamless(ground, ground.width // 16)
    ground.save(OUT / f"{zone}_ground.png", optimize=True)
    top = ROAD_TOP - (g["road_top"] * gh - keep) * scale
    layers.append(layer("ground", ground, (640 - ground.width * scale / 2, top, ground.width * scale, ground.height * scale), 1.0, tile=True) | {"file": f"{zone}_ground.png"})

    near = row_layer(Image.open(paths["near"]).convert("RGBA"))
    near_h = 175.0
    near_w = near.width * near_h / near.height
    near.save(OUT / f"{zone}_near.png", optimize=True)
    layers.append(layer("near", near, (640 - near_w / 2, ROAD_TOP + 2 - near_h, near_w, near_h), 1.0, tile=True) | {"file": f"{zone}_near.png"})

    front = row_layer(Image.open(paths["front"]).convert("RGBA"), quiet=False)
    front_h = 150.0
    front_w = front.width * front_h / front.height
    front.save(OUT / f"{zone}_front.png", optimize=True)
    layers.append(layer("front", front, (640 - front_w / 2, 742 - front_h, front_w, front_h), 1.35, tile=True, front=True) | {"file": f"{zone}_front.png"})

    def screen(entry):
        wx, wy, ww, wh = entry["rect"]
        return (SCREEN_ANCHOR[0] + (wx - CAMERA[0]) * ZOOM, SCREEN_ANCHOR[1] + (wy - CAMERA[1]) * ZOOM, ww * ZOOM, wh * ZOOM)
    compose(zone, [(Image.open(OUT / entry["file"]), screen(entry), entry["tile"]) for entry in layers if not entry["front"]])
    meta = {"layers": layers, "sky": "%02x%02x%02x" % top_row[:3], "generator": "art/royal/backdrops.py"}
    (OUT / f"{zone}.json").write_text(json.dumps(meta, indent=1))
    print(f"{zone}: {[l['name'] for l in layers]}")


def main():
    zones = sys.argv[1:] or sorted({p.stem.split("_")[0] for p in SOURCE.glob("*_far.png")})
    for zone in zones:
        build(zone)


if __name__ == "__main__":
    main()
