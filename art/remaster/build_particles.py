"""Build the 11 tintable particle sprites (128x128 straight-alpha RGBA, white/greyscale).

blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_particles.py -- \
    [--ids all|particle_fire,particle_smoke] [--samples 64] [--res 512] [--no-blend]

Each sprite is authored in Blender (orthographic camera looking down -Z, sprite in the
XY plane, +Y is "up" in the image) as one or two passes:
  lit  - shaded geometry / scattering volume -> alpha = coverage, RGB = greyscale shading
  glow - emission on black                    -> alpha = brightness, RGB = white
rk.fx.compose merges the passes, adds bloom halos and fades to a transparent border.
Renders: artifacts/remaster/particles/<id>.png   Sources: art/remaster/blend/particles/<id>.blend
"""
import argparse
import json
import math
import random
import shutil
import sys
import tempfile
import time
import traceback
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import bpy  # noqa: E402

from rk import core, fx, geo  # noqa: E402
from rk.nodekit import material  # noqa: E402

OUT = core.REVIEW / 'particles'
BLEND = HERE / 'blend' / 'particles'
IDS = ['particle_soft', 'particle_deploy', 'particle_smoke', 'particle_spark', 'particle_fire', 'particle_heal',
       'particle_frost', 'particle_lightning', 'particle_arcane', 'particle_stone', 'particle_trail']


# ------------------------------------------------------------------ shader snippets


def gauss(n, d, sigma, amp=1.0):
    """amp * exp(-(d / sigma)^2)"""
    q = n.math('DIVIDE', d, sigma)
    e = n.math('EXPONENT', n.mul(n.mul(q, q), -1.0))
    return n.mul(e, amp)


def field_plane(name, coll, build, size=2.0):
    """Emission plane whose brightness is a scalar field built from object XY coordinates."""
    m, n = material(name)
    p = n.tc('Object')
    x, y, _ = n.xyz(p)
    d = n.vmath('LENGTH', n.combine(x, y, 0))
    val = build(n, x, y, d)
    n.surface(n.add_shader(n.transparent(), n.emission((1, 1, 1, 1), val)))
    return fx.plane(name, size, m, coll)


def lights_key(rig, key=900, fill=180, rim=420, key_pos=(-2.2, 2.6, 3.4), size=2.2):
    core.area_light('Key', key_pos, (0, 0, 0), key, (1, 1, 1), size, rig)
    core.area_light('Fill', (2.8, -0.6, 2.4), (0, 0, 0), fill, (1, 1, 1), 3.0, rig)
    core.area_light('Rim', (1.6, -2.6, 0.6), (0, 0, 0), rim, (1, 1, 1), 1.5, rig)


# ------------------------------------------------------------------ sprites


def soft(scene, lit, glow, rig, rng):
    # Also the runtime's universal fallback and ambient-weather sprite (ParticleTextureLoader.SoftTexture),
    # drawn at 1.5-28 px: a full, smooth gaussian disc rather than a peaked glow.
    def f(n, x, y, d):
        return n.add(gauss(n, d, 0.5, 0.92), gauss(n, d, 0.16, 0.08))
    field_plane('Soft glow', glow, f)
    return dict(glow_gain=1.0, glow_gamma=1.0, bloom=(), fade_round=True, fade=(0.8, 0.985))


