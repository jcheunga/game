#!/bin/bash
# Exports the signed Android App Bundle to upload to Google Play, and checks it.
#
#   GODOT_ANDROID_KEYSTORE_RELEASE_PATH=~/keys/crownroad-upload.jks \
#   GODOT_ANDROID_KEYSTORE_RELEASE_USER=upload \
#   GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD=... \
#   scripts/release/android_release.sh
#
# 1. Runs the release preflight for Android and stops if anything fails.
# 2. Exports builds/android/crownroad-<version code>.aab, signed with your upload key. Play App
#    Signing re-signs it for players; the upload key only proves the upload is yours.
# 3. Lists the bundle's size against Play's limits (scripts/tools/bundle_size.py) and checks every
#    native library is 16 KB page-aligned, which Android 15+ and Play require.
# 4. Prints the bundle's SHA-256 to record with the release.
#
# Make the upload key once, keep it out of the repository and back it up; losing it means asking
# Google to reset it:
#   keytool -genkeypair -v -keystore ~/keys/crownroad-upload.jks -alias upload \
#     -keyalg RSA -keysize 2048 -validity 10000
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$root"
export PATH="/usr/local/share/dotnet:$PATH"

for var in GODOT_ANDROID_KEYSTORE_RELEASE_PATH GODOT_ANDROID_KEYSTORE_RELEASE_USER GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD; do
	[ -n "${!var:-}" ] || { echo "Set $var to your upload key (see the top of this script)." >&2; exit 1; }
done
[ -f "$GODOT_ANDROID_KEYSTORE_RELEASE_PATH" ] || { echo "No keystore at $GODOT_ANDROID_KEYSTORE_RELEASE_PATH" >&2; exit 1; }

python3 scripts/release/preflight.py android

code="$(sed -n '/^\[preset\.1\.options\]/,/^\[preset/p' export_presets.cfg | sed -n 's/^version\/code=//p' | head -1)"
out="builds/android/crownroad-$code.aab"
mkdir -p builds/android
echo "Exporting $out..."
godot --headless --path . --export-release "Android" "$out" > builds/android/export.log 2>&1 \
	|| { echo "Export failed; see builds/android/export.log" >&2; exit 1; }
[ -s "$out" ] || { echo "The export wrote no bundle; see builds/android/export.log" >&2; exit 1; }

python3 scripts/tools/bundle_size.py "$out"

# Every native library must be 16 KB-aligned (each LOAD segment's alignment 0x4000 or more).
sdk="${ANDROID_HOME:-$HOME/Library/Android/sdk}"
readelf="$(ls -d "$sdk"/ndk/*/toolchains/llvm/prebuilt/*/bin/llvm-readelf 2>/dev/null | tail -1 || true)"
if [ -n "$readelf" ]; then
	libs="$(mktemp -d)"
	unzip -q -o "$out" 'base/lib/arm64-v8a/*.so' -d "$libs"
	misaligned=0
	for lib in "$libs"/base/lib/arm64-v8a/*.so; do
		if "$readelf" -lW "$lib" | python3 -c 'import sys; sys.exit(any(f[:1] == ["LOAD"] and int(f[-1], 16) < 0x4000 for f in map(str.split, sys.stdin)))'; then :; else
			echo "Not 16 KB-aligned: $(basename "$lib")" >&2; misaligned=1
		fi
	done
	rm -rf "$libs"
	[ "$misaligned" = 0 ] || exit 1
	echo "Every arm64 native library is 16 KB-aligned."
else
	echo "No NDK llvm-readelf under $sdk/ndk; skipped the 16 KB alignment check." >&2
fi

echo
echo "Upload $out to Play Console (internal testing first)."
shasum -a 256 "$out"
