#!/usr/bin/env python3
"""Expand the 60-stage campaign (six stages per zone, postgame stages 47-60) into ten zones of ten
contiguous stages. Each zone keeps its six authored stages and gains four new ones built from the
stages around them; the zone's final stage (slot 10) is its boss. Zones with a postgame boss end on
it and keep their old boss as a mid-zone boss in slot 5.

Difficulty values that rose with stage number (enemy health/damage, base health, rewards, spawn
cadence...) are re-spread over the new 1-100 range, so content keeps its authored waves while its
pressure follows the new position.

    python3 scripts/analysis/expand_campaign.py [--source scripts/analysis/legacy/stages-60.json] [--write]

Prints the old->new stage mapping (mirrored in scripts/core/CampaignRenumbering.cs).
"""
import argparse
import copy
import json
import math
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ZONES = ['city', 'harbor', 'foundry', 'quarantine', 'thornwall', 'basilica', 'mire', 'steppe', 'gloamwood', 'citadel']
LAST_MAIN = 46
OLD_COUNT = 60
NEW_COUNT = 100
# fields that grow with stage number, re-sampled along the new progression
LINEAR = ['EnemyHealthScale', 'EnemyDamageScale', 'PlayerBaseHealth', 'EnemyBaseHealth', 'EnemySpawnMin', 'EnemySpawnMax',
          'BonusWaveChance']
BOSS_PREFIX = 'enemy_boss'

# Four new stages per zone: name and the tip shown on the stage card.
NEW_STAGES = {
    'city': [
        ('Lamplighters Row', 'Runners slip between the lamp posts. Keep a defender ahead of your archers.'),
        ('Tollhouse Square', 'Brutes shoulder through the toll gate behind walker screens. Hold courage for the second push.'),
        ('Chapel Steps', 'Spitters lob blight from the steps. Close the distance with melee before they settle.'),
        ('Belfry Approach', 'The Grave Lord\'s vanguard tests the bell road. Clear each wave before the next arrives.'),
    ],
    'harbor': [
        ('Netmenders Wharf', 'Bloaters drift in with the fog. Let ranged troops pop them before they reach your line.'),
        ('Ropewalk', 'Splitters crowd the long sheds. Keep courage ready to catch the pieces.'),
        ('Gull Point', 'Crushers lead the tide onto the point. Stack melee in front of the wagon.'),
        ('Salt Chains', 'Howlers rally the drowned for the final push. Break them early.'),
    ],
    'foundry': [
        ('Coal Yard', 'Saboteurs dash for the wagon between the coal carts. Keep a fast melee unit on the line.'),
        ('Bellows Hall', 'Furnace vents flare across the hall. Advance between bursts.'),
        ('Slag Run', 'Heavy columns crawl down the slag run. Siege troops earn their keep here.'),
        ('Crucible Gate', 'The forge guard holds the crucible gate. Spend courage on a strong front line.'),
    ],
    'quarantine': [
        ('Lime Pits', 'Jammers smother your signal near the pits. Win fights fast before courage dries up.'),
        ('Cordon Line', 'Shieldwalls lock the cordon. Bring troops that hit hard up close.'),
        ('Fever Wards', 'Spitter crews fire from the ward windows. Keep moving forward.'),
        ('Sealed Infirmary', 'The vault guard musters everything left. Keep the wagon healthy for the last waves.'),
    ],
    'thornwall': [
        ('Frost Cairns', 'Tunnelers break through the snow behind your line. Keep a guard near the wagon.'),
        ('Ice Bridge', 'Mirrors copy your strongest troops on the bridge. Send many small units.'),
        ('Signal Ridge', 'Avalanche lanes sweep the ridge. Time your pushes between slides.'),
        ('Gatehouse Scree', 'The pass guard holds the scree. Bring siege troops for the last stretch.'),
    ],
    'basilica': [
        ('Bone Choir', 'Liches raise the fallen behind the choir stalls. Push through to the casters.'),
        ('Candle Vault', 'Shieldwalls guard the vault stair. Heavy hitters first, archers behind.'),
        ('Crypt Gallery', 'Splitters crowd the narrow gallery. Keep courage ready for the pieces.'),
        ('Reliquary Nave', 'The reliquary wakes. Hold a strong line through every wave.'),
    ],
    'mire': [
        ('Reedbank', 'Bloaters wade out of the reeds. Pop them at range.'),
        ('Lantern Ferry', 'Siege towers roll up the causeway. Break them before they unload.'),
        ('Sunken Graves', 'The dead rise from the drowned graves in waves. Pace your courage.'),
        ('Bellwater', 'The mire bell calls everything left. Keep the wagon healthy.'),
    ],
    'steppe': [
        ('Dry Wells', 'Runners sweep across the open grass. Defenders hold them off your archers.'),
        ('Tent Lines', 'Brutes charge out of the war camp. Keep courage for a second wall.'),
        ('Horse Barrows', 'Crushers lead the column past the barrows. Siege troops help here.'),
        ('Warhorn Ridge', 'The war band gathers on the ridge. Break each wave before the next.'),
    ],
    'gloamwood': [
        ('Fungal Hollow', 'Glowcaps hide tunnelers. Keep a guard close to the wagon.'),
        ('Charcoal Camp', 'Saboteurs dart between the kilns. Fast melee keeps them off the wagon.'),
        ('Hanging Oaks', 'Liches chant beneath the oaks. Push through to the casters.'),
        ('Heartwood Path', 'The verge closes in. Hold a strong line to the end.'),
    ],
    'citadel': [
        ('Barbican Steps', 'Revenant captains lead the garrison. Kill them first and the rest falter.'),
        ('Siege Yard', 'Boneballistas cover the yard. Rush them with fast troops.'),
        ('Crownward Wall', 'Shieldwalls hold the wall walk. Bring heavy hitters.'),
        ('Throne Approach', 'The Ashen Regent\'s guard musters for the last stand. Spend everything.'),
    ],
}