def deploy(scene, lit, glow, rig, rng):
    # shock ring: crisp outer lip, inner wash fading to clear, a small glint at the heart
    def ring(n, x, y, d):
        lip = n.mul(n.smooth(d, 0.46, 0.595), n.smooth(d, 0.648, 0.603, 0.0, 1.0))
        lip = n.mul(lip, n.maprange(n.noise(n.combine(x, y, 0), 7.0, 3, .5), .3, .7, .82, 1.0))
        wash = n.mul(n.math('POWER', n.maprange(d, 0.1, 0.6), 3.0), n.smooth(d, 0.62, 0.57, 0.0, 1.0))
        ang = n.math('ARCTAN2', y, x)
        spokes = n.maprange(n.math('ABSOLUTE', n.math('SINE', n.mul(ang, 12.0))), 0.6, 1.0, 0.0, 1.0)
        wash = n.mul(wash, n.add(0.7, n.mul(spokes, 0.5)))
        v = n.add(n.mul(lip, 1.15), n.mul(wash, 0.42))
        return n.add(v, gauss(n, d, 0.12, 0.35))
    field_plane('Shock ring', glow, ring)
    glint = fx.emit_mat('Heart glint', 2.0)
    for j in range(4):
        _needle(f'Glint {j}', j * math.pi / 2 + math.pi / 2, 0.3 if j % 2 == 0 else 0.24, 0.07, glint, glow,
                z=0.03, power=2.2)
    for k in range(8):
        a = k * math.tau / 8 + math.pi / 2
        long_ = k % 2 == 0
        r1, w = (0.95 if long_ else 0.8), (0.1 if long_ else 0.07)
        ray = fx.emit_mat(f'Burst ray {k}', 1.7)
        o = _needle(f'Ray {k}', a, r1 - 0.52, w, ray, glow, z=0.01, power=1.6)
        o.location = (math.cos(a) * 0.52, math.sin(a) * 0.52, 0)
    mote = fx.emit_mat('Mote', 1.0)
    for k in range(8):
        a = (k + 0.5) * math.tau / 8 + math.pi / 2 + rng.uniform(-.12, .12)
        r = rng.uniform(0.7, 0.8)
        s = rng.uniform(0.014, 0.022)
        geo.cylinder(f'Mote {k}', s, 0.002, (math.cos(a) * r, math.sin(a) * r, 0.02), mote, glow, 12, bevel=0)
    return dict(glow_gain=1.0, glow_gamma=1.0, bloom=((1.4, 0.35), (5.0, 0.22)), fade_round=True, fade=(0.88, 0.99))


def _needle(name, angle, length, width, mat, coll, z=0.01, power=2.2, steps=14):
    """Thin tapered ray from the centre: half-width w(r) = width/2 * (1 - r/L)^power."""
    ca, sa = math.cos(angle), math.sin(angle)
    left, right = [], []
    for i in range(steps + 1):
        r = length * i / steps
        w = width / 2 * (1 - i / steps) ** power + 0.0015
        left.append((ca * r - sa * w, sa * r + ca * w))
        right.append((ca * r + sa * w, sa * r - ca * w))
    pts = [(0.0, 0.0)] + right + list(reversed(left[1:]))
    return fx.polygon(name, pts, mat, coll, z=z)


def spark(scene, lit, glow, rig, rng):
    main = fx.emit_mat('Spark main', 2.6, falloff=(0.0, 0.95, 1.3))
    for k in range(4):
        _needle(f'Spark ray {k}', k * math.pi / 2 + math.pi / 2, 0.93 if k % 2 == 0 else 0.8, 0.13, main, glow,
                power=2.4)
    diag = fx.emit_mat('Spark diagonals', 1.5, falloff=(0.0, 0.5, 1.2))
    for k in range(4):
        _needle(f'Spark diagonal {k}', k * math.pi / 2 + math.pi / 4, 0.46, 0.07, diag, glow, z=0.02, power=2.0)

    def halo(n, x, y, d):
        return n.add(gauss(n, d, 0.085, 1.0), gauss(n, d, 0.26, 0.3))
    field_plane('Spark halo', glow, halo)
    return dict(glow_gain=1.0, glow_gamma=1.1, bloom=((1.0, 0.3), (3.5, 0.14)), fade_round=True, fade=(0.9, 0.995))


