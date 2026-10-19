#!/bin/bash
# Buys through the StoreKit plugin on an iOS simulator, against a throwaway local server, without
# Xcode's window or an App Store account:
#
#   native/StoreKit/test/simulator_e2e.sh            export, build and run every phase
#   native/StoreKit/test/simulator_e2e.sh --prepare  only export and build, to run from Xcode
#
# 1. Exports a debug iOS project with the test drivers, using a copy of the iOS preset with a
#    placeholder team ID (the export refuses a blank one, and a simulator build is not signed).
# 2. Godot 4.6.1's official iOS template has an x86_64-only simulator engine library, while iOS 27
#    simulators run arm64 only. The script compiles the engine for the arm64 simulator once from the
#    Godot source build.sh downloaded (a few minutes, kept in build/engine) and puts it in the
#    exported project. It allows a scene path on the command line, which official templates refuse,
#    so the driver scene can be launched. Device builds keep the official library.
# 3. Selects Crownroad.storekit in the scheme (use_storekit_config.sh) and builds for the simulator.
# 4. Starts a server with test purchase claims and a fresh database, and launches the game once per
#    phase. Most phases run scenes/tests/StoreKitPluginReview.tscn, which drives the plugin's engine
#    singleton and validates with the server the way NativeIAPService does. The shop phase runs
#    scenes/tests/StoreKitShopReview.tscn: the game's data, catalog, save and cloud backup load
#    under NativeAOT, and a pack is bought from the real shop through NativeIAPService. Every phase
#    fails if the game logs that it could not read its data or shop catalog.
#    StoreKitTestSession.swift, loaded into the game, opens an SKTestSession from Crownroad.storekit,
#    the way an Xcode run with that configuration does, and sets up Ask to Buy or a simulated
#    failure where a phase needs it.
#    SKTestSession looks for XCTest, so the launch puts Xcode's simulator test frameworks on the
#    search path, as a test run does. The game's log is read from the simulator's unified log.
#
# Needs Xcode with an iOS simulator runtime, the plugin installed by build.sh, and the Godot editor
# and .NET SDK the project uses. Each run uses a new isolated save and player.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
build="$here/../build"
work="$build/e2e"
port="${STOREKIT_E2E_PORT:-8098}"
godot_tag="4.6.1-stable"
godot_source="${GODOT_SOURCE:-$build/godot-$godot_tag}"
[ -d "$godot_source" ] || godot_source="$here/../../GameCenter/build/godot-$godot_tag"
export PATH="/usr/local/share/dotnet:$PATH"
prepare_only=0 reuse=0
[ "${1:-}" = "--prepare" ] && prepare_only=1
# --reuse runs the phases against the app the last run built (after changes to this script only).
[ "${1:-}" = "--reuse" ] && reuse=1

[ -f "$root/ios/plugins/storekit/storekit.gdip" ] || { echo "Run native/StoreKit/build.sh first." >&2; exit 1; }
[ -d "$godot_source" ] || { echo "No Godot $godot_tag source; run native/StoreKit/build.sh first." >&2; exit 1; }
app="$work/derived/Build/Products/Debug-iphonesimulator/crownroad.app"
project="$work/export/crownroad.xcodeproj"
if [ "$reuse" = 0 ]; then
rm -rf "$work/export" && mkdir -p "$work/export"

# 1. Export with a temporary preset; the committed one is put back whatever happens.
presets="$root/export_presets.cfg"
cp "$presets" "$work/export_presets.cfg.keep"
trap 'cp "$work/export_presets.cfg.keep" "$presets"' EXIT
python3 - "$presets" <<'EOF'
import sys
path = sys.argv[1]
text = open(path, encoding="utf-8").read()
start = text.index('name="iOS"')
head, ios = text[:start], text[start:]
ios = ios.replace('application/app_store_team_id=""', 'application/app_store_team_id="ABCDE12345"', 1)
ios = ios.replace('scripts/tests/*,scenes/tests/*,', '', 1)
ios = ios.replace('.options]\n', '.options]\n\napplication/export_project_only=true\n', 1)
open(path, "w", encoding="utf-8").write(head + ios)
EOF
godot --headless --path "$root" --export-debug "iOS" "$work/export/crownroad.ipa" > "$work/export.log" 2>&1 \
	|| { echo "Export failed; see $work/export.log" >&2; exit 1; }
