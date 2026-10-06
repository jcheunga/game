# Campaign progression and purchase economy

Implementation follow-up: [Reward and challenge improvements, 25 September](FUN_AND_PROGRESSION_UPDATE.md). That pass adds milestone rewards, trained recruits, mastery rewards, late-unit development paths, and targeted encounter changes. The measurements and proposed economy below remain the historical 11 September analysis.

Analytical development pass — 11 September 2026. Current combat observations and a **proposed**, unapplied economy are separated below. The proposed economy is a starting point for further combat tuning, not a release-ready balance claim.

The campaign should ask players to build a dependable three-unit squad, acquire counters, and gradually specialize. An underleveled squad should clear teaching and recovery missions; bosses should reward a visible preparation goal. A new recruit should solve a different problem, while leveling and equipment make the player's preferred troops remain useful. Purchases can accelerate that development and widen the roster. Normal campaign completion should have a practical earned route.

## What the current game actually does

| Finding | Evidence | Design consequence |
|---|---|---|
| Basic progression finishes much earlier than the campaign economy | The starter trio costs **1,248 gold** to raise from level 1 to 5. Starting gold plus the first eight base rewards is **1,380**. There is no campaign gate on those level purchases. | A focused player can exhaust the main leveling goal very early. |
| Gold income outruns ordinary sinks | Sixty authored stage rewards total **299,300** gold. Recruiting and fully leveling all 19 roster units costs 15,374; all spells 5,625; all 13 wagon upgrades 16,925: **37,924 total**. | New recruits and ordinary upgrades become financially trivial long before the finale. Promotions, awakening, crafting and other advanced systems are additional sinks, so this is not a claim that every progression system costs only 37,924. |
| Gold packs change meaning radically | The configured $19.99 pack contains 18,000 gold; stage 60 alone gives 22,500 before bonuses. The $0.99 500-gold pack buys roughly eight early unit-level purchases but less than one fortieth of a late victory. | Price the value of a useful development goal after stabilizing rewards. These are repository offer settings, not verified live storefront prices. |
| Recruitment is crowded early and absent in the middle | Sixteen units including starters are available by stage 13; the next recruit arrives at 48, then 51 and 57. | Stages 14–47 need recurring recruitment contracts, equipment choices and specialization goals. |
| Late recruits have less progression depth | Lantern Guard, Ballista Crew and Stormcaller have no promotion entries or unit skill trees, unlike the earlier 16 troops. | These units can be effective counters, but currently offer less long-term development. Complete those paths before marketing their advancement. |
| Relic rarity is not a stage ladder | Every boss roll uses the entire selected rarity pool. Base odds are 60% common, 30% rare, 10% epic. Epic entries include raid/tower-labelled relics. | A first boss can drop an endgame-looking reward. Authored names and provenance do not enforce a source restriction. |
| Food funds victories generously | One successful entry per stage yields **+715 net food**, before exploration and other rewards. | Food is primarily pressure on repeated failed attempts, not a sustained goal for a successful campaign. Do not assume food sales will fund the game. |
| Adventure exploration provides another sizable source | Current one-time map caches total **4,490 gold and 95 food**, plus up to 9 starting courage per district from shrines. | A campaign-only ledger is deliberately conservative, and cannot by itself justify production reward values. |

Unit-level health rises by 12% of base per level and damage by 10%. Attack cooldown is reduced by **0.03 seconds** per level, not 3%. Level 5 gives 48% more health and approximately 49–72% more basic DPS across the roster, before other bonuses. The exported per-unit table contains the exact runtime ratios. This is not a universal combat-power score: armor, courage cost, splash, lane reach, special abilities and survivability matter.

Basic levels are bought with gold. Mastery is a separate earned-XP system; deployments, kills and victories award XP. Promotions require level 5, gold and sigils. Skill trees consume gold and tomes; tomes mostly come from other activities rather than the ordinary boss reward. Equipment, doctrine, guild perks, enchantment and awakening add further power. A normal-stage requirement must not silently assume every layer is active.

### Map order is a separate design issue

Ordinary adventure encounters are accessible independently. A district's last numeric stage is gated behind that district's other five leaders. For example, King's Road contains stages 1, 2, 3, 4, 47 and 57. It is not a six-step early chapter. Stage 57 is the map's final leader while stage 4 contains its early combat boss. Six map-final encounters—51, 53, 54, 55, 57 and 59—do not contain an authored boss-class wave entry.

Consequently, numeric stage order is an **analysis itinerary**, not an enforced player route. Unlocking recruits still uses the highest unlocked stage; an out-of-order victory can jump that track. Mere exploration does not unlock those recruits. The map needs a separate encounter tier, clear recommended builds, and a suggested next affordable challenge. Keep exploration open, but avoid implying that a nearby endgame leader is the next introductory mission. First-clear equipment and contracts must attach to stable stage IDs or explicit milestone records, not simply “any boss” or “highest stage reached.”

