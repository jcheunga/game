"""King's Road (urban, highway, night), Saltwake Docks (industrial, shipyard, swamp) and
Emberforge March (railyard, smelter, foundry) battlefield fallbacks."""
import math
import random

from mathutils import Vector

from rk import arch, env
from rk import dressing as P
from rk.palette import LANTERN_TEAL, ROT_CRIMSON

from menu_scenes.common import golden, rocks, trees, tufts

from . import kit as K
from .common import BF, back_band, field_height, ground, row, scatter_front


def _bushes():
    return [env.proto(f'bfbush{i}', lambda i=i: env.bush(70 + i, 0.9 + 0.3 * i)) for i in range(3)]


def _rocks(moss=0.3):
    return [env.proto(f'bfrock{i}{moss}', lambda i=i: env.rock(80 + i, 0.6, moss=moss)) for i in range(3)]


def _tufts(color=('6b7a34', 'a69a52')):
    return [env.proto(f'bftuft{i}{color[0]}', lambda i=i: env.grass_tuft(90 + i, 0.55, color)) for i in range(3)]


def _road_ground(name, grass=('5a6a32', '74803e', '9a9446'), dirt=('8a6c4a', '6a5038'), dry=0.3):
    return env.ground(name, grass=grass, dry='ad9658', dry_amount=dry, flowers=0.25, dirt=dirt)


def _house_row(bf, h, rng, style='warm', lit=0.6, gap=0.8, y_off=0.0, floors=(1, 2, 2), seed=0, M=None):
    """Houses side by side, fronts facing the field (their doors and ground floors fill the top band)."""
    y_min, _ = back_band(bf)
    x = bf.x0 - 6
    i = 0
    while x < bf.x1 + 6:
        w = rng.uniform(5.5, 7.5)
        d = rng.uniform(5.0, 6.5)
        cx = x + w / 2
        y = y_min + y_off + d / 2
        arch.house((cx, y, h(cx, y)), 0.0, w, d, floors=rng.choice(floors), seed=seed * 40 + i, style=style, lit=lit,
                   h1=rng.uniform(2.8, 3.2), M=M)
        x += w + gap
        i += 1


# ------------------------------------------------------------------ King's Road
def urban(scene):
    bf = BF()
    rng = random.Random(1)
    h = field_height(bf)
    ground(bf, _road_ground('Town square', grass=('6a6a48', '7a7a52', '8a845a'), dry=0.1), h,
           masks={'cobble': lambda x, y: 1.0 if (y > bf.D - 0.6 or y < 0.5) else 0.0})
    golden(scene, sun_az=228, sun_el=34, strength=4.6, haze_density=0.0, clouds=0.5, light_strength=0.65)
    _house_row(bf, h, rng, seed=1)
    M = P.pm()
    y_min, _ = back_band(bf)
    for x in row(5, bf.x0, bf.x1, rng, 0.2):
        P.lantern_post((x, y_min - 0.1, 0), 0.0, power=20)
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        P.barrel((x + 1.2, y_min + 0.1, 0), rng.uniform(0, 6), M=M)
        P.crate((x - 1.0, y_min + 0.2, 0), rng.uniform(0, 6), 0.7, M)
    # near edge: market wares and a cart under the frame, cobbles
    y0, _ = bf.front_y_range()
    for i, x in enumerate(row(3, bf.x0, bf.x1, rng, 0.3)):
        y = y0 + 1.2
        s = bf.fit(y, 2.6)
        if i == 1:
            P.cart((x, y, 0), rng.uniform(-0.3, 0.3), M, load='sacks') if s > 0.9 else None
        else:
            P.stall((x, y - 0.4, 0), math.pi, w=3.0, d=1.5, seed=i, M=M)
    scatter_front(bf, h, lambda: [env.proto('bfbarrel', lambda: _barrel_proto(M))], 6, seed=3, nominal_h=1.0,
                  y_range=(y0 + 2.4, -1.2))
    bf.camera()
    return dict(bloom=0.12, saturation=1.06)


