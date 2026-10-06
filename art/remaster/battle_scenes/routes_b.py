"""Ashen Ward (checkpoint, decon, lab, blacksite), Thornwall Pass (pass, shrine, watchfort) and
Hollow Basilica (cathedral, ossuary, reliquary) battlefield fallbacks."""
import math
import random

from rk import arch, env
from rk import dressing as P
from rk.palette import PLAGUE

from menu_scenes.common import golden

from . import kit as K
from .common import BF, back_band, drifts, field_height, ground, row, scatter_front


def _ward_ground(name):
    return env.ground(name, grass=('5a6048', '6a6e52', '7a7a5c'), dry='7e7a62', dry_amount=0.3, flowers=0.0,
                      dirt=('6a604c', '524a3a'), ash=0.45, ash_color='4a4840',
                      cobble=('7a786c', '62605a'))


def _ward_light(scene):
    golden(scene, sun_az=232, sun_el=30, strength=3.4, haze_density=0.0, clouds=0.9, light_strength=0.85,
           zenith='5a6470', horizon='c8c0a0', mid='d0c8b0', upper='98a0a4')


def checkpoint(scene):
    bf = BF()
    rng = random.Random(13)
    h = field_height(bf)
    ground(bf, _ward_ground('Outer ward'), h)
    _ward_light(scene)
    M = P.pm()
    y_min, y_max = back_band(bf)
    K.palisade((bf.x0 - 6, y_min + 0.9), (bf.x1 + 6, y_min + 0.9), 3.4, M, seed=1)
    # the palisade is backlit by the battle sun: purge fires along its foot keep it readable
    for x in row(5, bf.x0, bf.x1, rng, 0.2):
        P.brazier((x, y_min + 0.1, 0), M, power=420, h=1.0)
    for x in row(2, bf.x0, bf.x1, rng, 0.2):
        K.bell_frame((x, y_min + 0.0, 0), 0.0, M, h=3.0)
    for x in row(5, bf.x0, bf.x1, rng, 0.3):
        P.hanging_banner((x, y_min + 0.15, 3.0), 0.8, 1.6, M['crimson'], M, tails=True)
    y0, _ = bf.front_y_range()
    for x in row(3, bf.x0, bf.x1, rng, 0.3):
        K.cheval((x, y0 + 1.0, 0), rng.uniform(-0.15, 0.15), 3.2, M)
    for x in row(3, bf.x0, bf.x1, rng, 0.3):
        P.brazier((x + 2.0, y0 + 2.6, 0), M, power=160, h=0.8)
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        P.crate((x - 1.5, y0 + 0.4, 0), rng.uniform(0, 6), 0.8, M)
    bf.camera()
    return dict(bloom=0.14, saturation=0.95)


def decon(scene):
    bf = BF()
    rng = random.Random(14)
    h = field_height(bf)
    ground(bf, _ward_ground('Purge cloister'), h, masks={'cobble': lambda x, y: 1.0})
    _ward_light(scene)
    M = P.pm()
    AM = arch.mats('grey')
    y_min, y_max = back_band(bf)
    arch.arcade((bf.x1 + 8, y_min + 0.3), (bf.x0 - 8, y_min + 0.3), bays=9, h=4.2, depth=3.0, M=AM, seed=2, lit=0.5)
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        P.cauldron((x, y_min + 1.2, 0), 0.55, M, liquid=env.glow_mat(PLAGUE, 2.0, name='Lye'), power=40)
        P.smoke_column((x, y_min + 1.2, 0.8), height=3.5, radius=0.4, density=0.35, color='d0d8c8', drift=(0.3, 0.5),
                       name='Steam')
    y0, _ = bf.front_y_range()
    for x in row(5, bf.x0, bf.x1, rng, 0.3):
        P.barrel((x, y0 + 1.6, 0), 0.0, M=M)
        K.ward_sigil((x + 1.8, y0 + 0.8, 0.01), 0.8, PLAGUE, power=20)
    for x in row(3, bf.x0, bf.x1, rng, 0.3):
        P.sack((x + 0.6, -1.0, 0), rng.uniform(0, 6), 0.5, M, seed=int(x))
    bf.camera()
    return dict(bloom=0.16, saturation=0.95)


