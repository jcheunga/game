# ASSETS

Accurate drop-in asset manifest for the current runtime pipeline.

The game already supports incremental art/audio replacement. Missing files do not break the build or the game. They fall back to procedural visuals or generated audio until you drop authored assets into `assets/`.

## Audit

- In game: open the debug console with `` ` `` and run `assets`
- Repo-side: `cd server && dotnet run -- --test-data ../data`

Both paths print the current asset coverage and the exact IDs still missing.

## Drop Locations

| Asset Type | Drop Location | Notes |
|-----------|---------------|-------|
| Unit sprite sheet | `assets/units/{visual_class}.png` | Optional metadata: `assets/units/{visual_class}.json` |
| Battle terrain | `assets/backgrounds/{terrain_id}.png` | Full-frame battlefield backdrop |
| Structures | `assets/structures/{structure_id}.png` | `war_wagon`, `gatehouse` |
| Particle texture | `assets/particles/{particle_id}.png` | Battle VFX sprite used by CPU particle bursts/trails |
| Screen background | `assets/ui/backgrounds/{screen_id}.png` | Shared full-screen menu background |
| Route-specific screen override | `assets/ui/backgrounds/{screen_id}_{route_id}.png` | Optional override for route-aware screens |
| District map art | `assets/map/backgrounds/{route_id}.png` | Campaign map panel art |
| Unit icon | `assets/ui/icons/units/{unit_id}.png` | Optional fallback: `{visual_class}.png` |
| Spell icon | `assets/ui/icons/spells/{spell_id}.png` | Optional fallback: `{effect_type}.png` |
| Relic icon | `assets/ui/icons/relics/{relic_id}.png` | Armory/loadout card art |
| Reward icon | `assets/ui/icons/rewards/{reward_type}.png` | Currency/reward badge art used across reward screens, main-menu summary chips, and economy HUD strips |
| Meta icon | `assets/ui/icons/meta/{meta_id}.png` | Social, leaderboard, and challenge-status badge art |
| Codex icon | `assets/ui/icons/codex/{entry_id}.png` | Optional fallback if no portrait exists |
| Codex portrait | `assets/ui/portraits/codex/{entry_id}.png` | Detail-screen portrait or splash art |
| Music | `assets/music/{track_id}.ogg` | `.ogg`, `.mp3`, and `.wav` all load |
| SFX override | `assets/sfx/{cue_id}.ogg` | `.ogg`, `.mp3`, and `.wav` all load |

## Fallback Rules

- Missing unit sprites fall back to the procedural silhouettes already used in battle.
- Missing terrain, structure, map, and menu backgrounds fall back to the current color-block/procedural presentation.
- Missing particle textures fall back to the existing built-in Godot particle quads.
- Missing unit/spell/relic/codex/reward images fall back to generated badges with initials, so the UI still stays readable.
- Missing music and SFX fall back to the procedural audio already shipped in the repo.
- You can replace assets incrementally. There is no requirement to finish a whole category in one pass.

## Sizes And Formats

- Unit sheets: PNG, authored facing right
- Unit metadata: JSON, see `assets/units/_example.json`
- Menu, map, and battle backgrounds: PNG, target `1280x720`
- Structures: PNG, authored against transparent background
- Particle textures: PNG with transparency, target `64x64` to `256x256`
- Unit/spell/relic icons: PNG, target `128x128`
- Reward icons: PNG, target `128x128`
- Codex portraits: PNG, target `512x512` or larger portrait crop
- Music/SFX: loopable `ogg` preferred, `mp3`/`wav` also supported

## Screen IDs

These are the generic menu background slots:

- `main_menu`
- `map`
- `loadout`
- `shop`
- `cash_shop`
- `endless`
- `multiplayer`
- `lan_race`
- `arena`
- `battle_summary`
- `bounty`
- `codex`
- `event`
- `expedition`
- `forge`
- `friends`
- `guild`
- `leaderboard`
- `login_calendar`
- `profile`
- `raid`
- `season_pass`
- `skill_tree`
- `settings`
- `tower`

These screens also support optional route-specific variants:

- `map_{route_id}`
- `loadout_{route_id}`
- `shop_{route_id}`
- `endless_{route_id}`
- `multiplayer_{route_id}`

Example:

```text
assets/ui/backgrounds/map.png
assets/ui/backgrounds/map_thornwall.png
assets/ui/backgrounds/loadout_citadel.png
```

## Route IDs

- `city` = King's Road
- `harbor` = Saltwake Docks
- `foundry` = Emberforge March
- `quarantine` = Ashen Ward
- `thornwall` = Thornwall Pass
- `basilica` = Hollow Basilica
- `mire` = Mire of Saints
- `steppe` = Sunfall Steppe
- `gloamwood` = Gloamwood Verge
- `citadel` = Crownfall Citadel

## Terrain IDs

Current campaign coverage is 60 stages across 31 terrain IDs.

- `city`: `highway`, `night`, `urban`
- `harbor`: `industrial`, `shipyard`, `swamp`
- `foundry`: `foundry`, `railyard`, `smelter`
- `quarantine`: `blacksite`, `checkpoint`, `decon`, `lab`
- `thornwall`: `pass`, `shrine`, `watchfort`
- `basilica`: `cathedral`, `ossuary`, `reliquary`
- `mire`: `chapel`, `ferry`, `marsh`
- `steppe`: `grassland`, `siegecamp`, `waystation`
- `gloamwood`: `grove`, `timberroad`, `witchcircle`
- `citadel`: `breachyard`, `bridgefort`, `innerkeep`

## Structure IDs

- `war_wagon`
- `gatehouse`

## Particle IDs

- `particle_soft`
- `particle_deploy`
- `particle_smoke`
- `particle_spark`
- `particle_fire`
- `particle_heal`
- `particle_frost`
- `particle_lightning`
- `particle_arcane`
- `particle_stone`
- `particle_trail`

## Unit Visual Classes

The current roster uses 23 unique `visual_class` IDs. A single sprite sheet can serve multiple units that share the same class.

- `banner`: Banner Knight
- `berserker`: Berserker
- `bloater`: Rot Hulk
- `boss`: Ashen Regent, Bone Pontiff, Dread Sovereign, Gloamwood Witch, Grave Lord, Harrow Tidemaster, Iron Warden, Mire Behemoth, Plague Archon, Plague Monarch, Reliquary Tyrant, Steppe Warlord, Thornwall Chieftain, Tidecaller
- `brute`: Grave Brute
- `crusher`: Bone Juggernaut, Catacomb Giant
- `fighter`: Halberdier, Spearman, Swordsman
- `gunner`: Alchemist, Archer, Crossbowman
- `hound`: War Hound
- `howler`: Dread Herald, Revenant Captain
- `jammer`: Hexer
- `mirror`: Mirror Knight
- `necromancer`: Lich, Necromancer
- `runner`: Ghoul, Tunneler
- `saboteur`: Sapper
- `shield`: Lantern Guard, Shield Knight, Shield Wall
- `siegetower`: Siege Tower
- `skirmisher`: Cavalry Rider, Rogue
- `sniper`: Ballista Crew, Mage
- `spitter`: Blight Caster, Bone Ballista, Plague Engine
- `splitter`: Bone Nest
- `support`: Battle Monk, Siege Engineer, Stormcaller
- `walker`: Risen, Risen Thrall

## Unit Sprite Metadata

If you add `assets/units/{visual_class}.json`, the loader reads:

- `frameWidth`
- `frameHeight`
- `animations.idle`
- `animations.walk`
- `animations.attack`
- `animations.hit`
- `animations.death`
- `animations.deploy`

If no metadata exists, the runtime uses the default row order above.

## UI Icon Slots

Unit icons can be authored either per unit or per shared class:

- `assets/ui/icons/units/{unit_id}.png`
- `assets/ui/icons/units/{visual_class}.png`

Spell icons can be authored either per spell or per shared effect type:

- `assets/ui/icons/spells/{spell_id}.png`
- `assets/ui/icons/spells/{effect_type}.png`

Relic icons are per relic:

- `assets/ui/icons/relics/{relic_id}.png`

Reward icons are per reward type:

- `assets/ui/icons/rewards/{reward_type}.png`

Current reward icon IDs:

- `gold`
- `food`
- `tomes`
- `essence`
- `sigils`
- `shards`
- `relic`
- `season_xp`
- `unit`
- `spell`

Meta icons are per social/competitive status type:

- `assets/ui/icons/meta/{meta_id}.png`

Current meta icon IDs:

- `arena_rating`
- `tower_floor`
- `endless_wave`
- `daily_streak`
- `guild`
- `friends`
- `challenge`
- `members`

Codex detail portraits are per codex entry:

- `assets/ui/portraits/codex/{entry_id}.png`

Optional codex icons can also be authored per entry:

- `assets/ui/icons/codex/{entry_id}.png`

## Music Track IDs

Scene tracks:

- `title`
- `campaign`
- `shop`
- `loadout`
- `endless_prep`
- `multiplayer`

Battle tracks:

- `battle`
- `battle_road`
- `battle_harbor`
- `battle_foundry`
- `battle_quarantine`
- `battle_pass`
- `battle_basilica`
- `battle_mire`
- `battle_steppe`
- `battle_gloamwood`
- `battle_citadel`

## SFX Cue IDs

Core interaction:

- `ui_hover`, `ui_confirm`, `scene_change`, `deploy`

Combat:

- `impact_light`, `impact_heavy`, `bus_hit`, `barricade_hit`, `repair`
- `hazard_warning`, `hazard_strike`, `spell_cast`
- `boss_spawn`, `boss_death`, `victory`, `defeat`

Progression:

- `upgrade_confirm`, `achievement_unlock`, `relic_pickup`

Ambience:

- `ambience_menu`, `ambience_battle`, `ambience_endless`, `ambience_multiplayer`, `ambience_shop`
- `ambience_route_road`, `ambience_route_harbor`, `ambience_route_foundry`, `ambience_route_quarantine`
- `ambience_route_thornwall`, `ambience_route_basilica`, `ambience_route_mire`, `ambience_route_steppe`
- `ambience_route_gloamwood`, `ambience_route_citadel`

## Recommended Handoff Order

1. `assets/units`
2. `assets/backgrounds`
3. `assets/structures`
4. `assets/particles`
5. `assets/ui/backgrounds`
6. `assets/map/backgrounds`
7. `assets/music`
8. `assets/sfx`

That order covers the main campaign loop first: units, battle spaces, combat VFX, title/map/loadout/results, then audio polish.
