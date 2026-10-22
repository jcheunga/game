#!/usr/bin/env python3
"""Old master (puppet/, blue-grey) beside the side-on repaint (puppet_stride/, warm grey) for many units: check a
repaint kept the design and the ANATOMY (a repaint once gave the Drowned Colossus four arms), and that no arm is
painted folded with its fist against its own shoulder (it smears when the rig opens it).

  art/remaster/puppet/run.sh art/remaster/puppet/review/masters_sheet.py out.png all
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
import common as C  # noqa: E402

out, ids = sys.argv[1], [i for i in C.ids_arg(sys.argv[2:]) if (C.STRIDE / i / 'master.png').exists()]
T = 260
o = Image.new('RGB', (T * 2 * 4 + 30, T * ((len(ids) + 3) // 4)), C.PAGE)
d = ImageDraw.Draw(o)
for k, u in enumerate(ids):
    x0, y0 = (k % 4) * (T * 2 + 10), (k // 4) * T
    for j, p in enumerate((C.PUPPET / u / 'master.png', C.STRIDE / u / 'master.png')):
        if p.exists():
            im = Image.open(p).convert('RGBA').resize((T, T), Image.LANCZOS)
            o.paste(C.on_bg(im, C.BG if j else (190, 195, 205)).convert('RGB'), (x0 + j * T, y0))
    d.text((x0 + 4, y0 + 4), C.short(u), fill=(0, 0, 0))
o.save(out)
print(out, o.size, len(ids), 'units')
