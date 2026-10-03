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
        trousers=S.cloth('3b3a3f', name='Dark wool', var='24232a'),
        paint=S.paint(cloth, name='Shield enamel'),
        steel=S.metal(steel, name='Bright steel', rough=0.28),
        dark_steel=S.metal('4a5257', name='Blued steel', rough=0.34, edge=1.9),
        mail=S.mail('8c9499', name='Riveted mail'),
        trim=S.gold(BRASS, name='Antique brass'),
        gold=S.gold('d9a84a', name='Gilt'),
        leather=S.leather('5a3b24', name='Saddle leather'),
        leather_dark=S.leather('2f2219', name='Dark leather'),
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
        trousers=S.cloth('2b2a2a', name='Rotten cloth', var='181717'),
        paint=S.paint(cloth, name='Scarred enamel', under='5a4a3a', chip=0.6),
        steel=S.metal(iron, name='Rusted iron', rough=0.42, wear=0.32),
        dark_steel=S.metal('35383c', name='Black iron', rough=0.45, wear=0.2, edge=2.2),
        mail=S.mail('5d5a52', name='Rusted mail', rough=0.5, wear=0.5),
        trim=S.gold('a08a5a', name='Tarnished brass', rough=0.38),
        gold=S.gold('b38a3e', name='Grave gold', rough=0.32),
        leather=S.leather('3d2c20', name='Rotten leather'),
        leather_dark=S.leather('241a14', name='Black leather'),
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
