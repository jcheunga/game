#!/usr/bin/env python3
"""The painted Lantern Caravan and the battle dressing around both bases.

The caravan was a Blender render on a painted field. Codex repaints it in the unit sheet's style (output/units.png),
using the old render as the layout guide, then edits that painting for its open door, its battle damage, every skin,
and the overlays the runtime lays over it (armour plating, snow, mud). The same runner paints the roof weapons, each zone's barricade in front of the stronghold, the damage decals, the stronghold's banner meter and
the victory flag.

  python3 art/royal/caravan.py run [job ...]     Codex: paint every job still missing (resumes, waits out limits)
  python3 art/royal/caravan.py list              jobs and whether each is painted
  python3 art/royal/caravan.py sheet name ...    a painting over a labelled grid, for reading points by eye

Paintings land in art/royal/gen/caravan; caravan_cut.py turns them into assets.
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
GEN = ROOT / "art/royal/gen/caravan"
STYLE = ROOT / "output/units.png"
OLD = ROOT / "assets/structures/battle-v2"

STYLE_LINE = ("Painted by the same artist as the unit sheet attached as a reference: identical hand-painted rendering, "
              "the same proportions, level of detail, materials and lighting (soft warm key light from the upper left, a "
              "cool rim light, rich hand-crafted texture, crisp readable shapes).")
TRANSPARENT = ("The background must be TRANSPARENT (a PNG with an alpha channel): nothing behind the subject, no floor, "
               "no ground, no cast shadow, no backdrop, no text, no border.")
SAME_FRAME = ("Everything else must stay exactly the same: the same wagon, the same position in the frame, the same size, "
              "the same framing, the same painting style and lighting. Keep the background TRANSPARENT (PNG with alpha, "
              "no floor, no shadow). Output the same size as the input.")

WAGON = f"""Image 1 is a plain 3D render of the war wagon of the Lantern Caravan, the player's mobile base in the side-scrolling strategy game "Crownroad: Siege of Ash". Image 2 is the game's official unit sheet; its top half shows the Lantern Caravan's people.
Use your image generation tool to EDIT image 1: repaint the wagon as a beautiful, professional piece of game art. {STYLE_LINE}

Keep the layout of image 1 so the game's attachment points still line up: the same side view facing RIGHT seen slightly from above, the same position and size in the frame, the two big spoked wheels where they are, the troop hold slung low between the wheels with its CLOSED ramp door at the bottom centre, the flat roof deck with its low railing, the lantern cupola at the right end of the roof, the tall flag at the left end, the hanging lanterns at both ends, and the shafts reaching out to the right.

Make it handsome and finely made, the pride of the caravan: a carved oak body with iron strapwork and brass rivets, panels with the caravan's teal-and-cream heraldry and gold lantern emblems (as worn by the troops in image 2), a heater shield or two, iron-tyred wheels with brass hubs, and a grand brass-and-glass lantern in the cupola glowing with warm golden light. Rich but readable: clear large shapes first, fine detail second, no clutter. Road-worn and battle-ready, not new.
The roof deck stays open and mostly empty: the game places its crew and weapons there.
No horses, no people, no text. {TRANSPARENT} Square 1024x1024, the wagon framed exactly as in image 1."""

CLEAR_ROOF = f"""The attached image is a game painting of a war wagon with a transparent background. Use your image generation tool to EDIT it: clear the roof deck. Remove the crates and the barrel standing on the roof, leaving the bare plank deck inside its railing (the game places the crew and weapons there). Keep the flag, the railing posts and the lantern cupola. {SAME_FRAME}"""

DOOR = f"""The attached image is a game painting of a war wagon with a transparent background. Use your image generation tool to EDIT it: open the troop hold's ramp door. The door at the bottom centre, between the wheels, swings down on its hinges into a sturdy plank ramp reaching the ground below and in front of the wagon (toward the viewer). Inside the open doorway the hold is lit by warm golden lantern light. Only the door and the doorway change. {SAME_FRAME}"""

