#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"
GODOT_BIN="${GODOT_BIN:-godot}"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
RUN_TAG="$(date +%s)-$$"
mkdir -p artifacts/live-ui-parity
"$DOTNET_BIN" build Game.csproj
"$GODOT_BIN" --headless --editor --path . --import > artifacts/live-ui-parity/import.log 2>&1
for mode in desktop small phone; do
  resolution=1280x720
  args=(--live-parity)
  if [[ "$mode" == small ]]; then resolution=1024x768; args+=(--small-window); fi
  if [[ "$mode" == phone ]]; then resolution=844x390; args+=(--mobile-preview); fi
  log="artifacts/live-ui-parity/$mode.log"
  "$GODOT_BIN" --path . --windowed --rendering-method gl_compatibility --resolution "$resolution" \
    res://scenes/tests/UiReviewSmoke.tscn -- "--save-suffix=ui-review-parity-$RUN_TAG-$mode" "${args[@]}" > "$log" 2>&1
  if ! rg -q 'LIVE_UI_PARITY_RESULT: 0 failures' "$log" || rg -q '^ERROR:' "$log"; then
    cat "$log"
    exit 1
  fi
  echo "Live navigation parity $mode passed."
done
echo "Screenshots and logs: $ROOT_DIR/artifacts/live-ui-parity"
