#!/usr/bin/env python3
"""Install the royal storehouse art (art/royal/gen/storehouse, from art/royal/jobs/14-storehouse.json).

  store_<product id>   each pack's painted picture, assets/ui/royal/items/store_<id>.png: trimmed on its alpha and
                       centred on a square canvas, bottom-aligned so every pack stands on the same floor in its card
  store-ribbon         the blank crimson ribbon for the BEST VALUE / ONCE PER ACCOUNT tags, assets/ui/royal/kit
  storetab-shards      the Shards tab's glyph: a crystal cluster drawn here in the carved cream style of the others

The other tab glyphs (storetab-*) and the small store-* glyphs are painted on glyph sheet 12 and cut by glyphs.py.

  python3 art/royal/storehouse.py                          everything
  python3 art/royal/storehouse.py shard_pouch storetab-shards   just the named packs / pieces
  python3 art/royal/glyphs.py storetab-gold storetab-rations storetab-bundles storetab-ledger store-unit store-once \
      store-secure
"""
import math
import sys
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "art/royal/gen/storehouse"
ITEMS = ROOT / "assets/ui/royal/items"
KIT = ROOT / "assets/ui/royal/kit"
PACKS = ["gold_pouch", "gold_chest", "gold_warchest", "gold_treasury", "food_rations", "food_provisions",
         "food_stockpile", "food_granary", "starter_kit", "campaign_resupply",
         "shard_pouch", "shard_casket", "shard_coffer", "shard_reliquary"]
SIZE = 512
CREAM = (250, 238, 192)


def trimmed(path):
    image = Image.open(path).convert("RGBA")
    # Codex leaves a faint haze in the alpha; drop it so the trimmed box hugs the painting.
    alpha = image.getchannel("A").point(lambda a: 0 if a < 8 else a)
    image.putalpha(alpha)
    return image.crop(alpha.point(lambda a: 255 if a > 24 else 0).getbbox())


def shard_tab_glyph():
    """Three crystals, the tall one in front, with carved ridge lines; drawn 8x and reduced."""
    k = 8
    width, height = 48 * k, 52 * k
    mask = Image.new("L", (width, height), 0)
    draw = ImageDraw.Draw(mask)

    def crystal(cx, base, half, tall, shoulder, angle):
        body = [(0, -tall), (half, -tall + shoulder), (half, -shoulder * .35), (0, 0), (-half, -shoulder * .35), (-half, -tall + shoulder)]
        ridge = [(-half, -tall + shoulder), (0, -tall + shoulder * 1.45), (half, -tall + shoulder)]
        spine = [(0, -tall + shoulder * 1.45), (0, -shoulder * .15)]
        turn = math.radians(angle)

        def place(point):
            x, y = point
            return cx + x * math.cos(turn) - y * math.sin(turn), base + x * math.sin(turn) + y * math.cos(turn)
        return [place(p) for p in body], [place(p) for p in ridge], [place(p) for p in spine]

    def grown(poly, amount):
        cx = sum(x for x, _ in poly) / len(poly)
        cy = sum(y for _, y in poly) / len(poly)
        return [(x + (x - cx) / math.hypot(x - cx, y - cy) * amount, y + (y - cy) / math.hypot(x - cx, y - cy) * amount) for x, y in poly]

    line = int(2.2 * k)
    sides = [crystal(width * .27, height * .97, 6.5 * k, 30 * k, 8 * k, -24), crystal(width * .73, height * .97, 6.5 * k, 27 * k, 8 * k, 22)]
    centre = crystal(width * .5, height * .99, 9 * k, 50 * k, 11 * k, 0)
    for body, ridge, spine in sides:
        draw.polygon(body, fill=255)
        draw.line(ridge, fill=0, width=line, joint="curve")
        draw.line(spine, fill=0, width=line)
    # The centre crystal stands in front: clear a gap around it, then fill it.
    draw.polygon(grown(centre[0], 2.6 * k), fill=0)
    draw.polygon(centre[0], fill=255)
    draw.line(centre[1], fill=0, width=line, joint="curve")
    draw.line(centre[2], fill=0, width=line)
    mask = mask.crop(mask.getbbox())
    glyph = Image.new("RGBA", mask.size, CREAM + (0,))
    glyph.putalpha(mask)
    glyph = glyph.resize((round(mask.width / k), round(mask.height / k)), Image.LANCZOS)
    glyph.save(KIT / "storetab-shards.png", optimize=True)
    print("storetab-shards", glyph.size)


def install_pack(pack):
    image = trimmed(SOURCE / f"{pack}.png")
    side = round(max(image.size) * 1.04)
    canvas = Image.new("RGBA", (side, side))
    canvas.paste(image, ((side - image.width) // 2, side - image.height - round(side * .02)))
    canvas.resize((SIZE, SIZE), Image.LANCZOS).save(ITEMS / f"store_{pack}.png", optimize=True)
    print("store_" + pack, "content", image.size)


def install_ribbon():
    ribbon = trimmed(SOURCE / "ribbon.png")
    ribbon = ribbon.resize((480, round(ribbon.height * 480 / ribbon.width)), Image.LANCZOS)
    ribbon.save(KIT / "store-ribbon.png", optimize=True)
    print("store-ribbon", ribbon.size)


def main(names):
    pieces = {"store-ribbon": install_ribbon, "storetab-shards": shard_tab_glyph}
    for name in names or PACKS + list(pieces):
        if name in pieces:
            pieces[name]()
        elif name in PACKS:
            install_pack(name)
        else:
            sys.exit(f"unknown piece: {name}")


if __name__ == "__main__":
    main(sys.argv[1:])