def smoke(scene, lit, glow, rig, rng):
    rng = random.Random(31)
    m, n = material('Smoke puff')
    p = n.tc('Object')
    warp = n.vmath('SUBTRACT', n.noise(p, 1.3, 3, .55, out='Color'), (0.5, 0.5, 0.5))
    q = n.vmath('ADD', p, n.vmath('SCALE', warp, scale=0.42))
    blobs = [(0.0, 0.02, 0.0, 0.52)]
    for k in range(6):
        a = k * math.tau / 6 + rng.uniform(-0.25, 0.25)
        r = rng.uniform(0.3, 0.42)
        blobs.append((math.cos(a) * r, math.sin(a) * r * 0.92, rng.uniform(-0.12, 0.12), rng.uniform(0.27, 0.36)))
    field = None
    for cx, cy, cz, r in blobs:
        dd = n.vmath('DISTANCE', q, (cx, cy, cz))
        b = n.maprange(dd, 0.0, r, 1.0, 0.0, clamp=True)
        field = b if field is None else n.math('MAXIMUM', field, b)
    billow = n.noise(q, 3.2, 6, .62)
    wisp = n.noise(n.vmath('MULTIPLY', q, (1.6, 2.6, 1.6)), 7.5, 5, .65, distortion=0.4)
    env = n.smooth(field, -0.05, 0.3)
    f = n.add(field, n.mul(n.mul(n.add(billow, -0.5), 0.75), env))
    f = n.add(f, n.mul(n.mul(n.add(wisp, -0.5), 0.45), env))
    dens = n.mul(n.math('POWER', n.smooth(f, 0.05, 0.75), 1.6), 22.0)
    n.volume(density=dens, color=(0.8, 0.8, 0.8, 1), absorption=(0.45, 0.45, 0.45, 1), anisotropy=0.15)
    geo.box('Smoke domain', (2.0, 2.0, 1.4), (0, 0, 0), m, lit, bevel=0)
    core.sun_light('Sun', (-0.75, 0.75, 0.3), 7.0, (1, 1, 1), 5, rig)
    fx.world_fill(scene, 0.06)
    scene.cycles.volume_bounces = 2
    return dict(lit_gain=1.5, lit_floor=0.22, lit_alpha_gamma=0.85, lit_alpha_gain=1.05, bloom=(),
                fade_round=True, fade=(0.82, 0.99), alpha_cap=0.9)


def fire(scene, lit, glow, rig, rng):
    m, n = material('Flame volume')
    p = n.tc('Object')
    x, y, z = n.xyz(p)
    s0 = n.maprange(y, -0.6, 0.9, 0.0, 1.0)
    amp = n.add(0.03, n.mul(n.math('POWER', s0, 1.3), 0.3))
    wv = n.vmath('SUBTRACT', n.noise(n.vmath('MULTIPLY', p, (1.0, 0.6, 1.0)), 2.4, 3, .5, out='Color'),
                 (0.5, 0.5, 0.5))
    wv = n.vmath('MULTIPLY', wv, n.combine(1.0, 0.45, 1.0))
    q = n.vmath('ADD', p, n.vmath('MULTIPLY', wv, n.combine(amp, amp, amp)))
    qx, qy, qz = n.xyz(q)

    def lobe(x0, y0, h, w, lean):
        s = n.maprange(qy, y0, y0 + h, 0.0, 1.0)
        R = n.mul(n.mul(n.math('POWER', s, 0.45), n.math('POWER', n.math('SUBTRACT', 1.0, s), 0.95)), w)
        cx = n.add(x0, n.mul(n.mul(s, s), lean))
        r = n.vmath('LENGTH', n.combine(n.math('SUBTRACT', qx, cx), 0, n.mul(qz, 1.2)))
        return n.math('SUBTRACT', 1.0, n.math('DIVIDE', r, n.math('MAXIMUM', R, 1e-3))), s
    body, s = lobe(0.0, -0.64, 1.56, 1.02, 0.04)
    left, _ = lobe(-0.21, -0.52, 1.0, 0.64, -0.24)
    right, _ = lobe(0.22, -0.54, 1.1, 0.66, 0.26)
    f = n.math('MAXIMUM', body, n.math('MAXIMUM', left, right))
    lick = n.noise(n.vmath('MULTIPLY', q, (3.0, 1.0, 3.0)), 1.8, 3, .55)
    f = n.math('SUBTRACT', f, n.mul(n.mul(n.add(lick, -0.45), 1.1), n.math('POWER', s, 1.3)))
    shell = n.smooth(f, 0.03, 0.11)
    inner = n.smooth(f, 0.25, 0.75)
    heat = n.add(n.mul(shell, 0.62), n.mul(inner, 1.0))
    heat = n.mul(heat, n.maprange(s, 0.0, 1.0, 1.1, 0.7))
    n.volume(density=0.0, emission=n.mul(heat, 2.2))
    geo.box('Flame domain', (2.0, 2.0, 1.0), (0, 0, 0), m, glow, bevel=0)
    return dict(glow_gain=1.0, glow_gamma=1.0, bloom=((1.0, 0.14), (3.5, 0.12)), fade_round=True,
                fade=(0.86, 0.99))


