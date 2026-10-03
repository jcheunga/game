# Campaign and combat review

Review scope: all 60 authored stages, normal encounters, district commanders, veteran encounters, targeting, reinforcement scheduling, courage disruption, hazards, and battle controls.

## Findings and changes

The largest problems were combat rules amplifying the numbers, rather than a shortage of enemy types.

| Finding | Change |
| --- | --- |
| A gate rush could skip most waves, including the first boss. The baseline cleared stage 4 in 34 seconds without encountering its commander. | Scripted battles require a breached gate and defeated defenders from every wave. Troops seek surviving enemies after breaching the gate. The HUD distinguishes a breached gate from victory. |
| Health, damage, attack speed, and damage against the wagon all escalated together. By late campaign, distinct enemy attacks collapsed toward the same 0.45-second cooldown. | Authored attack rhythms now persist. Health grows from 0.90× to 2.00× and damage from 0.90× to 1.45× across 60 stages. The additional wagon damage is bounded. Gate health grows from 380 to 1,488 before modifiers, instead of ending at 11,800. |
| Melee units could chase a high-priority caster while adjacent enemies attacked them. | Frontliners engage enemies in reach first. Ranged troops retain support targeting; the Rogue retains its backstab targeting. |
| Summoning and boss phases modified the collection being simulated, causing exceptions. | Simulation and phase evaluation iterate the units present at the start of the pass. Reinforcements act on the following tick. |
| A future reinforcement could block an earlier one; overdue enemies could appear together after a full battlefield cleared. | Stable chronological scheduling and a minimum release interval prevent queue blocking and single-frame bursts. Authored queued enemies are retained. |
| Success could leave long empty waits, while a struggling player could receive another complete wave. | A clear battlefield provides four seconds of visible recovery. A crowded frontline holds the next wave until pressure thins. The normal enemy cap increases in smaller steps. |
| District finales repeatedly used the generic Grave Lord, while several preceding stages used another district's boss. | The ten primary district finales use their own commander identities. Intermediate encounters use veteran enemies, and the late named rematches remain. |
| Boss phase attacks were immediate; early commanders could repeatedly build very heavy escort groups. | Phase changes warn for 1.6 seconds and pause the commander during the warning. Early escort groups are smaller; recurring summons respect the enemy cap. |
| Several hexers could repeatedly delay every card and keep refreshing a signal jam. | Active enemy jams do not stack their card penalties, and ordinary hexers respect a three-second recovery window. Authored boss phases and scripted mutators still have their own effects. |
| Shield Walls did not intercept shots from the player's side because the position check was reversed. | Shields now protect backliners when positioned in front of them. |
| Cursed ground damaged every deployed ally, including troops far from combat. | Cursed ground occupies a visible central strip with safe lanes above and below it. Briefings describe its actual area effect. |
| The opening introduced too many enemy mechanics, side objectives, and inactive command buttons. | Stages 1–4 teach basic deployment, runners, brutes/casters, and the first commander. Automatic side missions begin afterward. Contextual orders appear when available; the caravan command shows its charging/spent state. |
| Veteran stages combined new heavy archetypes with a large extra stat modifier. | Heavy introductions are separated, siege enemies attack more deliberately, captain buffs are smaller, and the late elite modifier rises from 1.12× toward 1.30×. |

The opening no longer asks players to conserve deployments before they have learned the basic frontline/backline relationship. Early optional goals emphasize wagon protection and completion time.

## Intended progression

| Stages | Main lesson or pressure |
| --- | --- |
| 1 | Match a deployment to the enemy's height; protect the Archer with a frontline. |
| 2 | Respond to runners in the correct lane. |
| 3 | Hold brutes while ranged units deal with casters. |
| 4 | First commander: recognize the phase warning and retain a reserve. |
| 5–8 | Death bursts, split enemies, support targets, and the Tidecaller finale. |
| 9–16 | Introduce marked hazards, sappers, and signal disruption without removing the recovery window. |
| 17–30 | Combine familiar counters with route weather and objectives; deployment position matters. |
| 31–46 | Mature squads handle combinations and route-specific boss phases. |
| 47–60 | Veteran remixes and heavier siege units build toward the named final commanders. |

Stage numbering follows the existing campaign and revisits; this review does not renumber stages, move unlocks, reset saves, or change authored stage rewards.

## Verification and limits

The first-pass 120-run stage-by-stage results are in [COMBAT_BENCHMARK.md](COMBAT_BENCHMARK.md).

- Real-engine regression checks cover summoning, boss warnings and resolution, gate/wave victory conditions, reinforcement order and backpressure, target selection, shields, cursed-ground safe lanes, and jam recovery.
- Two benchmark profiles exercise all 60 stages with a fixed combat seed per stage and half-second decisions. Saves are isolated and reset between stages.
- The basic profile uses the starter squad, conservative unit levels, and no spells. The tactical profile uses stage-appropriate unit upgrades, modest wagon upgrades, and Fireball/Heal. It has no doctrines, equipment, promotions, or skill-tree investment. Alternate unlocked squads can be supplied explicitly.
- Benchmark losses and timeouts are balance observations, not test-suite passes or failures. A clean run means the simulation completed without the regression failures; it does not mean every stage was won.
- A real-input stage playthrough verifies victory, persisted stars, and return to the map. Rendered fixtures verify the boss warning and cursed-ground marking.

