"""Keep the large painted textures on import settings that suit a phone download.

Godot imports a new PNG losslessly, which ships every painting at full PNG size. These rules move the
big painted art to settings that look the same on a phone (docs/BUILD_SIZE.md has the measurements):

- Zone map paintings: GPU-compressed (VRAM) at normal quality, ETC2 on phones, with mipmaps for the
  zoomed-out map. Half the size of high-quality ASTC, in the download and in memory.
- Painted pictures (battle backdrops, item pictures, plates, mission art, portraits, unit icons, structures):
  lossy WebP at quality 0.9. Alpha stays lossless, so hit tests and trimming on alpha are unchanged.

Run after adding art, then reimport (godot --headless --path . --import):
  python3 scripts/tools/mobile_textures.py          # rewrite .import files that differ
  python3 scripts/tools/mobile_textures.py --check  # list them and exit 1 instead
"""
import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

PAINTED = {"compress/mode": "1", "compress/lossy_quality": "0.9"}
MAP_TILE = {"compress/mode": "2", "compress/high_quality": "false", "mipmaps/generate": "true"}

RULES = [
    ("assets/world/royal/maps/*.jpg", MAP_TILE),
    ("assets/world/royal/*.png", PAINTED),
    ("assets/ui/royal/items/*.png", PAINTED),
    ("assets/ui/royal/plates/*.png", PAINTED),
    ("assets/ui/royal/missions/*.png", PAINTED),
    ("assets/ui/portraits/codex/*.png", PAINTED),
    ("assets/ui/icons/units/*.png", PAINTED),
    ("assets/structures/*.png", PAINTED),
    ("assets/structures/battle-v2/*.png", PAINTED),
]


def apply(text, settings):
    """The .import text with these [params] values set."""
    head, sep, params = text.partition("[params]")
    for key, value in settings.items():
        line = f"{key}={value}"
        pattern = re.compile(rf"^{re.escape(key)}=.*$", re.M)
        params = pattern.sub(line, params) if pattern.search(params) else params.rstrip("\n") + f"\n{line}\n"
    return head + sep + params


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true", help="report files that differ and exit 1; change nothing")
    args = parser.parse_args()
    differ = []
    for pattern, settings in RULES:
        for source in sorted(ROOT.glob(pattern)):
            sidecar = source.with_name(source.name + ".import")
            if not sidecar.exists() or 'importer="texture"' not in (text := sidecar.read_text()):
                continue
            updated = apply(text, settings)
            if updated != text:
                differ.append(sidecar.relative_to(ROOT))
                if not args.check:
                    sidecar.write_text(updated)
    verb = "need" if args.check else "updated"
    print(f"{len(differ)} texture import file(s) {verb} mobile settings" + ("" if not differ else ":"))
    for path in differ[:40]:
        print(f"  {path}")
    if len(differ) > 40:
        print(f"  ... and {len(differ) - 40} more")
    if differ and not args.check:
        print("Now reimport: godot --headless --path . --import")
    return 1 if args.check and differ else 0


if __name__ == "__main__":
    sys.exit(main())
