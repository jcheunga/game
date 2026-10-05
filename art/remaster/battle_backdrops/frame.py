"""Layered zone battle backdrops, framed exactly onto the battle world.

Runtime contract (WorldEnvironmentArt.LoadZoneBackdrop / BattleTerrainCanvas): <zone>.json lists layers back to
front. Each layer PNG covers its game rect ("rect": [x, y, w, h], game units) when the battle camera is centred on
the field, and scrolls at "parallax" times the camera's speed (1 = locked to the field). Game x runs along the
field; game y grows toward the camera and the walking band is centred on game y BAND_C (data/combat_config.json).

The layers stand in for one perspective view at three distances:
- near (parallax 1): the battle camera itself, orthographic and 22 degrees above the ground, so the road, its
  verges and the scenery lining it agree with the unit and structure sprites. Transparent above the scenery.
- mid (parallax ~0.5): mid-distance scenery at half scale, seen lower (about 11 degrees), as perspective would.
  Orthographic, so it can scroll across the whole field without converging.
- far (parallax ~0.15): a true perspective vista with the horizon and painted sky.
Each layer is a separate Blender scene lit by the battle sun (upper left, behind the scene).
"""
import json
import math

from mathutils import Vector

from rk import core, env

PPM = 16.0          # game units per metre at the near layer: soldiers ~2.3 m drawn, the gatehouse ~6 m
SIDE_MARGIN = 10.0


def combat():
    return json.loads((core.ROOT / 'data/combat_config.json').read_text())['Combat']


def _even(v):
    return int(round(v / 2)) * 2


class Ortho:
    """Orthographic layer. Scene axes: X along the field, Y depth away from the camera, Z up. Scene metres draw at
    PPM*scale game units, and ground at depth Y=0 lands on game row `line`."""
    kind = 'ortho'

    def __init__(self, name, parallax, pitch, line, gy0, gy1, res_x, scale=None):
        c = combat()
        self.name, self.parallax = name, parallax
        self.scale = parallax if scale is None else scale
        self.world_w = c['BattlefieldLeft'] + c['BattlefieldRight']
        self.gx0, self.gx1 = -SIDE_MARGIN, self.world_w + SIDE_MARGIN
        self.gy0, self.gy1, self.line = gy0, gy1, line
        self.pitch = pitch
        self.th = math.radians(pitch)
        self.s, self.c = math.sin(self.th), math.cos(self.th)
        self.k = PPM * self.scale
        self.W, self.H = (self.gx1 - self.gx0) / self.k, (self.gy1 - self.gy0) / self.k
        self.res = (res_x, _even(res_x * self.H / self.W))
        self.x0, self.x1 = self.X(self.gx0), self.X(self.gx1)
        self.cam_dist = 160.0
        self.top_y = self.Y(self.gy0)       # ground depth seen at the top edge
        self.bottom_y = self.Y(self.gy1)    # ground depth seen at the bottom edge

    def X(self, gx):
        return (gx - (self.gx0 + self.gx1) / 2) / self.k

    def gx(self, x):
        return (self.gx0 + self.gx1) / 2 + x * self.k

    def Y(self, gy, z=0.0):
        """Ground depth whose point at height z lands on game row gy."""
        return ((self.line - gy) / self.k - z * self.c) / self.s

    def game_y(self, y, z=0.0):
        return self.line - (y * self.s + z * self.c) * self.k

    def height_to(self, y, gy):
        """Height an object standing at depth y needs to reach game row gy."""
        return ((self.line - gy) / self.k - y * self.s) / self.c

    def rect(self):
        return [self.gx0, self.gy0, self.gx1 - self.gx0, self.gy1 - self.gy0]

    def camera(self, scene):
        scene.render.resolution_x, scene.render.resolution_y = self.res
        d = Vector((0, self.c, -self.s))
        up = Vector((0, self.s, self.c))
        target = up * ((self.line - (self.gy0 + self.gy1) / 2) / self.k)
        cam = env.camera(tuple(target - d * self.cam_dist), tuple(target), lens=50)
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = self.W
        cam.data.sensor_fit = 'HORIZONTAL'
        cam.data.clip_start = 1.0
        cam.data.clip_end = self.cam_dist + 900
        scene.camera = cam
        return cam

    def row(self, n, rng, jitter=0.3, x0=None, x1=None):
        x0 = self.x0 - 2 if x0 is None else x0
        x1 = self.x1 + 2 if x1 is None else x1
        step = (x1 - x0) / max(1, n)
        return [x0 + step * (i + 0.5) + rng.uniform(-jitter, jitter) * step for i in range(n)]

    def manifest(self, file):
        return {'file': file, 'rect': [round(v, 3) for v in self.rect()], 'parallax': self.parallax}


