"""Battlefield fallback framing.

Runtime facts (scripts/combat/BattleController.*): the 1280x720 image is tiled across the 2560x720
battle world in alternating mirrored panels, then the combat rectangle x 84..2476, y 96..584 is
painted over with procedural ground. So only the top band (y < 96, the far/back edge), the bottom
band (y > 584, the near/front edge) and the outer 84 px columns are ever visible; the central band
must stay quiet ground. The camera is an orthographic 3/4 view; BF converts between the field and
image space so back scenery starts beyond the far edge and front props never rise into the band.
"""
import math
import random

from mathutils import Vector

from rk import env

BAND_TOP = 96      # image px: far edge of the combat field
BAND_BOTTOM = 584  # image px: near edge
IMG_W, IMG_H = 1280, 720


class BF:
    def __init__(self, width=24.0, pitch=38.0):
        self.W = width
        self.th = math.radians(pitch)
        self.ppm = IMG_W / width                       # image px per view-plane metre
        band_m = (BAND_BOTTOM - BAND_TOP) / self.ppm   # band height in the view plane
        self.D = band_m / math.sin(self.th)            # field depth on the ground
        centre_offset = (IMG_H / 2 - (BAND_TOP + BAND_BOTTOM) / 2) / self.ppm
        self.yT = self.D / 2 - centre_offset / math.sin(self.th)
        self.x0, self.x1 = -width / 2, width / 2

    # image row of a world point (ortho camera; x does not matter)
    def img_y(self, y, z=0.0):
        v = (y - self.yT) * math.sin(self.th) + z * math.cos(self.th)
        return IMG_H / 2 - v * self.ppm

    def front_max_h(self, y, margin=6):
        """Tallest object that can stand at ground depth y (< 0) without rising into the combat band."""
        v_lim = (IMG_H / 2 - (BAND_BOTTOM + margin)) / self.ppm
        return max(0.0, (v_lim - (y - self.yT) * math.sin(self.th)) / math.cos(self.th))

    def back_min_y(self, margin=0.4):
        """First ground depth behind the far edge where back scenery may stand."""
        return self.D + margin

    def front_y_range(self):
        """Ground depths visible in the bottom band (near edge .. frame bottom)."""
        y_bottom = self.yT - (IMG_H / 2) / self.ppm / math.sin(self.th)
        return y_bottom, 0.0

    def camera(self, lens_dist=60.0):
        d = Vector((0, math.cos(self.th), -math.sin(self.th)))
        target = Vector((0, self.yT, 0))
        cam = env.camera(tuple(target - d * lens_dist), tuple(target), lens=50)
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = self.W
        cam.data.clip_end = 600
        return cam

    def fit(self, y, nominal_h):
        """Scale factor for a front prop of nominal height at depth y."""
        mh = self.front_max_h(y)
        return min(1.0, mh / max(nominal_h, 1e-3))


def field_height(bf, back_rise=0.0, front_drop=0.0, seed=0, amp=0.25, back_start=1.0, front_start=0.5):
    """Flat field; the ground may rise behind it (banks, slopes) and fall away in front."""
    def h(x, y):
        z = 0.0
        if y > bf.D + back_start:
            t = env.smoothstep(bf.D + back_start, bf.D + back_start + 10.0, y)
            z += back_rise * t + env.fbm(x, y, 0.12, 3, seed) * amp * t * 4
        elif y < -front_start:
            t = env.smoothstep(-front_start, -front_start - 6.0, y)
            z -= front_drop * t
            z += env.fbm(x, y, 0.2, 3, seed + 3) * amp * t * 2
        return z
    return h


def ground(bf, mat, height, extra_back=40.0, extra_front=14.0, masks=None, res=6.0):
    w = bf.W + 12
    d = bf.D + extra_back + extra_front
    cy = (bf.D + extra_back - extra_front) / 2
    return env.terrain((w, d), (int(w * res / 2), int(d * res / 2)), (0, cy), height, mat=mat, masks=masks or {})


def row(n, x0, x1, rng, jitter=0.6):
    """Evenly spread x positions with jitter across the panel width (mirrored tiling joins at both ends)."""
    if n <= 0:
        return []
    step = (x1 - x0) / n
    return [x0 + step * (i + 0.5) + rng.uniform(-jitter, jitter) * step for i in range(n)]


def back_band(bf):
    """(y_min, y_max) ground depths visible behind the far edge."""
    top = bf.yT + (IMG_H / 2) / bf.ppm / math.sin(bf.th)
    return bf.back_min_y(), top