def heal(scene, lit, glow, rig, rng):
    arm, ln, rad = 0.15, 0.55, 0.07
    pts = []

    def arc(cx, cy, a0, a1, r, steps=6):
        for i in range(steps + 1):
            a = a0 + (a1 - a0) * i / steps
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    c = arm - rad
    e = ln - rad
    arc(c, e, 0, math.pi / 2, rad)
    arc(-c, e, math.pi / 2, math.pi, rad)
    pts.append((-arm, arm))
    arc(-e, c, math.pi / 2, math.pi, rad)
    arc(-e, -c, math.pi, 1.5 * math.pi, rad)
    pts.append((-arm, -arm))
    arc(-c, -e, math.pi, 1.5 * math.pi, rad)
    arc(c, -e, 1.5 * math.pi, 2 * math.pi, rad)
    pts.append((arm, -arm))
    arc(e, -c, 1.5 * math.pi, 2 * math.pi, rad)
    arc(e, c, 0, math.pi / 2, rad)
    pts.append((arm, arm))
    mat = fx.lit_mat('Holy plus', base=0.85, rough=0.3, spec=0.7, emit=0.14, coat=0.5)
    geo.extrude('Holy plus', pts, 0.16, mat, lit, plane='XY', bevel=0.06, segments=5)
    lights_key(rig, key=110, fill=20, rim=110)
    fx.world_fill(scene, 0.1)

    def halo(n, x, y, d):
        return n.add(gauss(n, d, 0.38, 0.62), gauss(n, d, 0.7, 0.25))
    field_plane('Holy halo', glow, halo)
    sp = fx.emit_mat('Holy sparkle', 2.0)
    for k, (sx, sy, size) in enumerate(((0.55, 0.55, 0.2), (-0.52, -0.48, 0.14), (-0.58, 0.44, 0.11),
                                        (0.47, -0.58, 0.15))):
        for j in range(4):
            o = _needle(f'Sparkle {k} {j}', j * math.pi / 2, size, size * 0.38, sp, glow, z=0.2, power=2.0)
            o.location = (sx, sy, 0)
    return dict(lit_gain=1.0, lit_floor=0.62, glow_gain=1.0, glow_gamma=1.0, bloom=((1.5, 0.22), (6.0, 0.16)),
                bloom_from='lit', fade_round=True, fade=(0.86, 0.99))


def _prism(name, p0, p1, w0, h, mat, coll, tip=0.35, base_taper=0.55):
    """Faceted crystal blade from p0 to p1 (XY), diamond section width w0, ridge height h."""
    x0, y0 = p0
    x1, y1 = p1
    dx, dy = x1 - x0, y1 - y0
    ln = math.hypot(dx, dy)
    ux, uy = dx / ln, dy / ln
    nx, ny = -uy, ux
    secs = []
    steps = 8
    for i in range(steps + 1):
        t = i / steps
        w = w0 * (base_taper + (1 - base_taper) * min(1.0, t / 0.25))
        if t > 1 - tip:
            w *= max(0.0, (1 - t) / tip)
        w = max(w, 1e-4)
        cx, cy = x0 + dx * t, y0 + dy * t
        hh = h * w / w0
        secs.append([(cx + nx * w / 2, cy + ny * w / 2, 0.0), (cx, cy, hh), (cx - nx * w / 2, cy - ny * w / 2, 0.0),
                     (cx, cy, -hh * 0.3)])
    return geo.loft(name, secs, mat, coll, closed=True, cap=True, smooth=False)


