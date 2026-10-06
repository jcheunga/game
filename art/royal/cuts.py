#!/usr/bin/env python3
"""Cut interface pieces (icons, selected states, decorations) from the concept screens.

Cuts are listed in art/royal/cuts.json:
  {"name": "icon-heart", "from": "warband", "rect": [764, 394, 30, 30], "mode": "key-light"}
Rects are 1280x720 canvas units; pixels are taken from the full-resolution concept (or a generated
plate with "plate": true) and written to assets/ui/royal/kit/<name>.png.

Modes:
  plain      copy the pixels as they are (opaque)
  key-light  light lettering/line icon on a dark ground -> cream icon with alpha from brightness
  key-gold   gold/painted icon on a dark ground -> original colours, alpha from distance to the ground
  plate      copy from the generated clean plate (keeps its alpha)

  python3 art/royal/cuts.py [name ...]
"""
import json
import sys
from pathlib import Path
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "art/royal"))
from plates import CONCEPTS, GEN, SIZE  # noqa: E402

OUT = ROOT / "assets/ui/royal/kit"
SPEC = ROOT / "art/royal/cuts.json"
SCALE = SIZE[0] / 1280.0
_cache = {}


def source(name, plate=False):
    key = (name, plate)
    if key not in _cache:
        path = ROOT / f"assets/ui/royal/plates/{name}.png" if plate else CONCEPTS[name]
        image = Image.open(path).convert("RGBA")
        if image.size != SIZE:
            image = image.resize(SIZE, Image.LANCZOS)
        _cache[key] = image
    return _cache[key]


def crop(image, rect, pad=0):
    x, y, w, h = rect
    box = (round((x - pad) * SCALE), round((y - pad) * SCALE), round((x + w + pad) * SCALE), round((y + h + pad) * SCALE))
    return image.crop(box)


def luminance(p):
    return 0.299 * p[0] + 0.587 * p[1] + 0.114 * p[2]


def key_light(image, ground=None, ink=(240, 232, 214), lo=None, hi=None):
    """Alpha from brightness above the darkest ground; colour becomes the given ink ("auto": the icon's own)."""
    pixels = list(image.get_flattened_data())
    values = sorted(luminance(p) for p in pixels)
    if ink == "auto":
        bright = sorted(pixels, key=luminance)[-max(4, len(pixels) // 12):]
        ink = tuple(int(sum(p[i] for p in bright) / len(bright)) for i in range(3))
    lo = values[int(len(values) * 0.35)] + 6 if lo is None else lo
    hi = values[int(len(values) * 0.995)] if hi is None else hi
    out = []
    for p in pixels:
        a = max(0.0, min(1.0, (luminance(p) - lo) / max(1.0, hi - lo)))
        out.append((ink[0], ink[1], ink[2], int(a * 255)))
    result = Image.new("RGBA", image.size)
    result.putdata(out)
    return result


def key_ground(image, tolerance=38, softness=40):
    """Keep original colours; alpha from colour distance to the median border colour."""
    w, h = image.size
    border = [image.getpixel((x, 0)) for x in range(w)] + [image.getpixel((x, h - 1)) for x in range(w)] + \
             [image.getpixel((0, y)) for y in range(h)] + [image.getpixel((w - 1, y)) for y in range(h)]
    ground = tuple(sorted(c[i] for c in border)[len(border) // 2] for i in range(3))
    out = []
    for p in image.get_flattened_data():
        d = ((p[0] - ground[0]) ** 2 + (p[1] - ground[1]) ** 2 + (p[2] - ground[2]) ** 2) ** 0.5
        a = max(0.0, min(1.0, (d - tolerance) / softness))
        out.append((p[0], p[1], p[2], int(a * 255)))
    result = Image.new("RGBA", image.size)
    result.putdata(out)
    return result.filter(ImageFilter.SMOOTH) if False else result


def tighten(cut, plate):
    """Shrinks a loose rect to the bright ink inside it (icons on dark plates)."""
    image = crop(source(cut["from"], plate), cut["rect"]).convert("RGB")
    values = sorted(luminance(p) for p in image.get_flattened_data())
    threshold = values[int(len(values) * 0.35)] + cut.get("tighten_margin", 40)
    w, h = image.size
    xs, ys = [], []
    for y in range(h):
        for x in range(w):
            if luminance(image.getpixel((x, y))) > threshold:
                xs.append(x); ys.append(y)
    if not xs:
        return cut["rect"]
    x0, y0 = cut["rect"][0], cut["rect"][1]
    pad = 1.5
    return [round(x0 + min(xs) / SCALE - pad, 1), round(y0 + min(ys) / SCALE - pad, 1),
            round((max(xs) - min(xs) + 1) / SCALE + 2 * pad, 1), round((max(ys) - min(ys) + 1) / SCALE + 2 * pad, 1)]


def fill_columns(image, inner, column):
    """Paints over content inside `inner` (canvas units relative to the cut) by repeating one clean column."""
    x0, y0, w, h = (round(v * SCALE) for v in inner)
    cx = round(column * SCALE)
    pixels = image.load()
    for y in range(y0, y0 + h):
        sample = pixels[cx, y]
        for x in range(x0, x0 + w):
            pixels[x, y] = sample
    return image


def run(cut):
    plate = cut.get("mode") == "plate" or cut.get("plate", False)
    if cut.get("tighten"):
        cut["rect"] = tighten(cut, plate)
    image = crop(source(cut["from"], plate), cut["rect"], cut.get("pad", 0))
    for inner in cut.get("clear", []):
        image = fill_columns(image, inner["rect"], inner["column"])
    if cut.get("mirror_top"):
        # Rebuilds a top border hidden in the source by mirroring the bottom border.
        rows = round(cut["mirror_top"] * SCALE)
        strip = image.crop((0, image.height - rows, image.width, image.height)).transpose(Image.FLIP_TOP_BOTTOM)
        image.paste(strip, (0, 0))
    mode = cut.get("mode", "plain")
    if mode == "circle":
        # A round medallion face: the crop inside an inscribed circle with a one-pixel soft rim.
        from PIL import ImageDraw
        image = image.convert("RGBA")
        big = Image.new("L", (image.width * 4, image.height * 4), 0)
        ImageDraw.Draw(big).ellipse((2, 2, big.width - 3, big.height - 3), fill=255)
        image.putalpha(big.resize(image.size, Image.LANCZOS))
    elif mode == "key-light":
        ink = cut.get("ink", (240, 232, 214))
        image = key_light(image.convert("RGB"), ink=ink if ink == "auto" else tuple(ink), lo=cut.get("lo"), hi=cut.get("hi"))
    elif mode == "key-gold":
        image = key_ground(image.convert("RGB"), cut.get("tolerance", 38), cut.get("softness", 40))
    elif mode == "plain":
        image = image.convert("RGB")
    OUT.mkdir(parents=True, exist_ok=True)
    image.save(OUT / f"{cut['name']}.png", optimize=True)


def main():
    cuts = json.loads(SPEC.read_text()) if SPEC.exists() else []
    wanted = set(sys.argv[1:])
    for cut in cuts:
        if wanted and cut["name"] not in wanted:
            continue
        run(cut)
        print("cut", cut["name"], cut["rect"])


if __name__ == "__main__":
    main()
