#!/bin/zsh
# Stance-frame guides for paintover.py (build_units.py --guide), a few units per Blender process.
# usage: [LOG=..] [CHUNK=4] art/remaster/guide_all.sh ids-file
cd "$(dirname $0)/../.."
LOG=${LOG:-artifacts/remaster/guide_all.log}
CHUNK=${CHUNK:-4}
ids=($(cat $1))
chunk=()
run() {
  /opt/homebrew/bin/blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_units.py -- \
    --ids ${(j:,:)chunk} --guide --samples 64 --no-blend >> $LOG 2>&1
}
for id in $ids; do
  chunk+=$id
  if (( ${#chunk} == CHUNK )); then run; chunk=(); fi
done
(( ${#chunk} )) && run
echo GUIDE_ALL_DONE >> $LOG
