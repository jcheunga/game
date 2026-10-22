#!/usr/bin/env python3
"""Show WHERE motion_check finds smear: the frame rendered, with every smeared cell painted red over it, and the
two bones that tear it printed. Use it on the worst frame check_report names before changing anything.

  art/remaster/puppet/run.sh art/remaster/puppet/review/smear_overlay.py enemy_shade attack 2 out.png

The frame is the clip-local index check_report prints ("at frame 2"). Runs Blender twice (measure, render).
"""
import json
import os
import subprocess
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import common as C  # noqa: E402

BLENDER = os.environ.get('BLENDER', '/opt/homebrew/bin/blender')
ident, clip, i, out = sys.argv[1], sys.argv[2], int(sys.argv[3]), sys.argv[4]
tmp = Path(tempfile.mkdtemp(prefix='smear_'))
dump = tmp / 'dump.npz'
env = dict(os.environ, MC_DUMP=f'{clip}:{i}:{dump}')
subprocess.run([BLENDER, '--background', '--factory-startup', '--python-exit-code', '1', '--python',
                str(C.ROOT / 'art/remaster/puppet/motion_check.py'), '--', '--ids', ident, '--out', str(tmp / 'mc')],
               env=env, check=True, capture_output=True)
rows = json.loads((tmp / 'mc' / f'{ident}.json').read_text())['frames']
row = next(r for r in rows if r['clip'] == clip and r['i'] == i)
start = C.meta(ident)['animations'][clip]['start']
subprocess.run([BLENDER, '--background', '--factory-startup', '--python-exit-code', '1', '--python',
                str(C.ROOT / 'art/remaster/puppet/build.py'), '--', '--ids', ident, '--frames', str(start + i + 1),
                '--out', str(tmp / 'r')], check=True, capture_output=True)
f = Image.open(tmp / 'r' / ident / f'{start + i:03d}.png').convert('RGBA')
d = np.load(dump)
w, h = f.size
W, Hc, cx, cz, x0, z1, res = (float(d[k]) for k in ('W', 'Hc', 'cx', 'cz', 'x0', 'z1', 'res'))
a = np.array(C.on_bg(f)).astype(float)
for zc, xc in d['smear']:
    X, Z = x0 + (xc + .5) * res, z1 - (zc + .5) * res
    px, py = (X - (cx - W / 2)) / W * w, (cz + Hc / 2 - Z) / Hc * h
    r = max(1, int(res / W * w / 2))
    ys, xs = slice(max(0, int(py) - r), int(py) + r + 1), slice(max(0, int(px) - r), int(px) + r + 1)
    a[ys, xs, :3] = a[ys, xs, :3] * .45 + np.array([255, 0, 0]) * .55
im = Image.fromarray(a.clip(0, 255).astype(np.uint8))
bb = f.getchannel('A').getbbox()
im = im.crop((bb[0] - 10, bb[1] - 10, bb[2] + 10, bb[3] + 10))
im.convert('RGB').save(out)
print(out, f"smear {row['smear_bpx']} battle px² ({row['smear_pct']}% of the figure); tearing bones {row['by_pair']}")
