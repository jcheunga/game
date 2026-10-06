"""Shared recipe helpers: head placement, kits and the standard render options."""
from mathutils import Vector

from rk import anim, armor as A


def head_frame(ch, size=1.0):
    C = ch.J['head'] + Vector((0.03, 0, 0.16 * size))
    return C, 0.19 * size


def add_head(ch, objs):
    return ch.add_rigid(objs, 'head')


def soldier_body(ch, M, torso=None, sleeve=None, legs=None, glove=None, forearm=None):
    return ch.build_body(dict(skin=M['skin'], torso=torso or M['linen'], sleeve=sleeve or M['mail'],
                              glove=glove or M['leather_dark'], legs=legs or M['trousers'], forearm=forearm))


def boots(ch, M, mat=None, cuff=None, shaft=0.2, toe='round', plate=None):
    for side in ('R', 'L'):
        ch.add(A.boot(ch.J, side, mat or M['leather_dark'], ch.coll, shaft=shaft, cuff=cuff, toe=toe, plate=plate))


def gloves(ch, M, mat_key='leather_dark', size=1.0, sides=('R', 'L')):
    for side in sides:
        ch.add(A.gauntlet(ch.J, side, M, ch.coll, size=size, mat_key=mat_key))


def plate_arms(ch, M, sides=('R', 'L'), pauldrons=True, size=1.0, lames=3, spikes=0, couters=True, rerebrace=False):
    for side in sides:
        if pauldrons:
            ch.add(A.pauldron(ch.J, side, M, ch.coll, size=size, lames=lames, spikes=spikes))
        ch.add(A.vambrace(ch.J, side, M, ch.coll))
        if couters:
            ch.add(A.couter(ch.J, side, M, ch.coll))
        if rerebrace:
            ch.add(A.upper_arm_plate(ch.J, side, M, ch.coll))


def plate_legs(ch, M, cuisse=True, poleyn=True, greave=True, size=1.0):
    for side in ('R', 'L'):
        ch.add(A.leg_plates(ch.J, side, M, ch.coll, cuisse=cuisse, poleyn=poleyn, greave=greave, size=size))


def finish(ch, stance, profile, *, gait='march', heavy=1.0, low_strike=False, death='back', release=None,
           float_mode=False, cape=True, flourish=None, attack_override=None, portrait=None, body_z=None,
           min_scale=3.6, cam_z=1.15):
    """Bind, key all six clips and return render options."""
    from .deaths import DEATHS
    ch.finalize_bones()
    ch.bind()
    death = DEATHS.get(ch.ident, death)
    if release:
        ch.add_prop('nocked', release, None)
    clips = anim.all_clips(stance, profile, gait=gait, heavy=heavy, low_strike=low_strike, death=death,
                           attack_override=attack_override, float_mode=float_mode, cape=cape, flourish=flourish,
                           melee=_is_ranged(ch.ident))
    ch.key_clips(clips, release=release)
    return dict(profile=profile, portrait_cfg=portrait or {}, body_z=body_z, min_scale=min_scale, cam_target_z=cam_z)


def legacy_profile(spec):
    """Same motion-profile selection as the original pipeline (runtime keys effects off these names)."""
    ident, name, cls = spec['Id'], spec['DisplayName'].lower(), spec['VisualClass']
    if cls == 'siegetower': return 'siege-deploy'
    if cls == 'splitter': return 'nest-pulse'
    if 'engine' in ident: return 'bombard'
    if 'ballista' in ident: return 'ballista'
    if cls == 'hound': return 'pounce'
    if ident in ('player_raider', 'enemy_boss_steppe'): return 'mounted-lance'
    if 'archer' in name: return 'bow-draw'
    if 'crossbow' in name: return 'crossbow'
    if 'spear' in name: return 'spear-thrust'
    if 'alchemist' in name or 'sapper' in name: return 'flask-toss'
    if 'engineer' in name: return 'hammer-command'
    if any(x in name for x in ('mage', 'monk', 'witch', 'lich', 'pontiff', 'necromancer', 'caster', 'hexer', 'stormcaller',
                               'tidecaller', 'archon', 'herald')):
        return 'staff-cast' if spec.get('UsesProjectile') else 'staff-strike'
    if any(x in name for x in ('halberd', 'berserker', 'chieftain')): return 'axe-cleave'
    if cls in ('brute', 'crusher'): return 'heavy-smash'
    if cls in ('bloater', 'runner') or 'mire' in ident: return 'claw-rake'
    if ident == 'player_rogue': return 'blade-stab'
    if cls in ('shield', 'mirror'): return 'guard-cut'
    if cls == 'boss': return 'royal-cleave'
    return 'sword-cut'


_SPECS = None


def _is_ranged(ident):
    try:
        return bool(spec_of(ident).get('UsesProjectile'))
    except KeyError:
        return False


def spec_of(ident):
    global _SPECS
    if _SPECS is None:
        import json
        from rk.core import ROOT
        _SPECS = {u['Id']: u for u in json.loads((ROOT / 'data/units.json').read_text())['Units']}
    return _SPECS[ident]


def profile_of(ident):
    return legacy_profile(spec_of(ident))


def finish_clips(ch, clips, profile, release=None, body_z=None, min_scale=3.6, cam_z=1.15, portrait=None,
                 companions=(), stance=None):
    """Bind and key explicit clip dictionaries (beasts, machines, mounted riders).

    companions: (character, clips[, stance]). Units listed in roster/deaths.py (riders and crews
    use their own '<id>_rider' / '<id>_crew' entries) get their death performance from there."""
    from rk.death import performance
    from .deaths import DEATHS
    ch.finalize_bones()
    ch.bind()
    # Bind crews while the mount is still at rest: keying its clips leaves it posed. Riders arrive already bound.
    for comp, *_ in companions:
        if not comp.bound:
            comp.finalize_bones()
            comp.bind()
    if release:
        ch.add_prop('nocked', release, None)
    if ch.ident in DEATHS:
        clips = dict(clips, death=performance(stance or {}, DEATHS[ch.ident]))
    ch.key_clips(clips, release=release)
    for comp, comp_clips, *rest in companions:
        if comp.ident in DEATHS:
            comp_clips = dict(comp_clips, death=performance(rest[0] if rest else {}, DEATHS[comp.ident]))
        comp.key_clips(comp_clips)
    return dict(profile=profile, portrait_cfg=portrait or {}, body_z=body_z, min_scale=min_scale, cam_target_z=cam_z)


def pelt(parts, strands, length=0.05, count=2500, children=8, comb=(-0.3, 0, -0.8), clump=0.5, seed=7):
    """Grow fur on the first object of a garment part list (mantles, capes, pelts)."""
    from mathutils import Vector
    from rk import fur as F
    obj = parts[0][0]
    me = obj.data
    centre = sum((v.co for v in me.vertices), Vector()) / max(1, len(me.vertices))
    outward = sum((p.normal.dot(p.center - centre) for p in me.polygons))
    if outward < 0:
        me.flip_normals()
        me.update()
    mod = F.add_fur(obj, strands, length=length, count=count, children=children, comb=comb, normal=0.5, clump=clump,
                    radius=0.004, seed=seed)
    # Emit from the base sheet (before thickness), so every strand grows outward.
    obj.modifiers.move(obj.modifiers.find(mod.name), 0)
    return parts
