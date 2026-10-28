#!/bin/bash
# Builds the signed iOS archive and the .ipa to upload to App Store Connect.
#
#   APP_STORE_TEAM_ID=ABCDE12345 scripts/release/ios_release.sh
#
# Needs full Xcode signed in to your Apple Developer account (Xcode → Settings → Accounts), so
# automatic signing can make the distribution certificate and App Store profile for
# com.crownroad.game. The team ID stays out of export_presets.cfg: the script sets it on a copy of
# the preset for the export and puts the committed file back whatever happens.
#
# 1. Runs the release preflight for iOS and stops if anything fails.
# 2. Exports the Xcode project to builds/ios/xcode with Godot's iOS template.
# 3. Checks the exported Info.plist and PrivacyInfo.xcprivacy carry what the store answers promise.
# 4. Archives a Release build and exports builds/ios/crownroad.ipa for App Store Connect.
# 5. Prints the .ipa's SHA-256. Upload it with Apple's Transporter app, or from Xcode's Organizer
#    (Window → Organizer) with the archive in builds/ios.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$root"
export PATH="/usr/local/share/dotnet:$PATH"
team="${APP_STORE_TEAM_ID:-}"
[[ "$team" =~ ^[A-Z0-9]{10}$ ]] || { echo "Set APP_STORE_TEAM_ID to your 10-character Apple team ID." >&2; exit 1; }

python3 scripts/release/preflight.py ios

work="$root/builds/ios"
rm -rf "$work/xcode" && mkdir -p "$work/xcode"
presets="$root/export_presets.cfg"
cp "$presets" "$work/export_presets.cfg.keep"
trap 'cp "$work/export_presets.cfg.keep" "$presets"' EXIT
python3 - "$presets" "$team" <<'EOF'
import re, sys
path, team = sys.argv[1], sys.argv[2]
text = open(path, encoding="utf-8").read()
start = text.index('name="iOS"')
head, ios = text[:start], text[start:]
ios = re.sub(r'application/app_store_team_id=".*?"', f'application/app_store_team_id="{team}"', ios, count=1)
ios = ios.replace('application/export_project_only=false', 'application/export_project_only=true', 1)
if 'application/export_project_only=true' not in ios:
    ios = ios.replace('.options]\n', '.options]\n\napplication/export_project_only=true\n', 1)
open(path, "w", encoding="utf-8").write(head + ios)
EOF
echo "Exporting the Xcode project..."
godot --headless --path "$root" --export-release "iOS" "$work/xcode/crownroad.ipa" > "$work/export.log" 2>&1 \
	|| { echo "Export failed; see $work/export.log" >&2; exit 1; }
cp "$work/export_presets.cfg.keep" "$presets"
trap - EXIT

project="$work/xcode/crownroad.xcodeproj"
[ -d "$project" ] || { echo "No Xcode project was exported; see $work/export.log" >&2; exit 1; }
plist="$work/xcode/crownroad/crownroad-Info.plist"
privacy="$work/xcode/PrivacyInfo.xcprivacy"
[ "$(/usr/libexec/PlistBuddy -c 'Print :ITSAppUsesNonExemptEncryption' "$plist")" = "false" ] \
	|| { echo "Info.plist lacks ITSAppUsesNonExemptEncryption = false" >&2; exit 1; }
for type in EmailAddress UserID PurchaseHistory ProductInteraction GameplayContent CrashData; do
	grep -q "NSPrivacyCollectedDataType$type<" "$privacy" || { echo "PrivacyInfo.xcprivacy does not declare $type" >&2; exit 1; }
done
echo "Info.plist and the privacy manifest match the store answers."

echo "Archiving (this signs with your team; Xcode may ask for keychain access)..."
xcodebuild -project "$project" -scheme crownroad -configuration Release -destination "generic/platform=iOS" \
	-archivePath "$work/crownroad.xcarchive" -allowProvisioningUpdates DEVELOPMENT_TEAM="$team" CODE_SIGN_STYLE=Automatic \
	archive > "$work/archive.log" 2>&1 || { echo "Archive failed; see $work/archive.log" >&2; exit 1; }

cat > "$work/ExportOptions.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>method</key><string>app-store-connect</string>
	<key>teamID</key><string>$team</string>
	<key>signingStyle</key><string>automatic</string>
	<key>uploadSymbols</key><true/>
</dict>
</plist>
EOF
xcodebuild -exportArchive -archivePath "$work/crownroad.xcarchive" -exportOptionsPlist "$work/ExportOptions.plist" \
	-exportPath "$work/export" -allowProvisioningUpdates > "$work/export-archive.log" 2>&1 \
	|| { echo "Exporting the .ipa failed; see $work/export-archive.log" >&2; exit 1; }
ipa="$(ls "$work"/export/*.ipa | head -1)"
cp "$ipa" "$work/crownroad.ipa"

echo
echo "Upload $work/crownroad.ipa with Transporter, or the archive from Xcode's Organizer."
shasum -a 256 "$work/crownroad.ipa"
