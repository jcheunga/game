"""Mire of Saints (marsh, chapel, ferry), Gloamwood Verge (grove, timberroad, witchcircle) and
Crownfall Citadel (bridgefort, breachyard, innerkeep) battlefield fallbacks."""
import math
import random

from rk import arch, env, geo
from rk import dressing as P
from rk.palette import LANTERN_TEAL, PLAGUE, ROT_CRIMSON

from menu_scenes.common import golden, night, rocks, trees, tufts

from . import kit as K
from .common import BF, back_band, field_height, ground, row, scatter_front, shafts


# ------------------------------------------------------------------ Mire of Saints
def _mire_ground(name):
    return env.ground(name, grass=('5a6a3e', '6e7a48', '8e8a56'), dry='a89a62', dry_amount=0.45, flowers=0.15,
                      flower_colors=('e8e0c0', 'd8c870', 'b0a8d0'), dirt=('6a5a40', '54462e'), wet=0.5)


def _mire_light(scene):
    golden(scene, sun_az=148, sun_el=50, strength=3.8, haze_density=0.0, clouds=0.7, light_strength=1.1,
           zenith='5a7488', horizon='d8d4b0', mid='e0dcc0', upper='9ab0b4')
    scene.view_settings.exposure = 0.15


def _banked(bf, water_back=True, water_front=True):
    def h(x, y):
        if water_back and y > bf.D + 0.8:
            return -0.7 + env.fbm(x, y, 0.3, 2, 4) * 0.2
        if water_front and y < -1.6:
            return -0.7 + env.fbm(x, y, 0.3, 2, 5) * 0.2
        return env.fbm(x, y, 0.15, 2, 6) * 0.08
    return h


def _bog_water(bf, y_min, y_max, z=-0.35):
    mat = env.water('Mire shallows', '1e2a20', '46543a')
    K.water_plane(bf.x0 - 12, bf.x1 + 12, bf.D + 0.5, y_max + 20, z=z, name='Bog water')
    K.water_plane(bf.x0 - 12, bf.x1 + 12, -14, -1.4, z=z, name='Near bog')
    for o in env.C['Terrain'].objects:
        if o.name.startswith(('Bog water', 'Near bog')):
            o.data.materials.clear()
            o.data.materials.append(mat)


def marsh(scene):
    bf = BF()
    rng = random.Random(27)
    h = _banked(bf)
    ground(bf, _mire_ground('Bog causeway'), h)
    _mire_light(scene)
    y_min, y_max = back_band(bf)
    _bog_water(bf, y_min, y_max)
    M = P.pm()
    for x in row(8, bf.x0, bf.x1, rng, 0.4):
        _golden_reeds(x, y_min + rng.uniform(0.4, 2.0), -0.3, int(x * 3), 20, 1.8)
    _lilies(bf, rng, (y_min + 0.2, y_max + 1), 20)
    trees(5, lambda r: (r.uniform(bf.x0 - 2, bf.x1 + 2), r.uniform(y_min + 1.0, y_max)), h, seed=6, kinds=('dead',),
          min_gap=6, scale=(0.5, 0.75))
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        P.lantern_post((x, y_min - 0.2, 0), 0.0, power=40, color='ffb04a', h=2.2)
    y0, _ = bf.front_y_range()
    for x in row(8, bf.x0, bf.x1, rng, 0.4):
        _golden_reeds(x, rng.uniform(y0 + 0.4, -2.0), -0.3, int(x * 5), 14, 1.3)
    _lilies(bf, rng, (y0, -1.8), 16)
    shafts(bf, 0.012, 'eee6c8', 6.0)
    bf.camera()
    return dict(bloom=0.14, saturation=1.04, sun_strength=3.8)


