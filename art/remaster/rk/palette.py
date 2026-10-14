"""Faction palettes. Lantern Caravan: oak, blue-black iron, antique brass, teal cloth,
amber lantern light. Rotbound Host: bone, rusted iron, ruined crimson heraldry,
plague green and soul-teal light."""
from . import shaders as S

LANTERN_TEAL = '1c6e69'
LANTERN_TEAL_DARK = '144744'
BRASS = 'c89a48'
AMBER = 'ffb04a'
ROT_CRIMSON = '7a1e30'
ROT_PURPLE = '4e2f6e'
PLAGUE = '9cf25c'
SOUL = '6ff0d2'


def lantern(cloth=LANTERN_TEAL, cloth2=LANTERN_TEAL_DARK, skin='b98d77', hair='3a2516', steel='a7b0b4',
            accent=None):
    m = dict(
        cloth=S.cloth(cloth, name='Caravan cloth', var=cloth2),
        cloth_dark=S.cloth(cloth2, name='Caravan cloth dark', var='0a2423'),
        cape=S.cloth('1d5c58', name='Caravan cape', var='123d3b', sheen=0.8),
        accent=S.cloth(accent or 'b3893f', name='Accent cloth', var='7a5a26'),
        linen=S.cloth('d9ccb0', name='Linen', var='a8987a'),
        wool=S.cloth('5a4a3a', name='Wool', var='3a2e24'),
        trousers=S.cloth('4a4852', name='Dark wool', var='302e36'),
        paint=S.paint(cloth, name='Shield enamel'),
        steel=S.metal(steel, name='Bright steel', rough=0.28),
        dark_steel=S.metal('4a5257', name='Blued steel', rough=0.34, edge=1.9),
        mail=S.mail('8c9499', name='Riveted mail'),
        trim=S.gold(BRASS, name='Antique brass'),
        gold=S.gold('d9a84a', name='Gilt'),
        leather=S.leather('5a3b24', name='Saddle leather'),
        leather_dark=S.leather('3d2c1f', name='Dark leather'),
        grip=S.leather('3a281b', name='Grip leather'),
        wood=S.wood('6b4a2e', name='Oak'),
        skin=S.skin(skin),
        hair=S.fur(hair, name='Hair', tip='6a4a2e', scale=0.6),
        eye=S.eye(),
        dark=S.flat('080605', name='Shadow'),
        glow=S.emissive(AMBER, 9, name='Lantern light', core='fff0c4'),
        lamp_glass=S.glass('ffd38a', name='Lantern glass', glow=2.5),
        blade=S.metal('d3d9dc', name='Blade steel', rough=0.24, edge=1.3, hammer=0.0, cavity=0.7),
        hilt=S.gold(BRASS, name='Hilt brass'),
        string=S.flat('d8cfb8', name='Bowstring', rough=.7),
        rope=S.rope(),
        bone=S.bone(),
        gem=S.gem('2fd1c0', name='Teal gem'),
    )
    return m


def rotbound(cloth=ROT_CRIMSON, glow=SOUL, iron='5d6064', flesh='6f8063', bonec='cfc7ae'):
    m = dict(
        cloth=S.cloth(cloth, name='Ruined heraldry', var='2c1018', weave=40),
        cloth_dark=S.cloth('2e2838', name='Grave cloth', var='191522'),
        accent=S.cloth('6b5a3a', name='Faded gold cloth', var='3f3420'),
        linen=S.cloth('a59a80', name='Burial linen', var='6e6550'),
        wool=S.cloth('3a3530', name='Mouldering wool', var='201c18'),
        trousers=S.cloth('3d3936', name='Rotten cloth', var='24201e'),
        paint=S.paint(cloth, name='Scarred enamel', under='5a4a3a', chip=0.6),
        steel=S.metal(iron, name='Rusted iron', rough=0.42, wear=0.32),
        dark_steel=S.metal('35383c', name='Black iron', rough=0.45, wear=0.2, edge=2.2),
        mail=S.mail('5d5a52', name='Rusted mail', rough=0.5, wear=0.5),
        trim=S.gold('a08a5a', name='Tarnished brass', rough=0.38),
        gold=S.gold('b38a3e', name='Grave gold', rough=0.32),
        leather=S.leather('3d2c20', name='Rotten leather'),
        leather_dark=S.leather('32261c', name='Black leather'),
        grip=S.leather('2b1f17', name='Grip'),
        wood=S.wood('4f3a28', name='Grave wood', dark=0.45),
        skin=S.flesh(flesh),
        hair=S.fur('2d2a26', name='Grave hair', tip='5a554c', scale=0.6),
        eye=S.eye('101010'),
        dark=S.flat('050404', name='Void'),
        glow=S.emissive(glow, 10, name='Soul light', core='e8fff8'),
        plague=S.emissive(PLAGUE, 8, name='Plague light', core='f4ffd0'),
        lamp_glass=S.glass('9ff5d8', name='Ghost glass', glow=2.5),
        blade=S.metal('8d8f8a', name='Notched steel', rough=0.35, wear=0.35),
        hilt=S.gold('7d6642', name='Old brass', rough=0.4),
        string=S.flat('a69c86', name='Gut string', rough=.7),
        rope=S.rope('6e5a40'),
        bone=S.bone(bonec),
        bone_dark=S.bone('a8987a', name='Grave bone', stain='3b2c1c'),
        gem=S.gem('7a3df0', name='Grave gem'),
        horn=S.bone('3a3029', name='Horn', stain='15100c', crack=.2),
    )
    return m


