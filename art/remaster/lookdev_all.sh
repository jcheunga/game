#!/bin/zsh
# Lookdev every unit (stance render + icon) in chunks, skipping units with a render newer than $STAMP.
# usage: [LOG=..] [CHUNK=6] art/remaster/lookdev_all.sh [ids-file] [tag]
cd "$(dirname $0)/../.."
IDS=${1:-}
TAG=${2:-v3}
LOG=${LOG:-artifacts/remaster/lookdev/all.log}
CHUNK=${CHUNK:-6}
mkdir -p artifacts/remaster/lookdev
if [[ -z "$IDS" ]]; then
  ids=($(python3 -c "import json;print(' '.join(u['Id'] for u in json.load(open('data/units.json'))['Units']))"))
else
  ids=($(cat $IDS))
fi
chunk=()
for id in $ids; do
  chunk+=$id
  if (( ${#chunk} == CHUNK )); then
    /opt/homebrew/bin/blender --background --factory-startup --python-exit-code 1 --python art/remaster/lookdev.py -- \
      --ids ${(j:,:)chunk} --samples 24 --size 600 --tag $TAG --portrait >> $LOG 2>&1
    chunk=()
  fi
done
if (( ${#chunk} )); then
  /opt/homebrew/bin/blender --background --factory-startup --python-exit-code 1 --python art/remaster/lookdev.py -- \
    --ids ${(j:,:)chunk} --samples 24 --size 600 --tag $TAG --portrait >> $LOG 2>&1
fi
echo ALL_DONE >> $LOG
