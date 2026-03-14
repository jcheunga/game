# ROADMAP

## Goal

Build a game that captures the core feel of **Dead Ahead: Zombie Warfare** while using original code, original presentation, and a cleaner internal architecture.

Theme pivot:

- keep the lane-battle, cooldown-card, stage-progression structure
- replace the current zombie/apocalypse presentation with an original **medieval fantasy** setting
- shift the battlefield fantasy toward **warbands, castles, siege pressure, and magic**

Current target:

- campaign map -> mission select -> battle
- medieval fantasy battlefield framing, likely **war wagon/caravan vs gatehouse/fortress**
- courage-based card deployment
- cooldown-driven deck play
- stage-based enemy waves with medieval/fantasy factions
- persistent roster and upgrade progression
- shop-driven economy with gold and food as the main progression currencies

Longer-term expansion targets:

- endless roguelite mode built on top of the battle/meta systems
- multiplayer built only after the combat loop and state model are stable
- internet/mobile backend-backed multiplayer after the LAN and async room flows are proven locally

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

- battle still uses prototype presentation instead of a strong war wagon/gatehouse fantasy
- the current content/theme is still too tied to zombie-modern framing and needs a full medieval fantasy conversion
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

### Milestone 0: Medieval Fantasy Theme Pivot

Objective: lock the new fiction and content direction before scaling more maps and units.

- replace zombie/modern naming, factions, and encounter language with original medieval fantasy equivalents
- decide the final battlefield fantasy, such as **war wagon vs gatehouse**, **caravan vs keep**, or another original siege framing
- define the first player kingdom/faction, first enemy factions, and magic tone
- rename/retheme currencies, missions, stage labels, and route presentation where needed
- create a short art/content bible so later roster, spell, and map work all point at the same setting

### Milestone 1: DAZW-Style Battle Core

Objective: make the battle feel recognizably closer to the target game.

- present combat as a readable **medieval siege lane battle**
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
- add a dedicated **spell/magic card layer** alongside troop cards
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
- fantasy-specific mission events such as ritual sites, gate breaches, relic escorts, cursed weather, or siege objectives

### Milestone 5: Presentation Pass

Objective: replace prototype abstraction with readable game feedback.

- proper medieval/fantasy sprites and animations
- hit, death, projectile, deploy, and spell effects
- stronger HUD and battle readability
- map and menu polish with stronger regional identity
- audio pass
- replace zombie/modern placeholder presentation with armor, banners, fortifications, beasts, undead, spell FX, and stronger faction silhouettes

### Milestone 6: Content Expansion And Balance

Objective: scale once the systems are trustworthy.

- launch roster target of **at least 10 unique player units**
- add a reusable **spell roster** with active magic/support cards
- campaign target of **at least 10 maps/districts**
- each map should contain **5 or more stages**, for a target of **50+ campaign stages**
- stage modifiers and challenge content
- balance passes for courage economy, cooldowns, and wave pacing
- each map should have a distinct biome/faction/hazard identity

Minimum player unit target for the first medieval roster:

- `Swordsman`
- `Spearman`
- `Shield Knight`
- `Archer`
- `Crossbowman`
- `Cavalry Rider`
- `Halberdier`
- `Battle Monk`
- `Mage`
- `Siege Engineer`

Minimum spell/magic target for the first medieval roster:

- `Fireball`
- `Heal`
- `Frost Burst`
- `Lightning Strike`
- `Barrier Ward`

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

### Milestone 9: Internet Multiplayer And Mobile Backend

Objective: extend the current async/LAN multiplayer systems into a store-ready internet stack for mobile release.

- keep async challenge boards as the first internet-safe multiplayer feature
- move room/session state onto a transport-agnostic model that can be reused by LAN and online rooms
- add backend-backed player identity, profile sync, and challenge result submission
- add online room discovery/matchmaking instead of local-IP-only hosting
- add backend-backed live room telemetry and race-monitor updates for in-progress internet challenge rooms
- choose relay or authoritative hosting for real-time room races instead of raw peer-to-peer assumptions
- support reconnects, mobile backgrounding, and stale-room recovery
- add basic anti-cheat, leaderboard validation, and moderation/reporting hooks
- keep LAN as the local proving ground for room flow before shipping the internet equivalent

