# Crownroad and Dead Ahead: Zombie Warfare — content and mechanics map

Reviewed 8 October 2026. Scope: playable units, enemies, equipment, support items, and spells.

**Updated direction:** the user wants a much larger collection. The [full roster expansion design](/Users/jason/side/game/docs/CONTENT_EXPANSION_PLAN.md) proposes 50 playable units, 60 non-boss encounters, 100 relics, 30 spells, and 12 tactical items. Its 14 commanders comprise ten zone bosses and four mid-zone bosses. This comparison remains a record of the existing game; the expansion document governs proposed content scope.

## Main finding

Crownroad shares Zombie Warfare's core battle loop: protect a vehicle, deploy troops with courage, earn a second combat resource from kills, and break an enemy structure. Its strongest independent direction is **a caravan conducting a fantasy siege**, with a separate spell loadout, mobile artillery, formation proximity bonuses, enemy deployment disruption, and district commanders.

The largest gap is **how often a unit or item changes the player's tactical decisions**. Dead Ahead has a broader roster and many interactions between attack patterns, damage resistances, death effects, team powers, and equipment sets. Crownroad has several distinctive mechanics already, but much of its equipment and some later units provide stronger versions of existing behavior. Expanding those interactions would add more value than matching Dead Ahead's roster size.

This is a comparison of the current repository's data and combat code with the live wiki. It is not a playtest or a balance verdict. Counter suggestions below are design analysis, not measured win-rate claims. Role comparisons are approximate; matching names do not imply matching mechanics.

## 1. Scale and structure

| Area | Dead Ahead: Zombie Warfare | Crownroad now | Practical difference |
|---|---|---|---|
| Playable troops | 52 human units | 19 playable units | Dead Ahead offers more alternatives within each role; Crownroad has a smaller roster to teach and differentiate. |
| Deployable support | 8 support items, included in its 60-unit total | 10 spells, with a separate spell loadout | Support competes with troops for deck space in Dead Ahead. Crownroad can equip up to 6 units **plus** 5 spells. |
| Enemies | Wiki lists 67 zombies, 9 marauders, and 3 bosses, plus holiday variants and removed entries | 19 ordinary enemy definitions and 14 combat boss definitions | Dead Ahead has more ordinary variations and a separate hostile human faction. Crownroad concentrates more named content in commanders. |
| Equipment | 4 upgrade-item slots; 32 item sets; an additional charm slot unlocked at unit level 6 | 33 named relics; 1 slot initially and a second after promotion | These counts measure different things: a Dead Ahead set has multiple slots and random stat combinations; a Crownroad relic is one fixed definition. |
| Unit growth | Unit special abilities unlock at level 30 | Maximum unit level 5; 17 units have automatic cooldown abilities at level 4; promotion at level 5 | Crownroad delivers abilities earlier in its much shorter level ladder. Siege Engineer and Necromancer have no entry in the automatic ability catalog. |
| Team building | Team powers generally have 2/3/5-member thresholds; skins can change affiliation | 4 role synergies, each requiring 2 cards, plus 10 proximity combo definitions | Crownroad uses squad roles and battlefield proximity rather than a large skin/faction collection system. |

