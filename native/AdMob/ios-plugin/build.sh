#!/bin/bash
# Builds the CrownroadAds iOS plugin (AdMob rewarded ads after Google's consent form) and installs it, with Google's
# frameworks, in res://ios/plugins/crownroadads.
#
# An iOS plugin is linked into the engine, so src/ is compiled against the engine headers of the exact Godot version
# the game exports with, using the defines of the official templates (as native/GameCenter/build.sh does). The first
# run downloads Google's Mobile Ads SDK for iOS (googlemobileadssdkios.zip from dl.google.com, about 12 MB, holding
# GoogleMobileAds.xcframework and UserMessagingPlatform.xcframework) into build/, and the Godot source tag and SCons
# as GameCenter does. GODOT_SOURCE can point at an existing checkout of the tag. Google's frameworks are copied next
# to the plugin but not committed (.gitignore); rerun this script on a new checkout.
#
# Needs Xcode, git, curl and python3.
set -euo pipefail

GODOT_TAG="4.6.1-stable"
here="$(cd "$(dirname "$0")" && pwd)"
build="$here/build"
godot="${GODOT_SOURCE:-$build/godot-$GODOT_TAG}"
[ -d "$godot" ] || godot="$here/../../GameCenter/build/godot-$GODOT_TAG"
dest="$here/../../../ios/plugins/crownroadads"
sdk="$build/sdk"

mkdir -p "$build"
if [ ! -d "$sdk/GoogleMobileAds.xcframework" ]; then
	curl -fsSL -o "$build/googlemobileadssdkios.zip" https://dl.google.com/googleadmobadssdk/googlemobileadssdkios.zip
	rm -rf "$build/unpacked" && mkdir -p "$build/unpacked" && (cd "$build/unpacked" && unzip -q ../googlemobileadssdkios.zip)
	mkdir -p "$sdk"
	for framework in GoogleMobileAds UserMessagingPlatform; do
		found="$(find "$build/unpacked" -type d -name "$framework.xcframework" | head -n 1)"
		[ -n "$found" ] || { echo "$framework.xcframework is not in Google's package"; exit 1; }
		cp -R "$found" "$sdk/"
	done
fi
if [ ! -d "$godot" ]; then
	git clone --depth 1 --branch "$GODOT_TAG" https://github.com/godotengine/godot.git "$godot"
fi
scons="$(command -v scons || true)"
if [ -z "$scons" ]; then
	[ -x "$build/venv/bin/scons" ] || { python3 -m venv "$build/venv" && "$build/venv/bin/pip" install --quiet scons; }
	scons="$build/venv/bin/scons"
fi
# Only the generated headers are needed, not an engine build.
(cd "$godot" && "$scons" platform=ios target=template_debug \
	core/version_generated.gen.h core/disabled_classes.gen.h core/object/gdvirtual.gen.inc \
	core/extension/ext_wrappers.gen.inc core/extension/gdextension_interface.gen.h \
	modules/modules_enabled.gen.h core/profiling/profiling.gen.h)

# slice <iphoneos|iphonesimulator>: the folder of each Google framework that holds that SDK's headers.
slice() {
	case "$1" in
		iphoneos) echo "ios-arm64" ;;
		*) echo "ios-arm64_x86_64-simulator" ;;
	esac
}

# compile <debug|release> <iphoneos|iphonesimulator> <arch>: one static library slice.
compile() {
	local target="$1" sdk_name="$2" arch="$3"
	local objects="$build/obj/$target-$sdk_name-$arch" part
	part="$(slice "$sdk_name")"
	local flags=(-arch "$arch" -isysroot "$(xcrun --sdk "$sdk_name" --show-sdk-path)" -std=gnu++17 -stdlib=libc++
		-fobjc-arc -fmodules -fcxx-modules -fblocks -fvisibility=hidden -fno-exceptions -O2 -Wall -Werror=return-type
		-DNDEBUG -DNS_BLOCK_ASSERTIONS=1 -DTHREADS_ENABLED
		-DIOS_ENABLED -DAPPLE_EMBEDDED_ENABLED -DUNIX_ENABLED -DCOREAUDIO_ENABLED
		-I"$godot" -I"$godot/platform/ios"
		-F"$sdk/GoogleMobileAds.xcframework/$part" -F"$sdk/UserMessagingPlatform.xcframework/$part")
	# Threads and debug features change the layout of engine classes such as Object, so they must match the
	# template the plugin is linked into.
	[ "$target" = debug ] && flags+=(-DDEBUG_ENABLED)
	if [ "$sdk_name" = iphonesimulator ]; then flags+=(-mios-simulator-version-min=15.0 -DIOS_SIMULATOR); else flags+=(-miphoneos-version-min=15.0); fi
	rm -rf "$objects" && mkdir -p "$objects"
	for source in "$here"/src/*.mm "$here"/src/*.cpp; do
		xcrun --sdk "$sdk_name" clang++ "${flags[@]}" -c "$source" -o "$objects/$(basename "$source").o"
	done
	xcrun --sdk "$sdk_name" libtool -static -o "$objects/libcrownroadads.a" "$objects"/*.o
}

rm -rf "$build/obj" "$build/out" && mkdir -p "$build/out"
for target in debug release; do
	compile "$target" iphoneos arm64
	compile "$target" iphonesimulator arm64
	compile "$target" iphonesimulator x86_64
	mkdir -p "$build/obj/$target-simulator"
	lipo -create "$build/obj/$target-iphonesimulator-arm64/libcrownroadads.a" "$build/obj/$target-iphonesimulator-x86_64/libcrownroadads.a" \
		-output "$build/obj/$target-simulator/libcrownroadads.a"
	xcodebuild -create-xcframework -library "$build/obj/$target-iphoneos-arm64/libcrownroadads.a" \
		-library "$build/obj/$target-simulator/libcrownroadads.a" -output "$build/out/crownroadads.$target.xcframework"
done

rm -rf "$dest" && mkdir -p "$dest"
cp -R "$build/out/crownroadads.debug.xcframework" "$build/out/crownroadads.release.xcframework" "$dest/"
cp -R "$sdk/GoogleMobileAds.xcframework" "$sdk/UserMessagingPlatform.xcframework" "$dest/"
cp "$here/src/crownroadads.gdip" "$dest/"
version="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$sdk"/GoogleMobileAds.xcframework/ios-arm64/GoogleMobileAds.framework/Info.plist 2>/dev/null || echo unknown)"
echo "Installed the CrownroadAds plugin in ios/plugins/crownroadads (Godot $GODOT_TAG, Google Mobile Ads $version)."
