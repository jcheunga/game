# Developer tools

Debug builds and the editor only · Settings → Account → Developer tools

Developer mode starts off. While it is off, every tool is disabled, the campaign plays its standard waves and battles run at normal speed. Isolated test saves and release builds never see the tools. The mode, the chosen wave version and the battle speed save with the local save.

## Settings page

| Section | Tools |
| --- | --- |
| Enemy waves | Standard, Horde or Regional for every campaign stage, from the next battle |
| Supplies | Gold, food, Essence, relic shards and sigils |
| Player level | +1 level, +10 levels, max level (rewards wait on the level track) |
| Campaign | Win next stage, win to the zone boss, win every stage, open every zone |
| Warband | Recruit every unit, spell and tool; train to the level cap, with spells and the war wagon |
| Battle and time | Battle speed 1×, 2×, 4× or 8×; skip 1 or 8 hours of food recharge and expeditions |

A won stage counts as a three-star victory with its normal gold, food, XP, map reveal and unlocks. Fast-forwarded wins skip the cloud backup and analytics. **Open every zone** charts the plain ground in all ten zones, so every stage, cache and find sits on the frontier. Nothing is won or gathered, and boss gates still wait for their zone's leaders.

## In battle

With developer mode on, the pause menu adds **Win battle**, **Lose battle** (**End run** in endless and boss rush), **Fill courage and mana** and a speed button. A developer speed-up drops back to 1× on the result screen. Shared LAN and online rooms get none of these.

## Wave versions

| Version | File | Shape |
| --- | --- | --- |
| Standard | `data/stages.json` | The campaign's own waves, rebuilt by `scripts/tools/revamp_content.py` |
| Horde | `data/waves/horde.json` | Many light bodies on short gaps around each standard wave's featured enemies; grave rats join from stage 5 |
| Regional | `data/waves/regional.json` | Each zone's four regional enemies beside the opening four, in fewer, heavier packs; newcomers at slots 2, 4, 6 and 8 |

Version files list every zone and its stages, with one line per wave entry. Edit them by hand: `MapName` and `StageName` are labels for reading only. A stage left out of a version keeps its standard waves. A new file in `data/waves/` with a unique `Id`, `Name` and `Order` appears as another choice.

`python3 scripts/tools/build_wave_versions.py` writes Horde and Regional from the current standard waves, but only when the file is missing. `--force` rebuilds them and loses hand edits.

The data check (`server: dotnet run -- --test-data ../data`) rejects a version that names a non-enemy unit, a stage under the wrong zone, an empty wave or waves out of time order. At runtime an unknown unit is skipped with a warning, so a typo cannot break a battle. Neither version has been balance-tested. Horde fields about four times the standard enemy count, and Regional brings 30 enemy types the standard campaign never uses.

## Tests

`godot --path . res://scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-dev --developer` taps every settings tool and the battle pause tools. The `--home-map` review runs the settings part too. Add `--small-window --window=844x390` for the phone layout.
