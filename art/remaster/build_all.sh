#!/bin/zsh
# Production renders for a list of units, a few per Blender process (memory stays flat over long runs).
# usage: [LOG=..] [CHUNK=3] [SAMPLES=96] [EXTRA="--paint"] art/remaster/build_all.sh ids-file
# Masters land in artifacts/remaster/units/<id>; then: python3 art/remaster/pack.py stage
cd "$(dirname $0)/../.."
LOG=${LOG:-artifacts/remaster/build_all.log}
CHUNK=${CHUNK:-3}
SAMPLES=${SAMPLES:-96}
ids=($(cat $1))
chunk=()
run() {
  /opt/homebrew/bin/blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_units.py -- \
    --ids ${(j:,:)chunk} --samples $SAMPLES --no-blend ${=EXTRA} >> $LOG 2>&1
}
for id in $ids; do
  chunk+=$id
  if (( ${#chunk} == CHUNK )); then
    run
    chunk=()
  fi
done
(( ${#chunk} )) && run
echo BUILD_ALL_DONE >> $LOG
