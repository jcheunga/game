"""Saltwake Docks: a stone quay on a bright sea morning. Bollards, crates and a crane line the quay; behind it the
harbour basin with moored ships and a breakwater light; beyond, open sea, sails and a lighthouse headland."""
import math
import random

from rk import arch, env, geo
from rk import dressing as P
from battle_scenes import kit as K

from . import common as CM
from . import ships as S

MOOD = dict(zenith='1f5ea8', horizon='e8e4d6', mid='dce8f0', upper='70acec', clouds=0.85, cloud_lit='ffffff',
            cloud_dark='9aa8b8', cloud_scale=1.2, sun='fff2dc', strength=4.8, sky_light=0.7, glow='fff4e0',
            glow_amount=0.6)
FOG = dict(far_dist=2200.0, mid=(0.1, 0.003, 0.4))
LOOK = dict(far=dict(saturation=1.08, bloom=0.12, split=0.05), mid=dict(saturation=1.02, split=0.06),
            near=dict(saturation=1.06, split=0.08, contrast=1.02))
SEA = dict(deep='1c4a5c', shallow='3f7e86')


def _canvas():
    return env.flat('e8e0cc', 0.85, 'Sail canvas')


def _sail_ship(loc, L, rot, seed):
    """A distant ship under sail, turned so its canvas shows."""
    out = S.ship(loc, L=L, rot=rot, masts=3, seed=seed)
    return out


# ------------------------------------------------------------------ far: open sea, sails, a lighthouse headland
def far(L, scene, mood):
    S.water(-7000, 7000, 120, 15000, z=0.0, mat=S.sea('Open sea', deep='12323e', wave=0.35, scale=0.02), name='Open sea')
    head_y = 2600.0
    hx0, hx1 = L.X(700, head_y), L.X(1150, head_y)

    def cliff(x, y):
        d = max(0.0, min(x - hx0, hx1 - x, (y - head_y + 260) * 2.2, (head_y + 520 - y) * 2.2))
        return -6 + 78 * env.smoothstep(0, 60, d) + env.fbm(x, y, 0.01, 4, 7) * 14 * env.smoothstep(20, 120, d)
    rock = env.aerial_mat('Headland', 'ffffff', 1e7, rock=('6c6860', '8a8478'), green='6a7a48')
    env.terrain((hx1 - hx0 + 200, 900), (160, 120), ((hx0 + hx1) / 2, head_y + 130), cliff, mat=rock, name='Headland')
    M = arch.mats('grey')
    lx = hx0 + 70
    arch.tower((lx, head_y, cliff(lx, head_y) - 1), r=6.0, h=34.0, roof='cone', roof_h=7.0, style='grey', seed=3,
               lit=0.8, M=M)
    env.point((lx, head_y, cliff(lx, head_y) + 37), power=4e5, color='ffe8b0', radius=2.0, name='Lighthouse lamp')
    env.mountains(8, 7200, (120, 230), arc=(0.12, 0.6), base_z=-5, seed=5, direction=90, fog=mood['horizon'], fog_dist=1e7,
                  rock=('6a7078', '868a90'), green='66745a', jag=0.4)
    rng = random.Random(9)
    for i in range(6):
        y = rng.uniform(700, 2400)
        x = rng.uniform(-L.half_width(y) * 0.9, L.half_width(y) * 0.9)
        S.ship((x, y, 0.0), L=rng.uniform(22, 30), rot=rng.uniform(0.5, 0.9) * rng.choice((-1, 1)), masts=3, seed=i)


# ------------------------------------------------------------------ mid: harbour basin, moored ships, breakwater
def mid(L, scene, mood):
    rng = random.Random(4)
    S.water(L.x0 - 20, L.x1 + 20, -14, 60, z=-0.7, mat=S.sea('Harbour basin', deep='10303a', wave=0.5, scale=0.6))
    M = arch.mats('grey')
    W = arch.mats('warm')
    PM = P.pm()
    # the far quay: warehouses and cranes on the left, the breakwater and its light opening to sea on the right
    split = L.X(520)
    quay = env.ground('Far quay', grass=('6a6a5c', '7a7866', '8a8670'), dry='8a8670', dirt=('6e6252', '544a3e'),
                      dry_amount=0.0, flowers=0.0, cobble=('8c867a', '6a645a'))
    env.terrain((split - L.x0 + 20, 14), (60, 8), ((L.x0 - 20 + split) / 2, 23), lambda x, y: 0.0, mat=quay,
                masks={'cobble': lambda x, y: 1.0, 'path': lambda x, y: 0.0}, name='Far quay')
    arch.wall((L.x0 - 20, 16.2), (split, 16.2), 1.0, 0.6, crenels=False, style='grey', M=M, base_z=-0.9, walk=False)
    for k, (a, b) in enumerate([(L.x0 - 6, L.X(170)), (L.X(176), L.X(340)), (L.X(346), split - 2)]):
        CM.houses(L, rng, 18.5, style=('grey', 'warm', 'grey')[k], floors=(2, 3), lit=0.5, seed=7 + k, x_runs=[(a, b)],
                  M=(M, W, M)[k], widths=(6.5, 9.5), depth=(7, 9), pitch=(42, 54))
    for x in (L.X(140), L.X(380)):
        K.crane((x, 17.0, 0.0), 0.3, M=PM, h=10.0)
    by = 22.0
    arch.wall((split - 4, by), (L.x1 + 20, by), 2.4, 3.0, crenels=False, style='grey', M=M, base_z=-1.0, walk=False)
    bx = L.X(820)
    arch.tower((bx, by + 0.4, 1.4), r=1.9, h=6.0, roof='cone', roof_h=2.4, style='grey', seed=2, lit=0.9, M=M)
    env.point((bx, by + 0.4, 10.0), power=1500, color='ffd890', radius=0.6, name='Harbour light')
    # the moorings: a forest of masts
    for i, x in enumerate(L.row(5, rng, 0.2)):
        S.ship((x, 7.0 + (i % 2) * 4.5, -0.7), L=rng.uniform(18, 23), rot=rng.choice((-1, 1)) * rng.uniform(0.35, 0.6),
               masts=3 if i % 2 == 0 else 2, seed=i,
               hull=('3a2a1e', '2e2a26', '4a3020')[i % 3], band=('9a7a34', '7a2a22', '2a5a5a')[i % 3])
    for x in L.row(8, random.Random(6), 0.4):
        S.rowboat((x, rng.uniform(1.5, 4.0), -0.7), L=rng.uniform(4.5, 6), rot=rng.uniform(-0.3, 0.3), seed=int(x))


