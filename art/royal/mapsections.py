#!/usr/bin/env python3
"""Paint each zone's atlas in four overlapping sections and stitch them into one map.

A zone's world is too large for one Codex image (about 1.6 megapixels) to stay sharp, so the map is painted
as four overlapping 16:9 sections (north-west, north-east, south-west, south-east) over the section guides
written by `UiReviewSmoke --map-guides` (art/royal/gen/map-guides/<zone>-section<k>.png, crops.json).
Later sections are edited from a target that already carries the painted strip of their finished
neighbours, so the painting continues across the seams; `stitch` colour-matches the sections and feathers
them together.

  python3 art/royal/mapsections.py run [--zones city,harbor] [--parallel 3]   # targets + Codex, wave by wave
  python3 art/royal/mapsections.py stitch [--zones ...]                      # assets/world/royal/maps/<zone>.jpg/json
"""
import argparse
import json
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageStat

ROOT = Path(__file__).resolve().parents[2]
GEN = ROOT / "art/royal/gen"
GUIDES = GEN / "map-guides"
SECTIONS = GEN / "map-sections"
TARGETS = SECTIONS / "targets"
JOBS = ROOT / "art/royal/jobs/08-map-sections.json"
OUT = ROOT / "assets/world/royal/maps"
CONCEPT = "output/crownroad-ui-ideas-2026-10-06/06-cartographers-atlas-home.png"
# Each section continues the painting of the sections it overlaps that are painted before it.
DEPENDS = {0: [], 1: [0], 2: [0], 3: [0, 1, 2]}
EDGE = {1: "left", 2: "top", 3: "top and left"}
TARGET_SIZE = (1920, 1080)
# The zone paintings import as lossy textures with mipmaps, so the zoomed-out atlas stays smooth.
IMPORT = """[remap]

importer="texture"
type="CompressedTexture2D"

[deps]

source_file="res://assets/world/royal/maps/{zone}.jpg"

[params]

compress/mode=1
compress/high_quality=false
compress/lossy_quality=0.9
compress/uastc_level=0
compress/rdo_quality_loss=0.0
compress/hdr_compression=1
compress/normal_map=0
compress/channel_pack=0
mipmaps/generate=true
mipmaps/limit=-1
roughness/mode=0
roughness/src_normal=""
process/channel_remap/red=0
process/channel_remap/green=1
process/channel_remap/blue=2
process/channel_remap/alpha=3
process/fix_alpha_border=true
process/premult_alpha=false
process/normal_map_invert_y=false
process/hdr_as_srgb=false
process/hdr_clamp_exposure=false
process/size_limit=0
detect_3d/compress_to=1
"""


def zone_themes():
    """The zone themes written for the original single-painting maps (art/royal/jobs/04-maps.json)."""
    themes = {}
    for job in json.loads((ROOT / "art/royal/jobs/04-maps.json").read_text()):
        line = next(l for l in job["prompt"].splitlines() if l.startswith("Zone theme:"))
        themes[job["id"].removeprefix("map-")] = line
    return themes


def crops():
    return json.loads((GUIDES / "crops.json").read_text())


def section_path(zone, k):
    return SECTIONS / f"{zone}-{k}.png"


def paste_world(canvas, canvas_rect, image, image_rect):
    """Pastes the part of `image` (covering world rect image_rect) that overlaps canvas_rect onto canvas."""
    x0, y0 = max(canvas_rect[0], image_rect[0]), max(canvas_rect[1], image_rect[1])
    x1 = min(canvas_rect[0] + canvas_rect[2], image_rect[0] + image_rect[2])
    y1 = min(canvas_rect[1] + canvas_rect[3], image_rect[1] + image_rect[3])
    if x1 <= x0 or y1 <= y0:
        return
    sx, sy = image.width / image_rect[2], image.height / image_rect[3]
    piece = image.crop((round((x0 - image_rect[0]) * sx), round((y0 - image_rect[1]) * sy),
                        round((x1 - image_rect[0]) * sx), round((y1 - image_rect[1]) * sy)))
    cx, cy = canvas.width / canvas_rect[2], canvas.height / canvas_rect[3]
    size = (round((x1 - x0) * cx), round((y1 - y0) * cy))
    canvas.paste(piece.resize(size, Image.LANCZOS), (round((x0 - canvas_rect[0]) * cx), round((y0 - canvas_rect[1]) * cy)))


