#!/usr/bin/env python3
"""Side-by-side and blended comparison of a capture with its concept reference.

  python3 art/royal/compare.py warband [more names...]

Reads artifacts/royal-ui/capture/<name>.png and the matching art/royal/ref image; writes
artifacts/royal-ui/compare/<name>.png (capture | reference on top, 50% blend below).
"""
import sys
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
REFS = {
    "home": "ui-06-cartographers-atlas-home", "warband": "ui-07-warband-armory", "wagon": "ui-08-war-wagon-workshop",
    "preparation": "ui-09-battle-preparation", "victory": "ui-10-victory-rewards", "battle": "ui-02-clean-steel-battle",
    "hub": "menu-01-caravan-menu-hub", "spells": "menu-02-spell-library", "relics": "menu-03-relics-armory",
    "achievements": "menu-04-achievements", "codex": "menu-05-crownroad-codex", "settings": "menu-06-settings",
    "endless": "menu-07-endless-survival", "multiplayer": "menu-08-multiplayer-challenges", "forge": "menu-09-relic-forge",
    "season": "menu-10-season-rewards",
}


TITLES = {
    "home": "Home atlas", "hub": "Caravan hub", "warband": "Warband", "spells": "Spells", "relics": "Relics",
    "wagon": "War wagon", "achievements": "Achievements", "codex": "Codex", "settings": "Settings",
    "endless": "Endless survival", "multiplayer": "Multiplayer challenges", "forge": "Relic forge",
    "season": "Season rewards", "preparation": "Prepare for battle", "battle": "Battle", "victory": "Victory",
}


def page():
    """artifacts/royal-ui/index.html: every captured screen beside its concept."""
    rows = []
    for name, title in TITLES.items():
        capture = ROOT / f"artifacts/royal-ui/capture/{name}.png"
        if not capture.exists():
            continue
        ref = f"../../art/royal/ref/{REFS[name]}.png"
        rows.append(f'<section><h2>{title}</h2><div class="pair"><figure><figcaption>In game</figcaption><a href="capture/{name}.png">'
                    f'<img loading="lazy" src="capture/{name}.png" alt="{title} in game"></a></figure><figure><figcaption>Concept</figcaption>'
                    f'<a href="{ref}"><img loading="lazy" src="{ref}" alt="{title} concept"></a></figure></div></section>')
    zones = sorted((ROOT / "artifacts/royal-ui/capture").glob("battle-*.png"))
    if zones:
        rows.append('<section><h2>Battlefields</h2><div class="pair">' + "".join(
            f'<figure><figcaption>{z.stem[7:].title()}</figcaption><a href="capture/{z.name}"><img loading="lazy" src="capture/{z.name}" alt="{z.stem}"></a></figure>'
            for z in zones) + "</div></section>")
    style = ("*{box-sizing:border-box}body{margin:0;background:#0b1720;color:#f0e3c7;font-family:Georgia,serif}main{max-width:1600px;margin:auto;padding:32px}"
             "h1{font-size:36px;color:#edca8d;margin:0 0 10px}section{margin:36px 0;padding:20px;border:1px solid #856a45;border-radius:8px;background:#15242c}"
             "h2{font-size:24px;margin:0 0 20px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:18px}figure{margin:0;min-width:0}"
             "figcaption{margin:0 0 10px;color:#ddbd80}img{display:block;width:100%;border:1px solid #5a4d3e;border-radius:5px}"
             "@media(max-width:900px){main{padding:16px}.pair{grid-template-columns:1fr}}")
    html = (f'<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">'
            f'<title>Crownroad screen review</title><style>{style}</style><main><h1>Crownroad screen review</h1>{"".join(rows)}</main></html>')
    (ROOT / "artifacts/royal-ui/index.html").write_text(html)
    print(ROOT / "artifacts/royal-ui/index.html")


def main():
    if sys.argv[1:] == ["--page"]:
        page()
        return
    out = ROOT / "artifacts/royal-ui/compare"
    out.mkdir(parents=True, exist_ok=True)
    for name in sys.argv[1:]:
        capture = Image.open(ROOT / f"artifacts/royal-ui/capture/{name}.png").convert("RGB").resize((1280, 720), Image.LANCZOS)
        ref = Image.open(ROOT / f"art/royal/ref/{REFS[name]}.png").convert("RGB")
        sheet = Image.new("RGB", (2560, 1440))
        sheet.paste(capture, (0, 0)); sheet.paste(ref, (1280, 0))
        sheet.paste(Image.blend(capture, ref, .5), (0, 720))
        sheet.paste(capture.crop((0, 0, 1280, 720)), (1280, 720))
        sheet.save(out / f"{name}.png")
        print(out / f"{name}.png")


if __name__ == "__main__":
    main()