def _barrel_proto(M):
    from rk import geo
    objs = P.barrel((0, 0, 0), 0.0, M=M)
    return geo.join([o for o in objs], 'Barrel proto')


def highway(scene):
    bf = BF()
    rng = random.Random(2)
    h = field_height(bf, back_rise=0.6, front_drop=0.2, seed=2)
    ground(bf, _road_ground('Kings road verge'), h,
           masks={'path': lambda x, y: 1 - env.smoothstep(2.0, 3.5, abs(y - bf.D / 2))})
    golden(scene, sun_az=232, sun_el=32, strength=4.6, haze_density=0.0, clouds=0.5, light_strength=0.65)
    M = P.pm()
    y_min, y_max = back_band(bf)
    # a low causeway wall with milestones, poplars and banners beyond
    arch.wall((bf.x0 - 6, y_min + 0.5), (bf.x1 + 6, y_min + 0.5), 1.8, 0.9, crenels=False)
    for x in row(3, bf.x0, bf.x1, rng, 0.2):
        P.banner_pole((x, y_min - 0.1, 0), 0.0, M['teal'], h=4.2, width=0.9, drop=1.8)
    trees(12, lambda r: (r.uniform(bf.x0 - 3, bf.x1 + 3), r.uniform(y_min + 1.4, y_min + 3.0)), h, seed=5,
          kinds=('cypress', 'olive', 'broad'), min_gap=3.0, scale=(0.8, 1.1))
    tufts(90, lambda r: (r.uniform(bf.x0 - 3, bf.x1 + 3), r.uniform(y_min, y_min + 2)), h, seed=6, scale=(1.0, 2.0))
    y0, _ = bf.front_y_range()
    P.fence((bf.x0 - 4, y0 + 1.6), (bf.x1 + 4, y0 + 1.4), 1.1, posts=2.2, height_fn=h)
    P.signpost((rng.uniform(-4, 4), y0 + 0.6, h(0, y0 + 0.6)), 0.3, M)
    scatter_front(bf, h, _bushes, 6, seed=4, nominal_h=1.2, y_range=(y0 - 0.5, y0 + 1.0), min_gap=3)
    scatter_front(bf, h, _rocks, 7, seed=5, nominal_h=0.6, y_range=(y0, -0.8))
    scatter_front(bf, h, _tufts, 60, seed=6, nominal_h=0.5, y_range=(y0, -0.4), min_gap=0.4)
    bf.camera()
    return dict(bloom=0.12, saturation=1.06)


def night_town(scene):
    bf = BF()
    rng = random.Random(3)
    h = field_height(bf)
    ground(bf, _road_ground('Night square', grass=('4a5440', '5a6448', '6a7050'), dry=0.1), h,
           masks={'cobble': lambda x, y: 1.0 if (y > bf.D - 0.6 or y < 0.5) else 0.0})
    golden(scene, sun_az=148, sun_el=50, strength=2.0, haze_density=0.0, clouds=0.8, light_strength=0.9,
           zenith='2a3458', horizon='d0805a', mid='c87a62', upper='5a5a80')
    scene.view_settings.exposure = 0.2
    M = P.pm()
    _house_row(bf, h, rng, lit=0.95, seed=3)
    y_min, _ = back_band(bf)
    for x in row(6, bf.x0, bf.x1, rng, 0.2):
        P.lantern_post((x, y_min - 0.1, 0), 0.0, power=120)
    y0, _ = bf.front_y_range()
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        y = y0 + 1.0
        P.gravestone((x, y, 0), rng.uniform(-0.3, 0.3), seed=int(x * 3), M=M)
        P.candle((x + 0.5, y - 0.3, 0), 0.25, 0.04, M, power=8)
    for x in row(3, bf.x0, bf.x1, rng, 0.4):
        P.brazier((x, y0 + 2.2, 0), M, power=180, h=0.8)
    P.fence((bf.x0 - 4, y0 + 0.2), (bf.x1 + 4, y0 + 0.2), 1.0, posts=2.4)
    bf.camera()
    return dict(bloom=0.24, bloom_threshold=0.55, saturation=1.05, sun_strength=2.0)