def lab(scene):
    bf = BF()
    rng = random.Random(15)
    h = field_height(bf)
    ground(bf, _ward_ground('Leechcourt'), h, masks={'cobble': lambda x, y: 1.0 if y > bf.D - 0.5 else 0.0})
    _ward_light(scene)
    M = P.pm()
    AM = arch.mats('grim')
    y_min, y_max = back_band(bf)
    x = bf.x0 - 6
    i = 0
    while x < bf.x1 + 6:
        w = rng.uniform(5.5, 7)
        arch.house((x + w / 2, y_min + 3.1, 0), 0.0, w, 6.0, floors=2, seed=80 + i, style='grim', lit=0.9, M=AM)
        x += w + 1.2
        i += 1
    for x in row(3, bf.x0, bf.x1, rng, 0.3):
        K.alembic_table((x, y_min - 0.2, 0), 0.0, M, seed=int(x))
    for x in row(2, bf.x0, bf.x1, rng, 0.3):
        K.vat((x + 3.0, y_min + 0.2, 0), 0.7, M, PLAGUE, power=90)
    y0, _ = bf.front_y_range()
    for x in row(3, bf.x0, bf.x1, rng, 0.3):
        K.cheval((x, y0 + 1.2, 0), 0.1, 2.8, M)
        K.vat((x + 3.4, y0 + 0.4, 0), 0.6, M, PLAGUE, power=60)
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        P.crate((x, -1.0, 0), rng.uniform(0, 6), 0.55, M)
    bf.camera()
    return dict(bloom=0.18, saturation=0.95)


def blacksite(scene):
    bf = BF()
    rng = random.Random(16)
    h = field_height(bf)
    ground(bf, _ward_ground('Black vault yard'), h, masks={'cobble': lambda x, y: 0.6})
    golden(scene, sun_az=232, sun_el=24, strength=2.6, haze_density=0.0, clouds=0.9, light_strength=0.8,
           zenith='3a3a48', horizon='8a7a70', mid='9a8a80', upper='6a6a78')
    M = P.pm()
    vault = env.masonry('Black vault', '3e3c3a', '33312f', '1a1918', block=(0.8, 0.45), soot=0.4)
    y_min, y_max = back_band(bf)
    arch.slab((0, y_min + 1.2, 3.0), (1, 0, 0), (0, 0, 1), bf.W + 16, 6.0, 1.6, vault, [], 'Vault wall')
    for x in row(3, bf.x0, bf.x1, rng, 0.2):
        K.iron_door((x, y_min + 0.3, 0), 0.0, w=2.4, h=3.4, M=M, glow=PLAGUE)
    for x in row(6, bf.x0, bf.x1, rng, 0.2):
        P.torch((x + 1.6, y_min + 0.35, 1.8), M=M, wall=(0, -1, 0), power=60)
    y0, _ = bf.front_y_range()
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        K.ward_sigil((x, y0 + 1.4, 0.01), 1.0, PLAGUE, power=40)
        P.stake_line((x - 1.4, y0 + 0.2), (x + 1.4, y0 + 0.4), M, spacing=0.5, h=1.6, seed=int(x))
    for x in row(3, bf.x0, bf.x1, rng, 0.3):
        P.chest((x + 2.2, -1.2, 0), rng.uniform(-0.3, 0.3), 0.9, 0.55, 0.5, M)
    bf.camera()
    return dict(bloom=0.2, saturation=0.95)


# ------------------------------------------------------------------ Thornwall Pass
def _snow_ground(name):
    return env.ground(name, grass=('6a7058', '7e8268', '949680'), dry='a8a48c', dry_amount=0.3, flowers=0.0,
                      rock='6e6c6a', snow=1.0, dirt=('6a6458', '4e4a42'))


def _cold_light(scene, el=28):
    golden(scene, sun_az=148, sun_el=50, strength=4.2, haze_density=0.0, clouds=0.6, light_strength=1.5,
           zenith='3e64a0', horizon='c8d8e8', mid='d8e4ee', upper='7898c0')
    scene.view_settings.exposure = -0.15


