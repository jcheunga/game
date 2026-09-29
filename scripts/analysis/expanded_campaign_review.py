"""Summarize final real-engine campaign samples; does not simulate combat itself."""
from pathlib import Path
import hashlib
import json
import statistics

ROOT = Path(__file__).resolve().parents[2]
LOGS = ROOT / 'artifacts/expanded-campaign'

def samples(name):
    text = (LOGS / f'{name}.log').read_text()
    if 'COMBAT_REVIEW_RESULT: 0 failures' not in text:
        raise RuntimeError(f'{name} did not finish cleanly')
    return [json.loads(line.split(': ', 1)[1]) for line in text.splitlines() if line.startswith('COMBAT_SAMPLE: ')]

core = samples('final-early') + samples('final-late')
assert sorted(r['stage'] for r in core) == list(range(1, 61))
specialists = sum((samples(n) for n in ['final-counter-mid', 'final-counter-late', 'final-counter-end']), [])
repeats = samples('final-repeat')
all_runs = core + specialists + repeats
stages = json.loads((ROOT / 'data/stages.json').read_text())['Stages']
passed = [r for r in core if r['won']]
winning_stages = {r['stage'] for r in all_runs if r['won']}
assert len(winning_stages) == 60

files = ['data/stages.json', 'data/combat_config.json', 'data/units.json',
         'scripts/combat/BattleController.cs', 'scripts/combat/BattleSpawnDirector.cs',
         'scripts/combat/BattleController.FieldObjectives.cs', 'scripts/combat/BattleController.FieldNavigation.cs',
         'scripts/core/StageMissionEvents.cs']
summary = dict(core=core, specialists=specialists, repeats=repeats,
    source_sha256={f: hashlib.sha256((ROOT / f).read_bytes()).hexdigest() for f in files})
(LOGS / 'review.json').write_text(json.dumps(summary, indent=2) + '\n')

lines = ['# Expanded campaign battlefield', '',
    'Implemented for all 60 authored campaign stages on the 2560-unit map. The previous length-only audit is preserved in [Stage length review](STAGE_LENGTH_REVIEW.md).', '',
    '## What changed', '',
    '- Every stage has Approach, Crossroads, and Gate encounters. Packs enter at authored points along the map. Advancing can trigger them early, with at least 2.5 seconds of warning and 5 seconds between pack starts. Existing enemy caps and staggered spawns remain; time fallback prevents waiting forever when the player holds back.',
    '- Every stage has a themed forward post. Hold it uncontested for 2.5–3.5 seconds to earn 3 deployments in stages 1–20 or 4 thereafter. Each still costs normal courage and card recovery, needs 8 seconds of post recovery, and stays near the captured lane. Nearby enemies block it; the player can switch back to the wagon to save charges. Blocked, cooling, and spent posts fall back to the wagon, including the placement preview.',
    '- Every stage has a one-time optional supply cache, held for 2.5 seconds. City, Thornwall, and Steppe caches grant 25 courage and 3 seconds of card recovery. Quarantine, Basilica, and Mire caches repair 12% of wagon hull and grant 8 courage. Harbor, Foundry, Gloamwood, and Citadel caches damage the gate by 12% and suppress enemy specials that summon or jam for 18 seconds.',
    '- Allies near a capture ring stay long enough to secure it while nearby combat takes priority. This also helps existing hold missions. Existing missions open earlier and move into reach; their hold duration and victory requirements remain intact.',
    '- The seven cursed stages (29, 43, 49, 52, 53, 55, 59) now use two separated 240/280-unit pockets instead of the 2052-unit strip. Ordinary hazards keep warning windows and are moved off forward-post approaches where needed.',
    '- Summoners and jammers wait until combat comes within 550 units or they approach the wagon. Repeated summons have finite reserves: 3 per ordinary summoner, 3 per early boss, 5 per boss after stage 20. Boss phase attacks remain. Breaching the gate stops periodic summon/jam pressure; every remaining wave and enemy still has to be defeated.',
    '- A clickable/touchable minimap shows allies, enemies, bosses, the post, supplies, the visible camera area, incoming packs, and tunnel warnings. Side buttons find offscreen enemies. The Field briefing and battle intel explain the stage rewards.',
    '- Time-star targets remain at their prior values except stage 8 (105→165 seconds) and stage 25 (150→190 seconds). Stars still reward execution and loadout choices; the targets are not battle time limits.',
    '- The new campaign mechanics are enabled only for campaign play. Shared scrolling still works in the other modes; challenge and endless spawn rules retain their existing behavior.', '',
    '## Validation', '',
    f'- {len(all_runs)} final engine simulations: all 60 stages with the starter squad, {len(repeats)} difficult-stage repeats with a second seed, and {len(specialists)} specialist runs. Every stage has at least one verified winning run.',
    f'- Starter squad on seed 0: {len(passed)}/60 wins; median successful clear {statistics.median(r["seconds"] for r in passed):.1f}s; median first contact {statistics.median(r["firstContact"] for r in core):.1f}s. All {sum(r["outpostCaptured"] for r in core)} posts and {sum(r["supplyCollected"] for r in core)} supply caches captured. Specialist squads cleared the five starter-squad losses.',
    '- These are deterministic bots in the real Godot engine, not human playtests. The starter squad uses Brawler, Shooter, and Defender at the existing review progression levels, with tactical healing/fireball. The field-aware bot chooses objective lanes and avoids forward deployment when enemies are threatening its rear. Specialist runs also equip the review armaments and use only stage-unlocked units. No gear, doctrines, or purchases are assumed.',
    '- Repeated outcomes still vary: stage 58 reaches the 300-second test cutoff with the starter squad on seed 1000, while its specialist run clears. Some late bosses take over four minutes. These remain human-playtest balance targets, not evidence of a universal easy clear or a guarantee of three stars.',
    '- Build: no warnings or errors. Field/objective and 60-stage layout checks: 389 pass. Existing combat regressions: 122 pass. Camera/input: 31 pass, including desktop and touch minimap gestures. Mobile presentation: 144 pass. Data validation: 7,492 pass. Saves were isolated with test-only suffixes.', '',
    '## Stage-by-stage results', '',
    'Core is the final seed-0 starter squad run. Specialist is the tested alternative where available, not a required loadout. “Supplies” means the optional reward was collected in the core run. Percentages locate objectives along the playable field.', '',
    '| Stage | Name | Post X / deployments | Supply reward | Core | Supplies | Specialist |',
    '|---|---|---|---|---|---|---|']
