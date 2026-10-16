#!/bin/bash
# Builds the GameCenter iOS plugin and installs it in res://ios/plugins/gamecenter.
#
# The sources in src/ are Godot's official GameCenter plugin
# (https://github.com/godotengine/godot-ios-plugins, commit caafb2c) with the changes listed in
# README.md. An iOS plugin is linked into the engine, so it is compiled against the engine headers
# of the exact Godot version the game exports with, using the defines of the official templates.
#
# Needs Xcode, git and python3. The first run downloads the Godot source tag and SCons (used only
# to generate a few engine headers) into build/, which is not committed. GODOT_SOURCE can point at
# an existing checkout of that tag instead, and an installed scons is used when there is one.
set -euo pipefail

GODOT_TAG="4.6.1-stable"
here="$(cd "$(dirname "$0")" && pwd)"
build="$here/build"
godot="${GODOT_SOURCE:-$build/godot-$GODOT_TAG}"
dest="$here/../../ios/plugins/gamecenter"

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

# compile <debug|release> <iphoneos|iphonesimulator> <arch>: one static library slice.
compile() {
	local target="$1" sdk="$2" arch="$3"
	local objects="$build/obj/$target-$sdk-$arch"
	local flags=(-arch "$arch" -isysroot "$(xcrun --sdk "$sdk" --show-sdk-path)" -std=gnu++17 -stdlib=libc++
		-fobjc-arc -fmodules -fcxx-modules -fblocks -fvisibility=hidden -fno-exceptions -O2 -Wall -Werror=return-type
		-DNDEBUG -DNS_BLOCK_ASSERTIONS=1 -DTHREADS_ENABLED
		-DIOS_ENABLED -DAPPLE_EMBEDDED_ENABLED -DUNIX_ENABLED -DCOREAUDIO_ENABLED
		-I"$godot" -I"$godot/platform/ios")
	# Threads and debug features change the layout of engine classes such as Object, so they must
	# match the template the plugin is linked into.
	[ "$target" = debug ] && flags+=(-DDEBUG_ENABLED)
	if [ "$sdk" = iphonesimulator ]; then flags+=(-mios-simulator-version-min=15.0 -DIOS_SIMULATOR); else flags+=(-miphoneos-version-min=15.0); fi
	rm -rf "$objects" && mkdir -p "$objects"
	for source in "$here"/src/*.mm "$here"/src/*.cpp; do
		xcrun --sdk "$sdk" clang++ "${flags[@]}" -c "$source" -o "$objects/$(basename "$source").o"
	done
	xcrun --sdk "$sdk" libtool -static -o "$objects/libgamecenter.a" "$objects"/*.o
}

rm -rf "$build/obj" "$build/out" && mkdir -p "$build/out"
for target in debug release; do
	compile "$target" iphoneos arm64
	compile "$target" iphonesimulator arm64
	compile "$target" iphonesimulator x86_64
	mkdir -p "$build/obj/$target-simulator"
	lipo -create "$build/obj/$target-iphonesimulator-arm64/libgamecenter.a" "$build/obj/$target-iphonesimulator-x86_64/libgamecenter.a" \
		-output "$build/obj/$target-simulator/libgamecenter.a"
	xcodebuild -create-xcframework -library "$build/obj/$target-iphoneos-arm64/libgamecenter.a" \
		-library "$build/obj/$target-simulator/libgamecenter.a" -output "$build/out/gamecenter.$target.xcframework"
done

rm -rf "$dest" && mkdir -p "$dest"
cp -R "$build/out/gamecenter.debug.xcframework" "$build/out/gamecenter.release.xcframework" "$dest/"
cp "$here/src/gamecenter.gdip" "$dest/"
echo "Installed the GameCenter plugin in ios/plugins/gamecenter (Godot $GODOT_TAG)."