def frost(scene, lit, glow, rig, rng):
    ice = fx.lit_mat('Ice crystal', base=0.8, rough=0.12, spec=1.0, emit=0.06, coat=1.0)
    for k in range(6):
        a = k * math.tau / 6 + math.pi / 2
        ca, sa = math.cos(a), math.sin(a)

        def P(t, off=0.0):
            return (ca * t - sa * off, sa * t + ca * off)
        _prism(f'Arm {k}', P(0.08), P(0.86), 0.085, 0.06, ice, lit, tip=0.3, base_taper=1.0)
        for t, l, w in ((0.34, 0.25, 0.06), (0.54, 0.2, 0.05), (0.7, 0.12, 0.04)):
            for sgn in (-1, 1):
                b = a + sgn * math.radians(58)
                p0 = P(t)
                p1 = (p0[0] + math.cos(b) * l, p0[1] + math.sin(b) * l)
                _prism(f'Branch {k}', p0, p1, w, 0.04, ice, lit, tip=0.45, base_taper=1.0)
    hexa = [(0.17 * math.cos(k * math.tau / 6 + math.pi / 2), 0.17 * math.sin(k * math.tau / 6 + math.pi / 2))
            for k in range(6)]
    geo.extrude('Hex core', hexa, 0.05, ice, lit, plane='XY', bevel=0.02, segments=2)
    lights_key(rig, key=70, fill=14, rim=90)
    fx.world_fill(scene, 0.12)

    def halo(n, x, y, d):
        return n.add(gauss(n, d, 0.22, 0.4), gauss(n, d, 0.6, 0.16))
    field_plane('Frost halo', glow, halo)
    return dict(lit_gain=1.0, lit_floor=0.4, glow_gain=1.0, glow_gamma=1.0, bloom=((1.2, 0.3), (4.5, 0.18)),
                bloom_from='lit', fade_round=True, fade=(0.88, 0.995))


def lightning(scene, lit, glow, rig, rng):
    rng = random.Random(5)
    core_m = fx.emit_mat('Bolt core', 3.0)
    main = fx.jagged((-0.22, 0.92), (0.18, -0.92), rng, depth=4, rough=0.42, decay=0.5)
    fine = [main[0]]
    for a, b in zip(main, main[1:]):
        fine += fx.jagged(a, b, rng, depth=2, rough=0.18, decay=0.6)[1:]
    main = fine
    n = len(main)
    widths = [0.05 * (1.0 - 0.5 * i / (n - 1)) for i in range(n)]
    fx.ribbon('Bolt', main, widths, core_m, glow, z=0.02, taper=(0.5, 0.15))
    for frac, end, w in ((0.3, (0.66, 0.22), 0.03), (0.52, (-0.66, -0.36), 0.026), (0.7, (0.6, -0.68), 0.02)):
        sx, sy = main[int(n * frac)]
        br = fx.jagged((sx, sy), end, rng, depth=3, rough=0.4, decay=0.5)
        fine = [br[0]]
        for a, b in zip(br, br[1:]):
            fine += fx.jagged(a, b, rng, depth=2, rough=0.18, decay=0.6)[1:]
        m = len(fine)
        ws = [w * (1 - 0.8 * i / (m - 1)) + 0.004 for i in range(m)]
        fx.ribbon('Fork', fine, ws, core_m, glow, z=0.02, taper=(1.0, 0.2))
    return dict(glow_gain=1.0, glow_gamma=1.0, bloom=((1.2, 0.8), (3.2, 1.0), (8.0, 0.55)), fade_round=False,
                fade=(0.9, 0.995))


