"""Write the alternate campaign wave versions in data/waves/ from the standard waves in data/stages.json.

Each version file lists every zone and stage. Edit it by hand afterwards: this script only writes a file
that does not exist yet, unless --force is given. A stage left out of a version keeps its standard waves.

  python3 scripts/tools/build_wave_versions.py            # write missing versions
  python3 scripts/tools/build_wave_versions.py --force    # rebuild both from the current standard waves
"""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WAVES = ROOT / 'data' / 'waves'

stages = json.loads((ROOT / 'data/stages.json').read_text())['Stages']
units = {u['Id']: u for u in json.loads((ROOT / 'data/units.json').read_text())['Units']}
names = {uid: u['DisplayName'] for uid, u in units.items()}
zones = list(dict.fromkeys(s['MapId'] for s in stages))
SWARMERS = {'enemy_grave_rat', 'enemy_plague_leech', 'enemy_bog_spore'}


def standard_boss(stage):
    return next((e['UnitId'] for w in stage['Waves'] for e in w['Entries'] if units[e['UnitId']].get('IsBoss')), None)


def entries(ids):
    return [{'UnitId': uid, 'Count': ids.count(uid)} for uid in dict.fromkeys(ids)]


def wave(label, index, gap, ids, interval=.85, advance_step=.16):
    return {
        'Label': label, 'TriggerTime': 3 + index * gap, 'SpawnInterval': interval,
        'Area': ['Approach', 'Crossroads', 'Gate'][min(2, index)],
        'AdvanceTriggerXRatio': -1 if index == 0 else round(min(.82, .2 + index * advance_step), 2),
        'Entries': entries(ids),
    }


# ---- Horde: Dead Ahead pressure. Many light bodies on short gaps around the standard wave's featured enemies.

HORDE_LABELS = ['Shambling mob', 'Ghoul pack', 'Grave surge', 'Rat tide', 'Second surge', 'The swell', 'Last tide', 'Overrun']


