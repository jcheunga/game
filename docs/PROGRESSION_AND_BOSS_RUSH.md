# Progression, combat feedback and boss rush

Implemented prototype · 11 October 2026

**More → Caravan → Progression** opens three pages over the current map:

- **Awakening:** choose an owned ally, train its token for 100 Gold and 2 Essence, and preview the next star's actual battle stats before spending tokens and Gold. Five stars share the same permanent bonus caps as equipment; they do not multiply a new growth layer.
- **Collection:** view and claim the 24 existing milestones across six categories. The core unit total is now 32, and utility collection includes all 30 spells plus 12 tactical cards. Retired legacy recruits are excluded from completion requirements so new saves can reach 100%. Claims persist and pay once.
- **Wagon looks:** inspect the eight existing skin assets and select an unlocked appearance. These are cosmetic. Royal Guard now requires a campaign victory, rather than merely reaching the final stage; Bone Collector uses the attainable current Codex collection.

**More → Activities → Boss rush** unlocks after the King's Road boss. It contains all 14 commanders in campaign order, with levels 2–20 and the same shallow enemy curve. The old 1.2×–5× health ladder is retired. Each round has one commander and two familiar escorts. From the second round, while the commander lives, packs of walkers, runners and brutes join it every 8 seconds until that round's reinforcements run out (see 9 November below). Defeat the commander and authored escorts to advance; summoned bodies and reinforcements do not create another round requirement. Boss reinforcement paths use bounded escorts.

Between rounds the troops regroup, the wagon repairs fully, courage resets to 40, and mana returns to at least its starting amount. Boss rush costs no food. Cleared rounds award 75, 85, …, 205 Gold, banked when the run ends. Result submission pays once, records the best completed wave and completion time, and does not change campaign stars. Retry remains in boss-rush mode.

Combat now has rectangular special-cast warnings, status badges above health bars, and critical-hit callouts. **Pause → Battle guide** explains the labels and supplies an encounter tip. The courage pickup system retains manual collection and has no countdown ring.

Stage 80's packs were rebuilt around known infantry and ranged threats, with a lighter commander escort instead of stacked healing and corpse-summoning pressure. Unit base stats and level growth were not increased. A normal-speed engine run cleared it using Militia, Spearman, Swordsman, Archer and Field Medic at suggested levels without equipment.

## Validation boundary

The mechanics review currently passes 724 checks, including all fourteen scripted boss-rush transitions and payment, progression persistence, real star-gain previews, collection claims, skin selection and first-sighting/status feedback. Desktop and phone interface checks cover native progression actions, boss-rush launch and the combat guide. These checks do not prove every squad can win the rush.

Normal-speed gameplay checks are authoritative for encounter balance. Exploratory accelerated runs showed timing differences and are not used as proof of balance. Stage 95 clears at normal speed with three stars in 206.8 seconds after a matching encounter adjustment. The no-equipment normal-speed rush cleared 13 commanders; the final commander clears in a focused normal-speed check using earned relics and level-3 wagon upgrades. A continuous equipped run has not been claimed. Wider squad and physical-device playtesting remain necessary.

**6 November 2026 — the final commander stays hard.** `--bossrush-gameplay` now plays the rush with the campaign's scripted player (courage packs collected, spells aimed, caravan spell fired) and the endgame squad the tower and events check against (every troop level 20, wagon tracks at their rank cap). That squad clears commanders 1–13 without the wagon taking damage. The Ashen Regent, whose rally call keeps adding walkers, falls in every fresh start (13 of 13), but at the end of a full run it fell in only 3 of 11. The difference was not traced: round-start troop and enemy stats, courage rate and cooldowns match a fresh start. The commander was left as it is on purpose, as the rush's real test. A full run passes when it clears the first thirteen and reaches the Ashen Regent (`BOSS_RUSH_FINAL` names the commander that held, if any); a run started there with `--rush-start=13` must win.

With `--squad=stage:level` the rush plays with the squad a player has on reaching that stage. On 9 November squads from stages 30 (troop level 8), 50 (10) and 70 (14) all cleared commanders 1–12 without the wagon taking damage (rounds are a commander and two escorts, at most four foes) and all fell to the Dread Sovereign, whose summons put 9–12 foes on the field. A stage-100 squad at level 17 cleared all fourteen (lowest wagon health 33%). The rush is a cliff: twelve walkover rounds, then two hard ones.

