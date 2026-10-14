# Crownroad playable content and stats

Implemented prototype · Generated from the playable data. Rebuild with `python3 scripts/tools/revamp_content.py`.

32 core recruits plus 5 retained legacy ownership IDs; 59 ordinary hostile types, 14 bosses and the optional Ash Pilgrim; 100 equipment relics; 30 spells and 12 tactical cards. New art uses existing placeholder models and pictures.

Stats below are bare values before equipment and temporary effects. HP and attack show level 1 → level 20. HP gains 1.5% of base per level, attack and structure damage gain 1%; other stats stay fixed. Crit damage is bonus damage: +50% means a critical hit deals 150%. Militia and Sapper stats are per body.

Every level record is available in [the playable catalog](../design/playable-content-catalog.json). Main campaign enemies progress from level 1 to 20; ten regional debuts preserve familiar troops. Other ordinary types enter the regional endless pools.

## Core recruits

| Name | HP 1 → 20 | Attack 1 → 20 | Armor | Phys % | Magic % | Crit % / bonus % | Speed / reach / interval | Deployment cost / bodies |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Peasant | 30 → 39 | 5 → 5.95 | 0 | 0 | 0 | 0 / +50 | 36 / 24 / 1.2s | 8 / 1 |
| Militia | 24 → 31 | 4.5 → 5.355 | 2 | 1.961 | 0 | 3 / +50 | 34 / 28 / 1.15s | 14 / 2 |
| Spearman | 54 → 69 | 8 → 9.52 | 8 | 7.407 | 0 | 6 / +50 | 34 / 46 / 1.1s | 18 / 1 |
| Archer | 44 → 57 | 8 → 9.52 | 2 | 1.961 | 0 | 6 / +50 | 33.8 / 73 / 1s | 18 / 1 |
| Swordsman | 64 → 82 | 10 → 11.9 | 10 | 9.091 | 5 | 8 / +50 | 35 / 32 / 0.9s | 20 / 1 |
| Field Medic | 48 → 62 | 4 → 4.76 | 4 | 3.846 | 5 | 0 / +50 | 32 / 60 / 1.5s | 20 / 1 |
| Axeman | 62 → 80 | 11 → 13.09 | 8 | 7.407 | 0 | 8 / +50 | 37 / 30 / 1.1s | 20 / 1 |
| Maceman | 68 → 87 | 12 → 14.28 | 14 | 12.281 | 0 | 6 / +50 | 30 / 30 / 1.3s | 22 / 1 |
| Caravan Engineer | 52 → 67 | 6 → 7.14 | 6 | 5.66 | 0 | 3 / +50 | 35 / 60 / 1.2s | 22 / 1 |
| Billman | 58 → 75 | 9 → 10.71 | 10 | 9.091 | 0 | 6 / +50 | 31 / 52 / 1.2s | 22 / 1 |
| Man-at-Arms | 72 → 93 | 11 → 13.09 | 20 | 16.667 | 5 | 8 / +50 | 31 / 32 / 1.1s | 24 / 1 |
| Crossbowman | 44 → 57 | 13 → 15.47 | 4 | 3.846 | 0 | 8 / +55 | 31.5 / 94 / 1.5s | 24 / 1 |
| Shield Knight | 76 → 98 | 8 → 9.52 | 34 | 25.373 | 5 | 5 / +50 | 29 / 28 / 1.15s | 28 / 1 |
| Battle Monk | 54 → 69 | 7 → 8.33 | 8 | 7.407 | 10 | 3 / +50 | 33 / 64 / 1.2s | 22 / 1 |
| Paladin | 72 → 93 | 10 → 11.9 | 22 | 18.033 | 12 | 6 / +55 | 30 / 34 / 1.25s | 30 / 1 |
| Pikeman | 62 → 80 | 9 → 10.71 | 12 | 10.714 | 0 | 5 / +50 | 30 / 58 / 1.4s | 22 / 1 |
| Halberdier | 66 → 85 | 12 → 14.28 | 16 | 13.793 | 0 | 6 / +55 | 31 / 42 / 1.4s | 26 / 1 |
| Cavalry Rider | 52 → 67 | 9 → 10.71 | 10 | 9.091 | 0 | 8 / +50 | 68 / 24 / 0.9s | 24 / 1 |
| Gravebound Knight | 68 → 87 | 9 → 10.71 | 16 | 13.793 | 5 | 5 / +50 | 30 / 32 / 1.2s | 28 / 1 |
| Greatswordsman | 64 → 82 | 14 → 16.66 | 12 | 10.714 | 0 | 6 / +60 | 30 / 40 / 1.7s | 26 / 1 |
| Quartermaster | 48 → 62 | 5 → 5.95 | 4 | 3.846 | 0 | 3 / +50 | 30 / 64 / 1.3s | 22 / 1 |
| Berserker | 58 → 75 | 11 → 13.09 | 4 | 3.846 | 0 | 10 / +55 | 43 / 30 / 0.9s | 24 / 1 |
| Sapper Crew | 32 → 41 | 4 → 4.76 | 4 | 3.846 | 0 | 3 / +50 | 26 / 28 / 1.4s | 30 / 2 |
| Outrider | 48 → 62 | 8 → 9.52 | 8 | 7.407 | 0 | 8 / +50 | 68 / 28 / 0.9s | 22 / 1 |
| Longbowman | 42 → 54 | 17 → 20.23 | 2 | 1.961 | 0 | 8 / +55 | 31 / 128 / 2.2s | 24 / 1 |
| Warhammer Knight | 80 → 103 | 17 → 20.23 | 24 | 19.355 | 5 | 6 / +55 | 29 / 30 / 1.4s | 32 / 1 |
| Rogue | 40 → 51 | 12 → 14.28 | 4 | 3.846 | 0 | 12 / +60 | 54 / 26 / 0.9s | 22 / 1 |
| Duelist | 54 → 69 | 11 → 13.09 | 8 | 7.407 | 0 | 12 / +60 | 42 / 30 / 1s | 22 / 1 |
| Mage | 44 → 57 | 13 → 15.47 | 2 | 1.961 | 18 | 6 / +55 | 30 / 104 / 1.5s | 28 / 1 |
| Poleaxe Knight | 84 → 108 | 18 → 21.42 | 26 | 20.635 | 5 | 8 / +55 | 30 / 42 / 1.5s | 34 / 1 |
| Banner Knight | 64 → 82 | 7 → 8.33 | 16 | 13.793 | 5 | 4 / +50 | 29 / 28 / 1.1s | 26 / 1 |
| Ballista Crew | 54 → 69 | 18 → 21.42 | 8 | 7.407 | 0 | 5 / +55 | 22 / 116 / 2.1s | 36 / 1 |

