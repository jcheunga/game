"""Shared icon stage: camera-relative lighting rig, auto-framing camera, render and post (bloom)."""
import math
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector

from rk import core


def cam_basis(az, el):
    """Camera direction (from target to camera) and its right/up vectors. az 0 = camera on -Y."""
    a, e = math.radians(az), math.radians(el)
    back = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))).normalized()
    right = Vector((0, 0, 1)).cross(back).normalized()
    up = back.cross(right).normalized()
    return back, right, up


def world(scene, strength=1.0, diffuse=0.33):
    """Product-shot environment by elevation: warm floor bounce, bright softbox band, cool sky."""
    w = scene.world
    w.use_nodes = True
    nt = w.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputWorld')
    bg = nt.nodes.new('ShaderNodeBackground')
    coord = nt.nodes.new('ShaderNodeTexCoord')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    mr = nt.nodes.new('ShaderNodeMapRange')
    mr.inputs['From Min'].default_value = -1.0
    mr.inputs['From Max'].default_value = 1.0
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    nt.links.new(coord.outputs['Generated'], sep.inputs[0])
    nt.links.new(sep.outputs['Z'], mr.inputs['Value'])
    nt.links.new(mr.outputs['Result'], ramp.inputs['Fac'])
    els = ramp.color_ramp.elements
    # fac 0.5 = horizon
    stops = [(0.0, '201a15'), (0.33, '4d4034'), (0.47, '8f8170'), (0.56, 'e2d7c4'), (0.68, 'fbf3e6'), (0.8, 'a4b4c6'),
             (1.0, '46566a')]
    while len(els) < len(stops):
        els.new(0.5)
    for el, (p, c) in zip(els, stops):
        el.position = p
        el.color = core.srgb(c)
    nt.links.new(ramp.outputs['Color'], bg.inputs['Color'])
    # bright studio for reflections, dimmer for diffuse bounce so matte materials keep their value
    lp = nt.nodes.new('ShaderNodeLightPath')
    mix = nt.nodes.new('ShaderNodeMix')
    mix.data_type = 'FLOAT'
    nt.links.new(lp.outputs['Is Glossy Ray'], mix.inputs[0])
    mix.inputs[2].default_value = strength * diffuse
    mix.inputs[3].default_value = strength
    nt.links.new(mix.outputs[0], bg.inputs['Strength'])
    nt.links.new(bg.outputs[0], out.inputs[0])


def rig(scene, coll, az=22.0, el=18.0, key=1.0, rim=1.0, fill=1.0, key_color='ffe0b8', rim_color='9ed4ff',
        rim2_color='ffc68a', world_strength=1.0, target=(0, 0, 0)):
    """Warm key (upper left front), cool rim (back right), warm kicker (back left), soft fill."""
    world(scene, world_strength)
    back, right, up = cam_basis(az, el)
    t = Vector(target)
    D = 7.0
    lights = []

    def put(name, vec, power, color, size, diffuse=True):
        loc = t + vec.normalized() * D
        lt = core.area_light(name, loc, t, power, core.srgb(color), size, coll)
        lt.visible_diffuse = diffuse
        lights.append(lt)
    put('Key warm', back * 0.9 - right * 0.95 + up * 1.15, 680 * key, key_color, 4.0)
    put('Rim cool', -back * 0.95 + right * 1.0 + up * 0.55, 1500 * rim, rim_color, 2.5)
    put('Kicker warm', -back * 0.9 - right * 1.1 + up * 0.25, 600 * rim, rim2_color, 2.5)
    put('Fill', back * 1.0 + right * 0.6 - up * 0.25, 110 * fill, 'c8d6ff', 6.0)
    put('Reflector', back * 1.0 - right * 0.25 + up * 0.55, 520 * fill, 'fff1dc', 7.0, diffuse=False)
    put('Top', up * 1.0 + back * 0.2, 170 * key, 'fff4e6', 4.0)
    return lights