# ------------------------------------------------------------------ Saltwake Docks
def _quay(bf, h_back_water=True):
    """Field is a stone quay; water lies beyond the far edge and below the near edge."""
    def h(x, y):
        if y > bf.D + 0.9:
            return -1.2
        if y < -1.2:
            return -1.2
        return 0.0
    return h


def _quay_ground(name='Quay stones'):
    return env.ground(name, grass=('6a6658', '7a7466', '8a8474'), dry='8a8270', dry_amount=0.1, flowers=0.0,
                      cobble=('8a8478', '6e6a60'), cobble_scale=2.6)


def industrial(scene):
    bf = BF()
    rng = random.Random(5)
    h = _quay(bf)
    ground(bf, _quay_ground(), h, masks={'cobble': lambda x, y: 1.0})
    golden(scene, sun_az=240, sun_el=30, strength=4.0, haze_density=0.0, clouds=0.8, light_strength=0.8,
           zenith='5a6a80', horizon='d8c0a0', mid='e0d0b8', upper='a8b8c8')
    y_min, y_max = back_band(bf)
    K.water_plane(bf.x0 - 10, bf.x1 + 10, bf.D + 0.9, y_max + 20, z=-0.6)
    K.water_plane(bf.x0 - 10, bf.x1 + 10, -12, -1.2, z=-0.6, name='Near water')
    M = P.pm()
    for x in row(7, bf.x0, bf.x1, rng, 0.15):
        K.bollard((x, bf.D + 0.5, 0))
    for x in row(2, bf.x0, bf.x1, rng, 0.2):
        K.crane((x, bf.D + 0.4, 0), 0.0, M, h=7.0)
    for i, x in enumerate(row(3, bf.x0, bf.x1, rng, 0.3)):
        K.boat((x + 2.0, y_min + 1.8, -0.6), rng.uniform(-0.2, 0.2), L=6.0, M=M, sail=M['canvas'] if i % 2 else None)
    for x in row(5, bf.x0, bf.x1, rng, 0.3):
        K.rope_coil((x, bf.D + 0.1, 0))
    y0, _ = bf.front_y_range()
    for x in row(9, bf.x0, bf.x1, rng, 0.2):
        K.bollard((x, -0.9, 0))
    for i, x in enumerate(row(5, bf.x0, bf.x1, rng, 0.3)):
        K.boat((x, y0 + 1.2, -0.6), math.pi + rng.uniform(-0.2, 0.2), L=4.5, M=M, mast=False)
    bf.camera()
    return dict(bloom=0.12, saturation=1.02)


def shipyard(scene):
    bf = BF()
    rng = random.Random(6)
    h = field_height(bf)
    ground(bf, env.ground('Slipway', grass=('6a6050', '7a705e', '8a806a'), dry='8a7a60', dry_amount=0.2,
                          flowers=0.0, dirt=('6a5a44', '544636')), h, masks={'path': lambda x, y: 0.6})
    golden(scene, sun_az=235, sun_el=30, strength=4.2, haze_density=0.0, clouds=0.8, light_strength=0.75)
    M = P.pm()
    y_min, y_max = back_band(bf)
    K.hull_on_stocks((rng.uniform(-3, 3), y_min + 2.6, 0), 0.0, L=22.0, M=M)
    for x in row(3, bf.x0, bf.x1, rng, 0.3):
        K.log_stack((x, y_min + 0.4, 0), 0.0, L=4.0, r=0.28)
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        P.barrel((x, y_min - 0.1, 0), rng.uniform(0, 6), M=M)
    y0, _ = bf.front_y_range()
    for x in row(3, bf.x0, bf.x1, rng, 0.3):
        K.log_stack((x, y0 + 0.9, 0), 0.05, L=3.6, r=0.18)
    for x in row(5, bf.x0, bf.x1, rng, 0.3):
        K.rope_coil((x, -1.1, 0))
        P.crate((x + 1.4, y0 + 2.2, 0), rng.uniform(0, 6), 0.7, M)
    bf.camera()
    return dict(bloom=0.12, saturation=1.04)