def chapel(scene):
    bf = BF()
    rng = random.Random(28)
    h = _banked(bf)
    ground(bf, _mire_ground('Chapel causeway'), h, masks={'cobble': lambda x, y: 0.45})
    _mire_light(scene)
    y_min, y_max = back_band(bf)
    _bog_water(bf, y_min, y_max, z=-0.25)
    M = P.pm()
    for x in row(5, bf.x0 - 2, bf.x1 + 2, rng, 0.05):
        arch.column((x, y_min + 1.2, -0.3), 5.0, 0.36, env.smooth_stone('9a948a', name='Drowned stone', moss=0.7))
    for x in row(2, bf.x0, bf.x1, rng, 0.2):
        K.bell_frame((x + 2.4, y_min + 1.8, -0.3), 0.0, M, h=3.4)
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        K.pew((x, y_min + 0.6, -0.45), math.pi + rng.uniform(-0.3, 0.3), 2.4, M, broken=0.2)
    for x in row(6, bf.x0, bf.x1, rng, 0.4):
        _golden_reeds(x, y_min + 2.0, -0.3, int(x * 7), 12, 1.4)
    _lilies(bf, rng, (y_min, y_max), 14)
    y0, _ = bf.front_y_range()
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        K.pew((x, y0 + 1.2, -0.45), rng.uniform(-0.3, 0.3), 2.4, M, broken=-0.3)
        K.candle_cluster((x + 2.0, -1.2, 0), 5, seed=int(x), power=12)
    for x in row(8, bf.x0, bf.x1, rng, 0.4):
        _golden_reeds(x, rng.uniform(y0, -2.0), -0.3, int(x * 11), 10, 1.2)
    shafts(bf, 0.012, 'eee6c8', 6.0)
    bf.camera()
    return dict(bloom=0.14, saturation=1.04, sun_strength=3.8)


def ferry(scene):
    bf = BF()
    rng = random.Random(29)
    h = _banked(bf)
    ground(bf, env.planks('Ferry deck', '7a6448', board=0.3, length=3.0), h)
    _mire_light(scene)
    y_min, y_max = back_band(bf)
    _bog_water(bf, y_min, y_max)
    M = P.pm()
    from rk import geo
    for x in row(9, bf.x0, bf.x1, rng, 0.15):
        geo.cylinder('Ferry post', 0.16, 2.6, (x, bf.D + 0.5, 0.4), M['wood_dark'], env.C['Props'], 8, bevel=0.02)
    for i in range(2):
        P.bunting((bf.x0 - 6, bf.D + 0.5, 1.6 - i * 0.6), (bf.x1 + 6, bf.D + 0.5, 1.6 - i * 0.6), sag=0.3, count=1,
                  colors=[M['iron']], size=0.01)
    for i, x in enumerate(row(2, bf.x0, bf.x1, rng, 0.2)):
        K.boat((x, y_min + 2.0, -0.4), rng.uniform(-0.2, 0.2), L=7.0, M=M, sail=M['crimson'], sunk=0.1)
    for x in row(6, bf.x0, bf.x1, rng, 0.4):
        _golden_reeds(x, y_min + rng.uniform(0.6, 2.6), -0.3, int(x * 3), 12, 1.5)
    _lilies(bf, rng, (y_min, y_max), 12)
    y0, _ = bf.front_y_range()
    for x in row(9, bf.x0, bf.x1, rng, 0.15):
        geo.cylinder('Ferry post', 0.16, 1.6, (x, -1.3, -0.1), M['wood_dark'], env.C['Props'], 8, bevel=0.02)
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        K.rope_coil((x, -0.6, 0))
        P.lantern_post((x + 2.4, -0.9, 0), math.pi, power=30, color='ffb04a', h=1.6, arm=False)
    for x in row(8, bf.x0, bf.x1, rng, 0.4):
        _golden_reeds(x, rng.uniform(y0, -2.0), -0.3, int(x * 13), 10, 1.2)
    _lilies(bf, rng, (y0, -1.8), 12)
    shafts(bf, 0.012, 'eee6c8', 6.0)
    bf.camera()
    return dict(bloom=0.14, saturation=1.04, sun_strength=3.8)


def _forest_ground(name):
    return env.ground(name, grass=('3e5228', '587034', '86904a'), dry='9a8a50', dry_amount=0.3, flowers=0.45,
                      flower_colors=('f0ecd8', 'e8d070', 'b8a8e0'), dirt=('6a5438', '503e2a'))


def _forest_light(scene):
    golden(scene, sun_az=148, sun_el=50, strength=4.4, haze_density=0.0, clouds=0.6, light_strength=0.9,
           zenith='4a6a84', horizon='d8d0b0', mid='e0d8c0', upper='8aa4b4')
    scene.view_settings.exposure = 0.1


