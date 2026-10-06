# Theme bible

## Core direction

The game uses an original medieval fantasy siege frame.

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

## Zones

Internal route IDs keep their original names for save and data stability. The
caravan crosses ten zones in this order (titles and summaries in
`scripts/core/RouteCatalog.cs`):

- `city` -> `King's Road`
  - Outer farms, pilgrim roads, market wards, and bell-tower approaches
- `harbor` -> `Saltwake Docks`
  - Tide-broken quays, chainlift yards, wreck piers, and sea-fort approaches
- `foundry` -> `Emberforge March`
  - Coal spurs, smelter lanes, furnace bridges, and forge-crown battlements
- `quarantine` -> `Ashen Ward`
  - Plague cloisters, purge halls, ritual tents, and sealed vault approaches
- `thornwall` -> `Thornwall Pass`
  - Cliff roads, avalanche shrines, and watch forts
- `basilica` -> `Hollow Basilica`
  - Ruined cathedrals, ossuary courts, and reliquary vaults
- `mire` -> `Mire of Saints`
  - Bog causeways, drowned chapels, and plague ferries
- `steppe` -> `Sunfall Steppe`
  - Burned waystations, open grassland forts, and roaming siege camps
- `gloamwood` -> `Gloamwood Verge`
  - Thorn groves, witch circles, and haunted timber roads
- `citadel` -> `Crownfall Citadel`
  - Bridge forts, breach yards, and the inner keep

## Roster names

Display names come from `DisplayName` in `data/units.json`.

Player roster (19):

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
- `player_hound` -> `War Hound`
- `player_banner` -> `Banner Knight`
- `player_necromancer` -> `Necromancer`
- `player_rogue` -> `Rogue`
- `player_berserker` -> `Berserker`
- `player_lantern_guard` -> `Lantern Guard`
- `player_ballista` -> `Ballista Crew`
- `player_stormcaller` -> `Stormcaller`

Enemy roster (19):

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
- `enemy_shieldwall` -> `Shield Wall`
- `enemy_lich` -> `Lich`
- `enemy_siegetower` -> `Siege Tower`
- `enemy_mirror` -> `Mirror Knight`
- `enemy_tunneler` -> `Tunneler`
- `enemy_boneballista` -> `Bone Ballista`
- `enemy_catacomb_giant` -> `Catacomb Giant`
- `enemy_revenant_captain` -> `Revenant Captain`
- `enemy_plague_engine` -> `Plague Engine`

Bosses (14), with the zone each one leads:

- `enemy_boss` -> `Grave Lord` (King's Road)
- `enemy_boss_docks` -> `Tidecaller` (Saltwake Docks, mid-zone)
- `enemy_boss_tidemaster` -> `Harrow Tidemaster` (Saltwake Docks)
- `enemy_boss_forge` -> `Iron Warden` (Emberforge March)
- `enemy_boss_ward` -> `Plague Archon` (Ashen Ward, mid-zone)
- `enemy_boss_plague_monarch` -> `Plague Monarch` (Ashen Ward)
- `enemy_boss_pass` -> `Thornwall Chieftain` (Thornwall Pass)
- `enemy_boss_basilica` -> `Bone Pontiff` (Hollow Basilica, mid-zone)
- `enemy_boss_reliquary` -> `Reliquary Tyrant` (Hollow Basilica)
- `enemy_boss_mire` -> `Mire Behemoth` (Mire of Saints)
- `enemy_boss_steppe` -> `Steppe Warlord` (Sunfall Steppe)
- `enemy_boss_verge` -> `Gloamwood Witch` (Gloamwood Verge)
- `enemy_boss_citadel` -> `Dread Sovereign` (Crownfall Citadel, mid-zone)
- `enemy_boss_ashen_regent` -> `Ashen Regent` (Crownfall Citadel)

## Language guardrails

- Prefer `caravan`, `war wagon`, `gatehouse`, `keep`, `ward`, `host`, `grave`, `blight`, `rite`, and `siege`
- Avoid modern outbreak terms like `infected`, `quarantine`, `convoy`, `blacksite`, `gas station`, and `metro` in new content
- Keep low-level system IDs unchanged unless a later migration explicitly requires it
