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
| Commander | A different fight for each boss (below) | The boss and its escort |

Each stage first casts its foes: one per role (two of the light, fast and ranged roles), weighted towards the zone's
own regional foes and the newest arrivals. Every wave draws on that cast, so a stage is known by four to ten foes
rather than everything met so far. Counts grow with the zone: about 10–20 foes a stage in the first zone, 30–50 in
the last.

**Zone casts (12 November 2026).** Until then Grave Rats were in 49 stages and Risen in 46: the campaign has only
five light rank-fillers and every recipe leans on them. Now each zone has its own two or three (`ZONE_FODDER`, with
light fast foes standing in: Ghouls in Emberforge and the Steppe, Tunnelers in Thornwall and the Mire), and every other foe is cast only for four zones from the one it debuts in (`CAST_ZONES`). The most-used
foe is now in 39 stages. A stand-in must not be a wagon-killer: Shades as Gloamwood fodder broke the wagon in 45
seconds on stage 85 and were taken out the same day.

**Commanders.** Each of the 14 boss stages has its own plan (`BOSS_PLANS`): the Grave Lord keeps the classic escort
and arrives last; the Tidecaller comes in second behind a swarm while the tides keep coming; the Harrow Tidemaster
stands behind a harpoon line; the Iron Warden leads from the first wave; the Plague Archon hides in a swarm; the
Plague Monarch brings its court and a last wave after it; the Thornwall Chieftain rides the first raid; the Bone
Pontiff closes a long procession; the Reliquary Tyrant shelters behind engines; the Mire Behemoth sets out alone at
the start and the rest overtake it; the Steppe Warlord charges in the third wave of cavalry; the Gloamwood Witch
springs an ambush; the Dread Sovereign fields only veterans; the Ashen Regent brings seven waves and a last host.

**Zone rules.** From the second zone, a zone's third, seventh and ninth stages carry its rule, a stage modifier
(`ZONE_RULES`): more foes at once in Saltwake, a burning strip in Emberforge, slower courage in the Ashen Ward,
reflected damage in Thornwall, rising dead in the Basilica, a weaker wagon hull in the Mire, faster waves on the
Steppe, a tougher gate in Gloamwood, stronger foes in the Citadel. The stage after each zone boss is a breather with
a rule in the player's favour (faster courage, protected fresh troops or a tougher hull, in turn). The briefing's
last sentence names the rule. The strength fit runs with the rules on, so they change how a stage plays, not how
hard it is overall.

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

## Signature moves, hazards and the rush budget (13 November 2026)

**Signature moves.** Every commander but the Grave Lord has one move of its own (`SignatureAbilityId` in units.json,
`BattleController.Strikes.cs`), used with every other rally, jam or raise call, starting with the first. Moves that
strike the ground draw a ring a second before they land; damage is a share of the commander's attack, so it follows
level and strength. Boss Rush uses the same commanders and so the same moves.

| Commander | Stage | Move |
| --- | --- | --- |
| Tidecaller | 15 | Undertow: a ring on your foremost troop that wounds and roots |
| Harrow Tidemaster | 20 | Tide barrage: three small rings on your foremost troops |
| Iron Warden | 30 | Iron skin: a shield worth 15% of its health for 6 seconds |
| Plague Archon | 35 | Plague breath: a wide ring in front of it that burns for 5 seconds |
| Plague Monarch | 40 | Pestilence: two rings that burn and halve healing |
| Thornwall Chieftain | 50 | Ground slam: a ring around it that wounds and roots |
| Bone Pontiff | 55 | Dark mass: heals foes near it by 15% |
| Reliquary Tyrant | 60 | Bone ward: shields foes near it for 20% of their health |
| Mire Behemoth | 70 | Quake: a very wide ring around it that wounds and throws back |
| Steppe Warlord | 80 | Blood frenzy: attack ×1.2 and speed ×1.25 for 5 seconds, taking 20% more damage |
| Gloamwood Witch | 90 | Slumber: rings on your two hardest hitters; asleep until struck |
| Dread Sovereign | 95 | Dread: troops near it strike 30% weaker and move slower for 5 seconds |
| Ashen Regent | 100 | Ashfall: burning rings under every other troop |

`UiReviewSmoke --commanders` plays every move and a hazard strike and captures them in `artifacts/commanders`.

**Hazards.** The last four zones each have one (`ZONE_HAZARDS`: Bog gas, Grass fire, Witch-light, Falling ash) on their
second, sixth and eighth stages, twelve stages in all, never together with a zone rule. Two spots on the road erupt in
turn every 14 seconds after 1.8 seconds of warning, and the spot drifts a little each time. A strike takes 18% of
maximum health from everything in the ring, friend and foe (a quarter of that from commanders), so luring a column
into a ring is as good as stepping out of one.

**Rush budget.** `RUSH_SHARE = .25`: fast foes that damage the wagon may bring no more than a quarter of the wagon's
health (one swing each) in the first 40 seconds of a stage that has no commander. See docs/PLAYTHROUGH_SIM.md.
