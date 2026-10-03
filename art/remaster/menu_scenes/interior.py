"""Interior scenes: forge, treasure vault (cash shop), library (codex), great hall (guild),
hall of champions (leaderboard), armoury (loadout), commander's tent (profile), study (settings)."""
import math
import random

from mathutils import Vector

from rk import arch, env
from rk import dressing as P
from rk import shaders as S
from rk.palette import LANTERN_TEAL, ROT_CRIMSON

from .common import golden, night


def _indoor_air(w, d, h, density=0.03, color='e6c8a0', front=-3.0, anisotropy=0.55):
    """Homogeneous dusty air inside a room so window light forms visible shafts."""
    env.haze((0, front, -0.2), (w, (d - front) * 2, h + 1.0), density, color, anisotropy, falloff=None,
             name='Room air')


def _fill(cam_loc, target, power=1200, color='ffd8a8', size=8.0, lift=3.0):
    """Soft bounce light from behind/above the camera so interiors stay readable under UI panels."""
    loc = Vector(cam_loc) + Vector((0, 0, lift))
    env.area(loc, target, power, color, size, name='Fill bounce')


def _fireplace(x, y, M, AM, w=3.4, h=3.2, power=900):
    """Hooded stone fireplace against a back wall at y."""
    out = []
    st = AM['stone2']
    arch.slab((x, y - 0.5, h / 2), (1, 0, 0), (0, 0, 1), w + 1.2, h, 1.0, st, out, 'Fireplace mass', bevel=0.04)
    arch.slab((x, y - 1.02, h * 0.42), (1, 0, 0), (0, 0, 1), w - 0.6, h * 0.8, 0.06, AM['dark'], out, 'Firebox',
              bevel=0)
    arch.slab((x, y - 1.15, h * 0.86), (1, 0, 0), (0, 0, 1), w + 1.5, 0.35, 0.5, AM['stone'], out, 'Mantel', bevel=0.03)
    arch.slab((x, y - 0.6, h + 2.0), (1, 0, 0), (0, 0, 1), w + 0.4, 4.0, 0.9, st, out, 'Chimney breast', bevel=0.03)
    out += P.campfire((x, y - 1.6, 0.0), 0.7, M, power=power, smoke=False)
    return out


# ------------------------------------------------------------------ forge
def forge(scene):
    golden(scene, sun_az=110, sun_el=28, strength=6.5, haze_density=0.0, clouds=0.4, light_strength=0.35)
    AM = arch.mats('grey')
    AM['stone'] = env.masonry('Forge walls', '7d756a', '645d54', '3a342e', moss=0.0, soot=0.55)
    M = P.pm()
    w, d, h = 14.0, 11.0, 6.8
    arch.room(w, d, h, front=-9.0, M=AM, floor='flag', style='grey',
              back_openings=[(-4.8, 3.0, 1.4, 2.2, True), (4.8, 3.0, 1.4, 2.2, True)],
              left_openings=[(7.5, 2.8, 1.2, 2.0, True)])
    _indoor_air(w, d, h, 0.012, 'e0c8b0')
    P.forge_hearth((0, 9.6, 0), 0.0, M, w=3.6, d=2.4, power=2600)
    P.sparks((0, 8.4, 1.3), 60, 1.6, seed=2, rise=1.0)
    P.bellows((2.6, 9.4, 0), 0.0, M)
    P.coal_heap((-2.8, 9.6, 0), 0.9, M, seed=2)
    P.anvil((-1.9, 5.4, 0), 0.4, M, s=1.25)
    P.anvil((2.6, 4.6, 0), -0.3, M, s=1.1)
    P.sparks((-1.9, 5.4, 1.0), 45, 1.1, seed=5, rise=0.6)
    P.trough((-4.2, 5.8, 0), math.pi / 2, M)
    for i, (x, y) in enumerate(((-5.6, 8.9), (-6.0, 8.0), (5.6, 8.6))):
        P.barrel((x, y, 0), i, M=M)
    P.tool_wall((-3.4, 10.62, 2.6), (0, -1, 0), 2.6, M, seed=1)
    P.tool_wall((3.6, 10.62, 2.6), (0, -1, 0), 2.6, M, seed=2)
    P.tool_wall((6.62, 6.0, 2.4), (-1, 0, 0), 3.0, M, seed=3)
    P.workbench((5.6, 3.4, 0), -math.pi / 2, M, seed=1, w=2.6)
    P.weapon_rack((-6.1, 3.6, 0), math.pi / 2, M, seed=3, w=3.0)
    P.grindstone((4.6, 0.6, 0), 0.6, M)
    for x in (-6.6, 6.6):
        P.torch((x, 1.0, 2.3), M=M, wall=(1 if x < 0 else -1, 0, 0), power=90)
    P.hanging_lantern((0, 4.0, 5.6), M, power=60, size=1.3, chain=1.0)
    for x in (-4.8, 4.8):
        P.crossed_swords((x, 10.6, 5.6), (0, -1, 0), M, 0.9)
    env.camera((0.5, -4.2, 3.9), (0, 7.5, 1.9), lens=24)
    _fill((0.5, -4.2, 3.9), (0, 6, 1.5), 900, 'ffc890', size=8)
    scene.view_settings.exposure = 0.35
    return dict(bloom=0.28, bloom_threshold=0.6, vignette=0.4, saturation=1.08)