# Authored stages whose text described their old place in the campaign.
REWRITE = {
    1: 'Learn the line: tap a Swordsman to send it out of the caravan, then an Archer to follow it. Break the gate and rout every wave to win.',
    8: 'The Tidecaller holds the harbor mouth. Its entries begin toward the end of the battle.',
    16: 'Purge cycles and sealed kill-box walls hold until the Plague Archon breaches the vault.',
    26: 'Reliquary flares and censer clouds hold until the Bone Pontiff claims the high altar.',
    46: 'Mixed command waves and breach lines hold the keep until the Dread Sovereign joins the siege.',
    45: 'The inner ring tightens into an attrition line where heralds, hexers and raiders converge.',
    47: 'Fast dead and curse fire return to the King\'s Road. Keep archers behind a solid front line.',
    48: 'Drowned hulks grind along the docks in long waves. Pace your courage.',
    49: 'Furnace vents, split broods and sapper dives peak together. Advance between vent bursts.',
    50: 'Hold the failing purge seal while jammers smother your signal. The Plague Archon returns at the end.',
    51: 'Whiteout hides bone ballistas on the ramparts. Rush them before they wear down the wagon.',
    52: 'The vault gives way beneath the caravan as the Reliquary Tyrant rises with its embalmed giants. Save courage for the tyrant.',
    53: 'A plague flotilla drifts beside the causeway while rot hulks and drowned giants grind every step.',
    54: 'Horns call charge after charge across the open steppe. Break each wave before the next horn sounds.',
    55: 'Tunnelers and mirror knights strike out of the fog. Keep a guard by the wagon.',
    56: 'The throne hall erupts in mixed command waves as the Ashen Regent seizes the breach. Spend everything.',
    57: 'A grave procession takes the King\'s Road with siege bolts and revenant officers. Keep melee and ranged troops together.',
    58: 'The chains drag up a drowned command ship while plague engines rake the dock. The Harrow Tidemaster calls the tide in last.',
    59: 'The forge cracks open: plague engines, revenant captains and furnace surges hit at once. Bring siege troops.',
    60: 'The seal complex ruptures into a plague court. Signal denial and escort commanders shield the Plague Monarch.',
}
# A zone's own boss stands in where a later zone's boss appeared early.
BOSS_SWAP = {50: ('enemy_boss_citadel', 'enemy_boss_ward', 'Plague Archon returns')}
# Unlock stages spread across the longer campaign.
UNIT_UNLOCKS = {
    'player_brawler': 1, 'player_shooter': 1, 'player_defender': 1, 'player_ranger': 2, 'player_hound': 3,
    'player_spear': 4, 'player_raider': 6, 'player_mechanic': 8, 'player_marksman': 11, 'player_banner': 13,
    'player_rogue': 16, 'player_breacher': 21, 'player_grenadier': 24, 'player_necromancer': 27,
    'player_berserker': 31, 'player_coordinator': 34, 'player_lantern_guard': 41, 'player_ballista': 51,
    'player_stormcaller': 61,
}
SPELL_UNLOCKS = {
    'spell_fireball': 1, 'spell_heal': 3, 'spell_stone_barricade': 5, 'spell_frost_burst': 9, 'spell_war_cry': 12,
    'spell_lightning_strike': 15, 'spell_polymorph': 19, 'spell_barrier_ward': 23, 'spell_earthquake': 28,
    'spell_resurrect': 33,
}