## Measured progression, not assumed requirements

The main comparison ran **258 real combat attempts**: all 60 stages with three builds, then two additional encounter seeds on 13 boundaries. All used the starter Swordsman / Archer / Shield Knight trio and the same automated tactical controller.

| Build | Unit progression | Wagon and spells | Initial equipment | Initial full sweep |
|---|---|---|---|---|
| Core | L1 at 1–3, L2 at 4–7, L3 at 8–15, L4 at 16–27, L5 at 28–60 | Five support wagon upgrades reach L1 at 9, L2 at 17, L3 at 25; spells L2 at 16 and L3 at 31 | None | 54/60 clears |
| Lean | One unit level below core, minimum L1 | Same as core | None | 48/60 clears |
| Equipped lean | Same levels as lean | Same as core | Battle Drum on Swordsman, Sharpened Edge on Archer, Iron Pendant on Shield Knight | 51/60 clears |

The additional seeds change the interpretation of some apparent successes:

- **Stage 12:** core cleared 1/3; both weaker builds 0/3. Treat the Iron Warden as a preparation problem requiring a targeted weapon/counter test. Merely labelling L3 “recommended” is insufficient.
- **Stage 16:** core L4 cleared 3/3; lean L3 0/3; equipped L3 2/3. This is a useful level-versus-item threshold.
- **Stage 21:** core L4 cleared 3/3; lean L3 1/3; equipped L3 2/3. Preparation increases reliability while a weaker clear remains possible.
- **Stage 25:** core L4 cleared 2/3; lean L3 1/3; equipped L3 3/3. Basic equipment can be a meaningful alternative to another level here.
- **Stage 26:** core L4 cleared 3/3; lean L3 1/3; equipped L3 2/3. This is another useful preparation checkpoint.
- **Stage 29:** core L5 cleared 2/3; lean L4 0/3; equipped L4 2/3. Teach the safe lane before presenting health purchases as the answer to cursed ground.
- **Stages 52, 58 and 60:** none of these three builds cleared any of the three seeds. These need a specialist/weapon plan, not an instruction to buy one more basic level.
- **Stage 55:** core cleared 3/3, lean 2/3, equipped lean 0/3. More stats do not produce a perfectly ordered result under this controller: kill timing, enemy arrivals, courage use and deployment choices interact. Investigate the encounter/controller before claiming an item is harmful or mandatory.

The additional weapon and specialist runs are in the matrix's **Targeted purchases** table. They distinguish the value of wagon investment from recruitment. They purchase a complete armament package, so they do not identify the cheapest sufficient individual upgrade.

Those **45 additional attempts** bring this pass to **303 battles**. On stage 58, armed starters cleared 2/3 in roughly 191–201 seconds, with one timeout; Lantern Guard / Ballista Crew / Stormcaller cleared 3/3 in 108–173 seconds. Their current total purchase-and-upgrade investment is 10,568 gold, including the weapon package. On stage 60, armed starters cleared 0/3 while Lantern Guard / Stormcaller / Battle Monk cleared 3/3 in 122–128 seconds, at a total investment of 10,180 gold. This is evidence that recruiting a counter can be a meaningful goal even after ordinary levels are capped.

Stage 12 remains unstable even with the full armament package (2/3); stage 52 has no clear in the new automated profiles. The earlier direct stage-52 finish demonstrates possibility, not adequate reliability. Those are priority tuning targets before accepting the proposed progression ladder.

These are observed clears, not population win rates or proofs of minimum equipment. Battle-controller seeds do not control every possible engine/system randomness source. The controller uses Fireball, Heal and convoy commands; it does not use earned assault/defensive field orders, and it is not a flawless lane strategist. Each test starts from an isolated snapshot: mastery earned inside the battle still applies, while accumulated campaign mastery and exploration bonuses are absent. A timeout at 210 seconds is not a win. Prior direct boss finishes remain documented in `DIRECT_PLAYTEST.md`.

## Proposed development ladder

These targets are hypotheses to tune against, not newly enforced locks. Do not change existing unit access or remove owned progress to impose this schedule.