## Retained legacy recruits

| Name | HP 1 → 20 | Attack 1 → 20 | Armor | Phys % | Magic % | Crit % / bonus % | Speed / reach / interval | Deployment cost / bodies |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| War Hound | 28 → 36 | 5 → 5.95 | 0 | 0 | 0 | 6 / +50 | 75 / 24 / 0.9s | 12 / 1 |
| Alchemist | 54 → 69 | 10 → 11.9 | 6 | 5.66 | 8 | 6 / +50 | 29 / 80 / 1.6s | 26 / 1 |
| Necromancer | 48 → 62 | 9 → 10.71 | 4 | 3.846 | 12 | 6 / +50 | 29 / 80 / 1.5s | 30 / 1 |
| Lantern Guard | 80 → 103 | 11 → 13.09 | 22 | 18.033 | 5 | 6 / +50 | 30 / 34 / 1.2s | 28 / 1 |
| Stormcaller | 44 → 57 | 10 → 11.9 | 2 | 1.961 | 18 | 6 / +50 | 30 / 107 / 1.5s | 28 / 1 |

## Ordinary enemies

| Name | HP 1 → 20 | Attack 1 → 20 | Armor | Phys % | Magic % | Crit % / bonus % | Speed / reach / interval | Deployment cost / bodies |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Risen | 37 → 48 | 4.95 → 5.891 | 5.6 | 5.303 | 0 | 0 / +25 | 32.1 / 30 / 1s | 0 / 1 |
| Ghoul | 24 → 31 | 4.4 → 5.236 | 1.4 | 1.381 | 0 | 5 / +25 | 61.8 / 24 / 0.7s | 0 / 1 |
| Rot Hulk | 53 → 68 | 7.15 → 8.508 | 7 | 6.542 | 7 | 5 / +25 | 24.5 / 30 / 1.15s | 0 / 1 |
| Grave Brute | 67 → 86 | 9.35 → 11.126 | 15.4 | 13.345 | 3.5 | 5 / +40 | 22.2 / 34 / 1.1s | 0 / 1 |
| Blight Caster | 28 → 36 | 6.05 → 7.199 | 2.8 | 2.724 | 14 | 5 / +25 | 28 / 85 / 1.3s | 0 / 1 |
| Bone Nest | 47 → 60 | 5.5 → 6.545 | 9.8 | 8.925 | 7 | 3 / +25 | 30.3 / 28 / 0.95s | 0 / 1 |
| Sapper | 31 → 40 | 6.6 → 7.854 | 4.2 | 4.031 | 0 | 8 / +40 | 67.6 / 24 / 0.74s | 0 / 1 |
| Dread Herald | 50 → 64 | 7.15 → 8.508 | 12.6 | 11.19 | 10.5 | 5 / +35 | 42 / 30 / 1.02s | 0 / 1 |
| Hexer | 42 → 54 | 4.95 → 5.891 | 7 | 6.542 | 21 | 3 / +25 | 37.3 / 30 / 0.96s | 0 / 1 |
| Bone Juggernaut | 91 → 117 | 12.65 → 15.053 | 28 | 21.875 | 7 | 5 / +50 | 17.5 / 36 / 1.2s | 0 / 1 |
| Shield Wall | 72 → 93 | 6.05 → 7.199 | 31.5 | 23.954 | 7 | 3 / +25 | 18.7 / 30 / 1.1s | 0 / 1 |
| Lich | 54 → 69 | 7.7 → 9.163 | 5.6 | 5.303 | 28 | 5 / +40 | 21 / 90 / 1.4s | 0 / 1 |
| Siege Tower | 120 → 154 | 0 → 0 | 35 | 25.926 | 7 | 0 / +0 | 10.5 / 0 / 999s | 0 / 1 |
| Mirror Knight | 53 → 68 | 7.7 → 9.163 | 19.6 | 16.388 | 17.5 | 8 / +50 | 33.8 / 30 / 0.95s | 0 / 1 |
| Tunneler | 34 → 44 | 7.15 → 8.508 | 7 | 6.542 | 3.5 | 10 / +40 | 40.8 / 26 / 0.8s | 0 / 1 |
| Bone Ballista | 55 → 71 | 8.8 → 10.472 | 16.8 | 14.384 | 3.5 | 5 / +40 | 14 / 110 / 2.05s | 0 / 1 |
| Catacomb Giant | 102 → 131 | 12.1 → 14.399 | 28 | 21.875 | 10.5 | 5 / +50 | 14 / 42 / 1.22s | 0 / 1 |
| Revenant Captain | 58 → 75 | 7.7 → 9.163 | 21 | 17.355 | 14 | 8 / +45 | 33.8 / 34 / 0.98s | 0 / 1 |
| Plague Engine | 108 → 139 | 11 → 13.09 | 24.5 | 19.679 | 14 | 3 / +40 | 10.5 / 105 / 2.2s | 0 / 1 |
| Grave Rat | 11 → 14 | 2.2 → 2.618 | 0 | 0 | 0 | 5 / +25 | 86 / 18 / 0.6s | 0 / 1 |
| Rustguard | 60 → 77 | 4.95 → 5.891 | 38.5 | 27.798 | 3.5 | 3 / +25 | 24 / 30 / 1.15s | 0 / 1 |
| Bellringer | 35 → 45 | 3.3 → 3.927 | 5.6 | 5.303 | 10.5 | 0 / +25 | 23 / 60 / 1.6s | 0 / 1 |
| Highwayman | 34 → 44 | 6.6 → 7.854 | 8.4 | 7.749 | 0 | 8 / +40 | 40 / 95 / 1.7s | 0 / 1 |
| Drowned Harpooner | 47 → 60 | 6.6 → 7.854 | 12.6 | 11.19 | 7 | 6 / +35 | 29 / 92 / 1.6s | 0 / 1 |
| Salt Wraith | 29 → 37 | 7.15 → 8.508 | 0 | 0 | 31.5 | 5 / +40 | 48 / 30 / 0.95s | 0 / 1 |
| Barnacle Guard | 78 → 100 | 5.5 → 6.545 | 24.5 | 19.679 | 10.5 | 3 / +25 | 20 / 32 / 1.25s | 0 / 1 |
| Hookblade Raider | 40 → 51 | 8.8 → 10.472 | 8.4 | 7.749 | 3.5 | 12 / +50 | 58 / 28 / 0.82s | 0 / 1 |
| Ash Hound | 31 → 40 | 6.6 → 7.854 | 3.5 | 3.382 | 3.5 | 8 / +40 | 76 / 24 / 0.8s | 0 / 1 |
| Cinder Spitter | 35 → 45 | 5.5 → 6.545 | 4.2 | 4.031 | 17.5 | 5 / +25 | 26 / 95 / 1.55s | 0 / 1 |
| Furnace Knight | 85 → 109 | 11 → 13.09 | 35 | 25.926 | 14 | 5 / +40 | 19 / 34 / 1.45s | 0 / 1 |
| Powder Bandit | 41 → 53 | 5.5 → 6.545 | 7 | 6.542 | 0 | 5 / +25 | 34 / 88 / 1.8s | 0 / 1 |
| Plague Leech | 16 → 21 | 2.2 → 2.618 | 0 | 0 | 7 | 0 / +25 | 48 / 22 / 1.2s | 0 / 1 |
| Miasma Priest | 43 → 55 | 4.4 → 5.236 | 4.2 | 4.031 | 24.5 | 4 / +25 | 24 / 96 / 1.55s | 0 / 1 |
| Rot Surgeon | 48 → 62 | 3.3 → 3.927 | 8.4 | 7.749 | 17.5 | 3 / +25 | 27 / 58 / 1.4s | 0 / 1 |
| Sealbreaker | 55 → 71 | 8.25 → 9.817 | 15.4 | 13.345 | 21 | 8 / +45 | 35 / 36 / 1.25s | 0 / 1 |
| Cliff Stalker | 38 → 49 | 9.35 → 11.126 | 5.6 | 5.303 | 3.5 | 12 / +50 | 62 / 26 / 0.86s | 0 / 1 |
| Thorn Brute | 82 → 105 | 10.45 → 12.435 | 25.2 | 20.128 | 7 | 5 / +40 | 22 / 36 / 1.35s | 0 / 1 |
| Briar Witch | 40 → 51 | 5.5 → 6.545 | 2.8 | 2.724 | 24.5 | 5 / +30 | 25 / 92 / 1.6s | 0 / 1 |
| Avalanche Bearer | 90 → 116 | 13.2 → 15.708 | 18.2 | 15.398 | 7 | 5 / +45 | 17 / 116 / 2.4s | 0 / 1 |
| Chain Penitent | 64 → 82 | 6.6 → 7.854 | 16.8 | 14.384 | 14 | 4 / +35 | 26 / 42 / 1.3s | 0 / 1 |
| Ossuary Archer | 32 → 41 | 7.7 → 9.163 | 5.6 | 5.303 | 10.5 | 8 / +45 | 27 / 116 / 1.65s | 0 / 1 |
| Bone Cantor | 49 → 63 | 4.4 → 5.236 | 5.6 | 5.303 | 28 | 4 / +30 | 22 / 86 / 1.7s | 0 / 1 |
| Reliquary Eye | 72 → 93 | 9.9 → 11.781 | 11.2 | 10.072 | 31.5 | 5 / +45 | 0 / 124 / 2.1s | 0 / 1 |
| Swamp Maw | 66 → 85 | 12.1 → 14.399 | 12.6 | 11.19 | 7 | 8 / +50 | 20 / 32 / 1.5s | 0 / 1 |
| Bog Spore | 29 → 37 | 2.75 → 3.272 | 1.4 | 1.381 | 17.5 | 0 / +25 | 19 / 24 / 1.8s | 0 / 1 |
| Drowned Colossus | 114 → 146 | 13.75 → 16.363 | 25.2 | 20.128 | 14 | 5 / +50 | 14 / 40 / 1.55s | 0 / 1 |
| Bloated Eel | 36 → 46 | 6.6 → 7.854 | 2.8 | 2.724 | 7 | 10 / +35 | 72 / 24 / 0.75s | 0 / 1 |
| Bone Lancer | 60 → 77 | 11 → 13.09 | 18.2 | 15.398 | 3.5 | 10 / +55 | 70 / 40 / 1.2s | 0 / 1 |
| Dust Reaver | 43 → 55 | 7.15 → 8.508 | 9.8 | 8.925 | 0 | 10 / +45 | 49 / 90 / 1.3s | 0 / 1 |
| Carrion Banner | 58 → 75 | 5.5 → 6.545 | 14 | 12.281 | 17.5 | 5 / +35 | 28 / 30 / 1.3s | 0 / 1 |
| War Drummer | 52 → 67 | 4.4 → 5.236 | 11.2 | 10.072 | 7 | 3 / +25 | 32 / 28 / 1.35s | 0 / 1 |
| Shade | 31 → 40 | 10.45 → 12.435 | 0 | 0 | 21 | 15 / +65 | 60 / 26 / 0.85s | 0 / 1 |
| Mirror Wisp | 29 → 37 | 6.6 → 7.854 | 0 | 0 | 31.5 | 8 / +40 | 35 / 100 / 1.4s | 0 / 1 |
| Wicker Giant | 106 → 136 | 12.1 → 14.399 | 42 | 29.577 | 7 | 5 / +45 | 18 / 42 / 1.45s | 0 / 1 |
| Dream Weaver | 42 → 54 | 4.95 → 5.891 | 1.4 | 1.381 | 28 | 4 / +30 | 25 / 110 / 1.8s | 0 / 1 |
| Gate Breaker | 110 → 141 | 13.2 → 15.708 | 35 | 25.926 | 10.5 | 5 / +50 | 17 / 38 / 1.7s | 0 / 1 |
| Crown Executioner | 76 → 98 | 15.4 → 18.326 | 22.4 | 18.301 | 10.5 | 12 / +65 | 30 / 34 / 1.65s | 0 / 1 |
| Null Magister | 58 → 75 | 7.7 → 9.163 | 8.4 | 7.749 | 38.5 | 6 / +45 | 25 / 112 / 1.75s | 0 / 1 |
| Soul Standard | 70 → 90 | 4.4 → 5.236 | 14 | 12.281 | 31.5 | 3 / +30 | 20 / 84 / 1.75s | 0 / 1 |

