# Crownroad — the 30-spell grimoire

Design proposal · 8 October 2026 · 10 existing spells + 20 additions.

The expanded grimoire gives the Lantern Caravan more ways to protect specialist troops, manipulate a formation, answer a particular enemy rule, or combine effects with its recruits and relics. This is proposed content, not an implemented spell patch. New damage, duration, radius, and acquisition values still need encounter testing.

## Collection and battle loadout

| Collection | Existing | New | Total |
|---|---:|---:|---:|
| Spells | 10 | 20 | **30** |
| Tactical items | 0 | 12 | **12** |
| Combined utility choices | 10 | 32 | **42** |

Keep **five shared utility slots** per battle: spells and tactical items compete for those positions. A large collection gives more build choices without turning each battle into a tray of 42 actions. Spell families are browsing categories, not extra meters, class requirements, or mandatory set bonuses.

The new mana prices and cooldowns are prototype estimates. Existing prices and cooldowns below are copied from the current game data. The new spells fit within the current base mana cap of 30; mana still comes from combat kills rather than passive regeneration. The design does not add a second casting resource.

## Spell families

| Family | Count | Primary job | Spells |
|---|---|---|---|
| Damage | 6 | Clear a cluster or expose an armored target. | Fireball, Lightning Strike, Earthquake, Molten Brand, Meteor, Chain Lightning |
| Protection | 6 | Keep a living specialist or formation alive. | Heal, Barrier Ward, Aegis Bolt, Purifying Light, Verdant Renewal, Mirror Ward |
| Control | 7 | Change enemy movement, space, or commitment. | Frost Burst, Stone Barricade, Polymorph, Tidal Push, Anchor Hex, Gravitation Well, Bramble Snare |
| Command | 4 | Reposition, rally, or disrupt a formation. | War Cry, Gale Step, Rallying Advance, Royal Edict |
| Ritual | 3 | Spend or deny corpse resources and recover a fallen ally. | Resurrect, Soul Harvest, Consecrated Ground |
| Counter | 4 | Answer a particular enemy support or concealment rule. | Radiant Rebuke, Withering Seal, Revealing Flare, Banish |

## Existing ten spells: retain and differentiate

The intended identities in this table include planned refinements. They do not claim that current Polymorph, Frost Burst, or other spells already implement every proposed status rule.

| Existing spell / family | Current discovery, mana, cooldown | Intended identity |
|---|---|---|
| Fireball · Damage | Stage 1 · 8 mana · 12s | Immediate enemy-only area damage; the inexpensive fast answer to a cluster. |
| Heal · Protection | Stage 3 · 8 mana · 13.5s | Immediate ally area healing with limited wagon repair; not continuous regeneration. |
| Frost Burst · Control | Stage 9 · 8 mana · 15s | Quick area chill and modest damage; the current runtime uses movement slow, with explicit chill proposed. |
| Lightning Strike · Damage | Stage 15 · 10 mana · 16s | Focused burst near the aimed location; true multi-target chain belongs to Chain Lightning. |
| Barrier Ward · Protection | Stage 23 · 10 mana · 18s | Temporary group damage reduction; not Aegis Bolt's finite individual shield pool. |
| Stone Barricade · Control | Stage 5 · 10 mana · 20s | Immediately creates a temporary durable allied blocker; no construction windup. |
| War Cry · Command | Stage 12 · 12 mana · 22s | Global brief damage-and-movement rally for deployed allies; attack recovery is a separate stat. |
| Earthquake · Damage | Stage 28 · 12 mana · 24s | Broad direct damage and ground slow; a stronger immediate control impact than a prepared root strip. |
| Polymorph · Control | Stage 19 · 8 mana · 18s | Implement a bounded transformation with attack suppression, or rename it to describe the current slow/vulnerability debuff; bosses get a shorter soft-control alternative. |
| Resurrect · Ritual | Stage 33 · 12 mana · 26s | Restore one eligible fallen ally at partial health while preserving its spent survival/return state in the expanded design. |

## Twenty new spells across the ten districts

Discovery stages reveal an optional spell purchase/contract and its counter examples. They are proposals; existing unlock stages stay unchanged in this design. Each new spell has a primary job and a tradeoff, rather than being a higher-cost universal replacement for an early spell.

### King's Road

