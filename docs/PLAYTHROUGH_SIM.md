# Whole-campaign playthrough simulation

Implemented · November 2026

`CombatReviewSmoke --playthrough-sim` plays the whole campaign, stage 1 to 100, from a new save. Every battle is the
real `BattleController` stepped at a fixed 60 Hz and played by a scripted player, and every reward goes through the
game's own victory path (stage gold, first-clear food, XP and level caps, milestone recruits and relics,
achievements). Between battles the player spends gold the way a focused player would, opens the atlas's supply tiles,
and lives within the food supply.

```sh
# Both profiles in parallel, with the pacing checks, then the zone-by-zone report (10-20 minutes):
scripts/tests/playthrough_check.sh

# One run by hand:
godot --headless --path . res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-playthrough \
    --playthrough-sim --profile=free --out=free
python3 scripts/analysis/playthrough_report.py artifacts/playthrough/free.json [more runs...]
```

Options: `--profile=free|spender`, `--to-stage=N`, `--daily-minutes=60` (play time a day), `--budget=1000`
(the spender's dollar limit), `--max-grind=300` (replays at one stage before the run gives up and reports a wall),
`--out=name` (writes `artifacts/playthrough/<name>.json`), and the check bands `--expect-replays=a-b`,
`--expect-battles=a-b`, `--expect-dollars=a-b`, `--expect-days=a-b`.

## The simulated player

- **Squad:** two melee troops, two ranged and one support, each the newest owned troop of its role unless it is far
  behind in training (score: unlock stage + 2 × level), plus the newest area spell (Fireball, then Lightning Strike,
  Meteor and Chain Lightning). Boss relics are equipped on the squad.
- **Shopping, before every battle:** claim level rewards; recruit a newly unlocked troop when it would take a place in the
  squad straight away (so no gold goes on troops left on the bench);
  buy the next area spell; then buy upgrades cheapest first until the gold runs out: squad troops up to the level
  cap, the spell up to level 3, and eight wagon tracks (hull, stores, march drum, archers, axles, ballista, volley,
  repairs) up to the wagon rank cap. Wagon tracks count at half (hull, stores, march drum) or four fifths of their
  price in that ordering; cheapest-first alone starved the wagon, since troop levels cost less.
- **In battle:** taps courage packs about a second after they drop, keeps two troops in front, deploys whatever else
  is ready, aims area spells at the thickest group of enemies, and fires the caravan spell at three enemies or a
  commander. A person plays better; treat the results as a floor.
- **Losing:** two quick retries the first time, then a grind before every further try: replay the last beaten stage
  until the replays have bought about a level of training for the whole squad (or lifted a level cap when nothing
  under the caps is left), three to fifteen replays.
- **Food and time:** a battle costs its rations and takes its battle time plus 40 seconds of menus. Rations recharge
  2 every five minutes up to 24. A short supply is refilled with gold (three a day, when 600 gold is spare), or the
  player comes back when the store is full. A day holds `--daily-minutes` of play.
- **Spender:** where the free player would grind for gold, the spender buys the smallest gold pack that covers every
  upgrade the caps allow; out of food, a food pack. Level caps still need XP, so a spender grinds those.

## What it found, and the retune (5 November 2026)

Before: the scripted player cleared all 100 stages in 100 battles with **no losses**; the wagon's hull was never
scratched; the campaign took about 4 hours of play over 4 days. Gold was never short. Worse for any retune, training
barely mattered: on the old shallow level curve (+1.5% health, +1% damage a level) a squad trained to its caps beat
foes hardly stronger than one four levels behind (stage 50: 1.44 against 1.38 campaign strength), so a stuck player
had nothing worth grinding for, and nothing worth buying.

The retune, in order of effect. The aim (the user's, on 5 November): finishing the campaign is much harder unless you
pay, but a free player who puts in the time still finishes.

1. **Steeper level curve** (`UnitBalance`): +4.5% health and +3% damage a level, for troops and foes alike (level 20:
   +85.5% and +57%). Four levels of training are now worth about a fifth of a squad's strength; the gap between a
   lagging squad and one at its caps was 0.4–1.8 campaign strength on the old waves (median 0.88). See
   [progression balance](PROGRESSION_BALANCE.md).
2. **Campaign strength** (`CampaignPacing.Strength`, campaign battles only): each stage's enemies get health ×(1 + s)
   and damage ×(1 + s/2). `--campaign-fit` measured, for every stage, the strongest s beaten by a player who just
   reached it with troops 4 levels and the wagon 2 ranks behind the caps (s_lag), and with everything at the caps
   (s_cap). `scripts/analysis/campaign_fit.py` puts leaders 40% of the way from s_lag to s_cap, mid-bosses 65% and
   zone bosses 75%, ramped in over the first zone, with leaders capped at 2.25. A player who never pays sits at about
   s_lag, so most stages ask for training they have to grind gold for; a squad trained to its caps, which a purchase
   buys at once, wins them. Refitted to the new waves and economy (items 3 and 4), the mean s is 0.55 (enemy
   health ×1.55), from −0.4 to 2.0: stages field far more foes than before, so each needs less health. The new waves
   narrowed the lagging-to-trained gap to a median of 0.37 (15 stages measured; the rest estimated from it).
3. **Dead Ahead's economy** ([comparison](DEAD_AHEAD_COMPARISON.md#costs-and-waves-november-2026)): courage starts at 10
   and regenerates 2 a second (was 0 and 3); deploy cooldowns are half as long again (5–21 s); mana is 5 a kill (was
   7). Recruit prices climb with the unlock stage (60 × 1.04^(stage − 1): 60 at stage 2, 400 at 50, 2,600 at 98),
   spell prices likewise (50 × 1.035^(stage − 1)), and training grows 18% a level (40 gold for level 2, 785 for level
   20, 4,935 in all; was 35 + 7 a level). Wagon tracks cost half as much again. A squad kept at its caps costs about
   twice what first clears pay.
4. **New waves** ([campaign waves](CAMPAIGN_WAVES.md)): each stage plays a recipe (vanguard, swarm, raiders, shield
   line, siege, necromancy, ambush, elite, long march, commander), all 59 ordinary foes are introduced one a stage, and
   a leader ends in a Reckoning surge about two minutes in. Stages field two to three times the foes they did.
5. **Replays cost food**: a stage already won costs 6 rations to replay (a first attempt or retry costs 2), so the
   grind draws on the food store (24, recharging 2 every five minutes) or on food packs.
6. **Tower and events on the tuned stage.** A tower floor or event stage is a campaign stage made stronger, so they
   now apply the stage's campaign strength first and their own fitted strength on top (the tower screen already
   compares a floor's foes with "the battlefield's campaign stage"). Their reference player (`SeedTowerSquad`) is now
   the campaign fit's trained player: every wagon track at its rank cap, the spell at level 3 and the milestone
   relics. `--event-fit` and three repeat runs of `--events` eased all ten event stages to enemy health ×0.60–1.08
   (they were ×1.10–1.50), with a margin, since these battles don't repeat exactly from run to run. `--tower-fit`
   refitted every floor: s from −0.15 to 0.13 on top of the tuned stage (the boss floors 20, 78 and 90 needed
   `--tower-lowest=-0.2`).

### Pacing after the retune

| Run | Battles | Losses | Grind replays | Days at 1 h/day | Play hours | Money |
| --- | --- | --- | --- | --- | --- | --- |
| Before (free) | 100 | 0 | 0 | 4 | 4.0 | $0 |
| Free player | 324 | 85 | 184 | 24 | 21.4 | $0 |
| Spender | 184 | 40 | 44 | 12 | 11.9 | $93.90 (ten gold packs, most of it on late recruits and training) |

The free player's long walls are stages 9–10 (the first boss, 30 replays), 67 (81 replays) and 95 (the Citadel
mid-boss, 59 replays); its food store ran dry four times while grinding. The spender buys its way past every gold
wall but still replays 44 times before the stage 50 boss, where the first-clear level cap rather than gold was the
limit. Runs with the same settings differ a lot: battles are seeded, but one different early loss shifts every later
seed and purchase, so treat a run's totals as a sample (`playthrough_check.sh` checks bands, not exact counts).

### Settings tried on the way

| Setting | Free player | Spender |
| --- | --- | --- |
| Before the retune | 100 battles, 0 losses, 4 days | – |
| Leaders just under s_lag, bosses 45%, training 40 + 8, wagon unchanged | 161–250 battles, 9–16 days | $9.99, 103 battles |
| Leaders 40%, bosses 85%, training 60 + 12, wagon ×1.5 | 656 battles (419 losses), about 40 days | $48.93, 176 battles; stage 50 still took 24 losses at its caps |
| Leaders 40%, bosses 75%, same prices, longer grinds between tries | 515 battles (234 losses), 30 days | $40.95, 132 battles |
| The same, plus Dead Ahead's economy, the new waves and 6-ration replays, refitted (current) | 324 battles (85 losses), 24 days | $93.90, 184 battles |

The first was the earlier "kind of need to grind" pass; the user asked for much harder unless you pay. At 85% the
bosses sat inside the fit's seed noise, so even a squad at its caps lost repeatedly (stage 50); 75% keeps them
within reach of a paying player. The simulated player used to retry three times per grind cycle; it now grinds about
a level of training before each further try, as a stuck person would, which turns most of those losses into
replays.

### Observations outside the retune

- **Food.** Before replays cost 6 rations, food was never short: first-clear rations (+6), atlas finds and level
  rewards outpaced the 2-ration entry, and the free player held 400+ by stage 40. Now a grind draws the store down:
  the free player ran out four times, and in one run before the outlier clamp it spent most of 40 days on 4–18
  rations. The spender, which hardly grinds, never ran short.
- The scripted player is weaker than a person in battle and plainer in the shop (no relic crafting, promotions or
  awakening). A person should grind less than these numbers.

## Retuning

After changing stages, troops, prices or the level curve:

```sh
# Fit every stage (about an hour on four processes; more than four puts a closed-lid Mac into thermal sleep):
godot --headless --path . res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-campfit-1 \
    --campaign-fit --profiles=lag,cap --guess=2 --stages=1,5,9,13 > fit-1.log
python3 scripts/analysis/campaign_fit.py fit-*.log --write
# Then check the pacing:
scripts/tests/playthrough_check.sh
# Tower floors and event stages are built on the tuned campaign stage, so refit them too (docs/PROGRESSION_AND_BOSS_RUSH.md):
godot --headless --path . res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-towerfit-1 \
    --tower-fit --floors=1,5,9 > tower-1.log
python3 scripts/analysis/tower_fit.py tower-*.log --write
godot --headless --path . res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-eventfit --event-fit
```

Headless runs must keep audio muted (the sim calls `LiveUiReview.StopAudio` before every battle). Unmuted, the
audio server spent about 90% of each process's time mixing, and a battle took two minutes instead of three seconds.
