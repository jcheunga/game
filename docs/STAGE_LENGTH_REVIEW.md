# Campaign review after doubling battlefield length

This is the historical review of the map-length change before the campaign redesign. See [Expanded campaign](EXPANDED_CAMPAIGN.md) for the implemented mechanics and current all-stage validation.

Reviewed 2026-09-28. **The longer map changes stage balance; the existing campaign has not been individually redesigned for it.**

## What changed

Every stage uses the same combat layout: world width grew from 1280 to 2560, and the enemy spawn/gate moved 1280 units right. The base gap is now 2368 instead of 1088, and the playable horizontal span is 2392 instead of 1112. Units retain their movement speeds, attack ranges, costs, health, and damage. Waves, star targets, mission start times, hold requirements, hazard intervals/radii, and summon cooldowns retain their authored values. Map-menu node positions and progression requirements did not change.

Victory still requires an intact wagon, a breached gate, and every scripted wave/pending spawn/living defender cleared. Star time targets are optional scoring conditions; they are not automatic defeat timers. Mission TargetSeconds measures accumulated objective progress, not a deadline to reach the site.

## Coverage and limitations

- Inspected all **60 stages, 330 waves, 56 resolved side missions, and 108 hazards**, including generated campaign side missions and runtime star-objective overrides.
- Ran every stage at both widths using the existing tactical benchmark: **120 engine battles**. Repeated 19 flagged stages at both widths with two additional battle RNG seeds: **76 more battles**. Compared specialist squads on stages 58 and 60 at both widths: **4 additional battles**, for **200 total**.
- The reference squad is Swordsman, Archer, and Shield Knight, at the harness's stage-scaled upgrade levels, with its standard tactical spells/base upgrades. The two specialist checks additionally use the armaments profile and stage-unlocked counter squads. Each run uses an isolated save; the player's save is untouched.
- This is a consistent automated strategy, not optimal play or proof a stage is impossible. The bot does not deliberately secure mission lanes, dodge every hazard, or select the late adaptive-wave directive. Its star misses on those objectives must not be interpreted as engine failures. Matching battle seeds reduce variation; they do not establish exhaustive balance coverage.
- **300 seconds is the audit cutoff, not a gameplay timeout.** All 200 runs finished without a combat-check failure; losses and cutoffs are recorded as balance outcomes. The independent combat regression suite passed 122 checks, and the layout audit passed 180 checks. Headless runs report the previously known ObjectDB exit warning.

## Findings

The initial full sweep won **52/60** at the old width and **53/60** at the new width. Among stages won at both widths, median clear time increased **23.2s**. Longer travel sometimes gives the player more buildup time, so difficulty does not increase uniformly.

1. **Retune stage 8's speed objective first.** Its unchanged 105s target was met in 3/3 short-map samples and missed in 3/3 long-map samples (105.6–182.2s). Stage 25's 150s target also fell from 3/3 to 1/3 successes (148.8–233.7s on the long field). Tune individual targets alongside encounter pacing; doubling every timer would hide the larger outliers.
2. **Review summoner and boss cleanup before declaring the campaign balanced.** Stage 12 falls from 3/3 to 1/3 wins. Stage 26 falls from 3/3 to 2/3. Stages 52 and 53 fall from 1/3 to 0/3. Existing limits on simultaneous summons still apply, but the longer approach provides more time for summons, jamming, and attrition before reinforcements arrive.
3. **Cursed ground expands much more than a simple camera change.** Its damaging strip grows from 772 to 2052 units (2.66×), with unchanged damage per second. This affects stages 29, 43, 49, 52, 53, 55, and 59. Safe upper/lower lanes still exist. Consider authored curse patches or a capped strip length rather than scaling the damage zone across almost the whole map.
4. **Review mission placement and activation together.** Stage 50's Purge Seal succeeds in 2/3 short runs and 0/3 long runs. Stage 44's Breach Crew moves from x916 to x1914 with its 32s activation unchanged; completion falls 3/3 → 2/3. Stage 19's shrine moves from x719 to x1461, but repeat success is 1/3 at both lengths, so that initial miss alone is not a confirmed regression.
5. **Late encounters require focused manual/specialist tuning.** Stage 58 reaches the audit cap in every long reference run. The specialist comparisons below also regress at the larger width, strengthening the case beyond the basic squad's known limitations.

### Specialist comparisons (one matched seed each)

| Stage | Squad | Old map | Long map |
|---|---|---|---|
| 58 · Leviathan Wake | lantern_guard, ballista, stormcaller | Win 125.1s · 3★ | Cap 300.0s · 0★ |
| 60 · Pale Crown | lantern_guard, stormcaller, coordinator | Win 162.2s · 2★ | Loss 213.6s · 0★ |

## Every stage

The two result columns show the same initial seed. Repeat wins include all three tested seeds where available. ‘No specific regression observed’ means no isolated issue in this limited strategy; it does not certify optimal difficulty or three-star attainability.