DAMAGED = f"""The attached image is a game painting of a war wagon with a transparent background. Use your image generation tool to EDIT it: show it battered after a hard fight but still standing and working: splintered and cracked planks, a few gouges and scorch marks, one of the smaller shields knocked askew or missing, torn cloth, dents in the iron. Keep it readable and dignified, not wrecked: the wheels, the troop hold and its ramp door at the bottom centre, the roof deck and the lantern cupola stay whole and in place, and the lantern still glows. {SAME_FRAME}"""

ARMOUR = f"""The attached image is a game painting of a war wagon with a transparent background. Use your image generation tool to EDIT it: up-armour it for war. Bolt dark forged-iron plates over the lower hull and the sides of the body, add iron spikes around the wheel hubs and iron studs on the wheel rims, and iron caps on the roof railing posts. Keep its colours and heraldry visible between the plates. The troop hold's ramp door at the bottom centre, the roof deck and the lantern cupola stay unchanged. {SAME_FRAME}"""

SNOW = f"""The attached image is a game painting of a war wagon with a transparent background. Use your image generation tool to EDIT it: it has travelled through a snowy mountain pass. Add soft drifts of snow on the roof deck, the cupola roof, the tops of the shields and ledges, a dusting on the wheels, and a few icicles under the eaves. Do not change anything else. {SAME_FRAME}"""

MUD = f"""The attached image is a game painting of a war wagon with a transparent background. Use your image generation tool to EDIT it: it has travelled through a boggy marsh. Add wet dark mud caked on the wheels and their rims, splattered up the lower hull, the troop hold and its door, fading out halfway up the body. Do not change anything else. {SAME_FRAME}"""

# Skin briefs: what each unlockable caravan looks like. Image 2 of each edit is the old render of that skin.
SKINS = {
    "skin_iron": "Iron Plated: reinforced with forge-hardened dark iron plates and heavy rivets over the oak, grey-steel and teal heraldry, a soot-dark iron lantern cupola.",
    "skin_royal": "Royal Guard: draped in the king's colours, deep royal blue and gold with white trim, gilded carving and crown emblems, gold-fringed banners, a gilded lantern cupola.",
    "skin_bone": "Bone Collector: adorned with trophies taken from the undead host, bleached bone, skulls and horns lashed to weathered pale wood, parchment-coloured cloth, a bone-framed lantern cupola.",
    "skin_flame": "Flame Forged: wreathed in embers from the Challenge Tower's forge, blackened scorched wood with glowing orange seams, copper and red-orange heraldry, a cupola lantern burning with fierce orange flame.",
    "skin_shadow": "Shadow Runner: a midnight-black caravan that strikes fear, black-stained wood, dark steel, deep indigo and silver heraldry, a cold pale-violet lantern glow.",
    "skin_guild": "Guild Banner: flies the warband's banner, emerald green and gold heraldry with heraldic pennants and guild crests, polished wood.",
    "skin_legendary": "Legendary: the ultimate caravan, white-and-gold ornamented wood with violet enamel, gilded filigree and gemstones, a radiant lavender-white lantern glow.",
}


def skin_prompt(brief):
    return f"""Image 1 is a game painting of a war wagon with a transparent background. Image 2 is an older, plain render of one of its unlockable variants. Use your image generation tool to EDIT image 1 into that variant: {brief}
Take from image 2 only the variant's colours, materials and motifs. Keep image 1's painting style, finish and level of detail, and keep its exact layout: the same wagon shape, wheels, troop hold and closed ramp door at the bottom centre, roof deck and railing, lantern cupola, flag, lanterns and shafts, in the same places and at the same size. {TRANSPARENT} Output the same size as the input."""


