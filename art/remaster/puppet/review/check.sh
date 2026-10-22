#!/bin/zsh
# The objective pass: motion_check.py over every frame of the units (N Blenders in parallel), then check_report.py's
# pass criteria (smear, ground, foot slip, gait: squat and crab walk). Exit status 1 if any unit fails.
#
#   art/remaster/puppet/review/check.sh all                 # ~10 min, writes artifacts/remaster/motion_check/
#   art/remaster/puppet/review/check.sh enemy_shade,player_spear --out /tmp/mc   # a quick look at a few
#
# motion_check poses the rig itself (it does not read renders), so it measures the current code and sources: run it
# before rendering to try a change, and again after rendering to sign off.
cd "${0:A:h}/../../../.."
BLENDER=${BLENDER:-/opt/homebrew/bin/blender}
J=3; OUT=artifacts/remaster/motion_check
IDS=()
while (( $# )); do
  case $1 in
    -j) J=$2; shift 2;;
    --out) OUT=$2; shift 2;;
    -*) echo "unknown option $1"; exit 2;;
    all) IDS+=($(python3 -c "import json;print(' '.join(u['Id'] for u in json.load(open('data/units.json'))['Units']))")); shift;;
    *) IDS+=(${(s:,:)1}); shift;;
  esac
done
(( ${#IDS} )) || { echo "no units given"; exit 2; }
mkdir -p $OUT
chunks=()
for k in $(seq 0 $((J - 1))); do
  c=""; for ((n = k + 1; n <= ${#IDS}; n += J)); do c+="${IDS[$n]},"; done
  [[ -n $c ]] && chunks+=(${c%,})
done
for c in $chunks; do
  $BLENDER --background --factory-startup --python-exit-code 1 --python art/remaster/puppet/motion_check.py -- \
    --ids $c --out $OUT 2>&1 | grep -E "Error|Traceback" &
done
wait
python3 art/remaster/puppet/check_report.py $OUT | tee /dev/stderr | grep -q "MOTION_CHECK: 0 of" || exit 1
