#!/usr/bin/env python3
"""Record exactly what a release is built from, so launch day has a known-good version to roll back to.

    python3 scripts/release/freeze.py builds/android/crownroad-1.aab builds/ios/crownroad.ipa
    python3 scripts/release/freeze.py --dry-run                      print the record, write nothing
    python3 scripts/release/freeze.py --server-image sha256:…        add the deployed API image afterwards

Run it on the release commit after preflight passes and the store builds are exported. It refuses uncommitted
changes to tracked files, then writes docs/releases/<version name>.json: the commit, the Godot editor, export
template, Android build template and .NET SDK versions, both presets' version numbers, the server schema version,
the shop catalog's product IDs and hash, and the size and SHA-256 of each build named. Commit the file and tag the
commit (it prints both commands). Once the server is deployed, `docker image inspect --format '{{.Id}}'
server-crownroad-server` on the VM gives the image; --server-image alone adds it to the record. See docs/LAUNCH_DAY.md.
"""
import argparse
import hashlib
import json
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from preflight import ROOT, preset, sections, text  # noqa: E402

TEMPLATES = Path.home() / "Library/Application Support/Godot/export_templates"


def run(*command):
    try:
        return subprocess.run(command, cwd=ROOT, capture_output=True, text=True, check=True).stdout.rstrip()
    except (OSError, subprocess.CalledProcessError):
        return ""


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as stream:
        for block in iter(lambda: stream.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("builds", nargs="*", type=Path, help="the signed .aab and .ipa that go to the stores")
    parser.add_argument("--server-image", default="", help="the deployed API image ID (sha256:…)")
    parser.add_argument("--dry-run", action="store_true", help="print the record without writing it")
    args = parser.parse_args()

    presets = sections(ROOT / "export_presets.cfg")
    _, android = preset(presets, "Android")
    _, ios = preset(presets, "iOS")
    version = text(android.get("version/name")) or "unversioned"
    out = ROOT / "docs/releases" / f"{version}.json"
    if args.server_image and not args.builds and out.exists() and not args.dry_run:
        record = json.loads(out.read_text(encoding="utf-8"))
        record["server"]["image"] = args.server_image
        out.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
        print(f"Added the server image to {out.relative_to(ROOT)}. Commit it.")
        return 0

    changed = run("git", "status", "--porcelain", "--untracked-files=no")
    if changed and not args.dry_run:
        print("Commit or discard these changes first; a release is frozen from a clean commit:\n" + changed)
        return 1
    missing = [str(path) for path in args.builds if not path.is_file()]
    if missing:
        print("No such build: " + ", ".join(missing))
        return 1

    editor = run("godot", "--version")
    editor_templates = re.sub(r"\.official\..*$", "", editor)
    catalog_path = ROOT / "data/shop_products.json"
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    products = catalog if isinstance(catalog, list) else catalog.get("products", [])
    schema = max(map(int, re.findall(r"SetSchemaVersion\(conn, (\d+)\)", (ROOT / "server/Database.cs").read_text(encoding="utf-8"))))
    build_template = ROOT / "android/.build_version"

    record = {
        "version": version,
        "frozenAt": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "commit": run("git", "rev-parse", "HEAD"),
        "pushed": bool(run("git", "branch", "-r", "--contains", "HEAD")),
        "uncommittedChanges": bool(changed),
        "tools": {
            "godot": editor,
            "exportTemplatesInstalled": (TEMPLATES / editor_templates).is_dir() if editor_templates else False,
            "androidBuildTemplate": build_template.read_text(encoding="utf-8").strip() if build_template.exists() else "",
            "dotnetSdk": run("dotnet", "--version"),
        },
        "android": {"versionName": version, "versionCode": text(android.get("version/code")),
                    "targetSdk": text(android.get("gradle_build/target_sdk"))},
        "ios": {"shortVersion": text(ios.get("application/short_version")), "build": text(ios.get("application/version")),
                "minimumIos": text(ios.get("application/min_ios_version"))},
        "server": {"schemaVersion": schema, "image": args.server_image},
        "shop": {"catalogSha256": sha256(catalog_path),
                 "products": [{"id": p.get("id"), "apple": p.get("appleProductId"), "google": p.get("googleProductId")} for p in products]},
        "builds": [{"file": path.name, "bytes": path.stat().st_size, "sha256": sha256(path)} for path in args.builds],
    }
    rendered = json.dumps(record, indent=2) + "\n"
    if args.dry_run:
        print(rendered, end="")
        return 0

    if out.exists():
        print(f"{out.relative_to(ROOT)} already exists. A frozen release doesn't change: raise the version for a new one.")
        return 1
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(rendered, encoding="utf-8")
    print(rendered, end="")
    if not record["pushed"]:
        print("Warning: this commit isn't on any remote branch yet. Push it so the release can be rebuilt.")
    if not record["tools"]["exportTemplatesInstalled"]:
        print(f"Warning: no export templates for {editor or 'the editor'} are installed.")
    print(f"\nWrote {out.relative_to(ROOT)}. Then:\n"
          f"  git add {out.relative_to(ROOT)} && git commit -m 'Freeze {version}'\n"
          f"  git tag -a v{version} {record['commit'][:12]} -m 'Crownroad {version}' && git push origin v{version}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
