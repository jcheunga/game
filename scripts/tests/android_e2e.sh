#!/bin/bash
# Runs the game's server calls on an Android emulator or phone, against a throwaway local server:
#
#   scripts/tests/android_e2e.sh           export, install and run both launches
#   scripts/tests/android_e2e.sh --reuse   run the build the last run installed (after changes to this script only)
#
# 1. Exports a debug APK whose main scene is scenes/tests/AndroidServerReview.tscn, from temporary
#    copies of project.godot (main scene) and export_presets.cfg (APK instead of AAB, test scenes
#    included, the driver's arguments in command_line/extra_args). Official Android templates refuse
#    a scene path on the command line, and the exported launcher ignores the intent's
#    command_line_params, so the arguments go into the build's assets/_cl_. The committed files are
#    put back whatever happens. Installs the Android build template first if android/ has none.
# 2. Starts a server as server_e2e.sh does, on a fresh database, and forwards the device's port to it
#    with adb reverse: AccountSignIn accepts plain HTTP only on loopback, so the game uses
#    http://127.0.0.1 rather than the emulator's 10.0.2.2.
# 3. Clears the app's data and launches it (fresh), then launches it again (relaunch). The game's log
#    is read from logcat (tag godot). Both launches must report no failures and no load errors.
#
# Needs a booted emulator or a phone with USB debugging (ANDROID_SERIAL picks one), adb, the Godot
# editor with its Android SDK and JDK set in editor settings, and the .NET SDK. Play Billing purchases
# need a Play-installed build and are not covered; the shop is checked to refuse without it.
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
work="$root/artifacts/android-e2e"
port="${ANDROID_E2E_PORT:-8099}"
package=com.crownroad.game
driver=res://scenes/tests/AndroidServerReview.tscn
export PATH="/usr/local/share/dotnet:$PATH"
adb="${ADB:-$(command -v adb || echo "$HOME/Library/Android/sdk/platform-tools/adb")}"
reuse=0
[ "${1:-}" = "--reuse" ] && reuse=1
mkdir -p "$work"

"$adb" get-state > /dev/null 2>&1 || { echo "No Android device; boot an emulator (emulator -avd <name>) or plug in a phone." >&2; exit 1; }

if [ "$reuse" = 0 ]; then
	# The Android plugins' built libraries are not in git (bin/ is ignored).
	for aar in GodotGooglePlayBilling/bin/debug/GodotGooglePlayBilling-debug.aar PlayGamesSignIn/bin/debug/PlayGamesSignIn-debug.aar; do
		[ -f "$root/addons/$aar" ] || { echo "Missing addons/$aar; copy addons/*/bin from a checkout that has them, or build native/PlayGamesSignIn." >&2; exit 1; }
	done
	# 1. Export with temporary settings.
	args="-- --android-e2e --server=http://127.0.0.1:$port"
	# The emulator's Vulkan (gfxstream on MoltenVK) stops handing Godot swapchain images after a few
	# frames, which freezes the main loop, so an emulator build draws with OpenGL ES instead.
	[ "$("$adb" shell getprop ro.kernel.qemu | tr -d '\r')" = 1 ] && args="--rendering-method gl_compatibility --rendering-driver opengl3 $args"
	presets="$root/export_presets.cfg" project="$root/project.godot"
	cp "$presets" "$work/export_presets.cfg.keep"
	cp "$project" "$work/project.godot.keep"
	trap 'cp "$work/export_presets.cfg.keep" "$presets"; cp "$work/project.godot.keep" "$project"' EXIT
	python3 - "$presets" "$project" "$driver" "$args" <<'EOF'