| Encounter tier | Main squad target | Item / collection goal | Preparation lesson |
|---|---|---|---|
| 1–4 | L1, first L2 purchase before 4 | First common item after stage 4 | Match lanes, protect ranged troops, preserve courage for the boss warning. |
| 5–8 | L2 | Second common choice after 8; choose a cheap alternate recruit | Area damage and interception; a weaker squad should still finish these introductions. |
| 9–15 | L3 | Third common choice after 12; introduce a free doctrine choice | Develop Archer Crew, then Mounted Ballista before 12; Firepot before 15. Test stage 12 for reliable clears. |
| 16–26 | L4 target; L3 plus useful equipment is a challenge route | Rare choice after 16 and 26; recruitment contracts at 18 and 23 | Item-versus-level decisions, anti-jam preparation, support removal. Upgrade five support wagon systems to L2 by 21. |
| 27–36 | L4 | Third rare choice after 31; contracts at 28 and 33 | Build a bench and swap a role to answer attrition, dives and armor. Avoid making all bench units equal-level requirements. |
| 37–46 | L5 on the core, L3–4 on the bench | First promotion around 40; epic choice after 41; contracts at 38 and 43 | Specialize the main squad; upgrades should improve reliability and optional objectives rather than erase enemy mechanics. |
| 47–56 | L5 core; specialist trial and catch-up path | Second promotion around 48; more targeted rare/epic gear | Sustained ranged damage, siege pressure and recovery; stages 52/56 are explicit loadout checks requiring further validation. |
| 57–60 | L5 chosen counters; developed weapon mounts | Third promotion around 56 on eligible veterans; final choice reward | Combine previously taught mechanics. Promotions cannot be assumed on the three late units until their paths exist. |

Stages 1–8 introduce systems one at a time. Thereafter, make each group alternate introduction → practice → mixed test → reward/recovery, with roughly one preparation checkpoint every four to six encounters. Do not achieve that rhythm solely by raising enemy health. Use pressure timing, enemy role combinations and safe counterplay, and budget each new mechanic before combining it with another.

Each stage row in `progression/STAGE_MATRIX.md` has a level target, measured builds, gross pre-entry income and a proposed purchase/tactic priority. `progression/audit.json` additionally records the exact authored enemy composition, boss identity, modifiers, authored-wave HP total, runtime prices, wagon targets and purchase ledger. Authored HP totals exclude summons, extra events, defenses and modifiers beyond the base stage HP scale; they describe content volume, not difficulty.

### Recruitment without invalidating favorites

Keep early recruits available as alternative strategies. Use earned contracts at 18/23/28/33/38/43 to spotlight an unowned counter or help train an already owned one. A contract should offer a relevant choice, not force an arbitrary new unit into the deck. Earned catch-up training should bring a new mid/late recruit to within one level of the core.

For the late roster, prototype Lantern Guard access/trial at 43, Ballista Crew at 47 and Stormcaller at 53, ahead of their major tests. Those are proposed access changes only. Add promotion and skill-tree entries at the same time. Prove that at least one veteran alternative remains viable; a particular recruit should not become the purchase receipt required to continue.

## A quantified economy candidate

The candidate preserves recruit, spell, wagon and promotion gold costs, but changes the **unit-level cost multipliers to 1× / 2× / 3× / 5×** for the four upgrades. The starter trio then costs **4,017 gold** to reach L5 instead of 1,248. This makes later levels a larger decision without inflating combat stats.

Candidate first-clear base gold is **90 + 12 × (stage − 1), plus 60 on encounters containing an authored combat boss**. Replays pay 35% of that amount, rounded down. This gives **27,540 gold** over the 60-stage itinerary, versus the current 299,300. It is an economy normalization prototype, not a suggestion to silently reduce existing players' rewards by that amount. Current money packs must be repriced/repackaged against the finalized curve before any launch or migration.

The model buys upgrades **before** entering their target stage, using only already earned money. It does not use the reward of the fight being prepared for.

| Candidate route | Actual modeled purchases | Total gold investment | Required resource-farming replays |
|---|---|---|---|
| Focused core | Three starters to L5, two spells to L3, eleven wagon systems to L3 at staggered milestones, three starter promotions | 15,237 | 0 |
| Flexible roster | Focused core plus Mage / Monk / Alchemist / Halberdier; bench L3 initially and L4 from 45 | 19,721 | 0 |
| Broad equal-level collection | Same seven units, all raised to the core level immediately | 22,991 | 27 |

The third route exposes a **timing** problem: simultaneously taking seven units to L5 at stage 37 creates a 3,867-gold shortfall, despite enough lifetime income later. This is a reason to stagger bench development and offer catch-up training. It is not a target of 27 mandatory repeats for a normal player. Optional spending can make broader collection faster, while the focused and practical flexible paths are affordable without farming in this restricted ledger.

These are **financially affordable itineraries**, not complete demonstrated campaign clears using the candidate builds. Some late encounters require different squads than the starter-focused ledger. The paid and free acquisition paths for those counters still need to be included in the final build ledger and tested. Candidate training timing, new item choices, and new unlocks have not been applied to combat. No claim is made that an untested candidate loadout beats stage 52.