# ------------------------------------------------------------------ treasure vault
def cash_shop(scene):
    night(scene, strength=0.0, haze_density=0.0, light_strength=0.05, clouds=0.0)
    AM = arch.mats('grey')
    M = P.pm()
    w, d, h = 12.0, 16.0, 7.0
    arch.vault(w, d, h, M=AM, front=-3.0, ribs=5)
    _indoor_air(w, d, h, 0.02, 'c8a070', front=-3)
    P.vault_door((0, 15.7, 0), 0.0, M, r=2.4)
    rng = random.Random(3)
    for (x, y, r, hh) in ((-3.6, 9.5, 1.6, 0.8), (3.8, 10.0, 1.8, 0.9), (-4.2, 4.5, 1.2, 0.6), (4.0, 4.0, 1.3, 0.65),
                          (0.0, 12.5, 1.4, 0.55)):
        P.coin_pile((x, y, 0), r, hh, M, seed=int(x * 10 + y), coins=70)
    P.chest((-2.0, 6.8, 0), 0.5, 1.1, 0.65, 0.65, M, open_lid=1.2)
    P.chest((2.4, 7.2, 0), -0.6, 1.0, 0.6, 0.6, M, open_lid=1.0)
    P.chest((-4.6, 12.6, 0), 0.2, 1.1, 0.65, 0.65, M, open_lid=0.0)
    P.chest((4.8, 13.0, 0), -0.3, 1.0, 0.6, 0.6, M, open_lid=0.0)
    for (x, y, c) in ((-3.0, 11.6, '2fd1c0'), (3.2, 12.2, 'c23a4a'), (-1.5, 3.0, '7a3df0'), (5.0, 7.5, '2fd1c0')):
        P.gem_cluster((x, y, 0.4), 7, c, seed=int(x * 3), size=0.55, power=40)
    for i in range(10):
        P.goblet((rng.uniform(-3.5, 3.5), rng.uniform(2.0, 8.0), 0.0), M)
    for x in (-3.0, 3.0):
        P.trophy_cup((x, 12.0, 0.9), 0.9, M)
    for y in (2.0, 8.0, 14.0):
        for x in (-5.3, 5.3):
            P.torch((x, y, 2.4), M=M, wall=(1 if x < 0 else -1, 0, 0), power=60)
    P.brazier((-2.4, 14.0, 0), M, power=260)
    P.brazier((2.4, 14.0, 0), M, power=260)
    env.camera((0.0, -3.0, 3.6), (0, 10.0, 2.0), lens=24)
    _fill((0.0, -3.0, 3.6), (0, 8, 0.5), 700, 'ffd090', size=6)
    for (x, y) in ((-3.6, 9.5), (3.8, 10.0), (-4.2, 4.5), (4.0, 4.0), (0.0, 12.5)):
        env.point((x, y - 0.8, 2.6), 120, 'ffc870', 0.6, name='Gold sheen')
    env.area((0, 8, 6.5), (0, 8, 0), 600, 'ffe0a0', 3.0, name='Oculus')
    scene.view_settings.exposure = 0.2
    return dict(bloom=0.3, bloom_threshold=0.55, vignette=0.4, saturation=1.1)


