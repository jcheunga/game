# Crownroad content implementation

Implemented prototype · Updated 11 October 2026

## Playable scope

- **32 core recruits:** 20 foot melee, 2 mounted melee, 3 ranged, 5 support, and 2 siege. Peasant starts a new save; the opening contracts provide Militia, Spearman, Archer and Swordsman. Infantry branches include axe, mace, hooked polearm, pike, halberd, poleaxe, two-handed sword, armored professional and knight roles.
- **Five retained legacy recruits:** existing War Hound, Alchemist, Necromancer, Lantern Guard and Stormcaller ownership IDs remain usable. Existing ownership and levels survive loading. They are outside new-save recruitment.
- **59 ordinary hostile types, 14 bosses and one optional encounter:** ten zone bosses plus four mid-zone commanders explain the boss count. Ash Pilgrim is a noncombatant escort in stages 17 and 67, with a small manually collected reward for reaching safety.
- **100 equipment relics:** permanent stat packages plus bounded conditional effects. Equipment relics are wearable items. The twelve tactical tools are battlefield consumable cards, which occupy the same five loadout slots as spells.
- **30 spells and 12 tactical tools:** shields, interrupts, displacement, roots, cleansing, armor reduction, delayed attacks, corpse denial, regeneration, reveal, reflection, banishment and silence; palisades, traps, healing, turret, supply crate, decoy, smoke, censer, repair, grounding rod and war horn.

The previous 50-unit draft remains a reserve pool. It is not the current recruit count. Existing art serves as placeholder models, figures and item pictures for new content.

## Stats and progression

Units level from 1 to 20. Each level adds 1.5% of base health and 1% of base ordinary/structure damage: level 20 gives +28.5% health and +19% damage. Armor, magical resistance, critical chance, critical damage, speed, reach and timing stay fixed with unit level.

Physical resistance comes from `100 × armor / (100 + armor)` plus direct resistance points, capped at 75%. Magical resistance is separate, also capped at 75%. Critical damage is displayed as bonus damage: +50% produces a 150% hit. Physical, magical and scripted true damage have explicit paths.

All permanent sources join one capped budget, including relics, enchantments, promotion, mastery, awakening, guild/account bonuses and passive squad synergies. Combined caps are +10% health/damage/speed/reach/structure damage, +10 armor, +0.5 physical resistance points, +8 magic resistance points, +5 critical chance points, +10 critical damage points, 5% attack interval reduction and 10% deployment cooldown reduction. Conditional skills have separate cooldowns, finite charges, target limits or other restrictions.

Spells retain levels 1–3. Flat damage, healing and durability gain 5% per upgrade. Ratios, crowd-control duration, displacement distance, charges and resource income keep their authored values.

Militia and Sapper Crew deploy two bodies for one payment. Summons and tactical objects have limited lifetimes and cannot generate ordinary kill mana or courage drops. Revival sources share one return allowance per body. Control has recovery protection, with shorter boss disables.

## Campaign and enemy discoveries

All 100 normal stages have rebuilt waves and explicit enemy levels from 1–2 in the first zone to 19–20 in the final zone. Normal health/damage stage multipliers are 1; difficulty comes from encounter composition, timing and commander mechanics. Stage geometry and mission sites remain authored per route.

The opening palette teaches Risen, Ghoul, Brute and Blight Spitter. Each zone then introduces one new regional ordinary type at its fourth stage:

| Stage | Regional debut |
| --- | --- |
| 4 | Rustguard |
| 14 | Drowned Harpooner |
| 24 | Cinder Spitter |
| 34 | Miasma Priest |
| 44 | Cliff Stalker |
| 54 | Bone Cantor |
| 64 | Swamp Maw |
| 74 | Bone Lancer |
| 84 | Shade |
| 94 | Null Magister |

Later stages reuse familiar threats. Other ordinary types are available in the regional endless pools, where expanded types begin appearing from wave six. The main campaign avoids introducing the entire enemy collection at once.

Eligible enemy defeats retain the manual courage pack system: 10% chance, +20 courage, eight-second expiry, fading in the last two seconds, and a guarantee after fifteen eligible misses. Click or tap to collect; there is no countdown ring. Only enemies drop packs: Quartermaster, Supply Crate, objective and relic courage goes straight to the meter, with its own smaller values and limits.

## Current documents and rebuilding

[The readable playable catalog](catalogs/CURRENT_CONTENT_STATS.md) lists every unit/enemy, equipment relic and utility card. [The structured playable catalog](design/playable-content-catalog.json) contains all twenty bare stat records for every unit and enemy, plus equipment packages and utility values.

Run `python3 scripts/tools/revamp_content.py` to rebuild playable units, equipment, spells, stages, Codex entries and these stat exports. It consumes the reviewed melee core, progression policy and expansion reference. The progression policy's enemy bases preserve the original authoring budgets; the exporter applies the encounter tuning. Use the generated playable catalog for current enemy numbers. Earlier Word catalogs remain historical snapshots.

## Equipment choices — implemented

The armory now shows armor, resistance, critical, reach, recovery and structure bonuses alongside health and damage. The relic detail card lists every bonus, even for an unowned relic, so players can plan a build before acquiring it.

Equipping happens on the relics page itself (no modal): choosing an ally shows a before/after comparison using the same calculations as battle. It includes the item being replaced, its enchantment, both relic slots, promotion, squad bonuses and other permanent sources. Nothing equips until the player uses the equip button. Removing an item also has a comparison before confirmation.

