#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"
GODOT_BIN="${GODOT_BIN:-godot}"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
RUN_TAG="$(date +%s)-$$"
mkdir -p artifacts/adventure-map

"$DOTNET_BIN" build Game.csproj
"$GODOT_BIN" --headless --editor --path . --import > artifacts/adventure-map/import.log 2>&1
"$GODOT_BIN" --path . --windowed --rendering-method gl_compatibility --resolution 1280x720 \
  res://scenes/tests/UiReviewSmoke.tscn -- \
  "--save-suffix=ui-review-adventure-$RUN_TAG" --adventure \
  > artifacts/adventure-map/run.log 2>&1
if ! rg -q 'ADVENTURE_REVIEW_RESULT: 0 failures' artifacts/adventure-map/run.log || rg -q '^ERROR:' artifacts/adventure-map/run.log; then
  cat artifacts/adventure-map/run.log
  exit 1
fi
echo "Adventure map checks passed. Screenshots and logs: $ROOT_DIR/artifacts/adventure-map"