# ------------------------------------------------------------------ library
def codex(scene):
    golden(scene, sun_az=95, sun_el=24, strength=6.0, haze_density=0.0, clouds=0.5, light_strength=0.4)
    AM = arch.mats('warm')
    M = P.pm()
    w, d, h = 16.0, 13.0, 8.0
    arch.room(w, d, h, front=-9.0, M=AM, floor='boards', back_openings=[(0.0, 2.2, 2.6, 3.6, True)],
              glass=None)
    _indoor_air(w, d, h, 0.014, 'e8cfa8')
    for x in (-5.6, -2.7, 2.7, 5.6):
        P.bookshelf((x, 12.4, 0), 0.0, 2.6, 5.2, 0.5, 8, M, seed=int(x * 10) + 50)
    for y in (2.0, 5.0, 8.0, 11.0):
        P.bookshelf((-7.5, y, 0), -math.pi / 2, 2.8, 5.0, 0.5, 7, M, seed=int(y * 7))
        P.bookshelf((7.5, y, 0), math.pi / 2, 2.8, 5.0, 0.5, 7, M, seed=int(y * 7) + 3)
    P.ladder((-6.2, 9.8, 0), (-6.6, 11.9, 4.6), M)
    for (x, y, r) in ((-3.4, 6.5, 0.0), (3.4, 6.5, 0.0)):
        P.table((x, y, 0), r, 2.6, 1.2, 0.85, M)
        P.book_stack((x - 0.6, y, 0.85), 4, M, seed=int(x))
        P.book_stack((x + 0.4, y + 0.1, 0.85), 1, M, seed=int(x) + 1, open_book=True)
        P.inkwell((x + 1.0, y - 0.3, 0.85), M)
        P.candle((x + 0.9, y + 0.35, 0.85), 0.25, 0.03, M, power=6.0)
        P.candle((x - 1.0, y + 0.35, 0.85), 0.18, 0.03, M, power=6.0)
        for s in (-1, 1):
            P.chair((x + s * 0.7, y - 1.0, 0), 0.0, M)
    P.lectern((0, 9.6, 0), 0.0, M)
    P.globe((5.2, 2.5, 0), M)
    P.scroll_pile((-5.5, 2.4, 0), 8, M, seed=2)
    P.rug((0, 6, 0.0), 6.0, 7.5, LANTERN_TEAL, 'b0803a')
    P.chandelier((0, 6.0, 5.4), 1.4, 12, M, power=320)
    env.camera((0, -5.0, 4.2), (0, 8.5, 2.6), lens=24)
    _fill((0, -5.0, 4.2), (0, 8, 2.0), 1300, 'ffd8a8')
    scene.view_settings.exposure = 0.3
    return dict(bloom=0.22, vignette=0.38, saturation=1.06)