Promoted allies can choose slot 1 or slot 2. Unpromoted allies see the second slot as locked. A relic can occupy only one slot across the warband; both equip paths enforce that rule. Removing it preserves ownership and its enchantment.

The comparison shows supplied and applied bonuses when the shared limits discard excess. For example, Guardian Shield with Thorns plus Tower Charm supplies 14.6 armor points, but the permanent layer applies 10. A wearer already receiving 8.6 points gains only another 1.4. The displayed physical resistance derives from the applied armor.

## Next content priorities

- **Forge enchantments — implemented:** all ten effects, Gold/Essence prices, actual wearer stat comparisons, shared caps, replacement and free removal are now accessible. See [Forge enchantments](ENCHANTMENTS.md).
- **Combat feedback and enemy teaching — implemented:** rectangular cast warnings, status badges, critical callouts, first-sighting counter cards and Pause → Battle guide.
- **Boss rush — implemented:** all 14 commanders, shallow levels 2–20, bounded reinforcements, between-round regrouping and persistent results. The no-equipment normal-speed squad cleared 13 commanders; the final commander clears with earned relics and level-3 defensive wagon upgrades. The fourteen transitions and results also pass the mechanics review.
- **Campaign balance — first pass:** stage 80 was retuned through encounter composition, and an early-unit squad cleared it at normal speed. Stage 95 now also clears at normal speed with three stars after its composition adjustment. Accelerated runs are exploratory only.
- **Progression interfaces — implemented:** More → Caravan → Progression provides capped awakening previews and token training, 24 collection claims and eight cosmetic wagon looks. See [the progression and boss-rush notes](PROGRESSION_AND_BOSS_RUSH.md).

## Validation and practical limits

The game builds without warnings or errors. Repository verification passed **11,343 data checks and 86 backend tests**, and built the website. The content review passed **724 checks**, covering all registered unit/enemy content actions, the twenty-level stat export, permanent caps, damage types, critical hits, cast interruption, one-time return, paired deployment, utility dispatch, finite tactical pulses, resource eligibility, saved recruitment, retained legacy deck access, equipment comparisons and all ten Forge enchantments. The separate courage review passed **55 checks** for desktop mouse and phone touch collection, expiry, pause, seed reproducibility and drop farming exclusions. The progression reward review passed **31 checks** for claims, save round trips and promotion.

The equipment review passed **29 UI checks** across desktop and phone layouts: native ally/slot selection, gain/loss previews, cap explanations, deliberate equip/removal, locked slots, unowned previews, persistent action buttons and preservation of map position and travel supplies. The stat/save checks confirm that the preview matches the equipped battle stats, includes enchantments and prevents cross-slot duplicates. These are engine UI checks; physical device playtesting remains part of the wider presentation pass.

The Forge enchantment review passed **37 UI checks** across desktop and phone layouts, covering all ten choices, visible costs, actual second-slot wearer stats, application, replacement, free removal, save reload, affordability, stored relics and empty collections. The equipment review also passed again after sharing its stat comparison with the Forge.

Nine selected campaign benchmark encounters—stages 1, 3, 4, 10, 14, 30, 54, 94 and 100—won at suggested levels without equipment. A squad using Militia, Spearman, Swordsman, Archer and Field Medic also won stages 94 and 100; it originally lost stage 80. The current normal-speed run clears the retuned encounter with three stars in 186 seconds, without equipment. These are scripted engine benchmarks, not a claim that all compositions or all 100 encounters have been manually balanced.

The armory review passed **92 checks**, covering every recruit and utility card, resolved battle costs/stats, upgrade interactions and inspector text layout. New pictures remain shared placeholders. Enemy and relic mechanics are initial playable implementations; further encounter, effect-feedback and device playtesting should refine them. Wider squad balance and physical-device testing remain follow-up work. Save ownership and level preservation are checked, but a large content rebalance changes the strength of existing builds.

Reproduce the checks with an isolated save suffix:

```sh
./scripts/verify_all.sh
godot --headless --path . scenes/tests/ContentRevampSmoke.tscn -- --save-suffix=content-review-local
godot --headless --path . scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-local-courage --courage-drops
godot --headless --path . scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-local-rewards --progression-rewards
godot --path . scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-local-content --armory-details
godot --path . scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-local-equipment --equipment-preview
godot --path . scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-local-enchantments --enchantments
```

The expansion interface review passed **21 checks** across desktop and phone layouts for awakening, collection claims, skin selection, boss-rush launch and the battle guide.

Normal-speed final checks: stage 95 clears with three stars in 206.8 seconds. The no-equipment boss-rush run cleared 13 of 14 commanders in 836.7 seconds; a focused final-round check with milestone relics and level-3 wagon upgrades cleared the last commander in 113.9 seconds with 544 hull remaining. A continuous fully equipped run has not been claimed.

## Production polish — 12 October 2026

The subsequent non-art pass is recorded in [PRODUCTION_POLISH.md](PRODUCTION_POLISH.md). It adds account-safe defaults, visible save recovery, repeated-input protection, offline background pause, full-screen battle settings, valid target previews and denial messages, paced teaching and enemy briefings, focused first-sighting waves, reduced status-drawing allocations, boss-rush retreat payouts and release exclusions. Unit/equipment stat budgets remain unchanged.

Earlier benchmark observations above describe the 11 October snapshot. The polish pass corrects the Archer's close-range special behavior and records fresh introduction/final-zone samples. The updated benchmark prepares available spells for custom squads and can explicitly click visible courage packs; this flag exists only in the review driver. See the polish report for current results and limitations.
