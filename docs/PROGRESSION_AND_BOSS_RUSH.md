# Progression, combat feedback and boss rush

Implemented prototype · 11 October 2026

**More → Caravan → Progression** opens three pages over the current map:

- **Awakening:** choose an owned ally, train its token for 100 Gold and 2 Essence, and preview the next star's actual battle stats before spending tokens and Gold. Five stars share the same permanent bonus caps as equipment; they do not multiply a new growth layer.
- **Collection:** view and claim the 24 existing milestones across six categories. The core unit total is now 32, and utility collection includes all 30 spells plus 12 tactical cards. Retired legacy recruits are excluded from completion requirements so new saves can reach 100%. Claims persist and pay once.
- **Wagon looks:** inspect the eight existing skin assets and select an unlocked appearance. These are cosmetic. Royal Guard now requires a campaign victory, rather than merely reaching the final stage; Bone Collector uses the attainable current Codex collection.

**More → Activities → Boss rush** unlocks after the King's Road boss. It contains all 14 commanders in campaign order, with levels 2–20 and the same shallow enemy curve. The old 1.2×–5× health ladder is retired. Each round has one commander and two familiar escorts. Defeat the commander and authored escorts to advance; summoned bodies do not create another round requirement. Boss reinforcement paths use bounded escorts.

Between rounds the troops regroup, the wagon repairs fully, courage resets to 40, and mana returns to at least its starting amount. Boss rush costs no food. Cleared rounds award 75, 85, …, 205 Gold, banked when the run ends. Result submission pays once, records the best completed wave and completion time, and does not change campaign stars. Retry remains in boss-rush mode.

Combat now has rectangular special-cast warnings, status badges above health bars, and critical-hit callouts. **Pause → Battle guide** explains the labels and supplies an encounter tip. The courage pickup system retains manual collection and has no countdown ring.

Stage 80's packs were rebuilt around known infantry and ranged threats, with a lighter commander escort instead of stacked healing and corpse-summoning pressure. Unit base stats and level growth were not increased. A normal-speed engine run cleared it using Militia, Spearman, Swordsman, Archer and Field Medic at suggested levels without equipment.

## Validation boundary

The mechanics review currently passes 724 checks, including all fourteen scripted boss-rush transitions and payment, progression persistence, real star-gain previews, collection claims, skin selection and first-sighting/status feedback. Desktop and phone interface checks cover native progression actions, boss-rush launch and the combat guide. These checks do not prove every squad can win the rush.

Normal-speed gameplay checks are authoritative for encounter balance. Exploratory accelerated runs showed timing differences and are not used as proof of balance. Stage 95 clears at normal speed with three stars in 206.8 seconds after a matching encounter adjustment. The no-equipment normal-speed rush cleared 13 commanders; the final commander clears in a focused normal-speed check using earned relics and level-3 wagon upgrades. A continuous equipped run has not been claimed. Wider squad and physical-device playtesting remain necessary.

```sh
godot --headless --path . scenes/tests/ContentRevampSmoke.tscn -- --save-suffix=content-review-expansion
godot --path . scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-expansion --expansion
godot --headless --path . scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-rush --bossrush-gameplay
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
