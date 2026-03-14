# Game (Godot + C#)

Godot `4.6.1` Mono project with a Dead Ahead-style structure and an original medieval fantasy siege wrapper:
main menu -> campaign map/endless prep -> loadout briefing -> freefield battle.

Current fiction lock: `Lantern Caravan` vs the `Rotbound Host`, framed as `war wagon vs gatehouse`.

Roadmap and milestone plan: `ROADMAP.md`
Theme reference: `THEME_BIBLE.md`
Campaign target reference: `CAMPAIGN_PLAN.md`

## Requirements

- Godot Mono build (you already have `godot` in PATH)
- .NET SDK 8+

## Run

```bash
godot --editor --path .
```

Or run headless:

```bash
godot --headless --path . --quit
```

## Build C# solution

```bash
godot --headless --path . --build-solutions --quit
```

## Smoke tests

Run the full online-room regression suite sequentially:

```bash
bash scripts/smoke/http_online_room_suite.sh
```

This covers the HTTP room providers plus the local room-session scope, stale-seat recovery, and ticket-swap regressions without parallel build-artifact contention.

Run the full HTTP multiplayer/backend regression suite:

```bash
bash scripts/smoke/http_multiplayer_backend_suite.sh
```

This adds player-profile sync, challenge sync, remote leaderboard/feed coverage, and the full online-room suite.

Run the full multiplayer stack regression suite:

```bash
bash scripts/smoke/multiplayer_stack_suite.sh
```

This runs the LAN headless race smoke plus the full HTTP multiplayer/backend suite.

## Prototype controls

- Main menu:
  - Shows live caravan progress, unlocked-stage count, authored campaign progress, gold/food, next deployment, and active squad summary
  - `Start Campaign`: opens map
  - `Caravan Armory`: opens the dedicated unit/base upgrade screen
  - `Endless Run`: opens the survival-mode prep screen
  - `Multiplayer Challenge`: opens the async challenge prep screen
  - `Settings`: opens the shared audio/interface settings screen
  - `Reset Progress`: restores stage unlock/resource defaults
- Settings:
  - Adjust persistent SFX level, ambience level, mute state, combat intel visibility, and FPS counter visibility
  - Refresh the current player profile against the active local/HTTP multiplayer provider and review cached auth/session status
  - Returns to the menu/prep screen you opened it from
