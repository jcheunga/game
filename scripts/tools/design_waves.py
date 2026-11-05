#!/usr/bin/env python3
"""Write the campaign's standard waves (data/stages.json) from a recipe per stage.

    python3 scripts/tools/design_waves.py            # print a summary of what would be written
    python3 scripts/tools/design_waves.py --write    # rewrite every stage's Waves and Description, and enemy debuts

Every stage plays one recipe, so neighbouring stages feel different: a balanced vanguard, a swarm of light bodies on
short gaps, fast raiders diving for the wagon, a shield line with casters behind it, a siege with engines and sappers,
the restless dead around their summoners, a quiet road that turns into an ambush, a few elite veterans, a long march
of steady columns, or a commander with escorts. Each zone shuffles the recipes over its leader slots; slot 10 (and
slot 5 where the zone has a mid-boss) is the commander.

Enemies are introduced one a stage, as a "First sighting" wave: each zone brings in its own regional foes (the ones
in units.json with its HomeZone) beside a share of the classic types, and writes each one's CampaignDebutStage, so a
battle never fields a foe the campaign hasn't shown. Each zone keeps its headline debut on slot 4 (NewEnemyId).

From stage 4 on, a leader's last wave is the Reckoning: a big mixed surge about two minutes in, so a defence that
stalls instead of breaking the gate is overrun, like Dead Ahead's timer wave. Counts grow with the zone. Difficulty is
not set here: CampaignPacing's strength table is fitted to these waves (scripts/analysis/campaign_fit.py), so refit
after rewriting them.
"""
import json
import random
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
STAGES_PATH = ROOT / 'data/stages.json'
UNITS_PATH = ROOT / 'data/units.json'

# The stage each foe is first fielded; one a stage after the opening three, each zone's headline debut on slot 4.
INTRO = {
    1: ['enemy_walker'], 2: ['enemy_runner'], 3: ['enemy_brute', 'enemy_spitter'], 4: ['enemy_rustguard'],
    6: ['enemy_grave_rat'], 8: ['enemy_bloater'],
    12: ['enemy_salt_wraith'], 13: ['enemy_splitter'], 14: ['enemy_drowned_harpooner'], 16: ['enemy_hookblade_raider'],
    17: ['enemy_saboteur'], 18: ['enemy_barnacle_guard'],
    22: ['enemy_ash_hound'], 23: ['enemy_shieldwall'], 24: ['enemy_cinder_spitter'], 26: ['enemy_powder_bandit'],
    27: ['enemy_howler'], 28: ['enemy_furnace_knight'],
    32: ['enemy_plague_leech'], 33: ['enemy_jammer'], 34: ['enemy_miasma_priest'], 36: ['enemy_rot_surgeon'],
    37: ['enemy_crusher'], 38: ['enemy_sealbreaker'], 39: ['enemy_tunneler'],
    42: ['enemy_thorn_brute'], 43: ['enemy_mirror'], 44: ['enemy_cliff_stalker'], 46: ['enemy_briar_witch'],
    47: ['enemy_boneballista'], 48: ['enemy_avalanche_bearer'],
    52: ['enemy_chain_penitent'], 53: ['enemy_lich'], 54: ['enemy_bone_cantor'], 56: ['enemy_ossuary_archer'],
    57: ['enemy_catacomb_giant'], 58: ['enemy_reliquary_eye'],
    62: ['enemy_bog_spore'], 63: ['enemy_siegetower'], 64: ['enemy_swamp_maw'], 66: ['enemy_bloated_eel'],
    67: ['enemy_bellringer'], 68: ['enemy_drowned_colossus'],
    72: ['enemy_dust_reaver'], 73: ['enemy_revenant_captain'], 74: ['enemy_bone_lancer'], 76: ['enemy_war_drummer'],
    77: ['enemy_highwayman'], 78: ['enemy_carrion_banner'],
    82: ['enemy_mirror_wisp'], 83: ['enemy_plague_engine'], 84: ['enemy_shade'], 86: ['enemy_dream_weaver'],
    88: ['enemy_wicker_giant'],
    92: ['enemy_crown_executioner'], 94: ['enemy_null_magister'], 96: ['enemy_soul_standard'], 98: ['enemy_gate_breaker'],
}