# Enemies first appear in the regular campaign at these stages (new numbering); the postgame elites are held
# back to the zones whose own remix stages feature them. A stage that would field an enemy earlier fields the
# next role-alike the player has already met.
INTRODUCED = {
    'enemy_walker': 1, 'enemy_runner': 2, 'enemy_brute': 4, 'enemy_spitter': 4, 'enemy_bloater': 11, 'enemy_splitter': 12,
    'enemy_crusher': 14, 'enemy_howler': 14, 'enemy_saboteur': 21, 'enemy_jammer': 31, 'enemy_shieldwall': 34,
    'enemy_tunneler': 42, 'enemy_mirror': 46, 'enemy_boneballista': 48, 'enemy_lich': 52, 'enemy_catacomb_giant': 58,
    'enemy_plague_engine': 66, 'enemy_siegetower': 66, 'enemy_revenant_captain': 74,
}
STAND_IN = {
    'enemy_revenant_captain': 'enemy_howler', 'enemy_catacomb_giant': 'enemy_crusher', 'enemy_plague_engine': 'enemy_crusher',
    'enemy_siegetower': 'enemy_crusher', 'enemy_boneballista': 'enemy_spitter', 'enemy_lich': 'enemy_spitter',
    'enemy_mirror': 'enemy_howler', 'enemy_tunneler': 'enemy_runner', 'enemy_shieldwall': 'enemy_brute',
    'enemy_jammer': 'enemy_spitter', 'enemy_saboteur': 'enemy_runner', 'enemy_howler': 'enemy_runner',
    'enemy_crusher': 'enemy_brute', 'enemy_splitter': 'enemy_walker', 'enemy_bloater': 'enemy_walker',
    'enemy_brute': 'enemy_walker', 'enemy_spitter': 'enemy_walker', 'enemy_runner': 'enemy_walker',
}
# Per-zone shaping of the difficulty curve, from benchmark sweeps (enemy health, enemy damage, wagon health
# multipliers on the linearly re-sampled values).
ZONE_SHAPE = {z: (1.0, 1.0, 1.0) for z in range(10)}
ZONE_SHAPE.update({6: (1.08, 1.1, 1.0), 8: (1.1, 1.12, 1.0)})
# Boss stages bring the boss with its largest waves; their escorts are thinned and their health eased so a
# prepared squad can win them narrowly.
BOSS_EASE = dict(count=0.8, health=0.9)
# Individual stages, from three-seed benchmark sweeps of every boss stage: enemy health and escort count
# multipliers on top of BOSS_EASE.
STAGE_TUNE = {
    30: dict(health=0.92), 35: dict(health=0.85, count=0.85, drop=('swarm_density',)), 40: dict(count=1.2),
    50: dict(health=0.85, count=0.85), 55: dict(health=0.8, count=0.8, drop=('swarm_density',)), 60: dict(health=1.3, count=1.35),
    70: dict(health=0.82, count=0.85), 80: dict(health=0.9), 100: dict(health=0.8, count=0.85, drop=('elite_vanguard',)),
}
# Modifiers likewise arrive where the regular campaign first used them, never stronger than regular stages there
# (value caps grow linearly from the first use).
MODIFIER_INTRO = {
    'reinforced_barricade': (11, 1.12, 0.0026), 'armored_convoy': (11, 1.06, 0.0006), 'swarm_density': (14, 2.0, 0.0),
    'mirror_pressure': (46, 0.15, 0.0003), 'fortified_deploy': (51, 0.6, 0.0), 'lich_graveyard': (57, 0.25, 0.0002),
    'elite_vanguard': (62, 1.18, 0.003), 'cursed_ground': (64, 2.5, 0.04), 'tunnel_invasion': (71, 1.0, 0.0),
    'rapid_assault': (72, 0.82, -0.003),
}
# Remix waves were authored as tight bursts for the postgame; the first four zones space them out.
EARLY_BURST_LIMIT = (40, 0.45)


