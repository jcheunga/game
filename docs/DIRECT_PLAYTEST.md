# Direct gameplay review

The final revised Reliquary boss segment ended in **victory at 268.3 seconds, 51% wagon hull, and two stars**. I took over at approximately 127 seconds after an automated opening, using Lantern Guard / Ballista Crew / Mage and upgraded wagon armaments. Across the full reference sweep, targeted counter-squad checks, and this direct finish, every campaign stage has an observed clear. This is not a claim that one squad cleared the entire campaign or that all 60 stages were played manually.

## Method

I operated the actual Godot game window using deployment cards, Fireball, Heal, convoy commands, earned field orders, and pause. Each run used an isolated save. Late-game squads used level 5 units, level 3 spells, and modest wagon upgrades, without equipment, doctrines, promotions, or skill-tree investment.

Opening reviews started at the beginning of the stage. Boss-focused reviews used the combat runner to prepare the opening, then handed control over when the boss arrived. Pauses and a slower test clock allowed inspection between inputs. These are direct interaction observations, not human reaction-time or win-rate measurements. Automated preparation is distinguished from direct play below.

## Observations

| Stage | Direct interaction and outcome | Finding |
| --- | --- | --- |
| 52 — Reliquary Cataclysm | Starter squad lost at 103.8 seconds. Lantern Guard / Ballista Crew / Necromancer breached the gate around 102 seconds, but a boss fight continued beyond 307 seconds before retreat. | The gate rebuilt after destruction, artillery accumulated, and recurring healing erased progress. All three behaviors were corrected. |
| 55 — tunnel invasion | Before the fix, the direct opening ended in defeat at 45.8 seconds. In the revised opening, after the runner prepared 15 seconds, I observed the warning at 16 seconds, responded to the arrival at 18, and retained 2,206 / 2,353 hull at 34 seconds before retreating. | The invasion now gives a visible response window and recovery between ambushes. This was an opening retest, not a full direct clear. |
| 58 — Tidemaster | Took control at the boss after automated preparation. The gate stayed breached; the run ended in defeat at 195.2 seconds. A separate automated counter-squad run cleared at 113.3 seconds. | Boss pressure continued after the gate fell. Direct input timing and clock changes prevent treating this loss as a controlled difficulty measurement. |
| 60 — final boss | Took control on boss arrival around 109 seconds with Lantern Guard / Stormcaller / Battle Monk. Used the earned assault order and Fireball; victory at 122.4 seconds with full wagon hull and two stars. | Area damage, support, and the earned command provided a successful boss finish. This was a direct boss finish following an automated opening. |

## Changes from direct play

1. Destroyed gates cannot be repaired back into existence. Repair still works on intact gates.
2. Reliquary Tyrant maintains at most two living bone artillery crews across its summoning commands and graveyard resurrection. It can replace casualties. Its initial phase retains one heal; recurring Ossuary Fire no longer heals it.
3. Tunnel invasion warns at 16 seconds and arrives at 18 seconds. Further arrivals have 22–28 seconds of recovery before their next warning. Two living tunnelers suppress extra invasion spawns; authored waves retain their own units.
4. Retreating from pause releases pause before the map transition. I verified the paused retreat in the game window.
5. Resolved objective markers briefly display their outcome, then fade, so old objectives stop covering later combat. The final-boss review confirmed cleared markers disappeared.

## Automated comparison

The follow-up tactical starter-squad sweep cleared 57 of 60 stages, compared with 56 in the first pass. Stages 52, 58, and 60 remained losses for that limited profile. Stage 55 cleared in 143.8 seconds with 81.6% hull. These results predate the workspace's subsequent base-weapon changes and are historical balance observations, not a claim about the current build's win rate.

The Reliquary counter-squad benchmark also produced long fights after the fixes: one run reached the 210-second limit with 98.1% hull and a breached gate; another lost at 193.5 seconds. Engine outcomes can vary despite seeding the encounter controller. A surviving wagon at timeout does not count as a clear.

Detailed first-pass results and profile definitions are in [COMBAT_BENCHMARK.md](COMBAT_BENCHMARK.md). The broader stage, wave, and boss changes are in [COMBAT_REVIEW.md](COMBAT_REVIEW.md).

## Scope

All 60 stages received data review and automated combat coverage. Direct interaction focused on late-stage trouble spots and concrete gameplay faults; it did not cover 60 complete manual runs. Phone-size readability remains unverified.

## September 9 current-build retest

The workspace now includes wagon and stronghold weapons. A new tactical sweep on this build cleared **54/60**: five defeats and one timeout. The unchanged reference profile does not purchase the new weapon mounts or their additional skills, so this comparison combines changed combat with a profile that has not invested in those additions.

| Stage | Current reference outcome | Follow-up concern |
| --- | --- | --- |
| 25 | Defeat at 137.3s, gate breached | Mid-campaign sustained pressure; 67 enemies defeated. |
| 52 | Defeat at 85.0s, 85.6% gate remaining | Opening difficulty remains high for the starter squad. |
| 53 | Defeat at 139.9s, gate breached | Surviving defenders overwhelm the wagon. |
| 56 | Defeat at 165.2s, gate breached | Ashen Regent remains dangerous after the breach. |
| 58 | Timeout at 210s, full hull, gate breached | A surviving wagon and boss indicate a prolonged cleanup, not a win. |
| 60 | Defeat at 150.9s, gate breached | Starter squad does not finish the final boss. |

I also repeated stage 52 with Lantern Guard / Ballista Crew / Necromancer in the actual window. The runner prepared the approach; I activated the earned assault order around 139 seconds, then took over at the boss's arrival around 154 seconds with only 282 / 2,176 hull left. Fireball hit the artillery threatening the wagon, but the run ended in defeat at **161.8 seconds**, with 52 enemies defeated. The gate had never been destroyed in this attempt, so its subsequent repair was valid. This short boss attempt does not establish that the entire Reliquary fight is now well paced. The artillery cap and preservation of boss damage are verified by the regression checks; a successful direct clear is not claimed.

