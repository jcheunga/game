#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"
GODOT_BIN="${GODOT_BIN:-godot}"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
RUN_TAG="$(date +%s)-$$"
mkdir -p artifacts/typography
"$DOTNET_BIN" build Game.csproj
"$GODOT_BIN" --headless --editor --path . --import > artifacts/typography/import.log 2>&1
for mode in typography typography-advanced small-window; do
  resolution=1280x720
  args=("--$mode")
  if [[ "$mode" == small-window ]]; then
    resolution=1024x768
    args+=(--typography)
  fi
  log="artifacts/typography/$mode.log"
  "$GODOT_BIN" --path . --windowed --rendering-method gl_compatibility --resolution "$resolution" \
    res://scenes/tests/UiReviewSmoke.tscn -- "--save-suffix=ui-review-$RUN_TAG-$mode" "${args[@]}" > "$log" 2>&1
  if ! rg -q 'TYPOGRAPHY_RESULT: 0 failures' "$log" || rg -q '^ERROR:' "$log"; then
    cat "$log"
    exit 1
  fi
  echo "Readability review $mode passed."
done
echo "Screenshots and logs: $ROOT_DIR/artifacts/typography*"
