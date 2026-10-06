"""Lantern Caravan material sets and feature flags for the war wagon and its skins.

Every skin shares the wagon geometry and camera so the runtime can swap textures
in the same 180x140 rect; skins change materials, heraldry and a few dressing
props (trophies, braziers, crystals, coin chests, plating)."""
from rk import shaders as S
from rk import structures as ST


def _base(cloth='1a6e6a', cloth_dark='0f4542', glow='ffb04a', glow_core='ffd88a', glass='ff9c3a', wood='5e4129',
          wood_dark='2b2019', iron='1e252b', brass='c89a48', paint=None, rope='9b7b52'):
    return dict(
        wood=ST.board(wood, name='Oak boards', axis='X', grime=.35, dark=.52),
        wood_v=ST.board(ST_scale(wood, .88), name='Oak uprights', axis='Z', grain=4.0, dark=.52),
        deck=ST.board(ST_scale(wood, 1.1), name='Deck boards', axis='X', worn=.7),
        wood_dark=ST.board(wood_dark, name='Tarred undercarriage', axis='X', dark=.6, worn=.4),
        iron=ST.plate(iron, name='Blue-black iron', rough=.42, edge=2.3),
        stud=S.gold('a07a3c', name='Rivet brass', rough=.34),
        brass=S.gold(brass, name='Antique brass'),
        gilt=S.gold('d9a84a', name='Gilt'),
        cloth=S.cloth(cloth, name='Caravan cloth', var=cloth_dark, sheen=.3, edge_light=.06),
        skull_emblem=None,
        cloth_dark=S.cloth(cloth_dark, name='Caravan cloth dark', var='0a2423'),
        paint=paint or ST.enamel(cloth, name='Shield enamel', under='8a6a3a', chip=.28),
        glow=S.emissive(glow, 10, name='Lantern flame', core=glow_core),
        lamp_glass=S.emissive(glass, 2.4, name='Lantern glass', core=glow_core, flicker=.25, base=ST_scale(glass, .3)),
        verdigris=ST.verdigris(name='Verdigris copper'),
        window=S.emissive(glow, 1.6, name='Warm interior', core='ffd9a0', flicker=.5, base='2a1a0e'),
        void=S.flat('0b0807', name='Recess shadow', rough=.95),
        rope=S.rope(rope),
        leather=S.leather('4a3020', name='Harness leather'),
        bone=S.bone('d6cba8', name='Trophy bone'),
        dark=S.flat('080605', name='Shadow'),
        blade=S.metal('c9d0d3', name='Bright steel', rough=.26, edge=1.3, hammer=0.0),
        flame=ST.flame(glow, glow_core, 1.6, name='Fire'),
        coals=ST.coals('ff6a1a'),
    )


def ST_scale(hex_color, k):
    c = [int(hex_color[i:i + 2], 16) for i in (0, 2, 4)]
    return ''.join(f'{min(255, int(v * k)):02x}' for v in c)