- Multiplayer challenge:
  - Build or import a shareable challenge code like `CH-04-PRS-4821`
  - Browse a daily featured challenge queue generated from unlocked stages
  - Featured boards can lock everyone to the same 3-card squad for fairer async score races
  - Pin challenge codes to keep rematch boards saved locally
  - `Refresh Online` now also pulls a provider-backed internet room directory, so the multiplayer screen can preview remote room boards before full internet room join exists
  - `Refresh Online` now also refreshes the provider-backed player profile/auth snapshot used by the internet multiplayer stack
  - `Quick Match` now negotiates a provider-backed room seat for the currently selected board and immediately adopts the returned join ticket into the room monitor flow
  - `Host Online Room` now publishes the selected async board through a provider-backed room-host flow, adopts the returned host seat locally, and injects that hosted room back into the cached room directory
  - Online room listings now also support a provider-backed `Request Join` step, so the client can negotiate a backend join ticket and relay hint before real internet room transport is built
  - Room join, host-seat adoption, and quick match now also arm the selected board and locked/shared deck locally from the negotiated room ticket, so internet room prep no longer depends on a separate manual board-load step
  - Once the selected board is armed from a room ticket, the main deploy button now reflects joined-room state directly, so internet room races wait on backend launch/countdown instead of behaving like a separate manual challenge start
  - Once a join ticket exists, `Refresh Online` now also pulls a provider-backed room session snapshot so the multiplayer screen can show current runners, ready state, and race-monitor text for that backend room
  - Joined internet rooms now also expose provider-backed room action controls, so the client can toggle ready state and then repoll the backend room snapshot without needing real internet room transport yet
  - Joined internet rooms now also support menu-side auto refresh, so the room monitor and scoreboard can keep updating while you wait in multiplayer prep without constant manual refresh clicks
  - Internet room seats now track ticket expiry, warn when a seat has gone stale, and expose a direct `Recover Seat` path that first tries to rejoin the original room before falling back to quick match
  - Active internet room seats now also support provider-backed lease refresh, so joined-room auto refresh and mobile resume recovery can extend a healthy seat before it expires instead of treating every long prep wait as a stale-ticket failure
  - Hosted internet rooms now also expose a provider-backed `Launch Online Room` control, so the host path can push the backend room into countdown state before real internet room transport exists
  - Hosted internet rooms now also expose a provider-backed rematch reset control, so finished room rounds can return to prep instead of staying stuck on a submitted board
  - Joined or hosted internet rooms now also expose a provider-backed `Leave Online Room` control, so backend room state and local cached room/session/result data can be cleared cleanly without replacing it through another join ticket
  - Async challenge clears, failures, and retreats can now submit provider-backed room results when the selected board matches the active internet room ticket
  - Joined internet rooms now also cache a provider-backed room scoreboard, so multiplayer prep can review shared room standings alongside the room monitor
  - Joined internet room challenge runs now also stream provider-backed live telemetry heartbeats, and the multiplayer screen surfaces that cached telemetry provider status next to the room monitor and scoreboard
  - Joined internet rooms now also expose a provider-backed moderation/report hook with selectable reasons, so the current room or top remote runner can be flagged without waiting for the final store/backend pass
  - Internet room battles now return through the room flow on the end screen, so submitted room results no longer offer a direct retry path that bypasses backend rematch/reset state
  - If an internet room has already entered backend countdown, battle now respects that launch sync with an in-battle start barrier instead of beginning simulation immediately on scene load
  - During live internet-room races, battle now keeps polling the joined-room session snapshot and shows a compact room monitor in challenge intel, so peer race state remains visible mid-run
  - Joined-room session snapshots now carry structured per-peer race telemetry, and battle uses that to show a compact room-pace summary instead of relying only on raw monitor strings
  - Submitted internet-room runners now also carry provisional score/rank in the shared room snapshot, so room pace and monitor text can keep reflecting standings after the first clears land
  - Room scoreboard refreshes now fold cached standings back into the joined-room session snapshot automatically, so room monitor consumers do not have to wait for a second session poll to see posted ranks
  - A shared app-lifecycle service now pauses online-room traffic while the app is backgrounded or unfocused and runs profile/room recovery on resume, which gives the mobile internet stack an explicit backgrounding path instead of relying on accidental recovery
  - Internet room battle end screens now keep refreshing the joined-room monitor and shared standings, so posted room results remain visible without immediately returning to multiplayer prep
  - `Refresh Online` now pulls both a cached remote leaderboard for the selected challenge code and a provider-backed remote featured feed, so backend-authored challenge boards can sit alongside the local daily queue
  - `LAN Race` rooms can host the current seeded board over local ENet, sync it to nearby clients, and compare submitted results on a shared room scoreboard
  - LAN rooms now also track per-peer ready state and block launch until the synced board is armed on every connected machine
  - LAN rooms now surface a dedicated launch-readiness panel that names the active runner pool, deck blockers, ready blockers, and spectators instead of leaving launch state implicit
  - LAN race end screens now route back through the room-rematch flow instead of defaulting to a solo async retry path
  - Caravan call signs are now persisted in Settings and used across LAN room labels, ready states, and scoreboards
  - LAN room summaries now also show live peer race state, so the lobby can tell who is still in prep, who is currently in battle, and who has already submitted a result
  - Player-deck LAN boards now also sync each peer's current squad and active deck synergy into the room summary before launch
  - Changing cards in a LAN room now clears that runner's ready state, and player-deck boards will not launch until every synced runner has a full 3-card squad
  - Active LAN races now also stream low-bandwidth live telemetry back into the room, so racing peers show current time, hull, and defeats before the final scoreboard submission lands
  - The LAN screen now has a dedicated race monitor panel separate from the final scoreboard, so live progress and completed submissions stay readable during a room race
  - LAN races now also use a shared load barrier and countdown, so combat does not start until every runner has finished loading into battle
  - If a runner disconnects during a LAN race, the room now preserves a `DC` result entry instead of silently dropping them from the monitor and scoreboard
  - LAN challenge end screens now stay live after your own run finishes, updating with room-monitor and scoreboard changes as other runners finish or disconnect
  - LAN rooms now also keep cumulative session standings across rematches, and those standings are shown both in the room panel and on the live LAN challenge end screen
  - Late joiners now enter an in-progress LAN race as spectators instead of being counted as missing competitors until the next rematch/reset
  - Hosts can no longer rebroadcast or relaunch a LAN board while a round is in flight, and the room now switches into an explicit rematch-ready state once all active submissions are in
  - Spectators now stay out of the next launch until they explicitly ready for the rematch, so late joiners do not get auto-pulled into the following race
  - Completed runners now also leave the stale `submitted` state once they re-arm for a rematch, so the room monitor reflects the real rematch pool instead of last-round status
  - Host launch attempts now fail with named blockers, and the room refuses zero-runner rematches instead of silently launching an empty round
  - `scripts/smoke/lan_race_smoke.sh` now runs a real two-instance headless LAN smoke test, with `LanSmokeDirector` driving host/join/ready/launch/result submission over ENet and `--save-suffix=...` keeping smoke runs off the main save slot
  - LAN room presentation now runs through a shared multiplayer room snapshot/formatter layer, so future internet-backed rooms can reuse the same room, readiness, and monitor model instead of starting over
  - Async challenge results now also queue into a persistent internet-ready outbox with a stable player profile ID, so future mobile/backend submission has a real client-side packet path instead of starting from local history only
  - The multiplayer screen now includes a manual `Flush Outbox` path backed by a provider-based sync service, with the default `Local Journal Stub` buffering batch envelopes so queued challenge packets can be exercised end-to-end before a real internet backend exists
  - Settings now also expose the sync provider mode, optional HTTP endpoint, and auto-flush toggle so a future mobile/backend provider can be enabled without changing the queue UI
  - Settings now also surface a provider-backed player profile/auth status block, so callsign/profile sync can be exercised before full internet account systems exist
  - `scripts/smoke/http_player_profile_sync_smoke.sh` now drives the HTTP player-profile provider end to end against a local stub server, so the backend profile/auth seam is smoke-tested before room matchmaking depends on it
  - `scripts/smoke/http_challenge_sync_smoke.sh` now drives the HTTP sync provider end to end against a local stub server, so the online challenge-submission path is smoke-tested instead of only compile-checked
  - `scripts/smoke/http_challenge_leaderboard_smoke.sh` now drives the HTTP leaderboard provider end to end against a local stub server, so the remote board fetch path is also smoke-tested
  - `scripts/smoke/http_challenge_feed_smoke.sh` now drives the HTTP challenge-feed provider end to end against a local stub server, so backend-authored board discovery is also smoke-tested
  - `scripts/smoke/http_online_room_directory_smoke.sh` now drives the HTTP room-directory provider end to end against a local stub server, so internet room discovery is also smoke-tested before real room join ships
  - `scripts/smoke/http_online_room_matchmake_smoke.sh` now drives the HTTP room-matchmake provider end to end against a local stub server, so backend quick-join seat negotiation is also smoke-tested before full internet room transport ships
  - `scripts/smoke/http_online_room_join_smoke.sh` now drives the HTTP room-join provider end to end against a local stub server, so backend join-ticket negotiation is also smoke-tested before real internet room transport ships
  - `scripts/smoke/http_online_room_lease_smoke.sh` now drives the HTTP room-seat lease provider end to end against a local stub server, so backend seat-refresh handoff is also smoke-tested before long-lived mobile room sessions depend on it
  - `scripts/smoke/http_online_room_session_smoke.sh` now drives the HTTP room-session provider end to end against a local stub server, so backend room-lobby polling is also smoke-tested before real internet room transport ships
  - `scripts/smoke/http_online_room_action_smoke.sh` now drives the HTTP room-action provider end to end against a local stub server, so backend ready-state actions are also smoke-tested before real internet room transport ships
  - `scripts/smoke/http_online_room_create_smoke.sh` now drives the HTTP room-create provider end to end against a local stub server, so backend room-host publish is also smoke-tested before real internet room transport ships
  - `scripts/smoke/http_online_room_launch_smoke.sh` now drives the HTTP room-launch action end to end against a local stub server, so backend room countdown/launch handoff is also smoke-tested before real internet room transport ships
  - `scripts/smoke/http_online_room_reset_smoke.sh` now drives the HTTP room-reset action end to end against a local stub server, so backend rematch/reset handoff is also smoke-tested before real internet room transport ships
  - `scripts/smoke/http_online_room_leave_smoke.sh` now drives the HTTP room-leave action end to end against a local stub server, so backend room-exit handoff is also smoke-tested before real internet room transport ships
  - `scripts/smoke/http_online_room_result_smoke.sh` now drives the HTTP room-result provider end to end against a local stub server, so backend room-result submission is also smoke-tested before real internet room transport ships
  - `scripts/smoke/http_online_room_scoreboard_smoke.sh` now drives the HTTP room-scoreboard provider end to end against a local stub server, so backend room standings fetch is also smoke-tested before real internet room transport ships
  - `scripts/smoke/http_online_room_telemetry_smoke.sh` now drives the HTTP room-telemetry provider end to end against a local stub server, so backend live race-monitor heartbeats are also smoke-tested before real internet room transport ships
  - `scripts/smoke/http_online_room_report_smoke.sh` now drives the HTTP room-report provider end to end against a local stub server, so backend moderation/report intake is also smoke-tested before internet room play depends on it
  - Pick a stage and mutator, or roll a new seeded code
  - Run the same seeded encounter locally and compare score on the same code
  - Review the scoring formula, medal targets, and the full post-run score breakdown
  - Launch with a local ghost benchmark that replays the best saved deployment tape for the current board when available
  - Get live ghost split feedback on deploys and a result-screen delta versus the armed benchmark
  - Inspect the latest local run tape, including deck context, stored score split, and deployment timeline for the selected board
  - Review recent local attempts for the current code and recent challenge queue history
  - `Caravan Armory`: adjust the active squad and upgrades before posting a score