def caravan(teal='1d6470', teal_dark='113f47', skin='c48562', hair='3b2416', accent=None):
    """Lantern Caravan in the roster-sheet style: saturated teal and cream heraldry, blued steel with brass
    trims, warm leathers and oak."""
    m = dict(
        cloth=S.st_cloth(teal, name='Caravan teal', shadow=teal_dark),
        cloth_dark=S.st_cloth(teal_dark, name='Caravan teal dark', shadow='0c2c31'),
        cape=S.st_cloth('1a5d68', name='Caravan cape', shadow='0e3940', sheen=.7),
        accent=S.st_cloth(accent or 'b78a3c', name='Accent cloth', shadow='7a5a26'),
        linen=S.st_cloth('d9c9a6', name='Cream linen', shadow='a99878', sheen=.35, edge=.12),
        wool=S.st_cloth('6a5543', name='Wool', shadow='45362a'),
        trousers=S.st_cloth('4a3222', name='Brown trousers', shadow='2e2016', sheen=.4),
        trousers_dark=S.st_cloth('262220', name='Dark hose', shadow='161412', sheen=.35),
        paint=S.st_cloth(teal, name='Shield paint', sheen=.2, rough=.6),
        paint_blue=S.st_cloth('2c4f86', name='Royal blue enamel', shadow='1c3561', sheen=.15, rough=.5, edge=.3),
        flame=S.emissive('ffa63a', 14, name='Spell flame', core='fff2c0'),
        steel=S.st_metal('7a8590', name='Bright steel', rough=.34, edge_col='eef3f7'),
        dark_steel=S.st_metal('566170', name='Blued steel', rough=.42, edge_col='d2dbe4', edge=.8),
        mail=S.mail('6d757c', name='Riveted mail'),
        trim=S.st_gold('c39645', name='Antique brass'),
        gold=S.st_gold('d6a646', name='Gilt'),
        leather=S.st_leather('5c3820', name='Saddle leather'),
        leather_dark=S.st_leather('2e1f15', name='Dark leather', edge=.35),
        grip=S.st_leather('3a281b', name='Grip leather'),
        wood=S.st_wood('6a4528', name='Oak'),
        skin=S.st_skin(skin),
        hair=S.st_hair(hair, tip='6a4630'),
        eye=S.iris_eye('4a3020'),
        dark=S.flat('080605', name='Shadow'),
        glow=S.emissive(AMBER, 9, name='Lantern light', core='fff0c4'),
        enamel=S.emissive('f0a040', 2.2, name='Lantern enamel', core='ffd890', flicker=0.15, base='8a5a1a'),
        lamp_glass=S.glass('ffd38a', name='Lantern glass', glow=2.5),
        blade=S.st_metal('9aa3ab', name='Blade steel', rough=.22, edge_col='f2f5f7', edge=.9, cavity=.4),
        hilt=S.st_gold('c39645', name='Hilt brass'),
        string=S.flat('d8cfb8', name='Bowstring', rough=.7),
        rope=S.rope(),
        bone=S.st_bone('c6b48c'),
        gem=S.gem('2fd1c0', name='Teal gem'),
    )
    return m


