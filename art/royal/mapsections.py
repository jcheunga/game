#!/usr/bin/env python3
"""Paint each zone's atlas in overlapping sections and stitch them into one sharp map.

A zone's world is far too large for one Codex image (about 1.6 megapixels) to stay sharp when zoomed in, so the
map is painted as a GRID x GRID mosaic of overlapping 16:9 sections over the zone's layout guide
(art/royal/gen/map-guides/<zone>.png and crops.json, written by `UiReviewSmoke --map-guides`). Sections are painted
in diagonal waves from the north-west corner: each later section is edited from a target that already carries the
painted strips of its finished neighbours above and to its left, so the painting continues across the joins. The
zone's previous painting, cropped to the same area, is passed along as a content reference so colours, terrain
and props stay consistent. `stitch` colour-matches the sections, feathers them together and installs the map as
2 x 2 texture tiles (none wider than 4096 px, for older phone GPUs).

  python3 art/royal/mapsections.py run [--zones city,harbor] [--parallel 3]   # targets + Codex, wave by wave
  python3 art/royal/mapsections.py stitch [--zones ...]                      # assets/world/royal/maps/<zone>-<k>.jpg + .json
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
GRID = 4
# Neighbouring sections overlap by this fraction of a section's width (or height).
OVERLAP = .25
SECTIONS = GEN / f"map-sections/grid{GRID}"
TARGETS = SECTIONS / "targets"
REFERENCES = SECTIONS / "references"
JOBS = ROOT / "art/royal/jobs/08-map-sections.json"
OUT = ROOT / "assets/world/royal/maps"
CONCEPT = "output/crownroad-ui-ideas-2026-10-06/06-cartographers-atlas-home.png"
TARGET_SIZE = (1920, 1080)
TILES = 2
# The zone paintings import as GPU-compressed textures with mipmaps, so a zoomed-out atlas stays smooth and a
# large map fits in a phone's memory.
IMPORT = """[remap]

importer="texture"
type="CompressedTexture2D"

[deps]

source_file="res://assets/world/royal/maps/{name}.jpg"

[params]

compress/mode=2
compress/high_quality=true
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


def painting_rect(zone):
    return json.loads((GUIDES / "crops.json").read_text())[zone]["rect"]


def section_rects(rect):
    """World rects of the GRID x GRID sections, keyed (row, column); each is 16:9 like the whole painting."""
    fraction = 1 / (GRID - (GRID - 1) * OVERLAP)
    w, h = rect[2] * fraction, rect[3] * fraction
    step_x, step_y = w * (1 - OVERLAP), h * (1 - OVERLAP)
    return {(i, j): [rect[0] + j * step_x, rect[1] + i * step_y, w, h] for i in range(GRID) for j in range(GRID)}


def depends(i, j):
    """The sections painted before this one that it overlaps: above, to the left and diagonally above-left."""
    return [(a, b) for a, b in ((i - 1, j), (i, j - 1), (i - 1, j - 1)) if a >= 0 and b >= 0]


def section_path(zone, i, j):
    return SECTIONS / f"{zone}-{i}{j}.png"


def crop_world(image, image_rect, rect, size):
    """The part of `image` (covering world rect image_rect) inside world rect `rect`, resized to `size`."""
    sx, sy = image.width / image_rect[2], image.height / image_rect[3]
    box = (round((rect[0] - image_rect[0]) * sx), round((rect[1] - image_rect[1]) * sy),
           round((rect[0] + rect[2] - image_rect[0]) * sx), round((rect[1] + rect[3] - image_rect[1]) * sy))
    return image.crop(box).resize(size, Image.LANCZOS)


def paste_world(canvas, canvas_rect, image, image_rect):
    """Pastes the part of `image` (covering world rect image_rect) that overlaps canvas_rect onto canvas."""
    x0, y0 = max(canvas_rect[0], image_rect[0]), max(canvas_rect[1], image_rect[1])
    x1 = min(canvas_rect[0] + canvas_rect[2], image_rect[0] + image_rect[2])
    y1 = min(canvas_rect[1] + canvas_rect[3], image_rect[1] + image_rect[3])
    if x1 <= x0 or y1 <= y0:
        return
    cx, cy = canvas.width / canvas_rect[2], canvas.height / canvas_rect[3]
    piece = crop_world(image, image_rect, [x0, y0, x1 - x0, y1 - y0], (round((x1 - x0) * cx), round((y1 - y0) * cy)))
    canvas.paste(piece, (round((x0 - canvas_rect[0]) * cx), round((y0 - canvas_rect[1]) * cy)))