cp "$work/export_presets.cfg.keep" "$presets"
trap - EXIT

# 2. The engine for the arm64 simulator.
engine="$build/engine/libgodot-$godot_tag-ios-simulator-arm64-debug-path-overrides.a"
if [ ! -f "$engine" ]; then
	scons="$(command -v scons || echo "$build/venv/bin/scons")"
	echo "Compiling the Godot engine for the arm64 simulator (once, a few minutes)..."
	(cd "$godot_source" && "$scons" platform=ios target=template_debug ios_simulator=yes arch=arm64 \
		module_mono_enabled=yes generate_bundle=no disable_path_overrides=no -j"$(sysctl -n hw.ncpu)") > "$work/engine.log" 2>&1 \
		|| { echo "Engine build failed; see $work/engine.log" >&2; exit 1; }
	mkdir -p "$(dirname "$engine")"
	cp "$godot_source/bin/libgodot.ios.template_debug.arm64.simulator.a" "$engine"
fi
cp "$engine" "$work/export/crownroad.xcframework/ios-arm64_x86_64-simulator/libgodot.a"

# 3. StoreKit configuration and simulator build.
"$here/../use_storekit_config.sh" "$project"
xcodebuild -project "$project" -scheme crownroad -configuration Debug -sdk iphonesimulator \
	-destination 'generic/platform=iOS Simulator' -derivedDataPath "$work/derived" ARCHS=arm64 \
	CODE_SIGNING_ALLOWED=NO build > "$work/xcodebuild.log" 2>&1 \
	|| { echo "Simulator build failed; see $work/xcodebuild.log" >&2; exit 1; }
fi
if [ "$prepare_only" = 1 ]; then
	echo "Built $app. Open $project in Xcode to run it with the StoreKit configuration."
	exit 0
fi

# The library that opens the SKTestSession inside the game.
sdk="$(xcrun --sdk iphonesimulator --show-sdk-path)"
session_library="$work/StoreKitTestSession.dylib"
xcrun --sdk iphonesimulator clang -target arm64-apple-ios17.0-simulator -isysroot "$sdk" \
	-c "$here/StoreKitTestSessionStart.c" -o "$work/StoreKitTestSessionStart.o"
xcrun --sdk iphonesimulator swiftc -target arm64-apple-ios17.0-simulator -sdk "$sdk" \
	-F "$sdk/Developer/Library/Frameworks" -emit-library -module-name StoreKitTestSession \
	-o "$session_library" "$here/StoreKitTestSession.swift" "$work/StoreKitTestSessionStart.o"

# 4. Server, simulator and the phases.
server=""
stop_server() { [ -n "$server" ] && kill "$server" 2> /dev/null && wait "$server" 2> /dev/null; server=""; }
trap stop_server EXIT
rm -f "$work/server.db"
dotnet build "$root/server/Server.csproj" -v q -nologo > /dev/null
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://127.0.0.1:$port" DbPath="$work/server.db" \
CROWNROAD_ALLOW_TEST_PURCHASE_CLAIMS=1 \
	dotnet run --project "$root/server" --no-build --no-launch-profile > "$work/server.log" 2>&1 &
server=$!
for _ in $(seq 1 60); do curl -sf "http://127.0.0.1:$port/health" > /dev/null && break; sleep 0.5; done
curl -sf "http://127.0.0.1:$port/health" > /dev/null || { echo "Server did not start; see $work/server.log" >&2; exit 1; }

if ! xcrun simctl list devices booted | grep -q Booted; then
	device="$(xcrun simctl list devices available | grep -m1 -oE 'iPhone[^(]*\(([0-9A-F-]{36})\)' | grep -oE '[0-9A-F-]{36}')"
	[ -n "$device" ] || { echo "No iPhone simulator; install one with: xcodebuild -downloadPlatform iOS" >&2; exit 1; }
	xcrun simctl boot "$device" && xcrun simctl bootstatus "$device" > /dev/null
fi
bundle="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$app/Info.plist")"
xcrun simctl terminate booted "$bundle" 2> /dev/null || true
xcrun simctl install booted "$app"

