# ROADMAP

## Goal

Build a game that captures the core feel of **Dead Ahead: Zombie Warfare** while using original code, original presentation, and a cleaner internal architecture.

Current target:

- campaign map -> mission select -> battle
- bus vs barricade presentation
- courage-based card deployment
- cooldown-driven deck play
- stage-based zombie waves
- persistent roster and upgrade progression
- shop-driven economy with gold and food as the main progression currencies

Longer-term expansion targets:

- endless roguelite mode built on top of the battle/meta systems
- multiplayer built only after the combat loop and state model are stable

## What We Have Already Done

The project already has a working vertical slice:

- Godot 4 + C# project bootstrapped and runnable
- title screen, campaign map, and battle scene flow
- save/load for progression state
- stage unlock progression
- prototype scrap/fuel rewards
- data-driven units, stages, and combat tuning
- real-time combat with melee and projectile units
- enemy wave spawning
- stage-specific terrain palettes and basic map routing

This means the project is no longer at "empty prototype" status. It has a combat sandbox, a progression shell, and content data that can be evolved instead of replaced.

## What Still Does Not Match The Target

Main gaps versus the intended DAZW-style experience:

- battle still uses prototype presentation instead of a strong bus/barricade fantasy
- deployment was roster-based rather than deck/card-based
- stages are still mostly tuning-driven rather than explicitly scripted
- no squad-building metagame beyond basic stage selection
- no unit upgrade tree or long-term roster progression
- no real shop/payment flow for buying units, unit upgrades, or bus/base upgrades
- current prototype currencies do not match the intended gold/food economy loop
- no mission objectives, stars, or encounter scripting
- no differentiated enemy abilities or special combat rules
- placeholder rendering is still carrying too much of the experience

## Milestones

### Milestone 1: DAZW-Style Battle Core

Objective: make the battle feel recognizably closer to the target game.

- present combat as **bus escort vs zombie barricade**
- use a **limited active deck**
- add **per-card cooldowns**
- support **stage wave scripting**
- tighten battlefield pacing around intentional pressure

### Milestone 2: Squad And Meta Layer

Objective: create pre-battle decision making and persistent player growth.

- squad/deck editing before deployment
- roster unlock rules
- unit leveling and upgrade costs
- in-game shop flow for purchasing new units
- base/bus upgrade track purchased through the same economy layer
- persistent team composition data
- better rewards and stage completion structure

### Milestone 3: Economy, Shop, And Map Costs

Objective: replace the prototype reward model with the long-term progression economy.

- replace scrap/fuel with `gold` and `food`
- use `gold` for unit purchases, unit levels, unit upgrades, and bus/base upgrades
- use `food` to start new stages and to explore new sections of the campaign map
- add shop/payment UI for buying units and confirming upgrade costs
- persist owned units, upgrade tiers, and base upgrade levels in save data
- tune stage rewards so campaign clears feed both upgrade growth and map expansion

### Milestone 4: Mission Structure

Objective: move from "sandbox stage" to "campaign mission".

- scripted enemy wave sets
- timed events and boss entries
- alternate mission goals
- star ratings or bonus objectives
- clearer map progression by district/route

### Milestone 5: Presentation Pass

Objective: replace prototype abstraction with readable game feedback.

- proper sprites/animations
- hit, death, projectile, and deploy effects
- stronger HUD and battle readability
- map and menu polish
- audio pass

### Milestone 6: Content Expansion And Balance

Objective: scale once the systems are trustworthy.

- more units and zombie archetypes
- more maps and districts
- stage modifiers and challenge content
- balance passes for courage economy, cooldowns, and wave pacing

### Milestone 7: Endless Roguelite Mode

Objective: add a replayable run-based mode that reuses the combat and progression systems without depending on the linear campaign.

- endless or floor-based stage progression
- draft choices, random modifiers, or route forks between battles
- temporary run upgrades layered on top of permanent roster progression
- escalating enemy wave generation and boss checkpoints
- run rewards that feed back into the main progression economy

### Milestone 8: Multiplayer

Objective: support networked play only after combat/state flow is stable enough to stop rewriting core rules.

- decide the actual mode first: co-op defense, async challenge, or head-to-head
- refactor combat authority and game state to be deterministic/network-safe
- replicate deployment, combat events, and mission results over the network
- build lobby/session flow and disconnect handling
- rebalance units/objectives around the chosen multiplayer mode

## Immediate Next Sprint

This sprint should stay narrow and practical:

1. add persistent deck selection
2. add deploy cooldowns to unit cards
3. shift battle language and visuals toward bus vs barricade
4. replace generic spawn randomness with wave-script support
5. keep content count small while the combat foundation is stabilizing

## Work Completed In This Sprint