def pass_(scene):
    bf = BF()
    rng = random.Random(17)
    h = drifts(bf, field_height(bf, back_rise=1.4, front_drop=0.4, seed=7), amp=0.8, seed=3)
    ground(bf, _snow_ground('Pass snow'), h, res=9.0)
    _cold_light(scene)
    y_min, y_max = back_band(bf)
    _outcrops(bf, h, rng, (y_min + 0.4, y_min + 2.4), n=7, size=(1.4, 2.6), flat=1.2)
    _outcrops(bf, h, rng, (y_min - 0.4, y_min + 0.4), n=6, size=(0.5, 0.9))
    _snow_pines(bf, h, rng, (y_min + 1.6, y_max), 7)
    y0, _ = bf.front_y_range()
    _outcrops(bf, h, rng, (y0 + 0.2, y0 + 1.4), n=5, size=(0.9, 1.3), flat=0.8)
    _outcrops(bf, h, rng, (y0 + 1.6, -1.4), n=6, size=(0.35, 0.6))
    scatter_front(bf, h, lambda: [env.proto(f'bfpine{i}', lambda i=i: env.conifer(66 + i, 6.0)) for i in range(2)], 4,
                  seed=7, nominal_h=6.0, y_range=(y0 - 0.8, y0 + 0.2), min_gap=5.0)
    bf.camera()
    return dict(bloom=0.1, saturation=1.02, shadows='1a2840', sun_strength=4.2)


def shrine(scene):
    bf = BF()
    rng = random.Random(18)
    h = drifts(bf, field_height(bf, back_rise=0.8, front_drop=0.3, seed=8), amp=0.6, seed=5)
    ground(bf, _snow_ground('Shrine snow'), h, res=9.0,
           masks={'cobble': lambda x, y: 1.0 if bf.D - 1.0 < y < bf.D + 2.0 else 0.0})
    _cold_light(scene, el=32)
    M = P.pm()
    y_min, y_max = back_band(bf)
    for i, x in enumerate(row(3, bf.x0, bf.x1, rng, 0.15)):
        K.shrine_altar((x, y_min + 0.4, h(x, y_min + 0.4)), 0.0, M, seed=i)
    for x in row(5, bf.x0, bf.x1, rng, 0.3):
        K.cairn((x + 2.2, y_min + 0.2, h(x + 2.2, y_min + 0.2)), rng.uniform(0.8, 1.3), seed=int(x * 3))
    for i in range(2):
        K.prayer_flags((bf.x0 - 4, y_min + 0.8 + i * 0.7, 1.9 + i * 0.4), (bf.x1 + 4, y_min + 0.8 + i * 0.7, 1.9 + i * 0.4),
                       sag=0.35, count=34)
    _outcrops(bf, h, rng, (y_min + 1.6, y_min + 3.0), n=6, size=(1.4, 2.4), flat=1.2)
    y0, _ = bf.front_y_range()
    for x in row(5, bf.x0, bf.x1, rng, 0.3):
        K.candle_cluster((x, y0 + 1.3, h(x, y0 + 1.3)), 6, seed=int(x * 3), power=12)
        P.standing_stone((x + 1.6, y0 + 0.4, h(x + 1.6, y0 + 0.4)), 1.4, seed=int(x),
                         mat=env.snow_rock('Shrine menhir', '6e6a64', 0.45))
    _outcrops(bf, h, rng, (y0 + 1.8, -1.2), n=5, size=(0.35, 0.55))
    bf.camera()
    return dict(bloom=0.14, saturation=1.02, shadows='1a2840', sun_strength=4.2)


