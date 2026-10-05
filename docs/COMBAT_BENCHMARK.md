# Combat benchmark results — first balance pass

These are fixed-seed automated observations, not human win-rate estimates. Both profiles use the starter squad; tactical adds ordinary upgrades and spell decisions, but no equipment, doctrines, promotions or skill-tree investment. A loss does not constitute a regression-test failure. Each stage begins with an isolated reset profile.

First-pass verification: 27 combat regression checks passed; 120 campaign samples completed without simulation errors. Game build: zero warnings/errors. Data validation: 6,398 passed. Server tests: 71 passed. Real-input stage victory, saved stars and return to map passed.

| Stage | Encounter | Basic result / seconds | Tactical result / seconds |
| --- | --- | --- | --- |
| 1 | Far Gate | Win / 36.4 | Win / 36.3 |
| 2 | Stone Causeway | Win / 41.4 | Win / 36.7 |
| 3 | Market Ward | Win / 53.0 | Win / 56.6 |
| 4 | Bell Tower Gate | Win / 62.0 | Win / 55.4 |
| 5 | Mooring Ring | Win / 61.0 | Win / 55.7 |
| 6 | Drowned Quay | Win / 69.6 | Win / 64.8 |
| 7 | Chainlift Yard | Win / 82.0 | Win / 83.3 |
| 8 | Wreck Admiral | Loss / 121.0 | Win / 90.5 |
| 9 | Forge Siding | Win / 65.3 | Win / 66.6 |
| 10 | Smelter Row | Win / 79.5 | Win / 73.0 |
| 11 | Cinder Causeway | Win / 89.3 | Win / 81.5 |
| 12 | Furnace Crown | Loss / 122.5 | Win / 96.2 |
| 13 | Outer Ward | Win / 82.8 | Win / 80.0 |
| 14 | Purge Cloister | Win / 80.5 | Win / 75.1 |
| 15 | Leechcourt | Loss / 102.2 | Win / 109.8 |
| 16 | Black Vault Seal | Win / 123.0 | Win / 74.2 |
| 17 | Narrow Ascent | Win / 63.3 | Win / 52.2 |
| 18 | Rime Switchback | Win / 70.1 | Win / 55.3 |
| 19 | Avalanche Shrine | Win / 87.8 | Win / 74.1 |
| 20 | High Watch | Win / 97.7 | Win / 78.0 |
| 21 | Thornwall Gate | Win / 114.4 | Win / 84.2 |
| 22 | Outer Nave | Win / 84.9 | Win / 64.4 |
| 23 | Ossuary Court | Win / 90.6 | Win / 69.7 |
| 24 | Choir Ruin | Win / 93.0 | Win / 68.4 |
| 25 | Reliquary Steps | Win / 95.9 | Win / 96.4 |
| 26 | Sepulcher Crown | Win / 84.0 | Win / 81.3 |
| 27 | Bog Causeway | Win / 91.9 | Win / 72.8 |
| 28 | Drowned Chapel | Win / 89.1 | Win / 64.3 |
| 29 | Plague Ferry | Loss / 75.9 | Win / 90.2 |
| 30 | Saints' Mire | Win / 81.2 | Win / 70.3 |
| 31 | Mire Bell | Loss / 120.0 | Win / 82.3 |
| 32 | Burned Waystation | Win / 56.2 | Win / 53.2 |
| 33 | Ash Grass | Win / 57.2 | Win / 58.7 |
| 34 | Siege Ring | Win / 65.2 | Win / 64.9 |
| 35 | Sunfall Redoubt | Win / 66.2 | Win / 62.6 |
| 36 | Sunfall Warcamp | Win / 75.1 | Win / 70.6 |
| 37 | Thorn Verge | Win / 60.0 | Win / 60.6 |
| 38 | Witch Circle | Win / 57.5 | Win / 66.7 |
| 39 | Blackbark Road | Win / 66.1 | Win / 59.8 |
| 40 | Snare Grove | Win / 77.1 | Win / 70.2 |
| 41 | Gloamwood Heart | Win / 81.2 | Win / 78.3 |
| 42 | Bridge Bastion | Win / 73.2 | Win / 70.0 |
| 43 | Breach Yard | Win / 65.2 | Win / 73.0 |
| 44 | Crownward Gate | Win / 58.1 | Win / 53.6 |
| 45 | Inner Ring | Win / 75.0 | Win / 60.7 |
| 46 | Crownfall Keep | Win / 107.4 | Win / 82.0 |
| 47 | Lantern Requiem | Win / 71.0 | Win / 65.2 |
| 48 | Deadwake Armada | Win / 78.9 | Win / 64.0 |
| 49 | Forge Crown | Loss / 101.5 | Win / 111.0 |
| 50 | Ashen Remnant | Win / 85.1 | Win / 89.3 |
| 51 | Whiteout Bastion | Win / 82.3 | Win / 76.7 |
| 52 | Reliquary Cataclysm | Loss / 81.5 | Loss / 100.6 |
| 53 | Blackwater Procession | Loss / 119.0 | Win / 167.1 |
| 54 | Warhorn Expanse | Win / 91.5 | Win / 127.7 |
| 55 | Moonless Snare | Loss / 133.3 | Loss / 155.3 |
| 56 | Ashen Throne | Loss / 155.7 | Win / 128.2 |
| 57 | Bellgrave Siege | Win / 90.5 | Win / 93.7 |
| 58 | Leviathan Wake | Loss / 175.0 | Loss / 177.8 |
| 59 | Molten Ascension | Win / 176.7 | Win / 182.3 |
| 60 | Pale Crown | Loss / 185.5 | Loss / 165.7 |