for stage, run in zip(stages, sorted(core, key=lambda r: r['stage'])):
    plan = stage['Battlefield']
    alt = [r for r in specialists if r['stage'] == run['stage']]
    alt_text = '; '.join(f'{"Win" if r["won"] else "Loss"} {r["seconds"]:.1f}s' for r in alt) or '—'
    lines.append(f'| {run["stage"]} | {stage["StageName"]} | {plan["OutpostXRatio"]:.0%} / {plan["ForwardDeployments"]} | {plan["SupplyReward"]} | {"Win" if run["won"] else "Loss"} {run["seconds"]:.1f}s · {run["stars"]}★ | {"Yes" if run["supplyCollected"] else "No"} | {alt_text} |')
lines += ['', 'Specialist squads: stages 25/26/46/50 use Defender, Grenadier, Coordinator; 52/53/56 use Lantern Guard, Ballista, Coordinator; 58/60 use Lantern Guard, Ballista, Stormcaller.', '',
    '## Reproduce', '', 'From the project root:', '', '```sh', 'dotnet build --no-restore',
    'godot --headless --path . --fixed-fps 60 res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-field-check --field-objectives',
    'godot --headless --path . --fixed-fps 60 res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-camera-check --camera',
    'godot --headless --path . --fixed-fps 60 res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-campaign-check --tactical --field-tactics --time-limit=300',
    'python3 scripts/analysis/expanded_campaign_review.py', '```', '',
    'Raw final logs and the machine-readable report with source hashes are in `artifacts/expanded-campaign/`. The summarizer expects the recorded `final-early`, `final-late`, `final-repeat`, and `final-counter-*` logs. Earlier exploratory logs are retained separately and excluded from the final totals.', '']
(ROOT / 'docs/EXPANDED_CAMPAIGN.md').write_text('\n'.join(lines))
print(f'Report: {len(core)} stages, {len(passed)} starter wins, {len(winning_stages)} stages with a verified win; {len(all_runs)} final simulations.')
