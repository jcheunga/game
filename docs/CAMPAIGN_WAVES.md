# Campaign waves

Implemented · November 2026

The campaign's standard waves (`Waves` in `data/stages.json`) are written by `scripts/tools/design_waves.py`.
Until November 2026 every stage was built from one formula: three to six waves called Road patrol, Broken ranks,
Crossroads pressure and so on, twelve to fourteen seconds apart, each holding one or two of two to four foes. The
campaign fielded 14 of the 59 ordinary enemy types; the other 45 only appeared in endless mode. Now it fields all 59.

```sh
python3 scripts/tools/design_waves.py           # print what each stage would get
python3 scripts/tools/design_waves.py --write   # rewrite Waves, Description and enemy debut stages
```

It overwrites hand edits to the standard waves. After rewriting them, refit the campaign strength
(`scripts/analysis/campaign_fit.py`, see [the playthrough simulation](PLAYTHROUGH_SIM.md)): difficulty comes from
that table, fitted to whatever these waves are.

## Recipes

Each stage plays one recipe. Each zone deals its nine leader slots a shuffled hand, so neighbouring stages differ;
boss stages (slot 10, and slot 5 where a zone has a mid-boss) are commanders.

| Recipe | Waves | What it asks of the player |
| --- | --- | --- |
| Vanguard | Scouting party, Forward column, Flanking pack, Main host | A balanced host; hold the front and save courage for the main body |
| Swarm | First swarm, Rising tide, Teeming mass, Flood (spawns 0.4 s apart) | Many light bodies on short gaps; area spells and cheap troops |
| Raiders | Outriders, Hit and run, Raiding party, Wolfpack | Fast foes diving for the wagon; keep a screen in front |
| Shield line | Shield line, Second rank, Locked shields, Iron advance | Armored fronts with casters behind |
| Siege | Sappers, Siege train, Engines forward, Breaching party | Artillery, siege towers and sappers aimed at the wagon; push out early |
| Necromancy | Restless dead, Grave choir, Bone harvest, Dark rite | Summoners and death-spawners; kill the source first |
| Ambush | Quiet road, Stragglers, Ambush!, Closing jaws | Two light waves, then a large mixed surge |
| Elite | Picked men, Champions, Heavy guard, Elite cadre | Few, tough veterans; focus fire |
| Long march | Seven columns nine seconds apart | Steady pressure; pace the courage |
| Commander | Skirmishers, Honor guard, Bodyguard, "‹Boss› takes the field" | The boss and its escort |

Each stage first casts its foes: one per role (two of the light, fast and ranged roles), weighted towards the zone's
own regional foes and the newest arrivals. Every wave draws on that cast, so a stage is known by four to ten foes
rather than everything met so far. Counts grow with the zone: about 10–20 foes a stage in the first zone, 30–50 in
the last.

**The Reckoning.** From stage 4, a leader's last wave is a large mixed surge 110 seconds in (plus five a zone). Waves
come in order and wait for a crowded field, so it arrives only for a defence that hasn't broken the gate by then,
the way Dead Ahead sends a big wave when its timer runs out.

## Introductions

After the opening three stages, a stage introduces at most one foe, in its own short "First sighting" wave second.
Each zone brings in its regional foes (the units with its `HomeZone`) beside a share of the classic types; the zone's
headline debut stays on slot 4 (`NewEnemyId`). `CampaignDebutStage` in `data/units.json` records each foe's stage
(101 for foes the campaign doesn't field); the battle replaces a foe fielded before its debut with a Risen.

| Zone | Introductions (stage) |
| --- | --- |
| King's Road | Risen 1, Ghoul 2, Grave Brute and Blight Caster 3, Rustguard 4, Grave Rat 6, Rot Hulk 8 |
| Saltwake Docks | Salt Wraith 12, Bone Nest 13, Drowned Harpooner 14, Hookblade Raider 16, Sapper 17, Barnacle Guard 18 |
| Emberforge March | Ash Hound 22, Shield Wall 23, Cinder Spitter 24, Powder Bandit 26, Dread Herald 27, Furnace Knight 28 |
| Ashen Ward | Plague Leech 32, Hexer 33, Miasma Priest 34, Rot Surgeon 36, Bone Juggernaut 37, Sealbreaker 38, Tunneler 39 |
| Thornwall Pass | Thorn Brute 42, Mirror Knight 43, Cliff Stalker 44, Briar Witch 46, Bone Ballista 47, Avalanche Bearer 48 |
| Hollow Basilica | Chain Penitent 52, Lich 53, Bone Cantor 54, Ossuary Archer 56, Catacomb Giant 57, Reliquary Eye 58 |
| Mire of Saints | Bog Spore 62, Siege Tower 63, Swamp Maw 64, Bloated Eel 66, Bellringer 67, Drowned Colossus 68 |
| Sunfall Steppe | Dust Reaver 72, Revenant Captain 73, Bone Lancer 74, War Drummer 76, Highwayman 77, Carrion Banner 78 |
| Gloamwood Verge | Mirror Wisp 82, Plague Engine 83, Shade 84, Dream Weaver 86, Wicker Giant 88 |
| Crownfall Citadel | Crown Executioner 92, Null Magister 94, Soul Standard 96, Gate Breaker 98 |

The Horde and Regional wave versions in `data/waves/` (developer tools) are unchanged and still built from the old
standard waves.