**9 November 2026 — reinforcements smooth the climb.** Each commander from the second to the twelfth now calls reinforcements while it lives: a pack (one troop, two from round 5, three from round 9) every 8 seconds until its round's allowance runs out. The allowances are 0, 1, 2, 3, 4, 3, 6, 5, 13, 10, 7, 15 for rounds 1–12. The last two commanders summon their own reinforcements and get none. Reinforcements drop no courage and leave when their commander's round ends. A squad that kills the commander quickly meets few of them; a squad that can't meets them all.

The allowances are set per commander because the commanders are uneven. The ranged Harrow Tidemaster (3), the armoured Plague Monarch (6), the Bone Pontiff (8) and the Steppe Warlord (11) get fewer than their neighbours. Three other approaches were tried and rejected:

- **One formula for every commander.** Rounds 3 and 6 became walls for every squad.
- **Uncapped reinforcements.** Slow commanders snowballed, and round 3 beat even a stage-70 squad.
- **Bigger opening packs.** Up to 12 walkers, runners and brutes at once, or packs drawn from each commander's own stage, barely touched the wagon. The exception was the Plague Monarch's shieldwalls, healers and splitters, which made round 6 a spike.

Where each squad fell, two seeds each. The first seed ran before rounds 9 and 12 were raised from 10 and 12 to 13 and 15; no squad that reached those rounds took damage there either way.

| Squad | First wagon damage | Fell at |
|---|---|---|
| Stage 30, troop level 8 | round 6 | rounds 10 and 11 |
| Stage 70, troop level 14, the playthrough sim's deck (`--deck`) | round 6 | rounds 6 and 10 |
| Stage 100, troop level 17 | round 11 | rounds 13 and 14 |
| Endgame (level 20), plus a third seed | round 6 or 11 | round 14 in three runs; the fourth cleared all fourteen |

Squads from stages 30, 50 and 70 perform alike in this sim, because troop base stats barely rise with later unlocks. The rush therefore can't separate them round by round. What it does separate is a mid-campaign squad from an endgame one. Before reinforcements, every one of these squads cleared rounds 1–12 untouched.

The endgame squad still always reaches the Ashen Regent, so `--bossrush-gameplay` passes. A run started at the Regent (`--rush-start=13`) still wins.

```sh
godot --headless --path . scenes/tests/ContentRevampSmoke.tscn -- --save-suffix=content-review-expansion
godot --path . scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-expansion --expansion
godot --headless --path . scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-rush --bossrush-gameplay
godot --headless --path . scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-rush-final --bossrush-gameplay --rush-start=13
```

## Retreat polish — 12 October 2026

Quitting a boss rush now records its completed commanders, pays their earned gold once, and returns to the boss-rush menu. It does not change campaign stars. A completed or failed run still uses the same idempotent reward path.

## Challenge tower — 3 November 2026

Each floor card listed battle modifiers that tower battles never applied: a tower battle used its campaign stage and the
floor's health and damage multipliers only. Floors now play with their modifiers, added to a copy of the campaign
stage, at the strengths the campaign authored them with:

| Floors | Modifiers |
| --- | --- |
| 1–20 | None |
| 21–40 | Elite vanguard (enemy health and damage +18 %) |
| 41–60 | Elite vanguard, rapid assault (18 % less time between enemy spawns) |
| 61–80 | Rapid assault, cursed ground (2.5 damage a second in a strip along the middle of the lane) |
| 81–100 | Elite vanguard, cursed ground, fortified deploy (a troop just placed takes 40 % less damage for 4 s) |

Cursed ground had never been shown to players. It drew a debug label over the lane for the whole battle; it is now a
faint violet haze over the strip.

Measured before the change, the tower's curve (enemy health ×1 to ×5, damage ×1 to ×3.5 on top of each campaign
stage) was beyond any squad: troops all at level 20 with every unit and full wagon upgrades, but no relics, won floor 25
and lost floor 30 and every floor sampled above it. A tower battle is also harder than its campaign stage at ×1,
because it lacks the campaign's battlefield help: stage 100 is won with the wagon untouched, floor 100 at ×1 with 6 %
of it left.

