#!/usr/bin/env python3
"""What a change to layers.py did to the cut: per unit, its master tinted where paint LEFT (magenta) or JOINED (cyan)
the arm layers, left (orange) or joined (green) the off-hand, and left (dark red) or joined (navy) the base, against a
backup of the cut layers. Re-cut every unit after a layers.py change and look at every unit that changed.

  mkdir -p /tmp/cut_backup && for d in artifacts/remaster/puppet_stride/*/; do mkdir -p /tmp/cut_backup/$(basename $d); \\
      cp $d/*.png $d/layers.json /tmp/cut_backup/$(basename $d)/; done        # before the change
  art/remaster/puppet/review/recut.sh all                                   # after it
  art/remaster/puppet/run.sh art/remaster/puppet/review/layer_diff.py out.png /tmp/cut_backup enemy_spitter,...
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
import common as C  # noqa: E402

out, before, ids = sys.argv[1], Path(sys.argv[2]), C.ids_arg(sys.argv[3:])
T, tiles = 400, []


def mask(p):
    return (np.array(Image.open(p))[..., 3] > 40) if p.exists() else None


for u in ids:
    mm = np.array(Image.open(C.STRIDE / u / 'master.png').convert('RGBA')).astype(float)
    al = mm[..., 3:4] / 255
    img = mm[..., :3] * al + np.array(C.BG) * (1 - al)
    for name, lost_c, gain_c in (('arm_R', (255, 0, 255), (0, 200, 255)), ('arm_L', (255, 0, 255), (0, 200, 255)),
                                 ('offhand', (255, 140, 0), (0, 220, 0)), ('base', (120, 0, 0), (0, 0, 160))):
        o, n = mask(before / u / f'{name}.png'), mask(C.STRIDE / u / f'{name}.png')
        if o is None and n is None:
            continue
        z = np.zeros(mm.shape[:2], bool)
        o, n = (z if o is None else o), (z if n is None else n)
        img[o & ~n] = img[o & ~n] * .3 + np.array(lost_c) * .7
        img[n & ~o] = img[n & ~o] * .3 + np.array(gain_c) * .7
    t = Image.fromarray(img.clip(0, 255).astype(np.uint8)).resize((T, T))
    ImageDraw.Draw(t).text((4, 4), u, fill=(0, 0, 0))
    tiles.append(t)
cols = 5
o = Image.new('RGB', (cols * T, ((len(tiles) + cols - 1) // cols) * T), (255, 255, 255))
for k, t in enumerate(tiles):
    o.paste(t, ((k % cols) * T, (k // cols) * T))
o.save(out)
print(out, o.size)