def swamp(scene):
    bf = BF()
    rng = random.Random(7)
    h = _quay(bf)
    ground(bf, env.ground('Drowned quay', grass=('4a5a3c', '5a6644', '6a7050'), dry='6a6a4a', dry_amount=0.2,
                          flowers=0.0, cobble=('6a6a60', '565650'), wet=0.6), h,
           masks={'cobble': lambda x, y: 0.5})
    golden(scene, sun_az=235, sun_el=24, strength=3.0, haze_density=0.0, clouds=0.9, light_strength=0.8,
           zenith='4a5a66', horizon='a8a890', mid='b8b8a0', upper='7a8a90')
    y_min, y_max = back_band(bf)
    K.water_plane(bf.x0 - 10, bf.x1 + 10, bf.D + 0.9, y_max + 20, z=-0.5, deep='18302a', shallow='3a5a48')
    K.water_plane(bf.x0 - 10, bf.x1 + 10, -12, -1.2, z=-0.5, deep='18302a', shallow='3a5a48', name='Near water')
    M = P.pm()
    for i, x in enumerate(row(3, bf.x0, bf.x1, rng, 0.3)):
        K.boat((x, y_min + 1.6, -0.5), rng.uniform(-0.5, 0.5), L=6.0, M=M, sunk=0.8, sail=None)
    for x in row(10, bf.x0, bf.x1, rng, 0.4):
        from rk import geo
        o = geo.cylinder('Piling', 0.18, 3.2 * rng.uniform(0.6, 1.0), (x, y_min + rng.uniform(1.0, 3.0), 0.3),
                         P.pm()['wood_dark'], env.C['Props'], 8, bevel=0.02)
    for x in row(8, bf.x0, bf.x1, rng, 0.4):
        K.reeds((x, y_min + rng.uniform(0.8, 3.0), -0.4), 16, 1.6, seed=int(x * 7))
        K.reeds((x + 1.0, rng.uniform(-3.2, -1.4), -0.4), 12, 1.2, seed=int(x * 9))
    trees(6, lambda r: (r.uniform(bf.x0, bf.x1), r.uniform(y_min + 2, y_max)), h, seed=8, kinds=('dead',), min_gap=5)
    bf.camera()
    return dict(bloom=0.12, saturation=0.98)


# ------------------------------------------------------------------ Emberforge March
def _forge_ground(name):
    return env.ground(name, grass=('5a4e40', '6a5a48', '7a6650'), dry='7a6a54', dry_amount=0.4, flowers=0.0,
                      dirt=('5a4a3a', '3e322a'), ash=0.6, ash_color='2e2826')


def railyard(scene):
    bf = BF()
    rng = random.Random(9)
    h = field_height(bf)
    ground(bf, _forge_ground('Coal yard'), h)
    golden(scene, sun_az=230, sun_el=26, strength=3.6, haze_density=0.0, clouds=0.9, light_strength=0.7,
           zenith='4a4a58', horizon='d89a6a', mid='d8aa80', upper='8a8a96')
    M = P.pm()
    y_min, y_max = back_band(bf)
    K.rails(bf.x0 - 8, bf.x1 + 8, y_min + 0.6, 0.0, M)
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        K.ore_cart((x, y_min + 0.6, 0.2), 0.0, M, load='coal' if rng.random() < 0.6 else 'ore')
    for x in row(2, bf.x0, bf.x1, rng, 0.25):
        K.chimney_stack((x, y_min + 3.0, 0), 12.0, 0.9, M)
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        P.coal_heap((x, y_min + 2.0, 0), 1.4, M, seed=int(x))
    y0, _ = bf.front_y_range()
    K.rails(bf.x0 - 8, bf.x1 + 8, y0 + 1.4, 0.0, M)
    for x in row(2, bf.x0, bf.x1, rng, 0.3):
        K.ore_cart((x, y0 + 1.4, 0.2), 0.0, M)
    for x in row(5, bf.x0, bf.x1, rng, 0.3):
        P.coal_heap((x, -1.4, 0), 0.6, M, seed=int(x) + 5)
    bf.camera()
    return dict(bloom=0.16, saturation=1.04)