import sys
presets, project, driver, args = sys.argv[1:]
text = open(presets, encoding="utf-8").read()
start = text.index('name="Android"')
end = text.find('\n[preset.', text.index('.options]', start))
android = text[start:end if end >= 0 else len(text)]
changed = android.replace('scripts/tests/*,scenes/tests/*,', '', 1)
changed = changed.replace('gradle_build/export_format=1', 'gradle_build/export_format=0', 1)
changed = changed.replace('.options]\n', f'.options]\n\ncommand_line/extra_args="{args}"\n', 1)
assert changed.count('export_format=0') == 1 and 'scenes/tests/*' not in changed, "Unexpected Android preset"
open(presets, "w", encoding="utf-8").write(text.replace(android, changed, 1))
text = open(project, encoding="utf-8").read()
main = 'run/main_scene="res://scenes/MainMenu.tscn"'
assert main in text, "Unexpected main scene"
open(project, "w", encoding="utf-8").write(text.replace(main, f'run/main_scene="{driver}"', 1))
EOF
	install_template=()
	[ -f "$root/android/.build_version" ] || install_template=(--install-android-build-template)
	rm -f "$work/crownroad.apk"
	godot --headless --path "$root" ${install_template[@]+"${install_template[@]}"} --export-debug "Android" "$work/crownroad.apk" > "$work/export.log" 2>&1 \
		&& [ -f "$work/crownroad.apk" ] || { echo "Export failed; see $work/export.log" >&2; exit 1; }
	cp "$work/export_presets.cfg.keep" "$presets"
	cp "$work/project.godot.keep" "$project"
	trap - EXIT
	"$adb" install -r "$work/crownroad.apk" > "$work/install.log" 2>&1 || { echo "Install failed; see $work/install.log" >&2; exit 1; }
fi

# 2. Server and port forwarding.
server=""
cleanup() {
	[ -n "$server" ] && kill "$server" 2> /dev/null && wait "$server" 2> /dev/null
	"$adb" reverse --remove "tcp:$port" > /dev/null 2>&1 || true
}
trap cleanup EXIT
rm -f "$work/server.db"
dotnet build "$root/server/Server.csproj" -v q -nologo > /dev/null
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://127.0.0.1:$port" DbPath="$work/server.db" \
CROWNROAD_DEV_SIGN_IN=1 CROWNROAD_ALLOW_TEST_PURCHASE_CLAIMS=1 \
	dotnet run --project "$root/server" --no-build --no-launch-profile > "$work/server.log" 2>&1 &
server=$!
for _ in $(seq 1 60); do curl -sf "http://127.0.0.1:$port/health" > /dev/null && break; sleep 0.5; done
curl -sf "http://127.0.0.1:$port/health" > /dev/null || { echo "Server did not start; see $work/server.log" >&2; exit 1; }
"$adb" reverse "tcp:$port" "tcp:$port" > /dev/null

# 3. The two launches.
# What the game logs when it cannot read its JSON, or when managed code fails.
load_errors="Failed to load game data|ShopProductCatalog: failed|Failed to parse save file|JsonTypeInfo metadata|Unhandled exception|SCRIPT ERROR"
failures=0
results="$work/results.txt"
: > "$results"
launch() {
	local name="$1" log="$work/launch-$1.log"
	"$adb" shell am force-stop "$package"
	[ "$name" = fresh ] && "$adb" shell pm clear "$package" > /dev/null
	"$adb" logcat -c
	"$adb" shell am start -n "$package/com.godot.game.GodotAppLauncher" > /dev/null
	for _ in $(seq 1 240); do
		"$adb" logcat -d -v brief > "$log" 2>&1
		grep -aq "ANDROID_RESULT" "$log" && break
		sleep 1
	done
	sleep 1
	"$adb" logcat -d -v brief > "$log" 2>&1
	"$adb" shell am force-stop "$package"
	echo "== $name" | tee -a "$results"
	grep -aohE "(ANDROID_(PHASE|CHECK|RESULT):.*|($load_errors).*)" "$log" | tee -a "$results" || true
	if ! grep -aq "ANDROID_RESULT: $name 0 failures" "$log" || grep -aqE "$load_errors" "$log"; then
		failures=$((failures + 1)); echo "Launch $name failed; see $log"
	fi
}
launch fresh
launch relaunch

echo
echo "$(grep -c "^ANDROID_CHECK: PASS" "$results") checks passed, $(grep -c "^ANDROID_CHECK: FAIL" "$results") failed, $failures launches failed"
echo "Logs: $work"
[ "$failures" = 0 ]
