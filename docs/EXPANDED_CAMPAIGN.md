# Expanded campaign battlefield

Implemented for all 60 authored campaign stages on the 2560-unit map. The previous length-only audit is preserved in [Stage length review](STAGE_LENGTH_REVIEW.md).

## What changed

- Every stage has Approach, Crossroads, and Gate encounters. Packs enter at authored points along the map. Advancing can trigger them early, with at least 2.5 seconds of warning and 5 seconds between pack starts. Existing enemy caps and staggered spawns remain; time fallback prevents waiting forever when the player holds back.
- Every stage has a themed forward post. Hold it uncontested for 2.5–3.5 seconds to earn 3 deployments in stages 1–20 or 4 thereafter. Each still costs normal courage and card recovery, needs 8 seconds of post recovery, and stays near the captured lane. Nearby enemies block it; the player can switch back to the wagon to save charges. Blocked, cooling, and spent posts fall back to the wagon, including the placement preview.
- Every stage has a one-time optional supply cache, held for 2.5 seconds. City, Thornwall, and Steppe caches grant 25 courage and 3 seconds of card recovery. Quarantine, Basilica, and Mire caches repair 12% of wagon hull and grant 8 courage. Harbor, Foundry, Gloamwood, and Citadel caches damage the gate by 12% and suppress enemy specials that summon or jam for 18 seconds.
- Allies near a capture ring stay long enough to secure it while nearby combat takes priority. This also helps existing hold missions. Existing missions open earlier and move into reach; their hold duration and victory requirements remain intact.
- The seven cursed stages (29, 43, 49, 52, 53, 55, 59) now use two separated 240/280-unit pockets instead of the 2052-unit strip. Ordinary hazards keep warning windows and are moved off forward-post approaches where needed.
- Summoners and jammers wait until combat comes within 550 units or they approach the wagon. Repeated summons have finite reserves: 3 per ordinary summoner, 3 per early boss, 5 per boss after stage 20. Boss phase attacks remain. Breaching the gate stops periodic summon/jam pressure; every remaining wave and enemy still has to be defeated.
- A clickable/touchable minimap shows allies, enemies, bosses, the post, supplies, the visible camera area, incoming packs, and tunnel warnings. Side buttons find offscreen enemies. The Field briefing and battle intel explain the stage rewards.
- Time-star targets remain at their prior values except stage 8 (105→165 seconds) and stage 25 (150→190 seconds). Stars still reward execution and loadout choices; the targets are not battle time limits.
- The new campaign mechanics are enabled only for campaign play. Shared scrolling still works in the other modes; challenge and endless spawn rules retain their existing behavior.

## Validation

- 80 final engine simulations: all 60 stages with the starter squad, 11 difficult-stage repeats with a second seed, and 9 specialist runs. Every stage has at least one verified winning run.
- Starter squad on seed 0: 55/60 wins; median successful clear 110.8s; median first contact 6.5s. All 60 posts and 59 supply caches captured. Specialist squads cleared the five starter-squad losses.
- These are deterministic bots in the real Godot engine, not human playtests. The starter squad uses Brawler, Shooter, and Defender at the existing review progression levels, with tactical healing/fireball. The field-aware bot chooses objective lanes and avoids forward deployment when enemies are threatening its rear. Specialist runs also equip the review armaments and use only stage-unlocked units. No gear, doctrines, or purchases are assumed.
- Repeated outcomes still vary: stage 58 reaches the 300-second test cutoff with the starter squad on seed 1000, while its specialist run clears. Some late bosses take over four minutes. These remain human-playtest balance targets, not evidence of a universal easy clear or a guarantee of three stars.
- Build: no warnings or errors. Field/objective and 60-stage layout checks: 389 pass. Existing combat regressions: 122 pass. Camera/input: 31 pass, including desktop and touch minimap gestures. Mobile presentation: 144 pass. Data validation: 7,492 pass. Saves were isolated with test-only suffixes.

## Stage-by-stage results

