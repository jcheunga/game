#!/bin/zsh
# Builds each unit's puppet as soon as its sources are ready (puppet/ready.py --unbuilt), every minute, until all
# 111 units have a complete build. Logs PUPPET_COMPLETE / PUPPET_FAILED lines.
cd "${0:A:h}/../../.."
while true; do
  ids=$(python3 art/remaster/puppet/ready.py --unbuilt)
  if [[ -n $ids ]]; then
    /Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python-exit-code 1 \
      --python art/remaster/puppet/build.py -- --ids $ids 2>&1 | grep -E "PUPPET_COMPLETE|PUPPET_FAILED|Error"
  fi
  n=$(ls artifacts/remaster/puppet_units/*/complete.json 2>/dev/null | wc -l)
  (( n >= 111 )) && { echo AUTOBUILD_DONE; break; }
  sleep 60
done
