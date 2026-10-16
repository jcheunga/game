#!/bin/bash
# Selects Crownroad.storekit as the StoreKit configuration of an exported iOS project, so running it
# from Xcode buys the game's products from Xcode instead of the App Store (see README.md).
#
#   native/StoreKit/use_storekit_config.sh builds/ios/crownroad.xcodeproj
#
# Copies the configuration next to the project and adds it to the Run action of every shared
# scheme. Run it again after each export, which rewrites the scheme.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
project="${1:?usage: use_storekit_config.sh <exported .xcodeproj>}"
project="$(cd "$project" && pwd)"
cp "$here/Crownroad.storekit" "$(dirname "$project")/Crownroad.storekit"

shopt -s nullglob
schemes=("$project"/xcshareddata/xcschemes/*.xcscheme)
[ ${#schemes[@]} -gt 0 ] || { echo "No shared scheme in $project" >&2; exit 1; }
for scheme in "${schemes[@]}"; do
	# The reference is relative to the folder that holds the .xcodeproj.
	python3 - "$scheme" <<'EOF'
import re, sys
path = sys.argv[1]
text = open(path, encoding="utf-8").read()
text = re.sub(r"\s*<StoreKitConfigurationFileReference[^>]*>\s*</StoreKitConfigurationFileReference>", "", text)
reference = ('      <StoreKitConfigurationFileReference\n'
             '         identifier = "../Crownroad.storekit">\n'
             '      </StoreKitConfigurationFileReference>\n')
text, count = re.subn(r"(\n)(\s*</LaunchAction>)", lambda m: m.group(1) + reference + m.group(2), text, count=1)
if count != 1:
    sys.exit(f"No LaunchAction in {path}")
open(path, "w", encoding="utf-8").write(text)
EOF
	echo "StoreKit configuration selected in $(basename "$scheme")"
done
