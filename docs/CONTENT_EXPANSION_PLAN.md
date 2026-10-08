# Crownroad — full roster expansion design

Design proposal · 8 October 2026 · Not implemented or balance-tested.

## Direction and target

Build Crownroad into a substantial collection game about the Lantern Caravan reclaiming a fallen kingdom. Recruitment, enemy discoveries, and equipment should keep revealing new tactics throughout the ten districts. The medieval siege identity anchors the roster: levies, mounted scouts, field healers, ward casters, engineers, mercenaries, blighted beasts, and the Rotbound Host.

| Collection | Existing | Additions | Designed total |
|---|---:|---:|---:|
| Playable unit cards | 19 | 31 | **50** |
| Non-boss enemy encounters | 19 | 41 | **60** |
| Combat bosses | 14 | 0 | **14** |
| Equipment relics | 33 | 67 | **100** |
| Spell cards | 10 | 20 | **30** |
| Tactical item cards | 0 | 12 | **12** |

The 60 non-boss encounters comprise **59 hostile enemy types and one optional Ash Pilgrim encounter**. Summoned minions, illusions, temporary blockers, and promoted titles do not inflate the collection count. There are 124 collectible/encounter roster definitions across allies, non-boss encounters, and bosses, plus separately authored summoned objects as needed.

Keep a battle squad at six unit cards. The proposed utility loadout has **five shared slots for spells and tactical items**: taking a turret or trap means leaving out one spell. These are reusable battle actions bought/unlocked once and paid for with mana on use; they are not a new inventory of consumables. This creates a composition choice without adding another row of battle controls. The armory can browse Spells and Tactical Gear separately while preparation shows one combined utility loadout.

Relics continue to use one slot initially and two after promotion. The eight relic families below are **collection/filter labels**, not automatic set bonuses. Each item works individually; equipping a second item combines two readable effects. No four-slot gear migration or random substat farming is assumed by this design.

## Design rules

1. Every new troop has a defining combat rule, a weakness, and a readable silhouette. Some accessible troops remain simple; complexity increases through the campaign.
2. Enemy variants have different counterplay, not just different health or tint. A threat must expose its charge, channel, stance, ward, or vulnerable window through animation and effects.
3. Common relics explain one useful relationship. Rare relics support a recognizable build. Epic and hardened relics enable more specialized combinations. Higher rarity does not always replace a lower-rarity counter item.
4. Most recruits provide an alternative to an established role. Existing Swordsman, Archer, War Hound, and other early recruits remain useful through cost, deployment cadence, and specific matchups.
5. Every required enemy answer has multiple sources: a unit, a utility card, positioning/timing, or a broadly obtainable relic. A premium recruit or rare drop must never be the only answer.
6. Keep the shallow battlefield, courage/mana loop, six-card squad, and established district progression. New companions and paired deployments consume the active-body budget; adding collection entries does not automatically increase battle density.

Prototype courage values below are starting design estimates, not final balance. Health, damage, cooldown, gold price, effect durations, and acquisition rates remain tuning work. New and existing units need comparison at the same progression level before final values are assigned.

## Shared mechanics that make the expanded roster possible

| Foundation | Proposed rule | Enables |
|---|---|---|
| Armor and wards | Physical armor and magical wards are explicit defenses with distinct visual cues. Ordinary defenses reduce rather than erase damage; rare immunity states have short, clear vulnerable windows. | Maceman, Wardbreaker, Rustguard, Furnace Knight, Reliquary Eye. |
| Status effects | Burn, chill/root, blight/wound, and conceal/reveal use a consistent duration/stack model. Blight applies attrition and healing penalties; it does not secretly alter several unrelated stats. | Flame Adept, Frost Witch, Plague Doctor, Shade, control and cleansing relics. |
| Control resistance | After hard control, a target gets a resistance window. Bosses shorten control and take capped alternatives to execution; no permanent freeze or interrupt loops. | Pikeman, Frost Witch, Briar Witch, Crown Executioner. |
| Shields and interception | A shield is a visible temporary health pool; damage reduction remains a separate effect. Interception redirects eligible projectiles, while Iron Sentinel's damage sharing has its own cap. | Shield Knight, Runesmith, Iron Sentinel, defensive relics. |
| Channels and weapon patterns | A charge, reload, setup, spray, or chant has a visible commitment and recovery. Only marked channels can be interrupted. | Longbowman, Javelin Skirmisher, Chanter, Trebuchet Crew, Bone Cantor. |
| Corpse tokens | Eligible natural enemy deaths can leave short-lived tokens. Summons and split bodies do not create additional tokens. Consumption removes the token for all competing effects. | Necromancer, Bone Cantor, Hollow Chalice, Gravewarden's Key. |
| Summons and companions | The owner has a strict cap and an origin record. Illusions do no damage. Summoned bodies do not generate free recruitment/refund loops, loot, or more corpse tokens. | Militia, Sapper Crew, Falconer, Dragon Tamer, Illusionist. |
| Tactical objects | Blockers, turrets, shrines, caches, and traps have a placement footprint, lifetime/charges, team, and destruction rule. Clearly distinguish visible ground hazards from solid blockers. | Engineer, Runesmith, Trapper, all twelve tactical items. |
| Relic triggers | A common event system handles deployment, committed hits, qualifying kills, healing, interrupts, and objectives. Each trigger has an owner, cooldown, eligibility, and stacking rule. | All conditional equipment effects. |
| Survival and return | One shared survival/return allowance per paid unit instance. Revivals preserve origin state; a fresh paid deployment is a fresh instance. Death-trigger equipment cannot repeat after a return. | Gravebound Knight, Resurrect, Phoenix Ember, lethal-save relics. |

Resource-granting equipment uses actual paid courage and qualifying natural enemy kills. A shared objective reward pays once per objective across the whole squad. Damage caused by a proc cannot recursively generate itself, another identical proc, reflection loops, or repeated lifesteal. Enemy summons have a bounded reward budget rather than unlimited farmable income.

## The 50-unit warband

The complete warband has **13 Frontline, 12 Recon, 13 Support, and 12 Breach cards**. Role tags remain compatible with the current four squad synergies.

All existing units remain in the collection. The tables distinguish their planned identities from the current implementation documented in [the comparison](/Users/jason/side/game/docs/DEAD_AHEAD_COMPARISON.md).

| Existing unit | Role | Planned identity / differentiation |
|---|---|---|
| Swordsman | Frontline | Keep the generalist and cleave; distinct from Militia bodies and Maceman armor break. |
| Archer | Support | Keep cheap single-shot ranged support and volley; Longbowman trades tempo for charged shots. |
| Shield Knight | Frontline | Add a limited allied projectile interception stance; Lantern Guard remains the durable splash fighter. |
| Spearman | Frontline | Keep reach and armor-ignoring thrust; Pikeman specializes in braced charge interception. |
| Crossbowman | Recon | Keep quick precision shots and farthest-target special; Longbowman has a charged opening and poor mobility. |
| Cavalry Rider | Recon | Keep the line charge; Outrider intercepts defensively and Mounted Archer fires while moving. |
| Siege Engineer | Support | Keep wagon repair and add one temporary repairable bolt emplacement; Runesmith specializes in allied wards and constructed objects. |
| Mage | Recon | Make Arcane Beam pierce a line; Stormcaller owns chained hits between enemies. |
| Halberdier | Breach | Keep melee siege damage and sweep; Wardbreaker removes magical protection rather than physical armor. |
| Alchemist | Breach | Keep direct lobbed splash with a short splash-debuff special; Firepot Thrower owns lasting ground fire. Correct burning-ground lore unless intentionally added. |
| Battle Monk | Support | Keep a combat aura and periodic area healing; Field Medic is the dedicated single-patient healer. |
| War Hound | Recon | Keep cheapest fast expendable scout; Militia adds two slower bodies at higher total cost. |
| Banner Knight | Support | Keep nearby offensive aura and rally; Chanter improves ability recovery rather than passive damage. |
| Necromancer | Breach | Implement corpse-token conversion into capped temporary allied Risen; no tokens or resource refunds from summoned bodies. |
| Rogue | Recon | Keep backline targeting and a true next-strike Vanish bonus; Duelist owns defensive melee parries. |
| Berserker | Frontline | Keep missing-health damage; implement real attack recovery acceleration for frenzy. |
| Lantern Guard | Frontline | Keep splash and toughness; replace duplicated Shield Wall special with a short defensive sweep that protects its own position. |
| Ballista Crew | Breach | Keep mobile structure-hitting bolts; replace duplicated Snipe with a marked armor-piercing siege shot. |
| Stormcaller | Support | Make ordinary attacks or special chain through a limited number of packed enemies; do not reuse Mage beam. |

