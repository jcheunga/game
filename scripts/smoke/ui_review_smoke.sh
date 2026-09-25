#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"
GODOT_BIN="${GODOT_BIN:-godot}"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
RUN_TAG="$(date +%s)-$$"
mkdir -p artifacts/ui-review

"$DOTNET_BIN" build Game.csproj
"$GODOT_BIN" --headless --editor --path . --import > artifacts/ui-review/import.log 2>&1
for mode in core all-menus playthrough; do
  "$GODOT_BIN" --path . --windowed --rendering-method gl_compatibility --resolution 1280x720 \
    res://scenes/tests/UiReviewSmoke.tscn -- \
    "--save-suffix=ui-review-$RUN_TAG-$mode" "--$mode" \
    > "artifacts/ui-review/$mode.log" 2>&1
  if ! rg -q 'UI_REVIEW_RESULT: 0 failures' "artifacts/ui-review/$mode.log"; then
    cat "artifacts/ui-review/$mode.log"
    exit 1
  fi
  echo "UI review $mode passed."
done
echo "Screenshots and logs: $ROOT_DIR/artifacts/ui-review"
