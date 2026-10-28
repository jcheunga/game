#!/usr/bin/env python3
"""Check that the repository is ready to export signed store builds.

    python3 scripts/release/preflight.py            both platforms
    python3 scripts/release/preflight.py android    Google Play only
    python3 scripts/release/preflight.py ios        App Store only

Each line is PASS, or FAIL with what to set and where. Exits 1 if anything
fails. It reads project.godot, export_presets.cfg, the shop catalog, the
website's launch values and Apple's sign-in art; it never builds anything.
android_release.sh and ios_release.sh run it first. The iOS team ID can also
come from the APP_STORE_TEAM_ID environment variable, which ios_release.sh
uses so it never has to be committed.
"""
import json
import os
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
results = []


def check(platform, ok, label, fix=""):
    results.append((platform, bool(ok), label, fix))


def sections(path):
    """Godot's .cfg files as {section: {key: raw value}}."""
    out, current = {}, None
    for line in path.read_text(encoding="utf-8").splitlines():
        header = re.match(r"^\[(.+)\]$", line)
        if header:
            current = out.setdefault(header.group(1), {})
        elif current is not None and "=" in line and not line.startswith(";"):
            key, value = line.split("=", 1)
            current[key] = value
    return out


def text(value):
    return value.strip().strip('"') if value is not None else ""


def preset(presets, name):
    for section, values in presets.items():
        if re.fullmatch(r"preset\.\d+", section) and text(values.get("name")) == name:
            return values, presets.get(section + ".options", {})
    return {}, {}


def main():
    wanted = {arg.lower() for arg in sys.argv[1:]} or {"android", "ios"}
    project = sections(ROOT / "project.godot")
    presets = sections(ROOT / "export_presets.cfg")

    api = text(project.get("crownroad", {}).get("network/api_base_url"))
    check("both", api.startswith("https://") and len(api) > len("https://"), f"Live API address is set ({api or 'blank'})",
          "Set crownroad/network/api_base_url in project.godot to the deployed https:// origin.")

    catalog = json.loads((ROOT / "data/shop_products.json").read_text(encoding="utf-8"))
    products = catalog if isinstance(catalog, list) else catalog.get("products", [])
    check("both", len(products) == 10 and all(p.get("appleProductId") and p.get("googleProductId") for p in products),
          f"{len(products)} shop products, each with App Store and Google Play IDs",
          "Every product in data/shop_products.json needs appleProductId and googleProductId.")

    site = json.loads((ROOT / "site/site.json").read_text(encoding="utf-8"))
    missing = [key for key in ("domain", "legal_name", "support_email", "governing_law", "effective_date") if not site.get(key)]
    links = (ROOT / "scripts/core/SiteLinks.cs").read_text(encoding="utf-8")
    base = re.search(r'Base = "https://([^"/]+)"', links)
    check("both", base and base.group(1) == site.get("domain"), f"The game's website links use {site.get('domain')}",
          "Make SiteLinks.Base in scripts/core/SiteLinks.cs match the domain in site/site.json.")
    check("both", not missing, "Website launch values are filled in" + (f" (missing {', '.join(missing)})" if missing else ""),
          "Fill site/site.json, then run python3 scripts/tools/build_site.py --strict.")

    if "android" in wanted:
        header, options = preset(presets, "Android")
        check("android", options, "Android export preset exists", "Add an Android preset in the editor.")
        project_id = text(project.get("crownroad", {}).get("play_games/project_id"))
        check("android", project_id.isdigit(), f"Play Games project ID is set ({project_id or 'blank'})",
              "Copy the numeric project ID from Play Console → Play Games Services into crownroad/play_games/project_id.")
        check("android", text(options.get("gradle_build/export_format")) == "1", "Exports an Android App Bundle (.aab)",
              "Set Gradle Build → Export Format to Android App Bundle.")
        check("android", text(options.get("gradle_build/target_sdk")) == "36", "Targets Android API 36",
              "Set Gradle Build → Target SDK to 36, or newer if Play now requires it.")
        code = text(options.get("version/code"))
        check("android", code.isdigit() and int(code) >= 1, f"Version code {code}, name {text(options.get('version/name'))}",
              "Raise version/code for every upload; Play refuses a code it has seen.")
        check("android", text(options.get("permissions/internet")) == "true", "Internet permission is on",
              "Turn on Permissions → Internet.")

    if "ios" in wanted:
        header, options = preset(presets, "iOS")
        check("ios", options, "iOS export preset exists", "Add an iOS preset in the editor.")
        team = os.environ.get("APP_STORE_TEAM_ID", "") or text(options.get("application/app_store_team_id"))
        check("ios", re.fullmatch(r"[A-Z0-9]{10}", team or ""), f"App Store team ID is set ({team or 'blank'})",
              "Set APP_STORE_TEAM_ID to the 10-character team ID from developer.apple.com → Membership.")
        check("ios", text(options.get("application/bundle_identifier")) == "com.crownroad.game", "Bundle ID is com.crownroad.game")
        check("ios", text(options.get("entitlements/game_center")) == "true" and text(options.get("plugins/GameCenter")) == "true",
              "Game Center entitlement and plugin are on", "The editor has turned these off before; set them in the iOS preset.")
        check("ios", text(options.get("plugins/StoreKit")) == "true", "StoreKit plugin is on")
        check("ios", text(options.get("application/min_ios_version")) == "15.0", "Minimum iOS 15.0",
              "Set Application → Min iOS Version to 15.0.")
        check("ios", "ITSAppUsesNonExemptEncryption" in text(options.get("application/additional_plist_content")),
              "Info.plist answers the encryption question (HTTPS only)")
        collected = [key.split("/")[2] for key, value in options.items()
                     if key.startswith("privacy/collected_data/") and key.endswith("/collected") and text(value) == "true"]
        check("ios", set(collected) >= {"email_address", "user_id", "purchase_history", "product_interaction", "gameplay_content", "crash_data"},
              f"Privacy manifest declares {len(collected)} collected data types",
              "Match the iOS preset's Privacy options to docs/STORE_PRIVACY_ANSWERS.md.")
        android_header, _ = preset(presets, "Android")
        check("ios", text(header.get("exclude_filter")) == text(android_header.get("exclude_filter")),
              "iOS leaves out the same unused files as Android", "Copy the Android preset's exclude filter to iOS.")
        art = ROOT / "assets/ui/signin"
        # The iPhone shows no Sign in with Apple button (AccountDialog.BrowserSignIn), so only the Game Center icon is needed.
        check("ios", (art / "game-center.png").exists(), "Apple's Game Center icon is in",
              "Download it from Apple Design Resources and run art/signin/build.py (see art/signin/README.md).")

    failed = 0
    for platform, ok, label, fix in results:
        tag = "PASS" if ok else "FAIL"
        print(f"{tag}  [{platform}] {label}")
        if not ok:
            failed += 1
            if fix:
                print(f"      → {fix}")
    print(f"\n{len(results) - failed} of {len(results)} release checks pass.")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
