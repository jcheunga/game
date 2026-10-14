#!/usr/bin/env python3
"""Units whose puppet sources are complete (master, landmarks, and layers where the kind has them).

  python3 art/remaster/puppet/ready.py [--unbuilt]    # comma-separated ids, for build.py --ids
"""
import argparse
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import masters as M  # noqa: E402

ap = argparse.ArgumentParser()
ap.add_argument('--unbuilt', action='store_true', help='only units without a newer complete build')
a = ap.parse_args()
out = []
for i in M.UNITS:
    d = M.PUPPET / i
    if not (d / 'master.png').exists() or not (d / 'landmarks.json').exists():
        continue
    if M.kind_of(i) in M.LAYERED and not (d / 'layers.json').exists():
        continue
    done = M.REVIEW / 'puppet_units' / i / 'complete.json'
    if a.unbuilt and done.exists() and done.stat().st_mtime > max(p.stat().st_mtime for p in d.glob('*.json')):
        continue
    out.append(i)
print(','.join(out))