The model starts with 120 gold and excludes optional income, failed attempts, login calendars, bounties, guilds, tower rewards and discounts. All three promotions have sufficient authored boss-kill sigils on the numeric itinerary: costs are 3 + 4 + 3, against 15 authored boss kills over the full campaign. Out-of-order play and lost fights must be tested separately. Food and actual playtime are not converted into revenue or retention forecasts.

## Items must lead to the next item

Implemented in this pass: a duplicate **boss** relic now gives the corresponding dismantle value—**1 common / 3 rare / 9 epic shards**—while preserving the equipped original. Other idempotent grants retain their existing semantics. The battle displays the shard reward. The Relic Repository description now states its actual rarity effect: +4.8 percentage points epic and +2.4 rare per level, rather than promising a nonexistent 12% extra drop chance.

Next design step: guarantee first-clear choice rewards after 4/8/12 (common), 16/26/31 (rare), and 41/46/56/60 (epic or an equivalent targeted crafting reward). These must be persisted once per milestone, work with out-of-order clears, and turn an already-owned choice into a known useful alternative. Keep optional boss rolls as bonus collection progress. Separate campaign, raid, tower and event drop pools so reward identities retain meaning.

At current forge costs, a rare needs 15 shards plus 600 gold; an epic needs 40 plus 1,500. At base rarity odds, a fully collected common/rare/epic pool now yields an expected **2.4 shards per boss kill** (0.6×1 + 0.3×3 + 0.1×9). That is a long-run rate, not a guaranteed number of kills to a craft. During collection, first copies enter inventory instead of producing shards. Guaranteed choices provide the progression floor that random drops currently lack.

## Purchases as a visible goal

Working assumption: sell earnable advancement and collection convenience; normal campaign power remains available through play. No cash prices, receipts or live storefront products were changed.

| Offer concept | Concrete value | Earned counterpart | When it belongs |
|---|---|---|---|
| First specialization bundle | Choice of an early alternate recruit, enough training to join the squad, a named common relic | Recruitment contract, stage gold and first-clear relic choices | After the first boss, when the player understands a role they want to try |
| Counter-squad training | Train one owned reserve unit to the chapter's catch-up level; fixed ingredients shown | Gold plus earned training contract | In the roster or preparation screen, tied to a player-selected build goal |
| Relic crafting bundle | A stated shard amount and crafting gold toward a selected item | Duplicate boss drops, dismantling and targeted rewards | Beside the forge recipe, with the remaining requirement shown |
| Collection/pass progression | A disclosed sequence of items, resources and cosmetics | Free progression track with enough campaign power | Between chapters, after players have demonstrated interest in collecting |

Gold packs should show their effect on the player's selected goal, not only a large coin count. The same amount can be excessive in an early economy and irrelevant in a late one. The candidate reduces that distortion but does not establish the right real-money price. Fixed-content offers are easier to evaluate than selling uncertain progression. Avoid making basic retries the primary purchase pitch; the player should be buying a build they want.

Measure purchases alongside progression: stage attempts and clears by squad/level/item, pre-attempt resources, upgrades made after losses, next-attempt outcome, time to first useful item, idle currency, bench use, and paid versus earned item acquisition. Track conversion, repeat purchases and retention together. Do not assume that harder blockers improve revenue; test whether a purchase creates lasting build variety and satisfaction. These are instrumentation and experiment requirements, not forecasts.

## What is ready and what still needs tuning

Ready: a reproducible 60-stage matrix, runtime price/power export, three-build comparison with repeated boundary seeds, targeted weapon/counter checks, four financial itineraries, and the duplicate-relic progression fix.

Before enabling the proposed economy: validate exact prepared builds on the unstable checkpoints, reduce the stage-37 collection purchase spike, test out-of-order adventures and failure/retry budgets, include all optional income, and enforce source-specific/guaranteed item rewards. Establish stage-appropriate free acquisition for every recommended specialist. Then test each checkpoint with a target build, a deliberately weaker build, and an invested build across more seeds and direct input. Do not require all advanced power layers for the first campaign clear.

Reproduce with `bash scripts/smoke/progression_review.sh`; regenerate only the reports with `python3 scripts/analysis/progression_audit.py`. Detailed samples and input hashes are saved in `progression/audit.json`; raw logs remain under `artifacts/progression/`. Run `scripts/smoke/combat_review_smoke.sh` and `scripts/verify_all.sh` after changing production reward behavior.

Validation for this pass: build succeeds with zero warnings/errors; **97 combat regression checks, 6,398 data checks and 71 server tests pass**. The combat test process still emits its previously documented Godot ObjectDB cleanup warning at exit.