Core is the final seed-0 starter squad run. Specialist is the tested alternative where available, not a required loadout. “Supplies” means the optional reward was collected in the core run. Percentages locate objectives along the playable field.

| Stage | Name | Post X / deployments | Supply reward | Core | Supplies | Specialist |
|---|---|---|---|---|---|---|
| 1 | Far Gate | 36% / 3 | courage | Win 56.8s · 3★ | Yes | — |
| 2 | Stone Causeway | 38% / 3 | courage | Win 53.5s · 3★ | Yes | — |
| 3 | Market Ward | 41% / 3 | courage | Win 69.0s · 3★ | Yes | — |
| 4 | Bell Tower Gate | 44% / 3 | courage | Win 74.4s · 3★ | Yes | — |
| 5 | Mooring Ring | 36% / 3 | siege | Win 73.3s · 2★ | Yes | — |
| 6 | Drowned Quay | 38% / 3 | siege | Win 90.5s · 3★ | Yes | — |
| 7 | Chainlift Yard | 41% / 3 | siege | Win 122.6s · 2★ | Yes | — |
| 8 | Wreck Admiral | 44% / 3 | siege | Win 166.1s · 2★ | Yes | — |
| 9 | Forge Siding | 36% / 3 | siege | Win 78.9s · 3★ | Yes | — |
| 10 | Smelter Row | 38% / 3 | siege | Win 98.7s · 3★ | Yes | — |
| 11 | Cinder Causeway | 41% / 3 | siege | Win 121.6s · 3★ | Yes | — |
| 12 | Furnace Crown | 44% / 3 | siege | Win 145.6s · 3★ | Yes | — |
| 13 | Outer Ward | 36% / 3 | repair | Win 97.5s · 2★ | Yes | — |
| 14 | Purge Cloister | 38% / 3 | repair | Win 88.1s · 2★ | Yes | — |
| 15 | Leechcourt | 41% / 3 | repair | Win 184.0s · 1★ | Yes | — |
| 16 | Black Vault Seal | 44% / 3 | repair | Win 204.4s · 3★ | Yes | — |
| 17 | Narrow Ascent | 36% / 3 | courage | Win 74.4s · 2★ | Yes | — |
| 18 | Rime Switchback | 38% / 3 | courage | Win 74.4s · 3★ | Yes | — |
| 19 | Avalanche Shrine | 41% / 3 | courage | Win 96.7s · 3★ | Yes | — |
| 20 | High Watch | 44% / 3 | courage | Win 117.2s · 2★ | Yes | — |
| 21 | Thornwall Gate | 36% / 4 | courage | Win 147.3s · 2★ | Yes | — |
| 22 | Outer Nave | 38% / 4 | repair | Win 90.5s · 3★ | Yes | — |
| 23 | Ossuary Court | 41% / 4 | repair | Win 96.9s · 3★ | Yes | — |
| 24 | Choir Ruin | 44% / 4 | repair | Win 93.7s · 3★ | Yes | — |
| 25 | Reliquary Steps | 36% / 4 | repair | Loss 171.9s · 0★ | Yes | Win 161.9s |
| 26 | Sepulcher Crown | 38% / 4 | repair | Loss 152.5s · 0★ | Yes | Win 154.6s |
| 27 | Bog Causeway | 41% / 4 | repair | Win 152.9s · 2★ | Yes | — |
| 28 | Drowned Chapel | 44% / 4 | repair | Win 130.0s · 3★ | Yes | — |
| 29 | Plague Ferry | 36% / 4 | repair | Win 136.9s · 2★ | Yes | — |
| 30 | Saints' Mire | 38% / 4 | repair | Win 141.5s · 3★ | Yes | — |
| 31 | Mire Bell | 41% / 4 | repair | Win 163.7s · 2★ | Yes | — |
| 32 | Burned Waystation | 44% / 4 | courage | Win 62.3s · 2★ | Yes | — |
| 33 | Ash Grass | 36% / 4 | courage | Win 66.4s · 3★ | Yes | — |
| 34 | Siege Ring | 38% / 4 | courage | Win 81.2s · 2★ | Yes | — |
| 35 | Sunfall Redoubt | 41% / 4 | courage | Win 81.4s · 3★ | Yes | — |
| 36 | Sunfall Warcamp | 44% / 4 | courage | Win 93.5s · 2★ | Yes | — |
| 37 | Thorn Verge | 36% / 4 | siege | Win 73.9s · 2★ | Yes | — |
| 38 | Witch Circle | 38% / 4 | siege | Win 72.4s · 2★ | Yes | — |
| 39 | Blackbark Road | 41% / 4 | siege | Win 88.7s · 1★ | Yes | — |
| 40 | Snare Grove | 44% / 4 | siege | Win 91.0s · 2★ | Yes | — |
| 41 | Gloamwood Heart | 36% / 4 | siege | Win 124.7s · 2★ | Yes | — |
| 42 | Bridge Bastion | 38% / 4 | siege | Win 79.5s · 2★ | Yes | — |
| 43 | Breach Yard | 41% / 4 | siege | Win 119.7s · 2★ | Yes | — |
| 44 | Crownward Gate | 44% / 4 | siege | Win 95.6s · 2★ | Yes | — |
| 45 | Inner Ring | 36% / 4 | siege | Win 114.5s · 2★ | Yes | — |
| 46 | Crownfall Keep | 38% / 4 | siege | Win 205.3s · 2★ | Yes | Win 162.0s |
| 47 | Lantern Requiem | 41% / 4 | courage | Win 110.8s · 2★ | Yes | — |
| 48 | Deadwake Armada | 44% / 4 | siege | Win 145.6s · 2★ | Yes | — |
| 49 | Forge Crown | 36% / 4 | siege | Win 145.2s · 2★ | Yes | — |
| 50 | Ashen Remnant | 38% / 4 | repair | Loss 224.5s · 0★ | Yes | Win 213.9s |
| 51 | Whiteout Bastion | 41% / 4 | courage | Win 144.7s · 1★ | Yes | — |
| 52 | Reliquary Cataclysm | 44% / 4 | repair | Loss 105.6s · 0★ | Yes | Win 271.1s |
| 53 | Blackwater Procession | 36% / 4 | repair | Win 217.3s · 1★ | Yes | Loss 149.0s |
| 54 | Warhorn Expanse | 38% / 4 | courage | Win 146.5s · 1★ | Yes | — |
| 55 | Moonless Snare | 41% / 4 | siege | Win 176.7s · 2★ | Yes | — |
| 56 | Ashen Throne | 44% / 4 | siege | Win 257.7s · 2★ | Yes | Win 182.5s |
| 57 | Bellgrave Siege | 36% / 4 | courage | Win 159.4s · 1★ | Yes | — |
| 58 | Leviathan Wake | 38% / 4 | siege | Win 245.6s · 2★ | Yes | Win 254.6s |
| 59 | Molten Ascension | 41% / 4 | siege | Win 222.6s · 1★ | Yes | — |
| 60 | Pale Crown | 44% / 4 | repair | Loss 246.9s · 0★ | No | Win 207.9s |

Specialist squads: stages 25/26/46/50 use Defender, Grenadier, Coordinator; 52/53/56 use Lantern Guard, Ballista, Coordinator; 58/60 use Lantern Guard, Ballista, Stormcaller.

## Reproduce

From the project root:

```sh
dotnet build --no-restore
godot --headless --path . --fixed-fps 60 res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-field-check --field-objectives
godot --headless --path . --fixed-fps 60 res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-camera-check --camera
godot --headless --path . --fixed-fps 60 res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-campaign-check --tactical --field-tactics --time-limit=300
python3 scripts/analysis/expanded_campaign_review.py
```

Raw final logs and the machine-readable report with source hashes are in `artifacts/expanded-campaign/`. The summarizer expects the recorded `final-early`, `final-late`, `final-repeat`, and `final-counter-*` logs. Earlier exploratory logs are retained separately and excluded from the final totals.
