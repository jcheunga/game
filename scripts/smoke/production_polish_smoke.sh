#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"
GODOT_BIN="${GODOT_BIN:-godot}"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
RUN_TAG="$(date +%s)-$$"
mkdir -p artifacts/production-polish
"$DOTNET_BIN" build Game.csproj > artifacts/production-polish/build.log 2>&1
for review in production-polish performance courage-drops; do
  "$GODOT_BIN" --headless --path . res://scenes/tests/CombatReviewSmoke.tscn -- \
    "--save-suffix=combat-review-polish-$RUN_TAG-$review" "--$review" \
    > "artifacts/production-polish/$review.log" 2>&1
  rg -q 'COMBAT_REVIEW_RESULT: 0 failures' "artifacts/production-polish/$review.log"
  if rg -q 'COMBAT_CHECK: FAIL|ERROR:' "artifacts/production-polish/$review.log"; then
    cat "artifacts/production-polish/$review.log"; exit 1
  fi
done
"$GODOT_BIN" --path . res://scenes/tests/UiReviewSmoke.tscn -- \
  "--save-suffix=ui-review-polish-$RUN_TAG" --production-polish \
  > artifacts/production-polish/ui.log 2>&1
rg -q 'PRODUCTION_POLISH_UI_RESULT: 0 failures' artifacts/production-polish/ui.log
if rg -q '^FAIL|ERROR:|UI_OVERFLOW' artifacts/production-polish/ui.log; then
  cat artifacts/production-polish/ui.log; exit 1
fi
echo "Production polish: reliability, combat, courage and desktop/phone UI checks passed."
