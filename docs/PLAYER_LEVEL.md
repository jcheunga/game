# Player level and nickname

Implemented · 15 October 2026

## Nickname

A new caravan chooses a nickname on the home map before playing. The field starts with a suggested name such as "Ashen Warden", and **Suggest** rolls another. Names are 3–18 letters, numbers, spaces, hyphens or underscores. Other players see the nickname on rankings and in rooms.

- **Rename:** tap the name on the level track, use **Change nickname** in the profile, or edit **Settings → Gameplay → Nickname**.
- **Account creation:** the account dialog has a nickname field, and a new account is created with that name. Signing in to an existing account restores that account's own name.
- **Saves from before nicknames:** a save that still uses the default "Lantern" is asked once; a save with any other name keeps it.
- **Test runs:** runs with `--save-suffix` or `--save-file` skip the prompt.

## Levels and XP

There are 100 levels. Level `L` needs `90 + 5 × (L − 1)` XP to reach the next one, so level 100 takes 33,165 XP in total. Every finished battle pays XP before its own results apply. Retreats pay nothing, except in endless and boss rush, where withdrawing banks the run.

| Battle | XP |
| --- | --- |
| Campaign, first win of a stage | 40 + stage |
| Campaign, replay win | 15 + stage ÷ 3 |
| Campaign, defeat | 8 + stage ÷ 10 |
| Endless | 10 + 4 × wave reached (max 250) |
| Boss rush | 10 + 15 × commanders defeated (max 220) |
| Tower | win 40 + floor, loss 12 |
| Arena | win 60, loss 20 |
| Seasonal event | win 60, loss 12 |
| Challenges and rooms | win 40, loss 12 |

Clearing every campaign stage once reaches about level 46. The rest of the track comes from replays and the other modes.

## Caps

- **Unit training:** at level 1, units can train to level 2. Every second level adds one (levels 3, 5, 7 … 37), reaching the unit maximum of 20 at level 37.
- **War wagon rank:** at level 1, each wagon upgrade can reach level 1. Levels 5, 12, 20 and 30 raise this to 2, 3, 4 and 5.

Clearing each stage once keeps the unit cap at or above `CampaignProgressionCatalog.SuggestedLevel` for every stage, usually two or three levels above it. The combat regressions check this. Campaign contract rewards may grant units above the cap. Training a save had before levels existed is never lowered.

The armory names the level that lifts a cap: a unit shows **Player level N**, and a wagon upgrade shows **PLAYER LV N** with a padlock.

## Rewards

Every level, including level 1, has a reward. Rewards stay on the level track until claimed, one at a time or all at once with **Claim all**.

| Level | Reward |
| --- | --- |
| 1 | 100 gold (welcome purse) |
| Every 10th | 1 + L ÷ 25 sigils |
| Other 5th | 2 + L ÷ 10 relic shards |
| Other 4th | 2 + L ÷ 25 essence |
| Other 3rd | 6 + L ÷ 10 food |
| The rest | 40 + 10 × L gold |

A save from before levels starts at the level its won stages earn, counting each as a first clear. Those levels' rewards are waiting to be claimed. Player level survives prestige.

## Where it shows

- **Home map:** a player card to the left of the settings ring shows the medallion, nickname and XP. The medallion's ring is the XP bar, and a red badge counts rewards to claim. Tapping the card opens the level track.
- **Level track:** `PlayerLevelMenu` uses the season board's plate.
  - **Grid:** a scrolling grid holds all hundred levels, ten to a row. Each cell shows the level, its reward and a claim button, or a claimed or locked state. The current level has a gold rim and every tenth level a bronze one.
  - **Cap badges:** small badges in a cell's corner mark the caps that level lifts: a helmet for units, a hammer for the wagon.
  - **Rail:** the five badges jump to levels 1, 21, 41, 61 and 81.
  - **Footer:** shows the current caps and the next level that lifts one.
- **Result board:** a level card shows the XP earned, the bar and **LEVEL UP · N** when a level was gained. It sits beside the mastery card when there is one.

## Code and checks

- `PlayerLevelCatalog` holds the curve, caps, rewards and XP table.
- `GameState.PlayerLevel.cs` holds state, claims and nickname rules. Save version 48 adds `PlayerXP`, `ClaimedLevelRewards` and `HasChosenCallsign`.
- `BattleController.AwardPlayerXP` is called from `EndBattle` and from the endless and boss-rush retreats.

```sh
godot --headless --path . --fixed-fps 60 res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-levels --regressions
godot --path . --windowed --resolution 1280x720 res://scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-levels --player-level
godot --path . --windowed --resolution 844x390 res://scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-levels-phone --player-level --mobile-preview
```