def host(crimson='6e1c25', crimson_dark='380c12', bonec='a8956c', glow='9cf25c', flesh='a6a98a', iron='4a4440',
         skin=None, hair=None, accent=None):
    """Rotbound Host in the roster-sheet style: ruined crimson heraldry, weathered bone, rust-eaten iron
    with tarnished brass, sickly flesh and plague-green soul light."""
    m = dict(
        cloth=S.st_cloth(crimson, name='Rot crimson', shadow=crimson_dark, sheen=.5),
        cloth_dark=S.st_cloth(crimson_dark, name='Rot crimson dark', shadow='2a0a0e', sheen=.4),
        cape=S.st_cloth('7a1d29', name='Rot cape', shadow='3e0e15', sheen=.5),
        accent=S.st_cloth(accent or 'c9b48a', name='Bone linen', shadow='8a7a58'),
        linen=S.st_cloth('b3a582', name='Burial linen', shadow='7a6e52', sheen=.3),
        wool=S.st_cloth('4a403a', name='Grave wool', shadow='2a2420'),
        trousers=S.st_cloth('3e3530', name='Rotten cloth', shadow='241e1a'),
        trousers_dark=S.st_cloth('2a2422', name='Black rags', shadow='181412'),
        paint=S.st_cloth(crimson, name='Rot shield paint', shadow=crimson_dark, sheen=.15, rough=.6),
        paint_blue=S.st_cloth(crimson, name='Rot shield paint', shadow=crimson_dark, sheen=.15, rough=.6),
        steel=S.st_metal('6a6460', name='Rusted iron', rough=.42, edge_col='b8b0a6', wear=.45),
        dark_steel=S.st_metal(iron, name='Black iron', rough=.45, edge_col='a69c90', wear=.35),
        mail=S.mail('5d5a52', name='Rusted mail', rough=.5, wear=.5),
        trim=S.st_gold('a2803e', name='Tarnished brass', rough=.4, edge_col='e2c78a'),
        gold=S.st_gold('b8913f', name='Grave gold', rough=.35),
        leather=S.st_leather('5a3a26', name='Rotten leather'),
        leather_dark=S.st_leather('35251a', name='Black leather', edge=.3),
        grip=S.st_leather('2e2018', name='Grip'),
        wood=S.st_wood('5a3c24', name='Grave wood'),
        skin=S.st_flesh(flesh) if skin is None else S.st_skin(skin),
        flesh=S.st_flesh(flesh),
        hair=S.st_hair(hair or '2d2a26', tip='5a554c'),
        eye=S.iris_eye('9cf25c', sclera='3a3a2a', glow=4.0),
        dark=S.flat('080605', name='Void'),
        glow=S.emissive(glow, 10, name='Soul light', core='eaffc8'),
        plague=S.emissive(glow, 8, name='Plague light', core='f4ffd0'),
        flame=S.emissive(glow, 12, name='Blight flame', core='f0ffd8'),
        enamel=S.emissive(glow, 2.5, name='Blight enamel', core='d8ffb0', flicker=0.2, base='3a5a1a'),
        lamp_glass=S.glass('b8f59f', name='Ghost glass', glow=2.5),
        blade=S.st_metal('7d7872', name='Notched steel', rough=.36, edge_col='d6d2cc', wear=.3),
        hilt=S.st_gold('8a6a3a', name='Old brass', rough=.42),
        string=S.flat('a69c86', name='Gut string', rough=.7),
        rope=S.rope('6e5a40'),
        bone=S.st_bone(bonec, stain='5a4528'),
        bone_dark=S.st_bone('a8987a', name='Grave bone', stain='3b2c1c'),
        gem=S.gem('7a3df0', name='Grave gem'),
        chain=S.st_metal('4f4842', name='Chain iron', rough=.48, edge_col='a8a098', wear=.5),
        pustule=S.emissive(glow, 6, name='Pustule glow', core='f6ffd8', flicker=0.3, base='6a8a2a'),
    )
    return m


HOST_ZONES = {
    # zone: palette.host overrides (cloth, glow, flesh, bone, iron) for the Rotbound Host's regional bands
    'city': dict(),
    'harbor': dict(crimson='2f5058', crimson_dark='16282c', glow='7ff0ff', flesh='7d9a90', bonec='b2ae94', iron='3c4446'),
    'foundry': dict(crimson='5a2a1a', crimson_dark='2a120a', glow='ff7a2a', flesh='8a7a6a', iron='3a3230'),
    'quarantine': dict(crimson='6a6230', crimson_dark='38331a', glow='c8f040', flesh='a8a47c'),
    'thornwall': dict(crimson='4e3a26', crimson_dark='2a1e12', glow='9cf25c', flesh='8a8a6a'),
    'basilica': dict(crimson='7a1d28', crimson_dark='3e0d14', glow='ffd98a', bonec='c9b991'),
    'mire': dict(crimson='3c4a2c', crimson_dark='1e2614', glow='a8ff7a', flesh='6f7a5a'),
    'steppe': dict(crimson='7a6a54', crimson_dark='4a3e30', glow='ff9a4a', flesh='9a8a72'),
    'gloamwood': dict(crimson='3e2c52', crimson_dark='1e1428', glow='b98aff', flesh='8a8494'),
    'citadel': dict(crimson='4a4a54', crimson_dark='26262c', glow='9cf25c', iron='34363c'),
}


def zone_host(zone, **kw):
    return host(**dict(HOST_ZONES.get(zone or 'city', {}), **kw))
