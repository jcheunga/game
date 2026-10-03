"""Particle-sprite authoring: render a sprite in Blender (orthographic, top-down, XY plane)
and turn the float render into a tintable, straight-alpha white/greyscale PNG.

Two passes are supported:
  * lit   - geometry or scattering volumes lit by lights; alpha = coverage, RGB = greyscale shading
  * glow  - emission on black; alpha = brightness, RGB = white
The passes are merged (glow under/over lit), optionally bloomed, and faded to a fully
transparent border so no square edges ever show in game.
"""
import math
from pathlib import Path

import bpy
import numpy as np
import OpenImageIO as oiio

from . import core
from .nodekit import material

LIT = 'FX lit'
GLOW = 'FX glow'


# ------------------------------------------------------------------ scene
def sprite_scene(res=512, samples=64):
    scene = core.reset()
    core.setup_render(scene, samples=samples, transparent=True, look='None')
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    scene.cycles.use_denoising = False
    scene.cycles.adaptive_threshold = 0.005
    scene.cycles.max_bounces = 6
    scene.cycles.volume_bounces = 2
    scene.cycles.sample_clamp_indirect = 0
    scene.render.resolution_x = res
    scene.render.resolution_y = res
    scene.render.filter_size = 1.0
    scene.render.image_settings.file_format = 'OPEN_EXR'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.render.image_settings.color_depth = '32'
    world = scene.world
    world.use_nodes = True
    nt = world.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputWorld')
    bg = nt.nodes.new('ShaderNodeBackground')
    bg.inputs['Color'].default_value = (1, 1, 1, 1)
    bg.inputs['Strength'].default_value = 0.0
    nt.links.new(bg.outputs[0], out.inputs[0])
    lit = core.collection(LIT)
    glow = core.collection(GLOW)
    rig = core.collection('FX rig')
    cam_data = bpy.data.cameras.new('Sprite camera')
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = 2.0
    cam_data.clip_start = 0.1
    cam_data.clip_end = 40
    cam = bpy.data.objects.new('Sprite camera', cam_data)
    rig.objects.link(cam)
    cam.location = (0, 0, 10)
    cam.rotation_euler = (0, 0, 0)
    scene.camera = cam
    return scene, lit, glow, rig


def world_fill(scene, strength=0.25, color=(1, 1, 1, 1)):
    bg = scene.world.node_tree.nodes['Background']
    bg.inputs['Color'].default_value = color
    bg.inputs['Strength'].default_value = strength


def emit_mat(name, strength=1.0, transparent=True, falloff=None):
    """Emission that does not register coverage (so it is treated as glow).
    falloff=(r0, r1, power): brightness ramps down with distance from the origin."""
    m, n = material(name)
    s = strength
    if falloff is not None:
        r0, r1, pw = falloff
        d = n.vmath('LENGTH', n.tc('Object'))
        f = n.math('POWER', n.maprange(d, r0, r1, 1.0, 0.0), pw)
        s = n.mul(f, strength)
    e = n.emission((1, 1, 1, 1), s)
    n.surface(n.add_shader(n.transparent(), e) if transparent else e)
    return m


def lit_mat(name, base=0.85, rough=0.45, spec=0.5, emit=0.0, coat=0.0):
    m, n = material(name)
    kw = {'Base Color': (base, base, base, 1), 'Roughness': rough, 'Specular IOR Level': spec}
    if emit:
        kw.update({'Emission Color': (1, 1, 1, 1), 'Emission Strength': emit})
    if coat:
        kw.update({'Coat Weight': coat, 'Coat Roughness': 0.08})
    n.principled(**kw)
    return m


def show_only(coll_name):
    for c in bpy.context.scene.collection.children:
        if c.name in (LIT, GLOW):
            c.hide_render = c.name != coll_name


def render_pass(scene, coll_name, path):
    show_only(coll_name)
    core.render_to(scene, path)
    for c in bpy.context.scene.collection.children:
        c.hide_render = False
    return read_exr(path)


# ------------------------------------------------------------------ image io
def read_exr(path):
    buf = oiio.ImageBuf(str(path))
    arr = np.asarray(buf.get_pixels(oiio.FLOAT), dtype=np.float32)
    if arr.shape[2] == 3:
        arr = np.concatenate([arr, np.ones(arr.shape[:2] + (1,), np.float32)], 2)
    return arr


def write_png(arr, path):
    """arr: HxWx4 float straight alpha in display space [0,1]."""
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    h, w = arr.shape[:2]
    data = np.clip(np.round(arr * 255.0), 0, 255).astype(np.uint8)
    spec = oiio.ImageSpec(w, h, arr.shape[2], oiio.UINT8)
    spec.attribute('oiio:UnassociatedAlpha', 1)
    out = oiio.ImageOutput.create(str(path))
    out.open(str(path), spec)
    out.write_image(data)
    out.close()


