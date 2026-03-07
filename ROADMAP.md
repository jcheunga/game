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

## What We Have Already Done

The project already has a working vertical slice:

- Godot 4 + C# project bootstrapped and runnable
- title screen, campaign map, and battle scene flow
- save/load for progression state
- stage unlock progression
- scrap/fuel rewards
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
- persistent team composition data
- better rewards and stage completion structure

### Milestone 3: Mission Structure

Objective: move from "sandbox stage" to "campaign mission".

- scripted enemy wave sets
- timed events and boss entries
- alternate mission goals
- star ratings or bonus objectives
- clearer map progression by district/route

### Milestone 4: Presentation Pass

Objective: replace prototype abstraction with readable game feedback.

- proper sprites/animations
- hit, death, projectile, and deploy effects
- stronger HUD and battle readability
- map and menu polish
- audio pass

### Milestone 5: Content Expansion And Balance

Objective: scale once the systems are trustworthy.

- more units and zombie archetypes
- more maps and districts
- stage modifiers and challenge content
- balance passes for courage economy, cooldowns, and wave pacing

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
- added persistent unit upgrades funded by scrap and applied in battle
- added roster unlock rules and expanded the player unit pool

## Recommended Build Order After This Commit

1. split battle responsibilities into smaller systems inside `BattleController`
2. add a pre-battle loadout screen or stronger squad management flow
3. deepen mission objectives beyond the current basic star rules
4. add more enemy abilities/archetypes to match the larger player roster
5. replace placeholder combat visuals with authored assets and effects

## Guardrails

- replicate gameplay structure, not copyrighted names or assets
- keep data-driven content wherever possible
- avoid large rewrites until wave scripting lands
- validate each milestone with a playable loop before expanding content