suffix="storekit-$(date +%s)"
testing="$(xcode-select -p)/Platforms/iPhoneSimulator.platform/Developer"
failures=0
results="$work/results.txt"
: > "$results"
# phase <name> [reset] [ask-to-buy] [fail]: one launch of the driver.
phase() {
	local name="$1" reset="${2:-0}" ask="${3:-0}" fail="${4:-}" log="$work/phase-$1.log" errors="$work/phase-$1.err.log"
	local scene=res://scenes/tests/StoreKitPluginReview.tscn
	[ "$name" = shop ] && scene=res://scenes/tests/StoreKitShopReview.tscn
	: > "$errors"
	xcrun simctl spawn booted log stream --level info --style compact --predicate 'subsystem == "com.crownroad.game"' > "$log" 2>&1 &
	sleep 1
	SIMCTL_CHILD_DYLD_INSERT_LIBRARIES="$session_library" \
	SIMCTL_CHILD_DYLD_FRAMEWORK_PATH="$testing/Library/Frameworks:$testing/Library/PrivateFrameworks" \
	SIMCTL_CHILD_DYLD_LIBRARY_PATH="$testing/usr/lib" \
	SIMCTL_CHILD_CR_STOREKIT_CONFIG="$here/../Crownroad.storekit" \
	SIMCTL_CHILD_CR_STOREKIT_RESET="$reset" SIMCTL_CHILD_CR_STOREKIT_ASK_TO_BUY="$ask" SIMCTL_CHILD_CR_STOREKIT_FAIL="$fail" \
		xcrun simctl launch --terminate-running-process --stderr="$errors" booted "$bundle" \
		"$scene" -- --save-suffix="$suffix" \
		--server="http://127.0.0.1:$port" --phase="$name" > /dev/null
	local answered=0
	for _ in $(seq 1 360); do
		grep -aq "STOREKIT_RESULT" "$log" && break
		if [ "$answered" = 0 ] && grep -aq "STOREKIT_AWAITING_APPROVAL" "$log"; then
			answered=1
			xcrun simctl spawn booted notifyutil -p com.crownroad.storekit-test.approve
		fi
		sleep 0.5
	done
	# The test session's transactions as the phase leaves them, for the log.
	xcrun simctl spawn booted notifyutil -p com.crownroad.storekit-test.list
	sleep 1
	xcrun simctl terminate booted "$bundle" 2> /dev/null || true
	# Killing xcrun leaves the simulator's log process running, so it is stopped by name.
	pkill -f 'log stream --level info --style compact --predicate subsystem == "com.crownroad.game"' || true
	echo "== $name" | tee -a "$results"
	grep -aohE "(STOREKIT_(CHECK|RESULT):.*|SCRIPT ERROR.*|\[StoreKitTest\] Session.*|($load_errors).*)" "$log" "$errors" | tee -a "$results" || true
	if ! grep -aq "STOREKIT_RESULT: $name 0 failures" "$log" || grep -aqE "$load_errors" "$log" "$errors"; then
		failures=$((failures + 1)); echo "Phase $name failed; see $log"
	fi
}
# What the game logs when it cannot read its JSON (as the NativeAOT build did with reflection-based System.Text.Json).
load_errors="Failed to load game data|ShopProductCatalog: failed|Failed to parse save file|JsonTypeInfo metadata"

# STOREKIT_E2E_PHASES picks phases, for example "buy interrupt recover". Buy, ask-to-buy and shop
# start from no transactions, which also clears the unfinished one the unverified phase leaves behind.
# Cancelling and declining are not phases: Xcode's test store reports a simulated cancellation as an
# unknown error and still records the purchase, and signs a declined Ask to Buy request like a
# completed purchase. The shop's handling of both is checked by shop_desktop.sh; StoreKit's needs a
# device or an Xcode run where the payment sheet is cancelled by hand.
for name in ${STOREKIT_E2E_PHASES:-buy clean unverified ask-to-buy interrupt recover clean shop}; do
	case "$name" in
		buy | shop) phase "$name" 1 ;;
		unverified) phase unverified 0 0 unverified ;;
		ask-to-buy) phase ask-to-buy 1 1 ;;
		*) phase "$name" ;;
	esac
done

echo
echo "$(grep -c "^STOREKIT_CHECK: PASS" "$results") checks passed, $(grep -c "^STOREKIT_CHECK: FAIL" "$results") failed, $failures phases failed"
echo "Logs: $work"
[ "$failures" = 0 ]