RUNES = {
    'fehu': [[(0.2, 0), (0.2, 1)], [(0.2, 0.62), (0.75, 0.95)], [(0.2, 0.32), (0.75, 0.65)]],
    'uruz': [[(0.2, 0), (0.2, 1), (0.8, 0.7), (0.8, 0)]],
    'thurisaz': [[(0.25, 0), (0.25, 1)], [(0.25, 0.75), (0.75, 0.5), (0.25, 0.25)]],
    'ansuz': [[(0.25, 0), (0.25, 1)], [(0.25, 1), (0.75, 0.72)], [(0.25, 0.68), (0.75, 0.4)]],
    'raido': [[(0.2, 0), (0.2, 1), (0.75, 0.75), (0.2, 0.5), (0.8, 0)]],
    'kenaz': [[(0.75, 1), (0.25, 0.5), (0.75, 0)]],
    'gebo': [[(0.15, 0.05), (0.85, 0.95)], [(0.85, 0.05), (0.15, 0.95)]],
    'hagalaz': [[(0.2, 0), (0.2, 1)], [(0.8, 0), (0.8, 1)], [(0.2, 0.68), (0.8, 0.32)]],
    'tiwaz': [[(0.5, 0), (0.5, 1)], [(0.15, 0.66), (0.5, 1), (0.85, 0.66)]],
    'algiz': [[(0.5, 0), (0.5, 1)], [(0.12, 1), (0.5, 0.55), (0.88, 1)]],
    'sowilo': [[(0.75, 1), (0.25, 0.62), (0.75, 0.38), (0.25, 0)]],
    'dagaz': [[(0.15, 0), (0.15, 1), (0.85, 0), (0.85, 1), (0.15, 0)]],
    'othala': [[(0.15, 0), (0.75, 0.55), (0.5, 0.95), (0.25, 0.55), (0.85, 0)]],
    'ingwaz': [[(0.5, 0.05), (0.85, 0.5), (0.5, 0.95), (0.15, 0.5), (0.5, 0.05)]],
}


def arcane(scene, lit, glow, rig, rng):
    line = fx.emit_mat('Rune line', 1.6)
    soft_m = fx.emit_mat('Rune ring glow', 0.45)
    fx.annulus('Outer ring', 0.84, 0.875, line, glow, 160, z=0.02)
    fx.annulus('Inner ring', 0.565, 0.59, line, glow, 160, z=0.02)
    fx.annulus('Inner ring tick', 0.45, 0.465, line, glow, 160, z=0.02)
    names = ['fehu', 'ansuz', 'raido', 'kenaz', 'tiwaz', 'algiz', 'sowilo', 'othala', 'thurisaz', 'dagaz']
    count = len(names)
    for k, name in enumerate(names):
        a = math.pi / 2 - k * math.tau / count
        ca, sa = math.cos(a), math.sin(a)
        # local frame: glyph x along tangent (clockwise), glyph y radially outward
        tx, ty = sa, -ca
        rx, ry = ca, sa
        r0, gh, gw = 0.63, 0.17, 0.11
        for si, stroke in enumerate(RUNES[name]):
            pts = []
            for gx, gy in stroke:
                lx = (gx - 0.5) * gw
                ly = r0 + gy * gh
                pts.append((tx * lx + rx * ly, ty * lx + ry * ly))
            # densify for clean ribbons at corners
            dense = []
            for p, q in zip(pts, pts[1:]):
                for i in range(4):
                    t = i / 4
                    dense.append((p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t))
            dense.append(pts[-1])
            fx.ribbon(f'Rune {name} {si}', dense, 0.022, line, glow, z=0.02)
    # separators between glyphs
    for k in range(count):
        a = math.pi / 2 - (k + 0.5) * math.tau / count
        pts = [(math.cos(a) * 0.7, math.sin(a) * 0.7), (math.cos(a) * 0.76, math.sin(a) * 0.76)]
        fx.ribbon(f'Dot {k}', pts, 0.026, line, glow, z=0.02)
    # inner sigil: a rotating triangle pair (hexagram) inside the inner ring

    diamond = [(0.38 * math.cos(math.pi / 2 + i * math.pi / 2), 0.38 * math.sin(math.pi / 2 + i * math.pi / 2))
               for i in range(5)]
    fx.ribbon('Sigil diamond', diamond, 0.018, line, glow, z=0.02)
    fx.annulus('Sigil eye', 0.15, 0.168, line, glow, 96, z=0.02)
    for i in range(4):
        a = math.pi / 4 + i * math.pi / 2
        fx.ribbon(f'Sigil spoke {i}', [(math.cos(a) * 0.2, math.sin(a) * 0.2), (math.cos(a) * 0.3, math.sin(a) * 0.3)],
                  0.016, line, glow, z=0.02, taper=(1.0, 0.3))

    def heart(n, x, y, d):
        rim = gauss(n, n.math('SUBTRACT', d, 0.858), 0.045, 0.32)
        return n.add(n.add(gauss(n, d, 0.16, 0.7), gauss(n, d, 0.42, 0.18)), rim)
    field_plane('Arcane heart', glow, heart)
    return dict(glow_gain=1.0, glow_gamma=1.0, bloom=((1.0, 0.4), (3.5, 0.25)), fade_round=True,
                fade=(0.93, 0.995))