| # | Stage | Old map | Long map | Repeat wins, old → long | Review |
|---:|---|---|---|---|---|
| 1 | Far Gate | Win 36.5s · 3★ | Win 49.3s · 3★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 2 | Stone Causeway | Win 39.0s · 3★ | Win 56.5s · 3★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 3 | Market Ward | Win 63.8s · 3★ | Win 77.4s · 3★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 4 | Bell Tower Gate | Win 61.4s · 3★ | Win 80.4s · 3★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 5 | Mooring Ring | Win 65.3s · 2★ | Win 79.8s · 2★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 6 | Drowned Quay | Win 64.7s · 3★ | Win 74.9s · 3★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 7 | Chainlift Yard | Win 81.1s · 2★ | Win 155.1s · 2★ | 3/3 → 3/3 | Pacing: all wins, but clears rise from 81–89s to 127–155s. Review convoy travel and held waves. |
| 8 | Wreck Admiral | Win 83.9s · 3★ | Win 182.2s · 2★ | 3/3 → 3/3 | Priority: 105s target missed in all three long runs; all three short runs earned it. |
| 9 | Forge Siding | Win 71.6s · 3★ | Win 95.8s · 3★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 10 | Smelter Row | Win 87.0s · 3★ | Win 116.2s · 3★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 11 | Cinder Causeway | Win 88.7s · 3★ | Win 107.4s · 3★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 12 | Furnace Crown | Win 104.2s · 2★ | Win 181.5s · 2★ | 3/3 → 1/3 | Priority: reference squad wins fall from 3/3 to 1/3; investigate Forge escorts and reinforcement travel. |
| 13 | Outer Ward | Win 80.0s · 2★ | Win 145.3s · 1★ | 3/3 → 3/3 | Monitor jam/hazard exposure; initial star loss did not repeat consistently. |
| 14 | Purge Cloister | Win 72.8s · 3★ | Win 94.9s · 3★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 15 | Leechcourt | Loss 131.2s · 0★ | Win 170.7s · 1★ | One seed | Initial loss becomes a win; the larger field can also favor player buildup. Still misses deploy/jam stars. |
| 16 | Black Vault Seal | Win 92.0s · 2★ | Win 204.9s · 2★ | 3/3 → 3/3 | Pacing: clears increase to 140–205s; no side mission secured in the long repeats. |
| 17 | Narrow Ascent | Win 62.4s · 2★ | Win 70.5s · 2★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 18 | Rime Switchback | Win 69.3s · 3★ | Win 78.1s · 3★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 19 | Avalanche Shrine | Win 80.9s · 3★ | Win 94.8s · 2★ | 3/3 → 3/3 | Shrine completion varies at both lengths (1/3 each); shifted position needs lane-directed playtesting. |
| 20 | High Watch | Win 85.8s · 2★ | Win 102.5s · 2★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 21 | Thornwall Gate | Win 111.3s · 2★ | Win 122.3s · 2★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 22 | Outer Nave | Win 75.0s · 3★ | Win 102.4s · 3★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 23 | Ossuary Court | Win 83.8s · 2★ | Win 114.5s · 3★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 24 | Choir Ruin | Win 72.3s · 3★ | Win 102.3s · 3★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 25 | Reliquary Steps | Win 107.5s · 3★ | Win 148.8s · 3★ | 3/3 → 3/3 | Priority: 150s target missed in 2/3 long runs, versus 0/3 short runs; graveyard pressure lasts longer. |
| 26 | Sepulcher Crown | Win 99.5s · 2★ | Loss 165.7s · 0★ | 3/3 → 2/3 | Reliability: wins drop 3/3 → 2/3; longer Grave Toll encounter. |
| 27 | Bog Causeway | Win 77.0s · 3★ | Win 108.9s · 3★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 28 | Drowned Chapel | Win 74.6s · 3★ | Win 105.0s · 3★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 29 | Plague Ferry | Win 109.3s · 2★ | Win 169.1s · 2★ | One seed | Pacing/attrition: clear grows 109s → 169s; this stage also has the expanded cursed strip. |
| 30 | Saints' Mire | Win 79.4s · 3★ | Win 102.8s · 3★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 31 | Mire Bell | Win 101.3s · 2★ | Win 119.8s · 2★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 32 | Burned Waystation | Win 57.0s · 2★ | Win 70.9s · 2★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 33 | Ash Grass | Win 61.6s · 2★ | Win 74.4s · 3★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 34 | Siege Ring | Win 68.8s · 2★ | Win 95.3s · 2★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 35 | Sunfall Redoubt | Win 68.9s · 3★ | Win 92.0s · 3★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 36 | Sunfall Warcamp | Win 77.2s · 2★ | Win 99.8s · 2★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 37 | Thorn Verge | Win 67.8s · 2★ | Win 81.7s · 2★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 38 | Witch Circle | Win 65.8s · 2★ | Win 86.0s · 2★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 39 | Blackbark Road | Win 67.9s · 1★ | Win 95.2s · 1★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 40 | Snare Grove | Win 73.4s · 1★ | Win 97.8s · 1★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 41 | Gloamwood Heart | Win 91.3s · 2★ | Win 107.0s · 2★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 42 | Bridge Bastion | Win 67.8s · 2★ | Win 83.6s · 2★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 43 | Breach Yard | Win 78.3s · 2★ | Win 113.3s · 1★ | 3/3 → 3/3 | Side mission completion falls 3/3 → 1/3; check marked lane access and hazard overlap. |
| 44 | Crownward Gate | Win 63.0s · 2★ | Win 131.8s · 2★ | 3/3 → 3/3 | Breach crew moves from x916 to x1914; success falls 3/3 → 2/3. Retest approach and hold position. |
| 45 | Inner Ring | Win 79.2s · 1★ | Win 92.8s · 2★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 46 | Crownfall Keep | Win 90.5s · 2★ | Win 114.7s · 2★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 47 | Lantern Requiem | Win 67.3s · 2★ | Win 98.0s · 2★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 48 | Deadwake Armada | Win 82.3s · 2★ | Win 101.2s · 2★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 49 | Forge Crown | Loss 141.0s · 0★ | Win 203.0s · 1★ | One seed | Initial reference loss becomes a win, despite longer cursed ground. Do not assume all maps become harder. |
| 50 | Ashen Remnant | Win 103.2s · 2★ | Win 122.5s · 1★ | 3/3 → 3/3 | Priority: Purge Seal completion falls 2/3 → 0/3; original 36s activation and hold rule need review. |
| 51 | Whiteout Bastion | Win 87.9s · 1★ | Win 176.9s · 1★ | 3/3 → 3/3 | Pacing: 80–88s becomes 115–177s. Review siege reinforcement travel; mission remains attainable. |
| 52 | Reliquary Cataclysm | Loss 189.3s · 0★ | Loss 113.9s · 0★ | 1/3 → 0/3 | Priority: 1/3 → 0/3 reference wins; cursed ground spans 2.66× as far. Baseline already difficult. |
| 53 | Blackwater Procession | Loss 144.9s · 0★ | Loss 220.9s · 0★ | 1/3 → 0/3 | Priority: 1/3 → 0/3 reference wins. Check curse attrition and late pressure; baseline already difficult. |
| 54 | Warhorn Expanse | Win 146.1s · 1★ | Win 171.2s · 1★ | One seed | No specific regression observed; retain for a general pacing playtest. |
| 55 | Moonless Snare | Win 138.4s · 1★ | Loss 220.6s · 0★ | 3/3 → 2/3 | Priority: 3/3 → 2/3 wins, mission 2/3 → 0/3; expanded curse strip plus tunnel pressure. |
| 56 | Ashen Throne | Loss 178.2s · 0★ | Loss 238.1s · 0★ | 0/3 → 0/3 | No reference wins at either length; longer defeats alone do not prove a new blocker. Needs a stronger squad review. |
| 57 | Bellgrave Siege | Win 101.9s · 1★ | Win 174.4s · 1★ | 3/3 → 3/3 | Pacing: 96–103s becomes 111–181s; escort remains attainable in every sample. |
| 58 | Leviathan Wake | Cap 300.0s · 0★ | Cap 300.0s · 0★ | 0/3 → 0/3 | Priority: all three long reference runs reach the 300s audit cap. Specialist siege squad also times out. |
| 59 | Molten Ascension | Loss 199.2s · 0★ | Win 238.9s · 1★ | One seed | Initial reference loss becomes a long-map win (239s); expanded curse exposure still warrants specialist review. |
| 60 | Pale Crown | Loss 139.1s · 0★ | Loss 228.9s · 0★ | 0/3 → 0/3 | Priority: specialist support squad changes from a win to a loss; normal reference fails at both lengths. |

