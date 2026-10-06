#!/usr/bin/env python3
"""Overlay a screen spec on its 1280x720 reference to check measurements.

  python3 art/royal/specview.py art/royal/specs/warband.json [out.png] [--crop x,y,w,h] [--scale 2]

Spec format (all coordinates in 1280x720 canvas units):
{
  "screen": "warband",
  "ref": "art/royal/ref/ui-07-warband-armory.png",
  "elements": [
    {"id": "title", "kind": "text", "text": "Warband", "font": "cinzel", "weight": 700, "size": 55,
     "x": 152, "baseline": 83, "align": "left", "color": "#f0d9a0", "style": "gold"},
    {"id": "tab.spells", "kind": "rect", "rect": [743, 57, 96, 67], "role": "button"},
    ...
  ]
}
Text "x" is the left edge for align=left, the centre for align=center and the right edge for align=right.
A text element may give "width" (the measured ink width of the concept lettering) instead of "size"; the
font size is then fitted to that width. Pass --write to store fitted sizes back into the spec.
Fonts: "cinzel" (Cinzel, caps/small caps) and "crimson" (Crimson Pro). Text is drawn in magenta at 70% so it
can be compared with the concept lettering underneath; rects are drawn as cyan outlines with their ids.
"""
import json
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
FONTS = {"cinzel": ROOT / "assets/fonts/Cinzel-Variable.ttf", "crimson": ROOT / "assets/fonts/CrimsonPro-Variable.ttf"}


def font(name, weight, size):
    face = ImageFont.truetype(str(FONTS[name]), size, layout_engine=ImageFont.Layout.BASIC)
    try:
        face.set_variation_by_axes([weight])
    except Exception:
        pass
    return face


def fit(element, text, draw):
    """Font size whose rendered ink width matches the measured concept width."""
    name, weight, target = element.get("font", "crimson"), element.get("weight", 600), element["width"]
    best, error = 10, 1e9
    for size in range(8, 121):
        left, _, right, _ = font(name, weight, size).getbbox(text, anchor="ls")
        if abs((right - left) - target) < error:
            best, error = size, abs((right - left) - target)
    return best


def main():
    args = [a for a in sys.argv[1:]]
    crop = None
    scale = 1.0
    if "--crop" in args:
        i = args.index("--crop"); crop = [int(v) for v in args[i + 1].split(",")]; del args[i:i + 2]
    write = "--write" in args
    if write:
        args.remove("--write")
    if "--scale" in args:
        i = args.index("--scale"); scale = float(args[i + 1]); del args[i:i + 2]
    spec_path = Path(args[0])
    out = args[1] if len(args) > 1 else "/tmp/specview.png"
    spec = json.loads(spec_path.read_text())
    base = Image.open(ROOT / spec["ref"]).convert("RGBA")
    if base.size != (1280, 720):
        base = base.resize((1280, 720), Image.LANCZOS)
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    label_font = ImageFont.truetype("/System/Library/Fonts/Menlo.ttc", 9)
    for element in spec["elements"]:
        if element.get("kind") == "text":
            text = element["text"]
            if "width" in element:
                element["size"] = fit(element, text, draw)
            face = font(element.get("font", "crimson"), element.get("weight", 600), int(round(element["size"])))
            left, _, right, _ = face.getbbox(text, anchor="ls")
            width = right - left
            x = element["x"] - left
            if element.get("align", "left") == "left":
                element["pen_x"] = round(x, 1)
            if element.get("align") == "center":
                x -= width / 2
            elif element.get("align") == "right":
                x -= width
            draw.text((x, element["baseline"]), text, font=face, fill=(255, 0, 255, 175), anchor="ls")
        rect = element.get("rect")
        if rect:
            x, y, w, h = rect
            draw.rectangle([x, y, x + w, y + h], outline=(0, 255, 255, 220), width=1)
            draw.text((x + 2, y + 1), element["id"], fill=(0, 255, 255, 255), font=label_font)
    if write:
        spec_path.write_text(json.dumps(spec, indent=1))
    image = Image.alpha_composite(base, layer).convert("RGB")
    if crop:
        x, y, w, h = crop
        image = image.crop((x, y, x + w, y + h))
    if scale != 1:
        image = image.resize((int(image.width * scale), int(image.height * scale)), Image.LANCZOS)
    image.save(out)


if __name__ == "__main__":
    main()
