"""Build, animate and render remastered units.

blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_units.py -- \
    --ids player_brawler [--preview] [--samples 96] [--no-blend]

--preview renders a representative subset of frames plus the portrait and a
contact sheet to artifacts/remaster/preview/<id>/. Without it, all 32 frames
are rendered to artifacts/remaster/units/<id>/ (the packing source).
"""
import argparse
import json
import sys
import time
import traceback
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import bpy  # noqa: E402

from rk import core, shaders  # noqa: E402
from rk.character import render_character  # noqa: E402
from roster import load_all  # noqa: E402

ROOT = HERE.parent.parent
parser = argparse.ArgumentParser()
parser.add_argument('--ids', default='all')
parser.add_argument('--samples', type=int, default=96)
parser.add_argument('--preview', action='store_true')
parser.add_argument('--no-blend', action='store_true')
parser.add_argument('--no-portrait', action='store_true')
parser.add_argument('--resume', action='store_true')
args = parser.parse_args(core.args_after_dashes(sys.argv))

units = json.loads((ROOT / 'data/units.json').read_text())['Units']
titles = {u['Id']: u['DisplayName'] for u in units}
registry = load_all()
ids = [u['Id'] for u in units if u['Id'] in registry] if args.ids == 'all' else args.ids.split(',')
PREVIEW_FRAMES = [1, 5, 7, 12, 14, 15, 17, 21, 24, 26, 28, 31]

for ident in ids:
    if ident not in registry:
        print('UNIT_MISSING_RECIPE ' + ident, flush=True)
        continue
    out = core.REVIEW / ('preview' if args.preview else 'units') / ident
    if args.resume and (out / 'complete.json').exists():
        print('UNIT_SKIP ' + ident, flush=True)
        continue
    t0 = time.time()
    import os
    for attempt in ('gpu', 'cpu'):
        if attempt == 'cpu':
            # The GPU is shared; fall back to CPU rendering when Metal runs out of memory.
            os.environ['RK_DEVICE'] = 'CPU'
            print('UNIT_RETRY_CPU ' + ident, flush=True)
        try:
            core.reset()
            shaders.reset_cache()
            ch, opts = registry[ident](ident, titles.get(ident, ident))
            blend = None if args.no_blend or args.preview else HERE / 'blend' / 'units' / f'{ident}.blend'
            meta = render_character(ch, out, samples=args.samples, portrait=not args.no_portrait,
                                    frames=PREVIEW_FRAMES if args.preview else None, save_blend=blend,
                                    profile=opts['profile'], portrait_cfg=opts.get('portrait_cfg'),
                                    body_z=opts.get('body_z'), min_scale=opts.get('min_scale', 3.6),
                                    cam_target_z=opts.get('cam_target_z', 1.15))
            if args.preview:
                (out / 'complete.json').unlink(missing_ok=True)
            print(f'UNIT_COMPLETE {ident} {time.time() - t0:.1f}s scale={meta["drawScale"]:.3f}', flush=True)
            break
        except Exception as exc:
            traceback.print_exc()
            if attempt == 'gpu' and 'Memory' in str(exc):
                continue
            print('UNIT_FAILED ' + ident, flush=True)
            break
    os.environ.pop('RK_DEVICE', None)