Every floor now has its own fitted strength (`ChallengeTowerCatalog.Strength`). `CombatReviewSmoke --tower-fit` plays
each floor at fixed 60 Hz steps with a scripted player and finds the strongest enemies (health 1 + 4s, damage
1 + 2.5s, with the floor's modifiers) that the floor's reference player beats on two seeds, between s = −0.1 (health
×0.6, damage ×0.75) and the original curve. The reference player has the campaign stage's troops, at that stage's
level cap rising evenly to level 20 by floor 100, hull, stores and march drum at their level's wagon rank cap,
fireball, and no relics.
`scripts/analysis/tower_fit.py` writes the table, 0.02 inside each measured edge; floors whose player beat the original
curve keep it.

- Floors 1–39 keep the original curve except the zone bosses (10, 20, 30) and floor 35.
- Seven floors sit a little below their campaign stage (s from −0.004 to −0.1): the zone bosses 40, 70, 80 and 100,
  and floors 35, 55 and 95.
- The hardest floor is 94 (enemy health ×3.0, damage ×2.3); the original curve reached ×5 and ×3.5.

So a floor asks for troops trained past the campaign, and floor 100 for every troop at level 20; relics, a better deck
and better play give headroom. `CombatReviewSmoke --tower` checks the modifiers reach the battle and that the
reference player still beats floors 21, 61 and 100 as fitted. When campaign stages, modifiers or troops change and
that fails, refit (about 15 minutes across six processes):

```sh
godot --headless --path . res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-towerfit-1 --tower-fit --floors=1,7,13 > fit-1.log
python3 scripts/analysis/tower_fit.py fit-*.log --write
```

`--tower-balance [--floors=] [--squad=] [--unit-level=] [--strength=] [--modes=campaign,plain,modifiers]` prints
single runs for comparison. The scripted player is weaker than a person: it deploys whatever is ready and throws
fireball at the nearest enemy, so these numbers are a floor, not proof that every squad wins. Playtest the late floors
on a phone.

## Seasonal events — 4 November 2026

Both events had fixed 2026 dates (Midwinter Siege 20 March – 3 April, Harvest Moon 1–15 May) that had passed, so
neither would have run after launch. They now return every year: Midwinter Siege from 15 December to 5 January, and
Harvest Moon from 20 September to 10 October. Each year's run keeps its own clears and milestone claims, under keys
such as `midwinter_siege@2026` (the year the run opened); a save's older bare keys count toward no year. Festival
Champion counts a full run in any year. Between runs the hub's Event tile names the day the next one opens.

The event screen promised "their own modifiers", and as on the tower, event battles never applied them. Each event
stage now plays with its modifiers at their authored strengths (`StageModifiers.AuthoredStrength`, shared with the
tower), and its row lists them under the stage name, with the foes' health and damage beside it.
`CombatReviewSmoke --event-fit` found every event stage beatable at its authored strength, with its modifiers, by a
player who just cleared its campaign stage (troops at that stage's level cap, no relics), so none was retuned.
`CombatReviewSmoke --events` checks the windows (including the run across the new year), the per-year keys, that the
battle plays with the modifiers, and that the reference player beats every stage.

## Tower and events on the tuned campaign — 5 November 2026

The campaign now has a per-stage strength table (`CampaignPacing`, see [the playthrough simulation](PLAYTHROUGH_SIM.md)),
new waves ([campaign waves](CAMPAIGN_WAVES.md)), a steeper level curve and slower courage. Tower floors and event
stages are built on campaign stages, so:

- They apply the campaign stage's strength first and their own fitted strength on top, as the tower screen already
  says ("Enemy health compared with the battlefield's campaign stage").
- The reference player (`SeedTowerSquad`, used by `--tower`, `--tower-fit`, `--events` and `--event-fit`) is the
  campaign fit's trained player: every wagon track at its rank cap, the spell at level 3 and the milestone relics. It
  had three wagon tracks, an untrained Fireball and no relics.
- `--event-fit` and repeat runs of `--events` eased all ten event stages, to enemy health ×0.60–1.08 (they were
  ×1.10–1.50), with a margin: these battles don't repeat exactly from run to run (relic order was one cause, now
  fixed; others remain), so an exact fit fails some runs. `--tower-fit` refitted every floor (s from −0.15 to 0.13 on
  top of the tuned stage). Its search floor can go below −0.1 with `--tower-lowest`, used for the boss floors 20, 78
  and 90, which the reference player could not win at −0.1; the catalog check accepts floors down to s = −0.2.
- 9 November 2026: campaign stages 1–70 got harder ([as hard as possible](PLAYTHROUGH_SIM.md#as-hard-as-possible-9-november-2026)),
  so `--tower-fit` refitted floors 1–70 (their own s fell 0.02 on average; floors 71–100 sit on stages that kept
  their strength), and `--event-fit` with `--events` runs eased four event stages with a margin: Midwinter
  Siege 1 (stage 34) to ×0.74 / ×0.84, Midwinter Siege 2 (stage 51) to ×0.92 / ×0.97 and Harvest Moon Hunt 1
  (stage 22) to ×0.98 / ×1.02. Harvest Moon Hunt 3 (stage 57), already at the fit's floor of ×0.60 / ×0.75, lost
  one `--events` run in six and went to ×0.55 / ×0.72.