- Endless prep:
  - Choose `King's Road`, `Saltwake Docks`, `Emberforge March`, `Ashen Ward`, `Thornwall Pass`, `Hollow Basilica`, `Mire of Saints`, `Sunfall Steppe`, `Gloamwood Verge`, or `Crownfall Citadel` for the survival run
  - Pick one temporary opening boon before the run starts
  - Review the route-specific boss checkpoint that arrives every 15 waves and what it pays out
  - Review best endless wave/time, route pressure profile, and active squad stats
  - `Begin Endless March`: start the endless survival battle
  - `Caravan Armory`: buy upgrades and edit the active squad before the run
- Campaign map:
  - Switch between `King's Road`, `Saltwake Docks`, `Emberforge March`, `Ashen Ward`, `Thornwall Pass`, `Hollow Basilica`, `Mire of Saints`, `Sunfall Steppe`, `Gloamwood Verge`, and `Crownfall Citadel` in the map selector
  - Review route banner progress, district buildout status, earned stars, and route-specific stage styling on the map
  - Route, map, and stage panels now tint to the selected district so each front reads like its own campaign space instead of a shared generic menu
  - Hover stage nodes for threat and star intel before selecting them
  - Review caravan readiness, active squad summary, and route-specific exploration costs
  - Spend food to explore the next stage and to begin stage deployments
  - `Open Caravan Armory`: jump into the dedicated shop screen
  - Select a stage node and press `Deploy`