# ------------------------------------------------------------------ great hall
def guild(scene):
    golden(scene, sun_az=150, sun_el=22, strength=6.5, haze_density=0.0, clouds=0.5, light_strength=0.35)
    AM = arch.mats('warm')
    M = P.pm()
    w, d, h = 18.0, 26.0, 9.0
    arch.room(w, d, h, front=-9.0, M=AM, floor='flag',
              left_openings=[(y, 4.2, 1.4, 2.6, True) for y in (5.0, 11.0, 17.0, 23.0)],
              right_openings=[(y, 4.2, 1.4, 2.6, True) for y in (5.0, 11.0, 17.0, 23.0)])
    _indoor_air(w, d, h, 0.012, 'e6c8a0')
    pillar = env.smooth_stone('a89c88', name='Pillar stone')
    for y in (2.0, 8.0, 14.0, 20.0):
        for x in (-5.6, 5.6):
            arch.column((x, y, 0), 7.6, 0.42, pillar)
            P.hanging_banner((x, y + 3.0, 7.2), 1.4, 3.6, M['teal'], M, emblem=None,
                             rot=0.0)
    for x in (-3.0, 3.0):
        P.table((x, 11, 0), math.pi / 2, 16.0, 1.4, 0.85, M)
        for s in (-1, 1):
            P.bench((x + s * 1.1, 11, 0), math.pi / 2, 15.0, M)
        rng = random.Random(int(x))
        for k in range(8):
            y = 4.5 + k * 1.9
            P.goblet((x + rng.uniform(-0.3, 0.3), y, 0.85), M)
            if k % 3 == 0:
                P.candle((x, y + 0.8, 0.85), 0.22, 0.03, M, power=5.0)
    _fireplace(0, 26.0, M, AM, w=4.0, h=3.6, power=1800)
    P.banner_pole((-3.6, 23.5, 0), 0.0, M['teal'], h=6.0, width=1.1, drop=2.4)
    P.banner_pole((3.6, 23.5, 0), 0.0, M['teal'], h=6.0, width=1.1, drop=2.4)
    P.chair((0, 22.0, 0), math.pi, M, high=True)
    for y in (6.0, 14.0, 20.0):
        P.chandelier((0, y, 6.2), 1.6, 12, M, power=380)
    env.camera((0, -6.5, 5.0), (0, 14, 2.8), lens=24)
    _fill((0, -6.5, 5.0), (0, 12, 2.0), 1800, 'ffd8a8', size=10)
    scene.view_settings.exposure = 0.35
    return dict(bloom=0.22, vignette=0.38, saturation=1.06)


# ------------------------------------------------------------------ hall of champions
def leaderboard(scene):
    golden(scene, sun_az=120, sun_el=35, strength=7.0, haze_density=0.0, clouds=0.4, light_strength=0.35)
    AM = arch.mats('warm')
    M = P.pm()
    w, d, h = 17.0, 24.0, 10.0
    arch.room(w, d, h, front=-9.0, M=AM, floor='flag', back_openings=[(0.0, 5.0, 2.4, 3.0, True)],
              left_openings=[(y, 5.0, 1.3, 2.4, True) for y in (6.0, 13.0, 20.0)],
              right_openings=[(y, 5.0, 1.3, 2.4, True) for y in (6.0, 13.0, 20.0)])
    _indoor_air(w, d, h, 0.012, 'efd6b0')
    marble = env.smooth_stone('cfc6b4', name='Hall marble', veins=0.5)
    bronze = S.metal('8a6a3a', name='Bronze', rough=0.35)
    for i, y in enumerate((4.0, 9.5, 15.0, 20.5)):
        for s in (-1, 1):
            P.statue((s * 6.2, y, 0), -s * 0.93, mat=marble if (i + (s > 0)) % 2 else bronze, h=3.3,
                     pose='sword' if i % 2 == 0 else 'lantern', M=M, plinth_mat=AM['stone2'])
            P.hanging_banner((s * 8.1, y + 2.7, 8.0), 1.3, 3.8, M['teal'] if s < 0 else M['crimson'], M,
                             rot=-s * math.pi / 2)
    # central dais with the champion's cup in a beam of light
    arch.slab((0, 17.0, 0.25), (1, 0, 0), (0, 1, 0), 5.0, 5.0, 0.5, AM['stone2'], [], 'Dais', bevel=0.04)
    arch.slab((0, 17.0, 0.75), (1, 0, 0), (0, 1, 0), 3.4, 3.4, 0.5, AM['stone2'], [], 'Dais step', bevel=0.04)
    arch.slab((0, 17.0, 1.5), (1, 0, 0), (0, 1, 0), 1.2, 1.2, 1.0, marble, [], 'Cup plinth', bevel=0.03)
    P.trophy_cup((0, 17.0, 2.0), 1.5, M)
    env.spot((0, 15.0, 9.5), (0, 17.0, 2.5), 900, 'ffe2b0', 18, 0.5, 0.3, name='Champion beam')
    for x in (-2.6, 2.6):
        P.brazier((x, 14.4, 0.5), M, power=200)
    P.rug((0, 7, 0.0), 3.2, 16.0, ROT_CRIMSON, 'b0803a')
    env.camera((0, -6.0, 4.6), (0, 14, 3.0), lens=24)
    _fill((0, -6.0, 4.6), (0, 12, 2.0), 1800, 'ffd8a8', size=10)
    scene.view_settings.exposure = 0.3
    return dict(bloom=0.24, vignette=0.4, saturation=1.06)


