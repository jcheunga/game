# Game (Godot + C#)

Godot `4.6.1` Mono project with a Dead Ahead-style prototype loop:
main menu -> campaign map -> freefield battle.

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
  - `Start Campaign`: opens map
  - `Reset Progress`: restores stage unlock/resource defaults
- Campaign map:
  - Switch between `City Route` and `Harbor Front` in the map selector
  - Select stage node (`1-8`) and press `Deploy`
  - `Back To Title`: return to menu
- Battle:
  - Pick a unit card (`Brawler`, `Shooter`, `Defender`, `Ranger`)
  - Click anywhere on the battlefield to deploy at the clicked height
  - Units aggro and fight when enemies enter their aggro box
  - `Shooter` and `Ranger` fire visible projectiles
  - Higher stages can spawn the `Overlord` boss enemy
  - Units and enemies attack the opposing base core repeatedly until it is destroyed
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
- `scenes/MapMenu.tscn`: stage map/menu
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
