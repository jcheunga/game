#!/usr/bin/env python3
"""Install royal-UI plates into assets/ui/royal/plates/.

A plate is a whole concept screen (or HUD sheet) that screens draw at canvas scale and overlay with
live text and content. The clean, text-free plates come from art/royal/gen/plates/ (see codexgen.py);
until one exists the original concept is installed so layouts can be built and checked against it.

  python3 art/royal/plates.py [name ...]
"""
import json
import sys
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
UI = ROOT / "output/crownroad-ui-ideas-2026-10-06"
MENU = ROOT / "output/crownroad-menu-concepts-2026-10-06"
GEN = ROOT / "art/royal/gen/plates"
OUT = ROOT / "assets/ui/royal/plates"
SIZE = (1672, 941)

CONCEPTS = {
    "warband": UI / "07-warband-armory.png",
    "wagon": UI / "08-war-wagon-workshop.png",
    "preparation": UI / "09-battle-preparation.png",
    "victory": UI / "10-victory-rewards.png",
    "hud-battle": UI / "02-clean-steel-battle.png",
    "hud-home": UI / "06-cartographers-atlas-home.png",
    "hub": MENU / "01-caravan-menu-hub.png",
    "spells": MENU / "02-spell-library.png",
    "relics": MENU / "03-relics-armory.png",
    "achievements": MENU / "04-achievements.png",
    "codex": MENU / "05-crownroad-codex.png",
    "settings": MENU / "06-settings.png",
    "endless": MENU / "07-endless-survival.png",
    "multiplayer": MENU / "08-multiplayer-challenges.png",
    "forge": MENU / "09-relic-forge.png",
    "season": MENU / "10-season-rewards.png",
}
# Untouched concepts are kept as "<name>-concept" for cutting artwork that the clean plates remove
# (illustrations, icons, selected states).
KEEP_CONCEPT = False


def install(name):
    clean = GEN / f"{name}.png"
    source = clean if clean.exists() else CONCEPTS[name]
    image = Image.open(source).convert("RGBA")
    if image.size != SIZE:
        image = image.resize(SIZE, Image.LANCZOS)
    OUT.mkdir(parents=True, exist_ok=True)
    image.save(OUT / f"{name}.png", optimize=True)
    if KEEP_CONCEPT:
        concept = Image.open(CONCEPTS[name]).convert("RGB")
        if concept.size != SIZE:
            concept = concept.resize(SIZE, Image.LANCZOS)
        concept.save(OUT / f"{name}-concept.png", optimize=True)
    return {"name": name, "source": str(source.relative_to(ROOT)), "clean": clean.exists()}


def install_specs():
    """Specs are runtime layout data: screens place their live content by element id."""
    target = ROOT / "assets/ui/royal/specs"
    target.mkdir(parents=True, exist_ok=True)
    for spec in (ROOT / "art/royal/specs").glob("*.json"):
        (target / spec.name).write_text(spec.read_text())


def main():
    install_specs()
    names = sys.argv[1:] or list(CONCEPTS)
    manifest_path = OUT / "plates.json"
    manifest = json.loads(manifest_path.read_text()) if manifest_path.exists() else {}
    for name in names:
        manifest[name] = install(name)
        print(manifest[name])
    manifest_path.write_text(json.dumps(manifest, indent=1, sort_keys=True))


if __name__ == "__main__":
    main()
