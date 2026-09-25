#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"
GODOT_BIN="${GODOT_BIN:-godot}"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
RUN_TAG="$(date +%s)-$$"
mkdir -p artifacts/combat-review
"$DOTNET_BIN" build Game.csproj
"$GODOT_BIN" --headless --path . --fixed-fps 60 res://scenes/tests/CombatReviewSmoke.tscn -- \
  "--save-suffix=combat-review-$RUN_TAG" --regressions > artifacts/combat-review/regressions.log 2>&1
cat artifacts/combat-review/regressions.log
rg -q 'COMBAT_REVIEW_RESULT: 0 failures' artifacts/combat-review/regressions.log
if rg -q 'ERROR:|COMBAT_CHECK: FAIL' artifacts/combat-review/regressions.log; then
  exit 1
fi
if [[ "${1:-}" == "--campaign" ]]; then
  for profile in basic tactical; do
    EXTRA_ARG=--basic
    if [[ "$profile" == tactical ]]; then EXTRA_ARG=--tactical; fi
    "$GODOT_BIN" --headless --path . --fixed-fps 60 res://scenes/tests/CombatReviewSmoke.tscn -- \
      "--save-suffix=combat-review-$RUN_TAG-$profile" "$EXTRA_ARG" \
      > "artifacts/combat-review/$profile.log" 2>&1
    rg -q 'COMBAT_REVIEW_RESULT: 0 failures' "artifacts/combat-review/$profile.log"
    if rg -q 'ERROR:|COMBAT_CHECK: FAIL' "artifacts/combat-review/$profile.log"; then
      cat "artifacts/combat-review/$profile.log"
      exit 1
    fi
    echo "Completed 60-stage $profile benchmark (wins, losses and timeouts are recorded as balance observations)."
  done
fi