These six reference outcomes prompted separate equipment and counter-squad checks below. The earlier stage 60 direct victory remains evidence for that earlier build only.

Checks on September 9: build succeeded with zero warnings/errors; **74 combat checks**, **6,398 data checks**, and **71 server tests** passed after the final Reliquary changes. The extra assertion covers graveyard resurrection after the boss has already replaced an artillery casualty. Godot reported an ObjectDB cleanup warning at test-process exit; this remains separate from the passing combat assertions. Logs are under `artifacts/combat-review/`.

## Equipment and counter-squad checks

The explicit `--tactical --armaments` profile adds the new wagon weapons, volley, emergency repair, and armor at the same modest upgrade level as the existing wagon purchases. The original tactical profile remains unchanged for comparison. These are targeted automated runs, not additional direct victories.

| Stage | Squad and purchases | Outcome |
| --- | --- | --- |
| 25 | Halberdier / Shield Knight / Mage, upgraded armaments | Victory at 125.6s, full hull |
| 53 | Starter squad, upgraded armaments | Victory at 152.9s, 64.4% hull |
| 56 | Starter squad, upgraded armaments | Victory at 163.1s, 53.2% hull |
| 58 | Lantern Guard / Ballista Crew / Stormcaller, upgraded armaments | Victory at 107.8s, full hull |
| 60 | Lantern Guard / Stormcaller / Battle Monk, upgraded armaments | Victory at 126.2s, full hull |

Stage 52 remained an outlier. The recommended Lantern Guard / Ballista Crew / Mage squad with armaments lost at 180.8 seconds before the final tuning. A direct boss segment with Battle Monk support reached 218 seconds before I retreated: a protected ranged line could be rebuilt, but attacks and replacements left little time to turn that recovery into boss damage.

The final stage-52 change replaces the second wave lich in **Sepulcher break** with a spitter, retaining the earlier lich encounter. Reliquary Tyrant now attacks every **1.8 seconds** instead of 1.42 and summons every **15 seconds** instead of 11.5. Its health and damage per hit are unchanged. This reduces overlapping reinforcement pressure and gives each rebuilt frontline more time to act. The graveyard path also respects the two-artillery limit, closing a case where a replacement arrived before the dead crew's resurrection.

A post-change recommended-squad benchmark reached 210 seconds with a breached gate, 24.5% hull, and the boss at 281 / 911 health. That is a timeout, not a clear; it demonstrates a longer recovery opportunity but cannot alone establish a satisfying finish.

## Final direct Reliquary finish

The revised direct attempt used an automated opening until boss arrival at approximately 127 seconds, with full wagon hull and an already breached gate. I then used the earned assault order, upper-lane deployments, Fireball, and Heal. Individual reinforcements were often overwhelmed before their support arrived; rebuilding a group and fighting within wagon weapon range ultimately finished the commander. The result was **268.3 seconds, about 51% hull, 77 enemies defeated, 22 deployments, and two stars**.

The gate stayed destroyed for the entire boss segment. Artillery casualties could be replaced without an accumulating battery, and the initial phase heal did not become repeated regeneration. The wagon survived a failed forward push and a successful defensive recovery. This was a long fight, and direct-control pauses/tool latency prevent using its duration as a human clear-time target. It establishes a playable recovery and finish; it does not establish universal difficulty or win rates.

The complete tactical reference sweep was repeated after the final tuning: **54 victories, five defeats, and one timeout**, with no reported simulation errors. Separate purchased-armament and counter-squad checks supply clears for five of those six trouble spots; the direct Reliquary finish supplies the remaining one. Logs: `tuned-tactical-final.log`, `armament-hotspots.log`, `current-counter25.log`, `current-counter58.log`, `current-counter60.log`, and `isolated-52-paced-retry.log` under `artifacts/combat-review/`.

## September 10 follow-up

Two issues from the long boss segments received a further focused change. Late field orders now keep their immediate combat benefits without opening another branch objective once a boss is present or the gate is breached. Tidemaster now replaces plague-engine casualties instead of continually adding engines while at least two are alive. The earlier Reliquary artillery rule is shared by both siege commanders.

Targeted stage-58 results after this change: the tactical starter profile still timed out at 210 seconds, with 89.8% hull and the boss plus two engines remaining; its peak enemy count was five. The Lantern Guard / Ballista Crew / Stormcaller profile with purchased armaments cleared in 107.8 seconds with full hull. This verifies bounded siege accumulation while preserving the effective counter-squad clear; it does not show a starter-squad victory or establish a human win rate. These targeted runs do not replace the historical full-campaign sweep.

Validation: **84 combat checks**, **6,398 data checks**, and **71 server tests** pass; the build has zero warnings/errors. The known Godot ObjectDB cleanup warning remains at test-process exit. Evidence is in `finish-pacing-regressions.log`, `finish-pacing-verification.log`, `finish58-reference.log`, and `finish58-counter.log` under `artifacts/combat-review/`.

The native input retest also completed successfully. The runner prepared stage 58 until Tidemaster arrived at approximately 94 seconds with a breached gate and full hull. I used the earned assault order, Fireball, and Heal in the actual game window. Its follow-up reinforcements arrived without a new branch-objective marker, and the visible boss phase still gave its warning. The completed run recorded **victory at 105.0 seconds, full wagon hull, 36 enemies defeated, and 11 total spells** in `finish58-input.log`. The opening and earlier spells were automated; this was a direct boss finish, not a complete manual stage run. The result window was no longer open when the completed log was retrieved.
