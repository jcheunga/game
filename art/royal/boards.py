#!/usr/bin/env python3
"""Install the painted boards and their pieces (art/royal/jobs/15-boards.json) for RoyalBoard screens.

  boards   art/royal/gen/boards/<look>.png    -> assets/ui/royal/plates/board-<look>.png (1672x941, alpha)
  emblems  art/royal/gen/emblems/sheet-*.png  -> assets/ui/royal/kit/emblem-<name>.png (3x3 sheets)
  sheets   art/royal/gen/sheets/<sheet>.png   -> assets/ui/royal/items/<name>.png (3x3 sheets, 512 px squares)
  scenes   art/royal/gen/scenes/<name>.png    -> assets/ui/royal/items/scene-<name>.png
  kit      assets/ui/royal/kit/paper.png       -> paper-clean.png (page buttons painted out)

A sheet's nine largest opaque objects are taken in reading order, each trimmed to its alpha with an even
margin. `python3 art/royal/boards.py [boards|emblems|sheets|scenes ...]` (default: all), then
`godot --headless --path . --import`.
"""
import sys
from pathlib import Path
from PIL import Image, ImageChops, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
GEN = ROOT / "art/royal/gen"
PLATES = ROOT / "assets/ui/royal/plates"
KIT = ROOT / "assets/ui/royal/kit"
ITEMS = ROOT / "assets/ui/royal/items"
SIZE = (1672, 941)

BOARDS = {"banners": "board-banners", "camp": "board-camp"}
EMBLEMS = {
    "sheet-a": ["tower", "expeditions", "gifts", "storehouse", "progression", "guild", "friends", "arena", "rankings"],
    "sheet-b": ["bounties", "bossrush", "raid", "event", "lantern", "crown", "hourglass", "rations", "horn"],
}
SHEETS = {
    # The honours sheet's first cell (a resupply crate) went unused once the storehouse got its own pack art.
    "honours": [None, "arena-bronze", "arena-silver", "arena-gold", "arena-platinum", "arena-diamond",
                "perk-guild_vitality", "perk-guild_haste", "perk-guild_fortune"],
    "bounties": ["bounty-enemy_defeats", "bounty-unit_deploys", "bounty-spell_casts", "bounty-stages_cleared", "bounty-gold_earned",
                 "bounty-arena_wins", "bounty-endless_wave", "bounty-boss_kills", "bounty-expeditions"],
}
SCENE_WIDTH = 1100


def alpha_box(image, threshold=24):
    return image.getchannel("A").point(lambda a: 255 if a > threshold else 0).getbbox()


def boards():
    for source, name in BOARDS.items():
        path = GEN / "boards" / f"{source}.png"
        if not path.exists():
            print("missing", path); continue
        image = Image.open(path).convert("RGBA")
        if image.size != SIZE:
            image = image.resize(SIZE, Image.LANCZOS)
        image.save(PLATES / f"{name}.png", optimize=True)
        print(name, Image.open(path).size)


