"""Sunfall Steppe battlefields: grassland, siegecamp, waystation."""
import random

from rk import arch, env
from rk import dressing as P
from rk.palette import ROT_CRIMSON

from menu_scenes.common import golden, rocks, trees, tufts

from .common import BF, field_height, ground, row


def _steppe_ground(name='Steppe field'):
    return env.ground(name, grass=('6e7038', '8e8a44', 'b09e56'), dry='c2a460', dry_amount=0.65, flowers=0.35,
                      flower_colors=('efe2c0', 'e0b040', 'c86a3a'), dirt=('8a6a44', '6a5034'))


def _front_litter(bf, height, rng, n_rocks=10, n_tufts=90, seed=0):
    y0, y1 = bf.front_y_range()
    rocks(n_rocks, lambda r: (r.uniform(bf.x0 - 2, bf.x1 + 2), r.uniform(y0, -1.2)), height, seed=seed + 1,
          size=(0.3, 0.8), moss=0.2)
    tufts(n_tufts, lambda r: (r.uniform(bf.x0 - 2, bf.x1 + 2), r.uniform(y0, -0.6)), height, seed=seed + 2,
          scale=(1.0, 2.2), color=('7a7a3a', 'c0a860'))


def grassland(scene):
    bf = BF()
    rng = random.Random(33)
    h = field_height(bf, back_rise=0.8, front_drop=0.3, seed=3)
    ground(bf, _steppe_ground(), h)
    golden(scene, sun_az=220, sun_el=30, strength=4.6, haze_density=0.0, clouds=0.5, light_strength=0.65)
    by = bf.back_min_y()
    P.dry_wall((bf.x0 - 4, by + 0.5), (bf.x1 + 4, by + 0.7), 0.95, seed=2, height_fn=h)
    for i, x in enumerate(row(6, bf.x0, bf.x1, rng, 0.25)):
        y = by + rng.uniform(1.0, 2.0)
        if i % 3 == 0:
            P.haystack((x, y, h(x, y) - 0.1), rng.uniform(1.2, 1.5), rng.uniform(2.0, 2.4), seed=i)
        elif i % 3 == 1:
            P.standing_stone((x, y, h(x, y)), rng.uniform(2.2, 3.0), seed=i)
    trees(12, lambda r: (r.uniform(bf.x0 - 3, bf.x1 + 3), r.uniform(by + 1.6, by + 3.5)), h, seed=4,
          kinds=('olive', 'broad', 'cypress'), min_gap=3.5, scale=(0.7, 1.0))
    trees(8, lambda r: (r.uniform(bf.x0 - 3, bf.x1 + 3), r.uniform(by + 0.9, by + 1.6)), h, seed=6,
          kinds=('bush',), min_gap=2.5, scale=(0.8, 1.3))
    tufts(140, lambda r: (r.uniform(bf.x0 - 3, bf.x1 + 3), r.uniform(by - 0.2, by + 3)), h, seed=5, scale=(1.2, 2.4),
          color=('7a7a3a', 'c0a860'))
    # near edge: tall ash-grass, field stones, a wheel and a spear left from a raid
    _front_litter(bf, h, rng, seed=8)
    y0, _ = bf.front_y_range()
    trees(6, lambda r: (r.uniform(bf.x0 - 2, bf.x1 + 2), r.uniform(y0 - 0.5, y0 + 1.2)), h, seed=9, kinds=('bush',),
          min_gap=3, scale=(0.9, 1.3))
    for x in row(3, bf.x0, bf.x1, rng, 0.4):
        y = rng.uniform(y0 + 0.6, -2.4)
        s = bf.fit(y, 1.15)
        P.wheel((x, y, h(x, y) + 0.1), 0.55 * s, 0.1, axis='X')
    bf.camera()
    return dict(bloom=0.12, saturation=1.08)