- Shop:
  - Review owned units, shop-locked units, and active deck status
  - Review owned spells, spell archive unlocks, and the active magic deck
  - See each unit’s squad role (`Frontline`, `Recon`, `Support`, `Breach`) and the active deck synergy it contributes to
  - Use the `Action Board` to follow stage-targeted buy/upgrade suggestions
  - The `Action Board` now also reacts to authored battlefield events like ritual holds, relic escorts, and breach charges
  - Buy new units with gold once their shop stage is explored
  - Scribe new spells with gold once their route stage is explored
  - Equip up to 2 active spell cards alongside the 3-card squad
  - Upgrade owned units with gold
  - Preview unit stat gains before buying upgrades
  - Upgrade `War Wagon Plating`, `Caravan Stores`, `March Drum`, and `Rune Beacon`
  - Review route intel, battlefield events, next exploration costs, and upcoming unit/spell unlocks while shopping
  - Return to the title screen, campaign map, or endless prep from the same screen
  - `Stage Briefing`: jump straight into the selected campaign loadout screen
  - `Back To Title`: return to menu
- Loadout:
  - Review stage objectives, active stage modifiers, stage hazards, scripted wave timing, enemy threat mix, and active squad stats
  - Battlefield mission events now get the same dedicated briefing treatment on the map, loadout screen, and armory route-intel panel
  - Shared encounter intel now explicitly calls out support pressure like howlers, jammers, saboteurs, and spitters before deployment
  - Live wave intel and campaign node tooltips now keep those support-pressure tags visible during stage selection and battle
  - Battle HUD now also shows the currently active support pressure on the field, including howlers, jammers, saboteurs, spitters, bosses, and any live signal jam timer
  - Async challenge mutators can now also script route-wide signal blackouts, so multiplayer boards can pressure caravan timing without only inflating enemy stats
  - Review active deck synergies before deploying
  - Review the active magic deck before deploying, including spell courage costs, cooldowns, and effect summaries
  - Later stages can swap in custom goals like deploy caps, enemy defeat targets, hazard-hit limits, or signal-jam uptime limits
  - `Deploy Caravan`: spend food and start the battle
  - `Caravan Armory`: adjust squad and upgrades before deploying
  - `Back To Map`: return to route selection