def horde(stage):
    n = stage['StageNumber']
    standard = stage['Waves']
    fodder = ['enemy_walker'] + (['enemy_runner'] if n >= 2 else []) + (['enemy_grave_rat'] if n >= 5 else [])
    count = min(8, len(standard) + 2)
    waves = []
    for wi in range(count):
        source = standard[wi * len(standard) // count]
        featured = [e['UnitId'] for e in source['Entries'] if e['UnitId'] not in fodder]
        if n > 50: featured += [uid for uid in featured if not units[uid].get('IsBoss')][:1]
        bodies = min(12, 2 + n // 10 + wi // 2)
        ids = [fodder[(n + wi + i) % len(fodder)] for i in range(max(1, bodies - len(featured)))]
        boss = [uid for uid in featured if units[uid].get('IsBoss')]
        if wi < count - 1: featured = [uid for uid in featured if not units[uid].get('IsBoss')]
        elif not boss and standard_boss(stage): featured.append(standard_boss(stage))
        waves.append(wave(HORDE_LABELS[wi], wi, 10 if n < 11 else 9, ids + featured, .45, .11))
    return waves


# ---- Regional: each zone fields its own four regional enemies beside the opening four, in fewer, heavier packs.

REGIONAL_LABELS = ['Local patrol', 'Border watch', 'Muster', 'Warband', 'Elite guard', 'Commander escort']
BASICS = ['enemy_walker', 'enemy_runner', 'enemy_brute', 'enemy_spitter']


def regional_roster(zone):
    """The zone's regional enemies in the order they appear: lightest at slot 2, the official debut at slot 4."""
    debut = next(s['NewEnemyId'] for s in stages if s['MapId'] == zone and s['NewEnemyId'])
    others = sorted((uid for uid, u in units.items() if uid.startswith('enemy_') and u.get('HomeZone') == zone
                     and u.get('ContentBehavior') and not u.get('IsBoss') and not u.get('NonCombatant') and uid != debut),
                    key=lambda uid: units[uid]['MaxHealth'] * units[uid]['AttackDamage'])
    return {2: others[0], 4: debut, 6: others[1], 8: others[2]}


def regional(stage):
    n, zone = stage['StageNumber'], stage['MapId']
    z, local = zones.index(zone), (n - 1) % 10 + 1
    roster = regional_roster(zone)
    known = [uid for slot, uid in sorted(roster.items()) if slot <= local]
    newcomer = roster.get(local)
    basics = BASICS[:1 + (n >= 2) + 2 * (n >= 3)]
    count = len(stage['Waves'])
    boss = standard_boss(stage)
    waves, used = [], {'local': n, 'basic': n}
    for wi in range(count):
        bodies = (1 if n <= 2 else 2) + (z >= 2) + (z >= 5) + (wi == count - 1 and n > 3)
        ids = []
        for i in range(bodies):
            # Alternate local and familiar bodies, leading with a local one from the second wave on; each pool
            # keeps rotating across the stage's waves so every known type gets its turn.
            kind = 'local' if known and (i + wi) % 2 == 1 else 'basic'
            pool = known if kind == 'local' else basics
            pick = pool[used[kind] % len(pool)]
            used[kind] += 1
            ids += [pick] * (2 if pick in SWARMERS else 1)
        used['basic'] += 1  # shift the pairing, so the same two packs do not simply alternate
        label = REGIONAL_LABELS[wi]
        if newcomer and wi == 1:
            ids = ['enemy_walker', newcomer] + (['enemy_brute'] if z >= 3 else [])
            label = 'First sighting: ' + names[newcomer]
        if boss and wi == count - 1: ids.append(boss)
        waves.append(wave(label, wi, 14 if n < 11 else 12, ids))
    return waves


VERSIONS = [
    ('horde', 'Horde', 2, 'Dead Ahead pressure: many light bodies on short gaps, built around each standard wave\'s featured enemies. Grave rats join the fodder from stage 5.', horde),
    ('regional', 'Regional', 3, 'Each zone fields its own four regional enemies beside the opening four, in fewer and heavier packs. Newcomers arrive at slots 2, 4, 6 and 8.', regional),
]


def dump(version):
    """Indented JSON with each wave entry on one line, so a stage reads as a short block."""
    text = json.dumps(version, indent=2, ensure_ascii=False)
    lines, out, i = text.split('\n'), [], 0
    while i < len(lines):
        if lines[i].strip() == '{' and i + 3 < len(lines) and '"UnitId"' in lines[i + 1] and '"Count"' in lines[i + 2]:
            indent = lines[i][:len(lines[i]) - len(lines[i].lstrip())]
            out.append(f'{indent}{{ {lines[i + 1].strip()} {lines[i + 2].strip()} {lines[i + 3].strip()}')
            i += 4
            continue
        out.append(lines[i])
        i += 1
    return '\n'.join(out) + '\n'


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--force', action='store_true', help='overwrite versions that already exist (loses hand edits)')
    args = parser.parse_args()
    WAVES.mkdir(exist_ok=True)
    for vid, title, order, description, build in VERSIONS:
        path = WAVES / f'{vid}.json'
        if path.exists() and not args.force:
            print(f'kept {path.relative_to(ROOT)} (exists; --force rebuilds it)')
            continue
        version = {
            'Id': vid, 'Name': title, 'Order': order, 'Description': description,
            'Zones': [{
                'MapId': zone, 'MapName': next(s['MapName'] for s in stages if s['MapId'] == zone),
                'Stages': [{'StageNumber': s['StageNumber'], 'StageName': s['StageName'], 'Waves': build(s)}
                           for s in stages if s['MapId'] == zone],
            } for zone in zones],
        }
        path.write_text(dump(version))
        total = sum(e['Count'] for z in version['Zones'] for s in z['Stages'] for w in s['Waves'] for e in w['Entries'])
        print(f'wrote {path.relative_to(ROOT)} · {total} enemies over {len(stages)} stages')


if __name__ == '__main__':
    main()
