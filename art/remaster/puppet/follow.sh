#!/bin/zsh
# Follows masters.py's master stage: every few minutes, runs the later stages (landmarks, edits, layers, icon) for
# units whose master has landed but whose sources are unfinished, until every unit with a master is done.
cd "${0:A:h}/../../.."
P=artifacts/remaster/puppet
while true; do
  todo=()
  for d in $P/*/; do
    i=${d:t}
    [[ -f $d/master.png ]] || continue
    [[ -f artifacts/remaster/puppet_units/$i/portrait.png && -f $d/landmarks.json ]] || { todo+=$i; continue }
    k=$(python3 -c "import sys;sys.path.insert(0,'art/remaster/puppet');import masters as m;print(m.kind_of('$i'))")
    if [[ $k == humanoid || $k == mount || $k == machine ]]; then [[ -f $d/layers.json ]] || todo+=$i; fi
  done
  if (( ${#todo} )); then
    python3 art/remaster/puppet/masters.py ${(j:,:)todo} --stages landmarks,body,torso,layers,icon --jobs 3
  fi
  n=$(ls -d $P/*/master.png 2>/dev/null | wc -l)
  if (( n >= 111 )) && (( ${#todo} == 0 )); then echo FOLLOW_DONE; break; fi
  sleep 120
done
