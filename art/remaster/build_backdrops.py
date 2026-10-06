"""Build the layered zone battle backdrops: a far vista, a mid-distance skyline and the near street, each its own
Blender scene, framed onto the battle world (see battle_backdrops/frame.py for the contract).

blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_backdrops.py -- \
    [--zones all|city,harbor] [--layers far,mid,near] [--samples 64] [--scale 1.0] [--out DIR]

Writes <out>/<zone>_far.png, <zone>_mid.png, <zone>.png (near), <zone>.json and preview composites
<out>/preview/<zone>-<stop>.png at the camera's left, centre and right stops. --scale < 1 renders smaller drafts.
Each layer renders once to a multilayer EXR (for its mist pass) and a view-transformed PNG; post adds distance
fog toward the sky's horizon colour and grades. Default out: artifacts/remaster/backdrops/.
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
import numpy as np  # noqa: E402
import OpenImageIO as oiio  # noqa: E402

from rk import core, env, post  # noqa: E402
from battle_backdrops import frame as F  # noqa: E402
from battle_backdrops import common as CM  # noqa: E402
from battle_backdrops import ZONES  # noqa: E402

OUT = core.REVIEW / 'backdrops'
ORDER = ('far', 'mid', 'near')


def make_layer(name, overrides, scale):
    o = dict(overrides.get(name, {}))
    if name == 'far':
        a = dict(parallax=0.15, horizon=246.0, gy0=-150.0, gy1=332.0, res_x=3200, fov=40.0, cam_h=10.0)
        a.update(o)
        a['res_x'] = int(a['res_x'] * scale)
        return F.Persp('far', **a)
    if name == 'mid':
        a = dict(parallax=0.45, pitch=10.0, line=298.0, gy0=100.0, gy1=346.0, res_x=3840)
        a.update(o)
        a['res_x'] = int(a['res_x'] * scale)
        return F.Ortho('mid', **a)
    a = dict(res_x=5120)
    a.update(o)
    a['res_x'] = int(a['res_x'] * scale)
    return F.Near(**a)


def set_format(scene, multilayer):
    f = scene.render.image_settings
    if multilayer:
        f.media_type = 'MULTI_LAYER_IMAGE'
        f.file_format = 'OPEN_EXR_MULTILAYER'
        f.color_depth = '32'
        f.exr_codec = 'ZIP'
    else:
        f.media_type = 'IMAGE'
        f.file_format = 'PNG'
        f.color_mode = 'RGBA'
        f.color_depth = '16'


def render(scene, L, raw):
    """One render: the mist pass to EXR, the view-transformed beauty to PNG."""
    vl = scene.view_layers[0]
    vl.use_pass_mist = True
    ms = scene.world.mist_settings
    ms.start, ms.falloff = 0.0, 'LINEAR'
    ms.depth = 12000.0 if L.kind == 'persp' else L.cam_dist + 1200.0
    raw.parent.mkdir(parents=True, exist_ok=True)
    set_format(scene, True)
    scene.render.filepath = str(raw.with_suffix('.exr'))
    try:
        bpy.ops.render.render(write_still=True)
    except RuntimeError as err:
        if scene.cycles.device != 'GPU' or 'emory' not in str(err):
            raise
        print('RENDER_GPU_OOM falling back to CPU', flush=True)
        scene.cycles.device = 'CPU'
        bpy.ops.render.render(write_still=True)
    set_format(scene, False)
    bpy.data.images['Render Result'].save_render(str(raw.with_suffix('.png')), scene=scene)
    return ms.depth


def read_rgba(path):
    buf = oiio.ImageBuf(str(path))
    a = np.asarray(buf.get_pixels(oiio.FLOAT), dtype=np.float32)
    if a.shape[2] == 3:
        a = np.concatenate([a, np.ones(a.shape[:2] + (1,), np.float32)], 2)
    return a


def read_mist(path):
    inp = oiio.ImageInput.open(str(path))
    i = 0
    try:
        while inp.seek_subimage(i, 0):
            names = list(inp.spec().channelnames)
            if any('Mist' in n for n in names):
                return np.asarray(inp.read_image(i, 0, 0, 1, oiio.FLOAT), dtype=np.float32).reshape(
                    inp.spec().height, inp.spec().width)
            i += 1
    finally:
        inp.close()
    raise RuntimeError('no mist pass in ' + str(path))


def write_rgba(a, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    h, w = a.shape[:2]
    data = np.clip(np.round(a * 255.0), 0, 255).astype(np.uint8)
    out = oiio.ImageOutput.create(str(path))
    out.open(str(path), oiio.ImageSpec(w, h, 4, oiio.UINT8))
    out.write_image(data)
    out.close()


def hexc(h):
    return np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)], np.float32)


def grade(rgb, saturation=1.0, contrast=1.0, split=0.1, shadows='18222a', highlights='ffe2b0', lift=0.0, exposure=1.0):
    rgb = rgb * exposure
    lum_w = np.array([0.2126, 0.7152, 0.0722], np.float32)
    if split:
        t = np.clip(rgb @ lum_w, 0, 1)[..., None]
        tone = hexc(shadows) * (1 - t) + hexc(highlights) * t
        rgb = rgb * (1 - split) + rgb * tone * 1.6 * split
    if lift:
        rgb = rgb + lift * (1 - rgb)
    if contrast != 1.0:
        rgb = (rgb - 0.5) * contrast + 0.5
    if saturation != 1.0:
        lum = (rgb @ lum_w)[..., None]
        rgb = lum + (rgb - lum) * saturation
    return np.clip(rgb, 0, 1)


def bleed(a):
    """Spread colour under transparent pixels so filtered edges never fringe dark."""
    alpha = a[..., 3:4]
    pre = a[..., :3] * alpha
    w = a.shape[1]
    for sigma in (w * 0.0015, w * 0.006, w * 0.02):
        sp, sa = post.blur(pre, sigma), post.blur(alpha, sigma)
        fill = sp / np.maximum(sa, 1e-4)
        # only fully transparent pixels: smoke and soft edges keep their own colour
        hole = (alpha < 0.004) & (sa > 1e-3)
        a[..., :3] = np.where(hole, fill, a[..., :3])
    return a


def nearest_depth(dist, alpha, radius=3):
    """Silhouette pixels mix their object's mist with the empty sky's; give them the nearest solid depth instead,
    so distance fog never rims an edge with haze."""
    solid = np.where(alpha > 0.98, dist, np.inf)
    best = solid.copy()
    h, w = dist.shape
    for dy in range(-radius, radius + 1):
        for dx in range(-radius, radius + 1):
            shifted = np.full_like(solid, np.inf)
            shifted[max(0, dy):h + min(0, dy), max(0, dx):w + min(0, dx)] = \
                solid[max(0, -dy):h + min(0, -dy), max(0, -dx):w + min(0, -dx)]
            best = np.minimum(best, shifted)
    return np.where(alpha > 0.98, dist, np.where(np.isfinite(best), best, dist))


def finish(L, raw, dst, fog_color, fog, look):
    img = read_rgba(raw.with_suffix('.png'))
    dist = read_mist(raw.with_suffix('.exr')) * fog['depth']
    if L.kind != 'persp':
        dist = nearest_depth(dist, img[..., 3])
    if L.kind == 'persp':
        f = (1 - np.exp(-dist / fog['far_dist'])) * fog['far_max']
        f = np.where(dist >= fog['depth'] * 0.999, 0.0, f)       # the sky itself
    else:
        base, gain, top = fog[L.name]
        f = np.clip(base + gain * (dist - L.cam_dist), 0, top)
    f = f[..., None].astype(np.float32)
    img[..., :3] = img[..., :3] * (1 - f) + fog_color * f
    g = dict(look.get(L.name, {}))
    bloom = g.pop('bloom', 0.0)
    img[..., :3] = grade(img[..., :3], **g)
    if bloom:
        lum = img[..., :3] @ np.array([0.2126, 0.7152, 0.0722], np.float32)
        hi = np.clip((lum - 0.62) / 0.38, 0, 1)[..., None] * img[..., :3] * img[..., 3:4]
        w = img.shape[1]
        glow = post.blur(hi, w * 0.006) * 0.6 + post.blur(hi, w * 0.025) * 0.4
        img[..., :3] = 1 - (1 - img[..., :3]) * (1 - np.clip(glow * bloom * 2.2, 0, 1))
    if L.kind != 'persp':
        img = bleed(img)
    write_rgba(img, dst)
    return img


def horizon_color(L, raw, fallback):
    """Mean sky colour just above the horizon in the far render: the colour distance fades toward."""
    img = read_rgba(raw.with_suffix('.png'))
    mist = read_mist(raw.with_suffix('.exr'))
    h = img.shape[0]
    row = int((L.horizon - L.gy0) / (L.gy1 - L.gy0) * h)
    band = slice(max(0, row - int(h * 0.08)), max(1, row - int(h * 0.01)))
    sky = mist[band] >= 0.999
    if sky.sum() < 200:
        return hexc(fallback)
    return img[band][..., :3][sky].mean(0)


# ------------------------------------------------------------------ previews (game framing, numpy only)
DESKTOP = dict(size=(1280, 720), zoom=1280 / F.combat()['ViewWidth'], band_row=92 + 0.73 * (720 - 168 - 92))


def composite(layers, cx, frame=DESKTOP, scale=0.5):
    w, h = int(frame['size'][0] * scale), int(frame['size'][1] * scale)
    z = frame['zoom'] * scale
    band_c, world_c = layers['band_c'], layers['world_c']
    ys = band_c + (np.arange(h) + 0.5 - frame['band_row'] * scale) / z
    out = np.zeros((h, w, 3), np.float32)
    out[:] = layers['sky']
    for img, (gx0, gy0, gw, gh), par in layers['stack']:
        off = (1 - par) * (cx - world_c)
        xs = cx + (np.arange(w) + 0.5 - w / 2) / z - off
        u = ((xs - gx0) / gw * img.shape[1]).astype(int)
        v = ((ys - gy0) / gh * img.shape[0]).astype(int)
        uu, vv = np.meshgrid(np.clip(u, 0, img.shape[1] - 1), np.clip(v, 0, img.shape[0] - 1))
        inside = ((u >= 0) & (u < img.shape[1]))[None, :] & ((v >= 0) & (v < img.shape[0]))[:, None]
        px = img[vv, uu]
        a = px[..., 3:4] * inside[..., None]
        out = out * (1 - a) + px[..., :3] * a
    for row in (layers['top'], layers['bottom']):
        r = int((row - band_c) * z + frame['band_row'] * scale)
        if 0 <= r < h:
            out[r, ::4] = (1, 1, 1)
    return out


def build(zone, samples, scale, out, only, repost=False):
    t0 = time.time()
    mod = ZONES[zone]
    overrides = getattr(mod, 'LAYERS', {})
    fog = dict(depth=0.0, far_dist=1600.0, far_max=0.9, mid=(0.22, 0.004, 0.6), near=(0.0, 0.003, 0.25))
    fog.update(getattr(mod, 'FOG', {}))
    look = getattr(mod, 'LOOK', {})
    manifest = {}
    fog_color = hexc(getattr(mod, 'MOOD', {}).get('horizon', 'f4c48a'))
    meta_path = out / f'{zone}.json'
    if meta_path.exists():
        prev = json.loads(meta_path.read_text())
        if 'fog' in prev:
            fog_color = np.array(prev['fog'], np.float32)
        manifest = {l['name']: l for l in prev.get('layers', []) if 'name' in l}
    for name in ORDER:
        if only and name not in only:
            continue
        L = make_layer(name, overrides, scale)
        raw = out / 'raw' / f'{zone}_{name}'
        if repost:
            # finish again from the saved renders (grade or post-process changes only)
            mood = dict(horizon=getattr(mod, 'MOOD', {}).get('horizon', 'f4c48a'))
            fog['depth'] = 12000.0 if L.kind == 'persp' else L.cam_dist + 1200.0
        else:
            scene = env.menu_scene(samples=samples)
            mood = CM.light(scene, name, getattr(mod, 'MOOD', {}))
            getattr(mod, name)(L, scene, mood)
            L.camera(scene)
            if L.kind != 'persp':
                scene.render.film_transparent = True
                scene.render.image_settings.color_mode = 'RGBA'
            fog['depth'] = render(scene, L, raw)
        if name == 'far':
            fog_color = horizon_color(L, raw, mood['horizon'])
        file = f'{zone}.png' if name == 'near' else f'{zone}_{name}.png'
        finish(L, raw, out / file, fog_color, fog, look)
        entry = L.manifest(file)
        entry['name'] = name
        manifest[name] = entry
        print(f'BACKDROP_LAYER {zone} {name} {L.res[0]}x{L.res[1]} {time.time() - t0:.1f}s', flush=True)
    near = make_layer('near', overrides, scale)
    layers = [manifest[n] for n in ORDER if n in manifest]
    far_img = read_rgba(out / manifest['far']['file']) if 'far' in manifest else None
    sky = far_img[2, :, :3].mean(0) if far_img is not None else fog_color
    meta = {'layers': layers, 'rect': [round(v, 3) for v in near.rect()], 'sky': ''.join(f'{int(c * 255):02x}' for c in sky),
            'fog': [float(c) for c in fog_color], 'generator': 'art/remaster/build_backdrops.py'}
    meta_path.write_text(json.dumps(meta, indent=2) + '\n')
    if len(layers) == 3:
        stack = [(read_rgba(out / l['file']), l['rect'], l['parallax']) for l in layers]
        comp = dict(stack=stack, sky=sky, band_c=near.band_c, world_c=near.world_w / 2, top=near.top_row,
                    bottom=near.bottom_row)
        for stop, cx in (('left', 237.0), ('centre', near.world_w / 2), ('right', near.world_w - 237.0)):
            img = composite(comp, cx)
            write_rgba(np.concatenate([img, np.ones(img.shape[:2] + (1,), np.float32)], 2), out / 'preview' / f'{zone}-{stop}.png')
    return time.time() - t0


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--zones', default='all')
    p.add_argument('--layers', default='')
    p.add_argument('--samples', type=int, default=64)
    p.add_argument('--scale', type=float, default=1.0)
    p.add_argument('--out', default=str(OUT))
    p.add_argument('--repost', action='store_true', help='re-run post-processing on the saved raw renders')
    args = p.parse_args(core.args_after_dashes(sys.argv))
    zones = list(ZONES) if args.zones == 'all' else [z.strip() for z in args.zones.split(',') if z.strip()]
    only = [s.strip() for s in args.layers.split(',') if s.strip()]
    out = Path(args.out)
    failed = []
    for zone in zones:
        try:
            print(f'BACKDROP_COMPLETE {zone} {build(zone, args.samples, args.scale, out, only, args.repost):.1f}s', flush=True)
        except Exception:
            traceback.print_exc()
            failed.append(zone)
            print('BACKDROP_FAILED ' + zone, flush=True)
    if failed:
        sys.exit(1)


main()