def tame_modifiers(stage):
    n = stage['StageNumber']
    kept = []
    for m in stage.get('Modifiers', []):
        intro = MODIFIER_INTRO.get(m['Type'])
        if intro is None:
            kept.append(m)
            continue
        first, cap, growth = intro
        if n < first:
            continue
        limit = cap + growth * (n - first)
        m = dict(m)
        if 'Value' in m:
            # rapid_assault shrinks intervals: its cap is a floor
            m['Value'] = round(max(m['Value'], limit) if growth < 0 or m['Type'] == 'rapid_assault' else min(m['Value'], limit), 3)
        kept.append(m)
    if kept:
        stage['Modifiers'] = kept
    else:
        stage.pop('Modifiers', None)
    if not any(m['Type'] == 'cursed_ground' for m in kept):
        stage['Battlefield']['CursePatches'] = []


def introduce(stage):
    n = stage['StageNumber']
    for w in stage['Waves']:
        merged = {}
        for e in w['Entries']:
            unit = e['UnitId']
            while INTRODUCED.get(unit, 0) > n:
                unit = STAND_IN[unit]
            merged[unit] = merged.get(unit, 0) + e['Count']
        w['Entries'] = [{'UnitId': u, 'Count': c} for u, c in merged.items()]
        if n <= EARLY_BURST_LIMIT[0]:
            w['SpawnInterval'] = max(w['SpawnInterval'], EARLY_BURST_LIMIT[1])


def interp(table, x):
    """Linear interpolation over [(stage, value)] sorted by stage."""
    for (x0, y0), (x1, y1) in zip(table, table[1:]):
        if x0 <= x <= x1:
            t = (x - x0) / (x1 - x0) if x1 > x0 else 0
            return y0 + (y1 - y0) * t
    return table[-1][1] if x > table[-1][0] else table[0][1]


def progress(new_number):
    """Where a new stage number sits on the old 1-60 difficulty curve."""
    return 1 + (new_number - 1) * (OLD_COUNT - 1) / (NEW_COUNT - 1)


def is_boss_stage(stage):
    return any(e['UnitId'].startswith(BOSS_PREFIX) for w in stage['Waves'] for e in w['Entries'])


def plan_zone(stages):
    """Returns ten (old_stage_or_None) slots; None marks a new stage."""
    mains = sorted((s for s in stages if s['StageNumber'] <= LAST_MAIN), key=lambda s: s['StageNumber'])
    post = sorted((s for s in stages if s['StageNumber'] > LAST_MAIN), key=lambda s: s['StageNumber'])
    main_boss = mains[-1]
    post_bosses = [s for s in post if is_boss_stage(s) and any(
        e['UnitId'].startswith(BOSS_PREFIX) and e['UnitId'] != 'enemy_boss_citadel' for w in s['Waves'] for e in w['Entries'])]
    slots = [None] * 10
    if post_bosses:
        final = post_bosses[-1]
        slots[9], slots[4] = final, main_boss
        others = mains[:-1] + [s for s in post if s is not final]
        for i, s in zip((0, 1, 3, 6), others):
            slots[i] = s
    else:
        slots[9] = main_boss
        others = mains[:-1] + post
        for i, s in zip((0, 1, 3, 5, 7), others):
            slots[i] = s
    assert sum(s is not None for s in slots) == 6, [s and s['StageNumber'] for s in slots]
    return slots


def roster(stage):
    return [e['UnitId'] for w in stage['Waves'] for e in w['Entries'] if not e['UnitId'].startswith(BOSS_PREFIX)]


