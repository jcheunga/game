#!/bin/zsh
# Render puppets with build.py, N Blender jobs in parallel (each unit its own Blender: one crash costs one unit).
#
#   art/remaster/puppet/review/render.sh all                    # every unit into artifacts/remaster/puppet_units
#   art/remaster/puppet/review/render.sh -j 3 enemy_lich,player_spear
#   art/remaster/puppet/review/render.sh --clip death all       # only the death clip, in place (every frame is still
#                                                               # posed, so metadata and weapon drops stay right)
#   art/remaster/puppet/review/render.sh --out /tmp/try enemy_lich   # a trial render beside the shipped one
#
# All 111 take ~30 min at -j 3. Keep the Mac awake (lid open): asleep, renders stall for hours. Never start a second
# render while one is running (pgrep -f review/render.sh): two Blenders writing one unit corrupt it.
cd "${0:A:h}/../../../.."
BLENDER=${BLENDER:-/opt/homebrew/bin/blender}
J=3; CLIP=""; OUT=""
IDS=()
while (( $# )); do
  case $1 in
    -j) J=$2; shift 2;;
    --clip) CLIP=$2; shift 2;;
    --out) OUT=$2; shift 2;;
    -*) echo "unknown option $1"; exit 2;;
    all) IDS+=($(python3 -c "import json;print(' '.join(u['Id'] for u in json.load(open('data/units.json'))['Units']))")); shift;;
    *) IDS+=(${(s:,:)1}); shift;;
  esac
done
(( ${#IDS} )) || { echo "no units given"; exit 2; }
LOG=artifacts/remaster/puppet_logs; mkdir -p $LOG; : > $LOG/render.log
one() {
  local u=$1 fr=()
  if [[ -n $CLIP ]]; then
    fr=(--frames $(python3 -c "import json;d=json.load(open('artifacts/remaster/puppet_units/$u/metadata.json'))['animations']['$CLIP'];print(','.join(str(i+1) for i in range(d['start'],d['start']+d['count'])))"))
  fi
  local o=(); [[ -n $OUT ]] && o=(--out $OUT)
  $BLENDER --background --factory-startup --python-exit-code 1 --python art/remaster/puppet/build.py -- --ids $u $fr $o \
    2>&1 | grep -E "PUPPET_COMPLETE|PUPPET_FAILED|Error|Traceback" | cut -c1-160 >> $LOG/render.log
}
for u in $IDS; do
  while (( $(jobs -r | wc -l) >= J )); do sleep 1; done
  one $u &
done
wait
ok=$(grep -c PUPPET_COMPLETE $LOG/render.log)
echo "rendered $ok of ${#IDS} (log $LOG/render.log)"
grep -E "FAILED|Error|Traceback" $LOG/render.log | head -5
