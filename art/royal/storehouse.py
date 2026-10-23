#!/usr/bin/env python3
"""Install the royal storehouse art (art/royal/gen/storehouse, from art/royal/jobs/14-storehouse.json).

  store_<product id>   each pack's painted picture, assets/ui/royal/items/store_<id>.png: trimmed on its alpha and
                       centred on a square canvas, bottom-aligned so every pack stands on the same floor in its card
  store-ribbon         the blank crimson ribbon for the BEST VALUE / ONCE PER ACCOUNT tags, assets/ui/royal/kit

The tab glyphs (storetab-*) and the small store-* glyphs are painted on glyph sheet 12 and cut by glyphs.py.

  python3 art/royal/storehouse.py
  python3 art/royal/glyphs.py storetab-gold storetab-rations storetab-bundles storetab-ledger store-unit store-once \
      store-secure
"""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "art/royal/gen/storehouse"
ITEMS = ROOT / "assets/ui/royal/items"
KIT = ROOT / "assets/ui/royal/kit"
PACKS = ["gold_pouch", "gold_chest", "gold_warchest", "gold_treasury", "food_rations", "food_provisions",
         "food_stockpile", "food_granary", "starter_kit", "campaign_resupply"]
SIZE = 512


def trimmed(path):
    image = Image.open(path).convert("RGBA")
    # Codex leaves a faint haze in the alpha; drop it so the trimmed box hugs the painting.
    alpha = image.getchannel("A").point(lambda a: 0 if a < 8 else a)
    image.putalpha(alpha)
    return image.crop(alpha.point(lambda a: 255 if a > 24 else 0).getbbox())


def main():
    for pack in PACKS:
        image = trimmed(SOURCE / f"{pack}.png")
        side = round(max(image.size) * 1.04)
        canvas = Image.new("RGBA", (side, side))
        canvas.paste(image, ((side - image.width) // 2, side - image.height - round(side * .02)))
        canvas.resize((SIZE, SIZE), Image.LANCZOS).save(ITEMS / f"store_{pack}.png", optimize=True)
        print("store_" + pack, "content", image.size)
    ribbon = trimmed(SOURCE / "ribbon.png")
    ribbon = ribbon.resize((480, round(ribbon.height * 480 / ribbon.width)), Image.LANCZOS)
    ribbon.save(KIT / "store-ribbon.png", optimize=True)
    print("store-ribbon", ribbon.size)


if __name__ == "__main__":
    main()
