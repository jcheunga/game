#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"
GODOT_BIN="${GODOT_BIN:-godot}"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
RUN_TAG="$(date +%s)-$$"
mkdir -p artifacts/stage-stars
"$DOTNET_BIN" build Game.csproj
"$GODOT_BIN" --headless --editor --path . --import > artifacts/stage-stars/import.log 2>&1
"$GODOT_BIN" --headless --path . res://scenes/tests/CombatReviewSmoke.tscn -- \
  "--save-suffix=combat-review-stars-$RUN_TAG" --stage-stars > artifacts/stage-stars/scoring.log 2>&1
rg -q 'COMBAT_REVIEW_RESULT: 0 failures' artifacts/stage-stars/scoring.log
for size in regular small; do
  EXTRA_ARGS=(--stage-stars)
  if [[ "$size" == small ]]; then EXTRA_ARGS+=(--small-window); fi
  "$GODOT_BIN" --path . --windowed --rendering-method gl_compatibility --resolution 1280x720 \
    res://scenes/tests/UiReviewSmoke.tscn -- "--save-suffix=ui-review-stars-$RUN_TAG-$size" \
    "${EXTRA_ARGS[@]}" > "artifacts/stage-stars/$size.log" 2>&1
  rg -q 'STAGE_STAR_UI_RESULT: 0 failures' "artifacts/stage-stars/$size.log"
done
if rg -n '^ERROR:|_CHECK: FAIL' artifacts/stage-stars/{scoring,regular,small}.log; then exit 1; fi
echo "Stage star scoring and map presentation passed. Captures: $ROOT_DIR/artifacts/stage-stars"
