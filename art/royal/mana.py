#!/usr/bin/env python3
"""Derive the battle HUD's mana pieces from the clean-steel courage kit.

Mana is royal blue, so courage, which the concept painted blue, takes the amber of its flame icon instead:
  hud-fill-mana            the concept's courage fill, shifted to royal blue
  hud-fill-courage-ember   the same fill in amber, for the courage bar
  hud-cost-mana            the bronze card cost badge with a blue centre (the gold ring is kept)
  hud-mana                 a faceted mana crystal for the meter socket, drawn at 8x and reduced

  python3 art/royal/mana.py
"""
import colorsys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
KIT = ROOT / "assets/ui/royal/kit"
BLUE = 218 / 360.0
AMBER = 31 / 360.0


def recolour(pixel, hue, sat_scale, val_scale):
    r, g, b = (c / 255.0 for c in pixel[:3])
    _, s, v = colorsys.rgb_to_hsv(r, g, b)
    r, g, b = colorsys.hsv_to_rgb(hue, min(1.0, s * sat_scale), min(1.0, v * val_scale))
    return (round(r * 255), round(g * 255), round(b * 255)) + tuple(pixel[3:])


def fills():
    source = Image.open(KIT / "hud-fill-courage.png").convert("RGB")
    for name, hue, sat, val in (("hud-fill-mana", BLUE, 0.95, 1.3), ("hud-fill-courage-ember", AMBER, 0.95, 1.4)):
        out = Image.new("RGB", source.size)
        out.putdata([recolour(p, hue, sat, val) for p in source.get_flattened_data()])
        out.save(KIT / f"{name}.png")


def badge():
    source = Image.open(KIT / "hud-cost.png").convert("RGBA")
    w, h = source.size
    cx, cy = (w - 1) / 2, (h - 1) / 2
    out = source.copy()
    for y in range(h):
        for x in range(w):
            p = source.getpixel((x, y))
            r = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
            # The gold ring starts at r ~26; blend the last pixel so the seam stays soft.
            t = max(0.0, min(1.0, 25.5 - r))
            if t <= 0:
                continue
            v = recolour(p, BLUE, 0.8, 1.6)
            out.putpixel((x, y), tuple(round(p[i] + (v[i] - p[i]) * t) for i in range(3)) + (p[3],))
    out.save(KIT / "hud-cost-mana.png")


def crystal():
    # Canvas matches hud-flame (37 x 42) so the socket shows both icons at the same density.
    w, h, k = 37, 42, 8
    W, H = w * k, h * k
    cx = W / 2
    top, shoulder, waist, bottom = 4 * k, 14 * k, 27 * k, 38 * k
    half = 12 * k
    inner = 3.8 * k
    outline = [(cx, top), (cx + half, shoulder), (cx + half * 0.9, waist), (cx, bottom),
               (cx - half * 0.9, waist), (cx - half, shoulder)]

    glow = Image.new("RGBA", (W, H))
    ImageDraw.Draw(glow).polygon(outline, fill=(80, 150, 255, 190))
    glow = glow.filter(ImageFilter.GaussianBlur(3.2 * k))

    gem = Image.new("RGBA", (W, H))
    d = ImageDraw.Draw(gem)
    d.polygon(outline, fill=(12, 34, 92, 255))
    edge = 1.1 * k
    facets = [
        # left crown, right crown, the bright table, left and right pavilions, the dark keel
        ([(cx, top + edge), (cx - half + edge, shoulder), (cx - inner, shoulder + 1.5 * k)], (170, 214, 255)),
        ([(cx, top + edge), (cx + half - edge, shoulder), (cx + inner, shoulder + 1.5 * k)], (88, 150, 240)),
        ([(cx, top + edge), (cx + inner, shoulder + 1.5 * k), (cx + inner, waist - 1 * k), (cx, bottom - 3 * k),
          (cx - inner, waist - 1 * k), (cx - inner, shoulder + 1.5 * k)], (124, 186, 255)),
        ([(cx - half + edge, shoulder), (cx - inner, shoulder + 1.5 * k), (cx - inner, waist - 1 * k),
          (cx - half * 0.9 + edge, waist)], (58, 118, 218)),
        ([(cx + half - edge, shoulder), (cx + inner, shoulder + 1.5 * k), (cx + inner, waist - 1 * k),
          (cx + half * 0.9 - edge, waist)], (36, 84, 188)),
        ([(cx - half * 0.9 + edge, waist), (cx - inner, waist - 1 * k), (cx, bottom - 3 * k), (cx, bottom - edge)], (46, 100, 204)),
        ([(cx + half * 0.9 - edge, waist), (cx + inner, waist - 1 * k), (cx, bottom - 3 * k), (cx, bottom - edge)], (24, 58, 150)),
    ]
    for points, colour in facets:
        d.polygon(points, fill=colour + (255,))
    # A cool glint on the upper-left crown and a spark on the table.
    d.polygon([(cx - 1.2 * k, top + 3 * k), (cx - half + 3.2 * k, shoulder - 0.4 * k), (cx - 3.6 * k, shoulder + 0.3 * k)],
              fill=(232, 244, 255, 235))
    d.ellipse([cx - 1.6 * k, shoulder + 3 * k, cx + 0.2 * k, shoulder + 4.8 * k], fill=(248, 252, 255, 220))
    # Gold rim, to sit with the bronze-edged sockets.
    d.line(outline + [outline[0]], fill=(214, 168, 82, 255), width=round(0.9 * k), joint="curve")

    out = Image.alpha_composite(glow, gem).resize((w, h), Image.LANCZOS)
    out.save(KIT / "hud-mana.png")


if __name__ == "__main__":
    fills()
    badge()
    crystal()
    print("wrote hud-fill-mana, hud-fill-courage-ember, hud-cost-mana, hud-mana")