- documented the target direction and milestone structure
- added persistent active-deck state
- added deck selection controls on the campaign map
- added per-unit deploy cooldown data
- added cooldown-based deploy gating in battle
- shifted battle framing from generic base-vs-hive toward bus-vs-barricade
- added stage-authored wave definitions and scripted enemy spawning
- added persistent stage star ratings and basic mission goals
- added persistent unit upgrades funded by the current prototype economy and applied in battle
- added roster unlock rules and expanded the player unit pool
- added a pre-battle loadout screen between map and battle
- split battle deck/spawn/objective logic out of `BattleController`
- added early enemy trait expansion with armored heavies and bloater death bursts
- replaced fixed star rules with stage-authored mission objectives
- added basic deploy/death/base-hit combat feedback effects
- expanded enemy behaviors with projectile spitters and splitter death-spawns
- surfaced stage threat intel in loadout and live wave previews in battle HUD
- added data-driven stage modifiers that affect hull values, courage gain, and swarm pressure
- added live in-battle objective tracking instead of end-screen-only star feedback
- started the presentation pass with differentiated unit silhouettes, hit flashes, and projectile trails
- improved battlefield readability with base damage states, impact shake, base health bars, and stronger deploy cards
- added floating combat text so damage events read clearly during melee, projectile, burst, and base hits
- upgraded campaign presentation with themed route maps, stronger stage-node states, route progress banners, and compact stage intel on the map itself
- upgraded the title screen from a placeholder launcher into a live convoy/progression briefing
- started the endless-mode scaffold with a dedicated prep screen, persisted run records, and a survival battle ruleset with generated escalating waves
- added the first roguelite hook to endless mode with temporary opening boon choices
- added checkpoint draft upgrades inside endless runs so survival now includes mid-run decisions
- added route-fork decisions on major endless checkpoints so later segments can trade pressure for payout
- added fork-specific segment events so chosen route branches now alter the actual enemy packs inside each endless segment
- added convoy support events tied to endless route forks so branch choices now affect both enemy pressure and player-side assistance
- added battlefield events tied to endless route forks so each branch now changes the live arena, not only the spawns and support layer
- added live endless segment directives tied to route forks so each checkpoint block now has an optional objective/reward layer on top of survival
- added route-specific endless contact events so each segment can spawn a battlefield-side rescue/cache/relay objective with its own reward path
- upgraded endless contact events from pure zone logic into visible relay, cache, and safehouse setpieces in battle
- upgraded endless contact events again into spawned battlefield actors with durability and contested-state feedback instead of controller-only props
- added enemy-side contact targeting so endless actors are now pressured by actual attacker behavior, not only passive zone decay
- added player-side contact support actions so allied units can escort, haul, or uplink contacts instead of only repairing them through passive presence
- extended the contact loop to ranged units so spitters, gunners, and snipers now affect contact actors through projectile interactions too
- surfaced endless contact telemetry in the HUD and checkpoint summaries so hull, support contribution, and enemy pressure are readable while balancing
- added route-specific contact failure penalties so missed relay/cache/safehouse events now hit real run resources and convoy health instead of only denying upside
- added route-specific contact success tradeoffs so securing relay/cache/safehouse events now shifts the rest of the segment instead of being pure upside
- added route-specific hostile response packs so active relay/cache/safehouse contacts now draw their own reinforcements during the segment
- added one-time midpoint convoy assists so each active relay/cache/safehouse contact now has a player-side swing event during the segment too
- added explicit endless boss checkpoints every 15 waves so survival now builds toward route-specific warlord surges with dedicated escort packs, HUD intel, and extra clear rewards
- replaced the prototype scrap/fuel layer with a persistent gold/food economy, including stage entry costs and exploration costs
- added owned-unit purchasing, persistent unit leveling, and bus/base upgrades powered by gold
- added a dedicated convoy shop screen so purchases, upgrades, deck edits, and route intel are no longer mixed into the map view
- expanded the bus upgrade track with a deploy-cooldown `Dispatch Console` upgrade and surfaced upgrade previews in the shop
- expanded the bus upgrade track again with an anti-jam `Signal Relay` upgrade that shortens jammer windows and softens their courage/cooldown disruption
- added a shop-side action board that recommends direct purchases and upgrades based on the selected stage threat mix
- added squad-role deck synergies so pairing cards by role now changes deployed unit stats and gives the loadout/shop layer a stronger deck-building decision
- added the first multiplayer slice as an async challenge mode with shareable codes, seeded encounters, personal-best score tracking, and a dedicated prep screen
- added persistent async challenge history so multiplayer prep now shows recent local attempts for the current code and recent queue activity
- added a rotating featured challenge queue plus pinned rematch boards so the async multiplayer screen now has recurring boards and saved rivalry codes instead of only ad-hoc code entry
- upgraded featured async challenge boards with deterministic locked-squad runs so some daily multiplayer boards now compare execution on the exact same 3-card convoy instead of only the same seed
- added async challenge score transparency so multiplayer prep now explains the formula up front and battle results show the full post-run point breakdown instead of only the final score
- added async medal target tiers so challenge prep, featured boards, pinned rematches, and result screens now show deterministic bronze/silver/gold/ace score ladders for the same seeded board
- added async run tapes so local challenge history now stores deck context plus deployment timing/lane logs for the selected board instead of only score lines
- upgraded async run tapes again so saved local challenge history now preserves the underlying score split, not only the final score and deployment log
- added async challenge ghost benchmarks so multiplayer prep can arm the best saved local run for a board and battle can replay its deployment timing/lane drops as a live benchmark overlay
- upgraded async ghosts with live split feedback and result-screen deltas so the benchmark now teaches timing/route discipline instead of only replaying markers
- added a `Blackout Relay` async mutator so challenge boards can pulse scripted signal blackouts and test convoy timing under jammer-style pressure instead of only raw stat modifiers
- added a LAN race room flow with host/join, shared board sync, launch control, and room scoreboard submission on top of the async challenge system
- expanded the player roster again with a `Mechanic` support unit that can repair the bus and is surfaced through the shop recommendation board
- expanded the campaign with a third `Foundry Line` district, including four new scripted stages and endless-route support
- expanded the campaign again with a fourth `Quarantine Wall` district, including four new scripted stages, new checkpoint/decon/lab/blacksite battle palettes, and endless-route support for ranged-support/saboteur-heavy pressure
- added a late-game `Breacher` unit that specializes in higher barricade/base damage for reinforced stages
- added a `Saboteur` enemy archetype that dives the bus/objective path and is now seeded into Foundry stages and endless Foundry pressure
- added stage-authored battlefield hazards with telegraph rings, timed pulses, loadout intel, and Foundry-specific heat/rail/furnace encounters
- added a new hazard-hit mission objective type so Foundry stars can reward actually navigating battlefield hazards cleanly
- added a `Howler` support enemy with a live ally buff aura, plus Harbor/Foundry/endless seeding so late-game waves now mix in support-priority targets instead of only raw frontline pressure
- added a `Jammer` support enemy that can suppress courage gain and spike card recovery, plus late Quarantine/endless seeding so enemy pressure now attacks convoy tempo as well as hull
- added a `signal_jam_limit` mission objective type and seeded it into jammer-heavy Quarantine stages so support disruption now matters to star play too
- upgraded shared encounter intel so map/loadout/multiplayer briefings now call out support pressure like howlers, jammers, saboteurs, and spitters instead of only raw threat totals
- carried those support-pressure tags into campaign node tooltips and live next-wave battle intel so the player keeps the same read after deployment
- added live battle support-pressure telemetry so active howlers, jammers, saboteurs, spitters, bosses, and signal-jam windows stay visible while the fight is running
- upgraded the `Overlord` boss from a large stat block into a real rally-command encounter that buffs nearby infected and spawns escort bodies during boss stages
- expanded the player roster again with a `Grenadier` splash-damage card and the shared AoE attack support needed to counter clustered Harbor/Foundry waves
- expanded the player roster again with a late-game `Coordinator` support card that uses a live ally aura to boost nearby convoy units against support-heavy Quarantine and late-route waves
- started the audio pass with a shared procedural audio layer for UI interactions, scene ambience pulses, deploys, impacts, hazard warnings, base hits, and win/loss stingers
- added persistent audio settings on the main menu so SFX level, ambience level, and mute state survive saves instead of resetting every boot
- moved audio/interface controls into a shared settings screen so campaign, shop, loadout, endless, and multiplayer prep all expose the same persistent options flow

## Recommended Build Order After This Commit

1. replace the prototype scrap/fuel layer with the planned gold/food economy and shop flow
2. add unit purchasing, unit leveling, and bus/base upgrades on top of that economy
3. connect food costs to stage entry and map exploration so route expansion has real pressure
4. keep balancing courage economy, unlock pacing, and objective targets against the new currencies
5. replace placeholder combat visuals with authored assets and richer effects
6. expand campaign breadth with more maps and districts once the core loop is solid
7. add audio and stronger menu/map presentation
8. deepen the endless scaffold with run upgrades, modifier drafts, and route forks instead of only raw survival scaling
9. start multiplayer only after combat authority/state sync requirements are clear

## Guardrails

- replicate gameplay structure, not copyrighted names or assets
- keep data-driven content wherever possible
- avoid large rewrites until wave scripting lands
- validate each milestone with a playable loop before expanding content
- treat multiplayer as a later architecture project, not a bolt-on UI feature