def _treeline(bf, h, y_min, y_max, seed, dense=9):
    """Open woodland edge: spaced trunks with sunlit grass and ferns between, canopies above the frame."""
    # canopy line just beyond the frame throws dappled shade into the band; a few trunks stand inside it
    trees(dense + 3, lambda r: (r.uniform(bf.x0 - 3, bf.x1 + 3), r.uniform(y_max + 0.5, y_max + 4.5)), h, seed=seed,
          kinds=('broad', 'conifer'), min_gap=4.0, scale=(0.9, 1.2), palette=FOREST)
    trees(3, lambda r: (r.uniform(bf.x0, bf.x1), r.uniform(y_min + 1.2, y_max - 0.5)), h, seed=seed + 7,
          kinds=('broad',), min_gap=8.0, scale=(0.8, 1.0), palette=FOREST)
    ferns = [env.proto(f'bffern{i}', lambda i=i: env.bush(130 + i, 0.7, palette=('3a5a26', '5e8034', '9ab04a')))
             for i in range(3)]
    for x in row(11, bf.x0 - 2, bf.x1 + 2, rng_for(seed), 0.45):
        y = rng_for(seed + int(x * 10)).uniform(y_min - 0.2, y_min + 1.6)
        env.inst(ferns[int(abs(x * 7)) % 3], (x, y, h(x, y)), x, rng_for(seed + 3).uniform(0.7, 1.1), env.C['Vegetation'])


def grove(scene):
    bf = BF()
    rng = random.Random(37)
    h = field_height(bf, back_rise=0.4, seed=10)
    ground(bf, _forest_ground('Thorn verge'), h)
    _forest_light(scene)
    y_min, y_max = back_band(bf)
    _treeline(bf, h, y_min, y_max, 30)
    for x in row(5, bf.x0, bf.x1, rng, 0.4):
        K.mushrooms((x, y_min + rng.uniform(-0.2, 0.6), h(x, y_min)), 7, seed=int(x * 3))
    for x in row(2, bf.x0, bf.x1, rng, 0.3):
        P.stake_line((x - 1.6, y_min - 0.1), (x + 1.6, y_min + 0.1), spacing=0.35, h=1.4, seed=int(x))
    y0, _ = bf.front_y_range()
    scatter_front(bf, h, lambda: [env.proto(f'bfgfern{i}', lambda i=i: env.bush(140 + i, 0.9,
                                                                                 palette=('3a5a26', '5e8034', '9ab04a')))
                                  for i in range(3)], 7, seed=9, nominal_h=1.1, min_gap=3.0)
    for x in row(4, bf.x0, bf.x1, rng, 0.4):
        K.mushrooms((x, rng.uniform(y0 + 0.5, -1.0), 0), 6, seed=int(x * 5))
        K.stump((x + 2.0, rng.uniform(y0 + 0.5, -1.4), 0))
    tufts(90, lambda r: (r.uniform(bf.x0 - 2, bf.x1 + 2), r.uniform(y0, -0.6)), h, seed=4, scale=(1.0, 1.8),
          color=('5a7a34', 'a8b060'))
    shafts(bf, 0.016, 'f2e6c8', 9.0)
    bf.camera()
    return dict(bloom=0.16, saturation=1.06, sun_strength=4.4)


def timberroad(scene):
    bf = BF()
    rng = random.Random(38)
    h = field_height(bf, back_rise=0.4, seed=11)
    ground(bf, _forest_ground('Blackbark road'), h,
           masks={'path': lambda x, y: 1 - env.smoothstep(2.0, 3.6, abs(y - bf.D / 2))})
    _forest_light(scene)
    M = P.pm()
    y_min, y_max = back_band(bf)
    _treeline(bf, h, y_min + 1.6, y_max, 32, dense=8)
    for x in row(3, bf.x0, bf.x1, rng, 0.25):
        K.log_stack((x, y_min + 0.6, h(x, y_min)), 0.0, L=4.4, r=0.26)
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        K.stump((x + 2.6, y_min + 0.1, h(x, y_min)))
    y0, _ = bf.front_y_range()
    for x in row(3, bf.x0, bf.x1, rng, 0.3):
        K.log_stack((x, y0 + 1.1, 0), 0.05, L=3.6, r=0.2)
    for x in row(5, bf.x0, bf.x1, rng, 0.4):
        K.stump((x + 1.6, rng.uniform(y0 + 2.0, -1.0), 0))
    tufts(80, lambda r: (r.uniform(bf.x0 - 2, bf.x1 + 2), r.uniform(y0, -0.6)), h, seed=5, scale=(1.0, 1.8),
          color=('5a7a34', 'a8b060'))
    shafts(bf, 0.016, 'f2e6c8', 9.0)
    bf.camera()
    return dict(bloom=0.14, saturation=1.06, sun_strength=4.4)