- Battle:
  - Pick a unit card (`Swordsman`, `Archer`, `Shield Knight`, `Crossbowman`, `Cavalry Rider`, `Siege Engineer`, `Mage`, `Halberdier`, `Alchemist`, `Battle Monk`) when unlocked
  - Pick a spell card (`Fireball`, `Heal`, `Frost Burst`, `Lightning Strike`, `Barrier Ward`) when unlocked and equipped
  - Click anywhere on the battlefield to deploy at the clicked height or target a spell cast
  - Unit deploys and spell casts both consume courage and enter cooldown
  - Pair two cards from the same squad role to activate deck synergies like `Frontline Drill`, `Recon Link`, `Support Mesh`, or `Breach Line`
  - `Fireball` clears clustered pushes, `Heal` restores allies and war wagon hull, `Frost Burst` slows packed lanes, `Lightning Strike` deletes priority backliners, and `Barrier Ward` hardens allied units in a threatened lane
  - Units aggro and fight when enemies enter their aggro box
  - `Archer` and `Crossbowman` fire visible projectiles
  - `Siege Engineer` can repair the war wagon during lulls instead of only pushing forward
  - `Halberdier` trades tempo for higher gate/base damage on reinforced late-game stages
  - `Alchemist` adds splash-damage projectile coverage for grouped split-brood/support waves
  - `Battle Monk` buffs nearby allies with a live attack/speed aura so the caravan can scale through heavier support-heavy late-game waves
  - `Blight Caster` now uses the same projectile attack path from the enemy side
  - `Sapper` tries to slip past lane fights and cash in higher war wagon damage unless the caravan ties it up directly
  - `Dread Herald` buffs nearby undead movement and damage, so late Saltwake/Emberforge waves now have a real support target to prioritize
  - `Hexer` can disrupt caravan courage flow and spike card recovery, so late Ashen Ward waves now pressure the economy as well as the front line
  - `Grave Lord` now uses a live rally call that buffs nearby undead and spawns escorts instead of acting like a pure stat-check boss
  - `Bone Juggernaut` and `Grave Lord` reduce incoming damage
  - `Rot Hulk` explodes on death and damages nearby units
  - `Bone Nest` breaks into smaller risen on death
  - Later stages can apply route modifiers like reinforced gates, armored caravans, courage surges, and swarm density
  - Foundry stages can also author timed battlefield hazards like rail surges, heat vents, cinder blasts, and furnace bursts
  - Later campaign stages can now also author battlefield mission events like ritual sites, relic escorts, and gate breaches, with live progress, success swings, and failure penalties
  - `Emberforge March` adds railyard/smelter/foundry battle palettes plus heavier splitter/crusher route pressure
  - `Ashen Ward` adds checkpoint/cloister/leechcourt/vault battle palettes plus spitter/howler/saboteur-heavy purge-route pressure
  - `Thornwall Pass` adds pass/shrine/watchfort battle palettes plus faster runner/howler/sapper pressure under mountain hazards
  - `Hollow Basilica` adds cathedral/ossuary/reliquary battle palettes plus splitter/spitter/hexer pressure around relic choke points
  - `Mire of Saints` adds marsh/chapel/ferry battle palettes plus attrition-heavy bloater/splitter/spitter pressure under bog hazards
  - `Sunfall Steppe` adds grassland/waystation/siegecamp battle palettes plus fast rider/howler/sapper pressure under fire hazards
  - `Gloamwood Verge` adds grove/witchcircle/timberroad battle palettes plus ambush-heavy runner/hexer/sapper pressure under snare hazards
  - `Crownfall Citadel` adds bridgefort/breachyard/innerkeep battle palettes plus mixed endgame command pressure under keep hazards
  - Units now use differentiated silhouettes and size profiles instead of plain circles
  - Hits and attacks flash units, and projectiles now draw trails with impact pulses
  - The war wagon and gatehouse now show damage state, shake on impact, and expose clearer base health bars
  - Deploy cards are now color-coded by unit and show clearer ready/recover states
  - Damage now produces floating combat text for melee hits, projectile hits, death bursts, and base hits
  - Deploys, deaths, death bursts, and base hits now emit simple combat feedback pulses
  - The battle HUD previews the next scripted wave countdown and composition
  - The battle HUD also shows live mission objective progress and failed star conditions
  - Active campaign mission events now show their own battlefield-event progress in combat instead of living only in stage text
  - Battle HUD panels now inherit the active route colors, and combat opens with a route/stage briefing banner that keeps the live mission or mutator state visible
  - The war wagon and gatehouse now carry route heraldry and stronger siege silhouettes instead of reading like plain abstract blocks
  - Hazard-heavy Foundry missions now also track hazard-hit limits as real star conditions
  - Endless mode swaps stage goals for escalating survival waves, checkpoint draft upgrades, route-fork choices, fork-specific segment events, caravan support events, battlefield events, live segment directives, route-specific contact events, projected spoils, retreat-based reward banking, and a temporary opening boon
  - Every 15th wave is now a deliberate boss checkpoint with route-specific escort pressure and extra clear rewards
  - Endless contact events now render as visible relay, cache, and safehouse moments on the battlefield instead of only zone markers
  - Those endless contacts are now spawned battlefield actors with their own durability, contested state, and failure/survival feedback
  - Eligible melee enemies in endless mode can now break off and explicitly attack contact actors instead of only pressuring them through zone logic
  - Player units in endless mode can now actively support contacts with escort/haul/uplink actions instead of relying only on passive presence repair
  - Ranged units now participate in the same loop too: spitters can pressure contacts at range, and player gunners/snipers can support them with projectile-based uplinks
  - Endless HUD and checkpoint reports now surface contact actor hull, support actions, pressure actions, and contribution totals instead of leaving that loop only in floating text
  - Contact failures now carry route-specific penalties like courage loss, cooldown setbacks, lost spoils, and caravan hull damage instead of only missing a reward
  - Contact successes now also carry route-specific tradeoffs, like earlier surge timing, higher enemy caps, or reduced courage gain until checkpoint
  - Active endless contacts now call in route-specific hostile response packs, so relay/cache/safehouse events create their own reinforcement pressure during the segment
  - Each endless contact now also fires a one-time midpoint caravan assist, like relay uplinks, reserve stores, or safehouse militia support
  - Higher stages can spawn the `Grave Lord` boss enemy
  - Units and enemies attack the opposing base core repeatedly until it is destroyed
  - Mission stars are evaluated from stage-authored objective rules
  - Win by reducing enemy gatehouse HP to 0
  - Lose if war wagon hull reaches 0