# ------------------------------------------------------------------ near: the quay
def near(fr, scene, mood):
    rng = random.Random(2)
    edge = fr.back_y + 2.6            # quay edge behind the road
    front_edge = fr.front_y - 0.8     # quay edge in front of it
    stone = env.ground('Quay stone', grass=('6a6a5c', '7a7866', '8a8670'), dry='8a8670', dirt=('6e6252', '544a3e'),
                       dry_amount=0.0, flowers=0.0, cobble=('8c867a', '6a645a'), cobble_scale=4.2)
    CM.ground(fr, stone, front_edge - 0.2, edge + 0.2, masks={'cobble': lambda x, y: 1.0, 'path': lambda x, y: 0.0})
    basin = S.sea('Quayside water', deep='0e2c34', wave=0.5, scale=1.0)
    S.water(fr.x0 - 14, fr.x1 + 14, edge - 0.2, fr.back_y + 12, z=-1.0, mat=CM.soft_edge(basin, edge + 0.6, edge + 3.0),
            name='Quayside water')
    S.water(fr.x0 - 14, fr.x1 + 14, fr.near_y - 8, front_edge + 0.2, z=-1.0, mat=basin, name='Front water')
    M = arch.mats('grey')
    PM = P.pm()
    for y in (edge, front_edge):
        arch.wall((fr.x0 - 12, y), (fr.x1 + 12, y), 1.3, 0.6, crenels=False, style='grey', M=M, base_z=-1.25, walk=False)
    for x in fr.row(16, rng, 0.25):
        K.bollard((x, edge - 0.5, 0.05), PM)
    for x in fr.row(14, random.Random(3), 0.25):
        K.bollard((x, front_edge + 0.45, 0.05), PM)
    # quay clutter in loose groups, kept low so the harbour shows over it
    for i, x in enumerate(fr.row(11, rng, 0.4)):
        kind = rng.random()
        if kind < 0.35:
            for k in range(rng.randint(3, 5)):
                P.crate((x + (k % 3) * 0.8, edge - 1.5 + (k // 3) * 0.75, 0.0), rng.uniform(-0.2, 0.2), 0.75, PM)
            P.crate((x + 0.4, edge - 1.4, 0.75), 0.3, 0.7, PM)
        elif kind < 0.65:
            for k in range(rng.randint(2, 4)):
                P.barrel((x + k * 0.72, edge - 1.2 - (k % 2) * 0.5, 0.0), rng.uniform(0, 6), M=PM)
            K.rope_coil((x - 1.0, edge - 0.9, 0.0), M=PM)
        else:
            P.fence((x - 1.6, edge - 1.1), (x + 1.6, edge - 1.1), h=1.7, M=PM, posts=1.6, rails=4)
            P.sack((x + 2.2, edge - 1.3, 0), 0.4, 0.6, PM, seed=i)
            P.sack((x + 2.8, edge - 1.0, 0), 1.2, 0.5, PM, seed=i + 1)
    K.crane((fr.X(330), edge - 1.0, 0.0), 0.4, M=PM, h=7.0)
    for x in fr.row(9, rng, 0.2):
        P.lantern_post((x, fr.back_y + 0.3, 0), 0.0, h=2.6, M=PM, power=14)
    for x in fr.row(6, random.Random(5), 0.35):
        S.rowboat((x, edge + rng.uniform(0.9, 1.6), -1.0), L=rng.uniform(4.2, 5.2), rot=rng.uniform(-0.15, 0.15), seed=int(x))
    for x in fr.row(4, random.Random(8), 0.4):
        S.rowboat((x, front_edge - rng.uniform(2.6, 4.6), -1.0), L=4.4 * fr.fit(front_edge - 3, 0.9),
                  rot=rng.uniform(-0.2, 0.2), seed=int(x * 3))