| Spell / prototype estimate | Target and effect | Job and tradeoff | Limits | Visual brief |
|---|---|---|---|---|
| **Aegis Bolt** · Protection<br>Stage 4 · 6 mana · 12s | One allied troop. Give one ally a small temporary shield pool. | Save a fragile specialist before a heavy hit. One target; does not protect the wagon or grant permanent defense. | Shield amount and duration cap; separate from Barrier Ward's group damage reduction. | A narrow amber bolt resolves into a small hexagonal shield. |
| **Radiant Rebuke** · Counter<br>Stage 7 · 6 mana · 14s | One enemy troop. Interrupt a marked channel and deal modest direct damage. | Stop Bellringer, a healer, or a marked charge windup. Poor raw damage; ordinary attacks are not channels. | Boss disruption obeys an immunity window; does not cancel entire scripted phases. | An ivory hand-shaped flash snaps the enemy's cast glyph. |

### Saltwake Docks

| Spell / prototype estimate | Target and effect | Job and tradeoff | Limits | Visual brief |
|---|---|---|---|---|
| **Tidal Push** · Control<br>Stage 14 · 8 mana · 18s | An aimed short line. Push affected enemies a short distance toward their stronghold. | Separate a support pack or relieve pressure near the wagon. No large damage; displacement-resistant enemies move less. | Push distance and affected-body count cap; never pushes through map bounds or teleports behind bases. | A low blue crest travels along the lane in the chosen direction. |
| **Anchor Hex** · Control<br>Stage 18 · 8 mana · 18s | One enemy troop. Temporarily prevent special leap, burrow, phase, or charge movement. | Keep a Tunneler or Cliff Stalker in the frontline. Normal walking and attacks continue; it is not a full stun. | Applies before the movement commitment; cannot undo a leap already resolved. Bosses resist repeated applications. | A ghostly chain and anchor settle around the target's feet. |

### Emberforge March

| Spell / prototype estimate | Target and effect | Job and tradeoff | Limits | Visual brief |
|---|---|---|---|---|
| **Molten Brand** · Damage<br>Stage 24 · 10 mana · 18s | One armored enemy. Briefly soften physical armor and apply a small burn. | Open Rustguard, Bone Juggernaut, or a fortified enemy for physical attackers. Less valuable against unarmored targets or magical wards. | Armor reduction has a floor and does not stack with copies; cannot target the stronghold. | An orange brand traces a crack across armor plates. |
| **Meteor** · Damage<br>Stage 29 · 18 mana · 28s | A marked ground area. After a visible delay, deal strong area damage and leave a brief fire patch. | Punish a committed dense pack or stationary caster formation. High mana cost; mobile enemies may leave the warning area. | One impact; burn stacks are capped. Cannot damage structures or skip a boss vulnerability state. | An amber ground mark precedes one fiery stone and a short ember field. |

### Ashen Ward

| Spell / prototype estimate | Target and effect | Job and tradeoff | Limits | Visual brief |
|---|---|---|---|---|
| **Purifying Light** · Protection<br>Stage 32 · 8 mana · 14s | A small allied area. Instantly remove blight, wounds, and burn from a few allies, with a small heal. | Restore healing effectiveness during Miasma Priest or Crown Executioner pressure. No cleanse of root, sleep, or hostile terrain; modest healing. | Cleanses existing statuses once; grants no ongoing immunity. Distinct from Purifying Censer's placed pulses. | A clean ivory pulse peels dark motes away from affected troops. |
| **Withering Seal** · Counter<br>Stage 38 · 10 mana · 20s | One enemy troop. Reduce healing received and ward regeneration for a short window. | Limit Rot Surgeon support or a regenerating colossus. Low direct damage; does not strip an existing shield or stop the stronghold's repair. | Boss reduction is smaller; repeated casts refresh within a cap rather than multiply. | A black-and-green broken ring closes around a healing sigil. |

### Thornwall Pass

| Spell / prototype estimate | Target and effect | Job and tradeoff | Limits | Visual brief |
|---|---|---|---|---|
| **Gale Step** · Command<br>Stage 43 · 6 mana · 14s | One ally and a short destination. Move one allied troop a short distance along the battle band. | Rescue a caster from a warning area or reposition a slow crew. No damage; interrupted setup must begin again, and committed melee cannot instantly jump to a new target. | Valid destination stays in the band; invalid placement spends nothing. Movement does not reset cooldowns or create a free attack. | A teal ribbon traces the allowed short movement path. |
| **Gravitation Well** · Control<br>Stage 48 · 12 mana · 22s | A small ground area. Briefly draw eligible enemies toward a marked center without changing their target allegiance. | Gather a spread pack for Alchemist, Stormcaller, or Meteor. No lure and no damage by itself; heavy enemies move less. | Limited pull force, duration, and body count. No overlap multiplier or permanent trap. | A low violet spiral with a plainly bounded pull area. |

