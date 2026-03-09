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
  - `Reset Progress`: restores stage unlock/resource defaults
- Multiplayer challenge:
  - Build or import a shareable challenge code like `CH-04-PRS-4821`
  - Pick a stage and mutator, or roll a new seeded code
  - Run the same seeded encounter locally and compare score on the same code
  - Review recent local attempts for the current code and recent challenge queue history
  - `Convoy Shop`: adjust the active squad and upgrades before posting a score
- Endless prep:
  - Choose `City Route`, `Harbor Front`, or `Foundry Line` for the survival run
  - Pick one temporary opening boon before the run starts
  - Review best endless wave/time, route pressure profile, and active squad stats
  - `Deploy Endless Convoy`: start the endless survival battle
  - `Convoy Shop`: buy upgrades and edit the active squad before the run
- Campaign map:
  - Switch between `City Route`, `Harbor Front`, and `Foundry Line` in the map selector
  - Review route banner progress, earned stars, and route-specific stage styling on the map
  - Hover stage nodes for threat and star intel before selecting them
  - Review convoy readiness, active squad summary, and route-specific exploration costs
  - Spend food to explore the next stage and to begin stage deployments
  - `Open Convoy Shop`: jump into the dedicated shop screen
  - Select stage node (`1-8`) and press `Deploy`
- Shop:
  - Review owned units, shop-locked units, and active deck status
  - Use the `Action Board` to follow stage-targeted buy/upgrade suggestions
  - Buy new units with gold once their shop stage is explored
  - Upgrade owned units with gold
  - Preview unit stat gains before buying upgrades
  - Upgrade `Hull Plating`, `Convoy Pantry`, and `Dispatch Console` bus systems
  - Review route intel, next exploration costs, and upcoming unit unlocks while shopping
  - Return to the title screen, campaign map, or endless prep from the same screen
  - `Stage Briefing`: jump straight into the selected campaign loadout screen
  - `Back To Title`: return to menu
- Loadout:
  - Review stage objectives, active stage modifiers, stage hazards, scripted wave timing, enemy threat mix, and active squad stats
  - Later stages can swap in custom goals like deploy caps, enemy defeat targets, or hazard-hit limits
  - `Deploy Convoy`: spend food and start the battle
  - `Convoy Shop`: adjust squad and upgrades before deploying
  - `Back To Map`: return to route selection
- Battle:
  - Pick a unit card (`Brawler`, `Shooter`, `Defender`, `Ranger`, `Raider`, `Mechanic`, `Marksman`, `Breacher`) when unlocked
  - Click anywhere on the battlefield to deploy at the clicked height
  - Card deploys consume courage and enter cooldown
  - Units aggro and fight when enemies enter their aggro box
  - `Shooter` and `Ranger` fire visible projectiles
  - `Mechanic` can repair the bus during lulls instead of only pushing forward
  - `Breacher` trades tempo for higher barricade/base damage on reinforced late-game stages
  - `Spitter` now uses the same projectile attack path from the enemy side
  - `Saboteur` tries to slip past lane fights and cash in higher bus damage unless the convoy ties it up directly
  - `Crusher` and `Overlord` reduce incoming damage
  - `Bloater` explodes on death and damages nearby units
  - `Splitter` breaks into smaller walkers on death
  - Later stages can apply route modifiers like reinforced barricades, armored convoys, courage surges, and swarm density
  - Foundry stages can also author timed battlefield hazards like rail surges, heat vents, cinder blasts, and furnace bursts
  - The new `Foundry Line` district adds railyard/smelter/foundry battle palettes plus heavier splitter/crusher route pressure
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
- Unit and stage tuning are data-driven from JSON files in `data/` and loaded through `GameData`.
- Global combat pacing/limits (spawn pressure, enemy cap, base approach distance, courage economy, etc.) is in `data/combat_config.json`.
- Stage objective, modifier, and encounter-intel summaries are shared across map, loadout, and battle-facing UI.
- Endless-mode best wave/time, selected route, owned units, base upgrades, and the gold/food economy are also persisted in `GameState`.