## Bosses

| Name | HP 1 → 20 | Attack 1 → 20 | Armor | Phys % | Magic % | Crit % / bonus % | Speed / reach / interval | Deployment cost / bodies |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Grave Lord | 224 → 288 | 20.15 → 23.978 | 24.5 | 19.679 | 14 | 5 / +50 | 14 / 44 / 1.4s | 0 / 1 |
| Tidecaller | 196 → 252 | 16.9 → 20.111 | 14 | 12.281 | 24.5 | 5 / +40 | 18.7 / 85 / 1.5s | 0 / 1 |
| Iron Warden | 266 → 342 | 18.2 → 21.658 | 45.5 | 31.271 | 14 | 5 / +50 | 12.8 / 40 / 1.5s | 0 / 1 |
| Plague Archon | 238 → 306 | 19.5 → 23.205 | 21 | 17.355 | 28 | 5 / +45 | 15.2 / 44 / 1.4s | 0 / 1 |
| Thornwall Chieftain | 217 → 279 | 18.85 → 22.431 | 24.5 | 19.679 | 10.5 | 8 / +50 | 21 / 38 / 1.3s | 0 / 1 |
| Bone Pontiff | 252 → 324 | 20.8 → 24.752 | 28 | 21.875 | 28 | 5 / +50 | 14 / 44 / 1.4s | 0 / 1 |
| Mire Behemoth | 336 → 432 | 22.1 → 26.299 | 35 | 25.926 | 17.5 | 5 / +50 | 10.5 / 42 / 1.6s | 0 / 1 |
| Steppe Warlord | 245 → 315 | 23.4 → 27.846 | 24.5 | 19.679 | 10.5 | 10 / +60 | 22.2 / 40 / 1.2s | 0 / 1 |
| Gloamwood Witch | 231 → 297 | 18.2 → 21.658 | 14 | 12.281 | 35 | 8 / +50 | 16.3 / 90 / 1.5s | 0 / 1 |
| Dread Sovereign | 322 → 414 | 26 → 30.94 | 38.5 | 27.798 | 28 | 8 / +60 | 15.2 / 46 / 1.3s | 0 / 1 |
| Reliquary Tyrant | 301 → 387 | 22.1 → 26.299 | 31.5 | 23.954 | 31.5 | 5 / +50 | 14 / 90 / 1.8s | 0 / 1 |
| Ashen Regent | 392 → 504 | 28.6 → 34.034 | 45.5 | 31.271 | 35 | 10 / +65 | 17.5 / 46 / 1.25s | 0 / 1 |
| Harrow Tidemaster | 336 → 432 | 23.4 → 27.846 | 31.5 | 23.954 | 28 | 5 / +50 | 15.2 / 95 / 1.38s | 0 / 1 |
| Plague Monarch | 364 → 468 | 22.1 → 26.299 | 35 | 25.926 | 35 | 8 / +55 | 16.3 / 50 / 1.28s | 0 / 1 |

