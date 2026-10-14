# Progression, combat feedback and boss rush

Implemented prototype · 11 October 2026

**More → Caravan → Progression** opens three pages over the current map:

- **Awakening:** choose an owned ally, train its token for 100 Gold and 2 Essence, and preview the next star's actual battle stats before spending tokens and Gold. Five stars share the same permanent bonus caps as equipment; they do not multiply a new growth layer.
- **Collection:** view and claim the 24 existing milestones across six categories. The core unit total is now 32, and utility collection includes all 30 spells plus 12 tactical cards. Retired legacy recruits are excluded from completion requirements so new saves can reach 100%. Claims persist and pay once.
- **Wagon looks:** inspect the eight existing skin assets and select an unlocked appearance. These are cosmetic. Royal Guard now requires a campaign victory, rather than merely reaching the final stage; Bone Collector uses the attainable current Codex collection.

**More → Activities → Boss rush** unlocks after the King's Road boss. It contains all 14 commanders in campaign order, with levels 2–20 and the same shallow enemy curve. The old 1.2×–5× health ladder is retired. Each round has one commander and two familiar escorts. Defeat the commander and authored escorts to advance; summoned bodies do not create another round requirement. Boss reinforcement paths use bounded escorts.

Between rounds the troops regroup, the wagon repairs fully, courage resets to 40, and mana returns to at least its starting amount. Boss rush costs no food. Cleared rounds award 75, 85, …, 205 Gold, banked when the run ends. Result submission pays once, records the best completed wave and completion time, and does not change campaign stars. Retry remains in boss-rush mode.

Combat now has rectangular special-cast warnings, status badges above health bars, and critical-hit callouts. First sightings show a brief counter card when hints are enabled. **Pause → Battle guide** explains the labels and supplies an encounter tip. The courage pickup system retains manual collection and has no countdown ring.

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
