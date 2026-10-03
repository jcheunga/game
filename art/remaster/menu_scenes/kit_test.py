"""Kit test scenes (not shipped): close-up vegetation/buildings for look development."""
from rk import arch, env
from rk import dressing as P

from .common import golden, rect_area, rocks, tufts


def kit_trees(scene):
    env.terrain((60, 60), (60, 60), (0, 10), lambda x, y: 0.0, masks={'path': env.road_mask([(0, -20), (0, 40)], 4.0)})
    golden(scene, sun_az=225, sun_el=16.0, strength=4.5, haze_density=0.002, clouds=0.7, light_strength=0.6)
    env.inst(env.proto('b0', lambda: env.broadleaf(1, 7.0)), (-8, 10, 0), 0.3, 1.0, env.C['Vegetation'])
    env.inst(env.proto('b1', lambda: env.broadleaf(2, 8.0)), (-3, 16, 0), 1.3, 1.0, env.C['Vegetation'])
    env.inst(env.proto('c0', lambda: env.cypress(3, 9.0)), (4, 12, 0), 0.0, 1.0, env.C['Vegetation'])
    env.inst(env.proto('k0', lambda: env.conifer(4, 9.0)), (9, 16, 0), 0.0, 1.0, env.C['Vegetation'])
    env.inst(env.proto('o0', lambda: env.olive(5, 5.0)), (8, 6, 0), 0.0, 1.0, env.C['Vegetation'])
    env.inst(env.proto('u0', lambda: env.bush(6, 1.2)), (-1, 4, 0), 0.0, 1.0, env.C['Vegetation'])
    arch.house((0, 26, 0), 0.0, 6, 5, seed=3)
    P.banner_pole((2.5, 2, 0), 0.0, h=4.5)
    rocks(6, rect_area(-6, 6, 0, 8), lambda x, y: 0.0, seed=2)
    tufts(80, rect_area(-8, 8, -2, 8), lambda x, y: 0.0, seed=1)
    env.camera((0, -12, 4.0), (0, 10, 4.0), lens=30)
    return dict()