### The 31 new recruits

Discovery stages reveal recruitment and a preview of the troop's job. They do not force the player to purchase every recruit. An optional local contract can grant a selected recruit or a useful alternative reward. District contracts should offer a meaningful choice rather than dozens of separate mandatory upgrade tracks.

#### King's Road

| Recruit / discovery estimate | Defining rule | Weakness | Visual brief |
|---|---|---|---|
| **Militia** (Frontline)<br>Stage 3 · 12 courage | Two fragile levy soldiers deploy together; one card cooldown and one courage payment. | Splash attacks punish the pair; weak sustained damage. | Two kettle helmets and mismatched short spears. |
| **Maceman** (Frontline)<br>Stage 6 · 24 courage | Every third committed hit briefly breaks armor on one target. | Short reach and slow windup; vulnerable to rear casters. | Heavy round mace and dented half-plate. |
| **Field Medic** (Support)<br>Stage 8 · 30 courage | Prioritizes one injured ally and channels repeated healing instead of attacking. | Needs a protected patient; interruption stops the channel. | Satchel, white sleeve, folding field staff. |

#### Saltwake Docks

| Recruit / discovery estimate | Defining rule | Weakness | Visual brief |
|---|---|---|---|
| **Longbowman** (Recon)<br>Stage 12 · 32 courage | Charges a powerful straight shot while stationary; loses charge when displaced. | Slow opening shot and poor close defense. | Tall asymmetric bow and salt-blue hood. |
| **Javelin Skirmisher** (Recon)<br>Stage 15 · 22 courage | Throws once, retreats a short distance, then commits to melee. | Limited ammunition and one retreat; not sustained ranged damage. | Three visible javelins and small round shield. |
| **Quartermaster** (Support)<br>Stage 18 · 28 courage | Nearby allied kills earn small courage packets on a shared cooldown. | Low damage; no reward from summoned allies or artificial targets. | Bulky supply backpack and tally board. |

#### Emberforge March

| Recruit / discovery estimate | Defining rule | Weakness | Visual brief |
|---|---|---|---|
| **Iron Sentinel** (Frontline)<br>Stage 22 · 44 courage | Braces while stationary and redirects a capped part of nearby allies' direct damage to itself. | Slow advance; area hazards and blight bypass redirection. | Wide rectangular shield and broad iron shoulders. |
| **Firepot Thrower** (Breach)<br>Stage 25 · 38 courage | Lobs pots that leave short-lived burning patches. | Slow projectiles; fire-resistant foes and forced movement. | Clay pots, padded sleeves, furnace-orange straps. |
| **Runesmith** (Support)<br>Stage 27 · 34 courage | Places temporary wards on allies; repairs nearby player-built tactical objects. | Small ward radius and low personal damage. | Portable anvil and suspended rune plates. |
| **Flame Adept** (Breach)<br>Stage 29 · 40 courage | Sweeps a short cone of flame, applying capped burn stacks to clustered enemies. | Short range; poor isolated-target damage and fire wards. | Hand brazier and fan-shaped flame focus. |

#### Ashen Ward

| Recruit / discovery estimate | Defining rule | Weakness | Visual brief |
|---|---|---|---|
| **Plague Doctor** (Support)<br>Stage 32 · 30 courage | Cleanses blight and healing reduction from one ally, with modest recovery. | Weak raw healing; cannot erase terrain hazards everywhere. | Beaked mask, censer flask, ivory gloves. |
| **Flagellant** (Frontline)<br>Stage 35 · 18 courage | On death, grants a brief rally to nearby living allies; never triggers from revived or summoned copies. | Fragile; deaths consume courage and cannot sustain a free loop. | Chain flail, bandaged arms, small amber votives. |
| **Paladin** (Frontline)<br>Stage 38 · 42 courage | After protecting a wounded ally, charges one radiant strike and an ally shield. | High cost and long support cooldown; spread formations waste protection. | Sunburst shield, white tabard, short radiant sword. |

#### Thornwall Pass

| Recruit / discovery estimate | Defining rule | Weakness | Visual brief |
|---|---|---|---|
| **Pikeman** (Frontline)<br>Stage 42 · 26 courage | Braces against a telegraphed charge and interrupts the first charger reaching its spear tip. | Can be flanked; loses brace while moving or pulled. | Extra-long pike, foot brace, narrow helmet. |
| **Trapper** (Recon)<br>Stage 45 · 24 courage | Leaves visible snares behind its advance, with at most two active traps. | Enemy artillery clears traps; snares do not stop incorporeal foes. | Coiled wire, stake bundles, compact crossbow. |
| **Earthshaper** (Breach)<br>Stage 48 · 46 courage | Raises a temporary stone obstacle and sends a slow ground shock through packed enemies. | Long cast; mobile backline threats bypass the obstacle. | Stone gauntlets and stacked slate shoulder plates. |

#### Hollow Basilica

| Recruit / discovery estimate | Defining rule | Weakness | Visual brief |
|---|---|---|---|
| **Gravebound Knight** (Frontline)<br>Stage 52 · 38 courage | Returns once after death with reduced health and permanently reduced speed. | Corpse denial prevents return; further revival effects cannot reset the charge. | Cracked armor with a single teal heart-light. |
| **Spirit Keeper** (Support)<br>Stage 55 · 34 courage | Stores limited charges from nearby allied deaths and spends them to shield living allies. | Needs attrition nearby; no charges from summons or repeated revivals. | Lantern cage holding three visible spirit beads. |
| **Wardbreaker** (Breach)<br>Stage 58 · 40 courage | Melee strikes remove temporary magical wards; its special interrupts a channel. | Must reach the caster; cannot bypass physical armor. | Forked rune hammer and broken ward fragments. |

#### Mire of Saints

| Recruit / discovery estimate | Defining rule | Weakness | Visual brief |
|---|---|---|---|
| **Druid** (Support)<br>Stage 62 · 32 courage | Grows a small healing grove and a root patch that slows ground enemies. | Stationary patches can be avoided or burned away. | Branch staff, moss cloak, seed satchel. |
| **Frost Witch** (Breach)<br>Stage 65 · 40 courage | Builds chill on one target; a threshold briefly freezes it, with control resistance afterward. | Slow buildup; resistant elites shorten the freeze. | Ice spindle and sharply pointed pale-blue silhouette. |
| **Sapper Crew** (Breach)<br>Stage 68 · 44 courage | A protected two-person crew plants a timed charge against a structure or fortified target. | Planting can be interrupted; poor ordinary melee defense. | Demolition barrel between two hooded engineers. |

#### Sunfall Steppe

| Recruit / discovery estimate | Defining rule | Weakness | Visual brief |
|---|---|---|---|
| **Outrider** (Recon)<br>Stage 72 · 22 courage | Intercepts the first enemy nearing the wagon, then becomes a fast light melee unit. | Only one interception per instance; weak against heavy tanks. | Lean horse, short saber, rear-facing horn. |
| **Mounted Archer** (Recon)<br>Stage 75 · 34 courage | Fires while advancing between shots, then makes one evasive sidestep on contact. | Lower precision; vulnerable to roots and spear charges. | Small horse, short recurve bow, fluttering pennant. |
| **Executioner** (Frontline)<br>Stage 78 · 34 courage | Heavy swings finish ordinary enemies below a health threshold; bosses take a capped bonus hit. | Long windup; bad against swarms and reflection. | Broad cleaver and dark half-mask. |

#### Gloamwood Verge

| Recruit / discovery estimate | Defining rule | Weakness | Visual brief |
|---|---|---|---|
| **Falconer** (Recon)<br>Stage 82 · 28 courage | A single falcon reveals and marks a distant support enemy for allied focus. | The bird does little damage; loses value against simple melee packs. | Armored glove, hooded bird, feathered shoulder. |
| **Duelist** (Recon)<br>Stage 85 · 26 courage | Parries one telegraphed melee strike, then ripostes into the same enemy. | Projectiles and simultaneous attacks overwhelm the parry. | Narrow rapier, off-hand dagger, short cape. |
| **Illusionist** (Support)<br>Stage 88 · 38 courage | Creates one fragile decoy that redirects eligible enemies for a short window. | Area attacks clear the decoy; bosses resist the lure. | Split mirror focus and translucent duplicate silhouette. |

#### Crownfall Citadel

