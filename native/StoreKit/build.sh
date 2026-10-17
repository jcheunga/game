#!/bin/bash
# Builds the StoreKit iOS plugin and installs it in res://ios/plugins/storekit.
#
# StoreKit 2 is Swift-only, so src/ holds a Swift bridge (StoreKitBridge.swift) and the Objective-C++
# glue that registers it as the `StoreKit` engine singleton. swiftc writes an Objective-C header for
# the bridge, which the glue imports. Godot's iOS export template already contains a Swift file, so
# the exported Xcode project links the Swift runtime the bridge needs.
#
# Like native/GameCenter, the glue is linked into the engine, so it is compiled against the engine
# headers of the exact Godot version the game exports with, using the defines of the official
# templates. Needs Xcode, git and python3. The first run downloads the Godot source tag and SCons
# (used only to generate a few engine headers) into build/, which is not committed, unless
# native/GameCenter/build already has that tag. GODOT_SOURCE can point at an existing checkout of the
# tag instead, and an installed scons is used when there is one.
set -euo pipefail

GODOT_TAG="4.6.1-stable"
here="$(cd "$(dirname "$0")" && pwd)"
build="$here/build"
godot="${GODOT_SOURCE:-}"
if [ -z "$godot" ]; then
	godot="$build/godot-$GODOT_TAG"
	[ -d "$godot" ] || [ ! -d "$here/../GameCenter/build/godot-$GODOT_TAG" ] || godot="$here/../GameCenter/build/godot-$GODOT_TAG"
fi
dest="$here/../../ios/plugins/storekit"

# The token a purchase carries must match the server's, or every purchase is rejected.
"$here/test/run_tests.sh"

mkdir -p "$build"
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

# swift <iphoneos|iphonesimulator> <arch>: the bridge's object file and Objective-C header. They do
# not depend on the engine, so debug and release share them.
swift() {
	local sdk="$1" arch="$2"
	local objects="$build/obj/swift-$sdk-$arch" triple="$arch-apple-ios15.0"
	[ "$sdk" = iphonesimulator ] && triple="$triple-simulator"
	[ -f "$objects/storekit_swift.o" ] && return
	mkdir -p "$objects/include"
	xcrun --sdk "$sdk" swiftc -target "$triple" -sdk "$(xcrun --sdk "$sdk" --show-sdk-path)" \
		-module-name CrownroadStoreKit -parse-as-library -O -whole-module-optimization \
		-emit-object -o "$objects/storekit_swift.o" \
		-emit-objc-header-path "$objects/include/CrownroadStoreKit-Swift.h" "$here"/src/*.swift
}

# compile <debug|release> <iphoneos|iphonesimulator> <arch>: one static library slice.
compile() {
	local target="$1" sdk="$2" arch="$3"
	local objects="$build/obj/$target-$sdk-$arch" swift_objects="$build/obj/swift-$sdk-$arch"
	swift "$sdk" "$arch"
	local flags=(-arch "$arch" -isysroot "$(xcrun --sdk "$sdk" --show-sdk-path)" -std=gnu++17 -stdlib=libc++
		-fobjc-arc -fmodules -fcxx-modules -fblocks -fvisibility=hidden -fno-exceptions -O2 -Wall -Werror=return-type
		-DNDEBUG -DNS_BLOCK_ASSERTIONS=1 -DTHREADS_ENABLED
		-DIOS_ENABLED -DAPPLE_EMBEDDED_ENABLED -DUNIX_ENABLED -DCOREAUDIO_ENABLED
		-I"$godot" -I"$godot/platform/ios" -I"$swift_objects/include")
	# Threads and debug features change the layout of engine classes such as Object, so they must
	# match the template the plugin is linked into.
	[ "$target" = debug ] && flags+=(-DDEBUG_ENABLED)
	if [ "$sdk" = iphonesimulator ]; then flags+=(-mios-simulator-version-min=15.0 -DIOS_SIMULATOR); else flags+=(-miphoneos-version-min=15.0); fi
	rm -rf "$objects" && mkdir -p "$objects"
	for source in "$here"/src/*.mm "$here"/src/*.cpp; do
		xcrun --sdk "$sdk" clang++ "${flags[@]}" -c "$source" -o "$objects/$(basename "$source").o"
	done
	xcrun --sdk "$sdk" libtool -static -o "$objects/libstorekit.a" "$objects"/*.o "$swift_objects/storekit_swift.o"
}

rm -rf "$build/obj" "$build/out" && mkdir -p "$build/out"
for target in debug release; do
	compile "$target" iphoneos arm64
	compile "$target" iphonesimulator arm64
	compile "$target" iphonesimulator x86_64
	mkdir -p "$build/obj/$target-simulator"
	lipo -create "$build/obj/$target-iphonesimulator-arm64/libstorekit.a" "$build/obj/$target-iphonesimulator-x86_64/libstorekit.a" \
		-output "$build/obj/$target-simulator/libstorekit.a"
	xcodebuild -create-xcframework -library "$build/obj/$target-iphoneos-arm64/libstorekit.a" \
		-library "$build/obj/$target-simulator/libstorekit.a" -output "$build/out/storekit.$target.xcframework"
done

rm -rf "$dest" && mkdir -p "$dest"
cp -R "$build/out/storekit.debug.xcframework" "$build/out/storekit.release.xcframework" "$dest/"
cp "$here/src/storekit.gdip" "$dest/"
echo "Installed the StoreKit plugin in ios/plugins/storekit (Godot $GODOT_TAG)."