## Project layout

- `project.godot`: project config, startup scene, and autoload registration
- `Game.csproj`: C# project for Godot
- `Game.sln`: solution file for Rider/Visual Studio
- `data/units.json`: unit definitions and costs
- `data/stages.json`: stage progression/map/combat tuning
- `data/combat_config.json`: global combat/battlefield/economy tuning
- `scenes/MainMenu.tscn`: title screen
- `scenes/EndlessMenu.tscn`: endless-mode prep and route selection
- `scenes/MultiplayerMenu.tscn`: async multiplayer challenge prep and code entry
- `scenes/SettingsMenu.tscn`: shared audio/interface settings screen
- `scenes/MapMenu.tscn`: stage map/menu
- `scenes/ShopMenu.tscn`: dedicated unit/base shop screen
- `scenes/LoadoutMenu.tscn`: pre-battle squad and mission briefing
- `scenes/Battle.tscn`: freefield combat scene
- `scenes/autoload/`: autoload root scenes
- `scripts/core/`: app services (`SaveSystem`, `GameState`, `SceneRouter`)
- `scripts/data/`: data models and loader (`GameData`)
- `scripts/ui/`: menu/map UI controllers and components
- `scripts/combat/`: battle loop, unit model, and combat runtime stats

## Architecture notes

- Game flow is routed through `SceneRouter` (autoload).
- Progression/resources are centralized in `GameState` (autoload) and persisted by `SaveSystem` to `user://savegame.json`.
- Procedural UI/battle audio is centralized in `AudioDirector` (autoload), which synthesizes lightweight cues and scene ambience without external assets.
- Shared audio/interface options are surfaced through `SettingsMenu` and persisted in `GameState`.
- Unit and stage tuning are data-driven from JSON files in `data/` and loaded through `GameData`.
- Global combat pacing/limits (spawn pressure, enemy cap, base approach distance, courage economy, etc.) is in `data/combat_config.json`.
- Stage objective, modifier, mission-event, and encounter-intel summaries are shared across map, loadout, and battle-facing UI.
- Endless-mode best wave/time, selected route, owned units, base upgrades, and the gold/food economy are also persisted in `GameState`.
