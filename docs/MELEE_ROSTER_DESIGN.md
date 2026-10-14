# Crownroad Melee Roster and Recruitment Progression

Implemented core prototype · 9 October 2026

Make infantry the center of Crownroad. Begin with a Peasant and recruit Militia next, then open distinct spear, sword, axe, mace, and polearm branches before expensive armored professionals and knights. The initial core contains **32 cards: 20 foot melee, 2 mounted melee, 3 ranged, 5 support, and 2 siege**. Melee occupies **22 of 32 slots, about 69%**. This core, its recruitment schedule, level cap, paired deployments, and combat stats are now implemented with placeholder art. See [implementation notes](CONTENT_IMPLEMENTATION.md) and [the playable stat catalog](catalogs/CURRENT_CONTENT_STATS.md).

This revision takes priority over the earlier broad 50-unit draft for core recruitment and unit implementation order. The long-term collection target can still reach 50 through tested additions, while keeping at least 65% of the expanded core focused on melee and no more than two dedicated siege specialists. Twenty-five unselected earlier concepts remain reserve candidates, and seven new melee concepts are defined here. The previous Word catalogs remain numeric snapshots of the earlier pool; the current core and all its level 1–20 records are in [the revised roster data](/Users/jason/side/game/docs/design/melee-roster-revision.json).

## Historical inspiration