A follow-up direct input review uses the actual game window, cards, spells, and pause controls, including late-game squads. It exposed gate resurrection, accumulating Reliquary artillery and repeated healing, an unannounced opening tunneler, a stalled retreat from pause, and completed objective markers covering later fights. Those issues are now addressed. See [DIRECT_PLAYTEST.md](DIRECT_PLAYTEST.md) for observations and verification. Direct runs use pauses and test-clock adjustments; their outcomes are not a human win-rate estimate or a substitute for the fixed benchmark. Phone-size readability remains unverified.

## Reproduce

```sh
./scripts/smoke/combat_review_smoke.sh
./scripts/smoke/combat_review_smoke.sh --campaign
./scripts/verify_all.sh
```

Logs and rendered checks are written under `artifacts/combat-review/`. `CombatReviewSmoke` accepts `--stages=...`, `--tactical`, and `--squad=unit_id,unit_id,unit_id`. Use `--courage-pacing` for the focused regeneration and deployment checks. Every invocation requires a unique `--save-suffix=combat-review-...`; rendered checks use a graphical run with `--regressions --screenshots`.

The pacing changes apply to scripted encounters, including challenge boards. Endless retains its checkpoint structure, with chronological reinforcement handling and bounded attack-speed growth. Existing challenge scores from older tuning should not be treated as comparable balance benchmarks.

## Direct-review changes

- A breached gate stays destroyed; intact gates can still receive repairs.
- Reliquary Tyrant can maintain two bone artillery crews, replacing casualties. Graveyard resurrection respects that same limit when a replacement has already arrived. Its initial phase still heals once, but recurring Ossuary Fire no longer heals it.
- Tunnel invasion first warns at 16 seconds and emerges at 18 seconds, with a visible location marker. Further warnings have 22–28 seconds of recovery after the preceding arrival, and two living tunnelers suppress additional invasion spawns.
- Retreat releases pause before starting the scene transition.
- Completed or failed objective actors show their outcome briefly, then fade away to clear the battlefield.
- The final Reliquary retest removed the second wave lich from Sepulcher break in favor of a spitter. The Tyrant's attack interval is now 1.8 seconds and its summon interval 15 seconds, preserving its health and damage per hit while giving a rebuilt frontline more time to act.

## September 10 finish pacing

- Using an earned field order while a boss is alive or after the gate is breached still grants its combat effects and reinforcements. It no longer starts an additional branch objective at that point. This prevents an order used to finish a boss from creating a new failure penalty and counter-wave. Earlier branch objectives retain their normal behavior.
- Tidemaster's rally calls and phase summons now share the siege-artillery check: two living plague engines suppress further boss engine spawns, and casualties can be replaced. Existing authored enemies are counted, not removed. Other escort types retain their normal behavior. Encounter intel explains this opening.
- The expanded real-engine suite verifies both the ordinary and late field-order paths, their reinforcements, existing siege units, replacement after a casualty, phase summons, and unrelated escorts.

## October 2 courage pacing

Passive courage generation is now 3 per second, down from 4.5 (33% slower).
The authored configuration and fallback tuning agree. At this review, starting
courage was 45 and maximum courage remained 100; earned bonuses, upgrades, stage modifiers
and difficulty scales continue to apply. Before bonuses, a 20-courage Swordsman
takes about 6.7 seconds to fund from empty, compared with 4.4 seconds previously.

The build passed without warnings or errors. Five native pacing checks passed:
the opening budget and scout bonus, delayed affordability, pause behavior,
successful deployment with its normal cost and the maximum-courage cap. A
Swordsman/Archer/Shield Knight tactical squad cleared the sampled stages 1, 6,
12 and 21 at the new rate. These automated samples verify playable paths,
not human win rates or complete campaign balance.

The wider combat suite passes 611 checks and still fails three existing checks
for early gate completion, living-commanders blocking victory and ranged support
targeting. All three were reproduced at the original 4.5 rate before the new
pacing checks ran. The old rate also fails the new affordability checks, as
expected. Benchmark shutdown reports retained resources at both rates; the
focused native pacing review exits cleanly. Logs are in
`artifacts/courage-pacing/`.

## October 3 battle screen polish

The live battle HUD now shows wagon health and courage at the upper left and
gold at the upper right. Deployment cards sit individually over the scene;
the full-width HUD and card-panel backgrounds are removed. Wave counts, wave
progress, elapsed time, forecasts and the combat-intel panel stay hidden.
Immediate hazard and boss warnings still provide feedback during combat.

