#!/bin/sh
# Plays the whole campaign twice from a new save, stage 1 to 100, with CombatReviewSmoke --playthrough-sim: once as a
# player who never pays (grinds replays when a stage is too hard) and once as a spender (buys gold packs instead of
# grinding, food packs instead of waiting). Both runs go in parallel (about 45 minutes) and each checks that it
# finished the campaign and stayed inside the pacing bands below. Prints the zone-by-zone report at the end.
#
# The bands are the designed pacing (docs/PLAYTHROUGH_SIM.md): the campaign is much harder to finish without paying,
# a free player still finishes given enough time (weeks of play), and money skips most of the grind. Retune
# CampaignPacing (scripts/analysis/campaign_fit.py) or the prices, rerun this, and move a band only on purpose.
set -u
cd "$(dirname "$0")/../.."
export PATH="/usr/local/share/dotnet:$PATH"
FREE_REPLAYS=${FREE_REPLAYS:-150-1000}
FREE_DAYS=${FREE_DAYS:-15-90}
SPENDER_DOLLARS=${SPENDER_DOLLARS:-20-150}
work=$(mktemp -d "${TMPDIR:-/tmp}/crownroad-playthrough.XXXXXX")
run() {
    godot --headless --path . res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix="combat-review-playthrough-$1" \
        --playthrough-sim --profile="$1" --out="check-$1" "$2" "$3" > "$work/$1.log" 2>&1
}
run free --expect-replays="$FREE_REPLAYS" --expect-days="$FREE_DAYS" &
free=$!
sleep 5
run spender --expect-dollars="$SPENDER_DOLLARS" --expect-replays=0-150 &
spender=$!
wait $free; free_status=$?
wait $spender; spender_status=$?
grep -h "COMBAT_CHECK\|PLAYTHROUGH_WALL\|COMBAT_REVIEW_RESULT" "$work/free.log" "$work/spender.log"
# Money has to buy something: the spender finishes in well under two thirds of the free player's battles.
python3 - <<'PY' || free_status=1
import json, sys
free = json.load(open("artifacts/playthrough/check-free.json"))["summary"]
spender = json.load(open("artifacts/playthrough/check-spender.json"))["summary"]
ok = spender["battles"] * 3 < free["battles"] * 2
print(f"PLAYTHROUGH_CHECK: {'PASS' if ok else 'FAIL'} the spender needs under two thirds of the free player's battles ({spender['battles']} against {free['battles']})")
sys.exit(0 if ok else 1)
PY
python3 scripts/analysis/playthrough_report.py artifacts/playthrough/check-free.json artifacts/playthrough/check-spender.json
echo "Logs: $work"
[ $free_status -eq 0 ] && [ $spender_status -eq 0 ]