## Recommended implementation order

1. Separate hazard coverage from arena width, especially cursed ground; keep clearly usable safe lanes.
2. Tune the Forge and late summoner encounters around reinforcement travel and post-breach cleanup. Avoid raising enemy caps globally, which could amplify the same problem.
3. Reposition key mission sites and align activation with when their lane can be contested; preserve existing hold durations unless hands-on tests show they are inappropriate.
4. Recalibrate stage 8 and 25 speed targets after those encounter adjustments, then retest all timed/deployment-limit objectives.
5. Replay the full 60-stage sweep and the repeat/specialist cases, then manually verify objective-directed play, adaptive branches, mobile scrolling, and travel downtime. No stage-balance values were changed during this review.

## Reproduction and evidence

Build with `dotnet build Game.csproj`. Run `CombatReviewSmoke.tscn` with `--headless --fixed-fps 60`, an isolated `--save-suffix=combat-review-<unique-name>`, and `--tactical --time-limit=300`. Omit `--stages` for all 60. Add `--reference-map` for the original width; this changes only that test process's in-memory tuning. Use `--seed-offset=1000` or `2000` for repeats. `--stage-layout` exports resolved layouts, and `--regressions` runs the independent rule checks.

Raw logs and the complete per-stage objective/layout/result JSON are under `artifacts/stage-length-review/`. Regenerate this report with `python3 scripts/analysis/stage_length_review.py`. The JSON records SHA-256 hashes for the logs and reviewed game sources.
