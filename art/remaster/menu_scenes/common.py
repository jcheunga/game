"""Shared lighting moods and landscape helpers for the menu scene recipes."""
import math
import random

from rk import env


def golden(scene, sun_az=200.0, sun_el=9.0, strength=4.2, haze_density=0.012, haze_color='f0cfa2', clouds=0.6,
           exposure=0.0, sky_strength=1.0, light_strength=0.55, haze_size=(600, 600, 30), haze_center=(0, 40, -2),
           falloff=None, zenith='4a6488', horizon='f4b674', mid='f5d9a8', upper='a9bccb'):
    """Low warm sun, painted sky, warm aerial haze. sun_az: compass angle of the sun (0=+X, 90=+Y)."""
    a, e = math.radians(sun_az), math.radians(sun_el)
    d = (math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e))
    env.sky(scene, zenith=zenith, horizon=horizon, ground='4a3a2c', sun_dir=d, sun_glow='ffe2b0', glow=0.85,
            clouds=clouds, cloud_lit='fff0d8', cloud_dark='b49a98', strength=sky_strength, light_strength=light_strength,
            mid=mid, upper=upper)
    env.sun(d, strength, 'ffc98a', 1.2)
    if haze_density:
        env.haze(haze_center, haze_size, haze_density, haze_color, 0.5, falloff=falloff)
    scene.view_settings.exposure = exposure
    return d


def dusk(scene, sun_az=200.0, sun_el=3.0, strength=3.0, haze_density=0.014, haze_color='e0a088', clouds=0.8,
         exposure=0.0, zenith='2a2c48', horizon='e07a4a', glow='ff9a5a', light_strength=0.4, haze_center=(0, 40, -2),
         haze_size=(400, 400, 60), sun_color='ff9a5a', cloud_lit='ff9a6a', cloud_dark='4a3448'):
    a, e = math.radians(sun_az), math.radians(sun_el)
    d = (math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e))
    env.sky(scene, zenith=zenith, horizon=horizon, ground='2a2224', sun_dir=d, sun_glow=glow, glow=1.0,
            clouds=clouds, cloud_lit=cloud_lit, cloud_dark=cloud_dark, light_strength=light_strength)
    env.sun(d, strength, sun_color, 1.0)
    if haze_density:
        env.haze(haze_center, haze_size, haze_density, haze_color, 0.55, falloff=12.0)
    scene.view_settings.exposure = exposure
    return d


def night(scene, moon_az=120.0, moon_el=35.0, strength=0.35, haze_density=0.01, haze_color='7a8aa8', exposure=0.0,
          zenith='0c1424', horizon='2a3a58', glow='9ab8e8', clouds=0.4, light_strength=0.6):
    a, e = math.radians(moon_az), math.radians(moon_el)
    d = (math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e))
    env.sky(scene, zenith=zenith, horizon=horizon, ground='0c0c10', sun_dir=d, sun_glow=glow, glow=0.35,
            glow_power=40, clouds=clouds, cloud_lit='5a6a8a', cloud_dark='141a28', light_strength=light_strength)
    env.sun(d, strength, '9ab8ff', 0.8, name='Moon')
    if haze_density:
        env.haze((0, 40, -2), (400, 400, 50), haze_density, haze_color, 0.3, falloff=10.0)
    scene.view_settings.exposure = exposure
    return d


def rolling(seed=0, amp=2.5, scale=0.025, rise=None, flat=None):
    """Height function: gentle fbm hills; optional rise(y)->extra height and flat zones [(pts, width, z)]."""
    def h(x, y):
        z = env.fbm(x, y, scale, 4, seed) * amp
        if rise:
            z += rise(x, y)
        for pts, width, zz, soft in (flat or []):
            d = env.poly_dist(x, y, pts)
            t = 1 - env.smoothstep(width / 2, width / 2 + soft, d)
            z = z * (1 - t) + zz * t
        return z
    return h


def ring_area(cx, cy, r0, r1, a0=0, a1=math.tau):
    def f(rng):
        a = rng.uniform(a0, a1)
        r = math.sqrt(rng.uniform(r0 * r0, r1 * r1))
        return cx + math.cos(a) * r, cy + math.sin(a) * r
    return f


def rect_area(x0, x1, y0, y1):
    def f(rng):
        return rng.uniform(x0, x1), rng.uniform(y0, y1)
    return f


def trees(n, area, height, seed=0, kinds=('broad', 'cypress', 'conifer'), scale=(0.8, 1.3), avoid=None, min_gap=2.5,
          autumn=0.0, palette=None):
    """Scatter a mixed woodland from a few prototypes."""
    rng = random.Random(seed)
    protos = []
    for k in kinds:
        for i in range(2):
            key = f'{k}{i}{"a" if autumn else ""}'
            if k == 'broad':
                protos.append(env.proto(key, lambda i=i: env.broadleaf(seed * 10 + i, 7.0 + i,
                                                                       autumn=autumn and i == 1,
                                                                       **({'palette': palette} if palette else {}))))
            elif k == 'cypress':
                protos.append(env.proto(key, lambda i=i: env.cypress(seed * 10 + i + 3, 9.0 + i * 1.5)))
            elif k == 'conifer':
                protos.append(env.proto(key, lambda i=i: env.conifer(seed * 10 + i + 5, 9.0 + i * 2)))
            elif k == 'olive':
                protos.append(env.proto(key, lambda i=i: env.olive(seed * 10 + i + 11, 5.0 + i)))
            elif k == 'dead':
                protos.append(env.proto(key, lambda i=i: env.dead_tree(seed * 10 + i + 7, 6.0 + i * 2)))
            elif k == 'bush':
                protos.append(env.proto(key, lambda i=i: env.bush(seed * 10 + i + 9, 1.0 + 0.4 * i)))
    return env.scatter(protos, n, area, height, rng, scale, avoid=avoid, min_gap=min_gap)


def rocks(n, area, height, seed=0, size=(0.4, 1.2), avoid=None, moss=0.3):
    rng = random.Random(seed)
    protos = [env.proto(f'rock{i}{moss}', lambda i=i: env.rock(seed * 7 + i, 1.0, moss=moss)) for i in range(3)]
    return env.scatter(protos, n, area, height, rng, size, coll=env.C['Props'], avoid=avoid)


def tufts(n, area, height, seed=0, scale=(0.8, 1.6), avoid=None, color=('6b7a34', 'a69a52')):
    rng = random.Random(seed)
    protos = [env.proto(f'tuft{i}{color[0]}', lambda i=i: env.grass_tuft(seed * 3 + i, 0.5, color)) for i in range(3)]
    return env.scatter(protos, n, area, height, rng, scale, avoid=avoid)