# What each foe does in a wave. A foe can fill more than one role.
ROLES = {
    'fodder': ['enemy_walker', 'enemy_grave_rat', 'enemy_splitter', 'enemy_plague_leech', 'enemy_bog_spore'],
    'fast': ['enemy_runner', 'enemy_salt_wraith', 'enemy_hookblade_raider', 'enemy_saboteur', 'enemy_ash_hound',
             'enemy_tunneler', 'enemy_cliff_stalker', 'enemy_bloated_eel', 'enemy_bone_lancer', 'enemy_shade'],
    'tank': ['enemy_brute', 'enemy_rustguard', 'enemy_bloater', 'enemy_barnacle_guard', 'enemy_shieldwall',
             'enemy_furnace_knight', 'enemy_crusher', 'enemy_sealbreaker', 'enemy_thorn_brute', 'enemy_mirror',
             'enemy_chain_penitent', 'enemy_catacomb_giant', 'enemy_swamp_maw', 'enemy_drowned_colossus',
             'enemy_wicker_giant', 'enemy_crown_executioner', 'enemy_gate_breaker'],
    'ranged': ['enemy_spitter', 'enemy_drowned_harpooner', 'enemy_cinder_spitter', 'enemy_powder_bandit',
               'enemy_miasma_priest', 'enemy_briar_witch', 'enemy_ossuary_archer', 'enemy_dust_reaver',
               'enemy_highwayman', 'enemy_mirror_wisp', 'enemy_dream_weaver', 'enemy_null_magister'],
    'artillery': ['enemy_boneballista', 'enemy_avalanche_bearer', 'enemy_reliquary_eye', 'enemy_plague_engine'],
    'support': ['enemy_howler', 'enemy_jammer', 'enemy_rot_surgeon', 'enemy_lich', 'enemy_bone_cantor',
                'enemy_bellringer', 'enemy_revenant_captain', 'enemy_war_drummer', 'enemy_carrion_banner',
                'enemy_soul_standard'],
    'siege': ['enemy_saboteur', 'enemy_siegetower', 'enemy_gate_breaker', 'enemy_bloater'],
    'summoner': ['enemy_splitter', 'enemy_lich', 'enemy_bone_cantor', 'enemy_bellringer', 'enemy_catacomb_giant'],
}

# Recipes: per wave, the roles and base counts, then the gap between waves and the spawn interval inside a wave.
RECIPES = {
    'vanguard': dict(waves=[[('fodder', 2), ('fast', 1)], [('tank', 1), ('ranged', 1), ('fodder', 2)],
                            [('fast', 2), ('ranged', 1)], [('tank', 1), ('support', 1), ('ranged', 1), ('fodder', 2)]],
                     gap=13, interval=.8,
                     labels=['Scouting party', 'Forward column', 'Flanking pack', 'Main host'],
                     text='A balanced host probes your line. Hold the front and save courage for its main body.'),
    'swarm': dict(waves=[[('fodder', 4)], [('fast', 2), ('fodder', 3)], [('fodder', 5), ('tank', 1)],
                         [('fast', 3), ('fodder', 4)]],
                  gap=10, interval=.4,
                  labels=['First swarm', 'Rising tide', 'Teeming mass', 'Flood'],
                  text='Swarms of light foes on short gaps. Area spells and cheap bodies win this road.'),
    'raiders': dict(waves=[[('fast', 2)], [('fast', 2), ('fodder', 1)], [('fast', 3), ('ranged', 1)],
                           [('fast', 3), ('tank', 1)]],
                    gap=11, interval=.6,
                    labels=['Outriders', 'Hit and run', 'Raiding party', 'Wolfpack'],
                    text='Fast raiders dive for the wagon. Keep a screen in front and cut them down early.'),
    'shieldwall': dict(waves=[[('tank', 1), ('ranged', 1)], [('tank', 2), ('ranged', 1)],
                              [('tank', 1), ('support', 1), ('ranged', 2)], [('tank', 2), ('ranged', 2)]],
                       gap=15, interval=1.0,
                       labels=['Shield line', 'Second rank', 'Locked shields', 'Iron advance'],
                       text='Armored lines with casters behind them. Break the shields, or strike past them.'),
    'siege': dict(waves=[[('tank', 1), ('fodder', 2)], [('artillery', 1), ('fodder', 2)],
                         [('siege', 1), ('tank', 1), ('fast', 1)], [('artillery', 1), ('tank', 2), ('ranged', 1)]],
                  gap=14, interval=.9, needs='siege',
                  labels=['Sappers', 'Siege train', 'Engines forward', 'Breaching party'],
                  text='Siege engines and sappers make for your wagon. Push out before the engines set up.'),
    'necro': dict(waves=[[('fodder', 3), ('summoner', 1)], [('tank', 1), ('fodder', 2)],
                         [('summoner', 1), ('ranged', 1), ('fodder', 3)], [('support', 1), ('tank', 1), ('fodder', 3)]],
                  gap=13, interval=.7, needs='summoner',
                  labels=['Restless dead', 'Grave choir', 'Bone harvest', 'Dark rite'],
                  text='The dead rise again. Cut down the summoners first, or drown in what they raise.'),
    'ambush': dict(waves=[[('fodder', 1)], [('fodder', 2)],
                          [('fast', 3), ('tank', 1), ('ranged', 2), ('fodder', 2)], [('fast', 2), ('ranged', 1)]],
                   gaps=[0, 12, 20, 16], interval=.45,
                   labels=['Quiet road', 'Stragglers', 'Ambush!', 'Closing jaws'],
                   text='A quiet road that turns into an ambush. Keep courage in reserve for the sudden surge.'),
    'elite': dict(waves=[[('tank', 1)], [('tank', 1), ('ranged', 1)], [('tank', 2)],
                         [('tank', 1), ('support', 1), ('ranged', 1)]],
                  gap=16, interval=1.2,
                  labels=['Picked men', 'Champions', 'Heavy guard', 'Elite cadre'],
                  text='Few foes, every one a veteran. Focus your fire and guard your damage dealers.'),
    'march': dict(waves=[[('fodder', 2), ('fast', 1)], [('fodder', 2), ('ranged', 1)], [('fodder', 2), ('tank', 1)],
                         [('fodder', 2), ('fast', 1)], [('fodder', 2), ('support', 1)], [('fodder', 2), ('tank', 1)],
                         [('fodder', 3), ('ranged', 1)]],
                  gap=9, interval=.7,
                  labels=['Road column', 'Second column', 'Third column', 'Fourth column', 'Fifth column', 'Rearguard',
                          'Last column'],
                  text='A long march of steady columns. Pace your courage: the columns keep coming.'),
}
LEADER_RECIPES = ['vanguard', 'swarm', 'raiders', 'shieldwall', 'siege', 'necro', 'ambush', 'elite', 'march']