def _points(objs, dg, cap=6000):
    pts = []
    for o in objs:
        if o.type != 'MESH' or o.get('nofit') or o.hide_render:
            continue
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        mw = o.matrix_world
        n = len(me.vertices)
        step = max(1, n // cap)
        co = np.empty(n * 3, dtype=np.float32)
        me.vertices.foreach_get('co', co)
        co = co.reshape(-1, 3)[::step]
        m = np.array(mw)
        co = co @ m[:3, :3].T + m[:3, 3]
        pts.append(co)
        ev.to_mesh_clear()
    return np.concatenate(pts) if pts else np.zeros((1, 3))


def fit_camera(scene, cam, objs, az=22.0, el=18.0, fill=0.8, lens=70.0, shift=(0.0, 0.0), iters=7, aspect_bias=1.0):
    """Perspective camera along a fixed direction, distance and aim solved so the silhouette
    (all non-`nofit` meshes) fills `fill` of the square frame, centred (+shift in frame units)."""
    dg = bpy.context.evaluated_depsgraph_get()
    pts = _points(objs, dg)
    back, right, up = cam_basis(az, el)
    cam.data.type = 'PERSP'
    cam.data.lens = lens
    cam.data.sensor_fit = 'HORIZONTAL'
    cam.data.sensor_width = 36
    half = math.atan(18 / lens)
    center = Vector(((pts[:, 0].min() + pts[:, 0].max()) / 2, (pts[:, 1].min() + pts[:, 1].max()) / 2,
                     (pts[:, 2].min() + pts[:, 2].max()) / 2))
    radius = float(np.linalg.norm(pts - np.array(center), axis=1).max())
    dist = radius / math.tan(half) * 1.2 + 0.1
    for _ in range(iters):
        loc = center + back * dist
        fwd = -back
        rel = pts - np.array(loc)
        z = rel @ np.array(fwd)
        x = (rel @ np.array(right)) / z
        y = (rel @ np.array(up)) / z
        tanh = math.tan(half)
        x0, x1, y0, y1 = x.min(), x.max(), y.min(), y.max()
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        ext = max((x1 - x0) * aspect_bias, y1 - y0) / (2 * tanh)
        center = center + right * (cx * dist) + up * (cy * dist)
        dist = dist * ext / fill
    # optional framing shift: move the subject within the frame
    loc = center + back * dist
    center = center - right * (shift[0] * 2 * math.tan(half) * dist) - up * (shift[1] * 2 * math.tan(half) * dist)
    loc = center + back * dist
    cam.location = loc
    cam.rotation_euler = (-back).to_track_quat('-Z', 'Y').to_euler()
    cam.data.clip_start = max(0.01, dist * 0.02)
    cam.data.clip_end = dist * 10
    return center, dist


# ------------------------------------------------------------------ post process (numpy)
def _box_blur(a, r):
    if r < 1:
        return a
    r = int(r)
    out = a
    for axis in (0, 1):
        pad = [(0, 0)] * out.ndim
        pad[axis] = (r + 1, r)
        p = np.pad(out, pad, mode='constant')
        c = np.cumsum(p, axis=axis, dtype=np.float64)
        n = out.shape[axis]
        hi = np.take(c, np.arange(2 * r + 1, 2 * r + 1 + n), axis=axis)
        lo = np.take(c, np.arange(0, n), axis=axis)
        out = ((hi - lo) / (2 * r + 1)).astype(np.float32)
    return out


def gblur(a, sigma):
    """Approximate gaussian via three box blurs."""
    if sigma <= 0.3:
        return a
    w = math.sqrt(12 * sigma * sigma / 3 + 1)
    r = max(1, int((w - 1) / 2))
    out = a
    for _ in range(3):
        out = _box_blur(out, r)
    return out


def load_rgba(path):
    img = bpy.data.images.load(str(path), check_existing=False)
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(h, w, 4)


def save_rgba(arr, path):
    h, w, _ = arr.shape
    img = bpy.data.images.new('post', w, h, alpha=True)
    img.alpha_mode = 'STRAIGHT'
    img.pixels.foreach_set(np.clip(arr, 0, 1).astype(np.float32).ravel())
    img.filepath_raw = str(path)
    img.file_format = 'PNG'
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    img.save()
    bpy.data.images.remove(img)


def downsample(arr, factor):
    if factor <= 1:
        return arr
    h, w, c = arr.shape
    a = arr[:h - h % factor, :w - w % factor]
    # average in premultiplied space
    pm = a.copy()
    pm[..., :3] *= pm[..., 3:4]
    pm = pm.reshape(h // factor, factor, w // factor, factor, c).mean(axis=(1, 3))
    out = pm.copy()
    al = np.maximum(out[..., 3:4], 1e-6)
    out[..., :3] = np.where(out[..., 3:4] > 1e-6, out[..., :3] / al, 0)
    return out


def post(raw, out, bloom=0.6, threshold=0.62, radii=(3, 9, 24), weights=(0.5, 0.35, 0.25), shadow=0.35,
         shadow_sigma=5.0, ss=1, saturation_boost=1.0, glow_tint=None):
    """Bloom bright pixels (alpha-aware) and add a faint dark separation shadow; downsample by ss."""
    a = load_rgba(raw)
    a = downsample(a, ss)
    rgb, al = a[..., :3], a[..., 3]
    pm = rgb * al[..., None]
    lum = rgb @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    mx = rgb.max(axis=2)
    bright = np.clip((np.maximum(lum, mx * 0.85) - threshold) / (1 - threshold), 0, 1) ** 1.4 * al
    src = pm * bright[..., None]
    if glow_tint is not None:
        tint = np.array(core.srgb(glow_tint)[:3], dtype=np.float32) ** (1 / 2.2)
        src = src * 0.5 + bright[..., None] * tint * 0.5
    scale = a.shape[0] / 512.0
    glow = np.zeros_like(src)
    for r, wgt in zip(radii, weights):
        glow += gblur(src, r * scale) * wgt
    glow *= bloom
    if saturation_boost != 1.0:
        g = glow.mean(axis=2, keepdims=True)
        glow = np.clip(g + (glow - g) * saturation_boost, 0, None)
    # alpha-aware additive glow
    g_alpha = np.clip(glow.max(axis=2) * 1.15, 0, 1)
    out_a = al + g_alpha * (1 - al)
    out_pm = pm + glow
    # separation shadow underneath everything (very soft, dark)
    if shadow:
        sh = np.clip(gblur(out_a, shadow_sigma * scale) * shadow, 0, 1)
        fa = out_a + sh * (1 - out_a)
        out_pm = out_pm  # shadow is black: adds alpha only
        out_a = fa
    res = np.zeros_like(a)
    safe = np.maximum(out_a, 1e-6)
    res[..., :3] = np.clip(out_pm / safe[..., None], 0, 1)
    res[..., 3] = np.clip(out_a, 0, 1)
    save_rgba(res, out)
    return out