def stone(scene, lit, glow, rig, rng):
    import bmesh
    rng = random.Random(9)
    bm = bmesh.new()
    for i in range(22):
        th = rng.uniform(0, math.tau)
        ph = math.acos(rng.uniform(-1, 1))
        r = rng.uniform(0.75, 1.0)
        bm.verts.new((r * math.sin(ph) * math.cos(th) * 0.66, r * math.sin(ph) * math.sin(th) * 0.56,
                      r * math.cos(ph) * 0.46))
    bmesh.ops.convex_hull(bm, input=bm.verts)
    me = bpy.data.meshes.new('Rock chunk')
    bm.to_mesh(me)
    bm.free()
    rock = bpy.data.objects.new('Rock chunk', me)
    lit.objects.link(rock)
    geo.store_rest(rock)
    # chisel a few flat fracture planes and roughen
    sub = rock.modifiers.new('Detail', 'SUBSURF')
    sub.levels = sub.render_levels = 3
    sub.subdivision_type = 'SIMPLE'
    geo.apply_modifiers(rock)
    geo.displace_noise(rock, 0.035, 5.0, seed=3)
    geo.displace_noise(rock, 0.012, 16.0, seed=8)
    rock.rotation_euler = (0.35, -0.25, 0.6)
    mat, n = material('Chunk stone')
    p = n.tc('Object')
    cr = n.maprange(n.voronoi(p, 2.6, 'DISTANCE_TO_EDGE'), 0.0, 0.03, 1.0, 0.0)
    cr = n.mul(cr, n.maprange(n.noise(p, 2.0, 2, .5), .42, .58))
    grain = n.noise(p, 9.0, 6, .65)
    alb = n.maprange(grain, 0.3, 0.7, 0.5, 0.78)
    alb = n.mixf(n.mul(cr, 0.6), alb, 0.2)
    speck = n.maprange(n.voronoi(p, 45.0, 'F1'), 0.0, 0.1, 1.0, 0.0)
    alb = n.mixf(n.mul(speck, 0.3), alb, 0.92)
    cav = n.ao(0.12, local=True)
    alb = n.mul(alb, n.maprange(cav, 0.3, 1.0, 0.45, 1.0))
    col = n.combine(alb, alb, alb)
    hgt = n.add(n.mul(grain, 0.5), n.mul(cr, -0.8))
    n.principled(**{'Base Color': col, 'Roughness': 0.85, 'Normal': n.bump(hgt, 0.5, 0.03)})
    rock.data.materials.append(mat)
    for poly in rock.data.polygons:
        poly.use_smooth = False
    lights_key(rig, key=190, fill=8, rim=80, key_pos=(-3.2, 2.6, 1.8))
    fx.world_fill(scene, 0.08)
    return dict(lit_gain=1.0, lit_floor=0.12, bloom=(), fade_round=True, fade=(0.9, 0.995))