| Recruit / discovery estimate | Defining rule | Weakness | Visual brief |
|---|---|---|---|
| **Chanter** (Support)<br>Stage 92 · 32 courage | Completes a short chant to advance nearby allies' automatic ability recovery. | Interrupted chants give no benefit; does not speed every deployment card. | Hand bell, prayer strips, tall travel hood. |
| **Trebuchet Crew** (Breach)<br>Stage 95 · 56 courage | Sets up before launching slow siege stones with a wide impact area. | Very poor relocation; backline dives and interrupts stop setup. | Compact counterweight arm and wheeled timber frame. |
| **Dragon Tamer** (Breach)<br>Stage 98 · 60 courage | Deploys with one small pyre drake; directs a limited cone attack through a marked pack. | Very expensive; losing the tamer removes the drake's command attacks. | Low drake, iron muzzle, handler's hooked staff. |

## The 60 non-boss encounters

Three broad enemy families give the kingdom more variety:

- **Rotbound Host:** undead, spirits, and cursed siege constructs. Corpses, wards, hostile ritual support, and ruined heraldry distinguish them.
- **Grey Banner:** living mercenaries and human cult auxiliaries. Gray cloth, practical weapons, retreats, reloads, and coordination distinguish them. They do not all turn into undead on death.
- **Blightwild:** diseased wildlife, plants, and warped creatures. Low silhouettes, leaps, burrows, and terrain interactions distinguish them.

The Ash Pilgrim is a separate optional encounter. The first implementation can keep hostile families on the existing enemy combat side. Independent faction infighting is a later encounter feature, not a prerequisite for this roster.

### Existing enemy identities

| Existing enemy | Planned identity / distinction |
|---|---|
| Risen | Retain the basic teaching enemy. |
| Ghoul | Retain fast fragile pressure. |
| Rot Hulk | Retain death blast; use a clearly labeled variant only if enabling enemy collateral damage. |
| Grave Brute | Retain the uncomplicated heavy melee body. |
| Blight Caster | Retain ordinary ranged pressure; specialized blight belongs to Miasma Priest. |
| Bone Nest | Retain two-body death split; split bodies are not corpse-token or reward farms. |
| Sapper | Retain wagon rush; correct suicide-bomber lore or add a separately telegraphed one-use strike. |
| Dread Herald | Retain passive nearby damage/movement buff; War Drummer uses interruptible attack tempo instead. |
| Hexer | Retain global deployment disruption with its recovery window; Null Magister uses a local field. |
| Bone Juggernaut | Retain a readable physical tank; adopt explicit armor once counters are taught. |
| Shield Wall | Retain formation projectile interception. |
| Lich | Retain periodic simple reinforcements; Bone Cantor requires corpse tokens and channels for an elite. |
| Siege Tower | Retain the moving reinforcement carrier. |
| Mirror Knight | Retain general direct-damage reflection; Thorn Brute reflects only contact in a visible stance. |
| Tunneler | Retain movement behind the rearmost troop; Cliff Stalker selects ranged-primary targets and telegraphs a leap. |
| Bone Ballista | Retain long-range ordinary artillery. |
| Catacomb Giant | Retain splash melee and three-body death split. |
| Revenant Captain | Add a periodic visible escort rally instead of only a different Herald stat profile. |
| Plague Engine | Retain heavy artillery; optionally add a short marked blight patch after the status system exists. |

### The 41 new encounters

Each district gets four new hostile types. Introduce them separately before mixing their rules. The optional Ash Pilgrim can appear on authored routes without replacing a required enemy pack.

#### King's Road

| Enemy | Threat rule | Counterplay | Visual brief |
|---|---|---|---|
| **Grave Rat**<br>Blightwild · Rush | Small packs slip around a single isolated blocker. | Use a cheap screen or area damage. | Low rat silhouettes with bone tails. |
| **Rustguard**<br>Rotbound Host · Armor | Weak attacks but frontal physical armor; rear or armor-breaking hits expose it. | Maceman, magic, or surround with several bodies. | Rust-red shield held directly forward. |
| **Bellringer**<br>Rotbound Host · Support | Channels a visible bell call that recruits a small pack; interrupting it prevents the call. | Ranged focus or an interrupt. | Large hanging bell and thin skeletal bearer. |
| **Highwayman**<br>Grey Banner · Ranged | A living bandit shoots once, retreats, and then draws a knife. | Cheap fast melee; pin it before it retreats. | Crossbow, torn gray scarf, crooked hat. |

#### Saltwake Docks

| Enemy | Threat rule | Counterplay | Visual brief |
|---|---|---|---|
| **Drowned Harpooner**<br>Rotbound Host · Displacement | Telegraphs a harpoon that pulls the foremost allied unit out of formation. | Interrupt, barrier, or displacement protection. | Very long harpoon and trailing rope. |
| **Salt Wraith**<br>Rotbound Host · Phase | Briefly phases through a blocker; becomes vulnerable before attacking. | Keep a second defender near ranged troops. | Tattered sailcloth and a hollow blue face. |
| **Barnacle Guard**<br>Rotbound Host · Armor | A shell plate absorbs a limited number of direct hits before breaking. | Fast hits, armor break, or a timed heavy follow-up. | Broad shell-covered shoulders and anchor shield. |
| **Hookblade Raider**<br>Grey Banner · Diver | Runs past a moving frontline to attack a nearby support troop; ordinary blockers still stop it. | A held reserve defender or crowd control. | Twin hooks and short dockworker coat. |

#### Emberforge March

| Enemy | Threat rule | Counterplay | Visual brief |
|---|---|---|---|
| **Ash Hound**<br>Blightwild · Charger | Pauses with a bright coal glow before one rapid charge. | Pikeman, barrier, or a shot during the windup. | Low coal-black dog with bright jaws. |
| **Cinder Spitter**<br>Rotbound Host · Hazard | Its arcing shot leaves a short burning patch instead of persistent unavoidable damage. | Spread troops, cleanse burn, or kill the caster. | Furnace pouch in a skeletal rib cage. |
| **Furnace Knight**<br>Rotbound Host · State | Armor hardens while glowing; cools after its heavy swing. | Burst during cooling or interrupt the windup. | Furnace visor with clearly opening vents. |
| **Powder Bandit**<br>Grey Banner · Trap | Plants a visible powder keg, then retreats; the keg detonates after a delay. | Destroy the keg from range or move the fight. | Powder satchel, stubby torch, gray mask. |

#### Ashen Ward

| Enemy | Threat rule | Counterplay | Visual brief |
|---|---|---|---|
| **Plague Leech**<br>Blightwild · Parasite | Clings to an enemy tank and grants it limited healing; detaches when the host dies. | Area damage or kill the exposed leech during attachment. | Bright leech visibly attached to a larger body. |
| **Miasma Priest**<br>Rotbound Host · Debuff | Creates a marked cloud that reduces healing and applies capped blight. | Plague Doctor, Purifying Censer, or immediate caster focus. | Long incense staff and green cloth veil. |
| **Rot Surgeon**<br>Rotbound Host · Healer | Stops to heal one damaged enemy instead of attacking. | Interrupt or isolate the healer from its patient. | Bone saw, narrow medical apron, dripping vial. |
| **Sealbreaker**<br>Grey Banner · Counter-support | Channels a lance that strips one temporary ward or shield. | Interrupt, attack from another direction, or use raw health. | Human masked zealot with a forked gray spear. |

#### Thornwall Pass

| Enemy | Threat rule | Counterplay | Visual brief |
|---|---|---|---|
| **Cliff Stalker**<br>Blightwild · Diver | Crouches before leaping onto a ranged-primary unit. | Reserve melee or a trap near the backline. | Bent goatlike legs and hooked foreclaws. |
| **Thorn Brute**<br>Rotbound Host · Retaliation | Reflects melee contact damage while thorns are raised; drops them after swinging. | Ranged damage or attack during its exposed recovery. | Spiked arms that visibly fold down. |
| **Briar Witch**<br>Rotbound Host · Control | Telegraphs a root around one allied troop; damage and cleansing break it early. | Ranged focus or cleanse; controls cannot chain indefinitely. | Crooked branch crown and thorn spool. |
| **Avalanche Bearer**<br>Blightwild · Artillery | Throws a slow stone toward a marked cluster, briefly separating survivors. | Interrupt its lift or deploy away from the mark. | Hunched mountain ogre carrying an angular boulder. |

#### Hollow Basilica