def watchfort(scene):
    bf = BF()
    rng = random.Random(19)
    h = drifts(bf, field_height(bf, back_rise=0.6, front_drop=0.4, seed=9), amp=0.7, seed=7)
    ground(bf, _snow_ground('Watchfort snow'), h, res=9.0)
    _cold_light(scene, el=26)
    M = P.pm()
    AM = arch.mats('grey')
    y_min, y_max = back_band(bf)
    arch.wall((bf.x0 - 8, y_min + 1.4), (bf.x1 + 8, y_min + 1.4), 4.5, 2.4, M=AM)
    for x in row(2, bf.x0, bf.x1, rng, 0.15):
        arch.tower((x, y_min + 2.2, 0), 2.6, 9, 'crenel', M=AM, seed=int(x))
    for x in row(5, bf.x0, bf.x1, rng, 0.25):
        P.brazier((x, y_min + 0.0, h(x, y_min)), M, power=220, h=1.0)
    _outcrops(bf, h, rng, (y_min - 0.2, y_min + 0.6), n=6, size=(0.5, 0.9))
    y0, _ = bf.front_y_range()
    for x in row(3, bf.x0, bf.x1, rng, 0.3):
        P.stake_line((x - 1.8, y0 + 1.0), (x + 1.8, y0 + 1.2), M, spacing=0.4, h=1.6, seed=int(x), height_fn=h)
        P.crate((x + 2.6, y0 + 2.0, h(x + 2.6, y0 + 2.0)), rng.uniform(0, 6), 0.7, M)
    _outcrops(bf, h, rng, (y0 + 0.2, y0 + 1.0), n=5, size=(0.8, 1.2), flat=0.8)
    bf.camera()
    return dict(bloom=0.14, saturation=1.02, shadows='1a2840', sun_strength=4.2)


def _nave_ground(name):
    return env.ground(name, grass=('6e6a62', '7a766c', '868076'), dry='807a70', dry_amount=0.1, flowers=0.0,
                      cobble=('8a8478', '726c62'), cobble_scale=1.4)


def _nave_light(scene, warm=True):
    golden(scene, sun_az=230, sun_el=36, strength=2.8, haze_density=0.0, clouds=0.9, light_strength=0.9,
           zenith='4a5468', horizon='c8b8a0', mid='d0c4b0', upper='8a90a0')


def _nave_wall(bf, y_min, rng, mat_wall=None):
    """Ruined nave side: clustered piers, broken window bases and wall fill."""
    wall = mat_wall or env.masonry('Nave wall', '9a907e', '857c6c', '4e463c', block=(0.7, 0.4), moss=0.25)
    arch.slab((0, y_min + 2.0, 3.0), (1, 0, 0), (0, 0, 1), bf.W + 16, 6.0, 1.0, wall, [], 'Nave wall')
    for x in row(4, bf.x0 - 2, bf.x1 + 2, rng, 0.05):
        K.gothic_pier((x, y_min + 1.0, 0), 9.0, 0.5)
    glass = env.window_glass('ffcf80', 2.0, 'Stained glass')
    for x in row(3, bf.x0, bf.x1, rng, 0.05):
        arch.slab((x + 2.6, y_min + 1.45, 3.2), (1, 0, 0), (0, 0, 1), 1.6, 3.2, 0.1, glass, [], 'Lancet')


def cathedral(scene):
    bf = BF()
    rng = random.Random(22)
    h = field_height(bf)
    ground(bf, _nave_ground('Nave floor'), h, masks={'cobble': lambda x, y: 1.0})
    _nave_light(scene)
    M = P.pm()
    y_min, y_max = back_band(bf)
    _nave_wall(bf, y_min, rng)
    for x in row(3, bf.x0, bf.x1, rng, 0.3):
        K.candle_cluster((x, y_min + 0.3, 0), 7, seed=int(x), power=20)
    y0, _ = bf.front_y_range()
    for i, x in enumerate(row(4, bf.x0, bf.x1, rng, 0.2)):
        K.pew((x, y0 + 1.6, 0), math.pi + rng.uniform(-0.2, 0.2), 2.6, M, broken=0.25 if i % 2 else 0.0)
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        K.rubble((x + 2.0, -1.2, 0), 0.9, 7, seed=int(x))
    bf.camera()
    return dict(bloom=0.16, saturation=0.98)


