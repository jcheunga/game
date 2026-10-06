# Campaign and combat review

The campaign and combat review set the rules below for encounters, targeting,
reinforcement pacing, boss phases and hazards. They apply to the 100-stage
campaign: ten zones of ten stages, each zone's boss in slot 10 (see
[CAMPAIGN_PLAN.md](../CAMPAIGN_PLAN.md)). Fixed-seed sweeps of every stage are in
[COMBAT_BENCHMARK.md](COMBAT_BENCHMARK.md). Evidence from the earlier 60-stage
campaign, including the direct-input playtest, is archived in
[archive/COMBAT_BENCHMARK_60_STAGE.md](archive/COMBAT_BENCHMARK_60_STAGE.md) and
[archive/DIRECT_PLAYTEST.md](archive/DIRECT_PLAYTEST.md).

## Battle rules

- Destroying the enemy stronghold wins at once, whatever waves remain; a living
  commander does not block victory. The war wagon's hull reaching 0 is a defeat.
  Endless runs end on defeat or retreat.
- Every mode starts at 0 courage. Courage regenerates 3 per second up to 100
  (`data/combat_config.json`), before upgrades, bonuses and stage modifiers. The
  Surplus Courage boon in Endless speeds regeneration rather than granting an
  opening budget.
- Tapping a ready card sends that unit out of the wagon's door
  ([DEPLOYMENT_CARDS.md](DEPLOYMENT_CARDS.md)). Every enemy enters from the
  enemy stronghold.
- Only melee troops and units flagged `DamagesStructures` damage a base; other
  ranged troops hold at firing range.
- Melee troops engage nearby blockers before chasing support enemies. Ranged
  troops keep support targeting, and the Rogue's backstab prefers the rearmost
  enemy.
- Enemies keep their authored attack rhythms. Stage scaling raises health and
  damage instead: from 0.90× at stage 1 to about 2.07× health and 1.57× damage
  at the hardest stages, and stronghold health from 380 to 1,488
  (`data/stages.json`). Extra damage against the wagon is bounded. Endless caps
  its attack-speed growth at 15% of a unit's cooldown.
- Scripted waves release in order, at least 0.35 seconds apart. Once the field
  is clear, the next wave arrives within four seconds. While spawns are queued
  or more than a third of the enemy cap (at least two enemies) is alive, the
  next wave holds until the pressure thins.

## Bosses and hazards

- A boss phase change warns for 1.6 seconds with an amber ring around the
  commander, which stands still until the phase starts.
- On a campaign field each summoner, bosses included, calls at most three
  reinforcements. Reliquary Tyrant (bone ballistas), Harrow Tidemaster (plague
  engines) and, in the campaign, Iron Warden (brutes) keep at most two of their
  summoned unit alive, counting any already on the field, and replace
  casualties.
- On a campaign field, rally, raise-the-fallen and jam specials wait until the
  fighting reaches the caster: within 200 units of the wagon or of a player
  troop.
- Hexer jams do not stack their card penalties, and a new jam cannot start
  within three seconds of the last one ending.
- Shield Walls block player shots at enemies within their radius when they
  stand between the shooter and the target.
- Tunnelers burrow behind the rearmost player troop.
- Cursed ground damages player troops inside its marked area: authored patches
  on campaign stages, a central strip that leaves the band's edges clear
  elsewhere.

## Battle screen

- Wagon health and courage sit at the upper left and gold at the upper right.
  Deployment cards sit individually over the scene. Battle text is combat
  numbers only.
- The pause button at the top right or Escape pauses the battle with Resume, Restart, Game
  settings and Quit battle. Restart shows the stage's ration cost. Menu restarts
  and result-screen retries charge it once and refuse without enough rations;
  shared matches cannot restart individually. Game settings opens Sound and
  Gameplay inside the paused battle. Quitting releases the pause before the
  scene changes and returns to the map; Endless banks its payout once.
- Campaign victory shows earned stars and icon reward cards built from the
  resources, experience and mastery actually awarded. Showing the result twice
  cannot award it twice.
- Troops and structures cast soft shadows that follow the current animation
  frame. Zone light colour and lantern light tint bodies; health bars keep their
  UI colours.

## Zone progression

Each enemy first appears at the stage shown; earlier stages field a role-alike
instead (`scripts/analysis/expand_campaign.py`).

| Zone | Stages | New enemies (first stage) | Bosses (stage) |
| --- | --- | --- | --- |
| King's Road | 1–10 | Risen (1), Ghouls (2), Grave Brutes and Blight Casters (4) | Grave Lord (10) |
| Saltwake Docks | 11–20 | Rot Hulks (11), Bone Nests (12), Bone Juggernauts and Dread Heralds (14) | Tidecaller (15), Harrow Tidemaster (20) |
| Emberforge March | 21–30 | Sappers (21) | Iron Warden (30) |
| Ashen Ward | 31–40 | Hexers (31), Shield Walls (34) | Plague Archon (35, 37), Plague Monarch (40) |
| Thornwall Pass | 41–50 | Tunnelers (42), Mirror Knights (46), Bone Ballistas (48) | Thornwall Chieftain (50) |
| Hollow Basilica | 51–60 | Liches (55), Catacomb Giants (58) | Bone Pontiff (55), Reliquary Tyrant (60) |
| Mire of Saints | 61–70 | Siege Towers (66) | Mire Behemoth (70) |
| Sunfall Steppe | 71–80 | — | Steppe Warlord (80) |
| Gloamwood Verge | 81–90 | — | Gloamwood Witch (90) |
| Crownfall Citadel | 91–100 | Revenant Captains (97) | Dread Sovereign (95), Ashen Regent (100) |

From stage 51 a timed district condition presses the caravan. From stage 61
waves adapt to the battle and bosses press harder, and from stage 81 both turn
elite (`scripts/core/CampaignPacing.cs`).

## Verification

- The real-engine regression run covers summoning during simulation, boss
  phases, stronghold victory with a living commander, the shared artillery
  limit, Shield Wall interception, target selection, cursed ground, jam
  recovery, spawn scheduling, stage stars, base weapons, the battle band and
  courage pacing.
- The basic and tactical benchmark profiles sweep every stage with a fixed seed
  per stage from an isolated save. Losses and timeouts are balance
  observations, not test failures; see [COMBAT_BENCHMARK.md](COMBAT_BENCHMARK.md)
  for the reference player and results.
- These are automated checks. They do not estimate human win rates or replace
  play on physical devices.

## Reproduce

```sh
./scripts/smoke/combat_review_smoke.sh
./scripts/smoke/combat_review_smoke.sh --campaign
./scripts/verify_all.sh
```

Logs are written under `artifacts/combat-review/`. `CombatReviewSmoke` accepts
`--stages=...`, `--tactical` and `--squad=unit_id,unit_id,unit_id`, plus the
focused `--courage-pacing`, `--base-weapons`, `--camera` and `--lanes` reviews.
Rendered checks use a graphical run with `--regressions --screenshots`.
`UiReviewSmoke` adds `--battle-polish` (HUD and pause menu at desktop,
smaller-window and phone sizes), `--battle-lighting` (shadows and lighting in
all ten zones) and `--battle-cleanup` (results and return screens). Every
invocation needs a unique `--save-suffix=...`.