Sources: [Player Units](https://deadahead.wiki.gg/wiki/Player_Units), [Enemies](https://deadahead.wiki.gg/wiki/Enemies), [Upgrade Items](https://deadahead.wiki.gg/wiki/Upgrade_Items), [Charms](https://deadahead.wiki.gg/wiki/Charms), and [Team Powers](https://deadahead.wiki.gg/wiki/Team_Powers). Local census: [unit data](/Users/jason/side/game/data/units.json), [relic data](/Users/jason/side/game/data/equipment.json), [spell data](/Users/jason/side/game/data/spells.json), [progression](/Users/jason/side/game/scripts/core/GameState.cs:13), and [ability catalog](/Users/jason/side/game/scripts/core/UnitActiveAbilityCatalog.cs:29).

The live item page says **32 sets**. A search-engine copy returned an older count of 28, so this report uses the live page. The wiki spans three games; this comparison uses Zombie Warfare, rather than the bike game or Roadside.

## 2. Every Crownroad playable unit mapped

The comparison column identifies a similar job, not a direct replacement. Automatic abilities below unlock at level 4 and trigger during combat; they are not separate player-operated buttons.

| Crownroad unit | Similar Dead Ahead role | Actual Crownroad behavior and difference |
|---|---|---|
| **Swordsman** | [Redneck](https://deadahead.wiki.gg/wiki/Redneck): basic repeatable melee | General frontline attacker and structure attacker; gains a small cleave. Redneck emphasizes cheap expendability and later reanimates as a zombie. |
| **Archer** | [Gunslinger](https://deadahead.wiki.gg/wiki/Gunslinger): ranged support | Fires individual projectiles and gains a three-projectile volley. Gunslinger has burst fire, ammunition, and reloads. Archer does not damage structures. |
| **Shield Knight** | [Guard](https://deadahead.wiki.gg/wiki/Guard): durable melee protector | High health and temporary 60% damage reduction. Guard has persistent bullet resistance. Our Shield Knight has no passive interception of projectiles aimed at nearby allies. |
| **Spearman** | General melee fighter/damager; no close spear equivalent in the pages reviewed | Longer melee reach and a double-damage thrust that compensates for the target's base damage reduction. No dedicated anti-cavalry or anti-charge passive is authored. |
| **Crossbowman** | [Sniper Polina](https://deadahead.wiki.gg/wiki/Sniper_Polina): protected precision damage | Moderate-range, higher-damage shots; gains a shot at the farthest enemy in range. It has neither an ammo cycle nor Polina's bullet-resistance penetration. |
| **Cavalry Rider** | [Mechanic](https://deadahead.wiki.gg/wiki/Mechanic) / [Carol](https://deadahead.wiki.gg/wiki/Carol): rapid engagement and burst | Fast, relatively cheap melee; gains a forward charge that damages enemies along a line. Mechanic's signature is a powerful opening hammer strike; Carol dashes to individual targets. |
| **Siege Engineer** | [Welder](https://deadahead.wiki.gg/wiki/Welder): vehicle support | Returns to repair the damaged wagon, otherwise fights with ranged shots. Welder repairs objects and attacks the barricade while avoiding enemy combat. Our Engineer currently does not deploy turrets despite its codex description. |
| **Mage** | [Sniper Polina](https://deadahead.wiki.gg/wiki/Sniper_Polina): fragile long-range damage | Strong slow shots and a two-target Arcane Beam. Its magic identity currently sits on the shared projectile/damage system, without a distinct magic resistance category. |
| **Halberdier** | [Guard](https://deadahead.wiki.gg/wiki/Guard): heavier melee damage | High structure damage and a wide sweep. Useful breach specialization; no general armor-breaking passive is authored. |
| **Alchemist** | [Grenadier](https://deadahead.wiki.gg/wiki/Grenadier): area damage | Regular ranged splash attacks and an automatic stronger flask. Grenadier is a melee unit with a manual grenade and explosive death behavior. Our flasks do not create the burning ground promised by the codex. |
| **Battle Monk** | [Cap](https://deadahead.wiki.gg/wiki/Cap) + [Medic](https://deadahead.wiki.gg/wiki/Medic): buffs and healing | Local damage/movement aura and an automatic area heal. This combines support jobs that Dead Ahead distributes across different units. |
| **War Hound** | [Redneck](https://deadahead.wiki.gg/wiki/Redneck): cheap expendable deployment | Cheapest troop, very fast, low health, short deployment cooldown. Gains a temporary damage buff; its catalog calls this attack speed. The playable animal identity is a clear fantasy-roster distinction. |
| **Banner Knight** | [Cap](https://deadahead.wiki.gg/wiki/Cap): offensive support | Local damage/movement aura and a temporary reinforcement of those buffs. Cap distributes inspiration and, with his special ability, extra ammunition; our banner relies on proximity. |
| **Necromancer** | No allied summoner counterpart established; compare enemy [Necromancer](https://deadahead.wiki.gg/wiki/Necromancer) | Currently a regular ranged caster with proximity combos. The codex promises corpse-to-ally summoning, but I found no player summoning path or automatic ability for this unit. |
| **Rogue** | Shares a name with [Rogue](https://deadahead.wiki.gg/wiki/Rogue), but does a different job | Uses backline-biased targeting and gains temporary untargetability with a damage buff. Dead Ahead's Rogue specializes in stunning critical hits and agility-linked bullet resistance. |
| **Berserker** | [Berserker](https://deadahead.wiki.gg/wiki/Berserker): aggressive melee | Damage grows with missing health; frenzy adds damage and increases damage taken. Dead Ahead uses double swings, morale effects, and an injured-state critical-chance ability. |
| **Lantern Guard** | [Juggernaut](https://deadahead.wiki.gg/wiki/Juggernaut): durable area melee | Passive generic damage reduction, ordinary splash attacks, and temporary defense. Juggernaut has separate shield points and explosive critical attacks. Our automatic defense ability is shared with Shield Knight. |
| **Ballista Crew** | [Turret](https://deadahead.wiki.gg/wiki/Turret): sustained ranged fire, approximate only | Mobile splash artillery that can damage structures. Turret is a stationary item placed by the player. Our Anchor Shot shares Crossbowman's farthest-target ability implementation. |
| **Stormcaller** | General area-damage specialist; no close direct match established | Regular splash projectiles and a two-target Overcharge. Its automatic ability shares Mage's beam implementation; ordinary attacks use splash rather than an authored chain-lightning system. |

### What the roster comparison tells us

Crownroad already covers cheap deployment, tanking, ranged damage, area damage, repair, healing, buffs, assassination, low-health damage, and siege attacks. The opportunity is to give existing units **more distinct rules**, especially Necromancer, Siege Engineer, Mage/Stormcaller, and Shield Knight/Lantern Guard.

Dead Ahead distinguishes ranged units through patterns such as shotgun cones, automatic bursts, slow precision shots, and spray attacks. Crownroad mainly distinguishes ranged troops through range, damage, cadence, splash, and automatic abilities. That is simpler, but gives fewer weapon-specific interactions. See [Farmer](https://deadahead.wiki.gg/wiki/Farmer), [Gunslinger](https://deadahead.wiki.gg/wiki/Gunslinger), and [Dr. Norman](https://deadahead.wiki.gg/wiki/Dr._Norman).

Our formation system is worth developing: Shield Knight + Spearman, Mage + Shield Knight, and Alchemist + Halberdier already have proximity bonuses. They currently improve stats rather than create new formation behavior. Source: [combo catalog](/Users/jason/side/game/scripts/core/ComboPairCatalog.cs).

### Dead Ahead unit jobs without a direct Crownroad troop equivalent

| Job | Dead Ahead example | Closest Crownroad coverage |
|---|---|---|
| Pellet/cone weapon specialist | [Farmer](https://deadahead.wiki.gg/wiki/Farmer) | Alchemist and Stormcaller have splash, but not the shotgun's pellet pattern. |
| Sustained spray and slowing specialist | [Dr. Norman](https://deadahead.wiki.gg/wiki/Dr._Norman) | Frost Burst supplies temporary control as a spell; no troop has Norman's continuing spray pattern. |
| Gold-farming combat unit | [Austin](https://deadahead.wiki.gg/wiki/Austin) | No player-unit kill-to-gold perk found in the current roster. |
| Troop that revives itself | [Glenn](https://deadahead.wiki.gg/wiki/Glenn) | Resurrect is an external spell, rather than an individual troop's self-revival rule. |
| Dedicated healer and corpse reviver | [Medic](https://deadahead.wiki.gg/wiki/Medic), [Paramedic Nancy](https://deadahead.wiki.gg/wiki/Paramedic_Nancy) | Battle Monk combines an aura with an occasional heal; Heal and Resurrect carry much of the support work. |

These are absent jobs, not proposed additions. A smaller roster can deliberately leave them to spells or progression systems.

## 3. Every Crownroad ordinary enemy mapped

"First stage" is the first explicitly authored wave entry in the current 100-stage campaign. Summons can introduce an enemy outside that schedule.

| Crownroad enemy | First stage | Dead Ahead comparison | Implemented distinction / tactical pressure |
|---|---:|---|---|
| **Risen** | 1 | [Zombie](https://deadahead.wiki.gg/wiki/Zombie), basic walker | Standard slow melee pressure. Very close role. |
| **Ghoul** | 2 | [Fast Zombie](https://deadahead.wiki.gg/wiki/Fast_Zombie) / [Runner](https://deadahead.wiki.gg/wiki/Runner) | Fast, fragile melee pressure. Very close role. |
| **Grave Brute** | 4 | Larger tank zombies | More health and melee/structure damage. Its definition does not add a unique special rule. |
| **Blight Caster** | 4 | Ranged enemy role; Dead Ahead also has armed marauders | Ordinary ranged shots. No poison, corrosion, or morale damage is applied by its definition. |
| **Rot Hulk** | 11 | [Fat Zombie](https://deadahead.wiki.gg/wiki/Fat_Zombie) | Explodes on death **against the opposing team only**, and also attacks normally. Fat Zombie's death blast can hurt other zombies and enable chain reactions. |
| **Bone Nest** | 12 | [Putrid](https://deadahead.wiki.gg/wiki/Putrid), death-spawn pressure | Spawns two Risen on death, without Putrid's additional explosive death package. Requires cleanup after the apparent kill. |
| **Dread Herald** | 14 | Enemy empowerment / red-aura pressure, approximate only | A moving source of local damage and speed buffs. Killing the source removes the aura, creating a clear support-priority target. |
| **Bone Juggernaut** | 14 | [Bulletproof](https://deadahead.wiki.gg/wiki/Bulletproof) / armored tank role | Takes 24% less damage generally. Dead Ahead's Bulletproof specifically resists bullets, which creates a stronger distinction between counter weapons. |
| **Sapper** | 21 | Closer to a vehicle rusher than Dead Ahead's [Sapper](https://deadahead.wiki.gg/wiki/Sapper) | Fast wagon pressure and high structure damage; favors the objective when not already fighting in contact. It is not an authored suicide explosion. Dead Ahead's Sapper is a resistant tank with a possible armored-skeleton death transition. |
| **Hexer** | 31 | No close equivalent established in the reviewed roster | Suppresses courage generation and delays both unit and spell card recovery. This attacks the player's deployment economy directly. |
| **Shield Wall** | 34 | Armored enemies, but a different protection mechanism | Intercepts player projectiles aimed at nearby allies when positioned between attacker and target. This protects a formation, rather than just reducing its own incoming ranged damage. |
| **Tunneler** | 42 | [Insectoid](https://deadahead.wiki.gg/wiki/Insectoid), backline access | Relocates behind the rearmost player unit. Insectoid pounces on ranged-primary units; our tunneling criterion is rear position, so it can threaten a different target. |
| **Mirror Knight** | 46 | No direct counterpart established | Reflects 30% of damage back to a surviving attacker. Burst attackers need protection or recovery. |
| **Bone Ballista** | 48 | Ranged artillery role | Long-range splash and structure damage. Adds explicit siege pressure within the ordinary enemy roster. |
| **Lich** | 55 | [Necromancer](https://deadahead.wiki.gg/wiki/Necromancer) | Periodically creates a Risen and also fires ranged attacks. Dead Ahead's Necromancer summons three Dark Skeletons and cannot attack normally. Our "raise fallen" creates new units near the caster; it does not require an existing corpse. |
| **Catacomb Giant** | 58 | Heavy tank + [Putrid](https://deadahead.wiki.gg/wiki/Putrid)-style death spawning | Durable splash melee attacker that releases three Risen on death. A combined frontline/cleanup threat. |
| **Siege Tower** | 66 | Reinforcement pressure, approximate only | Slow, durable non-attacker that releases four Ghouls near the wagon and then destroys itself. Counterplay is to stop it before arrival. |
| **Revenant Captain** | 97 | Same broad support role as Dread Herald | Local damage/speed aura with different stats and radius. Currently an aura variant, rather than a new command mechanic. It can also be summoned by Plague Monarch earlier. |
| **Plague Engine** | Boss summon | Artillery role | Slow, durable ranged splash siege unit. Harrow Tidemaster can summon it; it has no direct authored wave entry. Its name does not imply an implemented poison effect. |

Local evidence: [unit definitions](/Users/jason/side/game/data/units.json), [campaign waves](/Users/jason/side/game/data/stages.json), [enemy abilities](/Users/jason/side/game/scripts/combat/BattleController.cs:3055), [targeting](/Users/jason/side/game/scripts/combat/BattleController.cs:3932), [death effects](/Users/jason/side/game/scripts/combat/BattleController.cs:4622), and [interception/reflect/tower behavior](/Users/jason/side/game/scripts/combat/BattleController.cs:4703).

### Important enemy systems Dead Ahead has that we lack or simplify

| System | Dead Ahead example | Crownroad status |
|---|---|---|
| Distinct hostile factions | Marauders target humans, can fight zombies, and can reanimate after death | The battle runtime uses Player/Enemy teams; no equivalent independent marauder faction is authored. |
| Allied deaths becoming hostile units | Many human units reanimate | No general player-death reanimation system found. Enemy death spawning and the Resurrect spell are separate systems. |
| Typed defenses and counters | Bulletproof resists bullets; Sapper has several damage resistances | Mainly generic damage reduction plus projectile interception; no comparable full melee/ranged/fire/explosion resistance matrix. |
| Hazardous friendly fire | Fat Zombie blast, Nitrogen freeze, Drone explosion | Most spells and enemy death bursts select one opposing team. The player has fewer opportunities to exploit enemy collateral damage. |
| Morale | Swarms can frighten susceptible humans | No equivalent charisma/valor/fear simulation found in the combat model. |
| Extreme charge threat | Charged Zombie winds up and performs a lethal contact charge | Our Sapper is a fast objective attacker, without that windup-and-impact rule. |
| Provocation / vulnerability puzzle | Egg changes state when provoked by suitable attacks | No ordinary enemy with this exact state-and-counter structure found. |
| Special displacement attacks | Psy's repeated knockback; Demon's battlefield attack | Crownroad has impact reactions and boss push/slow phases, but not equivalent recurring ordinary-enemy patterns. |
| Harmless optional encounter | Free Hugs stands idle, is not directly targeted by troops, and yields a large rage reward | No ordinary enemy with this non-hostile encounter rule found. Crownroad's map discoveries provide optional content outside the battle instead. |

Sources: [Enemies](https://deadahead.wiki.gg/wiki/Enemies), [Bulletproof](https://deadahead.wiki.gg/wiki/Bulletproof), [Sapper](https://deadahead.wiki.gg/wiki/Sapper), [Fat Zombie](https://deadahead.wiki.gg/wiki/Fat_Zombie), [Nitrogen](https://deadahead.wiki.gg/wiki/Nitrogen), [Drone](https://deadahead.wiki.gg/wiki/Drone), [Charged Zombie](https://deadahead.wiki.gg/wiki/Charged_Zombie), [Egg](https://deadahead.wiki.gg/wiki/Egg), [Psy](https://deadahead.wiki.gg/wiki/Psy), [Demon](https://deadahead.wiki.gg/wiki/Demon), and [Free Hugs](https://deadahead.wiki.gg/wiki/Free_Hugs).

These are design differences, not a checklist of features we must copy. A small set of readable counter rules could fit Crownroad better than a large resistance and morale simulation.

## 4. Bosses: more commanders, but shared building blocks

Our 14 bosses combine repeatable summoning, buffs or disruption with a health-triggered campaign phase. Their phase implementations are already differentiated:

| Crownroad boss | Campaign identity |
|---|---|
| Grave Lord | Risen reinforcements and a last-stand rally. |
| Tidecaller | Ranged pressure; Undertow damages, slows, and pushes the nearby line. |
| Harrow Tidemaster | Splash siege pressure and Plague Engine summons; floodgate phase damages, slows, pushes, and repairs the stronghold. |
| Iron Warden | Durable melee, Grave Brute summons, and a damaging Forge Surge with escort buffs. |
| Plague Archon | Courage/card disruption, Hexer escorts, and a slowing blackout phase. |
| Plague Monarch | Stronger deployment disruption, Revenant Captain escorts, and an intensified blackout phase. |
| Thornwall Chieftain | Ghoul reinforcements, movement buffs, and a stampede push. |
| Bone Pontiff | Bone Nest reinforcements; phase heals itself and repairs the stronghold; death explosion. |
| Reliquary Tyrant | Ranged splash siege attacks, Bone Ballista reinforcements, self-heal and stronghold repair. |
| Mire Behemoth | Durable melee, Rot Hulk summons, area damage/slow, self-heal, and a death explosion. |
| Steppe Warlord | Fast Ghoul reinforcements and a movement/damage rally. |
| Gloamwood Witch | Ranged siege caster; summons Hexers and damages/slows the highest-health player target. |
| Dread Sovereign | Bone Juggernaut reinforcements, stronghold repair, and a defensive escort rally. |
| Ashen Regent | Catacomb Giant reinforcements; damaging push and escort buffs; death explosion. |

Source: [campaign boss phases](/Users/jason/side/game/scripts/combat/BattleController.cs:3417) and [boss pacing](/Users/jason/side/game/scripts/combat/BattleController.BossPacing.cs).

Dead Ahead's [Boss](https://deadahead.wiki.gg/wiki/Boss) cycles between vulnerability states and a battlefield-wide crane attack. [Cephalopods](https://deadahead.wiki.gg/wiki/Cephalopods) replaces the barricade and changes the encounter through health phases, lasers, and a final countdown. These change encounter rules, whereas many Crownroad bosses use combinations of a shared commander toolkit. Our opportunity is to give selected major bosses one unmistakable interaction: a breakable ward, a protected ritual, a rotating safe area, or an escort that must be stopped.

## 5. "Items" means three different systems

### A. Equipment worn by units

Dead Ahead's upgrade items have slot-specific main stats, random substats, upgrading, rerolling, and a three-piece set bonus. Many set bonuses depend on events or circumstances: distance, entering combat, missing health, critical hits, kills, allied deaths, or enemy status. Charms add another equipment layer with class bonuses. See [Upgrade Items](https://deadahead.wiki.gg/wiki/Upgrade_Items) and [Charms](https://deadahead.wiki.gg/wiki/Charms).

Crownroad's relics have fixed health, damage, movement-speed, attack-cooldown, and structure-damage modifiers. There are no equipment-set identifiers or random substat rolls in the current relic model. The forge provides crafting, dismantling, and rarity fusion; enchantments add another upgrade choice. Promotion grants a second unrestricted relic slot rather than a new named gear category. See [relic definition](/Users/jason/side/game/scripts/data/EquipmentDefinition.cs), [forge](/Users/jason/side/game/scripts/core/RelicForgeCatalog.cs), and [equipment application](/Users/jason/side/game/scripts/core/GameState.cs:2373).

**Consequent difference:** Dead Ahead asks "which build changes how this unit fights?" more often. Crownroad currently asks "which combination of fixed bonuses best improves this unit?" Both can work, but the latter produces fewer surprising interactions.

### B. Tactical support deployed onto the battlefield

| Crownroad spell | Dead Ahead parallel | Difference in current behavior |
|---|---|---|
| Fireball | Red Barrel / Grenadier's grenade | Instant area damage against enemies. No persistent explosive obstacle or authored fire pool. |
| Heal | Medkit | Instant area healing and wagon repair when near the wagon. Medkit supplies repeated healing pulses and can gain poison-cleansing utility. |
| Frost Burst | Nitrogen / Dr. Norman | Area damage and a temporary movement slow. Nitrogen is a physical item with freeze interactions, including friendly units. |
| Lightning Strike | General burst support | Targeted area damage; no persistent support object or distinct typed lightning damage. |
| Barrier Ward | Protective unit abilities / shield effects, approximate only | Temporary damage reduction for allies in the selected area. This is not a separate shield-points pool. |
| Stone Barricade | Empty Barrel | Temporary durable allied blocker; differs from an impact-damage barrel dropped onto an enemy. |
| War Cry | Fury buff / Cap's inspiration | Temporary damage and movement buffs to deployed allies; spends kill-earned mana instead of a stockpiled consumable. |
| Earthquake | Area control role | Broad damage and slow; no close direct support-item equivalent established. |
| Polymorph | Crowd-control role | Currently very strong slow plus increased damage taken. The runtime does not replace the enemy with a sheep or explicitly disable its attacks. |
| Resurrect | Paramedic Nancy | Restores the last recorded fallen owned/deck ally at its death location with partial health. Nancy is an on-field support unit that travels to corpses. |

Sources: [Red Barrel](https://deadahead.wiki.gg/wiki/Red_Barrel), [Grenadier](https://deadahead.wiki.gg/wiki/Grenadier), [Medkit](https://deadahead.wiki.gg/wiki/Medkit), [Nitrogen](https://deadahead.wiki.gg/wiki/Nitrogen), [Dr. Norman](https://deadahead.wiki.gg/wiki/Dr._Norman), [Buffs](https://deadahead.wiki.gg/wiki/Buffs), [Cap](https://deadahead.wiki.gg/wiki/Cap), [Paramedic Nancy](https://deadahead.wiki.gg/wiki/Paramedic_Nancy), and [our spell execution](/Users/jason/side/game/scripts/combat/BattleController.cs:4983).

Dead Ahead also has **Turret, Generator, and Drone**: placed fire support, placed courage production, and an enemy lure/explosion. Crownroad's wagon has installed automatic weapon mounts, but those do not replace freely positioned turrets. We have no directly equivalent deployable generator or lure in the ten-spell roster. Sources: [Turret](https://deadahead.wiki.gg/wiki/Turret), [Generator](https://deadahead.wiki.gg/wiki/Generator), [Drone](https://deadahead.wiki.gg/wiki/Drone), and [wagon weapons](/Users/jason/side/game/scripts/core/BaseWeaponCatalog.cs).

### C. Consumable battle boosts

Dead Ahead's Fury, Extra Courage, and Energy Drink spend inventory consumables to boost damage, provide courage, or accelerate preparation. Crownroad's closest relationship is War Cry and progression upgrades; the current spell model does not contain a directly equivalent stockpile of those three boosts. See [Buffs](https://deadahead.wiki.gg/wiki/Buffs).

## 6. Current implementation gaps that affect this comparison

These are source-review findings. They should be resolved or reflected accurately in player-facing descriptions before adding more content.

| Finding | Evidence | Why it matters |
|---|---|---|
| **Player Necromancer does not raise allies** | Codex promises conversion of enemy corpses; unit data has regular ranged attacks; no player summoning path found, and no automatic ability catalog entry | One of the most distinctive promised roster jobs is currently a ranged stat profile. |
| **Siege Engineer does not deploy turrets** | Codex promises war-machine deployment; current combat behavior repairs the wagon and fights with projectiles | Its repair role is implemented, but its advertised construction role is not. |
| **Alchemist does not leave burning ground** | Codex describes burning ground; regular attacks and Volatile Flask apply direct splash damage | Area damage exists; persistent fire, damage-over-time, and its potential counters do not. |
| **Several enchantments define effects without applying them** | LifestealRatio, ThornsDamageRatio, CritChance, and CritMultiplier appear only as catalog properties/assignments; player-stat construction applies HealthScale, DamageScale, and SpeedScale | Lifesteal, Thorns, and Crit Strike have no corresponding effect found in the combat path. Vampiric applies its damage multiplier, but its healing portion is not wired through. |
| **Several relic names/lore imply special rules absent from their data** | Frostbound Crown is fixed stats, not a freeze; Phantom Mantle is stats, not a dodge; Spectral Lantern is stats, not nearby dark-magic protection | Names can create expectations of conditional behavior that the equipment system does not fulfill. |
| **Some later automatic abilities reuse earlier ones** | Lantern Guard → Shield Wall; Ballista Crew → Snipe; Stormcaller → Arcane Beam | Their base roles still differ, but their signature level-4 moments are less distinct than their names suggest. |
| **Some ability text describes a proxy** | Pack Howl and Blood Frenzy say attack speed, but execute damage multipliers | Similar average damage is not identical behavior: it changes burst, overkill, hit frequency, and any future on-hit effects. |
| **Polymorph is a debuff rather than a full transformation** | Execution applies speed and defense modifiers; no sheep substitution or attack-disable state | It is useful control, but "harmless sheep" overstates what is currently implemented. |

Evidence: [codex descriptions](/Users/jason/side/game/scripts/core/CodexCatalog.cs:28), [enchantment definitions](/Users/jason/side/game/scripts/core/EnchantmentCatalog.cs:44), [applied enchantment fields](/Users/jason/side/game/scripts/core/GameState.cs:2410), [shared ability execution](/Users/jason/side/game/scripts/combat/BattleController.cs:2731), [attack-speed proxies](/Users/jason/side/game/scripts/combat/BattleController.cs:3005), and [Polymorph](/Users/jason/side/game/scripts/combat/BattleController.cs:5235).

## 7. Recommended direction, in order

1. **Make current promises reliable.** Implement or correct Necromancer summoning, Engineer construction, advertised enchantment effects, and spell/relic descriptions. This is the clearest improvement to unit and item identity.
2. **Give each unit a defining rule as the roster expands.** Let Mage specialize in precise piercing magic and Stormcaller in chaining through formations; distinguish a Shield Knight that protects allies from a Lantern Guard that withstands swarms. Extend the roster with clear jobs and counterplay.
3. **Build a larger relic collection around useful behaviors.** Proposed examples: Spectral Lantern shortens nearby curse effects; Frostbound Crown slows on a timed trigger; Siege Hammer grants a bonus against structures at an explicit movement cost. Define cooldowns and limits, and organize the expanded collection into readable families.
4. **Teach a small, visible counter system.** Proposed rules: melee breaks Shield Wall protection; cleansing answers a clearly marked blight effect; siege weapons answer fortified targets. Use readable silhouettes and combat feedback. We do not need to duplicate every Dead Ahead damage type.
5. **Build encounters from interacting enemies.** Shield Wall protecting a Lich, Tunneler threatening the repairer, or Hexer arriving before Siege Tower already creates recognizable decisions. Give a few major bosses a distinct encounter rule beyond reinforcing and buffing the same line.

These recommendations are design proposals, not changes made by this review. The strongest Crownroad direction is the Lantern Caravan winning through formations, siege preparation, and battlefield rites. Its existing enemies and spells already support that identity.

## Appendix: all 33 relics and their actual effects

All values below come from the current JSON definitions. "Attack cooldown" is a reduction in seconds, not a percentage and not a card-deployment cooldown. "Structure damage" is a flat bonus to the unit's base-attack damage; a relic does not make a normally non-siege ranged unit able to attack structures.

| Relic | Rarity | Actual bonuses |
|---|---|---|
| Iron Pendant | common | +8% health |
| Sharpened Edge | common | +8% damage |
| Swift Boots | common | +8% movement speed |
| Battle Drum | common | −0.06s attack cooldown |
| Guardian Shield | rare | +12% health; +4 structure damage |
| War Brand | rare | +14% damage; −0.04s attack cooldown |
| Windrunner Cloak | rare | +6% health; +12% movement speed |
| Sage's Ring | rare | +8% health; +10% damage |
| Crown of Valor | epic | +18% health; +12% damage |
| Blade of Ruin | epic | +20% damage; +6 structure damage |
| Phantom Mantle | epic | +10% health; +16% movement speed; −0.06s attack cooldown |
| Dragon Heart | epic | +22% health; +8% damage; +4 structure damage |
| Wolftooth Charm | common | +4% damage; −0.08s attack cooldown |
| Tombstone Shard | rare | +14% health; +6% damage; +2 structure damage |
| Stormcaller Sigil | rare | +16% damage; +8% movement speed |
| Siege Hammer | rare | -5% movement speed; +12 structure damage |
| Spectral Lantern | epic | +24% damage; −0.04s attack cooldown |
| Immortal Wreath | epic | +30% health; +6% movement speed |
| Frostbound Crown | epic | +16% health; +14% damage; −0.04s attack cooldown; +3 structure damage |
| Moonfire Talisman | epic | +10% health; +22% damage; +10% movement speed; +2 structure damage |
| Hardened Bulwark | hardened | +25% health; +6 structure damage |
| Hardened Fang | hardened | +28% damage; −0.06s attack cooldown |
| Hardened Sigil | hardened | +15% health; +18% damage; +4 structure damage |
| Hardened Crown | hardened | +20% health; +15% damage; +6% movement speed; −0.04s attack cooldown |
| Hardened Soul | hardened | +30% health; +25% damage; +8% movement speed; −0.06s attack cooldown; +8 structure damage |
| Grave Lord's Crown | epic | +20% health; +16% damage; −0.04s attack cooldown; +5 structure damage |
| Iron Warden's Heart | epic | +24% health; +10% damage; +8 structure damage |
| Sovereign's Mantle | epic | +18% health; +20% damage; +6% movement speed; −0.04s attack cooldown; +4 structure damage |
| Plague Censer | epic | +14% health; +18% damage; +4% movement speed; −0.06s attack cooldown; +2 structure damage |
| Tower Sentinel's Ward | epic | +18% health; +10% damage; −0.04s attack cooldown; +4 structure damage |
| Ascendant's Signet | epic | +14% health; +20% damage; +6% movement speed; −0.04s attack cooldown; +2 structure damage |
| Apex Talisman | hardened | +22% health; +22% damage; +4% movement speed; −0.06s attack cooldown; +6 structure damage |
| Pinnacle Crown | hardened | +28% health; +28% damage; +8% movement speed; −0.08s attack cooldown; +10 structure damage |

## Review boundaries

- Read the live wiki's roster, equipment, charms, team-power and buff pages, plus representative unit/enemy/support detail pages linked throughout this report.
- Read current unit/relic/spell definitions and the relevant combat, equipment, progression, combo and codex implementations.
- Counted **combat definitions**, not portraits, asset files, codex entries or promoted titles. The codex includes legacy names/IDs and flavor text, so it is not a reliable independent roster census.
- Did not run the game or measure balance, pacing, usability, acquisition time, or production availability of each side mode. No gameplay changes were made.
