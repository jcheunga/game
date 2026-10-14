"""Re-render a saved lookdev scene with different view and light settings (fast look calibration).

blender --background --python art/remaster/relight.py -- --blend artifacts/remaster/lookdev/blend/X.blend \
    --out out.png [--look 'AgX - Medium High Contrast'] [--view AgX] [--exposure 0.4] [--scale key=1.2,fill=0.8,...]
"""
import argparse
import sys

import bpy

argv = sys.argv[sys.argv.index('--') + 1:]
ap = argparse.ArgumentParser()
ap.add_argument('--blend', required=True)
ap.add_argument('--out', required=True)
ap.add_argument('--view', default=None)
ap.add_argument('--look', default=None)
ap.add_argument('--exposure', type=float, default=None)
ap.add_argument('--ambient', type=float, default=None)
ap.add_argument('--scale', default='')
ap.add_argument('--samples', type=int, default=32)
ap.add_argument('--sheen', type=float, default=None)
a = ap.parse_args(argv)
bpy.ops.wm.open_mainfile(filepath=a.blend)
sc = bpy.context.scene
sc.cycles.samples = a.samples
if a.view:
    sc.view_settings.view_transform = a.view
if a.look:
    try:
        sc.view_settings.look = a.look
    except TypeError:
        pass
if a.exposure is not None:
    sc.view_settings.exposure = a.exposure
if a.ambient is not None and sc.world:
    for n in sc.world.node_tree.nodes:
        if n.type == 'BACKGROUND':
            n.inputs['Strength'].default_value *= a.ambient
for item in filter(None, a.scale.split(',')):
    k, v = item.split('=')
    for o in bpy.data.objects:
        if o.type == 'LIGHT' and o.name.lower().startswith(k.lower()):
            o.data.energy *= float(v)
if a.sheen is not None:
    for m in bpy.data.materials:
        if m.node_tree:
            for n in m.node_tree.nodes:
                if n.type == 'BSDF_PRINCIPLED':
                    n.inputs['Sheen Weight'].default_value *= a.sheen
sc.render.filepath = a.out
bpy.ops.render.render(write_still=True)