def witchcircle(scene):
    bf = BF()
    rng = random.Random(39)
    h = field_height(bf, back_rise=0.3, seed=12)
    ground(bf, _forest_ground('Witch circle'), h)
    golden(scene, sun_az=148, sun_el=50, strength=3.0, haze_density=0.0, clouds=0.8, light_strength=0.8,
           zenith='3a4a6a', horizon='c8a888', mid='d0b49a', upper='7a7a98')
    scene.view_settings.exposure = 0.1
    M = P.pm()
    y_min, y_max = back_band(bf)
    _treeline(bf, h, y_min + 1.6, y_max, 34, dense=8)
    for i, x in enumerate(row(5, bf.x0, bf.x1, rng, 0.15)):
        P.rune_stone((x, y_min + 0.4, h(x, y_min)), 0.0, h=2.8, seed=i, power=50, color='9cf25c')
    for x in row(6, bf.x0, bf.x1, rng, 0.4):
        K.mushrooms((x, y_min - 0.1, h(x, y_min)), 7, seed=int(x * 3), color='9cf25c', glow=5.0)
    y0, _ = bf.front_y_range()
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        K.ward_sigil((x, y0 + 1.4, 0.01), 0.9, '9cf25c', power=30)
        K.candle_cluster((x + 2.2, y0 + 2.4, 0), 5, seed=int(x), power=10)
        P.bones_pile((x - 2.0, -1.4, 0), 0.5, 6, M, seed=int(x), skulls=1)
    shafts(bf, 0.016, 'd8e0c8', 9.0)
    bf.camera()
    return dict(bloom=0.22, bloom_threshold=0.58, saturation=1.04, sun_strength=3.0)


def _citadel_ground(name):
    return env.ground(name, grass=('5a5a50', '66665a', '726e62'), dry='7a7468', dry_amount=0.2, flowers=0.0,
                      cobble=('7e7a72', '666258'), cobble_scale=2.6, ash=0.4, ash_color='3e3a36')


def _ash_light(scene):
    golden(scene, sun_az=228, sun_el=24, strength=3.2, haze_density=0.0, clouds=0.95, light_strength=0.85,
           zenith='4a4652', horizon='c08a6a', mid='c8a088', upper='7a7680')


def bridgefort(scene):
    bf = BF()
    rng = random.Random(42)

    def h(x, y):
        if y > bf.D + 1.2 or y < -1.6:
            return -3.0
        return 0.0
    ground(bf, _citadel_ground('Bridge deck'), h, masks={'cobble': lambda x, y: 1.0})
    _ash_light(scene)
    y_min, y_max = back_band(bf)
    K.water_plane(bf.x0 - 12, bf.x1 + 12, bf.D + 1.0, y_max + 30, z=-2.6, deep='2a3438', shallow='4a5a5a',
                  name='Moat')
    K.water_plane(bf.x0 - 12, bf.x1 + 12, -16, -1.5, z=-2.6, deep='2a3438', shallow='4a5a5a', name='Near moat')
    AM = arch.mats('grey')
    M = P.pm()
    arch.wall((bf.x0 - 8, bf.D + 0.6), (bf.x1 + 8, bf.D + 0.6), 1.2, 0.7, M=AM)
    for x in row(2, bf.x0, bf.x1, rng, 0.15):
        arch.tower((x, y_min + 3.4, -3.0), 3.0, 14, 'crenel', M=AM, seed=int(x), banner=M['crimson'])
    arch.wall((bf.x0 - 10, y_min + 3.4), (bf.x1 + 10, y_min + 3.4), 9.0, 2.4, M=AM, base_z=-3.0)
    y0, _ = bf.front_y_range()
    arch.wall((bf.x0 - 8, -1.0), (bf.x1 + 8, -1.0), 0.9, 0.7, M=AM, crenels=False)
    for x in row(5, bf.x0, bf.x1, rng, 0.3):
        K.rubble((x, y0 + 0.6, -3.0), 1.2, 6, seed=int(x))
    bf.camera()
    return dict(bloom=0.14, saturation=0.98)