| Enemy | Threat rule | Counterplay | Visual brief |
|---|---|---|---|
| **Chain Penitent**<br>Rotbound Host · Displacement | Tethers one frontline ally, limiting how far it can advance until the chain breaks. | Damage the penitent or sever the tether with a ward-breaking effect. | Dragging iron chain and candle-lined hood. |
| **Ossuary Archer**<br>Rotbound Host · Piercing | Its slow spine arrow penetrates the first unit and hurts the next. | Spread supports; a shield interceptor absorbs the arrow. | Tall bone bow and quiver of long white spines. |
| **Bone Cantor**<br>Rotbound Host · Summoner | Consumes nearby enemy corpse tokens to raise one tougher servant after a chant. | Interrupt or deny the corpses. | Jaw-shaped choir mask and floating bone notes. |
| **Reliquary Eye**<br>Rotbound Host · Puzzle | A stationary warded eye opens after releasing a bolt, creating a brief attack window. | Timed damage or Wardbreaker; never requires a single specific unit. | Stone orb with a plainly opening amber iris. |

#### Mire of Saints

| Enemy | Threat rule | Counterplay | Visual brief |
|---|---|---|---|
| **Swamp Maw**<br>Blightwild · Ambush | A visible mound waits before erupting beneath an advancing troop. | A disposable scout or ranged hit on the mound. | Wide toothed puddle and low mud mound. |
| **Bog Spore**<br>Blightwild · Death hazard | Bursts into a short blight cloud that also harms nearby enemies. | Kill from range; exploit its position in the enemy pack. | Round fungus sac and unmistakable green spots. |
| **Drowned Colossus**<br>Rotbound Host · Terrain | Regenerates slowly inside its own marked water patch; lurches out to attack. | Force it out, deny healing, or burst after the lurch. | Water barrel chest and trailing weed skirt. |
| **Bloated Eel**<br>Blightwild · Evasion | Makes one sharp dodge of an incoming projectile before committing to melee. | Fast second shot, splash, or a melee screen. | Sinuous low body with swollen pale gills. |

#### Sunfall Steppe

| Enemy | Threat rule | Counterplay | Visual brief |
|---|---|---|---|
| **Bone Lancer**<br>Rotbound Host · Charger | Winds up a long straight charge that ends on the first solid blocker. | Pikeman, Stone Barricade, or interrupt during the windup. | Dead mount and long crimson lance. |
| **Dust Reaver**<br>Grey Banner · Skirmisher | Alternates a short javelin volley with a vulnerable reload before retreating once. | Advance during reload or pin it with a root. | Layered gray scarves and three javelins. |
| **Carrion Banner**<br>Rotbound Host · Support | Consumes a limited number of enemy corpses to empower its nearby escorts. | Kill the bearer or remove corpse tokens. | Ragged carrion flag with three hanging skulls. |
| **War Drummer**<br>Grey Banner · Tempo | Channels a drumbeat that advances nearby enemies' attack recovery. | Interrupt the beat or separate the drummer from its pack. | Large hip drum and short gray mercenary cloak. |

#### Gloamwood Verge

| Enemy | Threat rule | Counterplay | Visual brief |
|---|---|---|---|
| **Shade**<br>Rotbound Host · Stealth | Fades during approach, then becomes targetable before its first strike. | Revealing light, traps, or a held rear guard. | Thin black silhouette with bright hands appearing before attack. |
| **Mirror Wisp**<br>Rotbound Host · Illusion | Creates one damage-free copy that can draw target locks until hit. | Splash or revealing light; the real wisp has a distinct core. | Twin wisps, only one with a solid violet core. |
| **Wicker Giant**<br>Rotbound Host · Fortified | Resists physical attacks while its wicker shell is intact; burn weakens the shell. | Fireball, Flame Adept, or Firepot Thrower. | Open wicker lattice with a buried ember heart. |
| **Dream Weaver**<br>Blightwild · Control | Marks one support troop for sleep; any damage wakes it, and repeated sleep is resisted. | Cleanse, interrupt, or a decoy that draws the cast. | Large hanging moth wings and a glowing spindle. |

#### Crownfall Citadel

| Enemy | Threat rule | Counterplay | Visual brief |
|---|---|---|---|
| **Gate Breaker**<br>Rotbound Host · Siege | Stops to smash a player blocker, causing a small blast behind it; weak against troops away from structures. | Intercept in open ground or damage it during the windup. | Broad ram-shaped skull and heavy two-handed maul. |
| **Crown Executioner**<br>Grey Banner · Elite | A marked heavy swing wounds one target, briefly reducing healing received. | Parry, interrupt, or cleanse the wound. | Gray royal half-cape and massive black cleaver. |
| **Null Magister**<br>Grey Banner · Counter-magic | Places a visible null field that suppresses magical ward and spell effects locally. | Physical attackers, interrupt, or cast outside the field. | Flat gray rune disc and dark academic coat. |
| **Soul Standard**<br>Rotbound Host · Revival | Stores one elite enemy's soul and returns it once unless the standard is destroyed. | Kill the standard first or deny the marked corpse. | Tall soul-cage banner with one clearly lit vessel. |

#### Optional encounter — Ash Pilgrim

A rare non-attacking traveler offers a visible salvage cache after a brief protected passage. Optional escort decision; no penalty for ignoring it and no reward for killing it. Visual: Tiny bowed traveler carrying a large dim lantern.

## Keep the 14 commanders and sharpen their encounters

The existing count is **ten zone-ending bosses plus four mid-zone bosses**. When the campaign was expanded to 100 stages, four later/postgame bosses became their district's finales, while the original bosses stayed at local stage 5. This is explicit in the [campaign generator](/Users/jason/side/game/scripts/analysis/expand_campaign.py:1) and [campaign plan](/Users/jason/side/game/CAMPAIGN_PLAN.md).

| Zone with an extra commander | Mid-zone boss | Zone-ending boss |
|---|---|---|
| Saltwake Docks | Stage 15 — Tidecaller | Stage 20 — Harrow Tidemaster |
| Ashen Ward | Stage 35 — Plague Archon; returns at 37 | Stage 40 — Plague Monarch |
| Hollow Basilica | Stage 55 — Bone Pontiff | Stage 60 — Reliquary Tyrant |
| Crownfall Citadel | Stage 95 — Dread Sovereign | Stage 100 — Ashen Regent |

The other six districts each have one zone-ending boss. Plague Archon's return is another encounter with the same boss type, not a fifteenth unique boss. Preparation and codex should distinguish **zone bosses** from **mid-zone commanders**. Dread Sovereign is the stage-95 commander; Ashen Regent is the actual campaign finale, even though some older codex lore still calls the Sovereign the final enemy.

No additional bosses are needed to reach the target; the expansion emphasizes ordinary enemies. These are proposed refinements to the existing commanders, preserving their identity and IDs:

| Commander | Signature encounter to build toward |
|---|---|
| Grave Lord | A visible reinforcement bell can be interrupted; surviving the call creates a short guarded phase. |
| Tidecaller | Harpoon escorts pull one protector away before a telegraphed Undertow. |
| Harrow Tidemaster | Alternates a marked flooded area and a dry attack window; artillery escorts make timing matter. |
| Iron Warden | Furnace armor opens after a heavy swing; interrupting the forge escort extends the opening. |
| Plague Archon | Cleansable local plague areas accompany its bounded deployment disruption. |
| Plague Monarch | Captains sustain a clearly shown ward; removing one opens a reliable offensive window. |
| Thornwall Chieftain | Telegraphs a charge corridor; a blocker, brace, or interruption can stop the stampede. |
| Bone Pontiff | Corpse-consuming ritual can be denied; the player chooses between clearing bodies and attacking the pontiff. |
| Reliquary Tyrant | A visible reliquary seal opens after its artillery volley; interrupting the guard keeps it open longer. |
| Mire Behemoth | Leaves its regeneration pool to attack; punish the movement or bring healing denial. |
| Steppe Warlord | Drummer escorts prepare a fast reinforcement charge, with an interruptible beat. |
| Gloamwood Witch | Creates a visibly identifiable false body; reveal and area attacks provide different answers. |
| Dread Sovereign | Its standard protects elite escorts; destroy the standard or outlast its finite soul charge. |
| Ashen Regent | Recombines previously taught ward, charge, and corpse rules in clearly separated phases. |

These are authored battle interactions, not extra tutorial text over combat. Preparation and codex explain rules; battlefield animation, warning shapes, objects, and numbers communicate execution.

## The 100-relic collection

Existing relics retain their IDs and ownership. Reworking an item's behavior should trade against its existing stat budget, not append a powerful trigger to all current multipliers unchanged. Basic stat equipment still provides accessible choices.

### Existing 33 relics