def ossuary(scene):
    bf = BF()
    rng = random.Random(23)
    h = field_height(bf)
    ground(bf, _nave_ground('Ossuary court'), h, masks={'cobble': lambda x, y: 1.0})
    _nave_light(scene)
    M = P.pm()
    y_min, y_max = back_band(bf)
    K.bone_wall((bf.x0 - 6, y_min + 0.6), (bf.x1 + 6, y_min + 0.6), 3.2, seed=2, M=M)
    for x in row(4, bf.x0, bf.x1, rng, 0.25):
        K.candle_cluster((x, y_min + 0.1, 0), 6, seed=int(x * 2), power=18)
    y0, _ = bf.front_y_range()
    for x in row(5, bf.x0, bf.x1, rng, 0.3):
        P.bones_pile((x, y0 + 1.2, 0), 0.8, 10, M, seed=int(x), skulls=2)
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        K.candle_cluster((x + 1.5, -1.2, 0), 5, seed=int(x * 5), power=10)
    bf.camera()
    return dict(bloom=0.16, saturation=0.96)


def reliquary(scene):
    bf = BF()
    rng = random.Random(24)
    h = field_height(bf)
    ground(bf, _nave_ground('Reliquary steps'), h, masks={'cobble': lambda x, y: 1.0})
    _nave_light(scene)
    M = P.pm()
    y_min, y_max = back_band(bf)
    step = env.smooth_stone('a49a88', name='Altar steps')
    for k in range(4):
        arch.slab((0, y_min + 0.4 + k * 0.6 + 2, 0.15 + k * 0.3), (1, 0, 0), (0, 1, 0), bf.W + 16, 4.0, 0.3, step, [],
                  'Step')
    for x in row(3, bf.x0, bf.x1, rng, 0.2):
        P.chest((x, y_min + 1.2, 0.6), 0.0, 1.2, 0.7, 0.7, M, open_lid=1.1)
        P.trophy_cup((x + 1.6, y_min + 1.5, 0.9), 0.8, M)
        K.candle_cluster((x - 1.6, y_min + 1.0, 0.6), 6, seed=int(x), power=18)
    for x in row(4, bf.x0, bf.x1, rng, 0.2):
        P.hanging_banner((x + 1.0, y_min + 2.6, 4.6), 1.0, 2.6, M['crimson'], M, tails=True)
    y0, _ = bf.front_y_range()
    for x in row(4, bf.x0, bf.x1, rng, 0.3):
        P.coin_pile((x, y0 + 1.0, 0), 0.7, 0.3, M, seed=int(x), coins=14)
        K.candle_cluster((x + 2.2, y0 + 2.0, 0), 5, seed=int(x * 7), power=12)
    bf.camera()
    return dict(bloom=0.2, saturation=1.0)
def _outcrops(bf, h, rng, ys, n=6, size=(1.2, 2.6), seed=0, flat=0.9):
    """Snow-capped rock outcrops half-buried in the drifts."""
    mat = env.snow_rock('Snowcap rock', '4e4c4c', 0.3)
    protos = [env.proto(f'snowcrop{i}{flat}', lambda i=i: _smooth_rock(env.rock(150 + i, 1.0, mat=mat, flat=flat, moss=0.0)))
              for i in range(3)]
    for x in row(n, bf.x0 - 1, bf.x1 + 1, rng, 0.45):
        y = rng.uniform(*ys)
        s = rng.uniform(*size)
        env.inst(rng.choice(protos), (x, y, h(x, y) - s * 0.25), rng.uniform(0, 6.28), (s * 1.3, s, s * 0.8),
                 env.C['Props'])


def _snow_pines(bf, h, rng, ys, n, scale=(0.5, 0.8), seed=0):
    protos = [env.proto(f'bfpine{i}', lambda i=i: env.conifer(66 + i, 6.0, palette=('1e3020', '2e4428', '4e5e34')))
              for i in range(2)]
    for x in row(n, bf.x0 - 2, bf.x1 + 2, rng, 0.45):
        y = rng.uniform(*ys)
        env.inst(rng.choice(protos), (x, y, h(x, y) - 0.2), rng.uniform(0, 6.28), rng.uniform(*scale), env.C['Vegetation'])




def _smooth_rock(o):
    """Soften the hull facets a little so snow-capped outcrops read as weathered stone, not paper."""
    from rk import geo
    mod = o.modifiers.new('Weathered', 'SUBSURF')
    mod.levels = mod.render_levels = 1
    geo.apply_modifiers(o)
    geo.displace_noise(o, 0.05, 3.0, seed=11)
    for p in o.data.polygons:
        p.use_smooth = True
    return o
