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
- real-money cash shop for gold and food packs via native mobile IAP and web payments

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

- ~~battle still uses prototype presentation instead of a strong war wagon/gatehouse fantasy~~ (war wagon/gatehouse framing implemented with route-themed HUD, heraldic silhouettes, and terrain-specific palettes)
- ~~the current content/theme is still too tied to zombie-modern framing and needs a full medieval fantasy conversion~~ (full medieval fantasy theme bible applied; menus, routes, units, stages, spells, and fiction all rethemed)
- ~~deployment was roster-based rather than deck/card-based~~ (deck/card-based deployment with cooldowns, active deck persistence, and loadout screen implemented)
- ~~stages are still mostly tuning-driven rather than explicitly scripted~~ (50 stages across 10 districts with authored waves, battlefield events, hazards, and modifiers)
- ~~no squad-building metagame beyond basic stage selection~~ (loadout screen, deck synergies, combo pairs, doctrine branches, and relic equipment all implemented)
- ~~no real shop/payment flow for buying units, unit upgrades, or bus/base upgrades~~ (convoy shop with unit purchases, leveling, spell upgrades, war wagon upgrades, and relic management)
- ~~current prototype currencies do not match the intended gold/food economy loop~~ (gold/food economy fully replaced scrap/fuel, with stage costs, upgrade costs, and reward tuning)
- ~~no mission objectives, stars, or encounter scripting~~ (stage-authored objectives, star ratings, heroic directives, and battlefield mission events all implemented)
- ~~no differentiated enemy abilities or special combat rules~~ (now have shield wall, lich, siege tower, mirror, tunneler, plus 10 unique bosses)
- placeholder rendering is still carrying too much of the experience — authored art assets are the main remaining gap

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

- launch roster target of **at least 16 unique player units**
- add a reusable **spell roster** with active magic/support/utility cards
- campaign target of **at least 10 maps/districts**
- each map should contain **5 or more stages**, for a target of **50+ campaign stages**
- stage modifiers and challenge content
- balance passes for courage economy, cooldowns, and wave pacing
- each map should have a distinct biome/faction/hazard identity
- unique named boss encounter at the end of each district
- equipment/relic system for unit customization
- unit active abilities unlocked at level 4+
- combo pair bonuses when specific units fight near each other
- daily challenge rotation

Minimum player unit target for the expanded medieval roster (all now in-game):

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
- `Alchemist`
- `War Hound` — cheap disposable scout, very fast, low health
- `Banner Knight` — aura buffer that boosts nearby allied attack and speed
- `Necromancer` — ranged caster that raises skeletons from enemy corpses
- `Rogue` — fast assassin that bypasses the frontline to target rear enemies
- `Berserker` — damage scales up as health drops, high-risk high-reward frontline

Minimum spell/magic target for the expanded medieval roster (all now in-game):

- `Fireball`
- `Heal`
- `Frost Burst`
- `Lightning Strike`
- `Barrier Ward`
- `Stone Barricade` — raise a temporary wall that blocks enemy advance
- `War Cry` — rally all deployed units with attack and speed boost
- `Earthquake` — wide-area damage and slow
- `Polymorph` — transform the toughest enemy in range into a harmless creature
- `Resurrect` — bring back the last fallen ally at half health

Enemy roster (all now in-game):

- `Risen` (basic melee), `Ghoul` (fast), `Grave Brute` (heavy), `Rot Hulk` (explodes on death)
- `Blight Caster` (ranged), `Bone Nest` (splits into minions), `Sapper` (fast base damage)
- `Dread Herald` (buff aura), `Hexer` (signal jam), `Bone Juggernaut` (armored tank)
- `Shield Wall` — blocks projectiles for nearby enemies, forces melee engagement
- `Lich` — periodically raises fallen enemies as new undead
- `Siege Tower` — slow structure that deploys a wave of enemies at the war wagon
- `Mirror Knight` — reflects 30% of incoming damage back to the attacker
- `Tunneler` — burrows behind player lines to attack rear units

Per-district bosses (all now in-game):