| Existing relic | Rarity | Proposed identity |
|---|---|---|
| Iron Pendant | common | Keep the current simple stat identity; accurately describe its actual bonuses. |
| Sharpened Edge | common | Keep the current simple stat identity; accurately describe its actual bonuses. |
| Swift Boots | common | Keep the current simple stat identity; accurately describe its actual bonuses. |
| Battle Drum | common | Keep the current simple stat identity; accurately describe its actual bonuses. |
| Guardian Shield | rare | Add a small deployment shield; trade some raw stats for the effect. |
| War Brand | rare | Reward a completed armor break with a brief personal damage window. |
| Windrunner Cloak | rare | Reduce slow duration, distinct from periodic cleansing. |
| Sage's Ring | rare | After a player spell, advance the wearer's automatic ability recovery on a cooldown. |
| Crown of Valor | epic | A qualifying elite kill briefly rallies a few nearby allies. |
| Blade of Ruin | epic | Offer a strong periodic hit at a capped health cost that cannot directly kill the wearer. |
| Phantom Mantle | epic | Dodge one direct projectile after a visible recharge; area attacks still work. |
| Dragon Heart | epic | A low-health threshold emits one small burn pulse per deployment. |
| Wolftooth Charm | common | Add a small benefit while fighting beside another beast troop; keep a useful base effect. |
| Tombstone Shard | rare | Restore a little health after a qualifying kill, on a cooldown. |
| Stormcaller Sigil | rare | Improve existing chained attacks without granting chains to every weapon. |
| Siege Hammer | rare | Keep the current simple stat identity; accurately describe its actual bonuses. |
| Spectral Lantern | epic | Weaken nearby blight duration; avoid duplicating Gloam Lantern's stealth reveal. |
| Immortal Wreath | epic | Increase healing received while critically wounded rather than promising unconditional immortality. |
| Frostbound Crown | epic | Emit a small chill pulse after a completed unit ability, with a cooldown. |
| Moonfire Talisman | epic | A qualifying kill empowers one later strike; cannot refresh indefinitely. |
| Hardened Bulwark | hardened | Cap a fraction of the first heavy direct hit taken each battle. |
| Hardened Fang | hardened | Reward a successful interrupt with brief real attack recovery acceleration. |
| Hardened Sigil | hardened | Convert a limited periodic hit to arcane damage; reduce universal stats accordingly. |
| Hardened Crown | hardened | Extend the wearer's qualifying temporary buffs, with a duration cap. |
| Hardened Soul | hardened | Gain a capped personal momentum bonus after qualifying kills; resets on death. |
| Grave Lord's Crown | epic | A qualifying corpse conversion grants one temporary servant, with a strict summon cap. |
| Iron Warden's Heart | epic | Regenerate a small shield only after a safe pause between attacks. |
| Sovereign's Mantle | epic | Periodically rally at most a few nearby living allies. |
| Plague Censer | epic | A completed unit ability cleanses one nearby blighted ally on a cooldown. |
| Tower Sentinel's Ward | epic | A battle-start ward protects against the first marked hazard; retain broad usefulness outside Tower. |
| Ascendant's Signet | epic | Participation in a field objective gives a capped personal momentum bonus. |
| Apex Talisman | hardened | Improve damage against named elites, with no percentage-health boss damage. |
| Pinnacle Crown | hardened | The first qualifying unit ability repeats once at reduced power; the echo cannot trigger item effects. |

### New 67 relics

The first eight families each contain six additions, for 48 new family relics. Nineteen signature relics complete the 67 additions. A relic's best-use suggestion is a browsing aid; any hard compatibility requirement must appear plainly in its description and equip preview.

### Vanguard

| Relic | Rarity | Defining effect | Best use and limit |
|---|---|---|---|
| Rampart Seal | common | Creates a small personal shield on deployment. | Frontline. Single shield; no refresh from summons. |
| Tower Charm | common | Briefly reduces direct damage while the wearer has remained stationary. | Holding troops. Benefit ends when moving. |
| Last Wall Token | rare | Gains temporary defense on crossing a low-health threshold. | Tanks. Once per deployment; healing cannot repeatedly retrigger it. |
| Iron Prayer | rare | Every fourth melee hit restores a little of the wearer's health. | Sustained melee. No healing from structures or artificial targets. |
| Sentinel Buckle | epic | Grants a ward after successfully intercepting a charge. | Pikeman or Outrider. Only genuine intercepts qualify; shared internal cooldown. |
| Oathbound Chain | epic | Shares a capped portion of received healing with the nearest injured ally. | Paired frontline. Shared healing cannot generate another share. |

### Scouts

| Relic | Rarity | Defining effect | Best use and limit |
|---|---|---|---|
| Farwatch Lens | common | Increases shot damage with distance up to a modest cap. | Long-range troops. No bonus at close range. |
| Windstring | common | Shortens the wearer's first shot windup after deployment. | Slow shooters. One activation per deployment. |
| Falcon Feather | rare | The first hit marks one target for a short allied focus bonus. | Precision troops. One mark per wearer; marks do not multiply. |
| Huntsman's Knot | rare | Boosts damage against an enemy isolated from its allies. | Duelist or ranged pickoff. No benefit in dense packs. |
| Trailfinder Boots | epic | A qualifying kill grants a brief movement burst. | Fast melee. Cooldown prevents permanent acceleration. |
| Twinbolt Clasp | epic | Every fifth ordinary projectile releases a weaker second bolt. | Single-shot ranged. Extra bolts cannot trigger themselves or duplicate summons. |

### Siege

| Relic | Rarity | Defining effect | Best use and limit |
|---|---|---|---|
| Breach Writ | common | The wearer's first structure strike briefly exposes that structure to allied siege damage. | Structure attackers. One exposure per structure; cannot make non-siege shots hit bases. |
| Demolition Rune | common | Deals a small secondary blast when the wearer destroys a tactical blocker. | Breach troops. No trigger from timed expiration or friendly dismantling. |
| Counterweight | rare | Every third artillery shot has a larger impact area and a longer windup. | Ballista or Trebuchet. Artillery only; no attack-speed stacking shortcut. |
| Gateward Medallion | rare | Reduces damage received from marked siege attacks. | Rear guard. No protection against ordinary melee or blight. |
| Sapper's Fuse | epic | Leaves one short-fuse bomb when the wearer dies. | Risky breach squads. Once per instance; revived deaths do not repeat it. |
| Chain Hoist Charm | epic | Accelerates a siege unit's advance until its first committed attack. | Slow siege. Bonus ends permanently after setup or attack begins. |

### Embers

| Relic | Rarity | Defining effect | Best use and limit |
|---|---|---|---|
| Cinder Ring | common | Adds a small burn to a periodic successful hit. | Sustained attackers. Burn stacks are capped and never trigger further on-hit effects. |
| Ashen Rosary | common | Deals bonus damage to burning targets. | Fire squads. Needs another reliable source of burn. |
| Coalglass Vial | rare | Extends burns created by the wearer. | Flame Adept or Firepot Thrower. Does not extend allied burns or control durations. |
| Fireward Clasp | rare | Reduces received burn damage and shortens afterburn. | Frontline in fire zones. Does not block direct physical damage. |
| Phoenix Ember | epic | The wearer returns once with low health after a short death delay. | Expensive troops. Shares the one-revival limit; corpse denial counters it. |
| Hearthstone | epic | On deployment, extinguishes burn on a small number of nearby allies. | Emergency support. No global hazard removal or repeated aura cleansing. |

### Frost

| Relic | Rarity | Defining effect | Best use and limit |
|---|---|---|---|
| Rime Needle | common | Every fourth hit applies a brief chill. | Fast attackers. Control buildup obeys the target's resistance window. |
| Winter Ribbon | common | Deals bonus damage to chilled or rooted targets. | Control squads. No bonus against a merely slow-moving unit with no status. |
| Hoarfrost Medal | rare | Grants a small shield when the wearer is rooted or slept. | Vulnerable backline. Shield has a long cooldown and does not remove control. |
| Icebound Hourglass | rare | A completed unit ability chills its primary target. | Units with targeted abilities. Area and summon abilities do not multiply triggers. |
| Glacial Band | epic | After the wearer heals an ally, its next attack creates a small chill burst. | Battle Monk or Field Medic. Cooldown; shared healing does not trigger it. |
| Thawstone | epic | Periodically removes one root or chill from the wearer. | Mobile troops. Does not cleanse blight, wounds, or hostile terrain. |

### Restoration