def build_target(zone, k, rects):
    """The edit target: the section guide with its painted neighbours' overlapping strips pasted in."""
    target = Image.open(GUIDES / f"{zone}-section{k}.png").convert("RGB").resize(TARGET_SIZE, Image.LANCZOS)
    for d in DEPENDS[k]:
        paste_world(target, rects[k], Image.open(section_path(zone, d)).convert("RGB"), rects[d])
    TARGETS.mkdir(parents=True, exist_ok=True)
    path = TARGETS / f"{zone}-{k}.png"
    target.save(path)
    return path


def prompt(zone, k, theme):
    common = (
        "Use case: sketch-to-render\n"
        "Asset type: one section of a large painted campaign map (one zone) for a medieval fantasy strategy game, "
        "Crownroad: Siege of Ash. The zone map is painted in four overlapping sections that are joined afterwards, so "
        "this section must match its neighbours exactly in scale, style and lighting.\n"
        "Input images: Image 1 is the EDIT TARGET. Image 2 is this zone's earlier painted map and Image 3 the game's "
        "concept map: both are STYLE REFERENCES only (match their painting style, palette, camera and finish; do not "
        "copy their layout, roads or interface).\n"
        "Primary request: repaint the flat-colour layout guide in Image 1 as a beautiful, highly detailed hand-painted 3D "
        "fantasy map in exactly the style of Images 2 and 3 (a top-down three-quarter view at about 45 degrees, painterly "
        "but crisp, warm light from the upper left, rich forests, tiny detailed props, a deep sea with foam along the coast).\n"
        "Follow the guide EXACTLY: the coastline shape and the dark-blue sea, the blue river course and the brown bridges, "
        "the tan roads and their curves, and the dark-green blobs as forests. At every RED dot paint a small empty flat "
        "clearing of trampled ground (no buildings, nothing on it) and at every WHITE dot a smaller empty patch of open "
        "ground; these spots must stay clear because castles and markers are placed there later. Elsewhere add hills, "
        "rocks, fields, small scattered cottages and trees to make it lively. Keep the scale of trees, roads and props "
        "small and even, as on a wide map.\n")
    if DEPENDS[k]:
        common += (
            f"Continuity: the strip along the {EDGE[k]} edge of Image 1 is ALREADY PAINTED: it is the finished artwork of the "
            "neighbouring section. Keep that painted strip exactly as it is and continue it seamlessly into the rest of the "
            "image, with the same colours, light, texture and level of detail, so no join is visible.\n")
    return common + theme + "\n" + (
        "Constraints: keep the same framing and every guide feature in place; the image edges are cut through the middle "
        "of the land, so paint right up to every edge with no border, vignette or fade; no castles or buildings on the red "
        "or white dots; no fog, no clouds over the land, no text, no labels, no interface, no frame, no compass, no watermark.")


def run(zones, parallel, waves=3):
    themes, rects_by_zone = zone_themes(), crops()
    for wave in ([0], [1, 2], [3])[:waves]:
        jobs = []
        for zone in zones:
            rects = rects_by_zone[zone]["sections"]
            for k in wave:
                if section_path(zone, k).exists():
                    continue
                if not all(section_path(zone, d).exists() for d in DEPENDS[k]):
                    continue
                target = build_target(zone, k, rects)
                style = GEN / f"maps/{zone}.png"
                jobs.append({"id": f"mapsec-{zone}-{k}", "out": str(section_path(zone, k).relative_to(ROOT)),
                             "inputs": [str(target.relative_to(ROOT)), str(style.relative_to(ROOT)), CONCEPT],
                             "edit": True, "prompt": prompt(zone, k, themes[zone])})
        if not jobs:
            continue
        JOBS.write_text(json.dumps(jobs, indent=1, ensure_ascii=False) + "\n")
        print(f"wave {wave}: {len(jobs)} jobs", flush=True)
        code = subprocess.call([sys.executable, str(ROOT / "art/royal/codexgen.py"), "--jobs", JOBS.stem,
                                "--parallel", str(parallel)], cwd=ROOT)
        if code == 3:
            sys.exit("usage limit reached; re-run later to continue")
    missing = [f"{z}-{k}" for z in zones for k in range(4) if not section_path(z, k).exists()]
    print("all sections painted" if not missing else f"still missing: {', '.join(missing)}")