The bottom-left gear opens a paused menu with Resume, Restart, Game settings
and Quit battle. Restart shows a ration icon and the normal stage-entry price.
Both menu restarts and result-screen retries charge once, reject insufficient
rations without replacing the battle, and release the scene-tree pause before
the transition. Shared matches cannot restart individually. Settings opens
inside the paused battle, offers Sound and Gameplay, and returns to the same
pause menu. Keyboard focus stays inside the menu and phone settings refits
when the window changes size.

Stage panoramas now render without changing their proportions, with natural
perimeter cropping instead of squeezed scenery strips. The 60 originals use
lossless imports. A new nine-material atlas adds world-sized fine ground
detail for all ten zone themes, with mipmaps, blended variation and feathered
edges. Movement, spawning, aiming and camera bounds are unchanged. Asset
provenance and the exact built-in generation prompt are in
`assets/world/battles/polished-v2/`.

The build passes with zero warnings/errors. `UiReviewSmoke --battle-polish`
passes 61 native checks across desktop, a smaller window and phone layouts,
and captures one battlefield for each of the ten zone themes. The deployment
card review passes 89 checks, the drag review passes 81, and the camera review
passes 27. Captures and logs are in `artifacts/battle-background-polish/`.
The verbose card-review shutdown still reports two retained title-music
resources (`title.ogg` and its Ogg packet sequence); these are audio shutdown
diagnostics, with no UI assertion failures. The final drag run also reports six
retained resources at shutdown; all 81 interaction assertions pass. The focused
battle-polish and camera runs finish without engine diagnostics.

## October 3 actor lighting and perspective

The caravan, its seven skins and the enemy gatehouse use new renders of their
editable 3D sources, with a shared 22-degree orthographic view. The gate faces
left into the battle. Projected ground anchors put wheels and foundations on
the terrain; roof weapons and their projectile origins use projected sockets.

Soft cast shadows follow the current animation silhouette, with denser contact
shadows under feet, wheels and stonework. Shadows render above terrain and
below actors. Ground-position sorting resolves overlaps between troops and
bases, while projectiles remain above them. Zone light color and restrained
lantern/torch light apply to bodies and death poses; health bars retain their
UI colors. Pooled troops clear all environmental and local light state.

`UiReviewSmoke --battle-lighting` captures both bases in all ten zones and the
phone close/overview layouts, and compares actual rendered pixels with the
shadow layer hidden to verify that shadows reach the visible terrain.
`CombatReviewSmoke --base-weapons` exercises targeting, mounted shots, pooling,
upgrades, pause, destruction and endless mode. Native captures and logs are in
`artifacts/battle-lighting/`. Render sources and repro steps are in
`assets/structures/battle-v2/README.md`.

The build passes with zero warnings/errors. The lighting review passes 43
checks, weapon checks pass 39, the 53-unit animation review passes 119, camera
checks pass 27 and battle-menu/layout checks pass 61. Some focused test runs
report retained resources at engine shutdown; their assertions all pass.

## October 3 battle simplification and return screens

Every mode starts at zero courage, including Endless opening boons and campaign
bonuses. Surplus Courage increases regeneration instead of granting an opening
budget. Deployment requires an explicitly selected owned warband card; a
successful deployment clears that selection. Summoning abilities, automatic
allied reinforcements and their Endless options are retired. Resurrection can
restore an owned member of the current battle roster.

All enemy entries use the enemy stronghold, including advance-triggered waves
and commander reinforcements. Bellfire, Market Collapse, tunnel attacks,
scripted side-objective actors and automatic field events are removed. Normal
enemy encounters, commander phases, base weapons and Endless checkpoints
remain. Map exploration and travel are free; battle entry and restart retain
their ration costs.

Ballistas launch shaded, arcing boulders for ordinary attacks, base attacks and
Anchor Shot. Ranged base damage applies once when its projectile arrives.
Spell previews, area rings and ground effects share the battlefield projection.
Ambient and weather particles use transparent soft textures at their intended
size, with a generated fallback if a texture cannot load. Pause contains only
its menu actions, without the old keyboard-instruction paragraph.

Campaign victory presents earned stars and icon reward cards instead of the
text report. The cards use actual awarded resource, experience and mastery
deltas, including unlocks and upgrades. Presenting the result twice cannot
award it twice. Quitting returns directly to the map; Endless banks its payout
once without opening a preparation or report modal. Explicit preparation
returns use the current home modal, including Endless, Tower, Arena, events and
challenges. Phone launch actions remain outside scrolling bodies.

The build passes without warnings or errors. `UiReviewSmoke --battle-cleanup`
passes 120 checks at both desktop and smaller-window sizes, including all 22
home activity destinations and phone preparation/Endless returns.
`UiReviewSmoke --drag-cards` passes 81 interaction checks. The combat star suite
passes 487 checks and mounted weapons pass 39. Native victory reviews pass 20
checks at each window size, including a full squad's rewards and payout
idempotency. Logs and captures are in
`artifacts/battle-cleanup/` and `artifacts/stage-stars/`. Some reward/weapon
fixtures retain existing resource-cleanup diagnostics at engine shutdown.