def read_png(path):
    buf = oiio.ImageBuf(str(path))
    buf.specmod().attribute('oiio:UnassociatedAlpha', 1)
    return np.asarray(buf.get_pixels(oiio.FLOAT), dtype=np.float32)


# ------------------------------------------------------------------ image maths
def downsample(arr, f):
    if f == 1:
        return arr
    h, w, c = arr.shape
    return arr.reshape(h // f, f, w // f, f, c).mean(axis=(1, 3))


def to_srgb(x):
    x = np.clip(x, 0.0, None)
    return np.where(x <= 0.0031308, x * 12.92, 1.055 * np.power(x, 1 / 2.4) - 0.055)


def blur(a, sigma):
    if sigma <= 0:
        return a
    r = int(math.ceil(sigma * 3))
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2)
    k /= k.sum()
    pad = np.pad(a, ((r, r), (r, r)), mode='constant')
    tmp = np.zeros_like(pad)
    for i, w in enumerate(k):
        tmp[:, r:-r] += w * pad[:, i:i + pad.shape[1] - 2 * r]
    out = np.zeros_like(pad)
    for i, w in enumerate(k):
        out[r:-r, :] += w * tmp[i:i + pad.shape[0] - 2 * r, :]
    return out[r:-r, r:-r]


def edge_fade(shape, inner=0.86, outer=0.985, round_=False):
    h, w = shape
    y, x = np.mgrid[0:h, 0:w]
    u = (x + 0.5) / w * 2 - 1
    v = (y + 0.5) / h * 2 - 1
    d = np.sqrt(u * u + v * v) if round_ else np.maximum(np.abs(u), np.abs(v))
    t = np.clip((outer - d) / (outer - inner), 0, 1)
    return t * t * (3 - 2 * t)


def compose(lit=None, glow=None, size=128, glow_gain=1.0, glow_gamma=1.0, lit_gain=1.0, lit_floor=0.0,
            lit_alpha_gamma=1.0, lit_alpha_gain=1.0, bloom=(), bloom_from='both', glow_over=False,
            fade=(0.86, 0.985), fade_round=False, alpha_cap=1.0):
    """Merge render passes into a straight-alpha sprite.

    lit:   premultiplied linear RGBA (coverage alpha)  -> greyscale shaded layer
    glow:  premultiplied linear RGBA (emission on black) -> white layer, alpha = brightness
    bloom: list of (sigma_px, strength) halos derived from the combined alpha, added as white glow.
    """
    layers = []
    f = None
    for src in (lit, glow):
        if src is not None:
            f = src.shape[0] // size
    a_lit = np.zeros((size, size), np.float32)
    rgb_lit = np.ones((size, size), np.float32)
    if lit is not None:
        d = downsample(lit, f)
        cov = np.clip(d[..., 3], 0, 1)
        lum = (0.2126 * d[..., 0] + 0.7152 * d[..., 1] + 0.0722 * d[..., 2])
        straight = np.where(cov > 1e-4, lum / np.maximum(cov, 1e-4), 0)
        shade = to_srgb(straight * lit_gain)
        rgb_lit = np.clip(lit_floor + (1 - lit_floor) * shade, 0, 1)
        a_lit = np.clip(np.power(np.clip(cov * lit_alpha_gain, 0, 1), lit_alpha_gamma), 0, 1)
    a_glow = np.zeros((size, size), np.float32)
    if glow is not None:
        d = downsample(glow, f)
        e = np.max(d[..., :3], axis=2) * glow_gain
        a_glow = np.power(np.clip(e, 0, 1), 1.0 / glow_gamma)
    # bloom halo (white)
    base = {'both': np.maximum(a_lit, a_glow), 'glow': a_glow, 'lit': a_lit}[bloom_from]
    a_bloom = np.zeros_like(base)
    for sigma, strength in bloom:
        a_bloom = a_bloom + blur(base, sigma) * strength
    a_bloom = np.clip(a_bloom, 0, 1)
    # white layers: glow + bloom (screen)
    a_white = 1 - (1 - a_glow) * (1 - a_bloom)
    if glow_over:
        top_a, top_rgb, bot_a, bot_rgb = a_white, np.ones_like(rgb_lit), a_lit, rgb_lit
    else:
        top_a, top_rgb, bot_a, bot_rgb = a_lit, rgb_lit, a_white, np.ones_like(rgb_lit)
    a = top_a + bot_a * (1 - top_a)
    rgb = np.where(a > 1e-5, (top_rgb * top_a + bot_rgb * bot_a * (1 - top_a)) / np.maximum(a, 1e-5), 1.0)
    a = a * edge_fade(a.shape, fade[0], fade[1], fade_round) * alpha_cap
    # bleed colour into fully transparent texels (no dark fringes when filtered)
    vis = a > 0.02
    fill = float(rgb[vis].mean()) if vis.any() else 1.0
    rgb = np.where(a > 0.004, rgb, fill)
    out = np.stack([rgb, rgb, rgb, np.clip(a, 0, 1)], axis=2).astype(np.float32)
    return out