# ------------------------------------------------------------------ armoury
def loadout(scene):
    golden(scene, sun_az=100, sun_el=26, strength=6.0, haze_density=0.0, clouds=0.4, light_strength=0.35)
    AM = arch.mats('grey')
    M = P.pm()
    w, d, h = 12.0, 9.0, 5.6
    arch.room(w, d, h, front=-9.0, M=AM, floor='flag', style='grey', left_openings=[(5.5, 2.6, 1.0, 1.7, True)],
              back_openings=[(-3.6, 2.9, 0.9, 1.4, True), (3.6, 2.9, 0.9, 1.4, True)])
    _indoor_air(w, d, h, 0.012, 'd8c0a0')
    plates = [S.metal('c4ccd0', name='Bright plate', rough=0.42, edge=1.3), S.metal('8a96a0', name='Blued plate', rough=0.45),
              S.gold('c09a50', name='Gilt plate', rough=0.4)]
    cloths = [M['teal'], M['crimson'], M['teal'], M['canvas'], M['teal']]
    env.area((0, 1.0, 5.0), (0, 7.0, 1.5), 700, 'ffe0b8', 7.0, name='Display light')
    for i, x in enumerate((-3.6, -1.8, 0.0, 1.8, 3.6)):
        P.armour_stand((x, 7.0 + (0.3 if i % 2 else 0), 0), math.pi, M, plates[i % 3], cloths[i], scale=1.15)
    for i, x in enumerate((-4.6, -2.7, -0.9, 0.9, 2.7, 4.6)):
        P.wall_shield((x, 8.85, 3.7 if i % 2 else 3.95), (0, -1, 0), M, 0.55,
                      paint=S.paint(LANTERN_TEAL if i % 2 == 0 else 'b0803a', name=f'Shield {i}'))
    P.crossed_swords((0, 8.85, 4.75), (0, -1, 0), M, 1.1)
    P.weapon_rack((-5.4, 3.0, 0), math.pi / 2, M, seed=7, w=3.4)
    P.weapon_rack((5.4, 3.0, 0), -math.pi / 2, M, seed=8, w=3.4)
    P.weapon_rack((-5.4, 6.6, 0), math.pi / 2, M, seed=9, w=2.2)
    P.chest((-3.6, 1.0, 0), 0.4, 1.1, 0.6, 0.6, M, open_lid=0.0)
    P.chest((3.8, 1.2, 0), -0.5, 1.0, 0.6, 0.6, M, open_lid=0.9, gold=False)
    P.barrel((5.1, 7.6, 0), 0.0, M=M)
    P.rug((0, 3.2, 0), 3.4, 5.0, LANTERN_TEAL, 'b0803a')
    for x in (-5.8, 5.8):
        P.torch((x, 5.0, 2.3), M=M, wall=(1 if x < 0 else -1, 0, 0), power=80)
    P.hanging_banner((-1.8, 8.7, 5.2), 0.8, 1.4, M['teal'], M, tails=True)
    P.hanging_banner((1.8, 8.7, 5.2), 0.8, 1.4, M['teal'], M, tails=True)
    env.camera((0.3, -2.2, 2.8), (0, 7.0, 1.6), lens=24)
    _fill((0.3, -2.2, 2.8), (0, 6, 1.5), 900, 'ffd8a8', size=10)
    scene.view_settings.exposure = 0.35
    return dict(bloom=0.22, vignette=0.4, saturation=1.06)


