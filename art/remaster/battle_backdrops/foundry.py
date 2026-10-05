"""Emberforge March: a cinder yard at a smoky dusk. Ore carts, coal and anvils line the rails behind the road;
beyond them brick foundries, glowing furnaces and smoking stacks; far off, a forge town under black peaks."""
import math
import random

from rk import arch, env
from rk import dressing as P
from battle_scenes import kit as K

from . import common as CM

MOOD = dict(zenith='302c46', horizon='f0a460', mid='eaa070', upper='6a5672', clouds=0.75, cloud_lit='ffb47a',
            cloud_dark='4a3842', cloud_scale=1.1, sun='ffbc7e', strength=3.9, sky_light=0.55, glow='ffb060',
            glow_amount=1.15, glow_el=4.0)
LOOK = dict(far=dict(saturation=1.05, bloom=0.2, split=0.1), mid=dict(saturation=1.02, bloom=0.12, split=0.1),
            near=dict(saturation=1.04, split=0.12, contrast=1.03, bloom=0.1))
FOG = dict(far_dist=1300.0, mid=(0.24, 0.005, 0.62))
CINDER = dict(grass=('46423e', '56524c', '68625a'), dry='68625a', dirt=('3a3430', '2a2622'))


def _dark_rock():
    return env.aerial_mat('Black peaks', 'ffffff', 1e7, rock=('3a3436', '524a4a'), green='4a4240')


# ------------------------------------------------------------------ far: black peaks, a forge town and its smoke
def far(L, scene, mood):
    town_x, town_y = L.X(330, 1700), 1700.0

    def height(x, y):
        z = env.fbm(x, y, 0.004, 4, 33) * 12 + 26 * env.smoothstep(800, 3200, y)
        return z + 28 * math.exp(-((x - town_x) ** 2 + (y - town_y) ** 2) / (2 * 240 ** 2))
    slag = env.ground('Slag fields', **CINDER, dry_amount=0.0, flowers=0.0, scale=0.1)
    env.terrain((3600, 1700), (320, 160), (0, 980), height, mat=slag, name='Slag near')
    env.terrain((9000, 4000), (260, 110), (0, 3700), height, mat=slag, name='Slag far')
    env.far_ground(radius=12000, inner=5600, z=30, fog=mood['horizon'], fog_dist=1e7, color=('3a322c', '4a3e34'))
    env.mountains(11, 6400, (190, 330), arc=(-0.6, 0.6), base_z=10, seed=17, direction=90, mat=_dark_rock(), jag=1.0)
    # a forge town on its hill: stacks, glowing furnaces, smoke drifting off
    M = arch.mats('grim')
    rng = random.Random(5)
    for i in range(18):
        a, r = rng.uniform(0, 6.28), rng.uniform(15, 110)
        hx, hy = town_x + math.cos(a) * r, town_y + math.sin(a) * r * 0.6
        arch.house((hx, hy, height(hx, hy) - 0.3), rng.uniform(-0.3, 0.3), rng.uniform(8, 12), rng.uniform(6, 9),
                   floors=rng.choice((1, 2)), seed=40 + i, style='grim', lit=0.8, M=M)
    for i in range(7):
        sx, sy = town_x + rng.uniform(-110, 110), town_y + rng.uniform(-40, 50)
        K.chimney_stack((sx, sy, height(sx, sy)), h=rng.uniform(26, 38), r=2.4, smoke=False)
    for i in range(5):
        gx, gy = town_x + rng.uniform(-90, 90), town_y + rng.uniform(-50, 30)
        env.point((gx, gy, height(gx, gy) + 6), power=2.5e5, color='ff8a32', radius=6.0, name='Forge glow')
    stacks = CM.tree_protos('fddead', 3, 9.0, kind='dead', seed=600)
    CM.woods(L, stacks, 30, (700, 2600), height, seed=8, count=(2, 6))