def mount_prompt(kind, what):
    return f"""Image 1 is a plain 3D render of a {kind} that stands on the roof of the Lantern Caravan's war wagon. Image 2 is the game's official unit sheet.
Use your image generation tool to EDIT image 1: repaint it as finished game art. {STYLE_LINE} {what}
Keep the same side view facing RIGHT, slightly from above, the same position and size in the frame, standing on its round turntable base. No people. {TRANSPARENT} Output the same size as the input."""


MOUNTS = {
    "mount_arrows": mount_prompt("archers' post (a quiver rack and arrow crates on a swivel stand)",
                                 "Oak and iron with teal-and-cream trim and a small gold lantern emblem."),
    "mount_ballista": mount_prompt("ballista", "A heavy oak bolt-thrower with iron fittings, a loaded bolt, teal-and-cream trim."),
    "mount_firepot": mount_prompt("firepot launcher (a small throwing arm with a burning pot)",
                                  "Oak and iron, the clay firepot in its cup burning with a warm orange flame."),
}

BALLISTA_STAND = f"""The attached image is a game painting of a ballista on a turntable with a transparent background, used for 2D cutout animation. Use your image generation tool to EDIT it: remove the whole bow assembly and its bolt (the horizontal stock, the bow arms, the string and the bolt), leaving ONLY the stand: the turntable base and the upright support with the pivot on top. Paint whatever they covered. {SAME_FRAME.replace("the same wagon, ", "")}"""

sys.path.insert(0, str(Path(__file__).parent))
from bases_briefs import ZONE_LOOK  # noqa: E402

ZONES = list(ZONE_LOOK)


def barricade_prompt(zone):
    return f"""Image 1 is a battle backdrop from the side-scrolling strategy game "Crownroad: Siege of Ash" for the zone {ZONE_LOOK[zone]} Image 2 is the enemy stronghold painted into that scene.
Use your image generation tool to paint ONE new image: a sprite sheet of FOUR separate barricade pieces that the undead host (the Rotbound Host: tattered crimson-and-black cloth, bone trophies, iron spikes) has piled across the road in front of that stronghold, built from this zone's own materials and colours. For example: a cluster of sharpened stakes, a heap of rubble or broken material, a toppled cart or crates, a spiked wooden barrier. Each piece is low and wide, about as tall as a soldier's waist to chest, seen from the side and slightly from above like the backdrop, sitting on its own small patch of ground.
Lay the four pieces out in a 2x2 grid, well separated, none touching another or the image edges.
Painted in exactly the style, palette and light of image 1 and image 2, as if by the same hand. No people, no creatures, no text. {TRANSPARENT} 1536x1024 landscape."""


DECALS = f"""Image 1 is a painted enemy stronghold from the game "Crownroad: Siege of Ash". Use your image generation tool to paint ONE new image: a sprite sheet of EIGHT battle-damage decals to lay over its walls, painted in the same style: four crack patterns in stone and timber (dark jagged cracks with a few chipped light edges), two black-brown scorch marks with soft edges, two chipped holes showing dark broken stone. Each about the same size, laid out in a 4x2 grid, well separated, none touching another or the image edges. Seen flat-on. {TRANSPARENT} 1536x768 landscape."""

BANNER = f"""Image 1 is a painted enemy stronghold of the Rotbound Host (an undead army) from the game "Crownroad: Siege of Ash". Use your image generation tool to paint ONE new image: a long, narrow horizontal war banner of the Rotbound Host for the stronghold's health meter: a black iron rod across the top with a small skull finial at each end, hanging from it a horizontal strip of tattered crimson-and-black cloth with a pale skull-under-a-broken-crown emblem at its LEFT end. The cloth is long and even so it can be cut short from the right. Painted in the same style as image 1. {TRANSPARENT} 1536x512, the banner filling the width with a small margin."""

FLAG = f"""Image 1 is the game's official unit sheet; its top half shows the Lantern Caravan (the player's side). Use your image generation tool to paint ONE new image: the Lantern Caravan's victory standard, a tall wooden banner pole with a brass lantern finial, flying a large teal-and-cream swallow-tailed banner with a gold lantern emblem, streaming to the RIGHT in the wind. {STYLE_LINE} {TRANSPARENT} Portrait 1024x1536, the pole upright, its foot at the bottom centre."""


