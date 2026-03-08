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
  - Shows live convoy progress, unlocked-stage count, resources, next deployment, and active squad summary
  - `Start Campaign`: opens map
  - `Endless Run`: opens the survival-mode prep screen
  - `Reset Progress`: restores stage unlock/resource defaults
- Endless prep:
  - Choose `City Route` or `Harbor Front` for the survival run
  - Pick one temporary opening boon before the run starts
  - Review best endless wave/time, route pressure profile, and active squad stats
  - `Deploy Endless Convoy`: start the endless survival battle
  - `Edit Squad On Map`: return to campaign squad management before the run
- Campaign map:
  - Switch between `City Route` and `Harbor Front` in the map selector
  - Review route banner progress, earned stars, and route-specific stage styling on the map
  - Hover stage nodes for threat and star intel before selecting them
  - Build a 3-card active squad and spend scrap on unit upgrades
  - Select stage node (`1-8`) and press `Deploy`
  - `Back To Title`: return to menu
- Loadout:
  - Review stage objectives, active stage modifiers, scripted wave timing, enemy threat mix, and active squad stats
  - Later stages can swap in custom goals like deploy caps or enemy defeat targets
  - `Deploy Convoy`: start the battle
  - `Back To Map`: adjust deck/upgrades before deploying
- Battle:
  - Pick a unit card (`Brawler`, `Shooter`, `Defender`, `Ranger`, `Raider`, `Marksman`) when unlocked
  - Click anywhere on the battlefield to deploy at the clicked height
  - Card deploys consume courage and enter cooldown
  - Units aggro and fight when enemies enter their aggro box
  - `Shooter` and `Ranger` fire visible projectiles
  - `Spitter` now uses the same projectile attack path from the enemy side
  - `Crusher` and `Overlord` reduce incoming damage
  - `Bloater` explodes on death and damages nearby units
  - `Splitter` breaks into smaller walkers on death
  - Later stages can apply route modifiers like reinforced barricades, armored convoys, courage surges, and swarm density
  - Units now use differentiated silhouettes and size profiles instead of plain circles
  - Hits and attacks flash units, and projectiles now draw trails with impact pulses
  - The bus and barricade now show damage state, shake on impact, and expose clearer base health bars
  - Deploy cards are now color-coded by unit and show clearer ready/recover states
  - Damage now produces floating combat text for melee hits, projectile hits, death bursts, and base hits
  - Deploys, deaths, death bursts, and base hits now emit simple combat feedback pulses
  - The battle HUD previews the next scripted wave countdown and composition
  - The battle HUD also shows live mission objective progress and failed star conditions
  - Endless mode swaps stage goals for escalating survival waves, checkpoint draft upgrades, route-fork choices, fork-specific segment events, projected salvage, retreat-based cash-out, and a temporary opening boon
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
- `scenes/MapMenu.tscn`: stage map/menu
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
- Endless-mode best wave/time and selected route are also persisted in `GameState`.
