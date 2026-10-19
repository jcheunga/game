# Production polish

Updated 12 October 2026. This records the non-art polish pass requested by the user. Hard Mode and battle mutator interfaces remain deferred. No artwork, models, music or sound files were created or replaced.

## Reliability

- Repeated Deploy input pays the stage entry cost once while the scene loads. Affordability and its explanation refresh together.
- Restoring preferences preserves the account session, caravan name, progress, privacy choices and service connections. It restores player preferences without enabling debug displays.
- Save writes flush and validate their staged file before replacing a valid save. Backup recovery and save status are available in Settings. Save failures have a readable status.
- Offline battles pause on backgrounding or focus loss. Pending card/pickup gestures are canceled. Returning waits for deliberate Resume; leaving a paused battle clears tree pause and battle speed.
- Failed scene changes release the loading overlay and transition latch.
- Boss-rush retreat records completed commanders and pays their earned rewards once, then returns to its menu. Completion/defeat has the existing result sound.

Background pause preserves the current running fight. An app restart restores persistent campaign data; it does not reconstruct a mid-fight simulation.

## Mobile and menus

Battle settings now use their complete screen on a separate layer. Volume controls and Back to Battle remain reachable on desktop and phone. Bottom popup messages and tutorial banners are removed across all screens, including purchase/upgrade confirmations, menu hints and battle hints. Older activity screens omit message-only footer strips and give that space to their content; footer action buttons remain available. Battle action warnings remain removed, including mana, cooldown, invalid-target and full-field denials. Account operations report their status within the Settings list. Unaffordable restart controls are disabled without a warning popup. Description/detail text respects the reading-size preference. Fixed artwork typography has not been redesigned.

Preparation's battlefield picture opens an enemy briefing with current enemy levels, resolved stats, descriptions and counters. Later ranged recruits use the correct preparation role icon. More activities retain their existing map-preserving routing.

## Combat feedback

Single-target, healing, instant-area and corpse-dependent spells validate their target before charging mana or cooldown. Invalid targets are rejected without a text popup; the bar and target tint provide feedback. Placement tools and persistent ground effects can still prepare empty ground; a repair cache must be beside the wagon. Drag previews show invalid targets visually and identify valid single-target recipients. Noncombatants are excluded from hostile content targeting.

The Archer and other bow/crossbow special shots defer to melee when an enemy is at arm's length. Critical splash hits now receive the same CRIT callout as direct hits. Existing status badges, shield bars and marked cast warnings remain active.

Courage packs retain manual click/tap collection, +20 courage for eligible random drops, eight-second expiry and their final fade. There is no pickup countdown ring or gameplay auto-collection.

## Onboarding and pacing

Bottom tutorial popups and the in-battle enemy first-sighting cards are removed at the user's request. Enemy briefings, Codex details and loading-screen tips provide guidance. Saved hint preferences still load but no longer show anything in battle.

Old authoring notes and internal ability names were replaced with player-facing descriptions. Their source is `docs/design/player-facing-content.json`, consumed by the content generator. Unit, equipment and spell stat budgets are unchanged.

Each zone's first-sighting encounter now presents its one new regional enemy beside familiar troops. It avoids piling healing, corpse summoning and ambush mechanics into the same teaching encounter. Enemy levels stay between 1 and 20. The twenty-level economy export now includes all nineteen upgrade prices instead of stopping at level 5.

## Balance evidence

The opening and zone-boss samples across all ten zones cleared. After the close-range Archer correction, fresh first-sighting checks cleared all ten introductions:

| Stage | Unit level | Result | Duration |
| --- | --- | --- | --- |
| 4 | 1 | Clear | 60s |
| 14 | 3 | Clear | 58s |
| 24 | 5 | Clear | 58.5s |
| 34 | 7 | Clear | 59.8s |
| 44 | 9 | Clear | 61.3s |
| 54 | 11 | Clear | 61.6s |
| 64 | 13 | Clear | 65.7s |
| 74 | 15 | Clear | 65.5s |
| 84 | 17 | Clear | 61.7s |
| 94 | 19 | Clear | 82.4s |

The mature prepared squad cleared stage 95 in 189.8 seconds and stage 100 in 157.2 seconds. An early-unit squad also cleared the revised stage 94 with full wagon health. Restricted squads can still lose later specialist encounters. These are scripted observations, not proof that every composition clears every stage.

With a properly prepared spell loadout, Militia, Spearman, Swordsman, Archer and Field Medic at level 16 cleared stage 80 without relics in 181.6 seconds. Clicking five visible packs shortened that to 154.8 seconds. Both runs kept full wagon health and used the same modest wagon upgrades. These replace the earlier custom-squad observations that did not prepare a spell loadout consistently.

The benchmark can explicitly click visible packs with `--collect-packs`; this exists only in the excluded test driver and uses the actual pointer handler. Custom squads now receive stage-available prepared spells, and the driver only casts equipped spells. It does not grant invisible courage or change unit stat growth.

## Performance and packaging

Status drawing reuses its label buffer, cached text widths and mark geometry. Unit-pool acquisition skips invalid entries without discarding later reusable bodies. A 64-combatant local simulation review measured median 0.27ms and 95th-percentile 2.52ms per simulation tick. This excludes a physical-device rendering/battery claim.

Release configurations exclude test classes from the game assembly. Export filters exclude test scenes, development scripts, reports, generated previews, source artwork and key files. A completed Android resource ZIP was audited for both resource exclusions and absence of test classes. This is a resource package, not a signed AAB or store-delivered build. The exporter emitted a shutdown `EditorSettings` message after completing its archive; it is retained in the audit report. The current compressed resource archive is about 599 MiB, so final mobile size acceptance remains open.

## Verification

- Debug and Release builds: zero warnings/errors.
- Repository verification: 11,343 data checks and 86 backend tests pass; website preview builds.
- Content behavior: 724 checks pass.
- Expanded combat regressions: 1,001 checks pass.
- Production reliability/targeting checks: 39 pass.
- Manual courage input/expiry: 55 checks pass.
- Production UI: 37 checks pass across desktop/phone, including boss-rush retreat.
- Existing battle/menu review passes across desktop, small window and phone.

Evidence is under `artifacts/production-polish/`. Reproduce the focused pass with `./scripts/smoke/production_polish_smoke.sh`; full repository checks use `./scripts/verify_all.sh`. Run every game-driving review with its required isolated save suffix.

The bottom-message removal follow-up passes Debug/Release builds, the 37 desktop/phone production UI checks, the battle menu review and focused Bounties/Tower/Daily Gifts layout checks. Its logs use the `bottom-tooltips-` prefix. A broader all-menu review stalled after the Armory capture and was stopped; it is not counted as a pass.

Physical-device performance/input, signed-store installation, live account/payment services and human campaign playtesting remain external acceptance work. The public launch gates remain in [PRODUCTION_LAUNCH.md](PRODUCTION_LAUNCH.md).

## Illustrated loading screens

Battle entry and return transitions use existing zone pictures with a shaded tip area and safe margins. No new illustrations were generated. See [loading art slots](LOADING_SCREENS.md). Final loading illustrations can be added as `assets/ui/loading/{zone}.png`, or `assets/ui/loading/default.png`; they take priority over the existing mission picture. Suggested canvas: 1920 × 1080, with the main subject toward the center/right and room for live text at the top-left and bottom-left. Keep lettering out of the illustration; the game draws it separately.

Battle entry holds the loading layout briefly, then fades into play. Combat and deployment stay frozen until it clears. Menu navigation retains its quick transitions.