def previous_painting(zone):
    """The zone's installed painting as one image with its world rect (tiled or single), for content references."""
    meta = json.loads((OUT / f"{zone}.json").read_text())
    rect = meta["rect"]
    if "tiles" not in meta:
        return Image.open(OUT / f"{zone}.jpg").convert("RGB"), rect
    scale = Image.open(OUT / f"{zone}-0.jpg").width / meta["tiles"][0][2]
    canvas = Image.new("RGB", (round(rect[2] * scale), round(rect[3] * scale)))
    for k, tile in enumerate(meta["tiles"]):
        paste_world(canvas, rect, Image.open(OUT / f"{zone}-{k}.jpg").convert("RGB"), tile)
    return canvas, rect


def build_inputs(zone, i, j, rects, guide, reference):
    """The edit target (the section's guide with its painted neighbours' strips pasted in) and the content reference."""
    target = crop_world(guide, painting_rect(zone), rects[(i, j)], TARGET_SIZE)
    for d in depends(i, j):
        paste_world(target, rects[(i, j)], Image.open(section_path(zone, *d)).convert("RGB"), rects[d])
    TARGETS.mkdir(parents=True, exist_ok=True)
    REFERENCES.mkdir(parents=True, exist_ok=True)
    target_path, reference_path = TARGETS / f"{zone}-{i}{j}.png", REFERENCES / f"{zone}-{i}{j}.png"
    target.save(target_path)
    crop_world(reference[0], reference[1], rects[(i, j)], TARGET_SIZE).save(reference_path)
    return target_path, reference_path


def prompt(i, j, theme):
    edges = [name for name, present in (("top", i > 0), ("left", j > 0)) if present]
    text = (
        "Use case: sketch-to-render\n"
        "Asset type: one tile of a large, highly detailed painted campaign map (one zone) for a medieval fantasy strategy "
        "game, Crownroad: Siege of Ash. The zone map is painted as a mosaic of overlapping tiles that are joined "
        "afterwards, so this tile must match its neighbours exactly in scale, style, colour and lighting.\n"
        "Input images: Image 1 is the EDIT TARGET. Image 2 is a LOW-RESOLUTION version of this exact area, already painted: "
        "it is the CONTENT REFERENCE (match its colours, terrain, forests, fields, rocks, buildings and props, placed the "
        "same way). Image 3 is the game's concept map: a STYLE REFERENCE only (do not copy its layout or interface).\n"
        "Primary request: repaint the flat-colour layout guide in Image 1 as a crisp, sharp, highly detailed hand-painted 3D "
        "fantasy map at full resolution, matching Image 2's content and Image 3's finish (a top-down three-quarter view at "
        "about 45 degrees, painterly but crisp, warm light from the upper left, a deep sea with foam along the coast). This "
        "tile covers a small part of the map, so render fine detail: individual trees, rooftops, fences, rocks and "
        "furrows, with clean edges and no blur.\n"
        "Follow the guide EXACTLY: the coastline shape and the dark-blue sea, the blue river course and the brown bridges, "
        "the tan roads and their curves, and the dark-green blobs as forests. At every RED dot paint a small empty flat "
        "clearing of trampled ground (no buildings, nothing on it) and at every WHITE dot a smaller empty patch of open "
        "ground; these spots must stay clear because castles and markers are placed there later. Keep the scale of "
        "trees, roads and props the same as in Image 2, scaled up to this tile.\n")
    if edges:
        text += (
            f"Continuity: the strip along the {' and '.join(edges)} edge of Image 1 is ALREADY PAINTED: it is the finished "
            "artwork of the neighbouring tile. Keep that painted strip exactly as it is and continue it seamlessly into the "
            "rest of the image, with the same colours, light, texture and level of detail, so no join is visible.\n")
    return text + theme + "\n" + (
        "Constraints: keep the same framing and every guide feature in place; the image edges are cut through the middle "
        "of the map, so paint right up to every edge with no border, vignette or fade; no castles or buildings on the red "
        "or white dots; no fog, no clouds over the land, no text, no labels, no interface, no frame, no compass, no watermark.")