## Immediate Next Sprint

This sprint should stay narrow and practical:

1. carry the first-pass fiction lock through the remaining menu, battle, and multiplayer copy
2. expand authored districts toward the 10-map / 50-stage campaign target
3. keep the existing combat/progression structure but retheme future content toward medieval factions, siege pressure, and magic
4. replace remaining legacy convoy/bus/zombie phrasing in active player-facing flows
5. only expand content after the renamed roster, route set, and stage plan are coherent

## Work Completed In This Sprint

- documented the target direction and milestone structure
- added persistent active-deck state
- added deck selection controls on the campaign map
- added per-unit deploy cooldown data
- added cooldown-based deploy gating in battle
- shifted battle framing from generic base-vs-hive toward war wagon-vs-gatehouse
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
- added stage-authored battlefield mission events, including ritual-site holds, relic escorts, and gate-breach charges with live progress, success swings, and failure penalties
- started the presentation pass with differentiated unit silhouettes, hit flashes, and projectile trails
- improved battlefield readability with base damage states, impact shake, base health bars, and stronger deploy cards
- upgraded the spell layer from shared placeholder pulses into distinct fireball, heal, frost, lightning, and ward battlefield signatures
- added floating combat text so damage events read clearly during melee, projectile, burst, and base hits
- upgraded battle debriefs so campaign and challenge runs now report route metrics, final objective outcomes, and battlefield-event resolution instead of only a binary result line
- upgraded endless run closure so defeats and voluntary retreats now open a full route debrief with banked payout, boon/path context, directive/contact reports, and reward-bank telemetry
- added persistent district-completion rewards with retroactive save reconciliation, so fully securing a campaign route now pays a one-time chapter bonus on top of stage clears
- added level-gated unit doctrines with persistent save data, armory selection, retraining costs, and real stat bonuses so unit growth now branches instead of stopping at flat levels
- added optional stage replay heroic directives with one-time bounty rewards, map/loadout/armory surfacing, and extra stage modifiers so campaign replays now carry a real challenge layer instead of only star cleanup
- upgraded campaign presentation with themed route maps, stronger stage-node states, route progress banners, and compact stage intel on the map itself
- extended route identity through the campaign map, loadout briefing, and caravan armory so each district now carries its own color treatment and battlefield-event briefing surface
- extended that route identity into live combat with route-themed HUD panels, in-battle briefing banners, and heraldic war wagon/gatehouse silhouettes
- upgraded the caravan armory action board so authored battlefield events can influence buy and upgrade recommendations before deployment
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
- added owned-unit purchasing, persistent unit leveling, and war wagon/base upgrades powered by gold
- added a dedicated convoy shop screen so purchases, upgrades, deck edits, and route intel are no longer mixed into the map view
- expanded the bus upgrade track with a deploy-cooldown `Dispatch Console` upgrade and surfaced upgrade previews in the shop
- expanded the bus upgrade track again with an anti-jam `Signal Relay` upgrade that shortens jammer windows and softens their courage/cooldown disruption
- added a shop-side action board that recommends direct purchases and upgrades based on the selected stage threat mix
- added squad-role deck synergies so pairing cards by role now changes deployed unit stats and gives the loadout/shop layer a stronger deck-building decision
- added the first multiplayer slice as an async challenge mode with shareable codes, seeded encounters, personal-best score tracking, and a dedicated prep screen
- added persistent async challenge history so multiplayer prep now shows recent local attempts for the current code and recent queue activity
- added a rotating featured challenge queue plus pinned rematch boards so the async multiplayer screen now has recurring boards and saved rivalry codes instead of only ad-hoc code entry
- added provider-backed internet room hosting, join tickets, room-session polling, room actions, result submission, scoreboards, and rematch reset so the async multiplayer screen now covers the full backend room lifecycle before real internet transport exists
- added provider-backed room leave flow plus service-side state cleanup, so joined or hosted internet rooms can be exited cleanly without relying on a replacement join ticket
- added provider-backed live online-room telemetry heartbeats plus a cached telemetry status block in multiplayer prep, so in-progress internet room races now have a race-monitor seam alongside results and standings
- added provider-backed joined-room moderation reports with selectable reasons, so the multiplayer screen now has a basic report/moderation hook for suspicious scores or abusive room behavior instead of leaving that entire store/backend concern for later
- added provider-backed player profile sync with a cached auth/session status seam in settings and multiplayer, so the internet stack now has a real identity handshake before deeper backend account work lands
- added provider-backed quick matchmaking for the selected async board, so the client can negotiate a seat directly from the backend instead of only browsing and joining room cards manually
- added joined-room auto refresh in multiplayer prep, so the internet room session and scoreboard can keep polling while the player waits in the lobby instead of relying only on manual refresh actions
- added ticket-expiry handling and seat renewal on internet rooms, so stale backend room tickets stop polling/actions/results cleanly and can be recovered through quick match instead of failing silently
- added provider-backed room-seat lease refresh plus auto-renew wiring, so healthy joined-room seats can be extended during long prep waits and mobile resume recovery instead of falling straight into stale-ticket failure
- added local board/deck arming from negotiated internet-room tickets, so room join, host adoption, and quick match now sync the selected challenge board and locked shared squad automatically instead of relying on a separate manual load step
- added joined-room race gating on the main deploy button, so once a board is armed from an internet-room ticket the multiplayer screen waits for backend launch/countdown and labels room-race entry clearly instead of acting like an unrelated solo challenge start
- routed internet-room battle end screens back through the room lobby instead of direct challenge retry, so submitted room results respect backend rematch/reset flow instead of bypassing it
- added an online-room battle start barrier tied to backend countdown state, so joined internet-room runs now hold simulation on scene load until the room launch sync actually expires
- added in-battle joined-room polling plus a compact online-room monitor in challenge intel, so live internet-room runs now keep showing peer race state instead of feeling disconnected once combat starts
- extended joined-room peer snapshots with structured race metrics and used them for an in-battle room-pace summary, so online races now expose relative standing and leader gap instead of only raw monitor text
- extended submitted peer snapshots with provisional score/rank, so room monitor and pace summaries can stay standings-aware after runners start posting clears instead of collapsing to plain \"submitted\" text
- merged refreshed room scoreboards back into the cached room session snapshot, so standings-aware room monitor consumers update immediately instead of waiting for an extra session poll round
- added a shared app-lifecycle service that pauses room traffic while backgrounded and refreshes profile/room state on resume, so the mobile multiplayer stack now has an explicit backgrounding/recovery path instead of depending on accidental polling behavior
- added a live online-room end-panel refresh loop, so submitted internet-room runs can keep showing room monitor state and shared standings from battle instead of freezing at a local-only result summary
- added HTTP stub smoke coverage for online room create, launch, reset, leave, result, scoreboard, and telemetry flows so the backend room contracts are exercised end to end instead of only compile-checked
- added HTTP stub smoke coverage for the player-profile contract as well, so backend identity/profile sync is exercised end to end instead of only being compile-checked
- added HTTP stub smoke coverage for the room-matchmake contract as well, so backend quick-join seat negotiation is exercised end to end instead of only being compile-checked
- added HTTP stub smoke coverage for the room-seat lease contract as well, so backend seat-refresh handoff is exercised end to end before mobile room sessions depend on it
- added HTTP stub smoke coverage for the room-report contract as well, so backend moderation/report intake is exercised end to end before internet room play depends on it
- upgraded featured async challenge boards with deterministic locked-squad runs so some daily multiplayer boards now compare execution on the exact same 3-card convoy instead of only the same seed
- added async challenge score transparency so multiplayer prep now explains the formula up front and battle results show the full post-run point breakdown instead of only the final score
- added async medal target tiers so challenge prep, featured boards, pinned rematches, and result screens now show deterministic bronze/silver/gold/ace score ladders for the same seeded board
- added async run tapes so local challenge history now stores deck context plus deployment timing/lane logs for the selected board instead of only score lines
- upgraded async run tapes again so saved local challenge history now preserves the underlying score split, not only the final score and deployment log
- added async challenge ghost benchmarks so multiplayer prep can arm the best saved local run for a board and battle can replay its deployment timing/lane drops as a live benchmark overlay
- upgraded async ghosts with live split feedback and result-screen deltas so the benchmark now teaches timing/route discipline instead of only replaying markers
- added a `Blackout Relay` async mutator so challenge boards can pulse scripted signal blackouts and test convoy timing under jammer-style pressure instead of only raw stat modifiers
- added a LAN race room flow with host/join, shared board sync, launch control, and room scoreboard submission on top of the async challenge system
- extended LAN race rooms with per-peer ready states so the host can verify the synced board is armed before launch
- added a dedicated LAN launch-readiness panel so the host can see the active runner pool, spectator pool, and named launch blockers without inferring them from the room text
- tightened LAN battle flow so challenge end screens route back through room-rematch paths instead of silently dropping into solo async retry behavior
- added persistent convoy call signs and wired them into LAN room labels, ready states, and room scoreboards so peers are identifiable across runs
- added live LAN peer race-state tracking so room summaries now show who is still in prep, who is currently racing, and who has already submitted a result
- added per-peer convoy deck syncing for player-deck LAN boards so the room summary now shows each runner's current squad and active synergy before launch
- tightened LAN board integrity so changing a convoy deck clears ready state and player-deck rooms require a full synced 3-card convoy on every peer before launch
- added live LAN race telemetry so room summaries now stream current time, hull, and defeat counts for peers that are already in battle before the final scoreboard arrives
- added a dedicated LAN race monitor panel so live room progress and completed submissions no longer compete for the same final-scoreboard space
- added a shared LAN battle-load barrier and countdown so room races no longer start simulation until every peer has finished loading into battle
- added disconnect preservation for LAN races so a dropped runner is recorded as `DC` instead of silently disappearing from the room monitor and final scoreboard
- updated LAN challenge end screens so they keep listening to room-state changes and refresh their room monitor / scoreboard while other runners finish
- added cumulative LAN session standings across rematches so the room menu and live LAN end screens now show an ongoing room leaderboard, not only the current-race result sheet
- added late-join spectator handling for LAN rooms so peers connecting during an active race are shown as spectators until the next reset/rematch instead of skewing active-race counts
- hardened LAN round locking so hosts cannot rehost/refresh/launch over an in-flight race, and room UI now flips into a clearer rematch-ready state once the round is complete
- tightened spectator rematch eligibility so late joiners stay out of the next launch until they explicitly ready, instead of being auto-pulled into the following race
- fixed rematch phase recovery so completed runners move back into the visible ready pool when they re-arm, instead of staying stuck in stale `submitted` room state
- tightened host launch gating again so empty rematch pools are rejected and launch failures now name the exact ready/deck blockers instead of only returning counts
- added a real two-instance headless LAN smoke harness, with an autoload test director driving host/join/ready/launch/result sync over ENet and isolated `--save-suffix=...` slots keeping smoke runs off the main save data
- extracted LAN room presentation onto a shared multiplayer room snapshot/formatter layer so future internet-backed rooms can reuse the same room, readiness, and monitor UI model instead of rebuilding it from scratch
- added a stable player profile ID plus a persistent async challenge submission outbox so future backend/mobile sync can build on real queued result envelopes instead of starting from local history blobs
- added the first backend-ready sync stub for async multiplayer, with a provider-based batch sync service, settings-backed provider selection, optional HTTP endpoint configuration, and a manual outbox flush path writing queued challenge result envelopes into a local journal so the mobile/backend submission lifecycle can be exercised before a live service exists
- added a real HTTP challenge-sync smoke harness, with a headless sync driver posting to a local stub server so the online provider path is exercised end to end instead of only existing as an interface and DTO layer
- added a provider-backed remote leaderboard pull path for async challenges, including a multiplayer-screen `Refresh Board` action and cached standings for the selected code
- added a real HTTP leaderboard smoke harness, with a headless leaderboard driver pulling from a local stub server so the online read path is exercised end to end instead of only existing as a provider abstraction
- added a provider-backed remote challenge-feed pull path for async multiplayer, including cached backend-authored featured boards on the multiplayer screen and a unified `Refresh Online` action that refreshes both feed and leaderboard state
- added a real HTTP challenge-feed smoke harness, with a headless feed driver pulling from a local stub server so backend-authored board discovery is exercised end to end instead of only existing as a provider abstraction
- added a provider-backed online room-directory pull path on the multiplayer screen, so internet room discovery can cache remote room boards ahead of full internet room join/matchmaking
- added a real HTTP room-directory smoke harness, with a headless room-directory driver pulling from a local stub server so online room discovery is exercised end to end instead of only existing as a provider abstraction
- added a provider-backed online room-join ticket path on top of room discovery, so the client can negotiate backend join access and relay hints before real internet room transport exists
- added a real HTTP room-join smoke harness, with a temp .NET runner posting to a local stub server so join-ticket negotiation is exercised end to end instead of only existing as a provider abstraction
- added a provider-backed online room-session polling path on top of join tickets, so the client can pull backend room runner/ready/monitor state before real internet room transport exists
- added a real HTTP room-session smoke harness, with a temp .NET runner posting to a local stub server so backend room-lobby polling is exercised end to end instead of only existing as a provider abstraction
- added a provider-backed online room-action path on top of session polling, so the client can toggle backend ready state and then repoll room state before real internet room transport exists
- added a real HTTP room-action smoke harness, with a temp .NET runner posting to a local stub server so backend ready-state actions are exercised end to end instead of only existing as a provider abstraction
- added a provider-backed online room-host path on top of directory/join/session work, so the selected async board can be published as a hosted room, adopted locally as a host seat, and merged back into the cached room directory
- added a real HTTP room-create smoke harness, with a temp .NET runner posting to a local stub server so backend room-host publish is exercised end to end instead of only existing as a provider abstraction
- extended online room actions with a host-side launch/countdown path, so hosted rooms can move beyond ready-state toggles and simulate backend round launch before real internet transport exists
- added a real HTTP room-launch smoke harness, with a temp .NET runner posting a `launch_round` action to a local stub server so backend room countdown handoff is exercised end to end instead of only existing as a UI button
- extended online room actions again with a host-side rematch/reset path, so finished backend room rounds can return to prep instead of staying stuck after submitted results
- added a real HTTP room-reset smoke harness, with a temp .NET runner posting a `reset_round` action to a local stub server so backend rematch/reset handoff is exercised end to end instead of only existing as a UI button
- added a provider-backed online room-result submission path, so async challenge clears, fails, and retreats can now report into an active backend room when the board code matches
- added a provider-backed online room-scoreboard fetch path, so multiplayer prep can review shared room standings instead of stopping at room monitor text alone
- added real HTTP room-result and room-scoreboard smoke harnesses, with temp .NET runners exercising backend result submission and standings fetch end to end against local stub servers
- added a sequential online-room smoke suite plus local room-state regression harnesses, so the full backend room surface can be rerun from one entrypoint without parallel build-lock flakiness
- added a sequential HTTP multiplayer/backend smoke suite, so profile sync, challenge sync/feed/leaderboard, and the full online-room provider surface can be rerun from one command
- added a top-level multiplayer stack smoke suite, so LAN plus the full HTTP multiplayer/backend path can be rerun from one command before deeper transport work lands
- hardened the LAN and HTTP multiplayer smoke harnesses for reruns, with per-run LAN ports and reusable stub-server sockets so repeated stack-suite runs stop cross-connecting rooms or failing on recently closed ports
- added stale internet-room seat recovery with direct room rejoin plus quick-match fallback, and wired that recovery path into manual refresh, the seat button, and app-resume recovery so expired runner seats no longer stop at a dead-end warning
- expanded the player roster again with a `Siege Engineer` support unit that can repair the war wagon and is surfaced through the shop recommendation board
- expanded the campaign with a third `Emberforge March` district, including four new scripted stages and endless-route support
- expanded the campaign again with a fourth `Ashen Ward` district, including four new scripted stages, new checkpoint/decon/lab/vault battle palettes, and endless-route support for ranged-support/sapper-heavy pressure
- expanded the campaign to a fifth `Thornwall Pass` district, including five new scripted stages, new pass/shrine/watchfort battle palettes, and endless-route support for faster raider/sapper pressure
- expanded the campaign again with a sixth `Hollow Basilica` district, including five new scripted stages, new cathedral/ossuary/reliquary battle palettes, and endless-route support for splitter/caster/hex pressure
- expanded the campaign again with a seventh `Mire of Saints` district, including five new scripted stages, new marsh/chapel/ferry battle palettes, and endless-route support for attrition-heavy bloater/splitter/spitter pressure
- expanded the campaign again with an eighth `Sunfall Steppe` district, including five new scripted stages, new grassland/waystation/siegecamp battle palettes, and endless-route support for fast rider/howler/sapper pressure
- expanded the campaign again with a ninth `Gloamwood Verge` district, including five new scripted stages, new grove/witchcircle/timberroad battle palettes, and endless-route support for ambush-heavy runner/hexer/sapper pressure
- expanded the campaign again with a tenth `Crownfall Citadel` district, including five new scripted stages, new bridgefort/breachyard/innerkeep battle palettes, and endless-route support for mixed endgame command pressure
- backfilled `King's Road`, `Saltwake Docks`, `Emberforge March`, and `Ashen Ward` with fifth capstone stages so the campaign now hits the `10`-district / `50`-stage target
- added a late-game `Halberdier` unit that specializes in higher gate/base damage for reinforced stages
- added a `Sapper` enemy archetype that dives the war wagon/objective path and is now seeded into Emberforge stages and endless Emberforge pressure
- added stage-authored battlefield hazards with telegraph rings, timed pulses, loadout intel, and Foundry-specific heat/rail/furnace encounters
- added a new hazard-hit mission objective type so Foundry stars can reward actually navigating battlefield hazards cleanly
- added a `Dread Herald` support enemy with a live ally buff aura, plus Saltwake/Emberforge/endless seeding so late-game waves now mix in support-priority targets instead of only raw frontline pressure
- added a `Hexer` support enemy that can suppress courage gain and spike card recovery, plus late Ashen Ward/endless seeding so enemy pressure now attacks caravan tempo as well as hull
- added a `signal_jam_limit` mission objective type and seeded it into jammer-heavy Quarantine stages so support disruption now matters to star play too
- upgraded shared encounter intel so map/loadout/multiplayer briefings now call out support pressure like howlers, jammers, saboteurs, and spitters instead of only raw threat totals
- carried those support-pressure tags into campaign node tooltips and live next-wave battle intel so the player keeps the same read after deployment
- added live battle support-pressure telemetry so active howlers, jammers, saboteurs, spitters, bosses, and signal-jam windows stay visible while the fight is running
- upgraded the `Grave Lord` boss from a large stat block into a real rally-command encounter that buffs nearby undead and spawns escort bodies during boss stages
- expanded the player roster again with an `Alchemist` splash-damage card and the shared AoE attack support needed to counter clustered Saltwake/Emberforge waves
- expanded the player roster again with a late-game `Battle Monk` support card that uses a live ally aura to boost nearby caravan units against support-heavy Ashen Ward and late-route waves
- added the first spell/magic card layer with persistent spell ownership, shop/loadout decking, and live battle casts for `Fireball`, `Heal`, `Frost Burst`, `Lightning Strike`, and `Barrier Ward`
- added a first-pass medieval fantasy theme bible centered on the `Lantern Caravan`, the `Rotbound Host`, and `war wagon vs gatehouse` battlefield framing
- rethemed the main menu, route catalog, unit display names, and stage/map display text toward the new fantasy fiction while keeping internal IDs and balance data stable
- started the audio pass with a shared procedural audio layer for UI interactions, scene ambience pulses, deploys, impacts, hazard warnings, base hits, and win/loss stingers
- added persistent audio settings on the main menu so SFX level, ambience level, and mute state survive saves instead of resetting every boot
- moved audio/interface controls into a shared settings screen so campaign, shop, loadout, endless, and multiplayer prep all expose the same persistent options flow
- defined the long-term 10-district / 50-stage campaign target in a dedicated campaign plan and surfaced authored-vs-target progress on the title screen and campaign map
- carried the fiction lock deeper into runtime content by retheming lingering stage/wave labels, spell copy, endless fork/directive/contact text, and fallback data away from modern/prototype wording

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