def jobs():
    """name -> (prompt, inputs [edit target first], output). Edits read earlier jobs' outputs, so order matters."""
    gen = lambda name: GEN / f"{name}.png"
    out = {"wagon_raw": (WAGON, [OLD / "war_wagon.png", STYLE], gen("wagon_raw")),
           "wagon": (CLEAR_ROOF, [gen("wagon_raw")], gen("wagon"))}
    for name, prompt in (("wagon_door", DOOR), ("wagon_damaged", DAMAGED), ("wagon_armour", ARMOUR),
                         ("wagon_snow", SNOW), ("wagon_mud", MUD)):
        out[name] = (prompt, [gen("wagon")], gen(name))
    for skin, brief in SKINS.items():
        out[skin] = (skin_prompt(brief), [gen("wagon"), OLD / f"war_wagon_{skin}.png"], gen(skin))
        out[skin + "_door"] = (DOOR, [gen(skin)], gen(skin + "_door"))
        out[skin + "_damaged"] = (DAMAGED, [gen(skin)], gen(skin + "_damaged"))
    for name, prompt in MOUNTS.items():
        out[name] = (prompt, [ROOT / f"assets/structures/{name}.png", STYLE], gen(name))
    out["mount_ballista_stand"] = (BALLISTA_STAND, [gen("mount_ballista")], gen("mount_ballista_stand"))
    for zone in ZONES:
        stage = (ZONES.index(zone) + 1) * 10
        out[f"barricade_{zone}"] = (barricade_prompt(zone), [ROOT / f"art/royal/gen/bases/guides/{zone}.png",
                                                             ROOT / f"art/royal/gen/bases/paint/stage_{stage:03d}.png"],
                                    gen(f"barricade_{zone}"))
    out["decals"] = (DECALS, [ROOT / "art/royal/gen/bases/paint/stage_050.png"], gen("decals"))
    out["banner"] = (BANNER, [ROOT / "art/royal/gen/bases/paint/stage_030.png"], gen("banner"))
    out["flag"] = (FLAG, [STYLE], gen("flag"))
    return out


# Edits keep their target's framing; everything else is a fresh painting that only looks at its references.
FRESH = {"decals", "banner", "flag"} | {f"barricade_{zone}" for zone in ZONES}

WRAPPER = """You are producing one image asset for the game project in this workspace.
Use your built-in image_gen tool (imagegen skill, built-in mode; never the CLI fallback) exactly once{hint}.
Then copy the generated PNG (from $CODEX_HOME/generated_images/...) to:
  {out}
creating parent folders if needed, and reply with exactly one line: DONE <width>x<height>.
Do not modify any other file and do not run anything else.

{prompt}
"""


