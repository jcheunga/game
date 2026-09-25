#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
mkdir -p artifacts/progression
run_tag="$(date +%s)-$$"
dotnet build Game.csproj
godot --headless --path . --fixed-fps 60 res://scenes/tests/CombatReviewSmoke.tscn -- \
  "--save-suffix=combat-review-economy-$run_tag" --economy-export > artifacts/progression/export.log 2>&1
for profile in core lean equipped; do
  extra=()
  if [[ "$profile" != core ]]; then extra+=(--unit-level-delta=-1); fi
  if [[ "$profile" == equipped ]]; then extra+=(--common-relics); fi
  for seed in 0 1000 2000; do
    stage_args=()
    suffix=""
    if [[ "$seed" != 0 ]]; then
      suffix="-seed-$seed"
      stage_args+=(--stages=12,15,16,21,25,26,29,52,53,55,56,58,60)
    fi
    godot --headless --path . --fixed-fps 60 res://scenes/tests/CombatReviewSmoke.tscn -- \
      "--save-suffix=combat-review-progression-$profile-$seed-$run_tag" --tactical \
      "--seed-offset=$seed" ${extra[@]+"${extra[@]}"} ${stage_args[@]+"${stage_args[@]}"} > "artifacts/progression/$profile$suffix.log" 2>&1
  done
done
for profile in armed siege-counter support-counter; do
  squad=()
  stages=12,15,16,21,25,26,29,52,53,55,56,58,60
  if [[ "$profile" == siege-counter ]]; then
    squad+=(--squad=player_lantern_guard,player_ballista,player_stormcaller)
    stages=58
  elif [[ "$profile" == support-counter ]]; then
    squad+=(--squad=player_lantern_guard,player_stormcaller,player_coordinator)
    stages=60
  fi
  for seed in 0 1000 2000; do
    godot --headless --path . --fixed-fps 60 res://scenes/tests/CombatReviewSmoke.tscn -- \
      "--save-suffix=combat-review-progression-$profile-$seed-$run_tag" --tactical --armaments \
      "--seed-offset=$seed" "--stages=$stages" ${squad[@]+"${squad[@]}"} > "artifacts/progression/$profile-seed-$seed.log" 2>&1
  done
done
python3 scripts/analysis/progression_audit.py