def breachyard(scene):
    bf = BF()
    rng = random.Random(43)
    h = field_height(bf, back_rise=0.3, seed=13)
    ground(bf, _citadel_ground('Breach yard'), h, masks={'cobble': lambda x, y: 0.5})
    _ash_light(scene)
    AM = arch.mats('grey')
    M = P.pm()
    y_min, y_max = back_band(bf)
    for x0, x1 in ((bf.x0 - 10, bf.x0 + 4), (bf.x0 + 9, bf.x1 - 4), (bf.x1 + 1, bf.x1 + 12)):
        arch.wall((x0, y_min + 1.6), (x1, y_min + 1.6), rng.uniform(3.0, 6.0), 2.4, M=AM)
    for x in row(4, bf.x0, bf.x1, rng, 0.2):
        K.rubble((x, y_min + 0.4, 0), 2.0, 16, seed=int(x))
    P.catapult_wreck((rng.uniform(-3, 3), y_min + 1.0, 0), 0.2, M)
    P.smoke_column((rng.uniform(-6, 6), y_min + 2.0, 0), height=14, radius=0.9, density=0.5, color='4a4440',
                   drift=(4, 4))
    y0, _ = bf.front_y_range()
    for x in row(5, bf.x0, bf.x1, rng, 0.3):
        K.rubble((x, y0 + 0.8, 0), 1.4, 10, seed=int(x) + 3)
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        P.stuck_weapon((x + 1.8, rng.uniform(-3.2, -2.0), 0), rng.choice(['sword', 'axe']), M, seed=int(x))
        P.fallen_shield((x - 1.0, -1.2, 0), M, seed=int(x), emblem=rng.choice(['lantern', 'skull']))
    bf.camera()
    return dict(bloom=0.14, saturation=0.98)


def innerkeep(scene):
    bf = BF()
    rng = random.Random(44)
    h = field_height(bf)
    ground(bf, _citadel_ground('Inner ring'), h, masks={'cobble': lambda x, y: 1.0})
    _ash_light(scene)
    AM = arch.mats('grim')
    M = P.pm()
    y_min, y_max = back_band(bf)
    arch.slab((0, y_min + 1.6, 4.5), (1, 0, 0), (0, 0, 1), bf.W + 16, 9.0, 2.2, AM['stone'], [], 'Keep wall')
    for x in row(3, bf.x0, bf.x1, rng, 0.1):
        P.hanging_banner((x, y_min + 0.4, 5.6), 1.4, 4.4, M['crimson'], M, tails=True)
    for x in row(3, bf.x0, bf.x1, rng, 0.1):
        P.torch((x + 3.4, y_min + 0.45, 2.2), M=M, wall=(0, -1, 0), power=70)
        P.statue((x + 3.4, y_min - 0.2, 0), 0.0, h=2.2, M=M, plinth=True,
                 mat=env.smooth_stone('5a5650', name='Black marble', veins=0.3))
    y0, _ = bf.front_y_range()
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        P.brazier((x, y0 + 2.4, 0), M, power=150, h=0.8)
        K.rubble((x + 2.4, y0 + 0.7, 0), 1.0, 7, seed=int(x))
    for x in row(3, bf.x0, bf.x1, rng, 0.3):
        P.torn_banner((x + 1.2, rng.uniform(y0 + 0.4, y0 + 1.2), 0), M['crimson'], M, h=3.2, seed=int(x))
    bf.camera()
    return dict(bloom=0.18, saturation=0.98)
def _lilies(bf, rng, ys, n=24):
    pad = env.mat_cached(('lilypad',), lambda: P.S.flat('5a7a3a', name='Lily pad', rough=0.5))
    flower = env.mat_cached(('lilyflower',), lambda: P.S.flat('f0e8d8', name='Lily flower', rough=0.5))
    from rk import geo
    for i in range(n):
        x, y = rng.uniform(bf.x0 - 2, bf.x1 + 2), rng.uniform(*ys)
        r = rng.uniform(0.12, 0.26)
        geo.cylinder('Lily pad', r, 0.02, (x, y, -0.32), pad, env.C['Props'], 12, bevel=0)
        if rng.random() < 0.3:
            geo.sphere('Lily', 0.08, (x + 0.1, y, -0.27), flower, env.C['Props'], 8, 5, scale=(1, 1, 0.6))


def _golden_reeds(x, y, z, seed, n=18, h=1.6):
    return K.reeds((x, y, z), n, h, seed=seed, color='c8b068')


FOREST = ('2e4424', '4a6630', '7e9440')


def rng_for(seed):
    return random.Random(seed)