def run(names, workers=3):
    """Paints every named job (default: all) still missing, edits after the paintings they edit."""
    import concurrent.futures
    import datetime as dt
    import re
    import subprocess
    import threading
    import time
    sys.path.insert(0, str(Path(__file__).parent))
    from codexgen import MODEL
    logs = ROOT / "art/royal/gen/logs"
    logs.mkdir(parents=True, exist_ok=True)
    lock, wake = threading.Lock(), [None]
    table = jobs()

    def wait_for_limit(text):
        found = re.search(r'resets_at\\?"?:\s*(\d{9,})', text)
        now = dt.datetime.now()
        when = dt.datetime.fromtimestamp(int(found.group(1))) + dt.timedelta(minutes=1) if found else now + dt.timedelta(minutes=20)
        with lock:
            if wake[0] is None or wake[0] < now:
                wake[0] = when
                print(f"{now:%H:%M} usage limit, waiting until {when:%H:%M}", flush=True)
            when = wake[0]
        while dt.datetime.now() < when:
            time.sleep(min(60.0, max(1.0, (when - dt.datetime.now()).total_seconds())))

    def paint(name):
        prompt, inputs, out = table[name]
        if out.exists():
            return True
        missing = [str(i) for i in inputs if not Path(i).exists()]
        if missing:
            print(f"{name}: waiting on {missing}", flush=True)
            return False
        out.parent.mkdir(parents=True, exist_ok=True)
        hint = ("; the attached images are style references only" if name in FRESH
                else " in edit mode: the FIRST attached image is the edit target")
        for _ in range(4):
            command = ["codex", "exec", "-m", MODEL, "-c", 'model_reasoning_effort="low"', "--skip-git-repo-check",
                       "-C", str(ROOT), "--sandbox", "workspace-write"]
            for image in inputs:
                command += ["-i", str(image)]
            started = time.time()
            result = subprocess.run(command + ["-"], input=WRAPPER.format(hint=hint, out=out, prompt=prompt.strip()),
                                    text=True, capture_output=True, cwd=ROOT)
            (logs / f"caravan-{name}.log").write_text(result.stdout[-6000:] + "\n--- stderr ---\n" + result.stderr[-3000:])
            if out.exists():
                print(f"{name}: ok in {time.time() - started:.0f}s", flush=True)
                return True
            text = result.stdout + result.stderr
            if "usage limit" in text.lower() or "usage_limit_reached" in text:
                wait_for_limit(text)
        print(f"{name}: failed", flush=True)
        return False

    pending = [n for n in (names or list(table)) if not table[n][2].exists()]
    print(f"{len(pending)} paintings to make", flush=True)
    # In rounds: each round paints every job whose inputs exist, so edits follow the paintings they edit.
    while pending:
        ready = [n for n in pending if all(Path(i).exists() for i in table[n][1])]
        if not ready:
            print("stuck on missing inputs:", pending, flush=True)
            return
        with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as pool:
            list(pool.map(paint, ready))
        pending = [n for n in pending if n not in ready]


def sheet(names):
    """The painting over a checker with a labelled grid every 32 px (every 128 bold), for reading points by eye."""
    from PIL import Image, ImageDraw, ImageFont
    font = ImageFont.truetype(str(next((ROOT / "assets/fonts").glob("*.ttf"))), 16)
    out_dir = GEN / "sheets"
    out_dir.mkdir(parents=True, exist_ok=True)
    for name in names:
        image = Image.open(GEN / f"{name}.png").convert("RGBA")
        w, h = image.size
        board = Image.new("RGBA", image.size, (90, 96, 90, 255))
        board.alpha_composite(image)
        draw = ImageDraw.Draw(board)
        for x in range(0, w, 32):
            draw.line([(x, 0), (x, h)], fill=(255, 255, 0, 160) if x % 128 == 0 else (0, 255, 255, 60), width=1)
            if x % 128 == 0:
                draw.text((x + 2, 2), str(x), fill=(255, 255, 0, 255), font=font, stroke_width=2, stroke_fill=(0, 0, 0))
        for y in range(0, h, 32):
            draw.line([(0, y), (w, y)], fill=(255, 255, 0, 160) if y % 128 == 0 else (0, 255, 255, 60), width=1)
            if y % 128 == 0:
                draw.text((2, y + 2), str(y), fill=(255, 255, 0, 255), font=font, stroke_width=2, stroke_fill=(0, 0, 0))
        board.convert("RGB").save(out_dir / f"{name}.png")
        print("sheet ->", (out_dir / f"{name}.png").relative_to(ROOT))


def main():
    command, args = (sys.argv[1], sys.argv[2:]) if len(sys.argv) > 1 else ("list", [])
    if command == "run":
        run(args)
    elif command == "list":
        for name, (_, _, out) in jobs().items():
            print(("done " if out.exists() else "     ") + name)
    elif command == "sheet":
        sheet(args)
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()