### Hollow Basilica

| Spell / prototype estimate | Target and effect | Job and tradeoff | Limits | Visual brief |
|---|---|---|---|---|
| **Soul Harvest** · Ritual<br>Stage 53 · 4 mana · 22s | An area containing eligible enemy corpse tokens. Consume up to three existing corpse tokens to recover mana. | Trade potential allied summons for another utility cast. Needs corpses and an initial payment; empty casts are canceled for free. | Prototype refund is capped at 10 mana before the 4-mana cost, for at most +6 net; only consumes each token once. No tokens from summons or split bodies, no proc-generated extra refund. | Up to three pale spirit threads flow toward the caravan meter. |
| **Consecrated Ground** · Ritual<br>Stage 58 · 10 mana · 22s | A small ground area. Prevent hostile use of corpse tokens in the area for a short window. | Deny Bone Cantor or Soul Standard while allies keep advancing. No damage or healing; friendly conversion can still consume those tokens. | Tokens are protected, not copied or refunded; sealing ends with the field. Live enemies and already-raised bodies are unaffected. | An ivory-and-amber ring of low candle marks outlines the ground. |

### Mire of Saints

| Spell / prototype estimate | Target and effect | Job and tradeoff | Limits | Visual brief |
|---|---|---|---|---|
| **Verdant Renewal** · Protection<br>Stage 63 · 8 mana · 16s | One allied troop. Attach a short regeneration effect that follows its recipient. | Sustain a mobile ally or soften damage-over-time attrition. Slow recovery cannot save an ally from immediate lethal burst; wounds reduce it. | Refreshes one regeneration effect rather than stacking many; cannot heal the wagon or restore dead troops. | A small vine ribbon coils around the target and releases green healing sparks. |
| **Bramble Snare** · Control<br>Stage 68 · 10 mana · 20s | A short ground strip. Create visible roots that briefly bind a limited number of crossing enemies. | Delay a fresh rush after the frontline has already passed. Rooted enemies can still attack; ranged threats need another answer. | At most three root activations, then the strip expires. Boss control is shorter and repeated roots are resisted. | Three thorn knots grow across a short strip of the lane. |

### Sunfall Steppe

| Spell / prototype estimate | Target and effect | Job and tradeoff | Limits | Visual brief |
|---|---|---|---|---|
| **Chain Lightning** · Damage<br>Stage 73 · 14 mana · 22s | One enemy and nearby secondary targets. Strike the primary target and jump to up to three different nearby enemies with falling damage. | Hit support behind a staggered pack without requiring one tight impact cluster. Weak against one isolated enemy; chain fails across large gaps. | At most four unique targets; no repeated target, proc recursion, reflection chain, or structure hit. | A bright branching arc traces each distinct target in sequence. |
| **Rallying Advance** · Command<br>Stage 78 · 10 mana · 18s | A small allied formation. Remove root or movement slow and briefly boost movement with displacement resistance. | Carry a vulnerable formation through a pull or rooting threat. No attack-damage boost and no cleanse of sleep, blight, or wounds. | Movement only; does not speed attack recovery, card preparation, or siege setup. Distinct from War Cry's offensive buff. | Amber pennant trails appear low behind the affected troops. |

### Gloamwood Verge

| Spell / prototype estimate | Target and effect | Job and tradeoff | Limits | Visual brief |
|---|---|---|---|---|
| **Revealing Flare** · Counter<br>Stage 83 · 6 mana · 12s | A marked small area. Reveal concealed enemies and identify the genuine body among illusions. | Expose Shade or Mirror Wisp without recruiting a reveal specialist. No direct damage; a short local window rather than global detection. | Does not bypass invulnerability, wards, or boss vulnerability cycles; revealed illusions remain until hit or expired. | A white-gold flare hangs briefly above a bounded ground glow. |
| **Mirror Ward** · Protection<br>Stage 88 · 12 mana · 24s | One allied troop. Deflect a limited number of direct projectiles back toward their origin at reduced damage. | Protect a backline specialist from hostile archers or artillery's direct bolt. Melee, splash, and ground fields bypass the ward. | Prototype cap of two eligible deflections; no re-reflection, new on-hit proc, or base damage from returned shots. | Two small silver panes orbit the selected ally and shatter on use. |

