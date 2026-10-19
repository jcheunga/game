"""Report what takes the space in an exported Android App Bundle, against Google Play's size limits.

  python3 scripts/tools/bundle_size.py builds/android/crownroad.aab [--top 25]

Sizes are the bundle's compressed (zipped) sizes, close to what Play counts as download size. For Play's
own per-device estimate, use bundletool's get-size total (docs/BUILD_SIZE.md). Imported textures under
.godot/imported are credited to their source files through the .import sidecars.
"""
import argparse
import re
import zipfile
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MB = 1_000_000
# https://support.google.com/googleplay/android-developer/answer/9859372 (checked 2026-10-18), compressed sizes.
LIMITS = {"base": 500 * MB, "asset pack": 1500 * MB, "install-time total": 4000 * MB}
# Play asks before downloading an app over mobile data above this size.
LARGE_APP = 200 * MB


def imported_sources():
    """Map each .godot/imported file to the source file that produced it."""
    sources = {}
    for sidecar in ROOT.rglob("*.import"):
        if any(part in (".godot", "android", ".git") for part in sidecar.relative_to(ROOT).parts):
            continue
        source = str(sidecar.relative_to(ROOT))[: -len(".import")]
        for dest in re.findall(r"res://(\.godot/imported/[^\"]+)", sidecar.read_text(errors="ignore")):
            sources[dest] = source
    return sources


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("bundle", type=Path)
    parser.add_argument("--top", type=int, default=25, help="how many asset folders to list")
    parser.add_argument("--depth", type=int, default=3, help="folder depth to group assets by")
    args = parser.parse_args()
    sources = imported_sources()
    modules, folders, kinds = Counter(), Counter(), Counter()
    with zipfile.ZipFile(args.bundle) as bundle:
        for info in bundle.infolist():
            module, _, path = info.filename.partition("/")
            modules[module] += info.compress_size
            if not path.startswith("assets/") or module == "base":
                continue
            rel = path[len("assets/"):]
            source = sources.get(rel, rel)
            parts = source.split("/")
            folders["/".join(parts[: min(args.depth, len(parts) - 1)]) or source] += info.compress_size
            kinds[Path(source).suffix.lower() or "(none)"] += info.compress_size
    total = sum(modules.values())
    print(f"{args.bundle.name}: {args.bundle.stat().st_size / MB:.1f} MB on disk, {total / MB:.1f} MB compressed entries")
    for module, size in modules.most_common():
        if size >= MB / 10:
            print(f"  {size / MB:8.1f} MB  {module}")
    print(f"\nLargest asset folders (top {args.top}):")
    for folder, size in folders.most_common(args.top):
        print(f"  {size / MB:8.1f} MB  {folder}")
    print("\nBy source type:")
    for kind, size in kinds.most_common(8):
        print(f"  {size / MB:8.1f} MB  {kind}")
    packs = {m: s for m, s in modules.items() if m.startswith("assetPack")}
    checks = [("base", modules.get("base", 0), LIMITS["base"])]
    checks += [(f"asset pack {m}", s, LIMITS["asset pack"]) for m, s in packs.items()]
    checks += [("install-time total", total, LIMITS["install-time total"])]
    print("\nGoogle Play limits (compressed):")
    for name, size, limit in checks:
        print(f"  {'ok  ' if size <= limit else 'OVER'} {name}: {size / MB:.1f} of {limit / MB:.0f} MB ({100 * size / limit:.0f}%)")
    note = "over" if total > LARGE_APP else "under"
    print(f"  Total download {total / MB:.1f} MB is {note} the {LARGE_APP / MB:.0f} MB mark where Play asks before using mobile data.")


if __name__ == "__main__":
    main()
