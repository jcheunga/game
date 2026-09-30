#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"
GODOT_BIN="${GODOT_BIN:-godot}"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
RUN_TAG="$(date +%s)-$$"
mkdir -p artifacts/privacy-review
"$DOTNET_BIN" build Game.csproj --nologo
OPTIONS=(--headless)
if [[ "${1:-}" == "--capture" ]]; then
  OPTIONS=(--windowed --rendering-method gl_compatibility --resolution 1280x720)
fi
"$GODOT_BIN" "${OPTIONS[@]}" --path . res://scenes/tests/PrivacyReview.tscn -- \
  "--save-suffix=privacy-review-$RUN_TAG" "$@" > artifacts/privacy-review/run.log 2>&1
cat artifacts/privacy-review/run.log
rg -q 'PRIVACY_REVIEW_RESULT: 0 failures' artifacts/privacy-review/run.log