def build_new(prev, nxt, name, tip, index):
    s = copy.deepcopy(prev)
    s['StageName'], s['Description'] = name, tip
    for key in ('WeatherId',):
        if key in nxt:
            s[key] = copy.deepcopy(nxt[key])
        else:
            s.pop(key, None)
    s['BossSpawnStartTime'] = 0.0
    waves = []
    spice = [u for u in roster(nxt) if u not in ('enemy_walker',)] or roster(nxt)
    for i, w in enumerate(prev['Waves']):
        w = copy.deepcopy(w)
        w['Entries'] = [e for e in w['Entries'] if not e['UnitId'].startswith(BOSS_PREFIX)]
        if not w['Entries']:
            w['Entries'] = [{'UnitId': 'enemy_walker', 'Count': 2}, {'UnitId': spice[i % len(spice)], 'Count': 1}]
        # trade the most common unit of the wave for one from the next stage's roster
        if i % 2 == index % 2 and spice:
            unit = spice[(i + index) % len(spice)]
            w['Entries'].sort(key=lambda e: -e['Count'])
            head = w['Entries'][0]
            if head['Count'] > 1 and unit not in [e['UnitId'] for e in w['Entries']]:
                head['Count'] -= 1
                w['Entries'].append({'UnitId': unit, 'Count': 1})
        if i < len(nxt['Waves']) and i % 2 == 1 and not any(e['UnitId'].startswith(BOSS_PREFIX) for e in nxt['Waves'][i]['Entries']):
            w['Label'] = nxt['Waves'][i]['Label']
        waves.append(w)
    s['Waves'] = waves
    bf = s['Battlefield']
    bf['OutpostYRatio'] = round(1.0 - bf['OutpostYRatio'], 3)
    bf['SupplyYRatio'] = round(1.0 - bf['SupplyYRatio'], 3)
    total = sum(e['Count'] for w in waves for e in w['Entries'])
    for o in s.get('Objectives', []):
        if o['Type'] == 'enemy_defeats':
            o['Value'] = float(min(o['Value'], max(8, total - 4)))
    return s


