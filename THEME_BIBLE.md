# Theme Bible

## Core Direction

This project is shifting from modern zombie-convoy fiction to an original medieval fantasy siege frame.

- Working title: `CROWNROAD: SIEGE OF ASH`
- Battlefield fantasy: `war wagon vs gatehouse`
- Player fantasy: a rune-lit royal caravan pushing from district to district to reopen a fallen kingdom
- Enemy fantasy: undead siege hosts led by grave lords, sappers, heralds, and plague casters
- Magic tone: fast battlefield rites and support sorcery, not large-scale wizard duels

## Factions

- Player faction: `Lantern Caravan`
  - A mobile warband escorting a heavy war wagon between isolated keeps
  - Visual tone: banner colors, wood-and-iron wagon plating, shrine lanterns, field armor, practical siege gear
- Enemy faction: `Rotbound Host`
  - Undead infantry, plague beasts, ritual support units, and grave-lord commanders
  - Visual tone: ruined heraldry, bone armor, plague braziers, ward-breaking tools, ash and blight magic

## Route Mapping

Internal route IDs stay unchanged for save/data stability. Display names shift to:

- `city` -> `King's Road`
  - Outer farms, pilgrim roads, market wards, and bell-tower approaches
- `harbor` -> `Saltwake Docks`
  - Tide-broken quays, chainlift yards, wreck piers, and sea-fort approaches
- `foundry` -> `Emberforge March`
  - Coal spurs, smelter lanes, furnace bridges, and forge-crown battlements
- `quarantine` -> `Ashen Ward`
  - Plague cloisters, purge halls, ritual tents, and sealed vault approaches

## First-Pass Roster Naming

Player roster display names:

- `player_brawler` -> `Swordsman`
- `player_shooter` -> `Archer`
- `player_defender` -> `Shield Knight`
- `player_spear` -> `Spearman`
- `player_ranger` -> `Crossbowman`
- `player_raider` -> `Cavalry Rider`
- `player_mechanic` -> `Siege Engineer`
- `player_marksman` -> `Mage`
- `player_breacher` -> `Halberdier`
- `player_grenadier` -> `Alchemist`
- `player_coordinator` -> `Battle Monk`

Enemy roster display names:

- `enemy_walker` -> `Risen`
- `enemy_runner` -> `Ghoul`
- `enemy_bloater` -> `Rot Hulk`
- `enemy_brute` -> `Grave Brute`
- `enemy_spitter` -> `Blight Caster`
- `enemy_splitter` -> `Bone Nest`
- `enemy_saboteur` -> `Sapper`
- `enemy_howler` -> `Dread Herald`
- `enemy_jammer` -> `Hexer`
- `enemy_crusher` -> `Bone Juggernaut`
- `enemy_boss` -> `Grave Lord`

## Language Guardrails

- Prefer `caravan`, `war wagon`, `gatehouse`, `keep`, `ward`, `host`, `grave`, `blight`, `rite`, and `siege`
- Avoid modern outbreak terms like `infected`, `quarantine`, `convoy`, `blacksite`, `gas station`, and `metro` in new content
- Keep low-level system IDs unchanged unless a later migration explicitly requires it

## This Pass

This pass only locks the core fiction and the highest-visibility display surfaces:

- main menu title and progress summary
- route names and route summaries
- unit display names
- stage names, map names, and top-level stage descriptions

Wave labels, deeper menu copy, and some legacy upgrade/battlefield wording are intentionally deferred to later cleanup passes.
