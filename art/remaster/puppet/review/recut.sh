#!/bin/zsh
# Re-cut people's layers with layers.py --legs (after editing layers.py, landmarks or a Codex edit), 4 at a time,
# and print each unit's draw order, weapon source and completeness.
#
#   art/remaster/puppet/review/recut.sh enemy_furnace_knight,enemy_boss_reliquary
#   art/remaster/puppet/review/recut.sh all          # every unit with puppet_stride sources
#
# Then look at artifacts/remaster/puppet_stride/<id>/layers_preview.png (each layer, then the reassembled figure),
# and after a layers.py change compare against a backup with review/layer_diff.py.
cd "${0:A:h}/../../../.."
P=artifacts/remaster/puppet_stride
IDS=()
for a in "$@"; do
  if [[ $a == all ]]; then IDS+=($(cd $P && ls -d */ | tr -d /))
  else IDS+=(${(s:,:)a}); fi
done
one() {
  art/remaster/puppet/run.sh art/remaster/puppet/layers.py $P/$1 --legs --preview $P/$1/layers_preview.png >/dev/null 2>&1 \
    && python3 -c "
import json;d=json.load(open('$P/$1/layers.json'))
print('$1', d['order'], 'weapon', d.get('weapon_source') or 'cut', 'whole', d.get('weapon_whole'))" \
    || echo "$1 FAILED"
}
# running jobs counted inline from $jobstates: `$(jobs -r | wc -l)` runs in a subshell that sees none (render.sh)
zmodload zsh/parameter
for u in $IDS; do
  [[ -f $P/$u/master.png ]] || continue
  while :; do r=(${(M)${(v)jobstates}:#running:*}); (( ${#r} < 4 )) && break; sleep .5; done
  one $u &
done
wait