def entries(ids):
    return [{'UnitId': uid, 'Count': ids.count(uid)} for uid in dict.fromkeys(ids)]


def main():
    write = '--write' in sys.argv
    doc = json.loads(STAGES_PATH.read_text())
    stages = doc['Stages']
    udoc = json.loads(UNITS_PATH.read_text())
    units = {u['Id']: u for u in udoc['Units']}
    names = {uid: u['DisplayName'] for uid, u in units.items()}
    debut = {uid: stage for stage, ids in INTRO.items() for uid in ids}
    assert all(uid in units and units[uid].get('Side') == 'Enemy' for uid in debut), 'unknown foe in INTRO'
    for role, ids in ROLES.items():
        assert all(uid in debut for uid in ids), f'{role}: foe without a debut'
    zones = list(dict.fromkeys(s['MapId'] for s in stages))
    summary = []
    for stage in stages:
        n, zone = stage['StageNumber'], zones.index(stage['MapId'])
        slot = (n - 1) % 10 + 1
        rng = random.Random(n * 7919)
        known = [uid for uid, at in debut.items() if at <= n]
        newcomers = INTRO.get(n, [])
        boss = next((e['UnitId'] for w in stage['Waves'] for e in w['Entries'] if units[e['UnitId']].get('IsBoss')), None)
        regional = {uid for uid in known if units[uid].get('HomeZone') == stage['MapId']}

        cast = {}
        def pick(role):
            # A stage casts one foe per role (two light ones) the first time the role comes up, and every wave draws
            # on that cast, so a stage is known by a handful of foes rather than a grab-bag of everything met so far.
            if role not in cast:
                pool = [uid for uid in ROLES[role] if uid in known] or [uid for uid in ROLES['fodder'] if uid in known]
                # This zone's own foes and the newest arrivals are cast most often.
                weights = [(3 if uid in regional else 1) * (1 + debut[uid] / 50) for uid in pool]
                chosen = []
                for _ in range(min(len(pool), 2 if role in ('fodder', 'fast', 'ranged') else 1)):
                    choice = rng.choices(pool, weights)[0]
                    i = pool.index(choice)
                    chosen.append(pool.pop(i)); weights.pop(i)
                cast[role] = chosen
            return rng.choice(cast[role])

        scale = .5 + .1 * zone
        def count(base, role):
            c = max(1, round(base * scale * (1.25 if role == 'fodder' else 1)))
            return c

        if n <= 3:
            # The opening three teach one thing each with a handful of bodies.
            plans = {1: [['enemy_walker'], ['enemy_walker'] * 2, ['enemy_walker'] * 2],
                     2: [['enemy_walker'] * 2, ['enemy_walker', 'enemy_runner'], ['enemy_walker', 'enemy_walker', 'enemy_runner']],
                     3: [['enemy_walker'] * 2, ['enemy_walker', 'enemy_brute'], ['enemy_walker', 'enemy_walker', 'enemy_spitter'],
                         ['enemy_brute', 'enemy_spitter', 'enemy_walker']]}[n]
            labels = ['Road patrol', 'Broken ranks', 'Crossroads pressure', 'Gate defenders']
            waves = [dict(ids=ids, label=labels[i], gap=14, interval=.85) for i, ids in enumerate(plans)]
            recipe, text = 'opening', stage['Description']
        else:
            commander = boss is not None
            if commander:
                recipe = 'commander'
            else:
                # Each zone deals its leader slots a shuffled hand of recipes; skip ones the zone can't field yet.
                order = LEADER_RECIPES[:]
                random.Random(zone * 104729).shuffle(order)
                usable = [r for r in order if not RECIPES[r].get('needs') or any(uid in known for uid in ROLES[RECIPES[r]['needs']])]
                recipe = usable[(slot - 1) % len(usable)]
            waves = []
            if recipe == 'commander':
                plan = [[('fodder', 2), ('fast', 1)], [('tank', 1), ('ranged', 1), ('fodder', 1)],
                        [('support', 1), ('tank', 1), ('fodder', 2)], [('tank', 1), ('ranged', 1)]]
                labels = ['Skirmishers', 'Honor guard', 'Bodyguard', f'{names[boss]} takes the field']
                for i, roles in enumerate(plan):
                    ids = [pick(role) for role, base in roles for _ in range(count(base, role))]
                    if i == len(plan) - 1: ids.append(boss)
                    waves.append(dict(ids=ids, label=labels[i], gap=13, interval=.8))
                text = f'{names[boss]} commands this field. Thin the escort first, then bring everything to bear.'
            else:
                r = RECIPES[recipe]
                for i, roles in enumerate(r['waves']):
                    ids = [pick(role) for role, base in roles for _ in range(count(base, role))]
                    gap = r['gaps'][i] if 'gaps' in r else r['gap']
                    waves.append(dict(ids=ids, label=r['labels'][i], gap=gap, interval=r['interval']))
                text = r['text']
            # A newcomer gets its own short wave second, beside a few familiar bodies.
            for uid in newcomers:
                ids = [uid] + [pick('fodder') for _ in range(count(2, 'fodder'))]
                waves.insert(1, dict(ids=ids, label=f'First sighting: {names[uid]}', gap=12, interval=.8))
            if newcomers:
                text = f'First sighting: {names[newcomers[0]]}. ' + text
            if recipe != 'commander':
                # The Reckoning: a big mixed surge late in the battle, for a defence that hasn't broken the gate.
                ids = [pick('fodder') for _ in range(count(3, 'fodder'))] + [pick('fast') for _ in range(count(2, 'fast'))] \
                    + [pick('tank') for _ in range(count(1, 'tank'))] + [pick('ranged') for _ in range(count(1, 'ranged'))]
                waves.append(dict(ids=ids, label='Reckoning', gap=0, interval=.5, at=110 + 5 * zone))
                text += ' Break the gate before the Reckoning.' if len(text) < 80 else ''
        # Times, entry areas and advance points. Early zones keep bursts at least .45 s apart.
        out, time = [], 3.0
        for i, w in enumerate(waves):
            if i > 0: time = max(time + max(6, w.get('gap', 12)), w.get('at', 0))
            interval = max(.45 if zone < 4 else .3, w['interval'])
            area = 'Approach' if i == 0 else 'Gate' if i == len(waves) - 1 else ['Crossroads', 'Gate'][i % 2]
            out.append({'Label': w['label'], 'TriggerTime': round(time, 1), 'SpawnInterval': interval, 'Area': area,
                        'SpawnXRatio': 1, 'AdvanceTriggerXRatio': -1 if i == 0 else round(min(.82, .2 + i * .14), 2),
                        'Entries': entries(w['ids'])})
        stage['Waves'] = out
        if n > 1: stage['Description'] = text
        total = sum(e['Count'] for w in out for e in w['Entries'])
        summary.append(f"{n:3} {recipe:10} waves {len(out)} foes {total:3} kinds {len({e['UnitId'] for w in out for e in w['Entries']}):2}"
                       f"{'  new: ' + ', '.join(names[u] for u in newcomers) if newcomers else ''}")
    for uid, u in units.items():
        if u.get('Side') == 'Enemy' and not u.get('IsBoss'):
            u['CampaignDebutStage'] = debut.get(uid, 101)
    print('\n'.join(summary))
    if write:
        STAGES_PATH.write_text(json.dumps(doc, indent=2, ensure_ascii=False) + '\n')
        UNITS_PATH.write_text(json.dumps(udoc, indent=2, ensure_ascii=False) + '\n')
        print(f'Wrote {len(stages)} stages and {len(debut)} enemy debuts.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