def run(zones, parallel):
    themes = zone_themes()
    references = {}
    for wave in range(2 * GRID - 1):
        jobs = []
        for zone in zones:
            rects = section_rects(painting_rect(zone))
            guide = Image.open(GUIDES / f"{zone}.png").convert("RGB")
            for (i, j) in sorted(rects):
                if i + j != wave or section_path(zone, i, j).exists():
                    continue
                if not all(section_path(zone, *d).exists() for d in depends(i, j)):
                    continue
                if zone not in references:
                    references[zone] = previous_painting(zone)
                target, ref = build_inputs(zone, i, j, rects, guide, references[zone])
                jobs.append({"id": f"mapsec{GRID}-{zone}-{i}{j}", "out": str(section_path(zone, i, j).relative_to(ROOT)),
                             "inputs": [str(target.relative_to(ROOT)), str(ref.relative_to(ROOT)), CONCEPT],
                             "edit": True, "prompt": prompt(i, j, themes[zone])})
        if not jobs:
            continue
        JOBS.write_text(json.dumps(jobs, indent=1, ensure_ascii=False) + "\n")
        print(f"wave {wave}: {len(jobs)} jobs", flush=True)
        code = subprocess.call([sys.executable, str(ROOT / "art/royal/codexgen.py"), "--jobs", JOBS.stem,
                                "--parallel", str(parallel)], cwd=ROOT)
        if code == 3:
            sys.exit("usage limit reached; re-run later to continue")
    JOBS.unlink(missing_ok=True)
    missing = [f"{z}-{i}{j}" for z in zones for i in range(GRID) for j in range(GRID) if not section_path(z, i, j).exists()]
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
    for zone in zones:
        rect = painting_rect(zone)
        rects = section_rects(rect)
        if not all(section_path(zone, i, j).exists() for (i, j) in rects):
            print("skip", zone, "(sections missing)")
            continue
        scale = Image.open(section_path(zone, 0, 0)).width / rects[(0, 0)][2]
        size = (round(rect[2] * scale), round(rect[3] * scale))
        canvas = Image.new("RGB", size)
        laid = Image.new("L", size, 0)
        overlap_x = round(rects[(0, 0)][2] * OVERLAP * scale)
        overlap_y = round(rects[(0, 0)][3] * OVERLAP * scale)
        for (i, j) in sorted(rects, key=lambda key: (key[0] + key[1], key)):
            r = rects[(i, j)]
            x, y = round((r[0] - rect[0]) * scale), round((r[1] - rect[1]) * scale)
            w, h = min(round(r[2] * scale), size[0] - x), min(round(r[3] * scale), size[1] - y)
            piece = Image.open(section_path(zone, i, j)).convert("RGB").resize((w, h), Image.LANCZOS)
            box = (x, y, x + w, y + h)
            under = laid.crop(box)
            if i or j:
                piece = match_colours(piece, canvas.crop(box), under)
            # Feather each later section in over a narrow band at the middle of its overlap with what is laid.
            band = max(24, round(min(w, h) * .06))
            alpha = Image.new("L", (w, h), 255)
            if j:
                alpha = ImageChops.multiply(alpha, ramp((w, h), overlap_x / 2 - band / 2, band, True))
            if i:
                alpha = ImageChops.multiply(alpha, ramp((w, h), overlap_y / 2 - band / 2, band, False))
            alpha = ImageChops.lighter(alpha, ImageChops.invert(under))
            canvas.paste(piece, (x, y), alpha)
            laid.paste(255, box)
        # Split into 2 x 2 tiles that overlap by a few pixels, so no hairline shows where they meet.
        tiles, pad = [], 4
        for k in range(TILES * TILES):
            ti, tj = divmod(k, TILES)
            x0, x1 = max(0, size[0] * tj // TILES - pad), min(size[0], size[0] * (tj + 1) // TILES + pad)
            y0, y1 = max(0, size[1] * ti // TILES - pad), min(size[1], size[1] * (ti + 1) // TILES + pad)
            name = f"{zone}-{k}"
            canvas.crop((x0, y0, x1, y1)).save(OUT / f"{name}.jpg", quality=92, subsampling=0, optimize=True)
            if not (OUT / f"{name}.jpg.import").exists():
                (OUT / f"{name}.jpg.import").write_text(IMPORT.format(name=name))
            tiles.append([rect[0] + x0 / scale, rect[1] + y0 / scale, (x1 - x0) / scale, (y1 - y0) / scale])
        for stale in (f"{zone}.jpg", f"{zone}.jpg.import", f"{zone}.png", f"{zone}.png.import"):
            (OUT / stale).unlink(missing_ok=True)
        corners = [canvas.getpixel((4, 4)), canvas.getpixel((size[0] - 5, 4)), canvas.getpixel((4, size[1] - 5)), canvas.getpixel((size[0] - 5, size[1] - 5))]
        sea = tuple(sum(c[i] for c in corners) // 4 for i in range(3))
        (OUT / f"{zone}.json").write_text(json.dumps({"rect": rect, "sea": "%02x%02x%02x" % sea,
                                                     "tiles": [[round(v, 2) for v in t] for t in tiles]}, indent=1))
        print("map", zone, canvas.size)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=["run", "stitch"])
    parser.add_argument("--zones", default="")
    parser.add_argument("--parallel", type=int, default=3)
    args = parser.parse_args()
    zones = [z for z in args.zones.split(",") if z] or list(json.loads((GUIDES / "crops.json").read_text()))
    if args.command == "run":
        run(zones, args.parallel)
    else:
        stitch(zones)


if __name__ == "__main__":
    main()