def siegecamp(scene):
    bf = BF()
    rng = random.Random(34)
    h = field_height(bf, back_rise=0.6, seed=4)
    ground(bf, _steppe_ground('Siege ring'), h, masks={'path': lambda x, y: 0.4})
    golden(scene, sun_az=228, sun_el=22, strength=4.4, haze_density=0.0, clouds=0.8, light_strength=0.65,
           horizon='f0a060', mid='f0c08a')
    M = P.pm()
    by = bf.back_min_y()
    for i, x in enumerate(row(4, bf.x0, bf.x1, rng, 0.2)):
        P.pavilion((x, by + 2.6, h(x, by + 2.6)), rng.uniform(0, 6), r=2.2, a=ROT_CRIMSON, b='3a3030', M=M,
                   door=False, count=12)
    for i, x in enumerate(row(3, bf.x0, bf.x1, rng, 0.3)):
        P.ridge_tent((x + 2.5, by + 0.9, h(x + 2.5, by + 0.9)), rng.uniform(-0.3, 0.3), L=2.8, Wd=2.2, h=1.8,
                     mat=env.stripes('Raider tent', a=ROT_CRIMSON, b='5a4a3a', count=1.4, axis='x'), M=M)
    for x in row(3, bf.x0, bf.x1, rng, 0.3):
        P.banner_pole((x - 2.0, by + 0.2, 0), 0.0, M['crimson'], 'skull', h=4.4, width=0.9, drop=1.8)
    y0, _ = bf.front_y_range()
    for x in row(3, bf.x0, bf.x1, rng, 0.3):
        P.stake_line((x - 1.8, y0 + 1.4), (x + 1.8, y0 + 1.6), M, spacing=0.4, h=1.7, seed=int(x))
        P.brazier((x + 2.6, y0 + 2.4, 0), M, power=150, h=0.8)
    _front_litter(bf, h, rng, n_rocks=6, n_tufts=60, seed=12)
    bf.camera()
    return dict(bloom=0.14, saturation=1.06)


def waystation(scene):
    bf = BF()
    rng = random.Random(35)
    h = field_height(bf, back_rise=0.4, seed=5)
    ground(bf, _steppe_ground('Burned waystation'), h,
           masks={'path': lambda x, y: 1 - env.smoothstep(1.5, 3.0, abs(y - bf.D / 2))})
    golden(scene, sun_az=225, sun_el=20, strength=4.2, haze_density=0.0, clouds=0.8, light_strength=0.65,
           horizon='f0a060', mid='f0c08a')
    M = P.pm()
    AM = arch.mats('warm')
    AM['plaster'] = env.plaster('Scorched plaster', 'a89478', '3a2e24')
    AM['timber'] = P.S.wood('241a14', name='Charred beams', dark=0.4)
    by = bf.back_min_y()
    x = bf.x0 - 6
    i = 0
    while x < bf.x1 + 6:
        w = rng.uniform(6, 8)
        arch.house((x + w / 2, by + 0.1 + 3.0, 0), 0.0, w, 6.0, floors=rng.choice((1, 2)), seed=90 + i, M=AM, lit=0.2)
        x += w + 2.5
        i += 1
    P.smoke_column((rng.uniform(-6, 6), by + 3.0, 4.0), height=14, radius=1.0, density=0.45, color='4a4440',
                   drift=(4, 4))
    for x in row(3, bf.x0, bf.x1, rng, 0.3):
        P.cart((x + 3.5, by - 0.2, 0), rng.uniform(-0.3, 0.3), M, load='barrels')
    y0, _ = bf.front_y_range()
    P.fence((bf.x0 - 4, y0 + 1.5), (bf.x1 + 4, y0 + 1.3), 1.1, posts=2.4, height_fn=h)
    for x in row(3, bf.x0, bf.x1, rng, 0.3):
        P.campfire((x, y0 + 2.6, h(x, y0 + 2.6)), 0.5, M, power=200, smoke=False, seed=int(x))
    _front_litter(bf, h, rng, n_rocks=6, n_tufts=60, seed=13)
    bf.camera()
    return dict(bloom=0.14, saturation=1.04)