def smelter(scene):
    bf = BF()
    rng = random.Random(10)
    h = field_height(bf)
    ground(bf, _forge_ground('Smelter row'), h)
    golden(scene, sun_az=232, sun_el=24, strength=3.6, haze_density=0.0, clouds=0.9, light_strength=0.7,
           zenith='4a4450', horizon='e0905a', mid='e0a070', upper='8a8090')
    M = P.pm()
    y_min, y_max = back_band(bf)
    for x in row(4, bf.x0, bf.x1, rng, 0.15):
        K.furnace((x, y_min + 1.5, 0), 0.0, M, w=3.6, h=4.6, power=700)
    for x in row(4, bf.x0, bf.x1, rng, 0.2):
        K.slag_heap((x + 3.5, y_min + 0.4, 0), 1.0, seed=int(x))
    y0, _ = bf.front_y_range()
    for x in row(5, bf.x0, bf.x1, rng, 0.3):
        K.slag_heap((x, y0 + 0.8, 0), 1.3, seed=int(x) + 3)
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        P.anvil((x, y0 + 2.4, 0), rng.uniform(0, 6), M, s=0.9)
        P.sparks((x, y0 + 2.4, 1.0), 20, 0.6, seed=int(x))
    bf.camera()
    return dict(bloom=0.22, bloom_threshold=0.55, saturation=1.06)


def foundry(scene):
    bf = BF()
    rng = random.Random(11)
    h = field_height(bf)
    ground(bf, _forge_ground('Cinder causeway'), h, masks={'cobble': lambda x, y: 1.0 if y > bf.D - 0.4 else 0.0})
    golden(scene, sun_az=228, sun_el=26, strength=3.6, haze_density=0.0, clouds=0.9, light_strength=0.7,
           zenith='4a4450', horizon='e0905a', mid='e0a070', upper='8a8090')
    M = P.pm()
    AM = arch.mats('grey')
    AM['stone'] = env.masonry('Foundry walls', '6e5e50', '5a4c40', '2e2620', soot=0.7)
    y_min, y_max = back_band(bf)
    x = bf.x0 - 6
    i = 0
    while x < bf.x1 + 6:
        w = rng.uniform(7, 9)
        arch.house((x + w / 2, y_min + 0.2 + 3.5, 0), 0.0, w, 7.0, floors=1, h1=4.2, seed=60 + i, style='grey',
                   lit=1.0, M=AM, chimney=False)
        x += w + 2.4
        i += 1
    for x in row(3, bf.x0, bf.x1, rng, 0.2):
        K.chimney_stack((x, y_min + 8.0, 0), 14.0, 1.0, M)
    for x in row(6, bf.x0, bf.x1, rng, 0.3):
        P.brazier((x, y_min - 0.1, 0), M, power=150, h=0.9)
    y0, _ = bf.front_y_range()
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        K.slag_heap((x, y0 + 0.6, 0), 1.2, seed=int(x) + 7)
        P.anvil((x + 2.6, y0 + 2.0, 0), rng.uniform(0, 6), M, s=0.9)
    for x in row(3, bf.x0, bf.x1, rng, 0.3):
        P.barrel((x, -1.6, 0), 0, M=M)
    bf.camera()
    return dict(bloom=0.22, bloom_threshold=0.55, saturation=1.06)