| Relic | Rarity | Defining effect | Best use and limit |
|---|---|---|---|
| Mercy Bell | common | Improves the wearer's direct healing of allies at low health. | Healers. No effect on lifesteal or terrain healing. |
| Field Surgeon's Badge | common | Healing another ally briefly protects the wearer. | Field Medic. Protection refreshes up to a cap, without stacking. |
| Bloodroot Pendant | rare | Heals a small portion of direct damage dealt. | Sustained melee. No lifesteal from structures, damage-over-time, or summons. |
| Springwater Charm | rare | On deployment, cleanses one blight or wound from the nearest affected ally. | Support reserve. One target; control effects remain. |
| Mender's Thread | epic | Converts limited direct overhealing into a temporary shield. | Healers and durable allies. Shield cap; shields do not count as healing. |
| Second Breath Seal | epic | Survives one lethal direct hit at one health. | Fragile specialists. Once per battle per wearer; damage-over-time can still finish it. |

### Shadows

| Relic | Rarity | Defining effect | Best use and limit |
|---|---|---|---|
| Nightveil Brooch | common | The wearer begins briefly concealed until it attacks. | Assassins. Revealing effects and area damage still work. |
| Assassin's Token | common | The first strike against a newly selected enemy gains a small bonus. | Target-switching melee. Cooldown prevents target-lock manipulation. |
| Grave Silk | rare | A qualifying kill seals that corpse against enemy resurrection. | Corpse-denial squads. Does not prevent authored split-on-death bodies already released. |
| Umbral Coin | rare | A qualifying elite kill returns a small amount of courage. | Elite hunters. Per-wearer cooldown; no rewards from player-made targets. |
| Phantom Bell | epic | After receiving a direct heal, briefly reduces reflected damage received. | Burst attackers. Only reflection; does not reduce the original incoming hit. |
| Moonstep Anklet | epic | After completing several attacks, dodges one incoming projectile. | Ranged duelists. Cooldown and no protection against splash or ground fields. |

### Command

| Relic | Rarity | Defining effect | Best use and limit |
|---|---|---|---|
| Marshal's Signet | common | Deployment briefly buffs a few nearby allies. | Cheap support. Duplicate signets refresh rather than multiply the same buff. |
| Supply Ledger | common | Returns a small fraction of paid courage after the wearer's first death. | Expendable troops. Tracks actual paid cost; never rewards summons or repeated revival. |
| Banner Ribbon | rare | Extends the wearer's existing aura radius. | Banner Knight or Battle Monk. Creates no aura on a unit without one; radius has a cap. |
| Captain's Whistle | rare | A completed unit ability can interrupt one channel in a small nearby radius. | Frontline leaders. Cooldown; no interruption of unmarked ordinary attacks. |
| Caravan Token | epic | The wearer's participation in a field objective grants one small supply reward. | Objective squads. One reward per objective for the whole squad, not per copy. |
| King's Commission | epic | Gains a limited personal bonus when supported by a nearby ally from a different squad role. | Mixed formations. One qualifying partner; no faction-skin requirement. |

### Signature

| Relic | Rarity | Defining effect | Best use and limit |
|---|---|---|---|
| Saint's Reliquary | epic | On the wearer's first death, releases one pulse of healing for living allies. | Sacrificial support. No revival chain; summoned deaths cannot trigger it. |
| Stormglass Prism | epic | A periodic direct shot chains a weaker hit to one nearby enemy. | Mage or Stormcaller. Chain cannot recursively chain or retrigger on-hit items. |
| Earthbind Idol | epic | Stationary defenders become resistant to pulls and forced pushes. | Shield Knight or Iron Sentinel. No immunity while moving; roots and blight remain. |
| Dragonbone Quiver | epic | A periodic arrow pierces one target with reduced damage to the next. | Archer or Longbowman. Arrows only; structure eligibility stays unchanged. |
| Emberforge Anvil | epic | Heavy melee recovery briefly grants physical armor. | Maceman or Executioner. Only after a committed heavy hit; no benefit during windup. |
| Mirror Shard | epic | Reflects a capped fraction of direct melee damage received. | Tanks. Reflected damage cannot reflect again or lifesteal. |
| Gravewarden's Key | epic | Periodically seals one nearby enemy corpse before a summoner can consume it. | Anti-summoner support. Limited charges and radius; no erasing live enemies. |
| Tidemaster's Pearl | epic | A forced displacement grants a brief movement and attack recovery boost. | Troops facing harpoons. Long cooldown; allies cannot farm it with their own movement effects. |
| Sunfall Spur | epic | A charge kill advances the wearer's charge recovery once. | Cavalry Rider. Recovery floor and one reward per charge. |
| Gloam Lantern | epic | Reveals nearby concealed enemies and identifies the real illusion. | Backline protection. Small radius; no global reveal or automatic damage. |
| Crownroad Charter | epic | Completing a supply objective restores a little squad mana. | Objective squads. One payout per objective across all copies. |
| Hollow Chalice | epic | Consumes an enemy corpse token to recover the wearer's health. | Attrition melee. Competes with friendly summoners; no gain from summoned corpses. |
| Royal Execution Writ | hardened | A periodic finishing strike removes an ordinary low-health target. | Executioner or Rogue. Bosses and protected elites take capped damage instead. |
| Plagueglass Mask | hardened | Strongly reduces healing penalties from blight and wounds. | Healers or tanks. Does not remove the status or its other damage. |
| Runic Capacitor | hardened | Stores limited charges from player spell casts and spends one on the wearer's next hit. | Caster squads. No charge from item procs, copied spells, or refunds. |
| Wandering Shrine | hardened | After standing safely for a short time, emits a small healing field. | Protected support. Field ends when moving; only one per wearer. |
| Basilica Hourglass | hardened | A first lethal hit suspends the wearer briefly; it cannot act until restored with a little health. | Expensive specialists. Uses the same survival/return allowance as other lethal-save items. |
| Siegebreaker's Crest | hardened | Improves damage against explicitly fortified enemies. | Breach specialists. No universal damage bonus or boss percentage-health burst. |
| Lantern of Dawn | hardened | Prevents one nearby ally's lethal direct hit and spends its battle charge. | Formation support. Shared squad limit; cannot protect another Lantern of Dawn trigger. |

## The 12 tactical item cards

All twelve compete with the thirty spells for the five shared utility slots, creating **42 utility choices** in the collection. Objects cost mana on placement and have their own cooldown. Placement previews show range and footprint. Invalid placement spends nothing. Refills, repairs, or pickup effects are automatic when the required condition is met; they do not add a separate tiny collect button on mobile.

| Tactical item / availability | Behavior | Tradeoff | Visual brief |
|---|---|---|---|
| Palisade Kit · Early campaign | Builds a cheap wooden blocker after a short, interruptible setup. | Buys time against rushers; weaker and slower to appear than Stone Barricade. | Wooden stakes with a small construction marker. |
| Firepot Mine · Emberforge | A ground trap explodes on the first enemy and leaves a short burning patch. | Area denial; enemies can destroy the visible mine before triggering it. | Clay pot with a bright wick. |
| Healing Shrine · King's Road | A stationary shrine emits a limited number of healing pulses. | Sustained recovery; cannot heal the wagon and can be destroyed. | Short wooden shrine with amber lamp. |
| Bolt Turret · Emberforge | A placed bolt-thrower fires a finite number of ordinary shots. | Rear defense; does not attack structures and needs a safe position. | Small tripod crossbow with visible bolt rack. |
| Supply Crate · Saltwake | A crate yields small courage packets while allies hold its immediate area. | Trades mana for deployment reserve; enemies can break the crate. | Teal-strapped crate and three supply parcels. |
| Frost Trap · Mire | The first enemy triggers a short freeze and consumes the trap. | Stops a dangerous advance; control resistance applies to elites. | Pale-blue rune plate with a cracked rim. |
| Decoy Lantern · Gloamwood | A placed lantern lures eligible nearby enemies briefly. | Protects supports or gathers a pack; bosses resist and area damage destroys it. | Amber lamp on a fragile tall stick. |
| Smoke Pot · Saltwake | A small smoke area breaks enemy target locks and conceals moving allies briefly. | Enables retreat or setup; attacks reveal allies and hazards still hurt. | Dark clay flask and low gray plume. |
| Purifying Censer · Ashen Ward | A temporary field removes one blight or wound per pulse. | Allows healing through plague pressure; does not cleanse the entire battlefield. | Ivory-and-brass censer with clean white smoke. |
| Repair Cache · King's Road | A placed cache near the wagon applies a few repair pulses while protected. | Emergency repair; never increases maximum wagon health. | Tool box, spare plank, short amber repair sparks. |
| Grounding Rod · Hollow Basilica | A placed rod interrupts one marked enemy channel in its radius, then burns out. | Answers a summoner or null cast; must be positioned before completion. | Forked iron stake and blue-white rune. |
| War Horn · Sunfall | Marks one enemy near the aimed point as a temporary focus target for eligible allies. | Prioritizes a healer or commander; never forces units through blockers. | Curved brass horn and a clear target pennant. |

