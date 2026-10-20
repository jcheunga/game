#!/usr/bin/env python3
"""Set landmarks placed by eye on a gridded copy of a painting (they win over Codex's).

  python3 art/remaster/puppet/setlm.py <unit dir> hip_R=505,540 knee_R=488,720 offhand_item=null want_held=true ...
(want_held: the cut weapon is broken, ask Codex for the weapon alone)
"""
import json
import sys
from pathlib import Path

d = Path(sys.argv[1])
p = d / 'landmarks.json'
lm = json.loads(p.read_text()) if p.exists() else {'kind': 'humanoid', 'facing': 'right'}
for kv in [a for a in sys.argv[2:] if not a.startswith('--')]:
    k, v = kv.split('=', 1)
    if k == 'carried':          # carried=x,y;x,y : points on things the body carries (restored to it)
        lm[k] = [[int(float(c)) for c in pt.split(',')] for pt in v.split(';') if pt and pt != 'none']
        continue
    try:
        lm[k] = None if v == 'null' else (v == 'true') if v in ('true', 'false') else \
            float(v) if ',' not in v and '.' in v else [int(round(float(x))) for x in v.split(',')]
    except ValueError:          # a word (arm_cloth=R)
        lm[k] = v
anat = next((a for a in sys.argv[2:] if a.startswith('--anatomy')), None)
if anat:
    # hips from the legs: hip joint to knee is as long as knee to ankle (Drillis & Contini: 0.245 H and 0.246 H). Each
    # leg puts the hip a shin's length from its knee, under the pelvis; the more upright leg says it best (a trailing
    # leg's knee is far behind the pelvis, and a thigh that long cannot reach it), so the hips sit at the higher of
    # the two. A hip at the skirt's hem (where Codex puts it) makes a leg that pivots at the knee guard: the walk
    # reads as short legs squatting. --anatomy=R|L trusts only that leg.
    pel = lm['pelvis']
    only = anat.split('=')[1] if '=' in anat else None
    ys = {}
    for s_ in 'RL':
        k, a = lm.get('knee_' + s_), lm.get('ankle_' + s_)
        if not (k and a) or (only and s_ != only):
            continue
        shin = ((k[0] - a[0]) ** 2 + (k[1] - a[1]) ** 2) ** 0.5
        dx = abs(pel[0] - k[0])
        # (a knee much further out than a thigh's slant allows says nothing reliable about the hip)
        ys[s_] = (k[1] - max(shin * shin - dx * dx, (0.6 * shin) ** 2) ** 0.5, dx / max(1.0, shin))
    good = [y for y, r in ys.values() if r < 0.85] or [min(ys.values(), key=lambda v: v[1])[0]]
    hy = sum(good) / len(good)
    # these figures stand 5.5-6.5 heads tall: the hip joint sits a third to a half of the height above the ground
    ground = max(lm[k][1] for k in ('toe_R', 'toe_L', 'ankle_R', 'ankle_L') if lm.get(k))
    H = ground - lm['head_top'][1]
    hy = min(ground - 0.33 * H, max(ground - 0.5 * H, hy))
    for s_ in 'RL':
        lm['hip_' + s_] = [int(round(pel[0])), int(round(hy))]
    lm['hips_from'] = ''.join(s_ for s_, (y, r) in ys.items() if r < 0.85) or 'fallback'
lm['located_by'] = 'claude'
p.write_text(json.dumps(lm, indent=1) + '\n')
print(json.dumps(lm))