## Optional encounter

| Name | HP 1 → 20 | Attack 1 → 20 | Armor | Phys % | Magic % | Crit % / bonus % | Speed / reach / interval | Deployment cost / bodies |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Ash Pilgrim | 80 → 103 | 0 → 0 | 2.1 | 2.057 | 7 | 0 / +0 | 24 / 0 / 999s | 0 / 1 |

## Equipment relics

All permanent sources share the same total bonus caps: health/damage/speed/reach/structure +10%, armor +10, physical resistance +0.5 pp, magic resistance +8 pp, critical chance +5 pp, critical bonus +10 pp, attack interval reduction 5%.

| Relic | Rarity | Stat bonuses | Effect |
| --- | --- | --- | --- |
| Iron Pendant | common | +4% health | +4% health. |
| Sharpened Edge | common | +4% attack | +4% attack. |
| Swift Boots | common | +4% speed | +4% speed. |
| Battle Drum | common | +2.5% attack recovery | +2.5% attack recovery. |
| Guardian Shield | rare | +3.2% health, +5.6 armor, +1.6 pp magic resistance, +3.8% structure damage | Add a small deployment shield; trade some raw stats for the effect. |
| War Brand | rare | +4% attack, +1.6% attack recovery, +4 pp critical damage | Reward a completed armor break with a brief personal damage window. |
| Windrunner Cloak | rare | +2.4% health, +4% speed, +2.4 pp magic resistance | Reduce slow duration, distinct from periodic cleansing. |
| Sage's Ring | rare | +2.4% health, +3.2% attack, +4 pp magic resistance, +3.2 pp critical damage | After a player spell, advance the wearer's automatic ability recovery on a cooldown. |
| Crown of Valor | epic | +3.6% health, +2.4% attack, +3.6 armor, +1.8 pp magic resistance, +2.4 pp critical damage | A qualifying elite kill briefly rallies a few nearby allies. |
| Blade of Ruin | epic | +4.2% attack, +1.2 pp critical chance, +4.8 pp critical damage, +4.6% structure damage | Offer a strong periodic hit at a capped health cost that cannot directly kill the wearer. |
| Phantom Mantle | epic | +2.4% health, +3.6% speed, +3 pp magic resistance, +1.2% attack recovery | Dodge one direct projectile after a visible recharge; area attacks still work. |
| Dragon Heart | epic | +4.8% health, +1.8% attack, +3 armor, +2.4 pp magic resistance, +4.6% structure damage | A low-health threshold emits one small burn pulse per deployment. |
| Wolftooth Charm | common | +2% attack, +2.5% attack recovery | Add a small benefit while fighting beside another beast troop; keep a useful base effect. |
| Tombstone Shard | rare | +4% health, +1.6% attack, +2.4 pp magic resistance, +3.2 armor, +3.8% structure damage | Restore a little health after a qualifying kill, on a cooldown. |
| Stormcaller Sigil | rare | +4.8% attack, +3.2 pp magic resistance, +4.8 pp critical damage, +1.6% speed | Improve existing chained attacks without granting chains to every weapon. |
| Siege Hammer | rare | +-2% speed, +7.8% structure damage | +-2% speed, +7.8% structure damage. |
| Spectral Lantern | epic | +3.6% attack, +1.8% health, +3.6 pp magic resistance, +2.4% reach | Weaken nearby blight duration; avoid duplicating Gloam Lantern's stealth reveal. |
| Immortal Wreath | epic | +5% health, +3.6 armor, +3 pp magic resistance, +1.2% speed | Increase healing received while critically wounded rather than promising unconditional immortality. |
| Frostbound Crown | epic | +3.6% health, +3% attack, +2.4 armor, +3 pp magic resistance, +1.2% attack recovery, +4.2% structure damage | Emit a small chill pulse after a completed unit ability, with a cooldown. |
| Moonfire Talisman | epic | +2.4% health, +4.2% attack, +1.5 pp critical chance, +2.4% speed, +3.8% structure damage | A qualifying kill empowers one later strike; cannot refresh indefinitely. |
| Hardened Bulwark | hardened | +3.52% health, +6 armor, +0.5 pp physical resistance, +4.6% structure damage | Cap a fraction of the first heavy direct hit taken each battle. |
| Hardened Fang | hardened | +3.96% attack, +1.32% attack recovery, +1.32 pp critical chance, +3.96 pp critical damage | Reward a successful interrupt with brief real attack recovery acceleration. |
| Hardened Sigil | hardened | +2.2% health, +2.64% attack, +2.64 armor, +3.08 pp magic resistance, +4.6% structure damage | Convert a limited periodic hit to arcane damage; reduce universal stats accordingly. |
| Hardened Crown | hardened | +3.08% health, +2.2% attack, +3.52 armor, +2.64 pp magic resistance, +1.32% speed, +0.88% attack recovery | Extend the wearer's qualifying temporary buffs, with a duration cap. |
| Hardened Soul | hardened | +3.96% health, +3.52% attack, +3.52 armor, +2.64 pp magic resistance, +0.88 pp critical chance, +2.64 pp critical damage, +1.32% attack recovery, +1.32% speed, +5.4% structure damage | Gain a capped personal momentum bonus after qualifying kills; resets on death. |
| Grave Lord's Crown | epic | +4.2% health, +3% attack, +3.6 pp magic resistance, +1.2% attack recovery, +4.2% structure damage | A qualifying corpse conversion grants one temporary servant, with a strict summon cap. |
| Iron Warden's Heart | epic | +4.8% health, +2.4% attack, +6 armor, +1.8 pp magic resistance, +5.4% structure damage | Regenerate a small shield only after a safe pause between attacks. |
| Sovereign's Mantle | epic | +3.6% health, +4.2% attack, +4.8 armor, +3 pp magic resistance, +1.8% speed, +1.2% attack recovery, +4.6% structure damage | Periodically rally at most a few nearby living allies. |
| Plague Censer | epic | +3% health, +3% attack, +4 pp magic resistance, +1.2% speed, +1.8% attack recovery, +3.8% structure damage | A completed unit ability cleanses one nearby blighted ally on a cooldown. |
| Tower Sentinel's Ward | epic | +3.6% health, +1.8% attack, +5.4 armor, +3 pp magic resistance, +1.2% attack recovery, +4.6% structure damage | A battle-start ward protects against the first marked hazard; retain broad usefulness outside Tower. |
| Ascendant's Signet | epic | +3% health, +4.2% attack, +3 pp magic resistance, +3.6 pp critical damage, +1.8% speed, +1.2% attack recovery, +3.8% structure damage | Participation in a field objective gives a capped personal momentum bonus. |
| Apex Talisman | hardened | +3.08% health, +3.08% attack, +3.08 armor, +2.64 pp magic resistance, +0.88 pp critical chance, +3.08 pp critical damage, +0.88% speed, +1.32% attack recovery, +5.4% structure damage | Improve damage against named elites, with no percentage-health boss damage. |
| Pinnacle Crown | hardened | +3.96% health, +3.96% attack, +3.96 armor, +3.08 pp magic resistance, +1.32 pp critical chance, +3.96 pp critical damage, +1.32% speed, +1.76% attack recovery, +6.2% structure damage | The first qualifying unit ability repeats once at reduced power; the echo cannot trigger item effects. |
| Rampart Seal | common | +3% health, +4 armor | Creates a small personal shield on deployment. |
| Tower Charm | common | +6 armor, +1 pp magic resistance | Briefly reduces direct damage while the wearer has remained stationary. |
| Last Wall Token | rare | +4% health, +4.8 armor | Gains temporary defense on crossing a low-health threshold. |
| Iron Prayer | rare | +3.2% health, +3.2 pp critical damage | Every fourth melee hit restores a little of the wearer's health. |
| Sentinel Buckle | epic | +4.2 armor, +0.5 pp physical resistance | Grants a ward after successfully intercepting a charge. |
| Oathbound Chain | epic | +3.6% health, +3 armor, +1.8 pp magic resistance | Shares a capped portion of received healing with the nearest injured ally. |
| Farwatch Lens | common | +2% attack, +2.4% reach | Increases shot damage with distance up to a modest cap. |
| Windstring | common | +2% attack, +2% attack recovery | Shortens the wearer's first shot windup after deployment. |
| Falcon Feather | rare | +2% attack, +1.2 pp critical chance | The first hit marks one target for a short allied focus bonus. |
| Huntsman's Knot | rare | +2.4% attack, +4.8 pp critical damage | Boosts damage against an enemy isolated from its allies. |
| Trailfinder Boots | epic | +2.4% speed, +1.2 pp critical chance | A qualifying kill grants a brief movement burst. |
| Twinbolt Clasp | epic | +2.4% attack, +1.5 pp critical chance, +1.8% reach | Every fifth ordinary projectile releases a weaker second bolt. |
| Breach Writ | common | +5.4% structure damage, +2 armor | The wearer's first structure strike briefly exposes that structure to allied siege damage. |
| Demolition Rune | common | +2% attack, +5% structure damage | Deals a small secondary blast when the wearer destroys a tactical blocker. |
| Counterweight | rare | +3.2% attack, +7% structure damage, +-2% speed | Every third artillery shot has a larger impact area and a longer windup. |
| Gateward Medallion | rare | +5.6 armor, +0.5 pp physical resistance | Reduces damage received from marked siege attacks. |
| Sapper's Fuse | epic | +3% health, +6.2% structure damage | Leaves one short-fuse bomb when the wearer dies. |
| Chain Hoist Charm | epic | +2.4% attack, +1.2% speed | Accelerates a siege unit's advance until its first committed attack. |
| Cinder Ring | common | +2% attack, +3 pp critical damage | Adds a small burn to a periodic successful hit. |
| Ashen Rosary | common | +3% attack, +1 pp critical chance | Deals bonus damage to burning targets. |
| Coalglass Vial | rare | +2.4% attack, +1.2 pp magic resistance | Extends burns created by the wearer. |
| Fireward Clasp | rare | +3.2% health, +2.4 pp magic resistance | Reduces received burn damage and shortens afterburn. |
| Phoenix Ember | epic | +3.6% health, +3 pp magic resistance | The wearer returns once with low health after a short death delay. |
| Hearthstone | epic | +3% health, +1.8 armor, +2.4 pp magic resistance | On deployment, extinguishes burn on a small number of nearby allies. |
| Rime Needle | common | +2% attack, +1 pp critical chance | Every fourth hit applies a brief chill. |
| Winter Ribbon | common | +2.5% attack, +5 pp critical damage | Deals bonus damage to chilled or rooted targets. |
| Hoarfrost Medal | rare | +3.2% health, +3.2 armor, +1.6 pp magic resistance | Grants a small shield when the wearer is rooted or slept. |
| Icebound Hourglass | rare | +1.6% attack, +2.4 pp magic resistance, +1.6% attack recovery | A completed unit ability chills its primary target. |
| Glacial Band | epic | +3% health, +1.5% attack, +2.4 pp magic resistance | After the wearer heals an ally, its next attack creates a small chill burst. |
| Thawstone | epic | +1.8% speed, +2.4 armor, +2.4 pp magic resistance | Periodically removes one root or chill from the wearer. |
| Mercy Bell | common | +3% health, +2 pp magic resistance | Improves the wearer's direct healing of allies at low health. |
| Field Surgeon's Badge | common | +4% health, +4 armor | Healing another ally briefly protects the wearer. |
| Bloodroot Pendant | rare | +2.4% attack, +2.4% health | Heals a small portion of direct damage dealt. |
| Springwater Charm | rare | +3.2% health, +2.4 pp magic resistance | On deployment, cleanses one blight or wound from the nearest affected ally. |
| Mender's Thread | epic | +3.6% health, +3 pp magic resistance | Converts limited direct overhealing into a temporary shield. |
| Second Breath Seal | epic | +4.2% health, +2.4 armor | Survives one lethal direct hit at one health. |
| Nightveil Brooch | common | +2% attack, +2% speed | The wearer begins briefly concealed until it attacks. |
| Assassin's Token | common | +1.5 pp critical chance, +5 pp critical damage | The first strike against a newly selected enemy gains a small bonus. |
| Grave Silk | rare | +2.4% attack, +3.2 pp magic resistance | A qualifying kill seals that corpse against enemy resurrection. |
| Umbral Coin | rare | +2.4% attack, +1.6 pp critical chance | A qualifying elite kill returns a small amount of courage. |
| Phantom Bell | epic | +2.4% health, +3.6 pp critical damage, +2.4 pp magic resistance | After receiving a direct heal, briefly reduces reflected damage received. |
| Moonstep Anklet | epic | +2.4% speed, +1.5 pp critical chance, +4.2 pp critical damage | After completing several attacks, dodges one incoming projectile. |
| Marshal's Signet | common | +3% health, +3 armor | Deployment briefly buffs a few nearby allies. |
| Supply Ledger | common | +3% health, +2% speed | Returns a small fraction of paid courage after the wearer's first death. |
| Banner Ribbon | rare | +3.2% health, +2.4 pp magic resistance, +1.8% reach | Extends the wearer's existing aura radius. |
| Captain's Whistle | rare | +1.6% attack, +3.2% health, +1.2 pp critical chance | A completed unit ability can interrupt one channel in a small nearby radius. |
| Caravan Token | epic | +3% health, +3 armor, +2.4 pp magic resistance | The wearer's participation in a field objective grants one small supply reward. |
| King's Commission | epic | +2.4% attack, +3% health, +1.2 pp critical chance | Gains a limited personal bonus when supported by a nearby ally from a different squad role. |
| Saint's Reliquary | epic | +4.2% health, +3 pp magic resistance, +2.4 armor | On the wearer's first death, releases one pulse of healing for living allies. |
| Stormglass Prism | epic | +3% attack, +4.8 pp critical damage, +2.4% reach | A periodic direct shot chains a weaker hit to one nearby enemy. |
| Earthbind Idol | epic | +3% health, +5.4 armor, +2.4 pp magic resistance | Stationary defenders become resistant to pulls and forced pushes. |
| Dragonbone Quiver | epic | +3% attack, +1.8 pp critical chance, +3% reach | A periodic arrow pierces one target with reduced damage to the next. |
| Emberforge Anvil | epic | +3% health, +6 armor, +6.2% structure damage | Heavy melee recovery briefly grants physical armor. |
| Mirror Shard | epic | +3.6% health, +5.4 armor, +0.5 pp physical resistance | Reflects a capped fraction of direct melee damage received. |
| Gravewarden's Key | epic | +3% health, +3.6 pp magic resistance, +1.8% attack | Periodically seals one nearby enemy corpse before a summoner can consume it. |
| Tidemaster's Pearl | epic | +3.6% health, +3.6 armor, +2.4% speed | A forced displacement grants a brief movement and attack recovery boost. |
| Sunfall Spur | epic | +3.6% attack, +3% speed, +1.2 pp critical chance | A charge kill advances the wearer's charge recovery once. |
| Gloam Lantern | epic | +3% health, +3.6 pp magic resistance, +3% reach | Reveals nearby concealed enemies and identifies the real illusion. |
| Crownroad Charter | epic | +4.2% health, +3 pp magic resistance, +2.4 armor | Completing a supply objective restores a little squad mana. |
| Hollow Chalice | epic | +3% attack, +3.6% health, +4.8 pp critical damage | Consumes an enemy corpse token to recover the wearer's health. |
| Royal Execution Writ | hardened | +3.08% attack, +1.32 pp critical chance, +4.84 pp critical damage | A periodic finishing strike removes an ordinary low-health target. |
| Plagueglass Mask | hardened | +3.08% health, +3.3 pp magic resistance, +2.2 armor | Strongly reduces healing penalties from blight and wounds. |
| Runic Capacitor | hardened | +3.08% attack, +2.64 pp magic resistance, +4.4 pp critical damage | Stores limited charges from player spell casts and spends one on the wearer's next hit. |
| Wandering Shrine | hardened | +3.96% health, +3.08 pp magic resistance, +2.64 armor | After standing safely for a short time, emits a small healing field. |
| Basilica Hourglass | hardened | +3.96% health, +3.96 armor, +3.08 pp magic resistance | A first lethal hit suspends the wearer briefly; it cannot act until restored with a little health. |
| Siegebreaker's Crest | hardened | +3.08% attack, +8% structure damage, +2.64 armor | Improves damage against explicitly fortified enemies. |
| Lantern of Dawn | hardened | +4.4% health, +3.52 pp magic resistance, +3.52 armor | Prevents one nearby ally's lethal direct hit and spends its battle charge. |