def blobs(image, scale=4):
    """The nine objects of a 3 x 3 sheet as (box, mask) at full size, in reading order. Each opaque blob belongs
    to the cell holding its centre, so a neighbour reaching into a cell stays with its own object; two objects
    that touch are parted along the grid line between their cells."""
    small = image.getchannel("A").resize((image.width // scale, image.height // scale), Image.BILINEAR)
    mask = small.point(lambda a: 255 if a > 64 else 0)
    w, h = mask.size
    pixels = mask.load()
    cell_of = lambda x, y: min(2, y * 3 // h) * 3 + min(2, x * 3 // w)
    seen = bytearray(w * h)
    members = [[] for _ in range(9)]
    for y in range(h):
        for x in range(w):
            if pixels[x, y] == 0 or seen[y * w + x]:
                continue
            stack, points = [(x, y)], []
            seen[y * w + x] = 1
            while stack:
                px, py = stack.pop()
                points.append((px, py))
                for nx, ny in ((px + 1, py), (px - 1, py), (px, py + 1), (px, py - 1)):
                    if 0 <= nx < w and 0 <= ny < h and pixels[nx, ny] and not seen[ny * w + nx]:
                        seen[ny * w + nx] = 1
                        stack.append((nx, ny))
            by_cell = {}
            for point in points:
                by_cell.setdefault(cell_of(*point), []).append(point)
            if sum(len(part) >= len(points) * .15 for part in by_cell.values()) > 1:
                for cell, part in by_cell.items():
                    members[cell].extend(part)
            else:
                cx = sum(p[0] for p in points) // len(points); cy = sum(p[1] for p in points) // len(points)
                members[cell_of(cx, cy)].extend(points)
    objects = []
    for points in members:
        if not points:
            objects.append(None); continue
        own = Image.new("L", (w, h))
        draw = own.load()
        for px, py in points:
            draw[px, py] = 255
        xs = [p[0] for p in points]; ys = [p[1] for p in points]
        box = (min(xs) * scale, min(ys) * scale, (max(xs) + 1) * scale, (max(ys) + 1) * scale)
        objects.append((box, own.filter(ImageFilter.MaxFilter(3)).resize(image.size, Image.BILINEAR)))
    return objects


def cells(path, names):
    """Yields (name, trimmed RGBA object) for each object of a 3 x 3 sheet, in reading order."""
    sheet = Image.open(path).convert("RGBA")
    for name, found in zip(names, blobs(sheet)):
        if found is None:
            if name is not None:
                print("warning: empty cell", name, path.name)
            continue
        box, mask = found
        piece = sheet.copy()
        piece.putalpha(ImageChops.multiply(sheet.getchannel("A"), mask))
        piece = piece.crop(box)
        trimmed = alpha_box(piece)
        yield name, piece.crop(trimmed) if trimmed else piece


def square(image, side, margin=1.08):
    edge = round(max(image.size) * margin)
    canvas = Image.new("RGBA", (edge, edge))
    canvas.paste(image, ((edge - image.width) // 2, (edge - image.height) // 2))
    return canvas.resize((side, side), Image.LANCZOS)


def emblems():
    for sheet, names in EMBLEMS.items():
        path = GEN / "emblems" / f"{sheet}.png"
        if not path.exists():
            print("missing", path); continue
        for name, image in cells(path, names):
            # Header emblems draw at about 80 canvas units; keep them at twice the kit density.
            square(image, 224, 1.04).save(KIT / f"emblem-{name}.png", optimize=True)
            print("emblem", name)


def sheets():
    for sheet, names in SHEETS.items():
        path = GEN / "sheets" / f"{sheet}.png"
        if not path.exists():
            print("missing", path); continue
        for name, image in cells(path, names):
            if name is None:
                continue
            square(image, 512).save(ITEMS / f"{name}.png", optimize=True)
            print("item", name)


def scenes():
    for path in sorted((GEN / "scenes").glob("*.png")):
        image = Image.open(path).convert("RGB")
        if image.width > SCENE_WIDTH:
            image = image.resize((SCENE_WIDTH, round(image.height * SCENE_WIDTH / image.width)), Image.LANCZOS)
        image.save(ITEMS / f"scene-{path.stem}.png", optimize=True)
        print("scene", path.stem, image.size)


def kit():
    """paper-clean: the codex parchment without the two page buttons painted on its top-right corner."""
    paper = Image.open(KIT / "paper.png").convert("RGBA")
    # The parchment just below the buttons has the same edge shading; a feathered mask hides the seam.
    box = (536, 14, 668, 88)
    patch = paper.crop((box[0], box[3] + 4, box[2], box[3] + 4 + box[3] - box[1]))
    mask = Image.new("L", patch.size)
    mask.paste(255, (6, 6, patch.width - 6, patch.height - 6))
    paper.paste(patch, box[:2], mask.filter(ImageFilter.GaussianBlur(4)))
    paper.save(KIT / "paper-clean.png", optimize=True)
    print("kit paper-clean")


def main():
    steps = {"boards": boards, "emblems": emblems, "sheets": sheets, "scenes": scenes, "kit": kit}
    for name in sys.argv[1:] or list(steps):
        steps[name]()


if __name__ == "__main__":
    main()