def scatter_front(bf, height, protos_fn, n, seed=0, nominal_h=1.0, y_range=None, x_pad=2.0, min_gap=1.5, coll=None):
    """Instance props in the bottom band, each scaled so it never rises into the combat band."""
    rng = random.Random(seed)
    y0, y1 = y_range or (bf.front_y_range()[0] - 0.3, -0.6)
    placed = []
    protos = protos_fn()
    tries = 0
    while len(placed) < n and tries < n * 40:
        tries += 1
        x, y = rng.uniform(bf.x0 - x_pad, bf.x1 + x_pad), rng.uniform(y0, y1)
        if any((x - a) ** 2 + (y - b) ** 2 < min_gap ** 2 for a, b in placed):
            continue
        s = rng.uniform(0.85, 1.15) * bf.fit(y, nominal_h)
        if s < 0.35:
            continue
        placed.append((x, y))
        env.inst(rng.choice(protos), (x, y, height(x, y) - 0.03), rng.uniform(0, math.tau), s, coll or env.C['Props'])
    return placed


# ------------------------------------------------------------------ lighting contract (scripts/combat/BattleLighting.cs)
# In battle the sun sits upper-left and sprite shadows are projected down-right by ShadowCast ~(0.72, 0.28) of the
# sprite height. With this camera that is a sun at azimuth ~148 deg (left and beyond the far edge) and ~50 deg up.
SUN_AZ, SUN_EL = 148.0, 50.0
ROUTE_OF = {t: r for r, ts in (
    ('city', ('urban', 'highway', 'night')), ('harbor', ('industrial', 'shipyard', 'swamp')),
    ('foundry', ('railyard', 'smelter', 'foundry')), ('quarantine', ('checkpoint', 'decon', 'lab', 'blacksite')),
    ('thornwall', ('pass', 'shrine', 'watchfort')), ('basilica', ('cathedral', 'ossuary', 'reliquary')),
    ('mire', ('marsh', 'chapel', 'ferry')), ('steppe', ('grassland', 'siegecamp', 'waystation')),
    ('gloamwood', ('grove', 'timberroad', 'witchcircle')), ('citadel', ('bridgefort', 'breachyard', 'innerkeep')))
    for t in ts}
# Sun colours follow BattleLighting.ForZone tints (warm by default, cooler/greener where the game tints so).
SUN_COLOR = {'city': 'ffe0b4', 'harbor': 'fff0dc', 'foundry': 'ffd09a', 'quarantine': 'f2f0d4', 'thornwall': 'fff2dc',
             'basilica': 'fbeee0', 'mire': 'f0eccc', 'steppe': 'ffdcaa', 'gloamwood': 'f6e4c8', 'citadel': 'f8ece0'}


def sun_dir():
    a, e = math.radians(SUN_AZ), math.radians(SUN_EL)
    return Vector((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e)))


def relight(scene, terrain, strength=None, angle=3.0):
    """Force every sun/moon in the scene onto the shared battle sun direction and zone colour."""
    from rk.core import srgb
    d = sun_dir()
    col = srgb(SUN_COLOR.get(ROUTE_OF.get(terrain, 'city'), 'ffe0b4'))[:3]
    for o in scene.objects:
        if o.type == 'LIGHT' and o.data.type == 'SUN':
            o.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
            o.data.color = col
            o.data.angle = math.radians(angle)
            if strength is not None:
                o.data.energy = strength


def shafts(bf, density=0.012, color='f2e2c8', height=9.0):
    """Thin homogeneous air over both bands so the low sun throws visible light shafts through gaps."""
    y_min, y_max = back_band(bf)
    y0, _ = bf.front_y_range()
    env.haze((0, (y_min + y_max) / 2 + 2, -0.5), (bf.W + 30, (y_max - y_min) + 16, height), density, color, 0.6,
             falloff=None, name='Back shafts', soft=True)
    env.haze((0, y0 / 2, -0.5), (bf.W + 30, abs(y0) + 8, height * 0.6), density * 0.6, color, 0.6, falloff=None,
             name='Front shafts', soft=True)


def drifts(bf, base, amp=0.9, seed=0):
    """Wind-sculpted snow drifts (elongated along X) everywhere except the playable band."""
    from mathutils import noise as mnoise

    def h(x, y):
        z = base(x, y)
        inside = env.smoothstep(-1.2, 0.4, y) * (1 - env.smoothstep(bf.D - 0.4, bf.D + 1.2, y))
        w = 1 - inside
        if w <= 0:
            return z
        n1 = mnoise.noise(Vector((x * 0.16 + seed, y * 0.7, seed * 0.3)))
        ridge = (1 - abs(n1)) ** 3
        n2 = mnoise.noise(Vector((x * 0.05, y * 0.2, seed + 9.0)))
        return z + w * (ridge * amp + max(0.0, n2) * amp * 0.8)
    return h