def trail(scene, lit, glow, rig, rng):
    def f(n, x, y, d):
        ax = n.math('ABSOLUTE', x)
        # spindle: soft lozenge, widest at the centre, tapering to both ends
        span = n.math('MAXIMUM', n.math('SUBTRACT', 1.0, n.math('POWER', n.math('DIVIDE', ax, 0.94), 2.0)), 0.0)
        wcore = n.add(n.mul(span, 0.075), 0.001)
        whalo = n.add(n.mul(span, 0.3), 0.001)
        corev = n.math('EXPONENT', n.mul(n.math('POWER', n.math('DIVIDE', y, wcore), 2.0), -1.0))
        halov = n.math('EXPONENT', n.mul(n.math('POWER', n.math('DIVIDE', y, whalo), 2.0), -1.0))
        along = n.math('EXPONENT', n.mul(n.math('POWER', n.math('DIVIDE', ax, 0.66), 3.0), -1.0))
        streak = n.noise(n.combine(n.mul(x, 0.5), n.mul(y, 9.0), 0), 3.0, 2, .5)
        halov = n.mul(halov, n.maprange(streak, 0.3, 0.7, 0.86, 1.0))
        v = n.add(n.mul(corev, 0.7), n.mul(halov, 0.55))
        return n.mul(v, n.mul(along, n.math('POWER', span, 0.5)))
    field_plane('Trail streak', glow, f)
    return dict(glow_gain=1.0, glow_gamma=1.2, bloom=((1.2, 0.15),), fade_round=False, fade=(0.9, 0.995))


BUILDERS = dict(particle_soft=soft, particle_deploy=deploy, particle_smoke=smoke, particle_spark=spark,
                particle_fire=fire, particle_heal=heal, particle_frost=frost, particle_lightning=lightning,
                particle_arcane=arcane, particle_stone=stone, particle_trail=trail)


def build(ident, samples, res, save_blend):
    scene, lit, glow, rig = fx.sprite_scene(res, samples)
    rng = random.Random(hash(ident) & 0xffff)
    opts = BUILDERS[ident](scene, lit, glow, rig, rng)
    tmp = Path(tempfile.mkdtemp(prefix='fx_'))
    try:
        lit_px = fx.render_pass(scene, fx.LIT, tmp / 'lit.exr') if lit.all_objects else None
        glow_px = fx.render_pass(scene, fx.GLOW, tmp / 'glow.exr') if glow.all_objects else None
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    sprite = fx.compose(lit_px, glow_px, size=128, **opts)
    fx.write_png(sprite, OUT / f'{ident}.png')
    if save_blend:
        scene['fx_compose'] = json.dumps(opts)
        txt = bpy.data.texts.new('README_fx')
        txt.write('Sprite source. Collections "FX lit" (coverage/greyscale) and "FX glow" (emission->alpha) are '
                  'rendered separately and merged by art/remaster/rk/fx.py compose() with the options stored in '
                  'scene["fx_compose"]. Rebuild: art/remaster/build_particles.py --ids ' + ident + '\n')
        scene.render.image_settings.file_format = 'PNG'
        core.save_blend(BLEND / f'{ident}.blend')
    return fx.stats(sprite)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--ids', default='all')
    parser.add_argument('--samples', type=int, default=64)
    parser.add_argument('--res', type=int, default=512)
    parser.add_argument('--no-blend', action='store_true')
    args = parser.parse_args(core.args_after_dashes(sys.argv))
    ids = IDS if args.ids == 'all' else [i if i.startswith('particle_') else 'particle_' + i
                                          for i in args.ids.split(',')]
    failed = []
    for ident in ids:
        t0 = time.time()
        try:
            st = build(ident, args.samples, args.res, not args.no_blend)
            print(f'PARTICLE_COMPLETE {ident} {time.time() - t0:.1f}s {st}', flush=True)
        except Exception:
            traceback.print_exc()
            print('PARTICLE_FAILED ' + ident, flush=True)
            failed.append(ident)
    if failed:
        sys.exit(1)


main()
