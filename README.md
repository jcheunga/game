# Game (Godot + C#)

Godot `4.6.1` Mono project with a Dead Ahead-style prototype loop:
main menu -> campaign map/endless prep -> loadout briefing -> freefield battle.

Roadmap and milestone plan: `ROADMAP.md`

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

## Prototype controls

- Main menu:
  - Shows live convoy progress, unlocked-stage count, gold/food, next deployment, and active squad summary
  - `Start Campaign`: opens map
  - `Convoy Shop`: opens the dedicated unit/base upgrade screen
  - `Endless Run`: opens the survival-mode prep screen
  - `Multiplayer Challenge`: opens the async challenge prep screen
  - `Settings`: opens the shared audio/interface settings screen
  - `Reset Progress`: restores stage unlock/resource defaults
- Settings:
  - Adjust persistent SFX level, ambience level, mute state, combat intel visibility, and FPS counter visibility
  - Returns to the menu/prep screen you opened it from
- Multiplayer challenge:
  - Build or import a shareable challenge code like `CH-04-PRS-4821`
  - Browse a daily featured challenge queue generated from unlocked stages
  - Featured boards can lock everyone to the same 3-card convoy for fairer async score races
  - Pin challenge codes to keep rematch boards saved locally
  - `LAN Race` rooms can host the current seeded board over local ENet, sync it to nearby clients, and compare submitted results on a shared room scoreboard
  - Pick a stage and mutator, or roll a new seeded code
  - Run the same seeded encounter locally and compare score on the same code
  - Review the scoring formula, medal targets, and the full post-run score breakdown
  - Launch with a local ghost benchmark that replays the best saved deployment tape for the current board when available
  - Get live ghost split feedback on deploys and a result-screen delta versus the armed benchmark
  - Inspect the latest local run tape, including deck context, stored score split, and deployment timeline for the selected board
  - Review recent local attempts for the current code and recent challenge queue history
  - `Convoy Shop`: adjust the active squad and upgrades before posting a score
- Endless prep:
  - Choose `City Route`, `Harbor Front`, `Foundry Line`, or `Quarantine Wall` for the survival run
  - Pick one temporary opening boon before the run starts
  - Review the route-specific boss checkpoint that arrives every 15 waves and what it pays out
  - Review best endless wave/time, route pressure profile, and active squad stats
  - `Deploy Endless Convoy`: start the endless survival battle
  - `Convoy Shop`: buy upgrades and edit the active squad before the run
- Campaign map:
  - Switch between `City Route`, `Harbor Front`, `Foundry Line`, and `Quarantine Wall` in the map selector
  - Review route banner progress, earned stars, and route-specific stage styling on the map
  - Hover stage nodes for threat and star intel before selecting them
  - Review convoy readiness, active squad summary, and route-specific exploration costs
  - Spend food to explore the next stage and to begin stage deployments
  - `Open Convoy Shop`: jump into the dedicated shop screen
  - Select a stage node and press `Deploy`
- Shop:
  - Review owned units, shop-locked units, and active deck status
  - See each unit’s squad role (`Frontline`, `Recon`, `Support`, `Breach`) and the active deck synergy it contributes to
  - Use the `Action Board` to follow stage-targeted buy/upgrade suggestions
  - Buy new units with gold once their shop stage is explored
  - Upgrade owned units with gold
  - Preview unit stat gains before buying upgrades
  - Upgrade `Hull Plating`, `Convoy Pantry`, `Dispatch Console`, and `Signal Relay` bus systems
  - Review route intel, next exploration costs, and upcoming unit unlocks while shopping
  - Return to the title screen, campaign map, or endless prep from the same screen
  - `Stage Briefing`: jump straight into the selected campaign loadout screen
  - `Back To Title`: return to menu
- Loadout:
  - Review stage objectives, active stage modifiers, stage hazards, scripted wave timing, enemy threat mix, and active squad stats
  - Shared encounter intel now explicitly calls out support pressure like howlers, jammers, saboteurs, and spitters before deployment
  - Live wave intel and campaign node tooltips now keep those support-pressure tags visible during stage selection and battle
  - Battle HUD now also shows the currently active support pressure on the field, including howlers, jammers, saboteurs, spitters, bosses, and any live signal jam timer
  - Async challenge mutators can now also script route-wide signal blackouts, so multiplayer boards can pressure convoy timing without only inflating enemy stats
  - Review active deck synergies before deploying
  - Later stages can swap in custom goals like deploy caps, enemy defeat targets, hazard-hit limits, or signal-jam uptime limits
  - `Deploy Convoy`: spend food and start the battle
  - `Convoy Shop`: adjust squad and upgrades before deploying
  - `Back To Map`: return to route selection
