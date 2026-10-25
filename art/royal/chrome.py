#!/usr/bin/env python3
"""Generic concept chrome for screens without a concept of their own: the modal frame and header,
navy and gold buttons, tabs, tiles, parchment and the close button, cut from the clean plates.

  python3 art/royal/chrome.py [name ...]
"""
import sys
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
PLATES = ROOT / "assets/ui/royal/plates"
OUT = ROOT / "assets/ui/royal/kit"
SCALE = 1672 / 1280


def plate(name):
    return Image.open(PLATES / f"{name}.png").convert("RGBA")


def cut(image, x, y, w, h):
    return image.crop((round(x * SCALE), round(y * SCALE), round((x + w) * SCALE), round((y + h) * SCALE)))


def tile_fill(target, texture, box):
    """Fills box (pixels) of target by tiling texture."""
    x0, y0, x1, y1 = box
    for y in range(y0, y1, texture.height):
        for x in range(x0, x1, texture.width):
            piece = texture.crop((0, 0, min(texture.width, x1 - x), min(texture.height, y1 - y)))
            target.paste(piece, (x, y))


def clear_tab_corners(tab):
    """Clears the sky above the tab's chamfered top corners, and the next tab's chamfer that meets its right one."""
    w = tab.width
    pixels = tab.load()
    for corner, reach in ((0, 7), (w - 1, 8)):
        def depth(x, y): return abs(x - corner) + y
        # Everything above the chamfer's dark outline goes, and so does any sky the corner cut leaves beside it.
        stack, seen = [(corner, 0)], set()
        while stack:
            x, y = stack.pop()
            if (x, y) in seen or not 0 <= x < w or y < 0 or depth(x, y) > reach + 3:
                continue
            seen.add((x, y))
            r, g, b, _ = pixels[x, y]
            if depth(x, y) > reach and not (b >= r - 5 and 0.299 * r + 0.587 * g + 0.114 * b > 45):
                continue
            pixels[x, y] = (r, g, b, 0)
            stack += [(x + 1, y), (x - 1, y), (x, y + 1)]
    return tab


def modal_frame(hub):
    """The hub's brass border around plain planks."""
    frame = cut(hub, 33, 92, 1214, 607)
    wood = cut(hub, 60, 260, 300, 120)
    border = round(16 * SCALE)
    tile_fill(frame, wood, (border, border, frame.width - border, frame.height - border))
    # Rebuild the four edges from clean samples so footer and tab outlines on the plate don't repeat.
    corner = round(34 * SCALE)
    w, h = frame.size
    for top in (0, h - border):
        sample = frame.crop((corner, top, corner + round(12 * SCALE), top + border))
        tile_fill(frame, sample, (corner, top, w - corner, top + border))
    for left in (0, w - border):
        sample = frame.crop((left, round(200 * SCALE), left + border, round(212 * SCALE)))
        tile_fill(frame, sample, (left, corner, left + border, h - corner))
    frame.save(OUT / "modal-frame.png", optimize=True)


def modal_header(multiplayer):
    """Header band: plain navy enamel on the left, castle art on the right; the emblem is drawn live."""
    header = cut(multiplayer, 33, 22, 1213, 98)
    plain = header.crop((round(300 * SCALE), 0, round(320 * SCALE), header.height))
    for x in range(round(14 * SCALE), round(300 * SCALE), plain.width):
        header.paste(plain.crop((0, 0, min(plain.width, round(300 * SCALE) - x), plain.height)), (x, 0))
    # The multiplayer plate paints its close button into the band; screens draw a live one, so the band's
    # right end is replaced by a mirror image of the forest beside it (seamless along the fold).
    def px(v): return round(v * SCALE)
    fold, right, top, bottom = px(1184 - 33), px(1240 - 33), px(25 - 22), px(115 - 22)
    forest = header.crop((2 * fold - right, top, fold, bottom)).transpose(Image.FLIP_LEFT_RIGHT)
    header.paste(forest, (fold, top))
    header.save(OUT / "modal-header.png", optimize=True)


def main(only=()):
    OUT.mkdir(parents=True, exist_ok=True)
    def wanted(name): return not only or name in only
    warband, hub, multiplayer, codex = plate("warband"), plate("hub"), plate("multiplayer"), plate("codex")
    # The tab is the strip's second tab with its whole rim: both gold dividers it shares with its neighbours and
    # the rail beneath, so a tab standing on its own is framed on all four sides.
    for name, rect in {"button-navy": (753, 610, 216, 62), "button-gold": (979, 610, 265, 62), "tab": (730.3, 56, 111.8, 72.6),
                       "tile": (753, 377, 181, 68), "close-button": (1186, 27, 54, 54)}.items():
        if wanted(name):
            piece = cut(warband, *rect)
            (clear_tab_corners(piece) if name == "tab" else piece).save(OUT / f"{name}.png", optimize=True)
    if wanted("paper"): cut(codex, 126, 145, 535, 512).save(OUT / "paper.png", optimize=True)
    if wanted("modal-frame"): modal_frame(hub)
    if wanted("modal-header"): modal_header(multiplayer)
    print("chrome done")


if __name__ == "__main__":
    main(sys.argv[1:])