### Crownfall Citadel

| Spell / prototype estimate | Target and effect | Job and tradeoff | Limits | Visual brief |
|---|---|---|---|---|
| **Banish** · Counter<br>Stage 93 · 14 mana · 24s | A small enemy area. Remove a limited number of temporary enemy summons or illusions; natural enemies lose one removable ward instead. | Clear ritual-made screens or expose a warded caster. Cannot delete natural elites, bosses, or hostile structures. | At most three summoned bodies or removable wards; no kill loot, corpse creation, or percentage-health damage from banishment. | An ivory doorway closes behind up to three fading silhouettes. |
| **Royal Edict** · Command<br>Stage 98 · 16 mana · 28s | A small enemy formation. Briefly suppress eligible enemy special abilities and command auras. | Open a window against layered Herald, Hexer, or caster support. Enemies keep their ordinary attacks and movement; cast cost is high. | Bosses receive only a short disruption to marked channels, never a whole-phase cancellation. Does not erase placed hazards or grant permanent silence. | A royal seal flashes above the area as enemy aura glyphs dim. |

## Differences that keep these choices meaningful

| Choices | Reason to pick one over the other |
|---|---|
| Fireball / Meteor | Fireball hits immediately for less mana. Meteor rewards predicting a committed pack, with a delay and a brief burn field. |
| Lightning Strike / Chain Lightning | Lightning Strike delivers focused burst around one location. Chain Lightning reaches distinct enemies across a staggered formation and loses damage per jump. |
| Heal / Verdant Renewal / Healing Shrine | Heal provides immediate area recovery. Renewal follows one mobile ally over time. Shrine is a placed, destroyable source of repeated healing. |
| Barrier Ward / Aegis Bolt / Mirror Ward | Barrier reduces incoming damage for an area. Aegis grants one ally a finite shield pool. Mirror deflects a small number of eligible direct projectiles. |
| Frost Burst / Anchor Hex / Bramble Snare | Frost Burst applies area chill. Anchor disables special movement on one enemy without stopping ordinary walking. Bramble roots a few enemies crossing a prepared strip. |
| Radiant Rebuke / Grounding Rod / Royal Edict | Rebuke interrupts one marked channel now. Rod prepares one interrupt at a location. Edict briefly suppresses eligible special abilities and auras across a small formation. |
| Purifying Light / Purifying Censer | Light immediately clears existing blight, wound, and burn in a small group. Censer supplies placed cleansing pulses for blight and wounds, without removing every status. |
| Tidal Push / Gravitation Well / Decoy Lantern | Push displaces enemies toward their stronghold. Well gathers eligible bodies locally. Decoy redirects eligible targeting toward a fragile object. |
| War Cry / Rallying Advance / War Horn | War Cry raises damage and movement. Advance clears movement control and resists displacement without raising damage. Horn changes focus priority. |
| Soul Harvest / Consecrated Ground / Banish | Harvest spends eligible corpses for mana. Ground temporarily denies hostile corpse use. Banish removes limited temporary bodies or removable wards already present. |

## Example spell-heavy loadouts

Each example occupies all five utility slots; any tactical item replaces one of these spells.

| Build | Five-spell selection | What it gives up |
|---|---|---|
| Basic caravan | Fireball, Heal, Frost Burst, Stone Barricade, Radiant Rebuke | Advanced reveal, corpse control, and specialized healing denial. |
| Protect the specialist | Aegis Bolt, Heal, Gale Step, Mirror Ward, Purifying Light | Strong area damage and offensive rally. |
| Burn and gather | Fireball, Meteor, Gravitation Well, Molten Brand, Withering Seal | Direct healing, revival, and broad protection. |
| Break the ritual | Radiant Rebuke, Consecrated Ground, Revealing Flare, Banish, Heal | Persistent siege fire and movement buffs. |
| Keep the charge moving | Tidal Push, Anchor Hex, Rallying Advance, War Cry, Verdant Renewal | Area cleanse, corpse tools, and shields. |
| Grave covenant | Soul Harvest, Consecrated Ground, Resurrect, Barrier Ward, Heal | Revealing illusions and strong immediate spell damage. Harvest also competes with Necromancer for corpse tokens. |

