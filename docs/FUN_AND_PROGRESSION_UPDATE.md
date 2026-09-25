# Reward and challenge improvements

25 September 2026. These changes are implemented in the local game. The broader currency/pricing proposal in PROGRESSION_DESIGN.md remains a separate, unapplied economy experiment.

## Rewarding progress

Eighteen campaign milestones now guarantee useful development rewards. They appear before battle on the map and in the victory report. A player can plan toward a named reward instead of depending entirely on a random boss drop.

| Clear | Guaranteed reward |
|---|---|
| 4 / 8 / 12 | Iron Pendant / Sharpened Edge / Battle Drum |
| 16 / 26 / 31 | War Brand / Guardian Shield / Sage's Ring |
| 18 / 23 / 28 / 33 | Level 3 Mage / Battle Monk / Alchemist / Halberdier, plus 1 / 2 / 2 / 2 tomes |
| 38 / 43 / 47 / 53 | Level 4 Banner Knight / Lantern Guard / Ballista Crew / Stormcaller, each with 3 tomes |
| 41 / 46 / 56 / 60 | Crown of Valor / Blade of Ruin / Frostbound Crown / Immortal Wreath |

Recruit contracts add the unit to the collection without replacing the active squad. Existing higher levels are preserved. Already-owned relics award their dismantle value in shards and keep the original equipped item. Rewards are once per campaign; prestige starts a new track. Existing saves can claim the newly introduced milestones once by replaying those stages. Out-of-order clears grant only the milestone actually completed.

Every stage also awards a one-time three-star mastery bonus: one relic shard on ordinary encounters or three on encounters with an authored combat boss. Improving a one- or two-star result later still earns the bonus. This gives stronger squads and better play an item-crafting goal after the first clear.

Campaign boss relic rolls exclude raid- and tower-specific items. Their dedicated activity rewards retain their identity. Ordinary campaign epics remain in the boss pool; this pass does not introduce rarity gates or remove owned items.

Lantern Guard, Ballista Crew and Stormcaller now each have a five-node skill tree and a level-5 promotion. Their promotion costs are respectively 2,200 gold / 5 sigils, 2,500 / 5, and 2,600 / 6. The new paths use the existing promotion and skill-tree interfaces. Their bonuses follow the scale of comparable veteran units, rather than making late recruits automatically replace the old roster.

## Challenge that rewards good play

Thirty-six authored kill-count objectives exceeded the number of enemies in their authored waves. A quick victory could therefore miss a star unless the player waited for summons or extra encounters. Those objectives now reward finishing with 60% hull; where a hull objective already exists, they instead reward a 150-second clear before stage 32 or 180 seconds thereafter. The existing hazard, deployment, mission and other objectives remain. Three-star play should reward execution, not deliberately extending a fight.

Iron Warden's final reinforcement wave contains one fewer Juggernaut and Brute, furnace warnings last 2.2 seconds, and hazards leave longer recovery intervals. His campaign summons and phase pressure now share a maximum of two living heavy escorts, including authored Brutes. He can replace casualties but cannot accumulate an unlimited escort wall. This new Warden limit applies to campaign encounters.

The Reliquary opening separates some overlapping pressure: one fewer Bone Nest, one fewer Blight Caster, an early Ghoul instead of a Hexer, and a Brute instead of the second Catacomb Giant. The late Hexer, siege engines, mirror enemy, hazards and boss remain. Repeated Ossuary Fire now briefly strengthens existing escorts; it no longer runs an additional summon-and-gate-repair cycle. The Tyrant's normal summon ability, two-artillery cap and telegraphed phase transition remain. Defeating artillery creates an opening rather than instantly undoing the player's progress.

## Observed outcomes

The following are automated real-engine observations, not human win-rate estimates. The controller uses Fireball, Heal and convoy commands, but not earned field orders or perfect hazard avoidance. Five encounter seeds were used on each focused build. Each run starts from an isolated save.

| Fight / build | Clears | Time / interpretation |
|---|---|---|
| Stage 12, level 2 starters, support wagon only | 2/5 | A weaker clear remains possible. |
| Stage 12, level 3 starters, support wagon only | 2/5 | Levels alone do not reliably solve the encounter. |
| Stage 12, level 3 starters with level 1 armament package | 5/5 | 101–164 seconds; current total build investment 1,654 gold. |
| Stage 52, level 5 Lantern Guard / Ballista Crew / Mage, level 3 wagon/spells, no initial gear | 3/5 | 119–237 seconds on clears; two runs reached the 270-second observation limit. |
| Same stage-52 build with three earlier milestone relics | 5/5 | 112–267 seconds; two clears still took over four minutes. |

The stage-52 relics were Crown of Valor on Lantern Guard (stage 41), Blade of Ruin on Ballista Crew (46), and War Brand on Mage (16). The recruits themselves have earned contracts before stage 52: Mage at 18, Guard at 43, Ballista at 47. The test uses level 5 upgrades and **no initial promotion, doctrine, skill-tree, enchantment or accumulated mastery bonuses**. In-battle mastery still operates. The full current snapshot costs 9,840 gold at normal listed prices including recruitment; actual contracts reduce recruit/training spending. This is an observed sufficient build, not proof of the cheapest possible clear.

The full 60-stage limited starter reference cleared 53 stages; failures were 12, 25, 52, 53, 56 and 60, with a stage-58 timeout. The prior reference cleared 54. This pass therefore does **not** claim a universal increase in starter-squad success: its intended progression is that preparation, equipment and role choices matter. The new reward track was tested separately from this deliberately gearless, freshly reset reference.

The Iron Warden package and the relic-equipped Reliquary build are reproducible with the CombatReviewSmoke runner. Use an isolated `--save-suffix=combat-review-<unique>` and seeds `0,1000,2000,3000,4000` via `--seed-offset`. Warden flags: `--tactical --armaments --stages=12`. Reliquary flags: `--tactical --armaments --milestone-relics --stages=52 --time-limit=270 --squad=player_lantern_guard,player_ballista,player_marksman`. The default observation limit remains 210 seconds; the longer limit is explicit in the Reliquary runs.

## Validation and limits

Build: zero warnings/errors. Checks: **121 combat regressions, 6,398 data validations, and 77 server tests pass**. Reward tests cover save/reload, serialized save round-trips, replay duplication, out-of-order claims, legacy saves, owned relic compensation, higher-level recruits, other-mode isolation, prestige reset, and actual promotion/skill purchases. The three map reward/preparation views pass engine UI checks in both 1280×720 and 1024×768 windows. Screenshots were visually inspected at both sizes; reward goals appear first and missing-entry-resource messages remain beside the battle button.

Raw results and screenshots: `artifacts/fun-pass-20260925/`. Historical inputs were saved there before editing. The existing test-exit ObjectDB warning remains in some engine runs.

The two long Reliquary clears remain a pacing limitation; additional field-order play or build investment may shorten them, but that is not established by these runs. This pass adds concrete progression and replay rewards without claiming that a subjective sense of fun can be proven by automated wins alone. Currency inflation and paid-pack value still need the separate end-to-end economy experiment, including optional income and existing-player migration.