def stats(arr):
    a = arr[..., 3]
    edge = max(a[0].max(), a[-1].max(), a[:, 0].max(), a[:, -1].max())
    vis = a > 0.04
    return dict(alpha_max=round(float(a.max()), 3), coverage=round(float(vis.mean()), 3),
                edge_alpha=round(float(edge), 4), rgb_mean=round(float(arr[..., 0][vis].mean()) if vis.any() else 0, 3))


# ------------------------------------------------------------------ sprite geometry (XY plane, z up to camera)
def mesh_from(name, verts, faces, mat, coll, smooth=False):
    from . import geo
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], [tuple(f) for f in faces])
    me.update()
    obj = bpy.data.objects.new(name, me)
    coll.objects.link(obj)
    if mat is not None:
        me.materials.append(mat)
    for p in me.polygons:
        p.use_smooth = smooth
    geo.store_rest(obj)
    return obj


def plane(name, size, mat, coll, z=0.0):
    s = size / 2
    return mesh_from(name, [(-s, -s, z), (s, -s, z), (s, s, z), (-s, s, z)], [(0, 1, 2, 3)], mat, coll)


def ribbon(name, pts, width, mat, coll, z=0.0, taper=(1.0, 1.0)):
    """Flat strip following a 2D polyline. width: float or per-point list; taper scales the end widths."""
    n = len(pts)
    if isinstance(width, (int, float)):
        width = [width] * n
    width = list(width)
    width[0] *= taper[0]
    width[-1] *= taper[1]
    verts = []
    for i, (x, y) in enumerate(pts):
        a = pts[max(0, i - 1)]
        b = pts[min(n - 1, i + 1)]
        tx, ty = b[0] - a[0], b[1] - a[1]
        ln = math.hypot(tx, ty) or 1.0
        nx, ny = -ty / ln, tx / ln
        w = width[i] / 2
        verts.append((x + nx * w, y + ny * w, z))
        verts.append((x - nx * w, y - ny * w, z))
    faces = [(2 * i, 2 * i + 1, 2 * i + 3, 2 * i + 2) for i in range(n - 1)]
    return mesh_from(name, verts, faces, mat, coll)


def annulus(name, r0, r1, mat, coll, segments=128, z=0.0, a0=0.0, a1=math.tau):
    full = abs(a1 - a0 - math.tau) < 1e-6
    steps = segments if full else segments + 1
    verts = []
    for i in range(steps):
        a = a0 + (a1 - a0) * i / segments
        verts.append((r0 * math.cos(a), r0 * math.sin(a), z))
        verts.append((r1 * math.cos(a), r1 * math.sin(a), z))
    faces = []
    for i in range(segments):
        j = (i + 1) % steps
        faces.append((2 * i, 2 * j, 2 * j + 1, 2 * i + 1))
    return mesh_from(name, verts, faces, mat, coll)


def polygon(name, pts, mat, coll, z=0.0):
    verts = [(x, y, z) for x, y in pts]
    return mesh_from(name, verts, [tuple(range(len(verts)))], mat, coll)


def star_outline(points=4, length=1.0, inner=0.06, power=4.0, steps=24, rotation=0.0):
    """Concave-sided star (astroid family): long thin rays joined by curved flanks."""
    out = []
    for k in range(points):
        a0 = rotation + k * math.tau / points
        for i in range(steps):
            t = i / steps
            # ray tip at t=0, valley at t=0.5
            ang = a0 + t * math.tau / points
            u = abs(math.cos(t * math.pi))  # 1 at tips, 0 at valley
            r = inner + (length - inner) * u ** power
            out.append((r * math.cos(ang), r * math.sin(ang)))
    return out


def jagged(p0, p1, rng, depth=5, rough=0.32, decay=0.55):
    """Midpoint-displacement polyline between two 2D points."""
    pts = [p0, p1]
    amp = rough
    for _ in range(depth):
        nxt = [pts[0]]
        for a, b in zip(pts, pts[1:]):
            mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
            dx, dy = b[0] - a[0], b[1] - a[1]
            ln = math.hypot(dx, dy) or 1
            off = rng.uniform(-1, 1) * amp * ln
            nxt += [(mx - dy / ln * off, my + dx / ln * off), b]
        pts = nxt
        amp *= decay
    return pts