## Follow-up after direct input review

A fresh 60-stage tactical run after the input-review fixes cleared **57/60**, with losses at 52 (100.6s), 58 (177.3s), and 60 (165.7s). Stage 55 now clears in 143.8s with 81.6% hull. The table above preserves the earlier baseline; it does not represent the later ambush and boss fixes.

That follow-up combat regression suite had **36 passing checks**. Native input observations, counter-squad checks, changes, and test limits are recorded in [DIRECT_PLAYTEST.md](DIRECT_PLAYTEST.md).

## September 9 build with base weapons

A later sweep on the workspace with wagon and stronghold weapons cleared **54/60** with the same reference profile: defeats at 25, 52, 53, 56, and 60, and a 210-second timeout at 58. Repeating all 60 stages after the final Reliquary tuning produced the same 54 clears. The profile does not purchase the new weapon mounts or skills. These results supersede 57/60 as the latest build observation; the earlier tables remain historical comparisons. The expanded regression suite passed **74 checks**.

Separate `--tactical --armaments` runs cleared 53 and 56 with the starter squad. Counter-squad runs with those purchases cleared 25, 58, and 60. Stage 52 remained a timeout in the automated recommended-squad run, then cleared in a direct boss segment following an automated opening: 268.3 seconds, about 51% hull, two stars. See [DIRECT_PLAYTEST.md](DIRECT_PLAYTEST.md) for exact outcomes, direct input observations, and test limits. These targeted checks are not another complete campaign sweep and do not replace the reference-profile results.

## September 10 targeted finish-pacing checks

After limiting Tidemaster's siege replacements and preventing new branch objectives from late field orders, the stage-58 starter profile still timed out at 210 seconds (89.8% hull, two engines remaining). The purchased-armament counter squad cleared at 107.8 seconds with full hull. A direct boss segment following an automated opening cleared at 105.0 seconds with full hull, after issuing the earned order and casting Fireball and Heal. The expanded regression suite passed **84 checks**; build, data validation, and server tests also passed. No new full-campaign win-rate claim is made from these targeted checks.

## October 5: 100-stage campaign on the two-screen field

The campaign now has ten zones of ten stages, with each zone's boss in slot 10. The data is generated by
`scripts/analysis/expand_campaign.py` from the 60-stage source in `scripts/analysis/legacy/`. The reference
player was rebuilt to resemble a real one:

- It owns every unit and spell unlocked by the stage. Its deck is the three strongest frontline and three
  strongest ranged troops, ranked by √(health × damage per second).
- It levels its troops on the design curve (level 1 until stage 9, then 2, 3, 4 and 5).
- It casts heal only with courage to spare, and fireball on clusters of four (three when flush).
- When no enemy is advancing, it banks courage and pushes in groups of three.
- It fills an empty front line with the least-represented strong melee type.
- Sweeps cap battles at 300 s, since the field takes about 30 s to cross.

Test-only switches for experiments, which change values in memory only:

- `--combat=Key=Value` overrides combat tuning.
- `--stage-mult=Field=x` and `--stage-shift=Field=x` scale or offset a stage field on every stage.
- `--no-hazards` and `--no-modifiers` strip those from every stage.
- `--trace` prints the field every five seconds.

Findings and changes, in the order they were made:

- **Fog let enemies walk past troops.** Weather scaled vertical aggro too; fog (0.72) dropped the shortest
  vertical reach below the band's half height. Weather now shortens only horizontal aggro. This fix alone took
  the sweep from 67 to 89 wins.
- **Postgame content moved into early zones.** Enemies and modifiers now appear only from the stage where the
  regular campaign introduces them; earlier stages field a role-alike instead. Postgame bursts in zones 1–4 are
  spaced to at least 0.45 s. Hazard damage follows each stage's new damage curve. New stages inherit modifiers
  from the stage before them, not after.
- **Bosses.** Each summoner now calls at most three reinforcements (bosses used to get five after stage 33).
  Boss stages have 0.8× escorts and 0.9× enemy health. A per-stage table (`STAGE_TUNE`) evens out individual
  bosses from three-seed sweeps.
- **Late zones were too comfortable.** Mire gained 8% enemy health and 10% damage; Gloamwood gained 10% and
  12% (`ZONE_SHAPE`). Afterwards both still clear 10/10, at 90% and 84% average hull, and the Gloamwood Witch
  falls with 18% hull left. Basilica stays neutral. Its lich-raising stages (57–59) are knife-edge: even +3%
  health or +6% damage turned them into 300-second stalemates with the gate untouched.

Reference sweep (seed 0, 300 s cap), before the late-zone hardening:

| Zone | Stages | Wins | Average wagon hull on a win |
| --- | --- | --- | --- |
| King's Road | 1–10 | 10/10 | 100% |
| Saltwake Docks | 11–20 | 10/10 | 88% |
| Emberforge March | 21–30 | 9/10 | 68% |
| Ashen Ward | 31–40 | 9/10 | 76% |
| Thornwall Pass | 41–50 | 9/10 | 91% |
| Hollow Basilica | 51–60 | 10/10 | 94% |
| Mire of Saints | 61–70 | 10/10 | 95% |
| Sunfall Steppe | 71–80 | 9/10 | 89% |
| Gloamwood Verge | 81–90 | 10/10 | 99% |
| Crownfall Citadel | 91–100 | 10/10 | 82% |

The 96 wins include stage 30 at 3% hull and stage 100 at 11%. Across three seeds the 15 boss stages won 35 of
45 runs. Every boss has at least one win, and the Plague Archon (35) and Bone Pontiff (55) win one run in three.