# ------------------------------------------------------------------ commander's tent
def profile(scene):
    night(scene, moon_az=80, moon_el=30, strength=0.3, haze_density=0.0, light_strength=0.4, zenith='141c34',
          horizon='3a3048')
    M = P.pm()
    floor = env.planks('Tent boards', '5a4028', board=0.3, length=2.6)
    arch.slab((0, 0, -0.1), (1, 0, 0), (0, 1, 0), 40, 40, 0.2, floor, [], 'Tent floor', bevel=0)
    P.pavilion((0, 2.0, 0), 0.0, r=6.5, h_wall=3.2, h_roof=3.8, a=LANTERN_TEAL, b='cfc2a2', M=M, door=False,
               pennant=False, count=24, scallops=False, pole=False, width=0.62)
    _indoor_air(14, 10, 7, 0.006, 'e0c090', front=-5)
    P.map_table((0, 3.0, 0), 0.0, M, seed=4)
    P.chair((0, 4.6, 0), math.pi, M, high=True)
    P.armour_stand((-3.9, 5.2, 0), math.pi * 0.85, M, scale=1.1)
    P.banner_pole((3.4, 5.6, 0), math.pi, M['teal'], h=3.6, width=0.9, drop=1.7)
    P.chest((-4.2, 1.6, 0), 0.9, 1.1, 0.6, 0.6, M, open_lid=0.0)
    P.rug((0, 3.0, 0.0), 5.0, 4.0, ROT_CRIMSON, 'b0803a')
    P.weapon_rack((4.6, 2.2, 0), -math.pi / 2 + 0.4, M, seed=2, w=1.8)
    P.book_stack((-3.2, 3.6, 0), 4, M, seed=5)
    P.globe((2.6, 6.2, 0), M, 0.32)
    for (x, y) in ((-2.4, 1.6), (2.2, 4.2), (0.0, 6.4)):
        P.hanging_lantern((x, y, 4.2), M, power=70, size=1.4, chain=0.8)
    env.camera((0, -3.4, 2.5), (0, 4.0, 1.25), lens=21)
    _fill((0, -3.4, 2.5), (0, 4, 1.0), 400, 'ffc890', size=3, lift=1.2)
    return dict(bloom=0.24, vignette=0.42, saturation=1.06)


# ------------------------------------------------------------------ quiet study
def settings(scene):
    night(scene, moon_az=95, moon_el=22, strength=1.4, haze_density=0.0, light_strength=0.6, zenith='18243e',
          horizon='4a5a78', glow='c0d0f0')
    AM = arch.mats('warm')
    M = P.pm()
    w, d, h = 7.5, 6.0, 4.4
    arch.room(w, d, h, front=-9.0, M=AM, floor='boards', back_openings=[(0.0, 1.2, 1.5, 1.7, True)],
              ceiling=True, beams=True)
    _indoor_air(w, d, h, 0.02, 'b8c4d8', front=-3)
    P.table((0, 5.1, 0), 0.0, 2.0, 0.85, 0.8, M)
    P.chair((0.3, 4.2, 0), 0.3, M, high=True)
    P.book_stack((-0.65, 5.15, 0.8), 3, M, seed=11)
    P.book_stack((0.2, 5.0, 0.8), 1, M, seed=12, open_book=True)
    P.inkwell((0.75, 4.95, 0.8), M)
    P.candle((0.7, 5.35, 0.8), 0.24, 0.035, M, power=18.0)
    P.candle((-0.85, 5.35, 0.8), 0.15, 0.03, M, power=8.0)
    P.bookshelf((-2.45, 5.75, 0), 0.0, 1.9, 3.6, 0.4, 6, M, seed=21)
    P.bookshelf((2.45, 5.75, 0), 0.0, 1.9, 3.6, 0.4, 6, M, seed=22)
    P.bookshelf((-3.55, 2.6, 0), -math.pi / 2, 2.2, 3.2, 0.4, 5, M, seed=23)
    P.globe((2.9, 3.6, 0), M, 0.3)
    P.scroll_pile((-2.6, 1.6, 0), 5, M, seed=4)
    P.chest((3.0, 1.6, 0), -0.4, 0.9, 0.5, 0.5, M)
    P.rug((0, 2.6, 0.0), 3.4, 2.6, LANTERN_TEAL, 'b0803a')
    P.hanging_banner((3.68, 3.0, 3.6), 0.9, 1.8, M['teal'], M, rot=-math.pi / 2, tails=True)
    P.hanging_lantern((-1.9, 4.2, 4.2), M, power=22, size=1.1, chain=0.6)
    env.camera((0.4, -0.6, 2.0), (0, 4.8, 1.3), lens=24)
    _fill((0.5, -1.9, 2.2), (0, 5, 1.0), 150, 'ffc890', size=3, lift=1.0)
    scene.view_settings.exposure = 0.45
    return dict(bloom=0.24, vignette=0.42, saturation=1.0)