Medieval infantry provides a useful range of equipment and training. Armor was worn by ordinary foot soldiers and militia as well as knights; a man-at-arms is not automatically a knight. [The Met explains these distinctions](https://www.metmuseum.org/toah/hd/aams/hd_aams.htm).

Bills were infantry staff weapons with agricultural origins, while poleaxes were associated with armored combat and elite users. These support different game branches for hooked infantry and heavy armored fighters. [Royal Armouries describes both](https://royalarmouries.org/objects-and-stories/stories/the-hundred-years-war-1337-1453). Pikes and halberds offer another useful distinction: formation reach versus a versatile cutting and thrusting weapon. [The Met compares them](https://www.metmuseum.org/art/collection/search/25898).

The recruitment ladder is game progression, not a historical social-promotion system. The setting mixes medieval European inspiration across periods with explicitly fantasy troops. Weapon counters, hook displacement, guard windows, armor penetration, and stagger timings below are authored game mechanics. Greatswordsman is our readable name for a late medieval two-handed-sword specialization; it should not borrow the later Renaissance Landsknecht costume by default. Berserker is a stylized game fighter, and Rogue is a light assault archetype, not a claim about standard medieval army ranks.

## Recruitment branches

| Branch | Recruitment path | Squad choice |
| --- | --- | --- |
| Cheap bodies | Peasant → Militia | One emergency body or a paid two-body deployment. |
| Spears and polearms | Spearman → Billman / Pikeman → Halberdier | Reach, a short hook pull, charge bracing, or a frontal sweep. |
| Swords and maneuver | Swordsman → Man-at-Arms → Greatswordsman / Duelist | Reliable generalist, personal guard, committed multi-target cut, or reactive parry. |
| Axes and maces | Axeman / Maceman → Warhammer Knight | Temporary shield pressure, shared armor exposure, or bounded interruption. |
| Armored professionals | Man-at-Arms → Shield Knight / Poleaxe Knight | Offensive reliability, allied projectile protection, or personal armor penetration. |
| Command and fantasy melee | Banner Knight / Paladin / Gravebound Knight | Formation offense, protecting a patient, or one costly return. |
| Mounted melee | Cavalry Rider / Outrider | An offensive charge or a one-use wagon interception. |

Arrows describe recruitment themes and increasing commitment, not mandatory purchases or replacement upgrades. Each card keeps its own identity and can level to 20. A high-level Peasant remains an inexpensive emergency body; a knight is a much larger courage and deployment commitment. Discovery stages reveal choices rather than obliging the player to buy every card.

## Preserving early recruits

Later recruits offer a modest per-body strength increase, a different weapon pattern, or a bounded skill in exchange for higher courage, slower deployment, longer attack recovery, or a narrower matchup. Compare equal-level squads at equal courage, not one Peasant against one knight. Keep a useful late-campaign role for Peasant, Militia, Spearman, Swordsman, and Archer.

Swordsman remains the inexpensive steady-damage baseline. Poleaxe Knight has 84 base HP versus Swordsman’s 64, but costs 34 courage versus 20 and attacks every 1.5 seconds versus 0.9. Its armor and specialized thrust help in a particular matchup; it does not replace the basic sword card in every fight. Militia’s two bodies remain a resource-efficient line choice with a real weakness to splash.

Defining inexpensive roles and required counters work from recruitment. Late unlocks do not receive an extra stat tier. Any later skill unlock follows a common level milestone available to early and late cards. Promotion, mastery, awakening, gear, enchantments, and account perks share one permanent bonus budget rather than adding independent multipliers. See [the progression and power budget](/Users/jason/side/game/docs/PROGRESSION_BALANCE.md) for those limits and the late-campaign checks.

## Twenty foot melee recruits

| Unit | Stage | Equipment | Defining game rule | Weakness |
| --- | --- | --- | --- | --- |
| Peasant | 1 | Club or improvised agricultural tool; ordinary clothes | Cheapest single body; quick deployment lets it buy a moment for another recruit. | Very low damage and protection; cannot hold a durable enemy alone. |
| Militia | 2 | Short spears; padded clothing and simple helmets | One payment deploys two bodies, offering a broader early blocking line. | Splash punishes the pair; each body is fragile. |
| Spearman | 3 | Spear; light body protection | Cheap reach behind another melee body; its committed thrust answers physical armor. | Poor close defense when a flanker reaches it. |
| Swordsman | 7 | Arming sword; padded armor with some mail | Reliable mobile generalist with a modest cleave. | No specialist answer to heavy armor, wards, or ranged focus. |
| Axeman | 12 | One-handed axe and small shield; light mail | Committed chops do extra damage to temporary shields and breakable tactical blockers. | Short reach; the bonus does not bypass permanent armor or create a structure-damage premium. |
| Maceman | 14 | Mace; reinforced body protection | Every third committed hit briefly reduces one enemy armor rating for allied follow-up. | Slow windup; must survive repeated contact. |
| Billman | 22 | Bill; padded coat and helmet | A marked hook attack pulls an eligible ordinary enemy a short distance into the line. | Cannot drag bosses or anchored targets; recovery exposes it to nearby attackers. |
| Man-at-Arms | 25 | Sword and buckler; mail with plate reinforcements | Alternates ordinary strikes with a short personal physical guard window. | Moderate cost and damage; its guard does not protect allies or stop magical damage. |
| Shield Knight | 32 | Large shield and sword; substantial armor | Holds contact and periodically intercepts an eligible projectile aimed at a nearby ally. | Limited interception allowance; vulnerable to magical damage and field hazards. |
| Paladin | 38 | Sword and shield; enchanted armor | Protecting a wounded ally charges a radiant melee strike and a limited allied shield. | High cost; needs a nearby patient and has a long support cooldown. |
| Pikeman | 42 | Long pike; helmet and body armor | Braces against a telegraphed charge and interrupts the first eligible charger reaching the point. | Moving or displacement cancels the brace; flankers can close inside its reach. |
| Halberdier | 45 | Halberd; medium body armor | Combines reach with a committed frontal sweep against clustered troops. | Sweep recovery creates a punish window; ordinary stronghold damage replaces its siege premium. |
| Gravebound Knight | 52 | Cracked armor and sword | Returns once at 25% of maximum HP and permanently reduced speed; the return is part of its survivability budget. | Corpse denial prevents return; revival sources share one return allowance. |
| Greatswordsman | 55 | Large two-handed sword; moderate body armor | A long frontal cut reaches up to three closely aligned ordinary targets. | Slow committed swing and little rear protection; this is a game specialization inspired by late medieval two-handed swords. |
| Berserker | 62 | Axe or sword; little armor | Missing health increases its offensive pressure and frenzy accelerates real attack recovery. | Low protection; burst or ranged focus can kill it before its pressure pays off. |
| Warhammer Knight | 78 | Warhammer; plate-inspired protection | Every fourth committed heavy hit interrupts an eligible windup or channel with a short stagger. | Must stay in contact; control resistance prevents a permanent lock. |
| Rogue | 82 | Short blade; light armor | Targets an exposed backline and gains a real next-strike bonus after Vanish. | Fragile when revealed or caught by area attacks. |
| Duelist | 85 | Sword and buckler; light armor | Parries one telegraphed melee strike and ripostes against that opponent. | A single-target defense; projectiles and multiple attackers overwhelm it. |
| Poleaxe Knight | 92 | Poleaxe; plate-inspired protection | Alternates a committed crushing blow and a thrust with personal physical-armor penetration. | High cost and slow recovery; penetration helps its own hit rather than exposing armor for the whole squad. |
| Banner Knight | 95 | Sword, standard and armor | Fights at the front while rallying nearby allies offensively. | Needs a nearby formation and lacks Shield Knight projectile interception. |

## Supporting cards and mounted melee

| Unit | Category | Stage | Defining rule | Weakness |
| --- | --- | --- | --- | --- |
| Archer | ranged | 5 | Affordable ordinary ranged support with a limited volley. | Needs protection and contributes no ordinary structure damage. |
| Field Medic | support | 9 | Channels repeated healing into one injured patient. | Interrupted healing stops; requires an intact frontline. |
| Caravan Engineer | support | 18 | Repairs the wagon and maintains one temporary repairable bolt emplacement. | Low personal damage and a vulnerable setup; construction support is its main job. |
| Crossbowman | ranged | 28 | Deliberate precision shots and a special aimed at a distant priority target. | Lower movement and firing cadence than light melee. |
| Battle Monk | support | 35 | Maintains its combat aura and periodic area healing. | Lower patient healing than the Medic; dispersed allies miss the aura. |
| Cavalry Rider | mounted melee | 48 | Makes an offensive line charge, then commits to close combat. | A braced defender stops the charge; expensive losses. |
| Quartermaster | support | 58 | Qualifying nearby allied kills produce small manually collected courage packs on a shared cooldown. | Low direct damage; no rewards from summoned or artificial targets. |
| Sapper Crew | siege | 68 | A close-range setup plants demolition against a structure or explicitly fortified target. | Two vulnerable bodies, weak ordinary combat, interruptible setup. |
| Outrider | mounted melee | 72 | Intercepts the first threat nearing the wagon, then fights as a fast light melee troop. | One interception; less sustained toughness than the offensive Cavalry Rider. |
| Longbowman | ranged | 75 | Charges a powerful straight shot while stationary. | Displacement loses the charge; poor close defense. |
| Mage | support | 88 | Provides magical direct damage and a line-piercing Arcane Beam. | A small late support option; fragile and vulnerable to null fields. |
| Ballista Crew | siege | 98 | Sustained ranged structure pressure with an armor-piercing marked siege shot. | Slow deployment and recovery; vulnerable to close attackers. |

Caravan Engineer keeps the existing Siege Engineer ownership ID. Halberdier becomes a troop-fighting polearm unit: its implemented level 1 ordinary structure damage is now 12, compared with 38 in the original definition and 24 in the previous core draft. Ballista Crew and Sapper Crew are the only dedicated siege specialists. A normal melee unit can still finish the stronghold. Trebuchet Crew remains reserve content.

## Proposed discovery schedule

| Zone | Core recruit discoveries |
| --- | --- |
| King's Road | Peasant (1), Militia (2), Spearman (3), Archer (5), Swordsman (7), Field Medic (9) |
| Saltwake Docks | Axeman (12), Maceman (14), Caravan Engineer (18) |
| Emberforge March | Billman (22), Man-at-Arms (25), Crossbowman (28) |
| Ashen Ward | Shield Knight (32), Battle Monk (35), Paladin (38) |
| Thornwall Pass | Pikeman (42), Halberdier (45), Cavalry Rider (48) |
| Hollow Basilica | Gravebound Knight (52), Greatswordsman (55), Quartermaster (58) |
| Mire of Saints | Berserker (62), Sapper Crew (68) |
| Sunfall Steppe | Outrider (72), Longbowman (75), Warhammer Knight (78) |
| Gloamwood Verge | Rogue (82), Duelist (85), Mage (88) |
| Crownfall Citadel | Poleaxe Knight (92), Banner Knight (95), Ballista Crew (98) |

The enemy introduction rule stays at one new ordinary enemy type per zone after the opening palette, with familiar enemies returning later. This table changes player recruitment only. Spearman is available before the provisional first-zone Rustguard debut. Give a visible demonstration of its armor-answering thrust and retain an accessible spell or positioning answer. Keep cleanse, reveal, interrupt, and ward-counter utility access ahead of encounters that require them; moving specialist units into reserves must not remove a required counter.

When implementing the new starter path, preserve already-owned units and their progression. New-save recruitment should use the revised schedule; existing saves should not lose a Swordsman or Shield Knight that they already own. Recruitment discovery, purchase eligibility, contracts, and tutorial rewards need one deliberate migration rather than a silent change to existing ownership.

## Combat stats for levels 1 and 20

All pairs are level 1 / level 20 before equipment, promotions, synergy, or temporary effects. Every core profile is now retuned for restrained power growth, including retained units. These core base values are now used by the live game definitions. The earlier base values are preserved in the structured data for comparison. All recruits use the same level curve and a late discovery grants no automatic level or rarity multiplier.

| Unit | HP | Attack | Armor | Physical resist % | Magic resist % | Crit chance % | Critical damage bonus % |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Peasant | 30 / 39 | 5 / 5.95 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 50 / 50 |
| Militia | 24 / 31 | 4.5 / 5.35 | 2 / 2 | 1.96 / 1.96 | 0 / 0 | 3 / 3 | 50 / 50 |
| Spearman | 54 / 69 | 8 / 9.52 | 8 / 8 | 7.41 / 7.41 | 0 / 0 | 6 / 6 | 50 / 50 |
| Archer | 44 / 57 | 8 / 9.52 | 2 / 2 | 1.96 / 1.96 | 0 / 0 | 6 / 6 | 50 / 50 |
| Swordsman | 64 / 82 | 10 / 11.9 | 10 / 10 | 9.09 / 9.09 | 5 / 5 | 8 / 8 | 50 / 50 |
| Field Medic | 48 / 62 | 4 / 4.76 | 4 / 4 | 3.85 / 3.85 | 5 / 5 | 0 / 0 | 50 / 50 |
| Axeman | 62 / 80 | 11 / 13.09 | 8 / 8 | 7.41 / 7.41 | 0 / 0 | 8 / 8 | 50 / 50 |
| Maceman | 68 / 87 | 12 / 14.28 | 14 / 14 | 12.28 / 12.28 | 0 / 0 | 6 / 6 | 50 / 50 |
| Caravan Engineer | 52 / 67 | 6 / 7.14 | 6 / 6 | 5.66 / 5.66 | 0 / 0 | 3 / 3 | 50 / 50 |
| Billman | 58 / 75 | 9 / 10.71 | 10 / 10 | 9.09 / 9.09 | 0 / 0 | 6 / 6 | 50 / 50 |
| Man-at-Arms | 72 / 93 | 11 / 13.09 | 20 / 20 | 16.67 / 16.67 | 5 / 5 | 8 / 8 | 50 / 50 |
| Crossbowman | 44 / 57 | 13 / 15.47 | 4 / 4 | 3.85 / 3.85 | 0 / 0 | 8 / 8 | 55 / 55 |
| Shield Knight | 76 / 98 | 8 / 9.52 | 34 / 34 | 25.37 / 25.37 | 5 / 5 | 5 / 5 | 50 / 50 |
| Battle Monk | 54 / 69 | 7 / 8.33 | 8 / 8 | 7.41 / 7.41 | 10 / 10 | 3 / 3 | 50 / 50 |
| Paladin | 72 / 93 | 10 / 11.9 | 22 / 22 | 18.03 / 18.03 | 12 / 12 | 6 / 6 | 55 / 55 |
| Pikeman | 62 / 80 | 9 / 10.71 | 12 / 12 | 10.71 / 10.71 | 0 / 0 | 5 / 5 | 50 / 50 |
| Halberdier | 66 / 85 | 12 / 14.28 | 16 / 16 | 13.79 / 13.79 | 0 / 0 | 6 / 6 | 55 / 55 |
| Cavalry Rider | 52 / 67 | 9 / 10.71 | 10 / 10 | 9.09 / 9.09 | 0 / 0 | 8 / 8 | 50 / 50 |
| Gravebound Knight | 68 / 87 | 9 / 10.71 | 16 / 16 | 13.79 / 13.79 | 5 / 5 | 5 / 5 | 50 / 50 |
| Greatswordsman | 64 / 82 | 14 / 16.66 | 12 / 12 | 10.71 / 10.71 | 0 / 0 | 6 / 6 | 60 / 60 |
| Quartermaster | 48 / 62 | 5 / 5.95 | 4 / 4 | 3.85 / 3.85 | 0 / 0 | 3 / 3 | 50 / 50 |
| Berserker | 58 / 75 | 11 / 13.09 | 4 / 4 | 3.85 / 3.85 | 0 / 0 | 10 / 10 | 55 / 55 |
| Sapper Crew | 32 / 41 | 4 / 4.76 | 4 / 4 | 3.85 / 3.85 | 0 / 0 | 3 / 3 | 50 / 50 |
| Outrider | 48 / 62 | 8 / 9.52 | 8 / 8 | 7.41 / 7.41 | 0 / 0 | 8 / 8 | 50 / 50 |
| Longbowman | 42 / 54 | 17 / 20.23 | 2 / 2 | 1.96 / 1.96 | 0 / 0 | 8 / 8 | 55 / 55 |
| Warhammer Knight | 80 / 103 | 17 / 20.23 | 24 / 24 | 19.35 / 19.35 | 5 / 5 | 6 / 6 | 55 / 55 |
| Rogue | 40 / 51 | 12 / 14.28 | 4 / 4 | 3.85 / 3.85 | 0 / 0 | 12 / 12 | 60 / 60 |
| Duelist | 54 / 69 | 11 / 13.09 | 8 / 8 | 7.41 / 7.41 | 0 / 0 | 12 / 12 | 60 / 60 |
| Mage | 44 / 57 | 13 / 15.47 | 2 / 2 | 1.96 / 1.96 | 18 / 18 | 6 / 6 | 55 / 55 |
| Poleaxe Knight | 84 / 108 | 18 / 21.42 | 26 / 26 | 20.63 / 20.63 | 5 / 5 | 8 / 8 | 55 / 55 |
| Banner Knight | 64 / 82 | 7 / 8.33 | 16 / 16 | 13.79 / 13.79 | 5 / 5 | 4 / 4 | 50 / 50 |
| Ballista Crew | 54 / 69 | 18 / 21.42 | 8 / 8 | 7.41 / 7.41 | 0 / 0 | 5 / 5 | 55 / 55 |

Health gains 1.5% of its level 1 base per level, and attack and structure damage gain 1% of their level 1 base per level. These increases are linear, without compounding. At level 20 the gains total 28.5% HP and 19% damage. Armor, magical resistance, critical chance, critical damage bonus, movement, reach, and timing stay fixed with level. Armor still derives physical resistance as 100 × armor / (100 + armor), with explicit resistance points afterward. Global defense and critical safety caps remain 75% resistance, 60% critical chance, and 200% critical damage bonus; ordinary authored profiles stay well below them. A 50% critical bonus means a 1.5× eligible hit before defenses.

## Deployment movement and attack timing

| Unit | Courage | Deploy cooldown s | Speed | Range | Attack interval s | Structure attack 1 / 20 | Bodies |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Peasant | 8 | 2.8 | 36 | 24 | 1.2 | 4 / 4.76 | 1 |
| Militia | 14 | 5 | 34 | 28 | 1.15 | 4 / 4.76 | 2 |
| Spearman | 18 | 6.5 | 34 | 46 | 1.1 | 7 / 8.33 | 1 |
| Archer | 18 | 6 | 33.8 | 73 | 1 | — | 1 |
| Swordsman | 20 | 6 | 35 | 32 | 0.9 | 10 / 11.9 | 1 |
| Field Medic | 20 | 9 | 32 | 60 | 1.5 | — | 1 |
| Axeman | 20 | 7 | 37 | 30 | 1.1 | 9 / 10.71 | 1 |
| Maceman | 22 | 8 | 30 | 30 | 1.3 | 10 / 11.9 | 1 |
| Caravan Engineer | 22 | 9 | 35 | 60 | 1.2 | — | 1 |
| Billman | 22 | 8.5 | 31 | 52 | 1.2 | 8 / 9.52 | 1 |
| Man-at-Arms | 24 | 8 | 31 | 32 | 1.1 | 10 / 11.9 | 1 |
| Crossbowman | 24 | 8 | 31.5 | 94 | 1.5 | — | 1 |
| Shield Knight | 28 | 9 | 29 | 28 | 1.15 | 9 / 10.71 | 1 |
| Battle Monk | 22 | 10 | 33 | 64 | 1.2 | — | 1 |
| Paladin | 30 | 11 | 30 | 34 | 1.25 | 10 / 11.9 | 1 |
| Pikeman | 22 | 8.5 | 30 | 58 | 1.4 | 8 / 9.52 | 1 |
| Halberdier | 26 | 9.5 | 31 | 42 | 1.4 | 12 / 14.28 | 1 |
| Cavalry Rider | 24 | 9 | 68 | 24 | 0.9 | 10 / 11.9 | 1 |
| Gravebound Knight | 28 | 11 | 30 | 32 | 1.2 | 10 / 11.9 | 1 |
| Greatswordsman | 26 | 10 | 30 | 40 | 1.7 | 12 / 14.28 | 1 |
| Quartermaster | 22 | 11 | 30 | 64 | 1.3 | — | 1 |
| Berserker | 24 | 8 | 43 | 30 | 0.9 | 9 / 10.71 | 1 |
| Sapper Crew | 30 | 12 | 26 | 28 | 1.4 | 14 / 16.66 | 2 |
| Outrider | 22 | 8 | 68 | 28 | 0.9 | 8 / 9.52 | 1 |
| Longbowman | 24 | 9 | 31 | 128 | 2.2 | — | 1 |
| Warhammer Knight | 32 | 12 | 29 | 30 | 1.4 | 12 / 14.28 | 1 |
| Rogue | 22 | 8 | 54 | 26 | 0.9 | 8 / 9.52 | 1 |
| Duelist | 22 | 8 | 42 | 30 | 1 | 9 / 10.71 | 1 |
| Mage | 28 | 10 | 30 | 104 | 1.5 | — | 1 |
| Poleaxe Knight | 34 | 12 | 30 | 42 | 1.5 | 12 / 14.28 | 1 |
| Banner Knight | 26 | 10 | 29 | 28 | 1.1 | 8 / 9.52 | 1 |
| Ballista Crew | 36 | 14 | 22 | 116 | 2.1 | 24 / 28.56 | 1 |

Militia and Sapper Crew values are per body, with one courage payment deploying both. Speed, range, ordinary attack interval, deployment cost, and deployment cooldown stay fixed with level. Splash radius is zero for every core ordinary attack except Ballista Crew, whose proposed radius is now 22; special cleaves, sweeps, and area powers use separately authored patterns. Greatswordsman uses a bounded frontal cut instead of a radial explosion. Every special skill must fit its own power and trigger budget.

## Making melee choices distinct

Armor should change matchups without turning every late recruit into the same heavily armored fighter. Cloth and padded coats support cheap bodies; mail and reinforced armor support professionals; plate-inspired kits support expensive protection or heavy weapon commitment. Magical resistance is an independent stat, so physical armor does not solve magical damage. Plate-inspired visual progression does not require making knights incapable of movement.

Use contrasting attack silhouettes: a short club swing, forward spear thrust, hooking bill motion, stationary pike brace, frontal halberd sweep, long sword cut, shield interception, and committed hammer blow. Keep each special bounded. Billman cannot drag protected bosses; Pikeman cannot brace while running; Duelist cannot parry an entire volley; Warhammer Knight cannot chain-stagger a target indefinitely.

Maceman briefly exposes armor for allies, Poleaxe Knight penetrates armor for its own hit, and Axeman attacks a temporary shield pool or tactical blocker. These use different defenses and should not collapse into three copies of armor break. Shield Knight protects nearby allies from a limited projectile, Man-at-Arms guards itself between attacks, and Banner Knight supplies offensive formation support.

Support construction and utility cards complement infantry. Avoid requiring a turret, engineer, or siege card in every squad. A typical six-card lineup can use four melee choices, one ranged card, and one support card. Retain reachable ranged or spell answers when an encounter would otherwise make a melee-heavy squad nonviable.

## First build priority

Start with Peasant, Militia, Spearman, Swordsman, and Archer, plus a simple healing utility. Build Maceman, Axeman, Field Medic, and Caravan Engineer next. Before expanding further, verify ordinary attacks, reach, commitment windows, paired deployments, armor and both resistances, critical stats, touch readability, and the level 20 upgrade economy. Add Billman and the specialized professionals once their hook, guard, parry, and heavy attack patterns are readable and distinct.

Keep all current enemy, item, and spell designs available as reference. Adjust encounter counters and melee equipment builds during playtesting, without increasing the two-siege limit or quietly expanding the enemy debut budget. The previous four unit delivery packs are superseded for unit priority by this melee-first sequence; their enemy, relic, tactical gear, and spell lists remain reference pools.

## Earlier concepts kept in reserve

Alchemist, War Hound, Necromancer, Lantern Guard, Stormcaller, Javelin Skirmisher, Iron Sentinel, Firepot Thrower, Runesmith, Flame Adept, Plague Doctor, Flagellant, Trapper, Earthshaper, Spirit Keeper, Wardbreaker, Druid, Frost Witch, Mounted Archer, Executioner, Falconer, Illusionist, Chanter, Trebuchet Crew, Dragon Tamer.

Reserve status affects future recruitment. Five existing IDs outside the core remain usable when already owned, with saved progression retained; they are hidden from new-save recruitment. Add a reserve later only when it introduces a tested squad choice. A round collection count alone is not a reason to keep a duplicate.