SQUAD = re.compile(r'\s*Recommended squad:[^\n]*')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--source', default=str(ROOT / 'scripts/analysis/legacy/stages-60.json'))
    ap.add_argument('--write', action='store_true')
    args = ap.parse_args()
    data = json.load(open(args.source))
    old = sorted(data['Stages'], key=lambda s: s['StageNumber'])
    if len(old) != OLD_COUNT:
        raise SystemExit(f'expected the {OLD_COUNT}-stage campaign, found {len(old)} stages')
    curves = {k: [(s['StageNumber'], s[k]) for s in old] for k in LINEAR}
    gold = [(s['StageNumber'], math.log(s['RewardGold'])) for s in old]
    capture = [(s['StageNumber'], s['Battlefield']['CaptureSeconds']) for s in old]
    forward = [(s['StageNumber'], s['Battlefield']['ForwardDeployments']) for s in old]
    units = json.load(open(ROOT / 'data/units.json'))
    unit_list = units['Units'] if isinstance(units, dict) else units
    by_name = {u.get('DisplayName'): u['Id'] for u in unit_list}

    out, mapping = [], {}
    for z, zone in enumerate(ZONES):
        zone_stages = [s for s in old if s['MapId'] == zone]
        slots = plan_zone(zone_stages)
        fresh = iter(NEW_STAGES[zone])
        built = []
        for i, src in enumerate(slots):
            n = z * 10 + i + 1
            if src is None:
                prev = next(slots[j] for j in range(i - 1, -1, -1) if slots[j] is not None)
                nxt = next(slots[j] for j in range(i + 1, 10) if slots[j] is not None)
                name, tip = next(fresh)
                st = build_new(prev, nxt, name, tip, i)
                st['_src_damage'] = prev['EnemyDamageScale']
            else:
                st = copy.deepcopy(src)
                st['_src_damage'] = src['EnemyDamageScale']
                mapping[src['StageNumber']] = n
                st['Description'] = REWRITE.get(src['StageNumber'], st['Description'])
                if src['StageNumber'] in BOSS_SWAP:
                    was, now, label = BOSS_SWAP[src['StageNumber']]
                    for w in st['Waves']:
                        for e in w['Entries']:
                            if e['UnitId'] == was:
                                e['UnitId'] = now
                                w['Label'] = label
            st['StageNumber'] = n
            built.append(st)
        out += built

    for st in out:
        introduce(st)
        tame_modifiers(st)
        n = st['StageNumber']
        p = progress(n)
        for k in LINEAR:
            v = interp(curves[k], p)
            st[k] = round(v, 3) if isinstance(st[k], float) else int(round(v))
        # hazards hit as hard as the stage's enemies now do, not as hard as their authored stage's
        ratio = st['EnemyDamageScale'] / st.pop('_src_damage')
        for h in st.get('Hazards', []):
            if 'Damage' in h:
                h['Damage'] = round(h['Damage'] * min(1.0, ratio), 1)
        if is_boss_stage(st):
            for w in st['Waves']:
                for e in w['Entries']:
                    if not e['UnitId'].startswith(BOSS_PREFIX):
                        e['Count'] = max(1, int(e['Count'] * BOSS_EASE['count'] + 0.5))
            st['EnemyHealthScale'] = round(st['EnemyHealthScale'] * BOSS_EASE['health'], 3)
        tune = STAGE_TUNE.get(n)
        if tune:
            for w in st['Waves']:
                for e in w['Entries']:
                    if not e['UnitId'].startswith(BOSS_PREFIX):
                        e['Count'] = max(1, int(e['Count'] * tune.get('count', 1.0) + 0.5))
            st['EnemyHealthScale'] = round(st['EnemyHealthScale'] * tune.get('health', 1.0), 3)
            if tune.get('drop') and 'Modifiers' in st:
                st['Modifiers'] = [m for m in st['Modifiers'] if m['Type'] not in tune['drop']]
        hs, ds, hull = ZONE_SHAPE[(n - 1) // 10]
        st['EnemyHealthScale'] = round(st['EnemyHealthScale'] * hs, 3)
        st['EnemyDamageScale'] = round(st['EnemyDamageScale'] * ds, 3)
        st['PlayerBaseHealth'] *= hull
        st['PlayerBaseHealth'] = float(round(st['PlayerBaseHealth'] / 5) * 5)
        st['EnemyBaseHealth'] = int(round(st['EnemyBaseHealth'] / 2) * 2)
        st['RewardGold'] = int(round(math.exp(interp(gold, p)) / 5) * 5)
        st['Battlefield']['CaptureSeconds'] = round(interp(capture, p) * 2) / 2
        st['Battlefield']['ForwardDeployments'] = 3 if p < 20.5 else 4
        # a squad naming units the player can't have yet would mislead
        squad = SQUAD.search(st['Description'])
        if squad:
            named = [x.strip() for x in squad.group(0).split(':', 1)[1].rstrip('.').split('+')]
            if any(UNIT_UNLOCKS.get(by_name.get(x), 999) > n for x in named):
                st['Description'] = SQUAD.sub('', st['Description']).strip()

    names = [s['StageName'] for s in out]
    assert len(set(names)) == len(names), [n for n in names if names.count(n) > 1]
    print('old->new:', json.dumps(dict(sorted(mapping.items()))))
    for z, zone in enumerate(ZONES):
        row = out[z * 10:(z + 1) * 10]
        print(f'{zone:<11}', ' '.join(
            f"{s['StageNumber']}{'*' if is_boss_stage(s) else ''}" for s in row))
    if args.write:
        data['Stages'] = out
        with open(ROOT / 'data/stages.json', 'w') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
            f.write('\n')
        print('wrote data/stages.json')
        for path, key, table in (('data/units.json', 'Units', UNIT_UNLOCKS), ('data/spells.json', 'Spells', SPELL_UNLOCKS)):
            doc = json.load(open(ROOT / path))
            for item in (doc[key] if isinstance(doc, dict) else doc):
                if item['Id'] in table:
                    item['UnlockStage'] = table[item['Id']]
            with open(ROOT / path, 'w') as f:
                json.dump(doc, f, indent=2, ensure_ascii=False)
                f.write('\n')
            print('wrote', path)


if __name__ == '__main__':
    main()
