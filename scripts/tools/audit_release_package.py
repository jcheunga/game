"""Audit the resource list emitted by a completed Godot release-pack export."""
import argparse
import json
import re
import zipfile
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("export_log", type=Path)
parser.add_argument("--package", type=Path, required=True, help="ZIP resource package produced by Godot --export-pack")
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
log = re.sub(r"\x1b\[[0-9;]*m", "", args.export_log.read_text())
paths = re.findall(r"Storing File: (res://[^\r\n]+)", log)
assert paths and ("[ DONE ] savepack" in log or "[ DONE ] savezip" in log), "No completed resource pack was found."
excluded = ("server/", "docs/", "artifacts/", "builds/", "output/", "art/",
            "scripts/tests/", "scenes/tests/", "scripts/smoke/", "scripts/analysis/", "scripts/tools/")
leaks = [p for p in paths if p.removeprefix("res://").startswith(excluded)]
assert not leaks, f"Internal resources in release pack: {leaks}"
assemblies = [p for p in paths if p.endswith("/Game.dll")]
assert len(assemblies) == 1, "Expected one published game assembly."
with zipfile.ZipFile(args.package) as package:
    assembly = package.read(assemblies[0].removeprefix("res://"))
    archive_leaks = [p for p in package.namelist() if p.startswith(excluded)]
    assert not archive_leaks, f"Internal resources in archive: {archive_leaks}"
for test_class in (b"UiReviewSmoke", b"CombatReviewSmoke", b"ContentRevampSmoke", b"LiveUiReview"):
    assert test_class not in assembly, f"Test class included in release assembly: {test_class!r}"
report = {"resourceCount": len(paths), "internalResourceLeaks": leaks,
          "testClassesExcluded": True, "assembly": assemblies[0],
          "packageBytes": args.package.stat().st_size,
          "exporterMessages": [line for line in log.splitlines() if line.startswith("ERROR:")],
          "scope": "Resource and assembly audit; does not verify signing, store delivery or physical devices."}
args.output.write_text(json.dumps(report, indent=2) + "\n")
print(f"Release resource audit: {len(paths)} files, no internal resources or test classes.")