Enemy lineups should make these tradeoffs visible in preparation. The player should not need to memorize all 30 spells to choose a reasonable early loadout.

## Counter and interaction rules

- **No spell-only mandatory counter.** A healer can be interrupted, focused, or denied healing; a charger can be blocked, intercepted, slowed before commitment, or movement-locked. Every required threat has another available answer.
- **Preserve siege jobs.** New spell damage does not automatically hit the enemy stronghold. Molten Brand targets an armored troop, and Withering Seal does not stop scripted stronghold repairs. Banish never deletes a boss or structure.
- **Bound crowd control.** Bosses shorten or soften control; repeated control applies a resistance window. Interrupts apply only to visibly marked channels. Silence does not permanently switch off a commander or cancel an entire scripted phase.
- **Respect target state.** Concealment, vulnerability, removable wards, corpse tokens, summon origin, and movement commitment must be explicit. A spell's name is not evidence that it bypasses a state.
- **Prevent trigger loops.** Returned projectiles cannot reflect again. Chained or dispelled effects do not create recursive on-hit, kill-loot, or corpse events. Item echoes cannot earn new casting refunds or charges.
- **Keep Soul Harvest finite.** The prototype restores 4 mana per eligible consumed corpse, capped at 10 total, after paying 4 mana: at most +6 net. Corpse tokens are consumed once; summons and split bodies create no new tokens. Empty casts cancel for free. A preview should show the expected net recovery.
- **Share revival allowance.** Resurrect respects the same paid-instance origin and survival/return limit as Gravebound Knight and lethal-save relics. It cannot reset spent death triggers.
- **Let terrain remain dangerous.** Cleansing a status does not erase its hostile source; standing in a Miasma cloud can reapply blight. Placed objects, auras, and spell fields obey separate visible lifetimes.
- **Handle null fields locally.** Null Magister's effect has an explicit boundary. Physical attacks, interruption before completion, and utility placement outside that boundary remain answers. The armory/briefing explains which magical effects it suppresses.

## Presentation and targeting

Use the existing tap/select-and-aim or drag-to-aim interaction. Portraits and mana costs stay readable at mobile size. Ally-target spells show an eligible ally highlight; line spells show the short strip; Gale Step previews both the selected troop and a valid destination; corpse spells preview eligible tokens and expected resource gain. Invalid targets or canceled gestures spend nothing.

Warnings, animation, and effects explain execution: Meteor has a ground warning, Mirror Ward shows its remaining panes, Anchor Hex shows a chain, and Royal Edict dims eligible command glyphs. Combat stays numbers-only; names and detailed rules remain in preparation, tooltips, and codex. A large magical effect must not obscure the enemy's own attack warning or the shallow walking band.

## Four delivery packs

Each content pack adds five spells. Pack membership is an implementation grouping, not a replacement for campaign discovery order. Later-pack spells cannot be required by earlier playable encounters.

| Pack | Five spell additions | Main shared work |
|---|---|---|
| 1 | Aegis Bolt, Radiant Rebuke, Tidal Push, Molten Brand, Gale Step | Individual shields, channel interruption, line displacement, armor weakening, ally movement. |
| 2 | Purifying Light, Withering Seal, Gravitation Well, Verdant Renewal, Bramble Snare | Cleansing, healing denial, regeneration, root strips, bounded pull fields. |
| 3 | Anchor Hex, Soul Harvest, Consecrated Ground, Revealing Flare, Mirror Ward | Mobility locks, corpse consumption/protection, reveal, projectile deflection. |
| 4 | Meteor, Chain Lightning, Rallying Advance, Banish, Royal Edict | Delayed fire impact, bounded chains, formation marching, dispel, area special suppression. |

Implement shared mechanics first, then connect authored spell definitions, targeting, mana/cooldown accounting, counters, visual feedback, audio, rewards, localization, codex, save registration, and asset checks. Adding names and icons alone does not complete a spell.

## Reference and status

Current spells: [live spell data](/Users/jason/side/game/data/spells.json). Expanded collection: [full content plan](/Users/jason/side/game/docs/CONTENT_EXPANSION_PLAN.md). Structured proposals: [design catalog](/Users/jason/side/game/docs/design/content-expansion-roster.json). Existing behavior and gaps: [Dead Ahead comparison](/Users/jason/side/game/docs/DEAD_AHEAD_COMPARISON.md).

All twenty additions are design proposals. The live game still has ten spells until implementation and validation are completed.