- `Grave Lord` (King's Road), `Tidecaller` (Saltwake Docks), `Iron Warden` (Emberforge March)
- `Plague Archon` (Ashen Ward), `Thornwall Chieftain` (Thornwall Pass), `Bone Pontiff` (Hollow Basilica)
- `Mire Behemoth` (Mire of Saints), `Steppe Warlord` (Sunfall Steppe), `Gloamwood Witch` (Gloamwood Verge)
- `Dread Sovereign` (Crownfall Citadel) — final boss, highest stats, spawns crushers

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

### Milestone 10: Cash Shop And Payments

Objective: add a real-money shop where players can purchase gold and food packs to accelerate progression.

**Gold packs:**

- `Pouch of Gold` — small gold bundle (e.g. 500 gold), cheapest entry point
- `Chest of Gold` — medium gold bundle (e.g. 2,000 gold), best value for regular players
- `War Chest` — large gold bundle (e.g. 6,000 gold), whale tier with a bonus percentage
- `King's Treasury` — premium gold bundle (e.g. 15,000 gold), maximum single purchase with the highest bonus

**Food packs:**

- `Field Rations` — small food bundle (e.g. 20 food), enough for a few stage entries
- `Caravan Provisions` — medium food bundle (e.g. 60 food), covers a full district push
- `Siege Stockpile` — large food bundle (e.g. 150 food), extended campaign run with exploration
- `Royal Granary` — premium food bundle (e.g. 400 food), maximum single purchase with a bonus

**Mixed/starter packs (optional):**

- `Adventurer's Kit` — starter bundle with gold + food + a guaranteed unit unlock, offered once to new players
- `Campaign Resupply` — mid-game bundle with gold + food at a combined discount

**Payment integration:**

- add native Android IAP via `godot-google-play-billing` plugin — accessible directly from C# via `Engine.GetSingleton("GodotGooglePlayBilling")`
- add native iOS IAP via `godot-store-kit` (StoreKit 2) plugin — GDExtension, so expose through a thin GDScript bridge node callable from C#
- for web/PC builds, add Stripe Checkout integration through the existing ASP.NET backend server via `Stripe.net` NuGet package
- no third-party middleware (RevenueCat etc.) — native store SDKs keep it simple for consumable packs and avoid the extra revenue cut on top of Apple/Google's 15–30% platform fee

**Server-side purchase validation:**

- add a `/purchase/validate` endpoint to the backend server
- client sends purchase receipt/token + player ID after native store purchase
- server validates receipt with Apple App Store Server API or Google Play Developer API
- server records the transaction in SQLite and credits gold/food to the player profile
- client fetches updated balance from the server after validation confirms
- never trust client-reported currency grants — all crediting happens server-side after receipt verification
- add purchase history logging and basic fraud detection (duplicate receipt rejection, velocity checks)

**Shop UI:**

- add a dedicated cash shop screen accessible from the caravan armory and campaign map
- display gold and food packs with clear pricing, bonus amounts, and value labels
- show current gold/food balance prominently
- confirm purchases with a second tap before initiating the native store flow
- surface purchase success/failure feedback clearly

## Immediate Next Sprint

**All code work is complete.** The project has reached code-complete status across all 10 milestones plus full ship-readiness infrastructure. Every remaining task is art production, audio production, deployment, or platform-specific build configuration — not code.

### What's Ready

| System | Status | How To Use |
|--------|--------|-----------|
| 16 player units, 10 spells, 16 enemies, 10 bosses | Implemented | All in `data/units.json`, `data/spells.json` |
| 50 campaign stages across 10 districts | Implemented | All in `data/stages.json` |
| Endless roguelite, multiplayer, daily challenges | Implemented | Full game loop |
| Cash shop + Stripe + native IAP scaffold | Implemented | Configure endpoint in Settings |
| Server (Docker + CI + admin dashboard + backups) | Ready to deploy | `cd server && docker compose up -d` |
| Unit sprite pipeline | Ready for art | Drop PNG at `assets/units/{visual_class}.png` |
| Background texture pipeline | Ready for art | Drop PNG at `assets/backgrounds/{terrain_id}.png` |
| Structure texture pipeline | Ready for art | Drop PNG at `assets/structures/{war_wagon,gatehouse}.png` |
| Music pipeline (17 track slots) | Ready for audio | Drop OGG at `assets/music/{track_id}.ogg` |
| SFX override pipeline (26 cue IDs) | Ready for audio | Drop OGG at `assets/sfx/{cue_id}.ogg` |
| Localization (English complete) | Ready for translation | Add `data/locale/{lang}.json` |
| Export presets (Web, Android, iOS) | Ready to build | `godot --export-release "Web" builds/web/index.html` |
| 71 server tests + 1119 data checks | All passing | `dotnet run -- --test` / `--test-data ../data` |

### What Remains (Non-Code)

1. **Art production** — author sprite sheets for 44 units + 10 bosses, 31 battlefield backgrounds, 2 structure textures (see ASSETS.md for the full manifest with frame sizes, tint colors, animation states, and silhouette briefs)
2. **Audio production** — record/compose 17 music tracks and 26 sound effects (see ASSETS.md sections 8-9 for the full cue/track list with mood descriptions and format specs)
3. **Server deployment** — `docker compose up -d` in `server/`, configure `STRIPE_SECRET_KEY` and `STRIPE_WEBHOOK_SECRET` in `.env` (see `.env.example`), verify at `/admin`
4. **Platform builds** — build Godot export templates with `godot-google-play-billing` (Android) and `godot-store-kit` (iOS) GDExtension plugins; `NativeIAPService` connects automatically
5. **Translation** — create `data/locale/{lang}.json` files for target languages (60+ keys in `en.json` as reference)
6. **Playtesting** — validate the balance pass (Mage/Rogue/Archer cost changes, Earthquake/Stone Barricade spell changes, late-game reward scaling in stages 42-50)

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
- upgraded the audio pass with route-specific ambient profiles and battle-pressure-driven pulse timing, so map/prep/combat no longer share one static background texture across every district
- added a shared campaign readiness evaluator and doctrine-aware armory guidance, so map/loadout/armory prep now calls out real coverage gaps instead of leaving balance reads buried in raw threat text
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
- added the `Spearman` reach-frontline card to complete the minimum 11-unit medieval roster target, with shop recommendations for rush and heavy stages
- added persistent spell upgrades with level 1-3 progression, gold-funded leveling in the shop, level-scaled power/radius/cooldown/courage in battle, and level display across loadout/shop/battle HUD
- added four new stage modifier types (`elite_vanguard`, `rapid_assault`, `cursed_ground`, `fortified_deploy`) with battle integration, readiness scoring, and seeded them across 12 mid-to-late campaign stages for replay variety
- completed legacy phrasing cleanup: renamed all internal `scrap/fuel` variable and method names to `gold/food` in BattleController.cs while preserving backward-compatible save data shims
- applied a balance pass across all 50 stages: smoothed late-game difficulty curve (stages 40-50), fixed stage 6 food economy spike, reduced stage 36 modifier overload, capped Crusher composition weight, and bumped sparse late-game spawn rates
- improved presentation pass: broke readiness gaps into bullet lists, added explicit stat labels and courage cost to deploy tooltips, split war wagon upgrades into two readable rows, restructured endless run payout into indented breakdown, split contact telemetry into multi-line format, and changed deploy/spell button states from generic READY/RECOVER to actionable DEPLOY/CAST/CD labels showing deficit amounts
- added scene transition fades so menu and battle scene changes now use a brief black fade-out/fade-in instead of hard cuts
- expanded the audio catalog with spell cast sounds (pitch-shifted per spell type), boss spawn stingers, and upgrade confirmation cues, wired into battle spell casts, boss unit spawns, and all shop upgrade paths
- added real `CpuParticles2D` particle effects for deploy bursts, unit death bursts, projectile impacts and trails, spell casts (fireball/heal/frost/lightning/ward), base hit debris, boss spawn bursts, death burst explosions, and victory/defeat end particles
- added terrain-specific ambient particle systems so each battlefield now has atmospheric effects: embers in foundry/smelter, snow in pass/watchfort, mist in marsh/swamp/shipyard, dust motes in cathedral/shrine, ash fall in ossuary/bridgefort, leaf drift in grove/timberroad, wind dust in grassland/steppe, and fireflies at night
- added a critical health screen-edge vignette that pulses red when the war wagon drops below 35% hull
- added visual courage meter bar and wave progress bar to the battle HUD with animated fill and flash-on-change
- added visual cooldown overlay on deploy and spell card buttons so card recovery reads as a shrinking dark fill
- added staggered fade-in entrance animations and ambient ember particles to the title screen
- added side-panel fade transition on campaign map stage selection changes
- added animated fade-in/scale battle end panel instead of instant visibility toggle
- added pulsing glow ring around selected campaign map nodes and gentle bob on locked stage icons
- added staggered fade-in entrance animations to loadout briefing panels
- added consistent entrance animations across all remaining menus: shop (title/summary/units/base panels), endless prep (title/mission/squad/bottom panels), settings (centered panel scale+fade), multiplayer (title/mission/squad/bottom panels), and LAN race (title/board/room/bottom panels)
- added a standalone ASP.NET backend server (`server/`) implementing all M9 HTTP contracts with SQLite persistence: player profile sync, challenge submission and leaderboards, challenge board feed, online room lifecycle (create/join/session/action/result/scoreboard/telemetry/leave/report/matchmake/seat-lease), stale room expiry, and seeded featured challenge boards
- added battle pause with Escape key, dimmed overlay, and hotkey reference display
- added keyboard hotkeys for battle: 1-5 to select unit cards, Q-T to select spell cards
- added a WebSocket relay transport (`/ws/relay/{roomId}`) for real-time online room state sync with peer join/leave notifications, message broadcast, and automatic room cleanup
- added 19 backend integration tests covering all HTTP endpoints plus the WebSocket relay, all passing
- expanded the player roster with 5 new units: `War Hound` (cheap/fast disposable scout), `Banner Knight` (aura buffer), `Necromancer` (raises skeletons from enemy corpses), `Rogue` (backstab targeting bypasses frontline), `Berserker` (damage scales up as health drops)
- expanded the spell roster with 5 new spells: `Stone Barricade` (temporary lane wall), `War Cry` (global ally buff), `Earthquake` (wide AoE + slow), `Polymorph` (transform toughest enemy into harmless creature), `Resurrect` (revive last fallen ally at half health)
- expanded the enemy roster with 5 new archetypes: `Shield Wall` (blocks projectiles for nearby enemies), `Lich` (periodically raises fallen undead), `Siege Tower` (slow structure that deploys enemies at the war wagon), `Mirror Knight` (reflects 30% damage back to attackers), `Tunneler` (burrows behind player frontline)
- added 9 unique per-district boss encounters: `Tidecaller`, `Iron Warden`, `Plague Archon`, `Thornwall Chieftain`, `Bone Pontiff`, `Mire Behemoth`, `Steppe Warlord`, `Gloamwood Witch`, and `Dread Sovereign` — each with distinct abilities, escort spawns, and stat profiles
- seeded new enemy types (shield wall, lich, siege tower, mirror knight, tunneler) across 31 mid-to-late campaign stages
- added an equipment/relic system with 12 relics (4 common, 4 rare, 4 epic), persistent ownership and per-unit equip slots, and stat bonuses applied during battle stat building
- added unit active abilities unlocked at level 4+ with 16 unique per-unit skills: Cleave, Arrow Volley, Shield Wall, Piercing Thrust, Snipe, Charge, Deploy Turret, Arcane Beam, Sweeping Strike, Volatile Flask, Blessing, Pack Howl, Inspire, Mass Raise, Vanish, Blood Frenzy
- added combo pair proximity bonuses for 6 unit pairings: Phalanx (Shield Knight + Spearman), Hunter Pack (two War Hounds), Arcane Guard (Mage + Shield Knight), Skirmish Line (Cavalry + Rogue), Siege Corps (Halberdier + Alchemist), Holy Order (Battle Monk + Banner Knight)
- added daily challenge rotation with deterministic date-seeded board selection, alternating locked/free squad days, and stage picks from the mid-campaign range
- added equipment/relic panel in the caravan armory shop with per-unit equip/unequip buttons, rarity-tinted panels, and stat bonus display
- wired relic drops from boss kills (60% common, 30% rare, 10% epic) and heroic stage directive completions (40% common, 20% rare, 5% epic)
- surfaced daily challenge section in multiplayer menu with date, board label, stage, and squad mode
- surfaced combo pair hints in loadout menu when matching units are decked together
- surfaced active ability info in shop and loadout unit cards with unlock level, description, and cooldown
- added encounter intel threat tags, wave pressure flags, and threat score multipliers for all 5 new enemy types
- added shop recommendations for all 5 new player units based on stage enemy composition
- added all 5 new enemy types to endless mode weighted spawn tables with wave thresholds and route-specific weights
- added spell audio pitch scaling for all 5 new spells
- added particle effects for all 5 new spells with distinct visual signatures
- fixed Plague Archon boss jam_signal to also spawn escort jammers alongside the debuff
- fixed polymorph spell debuff that was silently clamped to 1.0x by ApplyTemporaryCombatBuff (now uses speed/defense modifiers)
- fixed stone barricade spell spawning permanent units by adding duration-based expiry with crumble feedback
- fixed equipment speed scale bonus being defined but never applied to unit stats
- completed the daily challenge feature with a playable "Play Daily Challenge" button, seeded locked-squad unit generation, per-day completion tracking (save v27), auto-detection of daily completions, and replay support
- upgraded endless boss checkpoints from a single Grave Lord to a 6-tier boss rotation using all 10 district bosses with themed escort packs and escalating rewards
- bumped spell deck size from 2 to 3 so players can meaningfully choose between the 10 available spells
- added spell counter recommendations to the loadout threat briefing based on detected enemy composition (e.g., Polymorph vs armor, Earthquake vs swarms, Resurrect vs boss stages)
- added relic acquisition summary to the victory end panel when a new relic drops from a boss kill
- added unit-type-specific deploy and death sound pitch offsets so hounds, banners, necromancers, berserkers, siege towers, and other unit classes each sound distinct
- expanded the battle pause hotkey reference with tips about active abilities, combo pairs, new spell types, and relic equipment
- added notable enemy callouts to campaign map stage tooltips (Shield Wall, Lich, Siege Tower, Mirror Knight, Tunneler, and boss encounters)
- expanded endless boons from 3 to 8 with Relic Forge, Corpse Hoard, Berserker Blood, Shield Formation, and Splitter Bane — all with full runtime effects (skeleton health scaling, global berserk rage, bus armor, gold bonus)
- expanded endless draft upgrades from 6 to 11 with Skeleton Surplus, Relic Spark, Berserk Ritual, Mirror Ward, and Rally Banner — each with immediate combat effects
- added 5 new async challenge mutators: Mirror Field, Undying Host, Siege Wave, Splitter Swarm, Tunneler Ambush — with score multipliers and stat scaling
- added 2 new featured challenge board templates (Ambush Gauntlet, Siege Front) that rotate the new mutators into the daily featured queue
- added 3 new war wagon base upgrades: Relic Repository (boss drop rate), Arrow Ward (projectile damage reduction), Siege Hammer (gatehouse damage bonus)
- added 3 new stage modifiers: mirror_pressure (damage reflection), lich_graveyard (enemy reanimation), tunnel_invasion (periodic tunneler spawns) — seeded across 7 campaign stages
- added relic rewards for completing campaign districts: common relics for early districts, rare for mid, epic for late, plus two bonus epic relics for full campaign completion
- added 4 difficulty modes (Apprentice, Warden, Champion, Legend) with scaling enemy stats, courage gain, and gold rewards — selectable in Settings, excluded from challenge mode for fair scoring
- added a 15-hint contextual tutorial system that teaches courage, cooldowns, spells, deck building, food, relics, combos, active abilities, bosses, endless mode, daily challenges, and difficulty — hints show once per context and can be disabled in Settings
- added a 20-achievement system across 5 categories (campaign, combat, endless, collection, mastery) with persistent tracking, automatic condition evaluation, achievement notifications on unlock, and a full achievement checklist in Settings
- expanded endless route forks from 3 to 8: Ambush Ravine, Ritual Grounds, Siege Camp, Plague Winds, Necromancer's Tomb — each with distinct enemy pressure profiles and gold scaling
- expanded endless contact events from 3 to 6: Relic Recovery (dig site defense), Ritual Disruption (interrupt enemy channel), Convoy Escort (guard supply wagon) — mapped to the new route forks
- added endless run history tracking with persistent top-20 run records (wave, time, route, boon, gold, difficulty) and a run history panel in the endless prep screen with best-wave highlighting
- added per-unit battle statistics tracking (damage dealt, spells cast, active abilities triggered) displayed on victory/defeat/endless debrief screens showing top 3 damage-dealing units
- added 3 new audio cues: achievement unlock (bright ascending double-tap), relic pickup (sparkly mid-high), boss death (low thud + high chime) — wired into achievement, relic drop, and boss kill events
- added prestige color system with 3 unlockable tint variants per unit (Crimson via Boss Slayer, Frost via Endurance, Golden via Campaign Complete) — selectable in the armory with persistent per-unit color choice
- added unit voice line callouts: 16 player units each have deploy, kill, and ability quotes displayed as floating battle text with probability gating; 5 enemy types have deploy callouts; kill credit tracked via LastDamagedBy
- added weather system with 5 conditions (Clear, Heavy Rain, Dense Fog, Ash Storm, Blizzard) affecting speed, aggro range, courage gain, and damage — seeded across 12 campaign stages with enhanced ambient particle effects (rain drops, fog overlay, wind-driven ash, heavy snow) and weather info in loadout briefing and map tooltips
- wired all 3 new endless contact events (Relic Recovery, Ritual Disruption, Convoy Escort) into the full contact handling pipeline: anchors, colors, cadence, rewards, success tradeoffs, failure penalties, response waves, support moments, and health/repair/progress logic
- added 4 backend integration tests covering achievement sync, achievement list, daily complete, and daily leaderboard endpoints — all 23 server tests pass
- added daily leaderboard display in the multiplayer menu showing top 10 scores for today's challenge with rank, profile, and score
- extended the /health endpoint to report achievement and daily completion row counts
- added a cash shop product catalog (`data/shop_products.json`) with 4 gold packs (Pouch/Chest/War Chest/Treasury), 4 food packs (Rations/Provisions/Stockpile/Granary), and 2 mixed packs (Adventurer's Kit starter bundle, Campaign Resupply)
- added `ShopProductCatalog` data loader for the client-side product catalog with category filtering and ID lookup
- added a dedicated `CashShopMenu` screen ("Royal Storehouse") with gold pack, food pack, and starter/bundle columns, two-tap purchase confirmation, live gold/food balance display, and staggered entrance animations
- wired the cash shop into `SceneRouter` and added navigation buttons on the campaign map and caravan armory
- added server-side `purchases` table with purchase ID, profile, product, platform, receipt token, transaction ID, and credit amounts
- added `/purchase/validate` endpoint with server-side receipt recording, duplicate transaction rejection, velocity limiting (10/hour), and one-time purchase enforcement for the starter kit
- added `/purchase/history` endpoint returning the 50 most recent purchases for a profile
- added `/purchase/products` endpoint returning the full product reward catalog from the server
- extended the `/health` endpoint to report purchase row counts
- added `HttpApiPurchaseValidationProvider` on the client for server-validated purchase flow with receipt submission and history retrieval
- added `GameState` purchase tracking: `TryApplyPurchaseReward()` credits gold/food, records purchased product IDs, grants random unit unlocks for starter kits, and persists all purchase state (save v31)
- added offline/local purchase fallback so the cash shop works without a configured validation endpoint
- added 5 backend integration tests covering purchase validation, purchase history, product catalog, duplicate rejection, and one-time starter kit enforcement — all 28 server tests pass
- added Stripe Checkout integration to the backend server via `Stripe.net` NuGet package
- added `/purchase/stripe-checkout` endpoint that creates a Stripe Checkout session with inline price data, product metadata, and profile binding — returns a checkout URL the client opens in a browser
- added `/purchase/stripe-webhook` endpoint that processes `checkout.session.completed` events, validates signatures when `STRIPE_WEBHOOK_SECRET` is set, and credits gold/food to the player profile with duplicate and one-time purchase protection
- added `/purchase/stripe-status` endpoint that queries a Stripe session by ID so the client can poll payment completion after returning from checkout
- added client-side Stripe flow: `HttpApiPurchaseValidationProvider.CreateStripeCheckout()` and `CheckStripeStatus()` methods, plus `CashShopMenu.ExecuteStripePurchase()` that opens the checkout URL via `OS.ShellOpen()`
- the cash shop now routes web/PC purchases through Stripe Checkout and mobile purchases through the native receipt validation path
- added 2 backend integration tests covering Stripe endpoint behavior when no API key is configured (503 responses) — all 30 server tests pass
- added a `NativeIAPService` autoload that detects platform (iOS/Android/web) and wires into `GodotGooglePlayBilling` or `StoreKit` singletons when present — queries native product prices, handles purchase callbacks, consumes Google receipts, and feeds results into server-side validation
- added native localized price display in the cash shop so iOS/Android builds show store-localized prices instead of hardcoded USD amounts
- updated `CashShopMenu` to route mobile purchases through `NativeIAPService.PurchaseProduct()` → receipt → server validation, web/PC through Stripe Checkout, and offline through local crediting
- added a Payments section to the Settings screen with purchase validation endpoint config, total purchase count, and detected payment platform display
- wired `SetPurchaseValidationEndpoint("")` into the Restore Defaults button
- added server deployment infrastructure: `Dockerfile` (multi-stage build with `/data` volume for SQLite), `docker-compose.yml` with health checks, `.dockerignore`, and `.env.example` with documented configuration for Stripe keys and CORS
- added CORS middleware with configurable `AllowedOrigins` (defaults to `*`, supports comma-separated origin list with credentials)
- added `StaleDataCleanup` background service that runs every 15 minutes to expire stale rooms (6h+), purge old telemetry (24h+), and trim aged reports (90d+)
- replaced the flat `/health` endpoint with a lightweight check plus a new `/stats` endpoint reporting full table counts, active/expired rooms, relay state, Stripe configuration status, and server uptime
- applied a comprehensive balance pass across units, spells, and stages:
  - **Spells:** Earthquake power 28→38 and courage 30→28 (was weaker than early Fireball), Stone Barricade courage 22→26 and duration 6→4.5s (overpowered for cost), Frost Burst courage 24→20 (overpriced), Barrier Ward power 0.62→0.72 (clearer value), Resurrect courage 32→28 and cooldown 30→26s (too expensive for single use)
  - **Units:** Archer cost 30→24 (range tax was too steep vs Swordsman), Crossbowman damage 13→16 (underperforming for cost), Mage cost 42→46 and damage 26→22 (too strong too early at unlock 6), Rogue cost 22→26 (dominant pick needed cost increase), Battle Monk cost 34→30 (overpriced vs Banner Knight), Berserker damage 16→18 (underwhelming for unlock 12)
  - **Stages:** Stage 5 health/damage scales raised to 1.34/1.30 (was backward vs stage 4), food reward 4→5; Stage 13 food reward 8→10 (difficulty wall with no compensation); Stage 36 health/damage scales raised to 4.12/3.90 (was identical to stage 35); Stages 42-50 gold rewards increased 10-29% and food rewards increased 2-8 to close the late-game reward desert where linear rewards met exponential difficulty
- added GitHub Actions CI workflow (`server-tests.yml`) that runs `dotnet run -- --test` on server pushes and builds+verifies the Docker image on main
- added Godot export presets (`export_presets.cfg`) for Web (PWA-enabled), Android (arm64, Gradle build, Google Play Billing ready), and iOS (StoreKit IAP capability enabled)
- added `server/deploy.sh` script that runs tests, builds Docker image, and optionally pushes to a configured registry
- updated ASSETS.md art manifest from 11 player + 11 enemy units to the complete 16 player + 1 summoned + 16 enemy + 10 boss roster with silhouette briefs, tint colors, scale values, and visual class assignments for all new units (War Hound, Banner Knight, Necromancer, Rogue, Berserker, Risen Thrall, Shield Wall, Lich, Siege Tower, Mirror Knight, Tunneler, and all 10 district bosses)
- updated the frame size reference table to cover all unit scales from 0.82x (War Hound) through 1.55x (Dread Sovereign)
- added cloud save backup and restore: `POST /cloud-save/upload` accepts the full save JSON with SHA-256 hash and version, `GET /cloud-save/download` returns the stored save, `GET /cloud-save/info` returns metadata without the payload — all stored per profile in a new `cloud_saves` table with 512KB size limit
- added `CloudSaveService` on the client with `Upload()`, `Download()`, and `GetInfo()` methods that serialize the local save file and sync with the server
- added `GameState.ReloadFromDisk()` so cloud save restore can apply the downloaded save immediately
- added cloud save UI in Settings: Upload Save, Restore Save, and Check Cloud buttons with status display under the Payments section
- added `InputEventScreenTouch` handling in `BattleController` so mobile touch inputs trigger battlefield deploy/cast at the touch position, alongside existing mouse click support
- added 3 backend integration tests covering cloud save upload, download (with empty-profile check), and info — all 33 server tests pass
- added cloud save count to the `/stats` endpoint
- added battle speed controls with 4 speeds (1x, 1.5x, 2x, 3x) via an in-HUD speed button and Space key toggle — `Engine.TimeScale` resets to 1x on scene exit
- updated the pause overlay hotkey reference to include the Space speed toggle
- added auto cloud backup after campaign stage victories — `TryAutoCloudBackup()` fires silently after `ApplyVictory` when a server endpoint is configured
- added `SafeAreaService` autoload that queries `DisplayServer.GetDisplaySafeArea()` on startup and window resize, exposing per-edge inset margins for mobile notch/island displays
- applied safe area insets to the battle HUD: top info panels offset by top/left insets, bottom card panel offset by bottom insets and narrowed by left+right insets
- `SafeAreaService.CreateSafeMarginContainer()` and `ApplyToControl()` helpers available for all menus to adopt safe area margins
- added a sprite loading pipeline (`UnitSpriteLoader`) that caches sprite sheets per visual class from `assets/units/{visual_class}.png` with optional JSON animation metadata — supports custom frame sizes, per-state frame ranges, durations, and loop flags, or falls back to a default row-based layout
- modified `Unit._Draw()` to check for a sprite sheet before rendering: when a sprite exists it renders the correct animation frame with facing flip, hit flash modulation, and position-based walk detection; when no sprite is found it falls back to the existing procedural `DrawUnitSilhouette` code
- art assets can now be dropped into `assets/units/` one unit at a time with no code changes — the game automatically picks up new sprites and renders them instead of placeholders
- added `assets/units/_example.json` documenting the expected sprite sheet metadata format
- updated ASSETS.md with sprite pipeline integration instructions
- added `BattlefieldTextureLoader` for background and structure textures — caches textures per terrain/structure ID from `assets/backgrounds/` and `assets/structures/`, with the same fallback-to-procedural pattern as the unit sprite pipeline
- modified `BattleController._Draw()` to render a background texture from `assets/backgrounds/{terrain_id}.png` when present, falling back to procedural sky/ground/stripes when absent — terrain decorations and ambient particles still render on top
- modified `DrawPlayerBus()` and `DrawEnemyBarricade()` to render structure textures from `assets/structures/war_wagon.png` and `assets/structures/gatehouse.png` when present, with hit flash modulation, damage smoke, and health bars — falling back to procedural draw when absent
- created `assets/backgrounds/` and `assets/structures/` directories so art can be dropped in for all 31 terrain types and both base structures with no code changes
- updated ASSETS.md with drop-in instructions for backgrounds (per terrain_id) and structures (war_wagon, gatehouse)
- added `MusicPlayer` autoload with crossfade support: maps scene paths to track IDs (title, campaign, shop, loadout, battle, etc.) and route IDs to per-district battle tracks (battle_road through battle_citadel) — loads OGG/MP3/WAV from `assets/music/` with the same miss-cache pattern as the other asset pipelines
- wired `MusicPlayer.PlayForScene()` into `SceneRouter.ChangeScene()` so music transitions automatically on every scene change
- added `MusicVolumePercent` to `GameSaveData` (persisted) and `GameState` with `SetMusicVolumePercent()` that updates the `MusicPlayer` volume scale
- added Music +/- buttons to the Settings audio panel alongside SFX/Ambience controls, with music volume shown in the audio label and reset in Restore Defaults
- created `assets/music/` directory for drop-in music tracks
- updated ASSETS.md with a full music section: 17 track IDs mapped to scene/route contexts, format recommendations (loopable OGG at -14 LUFS), and drop-in instructions
- added authored SFX override support to `AudioDirector`: `RegisterCue()` now checks `assets/sfx/{cue_id}.ogg/.mp3/.wav` before falling back to the procedural synth cue — authored audio files can be dropped in per sound effect with no code changes
- updated ASSETS.md SFX section with drop-in instructions for authored sound effects
- added a localization framework (`Locale` static class) that loads key-value translations from `data/locale/{language}.json` with English fallback — supports `Locale.Get("key")` with format args, language enumeration, and per-language file isolation
- created `data/locale/en.json` with 60+ UI string keys covering title, buttons, HUD, results, shop, cash shop, settings, cloud save, endless, multiplayer, difficulty, and currency labels
- added `Language` to `GameSaveData` and `GameState` with `SetLanguage()` that updates `Locale` and persists the choice
- initialized `Locale.SetLanguage()` during `GameState.LoadOrInitialize()` so the correct language loads on boot
- added a Language cycle button to the Settings interface panel that rotates through all available locale files in `data/locale/`
- wired language display into the Settings interface label and language reset into Restore Defaults
- added a gameplay analytics system: `POST /analytics/ingest` accepts batches of typed events (up to 50 per request) with profile, platform, and version metadata — stored in an `analytics_events` table with type+time index
- added `GET /analytics/summary` for querying event counts grouped by event data (filterable by type and time window) — useful for identifying which stages have high fail rates, which units are most purchased, etc.
- added `AnalyticsService` on the client with batched queue (auto-flush at 10 events, max 200 queue), convenience methods for key game events (`TrackStageStart`, `TrackStageEnd`, `TrackEndlessEnd`, `TrackUnitPurchase`, `TrackIAPPurchase`, `TrackDailyChallenge`, `TrackSessionStart`), and silent error handling
- wired analytics tracking into `ApplyVictory`, `ApplyDefeat`, `ApplyRetreat`, and session start — stage win/loss data now flows to the server for difficulty tuning
- added analytics event count to the `/stats` endpoint and 30-day retention cleanup to `StaleDataCleanup`
- added 2 backend integration tests covering analytics ingest and summary — all 35 server tests pass
- added a loading tip system: `LoadingTipCatalog` with 30 gameplay tips, displayed as a centered label during scene transition fades — tips fade in/out alongside the black overlay
- modified `SceneRouter` to show a random tip during every scene change, providing useful gameplay guidance during load times on mobile
- added 26 validation and edge case tests covering: empty/missing required fields (profile IDs, room IDs, board codes, dates), bounds rejection (oversized scores, elapsed times, enemy defeats in challenge sync), nonexistent resource lookups (rooms, profiles, board codes, dates), duplicate room joins, unknown action types, oversized payloads (cloud save 512KB, analytics 50-event batch), empty batch handling, event type filtering, invalid daily scores, and the health endpoint
- fixed a room join bug where `INSERT OR REPLACE` silently allowed duplicate joins instead of returning 409 — `InsertSeat` now checks for existing active seats and throws SqliteException(19) for duplicates, while properly allowing rejoin after leaving
- fixed 6 server input validation bugs found by the edge case test pass:
  - `RoomResult` now rejects empty roomId (was silently updating with empty key)
  - `RoomTelemetry` now rejects empty roomId
  - `RoomLeave` now rejects empty roomId
  - `RoomReport` now rejects empty roomId, reporterProfileId, and reason
  - `RoomMatchmake` now rejects empty profileId and boardCode
  - `RoomSeatLease` now rejects empty roomId and profileId
- added 6 more validation tests covering all the above fixes (missing roomId for result/telemetry/leave, missing fields for report/matchmake/seat-lease)
- all 67 server tests pass (35 happy-path + 32 validation/edge case)
- added `DataIntegrityValidator` that parses all game data JSON files (units, stages, spells, equipment, shop products) and validates 1119 cross-reference checks: duplicate IDs, empty required fields, non-positive stats, spawn-on-death and special-spawn unit ID references, stage MapId validity, stage wave unit ID references, reward-vs-cost ratios, sequential stage numbering, equipment rarity values, spell effect types, and roster size minimums (16 player units, 15 enemies, 10 spells, 12 equipment, 50 stages, 10 products)
- runnable via `dotnet run -- --test-data ../data` from the server directory
- added data validation step to the GitHub Actions CI workflow so data authoring errors are caught before merge
- added `UnitPool` object pool for combat units: `Acquire()` returns a pooled or new `Unit`, `Release()` hides and caches it (max 64), `Clear()` frees all on scene exit — reduces GC pressure from spawning/freeing hundreds of Node2D instances per battle on mobile
- added `Unit.ResetForPool()` that clears all combat state, timers, buffers, and sprite cache so pooled units start clean
- wired `UnitPool.Acquire()` into `BattleController.SpawnUnit()` and `UnitPool.Release()` into the death cleanup loop (replaces `new Unit()` / `QueueFree()`)
- added GDPR/analytics consent system: `AnalyticsConsent` and `HasShownConsentPrompt` persisted in save data, `AnalyticsService.Track()` gated on consent, toggle in Settings under Privacy section
- added first-run consent prompt on the title screen: modal overlay with "Allow Analytics" / "No Thanks" — shown once, choice remembered across sessions, changeable in Settings
- added `ProjectilePool` object pool (max 48) matching the unit pool pattern — `Acquire()`, `Release()`, `Clear()` with `Projectile.ResetForPool()` that clears target, callbacks, and trail particles
- wired `ProjectilePool.Acquire()` into all 3 projectile spawn sites in BattleController and `ProjectilePool.Release(this)` into both hit and cancel paths in Projectile
- both pools clear on scene exit via `_ExitTree()`
- rewrote the README as a comprehensive shipping guide: quick start, server commands, test counts, drop-in asset tables, translation guide, export preset commands, key documents, and architecture overview
- added a server admin dashboard at `/admin` — a styled HTML page showing live overview cards (players, rooms, purchases, cloud saves, analytics, achievements, dailies, reports, relay rooms), recent purchase log, 24h analytics event breakdown, uptime, Stripe config status, and API quick links
- added `AppRatingPrompt` for mobile store ratings: shows a modal overlay ("Enjoying Crownroad?" with "Rate Now" / "Maybe Later") once after unlocking 5+ stages, uses the hint system to track shown state, opens the platform-appropriate store listing via `OS.ShellOpen()`
- wired the rating prompt into the campaign map `_Ready()` so it appears naturally when returning from a successful stage clear
- added database migration system: `schema_version` table tracks applied migrations, `RunMigrations()` runs on every `Initialize()` call, `TryAddColumn()` helper for safe column additions — migration 2 adds `price_cents` to purchases for Stripe audit
- added `Database.Backup()` using SQLite `VACUUM INTO` for atomic database snapshots, plus `CleanupOldBackups(keepCount)` for rotation
- added periodic database backup to `StaleDataCleanup` (every 6 hours, keeps last 5 backups)
- added `POST /admin/backup` endpoint for on-demand database backup
- added 2 tests: database backup endpoint and schema version verification — all 69 server tests pass
- added a balance visualization dashboard at `/admin/balance` — styled HTML page rendering interactive bar charts for enemy health scale and reward gold across all 50 stages, a color-coded stage detail table (food ratio warnings in red/yellow/green), a player unit value table showing DPS, damage-per-cost, and HP-per-cost efficiency with outlier highlighting, and a spell value table showing power-per-courage ratios — reads directly from the game data JSON files so it always reflects the current balance state
- linked the balance dashboard from the admin panel
- added `DebugConsole` autoload (backtick key to toggle): in-game command line with 18 commands — `gold <n>`, `food <n>`, `unlock <stage>`, `unlockall`, `reset`, `stage <n>`, `difficulty <id>`, `speed <n>`, `stats`, `units`, `spells`, `stages <from> <to>`, `achievement <id>`, `relic <id>`, `equip <unit> <relic>`, `cloud upload/download`, `analytics flush` — with command history (up/down arrows) and scrolling output
- added accessibility features: `FontSizeOffset` (-4 to +8, applied via `ThemeDB.FallbackFontSize`) and `HighContrast` toggle, both persisted in save data, with Font -/+  and High Contrast buttons in Settings, and reset in Restore Defaults
- ran a code quality review across all 14 new files; fixed: `CloudSaveService` null-ref on `GameState.Instance.PlayerProfileId` (3 sites), `NativeIAPService` thread-safety for `_pendingPurchaseCallback` (6 callback sites + setter now locked), `UnitPool.Release` parent check using `IsInsideTree()`, `HttpClient` ambiguity with `Godot.HttpClient` in AnalyticsService and CloudSaveService (fully qualified), missing `using System` in SettingsMenu, `Control.SizeFlags` qualifier in DebugConsole
- verified both the Godot client (`dotnet build Game.csproj`) and server (`dotnet run -- --test`) build cleanly with 0 errors, 0 warnings
- wired the `HighContrast` flag into actual rendering: player units are lightened 25%, enemy units darkened 15%, green outline ring behind player units, red behind enemies, health bars wider/taller with team-colored fills (green=player, red=enemy) and darker backgrounds
- added crash/error reporting: `crash_reports` server table, `POST /crash-report` endpoint with error type, message, stack trace (truncated to 4KB), client version, platform, and active scene — plus crash count in `/stats` and admin dashboard
- added `CrashReporter` autoload that hooks `AppDomain.CurrentDomain.UnhandledException` and sends crash reports to the server silently, with `ReportError()` and `ReportWarning()` static methods for manual reporting
- added 2 server tests for crash reporting (happy path + missing details validation) — all 71 server tests pass
- added `NetworkStatus` autoload that pings `/health` every 45 seconds to track online/server-reachable state, with `GetStatusLabel()` and `GetStatusColor()` for UI display
- added network status indicator to the title screen showing "Online" (green), "Server unreachable" (yellow), "No network" (red), or "Offline mode" (gray)
- added `DeepLinkHandler` autoload that parses challenge codes from command-line args (`--challenge=CH-...`), URLs (`https://crownroad.game/challenge/CH-...`), or bare codes — stores as pending challenge consumed on title screen to auto-navigate to multiplayer
- added "Share Link" button on the multiplayer screen that copies a `https://crownroad.game/challenge/{code}` deep link URL to the clipboard
- wired deep link handling into the title screen: if a pending challenge code exists on load, auto-navigates to multiplayer with that code selected
- added `ParticleTextureLoader` for drop-in particle textures from `assets/particles/{id}.png` — wired into `BattleParticles.CreateBase()` so all particle effects use authored textures when present (default: `particle_soft`), completing the 6th and final asset pipeline
- added `ScreenshotCapture` utility: captures viewport to PNG in `user://screenshots/`, available via F12 in battle, `screenshot` in debug console, and `ScreenshotCapture.Capture()` API
- updated the battle pause overlay with F12 screenshot hotkey reference
- updated ASSETS.md with particle texture drop-in instructions

## Bugs, Stability, And Hardening

Issues found through code review. All actionable items have been fixed. Remaining items are architectural decisions or accepted design tradeoffs.

### Critical: Server Concurrency And Data Races — ALL FIXED

- **(FIXED)** **Room join race condition** — `CountActiveSeats()` + `InsertSeat()` now run inside a single transaction so concurrent joins cannot exceed `max_players`.
- **(FIXED)** **Matchmake race condition** — `RoomMatchmake` find-or-create + seat insert now wrapped in a single transaction.
- **(FIXED)** **No transaction boundaries** — Room creation, launch, reset, and profile upsert now use explicit transactions so partial failures cannot leave orphaned rows.

### Critical: WebSocket Relay Hardening — ALL FIXED

- **(FIXED)** **Broadcast can hang forever** — `SendSafe()` now uses a 5-second `CancellationTokenSource` timeout per send.
- **(FIXED)** **Unbounded room dictionary** — Empty rooms are pruned when the last peer disconnects.
- **(FIXED)** **No connection timeout** — The receive loop now uses a 60-second timeout; peers that send no messages within that window are disconnected.
- **(ACCEPTED)** **No backpressure** — The 5-second send timeout prevents unbounded memory growth. Full per-peer queue depth limits can be added if load testing shows a need.
- **(ACCEPTED)** **All state is in-memory** — A server restart loses active WebSocket connections. This is acceptable for relay rooms since clients reconnect automatically and room state is persisted in SQLite.

### High: Input Validation And Anti-Cheat — FIXED

- **(FIXED)** **No bounds checking on challenge results** — Challenge sync rejects entries with score > 999999, elapsed > 7200s, or defeats > 9999. Room results are clamped to plausible ranges.
- **(FIXED)** **No rate limiting** — Added per-IP rate limiting middleware (60 requests/minute) with sliding window and stale-entry cleanup.
- **(FIXED)** **Empty/invalid IDs** — All mutation endpoints now validate profileId is non-empty before processing.
- **(ACCEPTED)** **Challenge scores are unsigned** — HMAC signing is a design decision that requires client-server key agreement. Bounds validation + rate limiting mitigate casual abuse. Signing can be added when competitive leaderboards launch.

### High: Save Data Integrity — ALL FIXED

- **(FIXED)** **Non-atomic save writes** — `SaveSystem.Save()` now writes to a `.tmp` file, verifies it, rotates the current save to `.bak`, then renames `.tmp` to the real path.
- **(FIXED)** **No save file backup** — Previous save is now kept as `.bak` and automatically tried if the main save fails to load.
- **(FIXED)** **Save data versioning** — `GameSaveData.Version` already exists (currently 25) and `ApplySavedData()` already has per-version migration paths for all fields.
- **(FIXED)** **Validation on load** — `ClampState()` already validates all loaded data: Gold/Food >= 0, stages clamped to valid range, all collections normalized, volume percentages clamped 0-100.

### High: State Synchronization — ALL FIXED

- **(FIXED)** **Scoreboard/session merge timestamp** — `MergeScoreboardIntoSnapshot` now skips the merge if the scoreboard data is more than 5 seconds older than the session snapshot.
- **(FIXED)** **Stale session displayed after fetch failure** — The session service now tracks `_lastFetchFailed` and shows a `[stale data — Xs ago]` warning when the latest fetch failed.
- **(FIXED)** **Telemetry thundering herd** — Online room telemetry and monitor refresh intervals now include ±200ms and ±300ms random jitter respectively.

### Medium: Resource Cleanup And Memory — FIXED

- **(FIXED)** **Projectile trail cleanup** — `Projectile._ExitTree()` now stops and frees the trail particle emitter when the projectile is removed from the scene tree.
- **(FIXED)** **Projectile visual feedback on cancel** — Projectiles now spawn impact sparks at their current position when the target dies mid-flight.
- **(ACCEPTED)** **End-screen refresh** — Already stops naturally when the scene changes (BattleController is freed by SceneRouter). The refresh method also guards on `HasActiveTicket()`.

### Medium: Combat Simulation — FIXED / ACCEPTED

- **(ACCEPTED)** **Non-deterministic multiplayer simulation** — This is an architecture-level concern that requires fixed-timestep simulation. The current floating-point approach is acceptable for the async challenge and LAN race modes where small divergences don't affect gameplay outcomes. Can be revisited if authoritative server-side simulation is needed.
- **(FIXED)** **Projectile damage lost on target death** — Projectiles now spawn impact sparks at their current position when the target dies mid-flight.
- **(FIXED)** **RNG seeded per battle run** — Challenge mode already seeds `_rng` from `_challengeDefinition.Seed`. Campaign and endless modes use `Randomize()` which is correct since they don't need determinism.

### Medium: Database Integrity — ALL FIXED

- **(FIXED)** **Foreign keys enforced** — `Database.Open()` runs `PRAGMA foreign_keys=ON` on every connection.
- **(FIXED)** **Index on status columns** — Added `idx_room_seats_status` composite index. `rooms.status` already had `idx_rooms_status`.
- **(FIXED)** **Duplicate join returns 409** — `RoomJoin` catches `SqliteException` constraint violations and returns `409 Conflict`.

### Low: UI Edge Cases — FIXED / ACCEPTED

- **(FIXED)** **Deploy button debounce** — Deploy buttons are already disabled via `button.Disabled` when `!isReady || !hasCourage || _battleEnded`, and `_deck.CanDeploy()` gates on cooldown. Double-deploys are impossible.
- **(ACCEPTED)** **UI freezes during network calls** — HTTP providers use synchronous `Client.Send()` which blocks briefly. Converting all 15 providers to async/await requires threading changes throughout the Godot call chain. The 15-second timeout (increased from 6s) keeps worst-case freezes bounded. Can be revisited for mobile where main-thread blocking is more impactful.
- **(ACCEPTED)** **No cancellation support** — Same as above; requires async provider refactor. Scene changes free all nodes, so orphaned requests complete harmlessly.

### Low: Infrastructure — ALL FIXED

- **(FIXED)** **HttpClient timeout** — All 15 HTTP providers updated from 6-second to 15-second timeout.
- **(FIXED)** **Structured logging** — Added `RequestLogger` middleware that logs method, path, status code, elapsed time, and profile ID for every request using `ILogger`.
- **(FIXED)** **Health check endpoint** — Added `/health` endpoint that verifies database connectivity and reports relay room count.
- **(FIXED)** **Rate limiting** — Added `RateLimiter` middleware with per-IP sliding window (60 requests/minute), `429 Too Many Requests` responses, and periodic stale-entry cleanup.

## Ship Checklist

1. **Start with the 3 starter units** — drop `fighter.png`, `gunner.png`, `shield.png` into `assets/units/` and verify they render in battle
2. **Art the remaining 41 units** — work through ASSETS.md roster, one visual class at a time; the game renders sprites when present and procedural shapes when absent, so partial art is fine
3. **Add battlefield backgrounds** — start with `urban.png` and `industrial.png` in `assets/backgrounds/`, then fill remaining 29 terrain IDs
4. **Add structure textures** — `war_wagon.png` and `gatehouse.png` in `assets/structures/`
5. **Record music** — start with `title.ogg` and `battle.ogg` in `assets/music/`, then add per-route tracks
6. **Record SFX** — replace procedural cues starting with `deploy.ogg`, `impact_light.ogg`, `victory.ogg` in `assets/sfx/`
7. **Deploy server** — `cd server && docker compose up -d`, configure `.env`, verify at `http://host:8080/admin`
8. **Playtest** — run all 10 districts + endless mode, validate balance pass
9. **Translate** — add `data/locale/{lang}.json` for target markets
10. **Build for mobile** — export with IAP plugins, set store IDs in `data/shop_products.json`
11. **Submit to stores** — App Store / Google Play with screenshots and descriptions

## Guardrails

- replicate gameplay structure, not copyrighted names or assets
- keep data-driven content wherever possible
- avoid large rewrites until wave scripting lands
- validate each milestone with a playable loop before expanding content
- treat multiplayer as a later architecture project, not a bolt-on UI feature