def match_colours(image, reference, mask):
    """Shifts image's colours so that, inside mask, their mean and spread match the reference's."""
    a, b = ImageStat.Stat(image, mask), ImageStat.Stat(reference, mask)
    if a.count[0] < 100:
        return image
    bands = []
    for c, band in enumerate(image.split()):
        gain = b.stddev[c] / max(a.stddev[c], 1e-3)
        bands.append(band.point(lambda v, c=c, gain=gain: round((v - a.mean[c]) * gain + b.mean[c])))
    return Image.merge("RGB", bands)


def ramp(size, start, band, horizontal):
    """An L mask rising from 0 to 255 across `band` pixels from `start` along one axis."""
    length = size[0] if horizontal else size[1]
    line = Image.new("L", (length, 1))
    line.putdata([max(0, min(255, round((i - start) / band * 255))) for i in range(length)])
    if not horizontal:
        line = line.transpose(Image.Transpose.ROTATE_270).transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    return line.resize(size)


def stitch(zones):
    OUT.mkdir(parents=True, exist_ok=True)
    data = crops()
    for zone in zones:
        if not all(section_path(zone, k).exists() for k in range(4)):
            print("skip", zone, "(sections missing)")
            continue
        rect, rects = data[zone]["rect"], data[zone]["sections"]
        scale = Image.open(section_path(zone, 0)).width / rects[0][2]
        size = (round(rect[2] * scale), round(rect[3] * scale))
        canvas = Image.new("RGB", size)
        laid = Image.new("L", size, 0)
        overlap_x = round((rects[0][0] + rects[0][2] - rects[1][0]) * scale)
        overlap_y = round((rects[0][1] + rects[0][3] - rects[2][1]) * scale)
        for k in range(4):
            r = rects[k]
            x, y = round((r[0] - rect[0]) * scale), round((r[1] - rect[1]) * scale)
            w, h = min(round(r[2] * scale), size[0] - x), min(round(r[3] * scale), size[1] - y)
            piece = Image.open(section_path(zone, k)).convert("RGB").resize((w, h), Image.LANCZOS)
            box = (x, y, x + w, y + h)
            under = laid.crop(box)
            if k > 0:
                piece = match_colours(piece, canvas.crop(box), under)
            # Feather each later section in over a narrow band at the middle of its overlap with what is laid.
            band = max(24, round(min(w, h) * .06))
            alpha = Image.new("L", (w, h), 255)
            if k in (1, 3):
                alpha = ImageChops.multiply(alpha, ramp((w, h), overlap_x / 2 - band / 2, band, True))
            if k in (2, 3):
                alpha = ImageChops.multiply(alpha, ramp((w, h), overlap_y / 2 - band / 2, band, False))
            alpha = ImageChops.lighter(alpha, ImageChops.invert(under))
            canvas.paste(piece, (x, y), alpha)
            laid.paste(255, box)
        # A high-quality JPEG keeps the wide painting small; Godot imports it lossy with mipmaps (see IMPORT).
        canvas.save(OUT / f"{zone}.jpg", quality=92, subsampling=0, optimize=True)
        for stale in (OUT / f"{zone}.png", OUT / f"{zone}.png.import"):
            stale.unlink(missing_ok=True)
        import_file = OUT / f"{zone}.jpg.import"
        if not import_file.exists():
            import_file.write_text(IMPORT.format(zone=zone))
        corners = [canvas.getpixel((4, 4)), canvas.getpixel((size[0] - 5, 4)), canvas.getpixel((4, size[1] - 5)), canvas.getpixel((size[0] - 5, size[1] - 5))]
        sea = tuple(sum(c[i] for c in corners) // 4 for i in range(3))
        (OUT / f"{zone}.json").write_text(json.dumps({"rect": rect, "sea": "%02x%02x%02x" % sea}, indent=1))
        print("map", zone, canvas.size)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=["run", "stitch"])
    parser.add_argument("--zones", default="")
    parser.add_argument("--parallel", type=int, default=3)
    parser.add_argument("--waves", type=int, default=3, help="paint only the first N waves (NW; NE+SW; SE)")
    args = parser.parse_args()
    zones = [z for z in args.zones.split(",") if z] or list(crops())
    if args.command == "run":
        run(zones, args.parallel, args.waves)
    else:
        stitch(zones)


if __name__ == "__main__":
    main()