## Spells and tactical gear

These share five loadout slots. Levels 1–3 give flat damage, healing and durability +5% per upgrade; ratios, control durations, movement distance and resource income stay fixed.

| Card | Kind | Stage | Mana | Cooldown | Duration | Power | Effect |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Fireball | spell | 1 | 8 | 12s | 0s | 34 | Drop an explosive blast that clears clustered enemies. |
| Heal | spell | 3 | 8 | 13.5s | 0s | 22 | Restore nearby allies and patch the war wagon hull. |
| Frost Burst | spell | 9 | 8 | 15s | 3.2s | 16 | Burst a choke point and slow a whole enemy pack. |
| Lightning Strike | spell | 15 | 10 | 16s | 0s | 42 | Arc high damage through priority ranged or support targets. |
| Barrier Ward | spell | 23 | 10 | 18s | 4.5s | 0.72 | Ward a lane so allied units absorb less incoming damage. |
| Stone Barricade | spell | 5 | 10 | 20s | 4.5s | 60 | Raise a temporary stone wall that blocks enemy advance. |
| War Cry | spell | 12 | 12 | 22s | 5s | 1.35 | Rally all deployed units with a burst of attack and speed. |
| Earthquake | spell | 28 | 12 | 24s | 3.5s | 38 | Shake the ground across a wide area dealing damage and slowing enemies. |
| Polymorph | spell | 19 | 8 | 18s | 4s | 0 | Briefly disables one enemy. Ordinary targets resist repeated control; bosses recover sooner. |
| Resurrect | spell | 33 | 12 | 26s | 0s | 0.5 | Bring back the last fallen ally at half health. |
| Aegis Bolt | spell | 4 | 6 | 12s | 5s | 14 | Give one ally a small temporary shield pool. One target; does not protect the wagon or grant permanent defense. |
| Radiant Rebuke | spell | 7 | 6 | 14s | 0.3s | 10 | Interrupt a marked channel and deal modest direct damage. Poor raw damage; ordinary attacks are not channels. |
| Tidal Push | spell | 14 | 8 | 18s | 0.5s | 8 | Push affected enemies a short distance toward their stronghold. No large damage; displacement-resistant enemies move less. |
| Anchor Hex | spell | 18 | 8 | 18s | 2s | 0 | Temporarily prevent special leap, burrow, phase, or charge movement. Normal walking and attacks continue; it is not a full stun. |
| Molten Brand | spell | 24 | 10 | 18s | 3s | 12 | Briefly soften physical armor and apply a small burn. Less valuable against unarmored targets or magical wards. |
| Meteor | spell | 29 | 18 | 28s | 3s | 30 | After a visible delay, deal strong area damage and leave a brief fire patch. High mana cost; mobile enemies may leave the warning area. |
| Purifying Light | spell | 32 | 8 | 14s | 0s | 8 | Instantly remove blight, wounds, and burn from a few allies, with a small heal. No cleanse of root, sleep, or hostile terrain; modest healing. |
| Withering Seal | spell | 38 | 10 | 20s | 4s | 0 | Reduce healing received and ward regeneration for a short window. Low direct damage; does not strip an existing shield or stop the stronghold's repair. |
| Gale Step | spell | 43 | 6 | 14s | 1.5s | 0 | Move one allied troop a short distance along the battle band. No damage; interrupted setup must begin again, and committed melee cannot instantly jump to a new target. |
| Gravitation Well | spell | 48 | 12 | 22s | 3s | 0 | Briefly draw eligible enemies toward a marked center without changing their target allegiance. No lure and no damage by itself; heavy enemies move less. |
| Soul Harvest | spell | 53 | 4 | 22s | 0s | 4 | Consume up to three existing corpse tokens to recover mana. Needs corpses and an initial payment; empty casts are canceled for free. |
| Consecrated Ground | spell | 58 | 10 | 22s | 4s | 3 | Prevent hostile use of corpse tokens in the area for a short window. No damage or healing; friendly conversion can still consume those tokens. |
| Verdant Renewal | spell | 63 | 8 | 16s | 4s | 4 | Attach a short regeneration effect that follows its recipient. Slow recovery cannot save an ally from immediate lethal burst; wounds reduce it. |
| Bramble Snare | spell | 68 | 10 | 20s | 1.5s | 3 | Create visible roots that briefly bind a limited number of crossing enemies. Rooted enemies can still attack; ranged threats need another answer. |
| Chain Lightning | spell | 73 | 14 | 22s | 0s | 15 | Strike the primary target and jump to up to three different nearby enemies with falling damage. Weak against one isolated enemy; chain fails across large gaps. |
| Rallying Advance | spell | 78 | 10 | 18s | 4s | 1.08 | Remove root or movement slow and briefly boost movement with displacement resistance. No attack-damage boost and no cleanse of sleep, blight, or wounds. |
| Revealing Flare | spell | 83 | 6 | 12s | 5s | 5 | Reveal concealed enemies and identify the genuine body among illusions. No direct damage; a short local window rather than global detection. |
| Mirror Ward | spell | 88 | 12 | 24s | 5s | 0.5 | Deflect a limited number of direct projectiles back toward their origin at reduced damage. Melee, splash, and ground fields bypass the ward. |
| Banish | spell | 93 | 14 | 24s | 0s | 8 | Remove a limited number of temporary enemy summons or illusions; natural enemies lose one removable ward instead. Cannot delete natural elites, bosses, or hostile structures. |
| Royal Edict | spell | 98 | 16 | 28s | 2s | 0 | Briefly suppress eligible enemy special abilities and command auras. Enemies keep their ordinary attacks and movement; cast cost is high. |
| Palisade Kit | tactical | 6 | 6 | 18s | 10s | 0 | Builds a cheap wooden blocker after a short, interruptible setup. Buys time against rushers; weaker and slower to appear than Stone Barricade. |
| Firepot Mine | tactical | 25 | 10 | 20s | 8s | 32 | A ground trap explodes on the first enemy and leaves a short burning patch. Area denial; enemies can destroy the visible mine before triggering it. |
| Healing Shrine | tactical | 8 | 10 | 24s | 10s | 14 | A stationary shrine emits a limited number of healing pulses. Sustained recovery; cannot heal the wagon and can be destroyed. |
| Bolt Turret | tactical | 28 | 12 | 26s | 12s | 12 | A placed bolt-thrower fires a finite number of ordinary shots. Rear defense; does not attack structures and needs a safe position. |
| Supply Crate | tactical | 18 | 10 | 26s | 9s | 8 | A crate yields small courage packets while allies hold its immediate area. Trades mana for deployment reserve; enemies can break the crate. |
| Frost Trap | tactical | 44 | 8 | 20s | 8s | 8 | The first enemy triggers a short freeze and consumes the trap. Stops a dangerous advance; control resistance applies to elites. |
| Decoy Lantern | tactical | 84 | 8 | 22s | 5s | 0 | A placed lantern lures eligible nearby enemies briefly. Protects supports or gathers a pack; bosses resist and area damage destroys it. |
| Smoke Pot | tactical | 16 | 8 | 20s | 4s | 0 | A small smoke area breaks enemy target locks and conceals moving allies briefly. Enables retreat or setup; attacks reveal allies and hazards still hurt. |
| Purifying Censer | tactical | 36 | 10 | 24s | 8s | 0 | A temporary field removes one blight or wound per pulse. Allows healing through plague pressure; does not cleanse the entire battlefield. |
| Repair Cache | tactical | 21 | 10 | 26s | 8s | 16 | A placed cache near the wagon applies a few repair pulses while protected. Emergency repair; never increases maximum wagon health. |
| Grounding Rod | tactical | 55 | 8 | 22s | 8s | 0 | A placed rod interrupts one marked enemy channel in its radius, then burns out. Answers a summoner or null cast; must be positioned before completion. |
| War Horn | tactical | 48 | 6 | 18s | 5s | 0 | Marks one enemy near the aimed point as a temporary focus target for eligible allies. Prioritizes a healer or commander; never forces units through blockers. |