- Battle:
  - Pick a unit card (`Brawler`, `Shooter`, `Defender`, `Ranger`, `Raider`, `Mechanic`, `Marksman`, `Breacher`, `Grenadier`, `Coordinator`) when unlocked
  - Click anywhere on the battlefield to deploy at the clicked height
  - Card deploys consume courage and enter cooldown
  - Pair two cards from the same squad role to activate deck synergies like `Frontline Drill`, `Recon Link`, `Support Mesh`, or `Breach Line`
  - Units aggro and fight when enemies enter their aggro box
  - `Shooter` and `Ranger` fire visible projectiles
  - `Mechanic` can repair the bus during lulls instead of only pushing forward
  - `Breacher` trades tempo for higher barricade/base damage on reinforced late-game stages
  - `Grenadier` adds splash-damage projectile coverage for grouped splitter/support waves
  - `Coordinator` buffs nearby allies with a live attack/speed aura so the convoy can scale through heavier support-heavy late-game waves
  - `Spitter` now uses the same projectile attack path from the enemy side
  - `Saboteur` tries to slip past lane fights and cash in higher bus damage unless the convoy ties it up directly
  - `Howler` buffs nearby infected movement and damage, so late Harbor/Foundry waves now have a real support target to prioritize
  - `Jammer` can disrupt convoy courage flow and spike card recovery, so late Quarantine waves now pressure the economy as well as the front line
  - `Overlord` now uses a live rally call that buffs nearby infected and spawns escorts instead of acting like a pure stat-check boss
  - `Crusher` and `Overlord` reduce incoming damage
  - `Bloater` explodes on death and damages nearby units
  - `Splitter` breaks into smaller walkers on death
  - Later stages can apply route modifiers like reinforced barricades, armored convoys, courage surges, and swarm density
  - Foundry stages can also author timed battlefield hazards like rail surges, heat vents, cinder blasts, and furnace bursts
  - The new `Foundry Line` district adds railyard/smelter/foundry battle palettes plus heavier splitter/crusher route pressure
  - `Quarantine Wall` adds checkpoint/decon/lab/blacksite battle palettes plus spitter/howler/saboteur-heavy toxic checkpoint pressure
  - Units now use differentiated silhouettes and size profiles instead of plain circles
  - Hits and attacks flash units, and projectiles now draw trails with impact pulses
  - The bus and barricade now show damage state, shake on impact, and expose clearer base health bars
  - Deploy cards are now color-coded by unit and show clearer ready/recover states
  - Damage now produces floating combat text for melee hits, projectile hits, death bursts, and base hits
  - Deploys, deaths, death bursts, and base hits now emit simple combat feedback pulses
  - The battle HUD previews the next scripted wave countdown and composition
  - The battle HUD also shows live mission objective progress and failed star conditions
  - Hazard-heavy Foundry missions now also track hazard-hit limits as real star conditions
  - Endless mode swaps stage goals for escalating survival waves, checkpoint draft upgrades, route-fork choices, fork-specific segment events, convoy support events, battlefield events, live segment directives, route-specific contact events, projected salvage, retreat-based cash-out, and a temporary opening boon
  - Every 15th wave is now a deliberate boss checkpoint with route-specific escort pressure and extra clear rewards
  - Endless contact events now render as visible relay, cache, and safehouse moments on the battlefield instead of only zone markers
  - Those endless contacts are now spawned battlefield actors with their own durability, contested state, and failure/survival feedback
  - Eligible melee enemies in endless mode can now break off and explicitly attack contact actors instead of only pressuring them through zone logic
  - Player units in endless mode can now actively support contacts with escort/haul/uplink actions instead of relying only on passive presence repair
  - Ranged units now participate in the same loop too: spitters can pressure contacts at range, and player gunners/snipers can support them with projectile-based uplinks
  - Endless HUD and checkpoint reports now surface contact actor hull, support actions, pressure actions, and contribution totals instead of leaving that loop only in floating text
  - Contact failures now carry route-specific penalties like courage loss, cooldown setbacks, salvage loss, and convoy hull damage instead of only missing a reward
  - Contact successes now also carry route-specific tradeoffs, like earlier surge timing, higher enemy caps, or reduced courage gain until checkpoint
  - Active endless contacts now call in route-specific hostile response packs, so relay/cache/safehouse events create their own reinforcement pressure during the segment
  - Each endless contact now also fires a one-time midpoint convoy assist, like relay uplinks, salvage reserve parts, or safehouse militia support
  - Higher stages can spawn the `Overlord` boss enemy
  - Units and enemies attack the opposing base core repeatedly until it is destroyed
  - Mission stars are evaluated from stage-authored objective rules
  - Win by reducing enemy hive HP to 0
  - Lose if player base HP reaches 0

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
- Stage objective, modifier, and encounter-intel summaries are shared across map, loadout, and battle-facing UI.
- Endless-mode best wave/time, selected route, owned units, base upgrades, and the gold/food economy are also persisted in `GameState`.