## The 30-spell grimoire

The [complete spell roster design](/Users/jason/side/game/docs/SPELL_ROSTER_DESIGN.md) retains the existing ten spells and adds **twenty**, two discoveries per district. Each addition has a target rule, prototype mana cost and cooldown, combat job, tradeoff, counter limits, and visual brief. The twenty proposals and pack assignments also appear in the structured design catalog.

| District | Two new spells | New tactical jobs |
|---|---|---|
| King's Road | Aegis Bolt, Radiant Rebuke | Save a fragile specialist before a heavy hit. / Stop Bellringer, a healer, or a marked charge windup. |
| Saltwake Docks | Tidal Push, Anchor Hex | Separate a support pack or relieve pressure near the wagon. / Keep a Tunneler or Cliff Stalker in the frontline. |
| Emberforge March | Molten Brand, Meteor | Open Rustguard, Bone Juggernaut, or a fortified enemy for physical attackers. / Punish a committed dense pack or stationary caster formation. |
| Ashen Ward | Purifying Light, Withering Seal | Restore healing effectiveness during Miasma Priest or Crown Executioner pressure. / Limit Rot Surgeon support or a regenerating colossus. |
| Thornwall Pass | Gale Step, Gravitation Well | Rescue a caster from a warning area or reposition a slow crew. / Gather a spread pack for Alchemist, Stormcaller, or Meteor. |
| Hollow Basilica | Soul Harvest, Consecrated Ground | Trade potential allied summons for another utility cast. / Deny Bone Cantor or Soul Standard while allies keep advancing. |
| Mire of Saints | Verdant Renewal, Bramble Snare | Sustain a mobile ally or soften damage-over-time attrition. / Delay a fresh rush after the frontline has already passed. |
| Sunfall Steppe | Chain Lightning, Rallying Advance | Hit support behind a staggered pack without requiring one tight impact cluster. / Carry a vulnerable formation through a pull or rooting threat. |
| Gloamwood Verge | Revealing Flare, Mirror Ward | Expose Shade or Mirror Wisp without recruiting a reveal specialist. / Protect a backline specialist from hostile archers or artillery's direct bolt. |
| Crownfall Citadel | Banish, Royal Edict | Clear ritual-made screens or expose a warded caster. / Open a window against layered Herald, Hexer, or caster support. |

### Existing spell identities alongside tactical gear

| Spell | Intended reason to take it |
|---|---|
| Fireball | Immediate area damage; faster and less position-dependent than a mine or firepot field. |
| Heal | Immediate ally recovery and limited wagon repair; Healing Shrine supplies slower ongoing recovery. |
| Frost Burst | Immediate area chill; Frost Trap is cheaper single-trigger preparation. |
| Lightning Strike | Precise burst against a marked priority target; Grounding Rod supplies a narrower interrupt job. |
| Barrier Ward | Immediate temporary protection for a group; Runesmith supplies smaller ongoing wards. |
| Stone Barricade | Immediate solid blocker with finite duration; Palisade Kit is cheaper but builds slowly. |
| War Cry | Brief broad offensive rally; War Horn changes target priority rather than raising everyone's stats. |
| Earthquake | Wide damage and ground control; slow setup troops offer sustained siege pressure instead. |
| Polymorph | A real temporary transformation/control state with explicit boss limits, or rename it to match the debuff. |
| Resurrect | Recover one eligible fallen ally at partial health using the shared return allowance. |

## Ten district identities and collection pacing

| District | Recruits introduced | Four new hostile threats | What the player learns |
|---|---|---|---|
| King's Road | Militia, Maceman, Field Medic | Grave Rat, Rustguard, Bellringer, Highwayman | Bodies, armor, interruption, single-patient healing. |
| Saltwake Docks | Longbowman, Javelin Skirmisher, Quartermaster | Drowned Harpooner, Salt Wraith, Barnacle Guard, Hookblade Raider | Displacement, second-line protection, weapon recovery, reserve courage. |
| Emberforge March | Iron Sentinel, Firepot Thrower, Runesmith, Flame Adept | Ash Hound, Cinder Spitter, Furnace Knight, Powder Bandit | Charge warnings, fire fields, shields, setup and destruction. |
| Ashen Ward | Plague Doctor, Flagellant, Paladin | Plague Leech, Miasma Priest, Rot Surgeon, Sealbreaker | Cleansing, healing denial, support targeting, temporary wards. |
| Thornwall Pass | Pikeman, Trapper, Earthshaper | Cliff Stalker, Thorn Brute, Briar Witch, Avalanche Bearer | Charge interception, backline reserve, traps, conditional retaliation. |
| Hollow Basilica | Gravebound Knight, Spirit Keeper, Wardbreaker | Chain Penitent, Ossuary Archer, Bone Cantor, Reliquary Eye | Corpse competition, piercing, revival limits, vulnerability windows. |
| Mire of Saints | Druid, Frost Witch, Sapper Crew | Swamp Maw, Bog Spore, Drowned Colossus, Bloated Eel | Terrain, status buildup, limited collateral damage, planted siege charges. |
| Sunfall Steppe | Outrider, Mounted Archer, Executioner | Bone Lancer, Dust Reaver, Carrion Banner, War Drummer | Interception, mobile fire, reload opportunities, attack tempo. |
| Gloamwood Verge | Falconer, Duelist, Illusionist | Shade, Mirror Wisp, Wicker Giant, Dream Weaver | Reveal, lure, parry, concealment, counter selection. |
| Crownfall Citadel | Chanter, Trebuchet Crew, Dragon Tamer | Gate Breaker, Crown Executioner, Null Magister, Soul Standard | Combining learned counters while protecting expensive specialist troops. |

Within each ten-stage district, a useful starting sequence is: establish the district with familiar enemies; introduce a new type around local stages 2, 4, 6, and 8; rehearse a pair before a commander battle. Existing mid-district boss stages remain. This schedule needs individual encounter editing; it is not an instruction to insert all new enemies into every wave.

Early discovery previews can appear before purchase eligibility, showing the silhouette, primary job, and one counter example. Unlocks should come from campaign progress and optional contracts; rare relics should also have a visible crafting path. Core counter tools must be available before their required threats. Existing Fireball, disposable troops, and ranged focus cover initial charges before specialized Pikeman interception arrives.

Equipment families have broad acquisition identities: Vanguard/Restoration through patrol and rescue contracts; Scouts through hunt contracts; Siege through stronghold objectives; Embers/Frost through relevant district routes; Shadows through ambush/reveal encounters; Command through escort and supply objectives. Signature relics use named district challenges, bosses, Tower, raids, or late forge recipes. These sources are proposals; final drop tables must guarantee access to essential counters and avoid making all 100 relics required purchases.

## Example full squads

These are mature collection examples, not recommended opening-game purchases. Each has six unit cards and five utility cards.

| Build | Six-unit squad | Five utilities | Main compromise |
|---|---|---|---|
| Hold the line | Iron Sentinel, Pikeman, Field Medic, Archer, Runesmith, Banner Knight | Heal, Barrier Ward, Palisade Kit, Purifying Censer, War Horn | Durable but slow; struggles to finish a protected siege target. |
| Clear the horde | Lantern Guard, Alchemist, Flame Adept, Stormcaller, Battle Monk, War Hound | Fireball, Frost Burst, Earthquake, Firepot Mine, Healing Shrine | Strong against packs; vulnerable to dispersed artillery and null fields. |
| Siege caravan | Shield Knight, Halberdier, Siege Engineer, Runesmith, Sapper Crew, Ballista Crew | Stone Barricade, Repair Cache, Smoke Pot, War Cry, Grounding Rod | Structure pressure needs setup; reserve courage matters against backline dives. |
| Grave covenant | Gravebound Knight, Necromancer, Spirit Keeper, Berserker, Field Medic, Wardbreaker | Heal, Resurrect, Barrier Ward, Purifying Censer, War Horn | Depends on eligible corpses and careful revival allocation; weak against corpse denial. |
| Mobile hunters | Cavalry Rider, Mounted Archer, Falconer, Rogue, Duelist, Quartermaster | Smoke Pot, Decoy Lantern, Frost Trap, War Cry, Heal | Picks off support; has less sustained tanking and structure damage. |