def skin(name):
    F = dict(emblem='lantern', finial='spear', plated=False, trophies=False, braziers=False, crystals=False,
             coins=False, light='ffb04a', light_power=1.0, dome='brass', cupola_glow=1.0, stripes=False,
             banner_trim='brass', wheel_disc=False, crown=False, halo=False, flag_tatter=0.0, ember=False)
    if name in ('', 'default', 'war_wagon'):
        M = _base()
    elif name == 'iron':
        M = _base(cloth='3d5f84', cloth_dark='1f3450', wood='3e3128', wood_dark='221c18', iron='2a3038', brass='a8844a')
        M['plate'] = ST.plate('8090a6', name='Gunmetal plate', rough=.4, edge=1.6, var=.2, metallic=.55)
        M['iron'] = ST.plate('2a313a', name='Black iron frame', rough=.45, edge=2.6, metallic=.7)
        M['paint'] = ST.plate('8d9aab', name='Steel shield', rough=.36, edge=1.4, var=.1, metallic=.6)
        F.update(plated=True, wheel_disc=True, finial='spike', dome='plate')
    elif name == 'royal':
        M = _base(cloth='2443a0', cloth_dark='16296a', wood='e8e1d2', wood_dark='2a3150', iron='1d2a52', brass='e2b552')
        M['wood'] = ST.enamel('dcd5c6', name='Ivory lacquer', under='8a6a40', chip=.12, var=.06)
        M['wood_v'] = ST.enamel('2443a0', name='Royal blue lacquer', under='c9a24a', chip=.1, var=.05)
        M['deck'] = ST.board('8a5a36', name='Varnished deck', axis='X', worn=.6)
        M['wood_dark'] = ST.enamel('1d2a52', name='Midnight lacquer', under='6b4a2e', chip=.15, var=.05)
        M['iron'] = S.gold('d9a84a', name='Gilded bands', rough=.24)
        M['stud'] = S.gold('f0c868', name='Gold studs', rough=.2)
        M['paint'] = ST.enamel('2443a0', name='Royal enamel', under='e2b552', chip=.1)
        F.update(finial='fleur', dome='gold', crown=True, banner_trim='gilt')
    elif name == 'bone':
        M = _base(cloth='4a1c22', cloth_dark='261014', wood='5e5850', wood_dark='2a2622', iron='34302c', brass='b8aa88',
                  glow='ffc070', glow_core='ffe2a8')
        M['wood'] = ST.board('6a645a', name='Grave-weathered boards', axis='X', dark=.55, grime=.6, alt='544e46')
        M['wood_v'] = ST.board('4c4740', name='Grave-weathered uprights', axis='Z', dark=.55)
        M['iron'] = ST.plate('34302c', name='Grave iron', rough=.55, edge=2.0, wear=.35)
        M['stud'] = S.bone('d8ccaa', name='Bone studs')
        M['brass'] = S.bone('d8ccaa', name='Bone fittings', crack=.3)
        M['paint'] = ST.enamel('c8bea4', name='Bone-white shield', under='4a3a2a', chip=.35)
        M['skull_ink'] = ST.plate('2a2420', name='Blackened skull inlay', rough=.5, metallic=.3)
        F.update(trophies=True, finial='skull', dome='bone', flag_tatter=1.0, banner_trim='bone', emblem='skull')
    elif name == 'flame':
        M = _base(cloth='a8361a', cloth_dark='5a1a0c', wood='2a2220', wood_dark='18120f', iron='2b2725', brass='b0703a',
                  glow='ff7a2a', glow_core='ffe0a0', glass='ff9a50')
        M['wood'] = ST.board('3a2c26', name='Charred boards', axis='X', char=.7, ember='ff5a14', ember_strength=6, dark=.5,
                             ember_share=.45)
        M['wood_v'] = ST.board('2e2420', name='Charred uprights', axis='Z', char=.7)
        M['iron'] = ST.plate('2a2624', name='Forge iron', rough=.4, edge=2.6, tint='5a3020')
        M['paint'] = ST.plate('2a2422', name='Blackened shield', rough=.45, edge=3.2, metallic=.6, tint='6a2a10')
        M['flame'] = ST.flame('ff4a0a', 'ff9a30', 1.6, name='Forge fire')
        M['brass'] = S.gold('d0703a', name='Hot copper', rough=.3)
        F.update(braziers=True, finial='flame', dome='iron', light='ff7a2a', light_power=1.2, ember=True)
    elif name == 'shadow':
        M = _base(cloth='2a1470', cloth_dark='140838', wood='231c26', wood_dark='121016', iron='17161e', brass='8a84a8',
                  glow='a066ff', glow_core='d8b8ff', glass='8a50ff')
        M['wood'] = ST.board('241d28', name='Ebony boards', axis='X', dark=.6, alt='2e2238')
        M['wood_v'] = ST.board('1e1922', name='Ebony uprights', axis='Z', dark=.6)
        M['window'] = S.emissive('8a50ff', 2.4, name='Spectral interior', core='c8a0ff', flicker=.5, base='1a1028')
        M['paint'] = ST.enamel('3b2358', name='Night enamel', under='8d88a6', chip=.2)
        F.update(light='9a5cff', light_power=2.2, finial='spike', dome='iron', banner_trim='brass')
    elif name == 'guild':
        M = _base(cloth='0f6a54', cloth_dark='073a2c', wood='4e2418', wood_dark='2a140e', iron='2c2a26', brass='e0b04a')
        M['wood'] = ST.board('4e2418', name='Mahogany boards', axis='X', dark=.6, alt='5a2a1a')
        M['wood_v'] = ST.board('3c1a12', name='Mahogany uprights', axis='Z')
        M['iron'] = S.gold('b8893c', name='Bronze bands', rough=.34)
        M['stud'] = S.gold('e8c060', name='Gold studs', rough=.22)
        M['cloth2'] = S.cloth('e8d9b0', name='Cream silk', var='c9b88c', sheen=.9)
        M['paint'] = ST.enamel('1d6b4a', name='Guild enamel', under='e0b04a', chip=.15)
        F.update(emblem='coin', coins=True, stripes=True, finial='orb', dome='gold', banner_trim='gilt')
    elif name == 'legendary':
        M = _base(cloth='f0e9d6', cloth_dark='c9bfa2', wood='efe8da', wood_dark='3a3a48', iron='e2b552', brass='f2c45a',
                  glow='bff4ff', glow_core='ffffff', glass='d8faff')
        M['wood'] = ST.enamel('d6d0c4', name='Pearl lacquer', under='d9a84a', chip=.08, var=.05)
        M['wood_v'] = ST.enamel('e0dace', name='Pearl uprights', under='d9a84a', chip=.06, var=.04)
        M['deck'] = ST.board('b08a5a', name='Pale deck', axis='X')
        M['wood_dark'] = ST.enamel('2f3446', name='Night lacquer', under='d9a84a', chip=.1)
        M['iron'] = S.gold('f0c050', name='Radiant gold', rough=.18)
        M['stud'] = S.gold('ffd877', name='Bright gold', rough=.15)
        M['paint'] = ST.enamel('dcd6ca', name='Pearl enamel', under='f0c050', chip=.05)
        M['crystal'] = S.gem('5fd8ff', name='Radiant crystal', glow=2.2)
        M['lamp_glass'] = S.emissive('8fe8ff', 2.0, name='Crystal glass', core='d8faff', flicker=.2, base='2a5a6a')
        F.update(crystals=True, finial='crystal', dome='gold', light='c8f4ff', light_power=1.5, halo=True, crown=False,
                 banner_trim='gilt')
    else:
        raise KeyError(name)
    M.setdefault('plate', M['iron'])
    M.setdefault('cloth2', M['cloth_dark'])
    M.setdefault('crystal', S.gem('ffc46a', name='Amber crystal', glow=3.0))
    return M, F