# ------------------------------------------------------------------ mid: brick foundries and smoking stacks
def mid(L, scene, mood):
    rng = random.Random(4)
    yard = env.ground('Foundry yard', **CINDER, dry_amount=0.0, flowers=0.0)
    CM.ground(L, yard, -12, 40, fade=(20.0, 26.0))
    M = arch.mats('grim')
    G = arch.mats('grey')
    PM = P.pm()
    runs = [(x - w / 2, x + w / 2) for x, w in CM.spans(L, rng, (12, 24), (6, 14))]
    CM.houses(L, rng, 6.0, style='grim', floors=(1, 2), lit=0.8, seed=5, x_runs=runs, widths=(7.0, 10.0), M=M,
              pitch=(30, 40))
    for i, x in enumerate(L.row(6, rng, 0.3)):
        y, h = 14.0 + rng.uniform(-2, 2), rng.uniform(11, 14)
        K.chimney_stack((x, y, 0), h=h, r=1.0, M=PM, smoke=False)
        P.smoke_column((x, y, h), height=12, radius=1.1, density=0.22, color='8a7a70', drift=(3, 5), name='Stack smoke')
    for x in L.row(5, random.Random(9), 0.35):
        K.furnace((x, 3.0, 0), 0.0, M=PM, w=3.6, h=4.4, power=1400)
    K.crane((L.X(620), 9.0, 0), 0.2, M=PM, h=10.0)
    arch.wall((L.x0 - 10, 18.0), (L.x1 + 10, 18.0), 3.5, 1.2, crenels=False, style='grey', M=G, walk=False)


# ------------------------------------------------------------------ near: the cinder yard
def near(fr, scene, mood):
    rng = random.Random(2)
    road = lambda x, y: env.smoothstep(fr.band_near - 0.6, fr.band_near + 0.4, y) * \
        (1 - env.smoothstep(fr.band_far - 0.4, fr.band_far + 0.6, y))
    mat = env.ground('Cinder yard', **CINDER, dry_amount=0.0, flowers=0.0, cobble=('726a62', '524c46'), cobble_scale=4.4)
    CM.ground(fr, mat, fr.near_y - 4, fr.back_y + 12, fade=(fr.back_y + 3.8, fr.back_y + 7.0), masks={
        'cobble': road, 'path': lambda x, y: 0.0})
    PM = P.pm()
    M = arch.mats('grim')
    row = fr.back_y + 1.6
    K.rails(fr.x0 - 12, fr.x1 + 12, row, M=PM)
    for i, x in enumerate(fr.row(6, rng, 0.35)):
        K.ore_cart((x, row, 0.0), 0.0, M=PM, load='coal' if i % 2 else 'ore')
    coal = CM.protos('fdcoal', 3, lambda i: env.rock(170 + i, 0.55, mat=env.flat('1c1a1a', 0.7, 'Coal'), moss=0.0))
    for x in fr.row(9, random.Random(3), 0.4):
        for k in range(rng.randint(6, 11)):
            env.inst(rng.choice(coal), (x + rng.gauss(0, 0.9), row + 2.4 + rng.gauss(0, 0.5), -0.1), rng.uniform(0, 6.28),
                     rng.uniform(0.6, 1.3), env.C['Props'])
    for x in fr.row(2, random.Random(8), 0.3):
        K.slag_heap((x, row + 2.8, 0), r=1.2, seed=int(x * 10) % 97)
    for i, x in enumerate(fr.row(7, random.Random(4), 0.35)):
        kind = i % 3
        if kind == 0:
            P.anvil((x, row + 1.4, 0), 0.4, PM)
            P.barrel((x + 1.2, row + 1.5, 0), 0.0, M=PM)
        elif kind == 1:
            P.brazier((x, row + 1.3, 0), PM, power=260, h=1.0)
            P.crate((x + 1.3, row + 1.6, 0), 0.3, 0.75, PM)
        else:
            arch.wall((x - 2.6, row + 2.0), (x + 2.6, row + 2.0), 1.4, 0.5, crenels=False, style='grim', M=M, walk=False)
    for x in fr.row(10, rng, 0.2):
        P.lantern_post((x, fr.back_y + 0.3, 0), 0.0, h=2.6, M=PM, power=16)
    # in front: a second line of rails, scattered slag and crates, kept below the band
    K.rails(fr.x0 - 12, fr.x1 + 12, fr.front_y - 3.0, M=PM)
    slag = CM.protos('fdslag', 3, lambda i: env.rock(150 + i, 0.6, mat=env.flat('2c2420', 0.9), moss=0.0))
    CM.front_scatter(fr, slag, 18, (fr.front_y - 13, fr.front_y - 4.5), 0.5, seed=4, min_gap=2.2)
    crates = CM.protos('fdcrate', 2, lambda i: CM.one(P.crate((0, 0, 0), 0.0, 0.7 + 0.1 * i, PM)))
    CM.front_scatter(fr, crates, 8, (fr.front_y - 12, fr.front_y - 5), 0.8, seed=6, min_gap=3.0)