Useful paired relic examples include Cinder Ring + Ashen Rosary for burn offense, Rime Needle + Winter Ribbon for control offense, Bloodroot Pendant + Phantom Bell for an attacker facing reflection, and Banner Ribbon + Marshal's Signet for local support. These are interactions between individual effects, not hidden set bonuses.

## Expansion delivery order

The final target stays large. The packs below make implementation and art review manageable; they do not reduce the designed roster.

| Pack | New units | New hostile enemies | New relics | New tactical gear | New spells | Focus |
|---|---:|---:|---:|---:|---:|---|
| 1 — Caravan foundations | 8 | 10 | 16 | 4 | 5 | Repair existing promises; armor, healing, shields, channels, charges, and readable utility objects. |
| 2 — Fire and blight | 8 | 10 | 17 | 3 | 5 | Burn, chill, cleanse, field hazards, and proper equipment triggers. |
| 3 — Graves and pursuit | 8 | 10 | 17 | 3 | 5 | Corpse tokens, bounded revival, leaps, mobile weapons, and decoys. |
| 4 — Royal siege | 7 | 10 | 17 | 2 | 5 | Expensive siege, companions, advanced counters, and commander refinements. |
| Optional discovery pass | 0 | 0 | 0 | 0 | 0 | Add the Ash Pilgrim encounter once escort/reward rules are ready. |

Pack 1 recruits: **Militia, Maceman, Field Medic, Longbowman, Javelin Skirmisher, Quartermaster, Iron Sentinel, Runesmith**. Pack 1 enemies: **Grave Rat, Rustguard, Bellringer, Highwayman, Drowned Harpooner, Salt Wraith, Barnacle Guard, Hookblade Raider, Ash Hound, Furnace Knight**. Pack 1 gear: **Palisade Kit, Healing Shrine, Bolt Turret, Supply Crate**. It delivers paired bodies, armor breaking, sustained healing, charged shots, limited ammunition, reserve income, damage sharing, and wards while building foundations used throughout the rest of the roster.

Implementation packs do not dictate campaign order: a later-pack counter cannot be required by an earlier-pack encounter. New content stays out of live encounters until its counters, warning presentation, acquisition, and art are complete.

### Pack 1 contents

**Units:** Militia, Maceman, Field Medic, Longbowman, Javelin Skirmisher, Quartermaster, Iron Sentinel, Runesmith.

**Hostile enemies:** Grave Rat, Rustguard, Bellringer, Highwayman, Drowned Harpooner, Salt Wraith, Barnacle Guard, Hookblade Raider, Ash Hound, Furnace Knight.

**Relics:** Rampart Seal, Tower Charm, Last Wall Token, Iron Prayer, Oathbound Chain, Farwatch Lens, Windstring, Falcon Feather, Huntsman's Knot, Trailfinder Boots, Breach Writ, Gateward Medallion, Mercy Bell, Field Surgeon's Badge, Marshal's Signet, Supply Ledger.

**Tactical gear:** Palisade Kit, Healing Shrine, Bolt Turret, Supply Crate.

**Spells:** Aegis Bolt, Radiant Rebuke, Tidal Push, Molten Brand, Gale Step.

### Pack 2 contents

**Units:** Firepot Thrower, Flame Adept, Plague Doctor, Flagellant, Paladin, Earthshaper, Druid, Frost Witch.

**Hostile enemies:** Cinder Spitter, Powder Bandit, Plague Leech, Miasma Priest, Rot Surgeon, Sealbreaker, Thorn Brute, Briar Witch, Bog Spore, Wicker Giant.

**Relics:** Cinder Ring, Ashen Rosary, Coalglass Vial, Fireward Clasp, Phoenix Ember, Hearthstone, Rime Needle, Winter Ribbon, Hoarfrost Medal, Icebound Hourglass, Glacial Band, Thawstone, Bloodroot Pendant, Springwater Charm, Earthbind Idol, Emberforge Anvil, Plagueglass Mask.

**Tactical gear:** Firepot Mine, Frost Trap, Purifying Censer.

**Spells:** Purifying Light, Withering Seal, Gravitation Well, Verdant Renewal, Bramble Snare.

### Pack 3 contents

**Units:** Pikeman, Trapper, Gravebound Knight, Spirit Keeper, Outrider, Mounted Archer, Duelist, Illusionist.

**Hostile enemies:** Cliff Stalker, Avalanche Bearer, Chain Penitent, Ossuary Archer, Bone Cantor, Reliquary Eye, Swamp Maw, Drowned Colossus, Bloated Eel, Shade.

**Relics:** Sentinel Buckle, Twinbolt Clasp, Mender's Thread, Second Breath Seal, Nightveil Brooch, Assassin's Token, Grave Silk, Umbral Coin, Phantom Bell, Moonstep Anklet, Banner Ribbon, Captain's Whistle, King's Commission, Gravewarden's Key, Tidemaster's Pearl, Gloam Lantern, Hollow Chalice.

**Tactical gear:** Decoy Lantern, Smoke Pot, Repair Cache.

**Spells:** Anchor Hex, Soul Harvest, Consecrated Ground, Revealing Flare, Mirror Ward.

### Pack 4 contents

**Units:** Wardbreaker, Sapper Crew, Executioner, Falconer, Chanter, Trebuchet Crew, Dragon Tamer.

**Hostile enemies:** Bone Lancer, Dust Reaver, Carrion Banner, War Drummer, Mirror Wisp, Dream Weaver, Gate Breaker, Crown Executioner, Null Magister, Soul Standard.

**Relics:** Demolition Rune, Counterweight, Sapper's Fuse, Chain Hoist Charm, Caravan Token, Saint's Reliquary, Stormglass Prism, Dragonbone Quiver, Mirror Shard, Sunfall Spur, Crownroad Charter, Royal Execution Writ, Runic Capacitor, Wandering Shrine, Basilica Hourglass, Siegebreaker's Crest, Lantern of Dawn.

**Tactical gear:** Grounding Rod, War Horn.

**Spells:** Meteor, Chain Lightning, Rallying Advance, Banish, Royal Edict.

## Production and integration requirements

- Give each of the 31 new recruits and 41 encounters its own recognizable portrait/silhouette. Ash Pilgrim needs travel/idle/interaction art, rather than a full combat set. Small details must read at actual battle size.
- Allied color and material identity stays tied to the caravan: teal cloth, iron, oak, brass, and amber lanterns. Enemy species vary shape and movement; mercenaries use gray cloth, undead use ruined heraldry/bone, and wildlife uses body shape rather than equipment as the main identifier.
- Paired troops, cavalry, the falcon, drake, siege crews, and placed objects need explicit footprint, hitbox, owner, and animation decisions. Apparent flight is not automatic immunity or a new aerial lane.
- Equipment needs 67 new item pictures plus reviewed existing descriptions. Tactical items need 12 card pictures and their visible battlefield objects/effects. The twenty new spells need their own card pictures, targeting feedback, and combat effects. Reuse rigs and materials where sensible, but do not finish a roster entry as a recolored shared silhouette.
- Adding JSON definitions alone is insufficient: roster registration, promotions, level-4 abilities, squad tags, targeting, projectiles, codex, locale strings, audio, reward sources, enemy waves, endless/challenge selection, save handling, and asset checks must agree.
- Existing IDs and ownership stay stable. Adding a new item category and changing utilities to shared slots needs an explicit save migration; current equipped spells should retain their loadout positions.
- Verify trigger eligibility, hard-control resistance, corpse consumption, summon limits, revive origin state, resource refunds, and reflection recursion before content-wide playtests. Then check mobile cards/armory browsing and actual-sized battle art.
- Test encounter composition, not only isolated stats. A shielded healer, rooting caster behind artillery, or corpse-denial enemy against a summoner squad is a different balance problem from either unit alone.

The companion [design catalog](/Users/jason/side/game/docs/design/content-expansion-roster.json) records every retained and proposed entry with stable proposed IDs, mechanics, counters, and visual briefs. It is design data outside the live game's data folder. Prototype costs and discovery stages remain proposals.

## Source boundary

Current counts and existing behavior derive from Crownroad's [unit data](/Users/jason/side/game/data/units.json), [equipment](/Users/jason/side/game/data/equipment.json), [spells](/Users/jason/side/game/data/spells.json), [theme bible](/Users/jason/side/game/THEME_BIBLE.md), [combat implementation](/Users/jason/side/game/scripts/combat/BattleController.cs), and [prior comparison](/Users/jason/side/game/docs/DEAD_AHEAD_COMPARISON.md). All proposed names, mechanics, costs, and schedules in this document are new design proposals, not claims that those features already exist.