class Near(Ortho):
    """The battle camera: the road the troops walk, its verges and the scenery lining it."""

    def __init__(self, res_x=4096, top=230.0, bottom=260.0):
        c = combat()
        self.top_row, self.bottom_row = c['BattlefieldTop'], c['BattlefieldBottom']
        self.pad = c['SpawnVerticalPadding']
        band_c = (self.top_row + self.bottom_row) / 2
        super().__init__('near', 1.0, 22.0, band_c, band_c - top, band_c + bottom, res_x)
        self.band_c = band_c
        self.band_far, self.band_near = self.Y(self.top_row), self.Y(self.bottom_row)
        self.back_y = self.band_far + 1.5     # first depth behind the road where scenery may stand
        self.front_y = self.band_near - 1.5   # first depth in front of the road
        self.near_y = self.bottom_y
        self.wagon_x = self.X(c['PlayerBaseX'])
        self.gate_x = self.X(c['EnemyBaseX'])

    def front_max_h(self, y, margin=8.0):
        """Tallest prop standing at ground depth y (in front of the road) that stays below the band on screen."""
        v_lim = (self.band_c - (self.bottom_row + margin)) / PPM
        return max(0.0, (v_lim - y * self.s) / self.c)

    def fit(self, y, nominal_h):
        return min(1.0, self.front_max_h(y) / max(nominal_h, 1e-3))


class Persp:
    """Perspective vista: camera at height cam_h looking level along +Y; the horizon lands on game row `horizon`."""
    kind = 'persp'

    def __init__(self, name, parallax, horizon, gy0, gy1, res_x, fov=40.0, cam_h=10.0):
        c = combat()
        self.name, self.parallax = name, parallax
        self.world_w = c['BattlefieldLeft'] + c['BattlefieldRight']
        self.gx0, self.gx1 = -SIDE_MARGIN, self.world_w + SIDE_MARGIN
        self.gy0, self.gy1, self.horizon = gy0, gy1, horizon
        self.fov, self.cam_h = fov, cam_h
        self.k = (self.gx1 - self.gx0) / (2 * math.tan(math.radians(fov) / 2))   # game units per unit tangent
        w, h = self.gx1 - self.gx0, self.gy1 - self.gy0
        self.res = (res_x, _even(res_x * h / w))
        self.cam_dist = 0.0

    def project(self, x, y, z):
        return (self.gx0 + self.gx1) / 2 + self.k * x / y, self.horizon - self.k * (z - self.cam_h) / y

    def X(self, gx, y):
        return (gx - (self.gx0 + self.gx1) / 2) * y / self.k

    def half_width(self, y, margin=1.08):
        return (self.gx1 - self.gx0) / 2 * y / self.k * margin

    def depth_for_row(self, gy, z=0.0):
        """Depth where ground at height z appears on game row gy (rows below the horizon only)."""
        return self.k * (self.cam_h - z) / max(gy - self.horizon, 1e-3)

    def height_for_row(self, gy, y):
        """Height at depth y that appears on game row gy."""
        return self.cam_h + (self.horizon - gy) * y / self.k

    def rect(self):
        return [self.gx0, self.gy0, self.gx1 - self.gx0, self.gy1 - self.gy0]

    def camera(self, scene):
        scene.render.resolution_x, scene.render.resolution_y = self.res
        cam = env.camera((0, 0, self.cam_h), (0, 100, self.cam_h), lens=18.0 / math.tan(math.radians(self.fov) / 2))
        cam.data.sensor_fit = 'HORIZONTAL'
        cam.data.shift_y = (self.horizon - (self.gy0 + self.gy1) / 2) / (self.gx1 - self.gx0)
        cam.data.clip_start = 0.5
        cam.data.clip_end = 20000
        scene.camera = cam
        return cam

    def manifest(self, file):
        return {'file': file, 'rect': [round(v, 3) for v in self.rect()], 'parallax': self.parallax}
